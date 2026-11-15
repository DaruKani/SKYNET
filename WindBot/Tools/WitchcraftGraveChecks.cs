using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using WindBot;
using WindBot.Game;
using WindBot.Game.AI;
using WindBot.Game.AI.Decks;
using YGOSharp.OCGWrapper;
using YGOSharp.OCGWrapper.Enums;

internal static partial class WitchcraftOpponentChecks
{
    private static void GraveChecks()
    {
        Type exType = typeof(Witchcraft2026Executor), planner = exType.GetNestedType("WPlanner", F);
        Type zone = exType.GetNestedType("WZone", F);
        Func<object> empty = () => Activator.CreateInstance(exType.GetNestedType("WState", F), true);
        Action<object, int, string> add = (s, id, z) => Call(s, "Add", id, Enum.Parse(zone, z), 1);
        Func<object, List<object>> acts = s => ((IEnumerable)planner.GetMethod("Actions", F).Invoke(null, new[] { s })).Cast<object>().ToList();
        Func<object, string, List<object>> mode = (s, kind) => acts(s).Where(a => Get(a, "Kind").ToString() == kind).ToList();
        Func<object, object, object> apply = (s, a) => planner.GetMethod("Apply", F).Invoke(null, new[] { s, a });
        Func<object, object, object> deny = (s, a) => exType.GetMethod("DeniedAction", F).Invoke(null, new[] { s, a });
        Func<object, int, string, int> count = (s, id, z) => (int)Call(s, "Count", id, Enum.Parse(zone, z));
        Func<object, double> value = s => (double)planner.GetMethod("Evaluate", F).Invoke(null, new[] { s });

        object seed = empty(); add(seed, 59106048, "Grave"); add(seed, 83289866, "Hand"); add(seed, 83289866, "Backrow");
        add(seed, 57916305, "Deck"); add(seed, 6071005, "Deck");
        object redraw = mode(seed, "SeedRedraw").Single(); object drawn = apply(seed, redraw);
        Check("Seed GY produces profitable redraw for redundant protection", value(drawn) > value(seed));
        Check("Seed draw is unknown and stops speculative continuation", (bool)Get(drawn, "RandomBoundary") && (int)Get(drawn, "UnknownHand") == 1 && acts(drawn).Count == 0 && count(drawn, 57916305, "Hand") == 0);
        object denied = deny(seed, redraw);
        Check("negated Seed banishes only cost, preserves hand and does not draw", count(denied, 59106048, "Banished") == 1 && count(denied, 83289866, "Hand") == 1 && (int)Get(denied, "UnknownHand") == 0);
        object noSpell = empty(); add(noSpell, 59106048, "Grave"); add(noSpell, 14558127, "Hand"); add(noSpell, 57916305, "Deck");
        Check("Seed cannot redraw a hand without a Spell", mode(noSpell, "SeedRedraw").Count == 0);
        object preserve = empty(); add(preserve, 59106048, "Grave"); add(preserve, 57916305, "Hand"); add(preserve, 58139997, "Deck");
        Check("Seed does not assign positive cycling value to a useful starter over dead high monster", mode(preserve, "SeedRedraw").All(a => value(apply(preserve, a)) < value(preserve)));

        object pittore = empty(); add(pittore, 95245544, "Grave"); add(pittore, 83289866, "Hand"); add(pittore, 83289866, "Backrow"); add(pittore, 57916305, "Deck");
        object draw = mode(pittore, "PittoreDraw").Single(); object pittoreAfter = apply(pittore, draw);
        Check("Pittore models draw then Witch send with no invented draw identity", count(pittoreAfter, 95245544, "Banished") == 1 && count(pittoreAfter, 83289866, "Grave") == 1 && (int)Get(pittoreAfter, "UnknownHand") == 1);
        Check("negated Pittore retains its planned resolution discard", count(deny(pittore, draw), 83289866, "Hand") == 1);
        object risky = empty(); add(risky, 95245544, "Grave"); add(risky, 14558127, "Hand"); add(risky, 57916305, "Deck");
        Check("Pittore deliberate safety policy does not risk banishing valuable non-Witch hand", mode(risky, "PittoreDraw").Count == 0);
        object emptyHand = empty(); add(emptyHand, 95245544, "Grave"); add(emptyHand, 57916305, "Deck");
        Check("empty-hand Pittore has a modeled chance of GY setup, not a kept draw", mode(emptyHand, "PittoreDraw").Count == 1 && (int)Get(apply(emptyHand, mode(emptyHand, "PittoreDraw")[0]), "UnknownHand") == 0);

        object pot = empty(); add(pot, 59851535, "Grave"); add(pot, 57916305, "Grave");
        object reclaim = mode(pot, "PotterieRecover").Single();
        Check("Potterie recovers a real Witch card from empty hand", count(apply(pot, reclaim), 57916305, "Hand") == 1 && count(apply(pot, reclaim), 59851535, "Banished") == 1);
        Check("negated Potterie spends banish but does not recover target", count(deny(pot, reclaim), 57916305, "Grave") == 1);
        Set(pot, "UnknownHand", 1);
        Check("Potterie respects unknown cards in hand", mode(pot, "PotterieRecover").Count == 0);
        object extraRecover = empty(); add(extraRecover, 59851535, "Grave"); add(extraRecover, 33475154, "Grave");
        Check("Potterie returns Fusion to Extra Deck rather than fabricating a hand monster", count(apply(extraRecover, mode(extraRecover, "PotterieRecover").Single()), 33475154, "Extra") == 1);

        object distortion = empty(); add(distortion, 69748261, "Grave"); add(distortion, 6071005, "Deck"); add(distortion, 58139997, "Hand");
        Check("Distortion never assumes GY age without evidence", mode(distortion, "DistortionSearch").Count == 0);
        Set(distortion, "DistortionReady", true);
        object search = mode(distortion, "DistortionSearch").First(a => (int)Get(a, "Cost") == 58139997);
        object searched = apply(distortion, search);
        Check("Distortion searches high Spellcaster then discards at resolution", count(searched, 6071005, "Hand") == 1 && count(searched, 58139997, "Grave") == 1 && count(searched, 69748261, "Banished") == 1);
        Check("negated Distortion leaves discard in hand and target in Deck", count(deny(distortion, search), 58139997, "Hand") == 1 && count(deny(distortion, search), 6071005, "Deck") == 1);
        Set(distortion, "OpponentTurn", true);
        Check("Distortion GY is Quick, not own-main ignition only", mode(distortion, "DistortionSearch").Count > 0);

        object copy = empty(); add(copy, 64756282, "Grave"); add(copy, 6958567, "Grave"); add(copy, 21744288, "Grave"); add(copy, 9603252, "Extra");
        object fusion = mode(copy, "Fusion").First(a => (int)Get(a, "CopiedSpell") == 6958567);
        object fusionAfter = apply(copy, fusion);
        Check("Genni copied Celebration can recycle Genni from banishment AFTER paying copy cost", count(fusionAfter, 9603252, "Board") == 1 && count(fusionAfter, 64756282, "Deck") == 1 && count(fusionAfter, 6958567, "Banished") == 1);
        object fusionDenied = deny(copy, fusion);
        Check("negated Genni Fusion pays both banishes but retains materials", count(fusionDenied, 64756282, "Banished") == 1 && count(fusionDenied, 6958567, "Banished") == 1 && count(fusionDenied, 21744288, "Grave") == 1 && count(fusionDenied, 9603252, "Extra") == 1);
        add(fusionAfter, 64756282, "Grave"); add(fusionAfter, 57916305, "Grave"); add(fusionAfter, 6071005, "Deck");
        Check("all Genni GY copy modes share one use even if a new Genni becomes available", !acts(fusionAfter).Any(a => (int)Get(a, "Source") == 64756282 && Get(a, "Location").ToString() == "Grave"));
        Check("copying does not consume original Celebration activation count", !((HashSet<string>)Get(fusionAfter, "Used")).Contains("6958567:Fusion"));
        object confusion = empty(); add(confusion, 64756282, "Grave"); add(confusion, 35098357, "Grave"); add(confusion, 21744288, "Hand"); add(confusion, 58139997, "Board"); add(confusion, 9603252, "Extra");
        object cf = mode(confusion, "Fusion").First(a => (int)Get(a, "CopiedSpell") == 35098357);
        Check("copied Confusion consumes hand/field materials into GY, not recycle", count(apply(confusion, cf), 21744288, "Grave") == 1 && count(apply(confusion, cf), 58139997, "Grave") == 1);
        object unveil = empty(); add(unveil, 64756282, "Grave"); add(unveil, 70226289, "Grave"); add(unveil, 84523092, "Hand");
        Check("Genni can copy Unveiling from GY", mode(unveil, "Unveiling").Any(a => count(apply(unveil, a), 84523092, "Board") == 1));
        object unveiled = apply(unveil, mode(unveil, "Unveiling").Single());
        Check("copied Unveiling establishes Spellcaster response protection", (bool)Get(unveiled, "SpellcasterProtected"));
        object lacrima = empty(); add(lacrima, 64756282, "Grave"); add(lacrima, 73664385, "Grave"); add(lacrima, 59106048, "Board"); add(lacrima, 95245544, "Deck");
        Check("Genni supports Lacrima send as well as recruit", mode(lacrima, "LacrimaSend").Any(a => (int)Get(a, "CopiedSpell") == 73664385));
        Call(lacrima, "Move", 59106048, Enum.Parse(zone, "Board"), Enum.Parse(zone, "Grave"));
        Check("copied Lacrima send does not inherit direct activation board condition", mode(lacrima, "LacrimaSend").Count > 0);

        var duel = new Duel { Player = 0, Phase = DuelPhase.Main1, Turn = 2 };
        duel.Fields[0].Init(0, 0, 0); duel.Fields[1].Init(0, 0, 0);
        duel.Fields[0].SetInitialDeck(new[] { NamedCard.Get(6071005), NamedCard.Get(57916305) });
        var bot = new Witchcraft2026Executor(new GameAI(new GameClient(new WindBotInfo { Chat = false }), duel), duel);
        var trap = Card(69748261, CardLocation.Grave, 0); duel.Fields[0].Graveyard.Add(trap);
        bot.OnMove(trap, 0, (int)CardLocation.SpellZone, 0, (int)CardLocation.Grave);
        Check("runtime tracks Distortion sent this turn as unavailable", !(bool)Get(Call(bot, "Observe"), "DistortionReady"));
        duel.Turn = 3; bot.OnNewTurn();
        Check("next turn Distortion age survives normal turn reset", (bool)Get(Call(bot, "Observe"), "DistortionReady"));
        bot.OnMove(trap, 0, (int)CardLocation.Grave, 0, (int)CardLocation.Removed);
        bot.OnMove(trap, 0, (int)CardLocation.Removed, 0, (int)CardLocation.Grave);
        Check("re-entering GY resets Distortion age", !(bool)Get(Call(bot, "Observe"), "DistortionReady"));

        var surplus = Card(83289866, CardLocation.Hand, 0); var useful = Card(57916305, CardLocation.Hand, 0);
        duel.Fields[0].Hand.Add(surplus); duel.Fields[0].Hand.Add(useful); duel.Fields[0].SpellZone[0] = Card(83289866, CardLocation.SpellZone, 0);
        Set(bot, "_accepted", draw); Set(bot, "_awaitingChain", true);
        Check("Pittore callback chooses from actual post-draw hand, preserving live Creation", bot.OnSelectCard(new[] { useful, surplus }, 1, 1, HintMsg.ToGrave, false).Single() == surplus);
        Set(bot, "_accepted", redraw);
        Check("Seed subgroup starts with its selected Spell", bot.OnSelectCard(new[] { useful, surplus }, 1, 1, HintMsg.ToDeck, false).Single() == surplus);
        Check("Seed subgroup finishes instead of cycling unselected live starter", bot.OnSelectCard(new[] { useful }, 0, 1, HintMsg.ToDeck, true).Count == 0);
        var duplicateSurplus = Card(83289866, CardLocation.Hand, 0);
        Check("Seed exact subgroup does not select another copy after its planned count is met", bot.OnSelectCard(new[] { duplicateSurplus }, 0, 1, HintMsg.ToDeck, true).Count == 0);
        bot.OnChainEnd();
        Check("chain end retires Seed selection ownership", ((IList)Get(bot, "_seedPicked")).Count == 0);
        bot.SetCard(ExecutorType.Activate, Card(6071005, CardLocation.Grave, 0), 6071005 * 16 + 1);
        Check("Marka trigger with no enemy does not invent tribute", !(bool)Call(bot, "GraveWindowAction"));
        var enemy = Card(37818794); duel.Fields[1].MonsterZone[0] = enemy;
        Check("Marka GY trigger accepts real surviving enemy including destruction-protected body", (bool)Call(bot, "GraveWindowAction"));
        Check("Marka tribute selects actual offered monster", bot.OnSelectCard(new[] { enemy }, 1, 1, HintMsg.Release, false).Single() == enemy);
        var marka = Card(6071005, CardLocation.Grave, 0); var link = Link(marka, 1); marka.Location = CardLocation.MonsterZone;
        duel.Fields[0].MonsterZone[0] = marka; duel.CurrentChainInfo.Add(link); duel.SolvingChainIndex = 1; bot.OnChainSolved(1);
        object redirected = Call(bot, "Observe"); Call(redirected, "Move", 6071005, Enum.Parse(zone, "Board"), Enum.Parse(zone, "Grave"));
        Check("revived Marka is banished on leaving in subsequent prediction", count(redirected, 6071005, "Banished") == 1);
        duel.Phase = DuelPhase.End; duel.CurrentChainInfo.Clear(); duel.SolvingChainIndex = 0;
        bot.SetCard(ExecutorType.Activate, Card(33475154, CardLocation.Grave, 0), 33475154 * 16 + 1);
        Check("Rilliona own End Phase revival has explicit handler", (bool)Call(bot, "GraveWindowAction"));
        Check("Rilliona reveal callback preserves Spell in hand", bot.OnSelectCard(new[] { surplus }, 1, 1, HintMsg.Confirm, false).Single() == surplus && duel.Fields[0].Hand.Contains(surplus));
        duel.Player = 1;
        Check("Rilliona cannot revive in opposing End Phase", !(bool)Call(bot, "GraveWindowAction"));
        bot.SetCard(ExecutorType.Activate, Card(53971455, CardLocation.Grave, 0), 53971455 * 16 + 1);
        Check("Luluwalilith offered GY End Phase trigger supported on either turn", (bool)Call(bot, "GraveWindowAction"));
        duel.Player = 0;
        bot.SetCard(ExecutorType.Activate, Card(56894757, CardLocation.Grave, 0), 56894757 * 16);
        Check("Side-deck Draping GY recovery uses existing End Phase handler", (bool)Call(bot, "EndPhaseRecovery"));

        var seedDuel = new Duel { Player = 0, Phase = DuelPhase.Main1, Turn = 2 };
        seedDuel.Fields[0].Init(0, 0, 0); seedDuel.Fields[1].Init(0, 0, 0);
        // The tracker stores remaining counts, not original counts minus zones.
        seedDuel.Fields[0].SetInitialDeck(new[] { NamedCard.Get(57916305), NamedCard.Get(6071005) });
        var seedBot = new Witchcraft2026Executor(new GameAI(new GameClient(new WindBotInfo { Chat = false }), seedDuel), seedDuel);
        var seedCard = Card(59106048, CardLocation.Grave, 0);
        seedDuel.Fields[0].Graveyard.Add(seedCard); seedDuel.Fields[0].Hand.Add(Card(83289866, CardLocation.Hand, 0));
        seedDuel.Fields[0].SpellZone[0] = Card(83289866, CardLocation.SpellZone, 0);
        seedDuel.MainPhase = new MainPhase(); seedDuel.MainPhase.ActivableCards.Add(seedCard); seedDuel.MainPhase.ActivableDescs.Add(59106048 * 16 + 1);
        seedBot.SetCard(ExecutorType.Activate, seedCard, 59106048 * 16 + 1);
        Check("real Seed idle handler accepts beneficial engine-offered GY redraw", (bool)Call(seedBot, "PlannedAction"));
        Set(seedBot, "_unveilingProtection", true);
        Check("observed state carries established Unveiling protection", (bool)Get(Call(seedBot, "Observe"), "SpellcasterProtected"));
        seedBot.OnNewTurn();
        Check("new turn clears Unveiling protection", !(bool)Get(Call(seedBot, "Observe"), "SpellcasterProtected"));
    }
}
