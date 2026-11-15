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
    private const int Resh = 23912837, Evil = 97051536, Purulia = 84192580;
    private const int Mesh = 32467459, Construct = 20366274, Apkallone = 50907446;
    private const int ElGrysta = 48424886, Perfume = 81196066;
    private const int Wendikuruhu = 8852158, Winda = 94977269;
    private const int Armageddon = 28985331, Ash = 14558127, LinkConstruct = 86938484;
    private const int BlueEyes = 89631139;
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    private static readonly List<Dictionary<string, object>> Results = new List<Dictionary<string, object>>();
    private static readonly List<int> MainDeck = new List<int>();
    private static readonly List<int> ExtraDeck = new List<int>();
    private static readonly List<int> SideDeck = new List<int>();
    private static readonly Dictionary<string, object> LevComboEvidence = new Dictionary<string, object>();

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
            Bot.LifePoints = 8000;
            Enemy.LifePoints = 8000;
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

        internal ClientCard SelectIdle(params KeyValuePair<ClientCard, int>[] offers)
        {
            MainPhase main = new MainPhase { CanEndPhase = true };
            for (int i = 0; i < offers.Length; ++i)
            {
                ClientCard card = offers[i].Key;
                int description = offers[i].Value;
                main.ActivableCards.Add(card);
                main.ActivableDescs.Add(description);
                card.ActionActivateIndex[description] = i;
            }
            Duel.MainPhase = main;
            MainPhaseAction action = AI.OnSelectIdleCmd(main);
            Require(action.Action == MainPhaseAction.MainAction.Activate,
                "Idle scheduler did not choose an activation");
            Require(action.Index >= 0 && action.Index < offers.Length,
                "Idle scheduler returned an invalid activation index");
            return offers[action.Index].Key;
        }

        internal ClientCard Extra(int id)
        {
            return Bot.ExtraDeck.First(c => c.IsCode(id));
        }

        internal void Move(ClientCard card, CardLocation destination, bool faceDown = false)
        {
            if (card.Location == CardLocation.Hand) Bot.Hand.Remove(card);
            else if (card.Location == CardLocation.Grave) Bot.Graveyard.Remove(card);
            else if (card.Location == CardLocation.Removed) Bot.Banished.Remove(card);
            else if (card.Location == CardLocation.Extra) Bot.ExtraDeck.Remove(card);
            else if (card.Location == CardLocation.MonsterZone) Bot.MonsterZone[card.Sequence] = null;
            else if (card.Location == CardLocation.SpellZone) Bot.SpellZone[card.Sequence] = null;

            card.LastLocation = card.Location;
            card.Location = destination;
            card.Position = (int)(faceDown ? CardPosition.FaceDownDefence : CardPosition.FaceUpAttack);
            if (destination == CardLocation.Hand) Bot.Hand.Add(card);
            else if (destination == CardLocation.Grave) Bot.Graveyard.Add(card);
            else if (destination == CardLocation.Removed) Bot.Banished.Add(card);
            else if (destination == CardLocation.Extra) Bot.ExtraDeck.Add(card);
            else if (destination == CardLocation.MonsterZone)
            {
                int slot = Array.FindIndex(Bot.MonsterZone, value => value == null);
                Require(slot >= 0, "No open monster zone");
                card.Sequence = slot;
                Bot.MonsterZone[slot] = card;
            }
            else if (destination == CardLocation.SpellZone)
            {
                int slot = Array.FindIndex(Bot.SpellZone, value => value == null);
                Require(slot >= 0, "No open spell/trap zone");
                card.Sequence = slot;
                Bot.SpellZone[slot] = card;
            }
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

    private static KeyValuePair<ClientCard, int> Offer(ClientCard card, int offset)
    {
        return new KeyValuePair<ClientCard, int>(card, Desc(card.Id, offset));
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

    private static Dictionary<string, object> Snapshot(
        Fixture fixture, int immediateActivations, int conditionalEffects,
        int floodgateCoverage, string limit)
    {
        return new Dictionary<string, object>
        {
            { "monsters", fixture.Bot.GetMonsters().Select(c => c.Id + ":" + c.Position).ToArray() },
            { "spells", fixture.Bot.GetSpells().Select(c => c.Id + ":" + c.Position).ToArray() },
            { "graveyard", fixture.Bot.Graveyard.Select(c => c.Id).OrderBy(id => id).ToArray() },
            { "banished", fixture.Bot.Banished.Select(c => c.Id).OrderBy(id => id).ToArray() },
            { "hand", fixture.Bot.Hand.Select(c => c.Id).OrderBy(id => id).ToArray() },
            { "lifePoints", fixture.Bot.LifePoints },
            { "immediatelyUsableActivations", immediateActivations },
            { "conditionalEffects", conditionalEffects },
            { "floodgateCoverage", floodgateCoverage },
            { "limit", limit }
        };
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

    private static Fixture ExecuteLevOpening(bool assisted)
    {
        Fixture f = new Fixture();
        ClientCard lev = f.Add(Lev, CardLocation.Hand);
        if (assisted) f.Add(Grysta, CardLocation.Hand);
        f.Add(Ash, CardLocation.Hand);
        f.Add(MaxxC, CardLocation.Hand);
        f.Add(Droll, CardLocation.Hand);
        if (!assisted) f.Add(Veiler, CardLocation.Hand);

        Require(f.AI.OnSelectEffectYn(lev, Desc(Lev, 0)), "Lev activation was rejected");
        f.Move(lev, CardLocation.SpellZone);
        f.Chain(lev, true);
        ClientCard apkallone = f.Extra(Apkallone);
        Is(f.Select(HintMsg.ToGrave, apkallone, f.Extra(Evil), f.Extra(Construct)),
            Apkallone, "Lev did not begin the primary route");
        f.Move(apkallone, CardLocation.Grave);
        f.AI.OnChainEnd();

        Require(f.AI.OnSelectEffectYn(apkallone, Desc(Apkallone, 1)),
            "Apkallone grave trigger was rejected");
        f.Chain(apkallone, true);
        Is(f.Select(HintMsg.AddToHand, Card(Squamata, CardLocation.Deck),
            Card(Wendi, CardLocation.Deck), Card(Schism, CardLocation.Deck)),
            Squamata, "Apkallone did not search the primary-route Squamata");
        ClientCard squamata = f.Add(Squamata, CardLocation.Hand);
        Is(f.Select(HintMsg.Discard, f.Bot.Hand.ToArray()), Squamata,
            "Apkallone did not discard Squamata by effect");
        f.Move(squamata, CardLocation.Grave);
        f.AI.OnChainEnd();

        Require(f.AI.OnSelectEffectYn(squamata, Desc(Squamata, 1)),
            "Squamata grave trigger was rejected");
        f.Chain(squamata, true);
        Is(f.Select(HintMsg.ToGrave, Card(Grysta, CardLocation.Deck),
            Card(Hedgehog, CardLocation.Deck), Card(Wendi, CardLocation.Deck)),
            Grysta, "Squamata did not send Tohushaddoll Grysta");
        ClientCard tohushaddoll = f.Add(Grysta, CardLocation.Grave);
        f.AI.OnChainEnd();

        Require(f.AI.OnSelectEffectYn(tohushaddoll, Desc(Grysta, 1)),
            "Tohushaddoll Grysta fusion trigger was rejected");
        f.Chain(tohushaddoll, true);
        ClientCard mesh = f.Extra(Mesh);
        Is(f.Select(HintMsg.SpSummon, mesh, f.Extra(Construct), f.Extra(Winda)),
            Mesh, "Tohushaddoll Grysta did not choose Meshachrer");
        IList<ClientCard> openingMaterials = f.Select(HintMsg.FusionMaterial, 3, 3,
            apkallone, squamata, tohushaddoll);
        Require(openingMaterials.Count == 3, "Meshachrer did not use exactly three materials");
        foreach (ClientCard material in openingMaterials) f.Move(material, CardLocation.Removed);
        f.Move(mesh, CardLocation.MonsterZone);
        f.AI.OnChainEnd();

        Require(f.AI.OnSelectEffectYn(mesh, Desc(Mesh, 1)), "Meshachrer search was rejected");
        f.Bot.LifePoints -= 800;
        f.Chain(mesh, true);
        int meshChoice = assisted ? Void : Wendi;
        Is(f.Select(HintMsg.AddToHand, Card(Void, CardLocation.Deck),
            Card(Wendi, CardLocation.Deck), Card(Schism, CardLocation.Deck)),
            meshChoice, assisted ? "Meshachrer missed Void with a reveal available" :
            "Meshachrer searched an unusable Void without a reveal");
        ClientCard meshSearch = f.Add(meshChoice, CardLocation.Hand);
        f.AI.OnChainEnd();

        if (!assisted)
        {
            Require(f.AI.OnSelectEffectYn(lev, Desc(Lev, 1)),
                "Lev stopped before converting Meshachrer into a control body");
            f.Chain(lev, true);
            Is(f.Select(HintMsg.Release, mesh), Mesh, "Lev tributed the wrong Fusion");
            f.Move(mesh, CardLocation.Grave);
            ClientCard winda = f.Extra(Winda);
            Is(f.Select(HintMsg.SpSummon, winda, f.Extra(Construct)), Winda,
                "Lev-only route did not choose the verified Winda endboard");
            CardPosition position = f.Executor.OnSelectPosition(Winda,
                new[] { CardPosition.FaceUpAttack, CardPosition.FaceUpDefence });
            Require(position == CardPosition.FaceUpDefence,
                "Lev exposed its 0 ATK Winda in Attack Position");
            f.Move(winda, CardLocation.MonsterZone);
            winda.Position = (int)position;
            f.AI.OnChainEnd();
            LevComboEvidence["levOnly"] = Snapshot(f, 0, 0, 1,
                "Void requires an additional Shaddoll card in hand; the legal Lev-only line ends on 0 ATK Winda in Defense Position plus follow-up Wendi.");
            return f;
        }

        ClientCard voidCard = meshSearch;
        Require(f.AI.OnSelectEffectYn(voidCard, Desc(Void, 0)), "Void activation was rejected");
        f.Move(voidCard, CardLocation.SpellZone);
        f.Chain(voidCard, true);
        ClientCard revealed = f.Bot.Hand.First(c => c.IsCode(Grysta));
        Is(f.Select(HintMsg.Confirm, revealed), Grysta, "Void revealed the wrong card type");
        IList<ClientCard> voidSearch = f.Select(HintMsg.AddToHand, 2, 2,
            Card(ElFusion, CardLocation.Deck), Card(Schism, CardLocation.Deck),
            Card(Lev, CardLocation.Deck), Card(Squamata, CardLocation.Deck));
        Has(voidSearch, ElFusion, "Void missed El Shaddoll Fusion");
        Has(voidSearch, Schism, "Void missed Shaddoll Schism");
        ClientCard elFusion = f.Add(ElFusion, CardLocation.Hand);
        ClientCard schism = f.Add(Schism, CardLocation.Hand);
        Is(f.Select(HintMsg.Discard, f.Bot.Hand.ToArray()), Schism,
            "Void did not stage Schism for Construct recovery");
        f.Move(schism, CardLocation.Grave);
        f.AI.OnChainEnd();

        Require(f.AI.OnSelectEffectYn(lev, Desc(Lev, 1)),
            "Lev stopped before the Construct continuation");
        f.Chain(lev, true);
        Is(f.Select(HintMsg.Release, mesh), Mesh, "Lev did not tribute Meshachrer");
        f.Move(mesh, CardLocation.Grave);
        ClientCard construct = f.Extra(Construct);
        Is(f.Select(HintMsg.SpSummon, construct, f.Extra(Winda)), Construct,
            "Lev missed Construct after Void established the continuation");
        f.Move(construct, CardLocation.MonsterZone);
        construct.Position = (int)f.Executor.OnSelectPosition(Construct,
            new[] { CardPosition.FaceUpAttack, CardPosition.FaceUpDefence });
        f.AI.OnChainEnd();

        Require(f.AI.OnSelectEffectYn(construct, Desc(Construct, 0)),
            "Construct send effect was rejected");
        f.Chain(construct, true);
        Is(f.Select(HintMsg.ToGrave, Card(Hedgehog, CardLocation.Deck),
            Card(Wendi, CardLocation.Deck), Card(Resh, CardLocation.Deck)),
            Hedgehog, "Construct did not prepare the Wendi search");
        ClientCard hedgehog = f.Add(Hedgehog, CardLocation.Grave);
        f.AI.OnChainEnd();

        Require(f.AI.OnSelectEffectYn(hedgehog, Desc(Hedgehog, 1)),
            "Hedgehog search was rejected");
        f.Chain(hedgehog, true);
        Is(f.Select(HintMsg.AddToHand, Card(Wendi, CardLocation.Deck),
            Card(Squamata, CardLocation.Deck)), Wendi,
            "Hedgehog did not search the WIND material");
        ClientCard wendi = f.Add(Wendi, CardLocation.Hand);
        f.AI.OnChainEnd();

        Require(f.AI.OnSelectEffectYn(elFusion, Desc(ElFusion, 0)),
            "El Shaddoll Fusion continuation was rejected");
        f.Move(elFusion, CardLocation.SpellZone);
        f.Chain(elFusion, true);
        ClientCard wendikuruhu = f.Extra(Wendikuruhu);
        Is(f.Select(HintMsg.SpSummon, wendikuruhu, f.Extra(Winda), f.Extra(Construct)),
            Wendikuruhu, "El Shaddoll Fusion missed Wendikuruhu");
        IList<ClientCard> controlMaterials = f.Select(HintMsg.FusionMaterial, 2, 2,
            construct, wendi, revealed);
        Has(controlMaterials, Construct, "Control Fusion did not use Construct");
        Has(controlMaterials, Wendi, "Control Fusion did not use Wendi");
        foreach (ClientCard material in controlMaterials) f.Move(material, CardLocation.Grave);
        f.Move(elFusion, CardLocation.Grave);
        f.Move(wendikuruhu, CardLocation.MonsterZone);
        wendikuruhu.Position = (int)CardPosition.FaceUpDefence;
        f.AI.OnChainEnd();

        Require(f.AI.OnSelectEffectYn(wendi, Desc(Wendi, 1)), "Wendi setup trigger was rejected");
        f.Chain(wendi, true);
        Is(f.Select(HintMsg.SpSummon, Card(Ariel, CardLocation.Deck),
            Card(Hedgehog, CardLocation.Deck), Card(Squamata, CardLocation.Deck)),
            Ariel, "Wendi did not set Ariel for the control line");
        ClientCard ariel = f.Add(Ariel, CardLocation.MonsterZone, 0, true);
        f.AI.OnChainEnd();

        Require(f.AI.OnSelectEffectYn(construct, Desc(Construct, 2)),
            "Construct recovery trigger was rejected");
        f.Chain(construct, true);
        Is(f.Select(HintMsg.AddToHand, schism, elFusion), Schism,
            "Construct did not recover Schism");
        f.Move(schism, CardLocation.Hand);
        f.Move(schism, CardLocation.SpellZone, true);
        f.AI.OnChainEnd();

        Require(f.AI.OnSelectEffectYn(wendikuruhu, Desc(Wendikuruhu, 0)),
            "Wendikuruhu did not continue into the prepared Flip body");
        f.Chain(wendikuruhu, true);
        Is(f.Select(HintMsg.PosChange, ariel), Ariel, "Wendikuruhu did not flip Ariel");
        ariel.Position = (int)CardPosition.FaceUpDefence;
        f.AI.OnChainEnd();

        Require(f.AI.OnSelectEffectYn(ariel, Desc(Ariel, 0)), "Ariel revival was rejected");
        f.Chain(ariel, true);
        Is(f.Select(HintMsg.SpSummon, squamata), Squamata,
            "Ariel did not recover the banished Squamata");
        f.Move(squamata, CardLocation.MonsterZone, true);
        f.AI.OnChainEnd();

        LevComboEvidence["levPlusShaddoll"] = Snapshot(f, 2, 2, 1,
            "Two immediate activation windows are Wendikuruhu and Schism. Squamata destruction and Schism's same-attribute send are conditional; Winda is available through Schism. Link Shaddoll Construct is in the Side Deck, so the showcased Link continuation is unavailable in game one.");
        return f;
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

        Run("Lev sends Meshachrer when the primary route is unavailable and revival is live", () =>
        {
            Fixture f = new Fixture();
            f.Add(Squamata, CardLocation.Grave);
            ClientCard lev = f.Add(Lev, CardLocation.SpellZone);
            f.Chain(lev, true);
            Is(f.Select(HintMsg.ToGrave, Card(Construct, CardLocation.Extra),
                Card(Mesh, CardLocation.Extra)), Mesh,
                "Lev missed Meshachrer revival value");
        });

        Run("Lev falls back to Infernoid Evil when Apkallone is unavailable", () =>
        {
            Fixture f = new Fixture();
            f.Add(Wendi, CardLocation.Hand);
            ClientCard lev = f.Add(Lev, CardLocation.SpellZone);
            f.Chain(lev, true);
            Is(f.Select(HintMsg.ToGrave, Card(Construct, CardLocation.Extra),
                Card(Evil, CardLocation.Extra)), Evil,
                "Lev missed the available Evil-to-Void fallback");
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

        Run("Idle scheduler starts Lev before an independent Void line", () =>
        {
            Fixture f = new Fixture();
            ClientCard voidCard = f.Add(Void, CardLocation.Hand);
            ClientCard lev = f.Add(Lev, CardLocation.Hand);
            f.Add(Grysta, CardLocation.Hand);
            Is(f.SelectIdle(Offer(voidCard, 0), Offer(lev, 0)), Lev,
                "Idle scheduler spent Void before committing the Lev starter");
        });

        Run("Idle scheduler preserves Mesh search then Void then Lev tribute", () =>
        {
            Fixture f = new Fixture();
            ClientCard lev = f.Add(Lev, CardLocation.SpellZone);
            ClientCard mesh = f.Add(Mesh, CardLocation.MonsterZone);
            ClientCard elFusion = f.Add(ElFusion, CardLocation.Hand);
            f.Add(Grysta, CardLocation.Hand);

            Is(f.SelectIdle(Offer(elFusion, 0), Offer(lev, 1), Offer(mesh, 1)), Mesh,
                "Idle scheduler tributed Mesh or fused before taking its search");

            ClientCard voidCard = f.Add(Void, CardLocation.Hand);
            Is(f.SelectIdle(Offer(elFusion, 0), Offer(lev, 1), Offer(voidCard, 0),
                Offer(mesh, 1)), Void,
                "Idle scheduler skipped the searched Void setup");

            Is(f.SelectIdle(Offer(elFusion, 0), Offer(lev, 1), Offer(voidCard, 0),
                Offer(mesh, 1)), Lev,
                "Idle scheduler fused before Lev converted Mesh into Construct");
        });

        Run("Lev-only deterministic opening reports the legal Winda ceiling", () =>
        {
            Fixture f = ExecuteLevOpening(false);
            Require(f.Bot.GetMonsters().Count(c => c.IsCode(Winda) && c.IsDefense()) == 1,
                "Lev-only line did not end on Defense Position Winda");
            Require(f.Bot.Hand.Any(c => c.IsCode(Wendi)),
                "Lev-only line lost its Wendi follow-up");
            Require(f.Bot.Banished.Count(c => c.IsCode(Apkallone, Squamata, Grysta)) == 3,
                "Lev-only line recorded the wrong banished materials");
            Require(f.Bot.Graveyard.Count(c => c.IsCode(Mesh)) == 1,
                "Lev-only line recorded the wrong Graveyard");
        });

        Run("Lev plus one compatible Shaddoll reaches the verified control continuation", () =>
        {
            Fixture f = ExecuteLevOpening(true);
            Require(f.Bot.GetMonsters().Any(c => c.IsCode(Wendikuruhu) && c.IsDefense()),
                "Full continuation did not retain Wendikuruhu");
            Require(f.Bot.GetMonsters().Any(c => c.IsCode(Ariel) && c.IsFaceup()),
                "Full continuation did not flip Ariel");
            Require(f.Bot.GetMonsters().Any(c => c.IsCode(Squamata) && c.IsFacedown()),
                "Full continuation did not preserve the Squamata Flip interaction");
            Require(f.Bot.GetSpells().Any(c => c.IsCode(Schism) && c.IsFacedown()),
                "Full continuation did not set the recovered Schism");
            Require(new[] { Construct, Wendi, Hedgehog, ElFusion, Mesh }
                .All(id => f.Bot.Graveyard.Any(c => c.IsCode(id))),
                "Full continuation did not record its spent engine resources");
            Require(!f.Bot.ExtraDeck.Any(c => c.IsCode(LinkConstruct)),
                "Game-one Extra Deck unexpectedly contained the sided Link continuation");
        });

        Run("Interrupted Lev routes recalculate from the legal offers", () =>
        {
            Fixture f = new Fixture();
            f.Add(Lev, CardLocation.SpellZone);
            f.Add(Grysta, CardLocation.Hand);
            ClientCard mesh = f.Add(Mesh, CardLocation.MonsterZone);
            f.Chain(mesh, true);
            Is(f.Select(HintMsg.AddToHand, Card(ElFusion, CardLocation.Deck),
                Card(Wendi, CardLocation.Deck), Card(Schism, CardLocation.Deck)),
                ElFusion, "Missing Void did not produce the legal Fusion fallback");

            Fixture noMesh = new Fixture();
            noMesh.Add(Lev, CardLocation.SpellZone);
            ClientCard tohushaddoll = noMesh.Add(Grysta, CardLocation.Grave);
            noMesh.Chain(tohushaddoll, true);
            Is(noMesh.Select(HintMsg.SpSummon, Card(Construct, CardLocation.Extra),
                Card(Winda, CardLocation.Extra)), Construct,
                "Missing Meshachrer did not produce a legal alternate Fusion");
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

        Run("Void trap reveal finds Wendi plus El Fusion for the Lev continuation", () =>
        {
            Fixture f = new Fixture();
            f.Add(Lev, CardLocation.SpellZone);
            f.Add(Mesh, CardLocation.MonsterZone);
            ClientCard card = f.Add(Void, CardLocation.SpellZone);
            SetPrivate(f.Executor, "_voidRevealedType", CardType.Trap);
            f.Chain(card, true);
            IList<ClientCard> selected = f.Select(HintMsg.AddToHand, 2, 2,
                Card(ElFusion, CardLocation.Deck), Card(Wendi, CardLocation.Deck),
                Card(Squamata, CardLocation.Deck), Card(Schism, CardLocation.Deck));
            Has(selected, ElFusion, "Void failed to choose the Spell type");
            Has(selected, Wendi, "Void failed to choose the WIND material");
        });

        Run("Armageddon Knight sends Squamata into the adaptive route", () =>
        {
            Fixture f = new Fixture();
            ClientCard armageddon = f.Add(Armageddon, CardLocation.MonsterZone);
            Require(f.AI.OnSelectEffectYn(armageddon, 0),
                "Armageddon Knight effect was rejected");
            f.Chain(armageddon, true);
            Is(f.Select(HintMsg.ToGrave, Card(Squamata, CardLocation.Deck),
                Card(Grysta, CardLocation.Deck), Card(Dragon, CardLocation.Deck)),
                Squamata, "Armageddon Knight missed the Squamata starter");
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

        Run("Opponent-turn Fusion adds Apkallone disruption behind Winda", () =>
        {
            Fixture f = new Fixture();
            f.Duel.Player = 1;
            f.Add(Winda, CardLocation.MonsterZone);
            f.Add(Wendi, CardLocation.Hand);
            ClientCard threat = f.Add(BlueEyes, CardLocation.MonsterZone, 1);
            ClientCard fusion = f.Add(ElFusion, CardLocation.Hand);
            f.Chain(threat, false);
            Require(f.AI.OnSelectEffectYn(fusion, 0),
                "El Shaddoll Fusion stopped at Winda instead of using the opponent-turn branch");
            f.Chain(fusion, true);
            Is(f.Select(HintMsg.SpSummon, f.Extra(Winda), f.Extra(Apkallone),
                f.Extra(Construct), f.Extra(ElGrysta)), Apkallone,
                "Fusion did not choose the live face-up negate behind Winda");
        });

        Run("Opponent-turn Fusion can prepare Grysta summon negation", () =>
        {
            Fixture f = new Fixture();
            f.Duel.Player = 1;
            f.Add(Winda, CardLocation.MonsterZone);
            f.Add(Wendi, CardLocation.Hand);
            ClientCard fusion = f.Add(ElFusion, CardLocation.Hand);
            f.Chain(fusion, true);
            Is(f.Select(HintMsg.SpSummon, f.Extra(Winda), f.Extra(ElGrysta),
                f.Extra(74822425)), ElGrysta,
                "Fusion missed the available summon-negate branch");
        });

        Run("El Shaddoll Grysta holds a Shaddoll cost for a real summon", () =>
        {
            Fixture f = new Fixture();
            f.Duel.Player = 1;
            ClientCard grysta = f.Add(ElGrysta, CardLocation.MonsterZone);
            f.Add(Wendi, CardLocation.Hand);
            Require(f.AI.OnSelectEffectYn(grysta, 0),
                "El Shaddoll Grysta rejected its live summon-negate window");
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
            f.Add(Grysta, CardLocation.Hand);
            f.Add(Lev, CardLocation.SpellZone);
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

        Run("Apkallone declines its optional negate without an enemy target", () =>
        {
            Fixture f = new Fixture();
            ClientCard apkallone = f.Add(Apkallone, CardLocation.MonsterZone);
            f.Add(Wendi, CardLocation.MonsterZone);
            Require(!f.AI.OnSelectEffectYn(apkallone, Desc(Apkallone, 0)),
                "Apkallone activated only to negate a friendly card");
        });

        Run("Apkallone targets the live enemy card instead of itself", () =>
        {
            Fixture f = new Fixture();
            ClientCard apkallone = f.Add(Apkallone, CardLocation.MonsterZone);
            ClientCard enemy = f.Add(BlueEyes, CardLocation.MonsterZone, 1);
            Require(f.AI.OnSelectEffectYn(apkallone, Desc(Apkallone, 0)),
                "Apkallone declined a useful enemy negate");
            f.Chain(apkallone, true);
            Is(f.Select(HintMsg.Disable, apkallone, enemy), BlueEyes,
                "Apkallone selected a friendly negate target");
        });

        Run("A continuously negated Apkallone does not predict a useful resolution", () =>
        {
            Fixture f = new Fixture();
            ClientCard apkallone = f.Add(Apkallone, CardLocation.MonsterZone);
            Set(apkallone, "Disabled", 1);
            f.Add(BlueEyes, CardLocation.MonsterZone, 1);
            Require(!f.AI.OnSelectEffectYn(apkallone, Desc(Apkallone, 0)),
                "A disabled Apkallone attempted to use its optional negate");
        });

        Run("Low-ATK utility monster chooses Defense to avoid lethal exposure", () =>
        {
            Fixture f = new Fixture();
            f.Bot.LifePoints = 1000;
            f.Add(BlueEyes, CardLocation.MonsterZone, 1);
            CardPosition selected = f.Executor.OnSelectPosition(Purulia,
                new[] { CardPosition.FaceUpAttack, CardPosition.FaceUpDefence });
            Require(selected == CardPosition.FaceUpDefence,
                "A 100 ATK utility monster was exposed in Attack Position");
        });

        Run("Resh revives Winda to rebuild the control plan", () =>
        {
            Fixture f = new Fixture();
            ClientCard resh = f.Add(Resh, CardLocation.SpellZone);
            ClientCard winda = f.Add(Winda, CardLocation.Grave);
            Require(f.AI.OnSelectEffectYn(resh, Desc(Resh, 0)),
                "Resh declined a live Winda rebuild");
            f.Chain(resh, true);
            Is(f.Select(HintMsg.SpSummon, winda, Card(Squamata, CardLocation.Grave)),
                Winda, "Resh revived a low-value body instead of Winda");
        });

        Run("Resh flips a prepared Shaddoll without exposing another monster", () =>
        {
            Fixture f = new Fixture();
            f.Duel.Player = 1;
            ClientCard resh = f.Add(Resh, CardLocation.Grave);
            ClientCard squamata = f.Add(Squamata, CardLocation.MonsterZone, 0, true);
            f.Add(Wendi, CardLocation.Grave);
            f.Add(BlueEyes, CardLocation.MonsterZone, 1);
            Require(f.AI.OnSelectEffectYn(resh, Desc(Resh, 1)),
                "Resh declined a useful opponent-turn Flip interaction");
            f.Chain(resh, true);
            Is(f.Select(HintMsg.PosChange, squamata), Squamata,
                "Resh did not flip the prepared control monster");
        });

        Run("Fusion material preserves an established Winda", () =>
        {
            Fixture f = new Fixture();
            ClientCard fusion = f.Add(Fusion, CardLocation.SpellZone);
            ClientCard winda = f.Add(Winda, CardLocation.MonsterZone);
            ClientCard squamata = f.Add(Squamata, CardLocation.MonsterZone);
            f.Chain(fusion, true);
            Is(f.Select(HintMsg.FusionMaterial, 1, 1, winda, squamata)[0], Squamata,
                "Fusion planning consumed the active floodgate without justification");
        });

        Run("Active Winda stops redundant Fusion extension", () =>
        {
            Fixture f = new Fixture();
            f.Add(Winda, CardLocation.MonsterZone);
            f.Add(Wendi, CardLocation.Hand);
            f.Add(Veiler, CardLocation.Hand);
            ClientCard fusion = f.Add(Fusion, CardLocation.Hand);
            Require(!f.AI.OnSelectEffectYn(fusion, 0),
                "The executor weakened its Winda lock with another Fusion");
        });

        Run("A safe lethal board stops extending and proceeds toward battle", () =>
        {
            Fixture f = new Fixture();
            f.Enemy.LifePoints = 2000;
            f.Add(Construct, CardLocation.MonsterZone);
            f.Add(Wendi, CardLocation.Hand);
            f.Add(Veiler, CardLocation.Hand);
            ClientCard fusion = f.Add(Fusion, CardLocation.Hand);
            Require(!f.AI.OnSelectEffectYn(fusion, 0),
                "The executor extended instead of preserving on-board lethal damage");
        });

        Run("Unknown backrow prevents the safe-lethal shortcut", () =>
        {
            Fixture f = new Fixture();
            f.Enemy.LifePoints = 2000;
            f.Add(Construct, CardLocation.MonsterZone);
            f.Add(Schism, CardLocation.SpellZone, 1, true);
            f.Add(Wendi, CardLocation.Hand);
            f.Add(Veiler, CardLocation.Hand);
            ClientCard fusion = f.Add(Fusion, CardLocation.Hand);
            Require(f.AI.OnSelectEffectYn(fusion, 0),
                "The executor treated unknown backrow as guaranteed safe lethal");
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
            if (line == "!side") { section = SideDeck; continue; }
            int id;
            if (section != null && int.TryParse(line, out id)) section.Add(id);
        }
        Require(MainDeck.Count == 40, "Expected the supplied 40-card Main Deck; got " + MainDeck.Count);
        Require(ExtraDeck.Count == 15, "Expected 15 Extra Deck cards; got " + ExtraDeck.Count);
        Require(SideDeck.Count == 9, "Expected the supplied 9-card Side Deck; got " + SideDeck.Count);
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
            report["sideCount"] = SideDeck.Count;
            report["results"] = Results;
            report["levComboEvidence"] = LevComboEvidence;
            report["evidence"] = "Deterministic compiled-executor route replay using the selected script semantics; no Lua engine execution.";
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
