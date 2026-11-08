using System;
using System.Linq;

namespace WindBot.Game.AI.Decks
{
    public partial class Witchcraft2026Executor
    {
        // Small sensitivity model over observed opposing interactions, never hidden
        // hand traps. Costs below have existing script-backed action coverage.
        internal static WState DeniedAction(WState before, WAction action)
        {
            WState failed = before.Copy();
            failed.DenialFloor = null; failed.CommitmentCost = 0;
            failed.Pending = 0; failed.ViceWindow = false;
            failed.Used.Add(action.Usage);
            if (action.Location == WZone.Hand && IsModelSpell(action.Source) &&
                action.Kind != WKind.SetTrap && action.Kind != WKind.ActivateField)
                failed.Move(action.Source, WZone.Hand, WZone.Grave);
            if (action.Kind == WKind.Recruit)
            {
                failed.Move(action.Source, WZone.Board, WZone.Grave);
                failed.Move(action.Cost, action.CostZone, WZone.Grave);
            }
            else if (action.Kind == WKind.EdelRevive)
                failed.Move(action.Source, WZone.Board, WZone.Grave);
            else if (action.Kind == WKind.Send)
                failed.Move(action.Source, WZone.Grave, WZone.Banished);
            else if (action.Kind == WKind.GenniCopy || action.Kind == WKind.GenniLacrima)
            {
                failed.Move(CardId.Genni, WZone.Grave, WZone.Banished);
                failed.Move(action.Kind == WKind.GenniCopy ? CardId.Creation : CardId.Lacrima, WZone.Grave, WZone.Banished);
            }
            else if (action.Kind == WKind.Marka)
                failed.Move(action.Source, WZone.Hand, WZone.Grave);
            else if (action.Kind == WKind.NegateBoard)
                failed.Move(action.Cost, WZone.Hand, WZone.Grave);
            return failed;
        }

        internal static double IndependentAccess(WState state)
        {
            // Value an actual spare access card only with an unused effect and a
            // remaining own-deck target. No card is invented after a failed search.
            double access = 0;
            if (state.Has(CardId.SpellPowerMastery, WZone.Hand) && !state.Used.Contains(CardId.SpellPowerMastery + ":" + WKind.Mastery) &&
                (state.Has(CardId.MaidGenni, WZone.Deck) || state.Has(CardId.MaidVerre, WZone.Deck))) access += 6;
            if (state.Has(CardId.Creation, WZone.Hand) && !state.Used.Contains(CardId.Creation + ":" + WKind.Creation) &&
                WitchMonsters.Any(id => state.Has(id, WZone.Deck))) access += 4;
            return access;
        }

        private static void AssessRouteExposure(WState before, WAction action, WState after)
        {
            if (action.Kind == WKind.Normal || action.Kind == WKind.DarkSummon || action.Kind == WKind.SetTrap) return;
            bool monster = !IsModelSpell(action.Source);
            OpponentResponse kind = monster ? (action.Location == WZone.Board ? OpponentResponse.FieldMonster :
                OpponentResponse.HandGraveMonster) : OpponentResponse.Spell;
            bool exposed = (before.OpposingResponses & kind) != 0;
            bool paidRecruit = action.Kind == WKind.Recruit && action.CostZone == WZone.Hand &&
                !before.Has(CardId.Walpurgis, WZone.Backrow);
            if (!exposed && !paidRecruit) return;
            WState failed = DeniedAction(before, action);
            double floor = WPlanner.UninterruptedValue(failed) + IndependentAccess(failed);
            if (exposed)
            {
                // Jacks is removal, not negation. Model the vulnerable board after
                // successful resolution separately from C104's failed field effect.
                if (kind == OpponentResponse.HandGraveMonster)
                {
                    int[] bodies = after.In(WZone.Board).Where(id => before.Has(id, WZone.Board) &&
                        (id != CardId.RedEyesDarkDragoon || before.Disabled.Contains(id))).ToArray();
                    if (bodies.Length == 0) return;
                    floor = double.PositiveInfinity;
                    // At most seven existing bodies: compare actual resource loss,
                    // rather than assigning an opponent a fixed target priority.
                    foreach (int body in bodies)
                    {
                        failed = after.Copy(); failed.DenialFloor = null;
                        failed.Move(body, WZone.Board, WZone.Grave);
                        floor = Math.Min(floor, WPlanner.UninterruptedValue(failed) + IndependentAccess(failed));
                    }
                }
                after.DenialFloor = before.DenialFloor.HasValue ? Math.Min(before.DenialFloor.Value, floor) : floor;
                if (!before.DenialFloor.HasValue || floor < before.DenialFloor.Value)
                    after.Vulnerability = action.Kind + "/" + kind + "/single-response-sensitivity";
            }
            if (paidRecruit)
            {
                // This is paid-resource insurance, not an assertion that the hidden
                // opponent has Impulse/Ash. Prefer preserving independent access.
                after.CommitmentCost += Math.Max(0, IndependentAccess(before) - IndependentAccess(failed));
            }
        }
    }
}
