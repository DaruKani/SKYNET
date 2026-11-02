using YGOSharp.OCGWrapper.Enums;
using System.Collections.Generic;
using System.Linq;
using WindBot;
using WindBot.Game;
using WindBot.Game.AI;
using System;

namespace WindBot.Game.AI.Decks
{
    [Deck("TheLegend", "AI_TheLegend")]
    public class TheLegendExecutor : DefaultExecutor
    {
        public class CardId
        {
            public const int 青眼白龙 = 120120000;
            public const int 破坏之剑士 = 120170000;
            public const int 连击龙 = 120110001;
            public const int 人造人 = 120155000;
            public const int 千年盾 = 120194001; 
            public const int 血腥魔兽人 = 120194002;
            public const int 圣精灵 = 120194003;
            public const int 小手枪龙 = 120223101;
            public const int 狼人 = 120214001;
            public const int 海龙骑士 = 120199032;
            public const int 成金恐龙王 = 120151010;       
            public const int 机器栗子 = 120120017;
            public const int 斗牛士 = 120170035;
            public const int 凤凰龙 = 120110009;
            public const int 死苏 = 120194004;            
            public const int 成金哥布林 = 120151018;
            public const int 强欲之壶 = 120181003;
            public const int 贪欲之壶 = 120217100;
            public const int 工厂 = 120214003;
            public const int 古之规则 = 120214010;
            public const int 对死者的供奉 = 120151023;
            public const int 落穴 = 120150019;
            public const int 圣防 = 120198003;
            public const int 炸甲 = 120194005;
            public const int 暗黑释放 = 120105013;
        }

        public TheLegendExecutor(GameAI ai, Duel duel)
            : base(ai, duel)
        {
            AddExecutor(ExecutorType.Activate, _CardId.HMD, HMDEffect);
            AddExecutor(ExecutorType.Activate, _CardId.手枪龙, 手枪龙Effect);
            AddExecutor(ExecutorType.Activate, _CardId.黑洞, 黑洞Effect);
            AddExecutor(ExecutorType.Activate, _CardId.疾风弹, 疾风弹Effect);
            AddExecutor(ExecutorType.Activate, CardId.小手枪龙, 小手枪龙Effect);            
            AddExecutor(ExecutorType.Activate, CardId.海龙骑士,new海龙骑士Effect);
            //AddExecutor(ExecutorType.Activate, CardId.小手枪龙, DefaultMysticalSpaceTyphoon);
            AddExecutor(ExecutorType.Activate, CardId.强欲之壶);
            AddExecutor(ExecutorType.Activate, CardId.成金哥布林);
            AddExecutor(ExecutorType.Activate, CardId.贪欲之壶);
            AddExecutor(ExecutorType.Activate, CardId.成金恐龙王, 成金恐龙王Effect);
            AddExecutor(ExecutorType.Activate, CardId.机器栗子);
            AddExecutor(ExecutorType.Repos, CardId.机器栗子, 栗子summon2);
            AddExecutor(ExecutorType.Activate, CardId.连击龙);
            AddExecutor(ExecutorType.Activate, CardId.斗牛士, 斗牛士Effect);
            AddExecutor(ExecutorType.SpSummon);
            AddExecutor(ExecutorType.Repos, CardId.斗牛士, DefaultMonsterRepos);
            AddExecutor(ExecutorType.Repos, CardId.小手枪龙, DefaultMonsterRepos);
            AddExecutor(ExecutorType.Repos, CardId.狼人, DefaultMonsterRepos);
            AddExecutor(ExecutorType.Repos, CardId.海龙骑士, DefaultMonsterRepos);
            AddExecutor(ExecutorType.Summon, CardId.青眼白龙, RushMonsterSummon);
            AddExecutor(ExecutorType.Summon, _CardId.手枪龙, RushMonsterSummon);
            AddExecutor(ExecutorType.Summon, _CardId.黑魔术师, RushMonsterSummon);
            AddExecutor(ExecutorType.Summon, CardId.破坏之剑士, RushMonsterSummon);
            AddExecutor(ExecutorType.SummonOrSet, CardId.连击龙, RushMonsterSummon);
            AddExecutor(ExecutorType.SummonOrSet, CardId.人造人, RushMonsterSummon);
            AddExecutor(ExecutorType.Summon, CardId.机器栗子,栗子summon);            
            AddExecutor(ExecutorType.Activate, CardId.古之规则, 古之规则Effect);
            AddExecutor(ExecutorType.Activate, CardId.死苏, 死苏Effect);
            AddExecutor(ExecutorType.Activate, CardId.工厂, 工厂Effect);
            AddExecutor(ExecutorType.MonsterSet, CardId.斗牛士, Monsterset);
            AddExecutor(ExecutorType.Summon, CardId.海龙骑士);
            AddExecutor(ExecutorType.Summon, CardId.斗牛士);
            AddExecutor(ExecutorType.Summon, CardId.小手枪龙);
            AddExecutor(ExecutorType.Summon, CardId.狼人);
            AddExecutor(ExecutorType.Activate, CardId.凤凰龙);
            AddExecutor(ExecutorType.Activate, CardId.对死者的供奉, 死供Effect);
            AddExecutor(ExecutorType.Repos, CardId.青眼白龙, DefaultMonsterRepos);
            AddExecutor(ExecutorType.Repos, _CardId.手枪龙, DefaultMonsterRepos);
            AddExecutor(ExecutorType.Repos, CardId.破坏之剑士, DefaultMonsterRepos);
            AddExecutor(ExecutorType.Repos, CardId.连击龙, DefaultMonsterRepos);
            AddExecutor(ExecutorType.Repos, CardId.人造人, DefaultMonsterRepos);
            AddExecutor(ExecutorType.Repos, CardId.青眼白龙, 反转Repos);
            AddExecutor(ExecutorType.Repos, _CardId.手枪龙, 反转Repos);
            AddExecutor(ExecutorType.Repos, _CardId.黑魔术师, 反转Repos);
            AddExecutor(ExecutorType.Repos, CardId.小手枪龙, 反转Repos);
            AddExecutor(ExecutorType.Repos, CardId.狼人, 反转Repos);
            AddExecutor(ExecutorType.MonsterSet, CardId.千年盾, RushMonsterSummon);
            AddExecutor(ExecutorType.Repos, CardId.成金恐龙王, 成金恐龙王Summon);
            AddExecutor(ExecutorType.Summon, CardId.成金恐龙王, 成金恐龙王Summon);
            AddExecutor(ExecutorType.SummonOrSet, CardId.血腥魔兽人);
            AddExecutor(ExecutorType.Summon, CardId.凤凰龙, 凤凰龙summon);
            AddExecutor(ExecutorType.SpellSet, CardId.圣防);
            AddExecutor(ExecutorType.SpellSet, CardId.落穴);
            AddExecutor(ExecutorType.Activate, CardId.落穴, 落穴Effect);
            AddExecutor(ExecutorType.Activate, CardId.圣防, 圣防Effect);
            AddExecutor(ExecutorType.Activate, CardId.炸甲, 炸甲Effect);
            AddExecutor(ExecutorType.Activate, _CardId.魔法筒, 魔法筒Effect);
            AddExecutor(ExecutorType.Activate, _CardId.激流葬, 激流葬Effect);
            AddExecutor(ExecutorType.SpellSet, CardId.对死者的供奉);
            AddExecutor(ExecutorType.MonsterSet, CardId.圣精灵);
            AddExecutor(ExecutorType.MonsterSet, CardId.凤凰龙);
            
            AddExecutor(ExecutorType.MonsterSet, CardId.机器栗子);
            AddExecutor(ExecutorType.MonsterSet, CardId.成金恐龙王);
            AddExecutor(ExecutorType.Activate, CardId.古之规则);
            AddExecutor(ExecutorType.Activate, _CardId.破灭之龙魔导士, 破灭之龙魔导士Effect);
            AddExecutor(ExecutorType.Activate, _CardId.星战骑佩流安, 星战骑佩流安Effect);
            AddExecutor(ExecutorType.Activate, _CardId.加百列热茶, 加百列热茶Effect);
            AddExecutor(ExecutorType.Activate, DefaultDontChainMyself);
            AddExecutor(ExecutorType.Repos, CardId.青眼白龙, 圣防Repos);
            AddExecutor(ExecutorType.Repos, CardId.破坏之剑士, 圣防Repos);
            AddExecutor(ExecutorType.SummonOrSet, RushMonsterSummon);                               
            AddExecutor(ExecutorType.SpellSet);
            //AddExecutor(ExecutorType.Activate, CardId.sionmax, sionmaxEffect);
 
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
        public bool Monsterset()
        {
            if (Duel.Turn == 1)
            {
                return true;
            }
            else if (Bot.HasInHand(new[] {
                
                CardId.连击龙,
                CardId.青眼白龙,
                _CardId.黑魔术师,
                _CardId.手枪龙,
                CardId.人造人,
                CardId.破坏之剑士,

                }))
                return false;
            return true;
        }
        private bool 成金恐龙王Effect()
        {
            AI.SelectCard(CardId.成金哥布林);
            return true;
        }
        private bool 古之规则Effect()
        {
            if (Bot.HasInHandOrInSpellZone(_CardId.HMD))
                AI.SelectCard(_CardId.黑魔术师); 
            else if (Bot.HasInHand(CardId.青眼白龙))
                AI.SelectCard(CardId.青眼白龙);
            else
             AI.SelectCard(_CardId.黑魔术师);
            return true;
        }
        private bool 工厂Effect()
        {
            if (Bot.HasInGraveyard(CardId.青眼白龙) && Bot.HasInHandOrInSpellZone(_CardId.疾风弹))
            { AI.SelectCard(CardId.青眼白龙);
            AI.SelectNextCard(CardId.血腥魔兽人, CardId.圣精灵, _CardId.黑魔术师, CardId.青眼白龙);
            }
            else   if (Bot.HasInGraveyard(_CardId.黑魔术师) && Bot.HasInHandOrInSpellZone(_CardId.HMD))
            { AI.SelectCard(_CardId.黑魔术师);         
            AI.SelectNextCard(CardId.血腥魔兽人, CardId.圣精灵, CardId.青眼白龙,_CardId.黑魔术师);
            }
            else
             AI.SelectCard(CardId.血腥魔兽人, CardId.圣精灵);
            AI.SelectNextCard(CardId.血腥魔兽人, CardId.圣精灵, CardId.青眼白龙, _CardId.黑魔术师);
            return true;
        }
        private bool 成金恐龙王Summon()
        {
            if (Enemy.GetHandCount() < 1) return true;
            return false;
        }
        private bool 栗子summon()
        {
            if (Bot.HasInGraveyard(CardId.机器栗子) && Bot.GetMonstersInMainZone().Count == 0)
                    return true;
            return false;
        }
        private bool 栗子summon2()
        {
            if (Bot.HasInGraveyard(CardId.机器栗子) && Bot.GetMonstersInMainZone().Count == 1)
                return true;
            return false;
        }
        private bool 凤凰龙summon()
        {
            if (Bot.HasInGraveyard(new[] { CardId.连击龙, CardId.青眼白龙 }) && 
                Bot.MonsterZone.GetMatchingCardsCount(card => card.Level > 4) < 2 && 
                (Bot.MonsterZone.GetMatchingCardsCount(card => card.Level < 5) + Bot.Hand.GetMatchingCardsCount(card => card.Level < 5)) <= 2)
            {
                return true;
            }                
            return false;
        }
        private bool new海龙骑士Effect()
        {
            List<ClientCard> spells = Enemy.GetSpells();
            ClientCard selected = spells.FirstOrDefault(card => card.IsFacedown());
            AI.SelectCard(_CardId.手枪龙, CardId.海龙骑士, CardId.青眼白龙,CardId.狼人, _CardId.黑魔术师, CardId.圣精灵, CardId.小手枪龙, CardId.血腥魔兽人, CardId.机器栗子);
            AI.SelectNextCard(selected);
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
        public ClientCard GetBestEnemyCard_random()
        {
            // monsters
            ClientCard card = Util.GetProblematicEnemyMonster(0, true);
            if (card != null)
                return card;
            if (Util.GetOneEnemyBetterThanMyBest() != null)
            {
                card = Enemy.MonsterZone.GetHighestAttackMonster(true);
                if (card != null)
                    return card;
            }

            // spells
            List<ClientCard> enemy_spells = Enemy.GetSpells();
            RandomSort(enemy_spells);
            foreach (ClientCard sp in enemy_spells)
            {
                if (sp.IsFaceup() && !sp.IsDisabled()) return sp;
            }
            if (enemy_spells.Count > 0) return enemy_spells[0];

            List<ClientCard> monsters = Enemy.GetMonsters();
            if (monsters.Count > 0)
            {
                RandomSort(monsters);
                return monsters[0];
            }

            return null;
        }
        private bool 小手枪龙Effect()
        {
            ClientCard target = Util.GetOneEnemyBetterThanMyBest(true, true);
            if (target != null)
            {
                AI.SelectCard(target);
                return true;
            }
            List<ClientCard> targets = Enemy.GetSpells();
            RandomSort(targets);
            if (targets.Count > 0)
            {
                AI.SelectCard(targets[0]);
                return true;
            }
            target = GetBestEnemyCard_random();
            if (target != null)
            {
                AI.SelectCard(target);
                return true;
            }
            return true;
        }

    }
}





