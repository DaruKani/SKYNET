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
    private static object Restrict(object s, int id, int used, bool active = true)
    {
        Type type = S.DeclaringType.GetNestedType("WRestriction", F);
        object r = Activator.CreateInstance(type, true);
        Set(r, "Id", id); Set(r, "Player", 1); Set(r, "Used", used); Set(r, "Active", active);
        Set(r, "SummonLimit", id == 94977269 ? 1 : 0); Set(r, "SpellHandGrave", id == 19261966);
        ((IList)Get(s, "Restrictions")).Add(r);
        return r;
    }
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
    private static void Property(object o, string n, object v)
    {
        PropertyInfo p = o.GetType().GetProperty(n, F);
        if (p != null) p.SetValue(o, v, null); else Set(o, n, v);
    }

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
        object exchangeState = Empty(); Add(exchangeState, 7656689, "Board");
        Add(exchangeState, 95245544, "Board"); Add(exchangeState, 22623509, "Deck");
        object exchange = Actions(exchangeState).Cast<object>().First(a => Kind(a) == "MaidExchange" &&
            ((int[])Get(a, "Materials"))[0] == 95245544);
        Set(ex, "_accepted", exchange);
        candidates = list(); candidates.Add(card(84523092, "MonsterZone"));
        candidates.Add(card(7656689, "MonsterZone")); candidates.Add(card(95245544, "MonsterZone"));
        int removeHint = (int)asm.GetType("WindBot.Game.AI.HintMsg").GetField("Remove", F).GetValue(null);
        picked = (IList)executor.GetMethod("OnSelectCard").Invoke(ex,
            new object[] { candidates, 1, 1, removeHint, false });
        Check("Maid removal prompt obeys planned expendable body, not Haine fallback",
            picked.Count == 1 && (int)Property(picked[0], "Id") == 95245544);
        object copyState = Empty(); Add(copyState, 64756282, "Grave"); Add(copyState, 73664385, "Grave");
        Add(copyState, 6071005, "Deck");
        object copy = Actions(copyState).Cast<object>().First(a => Kind(a) == "GenniLacrima");
        Set(ex, "_accepted", copy);
        int option = (int)executor.GetMethod("OnSelectOption").Invoke(ex,
            new object[] { new List<int> { 73664385 * 16 + 1, 73664385 * 16 + 2 } });
        Check("copied Lacrima chooses recruit rather than send mode", option == 1);
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
        // Actual ChainInfo snapshots and target object identities; no hidden identities.
        Type chainType = asm.GetType("WindBot.Game.ChainInfo");
        IList links = (IList)Property(duel, "CurrentChainInfo");
        Func<object, int, int, object> link = (c, player, desc) =>
            Activator.CreateInstance(chainType, new[] { c, (object)player, desc });
        object enemy = card(98338152, "SpellZone"); Property(enemy, "Controller", 1);
        Property(enemy, "Type", 65538);
        object haine = card(84523092, "MonsterZone"); Property(haine, "Type", 33);
        object hlink = link(haine, 0, 84523092 * 16);
        ((IList)Property(hlink, "Targets")).Add(enemy); links.Add(hlink);
        Func<object> forecast = () => executor.GetMethod("ChainForecast", F).Invoke(ex, null);
        Func<object, string, object, bool> contains = (o, field, v) =>
            (bool)Get(o, field).GetType().GetMethod("Contains").Invoke(Get(o, field), new[] { v });
        Check("pending Haine removal is not another useful Vice target",
            contains(forecast(), "Removed", enemy) &&
            (double)executor.GetMethod("RemovalValue", F).Invoke(ex, new[] { enemy, (object)false }) == 0);
        object dragoon = card(37818794, "MonsterZone");
        links.Add(link(dragoon, 1, 37818794 * 16 + 1));
        Check("duplicate target can be useful when prior removal is negated",
            !contains(forecast(), "Removed", enemy) &&
            (double)executor.GetMethod("RemovalValue", F).Invoke(ex, new[] { enemy, (object)false }) > 0);
        links.Clear(); links.Add(hlink); Property(enemy, "Location", Enum.Parse(location, "Grave"));
        Check("target that left field invalidates pending destruction", !contains(forecast(), "Removed", enemy));
        Property(enemy, "Location", Enum.Parse(location, "SpellZone"));
        links.Clear();
        object anchor = link(enemy, 1, -1); ((IList)Property(anchor, "Targets")).Add(haine);
        links.Add(anchor); links.Add(hlink);
        Check("destroying Anchor does not negate its monster disable", contains(forecast(), "Disabled", haine));
        Check("destroying activated quick-play has zero marginal value",
            (double)executor.GetMethod("RemovalValue", F).Invoke(ex, new[] { enemy, (object)true }) == 0);
        links.Clear();
        object fields = Property(duel, "Fields");
        object bot = ((Array)fields).GetValue(0), opponent = ((Array)fields).GetValue(1);
        bot.GetType().GetMethod("SetInitialDeck").Invoke(bot,
            new object[] { Array.CreateInstance(asm.GetType("YGOSharp.OCGWrapper.NamedCard"), 0) });
        Array monsters = (Array)Property(bot, "MonsterZone");
        monsters.SetValue(dragoon, 0); Property(dragoon, "Attack", 3000); Property(dragoon, "Type", 97);
        object vice = card(9603252, "MonsterZone"); Property(vice, "Type", 97); monsters.SetValue(vice, 1);
        object fieldCard = card(32353566, "SpellZone");
        links.Add(link(fieldCard, 0, 32353566 * 16));
        object ash = card(14558127, "Hand"); links.Add(link(ash, 1, -1));
        Property(duel, "LastChainPlayer", 1);
        Check("optional search on established board does not spend Dragoon",
            !(bool)executor.GetMethod("ValuableNegate", F).Invoke(ex, new object[] { "Dragoon", 10.0 }));
        links.Clear(); object zero = card(76072561, "Grave");
        links.Add(link(zero, 1, 76072561 * 16 + 1));
        Check("known Zero recruitment plus removal warrants reserved negate",
            (bool)executor.GetMethod("ValuableNegate", F).Invoke(ex, new object[] { "Dragoon", 10.0 }));
        links.Clear(); Property(duel, "Player", 0);
        Property(duel, "Phase", Enum.Parse(asm.GetType("YGOSharp.OCGWrapper.Enums.DuelPhase"), "Main1"));
        Property(Property(duel, "MainPhase"), "CanBattlePhase", true);
        Property(opponent, "LifePoints", 1900);
        Check("empty-field lethal enters Battle instead of another combo",
            (bool)executor.GetMethod("TryBattleForLethal", F).Invoke(ex, null));
        Property(dragoon, "Attacked", true);
        Check("already attacked monster cannot supply lethal",
            !(bool)executor.GetMethod("TryBattleForLethal", F).Invoke(ex, null));
        Property(dragoon, "Attacked", false); Property(opponent, "LifePoints", 8000);
        Check("nonlethal board keeps planning", !(bool)executor.GetMethod("TryBattleForLethal", F).Invoke(ex, null));
        Property(opponent, "LifePoints", 1900);
        ((Array)Property(opponent, "MonsterZone")).SetValue(card(76072561, "MonsterZone"), 0);
        Check("known opposing blocker invalidates empty-field forecast",
            !(bool)executor.GetMethod("TryBattleForLethal", F).Invoke(ex, null));
        links.Clear(); IList chainCards = (IList)Property(duel, "CurrentChain"); chainCards.Clear();
        object firstPrompt = Activator.CreateInstance(A, true), secondPrompt = Activator.CreateInstance(A, true);
        Set(firstPrompt, "Source", 9603252); Set(firstPrompt, "Target", 57916305);
        Set(secondPrompt, "Source", 9603252); Set(secondPrompt, "Target", 6958567);
        foreach (object prompt in new[] { firstPrompt, secondPrompt })
        {
            Set(ex, "_accepted", prompt); Set(ex, "_awaitingChain", true);
            executor.GetMethod("OnChaining").Invoke(ex, new object[] { 0, vice });
            chainCards.Add(vice); links.Add(link(vice, 0, 9603252 * 16 + 2));
        }
        Property(duel, "SolvingChainIndex", 1);
        bool firstBound = ReferenceEquals(executor.GetMethod("PromptAction", F).Invoke(ex, null), firstPrompt);
        Property(duel, "SolvingChainIndex", 2);
        Check("same-card chain links retain separate prompt ownership", firstBound &&
            ReferenceEquals(executor.GetMethod("PromptAction", F).Invoke(ex, null), secondPrompt));
        executor.GetMethod("OnChainEnd").Invoke(ex, null);
        Check("chain end retires all link-owned prompts", ((IDictionary)Get(ex, "_prompts")).Count == 0);
        links.Clear(); chainCards.Clear(); Property(duel, "SolvingChainIndex", 0);
        foreach (int index in Enumerable.Range(0, monsters.Length)) monsters.SetValue(null, index);
        Array foes = (Array)Property(opponent, "MonsterZone");
        foreach (int index in Enumerable.Range(0, foes.Length)) foes.SetValue(null, index);
        object winda = card(94977269, "MonsterZone"); Property(winda, "Controller", 1);
        Property(winda, "Type", 97); foes.SetValue(winda, 0);
        Func<object, int, bool> protectedFrom = (c, player) => (bool)executor.GetMethod("DestructionProtected", F)
            .Invoke(null, new[] { c, (object)player, false });
        Check("active Winda resists opposing effect destruction", protectedFrom(winda, 0));
        Check("Winda protection is controller-relative, not unconditional", !protectedFrom(winda, 1));
        Property(winda, "Disabled", 1);
        Check("negated Winda becomes destructible", !protectedFrom(winda, 0));
        Property(winda, "Disabled", 0);
        object verre = card(21522601, "MonsterZone"); Property(verre, "Type", 33);
        monsters.SetValue(verre, 0); monsters.SetValue(vice, 1);
        Set(ex, "_viceRemoval", winda);
        links.Add(link(verre, 0, 21522601 * 16 + 1)); links.Add(link(vice, 0, 9603252 * 16));
        Check("Vice ABOVE Verre resolves against still-protected Winda", !contains(forecast(), "Removed", winda));
        links.Clear(); links.Add(link(vice, 0, 9603252 * 16)); links.Add(link(verre, 0, 21522601 * 16 + 1));
        Check("Verre ABOVE Vice disables protection before destruction", contains(forecast(), "Removed", winda));
        links.Clear(); Set(ex, "_viceRemoval", null);
        double pair = (double)executor.GetMethod("DestructionPairValue", F).Invoke(ex, new[] { vice, winda });
        Check("Celebration pair predicts own Vice loss without Winda removal", pair < 0);
        object seed = card(59106048, "MonsterZone"); monsters.SetValue(seed, 2);
        object ordinary = card(69247929, "MonsterZone"); Property(ordinary, "Controller", 1);
        Property(ordinary, "Attack", 2000); Property(ordinary, "Type", 17);
        object pupils = card(69964858, "MonsterZone"); links.Add(link(pupils, 0, 69964858 * 16));
        Property(duel, "SolvingChainIndex", 1);
        candidates = list(); candidates.Add(vice); candidates.Add(seed); candidates.Add(winda); candidates.Add(ordinary);
        int destroyHint = (int)asm.GetType("WindBot.Game.AI.HintMsg").GetField("Destroy", F).GetValue(null);
        picked = (IList)executor.GetMethod("OnSelectCard").Invoke(ex, new object[] { candidates, 2, 2, destroyHint, false });
        Check("forced copied Celebration picks expendable friendly and destructible enemy",
            picked.Contains(seed) && picked.Contains(ordinary) && !picked.Contains(vice) && !picked.Contains(winda));
        Property(duel, "SolvingChainIndex", 0); links.Clear();
        object scheiren = card(572850, "Hand"); Property(scheiren, "Controller", 1);
        links.Add(link(scheiren, 1, 572850 * 16));
        Check("negate-destroy does not spend Distortion merely to enable known GY Fusion",
            !(bool)executor.GetMethod("ValuableNegate", F).Invoke(ex, new object[] { "Distortion", 5.0 }));
        object trap = card(69748261, "SpellZone"); links.Add(link(trap, 0, -1));
        Check("negate-destroy forecast sends hand monster away and records GY follow-up",
            contains(forecast(), "Removed", scheiren) && contains(forecast(), "GraveFollowups", scheiren));
        links.Clear(); Property(scheiren, "Location", Enum.Parse(location, "Grave"));
        links.Add(link(scheiren, 1, 572850 * 16 + 1));
        Check("actual GY Fusion offer can be negated without a second destroy trigger",
            (bool)executor.GetMethod("ValuableNegate", F).Invoke(ex, new object[] { "Distortion", 5.0 }));
        links.Clear();
        object ano = card(19261966, "MonsterZone"); Property(ano, "Controller", 1);
        Property(ano, "Attack", 2700); Property(ano, "Type", 97); foes.SetValue(ano, 0);
        monsters.SetValue(null, 0); monsters.SetValue(null, 2);
        Check("equal trade does not throw away Vice when no restriction bypass is unlocked",
            (double)executor.GetMethod("EqualTradeValue", F).Invoke(ex, new[] { vice, ano }) < 0);
        IList hand = (IList)Property(bot, "Hand"); hand.Add(card(70226289, "Hand")); hand.Add(card(84523092, "Hand"));
        hand.Add(card(57916305, "Hand"));
        Check("equal trade can be worthwhile when it unlocks a real hand-summon defense",
            (double)executor.GetMethod("EqualTradeValue", F).Invoke(ex, new[] { vice, ano }) > 0);
        hand.Clear();
        // Actual successful summon callbacks count events, not monsters in a group.
        foes.SetValue(winda, 0);
        ((IList)Property(duel, "LastSummonedCards")).Add(vice);
        Property(duel, "LastSummonPlayer", 0);
        executor.GetMethod("OnSpSummoning").Invoke(ex, null);
        executor.GetMethod("OnSpSummoned").Invoke(ex, null);
        object observed = executor.GetMethod("Observe", F).Invoke(ex, null);
        Check("observed Winda tracks one successful simultaneous summon event",
            ((IList)Get(observed, "Restrictions")).Cast<object>().Any(r => (int)Get(r, "Used") == 1));
        Property(winda, "Disabled", 1);
        executor.GetMethod("OnSpSummoning").Invoke(ex, null); executor.GetMethod("OnSpSummoned").Invoke(ex, null);
        observed = executor.GetMethod("Observe", F).Invoke(ex, null);
        Check("Winda counter still advances while temporarily disabled (core semantics)",
            ((IList)Get(observed, "Restrictions")).Cast<object>().Any(r => (int)Get(r, "Used") == 2 && !(bool)Get(r, "Active")));
        executor.GetMethod("OnMove").Invoke(ex, new object[] { winda, 1, 4, 1, 16 });
        Check("source leaving resets its own restriction lifetime", ((IDictionary)Get(ex, "_limitCounts")).Count == 0);
        Property(winda, "Disabled", 0);
        executor.GetMethod("OnSpSummoning").Invoke(ex, null); executor.GetMethod("OnSpSummoned").Invoke(ex, null);
        executor.GetMethod("OnPosChange").Invoke(ex, new object[] { winda, 1, 8 });
        Check("setting the restriction face-down clears its lifetime counter", ((IDictionary)Get(ex, "_limitCounts")).Count == 0);
        links.Clear(); Property(scheiren, "Location", Enum.Parse(location, "Hand"));
        links.Add(link(scheiren, 1, 572850 * 16));
        ((HashSet<int>)Get(ex, "_graveFusionSpent")).Add(572850);
        Check("already-spent public GY trigger does not cause unconditional Scheiren pass",
            (bool)executor.GetMethod("ValuableNegate", F).Invoke(ex, new object[] { "Distortion", 5.0 }));
        links.Clear(); object gazeCard = card(22283204, "SpellZone"); links.Add(link(gazeCard, 0, 22283204 * 16));
        Property(duel, "SolvingChainIndex", 1); Property(duel, "Turn", 5);
        IList summoned = (IList)Property(duel, "LastSummonedCards"); summoned.Clear(); summoned.Add(dragoon);
        monsters.SetValue(dragoon, 3); executor.GetMethod("OnSpSummoned").Invoke(ex, null);
        observed = executor.GetMethod("Observe", F).Invoke(ex, null);
        Check("Gaze successful summon callback observes next-turn expiry", ((IDictionary)Get(observed, "Temporary")).Contains(37818794) &&
            (int)((IDictionary)Get(observed, "Temporary"))[37818794] == 6);
        executor.GetMethod("OnMove").Invoke(ex, new object[] { dragoon, 0, 4, 0, 16 });
        Check("leaving field clears the observed Gaze card-instance lifetime", ((IDictionary)Get(ex, "_expires")).Count == 0);
    }

    private static void OutcomeModelChecks(Type executor)
    {
        object s = Empty(); Add(s, 64756282, "Board"); Add(s, 21744288, "Deck");
        Add(s, 84523092, "Deck"); Add(s, 70226289, "Hand"); Add(s, 32353566, "Backrow");
        Add(s, 57916305, "Deck"); object restriction = Restrict(s, 94977269, 0);
        object recruit = Actions(s).Cast<object>().First(a => Kind(a) == "Recruit" && (int)Get(a, "Target") == 21744288);
        object after = Apply(s, recruit);
        Check("one summon under Winda blocks subsequent ladder summons", !Actions(after).Cast<object>().Any(a => Kind(a) == "Recruit"));
        Set(restriction, "Used", 1);
        Check("spent Winda excludes summon roots rather than just lowering their score", !Actions(s).Cast<object>().Any(a => Kind(a) == "Recruit"));
        Set(restriction, "Active", false);
        Check("negating restriction restores summon generation", Actions(s).Cast<object>().Any(a => Kind(a) == "Recruit"));
        ((IList)Get(s, "Restrictions")).Clear();
        Check("removing restriction restores summon generation", Actions(s).Cast<object>().Any(a => Kind(a) == "Recruit"));
        s = Empty(); Add(s, 69964858, "Board"); Add(s, 6958567, "Hand"); Add(s, 9603252, "Extra");
        Add(s, 64756282, "Grave"); Add(s, 21744288, "Grave"); Add(s, 57916305, "Deck");
        Restrict(s, 94977269, 1);
        Check("Pupils cannot offer an impossible copied Fusion under spent Winda", !Actions(s).Cast<object>().Any(a => Kind(a) == "Fusion"));
        Check("Pupils retains its legal non-summoning search alternative", Actions(s).Cast<object>().Any(a => Kind(a) == "PupilsSearch"));
        object search = Actions(s).Cast<object>().First(a => Kind(a) == "PupilsSearch");
        after = Apply(s, search); ((IList)Get(after, "Restrictions")).Clear();
        Check("Pupils search and copy share the installed once-per-name limit", !Actions(after).Cast<object>().Any(a => Kind(a) == "Fusion" && (int)Get(a, "Source") == 69964858));
        Add(s, 21522601, "Board");
        object negate = Actions(s).Cast<object>().First(a => Kind(a) == "NegateBoard");
        after = Apply(s, negate);
        Check("Verre branch spends a real spell then unlocks Fusion", Count(after, 6958567, "Hand") == 0 &&
            ((IList)Get(after, "Restrictions")).Cast<object>().All(r => !(bool)Get(r, "Active")));
        s = Empty(); Add(s, 73664385, "Hand"); Add(s, 84523092, "Hand"); Add(s, 70226289, "Hand");
        Add(s, 64756282, "Grave"); Add(s, 73664385, "Grave"); Add(s, 58139997, "Board");
        Add(s, 21522601, "Grave"); Set(s, "EnemyChain", true); Restrict(s, 19261966, 0);
        Check("Anoyatyllis blocks direct spell summons from hand", !Actions(s).Cast<object>().Any(a => Kind(a) == "Unveiling" || Kind(a) == "LacrimaRecruit"));
        Check("copied Lacrima is a monster effect and can summon from hand", Actions(s).Cast<object>().Any(a => Kind(a) == "GenniLacrima" && (int)Get(a, "Target") == 84523092));
        Check("Anoyatyllis does not block Edel monster-effect revival", Actions(s).Cast<object>().Any(a => Kind(a) == "EdelRevive"));
        Add(s, 84523092, "Deck");
        recruit = Actions(s).Cast<object>().First(a => Kind(a) == "LacrimaRecruit" && (int)Get(a, "Target") == 84523092);
        after = Apply(s, recruit);
        Check("direct Lacrima chooses Deck rather than same-name blocked Hand", Count(after, 84523092, "Hand") == 1 && Count(after, 84523092, "Deck") == 0 && Count(after, 84523092, "Board") == 1);
        Add(s, 22283204, "Hand"); Add(s, 59400890, "Grave"); Add(s, 37818794, "Extra"); Set(s, "Turn", 5);
        object gaze = Actions(s).Cast<object>().First(a => Kind(a) == "Gaze"); after = Apply(s, gaze);
        Check("Gaze is an Extra summon despite its GY return target", Count(after, 37818794, "Board") == 1);
        object projected = executor.GetMethod("ProjectDelayed", F).Invoke(null, new[] { after, (object)6 });
        Check("Gaze Dragoon is banished in next-turn End projection", Count(projected, 37818794, "Board") == 0 && Count(projected, 37818794, "Banished") == 1);
        Move(after, 37818794, "Board", "Grave"); Move(after, 37818794, "Grave", "Board");
        projected = executor.GetMethod("ProjectDelayed", F).Invoke(null, new[] { after, (object)6 });
        Check("leaving and reviving clears Gaze delayed removal lifetime", Count(projected, 37818794, "Board") == 1);
        s = Empty(); Add(s, 58139997, "Hand"); Add(s, 32353566, "Backrow");
        Add(s, 95245544, "Deck"); Add(s, 21522601, "Deck"); Add(s, 83289866, "Backrow");
        Restrict(s, 94977269, 0);
        after = Run(s, "collapsed board with unmodeled-before Pittore access");
        Check("Walpurgis recovery reaches a real boss with remaining Pittore", Count(after, 21522601, "Board") == 1);
        s = Empty(); Add(s, 58139997, "Hand"); Add(s, 32353566, "Backrow"); Add(s, 83289866, "Backrow");
        Add(s, 95245544, "Deck"); Add(s, 59851535, "Deck"); Add(s, 84523092, "Deck"); Add(s, 57916305, "Deck");
        Add(s, 57916305, "Grave"); Add(s, 35098357, "Grave"); Add(s, 70226289, "Grave"); Restrict(s, 94977269, 0);
        after = Run(s, "194227 19:55 collapsed board public-resource subset");
        Check("reported collapse has Walpurgis -> remaining low -> Haine recovery", Count(after, 84523092, "Board") == 1);
        s = Empty(); Add(s, 58139997, "Hand");
        Check("empty board and only Edel does not invent a free summon", Actions(s).Count == 0);
        Add(s, 38943357, "Hand"); Add(s, 7656689, "Banished"); Add(s, 22623509, "Grave");
        Check("19:57 Mastery cannot search the Maids already outside Deck", Actions(s).Count == 0);
        s = Empty(); Add(s, 6958567, "Hand"); Add(s, 9603252, "Extra"); Add(s, 64756282, "Grave"); Add(s, 21744288, "Grave");
        Check("Fusion exists before new floodgate", Actions(s).Cast<object>().Any(a => Kind(a) == "Fusion"));
        string key = (string)S.GetMethod("Key", F).Invoke(s, null); Restrict(s, 94977269, 1);
        Check("new floodgate invalidates plan key and eliminates impossible Fusion", key != (string)S.GetMethod("Key", F).Invoke(s, null) &&
            !Actions(s).Cast<object>().Any(a => Kind(a) == "Fusion"));
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
        OutcomeModelChecks(executor);
        DeckPath = Path.Combine(Path.GetDirectoryName(assembly), "Decks", "AI_Witchcraft2026.ydk");
        try
        {
            Directory.SetCurrentDirectory(Path.GetDirectoryName(assembly));
            CallbackChecks(executor, args[2]);
            object reported = Opening(59106048, 95245544, 58139997, 73664385, 32236916);
            object openingPlan = Plan(reported);
            Check("150713 resolve field access before committing normal",
                Steps(openingPlan).Count > 0 && Kind(Get(Steps(openingPlan)[0], "Action")) != "Normal",
                Steps(openingPlan).Cast<object>().Select(s => Get(s, "Action").ToString()).ToArray());
            Move(reported, 32236916, "Hand", "Grave");
            ((HashSet<string>)Get(reported, "Used")).Add("32236916:FieldAccess");
            object noField = Run(reported, "failed Bifrost without field");
            Check("failed search has a productive continuation", Count(noField, 59106048, "Hand") < 1 ||
                Count(noField, 95245544, "Hand") < 1 || Count(noField, 32353566, "Backrow") > 0);
            object stuck = Opening(59106048, 58139997, 7656689);
            Move(stuck, 21522601, "Deck", "Board");
            Check("reported stranded Maid Genni has hand summon", Actions(stuck).Cast<object>().Any(a =>
                Kind(a) == "MaidSummon" && (int)Get(a, "Source") == 7656689));
            object recovered = Run(stuck, "field destroyed with Maid Genni in hand");
            Check("fieldless Maid line restores access or interaction",
                Count(recovered, 32353566, "Backrow") > 0 || Count(recovered, 9603252, "Board") > 0);
            object preserve = Empty(); Add(preserve, 7656689, "Board"); Add(preserve, 84523092, "Board");
            Add(preserve, 57916305, "Hand"); Add(preserve, 64756282, "Deck"); Set(preserve, "OpponentTurn", true);
            Check("do not banish live Haine merely for low recruit", Steps(Plan(preserve)).Count == 0);
            object enemyTurn = Empty(); Add(enemyTurn, 64756282, "Board"); Add(enemyTurn, 32353566, "Backrow");
            Add(enemyTurn, 57916305, "Deck"); Add(enemyTurn, 21522601, "Deck"); Set(enemyTurn, "OpponentTurn", true);
            Check("opponent turn cannot invent Walpurgis deck-discard cost",
                !Actions(enemyTurn).Cast<object>().Any(a => Kind(a) == "Recruit"));
            object pressure = Empty(); Add(pressure, 64756282, "Board"); Add(pressure, 73664385, "Hand");
            Add(pressure, 21522601, "Deck"); Set(pressure, "Maxx", true); Set(pressure, "Normal", false);
            Check("Maxx C optional low-value summon is passed", Steps(Plan(pressure)).Count == 0);
            Set(pressure, "IncomingAttack", 8000);
            // A weak existing body is not sufficient: test a live defense upgrade rather than body count.
            Set(pressure, "LifePoints", 1000);
            Check("Maxx C allows one defensive summon under public lethal pressure",
                Steps(Plan(pressure)).Count > 0 && Kind(Get(Steps(Plan(pressure))[0], "Action")) == "Recruit");
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
