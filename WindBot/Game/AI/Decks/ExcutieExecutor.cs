using YGOSharp.OCGWrapper.Enums;
using System.Collections.Generic;
using System.Linq;
using WindBot;
using WindBot.Game;
using WindBot.Game.AI;
using System;

namespace WindBot.Game.AI.Decks
{
    [Deck("Excutie", "AI_Excutie")]
    public class ExcutieExecutor : DefaultExecutor
    {
        public class CardId
        {
            public const int 夜魔女 = 120231034;
            public const int 光天女 = 120231031;
            public const int 浮游女 = 120231033;
            public const int 遥控女 = 120231035;
            public const int 救火女 = 120235019; 
            public const int 细剑女 = 120231032;
            public const int 主存女 = 120231036;
            public const int 花草女 = 120231037;
            public const int 劈裂史莱姆 = 120222036;

            public const int 紧急出动 = 120231039;
            public const int 天之选别 = 120231065;       
            public const int 紧急事态 = 120231040;
            public const int 幻影咆哮 = 120235065;

        }

        public ExcutieExecutor(GameAI ai, Duel duel)
            : base(ai, duel)
        {
            AddExecutor(ExecutorType.Activate, _CardId.黑洞, 黑洞Effect);
            AddExecutor(ExecutorType.Activate, _CardId.傲慢之壶);
            AddExecutor(ExecutorType.Activate, CardId.天之选别);
            AddExecutor(ExecutorType.Activate, _CardId.圣防, 圣防Effect);
            AddExecutor(ExecutorType.Activate, _CardId.捕获, 捕获Effect);
            AddExecutor(ExecutorType.Activate, _CardId.亚龙地狱, 亚龙地狱Effect);
            AddExecutor(ExecutorType.Activate, CardId.紧急事态);
            AddExecutor(ExecutorType.Activate, CardId.幻影咆哮);

            AddExecutor(ExecutorType.SpSummon, CardId.救火女, 救火女Sp);
            AddExecutor(ExecutorType.SpSummon, CardId.夜魔女);
            AddExecutor(ExecutorType.SpSummon, CardId.光天女);
            AddExecutor(ExecutorType.SpSummon, CardId.细剑女);
            AddExecutor(ExecutorType.SpSummon, CardId.救火女);
            AddExecutor(ExecutorType.SpSummon, CardId.花草女);
            AddExecutor(ExecutorType.Activate, CardId.紧急出动, 紧急出动Effect);
            AddExecutor(ExecutorType.SpellSet, _CardId.黑洞);
            AddExecutor(ExecutorType.SpellSet, _CardId.圣防);
            AddExecutor(ExecutorType.SpellSet, _CardId.傲慢之壶);

            AddExecutor(ExecutorType.Activate, CardId.花草女);
            AddExecutor(ExecutorType.Activate, CardId.遥控女, 遥控女Effect1);
            AddExecutor(ExecutorType.Activate, CardId.浮游女, 浮游女Effect);
            AddExecutor(ExecutorType.Activate, CardId.光天女, 光天女Effect);
            AddExecutor(ExecutorType.Activate, CardId.主存女, 主存女Effect);
            AddExecutor(ExecutorType.Activate, _CardId.海龙骑士, 海龙骑士Effect);
            AddExecutor(ExecutorType.Activate, CardId.劈裂史莱姆, 劈裂史莱姆Effect);

            AddExecutor(ExecutorType.Summon, CardId.夜魔女, 超可爱summon);
            AddExecutor(ExecutorType.Summon, CardId.浮游女, 超可爱summon);
            AddExecutor(ExecutorType.Summon, CardId.浮游女, 超可爱summon3);
            AddExecutor(ExecutorType.Summon, CardId.光天女, 超可爱summon);
            AddExecutor(ExecutorType.Summon, _CardId.幻影之龙);
            AddExecutor(ExecutorType.Summon, CardId.救火女, 超可爱summon);
            AddExecutor(ExecutorType.Summon, CardId.细剑女, 超可爱summon);
            AddExecutor(ExecutorType.Summon, CardId.遥控女, 超可爱summon2);
            AddExecutor(ExecutorType.Summon, CardId.主存女, 超可爱summon2);

            AddExecutor(ExecutorType.Activate, CardId.紧急出动, 紧急出动Effect2);
            AddExecutor(ExecutorType.Activate, CardId.救火女, 救火女Effect1);
            AddExecutor(ExecutorType.MonsterSet, _CardId.海龙骑士,海龙set);
            AddExecutor(ExecutorType.MonsterSet, _CardId.结界像,结界像set);
            AddExecutor(ExecutorType.MonsterSet, CardId.劈裂史莱姆, 史莱姆set);
            AddExecutor(ExecutorType.Summon, _CardId.海龙骑士);
            AddExecutor(ExecutorType.Summon, _CardId.结界像);
            AddExecutor(ExecutorType.Summon, CardId.劈裂史莱姆);

            AddExecutor(ExecutorType.Activate, CardId.遥控女, 遥控女Effect2);
            AddExecutor(ExecutorType.Activate, CardId.救火女, 救火女Effect2);
            //AddExecutor(ExecutorType.Activate, DefaultDontChainMyself);
            AddExecutor(ExecutorType.Repos, DefaultMonsterRepos);

            AddExecutor(ExecutorType.Repos, CardId.救火女, 反转Repos);;
            AddExecutor(ExecutorType.Repos, CardId.光天女, 反转Repos);
            AddExecutor(ExecutorType.Repos, CardId.夜魔女, 反转Repos);
            AddExecutor(ExecutorType.Repos, CardId.花草女, 圣防Repos);
            AddExecutor(ExecutorType.Repos, CardId.主存女, 圣防Repos);
            AddExecutor(ExecutorType.Repos, CardId.细剑女, 圣防Repos);
            AddExecutor(ExecutorType.Repos, CardId.救火女, 圣防Repos);
            AddExecutor(ExecutorType.Repos, CardId.遥控女, 圣防Repos);
            AddExecutor(ExecutorType.Repos, CardId.浮游女, 圣防Repos);
            AddExecutor(ExecutorType.Repos, CardId.光天女, 圣防Repos);
            AddExecutor(ExecutorType.Repos, CardId.夜魔女, 圣防Repos);
            AddExecutor(ExecutorType.SummonOrSet, ImFeelingLazy);
            AddExecutor(ExecutorType.SpellSet);
        }

   

        bool atkup = false;
        public override void OnNewTurn()
        {
            atkup = false;
            base.OnNewTurn();
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

        private IList<int> targets = new[] {
                CardId.夜魔女, CardId.光天女, CardId.细剑女, CardId.救火女, CardId.浮游女,CardId.遥控女, CardId.主存女, CardId.花草女};

        private IList<int> tar = new[] {
                CardId.花草女, CardId.主存女, CardId.遥控女, CardId.救火女, CardId.浮游女,CardId.光天女,CardId.细剑女};

        private IList<int> tar2 = new[] {
                CardId.花草女, CardId.主存女, CardId.遥控女, CardId.浮游女};
        private bool 救火女Sp()
        {
            int topattack = (Bot.MonsterZone.GetMatchingCardsCount(card => card.Level == 6) + Enemy.MonsterZone.GetMatchingCardsCount(card => card.Level == 6)) * 600 + 1700;
            if (Util.IsOneEnemyBetter() && !atkup && Util.GetBestAttack(Enemy) < topattack)
                return true;
            return false;
        }
        private bool 超可爱summon3()
        {
            int tributecount = 1;
            IList<int> ssmon = new[] {
                CardId.花草女, CardId.细剑女, CardId.救火女, CardId.夜魔女};
            if ((Bot.MonsterZone.GetMonsters().Count <= 2) && (Bot.HasInMonstersZoneOrInGraveyard(ssmon)))
            for (int j = 0; j < 7; ++j)
            {
                ClientCard tributeCard = Bot.MonsterZone[j];
                if (tributeCard == null) continue;
                if (tributeCard.IsCode(ssmon))
                    tributecount--;
            }
            return tributecount <= 0;
        }

        private bool 超可爱summon2()
        {
            int tributecount = 1;
            for (int j = 0; j < 7; ++j)
            {
                ClientCard tributeCard = Bot.MonsterZone[j];
                if (tributeCard == null) continue;
                if (tributeCard.GetDefensePower() < 1500)
                    tributecount--;
            }
            return tributecount <= 0;
        }

        private bool 超可爱summon()
        {
            int tributecount = 1;
            for (int j = 0; j < 7; ++j)
            {
                ClientCard tributeCard = Bot.MonsterZone[j];
                if (tributeCard == null) continue;
                if (tributeCard.GetDefensePower() < 1550)
                    tributecount--;
            }
            return tributecount <= 0;
        }
        private bool 救火女Effect1()
        {
            int topattack = (Bot.MonsterZone.GetMatchingCardsCount(card => card.Level == 6) + Enemy.MonsterZone.GetMatchingCardsCount(card => card.Level == 6)) * 600 + 1700;
            if ((Util.IsOneEnemyBetter() && !atkup && Util.GetBestAttack(Enemy) < topattack)
                || (Enemy.HasAttackingMonster() && Enemy.GetMonsters().Any(card => card?.Data == null))
                )
            { 
                foreach (ClientCard m in Bot.Hand)
                    AI.SelectCard(m);
                atkup = true;
                return true;
            }
           
            return false;
        }
        private bool 救火女Effect2()
        {
            int topattack = (Bot.MonsterZone.GetMatchingCardsCount(card => card.Level == 6) + Enemy.MonsterZone.GetMatchingCardsCount(card => card.Level == 6)) * 600 + 1700;
            ClientCard enemymon = Enemy.GetMonsters().GetLowestAttackMonster();
            if (enemymon != null && !atkup && Enemy.HasAttackingMonster() && !Util.IsOneEnemyBetter())
            {             
                    if (Util.GetTotalAttackingMonsterAttack(0) + 100 <= topattack ||  Enemy.LifePoints + enemymon.Attack <= topattack)
                    {
                   foreach (ClientCard m in Bot.Hand)
                        AI.SelectCard(m);
                    atkup = true;                
                    return true;
                    }
                return false;
            }
            return false;
        }
        private bool 光天女Effect()
        {
            ClientCard mon = Enemy.MonsterZone.GetMatchingCards(card => card.Level == 7 || card.Level == 8).OrderByDescending(card => card.Attack).FirstOrDefault();
            if (mon != null)
            {
                AI.SelectCard(tar);
                AI.SelectCard(mon);
                return true;
            }
            return false;
        }
        private bool 主存女Effect()
        {
                AI.SelectCard(tar);
                AI.SelectNextCard(Enemy.GetSpells());
                return true;
        }
        private bool 浮游女Effect()
        {
            AI.SelectCard(targets);
            return true;
        }
        private bool 遥控女Effect2()
        {
            foreach (ClientCard mon in Enemy.GetMonsters())
                if (mon.IsFacedown())
                {
                    AI.SelectCard(targets);
                    AI.SelectNextCard(mon);
                    return true;
                }
            return false;
        }

        private bool 遥控女Effect1()
        {
            ClientCard mon = Util.GetOneEnemyBetterThanValue(1800);
            if(mon != null && mon.Defense < Util.GetBestBotMonster().Attack && Bot.HasInGraveyard(targets))
                AI.SelectCard(targets);
            AI.SelectNextCard(mon);
            return true;
        }
 
        private bool 紧急出动Effect()
        {
            if(Bot.HasInGraveyard(targets)|| (Bot.GetMonsterCount() == 0 && Bot.HasInHandOrInGraveyard(targets)))
                AI.SelectCard(targets);
            return true;
        }
        private bool 捕获Effect()
        {
            foreach (ClientCard mon in Duel.LastSummonedCards)
                if (mon.Level >= 6 && mon.Level >= Enemy.MonsterZone.GetHighestLevelMonster().Level && mon.Attack > 1700 && Bot.HasInMonstersZone(targets))
                    AI.SelectCard(mon);
            return true;
        }
        private bool 紧急出动Effect2()
        {
            if (Bot.HasInHand(targets))
                AI.SelectCard(CardId.夜魔女, CardId.光天女, CardId.细剑女);
            return true;
        }
        private bool 劈裂史莱姆Effect()
        {
            ClientCard mon = Enemy.GetMonsters().GetHighestAttackMonster();
            ClientCard m = Bot.GetMonsters().GetHighestAttackMonster();
            if (mon != null && m != null && mon.Attack <= m.Attack + 1000 && mon.Attack > m.Attack)
            {          
                   AI.SelectCard(tar);
                    AI.SelectNextCard(mon);
                if (Bot.HasInGraveyard(_CardId.幻影之龙))
                { AI.SelectYesNo(true);
                  AI.SelectThirdCard(_CardId.幻影之龙);
                }
                return true;
            }
            if (mon != null && m != null && (Bot.HasInMonstersZone(CardId.花草女) || Bot.HasInMonstersZone(CardId.主存女)) && Bot.HasInGraveyard(_CardId.幻影之龙))
            {
                AI.SelectCard(CardId.花草女,CardId.主存女);
                AI.SelectNextCard(mon);
                AI.SelectYesNo(true);
                AI.SelectThirdCard(_CardId.幻影之龙);
                return true;
            }
            if (mon != null && Bot.HasInMonstersZone(_CardId.幻影之龙))
            {
                AI.SelectCard(_CardId.幻影之龙);
                AI.SelectNextCard(mon);
                AI.SelectYesNo(true);
                AI.SelectThirdCard(_CardId.幻影之龙);
                return true;
            }
            return false;
        }
        private bool 结界像set()
        {
            return Util.IsOneEnemyBetter();
        }

        private bool 海龙set()
        {
            if ((Util.IsOneEnemyBetter() || Duel.Turn == 1))
            {
                if (Enemy.GetSpellCount() ==0 || Bot.GetSpellCount() < 2)
                return true;
            }         
            return false;
        }
        private bool 史莱姆set()
        {
                if ( Duel.Turn == 1 || !Bot.HasInMonstersZone(tar2))
                return true;
            return false;
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
      
        private bool ImFeelingLazy()
        {
            if (Executors.Any(exec => (exec.Type == ExecutorType.SummonOrSet || exec.Type == ExecutorType.Summon || exec.Type == ExecutorType.MonsterSet) && exec.CardId == Card.Id))
                return false;
            return RushMonsterSummon();
        }
    }
}





