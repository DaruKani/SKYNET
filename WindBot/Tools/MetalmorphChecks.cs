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

// Explicit synthetic offers exercise the real dispatcher and prompt callbacks.
// These checks do not execute Lua or establish duel strength.
internal static class MetalmorphChecks
{
    private const int Illusionist = 29157292, Max = 89812483, Flame = 91152455;
    private const int Fullmetal = 80870883, Time = 15216188, Zoa = 53753697;
    private const int Metalzoa = 27268998, Swordsman = 27704731, Claws = 83711531;
    private const int Bmd = 93969023, Redmd = 88264978, Seal = 24361622;
    private const int Magnamhut = 33854624, HopEar = 1980574, Stardust = 44508094;
    private const int Baronne = 84815190, Accel = 30983281, MaxxC = 23434538;
    private static readonly Type ExecutorClass = typeof(Executor).Assembly.GetType("WindBot.Game.AI.Decks.MetalmorphExecutor");
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    private static readonly List<Dictionary<string, object>> Results = new List<Dictionary<string, object>>();
    private static List<int> MainDeck = new List<int>();
    private static List<int> Extra = new List<int>();

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
            Bot.SetInitialDeck(MainDeck.Select(NamedCard.Get));
            foreach (int id in Extra) Bot.ExtraDeck.Add(Card(id, CardLocation.Extra));
            Duel.Turn = 1;
            Duel.Player = 0;
            Duel.Phase = DuelPhase.Main1;
            Duel.IsNewRule = true;
            Duel.IsNewRule2020 = true;
            var client = new GameClient(new WindBotInfo { Chat = false, Deck = "Metalmorph", Dialog = "default" });
            AI = new GameAI(client, Duel);
            Executor = (Executor)Activator.CreateInstance(ExecutorClass, AI, Duel);
            AI.Executor = Executor;
            Executor.Deck = "AI_Metalmorph";
            AI.OnNewTurn();
            AI.OnNewPhase();
        }

        internal ClientCard Add(int id, CardLocation location, int controller = 0)
        {
            ClientCard card = Card(id, location);
            card.Controller = controller;
            ClientField field = Duel.Fields[controller];
            if (location == CardLocation.Hand) field.Hand.Add(card);
            else if (location == CardLocation.Grave) field.Graveyard.Add(card);
            else if (location == CardLocation.Removed) field.Banished.Add(card);
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
                if (Bot.Deck.Count > 0) Bot.Deck.RemoveAt(Bot.Deck.Count - 1);
            }
            return card;
        }

        internal void Chain(ClientCard card, int description, bool resolving)
        {
            Duel.CurrentChain.Clear();
            Duel.CurrentChainInfo.Clear();
            Duel.CurrentChain.Add(card);
            Duel.CurrentChainInfo.Add(new ChainInfo(card, card.Controller, description));
            Duel.LastChainPlayer = card.Controller;
            Duel.SolvingChainIndex = resolving ? 1 : 0;
        }

        internal void ClearChain()
        {
            AI.OnChainEnd();
            Duel.CurrentChain.Clear();
            Duel.CurrentChainInfo.Clear();
            Duel.SolvingChainIndex = 0;
            Duel.LastChainPlayer = -1;
        }

        internal bool Activate(ClientCard card, int description)
        {
            bool accepted = AI.OnSelectEffectYn(card, description);
            if (accepted)
            {
                Chain(card, description, false);
                AI.OnChaining(card, card.Controller);
            }
            return accepted;
        }

        internal IList<ClientCard> Select(int hint, params ClientCard[] offered)
        {
            IList<ClientCard> result = AI.OnSelectCard(offered, 1, 1, hint, false);
            Require(result != null && result.Count == 1 && offered.Contains(result[0]), "Expected one offered card");
            return result;
        }

        internal MainPhaseAction OfferSummon(ClientCard card, bool special)
        {
            var main = new MainPhase { CanEndPhase = true };
            if (special) main.SpecialSummonableCards.Add(card); else main.SummonableCards.Add(card);
            Duel.MainPhase = main;
            return AI.OnSelectIdleCmd(main);
        }
    }

    private static ClientCard Card(int id, CardLocation location)
    {
        var card = new ClientCard(id, location, 0, (int)CardPosition.FaceUpAttack) { Controller = 0 };
        // Model queried public card state. Own-deck slots remain the ordinary hidden placeholders.
        foreach (string property in new[] { "Type", "Level", "Race", "Attribute", "Attack", "Defense" })
            typeof(ClientCard).GetProperty(property).SetValue(card, card.Data.GetType().GetProperty(property).GetValue(card.Data, null), null);
        Set(card, "BaseAttack", card.Data.Attack);
        return card;
    }

    private static void Set(ClientCard card, string property, int value)
    {
        typeof(ClientCard).GetProperty(property).SetValue(card, value, null);
    }

    private static int Desc(int id, int index) { return id * 16 + index; }
    private static void Require(bool condition, string reason) { if (!condition) throw new InvalidOperationException(reason); }
    private static void IsCard(IList<ClientCard> selected, int id) { Require(selected.Count == 1 && selected[0].Id == id, "Expected " + id + "; got " + string.Join(",", selected.Select(c => c.Id))); }
    private static void Run(string name, Action test)
    {
        var watch = Stopwatch.StartNew();
        string detail = "passed";
        bool passed = false;
        try { test(); passed = true; }
        catch (Exception error) { detail = (error.InnerException ?? error).Message; }
        Results.Add(new Dictionary<string, object> { { "name", name }, { "passed", passed }, { "elapsedMs", watch.ElapsedMilliseconds }, { "detail", detail } });
        Console.WriteLine((passed ? "PASS " : "FAIL ") + name + ": " + detail);
    }

    private static int Queued(Fixture f)
    {
        return ((IList)typeof(GameAI).GetField("m_selector", Flags).GetValue(f.AI)).Count;
    }

    private static void Stale(Fixture f, int id)
    {
        f.Executor.SetCard(ExecutorType.Activate, Card(id, CardLocation.Hand), -1);
    }

    private static void Effects()
    {
        Run("Illusionist summon tutor does not queue a race declaration", () =>
        {
            var f = new Fixture();
            ClientCard illusionist = f.Add(Illusionist, CardLocation.MonsterZone);
            Require(f.AI.OnSelectEffectYn(illusionist, Desc(Illusionist, 0)), "Summon trigger rejected");
            Require(((IList)typeof(GameAI).GetField("m_races", Flags).GetValue(f.AI)).Count == 0, "Tutor queued a race");
        });
        Run("Illusionist tutors on the opponent turn", () =>
        {
            var f = new Fixture();
            f.Duel.Player = 1;
            ClientCard illusionist = f.Add(Illusionist, CardLocation.MonsterZone);
            Require(f.AI.OnSelectEffectYn(illusionist, Desc(Illusionist, 0)), "Opponent-turn trigger rejected");
        });
        Run("Unknown Illusionist description uses observed summon context", () =>
        {
            var f = new Fixture();
            f.Duel.Player = 1;
            ClientCard illusionist = f.Add(Illusionist, CardLocation.MonsterZone);
            f.Duel.LastSummonedCards.Add(illusionist);
            Require(f.AI.OnSelectEffectYn(illusionist, -1), "Observed summon trigger rejected");
        });
        Run("Unknown Illusionist description is not assumed to be race change", () =>
        {
            var f = new Fixture();
            Require(!f.AI.OnSelectEffectYn(f.Add(Illusionist, CardLocation.MonsterZone), -1), "Ambiguous effect accepted");
        });
        Run("Illusionist still declares Dragon for the actual race effect", () =>
        {
            var f = new Fixture();
            Require(f.AI.OnSelectEffectYn(f.Add(Illusionist, CardLocation.MonsterZone), Desc(Illusionist, 1)), "Race effect rejected");
            Require(f.AI.OnAnnounceRace(1, new[] { CardRace.Fiend, CardRace.Dragon })[0] == CardRace.Dragon, "Dragon priority lost");
        });
        Run("Flame accepts the Lua destruction replacement description 96", () =>
        {
            var f = new Fixture();
            Require(f.AI.OnSelectEffectYn(f.Add(Flame, CardLocation.SpellZone), 96), "Replacement rejected");
        });
        Run("Flame does not confuse a selection hint with replacement description", () =>
        {
            var f = new Fixture();
            Require(!f.AI.OnSelectEffectYn(f.Add(Flame, CardLocation.SpellZone), HintMsg.DestroyReplace), "Wrong description accepted");
        });
        Run("Illusionist tutor resolves with a stale unrelated candidate", () =>
        {
            var f = new Fixture();
            f.Chain(f.Add(Illusionist, CardLocation.MonsterZone), Desc(Illusionist, 0), true);
            Stale(f, Time);
            IsCard(f.Select(HintMsg.Set, Card(Time, CardLocation.Deck), Card(Max, CardLocation.Deck)), Max);
        });
        Run("Fullmetal hand tutor retains activation identity after source moves", () =>
        {
            var f = new Fixture();
            ClientCard full = f.Add(Fullmetal, CardLocation.Hand);
            f.Chain(full, Desc(Fullmetal, 0), true);
            full.Location = CardLocation.Grave;
            full.SetId(MaxxC);
            full.Controller = 1;
            Stale(f, Time);
            IsCard(f.Select(HintMsg.Set, Card(Time, CardLocation.Deck), Card(Max, CardLocation.Deck)), Max);
        });
        Run("Lower chain link owns its tutor instead of latest link", () =>
        {
            var f = new Fixture();
            f.Chain(f.Add(Fullmetal, CardLocation.Hand), Desc(Fullmetal, 0), true);
            ClientCard enemy = f.Add(MaxxC, CardLocation.Grave, 1);
            f.Duel.CurrentChain.Add(enemy);
            f.Duel.CurrentChainInfo.Add(new ChainInfo(enemy, 1, -1));
            Stale(f, Time);
            IsCard(f.Select(HintMsg.Set, Card(Time, CardLocation.Deck), Card(Max, CardLocation.Deck)), Max);
        });
        Run("Fullmetal non-targeting burn maximizes original ATK over Link status", () =>
        {
            var f = new Fixture();
            ClientCard full = f.Add(Fullmetal, CardLocation.MonsterZone);
            f.Chain(full, Desc(Fullmetal, 1), true);
            f.Executor.SetCard(ExecutorType.Activate, full, Desc(Fullmetal, 1));
            ClientCard lowLink = f.Add(73539069, CardLocation.MonsterZone, 1);
            ClientCard high = f.Add(Fullmetal, CardLocation.MonsterZone, 1);
            Set(lowLink, "BaseAttack", 300);
            Set(lowLink, "Attack", 300);
            Set(high, "BaseAttack", 3400);
            Set(high, "Attack", 0);
            IsCard(f.Select(HintMsg.Faceup, lowLink, high), Fullmetal);
        });
        Run("Fullmetal burn uses snapshot after source leaves field", () =>
        {
            var f = new Fixture();
            ClientCard full = f.Add(Fullmetal, CardLocation.MonsterZone);
            f.Chain(full, Desc(Fullmetal, 1), true);
            full.Location = CardLocation.Grave;
            full.SetId(MaxxC);
            Stale(f, Time);
            ClientCard low = f.Add(Bmd, CardLocation.MonsterZone, 1);
            ClientCard high = f.Add(Metalzoa, CardLocation.MonsterZone, 1);
            Set(low, "BaseAttack", 600);
            Set(high, "BaseAttack", 3000);
            IsCard(f.Select(HintMsg.Faceup, low, high), Metalzoa);
        });
        Run("Time Engine takes free wipe after revive has consumed the grave target", () =>
        {
            var f = new Fixture();
            ClientCard time = f.Add(Time, CardLocation.SpellZone);
            f.Chain(time, Desc(Time, 0), true);
            f.Executor.SetCard(ExecutorType.Activate, time, Desc(Time, 0));
            f.Add(Fullmetal, CardLocation.MonsterZone);
            f.Add(Bmd, CardLocation.MonsterZone, 1);
            Require(f.AI.OnSelectYesNo(Desc(Time, 2)), "Free destruction declined after successful revive");
        });
        Run("Time Engine takes free burn after its wipe emptied the enemy board", () =>
        {
            var f = new Fixture();
            ClientCard time = f.Add(Time, CardLocation.SpellZone);
            f.Chain(time, Desc(Time, 0), true);
            f.Executor.SetCard(ExecutorType.Activate, time, Desc(Time, 0));
            Require(f.AI.OnSelectYesNo(Desc(Time, 3)), "Free damage declined");
        });
        Run("Resolution yes/no does not mutate activation bookkeeping", () =>
        {
            var f = new Fixture();
            ClientCard time = f.Add(Time, CardLocation.SpellZone);
            f.Chain(time, Desc(Time, 0), true);
            f.Executor.SetCard(ExecutorType.Activate, time, Desc(Time, 0));
            FieldInfo recent = ExecutorClass.GetField("_lastOwnLikelyDestroyedMonster", Flags);
            recent.SetValue(f.Executor, Bmd);
            Require(f.AI.OnSelectYesNo(Desc(Time, 3)), "Free damage rejected by old target heuristics");
            Require((int)recent.GetValue(f.Executor) == Bmd, "Resolution reran activation and consumed state");
        });
        Run("Max conversion keeps tribute and Fullmetal choices without duplicate queue", () =>
        {
            var f = new Fixture();
            ClientCard claws = f.Add(Claws, CardLocation.MonsterZone);
            ClientCard seal = f.Add(Seal, CardLocation.MonsterZone);
            ClientCard max = f.Add(Max, CardLocation.SpellZone);
            max.Position = (int)CardPosition.FaceDownDefence;
            Require(f.AI.OnSelectEffectYn(max, -1), "Claws conversion rejected");
            Require(Queued(f) == 0, "Callback-owned conversion left a duplicate selector");
            Stale(f, Illusionist);
            IsCard(f.Select(HintMsg.Release, seal, claws), Claws);
            f.Chain(max, -1, true);
            Stale(f, Time);
            IsCard(f.Select(HintMsg.SpSummon, Card(Metalzoa, CardLocation.Deck), Card(Fullmetal, CardLocation.Deck)), Fullmetal);
        });
        Run("Flame conversion keeps Claws tribute and Swordsman diversification", () =>
        {
            var f = new Fixture();
            ClientCard claws = f.Add(Claws, CardLocation.MonsterZone);
            ClientCard zoa = f.Add(Zoa, CardLocation.MonsterZone);
            ClientCard max = f.Add(Max, CardLocation.SpellZone);
            max.Position = (int)CardPosition.FaceDownDefence;
            ClientCard flame = f.Add(Flame, CardLocation.SpellZone);
            flame.Position = (int)CardPosition.FaceDownDefence;
            Require(f.AI.OnSelectEffectYn(flame, -1), "Flame conversion rejected");
            Require(Queued(f) == 0, "Flame left a duplicate selector");
            Stale(f, Time);
            // Existing strategy prefers Zoa when Metalzoa remains available.
            IsCard(f.Select(HintMsg.Release, claws, zoa), Zoa);
            f.Chain(flame, -1, true);
            IsCard(f.Select(HintMsg.SpSummon, Card(Fullmetal, CardLocation.Deck), Card(Swordsman, CardLocation.Deck)), Swordsman);
        });
        Run("Opponent-owned resolution is not hijacked by stale own candidate", () =>
        {
            var f = new Fixture();
            f.Chain(f.Add(Max, CardLocation.SpellZone, 1), -1, true);
            Stale(f, Time);
            var offered = new[] { Card(MaxxC, CardLocation.Hand), Card(Fullmetal, CardLocation.Hand) };
            Require(f.Executor.OnSelectCard(offered, 1, 1, HintMsg.SpSummon, false) == null, "Own policy claimed opponent effect");
        });
        Run("Chain callback preserves protocol-owned target observations", () =>
        {
            var f = new Fixture();
            ClientCard target = f.Add(Fullmetal, CardLocation.MonsterZone);
            f.Duel.LastChainTargets.Add(target);
            f.AI.OnChaining(f.Add(MaxxC, CardLocation.Grave, 1), 1);
            Require(f.Duel.LastChainTargets.Contains(target), "Executor erased protocol target state");
        });
        Run("BMD grave search preserves REDMD extender priority", () =>
        {
            var f = new Fixture();
            f.Chain(f.Add(Bmd, CardLocation.Grave), -1, true);
            f.Add(73539069, CardLocation.MonsterZone);
            Stale(f, Time);
            IsCard(f.Select(HintMsg.AddToHand, Card(Claws, CardLocation.Deck), Card(Redmd, CardLocation.Deck)), Redmd);
        });
        Run("Seal float preserves Magnamhut priority after source removal", () =>
        {
            var f = new Fixture();
            ClientCard seal = f.Add(Seal, CardLocation.Grave);
            f.Chain(seal, Desc(Seal, 1), true);
            seal.Location = CardLocation.Removed;
            Stale(f, Time);
            IsCard(f.Select(HintMsg.SpSummon, Card(Claws, CardLocation.Deck), Card(Magnamhut, CardLocation.Deck)), Magnamhut);
        });
        Run("Fullmetal field negate cannot fall through the hand tutor", () =>
        {
            var f = new Fixture();
            ClientCard full = f.Add(Fullmetal, CardLocation.MonsterZone);
            f.Chain(f.Add(MaxxC, CardLocation.Grave, 1), -1, false);
            Require(!f.AI.OnSelectEffectYn(full, Desc(Fullmetal, 1)), "Hand tutor overrode deliberate negate hold");
        });
        Run("Accepted prompt state expires at chain end and cannot leak to another duel", () =>
        {
            var f = new Fixture();
            ClientCard max = f.Add(Max, CardLocation.SpellZone);
            max.Position = (int)CardPosition.FaceDownDefence;
            f.Add(Claws, CardLocation.MonsterZone);
            Require(f.AI.OnSelectEffectYn(max, -1), "Conversion rejected");
            f.ClearChain();
            Stale(f, Time);
            var offers = new[] { Card(MaxxC, CardLocation.Hand), Card(Fullmetal, CardLocation.Hand) };
            Require(f.Executor.OnSelectCard(offers, 1, 1, HintMsg.SpSummon, false) == null, "Ended chain retained prompt ownership");
            var other = new Fixture();
            Require(other.Executor.OnSelectCard(offers, 1, 1, HintMsg.SpSummon, false) == null, "Prompt state crossed duels");
        });
        Run("Striker material prompt retains accepted summon after another candidate was inspected", () =>
        {
            var f = new Fixture();
            ClientCard bmd = f.Add(Bmd, CardLocation.MonsterZone);
            ClientCard other = f.Add(Illusionist, CardLocation.MonsterZone);
            Require(f.OfferSummon(Card(73539069, CardLocation.Extra), true).Action == MainPhaseAction.MainAction.SpSummon,
                "Striker summon rejected");
            Stale(f, Time);
            IsCard(f.Select(HintMsg.LinkMaterial, other, bmd), Bmd);
            f.AI.OnSpSummoned();
            Require(f.Executor.OnSelectCard(new[] { other, bmd }, 1, 1, HintMsg.LinkMaterial, false) == null,
                "Completed special summon retained material ownership");
        });
        Run("Canceled conversion cannot claim another phase's prompt", () =>
        {
            var f = new Fixture();
            ClientCard max = f.Add(Max, CardLocation.SpellZone);
            max.Position = (int)CardPosition.FaceDownDefence;
            f.Add(Claws, CardLocation.MonsterZone);
            Require(f.AI.OnSelectEffectYn(max, -1), "Conversion rejected");
            f.Duel.Phase = DuelPhase.End;
            f.AI.OnNewPhase();
            Stale(f, Time);
            var offers = new[] { Card(MaxxC, CardLocation.Hand), Card(Fullmetal, CardLocation.Hand) };
            Require(f.Executor.OnSelectCard(offers, 1, 1, HintMsg.SpSummon, false) == null, "Phase change retained an abandoned prompt");
        });
        Run("Negated Max does not consume the successful opponent-turn conversion budget", () =>
        {
            var f = new Fixture();
            f.Duel.Player = 1;
            ClientCard max = f.Add(Max, CardLocation.SpellZone);
            max.Position = (int)CardPosition.FaceDownDefence;
            f.Add(Magnamhut, CardLocation.MonsterZone);
            Require(f.AI.OnSelectEffectYn(max, -1), "Opponent conversion rejected");
            f.Chain(max, -1, true);
            f.Duel.NegatedChainIndexList.Add(1);
            f.AI.OnChainSolved(1);
            Require((int)ExecutorClass.GetField("_oppTurnMetallizeResolvedCount", Flags).GetValue(f.Executor) == 0,
                "Negated trap consumed successful conversion budget");
        });
        Run("Resolved Max conversion count uses original chain identity after source changes", () =>
        {
            var f = new Fixture();
            f.Duel.Player = 1;
            ClientCard max = f.Add(Max, CardLocation.SpellZone);
            max.Position = (int)CardPosition.FaceDownDefence;
            f.Add(Magnamhut, CardLocation.MonsterZone);
            Require(f.AI.OnSelectEffectYn(max, -1), "Opponent conversion rejected");
            f.Chain(max, -1, true);
            max.SetId(MaxxC);
            f.AI.OnChainSolved(1);
            Require((int)ExecutorClass.GetField("_oppTurnMetallizeResolvedCount", Flags).GetValue(f.Executor) == 1,
                "Resolved conversion lost its immutable identity");
        });
        Run("Phoenix owns discard 501 and destruction 502 without stale queues", () =>
        {
            var f = new Fixture();
            ClientCard phoenix = f.Add(2857636, CardLocation.MonsterZone);
            ClientCard keep = f.Add(Max, CardLocation.Hand);
            ClientCard discard = f.Add(14558127, CardLocation.Hand);
            ClientCard low = f.Add(Max, CardLocation.SpellZone, 1);
            low.Position = (int)CardPosition.FaceDownDefence;
            ClientCard floodgate = f.Add(61740673, CardLocation.SpellZone, 1);
            Require(f.Activate(phoenix, Desc(2857636, 0)), "Phoenix trigger rejected");
            Require(Queued(f) == 0, "Phoenix left duplicate cost/target selectors");
            Stale(f, Time);
            IsCard(f.Select(HintMsg.Discard, keep, discard), 14558127);
            IsCard(f.Select(HintMsg.Destroy, low, floodgate), 61740673);
            Require(Queued(f) == 0, "Phoenix target left an unconsumed selector");
        });
        Run("Accesscode ATK target uses the largest offered Link material", () =>
        {
            var f = new Fixture();
            ClientCard access = f.Add(86066372, CardLocation.MonsterZone);
            ClientCard low = f.Add(73539069, CardLocation.Grave);
            ClientCard high = f.Add(2857636, CardLocation.Grave);
            Require(f.Activate(access, Desc(86066372, 0)), "ATK trigger rejected");
            Require(Queued(f) == 0, "ATK trigger left duplicate target selector");
            Stale(f, Time);
            IsCard(f.Select(HintMsg.Target, low, high), 2857636);
        });
        Run("Accesscode accepts ATK trigger with only face-up banished materials", () =>
        {
            var f = new Fixture();
            ClientCard access = f.Add(86066372, CardLocation.MonsterZone);
            ClientCard low = f.Add(73539069, CardLocation.Removed);
            ClientCard high = f.Add(2857636, CardLocation.Removed);
            Require(f.Activate(access, Desc(86066372, 0)), "Banished material trigger rejected");
            IsCard(f.Select(HintMsg.Target, low, high), 2857636);
        });
        Run("Accesscode preserves banish cost priority and handles non-target destruction 502", () =>
        {
            var f = new Fixture();
            ClientCard access = f.Add(86066372, CardLocation.MonsterZone);
            ClientCard seal = f.Add(Seal, CardLocation.Grave);
            ClientCard striker = f.Add(73539069, CardLocation.Grave);
            ClientCard low = f.Add(Max, CardLocation.SpellZone, 1);
            low.Position = (int)CardPosition.FaceDownDefence;
            ClientCard floodgate = f.Add(61740673, CardLocation.SpellZone, 1);
            Require(f.Activate(access, Desc(86066372, 1)), "Destruction effect rejected");
            Require(Queued(f) == 0, "Accesscode left duplicate cost/removal selectors");
            IsCard(f.Select(HintMsg.Remove, striker, seal), Seal);
            f.Duel.SolvingChainIndex = 1;
            Stale(f, Time);
            IsCard(f.Select(HintMsg.Destroy, low, floodgate), 61740673);
            Require(Queued(f) == 0, "Destruction left an unconsumed selector");
        });
    }

    private static string Hash(string file)
    {
        using (var sha = SHA256.Create())
        using (var stream = File.OpenRead(file))
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
    }

    public static int MainEntry(string[] args)
    {
        if (args.Length != 3) return 2;
        NamedCardsManager.Init(Path.GetFullPath(args[0]));
        typeof(Executor).Assembly.GetType("WindBot.Program").GetField("Rand", Flags).SetValue(null, new Random(20261102));
        bool extra = false;
        foreach (string raw in File.ReadAllLines(args[1]))
        {
            string line = raw.Trim();
            if (line == "#extra") { extra = true; continue; }
            if (line == "!side") break;
            int id;
            if (int.TryParse(line, out id)) (extra ? Extra : MainDeck).Add(id);
        }
        Effects();
        var report = new Dictionary<string, object>
        {
            { "format", "metalmorph-focused-checks-v1" },
            { "evidence", "Synthetic legal-offer, dispatcher and prompt fixtures; no Lua or duel results." },
            { "executableSha256", Hash(typeof(Executor).Assembly.Location) },
            { "databaseSha256", Hash(args[0]) }, { "deckSha256", Hash(args[1]) },
            { "passed", Results.All(item => (bool)item["passed"]) }, { "checks", Results }
        };
        File.WriteAllText(args[2], new JavaScriptSerializer().Serialize(report));
        return Results.All(item => (bool)item["passed"]) ? 0 : 1;
    }

    public static int Main(string[] args) { return MainEntry(args); }
}
