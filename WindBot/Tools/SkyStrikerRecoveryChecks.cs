using System;
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

// Reconstructed observed-state/dispatcher fixtures, NOT an ocgcore replay.
// Actual Lua-backed companion cases are run separately. No unknown hand or
// facedown identities from the supplied logs are populated in these fixtures.
internal static class SkyStrikerRecoveryChecks
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static |
        BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly List<object> Results = new List<object>();
    private static int Failures;
    private sealed class Fixture
    {
        public Duel Duel = new Duel();
        public GameAI AI;
        public SkyStriker2026Executor Bot;
        public Fixture(int turn = 15, int player = 1)
        {
            for (int i = 0; i < 2; i++)
            {
                Duel.Fields[i].Init(0, 0, i);
                Duel.Fields[i].LifePoints = 8000;
                Duel.Fields[i].SetInitialDeck(new List<NamedCard>());
            }
            Duel.Turn = turn;
            Duel.Player = player;
            Duel.Phase = DuelPhase.Main1;
            AI = new GameAI(new GameClient(new WindBotInfo { Name = "RecoveryQA",
                Dialog = "default", Chat = false }), Duel);
            Bot = new SkyStriker2026Executor(AI, Duel);
            AI.Executor = Bot;
        }
        public ClientCard Add(int id, int player, CardLocation location, int zone = 0)
        {
            var c = new ClientCard(id, location, zone, (int)CardPosition.FaceUpAttack);
            c.Controller = player;
            foreach (string p in new[] { "Type", "Attack", "Defense", "Attribute", "Race", "Level" })
                typeof(ClientCard).GetProperty(p).SetValue(c,
                    c.Data.GetType().GetProperty(p).GetValue(c.Data, null), null);
            Set(c, "LinkCount", c.HasType(CardType.Link) ? c.Level : 0);
            Set(c, "ProcCompleted", 1);
            var field = Duel.Fields[player];
            if (location == CardLocation.MonsterZone) field.MonsterZone[zone] = c;
            else if (location == CardLocation.SpellZone) field.SpellZone[zone] = c;
            else if (location == CardLocation.Hand) field.Hand.Add(c);
            else if (location == CardLocation.Grave) field.Graveyard.Add(c);
            else if (location == CardLocation.Extra) field.ExtraDeck.Add(c);
            return c;
        }
        public ChainInfo Chain(ClientCard c, int offset, params ClientCard[] targets)
        {
            var info = new ChainInfo(c, c.Controller, c.Id * 16 + offset);
            foreach (var t in targets) info.Targets.Add(t);
            Duel.CurrentChain.Add(c);
            Duel.CurrentChainInfo.Add(info);
            Duel.LastChainPlayer = c.Controller;
            return info;
        }
        public int Offer(params ClientCard[] cards)
        {
            return AI.OnSelectChain(cards, cards.Select(c => c.Id * 16).ToList(),
                cards.Select(c => false).ToList());
        }
        public bool Handler(string method, ClientCard c, int offset = 0)
        {
            Bot.SetCard(ExecutorType.Activate, c, c.Id * 16 + offset);
            return (bool)Call(Bot, method);
        }
    }
    private static object Call(object o, string method, params object[] args)
    {
        return o.GetType().GetMethod(method, Flags).Invoke(o, args);
    }
    private static void Set(object o, string name, object value)
    {
        o.GetType().GetProperty(name, Flags).SetValue(o, value, null);
    }
    private static void Check(string name, string evidence, string expected, Func<object> run)
    {
        object actual;
        try { actual = run(); }
        catch (Exception e) { actual = "ERROR: " + e.GetBaseException().Message; }
        bool pass = Convert.ToString(actual) == expected;
        if (!pass) Failures++;
        Results.Add(new { name, evidence, expected, actual, passed = pass });
        Console.WriteLine((pass ? "PASS " : "FAIL ") + name + " actual=" + actual);
    }
    private static Fixture Ultimate(int id, int offset, out ClientCard sky)
    {
        var f = new Fixture();
        var own = f.Add(26077387, 0, CardLocation.MonsterZone, 0);
        var monster = f.Add(id, 1, CardLocation.MonsterZone, 0);
        f.Add(80280944, 1, CardLocation.MonsterZone, 1); // observed Giant Rex
        sky = f.Add(38817295, 0, CardLocation.SpellZone);
        sky.Position = (int)CardPosition.FaceDownDefence;
        var target = id == 95503687 ? f.Add(77558536, 1, CardLocation.Grave) : own;
        f.Chain(monster, offset, target);
        return f;
    }
    private static void Run()
    {
        Check("Duster: salvage Shark by banishing public Snow", "232745 turn 5; offers reconstructed; Snow public in GY", "0", () =>
        {
            var f = new Fixture(5);
            var shark = f.Add(51227866, 0, CardLocation.SpellZone);
            shark.Position = (int)CardPosition.FaceDownDefence;
            f.Add(55623480, 1, CardLocation.Grave);
            f.Chain(f.Add(18144506, 1, CardLocation.SpellZone), 0);
            return f.Offer(shark);
        });
        Check("Duster: no monster means no profitable Crown or Widow", "232745 turn 5 empty enemy MZ; hypothetical offers are negative robustness only", "-1", () =>
        {
            var f = new Fixture(5);
            f.Chain(f.Add(18144506, 1, CardLocation.SpellZone), 0);
            return f.Offer(f.Add(98829635, 0, CardLocation.SpellZone, 0),
                f.Add(98338152, 0, CardLocation.SpellZone, 1));
        });
        Check("Lumina revival: Ultimate field mode chains before extension", "232745 turn 15 Main1; matching script offset 0", "0", () =>
        {
            ClientCard sky; var f = Ultimate(95503687, 0, out sky);
            return f.Offer(sky);
        });
        Check("Michael banish: protect own MZ body immediately", "232745 turn 15 target Raye; matching script offset 0", "0", () =>
        {
            ClientCard sky; var f = Ultimate(4779823, 0, out sky);
            return f.Offer(sky);
        });
        Check("Lumina end mill is not its revival", "negative variant: offset 1 End Phase, no target", "-1", () =>
        {
            ClientCard sky; var f = Ultimate(95503687, 1, out sky);
            f.Duel.CurrentChainInfo.Last().Targets.Clear();
            f.Duel.Phase = DuelPhase.End;
            return f.Offer(sky);
        });
        Check("Unavailable Crown cannot veto an offered Widow", "dispatcher negative: Crown exists but is absent from legal offers", "0", () =>
        {
            var f = new Fixture();
            var raiden = f.Add(77558536, 1, CardLocation.MonsterZone);
            f.Chain(raiden, 0);
            f.Add(98829635, 0, CardLocation.SpellZone, 0);
            var widow = f.Add(98338152, 0, CardLocation.SpellZone, 1);
            return f.Offer(widow);
        });
        Check("Inconclusive plan is not a veto over Engage", "232745 repeated considered=4; projection cannot price missing targets", "True", () =>
        {
            var f = new Fixture(14, 0);
            f.Add(63288573, 0, CardLocation.MonsterZone, 5);
            var engage = f.Add(63166095, 0, CardLocation.Hand);
            f.Add(98338152, 0, CardLocation.Hand);
            f.Add(77558536, 1, CardLocation.MonsterZone);
            f.Duel.Fields[0].SetInitialDeck(new List<NamedCard> { NamedCard.Get(9726840) });
            f.Duel.MainPhase.ActivableCards.Add(engage);
            return f.Handler("EngageEffect", engage);
        });
        Check("Battle lethal preserves Upstart and Engage", "controlled variant: 2000 attacker vs 1500 LP, empty board", "False/False", () =>
        {
            var f = new Fixture(14, 0);
            f.Duel.Fields[1].LifePoints = 1500;
            var zero = f.Add(76072561, 0, CardLocation.MonsterZone, 5);
            Set(zero, "Attack", 2000);
            var upstart = f.Add(70368879, 0, CardLocation.Hand);
            var engage = f.Add(63166095, 0, CardLocation.Hand);
            return f.Handler("UpstartGoblinEffect", upstart) + "/" +
                f.Bot.OnPreActivate(engage);
        });
        Check("Unknown defense does not manufacture proven lethal", "negative variant: unknown facedown monster", "True", () =>
        {
            var f = new Fixture(14, 0);
            f.Duel.Fields[1].LifePoints = 1500;
            f.Add(76072561, 0, CardLocation.MonsterZone, 5);
            f.Duel.Fields[1].MonsterZone[0] = new ClientCard(0, CardLocation.MonsterZone, 0,
                (int)CardPosition.FaceDownDefence) { Controller = 1 };
            return f.Bot.OnPreActivate(f.Add(63166095, 0, CardLocation.Hand));
        });
        Check("Two attackers prefer battle over a draw", "controlled supported line: Raye + Roze = 3000 vs 2500 LP", "ToBattlePhase", () =>
        {
            var f = new Fixture(14, 0);
            f.Duel.Fields[1].LifePoints = 2500;
            f.Add(26077387, 0, CardLocation.MonsterZone, 0);
            f.Add(37351133, 0, CardLocation.MonsterZone, 1);
            var draw = f.Add(70368879, 0, CardLocation.Hand);
            draw.ActionActivateIndex[draw.Id * 16] = 0;
            f.Duel.MainPhase.ActivableCards.Add(draw);
            f.Duel.MainPhase.ActivableDescs.Add(draw.Id * 16);
            f.Duel.MainPhase.CanBattlePhase = true;
            return f.AI.OnSelectIdleCmd(f.Duel.MainPhase).Action;
        });
        Check("Known blocker plus remaining attacker is lethal", "controlled supported line: 3000 destroys 2500 normal Fusion then 1500 direct vs 2000 LP", "False", () =>
        {
            var f = new Fixture(14, 0);
            f.Duel.Fields[1].LifePoints = 2000;
            var attacker = f.Add(63288573, 0, CardLocation.MonsterZone, 5);
            Set(attacker, "Attack", 3000);
            f.Add(26077387, 0, CardLocation.MonsterZone, 0);
            var defender = f.Add(41462083, 1, CardLocation.MonsterZone);
            Set(defender, "Attack", 2500);
            return f.Handler("UpstartGoblinEffect", f.Add(70368879, 0, CardLocation.Hand));
        });
        Check("Lemnisgate bounce defers to free safe battle", "controlled variant: 3000 Kagari vs 2500 normal Fusion, no enemy backrow", "False", () =>
        {
            var f = new Fixture(14, 0);
            var kagari = f.Add(63288573, 0, CardLocation.MonsterZone, 5);
            Set(kagari, "Attack", 3000);
            f.Add(41462083, 1, CardLocation.MonsterZone); // Thousand Dragon, normal Fusion
            foreach (int id in new[] { 98338152, 51227866, 76072561, 90673288 })
                f.Add(id, 0, CardLocation.Grave);
            return f.Handler("LemnisgateEffect", f.Add(34433770, 0, CardLocation.Hand));
        });
        Check("Lemnisgate GY cannot replace a fresh attacking Kagari for free value", "negative variant: opponent battle, no endangered material; Blue-Eyes weaker than Kagari", "False", () =>
        {
            var f = new Fixture(15);
            f.Duel.Phase = DuelPhase.BattleStart;
            var kagari = f.Add(63288573, 0, CardLocation.MonsterZone, 5);
            Set(kagari, "Attack", 4000);
            f.Add(89631139, 1, CardLocation.MonsterZone);
            f.Add(12421694, 0, CardLocation.Extra);
            return f.Handler("LemnisgateEffect", f.Add(34433770, 0, CardLocation.Grave));
        });
        Check("Lemnisgate GY keeps normal Main Phase link free", "matches existing native lemn-grave fixture", "False", () =>
        {
            var f = new Fixture(14, 0);
            f.Add(52340445, 0, CardLocation.MonsterZone);
            f.Add(63288573, 0, CardLocation.Extra);
            f.Add(63166095, 0, CardLocation.Grave);
            return f.Handler("LemnisgateEffect", f.Add(34433770, 0, CardLocation.Grave));
        });
        Check("Negated monster is not assumed harmless after destruction", "negative: disabled Shaddoll still has unmodeled GY trigger", "True", () =>
        {
            var f = new Fixture(14, 0);
            f.Duel.Fields[1].LifePoints = 100;
            var kagari = f.Add(63288573, 0, CardLocation.MonsterZone, 5);
            Set(kagari, "Attack", 4000);
            var construct = f.Add(20366274, 1, CardLocation.MonsterZone);
            Set(construct, "Disabled", 1);
            return f.Bot.OnPreActivate(f.Add(63166095, 0, CardLocation.Hand));
        });
        Check("Freshly set Linkage is not future battle fuel", "negative: only lethal with a Quick-Play set this turn", "True", () =>
        {
            var f = new Fixture(14, 0);
            f.Duel.Fields[1].LifePoints = 7000;
            f.Add(8491308, 0, CardLocation.MonsterZone, 5);
            f.Add(63288573, 0, CardLocation.Extra);
            f.Add(76072561, 0, CardLocation.Extra);
            f.Add(26077387, 0, CardLocation.Grave);
            f.Add(37351133, 0, CardLocation.Grave);
            var linkage = f.Add(9726840, 0, CardLocation.SpellZone);
            linkage.Position = (int)CardPosition.FaceDownDefence;
            f.Bot.OnMove(linkage, 0, (int)CardLocation.Hand, 0, (int)CardLocation.SpellZone);
            return f.Bot.OnPreActivate(f.Add(63166095, 0, CardLocation.Hand));
        });
        Check("Lemnisgate GY accepts a real defensive timing gain", "positive: opponent Battle; weaker Shizuku converts to legal Kaina", "True", () =>
        {
            var f = new Fixture(15);
            f.Duel.Phase = DuelPhase.BattleStart;
            f.Add(90673288, 0, CardLocation.MonsterZone, 5);
            f.Add(89631139, 1, CardLocation.MonsterZone);
            f.Add(12421694, 0, CardLocation.Extra);
            return f.Handler("LemnisgateEffect", f.Add(34433770, 0, CardLocation.Grave));
        });
        Check("Lemnisgate keeps free battle plan after entering Battle", "native fixture found Main1 hold could become wasteful BattleStart activation", "False", () =>
        {
            var f = new Fixture(3, 0);
            f.Duel.Phase = DuelPhase.BattleStart;
            f.Add(89631139, 0, CardLocation.MonsterZone);
            f.Add(41462083, 1, CardLocation.MonsterZone);
            foreach (int id in new[] { 98338152, 51227866, 76072561, 90673288 })
                f.Add(id, 0, CardLocation.Grave);
            return f.Handler("LemnisgateEffect", f.Add(34433770, 0, CardLocation.Hand));
        });
    }
    public static int Main(string[] args)
    {
        NamedCardsManager.Init(Path.GetFullPath(args[0]));
        Run();
        File.WriteAllText(args[1], new JavaScriptSerializer().Serialize(new {
            kind = "observed-state-dispatcher; not engine legality", checks = Results,
            passed = Failures == 0, failures = Failures }));
        return Failures == 0 ? 0 : 1;
    }
}
