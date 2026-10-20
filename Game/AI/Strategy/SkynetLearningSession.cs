using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security;

namespace Skynet.Game.AI.Strategy
{
    [DataContract]
    public sealed class SkynetModel
    {
        [DataMember(Name = "format")]
        public string Format { get; set; }
        [DataMember(Name = "featureSchema")]
        public string FeatureSchema { get; set; }
        [DataMember(Name = "version")]
        public string Version { get; set; }
        [DataMember(Name = "weights")]
        public Dictionary<string, double> Weights { get; set; }
    }

    [DataContract]
    public sealed class SkynetDecisionRecord
    {
        [DataMember(Name = "features")]
        public List<Dictionary<string, double>> Features { get; set; }
        [DataMember(Name = "baselineScores")]
        public List<double> BaselineScores { get; set; }
        [DataMember(Name = "probabilities")]
        public List<double> Probabilities { get; set; }
        [DataMember(Name = "chosen")]
        public int Chosen { get; set; }
    }

    [DataContract]
    public sealed class SkynetEpisode
    {
        [DataMember(Name = "format")]
        public string Format { get; set; }
        [DataMember(Name = "featureSchema")]
        public string FeatureSchema { get; set; }
        [DataMember(Name = "id")]
        public string Id { get; set; }
        [DataMember(Name = "sourceModel")]
        public string SourceModel { get; set; }
        [DataMember(Name = "policyRevision", IsRequired = true)]
        public string PolicyRevision { get; set; }
        [DataMember(Name = "group")]
        public string Group { get; set; }
        [DataMember(Name = "result")]
        public int Result { get; set; }
        [DataMember(Name = "truncated")]
        public bool Truncated { get; set; }
        [DataMember(Name = "decisions")]
        public List<SkynetDecisionRecord> Decisions { get; set; }
    }

    /// <summary>
    /// Scores only the caller's legal, supported choices. Training explores those choices
    /// and saves a completed episode; an offline trainer creates a separate candidate model.
    /// Frozen inference never creates directories, episodes, or mutable model checkpoints.
    /// </summary>
    public sealed class SkynetLearningSession
    {
        public const string FeatureSchema = "skynet-public-v1";
        public const string ModelFormat = "skynet-linear-v1";
        public const string EpisodeFormat = "skynet-episode-v1";
        public const string PolicyRevision = "skynet-policy-v2";
        private const int MaximumDecisions = 4096;
        private static readonly string[] FeatureNames =
        {
            "develop", "pressure", "advantage", "interaction", "recovery",
            "protection", "conserve", "risk", "synergy"
        };

        private readonly bool _training;
        private readonly string _dataDirectory;
        private readonly Random _random;
        private readonly SkynetModel _model;
        private SkynetEpisode _episode;
        private bool _completed;

        // Set by the host to a deck/encounter cohort before BeginDuel. Grouped holdouts
        // keep related episodes together. No card identities are included in the trace.
        public string TrainingGroup { get; set; }
        public string LastEpisodePath { get; private set; }
        public string ModelVersion { get { return _model.Version; } }
        public bool Training { get { return _training; } }

        public SkynetLearningSession(string modelPath, bool training, string dataDirectory, int seed)
        {
            if (training && string.IsNullOrWhiteSpace(dataDirectory))
                throw new ArgumentException("Training requires an episode directory.", "dataDirectory");
            _training = training;
            _dataDirectory = dataDirectory;
            _random = new Random(seed);
            _model = LoadModel(modelPath);
        }

        public void BeginDuel()
        {
            _completed = false;
            LastEpisodePath = null;
            _episode = _training ? new SkynetEpisode
            {
                Format = EpisodeFormat,
                FeatureSchema = FeatureSchema,
                Id = Guid.NewGuid().ToString("N"),
                SourceModel = _model.Version,
                PolicyRevision = PolicyRevision,
                Group = TrainingGroup ?? "",
                Decisions = new List<SkynetDecisionRecord>()
            } : null;
        }

        public int Choose(IList<IDictionary<string, double>> features, IList<double> baselineScores)
        {
            if (features == null || baselineScores == null || features.Count == 0
                || features.Count != baselineScores.Count)
                throw new ArgumentException("Each candidate requires features and a baseline score.");

            double[] scores = new double[features.Count];
            int best = 0;
            for (int i = 0; i < features.Count; ++i)
            {
                ValidateFeatures(features[i]);
                double score = baselineScores[i];
                if (!IsFinite(score))
                    throw new ArgumentException("Candidate scores must be finite.", "baselineScores");
                foreach (KeyValuePair<string, double> feature in features[i])
                {
                    double weight;
                    if (_model.Weights.TryGetValue(feature.Key, out weight))
                        score += weight * feature.Value;
                }
                if (!IsFinite(score))
                    throw new ArgumentException("Candidate score overflow.");
                scores[i] = score;
                if (score > scores[best])
                    best = i;
            }
            if (!_training || _episode == null || _completed)
                return best;

            // Subtracting the largest logit keeps exp finite even for extreme scores.
            double[] probabilities = new double[scores.Length];
            double total = 0;
            for (int i = 0; i < scores.Length; ++i)
            {
                probabilities[i] = Math.Exp(scores[i] - scores[best]);
                total += probabilities[i];
            }
            double draw = _random.NextDouble();
            int chosen = best;
            double cumulative = 0;
            for (int i = 0; i < probabilities.Length; ++i)
            {
                probabilities[i] /= total;
                cumulative += probabilities[i];
                if (draw < cumulative)
                {
                    chosen = i;
                    draw = double.PositiveInfinity;
                }
            }
            if (_episode.Decisions.Count < MaximumDecisions)
            {
                _episode.Decisions.Add(new SkynetDecisionRecord
                {
                    Features = features.Select(vector => new Dictionary<string, double>(vector)).ToList(),
                    BaselineScores = baselineScores.ToList(),
                    Probabilities = probabilities.ToList(),
                    Chosen = chosen
                });
            }
            else
                _episode.Truncated = true;
            return chosen;
        }

        public void CompleteDuel(int localResult)
        {
            if (localResult < 0 || localResult > 2)
                throw new ArgumentOutOfRangeException("localResult");
            if (_completed)
                return;
            _completed = true;
            if (!_training || _episode == null || _episode.Decisions.Count == 0)
                return;
            _episode.Result = localResult;
            string temporaryPath = null;
            try
            {
                Directory.CreateDirectory(_dataDirectory);
                string finalPath = Path.Combine(_dataDirectory, _episode.Id + ".json");
                temporaryPath = finalPath + ".tmp";
                using (FileStream stream = new FileStream(temporaryPath, FileMode.CreateNew,
                    FileAccess.Write, FileShare.None))
                {
                    Serializer(typeof(SkynetEpisode)).WriteObject(stream, _episode);
                    stream.Flush(true);
                }
                File.Move(temporaryPath, finalPath);
                LastEpisodePath = finalPath;
            }
            catch (IOException exception)
            {
                Logger.WriteLine("SKYNET could not save its completed training episode: " + exception.Message);
            }
            catch (UnauthorizedAccessException exception)
            {
                Logger.WriteLine("SKYNET could not save its completed training episode: " + exception.Message);
            }
            catch (SecurityException exception)
            {
                Logger.WriteLine("SKYNET could not save its completed training episode: " + exception.Message);
            }
            finally
            {
                // Unique temporary files belong only to this session. A failed rename must
                // not expose a partial episode or remove any other session's completed data.
                if (temporaryPath != null && File.Exists(temporaryPath))
                {
                    try { File.Delete(temporaryPath); }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                    catch (SecurityException) { }
                }
                _episode = null;
            }
        }

        private static SkynetModel LoadModel(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return new SkynetModel
                {
                    Format = ModelFormat,
                    FeatureSchema = FeatureSchema,
                    Version = "heuristic-v1",
                    Weights = new Dictionary<string, double>()
                };
            SkynetModel model;
            try
            {
                using (FileStream stream = File.OpenRead(path))
                {
                    if (stream.Length > 1024 * 1024)
                        throw new InvalidDataException("SKYNET model exceeds the 1 MiB size limit.");
                    model = (SkynetModel)Serializer(typeof(SkynetModel)).ReadObject(stream);
                }
            }
            catch (SerializationException exception)
            {
                throw new InvalidDataException("SKYNET model is malformed JSON.", exception);
            }
            if (model == null || model.Format != ModelFormat || model.FeatureSchema != FeatureSchema
                || string.IsNullOrWhiteSpace(model.Version) || model.Weights == null)
                throw new InvalidDataException("SKYNET model format or feature schema is incompatible.");
            foreach (KeyValuePair<string, double> weight in model.Weights)
            {
                if (!FeatureNames.Contains(weight.Key) || !IsFinite(weight.Value) || Math.Abs(weight.Value) > 20)
                    throw new InvalidDataException("SKYNET model has an unknown feature or invalid weight.");
            }
            return model;
        }

        private static void ValidateFeatures(IDictionary<string, double> features)
        {
            if (features == null)
                throw new ArgumentException("Candidate features cannot be null.", "features");
            foreach (KeyValuePair<string, double> feature in features)
            {
                if (!FeatureNames.Contains(feature.Key) || !IsFinite(feature.Value)
                    || Math.Abs(feature.Value) > 100)
                    throw new ArgumentException("Candidate feature is unknown, nonfinite, or out of bounds.");
            }
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static DataContractJsonSerializer Serializer(Type type)
        {
            return new DataContractJsonSerializer(type,
                new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true });
        }
    }
}
