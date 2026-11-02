using YGOSharp.OCGWrapper.Enums;
using System.Collections.Generic;
using System.Linq;
using WindBot;
using WindBot.Game;
using WindBot.Game.AI;
using System;

//剩余BUG：
//攻击对象不懂得选择
namespace WindBot.Game.AI.Decks
{
    [Deck("Mituzi", "AI_Mituzi")]
    public class MituziExecutor : DefaultExecutor
    {
        public class CardId
        {
            public const int 幻煌 = 120247063;
            public const int 起重龙 = 120199020;
            public const int 幻龙重骑 = 120155022;
            public const int 建造龙 = 120155024;
            public const int 幻刃破龙 = 120151029;
            public const int 铲子龙 = 120155002;
            public const int 搅拌龙 = 120192010;
            public const int 荒废蟠龙 = 120192011;
            public const int 风车鳞虫 = 120183034;
            public const int 丰沃蟠龙 = 120170037;
            public const int 高空工龙 = 120247046;
            public const int 镐头龙 = 120155025;
            public const int 贯通 = 120120037;
            public const int 阴阳封阵 = 120183053;
            public const int 死者苏生 = 120194004;
            public const int 透幻乡险峻 = 120155042;
            public const int 风 = 120155044;
            public const int 幻刃复归 = 120151033;
            public const int 幻刃急攻 = 120155053;
            public const int 幻刃封锁 = 120155054;
            public const int 幻坏奥义 = 120247058;
            public const int 暗黑释放 = 120105013;
        }


        public int[] priList = { CardId.起重龙, CardId.建造龙, CardId.幻龙重骑,  CardId.幻煌, CardId.幻刃破龙 };
        public int[] rePriList = { CardId.幻刃破龙, CardId.幻煌, CardId.幻龙重骑, CardId.建造龙, CardId.起重龙, };
        public MituziExecutor(GameAI ai, Duel duel)
            : base(ai, duel)
        {
            AddExecutor(ExecutorType.Activate, _CardId.黑洞, 黑洞Effect);
            AddExecutor(ExecutorType.SpSummon);
            AddExecutor(ExecutorType.Activate, _CardId.魔法筒, 魔法筒Effect);
            AddExecutor(ExecutorType.Activate, _CardId.炸甲, 炸甲Effect);
            AddExecutor(ExecutorType.Activate, _CardId.落穴, 落穴Effect);
            AddExecutor(ExecutorType.Activate, _CardId.激流葬, 激流葬Effect);
            AddExecutor(ExecutorType.Activate, _CardId.圣防, 圣防Effect);
            AddExecutor(ExecutorType.Activate, CardId.幻坏奥义, 奥义Effect);
            AddExecutor(ExecutorType.Activate, CardId.阴阳封阵, 阴阳封阵Effect);
            AddExecutor(ExecutorType.SpellSet, CardId.阴阳封阵);
            AddExecutor(ExecutorType.Activate, CardId.透幻乡险峻,DefaultField);
            AddExecutor(ExecutorType.Repos, 幻刃急攻Repos);
            AddExecutor(ExecutorType.Activate, CardId.风);
            AddExecutor(ExecutorType.Activate, CardId.丰沃蟠龙,丰沃蟠龙Effect);
            AddExecutor(ExecutorType.Activate, CardId.荒废蟠龙, 荒废蟠龙Effect);
            AddExecutor(ExecutorType.Activate, CardId.搅拌龙, 搅拌龙Effect);
            AddExecutor(ExecutorType.Activate, CardId.风车鳞虫);
            AddExecutor(ExecutorType.Activate, CardId.起重龙);
            AddExecutor(ExecutorType.Activate, CardId.建造龙, 建造龙Effect);
            AddExecutor(ExecutorType.Activate, CardId.镐头龙, 镐头龙Effect);
            AddExecutor(ExecutorType.Summon, CardId.幻煌, RushMonsterSummon);
            AddExecutor(ExecutorType.Summon, _CardId.伟大魔兽, 伟大魔兽Summon);
            AddExecutor(ExecutorType.Summon, 幻刃急攻Summon);

            AddExecutor(ExecutorType.Summon, CardId.镐头龙, 镐头龙Summon);
            AddExecutor(ExecutorType.Summon, CardId.搅拌龙, 搅拌龙Summon);
            AddExecutor(ExecutorType.MonsterSet, 幻刃急攻Set);          
            AddExecutor(ExecutorType.Activate, CardId.死者苏生, 死苏Effect);
            AddExecutor(ExecutorType.Activate, CardId.贯通, 贯通Effect);
            AddExecutor(ExecutorType.SummonOrSet, RushMonsterSummon);
            AddExecutor(ExecutorType.Activate, CardId.幻刃复归, 幻刃复归Effect);
            AddExecutor(ExecutorType.Activate, CardId.幻刃封锁, 幻刃封锁Effect);
            AddExecutor(ExecutorType.Activate, CardId.幻刃急攻, 幻刃急攻Effect);
            AddExecutor(ExecutorType.Activate, CardId.透幻乡险峻);
            AddExecutor(ExecutorType.Activate, _CardId.破灭之龙魔导士, 破灭之龙魔导士Effect);
            AddExecutor(ExecutorType.Activate, _CardId.星战骑佩流安, 星战骑佩流安Effect);
            AddExecutor(ExecutorType.Activate, _CardId.加百列热茶, 加百列热茶Effect);
            AddExecutor(ExecutorType.Activate, DefaultDontChainMyself);
            AddExecutor(ExecutorType.Repos, DefaultMonsterRepos);
            AddExecutor(ExecutorType.Repos, CardId.幻煌,反转Repos);
            AddExecutor(ExecutorType.Repos, CardId.起重龙,反转Repos);
            AddExecutor(ExecutorType.Repos, CardId.建造龙, 反转Repos);
            AddExecutor(ExecutorType.Repos, 圣防Repos);
            AddExecutor(ExecutorType.SpellSet, TrapCardSet);
            AddExecutor(ExecutorType.SpellSet);
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
            HintMsg.FusionMaterial, HintMsg.SynchroMaterial, HintMsg.XyzMaterial, HintMsg.LinkMaterial, HintMsg.Tribute
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

         private bool TrapCardSet()
         {
             if (Card.HasType(CardType.Trap)) return true;
             return false;
         }
         private bool MonsRepos()
         {
             if (Card.Id == CardId.镐头龙)
             {
                 if (Card.IsAttack()) return true;
                 if (Card.IsFacedown())
                     return Discord1Draw1Eff(CardRace.Wyrm, false);

                 return false;
             }
             if (Card.Id == CardId.风车鳞虫)
             {
                 if (Card.IsFacedown())
                 {
                     ClientCard card = Util.GetWorstEnemyMonster();
                     if (card == null) return true;
                     if (Bot.HasInSpellZone(CardId.透幻乡险峻))
                     {
                         if (Card.Attack + 400 >= (card.IsAttack() ? card.Attack : card.Defense))
                             return true;
                         else return false;
                     }
                     else
                     {
                         if (Card.Attack >= (card.IsAttack() ? card.Attack : card.Defense))
                             return true;
                         else return false;
                     }
                 }
             }
             if (Card.Id == CardId.丰沃蟠龙)
             {
                 if (Card.IsFacedown())
                     return Bot.GetMonsterCount()>1&&
                         Bot.Hand.GetMatchingCardsCount(card => card.Level > 4) > 0;
             }
             ClientCard emMon = Util.GetWorstEnemyMonster();
             if (emMon == null) return Card.IsDefense();
             else
             {
                 if (Card.Attack >= emMon.GetDefensePower()) return Card.IsDefense();
                 else return Card.IsAttack();
             }

         }

         private bool 镐头龙Summon()
         {
             if (Bot.HasInMonstersZone(CardId.幻刃破龙) || (Bot.HasInMonstersZone(CardId.幻煌)) && Bot.HasInHand(CardId.建造龙) && Bot.HasInHand(CardId.起重龙))
                 return Discord1Draw1Eff(CardRace.Wyrm, false, true);
             return Discord1Draw1Eff(CardRace.Wyrm, false, false);
         }
         private bool 风车鳞虫Summon()
         {
             ClientCard card = Util.GetWorstEnemyMonster();
             if (card == null) return true;
             if (Bot.HasInSpellZone(CardId.透幻乡险峻))
             {
                 if (Card.Attack + 400 >= (card.IsAttack() ? card.Attack : card.Defense))
                     return true;
                 else return false;
             }
             else
             {
                 if (Card.Attack >= (card.IsAttack() ? card.Attack : card.Defense))
                     return true;
                 else return false;
             }

         }
         private bool 搅拌龙Summon()
         {
             return 搅拌龙Effect(false);
         }

    
       // protected override IList<ClientCard> OnSelectTribute(IList<ClientCard> cards, int min, int max)

            protected bool 伟大魔兽Summon()
        {
            int[] materials = new[] { CardId.幻刃破龙, CardId.建造龙, CardId.幻龙重骑, CardId.起重龙 };
            if (Bot.HasInMonstersZone(materials))
            {
                AI.SelectCard(materials);
                return true;
            }
            return false;
        }
        private bool 幻刃急攻Summon()
        {
            if (Card.Level < 5 && (Card.Race & (int)CardRace.Wyrm) > 0 &&
                Bot.HasInSpellZone(CardId.幻刃急攻) && !IsUpperMonSummon())
            {
                IList<ClientCard> lowerList =
                   Bot.Hand.GetMatchingCards(card => (card.Level < 5 && ((card.Race & (int)CardRace.Wyrm) > 0)));
                if (Card.Id == lowerList.OrderByDescending(card=>card.Attack).FirstOrDefault().Id &&
                    Bot.Hand.GetMatchingCardsCount(card => card.Id == Card.Id) < 2)
                    return false;
            }
            return MonsSummon();
        }

        private bool 幻刃急攻Repos()
        {
            if (Bot.HasInSpellZone(CardId.幻刃急攻)&&
                Bot.Hand.GetMatchingCardsCount(card => card.Level < 7 && (card.Race & (int)CardRace.Wyrm) > 0)>0)
            {
                ClientCard bestMon = Bot.MonsterZone.GetMonsters().OrderByDescending(card => card.Attack).FirstOrDefault();
                if (bestMon == null) return false;
                if(Card.Id == bestMon.Id)
                {
                    return !Card.IsAttack();
                }
            }
            return MonsRepos();
        }
        private bool 幻刃急攻Set()
        {
            if (Card.Level < 5 && (Card.Race & (int)CardRace.Wyrm) > 0 &&
                Bot.HasInSpellZone(CardId.幻刃急攻) && !IsUpperMonSummon())
            {
                IList<ClientCard> lowerList =
                    Bot.Hand.GetMatchingCards(card => (card.Level < 5 && ((card.Race & (int)CardRace.Wyrm) > 0)));
                if (Card.Id == lowerList.OrderByDescending(card => card.Attack).FirstOrDefault().Id &&
                    Bot.Hand.GetMatchingCardsCount(card => card.Id == Card.Id) < 2)
                    return false;
            }
            return MonsSet();
        }
        private bool MonsSet()
        {
            ClientCard EmWorstMon = Util.GetWorstEnemyMonster();
            if (Card.Level < 5)
            {
                if (Card.Id == CardId.镐头龙)
                {
                    return !Discord1Draw1Eff(CardRace.Wyrm, false);
                }

                if (Duel.Turn == 1)
                { if (Bot.HasInHandOrInSpellZone(CardId.幻刃封锁) &&
                    (Bot.MonsterZone.GetMatchingCardsCount(card => card.IsFaceup() && card.Level < 5) <= 0))
                        return false;
                    else
                        return true;
                }
                if (EmWorstMon == null) return false;

                if (Card.Id == CardId.风车鳞虫)
                {
                    return !风车鳞虫Summon();
                }
                if (Bot.HasInSpellZone(CardId.透幻乡险峻))
                {
                    return Card.Attack + 400 < (EmWorstMon.GetDefensePower());
                }
                else 
                return Card.Attack < (EmWorstMon.GetDefensePower());
            }
            else
                return false;
        }

        private bool UpperMonSummon(ClientCard _card)       //检查某怪兽是否能够上级召唤
        {
            if (_card.Level < 5) return true;
            int tributeAllow = Bot.MonsterZone.GetMatchingCardsCount(card => card.Level < 5);
            if (Bot.SpellZone.GetMatchingCardsCount(card => card.HasType(CardType.Field)) + Enemy.SpellZone.GetMatchingCardsCount(card => card.HasType(CardType.Field)) > 0)              
            {
                if (_card.Id == CardId.建造龙)
                tributeAllow += Bot.HasInMonstersZone(CardId.幻刃破龙) ? 1 : 0;
                tributeAllow += Bot.HasInMonstersZone(CardId.幻煌) ? 1 : 0;
                if (_card.Id == CardId.起重龙 || Bot.HasInMonstersZone(CardId.起重龙))
                    tributeAllow += Bot.HasInMonstersZone(CardId.建造龙) ? 1 : 0;
            }
            if ((_card.Id != CardId.幻刃破龙 || _card.Id != CardId.幻煌) && Bot.Hand.GetMatchingCardsCount(card => card.Level > 6) > 2 &&
                Bot.Hand.GetMatchingCardsCount(card => card.Level < 5) + Bot.MonsterZone.GetMatchingCardsCount(card => card.Level < 5) < 2)
                tributeAllow = Bot.GetMonsterCount();
            return tributeAllow >= (_card.Level > 6 ? 2 : 1);
        }

        private bool IsUpperMonSummon()       //检查手卡是否有怪兽可以上级召唤
        {
            foreach(ClientCard c in Bot.Hand.GetMatchingCards(card=>card.Level>4))
            {
                if (UpperMonSummon(c)) 
                    return true;
                else
                {
                    if (Bot.MonsterZone.GetMatchingCardsCount(card => card.Level > 4) <= 1 &&
                        Bot.MonsterZone.GetMatchingCardsCount(card => card.Level < 5) +
                        Bot.Hand.GetMatchingCardsCount(card => card.Level > 5) >= (c.Level > 6 ? 2 : 1))
                        return true;
                    else
                        return false;
                }
            }
            return false;
        }
        private bool MonsSummon()
        {
            if (Card.Id == CardId.镐头龙 )
            {
                return 镐头龙Summon();
            }
            if(Card.Id == CardId.风车鳞虫)
            {
                return 风车鳞虫Summon();
            }
            if(Card.Level>6)
            {
                return UpperMonSummon(Card);
            }
            return RushMonsterSummon();
        }
        private bool 镐头龙Effect()
        {
            if (Bot.HasInMonstersZone(CardId.幻刃破龙) || (Bot.HasInMonstersZone(CardId.幻煌)) && Bot.HasInHand(CardId.建造龙) && Bot.HasInHand(CardId.起重龙))
                return Discord1Draw1Eff(CardRace.Wyrm, true, true);
            return Discord1Draw1Eff(CardRace.Wyrm, true, false);
        }
        /*private bool 荒废蟠龙Effect()
        {
            if(Bot.Hand.GetMatchingCardsCount(card=>card.HasType(CardType.Field)) > 0)
            {
                AI.SelectCard(Bot.GetFieldSpellCard());
                return true;
            }
            return false;
        }*/
        private bool Discord1Draw1Eff(CardRace race, bool isAISelect = true, bool isReplaceUpperMon = false)
        {
            if (Bot.Hand.GetMatchingCardsCount(card => (card.Race & (int)race) > 0) > 0)
            {
                int triCost = Bot.Hand.GetMatchingCardsCount(card => card.Level > 4 && card.Level < 7) +
                    Bot.Hand.GetMatchingCardsCount(card => card.Level > 6) * 2;
                int tributeNum = (isReplaceUpperMon ?
                    Bot.GetMonsterCount() : Bot.MonsterZone.GetMatchingCardsCount(card => card.Level < 5)) +
                    Bot.Hand.GetMatchingCardsCount(card => card.Level < 5);

                if (triCost > tributeNum &&
                    Bot.Hand.GetMatchingCardsCount(card => (card.Race & (int)race) > 0 && card.Level > 4) > 0)
                {
                    if (isAISelect && triCost > 2) AI.SelectCard(rePriList);
                }
                else if (triCost < tributeNum -1 && triCost >0 && Bot.GetSpellCountWithoutField()<3 &&
                     Bot.Hand.GetMatchingCardsCount(card => (card.Race & (int)race) > 0 && card.Level < 5) > 0)
                {
                    if (isAISelect) AI.SelectCard(Bot.Hand.GetFirstMatchingCard(card => (card.Race & (int)race) > 0 && card.Level < 5));
                }
                else return false;
                return true;
            }
            return false;
        }
        bool ATKdown = false;
        public override void OnNewTurn()
        {
            ATKdown = false;
            base.OnNewTurn();
        }
        public override BattlePhaseAction OnSelectAttackTarget(ClientCard attacker, IList<ClientCard> defenders)
        {
            if (ATKdown && Bot.GetMonsterCount() <= Enemy.GetMonsterCount())
            {
                for (int i = 0; i < defenders.Count; i++)
                {
                    ClientCard defender = defenders[i];
                    attacker.RealPower = attacker.Attack;
                    defender.RealPower = defender.GetDefensePower();
                    if (!OnPreBattleBetween(attacker, defender))
                        continue;
                    if (attacker.RealPower > defender.RealPower && defender.Level > 6 && defender.IsAttack() && defender.Attack < 1800)
                    {
                        return AI.Attack(attacker, defender);
                    }
                }
            }
            return base.OnSelectAttackTarget(attacker, defenders);
        }
        private bool 建造龙Effect()
        {
            if(Bot.Hand.GetMatchingCardsCount(card=>card.Level>6)>0)
            {
                foreach(ClientCard card in Bot.Hand.GetMatchingCards(card=>card.Level>6))
                {
                    if (UpperMonSummon(card)) return false;
                }
            }
            return 建造龙Effect(true);
        
        }
        private bool 建造龙Effect( bool isAISelect = true,bool isDefense = false)
        {
            if (Bot.SpellZone.GetMatchingCardsCount(card => card.HasType(CardType.Field)) > 0 ||
                    Enemy.SpellZone.GetMatchingCardsCount(card => card.HasType(CardType.Field)) > 0)
            {
                if (isAISelect)
                {
                    IList<ClientCard> matchList = Bot.Graveyard.GetMatchingCards(card => 
                    (card.Race & (int)CardRace.Wyrm) > 0 && card.HasType(CardType.Normal));
                    ClientCard mon = matchList.OrderByDescending(card=>card.Level).FirstOrDefault();
                    AI.SelectCard(mon);
                    ClientCard worstEmon = Util.GetWorstEnemyMonster();
                    if (worstEmon == null) AI.SelectPosition(isDefense ? CardPosition.FaceDownDefence : CardPosition.FaceUpAttack);
                    else if ((Bot.HasInSpellZone(CardId.透幻乡险峻) ? mon.Attack + 400 : mon.Attack) >= worstEmon.GetDefensePower())
                        AI.SelectPosition(isDefense ? CardPosition.FaceDownDefence : CardPosition.FaceUpAttack);
                    else
                        AI.SelectPosition(CardPosition.FaceUpDefence);
                }
                return true;
            }
            return false;
        }

        private bool 搅拌龙Effect()
        {
            return 搅拌龙Effect(true);
        }
        private bool 搅拌龙Effect(bool isAISelect = true)
        {
            if (Bot.SpellZone.GetMatchingCardsCount(card => card.HasType(CardType.Field)) > 0 ||
                    Enemy.SpellZone.GetMatchingCardsCount(card => card.HasType(CardType.Field)) > 0)
            {
                if(isAISelect)
                AI.SelectYesNo(true);
                return true;
            }
            return false;
        }
        private bool 丰沃蟠龙Effect()
        {
            return 丰沃蟠龙Effect(true);
            
        }
        private bool 丰沃蟠龙Effect(bool isAISelect = true)
        {
            if (Bot.Graveyard.GetMatchingCardsCount(card => card.HasType(CardType.Field)) > 0)
            {
                int[] fieldList = new int[] { CardId.透幻乡险峻 };
                if (isAISelect) AI.SelectCard(fieldList);
                return true;
            }
            return false;
        }
        private bool 荒废蟠龙Effect()
        { 
            if (Enemy.LifePoints <= 600) 
                AI.SelectCard(CardId.透幻乡险峻); 
            else if (Bot.HasInHand(CardId.透幻乡险峻))
            {
                AI.SelectCard(CardId.透幻乡险峻);  return true;
            }
            return false;
        }
        private bool 奥义Effect()
        {
            var cards = Bot.Graveyard.GetMatchingCards(card => (card.Race & (int)CardRace.Wyrm) > 0);
            int totalLevel = cards.Sum(card => card.Level);
            if (totalLevel >= 16)
            {
                var sortedCards = cards.OrderBy(card => card.Level).ToList();
                var ttl = 0;
                var targets = new List<ClientCard>(); 
                for (int i = 0; i < sortedCards.Count() && ttl < 16; i++)
                {
                    targets.Add(sortedCards[i]); 
                    ttl += sortedCards[i].Level;
                }
                if (ttl >= 16)
                {
                    if (Util.GetTotalAttackingMonsterAttack(1) >= 1900 && (Enemy.GetMonsters().GetImmuneTrapMonster() == null)
                       || Util.GetTotalAttackingMonsterAttack(1) - Util.GetTotalAttackingMonsterAttack(0) > Bot.LifePoints && !Bot.HasDefendingMonster())
                    {
                        AI.SelectCard(targets); 
                        return true;
                    }
                }
            }

            return false;
        }

        private bool 贯通Effect()
        {
            return 贯通Effect(true);
        }
        private bool 贯通Effect(bool isAISelect = true)   
        {
            if (Enemy.HasDefendingMonster() && Bot.HasAttackingMonster())
            {
                if(isAISelect)
                {
                    int monBotNum = Bot.MonsterZone.GetMatchingCardsCount(card => card.Level > 6 && card.IsAttack()),
                        monEnNum = Enemy.MonsterZone.GetMatchingCardsCount(card => card.Level > 6 && card.IsAttack());
                    ClientCard EnMon = Enemy.MonsterZone.GetMatchingCards(card => card.IsDefense() && card.IsFaceup())
                        .OrderByDescending(card => card.Defense).FirstOrDefault(),
                        botMon = Bot.MonsterZone.GetMatchingCards(card => card.IsAttack() && card.IsFaceup())
                        .OrderByDescending(card => card.Attack).FirstOrDefault();

                    if (EnMon == null) 
                    {
                        AI.SelectCard(Bot.MonsterZone.GetMatchingCards(card=>card.IsAttack())
                            .OrderByDescending(card=>card.Attack).FirstOrDefault());
                        return true; 
                    }
                    if (monBotNum>0&&(monBotNum>monEnNum)&&EnMon.Defense<botMon.Attack)
                    {
                        ClientCard objMon = Bot.MonsterZone.GetMatchingCards(card => card.IsAttack() && card.Level > 6 && card.Attack > EnMon.Defense)
                            .OrderBy(card => card.Attack).FirstOrDefault();

                        AI.SelectCard(objMon);
                        return true;
                    }
                }
            }
            return false;
        }

        private bool 幻刃复归Effect()
        {
             return 幻刃复归Effect(true, true);
        }
        private bool 幻刃复归Effect(bool isAttack = true, bool isAISelect = true)
        {
            if (Bot.Graveyard.GetMatchingCardsCount(card => card.Level > 6 && ((card.Race & (int)CardRace.Wyrm) > 0)) > 0)
            {
                if (isAISelect)
                {
                    int[] localList = new int[] { CardId.建造龙, CardId.幻刃破龙, CardId.幻煌, CardId.起重龙 };
                    if (Bot.HasInMonstersZone(CardId.建造龙) && 建造龙Effect(true, false))
                    {
                        if (Bot.HasInGraveyard(localList[0]) || Bot.HasInGraveyard(localList[1]))
                            AI.SelectCard(localList);
                        else
                            return false;
                    }
                    else AI.SelectCard(priList);

                    IList<ClientCard> atkingMon =
                    Enemy.MonsterZone.GetMatchingCards(card => card.Attacked == false && card.IsAttack());
                    if (atkingMon.GetHighestAttackMonster() == null)
                         AI.SelectPosition(isAttack ? CardPosition.FaceUpAttack : CardPosition.FaceDownDefence);
                    else
                    {
                        bool defaultAtk = (atkingMon.OrderByDescending(card=>card.Attack).FirstOrDefault().Attack <=
                            (2400 + (Bot.HasInSpellZone(CardId.透幻乡险峻) ? 400 : 0)));
                        AI.SelectPosition(defaultAtk ? CardPosition.FaceUpAttack : CardPosition.FaceDownDefence);
                    }

                }
                return true;
            }
            return false;
        }
        private bool 幻刃急攻Effect()
        {
            return 幻刃急攻Effect(true);
        }
        private bool 幻刃急攻Effect(bool isAISelect = true)
        {
            if (Bot.Hand.GetMatchingCardsCount(card => card.Level < 7 && (card.Race & (int)CardRace.Wyrm)>0) > 0)
            {
                if (isAISelect)
                {
                    IList<ClientCard> lower7Mons = Bot.Hand.GetMatchingCards(card => card.Level < 7&&(card.Race & (int)CardRace.Wyrm)>0);
                    ClientCard mon = lower7Mons.OrderByDescending(card => card.Attack).FirstOrDefault();
                    if (Bot.BattlingMonster.GetDefensePower() + mon.Attack >= Enemy.BattlingMonster.Attack)
                        AI.SelectCard(mon);
                    else return false;
                }
                return true;
            }
            return false;
        }

        private bool 幻刃封锁Effect()
        {
            return 幻刃封锁Effect(true);
        }
        private bool 幻刃封锁Effect(bool isAISelect = true)
        {
            if (Bot.MonsterZone.GetMatchingCardsCount(card => card.Level > 6 && ((card.Race & (int)CardRace.Wyrm) > 0)) > 0)
            {
                
                if (Bot.HasInMonstersZone(CardId.建造龙) && 
                    (Bot.SpellZone.GetMatchingCardsCount(card=>card.HasType(CardType.Field))>0|| Enemy.SpellZone.GetMatchingCardsCount(card => card.HasType(CardType.Field)) > 0) &&
                    Bot.MonsterZone.GetMatchingCardsCount(card => card.IsFaceup() && (card.HasType(CardType.Normal))) > 0)
                {
                    IList<ClientCard> NormalList = Bot.MonsterZone.GetMatchingCards(card => (card.HasType(CardType.Normal)));
                    if (isAISelect)
                    {
                        AI.SelectCard(NormalList.GetLowestLevelMonster());
                    }
                }
                else if (Bot.MonsterZone.GetMatchingCardsCount(card => card.IsFaceup() && (card.Level < 5) && (card.Race & (int)CardRace.Wyrm) > 0) > 0)
                {
                    if (isAISelect)
                    {
                        AI.SelectCard(Bot.MonsterZone.GetMatchingCards(card =>
                        card.IsFaceup() && (card.Level < 5) && (card.Race & (int)CardRace.Wyrm) > 0)
                            .OrderBy(card => card.Attack).FirstOrDefault());
                    }
                }
                else
                {
                    ClientCard mon = Util.GetBestBotMonster();
                    int dangerEnMons = Enemy.MonsterZone.GetMatchingCardsCount(card => card.Attack > mon.GetDefensePower());
                    if ((Bot.HasInSpellZone(CardId.幻刃急攻) && 幻刃急攻Effect(false)) ||
                        (Bot.HasInSpellZone(CardId.幻刃复归)&&幻刃复归Effect(false,false)))
                            dangerEnMons--;
                    if (dangerEnMons > 0)
                    {
                        if (isAISelect)
                            AI.SelectCard(rePriList);
                    }
                    else return false;
                }
                
            }
            else
            {
                ClientCard c = Bot.MonsterZone.GetMatchingCards(card =>
                 card.IsFaceup() && (card.Race & (int)CardRace.Wyrm) > 0)
                    .OrderBy(card => card.GetDefensePower()).FirstOrDefault();
                AI.SelectCard(c);
            }
            if (isAISelect)
                AI.SelectNextCard(Duel.LastSummonedCards);
            return true;
        }

        //泛用墓地回收函数,内置AISelectCard, slcNum:回收数量,idList优先回收ID表,keepNums保留数量表,执行成功返回true
        private bool GraveReturnSelect(int slcNum ,int[] idList,int[] keepNums)   
        {
            int hadSlc = 0;
            if (idList.FirstOrDefault() == 0 || idList.Length > keepNums.Length) return false;
            for(int i=0;i<idList.Length;i++)  //优先回收
            {
                int keepNum = keepNums.ElementAtOrDefault(i);       //keepNum不存在时为0
                int hasNum = Bot.Graveyard.GetCardCount(idList[i]);
                for (int j = 0; j < hasNum - keepNum; j++)          //返回优先
                {
                    AI.SelectCard(idList[j]);
                    if (++hadSlc >= slcNum) return true;
                }
            }
            foreach (ClientCard card in Bot.Graveyard)              //返回剩余
            {
                if (!idList.Contains(card.Id))
                {
                    AI.SelectCard(card);
                    if (++hadSlc >= slcNum) return true;
                }
            }
            foreach (ClientCard card in Bot.Graveyard)              //能返则返
            {
                AI.SelectCard(card);
                if (++hadSlc >= slcNum) return true;
            }
            return hadSlc >= slcNum;
        }
        private bool 阴阳封阵Effect()
        {
            if (Enemy.HasAttackingMonster())
            {

                ClientCard c = Enemy.MonsterZone.GetMatchingCards(card=>card.IsAttack()&&card.Level>6).GetHighestAttackMonster();
                if (c == null || !GraveReturnSelect(4, priList, new int[] { 0, 0, 1 })) return false;
                AI.SelectNextCard(c);
                ATKdown = true;
                return true;
            }
            return false;
        }
    }
}
