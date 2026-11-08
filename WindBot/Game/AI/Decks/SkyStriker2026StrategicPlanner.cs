using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using YGOSharp.OCGWrapper.Enums;
using WindBot.Game;

namespace WindBot.Game.AI.Decks
{
    // Bounded RDA-style route planner for the normal SkyStriker2026 executor.
    // It models only public own resources and observed opponent counts. The normal
    // executor remains the authority for legal offers, costs, targets and prompts.
    public partial class SkyStriker2026Executor
    {
        private enum SkyStrategicActionKind
        {
            None,
            ActivateReinforcement,
            ActivateEngage,
            ActivatePot,
            NormalRaye,
            NormalRoze,
            ActivateHornet,
            RayeTagZero,
            LinkKagari,
            LinkBridge,
            LinkZero,
            LinkShizuku,
            ActivateUltimateLinkage,
            SetLinkageFuel,
            ActivateLinkage,
            ResolveZeroSearch,
            LinkBattleStarter
        }

        private enum SkyStrategicExtraKind
        {
            None,
            Zero,
            Kagari,
            Bridge,
            Shizuku,
            Other
        }

        private sealed class SkyStrategicAction
        {
            public SkyStrategicActionKind Kind;
            public int SearchTarget;
            public string Text;
        }

        private sealed class SkyStrategicTransition
        {
            public SkyStrategicAction Action;
            public SkyStrategicState After;
        }

        private sealed class SkyStrategicPlan
        {
            public SkyStrategicState Root;
            public readonly List<SkyStrategicTransition> Steps =
                new List<SkyStrategicTransition>();
            public double Score;
            public int Expanded;
            public bool TimedOut;
            public int ConsideredRootActions;
            public string Outcome = "Unmodeled";
            public string Reason = "no modeled legal root";
        }

        private sealed class SkyStrategicState : IEquatable<SkyStrategicState>
        {
            public bool MainPhase;
            // Only the first step is filtered by the server's current offers.
            public int RootActionMask = -1;
            public bool Main1;
            public bool Turn1;
            public bool NormalAvailable;
            public bool CanSearch = true;
            public bool CanDraw = true;
            public bool PotPermitted = true;
            public int MainRaye;
            public int MainRoze;
            public int MainToken;
            public int MainOther;
            public SkyStrategicExtraKind ExtraKind;
            public bool FieldFuel;
            public bool HandRaye;
            public bool HandRoze;
            public bool HandEngage;
            public bool HandReinforcement;
            public bool HandHornet;
            public bool HandLinkage;
            public int HandLinkageFuelId;
            public int HandLinkageFuelCost;
            public bool HandUltimate;
            public bool HandWidow;
            public bool HandLemnisgate;
            public bool HandMultirole;
            public bool HandPot;
            public bool LemnisgateLive;
            public bool DeckRaye;
            public bool DeckRoze;
            public bool DeckEngage;
            public bool DeckHornet;
            public bool DeckLinkage;
            public bool DeckWidow;
            public bool DeckLemnisgate;
            public bool DeckMultirole;
            public bool GraveEngage;
            public int GraveEngageCopies;
            public bool GraveHornet;
            public int GraveSpells;
            public int GraveAceMonsters;
            public bool GraveRaye;
            public bool GraveRoze;
            public int BattleStarterAttack;
            public bool ExtraZero;
            public bool ExtraKagari;
            public bool ExtraBridge;
            public bool ExtraHayate;
            public bool ExtraShizuku;
            public bool ZeroUsed;
            public bool KagariUsed;
            public bool PendingZeroSearch;
            public bool HornetSetup;
            public int EnemyMonsters;
            public int EnemyField;
            public int EnemyLife;
            public int DeckCount;
            public int Advantage;
            public int PotRisk;
            public int ResourceCost;
            public int Step;

            public int MainCount
            {
                get { return MainRaye + MainRoze + MainToken + MainOther; }
            }

            public bool EmptyMainZone
            {
                get { return MainCount == 0; }
            }

            public bool CanUseSkyStrikerSpell
            {
                get { return MainPhase && EmptyMainZone; }
            }

            public SkyStrategicState Clone()
            {
                return (SkyStrategicState)MemberwiseClone();
            }

            public bool Equals(SkyStrategicState other)
            {
                return other != null && GetKey() == other.GetKey();
            }

            public override bool Equals(object obj)
            {
                return Equals(obj as SkyStrategicState);
            }

            public override int GetHashCode()
            {
                return GetKey().GetHashCode();
            }

            public string GetKey()
            {
                return string.Join("|", new[]
                {
                    MainPhase ? "1" : "0", RootActionMask.ToString(),
                    Main1 ? "1" : "0", Turn1 ? "1" : "0",
                    NormalAvailable ? "1" : "0", MainRaye.ToString(), MainRoze.ToString(),
                    CanSearch ? "1" : "0", CanDraw ? "1" : "0", PotPermitted ? "1" : "0",
                    MainToken.ToString(), MainOther.ToString(), ((int)ExtraKind).ToString(),
                    FieldFuel ? "1" : "0", HandRaye ? "1" : "0", HandRoze ? "1" : "0",
                    HandEngage ? "1" : "0", HandReinforcement ? "1" : "0",
                    HandHornet ? "1" : "0", HandLinkage ? "1" : "0",
                    HandLinkageFuelId.ToString(), HandLinkageFuelCost.ToString(),
                    HandUltimate ? "1" : "0", HandWidow ? "1" : "0",
                    HandLemnisgate ? "1" : "0", HandMultirole ? "1" : "0",
                    HandPot ? "1" : "0", LemnisgateLive ? "1" : "0",
                    DeckRaye ? "1" : "0", DeckRoze ? "1" : "0",
                    DeckEngage ? "1" : "0", DeckHornet ? "1" : "0",
                    DeckLinkage ? "1" : "0", DeckWidow ? "1" : "0",
                    DeckLemnisgate ? "1" : "0", DeckMultirole ? "1" : "0",
                    GraveEngage ? "1" : "0", GraveEngageCopies.ToString(),
                    GraveHornet ? "1" : "0",
                    GraveSpells.ToString(), GraveAceMonsters.ToString(),
                    GraveRaye ? "1" : "0", GraveRoze ? "1" : "0",
                    BattleStarterAttack.ToString(),
                    ExtraZero ? "1" : "0", ExtraKagari ? "1" : "0",
                    ExtraBridge ? "1" : "0", ExtraShizuku ? "1" : "0",
                    ExtraHayate ? "1" : "0",
                    ZeroUsed ? "1" : "0", KagariUsed ? "1" : "0",
                    PendingZeroSearch ? "1" : "0", HornetSetup ? "1" : "0",
                    EnemyMonsters.ToString(), EnemyField.ToString(), EnemyLife.ToString(),
                    DeckCount.ToString(), Advantage.ToString(), PotRisk.ToString(),
                    ResourceCost.ToString(), Step.ToString()
                });
            }
        }

        private sealed class SkyStrategicNode
        {
            public SkyStrategicState State;
            public readonly List<SkyStrategicTransition> Steps =
                new List<SkyStrategicTransition>();
            public double Score;
        }

        private static class SkyStrategicPlanner
        {
            private const int TimeLimitMilliseconds = 35;
            private const int MaximumDepth = 8;
            private const int BeamWidth = 72;
            private const int MaximumExpanded = 2500;
            private const int MaximumCandidates = 6000;

            public static SkyStrategicPlan Plan(SkyStrategicState root,
                CancellationToken cancellation)
            {
                var result = new SkyStrategicPlan
                {
                    Root = root,
                    Score = EvaluateTerminal(root)
                };
                if (root == null || !root.MainPhase)
                    return result;

                var watch = Stopwatch.StartNew();
                var frontier = new List<SkyStrategicNode>
                {
                    new SkyStrategicNode { State = root, Score = Evaluate(root) }
                };
                SkyStrategicNode best = null;
                double bestScore = EvaluateTerminal(root);
                int expanded = 0;
                int generated = 0;

                for (int depth = 0; depth < MaximumDepth && frontier.Count > 0; depth++)
                {
                    var candidates = new List<SkyStrategicNode>();
                    foreach (SkyStrategicNode node in frontier)
                    {
                        if (Expired(watch, cancellation) || expanded >= MaximumExpanded ||
                            generated >= MaximumCandidates)
                            break;
                        expanded++;
                        foreach (SkyStrategicTransition transition in Expand(node.State,
                            cancellation))
                        {
                            if (Expired(watch, cancellation) || generated++ >= MaximumCandidates)
                                break;
                            if (depth == 0 && !root.PendingZeroSearch &&
                                (root.RootActionMask & (1 << (int)transition.Action.Kind)) == 0)
                                continue;
                            if (depth == 0)
                                result.ConsideredRootActions |= 1 << (int)transition.Action.Kind;
                            var next = new SkyStrategicNode
                            {
                                State = transition.After,
                                Score = Evaluate(transition.After)
                            };
                            next.Steps.AddRange(node.Steps);
                            next.Steps.Add(transition);
                            candidates.Add(next);
                            double terminalScore = EvaluateTerminal(next.State);
                            if (terminalScore > bestScore)
                            {
                                best = next;
                                bestScore = terminalScore;
                            }
                        }
                    }
                    if (Expired(watch, cancellation) || expanded >= MaximumExpanded ||
                        generated >= MaximumCandidates)
                        break;
                    frontier = candidates
                        .GroupBy(node => node.State.GetKey())
                        .Select(group => group.OrderByDescending(node => node.Score).First())
                        .OrderByDescending(node => node.Score)
                        .Take(BeamWidth)
                        .ToList();
                }

                result.Expanded = expanded;
                result.TimedOut = Expired(watch, cancellation);
                result.Outcome = result.TimedOut || expanded >= MaximumExpanded || generated >= MaximumCandidates
                    ? "Incomplete" : result.ConsideredRootActions == 0 ? "Unmodeled" : "NoImprovement";
                result.Reason = result.Outcome == "Incomplete" ? "bounded search exhausted; local evaluation required" :
                    result.Outcome == "Unmodeled" ? "no supported root; local evaluation required" :
                    "projection found no better terminal; not proof that PASS dominates real actions";
                if (!cancellation.IsCancellationRequested && best != null &&
                    bestScore > EvaluateTerminal(root) + 100)
                {
                    result.Steps.AddRange(best.Steps);
                    result.Score = bestScore;
                    result.Outcome = "Route";
                    result.Reason = "completed modeled route improves projected terminal";
                }
                return result;
            }

            private static bool Expired(Stopwatch watch, CancellationToken cancellation)
            {
                return cancellation.IsCancellationRequested ||
                    watch.ElapsedMilliseconds >= TimeLimitMilliseconds;
            }

            private static IEnumerable<SkyStrategicTransition> Expand(
                SkyStrategicState state, CancellationToken cancellation)
            {
                if (cancellation.IsCancellationRequested)
                    yield break;

                if (state.PendingZeroSearch)
                {
                    foreach (SkyStrategicTransition transition in ExpandZeroSearch(state))
                        yield return transition;
                    yield break;
                }

                // Deterministic deck access is deliberately expanded before Pot.
                if (state.CanSearch && state.CanUseSkyStrikerSpell && state.HandReinforcement &&
                    state.DeckRaye)
                {
                    SkyStrategicState after = Next(state);
                    after.HandReinforcement = false;
                    ConsumeHandFuel(after, CardId.ReinforcementOfTheArmy);
                    after.HandRaye = true;
                    after.DeckRaye = false;
                    after.GraveSpells++;
                    yield return Move(SkyStrategicActionKind.ActivateReinforcement,
                        "ROTA -> Raye", after);
                }

                if (state.CanSearch && state.CanUseSkyStrikerSpell && state.HandEngage)
                {
                    foreach (int target in GetEngageTargets(state))
                    {
                        SkyStrategicState after = Next(state);
                        int spellsBefore = after.GraveSpells;
                        after.HandEngage = false;
                        ConsumeHandFuel(after, CardId.Engage);
                        after.GraveEngage = true;
                        after.GraveEngageCopies++;
                        after.GraveSpells++;
                        if (spellsBefore >= 3 && state.CanDraw)
                            after.Advantage++;
                        AddSearchTarget(after, target, false);
                        yield return Move(SkyStrategicActionKind.ActivateEngage,
                            "Engage -> " + GetCardLabel(target), after, target);
                    }
                }

                if (state.CanUseSkyStrikerSpell && state.HandUltimate &&
                    state.HandLinkage && state.ExtraKind == SkyStrategicExtraKind.None &&
                    state.EnemyMonsters > 0 && state.ExtraZero && !state.ZeroUsed)
                {
                    SkyStrategicState after = Next(state);
                    after.HandUltimate = false;
                    ConsumeHandFuel(after, CardId.UltimateSky);
                    after.HandLinkage = false;
                    after.GraveSpells += 2;
                    after.ExtraKind = SkyStrategicExtraKind.Zero;
                    after.ExtraZero = false;
                    after.ZeroUsed = true;
                    after.PendingZeroSearch = true;
                    yield return Move(SkyStrategicActionKind.ActivateUltimateLinkage,
                        "Ultimate Sky -> chain Linkage -> Zero", after);
                }

                if (state.CanUseSkyStrikerSpell && state.HandLinkage &&
                    !state.FieldFuel && state.HandLinkageFuelId != 0 &&
                    state.ExtraKind == SkyStrategicExtraKind.None && state.ExtraZero &&
                    !state.ZeroUsed)
                {
                    SkyStrategicState after = Next(state);
                    int fuelId = after.HandLinkageFuelId;
                    after.HandLinkageFuelId = 0;
                    after.ResourceCost += after.HandLinkageFuelCost;
                    after.HandLinkageFuelCost = 0;
                    after.FieldFuel = true;
                    if (fuelId == CardId.Lemnisgate)
                        after.HandLemnisgate = false;
                    else if (fuelId == CardId.WidowAnchor)
                        after.HandWidow = false;
                    else if (fuelId == CardId.UltimateSky)
                        after.HandUltimate = false;
                    else if (fuelId == CardId.Engage)
                        after.HandEngage = false;
                    else if (fuelId == CardId.HornetDrones)
                        after.HandHornet = false;
                    else if (fuelId == CardId.ReinforcementOfTheArmy)
                        after.HandReinforcement = false;
                    else if (fuelId == CardId.PotOfDesires)
                        after.HandPot = false;
                    else if (fuelId == CardId.Multirole)
                        after.HandMultirole = false;
                    yield return Move(SkyStrategicActionKind.SetLinkageFuel,
                        "set " + GetCardLabel(fuelId) + " -> Linkage fuel", after,
                        fuelId);
                }

                if (state.CanUseSkyStrikerSpell && state.HandLinkage && state.FieldFuel &&
                    state.ExtraKind == SkyStrategicExtraKind.None && state.ExtraZero &&
                    !state.ZeroUsed)
                {
                    SkyStrategicState after = Next(state);
                    after.HandLinkage = false;
                    after.FieldFuel = false;
                    after.GraveSpells += 2;
                    after.ExtraKind = SkyStrategicExtraKind.Zero;
                    after.ExtraZero = false;
                    after.ZeroUsed = true;
                    after.PendingZeroSearch = true;
                    yield return Move(SkyStrategicActionKind.ActivateLinkage,
                        "Linkage fuel -> Zero", after);
                }

                if (state.NormalAvailable && state.MainCount == 0 &&
                    state.ExtraKind == SkyStrategicExtraKind.None && state.HandRaye)
                {
                    SkyStrategicState after = Next(state);
                    after.HandRaye = false;
                    after.MainRaye = 1;
                    after.NormalAvailable = false;
                    yield return Move(SkyStrategicActionKind.NormalRaye,
                        "Normal Raye", after);
                }

                if (state.NormalAvailable && state.MainCount == 0 &&
                    state.ExtraKind == SkyStrategicExtraKind.None && state.HandRoze)
                {
                    SkyStrategicState after = Next(state);
                    after.HandRoze = false;
                    after.MainRoze = 1;
                    after.NormalAvailable = false;
                    yield return Move(SkyStrategicActionKind.NormalRoze,
                        "Normal Roze", after);
                }

                if (state.Main1 && state.CanUseSkyStrikerSpell && state.HandHornet &&
                    (state.HornetSetup || state.Turn1 || state.EnemyMonsters == 0) &&
                    (state.ExtraKind == SkyStrategicExtraKind.None ||
                    state.ExtraKind == SkyStrategicExtraKind.Kagari ||
                    state.ExtraKind == SkyStrategicExtraKind.Bridge))
                {
                    SkyStrategicState after = Next(state);
                    after.HandHornet = false;
                    ConsumeHandFuel(after, CardId.HornetDrones);
                    after.GraveHornet = true;
                    after.GraveSpells++;
                    after.MainToken++;
                    if (state.ExtraKind == SkyStrategicExtraKind.None)
                        after.HornetSetup = true;
                    yield return Move(SkyStrategicActionKind.ActivateHornet,
                        state.ExtraKind == SkyStrategicExtraKind.None
                            ? "Hornet token starter" : "Hornet second token", after);
                }

                if (state.Main1 && state.MainCount == 1 && state.MainRaye == 1 &&
                    (state.Turn1 || state.EnemyMonsters == 0) &&
                    state.ExtraKind == SkyStrategicExtraKind.None && state.ExtraZero &&
                    !state.ZeroUsed)
                {
                    SkyStrategicState after = Next(state);
                    after.MainRaye = 0;
                    after.GraveAceMonsters++;
                    after.GraveRaye = true;
                    after.ExtraKind = SkyStrategicExtraKind.Zero;
                    after.ExtraZero = false;
                    after.ZeroUsed = true;
                    after.PendingZeroSearch = true;
                    yield return Move(SkyStrategicActionKind.RayeTagZero,
                        "Raye tag -> Zero", after);
                }

                // Reserve Kagari and Zero before spending either summon. The
                // terminal battle projection is only useful if the search can
                // also reach that position from a normal summon/token starter.
                if (state.Main1 && !state.Turn1 && state.EnemyField == 0 &&
                    state.MainCount == 1 && state.MainOther == 0 &&
                    state.ExtraKind == SkyStrategicExtraKind.None && state.ExtraHayate &&
                    state.ExtraKagari && state.ExtraZero && !state.KagariUsed && !state.ZeroUsed)
                {
                    SkyStrategicState after = Next(state);
                    BuryMainMaterials(after);
                    after.MainRaye = 0;
                    after.MainRoze = 0;
                    after.MainToken = 0;
                    after.HornetSetup = false;
                    after.ExtraKind = SkyStrategicExtraKind.Bridge;
                    after.BattleStarterAttack = 1500;
                    after.ExtraHayate = false;
                    yield return Move(SkyStrategicActionKind.LinkBattleStarter,
                        "Link Hayate; reserve Kagari and Zero for battle", after);
                }

                if (state.MainCount == 1 && state.MainOther == 0 &&
                    state.ExtraKind == SkyStrategicExtraKind.None && state.ExtraKagari &&
                    !state.KagariUsed)
                {
                    SkyStrategicState after = Next(state);
                    BuryMainMaterials(after);
                    after.MainRaye = 0;
                    after.MainRoze = 0;
                    after.MainToken = 0;
                    after.ExtraKind = SkyStrategicExtraKind.Kagari;
                    after.BattleStarterAttack = 0;
                    after.ExtraKagari = false;
                    after.KagariUsed = true;
                    if (after.HornetSetup && after.GraveHornet)
                    {
                        after.GraveHornet = false;
                        after.HandHornet = true;
                        after.GraveSpells = Math.Max(0, after.GraveSpells - 1);
                    }
                    else if (after.GraveEngage)
                    {
                        RemoveGraveEngage(after);
                        after.HandEngage = true;
                        after.GraveSpells = Math.Max(0, after.GraveSpells - 1);
                    }
                    yield return Move(SkyStrategicActionKind.LinkKagari,
                        "Link Kagari and recover engine", after);
                }

                if (state.ExtraKind == SkyStrategicExtraKind.Kagari &&
                    state.MainCount == 1 && state.MainToken == 1 && state.ExtraBridge)
                {
                    SkyStrategicState after = Next(state);
                    after.ExtraKind = SkyStrategicExtraKind.Bridge;
                    after.BattleStarterAttack = 0;
                    after.ExtraBridge = false;
                    after.GraveAceMonsters++;
                    yield return Move(SkyStrategicActionKind.LinkBridge,
                        "Kagari -> bridge Link", after);
                }

                bool bridgeZero = state.ExtraKind == SkyStrategicExtraKind.Bridge &&
                    state.MainCount == 1 && state.MainToken == 1;
                bool pairZero = state.ExtraKind == SkyStrategicExtraKind.None &&
                    state.MainCount == 2 && state.MainRaye >= 1 && state.MainRoze >= 1;
                if ((bridgeZero || pairZero) && state.ExtraZero && !state.ZeroUsed)
                {
                    SkyStrategicState after = Next(state);
                    BuryMainMaterials(after);
                    after.MainRaye = 0;
                    after.MainRoze = 0;
                    after.MainToken = 0;
                    after.MainOther = 0;
                    if (bridgeZero)
                        after.GraveAceMonsters++;
                    after.ExtraKind = SkyStrategicExtraKind.Zero;
                    after.BattleStarterAttack = 0;
                    after.ExtraZero = false;
                    after.ZeroUsed = true;
                    after.PendingZeroSearch = true;
                    yield return Move(SkyStrategicActionKind.LinkZero,
                        "clear both Main Zones -> Zero", after);
                }

                if ((state.Turn1 || !state.Main1) &&
                    state.MainCount == 1 && state.MainOther == 0 &&
                    state.ExtraKind == SkyStrategicExtraKind.None && state.ExtraShizuku)
                {
                    SkyStrategicState after = Next(state);
                    BuryMainMaterials(after);
                    after.MainRaye = 0;
                    after.MainRoze = 0;
                    after.MainToken = 0;
                    after.ExtraKind = SkyStrategicExtraKind.Shizuku;
                    // Summoned in attack position by the existing Link handler.
                    after.BattleStarterAttack = 1500;
                    after.ExtraShizuku = false;
                    yield return Move(SkyStrategicActionKind.LinkShizuku,
                        "Link Shizuku end board", after);
                }

                if (state.PotPermitted && state.CanDraw && state.CanUseSkyStrikerSpell &&
                    state.HandPot && state.DeckCount > 15)
                {
                    SkyStrategicState after = Next(state);
                    after.HandPot = false;
                    ConsumeHandFuel(after, CardId.PotOfDesires);
                    after.GraveSpells++;
                    after.DeckCount -= 10;
                    after.Advantage++;
                    after.PotRisk += GetPotRisk(state);
                    yield return Move(SkyStrategicActionKind.ActivatePot,
                        "Pot of Desires recovery", after);
                }
            }

            private static IEnumerable<SkyStrategicTransition> ExpandZeroSearch(
                SkyStrategicState state)
            {
                var targets = new List<int>();
                if (state.CanSearch && state.DeckEngage || state.GraveEngage)
                    targets.Add(CardId.Engage);
                else if (state.CanSearch)
                {
                    if (state.DeckLinkage && !state.HandLinkage)
                        targets.Add(CardId.Linkage);
                    if (state.DeckWidow && !state.HandWidow)
                        targets.Add(CardId.WidowAnchor);
                    if (state.DeckLemnisgate && !state.HandLemnisgate &&
                        state.GraveSpells > 0 && state.GraveAceMonsters > 0 &&
                        state.LemnisgateLive)
                        targets.Add(CardId.Lemnisgate);
                }
                foreach (int target in targets.Distinct())
                {
                    SkyStrategicState after = Next(state);
                    after.PendingZeroSearch = false;
                    AddSearchTarget(after, target, true);
                    yield return Move(SkyStrategicActionKind.ResolveZeroSearch,
                        "Zero search -> " + GetCardLabel(target), after, target);
                }
                if (targets.Count == 0)
                {
                    SkyStrategicState after = Next(state);
                    after.PendingZeroSearch = false;
                    yield return Move(SkyStrategicActionKind.ResolveZeroSearch,
                        "Zero: no modeled search target", after);
                }
            }

            private static IEnumerable<int> GetEngageTargets(SkyStrategicState state)
            {
                bool starter = state.HandRaye || state.HandRoze || state.HandHornet ||
                    state.MainCount > 0 || state.ExtraKind != SkyStrategicExtraKind.None;
                if (!starter && state.DeckRaye)
                    yield return CardId.Raye;
                if (!state.HandLinkage && state.DeckLinkage && !state.Turn1 &&
                    state.EnemyField == 0)
                    yield return CardId.Linkage;
                if (starter && !state.HandWidow && state.DeckWidow)
                    yield return CardId.WidowAnchor;
                if (state.DeckLemnisgate && !state.HandLemnisgate &&
                    state.GraveSpells > 0 && state.GraveAceMonsters > 0 &&
                    state.LemnisgateLive)
                    yield return CardId.Lemnisgate;
                if (!state.HandMultirole && state.DeckMultirole)
                    yield return CardId.Multirole;
                if (!starter && !state.HandHornet && state.DeckHornet)
                    yield return CardId.HornetDrones;
                if (!state.HandLinkage && state.DeckLinkage)
                    yield return CardId.Linkage;
                if (!state.HandWidow && state.DeckWidow)
                    yield return CardId.WidowAnchor;
                if (!starter && state.DeckRoze)
                    yield return CardId.Roze;
            }

            private static void AddSearchTarget(SkyStrategicState state, int target,
                bool allowGrave)
            {
                if (target == CardId.Raye)
                {
                    state.HandRaye = true;
                    state.DeckRaye = false;
                }
                else if (target == CardId.Roze)
                {
                    state.HandRoze = true;
                    state.DeckRoze = false;
                }
                else if (target == CardId.Engage)
                {
                    state.HandEngage = true;
                    if (allowGrave && state.GraveEngage &&
                        (!state.CanSearch || !state.DeckEngage || state.GraveSpells -
                        Math.Max(1, state.GraveEngageCopies) >= 3))
                    {
                        RemoveGraveEngage(state);
                        state.GraveSpells = Math.Max(0, state.GraveSpells - 1);
                    }
                    else
                        state.DeckEngage = false;
                }
                else if (target == CardId.HornetDrones)
                {
                    state.HandHornet = true;
                    state.DeckHornet = false;
                }
                else if (target == CardId.Linkage)
                {
                    state.HandLinkage = true;
                    state.DeckLinkage = false;
                }
                else if (target == CardId.WidowAnchor)
                {
                    state.HandWidow = true;
                    state.DeckWidow = false;
                }
                else if (target == CardId.Lemnisgate)
                {
                    state.HandLemnisgate = true;
                    state.DeckLemnisgate = false;
                }
                else if (target == CardId.Multirole)
                {
                    state.HandMultirole = true;
                    state.DeckMultirole = false;
                }
            }

            private static SkyStrategicState Next(SkyStrategicState state)
            {
                SkyStrategicState next = state.Clone();
                next.Step++;
                return next;
            }

            private static void ConsumeHandFuel(SkyStrategicState state, int id)
            {
                // The fuel candidate is a real card, not a second copy of the
                // same hand resource after its modeled activation.
                if (state.HandLinkageFuelId != id)
                    return;
                state.HandLinkageFuelId = 0;
                state.HandLinkageFuelCost = 0;
            }

            private static void RemoveGraveEngage(SkyStrategicState state)
            {
                state.GraveEngageCopies = Math.Max(0, state.GraveEngageCopies - 1);
                state.GraveEngage = state.GraveEngageCopies > 0;
            }

            private static void BuryMainMaterials(SkyStrategicState state)
            {
                state.GraveRaye |= state.MainRaye > 0;
                state.GraveRoze |= state.MainRoze > 0;
                // A token disappears; it cannot fund Lemnisgate or a revival.
                state.GraveAceMonsters += state.MainRaye + state.MainRoze;
            }

            private static double EvaluateTerminal(SkyStrategicState state)
            {
                if (state == null || state.PendingZeroSearch ||
                    !state.EmptyMainZone && HasModeledSkyStrikerSpell(state))
                    return double.MinValue;
                return Evaluate(state) + GetFollowUpValue(state);
            }

            public static int GetFollowUpValue(SkyStrategicState state)
            {
                // Discounted next-turn access, not a simulated enemy turn. Do
                // not invent draws, searches or survival of our current board.
                int access = 0;
                bool linkAvailable = state.ExtraZero || state.ExtraKagari ||
                    state.ExtraBridge || state.ExtraShizuku;
                if (linkAvailable)
                {
                    if (state.HandRaye || state.HandRoze)
                        access = 650;
                    if (state.HandHornet)
                        access = Math.Max(access, 700);
                    if (state.HandReinforcement && state.DeckRaye)
                        access = Math.Max(access, 650);
                    if (state.HandEngage && (state.DeckRaye || state.DeckHornet))
                        access = Math.Max(access, 900);
                }
                if (state.GraveEngage && state.ExtraKagari && access > 0)
                    access += 400;
                return access;
            }

            public static int GetPreparedBattleDamage(SkyStrategicState state)
            {
                if (state.Turn1 || !state.Main1 || !state.EmptyMainZone ||
                    state.EnemyField != 0 || !state.HandLinkage ||
                    state.BattleStarterAttack < 1500 ||
                    state.ExtraKind == SkyStrategicExtraKind.None ||
                    state.ExtraKind == SkyStrategicExtraKind.Zero ||
                    state.ExtraKind == SkyStrategicExtraKind.Kagari ||
                    !state.ExtraKagari || !state.ExtraZero || state.KagariUsed ||
                    state.ZeroUsed || !(state.DeckRaye || state.GraveRaye) ||
                    !(state.DeckRoze || state.GraveRoze))
                    return 0;
                return state.BattleStarterAttack + 6500;
            }

            private static SkyStrategicTransition Move(SkyStrategicActionKind kind,
                string text, SkyStrategicState after, int searchTarget = 0)
            {
                return new SkyStrategicTransition
                {
                    Action = new SkyStrategicAction
                    {
                        Kind = kind,
                        SearchTarget = searchTarget,
                        Text = text
                    },
                    After = after
                };
            }

            private static int GetPotRisk(SkyStrategicState state)
            {
                int risk = 1;
                bool starter = state.HandRaye || state.HandRoze || state.HandHornet ||
                    state.MainCount > 0 || state.ExtraKind != SkyStrategicExtraKind.None;
                if (!starter)
                    risk += 5;
                if (!state.HandLinkage)
                    risk += 3;
                if (!state.HandRoze && state.DeckRoze)
                    risk += 2;
                if (!state.HandWidow && state.DeckWidow)
                    risk++;
                return risk;
            }

            private static double Evaluate(SkyStrategicState state)
            {
                if (state == null)
                    return double.MinValue;
                double score = 0;
                if (state.ExtraKind == SkyStrategicExtraKind.Zero)
                    score += 6400;
                else if (state.ExtraKind == SkyStrategicExtraKind.Shizuku)
                    score += 3300;
                else if (state.ExtraKind == SkyStrategicExtraKind.Kagari)
                    score += 1900;
                else if (state.ExtraKind == SkyStrategicExtraKind.Bridge)
                    score += 1500;
                else if (state.ExtraKind == SkyStrategicExtraKind.Other)
                    score += 900;

                if (state.EmptyMainZone)
                    score += 700;
                else if (HasModeledSkyStrikerSpell(state) &&
                    !(state.ExtraKind == SkyStrategicExtraKind.Bridge &&
                    state.MainToken == 1))
                    score -= 3600 + 450 * state.MainCount;

                score += Math.Min(3, state.GraveSpells) * 380;
                if (state.GraveSpells >= 3)
                    score += 1050;
                if (state.HandEngage)
                    score += 1650;
                if (state.HandWidow)
                    score += 1550;
                if (state.HandLinkage)
                    score += state.Turn1 ? 850 : 1250;
                if (state.HandLemnisgate && state.GraveSpells > 0 &&
                    state.GraveAceMonsters > 0 && state.LemnisgateLive)
                    score += 900;
                if (state.HandMultirole)
                    score += 650;
                if (state.HandRaye)
                    score += 650;
                if (state.HandRoze)
                    score += 350;
                if (state.PendingZeroSearch)
                    score += 700;
                score += state.Advantage * 620;
                score -= state.PotRisk * 720;
                score -= state.ResourceCost;
                score -= state.Step * 65;
                if (GetPreparedBattleDamage(state) >= Math.Max(1, state.EnemyLife))
                    score += 20000;

                bool hasStarter = state.HandRaye || state.HandRoze || state.HandHornet ||
                    state.MainCount > 0 || state.ExtraKind != SkyStrategicExtraKind.None;
                if (!hasStarter)
                    score -= state.Turn1 ? 2800 : 1800;
                if (state.ExtraKind == SkyStrategicExtraKind.Zero &&
                    state.EmptyMainZone && (state.HandWidow || state.HandLinkage ||
                    state.HandEngage))
                    score += 1200;
                if (!state.Turn1 && state.EnemyField == 0 && state.HandLinkage &&
                    state.ExtraKind != SkyStrategicExtraKind.None &&
                    state.ExtraKind != SkyStrategicExtraKind.Zero)
                    score += 1800;
                return score;
            }

            private static bool HasModeledSkyStrikerSpell(SkyStrategicState state)
            {
                return state.HandEngage || state.HandHornet || state.HandLinkage ||
                    state.HandWidow || state.HandLemnisgate || state.HandMultirole;
            }

            private static string GetCardLabel(int id)
            {
                if (id == CardId.Raye) return "Raye";
                if (id == CardId.Roze) return "Roze";
                if (id == CardId.Engage) return "Engage";
                if (id == CardId.HornetDrones) return "Hornet Drones";
                if (id == CardId.Linkage) return "Linkage";
                if (id == CardId.WidowAnchor) return "Widow Anchor";
                if (id == CardId.Lemnisgate) return "Lemnisgate";
                if (id == CardId.Multirole) return "Multirole";
                return id.ToString();
            }
        }

        private SkyStrategicState StrategicPlanState;
        private SkyStrategicPlan StrategicPlan;
        private CancellationTokenSource StrategicPlanCancellation;
        private int StrategicSearchTargetPending;
        private string StrategicObservationKey;

        private SkyStrategicAction GetStrategicRecommendation()
        {
            if (Duel.Player != 0 || Duel.CurrentChain.Count > 0 ||
                (Duel.Phase != DuelPhase.Main1 && Duel.Phase != DuelPhase.Main2))
                return null;

            SkyStrategicState observed = CaptureSkyStrategicState();
            string observationKey = GetDecisionObservationKey();
            if (StrategicPlan == null || StrategicPlanState == null ||
                !StrategicPlanState.Equals(observed) || StrategicObservationKey != observationKey)
            {
                RetireSkyStrategicPlan();
                StrategicPlanCancellation = new CancellationTokenSource();
                StrategicPlan = SkyStrategicPlanner.Plan(observed,
                    StrategicPlanCancellation.Token);
                StrategicPlanState = observed;
                StrategicObservationKey = observationKey;
                if (StrategicPlan.Steps.Count > 0)
                {
                    SkyStrategicState end = StrategicPlan.Steps.Last().After;
                    Logger.DebugWriteLine("[SkyStriker2026:RDA] score=" +
                        StrategicPlan.Score.ToString("0") + " expanded=" +
                        StrategicPlan.Expanded.ToString() + " timeout=" +
                        StrategicPlan.TimedOut.ToString() + " end=" + end.ExtraKind +
                        " main=" + end.MainCount + " followUp=" +
                        SkyStrategicPlanner.GetFollowUpValue(end) + " battleDamage=" +
                        SkyStrategicPlanner.GetPreparedBattleDamage(end) + " route=" +
                        string.Join(" > ", StrategicPlan.Steps.Select(step =>
                            step.Action.Text).ToArray()));
                }
                else
                {
                    Logger.DebugWriteLine("[SkyStriker2026:plan] outcome=" + StrategicPlan.Outcome +
                        " reason=" + StrategicPlan.Reason + " consideredMask=" +
                        StrategicPlan.ConsideredRootActions + " expanded=" + StrategicPlan.Expanded);
                }
            }
            return StrategicPlan == null || StrategicPlan.Steps.Count == 0
                ? null : StrategicPlan.Steps[0].Action;
        }

        private bool IsStrategicallyRecommended(SkyStrategicActionKind kind)
        {
            if (Duel.Player != 0 || Duel.CurrentChain.Count > 0 ||
                (Duel.Phase != DuelPhase.Main1 && Duel.Phase != DuelPhase.Main2))
                return false;
            SkyStrategicAction action = GetStrategicRecommendation();
            return action != null && action.Kind == kind;
        }

        private bool StrategicAllows(SkyStrategicActionKind kind)
        {
            if (Duel.Player != 0 || Duel.CurrentChain.Count > 0 ||
                (Duel.Phase != DuelPhase.Main1 && Duel.Phase != DuelPhase.Main2))
                return true;
            SkyStrategicAction action = GetStrategicRecommendation();
            if (action != null)
                return action.Kind == kind;
            // A bit in ConsideredRootActions means generated, NOT evaluated to
            // completion and NOT proven inferior to PASS. Handlers retain their
            // cost/legality guards. The one-step fallback must still have an exit.
            bool allow = true;
            string reason = "evaluate offered action with local cost/target guards";
            if (kind == SkyStrategicActionKind.ActivateHornet)
            {
                allow = Bot.HasInExtra(new[] { CardId.Kagari, CardId.Shizuku,
                    CardId.Hayate, CardId.Kaina, CardId.Amatsu, CardId.Linkuriboh });
                reason = allow ? "token has an Extra Deck conversion" : "PASS: token would block MZ with no exit";
            }
            if ((kind == SkyStrategicActionKind.NormalRaye || kind == SkyStrategicActionKind.NormalRoze) &&
                HasPlannedSkyStrikerSpell())
            {
                bool exit = (!KagariSummoned && Bot.HasInExtra(CardId.Kagari)) ||
                    (!ShizukuSummoned && Bot.HasInExtra(CardId.Shizuku)) ||
                    (!HayateSummoned && Bot.HasInExtra(CardId.Hayate)) ||
                    (!KainaSummoned && Bot.HasInExtra(CardId.Kaina)) ||
                    kind == SkyStrategicActionKind.NormalRaye && !ZeroSummoned && Bot.HasInExtra(CardId.Zero);
                allow = exit;
                reason = exit ? "normal summon has a modeled MZ exit" : "PASS: preserve spell access; no MZ exit";
            }
            Logger.DebugWriteLine("[SkyStriker2026:decision] fallback=" + kind +
                " accept=" + allow + " reason=" + reason);
            return allow;
        }

        private bool StrategicReservesModeledLink()
        {
            SkyStrategicAction action = GetStrategicRecommendation();
            return action != null && (action.Kind == SkyStrategicActionKind.LinkKagari ||
                action.Kind == SkyStrategicActionKind.LinkBridge ||
                action.Kind == SkyStrategicActionKind.LinkZero ||
                action.Kind == SkyStrategicActionKind.LinkBattleStarter ||
                action.Kind == SkyStrategicActionKind.LinkShizuku);
        }

        private int GetStrategicSearchTarget(SkyStrategicActionKind kind)
        {
            SkyStrategicAction action = GetStrategicRecommendation();
            return action != null && action.Kind == kind ? action.SearchTarget : 0;
        }

        private void RetireSkyStrategicPlan()
        {
            if (StrategicPlanCancellation != null)
            {
                StrategicPlanCancellation.Cancel();
                StrategicPlanCancellation.Dispose();
                StrategicPlanCancellation = null;
            }
            StrategicPlanState = null;
            StrategicPlan = null;
            StrategicObservationKey = null;
        }

        private SkyStrategicState CaptureSkyStrategicState()
        {
            List<ClientCard> main = Bot.GetMonstersInMainZone();
            List<ClientCard> extra = Bot.GetMonsters().Where(card => card.Sequence >= 5)
                .ToList();
            SkyStrategicExtraKind extraKind = SkyStrategicExtraKind.None;
            if (extra.Any(card => card.IsCode(CardId.Zero)))
                extraKind = SkyStrategicExtraKind.Zero;
            else if (extra.Any(card => card.IsCode(CardId.Kagari)))
                extraKind = SkyStrategicExtraKind.Kagari;
            else if (extra.Any(card => card.IsCode(CardId.Shizuku)))
                extraKind = SkyStrategicExtraKind.Shizuku;
            else if (extra.Any(IsSkyStrikerAceLink))
                extraKind = SkyStrategicExtraKind.Bridge;
            else if (extra.Count > 0)
                extraKind = SkyStrategicExtraKind.Other;

            return new SkyStrategicState
            {
                MainPhase = Duel.Phase == DuelPhase.Main1 || Duel.Phase == DuelPhase.Main2,
                RootActionMask = CaptureStrategicRootOffers(),
                Main1 = Duel.Phase == DuelPhase.Main1,
                Turn1 = Duel.Turn == 1,
                NormalAvailable = !NormalSummoned,
                CanSearch = DefaultCheckWhetherBotCanSearch(),
                CanDraw = DefaultCheckWhetherBotCanDraw(),
                PotPermitted = IsPotRecoveryWorthRisk(),
                MainRaye = main.Count(card => card.IsCode(CardId.Raye)),
                MainRoze = main.Count(card => card.IsCode(CardId.Roze)),
                MainToken = main.Count(card => card.IsCode(CardId.Token)),
                MainOther = main.Count(card => !card.IsCode(CardId.Raye, CardId.Roze,
                    CardId.Token)),
                ExtraKind = extraKind,
                FieldFuel = Bot.GetSpells().Any(card => !card.IsCode(CardId.Linkage)),
                HandRaye = Bot.HasInHand(CardId.Raye),
                HandRoze = Bot.HasInHand(CardId.Roze),
                HandEngage = Bot.HasInHand(CardId.Engage),
                HandReinforcement = Bot.HasInHand(CardId.ReinforcementOfTheArmy),
                HandHornet = Bot.HasInHand(CardId.HornetDrones),
                HandLinkage = Bot.HasInHand(CardId.Linkage),
                HandLinkageFuelId = GetBestLinkageFuelFromHand(),
                HandLinkageFuelCost = GetBestLinkageFuelCostFromHand(),
                HandUltimate = Bot.HasInHand(CardId.UltimateSky),
                HandWidow = Bot.HasInHand(CardId.WidowAnchor),
                HandLemnisgate = Bot.HasInHand(CardId.Lemnisgate),
                HandMultirole = Bot.HasInHand(CardId.Multirole),
                HandPot = Bot.HasInHand(CardId.PotOfDesires),
                LemnisgateLive = HasProfitableLemnisgatePlan(),
                DeckRaye = Bot.HasInDeck(CardId.Raye),
                DeckRoze = Bot.HasInDeck(CardId.Roze),
                DeckEngage = Bot.HasInDeck(CardId.Engage),
                DeckHornet = Bot.HasInDeck(CardId.HornetDrones),
                DeckLinkage = Bot.HasInDeck(CardId.Linkage),
                DeckWidow = Bot.HasInDeck(CardId.WidowAnchor),
                DeckLemnisgate = Bot.HasInDeck(CardId.Lemnisgate),
                DeckMultirole = Bot.HasInDeck(CardId.Multirole),
                GraveEngage = Bot.HasInGraveyard(CardId.Engage),
                GraveEngageCopies = Bot.Graveyard.Count(card => card.IsCode(CardId.Engage)),
                GraveHornet = Bot.HasInGraveyard(CardId.HornetDrones),
                GraveSpells = Bot.GetGraveyardSpells().Count,
                GraveAceMonsters = Bot.GetGraveyardMonsters().Count(IsSkyStrikerAce),
                GraveRaye = Bot.HasInGraveyard(CardId.Raye),
                GraveRoze = Bot.HasInGraveyard(CardId.Roze),
                BattleStarterAttack = extra.Where(card => card.IsFaceup() &&
                    IsSkyStrikerAceLink(card) && !card.Attacked && !card.IsDisabled() &&
                    !card.IsCode(CardId.Kagari, CardId.Zero))
                    .Select(card => Math.Max(0, card.GetAttackPower())).DefaultIfEmpty(0).Max(),
                ExtraZero = Bot.HasInExtra(CardId.Zero),
                ExtraKagari = Bot.HasInExtra(CardId.Kagari),
                ExtraHayate = !HayateSummoned && Bot.HasInExtra(CardId.Hayate),
                ExtraBridge = !HayateSummoned && Bot.HasInExtra(CardId.Hayate) ||
                    !KainaSummoned && Bot.HasInExtra(CardId.Kaina) ||
                    !ShizukuSummoned && Bot.HasInExtra(CardId.Shizuku) ||
                    !AmatsuSummoned && Bot.HasInExtra(CardId.Amatsu),
                ExtraShizuku = !ShizukuSummoned && Bot.HasInExtra(CardId.Shizuku),
                ZeroUsed = ZeroSummoned,
                KagariUsed = KagariSummoned,
                PendingZeroSearch = ZeroSearchPending,
                HornetSetup = HornetSetupStage != HornetZeroSetupStage.None,
                EnemyMonsters = Enemy.GetMonsterCount(),
                EnemyField = Enemy.GetFieldCount(),
                EnemyLife = Enemy.LifePoints,
                DeckCount = Bot.Deck.Count
            };
        }

        private int CaptureStrategicRootOffers()
        {
            return BuildStrategicRootOffers(Duel.MainPhase, GetBestLinkageFuelFromHand());
        }

        private static int BuildStrategicRootOffers(MainPhase main, int fuel)
        {
            if (main == null)
                return 0;
            int mask = 0;
            foreach (ClientCard card in main.ActivableCards)
            {
                SkyStrategicActionKind kind = SkyStrategicActionKind.None;
                if (card.IsCode(CardId.Engage)) kind = SkyStrategicActionKind.ActivateEngage;
                else if (card.IsCode(CardId.ReinforcementOfTheArmy)) kind = SkyStrategicActionKind.ActivateReinforcement;
                else if (card.IsCode(CardId.HornetDrones)) kind = SkyStrategicActionKind.ActivateHornet;
                else if (card.IsCode(CardId.PotOfDesires)) kind = SkyStrategicActionKind.ActivatePot;
                else if (card.IsCode(CardId.Linkage)) kind = SkyStrategicActionKind.ActivateLinkage;
                else if (card.IsCode(CardId.UltimateSky)) kind = SkyStrategicActionKind.ActivateUltimateLinkage;
                else if (card.IsCode(CardId.Raye) && card.Location == CardLocation.MonsterZone)
                    kind = SkyStrategicActionKind.RayeTagZero;
                if (kind != SkyStrategicActionKind.None)
                    mask |= 1 << (int)kind;
            }
            foreach (ClientCard card in main.SummonableCards)
            {
                if (card.IsCode(CardId.Raye)) mask |= 1 << (int)SkyStrategicActionKind.NormalRaye;
                if (card.IsCode(CardId.Roze)) mask |= 1 << (int)SkyStrategicActionKind.NormalRoze;
            }
            foreach (ClientCard card in main.SpecialSummonableCards)
            {
                if (card.Location != CardLocation.Extra)
                    continue;
                if (card.IsCode(CardId.Zero)) mask |= 1 << (int)SkyStrategicActionKind.LinkZero;
                if (card.IsCode(CardId.Kagari)) mask |= 1 << (int)SkyStrategicActionKind.LinkKagari;
                if (card.IsCode(CardId.Shizuku)) mask |= 1 << (int)SkyStrategicActionKind.LinkShizuku;
                if (card.IsCode(CardId.Hayate)) mask |= 1 << (int)SkyStrategicActionKind.LinkBattleStarter;
                if (card.IsCode(CardId.Hayate, CardId.Kaina, CardId.Amatsu, CardId.Shizuku))
                    mask |= 1 << (int)SkyStrategicActionKind.LinkBridge;
            }
            if (fuel != 0 && main.SpellSetableCards.Any(card => card.IsCode(fuel)))
                mask |= 1 << (int)SkyStrategicActionKind.SetLinkageFuel;
            return mask;
        }
    }
}
