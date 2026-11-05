using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using YGOSharp.OCGWrapper.Enums;

namespace WindBot.Game.AI.Decks
{
    public partial class Witchcraft2026Executor
    {
        internal enum WitchcraftActionKind
        {
            None,
            ActivateFieldSpellAccess,
            ActivateMastery,
            ActivateCreation,
            ActivateMarka,
            ActivateWalpurgis,
            SummonMaidVerre,
            NormalSchmietta,
            NormalLowWitchcrafter,
            ActivateLowWitchcrafter,
            ActivateTerracottan,
            ActivateCelebration,
            ActivateConfusion,
            SetDistortion
        }

        internal sealed class WitchcraftStrategicAction
        {
            public WitchcraftActionKind Kind;
            public int SearchTarget;

            public WitchcraftStrategicAction(WitchcraftActionKind kind, int searchTarget)
            {
                Kind = kind;
                SearchTarget = searchTarget;
            }
        }

        internal sealed class WitchcraftStrategicState
        {
            public bool MainPhase;
            public bool NormalAvailable;
            public bool SpellActivated;
            public bool EnemyDrawPressure;
            public bool HandFieldSpellAccess;
            public bool HandMastery;
            public bool HandCreation;
            public bool HandMarka;
            public bool HandWalpurgis;
            public bool HandMaidVerre;
            public bool HandSchmietta;
            public bool HandOtherLow;
            public bool HandTerracottan;
            public bool HandCelebration;
            public bool HandConfusion;
            public bool HandDistortion;
            public int HandLowId;
            public int FieldLowId;
            public int GuideDepth;
            public bool FieldWalpurgis;
            public bool FieldMaidVerre;
            public bool FieldLow;
            public bool FieldLevelFiveWitchcrafter;
            public bool GraveSchmietta;
            public bool GraveMadameVerre;
            public bool GraveCelebration;
            public bool GraveWitchcrafterSpell;
            public bool LowEffectUsed;
            public bool ExtraViceMadame;
            public bool ExtraMadameRillie;
            public bool ExtraPupils;
            public bool BoardViceMadame;
            public bool BoardMadameRillie;
            public bool BoardPupils;
            public bool SetDistortion;
            public bool WitchcrafterExtraLock;
            public int HandSpellCount;
            public int Steps;

            public WitchcraftStrategicState Clone()
            {
                return (WitchcraftStrategicState)MemberwiseClone();
            }

            public string Key()
            {
                return string.Join("|", new object[]
                {
                    MainPhase, NormalAvailable, SpellActivated, EnemyDrawPressure,
                    HandFieldSpellAccess, HandMastery, HandCreation, HandMarka,
                    HandWalpurgis, HandMaidVerre,
                    HandSchmietta, HandOtherLow, HandTerracottan, HandCelebration,
                    HandConfusion, HandDistortion, HandLowId, FieldLowId, GuideDepth,
                    FieldWalpurgis, FieldMaidVerre,
                    FieldLow, FieldLevelFiveWitchcrafter, GraveSchmietta,
                    GraveMadameVerre, GraveCelebration, GraveWitchcrafterSpell,
                    LowEffectUsed,
                    ExtraViceMadame, ExtraMadameRillie, ExtraPupils, BoardViceMadame,
                    BoardMadameRillie, BoardPupils, SetDistortion,
                    WitchcrafterExtraLock, HandSpellCount
                });
            }
        }

        internal sealed class WitchcraftStrategicStep
        {
            public WitchcraftStrategicAction Action;
            public WitchcraftStrategicState After;

            public WitchcraftStrategicStep(WitchcraftStrategicAction action,
                WitchcraftStrategicState after)
            {
                Action = action;
                After = after;
            }
        }

        internal sealed class WitchcraftStrategicPlan
        {
            public WitchcraftStrategicState Root;
            public IList<WitchcraftStrategicStep> Steps;
            public int Expanded;
            public bool TimedOut;

            public WitchcraftStrategicPlan(WitchcraftStrategicState root,
                IList<WitchcraftStrategicStep> steps, int expanded, bool timedOut)
            {
                Root = root;
                Steps = steps;
                Expanded = expanded;
                TimedOut = timedOut;
            }
        }

        internal static class WitchcraftStrategicPlanner
        {
            private const int TimeLimitMilliseconds = 45;
            private const int MaxDepth = 12;
            private const int BeamWidth = 96;
            private const int MaxExpanded = 3500;
            private const int MaxCandidates = 8000;

            private sealed class Node
            {
                public WitchcraftStrategicState State;
                public List<WitchcraftStrategicStep> Steps;
                public double Score;
            }

            public static WitchcraftStrategicPlan Plan(WitchcraftStrategicState root,
                CancellationToken cancellation)
            {
                Stopwatch watch = Stopwatch.StartNew();
                int expanded = 0;
                bool timedOut = false;
                Node rootNode = new Node
                {
                    State = root,
                    Steps = new List<WitchcraftStrategicStep>(),
                    Score = Evaluate(root)
                };
                Node best = rootNode;
                List<Node> frontier = new List<Node> { rootNode };

                for (int depth = 0; depth < MaxDepth && frontier.Count > 0; depth++)
                {
                    if (cancellation.IsCancellationRequested)
                        return new WitchcraftStrategicPlan(root,
                            new List<WitchcraftStrategicStep>(), expanded, false);
                    if (watch.ElapsedMilliseconds >= TimeLimitMilliseconds || expanded >= MaxExpanded)
                    {
                        timedOut = true;
                        break;
                    }

                    List<Node> candidates = new List<Node>();
                    foreach (Node node in frontier)
                    {
                        if (cancellation.IsCancellationRequested)
                            return new WitchcraftStrategicPlan(root,
                                new List<WitchcraftStrategicStep>(), expanded, false);
                        if (watch.ElapsedMilliseconds >= TimeLimitMilliseconds ||
                            expanded >= MaxExpanded || candidates.Count >= MaxCandidates)
                        {
                            timedOut = true;
                            break;
                        }
                        expanded++;
                        foreach (WitchcraftStrategicStep transition in Expand(node.State))
                        {
                            List<WitchcraftStrategicStep> steps =
                                new List<WitchcraftStrategicStep>(node.Steps) { transition };
                            double score = Evaluate(transition.After);
                            Node candidate = new Node
                            {
                                State = transition.After,
                                Steps = steps,
                                Score = score
                            };
                            candidates.Add(candidate);
                            if (score > best.Score || score == best.Score &&
                                steps.Count < best.Steps.Count)
                                best = candidate;
                        }
                    }

                    frontier = candidates
                        .GroupBy(candidate => candidate.State.Key())
                        .Select(group => group.OrderByDescending(candidate => candidate.Score)
                            .ThenBy(candidate => candidate.Steps.Count).First())
                        .OrderByDescending(candidate => candidate.Score)
                        .ThenBy(candidate => candidate.Steps.Count)
                        .Take(BeamWidth)
                        .ToList();
                }

                IList<WitchcraftStrategicStep> nextAction = best.Steps.Count == 0
                    ? new List<WitchcraftStrategicStep>()
                    : new List<WitchcraftStrategicStep> { best.Steps[0] };
                return new WitchcraftStrategicPlan(root, nextAction, expanded, timedOut);
            }

            private static IEnumerable<WitchcraftStrategicStep> Expand(
                WitchcraftStrategicState state)
            {
                if (!state.MainPhase)
                    yield break;

                if (state.HandFieldSpellAccess && !state.HandWalpurgis &&
                    !state.FieldWalpurgis)
                {
                    WitchcraftStrategicState next = Next(state);
                    next.HandFieldSpellAccess = false;
                    next.HandWalpurgis = true;
                    next.SpellActivated = true;
                    next.HandSpellCount = Math.Max(0, next.HandSpellCount - 1);
                    yield return Step(WitchcraftActionKind.ActivateFieldSpellAccess,
                        CardId.Walpurgis, next);
                }

                if (state.HandMastery)
                {
                    WitchcraftStrategicState next = Next(state);
                    next.HandMastery = false;
                    next.HandMaidVerre = true;
                    next.SpellActivated = true;
                    next.HandSpellCount = Math.Max(0, next.HandSpellCount - 1);
                    yield return Step(WitchcraftActionKind.ActivateMastery,
                        CardId.MaidVerre, next);
                }

                if (state.HandCreation)
                {
                    WitchcraftStrategicState next = Next(state);
                    next.HandCreation = false;
                    next.SpellActivated = true;
                    next.HandSpellCount = Math.Max(0, next.HandSpellCount - 1);
                    int target;
                    if (!state.FieldWalpurgis && !state.HandWalpurgis && !state.HandMarka)
                    {
                        next.HandMarka = true;
                        target = CardId.Marka;
                    }
                    else if (!state.HandSchmietta && !state.FieldLow)
                    {
                        next.HandSchmietta = true;
                        target = CardId.Schmietta;
                    }
                    else
                    {
                        next.HandTerracottan = true;
                        target = CardId.Terracottan;
                    }
                    yield return Step(WitchcraftActionKind.ActivateCreation, target, next);
                }

                if (state.HandMarka && !state.FieldWalpurgis && !state.HandWalpurgis)
                {
                    WitchcraftStrategicState next = Next(state);
                    next.HandMarka = false;
                    next.HandWalpurgis = true;
                    yield return Step(WitchcraftActionKind.ActivateMarka,
                        CardId.Walpurgis, next);
                }

                if (state.HandWalpurgis)
                {
                    WitchcraftStrategicState next = Next(state);
                    next.HandWalpurgis = false;
                    next.FieldWalpurgis = true;
                    next.SpellActivated = true;
                    next.HandOtherLow = true;
                    next.HandLowId = CardId.Genni;
                    next.GuideDepth = Math.Max(next.GuideDepth, 1);
                    yield return Step(WitchcraftActionKind.ActivateWalpurgis,
                        CardId.Genni, next);
                }

                if (state.HandMaidVerre && state.SpellActivated && !state.FieldMaidVerre)
                {
                    WitchcraftStrategicState next = Next(state);
                    next.HandMaidVerre = false;
                    next.FieldMaidVerre = true;
                    next.FieldLevelFiveWitchcrafter = true;
                    if (!next.HandSchmietta && !next.FieldLow)
                        next.HandCreation = true;
                    else
                        next.HandCelebration = true;
                    next.HandSpellCount++;
                    yield return Step(WitchcraftActionKind.SummonMaidVerre,
                        !state.HandSchmietta && !state.FieldLow
                            ? CardId.Creation : CardId.Celebration, next);
                }

                if (state.NormalAvailable && state.HandSchmietta)
                {
                    WitchcraftStrategicState next = Next(state);
                    next.NormalAvailable = false;
                    next.HandSchmietta = false;
                    next.FieldLow = true;
                    next.FieldLowId = CardId.Schmietta;
                    yield return Step(WitchcraftActionKind.NormalSchmietta, 0, next);
                }

                if (state.NormalAvailable && state.HandOtherLow)
                {
                    WitchcraftStrategicState next = Next(state);
                    next.NormalAvailable = false;
                    next.HandOtherLow = false;
                    next.FieldLow = true;
                    next.FieldLowId = state.HandLowId != 0
                        ? state.HandLowId : CardId.Genni;
                    next.HandLowId = 0;
                    if (next.FieldLowId == CardId.Genni && next.FieldWalpurgis)
                        next.GuideDepth = Math.Max(next.GuideDepth, 2);
                    yield return Step(WitchcraftActionKind.NormalLowWitchcrafter, 0, next);
                }

                if (state.FieldLow && !state.LowEffectUsed &&
                    (state.FieldWalpurgis || state.HandSpellCount > 0))
                {
                    int target = GetNextGuideLowWitchcrafter(state.FieldLowId);
                    if (target != 0)
                    {
                        WitchcraftStrategicState next = Next(state);
                        next.GraveSchmietta = next.GraveSchmietta ||
                            state.FieldLowId == CardId.Schmietta;
                        next.LowEffectUsed = false;
                        next.GraveWitchcrafterSpell = true;
                        next.GraveCelebration = next.GraveCelebration ||
                            state.FieldLowId == CardId.Schmietta;
                        next.FieldLowId = target;
                        next.GuideDepth = Math.Max(next.GuideDepth,
                            target == CardId.Schmietta ? 3 :
                            target == CardId.Pittore ? 4 :
                            target == CardId.Potterie ? 5 : 6);
                        if (!state.FieldWalpurgis)
                            next.HandSpellCount = Math.Max(0, next.HandSpellCount - 1);
                        yield return Step(WitchcraftActionKind.ActivateLowWitchcrafter,
                            target, next);
                    }
                }

                if (state.HandTerracottan && state.GraveWitchcrafterSpell)
                {
                    WitchcraftStrategicState next = Next(state);
                    next.HandTerracottan = false;
                    next.FieldLow = true;
                    next.HandCelebration = true;
                    next.HandSpellCount++;
                    next.WitchcrafterExtraLock = true;
                    yield return Step(WitchcraftActionKind.ActivateTerracottan,
                        CardId.Celebration, next);
                }

                bool fusionMaterials = state.GraveMadameVerre &&
                    (state.FieldLow || state.FieldMaidVerre || state.FieldLevelFiveWitchcrafter);
                if (state.HandCelebration && fusionMaterials &&
                    (state.ExtraMadameRillie || state.ExtraViceMadame || state.ExtraPupils))
                {
                    WitchcraftStrategicState next = Next(state);
                    next.HandCelebration = false;
                    next.GraveCelebration = true;
                    next.HandSpellCount = Math.Max(0, next.HandSpellCount - 1);
                    ApplyFusion(next);
                    yield return Step(WitchcraftActionKind.ActivateCelebration,
                        FusionTarget(next), next);
                }

                if (state.HandConfusion &&
                    (state.FieldLow || state.FieldMaidVerre) &&
                    (state.ExtraViceMadame || state.ExtraPupils))
                {
                    WitchcraftStrategicState next = Next(state);
                    next.HandConfusion = false;
                    next.HandSpellCount = Math.Max(0, next.HandSpellCount - 1);
                    next.SpellActivated = true;
                    ApplyFusion(next);
                    yield return Step(WitchcraftActionKind.ActivateConfusion,
                        FusionTarget(next), next);
                }

                if (state.HandDistortion && !state.SetDistortion && state.FieldLevelFiveWitchcrafter)
                {
                    WitchcraftStrategicState next = Next(state);
                    next.HandDistortion = false;
                    next.SetDistortion = true;
                    yield return Step(WitchcraftActionKind.SetDistortion, 0, next);
                }
            }

            private static WitchcraftStrategicState Next(WitchcraftStrategicState state)
            {
                WitchcraftStrategicState next = state.Clone();
                next.Steps++;
                return next;
            }

            private static WitchcraftStrategicStep Step(WitchcraftActionKind kind,
                int target, WitchcraftStrategicState after)
            {
                return new WitchcraftStrategicStep(
                    new WitchcraftStrategicAction(kind, target), after);
            }

            private static void ApplyFusion(WitchcraftStrategicState state)
            {
                state.FieldLow = false;
                if (state.ExtraMadameRillie && state.GraveMadameVerre)
                {
                    state.ExtraMadameRillie = false;
                    state.BoardMadameRillie = true;
                    state.FieldLevelFiveWitchcrafter = true;
                }
                else if (state.ExtraViceMadame)
                {
                    state.ExtraViceMadame = false;
                    state.BoardViceMadame = true;
                }
                else if (state.ExtraPupils)
                {
                    state.ExtraPupils = false;
                    state.BoardPupils = true;
                }
            }

            private static int FusionTarget(WitchcraftStrategicState state)
            {
                if (state.BoardMadameRillie)
                    return CardId.MadameRillie;
                if (state.BoardViceMadame)
                    return CardId.ViceMadame;
                if (state.BoardPupils)
                    return CardId.Pupils;
                return 0;
            }

            private static double Evaluate(WitchcraftStrategicState state)
            {
                double score = 0;
                if (state.BoardMadameRillie) score += 32;
                if (state.BoardViceMadame) score += 26;
                if (state.BoardPupils) score += 20;
                if (state.FieldLevelFiveWitchcrafter) score += 11;
                if (state.FieldMaidVerre) score += 8;
                if (state.FieldWalpurgis) score += 7;
                if (state.SetDistortion) score += 12;
                if (state.HandCelebration) score += 4;
                if (state.GraveCelebration) score += 4;
                if (state.GraveMadameVerre) score += 5;
                score += state.GuideDepth * 12;
                score += Math.Min(5, state.HandSpellCount);
                if (state.WitchcrafterExtraLock && !state.BoardMadameRillie &&
                    !state.BoardViceMadame && !state.BoardPupils) score -= 7;
                if (state.EnemyDrawPressure)
                    score -= state.Steps * 1.8;
                else
                    score -= state.Steps * 0.15;
                return score;
            }
        }

        private WitchcraftStrategicPlan _strategicPlan;
        private CancellationTokenSource _strategicRequest;
        private int _strategicActionTarget;

        private bool StrategicAllows(WitchcraftActionKind kind)
        {
            if (!ValidateDeck())
                return false;
            WitchcraftStrategicState observed = CaptureStrategicState();
            if (_strategicPlan == null ||
                !SameStrategicState(_strategicPlan.Root, observed))
            {
                RetireStrategicPlan();
                _strategicRequest = new CancellationTokenSource();
                _strategicPlan = WitchcraftStrategicPlanner.Plan(observed,
                    _strategicRequest.Token);
                if (_strategicPlan.Steps.Count > 0)
                {
                    WitchcraftStrategicAction action = _strategicPlan.Steps[0].Action;
                    Logger.DebugWriteLine("Witchcraft2026 next action: " + action.Kind +
                        " target=" + action.SearchTarget + " expanded=" +
                        _strategicPlan.Expanded + " timedOut=" + _strategicPlan.TimedOut);
                }
                else
                {
                    Logger.DebugWriteLine("Witchcraft2026 next action: none expanded=" +
                        _strategicPlan.Expanded + " timedOut=" + _strategicPlan.TimedOut);
                }
            }
            if (_strategicPlan == null || _strategicPlan.Steps.Count == 0)
                return false;
            WitchcraftStrategicStep next = _strategicPlan.Steps[0];
            if (next.Action.Kind != kind)
                return false;
            _strategicActionTarget = next.Action.SearchTarget;
            RetireStrategicPlan();
            return true;
        }

        private int ConsumeStrategicActionTarget(int fallback)
        {
            int target = _strategicActionTarget;
            _strategicActionTarget = 0;
            return target != 0 ? target : fallback;
        }

        private static bool SameStrategicState(WitchcraftStrategicState expected,
            WitchcraftStrategicState observed)
        {
            return expected != null && observed != null && expected.Key() == observed.Key();
        }

        private WitchcraftStrategicState CaptureStrategicState()
        {
            IList<ClientCard> faceupLowWitchcrafters = Bot.GetMonsters()
                .Where(card => card.IsFaceup() && IsLowWitchcrafter(card.Id))
                .ToList();
            ClientCard handLow = Bot.Hand.FirstOrDefault(card => card.IsCode(
                CardId.Genni, CardId.Schmietta, CardId.Pittore, CardId.Potterie));
            ClientCard fieldLow = faceupLowWitchcrafters.FirstOrDefault();
            int fieldLowId = fieldLow == null ? 0 : fieldLow.Id;
            int guideDepth = Bot.HasInSpellZone(CardId.Walpurgis, true, true) ? 1 : 0;
            if (fieldLowId == CardId.Genni) guideDepth = 2;
            else if (fieldLowId == CardId.Schmietta) guideDepth = 3;
            else if (fieldLowId == CardId.Pittore) guideDepth = 4;
            else if (fieldLowId == CardId.Potterie) guideDepth = 5;
            else if (fieldLowId == CardId.Marka) guideDepth = 6;
            return new WitchcraftStrategicState
            {
                MainPhase = Duel.Player == 0 &&
                    (Duel.Phase == DuelPhase.Main1 || Duel.Phase == DuelPhase.Main2),
                NormalAvailable = !_normalSummonUsed,
                SpellActivated = _spellActivatedThisTurn,
                EnemyDrawPressure = enemyResolvedEffectIdList.Contains(CardId.MaxxC),
                HandFieldSpellAccess = Bot.HasInHand(CardId.Terraforming) ||
                    Bot.HasInHand(CardId.RainbowBridgeBifrost) && CanPayBifrostCost(),
                HandMastery = Bot.HasInHand(CardId.SpellPowerMastery),
                HandCreation = Bot.HasInHand(CardId.Creation),
                HandMarka = Bot.HasInHand(CardId.Marka),
                HandWalpurgis = Bot.HasInHand(CardId.Walpurgis),
                HandMaidVerre = Bot.HasInHand(CardId.MaidVerre),
                HandSchmietta = Bot.HasInHand(CardId.Schmietta),
                HandOtherLow = Bot.Hand.Any(card => card.IsCode(
                    CardId.Pittore, CardId.Genni, CardId.Potterie)),
                HandLowId = handLow == null ? 0 : handLow.Id,
                FieldLowId = fieldLowId,
                GuideDepth = guideDepth,
                HandTerracottan = Bot.HasInHand(CardId.Terracottan),
                HandCelebration = Bot.HasInHand(CardId.Celebration),
                HandConfusion = Bot.HasInHand(CardId.ConfusionConfession),
                HandDistortion = Bot.HasInHand(CardId.Distortion),
                FieldWalpurgis = Bot.HasInSpellZone(CardId.Walpurgis, true, true),
                FieldMaidVerre = Bot.HasInMonstersZone(CardId.MaidVerre, true),
                FieldLow = faceupLowWitchcrafters.Count > 0 || Bot.GetMonsters().Any(card =>
                    card.IsFaceup() && card.IsCode(CardId.Seed, CardId.MaidGenni,
                        CardId.Terracottan)),
                FieldLevelFiveWitchcrafter = Bot.GetMonsters().Any(card => card.IsFaceup() &&
                    card.HasSetcode(WitchcrafterSetcode) && card.Level >= 5),
                GraveSchmietta = Bot.HasInGraveyard(CardId.Schmietta),
                GraveMadameVerre = Bot.HasInGraveyard(CardId.MadameVerre),
                GraveCelebration = Bot.HasInGraveyard(CardId.Celebration),
                GraveWitchcrafterSpell = Bot.Graveyard.Any(card =>
                    card.IsSpell() && card.HasSetcode(WitchcrafterSetcode)),
                LowEffectUsed = faceupLowWitchcrafters.Count > 0 &&
                    faceupLowWitchcrafters.All(card =>
                        _usedLowWitchcrafterEffects.Contains(card.Id)),
                ExtraViceMadame = Bot.HasInExtra(CardId.ViceMadame),
                ExtraMadameRillie = Bot.HasInExtra(CardId.MadameRillie),
                ExtraPupils = Bot.HasInExtra(CardId.Pupils),
                BoardViceMadame = Bot.HasInMonstersZone(CardId.ViceMadame, true),
                BoardMadameRillie = Bot.HasInMonstersZone(CardId.MadameRillie, true),
                BoardPupils = Bot.HasInMonstersZone(CardId.Pupils, true),
                SetDistortion = Bot.HasInSpellZone(CardId.Distortion),
                HandSpellCount = Bot.Hand.Count(card => card.IsSpell())
            };
        }

        private void RetireStrategicPlan()
        {
            if (_strategicRequest != null)
            {
                _strategicRequest.Cancel();
                _strategicRequest.Dispose();
                _strategicRequest = null;
            }
            _strategicPlan = null;
        }
    }
}
