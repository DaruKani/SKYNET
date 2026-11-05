using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Web.Script.Serialization;

internal static class Witchcraft2026PlannerChecks
{
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic |
        BindingFlags.Static | BindingFlags.Instance;
    private static Type StateType;
    private static Type PlannerType;
    private static Type ExecutorType;
    private static readonly List<Dictionary<string, object>> Results =
        new List<Dictionary<string, object>>();

    private static void Set(object target, string name, object value)
    {
        StateType.GetField(name, Flags).SetValue(target, value);
    }

    private static object Get(object target, string name)
    {
        FieldInfo field = target.GetType().GetField(name, Flags);
        return field.GetValue(target);
    }

    private static object BaseState()
    {
        object state = Activator.CreateInstance(StateType, true);
        Set(state, "MainPhase", true);
        Set(state, "NormalAvailable", true);
        Set(state, "ExtraViceMadame", true);
        Set(state, "ExtraMadameRillie", true);
        Set(state, "ExtraPupils", true);
        Set(state, "HandDistortion", true);
        Set(state, "HandSpellCount", 1);
        return state;
    }

    private static object Plan(object state, CancellationToken cancellation)
    {
        return PlannerType.GetMethod("Plan", Flags).Invoke(null,
            new[] { state, (object)cancellation });
    }

    private static IList Steps(object plan)
    {
        return (IList)Get(plan, "Steps");
    }

    private static string[] Kinds(object plan)
    {
        return Steps(plan).Cast<object>()
            .Select(step => Get(Get(step, "Action"), "Kind").ToString())
            .ToArray();
    }

    private static int[] Targets(object plan)
    {
        return Steps(plan).Cast<object>()
            .Select(step => (int)Get(Get(step, "Action"), "SearchTarget"))
            .ToArray();
    }

    private static void Check(string name, object state, string first,
        params string[] required)
    {
        Stopwatch watch = Stopwatch.StartNew();
        object current = state;
        List<string> kinds = new List<string>();
        List<int> targets = new List<int>();
        int expanded = 0;
        bool timedOut = false;
        bool singleActionPlans = true;
        for (int action = 0; action < 16; action++)
        {
            object plan = Plan(current, CancellationToken.None);
            IList steps = Steps(plan);
            singleActionPlans = singleActionPlans && steps.Count <= 1;
            expanded += (int)Get(plan, "Expanded");
            timedOut = timedOut || (bool)Get(plan, "TimedOut");
            if (steps.Count == 0)
                break;
            object step = steps[0];
            object strategicAction = Get(step, "Action");
            kinds.Add(Get(strategicAction, "Kind").ToString());
            targets.Add((int)Get(strategicAction, "SearchTarget"));
            current = Get(step, "After");
        }
        watch.Stop();
        bool passed = kinds.Count > 0 && kinds[0] == first &&
            required.All(kinds.Contains) && singleActionPlans &&
            watch.ElapsedMilliseconds < 500;
        Results.Add(new Dictionary<string, object>
        {
            { "name", name },
            { "passed", passed },
            { "first", kinds.Count == 0 ? "None" : kinds[0] },
            { "route", kinds.ToArray() },
            { "targets", targets.ToArray() },
            { "singleActionPlans", singleActionPlans },
            { "elapsedMs", watch.ElapsedMilliseconds },
            { "expanded", expanded },
            { "timedOut", timedOut }
        });
        Console.WriteLine((passed ? "PASS " : "FAIL ") + name + ": " +
            (kinds.Count == 0 ? "None" : string.Join(" -> ", kinds)));
    }

    private static void CheckInterruptionReplans()
    {
        object opening = BaseState();
        Set(opening, "HandSchmietta", true);
        object firstPlan = Plan(opening, CancellationToken.None);
        string[] firstKinds = Kinds(firstPlan);

        object interrupted = BaseState();
        Set(interrupted, "NormalAvailable", false);
        Set(interrupted, "HandSchmietta", false);
        Set(interrupted, "FieldLow", false);
        object replanned = Plan(interrupted, CancellationToken.None);
        string[] replannedKinds = Kinds(replanned);
        bool passed = Steps(firstPlan).Count == 1 && firstKinds.Length == 1 &&
            firstKinds[0] == "NormalSchmietta" && Steps(replanned).Count <= 1 &&
            !replannedKinds.Contains("ActivateLowWitchcrafter");
        Results.Add(new Dictionary<string, object>
        {
            { "name", "interruption replans from observed state" },
            { "passed", passed },
            { "before", firstKinds },
            { "after", replannedKinds }
        });
        Console.WriteLine((passed ? "PASS " : "FAIL ") +
            "interruption replans from observed state");
    }

    private static void CheckWalpurgisGuideTargets()
    {
        object current = BaseState();
        Set(current, "HandWalpurgis", true);
        List<string> kinds = new List<string>();
        List<int> targets = new List<int>();
        for (int action = 0; action < 8; action++)
        {
            object plan = Plan(current, CancellationToken.None);
            IList steps = Steps(plan);
            if (steps.Count == 0)
                break;
            object step = steps[0];
            object strategicAction = Get(step, "Action");
            kinds.Add(Get(strategicAction, "Kind").ToString());
            targets.Add((int)Get(strategicAction, "SearchTarget"));
            current = Get(step, "After");
        }
        int[] expectedTargets = { 64756282, 0, 21744288, 95245544, 59851535, 6071005 };
        bool passed = targets.Take(expectedTargets.Length).SequenceEqual(expectedTargets) &&
            kinds.Count == expectedTargets.Length &&
            kinds.Skip(2).All(kind => kind == "ActivateLowWitchcrafter");
        Results.Add(new Dictionary<string, object>
        {
            { "name", "Walpurgis follows the published low-Witchcrafter ladder" },
            { "passed", passed },
            { "route", kinds.ToArray() },
            { "targets", targets.ToArray() },
            { "expectedTargets", expectedTargets }
        });
        Console.WriteLine((passed ? "PASS " : "FAIL ") +
            "Walpurgis follows Genni -> Schmietta -> Pittore -> Potterie -> Marka");
    }

    private static void CheckCancellation()
    {
        using (CancellationTokenSource cancellation = new CancellationTokenSource())
        {
            cancellation.Cancel();
            Stopwatch watch = Stopwatch.StartNew();
            object plan = Plan(BaseState(), cancellation.Token);
            watch.Stop();
            bool passed = Steps(plan).Count == 0 && watch.ElapsedMilliseconds < 500;
            Results.Add(new Dictionary<string, object>
            {
                { "name", "cancelled search returns no actions" },
                { "passed", passed },
                { "elapsedMs", watch.ElapsedMilliseconds }
            });
            Console.WriteLine((passed ? "PASS " : "FAIL ") +
                "cancelled search returns no actions");
        }
    }

    private static void CheckBifrostCostGate()
    {
        MethodInfo gate = ExecutorType.GetMethod("HasBifrostCost", Flags);
        bool fresh = (bool)gate.Invoke(null, new object[]
        {
            new[] { 37442336, 53971455, 5559570 }
        });
        bool exhausted = (bool)gate.Invoke(null, new object[]
        {
            new[] { 37442336, 53971455 }
        });
        bool passed = fresh && !exhausted;
        Results.Add(new Dictionary<string, object>
        {
            { "name", "Bifrost is rejected after its one legal cost package is spent" },
            { "passed", passed },
            { "freshExtraDeck", fresh },
            { "spentExtraDeck", exhausted }
        });
        Console.WriteLine((passed ? "PASS " : "FAIL ") +
            "Bifrost cost gate rejects the spent Extra Deck");
    }

    private static int Main(string[] args)
    {
        if (args.Length != 2)
        {
            Console.Error.WriteLine("Usage: Witchcraft2026PlannerChecks.exe WindBot.exe report.json");
            return 2;
        }

        Assembly assembly = Assembly.LoadFrom(Path.GetFullPath(args[0]));
        Type executor = assembly.GetType(
            "WindBot.Game.AI.Decks.Witchcraft2026Executor", true);
        ExecutorType = executor;
        StateType = executor.GetNestedType("WitchcraftStrategicState", Flags);
        PlannerType = executor.GetNestedType("WitchcraftStrategicPlanner", Flags);

        Plan(BaseState(), CancellationToken.None);

        object mastery = BaseState();
        Set(mastery, "HandMastery", true);
        Check("Mastery opens Maid Verre access", mastery, "ActivateMastery",
            "SummonMaidVerre");

        object fieldAccess = BaseState();
        Set(fieldAccess, "HandFieldSpellAccess", true);
        Check("Terraforming or Bifrost opens Walpurgis", fieldAccess,
            "ActivateFieldSpellAccess", "ActivateWalpurgis");

        object creation = BaseState();
        Set(creation, "HandCreation", true);
        Check("Creation finds Marka then Walpurgis", creation, "ActivateCreation",
            "ActivateMarka", "ActivateWalpurgis");

        object marka = BaseState();
        Set(marka, "HandMarka", true);
        Check("Marka opens Walpurgis", marka, "ActivateMarka",
            "ActivateWalpurgis");

        CheckWalpurgisGuideTargets();

        object terracottan = BaseState();
        Set(terracottan, "HandTerracottan", true);
        Set(terracottan, "GraveWitchcrafterSpell", true);
        Set(terracottan, "GraveMadameVerre", true);
        Check("Terracottan honors its Witchcrafter Extra lock", terracottan,
            "ActivateTerracottan", "ActivateCelebration");

        object schmietta = BaseState();
        Set(schmietta, "HandSchmietta", true);
        Check("Schmietta plus any Spell does not pass", schmietta,
            "NormalSchmietta", "ActivateLowWitchcrafter");

        object potterie = BaseState();
        Set(potterie, "HandOtherLow", true);
        Set(potterie, "HandLowId", 59851535);
        Check("Potterie plus any Spell starts the low Witchcrafter route", potterie,
            "NormalLowWitchcrafter", "ActivateLowWitchcrafter");

        CheckInterruptionReplans();
        CheckBifrostCostGate();
        CheckCancellation();

        bool passedAll = Results.All(result => (bool)result["passed"]);
        Dictionary<string, object> report = new Dictionary<string, object>
        {
            { "kind", "offline-planner-fixture" },
            { "passed", passedAll },
            { "checks", Results },
            { "note", "These checks do not start ocgcore or establish competitive strength." }
        };
        File.WriteAllText(args[1], new JavaScriptSerializer().Serialize(report));
        return passedAll ? 0 : 1;
    }
}
