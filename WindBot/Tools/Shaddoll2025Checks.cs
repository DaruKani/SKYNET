using System;
using System.Collections;
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
using WindBot.Game.AI.Decks;
using YGOSharp.OCGWrapper;
using YGOSharp.OCGWrapper.Enums;

// Synthetic legal-offer and callback checks. These exercise the compiled
// executor but do not execute Lua or establish that a complete combo resolves.
internal static class Shaddoll2025Checks
{
    private const int Wendi = 51023024, Grysta = 95072744, Beast = 3717252;
    private const int Dragon = 77723643, Squamata = 30328508, Hedgehog = 4939890;
    private const int Ariel = 97518132, Veiler = 97268402, MaxxC = 23434538;
    private const int Void = 61345801, ElFusion = 6417578, Lev = 34950192;
    private const int Fusion = 44394295, Schism = 21011044, Droll = 94145021;
    private const int Mesh = 32467459, Construct = 20366274, Apkallone = 50907446;
    private const int Perfume = 81196066, Wendikuruhu = 8852158, Winda = 94977269;
    private const int BlueEyes = 89631139;
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    private static readonly List<Dictionary<string, object>> Results = new List<Dictionary<string, object>>();
    private static readonly List<int> MainDeck = new List<int>();
    private static readonly List<int> ExtraDeck = new List<int>();

    private sealed class Fixture
    {
        internal readonly Duel Duel = new Duel();
        internal readonly GameAI AI;
        internal readonly Shaddoll2025Executor Executor;
        internal ClientField Bot { get { return Duel.Fields[0]; } }
        internal ClientField Enemy { get { return Duel.Fields[1]; } }

        internal Fixture()
        {
            Bot.Init(MainDeck.Count, 0, 0);
            Enemy.Init(40, 0, 1);
            Bot.SetInitialDeck(MainDeck.Select(NamedCard.Get));
            foreach (int id in ExtraDeck)
                Bot.ExtraDeck.Add(Card(id, CardLocation.Extra));
            Duel.Turn = 1;
            Duel.Player = 0;
            Duel.Phase = DuelPhase.Main1;
            Duel.IsNewRule = true;
            Duel.IsNewRule2020 = true;
            AI = new GameAI(new GameClient(new WindBotInfo
            {
                Chat = false,
                Deck = "Shaddoll2025",
                Dialog = "default"
            }), Duel);
            Executor = new Shaddoll2025Executor(AI, Duel);
            AI.Executor = Executor;
            Executor.Deck = "AI_Shaddoll2025";
            AI.OnNewTurn();
            AI.OnNewPhase();
        }

        internal ClientCard Add(int id, CardLocation location, int controller = 0, bool faceDown = false)
        {
            ClientCard card = Card(id, location, faceDown);
            card.Controller = controller;
            ClientField field = Duel.Fields[controller];
            if (location == CardLocation.Hand) field.Hand.Add(card);
            else if (location == CardLocation.Grave) field.Graveyard.Add(card);
            else if (location == CardLocation.Removed) field.Banished.Add(card);
            else if (location == CardLocation.MonsterZone)
            {
                int slot = Array.FindIndex(field.MonsterZone, value => value == null);
                Require(slot >= 0, "No open monster zone");
                card.Sequence = slot;
                field.MonsterZone[slot] = card;
            }
            else if (location == CardLocation.SpellZone)
            {
                int slot = Array.FindIndex(field.SpellZone, value => value == null);
                Require(slot >= 0, "No open spell/trap zone");
                card.Sequence = slot;
                field.SpellZone[slot] = card;
            }
            if (controller == 0 && MainDeck.Contains(id))
            {
                typeof(ClientField).GetMethod("TrackRemoveFromDeck", Flags).Invoke(Bot, new object[] { id });
                if (Bot.Deck.Count > 0) Bot.Deck.RemoveAt(Bot.Deck.Count - 1);
            }
            return card;
        }

        internal void Chain(ClientCard card, bool resolving)
        {
            Duel.CurrentChain.Clear();
            Duel.CurrentChainInfo.Clear();
            Duel.CurrentChain.Add(card);
            Duel.CurrentChainInfo.Add(new ChainInfo(card, card.Controller, Desc(card.Id, 0)));
            Duel.LastChainPlayer = card.Controller;
            Duel.LastChainLocation = card.Location;
            Duel.SolvingChainIndex = resolving ? 1 : 0;
        }

        internal IList<ClientCard> Select(int hint, int min, int max, params ClientCard[] offered)
        {
            IList<ClientCard> selected = AI.OnSelectCard(offered, min, max, hint, false);
            Require(selected != null && selected.Count >= min && selected.Count <= max,
                "Selection count violated bounds");
            Require(selected.Distinct().Count() == selected.Count && selected.All(offered.Contains),
                "Selection returned a card outside the legal offer");
            return selected;
        }

        internal ClientCard Select(int hint, params ClientCard[] offered)
        {
            return Select(hint, 1, 1, offered)[0];
        }
    }

    private static ClientCard Card(int id, CardLocation location, bool faceDown = false)
    {
        int position = (int)(faceDown ? CardPosition.FaceDownDefence : CardPosition.FaceUpAttack);
        ClientCard card = new ClientCard(id, location, 0, position) { Controller = 0 };
        Require(card.Data != null, "Missing database card " + id);
        foreach (string name in new[] { "Type", "Level", "Race", "Attribute", "Attack", "Defense" })
        {
            object value = card.Data.GetType().GetProperty(name, Flags).GetValue(card.Data, null);
            typeof(ClientCard).GetProperty(name, Flags).SetValue(card, value, null);
        }
        Set(card, "BaseAttack", card.Attack);
        Set(card, "BaseDefense", card.Defense);
        return card;
    }

    private static void Set(ClientCard card, string property, int value)
    {
        typeof(ClientCard).GetProperty(property, Flags).SetValue(card, value, null);
    }

    private static void SetPrivate(object target, string field, object value)
    {
        target.GetType().GetField(field, Flags).SetValue(target, value);
    }

    private static int Desc(int id, int offset)
    {
        return id * 16 + offset;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Is(ClientCard actual, int id, string detail)
    {
        Require(actual != null && actual.Id == id,
            detail + "; expected " + id + ", got " + (actual == null ? "null" : actual.Id.ToString()));
    }

    private static void Has(IList<ClientCard> actual, int id, string detail)
    {
        Require(actual.Any(c => c.Id == id), detail + "; missing " + id);
    }

    private static void Run(string name, Action action)
    {
        Stopwatch watch = Stopwatch.StartNew();
        bool passed = false;
        string detail = "passed";
        try
        {
            action();
            passed = true;
        }
        catch (Exception error)
        {
            detail = (error.InnerException ?? error).Message;
        }
        Results.Add(new Dictionary<string, object>
        {
            { "name", name },
            { "passed", passed },
            { "elapsedMs", watch.ElapsedMilliseconds },
            { "detail", detail }
        });
        Console.WriteLine((passed ? "PASS " : "FAIL ") + name + ": " + detail);
    }

    private static void Decisions()
    {
        Run("Lev defaults to Apkallone instead of a fixed irrelevant dump", () =>
        {
            Fixture f = new Fixture();
            ClientCard lev = f.Add(Lev, CardLocation.SpellZone);
            f.Chain(lev, true);
            Is(f.Select(HintMsg.ToGrave, Card(Apkallone, CardLocation.Extra),
                Card(Construct, CardLocation.Extra), Card(Perfume, CardLocation.Extra)),
                Apkallone, "Lev did not choose its search-and-discard setup");
        });

        Run("Lev chooses Perfume Dancer only for a meaningful battle reduction", () =>
        {
            Fixture f = new Fixture();
            ClientCard threat = f.Add(BlueEyes, CardLocation.MonsterZone, 1);
            Set(threat, "Defense", 3000);
            ClientCard lev = f.Add(Lev, CardLocation.SpellZone);
            f.Chain(lev, true);
            Is(f.Select(HintMsg.ToGrave, Card(Apkallone, CardLocation.Extra),
                Card(Perfume, CardLocation.Extra)), Perfume,
                "Lev ignored the available battle solution");
        });

        Run("Lev sends Meshachrer when its grave trigger creates a body", () =>
        {
            Fixture f = new Fixture();
            f.Add(Squamata, CardLocation.Grave);
            ClientCard lev = f.Add(Lev, CardLocation.SpellZone);
            f.Chain(lev, true);
            Is(f.Select(HintMsg.ToGrave, Card(Apkallone, CardLocation.Extra),
                Card(Mesh, CardLocation.Extra)), Mesh,
                "Lev missed Meshachrer revival value");
        });

        Run("Lev can use its tribute effect after resolving its activation effect", () =>
        {
            Fixture f = new Fixture();
            ClientCard openingLev = f.Add(Lev, CardLocation.Hand);
            Require(f.AI.OnSelectEffectYn(openingLev, 0), "Lev activation effect was rejected");
            f.Add(Construct, CardLocation.MonsterZone);
            ClientCard faceupLev = f.Add(Lev, CardLocation.SpellZone);
            Require(f.AI.OnSelectEffectYn(faceupLev, Desc(Lev, 1)),
                "The Shaddoll-only lock incorrectly blocked Lev's own second effect");
        });

        Run("Fusion Deployment remains a legal Cartesia extender under Lev's lock", () =>
        {
            Fixture f = new Fixture();
            ClientCard lev = f.Add(Lev, CardLocation.Hand);
            Require(f.AI.OnSelectEffectYn(lev, 0), "Lev activation effect was rejected");
            ClientCard deployment = f.Add(6498706, CardLocation.Hand);
            Require(f.AI.OnSelectEffectYn(deployment, 0),
                "Lev's Extra Deck lock incorrectly blocked a Deck summon");
            ClientCard cartesia = f.Add(95515789, CardLocation.MonsterZone);
            f.Chain(cartesia, true);
            Is(f.Select(HintMsg.SpSummon, Card(24915933, CardLocation.Extra),
                Card(Construct, CardLocation.Extra)), Construct,
                "Cartesia selected a non-Shaddoll Fusion under Lev's lock");
        });

        Run("Void search obeys different card types and prepares Lev plus Schism", () =>
        {
            Fixture f = new Fixture();
            ClientCard card = f.Add(Void, CardLocation.SpellZone);
            SetPrivate(f.Executor, "_voidRevealedType", CardType.Monster);
            f.Chain(card, true);
            IList<ClientCard> selected = f.Select(HintMsg.AddToHand, 2, 2,
                Card(Lev, CardLocation.Deck), Card(Schism, CardLocation.Deck),
                Card(Squamata, CardLocation.Deck));
            Has(selected, Lev, "Void failed to choose the Spell type");
            Has(selected, Schism, "Void failed to choose the Trap type");
        });

        Run("Squamata sends Dragon into visible backrow", () =>
        {
            Fixture f = new Fixture();
            f.Add(Schism, CardLocation.SpellZone, 1, true);
            ClientCard squamata = f.Add(Squamata, CardLocation.Grave);
            f.Chain(squamata, true);
            Is(f.Select(HintMsg.ToGrave, Card(Dragon, CardLocation.Deck),
                Card(Hedgehog, CardLocation.Deck), Card(Wendi, CardLocation.Deck)),
                Dragon, "Squamata failed to convert into backrow removal");
        });

        Run("Squamata sends Grysta when a legal fusion continuation exists", () =>
        {
            Fixture f = new Fixture();
            f.Add(Wendi, CardLocation.Hand);
            f.Add(MaxxC, CardLocation.Hand);
            ClientCard squamata = f.Add(Squamata, CardLocation.Grave);
            f.Chain(squamata, true);
            Is(f.Select(HintMsg.ToGrave, Card(Grysta, CardLocation.Deck),
                Card(Hedgehog, CardLocation.Deck), Card(Beast, CardLocation.Deck)),
                Grysta, "Squamata missed the adaptive fusion extender");
        });

        Run("Opponent-turn fusion chooses Winda at the interaction point", () =>
        {
            Fixture f = new Fixture();
            f.Duel.Player = 1;
            ClientCard fusion = f.Add(ElFusion, CardLocation.SpellZone);
            f.Chain(fusion, true);
            Is(f.Select(HintMsg.SpSummon, Card(Winda, CardLocation.Extra),
                Card(Construct, CardLocation.Extra)), Winda,
                "Opponent-turn fusion failed to establish the summon limit");
        });

        Run("Going second fusion chooses Construct over premature Winda", () =>
        {
            Fixture f = new Fixture();
            f.Duel.Turn = 2;
            f.Add(BlueEyes, CardLocation.MonsterZone, 1);
            ClientCard fusion = f.Add(Fusion, CardLocation.SpellZone);
            f.Chain(fusion, true);
            Is(f.Select(HintMsg.SpSummon, Card(Winda, CardLocation.Extra),
                Card(Construct, CardLocation.Extra)), Construct,
                "Board breaking was blocked by an early Winda choice");
        });

        Run("Fusion material gains value from Squamata and preserves Veiler", () =>
        {
            Fixture f = new Fixture();
            ClientCard fusion = f.Add(Fusion, CardLocation.SpellZone);
            f.Chain(fusion, true);
            IList<ClientCard> selected = f.Select(HintMsg.FusionMaterial, 1, 2,
                Card(Squamata, CardLocation.Hand), Card(Veiler, CardLocation.Hand));
            Is(selected[0], Squamata,
                "Material policy spent live interaction before a Shaddoll trigger");
            Require(selected.Count == 1, "Material policy consumed more cards than the prompt required");
        });

        Run("Wendikuruhu flips the useful own Flip monster first", () =>
        {
            Fixture f = new Fixture();
            ClientCard wendik = f.Add(Wendikuruhu, CardLocation.MonsterZone);
            ClientCard own = Card(Squamata, CardLocation.MonsterZone, true);
            own.Controller = 0;
            ClientCard enemy = Card(BlueEyes, CardLocation.MonsterZone, true);
            enemy.Controller = 1;
            f.Chain(wendik, true);
            IList<ClientCard> selected = f.Select(HintMsg.PosChange, 1, 2, own, enemy);
            Is(selected[0], Squamata,
                "Flip-control selected the opponent's facedown monster first");
            Require(selected.Count == 1, "Flip-control exposed more facedown cards than necessary");
        });

        Run("Wendikuruhu turns the opponent's best face-up monster down", () =>
        {
            Fixture f = new Fixture();
            ClientCard wendik = f.Add(Wendikuruhu, CardLocation.MonsterZone);
            ClientCard own = Card(Wendi, CardLocation.MonsterZone);
            own.Controller = 0;
            ClientCard enemy = Card(BlueEyes, CardLocation.MonsterZone);
            enemy.Controller = 1;
            f.Chain(wendik, true);
            Is(f.Select(HintMsg.PosChange, own, enemy), BlueEyes,
                "Flip-control failed to select the opposing threat");
        });

        Run("Ariel prioritizes the opponent's Extra Deck grave resource", () =>
        {
            Fixture f = new Fixture();
            ClientCard ariel = f.Add(Ariel, CardLocation.Grave);
            ClientCard own = Card(Squamata, CardLocation.Grave);
            own.Controller = 0;
            ClientCard enemy = Card(Construct, CardLocation.Grave); enemy.Controller = 1;
            ClientCard secondEnemy = Card(Winda, CardLocation.Grave); secondEnemy.Controller = 1;
            f.Chain(ariel, true);
            IList<ClientCard> selected = f.Select(HintMsg.Remove, 1, 3, own, enemy, secondEnemy);
            Is(selected[0], Construct,
                "Ariel banished an own resource before the opponent's Fusion");
            Require(selected.Count == 2 && selected.All(c => c.Controller == 1),
                "Ariel failed to banish all useful enemy targets or consumed an own resource");
        });

        Run("Resolved Maxx C shortens extension after a functional board", () =>
        {
            Fixture f = new Fixture();
            f.Add(Winda, CardLocation.MonsterZone);
            f.Add(Wendi, CardLocation.Hand);
            f.Add(MaxxC, CardLocation.Hand);
            ClientCard fusion = f.Add(Fusion, CardLocation.Hand);
            IList<int> resolved = (IList<int>)typeof(DefaultExecutor)
                .GetField("enemyResolvedEffectIdList", Flags).GetValue(f.Executor);
            resolved.Add(MaxxC);
            Require(!f.AI.OnSelectEffectYn(fusion, 0),
                "The executor extended into Maxx C despite already having Winda");
        });

        Run("Droll activates only in the opponent's legal post-search turn", () =>
        {
            Fixture f = new Fixture();
            ClientCard droll = f.Add(Droll, CardLocation.Hand);
            f.Duel.Player = 1;
            Require(f.AI.OnSelectEffectYn(droll, 0), "Droll declined the opponent's search window");
            Fixture ownTurn = new Fixture();
            ClientCard ownDroll = ownTurn.Add(Droll, CardLocation.Hand);
            ownTurn.Duel.Player = 0;
            Require(!ownTurn.AI.OnSelectEffectYn(ownDroll, 0),
                "Droll was spent during our own turn");
        });

        Run("Schism waits instead of fusing into an empty opponent board", () =>
        {
            Fixture f = new Fixture();
            f.Duel.Player = 1;
            f.Add(Wendi, CardLocation.Hand);
            f.Add(MaxxC, CardLocation.Grave);
            ClientCard schism = f.Add(Schism, CardLocation.SpellZone);
            Require(!f.AI.OnSelectEffectYn(schism, 0),
                "Schism spent its once-per-turn interaction without a threat");
        });

        Run("Meshachrer search prefers Void Unleashing as the broad extender", () =>
        {
            Fixture f = new Fixture();
            ClientCard mesh = f.Add(Mesh, CardLocation.MonsterZone);
            f.Chain(mesh, true);
            Is(f.Select(HintMsg.AddToHand, Card(Void, CardLocation.Deck),
                Card(Fusion, CardLocation.Deck), Card(Schism, CardLocation.Deck)),
                Void, "Meshachrer did not select the highest-coverage starter");
        });

        Run("Apkallone search prepares Schism before a redundant monster", () =>
        {
            Fixture f = new Fixture();
            ClientCard apkallone = f.Add(Apkallone, CardLocation.Grave);
            f.Chain(apkallone, true);
            Is(f.Select(HintMsg.AddToHand, Card(Schism, CardLocation.Deck),
                Card(Squamata, CardLocation.Deck)), Schism,
                "Apkallone failed to preserve opponent-turn interaction");
        });

        Run("Cartesia selects Granguignol when its named line is legal", () =>
        {
            Fixture f = new Fixture();
            ClientCard cartesia = f.Add(95515789, CardLocation.MonsterZone);
            f.Chain(cartesia, true);
            Is(f.Select(HintMsg.SpSummon, Card(24915933, CardLocation.Extra),
                Card(Construct, CardLocation.Extra)), 24915933,
                "Cartesia missed the deck's dedicated named Fusion");
        });

        Run("Faimena uses the board-breaking Fusion offered by the engine", () =>
        {
            Fixture f = new Fixture();
            f.Add(BlueEyes, CardLocation.MonsterZone, 1);
            ClientCard faimena = f.Add(1498449, CardLocation.Hand);
            f.Chain(faimena, true);
            Is(f.Select(HintMsg.SpSummon, Card(Construct, CardLocation.Extra),
                Card(Winda, CardLocation.Extra)), Construct,
                "Faimena chose an early lock over visible board pressure");
        });

        Run("Resolved Fuwalos also shortens Extra Deck extension", () =>
        {
            Fixture f = new Fixture();
            f.Add(Winda, CardLocation.MonsterZone);
            f.Add(Wendi, CardLocation.Hand);
            f.Add(MaxxC, CardLocation.Hand);
            ClientCard fusion = f.Add(Fusion, CardLocation.Hand);
            IList<int> resolved = (IList<int>)typeof(DefaultExecutor)
                .GetField("enemyResolvedEffectIdList", Flags).GetValue(f.Executor);
            resolved.Add(42141493);
            Require(!f.AI.OnSelectEffectYn(fusion, 0),
                "The executor ignored Fuwalos after reaching a functional board");
        });

        Run("One response covers a chain instead of spending Veiler and Shekhinaga", () =>
        {
            Fixture f = new Fixture();
            f.Duel.Player = 1;
            ClientCard enemy = f.Add(20001443, CardLocation.MonsterZone, 1);
            f.Chain(enemy, false);
            ClientCard veiler = f.Add(Veiler, CardLocation.Hand);
            ClientCard shekhinaga = f.Add(74822425, CardLocation.MonsterZone);
            f.Add(Wendi, CardLocation.Hand);
            Require(f.AI.OnSelectEffectYn(veiler, 0), "Veiler did not answer the live monster effect");
            Require(!f.AI.OnSelectEffectYn(shekhinaga, 0),
                "Shekhinaga duplicated Veiler on the same chain link");
        });

        Run("A negated Lev route can be abandoned for an independent Void starter", () =>
        {
            Fixture f = new Fixture();
            ClientCard lev = f.Add(Lev, CardLocation.Hand);
            Require(f.AI.OnSelectEffectYn(lev, 0), "Opening Lev was not accepted");
            f.AI.OnChainEnd();
            f.Duel.CurrentChain.Clear();
            f.Duel.CurrentChainInfo.Clear();
            ClientCard voidCard = f.Add(Void, CardLocation.Hand);
            f.Add(Wendi, CardLocation.Hand);
            Require(f.AI.OnSelectEffectYn(voidCard, 0),
                "The executor continued a failed Lev assumption instead of using Void");
        });

        Run("Winda prevents Cartesia from extending our own completed board", () =>
        {
            Fixture f = new Fixture();
            f.Add(Winda, CardLocation.MonsterZone);
            ClientCard cartesia = f.Add(95515789, CardLocation.MonsterZone);
            f.Add(Wendi, CardLocation.Hand);
            f.Add(MaxxC, CardLocation.Hand);
            Require(!f.AI.OnSelectEffectYn(cartesia, 0),
                "Cartesia ignored the active Winda restriction");
        });

        Run("Limited Extra Deck state selects the sole legal Fusion", () =>
        {
            Fixture f = new Fixture();
            ClientCard fusion = f.Add(Fusion, CardLocation.SpellZone);
            f.Chain(fusion, true);
            Is(f.Select(HintMsg.SpSummon, Card(Apkallone, CardLocation.Extra)),
                Apkallone, "The executor assumed an unavailable Extra Deck monster");
        });
    }

    private static void ParseDeck(string path)
    {
        IList<int> section = null;
        foreach (string raw in File.ReadAllLines(path))
        {
            string line = raw.Trim();
            if (line == "#main") { section = MainDeck; continue; }
            if (line == "#extra") { section = ExtraDeck; continue; }
            if (line == "!side") break;
            int id;
            if (section != null && int.TryParse(line, out id)) section.Add(id);
        }
        Require(MainDeck.Count == 40, "Expected 40 Main Deck cards; got " + MainDeck.Count);
        Require(ExtraDeck.Count == 15, "Expected 15 Extra Deck cards; got " + ExtraDeck.Count);
    }

    private static string Hash(string path)
    {
        using (FileStream stream = File.OpenRead(path))
        using (SHA256 sha = SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
    }

    private static int Main(string[] args)
    {
        if (args.Length != 3) return 2;
        bool passed = false;
        Dictionary<string, object> report = new Dictionary<string, object>();
        try
        {
            WindBot.Config.Load(new string[0]);
            NamedCardsManager.Init(Path.GetFullPath(args[0]));
            ParseDeck(args[1]);
            Decisions();
            passed = Results.All(result => (bool)result["passed"]);
            report["databaseSha256"] = Hash(args[0]);
            report["deckSha256"] = Hash(args[1]);
            report["executableSha256"] = Hash(typeof(Executor).Assembly.Location);
            report["mainCount"] = MainDeck.Count;
            report["extraCount"] = ExtraDeck.Count;
            report["results"] = Results;
            report["evidence"] = "Synthetic compiled-executor offers; no Lua or duel server execution.";
        }
        catch (Exception error)
        {
            report["error"] = error.ToString();
            Console.Error.WriteLine(error);
        }
        report["format"] = "windbot-shaddoll-2025-check-v1";
        report["passed"] = passed;
        File.WriteAllText(args[2], new JavaScriptSerializer().Serialize(report));
        Console.WriteLine((passed ? "PASS " : "FAIL ") + Results.Count + " Shaddoll 2025 focused checks.");
        return passed ? 0 : 1;
    }
}
