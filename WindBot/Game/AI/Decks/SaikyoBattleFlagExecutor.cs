using YGOSharp.OCGWrapper.Enums;
using System.Collections.Generic;
using System.Linq;
using WindBot;
using WindBot.Game;
using WindBot.Game.AI;
using System;

namespace WindBot.Game.AI.Decks
{
    [Deck("SaikyoBattleFlag", "AI_SaikyoBattleFlag")]
    public class SaikyoBattleFlagExecutor : DefaultExecutor
    {
        public class CardId
        {
            public const int 最强战旗 = 120181001;
            public const int 钢机神 = 120155015;
            public const int 连击龙 = 120110001;
            public const int 恶德 = 120183024;
            public const int 旗兽 = 120193002;
            public const int 社员 = 120183002;

            public const int 成金恐龙王 = 120151010;
            public const int 捕鸟蛛 = 120224010;       
            public const int 突击坦克 = 120183031;
            public const int 工匠无人机 = 120183030;
            public const int 双角兽 = 120181011;
            public const int 瞄准鹰 = 120193003;
            public const int 死苏 = 120194004;            
            public const int 世纪末兽机界 = 120130039;
            public const int 铁之重击 = 120183054;
            public const int 对死者的供奉 = 120151023;
            public const int 七抽 = 120199052;

            public const int 兽之拳 = 120155060;
            public const int 减俸 = 120183062;
            public const int 左迁 = 120183063;
            public const int 冻结 = 120183064;
            public const int 暗黑释放 = 120105013;
        }

        public SaikyoBattleFlagExecutor(GameAI ai, Duel duel)
            : base(ai, duel)
        {
            AddExecutor(ExecutorType.Activate, _CardId.黑洞, 黑洞Effect);
            AddExecutor(ExecutorType.SpSummon);
            AddExecutor(ExecutorType.Activate, CardId.铁之重击);           
            AddExecutor(ExecutorType.Activate, CardId.成金恐龙王);
            AddExecutor(ExecutorType.Activate, CardId.捕鸟蛛);
            AddExecutor(ExecutorType.Activate, CardId.恶德, 恶德Effect);            
            AddExecutor(ExecutorType.Activate, CardId.最强战旗, 最强战旗Eff);           
            AddExecutor(ExecutorType.Activate, CardId.突击坦克, 突击坦克Effect); 
            AddExecutor(ExecutorType.Activate, CardId.瞄准鹰, 瞄准鹰Effect);
            AddExecutor(ExecutorType.Activate, CardId.双角兽, 双角兽Effect);
            AddExecutor(ExecutorType.Activate, _CardId.手枪龙, 手枪龙Effect);
            AddExecutor(ExecutorType.Repos, CardId.双角兽, 双角兽summon);
            AddExecutor(ExecutorType.Repos, CardId.瞄准鹰, 瞄准鹰summon);
            AddExecutor(ExecutorType.Repos, CardId.最强战旗, DefaultMonsterRepos);
            AddExecutor(ExecutorType.Repos, CardId.钢机神, DefaultMonsterRepos);
            AddExecutor(ExecutorType.Repos, _CardId.手枪龙, DefaultMonsterRepos);
            AddExecutor(ExecutorType.Repos, CardId.恶德, DefaultMonsterRepos);
            AddExecutor(ExecutorType.Summon, CardId.最强战旗, DefaultDoubleSummon);
            AddExecutor(ExecutorType.Summon, CardId.钢机神, DefaultDoubleSummon);
            AddExecutor(ExecutorType.Summon, CardId.恶德, DefaultDoubleSummon);

            AddExecutor(ExecutorType.Repos, CardId.成金恐龙王, 瞄准鹰成金恐龙王summon);
            AddExecutor(ExecutorType.Repos, CardId.突击坦克, 瞄准鹰坦克summon);
            AddExecutor(ExecutorType.Repos, CardId.双角兽, 瞄准鹰双角兽summon);
            AddExecutor(ExecutorType.Summon, CardId.双角兽, 瞄准鹰双角兽summon);
            AddExecutor(ExecutorType.Summon, CardId.成金恐龙王, 瞄准鹰成金恐龙王summon);
            AddExecutor(ExecutorType.Summon, CardId.突击坦克, 瞄准鹰坦克summon);
            AddExecutor(ExecutorType.Summon, CardId.瞄准鹰, 瞄准鹰summon);
            AddExecutor(ExecutorType.Summon, CardId.瞄准鹰, 瞄准鹰summon2);
            AddExecutor(ExecutorType.Summon, CardId.瞄准鹰, 瞄准鹰summon3);

            AddExecutor(ExecutorType.Summon, _CardId.手枪龙, RushMonsterSummon);
            AddExecutor(ExecutorType.Summon, CardId.最强战旗, RushMonsterSummon);
            AddExecutor(ExecutorType.Summon, CardId.钢机神, RushMonsterSummon);
            AddExecutor(ExecutorType.Summon, CardId.恶德, RushMonsterSummon);

            AddExecutor(ExecutorType.Summon, CardId.双角兽, 双角兽summon);
            AddExecutor(ExecutorType.Summon, CardId.捕鸟蛛, 捕鸟蛛summon);
            AddExecutor(ExecutorType.Summon, CardId.成金恐龙王, 成金恐龙王Summon);

            AddExecutor(ExecutorType.Summon, CardId.最强战旗, DefaultOverSummon);
            AddExecutor(ExecutorType.Summon, CardId.钢机神, DefaultOverSummon);
            AddExecutor(ExecutorType.Activate, CardId.死苏, 死苏effect);
            AddExecutor(ExecutorType.Activate, CardId.最强战旗, 最强战旗Effect);
            AddExecutor(ExecutorType.Activate, CardId.对死者的供奉, 死供Effect);
            AddExecutor(ExecutorType.Activate, CardId.钢机神, 钢机神1Effect);
            AddExecutor(ExecutorType.Activate, CardId.钢机神, 钢机神3Effect);
            AddExecutor(ExecutorType.MonsterSet, CardId.成金恐龙王);            
            AddExecutor(ExecutorType.Activate, CardId.工匠无人机,无人机Effect);
                
            AddExecutor(ExecutorType.MonsterSet, CardId.工匠无人机);
            AddExecutor(ExecutorType.MonsterSet, CardId.突击坦克);
            AddExecutor(ExecutorType.MonsterSet, CardId.捕鸟蛛);
            AddExecutor(ExecutorType.Summon, CardId.工匠无人机, 无人机summon);
            AddExecutor(ExecutorType.Summon, CardId.突击坦克, 突击坦克summon);
            AddExecutor(ExecutorType.MonsterSet, CardId.旗兽);
            AddExecutor(ExecutorType.MonsterSet, CardId.社员);
            AddExecutor(ExecutorType.MonsterSet, CardId.瞄准鹰);
            AddExecutor(ExecutorType.MonsterSet, CardId.双角兽);
            AddExecutor(ExecutorType.Activate, CardId.世纪末兽机界, DefaultField);
            AddExecutor(ExecutorType.Activate, CardId.兽之拳, 兽之拳Effect);
            AddExecutor(ExecutorType.Activate, CardId.左迁, 左迁1Effect); 
            AddExecutor(ExecutorType.Activate, CardId.减俸);
            AddExecutor(ExecutorType.Activate, CardId.左迁,左迁2Effect);
            AddExecutor(ExecutorType.Activate, CardId.兽之拳);
            AddExecutor(ExecutorType.Activate, CardId.冻结,冻结Effect);
            AddExecutor(ExecutorType.Activate, _CardId.魔法筒, 魔法筒Effect);
            AddExecutor(ExecutorType.Activate, _CardId.炸甲, 炸甲Effect);
            AddExecutor(ExecutorType.Activate, _CardId.落穴, 落穴Effect);
            AddExecutor(ExecutorType.Activate, _CardId.激流葬, 激流葬Effect);
            AddExecutor(ExecutorType.Activate, _CardId.圣防, 圣防Effect);

            AddExecutor(ExecutorType.Activate, DefaultDontChainMyself);
            AddExecutor(ExecutorType.SummonOrSet, RushMonsterSummon);
            AddExecutor(ExecutorType.Activate, CardId.七抽, 七抽Effect);
            AddExecutor(ExecutorType.Repos, DefaultMonsterRepos);           
            AddExecutor(ExecutorType.SpellSet, DefaultSpellSet);
            AddExecutor(ExecutorType.Activate, CardId.世纪末兽机界, DefaultField2);
            AddExecutor(ExecutorType.Repos, CardId.钢机神, 反转Repos);
            AddExecutor(ExecutorType.Repos, CardId.最强战旗, 反转Repos);
            AddExecutor(ExecutorType.Repos, CardId.恶德, 反转Repos);
            AddExecutor(ExecutorType.Repos, CardId.最强战旗, 圣防Repos);
            AddExecutor(ExecutorType.Repos, CardId.钢机神, 圣防Repos);
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

        bool DoubleTribute = false;
        public override void OnNewTurn()
        {
            DoubleTribute = false;
            base.OnNewTurn();
        }
        private bool DefaultDoubleSummon()
        {
         int tribute_count = 0;
         IList<ClientCard> m_list = new List<ClientCard>(); List<ClientCard> _sort_list = new List<ClientCard>(Bot.GetMonsters());
            if (!DoubleTribute)
                return false;
            else
            {               
            foreach (ClientCard monster in _sort_list)
            {
                if (monster.IsCode(CardId.双角兽))
                    { tribute_count += 2;
                    m_list.Add(monster);
                    }
                    if (tribute_count == 2)  break;
             }
                AI.SelectMaterials(m_list);           
            DoubleTribute = false;
                return true;
            }                      
        }
        private bool DefaultOverSummon()
        {
            if (Bot.Hand.Count >= 6 && Bot.HasInMonstersZone(CardId.恶德)) return true;
            else return false;
        }
        private bool 左迁1Effect()
        {
            
        if (Enemy.BattlingMonster.Attack > 2000)
            {
            AI.SelectCard(Enemy.BattlingMonster);
            return true;
            }
            return false;
        }
        private bool 左迁2Effect()
        {
            ClientCard target = Util.GetBestEnemyMonster(true, true);
            if (target != null)
                AI.SelectCard(target);
            return true;        
        }
        private bool 冻结Effect()
        {
            foreach (ClientCard monster in Bot.GetMonsters())
            {
                if (monster.IsFacedown())
                {
                    AI.SelectCard(monster);
                    return true;
                }
                else if (monster.IsDefense())
                {
                    AI.SelectCard(monster);
                    return true;
                }
            }
            AI.SelectCard(Bot.MonsterZone.GetLowestAttackMonster());
            return true;
        }
        private bool 钢机神3Effect()
        {
            if (Duel.Turn == 1)
            {
                return false;
            }
            else 
                AI.SelectCard(_CardId.手枪龙, CardId.最强战旗, CardId.钢机神, CardId.恶德, CardId.捕鸟蛛, CardId.突击坦克, CardId.工匠无人机,  CardId.社员, CardId.双角兽, CardId.旗兽, CardId.瞄准鹰);
            return true;       
        }
        private bool 恶德Effect()
        {
            foreach (ClientCard m in Bot.Hand)
                AI.SelectCard(m);
            AI.SelectNextCard(CardId.左迁, CardId.减俸);
            AI.SelectNextCard(CardId.左迁, CardId.减俸);
            return true;
            
        }
        private bool 兽之拳Effect()
        {
            if (Enemy.BattlingMonster.Race == (int)CardRace.Dragon)
                return true;
            if (Enemy.BattlingMonster.Race == (int)CardRace.Fairy)
                return true;
            if (Enemy.BattlingMonster.Race == (int)CardRace.SpellCaster)
                return true;
            return false;
        }

        private bool 死苏effect()
        {
            List<ClientCard> cards = new List<ClientCard>(Bot.Graveyard.GetMatchingCards(card => card.IsCanRevive()));
            cards.Sort(CardContainer.CompareCardAttack);
            ClientCard selectedCard = null;
            for (int i = cards.Count - 1; i >= 0; --i)
            {
                ClientCard card = cards[i];
                if (card.Attack < 2000)
                    break;
                if (card.IsMonster())
                {
                    selectedCard = card;
                    break;
                }
            }
            cards = new List<ClientCard>(Enemy.Graveyard.GetMatchingCards(card => card.IsCanRevive()));
            cards.Sort(CardContainer.CompareCardAttack);
            for (int i = cards.Count - 1; i >= 0; --i)
            {
                ClientCard card = cards[i];
                if (card.Attack < 2000)
                    break;
                if (card.IsMonster() && card.HasType(CardType.Normal) && (selectedCard == null || card.Attack > selectedCard.Attack))
                {
                    selectedCard = card;
                    break;
                }
            }
            if (selectedCard != null)
            {
                AI.SelectCard(selectedCard);
                return true;
            }
            return false;
        }
        private bool 成金恐龙王Summon()
        {
            if (Enemy.GetHandCount() < 1) return true;
            return false;
        }
        private bool 突击坦克Effect()
        {           
            foreach (ClientCard mon in Enemy.GetMonsters())
                if (mon.Level >= 9)
                    AI.SelectCard(mon);
                else if (mon.Level >= 8)
                    AI.SelectCard(mon);
                else if (mon.Level >= 6)
                    AI.SelectCard(mon);
                else if (mon.Level >= 4)
                    AI.SelectCard(mon);
                else if (mon.Level >= 2)
                    AI.SelectCard(mon);
                else if (mon.Level >= 1)
                    AI.SelectCard(mon);
            return true;
        }
        private bool 突击坦克summon()
        {
            if (Bot.MonsterZone.GetMatchingCardsCount(card => (card.Race & (int)CardRace.Machine) > 0) == 2 || Util.GetOneEnemyBetterThanMyBest(true, true) == null) return true;
            return false;
        }
        private bool 捕鸟蛛summon()
        {
            if (Bot.HasInGraveyard(new[] { CardId.旗兽, CardId.社员 }) && Enemy.GetSpellCount()  >= 2)
                return true;
            return false;
        }
        private bool 双角兽summon()
        {
            if (Bot.Graveyard.GetMatchingCardsCount(card => (card.Race & (int)CardRace.Machine) > 0) >= 3 && Bot.HasInHand(new[] { CardId.最强战旗, CardId.钢机神, CardId.恶德 }) && !Bot.HasInMonstersZone(new[] { CardId.最强战旗, CardId.钢机神 })) 
                return true;
            return false;
        }
        private bool 双角兽Effect()
        {            
            if (Bot.HasInHand(new[] { CardId.最强战旗, CardId.钢机神, CardId.恶德 }) && !Bot.HasInMonstersZone(new[] { CardId.最强战旗, CardId.钢机神 }))
            {
                AI.SelectCard(CardId.瞄准鹰, CardId.捕鸟蛛, CardId.双角兽, CardId.工匠无人机, CardId.突击坦克, CardId.社员, CardId.旗兽, CardId.恶德, CardId.钢机神, CardId.最强战旗);
                DoubleTribute = true;
                return true;
            }
            return false;
        }
        private bool 瞄准鹰双角兽summon()
        {
            if (Bot.HasInMonstersZone(CardId.瞄准鹰) && Bot.HasInHand(new[] { CardId.最强战旗, CardId.钢机神, CardId.恶德 }) && Enemy.MonsterZone.GetMatchingCardsCount(card => card.Level >= 7) > 0)
                return true;
            return false;
        }
        private bool 瞄准鹰坦克summon()
        {
            if (Bot.HasInMonstersZone(CardId.瞄准鹰) && Bot.HasInHand(new[] { _CardId.手枪龙, CardId.最强战旗, CardId.钢机神, CardId.恶德 }) && Enemy.MonsterZone.GetMatchingCardsCount(card => card.Level >= 7) > 0)
                return true;
            return false;
        }
        private bool 瞄准鹰成金恐龙王summon()
        {
            if (Bot.HasInMonstersZone(CardId.瞄准鹰) && Bot.HasInHand(new[] { _CardId.手枪龙, CardId.最强战旗, CardId.钢机神, CardId.恶德 }) && Enemy.MonsterZone.GetMatchingCardsCount(card => card.Level >= 7) > 0)
                return true;
            return false;
        }
        private bool 瞄准鹰summon()
        {           
          foreach (ClientCard m in Bot.GetMonsters())
               if (m.HasAttribute(CardAttribute.Light) && m.Level <= 4 && m.IsFaceup())
              if (Bot.HasInHand(new[] { _CardId.手枪龙, CardId.最强战旗, CardId.钢机神, CardId.恶德 }))
             if (Enemy.MonsterZone.GetMatchingCardsCount(card => card.Level >= 7) > 0)                       
              return true;           
            return false;
        }
        private bool 瞄准鹰summon2()
        {
            if (Bot.Hand.GetCardCount(CardId.瞄准鹰) >= 2 )
                if (Bot.HasInHand(new[] { _CardId.手枪龙, CardId.最强战旗, CardId.钢机神, CardId.恶德 }))
                  if (Enemy.MonsterZone.GetMatchingCardsCount(card => card.Level >= 7) > 0)
                             return true;           
            return false;
        }
        private bool 瞄准鹰summon3()
        {
                     if (Bot.HasInHand(CardId.双角兽) || Bot.HasInHand(CardId.突击坦克) || Bot.HasInHand(CardId.成金恐龙王))
                if (Bot.HasInHand(new[] { _CardId.手枪龙, CardId.最强战旗, CardId.钢机神, CardId.恶德 }))
                            if (Enemy.MonsterZone.GetMatchingCardsCount(card => card.Level >= 7) > 0)
                                return true;
            return false;
        }
        private bool 瞄准鹰Effect()
        {
           
            if (Bot.MonsterZone.GetMatchingCardsCount(card => card.Level <= 4 && card.IsFaceup()) >= 2 && Bot.HasInHand(new[] { _CardId.手枪龙, CardId.最强战旗, CardId.钢机神, CardId.恶德, CardId.旗兽, CardId.社员 }))
            {
                AI.SelectCard(CardId.瞄准鹰,CardId.突击坦克, CardId.成金恐龙王, CardId.社员, CardId.旗兽);
            AI.SelectNextCard(CardId.瞄准鹰, CardId.突击坦克, CardId.成金恐龙王, CardId.社员, CardId.旗兽);
            AI.SelectThirdCard(_CardId.手枪龙, CardId.最强战旗, CardId.钢机神, CardId.恶德, CardId.旗兽, CardId.社员);
            return true;
            }
            return false;
        }
        private bool 七抽Effect()
        {
            if (Bot.HasInHand(CardId.恶德))
            {
                AI.SelectCard(CardId.恶德); AI.SelectYesNo(true);
                return true;
            }
            else if (Bot.HasInHand(CardId.钢机神))
            {
                AI.SelectCard(CardId.钢机神); AI.SelectYesNo(true);
                return true;
            }
            else if (Bot.HasInHand(CardId.最强战旗))
            {
                AI.SelectCard(CardId.最强战旗); AI.SelectYesNo(true);
                return true;
            }
            else if (Bot.HasInHand(_CardId.手枪龙))
            {
                AI.SelectCard(_CardId.手枪龙); AI.SelectYesNo(true);
                return true;
            }          
            else
            {
                return false;
            }
        }
        private bool 无人机Effect()
        {

            if ((Bot.MonsterZone.GetMatchingCardsCount(card => card.Level < 5)+ Bot.Hand.GetMatchingCardsCount(card => card.Level < 5)) <= 2 )
            {
                if (Bot.Hand.GetMatchingCardsCount(card => card.Level > 5) > 2)
                    AI.SelectCard(CardId.最强战旗, CardId.钢机神, CardId.恶德, _CardId.手枪龙);
               else if (Bot.Hand.GetMatchingCardsCount(card => card.Level > 5) == 1 && (Bot.MonsterZone.GetMatchingCardsCount(card => card.Level < 5) + Bot.Hand.GetMatchingCardsCount(card => card.Level < 5)) == 1)
                {
                    AI.SelectCard(CardId.最强战旗, CardId.钢机神, _CardId.手枪龙, CardId.恶德);
                return true;
                }
                return false;
            }
            else
            AI.SelectCard(CardId.瞄准鹰, CardId.突击坦克, CardId.工匠无人机, CardId.社员, CardId.旗兽);        
                return true;
        }
        private bool 无人机summon()
        {
            if (Bot.Hand.GetMatchingCardsCount(card => (card.Race & (int)CardRace.Machine) > 0) >=1 || Util.GetOneEnemyBetterThanMyBest(true, true) == null)
                return true;
            return false;
        }
        
        private bool DefaultField2()
        {
            if (Bot.GetHandCount() >= 2)
            {
                return true;
            }
            return false;
        }

    }
}








