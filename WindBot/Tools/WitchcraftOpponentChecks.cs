using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Web.Script.Serialization;
using WindBot;
using WindBot.Game;
using WindBot.Game.AI;
using WindBot.Game.AI.Decks;
using YGOSharp.OCGWrapper;
using YGOSharp.OCGWrapper.Enums;

internal static class WitchcraftOpponentChecks
{
    private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    private static readonly List<object> Results = new List<object>();
    private static int Failures;
    private static void Check(string name, bool passed, object evidence = null)
    {
        Results.Add(new { name, passed, evidence });
        Console.WriteLine((passed ? "PASS " : "FAIL ") + name);
        if (!passed) Failures++;
    }
    private static object Call(object obj, string method, params object[] args)
    { return obj.GetType().GetMethod(method, F).Invoke(obj, args); }
    private static void Set(object obj, string field, object value)
    { obj.GetType().GetField(field, F).SetValue(obj, value); }
    private static object Get(object obj, string field) { return obj.GetType().GetField(field, F).GetValue(obj); }
    private static ClientCard Card(int id, CardLocation loc = CardLocation.MonsterZone, int player = 1)
    {
        ClientCard c = new ClientCard(id, loc, 0, (int)CardPosition.FaceUpAttack) { Controller = player };
        NamedCard data = NamedCard.Get(id);
        foreach (string name in new[] { "Type", "Attack", "Defense", "Level", "Race" })
            typeof(ClientCard).GetProperty(name).SetValue(c, typeof(NamedCard).GetProperty(name).GetValue(data, null), null);
        return c;
    }
    private static ChainInfo Link(ClientCard c, int offset) { return new ChainInfo(c, c.Controller, c.Id * 16 + offset); }
    private static OpponentThreat Assess(OpponentAwareness model, ClientCard c, params ChainInfo[] links)
    { return model.Assess(c, links, new HashSet<int>(), false, true); }
    private static void UsageChecks()
    {
        var m = new OpponentAwareness();
        ClientCard izuna = Card(92248362), jokul = Card(28642461), hollie = Card(93156774);
        ChainInfo izunaLink = Link(izuna, 2);
        var izunaPending = Assess(m, izuna, izunaLink);
        var jokulUnused = Assess(m, jokul, izunaLink);
        Check("113011 Izuna committed send continues; Jokul retains ignition search",
            izunaPending.PreventedResolution == 0 && izunaPending.Future == 0 && jokulUnused.Future > 0 &&
            jokulUnused.Removal > izunaPending.Removal, new[] { izunaPending.Explain(), jokulUnused.Explain() });
        m.Solved(new[] { izunaLink }, 1, false); m.EndChain();
        ChainInfo j = Link(jokul, 2); m.Observe(new[] { j });
        m.Solved(new[] { j }, 1, true); m.EndChain();
        Check("negated use-count ignition remains consumed", Assess(m, jokul).Future == 0);
        var use = m.Usage(m.Identify(j), jokul, 1);
        Check("activation and negated status distinct from resolved", use.Activated == 1 && use.NegatedOrDisabled == 1 && use.Resolved == 0);
        Check("Jokul already used reverses removal ranking by remaining body", Assess(m, izuna).Removal > Assess(m, jokul).Removal);
        m.NewTurn();
        Check("turn reset restores ignition", Assess(m, jokul).Future > 0);
        jokul.Location = CardLocation.Hand; ChainInfo hand = Link(jokul, 1);
        m.Observe(new[] { hand }); m.Solved(new[] { hand }, 1, false); m.EndChain(); jokul.Location = CardLocation.MonsterZone;
        Check("hand summon use does not consume field search", Assess(m, jokul).Future > 0);
        ChainInfo control = Link(hollie, 1); m.Observe(new[] { control }); m.Solved(new[] { control }, 1, false); m.EndChain();
        Check("Hollie control and recruit modes share their count limit", Assess(m, hollie).Future == 0);
        m.NewTurn();
        ClientCard spell = Card(80181649, CardLocation.SpellZone);
        var caseValue = Assess(m, spell, Link(spell, 0)); var holValue = Assess(m, hollie, Link(spell, 0));
        Check("Case search is prevented but destruction gives a separate possible Quick-Play",
            caseValue.PreventedResolution == 8 && caseValue.DestructionBenefit > 0 && caseValue.Future == 0,
            new { caseValue = caseValue.Explain(), hollie = holValue.Explain(), conclusion = "diagnostic, not a winner oracle; reveal costs private" });
        Check("Hollie hidden reveal conditions represented as conditional not a guaranteed steal", holValue.Future > 0 && holValue.Future < 18);
        m.Solved(new[] { Link(spell, 0) }, 1, true); m.EndChain();
        Check("activation oath availability marked uncertain after combined protocol negation", m.Usage(m.Identify(Link(spell, 0)), spell, 1).ConsumptionUncertain);
        var c104 = Card(49456901);
        Check("C104 without specific material cannot threaten a negate", m.VisibleResponses(new[] { c104 }) == OpponentResponse.None);
        c104.Overlays.Add(2061963);
        Check("C104 with observed Number104 material exposes only field-monster response", m.VisibleResponses(new[] { c104 }) == OpponentResponse.FieldMonster);
        var cLink = Link(c104, 1); m.Observe(new[] { cLink }); m.Solved(new[] { cLink }, 1, false); m.EndChain();
        Check("used C104 response no longer counted", m.VisibleResponses(new[] { c104 }) == OpponentResponse.None);
        m.ResetInstance(c104);
        Check("soft once-per-instance refresh after leaving and returning", m.VisibleResponses(new[] { c104 }) == OpponentResponse.FieldMonster);
        m.Observe(new[] { Link(jokul, 2) }); m.EndChain(); m.ResetInstance(jokul);
        Check("name-turn use survives leaving and returning", Assess(m, jokul).Future == 0);
        Check("unknown copied description is not assigned another effect", m.Identify(new ChainInfo(hollie, 1, 123456)) == null);
        Check("ambiguous default Hollie description is not guessed", m.Identify(new ChainInfo(hollie, 1, 0)) == null);
        m.NewTurn(); m.Observe(new[] { new ChainInfo(hollie, 1, 0) }); m.EndChain();
        Check("ambiguous activity is reported without declaring every effect used", Assess(m, hollie).Effects.Any(t => t.Contains("availability-uncertain")) && Assess(m, hollie).Future > 0);
        Check("location-unique default Izuna description is identified", m.Identify(new ChainInfo(izuna, 1, 0)) != null);
        Check("new duel has independent usage", Assess(new OpponentAwareness(), jokul).Future > 0);
        var unknown = Assess(m, Card(69247929));
        Check("unsupported card has explicit bounded uncertainty", !unknown.Supported && unknown.Future > 0 && unknown.Future < 10);
        var counter = m.Assess(izuna, new[] { izunaLink }, new HashSet<int> { 0 }, false, true);
        Check("already negated chain link contributes no negate value", counter.NegateNow == 0);
        // The real Izuna has NO unused ignition on field after its summon trigger.
        // Do not invent one to force a ranking oracle. Verify that property with
        // an explicitly synthetic reviewed profile on an unrelated card instead.
        var synthetic = new OpponentEffect { CardId = 69247929, Offset = 0, Group = 0, Name = "fixture repeatable search",
            Location = CardLocation.MonsterZone, Window = OpponentWindow.Ignition, Outcome = OpponentOutcome.Search };
        var synthetic2 = new OpponentEffect { CardId = 28642461, Offset = 2, Group = 1, Name = "fixture search",
            Location = CardLocation.MonsterZone, Window = OpponentWindow.Ignition, Outcome = OpponentOutcome.Search };
        var general = new OpponentAwareness(new[] { synthetic, synthetic2 });
        var unrelated = Card(69247929);
        general.Observe(new[] { Link(jokul, 2) }); general.EndChain();
        Check("unused engine effect on unrelated low-ATK card beats a used searcher", Assess(general, unrelated).Removal > Assess(general, jokul).Removal);
        general.Observe(new[] { Link(unrelated, 0) }); general.EndChain(); general.NewTurn();
        general.Observe(new[] { Link(unrelated, 0) }); general.EndChain();
        Check("same generic rule reverses ranking when usage reverses", Assess(general, jokul).Removal > Assess(general, unrelated).Removal);
        var repeat = new OpponentEffect { CardId = 69247929, Offset = 0, Group = 0, Name = "fixture live repeatable resource",
            Location = CardLocation.MonsterZone, Window = OpponentWindow.Ignition, Outcome = OpponentOutcome.Draw, Limit = OpponentLimit.None };
        var repeatModel = new OpponentAwareness(new[] { repeat });
        repeatModel.Observe(new[] { Link(unrelated, 0) }); repeatModel.EndChain();
        Check("repeatable synthetic effect is not made once-per-turn", Assess(repeatModel, unrelated).Future > 0);
    }

    private static void IntegrationChecks()
    {
        var duel = new Duel { Player = 1, Phase = DuelPhase.Main1 };
        duel.Fields[0].Init(0, 0, 0); duel.Fields[1].Init(0, 0, 1);
        duel.Fields[0].SetInitialDeck(new NamedCard[0]);
        var game = new GameClient(new WindBotInfo { Chat = false });
        var ex = new Witchcraft2026Executor(new GameAI(game, duel), duel);
        var verre = Card(21522601, CardLocation.MonsterZone, 0);
        duel.Fields[0].MonsterZone[0] = verre;
        duel.Fields[0].Hand.Add(Card(57916305, CardLocation.Hand, 0));
        duel.Fields[0].Hand.Add(Card(6958567, CardLocation.Hand, 0));
        var hollie = Card(93156774); var spell = Card(80181649, CardLocation.SpellZone);
        duel.Fields[1].MonsterZone[0] = hollie; duel.Fields[1].SpellZone[0] = spell;
        duel.CurrentChainInfo.Add(Link(spell, 0)); duel.LastChainPlayer = 1;
        ex.SetCard(ExecutorType.Activate, verre, 21522601 * 16 + 1);
        Check("113011 real Verre handler holds on Case with only conditional Hollie threat", !(bool)Call(ex, "VerreEffect"));
        duel.CurrentChainInfo.Clear(); duel.CurrentChainInfo.Add(Link(hollie, 1));
        Check("Verre responds to committed live control effect", (bool)Call(ex, "VerreEffect"));
        duel.NegatedChainIndexList.Add(1);
        Check("Verre avoids redundant negate on already negated control", !(bool)Call(ex, "WorthVerre"));
        duel.CurrentChainInfo.Clear(); duel.NegatedChainIndexList.Clear();
        ex.OnNewTurn();
        var izuna = Card(92248362); var jokul = Card(28642461);
        duel.Fields[1].MonsterZone[0] = izuna; duel.Fields[1].MonsterZone[1] = jokul; duel.Fields[1].SpellZone[0] = null;
        duel.CurrentChainInfo.Add(Link(izuna, 2));
        Check("Haine actual target selector denies Jokul search, not Izuna committed send",
            ReferenceEquals(Call(ex, "RemovalTarget", true), jokul));
        var vice = Card(9603252, CardLocation.MonsterZone, 0);
        duel.CurrentChainInfo.Add(Link(vice, 0)); duel.SolvingChainIndex = 2;
        var picked = ex.OnSelectCard(new[] { izuna, jokul }, 1, 1, HintMsg.Destroy, false);
        Check("Vice resolution callback agrees with shared target evaluation", picked.Count == 1 && picked[0] == jokul);
        duel.CurrentChainInfo.Clear(); duel.CurrentChainInfo.Add(Link(jokul, 2));
        duel.SolvingChainIndex = 1; duel.NegatedChainIndexList.Add(1);
        ex.OnChainSolved(1); ex.OnChainEnd();
        duel.CurrentChainInfo.Clear(); duel.NegatedChainIndexList.Clear(); duel.SolvingChainIndex = 0;
        var observed = (OpponentAwareness)Get(ex, "_opponent");
        Check("actual lifecycle observes and retains negated usage across chain end", Assess(observed, jokul).Future == 0 &&
            observed.Usage(observed.Identify(Link(jokul, 2)), jokul, 1).NegatedOrDisabled == 1);
        ex.OnNewTurn();
        Check("actual new-turn callback retires opponent usages", Assess(observed, jokul).Future > 0);
    }

    private static void PlannerChecks()
    {
        Type executor = typeof(Witchcraft2026Executor), stateType = executor.GetNestedType("WState", F), zoneType = executor.GetNestedType("WZone", F);
        Type planner = executor.GetNestedType("WPlanner", F);
        Func<object> empty = () => Activator.CreateInstance(stateType, true);
        Action<object, int, string> add = (s, id, zone) => Call(s, "Add", id, Enum.Parse(zoneType, zone), 1);
        Func<object, IList> actions = s => ((IEnumerable)planner.GetMethod("Actions", F).Invoke(null, new[] { s })).Cast<object>().ToList();
        Func<object, double> value = s => (double)planner.GetMethod("Evaluate", F).Invoke(null, new[] { s });
        object before = empty(); add(before, 21744288, "Board"); add(before, 38943357, "Hand"); add(before, 22283204, "Hand");
        add(before, 7656689, "Deck"); add(before, 22623509, "Deck"); add(before, 84523092, "Deck"); Set(before, "Normal", false);
        var recruits = actions(before).Cast<object>().Where(a => Get(a, "Kind").ToString() == "Recruit" && (int)Get(a, "Target") == 84523092).ToList();
        object spendMastery = recruits.First(a => (int)Get(a, "Cost") == 38943357), spendGaze = recruits.First(a => (int)Get(a, "Cost") == 22283204);
        object bad = planner.GetMethod("Apply", F).Invoke(null, new[] { before, spendMastery });
        object safe = planner.GetMethod("Apply", F).Invoke(null, new[] { before, spendGaze });
        Check("111215 same recruit endpoint preserves independent Mastery instead of stranded Gaze", value(safe) > value(bad), new { preserve = value(safe), spend = value(bad) });
        var failed = executor.GetMethod("DeniedAction", F).Invoke(null, new[] { before, spendGaze });
        Check("negated recruit pays real costs and leaves no imagined boss", (int)Call(failed, "Count", 84523092, Enum.Parse(zoneType, "Board")) == 0 &&
            (int)Call(failed, "Count", 21744288, Enum.Parse(zoneType, "Grave")) == 1 &&
            (int)Call(failed, "Count", 38943357, Enum.Parse(zoneType, "Hand")) == 1);
        Check("interrupted starter replans from surviving independent access", actions(failed).Cast<object>().Any(a => Get(a, "Kind").ToString() == "Mastery"));
        Set(before, "OpposingResponses", OpponentResponse.FieldMonster);
        object exposed = planner.GetMethod("Apply", F).Invoke(null, new[] { before, spendGaze });
        Check("visible field negate reduces combo confidence", value(exposed) < value(safe) && Get(exposed, "DenialFloor") != null);
        Set(before, "OpposingResponses", OpponentResponse.None);
        object restored = planner.GetMethod("Apply", F).Invoke(null, new[] { before, spendGaze });
        Check("observed response spent/removed restores route confidence", value(restored) == value(safe));
        Check("risk exposure does not mutate authoritative input state", Get(before, "DenialFloor") == null);
        object noTargets = empty(); add(noTargets, 38943357, "Hand");
        Check("fallback value requires an actual remaining target", (double)executor.GetMethod("IndependentAccess", F).Invoke(null, new[] { noTargets }) == 0);
        object jacksBefore = empty(); add(jacksBefore, 7656689, "Hand"); add(jacksBefore, 84523092, "Board");
        Set(jacksBefore, "OpposingResponses", OpponentResponse.HandGraveMonster);
        object maid = actions(jacksBefore).Cast<object>().First(a => Get(a, "Kind").ToString() == "MaidSummon");
        object jacksAfter = planner.GetMethod("Apply", F).Invoke(null, new[] { jacksBefore, maid });
        Check("hand-response removal risk preserves actual summon in successful branch", (int)Call(jacksAfter, "Count", 7656689, Enum.Parse(zoneType, "Board")) == 1 && Get(jacksAfter, "DenialFloor") != null);
        object emptyField = empty(); add(emptyField, 7656689, "Hand"); Set(emptyField, "OpposingResponses", OpponentResponse.HandGraveMonster);
        // No valid Maid summon without another caster: do not manufacture an action to test an imaginary target.
        Check("no legal hand summon from empty field is fabricated by resilience", !actions(emptyField).Cast<object>().Any(a => Get(a, "Kind").ToString() == "MaidSummon"));
    }

    public static int Main(string[] args)
    {
        NamedCardsManager.Init(args[0]);
        if (args.Length > 2 && args[2] == "coverage-comparison")
        {
            CoverageComparison(args[1]);
            return 0; // observation receipt, not a PASS/FAIL regression suite
        }
        try { UsageChecks(); IntegrationChecks(); PlannerChecks(); CoverageChecks(); }
        catch (Exception e) { Check("harness exception", false, e.ToString()); }
        File.WriteAllText(args[1], new JavaScriptSerializer().Serialize(Results));
        return Failures == 0 ? 0 : 1;
    }

    private static void CoverageChecks()
    {
        Type executor = typeof(Witchcraft2026Executor), planner = executor.GetNestedType("WPlanner", F);
        Type zone = executor.GetNestedType("WZone", F);
        Func<object, List<object>> actions = s => ((IEnumerable)planner.GetMethod("Actions", F).Invoke(null, new[] { s })).Cast<object>().ToList();
        Func<object, object, object> apply = (s, a) => planner.GetMethod("Apply", F).Invoke(null, new[] { s, a });
        Func<object, object> plan = s => planner.GetMethod("Plan", F).Invoke(null, new object[] { s, null, System.Threading.CancellationToken.None });
        Func<object, int, string, int> count = (s, id, z) => (int)Call(s, "Count", id, Enum.Parse(zone, z));
        Func<object, double> value = s => (double)planner.GetMethod("Evaluate", F).Invoke(null, new[] { s });
        var duel = new Duel { Player = 0, Phase = DuelPhase.Main1, Turn = 2 };
        duel.Fields[0].Init(0, 0, 0); duel.Fields[1].Init(0, 0, 1);
        int[] deck = File.ReadLines("Decks/AI_Witchcraft2026.ydk").TakeWhile(l => l != "#extra")
            .Where(l => l.Length > 0 && char.IsDigit(l[0])).Select(int.Parse).ToArray();
        duel.Fields[0].SetInitialDeck(deck.Select(NamedCard.Get).ToArray());
        foreach (int id in new[] { 58139997, 73664385, 59106048, 83289866, 6958567 }) duel.Fields[0].Hand.Add(Card(id, CardLocation.Hand, 0));
        duel.Fields[0].Graveyard.Add(Card(14558127, CardLocation.Grave, 0)); // used during the opponent's first turn
        foreach (int id in new[] { 9603252, 69964858, 33475154, 37818794, 59400890 }) duel.Fields[0].ExtraDeck.Add(Card(id, CardLocation.Extra, 0));
        var box = Card(2606417, CardLocation.SpellZone); duel.Fields[1].SpellZone[0] = box;
        var game = new GameClient(new WindBotInfo { Chat = false });
        var ex = new Witchcraft2026Executor(new GameAI(game, duel), duel);
        object opening = Call(ex, "Observe");
        Check("232940 actual own first main hand includes Celebration draw and spent Ash", count(opening, 6958567, "Hand") == 1 && count(opening, 14558127, "Grave") == 1 && count(opening, 59106048, "Hand") == 1);
        Check("face-up Box is conditional backrow response, not guaranteed negate", (int)(OpponentResponse)Get(opening, "OpposingResponses") == 8);
        var normal = actions(opening).First(a => Get(a, "Kind").ToString() == "Normal" && (int)Get(a, "Source") == 59106048);
        object summoned = apply(opening, normal);
        var send = actions(summoned).First(a => Get(a, "Kind").ToString() == "LacrimaSend" && (int)Get(a, "Target") == 64756282);
        object resolved = apply(summoned, send);
        Check("Lacrima resolving creates actual Genni access and copied spell continuation", count(resolved, 64756282, "Grave") == 1 && actions(resolved).Any(a => Get(a, "Kind").ToString() == "GenniLacrima"));
        object failed = executor.GetMethod("DeniedAction", F).Invoke(null, new[] { summoned, send });
        Check("Ash-negated Lacrima spends Spell, not the unperformed Deck send", count(failed, 73664385, "Grave") == 1 && count(failed, 64756282, "Grave") == 0 && count(failed, 64756282, "Deck") > 0);
        Check("Ash does not reset Lacrima send limit or manufacture a new recruit window", !actions(failed).Any(a => Get(a, "Kind").ToString().StartsWith("Lacrima")));
        Check("post-Ash includes Bystreet activation and Celebration destruction", actions(failed).Any(a => Get(a, "Kind").ToString() == "ActivateBystreet") && actions(failed).Any(a => Get(a, "Kind").ToString() == "CelebrationDestroy"));
        object recovery = plan(failed);
        var route = ((IEnumerable)Get(recovery, "Steps")).Cast<object>().Select(m => Get(m, "Action")).ToList();
        Check("post-Ash planner plays a beneficial legal modeled continuation, not empty hold", route.Count > 0 && (double)Get(recovery, "Score") > value(failed), route.Select(a => a.ToString()).ToArray());
        var activate = actions(failed).First(a => Get(a, "Kind").ToString() == "ActivateBystreet");
        object protectedState = apply(failed, activate);
        Check("Bystreet activation stays on field and creates no extra body", count(protectedState, 83289866, "Backrow") == 1 && count(protectedState, 83289866, "Grave") == 0 && count(protectedState, 59106048, "Board") == 1);
        var destroy = actions(protectedState).First(a => Get(a, "Kind").ToString() == "CelebrationDestroy");
        object destroyed = apply(protectedState, destroy);
        Check("fresh shield protects Seed while paired destruction removes Box", count(destroyed, 59106048, "Board") == 1 && (OpponentResponse)Get(destroyed, "OpposingResponses") == OpponentResponse.None);
        Check("paired destruction spends Celebration shared Fusion mode", ((HashSet<string>)Get(destroyed, "Used")).Contains("6958567:Fusion") && ((HashSet<string>)Get(destroyed, "Used")).Contains("6958567:CelebrationDestroy"));
        object uncertain = Call(protectedState, "Copy"); ((HashSet<int>)Get(uncertain, "FreshShields")).Clear();
        object unshielded = apply(uncertain, destroy);
        Check("unobserved Bystreet shield is not assumed to save a sacrifice", count(unshielded, 59106048, "Grave") == 1);
        object noEnemy = Call(failed, "Copy"); ((IList)Get(noEnemy, "EnemyTargets")).Clear(); Set(noEnemy, "OpposingResponses", OpponentResponse.None);
        Check("no enemy means no destruction mode, but protection still evaluated", !actions(noEnemy).Any(a => Get(a, "Kind").ToString() == "CelebrationDestroy") && actions(noEnemy).Any(a => Get(a, "Kind").ToString() == "ActivateBystreet"));
        object noBody = Call(noEnemy, "Copy"); Call(noBody, "Move", 59106048, Enum.Parse(zone, "Board"), Enum.Parse(zone, "Grave"));
        Check("empty board does not auto-activate Bystreet", ((IEnumerable)Get(plan(noBody), "Steps")).Cast<object>().Count() == 0);
        object both = Call(failed, "Copy"); Call(both, "Add", 64756282, Enum.Parse(zone, "Grave"), 1); Call(both, "Add", 21744288, Enum.Parse(zone, "Grave"), 1);
        Check("Celebration independently generates Fusion and destruction modes", actions(both).Any(a => Get(a, "Kind").ToString() == "Fusion" && (int)Get(a, "Source") == 6958567) && actions(both).Any(a => Get(a, "Kind").ToString() == "CelebrationDestroy"));
        Check("no materials means Celebration cannot fabricate Fusion", !actions(failed).Any(a => Get(a, "Kind").ToString() == "Fusion"));
        object badTrade = Call(failed, "Copy");
        Call(badTrade, "Move", 83289866, Enum.Parse(zone, "Hand"), Enum.Parse(zone, "Grave"));
        IList weakTargets = (IList)Get(badTrade, "EnemyTargets"); weakTargets.Clear();
        object weak = Activator.CreateInstance(executor.GetNestedType("WRemovalTarget", F), true);
        Set(weak, "Key", 129); Set(weak, "Id", 2606417); Set(weak, "Value", 1.0); weakTargets.Add(weak);
        Set(badTrade, "OpposingResponses", OpponentResponse.None);
        Check("legal negative-value destruction is compared and rejected, not forced", actions(badTrade).Any(a => Get(a, "Kind").ToString() == "CelebrationDestroy") &&
            ((IEnumerable)Get(plan(badTrade), "Steps")).Cast<object>().Count() == 0);
        Set(ex, "_accepted", destroy);
        Check("destruction option follows actual offered option index", ex.OnSelectOption(new[] { 6958567 * 16 + 3, 6958567 * 16 + 2 }) == 1);

        // Real executor observation/offer boundary following the failed starter.
        var seed = duel.Fields[0].Hand.First(c => c.Id == 59106048); duel.Fields[0].Hand.Remove(seed); seed.Location = CardLocation.MonsterZone; duel.Fields[0].MonsterZone[0] = seed;
        var lacrima = duel.Fields[0].Hand.First(c => c.Id == 73664385); duel.Fields[0].Hand.Remove(lacrima); lacrima.Location = CardLocation.Grave; duel.Fields[0].Graveyard.Add(lacrima);
        Set(ex, "_normalUsed", true); ((HashSet<string>)Get(ex, "_used")).Add("73664385:LacrimaSend");
        var bystreet = duel.Fields[0].Hand.First(c => c.Id == 83289866); var celebration = duel.Fields[0].Hand.First(c => c.Id == 6958567);
        duel.MainPhase = new MainPhase(); duel.MainPhase.ActivableCards.Add(bystreet); duel.MainPhase.ActivableDescs.Add(0);
        duel.MainPhase.ActivableCards.Add(celebration); duel.MainPhase.ActivableDescs.Add(6958567 * 16);
        ex.SetCard(ExecutorType.Activate, bystreet, 0);
        Check("actual idle handler accepts represented Bystreet action after Ash", (bool)Call(ex, "PlannedAction"));
        object observed = Call(ex, "Observe"); object reportPlan = plan(observed);
        string audit = (string)Call(ex, "ActionCoverageAudit", observed, reportPlan);
        Check("coverage audit separates generated, offered and evaluated choices", audit.Contains("modeled=") && audit.Contains("CelebrationDestroy") && audit.Contains("unavailable-model=") && !audit.Contains("MISSING"), audit);
        ex.OnSelectChain(new[] { bystreet });
        Check("chain-window audit does not mislabel stale idle offers", ((string)Call(ex, "ActionCoverageAudit", observed, reportPlan)).Contains("idle-list-not-current"));
        var model = new OpponentAwareness(); box.Position = (int)CardPosition.FaceDownDefence;
        Check("facedown Box identity is not used for visible response", model.VisibleResponses(new[] { box }) == OpponentResponse.None);
        box.Position = (int)CardPosition.FaceUpAttack;
        object lowState = Call(failed, "Copy"); Call(lowState, "Add", 21744288, Enum.Parse(zone, "Board"), 1);
        var recruit = actions(lowState).First(a => Get(a, "Kind").ToString() == "Recruit");
        Check("Box cannot target a recruiting monster that tributes itself as cost", Get(apply(lowState, recruit), "DenialFloor") == null);
        var twoModes = actions(both).First(a => Get(a, "Kind").ToString() == "Fusion" && (int)Get(a, "Source") == 6958567);
        object fused = apply(both, twoModes);
        Check("Fusion also consumes Celebration destruction mode", !actions(fused).Any(a => Get(a, "Kind").ToString() == "CelebrationDestroy"));
        object exposed = Call(both, "Copy"); Call(exposed, "Add", 7656689, Enum.Parse(zone, "Board"), 1);
        var exchange = actions(exposed).First(a => Get(a, "Kind").ToString() == "MaidExchange" && (int)Get(a, "Target") == 84523092);
        object interrupted = apply(exposed, exchange);
        Check("surviving field monster effect is exposed to conditional Box disable", Get(interrupted, "DenialFloor") != null && Get(interrupted, "Vulnerability").ToString().Contains("conditional-Box"));
        Set(exposed, "OpposingResponses", OpponentResponse.None);
        Check("absent Box restores uninterrupted valuation without inventing another threat", Get(apply(exposed, exchange), "DenialFloor") == null && value(apply(exposed, exchange)) > value(interrupted));
        var deniedPair = executor.GetMethod("DeniedAction", F).Invoke(null, new[] { protectedState, destroy });
        Check("negated Celebration retains both targets but spends shared activation", count(deniedPair, 59106048, "Board") == 1 && ((IList)Get(deniedPair, "EnemyTargets")).Count == 1 && ((HashSet<string>)Get(deniedPair, "Used")).Contains("6958567:Fusion"));
        object disabledResponse = Call(protectedState, "Copy");
        object spentEnemy = Activator.CreateInstance(executor.GetNestedType("WRemovalTarget", F), true);
        Set(spentEnemy, "Key", 65); Set(spentEnemy, "Id", 49456901); Set(spentEnemy, "Value", 3.0);
        Set(spentEnemy, "Responses", OpponentResponse.FieldMonster);
        ((IList)Get(disabledResponse, "EnemyTargets")).Add(spentEnemy); Set(disabledResponse, "OpposingResponses", (OpponentResponse)8);
        Check("removing Box does not resurrect a previously disabled monster response", (OpponentResponse)Get(apply(disabledResponse, destroy), "OpposingResponses") == OpponentResponse.None);
        object fullBackrow = Call(failed, "Copy"); Set(fullBackrow, "OccupiedBackrow", 5);
        Check("full spell zones cannot generate either new hand activation", !actions(fullBackrow).Any(a => Get(a, "Kind").ToString() == "ActivateBystreet" || Get(a, "Kind").ToString() == "CelebrationDestroy"));
        duel.CurrentChainInfo.Clear(); duel.CurrentChain.Clear();
        duel.Fields[0].Hand.Remove(bystreet); bystreet.Location = CardLocation.SpellZone; duel.Fields[0].SpellZone[0] = bystreet;
        duel.CurrentChainInfo.Add(Link(bystreet, 0)); duel.SolvingChainIndex = 1;
        ex.OnChainSolved(1);
        Check("actual successful Bystreet resolution establishes shield evidence", ((HashSet<int>)Get(Call(ex, "Observe"), "FreshShields")).Contains(59106048));
        duel.Phase = DuelPhase.BattleStart; ex.OnNewPhase();
        Check("entering battle retires unreported shield-count certainty", ((HashSet<int>)Get(Call(ex, "Observe"), "FreshShields")).Count == 0);
        duel.Phase = DuelPhase.End;
        ex.SetCard(ExecutorType.Activate, Card(83289866, CardLocation.Grave, 0), 83289866 * 16);
        Check("Bystreet actual End Phase handler accepts legal GY recovery", (bool)Call(ex, "EndPhaseRecovery"));
        duel.Fields[0].MonsterZone[0] = null;
        Check("Bystreet recovery handler does not invent a required Witchcrafter", !(bool)Call(ex, "EndPhaseRecovery"));
    }

    private static void CoverageComparison(string output)
    {
        // Same public post-Ash state against either preserved runtime. No expected
        // winner encoded: record the actual generator and executor's shared planner.
        var duel = new Duel { Player = 0, Phase = DuelPhase.Main1, Turn = 2 };
        duel.Fields[0].Init(0, 0, 0); duel.Fields[1].Init(0, 0, 1);
        duel.Fields[0].SetInitialDeck(new NamedCard[0]);
        foreach (int id in new[] { 58139997, 83289866, 6958567 }) duel.Fields[0].Hand.Add(Card(id, CardLocation.Hand, 0));
        duel.Fields[0].MonsterZone[0] = Card(59106048, CardLocation.MonsterZone, 0);
        duel.Fields[0].Graveyard.Add(Card(73664385, CardLocation.Grave, 0));
        duel.Fields[0].Graveyard.Add(Card(14558127, CardLocation.Grave, 0));
        duel.Fields[1].SpellZone[0] = Card(2606417, CardLocation.SpellZone);
        var game = new GameClient(new WindBotInfo { Chat = false });
        var ex = new Witchcraft2026Executor(new GameAI(game, duel), duel);
        Set(ex, "_normalUsed", true); ((HashSet<string>)Get(ex, "_used")).Add("73664385:LacrimaSend");
        object state = Call(ex, "Observe");
        Type planner = typeof(Witchcraft2026Executor).GetNestedType("WPlanner", F);
        object result = planner.GetMethod("Plan", F).Invoke(null, new object[] { state, null, System.Threading.CancellationToken.None });
        File.WriteAllText(output, new JavaScriptSerializer().Serialize(new {
            purpose = "before/after post-Ash public subset, no Deck/Extra continuations; observation not regression pass",
            actions = ((IEnumerable)planner.GetMethod("Actions", F).Invoke(null, new[] { state })).Cast<object>().Select(a => a.ToString()).ToArray(),
            hold = planner.GetMethod("Evaluate", F).Invoke(null, new[] { state }),
            best = Get(result, "Score"), nodes = Get(result, "Expanded"),
            route = ((IEnumerable)Get(result, "Steps")).Cast<object>().Select(m => Get(m, "Action").ToString()).ToArray()
        }));
    }
}
