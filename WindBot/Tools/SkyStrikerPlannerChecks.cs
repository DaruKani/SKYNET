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

    private static int Main(string[] args)
    {
        if (args.Length != 2)
        {
            Console.Error.WriteLine("Usage: SkyStrikerPlannerChecks.exe WindBot.exe report.json");
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
            Check("Engage values a live Lemnisgate package", lemnisgateEngage,
                "ActivateEngage", 34433770, "RayeTagZero");

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

            bool passed = Results.All(result => (bool)result["passed"]);
            var report = new Dictionary<string, object>
            {
                { "purpose", "Offline SkyStriker2026 bounded planner and Ultimate Sky policy regression; not engine legality or strength evidence" },
                { "checks", Results },
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
