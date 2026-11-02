using YGOSharp.OCGWrapper.Enums;
using System.Collections.Generic;
using System.Linq;
using WindBot;
using WindBot.Game;
using WindBot.Game.AI;
using System;

namespace WindBot.Game.AI.Decks
{
    [Deck("DinocarriageDynarmix", "AI_DinocarriageDynarmix")]
    public class DinocarriageDynarmixExecutor : DefaultExecutor
    {
        public class CardId
        {
            public const int 超级恐龙王 = 120130029;
            public const int 凶怒甲龙 = 120231056;
            public const int 巨身多角龙 = 120130005;
            public const int 荒野盗龙 = 120151012;
            public const int 名流雷龙 = 120151013;
            public const int 抑龙 = 120151011;
            public const int 机械镰刀盗龙 = 120151009;
            public const int 成金恐龙王 = 120151010;
            public const int 侏罗纪世界 = 120151015;
            public const int 奇迹的共进化 = 120151016;
            public const int 成金哥布林 = 120151018;
            public const int 恐龙粉碎 = 120151017;
            public const int 超越进化 = 120151019;
            public const int 恐力重压 = 120151020;
            public const int 大恐龙驾L = 120151005;
            public const int 大恐龙驾R = 120151007;
            public const int 大恐龙驾 = 120151006;
        }

        public DinocarriageDynarmixExecutor(GameAI ai, Duel duel)
            : base(ai, duel)
        {
            AddExecutor(ExecutorType.SpSummon, CardId.大恐龙驾, MAXsummon);
            AddExecutor(ExecutorType.Activate, CardId.大恐龙驾, 大恐龙驾Effect);
            AddExecutor(ExecutorType.Activate, CardId.成金哥布林);
            AddExecutor(ExecutorType.Activate, CardId.抑龙);
            AddExecutor(ExecutorType.Activate, CardId.成金恐龙王, 成金恐龙王Effect);
            AddExecutor(ExecutorType.Activate, CardId.机械镰刀盗龙, 机械镰刀盗龙Effect);

            AddExecutor(ExecutorType.Summon, CardId.大恐龙驾L, MAXLsummon);
            AddExecutor(ExecutorType.Summon, CardId.大恐龙驾R, MAXRsummon);
            AddExecutor(ExecutorType.Activate, CardId.奇迹的共进化, 奇迹的共进化Effect);
            AddExecutor(ExecutorType.Repos, CardId.大恐龙驾L, 共进化repos);
            AddExecutor(ExecutorType.Repos, CardId.大恐龙驾R, 共进化repos);
            AddExecutor(ExecutorType.Repos, CardId.抑龙, 共进化repos);
            AddExecutor(ExecutorType.Repos, CardId.机械镰刀盗龙, 共进化repos);
            AddExecutor(ExecutorType.Repos, CardId.成金恐龙王, 共进化repos);
            AddExecutor(ExecutorType.Repos, CardId.名流雷龙, 共进化repos);
            AddExecutor(ExecutorType.Repos, CardId.荒野盗龙, 共进化repos);
            AddExecutor(ExecutorType.Summon, CardId.巨身多角龙, RushMonsterSummon);
            AddExecutor(ExecutorType.Summon, CardId.超级恐龙王, RushMonsterSummon);
            AddExecutor(ExecutorType.Summon, CardId.凶怒甲龙, RushMonsterSummon);
            AddExecutor(ExecutorType.Activate, CardId.超级恐龙王, 超级恐龙王Effect);
            AddExecutor(ExecutorType.Summon, CardId.机械镰刀盗龙);
            AddExecutor(ExecutorType.Summon, CardId.抑龙, 抑龙Summon);
            AddExecutor(ExecutorType.Summon, CardId.成金恐龙王, 成金恐龙王Summon);
            AddExecutor(ExecutorType.Activate, CardId.恐龙粉碎, 恐龙粉碎Effect);
            AddExecutor(ExecutorType.SummonOrSet, CardId.荒野盗龙, RushMonsterSummon);
            AddExecutor(ExecutorType.SummonOrSet, CardId.名流雷龙, RushMonsterSummon);

            AddExecutor(ExecutorType.MonsterSet, _CardId.千年盾, RushMonsterSummon);
            AddExecutor(ExecutorType.MonsterSet, CardId.大恐龙驾L, MAXLset);
            AddExecutor(ExecutorType.MonsterSet, CardId.大恐龙驾R, MAXRset);
            AddExecutor(ExecutorType.MonsterSet, CardId.抑龙);
            AddExecutor(ExecutorType.MonsterSet, CardId.成金恐龙王);
            AddExecutor(ExecutorType.MonsterSet, CardId.名流雷龙, monsterset);
            AddExecutor(ExecutorType.Activate, CardId.侏罗纪世界);
            AddExecutor(ExecutorType.Activate, CardId.凶怒甲龙);
            AddExecutor(ExecutorType.Activate, _CardId.圣防, 圣防Effect);
            AddExecutor(ExecutorType.Activate, _CardId.魔法筒, 魔法筒Effect);
            AddExecutor(ExecutorType.Activate, CardId.恐力重压);
            AddExecutor(ExecutorType.Activate, CardId.超越进化, 超越进化Effect);
            AddExecutor(ExecutorType.SpellSet);
            AddExecutor(ExecutorType.Repos, DefaultMonsterRepos);
            AddExecutor(ExecutorType.Repos, 圣防Repos);
            //AddExecutor(ExecutorType.Activate, DefaultDontChainMyself);
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
        private bool 大恐龙驾Effect()
        {
            {            
                IList<ClientCard> targets = new List<ClientCard>();
                foreach (ClientCard target in Enemy.GetMonsters())
                {
                    if (target.IsFacedown())
                        targets.Add(target);
                    if (targets.Count >= 2)
                        break;
                }
                if (targets.Count < 2)
                {
                    foreach (ClientCard target in Enemy.GetMonsters())
                    {
                        if (target.IsFaceup())
                            targets.Add(target);
                        if (targets.Count >= 2)
                            break;
                    }
                }
                if (targets.Count > 0)
                {
                    AI.SelectCard(
                     CardId.抑龙,
                     CardId.名流雷龙,
                     CardId.荒野盗龙,
                     CardId.巨身多角龙,
                     CardId.成金恐龙王,
                     CardId.机械镰刀盗龙,
                     CardId.凶怒甲龙, 
                     CardId.超级恐龙王,
                     CardId.大恐龙驾,
                     CardId.大恐龙驾L,
                     CardId.大恐龙驾R
                     );
                    AI.SelectNextCard(targets);
                    return true;
                }
                return false;
            }
        }
        private bool MAXsummon()
        {
            if (Bot.HasInMonstersZone(CardId.大恐龙驾))
            {
                return false;
            }
            else
            AI.SelectCard(CardId.大恐龙驾L);
            AI.SelectNextCard(CardId.大恐龙驾R);
            return true;
        }

        public override bool OnSelectHand()
        {
            // go first
            return true;
        }
        public bool monsterset()
        {
            if (Duel.Turn == 1)
            {
                return true;
            }
            else if (Bot.HasInHand(new[] {
                CardId.巨身多角龙,
                CardId.超级恐龙王,
                CardId.凶怒甲龙,
                }))
                return false;
            return true;
        }

        private bool 抑龙Summon()
        {
            if (Duel.Turn == 1) return false;
                if (Enemy.GetHandCount() > 1)  return true;
            return false;
        }
        private bool 成金恐龙王Summon()
        {
            if (Enemy.GetHandCount() < 1) return true;            
            return false;
        }

        private bool 成金恐龙王Effect()
        {           
            AI.SelectCard(CardId.成金哥布林);
            return true;
        }
        private bool 奇迹的共进化Effect()
        {
            if (Bot.HasInMonstersZone(CardId.大恐龙驾)) return false; 
            if (Bot.HasInHand(CardId.超级恐龙王) && Bot.MonsterZone.GetMatchingCardsCount(card => card.Level < 7) >= 1 && (Bot.HasInGraveyard(CardId.巨身多角龙) || Bot.HasInGraveyard(CardId.荒野盗龙)))
            {
                AI.SelectCard(                   
                    CardId.大恐龙驾L,
                    CardId.大恐龙驾R,
                    CardId.抑龙,
                    CardId.名流雷龙,
                    CardId.荒野盗龙, 
                    CardId.巨身多角龙
                    );
                AI.SelectNextCard(CardId.超级恐龙王); return true;
            }
            if (Bot.HasInHand(CardId.凶怒甲龙) && Bot.MonsterZone.GetMatchingCardsCount(card => card.Level < 5) >=1)
            {
                AI.SelectCard(
                    CardId.大恐龙驾L,
                    CardId.大恐龙驾R,
                    CardId.抑龙,
                    CardId.名流雷龙,
                    CardId.荒野盗龙,
                    CardId.巨身多角龙
                    );
                AI.SelectNextCard(CardId.凶怒甲龙); return true;
            }
            if (Bot.HasInHand(CardId.超级恐龙王) && Bot.MonsterZone.GetMatchingCardsCount(card => card.Level < 5) >= 1)
            {
                AI.SelectCard(
                    CardId.大恐龙驾L,
                    CardId.大恐龙驾R,
                    CardId.抑龙,
                    CardId.名流雷龙,
                    CardId.荒野盗龙,
                    CardId.巨身多角龙
                    );
                AI.SelectNextCard(CardId.超级恐龙王); return true;
            }
            return false;
        }
        private bool 机械镰刀盗龙Effect()
        {
            if (Bot.Hand.GetCardCount(CardId.大恐龙驾) >= 2) AI.SelectCard(CardId.大恐龙驾);
            else
            if (Bot.Hand.GetCardCount(CardId.大恐龙驾L) >= 2) AI.SelectCard(CardId.大恐龙驾L);
            else
            if (Bot.Hand.GetCardCount(CardId.大恐龙驾R) >= 2) AI.SelectCard(CardId.大恐龙驾R);
            else
            if (Bot.Hand.GetCardCount(CardId.超级恐龙王) >= 2) AI.SelectCard(CardId.超级恐龙王);
            else
                AI.SelectCard(
                    CardId.抑龙,
                    CardId.名流雷龙,
                    CardId.荒野盗龙,
                    CardId.巨身多角龙,
                    CardId.成金恐龙王,
                    CardId.机械镰刀盗龙
                    );
            return true;
        }
        private bool 恐龙粉碎Effect()
        {
            ClientCard check = null;
            List<ClientCard> spells = Enemy.GetSpells(); List<ClientCard> mons = Enemy.GetMonsters();
            foreach (ClientCard spell in spells) foreach (ClientCard mon in mons)
                {
                    if (Bot.HasInMonstersZone(CardId.大恐龙驾) && mon.IsFacedown()) check = mon;
                    else if (Bot.HasInMonstersZone(CardId.大恐龙驾) && spell.IsFacedown()) check = spell;
                    else if (spell.IsFacedown()) check = spell;
                    else check = mon;
                }

            if (Bot.Hand.GetCardCount(CardId.侏罗纪世界) >= 2)
            {
                AI.SelectCard(CardId.侏罗纪世界); AI.SelectNextCard(check);
            }
            else if (Bot.Hand.GetCardCount(CardId.大恐龙驾) >= 2)
            {
                AI.SelectCard(CardId.大恐龙驾); AI.SelectNextCard(check);
            }
            else if (Bot.Hand.GetCardCount(CardId.大恐龙驾R) >= 2)
            {
                AI.SelectCard(CardId.大恐龙驾R); AI.SelectNextCard(check);
            }
            else if (Bot.Hand.GetCardCount(CardId.大恐龙驾L) >= 2)
            {
                AI.SelectCard(CardId.大恐龙驾L); AI.SelectNextCard(check);
            }
            else if (Bot.Hand.GetCardCount(CardId.巨身多角龙) >= 2)
            {
                AI.SelectCard(CardId.巨身多角龙); AI.SelectNextCard(check);
            }
            else
                AI.SelectCard(                   
                    CardId.成金恐龙王,CardId.抑龙,CardId.奇迹的共进化,CardId.名流雷龙,CardId.超级恐龙王,CardId.恐力重压,CardId.超越进化,CardId.恐龙粉碎,CardId.荒野盗龙);
                AI.SelectNextCard(check);
            return true;
        }
        private bool 超越进化Effect()
        {
            AI.SelectCard(
                CardId.大恐龙驾,
                CardId.超级恐龙王,
                CardId.凶怒甲龙
                );
            return true;
        }
        private bool 超级恐龙王Effect()
        {
            AI.SelectCard(
                   CardId.巨身多角龙,
                   CardId.荒野盗龙,
                   CardId.名流雷龙
                   );
            return true;
        }
        private bool 共进化repos()
        {
            if (Bot.HasInHandOrInSpellZone(CardId.奇迹的共进化) && (Bot.Hand.GetCardCount(CardId.凶怒甲龙) + Bot.Hand.GetCardCount(CardId.超级恐龙王) >= 1))
                return true;
            return false;
        }
        private bool MAXLsummon()
        {
            if (Bot.Hand.GetCardCount(CardId.大恐龙驾L) >= 2 && Bot.HasInHandOrInSpellZone(CardId.奇迹的共进化) && (Bot.Hand.GetCardCount(CardId.凶怒甲龙) + Bot.Hand.GetCardCount(CardId.超级恐龙王) >= 1))
                return true;
            return false;
        }
        private bool MAXRsummon()
        {
            if (Bot.Hand.GetCardCount(CardId.大恐龙驾R) >= 2 && Bot.HasInHandOrInSpellZone(CardId.奇迹的共进化) && (Bot.Hand.GetCardCount(CardId.凶怒甲龙)+ Bot.Hand.GetCardCount(CardId.超级恐龙王) >= 1))
                return true;
            return false;
        }
        private bool MAXLset()
        {
            if (Bot.Hand.GetCardCount(CardId.大恐龙驾L) < 2) return false;
            return true;
        }
        private bool MAXRset()
        {
            if (Bot.Hand.GetCardCount(CardId.大恐龙驾R) < 2) return false;
            return true;
        }


    }
}





