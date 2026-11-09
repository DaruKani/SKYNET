using System;
using System.Collections.Generic;
using System.Linq;
using YGOSharp.OCGWrapper.Enums;

namespace WindBot.Game.AI.Decks
{
    public partial class Witchcraft2026Executor
    {
        // Immutable public target snapshots. Keys identify slots, never hidden cards.
        internal sealed class WRemovalTarget
        {
            public int Key, Id;
            public double Value;
            public OpponentResponse Responses;
        }

        private readonly HashSet<ClientCard> _freshBystreetShields = new HashSet<ClientCard>();
        private bool _bystreetSeenThisTurn;

        public override void OnNewPhase()
        {
            if (Duel.Phase != DuelPhase.Main1 && Duel.Phase != DuelPhase.Main2)
                _freshBystreetShields.Clear(); // battles can consume prevention without a chain
            Invalidate();
            base.OnNewPhase();
        }

        private static IEnumerable<WAction> CoverageActions(WState s)
        {
            if (s.Has(CardId.Bystreet, WZone.Hand) && !s.Has(CardId.Bystreet, WZone.Backrow) && s.OccupiedBackrow < 5)
                yield return new WAction { Kind = WKind.ActivateBystreet, Source = CardId.Bystreet,
                    Location = WZone.Hand, Description = -1, Route = "continuous protection / preserve hand alternative" };
            if (!s.Has(CardId.Celebration, WZone.Hand) || s.OccupiedBackrow >= 5) yield break;
            foreach (int own in s.In(WZone.Board).Where(IsWitchMaterial))
                foreach (WRemovalTarget enemy in s.EnemyTargets.Where(t => t.Value > 0).Take(12))
                    yield return new WAction { Kind = WKind.CelebrationDestroy, Source = CardId.Celebration,
                        Location = WZone.Hand, Description = 0, Target = enemy.Id, EnemyKey = enemy.Key,
                        Materials = new[] { own }, MaterialZones = new[] { WZone.Board },
                        Route = "non-targeting paired destruction; friendly loss and enemy consequence" };
        }

        private static void MarkSharedUsage(HashSet<string> used, WAction a)
        {
            if (a.Source == CardId.Celebration && (a.Kind == WKind.Fusion || a.Kind == WKind.CelebrationDestroy))
            {
                used.Add(CardId.Celebration + ":" + WKind.Fusion);
                used.Add(CardId.Celebration + ":" + WKind.CelebrationDestroy);
            }
            if (a.Cost == CardId.Bystreet && a.CostZone == WZone.Backrow)
                used.Add(CardId.Bystreet + ":cost-or-recovery");
        }

        private static void ApplyDestructionPair(WState s, WAction action)
        {
            int own = action.Materials[0];
            bool shield = s.Has(CardId.Bystreet, WZone.Backrow) && !s.Disabled.Contains(CardId.Bystreet) && s.FreshShields.Remove(own);
            if (!shield) s.Move(own, WZone.Board, WZone.Grave);
            WRemovalTarget enemy = s.EnemyTargets.First(t => t.Key == action.EnemyKey);
            s.RemovedThreat += enemy.Value;
            s.EnemyTargets.Remove(enemy);
            s.OpposingResponses &= s.EnemyTargets.Aggregate(OpponentResponse.None, (mask, t) => mask | t.Responses);
            if (!s.EnemyTargets.Any(t => t.Id == enemy.Id))
                s.Restrictions.RemoveAll(r => r.Player == 1 && r.Id == enemy.Id);
            // A removal cannot cancel an interruption already paid earlier in a route.
        }

        private void ObserveCoverage(WState s)
        {
            s.OccupiedBackrow = Bot.GetSpells().Count(c => c.Sequence < 5);
            s.CanEstablishShield = !_bystreetSeenThisTurn;
            foreach (ClientCard card in Bot.GetMonsters().Where(c => _freshBystreetShields.Contains(c)))
                if (s.Count(card.Id, WZone.Board) == 1) s.FreshShields.Add(card.Id);
            foreach (ClientCard card in Enemy.GetMonsters().Concat(Enemy.GetSpells()))
            {
                s.EnemyTargets.Add(new WRemovalTarget
                {
                    Key = ((int)card.Location * 16) + card.Sequence + 1,
                    Id = card.IsFaceup() ? card.Id : 0,
                    Value = RemovalValue(card, false),
                    Responses = _opponent.VisibleResponses(new[] { card })
                });
            }
            s.OpposingResponses = s.EnemyTargets.Aggregate(OpponentResponse.None, (mask, t) => mask | t.Responses);
        }

        private void ObserveBystreetResolution(ChainInfo info)
        {
            if (info.ActivatePlayer != 0 || info.ActivateId != CardId.Bystreet ||
                info.ActivateLocation != CardLocation.SpellZone) return;
            ClientCard source = info.RelatedCard;
            if (!_bystreetSeenThisTurn && source != null && source.Location == CardLocation.SpellZone &&
                source.IsFaceup() && !source.IsDisabled())
                _freshBystreetShields.UnionWith(Bot.GetMonsters().Where(c => c.IsFaceup() && c.HasSetcode(WitchcrafterSetcode)));
            _bystreetSeenThisTurn = true;
        }

        // Only an audit, never a fallback that executes an unsupported offer. Main
        // phase lists are stale during chain windows: explicitly label that boundary.
        private string ActionCoverageAudit(WState state, WPlan plan)
        {
            List<WAction> generated = WPlanner.Actions(state).Take(257).ToList();
            List<string> entries = new List<string>();
            if (_chainOffers != null || AI.SelectingEffectYn)
                return "window=chain/trigger; idle-list-not-current; optional idle actions deferred; current=" +
                    (Card == null ? "none" : Card.Id + "/" + Card.Location + "/" + ActivateDescription);
            MainPhase main = Duel.MainPhase;
            if (main == null) return "window=none; no idle offers";
            for (int i = 0; i < main.ActivableCards.Count && i < 16; i++)
            {
                ClientCard card = main.ActivableCards[i]; int desc = main.ActivableDescs[i];
                List<WAction> actions = generated.Where(a => ActionType(a) == ExecutorType.Activate && Matches(a, card, desc)).ToList();
                string status;
                if (actions.Count > 0)
                    status = "modeled=" + string.Join(",", actions.Select(a => a.Kind).Distinct()) +
                        "; immediate=" + string.Join(",", actions.Take(3).Select(a => WPlanner.Evaluate(WPlanner.Apply(state, a)).ToString("0.0"))) +
                        "; " + (plan.Steps.Count > 0 && actions.Any(a => a.ToString() == plan.Steps[0].Action.ToString()) ? "selected" :
                        plan.TimedOut ? "not-selected/budget-limited" : "not-selected/by-bounded-comparison");
                else status = UnmodeledOfferReason(state, card, desc);
                entries.Add(card.Id + "/" + card.Location + "/" + desc + " " + status);
            }
            int offered = generated.Count(IsOffered);
            return "window=idle model=" + generated.Count + " offered-model=" + offered +
                " unavailable-model=" + (generated.Count - offered) + " (projection-only, not engine-authorized)" +
                " unavailable-examples=[" + string.Join(" | ", generated.Where(a => !IsOffered(a)).Take(4)) + "]" +
                " other-offers=[normal:" + string.Join(",", main.SummonableCards.Take(8).Select(c => c.Id)) +
                ";special:" + string.Join(",", main.SpecialSummonableCards.Take(8).Select(c => c.Id)) +
                ";set:" + string.Join(",", main.SpellSetableCards.Take(8).Select(c => c.Id)) + "]" +
                " hold=" + WPlanner.Evaluate(state).ToString("0.0") + " best=" + plan.Score.ToString("0.0") +
                " decision=" + (plan.Steps.Count == 0 ? "HOLD" : "PLAY " + plan.Steps[0].Action.Kind) +
                " offers=[" + string.Join(" | ", entries) + "]";
        }

        private static string UnmodeledOfferReason(WState state, ClientCard card, int description)
        {
            if (card.Id == CardId.Lacrima)
                return "no-modeled-action: verify zone/used send/hostile chain; send-used=" +
                    state.Used.Contains(CardId.Lacrima + ":" + WKind.LacrimaSend) +
                    "; recruit-used=" + state.Used.Contains(CardId.Lacrima + ":" + WKind.LacrimaRecruit) +
                    "; opponent-chain=" + state.EnemyChain + "; no GY activation in installed script";
            if (card.Id == CardId.Celebration)
                return "no-modeled-action: shared activation used, no modeled Fusion materials or no useful public destruction pair; partial-coverage-check-required";
            if (card.Id == CardId.Bystreet)
                return card.Location == CardLocation.Grave ? "delegated: own End Phase recovery handler; shared cost/recovery limit engine-owned" :
                    "policy-omission: duplicate Bystreet or occupied S/T zones; no new shield assumed";
            if (card.Id == CardId.Seed && card.Location == CardLocation.Grave)
                return "MISSING: Seed hand redraw; random identities not modeled";
            if (card.Id == CardId.AshBlossom || card.Id == CardId.MaxxC || card.Id == CardId.Haine ||
                card.Id == CardId.MadameVerre || card.Id == CardId.ViceMadame || card.Id == CardId.Distortion || card.Id == CardId.Seed)
                return "delegated/partial: tactical handler owns applicable mode; not evidence every mode is supported";
            return "UNCLASSIFIED/MISSING: no generated candidate; inspect conditions/usage/mode; not a strategic rejection";
        }
    }
}
