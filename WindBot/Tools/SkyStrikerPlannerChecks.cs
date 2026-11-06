using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Web.Script.Serialization;

// Offline model checks only. This executable does not start an ocgcore duel and
// cannot establish engine legality or competitive strength.
internal static class SkyStrikerPlannerChecks
{
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic |
        BindingFlags.Static | BindingFlags.Instance;
    private static Type ExecutorType;
    private static Type StateType;
    private static Type PlannerType;
    private static readonly List<Dictionary<string, object>> Results =
        new List<Dictionary<string, object>>();

    private static void Set(object target, string name, object value)
    {
        StateType.GetField(name, Flags).SetValue(target, value);
    }

    private static object Get(object target, string name)
    {
        FieldInfo field = target.GetType().GetField(name, Flags);
        return field == null
            ? target.GetType().GetProperty(name, Flags).GetValue(target, null)
            : field.GetValue(target);
    }

    private static object BaseState()
    {
        object state = Activator.CreateInstance(StateType, true);
        Set(state, "MainPhase", true);
        Set(state, "Main1", true);
        Set(state, "Turn1", true);
        Set(state, "NormalAvailable", true);
        Set(state, "DeckRaye", true);
        Set(state, "DeckRoze", true);
        Set(state, "DeckEngage", true);
        Set(state, "DeckHornet", true);
        Set(state, "DeckLinkage", true);
        Set(state, "DeckWidow", true);
        Set(state, "DeckLemnisgate", true);
        Set(state, "DeckMultirole", true);
        Set(state, "ExtraZero", true);
        Set(state, "ExtraKagari", true);
        Set(state, "ExtraBridge", true);
        Set(state, "ExtraShizuku", true);
        Set(state, "EnemyLife", 8000);
        Set(state, "DeckCount", 35);
        return state;
    }

    private static object Plan(object state)
    {
        return PlannerType.GetMethod("Plan", Flags).Invoke(null,
            new[] { state, (object)CancellationToken.None });
    }

    private static void SetExtra(object state, string kind)
    {
        Set(state, "ExtraKind", Enum.Parse(StateType.GetField("ExtraKind", Flags).FieldType, kind));
    }

    private static int ActionMask(string kind)
    {
        Type actionType = ExecutorType.GetNestedType("SkyStrategicActionKind", Flags);
        return 1 << Convert.ToInt32(Enum.Parse(actionType, kind));
    }

    private static void Record(string name, bool passed)
    {
        Results.Add(new Dictionary<string, object> { { "name", name }, { "passed", passed } });
        Console.WriteLine((passed ? "PASS " : "FAIL ") + name);
    }

    private static object Transition(object state, string kind)
    {
        var moves = (IEnumerable)PlannerType.GetMethod("Expand", Flags).Invoke(null,
            new[] { state, (object)CancellationToken.None });
        object move = moves.Cast<object>().First(item =>
            Get(Get(item, "Action"), "Kind").ToString() == kind);
        return Get(move, "After");
    }

    private static void CheckPlanningHorizon(Assembly assembly)
    {
        object blockedEngage = BaseState();
        Set(blockedEngage, "HandEngage", true);
        Set(blockedEngage, "HandRaye", true);
        Set(blockedEngage, "RootActionMask", ActionMask("NormalRaye"));
        Check("Unavailable Engage replans through the offered normal summon",
            blockedEngage, "NormalRaye", 0, "RayeTagZero", "ResolveZeroSearch");
        Set(blockedEngage, "RootActionMask", 0);
        Check("No offered modeled action cannot reserve a fictional route",
            blockedEngage, "None", 0);

        // Exercise the live offer adapter using real protocol-model classes.
        Type mainType = assembly.GetType("WindBot.Game.MainPhase", true);
        Type cardType = assembly.GetType("WindBot.Game.ClientCard", true);
        object main = Activator.CreateInstance(mainType);
        object raye = System.Runtime.Serialization.FormatterServices.GetUninitializedObject(cardType);
        cardType.GetProperty("Id", Flags).SetValue(raye, 26077387, null);
        ((IList)mainType.GetProperty("SummonableCards").GetValue(main, null)).Add(raye);
        int offered = (int)ExecutorType.GetMethod("BuildStrategicRootOffers", Flags)
            .Invoke(null, new object[] { main, 0 });
        Record("Live offer adapter exposes Raye without inventing Engage or Extra summons",
            offered == ActionMask("NormalRaye"));

        object stranded = BaseState();
        SetExtra(stranded, "Kagari");
        Set(stranded, "HandHornet", true);
        Set(stranded, "HandWidow", true);
        Set(stranded, "ExtraBridge", false);
        Set(stranded, "RootActionMask", ActionMask("ActivateHornet"));
        Check("Do not spend Hornet into an unclearable Main Zone",
            stranded, "None", 0);
        object holdPlan = Plan(stranded);
        Record("Holding retains a rejected-action mask for executor dispatch",
            ((int)Get(holdPlan, "ConsideredRootActions") & ActionMask("ActivateHornet")) != 0);

        object zero = BaseState();
        SetExtra(zero, "Zero");
        Set(zero, "PendingZeroSearch", true);
        Set(zero, "GraveEngage", true);
        Set(zero, "GraveEngageCopies", 1);
        Set(zero, "GraveSpells", 3);
        object searched = Transition(zero, "ResolveZeroSearch");
        Record("Zero takes Deck Engage to retain three grave spells",
            (int)Get(searched, "GraveSpells") == 3 && (bool)Get(searched, "GraveEngage") &&
            !(bool)Get(searched, "DeckEngage"));
        Set(zero, "GraveSpells", 4);
        searched = Transition(zero, "ResolveZeroSearch");
        Record("Zero recovers grave Engage once three other spells remain",
            (int)Get(searched, "GraveSpells") == 3 && !(bool)Get(searched, "GraveEngage") &&
            (bool)Get(searched, "DeckEngage"));
        Set(zero, "GraveEngageCopies", 2);
        searched = Transition(zero, "ResolveZeroSearch");
        Record("Multiple grave Engages do not masquerade as other spells",
            (int)Get(searched, "GraveSpells") == 4 && !(bool)Get(searched, "DeckEngage"));

        object token = BaseState();
        Set(token, "MainToken", 1);
        object linked = Transition(token, "LinkKagari");
        Record("Token Link material cannot supply a grave monster",
            (int)Get(linked, "GraveAceMonsters") == 0);

        object fuel = BaseState();
        Set(fuel, "HandHornet", true);
        Set(fuel, "HandLinkageFuelId", 52340444);
        object activated = Transition(fuel, "ActivateHornet");
        Record("Spent Hornet cannot also be set as the same Linkage fuel",
            (int)Get(activated, "HandLinkageFuelId") == 0);
        Set(fuel, "HandLinkage", true);
        object set = Transition(fuel, "SetLinkageFuel");
        Record("Hornet committed as fuel is no longer available as a token starter",
            !(bool)Get(set, "HandHornet") && (bool)Get(set, "FieldFuel"));

        object battle = BaseState();
        Set(battle, "Turn1", false);
        SetExtra(battle, "Bridge");
        Set(battle, "BattleStarterAttack", 1500);
        Set(battle, "HandEngage", true);
        Set(battle, "HandWidow", true);
        Set(battle, "LemnisgateLive", true);
        Set(battle, "GraveSpells", 3);
        Set(battle, "GraveAceMonsters", 2);
        Check("Prepared 8000 line searches Linkage before more setup", battle,
            "ActivateEngage", 9726840);
        Set(battle, "HandLinkage", true);
        int damage = (int)PlannerType.GetMethod("GetPreparedBattleDamage", Flags)
            .Invoke(null, new[] { battle });
        Set(battle, "DeckRoze", false);
        int missingRoze = (int)PlannerType.GetMethod("GetPreparedBattleDamage", Flags)
            .Invoke(null, new[] { battle });
        Record("OTK projection needs both Raye and Roze access", damage == 8000 && missingRoze == 0);
        Set(battle, "DeckRoze", true);
        Set(battle, "MainOther", 1);
        Record("Blocked Main Zone invalidates the prepared Linkage battle line",
            (int)PlannerType.GetMethod("GetPreparedBattleDamage", Flags)
                .Invoke(null, new[] { battle }) == 0);

        object followUp = BaseState();
        Set(followUp, "HandHornet", true);
        int available = (int)PlannerType.GetMethod("GetFollowUpValue", Flags)
            .Invoke(null, new[] { followUp });
        Set(followUp, "ExtraZero", false);
        Set(followUp, "ExtraKagari", false);
        Set(followUp, "ExtraBridge", false);
        Set(followUp, "ExtraShizuku", false);
        Record("Next-turn starter value requires an available Extra Deck route",
            available > 0 && (int)PlannerType.GetMethod("GetFollowUpValue", Flags)
                .Invoke(null, new[] { followUp }) == 0);
    }

    private static object FixtureCard(Assembly assembly, int id, string location)
    {
        Type cardType = assembly.GetType("WindBot.Game.ClientCard", true);
        Type locationType = cardType.GetProperty("Location").PropertyType;
        object card = Activator.CreateInstance(cardType,
            new object[] { 0, Enum.Parse(locationType, location), 0, 1 });
        cardType.GetProperty("Id", Flags).SetValue(card, id, null);
        cardType.GetProperty("Type", Flags).SetValue(card, 2, null); // synthetic Spell metadata
        return card;
    }

    private static void CheckZeroSearchCallback(Assembly assembly)
    {
        object duel = Activator.CreateInstance(assembly.GetType("WindBot.Game.Duel", true));
        Array fields = (Array)duel.GetType().GetProperty("Fields").GetValue(duel, null);
        for (int i = 0; i < fields.Length; i++)
            fields.GetValue(i).GetType().GetMethod("Init").Invoke(fields.GetValue(i),
                new object[] { 0, 0, i });
        object executor = Activator.CreateInstance(ExecutorType, new object[] { null, duel });
        object deckEngage = FixtureCard(assembly, 63166095, "Deck");
        object graveEngage = FixtureCard(assembly, 63166095, "Grave");
        IList offered = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(deckEngage.GetType()));
        // Deliberately put grave first: the old planner override bypassed location ordering.
        offered.Add(graveEngage);
        offered.Add(deckEngage);
        object bot = fields.GetValue(0);
        object knownOwnDeck = Activator.CreateInstance(typeof(List<>).MakeGenericType(
            assembly.GetType("YGOSharp.OCGWrapper.NamedCard", true)));
        bot.GetType().GetMethod("SetInitialDeck").Invoke(bot, new[] { knownOwnDeck });
        IList grave = (IList)bot.GetType().GetProperty("Graveyard").GetValue(bot, null);
        grave.Add(graveEngage);
        grave.Add(FixtureCard(assembly, 98338152, "Grave"));
        grave.Add(FixtureCard(assembly, 52340444, "Grave"));
        FieldInfo pending = ExecutorType.GetField("StrategicSearchTargetPending", Flags);
        MethodInfo select = ExecutorType.GetMethod("SelectZeroSearch", Flags);
        pending.SetValue(executor, 63166095);
        IList selected = (IList)select.Invoke(executor, new object[] { offered, 1, 1 });
        Record("Zero selection callback honors Deck preference despite a planned Engage ID",
            selected.Count == 1 && selected[0] == deckEngage && (int)pending.GetValue(executor) == 0);
        grave.Add(FixtureCard(assembly, 9726840, "Grave"));
        pending.SetValue(executor, 63166095);
        selected = (IList)select.Invoke(executor, new object[] { offered, 1, 1 });
        Record("Next Zero callback re-evaluates grave threshold and clears its pending target",
            selected.Count == 1 && selected[0] == graveEngage && (int)pending.GetValue(executor) == 0);

        duel.GetType().GetProperty("Turn").SetValue(duel, 1, null);
        PropertyInfo phase = duel.GetType().GetProperty("Phase");
        phase.SetValue(duel, Enum.Parse(phase.PropertyType, "Main1"), null);
        object hornet = FixtureCard(assembly, 52340444, "Hand");
        IList hand = (IList)bot.GetType().GetProperty("Hand").GetValue(bot, null);
        hand.Add(hornet);
        hand.Add(FixtureCard(assembly, 98338152, "Hand"));
        object main = duel.GetType().GetProperty("MainPhase").GetValue(duel, null);
        ((IList)main.GetType().GetProperty("ActivableCards").GetValue(main, null)).Add(hornet);
        MethodInfo hornetHandler = ExecutorType.GetMethod("HornetDronesEffect", Flags);
        Record("Actual Hornet handler respects planner hold without any Extra Deck exit",
            !(bool)hornetHandler.Invoke(executor, null));

        object shizuku = FixtureCard(assembly, 90673288, "Extra");
        shizuku.GetType().GetProperty("Type", Flags).SetValue(shizuku, 0x4000021, null);
        ((IList)bot.GetType().GetProperty("ExtraDeck").GetValue(bot, null)).Add(shizuku);
        Record("Observed resource change retires hold and permits a completed Hornet route",
            (bool)hornetHandler.Invoke(executor, null));
    }

    private static string FirstKind(object plan)
    {
        IList steps = (IList)Get(plan, "Steps");
        return steps.Count == 0 ? "None" :
            Get(Get(steps[0], "Action"), "Kind").ToString();
    }

    private static int FirstTarget(object plan)
    {
        IList steps = (IList)Get(plan, "Steps");
        return steps.Count == 0 ? 0 :
            (int)Get(Get(steps[0], "Action"), "SearchTarget");
    }

    private static string[] RouteKinds(object plan)
    {
        return ((IList)Get(plan, "Steps")).Cast<object>()
            .Select(step => Get(Get(step, "Action"), "Kind").ToString())
            .ToArray();
    }

    private static void Check(string name, object state, string expectedKind,
        int expectedTarget, params string[] requiredKinds)
    {
        var watch = Stopwatch.StartNew();
        object plan = Plan(state);
        watch.Stop();
        string actualKind = FirstKind(plan);
        int actualTarget = FirstTarget(plan);
        string[] routeKinds = RouteKinds(plan);
        bool passed = actualKind == expectedKind &&
            (expectedTarget == 0 || actualTarget == expectedTarget) &&
            requiredKinds.All(routeKinds.Contains);
        Results.Add(new Dictionary<string, object>
        {
            { "name", name },
            { "passed", passed },
            { "expectedKind", expectedKind },
            { "actualKind", actualKind },
            { "expectedTarget", expectedTarget },
            { "actualTarget", actualTarget },
            { "routeKinds", routeKinds },
            { "elapsedMs", watch.ElapsedMilliseconds },
            { "expanded", Get(plan, "Expanded") },
            { "timedOut", Get(plan, "TimedOut") }
        });
        Console.WriteLine((passed ? "PASS " : "FAIL ") + name + ": " +
            actualKind + (actualTarget == 0 ? string.Empty : " -> " + actualTarget) +
            " (" + watch.ElapsedMilliseconds + " ms)");
    }

    private static void CheckForbiddenFirstTarget(string name, object state,
        string expectedKind, int forbiddenTarget)
    {
        var watch = Stopwatch.StartNew();
        object plan = Plan(state);
        watch.Stop();
        string actualKind = FirstKind(plan);
        int actualTarget = FirstTarget(plan);
        bool passed = actualKind == expectedKind && actualTarget != forbiddenTarget;
        Results.Add(new Dictionary<string, object>
        {
            { "name", name },
            { "passed", passed },
            { "expectedKind", expectedKind },
            { "actualKind", actualKind },
            { "forbiddenTarget", forbiddenTarget },
            { "actualTarget", actualTarget },
            { "elapsedMs", watch.ElapsedMilliseconds },
            { "expanded", Get(plan, "Expanded") },
            { "timedOut", Get(plan, "TimedOut") }
        });
        Console.WriteLine((passed ? "PASS " : "FAIL ") + name + ": " +
            actualKind + (actualTarget == 0 ? string.Empty : " -> " + actualTarget));
    }

    private static void CheckCancellation()
    {
        using (var cancellation = new CancellationTokenSource())
        {
            cancellation.Cancel();
            var watch = Stopwatch.StartNew();
            object plan = PlannerType.GetMethod("Plan", Flags).Invoke(null,
                new[] { BaseState(), (object)cancellation.Token });
            watch.Stop();
            bool passed = ((IList)Get(plan, "Steps")).Count == 0 &&
                watch.ElapsedMilliseconds < 500;
            Results.Add(new Dictionary<string, object>
            {
                { "name", "cancelled search returns no actions" },
                { "passed", passed },
                { "elapsedMs", watch.ElapsedMilliseconds },
                { "expanded", Get(plan, "Expanded") },
                { "timedOut", Get(plan, "TimedOut") }
            });
            Console.WriteLine((passed ? "PASS " : "FAIL ") +
                "cancelled search returns no actions (" +
                watch.ElapsedMilliseconds + " ms)");
        }
    }

    private static void CheckUltimateSkyPolicy(string name, int threatId,
        int observedScore, bool botTurn, int previousOwnCardId, bool expectedNegate)
    {
        const int threshold = 3600;
        int actualScore = (int)ExecutorType.GetMethod(
            "GetUltimateSkyChainPriority", Flags).Invoke(null, new object[]
            {
                threatId,
                observedScore,
                botTurn,
                previousOwnCardId
            });
        bool actualNegate = actualScore >= threshold;
        bool passed = actualNegate == expectedNegate;
        Results.Add(new Dictionary<string, object>
        {
            { "name", name },
            { "passed", passed },
            { "expectedNegate", expectedNegate },
            { "actualNegate", actualNegate },
            { "actualScore", actualScore },
            { "threshold", threshold }
        });
        Console.WriteLine((passed ? "PASS " : "FAIL ") + name + ": score=" +
            actualScore + "/" + threshold + " negate=" + actualNegate);
    }

    private static void CheckUltimateSkyOptionLatch()
    {
        bool selected = (bool)ExecutorType.GetMethod(
            "ShouldSelectUltimateSkyChainMode", Flags).Invoke(null,
            new object[] { true, false });
        Results.Add(new Dictionary<string, object>
        {
            { "name", "Ultimate Sky option keeps the accepted chain-negate mode" },
            { "passed", selected },
            { "pending", true },
            { "liveReevaluation", false },
            { "selectedChainMode", selected }
        });
        Console.WriteLine((selected ? "PASS " : "FAIL ") +
            "Ultimate Sky option keeps the accepted chain-negate mode");
    }

    private static void CheckZeroQuickPolicy(string name, bool canResolveExit,
        bool escapingRemoval, bool preemptiveRemoval, bool preventingLethal,
        bool losingZeroInBattle, int destroyScore, int minimumDestroyScore,
        bool expectedActivate)
    {
        bool actualActivate = (bool)ExecutorType.GetMethod(
            "ShouldActivateZeroQuickEffect", Flags).Invoke(null, new object[]
            {
                canResolveExit,
                escapingRemoval,
                preemptiveRemoval,
                preventingLethal,
                losingZeroInBattle,
                destroyScore,
                minimumDestroyScore
            });
        bool passed = actualActivate == expectedActivate;
        Results.Add(new Dictionary<string, object>
        {
            { "name", name },
            { "passed", passed },
            { "expectedActivate", expectedActivate },
            { "actualActivate", actualActivate },
            { "destroyScore", destroyScore },
            { "minimumDestroyScore", minimumDestroyScore }
        });
        Console.WriteLine((passed ? "PASS " : "FAIL ") + name +
            ": activate=" + actualActivate);
    }

    private static void CheckLemnisgatePolicy(string name, int usefulBounces,
        int maximumPairs, int expectedPairs)
    {
        int actualPairs = (int)ExecutorType.GetMethod(
            "GetLemnisgateRecyclePairCount", Flags).Invoke(null, new object[]
            {
                usefulBounces,
                maximumPairs
            });
        bool passed = actualPairs == expectedPairs;
        Results.Add(new Dictionary<string, object>
        {
            { "name", name },
            { "passed", passed },
            { "expectedPairs", expectedPairs },
            { "actualPairs", actualPairs },
            { "usefulBounces", usefulBounces },
            { "maximumPairs", maximumPairs }
        });
        Console.WriteLine((passed ? "PASS " : "FAIL ") + name +
            ": pairs=" + actualPairs);
    }

    private static void CheckLemnisgateOption(string name, int option,
        bool usefulBounce, bool usefulGraveLink, bool expectedChoice)
    {
        bool actualChoice = (bool)ExecutorType.GetMethod(
            "ShouldSelectLemnisgateOptionalEffect", Flags).Invoke(null,
            new object[] { option, usefulBounce, usefulGraveLink });
        bool passed = actualChoice == expectedChoice;
        Results.Add(new Dictionary<string, object>
        {
            { "name", name },
            { "passed", passed },
            { "option", option },
            { "expectedChoice", expectedChoice },
            { "actualChoice", actualChoice }
        });
        Console.WriteLine((passed ? "PASS " : "FAIL ") + name +
            ": choice=" + actualChoice);
    }

    private static void CheckLemnisgateNet(string name, int bounceValue,
        int recycleCost, bool expectedActivate)
    {
        bool actualActivate = (bool)ExecutorType.GetMethod(
            "ShouldActivateLemnisgateForNetValue", Flags).Invoke(null,
            new object[] { bounceValue, recycleCost });
        bool passed = actualActivate == expectedActivate;
        Results.Add(new Dictionary<string, object>
        {
            { "name", name },
            { "passed", passed },
            { "bounceValue", bounceValue },
            { "recycleCost", recycleCost },
            { "expectedActivate", expectedActivate },
            { "actualActivate", actualActivate }
        });
        Console.WriteLine((passed ? "PASS " : "FAIL ") + name +
            ": activate=" + actualActivate);
    }

    private static void CheckLemnisgateTempoWindow(string name,
        int enemyFieldCount, bool expectedWindow)
    {
        bool actualWindow = (bool)ExecutorType.GetMethod(
            "HasLemnisgateTempoWindow", Flags).Invoke(null,
            new object[] { enemyFieldCount });
        bool passed = actualWindow == expectedWindow;
        Results.Add(new Dictionary<string, object>
        {
            { "name", name },
            { "passed", passed },
            { "enemyFieldCount", enemyFieldCount },
            { "expectedWindow", expectedWindow },
            { "actualWindow", actualWindow }
        });
        Console.WriteLine((passed ? "PASS " : "FAIL ") + name +
            ": window=" + actualWindow);
    }

    private static void CheckLemnisgateReturnCount(string name,
        int recyclePairs, int expectedReturns)
    {
        int actualReturns = (int)ExecutorType.GetMethod(
            "GetLemnisgateReturnCount", Flags).Invoke(null,
            new object[] { recyclePairs });
        bool passed = actualReturns == expectedReturns;
        Results.Add(new Dictionary<string, object>
        {
            { "name", name },
            { "passed", passed },
            { "recyclePairs", recyclePairs },
            { "expectedReturns", expectedReturns },
            { "actualReturns", actualReturns }
        });
        Console.WriteLine((passed ? "PASS " : "FAIL ") + name +
            ": returns=" + actualReturns);
    }

    private static void CheckLemnisgateGraveLinkValue(string name,
        int planValue, int materialCost, bool expectedUse)
    {
        bool actualUse = (bool)ExecutorType.GetMethod(
            "ShouldUseLemnisgateGraveLinkForValue", Flags).Invoke(null,
            new object[] { planValue, materialCost });
        bool passed = actualUse == expectedUse;
        Results.Add(new Dictionary<string, object>
        {
            { "name", name },
            { "passed", passed },
            { "planValue", planValue },
            { "materialCost", materialCost },
            { "expectedUse", expectedUse },
            { "actualUse", actualUse }
        });
        Console.WriteLine((passed ? "PASS " : "FAIL ") + name +
            ": use=" + actualUse);
    }

    private static void CheckAshHold(string name,
        bool effectGuaranteedNegated, bool expectedHold)
    {
        bool actualHold = (bool)ExecutorType.GetMethod(
            "ShouldHoldAshAgainstGuaranteedNegation", Flags).Invoke(null,
            new object[] { effectGuaranteedNegated });
        bool passed = actualHold == expectedHold;
        Results.Add(new Dictionary<string, object>
        {
            { "name", name },
            { "passed", passed },
            { "expectedHold", expectedHold },
            { "actualHold", actualHold }
        });
        Console.WriteLine((passed ? "PASS " : "FAIL ") + name +
            ": hold=" + actualHold);
    }

    private static void CheckKainaBridge(string name, bool needsBridge,
        bool kainaAvailable, bool kainaAlreadyUsed, bool expectedPrefer)
    {
        bool actualPrefer = (bool)ExecutorType.GetMethod(
            "ShouldPreferKainaBridge", Flags).Invoke(null,
            new object[] { needsBridge, kainaAvailable, kainaAlreadyUsed });
        bool passed = actualPrefer == expectedPrefer;
        Results.Add(new Dictionary<string, object>
        {
            { "name", name },
            { "passed", passed },
            { "expectedPrefer", expectedPrefer },
            { "actualPrefer", actualPrefer }
        });
        Console.WriteLine((passed ? "PASS " : "FAIL ") + name +
            ": prefer=" + actualPrefer);
    }

    private static void CheckLemnisgateMirrorPenalty(string name,
        bool mirrorObserved, bool aceLink, bool zero, bool disabled,
        bool rayeOrRozeReady, int expectedPenalty)
    {
        int actualPenalty = (int)ExecutorType.GetMethod(
            "GetLemnisgateMirrorReturnPenalty", Flags).Invoke(null,
            new object[]
            {
                mirrorObserved,
                aceLink,
                zero,
                disabled,
                rayeOrRozeReady
            });
        bool passed = actualPenalty == expectedPenalty;
        Results.Add(new Dictionary<string, object>
        {
            { "name", name },
            { "passed", passed },
            { "expectedPenalty", expectedPenalty },
            { "actualPenalty", actualPenalty }
        });
        Console.WriteLine((passed ? "PASS " : "FAIL ") + name +
            ": penalty=" + actualPenalty);
    }

    private static void CheckSharkChainBranch(string name, bool botTurn,
        bool mainPhase, bool emptyMainZone, bool threeSpells,
        bool targetCanRevive, bool canConvert, bool expectedRevive)
    {
        bool actualRevive = (bool)ExecutorType.GetMethod(
            "ShouldReviveSharkChainTarget", Flags).Invoke(null, new object[]
            {
                botTurn,
                mainPhase,
                emptyMainZone,
                threeSpells,
                targetCanRevive,
                canConvert
            });
        bool passed = actualRevive == expectedRevive;
        Results.Add(new Dictionary<string, object>
        {
            { "name", name },
            { "passed", passed },
            { "expectedRevive", expectedRevive },
            { "actualRevive", actualRevive }
        });
        Console.WriteLine((passed ? "PASS " : "FAIL ") + name +
            ": revive=" + actualRevive);
    }

    private static void CheckWidowMirrorHold(string name, bool mirrorObserved,
        bool idleChain, bool targetIsRaye, bool targetDisabled, bool emergency,
        bool expectedHold)
    {
        bool actualHold = (bool)ExecutorType.GetMethod(
            "ShouldHoldWidowForMirrorRaye", Flags).Invoke(null, new object[]
            {
                mirrorObserved,
                idleChain,
                targetIsRaye,
                targetDisabled,
                emergency
            });
        bool passed = actualHold == expectedHold;
        Results.Add(new Dictionary<string, object>
        {
            { "name", name },
            { "passed", passed },
            { "expectedHold", expectedHold },
            { "actualHold", actualHold }
        });
        Console.WriteLine((passed ? "PASS " : "FAIL ") + name +
            ": hold=" + actualHold);
    }

    private static int Main(string[] args)
    {
        if (args.Length != 2 && args.Length != 3)
        {
            Console.Error.WriteLine("Usage: SkyStrikerPlannerChecks.exe WindBot.exe report.json [cards.cdb]");
            return 2;
        }

        try
        {
            Assembly assembly = Assembly.LoadFrom(Path.GetFullPath(args[0]));
            ExecutorType = assembly.GetType(
                "WindBot.Game.AI.Decks.SkyStriker2026Executor", true);
            StateType = ExecutorType.GetNestedType("SkyStrategicState", Flags);
            PlannerType = ExecutorType.GetNestedType("SkyStrategicPlanner", Flags);

            // Warm the planner separately from bounded behavior assertions.
            Plan(BaseState());
            CheckPlanningHorizon(assembly);
            if (args.Length == 3)
            {
                Environment.SetEnvironmentVariable("PATH", Path.GetDirectoryName(Path.GetFullPath(args[0])) +
                    Path.PathSeparator + Environment.GetEnvironmentVariable("PATH"));
                assembly.GetType("YGOSharp.OCGWrapper.NamedCardsManager", true)
                    .GetMethod("Init").Invoke(null, new object[] { Path.GetFullPath(args[2]) });
                CheckZeroSearchCallback(assembly);
            }

            object engageOnly = BaseState();
            Set(engageOnly, "HandEngage", true);
            Check("Engage finds the missing starter", engageOnly,
                "ActivateEngage", 52340444, "LinkZero", "ResolveZeroSearch");

            object rayeOnly = BaseState();
            Set(rayeOnly, "HandRaye", true);
            Check("Raye starts the Zero setup", rayeOnly, "NormalRaye", 0,
                "RayeTagZero", "ResolveZeroSearch");

            object establishedEngage = BaseState();
            Set(establishedEngage, "HandEngage", true);
            Set(establishedEngage, "HandRaye", true);
            Check("Established Engage converts access into interaction",
                establishedEngage, "ActivateEngage", 98338152, "RayeTagZero");

            object lemnisgateEngage = BaseState();
            Set(lemnisgateEngage, "HandEngage", true);
            Set(lemnisgateEngage, "HandRaye", true);
            Set(lemnisgateEngage, "HandWidow", true);
            Set(lemnisgateEngage, "GraveSpells", 2);
            Set(lemnisgateEngage, "GraveAceMonsters", 1);
            Set(lemnisgateEngage, "EnemyField", 1);
            Set(lemnisgateEngage, "LemnisgateLive", true);
            Check("Engage values a live Lemnisgate package", lemnisgateEngage,
                "ActivateEngage", 34433770, "RayeTagZero");

            object emptyFieldLemnisgate = BaseState();
            Set(emptyFieldLemnisgate, "HandEngage", true);
            Set(emptyFieldLemnisgate, "HandRaye", true);
            Set(emptyFieldLemnisgate, "HandWidow", true);
            Set(emptyFieldLemnisgate, "GraveSpells", 2);
            Set(emptyFieldLemnisgate, "GraveAceMonsters", 1);
            CheckForbiddenFirstTarget(
                "Engage does not search Lemnisgate into an empty opposing field",
                emptyFieldLemnisgate, "ActivateEngage", 34433770);

            object hornetOnly = BaseState();
            Set(hornetOnly, "HandHornet", true);
            Check("Hornet starts the Kagari bridge route", hornetOnly,
                "ActivateHornet", 0, "LinkKagari", "LinkBridge", "LinkZero");

            object mainTwoHornet = BaseState();
            Set(mainTwoHornet, "Main1", false);
            Set(mainTwoHornet, "HandHornet", true);
            Set(mainTwoHornet, "HandRoze", true);
            Check("Main Phase 2 does not invent the Hornet setup route",
                mainTwoHornet, "NormalRoze", 0);

            object ultimateLinkage = BaseState();
            Set(ultimateLinkage, "HandUltimate", true);
            Set(ultimateLinkage, "HandLinkage", true);
            Set(ultimateLinkage, "EnemyMonsters", 1);
            Set(ultimateLinkage, "EnemyField", 1);
            Check("Ultimate Sky converts dead Linkage into Zero", ultimateLinkage,
                "ActivateUltimateLinkage", 0, "ResolveZeroSearch");

            object setFuelLinkage = BaseState();
            Set(setFuelLinkage, "HandLinkage", true);
            Set(setFuelLinkage, "HandLinkageFuelId", 34433770);
            Check("Planner sets expendable fuel before Linkage",
                setFuelLinkage, "SetLinkageFuel", 34433770,
                "ActivateLinkage", "ResolveZeroSearch");

            object expensiveFuelLinkage = BaseState();
            Set(expensiveFuelLinkage, "HandLinkage", true);
            Set(expensiveFuelLinkage, "HandLinkageFuelId", 75105429);
            Set(expensiveFuelLinkage, "HandLinkageFuelCost", 20000);
            Check("Planner rejects Linkage when the only fuel loses more value",
                expensiveFuelLinkage, "None", 0);

            object deterministicBeforePot = BaseState();
            Set(deterministicBeforePot, "HandEngage", true);
            Set(deterministicBeforePot, "HandPot", true);
            Check("Deterministic Engage precedes Pot risk", deterministicBeforePot,
                "ActivateEngage", 52340444, "LinkZero");

            object pairClearance = BaseState();
            Set(pairClearance, "NormalAvailable", false);
            Set(pairClearance, "MainRaye", 1);
            Set(pairClearance, "MainRoze", 1);
            Check("Raye and Roze clear both Main Zones", pairClearance,
                "LinkZero", 0, "ResolveZeroSearch");

            CheckCancellation();

            CheckUltimateSkyPolicy("Ultimate Sky preserves itself against a low-value hand effect",
                99999999, 1600, true, 0, false);
            CheckUltimateSkyPolicy("Ultimate Sky stops Maxx C on the bot turn",
                23434538, 1600, true, 0, true);
            CheckUltimateSkyPolicy("Ultimate Sky protects Engage from Ash Blossom",
                14558127, 1600, true, 63166095, true);
            CheckUltimateSkyPolicy("Ultimate Sky still stops an independently dangerous grave effect",
                99999998, 4200, false, 0, true);
            CheckUltimateSkyOptionLatch();

            CheckZeroQuickPolicy("Zero escapes a target without requiring next-turn clearance",
                true, true, false, false, false, 0, 4800, true);
            CheckZeroQuickPolicy("Zero pre-empts the mirror Zero resolution window",
                true, false, true, false, false, 0, 4800, true);
            CheckZeroQuickPolicy("Zero destroys a valuable threat without a clearance gate",
                true, false, false, false, false, 5100, 4800, true);
            CheckZeroQuickPolicy("Zero holds against a low-value idle field",
                true, false, false, false, false, 1100, 3600, false);
            CheckZeroQuickPolicy("Zero cannot spend itself without both summon resources",
                false, true, true, true, true, 9000, 3600, false);

            CheckLemnisgatePolicy("Lemnisgate refuses a one-pair no-bounce recycle",
                0, 1, 0);
            CheckLemnisgatePolicy("Lemnisgate cannot stage itself by wasting one pair",
                0, 1, 0);
            CheckLemnisgatePolicy("Lemnisgate spends two pairs for one useful bounce",
                1, 2, 2);
            CheckLemnisgatePolicy("Lemnisgate spends three pairs for two useful bounces",
                2, 3, 3);
            CheckLemnisgateOption("Lemnisgate option 1 accepts a useful return",
                1, true, false, true);
            CheckLemnisgateOption("Lemnisgate option 1 refuses a targetless return",
                1, false, true, false);
            CheckLemnisgateOption("Lemnisgate option 2 follows the grave-Link plan",
                2, false, true, true);
            CheckLemnisgateNet("Lemnisgate rejects a negative resource exchange",
                3000, 2600, false);
            CheckLemnisgateNet("Lemnisgate accepts a material positive exchange",
                5200, 2400, true);
            CheckLemnisgateNet("Lemnisgate rejects targetless grave-Link staging",
                0, -3000, false);
            CheckLemnisgateTempoWindow(
                "Lemnisgate cannot activate into an empty opposing field", 0, false);
            CheckLemnisgateTempoWindow(
                "Lemnisgate may evaluate a public opposing field", 1, true);
            CheckLemnisgateReturnCount(
                "Lemnisgate one recycle pair cannot return a card", 1, 0);
            CheckLemnisgateReturnCount(
                "Lemnisgate two recycle pairs earn one return", 2, 1);
            CheckLemnisgateReturnCount(
                "Lemnisgate three recycle pairs earn two returns", 3, 2);
            CheckLemnisgateGraveLinkValue(
                "Lemnisgate GY Link accepts a positive conversion", 5600, 2100, true);
            CheckLemnisgateGraveLinkValue(
                "Lemnisgate GY Link rejects a thin material trade", 3500, 2500, false);
            CheckLemnisgateMirrorPenalty(
                "Lemnisgate does not return a disabled mirror Ace into a ready Raye",
                true, true, false, true, true, 8800);
            CheckLemnisgateMirrorPenalty(
                "Lemnisgate keeps Zero available as a genuine threat target",
                true, true, true, false, true, 0);

            CheckSharkChainBranch("Shark Cannon steals resolving Raye for an immediate route",
                true, true, true, true, true, true, true);
            CheckSharkChainBranch("Shark Cannon only banishes without a conversion route",
                true, true, true, true, true, false, false);
            CheckSharkChainBranch("Shark Cannon does not invent a steal on the opponent turn",
                false, true, true, true, true, true, false);

            CheckWidowMirrorHold("Widow holds against an idle mirror Raye that can tag",
                true, true, true, false, false, true);
            CheckWidowMirrorHold("Widow may answer Raye inside a live chain",
                true, false, true, false, false, false);

            CheckAshHold("Ash holds when the field effect is guaranteed negated",
                true, true);
            CheckAshHold("Ash remains available when a monster paid itself as cost",
                false, false);
            CheckKainaBridge("Hornet bridge prefers Kaina over Amatsu",
                true, true, false, true);

            bool passed = Results.All(result => (bool)result["passed"]);
            var report = new Dictionary<string, object>
            {
                { "purpose", "Offline SkyStriker2026 bounded route planner plus Ultimate Sky, Zero, Lemnisgate field/GY value, Shark Cannon, mirror Widow, Ash preservation and Kaina bridge policy regression; not engine legality or strength evidence" },
                { "checks", Results },
                { "callbackDatabase", args.Length == 3 ? Path.GetFullPath(args[2]) : null },
                { "passed", passed }
            };
            File.WriteAllText(Path.GetFullPath(args[1]),
                new JavaScriptSerializer().Serialize(report));
            return passed ? 0 : 1;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error.GetBaseException());
            return 2;
        }
    }
}
