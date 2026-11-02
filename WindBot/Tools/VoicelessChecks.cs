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
// These checks do not execute Lua; use voiceless_native_checks.py separately.
internal static class VoicelessChecks
{
    private const int Lo = 25801745, Saffira = 51296484, Skull = 10774240;
    private const int Barrier = 98477480, Prayers = 52472775, Radiance = 86310763;
    private const int Blessing = 39114494, Sage = 88284599, Sauravis = 4810828;
    private const int Diviner = 92919429, Trias = 26866984, PrePrep = 13048472;
    private const int Dyna = 73898890, Linkuriboh = 41999284, Gardna = 2220237;
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    private static readonly List<Dictionary<string, object>> Results = new List<Dictionary<string, object>>();
    private static List<int> MainDeck = new List<int>();
    private static List<int> Extra = new List<int>();

    private sealed class Fixture
    {
        internal readonly Duel Duel = new Duel();
        internal readonly GameAI AI;
        internal readonly VoicelessExecutor Executor;
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
            var client = new GameClient(new WindBotInfo { Chat = false, Deck = "Voiceless", Dialog = "default" });
            AI = new GameAI(client, Duel);
            Executor = new VoicelessExecutor(AI, Duel);
            AI.Executor = Executor;
            Executor.Deck = "AI_Voiceless";
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
        return new ClientCard(id, location, 0, (int)CardPosition.FaceUpAttack) { Controller = 0 };
    }

    private static int Desc(int id, int index) { return id * 16 + index; }
    private static HashSet<ClientCard> RedirectedTrias(Fixture fixture)
    {
        return (HashSet<ClientCard>)typeof(VoicelessExecutor).GetField("_voicelessRedirectedTrias", Flags).GetValue(fixture.Executor);
    }
    private static int ModeledRedirectedTrias(Fixture fixture)
    {
        object state = typeof(VoicelessExecutor).GetMethod("CaptureVoicelessState", Flags).Invoke(fixture.Executor, null);
        return (int)state.GetType().GetField("RedirectedTrias", Flags).GetValue(state);
    }
    private static ClientCard ResolveTrias(Fixture fixture)
    {
        ClientCard trias = fixture.Add(Trias, CardLocation.Grave);
        fixture.Chain(trias, Desc(Trias, 0), true);
        fixture.Bot.Graveyard.Remove(trias);
        fixture.Bot.MonsterZone[0] = trias;
        trias.Location = CardLocation.MonsterZone;
        trias.Sequence = 0;
        fixture.AI.OnChainSolved(1);
        return trias;
    }
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

    private static void Effects()
    {
        Run("Lo normal summon starts an empty-board route", () =>
        {
            var f = new Fixture();
            ClientCard lo = f.Add(Lo, CardLocation.Hand);
            Require(f.OfferSummon(lo, false).Action == MainPhaseAction.MainAction.Summon, "Lo opening was rejected");
        });
        Run("Lo placement prompt works without activation preselection", () =>
        {
            var f = new Fixture();
            ClientCard lo = f.Add(Lo, CardLocation.MonsterZone);
            f.Chain(lo, Desc(Lo, 0), true);
            IsCard(f.Select(HintMsg.ToField, Card(Radiance, CardLocation.Deck), Card(Blessing, CardLocation.Deck), Card(Barrier, CardLocation.Deck)), Barrier);
        });
        Run("Face-up Barrier searches Saffira for Lo", () =>
        {
            var f = new Fixture();
            f.Add(Lo, CardLocation.MonsterZone);
            ClientCard barrier = f.Add(Barrier, CardLocation.SpellZone);
            Require(f.Activate(barrier, Desc(Barrier, 0)), "Barrier field search rejected");
            f.Duel.SolvingChainIndex = 1;
            IsCard(f.Select(HintMsg.AddToHand, Card(Skull, CardLocation.Deck), Card(Saffira, CardLocation.Deck), Card(Lo, CardLocation.Deck)), Saffira);
        });
        Run("Saffira hand dump and optional ritual search route by snapshot", () =>
        {
            var f = new Fixture();
            f.Add(Lo, CardLocation.MonsterZone);
            ClientCard saffira = f.Add(Saffira, CardLocation.Hand);
            Require(f.Activate(saffira, Desc(Saffira, 0)), "Saffira hand activation rejected");
            f.Bot.Hand.Remove(saffira);
            f.Bot.Graveyard.Add(saffira);
            saffira.Location = CardLocation.Grave;
            f.Duel.SolvingChainIndex = 1;
            f.Executor.SetCard(ExecutorType.Activate, Card(Sauravis, CardLocation.Hand), -1);
            IsCard(f.Select(HintMsg.ToGrave, Card(Prayers, CardLocation.Deck)), Prayers);
            f.Add(Prayers, CardLocation.Grave);
            Require(f.AI.OnSelectYesNo(Desc(Saffira, 1)), "Real script optional search description rejected");
            IsCard(f.Select(HintMsg.AddToHand, Card(Sauravis, CardLocation.Deck), Card(Skull, CardLocation.Deck)), Skull);
        });
        Run("Saffira grave ritual is recognized with script description zero", () =>
        {
            var f = new Fixture();
            f.Add(Lo, CardLocation.MonsterZone);
            ClientCard skull = f.Add(Skull, CardLocation.Hand);
            ClientCard saffira = f.Add(Saffira, CardLocation.Grave);
            Require(f.Activate(saffira, 0), "Saffira grave ritual rejected");
            f.Duel.SolvingChainIndex = 1;
            IsCard(f.Select(HintMsg.SpSummon, Card(Sauravis, CardLocation.Hand), skull), Skull);
        });
        Run("Ritual material selection uses Lo as the entire tribute", () =>
        {
            var f = new Fixture();
            ClientCard lo = f.Add(Lo, CardLocation.MonsterZone);
            ClientCard trias = f.Add(Trias, CardLocation.Hand);
            f.Add(Skull, CardLocation.Hand);
            ClientCard saffira = f.Add(Saffira, CardLocation.Grave);
            f.Chain(saffira, 0, true);
            lo.OpParam1 = 1;
            lo.OpParam2 = 7;
            trias.OpParam1 = 9;
            IList<ClientCard> selected = f.AI.OnSelectSum(new[] { trias, lo }, new ClientCard[0], 7, 1, 2, HintMsg.Release, false);
            IsCard(selected, Lo);
        });
        Run("Simultaneous Skull and Lo triggers put Skull first", () =>
        {
            var f = new Fixture();
            ClientCard skull = f.Add(Skull, CardLocation.MonsterZone);
            ClientCard lo = f.Add(Lo, CardLocation.Grave);
            int choice = f.AI.OnSelectChain(new[] { lo, skull }, new[] { Desc(Lo, 1), Desc(Skull, 0) }, new[] { false, false });
            Require(choice == 1, "Skull was not selected as the first chain link");
            f.Chain(skull, Desc(Skull, 0), false);
            f.AI.OnChaining(skull, 0);
            Require(f.AI.OnSelectChain(new[] { lo }, new[] { Desc(Lo, 1) }, new[] { false }) == 0, "Lo was not accepted as the following link");
        });
        Run("Skull sole optional trigger accepts protocol description minus one", () =>
        {
            var f = new Fixture();
            ClientCard skull = f.Add(Skull, CardLocation.MonsterZone);
            f.Duel.LastSummonedCards.Add(skull);
            Require(f.Activate(skull, -1), "Sole Skull search confirmation was rejected");
            f.Duel.SolvingChainIndex = 1;
            IsCard(f.Select(HintMsg.AddToHand, Card(Sauravis, CardLocation.Deck), Card(Lo, CardLocation.Deck)), Lo);
        });
        Run("Diviner sends Trias when Lo access is needed", () =>
        {
            var f = new Fixture();
            ClientCard diviner = f.Add(Diviner, CardLocation.MonsterZone);
            f.Chain(diviner, Desc(Diviner, 0), true);
            IsCard(f.Select(HintMsg.ToGrave, Card(79606837, CardLocation.Extra), Card(Trias, CardLocation.Deck), Card(80532587, CardLocation.Extra)), Trias);
        });
        Run("Trias preserves Lo and tributes Diviner", () =>
        {
            var f = new Fixture();
            ClientCard diviner = f.Add(Diviner, CardLocation.MonsterZone);
            ClientCard lo = f.Add(Lo, CardLocation.MonsterZone);
            ClientCard trias = f.Add(Trias, CardLocation.Grave);
            Require(f.Activate(trias, Desc(Trias, 0)), "Trias activation rejected");
            IsCard(f.Select(HintMsg.Release, lo, diviner), Diviner);
        });
        Run("Dyna revival works before an opponent monster exists", () =>
        {
            var f = new Fixture();
            f.Duel.Player = 1;
            f.Duel.Turn = 2;
            f.AI.OnNewTurn();
            ClientCard dyna = f.Add(Dyna, CardLocation.MonsterZone);
            ClientCard skull = f.Add(Skull, CardLocation.Grave);
            f.Add(Lo, CardLocation.Grave);
            typeof(ClientCard).GetProperty("ProcCompleted").GetSetMethod(true).Invoke(skull, new object[] { 1 });
            Require(f.Activate(dyna, Desc(Dyna, 1)), "Opponent-turn Dyna revival rejected");
            IsCard(f.Select(HintMsg.Target, skull), Skull);
            f.Duel.SolvingChainIndex = 1;
            Require(f.AI.OnSelectOption(new[] { 1190, 1152 }) == 1, "Dyna selected add-to-hand instead of Special Summon");
        });
        Run("Blessing recovers Lo for the Linkuriboh route", () =>
        {
            var f = new Fixture();
            f.Add(Skull, CardLocation.Hand);
            f.Add(Linkuriboh, CardLocation.MonsterZone);
            ClientCard lo = f.Add(Lo, CardLocation.Grave);
            ClientCard saffira = f.Add(Saffira, CardLocation.Grave);
            ClientCard blessing = f.Add(Blessing, CardLocation.SpellZone);
            Require(f.Activate(blessing, Desc(Blessing, 1)), "Blessing recovery rejected");
            IsCard(f.Select(HintMsg.AddToHand, saffira, lo), Lo);
        });
        Run("Blessing ritual trigger selects Skull Guardian", () =>
        {
            var f = new Fixture();
            f.Add(Lo, CardLocation.Hand);
            ClientCard skull = f.Add(Skull, CardLocation.Hand);
            ClientCard blessing = f.Add(Blessing, CardLocation.SpellZone);
            f.Add(Gardna, CardLocation.MonsterZone);
            Require(f.Activate(blessing, Desc(Blessing, 2)), "Blessing ritual trigger rejected");
            f.Duel.SolvingChainIndex = 1;
            IsCard(f.Select(HintMsg.SpSummon, skull), Skull);
        });
        Run("Blessing sole ritual trigger accepts protocol description minus one", () =>
        {
            var f = new Fixture();
            f.Add(Lo, CardLocation.Hand);
            ClientCard skull = f.Add(Skull, CardLocation.Hand);
            ClientCard blessing = f.Add(Blessing, CardLocation.SpellZone);
            ClientCard gardna = f.Add(Gardna, CardLocation.MonsterZone);
            f.Duel.LastSummonedCards.Add(gardna);
            Require(f.Activate(blessing, -1), "Sole Blessing ritual confirmation was rejected");
            f.Duel.SolvingChainIndex = 1;
            IsCard(f.Select(HintMsg.SpSummon, Card(Sauravis, CardLocation.Hand), skull), Skull);
        });
        Run("Dragon Sage recycling selects a Ritual Spell and a second Spell", () =>
        {
            var f = new Fixture();
            ClientCard sage = f.Add(Sage, CardLocation.Hand);
            ClientCard prayers = f.Add(Prayers, CardLocation.Grave);
            ClientCard prePrep = f.Add(PrePrep, CardLocation.Grave);
            ClientCard barrier = f.Add(Barrier, CardLocation.Hand);
            Require(f.OfferSummon(sage, true).Action == MainPhaseAction.MainAction.SpSummon, "Legal Sage summon rejected");
            IList<ClientCard> selected = f.AI.OnSelectCard(new[] { barrier, prePrep, prayers }, 2, 2, HintMsg.ToDeck, false);
            Require(selected.Count == 2 && selected.Contains(prayers) && selected.Contains(prePrep), "Sage did not preserve the live Barrier in hand");
        });
        Run("Dragon Sage selects Sauravis alongside existing Skull Guardian", () =>
        {
            var f = new Fixture();
            f.Duel.Player = 1;
            f.Add(Skull, CardLocation.MonsterZone);
            f.Add(Lo, CardLocation.Grave);
            ClientCard sage = f.Add(Sage, CardLocation.MonsterZone);
            f.Chain(sage, Desc(Sage, 0), true);
            IsCard(f.Select(HintMsg.SpSummon, Card(Skull, CardLocation.Deck), Card(Sauravis, CardLocation.Deck)), Sauravis);
        });
        Run("Interrupted Saffira search can use already-held Prayers", () =>
        {
            var f = new Fixture();
            f.Add(Lo, CardLocation.MonsterZone);
            f.Add(Skull, CardLocation.Hand);
            ClientCard prayers = f.Add(Prayers, CardLocation.Hand);
            f.Add(Saffira, CardLocation.Grave);
            f.ClearChain();
            Require(f.Activate(prayers, 0), "Independent ritual route rejected after interrupted search");
        });
        Run("Radiance selects two distinct offered targets with two Rituals", () =>
        {
            var f = new Fixture();
            f.Duel.Player = 1;
            f.Add(Lo, CardLocation.MonsterZone);
            f.Add(Skull, CardLocation.MonsterZone);
            f.Add(Sauravis, CardLocation.MonsterZone);
            ClientCard radiance = f.Add(Radiance, CardLocation.SpellZone);
            ClientCard first = f.Add(89631139, CardLocation.MonsterZone, 1);
            ClientCard second = f.Add(46986414, CardLocation.MonsterZone, 1);
            Require(f.Activate(radiance, Desc(Radiance, 1)), "Radiance removal rejected");
            IList<ClientCard> targets = f.AI.OnSelectCard(new[] { first, second }, 1, 2, HintMsg.Destroy, false);
            Require(targets.Count == 2 && targets.Distinct().Count() == 2 && targets.Contains(first) && targets.Contains(second), "Radiance failed to use both available removals");
        });
        Run("Radiance recovery can summon Lo after interruption", () =>
        {
            var f = new Fixture();
            f.Add(Skull, CardLocation.MonsterZone);
            ClientCard radiance = f.Add(Radiance, CardLocation.SpellZone);
            ClientCard prayers = f.Add(Prayers, CardLocation.Grave);
            Require(f.Activate(radiance, Desc(Radiance, 0)), "Radiance recovery rejected without Lo");
            IsCard(f.Select(HintMsg.ToDeck, prayers), Prayers);
            f.Duel.SolvingChainIndex = 1;
            IsCard(f.Select(HintMsg.OperateCard, Card(Saffira, CardLocation.Deck), Card(Lo, CardLocation.Deck)), Lo);
            Require(f.AI.OnSelectOption(new[] { 1190, 1152 }) == 1, "Radiance should Special Summon recovery Lo");
        });
        Run("Skull Guardian handles an unknown opponent activation safely", () =>
        {
            var f = new Fixture();
            ClientCard skull = f.Add(Skull, CardLocation.MonsterZone);
            f.Add(Lo, CardLocation.MonsterZone);
            ClientCard opponent = f.Add(0, CardLocation.MonsterZone, 1);
            f.Chain(opponent, -1, false);
            Require(f.AI.OnSelectEffectYn(skull, Desc(Skull, 1)), "Unknown opponent identity broke the offered negate");
        });
        Run("Resolution context overrides stale executor candidate fields", () =>
        {
            var f = new Fixture();
            ClientCard enemy = f.Add(0, CardLocation.MonsterZone, 1);
            f.Chain(enemy, -1, true);
            f.Executor.SetCard(ExecutorType.Activate, Card(Saffira, CardLocation.Hand), Desc(Saffira, 0));
            IList<ClientCard> result = f.Executor.OnSelectCard(new[] { Card(Lo, CardLocation.Deck), Card(Skull, CardLocation.Deck) }, 1, 1, HintMsg.AddToHand, false);
            Require(result == null, "Unrelated resolving effect inherited Saffira selection context");
        });
        Run("Missing preferred search target retains an offered legal fallback", () =>
        {
            var f = new Fixture();
            f.Add(Lo, CardLocation.MonsterZone);
            ClientCard barrier = f.Add(Barrier, CardLocation.SpellZone);
            f.Chain(barrier, Desc(Barrier, 0), true);
            IsCard(f.Select(HintMsg.AddToHand, Card(Radiance, CardLocation.Deck)), Radiance);
        });
        Run("Observed effect use is retained until the next turn reset", () =>
        {
            var f = new Fixture();
            f.Add(Lo, CardLocation.MonsterZone);
            ClientCard barrier = f.Add(Barrier, CardLocation.SpellZone);
            Require(f.Activate(barrier, Desc(Barrier, 0)), "Initial Barrier effect rejected");
            f.ClearChain();
            MethodInfo used = typeof(VoicelessExecutor).GetMethod("VoicelessEffectUsed", Flags);
            Require((bool)used.Invoke(f.Executor, new object[] { Barrier, 0 }), "Accepted use disappeared at chain end");
            f.Duel.Turn++;
            f.AI.OnNewTurn();
            Require(!(bool)used.Invoke(f.Executor, new object[] { Barrier, 0 }), "Barrier did not reset on the next turn");
        });
        Run("Rejected effect does not leave queued future card selections", () =>
        {
            var f = new Fixture();
            ClientCard dyna = f.Add(Dyna, CardLocation.MonsterZone);
            f.Add(Skull, CardLocation.Grave);
            Require(!f.AI.OnSelectEffectYn(dyna, Desc(Dyna, 0)), "Dyna should preserve the Ritual instead of shuffling our own card");
            var queue = (ICollection)typeof(GameAI).GetField("m_selector", Flags).GetValue(f.AI);
            Require(queue.Count == 0, "Rejected candidate left queued selections");
        });
        Run("Trias banish-on-leave provenance survives a turn boundary", () =>
        {
            var f = new Fixture();
            ClientCard trias = ResolveTrias(f);
            Require(RedirectedTrias(f).Contains(trias), "Successful self-summon provenance was not observed");
            f.ClearChain();
            f.Duel.Turn++;
            f.Duel.Player = 1;
            f.AI.OnNewTurn();
            Require(RedirectedTrias(f).Contains(trias), "Trias redirect incorrectly expired with its once-per-turn flag");
            Require(ModeledRedirectedTrias(f) == 1, "Planner lost the observed redirect on the following turn");
            f.Bot.MonsterZone[0] = null;
            f.Bot.Banished.Add(trias);
            trias.Location = CardLocation.Removed;
            f.AI.OnMove(trias, 0, (int)CardLocation.MonsterZone, 0, (int)CardLocation.Removed);
            Require(!RedirectedTrias(f).Contains(trias), "Departed Trias left stale field-instance provenance");
            Require(ModeledRedirectedTrias(f) == 0, "Planner retained a departed Trias redirect");
        });
        Run("Setting Trias face-down retires the script redirect", () =>
        {
            var f = new Fixture();
            ClientCard trias = ResolveTrias(f);
            f.ClearChain();
            trias.Position = (int)CardPosition.FaceDownDefence;
            f.AI.OnPosChange(trias, (int)CardPosition.FaceUpAttack, (int)CardPosition.FaceDownDefence);
            Require(!RedirectedTrias(f).Contains(trias), "Face-down RESET_TURN_SET left an obsolete redirect");
        });
        Run("Negated Trias does not acquire successful-summon provenance", () =>
        {
            var f = new Fixture();
            ClientCard trias = f.Add(Trias, CardLocation.Grave);
            f.Chain(trias, Desc(Trias, 0), true);
            f.Duel.NegatedChainIndexList.Add(1);
            f.AI.OnChainSolved(1);
            Require(!RedirectedTrias(f).Contains(trias), "A negated effect was treated as a successful self-summon");
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
        typeof(VoicelessExecutor).Assembly.GetType("WindBot.Program").GetField("Rand", Flags).SetValue(null, new Random(20261102));
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
            { "format", "voiceless-focused-checks-v1" },
            { "evidence", "Synthetic legal-offer, dispatcher and prompt fixtures; no Lua or duel results." },
            { "executableSha256", Hash(typeof(VoicelessExecutor).Assembly.Location) },
            { "databaseSha256", Hash(args[0]) }, { "deckSha256", Hash(args[1]) },
            { "passed", Results.All(item => (bool)item["passed"]) }, { "checks", Results }
        };
        File.WriteAllText(args[2], new JavaScriptSerializer().Serialize(report));
        return Results.All(item => (bool)item["passed"]) ? 0 : 1;
    }

    public static int Main(string[] args) { return MainEntry(args); }
}
