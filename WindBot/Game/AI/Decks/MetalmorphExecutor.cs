using System;
using System.Collections.Generic;
using System.Linq;
using YGOSharp.OCGWrapper.Enums;
using WindBot;
using WindBot.Game;
using WindBot.Game.AI;

namespace WindBot.Game.AI.Decks
{
    [Deck("Metalmorph", "AI_Metalmorph")]
    class MetalmorphExecutor : DefaultExecutor
    {
        // ============================================================
        // Card IDs (source of truth: compiled_deck_cards.txt)
        // ============================================================
        public class CardId
        {
            // Main Deck
            public const int RedEyesDarknessMetalDragon = 88264978;
            public const int RedEyesBlackFullmetalDragon = 80870883;
            public const int MetalzoaX = 27268998;
            public const int TheBystialLubellion = 32731036;
            public const int ZoaTheFiendishBeast = 53753697;
            public const int FidraulisHarmonia = 70088809;
            public const int RedEyesMetalClawsDragon = 83711531;
            public const int MetalflameSwordsman = 27704731;
            public const int BystialMagnamhut = 33854624;
            public const int BystialDruiswurm = 6637331;
            public const int AshBlossomJoyousSpring = 14558127;
            public const int MaxxC = 23434538;
            public const int HopEarSquadron = 1980574;
            public const int BlackMetalDragon = 93969023;
            public const int MetalIllusionist = 29157292;

            // Spells
            public const int IncomingMachine = 94661166;
            public const int BrandedRegained = 34090915;
            public const int HeavyMetalRaiders = 3113667;
            public const int CalledByTheGrave = 24224830;
            public const int CrossoutDesignator = 65681983;

            // Traps
            public const int TimeEngine = 15216188;
            public const int FlameCoatingMetalmorph = 91152455;
            public const int MaxMetalmorph = 89812483;
            public const int InfiniteImpermanence = 10045474;

            // Extra Deck
            public const int HieraticSealOfTheHeavenlySpheres = 24361622;
            public const int StrikerDragon = 73539069;
            public const int AccesscodeTalker = 86066372;
            public const int KnightmarePhoenix = 2857636;
            public const int DharcTheDarkCharmerGloomy = 8264361;
            public const int SPLittleKnight = 29301450;
            public const int StardustWarrior = 74892653;
            public const int BaronneDeFleur = 84815190;
            public const int SatelliteWarrior = 84664085;
            public const int JunkWarriorExtreme = 16327715;
            public const int AccelSynchroStardustDragon = 30983281;
            public const int StardustDragon = 44508094;
            public const int DracoBerserkerOfTheTenyi = 5041348;
            public const int StardustDragonVictimSanctuary = 76636978;
        }

        // metallization condition for Swordsman (Lv5+ Warrior tribute)
        private bool _lastMetallizationWasWarrior5p = false;
        private CardRace _lastMetallizeRace = (CardRace)0;
        private int _lastMetallizeLevel = 0;
        private int _lastMetallizeTributeCode = 0;

        // Reset Matrix
        // ────────────────────────────────────────────────────────────────
        // Per-turn reset (OnNewTurn):
        //   - _impermDangerColumns
        //   - _calledByUsedThisChain, _lastOwnLikelyDestroyedMonster, _tributedThisTurn
        //   - _stardustDragonTributedThisTurn, _stardustWarriorNegateUsedThisTurn, _stardustWarriorLeftByOpponentThisTurn
        //   - _preferSatelliteFromAccel
        //   - _pendingHopEarTargetId, _pendingHopEarTargetLevel, _pendingHopEarPreferBaronne, _pendingHopEarPreferAccel
        //   - _pendingVictimSanctuarySummonPrompt
        //   - currentNegateCardList, enemyPlaceThisTurn, summonThisTurn, placedThisTurn
        // Per-chain reset (OnChainEnd):
        //   - _pendingEnemyImpermMirrorColumn, _pendingEnemyImpermFromHand, _pendingOppTurnMetallizeId
        //   - _pendingHopEarTargetId, _pendingHopEarTargetLevel, _pendingHopEarPreferBaronne, _pendingHopEarPreferAccel
        //   - _pendingVictimSanctuarySummonPrompt
        // Prune on ChainEnd:
        //   - enemyPlaceThisTurn, summonThisTurn, placedThisTurn
        private bool _calledByUsedThisChain = false;
        private int _lastOwnLikelyDestroyedMonster = 0;
        private bool _tributedThisTurn = false;
        private bool _stardustDragonTributedThisTurn = false;
        private bool _stardustWarriorNegateUsedThisTurn = false;
        private bool _stardustWarriorLeftByOpponentThisTurn = false;
        private bool _preferSatelliteFromAccel = false;

        // PURPOSE: remember the chosen Hop Ear body long enough to route the immediate Synchro prompt deterministically.
        // TRUSTS: Hop Ear target prompt resolves before its follow-up Synchro choice prompt in the same chain.
        // RESET: clear on OnChainEnd and OnNewTurn.
        private int _pendingHopEarTargetId = 0;
        private int _pendingHopEarTargetLevel = 0;
        private bool _pendingHopEarPreferBaronne = false;
        private bool _pendingHopEarPreferAccel = false;
        private bool _pendingFidraulisRevealPrompt = false;
        private bool _pendingFidraulisSendPrompt = false;
        private bool _pendingFidraulisDestroyPrompt = false;
        private int _pendingFidraulisRevealDesiredCount = 0;
        private readonly List<int> _pendingFidraulisRevealPickedIds = new List<int>();
        private bool _pendingVictimSanctuarySummonPrompt = false;

        // Magnamhut registers a non-chain End Phase search only after its summon trigger resolves.
        // The registered effect survives its source leaving the field and expires at this turn's end.
        private bool _pendingMagnamhutEndPhaseQueuePrompt = false;

        // Keep accepted action context until authoritative chaining/summon events.
        // Rejected candidates must not replace pending non-chain material ownership.
        private ChainInfo _acceptedPromptSource;

        // Opponent-turn Metallize budget (Max/Flame activations on opponent turn)
        private int _oppTurnMetallizeResolvedCount = 0;
        private int _pendingOppTurnMetallizeId = 0;

        // Enemy Imperm tracking (Odion gold baseline): infer on OnChaining, commit on OnChainSolved.
        private readonly HashSet<int> _impermDangerColumns = new HashSet<int>();
        private bool _enemyActivateInfiniteImpermanenceFromHand = false;
        private int _pendingEnemyImpermMirrorColumn = -1;
        private bool _pendingEnemyImpermFromHand = false;

        // Generic helper baselines from recent golden executors.
        private readonly List<int> NotToNegateIdList = new List<int>
        {
            58699500, 20343502, 25451383, 19403423
        };
        private readonly List<int> solemnReportBanishIdList = new List<int>
        {
            _CardId.MysticalSpaceTyphoon, 63166095, 9726840, 5380979, 92714517,
            6153210, 32548318, 30271097, 45171524, 81560239
        };
        private int dimensionShifterCount = 0;
        private readonly List<ClientCard> currentNegateCardList = new List<ClientCard>();
        private readonly List<ClientCard> enemyPlaceThisTurn = new List<ClientCard>();
        private readonly List<ClientCard> summonThisTurn = new List<ClientCard>();
        private readonly List<ClientCard> placedThisTurn = new List<ClientCard>();

        // never use as material/cost unless explicitly selected
        private static readonly HashSet<int> _neverUseAsMat = new HashSet<int>()
        {
            CardId.RedEyesBlackFullmetalDragon,
            CardId.MetalzoaX,
            CardId.RedEyesMetalClawsDragon,
            CardId.HieraticSealOfTheHeavenlySpheres,
            CardId.RedEyesDarknessMetalDragon
        };

        // Deck count table (exact current list).
        private readonly Dictionary<int, List<int>> DeckCountTable = new Dictionary<int, List<int>>
        {
            {
                3, new List<int>
                {
                    CardId.ZoaTheFiendishBeast, CardId.BlackMetalDragon, CardId.MetalIllusionist,
                    CardId.IncomingMachine, CardId.FlameCoatingMetalmorph, CardId.MaxMetalmorph
                }
            },
            {
                2, new List<int>
                {
                    CardId.RedEyesBlackFullmetalDragon, CardId.RedEyesMetalClawsDragon,
                    CardId.AshBlossomJoyousSpring, CardId.MaxxC, CardId.CalledByTheGrave,
                    CardId.CrossoutDesignator, CardId.InfiniteImpermanence,
                    CardId.HieraticSealOfTheHeavenlySpheres, CardId.HopEarSquadron
                }
            },
            {
                1, new List<int>
                {
                    CardId.RedEyesDarknessMetalDragon, CardId.MetalzoaX, CardId.MetalflameSwordsman,
                    CardId.TheBystialLubellion, CardId.BystialMagnamhut, CardId.BystialDruiswurm,
                    CardId.FidraulisHarmonia,
                    CardId.BrandedRegained, CardId.HeavyMetalRaiders, CardId.TimeEngine,
                    CardId.StrikerDragon, CardId.SPLittleKnight,
                    CardId.StardustWarrior, CardId.BaronneDeFleur, CardId.SatelliteWarrior,
                    CardId.JunkWarriorExtreme, CardId.AccelSynchroStardustDragon, CardId.StardustDragon,
                    CardId.DracoBerserkerOfTheTenyi, CardId.StardustDragonVictimSanctuary
                }
            }
        };

        public int CheckRemainInDeck(int id)
        {
            for (int count = 1; count < 4; ++count)
            {
                if (DeckCountTable[count].Contains(id))
                {
                    return Bot.GetCardCountInDeck(id);
                }
            }
            return 0;
        }

        public int CheckRemainInDeck(params int[] ids)
        {
            int sum = 0;
            foreach (int id in ids)
            {
                sum += CheckRemainInDeck(id);
            }
            return sum;
        }

        // ============================================================
        // Constructor — executor priority stack
        // ============================================================
        public MetalmorphExecutor(GameAI ai, Duel duel) : base(ai, duel)
        {
            // ============================================================
            // 1) Global interaction / handtraps
            // ============================================================
            AddExecutor(ExecutorType.Activate, CardId.AshBlossomJoyousSpring, AshBlossomActivate);
            AddExecutor(ExecutorType.Activate, CardId.MaxxC, MaxxC_Activate);
            AddExecutor(ExecutorType.Activate, CardId.FidraulisHarmonia, FidraulisHarmonia_Activate);
            AddExecutor(ExecutorType.Activate, CardId.InfiniteImpermanence, InfiniteImpermanence_Smart);
            AddExecutor(ExecutorType.Activate, CardId.CalledByTheGrave, CalledBy_Effect);
            AddExecutor(ExecutorType.Activate, CardId.CrossoutDesignator, CrossoutDesignatorActivate);

            // ============================================================
            // 2) Quick effects / reactive engine
            // ============================================================
            AddExecutor(ExecutorType.Activate, CardId.HieraticSealOfTheHeavenlySpheres, Seal_Bounce_FreeChain);
            AddExecutor(ExecutorType.Activate, CardId.MaxMetalmorph, MaxMetalmorph_OppTurn_Bystial);
            AddExecutor(ExecutorType.Activate, CardId.MaxMetalmorph, MaxMetalmorph_OppTurn_IllusionistOrZoa);
            AddExecutor(ExecutorType.Activate, CardId.MaxMetalmorph, MaxMetalmorph_OppTurn_Generic);
            AddExecutor(ExecutorType.Activate, CardId.FlameCoatingMetalmorph, FlameCoating_OppTurn_Generic);
            AddExecutor(ExecutorType.Activate, CardId.FlameCoatingMetalmorph, Flame_DestroyReplace);
            AddExecutor(ExecutorType.Activate, CardId.MetalzoaX, MetalzoaX_QuickPop);
            AddExecutor(ExecutorType.Activate, CardId.RedEyesBlackFullmetalDragon, Fullmetal_Negate);
            AddExecutor(ExecutorType.Activate, CardId.HopEarSquadron, HopEarSquadron_Activate);
            AddExecutor(ExecutorType.Activate, CardId.AccelSynchroStardustDragon, AccelSynchroStardustDragon_Activate);
            AddExecutor(ExecutorType.Activate, CardId.StardustDragon, StardustDragon_Activate);
            AddExecutor(ExecutorType.Activate, CardId.StardustWarrior, StardustWarrior_Activate);
            AddExecutor(ExecutorType.Activate, CardId.SatelliteWarrior, SatelliteWarrior_Activate);
            AddExecutor(ExecutorType.Activate, CardId.BaronneDeFleur, BaronneDeFleur_Negate);
            AddExecutor(ExecutorType.Activate, CardId.BaronneDeFleur, BaronneDeFleur_Destroy);
            AddExecutor(ExecutorType.Activate, CardId.StardustDragonVictimSanctuary, VictimSanctuary_Activate);
            AddExecutor(ExecutorType.Activate, CardId.DracoBerserkerOfTheTenyi, DracoBerserkerOfTheTenyi_Activate);
            AddExecutor(ExecutorType.Activate, CardId.JunkWarriorExtreme, JunkWarriorExtreme_Activate);
            AddExecutor(ExecutorType.Activate, CardId.SPLittleKnight, SPLittleKnight_QuickBanish);

            // ============================================================
            // 3) Primary engine starters
            // ============================================================
            AddExecutor(ExecutorType.Summon, CardId.MetalIllusionist, Summon_Illusionist);
            AddExecutor(ExecutorType.Activate, CardId.MetalIllusionist, Illusionist_SetMetalmorph);
            AddExecutor(ExecutorType.Activate, CardId.MetalIllusionist, Illusionist_DeclareDragon);
            AddExecutor(ExecutorType.Activate, CardId.ZoaTheFiendishBeast, Zoa_SpecialFromHand);
            AddExecutor(ExecutorType.Summon, CardId.ZoaTheFiendishBeast, Summon_Zoa_NoTributeIfOppHasMonster);
            AddExecutor(ExecutorType.Activate, CardId.ZoaTheFiendishBeast, Zoa_SetMetalmorphFromDeck);

            AddExecutor(ExecutorType.Summon, CardId.BlackMetalDragon, BMD_Summon);
            AddExecutor(ExecutorType.SpSummon, CardId.StrikerDragon, BMD_Link1_Striker);
            AddExecutor(ExecutorType.Activate, CardId.BlackMetalDragon, BMD_Grave_Search);
            AddExecutor(ExecutorType.Activate, CardId.BlackMetalDragon, BMD_Equip_ToRedEyes);
            AddExecutor(ExecutorType.SpSummon, CardId.RedEyesDarknessMetalDragon, REDMD_SpecialFromHand);
            AddExecutor(ExecutorType.Activate, CardId.RedEyesDarknessMetalDragon, REDMD_Revive);
            AddExecutor(ExecutorType.SpSummon, CardId.HieraticSealOfTheHeavenlySpheres, Seal_Summon);

            AddExecutor(ExecutorType.Activate, CardId.IncomingMachine, IncomingMachine_Use);
            AddExecutor(ExecutorType.Activate, CardId.HeavyMetalRaiders, HeavyMetalRaiders_Activate);

            // ============================================================
            // 4) Extension & payoff
            // ============================================================
            AddExecutor(ExecutorType.Activate, CardId.RedEyesMetalClawsDragon, Claws_SS_FromHand);
            AddExecutor(ExecutorType.Activate, CardId.RedEyesMetalClawsDragon, Claws_AddMetalmorph_OnSummon);
            AddExecutor(ExecutorType.Activate, CardId.MaxMetalmorph, MaxMetalmorph_MyTurn_Claws);
            AddExecutor(ExecutorType.Activate, CardId.MaxMetalmorph, MaxMetalmorph_MyTurn_Generic);
            AddExecutor(ExecutorType.Activate, CardId.FlameCoatingMetalmorph, FlameCoating_MyTurn_Generic);
            AddExecutor(ExecutorType.Activate, CardId.MetalzoaX, MetalzoaX_HandRevealSet);
            AddExecutor(ExecutorType.Activate, CardId.RedEyesBlackFullmetalDragon, Fullmetal_SetMetalmorph);
            AddExecutor(ExecutorType.Activate, CardId.MetalflameSwordsman, Swordsman_RevealAdd);
            AddExecutor(ExecutorType.Activate, CardId.BystialMagnamhut, BystialMagnamhut_Activate);
            AddExecutor(ExecutorType.Activate, CardId.BystialDruiswurm, BystialDruiswurm_Activate);
            AddExecutor(ExecutorType.Activate, CardId.TheBystialLubellion, TheBystialLubellion_Activate);
            AddExecutor(ExecutorType.SpSummon, CardId.TheBystialLubellion, TheBystialLubellion_GraveSummon);
            AddExecutor(ExecutorType.Activate, CardId.BrandedRegained, BrandedRegained_Activate);
            AddExecutor(ExecutorType.SpSummon, CardId.SPLittleKnight, SPLittleKnight_Summon);
            AddExecutor(ExecutorType.Activate, CardId.SPLittleKnight, SPLittleKnight_BanishOnSummon);
            AddExecutor(ExecutorType.SpSummon, CardId.DharcTheDarkCharmerGloomy, Dharc_Summon);
            AddExecutor(ExecutorType.Activate, CardId.DharcTheDarkCharmerGloomy, Dharc_Activate);
            AddExecutor(ExecutorType.SpSummon, CardId.DracoBerserkerOfTheTenyi, DracoBerserkerOfTheTenyi_Summon);
            AddExecutor(ExecutorType.SpSummon, CardId.KnightmarePhoenix, KnightmarePhoenix_Summon);
            AddExecutor(ExecutorType.Activate, CardId.KnightmarePhoenix, KnightmarePhoenix_Pop);
            AddExecutor(ExecutorType.SpSummon, CardId.AccesscodeTalker, Accesscode_Summon);
            AddExecutor(ExecutorType.Activate, CardId.AccesscodeTalker, Accesscode_AtkUp);
            AddExecutor(ExecutorType.Activate, CardId.AccesscodeTalker, Accesscode_Destroy);
            AddExecutor(ExecutorType.Activate, CardId.TimeEngine, TimeEngine_Activate);

            // ============================================================
            // 5) Delayed / cleanup activations
            // ============================================================
            AddExecutor(ExecutorType.GoToMainPhase2, SkipBattleWhenNoPositiveTrades);
            AddExecutor(ExecutorType.GoToEndPhase, GoToEndPhase_HoldMetalmorph);

            // ============================================================
            // 6) Housekeeping
            // ============================================================
            AddExecutor(ExecutorType.SpellSet, SpellSet_Smart);

            for (int i = 0; i < Executors.Count; ++i)
            {
                CardExecutor registered = Executors[i];
                if (registered.Type != ExecutorType.Activate && registered.Type != ExecutorType.SpSummon) continue;
                Executors[i] = new CardExecutor(registered.Type, registered.CardId, () =>
                {
                    bool accepted = registered.Func == null || registered.Func();
                    if (accepted) _acceptedPromptSource = new ChainInfo(Card, 0, ActivateDescription);
                    return accepted;
                });
            }
        }

        // ============================================================
        // Small helpers
        // ============================================================
        private bool HasSet(int id) => Bot.SpellZone.Any(s => s != null && s.IsFacedown() && s.IsCode(id));
        private bool HasFaceup(int id) => Bot.SpellZone.Any(s => s != null && s.IsFaceup() && s.IsCode(id));
        private bool IHaveRoomST() => Bot.SpellZone.Any(z => z == null);
        private bool HasMetalmorphTrapBoardCoverage(int id) => HasSet(id) || HasFaceup(id);
        private bool IsDarkMachine(ClientCard c) => c != null && c.IsMonster() && c.HasRace(CardRace.Machine) && c.HasAttribute(CardAttribute.Dark);
        private bool IsLightOrDarkMonster(ClientCard c) => c != null && c.IsMonster() && (c.HasAttribute(CardAttribute.Light) || c.HasAttribute(CardAttribute.Dark));
        private bool IsSettableSpellTrap(ClientCard c) => c != null && (c.IsSpell() || c.IsTrap());
        private bool HasMetalmorphTrapCoverage(int id)
            => HasSet(id) || HasFaceup(id) || Bot.Hand.Any(c => c != null && c.IsCode(id));
        private bool HavePrimaryBossOnField()
            => Bot.HasInMonstersZone(CardId.RedEyesBlackFullmetalDragon, true)
            || Bot.HasInMonstersZone(CardId.MetalzoaX, true)
            || Bot.HasInMonstersZone(CardId.MetalflameSwordsman, true);
        private bool HaveTimeEngineCoverage()
            => HasSet(CardId.TimeEngine) || HasFaceup(CardId.TimeEngine) || Bot.HasInHand(CardId.TimeEngine);

        // PURPOSE: when at least one boss is established, prioritize Time Engine from hand-based Metalmorph routes.
        // TRUSTS: Time Engine improves board stickiness and follow-up once we already have pressure.
        private bool ShouldPrioritizeTimeEngineNow()
        {
            if (!HavePrimaryBossOnField()) return false;
            if (HaveTimeEngineCoverage()) return false;
            return CheckRemainInDeck(CardId.TimeEngine) > 0
                || Bot.Graveyard.Any(c => c != null && c.IsCode(CardId.TimeEngine));
        }

        // PURPOSE: decide whether we should keep investing hand/field Fullmetal into trap setup this turn.
        // TRUSTS: additional trap setup is highest value when coverage is missing or Claws needs facedown fuel.
        // RESET: stateless helper.
        private bool WantsMetalmorphTrapSetupNow()
        {
            bool needTrapCoverage = !HasMetalmorphTrapBoardCoverage(CardId.MaxMetalmorph)
                                 || !HasMetalmorphTrapBoardCoverage(CardId.FlameCoatingMetalmorph)
                                 || !HasMetalmorphTrapBoardCoverage(CardId.TimeEngine);
            bool wantTimeProtect = ShouldPrioritizeTimeEngineNow();
            bool wantClawsBridge = Bot.HasInHand(CardId.RedEyesMetalClawsDragon) && !HasFacedownSendableForClaws();
            bool handLimitPush = ShouldSetForHandLimit();
            return needTrapCoverage || wantTimeProtect || wantClawsBridge || handLimitPush;
        }

        // PURPOSE: keep core bystial/dragon engines in GY for follow-up lines unless forced.
        private bool IsProtectedOwnBystialBanishCost(ClientCard c)
        {
            if (c == null) return true;
            // TRUSTS: Bystials are now expendable fuel because Branded Regained can recycle them.
            return c.IsCode(CardId.RedEyesDarknessMetalDragon)
                || c.IsCode(CardId.RedEyesBlackFullmetalDragon)
                || c.IsCode(CardId.MetalzoaX)
                || c.IsCode(CardId.RedEyesMetalClawsDragon)
                || c.IsCode(CardId.ZoaTheFiendishBeast)
                || c.IsCode(CardId.MetalflameSwordsman)
                || c.IsCode(CardId.BlackMetalDragon);
        }

        private List<ClientCard> GetBystialBanishTargets()
        {
            // 1) Always prefer opponent GY targets.
            var targets = Enemy.Graveyard
                .Where(c => c != null && IsLightOrDarkMonster(c))
                .OrderByDescending(c => c.Attack)
                .ToList();

            // 2) Only use our GY when the card is disposable for current lines.
            //    Keep Lubellion last among our own targets so we do not strand the Branded line prematurely.
            targets.AddRange(Bot.Graveyard
                .Where(c => c != null && IsLightOrDarkMonster(c) && !IsProtectedOwnBystialBanishCost(c))
                .OrderBy(c => c.IsCode(CardId.TheBystialLubellion) ? 1 : 0)
                .ThenBy(c => c.Attack));

            return targets;
        }

        private List<ClientCard> GetRegainedRecycleTargets()
        {
            var result = new List<ClientCard>();
            int[] bystialIds = { CardId.BystialDruiswurm, CardId.BystialMagnamhut, CardId.TheBystialLubellion };

            foreach (int id in bystialIds)
            {
                var pick = Bot.Banished.FirstOrDefault(c => c != null && c.IsFaceup() && c.IsCode(id));
                if (pick != null && !result.Contains(pick)) result.Add(pick);
            }

            result.AddRange(Bot.Banished.Where(c => c != null && c.IsFaceup() && IsLightOrDarkMonster(c) && !result.Contains(c)));
            result.AddRange(Enemy.Banished.Where(c => c != null && c.IsFaceup() && IsLightOrDarkMonster(c) && !result.Contains(c))
                                          .OrderByDescending(c => c.Attack));
            return result;
        }

        private bool ShouldSetForHandLimit()
        {
            if (Duel.Player != 0) return false;
            if (Duel.Phase != DuelPhase.Main1 && Duel.Phase != DuelPhase.Main2) return false;
            if (!IHaveRoomST()) return false;
            return Bot.Hand.Count > 6;
        }

        private bool HasSettableForHandLimit()
        {
            if (!ShouldSetForHandLimit()) return false;
            return Bot.Hand.Any(IsSettableSpellTrap);
        }

        private List<T> ShuffleList<T>(List<T> list)
        {
            List<T> result = new List<T>(list ?? new List<T>());
            int n = result.Count;
            while (n-- > 1)
            {
                int index = Program.Rand.Next(result.Count);
                int nextIndex = (index + Program.Rand.Next(result.Count - 1)) % result.Count;
                T temp = result[index];
                result[index] = result[nextIndex];
                result[nextIndex] = temp;
            }
            return result;
        }

        private bool CheckCanBeTargeted(ClientCard card, bool canBeTarget, CardType selfType)
        {
            if (card == null) return true;
            if (canBeTarget)
            {
                if (card.IsShouldNotBeTarget()) return false;
                if (((int)selfType & (int)CardType.Monster) > 0 && card.IsShouldNotBeMonsterTarget()) return false;
                if (((int)selfType & (int)CardType.Spell) > 0 && card.IsShouldNotBeSpellTrapTarget()) return false;
                if (((int)selfType & (int)CardType.Trap) > 0 && card.IsShouldNotBeSpellTrapTarget()
                    && !card.IsDisabled()) return false;
            }
            return true;
        }

        private bool CheckWhetherNegated(bool disablecheck = true, bool toFieldCheck = false, CardType type = 0, bool ignore41 = false)
        {
            bool isMonster = type == 0 && Card != null && Card.IsMonster();
            isMonster |= (type & CardType.Monster) != 0;
            bool isSpellOrTrap = type == 0 && Card != null && (Card.IsSpell() || Card.IsTrap());
            isSpellOrTrap |= (type & (CardType.Spell | CardType.Trap)) != 0;
            bool isCounter = (type & CardType.Counter) != 0;
            if (isSpellOrTrap && toFieldCheck)
            {
                if (CheckSpellWillBeNegate(isCounter)) return true;
                if (Card != null && DefaultCheckWhetherSpellActivateWillBeNegated(Card)) return true;
            }
            if (Card != null && DefaultCheckWhetherCardIsNegated(Card)) return true;
            if (isMonster && Card != null && (toFieldCheck || Card.Location == CardLocation.MonsterZone))
            {
                if ((toFieldCheck && ((type & CardType.Link) != 0)) || Card.IsDefense())
                {
                    if (Enemy.MonsterZone.Any(mon => CheckNumber41(mon, ignore41))
                        || Bot.MonsterZone.Any(mon => CheckNumber41(mon, ignore41)))
                    {
                        return true;
                    }
                }
                if (Enemy.HasInSpellZone(_CardId.SkillDrain, true, true)) return true;
            }
            if (disablecheck && Card != null)
            {
                return (Card.Location == CardLocation.MonsterZone || Card.Location == CardLocation.SpellZone)
                    && Card.IsDisabled() && Card.IsFaceup();
            }
            return false;
        }

        private bool CheckNumber41(ClientCard card, bool ignoreSelf41 = false)
        {
            return card != null && card.IsFaceup()
                && card.IsCode(_CardId.Number41BagooskatheTerriblyTiredTapir)
                && card.IsDefense() && !card.IsDisabled()
                && (!ignoreSelf41 || card.Controller == 0);
        }

        private bool CheckSpellWillBeNegate(bool isCounter = false, ClientCard target = null)
        {
            if (target == null) target = Card;
            if (target == null) return false;
            if (target.Location != CardLocation.SpellZone && target.Location != CardLocation.Hand) return false;

            if (Enemy.HasInMonstersZone(_CardId.NaturalExterio, true) && !isCounter) return true;
            if (target.IsSpell())
            {
                if (Enemy.HasInMonstersZone(_CardId.NaturiaBeast, true)) return true;
                if (Enemy.HasInSpellZone(_CardId.ImperialOrder, true) || Bot.HasInSpellZone(_CardId.ImperialOrder, true)) return true;
                if (Enemy.HasInMonstersZone(_CardId.SwordsmanLV7, true) || Bot.HasInMonstersZone(_CardId.SwordsmanLV7, true)) return true;
            }
            if (target.IsTrap() && (Enemy.HasInSpellZone(_CardId.RoyalDecreel, true) || Bot.HasInSpellZone(_CardId.RoyalDecreel, true)))
                return true;

            if (target.Location == CardLocation.SpellZone && (target.IsSpell() || target.IsTrap()))
            {
                int seq = target.Sequence;
                if (target.Controller == 1) seq = 4 - seq;
                if (infiniteImpermanenceNegatedColumns.Contains(seq)) return true;
            }
            return false;
        }

        private bool CheckCardShouldNegate(ClientCard card)
        {
            if (card == null) return false;
            if (card.IsMonster() && card.HasSetcode(_Setcode.TimeLord) && Duel.Phase == DuelPhase.Standby) return false;
            if (NotToNegateIdList.Contains(card.Id)) return false;
            if (card.HasSetcode(_Setcode.Danger) && card.Location == CardLocation.Hand) return false;
            if (card.IsMonster() && card.Location == CardLocation.MonsterZone && card.HasPosition(CardPosition.Defence))
            {
                if (Enemy.MonsterZone.Any(c => CheckNumber41(c)) || Bot.MonsterZone.Any(c => CheckNumber41(c))) return false;
            }
            if (DefaultCheckWhetherCardIsNegated(card)) return false;
            if (card.Location == CardLocation.SpellZone)
            {
                int sequence = card.Sequence;
                if (card.Controller == 1) sequence = 4 - sequence;
                if (infiniteImpermanenceNegatedColumns.Contains(sequence)) return false;
            }
            if (card.IsCode(_CardId.MulcharmyPurulia, _CardId.MulcharmyFuwalos, _CardId.MulcharmyNyalus, _CardId.MaxxC)) return false;
            if (card.IsDisabled()) return false;
            if (solemnReportBanishIdList.Contains(card.Id)) return true;
            return true;
        }

        private bool CheckCardShouldNegate(ChainInfo chainInfo)
        {
            if (chainInfo == null) return false;
            ClientCard card = chainInfo.RelatedCard;
            if (card == null) return false;
            if (card.IsMonster() && card.HasSetcode(_Setcode.TimeLord) && Duel.Phase == DuelPhase.Standby) return false;
            if (NotToNegateIdList.Contains(card.Id)) return false;
            if (card.HasSetcode(_Setcode.Danger) && card.Location == CardLocation.Hand) return false;
            if (card.IsMonster() && chainInfo.HasLocation(CardLocation.MonsterZone) && chainInfo.HasPosition(CardPosition.Defence))
            {
                if (Enemy.MonsterZone.Any(c => CheckNumber41(c)) || Bot.MonsterZone.Any(c => CheckNumber41(c))) return false;
            }
            if (DefaultCheckWhetherCardIsNegated(card)) return false;
            if (Duel.Player == 1 && card.IsCode(_CardId.MulcharmyPurulia, _CardId.MulcharmyFuwalos, _CardId.MulcharmyNyalus)) return false;
            if (card.IsDisabled()) return false;
            if (solemnReportBanishIdList.Contains(card.Id)) return true;
            return true;
        }

        private bool CheckWhetherWillbeRemoved()
        {
            if (dimensionShifterCount > 0) return true;
            List<int> checkIdList = new List<int>
            {
                _CardId.BanisheroftheRadiance, _CardId.BanisheroftheLight,
                _CardId.MacroCosmos, _CardId.DimensionalFissure,
                _CardId.KashtiraAriseHeart, _CardId.MaskedHERODarkLaw
            };
            foreach (int cardid in checkIdList)
            {
                foreach (ClientField cf in new List<ClientField> { Bot, Enemy })
                {
                    if (cf.HasInMonstersZone(cardid, true, false, true) || cf.HasInSpellZone(cardid, true, true))
                        return true;
                }
            }
            return false;
        }

        private int CompareUsableAttack(ClientCard cardA, ClientCard cardB)
        {
            if (cardA == null && cardB == null) return 0;
            if (cardA == null) return -1;
            if (cardB == null) return 1;
            int powerA = (cardA.IsDefense() && summonThisTurn.Contains(cardA)) ? 0 : cardA.Attack;
            int powerB = (cardB.IsDefense() && summonThisTurn.Contains(cardB)) ? 0 : cardB.Attack;
            if (powerA < powerB) return -1;
            if (powerA == powerB) return CardContainer.CompareCardLevel(cardA, cardB);
            return 1;
        }

        private bool CheckShouldNotIgnore(ClientCard card, bool ignore = false)
        {
            return !ignore || !currentNegateCardList.Contains(card);
        }

        private ClientCard GetProblematicEnemyMonster(int attack = 0, bool canBeTarget = false, bool ignoreCurrentDestroy = true, CardType selfType = 0)
        {
            ClientCard floodgateCard = Enemy.GetMonsters().Where(c => c?.Data != null && (!ignoreCurrentDestroy || !currentNegateCardList.Contains(c))
                && c.IsFloodgate() && c.IsFaceup()
                && CheckCanBeTargeted(c, canBeTarget, selfType)
                && CheckShouldNotIgnore(c)).OrderByDescending(card => card.Attack).FirstOrDefault();
            if (floodgateCard != null) return floodgateCard;

            ClientCard dangerCard = Enemy.MonsterZone.Where(c => c?.Data != null && (!ignoreCurrentDestroy || !currentNegateCardList.Contains(c))
                && c.IsMonsterDangerous() && c.IsFaceup() && CheckCanBeTargeted(c, canBeTarget, selfType)
                && CheckShouldNotIgnore(c)).OrderByDescending(card => card.Attack).FirstOrDefault();
            if (dangerCard != null) return dangerCard;

            ClientCard invincibleCard = Enemy.MonsterZone.Where(c => c?.Data != null && (!ignoreCurrentDestroy || !currentNegateCardList.Contains(c))
                && c.IsMonsterInvincible() && c.IsFaceup() && CheckCanBeTargeted(c, canBeTarget, selfType)
                && CheckShouldNotIgnore(c)).OrderByDescending(card => card.Attack).FirstOrDefault();
            if (invincibleCard != null) return invincibleCard;

            ClientCard equippedCard = Enemy.MonsterZone.Where(c => c?.Data != null && (!ignoreCurrentDestroy || !currentNegateCardList.Contains(c))
                && c.EquipCards.Count > 0 && CheckCanBeTargeted(c, canBeTarget, selfType)
                && CheckShouldNotIgnore(c)).OrderByDescending(card => card.Attack).FirstOrDefault();
            if (equippedCard != null) return equippedCard;

            ClientCard enemyExtraMonster = Enemy.MonsterZone.Where(c => c != null && (!ignoreCurrentDestroy || !currentNegateCardList.Contains(c))
                && (c.HasType(CardType.Fusion | CardType.Ritual | CardType.Synchro | CardType.Xyz) || (c.HasType(CardType.Link) && c.LinkCount >= 2))
                && CheckCanBeTargeted(c, canBeTarget, selfType) && CheckShouldNotIgnore(c)).OrderByDescending(card => card.Attack).FirstOrDefault();
            if (enemyExtraMonster != null) return enemyExtraMonster;

            ClientCard activatingAlbaz = Enemy.MonsterZone.FirstOrDefault(c => c != null && c.IsCode(68468459) && !c.IsDisabled()
                && !currentNegateCardList.Contains(c) && Duel.CurrentChain.Contains(c));
            if (activatingAlbaz != null) return activatingAlbaz;

            if (attack >= 0)
            {
                if (attack == 0) attack = GetBotBestPower();
                ClientCard betterCard = Enemy.MonsterZone.Where(c => c != null
                    && c.GetDefensePower() >= attack && c.GetDefensePower() > 0 && c.IsAttack()
                    && CheckCanBeTargeted(c, canBeTarget, selfType)
                    && (!ignoreCurrentDestroy || !currentNegateCardList.Contains(c)))
                    .OrderByDescending(c => c.Attack).FirstOrDefault();
                if (betterCard != null) return betterCard;
            }
            return null;
        }

        private List<ClientCard> GetProblematicEnemyCardList(bool canBeTarget = false, bool ignoreSpells = false, CardType selfType = 0)
        {
            List<ClientCard> resultList = new List<ClientCard>();

            List<ClientCard> floodgateList = Enemy.MonsterZone.Where(c => c?.Data != null && !currentNegateCardList.Contains(c)
                && c.IsFloodgate() && c.IsFaceup() && CheckCanBeTargeted(c, canBeTarget, selfType))
                .OrderByDescending(card => card.Attack).ToList();
            if (floodgateList.Count > 0) resultList.AddRange(floodgateList);

            List<ClientCard> problemEnemySpellList = Enemy.SpellZone.Where(c => c?.Data != null && !resultList.Contains(c) && !currentNegateCardList.Contains(c)
                && c.IsFloodgate() && c.IsFaceup() && CheckCanBeTargeted(c, canBeTarget, selfType)).ToList();
            if (problemEnemySpellList.Count > 0) resultList.AddRange(ShuffleList(problemEnemySpellList));

            List<ClientCard> dangerList = Enemy.MonsterZone.Where(c => c?.Data != null && !resultList.Contains(c) && !currentNegateCardList.Contains(c)
                && c.IsMonsterDangerous() && c.IsFaceup() && CheckCanBeTargeted(c, canBeTarget, selfType))
                .OrderByDescending(card => card.Attack).ToList();
            if (dangerList.Count > 0 && (Duel.Player == 0 || (Duel.Phase > DuelPhase.Main1 && Duel.Phase < DuelPhase.Main2)))
                resultList.AddRange(dangerList);

            List<ClientCard> invincibleList = Enemy.MonsterZone.Where(c => c?.Data != null && !resultList.Contains(c) && !currentNegateCardList.Contains(c)
                && c.IsMonsterInvincible() && c.IsFaceup() && CheckCanBeTargeted(c, canBeTarget, selfType))
                .OrderByDescending(card => card.Attack).ToList();
            if (invincibleList.Count > 0) resultList.AddRange(invincibleList);

            List<ClientCard> enemyMonsters = Enemy.GetMonsters().Where(c => c != null && !currentNegateCardList.Contains(c))
                .OrderByDescending(card => card.Attack).ToList();
            if (enemyMonsters.Count > 0)
            {
                foreach (ClientCard target in enemyMonsters)
                {
                    if ((target.HasType(CardType.Fusion | CardType.Ritual | CardType.Synchro | CardType.Xyz)
                         || (target.HasType(CardType.Link) && target.LinkCount >= 2))
                        && !resultList.Contains(target)
                        && CheckCanBeTargeted(target, canBeTarget, selfType))
                    {
                        resultList.Add(target);
                    }
                }
            }

            List<ClientCard> spells = Enemy.GetSpells().Where(c => c.IsFaceup() && !currentNegateCardList.Contains(c)
                && c.HasType(CardType.Equip | CardType.Pendulum | CardType.Field | CardType.Continuous)
                && CheckCanBeTargeted(c, canBeTarget, selfType)).ToList();
            if (spells.Count > 0 && !ignoreSpells) resultList.AddRange(ShuffleList(spells));

            return resultList;
        }

        private List<ClientCard> GetNormalEnemyTargetList(bool canBeTarget = true, bool ignoreCurrentDestroy = true, CardType selfType = 0, bool forNegate = false)
        {
            List<ClientCard> targetList = GetProblematicEnemyCardList(canBeTarget, selfType: selfType);
            List<ClientCard> enemyMonster = Enemy.GetMonsters().Where(card => card.IsFaceup() && !targetList.Contains(card)
                && (!ignoreCurrentDestroy || !currentNegateCardList.Contains(card))
                && (!forNegate || (!card.IsDisabled() && card.HasType(CardType.Effect))))
                .ToList();
            enemyMonster.Sort(CardContainer.CompareCardAttack);
            enemyMonster.Reverse();
            targetList.AddRange(enemyMonster);

            targetList.AddRange(ShuffleList(Enemy.GetSpells().Where(card =>
                (!ignoreCurrentDestroy || !currentNegateCardList.Contains(card)) && enemyPlaceThisTurn.Contains(card) && card.IsFacedown()).ToList()));
            targetList.AddRange(ShuffleList(Enemy.GetSpells().Where(card =>
                (!ignoreCurrentDestroy || !currentNegateCardList.Contains(card)) && !enemyPlaceThisTurn.Contains(card) && card.IsFacedown()).ToList()));
            targetList.AddRange(ShuffleList(Enemy.GetMonsters().Where(card => card.IsFacedown()
                && (!ignoreCurrentDestroy || !currentNegateCardList.Contains(card))
                && !forNegate).ToList()));

            return targetList;
        }

        private List<ClientCard> GetNormalEnemySpellTargetList(bool canBeTarget = true, bool ignoreCurrentDestroy = true, CardType selfType = 0, bool forNegate = false)
        {
            List<ClientCard> targetList = GetNormalEnemyTargetList(canBeTarget, ignoreCurrentDestroy, selfType, forNegate);
            return targetList.Where(card => card.HasType(CardType.Spell | CardType.Trap) || card.Location == CardLocation.SpellZone).ToList();
        }

        private List<ClientCard> GetMonsterListForTargetNegate(bool canBeTarget = false, CardType selfType = 0)
        {
            List<ClientCard> resultList = new List<ClientCard>();
            if (CheckWhetherNegated()) return resultList;

            ClientCard target = Enemy.MonsterZone.FirstOrDefault(card => card?.Data != null
                && card.IsMonsterShouldBeDisabledBeforeItUseEffect() && card.IsFaceup() && !card.IsShouldNotBeTarget()
                && CheckCanBeTargeted(card, canBeTarget, selfType)
                && !currentNegateCardList.Contains(card));
            if (target != null) resultList.Add(target);

            foreach (ClientCard chainingCard in Duel.CurrentChain)
            {
                if (chainingCard.Location == CardLocation.MonsterZone
                    && chainingCard.Controller == 1
                    && !chainingCard.IsDisabled()
                    && CheckCanBeTargeted(chainingCard, canBeTarget, selfType)
                    && !currentNegateCardList.Contains(chainingCard))
                {
                    resultList.Add(chainingCard);
                }
            }

            return resultList;
        }

        private int GetBotBestPower(bool onlyATK = false)
        {
            return Bot.MonsterZone.GetMonsters()
                .Where(card => !onlyATK || (!summonThisTurn.Contains(card) && Duel.Player == 0) || card.IsAttack())
                .Max(card => (int?)card.GetDefensePower()) ?? -1;
        }

        private bool CheckAtAdvantage()
        {
            if (GetProblematicEnemyMonster(0, false) == null && (Duel.Player == 0 || Bot.GetMonsterCount() > 0))
                return true;
            return false;
        }

        private bool CheckShouldNoMoreSpSummon()
        {
            if (CheckAtAdvantage() && enemyResolvedEffectIdList.Contains(_CardId.MaxxC)
                && !DefaultCheckWhetherBotCanSearch()
                && (Duel.Turn == 1 || Duel.Phase >= DuelPhase.Main2))
            {
                return true;
            }
            return false;
        }

        private bool CheckShouldNoMoreSpSummon(CardLocation loc)
        {
            if (CheckShouldNoMoreSpSummon()) return true;
            if (DefaultCheckWhetherBotCanSearch() || (Duel.Turn > 1 && Duel.Phase < DuelPhase.Main2)) return false;
            if (enemyResolvedEffectIdList.Contains(_CardId.MulcharmyPurulia) && (loc & CardLocation.Hand) != 0) return true;
            if (enemyResolvedEffectIdList.Contains(_CardId.MulcharmyFuwalos) && (loc & (CardLocation.Deck | CardLocation.Extra)) != 0) return true;
            if (enemyResolvedEffectIdList.Contains(_CardId.MulcharmyNyalus) && (loc & (CardLocation.Grave | CardLocation.Removed)) != 0) return true;
            return false;
        }

        private int GetSpecialSummonDrawCount(CardLocation loc)
        {
            int res = 0;
            if (DefaultCheckWhetherBotCanSearch())
                return 0;

            if (enemyResolvedEffectIdList.Contains(_CardId.MaxxC))
                res++;
            if ((loc & CardLocation.Hand) != 0)
                res += enemyResolvedEffectIdList.Count(id => id == _CardId.MulcharmyPurulia);
            if ((loc & (CardLocation.Deck | CardLocation.Extra)) != 0)
                res += enemyResolvedEffectIdList.Count(id => id == _CardId.MulcharmyFuwalos);
            if ((loc & (CardLocation.Grave | CardLocation.Removed)) != 0)
                res += enemyResolvedEffectIdList.Count(id => id == _CardId.MulcharmyNyalus);

            return res;
        }

        private int EstimateDirectDamage()
        {
            var faceup = Bot.GetMonsters().Where(m => m != null && m.IsFaceup()).ToList();
            if (faceup.Count == 0) return 0;
            return faceup.Sum(m => Math.Max(0, m.Attack));
        }

        private bool CanDirectLethalNow()
        {
            if (Enemy.GetMonsters().Any(m => m != null && m.IsFaceup())) return false;
            return EstimateDirectDamage() >= Enemy.LifePoints;
        }

        private bool HaveAnyBattleAttackerNow()
        {
            return Bot.GetMonsters().Any(m => m != null && m.IsFaceup() && m.Attack > 0);
        }

        // PURPOSE: identify Heavy Metal Raiders setup swings where we intentionally take battle damage
        // to keep a DARK Machine alive and bank permanent ATK under Raiders.
        // TRUSTS: we only take this line when LP remains above 0 after battle damage.
        // RESET: stateless helper.
        private bool IsHeavyMetalRaidersSetupAttack(ClientCard attacker, ClientCard defender, out int battleDamage, out int postBattleAttack)
        {
            battleDamage = 0;
            postBattleAttack = attacker != null ? attacker.Attack : 0;

            if (attacker == null || defender == null) return false;
            if (!HasFaceup(CardId.HeavyMetalRaiders)) return false;
            if (!attacker.IsFaceup() || !defender.IsFaceup()) return false;
            if (!IsDarkMachine(attacker)) return false;
            if (!defender.IsAttack()) return false; // no battle damage dealt to us if defender is in DEF
            if (attacker.Attack <= 0 || defender.Attack <= attacker.Attack) return false;

            battleDamage = defender.Attack - attacker.Attack;
            if (battleDamage <= 0) return false;
            if (Bot.LifePoints <= battleDamage) return false; // do not pay lethal

            postBattleAttack = attacker.Attack + battleDamage;
            return true;
        }

        private bool LooksLikeMagnamhutSearchPrompt(IList<ClientCard> cards, int min, int max, int hint)
        {
            if (!_pendingMagnamhutEndPhaseQueuePrompt || Duel.Phase != DuelPhase.End) return false;
            if (hint != HintMsg.AddToHand || min != 1 || max != 1) return false;
            if (Duel.GetCurrentChainCard() != null || Duel.GetCurrentSolvingChainInfo() != null) return false;
            if (cards == null || cards.Count == 0) return false;

            // c33854624.lua already filters these offered search candidates. Newly revealed
            // deck cards need not have a dynamic Race query, so do not recheck HasRace here.
            return cards.All(c => c != null && c.Controller == 0
                && (c.Location == CardLocation.Deck || c.Location == CardLocation.Grave)
                && !c.IsCode(CardId.BystialMagnamhut));
        }

        private ClientCard PickMagnamhutSearchTarget(IList<ClientCard> cards)
        {
            if (cards == null || cards.Count == 0) return null;

            return cards.FirstOrDefault(c => c != null && c.IsCode(CardId.TheBystialLubellion) && !Bot.HasInHand(CardId.TheBystialLubellion))
                ?? cards.FirstOrDefault(c => c != null && c.IsCode(CardId.RedEyesDarknessMetalDragon) && !Bot.HasInHand(CardId.RedEyesDarknessMetalDragon))
                ?? cards.FirstOrDefault(c => c != null && c.IsCode(CardId.BystialDruiswurm) && !Bot.HasInHand(CardId.BystialDruiswurm))
                ?? cards.FirstOrDefault(c => c != null && c.IsCode(CardId.BlackMetalDragon) && !Bot.HasInHand(CardId.BlackMetalDragon))
                ?? cards.FirstOrDefault(c => c != null && c.IsCode(CardId.BystialMagnamhut))
                ?? cards.FirstOrDefault(c => c != null && c.HasRace(CardRace.Dragon));
        }

        private bool HasEquipCardId(ClientCard card, int equipId)
            => card != null && card.EquipCards != null && card.EquipCards.Any(eq => eq != null && eq.IsCode(equipId));

        private bool HasMaxMetalmorphShield(ClientCard card)
            => HasEquipCardId(card, CardId.MaxMetalmorph);

        private bool TwoDragonsOnField()
            => Bot.MonsterZone.GetMonsters().Count(m => m != null && m.IsFaceup() && m.HasRace(CardRace.Dragon)) >= 2;

        private bool OppHasFaceupCard()
            => Enemy.GetMonsters().Any(m => m != null && m.IsFaceup())
            || Enemy.GetSpells().Any(s => s != null && s.IsFaceup());

        private ClientCard PickBestOpponentFaceup(IList<ClientCard> cards)
        {
            if (cards == null || cards.Count == 0) return null;

            var ranked = GetNormalEnemyTargetList(true, true, CardType.Monster);
            foreach (var pick in ranked)
            {
                if (pick != null && pick.IsFaceup() && cards.Contains(pick))
                    return pick;
            }

            return cards.Where(c => c != null && c.Controller == 1 && c.IsFaceup())
                        .OrderByDescending(c => c.Attack).FirstOrDefault()
                   ?? cards.FirstOrDefault(c => c != null && c.Controller == 1)
                   ?? cards[0];
        }

        private bool HasEnemyFaceupTargetableCard()
            => Enemy.GetMonsters().Any(m => m != null && m.IsFaceup() && !m.IsShouldNotBeTarget())
            || Enemy.GetSpells().Any(s => s != null && s.IsFaceup() && !s.IsShouldNotBeTarget());

        // PURPOSE: robustly identify enemy field cards even when prompt controller flags are inconsistent.
        // TRUSTS: enemy field objects are available in Enemy zone collections.
        // RESET: stateless helper.
        private bool IsEnemyFieldCard(ClientCard card)
        {
            if (card == null) return false;
            if (card.Controller == 1 || card.Owner == 1) return true;
            if (card.Location != CardLocation.MonsterZone && card.Location != CardLocation.SpellZone) return false;

            bool sameMonster = Enemy.GetMonsters().Any(m => m != null &&
                (ReferenceEquals(m, card) || (m.Sequence == card.Sequence && m.Location == card.Location && m.Id == card.Id)));
            if (sameMonster) return true;

            bool sameSpell = Enemy.GetSpells().Any(s => s != null &&
                (ReferenceEquals(s, card) || (s.Sequence == card.Sequence && s.Location == card.Location && s.Id == card.Id)));
            return sameSpell;
        }

        // PURPOSE: pick an opponent card only; avoids accidental self-targeting for destruction prompts.
        // TRUSTS: card lists for these prompts include enemy cards when activation gate passes.
        private ClientCard PickBestEnemyTargetOnly(IList<ClientCard> cards)
        {
            if (cards == null || cards.Count == 0) return null;

            var ranked = GetNormalEnemyTargetList(true, true, CardType.Monster);
            foreach (var target in ranked)
            {
                if (target != null && cards.Contains(target) && IsEnemyFieldCard(target))
                    return target;
            }

            var enemy =
                cards.Where(c => c != null && IsEnemyFieldCard(c) && c.IsFaceup() && !c.IsShouldNotBeTarget())
                     .OrderByDescending(ScoreEnemyCardForRemoval).FirstOrDefault()
                ?? cards.Where(c => c != null && IsEnemyFieldCard(c) && !c.IsShouldNotBeTarget())
                     .OrderByDescending(ScoreEnemyCardForRemoval).FirstOrDefault()
                ?? cards.FirstOrDefault(c => c != null && IsEnemyFieldCard(c));

            return enemy;
        }

        // PURPOSE: count legal-ish Satellite/Fidraulis payoff targets on the opponent field.
        // TRUSTS: targetability checks are approximate but sufficient for activation gating.
        // RESET: stateless helper.
        private int CountEnemyOnFieldTargets()
        {
            return Enemy.GetMonsters().Count(c => c != null && !c.IsShouldNotBeTarget())
                + Enemy.GetSpells().Count(c => c != null && !c.IsShouldNotBeTarget());
        }

        // PURPOSE: detect Fidraulis reveal prompt even when hint/type metadata is incomplete.
        // TRUSTS: reveal prompts offer only extra-deck revealable choices in practical duel flow.
        // RESET: stateless helper.
        private bool LooksLikeFidraulisRevealPrompt(IList<ClientCard> cards, int max, int hint)
        {
            if (cards == null || cards.Count == 0) return false;
            if (hint == HintMsg.Target || hint == HintMsg.Destroy || hint == HintMsg.Faceup || hint == HintMsg.SpSummon || hint == HintMsg.ToGrave) return false;
            if (cards.Any(c => c != null && IsEnemyFieldCard(c))) return false;
            var pool = cards.Where(c => c != null && (c.Location == CardLocation.Extra || c.HasType(CardType.Synchro))).ToList();
            if (pool.Count == 0) return false;
            return pool.Count == cards.Count || pool.Count >= Math.Max(1, cards.Count - 1);
        }

        // PURPOSE: keep Fidraulis reveal picks ordered consistently whether the engine asks for the whole subgroup at once or one card at a time.
        // TRUSTS: candidates are legal revealable Synchros from the Extra Deck prompt.
        // RESET: stateless helper.
        private List<ClientCard> GetOrderedFidraulisRevealPool(IEnumerable<ClientCard> cards)
        {
            var pool = cards?.Where(c => c != null && (c.Location == CardLocation.Extra || c.HasType(CardType.Synchro))).ToList()
                ?? new List<ClientCard>();
            if (pool.Count == 0) return pool;

            int[] revealOrder =
            {
                CardId.StardustDragonVictimSanctuary,
                CardId.DracoBerserkerOfTheTenyi,
                CardId.StardustDragon,
                CardId.AccelSynchroStardustDragon,
                CardId.SatelliteWarrior,
                CardId.BaronneDeFleur,
                CardId.StardustWarrior,
                CardId.JunkWarriorExtreme
            };

            var ordered = new List<ClientCard>();
            foreach (int id in revealOrder)
            {
                var hit = pool.FirstOrDefault(c => c.IsCode(id) && !ordered.Contains(c));
                if (hit != null) ordered.Add(hit);
            }
            ordered.AddRange(pool.Where(c => !ordered.Contains(c)));
            return ordered;
        }

        // PURPOSE: pick the maximum legal Fidraulis reveal package (up to 5, bounded by prompt max).
        // TRUSTS: cards are legal prompt options; priority list improves reveal quality.
        // RESET: stateless helper.
        private IList<ClientCard> PickFidraulisRevealCards(IList<ClientCard> cards, int min, int max)
        {
            var ordered = GetOrderedFidraulisRevealPool(cards);
            if (ordered.Count == 0) ordered = cards?.Where(c => c != null).ToList() ?? new List<ClientCard>();
            if (ordered.Count == 0) return null;

            int desired = _pendingFidraulisRevealDesiredCount > 0 ? _pendingFidraulisRevealDesiredCount : 5;
            int legalMax = Math.Max(1, max);
            int take = Math.Min(desired, Math.Min(legalMax, ordered.Count));
            take = Math.Max(min, take);
            return ordered.Take(take).ToList();
        }

        // PURPOSE: support Fidraulis subgroup selection when the engine requests one reveal at a time.
        // TRUSTS: pending selection state was seeded at activation time.
        // RESET: selection state clears on chain end / new turn.
        private ClientCard PickNextFidraulisRevealCard(IList<ClientCard> cards)
        {
            var ordered = GetOrderedFidraulisRevealPool(cards);
            if (ordered.Count == 0) return null;
            return ordered.FirstOrDefault(c => !_pendingFidraulisRevealPickedIds.Contains(c.Id)) ?? ordered.FirstOrDefault();
        }

        // PURPOSE: advance Fidraulis reveal state after each selected reveal and arm the follow-up send/destroy prompts only when the subgroup is complete.
        // TRUSTS: desired count was computed from current board/extradeck state when Fidraulis activated.
        // RESET: clears/arms only for the current chain.
        private void AdvanceFidraulisRevealState(IEnumerable<ClientCard> chosen)
        {
            if (chosen == null) return;
            foreach (var c in chosen)
            {
                if (c == null) continue;
                _pendingFidraulisRevealPickedIds.Add(c.Id);
            }

            if (_pendingFidraulisRevealDesiredCount <= 0)
                _pendingFidraulisRevealDesiredCount = _pendingFidraulisRevealPickedIds.Count;

            if (_pendingFidraulisRevealPickedIds.Count >= _pendingFidraulisRevealDesiredCount)
            {
                _pendingFidraulisRevealPrompt = false;
                _pendingFidraulisSendPrompt = _pendingFidraulisRevealDesiredCount >= 3;
                _pendingFidraulisDestroyPrompt = _pendingFidraulisRevealDesiredCount >= 5;
            }
        }

        // PURPOSE: enforce the Fidraulis send priority (Victim Sanctuary only).
        // TRUSTS: cards are legal send candidates from the Fidraulis resolution prompt.
        // RESET: stateless helper.
        private ClientCard PickFidraulisSendToGrave(IList<ClientCard> cards)
        {
            if (cards == null || cards.Count == 0) return null;

            return cards.FirstOrDefault(c => c != null && c.IsCode(CardId.StardustDragonVictimSanctuary))
                ?? cards.FirstOrDefault(c => c != null);
        }

        // PURPOSE: detect the Fidraulis "send revealed Synchro to GY" prompt even if Card context is missing.
        // TRUSTS: this prompt is a ToGrave/OperateCard style selection that includes Victim Sanctuary.
        // RESET: stateless helper.
        private bool LooksLikeFidraulisSendPrompt(IList<ClientCard> cards, int hint)
        {
            if (cards == null || cards.Count == 0) return false;
            if (!(hint == HintMsg.ToGrave || hint == HintMsg.OperateCard || hint == 505)) return false;
            if (!cards.Any(c => c != null && c.IsCode(CardId.StardustDragonVictimSanctuary)))
                return false;
            if (cards.Any(c => c != null && IsEnemyFieldCard(c))) return false;
            return true;
        }

        // PURPOSE: detect Fidraulis destroy prompts when Card context is missing mid-chain.
        // TRUSTS: this prompt only presents opponent field monsters after the 6-reveal branch.
        // RESET: stateless helper.
        private bool LooksLikeFidraulisDestroyPrompt(IList<ClientCard> cards, int hint)
        {
            if (cards == null || cards.Count == 0) return false;
            if (!(hint == HintMsg.Destroy || hint == HintMsg.Target || hint == HintMsg.OperateCard || hint == 505)) return false;
            if (cards.Any(c => c != null && c.Controller == 0)) return false;
            return cards.Any(c => c != null && c.Controller == 1 && c.Location == CardLocation.MonsterZone);
        }

        // PURPOSE: detect Fullmetal-style trap tutor prompts when card context is unavailable.
        // TRUSTS: these prompts are AddToHand/OperateCard/Set style lists containing only/mostly Metalmorph trap candidates.
        // RESET: stateless helper.
        private bool LooksLikeMetalmorphTrapTutorPrompt(IList<ClientCard> cards, int hint)
        {
            if (cards == null || cards.Count == 0) return false;
            if (!(hint == HintMsg.AddToHand || hint == HintMsg.OperateCard || hint == HintMsg.Set || hint == 505)) return false;

            int nonNullCount = cards.Count(c => c != null);
            int trapCount = cards.Count(c => c != null && IsMetalmorphTrapId(c.Id));
            if (trapCount == 0 || nonNullCount == 0) return false;
            if (trapCount == nonNullCount) return true;

            return trapCount >= 2 && cards.All(c => c == null || IsMetalmorphTrapId(c.Id) || MentionsMax(c));
        }

        // PURPOSE: prevent "tribute X -> summon same X" when alternatives exist.
        // TRUSTS: _lastMetallizeTributeCode is set in TrackMetallizeTribute before summon selection.
        private ClientCard PickMetallizeFollowupAvoidingTribute(params ClientCard[] options)
        {
            int tributeCode = _lastMetallizeTributeCode;
            var diff = options.FirstOrDefault(c => c != null && (tributeCode == 0 || c.Id != tributeCode));
            return diff ?? options.FirstOrDefault(c => c != null);
        }

        // PURPOSE: resolve Max/Flame equip prompts onto the intended newly-summoned boss body.
        // TRUSTS: _lastMetallizeRace/_lastMetallizeLevel were updated during tribute selection.
        private ClientCard PickMetallizeEquipTarget(IList<ClientCard> cards)
        {
            if (cards == null || cards.Count == 0) return null;
            var ownPool = cards.Where(c => c != null && c.Controller == 0 && c.IsFaceup() && c.IsMonster()).ToList();
            var pool = ownPool.Count > 0 ? ownPool : cards.Where(c => c != null && c.IsMonster()).ToList();
            if (pool.Count == 0) return cards.FirstOrDefault(c => c != null) ?? cards[0];

            bool fiendTrib = _lastMetallizeLevel >= 5 && ((_lastMetallizeRace & CardRace.Fiend) != 0);
            bool dragonTrib = _lastMetallizeLevel >= 5 && ((_lastMetallizeRace & CardRace.Dragon) != 0);
            bool warriorTrib = _lastMetallizeLevel >= 5 && _lastMetallizationWasWarrior5p;
            Func<ClientCard, bool> noEquip = c => c != null && (c.EquipCards == null || c.EquipCards.Count == 0);

            var pick =
                  (fiendTrib ? pool.FirstOrDefault(c => c.IsCode(CardId.MetalzoaX) && noEquip(c)) : null)
               ?? (dragonTrib ? pool.FirstOrDefault(c => c.IsCode(CardId.RedEyesBlackFullmetalDragon) && noEquip(c)) : null)
               ?? (warriorTrib ? pool.FirstOrDefault(c => c.IsCode(CardId.MetalflameSwordsman) && noEquip(c)) : null)
               ?? pool.FirstOrDefault(c => c.IsCode(CardId.MetalzoaX) && noEquip(c))
               ?? pool.FirstOrDefault(c => c.IsCode(CardId.RedEyesBlackFullmetalDragon) && noEquip(c))
               ?? pool.FirstOrDefault(c => c.IsCode(CardId.MetalflameSwordsman) && noEquip(c))
               ?? pool.FirstOrDefault(c => IsBoss(c) && noEquip(c))
               ?? pool.FirstOrDefault(c => c.IsCode(CardId.MetalzoaX))
               ?? pool.FirstOrDefault(c => c.IsCode(CardId.RedEyesBlackFullmetalDragon))
               ?? pool.FirstOrDefault(c => c.IsCode(CardId.MetalflameSwordsman))
               ?? pool.OrderByDescending(c => c.Attack).FirstOrDefault();

            return pick ?? (cards.FirstOrDefault(c => c != null) ?? cards[0]);
        }

        // PURPOSE: only allow BMD -> REDMD equip when another BMD body is already on our field.
        // TRUSTS: Card is the BMD currently resolving its equip effect.
        private bool HasSecondBmdBodyForRedmdEquip()
        {
            bool selfIsFieldBmd = Card != null
                               && Card.IsCode(CardId.BlackMetalDragon)
                               && Card.Location == CardLocation.MonsterZone;
            int faceupBmd = Bot.GetMonsters().Count(c => c != null && c.IsFaceup() && c.IsCode(CardId.BlackMetalDragon));
            return selfIsFieldBmd ? faceupBmd >= 2 : faceupBmd >= 1;
        }

        // PURPOSE: preserve the BMD -> Striker -> REDMD -> Seal route when it is fully online.
        // TRUSTS: used as a gate for optional detours (Incoming/Claws/early Seal).
        // RESET: stateless helper.
        private bool ShouldCommitToRedmdSealLineNow()
        {
            if (Duel.Player != 0) return false;
            if (Duel.Phase != DuelPhase.Main1 && Duel.Phase != DuelPhase.Main2) return false;
            if (Bot.HasInMonstersZone(CardId.HieraticSealOfTheHeavenlySpheres, true)) return false;
            if (!Bot.HasInExtra(CardId.HieraticSealOfTheHeavenlySpheres)) return false;
            if (Bot.HasInMonstersZone(CardId.RedEyesDarknessMetalDragon, true)) return false;

            bool bmdStarterOnline =
                Bot.HasInMonstersZone(CardId.BlackMetalDragon, true)
                || Bot.HasInMonstersZone(CardId.StrikerDragon, true);
            if (!bmdStarterOnline) return false;

            bool redmdInHand = Bot.HasInHand(CardId.RedEyesDarknessMetalDragon);
            bool redmdSearchableFromBmd =
                Bot.HasInMonstersZone(CardId.BlackMetalDragon, true)
                && Bot.HasInExtra(CardId.StrikerDragon)
                && CheckRemainInDeck(CardId.RedEyesDarknessMetalDragon) > 0;
            if (!redmdInHand && !redmdSearchableFromBmd) return false;

            bool canPayRedmdCostNow = Bot.GetMonsters().Any(m => m != null && m.IsFaceup() && m.HasRace(CardRace.Dragon));
            if (!canPayRedmdCostNow && !redmdSearchableFromBmd) return false;

            bool haveReviveTarget =
                Bot.Graveyard.Any(c => c != null && c.HasRace(CardRace.Dragon))
                || Bot.Hand.Any(c => c != null && c.HasRace(CardRace.Dragon) && !c.IsCode(CardId.RedEyesDarknessMetalDragon))
                || Bot.HasInMonstersZone(CardId.BlackMetalDragon, true);
            if (!haveReviveTarget) return false;

            return true;
        }

        // PURPOSE: force BMD grave search into REDMD whenever the full REDMD->Seal branch is executable.
        // TRUSTS: called only during Black Metal Dragon add prompts.
        // RESET: stateless helper.
        private bool ShouldForceRedmdFromBmdSearch(IList<ClientCard> options)
        {
            if (options == null || options.Count == 0) return false;
            if (!options.Any(c => c != null && c.IsCode(CardId.RedEyesDarknessMetalDragon))) return false;
            if (Bot.HasInMonstersZone(CardId.RedEyesDarknessMetalDragon, true)) return false;

            bool canPayRedmdCost = Bot.GetMonsters().Any(m => m != null && m.IsFaceup() && m.HasRace(CardRace.Dragon));
            if (!canPayRedmdCost) return false;

            bool haveReviveTarget =
                Bot.Graveyard.Any(c => c != null && c.HasRace(CardRace.Dragon))
                || Bot.Hand.Any(c => c != null && c.HasRace(CardRace.Dragon));
            if (!haveReviveTarget) return false;

            return Bot.HasInExtra(CardId.HieraticSealOfTheHeavenlySpheres)
                && !Bot.HasInMonstersZone(CardId.HieraticSealOfTheHeavenlySpheres, true);
        }

        // PURPOSE: determine whether a key BMD-line card is already accessible, so search can fetch the next missing piece.
        // TRUSTS: hand/field/GY/banished + deck depletion are sufficient availability signals for this line.
        // RESET: stateless helper.
        private bool HasBmdLineAccess(int id)
        {
            return Bot.HasInHand(id)
                || Bot.HasInMonstersZone(id, true)
                || Bot.Graveyard.Any(c => c != null && c.IsCode(id))
                || Bot.Banished.Any(c => c != null && c.IsCode(id))
                || CheckRemainInDeck(id) == 0;
        }

        // PURPOSE: enforce BMD grave-search line priorities by missing key piece:
        //          REDMD first, then Fullmetal, then Claws.
        // TRUSTS: options are legal candidates from BMD's search prompt.
        // RESET: stateless helper.
        private ClientCard PickBmdGraveSearchTarget(IList<ClientCard> options, bool forceRedmd)
        {
            if (options == null || options.Count == 0) return null;

            var redmd = options.FirstOrDefault(c => c != null && c.IsCode(CardId.RedEyesDarknessMetalDragon));
            var fullmetal = options.FirstOrDefault(c => c != null && c.IsCode(CardId.RedEyesBlackFullmetalDragon));
            var claws = options.FirstOrDefault(c => c != null && c.IsCode(CardId.RedEyesMetalClawsDragon));

            if (forceRedmd && redmd != null) return redmd;

            bool haveRedmd = HasBmdLineAccess(CardId.RedEyesDarknessMetalDragon);
            bool haveFullmetal = HasBmdLineAccess(CardId.RedEyesBlackFullmetalDragon);
            bool haveClaws = HasBmdLineAccess(CardId.RedEyesMetalClawsDragon);

            var pick =
                  (!haveRedmd ? redmd : null)
               ?? (!haveFullmetal ? fullmetal : null)
               ?? (!haveClaws ? claws : null)
               ?? fullmetal
               ?? claws
               ?? redmd
               ?? options.FirstOrDefault(c => c != null)
               ?? options[0];

            MMLog("[MM.BMD.SearchPick]", "pick=" + pick.Id
                + " forceRedmd=" + (forceRedmd ? "yes" : "no")
                + " haveR/F/C=" + (haveRedmd ? "1" : "0") + "/" + (haveFullmetal ? "1" : "0") + "/" + (haveClaws ? "1" : "0"));
            return pick;
        }

        // PURPOSE: choose the strongest REDMD fallback revive target once BMD/Illusionist/Claws priorities are exhausted.
        // TRUSTS: higher score means better extender/follow-up value this turn.
        // RESET: stateless helper.
        private int ScoreRedmdReviveExtender(ClientCard c)
        {
            if (c == null || !c.HasRace(CardRace.Dragon)) return int.MinValue;
            int score = 0;

            if (c.IsCode(CardId.TheBystialLubellion)) score += 9000;
            else if (c.IsCode(CardId.BystialMagnamhut)) score += 8500;
            else if (c.IsCode(CardId.BystialDruiswurm)) score += 8200;
            else if (c.IsCode(CardId.RedEyesBlackFullmetalDragon)) score += 7900;
            else if (c.IsCode(CardId.MetalflameSwordsman)) score += 7600;
            else if (c.IsCode(CardId.ZoaTheFiendishBeast)) score += 7300;

            score += Math.Max(0, c.Attack);
            score += c.Level * 120;
            return score;
        }

        private static int FullmetalBurnAtk(ClientCard c)
        {
            if (c == null) return 0;
            return c.BaseAttack > 0 ? c.BaseAttack : Math.Max(0, c.Attack);
        }

        private bool IsTimeEngineHighValueCode(int id)
        {
            return id == CardId.RedEyesBlackFullmetalDragon
                || id == CardId.MetalzoaX
                || id == CardId.MetalflameSwordsman
                || id == CardId.RedEyesDarknessMetalDragon
                || id == CardId.RedEyesMetalClawsDragon
                || id == CardId.ZoaTheFiendishBeast
                || id == CardId.BystialMagnamhut
                || id == CardId.BystialDruiswurm;
        }

        private bool IsTimeEngineLowValueCode(int id)
        {
            return id == CardId.StrikerDragon
                || id == CardId.HopEarSquadron
                || id == CardId.FidraulisHarmonia
                || id == CardId.BlackMetalDragon
                || id == CardId.MetalIllusionist
                || id == CardId.AshBlossomJoyousSpring
                || id == CardId.MaxxC;
        }

        private IList<ClientCard> SelectTimeEngineTarget(IList<ClientCard> cards, int min, int max)
        {
            var ownChoices = cards.Where(c => c != null && c.Controller == 0).ToList();
            var pool = ownChoices.Count > 0 ? ownChoices : cards.Where(c => c != null).ToList();

            var pick =
                  pool.FirstOrDefault(c => c.IsCode(CardId.RedEyesBlackFullmetalDragon))
               ?? pool.FirstOrDefault(c => c.IsCode(CardId.MetalzoaX))
               ?? pool.FirstOrDefault(c => c.IsCode(CardId.MetalflameSwordsman))
               ?? pool.FirstOrDefault(c => c.IsCode(CardId.RedEyesDarknessMetalDragon))
               ?? pool.FirstOrDefault(c => c.IsCode(CardId.RedEyesMetalClawsDragon))
               ?? pool.Where(c => c.IsMonster() && c.HasRace(CardRace.Machine) && c.Level >= 5)
                      .OrderByDescending(c => c.BaseAttack > 0 ? c.BaseAttack : c.Attack)
                      .FirstOrDefault()
               ?? pool.Where(c => c.IsMonster() && !IsTimeEngineLowValueCode(c.Id))
                      .OrderByDescending(c => c.BaseAttack > 0 ? c.BaseAttack : c.Attack)
                      .ThenByDescending(c => c.Level)
                      .FirstOrDefault()
               ?? pool.Where(c => c.IsMonster())
                      .OrderByDescending(c => c.BaseAttack > 0 ? c.BaseAttack : c.Attack)
                      .ThenByDescending(c => c.Level)
                      .FirstOrDefault()
               ?? cards.FirstOrDefault(c => c != null)
               ?? cards[0];

            MMLog("[MM.TimeEngine.Target]", "pick=" + (pick != null ? pick.Id : 0));
            return Util.CheckSelectCount(new List<ClientCard> { pick }, cards, min, max);
        }

        private ClientCard PickFullmetalBurnTarget(IList<ClientCard> cards)
        {
            // c80870883.lua does not target or remove the selected monster. Only its
            // original ATK determines damage; Link status and targeting protection do not.
            return cards.Where(c => c != null && c.Controller == 1 && c.IsFaceup()
                    && c.IsMonster() && c.IsAttack())
                .OrderByDescending(c => c.BaseAttack)
                .ThenByDescending(ScoreEnemyCardForRemoval)
                .FirstOrDefault();
        }

        private ClientCard PreferMetalmorphToSet(IList<ClientCard> options)
        {
            if (options == null || options.Count == 0) return null;

            bool haveMax = HasMetalmorphTrapCoverage(CardId.MaxMetalmorph);
            bool haveFlame = HasMetalmorphTrapCoverage(CardId.FlameCoatingMetalmorph);
            bool haveTime = HasMetalmorphTrapCoverage(CardId.TimeEngine);
            bool prioritizeTime = ShouldPrioritizeTimeEngineNow();

            if (prioritizeTime)
                return options.FirstOrDefault(c => c != null && c.IsCode(CardId.TimeEngine))
                    ?? options.FirstOrDefault(c => c != null && c.IsCode(CardId.MaxMetalmorph))
                    ?? options.FirstOrDefault(c => c != null && c.IsCode(CardId.FlameCoatingMetalmorph))
                    ?? options[0];

            if (!haveMax)
                return options.FirstOrDefault(c => c != null && c.IsCode(CardId.MaxMetalmorph))
                    ?? options.FirstOrDefault(c => c != null && c.IsCode(CardId.FlameCoatingMetalmorph))
                    ?? options.FirstOrDefault(c => c != null && c.IsCode(CardId.TimeEngine))
                    ?? options[0];

            if (!haveFlame)
                return options.FirstOrDefault(c => c != null && c.IsCode(CardId.FlameCoatingMetalmorph))
                    ?? options.FirstOrDefault(c => c != null && c.IsCode(CardId.MaxMetalmorph))
                    ?? options.FirstOrDefault(c => c != null && c.IsCode(CardId.TimeEngine))
                    ?? options[0];

            if (!haveTime)
                return options.FirstOrDefault(c => c != null && c.IsCode(CardId.TimeEngine))
                    ?? options.FirstOrDefault(c => c != null && c.IsCode(CardId.MaxMetalmorph))
                    ?? options.FirstOrDefault(c => c != null && c.IsCode(CardId.FlameCoatingMetalmorph))
                    ?? options[0];

            return options.FirstOrDefault(c => c != null && c.IsCode(CardId.MaxMetalmorph))
                ?? options.FirstOrDefault(c => c != null && c.IsCode(CardId.FlameCoatingMetalmorph))
                ?? options.FirstOrDefault(c => c != null && c.IsCode(CardId.TimeEngine))
                ?? options[0];
        }

        // PURPOSE: Fullmetal hand/field set route should establish immediate trap board access.
        // TRUSTS: Fullmetal set prompts provide only Metalmorph trap options.
        // RESET: stateless helper.
        private ClientCard PreferFullmetalTrapToSet(IList<ClientCard> options)
        {
            if (options == null || options.Count == 0) return null;

            bool haveMaxBoard = HasMetalmorphTrapBoardCoverage(CardId.MaxMetalmorph);
            bool haveTimeBoard = HasMetalmorphTrapBoardCoverage(CardId.TimeEngine);
            bool haveFlameBoard = HasMetalmorphTrapBoardCoverage(CardId.FlameCoatingMetalmorph);

            // Keep Max online first, then Time for protection package, then Flame.
            if (!haveMaxBoard)
                return options.FirstOrDefault(c => c != null && c.IsCode(CardId.MaxMetalmorph))
                    ?? options.FirstOrDefault(c => c != null && c.IsCode(CardId.TimeEngine))
                    ?? options.FirstOrDefault(c => c != null && c.IsCode(CardId.FlameCoatingMetalmorph))
                    ?? options[0];

            if (!haveTimeBoard)
                return options.FirstOrDefault(c => c != null && c.IsCode(CardId.TimeEngine))
                    ?? options.FirstOrDefault(c => c != null && c.IsCode(CardId.FlameCoatingMetalmorph))
                    ?? options.FirstOrDefault(c => c != null && c.IsCode(CardId.MaxMetalmorph))
                    ?? options[0];

            if (!haveFlameBoard)
                return options.FirstOrDefault(c => c != null && c.IsCode(CardId.FlameCoatingMetalmorph))
                    ?? options.FirstOrDefault(c => c != null && c.IsCode(CardId.TimeEngine))
                    ?? options.FirstOrDefault(c => c != null && c.IsCode(CardId.MaxMetalmorph))
                    ?? options[0];

            return options.FirstOrDefault(c => c != null && c.IsCode(CardId.MaxMetalmorph))
                ?? options.FirstOrDefault(c => c != null && c.IsCode(CardId.TimeEngine))
                ?? options.FirstOrDefault(c => c != null && c.IsCode(CardId.FlameCoatingMetalmorph))
                ?? options[0];
        }

        // PURPOSE: shared card-pick policy for "Metalmorph boss tutor" prompts.
        // TRUSTS: options may include both trap and non-trap tutor candidates.
        private ClientCard PickMetalmorphTutorCard(IList<ClientCard> options)
        {
            if (options == null || options.Count == 0) return null;

            var trapOptions = options.Where(c => c != null &&
                                                (c.IsCode(CardId.MaxMetalmorph) || c.IsCode(CardId.FlameCoatingMetalmorph) || c.IsCode(CardId.TimeEngine)))
                                     .ToList();
            if (trapOptions.Count > 0)
                return PreferMetalmorphToSet(trapOptions);

            return options.FirstOrDefault(c => c != null && c.IsCode(CardId.RedEyesBlackFullmetalDragon))
                ?? options.FirstOrDefault(c => c != null && c.IsCode(CardId.MetalzoaX))
                ?? options.FirstOrDefault(c => c != null && c.IsCode(CardId.MetalflameSwordsman))
                ?? options[0];
        }

        private bool LooksLikeSealFloatPrompt(IList<ClientCard> cards)
        {
            if (cards == null || cards.Count == 0) return false;
            return cards.Any(c => c != null && (c.Location == CardLocation.Deck || c.Location == CardLocation.Hand || c.Location == CardLocation.Extra));
        }

        private ClientCard PickSealBounceTarget(IList<ClientCard> cards)
        {
            var ranked = GetNormalEnemyTargetList(true, true, CardType.Monster);
            foreach (var target in ranked)
            {
                if (target != null && cards.Contains(target) && IsEnemyFieldCard(target))
                    return target;
            }

            var enemy = cards.Where(c => c != null && c.Controller == 1 && c.IsFaceup() && c.IsMonster())
                             .OrderByDescending(ScoreEnemyCardForRemoval)
                             .FirstOrDefault();
            if (enemy != null) return enemy;

            enemy = cards.Where(c => c != null && c.Controller == 1 && c.IsFaceup())
                         .OrderByDescending(ScoreEnemyCardForRemoval)
                         .FirstOrDefault();
            if (enemy != null) return enemy;

            // PURPOSE: do not bounce our own board with Seal unless absolutely unavoidable.
            // TRUSTS: normal Seal prompts should always include a valid enemy face-up target.
            var enemyAny = cards.FirstOrDefault(c => c != null && c.Controller == 1);
            if (enemyAny != null) return enemyAny;

            return cards[0];
        }

        // Boss / protected
        private static bool IsBoss(ClientCard c)
            => c != null && (c.IsCode(CardId.RedEyesBlackFullmetalDragon) || c.IsCode(CardId.MetalzoaX));

        private static bool IsProtectedForTribute(ClientCard c)
        {
            if (c == null) return true;
            if (IsBoss(c)) return true;
            return false;
        }

        // PURPOSE: preserve REDMD+BMD (or another low-cost dragon pair) for Seal lines when possible.
        // TRUSTS: we only protect REDMD from tribute costs when not under immediate pressure.
        private bool ShouldPreserveRedmdForSeal()
        {
            if (!Bot.HasInMonstersZone(CardId.RedEyesDarknessMetalDragon, true)) return false;
            if (Bot.HasInMonstersZone(CardId.HieraticSealOfTheHeavenlySpheres, true)) return false;
            if (!Bot.HasInExtra(CardId.HieraticSealOfTheHeavenlySpheres)) return false;

            var dragons = Bot.GetMonsters().Where(m => m != null && m.IsFaceup() && m.HasRace(CardRace.Dragon)).ToList();
            bool haveBmd = dragons.Any(m => m.IsCode(CardId.BlackMetalDragon));
            bool haveSecondNonBossDragon = dragons.Any(m => !m.IsCode(CardId.RedEyesDarknessMetalDragon) && !IsBoss(m));
            return haveBmd || haveSecondNonBossDragon;
        }

        private bool IsProtectedForTributeCost(ClientCard c)
        {
            if (c == null) return true;
            if (IsProtectedForTribute(c)) return true;
            if (c.IsCode(CardId.HieraticSealOfTheHeavenlySpheres)) return true;
            if (c.IsCode(CardId.RedEyesDarknessMetalDragon) && ShouldPreserveRedmdForSeal() && !EnemyHighPressure()) return true;
            return false;
        }

        // Treat these as off-limits for link fodder, too.
        private static bool IsProtectedForLinkMat(ClientCard c)
        {
            if (c == null) return true;
            if (IsProtectedForTribute(c)) return true;
            if (c.IsCode(CardId.HieraticSealOfTheHeavenlySpheres)) return true;
            if (c.IsCode(CardId.RedEyesDarknessMetalDragon)) return true;
            return false;
        }

        private static bool IsCoreBranchBody(ClientCard c)
        {
            if (c == null) return false;
            return c.IsCode(CardId.ZoaTheFiendishBeast)
                || c.IsCode(CardId.MetalflameSwordsman)
                || c.IsCode(CardId.RedEyesMetalClawsDragon);
        }

        // PURPOSE: keep premium Synchro interaction pieces off generic link ladders unless absolutely needed.
        // TRUSTS: these cards are higher-value as board interaction than as generic link material.
        // RESET: stateless helper.
        private static bool IsHighValueSynchroBody(ClientCard c)
        {
            if (c == null) return false;
            return c.IsCode(CardId.SatelliteWarrior)
                || c.IsCode(CardId.StardustWarrior)
                || c.IsCode(CardId.BaronneDeFleur)
                || c.IsCode(CardId.AccelSynchroStardustDragon)
                || c.IsCode(CardId.StardustDragon)
                || c.IsCode(CardId.DracoBerserkerOfTheTenyi);
        }

        // PURPOSE: preserve BMD when the Striker -> REDMD core branch has not been completed yet.
        // TRUSTS: if Striker/REDMD/Seal are already established, BMD can be used as expendable material.
        // RESET: stateless helper.
        private bool ShouldHoldBlackMetalForStrikerLine()
        {
            if (!Bot.HasInMonstersZone(CardId.BlackMetalDragon, true)) return false;
            if (ShouldCommitToRedmdSealLineNow()) return true;
            if (Bot.HasInMonstersZone(CardId.RedEyesDarknessMetalDragon, true)) return false;
            if (Bot.HasInMonstersZone(CardId.HieraticSealOfTheHeavenlySpheres, true)) return false;
            bool strikerLive = Bot.HasInExtra(CardId.StrikerDragon) && !Bot.HasInMonstersZone(CardId.StrikerDragon, true);
            return strikerLive;
        }

        // PURPOSE: score level-1 non-tuner bodies for optional Fidraulis -> Draco conversion.
        // TRUSTS: lower score means safer to spend for Draco.
        // RESET: stateless helper.
        private int ScoreDracoMaterialForUse(ClientCard c)
        {
            if (c == null) return int.MaxValue;
            int score = 1000;

            if (c.IsCode(CardId.MetalIllusionist)) score -= 500;
            if (c.IsCode(CardId.BlackMetalDragon)) score += ShouldHoldBlackMetalForStrikerLine() ? 6000 : 1500;
            if (IsCoreBranchBody(c)) score += 3000;
            if (IsHighValueSynchroBody(c)) score += 5000;
            if (IsBoss(c)) score += 9000;
            if (c.IsCode(CardId.RedEyesDarknessMetalDragon) || c.IsCode(CardId.HieraticSealOfTheHeavenlySpheres)) score += 9000;
            if (c.HasType(CardType.Link | CardType.Fusion | CardType.Synchro | CardType.Xyz)) score += 3500;

            score += Math.Max(0, c.Attack / 100);
            return score;
        }

        // PURPOSE: pick Fidraulis + safe level-1 non-tuner material for Draco without breaking core lines.
        // TRUSTS: caller is inside a legal Synchro prompt for Draco.
        // RESET: stateless helper.
        private List<ClientCard> PickDracoBerserkerMaterials(IList<ClientCard> cards, int sum)
        {
            var own = cards?.Where(c => c != null && c.Controller == 0 && c.IsFaceup()).ToList() ?? new List<ClientCard>();
            if (own.Count < 2) return null;

            var fid = own.FirstOrDefault(c => c.IsCode(CardId.FidraulisHarmonia) && c.HasType(CardType.Tuner));
            if (fid == null) return null;

            var pool = own.Where(c => c != fid
                                   && !c.HasType(CardType.Tuner)
                                   && c.Level > 0
                                   && fid.Level + c.Level == sum
                                   && !IsBoss(c)
                                   && !IsHighValueSynchroBody(c)
                                   && !c.IsCode(CardId.RedEyesDarknessMetalDragon)
                                   && !c.IsCode(CardId.HieraticSealOfTheHeavenlySpheres))
                          .ToList();
            if (pool.Count == 0) return null;

            if (ShouldHoldBlackMetalForStrikerLine())
                pool = pool.Where(c => !c.IsCode(CardId.BlackMetalDragon)).ToList();
            if (pool.Count == 0) return null;

            var pick = pool.OrderBy(ScoreDracoMaterialForUse).FirstOrDefault();
            if (pick == null) return null;

            return new List<ClientCard> { fid, pick };
        }

        private bool EnemyHighPressure()
        {
            int eMons = Enemy.GetMonsters().Count(m => m != null && m.IsFaceup());
            bool dangerousBody = Enemy.GetMonsters().Any(m => m != null && m.IsFaceup()
                && (m.IsMonsterDangerous() || m.IsMonsterInvincible() || m.Attack >= 2600));
            bool faceupFloodgate = Enemy.GetSpells().Any(s => s != null && s.IsFaceup() && s.IsFloodgate());
            return eMons >= 2 || dangerousBody || faceupFloodgate;
        }

        private bool ShouldHoldBattleCoreForBoard()
        {
            bool haveCore = Bot.HasInMonstersZone(CardId.ZoaTheFiendishBeast, true)
                         || Bot.HasInMonstersZone(CardId.MetalflameSwordsman, true);
            if (!haveCore) return false;
            if (EnemyHighPressure()) return false;

            var bestOur = Bot.GetMonsters().Where(m => m != null && m.IsFaceup())
                                           .OrderByDescending(m => m.Attack).FirstOrDefault();
            var bestEnemy = Enemy.GetMonsters().Where(m => m != null && m.IsFaceup())
                                               .OrderByDescending(m => m.GetDefensePower()).FirstOrDefault();
            if (bestEnemy == null) return true;
            if (bestOur == null) return false;
            return bestOur.Attack >= bestEnemy.GetDefensePower();
        }

        private bool HasSafeTributeForMetallize()
        {
            var myMons = Bot.GetMonsters().Where(m => m != null && m.IsFaceup()).ToList();
            bool haveSafe = myMons.Any(m => !IsProtectedForTributeCost(m));
            return haveSafe;
        }

        // PURPOSE: check Max targets across all legal zones (hand/deck/GY).
        private bool HasMaxSummonCandidate(int id)
        {
            if (id == 0) return false;
            return Bot.Hand.Any(c => c != null && c.IsCode(id))
                || Bot.Graveyard.Any(c => c != null && c.IsCode(id))
                || CheckRemainInDeck(id) > 0;
        }

        // PURPOSE: avoid firing Max when only self-replacement/no-upgrade lines remain.
        private bool HasMeaningfulMetallizeUpgradeForMax()
        {
            bool haveSwordsman = Bot.HasInMonstersZone(CardId.MetalflameSwordsman, true);
            bool canNewSwordsman = !haveSwordsman && HasMaxSummonCandidate(CardId.MetalflameSwordsman);

            // IMPORTANT: prevent "tribute Swordsman -> summon Swordsman" loops.
            return HasMaxSummonCandidate(CardId.RedEyesBlackFullmetalDragon)
                || HasMaxSummonCandidate(CardId.MetalzoaX)
                || canNewSwordsman;
        }

        // PURPOSE: avoid firing Flame when no deck target exists.
        private bool HasMeaningfulMetallizeUpgradeFromDeck()
        {
            bool haveSwordsman = Bot.HasInMonstersZone(CardId.MetalflameSwordsman, true);
            bool canNewSwordsman = !haveSwordsman && CheckRemainInDeck(CardId.MetalflameSwordsman) > 0;

            return CheckRemainInDeck(CardId.RedEyesBlackFullmetalDragon, CardId.MetalzoaX) > 0
                || canNewSwordsman;
        }

        // PURPOSE: avoid spending multiple Max/Flame activations on the opponent's turn.
        // TRUSTS: opponent-turn Metallize is very powerful, but redundant converts waste resources.
        private int GetOppTurnMetallizeLimit()
        {
            // Conservative by default; allow a second conversion only under high pressure.
            return EnemyHighPressure() ? 2 : 1;
        }

        private bool OppTurnMetallizeBudgetAllows()
        {
            if (Duel.Player != 1) return true;
            return _oppTurnMetallizeResolvedCount < GetOppTurnMetallizeLimit();
        }

        private void MarkOppTurnMetallizePending(int cardId)
        {
            if (Duel.Player != 1) return;
            _pendingOppTurnMetallizeId = cardId;
        }

        // ------------------------------------------------------------
        // Logging helper
        // ------------------------------------------------------------
        private void MMLog(string tag, string msg)
        {
            Logger.DebugWriteLine(tag + " " + msg + " (phase=" + Duel.Phase + ", pl=" + Duel.Player + ")");
        }

        public override int OnSelectPlace(int cardId, int player, CardLocation location, int available)
        {
            if (player == 0 && location == CardLocation.MonsterZone && cardId == CardId.DharcTheDarkCharmerGloomy)
            {
                bool leftOk = (available & Zones.z5) != 0 && (Bot.MonsterZone[0] == null || Bot.MonsterZone[2] == null);
                bool rightOk = (available & Zones.z6) != 0 && (Bot.MonsterZone[2] == null || Bot.MonsterZone[4] == null);
                if (leftOk && !rightOk) return Zones.z5;
                if (rightOk && !leftOk) return Zones.z6;
                if (leftOk) return Zones.z5;
                if (rightOk) return Zones.z6;
            }

            if (player == 0 && location == CardLocation.SpellZone)
            {
                int pick = ChooseSTPlace(available, Card, true, null);
                if ((pick & available) != 0) return pick;
            }
            return base.OnSelectPlace(cardId, player, location, available);
        }
        public override int OnSelectOption(IList<int> options)
        {
            ChainInfo currentSolvingChain = Duel.GetCurrentSolvingChainInfo();

            // 1190=Add to Hand, 1152=Special Summon.
            if (options != null && options.Count == 2 && options.Contains(1190) && options.Contains(1152))
            {
                CardLocation summonLoc = CardLocation.Hand | CardLocation.Deck | CardLocation.Extra | CardLocation.Grave | CardLocation.Removed;
                if (currentSolvingChain != null)
                {
                    switch (currentSolvingChain.ActivateId)
                    {
                        case CardId.BystialMagnamhut:
                        case CardId.BystialDruiswurm:
                        case CardId.ZoaTheFiendishBeast:
                        case CardId.RedEyesMetalClawsDragon:
                        case CardId.RedEyesDarknessMetalDragon:
                            summonLoc = CardLocation.Hand;
                            break;
                        case CardId.TheBystialLubellion:
                            summonLoc = CardLocation.Grave;
                            break;
                    }
                }

                return CheckShouldNoMoreSpSummon(summonLoc) ? options.IndexOf(1190) : options.IndexOf(1152);
            }

            // 1190=Add to Hand, 1153=Set.
            if (options != null && options.Count == 2 && options.Contains(1190) && options.Contains(1153))
            {
                bool setForHandLimit = Duel.Player == 0 && Bot.Hand.Count >= 6;
                return setForHandLimit ? options.IndexOf(1153) : options.IndexOf(1190);
            }

            return base.OnSelectOption(options);
        }

        private int ChooseSTPlace(int available, ClientCard card, bool avoidImpermanence, List<int> avoid)
        {
            if (card == null) card = Card;
            if (available == 0) available = 0x1f;

            List<int> free = new List<int>();
            for (int i = 0; i < 5; ++i)
            {
                int zone = 1 << i;
                if ((available & zone) == 0) continue;
                if (Bot.SpellZone[i] != null) continue;
                if (avoidImpermanence && _impermDangerColumns.Contains(i)) continue;
                if (avoid != null && avoid.Contains(i)) continue;
                free.Add(i);
            }

            // Relax avoid filters before giving up.
            if (free.Count == 0)
            {
                for (int i = 0; i < 5; ++i)
                {
                    int zone = 1 << i;
                    if ((available & zone) == 0) continue;
                    if (Bot.SpellZone[i] == null) free.Add(i);
                }
            }

            for (int n = free.Count - 1; n > 0; --n)
            {
                int j = Program.Rand.Next(n + 1);
                int tmp = free[n];
                free[n] = free[j];
                free[j] = tmp;
            }
            return free.Count > 0 ? (1 << free[0]) : 0;
        }

        private void SelectSTPlace(ClientCard card, bool avoidImpermanence, List<int> avoid)
        {
            int pick = ChooseSTPlace(0x1f, card, avoidImpermanence, avoid);
            AI.SelectPlace(pick);
        }

        private bool MentionsMax(ClientCard m)
        {
            if (m == null || !m.IsFaceup()) return false;

            // Fallback: explicit list for monsters that mention "Max Metalmorph"
            return m.IsCode(CardId.MetalIllusionist)
                || m.IsCode(CardId.RedEyesMetalClawsDragon)
                || m.IsCode(CardId.MetalflameSwordsman)
                || m.IsCode(CardId.RedEyesBlackFullmetalDragon)
                || m.IsCode(CardId.MetalzoaX)
                || m.IsCode(CardId.ZoaTheFiendishBeast);
        }

        private static bool IsMetalmorphTrapId(int id)
            => id == CardId.MaxMetalmorph || id == CardId.FlameCoatingMetalmorph || id == CardId.TimeEngine;

        private bool CanSpareFacedownMetalmorphForClaws(ClientCard card)
        {
            if (card == null || !IsMetalmorphTrapId(card.Id)) return false;
            int sameFd = Bot.SpellZone.Count(s => s != null && s.IsFacedown() && s.IsCode(card.Id));
            if (sameFd >= 2 || Bot.HasInHand(card.Id)) return true;

            bool haveOtherCoverage =
                card.IsCode(CardId.MaxMetalmorph)
                    ? (HasMetalmorphTrapCoverage(CardId.FlameCoatingMetalmorph) || HasMetalmorphTrapCoverage(CardId.TimeEngine))
                    : card.IsCode(CardId.FlameCoatingMetalmorph)
                        ? (HasMetalmorphTrapCoverage(CardId.MaxMetalmorph) || HasMetalmorphTrapCoverage(CardId.TimeEngine))
                        : (HasMetalmorphTrapCoverage(CardId.MaxMetalmorph) || HasMetalmorphTrapCoverage(CardId.FlameCoatingMetalmorph));

            bool canReplace = CheckRemainInDeck(card.Id) > 0;
            return haveOtherCoverage && (canReplace || !EnemyHighPressure());
        }

        // PURPOSE: allow "Fullmetal set -> Claws send lone Max -> SS Claws -> add back -> set+activate same turn" bridge.
        // TRUSTS: sending the only facedown Max is acceptable when Claws is in hand and Max can immediately convert into a payoff.
        // RESET: stateless helper.
        private bool ShouldUseLoneMetalmorphAsClawsCostNow(ClientCard facedown)
        {
            if (facedown == null) return false;
            if (!facedown.IsFacedown() || facedown.IsMonster()) return false;
            if (!facedown.IsCode(CardId.MaxMetalmorph)) return false; // keep narrow (combo bridge)

            if (Duel.Player != 0) return false;
            if (Duel.Phase != DuelPhase.Main1 && Duel.Phase != DuelPhase.Main2) return false;

            if (!Bot.HasInHand(CardId.RedEyesMetalClawsDragon)) return false;
            if (Bot.HasInMonstersZone(CardId.RedEyesMetalClawsDragon, true)) return false;
            if (CheckShouldNoMoreSpSummon(CardLocation.Hand)) return false;
            if (Bot.GetMonsterCount() >= 5) return false;

            // Don't detour if we still need to commit to the REDMD->Seal line.
            if (ShouldCommitToRedmdSealLineNow()) return false;

            // Only spend our lone Max if it can immediately convert into a real payoff.
            if (!HasMeaningfulMetallizeUpgradeForMax()) return false;

            return true;
        }

        // Helper: allow Claws cost from facedown S/T, preferring non-Metalmorph but permitting spare Metalmorphs.
        private bool HasFacedownSendableForClaws()
        {
            for (int i = 0; i < 5; ++i)
            {
                var s = Bot.SpellZone[i];
                if (s == null || !s.IsFacedown() || s.IsMonster()) continue;

                if (!IsMetalmorphTrapId(s.Id) || CanSpareFacedownMetalmorphForClaws(s) || ShouldUseLoneMetalmorphAsClawsCostNow(s))
                    return true;
            }
            return false;
        }


        // ============================================================
        // Lifecycle
        // ============================================================
        public override void OnNewTurn()
        {
            base.OnNewTurn();

            _acceptedPromptSource = null;

            _pendingMagnamhutEndPhaseQueuePrompt = false;
            _impermDangerColumns.Clear();
            _enemyActivateInfiniteImpermanenceFromHand = false;
            _pendingEnemyImpermMirrorColumn = -1;
            _pendingEnemyImpermFromHand = false;

            _calledByUsedThisChain = false;
            _lastMetallizationWasWarrior5p = false;
            _lastMetallizeRace = 0;
            _lastMetallizeLevel = 0;
            _lastMetallizeTributeCode = 0;
            _lastOwnLikelyDestroyedMonster = 0;
            _tributedThisTurn = false;
            _stardustDragonTributedThisTurn = false;
            _stardustWarriorNegateUsedThisTurn = false;
            _stardustWarriorLeftByOpponentThisTurn = false;
            _preferSatelliteFromAccel = false;
            _pendingHopEarTargetId = 0;
            _pendingHopEarTargetLevel = 0;
            _pendingHopEarPreferBaronne = false;
            _pendingHopEarPreferAccel = false;
            _pendingFidraulisRevealPrompt = false;
            _pendingFidraulisSendPrompt = false;
            _pendingFidraulisDestroyPrompt = false;
            _pendingFidraulisRevealDesiredCount = 0;
            _pendingFidraulisRevealPickedIds.Clear();
            _pendingVictimSanctuarySummonPrompt = false;
            if (dimensionShifterCount > 0) dimensionShifterCount--;
            currentNegateCardList.Clear();
            enemyPlaceThisTurn.Clear();
            summonThisTurn.Clear();
            placedThisTurn.Clear();

            // Reset opponent-turn Metallize budget at the start of each opponent turn.
            if (Duel.Player == 1)
            {
                _oppTurnMetallizeResolvedCount = 0;
                _pendingOppTurnMetallizeId = 0;
            }

            MMLog("[MM.STATE]", "turn reset");
        }

        public override void OnChaining(int player, ClientCard card)
        {
            _acceptedPromptSource = null;
            if (player == 1 && card != null && card.Id == CardId.InfiniteImpermanence)
            {
                _pendingEnemyImpermMirrorColumn = -1;
                _pendingEnemyImpermFromHand = false;

                if (_enemyActivateInfiniteImpermanenceFromHand)
                {
                    _pendingEnemyImpermFromHand = true;
                    _enemyActivateInfiniteImpermanenceFromHand = false;
                }
                else
                {
                    int seq = -1;
                    if (card.Location == CardLocation.SpellZone && card.Sequence >= 0 && card.Sequence <= 4)
                    {
                        seq = card.Sequence;
                    }
                    else
                    {
                        for (int i = 0; i < 5; i++)
                        {
                            ClientCard z = Enemy.SpellZone[i];
                            if (z != null && z.IsFaceup() && z.IsCode(CardId.InfiniteImpermanence))
                            {
                                seq = i;
                                break;
                            }
                        }
                    }

                    if (seq >= 0 && seq <= 4)
                        _pendingEnemyImpermMirrorColumn = 4 - seq;
                }
            }

            base.OnChaining(player, card);
        }

        public override void OnChainSolved(int chainIndex)
        {
            ChainInfo solving = Duel.GetCurrentSolvingChainInfo();

            if (solving != null && solving.ActivatePlayer == 0
                && solving.IsActivateCode(CardId.BystialMagnamhut)
                && solving.ActivateLocation == CardLocation.MonsterZone
                && !Duel.IsCurrentSolvingChainNegated())
            {
                _pendingMagnamhutEndPhaseQueuePrompt = true;
            }

            // Track successful opponent-turn Metallize resolutions (Max/Flame) to enforce a per-turn budget.
            if (solving != null && Duel.Player == 1 && _pendingOppTurnMetallizeId != 0
                && solving.ActivatePlayer == 0 && solving.IsActivateCode(_pendingOppTurnMetallizeId))
            {
                if (!Duel.IsCurrentSolvingChainNegated())
                {
                    _oppTurnMetallizeResolvedCount++;
                    MMLog("[MM.Metallize]", "resolved opp-turn id=" + _pendingOppTurnMetallizeId + " count=" + _oppTurnMetallizeResolvedCount);
                }
                _pendingOppTurnMetallizeId = 0;
            }

            if (solving != null && solving.ActivatePlayer == 1 && solving.IsActivateCode(CardId.InfiniteImpermanence))
            {
                if (!Duel.IsCurrentSolvingChainNegated())
                {
                    if (_pendingEnemyImpermMirrorColumn >= 0 && _pendingEnemyImpermMirrorColumn <= 4)
                    {
                        _impermDangerColumns.Add(_pendingEnemyImpermMirrorColumn);
                        MMLog("[MM.IMPERM]", "commit danger column=" + _pendingEnemyImpermMirrorColumn);
                    }
                    else if (_pendingEnemyImpermFromHand)
                    {
                        // Activated from hand: no mirrored danger column to record.
                        MMLog("[MM.IMPERM]", "resolved from hand (no column lock)");
                    }
                }

                _pendingEnemyImpermMirrorColumn = -1;
                _pendingEnemyImpermFromHand = false;
            }
            if (solving != null && !Duel.IsCurrentSolvingChainNegated())
            {
                if (solving.IsActivateCode(_CardId.DimensionShifter))
                    dimensionShifterCount = 2;
            }

            base.OnChainSolved(chainIndex);
        }

        public override void OnChainEnd()
        {
            _acceptedPromptSource = null;
            currentNegateCardList.Clear();
            _pendingEnemyImpermMirrorColumn = -1;
            _pendingEnemyImpermFromHand = false;
            _enemyActivateInfiniteImpermanenceFromHand = false;
            _preferSatelliteFromAccel = false;
            _pendingHopEarTargetId = 0;
            _pendingHopEarTargetLevel = 0;
            _pendingHopEarPreferBaronne = false;
            _pendingHopEarPreferAccel = false;
            _pendingFidraulisRevealPrompt = false;
            _pendingFidraulisSendPrompt = false;
            _pendingFidraulisDestroyPrompt = false;
            _pendingFidraulisRevealDesiredCount = 0;
            _pendingFidraulisRevealPickedIds.Clear();
            _pendingVictimSanctuarySummonPrompt = false;
            _pendingOppTurnMetallizeId = 0;
            for (int idx = enemyPlaceThisTurn.Count - 1; idx >= 0; idx--)
            {
                ClientCard checkTarget = enemyPlaceThisTurn[idx];
                if (checkTarget == null || !checkTarget.IsOnField())
                    enemyPlaceThisTurn.RemoveAt(idx);
            }
            for (int idx = summonThisTurn.Count - 1; idx >= 0; idx--)
            {
                ClientCard checkTarget = summonThisTurn[idx];
                if (checkTarget == null || checkTarget.Location != CardLocation.MonsterZone)
                    summonThisTurn.RemoveAt(idx);
            }
            for (int idx = placedThisTurn.Count - 1; idx >= 0; idx--)
            {
                ClientCard checkTarget = placedThisTurn[idx];
                if (checkTarget == null || checkTarget.Location != CardLocation.SpellZone)
                    placedThisTurn.RemoveAt(idx);
            }
            base.OnChainEnd();
            _calledByUsedThisChain = false;
        }

        public override void OnNewPhase()
        {
            _acceptedPromptSource = null;
            base.OnNewPhase();
        }

        public override void OnSpSummoned()
        {
            _acceptedPromptSource = null;
            base.OnSpSummoned();
        }

        public override void OnMove(ClientCard card, int pc, int pl, int cc, int cl)
        {
            if (card != null)
            {
                if (cc == 1 && (cl == (int)CardLocation.MonsterZone || cl == (int)CardLocation.SpellZone))
                {
                    if (!enemyPlaceThisTurn.Contains(card))
                        enemyPlaceThisTurn.Add(card);
                }
                if (pc == 0 && pl == (int)CardLocation.MonsterZone && cl != (int)CardLocation.MonsterZone)
                {
                    summonThisTurn.Remove(card);
                }
                if (cc == 0 && cl == (int)CardLocation.MonsterZone)
                {
                    if (!summonThisTurn.Contains(card))
                        summonThisTurn.Add(card);
                }
                if (cc == 0 && pl == (int)CardLocation.SpellZone && cl != (int)CardLocation.SpellZone)
                {
                    placedThisTurn.Remove(card);
                }
                if (cc == 0 && cl == (int)CardLocation.SpellZone)
                {
                    if (!placedThisTurn.Contains(card))
                        placedThisTurn.Add(card);
                }
            }

            bool ownMzoneToGy = card != null
                                && pc == 0
                                && pl == (int)CardLocation.MonsterZone
                                && cc == 0
                                && cl == (int)CardLocation.Grave;
            bool likelyOpponentDestroyWindow =
                Duel.LastChainPlayer == 1
                || (Duel.Player == 1 && (Duel.Phase == DuelPhase.BattleStart || Duel.Phase == DuelPhase.Battle
                                         || Duel.Phase == DuelPhase.Main1 || Duel.Phase == DuelPhase.Main2));
            if (ownMzoneToGy && likelyOpponentDestroyWindow)
            {
                _lastOwnLikelyDestroyedMonster = card.Id;
            }

            if (card != null && card.Id == CardId.StardustWarrior
                && pc == 0 && pl == (int)CardLocation.MonsterZone
                && (cc == 0 || cc == 1))
            {
                bool byOpponentWindow = Duel.LastChainPlayer == 1
                    || (Duel.Player == 1 && (Duel.Phase == DuelPhase.BattleStart || Duel.Phase == DuelPhase.Battle
                                          || Duel.Phase == DuelPhase.Main1 || Duel.Phase == DuelPhase.Main2));
                if (byOpponentWindow) _stardustWarriorLeftByOpponentThisTurn = true;
            }

            if (card != null && card.Id == CardId.InfiniteImpermanence
                && pl == (int)CardLocation.Hand && cl == (int)CardLocation.SpellZone
                && !card.IsFacedown() && cc == 1)
            {
                _enemyActivateInfiniteImpermanenceFromHand = true;
            }
            base.OnMove(card, pc, pl, cc, cl);
        }

        public override bool OnSelectHand() => true;

        public override CardPosition OnSelectPosition(int cardId, IList<CardPosition> positions)
        {
            // PURPOSE: monsters summoned by our Seal float should enter defense, since Seal sets their ATK to 0.
            // TRUSTS: position prompt occurs while Seal is the current/last resolving chain context.
            // RESET: stateless helper.
            var solving = Duel.GetCurrentSolvingChainCard();
            var last = Util.GetLastChainCard();
            bool sealContext =
                (solving != null && solving.Controller == 0 && solving.IsCode(CardId.HieraticSealOfTheHeavenlySpheres))
                || (last != null && last.Controller == 0 && last.IsCode(CardId.HieraticSealOfTheHeavenlySpheres));

            if (sealContext && positions != null && positions.Count > 0)
            {
                if (positions.Contains(CardPosition.FaceUpDefence))
                    return CardPosition.FaceUpDefence;
                if (positions.Contains(CardPosition.FaceDownDefence))
                    return CardPosition.FaceDownDefence;
            }

            return base.OnSelectPosition(cardId, positions);
        }

        // ============================================================
        // Executors — Starters & Lines
        // ============================================================
        private bool Summon_Illusionist()
        {
            if (Duel.Player != 0) return false;
            if (Duel.Phase != DuelPhase.Main1 && Duel.Phase != DuelPhase.Main2) return false;
            if (!IHaveRoomST()) return false;
            if (Bot.GetMonsterCount() >= 5) return false;
            if (Bot.HasInMonstersZone(CardId.MetalIllusionist)) return false;
            MMLog("[MM.NS.Illusionist]", "normal summon");
            return true;
        }
        private bool Illusionist_SetMetalmorph()
        {
            if (Card == null || Card.Location != CardLocation.MonsterZone) return false;
            bool summonTrigger = ActivateDescription == Util.GetStringId(CardId.MetalIllusionist, 0)
                || (AI.SelectingEffectYn && ActivateDescription == -1 && Duel.LastSummonedCards.Contains(Card));
            if (!summonTrigger || !IHaveRoomST()) return false;
            MMLog("[MM.Illusionist]", "summon trigger: set Metalmorph");
            return true;
        }

        private bool Illusionist_DeclareDragon()
        {
            if (ActivateDescription != Util.GetStringId(CardId.MetalIllusionist, 1)) return false;
            if (Card == null || Card.Location != CardLocation.MonsterZone || Card.HasRace(CardRace.Dragon)) return false;
            if (!Bot.HasInMonstersZone(CardId.MetalIllusionist, true)) return false;
            if (Duel.Player != 0) return false;
            if (Duel.Phase != DuelPhase.Main1 && Duel.Phase != DuelPhase.Main2) return false;
            AI.SelectRace(CardRace.Dragon);
            MMLog("[MM.Illusionist]", "declare Dragon");
            return true;
        }

        // --- Zoa (B/G) ---
        private bool Zoa_SpecialFromHand()
        {
            if (Duel.Player != 0) return false;
            if (Duel.Phase != DuelPhase.Main1 && Duel.Phase != DuelPhase.Main2) return false;
            if (Card == null || Card.Id != CardId.ZoaTheFiendishBeast) return false;
            if (Card.Location != CardLocation.Hand) return false;
            if (CheckShouldNoMoreSpSummon(CardLocation.Hand)) return false;
            if (Bot.GetMonsterCount() >= 5) return false;
            MMLog("[MM.Zoa.SS]", "special summon from hand");
            return true;
        }
        private bool Summon_Zoa_NoTributeIfOppHasMonster()
        {
            if (Duel.Player != 0) return false;
            if (Duel.Phase != DuelPhase.Main1 && Duel.Phase != DuelPhase.Main2) return false;
            if (Enemy.GetMonsterCount() == 0) return false;
            MMLog("[MM.NS.Zoa]", "normal summon (opp controls monster)");
            return true;
        }
        private bool Zoa_SetMetalmorphFromDeck()
        {
            if (!Bot.HasInMonstersZone(CardId.ZoaTheFiendishBeast, true)) return false;
            if (Duel.Player != 0) return false;
            if (Duel.Phase != DuelPhase.Main1 && Duel.Phase != DuelPhase.Main2) return false;
            MMLog("[MM.Zoa.Set]", "set Metalmorph from Deck");
            return true;
        }

        // --- Opp turn Max (B/D/A) ---
        private bool MaxMetalmorph_OppTurn_IllusionistOrZoa()
        {
            if (Duel.Player != 1) return false;
            if (!(Duel.Phase == DuelPhase.Draw || Duel.Phase == DuelPhase.Standby ||
                  Duel.Phase == DuelPhase.Main1 || Duel.Phase == DuelPhase.BattleStart ||
                  Duel.Phase == DuelPhase.Battle || Duel.Phase == DuelPhase.Main2 ||
                  Duel.Phase == DuelPhase.End)) return false;
            if (Card == null || Card.Id != CardId.MaxMetalmorph) return false;
            if (Card.Location != CardLocation.SpellZone || !Card.IsFacedown()) return false;

            if (!OppTurnMetallizeBudgetAllows()) return false;

            // Only flip if we actually have a safe tribute.
            if (!HasSafeTributeForMetallize()) return false;
            if (!HasMeaningfulMetallizeUpgradeForMax()) return false;

            bool zoaUp = Bot.HasInMonstersZone(CardId.ZoaTheFiendishBeast, true);
            bool illuUp = Bot.HasInMonstersZone(CardId.MetalIllusionist, true);
            if (!zoaUp && !illuUp) return false;

            bool haveFull = Bot.HasInMonstersZone(CardId.RedEyesBlackFullmetalDragon, true);
            bool haveMx = Bot.HasInMonstersZone(CardId.MetalzoaX, true);

            int preferId =
                  (!haveFull && HasMaxSummonCandidate(CardId.RedEyesBlackFullmetalDragon)) ? CardId.RedEyesBlackFullmetalDragon
                : (!haveMx && HasMaxSummonCandidate(CardId.MetalzoaX)) ? CardId.MetalzoaX
                : (zoaUp && HasMaxSummonCandidate(CardId.MetalzoaX)) ? CardId.MetalzoaX
                : (HasMaxSummonCandidate(CardId.RedEyesBlackFullmetalDragon)) ? CardId.RedEyesBlackFullmetalDragon
                : (HasMaxSummonCandidate(CardId.MetalzoaX)) ? CardId.MetalzoaX
                : 0;
            if (!HasMaxSummonCandidate(preferId)) return false;

            MarkOppTurnMetallizePending(Card.Id);
            MMLog("[MM.Max.Opp]", "convert via Illusionist/Zoa target=" + preferId);
            return true;
        }

        private bool MaxMetalmorph_OppTurn_Bystial()
        {
            if (Duel.Player != 1) return false;
            if (!(Duel.Phase == DuelPhase.Draw || Duel.Phase == DuelPhase.Standby ||
                  Duel.Phase == DuelPhase.Main1 || Duel.Phase == DuelPhase.BattleStart ||
                  Duel.Phase == DuelPhase.Battle || Duel.Phase == DuelPhase.Main2 ||
                  Duel.Phase == DuelPhase.End)) return false;
            if (Card == null || Card.Id != CardId.MaxMetalmorph) return false;
            if (Card.Location != CardLocation.SpellZone || !Card.IsFacedown()) return false;

            if (!OppTurnMetallizeBudgetAllows()) return false;

            // Only flip if we actually have a safe tribute.
            if (!HasSafeTributeForMetallize()) return false;
            if (!HasMaxSummonCandidate(CardId.RedEyesBlackFullmetalDragon)) return false;

            bool bystialUp = Bot.HasInMonstersZone(CardId.BystialMagnamhut, true) ||
                             Bot.HasInMonstersZone(CardId.BystialDruiswurm, true);
            if (!bystialUp) return false;

            MarkOppTurnMetallizePending(Card.Id);
            MMLog("[MM.Max.Opp]", "convert via Bystial");
            return true;
        }

        private bool MaxMetalmorph_OppTurn_Generic()
        {
            if (Duel.Player != 1) return false;
            if (Card == null || Card.Id != CardId.MaxMetalmorph) return false;
            if (Card.Location != CardLocation.SpellZone || !Card.IsFacedown()) return false;

            if (!OppTurnMetallizeBudgetAllows()) return false;

            // include End Phase so we don't miss the last window
            if (!(Duel.Phase == DuelPhase.Draw || Duel.Phase == DuelPhase.Standby ||
                  Duel.Phase == DuelPhase.Main1 || Duel.Phase == DuelPhase.BattleStart ||
                  Duel.Phase == DuelPhase.Battle || Duel.Phase == DuelPhase.Main2 ||
                  Duel.Phase == DuelPhase.End)) return false;

            // Safety: only flip if we have some acceptable tribute
            if (!HasSafeTributeForMetallize()) return false;
            if (!HasMeaningfulMetallizeUpgradeForMax()) return false;

            bool haveFull = Bot.HasInMonstersZone(CardId.RedEyesBlackFullmetalDragon, true);
            bool haveMx = Bot.HasInMonstersZone(CardId.MetalzoaX, true);
            bool zoaUp = Bot.HasInMonstersZone(CardId.ZoaTheFiendishBeast, true);

            int preferId =
                  (!haveFull && HasMaxSummonCandidate(CardId.RedEyesBlackFullmetalDragon)) ? CardId.RedEyesBlackFullmetalDragon
                : (!haveMx && HasMaxSummonCandidate(CardId.MetalzoaX)) ? CardId.MetalzoaX
                : (zoaUp && HasMaxSummonCandidate(CardId.MetalzoaX)) ? CardId.MetalzoaX
                : (HasMaxSummonCandidate(CardId.RedEyesBlackFullmetalDragon)) ? CardId.RedEyesBlackFullmetalDragon
                : (HasMaxSummonCandidate(CardId.MetalzoaX)) ? CardId.MetalzoaX
                : 0;
            if (!HasMaxSummonCandidate(preferId)) return false;

            MarkOppTurnMetallizePending(Card.Id);
            MMLog("[MM.Max.Opp]", "generic convert target=" + preferId);
            return true;
        }

        // --- Flame (opp turn) C ---
        private bool FlameCoating_OppTurn_Generic()
        {
            if (Duel.Player != 1) return false;
            if (!(Duel.Phase == DuelPhase.Draw || Duel.Phase == DuelPhase.Standby ||
                  Duel.Phase == DuelPhase.Main1 || Duel.Phase == DuelPhase.BattleStart ||
                  Duel.Phase == DuelPhase.Battle || Duel.Phase == DuelPhase.Main2 ||
                  Duel.Phase == DuelPhase.End)) return false;

            if (Card == null || Card.Id != CardId.FlameCoatingMetalmorph) return false;
            if (Card.Location != CardLocation.SpellZone || !Card.IsFacedown()) return false;
            if (!HasSafeTributeForMetallize()) return false;

            if (!OppTurnMetallizeBudgetAllows()) return false;

            bool haveMentionBody = Bot.GetMonsters().Any(m => m != null && m.IsFaceup() && MentionsMax(m) && !IsBoss(m))
                                || Bot.GetMonsters().Any(m => m != null && m.IsFaceup() && MentionsMax(m));
            if (!haveMentionBody) return false;

            bool haveFull = Bot.HasInMonstersZone(CardId.RedEyesBlackFullmetalDragon, true);
            bool haveMx = Bot.HasInMonstersZone(CardId.MetalzoaX, true);
            bool haveSwordsman = Bot.HasInMonstersZone(CardId.MetalflameSwordsman, true);
            bool canFull = HasMaxSummonCandidate(CardId.RedEyesBlackFullmetalDragon);
            bool canMx = HasMaxSummonCandidate(CardId.MetalzoaX);
            // IMPORTANT: don't "upgrade" into a duplicate Swordsman when we already have one.
            bool canSwordsman = !haveSwordsman && CheckRemainInDeck(CardId.MetalflameSwordsman) > 0;
            if (!canFull && !canMx && !canSwordsman) return false;

            // PURPOSE: keep Max as primary route for Fullmetal/Metalzoa when both traps are live.
            bool maxSetAndLive = HasSet(CardId.MaxMetalmorph) && HasSafeTributeForMetallize() && HasMeaningfulMetallizeUpgradeForMax();
            bool maxAdvancesCore = (!haveFull && HasMaxSummonCandidate(CardId.RedEyesBlackFullmetalDragon))
                                || (!haveMx && HasMaxSummonCandidate(CardId.MetalzoaX));
            if (maxSetAndLive && maxAdvancesCore) return false;

            int targetId =
                  (canSwordsman) ? CardId.MetalflameSwordsman
                : (!haveFull && canFull) ? CardId.RedEyesBlackFullmetalDragon
                : (!haveMx && canMx) ? CardId.MetalzoaX
                : (canSwordsman ? CardId.MetalflameSwordsman
                   : (canFull ? CardId.RedEyesBlackFullmetalDragon
                      : (canMx ? CardId.MetalzoaX : 0)));
            if (targetId == 0) return false;

            MarkOppTurnMetallizePending(Card.Id);
            MMLog("[MM.Flame.Opp]", "convert target=" + targetId);
            return true;
        }

        // --- Flame (my turn) generic ---
        private bool FlameCoating_MyTurn_Generic()
        {
            if (Duel.Player != 0) return false;
            if (Duel.Phase != DuelPhase.Main1 && Duel.Phase != DuelPhase.Main2) return false;
            if (Card == null || Card.Id != CardId.FlameCoatingMetalmorph) return false;
            if (Card.Location != CardLocation.SpellZone || !Card.IsFacedown()) return false;

            if (!HasSafeTributeForMetallize()) return false;
            if (!HasMeaningfulMetallizeUpgradeFromDeck()) return false;

            // ⬇️ use the top-level helper here
            if (!Bot.GetMonsters().Any(MentionsMax)) return false;

            // PURPOSE: don't burn Claws as the only tribute just to force a low-value convert.
            // TRUSTS: Claws is usually better kept for trap recursion/board pressure.
            var mentionPool = Bot.GetMonsters().Where(c => c != null && c.IsFaceup() && MentionsMax(c)).ToList();
            bool haveSafeMentionTribute = mentionPool.Any(c => !IsBoss(c));
            if (!haveSafeMentionTribute && !EnemyHighPressure()) return false;
            bool onlyClawsTribute = mentionPool.Count > 0 && mentionPool.All(c => c.IsCode(CardId.RedEyesMetalClawsDragon));
            if (onlyClawsTribute && !EnemyHighPressure()) return false;

            bool haveSwordsmanInDeck = CheckRemainInDeck(CardId.MetalflameSwordsman) > 0;
            bool haveSwordsmanOnField = Bot.HasInMonstersZone(CardId.MetalflameSwordsman, true);
            bool haveClawsOnField = Bot.HasInMonstersZone(CardId.RedEyesMetalClawsDragon, true);
            bool haveBossOnField = Bot.HasInMonstersZone(CardId.RedEyesBlackFullmetalDragon, true)
                                || Bot.HasInMonstersZone(CardId.MetalzoaX, true);
            bool haveMxOnField = Bot.HasInMonstersZone(CardId.MetalzoaX, true);
            bool haveZoaOnField = Bot.HasInMonstersZone(CardId.ZoaTheFiendishBeast, true);
            bool underPressure = EnemyHighPressure();
            bool needRemoval = Enemy.GetMonsters().Count(m => m != null && m.IsFaceup()) >= 2
                               || Enemy.GetSpells().Any(s => s != null && s.IsFaceup());
            bool haveMaxCoreOnline = HasSet(CardId.MaxMetalmorph) || Bot.HasInHand(CardId.MaxMetalmorph) || haveBossOnField;

            int targetId;
            bool pushSwordsman = !haveSwordsmanOnField
                              && haveSwordsmanInDeck
                              && !haveBossOnField
                              && (haveMaxCoreOnline || !underPressure)
                              && !needRemoval;
            bool pushMxViaZoa = haveZoaOnField && !haveMxOnField && CheckRemainInDeck(CardId.MetalzoaX) > 0;
            if (pushSwordsman)
            {
                targetId = CardId.MetalflameSwordsman;
            }
            else if (pushMxViaZoa)
            {
                targetId = CardId.MetalzoaX;
            }
            else
            {
                targetId = needRemoval ? CardId.MetalzoaX : CardId.RedEyesBlackFullmetalDragon;
                if (CheckRemainInDeck(targetId) == 0)
                    targetId = (targetId == CardId.MetalzoaX ? CardId.RedEyesBlackFullmetalDragon : CardId.MetalzoaX);
            }

            MMLog("[MM.Flame.My]", "convert target=" + targetId);
            return true;
        }

        private bool Flame_DestroyReplace()
        {
            // c91152455.lua uses SelectEffectYesNo(tp, c, 96), not HINTMSG_DESTROY_REPLACE.
            bool replace = Card != null && Card.Location == CardLocation.SpellZone
                && Card.IsFaceup() && ActivateDescription == 96;
            if (replace) MMLog("[MM.Flame.Replace]", "destroy replacement");
            return replace;
        }

        // --- Fullmetal (negate/burn + set) ---
        private bool Fullmetal_Negate()
        {
            if (Duel.LastChainPlayer != 1) return false;
            if (!Bot.HasInMonstersZone(CardId.RedEyesBlackFullmetalDragon, true)) return false;
            if (CheckWhetherNegated(true, true, CardType.Monster)) return false;
            if (Duel.IsCurrentSolvingChainNegated()) return false;
            ChainInfo currentChain = Duel.GetCurrentSolvingChainInfo();
            if (currentChain != null && currentChain.ActivatePlayer == 1 && currentChain.RelatedCard != null
                && !CheckCardShouldNegate(currentChain) && !EnemyHighPressure())
            {
                return false;
            }
            ClientCard last = Util.GetLastChainCard();
            if (last != null && last.Controller == 1 && !CheckCardShouldNegate(last) && !EnemyHighPressure())
            {
                return false;
            }

            bool haveBurnTarget = Enemy.GetMonsters().Any(m => m != null && m.IsFaceup() && m.IsAttack() && m.BaseAttack > 0);
            MMLog("[MM.Fullmetal.Negate]", "activate burnTarget=" + (haveBurnTarget ? "yes" : "no"));
            return true;
        }
        private bool Fullmetal_SetMetalmorph()
        {
            if (Duel.Player != 0) return false;
            if (Duel.Phase != DuelPhase.Main1 && Duel.Phase != DuelPhase.Main2) return false;
            if (Card == null || Card.Id != CardId.RedEyesBlackFullmetalDragon) return false;
            if (Card.Location != CardLocation.Hand) return false;
            if (!IHaveRoomST()) return false;

            bool canSetAny = CheckRemainInDeck(CardId.MaxMetalmorph, CardId.FlameCoatingMetalmorph, CardId.TimeEngine) > 0;
            if (!canSetAny) return false;

            if (Card.Location == CardLocation.Hand)
            {
                if (!WantsMetalmorphTrapSetupNow()) return false;
                MMLog("[MM.Fullmetal.Reveal]", "reveal/set Metalmorph from hand");
                return true;
            }

            return false;
        }

        // --- BMD → Striker → REDMD → Seal ---
        private bool BMD_Summon()
        {
            if (Duel.Player != 0) return false;
            if (Duel.Phase != DuelPhase.Main1 && Duel.Phase != DuelPhase.Main2) return false;
            if (Bot.HasInMonstersZone(CardId.BlackMetalDragon)) return false;
            MMLog("[MM.NS.BMD]", "normal summon");
            return true;
        }
        private bool BMD_Link1_Striker()
        {
            if (!Bot.HasInMonstersZone(CardId.BlackMetalDragon, true)) return false;
            if (CheckShouldNoMoreSpSummon(CardLocation.Extra)) return false;
            bool forceCoreLine = ShouldCommitToRedmdSealLineNow();
            if (!forceCoreLine && GetSpecialSummonDrawCount(CardLocation.Extra) > 1 && CheckAtAdvantage()) return false;
            MMLog("[MM.BMD.Link]", "link summon Striker Dragon");
            return true;
        }

        private bool BMD_Grave_Search()
        {
            if (Card == null || Card.Id != CardId.BlackMetalDragon) return false;
            bool trigger = Card.Location == CardLocation.Grave;
            if (trigger) MMLog("[MM.BMD.Search]", "grave trigger");
            return trigger;
        }

        private bool BMD_Equip_ToRedEyes()
        {
            if (Card == null || Card.Id != CardId.BlackMetalDragon) return false;
            if (Card.Location != CardLocation.Hand && Card.Location != CardLocation.MonsterZone) return false;
            if (Duel.Player != 0) return false;
            if (Duel.Phase != DuelPhase.Main1 && Duel.Phase != DuelPhase.Main2) return false;

            // PURPOSE: do not spend BMD equip unless we can execute the double-BMD REDMD conversion line.
            // TRUSTS: this line is valuable only when another BMD body is already on field.
            bool redmdOnline = Bot.HasInMonstersZone(CardId.RedEyesDarknessMetalDragon, true);
            bool canEquipToRedmd = redmdOnline && HasSecondBmdBodyForRedmdEquip();
            if (!canEquipToRedmd) return false;

            MMLog("[MM.BMD.Equip]", "equip to Red-Eyes body redmdAllowed=" + (canEquipToRedmd ? "yes" : "no"));
            return true;
        }

        private bool REDMD_SpecialFromHand()
        {
            if (Duel.Player != 0) return false;
            if (Duel.Phase != DuelPhase.Main1 && Duel.Phase != DuelPhase.Main2) return false;
            if (!Bot.HasInHand(CardId.RedEyesDarknessMetalDragon)) return false;
            if (Bot.HasInMonstersZone(CardId.RedEyesDarknessMetalDragon, true)) return false;
            if (CheckShouldNoMoreSpSummon(CardLocation.Hand)) return false;
            if (Bot.GetMonsterCount() >= 5) return false;
            bool canSummon = Bot.GetMonsters().Any(m => m != null && m.IsFaceup() && m.HasRace(CardRace.Dragon));
            if (canSummon) MMLog("[MM.REDMD.SS]", "special summon from hand");
            return canSummon;
        }

        private bool REDMD_Revive()
        {
            if (Duel.Player != 0) return false;
            if (Duel.Phase != DuelPhase.Main1 && Duel.Phase != DuelPhase.Main2) return false;
            if (!Bot.HasInMonstersZone(CardId.RedEyesDarknessMetalDragon, true)) return false;
            bool canRevive = Bot.Graveyard.Any(c => c != null && c.HasRace(CardRace.Dragon))
                || Bot.Hand.Any(c => c != null && c.HasRace(CardRace.Dragon));
            if (canRevive) MMLog("[MM.REDMD.Revive]", "activate");
            return canRevive;
        }

        private bool BystialMagnamhut_Activate()
        {
            if (Card == null || Card.Id != CardId.BystialMagnamhut) return false;

            if (Card.Location == CardLocation.Hand)
            {
                if (Duel.Player == 0 &&
                    Duel.Phase != DuelPhase.Main1 &&
                    Duel.Phase != DuelPhase.Main2 &&
                    Duel.Phase != DuelPhase.BattleStart) return false;
                if (CheckShouldNoMoreSpSummon(CardLocation.Hand)) return false;

                bool canSummon = Bot.GetMonsterCount() < 5 && GetBystialBanishTargets().Count > 0;
                if (canSummon) MMLog("[MM.Bystial.Magnamhut]", "summon from hand");
                return canSummon;
            }

            if (Card.Location == CardLocation.MonsterZone)
            {
                // The offered summon trigger has no target condition in c33854624.lua.
                // Do not reject it by inspecting hidden deck placeholders or current search targets.
                MMLog("[MM.Bystial.Magnamhut]", "summon trigger (queue end phase search)");
                return true;
            }

            return false;
        }

        private bool BystialDruiswurm_Activate()
        {
            if (Card == null || Card.Id != CardId.BystialDruiswurm) return false;

            if (Card.Location == CardLocation.Hand)
            {
                if (Duel.Player == 0 &&
                    Duel.Phase != DuelPhase.Main1 &&
                    Duel.Phase != DuelPhase.Main2 &&
                    Duel.Phase != DuelPhase.BattleStart) return false;
                if (CheckShouldNoMoreSpSummon(CardLocation.Hand)) return false;

                bool canSummon = Bot.GetMonsterCount() < 5 && GetBystialBanishTargets().Count > 0;
                if (canSummon) MMLog("[MM.Bystial.Druiswurm]", "summon from hand");
                return canSummon;
            }

            if (Card.Location == CardLocation.Grave)
            {
                bool canSend = Enemy.GetMonsters().Any(m => m != null && m.IsFaceup() && m.IsMonster() && m.IsSpecialSummoned && !m.IsShouldNotBeTarget());
                if (canSend) MMLog("[MM.Bystial.Druiswurm]", "GY send effect");
                return canSend;
            }

            return false;
        }

        private bool TheBystialLubellion_Activate()
        {
            if (Card == null || Card.Id != CardId.TheBystialLubellion) return false;

            if (Card.Location == CardLocation.Hand)
            {
                if (Duel.Player != 0) return false;
                if (Duel.Phase != DuelPhase.Main1 && Duel.Phase != DuelPhase.Main2) return false;
                if (CheckWhetherWillbeRemoved()) return false;
                bool haveTarget = CheckRemainInDeck(CardId.BystialDruiswurm, CardId.BystialMagnamhut) > 0;
                if (haveTarget) MMLog("[MM.Bystial.Lubellion]", "send to GY and search Bystial");
                return haveTarget;
            }

            if (Card.Location == CardLocation.MonsterZone)
            {
                if (Duel.Player != 0) return false;
                if (Duel.Phase != DuelPhase.Main1 && Duel.Phase != DuelPhase.Main2) return false;
                bool canSet = !HasFaceup(CardId.BrandedRegained) && CheckRemainInDeck(CardId.BrandedRegained) > 0;
                if (canSet) MMLog("[MM.Bystial.Lubellion]", "place Branded Regained");
                return canSet;
            }

            return false;
        }

        private bool TheBystialLubellion_GraveSummon()
        {
            if (Card == null || Card.Id != CardId.TheBystialLubellion) return false;
            if (Card.Location != CardLocation.Grave) return false;
            if (CheckShouldNoMoreSpSummon(CardLocation.Grave)) return false;
            if (Bot.GetMonsterCount() >= 5) return false;

            bool haveCost = Bot.GetMonsters().Any(c => c != null && c.IsFaceup() && c.Controller == 0 &&
                                                       c.HasRace(CardRace.Dragon) && c.HasAttribute(CardAttribute.Dark) &&
                                                       c.Level >= 6 && !c.IsCode(CardId.TheBystialLubellion));
            if (haveCost) MMLog("[MM.Bystial.Lubellion]", "summon from GY");
            return haveCost;
        }

        private bool BrandedRegained_Activate()
        {
            if (Card == null || Card.Id != CardId.BrandedRegained) return false;

            if (Card.Location == CardLocation.Hand)
            {
                if (!IHaveRoomST()) return false;
                bool activate = !HasFaceup(CardId.BrandedRegained);
                if (activate) MMLog("[MM.Regained]", "activate from hand");
                return activate;
            }

            if (Card.Location == CardLocation.SpellZone)
            {
                bool canRecycle = GetRegainedRecycleTargets().Count > 0;
                bool canReviveBystial = Duel.Player == 1
                    && Bot.GetMonsterCount() < 5
                    && Bot.Graveyard.Any(c => c != null && (c.IsCode(CardId.BystialMagnamhut)
                                                         || c.IsCode(CardId.BystialDruiswurm)
                                                         || c.IsCode(CardId.TheBystialLubellion)));
                bool activate = canRecycle || canReviveBystial;
                if (activate) MMLog("[MM.Regained]", canRecycle ? "recycle and draw" : "revive bystial");
                return activate;
            }

            return false;
        }

        private bool SPLittleKnight_Summon()
        {
            if (Duel.Player != 0) return false;
            if (Duel.Phase != DuelPhase.Main2) return false;
            if (!Bot.HasInExtra(CardId.SPLittleKnight)) return false;
            if (Bot.HasInMonstersZone(CardId.SPLittleKnight, true)) return false;
            if (CheckShouldNoMoreSpSummon(CardLocation.Extra)) return false;

            bool enemyHasBanishTarget = Enemy.GetMonsters().Any(c => c != null && c.IsFaceup() && !c.IsShouldNotBeTarget())
                                     || Enemy.GetSpells().Any(c => c != null && c.IsFaceup() && !c.IsShouldNotBeTarget());
            if (!enemyHasBanishTarget && !EnemyHighPressure()) return false;

            var pool = Bot.GetMonsters()
                .Where(c => c != null && c.IsFaceup() && c.HasType(CardType.Effect) && !IsProtectedForLinkMat(c))
                .OrderBy(ScoreOwnCardForCost)
                .ToList();
            if (pool.Count < 2) return false;

            List<ClientCard> mats = null;
            var lub = pool.FirstOrDefault(c => c.IsCode(CardId.TheBystialLubellion));
            if (lub != null)
            {
                var low = pool.FirstOrDefault(c => c != lub && !c.HasType(CardType.Link))
                       ?? pool.FirstOrDefault(c => c != lub);
                if (low != null) mats = new List<ClientCard> { lub, low };
            }

            if (mats == null)
            {
                mats = pool.Where(c => !c.HasType(CardType.Link)).Take(2).ToList();
                if (mats.Count < 2)
                    mats = pool.Take(2).ToList();
            }
            if (mats.Count < 2) return false;
            if (Util.GetBotAvailZonesFromExtraDeck(mats) == 0) return false;

            AI.SelectMaterials(mats);
            MMLog("[MM.SPLK.SS]", "link summon mats=" + mats[0].Id + "," + mats[1].Id);
            return true;
        }

        private bool SPLittleKnight_BanishOnSummon()
        {
            if (Card == null || Card.Id != CardId.SPLittleKnight) return false;
            if (DefaultCheckWhetherCardIsNegated(Card)) return false;

            int desc0 = Util.GetStringId(CardId.SPLittleKnight, 0);
            if (ActivateDescription > 0 && ActivateDescription != desc0) return false;

            var best =
                Enemy.GetMonsters().Where(c => c != null && !c.IsShouldNotBeTarget())
                    .OrderByDescending(ScoreEnemyCardForRemoval).FirstOrDefault()
                ?? Enemy.GetSpells().Where(c => c != null && !c.IsShouldNotBeTarget())
                    .OrderByDescending(ScoreEnemyCardForRemoval).FirstOrDefault()
                ?? Enemy.Graveyard.Where(c => c != null).OrderByDescending(c => c.Attack).FirstOrDefault()
                ?? Bot.Graveyard.Where(c => c != null && !IsBoss(c)).OrderByDescending(c => c.Attack).FirstOrDefault();
            if (best == null) return false;

            AI.SelectCard(best);
            MMLog("[MM.SPLK]", "banish on summon target=" + best.Id);
            return true;
        }

        private bool SPLittleKnight_QuickBanish()
        {
            if (Card == null || Card.Id != CardId.SPLittleKnight) return false;
            if (DefaultCheckWhetherCardIsNegated(Card)) return false;
            if (ActivateDescription != Util.GetStringId(CardId.SPLittleKnight, 1)) return false;

            var last = Util.GetLastChainCard();
            bool chainThreat = last != null && last.Controller == 1 && (last.IsFloodgate() || last.IsMonsterDangerous() || last.IsMonsterInvincible());

            var bestEnemy = Enemy.GetMonsters().Where(c => c != null && c.IsFaceup() && !c.IsShouldNotBeTarget())
                              .OrderByDescending(ScoreEnemyCardForRemoval).FirstOrDefault()
                           ?? Enemy.GetSpells().Where(c => c != null && c.IsFaceup() && !c.IsShouldNotBeTarget())
                              .OrderByDescending(ScoreEnemyCardForRemoval).FirstOrDefault();
            if (bestEnemy == null) return false;
            if (!chainThreat && !EnemyHighPressure()) return false;

            AI.SelectCard(new List<ClientCard> { Card, bestEnemy });
            MMLog("[MM.SPLK]", "quick banish self+" + bestEnemy.Id);
            return true;
        }

        private bool Dharc_Summon()
        {
            if (Duel.Player != 0) return false;
            if (Duel.Phase != DuelPhase.Main1 && Duel.Phase != DuelPhase.Main2) return false;
            if (!Bot.HasInExtra(CardId.DharcTheDarkCharmerGloomy)) return false;
            if (Bot.HasInMonstersZone(CardId.DharcTheDarkCharmerGloomy, true)) return false;
            if (CheckShouldNoMoreSpSummon(CardLocation.Extra)) return false;
            if (ShouldCommitToRedmdSealLineNow()) return false;
            if (!HasFreeDharcStealLaneIfSummoned()) return false;
            // Do not force Dharc lines without a real steal target.
            bool oppHasDarkGY = Enemy.Graveyard.Any(c => c != null && c.IsMonster() && c.HasAttribute(CardAttribute.Dark));
            if (!oppHasDarkGY) return false;
            if (ShouldHoldBattleCoreForBoard()) return false;
            if (CanDirectLethalNow()) return false;

            bool underPressure = EnemyHighPressure();
            bool urgentBreakNeed =
                Enemy.GetSpells().Any(s => s != null && s.IsFaceup() && s.IsFloodgate())
                || Enemy.GetMonsters().Any(m => m != null && m.IsFaceup() && (m.IsMonsterInvincible() || m.IsMonsterDangerous()));
            if (!underPressure && !urgentBreakNeed) return false;

            var pool = Bot.GetMonsters().Where(c => c != null
                                                 && c.IsFaceup()
                                                 && !IsProtectedForLinkMat(c)
                                                 && !IsHighValueSynchroBody(c))
                                        .OrderBy(ScoreOwnCardForCost).ToList();
            if (ShouldHoldBlackMetalForStrikerLine())
                pool = pool.Where(c => !c.IsCode(CardId.BlackMetalDragon)).ToList();

            if (pool.Count < 2 && underPressure)
            {
                pool = Bot.GetMonsters().Where(c => c != null && c.IsFaceup() && !IsProtectedForLinkMat(c))
                                        .OrderBy(ScoreOwnCardForCost).ToList();
                if (ShouldHoldBlackMetalForStrikerLine())
                    pool = pool.Where(c => !c.IsCode(CardId.BlackMetalDragon)).ToList();
            }
            if (pool.Count < 2) return false;

            var dark = pool.FirstOrDefault(c => c.HasAttribute(CardAttribute.Dark) && !IsCoreBranchBody(c))
                    ?? pool.FirstOrDefault(c => c.HasAttribute(CardAttribute.Dark));
            var other = pool.FirstOrDefault(c => c != dark && !IsCoreBranchBody(c))
                     ?? pool.FirstOrDefault(c => c != dark);
            if (dark == null || other == null) return false;

            // Keep core monsters for battle unless opponent pressure demands conversion.
            if (!underPressure)
            {
                if (IsCoreBranchBody(dark) && IsCoreBranchBody(other)) return false;

                int attackers = Bot.GetMonsters().Count(m => m != null && m.IsFaceup() && m.Attack > 0);
                if (attackers <= 2 && (IsCoreBranchBody(dark) || IsCoreBranchBody(other))) return false;
            }

            AI.SelectPlace(Zones.ExtraMonsterZones);
            AI.SelectMaterials(new List<ClientCard> { dark, other });
            MMLog("[MM.Dharc.SS]", "link summon mat=" + dark.Id + "," + other.Id);
            return true;
        }

        private bool Dharc_Activate()
        {
            if (Card == null || Card.Id != CardId.DharcTheDarkCharmerGloomy) return false;

            if (Card.Location == CardLocation.MonsterZone)
            {
                if (Bot.GetMonsterCount() >= 5) return false;
                bool canSteal = HasFreeDharcLinkedMainZone(Card)
                             && Enemy.Graveyard.Any(c => c != null && c.IsMonster() && c.HasAttribute(CardAttribute.Dark));
                if (canSteal) MMLog("[MM.Dharc]", "steal DARK from opponent GY");
                return canSteal;
            }

            if (Card.Location == CardLocation.Grave)
            {
                bool canAdd = CheckRemainInDeck(CardId.TheBystialLubellion, CardId.BystialDruiswurm,
                                                CardId.BystialMagnamhut, CardId.BlackMetalDragon) > 0;
                if (canAdd) MMLog("[MM.Dharc]", "destroyed search");
                return canAdd;
            }

            return false;
        }

        private bool DracoBerserkerOfTheTenyi_Summon()
        {
            if (Duel.Player != 0) return false;
            if (Duel.Phase != DuelPhase.Main1 && Duel.Phase != DuelPhase.Main2) return false;
            if (!Bot.HasInExtra(CardId.DracoBerserkerOfTheTenyi)) return false;
            if (Bot.HasInMonstersZone(CardId.DracoBerserkerOfTheTenyi, true)) return false;
            if (CheckShouldNoMoreSpSummon(CardLocation.Extra)) return false;
            if (ShouldCommitToRedmdSealLineNow()) return false;

            // Optional line only: do not convert winning boards into Draco.
            if (CanDirectLethalNow()) return false;

            var own = Bot.GetMonsters().Where(c => c != null && c.IsFaceup()).ToList();
            if (!own.Any(c => c.IsCode(CardId.FidraulisHarmonia) && c.HasType(CardType.Tuner))) return false;

            var mats = PickDracoBerserkerMaterials(own, 8);
            if (mats == null || mats.Count < 2) return false;

            AI.SelectMaterials(mats);
            MMLog("[MM.Draco.SS]", "synchro via Fidraulis+" + mats[1].Id);
            return true;
        }

        private bool DracoBerserkerOfTheTenyi_Activate()
        {
            if (Card == null || Card.Id != CardId.DracoBerserkerOfTheTenyi) return false;
            if (Card.Location != CardLocation.MonsterZone) return false;
            if (DefaultCheckWhetherCardIsNegated(Card)) return false;
            if (Duel.LastChainPlayer != 1 || Duel.CurrentChain == null || Duel.CurrentChain.Count == 0) return false;

            ChainInfo currentChain = Duel.GetCurrentSolvingChainInfo();
            if (currentChain != null && currentChain.ActivatePlayer == 1 && currentChain.RelatedCard != null && currentChain.RelatedCard.IsMonster())
            {
                if (CheckCardShouldNegate(currentChain) || EnemyHighPressure())
                {
                    MMLog("[MM.Draco]", "banish responding monster");
                    return true;
                }
            }

            ClientCard last = Util.GetLastChainCard();
            if (last != null && last.Controller == 1 && last.IsMonster() && (CheckCardShouldNegate(last) || EnemyHighPressure()))
            {
                MMLog("[MM.Draco]", "banish responding monster fallback");
                return true;
            }

            return false;
        }

        private bool KnightmarePhoenix_Summon()
        {
            if (Duel.Player != 0) return false;
            if (Duel.Phase != DuelPhase.Main1 && Duel.Phase != DuelPhase.Main2) return false;
            if (!Bot.HasInExtra(CardId.KnightmarePhoenix)) return false;
            if (!Bot.HasInExtra(CardId.AccesscodeTalker)) return false;
            if (Bot.HasInMonstersZone(CardId.AccesscodeTalker, true)) return false;
            if (CheckShouldNoMoreSpSummon(CardLocation.Extra)) return false;
            if (Bot.GetHandCount() == 0) return false;
            if (ShouldHoldBattleCoreForBoard()) return false;
            if (!Bot.HasInMonstersZone(CardId.DharcTheDarkCharmerGloomy, true)) return false;

            bool havePopTarget = Enemy.GetSpells().Any(s => s != null && !s.IsShouldNotBeTarget())
                              || Enemy.GetMonsters().Any(m => m != null && !m.IsShouldNotBeTarget());
            if (!havePopTarget) return false;

            var dharc = Bot.MonsterZone.FirstOrDefault(c => c != null && c.IsFaceup() && c.IsCode(CardId.DharcTheDarkCharmerGloomy));
            if (dharc == null) return false;
            var pool = Bot.GetMonsters().Where(c => c != null && c.IsFaceup() && !IsProtectedForLinkMat(c))
                                        .OrderBy(ScoreOwnCardForCost).ToList();
            var mats = pool.Where(c => c != dharc && !IsCoreBranchBody(c) && !c.HasType(CardType.Link))
                           .Take(2).ToList();
            if (mats.Count < 2)
                mats = pool.Where(c => c != dharc && !IsCoreBranchBody(c)).Take(2).ToList();
            if (mats.Count < 2) return false;

            AI.SelectMaterials(mats);
            MMLog("[MM.Phoenix.SS]", "link summon mat=" + mats[0].Id + "," + mats[1].Id + " (keep Dharc)");
            return true;
        }

        private bool KnightmarePhoenix_Pop()
        {
            if (Card == null || Card.Id != CardId.KnightmarePhoenix) return false;
            if (DefaultCheckWhetherCardIsNegated(Card)) return false;
            if (Bot.GetHandCount() == 0) return false;

            var target =
                Enemy.GetSpells().FirstOrDefault(s => s != null && !s.IsShouldNotBeTarget()) ??
                Enemy.GetMonsters().Where(m => m != null && !m.IsShouldNotBeTarget())
                     .OrderByDescending(ScoreEnemyCardForRemoval).FirstOrDefault();
            if (target == null) return false;

            var discard = Bot.Hand.FirstOrDefault(c => c != null &&
                                                       !c.IsCode(CardId.MaxMetalmorph) &&
                                                       !c.IsCode(CardId.FlameCoatingMetalmorph) &&
                                                       !c.IsCode(CardId.TimeEngine) &&
                                                       !c.IsCode(CardId.RedEyesBlackFullmetalDragon) &&
                                                       !c.IsCode(CardId.RedEyesDarknessMetalDragon))
                          ?? Bot.Hand.FirstOrDefault();

            if (discard == null) return false;

            MMLog("[MM.Phoenix]", "discard + pop");
            return true;
        }

        private bool Accesscode_Summon()
        {
            if (Duel.Player != 0) return false;
            if (Duel.Phase != DuelPhase.Main1 && Duel.Phase != DuelPhase.Main2) return false;
            if (!Bot.HasInExtra(CardId.AccesscodeTalker)) return false;
            if (Bot.HasInMonstersZone(CardId.AccesscodeTalker, true)) return false;
            if (CheckShouldNoMoreSpSummon(CardLocation.Extra)) return false;

            var phoenix = Bot.MonsterZone.FirstOrDefault(c => c != null && c.IsFaceup() && c.IsCode(CardId.KnightmarePhoenix));
            var dharc = Bot.MonsterZone.FirstOrDefault(c => c != null && c.IsFaceup() && c.IsCode(CardId.DharcTheDarkCharmerGloomy));
            if (phoenix != null && dharc != null)
            {
                AI.SelectMaterials(new List<ClientCard> { phoenix, dharc });
                MMLog("[MM.Accesscode.SS]", "link summon via Phoenix + Dharc");
                return true;
            }

            var links = Bot.GetMonsters().Where(c => c != null && c.IsFaceup() && c.HasType(CardType.Link) &&
                                                     !c.IsCode(CardId.AccesscodeTalker) && !IsProtectedForLinkMat(c))
                                         .OrderByDescending(c => c.LinkCount)
                                         .ThenBy(ScoreOwnCardForCost)
                                         .ToList();
            if (links.Count < 2) return false;

            for (int i = 0; i < links.Count; ++i)
            {
                for (int j = i + 1; j < links.Count; ++j)
                {
                    int linkSum = Math.Max(1, links[i].LinkCount) + Math.Max(1, links[j].LinkCount);
                    if (linkSum >= 3)
                    {
                        AI.SelectMaterials(new List<ClientCard> { links[i], links[j] });
                        MMLog("[MM.Accesscode.SS]", "link summon via generic links");
                        return true;
                    }
                }
            }

            return false;
        }

        private bool Accesscode_AtkUp()
        {
            if (Card == null || Card.Id != CardId.AccesscodeTalker) return false;
            if (ActivateDescription == Util.GetStringId(CardId.AccesscodeTalker, 1)) return false;
            // The engine offers this trigger only when a legal Link material exists;
            // c86066372.lua also permits face-up banished materials (e.g. under Shifter).
            MMLog("[MM.Accesscode]", "gain atk from offered Link material");
            return true;
        }

        private bool Accesscode_Destroy()
        {
            if (Card == null || Card.Id != CardId.AccesscodeTalker) return false;
            if (ActivateDescription != Util.GetStringId(CardId.AccesscodeTalker, 1)) return false;
            if (Enemy.GetFieldCount() == 0) return false;

            var costPool = Bot.Graveyard.Where(c => c != null && c.HasType(CardType.Link))
                            .Concat(Bot.GetMonsters().Where(c => c != null && c.HasType(CardType.Link) && c != Card))
                            .ToList();
            if (costPool.Count == 0) return false;

            // The callback owns both the banish cost and non-targeting destruction.
            MMLog("[MM.Accesscode]", "banish link + destroy");
            return true;
        }

        private bool FidraulisHarmonia_Activate()
        {
            if (Card == null || Card.Id != CardId.FidraulisHarmonia) return false;
            if (Card.Location != CardLocation.Hand) return false;
            if (Duel.Player != 1) return false;
            if (Bot.GetMonsterCount() >= 5) return false;

            var last = Util.GetLastChainCard();
            bool enemyFieldMonsterEffect = Duel.LastChainPlayer == 1
                && last != null
                && last.Controller == 1
                && last.IsMonster()
                && last.Location == CardLocation.MonsterZone;
            if (!enemyFieldMonsterEffect) return false;

            int synchroCount = Bot.ExtraDeck.Count(c => c != null && c.HasType(CardType.Synchro));
            if (synchroCount <= 0) return false;

            int enemyFieldTargets = CountEnemyOnFieldTargets();
            if (enemyFieldTargets < 2) return false;

            bool activate = true;
            if (activate)
            {
                _pendingFidraulisRevealPrompt = true;
                _pendingFidraulisSendPrompt = false;
                _pendingFidraulisDestroyPrompt = false;
                _pendingFidraulisRevealPickedIds.Clear();
                bool enemyHasMonster = Enemy.GetMonsters().Any(m => m != null);
                _pendingFidraulisRevealDesiredCount = Math.Min(synchroCount, enemyHasMonster ? 5 : 4);
                MMLog("[MM.Fidraulis]", "activate (reveal targetCount=" + _pendingFidraulisRevealDesiredCount + ", fieldTargets=" + enemyFieldTargets + ")");
            }
            return activate;
        }

        // PURPOSE: shared Hop Ear viability gate for both direct activation and Victim Sanctuary hold logic.
        // TRUSTS: called during opponent main phase windows where a hop-ear quick line may be chained.
        // RESET: stateless helper.
        private bool HasHopEarFollowupWindow()
        {
            if (Duel.Player != 1) return false;
            if (Duel.Phase != DuelPhase.Main1 && Duel.Phase != DuelPhase.Main2) return false;
            if (Bot.GetMonsterCount() >= 5) return false;

            var targets = Bot.GetMonsters().Where(m => m != null && m.IsFaceup() && !m.HasType(CardType.Link)).ToList();
            if (targets.Count == 0) return false;

            bool can8 = targets.Any(m => m.Level + 2 == 8) && Bot.HasInExtra(CardId.AccelSynchroStardustDragon);
            bool can10 = targets.Any(m => m.Level + 2 == 10)
                      && (Bot.HasInExtra(CardId.SatelliteWarrior) || Bot.HasInExtra(CardId.BaronneDeFleur));
            bool canFidraulisRoute = targets.Any(m => m.IsCode(CardId.FidraulisHarmonia))
                                  && (Bot.HasInExtra(CardId.AccelSynchroStardustDragon)
                                   || Bot.HasInExtra(CardId.SatelliteWarrior)
                                   || Bot.HasInExtra(CardId.BaronneDeFleur));

            return can8 || can10 || canFidraulisRoute;
        }

        private bool CanHopEarConvert(ClientCard target)
        {
            if (target == null || !target.IsFaceup() || target.HasType(CardType.Link)) return false;
            if (target.Level + 2 == 8 && Bot.HasInExtra(CardId.AccelSynchroStardustDragon)) return true;
            if (target.Level + 2 == 10 && (Bot.HasInExtra(CardId.SatelliteWarrior) || Bot.HasInExtra(CardId.BaronneDeFleur))) return true;
            return target.IsCode(CardId.FidraulisHarmonia)
                && (Bot.HasInExtra(CardId.AccelSynchroStardustDragon)
                 || Bot.HasInExtra(CardId.SatelliteWarrior)
                 || Bot.HasInExtra(CardId.BaronneDeFleur));
        }

        private bool CanReactiveBystialBecomeHopEarBody()
        {
            bool haveHandBystial = Bot.HasInHand(CardId.BystialMagnamhut) || Bot.HasInHand(CardId.BystialDruiswurm);
            return haveHandBystial && GetBystialBanishTargets().Count > 0;
        }

        // PURPOSE: Hop Ear should preserve established payoff monsters whenever a non-boss lv6/lv8 body can do the same job.
        // TRUSTS: for this deck, Fullmetal / Metalzoa X / Metalflame Swordsman are higher-value end-board pieces than temporary Hop Ear bodies.
        // RESET: stateless helper.
        private bool IsHopEarBossBody(ClientCard c)
            => c != null && (c.IsCode(CardId.MetalflameSwordsman) || IsBoss(c));

        private bool CanHopEarRouteToBaronneWith(ClientCard c)
            => c != null && c.IsFaceup() && !c.HasType(CardType.Link) && c.Level + 2 == 10 && Bot.HasInExtra(CardId.BaronneDeFleur);

        private bool CanHopEarRouteToAccelWith(ClientCard c)
            => c != null && c.IsFaceup() && !c.HasType(CardType.Link) && c.Level + 2 == 8 && Bot.HasInExtra(CardId.AccelSynchroStardustDragon);

        // PURPOSE: keep Hop Ear off boss bodies when a reactive Bystial line can create the same lv6 bridge later in the turn cycle.
        // TRUSTS: hand Bystials are the preferred disposable lv6 Hop Ear bodies.
        // RESET: stateless helper.
        private bool ShouldSaveHopEarBossForReactiveBystial(IEnumerable<ClientCard> targets)
        {
            var list = targets?.Where(c => c != null && c.IsFaceup()).ToList() ?? new List<ClientCard>();
            if (list.Count == 0) return false;
            if (!list.All(IsHopEarBossBody)) return false;
            return CanReactiveBystialBecomeHopEarBody();
        }

        // PURPOSE: prefer live lv6 Bystial bodies for the Hop Ear -> Accel -> Satellite line before spending lv8 Baronne bodies.
        // TRUSTS: lv6 bodies convert into Accel and preserve higher-value lv8 interruption when both routes are legal.
        // RESET: stateless helper.
        private ClientCard PickPreferredHopEarRouteBody(IList<ClientCard> pool)
        {
            if (pool == null || pool.Count == 0) return null;

            var lv6 = pool.Where(c => c != null && !IsHopEarBossBody(c) && CanHopEarRouteToAccelWith(c))
                          .OrderByDescending(c => c.IsCode(CardId.BystialMagnamhut) ? 2 : c.IsCode(CardId.BystialDruiswurm) ? 1 : 0)
                          .ThenByDescending(c => c.Attack)
                          .FirstOrDefault();
            if (lv6 != null) return lv6;

            var lv8 = pool.Where(c => c != null && !IsHopEarBossBody(c) && CanHopEarRouteToBaronneWith(c))
                          .OrderByDescending(c => c.Attack)
                          .FirstOrDefault();
            if (lv8 != null) return lv8;

            return null;
        }

        // PURPOSE: Dharc should steal DARK bodies that preserve the strongest Hop Ear follow-up if possible.
        // TRUSTS: lv8 + Hop Ear => Baronne, lv6 + Hop Ear => Accel line, otherwise highest-value DARK fallback.
        // RESET: stateless helper.
        private ClientCard PickDharcStealTarget(IList<ClientCard> cards)
        {
            if (cards == null || cards.Count == 0) return null;

            var pool = cards.Where(c => c != null && c.Controller == 1 && c.IsMonster() && c.HasAttribute(CardAttribute.Dark) && !c.IsShouldNotBeTarget())
                            .OrderByDescending(c => c.Attack)
                            .ToList();
            if (pool.Count == 0)
                pool = cards.Where(c => c != null && c.Controller == 1 && c.IsMonster() && c.HasAttribute(CardAttribute.Dark))
                            .OrderByDescending(c => c.Attack)
                            .ToList();
            if (pool.Count == 0)
                return cards.FirstOrDefault(c => c != null && c.Controller == 1 && c.IsMonster() && !c.IsShouldNotBeTarget())
                    ?? cards.FirstOrDefault(c => c != null && c.Controller == 1 && c.IsMonster())
                    ?? cards[0];

            var lv8HopEar = pool.FirstOrDefault(c => c.Level == 8 && Bot.HasInHand(CardId.HopEarSquadron) && Bot.HasInExtra(CardId.BaronneDeFleur));
            if (lv8HopEar != null) return lv8HopEar;

            var lv6HopEar = pool.FirstOrDefault(c => c.Level == 6 && Bot.HasInHand(CardId.HopEarSquadron) && Bot.HasInExtra(CardId.AccelSynchroStardustDragon));
            if (lv6HopEar != null) return lv6HopEar;

            return pool.FirstOrDefault(c => c.Level == 8)
                ?? pool.FirstOrDefault(c => c.Level == 6)
                ?? pool.FirstOrDefault();
        }

        // PURPOSE: Victim Sanctuary should summon Stardust Warrior first so the Accel -> Satellite line keeps Stardust Dragon available.
        // TRUSTS: when both options are legal, Stardust Warrior is the safer extender and Stardust Dragon remains a critical Synchro piece.
        // RESET: stateless helper.
        private ClientCard PickVictimSanctuarySummon(IList<ClientCard> cards)
        {
            if (cards == null || cards.Count == 0) return null;

            var stardustWarrior = cards.FirstOrDefault(c => c != null && c.IsCode(CardId.StardustWarrior));
            var stardustDragon = cards.FirstOrDefault(c => c != null && c.IsCode(CardId.StardustDragon));

            // Keep Stardust Dragon available for Accel -> Satellite whenever possible.
            return stardustWarrior
                ?? stardustDragon
                ?? cards[0];
        }

        // PURPOSE: Accel's immediate final Synchro prompt can occasionally lose Card context; detect by offered endpoints.
        // TRUSTS: prompt offers Satellite/Baronne from Extra when Accel's tribute line is resolving.
        // RESET: stateless helper.
        private bool LooksLikeAccelFinalSynchroPrompt(IList<ClientCard> cards, int hint)
        {
            if (!(hint == HintMsg.SpSummon || hint == HintMsg.OperateCard || hint == 505)) return false;
            if (cards == null || cards.Count == 0) return false;
            return cards.Any(c => c != null && (c.IsCode(CardId.SatelliteWarrior) || c.IsCode(CardId.BaronneDeFleur)));
        }

        // PURPOSE: only prefer Satellite over Baronne when the board has enough enemy value for Satellite's summon pop/ATK line.
        // TRUSTS: with both options available, Baronne is the safer default unless Satellite can convert into at least two removals.
        // RESET: stateless helper.
        private bool ShouldPreferSatelliteOverBaronne(IList<ClientCard> options = null)
        {
            bool hasSatellite = options == null
                ? Bot.HasInExtra(CardId.SatelliteWarrior)
                : options.Any(c => c != null && c.IsCode(CardId.SatelliteWarrior));
            if (!hasSatellite) return false;

            bool hasBaronne = options == null
                ? Bot.HasInExtra(CardId.BaronneDeFleur)
                : options.Any(c => c != null && c.IsCode(CardId.BaronneDeFleur));
            if (!hasBaronne) return true;

            return CountEnemyOnFieldTargets() >= 2;
        }

        // PURPOSE: shared selector for Accel prompt chain (revive tuner -> summon Stardust -> final Synchro).
        // TRUSTS: caller already validated prompt context (Card or solving-chain card / one-shot fallback).
        // RESET: relies on _preferSatelliteFromAccel, cleared on OnChainEnd/OnNewTurn.
        private IList<ClientCard> SelectAccelSynchroPrompt(IList<ClientCard> cards, int min, int max, int hint)
        {
            if (!(hint == HintMsg.SpSummon || hint == HintMsg.OperateCard || hint == 505)) return null;
            if (cards == null || cards.Count == 0) return null;

            if (LooksLikeAccelFinalSynchroPrompt(cards, hint))
            {
                bool preferSatellite = _preferSatelliteFromAccel || ShouldPreferSatelliteOverBaronne(cards);
                var satellite = cards.FirstOrDefault(c => c != null && c.IsCode(CardId.SatelliteWarrior));
                var baronne = cards.FirstOrDefault(c => c != null && c.IsCode(CardId.BaronneDeFleur));
                var synchroPick =
                      (preferSatellite ? satellite : baronne)
                   ?? satellite
                   ?? baronne;
                if (synchroPick != null)
                {
                    MMLog("[MM.Accel.Select]", "pick finalSynchro id=" + synchroPick.Id + " preferSatellite=" + (preferSatellite ? "Y" : "N")
                        + " enemyTargets=" + CountEnemyOnFieldTargets());
                    return Util.CheckSelectCount(new List<ClientCard> { synchroPick }, cards, min, max);
                }
            }

            var fromGyTuner = cards.FirstOrDefault(c => c != null && c.Location == CardLocation.Grave && c.IsCode(CardId.HopEarSquadron))
                           ?? cards.FirstOrDefault(c => c != null && c.Location == CardLocation.Grave && c.IsMonster() && c.Level <= 2 && c.HasType(CardType.Tuner));
            if (fromGyTuner != null)
                return Util.CheckSelectCount(new List<ClientCard> { fromGyTuner }, cards, min, max);

            var stardust = cards.FirstOrDefault(c => c != null && c.IsCode(CardId.StardustDragon));
            if (stardust != null)
            {
                MMLog("[MM.Accel.Select]", "pick Stardust route id=" + stardust.Id);
                return Util.CheckSelectCount(new List<ClientCard> { stardust }, cards, min, max);
            }

            return null;
        }

        // PURPOSE: Victim Sanctuary has exactly one Extra Deck summon pick; use a one-shot guard so later Accel prompts are not hijacked.
        // TRUSTS: its ssop only selects 1 "Stardust" Synchro from Extra and then ends.
        // RESET: clear on selection, OnNewTurn, OnChainSolved, and OnChainEnd.
        private bool LooksLikeVictimSanctuarySummonPrompt(IList<ClientCard> cards, int hint, int min, int max)
        {
            if (!_pendingVictimSanctuarySummonPrompt) return false;
            if (!(hint == HintMsg.SpSummon || hint == HintMsg.OperateCard || hint == 505)) return false;
            if (cards == null || cards.Count == 0 || min != 1 || max != 1) return false;

            return cards.All(c => c != null
                               && c.Location == CardLocation.Extra
                               && c.HasType(CardType.Synchro)
                               && (c.IsCode(CardId.StardustDragon) || c.IsCode(CardId.StardustWarrior)));
        }

        // PURPOSE: Junk Warrior Extreme should revive the full legal package, prioritizing Hop Ear then Black Metal Dragon.
        // TRUSTS: its prompt max already reflects current monster-zone capacity.
        // RESET: stateless helper.
        private List<ClientCard> GetJunkWarriorExtremeRevivePool(IList<ClientCard> cards)
        {
            if (cards == null) return new List<ClientCard>();

            int[] prefer = { CardId.HopEarSquadron, CardId.BlackMetalDragon };
            return cards.Where(c => c != null && c.Controller == 0 && c.Location == CardLocation.Grave && c.IsMonster() && c.Level <= 2)
                        .OrderBy(c =>
                        {
                            int idx = Array.IndexOf(prefer, c.Id);
                            return idx < 0 ? 99 : idx;
                        })
                        .ThenByDescending(c => c.Attack)
                        .ToList();
        }

        private bool IsHopEarTargetAlreadySafeFromCurrentChain(ClientCard target)
        {
            var last = Util.GetLastChainCard();
            if (target == null || last == null) return false;
            if (!HasMaxMetalmorphShield(target)) return false;
            if (!(last.HasType(CardType.Spell) || last.IsMonster())) return false;

            bool directlyTargetsThis = Duel.LastChainTargets.Any(t => t != null && t == target);
            if (directlyTargetsThis) return true;

            return last.Controller == 1 && CheckCardShouldNegate(last);
        }

        private ClientCard GetPreferredHopEarTarget(IList<ClientCard> cards)
        {
            var pool = cards?.Where(c => c != null && c.Controller == 0 && c.IsFaceup() && CanHopEarConvert(c)).ToList()
                       ?? new List<ClientCard>();
            if (pool.Count == 0) return null;

            var threatened = pool.Where(c => Duel.LastChainTargets.Any(t => t != null && t == c) && !IsHopEarTargetAlreadySafeFromCurrentChain(c))
                                 .OrderByDescending(c => c.IsCode(CardId.FidraulisHarmonia) ? 1 : 0)
                                 .ThenByDescending(c => c.Attack)
                                 .FirstOrDefault();
            if (threatened != null) return threatened;

            var preferredRouteBody = PickPreferredHopEarRouteBody(pool);
            if (preferredRouteBody != null) return preferredRouteBody;

            bool saveBossForLater = ShouldSaveHopEarBossForReactiveBystial(pool);
            if (!saveBossForLater)
            {
                var fid = pool.FirstOrDefault(c => c.IsCode(CardId.FidraulisHarmonia));
                if (fid != null) return fid;

                var nonBoss = pool.FirstOrDefault(c => !IsHopEarBossBody(c));
                if (nonBoss != null) return nonBoss;

                var safeSwordsman = pool.FirstOrDefault(c => c.IsCode(CardId.MetalflameSwordsman) && !IsHopEarTargetAlreadySafeFromCurrentChain(c));
                if (safeSwordsman != null) return safeSwordsman;

                var safeLv8Boss = pool.FirstOrDefault(c => (c.IsCode(CardId.RedEyesBlackFullmetalDragon) || c.IsCode(CardId.MetalzoaX)) && !IsHopEarTargetAlreadySafeFromCurrentChain(c));
                if (safeLv8Boss != null) return safeLv8Boss;
            }

            return pool.FirstOrDefault(c => !IsHopEarBossBody(c) && !IsHopEarTargetAlreadySafeFromCurrentChain(c))
                ?? pool.FirstOrDefault(c => !IsHopEarTargetAlreadySafeFromCurrentChain(c));
        }

        // PURPOSE: only spend Hop Ear when the current enemy chain actually pressures our board,
        //          or when we have no other live interruption and the conversion itself creates one.
        // TRUSTS: LastChain targets/related card are populated during opponent main-phase chains.
        // RESET: stateless helper.
        private bool ChainThreatensOurMonsterZone()
        {
            var last = Util.GetLastChainCard();
            ChainInfo current = Duel.GetCurrentSolvingChainInfo();

            bool targetsOurMonster = Duel.LastChainTargets.Any(t => t != null && t.Controller == 0 && t.Location == CardLocation.MonsterZone);
            bool meaningfulEnemyChain = (current != null && current.ActivatePlayer == 1 && current.RelatedCard != null && CheckCardShouldNegate(current))
                                     || (last != null && last.Controller == 1 && CheckCardShouldNegate(last));

            return targetsOurMonster || meaningfulEnemyChain;
        }

        // PURPOSE: preserve higher-value live interruption before converting with Hop Ear.
        // TRUSTS: face-up Metalzoa X / Fullmetal / Baronne / Seal usually give us enough reactive coverage already.
        // RESET: stateless helper.
        private bool HasAlternativeReactiveBossOnline()
        {
            return Bot.HasInMonstersZone(CardId.MetalzoaX, true)
                || Bot.HasInMonstersZone(CardId.RedEyesBlackFullmetalDragon, true)
                || Bot.HasInMonstersZone(CardId.BaronneDeFleur, true)
                || Bot.HasInMonstersZone(CardId.HieraticSealOfTheHeavenlySpheres, true);
        }

        // PURPOSE: final Hop Ear gate used by the live activation path.
        // TRUSTS: HasHopEarFollowupWindow already validated the summon math.
        // RESET: stateless helper.
        private bool ShouldUseHopEarNow()
        {
            if (!HasHopEarFollowupWindow()) return false;

            var liveTargets = Bot.GetMonsters().Where(m => m != null && m.IsFaceup() && CanHopEarConvert(m)).ToList();
            if (liveTargets.Count == 0) return false;

            var preferred = GetPreferredHopEarTarget(liveTargets);
            bool emergencyWindow = preferred != null && ChainThreatensOurMonsterZone() && !IsHopEarTargetAlreadySafeFromCurrentChain(preferred);
            if (emergencyWindow) return true;

            if (HasAlternativeReactiveBossOnline()) return false;
            if (ShouldSaveHopEarBossForReactiveBystial(liveTargets)) return false;

            bool havePreferredNonBossBody = liveTargets.Any(c => !IsHopEarBossBody(c));
            if (!havePreferredNonBossBody) return false;

            return Enemy.GetMonsters().Count(m => m != null && m.IsFaceup() && !m.IsShouldNotBeTarget()) >= 2;
        }

        // PURPOSE: keep Victim Sanctuary from consuming the window before the intended Hop Ear follow-up is used.
        // TRUSTS: if Fidraulis is on-field and Hop Ear is live in hand, Hop Ear line should be attempted first.
        // RESET: stateless helper.
        private bool ShouldHoldVictimSanctuaryForHopEar()
        {
            if (!Bot.HasInMonstersZone(CardId.FidraulisHarmonia, true)) return false;
            if (!Bot.HasInHand(CardId.HopEarSquadron)) return false;
            bool stardustRouteAlreadyStarted =
                Bot.HasInMonstersZone(CardId.AccelSynchroStardustDragon, true)
                || Bot.HasInMonstersZone(CardId.StardustDragon, true)
                || Bot.HasInMonstersZone(CardId.SatelliteWarrior, true)
                || Bot.HasInMonstersZone(CardId.StardustWarrior, true);
            if (stardustRouteAlreadyStarted) return false;
            return ShouldUseHopEarNow();
        }

        private bool HopEarSquadron_Activate()
        {
            if (Card == null || Card.Id != CardId.HopEarSquadron) return false;
            if (Card.Location != CardLocation.Hand) return false;
            if (Duel.Player != 1) return false;
            if (Duel.Phase != DuelPhase.Main1 && Duel.Phase != DuelPhase.Main2) return false;
            if (Bot.GetMonsterCount() >= 5) return false;
            bool activate = ShouldUseHopEarNow();
            if (activate)
            {
                string why = ChainThreatensOurMonsterZone() ? "activate (defensive convert)" : "activate (make live interrupt)";
                MMLog("[MM.HopEar]", why);
            }
            return activate;
        }

        private bool AccelSynchroStardustDragon_Activate()
        {
            if (Card == null || Card.Id != CardId.AccelSynchroStardustDragon) return false;
            if (Card.Location != CardLocation.MonsterZone) return false;
            if (DefaultCheckWhetherCardIsNegated(Card)) return false;

            bool isMainPhase = Duel.Phase == DuelPhase.Main1 || Duel.Phase == DuelPhase.Main2;
            if (!isMainPhase) return false;

            bool haveLv2TunerOnField = Bot.GetMonsters().Any(m => m != null && m.IsFaceup() && m.Level <= 2 && m.HasType(CardType.Tuner) && m != Card);
            bool canConvert = haveLv2TunerOnField && Bot.HasInExtra(CardId.StardustDragon)
                && (Bot.HasInExtra(CardId.SatelliteWarrior) || Bot.HasInExtra(CardId.BaronneDeFleur));
            if (canConvert)
            {
                _tributedThisTurn = true;
                _preferSatelliteFromAccel = ShouldPreferSatelliteOverBaronne();
                MMLog("[MM.Accel]", "tribute convert to Stardust route preferSatellite=" + (_preferSatelliteFromAccel ? "Y" : "N")
                    + " enemyTargets=" + CountEnemyOnFieldTargets());
                return true;
            }

            bool canReviveTuner = Bot.GetMonsterCount() < 5 &&
                                  Bot.Graveyard.Any(c => c != null && c.IsMonster() && c.Level <= 2 && c.HasType(CardType.Tuner));
            if (canReviveTuner)
            {
                MMLog("[MM.Accel]", "revive lv2 tuner");
                return true;
            }

            return false;
        }

        private bool StardustDragon_Activate()
        {
            if (Card == null || Card.Id != CardId.StardustDragon) return false;

            if (Card.Location == CardLocation.MonsterZone)
            {
                if (DefaultCheckWhetherCardIsNegated(Card)) return false;
                bool enemyChain = Duel.LastChainPlayer == 1;
                bool threatensUs = enemyChain &&
                                  (Duel.LastChainTargets.Any(t => t != null && t.Controller == 0)
                                   || EnemyHighPressure());
                if (threatensUs)
                {
                    _tributedThisTurn = true;
                    _stardustDragonTributedThisTurn = true;
                    MMLog("[MM.Stardust]", "negate destruction line");
                    return true;
                }
            }

            if (Card.Location == CardLocation.Grave)
            {
                bool revive = _stardustDragonTributedThisTurn
                           && Duel.Phase == DuelPhase.End
                           && Bot.GetMonsterCount() < 5;
                if (revive) MMLog("[MM.Stardust]", "end phase self revive");
                return revive;
            }

            return false;
        }

        private bool StardustWarrior_Activate()
        {
            if (Card == null || Card.Id != CardId.StardustWarrior) return false;

            if (Card.Location == CardLocation.MonsterZone)
            {
                if (DefaultCheckWhetherCardIsNegated(Card)) return false;
                bool oppPressure = Duel.Player == 1 &&
                                   (Duel.Phase == DuelPhase.Main1 || Duel.Phase == DuelPhase.Main2 || Duel.Phase == DuelPhase.BattleStart) &&
                                   Enemy.GetMonsterCount() > 0;
                if (oppPressure)
                {
                    _tributedThisTurn = true;
                    _stardustWarriorNegateUsedThisTurn = true;
                    MMLog("[MM.StardustWarrior]", "summon negate line");
                    return true;
                }
            }

            if (Card.Location == CardLocation.Grave)
            {
                bool canReviveSelf = _stardustWarriorNegateUsedThisTurn && Duel.Phase == DuelPhase.End && Bot.GetMonsterCount() < 5;
                if (canReviveSelf)
                {
                    MMLog("[MM.StardustWarrior]", "end phase self revive");
                    return true;
                }

                bool canFloat = _stardustWarriorLeftByOpponentThisTurn && Bot.GetMonsterCount() < 5 && Bot.HasInExtra(CardId.JunkWarriorExtreme);
                if (canFloat)
                {
                    MMLog("[MM.StardustWarrior]", "float into warrior synchro");
                    return true;
                }
            }

            return false;
        }

        // PURPOSE: Satellite Warrior float should follow the script setcode contract instead of a hardcoded 3-card list.
        // TRUSTS: script allows Level 8 or lower Warrior / Synchron / Stardust Synchros with different names.
        // RESET: stateless helper.
        private bool IsSatelliteReviveTarget(ClientCard c)
        {
            return c != null
                && c.Location == CardLocation.Grave
                && c.HasType(CardType.Synchro)
                && c.Level <= 8
                && (c.HasSetcode(0x66) || c.HasSetcode(0x1017) || c.HasSetcode(0xa3));
        }

        private List<ClientCard> GetSatelliteRevivePool(IEnumerable<ClientCard> source)
        {
            if (source == null) return new List<ClientCard>();

            int[] prefer =
            {
                CardId.StardustWarrior,
                CardId.AccelSynchroStardustDragon,
                CardId.StardustDragon,
                CardId.JunkWarriorExtreme
            };

            return source.Where(IsSatelliteReviveTarget)
                .GroupBy(c => c.Id)
                .Select(g => g.First())
                .OrderBy(c =>
                {
                    int idx = Array.IndexOf(prefer, c.Id);
                    return idx < 0 ? 99 : idx;
                })
                .ThenByDescending(c => c.Attack)
                .ToList();
        }

        // PURPOSE: Dharc only pays off when it has a real linked main-zone to place the stolen DARK monster.
        // TRUSTS: Link-2 Dharc line is intended to be summoned to an Extra Monster Zone in this deck.
        // RESET: stateless helper.
        private bool HasFreeDharcStealLaneIfSummoned()
        {
            bool leftLane = Bot.MonsterZone[5] == null && (Bot.MonsterZone[0] == null || Bot.MonsterZone[2] == null);
            bool rightLane = Bot.MonsterZone[6] == null && (Bot.MonsterZone[2] == null || Bot.MonsterZone[4] == null);
            return leftLane || rightLane;
        }

        private bool HasFreeDharcLinkedMainZone(ClientCard link)
        {
            if (link == null || !link.IsCode(CardId.DharcTheDarkCharmerGloomy)) return false;
            int freeMain = 0;
            for (int i = 0; i < 5; ++i)
                if (Bot.MonsterZone[i] == null)
                    freeMain |= 1 << i;
            return ((link.GetLinkedZones() & 0x1F) & freeMain) != 0;
        }

        private bool SatelliteWarrior_Activate()
        {
            if (Card == null || Card.Id != CardId.SatelliteWarrior) return false;

            if (Card.Location == CardLocation.MonsterZone)
            {
                int synchrosInGy = Bot.Graveyard.Count(c => c != null && c.HasType(CardType.Synchro));
                bool canPop = synchrosInGy > 0 && Enemy.GetFieldCount() > 0;
                if (canPop) MMLog("[MM.Satellite]", "on summon destroy");
                return canPop;
            }

            if (Card.Location == CardLocation.Grave)
            {
                var revivePool = GetSatelliteRevivePool(Bot.Graveyard);
                bool canRevive = revivePool.Count > 0 && Bot.GetMonsterCount() < 5;
                if (canRevive) MMLog("[MM.Satellite]", "destroyed revive package");
                return canRevive;
            }

            return false;
        }

        private bool BaronneDeFleur_Negate()
        {
            if (Card == null || Card.Id != CardId.BaronneDeFleur) return false;
            if (Card.Location != CardLocation.MonsterZone) return false;
            if (CheckWhetherNegated(true, true, CardType.Monster)) return false;
            if (ActivateDescription != Util.GetStringId(CardId.BaronneDeFleur, 1)) return false;
            if (Duel.LastChainPlayer != 1 || Duel.CurrentChain == null || Duel.CurrentChain.Count == 0) return false;

            ChainInfo currentChain = Duel.GetCurrentSolvingChainInfo();
            if (currentChain != null && currentChain.ActivatePlayer == 0 && currentChain.RelatedCard == Card)
                return false;

            if (currentChain != null && currentChain.ActivatePlayer == 1 && currentChain.RelatedCard != null
                && !CheckCardShouldNegate(currentChain) && !EnemyHighPressure())
            {
                return false;
            }

            ClientCard last = Util.GetLastChainCard();
            if (last != null && last.Controller == 1 && !CheckCardShouldNegate(last) && !EnemyHighPressure())
            {
                return false;
            }

            MMLog("[MM.Baronne]", "negate");
            return true;
        }

        private bool BaronneDeFleur_Destroy()
        {
            if (Card == null || Card.Id != CardId.BaronneDeFleur) return false;
            if (Card.Location != CardLocation.MonsterZone) return false;
            if (CheckWhetherNegated(true, true, CardType.Monster)) return false;
            if (ActivateDescription == Util.GetStringId(CardId.BaronneDeFleur, 1)
                || ActivateDescription == Util.GetStringId(CardId.BaronneDeFleur, 2))
                return false;
            if (Duel.CurrentChainInfo != null && Duel.CurrentChainInfo.Any(c => c != null && c.ActivatePlayer == 0 && c.RelatedCard == Card))
                return false;

            if (Duel.Player != 0) return false;
            if (Duel.Phase != DuelPhase.Main1 && Duel.Phase != DuelPhase.Main2) return false;
            if (!HasEnemyFaceupTargetableCard()) return false;

            ClientCard enemyTarget = null;
            var ranked = GetNormalEnemyTargetList(true, true, CardType.Monster);
            foreach (var pick in ranked)
            {
                if (pick != null && pick.IsFaceup() && IsEnemyFieldCard(pick))
                {
                    enemyTarget = pick;
                    break;
                }
            }

            enemyTarget = enemyTarget
                ?? Enemy.GetMonsters().Where(m => m != null && m.IsFaceup() && !m.IsShouldNotBeTarget())
                    .Concat(Enemy.GetSpells().Where(s => s != null && s.IsFaceup() && !s.IsShouldNotBeTarget()))
                    .OrderByDescending(ScoreEnemyCardForRemoval).FirstOrDefault();
            if (enemyTarget == null) return false;

            AI.SelectCard(enemyTarget);
            MMLog("[MM.Baronne]", "destroy one");
            return true;
        }

        private bool BaronneDeFleur_Activate()
        {
            return BaronneDeFleur_Negate() || BaronneDeFleur_Destroy();
        }

        private bool VictimSanctuary_Activate()
        {
            if (Card == null || Card.Id != CardId.StardustDragonVictimSanctuary) return false;
            if (Card.Location != CardLocation.Grave) return false;
            if (!_tributedThisTurn) return false;
            if (Bot.GetMonsterCount() >= 5) return false;
            if (ShouldHoldVictimSanctuaryForHopEar())
            {
                MMLog("[MM.VictimSanctuary]", "hold (Hop Ear line first)");
                return false;
            }

            bool canSummon = Bot.HasInExtra(CardId.StardustDragon) || Bot.HasInExtra(CardId.StardustWarrior);
            if (canSummon)
            {
                _pendingVictimSanctuarySummonPrompt = true;
                MMLog("[MM.VictimSanctuary]", "banish from GY -> summon Stardust");
            }
            return canSummon;
        }

        private bool JunkWarriorExtreme_Activate()
        {
            if (Card == null || Card.Id != CardId.JunkWarriorExtreme) return false;
            if (Card.Location != CardLocation.MonsterZone) return false;
            bool canRevive = Bot.GetMonsterCount() < 5 && Bot.Graveyard.Any(c => c != null && c.IsMonster() && c.Level <= 2);
            if (canRevive) MMLog("[MM.JunkExtreme]", "summon lv2 or lower from GY");
            return canRevive;
        }

        private bool Seal_Summon()
        {
            if (Duel.Player != 0) return false;
            if (Duel.Phase != DuelPhase.Main1 && Duel.Phase != DuelPhase.Main2) return false;
            if (CheckShouldNoMoreSpSummon(CardLocation.Extra)) return false;
            if (!TwoDragonsOnField()) return false;
            if (ShouldCommitToRedmdSealLineNow()) return false;

            // Don't over-link if we already have solid interruptions.
            bool haveFullmetal = Bot.HasInMonstersZone(CardId.RedEyesBlackFullmetalDragon, true);
            bool haveMX = Bot.HasInMonstersZone(CardId.MetalzoaX, true);
            bool haveSeal = Bot.HasInMonstersZone(CardId.HieraticSealOfTheHeavenlySpheres, true);
            var dragons = Bot.MonsterZone.GetMonsters().Where(m => m != null && m.IsFaceup() && m.HasRace(CardRace.Dragon)).ToList();
            bool haveSafeDragonPair = dragons.Count(m => !IsBoss(m)) >= 2;

            if (haveSeal) return false;
            // If a boss is online, only push Seal when we can do it with non-boss dragons.
            if ((haveFullmetal || haveMX) && !haveSafeDragonPair) return false;
            // Keep pressure pieces when we can convert this turn's battle into lethal.
            if (CanDirectLethalNow()) return false;

            MMLog("[MM.Seal.SS]", "link summon");
            return true;
        }

        // --- Seal (opp turn) — cost + bounce + float ---
        private bool Seal_Bounce_FreeChain()
        {
            if (!Bot.HasInMonstersZone(CardId.HieraticSealOfTheHeavenlySpheres, true)) return false;
            if (Duel.Player != 1) return false;
            if (!(Duel.Phase == DuelPhase.Main1 || Duel.Phase == DuelPhase.BattleStart ||
                  Duel.Phase == DuelPhase.Battle || Duel.Phase == DuelPhase.Main2)) return false;

            // PURPOSE: prevent low-value self-bounce lines that reduce our board presence.
            // TRUSTS: we keep Seal for interruption and only fire into enemy face-up threats.
            bool enemyFaceupMonster = Enemy.GetMonsters().Any(m => m != null && m.IsFaceup());
            bool enemyStickyBackrow = Enemy.GetSpells().Any(s => s != null && s.IsFaceup() &&
                (s.HasType(CardType.Continuous) || s.HasType(CardType.Field) || s.HasType(CardType.Equip)));
            bool enemyFaceup = enemyFaceupMonster || enemyStickyBackrow;
            if (enemyFaceup) MMLog("[MM.Seal.Bounce]", "activate (enemy target)");
            return enemyFaceup;
        }

        // --- Swordsman (tutor) ---
        private bool Swordsman_RevealAdd()
        {
            if (Duel.Player != 0) return false;
            if (Duel.Phase != DuelPhase.Main1 && Duel.Phase != DuelPhase.Main2) return false;
            if (!Bot.HasInHand(CardId.MetalflameSwordsman)) return false;

            bool wantTimeProtect = ShouldPrioritizeTimeEngineNow();
            bool needTrapCoverage = !HasMetalmorphTrapCoverage(CardId.MaxMetalmorph)
                                 || !HasMetalmorphTrapCoverage(CardId.FlameCoatingMetalmorph)
                                 || !HasMetalmorphTrapCoverage(CardId.TimeEngine);
            bool haveBoss = Bot.HasInMonstersZone(CardId.RedEyesBlackFullmetalDragon, true)
                         || Bot.HasInMonstersZone(CardId.MetalzoaX, true);
            bool wantBossBody = !haveBoss && CheckRemainInDeck(CardId.RedEyesBlackFullmetalDragon, CardId.MetalzoaX) > 0;
            bool wantClawsBridge = Bot.HasInHand(CardId.RedEyesMetalClawsDragon) && !Bot.HasInMonstersZone(CardId.RedEyesMetalClawsDragon, true);
            if (!needTrapCoverage && !wantTimeProtect && !wantBossBody && !wantClawsBridge) return false;

            bool hasTarget = CheckRemainInDeck(CardId.MaxMetalmorph, CardId.FlameCoatingMetalmorph, CardId.TimeEngine,
                                               CardId.RedEyesBlackFullmetalDragon, CardId.MetalzoaX, CardId.RedEyesMetalClawsDragon) > 0;
            if (!hasTarget) return false;

            MMLog("[MM.Swordsman]", wantTimeProtect ? "reveal/add (prioritize Time Engine)" : "reveal/add line");
            return true;
        }

        // --- Claws_SS_FromHand ---
        private bool Claws_SS_FromHand()
        {
            if (Duel.Player != 0) return false;
            if (Duel.Phase != DuelPhase.Main1 && Duel.Phase != DuelPhase.Main2) return false;

            if (!Bot.HasInHand(CardId.RedEyesMetalClawsDragon)) return false;
            if (CheckShouldNoMoreSpSummon(CardLocation.Hand)) return false;
            if (ShouldCommitToRedmdSealLineNow()) return false;
            if (CheckWhetherWillbeRemoved() && !EnemyHighPressure()) return false;

            // Prefer non-Metalmorph costs, but allow spare Metalmorphs to keep combo flow.
            if (!HasFacedownSendableForClaws()) return false;
            MMLog("[MM.Claws.SS]", "special summon from hand");
            return true;
        }

        private bool Claws_AddMetalmorph_OnSummon()
        {
            // Trigger on either player's turn; it's a normal on-summon effect
            if (!Bot.HasInMonstersZone(CardId.RedEyesMetalClawsDragon, true)) return false;
            bool haveInGy = Bot.Graveyard.Any(g => g != null &&
                (g.IsCode(CardId.MaxMetalmorph) || g.IsCode(CardId.FlameCoatingMetalmorph) || g.IsCode(CardId.TimeEngine)));
            bool haveInDeck = CheckRemainInDeck(CardId.MaxMetalmorph, CardId.FlameCoatingMetalmorph, CardId.TimeEngine) > 0;
            if (!(haveInGy || haveInDeck)) return false;
            MMLog("[MM.Claws.Add]", "on-summon add");
            return true;
        }

        private bool MaxMetalmorph_MyTurn_Claws()
        {
            if (Duel.Player != 0) return false;
            if (Duel.Phase != DuelPhase.Main1 && Duel.Phase != DuelPhase.Main2) return false;
            if (Card == null || Card.Id != CardId.MaxMetalmorph) return false;
            if (Card.Location != CardLocation.SpellZone || !Card.IsFacedown()) return false;
            if (!Bot.HasInMonstersZone(CardId.RedEyesMetalClawsDragon, true)) return false;
            if (!HasMaxSummonCandidate(CardId.RedEyesBlackFullmetalDragon)) return false;

            MMLog("[MM.Max.My]", "convert Claws -> Fullmetal");
            return true;
        }

        private bool MaxMetalmorph_MyTurn_Generic()
        {
            if (Duel.Player != 0) return false;
            if (Duel.Phase != DuelPhase.Main1 && Duel.Phase != DuelPhase.Main2) return false;
            if (Card == null || Card.Id != CardId.MaxMetalmorph) return false;
            if (Card.Location != CardLocation.SpellZone || !Card.IsFacedown()) return false;
            if (!HasSafeTributeForMetallize()) return false;
            if (!HasMeaningfulMetallizeUpgradeForMax()) return false;

            bool haveBridge = Bot.HasInMonstersZone(CardId.ZoaTheFiendishBeast, true)
                           || Bot.HasInMonstersZone(CardId.MetalIllusionist, true)
                           || Bot.HasInMonstersZone(CardId.BystialMagnamhut, true)
                           || Bot.HasInMonstersZone(CardId.BystialDruiswurm, true);
            if (!haveBridge) return false;

            bool haveBoss = Bot.HasInMonstersZone(CardId.RedEyesBlackFullmetalDragon, true)
                         || Bot.HasInMonstersZone(CardId.MetalzoaX, true);
            bool haveSwordsman = Bot.HasInMonstersZone(CardId.MetalflameSwordsman, true);
            bool canZoaLineIntoMx = Bot.HasInMonstersZone(CardId.ZoaTheFiendishBeast, true)
                                 && !Bot.HasInMonstersZone(CardId.MetalzoaX, true)
                                 && HasMaxSummonCandidate(CardId.MetalzoaX);
            bool shouldPushNow = canZoaLineIntoMx || !haveBoss || EnemyHighPressure();
            if (!shouldPushNow) return false;

            // IMPORTANT: don't "upgrade" into a duplicate Swordsman when we already have one.
            bool canSwordsman = !haveSwordsman && HasMaxSummonCandidate(CardId.MetalflameSwordsman);
            int targetId =
                (Bot.HasInMonstersZone(CardId.ZoaTheFiendishBeast, true) && HasMaxSummonCandidate(CardId.MetalzoaX))
                    ? CardId.MetalzoaX
                    : (HasMaxSummonCandidate(CardId.RedEyesBlackFullmetalDragon)
                        ? CardId.RedEyesBlackFullmetalDragon
                        : (HasMaxSummonCandidate(CardId.MetalzoaX)
                            ? CardId.MetalzoaX
                            : (((!haveBoss || EnemyHighPressure()) && canSwordsman) ? CardId.MetalflameSwordsman : 0)));

            if (targetId == 0) return false;
            MMLog("[MM.Max.My]", "generic convert target=" + targetId);
            return true;
        }

        private bool GoToEndPhase_HoldMetalmorph()
        {
            // Only on our Main Phase
            if (Duel.Player != 0) return false;
            if (Duel.Phase != DuelPhase.Main1 && Duel.Phase != DuelPhase.Main2) return false;
            // Don't pass early if we'd discard and still can set cards.
            if (HasSettableForHandLimit()) return false;

            // Don't pass away battle when we can cash in pressure this turn.
            if (CanDirectLethalNow()) return false;
            if (Enemy.GetMonsterCount() == 0 && HaveAnyBattleAttackerNow()) return false;

            bool metaReady = HasSet(CardId.MaxMetalmorph) || HasSet(CardId.FlameCoatingMetalmorph);
            bool hasBridge = Bot.HasInMonstersZone(CardId.MetalIllusionist, true)
                          || Bot.HasInMonstersZone(CardId.ZoaTheFiendishBeast, true);

            bool clawsAccel = Bot.HasInMonstersZone(CardId.RedEyesMetalClawsDragon, true);
            bool hold = metaReady && hasBridge && !clawsAccel;
            if (hold) MMLog("[MM.EndPhase]", "hold for opp-turn convert");
            return hold;
        }

        private bool SkipBattleWhenNoPositiveTrades()
        {
            if (Duel.Player != 0 || Duel.Phase != DuelPhase.BattleStart) return false;

            var attackers = Bot.GetMonsters()
                .Where(m => m != null && m.IsFaceup() && m.Attack > 0) // no CanAttack() in this API
                .OrderByDescending(m => m.Attack)
                .ToList();
            if (attackers.Count == 0) return false;

            var defenders = Enemy.GetMonsters().Where(m => m != null && m.IsFaceup()).ToList();
            if (defenders.Count == 0) return false; // we’ll allow direct attacks; handled by your target picker

            bool haveWinningTrade = false;
            bool haveRaidersSetupSwing = false;

            foreach (var atk in attackers)
            {
                foreach (var def in defenders)
                {
                    atk.RealPower = atk.Attack;
                    def.RealPower = def.GetDefensePower();
                    if (!OnPreBattleBetween(atk, def))
                        continue;

                    if (atk.RealPower > def.RealPower
                        || (atk.RealPower == def.RealPower && def.IsAttack() && Bot.GetMonsterCount() >= Enemy.GetMonsterCount()))
                    {
                        haveWinningTrade = true;
                        break;
                    }

                    int setupDamage;
                    int setupPostAtk;
                    if (IsHeavyMetalRaidersSetupAttack(atk, def, out setupDamage, out setupPostAtk))
                        haveRaidersSetupSwing = true;
                }

                if (haveWinningTrade) break;
            }

            if (!haveWinningTrade && !haveRaidersSetupSwing)
            {
                MMLog("[MM.Battle]", "skip battle (no positive trades)");
                return true;
            }
            return false;
        }

        // ============================================================
        // Utilities / S/T handlers
        // ============================================================
        private bool IncomingMachine_Use()
        {
            // Activate only on OUR Main Phases (search), never in Battle/opp turn.
            if (Duel.Player == 0 && (Duel.Phase == DuelPhase.Main1 || Duel.Phase == DuelPhase.Main2))
            {
                if (ShouldCommitToRedmdSealLineNow())
                {
                    MMLog("[MM.IncomingMachine]", "hold (commit REDMD line)");
                    return false;
                }

                // Prefer to fill missing traps first; avoids redundant picks.
                bool want =
                    !HasSet(CardId.MaxMetalmorph) ||
                    (!HasSet(CardId.FlameCoatingMetalmorph) && CheckRemainInDeck(CardId.FlameCoatingMetalmorph) > 0) ||
                    (!HasSet(CardId.TimeEngine) && CheckRemainInDeck(CardId.TimeEngine) > 0);
                bool activate = want && IHaveRoomST();
                if (activate) MMLog("[MM.IncomingMachine]", "activate (main phase search)");
                return activate;
            }

            // GY rider: only from GY, only if a Metalmorph trap is face-up, only when opp has DEF body.
            if (Card != null && Card.Location == CardLocation.Grave)
            {
                if (Duel.Player != 0) return false;
                // Keep this in pre-battle windows to avoid firing after attackers are spent.
                if (!(Duel.Phase == DuelPhase.Main1 || Duel.Phase == DuelPhase.BattleStart)) return false;
                if (!HaveAnyBattleAttackerNow()) return false;

                bool controlMetalmorph = Bot.SpellZone.Any(s => s != null && s.IsFaceup()
                    && (s.IsCode(CardId.MaxMetalmorph) || s.IsCode(CardId.FlameCoatingMetalmorph) || s.IsCode(CardId.TimeEngine)));
                bool oppHasDef = Enemy.GetMonsters().Any(m => m != null && m.IsFaceup() && m.IsDefense());
                bool canPunishDef = Bot.GetMonsters().Any(m => m != null && m.IsFaceup() && m.Attack > 0
                    && Enemy.GetMonsters().Any(e => e != null && e.IsFaceup() && e.IsDefense() && m.Attack > e.GetDefensePower()));
                bool activateGy = controlMetalmorph && oppHasDef && canPunishDef;
                if (activateGy) MMLog("[MM.IncomingMachine]", "activate (GY rider)");
                return activateGy;
            }
            return false;
        }

        private bool CalledBy_Effect()
        {
            if (_calledByUsedThisChain) return false;      // once per chain only
            if (Duel.LastChainPlayer != 1) return false;   // only vs opponent chains

            var last = Util.GetLastChainCard();
            if (last == null) return false;

            // Don’t fire at already-disabled effects
            if (last.IsDisabled()) return false;

            // Resolve alias/origin
            int originId = (last.Data != null && last.Data.Alias > 0) ? last.Data.Alias : last.Id;

            // --- Heuristic 1: skip Maxx "C" if we’re already ahead ---
            if (last.IsCode(CardId.MaxxC))
            {
                bool ahead = (Bot.GetMonsterCount() > Enemy.GetMonsterCount())
                             || (Bot.LifePoints > Enemy.LifePoints);
                if (ahead)
                {
                    return false;
                }
            }

            // --- Main path: reactively hit the resolving monster name ---
            bool oppHasSameName = Enemy.Graveyard.Any(g => g != null && g.IsMonster() && g.Id == originId);

            if (oppHasSameName)
            {
                _calledByUsedThisChain = true;
                MMLog("[MM.CalledBy]", "target matched chain card id=" + originId);
                return true;
            }

            // --- Heuristic 2: fallback — snipe a dangerous GY monster ---
            {
                var dangerIds = new HashSet<int> {
                    99937011, // Orcust Harp Horror
                    63542003, // Orcust Knightmare
                    9411399,  // Horus the Black Flame Dragon LV6
                    28954097, // Horus LV8
                    30680659  // Misc dangerous GY bodies
                };

                var graveTarget = Enemy.Graveyard.FirstOrDefault(c => c != null && c.IsMonster() && dangerIds.Contains(c.Id));
                if (graveTarget != null)
                {
                    _calledByUsedThisChain = true;
                    AI.SelectCard(graveTarget);
                    MMLog("[MM.CalledBy]", "fallback dangerous GY id=" + graveTarget.Id);
                    return true;
                }
            }

            return false;
        }

        private bool AshBlossomActivate()
        {
            bool activate = DefaultAshBlossomAndJoyousSpring();
            if (activate) MMLog("[MM.Ash]", "activate");
            return activate;
        }

        private bool MaxxC_Activate()
        {
            // Skip if our Maxx “C” is already negated
            if (Card.IsDisabled()) return false;

            // Only chain to opponent’s actions
            if (Duel.LastChainPlayer == 0) return false;

            bool activate = Duel.Player == 1;
            if (activate) MMLog("[MM.MaxxC]", "activate");
            return activate;
        }

        // PURPOSE: conserve Imperm when Seal can answer the same chain pressure.
        // TRUSTS: DefaultInfiniteImpermanence already validates legal timing/targets.
        private bool InfiniteImpermanence_Smart()
        {
            if (!DefaultInfiniteImpermanence()) return false;
            if (CheckWhetherNegated(true, true, CardType.Trap)) return false;

            ChainInfo current = Duel.GetCurrentSolvingChainInfo();
            if (current != null && current.ActivatePlayer == 1 && current.RelatedCard != null
                && !CheckCardShouldNegate(current) && !EnemyHighPressure())
            {
                MMLog("[MM.Imperm]", "hold (low value chain)");
                return false;
            }

            var negateTargets = GetMonsterListForTargetNegate(true, CardType.Trap);
            if (Duel.LastChainPlayer == 1 && negateTargets.Count == 0 && !EnemyHighPressure())
            {
                MMLog("[MM.Imperm]", "hold (no meaningful negate target)");
                return false;
            }

            // If Seal is online and can bounce the current chain source, keep Imperm for later.
            if (Bot.HasInMonstersZone(CardId.HieraticSealOfTheHeavenlySpheres, true) && Seal_Bounce_FreeChain())
            {
                var last = Util.GetLastChainCard();
                bool chainFromEnemyFaceupField =
                    last != null &&
                    last.Controller == 1 &&
                    last.IsFaceup() &&
                    (last.Location == CardLocation.MonsterZone || last.Location == CardLocation.SpellZone) &&
                    !last.IsShouldNotBeTarget();

                if (chainFromEnemyFaceupField)
                {
                    MMLog("[MM.Imperm]", "hold (Seal available for same chain)");
                    return false;
                }
            }

            MMLog("[MM.Imperm]", "activate");
            return true;
        }

        private bool CrossoutDesignatorActivate()
        {
            if (Duel.LastChainPlayer != 1) return false;
            if (CheckWhetherNegated(true, true, CardType.Spell)) return false;
            var last = Util.GetLastChainCard();
            if (last == null || last.IsDisabled()) return false;
            if (!CheckCardShouldNegate(last) && !EnemyHighPressure()) return false;

            int code = last.Id;
            int alias = last.Alias;
            if (alias != 0 && alias - code < 10) code = alias;
            if (code == 0 || code == CardId.CrossoutDesignator) return false;
            if (DefaultCheckWhetherCardIdIsNegated(code)) return false;

            // SCRIPT CONTRACT (c65681983): announce list is built from MAIN DECK codes and forbids Extra Deck types.
            // If we pre-announce an Extra Deck monster (e.g., S:P Little Knight), the engine rejects it with:
            //   "Pre-announced card cant be used: <id>"
            // IMPORTANT: do NOT call YGOSharp.OCGWrapper.Card.Get(code) here.
            // In some builds only NamedCardsManager is initialized; CardsManager._cards can be null and Card.Get(code) throws.
            // We can determine Extra Deck type directly from the chain card's runtime type flags.
            if (last.IsExtraCard())
            {
                MMLog("[MM.CROSSOUT]", "veto (illegal announce extra deck id=" + code + ")");
                return false;
            }

            if (CheckRemainInDeck(code) <= 0) return false;

            if (Card.Location != CardLocation.SpellZone)
                SelectSTPlace(null, true, null);
            AI.SelectAnnounceID(code);
            MMLog("[MM.CROSSOUT]", "announce id=" + code);
            return true;
        }

        private bool HeavyMetalRaiders_Activate()
        {
            if (Card == null || Card.Id != CardId.HeavyMetalRaiders) return false;

            // Heavy Metal Raiders second effect:
            // "Once per turn ... You can Special Summon 1 DARK Machine monster from your hand."
            if (Card.Location == CardLocation.SpellZone && Card.IsFaceup())
            {
                bool haveMZone = Bot.GetMonsterCount() < 5;
                bool haveDarkMachineInHand = Bot.Hand.Any(IsDarkMachine);
                bool activateTrigger = haveMZone && haveDarkMachineInHand;
                if (activateTrigger) MMLog("[MM.Raiders]", "trigger summon from hand");
                return activateTrigger;
            }

            if (!IHaveRoomST()) return false;
            bool activate = !HasFaceup(CardId.HeavyMetalRaiders);
            if (activate) MMLog("[MM.Raiders]", "activate field spell");
            return activate;
        }
        private bool TimeEngine_Activate()
        {
            if (Card != null && Card.Location == CardLocation.SpellZone
                && CheckWhetherNegated(true, true, CardType.Trap))
            {
                return false;
            }

            int recentDestroyed = _lastOwnLikelyDestroyedMonster;
            if (recentDestroyed != 0) _lastOwnLikelyDestroyedMonster = 0;

            bool lastLikelyTiny = IsTimeEngineLowValueCode(recentDestroyed);
            bool lastLikelyHigh = IsTimeEngineHighValueCode(recentDestroyed);
            if (recentDestroyed != 0 && lastLikelyTiny && !lastLikelyHigh)
            {
                MMLog("[MM.TimeEngine]", "hold (recent destroyed target too small)");
                return false;
            }

            bool havePremiumReviveInGy = Bot.Graveyard.Any(c => c != null && c.IsMonster() && c.IsCanRevive() &&
                                                             (IsTimeEngineHighValueCode(c.Id)
                                                              || (c.HasRace(CardRace.Machine) && c.Level >= 5)));

            if (Duel.Player == 1)
            {
                bool activateOpp = Enemy.GetMonsterCount() >= 1 || havePremiumReviveInGy;
                if (activateOpp)
                    MMLog("[MM.TimeEngine]",
                        havePremiumReviveInGy ? "activate (opp turn premium revive)" : "activate (opp turn)");
                return activateOpp;
            }

            if (Duel.Player == 0 && (Duel.Phase == DuelPhase.Main1 || Duel.Phase == DuelPhase.Main2))
            {
                bool activateMain = Enemy.GetMonsterCount() > Bot.GetMonsterCount() || havePremiumReviveInGy;
                if (activateMain)
                    MMLog("[MM.TimeEngine]",
                        havePremiumReviveInGy ? "activate (main phase premium revive)" : "activate (main phase pressure)");
                return activateMain;
            }

            bool activateOther = Enemy.GetMonsterCount() >= 1 || havePremiumReviveInGy;
            if (activateOther)
                MMLog("[MM.TimeEngine]",
                    havePremiumReviveInGy ? "activate (premium revive)" : "activate (enemy board present)");
            return activateOther;
        }

        // Metalzoa X
        private bool MetalzoaX_HandRevealSet()
        {
            if (Card == null || Card.Location != CardLocation.Hand) return false;
            if (Duel.Player != 0) return false;
            if (Duel.Phase != DuelPhase.Main1 && Duel.Phase != DuelPhase.Main2) return false;
            bool wantTimeProtect = ShouldPrioritizeTimeEngineNow();
            bool haveFaceDownMeta = Bot.SpellZone.Any(s => s != null && s.IsFacedown()
                && (s.IsCode(CardId.MaxMetalmorph) || s.IsCode(CardId.FlameCoatingMetalmorph) || s.IsCode(CardId.TimeEngine)));
            if (haveFaceDownMeta && !wantTimeProtect) return false;
            if (!IHaveRoomST()) return false;
            MMLog("[MM.MetalzoaX]", wantTimeProtect ? "reveal/set (prioritize Time Engine)" : "reveal/set Metalmorph");
            return true;
        }
        private bool MetalzoaX_QuickPop()
        {
            if (!Bot.HasInMonstersZone(CardId.MetalzoaX, true)) return false;
            if (Duel.LastChainPlayer != 1) return false;
            if (CheckWhetherNegated(true, true, CardType.Monster)) return false;
            ChainInfo current = Duel.GetCurrentSolvingChainInfo();
            if (current != null && current.ActivatePlayer == 1 && current.RelatedCard != null
                && !CheckCardShouldNegate(current) && !EnemyHighPressure())
            {
                return false;
            }

            var ranked = GetNormalEnemyTargetList(true, true, CardType.Monster);
            bool hasTarget = ranked.Any(c => c != null && IsEnemyFieldCard(c));
            if (hasTarget) MMLog("[MM.MetalzoaX.Pop]", "activate");
            return hasTarget;
        }


        // ============================================================
        // Smart set & prompt handlers
        // ============================================================
        private bool SpellSet_Smart()
        {
            if (!IHaveRoomST()) return false;

            bool handLimitMode = ShouldSetForHandLimit();

            // Normal mode: avoid duplicate face-down setup pieces.
            // Hand-limit mode: allow duplicates to reduce discard loss.
            if (!handLimitMode)
            {
                if (Card.IsCode(CardId.MaxMetalmorph) && HasSet(CardId.MaxMetalmorph)) return false;
                if (Card.IsCode(CardId.FlameCoatingMetalmorph) && HasSet(CardId.FlameCoatingMetalmorph)) return false;
                if (Card.IsCode(CardId.TimeEngine) && HasSet(CardId.TimeEngine)) return false;
            }

            if (Card.IsCode(CardId.CalledByTheGrave) || Card.IsCode(CardId.CrossoutDesignator) ||
                Card.IsCode(CardId.InfiniteImpermanence) ||
                Card.IsCode(CardId.MaxMetalmorph) || Card.IsCode(CardId.FlameCoatingMetalmorph) || Card.IsCode(CardId.TimeEngine))
            {
                SelectSTPlace(Card, true, null);
                MMLog("[MM.SpellSet]", "set id=" + Card.Id);
                return true;
            }

            // Hand-limit safety: set additional safe S/T to avoid discard.
            if (handLimitMode &&
                (Card.IsCode(CardId.IncomingMachine) || Card.IsCode(CardId.BrandedRegained)))
            {
                SelectSTPlace(Card, true, null);
                MMLog("[MM.SpellSet]", "set for hand-limit id=" + Card.Id);
                return true;
            }
            return false;
        }

        // ============================================================
        // Prompts & Selections
        // ============================================================
        public override bool OnSelectYesNo(int desc)
        {
            // Resolution choices must not rerun activation predicates (which may queue costs
            // or reject a now-spent revival). These are the optional benefits in the loaded Lua.
            ChainInfo solving = Duel.GetCurrentSolvingChainInfo();
            if (solving != null && solving.ActivatePlayer == 0)
            {
                if (solving.IsActivateCode(CardId.RedEyesBlackFullmetalDragon)
                    && desc == Util.GetStringId(CardId.RedEyesBlackFullmetalDragon, 2))
                    return Enemy.GetMonsters().Any(m => m != null && m.IsFaceup() && m.IsAttack() && m.BaseAttack > 0);
                if ((solving.IsActivateCode(CardId.MaxMetalmorph) && desc == Util.GetStringId(CardId.MaxMetalmorph, 2))
                    || (solving.IsActivateCode(CardId.FlameCoatingMetalmorph) && desc == Util.GetStringId(CardId.FlameCoatingMetalmorph, 1))
                    || (solving.IsActivateCode(CardId.MetalIllusionist) && desc == Util.GetStringId(CardId.MetalIllusionist, 2))
                    || (solving.IsActivateCode(CardId.KnightmarePhoenix) && desc == Util.GetStringId(CardId.KnightmarePhoenix, 1))
                    || (solving.IsActivateCode(CardId.TimeEngine)
                        && (desc == Util.GetStringId(CardId.TimeEngine, 2) || desc == Util.GetStringId(CardId.TimeEngine, 3))))
                    return true;
            }
            return base.OnSelectYesNo(desc);
        }

        public override IList<ClientCard> OnSelectCard(IList<ClientCard> cards, int min, int max, int hint, bool cancelable)
        {
            if (cards == null || cards.Count == 0) return base.OnSelectCard(cards, min, max, hint, cancelable);

            ChainInfo resolving = Duel.GetCurrentSolvingChainInfo();
            ChainInfo source = resolving ?? _acceptedPromptSource ?? Duel.CurrentChainInfo.LastOrDefault();
            ClientCard sourceCard = source != null ? source.RelatedCard : null;
            bool hasSource = source != null && source.ActivatePlayer == 0;
            int sourceId = source != null ? source.ActivateId : 0;
            CardLocation sourceLocation = source != null ? source.ActivateLocation : (CardLocation)0;
            Func<int, bool> isSource = id => hasSource && source.IsActivateCode(id);


            // Flame’s destroy-replacement — pick Flame
            if (hint == HintMsg.DestroyReplace)
            {
                var flame = cards.FirstOrDefault(c => c != null && c.IsCode(CardId.FlameCoatingMetalmorph));
                if (flame != null) return Util.CheckSelectCount(new List<ClientCard> { flame }, cards, min, max);
            }

            if (LooksLikeMagnamhutSearchPrompt(cards, min, max, hint))
            {
                var magPick = PickMagnamhutSearchTarget(cards) ?? cards[0];
                _pendingMagnamhutEndPhaseQueuePrompt = false;
                MMLog("[MM.Bystial.Magnamhut]", "end phase search pick=" + magPick.Id);
                return Util.CheckSelectCount(new List<ClientCard> { magPick }, cards, min, max);
            }

            bool selectingTimeEngine = isSource(CardId.TimeEngine);
            if (selectingTimeEngine)
            {
                return SelectTimeEngineTarget(cards, min, max);
            }


            // Fullmetal chooses damage from an offered original-ATK monster during resolution.
            if (resolving != null && isSource(CardId.RedEyesBlackFullmetalDragon))
            {
                bool looksLikeBurnTargetPrompt =
                    min == 1 && max == 1 && hint == HintMsg.Faceup
                    && cards.Any(c => c != null && c.Controller == 1 && c.Location == CardLocation.MonsterZone && c.IsMonster());

                if (looksLikeBurnTargetPrompt)
                {
                    var pickBurn = PickFullmetalBurnTarget(cards);
                    if (pickBurn != null)
                    {
                        int atk = pickBurn.BaseAttack > 0 ? pickBurn.BaseAttack : Math.Max(0, pickBurn.Attack);
                        MMLog("[MM.Fullmetal.BurnPick]", "pick=" + pickBurn.Id + " atk=" + atk);
                        return Util.CheckSelectCount(new List<ClientCard> { pickBurn }, cards, min, max);
                    }
                }
            }

            bool solvingOwnFidraulis = resolving != null && isSource(CardId.FidraulisHarmonia);

            if (((resolving == null && _pendingFidraulisRevealPrompt) || solvingOwnFidraulis) && LooksLikeFidraulisRevealPrompt(cards, max, hint))
            {
                if (_pendingFidraulisRevealDesiredCount > 1 && max <= 1)
                {
                    var nextReveal = PickNextFidraulisRevealCard(cards);
                    if (nextReveal != null)
                    {
                        AdvanceFidraulisRevealState(new List<ClientCard> { nextReveal });
                        MMLog("[MM.Fidraulis.RevealStep]", "pick=" + nextReveal.Id + " step=" + _pendingFidraulisRevealPickedIds.Count + "/" + _pendingFidraulisRevealDesiredCount);
                        return Util.CheckSelectCount(new List<ClientCard> { nextReveal }, cards, min, max);
                    }
                }
                else
                {
                    var reveal = PickFidraulisRevealCards(cards, min, max);
                    if (reveal != null && reveal.Count > 0)
                    {
                        AdvanceFidraulisRevealState(reveal);
                        MMLog("[MM.Fidraulis.Reveal]", "pick=" + string.Join(",", reveal.Select(c => c.Id.ToString()).ToArray()));
                        return Util.CheckSelectCount(reveal.ToList(), cards, min, max);
                    }
                }
            }

            // Fidraulis send routing can occasionally lose Card context; keep priority deterministic.
            if (_pendingFidraulisSendPrompt && (resolving == null || solvingOwnFidraulis) && LooksLikeFidraulisSendPrompt(cards, hint))
            {
                var send = PickFidraulisSendToGrave(cards) ?? cards[0];
                _pendingFidraulisSendPrompt = false;
                MMLog("[MM.Fidraulis.Send]", "pick=" + send.Id);
                return Util.CheckSelectCount(new List<ClientCard> { send }, cards, min, max);
            }

            if (_pendingFidraulisDestroyPrompt && (resolving == null || solvingOwnFidraulis) && LooksLikeFidraulisDestroyPrompt(cards, hint))
            {
                var destroy = PickBestEnemyTargetOnly(cards) ?? cards[0];
                _pendingFidraulisDestroyPrompt = false;
                MMLog("[MM.Fidraulis.Destroy]", "pick=" + destroy.Id);
                return Util.CheckSelectCount(new List<ClientCard> { destroy }, cards, min, max);
            }

            bool solvingOwnAccel = resolving != null && isSource(CardId.AccelSynchroStardustDragon);
            if (solvingOwnAccel)
            {
                var accelPick = SelectAccelSynchroPrompt(cards, min, max, hint);
                if (accelPick != null) return accelPick;
            }

            // Keep the final Accel branch deterministic even if Card context is stale or missing.
            if (solvingOwnAccel && LooksLikeAccelFinalSynchroPrompt(cards, hint))
            {
                bool preferSatellite = _preferSatelliteFromAccel || ShouldPreferSatelliteOverBaronne(cards);
                var satellite = cards.FirstOrDefault(c => c != null && c.IsCode(CardId.SatelliteWarrior));
                var baronne = cards.FirstOrDefault(c => c != null && c.IsCode(CardId.BaronneDeFleur));
                var forced =
                      (preferSatellite ? satellite : baronne)
                   ?? satellite
                   ?? baronne;
                if (forced != null)
                {
                    MMLog("[MM.Accel.SelectFallback]", "pick finalSynchro id=" + forced.Id + " preferSatellite=" + (preferSatellite ? "Y" : "N")
                        + " enemyTargets=" + CountEnemyOnFieldTargets());
                    return Util.CheckSelectCount(new List<ClientCard> { forced }, cards, min, max);
                }
            }

            if (isSource(CardId.StardustDragonVictimSanctuary) && LooksLikeVictimSanctuarySummonPrompt(cards, hint, min, max))
            {
                var stardust = PickVictimSanctuarySummon(cards) ?? cards[0];
                _pendingVictimSanctuarySummonPrompt = false;
                MMLog("[MM.VictimSanctuary.Select]", "pick=" + stardust.Id);
                return Util.CheckSelectCount(new List<ClientCard> { stardust }, cards, min, max);
            }

            if (hasSource)
            {
                // Illusionist / Zoa: which Metalmorph to set
                if (isSource(CardId.MetalIllusionist) || isSource(CardId.ZoaTheFiendishBeast))
                {
                    var pick0 = PickMetalmorphTutorCard(cards);
                    if (pick0 != null) MMLog("[MM.Meta.SetPick]", "source=" + sourceId + " pick=" + pick0.Id);
                    return Util.CheckSelectCount(new List<ClientCard> { pick0 }, cards, min, max);
                }

                // Swordsman — choose a card that "mentions Max"
                if (isSource(CardId.MetalflameSwordsman))
                {
                    var pick1 = PickMetalmorphTutorCard(cards) ?? cards[0];
                    return Util.CheckSelectCount(new List<ClientCard> { pick1 }, cards, min, max);
                }

                // Heavy Metal Raiders: pick DARK Machine from hand for trigger summon.
                if (isSource(CardId.HeavyMetalRaiders) &&
                    (hint == HintMsg.SpSummon || hint == HintMsg.OperateCard || hint == 505))
                {
                    var pool = cards.Where(c => c != null
                                             && c.Controller == 0
                                             && c.Location == CardLocation.Hand
                                             && IsDarkMachine(c)).ToList();

                    var pickRaiders =
                          pool.FirstOrDefault(c => c.IsCode(CardId.MetalIllusionist))
                       ?? pool.OrderByDescending(c => c.Attack).FirstOrDefault()
                       ?? cards.FirstOrDefault(IsDarkMachine)
                       ?? cards[0];

                    return Util.CheckSelectCount(new List<ClientCard> { pickRaiders }, cards, min, max);
                }

                // --- Max Metalmorph — release then SS (tribute-aware) ---
                if (isSource(CardId.MaxMetalmorph))
                {
                    if (hint == HintMsg.Equip)
                    {
                        var pickEquip = PickMetallizeEquipTarget(cards);
                        return Util.CheckSelectCount(new List<ClientCard> { pickEquip }, cards, min, max);
                    }

                    if (hint == HintMsg.Release)
                    {
                        _lastMetallizationWasWarrior5p = false;
                        _lastMetallizeRace = 0;
                        _lastMetallizeLevel = 0;

                        bool preferMx = Bot.HasInMonstersZone(CardId.ZoaTheFiendishBeast, true)
                                     && HasMaxSummonCandidate(CardId.MetalzoaX);
                        if (preferMx)
                        {
                            var zoa = cards.FirstOrDefault(c => c != null && c.IsFaceup() && c.IsCode(CardId.ZoaTheFiendishBeast));
                            if (zoa != null) { TrackMetallizeTribute(zoa); return Util.CheckSelectCount(new List<ClientCard> { zoa }, cards, min, max); }
                        }

                        // 0) Prefer Claws as a safe tribute when it advances a Max conversion line,
                        //     and ALWAYS prefer it over tributing Hieratic Seal.
                        var claws = cards.FirstOrDefault(c => c != null && c.IsFaceup() && c.IsCode(CardId.RedEyesMetalClawsDragon));
                        if (claws != null)
                        {
                            bool haveOtherSafeTribute = cards.Any(c => c != null && c.Controller == 0 && c.IsFaceup()
                                && !IsProtectedForTributeCost(c) && !c.IsCode(CardId.RedEyesMetalClawsDragon));

                            // If Claws is our only reasonable tribute (e.g., Seal is the other option), use Claws.
                            if (!haveOtherSafeTribute)
                            {
                                TrackMetallizeTribute(claws);
                                MMLog("[MM.Max-Release]", "tribute Claws (only safe tribute)");
                                return Util.CheckSelectCount(new List<ClientCard> { claws }, cards, min, max);
                            }

                            // Otherwise, follow the deck's safe-tribute preference (Claws is premium fuel for Max).
                            bool canBossConvert = HasMaxSummonCandidate(CardId.RedEyesBlackFullmetalDragon)
                                               || HasMaxSummonCandidate(CardId.MetalzoaX)
                                               || HasMaxSummonCandidate(CardId.MetalflameSwordsman);
                            if (canBossConvert)
                            {
                                TrackMetallizeTribute(claws);
                                MMLog("[MM.Max-Release]", "tribute Claws (priority)");
                                return Util.CheckSelectCount(new List<ClientCard> { claws }, cards, min, max);
                            }
                        }
                        // 1) Prefer Bystials (Magnamhut > Druiswurm)
                        var mag = cards.FirstOrDefault(c => c != null && c.IsFaceup() && c.IsCode(CardId.BystialMagnamhut));
                        if (mag != null) { TrackMetallizeTribute(mag); return Util.CheckSelectCount(new List<ClientCard> { mag }, cards, min, max); }
                        var dru = cards.FirstOrDefault(c => c != null && c.IsFaceup() && c.IsCode(CardId.BystialDruiswurm));
                        if (dru != null) { TrackMetallizeTribute(dru); return Util.CheckSelectCount(new List<ClientCard> { dru }, cards, min, max); }

                        // 2) Then Zoa, then Illusionist
                        var zoa2 = cards.FirstOrDefault(c => c != null && c.IsFaceup() && c.IsCode(CardId.ZoaTheFiendishBeast));
                        if (zoa2 != null) { TrackMetallizeTribute(zoa2); return Util.CheckSelectCount(new List<ClientCard> { zoa2 }, cards, min, max); }
                        var illu = cards.FirstOrDefault(c => c != null && c.IsFaceup() && c.IsCode(CardId.MetalIllusionist));
                        if (illu != null) { TrackMetallizeTribute(illu); return Util.CheckSelectCount(new List<ClientCard> { illu }, cards, min, max); }

                        // 3) Fallback: cheapest NON-protected own monster
                        var cheap = cards.Where(c => c != null && c.Controller == 0 && !IsProtectedForTributeCost(c) && !c.IsCode(CardId.RedEyesMetalClawsDragon))
                                         .OrderBy(ScoreOwnCardForCost)
                                         .FirstOrDefault();
                        if (cheap != null) { TrackMetallizeTribute(cheap); return Util.CheckSelectCount(new List<ClientCard> { cheap }, cards, min, max); }

                        // 4) Last-resort: anything that's not Fullmetal, if possible
                        var avoidFullmetal = cards.FirstOrDefault(c => c != null && c.Controller == 0 && !IsProtectedForTributeCost(c) && !c.IsCode(CardId.RedEyesMetalClawsDragon));
                        if (avoidFullmetal != null) { TrackMetallizeTribute(avoidFullmetal); return Util.CheckSelectCount(new List<ClientCard> { avoidFullmetal }, cards, min, max); }

                        var avoidClaws = cards.FirstOrDefault(c => c != null && c.Controller == 0 && !IsProtectedForTributeCost(c) && !c.IsCode(CardId.RedEyesMetalClawsDragon));
                        if (avoidClaws != null) { TrackMetallizeTribute(avoidClaws); return Util.CheckSelectCount(new List<ClientCard> { avoidClaws }, cards, min, max); }

                        TrackMetallizeTribute(cards[0]);
                        return Util.CheckSelectCount(new List<ClientCard> { cards[0] }, cards, min, max);
                    }
                    else
                    {
                        // Second pick = the monster to Special Summon: match Fiend/Dragon/Warrior rules
                        ClientCard pick = null;
                        if (_lastMetallizeLevel >= 5)
                        {
                            if ((_lastMetallizeRace & CardRace.Fiend) != 0)
                                pick = cards.FirstOrDefault(c => c != null && c.IsCode(CardId.MetalzoaX));
                            else if ((_lastMetallizeRace & CardRace.Dragon) != 0)
                                pick = cards.FirstOrDefault(c => c != null && c.IsCode(CardId.RedEyesBlackFullmetalDragon));
                            else if (_lastMetallizationWasWarrior5p)
                                pick = cards.FirstOrDefault(c => c != null && c.IsCode(CardId.MetalflameSwordsman));
                        }

                        bool haveFullmetal = Bot.HasInMonstersZone(CardId.RedEyesBlackFullmetalDragon, true);
                        bool haveMx = Bot.HasInMonstersZone(CardId.MetalzoaX, true);
                        bool haveSwordsman = Bot.HasInMonstersZone(CardId.MetalflameSwordsman, true);

                        var full = cards.FirstOrDefault(c => c != null && c.IsCode(CardId.RedEyesBlackFullmetalDragon));
                        var mx = cards.FirstOrDefault(c => c != null && c.IsCode(CardId.MetalzoaX));
                        var sword = cards.FirstOrDefault(c => c != null && c.IsCode(CardId.MetalflameSwordsman));

                        // Fallbacks: keep Fullmetal as primary Max line, but diversify if already established.
                        if (pick == null && full != null && !haveFullmetal) pick = full;
                        if (pick == null && mx != null && !haveMx) pick = mx;
                        if (pick == null && sword != null && !haveSwordsman) pick = sword;

                        pick = PickMetallizeFollowupAvoidingTribute(
                            pick,
                            (!haveFullmetal ? full : null),
                            (!haveMx ? mx : null),
                            (_lastMetallizationWasWarrior5p ? sword : null),
                            full,
                            mx,
                            sword,
                            cards.FirstOrDefault(c => c != null && !c.IsCode(CardId.RedEyesMetalClawsDragon)),
                            cards[0]
                        );
                        return Util.CheckSelectCount(new List<ClientCard> { pick }, cards, min, max);
                    }
                }

                // Flame Coating — cost + SS
                if (isSource(CardId.FlameCoatingMetalmorph))
                {
                    if (hint == HintMsg.Equip)
                    {
                        var pickEquip = PickMetallizeEquipTarget(cards);
                        return Util.CheckSelectCount(new List<ClientCard> { pickEquip }, cards, min, max);
                    }

                    if (hint == HintMsg.Release)
                    {
                        _lastMetallizationWasWarrior5p = false;
                        _lastMetallizeRace = 0;
                        _lastMetallizeLevel = 0;

                        // ⬇️ build the pool using the helper
                        var pool = cards.Where(c => c != null && c.Controller == 0 && c.IsFaceup() && MentionsMax(c)).ToList();

                        bool preferMx = Bot.HasInMonstersZone(CardId.ZoaTheFiendishBeast, true)
                                     && CheckRemainInDeck(CardId.MetalzoaX) > 0;
                        if (preferMx)
                        {
                            var zoa = pool.FirstOrDefault(c => c.IsCode(CardId.ZoaTheFiendishBeast));
                            if (zoa != null) { TrackMetallizeTribute(zoa); return Util.CheckSelectCount(new List<ClientCard> { zoa }, cards, min, max); }
                        }

                        // Existing priorities over the pool
                        bool underPressure = EnemyHighPressure();
                        var safePool = pool.Where(c => !c.IsCode(CardId.RedEyesBlackFullmetalDragon)
                                                    && !c.IsCode(CardId.MetalzoaX)
                                                    && !c.IsCode(CardId.MetalflameSwordsman))
                                          .ToList();
                        ClientCard pick =
                              safePool.FirstOrDefault(c => c.IsCode(CardId.RedEyesMetalClawsDragon))
                           ?? safePool.FirstOrDefault(c => c.IsCode(CardId.BystialDruiswurm))
                           ?? safePool.FirstOrDefault(c => c.IsCode(CardId.BystialMagnamhut))
                           ?? safePool.FirstOrDefault(c => c.IsCode(CardId.ZoaTheFiendishBeast))
                           ?? safePool.FirstOrDefault(c => c.IsCode(CardId.MetalIllusionist))
                           ?? safePool.FirstOrDefault(c => !IsCoreBranchBody(c) && !IsBoss(c))
                           ?? safePool.FirstOrDefault(c => !IsBoss(c))
                           ?? cards.Where(c => c != null && c.Controller == 0 && c.IsFaceup()
                                            && !c.IsCode(CardId.RedEyesBlackFullmetalDragon)
                                            && !c.IsCode(CardId.MetalzoaX)
                                            && !c.IsCode(CardId.MetalflameSwordsman)
                                            && !IsProtectedForTributeCost(c))
                                   .OrderBy(ScoreOwnCardForCost).FirstOrDefault()
                           ?? (underPressure ? pool.FirstOrDefault(c => c.IsCode(CardId.MetalflameSwordsman)) : null)
                           ?? cards.Where(c => c != null && c.Controller == 0 && c.IsFaceup() && !IsBoss(c))
                                   .OrderBy(ScoreOwnCardForCost).FirstOrDefault()
                           ?? pool.FirstOrDefault(c => c.IsCode(CardId.MetalflameSwordsman))
                           ?? pool.FirstOrDefault(c => c.IsCode(CardId.RedEyesBlackFullmetalDragon))
                           ?? pool.FirstOrDefault(c => c.IsCode(CardId.MetalzoaX))
                           ?? cards[0];

                        TrackMetallizeTribute(pick);
                        return Util.CheckSelectCount(new List<ClientCard> { pick }, cards, min, max);
                    }
                    else
                    {
                        // Second pick — what to SS/equip
                        ClientCard pick = null;
                        bool haveSwordsman = Bot.HasInMonstersZone(CardId.MetalflameSwordsman, true);
                        bool haveFullmetal = Bot.HasInMonstersZone(CardId.RedEyesBlackFullmetalDragon, true);
                        bool haveMx = Bot.HasInMonstersZone(CardId.MetalzoaX, true);
                        bool haveBoss = haveFullmetal || haveMx;
                        bool lastWasDragonBody = _lastMetallizeLevel >= 5 && ((_lastMetallizeRace & CardRace.Dragon) != 0);
                        bool lastWasFiendBody = _lastMetallizeLevel >= 5 && ((_lastMetallizeRace & CardRace.Fiend) != 0);
                        bool underPressure = EnemyHighPressure();
                        bool haveMaxCoreOnline = HasSet(CardId.MaxMetalmorph) || Bot.HasInHand(CardId.MaxMetalmorph) || haveFullmetal || haveMx;

                        var sword = cards.FirstOrDefault(c => c != null && c.IsCode(CardId.MetalflameSwordsman));
                        var full = cards.FirstOrDefault(c => c != null && c.IsCode(CardId.RedEyesBlackFullmetalDragon));
                        var mx = cards.FirstOrDefault(c => c != null && c.IsCode(CardId.MetalzoaX));

                        // PURPOSE: Flame is our preferred Swordsman route; diversify before duplicating bosses.
                        bool pushSwordsman = !haveSwordsman && !haveBoss && sword != null && (haveMaxCoreOnline || !underPressure);

                        pick = PickMetallizeFollowupAvoidingTribute(
                            (pushSwordsman ? sword : null),
                            (lastWasFiendBody ? mx : null),
                            (lastWasDragonBody ? full : null),
                            (!haveSwordsman ? sword : null),
                            (!haveFullmetal ? full : null),
                            (!haveMx ? mx : null),
                            sword,
                            full,
                            mx,
                            cards.FirstOrDefault(c => c != null && !c.IsCode(CardId.RedEyesMetalClawsDragon)),
                            cards[0]
                        );
                        return Util.CheckSelectCount(new List<ClientCard> { pick }, cards, min, max);
                    }
                }

                // Metalzoa X — hand (reveal): pick which Metalmorph to take
                if (isSource(CardId.MetalzoaX) && sourceLocation != CardLocation.MonsterZone)
                {
                    var pick3 = PickMetalmorphTutorCard(cards);
                    return Util.CheckSelectCount(new List<ClientCard> { pick3 }, cards, min, max);
                }

                // Metalzoa X — on field: pick best enemy face-up to pop
                if (isSource(CardId.MetalzoaX) && sourceLocation == CardLocation.MonsterZone)
                {
                    var target = PickBestOpponentFaceup(cards);
                    return Util.CheckSelectCount(new List<ClientCard> { target }, cards, min, max);
                }

                // Fullmetal: burn target = highest original ATK attack-position enemy monster.
                if (isSource(CardId.RedEyesBlackFullmetalDragon))
                {
                    bool canBurn = sourceLocation == CardLocation.MonsterZone;
                    bool hasBurnCandidates = cards.Any(c => c != null && c.Controller == 1 && c.IsMonster() && c.IsAttack());
                    if (canBurn && hasBurnCandidates && (hint == HintMsg.Target || hint == HintMsg.OperateCard || hint == 505))
                    {
                        var burn = PickFullmetalBurnTarget(cards);
                        MMLog("[MM.Fullmetal.Burn]", "target id=" + burn.Id + " atk=" + FullmetalBurnAtk(burn));
                        return Util.CheckSelectCount(new List<ClientCard> { burn }, cards, min, max);
                    }

                    // Fullmetal set effect: choose which Metalmorph to set — Max > Time > Flame.
                    if (hint == HintMsg.AddToHand || hint == HintMsg.OperateCard || hint == HintMsg.Set || hint == 505)
                    {
                        var trapOptions = cards.Where(c => c != null &&
                            (c.IsCode(CardId.MaxMetalmorph) || c.IsCode(CardId.FlameCoatingMetalmorph) || c.IsCode(CardId.TimeEngine))).ToList();

                        var pick = PreferFullmetalTrapToSet(trapOptions)
                                ?? cards.FirstOrDefault(c => c != null && (c.IsCode(CardId.MaxMetalmorph) || c.IsCode(CardId.FlameCoatingMetalmorph) || c.IsCode(CardId.TimeEngine)))
                                ?? cards[0];
                        MMLog("[MM.Fullmetal.SetPick]", "pick=" + pick.Id);
                        return Util.CheckSelectCount(new List<ClientCard> { pick }, cards, min, max);
                    }
                }

                // Black Metal Dragon (GY trigger): REDMD -> Fullmetal -> Claws (missing-piece aware).
                if (isSource(CardId.BlackMetalDragon))
                {
                    if (sourceLocation == CardLocation.Grave &&
                        (hint == HintMsg.AddToHand || hint == HintMsg.OperateCard || hint == 505))
                    {
                        bool forceRedmd = ShouldForceRedmdFromBmdSearch(cards);
                        ClientCard pick5 = PickBmdGraveSearchTarget(cards, forceRedmd) ?? cards[0];

                        return Util.CheckSelectCount(new List<ClientCard> { pick5 }, cards, min, max);
                    }

                    if (sourceLocation != CardLocation.Grave &&
                        (hint == HintMsg.Target || hint == HintMsg.Equip || hint == HintMsg.OperateCard || hint == 505))
                    {
                        bool canEquipToRedmd = HasSecondBmdBodyForRedmdEquip()
                                            && Bot.HasInMonstersZone(CardId.RedEyesDarknessMetalDragon, true);
                        var pickEquip =
                              (canEquipToRedmd ? cards.FirstOrDefault(c => c != null && c.IsCode(CardId.RedEyesDarknessMetalDragon)) : null)
                           ?? cards.FirstOrDefault(c => c != null && c.Controller == 0 && c.IsFaceup() && !IsBoss(c) && !c.IsCode(CardId.RedEyesDarknessMetalDragon))
                           ?? cards.FirstOrDefault(c => c != null && c.Controller == 0 && c.IsFaceup() && !c.IsCode(CardId.RedEyesDarknessMetalDragon))
                           ?? cards.FirstOrDefault(c => c != null && !c.IsCode(CardId.RedEyesDarknessMetalDragon))
                           ?? cards[0];
                        return Util.CheckSelectCount(new List<ClientCard> { pickEquip }, cards, min, max);
                    }
                }

                // Bystials (hand summon target + Magnamhut end-phase search + Druiswurm GY send)
                if (isSource(CardId.BystialMagnamhut) || isSource(CardId.BystialDruiswurm))
                {
                    if (sourceLocation == CardLocation.Hand &&
                        (hint == HintMsg.Remove || hint == HintMsg.Target || hint == HintMsg.OperateCard || hint == 505))
                    {
                        var pickBanish =
                              cards.FirstOrDefault(c => c != null && c.Controller == 1 && c.IsMonster() && (c.HasAttribute(CardAttribute.Light) || c.HasAttribute(CardAttribute.Dark)))
                           ?? cards.FirstOrDefault(c => c != null && c.Controller == 0 && c.IsMonster() && (c.HasAttribute(CardAttribute.Light) || c.HasAttribute(CardAttribute.Dark)) && !IsProtectedOwnBystialBanishCost(c) && !c.IsCode(CardId.TheBystialLubellion))
                           ?? cards.FirstOrDefault(c => c != null && c.Controller == 0 && c.IsMonster() && (c.HasAttribute(CardAttribute.Light) || c.HasAttribute(CardAttribute.Dark)) && !c.IsCode(CardId.TheBystialLubellion))
                           ?? cards.FirstOrDefault(c => c != null && c.Controller == 0 && c.IsMonster() && (c.HasAttribute(CardAttribute.Light) || c.HasAttribute(CardAttribute.Dark)) && !IsProtectedOwnBystialBanishCost(c))
                           ?? cards[0];
                        return Util.CheckSelectCount(new List<ClientCard> { pickBanish }, cards, min, max);
                    }

                    if (isSource(CardId.BystialDruiswurm) &&
                        sourceLocation == CardLocation.Grave &&
                        (hint == HintMsg.Target || hint == HintMsg.OperateCard || hint == 505))
                    {
                        var pickSend =
                              cards.FirstOrDefault(c => c != null && c.Controller == 1 && c.IsFaceup() && c.IsMonster() && c.IsSpecialSummoned && !c.IsShouldNotBeTarget())
                           ?? cards.FirstOrDefault(c => c != null && c.Controller == 1 && c.IsFaceup() && c.IsMonster() && !c.IsShouldNotBeTarget())
                           ?? cards[0];
                        return Util.CheckSelectCount(new List<ClientCard> { pickSend }, cards, min, max);
                    }
                }

                if (isSource(CardId.TheBystialLubellion))
                {
                    if (sourceLocation == CardLocation.Hand &&
                        (hint == HintMsg.ToGrave || hint == HintMsg.OperateCard || hint == 505))
                    {
                        var pickDump =
                              cards.FirstOrDefault(c => c != null && c.IsCode(CardId.BystialDruiswurm) && !Bot.HasInHand(CardId.BystialDruiswurm))
                           ?? cards.FirstOrDefault(c => c != null && c.IsCode(CardId.BystialMagnamhut) && !Bot.HasInHand(CardId.BystialMagnamhut))
                           ?? cards.FirstOrDefault(c => c != null && c.IsCode(CardId.BystialDruiswurm))
                           ?? cards.FirstOrDefault(c => c != null && c.IsCode(CardId.BystialMagnamhut))
                           ?? cards[0];
                        return Util.CheckSelectCount(new List<ClientCard> { pickDump }, cards, min, max);
                    }

                    if (sourceLocation == CardLocation.Grave &&
                        (hint == HintMsg.Release || hint == HintMsg.ToGrave || hint == HintMsg.OperateCard || hint == 505))
                    {
                        var darkDragon6 =
                            cards.Where(c => c != null && c.Controller == 0 && c.IsFaceup() &&
                                             c.HasAttribute(CardAttribute.Dark) && c.HasRace(CardRace.Dragon) &&
                                             c.Level >= 6 && !c.IsCode(CardId.TheBystialLubellion))
                                 .OrderBy(ScoreOwnCardForCost).FirstOrDefault();

                        var fallback = cards.Where(c => c != null && c.Controller == 0 && c.IsFaceup() && !IsProtectedForTributeCost(c))
                                            .OrderBy(ScoreOwnCardForCost).FirstOrDefault();
                        var pickTrib = darkDragon6 ?? fallback ?? cards[0];
                        _tributedThisTurn = true;
                        return Util.CheckSelectCount(new List<ClientCard> { pickTrib }, cards, min, max);
                    }

                    if (sourceLocation == CardLocation.MonsterZone &&
                        (hint == HintMsg.OperateCard || hint == HintMsg.AddToHand || hint == 505))
                    {
                        var pickBranded =
                              cards.FirstOrDefault(c => c != null && c.IsCode(CardId.BrandedRegained))
                           ?? cards[0];
                        return Util.CheckSelectCount(new List<ClientCard> { pickBranded }, cards, min, max);
                    }
                }

                if (isSource(CardId.BrandedRegained) &&
                    (hint == HintMsg.Target || hint == HintMsg.OperateCard || hint == 505))
                {
                    var pickRegained =
                          cards.FirstOrDefault(c => c != null && c.Controller == 0 && c.Location == CardLocation.Grave && c.IsCode(CardId.BystialDruiswurm))
                       ?? cards.FirstOrDefault(c => c != null && c.Controller == 0 && c.Location == CardLocation.Grave && c.IsCode(CardId.BystialMagnamhut))
                       ?? cards.FirstOrDefault(c => c != null && c.Controller == 0 && c.Location == CardLocation.Grave && c.IsCode(CardId.TheBystialLubellion))
                       ?? cards.FirstOrDefault(c => c != null && c.Controller == 0 && c.IsFaceup() && c.IsCode(CardId.BystialDruiswurm))
                       ?? cards.FirstOrDefault(c => c != null && c.Controller == 0 && c.IsFaceup() && c.IsCode(CardId.BystialMagnamhut))
                       ?? cards.FirstOrDefault(c => c != null && c.Controller == 0 && c.IsFaceup() && c.IsCode(CardId.TheBystialLubellion))
                       ?? cards.FirstOrDefault(c => c != null && c.Controller == 0 && c.IsFaceup() && c.IsMonster() && (c.HasAttribute(CardAttribute.Light) || c.HasAttribute(CardAttribute.Dark)))
                       ?? cards.FirstOrDefault(c => c != null && c.IsFaceup() && c.IsMonster() && (c.HasAttribute(CardAttribute.Light) || c.HasAttribute(CardAttribute.Dark)))
                       ?? cards[0];
                    return Util.CheckSelectCount(new List<ClientCard> { pickRegained }, cards, min, max);
                }

                // REDMD revive priority
                if (isSource(CardId.RedEyesDarknessMetalDragon))
                {
                    var bmd = cards.FirstOrDefault(c => c != null && c.IsCode(CardId.BlackMetalDragon));
                    if (bmd != null) return Util.CheckSelectCount(new List<ClientCard> { bmd }, cards, min, max);

                    var illu2 = cards.FirstOrDefault(c => c != null && c.IsCode(CardId.MetalIllusionist));
                    if (illu2 != null) return Util.CheckSelectCount(new List<ClientCard> { illu2 }, cards, min, max);

                    var claws2 = cards.FirstOrDefault(c => c != null && c.IsCode(CardId.RedEyesMetalClawsDragon));
                    if (claws2 != null) return Util.CheckSelectCount(new List<ClientCard> { claws2 }, cards, min, max);

                    var bestDragon = cards.Where(c => c != null && c.HasRace(CardRace.Dragon) && !c.IsCode(CardId.RedEyesDarknessMetalDragon))
                                          .OrderByDescending(ScoreRedmdReviveExtender)
                                          .FirstOrDefault() ?? cards[0];
                    return Util.CheckSelectCount(new List<ClientCard> { bestDragon }, cards, min, max);
                }

                // Striker Dragon — ensure BMD is used as material
                if (isSource(CardId.StrikerDragon) && hint == HintMsg.LinkMaterial)
                {
                    var bmd2 = cards.FirstOrDefault(c => c != null && c.IsCode(CardId.BlackMetalDragon));
                    if (bmd2 != null) return Util.CheckSelectCount(new List<ClientCard> { bmd2 }, cards, min, max);
                }

                // Incoming Machine! — choose what to add (Deck/GY)
                if (isSource(CardId.IncomingMachine))
                {
                    // Priorities:
                    // A) If we don't have a Max facedown and any copy exists → pick Max Metalmorph first.
                    ClientCard pick =
                          cards.FirstOrDefault(c => c != null && c.IsCode(CardId.MaxMetalmorph) && !HasSet(CardId.MaxMetalmorph))
                       // B) If Max already set or not offered: take Flame only when we actively need removal/DR (opp has 2+ face-ups or backrow).
                       ?? ((Enemy.GetMonsters().Count(m => m != null && m.IsFaceup()) >= 2
                            || Enemy.GetSpells().Any(s => s != null))
                           ? cards.FirstOrDefault(c => c != null && c.IsCode(CardId.FlameCoatingMetalmorph)) : null)
                       // C) Otherwise Time Engine as utility
                       ?? cards.FirstOrDefault(c => c != null && c.IsCode(CardId.TimeEngine))
                       // D) If no trap fits, grab a *monster that mentions Max* to keep lines live:
                       ?? cards.FirstOrDefault(c => c != null && (MentionsMax(c) || c.HasRace(CardRace.Dragon)))
                       // E) Fallback
                       ?? cards[0];

                    return Util.CheckSelectCount(new List<ClientCard> { pick }, cards, min, max);
                }

                // Claws — cost & add
                if (isSource(CardId.RedEyesMetalClawsDragon))
                {
                    if (hint == HintMsg.ToGrave)
                    {
                        var safeFd = cards.FirstOrDefault(c => c != null && c.IsFacedown() && !c.IsMonster() && !IsMetalmorphTrapId(c.Id));
                        var spareMetalmorph = cards.FirstOrDefault(c => c != null && c.IsFacedown() && !c.IsMonster()
                                                                      && IsMetalmorphTrapId(c.Id) && CanSpareFacedownMetalmorphForClaws(c));
                        var loneMax = cards.FirstOrDefault(c => c != null && c.IsFacedown() && !c.IsMonster()
                                                                    && c.IsCode(CardId.MaxMetalmorph) && ShouldUseLoneMetalmorphAsClawsCostNow(c));
                        var sendCost = safeFd ?? spareMetalmorph ?? loneMax;
                        if (sendCost != null && loneMax != null && sendCost == loneMax)
                            MMLog("[MM.Claws.Cost]", "send lone Max for bridge");
                        if (sendCost == null)
                        {
                            // No acceptable cost candidate.
                            return base.OnSelectCard(cards, min, max, hint, cancelable);
                        }
                        return Util.CheckSelectCount(new List<ClientCard> { sendCost }, cards, min, max);
                    }

                    // add: support ADD/OperateCard
                    if (hint == HintMsg.AddToHand || hint == HintMsg.OperateCard || hint == 505)
                    {
                        var maxGY = cards.FirstOrDefault(c => c != null && c.IsCode(CardId.MaxMetalmorph) && c.Location == CardLocation.Grave);
                        var maxDeck = cards.FirstOrDefault(c => c != null && c.IsCode(CardId.MaxMetalmorph));
                        var flame = cards.FirstOrDefault(c => c != null && c.IsCode(CardId.FlameCoatingMetalmorph));
                        var time = cards.FirstOrDefault(c => c != null && c.IsCode(CardId.TimeEngine));

                        bool haveMax = HasSet(CardId.MaxMetalmorph) || Bot.HasInHand(CardId.MaxMetalmorph);
                        bool haveFlame = HasSet(CardId.FlameCoatingMetalmorph) || Bot.HasInHand(CardId.FlameCoatingMetalmorph);
                        bool haveTime = HasSet(CardId.TimeEngine) || Bot.HasInHand(CardId.TimeEngine);

                        ClientCard pick6 =
                              (!haveMax ? (maxGY ?? maxDeck) : null)
                           ?? (!haveTime ? time : null)
                           ?? (!haveFlame ? flame : null)
                           ?? maxGY
                           ?? maxDeck
                           ?? time
                           ?? flame
                           ?? cards[0];

                        return Util.CheckSelectCount(new List<ClientCard> { pick6 }, cards, min, max);
                    }
                }

                // Called by / Crossout / Imperm targets
                if (isSource(CardId.CalledByTheGrave))
                {
                    // First choice: the exact name matching the last chain card from the OPPONENT’s GY
                    var last = Util.GetLastChainCard();
                    int originId = (last != null && last.Data != null && last.Data.Alias > 0) ? last.Data.Alias : (last?.Id ?? 0);

                    var exactOpp = cards.FirstOrDefault(c => c != null
                        && c.Controller == 1
                        && c.Location == CardLocation.Grave
                        && c.IsMonster()
                        && c.Id == originId);

                    if (exactOpp != null)
                        return Util.CheckSelectCount(new List<ClientCard> { exactOpp }, cards, min, max);

                    // Fallback: any opponent GY monster (never ours)
                    var anyOppGY = cards.FirstOrDefault(c => c != null
                        && c.Controller == 1
                        && c.Location == CardLocation.Grave
                        && c.IsMonster());

                    if (anyOppGY != null)
                        return Util.CheckSelectCount(new List<ClientCard> { anyOppGY }, cards, min, max);

                    // Absolute fallback: do nothing (avoid targeting our own grave)
                    return base.OnSelectCard(cards, min, max, hint, cancelable);
                }

                if (isSource(CardId.InfiniteImpermanence))
                {
                    var negateTargets = GetMonsterListForTargetNegate(true, CardType.Trap);
                    foreach (var pick in negateTargets)
                    {
                        if (pick != null && cards.Contains(pick))
                            return Util.CheckSelectCount(new List<ClientCard> { pick }, cards, min, max);
                    }

                    var ranked = GetNormalEnemyTargetList(true, true, CardType.Trap, true);
                    foreach (var pick in ranked)
                    {
                        if (pick != null && pick.Controller == 1 && pick.IsFaceup() && pick.IsMonster() && cards.Contains(pick))
                            return Util.CheckSelectCount(new List<ClientCard> { pick }, cards, min, max);
                    }

                    // Score: floodgates > omni-negates > highest ATK
                    Func<ClientCard, int> score = c =>
                    {
                        int s = 0;
                        if (c.IsFloodgate()) s += 10000;
                        if (c.IsMonsterInvincible() || c.IsMonsterDangerous()) s += 4000;
                        s += Math.Max(0, c.Attack);
                        return s;
                    };

                    var target = cards.Where(c => c != null && c.Controller == 1 && c.IsFaceup() && c.IsMonster())
                                      .OrderByDescending(score).FirstOrDefault()
                                 ?? PickBestOpponentFaceup(cards);
                    return Util.CheckSelectCount(new List<ClientCard> { target }, cards, min, max);
                }

                if (isSource(CardId.FidraulisHarmonia))
                {
                    if (hint == HintMsg.Target || hint == HintMsg.Destroy)
                    {
                        var destroy = PickBestEnemyTargetOnly(cards) ?? PickBestOpponentFaceup(cards) ?? cards[0];
                        _pendingFidraulisDestroyPrompt = false;
                        MMLog("[MM.Fidraulis.Destroy]", "pick=" + destroy.Id);
                        return Util.CheckSelectCount(new List<ClientCard> { destroy }, cards, min, max);
                    }

                    if (LooksLikeFidraulisRevealPrompt(cards, max, hint))
                    {
                        if (_pendingFidraulisRevealDesiredCount > 1 && max <= 1)
                        {
                            var nextReveal = PickNextFidraulisRevealCard(cards);
                            if (nextReveal != null)
                            {
                                AdvanceFidraulisRevealState(new List<ClientCard> { nextReveal });
                                MMLog("[MM.Fidraulis.RevealStep]", "pick=" + nextReveal.Id + " step=" + _pendingFidraulisRevealPickedIds.Count + "/" + _pendingFidraulisRevealDesiredCount);
                                return Util.CheckSelectCount(new List<ClientCard> { nextReveal }, cards, min, max);
                            }
                        }
                        else
                        {
                            var reveal = PickFidraulisRevealCards(cards, min, max);
                            if (reveal != null && reveal.Count > 0)
                            {
                                AdvanceFidraulisRevealState(reveal);
                                MMLog("[MM.Fidraulis.Reveal]", "pick=" + string.Join(",", reveal.Select(c => c.Id.ToString()).ToArray()));
                                return Util.CheckSelectCount(reveal.ToList(), cards, min, max);
                            }
                        }
                    }

                    if (hint == HintMsg.ToGrave || hint == HintMsg.OperateCard || hint == 505)
                    {
                        var send = PickFidraulisSendToGrave(cards) ?? cards[0];
                        _pendingFidraulisSendPrompt = false;
                        MMLog("[MM.Fidraulis.Send]", "pick=" + send.Id);
                        return Util.CheckSelectCount(new List<ClientCard> { send }, cards, min, max);
                    }
                }

                if (isSource(CardId.HopEarSquadron))
                {
                    if (hint == HintMsg.Faceup || hint == HintMsg.Target || hint == HintMsg.OperateCard || hint == 505)
                    {
                        var pickHopTarget = GetPreferredHopEarTarget(cards)
                           ?? cards.FirstOrDefault(c => c != null && c.Controller == 0 && c.IsFaceup() && CanHopEarConvert(c))
                           ?? cards.FirstOrDefault(c => c != null && c.Controller == 0 && c.IsFaceup() && !IsHopEarBossBody(c))
                           ?? cards[0];

                        _pendingHopEarTargetId = pickHopTarget?.Id ?? 0;
                        _pendingHopEarTargetLevel = pickHopTarget?.Level ?? 0;
                        _pendingHopEarPreferBaronne = pickHopTarget != null && CanHopEarRouteToBaronneWith(pickHopTarget);
                        _pendingHopEarPreferAccel = pickHopTarget != null && CanHopEarRouteToAccelWith(pickHopTarget);

                        MMLog("[MM.HopEar.Target]", "pick=" + _pendingHopEarTargetId + " lv=" + _pendingHopEarTargetLevel
                            + " accel=" + (_pendingHopEarPreferAccel ? "Y" : "N")
                            + " baronne=" + (_pendingHopEarPreferBaronne ? "Y" : "N"));
                        return Util.CheckSelectCount(new List<ClientCard> { pickHopTarget }, cards, min, max);
                    }

                    if (hint == HintMsg.SpSummon)
                    {
                        bool preferSatellite = ShouldPreferSatelliteOverBaronne(cards);
                        bool savingSwordsmanLine = Bot.HasInMonstersZone(CardId.MetalflameSwordsman, true) && CanReactiveBystialBecomeHopEarBody();
                        var satellite = cards.FirstOrDefault(c => c != null && c.IsCode(CardId.SatelliteWarrior));
                        var baronne = cards.FirstOrDefault(c => c != null && c.IsCode(CardId.BaronneDeFleur));
                        var syncPick =
                              (_pendingHopEarPreferBaronne ? baronne : null)
                           ?? (_pendingHopEarPreferAccel ? cards.FirstOrDefault(c => c != null && c.IsCode(CardId.AccelSynchroStardustDragon)) : null)
                           ?? (savingSwordsmanLine ? cards.FirstOrDefault(c => c != null && c.IsCode(CardId.AccelSynchroStardustDragon)) : null)
                           ?? (preferSatellite ? satellite : baronne)
                           ?? satellite
                           ?? baronne
                           ?? cards.FirstOrDefault(c => c != null && c.IsCode(CardId.AccelSynchroStardustDragon))
                           ?? cards[0];
                        MMLog("[MM.HopEar.Synchro]", "target=" + _pendingHopEarTargetId + " pick=" + syncPick.Id);
                        return Util.CheckSelectCount(new List<ClientCard> { syncPick }, cards, min, max);
                    }
                }

                if (isSource(CardId.AccelSynchroStardustDragon))
                {
                    var accelPick = SelectAccelSynchroPrompt(cards, min, max, hint);
                    if (accelPick != null) return accelPick;
                }

                if (isSource(CardId.StardustDragonVictimSanctuary) &&
                    LooksLikeVictimSanctuarySummonPrompt(cards, hint, min, max))
                {
                    var stardust = PickVictimSanctuarySummon(cards) ?? cards[0];
                    _pendingVictimSanctuarySummonPrompt = false;
                    MMLog("[MM.VictimSanctuary.Select]", "pick=" + stardust.Id);
                    return Util.CheckSelectCount(new List<ClientCard> { stardust }, cards, min, max);
                }

                if (isSource(CardId.SatelliteWarrior))
                {
                    if (sourceLocation == CardLocation.MonsterZone &&
                        (hint == HintMsg.Destroy || hint == HintMsg.Target || hint == HintMsg.OperateCard || hint == 505))
                    {
                        var pool = cards.Where(c => c != null && c.Controller == 1 && !c.IsShouldNotBeTarget())
                                        .OrderByDescending(ScoreEnemyCardForRemoval).ToList();
                        if (pool.Count == 0) pool = cards.Where(c => c != null).ToList();
                        var picked = pool.Take(Math.Max(1, max)).ToList();
                        if (picked.Count < min) picked = pool.Take(min).ToList();
                        MMLog("[MM.Satellite.Select]", "pick=" + picked.Count + " min=" + min + " max=" + max);
                        return Util.CheckSelectCount(picked, cards, min, max);
                    }

                    if (sourceLocation == CardLocation.Grave &&
                        (hint == HintMsg.SpSummon || hint == HintMsg.OperateCard || hint == 505))
                    {
                        var revivePool = GetSatelliteRevivePool(cards);
                        if (revivePool.Count > 0)
                        {
                            int take = Math.Min(max, Math.Max(min, revivePool.Count));
                            return Util.CheckSelectCount(revivePool.Take(take).ToList(), cards, min, max);
                        }
                    }
                }

                if (isSource(CardId.BaronneDeFleur) &&
                    ((hint == HintMsg.Target || hint == HintMsg.OperateCard || hint == 505)
                     || (max == 1 && cards.Any(c => c != null && (c.Location == CardLocation.MonsterZone || c.Location == CardLocation.SpellZone)))))
                {
                    var baronneTarget = PickBestEnemyTargetOnly(cards);
                    if (baronneTarget == null)
                    {
                        var ownFallback = cards.Where(c => c != null && !c.IsCode(CardId.BaronneDeFleur))
                                               .OrderBy(ScoreOwnCardForCost).FirstOrDefault();
                        if (ownFallback == null) return base.OnSelectCard(cards, min, max, hint, cancelable);
                        return Util.CheckSelectCount(new List<ClientCard> { ownFallback }, cards, min, max);
                    }
                    return Util.CheckSelectCount(new List<ClientCard> { baronneTarget }, cards, min, max);
                }

                if (isSource(CardId.StardustWarrior) &&
                    sourceLocation == CardLocation.Grave &&
                    (hint == HintMsg.SpSummon || hint == HintMsg.OperateCard || hint == 505))
                {
                    var pickWarrior =
                          cards.FirstOrDefault(c => c != null && c.IsCode(CardId.JunkWarriorExtreme))
                       ?? cards.FirstOrDefault(c => c != null && c.IsCode(CardId.StardustDragon))
                       ?? cards[0];
                    MMLog("[MM.StardustWarrior.Float]", "pick=" + pickWarrior.Id);
                    return Util.CheckSelectCount(new List<ClientCard> { pickWarrior }, cards, min, max);
                }

                if (isSource(CardId.JunkWarriorExtreme) &&
                    sourceLocation == CardLocation.MonsterZone &&
                    (hint == HintMsg.SpSummon || hint == HintMsg.OperateCard || hint == 505))
                {
                    var revivePool = GetJunkWarriorExtremeRevivePool(cards);
                    if (revivePool.Count > 0)
                    {
                        int take = Math.Min(max, Math.Max(min, revivePool.Count));
                        var picked = revivePool.Take(take).ToList();
                        MMLog("[MM.JunkExtreme.Select]", "pick=" + string.Join(",", picked.Select(c => c.Id.ToString()).ToArray()));
                        return Util.CheckSelectCount(picked, cards, min, max);
                    }
                }

                if (isSource(CardId.SPLittleKnight) &&
                    (hint == HintMsg.Target || hint == HintMsg.OperateCard || hint == 505))
                {
                    var self = cards.FirstOrDefault(c => c != null && c.Controller == 0 && c.IsCode(CardId.SPLittleKnight)) ?? sourceCard;
                    var enemy =
                          cards.FirstOrDefault(c => c != null && c.Controller == 1 && c.IsFaceup() && c.IsMonster() && !c.IsShouldNotBeTarget())
                       ?? cards.FirstOrDefault(c => c != null && c.Controller == 1 && c.IsFaceup() && !c.IsShouldNotBeTarget())
                       ?? cards.FirstOrDefault(c => c != null && c.Controller == 1 && !c.IsShouldNotBeTarget())
                       ?? cards[0];
                    if (max >= 2)
                        return Util.CheckSelectCount(new List<ClientCard> { self, enemy }, cards, min, max);
                    return Util.CheckSelectCount(new List<ClientCard> { enemy }, cards, min, max);
                }

                // REDMD hand-SS cost: banish Striker first, else lowest-value Dragon
                if (isSource(CardId.RedEyesDarknessMetalDragon) && (hint == HintMsg.Remove || hint == HintMsg.Release))
                {
                    var choices = cards.Where(c => c != null && c.IsFaceup() && c.HasRace(CardRace.Dragon)).ToList();
                    var striker = choices.FirstOrDefault(c => c.IsCode(CardId.StrikerDragon));
                    if (striker != null) return Util.CheckSelectCount(new List<ClientCard> { striker }, cards, min, max);

                    var sack = choices.OrderBy(ScoreOwnCardForCost).FirstOrDefault() ?? choices.FirstOrDefault();
                    if (sack != null) return Util.CheckSelectCount(new List<ClientCard> { sack }, cards, min, max);
                }

                // =========================
                //  HIERATIC SEAL SELECTORS
                // =========================
                if (isSource(CardId.HieraticSealOfTheHeavenlySpheres) && hint == HintMsg.Release)
                {
                    var self = cards.FirstOrDefault(c => c != null && c.IsCode(CardId.HieraticSealOfTheHeavenlySpheres));
                    if (self != null)
                    {
                        _tributedThisTurn = true;
                        return Util.CheckSelectCount(new List<ClientCard> { self }, cards, min, max);
                    }

                    var mag = cards.FirstOrDefault(c => c != null && c.IsFaceup() && c.IsCode(CardId.BystialMagnamhut));
                    if (mag != null)
                    {
                        _tributedThisTurn = true;
                        return Util.CheckSelectCount(new List<ClientCard> { mag }, cards, min, max);
                    }
                    var dru = cards.FirstOrDefault(c => c != null && c.IsFaceup() && c.IsCode(CardId.BystialDruiswurm));
                    if (dru != null)
                    {
                        _tributedThisTurn = true;
                        return Util.CheckSelectCount(new List<ClientCard> { dru }, cards, min, max);
                    }

                    var illu = cards.FirstOrDefault(c => c != null && c.IsFaceup() && c.IsCode(CardId.MetalIllusionist));
                    if (illu != null)
                    {
                        _tributedThisTurn = true;
                        return Util.CheckSelectCount(new List<ClientCard> { illu }, cards, min, max);
                    }
                    var zoa = cards.FirstOrDefault(c => c != null && c.IsFaceup() && c.IsCode(CardId.ZoaTheFiendishBeast));
                    if (zoa != null)
                    {
                        _tributedThisTurn = true;
                        return Util.CheckSelectCount(new List<ClientCard> { zoa }, cards, min, max);
                    }

                    var safe = cards.FirstOrDefault(c => c != null &&
                                                         !c.IsCode(CardId.RedEyesBlackFullmetalDragon) &&
                                                         !c.IsCode(CardId.MetalzoaX));
                    if (safe != null)
                    {
                        _tributedThisTurn = true;
                        return Util.CheckSelectCount(new List<ClientCard> { safe }, cards, min, max);
                    }

                    _tributedThisTurn = true;
                    return Util.CheckSelectCount(new List<ClientCard> { cards[0] }, cards, min, max);
                }

                // Seal float target — prefer Bystials for the Lubellion/Regained route
                if (isSource(CardId.HieraticSealOfTheHeavenlySpheres) &&
                   (hint == HintMsg.SpSummon || ((hint == HintMsg.OperateCard || hint == 505) && LooksLikeSealFloatPrompt(cards))))
                {
                    // 0) Magnamhut > Druiswurm
                    var mag = cards.FirstOrDefault(c => c != null && c.IsCode(CardId.BystialMagnamhut));
                    if (mag != null) return Util.CheckSelectCount(new List<ClientCard> { mag }, cards, min, max);
                    var dru = cards.FirstOrDefault(c => c != null && c.IsCode(CardId.BystialDruiswurm));
                    if (dru != null) return Util.CheckSelectCount(new List<ClientCard> { dru }, cards, min, max);

                    // 1) Then Claws (trap recursion follow-up)
                    var claws = cards.FirstOrDefault(c => c != null && c.IsCode(CardId.RedEyesMetalClawsDragon));
                    if (claws != null) return Util.CheckSelectCount(new List<ClientCard> { claws }, cards, min, max);

                    // 2) Then Illusionist (keeps Max lines live next turn)
                    var illu = cards.FirstOrDefault(c => c != null && c.IsCode(CardId.MetalIllusionist));
                    if (illu != null) return Util.CheckSelectCount(new List<ClientCard> { illu }, cards, min, max);

                    // 3) Fallback: any Dragon (avoid BMD)
                    var anyDragonNotBMD = cards.FirstOrDefault(c => c != null && c.HasRace(CardRace.Dragon) && !c.IsCode(CardId.BlackMetalDragon));
                    if (anyDragonNotBMD != null) return Util.CheckSelectCount(new List<ClientCard> { anyDragonNotBMD }, cards, min, max);

                    return Util.CheckSelectCount(new List<ClientCard> { cards[0] }, cards, min, max);
                }

                // Seal bounce target
                if (isSource(CardId.HieraticSealOfTheHeavenlySpheres) &&
                    (hint == HintMsg.OperateCard || hint == 505 || hint == HintMsg.Target))
                {
                    var target = PickSealBounceTarget(cards);
                    return Util.CheckSelectCount(new List<ClientCard> { target }, cards, min, max);
                }

                if (isSource(CardId.DharcTheDarkCharmerGloomy))
                {
                    if (sourceLocation == CardLocation.MonsterZone &&
                        (hint == HintMsg.SpSummon || hint == HintMsg.Target || hint == HintMsg.OperateCard || hint == 505))
                    {
                        var steal = PickDharcStealTarget(cards) ?? cards[0];
                        MMLog("[MM.Dharc.Target]", "pick=" + steal.Id + " lv=" + steal.Level);
                        return Util.CheckSelectCount(new List<ClientCard> { steal }, cards, min, max);
                    }

                    if (sourceLocation == CardLocation.Grave &&
                        (hint == HintMsg.AddToHand || hint == HintMsg.OperateCard || hint == 505))
                    {
                        var add =
                              cards.FirstOrDefault(c => c != null && c.IsCode(CardId.TheBystialLubellion))
                           ?? cards.FirstOrDefault(c => c != null && c.IsCode(CardId.BystialDruiswurm))
                           ?? cards.FirstOrDefault(c => c != null && c.IsCode(CardId.BystialMagnamhut))
                           ?? cards.FirstOrDefault(c => c != null && c.IsCode(CardId.BlackMetalDragon))
                           ?? cards[0];
                        return Util.CheckSelectCount(new List<ClientCard> { add }, cards, min, max);
                    }
                }

                if (isSource(CardId.KnightmarePhoenix))
                {
                    if (hint == HintMsg.Discard || hint == HintMsg.ToGrave || hint == HintMsg.Release)
                    {
                        var discard =
                              cards.FirstOrDefault(c => c != null && !c.IsCode(CardId.MaxMetalmorph) && !c.IsCode(CardId.FlameCoatingMetalmorph) &&
                                                         !c.IsCode(CardId.TimeEngine) &&
                                                         !c.IsCode(CardId.RedEyesBlackFullmetalDragon) && !c.IsCode(CardId.RedEyesDarknessMetalDragon))
                           ?? cards[0];
                        return Util.CheckSelectCount(new List<ClientCard> { discard }, cards, min, max);
                    }

                    if (hint == HintMsg.Destroy || hint == HintMsg.Target || hint == HintMsg.OperateCard || hint == 505)
                    {
                        var spellRanked = GetNormalEnemySpellTargetList(true, true, CardType.Monster);
                        var pop = spellRanked.FirstOrDefault(c => c != null && cards.Contains(c) && IsEnemyFieldCard(c))
                           ?? cards.FirstOrDefault(c => c != null && c.Controller == 1 && c.Location == CardLocation.SpellZone && !c.IsShouldNotBeTarget())
                           ?? cards.FirstOrDefault(c => c != null && c.Controller == 1 && !c.IsShouldNotBeTarget())
                           ?? cards[0];
                        return Util.CheckSelectCount(new List<ClientCard> { pop }, cards, min, max);
                    }
                }

                if (isSource(CardId.AccesscodeTalker))
                {
                    if (hint == HintMsg.Target)
                    {
                        // Only the ATK-gain effect targets. Its offered list is the actual
                        // material group, including face-up banished Link monsters.
                        var material = cards.Where(c => c != null && c.HasType(CardType.Link))
                            .OrderByDescending(c => c.LinkCount > 0 ? c.LinkCount : (c.Data != null ? c.Data.Level : 0))
                            .FirstOrDefault();
                        if (material != null)
                            return Util.CheckSelectCount(new List<ClientCard> { material }, cards, min, max);
                    }

                    if (hint == HintMsg.Remove || hint == HintMsg.ToGrave || hint == HintMsg.Release)
                    {
                        int[] prefer = { CardId.HieraticSealOfTheHeavenlySpheres, CardId.KnightmarePhoenix,
                            CardId.DharcTheDarkCharmerGloomy, CardId.SPLittleKnight, CardId.StrikerDragon };
                        var cost = cards.Where(c => c != null && c.HasType(CardType.Link)).OrderBy(c =>
                        {
                            int rank = Array.IndexOf(prefer, c.Id);
                            return rank < 0 ? 99 : rank;
                        }).ThenBy(c => c.Location != CardLocation.Grave).ThenBy(c => c.LinkCount).FirstOrDefault()
                            ?? cards[0];
                        return Util.CheckSelectCount(new List<ClientCard> { cost }, cards, min, max);
                    }

                    if (hint == HintMsg.Destroy)
                    {
                        var spellRanked = GetNormalEnemySpellTargetList(false, true, CardType.Monster);
                        var allRanked = GetNormalEnemyTargetList(false, true, CardType.Monster);
                        var pop = spellRanked.FirstOrDefault(c => c != null && cards.Contains(c) && IsEnemyFieldCard(c))
                           ?? allRanked.FirstOrDefault(c => c != null && cards.Contains(c) && IsEnemyFieldCard(c))
                           ?? cards.FirstOrDefault(c => c != null && c.Controller == 1 && c.Location == CardLocation.SpellZone && c.IsFacedown())
                           ?? cards.FirstOrDefault(c => c != null && c.Controller == 1)
                           ?? cards[0];
                        return Util.CheckSelectCount(new List<ClientCard> { pop }, cards, min, max);
                    }
                }

                if ((isSource(CardId.AccelSynchroStardustDragon) || isSource(CardId.SatelliteWarrior)
                    || isSource(CardId.BaronneDeFleur) || isSource(CardId.StardustWarrior)
                    || isSource(CardId.DracoBerserkerOfTheTenyi))
                    && hint == HintMsg.SynchroMaterial)
                {
                    var own = cards.Where(c => c != null && c.Controller == 0 && c.IsFaceup()).ToList();
                    if (own.Count >= 2)
                    {
                        if (isSource(CardId.AccelSynchroStardustDragon))
                        {
                            var tuner = own.FirstOrDefault(c => c.IsCode(CardId.HopEarSquadron) && c.HasType(CardType.Tuner))
                                     ?? own.FirstOrDefault(c => c.HasType(CardType.Tuner));
                            var non =
                                  own.FirstOrDefault(c => c != tuner && c.IsCode(CardId.BystialMagnamhut) && c.Level + (tuner?.Level ?? 0) == 8)
                               ?? own.FirstOrDefault(c => c != tuner && c.IsCode(CardId.BystialDruiswurm) && c.Level + (tuner?.Level ?? 0) == 8)
                               ?? own.FirstOrDefault(c => c != tuner && !c.HasType(CardType.Tuner) && c.Level + (tuner?.Level ?? 0) == 8)
                               ?? own.FirstOrDefault(c => c != tuner && !c.HasType(CardType.Tuner));
                            if (tuner != null && non != null)
                                return Util.CheckSelectCount(new List<ClientCard> { tuner, non }, cards, min, max);
                        }

                        if (isSource(CardId.SatelliteWarrior) || isSource(CardId.BaronneDeFleur) || isSource(CardId.StardustWarrior))
                        {
                            var tuner = own.FirstOrDefault(c => c.IsCode(CardId.HopEarSquadron) && c.HasType(CardType.Tuner))
                                     ?? own.FirstOrDefault(c => c.HasType(CardType.Tuner));
                            var non =
                                  own.FirstOrDefault(c => c != tuner && c.IsCode(CardId.StardustDragon) && c.Level + (tuner?.Level ?? 0) == 10)
                               ?? own.FirstOrDefault(c => c != tuner && c.HasType(CardType.Synchro) && c.Level + (tuner?.Level ?? 0) == 10)
                               ?? own.FirstOrDefault(c => c != tuner && !c.HasType(CardType.Tuner) && c.Level + (tuner?.Level ?? 0) == 10)
                               ?? own.FirstOrDefault(c => c != tuner && !c.HasType(CardType.Tuner));
                            if (tuner != null && non != null)
                                return Util.CheckSelectCount(new List<ClientCard> { tuner, non }, cards, min, max);
                        }

                        if (isSource(CardId.DracoBerserkerOfTheTenyi))
                        {
                            var dracoMats = PickDracoBerserkerMaterials(own, 8);
                            if (dracoMats != null && dracoMats.Count >= 2)
                                return Util.CheckSelectCount(dracoMats, cards, min, max);
                        }
                    }
                }

                if ((isSource(CardId.HieraticSealOfTheHeavenlySpheres) || isSource(CardId.SPLittleKnight) || isSource(CardId.DharcTheDarkCharmerGloomy) ||
                     isSource(CardId.KnightmarePhoenix) || isSource(CardId.AccesscodeTalker)) &&
                    hint == HintMsg.LinkMaterial)
                {
                    if (isSource(CardId.HieraticSealOfTheHeavenlySpheres))
                    {
                        var redmd = cards.FirstOrDefault(c => c != null && c.Controller == 0 && c.IsFaceup() && c.IsCode(CardId.RedEyesDarknessMetalDragon));
                        var bmd = cards.FirstOrDefault(c => c != null && c.Controller == 0 && c.IsFaceup() && c.IsCode(CardId.BlackMetalDragon));
                        if (redmd != null && bmd != null)
                            return Util.CheckSelectCount(new List<ClientCard> { redmd, bmd }, cards, min, max);

                        var drags = cards.Where(c => c != null && c.Controller == 0 && c.IsFaceup() && c.HasRace(CardRace.Dragon) && !IsBoss(c))
                                         .OrderBy(ScoreOwnCardForCost).ToList();
                        if (drags.Count >= 2)
                            return Util.CheckSelectCount(new List<ClientCard> { drags[0], drags[1] }, cards, min, max);
                    }

                    if (isSource(CardId.DharcTheDarkCharmerGloomy))
                    {
                        var pool = cards.Where(c => c != null
                                                 && c.Controller == 0
                                                 && c.IsFaceup()
                                                 && !IsProtectedForLinkMat(c)
                                                 && !IsHighValueSynchroBody(c))
                                        .ToList();
                        if (ShouldHoldBlackMetalForStrikerLine())
                            pool = pool.Where(c => !c.IsCode(CardId.BlackMetalDragon)).ToList();

                        if (pool.Count < 2 && EnemyHighPressure())
                        {
                            pool = cards.Where(c => c != null && c.Controller == 0 && c.IsFaceup() && !IsProtectedForLinkMat(c)).ToList();
                            if (ShouldHoldBlackMetalForStrikerLine())
                                pool = pool.Where(c => !c.IsCode(CardId.BlackMetalDragon)).ToList();
                        }

                        var dark = pool.FirstOrDefault(c => c.HasAttribute(CardAttribute.Dark) && !IsCoreBranchBody(c))
                                ?? pool.FirstOrDefault(c => c.HasAttribute(CardAttribute.Dark));
                        var other = pool.FirstOrDefault(c => c != dark && !IsCoreBranchBody(c))
                                 ?? pool.FirstOrDefault(c => c != dark);
                        if (dark != null && other != null) return Util.CheckSelectCount(new List<ClientCard> { dark, other }, cards, min, max);
                    }

                    if (isSource(CardId.SPLittleKnight))
                    {
                        var lub = cards.FirstOrDefault(c => c != null && c.Controller == 0 && c.IsFaceup() && c.IsCode(CardId.TheBystialLubellion) && !IsProtectedForLinkMat(c));
                        var low = cards.Where(c => c != null && c.Controller == 0 && c.IsFaceup() && c != lub
                                                && c.HasType(CardType.Effect) && !IsProtectedForLinkMat(c) && !IsCoreBranchBody(c))
                                       .OrderBy(ScoreOwnCardForCost).FirstOrDefault()
                               ?? cards.Where(c => c != null && c.Controller == 0 && c.IsFaceup() && c != lub
                                                && c.HasType(CardType.Effect) && !IsProtectedForLinkMat(c))
                                       .OrderBy(ScoreOwnCardForCost).FirstOrDefault();
                        if (lub != null && low != null)
                            return Util.CheckSelectCount(new List<ClientCard> { lub, low }, cards, min, max);

                        var mats = cards.Where(c => c != null && c.Controller == 0 && c.IsFaceup()
                                                 && c.HasType(CardType.Effect) && !IsProtectedForLinkMat(c) && !IsCoreBranchBody(c))
                                        .OrderBy(ScoreOwnCardForCost).Take(2).ToList();
                        if (mats.Count < 2)
                            mats = cards.Where(c => c != null && c.Controller == 0 && c.IsFaceup() && c.HasType(CardType.Effect) && !IsProtectedForLinkMat(c))
                                        .OrderBy(ScoreOwnCardForCost).Take(2).ToList();
                        if (mats.Count >= 2) return Util.CheckSelectCount(mats, cards, min, max);
                    }

                    if (isSource(CardId.KnightmarePhoenix))
                    {
                        bool keepDharc = Bot.HasInMonstersZone(CardId.DharcTheDarkCharmerGloomy, true)
                                      && Bot.HasInExtra(CardId.AccesscodeTalker)
                                      && !Bot.HasInMonstersZone(CardId.AccesscodeTalker, true);
                        var pool = cards.Where(c => c != null && c.Controller == 0 && c.IsFaceup() && !IsProtectedForLinkMat(c)
                                                 && !(keepDharc && c.IsCode(CardId.DharcTheDarkCharmerGloomy)))
                                        .ToList();
                        var first = pool.FirstOrDefault(c => !IsCoreBranchBody(c) && !c.HasType(CardType.Link))
                                 ?? pool.FirstOrDefault(c => !IsCoreBranchBody(c))
                                 ?? pool.FirstOrDefault();
                        var second = pool.FirstOrDefault(c => c != first && !IsCoreBranchBody(c) && !c.HasType(CardType.Link))
                                  ?? pool.FirstOrDefault(c => c != first && !IsCoreBranchBody(c))
                                  ?? pool.FirstOrDefault(c => c != first);
                        if (first != null && second != null) return Util.CheckSelectCount(new List<ClientCard> { first, second }, cards, min, max);
                    }

                    if (isSource(CardId.AccesscodeTalker))
                    {
                        var phoenix = cards.FirstOrDefault(c => c != null && c.Controller == 0 && c.IsFaceup() && c.IsCode(CardId.KnightmarePhoenix));
                        var dharc = cards.FirstOrDefault(c => c != null && c.Controller == 0 && c.IsFaceup() && c.IsCode(CardId.DharcTheDarkCharmerGloomy));
                        if (phoenix != null && dharc != null) return Util.CheckSelectCount(new List<ClientCard> { phoenix, dharc }, cards, min, max);

                        var links = cards.Where(c => c != null && c.Controller == 0 && c.IsFaceup() && c.HasType(CardType.Link) && !IsProtectedForLinkMat(c))
                                         .OrderByDescending(c => c.LinkCount).ThenBy(ScoreOwnCardForCost).ToList();
                        if (links.Count >= 2) return Util.CheckSelectCount(new List<ClientCard> { links[0], links[1] }, cards, min, max);
                    }
                }

            }

            return base.OnSelectCard(cards, min, max, hint, cancelable);
        }

        public override IList<ClientCard> OnSelectSynchroMaterial(IList<ClientCard> cards,
            IList<ClientCard> mandatoryCards, int sum, int min, int max)
        {
            // There is no accepted summon identity in this callback. Candidate Card can be stale.
            // Let GameAI validate/solve the server's mandatory materials and encoded level choices.
            return base.OnSelectSynchroMaterial(cards, mandatoryCards, sum, min, max);
        }

        public override IList<ClientCard> OnSelectLinkMaterial(IList<ClientCard> cards, int min, int max)
        {
            var pick = OnSelectCard(cards, min, max, HintMsg.LinkMaterial, false);
            return pick ?? base.OnSelectLinkMaterial(cards, min, max);
        }

        // metallization flag helper (updated)
        private void TrackMetallizeTribute(ClientCard tribute)
        {
            if (tribute != null && tribute.IsMonster())
            {
                _tributedThisTurn = true;
                _lastMetallizationWasWarrior5p = (tribute.Level >= 5 && tribute.HasRace(CardRace.Warrior));
                _lastMetallizeRace = (CardRace)tribute.Race;   // cast int -> CardRace
                _lastMetallizeLevel = tribute.Level;
                _lastMetallizeTributeCode = tribute.Id;
            }
            else
            {
                _lastMetallizationWasWarrior5p = false;
                _lastMetallizeRace = (CardRace)0;              // reset safely
                _lastMetallizeLevel = 0;
                _lastMetallizeTributeCode = 0;
            }
        }

        // ============================================================
        // Scoring helpers
        // ============================================================
        private int ScoreOwnCardForCost(ClientCard c)
        {
            if (c == null) return int.MaxValue;
            int score = 5000;
            if (_neverUseAsMat.Contains(c.Id)) score += 9999;
            if (IsProtectedForLinkMat(c)) score += 8000; // don't spend premium board pieces
            if (c.IsCode(CardId.TheBystialLubellion) || c.IsCode(CardId.BystialMagnamhut) || c.IsCode(CardId.BystialDruiswurm)) score -= 2500;
            if (c.IsCode(CardId.HopEarSquadron) || c.IsCode(CardId.FidraulisHarmonia)) score -= 1500;
            if ((c.HasType(CardType.Link) && c.LinkCount >= 2) || c.HasType(CardType.Fusion | CardType.Synchro | CardType.Xyz)) score += 3000;
            if (c.EquipCards != null && c.EquipCards.Count > 0) score += 1000;
            score += Math.Max(0, c.Attack / 100);
            return score;
        }

        private int ScoreEnemyCardForRemoval(ClientCard c)
        {
            if (c == null) return -1;
            int s = 0;
            if (c.IsFloodgate()) s += 6000;
            if (c.IsMonster() && c.IsFaceup() && c.IsMonsterDangerous()) s += 4000;
            if (c.IsMonster() && c.IsFaceup() && c.IsMonsterInvincible()) s += 3500;
            if (c.EquipCards != null && c.EquipCards.Count > 0) s += 800;
            if (c.HasType(CardType.Fusion | CardType.Ritual | CardType.Synchro | CardType.Xyz)) s += 700;
            if (c.HasType(CardType.Link) && c.LinkCount >= 2) s += 700;
            if (!c.IsMonster()) s += 500;
            s += Math.Max(0, c.Attack);
            return s;
        }

        public override ClientCard OnSelectAttacker(IList<ClientCard> attackers, IList<ClientCard> defenders)
        {
            if (attackers == null || attackers.Count == 0) return base.OnSelectAttacker(attackers, defenders);

            var usable = attackers.Where(a => a != null).ToList();
            if (usable.Count == 0) return base.OnSelectAttacker(attackers, defenders);

            usable.Sort(CompareUsableAttack);
            usable.Reverse();

            if (defenders == null || defenders.Count == 0)
            {
                var direct = usable.FirstOrDefault(a => a.CanDirectAttack && a.Attack > 0);
                if (direct != null) return direct;
            }

            return usable.FirstOrDefault(a => a.Attack > 0) ?? usable[0];
        }

        // ====================================
        // Safer battle: avoid suicide + skip pointless 0 ATK swings
        // Safer battle: only winning/even trades; skip pointless hits
        public override BattlePhaseAction OnSelectAttackTarget(ClientCard attacker, IList<ClientCard> defenders)
        {
            if (attacker == null) return null;

            // 1) Never swing with 0 ATK (no payoffs in this deck)
            if (attacker.Attack <= 0)
            {
                return null;
            }

            // 2) No defenders → direct if allowed AND worth it (lethal or decent chip)
            if (defenders == null || defenders.Count == 0)
            {
                if (!attacker.CanDirectAttack) return null;
                int dmg = Math.Max(0, attacker.Attack);
                bool lethal = dmg >= Enemy.LifePoints;
                bool chipOk = (Bot.LifePoints >= Enemy.LifePoints) || dmg >= 300; // tiny chip when behind is skipped
                if (lethal || chipOk)
                    return AI.Attack(attacker, null);
                return null;
            }

            // 3) Choose the best defender we BEAT after modifiers (OnPreBattleBetween)
            ClientCard best = null;
            int bestScore = int.MinValue;

            foreach (var def in defenders)
            {
                attacker.RealPower = attacker.Attack;
                def.RealPower = def.GetDefensePower();

                // lets WindBot apply effects that alter RealPower (e.g., battle tricks)
                if (!OnPreBattleBetween(attacker, def))
                    continue;

                // strictly winning trades only
                if (attacker.RealPower > def.RealPower)
                {
                    // prefer removing dangerous/high-value targets with some margin bonus
                    int margin = attacker.RealPower - def.RealPower;
                    int score = ScoreEnemyCardForRemoval(def) + margin;

                    if (def.IsAttack()) score += 400;     // popping an ATK-pos body is usually better
                    if (score > bestScore) { best = def; bestScore = score; }
                }
                // equal trade is acceptable only if defender in ATK and we're not behind on board
                else if (attacker.RealPower == def.RealPower && def.IsAttack()
                      && Bot.GetMonsterCount() >= Enemy.GetMonsterCount())
                {
                    int score = 100; // small incentive
                    if (score > bestScore) { best = def; bestScore = score; }
                }
                // otherwise: do NOT attack this defender
            }

            if (best != null)
                return AI.Attack(attacker, best);

            // 4) Heavy Metal Raiders setup: intentional damage to grow DARK Machines for follow-up pressure.
            ClientCard setupTarget = null;
            int setupBestScore = int.MinValue;
            int setupChosenDamage = 0;
            foreach (var def in defenders)
            {
                attacker.RealPower = attacker.Attack;
                def.RealPower = def.GetDefensePower();
                if (!OnPreBattleBetween(attacker, def))
                    continue;

                int setupDamage;
                int setupPostAtk;
                if (!IsHeavyMetalRaidersSetupAttack(attacker, def, out setupDamage, out setupPostAtk))
                    continue;

                int score = ScoreEnemyCardForRemoval(def) + setupPostAtk - (setupDamage * 3);
                if (def.IsAttack()) score += 250;
                if (score > setupBestScore)
                {
                    setupBestScore = score;
                    setupTarget = def;
                    setupChosenDamage = setupDamage;
                }
            }

            if (setupTarget != null)
            {
                MMLog("[MM.Battle]", "Raiders setup swing target=" + setupTarget.Id + " dmg=" + setupChosenDamage);
                return AI.Attack(attacker, setupTarget);
            }

            return null; // don't suicide / make bad trades
        }
    }
}
