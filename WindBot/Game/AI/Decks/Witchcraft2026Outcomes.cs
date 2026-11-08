using System;
using System.Collections.Generic;
using System.Linq;
using YGOSharp.OCGWrapper.Enums;

namespace WindBot.Game.AI.Decks
{
    public partial class Witchcraft2026Executor
    {
        // Semantic profiles from the selected MDPro4 script.zip, not card-name or
        // keyword guesses. Unknown effects remain unknown. This is not an engine.
        private const int SummonLimiter = 94977269;
        private const int SpellSummonLimiter = 19261966;
        private const int GraveFusionTrigger = 572850;

        internal sealed class WRestriction
        {
            public int Id, Player, Used, SummonLimit;
            public bool Active, SpellHandGrave;
            public WRestriction Copy() { return (WRestriction)MemberwiseClone(); }
        }

        private readonly Dictionary<ClientCard, int> _limitCounts = new Dictionary<ClientCard, int>();
        private readonly Dictionary<ClientCard, int> _expires = new Dictionary<ClientCard, int>();
        private readonly HashSet<ClientCard> _summonLimitSources = new HashSet<ClientCard>();
        private readonly List<WPrediction> _predictions = new List<WPrediction>();
        private readonly HashSet<int> _graveFusionSpent = new HashSet<int>();

        private sealed class WPrediction
        {
            public WAction Action;
            public WState Before, After;
        }

        internal static bool IsSummon(WAction a)
        {
            return a.Kind == WKind.Recruit || a.Kind == WKind.DarkSummon || a.Kind == WKind.Gaze ||
                a.Kind == WKind.GenniLacrima || a.Kind == WKind.LacrimaRecruit || a.Kind == WKind.MaidExchange ||
                a.Kind == WKind.Recover || a.Kind == WKind.Fusion || a.Kind == WKind.RillieRecruit ||
                a.Kind == WKind.EdelRevive || a.Kind == WKind.MaidSummon || a.Kind == WKind.Unveiling;
        }

        internal static WZone RecruitOrigin(WState s, WAction a)
        {
            // A copied spell resolves as a monster effect. The same printed spell
            // activated directly has a different restriction profile.
            bool blockedHand = a.Kind == WKind.LacrimaRecruit &&
                s.Restrictions.Any(r => r.Active && r.SpellHandGrave);
            return !blockedHand && s.Has(a.Target, WZone.Hand) ? WZone.Hand : WZone.Deck;
        }

        internal static bool SummonAllowed(WState s, WAction a)
        {
            if (!IsSummon(a)) return true;
            if (s.Restrictions.Any(r => r.Active && r.SummonLimit > 0 && r.Used >= r.SummonLimit)) return false;
            if (!s.Restrictions.Any(r => r.Active && r.SpellHandGrave)) return true;
            if (a.Kind == WKind.Unveiling) return false;
            if (a.Kind == WKind.LacrimaRecruit) return s.Has(a.Target, WZone.Deck);
            // Fusion/Gaze summon FROM Extra; materials in hand/GY are not summon origins.
            // Recruit, Edel, Rillie, Genni, Terra and Maid are monster effects.
            return true;
        }

        internal static string RestrictionSummary(WState s)
        {
            return string.Join(",", s.Restrictions.Select(r => r.Id + "/p" + r.Player +
                "/active=" + r.Active + "/used=" + r.Used + "/limit=" + r.SummonLimit +
                "/spell-hand-gy=" + r.SpellHandGrave));
        }

        private void ObserveOutcomes(WState s)
        {
            s.Turn = Duel.Turn;
            foreach (ClientCard card in Bot.GetMonsters().Concat(Enemy.GetMonsters()).Where(c => c.IsFaceup()))
            {
                if (card.Id == SummonLimiter || card.Id == SpellSummonLimiter)
                {
                    int used;
                    _limitCounts.TryGetValue(card, out used);
                    s.Restrictions.Add(new WRestriction { Id = card.Id, Player = card.Controller,
                        Active = !card.IsDisabled(), Used = used, SummonLimit = card.Id == SummonLimiter ? 1 : 0,
                        SpellHandGrave = card.Id == SpellSummonLimiter });
                }
                int expires;
                if (card.Controller == 0 && _expires.TryGetValue(card, out expires)) s.Temporary[card.Id] = expires;
            }
        }

        public override void OnSpSummoning()
        {
            // Capture existing limit sources before the summon completes. A newly
            // entering Winda does not retroactively count summons made before it.
            foreach (ClientCard card in Bot.GetMonsters().Concat(Enemy.GetMonsters()))
                if (card.Id == SummonLimiter && card.IsFaceup() && !Duel.SummoningCards.Contains(card))
                    _summonLimitSources.Add(card);
            base.OnSpSummoning();
        }

        public override void OnPosChange(ClientCard card, int previousPosition, int currentPosition)
        {
            if ((currentPosition & (int)CardPosition.FaceDown) != 0) _opponent.ResetInstance(card);
            if ((currentPosition & (int)CardPosition.FaceDown) != 0)
            {
                _limitCounts.Remove(card);
                _summonLimitSources.Remove(card);
                _expires.Remove(card);
            }
            Invalidate();
            base.OnPosChange(card, previousPosition, currentPosition);
        }

        private void SummonOutcomes()
        {
            if (Duel.LastSummonPlayer == 0)
            {
                foreach (ClientCard card in _summonLimitSources)
                {
                    int used; _limitCounts.TryGetValue(card, out used);
                    _limitCounts[card] = used + 1; // simultaneous group = one event
                }
                ChainInfo resolving = Duel.GetCurrentSolvingChainInfo();
                if (resolving != null && resolving.ActivatePlayer == 0 && resolving.ActivateId == CardId.GazeOfTimaeus)
                    foreach (ClientCard card in Duel.LastSummonedCards.Where(c => c.Controller == 0))
                        _expires[card] = Duel.Turn + 1;
            }
            _summonLimitSources.Clear();
        }

        internal static WState ProjectDelayed(WState source, int endTurn)
        {
            WState result = source.Copy();
            foreach (var pair in source.Temporary.Where(p => p.Value <= endTurn))
                if (result.Has(pair.Key, WZone.Board)) result.Move(pair.Key, WZone.Board, WZone.Banished);
            return result;
        }

        // sourcePlayer and negation at the moment of resolution are essential:
        // Winda resists the opponent, not its controller, and Verre BELOW Vice has
        // not resolved yet. Do not feed a whole-chain endpoint into this predicate.
        internal static bool DestructionProtected(ClientCard card, int sourcePlayer, bool negated)
        {
            if (card == null || card.Location != CardLocation.MonsterZone || !card.IsFaceup() ||
                card.IsDisabled() || negated) return false;
            return card.Id == CardId.RedEyesDarkDragoon ||
                (card.Id == SummonLimiter && card.Controller != sourcePlayer);
        }

        private double FriendlyDestructionLoss(ClientCard card)
        {
            if (DestructionProtected(card, 0, false)) return 0;
            WState before = Observe();
            if (!before.Has(card.Id, Zone(card.Location))) return 3;
            WState after = before.Copy(); after.Move(card.Id, Zone(card.Location), WZone.Grave);
            // Bystreet's per-card prevention counter is not exposed by this client.
            // Do not assume that its once-per-turn shield remains unused.
            return Math.Max(0, WPlanner.Evaluate(before) - WPlanner.Evaluate(after));
        }

        internal double DestructionPairValue(ClientCard own, ClientCard enemy)
        {
            return RemovalValue(enemy, false) - FriendlyDestructionLoss(own);
        }

        private IList<ClientCard> SelectDestructionPair(IList<ClientCard> cards, int min, int max, bool cancelable)
        {
            // Lua SelectSubGroup can ask for one card at a time. Respect each actual
            // offered list; never substitute an absent enemy or two friendly cards.
            var pairs = (from own in cards where own.Controller == 0
                         from enemy in cards where enemy.Controller == 1
                         select new { Own = own, Enemy = enemy, Value = DestructionPairValue(own, enemy) })
                         .OrderByDescending(p => p.Value).ToList();
            if (pairs.Count > 0)
            {
                if (cancelable && pairs[0].Value <= 0) return new List<ClientCard>();
                return new[] { pairs[0].Own, pairs[0].Enemy }.Take(max).ToList();
            }
            return cards.OrderByDescending(c => c.Controller == 1 ? RemovalValue(c, false) : -FriendlyDestructionLoss(c))
                .Take(Math.Min(max, Math.Max(1, min))).ToList();
        }

        private void ReconcilePredictions()
        {
            if (_predictions.Count == 0) return;
            WState actual = Observe();
            foreach (WPrediction prediction in _predictions)
            {
                List<string> differences = new List<string>();
                foreach (long key in prediction.Before.Cards.Keys.Union(prediction.After.Cards.Keys).Union(actual.Cards.Keys))
                {
                    int id = (int)(key / 8); WZone zone = (WZone)(key % 8);
                    int expectedDelta = prediction.After.Count(id, zone) - prediction.Before.Count(id, zone);
                    int actualDelta = actual.Count(id, zone) - prediction.Before.Count(id, zone);
                    if (expectedDelta != actualDelta) differences.Add(id + "/" + zone + ":expectedDelta=" + expectedDelta + "/observedDelta=" + actualDelta);
                }
                bool restrictionsChanged = RestrictionSummary(prediction.After) != RestrictionSummary(actual);
                Logger.DebugWriteLine("Witchcraft2026 reconcile: " + prediction.Action +
                    " status=" + (differences.Count == 0 && !restrictionsChanged ? "matched" : "changed-or-interrupted") +
                    " differences=" + string.Join(";", differences) +
                    " expectedRestrictions=" + RestrictionSummary(prediction.After) +
                    " observedRestrictions=" + RestrictionSummary(actual) +
                    " expectedExpiry=" + string.Join(",", prediction.After.Temporary.Select(p => p.Key + "@" + p.Value)) +
                    " observedExpiry=" + string.Join(",", actual.Temporary.Select(p => p.Key + "@" + p.Value)) +
                    " expectedScore=" + WPlanner.Evaluate(prediction.After).ToString("0.0") +
                    " observedScore=" + WPlanner.Evaluate(actual).ToString("0.0") +
                    " modelActions=" + WPlanner.Actions(actual).Take(257).Count() +
                    " concurrent chain effects included; retire dependent forecast");
            }
            _predictions.Clear(); Invalidate();
        }
    }
}
