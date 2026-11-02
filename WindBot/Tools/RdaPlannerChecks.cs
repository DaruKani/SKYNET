using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Web.Script.Serialization;

// Explicitly compiled by Invoke-RdaPlannerChecks.ps1, never part of WindBot.exe.
internal static class RdaPlannerChecks
{
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    private static Type ExecutorType;
    private static Type StateType;
    private static Type SearchType;
    private static Type PlannerType;
    private static readonly List<Dictionary<string, object>> Checks = new List<Dictionary<string, object>>();
    private static readonly List<Dictionary<string, object>> Hands = new List<Dictionary<string, object>>();
    private static List<int> MainDeck;
    private static List<int> ExtraDeck;

    private static object Get(object instance, string name)
    {
        FieldInfo field = instance.GetType().GetField(name, Flags);
        if (field != null) return field.GetValue(instance);
        return instance.GetType().GetProperty(name, Flags).GetValue(instance, null);
    }

    private static void Set(object instance, string name, object value)
    {
        instance.GetType().GetField(name, Flags).SetValue(instance, value);
    }

    private static object Call(string method, params object[] arguments)
    {
        return PlannerType.GetMethod(method, Flags).Invoke(null, arguments);
    }

    private static string Hash(string path)
    {
        using (var stream = File.OpenRead(path))
        using (var sha = SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
    }

    private static void Check(string name, bool passed, long elapsed, string detail)
    {
        Checks.Add(new Dictionary<string, object> { { "name", name }, { "passed", passed }, { "elapsedMs", elapsed }, { "detail", detail } });
        Console.WriteLine((passed ? "PASS " : "FAIL ") + name + " (" + elapsed + " ms): " + detail);
    }

    private static object State(int[] hand)
    {
        var remaining = new List<int>(MainDeck);
        foreach (int id in hand)
            if (!remaining.Remove(id)) throw new ArgumentException("Hand exceeds the selected deck: " + id);
        var state = Activator.CreateInstance(StateType, true);
        Set(state, "Hand", hand.OrderBy(id => id).ToArray());
        Set(state, "Deck", remaining.OrderBy(id => id).ToArray());
        Set(state, "Extra", ExtraDeck.OrderBy(id => id).ToArray());
        Set(state, "NormalSummon", 1);
        return state;
    }

    private static object Search(int limitMs, CancellationToken token)
    {
        var options = Activator.CreateInstance(SearchType, true);
        Set(options, "Name", "explicit-check");
        Set(options, "Width", 400);
        Set(options, "TimeLimitMs", limitMs);
        Set(options, "Token", token);
        return options;
    }

    private static IList Portfolio(object options)
    {
        var searches = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(SearchType));
        searches.Add(options);
        searches.Add(options);
        searches.Add(options);
        return searches;
    }

    private static void GateCheck(bool cancelled, bool sharedDeadline = false)
    {
        var gateField = PlannerType.GetField("SearchGateLock", Flags);
        var permitsField = PlannerType.GetField("SearchGatePermits", Flags);
        var activeField = PlannerType.GetField("SearchGateActive", Flags);
        Type budgetType = ExecutorType.GetNestedType("PlanningBudget", Flags);
        if (sharedDeadline && budgetType == null)
        {
            Check("absolute deadline gate waiter", false, 0, "This binary predates the monotonic request deadline.");
            return;
        }
        object gate = gateField.GetValue(null);
        int permits = (int)permitsField.GetValue(null);
        using (var release = new ManualResetEvent(false))
        using (var cancellation = new CancellationTokenSource())
        {
            if (cancelled) cancellation.Cancel();
            // Occupy every permit, without creating an unrelated duel or worker.
            lock (gate) activeField.SetValue(null, permits);
            var unblock = new Thread(() =>
            {
                release.WaitOne(1000);
                lock (gate)
                {
                    activeField.SetValue(null, 0);
                    Monitor.PulseAll(gate);
                }
            });
            unblock.IsBackground = true;
            unblock.Start();
            var watch = Stopwatch.StartNew();
            object root = State(new int[0]);
            object plan;
            int during;
            try
            {
                object options = Search(cancelled || sharedDeadline ? 5000 : 75, cancellation.Token);
                if (sharedDeadline)
                    Set(options, "Budget", Activator.CreateInstance(budgetType, new object[] { CancellationToken.None, 75 }));
                plan = Call("Plan", root, options, null);
                during = (int)activeField.GetValue(null);
            }
            finally
            {
                release.Set();
                unblock.Join(2000);
            }
            watch.Stop();
            string name = sharedDeadline ? "absolute deadline gate waiter" : cancelled ? "cancelled gate waiter" : "queue wait consumes search budget";
            Check(name, watch.ElapsedMilliseconds < 500 && ((IList)Get(plan, "Steps")).Count == 0,
                watch.ElapsedMilliseconds, "Expired work returns the supplied root with no actions.");
            Check(name + " permit balance", during == permits && (int)activeField.GetValue(null) == 0,
                0, "A waiter that never entered must not release another search's permit.");
        }
    }

    private static void PortfolioChecks(int[] hand)
    {
        var budgetField = PlannerType.GetField("PortfolioBudgetMs", Flags);
        if (budgetField == null)
        {
            Check("whole portfolio deadline", false, 0, "This binary predates the portfolio deadline.");
            return;
        }
        int previous = (int)budgetField.GetValue(null);
        try
        {
            budgetField.SetValue(null, 150);
            var watch = Stopwatch.StartNew();
            object plan = Call("PlanPortfolio", State(hand), Portfolio(Search(10000, CancellationToken.None)), null);
            watch.Stop();
            Check("whole portfolio deadline", watch.ElapsedMilliseconds < 1000, watch.ElapsedMilliseconds,
                "150 ms portfolio budget includes routes, gate wait, beam searches and post-processing; allowance covers JIT/scheduling.");
            Check("portfolio permits retired", (int)PlannerType.GetProperty("SearchGateInUse", Flags).GetValue(null, null) == 0,
                0, "No beam-search permit remains occupied after the portfolio returns.");
            using (var cancel = new CancellationTokenSource())
            {
                cancel.Cancel();
                watch.Restart();
                plan = Call("PlanPortfolio", State(hand), Portfolio(Search(10000, cancel.Token)), null);
                watch.Stop();
                Check("cancelled portfolio has no actions", watch.ElapsedMilliseconds < 500 && ((IList)Get(plan, "Steps")).Count == 0,
                    watch.ElapsedMilliseconds, "A retired request cannot contribute a route or action.");
            }
        }
        finally { budgetField.SetValue(null, previous); }
    }

    private static void PrequeuedDeadlineCheck(int[] hand)
    {
        Type budgetType = ExecutorType.GetNestedType("PlanningBudget", Flags);
        if (budgetType == null)
        {
            Check("background queue retains original absolute deadline", false, 0, "This binary predates the monotonic request deadline.");
            return;
        }
        object budget = Activator.CreateInstance(budgetType, new object[] { CancellationToken.None, 20 });
        Thread.Sleep(40);
        object options = Search(10000, CancellationToken.None);
        Set(options, "Budget", budget);
        var watch = Stopwatch.StartNew();
        object plan = Call("PlanPortfolio", State(hand), Portfolio(options), null);
        watch.Stop();
        Check("background queue retains original absolute deadline", watch.ElapsedMilliseconds < 500
            && ((IList)Get(plan, "Steps")).Count == 0 && (long)Get(plan, "PoolExpanded") == 0,
            watch.ElapsedMilliseconds, "Expired timestamp with CancellationToken.None must not restart its clock or expand nodes.");
    }

    private static void NodeCapCheck(int[] hand)
    {
        object options = Search(10000, CancellationToken.None);
        FieldInfo cap = SearchType.GetField("MaxExpandedNodes", Flags);
        if (cap == null)
        {
            Check("expanded node cap", false, 0, "This binary predates the node cap.");
            return;
        }
        cap.SetValue(options, 1);
        var watch = Stopwatch.StartNew();
        object plan = Call("Plan", State(hand), options, null);
        watch.Stop();
        Check("expanded node cap", (long)Get(plan, "Expanded") <= 1, watch.ElapsedMilliseconds,
            "A one-node budget cannot start a second expansion.");
    }

    private static void FixedHand(string name, int[] hand)
    {
        object root = State(hand);
        int[] initialHand = (int[])((int[])Get(root, "Hand")).Clone();
        var watch = Stopwatch.StartNew();
        object plan = Call("PlanPortfolio", root, Portfolio(Search(1500, CancellationToken.None)), null);
        watch.Stop();
        var steps = (IList)Get(plan, "Steps");
        var actions = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(ExecutorType.GetNestedType("PlanAction", Flags)));
        foreach (object step in steps) actions.Add(Get(step, "Action"));
        object replay = Call("SimulateActions", root, actions);
        object final = Get(plan, "Final");
        bool agrees = steps.Count == 0 || (replay != null && (bool)StateType.GetMethod("Equals", new[] { StateType }).Invoke(
            Get(((IList)replay)[((IList)replay).Count - 1], "After"), new[] { final }));
        Check(name + " model replay", agrees && initialHand.SequenceEqual((int[])Get(root, "Hand")),
            watch.ElapsedMilliseconds, "Every chosen step replays in the same model, and the root remains unchanged; this is not engine legality evidence.");
        Hands.Add(new Dictionary<string, object>
        {
            { "name", name }, { "hand", hand }, { "elapsedMs", watch.ElapsedMilliseconds },
            { "tier", Get(plan, "Tier") }, { "score", Get(plan, "Score") },
            { "steps", steps.Cast<object>().Select(step => Get(Get(step, "Action"), "Text")).ToArray() },
            { "finalMonsterIds", ((long[])Get(final, "Field")).Select(card => (int)(card >> 12)).ToArray() },
            { "finalPending", Get(final, "Pending") }
        });
    }

    private static void ReadDeck(string path)
    {
        MainDeck = new List<int>();
        ExtraDeck = new List<int>();
        List<int> section = MainDeck;
        foreach (string raw in File.ReadLines(path))
        {
            string line = raw.Trim();
            if (line == "#main") { section = MainDeck; continue; }
            if (line == "#extra") { section = ExtraDeck; continue; }
            if (line == "!side") { section = null; continue; }
            int id;
            if (section != null && int.TryParse(line, out id)) section.Add(id);
        }
        if (MainDeck.Count != 40 || ExtraDeck.Count != 15) throw new ArgumentException("Expected the selected 40/15 RDA deck.");
    }

    private static int Main(string[] args)
    {
        if (args.Length != 4) { Console.Error.WriteLine("Usage: RdaPlannerChecks.exe WindBot.exe cards.cdb deck.ydk report.json"); return 2; }
        try
        {
            string executable = Path.GetFullPath(args[0]);
            Assembly assembly = Assembly.LoadFrom(executable);
            assembly.GetType("YGOSharp.OCGWrapper.NamedCardsManager").GetMethod("Init").Invoke(null, new object[] { Path.GetFullPath(args[1]) });
            ExecutorType = assembly.GetType("WindBot.Game.AI.Decks.TheCrimsonKingExecutor", true);
            StateType = ExecutorType.GetNestedType("RdaState", Flags);
            SearchType = ExecutorType.GetNestedType("SearchOptions", Flags);
            PlannerType = ExecutorType.GetNestedType("RdaPlanner", Flags);
            ReadDeck(args[2]);
            Call("ApplyMode", 400, 1500, 2, 200, 600);
            // Warm type initialization separately from bounded queue assertions.
            Call("Plan", State(new int[0]), Search(0, CancellationToken.None), null);
            GateCheck(true);
            GateCheck(false);
            GateCheck(false, true);
            int[] soul = { 62991792, 14558127, 23434538, 10045474, 10045474 };
            PortfolioChecks(soul);
            PrequeuedDeadlineCheck(soul);
            NodeCapCheck(soul);
            FixedHand("Soul starter", soul);
            FixedHand("Bone starter", new[] { 25784595, 14558127, 23434538, 10045474, 10045474 });
            FixedHand("Power Vice with Darkness", new[] { 19434243, 83445539, 14558127, 23434538, 10045474 });
            var report = new Dictionary<string, object>
            {
                { "purpose", "Offline RDA scheduling and model regression; not an ocgcore duel or strength benchmark" },
                { "executableSha256", Hash(args[0]) }, { "databaseSha256", Hash(args[1]) }, { "deckSha256", Hash(args[2]) },
                { "checks", Checks }, { "hands", Hands }, { "passed", Checks.All(check => (bool)check["passed"]) }
            };
            File.WriteAllText(args[3], new JavaScriptSerializer().Serialize(report));
            return Checks.All(check => (bool)check["passed"]) ? 0 : 1;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error.GetBaseException());
            return 2;
        }
    }
}
