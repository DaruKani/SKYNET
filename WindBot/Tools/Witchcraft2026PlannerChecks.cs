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
    private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    private static Type S, P, Z, A;
    private static string DeckPath;
    private static readonly List<object> Results = new List<object>();
    private static object Get(object o, string n) { return o.GetType().GetField(n, F).GetValue(o); }
    private static void Set(object o, string n, object v) { o.GetType().GetField(n, F).SetValue(o, v); }
    private static object Zone(string n) { return Enum.Parse(Z, n); }
    private static void Add(object s, int id, string z, int n = 1) { S.GetMethod("Add", F).Invoke(s, new[] { (object)id, Zone(z), n }); }
    private static int Count(object s, int id, string z) { return (int)S.GetMethod("Count", F).Invoke(s, new[] { (object)id, Zone(z) }); }
    private static void Move(object s, int id, string from, string to) { S.GetMethod("Move", F).Invoke(s, new[] { (object)id, Zone(from), Zone(to) }); }
    private static object Empty() { return Activator.CreateInstance(S, true); }
    private static object Opening(params int[] hand)
    {
        object s = Empty();
        string zone = "Deck";
        foreach (string raw in File.ReadAllLines(DeckPath))
        {
            string line = raw.Trim();
            if (line == "#extra") { zone = "Extra"; continue; }
            if (line == "!side") break;
            int id;
            if (int.TryParse(line, out id)) Add(s, id, zone);
        }
        foreach (int id in hand) Move(s, id, "Deck", "Hand");
        return s;
    }
    private static IList Actions(object s) { return ((IEnumerable)P.GetMethod("Actions", F).Invoke(null, new[] { s })).Cast<object>().ToList(); }
    private static object Apply(object s, object a) { return P.GetMethod("Apply", F).Invoke(null, new[] { s, a }); }
    private static object Plan(object s, object filter = null, CancellationToken token = default(CancellationToken))
    { return P.GetMethod("Plan", F).Invoke(null, new[] { s, filter, (object)token }); }
    private static string Kind(object a) { return Get(a, "Kind").ToString(); }
    private static IList Steps(object plan) { return (IList)Get(plan, "Steps"); }
    private static Func<T, bool> OnlyNormal<T>() { return a => Kind(a) == "Normal"; }
    private static object Property(object o, string n) { return o.GetType().GetProperty(n, F).GetValue(o, null); }
    private static void Property(object o, string n, object v) { o.GetType().GetProperty(n, F).SetValue(o, v, null); }

    private static void CallbackChecks(Type executor, string database)
    {
        Assembly asm = executor.Assembly;
        asm.GetType("YGOSharp.OCGWrapper.NamedCardsManager").GetMethod("Init").Invoke(null, new object[] { database });
        Type cardType = asm.GetType("WindBot.Game.ClientCard");
        Type location = asm.GetType("YGOSharp.OCGWrapper.Enums.CardLocation");
        Func<int, string, object> card = (id, zone) => Activator.CreateInstance(cardType,
            new object[] { id, Enum.Parse(location, zone), 0, 1 });
        Func<IList> list = () => (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(cardType));
        object duel = Activator.CreateInstance(asm.GetType("WindBot.Game.Duel"));
        foreach (object field in (Array)Property(duel, "Fields"))
            field.GetType().GetMethod("Init").Invoke(field, new object[] { 0, 0, 0 });
        object info = Activator.CreateInstance(asm.GetType("WindBot.WindBotInfo"));
        Property(info, "Chat", false);
        object game = Activator.CreateInstance(asm.GetType("WindBot.Game.GameClient"), new[] { info });
        object ai = Activator.CreateInstance(asm.GetType("WindBot.Game.GameAI"), new[] { game, duel });
        object ex = Activator.CreateInstance(executor, new[] { ai, duel });
        object summonTrigger = Activator.CreateInstance(A, true);
        Set(summonTrigger, "Kind", Enum.Parse(executor.GetNestedType("WKind", F), "GazeSearch"));
        Set(summonTrigger, "Source", 59400890); Set(summonTrigger, "Location", Zone("Board"));
        Set(summonTrigger, "Description", 0);
        object dm = card(59400890, "MonsterZone");
        Check("generic optional trigger description accepted",
            (bool)executor.GetMethod("Matches", F).Invoke(null, new[] { summonTrigger, dm, (object)(-1) }));
        Check("wrong effect description rejected",
            !(bool)executor.GetMethod("Matches", F).Invoke(null, new[] { summonTrigger, dm, (object)123 }));

        object state = Empty(); Add(state, 33475154, "Board"); Set(state, "Pending", 33475154);
        foreach (int id in new[] { 84523092, 58139997, 7656689 }) Add(state, id, "Deck");
        object recruit = Actions(state).Cast<object>().First();
        Set(ex, "_accepted", recruit); Set(ex, "_awaitingChain", true);
        IList candidates = list();
        foreach (int id in new[] { 64756282, 84523092, 58139997, 7656689 }) candidates.Add(card(id, "Deck"));
        int summonHint = (int)asm.GetType("WindBot.Game.AI.HintMsg").GetField("SpSummon", F).GetValue(null);
        IList picked = (IList)executor.GetMethod("OnSelectCard").Invoke(ex,
            new object[] { candidates, 1, 3, summonHint, false });
        Check("actual Rilliona callback selects three distinct offered bodies",
            picked.Count == 3 && picked.Cast<object>().Select(c => (int)Property(c, "Id"))
                .OrderBy(i => i).SequenceEqual(new[] { 84523092, 58139997, 7656689 }.OrderBy(i => i)));
        Property(duel, "LastSummonPlayer", 1);
        executor.GetMethod("OnSummoning").Invoke(ex, null);
        Check("opponent summon does not spend own normal", !(bool)Get(ex, "_normalUsed"));
        Property(duel, "LastSummonPlayer", 0);
        executor.GetMethod("OnSummoning").Invoke(ex, null);
        Check("own summon spends own normal", (bool)Get(ex, "_normalUsed"));
        Set(ex, "_extraLock", true); Set(ex, "_maxx", true);
        ((HashSet<string>)Get(ex, "_used")).Add("test");
        executor.GetMethod("OnNewTurn").Invoke(ex, null);
        Check("new turn clears usages, lock, Maxx C and prompt ownership",
            !(bool)Get(ex, "_extraLock") && !(bool)Get(ex, "_normalUsed") &&
            !(bool)Get(ex, "_maxx") && Get(ex, "_accepted") == null &&
            ((HashSet<string>)Get(ex, "_used")).Count == 0);
        int place = (int)executor.GetMethod("OnSelectPlace").Invoke(ex,
            new object[] { 69964858, 0, Enum.Parse(location, "MonsterZone"), 0x60 });
        Check("Pupils placement returns offered EMZ mask", place == 0x20);
    }
    private static void Check(string name, bool passed, object detail = null)
    {
        Results.Add(new { name, passed, detail });
        Console.WriteLine((passed ? "PASS " : "FAIL ") + name);
    }
    private static object Run(object state, string name)
    {
        List<string> route = new List<string>();
        long maxMs = 0;
        for (int i = 0; i < 36; i++)
        {
            Stopwatch watch = Stopwatch.StartNew();
            object plan = Plan(state);
            maxMs = Math.Max(maxMs, watch.ElapsedMilliseconds);
            IList steps = Steps(plan);
            if (steps.Count == 0) break;
            object first = steps[0];
            object action = Get(first, "Action");
            route.Add(action.ToString());
            object after = Get(first, "After");
            int beforeCount = ((IDictionary)Get(state, "Cards")).Values.Cast<int>().Sum();
            int afterCount = ((IDictionary)Get(after, "Cards")).Values.Cast<int>().Sum();
            if (beforeCount != afterCount) throw new InvalidOperationException("Transition created/lost a card: " + action);
            // Re-observation resets forecast-only counters on each real decision.
            Set(after, "Steps", 0);
            Set(after, "FutureSummons", 0);
            state = after;
        }
        Check(name + ": bounded and no loop", route.Count < 36 && maxMs < 1000,
            new { route, maxMs, board = P.GetMethod("BoardSummary", F).Invoke(null, new[] { state }) });
        return state;
    }
    private static int Main(string[] args)
    {
        string assembly = Path.GetFullPath(args[0]);
        AppDomain.CurrentDomain.AssemblyResolve += (o, e) =>
        {
            string path = Path.Combine(Path.GetDirectoryName(assembly), new AssemblyName(e.Name).Name + ".dll");
            return File.Exists(path) ? Assembly.LoadFrom(path) : null;
        };
        Type executor = Assembly.LoadFrom(assembly).GetType("WindBot.Game.AI.Decks.Witchcraft2026Executor", true);
        S = executor.GetNestedType("WState", F);
        P = executor.GetNestedType("WPlanner", F);
        Z = executor.GetNestedType("WZone", F);
        A = executor.GetNestedType("WAction", F);
        DeckPath = Path.Combine(Path.GetDirectoryName(assembly), "Decks", "AI_Witchcraft2026.ydk");
        try
        {
            Directory.SetCurrentDirectory(Path.GetDirectoryName(assembly));
            CallbackChecks(executor, args[2]);
            object start = Opening(32353566, 21522601, 6071005, 57916305, 22623509);
            object full = Run(start, "guide opening");
            Check("guide endpoint has Pupils, Rilliona, Vice, revived Dragoon and Distortion",
                Count(full, 69964858, "Board") > 0 && Count(full, 33475154, "Board") > 0 &&
                Count(full, 9603252, "Board") > 0 && Count(full, 37818794, "Board") > 0 &&
                Count(full, 69748261, "Backrow") > 0);
            object oldHand = Opening(21522601, 6071005, 58139997, 57916305, 22623509);
            object regression = Run(oldHand, "reported 230832 opening");
            Check("reported hand reaches Rilliona and revived Dragoon",
                Count(regression, 33475154, "Board") > 0 && Count(regression, 37818794, "Board") > 0);
            object missing = Opening(32353566, 21522601, 6071005, 57916305, 22623509);
            Add(missing, 33475154, "Extra", -1);
            object repair = Run(missing, "missing Rilliona");
            Check("missing Rilliona never fabricated", Count(repair, 33475154, "Board") == 0);
            object disabled = Opening(32353566, 21522601, 6071005, 57916305, 22623509);
            Move(disabled, 32353566, "Hand", "Backrow");
            ((HashSet<int>)Get(disabled, "Disabled")).Add(32353566);
            Add(disabled, 64756282, "Board");
            Check("disabled Walpurgis supplies no deck discard",
                !Actions(disabled).Cast<object>().Any(a => Kind(a) == "Recruit" && Get(a, "CostZone").ToString() == "Deck"));
            object locked = Empty();
            Add(locked, 6071005, "Board"); Add(locked, 59400890, "Extra");
            Set(locked, "SpellActivity", true); Set(locked, "ExtraLock", true);
            Check("Terracottan lock forbids Dark Magician bridge", !Actions(locked).Cast<object>().Any(a => Kind(a) == "DarkSummon"));
            object noMaterials = Empty(); Add(noMaterials, 6958567, "Hand"); Add(noMaterials, 9603252, "Extra");
            Check("no materials means no fusion", !Actions(noMaterials).Cast<object>().Any(a => Kind(a) == "Fusion"));
            object spentBifrost = Empty(); Add(spentBifrost, 32236916, "Hand"); Add(spentBifrost, 32353566, "Deck");
            Check("Bifrost requires actual three Extra costs", !Actions(spentBifrost).Cast<object>().Any(a => Kind(a) == "FieldAccess"));
            object summoned = Empty(); Add(summoned, 33475154, "Board");
            foreach (int id in new[] { 84523092, 58139997, 7656689 }) Add(summoned, id, "Deck");
            Set(summoned, "Pending", 33475154);
            object recruit = Actions(summoned).Cast<object>().First(a => Kind(a) == "RillieRecruit");
            object recruited = Apply(summoned, recruit);
            Check("Rilliona recruits three only through its separate trigger",
                Count(recruited, 84523092, "Board") == 1 && Count(recruited, 58139997, "Board") == 1 &&
                Count(recruited, 7656689, "Board") == 1 && (int)Get(recruited, "FutureSummons") == 1);
            object maxx = Opening(32353566, 21522601, 6071005, 57916305, 22623509);
            Set(maxx, "Maxx", true);
            object stop = Run(maxx, "Maxx C");
            Check("Maxx C does not force the full guide", Count(stop, 33475154, "Board") == 0);
            var cts = new CancellationTokenSource(); cts.Cancel();
            Check("cancelled planner commits no action", Steps(Plan(start, null, cts.Token)).Count == 0);
            object filter = typeof(Witchcraft2026PlannerChecks).GetMethod("OnlyNormal", F).MakeGenericMethod(A).Invoke(null, null);
            object normalOnly = Opening(64756282, 32353566, 21522601, 6071005, 57916305);
            IList filtered = Steps(Plan(normalOnly, filter));
            Check("root action restricted to actual offers", filtered.Count > 0 && Kind(Get(filtered[0], "Action")) == "Normal");
            object oneVice = Empty(); Add(oneVice, 9603252, "Board");
            object twoVice = S.GetMethod("Copy", F).Invoke(oneVice, null); Add(twoVice, 9603252, "Board");
            double firstValue = (double)P.GetMethod("Evaluate", F).Invoke(null, new[] { oneVice });
            double secondValue = (double)P.GetMethod("Evaluate", F).Invoke(null, new[] { twoVice });
            Check("duplicate Vice does not duplicate hard-once interaction value", secondValue - firstValue <= 3.01);
        }
        catch (Exception e)
        {
            Check("harness exception", false, e.ToString());
        }
        File.WriteAllText(args[1], new JavaScriptSerializer().Serialize(Results));
        return Results.Any(r => !(bool)r.GetType().GetProperty("passed").GetValue(r, null)) ? 1 : 0;
    }
}
