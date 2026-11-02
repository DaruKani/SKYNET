using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using YGOSharp.OCGWrapper.Enums;

namespace WindBot.Game.AI.Decks
{
    // RDA-style observed state -> isolated transitions -> bounded search. These are
    // predictions used to rank a current legal offer, never a replacement duel state.
    // The model covers the five documented Voiceless routes, not arbitrary card text.
    internal static class VoicelessPlanner
    {
        internal const int Lo = 25801745, Barrier = 98477480, Saffira = 51296484;
        internal const int Skull = 10774240, Prayer = 52472775, Radiance = 86310763;
        internal const int Blessing = 39114494, Sage = 88284599, Sauravis = 4810828;
        internal const int Diviner = 92919429, Trias = 26866984, PrePrep = 13048472;
        internal const int Linkuriboh = 41999284, Gardna = 2220237, Dyna = 73898890;
        internal const int Herald = 79606837;
        internal static readonly int[] Cards = { Lo, Barrier, Saffira, Skull, Prayer,
            Radiance, Blessing, Sage, Sauravis, Diviner, Trias, PrePrep, Linkuriboh, Gardna, Dyna };

        internal enum Zone { Hand, Field, Grave, Deck, Extra, Banished }
        internal enum Effect { LoPlace, LoRevive, BarrierSearch, SaffiraSearch,
            SaffiraRitual, SkullSearch, BlessingRecover, BlessingRitual,
            DivinerMill, TriasSummon, PrePreparation, SageSummon }
        internal enum ActionKind { None, NormalLo, NormalDiviner, DivinerMill,
            TriasSummon, PlaceLo, ActivateBarrier, SearchBarrier, SaffiraSearch,
            SaffiraRitual, PrayerRitual, PrePreparation, SkullSearch, ReturnLo,
            ActivateBlessing, BlessingRecover, BlessingRitual, Linkuriboh,
            SecureGardna, DynaMondo, SummonSage, DeclineTrigger }

        internal sealed class State
        {
            private int[,] _counts = new int[6, Cards.Length];
            public bool[] Used = new bool[12];
            public bool NormalAvailable = true, OurTurn = true, SearchAllowed = true, ProperSkull;
            public bool PendingLo, PendingSkull, PendingDiviner, PendingRevive, PendingBlessing;
            public int ExtraSpells, OtherLightTributes, OtherMonsters, OtherSpells;
            public int RedirectedTrias;
            public int DisabledMask;
            public bool EnemyThreat;

            public int Count(Zone zone, int id)
            {
                int index = Array.IndexOf(Cards, id);
                return index < 0 ? 0 : _counts[(int)zone, index];
            }

            public void Add(Zone zone, int id, int count)
            {
                int index = Array.IndexOf(Cards, id);
                if (index < 0) throw new ArgumentException("Unmodeled card", "id");
                int after = _counts[(int)zone, index] + count;
                if (after < 0 || after > 60) throw new ArgumentOutOfRangeException("count");
                _counts[(int)zone, index] = after;
            }

            public State Copy()
            {
                State copy = (State)MemberwiseClone();
                copy._counts = (int[,])_counts.Clone();
                copy.Used = (bool[])Used.Clone();
                return copy;
            }

            public bool Available(Effect effect)
            {
                return !Used[(int)effect];
            }
            public bool Active(int id)
            {
                int index = Array.IndexOf(Cards, id);
                return Count(Zone.Field, id) > 0 && (DisabledMask & (1 << index)) == 0;
            }
            public string Key()
            {
                var values = new List<int>(110);
                foreach (int count in _counts) values.Add(count);
                foreach (bool used in Used) values.Add(used ? 1 : 0);
                values.AddRange(new[] { NormalAvailable ? 1 : 0, OurTurn ? 1 : 0,
                    ProperSkull ? 1 : 0, PendingLo ? 1 : 0, PendingSkull ? 1 : 0,
                    PendingDiviner ? 1 : 0, PendingRevive ? 1 : 0, PendingBlessing ? 1 : 0,
                    ExtraSpells, OtherLightTributes, OtherMonsters, OtherSpells,
                    DisabledMask, EnemyThreat ? 1 : 0, SearchAllowed ? 1 : 0, RedirectedTrias });
                return string.Join(",", values);
            }
        }

        internal sealed class Step
        {
            public readonly ActionKind Kind;
            public readonly int TargetId;
            public Step(ActionKind kind, int targetId)
            {
                Kind = kind;
                TargetId = targetId;
            }
            public override string ToString()
            {
                return Kind + ":" + TargetId;
            }
        }

        internal sealed class Result
        {
            public int TargetId, Expanded;
            public ActionKind FirstAction;
            public State FinalState;
            public IList<Step> Steps;
            public double Score;
            public bool TimedOut;
            public long ElapsedMilliseconds;
        }

        private sealed class Node
        {
            public State State;
            public List<Step> Steps;
            public double Score;
            public int Target;
        }

        private sealed class Move
        {
            public State State;
            public Step Step;
            public Move(State state, ActionKind kind, int target)
            {
                State = state;
                Step = new Step(kind, target);
            }
        }

        public static Result Plan(State observed, CancellationToken cancellation)
        {
            return Search(observed, 0, cancellation);
        }

        public static Result ChooseSearch(State observed, int sourceId, CancellationToken cancellation)
        {
            return Search(observed, sourceId, cancellation);
        }

        private static Result Search(State observed, int sourceId, CancellationToken cancellation)
        {
            // Synchronous, no worker queue. One deadline covers forced choices,
            // deduplication, expansion and sorting; node/frontier/depth are bounded.
            const int milliseconds = 100, maxExpanded = 2048, width = 128, depthLimit = 24;
            Stopwatch clock = Stopwatch.StartNew();
            State root = observed.Copy();
            var frontier = new List<Node>();
            if (sourceId == 0)
                frontier.Add(new Node { State = root, Steps = new List<Step>(), Score = Evaluate(root) });
            else
            {
                foreach (Move move in SearchChoices(root, sourceId))
                    frontier.Add(new Node { State = move.State, Steps = new List<Step> { move.Step },
                        Score = Evaluate(move.State), Target = move.Step.TargetId });
            }
            Node best = frontier.Where(n => IsSettled(n.State)).OrderByDescending(n => n.Score).FirstOrDefault();
            var seen = new HashSet<string>();
            int expanded = 0;
            for (int depth = 0; depth < depthLimit && frontier.Count > 0; depth++)
            {
                if (cancellation.IsCancellationRequested || clock.ElapsedMilliseconds >= milliseconds) break;
                var next = new List<Node>();
                foreach (Node node in frontier)
                {
                    if (cancellation.IsCancellationRequested || clock.ElapsedMilliseconds >= milliseconds || expanded >= maxExpanded) break;
                    if (!seen.Add(node.Target + ":" + node.State.Key())) continue;
                    expanded++;
                    foreach (Move move in Expand(node.State))
                    {
                        if (cancellation.IsCancellationRequested || clock.ElapsedMilliseconds >= milliseconds || next.Count >= width * 16) break;
                        var steps = new List<Step>(node.Steps) { move.Step };
                        var child = new Node { State = move.State, Steps = steps,
                            Score = Evaluate(move.State) - steps.Count * 0.05, Target = node.Target };
                        next.Add(child);
                        if (IsSettled(child.State) && (best == null || child.Score > best.Score)) best = child;
                    }
                }
                if (expanded >= maxExpanded) break;
                frontier = next.OrderByDescending(n => n.Score).Take(width).ToList();
            }
            if (cancellation.IsCancellationRequested) return null;
            return new Result { TargetId = best == null ? 0 : best.Target,
                FirstAction = best == null || best.Steps.Count == 0 ? ActionKind.None : best.Steps[0].Kind,
                FinalState = best == null ? root : best.State.Copy(),
                Steps = best == null ? new List<Step>().AsReadOnly() : best.Steps.AsReadOnly(),
                Score = best == null ? Evaluate(root) : best.Score, Expanded = expanded,
                TimedOut = clock.ElapsedMilliseconds >= milliseconds, ElapsedMilliseconds = clock.ElapsedMilliseconds };
        }

        private static void MoveCard(State state, Zone from, Zone to, int id)
        {
            state.Add(from, id, -1);
            if (from == Zone.Field && state.Count(Zone.Field, id) == 0)
                state.DisabledMask &= ~(1 << Array.IndexOf(Cards, id));
            if (id == Trias && from == Zone.Field && state.RedirectedTrias > 0)
            {
                state.RedirectedTrias--;
                if (to == Zone.Grave) to = Zone.Banished;
            }
            state.Add(to, id, 1);
        }

        private static bool IsSettled(State state)
        {
            return !state.PendingLo && !state.PendingSkull && !state.PendingDiviner
                && !state.PendingRevive && !state.PendingBlessing;
        }

        private static Move Change(State source, ActionKind kind, int id, Zone from, Zone to)
        {
            State state = source.Copy();
            MoveCard(state, from, to, id);
            return new Move(state, kind, id);
        }

        private static IEnumerable<Move> SearchChoices(State state, int source)
        {
            if (source == Lo && SpellCount(state) >= 5) yield break;
            if (!state.SearchAllowed && source != Lo && source != Saffira) yield break;
            int[] targets;
            ActionKind kind;
            if (source == Lo) { targets = new[] { Barrier, Radiance, Blessing }; kind = ActionKind.PlaceLo; }
            else if (source == Barrier) { targets = new[] { Saffira, Blessing, Sage, Lo, Skull, Prayer, Radiance }; kind = ActionKind.SearchBarrier; }
            else if (source == Skull) { targets = new[] { Sauravis, Sage, Lo, Saffira, Skull }; kind = ActionKind.SkullSearch; }
            else if (source == Saffira) { targets = new[] { Skull, Sauravis }; kind = ActionKind.SaffiraSearch; }
            else if (source == Herald) { targets = new[] { Skull, Prayer, Sauravis }; kind = ActionKind.SkullSearch; }
            else if (source == PrePrep) { targets = new[] { Prayer }; kind = ActionKind.PrePreparation; }
            else yield break;
            foreach (int target in targets)
            {
                Zone from = Zone.Deck;
                if ((state.Count(from, target) == 0 || !state.SearchAllowed) && source == Saffira) from = Zone.Grave;
                if (state.Count(from, target) == 0) continue;
                if (source == Lo && state.Count(Zone.Field, target) > 0) continue;
                Move move = Change(state, kind, target, from, source == Lo ? Zone.Field : Zone.Hand);
                if (source == Lo) { move.State.PendingLo = false; move.State.Used[(int)Effect.LoPlace] = true; }
                if (source == Barrier) move.State.Used[(int)Effect.BarrierSearch] = true;
                if (source == Skull) { move.State.PendingSkull = false; move.State.Used[(int)Effect.SkullSearch] = true; }
                yield return move;
            }
        }

        private static IEnumerable<Move> Expand(State state)
        {
            if (state.PendingRevive)
            {
                State after = state.Copy(); after.PendingRevive = false;
                if (after.Available(Effect.LoRevive) && after.Count(Zone.Grave, Lo) > 0 && MonsterCount(after) < 5)
                {
                    MoveCard(after, Zone.Grave, Zone.Field, Lo);
                    after.Used[(int)Effect.LoRevive] = true;
                    after.PendingLo = after.Available(Effect.LoPlace);
                    yield return new Move(after, ActionKind.ReturnLo, Lo);
                }
                else yield return new Move(after, ActionKind.DeclineTrigger, Lo);
                yield break;
            }
            if (state.PendingLo)
            {
                foreach (Move move in SearchChoices(state, Lo)) yield return move;
                State after = state.Copy(); after.PendingLo = false;
                yield return new Move(after, ActionKind.DeclineTrigger, Lo);
                yield break;
            }
            if (state.PendingSkull)
            {
                foreach (Move move in SearchChoices(state, Skull)) yield return move;
                State after = state.Copy(); after.PendingSkull = false;
                yield return new Move(after, ActionKind.DeclineTrigger, Skull);
                yield break;
            }
            if (state.PendingDiviner)
            {
                State after = state.Copy(); after.PendingDiviner = false;
                after.Used[(int)Effect.DivinerMill] = true;
                if (after.Count(Zone.Deck, Trias) > 0)
                {
                    MoveCard(after, Zone.Deck, Zone.Grave, Trias);
                    yield return new Move(after, ActionKind.DivinerMill, Trias);
                }
                else yield return new Move(after, ActionKind.DeclineTrigger, Diviner);
                yield break;
            }
            if (state.PendingBlessing)
            {
                State after = state.Copy(); after.PendingBlessing = false;
                if (after.Active(Blessing) && after.Available(Effect.BlessingRitual))
                    foreach (Move move in Rituals(after, ActionKind.BlessingRitual)) yield return move;
                yield return new Move(after, ActionKind.DeclineTrigger, Blessing);
                yield break;
            }
            if (!state.OurTurn) yield break;

            if (state.NormalAvailable && MonsterCount(state) < 5)
            {
                if (state.Count(Zone.Hand, Lo) > 0)
                {
                    Move move = Change(state, ActionKind.NormalLo, Lo, Zone.Hand, Zone.Field);
                    move.State.NormalAvailable = false; move.State.PendingLo = state.Available(Effect.LoPlace);
                    move.State.PendingBlessing = state.Active(Blessing) && state.Available(Effect.BlessingRitual);
                    yield return move;
                }
                if (state.Count(Zone.Hand, Diviner) > 0)
                {
                    Move move = Change(state, ActionKind.NormalDiviner, Diviner, Zone.Hand, Zone.Field);
                    move.State.NormalAvailable = false; move.State.PendingDiviner = state.Available(Effect.DivinerMill);
                    yield return move;
                }
            }
            if (state.Available(Effect.TriasSummon) && state.Count(Zone.Field, Diviner) > 0)
            {
                Zone from = state.Count(Zone.Grave, Trias) > 0 ? Zone.Grave : Zone.Hand;
                if (state.Count(from, Trias) > 0)
                {
                    Move move = Change(state, ActionKind.TriasSummon, Trias, from, Zone.Field);
                    MoveCard(move.State, Zone.Field, Zone.Grave, Diviner);
                    move.State.Used[(int)Effect.TriasSummon] = true;
                    move.State.RedirectedTrias++;
                    Zone loFrom = state.Count(Zone.Deck, Lo) > 0 ? Zone.Deck : Zone.Hand;
                    if (state.Count(loFrom, Lo) > 0 && MonsterCount(move.State) < 5)
                    {
                        MoveCard(move.State, loFrom, Zone.Field, Lo);
                        move.State.PendingLo = state.Available(Effect.LoPlace);
                    }
                    yield return move;
                }
            }
            if (SpellCount(state) < 5)
            {
                foreach (int id in new[] { Barrier, Blessing })
                    if (state.Count(Zone.Hand, id) > 0 && state.Count(Zone.Field, id) == 0)
                        yield return Change(state, id == Barrier ? ActionKind.ActivateBarrier : ActionKind.ActivateBlessing,
                            id, Zone.Hand, Zone.Field);
            }
            if (state.Active(Barrier) && state.Available(Effect.BarrierSearch))
                foreach (Move move in SearchChoices(state, Barrier)) yield return move;
            if (state.SearchAllowed && state.Count(Zone.Hand, PrePrep) > 0 && state.Available(Effect.PrePreparation)
                && state.Count(Zone.Deck, Prayer) > 0 && (state.Count(Zone.Deck, Skull) + state.Count(Zone.Grave, Skull)) > 0)
            {
                Move move = Change(state, ActionKind.PrePreparation, PrePrep, Zone.Hand, Zone.Grave);
                MoveCard(move.State, Zone.Deck, Zone.Hand, Prayer);
                MoveCard(move.State, state.Count(Zone.Deck, Skull) > 0 ? Zone.Deck : Zone.Grave, Zone.Hand, Skull);
                move.State.Used[(int)Effect.PrePreparation] = true;
                yield return move;
            }
            if (state.Count(Zone.Hand, Saffira) > 0 && state.Available(Effect.SaffiraSearch) && state.Count(Zone.Deck, Prayer) > 0)
            {
                State after = state.Copy();
                MoveCard(after, Zone.Hand, Zone.Grave, Saffira); MoveCard(after, Zone.Deck, Zone.Grave, Prayer);
                after.Used[(int)Effect.SaffiraSearch] = true;
                foreach (Move move in SearchChoices(after, Saffira)) yield return move;
                yield return new Move(after, ActionKind.SaffiraSearch, 0);
            }
            if (state.Count(Zone.Grave, Saffira) > 0 && state.Available(Effect.SaffiraRitual))
                foreach (Move move in Rituals(state, ActionKind.SaffiraRitual)) yield return move;
            if (state.Count(Zone.Hand, Prayer) > 0)
                foreach (Move move in Rituals(state, ActionKind.PrayerRitual)) yield return move;
            if (state.Active(Blessing) && state.Available(Effect.BlessingRecover))
            {
                foreach (int id in new[] { Lo, Saffira, Prayer, Skull, Sage, Barrier, Radiance })
                {
                    Zone from = state.Count(Zone.Grave, id) > 0 ? Zone.Grave : Zone.Banished;
                    if (state.Count(from, id) == 0) continue;
                    Move move = Change(state, ActionKind.BlessingRecover, id, from, Zone.Hand);
                    move.State.Used[(int)Effect.BlessingRecover] = true;
                    yield return move;
                }
            }
            if (state.Count(Zone.Hand, Sage) > 0 && state.Available(Effect.SageSummon) && MonsterCount(state) < 5)
            {
                State after = state.Copy();
                if (PaySage(after))
                {
                    MoveCard(after, Zone.Hand, Zone.Field, Sage); after.Used[(int)Effect.SageSummon] = true;
                    yield return new Move(after, ActionKind.SummonSage, Sage);
                }
            }
            if (state.Count(Zone.Field, Lo) > 0 && state.Count(Zone.Extra, Linkuriboh) > 0)
            {
                Move move = Change(state, ActionKind.Linkuriboh, Linkuriboh, Zone.Extra, Zone.Field);
                MoveCard(move.State, Zone.Field, Zone.Grave, Lo);
                move.State.PendingBlessing = state.Active(Blessing) && state.Available(Effect.BlessingRitual);
                yield return move;
            }
            if (state.Count(Zone.Field, Linkuriboh) > 0 && state.Count(Zone.Extra, Gardna) > 0)
            {
                Move move = Change(state, ActionKind.SecureGardna, Gardna, Zone.Extra, Zone.Field);
                MoveCard(move.State, Zone.Field, Zone.Grave, Linkuriboh);
                move.State.PendingBlessing = state.Active(Blessing) && state.Available(Effect.BlessingRitual);
                yield return move;
            }
            if (state.Count(Zone.Field, Lo) > 0 && state.Count(Zone.Field, Skull) > 0 && state.ProperSkull && state.Count(Zone.Extra, Dyna) > 0)
            {
                Move move = Change(state, ActionKind.DynaMondo, Dyna, Zone.Extra, Zone.Field);
                MoveCard(move.State, Zone.Field, Zone.Grave, Lo); MoveCard(move.State, Zone.Field, Zone.Grave, Skull);
                yield return move;
            }
        }

        private static IEnumerable<Move> Rituals(State state, ActionKind kind)
        {
            foreach (int ritual in new[] { Skull, Sauravis })
            {
                if (state.Count(Zone.Hand, ritual) == 0) continue;
                foreach (int material in new[] { Lo, Trias, Sage, Sauravis, Skull, 0 })
                {
                    if (material == 0 && state.OtherLightTributes == 0) continue;
                    Zone from = state.Count(Zone.Field, material) > 0 ? Zone.Field : Zone.Hand;
                    if (material != 0 && state.Count(from, material) <= (from == Zone.Hand && material == ritual ? 1 : 0)) continue;
                    if (material == Lo && from == Zone.Field && !state.Active(Lo)) continue;
                    State after = state.Copy();
                    if (material == 0) after.OtherLightTributes--;
                    else MoveCard(after, from, Zone.Grave, material);
                    if (MonsterCount(after) >= 5) continue;
                    if (kind == ActionKind.SaffiraRitual)
                    {
                        MoveCard(after, Zone.Grave, Zone.Banished, Saffira);
                        after.Used[(int)Effect.SaffiraRitual] = true;
                    }
                    else if (kind == ActionKind.PrayerRitual) MoveCard(after, Zone.Hand, Zone.Grave, Prayer);
                    else after.Used[(int)Effect.BlessingRitual] = true;
                    MoveCard(after, Zone.Hand, Zone.Field, ritual);
                    if (ritual == Skull)
                    {
                        after.ProperSkull = true;
                        after.PendingSkull = after.Available(Effect.SkullSearch);
                    }
                    after.PendingRevive = after.Count(Zone.Grave, Lo) > 0 && after.Available(Effect.LoRevive);
                    yield return new Move(after, kind, ritual);
                }
            }
        }

        private static bool PaySage(State state)
        {
            Zone prayerFrom = state.Count(Zone.Grave, Prayer) > 0 ? Zone.Grave : Zone.Hand;
            if (state.Count(prayerFrom, Prayer) == 0) return false;
            MoveCard(state, prayerFrom, Zone.Deck, Prayer);
            foreach (Zone zone in new[] { Zone.Grave, Zone.Hand })
                foreach (int id in new[] { PrePrep, Prayer, Barrier, Blessing })
                    if (state.Count(zone, id) > 0) { MoveCard(state, zone, Zone.Deck, id); return true; }
            if (state.ExtraSpells > 0) { state.ExtraSpells--; return true; }
            return false;
        }

        private static int MonsterCount(State state)
        {
            return state.OtherMonsters + new[] { Lo, Saffira, Skull, Sage, Sauravis, Diviner, Trias, Linkuriboh, Gardna, Dyna }
                .Sum(id => state.Count(Zone.Field, id));
        }

        private static int SpellCount(State state)
        {
            return state.OtherSpells + new[] { Barrier, Radiance, Blessing }.Sum(id => state.Count(Zone.Field, id));
        }

        public static double Evaluate(State state)
        {
            bool lo = state.Count(Zone.Field, Lo) > 0, skull = state.Active(Skull), sage = state.Active(Sage);
            int rituals = (skull ? 1 : 0) + (state.Active(Sauravis) ? 1 : 0);
            double score = skull ? (lo ? 140 : 38) : 0;
            if (state.Active(Sauravis)) score += 38;
            if (state.Active(Barrier)) score += lo && rituals > 0 ? 25 : 9;
            if (state.Active(Radiance)) score += rituals * 36;
            if (state.Active(Blessing)) score += 12;
            if (state.Active(Trias)) score += 8;
            if (state.Active(Linkuriboh)) score += 8;
            if (state.Active(Gardna)) score += 6;
            score += Math.Min(1, state.Count(Zone.Hand, Sauravis)) * 20;
            score += state.Count(Zone.Hand, Lo) * 2 + state.Count(Zone.Hand, Saffira) * 2;
            score += state.ExtraSpells * 0.4 + state.OtherLightTributes * 0.4;

            // Discounted opponent-turn opportunities. No promise is counted as a
            // currently live negate; the actual executor rechecks offers and zones.
            bool nextPlacement = state.Count(Zone.Deck, Radiance) > 0 && SpellCount(state) < 5;
            bool loGrave = state.Count(Zone.Grave, Lo) > 0;
            if (state.OurTurn && state.Active(Dyna) && state.Count(Zone.Grave, Skull) > 0 && state.ProperSkull && loGrave)
                score += (state.EnemyThreat ? 92 : 142) + (nextPlacement ? 39 : 0);
            if (state.OurTurn && sage && skull && loGrave && state.Count(Zone.Deck, Sauravis) + state.Count(Zone.Hand, Sauravis) > 0)
                score += (state.EnemyThreat ? 112 : 151) + (nextPlacement ? 48 : 0);
            if (state.OurTurn && state.Active(Blessing) && skull && lo && state.Count(Zone.Hand, Sauravis) > 0)
                score += (state.EnemyThreat ? 35 : 76) + (nextPlacement ? 25 : 0);
            if (state.EnemyThreat && skull && lo) score += 45;
            return score;
        }
    }

    public partial class VoicelessExecutor
    {
        private CancellationTokenSource _voicelessPlanningCancellation;
        private readonly object _voicelessPlanSync = new object();
        // c26866984.lua installs a persistent leave-field redirect on the actual
        // successfully summoned card. This survives new turns, unlike its use flag.
        private readonly HashSet<ClientCard> _voicelessRedirectedTrias = new HashSet<ClientCard>();
        private readonly Dictionary<string, VoicelessPlanner.Result> _voicelessPlanningCache =
            new Dictionary<string, VoicelessPlanner.Result>();
        private bool _voicelessDisconnectBound;

        private VoicelessPlanner.State CaptureVoicelessState()
        {
            var state = new VoicelessPlanner.State { NormalAvailable = !VoicelessNormalUsed,
                OurTurn = Duel.Player == 0, SearchAllowed = DefaultCheckWhetherBotCanSearch(),
                EnemyThreat = Enemy.GetMonsters().Count > 0 || Enemy.GetSpells().Count > 0 };
            foreach (int id in VoicelessPlanner.Cards)
            {
                state.Add(VoicelessPlanner.Zone.Hand, id, Bot.Hand.Count(c => c.Id == id));
                state.Add(VoicelessPlanner.Zone.Grave, id, Bot.Graveyard.Count(c => c.Id == id));
                state.Add(VoicelessPlanner.Zone.Banished, id, Bot.Banished.Count(c => c.Id == id && c.IsFaceup()));
                state.Add(VoicelessPlanner.Zone.Extra, id, Bot.ExtraDeck.Count(c => c.Id == id));
                state.Add(VoicelessPlanner.Zone.Deck, id, Bot.GetCardCountInDeck(id));
                var field = Bot.GetMonsters().Concat(Bot.GetSpells()).Where(c => c.Id == id && c.IsFaceup()).ToList();
                state.Add(VoicelessPlanner.Zone.Field, id, field.Count);
                if (field.Count > 0 && field.All(c => c.IsDisabled())) state.DisabledMask |= 1 << Array.IndexOf(VoicelessPlanner.Cards, id);
            }
            var known = new HashSet<int>(VoicelessPlanner.Cards);
            state.OtherMonsters = Bot.GetMonsters().Count(c => !known.Contains(c.Id) || c.IsFacedown());
            state.OtherSpells = Bot.GetSpells().Count(c => !known.Contains(c.Id) || c.IsFacedown());
            state.ExtraSpells = Bot.Hand.Concat(Bot.Graveyard).Count(c => !known.Contains(c.Id) && c.IsSpell());
            state.OtherLightTributes = Bot.Hand.Concat(Bot.GetMonsters()).Count(c => !known.Contains(c.Id)
                && c.IsMonster() && c.Level >= 7 && c.HasAttribute(CardAttribute.Light));
            state.ProperSkull = Bot.GetMonsters().Concat(Bot.Graveyard).Any(c => c.Id == VoicelessPlanner.Skull && c.ProcCompleted != 0);
            state.RedirectedTrias = Bot.GetMonsters().Count(c => c.Id == VoicelessPlanner.Trias && _voicelessRedirectedTrias.Contains(c));
            int[] ids = { VoicelessPlanner.Lo, VoicelessPlanner.Lo, VoicelessPlanner.Barrier,
                VoicelessPlanner.Saffira, VoicelessPlanner.Saffira, VoicelessPlanner.Skull,
                VoicelessPlanner.Blessing, VoicelessPlanner.Blessing, VoicelessPlanner.Diviner,
                VoicelessPlanner.Trias, VoicelessPlanner.PrePrep, VoicelessPlanner.Sage };
            int[] effects = { 0, 1, 0, 0, 1, 0, 0, 1, 0, 0, 0, 0 };
            for (int index = 0; index < ids.Length; index++) state.Used[index] = VoicelessEffectUsed(ids[index], effects[index]);
            return state;
        }

        public override void OnChainSolved(int chainIndex)
        {
            ChainInfo chain = Duel.GetCurrentSolvingChainInfo();
            if (chain != null && chain.ActivatePlayer == 0 && chain.IsActivateCode(VoicelessPlanner.Trias)
                && !Duel.IsCurrentSolvingChainNegated() && chain.RelatedCard != null
                && chain.RelatedCard.Location == CardLocation.MonsterZone && chain.RelatedCard.IsFaceup()
                && (chain.ActivateLocation == CardLocation.Hand || chain.ActivateLocation == CardLocation.Grave))
                _voicelessRedirectedTrias.Add(chain.RelatedCard);
            base.OnChainSolved(chainIndex);
        }

        public override void OnMove(ClientCard card, int previousControler, int previousLocation, int currentControler, int currentLocation)
        {
            if (card != null && previousLocation == (int)CardLocation.MonsterZone && currentLocation != (int)CardLocation.MonsterZone)
                _voicelessRedirectedTrias.Remove(card);
            base.OnMove(card, previousControler, previousLocation, currentControler, currentLocation);
        }

        public override void OnPosChange(ClientCard card, int previousPosition, int currentPosition)
        {
            // RESETS_REDIRECT in the selected script includes RESET_TURN_SET.
            if (card != null && (currentPosition & (int)CardPosition.FaceDown) != 0)
                _voicelessRedirectedTrias.Remove(card);
            base.OnPosChange(card, previousPosition, currentPosition);
        }

        private VoicelessPlanner.Result PlanVoiceless(int source)
        {
            VoicelessPlanner.State state = CaptureVoicelessState();
            string key = source + ":" + state.Key();
            VoicelessPlanner.Result result;
            CancellationToken cancellation;
            lock (_voicelessPlanSync)
            {
                if (!_voicelessDisconnectBound && AI.Game != null && AI.Game.Connection != null)
                {
                    AI.Game.Connection.Disconnected += delegate(Exception error) { OnVoicelessPlannerReset(); };
                    _voicelessDisconnectBound = true;
                }
                if (_voicelessPlanningCancellation == null) _voicelessPlanningCancellation = new CancellationTokenSource();
                cancellation = _voicelessPlanningCancellation.Token;
                if (_voicelessPlanningCache.TryGetValue(key, out result)) return result;
            }
            result = source == 0 ? VoicelessPlanner.Plan(state, cancellation)
                : VoicelessPlanner.ChooseSearch(state, source, cancellation);
            // Bounded per-instance cache. Observed counts, usage, disabled status and
            // public threat presence are all in the key; predictions never enter it.
            lock (_voicelessPlanSync)
            {
                if (cancellation.IsCancellationRequested) return null;
                if (_voicelessPlanningCache.Count >= 24) _voicelessPlanningCache.Clear();
                if (result != null) _voicelessPlanningCache[key] = result;
            }
            return result;
        }

        private int GetVoicelessSearch(int sourceId)
        {
            VoicelessPlanner.Result result = PlanVoiceless(sourceId);
            return result == null ? 0 : result.TargetId;
        }

        private bool ShouldUseVoicelessLink(int id)
        {
            VoicelessPlanner.Result result = PlanVoiceless(0);
            if (result == null) return false;
            return id == VoicelessPlanner.Dyna && result.FirstAction == VoicelessPlanner.ActionKind.DynaMondo
                || id == VoicelessPlanner.Linkuriboh && result.FirstAction == VoicelessPlanner.ActionKind.Linkuriboh
                || id == VoicelessPlanner.Gardna && result.FirstAction == VoicelessPlanner.ActionKind.SecureGardna;
        }

        private string GetVoicelessRoute()
        {
            VoicelessPlanner.Result result = PlanVoiceless(0);
            return result == null ? "Retired" : string.Join(" > ", result.Steps.Select(step => step.ToString()));
        }

        private void OnVoicelessPlannerReset()
        {
            lock (_voicelessPlanSync)
            {
                if (_voicelessPlanningCancellation != null)
                {
                    _voicelessPlanningCancellation.Cancel();
                    _voicelessPlanningCancellation.Dispose();
                    _voicelessPlanningCancellation = null;
                }
                _voicelessPlanningCache.Clear();
            }
        }
    }
}
