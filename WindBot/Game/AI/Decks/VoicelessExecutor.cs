using System;
using System.Collections.Generic;
using System.Linq;
using WindBot;
using WindBot.Game;
using WindBot.Game.AI;
using YGOSharp.OCGWrapper.Enums;


namespace WindBot.Game.AI.Decks
{
    [Deck("Voiceless", "AI_Voiceless", "NotFinished")]
    public partial class VoicelessExecutor : DefaultExecutor
    {
        public class CardId
        {
            public const int Saffira = 51296484;
            public const int Lo = 25801745;
            public const int VVSauravis = 88284599;
            public const int VVSkullGuard = 10774240;
            public const int VVPrayer = 52472775;
            public const int Token = 52340445;
            public const int VVBarrier = 98477480;
            public const int VVRadiance = 86310763;
            public const int Blessing = 39114494;
            public const int Linkuriboh = 41999284;
            public const int SecureGardna = 2220237;

            public const int AshBlossom = 14558127;
            public const int GhostMournerMoonlitChill = 52038441;
            public const int EffectVeiler = 97268402;
            public const int InfiniteImperm = 10045474;
            public const int Droll = 94145021;
            public const int PrePrep = 13048472;
            public const int Diviner = 92919429;
            public const int Trias = 26866984;
            public const int CalledBy = 24224830;
            public const int Sauravis = 4810828;
            public const int SolemnStrike = 40605147;

            public const int DynaMondo = 73898890;
            public const int SPLittleKnight = 29301450;
            public const int UnderworldGoddess = 98127546;
            public const int Ntss = 80532587;
            public const int ChaosAngel = 22850702;
            public const int Herald = 79606837;

            // Problem cards 
            public const int MaxxC = 23434538;
            public const int NaturalExterio = 99916754;
            public const int NaturalBeast = 33198837;
            public const int ImperialOrder = 61740673;
            public const int SwordsmanLV7 = 37267041;
            public const int RoyalDecree = 51452091;
            public const int Number41BagooskatheTerriblyTiredTapir = 90590303;
            public const int InspectorBoarder = 15397015;
            public const int DimensionShifter = 91800273;
        }

        
        

        public VoicelessExecutor(GameAI ai, Duel duel)
            : base(ai, duel)
        {
            AddExecutor(ExecutorType.Activate, CardId.AshBlossom, DefaultAshBlossomAndJoyousSpring);
            AddExecutor(ExecutorType.Activate, CardId.EffectVeiler, DefaultEffectVeiler);
            AddExecutor(ExecutorType.Activate, CardId.InfiniteImperm, DefaultInfiniteImpermanence);
            AddExecutor(ExecutorType.Activate, CardId.SolemnStrike, DefaultSolemnStrike);
            AddExecutor(ExecutorType.Activate, CardId.Droll);
            AddExecutor(ExecutorType.Activate, CardId.CalledBy, DefaultCalledByTheGrave);
            AddExecutor(ExecutorType.Activate, CardId.Sauravis, SauravisEffect);
            AddExecutor(ExecutorType.Activate, CardId.VVSkullGuard, SkullCounter);
            AddExecutor(ExecutorType.Activate, CardId.VVSauravis, DragonSageEffect);
            AddExecutor(ExecutorType.Activate, CardId.DynaMondo, DynaEffect);
            AddExecutor(ExecutorType.Activate, CardId.Linkuriboh, LinkuribohEffect);
            AddExecutor(ExecutorType.Activate, CardId.SPLittleKnight, SPLittleKnightActivate);
            AddExecutor(ExecutorType.Activate, CardId.ChaosAngel, ChaosAngelActivate);
            AddExecutor(ExecutorType.Activate, CardId.Ntss, NtssActivate);

            // Select Skull Guardian's search before Lo's revival when both are
            // offered, preserving the intended CL1 search / CL2 revival ordering.
            AddExecutor(ExecutorType.Activate, CardId.VVSkullGuard, SkullSearch);
            AddExecutor(ExecutorType.Activate, CardId.Lo, LoEffect);
            AddExecutor(ExecutorType.Activate, CardId.Diviner, DivinerEffect);
            AddExecutor(ExecutorType.Activate, CardId.Herald, HeraldSearch);
            AddExecutor(ExecutorType.Activate, CardId.Blessing, BlessingEffect);
            AddExecutor(ExecutorType.Activate, CardId.PrePrep, PrePrepSearch);
            AddExecutor(ExecutorType.Activate, CardId.VVBarrier, BarrierFirst);
            AddExecutor(ExecutorType.Activate, CardId.Saffira, SaffEffect);
            AddExecutor(ExecutorType.Activate, CardId.Saffira, SaffiraRitual);
            AddExecutor(ExecutorType.Activate, CardId.VVPrayer, PrayerEffect);
            AddExecutor(ExecutorType.Activate, CardId.Trias, TriasEffect);
            AddExecutor(ExecutorType.Activate, CardId.VVRadiance, RadianceEffect);
            AddExecutor(ExecutorType.Summon, CardId.Diviner, DivinerSummon);
            AddExecutor(ExecutorType.Summon, CardId.Lo, LoSummon);
            AddExecutor(ExecutorType.SpSummon, CardId.Linkuriboh, LinkuribohSummon);
            AddExecutor(ExecutorType.SpSummon, CardId.SecureGardna, SecureGardnaSummon);
            AddExecutor(ExecutorType.SpSummon, CardId.VVSauravis, DragonSageSummon);
            AddExecutor(ExecutorType.SpSummon, CardId.DynaMondo, DynaLinkSummon);
            AddExecutor(ExecutorType.SpSummon, CardId.ChaosAngel, ChaosAngelSummon);
            AddExecutor(ExecutorType.SpSummon, CardId.SPLittleKnight, SPLittleKnightSpSummon);
            AddExecutor(ExecutorType.Activate, CardId.UnderworldGoddess);
            AddExecutor(ExecutorType.SpellSet, VoicelessSpellSet);
            AddExecutor(ExecutorType.Repos, DefaultMonsterRepos);
        }


        private List<int> notToNegateIdList = new List<int>{
                58699500, 20343502
            };
        private List<int> notToBeTrapTargetList = new List<int>{
                72144675, 86188410, 41589166, 11443677, 72566043, 1688285, 59071624, 6511113, 48183890, 952523, 22423493, 73639099
            };
        private List<int> targetNegateIdList = new List<int> {
                _CardId.EffectVeiler, _CardId.InfiniteImpermanence, CardId.GhostMournerMoonlitChill, _CardId.BreakthroughSkill, 74003290, 67037924,
                9753964, 66192538, 23204029, 73445448, 35103106, 30286474, 45002991, 5795980, 38511382, 53742162, 30430448
            };
        private List<int> notToDestroySpellTrap = new List<int> { 50005218, 6767771 };

        private List<int> activatedCardIdList = new List<int>();
        private List<ClientCard> currentNegateMonsterList = new List<ClientCard>();
        private List<ClientCard> currentDestroyCardList = new List<ClientCard>();
        private List<ClientCard> setTrapThisTurn = new List<ClientCard>();
        private List<ClientCard> summonThisTurn = new List<ClientCard>();
        private List<ClientCard> enemySetThisTurn = new List<ClientCard>();
        private List<ClientCard> escapeTargetList = new List<ClientCard>();
        private List<ClientCard> summonInChainList = new List<ClientCard>();
        private List<int> chainSummoningIdList = new List<int>(3);
        private Dictionary<int, int> CalledbytheGraveCount = new Dictionary<int, int>();
        private List<int> infiniteImpermanenceList = new List<int>();
        private int CrossoutDesignatorTarget = 0;

        public bool NtssActivate()
        {
            List<ClientCard> problemCardList = GetProblematicEnemyCardList(true, selfType: CardType.Monster);
            problemCardList.AddRange(GetNormalEnemyTargetList(true, true, CardType.Monster));
            if (GetProblematicEnemyCardList(true, selfType: CardType.Monster).Count() == 0) //If there's no cards on opponent's field
                return false;
            else
            {
                AI.SelectCard(problemCardList);
                activatedCardIdList.Add(Card.Id);
                return true;
            }
        }
        public bool ChaosAngelActivate()
        {
            List<ClientCard> targetList = GetNormalEnemyTargetList(true, true, CardType.Monster);
            if (targetList.Count() > 0)
            {
                AI.SelectCard(targetList);
                currentDestroyCardList.Add(targetList[0]);
                return true;
            }
            return false;
        }
        public List<ClientCard> GetNormalEnemyTargetList(bool canBeTarget = true, bool ignoreCurrentDestroy = false, CardType selfType = 0)
        {
            List<ClientCard> targetList = GetProblematicEnemyCardList(canBeTarget, selfType: selfType);
            List<ClientCard> enemyMonster = Enemy.GetMonsters().Where(card => card.IsFaceup() && !targetList.Contains(card)
                && (!ignoreCurrentDestroy || !currentDestroyCardList.Contains(card))).ToList();
            enemyMonster.Sort(CardContainer.CompareCardAttack);
            enemyMonster.Reverse();
            targetList.AddRange(enemyMonster);
            targetList.AddRange(Util.ShuffleList(Enemy.GetSpells().Where(card =>
                (!ignoreCurrentDestroy || !currentDestroyCardList.Contains(card)) && enemySetThisTurn.Contains(card)).ToList()));
            targetList.AddRange(Util.ShuffleList(Enemy.GetSpells().Where(card =>
                (!ignoreCurrentDestroy || !currentDestroyCardList.Contains(card)) && !enemySetThisTurn.Contains(card)).ToList()));
            targetList.AddRange(Util.ShuffleList(Enemy.GetMonsters().Where(card => card.IsFacedown() && (!ignoreCurrentDestroy || !currentDestroyCardList.Contains(card))).ToList()));
            return targetList;
        }

        public ClientCard GetProblematicEnemyMonster(int attack = 0, bool canBeTarget = false, bool ignoreCurrentDestroy = false, CardType selfType = 0)
        {
            List<ClientCard> dangerList = Enemy.MonsterZone.Where(c => c?.Data != null &&
                c.IsMonsterDangerous() && c.IsFaceup() && CheckCanBeTargeted(c, canBeTarget, selfType)
                && (!ignoreCurrentDestroy || !currentDestroyCardList.Contains(c))).OrderByDescending(card => card.Attack).ToList();
            if (dangerList.Count() > 0) return dangerList[0];

            List<ClientCard> invincibleList = Enemy.MonsterZone.Where(c => c?.Data != null &&
                c.IsMonsterInvincible() && c.IsFaceup() && CheckCanBeTargeted(c, canBeTarget, selfType)
                && (!ignoreCurrentDestroy || !currentDestroyCardList.Contains(c))).OrderByDescending(card => card.Attack).ToList();
            if (invincibleList.Count() > 0) return invincibleList[0];

            List<ClientCard> equippedList = Enemy.MonsterZone.Where(c => c?.Data != null &&
                c.EquipCards.Count() > 0 && CheckCanBeTargeted(c, canBeTarget, selfType)
                && (!ignoreCurrentDestroy || !currentDestroyCardList.Contains(c))).OrderByDescending(card => card.Attack).ToList();
            if (equippedList.Count() > 0) return equippedList[0];

            List<ClientCard> enemyMonsters = Enemy.GetMonsters().OrderByDescending(card => card.Attack).ToList();
            if (enemyMonsters.Count() > 0)
            {
                foreach (ClientCard target in enemyMonsters)
                {
                    if ((target.HasType(CardType.Fusion | CardType.Ritual | CardType.Synchro | CardType.Xyz)
                            || (target.HasType(CardType.Link) && target.LinkCount >= 2))
                        && CheckCanBeTargeted(target, canBeTarget, selfType) && (!ignoreCurrentDestroy || !currentDestroyCardList.Contains(target))
                    ) return target;
                }
            }

            if (attack >= 0)
            {
                if (attack == 0)
                    attack = Util.GetBestAttack(Bot);
                List<ClientCard> betterList = Enemy.MonsterZone.GetMonsters()
                    .Where(card => card.GetDefensePower() >= attack && card.GetDefensePower() > 0 && card.IsAttack() && CheckCanBeTargeted(card, canBeTarget, selfType)
                    && (!ignoreCurrentDestroy || !currentDestroyCardList.Contains(card))).OrderByDescending(card => card.Attack).ToList();
                if (betterList.Count() > 0) return betterList[0];
            }
            return null;
        }

        public List<ClientCard> GetProblematicEnemyCardList(bool canBeTarget = false, bool ignoreSpells = false, CardType selfType = 0)
        {
            List<ClientCard> resultList = new List<ClientCard>();
            
            List<ClientCard> problemEnemySpellList = Enemy.SpellZone.Where(c => c?.Data != null && !resultList.Contains(c) && !currentDestroyCardList.Contains(c)
                && c.IsFloodgate() && c.IsFaceup() && CheckCanBeTargeted(c, canBeTarget, selfType)).ToList();
            if (problemEnemySpellList.Count() > 0) resultList.AddRange(Util.ShuffleList(problemEnemySpellList));

            List<ClientCard> dangerList = Enemy.MonsterZone.Where(c => c?.Data != null && !resultList.Contains(c) && !currentDestroyCardList.Contains(c)
                && c.IsMonsterDangerous() && c.IsFaceup() && CheckCanBeTargeted(c, canBeTarget, selfType)).OrderByDescending(card => card.Attack).ToList();
            if (dangerList.Count() > 0
                && (Duel.Player == 0 || (Duel.Phase > DuelPhase.Main1 && Duel.Phase < DuelPhase.Main2))) resultList.AddRange(dangerList);

            List<ClientCard> invincibleList = Enemy.MonsterZone.Where(c => c?.Data != null && !resultList.Contains(c) && !currentDestroyCardList.Contains(c)
                && c.IsMonsterInvincible() && c.IsFaceup() && CheckCanBeTargeted(c, canBeTarget, selfType)).OrderByDescending(card => card.Attack).ToList();
            if (invincibleList.Count() > 0) resultList.AddRange(invincibleList);

            List<ClientCard> enemyMonsters = Enemy.GetMonsters().Where(c => !currentDestroyCardList.Contains(c)).OrderByDescending(card => card.Attack).ToList();
            if (enemyMonsters.Count() > 0)
            {
                foreach(ClientCard target in enemyMonsters)
                {
                    if ( (target.HasType(CardType.Fusion | CardType.Ritual | CardType.Synchro | CardType.Xyz)
                            || (target.HasType(CardType.Link) && target.LinkCount >= 2))
                        && !resultList.Contains(target) && CheckCanBeTargeted(target, canBeTarget, selfType)
                        )
                    {
                        resultList.Add(target);
                    }
                }
            }

            List<ClientCard> spells = Enemy.GetSpells().Where(c => c.IsFaceup() && !currentDestroyCardList.Contains(c)
                && c.HasType(CardType.Equip | CardType.Pendulum | CardType.Field | CardType.Continuous) && CheckCanBeTargeted(c, canBeTarget, selfType)
                && !notToDestroySpellTrap.Contains(c.Id)).ToList();
            if (spells.Count() > 0 && !ignoreSpells) resultList.AddRange(Util.ShuffleList(spells));

            return resultList;
        }

        public bool CheckCanBeTargeted(ClientCard card, bool canBeTarget, CardType selfType)
        {
            if (card == null) return true;
            if (canBeTarget)
            {
                if (card.IsShouldNotBeTarget()) return false;
                if (((int)selfType & (int)CardType.Monster) > 0 && card.IsShouldNotBeMonsterTarget()) return false;
                if (((int)selfType & (int)CardType.Spell) > 0 && card.IsShouldNotBeSpellTrapTarget()) return false;
                if (((int)selfType & (int)CardType.Trap) > 0
                    && card.IsShouldNotBeSpellTrapTarget()) return false;
            }
            return true;
        }
        public bool CheckCanDirectAttack()
        {
            return Enemy.GetMonsterCount() == 0 && !activatedCardIdList.Contains(CardId.SPLittleKnight) && Duel.Turn > 1 && Duel.Player == 0 && Duel.Phase < DuelPhase.Main2;
        }

        public bool SPLittleKnightSpSummon()
        {
            if (CheckCanDirectAttack())
            {
                // for attack
                List<ClientCard> materialList = SPLittleKnightSelectMaterial();
                if (materialList.Count() >= 2 && GetMaterialAttack(materialList) < 1600)
                {
                    AI.SelectMaterials(materialList);
                    return true;
                }
            } else if (!NegatedCheck(true) && GetProblematicEnemyCardList(true, selfType: CardType.Monster).Count() > 0)
            {
                // for remove 
                List<ClientCard> materialList = SPLittleKnightSelectMaterial(true);
                if (materialList.Count() >= 2 && materialList.Any(card => card.HasType(CardType.Fusion | CardType.Synchro | CardType.Xyz | CardType.Link)))
                {
                    AI.SelectMaterials(materialList);
                    return true;
                }
            }
            return false;
        }
        public int GetMaterialAttack(List<ClientCard> materials)
        {
            if (Util.IsTurn1OrMain2()) return 0;
            int result = 0;
            foreach (ClientCard material in materials)
            {
                if (material.IsAttack() || !summonThisTurn.Contains(material)) result += material.Attack;
            }
            return result;
        }
        public List<ClientCard> GetCanBeUsedForLinkMaterial(bool useAdvancedMonster = false, Func<ClientCard, bool> exceptRule = null)
        {
            List<ClientCard> materialList = Bot.GetMonsters().Where(card => {
                if (card.IsFacedown() || (exceptRule != null && exceptRule(card))) return false;
                if (card.IsCode(CardId.ChaosAngel)
                    && !useAdvancedMonster && (card.IsAttack() || !summonThisTurn.Contains(card))) return false;
                return true;
            }).ToList();
            materialList.Sort(CompareUsableAttack);
            return materialList;
        }
        public List<ClientCard> SPLittleKnightSelectMaterial(bool needToUseEffect = false)
        {
            List<ClientCard> candidates = GetCanBeUsedForLinkMaterial(true)
                .Where(card => card.HasType(CardType.Effect)
                    && (!card.HasType(CardType.Link) || card.LinkCount <= 2)).ToList();
            return Util.GetLinkMaterials(candidates, 2, 2, 2).FirstOrDefault(materials =>
                (!needToUseEffect || materials.Any(card => card.HasType(CardType.Fusion | CardType.Synchro | CardType.Xyz | CardType.Link)))
                && Util.GetBotAvailZonesFromExtraDeck(materials) > 0) ?? new List<ClientCard>();
        }
        public List<ClientCard> GetDangerousCardinEnemyGrave(bool onlyMonster = false)
        {
            List<ClientCard> result = Enemy.Graveyard.GetMatchingCards(card => 
                (!onlyMonster || card.IsMonster())).ToList(); //some specific matchup stuff removed
            List<int> dangerMonsterIdList = new List<int>{
                99937011, 63542003, 9411399, 28954097, 30680659
            };
            result.AddRange(Enemy.Graveyard.GetMatchingCards(card => dangerMonsterIdList.Contains(card.Id)));
            return result;
        }

        public ClientCard GetBestEnemyMonster(bool onlyFaceup = false, bool canBeTarget = false, bool ignoreCurrentDestroy = false, CardType selfType = 0)
        {
            ClientCard card = GetProblematicEnemyMonster(0, canBeTarget, ignoreCurrentDestroy, selfType);
            if (card != null)
                return card;

            card = Enemy.MonsterZone.Where(c => c?.Data != null && c.HasType(CardType.Monster) && c.IsFaceup()
                && CheckCanBeTargeted(c, canBeTarget, selfType) && (!ignoreCurrentDestroy || !currentDestroyCardList.Contains(c)))
                .OrderByDescending(c => c.Attack).FirstOrDefault();
            if (card != null)
                return card;

            List<ClientCard> monsters = Enemy.GetMonsters().Where(c => CheckCanBeTargeted(c, canBeTarget, selfType)
                && (!ignoreCurrentDestroy || !currentDestroyCardList.Contains(c))).ToList();

            if (monsters.Count() > 0 && !onlyFaceup)
                return Util.ShuffleList(monsters)[0];

            return null;
        }
        public bool SPLittleKnightActivate()
        {
            if (ActivateDescription == -1 || ActivateDescription == Util.GetStringId(CardId.SPLittleKnight, 0))
            {
                // banish card
                List<ClientCard> problemCardList = GetProblematicEnemyCardList(true, selfType: CardType.Monster);
                problemCardList.AddRange(GetDangerousCardinEnemyGrave(false));
                problemCardList.AddRange(GetNormalEnemyTargetList(true, true, CardType.Monster));
                problemCardList.AddRange(Enemy.Graveyard.Where(card => card.HasType(CardType.Monster)).OrderByDescending(card => card.Attack));
                problemCardList.AddRange(Enemy.Graveyard.Where(card => !card.HasType(CardType.Monster)));
                if (problemCardList.Count() > 0)
                {
                    AI.SelectCard(problemCardList);
                    activatedCardIdList.Add(Card.Id);
                    return true;
                }
            } else if (ActivateDescription == Util.GetStringId(CardId.SPLittleKnight, 1))
            {
                ClientCard selfMonster = null;
                foreach (ClientCard target in Bot.GetMonsters())
                {
                    if (Duel.ChainTargets.Contains(target) && !escapeTargetList.Contains(target))
                    {
                        selfMonster = target;
                        break;
                    }
                }
                if (selfMonster == null)
                {
                    if (Duel.Player == 1)
                    {
                        selfMonster = Bot.GetMonsters().Where(card => card.IsAttack()).OrderBy(card => card.Attack).FirstOrDefault();
                        if (!Util.IsOneEnemyBetterThanValue(selfMonster.Attack, true)) selfMonster = null;
                    }
                }
                if (selfMonster != null)
                {
                    ClientCard nextMonster = null;
                    List<ClientCard> selfTargetList = Bot.GetMonsters().Where(card => card != selfMonster).ToList();
                    if (Enemy.GetMonsterCount() == 0 && selfTargetList.Count() > 0)
                    {
                        selfTargetList.Sort(CompareUsableAttack);
                        nextMonster = selfTargetList[0];
                        escapeTargetList.Add(nextMonster);
                    }
                    if (Enemy.GetMonsterCount() > 0)
                    {
                        nextMonster = GetBestEnemyMonster(false, true, true);
                        currentDestroyCardList.Add(nextMonster);
                    }
                    if (nextMonster != null)
                    {
                        AI.SelectCard(selfMonster);
                        AI.SelectNextCard(nextMonster);
                        escapeTargetList.Add(selfMonster);
                        activatedCardIdList.Add(Card.Id + 1);
                        return true;
                    }
                }
            }

            return false;
        }

        public void SelectSTPlace(ClientCard card = null, bool avoid_Impermanence = false, List<int> avoid_list = null)
        {
            List<int> list = new List<int> { 0, 1, 2, 3, 4 };
            Util.ShuffleListInPlace(list);
            foreach (int seq in list)
            {
                int zone = (int)System.Math.Pow(2, seq);
                if (Bot.SpellZone[seq] == null)
                {
                    if (card != null && card.Location == CardLocation.Hand && avoid_Impermanence && infiniteImpermanenceList.Contains(seq)) continue;
                    if (avoid_list != null && avoid_list.Contains(seq)) continue;
                    AI.SelectPlace(zone);
                    return;
                };
            }
            AI.SelectPlace(0);
        }

        public bool NegatedCheck(bool disablecheck = true)
        {
            if (Card.IsSpell() || Card.IsTrap())
            {
                if (SpellNegatable()) return true;
            }
            if (CheckCalledbytheGrave(Card.Id) > 0 || Card.Id == CrossoutDesignatorTarget)
            {
                return true;
            }
            if (Card.IsMonster() && Card.Location == CardLocation.MonsterZone && Card.IsDefense())
            {
                if (DefaultCheckWhetherNumber41IsActive())
                {
                    return true;
                }
            }
            if (disablecheck)
            {
                return Card.IsDisabled();
            }
            return false;
        }

        public int CheckCalledbytheGrave(int id)
        {
            if (!CalledbytheGraveCount.ContainsKey(id))
            {
                return 0;
            }
            return CalledbytheGraveCount[id];
        }
        // activate of CalledbytheGrave
        public bool CalledByEffect()
        {
            if (NegatedCheck(true)) return false;
            if (Duel.LastChainPlayer == 1)
            {
                // negate
                if (Util.GetLastChainCard().IsMonster())
                {
                    int code = Util.GetLastChainCard().Id;
                    if (code == 0) return false;
                    if (CheckCalledbytheGrave(code) > 0 || CrossoutDesignatorTarget == code) return false;
                    if (Enemy.Graveyard.GetFirstMatchingCard(card => card.IsMonster() && card.IsOriginalCode(code)) != null)
                    {
                        if (!(Card.Location == CardLocation.SpellZone))
                        {
                            SelectSTPlace(null, true);
                        }
                        AI.SelectCard(code);
                        CalledbytheGraveCount[code] = 2;
                        return true;
                    }
                }

                // banish target
                foreach (ClientCard cards in Enemy.Graveyard)
                {
                    if (Duel.ChainTargets.Contains(cards))
                    {
                        int code = cards.Id;
                        AI.SelectCard(cards);
                        CalledbytheGraveCount[code] = 2;
                        return true;
                    }
                }

                // become targets
                if (Duel.ChainTargets.Contains(Card))
                {
                    List<ClientCard> enemy_monsters = Enemy.Graveyard.GetMatchingCards(card => card.IsMonster()).ToList();
                    if (enemy_monsters.Count > 0)
                    {
                        enemy_monsters.Sort(CardContainer.CompareCardAttack);
                        enemy_monsters.Reverse();
                        int code = enemy_monsters[0].Id;
                        AI.SelectCard(code);
                        CalledbytheGraveCount[code] = 2;
                        return true;
                    }
                }
            }

            // avoid danger monster in grave
            if (Duel.LastChainPlayer == 1) return false;
            List<ClientCard> targets = CheckDangerousCardinEnemyGrave(true);
            if (targets.Count() > 0)
            {
                int code = targets[0].Id;
                if (!(Card.Location == CardLocation.SpellZone))
                {
                    SelectSTPlace(null, true);
                }
                AI.SelectCard(code);
                CalledbytheGraveCount[code] = 2;
                return true;
            }

            return false;
        }

        public virtual List<ClientCard> CheckDangerousCardinEnemyGrave(bool onlyMonster = false)
        {
            return new List<ClientCard>();
        }

        // Accepted uses are not predictions of successful resolution. Card locations,
        // resources and prompt candidates always come from the observed duel state.
        private readonly HashSet<long> _voicelessUsed = new HashSet<long>();
        private bool _normalSummonUsed;
        private int _pendingVoicelessSummon;
        private int _acceptedActivationId;
        private int _radianceSelectedId;
        private readonly HashSet<ClientCard> _selectedLinkMaterials = new HashSet<ClientCard>();

        private bool VoicelessNormalUsed { get { return _normalSummonUsed; } }

        private bool VoicelessEffectUsed(int id, int effect)
        {
            return _voicelessUsed.Contains(((long)id << 4) | (uint)effect);
        }

        private bool AcceptVoicelessEffect(int effect)
        {
            _voicelessUsed.Add(((long)Card.Id << 4) | (uint)effect);
            _acceptedActivationId = Card.Id;
            return true;
        }

        private int VoicelessPromptSource()
        {
            ChainInfo solving = Duel.GetCurrentSolvingChainInfo();
            if (solving != null)
                return solving.ActivatePlayer == 0 ? solving.ActivateId : 0;
            // Costs may precede MSG_CHAINING. This is a captured accepted action,
            // retired when the server confirms it, never a rejected candidate.
            if (_acceptedActivationId != 0) return _acceptedActivationId;
            ClientCard chain = Duel.GetCurrentChainCard();
            if (chain != null) return chain.Controller == 0 ? chain.Id : 0;
            return _pendingVoicelessSummon;
        }

        private bool HasFaceupLo()
        {
            return Bot.MonsterZone.Any(c => c != null && c.IsCode(CardId.Lo) && c.IsFaceup());
        }

        private bool HasFaceupSkull()
        {
            return Bot.MonsterZone.Any(c => c != null && c.IsCode(CardId.VVSkullGuard) && c.IsFaceup());
        }

        public override bool OnSelectHand()
        {
            return true;
        }

        private bool LoSummon()
        {
            if (HasFaceupLo() || _normalSummonUsed) return false;
            _normalSummonUsed = true;
            return true;
        }

        private bool DivinerSummon()
        {
            if (_normalSummonUsed || HasFaceupLo()) return false;
            _normalSummonUsed = true;
            return true;
        }

        private bool LoEffect()
        {
            if (Card.Location == CardLocation.Grave) return AcceptVoicelessEffect(1);
            if (Card.Location != CardLocation.MonsterZone || Card.IsDisabled()) return false;
            return AcceptVoicelessEffect(0);
        }

        private bool BarrierFirst()
        {
            if (Card.Location == CardLocation.Hand || Card.IsFacedown())
                return !Bot.SpellZone.Any(c => c != null && c != Card && c.IsCode(CardId.VVBarrier) && c.IsFaceup());
            if (Card.Location != CardLocation.SpellZone || Card.IsDisabled()) return false;
            return AcceptVoicelessEffect(0);
        }

        private bool SaffEffect()
        {
            // The matching Lua identifies the ritual effect by LOCATION_GRAVE;
            // the helper gives that effect no per-card StringId description.
            return Card.Location == CardLocation.Hand && AcceptVoicelessEffect(0);
        }

        private bool SaffiraRitual()
        {
            if (Card.Location != CardLocation.Grave) return false;
            return AcceptVoicelessEffect(1);
        }

        private bool SkullSearch()
        {
            bool summonTrigger = AI.SelectingEffectYn && ActivateDescription == -1
                && Duel.CurrentChain.Count == 0 && Duel.LastSummonedCards.Contains(Card);
            if (ActivateDescription != Util.GetStringId(CardId.VVSkullGuard, 0) && !summonTrigger) return false;
            return AcceptVoicelessEffect(0);
        }

        private bool SkullCounter()
        {
            if (ActivateDescription != Util.GetStringId(CardId.VVSkullGuard, 1)
                || Duel.LastChainPlayer != 1 || !HasFaceupLo() || Card.IsDisabled()) return false;
            return AcceptVoicelessEffect(1);
        }

        private bool SauravisEffect()
        {
            // The engine offers the hand effect only for an actual targeting chain,
            // and the field effect only in a summon-negation window.
            if (Card.Location == CardLocation.Hand) return Duel.LastChainPlayer == 1;
            return Card.Location == CardLocation.MonsterZone && !Card.IsDisabled();
        }

        private bool PrePrepSearch()
        {
            return AcceptVoicelessEffect(0);
        }

        private bool PrayerEffect()
        {
            return AcceptVoicelessEffect(Card.Location == CardLocation.Grave ? 1 : 0);
        }

        private bool DivinerEffect()
        {
            return AcceptVoicelessEffect(ActivateDescription == Util.GetStringId(CardId.Diviner, 1)
                || Card.Location == CardLocation.Grave || Card.Location == CardLocation.Removed ? 1 : 0);
        }

        private bool TriasEffect()
        {
            ClientCard diviner = Bot.MonsterZone.FirstOrDefault(c => c != null && c.IsCode(CardId.Diviner));
            if (diviner == null || VoicelessEffectUsed(CardId.Diviner, 1)) return false;
            // Finish the mill first, unless Tributing Diviner dodges an opponent's
            // targeted negation. Do not Tribute Lo merely because the engine allows it.
            if (Duel.LastChainPlayer == 0) return false;
            if (Duel.LastChainPlayer == 1 && !Duel.ChainTargets.Contains(diviner)) return false;
            return AcceptVoicelessEffect(0);
        }

        private bool HeraldSearch()
        {
            return AcceptVoicelessEffect(0);
        }

        private bool BlessingEffect()
        {
            if (Card.Location == CardLocation.Hand || Card.IsFacedown())
                return !Bot.SpellZone.Any(c => c != null && c != Card && c.IsCode(CardId.Blessing) && c.IsFaceup());
            if (ActivateDescription == Util.GetStringId(CardId.Blessing, 1))
                return AcceptVoicelessEffect(0);
            bool summonTrigger = AI.SelectingEffectYn && ActivateDescription == -1
                && Duel.CurrentChain.Count == 0
                && Duel.LastSummonedCards.Any(c => c != null && c.IsFaceup() && !c.HasType(CardType.Ritual));
            if (ActivateDescription == Util.GetStringId(CardId.Blessing, 2) || summonTrigger)
                return AcceptVoicelessEffect(1);
            return false;
        }

        private bool DragonSageSummon()
        {
            // Prefer sending Lo to the GY first when the selected route calls for
            // next-turn revival; otherwise an already-present Sage remains useful.
            if (HasFaceupLo() && ShouldUseVoicelessLink(CardId.Linkuriboh)) return false;
            _pendingVoicelessSummon = CardId.VVSauravis;
            _voicelessUsed.Add(((long)CardId.VVSauravis << 4));
            return true;
        }

        private bool DragonSageEffect()
        {
            if (Card.Location != CardLocation.MonsterZone || Duel.LastChainPlayer != 1 || Card.IsDisabled()) return false;
            return AcceptVoicelessEffect(1);
        }

        private bool DynaLinkSummon()
        {
            if (!ShouldUseVoicelessLink(CardId.DynaMondo)) return false;
            _pendingVoicelessSummon = CardId.DynaMondo;
            _selectedLinkMaterials.Clear();
            return true;
        }

        private bool DynaEffect()
        {
            if (ActivateDescription == Util.GetStringId(CardId.DynaMondo, 1))
                return Duel.Player == 1 && AcceptVoicelessEffect(1);
            bool summonTrigger = AI.SelectingEffectYn && ActivateDescription == -1
                && Duel.CurrentChain.Count == 0 && Duel.LastSummonedCards.Contains(Card);
            if (ActivateDescription != Util.GetStringId(CardId.DynaMondo, 0) && !summonTrigger) return false;
            // The initial shuffle would consume the Ritual intended for next turn.
            // Use removal only when a real enemy threat makes it worthwhile.
            if (GetProblematicEnemyCardList(true, selfType: CardType.Monster).Count == 0) return false;
            return AcceptVoicelessEffect(0);
        }

        private bool LinkuribohSummon()
        {
            if (!ShouldUseVoicelessLink(CardId.Linkuriboh)) return false;
            _pendingVoicelessSummon = CardId.Linkuriboh;
            _selectedLinkMaterials.Clear();
            return true;
        }

        private bool SecureGardnaSummon()
        {
            if (!ShouldUseVoicelessLink(CardId.SecureGardna)) return false;
            _pendingVoicelessSummon = CardId.SecureGardna;
            _selectedLinkMaterials.Clear();
            return true;
        }

        private bool LinkuribohEffect()
        {
            return Card.Location == CardLocation.MonsterZone && Duel.Player == 1;
        }

        private bool RadianceEffect()
        {
            if (Card.IsFacedown()) return true;
            if (ActivateDescription == Util.GetStringId(CardId.VVRadiance, 1))
            {
                if (Duel.CurrentChain.Count > 0 && Duel.LastChainPlayer == 0) return false;
                if (GetNormalEnemyTargetList(true, false, CardType.Trap).Count == 0) return false;
                // Preserve removal for an opponent's actual commitment; do not
                // destroy a lone harmless set card just because a window opened.
                if (Duel.Player == 1 && Duel.LastChainPlayer == -1
                    && Enemy.GetMonsterCount() == 0 && GetProblematicEnemyCardList(true).Count == 0) return false;
                return AcceptVoicelessEffect(0);
            }
            if (ActivateDescription == Util.GetStringId(CardId.VVRadiance, 0))
            {
                if (Duel.Player == 1 && HasFaceupSkull() && HasFaceupLo()) return false;
                if (Duel.Player == 0 && HasFaceupSkull() && HasFaceupLo()) return false;
                return AcceptVoicelessEffect(0);
            }
            return false;
        }

        private bool ChaosAngelSummon()
        {
            if (GetProblematicEnemyMonster() == null || HasFaceupSkull()) return false;
            AI.SelectMaterials(new[] { CardId.Trias, CardId.Lo });
            return true;
        }

        private bool VoicelessSpellSet()
        {
            return Card.IsTrap() || Card.IsCode(CardId.CalledBy);
        }

        private IList<ClientCard> SelectVoicelessCards(IList<ClientCard> cards, int min, int max,
            IEnumerable<ClientCard> preferred, int desired = 1)
        {
            List<ClientCard> ranked = preferred.Where(c => c != null && cards.Contains(c)).Distinct().ToList();
            ranked.AddRange(cards.Where(c => !ranked.Contains(c)));
            return ranked.Take(Math.Min(max, Math.Max(min, desired))).ToList();
        }

        private IList<ClientCard> SelectVoicelessIds(IList<ClientCard> cards, int min, int max, params int[] ids)
        {
            List<ClientCard> ranked = new List<ClientCard>();
            foreach (int id in ids)
                ranked.AddRange(cards.Where(c => c.IsCode(id) && !ranked.Contains(c)));
            return SelectVoicelessCards(cards, min, max, ranked);
        }

        private IEnumerable<ClientCard> RitualMaterials(IList<ClientCard> cards)
        {
            // Prefer Lo's alternate Ritual level while revival is still available.
            // Once it is spent, keep a live Lo beside Skull Guardian if another
            // offered tribute can finish the summon.
            return cards.OrderBy(c => c.IsCode(CardId.Lo)
                    ? (c.Location == CardLocation.Hand || !c.IsDisabled()
                        && (!VoicelessEffectUsed(CardId.Lo, 1) || !HasFaceupSkull()) ? 0 : 5)
                    : c.IsCode(CardId.Trias) ? 1 : c.IsCode(CardId.Diviner) ? 2
                    : c.IsCode(CardId.VVSkullGuard) ? 9 : 3)
                .ThenByDescending(c => c.Level);
        }

        public override IList<ClientCard> OnSelectCard(IList<ClientCard> cards, int min, int max, int hint, bool cancelable)
        {
            int source = VoicelessPromptSource();
            if (cards.Count == 0) return base.OnSelectCard(cards, min, max, hint, cancelable);
            if (hint == HintMsg.LinkMaterial && _pendingVoicelessSummon != 0)
            {
                if (min == 0 && cancelable) return new List<ClientCard>();
                int[] ids = _pendingVoicelessSummon == CardId.SecureGardna ? new[] { CardId.Linkuriboh }
                    : _pendingVoicelessSummon == CardId.Linkuriboh ? new[] { CardId.Lo }
                    : new[] { CardId.Lo, CardId.VVSkullGuard, CardId.Sauravis };
                IList<ClientCard> chosen = SelectVoicelessIds(cards, min, max, ids);
                foreach (ClientCard material in chosen) _selectedLinkMaterials.Add(material);
                return chosen;
            }
            if (source == CardId.VVSauravis && hint == HintMsg.ToDeck)
            {
                if (min == 0 && cancelable) return new List<ClientCard>();
                // Select the Ritual Spell first; subgroup offers then enforce that
                // exactly two Spells including a Ritual Spell are returned.
                return SelectVoicelessCards(cards, min, max, cards.OrderBy(c =>
                    c.IsSpell() && c.HasType(CardType.Ritual) ? 0 : c.Location == CardLocation.Grave ? 1 : 2));
            }
            if (source == CardId.Trias && hint == HintMsg.Release)
            {
                if (min == 0 && cancelable) return new List<ClientCard>();
                return SelectVoicelessIds(cards, min, max, CardId.Diviner);
            }
            if ((source == CardId.Saffira || source == CardId.VVPrayer || source == CardId.Blessing)
                && hint == HintMsg.Release)
            {
                if (min == 0 && cancelable) return new List<ClientCard>();
                return SelectVoicelessCards(cards, min, max, RitualMaterials(cards));
            }
            if (source == CardId.Lo && hint == HintMsg.ToField)
                return SelectVoicelessIds(cards, min, max, GetVoicelessSearch(CardId.Lo),
                    CardId.VVBarrier, CardId.VVRadiance, CardId.Blessing);
            if (source == CardId.VVBarrier && hint == HintMsg.AddToHand)
                return SelectVoicelessIds(cards, min, max, GetVoicelessSearch(CardId.VVBarrier),
                    CardId.Saffira, CardId.Lo, CardId.Blessing, CardId.VVRadiance);
            if (source == CardId.Saffira && hint == HintMsg.ToGrave)
                return SelectVoicelessIds(cards, min, max, CardId.VVPrayer);
            if (source == CardId.Saffira && hint == HintMsg.AddToHand)
                return SelectVoicelessIds(cards, min, max, GetVoicelessSearch(CardId.Saffira),
                    HasFaceupSkull() || Bot.HasInHand(CardId.VVSkullGuard) ? CardId.Sauravis : CardId.VVSkullGuard,
                    CardId.VVSkullGuard, CardId.Sauravis);
            if (source == CardId.VVSkullGuard && hint == HintMsg.AddToHand)
                return SelectVoicelessIds(cards, min, max, GetVoicelessSearch(CardId.VVSkullGuard),
                    HasFaceupLo() || Bot.HasInGraveyard(CardId.Lo) ? CardId.Sauravis : CardId.Lo,
                    CardId.VVSauravis, CardId.Saffira);
            if (source == CardId.PrePrep && hint == HintMsg.AddToHand)
                return SelectVoicelessIds(cards, min, max, CardId.VVPrayer,
                    HasFaceupSkull() || Bot.HasInHand(CardId.VVSkullGuard) ? CardId.Sauravis : CardId.VVSkullGuard,
                    CardId.VVSkullGuard, CardId.Sauravis);
            if (source == CardId.Herald && hint == HintMsg.AddToHand)
                return SelectVoicelessIds(cards, min, max, GetVoicelessSearch(CardId.Herald),
                    !Bot.HasInHand(CardId.VVPrayer) && !Bot.HasInGraveyard(CardId.Saffira) ? CardId.VVPrayer : CardId.VVSkullGuard,
                    CardId.Sauravis);
            if (source == CardId.Diviner && hint == HintMsg.ToGrave)
            {
                int mill = !HasFaceupLo() && !Bot.HasInGraveyard(CardId.Trias) && !Bot.HasInHand(CardId.Trias)
                    && Bot.HasInDeck(CardId.Trias) ? CardId.Trias
                    : GetProblematicEnemyCardList(true).Count > 0 ? CardId.Ntss : CardId.Herald;
                return SelectVoicelessIds(cards, min, max, mill, CardId.Herald, CardId.Trias);
            }
            if (source == CardId.Diviner && hint == HintMsg.SpSummon)
                return SelectVoicelessIds(cards, min, max, CardId.Lo);
            if (source == CardId.Blessing && hint == HintMsg.AddToHand)
                return SelectVoicelessIds(cards, min, max,
                    !HasFaceupLo() && !Bot.HasInHand(CardId.Lo) ? CardId.Lo : CardId.Saffira,
                    CardId.Saffira, CardId.VVPrayer, CardId.Lo, CardId.VVRadiance);
            if ((source == CardId.Saffira || source == CardId.VVPrayer || source == CardId.Blessing
                || source == CardId.VVSauravis) && hint == HintMsg.SpSummon)
                return SelectVoicelessIds(cards, min, max, HasFaceupSkull() ? CardId.Sauravis : CardId.VVSkullGuard,
                    CardId.VVSkullGuard, CardId.Sauravis);
            if (source == CardId.DynaMondo)
            {
                if (cards.Any(c => c.Controller == 1))
                    return SelectVoicelessCards(cards, min, max, GetNormalEnemyTargetList(true));
                return SelectVoicelessIds(cards, min, max, HasFaceupSkull() ? CardId.Sauravis : CardId.VVSkullGuard,
                    CardId.VVSkullGuard, CardId.Sauravis);
            }
            if (source == CardId.VVRadiance)
            {
                if (hint == HintMsg.Destroy)
                    return SelectVoicelessCards(cards, min, max, GetNormalEnemyTargetList(true, false, CardType.Trap), max);
                if (hint == HintMsg.ToDeck)
                    return SelectVoicelessCards(cards, min, max,
                        cards.OrderBy(c => c.Location == CardLocation.Grave ? 0 : 1).ThenBy(c => c.IsSpell() ? 0 : 1));
                if (hint == HintMsg.OperateCard || hint == HintMsg.SpSummon || hint == HintMsg.AddToHand)
                {
                    IList<ClientCard> chosen = SelectVoicelessIds(cards, min, max,
                        !HasFaceupLo() ? CardId.Lo : CardId.VVSauravis, CardId.Saffira, CardId.Lo);
                    _radianceSelectedId = chosen.Count > 0 ? chosen[0].Id : 0;
                    return chosen;
                }
            }
            return base.OnSelectCard(cards, min, max, hint, cancelable);
        }

        public override IList<ClientCard> OnSelectRitualTribute(IList<ClientCard> cards,
            IList<ClientCard> mandatoryCards, int sum, int min, int max, bool exactEqual)
        {
            int source = VoicelessPromptSource();
            if (source != CardId.Saffira && source != CardId.VVPrayer && source != CardId.Blessing)
                return base.OnSelectRitualTribute(cards, mandatoryCards, sum, min, max, exactEqual);
            // GameAI validates the server-supplied alternate ritual levels and
            // fills a valid sum if this preference alone is not sufficient.
            return RitualMaterials(cards).Take(Math.Max(1, min)).ToList();
        }

        public override int OnSelectOption(IList<int> options)
        {
            int source = VoicelessPromptSource();
            if (source == CardId.DynaMondo || source == CardId.VVRadiance)
            {
                int wanted = source == CardId.VVRadiance && _radianceSelectedId == CardId.Saffira ? 1190 : 1152;
                int index = options.IndexOf(wanted);
                if (index >= 0) return index;
            }
            return base.OnSelectOption(options);
        }

        public override bool OnSelectYesNo(int desc)
        {
            if (VoicelessPromptSource() == CardId.Saffira && desc == Util.GetStringId(CardId.Saffira, 1)) return true;
            return base.OnSelectYesNo(desc);
        }

        public override CardPosition OnSelectPosition(int cardId, IList<CardPosition> positions)
        {
            if ((cardId == CardId.Lo || cardId == CardId.Diviner) && positions.Contains(CardPosition.FaceUpDefence))
                return CardPosition.FaceUpDefence;
            if (positions.Contains(CardPosition.FaceUpAttack)) return CardPosition.FaceUpAttack;
            return base.OnSelectPosition(cardId, positions);
        }

        public override void OnChaining(int player, ClientCard card)
        {
            if (player == 0 && card != null && card.Id == _acceptedActivationId) _acceptedActivationId = 0;
            base.OnChaining(player, card);
        }

        public override void OnSpSummoning()
        {
            _pendingVoicelessSummon = 0;
            _selectedLinkMaterials.Clear();
            base.OnSpSummoning();
        }

        public override void OnChainEnd()
        {
            _acceptedActivationId = 0;
            _radianceSelectedId = 0;
            currentNegateMonsterList.Clear();
            currentDestroyCardList.Clear();
            escapeTargetList.Clear();
            summonInChainList.Clear();
            chainSummoningIdList.Clear();
            OnVoicelessPlannerReset();
            base.OnChainEnd();
        }

        public override void OnNewTurn()
        {
            _voicelessUsed.Clear();
            _normalSummonUsed = false;
            _pendingVoicelessSummon = 0;
            _acceptedActivationId = 0;
            _radianceSelectedId = 0;
            _selectedLinkMaterials.Clear();
            activatedCardIdList.Clear();
            currentNegateMonsterList.Clear();
            currentDestroyCardList.Clear();
            setTrapThisTurn.Clear();
            summonThisTurn.Clear();
            enemySetThisTurn.Clear();
            escapeTargetList.Clear();
            summonInChainList.Clear();
            chainSummoningIdList.Clear();
            infiniteImpermanenceList.Clear();
            CrossoutDesignatorTarget = 0;
            foreach (int id in CalledbytheGraveCount.Keys.ToList())
            {
                CalledbytheGraveCount[id]--;
                if (CalledbytheGraveCount[id] <= 0) CalledbytheGraveCount.Remove(id);
            }
            OnVoicelessPlannerReset();
            base.OnNewTurn();
        }

        public override void OnNewPhase()
        {
            _pendingVoicelessSummon = 0;
            _acceptedActivationId = 0;
            _selectedLinkMaterials.Clear();
            OnVoicelessPlannerReset();
            base.OnNewPhase();
        }

        private bool DefaultNoExecutor()
        {
            foreach (CardExecutor exec in Executors)
            {
                if (exec.Type == Type && Card.IsOriginalCode(exec.CardId))
                    return false;
            }
            return true;
        }

        private int CompareUsableAttack(ClientCard a, ClientCard b) 
        {
            return a.Attack.CompareTo(b.Attack);
        }

        private bool EmptyMainMonsterZone()
        {
            return Bot.MonsterZone.Any(c => c !=  null && c.Data != null);
        }

        public bool SpellNegatable(bool isCounter = false, ClientCard target = null)
        {
            // target default set
            if (target == null) target = Card;
            // won't negate if not on field
            if (target.Location != CardLocation.SpellZone && target.Location != CardLocation.Hand) return false;

            // negate judge
            if (Enemy.HasInMonstersZone(CardId.NaturalExterio, true) && !isCounter) return true;
            if (target.IsSpell())
            {
                if (Enemy.HasInMonstersZone(CardId.NaturalBeast, true)) return true;
                if (Enemy.HasInSpellZone(CardId.ImperialOrder, true) || Bot.HasInSpellZone(CardId.ImperialOrder, true)) return true;
                if (Enemy.HasInMonstersZone(CardId.SwordsmanLV7, true) || Bot.HasInMonstersZone(CardId.SwordsmanLV7, true)) return true;
            }
            if (target.IsTrap())
            {
                if (Enemy.HasInSpellZone(CardId.RoyalDecree, true) || Bot.HasInSpellZone(CardId.RoyalDecree, true)) return true;
            }
            if (target.Location == CardLocation.SpellZone && (target.IsSpell() || target.IsTrap()))
            {
                int selfSeq = -1;
                for (int i = 0; i < 5; ++i)
                {
                    if (Bot.SpellZone[i] == Card) selfSeq = i;
                }
                if (infiniteImpermanenceList.Contains(selfSeq))
                {
                    return true;
                }
            }
            // how to get here?
            return false;
        }

    }
}
