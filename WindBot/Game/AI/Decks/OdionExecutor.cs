using System;
using System.Collections.Generic;
using System.Linq;
using YGOSharp.OCGWrapper;
using YGOSharp.OCGWrapper.Enums;
using WindBot;
using WindBot.Game;
using WindBot.Game.AI;

namespace WindBot.Game.AI.Decks
{
    [Deck("Odion", "AI_Odion")]
    public class OdionExecutor : DefaultExecutor
    {
        // Archetypes (setcodes)
        // Apophis: 0x1c8
        // Serket:  0x1c7
        private const int SetcodeApophis = 0x1c8;
        private const int SetcodeSerket = 0x1c7;

        public class CardId
        {
            // Monsters
            public const int NibiruThePrimalBeing = 27204311;
            public const int PrimalBeingToken = 27204312;
            public const int AnubisTheLastJudge = 60411677;
            public const int TheWingedDragonOfRaSphereMode = 10000080;
            public const int MercilessScorpionOfSerket = 34926568;
            public const int RetaliatingC = 46502744;
            public const int ContactC = 87170768;
            public const int TheManWithTheMark = 97522863;
            public const int AshBlossomJoyousSpring = 14558127;
            public const int MaxxC = 23434538;
            public const int DrollLockBird = 94145021;

            // Spell Cards
            public const int LightningStorm = 14532163;
            public const int TreasuresOfTheKings = 69299029;
            public const int TempleOfTheKings = 29762407;
            public const int DefenseOfTheTemple = 96687733;
            public const int SuperPolymerization = 48130397;
            public const int CalledByTheGrave = 24224830;

            // Trap Cards
            public const int StatueOfAnguishPattern = 23626223;
            public const int ApophisTheSwampDeity = 85888377;
            public const int AngelStatueAzurune = 44822037;
            public const int ApophisTheSerpent = 95561146;
            public const int EvenlyMatched = 15693423;
            public const int InfiniteImpermanence = 10045474;
            public const int DominusPurge = 97045737;
            public const int VerdictOfAnubis = 59576447;

            // Extra Deck Monsters
            public const int PredaplantTriphyoverutum = 79864860;
            public const int StarvingVenomFusionDragon = 41209827;
            public const int StarvingVenomFusionDragonOfTheFourHeavenlyDragons = 27118421;
            public const int DivineScorpionBeastOfSerket = 23804920;
            public const int DivineSerpentApophis = 97800311;
            public const int GaruraWingsOfResonantLife = 11765832;
            public const int MudragonOfTheSwamp = 54757758;
            public const int DivineArsenalAAZEUSSkyThunder = 90448279;
            public const int SuperStarslayerTYPHONSkyCrisis = 93039339;
            public const int LinkSpider = 98978921;
            public const int SPLittleKnight = 29301450;
            public const int SilhouhatteRabbit = 1528054;
        }

        // ============================================================
        // Deck count parity (gold baseline)
        // Use this to safely gate 1-of / limited cards (ED + main) so we don't
        // waste or attempt lines that cannot exist.
        // ============================================================
        private readonly Dictionary<int, List<int>> DeckCountTable = new Dictionary<int, List<int>>
        {
            {3, new List<int>
                {
                    CardId.AnubisTheLastJudge,
                    CardId.TheManWithTheMark,
                    CardId.AshBlossomJoyousSpring,
                    CardId.TreasuresOfTheKings,
                    CardId.ApophisTheSwampDeity
                }
            },
            {2, new List<int>
                {
                    CardId.NibiruThePrimalBeing,
                    CardId.MercilessScorpionOfSerket,
                    CardId.MaxxC,
                    CardId.SuperPolymerization,
                    CardId.ApophisTheSerpent,
                    CardId.InfiniteImpermanence,
                    CardId.DivineScorpionBeastOfSerket,
                    CardId.DivineSerpentApophis,
                    CardId.SilhouhatteRabbit
                }
            },
            {1, new List<int>
                {
                    CardId.TheWingedDragonOfRaSphereMode,
                    CardId.RetaliatingC,
                    CardId.ContactC,
                    CardId.DrollLockBird,
                    CardId.LightningStorm,
                    CardId.TempleOfTheKings,
                    CardId.DefenseOfTheTemple,
                    CardId.CalledByTheGrave,
                    CardId.StatueOfAnguishPattern,
                    CardId.AngelStatueAzurune,
                    CardId.EvenlyMatched,
                    CardId.DominusPurge,
                    CardId.VerdictOfAnubis,
                    CardId.PredaplantTriphyoverutum,
                    CardId.StarvingVenomFusionDragon,
                    CardId.StarvingVenomFusionDragonOfTheFourHeavenlyDragons,
                    CardId.GaruraWingsOfResonantLife,
                    CardId.MudragonOfTheSwamp,
                    CardId.DivineArsenalAAZEUSSkyThunder,
                    CardId.SuperStarslayerTYPHONSkyCrisis,
                    CardId.LinkSpider,
                    CardId.SPLittleKnight
                }
            }
        };

        private enum SelectCtx
        {
            None = 0,
            SerketBanishCost = 1,
            DeityNegateTargets = 2,
            RabbitSetFromDeck = 3,
            DivineTribute = 4,
            DivineSetFromGrave = 5,
            RabbitDestroySTTarget = 6,
            StatueDestroyTarget = 7,
            DivineSerpentPopTarget = 8,
            DefenseFusionMaterials = 9,
            DivineScorpionBanishTarget = 10,
            SphereModeTributes = 11,
            RetaliatingSearchEarthInsect = 12,
            TyphonMaterial = 13,
            SecondDivineTribute = 14,
            AzuruneNegateCost = 15,
            SuperPolyFusionTarget = 16,
            SuperPolyFusionMaterials = 17,
            AnubisSummonCostToDeck = 18,
            Starv4DebuffTarget = 19,
            Starv4QuickFusionTarget = 20,
            Starv4QuickFusionMaterials = 21,
            StarvingVenomGainAtkTarget = 22,
            TyphonBounceTarget = 23
        }

        private SelectCtx _selectCtx = SelectCtx.None;

        // ---- Super Polymerization selection state ----
        // Activation sets SuperPolyFusionTarget; OnSelectCard chooses the Fusion; OnSelectFusionMaterial chooses materials.
        private bool _pendingSuperPoly = false;
        private int _superPolyChosenFusionId = 0;
        private int _superPolyBestFusionId = 0;
        private bool _superPolyAllowOwnMaterials = false;
        private bool _superPolyEmergencyDefense = false;
        private string _superPolyMustIncludeKey = string.Empty;

        // Super Poly resolution tracker (turn-scoped): enables MP2 conversion lines.
        private bool _superPolyResolvedThisTurn = false;

        // Starv4 quick fusion routing (per-chain)
        private bool _pendingStarv4QuickFusion = false;
        private int _starv4ChosenFusionId = 0;

        // ---- Anubis special summon (GY) selection state ----
        private bool _pendingAnubisSummon = false;
        private bool _didAnubisSpecialSummonThisTurn = false;

        // ---- Batch 1 milestone flags (per-turn) ----
        private bool _didAnubisDiscard = false;
        private bool _didManSearch = false;
        private bool _didTreasuresActivate = false;
        private bool _didSetForTreasures = false;
        private bool _didTreasuresSearchSerket = false;
        private bool _didSerketSummon = false;
        private bool _didSerketSearch = false;
        private bool _didTempleActivate = false;
        private bool _didFlipSerpent = false;
        private bool _didFlipSwampDeity = false;
        private bool _didSetSwampDeityFromHand = false;
        private bool _didRabbitSummon = false;
        private bool _didRabbitSet = false;
        private bool _didRabbitPopST = false;
        private bool _didDivineSummon = false;
        private bool _didDivineSetBack = false;
        private bool _usedTrapForTreasures = false;
        private bool _didSetMonsterForTreasures = false;
        private bool _didXyzSummonTyphon = false;
        private bool _didSummonSecondDivine = false;
        private int _divineSummonCount = 0;

        // Second Divine tribute selection can arrive in multiple sequential prompts (min=max=1 twice).
        // Keep track of what we already selected so we never "pad" into tributing an existing Divine.
        private readonly List<ClientCard> _secondDivineTributesChosen = new List<ClientCard>();

        // Pop budget (per-chain): prevent Statue/Rabbit/Divine from all firing at once.
        private int _chainPopCommitCount = 0;

        // Divine pop once-per-turn tracking (activation vs resolution)
        private bool _pendingDivinePop = false;
        private bool _didDivinePopThisTurn = false;

        // Droll once-per-turn
        private bool _didDrollThisTurn = false;

        // If Retaliating "C" added Maxx "C" this turn, delay Maxx "C" until next turn.
        private bool _blockMaxxCThisTurnFromRetaliating = false;


        private int _pendingDefenseFusionSerketSeq = -1;
        private bool _pendingDefenseFusionUseOpponent = false;
        private int _pendingDefenseFusionTargetId = 0;

        

        private int _pendingDefenseFusionSelectCalls = 0;
        private bool _pendingDefenseFusionFirstWasField = false;
        private string _pendingDefenseFusionFirstPickKey = null;
        private int _enemySpSummonFromExtraThisTurn = 0;
        private int _enemySpSummonFromExtraLastTurn = 0;

        // Activation vs resolution tracking (gold): set pending on activation; promote done on OnChainSolved when not negated
        private bool _pendingRabbitSet = false;
        private bool _pendingRabbitPopST = false;

        // Per-chain trap-monster ordering flags (opp-turn chain building)
        private bool _chainActivatedDeity = false;
        private bool _chainActivatedSerpent = false;
        private bool _chainActivatedStatue = false;

        // Allow set-turn activation for Deity when set by Serpent effect (EFFECT_TRAP_ACT_IN_SET_TURN)
        private bool _deitySetBySerpentThisTurn = false;

        // SpellSet-for-Treasures selection (one-shot per turn)
        private int _setForTreasuresId = 0;

        // MonsterSet-for-Treasures selection (one-shot per turn)
        private int _setMonsterForTreasuresId = 0;

        // Coarse normal-summon tracking (to avoid wasting NS on handtraps/fillers)
        private bool _normalSummonUsedThisTurn = false;

        // Prompt context flags (one-shot)
        private bool _askYesNoTreasuresSet = false;
        private bool _askYesNoSerpentSet = false;
        private bool _askYesNoDeityNegate = false;

        // Divine Serpent Apophis: set-back selection can be step-by-step (max=1 each step) depending on core build.
        // Track chosen names and whether we should answer YES to "select another?" prompts.
        private bool _askYesNoDivineSetBackMore = false;
        private readonly HashSet<int> _divineSetBackChosenIds = new HashSet<int>();
        // Enemy Imperm tracking (gold baseline)
        private readonly HashSet<int> _impermDangerColumns = new HashSet<int>();
        private bool _enemyActivateInfiniteImpermanenceFromHand = false;
        private bool _botActivateInfiniteImpermanenceFromHand = false;

        private bool _didFlipAzurune = false;
        private bool _didAzuruneNegateThisTurn = false;

        // Solved-only commit (gold): infer column on activation, commit in OnChainSolved when not negated
        private int _pendingEnemyImpermMirrorColumn = -1;
        private bool _pendingEnemyImpermFromHand = false;

        // Self Imperm tracking (same pattern; only matters if activated from Set S/T)
        private int _pendingSelfImpermColumn = -1;
        private bool _pendingSelfImpermFromHand = false;

        // Track our facedown cards set this turn (for Temple effect gating)
        private readonly List<ClientCard> _botSetThisTurn = new List<ClientCard>();

        // Avoid filling all S/T zones before Rabbit can set from Deck (debug + policy)
        private bool _loggedReserveZoneForRabbit = false;

        // Track Statue destroy targets already chosen this chain (avoid picking same card twice when multiple triggers happen)
        private readonly HashSet<string> _statueUsedTargetKeysThisChain = new HashSet<string>();
        // Cross-effect (Statue/Rabbit/Divine) de-dup per chain so we don't waste multiple pops on the same target.
        private readonly HashSet<string> _popUsedTargetKeysThisChain = new HashSet<string>();

        // Track Deity negate targets this chain so follow-up pops avoid wasting on already-negated cards
        private readonly HashSet<string> _deityNegatedTargetKeysThisChain = new HashSet<string>();

        // Sphere Mode tribute selection context (Batch 3)
        private bool _selectingSphereModeTributes = false;


        // ---- Batch 3: going-second board breaker plan tokens ----
        private bool _planEvenlyThisTurn = false;
        private bool _planEvenlyFromHand = false;
        private bool _didEvenlyActivateThisTurn = false;

        public OdionExecutor(GameAI ai, Duel duel) : base(ai, duel)
        {
            // ============================================================
            // 1) Global interaction / handtraps
            // ============================================================
            AddExecutor(ExecutorType.Activate, CardId.MaxxC, MaxxCActivate);
            AddExecutor(ExecutorType.Activate, CardId.AshBlossomJoyousSpring, DefaultAshBlossomAndJoyousSpring);
            AddExecutor(ExecutorType.Activate, CardId.DrollLockBird, DrollLockBirdActivate);
            AddExecutor(ExecutorType.Activate, CardId.RetaliatingC, RetaliatingCActivate);
            AddExecutor(ExecutorType.Activate, CardId.RetaliatingC, RetaliatingCSearchEarthInsect);
            AddExecutor(ExecutorType.Activate, CardId.ContactC, ContactCActivate);
            AddExecutor(ExecutorType.Activate, CardId.DominusPurge, DominusPurgeActivate);
            AddExecutor(ExecutorType.Activate, CardId.CalledByTheGrave, DefaultCalledByTheGrave);
            AddExecutor(ExecutorType.Activate, CardId.InfiniteImpermanence, DefaultInfiniteImpermanence);
            AddExecutor(ExecutorType.Activate, CardId.NibiruThePrimalBeing, NibiruThePrimalBeingActivate);

            // ============================================================
            // 1b) Going-second board breakers / comeback
            // ============================================================
            AddExecutor(ExecutorType.Summon, CardId.TheWingedDragonOfRaSphereMode, SphereModeSummon);
            AddExecutor(ExecutorType.Activate, CardId.LightningStorm, LightningStormActivate);
            AddExecutor(ExecutorType.Activate, CardId.SuperPolymerization, SuperPolymerizationActivate);
            // Zeus — emergency board reset (summon in Main2 after an Xyz battle; wipe as quick effect)
            AddExecutor(ExecutorType.SpSummon, CardId.DivineArsenalAAZEUSSkyThunder, Zeus_Summon);
            AddExecutor(ExecutorType.Activate, CardId.DivineArsenalAAZEUSSkyThunder, Zeus_Wipe);

            // Predaplant Triphyoverutum (Super Poly line): ED summon negate + GY self-revive
            AddExecutor(ExecutorType.Activate, CardId.PredaplantTriphyoverutum, TriphyoverutumNegateSummon);
            AddExecutor(ExecutorType.Activate, CardId.PredaplantTriphyoverutum, TriphyoverutumReviveFromGY);

            // Super Poly follow-ups: handle Starv4 / Starving Venom on-summon and Starv4 quick fusion.
            AddExecutor(ExecutorType.Activate, CardId.StarvingVenomFusionDragonOfTheFourHeavenlyDragons, Starv4DebuffOnSummon);
            AddExecutor(ExecutorType.Activate, CardId.StarvingVenomFusionDragonOfTheFourHeavenlyDragons, Starv4QuickFuseIntoStarvingVenom);
            AddExecutor(ExecutorType.Activate, CardId.StarvingVenomFusionDragon, StarvingVenomGainAtkOnSummon);
            AddExecutor(ExecutorType.Activate, CardId.EvenlyMatched, EvenlyMatchedActivate);
            AddExecutor(ExecutorType.GoToBattlePhase, GoToBattlePhaseForEvenly);

            // ============================================================
            // 2) Reactive disruptions / trap-monsters (Batch 2)
            // (Opp turn policies live here to avoid auto-firing mid-combo handlers.)
            // ============================================================
            // Chain-ordering note: we prefer to CHAIN Statue last so it RESOLVES first,
            // enabling Statue/Rabbit triggers off later trap-monster summons (Deity/Serpent).
            AddExecutor(ExecutorType.Activate, CardId.ApophisTheSwampDeity, ApophisSwampDeityDisrupt);
            AddExecutor(ExecutorType.Activate, CardId.ApophisTheSerpent, ApophisSerpentDisrupt);
            AddExecutor(ExecutorType.Activate, CardId.StatueOfAnguishPattern, StatueOfAnguishPatternActivateOppTurn);
            AddExecutor(ExecutorType.Activate, CardId.StatueOfAnguishPattern, StatueOfAnguishPatternDestroyEffect);
            AddExecutor(ExecutorType.Activate, CardId.SilhouhatteRabbit, SilhouhatteRabbitDestroySpellTrap);
            AddExecutor(ExecutorType.Activate, CardId.DivineSerpentApophis, DivineSerpentApophisPopOnTrap);
            AddExecutor(ExecutorType.Activate, CardId.AngelStatueAzurune, AngelStatueAzuruneNegateSpecialSummon);
            AddExecutor(ExecutorType.Activate, CardId.AngelStatueAzurune, AngelStatueAzuruneBattlePunish);
            AddExecutor(ExecutorType.Activate, CardId.AngelStatueAzurune, AngelStatueAzuruneActivateOppTurn);

            // S:P Little Knight (on-summon banish + quick dodge)
            AddExecutor(ExecutorType.Activate, CardId.SPLittleKnight, SPLittleKnightBanishOnSummon);
            AddExecutor(ExecutorType.Activate, CardId.SPLittleKnight, SPLittleKnightQuickBanish);

            // ============================================================
            // 3) Primary engine starters (Batch 1)
            // ============================================================
            AddExecutor(ExecutorType.Activate, CardId.AnubisTheLastJudge, AnubisDiscardSearchMan);

            AddExecutor(ExecutorType.Summon, CardId.TheManWithTheMark, TheManWithTheMarkSummon);
            AddExecutor(ExecutorType.Activate, CardId.TheManWithTheMark, TheManWithTheMarkSearch);

            // Treasures line
            AddExecutor(ExecutorType.Activate, CardId.TreasuresOfTheKings, TreasuresActivate);


            // Deterministic SpellSet: Evenly plan (keep field empty when possible)
            AddExecutor(ExecutorType.SpellSet, EvenlyMatchedSetCheck);

            // Deterministic mid-combo SpellSet to enable Treasures search if needed
            AddExecutor(ExecutorType.SpellSet, SetForTreasuresSearch);

            // C2: Monster set for Treasures condition (fallback if SpellSet couldn't reach 2 facedown)
            AddExecutor(ExecutorType.MonsterSet, MonsterSetForTreasures);

            // Treasures ignition search (while Treasures is face-up in Field Zone)
            AddExecutor(ExecutorType.Activate, CardId.TreasuresOfTheKings, TreasuresSearchSerket);

            // Serket line
            AddExecutor(ExecutorType.SpSummon, CardId.MercilessScorpionOfSerket, SerketSpecialSummon);
            AddExecutor(ExecutorType.Activate, CardId.MercilessScorpionOfSerket, SerketSearchTemple);
            // ============================================================
            // 3a) Mid-combo removal: Defense of the Temple
            // (Activate before Rabbit so Serket isn't consumed as link material.)
            // ============================================================
            AddExecutor(ExecutorType.Activate, CardId.DefenseOfTheTemple, DefenseOfTheTempleFusionSummon);
            AddExecutor(ExecutorType.Activate, CardId.DivineScorpionBeastOfSerket, DivineScorpionBeastOfSerketBanish);

            AddExecutor(ExecutorType.Activate, CardId.MercilessScorpionOfSerket, SerketBattleDestroy);

            // Temple (real Temple face-up in S/T enables set-turn Trap activation)
            AddExecutor(ExecutorType.Activate, CardId.TempleOfTheKings, TempleActivate);

            // Deterministic mid-combo SpellSet: set Swamp Deity from hand when Serpent couldn't set it from Deck
            AddExecutor(ExecutorType.SpellSet, SetSwampDeityFromHandMidCombo);

            // Apophis chain (policy: flip Serpent after Temple effect is online)
            AddExecutor(ExecutorType.Activate, CardId.ApophisTheSerpent, ApophisSerpentFlipAndSetDeity);
            AddExecutor(ExecutorType.Activate, CardId.ApophisTheSwampDeity, ApophisSwampDeityActivate);

            // Divine Serpent line (tribute 2 "Apophis" monsters)
            AddExecutor(ExecutorType.SpSummon, CardId.DivineSerpentApophis, DivineSerpentApophisSpecialSummon);
            AddExecutor(ExecutorType.SpSummon, CardId.DivineSerpentApophis, SecondDivineSerpentSummon);
            AddExecutor(ExecutorType.Activate, CardId.DivineSerpentApophis, DivineSerpentApophisSetBackApophis);

            // Rabbit extension (after Temple / Apophis setup)
            AddExecutor(ExecutorType.SpSummon, CardId.SilhouhatteRabbit, SilhouhatteRabbitSummon);
            AddExecutor(ExecutorType.Activate, CardId.SilhouhatteRabbit, SilhouhatteRabbitSetStatue);

            // Anubis recovery: after trap-monster engine is online, Special Summon Anubis from GY.
            AddExecutor(ExecutorType.SpSummon, CardId.AnubisTheLastJudge, AnubisSpecialSummonFromGrave);


            // ============================================================
            // 3c) Recovery / Alternative plays (E) - NEW
            // Threat answer: Link Spider -> S:P Little Knight
            AddExecutor(ExecutorType.SpSummon, CardId.LinkSpider, LinkSpiderSummonForSPLittleKnight);
            AddExecutor(ExecutorType.SpSummon, CardId.SPLittleKnight, SPLittleKnightSpSummon);

            // ============================================================
            AddExecutor(ExecutorType.SpSummon, CardId.SuperStarslayerTYPHONSkyCrisis, XyzSummonTyphon);
            AddExecutor(ExecutorType.Activate, CardId.SuperStarslayerTYPHONSkyCrisis, TyphonEffect);

            // ============================================================
            // 4) Housekeeping (always last)
            // ============================================================
            AddExecutor(ExecutorType.Repos, DefaultMonsterRepos);
            AddExecutor(ExecutorType.SpellSet, SpellSetCheck);
        }
        public override bool OnSelectHand()
        {
            // Go first by default.
            return true;
        }
        public override void OnNewTurn()
        {
            _selectCtx = SelectCtx.None;
            _pendingSuperPoly = false;
            _superPolyChosenFusionId = 0;
            _superPolyBestFusionId = 0;
            _superPolyAllowOwnMaterials = false;
            _superPolyEmergencyDefense = false;
            _superPolyMustIncludeKey = string.Empty;
            _superPolyResolvedThisTurn = false;
            _pendingStarv4QuickFusion = false;
            _starv4ChosenFusionId = 0;
            _pendingAnubisSummon = false;


            _enemySpSummonFromExtraLastTurn = _enemySpSummonFromExtraThisTurn;
            _enemySpSummonFromExtraThisTurn = 0;
            _didAnubisDiscard = false;
            _didManSearch = false;
            _didTreasuresActivate = false;
            _didSetForTreasures = false;
            _didTreasuresSearchSerket = false;
            _didSerketSummon = false;
            _didSerketSearch = false;
            _didTempleActivate = false;
            _didFlipSerpent = false;
            _didFlipSwampDeity = false;
            _didSetSwampDeityFromHand = false;
            _didRabbitSummon = false;
            _didRabbitSet = false;
            _didRabbitPopST = false;
            _pendingRabbitSet = false;
            _pendingRabbitPopST = false;
            _didDivineSummon = false;
            _didDivineSetBack = false;

            _usedTrapForTreasures = false;
            _didSetMonsterForTreasures = false;
            _setMonsterForTreasuresId = 0;
            _normalSummonUsedThisTurn = false;
            _didXyzSummonTyphon = false;
            _didSummonSecondDivine = false;
            _divineSummonCount = 0;

            _secondDivineTributesChosen.Clear();

            _chainPopCommitCount = 0;
            _pendingDivinePop = false;
            _didDivinePopThisTurn = false;
            _didDrollThisTurn = false;


            _deitySetBySerpentThisTurn = false;

            _planEvenlyThisTurn = false;
            _planEvenlyFromHand = false;
            _didEvenlyActivateThisTurn = false;

            _chainActivatedDeity = false;
            _chainActivatedSerpent = false;
            _chainActivatedStatue = false;

            _setForTreasuresId = 0;

            _askYesNoTreasuresSet = false;
            _askYesNoSerpentSet = false;
            _askYesNoDeityNegate = false;

            _askYesNoDivineSetBackMore = false;
            _divineSetBackChosenIds.Clear();

            _impermDangerColumns.Clear();
            _enemyActivateInfiniteImpermanenceFromHand = false;


            _pendingEnemyImpermMirrorColumn = -1;
            _pendingEnemyImpermFromHand = false;
            _loggedReserveZoneForRabbit = false;

            _botSetThisTurn.Clear();
            _deityNegatedTargetKeysThisChain.Clear();
            _popUsedTargetKeysThisChain.Clear();
            _selectingSphereModeTributes = false;

            _blockMaxxCThisTurnFromRetaliating = false;

            _didFlipAzurune = false;
            _didAzuruneNegateThisTurn = false;

        ODLog("[OD.OnNewTurn]", "reset");
            base.OnNewTurn();
        }
        public override void OnChainEnd()
        {
            // per-chain one-shots
            _selectCtx = SelectCtx.None;
            _pendingSuperPoly = false;
            _superPolyChosenFusionId = 0;
            _superPolyBestFusionId = 0;
            _superPolyAllowOwnMaterials = false;
            _superPolyEmergencyDefense = false;
            _superPolyMustIncludeKey = string.Empty;
            _pendingStarv4QuickFusion = false;
            _starv4ChosenFusionId = 0;
            _pendingAnubisSummon = false;



            // If a multi-step Second Divine tribute selection was in progress, drop it now.
            _secondDivineTributesChosen.Clear();
            _askYesNoTreasuresSet = false;
            _askYesNoSerpentSet = false;
            _askYesNoDeityNegate = false;
            _enemyActivateInfiniteImpermanenceFromHand = false;

            _chainPopCommitCount = 0;
            _pendingDivinePop = false;

            _pendingRabbitSet = false;
            _pendingRabbitPopST = false;
            _pendingEnemyImpermMirrorColumn = -1;
            _pendingEnemyImpermFromHand = false;

            _chainActivatedDeity = false;
            _chainActivatedSerpent = false;
            _chainActivatedStatue = false;

            // prune set-this-turn list (keep per-turn semantics, but drop stale refs)
            for (int i = _botSetThisTurn.Count - 1; i >= 0; i--)
            {
                ClientCard c = _botSetThisTurn[i];
                if (c == null) { _botSetThisTurn.RemoveAt(i); continue; }
                if (c.Location != CardLocation.SpellZone && c.Location != CardLocation.MonsterZone)
                    _botSetThisTurn.RemoveAt(i);
            }

            _statueUsedTargetKeysThisChain.Clear();
            _deityNegatedTargetKeysThisChain.Clear();
            _popUsedTargetKeysThisChain.Clear();
            _selectingSphereModeTributes = false;

            _botActivateInfiniteImpermanenceFromHand = false;
            _pendingSelfImpermColumn = -1;
            _pendingSelfImpermFromHand = false;

            _askYesNoDivineSetBackMore = false;
            _divineSetBackChosenIds.Clear();

            base.OnChainEnd();
        }
        public override void OnChaining(int player, ClientCard card)
        {
            Duel.LastChainTargets.Clear();

            // Enemy Imperm column tracking (gold): infer on activation, commit in OnChainSolved.
            if (player == 1 && card != null && card.Id == CardId.InfiniteImpermanence)
            {
                _pendingEnemyImpermMirrorColumn = -1;
                _pendingEnemyImpermFromHand = false;

                if (_enemyActivateInfiniteImpermanenceFromHand)
                {
                    // Activated from hand: no column lock.
                    _pendingEnemyImpermFromHand = true;
                    _enemyActivateInfiniteImpermanenceFromHand = false;
                    ODLog("[OD.Imperm.Track]", "pending: enemy imperm from hand -> no column");
                }
                else
                {
                    // Activated from set S/T: infer its column by sequence and mirror it (4 - seq).
                    int seq = -1;

                    if (card.Location == CardLocation.SpellZone && card.Sequence >= 0 && card.Sequence <= 4)
                    {
                        seq = card.Sequence;
                    }
                    else
                    {
                        // Fallback: scan for the face-up Impermanence currently on the enemy field.
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
                    {
                        _pendingEnemyImpermMirrorColumn = 4 - seq;
                        ODLog("[OD.Imperm.Track]", "pending dangerCol=" + _pendingEnemyImpermMirrorColumn);
                    }
                    else
                    {
                        ODLog("[OD.Imperm.Track]", "pending: enemy imperm -> unable to infer column");
                    }
                }
            }

            // Self Imperm column tracking (same rule): infer on activation, commit in OnChainSolved.
            if (player == 0 && card != null && card.Id == CardId.InfiniteImpermanence)
            {
                _pendingSelfImpermColumn = -1;
                _pendingSelfImpermFromHand = false;

                if (_botActivateInfiniteImpermanenceFromHand)
                {
                    // Activated from hand: no column lock.
                    _pendingSelfImpermFromHand = true;
                    _botActivateInfiniteImpermanenceFromHand = false;
                    ODLog("[OD.Imperm.Track]", "pending: self imperm from hand -> no column");
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
                        // Fallback: scan for the face-up Impermanence currently on our field.
                        for (int i = 0; i < 5; i++)
                        {
                            ClientCard z = Bot.SpellZone[i];
                            if (z != null && z.IsFaceup() && z.IsCode(CardId.InfiniteImpermanence))
                            {
                                seq = i;
                                break;
                            }
                        }
                    }

                    if (seq >= 0 && seq <= 4)
                    {
                        _pendingSelfImpermColumn = seq;
                        ODLog("[OD.Imperm.Track]", "pending self dangerCol=" + _pendingSelfImpermColumn);
                    }
                    else
                    {
                        ODLog("[OD.Imperm.Track]", "pending: self imperm -> unable to infer column");
                    }
                }
            }

            base.OnChaining(player, card);
        }


        public override void OnChainSolved(int chainIndex)
        {
            ChainInfo solving = Duel.GetCurrentSolvingChainInfo();

            // Commit solved-only enemy column-lock state.
            if (solving != null && solving.ActivatePlayer == 1 && solving.IsCode(CardId.InfiniteImpermanence))
            {
                if (!Duel.IsCurrentSolvingChainNegated())
                {
                    if (_pendingEnemyImpermMirrorColumn >= 0 && _pendingEnemyImpermMirrorColumn <= 4)
                    {
                        if (_impermDangerColumns.Add(_pendingEnemyImpermMirrorColumn))
                            ODLog("[OD.Imperm.Track]", "commit dangerCol=" + _pendingEnemyImpermMirrorColumn);
                    }
                    else if (_pendingEnemyImpermFromHand)
                    {
                        ODLog("[OD.Imperm.Track]", "commit: enemy imperm from hand -> no column");
                    }
                    else
                    {
                        ODLog("[OD.Imperm.Track]", "commit: unable to infer column");
                    }
                }
                else
                {
                    ODLog("[OD.Imperm.Track]", "imperm negated -> no column");
                }

                _pendingEnemyImpermMirrorColumn = -1;
                _pendingEnemyImpermFromHand = false;
            }

            if (solving != null && solving.ActivatePlayer == 0 && solving.IsCode(CardId.InfiniteImpermanence))
            {
                if (!Duel.IsCurrentSolvingChainNegated())
                {
                    if (_pendingSelfImpermColumn >= 0 && _pendingSelfImpermColumn <= 4)
                    {
                        if (_impermDangerColumns.Add(_pendingSelfImpermColumn))
                            ODLog("[OD.Imperm.Track]", "commit self dangerCol=" + _pendingSelfImpermColumn);
                    }
                    else if (_pendingSelfImpermFromHand)
                    {
                        ODLog("[OD.Imperm.Track]", "commit: self imperm from hand -> no column");
                    }
                    else
                    {
                        ODLog("[OD.Imperm.Track]", "commit: unable to infer self column");
                    }
                }
                else
                {
                    ODLog("[OD.Imperm.Track]", "self imperm negated -> no column");
                }

                _pendingSelfImpermColumn = -1;
                _pendingSelfImpermFromHand = false;
            }


            // Promote Rabbit pending -> done only on successful resolution.
            // Do NOT rely on ActivateDescription equality here; some engines normalize trigger desc to 0/-1.
            if (solving != null && solving.ActivatePlayer == 0)
            {
                bool negated = Duel.IsCurrentSolvingChainNegated();
                int desc = solving.ActivateDescription;

                // ------------------------------------------------------------
                // Silhouhatte Rabbit: (1) set-from-deck OR (2) pop S/T
                // ------------------------------------------------------------
                if (solving.IsCode(CardId.SilhouhatteRabbit))
                {
                    if (_pendingRabbitSet)
                    {
                        if (!negated)
                        {
                            _didRabbitSet = true;
                            ODLog("[OD.Rabbit.Set]", "resolved -> done (desc=" + desc + ")");
                        }
                        else
                        {
                            ODLog("[OD.Rabbit.Set]", "negated -> not done (desc=" + desc + ")");
                        }
                        _pendingRabbitSet = false;
                    }

                    if (_pendingRabbitPopST)
                    {
                        if (!negated)
                        {
                            _didRabbitPopST = true;
                            ODLog("[OD.Rabbit.PopST]", "resolved -> done (desc=" + desc + ")");
                        }
                        else
                        {
                            ODLog("[OD.Rabbit.PopST]", "negated -> not done (desc=" + desc + ")");
                        }
                        _pendingRabbitPopST = false;
                    }
                }

                // ------------------------------------------------------------
                // Divine Serpent Apophis: pop on Trap activation (once per turn)
                // Activation sets _pendingDivinePop; resolution promotes _didDivinePopThisTurn.
                // ------------------------------------------------------------
                if (solving.IsCode(CardId.DivineSerpentApophis))
                {
                    if (_pendingDivinePop)
                    {
                        if (!negated)
                        {
                            _didDivinePopThisTurn = true;
                            ODLog("[OD.Divine.Pop]", "resolved -> done (desc=" + desc + ")");
                        }
                        else
                        {
                            ODLog("[OD.Divine.Pop]", "negated -> not done (desc=" + desc + ")");
                        }
                        _pendingDivinePop = false;
                    }
                }
            }

            base.OnChainSolved(chainIndex);
        
            // Super Poly clean-up (selection routing may have been cleared earlier, but keep it tidy).
            if (solving != null && solving.ActivatePlayer == 0 && solving.IsCode(CardId.SuperPolymerization))
            {
                int resolvedFusionId = _superPolyChosenFusionId;
                if (!Duel.IsCurrentSolvingChainNegated())
                {
                    _superPolyResolvedThisTurn = true;
                    ODLog("[OD.SuperPoly.Resolved]", "turnFlag=1 fusionId=" + resolvedFusionId);
                }

                _pendingSuperPoly = false;
                _superPolyChosenFusionId = 0;

                if (_selectCtx == SelectCtx.SuperPolyFusionTarget || _selectCtx == SelectCtx.SuperPolyFusionMaterials)
                    _selectCtx = SelectCtx.None;

                ODLog("[OD.SuperPoly.ChainSolved]", "clear");
            }

            // Starv4 quick fusion chain clean-up
            if (solving != null && solving.ActivatePlayer == 0 && solving.IsCode(CardId.StarvingVenomFusionDragonOfTheFourHeavenlyDragons) && _pendingStarv4QuickFusion)
            {
                _pendingStarv4QuickFusion = false;
                _starv4ChosenFusionId = 0;
                if (_selectCtx == SelectCtx.Starv4QuickFusionTarget || _selectCtx == SelectCtx.Starv4QuickFusionMaterials)
                    _selectCtx = SelectCtx.None;
                ODLog("[OD.Starv4.ChainSolved]", "clear");
            }

}

        public override void OnMove(ClientCard card, int previousControler, int previousLocation, int currentControler, int currentLocation)
        {
            // TY-PHON: track when the opponent Special Summons from the Extra Deck.
            if (previousControler == 1
                && previousLocation == (int)CardLocation.Extra
                && currentControler == 1
                && currentLocation == (int)CardLocation.MonsterZone)
            {
                _enemySpSummonFromExtraThisTurn++;
                if (_enemySpSummonFromExtraThisTurn <= 2)
                    ODLog("[OD.Typhon.Track]", "enemy ED sp=" + _enemySpSummonFromExtraThisTurn);
            }

            // Gold Imperm tracking: if an Impermanence moves Hand -> SZone face-up, it was activated from hand (no column lock).
            if (card != null && card.Id == CardId.InfiniteImpermanence
                && previousLocation == (int)CardLocation.Hand && currentLocation == (int)CardLocation.SpellZone
                && !card.IsFacedown())
            {
                if (currentControler == 1)
                {
                    _enemyActivateInfiniteImpermanenceFromHand = true;
                    ODLog("[OD.Imperm.Track]", "enemy fromHand=1");
                }
                else if (currentControler == 0)
                {
                    _botActivateInfiniteImpermanenceFromHand = true;
                    ODLog("[OD.Imperm.Track]", "self fromHand=1");
                }
            }

            // Track our own facedown sets this turn (hand/deck/GY -> S/T zone).
            // Used for Temple-of-the-Kings "set this turn" logic and for debugging S/T capacity.
            if (card != null
                && currentControler == 0
                && currentLocation == (int)CardLocation.SpellZone
                && card.IsFacedown())
            {
                if (!_botSetThisTurn.Contains(card))
                {
                    _botSetThisTurn.Add(card);
                    ODLog("[OD.SetTrack]", "set id=" + card.Id + " from=" + ((CardLocation)previousLocation));
                }
            }


            base.OnMove(card, previousControler, previousLocation, currentControler, currentLocation);
        }

        public override int OnSelectPlace(int cardId, int player, CardLocation location, int available)
        {
            // Contact "C" must be placed on the opponent's field (prefer main monster zones).
            if (cardId == CardId.ContactC && player == 1 && location == CardLocation.MonsterZone)
            {
                int[] prefer = { 2, 1, 3, 0, 4, 5, 6 };
                foreach (int zone in prefer)
                {
                    int mask = (1 << zone);
                    if ((available & mask) != 0)
                        return mask;
                }
            }

            // Link Spider / S:P Little Knight placement:
            // - If EMZ is already occupied, prefer main monster zones (linked zones) over the other EMZ.
            // - Otherwise, prefer EMZ first.
            if (player == 0 && location == CardLocation.MonsterZone
                && (cardId == CardId.LinkSpider || cardId == CardId.SPLittleKnight))
            {
                bool emzOccupied =
                    (Bot.MonsterZone.Length > 5 && Bot.MonsterZone[5] != null)
                    || (Bot.MonsterZone.Length > 6 && Bot.MonsterZone[6] != null);

                int[] preferMain = { 2, 1, 3, 0, 4 };
                int[] preferEmz = { 5, 6 };

                IEnumerable<int> order = emzOccupied
                    ? preferMain.Concat(preferEmz)
                    : preferEmz.Concat(preferMain);

                foreach (int zone in order)
                {
                    int mask = (1 << zone);
                    if ((available & mask) != 0)
                    {
                        ODLog("[OD.ExtraPlace]", $"card={cardId} zone={zone} emzOcc={(emzOccupied ? 1 : 0)} avail={available}");
                        return mask;
                    }
                }
            }

            if (player == 0 && location == CardLocation.SpellZone && _impermDangerColumns.Count > 0)
            {
                int[] preferred = new[] { 1, 3, 0, 2, 4, 5 };
                int chosen = 0;
                bool forced = false;

                // First pass: pick a safe column
                foreach (int zoneId in preferred)
                {
                    int zoneMask = (1 << zoneId);
                    if ((available & zoneMask) == 0) continue;

                    if (zoneId >= 0 && zoneId <= 4 && _impermDangerColumns.Contains(zoneId))
                        continue;

                    chosen = zoneMask;
                    break;
                }

                // Second pass: forced into danger / only danger available
                if (chosen == 0)
                {
                    forced = true;
                    foreach (int zoneId in preferred)
                    {
                        int zoneMask = (1 << zoneId);
                        if ((available & zoneMask) == 0) continue;
                        chosen = zoneMask;
                        break;
                    }
                }

                int chosenCol = -1;
                if (chosen != 0)
                {
                    for (int i = 0; i <= 5; i++)
                    {
                        if (chosen == (1 << i)) { chosenCol = i; break; }
                    }
                }

                if (forced)
                    ODLog("[OD.SetZone][FORCED]", $"card={cardId} col={chosenCol} dangerCols={string.Join(",", _impermDangerColumns.OrderBy(x => x))} avail={available}");
                else
                    ODLog("[OD.SetZone]", $"card={cardId} col={chosenCol} avoidedDanger=1 dangerCols={string.Join(",", _impermDangerColumns.OrderBy(x => x))}");

                if (chosen != 0) return chosen;
            }

            return base.OnSelectPlace(cardId, player, location, available);
        }

        private bool TrapMonsterDangerVeto(string tag, ClientCard c = null)
        {
            c = c ?? Card;
            if (c == null) return false;
            if (!IsSTInEnemyImpermDangerColumn(c)) return false;

            bool safeAltExists = Bot.SpellZone.Any(z =>
                z != null && z.IsFacedown()
                && (z.IsCode(CardId.ApophisTheSerpent) || z.IsCode(CardId.ApophisTheSwampDeity) || z.IsCode(CardId.StatueOfAnguishPattern))
                && !IsSTInEnemyImpermDangerColumn(z));

            if (safeAltExists)
            {
                ODLog("[OD.TrapMonster][VETO]", $"tag={tag} card={c.Id} col={c.Sequence} dangerCols={string.Join(",", _impermDangerColumns.OrderBy(x => x))}");
                return true;
            }

            ODLog("[OD.TrapMonster][FORCED]", $"tag={tag} card={c.Id} col={c.Sequence} dangerCols={string.Join(",", _impermDangerColumns.OrderBy(x => x))}");
            return false;
        }

        private bool IsTrapMonsterPreferDefence(int id)
        {
            return id == CardId.StatueOfAnguishPattern
                || id == CardId.ApophisTheSerpent
                || id == CardId.ApophisTheSwampDeity
                || id == CardId.AngelStatueAzurune;
        }

        public override CardPosition OnSelectPosition(int cardId, IList<CardPosition> positions)
        {
            if (cardId == CardId.ContactC)
                return CardPosition.FaceUpDefence;

            // Nibiru token must be placed in DEF.
            if (cardId == CardId.PrimalBeingToken)
            {
                if (positions.Contains(CardPosition.FaceUpDefence))
                    return CardPosition.FaceUpDefence;
                if (positions.Contains(CardPosition.FaceUpAttack))
                    return CardPosition.FaceUpAttack;
            }

            if (cardId == CardId.RetaliatingC)
            {
                if (positions.Contains(CardPosition.FaceUpDefence))
                    return CardPosition.FaceUpDefence;
                if (positions.Contains(CardPosition.FaceUpAttack))
                    return CardPosition.FaceUpAttack;
            }

            // Trap-monsters are primarily defensive interruptions in this deck, but we should flip to ATK when
            // we are pushing damage / lethal on our turn.
            if (IsTrapMonsterPreferDefence(cardId))
            {
                bool canAtk = positions.Contains(CardPosition.FaceUpAttack);
                bool canDef = positions.Contains(CardPosition.FaceUpDefence);

                if (!canAtk && canDef) return CardPosition.FaceUpDefence;
                if (!canDef && canAtk) return CardPosition.FaceUpAttack;

                int atk = 0;
                NamedCard cd = NamedCard.Get(cardId);
                if (cd != null)
                {
                    atk = cd.Attack;
                }

                // Opponent turn: stay defensive.
                if (IsOppTurn())
                {
                    ODLog("[OD.Position]", "trap-monster DEF (opp turn) id=" + cardId);
                    return CardPosition.FaceUpDefence;
                }

                // Our turn: push damage when board is clear, or when a simple lethal window is detected.
                bool enemyHasMonsters = Enemy.MonsterZone.Any(m => m != null);
                int currentAttack = Util.GetTotalAttackingMonsterAttack(0);
                int potentialAttack = currentAttack + Math.Max(0, atk);

                int enemyMonsters = CountEnemyMonsters();
                int bestEnemyPower = Enemy.MonsterZone.Where(m => m != null).Select(m => m.GetDefensePower()).DefaultIfEmpty(0).Max();
                int bestOurAtk = Math.Max(Util.GetBestAttack(Bot), atk);

                bool directLethal = !enemyHasMonsters && potentialAttack >= Enemy.LifePoints;
                bool softLethal = (enemyMonsters <= 1) && (bestOurAtk >= bestEnemyPower) && (potentialAttack >= Enemy.LifePoints);

                if (canAtk && (directLethal || softLethal || !enemyHasMonsters))
                {
                    ODLog("[OD.Position]", "trap-monster ATK (push) id=" + cardId +
                          " (enemyMon=" + enemyMonsters + ", potAtk=" + potentialAttack + ", enemyLP=" + Enemy.LifePoints + ")");
                    return CardPosition.FaceUpAttack;
                }

                // Otherwise: default to DEF.
                if (canDef)
                {
                    ODLog("[OD.Position]", "trap-monster DEF (default) id=" + cardId +
                          " (enemyMon=" + enemyMonsters + ", bestEnemy=" + bestEnemyPower + ", bestOur=" + bestOurAtk + ")");
                    return CardPosition.FaceUpDefence;
                }

                return CardPosition.FaceUpAttack;
            }

            return base.OnSelectPosition(cardId, positions);
        }

        public override bool OnSelectYesNo(int desc)
        {
            // Gate one-shot YES routing by current solving chain card.
            // Prevents stale flags from answering unrelated prompts.
            ChainInfo solving = Duel.GetCurrentSolvingChainInfo();

            if (_askYesNoTreasuresSet)
            {
                if (solving != null && solving.IsCode(CardId.TreasuresOfTheKings))
                {
                    _askYesNoTreasuresSet = false;
                    return true;
                }
            }

            if (_askYesNoSerpentSet)
            {
                if (solving != null && solving.IsCode(CardId.ApophisTheSerpent))
                {
                    _askYesNoSerpentSet = false;
                    return true;
                }
            }

            if (_askYesNoDeityNegate)
            {
                if (solving != null && solving.IsCode(CardId.ApophisTheSwampDeity))
                {
                    _askYesNoDeityNegate = false;
                    return true;
                }
            }

            if (_askYesNoDivineSetBackMore)
            {
                // "Select another card?" prompt during Divine Serpent Apophis set-back selection.
                if (solving != null && solving.IsCode(CardId.DivineSerpentApophis))
                {
                    _askYesNoDivineSetBackMore = false;
                    return true;
                }
            }

            // Divine set-back flow: if we did not take the YES branch above, default to NO and clean up.
            if (solving != null && solving.IsCode(CardId.DivineSerpentApophis) && _selectCtx == SelectCtx.DivineSetFromGrave && _divineSetBackChosenIds.Count > 0)
            {
                _askYesNoDivineSetBackMore = false;
                _selectCtx = SelectCtx.None;
                return false;
            }

            // Lua: aux.Stringid(id,1)
            if (solving != null && solving.IsCode(CardId.VerdictOfAnubis))
            {
                int optNuke = Util.GetStringId(CardId.VerdictOfAnubis, 1);
                if (desc == optNuke)
                {
                    // Only choose YES when we can actually perform the extra destroy (Temple face-up) and it matters.
                    bool templeFaceup = HasTempleEffectOnline();
                    bool enemyHasMonsters = Enemy.MonsterZone.Any(c => c != null);
                    if (templeFaceup && enemyHasMonsters)
                    {
                        ODLog("[OD.Verdict]", "Yes: nuke line (Temple face-up, enemy monsters present)");
                        return true;
                    }
                    ODLog("[OD.Verdict]", "No: nuke line (no Temple or no targets)");
                    return false;
                }
            }

            return base.OnSelectYesNo(desc);
        }
        public override IList<ClientCard> OnSelectCard(IList<ClientCard> cards, int min, int max, int hint, bool cancelable)
        {
            if (_selectCtx == SelectCtx.SerketBanishCost && hint == HintMsg.Remove)
            {
                IList<ClientCard> chosen = SelectSerketBanishCost(cards, min, max);
                if (chosen != null)
                {
                    _selectCtx = SelectCtx.None;
            _pendingSuperPoly = false;
            _superPolyChosenFusionId = 0;
                    return chosen;
                }
            }

            if (_selectCtx == SelectCtx.DeityNegateTargets && hint == HintMsg.Disable)
            {
                IList<ClientCard> chosen = SelectDeityNegateTargets(cards, min, max);
                if (chosen != null)
                {
                    _selectCtx = SelectCtx.None;
            _pendingSuperPoly = false;
            _superPolyChosenFusionId = 0;
                    return chosen;
                }
            }

            if (_selectCtx == SelectCtx.RabbitSetFromDeck && (hint == HintMsg.Set || hint == 0))
            {
                IList<ClientCard> chosen = SelectRabbitSetFromDeck(cards, min, max);
                if (chosen != null)
                {
                    _selectCtx = SelectCtx.None;
            _pendingSuperPoly = false;
            _superPolyChosenFusionId = 0;
                    return chosen;
                }
            }

            if (_selectCtx == SelectCtx.DivineTribute && (hint == HintMsg.Release || (min == 2 && max == 2)))
            {
                IList<ClientCard> chosen = SelectDivineTributes(cards, min, max);
                if (chosen != null)
                {
                    _selectCtx = SelectCtx.None;
            _pendingSuperPoly = false;
            _superPolyChosenFusionId = 0;
                    return chosen;
                }
            }

            if (_selectCtx == SelectCtx.DivineSetFromGrave && (hint == HintMsg.Set || hint == 0))
            {
                IList<ClientCard> chosen = SelectDivineSetFromGrave(cards, min, max);
                if (chosen != null)
                {
                    // Multi-select (max>1): single prompt, clear immediately.
                    // Step-by-step (max==1): keep ctx if we intend to pick more.
                    if (max > 1 || !_askYesNoDivineSetBackMore)
                        _selectCtx = SelectCtx.None;

                    return chosen;
                }
            }

            if (_selectCtx == SelectCtx.RabbitDestroySTTarget && (hint == HintMsg.Destroy || hint == HintMsg.Target || hint == 0))
            {
                IList<ClientCard> chosen = SelectRabbitDestroySTTarget(cards, min, max);
                if (chosen != null)
                {
                    _selectCtx = SelectCtx.None;
            _pendingSuperPoly = false;
            _superPolyChosenFusionId = 0;
                    return chosen;
                }
            }

            if (_selectCtx == SelectCtx.StatueDestroyTarget && (hint == HintMsg.Destroy || hint == HintMsg.Target || hint == 0))
            {
                IList<ClientCard> chosen = SelectStatueDestroyTarget(cards, min, max);
                if (chosen != null)
                {
                    _selectCtx = SelectCtx.None;
            _pendingSuperPoly = false;
            _superPolyChosenFusionId = 0;
                    return chosen;
                }
            }

            if (_selectCtx == SelectCtx.DivineSerpentPopTarget && (hint == HintMsg.Destroy || hint == HintMsg.Target || hint == 0))
            {
                IList<ClientCard> chosen = SelectDivineSerpentPopTarget(cards, min, max);
                if (chosen != null)
                {
                    _selectCtx = SelectCtx.None;
            _pendingSuperPoly = false;
            _superPolyChosenFusionId = 0;
                    return chosen;
                }
            }

            // ---- Starv4 / Starving Venom prompt routing ----
            if (_selectCtx == SelectCtx.Starv4DebuffTarget && (hint == HintMsg.Disable || hint == HintMsg.Target || hint == 0))
            {
                var chosen = SelectStarv4DebuffTarget(cards, min, max);
                if (chosen != null) return chosen;
            }

            if (_selectCtx == SelectCtx.Starv4QuickFusionTarget)
            {
                var chosen = SelectStarv4QuickFusionTarget(cards, min, max);
                if (chosen != null) return chosen;
            }

            if (_selectCtx == SelectCtx.StarvingVenomGainAtkTarget && (hint == HintMsg.Faceup || hint == 0))
            {
                var chosen = SelectStarvingVenomGainAtkTarget(cards, min, max);
                if (chosen != null) return chosen;
            }

            if (_selectCtx == SelectCtx.SuperPolyFusionTarget)

            {

                var chosen = SelectSuperPolyFusionTarget(cards, min, max);

                if (chosen != null) return chosen;

            }


            if (_selectCtx == SelectCtx.DefenseFusionMaterials && (hint == HintMsg.FusionMaterial || hint == HintMsg.Target || hint == 0))
            {
                IList<ClientCard> chosen = SelectDefenseFusionMaterials(cards, min, max);
                if (chosen != null)
                {
                    // Fusion material selection may be multi-step (max==1 called twice).
                    // Keep ctx until SelectDefenseFusionMaterials clears _pendingDefenseFusionTargetId.
                    if (_pendingDefenseFusionTargetId == 0)
                        _selectCtx = SelectCtx.None;
                    return chosen;
                }
            }

            if (_selectCtx == SelectCtx.DivineScorpionBanishTarget && (hint == HintMsg.Remove || hint == HintMsg.Target || hint == 0))
            {
                IList<ClientCard> chosen = SelectDivineScorpionBanishTarget(cards, min, max);
                if (chosen != null)
                {
                    _selectCtx = SelectCtx.None;
            _pendingSuperPoly = false;
            _superPolyChosenFusionId = 0;
                    return chosen;
                }
            }

            if (_selectCtx == SelectCtx.RetaliatingSearchEarthInsect && (hint == HintMsg.AddToHand || hint == 0))
            {
                IList<ClientCard> chosen = SelectRetaliatingSearchEarthInsect(cards, min, max);
                if (chosen != null)
                {
                    _selectCtx = SelectCtx.None;
            _pendingSuperPoly = false;
            _superPolyChosenFusionId = 0;
                    return chosen;
                }
            }
            // Sphere Mode tribute selection (WindBot doesn't expose OnSelectTribute in this codebase; tributes arrive via OnSelectCard with HintMsg.Release).
            if (_selectCtx == SelectCtx.SphereModeTributes && (hint == HintMsg.Release || (min == 3 && max == 3)))
            {
                IList<ClientCard> chosen = SelectSphereModeTributes(cards, min, max, cancelable);
                _selectCtx = SelectCtx.None;
            _pendingSuperPoly = false;
            _superPolyChosenFusionId = 0;
                _selectingSphereModeTributes = false;
                if (chosen != null)
                    return chosen;
            }

            if (_selectCtx == SelectCtx.TyphonMaterial && (hint == HintMsg.XyzMaterial || hint == 0))
            {
                IList<ClientCard> matPick = SelectTyphonMaterial(cards, min, max);
                if (matPick != null)
                {
                    _selectCtx = SelectCtx.None;
                    return matPick;
                }
            }

            if (_selectCtx == SelectCtx.TyphonBounceTarget && (hint == HintMsg.ReturnToHand || hint == HintMsg.Target || hint == 0))
            {
                IList<ClientCard> bouncePick = SelectTyphonBounceTarget(cards, min, max, cancelable);
                if (bouncePick != null)
                {
                    _selectCtx = SelectCtx.None;
                    return bouncePick;
                }
            }

            if (_selectCtx == SelectCtx.SecondDivineTribute && (hint == HintMsg.Release || hint == 0 || (min <= 2 && max <= 2)))
            {
                // IMPORTANT: This tribute selection can be requested as two sequential prompts (min=max=1 twice).
                // Do NOT clear _selectCtx here; the selector will clear it only after the second pick.
                IList<ClientCard> chosen = SelectSecondDivineTributes(cards, min, max);
                if (chosen != null)
                    return chosen;
            }

            if (_selectCtx == SelectCtx.AzuruneNegateCost)
            {
                IList<ClientCard> chosen = SelectAzuruneNegateCost(cards, min, max);
                if (chosen != null)
                {
                    _selectCtx = SelectCtx.None;
            _pendingSuperPoly = false;
            _superPolyChosenFusionId = 0;
                    return chosen;
                }
            }


            if (_selectCtx == SelectCtx.AnubisSummonCostToDeck && (hint == HintMsg.ToDeck || hint == HintMsg.Target || hint == 0))
            {
                IList<ClientCard> chosen = SelectAnubisSummonCostToDeck(cards, min, max);
                if (chosen != null)
                    return chosen;
            }

            return base.OnSelectCard(cards, min, max, hint, cancelable);
        }

        public override IList<ClientCard> OnSelectFusionMaterial(IList<ClientCard> cards, int min, int max)
        {
            if (_selectCtx == SelectCtx.Starv4QuickFusionMaterials)
            {
                var chosen = SelectStarv4QuickFusionMaterials(cards, min, max);
                if (chosen != null) return chosen;
            }

            if (_selectCtx == SelectCtx.SuperPolyFusionMaterials)
            {
                var chosen = SelectSuperPolyFusionMaterials(cards, min, max);
                if (chosen != null) return chosen;
            }

            return base.OnSelectFusionMaterial(cards, min, max);
        }



        private IList<ClientCard> SelectSecondDivineTributes(IList<ClientCard> cards, int min, int max)
        {
            // NOTE: YGOPro can ask for tributes one-by-one (min=max=1 twice). If we clear context
            // after the first selection, base selection may "pad" into tributing the existing Divine.
            // Therefore we persist picks in _secondDivineTributesChosen until we have 2 total.

            if (cards == null || cards.Count == 0)
            {
                _secondDivineTributesChosen.Clear();
                _selectCtx = SelectCtx.None;
            _pendingSuperPoly = false;
            _superPolyChosenFusionId = 0;
                return null;
            }

            // Candidate pool: face-up Apophis monsters, NEVER allow the existing Divine as tribute.
            List<ClientCard> candidates = cards
                .Where(c => c != null && c.IsFaceup() && IsApophis(c) && c.Id != CardId.DivineSerpentApophis)
                .ToList();

            // Remove already-selected tributes (sequential prompts).
            candidates.RemoveAll(c => _secondDivineTributesChosen.Contains(c));

            if (candidates.Count == 0)
            {
                ODLog("[OD.Divine.Second]", "tribute pick failed: no legal candidates (min=" + min + ", max=" + max + ")");
                _secondDivineTributesChosen.Clear();
                _selectCtx = SelectCtx.None;
            _pendingSuperPoly = false;
            _superPolyChosenFusionId = 0;
                return null;
            }

            // Prefer to tribute the smallest-impact Apophis bodies first.
            int[] preferIds = new[]
            {
                CardId.ApophisTheSerpent,
                CardId.ApophisTheSwampDeity,
                CardId.AngelStatueAzurune
            };

            List<ClientCard> pick = new List<ClientCard>();

            // If the engine asks for 2 at once, pick 2 now; otherwise pick 1 per prompt.
            int needNow = (min == 2 && max == 2) ? 2 : 1;
            needNow = Math.Max(needNow, min);
            needNow = Math.Min(needNow, max);

            foreach (int id in preferIds)
            {
                if (pick.Count >= needNow) break;
                ClientCard c = candidates.FirstOrDefault(x => x != null && x.Id == id);
                if (c != null && !pick.Contains(c)) pick.Add(c);
            }

            foreach (ClientCard c in candidates)
            {
                if (pick.Count >= needNow) break;
                if (c != null && !pick.Contains(c)) pick.Add(c);
            }

            if (pick.Count < min)
            {
                // Don't call CheckSelectCount when we are short; it may pad with the existing Divine.
                ODLog("[OD.Divine.Second]", "tribute pick failed: short (picked=" + pick.Count + ", min=" + min + ")");
                _secondDivineTributesChosen.Clear();
                _selectCtx = SelectCtx.None;
            _pendingSuperPoly = false;
            _superPolyChosenFusionId = 0;
                return null;
            }

            // Commit these picks to the running selection.
            foreach (ClientCard c in pick)
            {
                if (c != null && !_secondDivineTributesChosen.Contains(c))
                    _secondDivineTributesChosen.Add(c);
            }

            int step = Math.Min(_secondDivineTributesChosen.Count, 2);
            ODLog("[OD.Divine.Second]",
                "tribute pick=" + string.Join(",", pick.Select(x => x.Id)) +
                " (min=" + min + ", max=" + max + ", step=" + step + "/2)");

            // Clear ctx only after we have selected 2 tributes total.
            if (_secondDivineTributesChosen.Count >= 2)
            {
                _secondDivineTributesChosen.Clear();
                _selectCtx = SelectCtx.None;
            _pendingSuperPoly = false;
            _superPolyChosenFusionId = 0;
            }

            return Util.CheckSelectCount(pick, cards, min, max);
        }

        // Also fix the existing SelectSecondDivineTributes method if it exists (remove duplicate)


        public override int OnSelectOption(IList<int> options)
        {
            // The Man with the Mark: always choose "Add to hand" (1190) when it's the current solving chain.
            ChainInfo currentSolvingChain = Duel.GetCurrentSolvingChainInfo();
            if (currentSolvingChain != null && currentSolvingChain.IsCode(CardId.TheManWithTheMark))
            {
                if (options.Contains(1190))
                    return options.IndexOf(1190);
            }
            return base.OnSelectOption(options);
        }

        // ------------------------------------------------------------
        // Battle overrides (Serket line)
        // ------------------------------------------------------------
        public override BattlePhaseAction OnSelectAttackTarget(ClientCard attacker, IList<ClientCard> defenders)
        {
            // Make Serket attack into bigger monsters when its battle-start destruction effect can convert the battle.
            if (attacker != null && attacker.IsCode(CardId.MercilessScorpionOfSerket) && attacker.IsFaceup() && !attacker.IsDisabled())
            {
                if (defenders != null && defenders.Count > 0)
                {
                    ClientCard best = defenders
                        .Where(d => d != null && d.IsFaceup())
                        .OrderByDescending(ScoreEnemyThreat)
                        .ThenByDescending(d => Math.Max(0, d.GetDefensePower()))
                        .FirstOrDefault();

                    if (best != null)
                    {
                        ODLog("[OD.Serket.Attack]", "force attack target=" + best.Id + " (atk=" + attacker.Attack + ", defPow=" + best.GetDefensePower() + ")");
                        return AI.Attack(attacker, best);
                    }
                }

                // No defenders -> direct is always fine.
                ODLog("[OD.Serket.Attack]", "direct");
                return AI.Attack(attacker, null);
            }

            return base.OnSelectAttackTarget(attacker, defenders);
        }

        public override ClientCard OnSelectAttacker(IList<ClientCard> attackers, IList<ClientCard> defenders)
        {
            // Prefer using Serket as the first attacker when it can pick off a dangerous monster.
            if (attackers != null)
            {
                ClientCard serket = attackers.FirstOrDefault(a =>
                    a != null && a.IsFaceup() && !a.IsDisabled() && a.IsCode(CardId.MercilessScorpionOfSerket));

                if (serket != null && defenders != null && defenders.Any(d => d != null && d.IsFaceup()))
                {
                    ODLog("[OD.Serket.Attacker]", "pick Serket first");
                    return serket;
                }
            }

            return base.OnSelectAttacker(attackers, defenders);
        }

        // ------------------------------------------------------------
        // Shared helpers
        // ------------------------------------------------------------
        private bool IsApophis(ClientCard c)
        {
            return c != null && c.HasSetcode(SetcodeApophis);
        }

        private bool IsSerket(ClientCard c)
        {
            return c != null && c.HasSetcode(SetcodeSerket);
        }

        private bool IsMyTurn()
        {
            return Duel.Player == 0;
        }

        private bool IsOppTurn()
        {
            return Duel.Player == 1;
        }

        private bool IsMainPhase1()
        {
            return Duel.Phase == DuelPhase.Main1;
        }

        private bool IsBattleEndStep()
        {
            // "At the end of the Battle Phase" activations are surfaced by the engine during DuelPhase.Battle (it only offers the prompt at Battle Phase end).
            return Duel.Phase == DuelPhase.Battle;
        }

        private int CountBotCardsOnField()
        {
            int count = 0;
            foreach (ClientCard m in Bot.MonsterZone)
                if (m != null) count++;
            // include field zone (index 5), ignore pend zones (6/7) for this deck
            foreach (ClientCard z in Bot.SpellZone.Take(6))
                if (z != null) count++;
            return count;
        }

        private int CountEnemyCardsOnField()
        {
            int count = 0;
            foreach (ClientCard m in Enemy.MonsterZone)
                if (m != null) count++;
            foreach (ClientCard z in Enemy.SpellZone.Take(6))
                if (z != null) count++;
            return count;
        }

        private bool BotControlsNoFaceupCards()
        {
            foreach (ClientCard m in Bot.MonsterZone)
                if (m != null && m.IsFaceup()) return false;
            foreach (ClientCard z in Bot.SpellZone.Take(6))
                if (z != null && z.IsFaceup()) return false;
            return true;
        }

        private bool BotControlsNoCardsAtAll()
        {
            return CountBotCardsOnField() == 0;
        }

        private int CountEnemyMonsters()
        {
            return Enemy.MonsterZone.Count(c => c != null && c.IsFaceup() && c.HasType(CardType.Monster));
        }

        private int CountEnemyBackrow()
        {
            return Enemy.SpellZone.Take(5).Count(c => c != null);
        }

        private int CountEnemyFaceupCards()
        {
            int cnt = 0;
            foreach (ClientCard c in Enemy.MonsterZone)
                if (c != null && c.IsFaceup()) cnt++;
            foreach (ClientCard c in Enemy.SpellZone.Take(5))
                if (c != null && c.IsFaceup()) cnt++;
            if (Enemy.SpellZone[5] != null && Enemy.SpellZone[5].IsFaceup()) cnt++;
            return cnt;
        }

        private bool BotHasFaceupContinuousTrap(int id)
        {
            foreach (ClientCard c in Bot.SpellZone.Take(5))
                if (c != null && c.IsFaceup() && c.Id == id) return true;
            foreach (ClientCard c in Bot.MonsterZone)
                if (c != null && c.IsFaceup() && c.Id == id) return true;
            return false;
        }

        private bool BotHasFacedownInSpellZone(int id)
        {
            foreach (ClientCard c in Bot.SpellZone.Take(5))
                if (c != null && c.IsFacedown() && c.Id == id) return true;
            return false;
        }

        private bool EnemyCommittedTwoThreats()
        {
            int m = CountEnemyMonsters();
            int st = CountEnemyBackrow();
            if (m >= 2) return true;
            if (m >= 1 && st >= 1) return true;

            // Also respond early to a single clearly problematic face-up card.
            if (Util.GetProblematicEnemyMonster(0, true) != null) return true;
            if (Util.GetBestEnemySpell(true) != null) return true;
            return false;
        }

        private bool IsMainPhase()
        {
            return Duel.Phase == DuelPhase.Main1 || Duel.Phase == DuelPhase.Main2;
        }

        /// <summary>
        /// Check remaining copies of a card in our deck/extra, based on DeckCountTable parity.
        /// Returns 0 if the card isn't tracked.
        /// </summary>
        private int CheckRemainInDeck(int id)
        {
            for (int count = 1; count < 4; ++count)
            {
                List<int> list;
                if (DeckCountTable.TryGetValue(count, out list) && list.Contains(id))
                    return Bot.GetCardCountInDeck(id);
            }
            return 0;
        }

        private int CheckRemainInDeck(params int[] ids)
        {
            int sum = 0;
            foreach (int id in ids)
                sum += CheckRemainInDeck(id);
            return sum;
        }

        private bool HasFreeSpellZone()
        {
            return Bot.SpellZone.Take(5).Any(z => z == null);
        }

        private int CountFreeSpellZone()
        {
            int free = 0;
            for (int i = 0; i < 5; i++)
                if (Bot.SpellZone[i] == null) free++;
            return free;
        }



        private int CountDistinctApophisTrapsInGrave()
        {
            HashSet<int> ids = new HashSet<int>();
            foreach (ClientCard c in Bot.Graveyard)
            {
                if (c == null) continue;
                if (c.Id == CardId.ApophisTheSerpent
                    || c.Id == CardId.ApophisTheSwampDeity
                    || c.Id == CardId.AngelStatueAzurune)
                {
                    ids.Add(c.Id);
                }
            }
            return ids.Count;
        }

        // How many S/T slots we want to preserve for Divine Serpent Apophis set-back effect this turn.
        // Prevents generic SpellSet / other sets from reducing Divine's max set count.
        private int GetDivineSetBackNeedSlots()
        {
            if (!IsMyTurn()) return 0;
            if (_didDivineSetBack) return 0;

            bool divineOnField = Bot.MonsterZone.Any(c => c != null && c.IsFaceup() && c.Id == CardId.DivineSerpentApophis);
            if (!divineOnField) return 0;

            int distinct = CountDistinctApophisTrapsInGrave();
            if (distinct <= 0) return 0;

            return Math.Min(3, distinct);
        }

        private string DumpBotSpellZone()
        {
            List<string> parts = new List<string>();
            for (int i = 0; i < 6; i++)
            {
                ClientCard z = Bot.SpellZone[i];
                string slot = (i == 5) ? "FZ" : i.ToString();

                if (z == null)
                {
                    parts.Add($"{slot}=.");
                    continue;
                }

                string face = z.IsFacedown() ? "FD" : "FU";
                parts.Add($"{slot}={z.Id}:{face}");
            }
            return string.Join(" ", parts);
        }
        private bool NeedReserveSpellZoneForRabbitSet()
        {
            if (!IsMyTurn()) return false;
            if (_didRabbitSet) return false;

            // If Rabbit is already on field, or we can reasonably make it this turn, reserve a slot.
            bool rabbitOnField = Bot.HasInMonstersZone(CardId.SilhouhatteRabbit);

            bool canMakeRabbit = false;
            if (!rabbitOnField && IsMainPhase())
            {
                ClientCard man = Bot.MonsterZone.FirstOrDefault(c => c != null && c.IsFaceup() && c.Id == CardId.TheManWithTheMark);
                ClientCard serket = Bot.MonsterZone.FirstOrDefault(c => c != null && c.IsFaceup() && c.Id == CardId.MercilessScorpionOfSerket);
                if (man != null && serket != null && man.HasType(CardType.Effect) && serket.HasType(CardType.Effect))
                    canMakeRabbit = true;
            }

            if (!rabbitOnField && !canMakeRabbit) return false;

            // Also reserve if we plan to use Defense of the Temple fusion
            bool planDefenseFusion = NeedReserveZoneForDefenseFusion();

            return planDefenseFusion || canMakeRabbit || rabbitOnField;
        }

        private bool EnemyHasAnyCardOnField()
        {
            foreach (ClientCard c in Enemy.MonsterZone)
                if (c != null) return true;
            // Enemy.SpellZone includes zones 0-4 + Field zone [5].
            foreach (ClientCard c in Enemy.SpellZone)
                if (c != null) return true;
            return false;
        }

        private bool IsEnemyFieldCard(ClientCard c)
        {
            if (c == null) return false;

            // Normal case
            if (c.Controller == 1) return true;

            // Robust case: prompt sometimes gives odd controller values; zone membership is more reliable.
            if (Enemy.MonsterZone.Any(z => z == c)) return true;
            if (Enemy.SpellZone.Any(z => z == c)) return true;

            return false;
        }


        private bool IsMyFieldCard(ClientCard c)
        {
            if (c == null) return false;

            // Normal case
            if (c.Controller == 0) return true;

            // Robust case: prompt sometimes gives odd controller values; zone membership is more reliable.
            if (Bot.MonsterZone.Any(z => z == c)) return true;
            if (Bot.SpellZone.Any(z => z == c)) return true;

            return false;
        }

        private bool NeedReserveZoneForDefenseFusion()
        {
            if (!IsMyTurn()) return false;

            // Need zone for Defense activation
            bool hasDefenseInHand = Bot.HasInHand(CardId.DefenseOfTheTemple);
            bool hasSerket = Bot.MonsterZone.Any(c => c != null && c.IsFaceup() && c.Id == CardId.MercilessScorpionOfSerket);

            return hasDefenseInHand && hasSerket;
        }

        private bool WantDefenseFusionRemovalNow()
        {
            if (!IsMyTurn() || !IsMainPhase()) return false;

            // Need the Spell available in hand and an open S/T zone to activate it.
            if (!Bot.HasInHand(CardId.DefenseOfTheTemple)) return false;
            if (CountFreeSpellZone() <= 0) return false;

            // Need the Fusion target available.
            if (!Bot.ExtraDeck.Any(c => c != null && c.Id == CardId.DivineScorpionBeastOfSerket)) return false;
            if (Bot.HasInMonstersZone(CardId.DivineScorpionBeastOfSerket)) return false;

            ClientCard serket = Bot.MonsterZone.FirstOrDefault(c => c != null && c.IsFaceup() && c.Id == CardId.MercilessScorpionOfSerket);
            if (serket == null) return false;

            // Need a secondary material (prefer opponent <=2500 so we preserve our board).
            bool haveOppMat = Enemy.MonsterZone.Any(c => c != null && c.IsFaceup() && c.IsMonster() && c.Attack <= 2500);
            bool haveOwnMat = Bot.MonsterZone.Any(c => c != null && c.IsFaceup() && c.IsMonster() && c != serket);

            if (!haveOppMat && !haveOwnMat) return false;

            // Only hold Rabbit when opponent presents a real threat we want to answer immediately.
            return EnemyHasDangerousCardForSP();
        }

        private string GetCardKey(ClientCard c)
        {
            if (c == null) return string.Empty;

            int ctrl = IsEnemyFieldCard(c) ? 1 : 0;

            // If the engine provides stable zone info, use it (best de-dup).
            bool zoneKnown =
                (c.Location == CardLocation.MonsterZone || c.Location == CardLocation.SpellZone) &&
                c.Sequence >= 0 && c.Sequence <= 6;

            if (zoneKnown)
                return $"{ctrl}:{(int)c.Location}:{c.Sequence}";

            // Fallback: use card id only (better than mismatched Location/Sequence)
            return $"{ctrl}:id:{c.Id}";
        }

        private bool IsKeyMonsterToKeep(ClientCard c)
        {
            if (c == null) return false;

            // Do not throw away key engines/bosses as material if avoidable.
            return c.Id == CardId.DivineSerpentApophis
                || c.Id == CardId.SilhouhatteRabbit
                || c.Id == CardId.DivineScorpionBeastOfSerket
                || c.Id == CardId.TheManWithTheMark
                || c.Id == CardId.AnubisTheLastJudge;
        }

        private bool ShouldHoldTrapMonsterAfterCalledBy()
        {
            // If we just chained Called by the Grave to a handtrap, don't "pile on" with extra trap-monster chains.
            // This prevents wasteful chains like: enemy Ash -> our Called By -> our Deity/Serpent/Statue.
            if (Duel.CurrentChain == null || Duel.CurrentChain.Count < 2) return false;

            ClientCard last = Util.GetLastChainCard();
            if (last == null || !last.IsCode(CardId.CalledByTheGrave)) return false;
            if (Duel.LastChainPlayer != 0) return false;

            // Any common handtrap in the chain -> hold.
            bool handtrapInChain =
                Util.ChainContainsCard(CardId.AshBlossomJoyousSpring) ||
                Util.ChainContainsCard(CardId.DrollLockBird) ||
                Util.ChainContainsCard(CardId.MaxxC) ||
                Util.ChainContainsCard(CardId.NibiruThePrimalBeing) ||
                Util.ChainContainsCard(CardId.RetaliatingC);

            // Sometimes the engine hides IDs; still hold to avoid pointless chain stacking.
            bool unknownInChain = Duel.CurrentChain.Any(cc => cc != null && cc.Id == 0);

            return handtrapInChain || unknownInChain;
        }

        private bool IsSTInEnemyImpermDangerColumn(ClientCard c)
        {
            if (c == null) return false;
            if (c.Location != CardLocation.SpellZone) return false;
            int seq = c.Sequence;
            if (seq < 0 || seq > 4) return false;
            return _impermDangerColumns.Contains(seq);
        }

        private bool HasStatueFollowupTrapMonsterThisTurn()
        {
            // Statue pops only trigger when another Trap is Special Summoned to the Monster Zone.
            // Require at least one other facedown Trap-Monster source available so flipping Statue isn't "dead".
            int[] followupIds = new[]
            {
                CardId.ApophisTheSerpent,
                CardId.ApophisTheSwampDeity,
                CardId.AngelStatueAzurune,
                CardId.DivineSerpentApophis,
            };

            foreach (var z in Bot.SpellZone.Take(5))
            {
                if (z == null) continue;
                if (!z.IsFacedown()) continue;
                if (z.Id == CardId.StatueOfAnguishPattern) continue;
                if (followupIds.Contains(z.Id))
                    return true;
            }

            return false;
        }

        private int CountEnemyPopTargets(Func<ClientCard, bool> filter)
        {
            int count = 0;

            foreach (var c in Enemy.MonsterZone)
            {
                if (c == null) continue;
                if (!filter(c)) continue;
                count++;
            }

            foreach (var c in Enemy.SpellZone)
            {
                if (c == null) continue;
                if (!filter(c)) continue;
                count++;
            }

            return count;
        }

        private bool TryCommitPopBudget(Func<ClientCard, bool> filter)
        {
            // First pop always allowed if we haven't committed one yet
            if (_chainPopCommitCount == 0)
            {
                _chainPopCommitCount = 1;
                return true;
            }

            // Second pop only if opponent committed 2+ threats AND at least 2 targets exist
            if (_chainPopCommitCount == 1)
            {
                if (!EnemyCommittedTwoThreats()) return false;
                if (CountEnemyPopTargets(filter) < 2) return false;

                _chainPopCommitCount = 2;
                return true;
            }

            // 2+ already committed this chain
            return false;
        }

        private bool HasFreeMonsterZone()
        {
            return Bot.MonsterZone.Any(z => z == null);
        }

        // Treasures in Field Zone counts as "Temple" by name for condition checks.
        private bool HasTempleNameOnline()
        {
            ClientCard fz = Bot.SpellZone[5];
            if (fz != null && fz.IsFaceup() && fz.Id == CardId.TreasuresOfTheKings)
                return true;

            // Real Temple face-up in S/T zone.
            foreach (ClientCard c in Bot.SpellZone.Take(5))
            {
                if (c != null && c.IsFaceup() && c.Id == CardId.TempleOfTheKings)
                    return true;
            }
            return false;
        }

        // Only real Temple of the Kings face-up in S/T zone enables "activate trap the turn it was set".
        private bool HasTempleEffectOnline()
        {
            foreach (ClientCard c in Bot.SpellZone.Take(5))
            {
                if (c != null && c.IsFaceup() && c.Id == CardId.TempleOfTheKings)
                    return true;
            }
            return false;
        }

        private bool IsTreasuresOnline()
        {
            ClientCard fz = Bot.SpellZone[5];
            return fz != null && fz.IsFaceup() && fz.Id == CardId.TreasuresOfTheKings;
        }

        private int CountFacedownOnField_Bot()
        {
            int cnt = 0;
            foreach (ClientCard c in Bot.MonsterZone)
                if (c != null && c.IsFacedown()) cnt++;
            foreach (ClientCard c in Bot.SpellZone.Take(5))
                if (c != null && c.IsFacedown()) cnt++;
            return cnt;
        }

        private bool HasAnyTrapInGrave_Bot()
        {
            return Bot.Graveyard.Any(c => c != null && c.HasType(CardType.Trap));
        }

        private bool WasSetThisTurn(ClientCard c)
        {
            return c != null && _botSetThisTurn.Contains(c);
        }
        // Serpent set preference: Deity > Azurune.
        // NOTE: We do not scan Bot.Deck here (hidden deck => placeholders). The prompt list is the truth.
        private int ChooseSerpentSetIdFromDeck()
        {
            bool deityOnField =
                Bot.SpellZone.Take(5).Any(c => c != null && c.Id == CardId.ApophisTheSwampDeity) ||
                Bot.MonsterZone.Any(c => c != null && c.Id == CardId.ApophisTheSwampDeity);

            bool azuruneOnField =
                Bot.SpellZone.Take(5).Any(c => c != null && c.Id == CardId.AngelStatueAzurune) ||
                Bot.MonsterZone.Any(c => c != null && c.Id == CardId.AngelStatueAzurune);

            if (!deityOnField) return CardId.ApophisTheSwampDeity;
            if (!azuruneOnField) return CardId.AngelStatueAzurune;

            return CardId.ApophisTheSwampDeity;
        }


        // ------------------------------------------------------------
        // Batch 1: Anubis discard -> search Man
        // ------------------------------------------------------------
        private bool AnubisDiscardSearchMan()
        {
            if (!IsMainPhase()) return false;
            if (_didAnubisDiscard) return false;
            if (Card.Location != CardLocation.Hand) return false;

            // Starter policy: only use Anubis when we don't already have Man and we haven't committed other starters.
            if (HasCommittedStarterThisTurn()) return false;

            bool alreadyHaveMan = Bot.HasInHand(CardId.TheManWithTheMark)
                || Bot.HasInMonstersZone(CardId.TheManWithTheMark)
                || Bot.Graveyard.Any(c => c != null && c.Id == CardId.TheManWithTheMark);
            if (alreadyHaveMan) return false;

            AI.SelectCard(CardId.TheManWithTheMark);
            _didAnubisDiscard = true;
            ODLog("[OD.Anubis]", "discard -> add Man");
            return true;
        }

        // ------------------------------------------------------------
        // Batch 4: Anubis recovery - Special Summon from GY (place 2 Traps/Temple back)
        // ------------------------------------------------------------
        private bool AnubisSpecialSummonFromGrave()
        {
            if (Card == null || Card.Id != CardId.AnubisTheLastJudge) return false;
            if (!IsMyTurn() || !IsMainPhase()) return false;
            if (_didAnubisSpecialSummonThisTurn) return false;
            RefreshEvenlyPlan();
            if (_planEvenlyThisTurn) return false;
            if (Card.Location != CardLocation.Grave) return false;

            // Require engine online (avoid spending 2 traps too early without payoff).
            bool engineOnline = HasTempleEffectOnline()
                || Bot.MonsterZone.Any(c => c != null && c.IsFaceup() && c.Id == CardId.DivineSerpentApophis)
                || _didDivineSummon || _didSummonSecondDivine;
            if (!engineOnline) return false;

            if (!HasFreeMonsterZone()) return false;

            // GY condition: 3+ different names among Temple + Traps.
            var pool = Bot.Graveyard
                .Where(c => c != null && (c.Id == CardId.TempleOfTheKings || c.HasType(CardType.Trap)))
                .ToList();

            int distinct = pool.Select(c => c.Id).Distinct().Count();
            if (distinct < 3) return false;
            if (pool.Count < 2) return false;

            _didAnubisSpecialSummonThisTurn = true;
            _pendingAnubisSummon = true;
            _selectCtx = SelectCtx.AnubisSummonCostToDeck;

            ODLog("[OD.Anubis.SS]", "attempt SS from GY (distinctPool=" + distinct + ")");
            return true;
        }

        private IList<ClientCard> SelectAnubisSummonCostToDeck(IList<ClientCard> cards, int min, int max)
        {
            if (cards == null || cards.Count == 0) return null;

            // Offered list is the truth; pick 2 different names with lowest future value.
            var pool = cards.Where(c => c != null).ToList();

            Func<ClientCard, int> Score = c =>
            {
                if (c.Id == CardId.TempleOfTheKings) return 9000;

                // Keep core Apophis/Statue traps in GY for Divine set-back loops.
                if (c.Id == CardId.ApophisTheSerpent
                    || c.Id == CardId.ApophisTheSwampDeity
                    || c.Id == CardId.AngelStatueAzurune
                    || c.Id == CardId.StatueOfAnguishPattern)
                    return 10000;

                // Low-impact staples / spent traps: OK to recycle.
                if (c.Id == CardId.InfiniteImpermanence
                    || c.Id == CardId.EvenlyMatched
                    || c.Id == CardId.DominusPurge
                    || c.Id == CardId.VerdictOfAnubis)
                    return 1000;

                return 2000;
            };

            var ordered = pool.OrderBy(Score).ThenBy(c => c.Id).ToList();

            var pick = new List<ClientCard>();
            foreach (var c in ordered)
            {
                if (pick.Count >= 2) break;
                if (pick.Any(x => x.Id == c.Id)) continue; // different names (per Anubis summon rule)
                pick.Add(c);
            }

            // If prompt forces duplicates, pad.
            foreach (var c in ordered)
            {
                if (pick.Count >= 2) break;
                if (pick.Contains(c)) continue;
                pick.Add(c);
            }

            ODLog("[OD.Anubis.SS]", "cost pick=" + string.Join(",", pick.Select(x => x.Id)) +
                  " (min=" + min + ", max=" + max + ")");

            _pendingAnubisSummon = false;
            _selectCtx = SelectCtx.None;

            return Util.CheckSelectCount(pick, cards, min, max);
        }

        private bool HasCommittedStarterThisTurn()
        {
            // If we've advanced the main line already, don't discard Anubis late.
            return _didManSearch
                || _didTreasuresActivate
                || _didTreasuresSearchSerket
                || _didSerketSummon
                || _didSerketSearch
                || _didTempleActivate
                || _didFlipSerpent
                || _didFlipSwampDeity
                || _didRabbitSummon
                || _didDivineSummon;
        }

        // ------------------------------------------------------------
        // Batch 1: Normal summon Man + search priorities
        // ------------------------------------------------------------
        private bool TheManWithTheMarkSummon()
        {
            if (!IsMainPhase()) return false;

            _normalSummonUsedThisTurn = true;
            ODLog("[OD.NS.Man]", "normal summon");
            return true;
        }

        private bool TheManWithTheMarkSearch()
        {
            if (!IsMainPhase()) return false;
            if (_didManSearch) return false;
            if (Card.Location != CardLocation.MonsterZone) return false;

            bool treasuresActive = IsTreasuresOnline();

            bool needTreasures = !Bot.HasInHand(CardId.TreasuresOfTheKings)
                && (Bot.SpellZone[5] == null || Bot.SpellZone[5].Id != CardId.TreasuresOfTheKings);

            // "Temple effect" = real Temple face-up (enables set-turn Trap activation).
            bool needTemple = !Bot.HasInHand(CardId.TempleOfTheKings)
                && !HasTempleEffectOnline()
                && !Bot.SpellZone.Take(5).Any(c => c != null && c.Id == CardId.TempleOfTheKings);

            bool needDefense = !Bot.HasInHand(CardId.DefenseOfTheTemple)
                && !Bot.SpellZone.Take(5).Any(c => c != null && c.Id == CardId.DefenseOfTheTemple);

            bool needSerket = !Bot.HasInHand(CardId.MercilessScorpionOfSerket)
                && !Bot.HasInMonstersZone(CardId.MercilessScorpionOfSerket);

            // If we have facedown Traps we set this turn (Treasures set-from-Deck, or our own hand sets),
            // Temple is the highest leverage search because it unlocks immediate trap activation.
            bool haveSetTurnTrap = Bot.SpellZone.Take(5).Any(z =>
                z != null
                && z.IsFacedown()
                && z.HasType(CardType.Trap)
                && WasSetThisTurn(z));

            bool wantTempleNow = needTemple
                && HasFreeSpellZone()
                && (haveSetTurnTrap || treasuresActive || _didTreasuresActivate);

            // Priority (capability-driven):
            //  1) Missing Treasures engine.
            //  2) Missing Temple effect AND we have set-turn Traps to unlock.
            //  3) Missing Serket extender (Treasure-name online enables its SS line).
            //  4) Missing Defense only when Serket is already available (otherwise it's dead).
            if (needTreasures)
            {
                AI.SelectCard(CardId.TreasuresOfTheKings);
                ODLog("[OD.Man.Search]", "add Treasures (engine missing)");
            }
            else if (wantTempleNow)
            {
                AI.SelectCard(CardId.TempleOfTheKings);
                ODLog("[OD.Man.Search]", "add Temple (enable set-turn traps)");
            }
            else if (needSerket)
            {
                AI.SelectCard(CardId.MercilessScorpionOfSerket);
                ODLog("[OD.Man.Search]", "add Serket (missing extender)");
            }
            else if (needDefense && (Bot.HasInHand(CardId.MercilessScorpionOfSerket) || Bot.HasInMonstersZone(CardId.MercilessScorpionOfSerket)))
            {
                AI.SelectCard(CardId.DefenseOfTheTemple);
                ODLog("[OD.Man.Search]", "add Defense (Serket available)");
            }
            else if (needTemple)
            {
                // Still useful as a backup even if we can't leverage it immediately.
                AI.SelectCard(CardId.TempleOfTheKings);
                ODLog("[OD.Man.Search]", "add Temple (backup)");
            }
            else if (needDefense)
            {
                AI.SelectCard(CardId.DefenseOfTheTemple);
                ODLog("[OD.Man.Search]", "add Defense (backup)");
            }
            else
            {
                ODLog("[OD.Man.Search]", "no priority target");
                return false;
            }

            _didManSearch = true;
            return true;
        }

        // ------------------------------------------------------------
        // Batch 1: Treasures activation (always YES to set from Deck, prefer Serpent)
        // ------------------------------------------------------------ (always YES to set from Deck, prefer Serpent)
        // ------------------------------------------------------------
        private bool TreasuresActivate()
        {
            if (!IsMainPhase()) return false;
            if (_didTreasuresActivate) return false;

            // Activation from hand only.
            if (Card.Location != CardLocation.Hand) return false;

            // Field zone occupied.
            if (Bot.SpellZone[5] != null) return false;

            _askYesNoTreasuresSet = true;
            AI.SelectCard(CardId.ApophisTheSerpent);

            _didTreasuresActivate = true;
            ODLog("[OD.Treasures.Act]", "activate -> set Serpent from Deck");
            return true;
        }

        // ------------------------------------------------------------
        // Batch 1: Deterministic SpellSet to enable Treasures search
        // IMPORTANT: SpellSet handlers are evaluated per candidate card (global Card).
        // Return true ONLY when Card is the chosen set card.
        // ------------------------------------------------------------
        private bool SetForTreasuresSearch()
        {
            if (!IsMainPhase()) return false;
            if (_didSetForTreasures) return false;
            if (_didTreasuresSearchSerket) return false;

            // Only when Treasures is face-up in Field Zone.
            if (!IsTreasuresOnline()) return false;

            // If search already enabled by trap in GY or 2 facedown, no need.
            if (HasAnyTrapInGrave_Bot()) return false;
            if (CountFacedownOnField_Bot() >= 2) return false;

            if (!HasFreeSpellZone()) return false;

            // Choose once, then return true only for that Card.
            if (_setForTreasuresId == 0)
            {
                _setForTreasuresId = ChooseSetForTreasuresId();
                if (_setForTreasuresId == 0) return false;
                ODLog("[OD.SetForTreasures]", "chosenId=" + _setForTreasuresId);
            }

            if (Card == null || Card.Id != _setForTreasuresId)
                return false;

            // Consume.
            _didSetForTreasures = true;
            int id = _setForTreasuresId;
            _setForTreasuresId = 0;

            // NEW: mark if we set a trap
            ClientCard card = Card;
            if (card != null && card.HasType(CardType.Trap))
            {
                _usedTrapForTreasures = true;
            }

            ODLog("[OD.SetForTreasures]", "set " + id + " (isTrap=" + _usedTrapForTreasures + ")");
            return true;
        }

        // C2: Monster set for Treasures condition (fallback)
        // NOTE: MonsterSet handlers are evaluated per candidate card (global Card).
        // Return true ONLY when Card is the chosen set monster.
        private bool MonsterSetForTreasures()
        {
            if (!IsMainPhase()) return false;
            if (_didSetMonsterForTreasures) return false;
            if (_didTreasuresSearchSerket) return false;
            if (!IsTreasuresOnline()) return false;

            // Only need to set a monster if we don't have enough facedown cards / trap in GY.
            if (HasAnyTrapInGrave_Bot()) return false;
            if (CountFacedownOnField_Bot() >= 2) return false;

            if (!HasFreeMonsterZone()) return false;

            if (!HasNormalSummonAvailable()) return false;

            // Never spend the normal summon on a filler set if Man is already available to be normal summoned.
            if (Bot.HasInHand(CardId.TheManWithTheMark) && HasNormalSummonAvailable())
                return false;

            // Choose once, then return true only for that Card.
            if (_setMonsterForTreasuresId == 0)
            {
                _setMonsterForTreasuresId = ChooseMonsterSetForTreasuresId();
                if (_setMonsterForTreasuresId == 0) return false;
                ODLog("[OD.MonsterSetForTreasures]", "chosenId=" + _setMonsterForTreasuresId);
            }

            if (Card == null || Card.Id != _setMonsterForTreasuresId)
                return false;

            // Validation (safety): can only set Level 4 or lower monsters from hand.
            if (!Card.IsMonster() || Card.Level > 4) return false;
            if (Card.Location != CardLocation.Hand) return false;

            // Don't set key engine pieces.
            if (Card.Id == CardId.TheManWithTheMark || Card.Id == CardId.MercilessScorpionOfSerket)
                return false;

            _didSetMonsterForTreasures = true;
            _normalSummonUsedThisTurn = true;

            int id = _setMonsterForTreasuresId;
            _setMonsterForTreasuresId = 0;

            ODLog("[OD.MonsterSetForTreasures]", "set " + id + " (fallback)");
            return true;
        }

        private int ChooseSetForTreasuresId()
        {
            // Prefer a trap that we are likely to flip/convert into a body this turn (Temple online),
            // so it doesn't permanently consume S/T space and can later be set back by Divine Serpent.
            // Setting a sticky Counter Trap like Verdict here often starves Divine's set-back.

            int[] prefer = new[]
            {
                // Apophis traps (best): become monsters, then go to GY, then get set back.
                CardId.ApophisTheSwampDeity,
                CardId.AngelStatueAzurune,
                CardId.ApophisTheSerpent,

                // Next best: traps we might actively use this turn.
                CardId.DominusPurge,
                CardId.InfiniteImpermanence,

                // Lowest priority: sticky backrow that tends to stay set.
                CardId.VerdictOfAnubis,
                CardId.EvenlyMatched
            };

            foreach (int id in prefer)
                if (Bot.HasInHand(id)) return id;

            return 0;
        }

        // PURPOSE: coarse normal-summon availability tracking to prevent wasting the Normal Summon on handtraps/fillers.
        // TRUSTS: Odion only consumes the normal summon via explicit handlers:
        //   - TheManWithTheMarkSummon
        //   - SphereModeSummon
        //   - MonsterSetForTreasures
        // RESET: OnNewTurn
        private bool HasNormalSummonAvailable()
        {
            return !_normalSummonUsedThisTurn;
        }

        private int ChooseMonsterSetForTreasuresId()
        {
            // Prefer setting the least-valuable monster first. Handtraps are last resort.
            var candidates = Bot.Hand
                .Where(c => c != null && c.IsMonster() && c.Level <= 4 && c.Location == CardLocation.Hand)
                .Where(c => c.Id != CardId.TheManWithTheMark && c.Id != CardId.MercilessScorpionOfSerket)
                .OrderBy(c => ScoreMonsterSetForTreasures(c))
                .ThenBy(c => c.Attack)
                .ToList();

            if (candidates.Count == 0)
                return 0;

            return candidates[0].Id;
        }

        private int ScoreMonsterSetForTreasures(ClientCard c)
        {
            // Higher score = keep in hand.
            if (c == null) return 999;

            // Preserve handtraps as much as possible (Ash should be the LAST option).
            if (c.Id == CardId.RetaliatingC) return 20;
            if (c.Id == CardId.DrollLockBird) return 30;
            if (c.Id == CardId.AshBlossomJoyousSpring) return 40;
            if (c.Id == CardId.MaxxC) return 50;

            // Unknown / non-handtrap monsters: OK to set first.
            return 0;
        }


        private bool AngelStatueAzuruneActivateOppTurn()
        {
            if (!IsOppTurn() || !IsMainPhase()) return false;
            if (_didFlipAzurune) return false;

            if (ShouldHoldTrapMonsterAfterCalledBy())
            {
                ODLog("[OD.Azurune.Disrupt]", "HOLD: Called By already chained vs handtrap");
                return false;
            }

            if (Card == null || Card.Location != CardLocation.SpellZone || !Card.IsFacedown())
                return false;

            if (!HasFreeMonsterZone()) return false;
            if (!EnemyCommittedTwoThreats()) return false;
            if (IsSTInEnemyImpermDangerColumn(Card)) return false;

            _didFlipAzurune = true;
            ODLog("[OD.Azurune.Disrupt]", "activate Azurune (opp turn policy)");
            return true;
        }

        private bool AngelStatueAzuruneNegateSpecialSummon()
        {
            if (!IsOppTurn()) return false;
            if (_didAzuruneNegateThisTurn) return false;

            if (Card == null || Card.Location != CardLocation.MonsterZone || !Card.IsFaceup())
                return false;

            if (DefaultCheckWhetherCardIsNegated(Card)) return false;

            if (ActivateDescription != Util.GetStringId(CardId.AngelStatueAzurune, 0))
                return false;

            _didAzuruneNegateThisTurn = true;
            _selectCtx = SelectCtx.AzuruneNegateCost;
            ODLog("[OD.Azurune.Negate]", "activate");
            return true;
        }

        private bool AngelStatueAzuruneBattlePunish()
        {
            if (Card == null) return false;
            if (ActivateDescription != Util.GetStringId(CardId.AngelStatueAzurune, 1))
                return false;

            ODLog("[OD.Azurune.Battle]", "activate battle revenge");
            return true;
        }

        private IList<ClientCard> SelectAzuruneNegateCost(IList<ClientCard> cards, int min, int max)
        {
            if (cards == null || cards.Count == 0) return null;

            List<ClientCard> pool = cards.Where(c => c != null).ToList();
            pool.Sort((a, b) => ScoreAzuruneNegateCost(a).CompareTo(ScoreAzuruneNegateCost(b)));

            ClientCard pick = pool[0];
            return Util.CheckSelectCount(new List<ClientCard> { pick }, cards, min, max);
        }

        private int ScoreAzuruneNegateCost(ClientCard c)
        {
            if (c == null) return 999;
            if (c.Id == CardId.ApophisTheSerpent) return 0;
            if (c.Id == CardId.StatueOfAnguishPattern) return 2;
            if (c.Id == CardId.ApophisTheSwampDeity) return 3;
            if (c.Id == CardId.AngelStatueAzurune) return 4;
            return 10;
        }

        // ------------------------------------------------------------
        // Treasures ignition search (Temple-mention monster)
        // Enabled by (2 facedown on field) OR (a Trap in GY)
        // Priority: The Man with the Mark -> Merciless Scorpion of Serket
        // ------------------------------------------------------------
        private bool TreasuresSearchSerket()
        {
            if (!IsMainPhase()) return false;
            if (_didTreasuresSearchSerket) return false;

            // Only the ignition while Treasures is face-up in Field Zone.
            if (Card.Location != CardLocation.SpellZone || Card.Sequence != 5) return false;
            if (!Card.IsFaceup()) return false;

            bool enabled = (CountFacedownOnField_Bot() >= 2) || HasAnyTrapInGrave_Bot();
            if (!enabled) return false;

            // C1 combo: If we have trap in hand that we set this turn, prefer Man
            bool trapWasSet = _usedTrapForTreasures;
            bool haveMan = Bot.HasInHand(CardId.TheManWithTheMark)
                || Bot.HasInMonstersZone(CardId.TheManWithTheMark)
                || Bot.Graveyard.Any(c => c != null && c.Id == CardId.TheManWithTheMark);
            bool haveSerket = Bot.HasInHand(CardId.MercilessScorpionOfSerket)
                || Bot.HasInMonstersZone(CardId.MercilessScorpionOfSerket);

            int targetId = 0;

            // If we already spent the normal summon this turn, prefer Serket (can advance immediately) over Man.
            bool canNormalSummon = HasNormalSummonAvailable();
            if (!canNormalSummon && !haveSerket)
            {
                targetId = CardId.MercilessScorpionOfSerket;
            }
            // Combo logic: if trap was set (C1), we likely want Man
            else if (trapWasSet && !haveMan)
            {
                targetId = CardId.TheManWithTheMark;
            }
            // Standard priority: Man first, then Serket
            else if (!haveMan)
            {
                targetId = CardId.TheManWithTheMark;
            }
            else if (!haveSerket)
            {
                targetId = CardId.MercilessScorpionOfSerket;
            }
            else
            {
                // Both already available, no need to search
                return false;
            }

            AI.SelectCard(targetId);
            _didTreasuresSearchSerket = true;
            ODLog("[OD.Treasures.Search]", "add " + targetId + " (trapWasSet=" + trapWasSet + ")");
            return true;
        }

        // ------------------------------------------------------------
        // Batch 1: Serket SS (banish cost selection via OnSelectCard)
        // ------------------------------------------------------------
        private bool SerketSpecialSummon()
        {
            if (!IsMainPhase()) return false;
            if (_didSerketSummon) return false;

            // Needs Temple by name online (Treasures in FZ qualifies).
            if (!HasTempleNameOnline()) return false;

            // Needs monster zone space.
            if (!HasFreeMonsterZone()) return false;

            _selectCtx = SelectCtx.SerketBanishCost;
            _didSerketSummon = true;
            ODLog("[OD.Serket.SS]", "special summon (select cost)");
            return true;
        }

        private IList<ClientCard> SelectSerketBanishCost(IList<ClientCard> cards, int min, int max)
        {
            // Prefer banishing from Deck if present in candidate list.
            int[] prefer = new[]
            {
                CardId.NibiruThePrimalBeing,
                CardId.TheWingedDragonOfRaSphereMode,
                CardId.AnubisTheLastJudge,
            };

            // 1) try Deck first
            foreach (int id in prefer)
            {
                ClientCard c = cards.FirstOrDefault(x => x != null && x.Id == id && x.Location == CardLocation.Deck);
                if (c != null)
                {
                    ODLog("[OD.Serket.SS]", "banish(deck) " + id);
                    return Util.CheckSelectCount(new List<ClientCard> { c }, cards, min, max);
                }
            }

            // 2) then Hand
            foreach (int id in prefer)
            {
                ClientCard c = cards.FirstOrDefault(x => x != null && x.Id == id && x.Location == CardLocation.Hand);
                if (c != null)
                {
                    ODLog("[OD.Serket.SS]", "banish(hand) " + id);
                    return Util.CheckSelectCount(new List<ClientCard> { c }, cards, min, max);
                }
            }

            // 3) fallback: first legal
            if (cards != null && cards.Count > 0)
            {
                ODLog("[OD.Serket.SS]", "banish(fallback) " + cards[0].Id);
                return Util.CheckSelectCount(new List<ClientCard> { cards[0] }, cards, min, max);
            }

            return null;
        }

        // ------------------------------------------------------------
        // Batch 1: Serket search priority (Temple first if effect not online)
        // ------------------------------------------------------------
        private bool SerketSearchTemple()
        {
            if (!IsMainPhase()) return false;
            if (_didSerketSearch) return false;

            // Effect from monster zone only.
            if (Card.Location != CardLocation.MonsterZone) return false;
            if (!Card.IsFaceup()) return false;

            // Use IsSerket instead of direct ID check
            if (!IsSerket(Card)) return false;

            bool haveTempleInHand = Bot.HasInHand(CardId.TempleOfTheKings);
            bool templeEffectOnline = HasTempleEffectOnline();

            bool haveDefense = Bot.HasInHand(CardId.DefenseOfTheTemple)
                || Bot.SpellZone.Take(5).Any(z => z != null && z.Id == CardId.DefenseOfTheTemple);

            bool haveTreasures = Bot.HasInHand(CardId.TreasuresOfTheKings) || IsTreasuresOnline();

            // Priority:
            //  1) If Temple effect is not online AND we don't already have Temple in hand, get Temple.
            //  2) Otherwise, use Serket to convert into additional gas (Defense / Treasures).
            int[] prefer = new[]
            {
                (!templeEffectOnline && !haveTempleInHand) ? CardId.TempleOfTheKings : 0,
                (!haveDefense) ? CardId.DefenseOfTheTemple : 0,
                (!haveTreasures) ? CardId.TreasuresOfTheKings : 0,
                // Fallbacks: if we can still add Temple, it's never "wrong" as a backup.
                (!haveTempleInHand) ? CardId.TempleOfTheKings : 0,
                (!haveDefense) ? CardId.DefenseOfTheTemple : 0,
                (!haveTreasures) ? CardId.TreasuresOfTheKings : 0,
            };

            int pick = prefer.FirstOrDefault(x => x != 0);
            if (pick == 0) return false;

            AI.SelectCard(pick);
            _didSerketSearch = true;
            ODLog("[OD.Serket.Search]", "Add id=" + pick);
            return true;
        }

        // ------------------------------------------------------------
        // Serket battle trigger (EVENT_BATTLE_START) — destroy battling monster
        // Lua: aux.Stringid(id,2)
        // ------------------------------------------------------------
        private bool SerketBattleDestroy()
        {
            // Disambiguate from the ignition search effect.
            int desc2 = Util.GetStringId(CardId.MercilessScorpionOfSerket, 2);
            if (ActivateDescription != -1 && ActivateDescription != desc2) return false;

            // Use IsSerket instead of direct ID check
            if (Card == null || !IsSerket(Card)) return false;
            if (Card.Location != CardLocation.MonsterZone || !Card.IsFaceup()) return false;

            // Only offered during battle timing; keep conservative.
            if (Duel.Phase != DuelPhase.BattleStart
                && Duel.Phase != DuelPhase.BattleStep
                && Duel.Phase != DuelPhase.Damage
                && Duel.Phase != DuelPhase.Battle)
                return false;

            ODLog("[OD.Serket.Battle]", "activate destroy-at-battle-start");
            return true;
        }

        // ------------------------------------------------------------
        // Batch 1: Activate Temple of the Kings
        // ------------------------------------------------------------
        private bool TempleActivate()
        {
            if (!IsMainPhase()) return false;
            if (_didTempleActivate) return false;

            // From hand activation.
            if (Card.Location != CardLocation.Hand) return false;

            // Don't waste if we already have a face-up Temple.
            if (HasTempleEffectOnline()) return false;

            // Need a free S/T zone (0-4).
            if (!HasFreeSpellZone()) return false;

            _didTempleActivate = true;
            ODLog("[OD.Temple.Act]", "Activate Temple of the Kings");
            return true;
        }

        // ------------------------------------------------------------
        // Mid-combo SpellSet: set Swamp Deity from hand once Temple effect is online
        // (only when we need an extra "Apophis" body and Serpent couldn't set it from Deck)
        // IMPORTANT: SpellSet handlers are evaluated per candidate card (global Card).
        // ------------------------------------------------------------
        private bool SetSwampDeityFromHandMidCombo()
        {
            if (!IsMainPhase()) return false;
            if (_didSetSwampDeityFromHand) return false;

            if (Card == null || Card.Id != CardId.ApophisTheSwampDeity) return false;
            if (!Bot.HasInHand(CardId.ApophisTheSwampDeity)) return false;
            if (!HasTempleEffectOnline()) return false;
            if (!HasFreeSpellZone()) return false;

            // Don't set if Deity is already set/active.
            bool alreadyOnField = Bot.SpellZone.Take(5).Any(c => c != null && c.Id == CardId.ApophisTheSwampDeity)
                || Bot.MonsterZone.Any(c => c != null && c.Id == CardId.ApophisTheSwampDeity);
            if (alreadyOnField) return false;

            // Only do this when it helps progression: we either already have Serpent as a monster,
            // or we have Serpent set and Temple effect online (so we'll flip it), and we want the 2nd Apophis.
            bool serpentRelevant = Bot.MonsterZone.Any(c => c != null && c.Id == CardId.ApophisTheSerpent)
                || Bot.SpellZone.Take(5).Any(c => c != null && c.Id == CardId.ApophisTheSerpent);
            if (!serpentRelevant) return false;

            _didSetSwampDeityFromHand = true;
            ODLog("[OD.Deity.SetFromHand]", "Set Swamp Deity (mid-combo)");
            return true;
        }

        // ------------------------------------------------------------
        // Batch 1: Flip Apophis the Serpent -> set Swamp Deity
        // ------------------------------------------------------------
        private bool ApophisSerpentFlipAndSetDeity()
        {
            if (!IsMainPhase()) return false;

            // If you already added IsMyTurn() in your file, keep this line.
            // If you DON'T have IsMyTurn(), delete this line.
            if (!IsMyTurn()) return false;

            if (_didFlipSerpent) return false;

            // Must have real Temple effect online to activate a Trap the turn it was set.
            if (!HasTempleEffectOnline())
            {
                ODLog("[OD.Serpent.Flip]", "Skip: no Temple effect");
                return false;
            }

            // Only activate when Serpent is facedown in S/T.
            if (Card.Location != CardLocation.SpellZone || !Card.IsFacedown())
                return false;

            // Needs monster zone space (Serpent becomes a monster).
            if (!HasFreeMonsterZone())
            {
                ODLog("[OD.Serpent.Flip]", "Skip: no free MZ");
                return false;
            }

            // IMPORTANT: do NOT scan Bot.Deck to decide if targets exist (hidden deck => Id=0).
            // Just answer YES and provide ordered preferences; the engine prompt list is the truth.
            _askYesNoSerpentSet = true;

            // Prefer: Deity > Azurune

            _didFlipSerpent = true;
            ODLog("[OD.Serpent.Flip]", "activate Serpent (prefer set: Deity > Azurune)");
            return true;
        }


        // ------------------------------------------------------------
        // Batch 1: Flip Swamp Deity (allow activation even if no negate targets)
        // ------------------------------------------------------------
        private bool ApophisSwampDeityActivate()
        {
            if (!IsMainPhase()) return false;
            if (!IsMyTurn()) return false;
            if (_didFlipSwampDeity) return false;

            // Only activate when facedown in S/T.
            if (Card.Location != CardLocation.SpellZone || !Card.IsFacedown()) return false;

            // Must not attempt set-this-turn activation unless Temple effect online.
            if (WasSetThisTurn(Card) && !(HasTempleEffectOnline() || _deitySetBySerpentThisTurn))
                return false;

            if (!HasFreeMonsterZone()) return false;

            // Optional negate: if we can negate anything, force yes and pick targets.
            int maxTargets = CountOtherFaceupContinuousTrapsYouControl_ExceptDeity();
            if (maxTargets > 0)
            {
                // Many cards present this as optional; safe to auto-yes.
                _askYesNoDeityNegate = true;
                _selectCtx = SelectCtx.DeityNegateTargets;
            }

            _didFlipSwampDeity = true;
            ODLog("[OD.Deity.Flip]", "activate Swamp Deity");
            return true;
        }

        private int CountOtherFaceupContinuousTrapsYouControl_ExceptDeity()
        {
            int cnt = 0;

            foreach (ClientCard c in Bot.SpellZone.Take(5))
            {
                if (c != null && c.IsFaceup() && c.HasType(CardType.Trap) && c.HasType(CardType.Continuous) && c.Id != CardId.ApophisTheSwampDeity)
                    cnt++;
            }
            foreach (ClientCard c in Bot.MonsterZone)
            {
                if (c != null && c.IsFaceup() && c.HasType(CardType.Trap) && c.HasType(CardType.Continuous) && c.Id != CardId.ApophisTheSwampDeity)
                    cnt++;
            }
            return cnt;
        }

        private IList<ClientCard> SelectDeityNegateTargets(IList<ClientCard> cards, int min, int max)
        {
            // Policy: never destroy/negate our own cards unless no enemy targets exist.
            List<ClientCard> enemy = cards
                .Where(c => c != null && IsEnemyFieldCard(c) && c.IsFaceup())
                .OrderByDescending(ScoreEnemyThreat)
                .ToList();

            List<ClientCard> bot = cards
                .Where(c => c != null && !IsEnemyFieldCard(c) && c.IsFaceup())
                .OrderByDescending(c => c.GetDefensePower())
                .ToList();

            List<ClientCard> pick = new List<ClientCard>();

            if (enemy.Count > 0)
            {
                pick.AddRange(enemy.Take(max));

                // Track keys for follow-up pops (Statue/Rabbit/Divine) so we avoid wasting on already-negated cards this chain.
                foreach (var t in pick)
                    _deityNegatedTargetKeysThisChain.Add(GetCardKey(t));

                ODLog("[OD.Deity.Negate]", "targets=enemy x" + pick.Count);
            }
            else
            {
                // Only if forced.
                pick.AddRange(bot.Take(max));
                ODLog("[OD.Deity.Negate]", "targets=bot x" + pick.Count);
            }

            return Util.CheckSelectCount(pick, cards, min, max);
        }

        private int ScoreEnemyThreat(ClientCard c)
        {
            // Simple heuristic: monsters first, then S/T; higher DEF power is higher threat.
            int baseScore = c.IsMonster() ? 100000 : 50000;
            return baseScore + c.GetDefensePower();
        }

        private IList<ClientCard> SelectRabbitSetFromDeck(IList<ClientCard> cards, int min, int max)
        {
            // Prefer Statue of Anguish Pattern.
            ClientCard statue = cards?.FirstOrDefault(c => c != null && c.Id == CardId.StatueOfAnguishPattern);
            if (statue != null)
            {
                ODLog("[OD.Rabbit.Set]", "pick Statue from Deck");
                return Util.CheckSelectCount(new List<ClientCard> { statue }, cards, min, max);
            }

            // Fallbacks that still progress trap-monster lines.
            int[] fallback = new[]
            {
                CardId.ApophisTheSwampDeity,
                CardId.ApophisTheSerpent,
                CardId.AngelStatueAzurune,
                CardId.VerdictOfAnubis,
                CardId.DominusPurge
            };
            foreach (int id in fallback)
            {
                ClientCard c = cards?.FirstOrDefault(x => x != null && x.Id == id);
                if (c != null)
                {
                    ODLog("[OD.Rabbit.Set]", "fallback pick id=" + id);
                    return Util.CheckSelectCount(new List<ClientCard> { c }, cards, min, max);
                }
            }

            if (cards != null && cards.Count > 0)
                return Util.CheckSelectCount(new List<ClientCard> { cards[0] }, cards, min, max);

            return null;
        }

        private IList<ClientCard> SelectDivineTributes(IList<ClientCard> cards, int min, int max)
        {
            // Tribute the two "Apophis" trap monsters: Serpent + Swamp Deity.
            ClientCard serpent = cards?.FirstOrDefault(c => c != null && c.Id == CardId.ApophisTheSerpent);
            ClientCard deity = cards?.FirstOrDefault(c => c != null && c.Id == CardId.ApophisTheSwampDeity);

            List<ClientCard> pick = new List<ClientCard>();
            if (serpent != null) pick.Add(serpent);
            if (deity != null) pick.Add(deity);

            ODLog("[OD.Divine.SS]", "tribute pick=" + string.Join(",", pick.Select(x => x.Id)));
            return Util.CheckSelectCount(pick, cards, min, max);
        }

                private IList<ClientCard> SelectDivineSetFromGrave(IList<ClientCard> cards, int min, int max)
        {
            var offer = (cards ?? new List<ClientCard>()).Where(c => c != null).ToList();

            int freeSZ = CountFreeSpellZone();
            int distinctOffer = offer.Select(c => c.Id).Distinct().Count();

            ODLog("[OD.Divine.SetBack]",
                $"select (offer={offer.Count}, min={min}, max={max}, freeSZ={freeSZ}, distinctOffer={distinctOffer}, chosen={string.Join(",", _divineSetBackChosenIds.OrderBy(id => id))}, szone={DumpBotSpellZone()}) (phase={Duel.Phase}, pl={Duel.Player})");

            // IMPORTANT: keep a "negate body" online first.
            // Deity > Azurune > Serpent (Serpent is mostly pressure; Deity/Azurune are your interaction pieces).
            int[] prefer = { CardId.ApophisTheSwampDeity, CardId.AngelStatueAzurune, CardId.ApophisTheSerpent };

            var ordered = offer
                .OrderBy(c =>
                {
                    int p = Array.IndexOf(prefer, c.Id);
                    return p < 0 ? 999 : p;
                })
                .ThenBy(c => c.Id)
                .ToList();

            List<ClientCard> sel = new List<ClientCard>();

            // Engine behavior differs by core build:
            //  A) One-shot multi-select (max > 1): return up to max distinct names in one response.
            //  B) Step-by-step (max == 1): engine repeats selection and shows a YES/NO prompt to continue.
            if (max > 1)
            {
                var seen = new HashSet<int>();
                foreach (var c in ordered)
                {
                    if (c == null) continue;
                    if (!seen.Add(c.Id)) continue;
                    sel.Add(c);
                    if (sel.Count >= max) break;
                }

                _askYesNoDivineSetBackMore = false;
                _divineSetBackChosenIds.Clear();

                ODLog("[OD.Divine.SetBack]", $"pick={string.Join(",", sel.Select(c => c.Id))} (phase={Duel.Phase}, pl={Duel.Player})");
                return Util.CheckSelectCount(sel, cards, min, max);
            }

            // Step-by-step pick: choose the best not-yet-chosen name.
            ClientCard pick = ordered.FirstOrDefault(c => c != null && !_divineSetBackChosenIds.Contains(c.Id));
            if (pick != null)
            {
                sel.Add(pick);
                _divineSetBackChosenIds.Add(pick.Id);
            }

            int remainingDistinct = distinctOffer - _divineSetBackChosenIds.Count;
            int desiredCap = Math.Min(3, distinctOffer);

            // Arm YES if we can/should keep selecting.
            _askYesNoDivineSetBackMore = (remainingDistinct > 0 && _divineSetBackChosenIds.Count < desiredCap && HasFreeSpellZone());

            ODLog("[OD.Divine.SetBack]",
                $"pick={string.Join(",", sel.Select(c => c.Id))} (remain={remainingDistinct}, cap={desiredCap}, askMore={(_askYesNoDivineSetBackMore ? 1 : 0)}) (phase={Duel.Phase}, pl={Duel.Player})");

            return Util.CheckSelectCount(sel, cards, min, max);
        }



        // ------------------------------------------------------------
        // Extension: Link Summon Silhouhatte Rabbit (2 effect monsters)
        // ------------------------------------------------------------
        private bool SilhouhatteRabbitSummon()
        {
            if (!IsMainPhase()) return false;
            if (_didRabbitSummon) return false;
            if (Bot.HasInMonstersZone(CardId.SilhouhatteRabbit)) return false;

            // If Defense of the Temple line is available, keep Serket on board (don't consume it as link material).
            if (WantDefenseFusionRemovalNow())
            {
                ODLog("[OD.Rabbit.SS][HOLD]", "hold Rabbit: Defense fusion line available (keep Serket)");
                return false;
            }

            ClientCard man = Bot.MonsterZone.FirstOrDefault(c => c != null && c.IsFaceup() && c.Id == CardId.TheManWithTheMark);
            ClientCard serket = Bot.MonsterZone.FirstOrDefault(c => c != null && c.IsFaceup() && c.Id == CardId.MercilessScorpionOfSerket);
            if (man == null || serket == null) return false;

            if (!man.HasType(CardType.Effect) || !serket.HasType(CardType.Effect)) return false;

            List<ClientCard> mats = new List<ClientCard> { man, serket };
            if (Util.GetBotAvailZonesFromExtraDeck(mats) == 0) return false;

            AI.SelectMaterials(mats);
            _didRabbitSummon = true;

            int freeSZ = CountFreeSpellZone();
            ODLog("[OD.Rabbit.SS]", "link summon using Man+Serket | freeSZ=" + freeSZ);

            return true;
        }


        // ------------------------------------------------------------
        // Extension: Rabbit on-Link-Summon -> set Statue of Anguish Pattern
        // ------------------------------------------------------------
        private bool SilhouhatteRabbitSetStatue()
        {
            if (!IsMyTurn()) return false;
            if (_didRabbitSet) return false;
            if (_pendingRabbitSet) return false;

            // Only the on-Link-Summon set effect.
            int descSet = Util.GetStringId(CardId.SilhouhatteRabbit, 0);

            // Trigger prompts sometimes arrive as 0/-1 (generic description).
            // Only reject when a positive description mismatches. When unknown, ensure Rabbit was just summoned.
            if (ActivateDescription > 0)
            {
                if (ActivateDescription != descSet) return false;
            }
            else
            {
                if (!Duel.LastSummonedCards.Contains(Card)) return false;
            }

            if (Card.Location != CardLocation.MonsterZone || !Card.IsFaceup()) return false;
            if (!HasFreeSpellZone()) return false;

            _selectCtx = SelectCtx.RabbitSetFromDeck;

            AI.SelectCard(
                CardId.StatueOfAnguishPattern,
                CardId.AngelStatueAzurune,
                CardId.ApophisTheSwampDeity,
                CardId.ApophisTheSerpent
            );

            _pendingRabbitSet = true;
            ODLog("[OD.Rabbit.Set]", "activate -> set-from-Deck (pending)");
            return true;
        }


        // ------------------------------------------------------------
        // Divine Serpent Apophis (tribute 2 "Apophis" monsters)
        // ------------------------------------------------------------
        private bool DivineSerpentApophisSpecialSummon()
        {
            if (!IsMainPhase()) return false;
            if (_didDivineSummon) return false;
            if (Bot.HasInMonstersZone(CardId.DivineSerpentApophis)) return false;
            if (!HasFreeMonsterZone()) return false;

            ClientCard serpent = Bot.MonsterZone.FirstOrDefault(c => c != null && c.IsFaceup() && c.Id == CardId.ApophisTheSerpent);
            ClientCard deity = Bot.MonsterZone.FirstOrDefault(c => c != null && c.IsFaceup() && c.Id == CardId.ApophisTheSwampDeity);
            if (serpent == null || deity == null) return false;

            _selectCtx = SelectCtx.DivineTribute;
            AI.SelectCard(CardId.ApophisTheSerpent, CardId.ApophisTheSwampDeity);

            _didDivineSummon = true;
            _divineSummonCount++;
            ODLog("[OD.Divine.SS]", "special summon (tribute Serpent+Deity)");
            return true;
        }


        // NEW: Second Divine Serpent for spare bodies (Requested)
        private bool SecondDivineSerpentSummon()
        {
            if (!IsMainPhase()) return false;
            if (_didSummonSecondDivine) return false;
            if (_divineSummonCount >= 2) return false; // Cap at 2 per turn
            if (!HasFreeMonsterZone()) return false;

            // Check for additional Apophis bodies beyond the first Divine
            // EXCLUDE existing Divine Serpent Apophis from being used as tribute
            var apophisMonsters = Bot.MonsterZone
                .Where(c => c != null && c.IsFaceup() && IsApophis(c) && c.Id != CardId.DivineSerpentApophis)
                .ToList();

            if (apophisMonsters.Count < 2) return false;

            _selectCtx = SelectCtx.SecondDivineTribute;
            _secondDivineTributesChosen.Clear();
            _didSummonSecondDivine = true;
            _divineSummonCount++;

            ODLog("[OD.Divine.Second]", "special summon second Divine (spare bodies)");
            return true;
        }

        // ------------------------------------------------------------
        // Divine Serpent Apophis effect -> set back Apophis traps from GY
        // ------------------------------------------------------------
        private bool DivineSerpentApophisSetBackApophis()
        {
            if (!IsMainPhase()) return false;
            if (_didDivineSetBack) return false;

            if (Card.Location != CardLocation.MonsterZone || !Card.IsFaceup()) return false;

            int desc = Util.GetStringId(CardId.DivineSerpentApophis, 0);
            if (ActivateDescription > 0 && ActivateDescription != desc) return false;

            if (!HasFreeSpellZone()) return false;

            bool hasAny = Bot.Graveyard.Any(c => c != null && (c.Id == CardId.ApophisTheSerpent
                || c.Id == CardId.ApophisTheSwampDeity
                || c.Id == CardId.AngelStatueAzurune));
            if (!hasAny) return false;

            _divineSetBackChosenIds.Clear();
            _askYesNoDivineSetBackMore = false;

            _selectCtx = SelectCtx.DivineSetFromGrave;

            _didDivineSetBack = true;
            ODLog("[OD.Divine.SetBack]", "set back Apophis traps");
            return true;
        }


        

        // ------------------------------------------------------------
        // Divine Serpent Apophis trigger -> pop 1 enemy card when a Trap is activated (except during Damage Step)
        // ------------------------------------------------------------
        private bool DivineSerpentApophisPopOnTrap()
        {
            if (_didDivinePopThisTurn || _pendingDivinePop) return false;

            if (!TryCommitPopBudget(c => true))
                return false;

            _pendingDivinePop = true;

            if (Card.Location != CardLocation.MonsterZone || !Card.IsFaceup()) return false;
            if (Duel.Phase == DuelPhase.Damage || Duel.Phase == DuelPhase.DamageCal) return false;
            if (!EnemyHasAnyCardOnField()) return false;

            int desc = Util.GetStringId(CardId.DivineSerpentApophis, 1);
            if (ActivateDescription > 0 && ActivateDescription != desc) return false;

            _selectCtx = SelectCtx.DivineSerpentPopTarget;
            ODLog("[OD.Divine.Pop]", "trigger -> select enemy card to destroy");
            return true;
        }

        // Enhanced Divine Serpent pop selection to avoid already-negated targets:
        private IList<ClientCard> SelectDivineSerpentPopTarget(IList<ClientCard> cards, int min, int max)
        {
            List<ClientCard> enemy = cards
                .Where(c => c != null && IsEnemyFieldCard(c))
                .OrderByDescending(ScoreEnemyThreat)
                .ToList();

            List<ClientCard> pick = new List<ClientCard>();

            if (enemy.Count == 0)
            {
                if (cards != null && cards.Count > 0 && cards[0] != null)
                    pick.Add(cards[0]);
                return Util.CheckSelectCount(pick, cards, min, max);
            }

            // AVOID: cards already negated by Deity this chain, AND cards already targeted by other pops
            ClientCard chosen = enemy.FirstOrDefault(c =>
                !_popUsedTargetKeysThisChain.Contains(GetCardKey(c)) &&
                !_deityNegatedTargetKeysThisChain.Contains(GetCardKey(c)) &&
                !_statueUsedTargetKeysThisChain.Contains(GetCardKey(c)))
                ?? enemy.FirstOrDefault(c => !_popUsedTargetKeysThisChain.Contains(GetCardKey(c)))
                ?? enemy.FirstOrDefault(c => !_deityNegatedTargetKeysThisChain.Contains(GetCardKey(c)))
                ?? enemy.FirstOrDefault(c => !_statueUsedTargetKeysThisChain.Contains(GetCardKey(c)))
                ?? enemy[0];

            pick.Add(chosen);
            _popUsedTargetKeysThisChain.Add(GetCardKey(chosen));

            ODLog("[OD.Divine.Pop]", "pick=" + string.Join(",", pick.Select(c => c.Id)) +
                  " (avoidedDeityNegated=" + _deityNegatedTargetKeysThisChain.Count +
                  ", avoidedPops=" + _popUsedTargetKeysThisChain.Count + ")");
            return Util.CheckSelectCount(pick, cards, min, max);
        }

        // Enhanced Rabbit pop selection with better target avoidance:
        private IList<ClientCard> SelectRabbitDestroySTTarget(IList<ClientCard> cards, int min, int max)
        {
            List<ClientCard> enemy = cards
                .Where(c => c != null && IsEnemyFieldCard(c))
                .OrderByDescending(ScoreEnemySpellTrapThreat)
                .ToList();

            List<ClientCard> pick = new List<ClientCard>();
            if (enemy.Count > 0)
            {
                // STRICTER avoidance: don't target cards already being negated OR already popped
                ClientCard chosen = enemy.FirstOrDefault(c =>
                    !_popUsedTargetKeysThisChain.Contains(GetCardKey(c)) &&
                    !_deityNegatedTargetKeysThisChain.Contains(GetCardKey(c)))
                    ?? enemy.FirstOrDefault(c => !_popUsedTargetKeysThisChain.Contains(GetCardKey(c)))
                    ?? enemy.FirstOrDefault(c => !_deityNegatedTargetKeysThisChain.Contains(GetCardKey(c)))
                    ?? enemy[0];

                pick.Add(chosen);
                _popUsedTargetKeysThisChain.Add(GetCardKey(chosen));
            }

            ODLog("[OD.Rabbit.PopST]", "pick=" + string.Join(",", pick.Select(c => c.Id)) +
                  " (avoided=" + (_deityNegatedTargetKeysThisChain.Count + _popUsedTargetKeysThisChain.Count) + ")");
            return Util.CheckSelectCount(pick, cards, min, max);
        }


        // ------------------------------------------------------------
        // Batch 2: Reactive policies (opp turn) + trigger targeting
        // ------------------------------------------------------------

        // Statue of Anguish Pattern: only activate (become monster) on opponent turn once they have committed.
        private bool StatueOfAnguishPatternActivateOppTurn()
        {
            if (!IsMainPhase()) return false;
            if (!IsOppTurn()) return false;

            if (ShouldHoldTrapMonsterAfterCalledBy())
            {
                ODLog("[OD.Statue.Act]", "HOLD: Called By already chained vs handtrap");
                return false;
            }

            // Don't activate twice in the same chain window.
            if (_chainActivatedStatue) return false;

            // Activation: facedown Continuous Trap in S/T.
            if (Card == null || Card.Location != CardLocation.SpellZone || !Card.IsFacedown()) return false;
            if (!HasFreeMonsterZone()) return false;

            // Don't flip too early; wait for 2 threats (2 monsters OR 1 monster+1 backrow).
            if (!EnemyCommittedTwoThreats()) return false;

            // Don't waste into an active enemy Imperm column (enemy set-Imperm this turn).
            if (IsSTInEnemyImpermDangerColumn(Card))
                return false;

            // Chain policy:
            // - Prefer CHAINING Statue after another trap-monster has already been activated this chain
            //   (so Statue resolves first, then the trap-monster resolves and triggers Statue to pop).
            // - If no trap-monster is in this chain, only flip if we still have a facedown follow-up
            //   trap-monster available for later.
            bool trapMonsterAlreadyInThisChain = (_chainActivatedDeity || _chainActivatedSerpent);
            bool hasFacedownFollowup = HasStatueFollowupTrapMonsterThisTurn();

            if (!trapMonsterAlreadyInThisChain && !hasFacedownFollowup)
            {
                ODLog("[OD.Statue.Act]", "HOLD: no follow-up (trapInChain=0, facedownFollowup=0)");
                return false;
            }

            _chainActivatedStatue = true;
            ODLog("[OD.Statue.Act]",
                $"flip Statue (opp turn policy, chainLast=1, trapInChain={(trapMonsterAlreadyInThisChain ? 1 : 0)}, facedownFollowup={(hasFacedownFollowup ? 1 : 0)})");

            return true;
        }

        // Statue of Anguish Pattern: trigger destruction when another S/T->MZ summon happens.
        private bool StatueOfAnguishPatternDestroyEffect()
        {
            // Trigger effect will be from Monster Zone.
            if (Card.Location != CardLocation.MonsterZone || !Card.IsFaceup()) return false;

            // Policy: never spend Statue pops on our own cards. If opponent has no field cards, skip.
            if (!EnemyHasAnyCardOnField()) return false;

            int desc = Util.GetStringId(CardId.StatueOfAnguishPattern, 0);
            if (ActivateDescription > 0 && ActivateDescription != desc) return false;

            if (!TryCommitPopBudget(c => true))
                return false;

            _selectCtx = SelectCtx.StatueDestroyTarget;
            ODLog("[OD.Statue.Destroy]", "select destroy target");
            return true;
        }

        private IList<ClientCard> SelectStatueDestroyTarget(IList<ClientCard> cards, int min, int max)
        {
            List<ClientCard> enemy = cards
                .Where(c => c != null && IsEnemyFieldCard(c))
                .OrderByDescending(ScoreEnemyThreat)
                .ToList();

            List<ClientCard> pick = new List<ClientCard>();

            if (enemy.Count == 0)
            {
                // If this ever happens, we prefer not to self-pop. We still must return something to avoid illegal selection.
                // (Should be rare; usually the prompt contains opponent cards.)
                ODLog("[OD.Statue.Destroy]", "no enemy candidates in prompt; fallback=first");
                if (cards != null && cards.Count > 0 && cards[0] != null)
                    pick.Add(cards[0]);

                return Util.CheckSelectCount(pick, cards, min, max);
            }

            ClientCard chosen = enemy.FirstOrDefault(c => !_popUsedTargetKeysThisChain.Contains(GetCardKey(c))
                                                         && !_statueUsedTargetKeysThisChain.Contains(GetCardKey(c))
                                                         && !_deityNegatedTargetKeysThisChain.Contains(GetCardKey(c)))
                ?? enemy.FirstOrDefault(c => !_popUsedTargetKeysThisChain.Contains(GetCardKey(c))
                                             && !_statueUsedTargetKeysThisChain.Contains(GetCardKey(c)))
                ?? enemy.FirstOrDefault(c => !_statueUsedTargetKeysThisChain.Contains(GetCardKey(c)))
                ?? enemy[0];
            _statueUsedTargetKeysThisChain.Add(GetCardKey(chosen));
            _popUsedTargetKeysThisChain.Add(GetCardKey(chosen));

            pick.Add(chosen);
            ODLog("[OD.Statue.Destroy]", "pick=" + string.Join(",", pick.Select(c => c.Id)));
            return Util.CheckSelectCount(pick, cards, min, max);
        }

        // Rabbit: destroy 1 enemy S/T when a S/T->MZ special summon happens (once per turn).
        private bool SilhouhatteRabbitDestroySpellTrap()
        {
            // Value primarily on opponent turn; restricting avoids hijacking the on-summon set trigger.
            if (!IsOppTurn()) return false;

            if (ShouldHoldTrapMonsterAfterCalledBy())
            {
                ODLog("[OD.Rabbit.PopST]", "HOLD: Called By already chained vs handtrap");
                return false;
            }
            if (Duel.Phase == DuelPhase.Damage || Duel.Phase == DuelPhase.DamageCal) return false;

            if (_didRabbitPopST || _pendingRabbitPopST) return false;

            if (Card.Location != CardLocation.MonsterZone || !Card.IsFaceup()) return false;

            int desc = Util.GetStringId(CardId.SilhouhatteRabbit, 1);
            // Engine may supply 0/-1 for generic trigger prompts; only reject when a positive desc mismatches.
            if (ActivateDescription > 0 && ActivateDescription != desc) return false;

            // Only meaningful if opponent has at least one S/T.
            if (Enemy.SpellZone.Take(5).All(z => z == null))
                return false;
            if (!TryCommitPopBudget(c => c.Location == CardLocation.SpellZone && c.Sequence >= 0 && c.Sequence <= 4))
                return false;

            _selectCtx = SelectCtx.RabbitDestroySTTarget;
            _pendingRabbitPopST = true;
            ODLog("[OD.Rabbit.PopST]", "activate -> select enemy S/T to destroy (pending)");
            return true;
        }


        private int ScoreEnemySpellTrapThreat(ClientCard c)
        {
            int score = 0;
            if (c == null) return score;

            // Prefer face-up floodgates/engines.
            if (c.IsFaceup()) score += 50000;
            if (c.IsFloodgate()) score += 40000;

            if (c.HasType(CardType.Field)) score += 15000;
            if (c.HasType(CardType.Continuous)) score += 12000;
            if (c.HasType(CardType.Equip)) score += 8000;
            if (c.HasType(CardType.Pendulum)) score += 4000;

            // Tie-breaker.
            score += c.GetDefensePower();
            return score;
        }

        // Opp turn policy: flip Serpent only when opponent has committed enough targets.
        private bool ApophisSerpentDisrupt()
        {
            if (!IsMainPhase()) return false;
            if (!IsOppTurn()) return false;

            if (ShouldHoldTrapMonsterAfterCalledBy())
            {
                ODLog("[OD.Serpent.Disrupt]", "HOLD: Called By already chained vs handtrap");
                return false;
            }
            if (_didFlipSerpent) return false;
            if (_chainActivatedStatue) return false;
            if (TrapMonsterDangerVeto("ApophisDeity")) return false;

            // Smart hold: if we already chained Deity this chain, keep Serpent set as a follow-up trigger for later.
            if (_chainActivatedDeity)
            {
                ODLog("[OD.Serpent.Disrupt]", "hold Serpent (Deity already chained this chain)");
                return false;
            }

            // Must be facedown in S/T.
            if (Card.Location != CardLocation.SpellZone || !Card.IsFacedown()) return false;
            if (!HasFreeMonsterZone()) return false;

            // If Statue is already a monster, Serpent will immediately trigger Statue's destroy.
            bool statueLive = BotHasFaceupContinuousTrap(CardId.StatueOfAnguishPattern);
            if (!statueLive)
            {
                // Otherwise, wait for 2 threats (2 monsters OR 1 monster+1 backrow) before committing.
                if (!EnemyCommittedTwoThreats()) return false;
            }

            int setId = ChooseSerpentSetIdFromDeck();
            if (setId != 0)
            {
                _askYesNoSerpentSet = true;
                AI.SelectCard(setId);
                if (setId == CardId.ApophisTheSwampDeity) _deitySetBySerpentThisTurn = true;
                ODLog("[OD.Serpent.Disrupt]", "will set from deck id=" + setId);
            }
            else
            {
                ODLog("[OD.Serpent.Disrupt]", "no set target in deck -> flip anyway");
            }

            _didFlipSerpent = true;
            _chainActivatedSerpent = true;
            ODLog("[OD.Serpent.Disrupt]", "activate Serpent (opp turn policy)");
            return true;
        }

        // Opp turn policy: flip Deity only when it can negate multiple or a key threat.
        private bool ApophisSwampDeityDisrupt()
        {
            if (!IsMainPhase()) return false;
            if (!IsOppTurn()) return false;

            if (ShouldHoldTrapMonsterAfterCalledBy())
            {
                ODLog("[OD.Deity.Disrupt]", "HOLD: Called By already chained vs handtrap");
                return false;
            }
            if (_didFlipSwampDeity) return false;

            if (TrapMonsterDangerVeto("ApophisDeity")) return false;

            if (Card.Location != CardLocation.SpellZone || !Card.IsFacedown()) return false;
            if (!HasFreeMonsterZone()) return false;

            // Set-turn gating: allow if Temple effect is online OR it was set by Serpent this turn.
            if (WasSetThisTurn(Card) && !(HasTempleEffectOnline() || _deitySetBySerpentThisTurn))
                return false;

            int maxTargets = CountOtherFaceupContinuousTrapsYouControl_ExceptDeity();
            if (maxTargets <= 0) return false;

            int enemyFaceup = CountEnemyFaceupCards();
            if (enemyFaceup <= 0) return false;

            bool hasProblem =
                Util.GetProblematicEnemyMonster(0, true) != null ||
                Util.GetBestEnemySpell(true) != null;

            // NEW: Hold Deity if we already have Serpent activated and no emergency
            if (_chainActivatedSerpent && !hasProblem)
            {
                ODLog("[OD.Deity.Disrupt]", "HOLD: Serpent already activated this chain, no emergency");
                return false;
            }

            // Don't cash in a 1-negate Deity too early unless there's a real problem.
            if (!hasProblem)
            {
                // Require at least 2 faceup enemy cards AND at least 2 negate targets for value
                if (enemyFaceup < 2) return false;
                if (maxTargets < 2) return false;

                // Also require significant threat level
                int highThreatCount = enemyFaceup; // Simple heuristic
                if (highThreatCount < 2) return false;
            }

            _askYesNoDeityNegate = true;
            _selectCtx = SelectCtx.DeityNegateTargets;

            _didFlipSwampDeity = true;
            _chainActivatedDeity = true;
            ODLog("[OD.Deity.Disrupt]", "activate Deity (emergency=" + hasProblem +
                  ", maxNegates=" + maxTargets + ", enemyFaceup=" + enemyFaceup + ")");
            return true;
        }

        // ------------------------------------------------------------
        // Batch 3: The Winged Dragon of Ra - Sphere Mode (board breaker)
        // ------------------------------------------------------------
        private bool SphereModeSummon()
        {
            if (Card == null || Card.Id != CardId.TheWingedDragonOfRaSphereMode) return false;
            if (!IsMyTurn() || !IsMainPhase1()) return false;
            RefreshEvenlyPlan();
            if (_planEvenlyThisTurn) return false; // don't conflict with Evenly plan

            // Only when opponent has 3+ monsters.
            int enemyMonsters = CountEnemyMonsters();
            if (enemyMonsters < 3) return false;

            // If we can run our main engine, prefer engine unless opponent board is very threatening.
            bool haveEngine = HasAnyEngineStarterInHand();
            bool enemyHasProblem = Util.GetProblematicEnemyMonster(0, true) != null || Enemy.MonsterZone.Any(c => c != null && c.IsFloodgate());
            if (haveEngine && !enemyHasProblem) return false;

            _selectingSphereModeTributes = true;
            _selectCtx = SelectCtx.SphereModeTributes;

            _normalSummonUsedThisTurn = true;
            ODLog("[OD.SphereMode]", "normal summon (prepare tribute select) enemyMonsters=" + enemyMonsters);
            return true;
        }

        private IList<ClientCard> SelectSphereModeTributes(IList<ClientCard> cards, int min, int max, bool cancelable)
        {
            // Sphere Mode should ONLY tribute opponent monsters. Never sacrifice our own monsters.
            // If we cannot select enough enemy tributes from the offered list, cancel (when possible).
            if (cards == null || cards.Count == 0) return null;

            var enemy = cards
                .Where(c => c != null && IsEnemyFieldCard(c) && c.IsMonster())
                .OrderByDescending(ScoreEnemyThreat)
                .ThenByDescending(c => Math.Max(0, c.Attack))
                .ToList();

            ODLog("[OD.SphereMode]", "tribute pool enemy=" + enemy.Count + "/" + cards.Count +
                  " (min=" + min + ", max=" + max + ", cancelable=" + (cancelable ? 1 : 0) + ")");

            // Fail-safe: if we can't meet 'min' using ONLY enemy monsters, cancel instead of padding with our own cards.
            if (enemy.Count < min)
            {
                ODLog("[OD.SphereMode]", "CANCEL: not enough enemy tributes offered (enemy=" + enemy.Count + " < " + min + ")");
                if (cancelable)
                {
                    // We tentatively consumed the normal summon in SphereModeSummon; revert if we cancel.
                    _normalSummonUsedThisTurn = false;
                    return new List<ClientCard>();
                }

                // Non-cancelable selection: let base handle it (may still force an undesirable tribute in rare cases).
                return null;
            }

            int need = Math.Min(max, min); // Usually min=max=3, but keep defensive.
            var pick = enemy.Take(need).ToList();

            ODLog("[OD.SphereMode]", "tribute pick=" + string.Join(",", pick.Select(c => c.Id)));
            return Util.CheckSelectCount(pick, cards, min, max);
        }

        // ------------------------------------------------------------
        // Batch 3: Defense of the Temple -> Fusion Summon Divine Scorpion Beast of Serket
        // ------------------------------------------------------------
        private bool DefenseOfTheTempleFusionSummon()
        {
            if (Card == null || Card.Id != CardId.DefenseOfTheTemple) return false;
            if (!IsMyTurn() || !IsMainPhase()) return false;
            if (DefaultCheckWhetherCardIsNegated(Card)) return false;

            // Priority: Divine Scorpion Beast of Serket (Merciless + <=2500 ATK body),
            // with preference to use 1 enemy body when legal.
            ClientCard serket = Bot.MonsterZone.FirstOrDefault(c => c != null && c.IsFaceup() && c.Id == CardId.MercilessScorpionOfSerket);

            ClientCard oppMat = null;
            ClientCard ownMat = null;

            bool canMakeSerketFusion = false;
            if (serket != null)
            {
                // Using Serket (a monster we control that mentions Temple) unlocks opponent face-up monsters as materials.
                // Prefer using opponent material (if legal) to preserve our board.
                oppMat = Enemy.MonsterZone
                    // Serket fusion needs a secondary material with <= 2500 ATK.
                    // Also: when opponent material is legal, we want the *most dangerous* eligible enemy body.
                    .Where(c => c != null && c.IsFaceup() && c.IsMonster() && c.Attack <= 2500)
                    .OrderByDescending(ScoreEnemySerketFusionMaterial)
                    .ThenByDescending(c => Math.Max(0, c.Attack))
                    .FirstOrDefault();

                // Prefer recyclable trap-monster bodies; keep Statue as last resort.
                ownMat = Bot.MonsterZone
                    .Where(c => c != null && c.IsFaceup() && c.IsMonster() && c != serket)
                    .OrderBy(c => ScoreOwnSerketFusionMaterial(c))
                    .FirstOrDefault();

                canMakeSerketFusion = (oppMat != null || ownMat != null);

                // If we have no opponent material and our only secondary is Statue, don't fuse unless forced.
                if (oppMat == null && ownMat != null && ownMat.Id == CardId.StatueOfAnguishPattern)
                {
                    ODLog("[OD.DefenseFusion]",
                        $"skip Serket fusion: only secondary mat is Statue (preserve popper). (phase={Duel.Phase}, pl={Duel.Player})");
                    canMakeSerketFusion = false;
                }
            }

            // Fallback (rare): Divine Serpent Apophis fusion (mirror / edge cases).
            bool canMakeDivineFusion = false;
            if (!canMakeSerketFusion)
            {
                var ownFieldTemple = Bot.MonsterZone
                    .Where(c => c != null && c.IsFaceup() && c.IsMonster() && MentionsTempleOfTheKings(c))
                    .ToList();

                var ownHandTemple = Bot.Hand
                    .Where(c => c != null && c.IsMonster() && MentionsTempleOfTheKings(c))
                    .ToList();

                // Opponent materials are only legal if we use a monster we control (ON FIELD) that mentions Temple.
                bool allowOppTemple = ownFieldTemple.Count > 0;

                var enemyTemple = allowOppTemple
                    ? Enemy.MonsterZone.Where(c => c != null && c.IsFaceup() && c.IsMonster() && MentionsTempleOfTheKings(c)).ToList()
                    : new List<ClientCard>();

                var allTemple = ownFieldTemple.Concat(ownHandTemple).Concat(enemyTemple).ToList();

                // Need 2 materials that mention Temple, with at least 1 of them being ours.
                if (allTemple.Count >= 2 && (ownFieldTemple.Count + ownHandTemple.Count) >= 1)
                {
                    // If we would fuse using only hand materials, require at least one free MZ.
                    bool usesAnyFieldMat = ownFieldTemple.Count > 0 || enemyTemple.Count > 0;
                    if (usesAnyFieldMat || HasFreeMonsterZone())
                        canMakeDivineFusion = true;
                }
            }

            if (!canMakeSerketFusion && !canMakeDivineFusion) return false;

            int targetId = canMakeSerketFusion ? CardId.DivineScorpionBeastOfSerket : CardId.DivineSerpentApophis;
            AI.SelectCard(targetId);

            _selectCtx = SelectCtx.DefenseFusionMaterials;
            _pendingDefenseFusionTargetId = targetId;


            _pendingDefenseFusionSelectCalls = 0;
            _pendingDefenseFusionFirstWasField = false;
            _pendingDefenseFusionFirstPickKey = null;
            // For Serket fusion: lock the exact Serket instance (sequence) and whether we expect an enemy body.
            if (canMakeSerketFusion && serket != null)
            {
                _pendingDefenseFusionSerketSeq = serket.Sequence;
                _pendingDefenseFusionUseOpponent = (oppMat != null);
            }
            else
            {
                _pendingDefenseFusionSerketSeq = -1;
                _pendingDefenseFusionUseOpponent = false;
            }

            ODLog("[OD.DefenseFusion]", "activate (target=" + targetId + ", useOpp=" + _pendingDefenseFusionUseOpponent + ")");
            return true;
        }


        private IList<ClientCard> SelectDefenseFusionMaterials(IList<ClientCard> cards, int min, int max)
        {
            // Supports two targets:
            //  - Divine Scorpion Beast of Serket: include our Merciless Scorpion, then prefer 1 enemy <=2500 ATK.
            //  - Divine Serpent Apophis: pick 2 Temple-mentioning monsters, ensuring at least 1 is ours.
            // IMPORTANT: FusionMaterial can be multi-step (called twice with max==1). In that case, return ONE
            // material per call and keep the pending context until the final pick.

            if (cards == null || cards.Count == 0)
                return null;

            if (_pendingDefenseFusionTargetId != 0)
                _pendingDefenseFusionSelectCalls++;
            else
                _pendingDefenseFusionSelectCalls = 0;

            bool singlePick = max <= 1;

            Func<ClientCard, string> Key = c =>
            {
                if (c == null) return string.Empty;
                return $"{c.Controller}:{(int)c.Location}:{c.Sequence}:{c.Id}";
            };

            Func<ClientCard, bool> IsFirstPick = c =>
            {
                if (c == null || string.IsNullOrEmpty(_pendingDefenseFusionFirstPickKey)) return false;
                return Key(c) == _pendingDefenseFusionFirstPickKey;
            };

            Func<IEnumerable<ClientCard>, ClientCard> AvoidFirstPick = pool =>
            {
                if (pool == null) return null;
                var list = pool.Where(x => x != null).ToList();
                if (list.Count == 0) return null;
                return list.FirstOrDefault(x => !IsFirstPick(x)) ?? list[0];
            };

            List<ClientCard> pick = new List<ClientCard>();
            bool done = true;

            try
            {
                ODLog("[OD.DefenseFusion.Mat]", $"prompt(step={_pendingDefenseFusionSelectCalls}, min={min}, max={max}, offer={cards.Count}) target={_pendingDefenseFusionTargetId}");

                if (_pendingDefenseFusionTargetId == CardId.DivineScorpionBeastOfSerket)
                {
                    // 2 materials total. If max==1 we expect 2 calls.
                    done = !singlePick ? true : (_pendingDefenseFusionSelectCalls >= 2);

                    // Try to find the exact Serket we armed via sequence.
                    ClientCard serket = cards.FirstOrDefault(c => c != null && c.Id == CardId.MercilessScorpionOfSerket && c.Sequence == _pendingDefenseFusionSerketSeq)
                        ?? cards.FirstOrDefault(c => c != null && c.Id == CardId.MercilessScorpionOfSerket);

                    if (singlePick)
                    {
                        ClientCard chosen = null;

                        // Prefer Serket if it is offered and we haven't already selected it.
                        if (serket != null && !IsFirstPick(serket))
                            chosen = serket;

                        if (chosen == null && _pendingDefenseFusionUseOpponent)
                        {
                            var enemyPool = cards
                                .Where(c => c != null
                                    && IsEnemyFieldCard(c)
                                    && c.Location == CardLocation.MonsterZone
                                    && c.IsFaceup()
                                    && c.IsMonster()
                                    && c.Attack <= 2500)
                                .Select(c => new { Card = c, Score = ScoreEnemySerketFusionMaterial(c) })
                                .OrderByDescending(x => x.Score)
                                .ThenByDescending(x => Math.Max(0, x.Card.Attack))
                                .ToList();

                            if (enemyPool.Count > 0)
                                ODLog("[OD.DefenseFusion.Mat]", "enemyPool top=" + string.Join(",", enemyPool.Take(5).Select(x => x.Card.Id + ":" + x.Score)));

                            chosen = AvoidFirstPick(enemyPool.Select(x => x.Card));
                        }

                        if (chosen == null)
                        {
                            // Fall back to our side.
                            var ownPool = cards
                                .Where(c => c != null && IsMyFieldCard(c) && c.Location == CardLocation.MonsterZone && c.IsFaceup() && c != serket)
                                .OrderBy(c => ScoreOwnSerketFusionMaterial(c))
                                .ToList();

                            chosen = AvoidFirstPick(ownPool);
                        }

                        if (chosen == null)
                            chosen = cards.FirstOrDefault(c => c != null);

                        if (chosen != null)
                        {
                            pick.Add(chosen);
                            if (_pendingDefenseFusionSelectCalls == 1)
                                _pendingDefenseFusionFirstPickKey = Key(chosen);
                        }

                        ODLog("[OD.DefenseFusion.Mat]", $"target=Serket singlePick pick=" + string.Join(",", pick.Select(c => c.Id)));
                        return Util.CheckSelectCount(pick, cards, min, max);
                    }

                    // Single prompt that allows selecting both materials at once.
                    if (serket != null) pick.Add(serket);

                    if (_pendingDefenseFusionUseOpponent)
                    {
                        var enemyPool = cards
                            .Where(c => c != null
                                && IsEnemyFieldCard(c)
                                && c.Location == CardLocation.MonsterZone
                                && c.IsFaceup()
                                && c.IsMonster()
                                && c.Attack <= 2500)
                            .Select(c => new { Card = c, Score = ScoreEnemySerketFusionMaterial(c) })
                            .OrderByDescending(x => x.Score)
                            .ThenByDescending(x => Math.Max(0, x.Card.Attack))
                            .ToList();

                        if (enemyPool.Count > 0)
                            ODLog("[OD.DefenseFusion.Mat]", "enemyPool top=" + string.Join(",", enemyPool.Take(5).Select(x => x.Card.Id + ":" + x.Score)));

                        ClientCard enemy = AvoidFirstPick(enemyPool.Select(x => x.Card));
                        if (enemy != null) pick.Add(enemy);
                    }

                    if (pick.Count < 2)
                    {
                        ClientCard own = cards
                            .Where(c => c != null && IsMyFieldCard(c) && c.Location == CardLocation.MonsterZone && c.IsFaceup() && c != serket)
                            .OrderBy(c => ScoreOwnSerketFusionMaterial(c))
                            .FirstOrDefault();

                        if (own != null) pick.Add(own);
                    }

                    ODLog("[OD.DefenseFusion.Mat]", "target=Serket pick=" + string.Join(",", pick.Select(c => c.Id)));
                    return Util.CheckSelectCount(pick, cards, min, max);
                }

                if (_pendingDefenseFusionTargetId == CardId.DivineSerpentApophis)
                {
                    done = !singlePick ? true : (_pendingDefenseFusionSelectCalls >= 2);

                    List<ClientCard> temple = cards
                        .Where(c => c != null && c.IsMonster() && MentionsTempleOfTheKings(c))
                        .ToList();

                    List<ClientCard> own = temple
                        .Where(c => !IsEnemyFieldCard(c))
                        .OrderBy(c => ScoreOwnDefenseFusionMaterial(c))
                        .ToList();

                    List<ClientCard> opp = temple
                        .Where(c => IsEnemyFieldCard(c))
                        .OrderByDescending(ScoreEnemyThreat)
                        .ToList();

                    if (own.Count == 0)
                        return null;

                    if (singlePick)
                    {
                        ClientCard chosen;

                        if (_pendingDefenseFusionSelectCalls == 1)
                        {
                            // Force our first pick to be OUR field monster if possible.
                            chosen = own.FirstOrDefault(c => c.Location == CardLocation.MonsterZone) ?? own[0];
                            _pendingDefenseFusionFirstWasField = (chosen.Location == CardLocation.MonsterZone);
                        }
                        else
                        {
                            chosen = AvoidFirstPick(own);
                            if (chosen == null && _pendingDefenseFusionFirstWasField)
                                chosen = AvoidFirstPick(opp);
                        }

                        if (chosen == null)
                            chosen = cards.FirstOrDefault(c => c != null);

                        if (chosen != null)
                        {
                            pick.Add(chosen);
                            if (_pendingDefenseFusionSelectCalls == 1)
                                _pendingDefenseFusionFirstPickKey = Key(chosen);
                        }

                        ODLog("[OD.DefenseFusion.Mat]", $"target=Divine singlePick pick=" + string.Join(",", pick.Select(c => c.Id)));
                        return Util.CheckSelectCount(pick, cards, min, max);
                    }

                    // Single prompt that allows selecting both materials at once.
                    ClientCard first = own.FirstOrDefault(c => c.Location == CardLocation.MonsterZone) ?? own[0];
                    pick.Add(first);

                    ClientCard second = own.Skip(1).FirstOrDefault()
                        ?? ((first.Location == CardLocation.MonsterZone) ? opp.FirstOrDefault() : null);

                    if (second != null) pick.Add(second);

                    ODLog("[OD.DefenseFusion.Mat]", "target=Divine pick=" + string.Join(",", pick.Select(c => c.Id)));
                    return Util.CheckSelectCount(pick, cards, min, max);
                }

                // Unknown target -> conservative fallback.
                done = true;
                ODLog("[OD.DefenseFusion.Mat]", "unknown target -> fallback");
                pick.AddRange(cards.Where(c => c != null).Take(Math.Max(1, max)));
                return Util.CheckSelectCount(pick, cards, min, max);
            }
            finally
            {
                // Only clear pending state once the selection is complete.
                // For multi-step (max==1) we keep state after the first call.
                if (!singlePick || done)
                {
                    _pendingDefenseFusionSerketSeq = -1;
                    _pendingDefenseFusionUseOpponent = false;
                    _pendingDefenseFusionTargetId = 0;
                    _pendingDefenseFusionSelectCalls = 0;
                    _pendingDefenseFusionFirstWasField = false;
                    _pendingDefenseFusionFirstPickKey = null;
                }
            }
        }


        private bool MentionsTempleOfTheKings(ClientCard c)
        {
            if (c == null) return false;
            int id = c.Id;

            // Deck-relevant conservative list (safe, not exhaustive).
            return id == CardId.MercilessScorpionOfSerket
                || id == CardId.TheManWithTheMark
                || id == CardId.ApophisTheSerpent
                || id == CardId.ApophisTheSwampDeity
                || id == CardId.AngelStatueAzurune;
        }

        private int ScoreOwnDefenseFusionMaterial(ClientCard c)
        {
            if (c == null) return int.MaxValue;
            int id = c.Id;

            // Never spend core engines unless forced
            if (id == CardId.SilhouhatteRabbit) return 1000000;
            if (id == CardId.DivineSerpentApophis) return 900000;
            if (id == CardId.SuperStarslayerTYPHONSkyCrisis) return 900000;

            // Avoid spending protection / recursion bodies
            if (id == CardId.TheManWithTheMark) return 600000;

            // Avoid spending Apophis bodies that should convert into Divine later
            if (id == CardId.ApophisTheSerpent
                || id == CardId.ApophisTheSwampDeity
                || id == CardId.AngelStatueAzurune)
                return 300000;

            // Prefer spending Serket first when it's a material anyway
            if (id == CardId.MercilessScorpionOfSerket) return 1000;

            return 20000 + c.GetDefensePower();
        }


        // Divine Scorpion Beast of Serket: banish target on summon / battle start
        private bool DivineScorpionBeastOfSerketBanish()
        {
            if (Card.Location != CardLocation.MonsterZone || !Card.IsFaceup()) return false;

            int desc = Util.GetStringId(CardId.DivineScorpionBeastOfSerket, 0);
            if (ActivateDescription > 0 && ActivateDescription != desc) return false;

            // Need at least one legal target in prompt; selector will handle.
            _selectCtx = SelectCtx.DivineScorpionBanishTarget;
            ODLog("[OD.SerketFusion.Banish]", "trigger -> select target to banish");
            return true;
        }

        // Serket fusion specifically: we want to spend RECYCLABLE trap-monster bodies first and preserve Statue.
        private int ScoreOwnSerketFusionMaterial(ClientCard c)
        {
            if (c == null) return 9999999;

            // Preferred recyclable bodies (interaction pieces).
            if (c.Id == CardId.ApophisTheSwampDeity) return 10;
            if (c.Id == CardId.AngelStatueAzurune) return 20;
            if (c.Id == CardId.ApophisTheSerpent) return 30;

            // Low-impact starters / bodies.
            if (c.Id == CardId.TheManWithTheMark) return 100;

            // Avoid spending high-value engines unless we must.
            if (c.Id == CardId.SilhouhatteRabbit) return 800000;
            if (c.Id == CardId.DivineSerpentApophis) return 850000;

            // Hard-avoid Statue (your popper + grind piece).
            if (c.Id == CardId.StatueOfAnguishPattern) return 900000;

            // Default: prefer lower-value bodies first.
            return 1000 + c.GetDefensePower();
        }

        private int ScoreEnemySerketFusionMaterial(ClientCard c)
        {
            if (c == null) return int.MinValue;

            // Goal: when Defense of the Temple lets us use opponent monsters as material,
            // spend the *most dangerous* eligible enemy body (acts like a premium removal).
            //
            // Notes:
            // - Avoid pure DEF-based scoring (Link monsters often have 0 DEF).
            // - Prefer "problematic" / floodgate / Extra Deck threats first, then higher ATK.

            int score = 0;

            // 1) "Problematic" heuristic from core util (negates, floodgates, must-answer bosses, etc.).
            // Compare via key to avoid reference mismatches between prompt cards and live zone cards.
            ClientCard problem = Util.GetProblematicEnemyMonster(0, true);
            if (problem != null && GetCardKey(problem) == GetCardKey(c))
                score += 600000;

            // 2) Floodgates are always high priority to remove.
            if (c.IsFloodgate())
                score += 500000;

            // 3) Extra Deck weighting (generally higher impact than main-deck bodies).
            if (c.HasType(CardType.Link))
                score += 120000 + Math.Max(0, c.LinkCount) * 20000;
            if (c.HasType(CardType.Fusion | CardType.Synchro | CardType.Xyz | CardType.Ritual))
                score += 100000;

            // 4) Active threats > already-disabled threats.
            if (!c.IsDisabled())
                score += 25000;

            // 5) Stats: prioritize current ATK (Link monsters have 0 DEF).
            int atk = Math.Max(0, c.Attack);
            int def = Math.Max(0, c.Defense);
            int pow = Math.Max(atk, def);

            score += pow;

            // Minor tiebreaker: whatever the engine's generic power heuristic says.
            score += Math.Max(0, c.GetDefensePower()) / 2;

            return score;
        }


        // ------------------------------------------------------------
        // Threat answer / conversion: Link Spider -> S:P Little Knight
        // - Can happen in MP1 or MP2.
        // - Triggers if enemy has a "problem" card, OR if Super Poly resolved this turn and we're in MP2.
        // - Uses Extra Deck presence checks (not CheckRemainInDeck), so follow-ups work after Link Spider is summoned.
        // ------------------------------------------------------------
        private int ScoreLinkSpiderNormalMaterial(ClientCard c)
        {
            if (c == null) return int.MaxValue;

            // Avoid key bodies.
            if (c.IsCode(CardId.SilhouhatteRabbit)) return 1000000;
            if (c.IsCode(CardId.DivineSerpentApophis)) return 900000;
            if (c.IsCode(CardId.MercilessScorpionOfSerket)) return 800000;

            // Prefer Apophis normals (lowest value first).
            if (c.IsFaceup() && c.HasType(CardType.Normal))
            {
                if (c.Id == CardId.ApophisTheSerpent) return 0;
                if (c.Id == CardId.ApophisTheSwampDeity) return 1;
                if (c.Id == CardId.AngelStatueAzurune) return 2;
                if (IsApophis(c)) return 10;
            }

            return 100 + Math.Max(0, c.GetDefensePower());
        }

        private int ScoreSPLittleKnightSecondMaterial(ClientCard c)
        {
            if (c == null) return int.MaxValue;

            // Spend-first priorities (lowest score = most expendable)
            if (c.IsCode(CardId.TheWingedDragonOfRaSphereMode)) return 0;
            if (c.IsCode(CardId.LinkSpider)) return 5;
            if (c.IsCode(CardId.NibiruThePrimalBeing)) return 10;
            if (c.IsCode(CardId.GaruraWingsOfResonantLife)) return (Duel.Phase == DuelPhase.Main2 && _superPolyResolvedThisTurn) ? 0 : 15;
            if (c.IsCode(CardId.TheManWithTheMark)) return 25;

            // Protect key engine/boss pieces.
            if (c.IsCode(CardId.DivineSerpentApophis)) return 950000;
            if (c.IsCode(CardId.MercilessScorpionOfSerket)) return 900000;
            if (c.IsCode(CardId.DivineScorpionBeastOfSerket)) return 900000;
            if (c.IsCode(CardId.SuperStarslayerTYPHONSkyCrisis)) return 900000;
            if (c.IsCode(CardId.StatueOfAnguishPattern)) return 850000;
            if (c.IsCode(CardId.AngelStatueAzurune)) return 850000;

            // Rabbit is usually valuable (extension), but becomes expendable when we need S:P to answer a threat.
            if (c.IsCode(CardId.SilhouhatteRabbit))
                return EnemyHasDangerousCardForSP() ? 60 : 800000;

            // Otherwise, spend the lowest-impact effect body.
            return 100 + Math.Max(0, c.Attack);
        }


        private bool EnemyHasDangerousCardForSP()
        {
            // Shared heuristic for "should we convert into S:P now?"
            // Keep it conservative: only trigger when opponent has a real board problem.
            return Util.GetProblematicEnemyMonster(0, true) != null
                || Util.GetProblematicEnemySpell() != null
                || Enemy.MonsterZone.Any(c => c != null && c.IsFaceup() && c.IsFloodgate())
                || Enemy.MonsterZone.Any(c => c != null && c.IsFaceup()
                    && (c.HasType(CardType.Fusion) || c.HasType(CardType.Synchro) || c.HasType(CardType.Xyz) || c.HasType(CardType.Link))
                    && ScoreEnemyThreat(c) >= 4)
                || Enemy.MonsterZone.Any(c => c != null && c.IsFaceup() && Math.Max(0, c.Attack) >= 2500);
        }

        private bool PreferLinkSpiderConversionForSP()
        {
            if (!IsMyTurn() || !IsMainPhase()) return false;

            // If we already have Link Spider, no need to convert.
            if (Bot.GetMonsters().Any(m => m != null && m.IsFaceup() && m.IsCode(CardId.LinkSpider)))
                return false;

            // If Link Spider isn't available in Extra Deck, cannot convert.
            if (!Bot.ExtraDeck.Any(c => c != null && c.Id == CardId.LinkSpider))
                return false;

            // Need at least one convertible normal (Apophis trap-monster, etc.)
            var normal = Bot.GetMonsters()
                .Where(m => m != null && m.IsFaceup() && m.HasType(CardType.Normal))
                .OrderBy(ScoreLinkSpiderNormalMaterial)
                .FirstOrDefault();
            if (normal == null || ScoreLinkSpiderNormalMaterial(normal) >= 800000) return false;

            // Need at least one effect monster to pair with Link Spider after conversion.
            var effects = Bot.GetMonsters()
                .Where(m => m != null && m.IsFaceup() && m.HasType(CardType.Effect))
                .OrderBy(ScoreSPLittleKnightSecondMaterial)
                .ToList();
            if (effects.Count == 0) return false;

            // If we cannot already make S:P directly, we definitely want the conversion.
            if (effects.Count < 2)
            {
                if (Util.GetBotAvailZonesFromExtraDeck(new List<ClientCard> { normal }) == 0)
                    return false;
                return true;
            }

            // If making S:P directly would consume a protected/high-value body, prefer converting a normal into Link Spider.
            int secondScore = ScoreSPLittleKnightSecondMaterial(effects[1]);
            if (secondScore >= 850000)
            {
                if (Util.GetBotAvailZonesFromExtraDeck(new List<ClientCard> { normal }) == 0)
                    return false;
                return true;
            }

            return false;
        }


        private bool NeedSPLittleKnightLine()
        {
            if (!IsMyTurn() || !IsMainPhase()) return false;

            // Don't attempt if we've already made S:P.
            if (Bot.GetMonsters().Any(m => m != null && m.IsFaceup() && m.IsCode(CardId.SPLittleKnight)))
                return false;

            // Require S:P availability in Extra Deck.
            if (!Bot.ExtraDeck.Any(c => c != null && c.Id == CardId.SPLittleKnight))
                return false;

            bool enemyProblem = EnemyHasDangerousCardForSP();
            bool mp2Conversion = (Duel.Phase == DuelPhase.Main2) && _superPolyResolvedThisTurn;
            if (!enemyProblem && !mp2Conversion) return false;

            // Need at least 2 bodies in total to link.
            if (Bot.GetMonsterCount() < 2) return false;

            int effectTotal = Bot.GetMonsters().Count(m => m != null && m.IsFaceup() && m.HasType(CardType.Effect));

            // Direct route: 2 effect monsters on field (even if one is high-value; we'll only do it when needed).
            if (effectTotal >= 2)
                return true;

            // If we already have Link Spider, we only need one more effect.
            bool haveLinkSpider = Bot.GetMonsters().Any(m => m != null && m.IsFaceup() && m.IsCode(CardId.LinkSpider));
            if (haveLinkSpider && effectTotal >= 2)
                return true;

            // Otherwise: convert a normal into Link Spider, then pair with an existing effect.
            bool haveNormal = Bot.GetMonsters().Any(m =>
                m != null && m.IsFaceup() && m.HasType(CardType.Normal)
                && ScoreLinkSpiderNormalMaterial(m) < 800000);

            bool haveAnyEffect = effectTotal >= 1;
            if (!haveAnyEffect) return false;

            return haveNormal && Bot.ExtraDeck.Any(c => c != null && c.Id == CardId.LinkSpider);
        }



        private bool LinkSpiderSummonForSPLittleKnight()
        {
            if (!NeedSPLittleKnightLine()) return false;
            if (Card == null || Card.Id != CardId.LinkSpider) return false;

            // If we already have a face-up Link Spider, don't try to make a second one.
            if (Bot.GetMonsters().Any(m => m != null && m.IsFaceup() && m.IsCode(CardId.LinkSpider)))
                return false;

            // Only summon Link Spider if we need the normal->effect conversion (or if it preserves key bodies).
            if (!PreferLinkSpiderConversionForSP())
                return false;

            // Pick a lowest-impact normal body.
            var normal = Bot.GetMonsters()
                .Where(m => m != null && m.IsFaceup() && m.HasType(CardType.Normal))
                .OrderBy(ScoreLinkSpiderNormalMaterial)
                .FirstOrDefault();

            if (normal == null || ScoreLinkSpiderNormalMaterial(normal) >= 800000) return false;

            // Ensure we will still have at least one effect monster on field to pair with Link Spider for S:P.
            bool haveOtherEffect = Bot.GetMonsters().Any(m =>
                m != null && m.IsFaceup() && m.HasType(CardType.Effect));

            if (!haveOtherEffect) return false;

            // Zone legality (EMZ or linked zone). Important when EMZ is occupied.
            if (Util.GetBotAvailZonesFromExtraDeck(new List<ClientCard> { normal }) == 0)
                return false;

            AI.SelectMaterials(normal);
            ODLog("[OD.SPLine]", "LinkSpider mats=[" + normal.Id + "] spResolved=" + (_superPolyResolvedThisTurn ? 1 : 0));
            return true;
        }

        private bool SPLittleKnightSpSummon()
        {
            if (!NeedSPLittleKnightLine()) return false;
            if (Card == null || Card.Id != CardId.SPLittleKnight) return false;

            // If we can preserve key bodies by converting a normal into Link Spider first, do that.
            if (PreferLinkSpiderConversionForSP())
            {
                ODLog("[OD.SPLine]", "defer S:P: prefer LinkSpider conversion first");
                return false;
            }

            var pool = Bot.GetMonsters()
                .Where(m => m != null && m.IsFaceup() && m.HasType(CardType.Effect))
                .OrderBy(ScoreSPLittleKnightSecondMaterial)
                .ToList();

            if (pool.Count < 2) return false;

            // Prefer using Link Spider if present (lowest-impact effect).
            ClientCard linkSpider = pool.FirstOrDefault(m => m.IsCode(CardId.LinkSpider));
            List<ClientCard> mats;

            if (linkSpider != null)
            {
                ClientCard second = pool.FirstOrDefault(m => m != linkSpider);
                if (second == null) return false;
                mats = new List<ClientCard> { linkSpider, second };
            }
            else
            {
                mats = pool.Take(2).ToList();
            }

            // Zone legality for Link-2 summon.
            if (Util.GetBotAvailZonesFromExtraDeck(mats) == 0)
                return false;

            AI.SelectMaterials(mats);
            ODLog("[OD.SPLine]", "S:P summon mats=[" + mats[0].Id + "," + mats[1].Id + "] spResolved=" + (_superPolyResolvedThisTurn ? 1 : 0));
            return true;
        }
        private bool SPLittleKnightBanishOnSummon()
        {
            if (Card == null || Card.Id != CardId.SPLittleKnight) return false;
            if (DefaultCheckWhetherCardIsNegated(Card)) return false;

            // Link Summon trigger (StringId 0). Some cores report ActivateDescription as -1/0 for delayed triggers.
            int desc0 = Util.GetStringId(CardId.SPLittleKnight, 0);
            if (ActivateDescription > 0 && ActivateDescription != desc0)
            {
                // Light debug (only when we're plausibly in the trigger window).
                if (IsMyTurn() && IsMainPhase())
                    ODLog("[OD.SPLK]", $"on-summon skip: desc={ActivateDescription} want={desc0}");
                return false;
            }

            // Prefer enemy field (monster > backrow), then enemy GY.
            ClientCard best = Enemy.GetMonsters()
                .Where(c => c != null && !c.IsShouldNotBeTarget())
                .OrderByDescending(ScoreEnemyThreat)
                .ThenByDescending(c => c.IsFaceup() ? 1 : 0)
                .ThenByDescending(c => Math.Max(0, c.GetDefensePower()))
                .FirstOrDefault();

            if (best == null)
            {
                // Can target facedown S/T too; ScoreEnemySpellTrapThreat already favors face-up floodgates/engines.
                best = Enemy.SpellZone
                    .Take(6)
                    .Where(c => c != null && !c.IsShouldNotBeTarget())
                    .OrderByDescending(ScoreEnemySpellTrapThreat)
                    .FirstOrDefault();
            }

            if (best == null)
            {
                best = Enemy.Graveyard
                    .Where(c => c != null && !c.IsShouldNotBeTarget())
                    .OrderByDescending(c => (c.IsMonster() ? 100000 : 0) + Math.Max(0, c.Attack) + c.GetDefensePower())
                    .FirstOrDefault();
            }

            if (best == null) return false;

            AI.SelectCard(best);
            ODLog("[OD.SPLK]", $"banish on-summon target={best.Id} desc={ActivateDescription}");
            return true;
        }

        private bool SPLittleKnightQuickBanish()
        {
            if (Card == null || Card.Id != CardId.SPLittleKnight) return false;
            if (DefaultCheckWhetherCardIsNegated(Card)) return false;

            // Quick effect on opponent chaining (StringId 1).
            if (ActivateDescription != Util.GetStringId(CardId.SPLittleKnight, 1)) return false;

            // Keep this conservative: only fire when a real problem exists.
            ClientCard last = Util.GetLastChainCard();
            bool chainIsThreat = last != null && IsEnemyFieldCard(last) && (last.IsFloodgate() || last.IsMonsterShouldBeDisabledBeforeItUseEffect());

            ClientCard best = Enemy.MonsterZone
                .Where(c => c != null && c.IsFaceup() && !c.IsShouldNotBeTarget())
                .OrderByDescending(ScoreEnemyThreat)
                .FirstOrDefault();

            if (!chainIsThreat && (best == null || ScoreEnemyThreat(best) < 6))
                return false;

            if (best == null) return false;

            // Target ourselves + their best threat (temporary banish, also dodges).
            AI.SelectCard(new List<ClientCard> { Card, best });
            ODLog("[OD.SPLK]", "quick banish targets self + " + best.Id + " (chainThreat=" + (chainIsThreat ? 1 : 0) + ")");
            return true;
        }

        // NEW: TY-PHON XYZ Combo (E)
        private bool XyzSummonTyphon()
        {
            if (Card == null || Card.Id != CardId.SuperStarslayerTYPHONSkyCrisis) return false;

            // Only attempt on our turn in Main Phase.
            if (!IsMyTurn() || !IsMainPhase()) return false;

            // Throttle: at most once per our turn.
            if (_didXyzSummonTyphon) return false;

            // Condition: opponent Special Summoned 2+ monsters from the Extra Deck THIS turn OR LAST turn.
            bool cond = (_enemySpSummonFromExtraThisTurn >= 2) || (_enemySpSummonFromExtraLastTurn >= 2);
            if (!cond) return false;

            // Prefer as a "we got stopped" fallback, not when our engine already succeeded.
            if (_didDivineSummon || _didSerketSummon || _didRabbitSummon) return false;

            // If it's already on field, skip.
            if (Bot.HasInMonstersZone(CardId.SuperStarslayerTYPHONSkyCrisis)) return false;

            // Need at least one meaningful enemy monster to bounce.
            ClientCard bounce = Enemy.MonsterZone
                .Where(c => c != null && c.IsFaceup())
                .OrderByDescending(ScoreEnemyThreat)
                .FirstOrDefault();
            if (bounce == null) return false;

            int bounceScore = ScoreEnemyThreat(bounce);
            int bouncePower = bounce.GetDefensePower();
            bool bounceIsProblem = bounce.IsFloodgate() || bounce.IsMonsterShouldBeDisabledBeforeItUseEffect() || bouncePower >= 2800;
            if (!bounceIsProblem) return false;
// TY-PHON alt-XYZ material is restricted to the highest ATK face-up monster(s) we control (Lua mfilter).
            List<ClientCard> faceup = Bot.MonsterZone.Where(c => c != null && c.IsFaceup()).ToList();
            if (faceup.Count == 0) return false;

            int maxAtk = faceup.Max(c => c.Attack);
            List<ClientCard> highest = faceup.Where(c => c.Attack == maxAtk).ToList();
            if (highest.Count == 0) return false;

            // If using TY-PHON would force us to cash in a key engine body, only allow it for a high-impact bounce.
            bool forcedKey = highest.All(IsKeyMonsterToKeep);
            if (forcedKey && bouncePower < 3200 && !bounce.IsFloodgate() && !bounce.IsMonsterShouldBeDisabledBeforeItUseEffect())
            {
                ODLog("[OD.Typhon.SS]", "HOLD: forcedKey material; power=" + bouncePower + " score=" + bounceScore + " target=" + bounce.Id);
                return false;
            }

            // Extra Deck summon zone availability (EMZ / MMZ rules).
            if (Util.GetBotAvailZonesFromExtraDeck(highest) == 0) return false;

            // Material selection is handled via OnSelectCard with SelectCtx.TyphonMaterial.
            // IMPORTANT: do not ever select the bounce target here; that's for the ignition effect later.
            _selectCtx = SelectCtx.TyphonMaterial;
            _didXyzSummonTyphon = true;

            ODLog("[OD.Typhon.SS]",
                "xyz summon (enemyEDSp this=" + _enemySpSummonFromExtraThisTurn +
                ", last=" + _enemySpSummonFromExtraLastTurn +
                ", forcedKey=" + (forcedKey ? 1 : 0) +
                ", bounce=" + bounce.Id + ", power=" + bouncePower + ", score=" + bounceScore + ")");
            return true;
        }

        
        // ============================================================
        // Zeus — emergency board reset line
        // ============================================================
        private bool Zeus_Summon()
        {
            if (Duel.Player != 0) return false;
            if (Duel.Phase != DuelPhase.Main2) return false;

            // The engine will only offer this summon when Zeus is actually legal
            // (an Xyz you control battled this turn and remains on field in Main2).
            List<ClientCard> xyz = Bot.GetMonsters()
                .Where(m => m != null && m.IsFaceup() && m.HasType(CardType.Xyz) && !m.IsCode(CardId.DivineArsenalAAZEUSSkyThunder))
                .ToList();
            if (xyz.Count == 0) return false;

            if (!ShouldZeusResetBoard()) return false;

            // Prefer overlaying on the least-valuable Xyz that still has at least 1 material.
            ClientCard pick = xyz
                .Where(m => m.Overlays != null && m.Overlays.Count >= 1)
                .OrderBy(m => ScoreOurXyzForZeusOverlay(m))
                .FirstOrDefault() ?? xyz.OrderBy(m => ScoreOurXyzForZeusOverlay(m)).First();

            AI.SelectCard(pick);

            ODLog("[OD.Zeus.SS]",
                "overlay=" + pick.Id +
                " ov=" + (pick.Overlays?.Count ?? 0) +
                " enemy=" + GetEnemyFieldCount());
            return true;
        }

        private bool Zeus_Wipe()
        {
            if (Card == null || Card.Id != CardId.DivineArsenalAAZEUSSkyThunder) return false;
            if (Card.Location != CardLocation.MonsterZone || !Card.IsFaceup()) return false;
            if (DefaultCheckWhetherCardIsNegated(Card)) return false;

            // Cost is "detach 2 materials". Overlays are card IDs (List<int>).
            if (Card.Overlays == null || Card.Overlays.Count < 2) return false;

            if (!ShouldZeusResetBoard()) return false;

            // We don't have overlay ClientCards here (only IDs), so just detach 2 IDs.
            // (Order usually doesn't matter for Zeus cost.)
            List<int> ovs = Card.Overlays.ToList();
            List<int> detach = ovs.Take(2).ToList();

            AI.SelectCard(detach);

            ODLog("[OD.Zeus.Wipe]",
                "detach=" + string.Join(",", detach) +
                " ov=" + Card.Overlays.Count +
                " enemy=" + GetEnemyFieldCount());
            return true;
        }

        private int GetEnemyFieldCount()
        {
            int mons = Enemy.GetMonsters().Count(c => c != null);
            int st = Enemy.SpellZone.Take(6).Count(c => c != null);
            return mons + st;
        }

        private int GetBotOtherFieldCountExcludingZeus()
        {
            int mons = Bot.GetMonsters().Count(c => c != null && c.IsFaceup() && !c.IsCode(CardId.DivineArsenalAAZEUSSkyThunder));
            int st = Bot.SpellZone.Take(6).Count(c => c != null);
            return mons + st;
        }

        private bool ShouldZeusResetBoard()
        {
            // Conservative: only when opponent has multiple live cards and we're not preserving a strong "Temple + set" engine.
            int enemyTotal = GetEnemyFieldCount();
            if (enemyTotal <= 1) return false;

            int enemyThreat = 0;
            foreach (ClientCard m in Enemy.GetMonsters().Where(c => c != null && c.IsFaceup()))
                enemyThreat += ScoreEnemyThreat(m);
            foreach (ClientCard st in Enemy.SpellZone.Take(6).Where(c => c != null))
                enemyThreat += ScoreEnemySpellTrapThreat(st);

            bool haveTemple = Bot.HasInSpellZone(CardId.TempleOfTheKings, true, true);
            int botOther = GetBotOtherFieldCountExcludingZeus();

            // If we have Temple online and a bunch of our own resources, avoid wiping unless enemy is truly scary.
            if (haveTemple && botOther >= 3 && enemyThreat < 80000) return false;

            // Primary triggers
            if (enemyThreat >= 90000) return true;                 // big threat (bosses/floodgates)
            if (enemyTotal >= 3 && botOther <= 3) return true;     // behind on board count
            if (Enemy.GetMonsters().Any(c => c != null && c.IsFaceup() && c.Attack >= 2800) && enemyTotal >= 2) return true;

            return false;
        }

        private int ScoreOurXyzForZeusOverlay(ClientCard xyz)
        {
            if (xyz == null) return int.MaxValue;

            // Prefer converting low-impact utility Xyz first; keep Typhon if possible.
            if (xyz.IsCode(CardId.SuperStarslayerTYPHONSkyCrisis)) return 50000;

            // Lower ATK / fewer materials => more expendable.
            int atk = Math.Max(0, xyz.Attack);
            int ov = xyz.Overlays?.Count ?? 0;
            return atk + (ov * 2000);
        }

        private int ScoreOverlayDetachCost(ClientCard ov)
        {
            if (ov == null) return int.MinValue;

            // Prefer detaching "small" materials; try to keep boss materials if present.
            if (ov.IsCode(CardId.SuperStarslayerTYPHONSkyCrisis)) return 999999;

            int atk = Math.Max(0, ov.Attack);
            int lvl = Math.Max(0, ov.Level);
            return atk + (lvl * 10);
        }

        private bool ShouldHoldTyphonMaterialsForZeus(ClientCard typhon)
        {
            if (typhon == null) return false;

            int ov = typhon.Overlays?.Count ?? 0;

            // If we still have 3+ overlays, detaching 1 keeps ZEUS wipe online (needs 2).
            if (ov >= 3) return false;

            // Only relevant if ZEUS exists in our Extra Deck.
            if (!Bot.ExtraDeck.Any(c => c != null && c.Id == CardId.DivineArsenalAAZEUSSkyThunder))
                return false;

            // Preserve overlays when a wipe is desirable, or when the enemy has multiple cards and we have exactly 2 overlays (keep option).
            if (ShouldZeusResetBoard()) return true;

            int enemyTotal = GetEnemyFieldCount();
            if (ov == 2 && enemyTotal >= 2) return true;

            return false;
        }

        private bool TyphonEffect()
        {
            if (Card == null || Card.Id != CardId.SuperStarslayerTYPHONSkyCrisis) return false;
            if (Card.Location != CardLocation.MonsterZone || !Card.IsFaceup()) return false;

            // Ignition -> only on our Main Phase.
            if (!IsMyTurn() || !IsMainPhase()) return false;

            if (DefaultCheckWhetherCardIsNegated(Card)) return false;

            // Effect: detach 1 material to return 1 monster on the field to the hand (non-targeting in Lua).
            if (Card.Overlays == null || Card.Overlays.Count == 0) return false;

            // Never fire if there are no enemy monsters (prevents accidental self/own bounces).
            if (!Enemy.MonsterZone.Any(c => c != null)) return false;

            // Preserve overlays for potential ZEUS wipe when we only have <=2 materials.
            if (ShouldHoldTyphonMaterialsForZeus(Card))
            {
                ODLog("[OD.Typhon.Effect]", "skip: preserve overlays for Zeus (ov=" + (Card.Overlays?.Count ?? 0) + ")");
                return false;
            }

            // Only bounce when it meaningfully improves our position.
            ClientCard best = Enemy.MonsterZone
                .Where(c => c != null && c.Location == CardLocation.MonsterZone)
                .OrderByDescending(ScoreTyphonBounceTarget)
                .FirstOrDefault();

            if (best == null) return false;

            int bestPower = best.GetDefensePower();
            bool worth = best.IsFloodgate() || best.IsMonsterShouldBeDisabledBeforeItUseEffect() || bestPower >= 2800;

            // If we still have only 1 overlay, be extra conservative (ZEUS would be offline after detaching).
            if ((Card.Overlays?.Count ?? 0) <= 1 && !best.IsFloodgate() && bestPower < 3200)
                worth = false;

            if (!worth)
            {
                ODLog("[OD.Typhon.Effect]", "skip: low impact bounce (best=" + best.Id + " defp=" + bestPower + " ov=" + (Card.Overlays?.Count ?? 0) + ")");
                return false;
            }

            // Route the return-to-hand prompt so we never pick our own monsters or TY-PHON itself.
            _selectCtx = SelectCtx.TyphonBounceTarget;

            AI.SelectCard(best);
            ODLog("[OD.Typhon.Effect]", "activate -> preselect enemy=" + best.Id + " (defp=" + bestPower + ", ov=" + (Card.Overlays?.Count ?? 0) + ")");
            return true;
        }

        // Predaplant Triphyoverutum: negate opponent's Extra Deck Special Summon (Quick Effect)
        private bool TriphyoverutumNegateSummon()
        {
            if (Card == null || Card.Id != CardId.PredaplantTriphyoverutum) return false;
            if (Card.Location != CardLocation.MonsterZone || !Card.IsFaceup()) return false;
            if (DefaultCheckWhetherCardIsNegated(Card)) return false;

            int desc = Util.GetStringId(CardId.PredaplantTriphyoverutum, 0);
            if (ActivateDescription > 0 && ActivateDescription != desc) return false;

            // No target prompt needed: the engine supplies the summoned group.
            ODLog("[OD.Triphy.NegateSummon]", "activate (desc=" + ActivateDescription + ")");
            return true;
        }

        // Predaplant Triphyoverutum: revive itself from GY (Ignition)
        private bool TriphyoverutumReviveFromGY()
        {
            if (Card == null || Card.Id != CardId.PredaplantTriphyoverutum) return false;
            if (Card.Location != CardLocation.Grave) return false;

            // Ignition -> only on our Main Phase.
            if (!IsMyTurn() || !IsMainPhase()) return false;
            if (!HasFreeMonsterZone()) return false;

            // Position is always DEF per card text.
            AI.SelectPosition(CardPosition.FaceUpDefence);

            ODLog("[OD.Triphy.Revive]", "SS from GY");
            return true;
        }

        private IList<ClientCard> SelectDivineScorpionBanishTarget(IList<ClientCard> cards, int min, int max)
        {
            // Enhanced banishing logic: prioritize high ATK monsters from field, then GY
            var enemyField = cards
                .Where(c => c != null && IsEnemyFieldCard(c) && c.IsMonster() && c.IsFaceup())
                .OrderByDescending(c => c.Attack)
                .ToList();

            var enemyGY = cards
                .Where(c => c != null && IsEnemyFieldCard(c) && c.Location == CardLocation.Grave && c.IsMonster())
                .OrderByDescending(c => c.Attack)
                .ToList();

            var pick = new List<ClientCard>();

            // Priority: High ATK field monster > Any field monster > High ATK GY > Any GY
            ClientCard chosen = enemyField.FirstOrDefault()
                ?? enemyGY.FirstOrDefault()
                ?? cards.FirstOrDefault(c => c != null);

            if (chosen != null) pick.Add(chosen);

            ODLog("[OD.SerketFusion.Banish]", "pick=" + string.Join(",", pick.Select(c => c.Id))
                + " (ATK=" + (chosen?.Attack ?? 0) + ")");
            return Util.CheckSelectCount(pick, cards, min, max);
        }

        private IList<ClientCard> SelectTyphonMaterial(IList<ClientCard> cards, int min, int max)
        {
            // TY-PHON alt-XYZ material is restricted to our highest ATK face-up monster(s).
            // The engine usually pre-filters, but keep this selector rule-correct and conservative.

            List<ClientCard> pool = cards?.Where(c => c != null).ToList() ?? new List<ClientCard>();
            if (pool.Count == 0) return null;

            // Defensive: if a detach prompt leaks through (overlay cards), just pick overlays deterministically.
            bool overlayPrompt = pool.Any(c => c.Location == CardLocation.Overlay);
            if (overlayPrompt)
            {
                List<ClientCard> pickOv = pool.Take(max).ToList();
                if (pickOv.Count < min) return null;

                ODLog("[OD.Typhon.Material]", "overlay pick=" + string.Join(",", pickOv.Select(c => c.Id)));
                return Util.CheckSelectCount(pickOv, cards, min, max);
            }

            // Prefer face-up monsters as materials (should match the Lua mfilter).
            List<ClientCard> faceup = pool.Where(c => c.Location == CardLocation.MonsterZone && c.IsFaceup()).ToList();
            if (faceup.Count > 0) pool = faceup;

            int maxAtk = pool.Max(c => c.Attack);
            List<ClientCard> highest = pool.Where(c => c.Attack == maxAtk).ToList();
            if (highest.Count == 0) highest = pool;

            // Do NOT throw away key engines if avoidable (but if all highest are key, we are forced by the card).
            List<ClientCard> safe = highest
                .Where(c => !IsKeyMonsterToKeep(c))
                .OrderBy(c => c.Attack)
                .ThenBy(c => c.GetDefensePower())
                .ThenBy(c => c.Id)
                .ToList();

            IEnumerable<ClientCard> candidates =
                (safe.Count > 0)
                    ? (IEnumerable<ClientCard>)safe
                    : highest.OrderBy(c => c.Attack)
                             .ThenBy(c => c.GetDefensePower())
                             .ThenBy(c => c.Id);

            List<ClientCard> pick = new List<ClientCard>();
            foreach (ClientCard c in candidates)
            {
                if (pick.Count >= max) break;
                pick.Add(c);
            }

            if (pick.Count < min) return null;

            ODLog("[OD.Typhon.Material]", "pick=" + string.Join(",", pick.Select(c => c.Id)) +
                  " (maxAtk=" + maxAtk + ", forcedKey=" + pick.Count(c => IsKeyMonsterToKeep(c)) + ")");

            return Util.CheckSelectCount(pick, cards, min, max);
        }

        private int ScoreTyphonBounceTarget(ClientCard c)
        {
            if (c == null) return int.MinValue;

            // Non-targeting bounce: prioritize floodgates / must-answer monsters, then raw DEF power.
            int score = 0;
            if (c.IsFloodgate()) score += 1000000;
            if (c.IsMonsterShouldBeDisabledBeforeItUseEffect()) score += 900000;

            score += c.GetDefensePower();

            // Extra Deck bodies are usually higher impact to remove temporarily.
            if (c.HasType(CardType.Xyz) || c.HasType(CardType.Link) || c.HasType(CardType.Synchro) || c.HasType(CardType.Fusion))
                score += 20000;

            // Mild ATK bonus.
            score += Math.Max(0, c.Attack);

            return score;
        }

        private IList<ClientCard> SelectTyphonBounceTarget(IList<ClientCard> cards, int min, int max, bool cancelable)
        {
            List<ClientCard> pool = cards?.Where(c => c != null).ToList() ?? new List<ClientCard>();
            if (pool.Count == 0) return null;

            // Prefer enemy monsters; never choose TY-PHON itself.
            List<ClientCard> enemy = pool
                .Where(c => IsEnemyFieldCard(c) && c.Location == CardLocation.MonsterZone && c.Id != CardId.SuperStarslayerTYPHONSkyCrisis)
                .OrderByDescending(ScoreTyphonBounceTarget)
                .ThenByDescending(c => c.GetDefensePower())
                .ThenByDescending(c => c.Attack)
                .ToList();

            if (enemy.Count > 0)
            {
                ClientCard best = enemy[0];
                List<ClientCard> pick = new List<ClientCard> { best };
                ODLog("[OD.Typhon.BounceTarget]", "pick enemy=" + best.Id + " (atk=" + best.Attack + ", defp=" + best.GetDefensePower() + ")");
                return Util.CheckSelectCount(pick, cards, min, max);
            }

            // Fallback: if the engine offered no enemy legal target (rare), pick the least harmful non-TY-PHON.
            List<ClientCard> safeFallback = pool
                .Where(c => c.Id != CardId.SuperStarslayerTYPHONSkyCrisis)
                .OrderBy(c => c.GetDefensePower())
                .ThenBy(c => Math.Max(0, c.Attack))
                .ThenBy(c => c.Id)
                .ToList();

            ClientCard fb = safeFallback.FirstOrDefault() ?? pool.First();
            ODLog("[OD.Typhon.BounceTarget]", "[FORCED] no enemy target offered -> pick=" + fb.Id);
            return Util.CheckSelectCount(new List<ClientCard> { fb }, cards, min, max);
        }



        // ------------------------------------------------------------
        // Batch 3: Tech / comeback cards
        // ------------------------------------------------------------

        private bool MaxxCActivate()
        {
            if (_blockMaxxCThisTurnFromRetaliating)
            {
                ODLog("[OD.MaxxC][BLOCK]", "added by Retaliating \"C\" this turn -> hold for next turn");
                return false;
            }
            return DefaultMaxxC();
        }

        private bool DrollLockBirdActivate()
        {
            if (Card == null || Card.Id != CardId.DrollLockBird) return false;

            // Droll triggers off a custom EVENT_TO_HAND hook, not "enemy chained".
            // If the engine is prompting us here, the condition is already satisfied (opponent added from Deck to hand, not Draw Phase).
            if (Card.Location != CardLocation.Hand) return false;

            // Once per turn (for us). Reset in OnNewTurn().
            if (_didDrollThisTurn) return false;

            if (DefaultCheckWhetherCardIsNegated(Card)) return false;

            _didDrollThisTurn = true;
            ODLog("[OD.Droll]", "activate");
            return true;
        }

        private bool RetaliatingCActivate()
        {
            if (DefaultCheckWhetherCardIsNegated(Card)) return false;
            if (Card.Location != CardLocation.Hand) return false;
            if (!HasFreeMonsterZone()) return false;
            if (Duel.LastChainPlayer != 1) return false;
            ODLog("[OD.RetaliatingC]", "special summon");
            return true;
        }


        private bool RetaliatingCSearchEarthInsect()
        {
            if (DefaultCheckWhetherCardIsNegated(Card)) return false;
            if (Card.Location != CardLocation.Grave) return false;

            // Trigger: this card was sent from field to GY.
            // Action: search an EARTH Insect with 1500 or less ATK (engine filters candidates).
            _selectCtx = SelectCtx.RetaliatingSearchEarthInsect;
            ODLog("[OD.RetaliatingC.Search]", "trigger -> search EARTH Insect <=1500");
            return true;
        }
        private IList<ClientCard> SelectRetaliatingSearchEarthInsect(IList<ClientCard> cards, int min, int max)
        {
            if (cards == null || cards.Count == 0) return null;

            // Build a deterministic selection that always satisfies min/max via Util.CheckSelectCount contract.
            List<ClientCard> pool = cards.Where(c => c != null).ToList();
            if (pool.Count == 0) return null;

            // Prefer Maxx "C" if offered, otherwise take the first available.
            ClientCard chosen = pool.FirstOrDefault(c => c.Id == CardId.MaxxC) ?? pool[0];
            if (chosen == null) return null;

            List<ClientCard> selected = new List<ClientCard> { chosen };

            // If prompt requires more than 1, fill with remaining cards deterministically.
            // (Do NOT exceed max, and let Util.CheckSelectCount enforce final bounds.)
            int target = Math.Max(min, 1);
            if (max > 0) target = Math.Min(target, max);

            if (selected.Count < target)
            {
                foreach (ClientCard c in pool)
                {
                    if (c == null || c == chosen) continue;
                    selected.Add(c);
                    if (selected.Count >= target) break;
                }
            }

            if (chosen.Id == CardId.MaxxC)
            {
                _blockMaxxCThisTurnFromRetaliating = true;
                ODLog("[OD.RetaliatingC.Search]", "pick=MaxxC -> block MaxxC this turn");
            }
            else
            {
                ODLog("[OD.RetaliatingC.Search]", "pick=" + chosen.Id);
            }

            return Util.CheckSelectCount(selected, cards, min, max);
        }

        private bool ContactCActivate()
        {
            if (Card == null || Card.Id != CardId.ContactC) return false;
            if (DefaultCheckWhetherCardIsNegated(Card)) return false;
            if (Card.Location != CardLocation.Hand) return false;

            // Contact "C" only matters on the opponent's turn (it is a tempo/ED lock handtrap).
            if (Duel.Player != 1) return false;

            // Need an open zone on opponent field.
            if (!Enemy.MonsterZone.Any(z => z == null)) return false;

            // Avoid double Contact "C".
            if (Enemy.MonsterZone.Any(z => z != null && z.Id == CardId.ContactC)) return false;

            // Fire early; if opponent already has a developed board, this is often too late.
            if (CountEnemyMonsters() > 2) return false;

            ODLog("[OD.ContactC]", "special summon to enemy field (lock extra deck)");
            return true;
        }

        private bool DominusPurgeActivate()
        {
            if (DefaultCheckWhetherCardIsNegated(Card)) return false;

            bool fromHand = Card.Location == CardLocation.Hand;
            if (fromHand)
            {
                // Huge drawback (attribute lock for the rest of the Duel). Keep very conservative.
                // Only use from hand as an emergency stop when we're likely to die.
                int enemyAtk = Util.GetTotalAttackingMonsterAttack(1);
                if (enemyAtk < Bot.LifePoints)
                    return false;
            }

            // Engine already ensures the activation is legal (i.e., the chained effect adds from Deck to hand).
            ODLog("[OD.Dominus]", "activate" + (fromHand ? " (from-hand emergency)" : ""));

            // If Purge will also destroy the negated card (we control any Trap in GY),
            // reserve that target so Divine/Statue/Rabbit pops don't waste on the same card.
            ClientCard chainCard = Util.GetLastChainCard();
            bool willDestroy = Bot.Graveyard.Any(c => c != null && c.HasType(CardType.Trap));
            if (willDestroy && chainCard != null && IsEnemyFieldCard(chainCard))
            {
                string key = GetCardKey(chainCard);
                if (!string.IsNullOrEmpty(key) && !_popUsedTargetKeysThisChain.Contains(key))
                {
                    _popUsedTargetKeysThisChain.Add(key);
                    ODLog("[OD.Dominus]", "reserve pop key=" + key + " id=" + chainCard.Id);
                }
            }

            return DefaultTrap();
        }
        private bool LightningStormActivate()
        {
            if (Card == null) return false;
            if (!IsMyTurn() || !IsMainPhase1()) return false;
            if (!BotControlsNoFaceupCards()) return false;
            if (DefaultCheckWhetherCardIsNegated(Card)) return false;

            int enemyMonsters = CountEnemyMonsters();
            int enemyBackrow = CountEnemyBackrow();

            // Don't waste on a single card unless it's clearly problematic.
            if (enemyMonsters + enemyBackrow <= 1)
            {
                if (Util.GetProblematicEnemyMonster(0, true) == null && Util.GetProblematicEnemySpell() == null)
                    return false;
            }

            int opt;
            if (enemyMonsters == 0) opt = 1; // only S/T
            else if (enemyBackrow == 0) opt = 0; // only monsters
            else if (enemyBackrow >= 2 && enemyBackrow >= enemyMonsters) opt = 1;
            else opt = 0;

            // Choose option explicitly. Any placement prompt (rare for activation) is handled by OnSelectPlace.
            AI.SelectOption(opt);

            ODLog("[OD.LightningStorm]", "activate opt=" + opt + " enemyM=" + enemyMonsters + " enemyST=" + enemyBackrow);
            return true;
        }

        private bool ComputeEvenlyPlan(out bool fromHand)
        {
            fromHand = false;

            if (!IsMyTurn() || !IsMainPhase1()) return false;
            if (_didEvenlyActivateThisTurn) return false;

            int enemyField = CountEnemyCardsOnField();
            int botField = CountBotCardsOnField();
            bool hasEvenlyInHand = Bot.HasInHand(CardId.EvenlyMatched);
            bool setEvenly = BotHasFacedownInSpellZone(CardId.EvenlyMatched);

            // Hand line: legal only if we control no cards.
            if (hasEvenlyInHand && botField == 0 && enemyField > 0)
            {
                fromHand = true;
                return true;
            }

            // Set line: if already set, or if setting now still leaves enemy > us at Battle End.
            if (setEvenly)
            {
                return enemyField > botField;
            }

            if (hasEvenlyInHand && botField > 0)
            {
                int projectedBotFieldAtBattleEnd = botField + 1; // set this copy in Main1
                return enemyField > projectedBotFieldAtBattleEnd;
            }

            return false;
        }

        private void RefreshEvenlyPlan()
        {
            bool fromHand;
            _planEvenlyThisTurn = ComputeEvenlyPlan(out fromHand);
            _planEvenlyFromHand = _planEvenlyThisTurn && fromHand;
        }

        private bool EvenlyMatchedSetCheck()
        {
            if (Card == null || Card.Id != CardId.EvenlyMatched) return false;
            if (!IsMyTurn() || !IsMainPhase1()) return false;
            if (Card.Location != CardLocation.Hand) return false;
            if (BotHasFacedownInSpellZone(CardId.EvenlyMatched)) return false;
            RefreshEvenlyPlan();
            if (!_planEvenlyThisTurn) return false;
            if (_planEvenlyFromHand) return false; // keep in hand for from-hand activation

            return true;
        }

        private bool GoToBattlePhaseForEvenly()
        {
            RefreshEvenlyPlan();
            if (!_planEvenlyThisTurn) return false;
            if (!IsMyTurn() || !IsMainPhase1()) return false;
            if (_didEvenlyActivateThisTurn) return false;

            // From-hand plan requires a fully empty field.
            if (_planEvenlyFromHand)
                return BotControlsNoCardsAtAll() && Bot.HasInHand(CardId.EvenlyMatched);

            // Set plan: ensure it's set before going Battle.
            if (BotHasFacedownInSpellZone(CardId.EvenlyMatched))
                return true;

            return false;
        }

        private bool SuperPolymerizationActivate()
        {
            if (Card == null || Card.Id != CardId.SuperPolymerization) return false;
            if (DefaultCheckWhetherCardIsNegated(Card)) return false;
            if (_pendingSuperPoly) return false;

            // Treat Super Poly as premium removal: only fire as defense (chain window) or as a real breaker.
            bool isMyTurn = IsMyTurn();
            if (isMyTurn && HasCommittedStarterThisTurn()) return false;

            int bestFusionId = 0;
            bool allowOwn = false;
            bool emergencyDefense = false;
            ClientCard mustInclude = null;

            if (isMyTurn)
            {
                // Our turn: breaker use only, in MP1.
                if (!IsMainPhase1()) return false;
                if (!IsSuperPolyBreakerWindow()) return false;

                if (!CanSuperPolyFusingEnemyOnly(out bestFusionId))
                    return false;
            }
            else
            {
                // Opp turn: defensive use only in a chain window (remove the activating monster when possible).
                if (!IsSuperPolyDefenseWindow(out mustInclude))
                    return false;

                emergencyDefense = true;

                if (!CanSuperPolyFusingEnemyOnly(out bestFusionId, mustInclude))
                {
                    // Emergency: allow using one of our expendable DARKs to meet a DARK fusion requirement.
                    if (!CanSuperPolyFusingWithOwnBackup(out bestFusionId, mustInclude))
                        return false;

                    allowOwn = true;
                }
            }

            _pendingSuperPoly = true;
            _superPolyChosenFusionId = 0;
            _superPolyBestFusionId = bestFusionId;
            _superPolyAllowOwnMaterials = allowOwn;
            _superPolyEmergencyDefense = emergencyDefense;
            _superPolyMustIncludeKey = (mustInclude != null) ? GetCardKey(mustInclude) : string.Empty;

            _selectCtx = SelectCtx.SuperPolyFusionTarget;

            ODLog("[OD.SuperPoly]", "activate (mode=" + (isMyTurn ? "breaker" : "defense") +
                  ", best=" + bestFusionId + ", allowOwn=" + (allowOwn ? 1 : 0) + ")");
            return true;
        }

        // ------------------------------------------------------------
        // Starv4 / Starving Venom support (post-Super Poly + general lines)
        // ------------------------------------------------------------
        private bool Starv4DebuffOnSummon()
        {
            if (Card == null || Card.Id != CardId.StarvingVenomFusionDragonOfTheFourHeavenlyDragons) return false;
            if (DefaultCheckWhetherCardIsNegated(Card)) return false;
            if (Card.Location != CardLocation.MonsterZone || !Card.IsFaceup()) return false;

            // Effect (1): on summon debuff/negate a face-up monster.
            if (ActivateDescription != Util.GetStringId(CardId.StarvingVenomFusionDragonOfTheFourHeavenlyDragons, 0))
                return false;

            if (!Enemy.MonsterZone.Any(m => m != null && m.IsFaceup()))
                return false;

            _selectCtx = SelectCtx.Starv4DebuffTarget;
            ODLog("[OD.Starv4.Debuff]", "activate -> select target phase=" + Duel.Phase);
            return true;
        }

        private bool Starv4QuickFuseIntoStarvingVenom()
        {
            if (Card == null || Card.Id != CardId.StarvingVenomFusionDragonOfTheFourHeavenlyDragons) return false;
            if (DefaultCheckWhetherCardIsNegated(Card)) return false;
            if (Card.Location != CardLocation.MonsterZone || !Card.IsFaceup()) return false;

            // Effect (2): quick fusion when opponent activates.
            if (ActivateDescription != Util.GetStringId(CardId.StarvingVenomFusionDragonOfTheFourHeavenlyDragons, 1))
                return false;

            if (_pendingStarv4QuickFusion) return false;

            // Trigger requirement: opponent activated something.
            if (Duel.CurrentChain == null || Duel.CurrentChain.Count <= 0) return false;
            if (Duel.LastChainPlayer != 1) return false;

            // Spec requirement: only do this if there is a DARK enemy monster to fuse with.
            List<ClientCard> enemyDark = Enemy.MonsterZone
                .Where(m => m != null && m.IsFaceup() && IsDark(m) && !m.HasType(CardType.Token))
                .ToList();
            if (enemyDark.Count == 0) return false;

            // We only run the small, safe line: Starv4 + DARK enemy -> Starving Venom Fusion Dragon.
            if (CheckRemainInDeck(CardId.StarvingVenomFusionDragon) <= 0) return false;
            if (!Bot.ExtraDeck.Any(c => c != null && c.Id == CardId.StarvingVenomFusionDragon)) return false;

            _pendingStarv4QuickFusion = true;
            _starv4ChosenFusionId = CardId.StarvingVenomFusionDragon;
            _selectCtx = SelectCtx.Starv4QuickFusionTarget;

            ODLog("[OD.Starv4.QuickFuse]", "activate -> SVFD enemyDark=" + enemyDark.Count);
            return true;
        }

        private bool StarvingVenomGainAtkOnSummon()
        {
            if (Card == null || Card.Id != CardId.StarvingVenomFusionDragon) return false;
            if (DefaultCheckWhetherCardIsNegated(Card)) return false;
            if (Card.Location != CardLocation.MonsterZone || !Card.IsFaceup()) return false;

            // Effect (1): on Fusion Summon, target an opponent's Special Summoned monster to gain ATK.
            if (ActivateDescription != Util.GetStringId(CardId.StarvingVenomFusionDragon, 0))
                return false;

            if (!Enemy.MonsterZone.Any(m => m != null && m.IsFaceup()))
                return false;

            _selectCtx = SelectCtx.StarvingVenomGainAtkTarget;
            ODLog("[OD.SVFD.GainAtk]", "activate -> select target phase=" + Duel.Phase);
            return true;
        }

        private IList<ClientCard> SelectStarv4DebuffTarget(IList<ClientCard> cards, int min, int max)
        {
            if (cards == null || cards.Count == 0) return null;
            List<ClientCard> pool = cards.Where(c => c != null).ToList();
            if (pool.Count == 0) return null;

            List<ClientCard> enemy = pool.Where(c => IsEnemyFieldCard(c)).ToList();
            ClientCard chosen = Util.GetProblematicEnemyMonster(0, true);
            if (chosen == null || !enemy.Contains(chosen))
                chosen = enemy.OrderByDescending(c => Math.Max(0, c.Attack)).FirstOrDefault();
            if (chosen == null)
                chosen = pool.OrderByDescending(c => Math.Max(0, c.Attack)).FirstOrDefault();

            if (chosen == null) return null;
            _selectCtx = SelectCtx.None;
            ODLog("[OD.Starv4.Debuff]", "select target id=" + chosen.Id + " atk=" + chosen.Attack);
            return Util.CheckSelectCount(new List<ClientCard> { chosen }, cards, min, max);
        }

        private IList<ClientCard> SelectStarv4QuickFusionTarget(IList<ClientCard> cards, int min, int max)
        {
            if (cards == null || cards.Count == 0) return null;
            List<ClientCard> pool = cards.Where(c => c != null).ToList();
            if (pool.Count == 0) return null;

            ClientCard chosen = pool.FirstOrDefault(c => c.Id == CardId.StarvingVenomFusionDragon);
            if (chosen == null) return null;

            _starv4ChosenFusionId = chosen.Id;
            _selectCtx = SelectCtx.Starv4QuickFusionMaterials;
            ODLog("[OD.Starv4.QuickFuse]", "select fusion id=" + chosen.Id);
            return Util.CheckSelectCount(new List<ClientCard> { chosen }, cards, min, max);
        }

        private IList<ClientCard> SelectStarv4QuickFusionMaterials(IList<ClientCard> cards, int min, int max)
        {
            if (cards == null || cards.Count == 0) return null;
            List<ClientCard> pool = cards.Where(c => c != null).ToList();
            if (pool.Count == 0) return null;

            ClientCard self = pool.FirstOrDefault(c => c.Id == CardId.StarvingVenomFusionDragonOfTheFourHeavenlyDragons && !IsEnemyFieldCard(c));
            if (self == null) return null;

            ClientCard other = pool
                .Where(c => c != self && IsEnemyFieldCard(c) && IsDark(c) && !c.HasType(CardType.Token))
                .OrderByDescending(c => Math.Max(0, c.Attack))
                .FirstOrDefault();

            if (other == null)
            {
                // Emergency fallback: fuse with our own DARK (avoid key pieces).
                other = pool
                    .Where(c => c != self && !IsEnemyFieldCard(c) && IsDark(c) && !c.HasType(CardType.Token))
                    .OrderBy(ScoreSPLittleKnightSecondMaterial)
                    .FirstOrDefault();
            }

            if (other == null) return null;

            List<ClientCard> selected = new List<ClientCard> { self, other };
            _selectCtx = SelectCtx.None;
            ODLog("[OD.Starv4.QuickFuse]", "mats=[" + self.Id + "," + other.Id + "]");
            return Util.CheckSelectCount(selected, cards, min, max);
        }

        private IList<ClientCard> SelectStarvingVenomGainAtkTarget(IList<ClientCard> cards, int min, int max)
        {
            if (cards == null || cards.Count == 0) return null;
            List<ClientCard> pool = cards.Where(c => c != null).ToList();
            if (pool.Count == 0) return null;

            ClientCard chosen = pool.Where(c => IsEnemyFieldCard(c))
                .OrderByDescending(c => Math.Max(0, c.Attack))
                .FirstOrDefault() ?? pool.OrderByDescending(c => Math.Max(0, c.Attack)).FirstOrDefault();

            if (chosen == null) return null;
            _selectCtx = SelectCtx.None;
            ODLog("[OD.SVFD.GainAtk]", "select target id=" + chosen.Id + " atk=" + chosen.Attack);
            return Util.CheckSelectCount(new List<ClientCard> { chosen }, cards, min, max);
        }

        private bool IsSuperPolyBreakerWindow()
        {
            // Don't spend Super Poly when we're already ahead and can convert by battle.
            if (IsWeComfortablyAheadByBattle())
                return false;

            // Require 2+ meaningful enemy monsters OR a hard "problem" board.
            int meaningful = CountMeaningfulEnemyMonsters();
            if (meaningful >= 2) return true;

            bool enemyHasProblem =
                Util.GetProblematicEnemyMonster(0, true) != null ||
                Util.GetProblematicEnemySpell() != null ||
                Enemy.MonsterZone.Any(c => c != null && c.IsFaceup() && c.IsFloodgate());

            if (enemyHasProblem) return true;

            return false;
        }

        private bool IsWeComfortablyAheadByBattle()
        {
            // If Serket is live, we can clear a big monster by battle even when ATK looks smaller.
            bool serketLive = Bot.MonsterZone.Any(m =>
                m != null && m.IsFaceup() && !m.IsDisabled() && m.IsCode(CardId.MercilessScorpionOfSerket));

            int botBest = 0;
            foreach (var m in Bot.MonsterZone)
            {
                if (m == null || !m.IsFaceup()) continue;
                botBest = Math.Max(botBest, m.GetDefensePower());
            }
            if (serketLive)
                botBest = Math.Max(botBest, 9999);

            int enemyBest = 0;
            int enemyCount = 0;
            foreach (var m in Enemy.MonsterZone)
            {
                if (m == null || !m.IsFaceup()) continue;
                enemyCount++;
                enemyBest = Math.Max(enemyBest, m.GetDefensePower());
            }

            if (enemyCount == 0) return true;

            int botCount = Bot.MonsterZone.Count(m => m != null);

            // If our best body matches/exceeds their best, and we aren't facing a "problem" floodgate, hold Super Poly.
            if (botBest >= enemyBest && botCount >= enemyCount)
            {
                if (Util.GetProblematicEnemyMonster(0, true) == null && Util.GetProblematicEnemySpell() == null)
                    return true;
            }

            return false;
        }

        private bool IsSuperPolyDefenseWindow(out ClientCard mustInclude)
        {
            mustInclude = null;

            if (!IsOppTurn()) return false;
            if (Duel.CurrentChain == null || Duel.CurrentChain.Count == 0) return false;
            if (Duel.LastChainPlayer != 1) return false;

            // Only defend when an enemy monster effect is being activated (Super Poly can remove it).
            ClientCard last = Util.GetLastChainCard();
            if (last == null) return false;
            if (!IsEnemyFieldCard(last) || !last.IsMonster() || !last.IsFaceup()) return false;

            // Only if we actually have something important to protect / preserve.
            bool haveKey =
                Bot.MonsterZone.Any(m => m != null && m.IsFaceup() &&
                    (m.Id == CardId.DivineSerpentApophis || m.Id == CardId.SilhouhatteRabbit || m.Id == CardId.StatueOfAnguishPattern)) ||
                HasTempleEffectOnline();

            if (!haveKey) return false;

            mustInclude = last;
            return true;
        }

        private int CountMeaningfulEnemyMonsters()
        {
            int count = 0;
            foreach (var m in Enemy.MonsterZone)
            {
                if (m == null || !m.IsFaceup()) continue;
                if (m.HasType(CardType.Token)) continue;

                if (m.IsFloodgate() || m.IsMonsterShouldBeDisabledBeforeItUseEffect())
                {
                    count++;
                    continue;
                }

                // Big bodies / extra deck bosses are usually meaningful in breaker contexts.
                if (m.GetDefensePower() >= 2600)
                    count++;
            }
            return count;
        }

        private bool CanSuperPolyFusingWithOwnBackup(out int bestFusionId, ClientCard mustInclude)
        {
            bestFusionId = 0;

            // Only attempt backup when the must-include enemy monster is DARK (otherwise DARK fusions won't remove it).
            if (mustInclude == null || !IsDark(mustInclude)) return false;

            // Count enemy + our expendable DARKs on field.
            var enemy = Enemy.MonsterZone.Where(c => c != null && c.IsFaceup()).ToList();

            int enemyDark = enemy.Count(IsDark);
            int enemyDarkNonToken = enemy.Count(c => IsDark(c) && !c.HasType(CardType.Token));

            int ownDark = Bot.MonsterZone
                .Where(c => c != null && c.IsFaceup() && IsDark(c) && IsSuperPolyOwnMaterialAllowed(c, false))
                .Count();

            int ownDarkEmergency = Bot.MonsterZone
                .Where(c => c != null && c.IsFaceup() && IsDark(c) && IsSuperPolyOwnMaterialAllowed(c, true))
                .Count();

            // Priority: Triphy > Starv4 > Starving Venom (regular)
            if (HasFusionInExtra(CardId.PredaplantTriphyoverutum) && enemyDark >= 1 && (enemyDark + ownDark) >= 3)
            {
                bestFusionId = CardId.PredaplantTriphyoverutum;
                return true;
            }
            if (HasFusionInExtra(CardId.PredaplantTriphyoverutum) && enemyDark >= 1 && (enemyDark + ownDarkEmergency) >= 3)
            {
                bestFusionId = CardId.PredaplantTriphyoverutum;
                return true;
            }

            if (HasFusionInExtra(CardId.StarvingVenomFusionDragonOfTheFourHeavenlyDragons) && enemyDark >= 1 && (enemyDark + ownDark) >= 2)
            {
                bestFusionId = CardId.StarvingVenomFusionDragonOfTheFourHeavenlyDragons;
                return true;
            }
            if (HasFusionInExtra(CardId.StarvingVenomFusionDragonOfTheFourHeavenlyDragons) && enemyDark >= 1 && (enemyDark + ownDarkEmergency) >= 2)
            {
                bestFusionId = CardId.StarvingVenomFusionDragonOfTheFourHeavenlyDragons;
                return true;
            }

            if (HasFusionInExtra(CardId.StarvingVenomFusionDragon) && enemyDarkNonToken >= 1 && (enemyDarkNonToken + ownDark) >= 2)
            {
                bestFusionId = CardId.StarvingVenomFusionDragon;
                return true;
            }
            if (HasFusionInExtra(CardId.StarvingVenomFusionDragon) && enemyDarkNonToken >= 1 && (enemyDarkNonToken + ownDarkEmergency) >= 2)
            {
                bestFusionId = CardId.StarvingVenomFusionDragon;
                return true;
            }

            return false;
        }

        private bool IsSuperPolyOwnMaterialAllowed(ClientCard c, bool emergency)
        {
            if (c == null) return false;
            if (c.Controller != 0) return false;
            if (!c.IsFaceup() || !c.IsMonster()) return false;

            // Never throw away our true key bodies unless it's an emergency and there is no alternative.
            if (c.Id == CardId.DivineSerpentApophis || c.Id == CardId.SilhouhatteRabbit || c.Id == CardId.StatueOfAnguishPattern)
                return emergency;

            // Avoid using Divine Scorpion Beast (engine payoff).
            if (c.Id == CardId.DivineScorpionBeastOfSerket)
                return emergency;

            return true;
        }

        private bool CanSuperPolyFusingEnemyOnly(out int bestFusionId)
        {
            return CanSuperPolyFusingEnemyOnly(out bestFusionId, null);
        }

        private bool CanSuperPolyFusingEnemyOnly(out int bestFusionId, ClientCard mustInclude)
        {
            bestFusionId = 0;

            // Enemy-only fuse policy (default). Offered fusion list will enforce legality.
            var enemy = Enemy.MonsterZone
                .Where(c => c != null && c.IsFaceup())
                .ToList();

            if (enemy.Count < 2)
                return false;

            // Must-include (defense): only accept fusions that can consume the chain monster.
            bool requireMust = mustInclude != null;

            int enemyDark = enemy.Count(IsDark);
            int enemyDarkNonToken = enemy.Count(c => IsDark(c) && !c.HasType(CardType.Token));

            bool mustIsDark = !requireMust || IsDark(mustInclude);
            bool mustIsDarkNonToken = !requireMust || (IsDark(mustInclude) && !mustInclude.HasType(CardType.Token));

            bool canTriphy = HasFusionInExtra(CardId.PredaplantTriphyoverutum) && enemyDark >= 3 && mustIsDark;
            bool canStarv4 = HasFusionInExtra(CardId.StarvingVenomFusionDragonOfTheFourHeavenlyDragons) && enemyDark >= 2 && mustIsDark;
            bool canStarv = HasFusionInExtra(CardId.StarvingVenomFusionDragon) && enemyDarkNonToken >= 2 && mustIsDarkNonToken;

            ClientCard a, b;
            bool canGarura = HasFusionInExtra(CardId.GaruraWingsOfResonantLife) &&
                (requireMust ? TryFindGaruraPairEnemyOnlyIncluding(enemy, mustInclude, out a, out b)
                            : TryFindGaruraPairEnemyOnly(enemy, out a, out b));

            bool canMud = HasFusionInExtra(CardId.MudragonOfTheSwamp) &&
                (requireMust ? TryFindMudragonPairEnemyOnlyIncluding(enemy, mustInclude, out a, out b)
                            : TryFindMudragonPairEnemyOnly(enemy, out a, out b));

            if (!canTriphy && !canStarv4 && !canStarv && !canGarura && !canMud)
                return false;

            // Priority: Triphy (3 DARK) > Starv4 (2 DARK) > Starving Venom (2 DARK, no tokens) > Garura > Mudragon
            if (canTriphy) bestFusionId = CardId.PredaplantTriphyoverutum;
            else if (canStarv4) bestFusionId = CardId.StarvingVenomFusionDragonOfTheFourHeavenlyDragons;
            else if (canStarv) bestFusionId = CardId.StarvingVenomFusionDragon;
            else if (canGarura) bestFusionId = CardId.GaruraWingsOfResonantLife;
            else bestFusionId = CardId.MudragonOfTheSwamp;

            ODLog("[OD.SuperPoly.Check]", "enemyOnly=1 best=" + bestFusionId +
                  " (enemy=" + enemy.Count + ", dark=" + enemyDark + ", darkNoToken=" + enemyDarkNonToken +
                  ", must=" + (mustInclude == null ? "0" : mustInclude.Id.ToString()) + ")");
            return true;
        }

        private bool HasFusionInExtra(int id)
        {
            return Bot.ExtraDeck.Any(c => c != null && c.Id == id);
        }

        private static bool IsDark(ClientCard c)
        {
            return c != null && (c.Attribute & (int)CardAttribute.Dark) != 0;
        }

        private bool TryFindGaruraPairEnemyOnly(List<ClientCard> enemy, out ClientCard a, out ClientCard b)
        {
            a = null;
            b = null;
            if (enemy == null || enemy.Count < 2) return false;

            int bestSum = -1;
            for (int i = 0; i < enemy.Count; i++)
            {
                for (int j = i + 1; j < enemy.Count; j++)
                {
                    ClientCard x = enemy[i];
                    ClientCard y = enemy[j];
                    if (x == null || y == null) continue;
                    if (x.Id == y.Id) continue; // different names
                    if (x.Attribute != y.Attribute) continue;
                    if (x.Race != y.Race) continue;

                    int sum = ScoreEnemyThreat(x) + ScoreEnemyThreat(y);
                    if (sum > bestSum)
                    {
                        bestSum = sum;
                        a = x;
                        b = y;
                    }
                }
            }

            return a != null && b != null;
        }

        private bool TryFindMudragonPairEnemyOnly(List<ClientCard> enemy, out ClientCard a, out ClientCard b)
        {
            a = null;
            b = null;
            if (enemy == null || enemy.Count < 2) return false;

            int bestSum = -1;
            for (int i = 0; i < enemy.Count; i++)
            {
                for (int j = i + 1; j < enemy.Count; j++)
                {
                    ClientCard x = enemy[i];
                    ClientCard y = enemy[j];
                    if (x == null || y == null) continue;
                    if (x.Attribute != y.Attribute) continue;
                    if (x.Race == y.Race) continue; // different Types

                    int sum = ScoreEnemyThreat(x) + ScoreEnemyThreat(y);
                    if (sum > bestSum)
                    {
                        bestSum = sum;
                        a = x;
                        b = y;
                    }
                }
            }

            return a != null && b != null;
        }

        private bool TryFindGaruraPairEnemyOnlyIncluding(List<ClientCard> enemy, ClientCard mustInclude, out ClientCard a, out ClientCard b)
        {
            a = null;
            b = null;
            if (enemy == null || enemy.Count < 2 || mustInclude == null) return false;

            // Pair must include the specified card.
            foreach (var other in enemy)
            {
                if (other == null || other == mustInclude) continue;
                if (other.Id == mustInclude.Id) continue; // different names
                if (other.Attribute != mustInclude.Attribute) continue;
                if (other.Race != mustInclude.Race) continue;

                a = mustInclude;
                b = other;
                return true;
            }
            return false;
        }

        private bool TryFindMudragonPairEnemyOnlyIncluding(List<ClientCard> enemy, ClientCard mustInclude, out ClientCard a, out ClientCard b)
        {
            a = null;
            b = null;
            if (enemy == null || enemy.Count < 2 || mustInclude == null) return false;

            foreach (var other in enemy)
            {
                if (other == null || other == mustInclude) continue;
                if (other.Attribute != mustInclude.Attribute) continue;
                if (other.Race == mustInclude.Race) continue; // different Types

                a = mustInclude;
                b = other;
                return true;
            }
            return false;
        }


        private IList<ClientCard> SelectSuperPolyFusionTarget(IList<ClientCard> cards, int min, int max)
        {
            if (cards == null || cards.Count == 0) return null;

            List<ClientCard> fusions = cards
                .Where(c => c != null && c.Location == CardLocation.Extra)
                .ToList();

            if (fusions.Count == 0) return null;

            ClientCard pick = null;

            // Fixed priority among the OFFERED legal fusions (playbook):
            // Triphy > Starv4 > Starving > Garura > Mudragon.
            int[] preferIds = new[]
            {
                CardId.PredaplantTriphyoverutum,
                CardId.StarvingVenomFusionDragonOfTheFourHeavenlyDragons,
                CardId.StarvingVenomFusionDragon,
                CardId.GaruraWingsOfResonantLife,
                CardId.MudragonOfTheSwamp,
            };

            // Find best offered by rank.
            ClientCard bestOffered = null;
            foreach (int id in preferIds)
            {
                bestOffered = fusions.FirstOrDefault(c => c.Id == id);
                if (bestOffered != null) break;
            }

            // If we pre-computed a "best fusion" elsewhere, honor it ONLY if it isn't lower priority
            // than the best offered option.
            ClientCard bestPre = null;
            if (_superPolyBestFusionId != 0)
                bestPre = fusions.FirstOrDefault(c => c.Id == _superPolyBestFusionId);

            if (bestPre != null)
            {
                int rankPre = 999;
                int rankOffered = 999;

                for (int i = 0; i < preferIds.Length; i++)
                {
                    if (preferIds[i] == bestPre.Id) rankPre = i;
                    if (bestOffered != null && preferIds[i] == bestOffered.Id) rankOffered = i;
                }

                pick = (bestOffered == null || rankPre <= rankOffered) ? bestPre : bestOffered;
            }
            else
            {
                pick = bestOffered;
            }

            if (pick == null)
                pick = fusions.OrderByDescending(c => Math.Max(0, c.Attack)).FirstOrDefault();

            if (pick == null) return null;

            _superPolyChosenFusionId = pick.Id;
            _selectCtx = SelectCtx.SuperPolyFusionMaterials;

            ODLog("[OD.SuperPoly.SelectFusion]", "pick=" + pick.Id + " (best=" + _superPolyBestFusionId + ")");
            return Util.CheckSelectCount(new List<ClientCard> { pick }, cards, min, max);
        }

        private IList<ClientCard> SelectSuperPolyFusionMaterials(IList<ClientCard> cards, int min, int max)
        {
            if (cards == null || cards.Count == 0) return null;

            List<ClientCard> pool = cards
                .Where(c => c != null && c.Location == CardLocation.MonsterZone && c.IsFaceup())
                .ToList();

            if (pool.Count == 0) return null;

            List<ClientCard> enemy = pool.Where(IsEnemyFieldCard).ToList();
            List<ClientCard> own = pool.Where(c => !IsEnemyFieldCard(c)).ToList();

            List<ClientCard> chosen = new List<ClientCard>();

            // DARK-based fusions (Triphy / Starv4 / Starving Venom)
            if (_superPolyChosenFusionId == CardId.PredaplantTriphyoverutum
                || _superPolyChosenFusionId == CardId.StarvingVenomFusionDragonOfTheFourHeavenlyDragons
                || _superPolyChosenFusionId == CardId.StarvingVenomFusionDragon)
            {
                int need = (_superPolyChosenFusionId == CardId.PredaplantTriphyoverutum) ? 3 : 2;

                bool noToken = (_superPolyChosenFusionId == CardId.StarvingVenomFusionDragon);
                Func<ClientCard, bool> isDarkMat = c => IsDark(c) && (!noToken || !c.HasType(CardType.Token));

                // 1) Must-include enemy monster (defense mode): try to include it first if legal.
                if (!string.IsNullOrEmpty(_superPolyMustIncludeKey))
                {
                    ClientCard must = enemy.FirstOrDefault(c => c != null && GetCardKey(c) == _superPolyMustIncludeKey && isDarkMat(c));
                    if (must != null)
                        chosen.Add(must);
                }

                // 2) Fill with enemy monsters (highest threat first).
                foreach (var e in enemy
                    .Where(c => c != null && isDarkMat(c) && !chosen.Contains(c))
                    .OrderByDescending(ScoreEnemyThreat)
                    .ThenByDescending(c => Math.Max(0, c.Attack)))
                {
                    if (chosen.Count >= need) break;
                    chosen.Add(e);
                }

                // 3) Emergency: allow one of our expendable DARKs only if needed.
                if (chosen.Count < need && _superPolyAllowOwnMaterials)
                {
                    foreach (var b in own
                        .Where(c => c != null && isDarkMat(c) && IsSuperPolyOwnMaterialAllowed(c, false))
                        .OrderBy(c => Math.Max(0, c.Attack)))
                    {
                        if (chosen.Count >= need) break;
                        chosen.Add(b);
                    }
                }

                // 4) Last resort: allow key bodies only in true emergency.
                if (chosen.Count < need && _superPolyAllowOwnMaterials && _superPolyEmergencyDefense)
                {
                    foreach (var b in own
                        .Where(c => c != null && isDarkMat(c) && IsSuperPolyOwnMaterialAllowed(c, true) && !chosen.Contains(c))
                        .OrderBy(c => Math.Max(0, c.Attack)))
                    {
                        if (chosen.Count >= need) break;
                        chosen.Add(b);
                    }
                }

                if (chosen.Count < need)
                    chosen.Clear();
            }
            else if (_superPolyChosenFusionId == CardId.GaruraWingsOfResonantLife)
            {
                ClientCard a, b;
                if (!string.IsNullOrEmpty(_superPolyMustIncludeKey))
                {
                    ClientCard must = enemy.FirstOrDefault(c => c != null && GetCardKey(c) == _superPolyMustIncludeKey);
                    if (must != null && TryFindGaruraPairEnemyOnlyIncluding(enemy, must, out a, out b))
                        chosen = new List<ClientCard> { a, b };
                    else if (TryFindGaruraPairEnemyOnly(enemy, out a, out b))
                        chosen = new List<ClientCard> { a, b };
                }
                else if (TryFindGaruraPairEnemyOnly(enemy, out a, out b))
                {
                    chosen = new List<ClientCard> { a, b };
                }
            }
            else if (_superPolyChosenFusionId == CardId.MudragonOfTheSwamp)
            {
                ClientCard a, b;
                if (!string.IsNullOrEmpty(_superPolyMustIncludeKey))
                {
                    ClientCard must = enemy.FirstOrDefault(c => c != null && GetCardKey(c) == _superPolyMustIncludeKey);
                    if (must != null && TryFindMudragonPairEnemyOnlyIncluding(enemy, must, out a, out b))
                        chosen = new List<ClientCard> { a, b };
                    else if (TryFindMudragonPairEnemyOnly(enemy, out a, out b))
                        chosen = new List<ClientCard> { a, b };
                }
                else if (TryFindMudragonPairEnemyOnly(enemy, out a, out b))
                {
                    chosen = new List<ClientCard> { a, b };
                }
            }

            if (chosen.Count == 0)
                return null;

            string poolStr = string.Join(",", chosen.Select(c => c.Id));
            ODLog("[OD.SuperPoly.SelectMats]",
                "fusion=" + _superPolyChosenFusionId +
                " pick=[" + poolStr + "] (min=" + min + ", max=" + max +
                ", allowOwn=" + (_superPolyAllowOwnMaterials ? 1 : 0) +
                ", emergency=" + (_superPolyEmergencyDefense ? 1 : 0) + ")");

            // After choosing materials, we're done with the selection routing for this activation.
            _pendingSuperPoly = false;
            _selectCtx = SelectCtx.None;

            _superPolyBestFusionId = 0;
            _superPolyAllowOwnMaterials = false;
            _superPolyEmergencyDefense = false;
            _superPolyMustIncludeKey = string.Empty;

            return Util.CheckSelectCount(chosen, cards, min, max);
        }

        private bool EvenlyMatchedActivate()
        {
            if (Card == null) return false;
            if (!IsBattleEndStep()) return false;
            if (_didEvenlyActivateThisTurn) return false;

            int enemyField = CountEnemyCardsOnField();
            int botField = CountBotCardsOnField();
            if (enemyField <= botField) return false;

            // From-hand activation requires controlling no cards.
            if (Card.Location == CardLocation.Hand && botField != 0)
                return false;

            _didEvenlyActivateThisTurn = true;
            ODLog("[OD.Evenly]", "activate (enemy=" + enemyField + ", bot=" + botField + ", fromHand=" + (Card.Location == CardLocation.Hand) + ")");
            return true;
        }

        private bool HasAnyEngineStarterInHand()
        {
            return Bot.HasInHand(CardId.AnubisTheLastJudge)
                || Bot.HasInHand(CardId.TheManWithTheMark)
                || Bot.HasInHand(CardId.TreasuresOfTheKings)
                || Bot.HasInHand(CardId.MercilessScorpionOfSerket);
        }

        // ------------------------------------------------------------
        // Generic Nibiru (kept simple for Batch 1)
        // ------------------------------------------------------------
        private bool NibiruThePrimalBeingActivate()
        {
            ODLog("[OD.Nibiru]", "activate");
            return true;
        }

        // ------------------------------------------------------------
        // Final fallback set policy (Handbook-style)
        // ------------------------------------------------------------
        private bool SpellSetCheck()
        {
            if (Card == null) return false;

            bool isSettable = Card.HasType(CardType.Trap) || Card.HasType(CardType.QuickPlay);
            if (!isSettable) return false;

            RefreshEvenlyPlan();

            // Evenly plan: keep the field empty (from-hand) or set only Evenly (set-plan).
            if (_planEvenlyThisTurn)
            {
                if (_planEvenlyFromHand)
                    return false;

                if (Card.Id != CardId.EvenlyMatched)
                    return false;

                // Only set 1 copy.
                if (BotHasFacedownInSpellZone(CardId.EvenlyMatched))
                    return false;
            }
            // Preserve backrow space for Divine Serpent Apophis set-back (can set up to 3 Apophis traps from GY).
            // SpellSet is evaluated per candidate card; if setting this card would reduce Divine's capacity, skip it.
            int divineNeed = GetDivineSetBackNeedSlots();
            if (divineNeed > 0)
            {
                int free = CountFreeSpellZone();
                // Setting 1 card consumes 1 slot => require (free - 1) >= divineNeed  <=> free > divineNeed.
                if (free <= divineNeed)
                {
                    ODLog("[OD.SpellSet]", "skip " + Card.Id + " (preserve Divine set-back: freeSZ=" + free + ", need=" + divineNeed + ")");
                    return false;
                }
            }


            // Don't set Verdict of Anubis unless its activation condition is already live.
            if (Card.Id == CardId.VerdictOfAnubis)
            {
                int otherST = 0;
                foreach (ClientCard z in Bot.SpellZone)
                    if (z != null) otherST++;
                foreach (ClientCard m in Bot.MonsterZone)
                    if (m != null && m.HasType(CardType.Spell | CardType.Trap)) otherST++;

                if (otherST < 3)
                {
                    ODLog("[OD.SpellSet]", "skip Verdict (need 3 other S/T on field; now=" + otherST + ")");
                    return false;
                }
            }

            // Keep at least 1 free S/T slot when needed for combos
            if (NeedReserveSpellZoneForRabbitSet() || NeedReserveZoneForDefenseFusion())
            {
                int free = CountFreeSpellZone();
                if (free <= 1)
                {
                    // Allow only key engine traps when tight on space
                    bool allow = (Card.Id == CardId.ApophisTheSerpent)
                        || (Card.Id == CardId.ApophisTheSwampDeity)
                        || (Card.Id == CardId.DefenseOfTheTemple); // NEW: Allow Defense

                    if (!allow)
                    {
                        if (!_loggedReserveZoneForRabbit)
                        {
                            _loggedReserveZoneForRabbit = true;
                            ODLog("[OD.SpellSet]", "reserve S/T slot for combo (freeSZ=" + free + ")");
                        }
                        return false;
                    }
                }
            }

            return true;
        }

        // ------------------------------------------------------------
        // Logging helper
        // ------------------------------------------------------------
        private void ODLog(string tag, string msg)
        {
            Logger.DebugWriteLine(tag + " " + msg + " (phase=" + Duel.Phase + ", pl=" + Duel.Player + ")");
        }
    }
}
