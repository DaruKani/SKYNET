using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Threading.Tasks;
using Skynet;
using Skynet.Game;
using Skynet.Game.AI;
using Skynet.Game.AI.Strategy;
using YGOSharp.OCGWrapper;
using YGOSharp.OCGWrapper.Enums;

namespace Skynet.Tests
{
    internal static class Program
    {
        private static int _passed;
        private static string _root;

        private static int Main(string[] args)
        {
            _root = Path.Combine(Path.GetTempPath(), "SkynetTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            try
            {
                Run("Frozen policy is greedy and never writes", FrozenPolicy);
                Run("Checkpoint weights influence selection", CheckpointSelection);
                Run("Malformed and incompatible checkpoints are rejected", InvalidModels);
                Run("Completed episodes have normalized finite probabilities", EpisodeProbabilities);
                Run("Feature snapshots cannot change after a decision", FeatureSnapshot);
                Run("Draw is preserved and terminal notification is idempotent", DrawAndDuplicate);
                Run("Aborted and empty duels do not produce training data", AbortedAndEmpty);
                Run("Independent sessions cannot overwrite each other's episodes", ConcurrentSessions);
                Run("Nonfinite, unknown and malformed features are rejected", InvalidCandidates);
                if (args.Length > 0)
                {
                    NamedCardsManager.Init(Path.GetFullPath(args[0]));
                    Run("Opponent hidden identities do not affect public action scores", HiddenInformation);
                    Run("Removal utility follows visible board advantage", RemovalUtility);
                    Run("Unsupported optional effects remain outside admitted choices", UnsupportedOptional);
                    Run("Forced unknown chains still choose an offered mandatory card", MandatoryChain);
                    Run("Resolution choices use the activation snapshot and legal candidates", ResolutionSelection);
                }
                else
                    Console.WriteLine("Database-backed policy fixtures skipped; pass cards.cdb path to include them.");
                Console.WriteLine("SKYNET assertions passed: " + _passed);
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(exception);
                return 1;
            }
            finally
            {
                string fullRoot = Path.GetFullPath(_root);
                string allowedPrefix = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)
                    + Path.DirectorySeparatorChar + "SkynetTests-";
                if (fullRoot.StartsWith(allowedPrefix, StringComparison.OrdinalIgnoreCase)
                    && Directory.Exists(fullRoot))
                    Directory.Delete(fullRoot, true);
            }
        }

        private static void Run(string name, Action test)
        {
            test();
            ++_passed;
            Console.WriteLine("PASS " + name);
        }

        private static List<IDictionary<string, double>> Features()
        {
            return new List<IDictionary<string, double>>
            {
                new Dictionary<string, double> { { "develop", 1 } },
                new Dictionary<string, double> { { "pressure", 1 } }
            };
        }

        private static void FrozenPolicy()
        {
            string data = Path.Combine(_root, "frozen");
            SkynetLearningSession session = new SkynetLearningSession(null, false, data, 1);
            session.BeginDuel();
            Equal(1, session.Choose(Features(), new[] { 0.0, 2.0 }));
            session.CompleteDuel(0);
            Assert(!Directory.Exists(data), "Frozen inference created an output directory.");
            Assert(session.LastEpisodePath == null, "Frozen inference saved an episode.");
        }

        private static void CheckpointSelection()
        {
            string path = Path.Combine(_root, "model.json");
            File.WriteAllText(path, "{\"format\":\"skynet-linear-v1\",\"featureSchema\":\"skynet-public-v1\","
                + "\"version\":\"fixture-v1\",\"weights\":{\"develop\":3.0}}");
            SkynetLearningSession session = new SkynetLearningSession(path, false, null, 1);
            Equal(0, session.Choose(Features(), new[] { 0.0, 2.0 }));
            Equal("fixture-v1", session.ModelVersion);
        }

        private static void InvalidModels()
        {
            string path = Path.Combine(_root, "invalid.json");
            foreach (string json in new[]
            {
                "{}",
                "{\"format\":\"skynet-linear-v1\",\"featureSchema\":\"wrong\",\"version\":\"v\",\"weights\":{}}",
                "{\"format\":\"skynet-linear-v1\",\"featureSchema\":\"skynet-public-v1\",\"version\":\"v\",\"weights\":{\"secret\":1}}",
                "{\"format\":\"skynet-linear-v1\",\"featureSchema\":\"skynet-public-v1\",\"version\":\"v\",\"weights\":{\"risk\":21}}"
            })
            {
                File.WriteAllText(path, json);
                Throws<InvalidDataException>(() => new SkynetLearningSession(path, false, null, 1));
            }
        }

        private static void EpisodeProbabilities()
        {
            SkynetLearningSession session = TrainingSession("probabilities", 7);
            session.BeginDuel();
            Equal(1, session.Choose(Features(), new[] { -1e200, 1e200 }));
            session.Choose(Features(), new[] { 0.0, 0.0 });
            session.CompleteDuel(0);
            SkynetEpisode episode = ReadEpisode(session.LastEpisodePath);
            Equal(2, episode.Decisions.Count);
            foreach (SkynetDecisionRecord decision in episode.Decisions)
            {
                Assert(Math.Abs(decision.Probabilities.Sum() - 1) < 1e-12, "Probabilities do not sum to one.");
                Assert(decision.Probabilities.All(p => !double.IsNaN(p) && !double.IsInfinity(p) && p >= 0),
                    "Invalid probability.");
            }
            Equal(1.0, episode.Decisions[0].Probabilities[1]);
            Equal(0.5, episode.Decisions[1].Probabilities[0]);
            Assert(!Directory.GetFiles(Path.GetDirectoryName(session.LastEpisodePath), "*.tmp").Any(),
                "Temporary training file was left behind.");
        }

        private static void FeatureSnapshot()
        {
            SkynetLearningSession session = TrainingSession("snapshot", 1);
            session.BeginDuel();
            List<IDictionary<string, double>> features = Features();
            session.Choose(features, new[] { 0.0, 0.0 });
            features[0]["develop"] = 99;
            session.CompleteDuel(0);
            Equal(1.0, ReadEpisode(session.LastEpisodePath).Decisions[0].Features[0]["develop"]);
        }

        private static void DrawAndDuplicate()
        {
            SkynetLearningSession session = TrainingSession("draw", 1);
            session.BeginDuel();
            session.Choose(Features(), new[] { 0.0, 0.0 });
            session.CompleteDuel(2);
            string path = session.LastEpisodePath;
            session.CompleteDuel(0);
            Equal(2, ReadEpisode(path).Result);
            Equal(1, Directory.GetFiles(Path.GetDirectoryName(path), "*.json").Length);
        }

        private static void AbortedAndEmpty()
        {
            SkynetLearningSession session = TrainingSession("aborted", 1);
            session.BeginDuel();
            session.Choose(Features(), new[] { 0.0, 0.0 });
            Assert(session.LastEpisodePath == null, "Unfinished duel saved an episode.");
            session.BeginDuel();
            session.CompleteDuel(1);
            Assert(!Directory.Exists(Path.Combine(_root, "aborted")), "Empty duel saved an episode.");
        }

        private static void ConcurrentSessions()
        {
            Parallel.For(0, 8, index =>
            {
                SkynetLearningSession session = TrainingSession("concurrent", index);
                session.BeginDuel();
                session.Choose(Features(), new[] { 0.0, 0.0 });
                session.CompleteDuel(index % 3);
                Assert(session.LastEpisodePath != null, "Session failed to save.");
            });
            string[] files = Directory.GetFiles(Path.Combine(_root, "concurrent"), "*.json");
            Equal(8, files.Length);
            Equal(8, files.Select(path => ReadEpisode(path).Id).Distinct().Count());
        }

        private static void InvalidCandidates()
        {
            SkynetLearningSession session = new SkynetLearningSession(null, false, null, 1);
            Throws<ArgumentException>(() => session.Choose(Features(), new[] { double.NaN, 0.0 }));
            List<IDictionary<string, double>> features = Features();
            features[0]["unknown"] = 1;
            Throws<ArgumentException>(() => session.Choose(features, new[] { 0.0, 0.0 }));
            Throws<ArgumentException>(() => session.Choose(Features(), new[] { 0.0 }));
            Throws<ArgumentOutOfRangeException>(() => session.CompleteDuel(-1));
        }

        private static Duel FixtureDuel()
        {
            Duel duel = new Duel();
            duel.Fields[0].Init(40, 0, 0);
            duel.Fields[1].Init(40, 0, 1);
            duel.Fields[0].LifePoints = 8000;
            duel.Fields[1].LifePoints = 8000;
            return duel;
        }

        private static ClientCard FixtureCard(Duel duel, int id, int controller, CardLocation location,
            CardPosition position = CardPosition.FaceUpAttack)
        {
            ClientCard card = new ClientCard(id, location, 0, (int)position) { Controller = controller };
            NamedCard data = NamedCard.Get(id);
            if (data == null)
                throw new InvalidOperationException("Fixture database lacks card " + id + ".");
            using (MemoryStream stream = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
                {
                    writer.Write((int)(Query.Type | Query.Level | Query.Attack | Query.Defence));
                    writer.Write(data.Type);
                    writer.Write(data.Level);
                    writer.Write(data.Attack);
                    writer.Write(data.Defense);
                }
                stream.Position = 0;
                using (BinaryReader reader = new BinaryReader(stream))
                    card.Update(reader, duel);
            }
            return card;
        }

        private static SkynetAgent FixtureExecutor(Duel duel, out GameAI ai)
        {
            GameClient client = new GameClient(new AgentOptions
            {
                Chat = false,
                Deck = "SKYNET",
                Dialog = "default"
            });
            ai = new GameAI(client, duel);
            SkynetAgent executor = new SkynetAgent(ai, duel);
            ai.Executor = executor;
            return executor;
        }

        private static void HiddenInformation()
        {
            Duel duel = FixtureDuel();
            SkynetPolicy policy = new SkynetPolicy(duel);
            ClientCard raigeki = FixtureCard(duel, 12580477, 0, CardLocation.Hand);
            LegalAction offer = new LegalAction(ExecutorType.Activate, raigeki, -1, 0);
            duel.Fields[1].MonsterZone[0] = FixtureCard(duel, 89631139, 1, CardLocation.MonsterZone,
                CardPosition.FaceDownDefence);
            ActionEvaluation before = policy.Evaluate(offer);
            duel.Fields[1].MonsterZone[0] = FixtureCard(duel, 40640057, 1, CardLocation.MonsterZone,
                CardPosition.FaceDownDefence);
            duel.Fields[1].Hand.Add(FixtureCard(duel, 46986414, 1, CardLocation.Hand));
            duel.Fields[1].Deck[0] = FixtureCard(duel, 89631139, 1, CardLocation.Deck);
            ActionEvaluation after = policy.Evaluate(offer);
            Equal(before.Score, after.Score);
            foreach (KeyValuePair<string, double> feature in before.Features)
                Equal(feature.Value, after.Features[feature.Key]);
            Equal(1.0, SkynetPolicy.CardValue(duel.Fields[1].MonsterZone[0]));
        }

        private static void RemovalUtility()
        {
            Duel duel = FixtureDuel();
            SkynetPolicy policy = new SkynetPolicy(duel);
            LegalAction raigeki = new LegalAction(ExecutorType.Activate,
                FixtureCard(duel, 12580477, 0, CardLocation.Hand), -1, 0);
            Assert(policy.Evaluate(raigeki) == null, "Board wipe was admitted on an empty board.");
            duel.Fields[1].MonsterZone[0] = FixtureCard(duel, 46986414, 1, CardLocation.MonsterZone);
            double oneMonster = policy.Evaluate(raigeki).Score;
            duel.Fields[1].MonsterZone[1] = FixtureCard(duel, 89631139, 1, CardLocation.MonsterZone);
            Assert(policy.Evaluate(raigeki).Score > oneMonster, "Removal did not value a stronger enemy board.");
            duel.Fields[1].MonsterZone[1] = null;
            duel.Fields[0].MonsterZone[0] = FixtureCard(duel, 89631139, 0, CardLocation.MonsterZone);
            LegalAction darkHole = new LegalAction(ExecutorType.Activate,
                FixtureCard(duel, 53129443, 0, CardLocation.Hand), -1, 0);
            Assert(policy.Evaluate(darkHole) == null, "Symmetric wipe destroyed a superior friendly board.");
            duel.Fields[1].SpellZone[0] = FixtureCard(duel, 55144522, 1, CardLocation.SpellZone);
            LegalAction mst = new LegalAction(ExecutorType.Activate,
                FixtureCard(duel, 5318639, 0, CardLocation.Hand), -1, 0);
            Assert(policy.Evaluate(mst) == null, "MST was treated as negation of a normal spell.");
        }

        private static void UnsupportedOptional()
        {
            Duel duel = FixtureDuel();
            GameAI ai;
            SkynetAgent executor = FixtureExecutor(duel, out ai);
            ClientCard kuriboh = FixtureCard(duel, 40640057, 0, CardLocation.Hand);
            LegalAction unsupported = new LegalAction(ExecutorType.Activate, kuriboh, -1, 3);
            LegalAction pass = new LegalAction(ExecutorType.Activate, null, -1, -1);
            Assert(ReferenceEquals(pass, executor.ChooseAction(new[] { unsupported, pass })),
                "Unsupported optional effect was admitted.");
            Equal(-1, ai.OnSelectChain(new[] { kuriboh }, new[] { -1 }, new[] { false }));
        }

        private static void MandatoryChain()
        {
            Duel duel = FixtureDuel();
            GameAI ai;
            FixtureExecutor(duel, out ai);
            ClientCard first = FixtureCard(duel, 40640057, 0, CardLocation.Grave);
            ClientCard second = FixtureCard(duel, 46986414, 0, CardLocation.Grave);
            int chosen = ai.OnSelectChain(new[] { first, second }, new[] { -1, -1 }, new[] { true, true });
            Assert(chosen == 0 || chosen == 1, "Mandatory chain did not choose a legal offered card.");
        }

        private static void ResolutionSelection()
        {
            Duel duel = FixtureDuel();
            GameAI ai;
            SkynetAgent executor = FixtureExecutor(duel, out ai);
            ClientCard source = FixtureCard(duel, 32807846, 0, CardLocation.SpellZone);
            duel.CurrentChain.Add(source);
            duel.CurrentChainInfo.Add(new ChainInfo(source, 0, -1));
            duel.SolvingChainIndex = 1;
            source.SetId(0);
            source.Controller = 1;
            ClientCard weak = FixtureCard(duel, 40640057, 0, CardLocation.Deck);
            ClientCard strong = FixtureCard(duel, 89631139, 0, CardLocation.Deck);
            IList<ClientCard> selected = executor.OnSelectCard(new[] { weak, strong }, 1, 1, HintMsg.AddToHand, false);
            Equal(1, selected.Count);
            Assert(ReferenceEquals(strong, selected[0]), "Resolution did not use its immutable source snapshot.");
            selected = executor.OnSelectCard(new[] { weak, strong }, 2, 2, HintMsg.Discard, false);
            Equal(2, selected.Count);
            Assert(selected.All(card => ReferenceEquals(card, weak) || ReferenceEquals(card, strong)),
                "Selection fabricated a candidate.");
        }

        private static SkynetLearningSession TrainingSession(string directory, int seed)
        {
            return new SkynetLearningSession(null, true, Path.Combine(_root, directory), seed)
            {
                TrainingGroup = "fixture-deck-versus-fixture-opponent"
            };
        }

        private static SkynetEpisode ReadEpisode(string path)
        {
            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(SkynetEpisode),
                new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true });
            using (FileStream stream = File.OpenRead(path))
                return (SkynetEpisode)serializer.ReadObject(stream);
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

        private static void Equal<T>(T expected, T actual)
        {
            Assert(EqualityComparer<T>.Default.Equals(expected, actual),
                "Expected " + expected + "; got " + actual + ".");
        }

        private static void Throws<T>(Action action) where T : Exception
        {
            try { action(); }
            catch (T) { return; }
            throw new InvalidOperationException("Expected exception " + typeof(T).Name + ".");
        }
    }
}
