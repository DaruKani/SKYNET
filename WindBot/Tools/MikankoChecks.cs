using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Web.Script.Serialization;
using WindBot;
using WindBot.Game;
using WindBot.Game.AI;
using YGOSharp.OCGWrapper;
using YGOSharp.OCGWrapper.Enums;

// Synthetic legal offers exercise the real dispatcher and Mikanko prompt callbacks.
// These checks do not execute Lua or establish competitive duel strength.
internal static class MikankoChecks
{
    private const int Nibiru = 27204311, Jizukiru = 63941210, ThunderKing = 48770333;
    private const int LavaGolem = 102380, Sanaki = 11161666, HaRe = 18377261;
    private const int HuLi = 6327734, Ohime = 81260679, GreatCeremony = 44649322;
    private const int NobleTwins = 84550369, WaterArabesque = 43527730;
    private const int DoubleEdgedSword = 41927278, FireDance = 80044027;
    private const int Rivalry = 78199891, Uzuhime = 57566760;
    private const int BlueEyes = 89631139, Baronne = 84815190;
    private static readonly Type ExecutorClass = typeof(Executor).Assembly.GetType("WindBot.Game.AI.Decks.MikankoExecutor");
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    private static readonly List<Dictionary<string, object>> Results = new List<Dictionary<string, object>>();
    private static readonly List<int> MainDeck = new List<int>();
    private static readonly List<int> ExtraDeck = new List<int>();

    private sealed class Fixture
    {
        internal readonly Duel Duel = new Duel();
        internal readonly GameAI AI;
        internal readonly Executor Executor;
        internal ClientField Bot { get { return Duel.Fields[0]; } }

        internal Fixture()
        {
            Duel.Fields[0].Init(MainDeck.Count, 0, 0);
            Duel.Fields[1].Init(40, 0, 1);
            Duel.Fields[0].LifePoints = 8000;
            Duel.Fields[1].LifePoints = 8000;
            Bot.SetInitialDeck(MainDeck.Select(NamedCard.Get));
            foreach (int id in ExtraDeck)
                Bot.ExtraDeck.Add(Card(id, CardLocation.Extra));
            Duel.Turn = 1;
            Duel.Player = 0;
            Duel.Phase = DuelPhase.Main1;
            Duel.IsNewRule = true;
            Duel.IsNewRule2020 = true;
            GameClient client = new GameClient(new WindBotInfo { Chat = false, Deck = "Mikanko", Dialog = "default" });
            AI = new GameAI(client, Duel);
            Executor = (Executor)Activator.CreateInstance(ExecutorClass, AI, Duel);
            AI.Executor = Executor;
            Executor.Deck = "AI_Mikanko";
            AI.OnNewTurn();
            AI.OnNewPhase();
        }

        internal ClientCard Add(int id, CardLocation location, int controller = 0)
        {
            ClientCard card = Card(id, location);
            card.Controller = controller;
            ClientField field = Duel.Fields[controller];
            if (location == CardLocation.Hand)
                field.Hand.Add(card);
            else if (location == CardLocation.Grave)
                field.Graveyard.Add(card);
            else if (location == CardLocation.MonsterZone)
            {
                int slot = Array.FindIndex(field.MonsterZone, value => value == null);
                card.Sequence = slot;
                field.MonsterZone[slot] = card;
            }
            else if (location == CardLocation.SpellZone)
            {
                int slot = Array.FindIndex(field.SpellZone, value => value == null);
                card.Sequence = slot;
                field.SpellZone[slot] = card;
            }
            if (controller == 0 && MainDeck.Contains(id))
            {
                typeof(ClientField).GetMethod("TrackRemoveFromDeck", Flags).Invoke(Bot, new object[] { id });
                if (Bot.Deck.Count > 0)
                    Bot.Deck.RemoveAt(Bot.Deck.Count - 1);
            }
            return card;
        }

        internal void Resolve(ClientCard source)
        {
            Duel.CurrentChain.Clear();
            Duel.CurrentChainInfo.Clear();
            Duel.CurrentChain.Add(source);
            Duel.CurrentChainInfo.Add(new ChainInfo(source, source.Controller, 0));
            Duel.LastChainPlayer = source.Controller;
            Duel.SolvingChainIndex = 1;
        }

        internal IList<ClientCard> Select(int hint, int min, int max, params ClientCard[] offered)
        {
            IList<ClientCard> result = AI.OnSelectCard(offered, min, max, hint, false);
            Require(result != null && result.Count >= min && result.Count <= max,
                "Selection count was outside the server bounds");
            Require(result.All(offered.Contains), "Selection fabricated a card outside the offer");
            return result;
        }

        internal MainPhaseAction OfferSpecial(ClientCard card)
        {
            MainPhase main = new MainPhase { CanEndPhase = true, CanBattlePhase = false };
            main.SpecialSummonableCards.Add(card);
            Duel.MainPhase = main;
            return AI.OnSelectIdleCmd(main);
        }
    }

    private static ClientCard Card(int id, CardLocation location)
    {
        ClientCard card = new ClientCard(id, location, 0, (int)CardPosition.FaceUpAttack) { Controller = 0 };
        foreach (string property in new[] { "Type", "Level", "Race", "Attribute", "Attack", "Defense" })
            typeof(ClientCard).GetProperty(property).SetValue(card,
                card.Data.GetType().GetProperty(property).GetValue(card.Data, null), null);
        Set(card, "BaseAttack", card.Data.Attack);
        return card;
    }

    private static void Set(ClientCard card, string property, int value)
    {
        typeof(ClientCard).GetProperty(property).SetValue(card, value, null);
    }

    private static void Require(bool condition, string reason)
    {
        if (!condition)
            throw new InvalidOperationException(reason);
    }

    private static void IsCard(IList<ClientCard> selected, int id)
    {
        Require(selected.Count == 1 && selected[0].Id == id,
            "Expected " + id + "; got " + string.Join(",", selected.Select(c => c.Id)));
    }

    private static void Run(string name, Action test)
    {
        Stopwatch watch = Stopwatch.StartNew();
        bool passed = false;
        string detail = "passed";
        try
        {
            test();
            passed = true;
        }
        catch (Exception error)
        {
            detail = (error.InnerException ?? error).Message;
        }
        Results.Add(new Dictionary<string, object>
        {
            { "name", name }, { "passed", passed },
            { "elapsedMs", watch.ElapsedMilliseconds }, { "detail", detail }
        });
        Console.WriteLine((passed ? "PASS " : "FAIL ") + name + ": " + detail);
    }

    private static void Effects()
    {
        Run("Mikanko always selects the second turn", () =>
        {
            Fixture f = new Fixture();
            Require(!f.Executor.OnSelectHand(), "Executor selected the first turn");
        });
        Run("Ohime searches Water Arabesque when the route is missing", () =>
        {
            Fixture f = new Fixture();
            f.Resolve(f.Add(Ohime, CardLocation.Hand));
            IsCard(f.Select(HintMsg.AddToHand, 1, 1,
                Card(FireDance, CardLocation.Deck), Card(WaterArabesque, CardLocation.Deck)), WaterArabesque);
        });
        Run("Ohime discards Great Ceremony for graveyard value", () =>
        {
            Fixture f = new Fixture();
            f.Resolve(f.Add(Ohime, CardLocation.Hand));
            IsCard(f.Select(HintMsg.Discard, 1, 1,
                Card(WaterArabesque, CardLocation.Hand), Card(GreatCeremony, CardLocation.Hand)), GreatCeremony);
        });
        Run("Noble Twins sends Great Ceremony", () =>
        {
            Fixture f = new Fixture();
            f.Resolve(f.Add(NobleTwins, CardLocation.SpellZone));
            IsCard(f.Select(HintMsg.ToGrave, 1, 1,
                Card(FireDance, CardLocation.Deck), Card(GreatCeremony, CardLocation.Deck)), GreatCeremony);
        });
        Run("Noble Twins sets Great Ceremony when Ohime is ready", () =>
        {
            Fixture f = new Fixture();
            f.Add(Ohime, CardLocation.Hand);
            f.Resolve(f.Add(NobleTwins, CardLocation.SpellZone));
            IsCard(f.Select(HintMsg.Set, 1, 1,
                Card(Rivalry, CardLocation.Deck), Card(GreatCeremony, CardLocation.Deck)), GreatCeremony);
        });
        Run("Water Arabesque recruits Ha-Re to find the missing Equip Spell", () =>
        {
            Fixture f = new Fixture();
            f.Resolve(f.Add(WaterArabesque, CardLocation.SpellZone));
            IsCard(f.Select(HintMsg.SpSummon, 1, 1,
                Card(Sanaki, CardLocation.Deck), Card(HaRe, CardLocation.Deck), Card(HuLi, CardLocation.Deck)), HaRe);
        });
        Run("Sanaki is not treated as a battle-damage reflector", () =>
        {
            Fixture f = new Fixture();
            ClientCard sanaki = f.Add(Sanaki, CardLocation.MonsterZone);
            ClientCard enemy = f.Add(BlueEyes, CardLocation.MonsterZone, 1);
            Require(f.Executor.OnBattle(new[] { sanaki }, new[] { enemy }) == null,
                "Sanaki attempted a reflection attack");
        });
        Run("Ohime is an unconditional battle-damage reflector", () =>
        {
            Fixture f = new Fixture();
            ClientCard ohime = f.Add(Ohime, CardLocation.MonsterZone);
            ClientCard enemy = f.Add(BlueEyes, CardLocation.MonsterZone, 1);
            BattlePhaseAction action = f.Executor.OnBattle(new[] { ohime }, new[] { enemy });
            Require(action != null && action.Action == BattlePhaseAction.BattleAction.Attack,
                "Ohime did not attack the reflected-damage target");
        });
        Run("Ha-Re reflects only while equipped", () =>
        {
            Fixture f = new Fixture();
            ClientCard hare = f.Add(HaRe, CardLocation.MonsterZone);
            ClientCard enemy = f.Add(BlueEyes, CardLocation.MonsterZone, 1);
            Require(f.Executor.OnBattle(new[] { hare }, new[] { enemy }) == null,
                "Unequipped Ha-Re attempted a reflection attack");
            ClientCard equip = f.Add(DoubleEdgedSword, CardLocation.SpellZone);
            equip.SetEquipTarget(hare);
            BattlePhaseAction action = f.Executor.OnBattle(new[] { hare }, new[] { enemy });
            Require(action != null && action.Action == BattlePhaseAction.BattleAction.Attack,
                "Equipped Ha-Re failed to attack");
        });
        Run("Double-Edged Sword targets the strongest opponent monster", () =>
        {
            Fixture f = new Fixture();
            f.Resolve(f.Add(DoubleEdgedSword, CardLocation.Hand));
            ClientCard low = f.Add(HuLi, CardLocation.MonsterZone, 1);
            ClientCard high = f.Add(BlueEyes, CardLocation.MonsterZone, 1);
            IsCard(f.Select(HintMsg.Target, 1, 1, low, high), BlueEyes);
        });
        Run("A second Kaiju is rejected while an OTK Kaiju target remains", () =>
        {
            Fixture f = new Fixture();
            f.Add(Jizukiru, CardLocation.MonsterZone, 1);
            f.Add(Baronne, CardLocation.MonsterZone, 1);
            MainPhaseAction action = f.OfferSpecial(f.Add(ThunderKing, CardLocation.Hand));
            Require(action.Action != MainPhaseAction.MainAction.SpSummon,
                "Executor attempted to replace its existing Kaiju target");
        });
        Run("Lava Golem preserves an existing Kaiju when only one other monster remains", () =>
        {
            Fixture f = new Fixture();
            f.Add(Jizukiru, CardLocation.MonsterZone, 1);
            f.Add(Baronne, CardLocation.MonsterZone, 1);
            MainPhaseAction action = f.OfferSpecial(f.Add(LavaGolem, CardLocation.Hand));
            Require(action.Action != MainPhaseAction.MainAction.SpSummon,
                "Executor attempted to tribute its existing Kaiju target");
        });
        Run("Lava Golem release prompt consumes non-Kaiju threats first", () =>
        {
            Fixture f = new Fixture();
            ClientCard lava = f.Add(LavaGolem, CardLocation.Hand);
            f.Resolve(lava);
            ClientCard kaiju = f.Add(Jizukiru, CardLocation.MonsterZone, 1);
            ClientCard first = f.Add(Baronne, CardLocation.MonsterZone, 1);
            ClientCard second = f.Add(BlueEyes, CardLocation.MonsterZone, 1);
            IList<ClientCard> selected = f.Select(HintMsg.Release, 2, 2, kaiju, first, second);
            Require(!selected.Contains(kaiju), "Lava Golem consumed the established Kaiju target");
        });
        Run("Nibiru waits on a low-pressure single body and spends on two bodies", () =>
        {
            Fixture f = new Fixture();
            f.Duel.Player = 1;
            ClientCard nibiru = f.Add(Nibiru, CardLocation.Hand);
            f.Add(Sanaki, CardLocation.MonsterZone, 1);
            Require(!f.AI.OnSelectEffectYn(nibiru, -1), "Nibiru spent into one low-pressure body");
            f.Add(HaRe, CardLocation.MonsterZone, 1);
            Require(f.AI.OnSelectEffectYn(nibiru, -1), "Nibiru failed to spend into two bodies");
        });
        Run("Extra Deck contains all three Uzuhime copies", () =>
        {
            Require(ExtraDeck.Count(id => id == Uzuhime) == 3, "Uzuhime count drifted from the supplied deck");
        });
    }

    private static string Hash(string path)
    {
        using (SHA256 sha = SHA256.Create())
        using (FileStream stream = File.OpenRead(path))
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
    }

    public static int Main(string[] args)
    {
        if (args.Length != 3 || ExecutorClass == null)
            return 2;
        NamedCardsManager.Init(Path.GetFullPath(args[0]));
        typeof(Executor).Assembly.GetType("WindBot.Program").GetField("Rand", Flags)
            .SetValue(null, new Random(20261116));
        bool extra = false;
        foreach (string raw in File.ReadAllLines(args[1]))
        {
            string line = raw.Trim();
            if (line == "#extra")
            {
                extra = true;
                continue;
            }
            if (line == "!side")
                break;
            int id;
            if (int.TryParse(line, out id))
                (extra ? ExtraDeck : MainDeck).Add(id);
        }
        Run("Supplied deck has 40 Main and 15 Extra cards", () =>
        {
            Require(MainDeck.Count == 40 && ExtraDeck.Count == 15,
                "Observed " + MainDeck.Count + " Main and " + ExtraDeck.Count + " Extra cards");
        });
        Effects();
        Dictionary<string, object> report = new Dictionary<string, object>
        {
            { "format", "mikanko-focused-checks-v1" },
            { "evidence", "Synthetic legal-offer, dispatcher, battle and prompt fixtures; no Lua execution or duel-strength claim." },
            { "executableSha256", Hash(typeof(Executor).Assembly.Location) },
            { "databaseSha256", Hash(args[0]) },
            { "deckSha256", Hash(args[1]) },
            { "harnessSha256", Hash(Assembly.GetExecutingAssembly().Location) },
            { "passed", Results.All(item => (bool)item["passed"]) },
            { "checks", Results }
        };
        File.WriteAllText(args[2], new JavaScriptSerializer().Serialize(report));
        return Results.All(item => (bool)item["passed"]) ? 0 : 1;
    }
}
