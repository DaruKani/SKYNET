using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using YGOSharp.OCGWrapper.Enums;

namespace Skynet.Game.AI.Shadow
{
    public sealed class ShadowSession
    {
        public const int MaximumRows = 4096;
        public const int MaximumBytes = 16 * 1024 * 1024;
        public const int MaximumRowBytes = 1024 * 1024;
        public const int MaximumCards = 512;
        public const int MaximumOffers = 4096;

        private readonly string _directory;
        private readonly string _agent;
        private FileStream _stream;
        private string _duelId;
        private bool _disabled;
        private bool _counterObserved;
        private bool _isTag;
        private int _rows;
        private long _bytes;
        private int? _outcome;

        public bool Enabled
        {
            get { return !_disabled && _stream != null; }
        }

        public ShadowSession(string directory, string agent)
        {
            _directory = directory;
            _agent = agent;
        }

        public static string ResolveDirectory(string directory)
        {
            string root = Path.GetPathRoot(directory);
            if (!Path.IsPathRooted(directory) || string.IsNullOrEmpty(root)
                || (Path.DirectorySeparatorChar == '\\'
                && (root == "\\" || root == "/" || root.EndsWith(":", StringComparison.Ordinal))))
                throw new ArgumentException("ShadowData must be an explicitly selected absolute directory.");
            return Path.GetFullPath(directory);
        }

        public void Begin(bool isTag)
        {
            End(false, "next-duel-start");
            if (_disabled || string.IsNullOrWhiteSpace(_directory))
                return;
            try
            {
                string directory = ResolveDirectory(_directory);
                Directory.CreateDirectory(directory);
                _duelId = Guid.NewGuid().ToString("N");
                _stream = new FileStream(Path.Combine(directory, _duelId + ".jsonl"),
                    FileMode.CreateNew, FileAccess.Write, FileShare.Read);
                _rows = 0;
                _bytes = 0;
                _outcome = null;
                _counterObserved = false;
                _isTag = isTag;
                Write(new ShadowRecord
                {
                    Kind = "start",
                    Coverage = new[] { "select_idlecmd" },
                    MaxRows = MaximumRows,
                    MaxBytes = MaximumBytes,
                    MaxRowBytes = MaximumRowBytes
                });
            }
            catch (Exception exception)
            {
                Disable(exception.GetType().Name);
            }
        }

        public void OnMessage(GameMessage message)
        {
            if (!Enabled)
                return;
            if (message == GameMessage.AddCounter || message == GameMessage.RemoveCounter)
                _counterObserved = true;
            int value = (int)message;
            if ((value >= 10 && value <= 26 && message != GameMessage.SelectIdleCmd)
                || (value >= 140 && value <= 143))
            {
                Write(new ShadowRecord
                {
                    Kind = "unsupported-prompt",
                    Prompt = message.ToString(),
                    Unsupported = new List<string> { "prompt-not-captured" }
                });
            }
        }

        public void AddOffer(List<ShadowOffer> offers, int action, int index,
            int controller, int location, int sequence, int description, bool fieldOnly = false)
        {
            if (!Enabled || offers == null)
                return;
            try
            {
                if (offers.Count >= MaximumOffers)
                    throw new InvalidDataException("Offer limit.");
                string[] names = { "summon", "spsummon", "reposition", "mset", "sset", "activate", "battle", "end", "shuffle" };
                offers.Add(new ShadowOffer
                {
                    Id = offers.Count,
                    Type = names[action],
                    ActionCode = action,
                    Index = index,
                    WireResponse = (index << 16) + action,
                    Description = description,
                    FieldOnly = fieldOnly,
                    Card = action < 6 ? new ShadowCardReference
                    {
                        Controller = controller,
                        Location = location,
                        Sequence = sequence
                    } : null
                });
            }
            catch (Exception exception)
            {
                Disable(exception.GetType().Name);
            }
        }

        public ShadowRecord Prepare(Duel duel, List<ShadowOffer> offers)
        {
            if (!Enabled || offers == null)
                return null;
            try
            {
                ShadowRecord record = new ShadowRecord
                {
                    Kind = "decision",
                    Prompt = "select_idlecmd",
                    Offers = offers,
                    Unsupported = new List<string>(),
                    Observation = new ShadowObservation
                    {
                        Global = new ShadowGlobal
                        {
                            MyLp = duel.Fields[0].LifePoints,
                            OpLp = duel.Fields[1].LifePoints,
                            Turn = duel.Turn,
                            Phase = (int)duel.Phase,
                            IsFirst = duel.IsFirst,
                            IsMyTurn = duel.Player == 0
                        },
                        // ClientCard currently discards Query.Counters. Zero is
                        // a placeholder, never an assertion that no counter exists.
                        CounterStateAvailable = false
                    }
                };
                if (_isTag)
                    record.Unsupported.Add("tag-duel-not-supported");
                if (_counterObserved)
                    record.Unsupported.Add("counter-update-observed");
                if (duel.CurrentChain.Count != 0 || duel.SolvingChainIndex != 0)
                    record.Unsupported.Add("chain-state-incomplete");
                if (offers.Any(offer => offer.Type == "shuffle"))
                    record.Unsupported.Add("shuffle-action-not-supported");
                if (offers.Any(offer => offer.FieldOnly))
                    record.Unsupported.Add("field-only-effect-not-supported");

                for (int player = 0; player < 2; ++player)
                {
                    ClientField field = duel.Fields[player];
                    record.Observation.Counts.Add(new ShadowCounts
                    {
                        Deck = field.Deck.Count,
                        Hand = field.Hand.Count,
                        Extra = field.ExtraDeck.Count,
                        Grave = field.Graveyard.Count,
                        Banished = field.Banished.Count,
                        Monster = field.MonsterZone.Count(card => card != null),
                        Spell = field.SpellZone.Count(card => card != null)
                    });
                    CaptureZone(record, field.Deck, player, CardLocation.Deck);
                    CaptureZone(record, field.Hand, player, CardLocation.Hand);
                    CaptureZone(record, field.ExtraDeck, player, CardLocation.Extra);
                    CaptureZone(record, field.Graveyard, player, CardLocation.Grave);
                    CaptureZone(record, field.Banished, player, CardLocation.Removed);
                    CaptureZone(record, field.MonsterZone, player, CardLocation.MonsterZone);
                    CaptureZone(record, field.SpellZone, player, CardLocation.SpellZone);
                    if (field.HintDescriptions != null && field.HintDescriptions.Count > 0)
                        AddUnsupported(record, "player-hint-state-incomplete");
                }
                foreach (ShadowOffer offer in offers)
                {
                    ShadowCard observed = offer.Card == null ? null : record.Observation.Cards.FirstOrDefault(card =>
                        card.Controller == offer.Card.Controller && card.Location == offer.Card.Location
                        && card.Sequence == offer.Card.Sequence);
                    if (offer.Card != null && observed == null)
                        AddUnsupported(record, "offered-card-not-observed");
                    if (offer.ActionCode == 5 && (observed == null || !observed.Known))
                    {
                        offer.Description = -1;
                        AddUnsupported(record, "hidden-offer-description-redacted");
                    }
                }
                return record;
            }
            catch (Exception exception)
            {
                Disable(exception.GetType().Name);
                return null;
            }
        }

        private void CaptureZone(ShadowRecord record, IList<ClientCard> cards, int player, CardLocation location)
        {
            for (int sequence = 0; sequence < cards.Count; ++sequence)
            {
                ClientCard card = cards[sequence];
                if (card == null)
                    continue;
                if (record.Observation.Cards.Count >= MaximumCards)
                    throw new InvalidDataException("Card limit.");
                bool faceup = card.IsFaceup();
                bool known = card.Id > 0 && location != CardLocation.Deck
                    && (player == 0 || (location != CardLocation.Hand
                    && (location == CardLocation.Grave || faceup)));
                ShadowCard snapshot = new ShadowCard
                {
                    Known = known,
                    Controller = player,
                    Location = (int)location,
                    Sequence = sequence,
                    Position = location == CardLocation.Deck || location == CardLocation.Hand ? 0 : card.Position
                };
                if (known)
                {
                    snapshot.Code = card.Id;
                    snapshot.Attribute = card.Attribute;
                    snapshot.Race = card.Race;
                    snapshot.Level = card.Level;
                    snapshot.Rank = card.Rank;
                    snapshot.LinkMarker = card.LinkMarker;
                    snapshot.Negated = card.Disabled != 0;
                    snapshot.Attack = card.Attack;
                    snapshot.Defense = card.Defense;
                    snapshot.Type = card.Type;
                    snapshot.OverlayCount = card.Overlays.Count;
                    if (snapshot.OverlayCount > 0)
                        AddUnsupported(record, "overlay-state-incomplete");
                    if (card.EquipTarget != null || card.EquipCards.Count > 0
                        || card.OwnTargets.Count > 0 || card.TargetCards.Count > 0)
                        AddUnsupported(record, "card-relations-state-incomplete");
                }
                record.Observation.Cards.Add(snapshot);
            }
        }

        private static void AddUnsupported(ShadowRecord record, string reason)
        {
            if (!record.Unsupported.Contains(reason))
                record.Unsupported.Add(reason);
        }

        public void Commit(ShadowRecord record, int response)
        {
            if (!Enabled || record == null)
                return;
            try
            {
                record.SelectedWireResponse = response;
                List<ShadowOffer> matches = record.Offers.Where(offer => offer.WireResponse == response).ToList();
                if (matches.Count == 1)
                    record.SelectedOfferId = matches[0].Id;
                else
                    AddUnsupported(record, "selected-wire-not-uniquely-offered");
                Write(record);
            }
            catch (Exception exception)
            {
                Disable(exception.GetType().Name);
            }
        }

        public void OnOutcome(int result)
        {
            if (!Enabled)
                return;
            if (result < 0 || result > 2)
            {
                Disable("InvalidOutcome");
                return;
            }
            _outcome = result;
            Write(new ShadowRecord { Kind = "outcome", Outcome = result });
        }

        public void End(bool authoritativeEnd, string reason)
        {
            if (Enabled)
                Write(new ShadowRecord
                {
                    Kind = "end",
                    Outcome = _outcome,
                    Completed = authoritativeEnd && _outcome.HasValue,
                    Reason = reason
                });
            Close();
        }

        private void Write(ShadowRecord record)
        {
            if (!Enabled)
                return;
            try
            {
                record.DuelId = _duelId;
                record.Agent = _agent;
                record.Sequence = _rows;
                using (MemoryStream row = new MemoryStream())
                {
                    new DataContractJsonSerializer(typeof(ShadowRecord)).WriteObject(row, record);
                    if (_rows >= MaximumRows || row.Length + 1 > MaximumRowBytes
                        || _bytes + row.Length + 1 > MaximumBytes)
                    {
                        Close();
                        Logger.WriteLine("SKYNET_SHADOW stopped at the per-duel capture limit.");
                        return;
                    }
                    row.Position = 0;
                    row.CopyTo(_stream);
                    _stream.WriteByte(10);
                    _stream.Flush();
                    _bytes += row.Length + 1;
                    ++_rows;
                }
            }
            catch (Exception exception)
            {
                Disable(exception.GetType().Name);
            }
        }

        private void Disable(string reason)
        {
            bool report = !_disabled;
            _disabled = true;
            Close();
            if (report)
            {
                try
                {
                    Logger.WriteLine("SKYNET_SHADOW disabled: " + reason + "; duel decisions continue unchanged.");
                }
                catch (Exception)
                {
                    // Diagnostics must never become a duel failure.
                }
            }
        }

        private void Close()
        {
            FileStream stream = _stream;
            _stream = null;
            if (stream == null)
                return;
            try
            {
                stream.Dispose();
            }
            catch (Exception)
            {
                _disabled = true;
            }
        }
    }
}
