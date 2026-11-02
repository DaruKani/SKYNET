using YGOSharp.OCGWrapper.Enums;
using System.Collections.Generic;
using System.Linq;
using WindBot;
using WindBot.Game;
using WindBot.Game.AI;
using System;

namespace WindBot.Game.AI.Decks
{
    [Deck("RushDragonDragears", "AI_RushDragonDragears")]
    public class RushDragonDragearsExecutor : DefaultExecutor
    {
        public class CardId
        {
            public const int 青眼白龙 = 120120000;
            public const int 破坏之剑士 = 120170000;
            public const int 黑魔术师 = 120130000;
            public const int 真红眼黑龙 = 120125001;
            public const int 恶魔召唤 = 120145000;
            public const int 人造人 = 120155000;
            public const int 连击龙 = 120110001;
            public const int 七星道魔术师 = 120105001;
            public const int 雅灭鲁拉 = 120120029;
            public const int 耳语妖精 = 120120018;
            public const int 侏儒兔 = 120228031;
            public const int 神秘庄家 = 120105006;
            public const int 光芒巫师 = 120145001;
            public const int 暗黑巫师 = 120105005;
            public const int 火星心少女 = 120145014;
            public const int 斗牛士 = 120170035;
            public const int 凤凰龙 = 120110009;
            public const int 七星道法师 = 120130016;
            public const int sionmax = 120150007;
            public const int 对死者的供奉 = 120151023;
            public const int 落穴 = 120150019;
            public const int 暗黑释放 = 120105013;
            public const int 七魔导枪 = 120228050;
        }

        public RushDragonDragearsExecutor(GameAI ai, Duel duel)
            : base(ai, duel)
        {
            AddExecutor(ExecutorType.Activate, _CardId.黑洞, 黑洞Effect);
            AddExecutor(ExecutorType.Activate, _CardId.强欲之壶);
            AddExecutor(ExecutorType.SpSummon);
            AddExecutor(ExecutorType.Repos, DefaultMonsterRepos);
            AddExecutor(ExecutorType.Activate, CardId.侏儒兔,侏儒兔Effect);
            AddExecutor(ExecutorType.Activate, CardId.七星道法师, 七星道法师Effect);
            AddExecutor(ExecutorType.Activate, CardId.对死者的供奉, 死供Eff);
            AddExecutor(ExecutorType.SummonOrSet, CardId.青眼白龙, RushMonsterSummon);
            AddExecutor(ExecutorType.Summon, CardId.破坏之剑士, RushMonsterSummon);
            AddExecutor(ExecutorType.SummonOrSet, CardId.恶魔召唤, RushMonsterSummon);
            AddExecutor(ExecutorType.SummonOrSet, CardId.连击龙, RushMonsterSummon);
            AddExecutor(ExecutorType.SummonOrSet, CardId.雅灭鲁拉, RushMonsterSummon);
            AddExecutor(ExecutorType.SummonOrSet, CardId.黑魔术师, RushMonsterSummon);
            AddExecutor(ExecutorType.SummonOrSet, CardId.真红眼黑龙, RushMonsterSummon);
            AddExecutor(ExecutorType.Summon, CardId.七星道魔术师, RushMonsterSummon);
            AddExecutor(ExecutorType.Summon, CardId.人造人, RushMonsterSummon);
            AddExecutor(ExecutorType.Summon, CardId.神秘庄家);
            AddExecutor(ExecutorType.Summon, CardId.侏儒兔, 侏儒兔Summon);
            AddExecutor(ExecutorType.Activate, CardId.神秘庄家, Draweffect);
            AddExecutor(ExecutorType.SpellSet, CardId.暗黑释放);
            AddExecutor(ExecutorType.SpellSet, CardId.落穴);
            AddExecutor(ExecutorType.Activate, CardId.暗黑释放, 圣防Effect);
            AddExecutor(ExecutorType.Activate, _CardId.魔法筒, 魔法筒Effect);
            AddExecutor(ExecutorType.Activate, _CardId.炸甲, 炸甲Effect);
            AddExecutor(ExecutorType.Activate, CardId.落穴, 落穴Effect);
            AddExecutor(ExecutorType.Activate, _CardId.激流葬, 激流葬Effect);
            AddExecutor(ExecutorType.Activate, _CardId.圣防, 圣防Effect);
            AddExecutor(ExecutorType.Activate, CardId.对死者的供奉, 死供Effect);
            AddExecutor(ExecutorType.SpellSet, CardId.对死者的供奉);
            AddExecutor(ExecutorType.Activate, CardId.凤凰龙);
            AddExecutor(ExecutorType.Summon, CardId.凤凰龙, 凤凰龙summon);
            AddExecutor(ExecutorType.MonsterSet, CardId.凤凰龙);
            AddExecutor(ExecutorType.MonsterSet, CardId.侏儒兔);
            AddExecutor(ExecutorType.MonsterSet, CardId.火星心少女, Monsterset);
            AddExecutor(ExecutorType.MonsterSet, CardId.七星道法师, Monsterset);
            AddExecutor(ExecutorType.MonsterSet, CardId.耳语妖精, Monsterset);
            AddExecutor(ExecutorType.Summon, CardId.七星道法师);
            AddExecutor(ExecutorType.Summon, _CardId.结界像);

            AddExecutor(ExecutorType.SummonOrSet, RushMonsterSummon);
            AddExecutor(ExecutorType.Activate, CardId.七魔导枪, 七魔导枪Effect);
            AddExecutor(ExecutorType.SpellSet);
            //AddExecutor(ExecutorType.Activate, CardId.sionmax, sionmaxEffect);
            AddExecutor(ExecutorType.Activate, CardId.耳语妖精, 耳语妖精Effect);
            AddExecutor(ExecutorType.Activate, CardId.火星心少女, 火星心少女Effect);
            AddExecutor(ExecutorType.Activate, CardId.连击龙);
            AddExecutor(ExecutorType.Activate, CardId.七星道魔术师);
            AddExecutor(ExecutorType.Activate, CardId.斗牛士, 斗牛士Effect);
            AddExecutor(ExecutorType.Activate, _CardId.破灭之龙魔导士, 破灭之龙魔导士Effect);
            AddExecutor(ExecutorType.Activate, _CardId.星战骑佩流安, 星战骑佩流安Effect);
            AddExecutor(ExecutorType.Activate, _CardId.加百列热茶, 加百列热茶Effect);
            AddExecutor(ExecutorType.Activate, DefaultDontChainMyself);
            AddExecutor(ExecutorType.Repos, CardId.破坏之剑士, 反转Repos);
            AddExecutor(ExecutorType.Repos, _CardId.结界像, 反转Repos);
            AddExecutor(ExecutorType.Repos, CardId.连击龙, 反转Repos);
            AddExecutor(ExecutorType.Repos, CardId.七星道魔术师, 反转Repos);
            AddExecutor(ExecutorType.Repos, CardId.青眼白龙, 圣防Repos);
            AddExecutor(ExecutorType.Repos, CardId.破坏之剑士, 圣防Repos);

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
            else if (Bot.HasInHandOrHasInMonstersZone(new[] {
                CardId.七星道魔术师,
                CardId.连击龙,
                CardId.青眼白龙,
                CardId.人造人,
                CardId.恶魔召唤,
                CardId.雅灭鲁拉,
                CardId.破坏之剑士,
                CardId.真红眼黑龙,
                CardId.黑魔术师
                }))
                return false;
            return true;
        }
        private bool 七魔导枪Effect()
        {
            if (Bot.HasInMonstersZone(CardId.七星道魔术师))
                AI.SelectCard(CardId.七星道魔术师); 
            return true;
        }
        private bool 火星心少女Effect()
        {
            foreach (ClientCard m in Bot.Hand)
                AI.SelectCard(m);
            AI.SelectNextCard(Enemy.GetMonsters().GetHighestAttackMonster());
            AI.SelectYesNo(true);
            return true;
        }
        private bool 死供Eff()
        {
            if (Util.IsOneEnemyBetterThanValue(2600, false))
            {
                foreach (ClientCard m in Bot.Hand)
                    AI.SelectCard(m);
                foreach (ClientCard mon in Enemy.GetMonsters())
                    if (mon.Level >= 8)
                        AI.SelectNextCard(mon);
                    else if (mon.Level >= 7)
                        AI.SelectNextCard(mon);
                    else if (mon.Level >= 5)
                        AI.SelectNextCard(mon);
                return true;
            }
            return false;
        }
        private bool 侏儒兔Summon()
        {
            if (Bot.HasInHandOrHasInMonstersZone(new[] {
                CardId.七星道魔术师,
                CardId.连击龙,
                CardId.青眼白龙,
                CardId.人造人,
                CardId.恶魔召唤,
                CardId.雅灭鲁拉,
                CardId.破坏之剑士,
                CardId.真红眼黑龙,
                CardId.黑魔术师
               }))
                return true;
             return false;
        }

       private bool 侏儒兔Effect()
        {
            AI.SelectYesNo(false);
            return true; 
        }
        private bool 七星道法师Effect()
        {
            AI.SelectCard(Enemy.GetMonsters().GetHighestAttackMonster());
            return true;
        }
        private bool Draweffect()
        {

            if ((Bot.MonsterZone.GetMatchingCardsCount(card => card.Level < 5) + Bot.Hand.GetMatchingCardsCount(card => card.Level < 5)) <= 2)
            {
                if ((Bot.Hand.GetMatchingCardsCount(card => card.Level > 5) > 2) && Bot.HasInHand(CardId.七星道魔术师))
                    AI.SelectCard(CardId.七星道魔术师);
                else
                    return false;
            }
            else
                AI.SelectCard(CardId.七星道法师, CardId.火星心少女, CardId.侏儒兔, CardId.耳语妖精, CardId.神秘庄家, CardId.光芒巫师, CardId.暗黑巫师);
            return true;
        }
        private bool 凤凰龙summon()
        {
            if (Bot.HasInGraveyard(new[] { CardId.连击龙, CardId.青眼白龙 }) &&
                Bot.MonsterZone.GetMatchingCardsCount(card => card.Level > 4) < 2 &&
                (Bot.MonsterZone.GetMatchingCardsCount(card => card.Level < 5) + Bot.Hand.GetMatchingCardsCount(card => card.Level < 5)) >= 2)
            {
                return true;
            }
            return false;
        }


    }
}





