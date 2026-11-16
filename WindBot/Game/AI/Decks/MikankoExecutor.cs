using System;
using System.Collections.Generic;
using System.Linq;
using WindBot;
using WindBot.Game;
using WindBot.Game.AI;
using YGOSharp.OCGWrapper.Enums;

namespace WindBot.Game.AI.Decks
{
    /// <summary>
    /// Going-second Mikanko executor for the supplied MDPro4 list.
    ///
    /// The installed cards.cdb and script.zip are authoritative.  This executor
    /// compares every currently offered Main Phase action, replans after every
    /// observed move/chain, and keeps prompt choices tied to the accepted action
    /// or immutable chain context.  It never reads hidden opponent identities.
    /// </summary>
    [Deck("Mikanko", "AI_Mikanko")]
    public sealed class MikankoExecutor : DefaultExecutor
    {
        public static class CardId
        {
            public const int Nibiru = 27204311;
            public const int Jizukiru = 63941210;
            public const int ThunderKing = 48770333;
            public const int LavaGolem = 102380;
            public const int Sanaki = 11161666;
            public const int HaRe = 18377261;
            public const int HuLi = 6327734;
            public const int MaxxC = 23434538;
            public const int Ohime = 81260679;
            public const int FeatherDuster = 18144506;
            public const int LightningStorm = 14532163;
            public const int PotOfSloth = 98476659;
            public const int TripleTacticsThrust = 35269904;
            public const int HiddenArmory = 52105192;
            public const int PreparationOfRites = 96729612;
            public const int ReflectionRondo = 79912449;
            public const int TripleTacticsTalent = 25311006;
            public const int DoubleEdgedSword = 41927278;
            public const int GreatCeremony = 44649322;
            public const int NobleTwins = 84550369;
            public const int FireDance = 80044027;
            public const int WaterArabesque = 43527730;
            public const int Mayowashidori = 57736667;
            public const int Rivalry = 78199891;

            public const int Uzuhime = 57566760;
            public const int Zeus = 90448279;
            public const int TyPhon = 93039339;
            public const int FortuneTune = 16259549;
            public const int DownerdMagician = 72167543;
            public const int Hiita = 48815792;
            public const int LittleKnight = 29301450;
            public const int Almiraj = 60303245;
            public const int Masquerena = 65741786;
            public const int KnightmarePhoenix = 2857636;
            public const int Accesscode = 86066372;
            public const int KnightmareUnicorn = 38342335;
            public const int Selene = 45819647;
        }

        private const int MikankoSetcode = 0x18d;
        private const int SystemSet = 1153;
        private const int SystemAddToHand = 1190;

        private static readonly Dictionary<int, int> MainDeckList = new Dictionary<int, int>
        {
            { CardId.Nibiru, 2 }, { CardId.Jizukiru, 2 }, { CardId.ThunderKing, 1 },
            { CardId.LavaGolem, 2 }, { CardId.Sanaki, 1 }, { CardId.HaRe, 2 },
            { CardId.HuLi, 1 }, { CardId.MaxxC, 1 }, { CardId.Ohime, 3 },
            { CardId.FeatherDuster, 1 }, { CardId.LightningStorm, 1 },
            { CardId.PotOfSloth, 2 }, { CardId.TripleTacticsThrust, 2 },
            { CardId.HiddenArmory, 3 }, { CardId.PreparationOfRites, 3 },
            { CardId.ReflectionRondo, 1 }, { CardId.TripleTacticsTalent, 1 },
            { CardId.DoubleEdgedSword, 1 }, { CardId.GreatCeremony, 1 },
            { CardId.NobleTwins, 3 }, { CardId.FireDance, 1 },
            { CardId.WaterArabesque, 3 }, { CardId.Mayowashidori, 1 },
            { CardId.Rivalry, 1 }
        };

        private static readonly Dictionary<int, int> ExtraDeckList = new Dictionary<int, int>
        {
            { CardId.Uzuhime, 3 }, { CardId.Zeus, 1 }, { CardId.TyPhon, 1 },
            { CardId.FortuneTune, 1 }, { CardId.DownerdMagician, 1 },
            { CardId.Hiita, 1 }, { CardId.LittleKnight, 1 }, { CardId.Almiraj, 1 },
            { CardId.Masquerena, 1 }, { CardId.KnightmarePhoenix, 1 },
            { CardId.Accesscode, 1 }, { CardId.KnightmareUnicorn, 1 },
            { CardId.Selene, 1 }
        };

        private sealed class Decision
        {
            public ExecutorType Type;
            public ClientCard Card;
            public int Description;
            public int Score;
            public string Reason;

            public override string ToString()
            {
                return Type + ":" + (Card == null ? 0 : Card.Id) + "/" + Description +
                    " score=" + Score + " " + Reason;
            }
        }

        private readonly OpponentAwareness _opponent = new OpponentAwareness();
        private readonly HashSet<string> _acceptedThisTurn = new HashSet<string>();
        private readonly HashSet<string> _resolvedThisTurn = new HashSet<string>();
        private int _acceptedId;
        private int _acceptedDescription = -1;
        private int _selectedEquipId;
        private string _lastCoverageKey;
        private bool _deckValidated;

        public MikankoExecutor(GameAI ai, Duel duel)
            : base(ai, duel)
        {
            // Fast effects and optional triggers are evaluated before own-turn planning.
            AddExecutor(ExecutorType.Activate, CardId.MaxxC, MaxxCEffect);
            AddExecutor(ExecutorType.Activate, CardId.Nibiru, NibiruEffect);
            AddExecutor(ExecutorType.Activate, TacticalActivation);

            // A lethal reflection line may deliberately enter Battle before using
            // optional extenders.  Otherwise the bounded offered-action comparison runs.
            AddExecutor(ExecutorType.GoToBattlePhase, EnterBattlePhase);
            AddExecutor(ExecutorType.Activate, PlannedMainAction);
            AddExecutor(ExecutorType.SpSummon, PlannedMainAction);
            AddExecutor(ExecutorType.Summon, PlannedMainAction);
            AddExecutor(ExecutorType.SpellSet, PlannedMainAction);
            AddExecutor(ExecutorType.Repos, MonsterRepos);
        }

        public override bool OnSelectHand()
        {
            // Mikanko is a board-breaking OTK deck.  False selects the second turn.
            return false;
        }

        private int EffectIndex(int id, int description)
        {
            for (int i = 0; i < 16; i++)
            {
                if (description == Util.GetStringId(id, i))
                    return i;
            }
            return -1;
        }

        private bool IsOwnMainPhase()
        {
            return Duel.Player == 0 &&
                (Duel.Phase == DuelPhase.Main1 || Duel.Phase == DuelPhase.Main2) &&
                Duel.MainPhase != null;
        }

        private bool MaxxCEffect()
        {
            return Duel.Player == 1 && !DefaultCheckWhetherCardIsNegated(Card) &&
                !_resolvedThisTurn.Contains(CardId.MaxxC + ":0");
        }

        private bool NibiruEffect()
        {
            if (Duel.Player != 1 || DefaultCheckWhetherCardIsNegated(Card))
                return false;
            List<ClientCard> monsters = Enemy.GetMonsters().Where(c => c.IsFaceup()).ToList();
            bool urgent = monsters.Sum(c => Math.Max(0, c.Attack)) >= Bot.LifePoints ||
                monsters.Any(c => c.IsFloodgate() || c.IsMonsterDangerous() || c.IsMonsterInvincible());
            bool accept = monsters.Count >= 2 || urgent;
            if (accept)
            {
                Logger.DebugWriteLine("[Mikanko] Nibiru now: bodies=" + monsters.Count +
                    " attack=" + monsters.Sum(c => Math.Max(0, c.Attack)) +
                    " urgent=" + urgent + "; engine already verified the five-summon condition");
            }
            return accept;
        }

        private bool TacticalActivation()
        {
            if (Card == null || Card.Controller != 0)
                return false;

            int effect = EffectIndex(Card.Id, ActivateDescription);
            bool accept = false;
            string reason = null;

            if (Card.Id == CardId.HaRe)
            {
                accept = true;
                reason = "equip trigger searches an Equip Spell";
            }
            else if (Card.Id == CardId.HuLi)
            {
                accept = true;
                reason = "equip trigger searches Rivalry";
            }
            else if (Card.Id == CardId.Sanaki && effect == 1)
            {
                accept = true;
                reason = "equip trigger recruits a non-Illusion Mikanko";
            }
            else if (Card.Id == CardId.Sanaki && effect == 2)
            {
                accept = Bot.GetMonsters().Any(c => c.IsFaceup());
                reason = "GY trigger equips Sanaki to retrigger Ha-Re or Hu-Li";
            }
            else if (Card.Id == CardId.Uzuhime && (effect == 0 || effect == 2))
            {
                accept = true;
                reason = effect == 0 ? "Xyz summon trigger searches an Equip Spell" :
                    "detach for another reflected attack";
            }
            else if (Card.Id == CardId.Mayowashidori && Card.Location == CardLocation.SpellZone)
            {
                accept = Enemy.GetMonsters().Concat(Enemy.GetSpells()).Any();
                reason = "post-battle bounce removes the highest public threat";
            }
            else if (Card.Id == CardId.Rivalry && Card.Location == CardLocation.Grave)
            {
                accept = Bot.Graveyard.Any(c => c.IsSpell() && c.HasType(CardType.Equip));
                reason = "recover a spent Equip Spell";
            }
            else if (Card.Id == CardId.Ohime && Card.Location == CardLocation.MonsterZone && effect == 1)
            {
                _selectedEquipId = PreferredEquipId(Bot.Graveyard.Where(c => c.IsSpell() && c.HasType(CardType.Equip)));
                accept = _selectedEquipId != 0 &&
                    (Duel.Player == 1 || EstimatedReflectionDamage() >= Enemy.LifePoints ||
                    Enemy.GetMonsters().Any(c => c.IsFloodgate() || c.IsMonsterDangerous()));
                reason = "quick-equip from GY: " + _selectedEquipId;
            }
            else if (Card.Id == CardId.Rivalry && Duel.Player == 1 && Card.Location == CardLocation.SpellZone)
            {
                _selectedEquipId = PreferredEquipIdFromDeck();
                accept = HasReflector() && Enemy.GetMonsters().Any();
                reason = "opponent-turn equip preserves a reflector and disrupts a public threat";
            }
            else if (Card.Id == CardId.FireDance && Duel.Player == 1)
            {
                accept = OwnMikankoInHandOrGrave();
                reason = "opponent-turn revive protects follow-up";
            }
            else if (Card.Id == CardId.Masquerena && Duel.Player == 1)
            {
                accept = Enemy.GetMonsters().Any(c => c.IsFloodgate() || c.IsMonsterDangerous()) ||
                    Enemy.GetMonsterCount() >= 2;
                reason = "I:P converts public bodies into a reactive Link";
            }
            else if (Card.Id == CardId.LittleKnight && effect == 1 && Duel.LastChainPlayer == 1)
            {
                accept = Enemy.GetMonsters().Any(c => c.IsFaceup());
                reason = "temporary banish interrupts a live opponent chain";
            }
            else if (Card.Id == CardId.LittleKnight && effect == 0)
            {
                accept = Enemy.GetMonsters().Concat(Enemy.GetSpells()).Any();
                reason = "Link summon trigger removes the highest public threat";
            }
            else if (Card.Id == CardId.Zeus && effect == 1)
            {
                int enemyCards = Enemy.GetMonsterCount() + Enemy.GetSpellCount();
                int ownOtherCards = Bot.GetMonsterCount() + Bot.GetSpellCount() - 1;
                accept = enemyCards >= 2 && enemyCards > ownOtherCards;
                reason = "Zeus wipe gains observable card advantage";
            }
            else if (Card.Id == CardId.Zeus && effect == 2)
            {
                accept = true;
                reason = "optional material recovery";
            }
            else if (Card.Id == CardId.KnightmarePhoenix || Card.Id == CardId.KnightmareUnicorn)
            {
                accept = Bot.Hand.Count > 0 && Enemy.GetMonsters().Concat(Enemy.GetSpells()).Any();
                reason = "Link summon removal trigger";
            }
            else if (Card.Id == CardId.Accesscode && effect == 0)
            {
                accept = true;
                reason = "Link summon ATK gain";
            }
            else if (Card.Id == CardId.Hiita)
            {
                accept = true;
                reason = "verified Hiita summon/search trigger";
            }
            else if (Card.Id == CardId.FortuneTune || Card.Id == CardId.DownerdMagician)
            {
                accept = true;
                reason = "optional Xyz value/replacement effect";
            }
            else if (Card.Id == CardId.Almiraj && Duel.LastChainPlayer == 1)
            {
                accept = Duel.CurrentChainInfo.LastOrDefault() != null &&
                    Duel.CurrentChainInfo.Last().Targets.Any(c => c.Controller == 0 && c.IsMonster());
                reason = "protect a monster actually targeted by the current chain";
            }
            else if (Card.Id == CardId.Selene && effect == 1 && Duel.Player == 1)
            {
                accept = true;
                reason = "opponent-turn Spellcaster revive";
            }

            if (!accept)
                return false;

            Accept(Card.Id, ActivateDescription, reason);
            return true;
        }

        private bool EnterBattlePhase()
        {
            if (!IsOwnMainPhase() || Duel.Phase != DuelPhase.Main1 || !Duel.MainPhase.CanBattlePhase)
                return false;
            int damage = EstimatedReflectionDamage();
            if (damage >= Enemy.LifePoints && Enemy.LifePoints > 0)
            {
                Logger.DebugWriteLine("[Mikanko] enter Battle: conservative reflected damage=" +
                    damage + " enemyLP=" + Enemy.LifePoints +
                    "; recompute from actual attack offers after every battle");
                return true;
            }
            Decision best = BestMainDecision(false);
            return best == null && HasReflector() && Enemy.GetMonsters().Any();
        }

        private bool PlannedMainAction()
        {
            if (!IsOwnMainPhase())
                return false;
            ValidateDeckListOnce();
            Decision best = BestMainDecision(true);
            if (best == null || best.Type != Type || !ReferenceEquals(best.Card, Card) ||
                (Type == ExecutorType.Activate && best.Description != ActivateDescription))
                return false;

            PreparePromptIntent(best);
            Accept(best.Card.Id, best.Description, best.Reason);
            Logger.DebugWriteLine("[Mikanko] choose " + best);
            return true;
        }

        private Decision BestMainDecision(bool logCoverage)
        {
            List<Decision> decisions = CurrentMainDecisions();
            if (logCoverage)
                LogCoverage(decisions);
            return decisions.Where(d => d.Score > 0)
                .OrderByDescending(d => d.Score)
                .ThenBy(d => TypeTieBreak(d.Type))
                .FirstOrDefault();
        }

        private List<Decision> CurrentMainDecisions()
        {
            List<Decision> result = new List<Decision>();
            MainPhase main = Duel.MainPhase;
            if (main == null)
                return result;

            for (int i = 0; i < main.ActivableCards.Count; i++)
            {
                string reason;
                int score = ScoreActivation(main.ActivableCards[i], main.ActivableDescs[i], out reason);
                result.Add(new Decision { Type = ExecutorType.Activate, Card = main.ActivableCards[i],
                    Description = main.ActivableDescs[i], Score = score, Reason = reason });
            }
            foreach (ClientCard card in main.SpecialSummonableCards)
            {
                string reason;
                result.Add(new Decision { Type = ExecutorType.SpSummon, Card = card, Description = -1,
                    Score = ScoreSpecialSummon(card, out reason), Reason = reason });
            }
            foreach (ClientCard card in main.SummonableCards)
            {
                string reason;
                result.Add(new Decision { Type = ExecutorType.Summon, Card = card, Description = -1,
                    Score = ScoreNormalSummon(card, out reason), Reason = reason });
            }
            foreach (ClientCard card in main.SpellSetableCards)
            {
                string reason;
                result.Add(new Decision { Type = ExecutorType.SpellSet, Card = card, Description = -1,
                    Score = ScoreSet(card, out reason), Reason = reason });
            }
            return result;
        }

        private static int TypeTieBreak(ExecutorType type)
        {
            if (type == ExecutorType.Activate) return 0;
            if (type == ExecutorType.SpSummon) return 1;
            if (type == ExecutorType.Summon) return 2;
            return 3;
        }

        private int ScoreActivation(ClientCard card, int description, out string reason)
        {
            reason = "intentionally rejected: unsupported or no positive public consequence";
            if (card == null || card.Controller != 0 || DefaultCheckWhetherCardIsNegated(card))
                return 0;

            int effect = EffectIndex(card.Id, description);
            int enemyMonsters = Enemy.GetMonsterCount();
            int enemySpells = Enemy.GetSpellCount();
            int enemyCards = enemyMonsters + enemySpells;
            bool reflector = HasReflector();

            switch (card.Id)
            {
                case CardId.FeatherDuster:
                    reason = "remove observable opponent backrow before committing a face-up card";
                    return enemySpells == 0 ? 0 : 11800 + enemySpells * 300;
                case CardId.LightningStorm:
                    reason = "choose the larger observable monster/backrow clear";
                    return enemyCards == 0 ? 0 : 11600 + Math.Max(enemySpells,
                        Enemy.GetMonsters().Count(c => c.IsAttack())) * 250;
                case CardId.PotOfSloth:
                    reason = "net one card while bottoming the lowest-value drawn resources";
                    return enemyCards == 0 ? 0 : 10500 + Math.Min(enemyCards, 6) * 80;
                case CardId.TripleTacticsThrust:
                    reason = "opponent monster effect unlocked a board-breaker or engine spell";
                    return 11100;
                case CardId.TripleTacticsTalent:
                    reason = "opponent monster effect unlocked draw/control for the OTK";
                    return 10900;
                case CardId.PreparationOfRites:
                    reason = "search Ohime without consuming the Normal Summon";
                    return Bot.HasInHand(CardId.Ohime) ? 5200 : 9800;
                case CardId.HiddenArmory:
                    reason = reflector && enemyMonsters > 0 ?
                        "search Double-Edged Sword for reflected lethal" :
                        "search Water Arabesque before any Normal Summon";
                    return reflector && enemyMonsters > 0 ? 10300 : 9000;
                case CardId.Ohime:
                    if (card.Location == CardLocation.Hand && effect == 0)
                    {
                        reason = "search the missing Mikanko route, then discard a GY-value card";
                        return 10100;
                    }
                    if (card.Location == CardLocation.MonsterZone && effect == 1)
                    {
                        int equip = PreferredEquipId(Bot.Graveyard.Where(c => c.IsSpell() && c.HasType(CardType.Equip)));
                        reason = "equip from GY to convert a real public target into pressure";
                        return equip == 0 ? 0 : (equip == CardId.DoubleEdgedSword ? 11200 : 8200);
                    }
                    return 0;
                case CardId.NobleTwins:
                    if (card.Location == CardLocation.Grave && effect == 1)
                    {
                        reason = "banish and detach an offered material to summon Ohime";
                        return 9600;
                    }
                    reason = "send a Mikanko card and set follow-up; accept the verified Extra Deck lock";
                    return 8800;
                case CardId.GreatCeremony:
                    reason = card.Location == CardLocation.Grave ?
                        "send an Equip Spell for Ohime/Mayowashidori recovery" :
                        "Special Summon Ohime or a missing Mikanko body";
                    return card.Location == CardLocation.Grave ? 7900 : 9700;
                case CardId.WaterArabesque:
                    if (card.Location == CardLocation.SpellZone && card.IsFaceup())
                    {
                        reason = "summon a different Mikanko and bounce the equipped public threat";
                        return 10200 + (card.EquipTarget != null && card.EquipTarget.Controller == 1 ? 500 : 0);
                    }
                    reason = enemyMonsters > 0 ?
                        "equip the highest strategic opponent monster before Arabesque summons and bounces it" :
                        "equip an own body to start the Mikanko route";
                    return 9200;
                case CardId.FireDance:
                    reason = "revive/summon a Mikanko and optionally create a disabled battle target";
                    return OwnMikankoInHandOrGrave() ? 8200 : 0;
                case CardId.Mayowashidori:
                    reason = card.Location == CardLocation.Grave ?
                        "revive a Mikanko and re-equip from GY" : "protect a reflector and enable post-battle bounce";
                    return card.Location == CardLocation.Grave ? 8000 : (Bot.GetMonsters().Any() ? 6500 : 0);
                case CardId.ReflectionRondo:
                    reason = "negate and take a public opponent monster while a Mikanko remains controlled";
                    return reflector && enemyMonsters > 0 ? 9500 : 0;
                case CardId.DoubleEdgedSword:
                    reason = "double reflected battle damage on the strongest opponent monster";
                    return reflector && enemyMonsters > 0 ? 10800 : 0;
                case CardId.Rivalry:
                    reason = card.Location == CardLocation.Grave ?
                        "recover an Equip Spell sent to GY" : "equip the best legal card directly from the Deck";
                    return card.Location == CardLocation.Grave ? 5800 :
                        (reflector && enemyMonsters > 0 ? 9000 : 6500);
                case CardId.Sanaki:
                    if (effect == 0)
                    {
                        reason = "return a reusable Mikanko card to hand without ending the route";
                        return Bot.GetSpells().Any(c => c.HasSetcode(MikankoSetcode) && c.IsFaceup()) ? 5700 : 0;
                    }
                    return 0;
                case CardId.HaRe:
                case CardId.HuLi:
                    reason = "mandatory-value equip trigger";
                    return 7600;
                case CardId.Uzuhime:
                    reason = effect == 0 ? "search an Equip Spell on Xyz Summon" :
                        "detach to continue attacking";
                    return 9200;
                case CardId.Zeus:
                    if (effect == 1)
                    {
                        reason = "send the larger observable opponent board to GY";
                        return enemyCards >= 2 && enemyCards > Bot.GetMonsterCount() + Bot.GetSpellCount() - 1 ? 10600 : 0;
                    }
                    reason = "attach a replacement material";
                    return 4200;
                case CardId.TyPhon:
                    reason = "detach to bounce the highest-value visible monster";
                    return enemyMonsters > 0 ? 9300 : 0;
                case CardId.KnightmarePhoenix:
                    reason = "discard the lowest-value card to destroy backrow";
                    return enemySpells > 0 && Bot.Hand.Count > 0 ? 8100 : 0;
                case CardId.KnightmareUnicorn:
                    reason = "discard the lowest-value card to shuffle a public threat";
                    return enemyCards > 0 && Bot.Hand.Count > 0 ? 8500 : 0;
                case CardId.Accesscode:
                    reason = effect == 0 ? "take verified Link-material ATK gain" : "banish a Link to destroy a public threat";
                    return effect == 0 ? 7800 : (enemyCards > 0 ? 9000 : 0);
                case CardId.LittleKnight:
                    reason = "banish a strategic public threat";
                    return enemyCards > 0 ? 8700 : 0;
                case CardId.Hiita:
                    reason = "revive an observed FIRE monster or take the destruction search";
                    return 6000;
                case CardId.Masquerena:
                    reason = "save I:P for the opponent Main Phase";
                    return Duel.Player == 1 ? 7800 : 0;
                case CardId.Selene:
                    reason = "revive a Spellcaster into a linked zone";
                    return Bot.Graveyard.Any(c => c.HasRace(CardRace.SpellCaster)) ? 5900 : 0;
                case CardId.FortuneTune:
                    reason = "take verified Xyz recovery/LP value";
                    return 2600;
                case CardId.DownerdMagician:
                    reason = "verified post-battle material effect";
                    return 1800;
                case CardId.Almiraj:
                    reason = "hold destruction protection for an observed target";
                    return 0;
            }
            return 0;
        }

        private int ScoreSpecialSummon(ClientCard card, out string reason)
        {
            reason = "intentionally rejected: summon does not improve the current public route";
            if (card == null)
                return 0;
            switch (card.Id)
            {
                case CardId.LavaGolem:
                    reason = "tribute two opponent monsters without targeting";
                    int nonKaijuTargets = Enemy.GetMonsters().Count(c => !IsKaijuId(c.Id));
                    return nonKaijuTargets >= 2 ? 12500 + BestThreatScore() : 0;
                case CardId.Jizukiru:
                case CardId.ThunderKing:
                    reason = "replace the highest strategic opponent monster with a large OTK target";
                    return Enemy.GetMonsterCount() > 0 && !EnemyHasKaiju() ?
                        12100 + BestThreatScore() : 0;
                case CardId.Uzuhime:
                    reason = "two Level 3 Mikanko become a battle-proof multi-attack reflector";
                    return Enemy.GetMonsterCount() > 0 ? 10100 : 7600;
                case CardId.TyPhon:
                    reason = "public Extra Deck history unlocked TY-PHON board breaking";
                    return Enemy.GetMonsterCount() > 0 ? 9900 : 0;
                case CardId.Zeus:
                    reason = "overlay after a verified Xyz battle for a live board wipe";
                    return Enemy.GetMonsterCount() + Enemy.GetSpellCount() >= 2 ? 9700 : 6800;
                case CardId.DownerdMagician:
                    reason = "add material before Zeus in Main Phase 2";
                    return Duel.Phase == DuelPhase.Main2 && Bot.GetMonsters().Any(c => c.HasType(CardType.Xyz)) ? 7300 : 0;
                case CardId.FortuneTune:
                    reason = "fallback Rank 3 that enables battle then Downerd/Zeus";
                    return Enemy.GetMonsterCount() > 0 ? 6100 : 3400;
                case CardId.LittleKnight:
                    reason = "convert expendable bodies into non-destruction removal";
                    return Enemy.GetMonsterCount() + Enemy.GetSpellCount() > 0 ? 7600 : 0;
                case CardId.KnightmarePhoenix:
                    reason = "convert expendable bodies and a discard into backrow removal";
                    return Enemy.GetSpellCount() > 0 && Bot.Hand.Count > 0 ? 7200 : 0;
                case CardId.KnightmareUnicorn:
                    reason = "convert expendable bodies and a discard into shuffle removal";
                    return Enemy.GetMonsterCount() + Enemy.GetSpellCount() > 0 && Bot.Hand.Count > 0 ? 7400 : 0;
                case CardId.Accesscode:
                    reason = "Link climb to close after Mikanko board breaking is unavailable";
                    return Enemy.GetMonsterCount() + Enemy.GetSpellCount() > 0 ? 7000 : 0;
                case CardId.Hiita:
                    reason = "use an observed FIRE monster in the opponent GY as extension";
                    return Enemy.Graveyard.Any(c => c.IsFaceup() && c.HasAttribute(CardAttribute.Fire)) ? 6200 : 0;
                case CardId.Almiraj:
                    reason = "put a Normal Summoned 0-ATK monster in GY for Fire Dance/Mayowashidori";
                    return Bot.GetMonsters().Count == 1 && !HasReflector() ? 3900 : 0;
                case CardId.Masquerena:
                    reason = "going-first fallback interaction";
                    return Enemy.GetMonsterCount() == 0 ? 4200 : 0;
                case CardId.Selene:
                    reason = "Spellcaster Link bridge toward Accesscode";
                    return 4600;
            }
            return 0;
        }

        private int ScoreNormalSummon(ClientCard card, out string reason)
        {
            reason = "intentionally rejected: preserve the Normal Summon";
            if (card == null || !card.HasSetcode(MikankoSetcode))
                return 0;
            if (card.Id == CardId.Sanaki)
            {
                reason = "Sanaki is the best Arabesque/equip trigger body";
                return 6500;
            }
            if (card.Id == CardId.HaRe)
            {
                reason = "Ha-Re becomes a reflector and searches an Equip Spell when equipped";
                return 6200;
            }
            if (card.Id == CardId.HuLi)
            {
                reason = "Hu-Li supplies targeting protection and Rivalry access";
                return 6000;
            }
            return 0;
        }

        private int ScoreSet(ClientCard card, out string reason)
        {
            reason = "intentionally rejected: do not set an immediately useful board breaker or engine spell";
            if (card == null || Duel.Phase != DuelPhase.Main2)
                return 0;
            if (card.Id == CardId.Rivalry)
            {
                reason = "preserve opponent-turn equip interaction";
                return 4800;
            }
            if (card.Id == CardId.GreatCeremony || card.Id == CardId.FireDance)
            {
                reason = "preserve Quick-Play follow-up after the OTK did not close";
                return 3300;
            }
            return 0;
        }

        private void PreparePromptIntent(Decision decision)
        {
            _selectedEquipId = 0;
            if (decision.Card.Id == CardId.DoubleEdgedSword || decision.Card.Id == CardId.ReflectionRondo ||
                decision.Card.Id == CardId.WaterArabesque || decision.Card.Id == CardId.Mayowashidori)
                _selectedEquipId = decision.Card.Id;
            if (decision.Card.Id == CardId.Rivalry)
                _selectedEquipId = PreferredEquipIdFromDeck();
            if (decision.Card.Id == CardId.Ohime && decision.Card.Location == CardLocation.MonsterZone)
                _selectedEquipId = PreferredEquipId(Bot.Graveyard.Where(c => c.IsSpell() && c.HasType(CardType.Equip)));
        }

        private void Accept(int id, int description, string reason)
        {
            _acceptedId = id;
            _acceptedDescription = description;
            _acceptedThisTurn.Add(id + ":" + EffectIndex(id, description));
            Logger.DebugWriteLine("[Mikanko] accept " + id + "/" + description + " reason=" + reason);
        }

        private void LogCoverage(IList<Decision> decisions)
        {
            string key = Duel.Turn + ":" + Duel.Phase + ":" + Bot.Hand.Count + ":" +
                Bot.GetMonsterCount() + ":" + Bot.GetSpellCount() + ":" + Enemy.GetMonsterCount() +
                ":" + Enemy.GetSpellCount() + "|" + string.Join(",", decisions.Select(d =>
                    d.Type + "/" + d.Card.Id + "/" + d.Description + "/" + d.Score));
            if (key == _lastCoverageKey)
                return;
            _lastCoverageKey = key;
            Decision best = decisions.Where(d => d.Score > 0).OrderByDescending(d => d.Score).FirstOrDefault();
            Logger.DebugWriteLine("[Mikanko] Action Coverage Audit offered=" + decisions.Count +
                " supported=" + decisions.Count(d => d.Score > 0) +
                " rejected=" + decisions.Count(d => d.Score <= 0) +
                " chosen=" + (best == null ? "pass" : best.ToString()) +
                " alternatives=[" + string.Join(" | ", decisions.OrderByDescending(d => d.Score).Take(8)) + "]");
        }

        private int PromptSource()
        {
            ChainInfo solving = Duel.GetCurrentSolvingChainInfo();
            if (solving != null)
                return solving.ActivatePlayer == 0 ? solving.ActivateId : 0;
            if (_acceptedId != 0)
                return _acceptedId;
            ClientCard current = Duel.GetCurrentChainCard();
            return current != null && current.Controller == 0 ? current.Id : 0;
        }

        private IList<ClientCard> Pick(IList<ClientCard> offered, IEnumerable<ClientCard> ordered,
            int min, int max, bool cancelable, bool many)
        {
            List<ClientCard> result = ordered.Where(c => c != null && offered.Contains(c))
                .Distinct().Take(many ? max : Math.Max(1, min)).ToList();
            if (result.Count < min)
            {
                if (cancelable)
                    return new List<ClientCard>();
                result.AddRange(offered.Where(c => !result.Contains(c)).Take(min - result.Count));
            }
            return result;
        }

        public override IList<ClientCard> OnSelectCard(IList<ClientCard> cards, int min, int max,
            int hint, bool cancelable)
        {
            if (cards == null || cards.Count == 0)
                return base.OnSelectCard(cards, min, max, hint, cancelable);

            int source = PromptSource();
            if (hint == HintMsg.Release &&
                (source == CardId.LavaGolem || source == CardId.Jizukiru || source == CardId.ThunderKing))
            {
                IEnumerable<ClientCard> releases = source == CardId.LavaGolem ?
                    cards.Where(c => !IsKaijuId(c.Id)).OrderByDescending(ThreatScore)
                        .Concat(cards.Where(c => IsKaijuId(c.Id)).OrderByDescending(ThreatScore)) :
                    cards.OrderByDescending(ThreatScore);
                return Pick(cards, releases, min, max, cancelable, true);
            }

            if (hint == HintMsg.Discard)
                return Pick(cards, cards.OrderBy(DiscardCost), min, max, cancelable, true);

            if (hint == HintMsg.ToDeck && source == CardId.PotOfSloth)
                return Pick(cards, cards.OrderBy(DiscardCost), min, max, cancelable, true);

            if (hint == HintMsg.XyzMaterial || hint == HintMsg.LinkMaterial ||
                hint == HintMsg.FusionMaterial || hint == HintMsg.SynchroMaterial)
                return Pick(cards, cards.OrderBy(MaterialCost), min, max, cancelable, true);

            if (hint == HintMsg.RemoveXyz)
                return Pick(cards, cards.OrderBy(MaterialCost), min, max, cancelable, true);

            if (source == CardId.Ohime)
            {
                if (hint == HintMsg.AddToHand)
                    return Pick(cards, OhimeSearchOrder(cards), min, max, cancelable, false);
                if (cards.All(c => c.Location == CardLocation.Grave && c.HasType(CardType.Equip)))
                {
                    IList<ClientCard> selected = Pick(cards, EquipSearchOrder(cards), min, max, cancelable, false);
                    if (selected.Count > 0) _selectedEquipId = selected[0].Id;
                    return selected;
                }
                if (cards.Any(c => c.IsMonster() && c.IsFaceup()))
                    return Pick(cards, EquipTargetOrder(cards, _selectedEquipId), min, max, cancelable, false);
            }

            if (source == CardId.PreparationOfRites)
                return Pick(cards, cards.OrderByDescending(c => c.Id == CardId.Ohime), min, max, cancelable, false);

            if (source == CardId.HiddenArmory || source == CardId.Uzuhime || source == CardId.HaRe)
                return Pick(cards, EquipSearchOrder(cards), min, max, cancelable, false);

            if (source == CardId.HuLi)
                return Pick(cards, cards.OrderByDescending(c => c.Id == CardId.Rivalry), min, max, cancelable, false);

            if (source == CardId.NobleTwins)
            {
                if (hint == HintMsg.ToGrave)
                    return Pick(cards, NobleTwinsMillOrder(cards), min, max, cancelable, false);
                if (hint == HintMsg.Set)
                    return Pick(cards, MikankoSetOrder(cards), min, max, cancelable, false);
                if (hint == HintMsg.SpSummon)
                    return Pick(cards, cards.OrderByDescending(c => c.Id == CardId.Ohime), min, max, cancelable, false);
            }

            if (source == CardId.GreatCeremony)
            {
                if (hint == HintMsg.SpSummon)
                    return Pick(cards, MikankoSummonOrder(cards), min, max, cancelable, false);
                if (hint == HintMsg.ToGrave)
                    return Pick(cards, CeremonyMillOrder(cards), min, max, cancelable, false);
            }

            if (source == CardId.WaterArabesque && hint == HintMsg.SpSummon)
                return Pick(cards, ArabesqueSummonOrder(cards), min, max, cancelable, false);

            if (source == CardId.FireDance && hint == HintMsg.SpSummon)
            {
                bool opponentCards = cards.Any(c => c.Controller == 1);
                return opponentCards ?
                    Pick(cards, cards.Where(c => c.Controller == 1).OrderByDescending(c => c.Attack),
                        min, max, cancelable, false) :
                    Pick(cards, MikankoSummonOrder(cards), min, max, cancelable, false);
            }

            if (source == CardId.Sanaki)
            {
                if (hint == HintMsg.ReturnToHand)
                    return Pick(cards, cards.OrderBy(SanakiReturnCost), min, max, cancelable, false);
                if (hint == HintMsg.SpSummon)
                    return Pick(cards, ArabesqueSummonOrder(cards), min, max, cancelable, false);
                if (hint == HintMsg.Equip || hint == HintMsg.Target || hint == HintMsg.Faceup)
                    return Pick(cards, EquipTargetOrder(cards, CardId.Sanaki), min, max, cancelable, false);
            }

            if (source == CardId.Mayowashidori)
            {
                if (hint == HintMsg.SpSummon)
                    return Pick(cards, MikankoSummonOrder(cards), min, max, cancelable, false);
                if (hint == HintMsg.ReturnToHand)
                    return Pick(cards, cards.Where(c => c.Controller == 1).OrderByDescending(ThreatScore)
                        .Concat(cards.Where(c => c.Controller == 0).OrderBy(DiscardCost)),
                        min, max, cancelable, false);
                return Pick(cards, EquipTargetOrder(cards, CardId.Mayowashidori), min, max, cancelable, false);
            }

            if (source == CardId.Rivalry)
            {
                if (cards.All(c => c.HasType(CardType.Equip)))
                {
                    IList<ClientCard> selected = Pick(cards, EquipSearchOrder(cards), min, max, cancelable, false);
                    if (selected.Count > 0) _selectedEquipId = selected[0].Id;
                    return selected;
                }
                if (hint == HintMsg.AddToHand)
                    return Pick(cards, EquipSearchOrder(cards), min, max, cancelable, false);
                return Pick(cards, EquipTargetOrder(cards, _selectedEquipId), min, max, cancelable, false);
            }

            if (source == CardId.DoubleEdgedSword || source == CardId.ReflectionRondo ||
                source == CardId.WaterArabesque)
                return Pick(cards, EquipTargetOrder(cards, source), min, max, cancelable, false);

            if (source == CardId.TripleTacticsTalent && hint == HintMsg.Control)
                return Pick(cards, cards.Where(c => c.Controller == 1).OrderByDescending(ThreatScore),
                    min, max, cancelable, false);

            if (source == CardId.TripleTacticsThrust && hint == HintMsg.OperateCard)
                return Pick(cards, ThrustSearchOrder(cards), min, max, cancelable, false);

            if (source == CardId.LightningStorm)
                return base.OnSelectCard(cards, min, max, hint, cancelable);

            if (source == CardId.KnightmarePhoenix)
                return Pick(cards, cards.Where(c => c.Controller == 1 && c.IsSpell())
                    .OrderByDescending(ThreatScore), min, max, cancelable, false);

            if (source == CardId.KnightmareUnicorn || source == CardId.TyPhon ||
                source == CardId.LittleKnight || source == CardId.Accesscode)
                return Pick(cards, cards.Where(c => c.Controller == 1).OrderByDescending(ThreatScore)
                    .Concat(cards.Where(c => c.Controller == 0).OrderBy(MaterialCost)),
                    min, max, cancelable, false);

            if (source == CardId.Zeus && hint == HintMsg.XyzMaterial)
                return Pick(cards, cards.OrderBy(MaterialCost), min, max, cancelable, false);

            if (source == CardId.Hiita && hint == HintMsg.SpSummon)
                return Pick(cards, cards.OrderByDescending(c => c.Attack), min, max, cancelable, false);

            if (source == CardId.Selene && hint == HintMsg.SpSummon)
                return Pick(cards, cards.OrderByDescending(c => c.Id == CardId.Ohime)
                    .ThenByDescending(c => c.Attack), min, max, cancelable, false);

            return base.OnSelectCard(cards, min, max, hint, cancelable);
        }

        public override IList<ClientCard> OnSelectXyzMaterial(IList<ClientCard> cards, int min, int max)
        {
            return Pick(cards, cards.OrderBy(MaterialCost), min, max, false, true);
        }

        public override IList<ClientCard> OnSelectLinkMaterial(IList<ClientCard> cards, int min, int max)
        {
            return Pick(cards, cards.OrderBy(MaterialCost), min, max, false, true);
        }

        public override int OnSelectOption(IList<int> options)
        {
            int source = PromptSource();
            if (source == CardId.LightningStorm)
            {
                int monster = options.IndexOf(Util.GetStringId(CardId.LightningStorm, 0));
                int spells = options.IndexOf(Util.GetStringId(CardId.LightningStorm, 1));
                int attackMonsters = Enemy.GetMonsters().Count(c => c.IsAttack());
                if (spells >= 0 && Enemy.GetSpellCount() >= attackMonsters) return spells;
                if (monster >= 0) return monster;
                if (spells >= 0) return spells;
            }
            if (source == CardId.TripleTacticsTalent)
            {
                int draw = options.IndexOf(Util.GetStringId(CardId.TripleTacticsTalent, 0));
                int control = options.IndexOf(Util.GetStringId(CardId.TripleTacticsTalent, 1));
                if (control >= 0 && Enemy.GetMonsters().Any() &&
                    (HasReflector() || Enemy.GetMonsters().Max(c => c.Attack) >= 2500)) return control;
                if (draw >= 0) return draw;
                int shuffle = options.IndexOf(Util.GetStringId(CardId.TripleTacticsTalent, 2));
                if (shuffle >= 0) return shuffle;
            }
            if (source == CardId.TripleTacticsThrust)
            {
                int add = options.IndexOf(SystemAddToHand);
                if (add >= 0) return add;
                int set = options.IndexOf(SystemSet);
                if (set >= 0) return set;
            }
            return base.OnSelectOption(options);
        }

        public override bool OnSelectYesNo(int desc)
        {
            int source = PromptSource();
            if (desc == Util.GetStringId(CardId.NobleTwins, 2))
                return true;
            if (desc == Util.GetStringId(CardId.FireDance, 1))
            {
                int fieldPower = Enemy.GetMonsters().Select(c => Math.Max(c.Attack, c.Defense))
                    .DefaultIfEmpty(0).Max();
                int gravePower = Enemy.Graveyard.Where(c => c.IsMonster())
                    .Select(c => Math.Max(c.Attack, c.Defense)).DefaultIfEmpty(0).Max();
                return HasReflector() && (Enemy.GetMonsterCount() == 0 || gravePower > fieldPower);
            }
            if (source == CardId.PreparationOfRites)
                return false;
            if (source == CardId.KnightmarePhoenix || source == CardId.KnightmareUnicorn)
                return true;
            if (source == CardId.Zeus || source == CardId.DownerdMagician || source == CardId.TyPhon)
                return true;
            return base.OnSelectYesNo(desc);
        }

        public override int OnSelectPlace(int cardId, int player, CardLocation location, int available)
        {
            if (player != 0)
                return base.OnSelectPlace(cardId, player, location, available);
            if (location == CardLocation.MonsterZone)
            {
                int[] zones = { 2, 1, 3, 0, 4, 5, 6 };
                foreach (int zone in zones)
                    if ((available & (1 << zone)) != 0) return 1 << zone;
            }
            if (location == CardLocation.SpellZone)
            {
                int[] zones = { 0, 4, 1, 3, 2, 5 };
                foreach (int zone in zones)
                    if ((available & (1 << zone)) != 0) return 1 << zone;
            }
            return base.OnSelectPlace(cardId, player, location, available);
        }

        public override CardPosition OnSelectPosition(int cardId, IList<CardPosition> positions)
        {
            if (IsReflectorId(cardId) && positions.Contains(CardPosition.FaceUpAttack))
                return CardPosition.FaceUpAttack;
            if (cardId == CardId.Sanaki && positions.Contains(CardPosition.FaceUpDefence))
                return CardPosition.FaceUpDefence;
            return base.OnSelectPosition(cardId, positions);
        }

        public override BattlePhaseAction OnBattle(IList<ClientCard> attackers, IList<ClientCard> defenders)
        {
            List<ClientCard> reflectors = attackers.Where(IsLiveReflector)
                .OrderByDescending(a => a.Id == CardId.Uzuhime && a.Overlays.Count > 0)
                .ThenByDescending(a => a.EquipCards.Any(e => e.Id == CardId.Mayowashidori))
                .ToList();
            if (reflectors.Count > 0 && defenders.Count > 0)
            {
                ClientCard target = defenders.Where(c => c.IsFaceup())
                    .OrderByDescending(ProjectedBattleDamage).ThenByDescending(ThreatScore).FirstOrDefault();
                if (target != null)
                {
                    Logger.DebugWriteLine("[Mikanko] battle reflector=" + reflectors[0].Id +
                        " target=" + target.Id + " projected=" + ProjectedBattleDamage(target) +
                        " threat=" + ThreatScore(target));
                    return AI.Attack(reflectors[0], target);
                }
            }
            if (defenders.Count == 0)
            {
                ClientCard direct = attackers.Where(c => c.CanDirectAttack && c.Attack > 0)
                    .OrderByDescending(c => c.Attack).FirstOrDefault();
                if (direct != null)
                    return AI.Attack(direct, null);
            }
            return base.OnBattle(attackers, defenders);
        }

        public override BattlePhaseAction OnSelectAttackTarget(ClientCard attacker, IList<ClientCard> defenders)
        {
            if (IsLiveReflector(attacker))
            {
                ClientCard target = defenders.Where(c => c.IsFaceup())
                    .OrderByDescending(ProjectedBattleDamage).ThenByDescending(ThreatScore).FirstOrDefault();
                if (target != null)
                    return AI.Attack(attacker, target);
            }
            return base.OnSelectAttackTarget(attacker, defenders);
        }

        private IEnumerable<ClientCard> OhimeSearchOrder(IEnumerable<ClientCard> cards)
        {
            bool hasArabesque = Bot.HasInHandOrInSpellZone(CardId.WaterArabesque);
            bool hasCeremony = Bot.HasInHandOrInSpellZone(CardId.GreatCeremony);
            bool hasNoble = Bot.HasInHandOrInSpellZone(CardId.NobleTwins);
            return cards.OrderByDescending(c =>
                c.Id == CardId.WaterArabesque && !hasArabesque ? 1000 :
                c.Id == CardId.GreatCeremony && !hasCeremony ? 950 :
                c.Id == CardId.NobleTwins && !hasNoble ? 900 :
                c.Id == CardId.FireDance && Bot.Graveyard.Any(x => x.HasSetcode(MikankoSetcode) && x.IsMonster()) ? 850 :
                c.Id == CardId.Rivalry ? 700 : c.Id == CardId.Mayowashidori ? 650 :
                c.Id == CardId.ReflectionRondo && Enemy.GetMonsterCount() > 0 ? 600 : 100);
        }

        private IEnumerable<ClientCard> EquipSearchOrder(IEnumerable<ClientCard> cards)
        {
            return cards.OrderByDescending(c =>
                c.Id == CardId.DoubleEdgedSword && HasReflector() && Enemy.GetMonsterCount() > 0 ? 1200 :
                c.Id == CardId.WaterArabesque && !Bot.HasInHandOrInSpellZone(CardId.WaterArabesque) ? 1100 :
                c.Id == CardId.Mayowashidori && Bot.Graveyard.Any(x => x.HasSetcode(MikankoSetcode) && x.IsMonster()) ? 950 :
                c.Id == CardId.ReflectionRondo && HasReflector() && Enemy.GetMonsterCount() > 0 ? 900 :
                c.Id == CardId.FireDance ? 800 : c.Id == CardId.DoubleEdgedSword ? 700 : 100);
        }

        private IEnumerable<ClientCard> NobleTwinsMillOrder(IEnumerable<ClientCard> cards)
        {
            return cards.OrderByDescending(c =>
                c.Id == CardId.GreatCeremony ? 1000 :
                c.Id == CardId.Mayowashidori && Bot.Graveyard.Any(x => x.HasSetcode(MikankoSetcode) && x.IsMonster()) ? 950 :
                c.Id == CardId.FireDance && Bot.Graveyard.Any(x => x.HasSetcode(MikankoSetcode) && x.IsMonster()) ? 900 :
                c.Id == CardId.DoubleEdgedSword && Bot.HasInMonstersZone(CardId.Ohime) ? 850 : 100);
        }

        private IEnumerable<ClientCard> CeremonyMillOrder(IEnumerable<ClientCard> cards)
        {
            return cards.OrderByDescending(c =>
                c.Id == CardId.DoubleEdgedSword && HasReflector() && Enemy.GetMonsterCount() > 0 ? 1200 :
                c.Id == CardId.Mayowashidori && Bot.Graveyard.Any(x => x.HasSetcode(MikankoSetcode) && x.IsMonster()) ? 1050 :
                c.Id == CardId.FireDance ? 900 : c.Id == CardId.WaterArabesque ? 800 : 100);
        }

        private IEnumerable<ClientCard> MikankoSetOrder(IEnumerable<ClientCard> cards)
        {
            bool summonOhime = Bot.HasInHand(CardId.Ohime) && !Bot.HasInMonstersZone(CardId.Ohime);
            return cards.OrderByDescending(c =>
                c.Id == CardId.GreatCeremony && summonOhime ? 1100 :
                c.Id == CardId.Rivalry ? 1000 : c.Id == CardId.FireDance ? 850 :
                c.Id == CardId.WaterArabesque ? 700 : c.Id == CardId.Mayowashidori ? 650 : 100);
        }

        private IEnumerable<ClientCard> MikankoSummonOrder(IEnumerable<ClientCard> cards)
        {
            return cards.OrderByDescending(c =>
                c.Id == CardId.Ohime ? 1200 : c.Id == CardId.HaRe ? 1000 :
                c.Id == CardId.HuLi ? 900 : c.Id == CardId.Sanaki ? 800 : 100);
        }

        private IEnumerable<ClientCard> ArabesqueSummonOrder(IEnumerable<ClientCard> cards)
        {
            bool needEquip = !Bot.HasInHandOrInSpellZone(CardId.DoubleEdgedSword) &&
                !Bot.Graveyard.Any(c => c.Id == CardId.DoubleEdgedSword);
            return cards.OrderByDescending(c =>
                c.Id == CardId.HaRe && needEquip ? 1200 :
                c.Id == CardId.Sanaki && !Bot.HasInMonstersZone(CardId.Sanaki) ? 1100 :
                c.Id == CardId.HuLi && !Bot.HasInMonstersZone(CardId.HuLi) ? 1000 :
                c.Id == CardId.HaRe ? 900 : 100);
        }

        private IEnumerable<ClientCard> EquipTargetOrder(IEnumerable<ClientCard> cards, int equipId)
        {
            IEnumerable<ClientCard> enemies = cards.Where(c => c.Controller == 1 && c.IsFaceup());
            IEnumerable<ClientCard> own = cards.Where(c => c.Controller == 0 && c.IsFaceup());
            if (equipId == CardId.DoubleEdgedSword)
                return enemies.OrderByDescending(c => Math.Max(c.Attack, c.Defense))
                    .ThenByDescending(ThreatScore).Concat(own.OrderByDescending(c => IsLiveReflector(c)));
            if (equipId == CardId.ReflectionRondo || equipId == CardId.WaterArabesque)
                return enemies.OrderByDescending(ThreatScore)
                    .Concat(own.OrderByDescending(c => c.HasSetcode(MikankoSetcode)));
            if (equipId == CardId.Sanaki)
                return own.OrderByDescending(c => c.Id == CardId.HaRe)
                    .ThenByDescending(c => c.Id == CardId.HuLi).Concat(enemies.OrderByDescending(ThreatScore));
            return own.OrderByDescending(IsLiveReflector)
                .ThenByDescending(c => c.Id == CardId.HaRe || c.Id == CardId.HuLi)
                .Concat(enemies.OrderByDescending(ThreatScore));
        }

        private IEnumerable<ClientCard> ThrustSearchOrder(IEnumerable<ClientCard> cards)
        {
            return cards.OrderByDescending(c =>
                c.Id == CardId.FeatherDuster && Enemy.GetSpellCount() >= 2 ? 1200 :
                c.Id == CardId.LightningStorm && Enemy.GetMonsterCount() + Enemy.GetSpellCount() >= 2 ? 1150 :
                c.Id == CardId.TripleTacticsTalent ? 1050 :
                c.Id == CardId.PreparationOfRites && !Bot.HasInHand(CardId.Ohime) ? 1000 :
                c.Id == CardId.HiddenArmory ? 900 : c.Id == CardId.NobleTwins ? 850 : 100);
        }

        private int PreferredEquipId(IEnumerable<ClientCard> cards)
        {
            ClientCard preferred = EquipSearchOrder(cards).FirstOrDefault();
            return preferred == null ? 0 : preferred.Id;
        }

        private int PreferredEquipIdFromDeck()
        {
            if (HasReflector() && Enemy.GetMonsterCount() > 0 && Bot.HasInDeck(CardId.DoubleEdgedSword))
                return CardId.DoubleEdgedSword;
            if (Enemy.GetMonsterCount() > 0 && Bot.HasInDeck(CardId.WaterArabesque))
                return CardId.WaterArabesque;
            if (HasReflector() && Enemy.GetMonsterCount() > 0 && Bot.HasInDeck(CardId.ReflectionRondo))
                return CardId.ReflectionRondo;
            if (Bot.HasInDeck(CardId.Mayowashidori))
                return CardId.Mayowashidori;
            return CardId.FireDance;
        }

        private int DiscardCost(ClientCard card)
        {
            if (card == null) return 10000;
            if (card.Id == CardId.GreatCeremony) return 0;
            if (card.Id == CardId.Mayowashidori) return 5;
            if (card.Id == CardId.NobleTwins) return 10;
            if (card.Id == CardId.FireDance) return 15;
            if (card.Id == CardId.PreparationOfRites && Bot.HasInHand(CardId.Ohime)) return 20;
            if (card.Id == CardId.PotOfSloth) return 25;
            if (card.Id == CardId.TripleTacticsThrust || card.Id == CardId.TripleTacticsTalent) return 30;
            if (card.Id == CardId.LightningStorm || card.Id == CardId.FeatherDuster) return Enemy.GetSpellCount() == 0 ? 35 : 90;
            if (card.Id == CardId.Nibiru || card.Id == CardId.MaxxC) return Duel.Player == 0 ? 40 : 120;
            if (card.Id == CardId.Ohime) return Bot.Hand.Count(c => c.Id == CardId.Ohime) > 1 ? 45 : 100;
            if (card.Id == CardId.DoubleEdgedSword || card.Id == CardId.WaterArabesque) return 110;
            return 60;
        }

        private int MaterialCost(ClientCard card)
        {
            if (card == null) return 10000;
            if (card.Controller == 1) return -1000 - ThreatScore(card);
            if (card.Id == CardId.Sanaki) return 0;
            if (card.Id == CardId.HaRe) return 10;
            if (card.Id == CardId.HuLi) return 25;
            if (card.Id == CardId.Ohime) return 90;
            if (card.Id == CardId.Uzuhime || card.Id == CardId.LittleKnight) return 100;
            return 50 + Math.Max(0, card.Attack / 200);
        }

        private int SanakiReturnCost(ClientCard card)
        {
            if (card == null) return 10000;
            if (card.Id == CardId.WaterArabesque || card.Id == CardId.FireDance ||
                card.Id == CardId.Mayowashidori) return 0;
            if (card.Id == CardId.Rivalry) return 10;
            if (card.Id == CardId.Sanaki) return 20;
            return 100;
        }

        private int ThreatScore(ClientCard card)
        {
            if (card == null) return 0;
            HashSet<int> negated = new HashSet<int>(Duel.NegatedChainIndexList.Select(i => i - 1));
            OpponentThreat assessment = _opponent.Assess(card, Duel.CurrentChainInfo, negated,
                card.IsDisabled(), Duel.Player == card.Controller);
            int score = (int)Math.Round(assessment.Removal * 100);
            if (card.IsFloodgate()) score += 5000;
            if (card.IsMonsterDangerous()) score += 3000;
            if (card.IsMonsterInvincible()) score += 2000;
            if (card.IsFaceup()) score += Math.Max(0, card.Attack) / 2;
            return score;
        }

        private int BestThreatScore()
        {
            return Enemy.GetMonsters().Concat(Enemy.GetSpells()).Select(ThreatScore).DefaultIfEmpty(0).Max();
        }

        private static bool IsKaijuId(int id)
        {
            return id == CardId.Jizukiru || id == CardId.ThunderKing;
        }

        private bool EnemyHasKaiju()
        {
            return Enemy.GetMonsters().Any(c => IsKaijuId(c.Id));
        }

        private static bool IsReflectorId(int id)
        {
            return id == CardId.HaRe || id == CardId.HuLi || id == CardId.Ohime || id == CardId.Uzuhime;
        }

        private bool IsLiveReflector(ClientCard card)
        {
            if (card == null || card.Controller != 0 || !card.IsFaceup() || card.IsDisabled())
                return false;
            if (card.Id == CardId.Ohime || card.Id == CardId.Uzuhime)
                return true;
            return (card.Id == CardId.HaRe || card.Id == CardId.HuLi) && card.EquipCards.Count > 0;
        }

        private bool HasReflector()
        {
            return Bot.GetMonsters().Any(IsLiveReflector);
        }

        private bool OwnMikankoInHandOrGrave()
        {
            return Bot.Hand.Concat(Bot.Graveyard).Any(c => c.IsMonster() && c.HasSetcode(MikankoSetcode));
        }

        private int ProjectedBattleDamage(ClientCard defender)
        {
            if (defender == null || !defender.IsFaceup())
                return 0;
            int power = defender.IsAttack() ? Math.Max(0, defender.Attack) : Math.Max(0, defender.Defense);
            bool doubleEdged = defender.EquipCards.Any(c => c.Id == CardId.DoubleEdgedSword && !c.IsDisabled());
            return power * (doubleEdged ? 2 : 1);
        }

        private int EstimatedReflectionDamage()
        {
            List<ClientCard> defenders = Enemy.GetMonsters().Where(c => c.IsFaceup()).ToList();
            if (defenders.Count == 0)
                return 0;
            int best = defenders.Max(ProjectedBattleDamage);
            int attacks = Bot.GetMonsters().Where(IsLiveReflector).Count();
            ClientCard uzuhime = Bot.GetMonsters().FirstOrDefault(c => c.Id == CardId.Uzuhime &&
                IsLiveReflector(c) && c.Overlays.Count > 0);
            if (uzuhime != null) attacks++;
            return best * attacks;
        }

        private bool MonsterRepos()
        {
            if (IsReflectorId(Card.Id))
                return Card.IsDefense();
            if (Card.Id == CardId.Sanaki)
                return Card.IsAttack() && Enemy.GetMonsters().Any();
            return DefaultMonsterRepos();
        }

        private void ValidateDeckListOnce()
        {
            if (_deckValidated)
                return;
            _deckValidated = true;
            List<string> drift = new List<string>();
            foreach (KeyValuePair<int, int> entry in MainDeckList)
            {
                int visible = Bot.GetCardCountInDeck(entry.Key) +
                    Bot.Hand.Count(c => c.Id == entry.Key) +
                    Bot.GetMonsters().Count(c => c.Id == entry.Key) +
                    Bot.GetSpells().Count(c => c.Id == entry.Key) +
                    Bot.Graveyard.Count(c => c.Id == entry.Key) +
                    Bot.Banished.Count(c => c.Id == entry.Key);
                if (visible != entry.Value)
                    drift.Add(entry.Key + " expected=" + entry.Value + " observed=" + visible);
            }
            foreach (KeyValuePair<int, int> entry in ExtraDeckList)
            {
                int visible = Bot.ExtraDeck.Count(c => c.Id == entry.Key);
                if (visible != entry.Value)
                    drift.Add("extra " + entry.Key + " expected=" + entry.Value + " observed=" + visible);
            }
            Logger.DebugWriteLine("[Mikanko] deck validation main=" + MainDeckList.Values.Sum() +
                " extra=" + ExtraDeckList.Values.Sum() + " drift=" +
                (drift.Count == 0 ? "none" : string.Join("; ", drift)));
        }

        public override void OnSelectChain(IList<ClientCard> cards)
        {
            _lastCoverageKey = null;
            base.OnSelectChain(cards);
        }

        public override void OnChaining(int player, ClientCard card)
        {
            _opponent.Observe(Duel.CurrentChainInfo);
            _acceptedId = 0;
            _acceptedDescription = -1;
            _lastCoverageKey = null;
            base.OnChaining(player, card);
        }

        public override void OnChainSolved(int chainIndex)
        {
            bool negated = Duel.NegatedChainIndexList.Contains(chainIndex);
            _opponent.Solved(Duel.CurrentChainInfo, chainIndex, negated);
            ChainInfo info = Duel.GetCurrentSolvingChainInfo();
            if (info != null && info.ActivatePlayer == 0 && !negated)
                _resolvedThisTurn.Add(info.ActivateId + ":" + EffectIndex(info.ActivateId, info.ActivateDescription));
            _lastCoverageKey = null;
            base.OnChainSolved(chainIndex);
        }

        public override void OnChainEnd()
        {
            _opponent.Observe(Duel.CurrentChainInfo);
            _opponent.EndChain();
            _acceptedId = 0;
            _acceptedDescription = -1;
            _selectedEquipId = 0;
            _lastCoverageKey = null;
            base.OnChainEnd();
        }

        public override void OnMove(ClientCard card, int previousControler, int previousLocation,
            int currentControler, int currentLocation)
        {
            _opponent.Observe(Duel.CurrentChainInfo);
            if (previousLocation != currentLocation)
                _opponent.ResetInstance(card);
            _lastCoverageKey = null;
            base.OnMove(card, previousControler, previousLocation, currentControler, currentLocation);
        }

        public override void OnSpSummoned()
        {
            _acceptedId = 0;
            _acceptedDescription = -1;
            _lastCoverageKey = null;
            base.OnSpSummoned();
        }

        public override void OnSummoning()
        {
            _acceptedId = 0;
            _acceptedDescription = -1;
            _lastCoverageKey = null;
            base.OnSummoning();
        }

        public override void OnNewPhase()
        {
            _acceptedId = 0;
            _acceptedDescription = -1;
            _lastCoverageKey = null;
            base.OnNewPhase();
        }

        public override void OnNewTurn()
        {
            _opponent.NewTurn();
            _acceptedThisTurn.Clear();
            _resolvedThisTurn.Clear();
            _acceptedId = 0;
            _acceptedDescription = -1;
            _selectedEquipId = 0;
            _lastCoverageKey = null;
            base.OnNewTurn();
        }
    }
}
