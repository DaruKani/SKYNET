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
        object plan = Plan(state, CancellationToken.None);
        watch.Stop();
        string[] kinds = Kinds(plan);
        int[] targets = Targets(plan);
        bool passed = kinds.Length > 0 && kinds[0] == first &&
            required.All(kinds.Contains) && watch.ElapsedMilliseconds < 500;
        Results.Add(new Dictionary<string, object>
        {
            { "name", name },
            { "passed", passed },
            { "first", kinds.Length == 0 ? "None" : kinds[0] },
            { "route", kinds },
            { "targets", targets },
            { "elapsedMs", watch.ElapsedMilliseconds },
            { "expanded", Get(plan, "Expanded") },
            { "timedOut", Get(plan, "TimedOut") }
        });
        Console.WriteLine((passed ? "PASS " : "FAIL ") + name + ": " +
            (kinds.Length == 0 ? "None" : string.Join(" -> ", kinds)));
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

        object walpurgis = BaseState();
        Set(walpurgis, "HandWalpurgis", true);
        Check("Walpurgis route reaches a Witchcrafter fusion", walpurgis,
            "ActivateWalpurgis", "ActivateLowWitchcrafter", "ActivateCelebration");

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
        Check("Potterie plus any Spell starts the low Witchcrafter route", potterie,
            "NormalLowWitchcrafter", "ActivateLowWitchcrafter");

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
