using YGOSharp.OCGWrapper.Enums;
using System.Collections.Generic;
using System.Linq;
using WindBot;
using WindBot.Game;
using WindBot.Game.AI;
using System;

namespace WindBot.Game.AI.Decks
{
    [Deck("ObsidianMagical", "AI_ObsidianMagical")]
    public class ObsidianMagicalExecutor : DefaultExecutor
    {
        public class CardId
        {
            public const int 遂行者 = 120254035;
            public const int 剑黎 = 120246080;
            public const int 黑曜 = 120246081;
            public const int DMG = 120181002;
            public const int DMA = 120130041;
            public const int DMB = 120203027;
            public const int 采掘 = 120203029;
            public const int 魔术的幕帘 = 120217018;
            public const int 二重加速 = 120246018;
            public const int 宝石 = 120246083;
            public const int 魔导书弃却 = 120246084;
            public const int 魔力抽出 = 120246085;
            public const int 七宝船 = 120199052;
            public const int 三猫 = 120249055;
            public const int 天惠枪 = 120244055;
            public const int 水流 = 120105012;
            public const int 幽灵旋风 = 120208053;

        }

        public ObsidianMagicalExecutor(GameAI ai, Duel duel)
            : base(ai, duel)
        {
            AddExecutor(ExecutorType.Activate, _CardId.魔法筒, 魔法筒Effect);
            AddExecutor(ExecutorType.Activate, CardId.DMA, DMAEffect);
            AddExecutor(ExecutorType.Activate, CardId.幽灵旋风);
            AddExecutor(ExecutorType.Activate, CardId.水流);
            AddExecutor(ExecutorType.Activate, CardId.三猫, 三猫Effect);
            AddExecutor(ExecutorType.Activate, _CardId.黑洞, 黑洞Effect);
                        
            AddExecutor(ExecutorType.Activate, CardId.魔导书弃却);
            AddExecutor(ExecutorType.Activate, CardId.七宝船, 七宝船Effect);
            AddExecutor(ExecutorType.Activate, CardId.遂行者, 遂行者Effect);            
            AddExecutor(ExecutorType.Activate, CardId.采掘, 采掘Effect);

            AddExecutor(ExecutorType.SpellSet, CardId.魔导书弃却, 抽出set);
            AddExecutor(ExecutorType.SpellSet, CardId.三猫, 抽出set);
            AddExecutor(ExecutorType.SpellSet, CardId.魔力抽出, 抽出set);
            AddExecutor(ExecutorType.SpellSet, CardId.幽灵旋风, 抽出set);
            AddExecutor(ExecutorType.SpellSet, CardId.水流, 抽出set);
            AddExecutor(ExecutorType.SpellSet, CardId.天惠枪, 抽出set);
            AddExecutor(ExecutorType.SpellSet, CardId.DMB, 抽出set);
            AddExecutor(ExecutorType.SpellSet, CardId.魔导书弃却, 抽出set);            
            AddExecutor(ExecutorType.SpellSet, CardId.魔术的幕帘, 抽出set);
            AddExecutor(ExecutorType.SpellSet, CardId.宝石, 抽出set);
            AddExecutor(ExecutorType.SpellSet, CardId.采掘, 抽出set);
            AddExecutor(ExecutorType.SpellSet, CardId.DMA, 抽出set);
            AddExecutor(ExecutorType.Activate, CardId.魔力抽出, 魔力抽出Effect);

            AddExecutor(ExecutorType.SpSummon, CardId.剑黎, 抽出sp);
            AddExecutor(ExecutorType.Activate, CardId.宝石, 宝石Effect);
            AddExecutor(ExecutorType.Activate, CardId.采掘, 采掘Effect2);
            AddExecutor(ExecutorType.Activate, CardId.七宝船, 七宝船Effect2);
            AddExecutor(ExecutorType.Activate, CardId.魔术的幕帘, 幕帘Effect);
            AddExecutor(ExecutorType.SpellSet, CardId.七宝船, 抽出set);
            AddExecutor(ExecutorType.Activate, CardId.二重加速, 二重加速Effect);
            AddExecutor(ExecutorType.Activate, CardId.DMB, DMBEffect);
            AddExecutor(ExecutorType.Activate, CardId.天惠枪, 天惠枪Effect);

            AddExecutor(ExecutorType.SpellSet, CardId.魔力抽出, speset);
            AddExecutor(ExecutorType.SpellSet, CardId.魔术的幕帘, 幕帘set);
            AddExecutor(ExecutorType.SpellSet, CardId.七宝船, speset);
            AddExecutor(ExecutorType.SpellSet, CardId.采掘, speset);
            AddExecutor(ExecutorType.SpellSet, CardId.幽灵旋风, speset);
            AddExecutor(ExecutorType.SpellSet, CardId.宝石, speset);
            AddExecutor(ExecutorType.SpellSet, CardId.DMB, speset);
            AddExecutor(ExecutorType.SpellSet, CardId.二重加速, speset);
            AddExecutor(ExecutorType.SpellSet, CardId.水流, speset);           
            AddExecutor(ExecutorType.SpellSet, CardId.天惠枪, speset);
            AddExecutor(ExecutorType.SpellSet, CardId.三猫, speset);            
            AddExecutor(ExecutorType.SpellSet, _CardId.魔法筒);
            AddExecutor(ExecutorType.SpellSet, CardId.魔术的幕帘);
            AddExecutor(ExecutorType.SpellSet, CardId.宝石);              
            AddExecutor(ExecutorType.SpellSet, _CardId.黑洞);
            AddExecutor(ExecutorType.SpellSet, CardId.DMA);

            AddExecutor(ExecutorType.Summon, CardId.DMG, DMGsummon);
            AddExecutor(ExecutorType.Activate, CardId.宝石, 宝石Effect2);

            //AddExecutor(ExecutorType.Activate, DefaultDontChainMyself);          
            
            AddExecutor(ExecutorType.Repos, CardId.黑曜, 反转Repos);
            AddExecutor(ExecutorType.Repos, _CardId.黑魔术师, 反转Repos);
            AddExecutor(ExecutorType.Repos, CardId.遂行者, 反转Repos);
            AddExecutor(ExecutorType.Repos, _CardId.黑魔术师, 圣防Repos);
            AddExecutor(ExecutorType.Repos, CardId.遂行者, 圣防Repos);
            AddExecutor(ExecutorType.Repos, CardId.DMG, 圣防Repos);
            AddExecutor(ExecutorType.Repos, DefaultMonsterRepos);

            //AddExecutor(ExecutorType.SummonOrSet, ImFeelingLazy);
        }


        private List<int> HintMsgForEnemy = new List<int>
        {
            HintMsg.Release, HintMsg.Destroy, HintMsg.Remove, HintMsg.ToGrave, HintMsg.ReturnToHand, HintMsg.ToDeck,
            HintMsg.FusionMaterial, HintMsg.SynchroMaterial, HintMsg.XyzMaterial, HintMsg.LinkMaterial, HintMsg.Disable
        };

        private List<int> HintMsgForDeck = new List<int>
        {
            HintMsg.SpSummon, HintMsg.ToGrave, HintMsg.Remove, HintMsg.AddToHand, HintMsg.FusionMaterial
        };
        private List<int> HintMsgForExtra = new List<int>
        {
            HintMsg.SpSummon, HintMsg.ToGrave, HintMsg.Remove, HintMsg.AddToHand, HintMsg.FusionMaterial
        };
        private List<int> HintMsgForSelf = new List<int>
        {
            HintMsg.Equip
        };

        private List<int> HintMsgForMaterial = new List<int>
        {
            HintMsg.FusionMaterial, HintMsg.SynchroMaterial, HintMsg.XyzMaterial, HintMsg.LinkMaterial, HintMsg.Release
        };

        private List<int> HintMsgForMaxSelect = new List<int>
        {
            HintMsg.SpSummon, HintMsg.ToGrave, HintMsg.AddToHand, HintMsg.FusionMaterial, HintMsg.Destroy, HintMsg.Equip ,HintMsg.Confirm
        };


        public override IList<ClientCard> OnSelectCard(IList<ClientCard> _cards, int min, int max, int hint, bool cancelable)
        {
            if (Duel.Phase == DuelPhase.BattleStart)
                return null;
            if (AI.HaveSelectedCards())
                return null;

            IList<ClientCard> selected = new List<ClientCard>();
            IList<ClientCard> cards = new List<ClientCard>(_cards);
            if (max > cards.Count)
                max = cards.Count;

            if (HintMsgForEnemy.Contains(hint))
            {
                IList<ClientCard> enemyCards = cards.Where(card => card.Controller == 1).ToList();

                // select enemy's card first
                while (enemyCards.Count > 0 && selected.Count < max)
                {
                    ClientCard card = enemyCards[Program.Rand.Next(enemyCards.Count)];
                    selected.Add(card);
                    enemyCards.Remove(card);
                    cards.Remove(card);
                }
            }

            if (HintMsgForDeck.Contains(hint))
            {
                IList<ClientCard> deckCards = cards.Where(card => card.Location == CardLocation.Deck).ToList();

                // select deck's card first
                while (deckCards.Count > 0 && selected.Count < max)
                {
                    ClientCard card = deckCards[Program.Rand.Next(deckCards.Count)];
                    selected.Add(card);
                    deckCards.Remove(card);
                    cards.Remove(card);
                }
            }
            if (HintMsgForExtra.Contains(hint))
            {
                IList<ClientCard> ExtraCards = cards.Where(card => card.Location == CardLocation.Extra).ToList();

                // select extra's card first
                while (ExtraCards.Count > 0 && selected.Count < max)
                {
                    ClientCard card = ExtraCards[Program.Rand.Next(ExtraCards.Count)];
                    selected.Add(card);
                    ExtraCards.Remove(card);
                    cards.Remove(card);
                }
            }

            if (HintMsgForSelf.Contains(hint))
            {
                IList<ClientCard> botCards = cards.Where(card => card.Controller == 0).ToList();

                // select bot's card first
                while (botCards.Count > 0 && selected.Count < max)
                {
                    ClientCard card = botCards[Program.Rand.Next(botCards.Count)];
                    selected.Add(card);
                    botCards.Remove(card);
                    cards.Remove(card);
                }
            }

            if (HintMsgForMaterial.Contains(hint))
            {
                IList<ClientCard> materials = cards.OrderBy(card => card.Attack).ToList();

                // select low attack first
                while (materials.Count > 0 && selected.Count < min)
                {
                    ClientCard card = materials[0];
                    selected.Add(card);
                    materials.Remove(card);
                    cards.Remove(card);
                }
            }

            // select random cards
            while (selected.Count < min)
            {
                ClientCard card = cards[Program.Rand.Next(cards.Count)];
                selected.Add(card);
                cards.Remove(card);
            }

            if (HintMsgForMaxSelect.Contains(hint))
            {
                // select max cards
                while (selected.Count < max)
                {
                    ClientCard card = cards[Program.Rand.Next(cards.Count)];
                    selected.Add(card);
                    cards.Remove(card);
                }
            }

            return selected;
        }

        public override int OnSelectOption(IList<int> options)
        {
            return Program.Rand.Next(options.Count);
        }

        public override CardPosition OnSelectPosition(int cardId, IList<CardPosition> positions)
        {
            YGOSharp.OCGWrapper.NamedCard cardData = YGOSharp.OCGWrapper.NamedCard.Get(cardId);
            if (cardData != null)
            {
                if (cardData.Attack < 0)
                    return CardPosition.FaceUpAttack;
                if (cardData.Attack <= 1000)
                    return CardPosition.FaceUpDefence;
            }
            return 0;
        }

        public override bool OnSelectHand()
        {
            // go first
            return true;
        }

        public void RandomSort(List<ClientCard> list)
        {

            int n = list.Count;
            while (n-- > 1)
            {
                int index = Program.Rand.Next(n + 1);
                ClientCard temp = list[index];
                list[index] = list[n];
                list[n] = temp;
            }
        }
        public ClientCard GetBelowLv8()
        {
            List<ClientCard> enemyMonsters = Enemy.GetMonsters();
            ClientCard highestLevelMonster = null;
            foreach (ClientCard monster in enemyMonsters)
            {
                if (monster.Level < 8)
                {
                    if (highestLevelMonster == null || monster.Level > highestLevelMonster.Level)
                    {
                        highestLevelMonster = monster;
                    }
                }
            }
            return highestLevelMonster;
        }

        public override BattlePhaseAction OnSelectAttackTarget(ClientCard attacker, IList<ClientCard> defenders)
        {
            int minAttack = 1450;
            foreach (ClientCard defender in defenders)
            {
                if (defender.IsAttack() && defender.Attack < minAttack)
                    minAttack = defender.Attack;
            }
            foreach (ClientCard defenderCard in defenders)
                if (defenderCard.IsAttack() && defenderCard.Attack == minAttack)
                {
                    return AI.Attack(attacker, defenderCard);
                }
            return base.OnSelectAttackTarget(attacker, defenders);
        }

        private IList<int> over3 = new[] {
                _CardId.黑洞, CardId.DMA, CardId.宝石, CardId.二重加速, CardId.DMB, CardId.魔力抽出, CardId.幽灵旋风, CardId.水流, CardId.魔导书弃却, CardId.天惠枪, CardId.魔术的幕帘, CardId.七宝船, CardId.采掘, CardId.三猫};

        private IList<int> target = new[] {
                CardId.三猫, CardId.宝石, _CardId.黑洞, CardId.DMB, CardId.魔导书弃却, CardId.幽灵旋风,CardId.魔术的幕帘,  CardId.水流,CardId.天惠枪, CardId.二重加速, CardId.七宝船, CardId.魔力抽出, CardId.DMA, CardId.采掘};

        private IList<int> cost2 = new[] {

                CardId.三猫, CardId.幽灵旋风,CardId.水流, CardId.DMB, CardId.二重加速, CardId.采掘,CardId.魔术的幕帘, CardId.七宝船, CardId.魔导书弃却, CardId.天惠枪, CardId.魔力抽出, CardId.宝石,CardId.DMA, _CardId.黑洞 };

        private IList<int> mon = new[] {

                _CardId.黑魔术师, CardId.遂行者, CardId.黑曜, CardId.剑黎};

        private IList<int> mon2 = new[] {
                CardId.黑曜,  CardId.遂行者, CardId.剑黎 ,_CardId.黑魔术师};

        private IList<int> mon3 = new[] {
                CardId.黑曜,  CardId.遂行者, CardId.剑黎 ,_CardId.黑魔术师 ,CardId.DMG};

        private bool speset()
        {
            if (Bot.GetSpellCountWithoutField() <= 1)
                return true;
            return false;
        }
        private bool 抽出set()
        {
            if (Bot.HasInSpellZone(CardId.魔力抽出) && Bot.GetSpellCountWithoutField() < 3)
                return true;
            if (Bot.HasInHand(CardId.魔力抽出) && Bot.GetSpellCountWithoutField() < 2)
                return true;
            return false;
        }
        private bool 幕帘set()
        {
            if (Bot.LifePoints > 1000 && Bot.GetSpellCountWithoutField() <= 1)
                return true;
            return false;
        }

        private bool 抽出sp()
        {
            if (Bot.HasInHand(CardId.魔力抽出) && Bot.Hand.GetMatchingCardsCount(card => card.Level >= 6) == 1 )       
                return false;
            return true;
        }
        private bool DMGsummon()
        {
            int tributecount = 1;
            IList<int> ssmon = new[] {
                CardId.DMG, _CardId.黑魔术师, CardId.剑黎, CardId.遂行者};
            if ((Bot.GetHandCount() <= 4) && (Bot.HasInMonstersZone(ssmon)) && (Bot.HasInMonstersZoneOrInGraveyard(_CardId.黑魔术师)))
            for (int j = 0; j < 7; ++j)
            {
                ClientCard tributeCard = Bot.MonsterZone[j];
                if (tributeCard == null) continue;
                if (tributeCard.IsCode(ssmon))
                    tributecount--;
            }
            return tributecount <= 0;
        }

        private bool 遂行者Effect()
        {
            int hya;
            if (Bot.GetGraveyardSpells().Count >= 15)
                hya = 1000;
            else
                hya = 1500;
            ClientCard highest8 = GetBelowLv8();

            if (!Bot.HasInMonstersZone(CardId.黑曜) && Bot.GetGraveyardSpells().Count >= 10)
            {
                AI.SelectYesNo(true);
                if (Bot.GetGraveyardMonsters().Count > 3)
                {
                    AI.SelectCard(over3);
                    if (highest8 != null)
                        AI.SelectNextCard(highest8);
                }
                if (Bot.GetGraveyardMonsters().Count <= 3)
                {
                    AI.SelectCard(target);
                    if (highest8 != null)
                        AI.SelectNextCard(highest8);
                }
            }
            if (Bot.HasInMonstersZone(CardId.黑曜) && Bot.GetGraveyardSpells().Count >= 10 && Util.GetBestAttack(Bot) - Util.GetBestAttack(Enemy) >= hya)
            {
                AI.SelectYesNo(true);
                if (Bot.GetGraveyardMonsters().Count > 3) AI.SelectCard(over3); AI.SelectNextCard(Enemy.MonsterZone.GetHighestAttackMonster());
                if (Bot.GetGraveyardMonsters().Count <= 3) AI.SelectCard(target); AI.SelectNextCard(Enemy.MonsterZone.GetHighestAttackMonster());
            }
            if (Bot.HasInMonstersZone(CardId.黑曜) && Bot.GetGraveyardSpells().Count >= 10 && Util.GetBestAttack(Bot) - Util.GetBestAttack(Enemy) < hya)
            {
                AI.SelectYesNo(false);
            }
            if (Bot.GetGraveyardSpells().Count < 10)
                return true;
            return false;
        }

        private bool DMAEffect()
        {
            if (Enemy.GetSpellCount() >= 2 
                && (Bot.HasInMonstersZone(_CardId.黑魔术师) || Bot.HasInMonstersZone(CardId.遂行者)))
                return true;
            if (Enemy.GetSpellCount() == 1 
                && (Bot.HasInMonstersZone(_CardId.黑魔术师) || Bot.HasInMonstersZone(CardId.遂行者))
                && Bot.MonsterZone.GetMatchingCardsCount(card => card.IsAttack() && card.Attack >= Enemy.LifePoints) >= 2)
                return true;
            return false;
        }

        private bool 七宝船Effect()
        {
            if (!Bot.HasInHand(CardId.魔力抽出) && Bot.HasInHand(mon))
            {
                AI.SelectCard(mon);
                AI.SelectYesNo(true);
                return true;
            }
            return false;
        }
        private bool 七宝船Effect2()
        {
            if (Bot.HasInHand(mon))
            {
                AI.SelectCard(mon);
                AI.SelectYesNo(true);
                return true;
            }
            else if (Bot.HasInHand(CardId.DMG) && Bot.GetSpellCount() >= 2 && Bot.GetHandCount() >=4)
            {
                AI.SelectCard(CardId.DMG);
                return true;
            }
            return false;
        }
        private bool 魔力抽出Effect()
        {
            if (Bot.HasInSpellZone(cost2))
            {
                AI.SelectCard(cost2);
                if (Bot.HasInHand(mon3))
                {
                AI.SelectYesNo(true);
                AI.SelectNextCard(mon3);
                }               
            }
            return true; ;
        }
        private bool 幕帘Effect()
        {
            if (Bot.LifePoints > 1000)
            {
                if (Bot.HasInHandOrInSpellZone(CardId.DMA) && (Bot.HasInHand(CardId.遂行者) || Bot.HasInHand(_CardId.黑魔术师)) && Enemy.GetSpellCount() >= 2)
                {
                    AI.SelectCard(CardId.遂行者, _CardId.黑魔术师);
                    return true;
                }
                if (Bot.HasInHand(CardId.黑曜) && Bot.GetGraveyardSpells().Count >= 5)
                {
                    AI.SelectCard(CardId.黑曜);
                    return true;
                }
                if (Bot.HasInHand(CardId.遂行者))
                {
                    AI.SelectCard(CardId.遂行者);
                    return true;
                }
                if (Bot.HasInHand(mon3)) AI.SelectCard(mon3);
                return true;
            }
            return false;
        }

        private bool 天惠枪Effect()
        {
            if (Bot.HasInMonstersZone(CardId.黑曜))
            {
                AI.SelectCard(CardId.黑曜);
                return true;
            }
            if (Bot.HasInMonstersZone(CardId.遂行者))
            {
                AI.SelectCard(CardId.遂行者);
                return true;
            }
            if (Bot.HasInMonstersZone(_CardId.黑魔术师))
            {
                AI.SelectCard(_CardId.黑魔术师);
                return true;
            }
            if (Bot.HasInMonstersZone(CardId.剑黎))
            {
                AI.SelectCard(CardId.剑黎);
                return true;
            }
            return true;
        }
        private bool 二重加速Effect()
        {
            if (Duel.Turn == 1)
                return false;
            if (!Enemy.HasAttackingMonster() && Enemy.HasDefendingMonster() && Enemy.GetMonsterCount() >= Bot.GetMonsterCount())
                return false;
            else
                AI.SelectCard();
                AI.SelectNextCard(mon3);
            return true;
        }
        private bool DMBEffect()
        {
            if (Duel.Turn == 1)
                return false;
            if (!Enemy.HasAttackingMonster() && Enemy.HasDefendingMonster() && Enemy.GetMonsterCount() >= Bot.GetMonsterCount())
                return false;
            else
                AI.SelectCard(mon3);
            return true;
        }
        private bool 三猫Effect()
        {
            if (Bot.GetGraveyardMonsters().Count <= 3)
            {
                AI.SelectYesNo(true);
                if (Bot.GetGraveyardMonsters().Count >= 2 && Bot.Hand.GetMatchingCardsCount(card => card.Level >= 7) >= 2 && Bot.HasInGraveyard(CardId.七宝船))
                {
                    AI.SelectCard(CardId.七宝船);
                    return true;
                }
                if (Bot.GetGraveyardMonsters().Count >= 2 && !Bot.GetMonsters().Any() && Bot.HasInGraveyard(CardId.宝石) && !Bot.HasInHandOrInSpellZone(CardId.宝石))
                {
                    AI.SelectCard(CardId.宝石);
                    return true;
                }
                if (Bot.GetGraveyardMonsters().Count >= 2 && Bot.Hand.GetMatchingCardsCount(card => card.Level >= 6) == 0 && Bot.HasInGraveyard(CardId.宝石))
                {
                    AI.SelectCard(CardId.宝石);
                    return true;
                }
                if (Bot.GetGraveyardMonsters().Count >= 2 && !Bot.GetMonsters().Any() && Bot.HasInHand(mon3) && Bot.HasInGraveyard(CardId.魔术的幕帘) && !Bot.HasInHandOrInSpellZoneOrInGraveyard(CardId.宝石))
                {
                    AI.SelectCard(CardId.魔术的幕帘);
                    return true;
                }
                if (Bot.GetGraveyardMonsters().Count >= 2 && Enemy.GetSpellCount() > 2 && (Bot.HasInMonstersZone(_CardId.黑魔术师) || Bot.HasInMonstersZone(CardId.遂行者)) && Bot.HasInGraveyard(CardId.DMA))
                {
                    AI.SelectCard(CardId.DMA);
                    return true;
                }
                if (Bot.GetGraveyardMonsters().Count >= 2 && Bot.HasInGraveyard(CardId.天惠枪) && !Enemy.HasAttackingMonster() && (Bot.HasInMonstersZone(CardId.黑曜) || Bot.HasInHandOrInSpellZone(CardId.宝石)))
                {
                    AI.SelectCard(CardId.天惠枪);
                    return true;
                }
                if (Bot.GetGraveyardMonsters().Count >= 2 && Bot.Hand.GetMatchingCardsCount(card => card.Level >= 6) == 0 && Bot.HasInGraveyard(CardId.剑黎))
                {
                    AI.SelectCard(CardId.剑黎);
                    return true;
                }
                else
                    AI.SelectCard(target);
                return true;
            }
            else if (!Bot.HasInHand(CardId.魔导书弃却) && Bot.HasInMonstersZone(CardId.黑曜))
            {
                return true;
            }
            else if (Bot.GetHandCount() >= 5)  
            {
                return true;
            }
            return false;
        }
        private bool 采掘Effect()
        {
            if (Util.IsOneEnemyBetter() && Util.GetTotalAttackingMonsterAttack(1) >= 3500 && !Enemy.HasInMonstersZone(_CardId.终焰魔神) && Bot.HasInGraveyard(_CardId.黑洞))
            {
                AI.SelectCard(cost2);
                AI.SelectNextCard(_CardId.黑洞);
                return true;
            }
            if (Enemy.GetMonsterCount() >0 && Bot.GetGraveyardMonsters().Count < 3 && Bot.HasInGraveyard(CardId.三猫) )
            {
                AI.SelectCard(cost2);
                AI.SelectNextCard(CardId.三猫);
                return true;
            }
            if (Bot.GetMonsterCount() <= 1 && Bot.HasInGraveyard(CardId.宝石) && (Bot.HasInHandOrInGraveyard(CardId.DMG) && Bot.HasInHandOrInGraveyard(mon2)))
            {
                AI.SelectCard(cost2);
             AI.SelectNextCard(CardId.宝石);
             return true;
            }
            if (Bot.Hand.GetMatchingCardsCount(card => card.Level >= 6) >= 3 && Bot.HasInGraveyard(CardId.七宝船))
            {
                AI.SelectCard(cost2);
                AI.SelectNextCard(CardId.七宝船);
                return true;
            }
            if (Bot.HasInMonstersZone(CardId.黑曜) && Bot.HasInGraveyard(CardId.天惠枪) && !Enemy.HasAttackingMonster() && Enemy.HasDefendingMonster())
            {
                AI.SelectCard(cost2);
                AI.SelectNextCard(CardId.天惠枪);
                return true;
            }

            return false;
        }
        private bool 采掘Effect2()
        {
            if (Enemy.GetSpellCount() >= 2 && (Bot.HasInMonstersZone(_CardId.黑魔术师) || Bot.HasInMonstersZone(CardId.遂行者)) && Bot.HasInGraveyard(CardId.DMA))
            {
                AI.SelectCard(cost2);
                AI.SelectNextCard(CardId.DMA);
                return true;
            }
            if (Bot.Deck.Count >= 10 && Bot.HasInGraveyard(CardId.魔导书弃却))
            {
                AI.SelectCard(cost2);
                AI.SelectNextCard(CardId.魔导书弃却);
                return true;
            }

            if (Duel.Turn != 1 && Bot.HasInGraveyard(CardId.二重加速) && Bot.HasAttackingMonster() && Bot.Graveyard.GetMatchingCardsCount(card => card.Level > 5) >= 2)
            {
                AI.SelectCard(cost2);
                AI.SelectNextCard(CardId.二重加速);
                return true;
            }
            return false;
        }
        private bool 宝石Effect()
        {
            if (Bot.GetMonsterCount() <= 1)
            {
                if (Bot.HasInHandOrInGraveyard(CardId.DMG) && Bot.HasInHandOrInGraveyard(mon2))
                    AI.SelectCard(mon3);
                    return true;
            }
            return false;
        }
        private bool 宝石Effect2()
        {
            if (Bot.GetMonsterCount() == 2)
            {
                if (Bot.HasInGraveyard(mon2) && Enemy.GetSpellCountWithoutField() <= 1)
                    AI.SelectCard(mon2);
                return true;
            }
            return false;
        }

    }
}





