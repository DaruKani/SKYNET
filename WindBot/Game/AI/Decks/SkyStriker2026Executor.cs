using System;
using System.Collections.Generic;
using System.Linq;
using YGOSharp.OCGWrapper.Enums;
using WindBot;
using WindBot.Game;
using WindBot.Game.AI;

namespace WindBot.Game.AI.Decks
{
    [Deck("SkyStriker2026", "AI_SkyStriker2026")]
    public partial class SkyStriker2026Executor : DefaultExecutor
    {
        private const int UltimateSkyChainNegateThreshold = 3600;

        private enum HornetZeroSetupStage
        {
            None,
            AwaitFirstToken,
            NeedKagari,
            KagariReady,
            AwaitSecondToken,
            NeedBridgeLink,
            NeedZero
        }

        private enum LinkageOtkStage
        {
            None,
            SummonKagari,
            KagariReady,
            SummonZero,
            ZeroReady,
            RayeRozeReady
        }

        public class CardId
        {
            public const int Raye = 26077387;
            public const int Roze = 37351133;
            public const int Kagari = 63288573;
            public const int Shizuku = 90673288;
            public const int Hayate = 8491308;
            public const int Kaina = 12421694;
            public const int Zero = 76072561;
            public const int Camellia = 63013339;
            public const int Amatsu = 25072579;
            public const int Azalea = 98462037;
            public const int Zeke = 75147529;
            public const int Token = 52340445;

            public const int Engage = 63166095;
            public const int Linkage = 9726840;
            public const int Lemnisgate = 34433770;
            public const int HornetDrones = 52340444;
            public const int WidowAnchor = 98338152;
            public const int SharkCannon = 51227866;
            public const int Afterburners = 99550630;
            public const int JammingWaves = 25955749;
            public const int Multirole = 24010609;
            public const int HerculesBase = 97616504;
            public const int AreaZero = 50005218;

            public const int AshBlossom = 14558127;
            public const int DrollLockBird = 94145021;
            public const int GhostBelle = 73642296;
            public const int MulcharmyFuwalos = 42141493;
            public const int GhostOgre = 59438930;
            public const int MaxxC = 23434538;
            public const int GlowUpBulb = 67441435;
            public const int EffectVeiler = 97268402;

            public const int ReinforcementOfTheArmy = 32807846;
            public const int PotOfDesires = 35261759;
            public const int FoolishBurialGoods = 35726888;
            public const int UpstartGoblin = 70368879;
            public const int TripleTacticsTalent = 25311006;
            public const int UltimateSky = 38817295;
            public const int MetalfoesFusion = 73594093;
            public const int TwinTwisters = 43898403;
            public const int MysticalSpaceTyphoon = 5318639;
            public const int RadiantTyphoonVision = 20508881;
            public const int TheFallenAndTheVirtuous = 30271097;
            public const int AlbionTheBrandedDragon = 87746184;
            public const int DragonThatDevoursTheDogma = 76666602;
            public const int EcclesiaAndTheDarkDragon = 78397661;
            public const int SuperStarslayerTyphon = 93039339;
            public const int ForbiddenDroplet = 24299458;
            public const int ForbiddenCrown = 98829635;
            public const int TheHallowedAzamina = 94845588;
            public const int AzaminaDebtors = 20934683;
            public const int Raigeki = 12580477;
            public const int LightningStorm = 14532163;
            public const int HarpiesFeatherDuster = 18144506;
            public const int EvenlyMatched = 15693423;
            public const int SphereMode = 10000080;
            public const int SolemnJudgment = 41420027;
            public const int SolemnStrike = 40605147;
            public const int SolemnWarning = 84749824;

            public const int BorrelswordDragon = 85289965;
            public const int TopologicBomberDragon = 5821478;
            public const int KnightmareUnicorn = 38342335;
            public const int KnightmarePhoenix = 2857636;
            public const int KnightmareCerberus = 75452921;
            public const int CrystronHalqifibrax = 50588353;
            public const int Linkuriboh = 41999284;
        }

        private bool KagariSummoned;
        private bool ShizukuSummoned;
        private bool HayateSummoned;
        private bool KainaSummoned;
        private bool ZeroSummoned;
        private bool CamelliaSummoned;
        private bool AmatsuSummoned;
        private bool AzaleaSummoned;
        private bool ZekeSummoned;
        private bool BorrelswordAttackEffectUsed;
        private bool BorrelswordPositionEffectUsed;
        private bool GlowUpBulbEffectUsed; // Do not reset it
        private bool BorrelswordComboStarted;
        private bool TopologicComboStarted;
        private bool NormalSummoned;
        private bool RayeGraveEffectUsed;
        private bool AreaZeroSetForJamming;
        private bool WidowAnchorTakeControl;
        private bool SharkCannonBanishOnly;
        private bool AmatsuRedirectPending;
        private bool SphereModeTributesPending;
        private ClientCard WidowAnchorTarget;
        private ClientCard SharkCannonTarget;
        private ClientCard ForbiddenCrownTarget;
        private ClientCard TripleTacticsTalentTarget;
        private ClientCard AzaleaDestroyTarget;
        private int LemnisgateRecyclePairCount;
        private int LemnisgateRecycleSelectionCount;
        private ClientCard ZeroDestroyTarget;
        private ClientCard FallenAndVirtuousDestroyTarget;
        private ClientCard FallenAndVirtuousReviveTarget;
        private ClientCard LinkageOtkSendTarget;
        private int PlannedLinkageFuelId;
        private HornetZeroSetupStage HornetSetupStage;
        private LinkageOtkStage LinkageOtk;
        private bool RayeToZeroSetupPending;
        private bool PreserveEmptyMainZoneForZero;
        private bool ZeroSearchPending;
        private bool LinkageSetupToZeroPending;
        private bool ZeroExitClearancePending;
        private bool UltimateSkyLinkageSetupPending;
        private bool UltimateSkyChainNegatePending;
        private bool OpponentAddedFromMainDeckThisTurn;
        private bool FallenAndVirtuousPreferDestroy;
        private bool SkyStrikerMirrorObserved;
        private string LastUltimateSkyTraceSignature;
        private readonly List<ClientCard> ForbiddenDropletTargets = new List<ClientCard>();
        private readonly List<ClientCard> UltimateSkyTargets = new List<ClientCard>();
        private readonly List<ClientCard> CurrentChainHandledCards = new List<ClientCard>();
        private readonly List<ClientCard> TurnNegatedCards = new List<ClientCard>();
        private readonly List<ClientCard> MultiroleSetCards = new List<ClientCard>();
        private readonly List<ClientCard> ControlledEnemyCards = new List<ClientCard>();

        public SkyStriker2026Executor(GameAI ai, Duel duel)
            : base(ai, duel)
        {
            AddExecutor(ExecutorType.GoToBattlePhase, EvenlyMatchedToBattle);

            AddExecutor(ExecutorType.Activate, CardId.AshBlossom, AshBlossomEffect);
            AddExecutor(ExecutorType.Activate, CardId.GhostBelle, DefaultGhostBelleAndHauntedMansion);
            AddExecutor(ExecutorType.Activate, CardId.DrollLockBird, DrollLockBirdEffect);
            AddExecutor(ExecutorType.Activate, CardId.MulcharmyFuwalos, DefaultMaxxC);
            AddExecutor(ExecutorType.Activate, CardId.GhostOgre, DefaultGhostOgreAndSnowRabbit);
            AddExecutor(ExecutorType.Activate, CardId.EffectVeiler, DefaultEffectVeiler);
            AddExecutor(ExecutorType.Activate, CardId.MaxxC, DefaultMaxxC);
            AddExecutor(ExecutorType.Activate, CardId.Amatsu, AmatsuEffect);
            AddExecutor(ExecutorType.Activate, CardId.Zero, ZeroEffect);
            AddExecutor(ExecutorType.Activate, CardId.SolemnWarning, DefaultSolemnWarning);
            AddExecutor(ExecutorType.Activate, CardId.SolemnStrike, DefaultSolemnStrike);
            AddExecutor(ExecutorType.Activate, CardId.SolemnJudgment, DefaultSolemnJudgment);

            AddExecutor(ExecutorType.Activate, CardId.Raye, RayeEffect);
            AddExecutor(ExecutorType.Activate, CardId.Roze, RozeEffect);
            AddExecutor(ExecutorType.Activate, CardId.TwinTwisters, TwinTwistersEffect);
            AddExecutor(ExecutorType.Activate, CardId.MysticalSpaceTyphoon, MysticalSpaceTyphoonEffect);
            AddExecutor(ExecutorType.Activate, CardId.HarpiesFeatherDuster, DefaultHarpiesFeatherDusterFirst);
            AddExecutor(ExecutorType.Activate, CardId.Raigeki, DefaultRaigeki);
            AddExecutor(ExecutorType.Activate, CardId.LightningStorm, LightningStormEffect);
            AddExecutor(ExecutorType.Activate, CardId.UltimateSky, UltimateSkyEffect);
            AddExecutor(ExecutorType.Activate, CardId.ForbiddenCrown, ForbiddenCrownEffect);

            AddExecutor(ExecutorType.Activate, CardId.ReinforcementOfTheArmy, ReinforcementOfTheArmyEffect);
            AddExecutor(ExecutorType.Activate, CardId.FoolishBurialGoods, FoolishBurialGoodsEffect);
            AddExecutor(ExecutorType.Activate, CardId.PotOfDesires, PotOfDesiresEffect);
            AddExecutor(ExecutorType.Activate, CardId.TripleTacticsTalent, TripleTacticsTalentEffect);
            AddExecutor(ExecutorType.Activate, CardId.UpstartGoblin, UpstartGoblinEffect);
            AddExecutor(ExecutorType.Activate, CardId.RadiantTyphoonVision, RadiantTyphoonVisionEffect);
            AddExecutor(ExecutorType.Activate, CardId.TheFallenAndTheVirtuous, FallenAndVirtuousEffect);
            AddExecutor(ExecutorType.Activate, CardId.AlbionTheBrandedDragon, AlbionTheBrandedDragonEffect);
            AddExecutor(ExecutorType.Activate, CardId.DragonThatDevoursTheDogma, DragonThatDevoursTheDogmaEffect);
            AddExecutor(ExecutorType.Activate, CardId.EcclesiaAndTheDarkDragon, EcclesiaAndTheDarkDragonEffect);

            AddExecutor(ExecutorType.Activate, CardId.Multirole, MultiroleActivation);
            AddExecutor(ExecutorType.Activate, CardId.WidowAnchor, WidowAnchorEffectFirst);
            AddExecutor(ExecutorType.Activate, CardId.ForbiddenDroplet, ForbiddenDropletEffect);
            AddExecutor(ExecutorType.Activate, CardId.Afterburners, AfterburnersEffect);
            AddExecutor(ExecutorType.Activate, CardId.JammingWaves, JammingWavesEffect);
            AddExecutor(ExecutorType.SpellSet, CardId.AreaZero, AreaZeroSetForJammingWaves);

            AddExecutor(ExecutorType.Activate, CardId.WidowAnchor, WidowAnchorEffect);
            AddExecutor(ExecutorType.Activate, CardId.SharkCannon, SharkCannonEffect);
            AddExecutor(ExecutorType.Activate, CardId.Multirole, MultiroleIgnitionEffect);

            AddExecutor(ExecutorType.Activate, CardId.Engage, EngageEffect);
            AddExecutor(ExecutorType.SpellSet, SetPlannedLinkageFuel);
            AddExecutor(ExecutorType.Activate, CardId.Linkage, LinkageEffect);
            AddExecutor(ExecutorType.Activate, CardId.Lemnisgate, LemnisgateEffect);
            AddExecutor(ExecutorType.Activate, CardId.HornetDrones, HornetDronesEffect);

            AddExecutor(ExecutorType.Activate, CardId.MetalfoesFusion, MetalfoesFusionEffect);

            AddExecutor(ExecutorType.SpSummon, CardId.Linkuriboh, LinkuribohSummon);
            AddExecutor(ExecutorType.SpSummon, CardId.Zero, ZeroSummon);
            AddExecutor(ExecutorType.SpSummon, CardId.Zeke, ZekeSummon);
            AddExecutor(ExecutorType.SpSummon, CardId.Azalea, AzaleaSummon);
            AddExecutor(ExecutorType.SpSummon, CardId.Camellia, CamelliaSummon);
            AddExecutor(ExecutorType.SpSummon, CardId.Kagari, KagariSummon);
            AddExecutor(ExecutorType.Activate, CardId.Kagari, KagariEffect);
            AddExecutor(ExecutorType.SpSummon, CardId.Amatsu, AmatsuSummon);
            AddExecutor(ExecutorType.SpSummon, CardId.SuperStarslayerTyphon, SuperStarslayerTyphonSummon);
            AddExecutor(ExecutorType.Activate, CardId.SuperStarslayerTyphon, SuperStarslayerTyphonEffect);
            AddExecutor(ExecutorType.Activate, CardId.Camellia, CamelliaEffect);
            AddExecutor(ExecutorType.Activate, CardId.Zeke, ZekeEffect);
            AddExecutor(ExecutorType.Activate, CardId.Azalea, AzaleaEffect);

            AddExecutor(ExecutorType.Summon, CardId.GlowUpBulb, TunerSummon);
            AddExecutor(ExecutorType.Summon, CardId.EffectVeiler, TunerSummon);
            AddExecutor(ExecutorType.Summon, CardId.GhostOgre, TunerSummon);
            AddExecutor(ExecutorType.Summon, CardId.AshBlossom, TunerSummon);
            AddExecutor(ExecutorType.SpSummon, CardId.CrystronHalqifibrax, CrystronHalqifibraxSummon);
            AddExecutor(ExecutorType.Activate, CardId.CrystronHalqifibrax, CrystronHalqifibraxEffect);
            AddExecutor(ExecutorType.Activate, CardId.Linkuriboh, LinkuribohEffect);
            AddExecutor(ExecutorType.Activate, CardId.GlowUpBulb, GlowUpBulbEffect);
            AddExecutor(ExecutorType.SpSummon, CardId.BorrelswordDragon, BorrelswordDragonSummon);
            AddExecutor(ExecutorType.Activate, CardId.BorrelswordDragon, BorrelswordDragonEffect);

            AddExecutor(ExecutorType.SpSummon, CardId.KnightmarePhoenix, KnightmarePhoenixSummon);
            AddExecutor(ExecutorType.Activate, CardId.KnightmarePhoenix, KnightmarePhoenixEffect);
            AddExecutor(ExecutorType.SpSummon, CardId.KnightmareCerberus, KnightmareCerberusSummon);
            AddExecutor(ExecutorType.Activate, CardId.KnightmareCerberus, KnightmareCerberusEffect);
            AddExecutor(ExecutorType.SpSummon, CardId.KnightmareUnicorn, KnightmareUnicornSummon);
            AddExecutor(ExecutorType.Activate, CardId.KnightmareUnicorn, KnightmareUnicornEffect);
            AddExecutor(ExecutorType.SpSummon, CardId.TopologicBomberDragon, TopologicBomberDragonSummon);
            AddExecutor(ExecutorType.SpSummon, CardId.KnightmarePhoenix, KnightmarePhoenixUnlockSummon);
            AddExecutor(ExecutorType.SpSummon, CardId.KnightmareCerberus, KnightmareCerberusUnlockSummon);

            AddExecutor(ExecutorType.SpSummon, CardId.Hayate, HayateSummon);
            AddExecutor(ExecutorType.Activate, CardId.Hayate, HayateEffect);
            AddExecutor(ExecutorType.SpSummon, CardId.Kaina, KainaSummon);
            AddExecutor(ExecutorType.Activate, CardId.Kaina, KainaEffect);
            AddExecutor(ExecutorType.SpSummon, CardId.Shizuku, ShizukuSummon);
            AddExecutor(ExecutorType.Activate, CardId.AreaZero, AreaZeroEffect);
            AddExecutor(ExecutorType.Summon, CardId.SphereMode, SphereModeSummon);
            AddExecutor(ExecutorType.Summon, CardId.Raye, RayeSummon);
            AddExecutor(ExecutorType.Summon, CardId.Roze, RozeSummon);

            AddExecutor(ExecutorType.Activate, CardId.HerculesBase, HerculesBaseEffect);

            // When Widow Anchor is absent from the GY, Shizuku can search it first,
            // use it on Shizuku, and let Multirole set it immediately afterwards.
            AddExecutor(ExecutorType.Activate, CardId.Shizuku, ShizukuFirstEndPhaseEffect);
            AddExecutor(ExecutorType.Activate, CardId.WidowAnchor, WidowAnchorEndPhaseEffect);
            AddExecutor(ExecutorType.Activate, CardId.Multirole, MultiroleEndPhaseEffect);
            AddExecutor(ExecutorType.Activate, CardId.Shizuku, ShizukuEffect);

            AddExecutor(ExecutorType.SpellSet, CardId.HornetDrones, SetHornetDrones);
            AddExecutor(ExecutorType.SpellSet, CardId.SolemnWarning, Util.IsTurn1OrMain2);
            AddExecutor(ExecutorType.SpellSet, CardId.SolemnJudgment, Util.IsTurn1OrMain2);
            AddExecutor(ExecutorType.SpellSet, CardId.WidowAnchor, Util.IsTurn1OrMain2);
            AddExecutor(ExecutorType.SpellSet, CardId.SharkCannon, Util.IsTurn1OrMain2);
            AddExecutor(ExecutorType.SpellSet, CardId.Linkage, SetLinkage);
            AddExecutor(ExecutorType.SpellSet, CardId.UltimateSky, SetUltimateSky);
            AddExecutor(ExecutorType.SpellSet, CardId.TheFallenAndTheVirtuous,
                SetFallenAndVirtuous);
            AddExecutor(ExecutorType.SpellSet, CardId.ForbiddenDroplet, Util.IsTurn1OrMain2);
            AddExecutor(ExecutorType.SpellSet, CardId.ForbiddenCrown, Util.IsTurn1OrMain2);
            AddExecutor(ExecutorType.SpellSet, CardId.TwinTwisters, Util.IsTurn1OrMain2);
            AddExecutor(ExecutorType.SpellSet, CardId.MetalfoesFusion, Util.IsTurn1OrMain2);
            AddExecutor(ExecutorType.SpellSet, CardId.HerculesBase, SetWhenHandIsFull);

            AddExecutor(ExecutorType.Activate, CardId.EvenlyMatched, EvenlyMatchedEffect);

            AddExecutor(ExecutorType.Repos, DefaultMonsterRepos);
        }

        public override bool OnSelectHand()
        {
            return true;
        }

        public override void OnNewTurn()
        {
            RetireSkyStrategicPlan();
            StrategicSearchTargetPending = 0;
            KagariSummoned = false;
            ShizukuSummoned = false;
            HayateSummoned = false;
            KainaSummoned = false;
            ZeroSummoned = false;
            CamelliaSummoned = false;
            AmatsuSummoned = false;
            AzaleaSummoned = false;
            ZekeSummoned = false;
            BorrelswordAttackEffectUsed = false;
            BorrelswordPositionEffectUsed = false;
            BorrelswordComboStarted = false;
            TopologicComboStarted = false;
            NormalSummoned = false;
            RayeGraveEffectUsed = false;
            AreaZeroSetForJamming = false;
            WidowAnchorTakeControl = false;
            SharkCannonBanishOnly = false;
            AmatsuRedirectPending = false;
            SphereModeTributesPending = false;
            WidowAnchorTarget = null;
            SharkCannonTarget = null;
            ForbiddenCrownTarget = null;
            TripleTacticsTalentTarget = null;
            AzaleaDestroyTarget = null;
            LemnisgateRecyclePairCount = 0;
            LemnisgateRecycleSelectionCount = 0;
            ZeroDestroyTarget = null;
            FallenAndVirtuousDestroyTarget = null;
            FallenAndVirtuousReviveTarget = null;
            FallenAndVirtuousPreferDestroy = false;
            LinkageOtkSendTarget = null;
            PlannedLinkageFuelId = 0;
            HornetSetupStage = HornetZeroSetupStage.None;
            LinkageOtk = LinkageOtkStage.None;
            RayeToZeroSetupPending = false;
            PreserveEmptyMainZoneForZero = false;
            ZeroSearchPending = false;
            LinkageSetupToZeroPending = false;
            UltimateSkyLinkageSetupPending = false;
            UltimateSkyChainNegatePending = false;
            LastUltimateSkyTraceSignature = null;
            OpponentAddedFromMainDeckThisTurn = false;
            if (ZeroExitClearancePending &&
                (!Bot.HasInMonstersZone(CardId.Raye) ||
                !Bot.HasInMonstersZone(CardId.Roze)))
                ZeroExitClearancePending = false;
            ForbiddenDropletTargets.Clear();
            UltimateSkyTargets.Clear();
            ControlledEnemyCards.RemoveAll(card => card == null || card.Controller != 0 ||
                card.Location != CardLocation.MonsterZone);
            CurrentChainHandledCards.Clear();
            TurnNegatedCards.Clear();
            base.OnNewTurn();
        }

        public override void OnSummoning()
        {
            if (Duel.LastSummonPlayer == 0)
                NormalSummoned = true;
            base.OnSummoning();
        }

        public override void OnChaining(int player, ClientCard card)
        {
            if (player == 1)
                ObserveSkyStrikerMirror(card, true);
            base.OnChaining(player, card);
        }

        public override void OnChainEnd()
        {
            RetireSkyStrategicPlan();
            TurnNegatedCards.RemoveAll(card => card == null ||
                card.Location != CardLocation.MonsterZone);
            foreach (ClientCard negated in Enemy.GetMonsters().Where(card =>
                card.IsFaceup() && card.IsDisabled()))
            {
                if (!TurnNegatedCards.Contains(negated))
                    TurnNegatedCards.Add(negated);
            }
            AmatsuRedirectPending = false;
            ForbiddenCrownTarget = null;
            TripleTacticsTalentTarget = null;
            AzaleaDestroyTarget = null;
            LemnisgateRecyclePairCount = 0;
            LemnisgateRecycleSelectionCount = 0;
            ZeroDestroyTarget = null;
            FallenAndVirtuousDestroyTarget = null;
            FallenAndVirtuousReviveTarget = null;
            FallenAndVirtuousPreferDestroy = false;
            LinkageOtkSendTarget = null;
            ForbiddenDropletTargets.Clear();
            UltimateSkyTargets.Clear();
            UltimateSkyLinkageSetupPending = false;
            UltimateSkyChainNegatePending = false;
            LastUltimateSkyTraceSignature = null;

            if (ZeroExitClearancePending &&
                (!Bot.HasInMonstersZone(CardId.Raye) ||
                !Bot.HasInMonstersZone(CardId.Roze)))
                ZeroExitClearancePending = false;

            if (RayeToZeroSetupPending && !Bot.HasInMonstersZone(CardId.Zero))
                RayeToZeroSetupPending = false;
            if (HornetSetupStage == HornetZeroSetupStage.AwaitFirstToken &&
                !Bot.HasInMonstersZone(CardId.Token))
                HornetSetupStage = HornetZeroSetupStage.None;
            if (HornetSetupStage == HornetZeroSetupStage.AwaitSecondToken &&
                !Bot.HasInMonstersZone(CardId.Token))
                HornetSetupStage = HornetZeroSetupStage.None;
            if (LinkageOtk == LinkageOtkStage.SummonKagari &&
                !Bot.HasInMonstersZone(CardId.Kagari))
                LinkageOtk = LinkageOtkStage.None;
            if (LinkageOtk == LinkageOtkStage.SummonZero &&
                !Bot.HasInMonstersZone(CardId.Zero))
                LinkageOtk = LinkageOtkStage.None;
            if (LinkageSetupToZeroPending && !Bot.HasInMonstersZone(CardId.Zero))
                LinkageSetupToZeroPending = false;
            if (LinkageOtk == LinkageOtkStage.RayeRozeReady &&
                (!Bot.HasInMonstersZone(CardId.Raye) ||
                !Bot.HasInMonstersZone(CardId.Roze)))
                LinkageOtk = LinkageOtkStage.None;

            CurrentChainHandledCards.Clear();
            base.OnChainEnd();
        }

        public override void OnNewPhase()
        {
            RetireSkyStrategicPlan();
            if (LinkageOtk != LinkageOtkStage.None &&
                (Duel.Phase <= DuelPhase.Main1 || Duel.Phase >= DuelPhase.Main2))
                LinkageOtk = LinkageOtkStage.None;
            if (HornetSetupStage != HornetZeroSetupStage.None &&
                Duel.Phase != DuelPhase.Main1)
                HornetSetupStage = HornetZeroSetupStage.None;
            if (Duel.Phase != DuelPhase.Main1)
                RayeToZeroSetupPending = false;
            if (Duel.Phase != DuelPhase.Main1 && Duel.Phase != DuelPhase.Main2)
                LinkageSetupToZeroPending = false;
            base.OnNewPhase();
        }

        public override void OnMove(ClientCard card, int previousControler, int previousLocation,
            int currentControler, int currentLocation)
        {
            RetireSkyStrategicPlan();
            if (previousControler == 1 && currentControler == 1 &&
                (previousLocation & (int)CardLocation.Deck) != 0 &&
                (currentLocation & (int)CardLocation.Hand) != 0 &&
                Duel.Phase != DuelPhase.Draw)
            {
                OpponentAddedFromMainDeckThisTurn = true;
                Logger.DebugWriteLine("[SkyStriker2026] Droll window: opponent added " +
                    "a card from Main Deck to hand outside the Draw Phase.");
            }

            if (currentControler == 1)
                ObserveSkyStrikerMirror(card, IsPublicOpponentLocation(card, currentLocation));
            ChainInfo solving = Duel.GetCurrentSolvingChainInfo();
            if (card != null && MultiroleSetCards.Contains(card) &&
                (currentControler != 0 || currentLocation != (int)CardLocation.SpellZone))
                MultiroleSetCards.Remove(card);

            if (card != null && ControlledEnemyCards.Contains(card) &&
                (currentControler != 0 ||
                (currentLocation & (int)CardLocation.MonsterZone) == 0))
                ControlledEnemyCards.Remove(card);

            if (card != null && card.IsCode(CardId.Zero) && previousControler == 0 &&
                (previousLocation & (int)CardLocation.MonsterZone) != 0 &&
                (currentControler != 0 ||
                (currentLocation & (int)CardLocation.MonsterZone) == 0))
            {
                PreserveEmptyMainZoneForZero = false;
                ZeroSearchPending = false;
            }

            if (card != null && previousControler == 0 && currentControler == 0 &&
                previousLocation == (int)CardLocation.Grave &&
                currentLocation == (int)CardLocation.SpellZone &&
                solving != null && solving.ActivatePlayer == 0 &&
                solving.IsActivateCode(CardId.Multirole))
                MultiroleSetCards.Add(card);

            bool gainedEnemyMonster = card != null && previousControler == 1 && currentControler == 0 &&
                (currentLocation & (int)CardLocation.MonsterZone) != 0 &&
                solving != null && solving.ActivatePlayer == 0 &&
                solving.IsActivateCode(CardId.WidowAnchor, CardId.SharkCannon,
                    CardId.TripleTacticsTalent);
            if (gainedEnemyMonster)
            {
                if (!ControlledEnemyCards.Contains(card))
                    ControlledEnemyCards.Add(card);
                if (solving.IsActivateCode(CardId.WidowAnchor))
                    WidowAnchorTarget = card;
            }

            if (card != null && currentControler == 0 &&
                (previousLocation & (int)CardLocation.MonsterZone) == 0 &&
                (currentLocation & (int)CardLocation.MonsterZone) != 0)
            {
                if (card.IsCode(CardId.Token))
                {
                    if (HornetSetupStage == HornetZeroSetupStage.AwaitFirstToken)
                        HornetSetupStage = HornetZeroSetupStage.NeedKagari;
                    else if (HornetSetupStage == HornetZeroSetupStage.AwaitSecondToken)
                        HornetSetupStage = HornetZeroSetupStage.NeedBridgeLink;
                }

                if (card.IsCode(CardId.Kagari))
                {
                    KagariSummoned = true;
                    if (HornetSetupStage == HornetZeroSetupStage.NeedKagari)
                        HornetSetupStage = HornetZeroSetupStage.KagariReady;
                    if (LinkageOtk == LinkageOtkStage.SummonKagari)
                        LinkageOtk = LinkageOtkStage.KagariReady;
                }
                else if (card.IsCode(CardId.Shizuku))
                    ShizukuSummoned = true;
                else if (card.IsCode(CardId.Hayate))
                    HayateSummoned = true;
                else if (card.IsCode(CardId.Kaina))
                    KainaSummoned = true;
                else if (card.IsCode(CardId.Zero))
                {
                    bool setupZero = HornetSetupStage == HornetZeroSetupStage.NeedZero ||
                        RayeToZeroSetupPending || LinkageSetupToZeroPending ||
                        ZeroExitClearancePending;
                    ZeroSummoned = true;
                    ZeroSearchPending = true;
                    LinkageSetupToZeroPending = false;
                    ZeroExitClearancePending = false;
                    if (HornetSetupStage == HornetZeroSetupStage.NeedZero)
                        HornetSetupStage = HornetZeroSetupStage.None;
                    if (RayeToZeroSetupPending)
                        RayeToZeroSetupPending = false;
                    if (LinkageOtk == LinkageOtkStage.SummonZero)
                        LinkageOtk = LinkageOtkStage.ZeroReady;
                    if (setupZero)
                        PreserveEmptyMainZoneForZero = true;
                }
                else if (card.IsCode(CardId.Camellia))
                    CamelliaSummoned = true;
                else if (card.IsCode(CardId.Amatsu))
                {
                    AmatsuSummoned = true;
                    if (HornetSetupStage == HornetZeroSetupStage.NeedBridgeLink)
                        HornetSetupStage = HornetZeroSetupStage.NeedZero;
                }
                else if (card.IsCode(CardId.Azalea))
                    AzaleaSummoned = true;
                else if (card.IsCode(CardId.Zeke))
                    ZekeSummoned = true;

                if (HornetSetupStage == HornetZeroSetupStage.NeedBridgeLink &&
                    card.IsCode(CardId.Hayate, CardId.Kaina, CardId.Shizuku))
                    HornetSetupStage = HornetZeroSetupStage.NeedZero;
            }
            if (card != null &&
                (currentLocation & (int)CardLocation.MonsterZone) != 0 &&
                (previousLocation & (int)CardLocation.MonsterZone) == 0)
                TurnNegatedCards.Remove(card);
            base.OnMove(card, previousControler, previousLocation, currentControler, currentLocation);
        }

        public override IList<ClientCard> OnSelectCard(IList<ClientCard> cards, int min, int max,
            int hint, bool cancelable)
        {
            if (SphereModeTributesPending && (hint == HintMsg.Release || (min == 3 && max == 3)))
            {
                SphereModeTributesPending = false;
                List<ClientCard> tributes = cards.Where(card => card.Controller == 1 && card.IsMonster())
                    .OrderByDescending(GetCardUtility).Take(3).ToList();
                if (tributes.Count >= min)
                    return Util.CheckSelectCount(tributes, cards, min, max);
                if (cancelable)
                    return new List<ClientCard>();
            }

            if (AmatsuRedirectPending && hint == HintMsg.Destroy &&
                cards.All(card => card.Controller == 0 && IsSkyStrikerAceLink(card)))
            {
                AmatsuRedirectPending = false;
                return Util.CheckSelectCount(cards.OrderBy(GetFieldResourceValue).ToList(),
                    cards, min, max);
            }

            ChainInfo solving = Duel.GetCurrentSolvingChainInfo();
            ClientCard activating = Duel.GetCurrentChainCard();
            if (activating != null && activating.Controller == 0)
            {
                if (activating.IsCode(CardId.TheFallenAndTheVirtuous))
                {
                    if (hint == HintMsg.ToGrave && cards.Any(card => card.Location == CardLocation.Extra))
                        return Util.CheckSelectCount(OrderCardsById(cards,
                            GetFallenAndVirtuousExtraDeckPriority(), false), cards, min, max);
                    if (hint == HintMsg.Destroy)
                        return SelectFallenAndVirtuousDestroyTarget(cards, min, max);
                    if (hint == HintMsg.SpSummon)
                        return SelectFallenAndVirtuousReviveTarget(cards, min, max);
                }

                if (activating.IsCode(CardId.ForbiddenDroplet) && hint == HintMsg.ToGrave)
                {
                    List<ClientCard> safeCosts = cards.Where(card =>
                        GetDropletCostValue(card) < 5000).OrderBy(GetDropletCostValue).ToList();
                    if (safeCosts.Count < min && cancelable)
                    {
                        ForbiddenDropletTargets.Clear();
                        return new List<ClientCard>();
                    }
                    int planned = Math.Min(ForbiddenDropletTargets.Count, safeCosts.Count);
                    int desired = Math.Min(max, Math.Max(min, planned));
                    List<ClientCard> costs = safeCosts.Take(desired).ToList();
                    return Util.CheckSelectCount(costs, cards, desired, desired);
                }

                if (activating.IsCode(CardId.UltimateSky) &&
                    (hint == HintMsg.Disable || hint == HintMsg.Faceup ||
                    hint == HintMsg.Target || hint == HintMsg.Select))
                    return SelectUltimateSkyTargets(cards, min, max);

                if (activating.IsCode(CardId.Kaina) && hint == HintMsg.Faceup)
                    return SelectKainaTarget(cards, min, max);

                if (activating.IsCode(CardId.Camellia) && hint == HintMsg.ToGrave &&
                    cards.Any(card => card.Controller == 1))
                    return Util.CheckSelectCount(OrderEnemyTargets(cards), cards, min, max);

                if (activating.IsCode(CardId.Azalea) && hint == HintMsg.Destroy)
                    return SelectAzaleaDestroyTarget(cards, min, max);

                if (activating.IsCode(CardId.Azalea) && hint == HintMsg.Remove)
                    return Util.CheckSelectCount(cards.OrderBy(GetGraveResourceValue).ToList(),
                        cards, min, max);

                if (activating.IsCode(CardId.Zeke))
                {
                    if (hint == HintMsg.Remove)
                        return Util.CheckSelectCount(OrderEnemyTargets(cards), cards, min, max);
                    if (hint == HintMsg.ToGrave)
                        return Util.CheckSelectCount(cards.OrderBy(GetFieldResourceValue).ToList(), cards, min, max);
                }

                if (activating.IsCode(CardId.Amatsu) && hint == HintMsg.Destroy)
                {
                    List<ClientCard> ordered = cards.Any(card => card.Controller == 1)
                        ? OrderEnemyTargets(cards)
                        : cards.OrderBy(GetFieldResourceValue).ToList();
                    return Util.CheckSelectCount(ordered, cards, min, max);
                }

                if (activating.IsCode(CardId.Lemnisgate) && hint == HintMsg.ToDeck)
                    return SelectLemnisgateRecycleCards(cards, min, max);

                if (activating.IsCode(CardId.EcclesiaAndTheDarkDragon) && hint == HintMsg.ToDeck)
                {
                    List<ClientCard> fusion = cards.Where(card =>
                        card.IsCode(CardId.DragonThatDevoursTheDogma)).ToList();
                    if (fusion.Count > 0)
                        return Util.CheckSelectCount(fusion, cards, min, max);
                    return Util.CheckSelectCount(OrderEnemyTargets(cards), cards, min, max);
                }
            }

            if (solving != null && solving.ActivatePlayer == 0 &&
                solving.IsActivateCode(CardId.TheFallenAndTheVirtuous))
            {
                if (hint == HintMsg.ToGrave && cards.Any(card => card.Location == CardLocation.Extra))
                    return Util.CheckSelectCount(OrderCardsById(cards,
                        GetFallenAndVirtuousExtraDeckPriority(), false), cards, min, max);
                if (hint == HintMsg.Destroy)
                    return SelectFallenAndVirtuousDestroyTarget(cards, min, max);
            }

            if (solving != null && solving.ActivatePlayer == 0 &&
                solving.IsActivateCode(CardId.AlbionTheBrandedDragon))
                return Util.CheckSelectCount(OrderCardsById(cards,
                    new[] { CardId.TheFallenAndTheVirtuous }, false), cards, min, max);

            if (solving != null && solving.ActivatePlayer == 0 && solving.IsActivateCode(CardId.Linkage))
            {
                if (hint == HintMsg.ToGrave)
                {
                    List<ClientCard> ordered = new List<ClientCard>();
                    ClientCard intended = FindMatchingOfferedCard(cards, LinkageOtkSendTarget);
                    if (intended != null)
                        ordered.Add(intended);
                    ordered.AddRange(cards
                        .Where(card => !ordered.Contains(card))
                        .OrderBy(card => IsSkyStrikerAce(card) && card.Attacked ? 0 :
                            ControlledEnemyCards.Contains(card) ? 1 : 2)
                        .ThenBy(GetFieldResourceValue)
                        .ToList());
                    LinkageOtkSendTarget = null;
                    PlannedLinkageFuelId = 0;
                    return Util.CheckSelectCount(ordered, cards, min, max);
                }
                if (hint == HintMsg.SpSummon)
                {
                    SelectSkyStrikerLinkPlace();
                    return Util.CheckSelectCount(OrderCardsById(cards, GetLinkageExtraDeckPriority(), false),
                        cards, min, max);
                }
            }

            if (solving != null && solving.ActivatePlayer == 0 && solving.IsActivateCode(CardId.Zero))
            {
                if (hint == HintMsg.AddToHand)
                    return SelectZeroSearch(cards, min, max);
                if (hint == HintMsg.SpSummon)
                    return Util.CheckSelectCount(OrderCardsById(cards, new[] { CardId.Raye, CardId.Roze }, false),
                        cards, min, max);
                if (hint == HintMsg.Destroy)
                    return SelectZeroDestroyTarget(cards, min, max);
            }

            if (solving != null && solving.ActivatePlayer == 0 && solving.IsActivateCode(CardId.Roze) &&
                hint == HintMsg.Disable)
                return Util.CheckSelectCount(OrderEnemyTargets(cards), cards, min, max);

            if (solving != null && solving.ActivatePlayer == 0 && solving.IsActivateCode(CardId.Camellia) &&
                hint == HintMsg.ToGrave)
            {
                List<ClientCard> ordered = OrderCardsById(cards, new[]
                {
                    CardId.Linkage,
                    CardId.Engage,
                    CardId.Lemnisgate,
                    CardId.WidowAnchor,
                    CardId.Raye,
                    CardId.Roze
                }, false);
                return Util.CheckSelectCount(ordered, cards, min, max);
            }

            if (solving != null && solving.ActivatePlayer == 0 &&
                solving.IsActivateCode(CardId.RadiantTyphoonVision))
            {
                if (hint == HintMsg.AddToHand)
                    return Util.CheckSelectCount(OrderCardsById(cards,
                        new[] { CardId.MysticalSpaceTyphoon }, false), cards, min, max);
                if (hint == HintMsg.Discard || hint == HintMsg.ToGrave)
                    return Util.CheckSelectCount(cards.OrderBy(GetHandResourceValue).ToList(), cards, min, max);
            }

            if (solving != null && solving.ActivatePlayer == 0 &&
                solving.IsActivateCode(CardId.DragonThatDevoursTheDogma))
            {
                if (hint == HintMsg.ToDeck)
                    return Util.CheckSelectCount(cards.OrderByDescending(GetEnemyGraveResourceValue).ToList(),
                        cards, min, max);
                if (hint == HintMsg.AddToHand)
                    return Util.CheckSelectCount(OrderCardsById(cards,
                        new[] { CardId.TheFallenAndTheVirtuous }, false), cards, min, max);
            }

            if (solving != null && solving.ActivatePlayer == 0 &&
                solving.IsActivateCode(CardId.ForbiddenDroplet) && hint == HintMsg.OperateCard)
                return SelectForbiddenDropletTargets(cards, min, max);

            if (solving != null && solving.ActivatePlayer == 0 &&
                solving.IsActivateCode(CardId.TripleTacticsTalent))
            {
                if (hint == HintMsg.Control)
                    return SelectTripleTacticsTalentControlTarget(cards, min, max);
                if (hint == HintMsg.ToDeck)
                    return SelectTripleTacticsTalentHandTarget(cards, min, max);
            }

            if (solving != null && solving.ActivatePlayer == 0 &&
                solving.IsActivateCode(CardId.Kaina) && hint == HintMsg.Faceup)
                return SelectKainaTarget(cards, min, max);

            if (solving != null && solving.ActivatePlayer == 0 &&
                solving.IsActivateCode(CardId.ForbiddenCrown) && hint == HintMsg.Faceup)
            {
                List<ClientCard> ordered = new List<ClientCard>();
                ClientCard intended = cards.FirstOrDefault(card =>
                    card == ForbiddenCrownTarget || ForbiddenCrownTarget != null &&
                    card.Controller == ForbiddenCrownTarget.Controller &&
                    card.Location == ForbiddenCrownTarget.Location &&
                    card.Sequence == ForbiddenCrownTarget.Sequence &&
                    card.IsCode(ForbiddenCrownTarget.Id));
                if (intended != null)
                    ordered.Add(intended);
                ordered.AddRange(OrderEnemyTargets(cards).Where(card => !ordered.Contains(card)));
                ForbiddenCrownTarget = null;
                return Util.CheckSelectCount(ordered, cards, min, max);
            }

            if (solving != null && solving.ActivatePlayer == 0 && solving.IsActivateCode(CardId.Lemnisgate) &&
                hint == HintMsg.ReturnToHand)
                return SelectLemnisgateBounceTargets(cards, min, max);

            if (solving != null && solving.ActivatePlayer == 0 && solving.IsActivateCode(CardId.Lemnisgate) &&
                hint == HintMsg.SpSummon)
            {
                SelectSkyStrikerLinkPlace();
                return Util.CheckSelectCount(OrderCardsById(cards, GetLemnisgateExtraDeckPriority(), false),
                    cards, min, max);
            }

            if (solving != null && solving.ActivatePlayer == 0 &&
                solving.IsActivateCode(CardId.SuperStarslayerTyphon) && hint == HintMsg.ReturnToHand)
                return Util.CheckSelectCount(OrderEnemyTargets(cards), cards, min, max);

            if (solving != null && solving.ActivatePlayer == 0 && solving.IsActivateCode(CardId.Multirole))
            {
                List<ClientCard> ordered = OrderCardsById(cards, new[]
                {
                    CardId.Engage,
                    CardId.WidowAnchor,
                    CardId.SharkCannon,
                    CardId.HornetDrones,
                    CardId.Afterburners,
                    CardId.JammingWaves
                }, true);
                return Util.CheckSelectCount(ordered, cards, min, max);
            }

            if (solving != null && solving.ActivatePlayer == 0 && solving.IsActivateCode(CardId.AreaZero))
            {
                List<ClientCard> ordered = OrderCardsById(cards, GetSkyStrikerSearchPriority(true), false);
                return Util.CheckSelectCount(ordered, cards, min, max);
            }

            return base.OnSelectCard(cards, min, max, hint, cancelable);
        }

        public override int OnSelectPlace(int cardId, int player, CardLocation location, int available)
        {
            if (player == 0 && location == CardLocation.MonsterZone)
            {
                if (cardId == CardId.TopologicBomberDragon &&
                    (available & Zones.ExtraMonsterZones) != 0)
                    return available & Zones.ExtraMonsterZones;

                bool shouldTriggerTopologic = false;
                int topologicLinkedZones = 0;
                ClientCard topologic = Bot.GetMonsters().FirstOrDefault(card =>
                    card.IsCode(CardId.TopologicBomberDragon) && !card.IsDisabled());
                if (topologic != null)
                {
                    topologicLinkedZones = topologic.GetLinkedZones() & available & Zones.MainMonsterZones;
                    shouldTriggerTopologic = topologicLinkedZones != 0 &&
                        (Enemy.GetMonstersInMainZone().Count >= 2 ||
                        Util.GetProblematicEnemyMonster()?.Sequence < 5);
                }

                if (shouldTriggerTopologic)
                {
                    return topologicLinkedZones & -topologicLinkedZones;
                }

                bool shouldAvoidTopologicLinkedZones = topologicLinkedZones != 0 && (available & ~topologicLinkedZones) != 0;
                if (shouldAvoidTopologicLinkedZones)
                    available &= ~topologicLinkedZones;

                if ((cardId == CardId.KnightmarePhoenix || cardId == CardId.KnightmareCerberus) &&
                    (available & Zones.ExtraMonsterZones) != 0)
                    return available & Zones.ExtraMonsterZones;

                if (cardId == CardId.Hayate && (available & Zones.z5) != 0)
                    return Zones.z5;

                ClientCard hayate = Bot.MonsterZone.GetFirstMatchingCard(card => card.IsCode(CardId.Hayate));
                if ((hayate != null && cardId == CardId.Linkuriboh) ||
                    (hayate != null && cardId == CardId.CrystronHalqifibrax) ||
                    (hayate != null && cardId == CardId.BorrelswordDragon))
                {
                    int linked = hayate.GetLinkedZones() & available & Zones.MainMonsterZones;
                    if (linked != 0)
                        return linked & -linked;
                }

                if (shouldAvoidTopologicLinkedZones)
                    return available;
            }
            return base.OnSelectPlace(cardId, player, location, available);
        }

        public override CardPosition OnSelectPosition(int cardId, IList<CardPosition> positions)
        {
            if (LinkageOtk == LinkageOtkStage.RayeRozeReady &&
                (cardId == CardId.Raye || cardId == CardId.Roze) &&
                positions.Contains(CardPosition.FaceUpAttack))
                return CardPosition.FaceUpAttack;
            return base.OnSelectPosition(cardId, positions);
        }

        public override ClientCard OnSelectAttacker(IList<ClientCard> attackers, IList<ClientCard> defenders)
        {
            ClientCard hayate = attackers.FirstOrDefault(card => card.IsCode(CardId.Hayate));
            if (hayate != null && GetHayateCrashTarget(hayate, defenders) != null)
                return hayate;

            if (Bot.HasInMonstersZone(CardId.BorrelswordDragon, true))
            {
                ClientCard raye = attackers.FirstOrDefault(card => card.IsCode(CardId.Raye));
                if (raye != null)
                    return raye;

                ClientCard borrelsword = attackers.FirstOrDefault(card => card.IsCode(CardId.BorrelswordDragon));
                if (borrelsword != null)
                    return borrelsword;
            }
            return base.OnSelectAttacker(attackers, defenders);
        }

        public override BattlePhaseAction OnBattle(IList<ClientCard> attackers, IList<ClientCard> defenders)
        {
            if (defenders.Count == 0 && Bot.HasInMonstersZone(CardId.BorrelswordDragon, true))
            {
                ClientCard raye = attackers.FirstOrDefault(card => card.IsCode(CardId.Raye));
                if (raye != null)
                    return AI.Attack(raye, null);
            }
            return base.OnBattle(attackers, defenders);
        }

        public override BattlePhaseAction OnSelectAttackTarget(ClientCard attacker, IList<ClientCard> defenders)
        {
            if (attacker.IsCode(CardId.Hayate))
            {
                ClientCard target = GetHayateCrashTarget(attacker, defenders);
                if (target != null)
                {
                    return AI.Attack(attacker, target);
                }
                if (attacker.CanDirectAttack)
                    return AI.Attack(attacker, null);
            }
            return base.OnSelectAttackTarget(attacker, defenders);
        }

        public override bool OnPreBattleBetween(ClientCard attacker, ClientCard defender)
        {
            if (!defender.IsMonsterHasPreventActivationEffectInBattle() &&
                attacker.IsCode(CardId.BorrelswordDragon) && !attacker.IsDisabled() &&
                !BorrelswordAttackEffectUsed && defender.IsFaceup())
            {
                int halfAttack = (defender.Attack + 1) / 2;
                attacker.RealPower += halfAttack;
                if (defender.IsAttack())
                    defender.RealPower = halfAttack;
            }
            return base.OnPreBattleBetween(attacker, defender);
        }

        public override bool OnSelectYesNo(int desc)
        {
            if (desc == Util.GetStringId(CardId.Zero, 2))
            {
                if (LinkageOtk == LinkageOtkStage.RayeRozeReady)
                    return false;
                if (ZeroDestroyTarget == null)
                    ZeroDestroyTarget = GetBestZeroDestroyTarget();
                return ZeroDestroyTarget != null;
            }
            if (desc == Util.GetStringId(CardId.Roze, 2))
                return Enemy.GetMonsters().Any(card => card.IsFaceup() && !card.IsDisabled());
            if (desc == Util.GetStringId(CardId.Lemnisgate, 1))
                return ShouldSelectLemnisgateOptionalEffect(1,
                    GetUsefulLemnisgateBounceTargets().Count > 0,
                    ShouldUseLemnisgateGraveLink());
            if (desc == Util.GetStringId(CardId.Lemnisgate, 2))
                return ShouldSelectLemnisgateOptionalEffect(2,
                    GetUsefulLemnisgateBounceTargets().Count > 0,
                    ShouldUseLemnisgateGraveLink());
            if (desc == Util.GetStringId(CardId.Engage, 0))
                return true;
            if (desc == Util.GetStringId(CardId.AreaZero, 2))
                return true;
            if (desc == Util.GetStringId(CardId.WidowAnchor, 0))
                return WidowAnchorTakeControl && WidowAnchorTarget != null;
            if (desc == Util.GetStringId(CardId.SharkCannon, 0))
                return !SharkCannonBanishOnly && ShouldReviveWithSharkCannon(SharkCannonTarget);
            if (desc == Util.GetStringId(CardId.KnightmarePhoenix, 1) ||
                desc == Util.GetStringId(CardId.KnightmareCerberus, 1) ||
                desc == Util.GetStringId(CardId.KnightmareUnicorn, 1))
                return true;
            if (desc == Util.GetStringId(CardId.JammingWaves, 0))
            {
                ClientCard target = Util.GetBestEnemyMonster();
                if (target == null)
                    return false;
                AI.SelectCard(target);
                return true;
            }
            if (desc == Util.GetStringId(CardId.Afterburners, 0))
            {
                ClientCard target = SkyStrikerMirrorObserved
                    ? GetMirrorPriorityEnemySpell() : null;
                if (target == null)
                    target = Util.GetBestEnemySpell();
                if (target == null)
                    return false;
                AI.SelectCard(target);
                return true;
            }
            return base.OnSelectYesNo(desc);
        }

        public override int OnSelectOption(IList<int> options)
        {
            int ultimateField = options.IndexOf(Util.GetStringId(CardId.UltimateSky, 1));
            int ultimateChain = options.IndexOf(Util.GetStringId(CardId.UltimateSky, 2));
            if (ultimateField >= 0 || ultimateChain >= 0)
            {
                if (ultimateChain >= 0 && ShouldSelectUltimateSkyChainMode(
                    UltimateSkyChainNegatePending, IsLiveUltimateSkyChainNegate()))
                {
                    UltimateSkyChainNegatePending = false;
                    return ultimateChain;
                }
                UltimateSkyChainNegatePending = false;
                if (ultimateField >= 0)
                    return ultimateField;
                return ultimateChain;
            }

            int talentDraw = options.IndexOf(Util.GetStringId(CardId.TripleTacticsTalent, 0));
            int talentControl = options.IndexOf(Util.GetStringId(CardId.TripleTacticsTalent, 1));
            int talentShuffle = options.IndexOf(Util.GetStringId(CardId.TripleTacticsTalent, 2));
            if (talentDraw >= 0 || talentControl >= 0 || talentShuffle >= 0)
                return SelectTripleTacticsTalentOption(options, talentDraw,
                    talentControl, talentShuffle);

            int drawVision = options.IndexOf(Util.GetStringId(CardId.RadiantTyphoonVision, 2));
            int searchMst = options.IndexOf(Util.GetStringId(CardId.RadiantTyphoonVision, 3));
            if (drawVision >= 0 || searchMst >= 0)
            {
                bool canDiscard = Bot.Hand.Any(card =>
                    (card.HasType(CardType.QuickPlay) || card.IsCode(CardId.RadiantTyphoonVision)));
                if (drawVision >= 0 && (canDiscard || Bot.Hand.Count >= 3))
                    return drawVision;
                if (searchMst >= 0)
                    return searchMst;
                return drawVision;
            }

            int destroyFallen = options.IndexOf(Util.GetStringId(CardId.TheFallenAndTheVirtuous, 1));
            int reviveFallen = options.IndexOf(Util.GetStringId(CardId.TheFallenAndTheVirtuous, 2));
            if (destroyFallen >= 0 || reviveFallen >= 0)
            {
                if (!IsLiveFallenAndVirtuousDestroyTarget(FallenAndVirtuousDestroyTarget))
                    FallenAndVirtuousDestroyTarget = GetFallenAndVirtuousDestroyTarget();
                if (destroyFallen >= 0 && FallenAndVirtuousDestroyTarget != null &&
                    (FallenAndVirtuousPreferDestroy || reviveFallen < 0 ||
                    FallenAndVirtuousReviveTarget == null))
                    return destroyFallen;
                if (reviveFallen >= 0)
                    return reviveFallen;
                return destroyFallen;
            }

            ChainInfo solving = Duel.GetCurrentSolvingChainInfo();
            if (solving != null && solving.ActivatePlayer == 0 &&
                solving.IsActivateCode(CardId.AlbionTheBrandedDragon))
            {
                int set = options.IndexOf(1153);
                int add = options.IndexOf(1190);
                if (set >= 0 && ShouldSetFallenAndVirtuousFromAlbion())
                    return set;
                if (add >= 0)
                    return add;
                if (set >= 0)
                    return set;
            }

            int destroyMonsters = options.IndexOf(Util.GetStringId(CardId.LightningStorm, 0));
            int destroySpells = options.IndexOf(Util.GetStringId(CardId.LightningStorm, 1));
            if (destroyMonsters >= 0 || destroySpells >= 0)
            {
                int attackMonsters = Enemy.GetMonsters().Count(card => card.IsAttack());
                if (destroyMonsters >= 0 && destroySpells >= 0)
                    return Enemy.GetSpellCount() > attackMonsters ? destroySpells : destroyMonsters;
                return destroyMonsters >= 0 ? destroyMonsters : destroySpells;
            }

            return base.OnSelectOption(options);
        }

        private static bool ShouldSelectUltimateSkyChainMode(bool pending,
            bool liveReevaluation)
        {
            return pending || liveReevaluation;
        }

        private bool ReinforcementOfTheArmyEffect()
        {
            if (!StrategicAllows(SkyStrategicActionKind.ActivateReinforcement))
                return false;
            AI.SelectCard(Bot.HasInHandOrHasInMonstersZone(CardId.Raye)
                ? new[] { CardId.Roze, CardId.Raye }
                : new[] { CardId.Raye, CardId.Roze });
            return true;
        }

        private bool DrollLockBirdEffect()
        {
            if (Duel.Phase == DuelPhase.Draw || !OpponentAddedFromMainDeckThisTurn)
                return false;

            OpponentAddedFromMainDeckThisTurn = false;
            Logger.DebugWriteLine("[SkyStriker2026] Droll: use the observed " +
                "Main Deck-to-hand response window.");
            return true;
        }

        private bool AshBlossomEffect()
        {
            ClientCard last = Util.GetLastChainCard();
            bool disabledFaceupFieldMonster = last != null && last.IsMonster() &&
                last.Location == CardLocation.MonsterZone && last.IsFaceup() &&
                last.IsDisabled();
            bool effectGuaranteedNegated = last != null &&
                (TurnNegatedCards.Contains(last) || IsCurrentChainHandled(last));
            if (ShouldHoldAshAgainstGuaranteedNegation(
                disabledFaceupFieldMonster || effectGuaranteedNegated))
            {
                Logger.DebugWriteLine("[SkyStriker2026] Ash: hold because the " +
                    "current field effect is already guaranteed negated.");
                return false;
            }
            return DefaultAshBlossomAndJoyousSpring();
        }

        private static bool ShouldHoldAshAgainstGuaranteedNegation(
            bool effectGuaranteedNegated)
        {
            return effectGuaranteedNegated;
        }

        private bool LightningStormEffect()
        {
            return Enemy.GetMonsters().Any(card => card.IsAttack()) || Enemy.GetSpellCount() > 0;
        }

        private bool UltimateSkyEffect()
        {
            UltimateSkyTargets.Clear();
            UltimateSkyChainNegatePending = false;
            if (DefaultCheckWhetherCardIsNegated(Card))
            {
                TraceUltimateSkyDecision("hold: card is negated", 0, null);
                return false;
            }
            if (Duel.CurrentChain.Any(card => card.Controller == 0 &&
                card.IsCode(CardId.UltimateSky)))
            {
                TraceUltimateSkyDecision("hold: Ultimate Sky already in chain", 0, null);
                return false;
            }

            int chainScore = GetUltimateSkyChainNegateScore();
            if (IsLiveUltimateSkyChainNegate(chainScore))
            {
                ClientCard last = Util.GetLastChainCard();
                UltimateSkyChainNegatePending = true;
                MarkCurrentChainHandled(last);
                TraceUltimateSkyDecision("activate: negate hand/GY monster", chainScore,
                    null);
                return true;
            }

            if (Duel.CurrentChain.Count > 0)
            {
                TraceUltimateSkyDecision("hold: chain effect below value threshold",
                    chainScore, null);
                return false;
            }

            if (CanStartUltimateSkyLinkagePlan())
            {
                UltimateSkyLinkageSetupPending = true;
                TraceUltimateSkyDecision("activate: Linkage fuel for Zero", 0, null);
                return true;
            }

            UltimateSkyTargets.AddRange(GetUltimateSkyFieldTargets());
            if (UltimateSkyTargets.Count == 0)
            {
                TraceUltimateSkyDecision("hold: no worthwhile field target", 0, null);
                return false;
            }
            TacticalDecision alternative = GetBestTacticalDecision();
            int ultimateScore = UltimateSkyTargets.Sum(GetUltimateSkyNegateScore) -
                UltimateSkyTargets.Count * 400;
            if (alternative.Kind != TacticalActionKind.None &&
                alternative.Score >= ultimateScore)
            {
                TraceUltimateSkyDecision("hold: better tactical response", 0,
                    alternative);
                UltimateSkyTargets.Clear();
                return false;
            }
            TraceUltimateSkyDecision("activate: field negate score=" +
                ultimateScore.ToString(), 0, alternative);
            foreach (ClientCard target in UltimateSkyTargets)
                MarkCurrentChainHandled(target);
            return true;
        }

        private bool CanStartUltimateSkyLinkagePlan()
        {
            return Card.Location == CardLocation.Hand && Duel.Player == 0 &&
                (Duel.Phase == DuelPhase.Main1 || Duel.Phase == DuelPhase.Main2) &&
                EmptyMainMonsterZone() && Bot.GetMonsterCount() == 0 &&
                Enemy.GetMonsterCount() > 0 &&
                Enemy.GetMonsters().Any(card => card.IsFaceup()) &&
                Bot.LifePoints > 800 &&
                Bot.HasInHand(CardId.Linkage) && Bot.HasInExtra(CardId.Zero) &&
                !HasBetterZeroStarterThanLinkage() &&
                StrategicAllows(SkyStrategicActionKind.ActivateUltimateLinkage);
        }

        private bool IsLiveUltimateSkyChainNegate()
        {
            return IsLiveUltimateSkyChainNegate(GetUltimateSkyChainNegateScore());
        }

        private bool IsLiveUltimateSkyChainNegate(int score)
        {
            ChainInfo last = Duel.CurrentChainInfo.LastOrDefault();
            if (last == null || last.ActivatePlayer != 1 ||
                !last.HasLocation(CardLocation.Hand | CardLocation.Grave) ||
                (last.ActivateType & (int)CardType.Monster) == 0 ||
                IsCurrentChainHandled(last.RelatedCard))
                return false;
            return score >= UltimateSkyChainNegateThreshold &&
                !Duel.CurrentChainInfo.Any(chain => chain.ActivatePlayer == 0 &&
                    chain.IsActivateCode(CardId.UltimateSky));
        }

        private int GetUltimateSkyChainNegateScore()
        {
            ChainInfo last = Duel.CurrentChainInfo.LastOrDefault();
            if (last == null || last.ActivatePlayer != 1 ||
                !last.HasLocation(CardLocation.Hand | CardLocation.Grave) ||
                (last.ActivateType & (int)CardType.Monster) == 0)
                return 0;

            ClientCard threat = last.RelatedCard ?? Util.GetLastChainCard();
            ClientCard previousOwnCard = Duel.CurrentChain.Count >= 2
                ? Duel.CurrentChain[Duel.CurrentChain.Count - 2] : null;
            return GetUltimateSkyChainPriority(threat == null ? 0 : threat.Id,
                GetObservedThreatScore(threat), Duel.Player == 0,
                previousOwnCard == null || previousOwnCard.Controller != 0
                    ? 0 : previousOwnCard.Id);
        }

        private static int GetUltimateSkyChainPriority(int threatId,
            int observedThreatScore, bool botTurn, int previousOwnCardId)
        {
            int score = Math.Max(0, observedThreatScore);
            if (botTurn && (threatId == CardId.MaxxC ||
                threatId == CardId.MulcharmyFuwalos))
                score += 5200;
            if (botTurn && threatId == CardId.DrollLockBird)
                score += 5000;

            bool protectingEngine = previousOwnCardId == CardId.Engage ||
                previousOwnCardId == CardId.Zero || previousOwnCardId == CardId.Raye ||
                previousOwnCardId == CardId.Kagari ||
                previousOwnCardId == CardId.Shizuku ||
                previousOwnCardId == CardId.Linkage ||
                previousOwnCardId == CardId.HornetDrones ||
                previousOwnCardId == CardId.Lemnisgate ||
                previousOwnCardId == CardId.Multirole ||
                previousOwnCardId == CardId.TheFallenAndTheVirtuous;
            bool engineHandTrap = threatId == CardId.AshBlossom ||
                threatId == CardId.GhostBelle || threatId == CardId.GhostOgre ||
                threatId == CardId.EffectVeiler;
            if (protectingEngine && engineHandTrap)
                score += 3400;
            return score;
        }

        private void TraceUltimateSkyDecision(string decision, int chainScore,
            TacticalDecision alternative)
        {
            ClientCard last = Util.GetLastChainCard();
            string lastLabel = last == null ? "none" :
                (last.Name ?? last.Id.ToString()) + "@" + last.Location.ToString();
            string targets = string.Join(",", Enemy.GetMonsters()
                .Concat(Enemy.GetSpells()).Where(card => card.IsFaceup())
                .Select(card => (card.Name ?? card.Id.ToString()) + "=" +
                    GetUltimateSkyNegateScore(card).ToString()).ToArray());
            string alternativeLabel = alternative == null ? "none" :
                alternative.Kind.ToString() + "=" + alternative.Score.ToString();
            string signature = Duel.Turn.ToString() + "|" + Duel.Player.ToString() +
                "|" + Duel.Phase.ToString() + "|" + Duel.CurrentChain.Count.ToString() +
                "|" + decision + "|" + chainScore.ToString() + "|" + lastLabel +
                "|" + targets + "|" + alternativeLabel;
            if (signature == LastUltimateSkyTraceSignature)
                return;
            LastUltimateSkyTraceSignature = signature;
            Logger.DebugWriteLine("[SkyStriker2026:UltimateSky] decision=" + decision +
                " turn=" + Duel.Turn.ToString() + " player=" + Duel.Player.ToString() +
                " phase=" + Duel.Phase.ToString() + " chain=" +
                Duel.CurrentChain.Count.ToString() + " chainScore=" +
                chainScore.ToString() + "/" +
                UltimateSkyChainNegateThreshold.ToString() + " last=" + lastLabel +
                " lp=" + Bot.LifePoints.ToString() + " ownMonsters=" +
                Bot.GetMonsterCount().ToString() + " enemyMonsters=" +
                Enemy.GetMonsterCount().ToString() + " fieldScores=[" + targets +
                "] alternative=" + alternativeLabel);
        }

        private List<ClientCard> GetUltimateSkyFieldTargets()
        {
            int enemyFaceupMonsters = Enemy.GetMonsters().Count(card => card.IsFaceup());
            if (Enemy.GetMonsterCount() <= Bot.GetMonsterCount())
                return new List<ClientCard>();

            int maximumByLife = Math.Max(0, (Bot.LifePoints - 1) / 800);
            int maximum = Math.Min(enemyFaceupMonsters, maximumByLife);
            if (maximum == 0)
                return new List<ClientCard>();

            List<ClientCard> candidates = Enemy.GetMonsters().Concat(Enemy.GetSpells())
                .Where(card => card.IsFaceup() && !card.IsDisabled() &&
                    !IsCurrentChainHandled(card) && GetUltimateSkyNegateScore(card) >= 2600)
                .OrderByDescending(GetUltimateSkyNegateScore)
                .ToList();
            if (candidates.Count == 0)
                return candidates;

            bool emergency = GetRemainingEnemyBattleAttack() >= Bot.LifePoints ||
                GetUltimateSkyNegateScore(candidates[0]) >= 6500;
            if (!emergency)
            {
                int safeMaximum = Math.Max(0, (Bot.LifePoints - 1601) / 800);
                maximum = Math.Min(maximum, safeMaximum);
            }
            if (maximum == 0)
                return new List<ClientCard>();
            return candidates.Take(maximum).ToList();
        }

        private int GetUltimateSkyNegateScore(ClientCard target)
        {
            if (target == null || target.Controller != 1 || !target.IsFaceup() ||
                target.IsDisabled())
                return 0;
            if (target.IsMonster())
                return GetObservedThreatScore(target);
            if (!IsPersistentSpellTrap(target))
                return 0;
            int score = target.IsFloodgate() ? 7000 : 3000;
            if (SkyStrikerMirrorObserved && target.IsCode(CardId.Multirole, CardId.AreaZero))
                score += target.IsCode(CardId.Multirole) ? 4200 : 2200;
            return score;
        }

        private IList<ClientCard> SelectUltimateSkyTargets(
            IList<ClientCard> cards, int min, int max)
        {
            List<ClientCard> ordered = new List<ClientCard>();
            foreach (ClientCard target in UltimateSkyTargets)
            {
                ClientCard offered = FindMatchingOfferedCard(cards, target);
                if (offered != null && !ordered.Contains(offered))
                    ordered.Add(offered);
            }
            int plannedOfferedCount = ordered.Count;
            ordered.AddRange(cards.Where(card => card.Controller == 1 && !ordered.Contains(card))
                .OrderByDescending(GetUltimateSkyNegateScore));

            if (min == 0 && plannedOfferedCount == 0)
            {
                UltimateSkyTargets.Clear();
                return new List<ClientCard>();
            }

            int desired = max == 1 ? 1 : Math.Min(max,
                Math.Max(min, plannedOfferedCount));
            IList<ClientCard> selected = Util.CheckSelectCount(ordered, cards,
                Math.Max(min, Math.Min(desired, ordered.Count)),
                Math.Max(min, Math.Min(desired, ordered.Count)));
            if (selected != null)
            {
                foreach (ClientCard selectedCard in selected)
                {
                    ClientCard planned = UltimateSkyTargets.FirstOrDefault(target =>
                        FindMatchingOfferedCard(new[] { selectedCard }, target) != null);
                    if (planned != null)
                        UltimateSkyTargets.Remove(planned);
                }
            }
            return selected;
        }

        private bool SetUltimateSky()
        {
            return Util.IsTurn1OrMain2() && Bot.GetSpellCountWithoutField() < 5;
        }

        private bool TripleTacticsTalentEffect()
        {
            TripleTacticsTalentTarget = GetTripleTacticsTalentControlTarget();
            return !DefaultCheckWhetherCardIsNegated(Card) &&
                (ShouldUseTripleTacticsTalentControl(TripleTacticsTalentTarget) ||
                DefaultCheckWhetherBotCanDraw() && Bot.Deck.Count >= 2 ||
                Enemy.Hand.Count > 0);
        }

        private int SelectTripleTacticsTalentOption(IList<int> options,
            int draw, int control, int shuffle)
        {
            TripleTacticsTalentTarget = GetTripleTacticsTalentControlTarget();
            if (control >= 0 && ShouldUseTripleTacticsTalentControl(
                TripleTacticsTalentTarget))
                return control;
            if (draw >= 0 && DefaultCheckWhetherBotCanDraw() && Bot.Deck.Count >= 2)
                return draw;
            if (shuffle >= 0 && Enemy.Hand.Count > 0)
                return shuffle;
            if (control >= 0 && TripleTacticsTalentTarget != null)
                return control;
            return draw >= 0 ? draw : shuffle >= 0 ? shuffle : control;
        }

        private ClientCard GetTripleTacticsTalentControlTarget()
        {
            return Enemy.GetMonsters()
                .OrderByDescending(GetTripleTacticsTalentControlScore)
                .FirstOrDefault();
        }

        private int GetTripleTacticsTalentControlScore(ClientCard target)
        {
            if (target == null || target.Controller != 1)
                return 0;
            int score = target.IsFaceup() ? GetObservedThreatScore(target) : 1200;
            if (Duel.Phase == DuelPhase.Main1 && target.IsFaceup())
                score += Math.Max(0, target.Attack);
            if (IsSkyStrikerAceLink(target))
                score += 2200;
            return score;
        }

        private bool ShouldUseTripleTacticsTalentControl(ClientCard target)
        {
            if (target == null)
                return false;
            int score = GetTripleTacticsTalentControlScore(target);
            int attackOnBoard = Bot.GetMonsters().Where(card => card.IsFaceup())
                .Sum(card => Math.Max(0, card.Attack));
            bool createsLethal = Duel.Phase == DuelPhase.Main1 && target.IsFaceup() &&
                attackOnBoard + Math.Max(0, target.Attack) >= Enemy.LifePoints;
            return createsLethal || score >= 6000;
        }

        private IList<ClientCard> SelectTripleTacticsTalentControlTarget(
            IList<ClientCard> cards, int min, int max)
        {
            List<ClientCard> ordered = new List<ClientCard>();
            ClientCard intended = FindMatchingOfferedCard(cards, TripleTacticsTalentTarget);
            if (intended != null)
                ordered.Add(intended);
            ordered.AddRange(cards.Where(card => !ordered.Contains(card))
                .OrderByDescending(GetTripleTacticsTalentControlScore));
            return Util.CheckSelectCount(ordered, cards, min, max);
        }

        private IList<ClientCard> SelectTripleTacticsTalentHandTarget(
            IList<ClientCard> cards, int min, int max)
        {
            List<ClientCard> ordered = cards.OrderByDescending(card =>
                card.IsCode(CardId.MaxxC, CardId.MulcharmyFuwalos,
                    CardId.AshBlossom, CardId.DrollLockBird) ? 10000 :
                card.IsFloodgate() || card.IsMonsterDangerous() ? 8000 :
                card.IsMonster() ? Math.Max(1000, card.Attack) : 2000)
                .ToList();
            return Util.CheckSelectCount(ordered, cards, min, max);
        }

        private bool MysticalSpaceTyphoonEffect()
        {
            if (Duel.CurrentChain.Any(card => card.Controller == 0 &&
                card.IsCode(CardId.MysticalSpaceTyphoon)))
                return false;
            ClientCard azaminaResource = Enemy.GetSpells()
                .Where(IsAzaminaFusionResourceDisruption)
                .OrderByDescending(GetSpellTrapDestructionScore)
                .FirstOrDefault();
            if (azaminaResource != null)
            {
                MarkCurrentChainHandled(azaminaResource);
                AI.SelectCard(azaminaResource);
                return true;
            }
            if (SkyStrikerMirrorObserved)
            {
                ClientCard target = GetMirrorPriorityEnemySpell();
                if (target != null)
                {
                    MarkCurrentChainHandled(target);
                    AI.SelectCard(target);
                    return true;
                }
            }
            return DefaultMysticalSpaceTyphoon();
        }

        private bool ForbiddenDropletEffect()
        {
            ForbiddenDropletTargets.Clear();
            TacticalDecision decision = GetBestTacticalDecision();
            if (decision.Kind != TacticalActionKind.ForbiddenDroplet)
                return false;
            ForbiddenDropletTargets.AddRange(decision.Targets);
            foreach (ClientCard target in ForbiddenDropletTargets)
                MarkCurrentChainHandled(target);
            LogTacticalDecision(decision);
            return ForbiddenDropletTargets.Count > 0;
        }

        private bool ForbiddenCrownEffect()
        {
            TacticalDecision decision = GetBestTacticalDecision();
            if (decision.Kind != TacticalActionKind.ForbiddenCrown ||
                decision.Targets.Count == 0)
                return false;
            ClientCard target = decision.Targets[0];
            ForbiddenCrownTarget = target;
            MarkCurrentChainHandled(target);
            LogTacticalDecision(decision);
            return true;
        }

        private bool IsUsableForbiddenCrownTarget(ClientCard target)
        {
            return target != null && target.Controller == 1 &&
                target.Location == CardLocation.MonsterZone && target.IsMonster() &&
                target.IsFaceup() && !target.IsDisabled() && !IsCurrentChainHandled(target);
        }

        private bool RadiantTyphoonVisionEffect()
        {
            if (Card.Location == CardLocation.Grave)
                return true;
            return Duel.Player == 0 || Duel.Phase == DuelPhase.End || Bot.Hand.Count <= 3;
        }

        private bool FallenAndVirtuousEffect()
        {
            FallenAndVirtuousDestroyTarget = GetFallenAndVirtuousDestroyTarget();
            FallenAndVirtuousReviveTarget = GetBestFallenAndVirtuousReviveTarget();
            bool canPayDestroyCost = Bot.HasInExtra(CardId.AlbionTheBrandedDragon) ||
                Bot.HasInExtra(CardId.DragonThatDevoursTheDogma) ||
                Bot.HasInExtra(CardId.EcclesiaAndTheDarkDragon);
            int destroyScore = canPayDestroyCost
                ? GetFallenAndVirtuousDestroyScore(FallenAndVirtuousDestroyTarget) : 0;
            bool hasEcclesia = Bot.GetMonsters().Concat(Bot.Graveyard)
                .Any(card => card.IsCode(CardId.EcclesiaAndTheDarkDragon));
            int reviveScore = hasEcclesia
                ? GetFallenAndVirtuousReviveScore(FallenAndVirtuousReviveTarget) : 0;
            FallenAndVirtuousPreferDestroy = destroyScore >= reviveScore;
            if (FallenAndVirtuousPreferDestroy && destroyScore >= 3500)
                MarkCurrentChainHandled(FallenAndVirtuousDestroyTarget);
            bool activate = Math.Max(destroyScore, reviveScore) >= 3500;
            Logger.DebugWriteLine("[SkyStriker2026] TFTV destroy=" + destroyScore +
                " revive=" + reviveScore + " activate=" + activate);
            return activate;
        }

        private bool AlbionTheBrandedDragonEffect()
        {
            return Card.Location == CardLocation.Grave && Duel.Phase == DuelPhase.End &&
                Bot.HasInDeck(CardId.TheFallenAndTheVirtuous);
        }

        private bool ShouldSetFallenAndVirtuousFromAlbion()
        {
            if (Duel.Phase != DuelPhase.End || Bot.GetSpellCountWithoutField() >= 5 ||
                Bot.HasInSpellZone(CardId.TheFallenAndTheVirtuous))
                return false;
            if (!HasFallenAndVirtuousFollowUp())
                return false;

            // At our End Phase, setting gives live interaction for the opponent's
            // turn. At the opponent's End Phase, hand is normally more flexible;
            // set only to avoid the hand-size discard or a known hand lock.
            if (Duel.Player == 0)
                return true;
            return Bot.Hand.Count >= 6;
        }

        private bool SetFallenAndVirtuous()
        {
            return Util.IsTurn1OrMain2() && Bot.GetSpellCountWithoutField() < 5 &&
                !Bot.HasInSpellZone(CardId.TheFallenAndTheVirtuous) &&
                HasFallenAndVirtuousFollowUp();
        }

        private bool HasFallenAndVirtuousFollowUp()
        {
            return Bot.HasInExtra(CardId.AlbionTheBrandedDragon) ||
                Bot.HasInExtra(CardId.DragonThatDevoursTheDogma) ||
                Bot.HasInExtra(CardId.EcclesiaAndTheDarkDragon) ||
                Bot.GetMonsters().Concat(Bot.Graveyard)
                    .Any(card => card.IsCode(CardId.EcclesiaAndTheDarkDragon));
        }

        private bool DragonThatDevoursTheDogmaEffect()
        {
            return true;
        }

        private bool EcclesiaAndTheDarkDragonEffect()
        {
            if (Card.Location != CardLocation.Grave ||
                (Duel.Phase != DuelPhase.Main1 && Duel.Phase != DuelPhase.Main2))
                return false;
            bool hasFusion = Bot.Graveyard.Concat(Bot.Banished)
                .Any(card => card.IsCode(CardId.AlbionTheBrandedDragon,
                    CardId.DragonThatDevoursTheDogma));
            return hasFusion && Enemy.GetFieldCount() > 0;
        }

        private bool RozeEffect()
        {
            if (DefaultCheckWhetherCardIsNegated(Card))
                return false;
            if (Card.Location == CardLocation.Hand)
            {
                if (PreserveEmptyMainZoneForZero && Bot.HasInMonstersZone(CardId.Zero))
                    return false;
                return Bot.MonsterZone.Any(card => card != null && IsSkyStrikerAce(card));
            }
            return Card.Location == CardLocation.Grave;
        }

        private bool AmatsuEffect()
        {
            if (DefaultCheckWhetherCardIsNegated(Card))
                return false;
            if (ActivateDescription == Util.GetStringId(CardId.Amatsu, 0))
            {
                ClientCard last = Util.GetLastChainCard();
                bool shouldRedirect = Duel.LastChainPlayer == 1 && last != null && last.IsMonster() &&
                    Bot.GetMonsters().Any(card => IsSkyStrikerAceLink(card)) &&
                    (Bot.HasInGraveyard(CardId.Raye) || last == Util.GetProblematicEnemyMonster() ||
                    last.IsFloodgate());
                if (shouldRedirect)
                    AmatsuRedirectPending = true;
                return shouldRedirect;
            }
            return ActivateDescription == Util.GetStringId(CardId.Amatsu, 1);
        }

        private bool ZeroEffect()
        {
            bool searchEffect = ActivateDescription == Util.GetStringId(CardId.Zero, 0) ||
                ActivateDescription == -1 && ZeroSearchPending;
            if (searchEffect)
            {
                StrategicSearchTargetPending = GetStrategicSearchTarget(
                    SkyStrategicActionKind.ResolveZeroSearch);
                ZeroSearchPending = false;
                bool activateSearch = LinkageOtk != LinkageOtkStage.ZeroReady &&
                    !DefaultCheckWhetherCardIsNegated(Card);
                Logger.DebugWriteLine("[SkyStriker2026] Zero summon search desc=" +
                    ActivateDescription.ToString() + " activate=" + activateSearch.ToString());
                return activateSearch;
            }
            if (DefaultCheckWhetherCardIsNegated(Card))
                return false;
            if (ActivateDescription != Util.GetStringId(CardId.Zero, 1))
                return false;
            bool canResolveExit = CanResolveZeroQuickEffect();
            if (!canResolveExit)
                return false;
            if (Duel.Player == 0 && Duel.Phase > DuelPhase.Main1 &&
                Duel.Phase < DuelPhase.Main2 && Card.Attacked)
            {
                if (LinkageOtk == LinkageOtkStage.ZeroReady)
                    LinkageOtk = LinkageOtkStage.RayeRozeReady;
                return true;
            }

            bool escapingRemoval = Duel.LastChainPlayer == 1 &&
                DefaultOnBecomeTarget();
            bool preemptiveRemoval = IsPreemptiveZeroRemovalThreat();
            bool preventingLethal = Duel.Player == 1 &&
                GetRemainingEnemyBattleAttack() >= Bot.LifePoints;
            ClientCard destroyTarget = GetBestZeroDestroyTarget();
            int destroyScore = GetZeroDestroyScore(destroyTarget);
            bool canClearNextTurn = CanPlanZeroExitClearance();
            bool losingZeroInBattle = Duel.Player == 1 && Bot.UnderAttack &&
                Bot.BattlingMonster != null && Bot.BattlingMonster.IsCode(CardId.Zero) &&
                Enemy.BattlingMonster != null &&
                Enemy.BattlingMonster.GetDefensePower() >=
                    Bot.BattlingMonster.GetDefensePower();
            int minimumDestroyScore = HasSetSkyStrikerInteraction() ? 4800 : 3600;
            if (canClearNextTurn)
                minimumDestroyScore -= 600;
            bool activate = ShouldActivateZeroQuickEffect(canResolveExit,
                escapingRemoval, preemptiveRemoval, preventingLethal,
                losingZeroInBattle, destroyScore, minimumDestroyScore);
            if (activate)
            {
                ZeroDestroyTarget = destroyTarget;
                if (canClearNextTurn)
                    ZeroExitClearancePending = true;
            }
            Logger.DebugWriteLine("[SkyStriker2026] Zero quick escape=" +
                escapingRemoval.ToString() + " preemptive=" +
                preemptiveRemoval.ToString() + " lethal=" +
                preventingLethal.ToString() + " battle=" +
                losingZeroInBattle.ToString() + " clear-next=" +
                canClearNextTurn.ToString() + " turn-player=" +
                Duel.Player.ToString() +
                " destroy=" + (destroyTarget == null ? "none" :
                    destroyTarget.Name ?? destroyTarget.Id.ToString()) +
                " score=" + destroyScore.ToString() + " threshold=" +
                minimumDestroyScore.ToString() + " activate=" + activate.ToString());
            return activate;
        }

        private bool CanResolveZeroQuickEffect()
        {
            int remainingMainMonsters = Bot.GetMonstersInMainZone()
                .Count(card => card != Card);
            return remainingMainMonsters <= 3 &&
                (Bot.HasInDeck(CardId.Raye) || Bot.HasInGraveyard(CardId.Raye)) &&
                (Bot.HasInDeck(CardId.Roze) || Bot.HasInGraveyard(CardId.Roze));
        }

        private bool IsPreemptiveZeroRemovalThreat()
        {
            ChainInfo last = Duel.CurrentChainInfo.LastOrDefault();
            return last != null && last.ActivatePlayer == 1 &&
                last.IsActivateCode(CardId.Zero) &&
                last.ActivateDescription == Util.GetStringId(CardId.Zero, 1);
        }

        private static bool ShouldActivateZeroQuickEffect(bool canResolveExit,
            bool escapingRemoval, bool preemptiveRemoval, bool preventingLethal,
            bool losingZeroInBattle, int destroyScore, int minimumDestroyScore)
        {
            return canResolveExit && (escapingRemoval || preemptiveRemoval ||
                preventingLethal || losingZeroInBattle ||
                destroyScore >= minimumDestroyScore);
        }

        private bool CanPlanZeroExitClearance()
        {
            return EmptyMainMonsterZone() && Bot.HasInExtra(CardId.Zero) &&
                (Bot.HasInDeck(CardId.Raye) || Bot.HasInGraveyard(CardId.Raye)) &&
                (Bot.HasInDeck(CardId.Roze) || Bot.HasInGraveyard(CardId.Roze));
        }

        private ClientCard GetBestZeroDestroyTarget()
        {
            return Enemy.GetMonsters().Concat(Enemy.GetSpells())
                .Where(card => card != null && card.IsFaceup())
                .OrderByDescending(GetZeroDestroyScore)
                .FirstOrDefault(card => GetZeroDestroyScore(card) > 0);
        }

        private int GetZeroDestroyScore(ClientCard target)
        {
            if (target == null || target.Controller != 1 || !target.IsFaceup())
                return 0;
            if (target.IsMonster())
            {
                int monsterScore = GetObservedThreatScore(target);
                if (SkyStrikerMirrorObserved && target.IsCode(CardId.Zero))
                    monsterScore += 4800;
                if (SkyStrikerMirrorObserved && IsSkyStrikerAceLink(target) &&
                    Enemy.GetGraveyardMonsters().Any(card => card.IsCode(CardId.Raye)))
                    monsterScore -= 1600;
                if (Bot.UnderAttack && Enemy.BattlingMonster == target &&
                    Bot.BattlingMonster != null &&
                    Bot.BattlingMonster.IsCode(CardId.Zero))
                    monsterScore += 5000;
                if (Duel.CurrentChain.Contains(target))
                    monsterScore -= 1800;
                return Math.Max(0, monsterScore);
            }

            return GetSpellTrapDestructionScore(target);
        }

        private int GetSpellTrapDestructionScore(ClientCard target)
        {
            if (target == null || target.Controller != 1 ||
                (!target.IsSpell() && !target.HasType(CardType.Trap)))
                return 0;
            if (IsAzaminaFusionResourceDisruption(target))
                return 9000;
            if (target.IsFacedown())
                return 1800;
            if (!IsPersistentSpellTrap(target))
                return 0;
            int score = target.IsFloodgate() ? 6500 : 3500;
            if (SkyStrikerMirrorObserved && target.IsCode(CardId.Multirole, CardId.AreaZero))
                score += target.IsCode(CardId.Multirole) ? 5000 : 2800;
            return score;
        }

        private bool IsPersistentSpellTrap(ClientCard card)
        {
            return card != null && card.HasType(CardType.Continuous | CardType.Equip |
                CardType.Field | CardType.Pendulum);
        }

        private bool IsAzaminaFusionResourceDisruption(ClientCard target)
        {
            if (target == null || target.Controller != 1 || !target.IsFaceup() ||
                !target.HasSetcode(0x19e))
                return false;
            ClientCard last = Util.GetLastChainCard();
            return Duel.LastChainPlayer == 1 && last != null &&
                Duel.CurrentChain.Contains(last) &&
                last.IsCode(CardId.TheHallowedAzamina, CardId.AzaminaDebtors);
        }

        private bool HasSetSkyStrikerInteraction()
        {
            return Bot.GetSpells().Any(card => card.IsFacedown() &&
                card.IsCode(CardId.WidowAnchor, CardId.SharkCannon, CardId.Linkage));
        }

        private IList<ClientCard> SelectZeroDestroyTarget(
            IList<ClientCard> cards, int min, int max)
        {
            List<ClientCard> ordered = new List<ClientCard>();
            ClientCard intended = FindMatchingOfferedCard(cards, ZeroDestroyTarget);
            if (intended != null)
                ordered.Add(intended);
            ordered.AddRange(cards.Where(card => card.Controller == 1 &&
                !ordered.Contains(card)).OrderByDescending(GetZeroDestroyScore));
            ordered.AddRange(cards.Where(card => !ordered.Contains(card)));
            ZeroDestroyTarget = null;
            return Util.CheckSelectCount(ordered, cards, min, max);
        }

        private bool LinkageEffect()
        {
            if (!EmptyMainMonsterZone() ||
                !Bot.GetMonsters().Concat(Bot.GetSpells()).Any(card => card != Card) ||
                !Bot.ExtraDeck.Any(IsSkyStrikerAceLink))
                return false;

            if (UltimateSkyLinkageSetupPending && Duel.Player == 0 &&
                Duel.CurrentChainInfo.Any(chain => chain.ActivatePlayer == 0 &&
                    chain.IsActivateCode(CardId.UltimateSky)) &&
                Bot.HasInExtra(CardId.Zero))
            {
                LinkageOtkSendTarget = Bot.GetSpells().FirstOrDefault(card =>
                    card.IsCode(CardId.UltimateSky));
                LinkageSetupToZeroPending = true;
                UltimateSkyLinkageSetupPending = false;
                Logger.DebugWriteLine("[SkyStriker2026] Linkage: chain to Ultimate Sky, " +
                    "send it and restore Zero from the empty board.");
                return true;
            }

            // Outside the tracked Battle Phase replacement, keeping an existing
            // Ace while Linkage summons another one would put the new Ace in the
            // Main Monster Zone and turn off the rest of the Striker Spell plan.
            if ((Duel.Phase == DuelPhase.Main1 || Duel.Phase == DuelPhase.Main2) &&
                Bot.GetMonstersExtraZoneCount() > 0)
                return false;

            if (Duel.Player == 1)
                return DefaultOnBecomeTarget() || Duel.CurrentChain.Count > 0;

            if (Duel.Phase > DuelPhase.Main1 && Duel.Phase < DuelPhase.Main2)
            {
                if (LinkageOtk == LinkageOtkStage.KagariReady)
                {
                    ClientCard kagari = Bot.GetMonsters().FirstOrDefault(card =>
                        card.IsCode(CardId.Kagari) && card.Attacked);
                    if (kagari != null && Bot.HasInExtra(CardId.Zero))
                    {
                        LinkageOtkSendTarget = kagari;
                        LinkageOtk = LinkageOtkStage.SummonZero;
                        return true;
                    }
                }

                if (LinkageOtk == LinkageOtkStage.None)
                {
                    ClientCard attacker = GetLinkageOtkStarter();
                    if (attacker != null)
                    {
                        LinkageOtkSendTarget = attacker;
                        LinkageOtk = LinkageOtkStage.SummonKagari;
                        return true;
                    }
                }

                return Bot.GetMonsters().Any(card => IsSkyStrikerAceLink(card) && card.Attacked) ||
                    Bot.GetMonsterCount() == 0;
            }

            if (Duel.Phase == DuelPhase.Main1 || Duel.Phase == DuelPhase.Main2)
            {
                if (Bot.GetMonsterCount() == 0)
                {
                    if (!StrategicAllows(SkyStrategicActionKind.ActivateLinkage))
                        return false;
                    if (HasBetterZeroStarterThanLinkage())
                        return false;
                    if (Bot.HasInExtra(CardId.Zero))
                    {
                        ClientCard plannedFuel = Bot.GetSpells().FirstOrDefault(card =>
                            PlannedLinkageFuelId != 0 &&
                            card.IsCode(PlannedLinkageFuelId));
                        if (plannedFuel != null)
                            LinkageOtkSendTarget = plannedFuel;
                        LinkageSetupToZeroPending = true;
                        return true;
                    }
                }
                return Enemy.GetFieldCount() > 0 &&
                    Bot.GetMonsters().Any(card => IsDisposableMonster(card));
            }

            return false;
        }

        private bool SetPlannedLinkageFuel()
        {
            int plannedFuel = GetStrategicSearchTarget(
                SkyStrategicActionKind.SetLinkageFuel);
            if (plannedFuel == 0 || Card == null || !Card.IsCode(plannedFuel) ||
                !Bot.HasInHand(CardId.Linkage) || !EmptyMainMonsterZone() ||
                Bot.GetSpellCountWithoutField() >= 5)
                return false;

            Logger.DebugWriteLine("[SkyStriker2026:RDA] set " + Card.Name +
                " as committed Linkage fuel.");
            PlannedLinkageFuelId = Card.Id;
            return true;
        }

        private int GetBestLinkageFuelFromHand()
        {
            ClientCard fuel = GetBestLinkageFuelCardFromHand();
            return fuel == null ? 0 : fuel.Id;
        }

        private int GetBestLinkageFuelCostFromHand()
        {
            ClientCard fuel = GetBestLinkageFuelCardFromHand();
            return fuel == null ? 0 : GetLinkageFuelCost(fuel);
        }

        private ClientCard GetBestLinkageFuelCardFromHand()
        {
            return Bot.Hand.Where(IsLinkageFuelCandidate)
                .OrderBy(GetLinkageFuelCost)
                .FirstOrDefault();
        }

        private bool IsLinkageFuelCandidate(ClientCard card)
        {
            if (card == null || card.IsCode(CardId.Linkage, CardId.Engage,
                CardId.Multirole, CardId.AreaZero, CardId.ReinforcementOfTheArmy,
                CardId.PotOfDesires))
                return false;
            return card.IsCode(CardId.Lemnisgate, CardId.SharkCannon,
                CardId.WidowAnchor, CardId.HornetDrones, CardId.UltimateSky,
                CardId.TheFallenAndTheVirtuous, CardId.ForbiddenDroplet,
                CardId.ForbiddenCrown, CardId.TwinTwisters,
                CardId.MysticalSpaceTyphoon, CardId.MetalfoesFusion,
                CardId.HerculesBase);
        }

        private int GetLinkageFuelCost(ClientCard card)
        {
            if (card.IsCode(CardId.Lemnisgate))
                return HasProfitableLemnisgatePlan() ? 5200 : 700;
            if (card.IsCode(CardId.MetalfoesFusion))
                return 500;
            if (card.IsCode(CardId.MysticalSpaceTyphoon, CardId.TwinTwisters))
                return Enemy.GetSpells().Any(target => target.IsFacedown() ||
                    IsPersistentSpellTrap(target)) ? 3700 : 900;
            if (card.IsCode(CardId.SharkCannon))
                return Enemy.GetGraveyardMonsters().Any(target =>
                    target.IsCode(CardId.Raye, CardId.Roze) && target.IsCanRevive())
                    ? 5100 : 1900;
            if (card.IsCode(CardId.WidowAnchor))
                return Bot.Hand.Count(target => target.IsCode(CardId.WidowAnchor)) > 1
                    ? 2600 : 4700;
            if (card.IsCode(CardId.HerculesBase))
                return 1600;
            if (card.IsCode(CardId.ForbiddenCrown))
                return 2500;
            if (card.IsCode(CardId.ForbiddenDroplet))
                return 3400;
            if (card.IsCode(CardId.HornetDrones))
                return 4300;
            if (card.IsCode(CardId.UltimateSky))
                return 4400;
            if (card.IsCode(CardId.TheFallenAndTheVirtuous))
                return 3600;
            return 3000;
        }

        private bool LemnisgateEffect()
        {
            if (Card.Location == CardLocation.Grave)
                return ShouldUseLemnisgateGraveLink();

            // Do not chain the field recycle to our own pending effect. Besides
            // spending Lemnisgate without a tempo target, recycling Kagari's
            // selected Spell here can make the recovery resolve without value.
            if (Duel.CurrentChainInfo.Any(chain => chain.ActivatePlayer == 0))
                return false;

            int pairCount;
            int usefulBounces;
            int bounceValue;
            int recycleCost;
            if (!TryBuildLemnisgatePlan(out pairCount, out usefulBounces,
                out bounceValue, out recycleCost))
                return false;
            LemnisgateRecyclePairCount = pairCount;

            Logger.DebugWriteLine("[SkyStriker2026] Lemnisgate: recycle pairs=" +
                LemnisgateRecyclePairCount + ", useful bounces=" + usefulBounces +
                ", bounce value=" + bounceValue + ", resource cost=" + recycleCost +
                ", net=" + (bounceValue - recycleCost));
            LemnisgateRecycleSelectionCount = 0;
            return true;
        }

        private bool HasProfitableLemnisgatePlan()
        {
            int pairCount;
            int usefulBounces;
            int bounceValue;
            int recycleCost;
            return TryBuildLemnisgatePlan(out pairCount, out usefulBounces,
                out bounceValue, out recycleCost);
        }

        private bool TryBuildLemnisgatePlan(out int pairCount,
            out int usefulBounces, out int bounceValue, out int recycleCost)
        {
            pairCount = 0;
            usefulBounces = 0;
            bounceValue = 0;
            recycleCost = 0;

            // Never burn a recycle pair merely to place Lemnisgate in the GY.
            // The grave-Link setup must send it through Amatsu or another real
            // route; the field effect needs an actual public tempo return.
            if (!HasLemnisgateTempoWindow(Enemy.GetFieldCount()))
                return false;

            List<ClientCard> spells = Bot.Graveyard.Where(card =>
                card.IsSpell() && IsSkyStrikerCard(card)).ToList();
            List<ClientCard> monsters = Bot.Graveyard.Where(card =>
                card.IsMonster() && IsSkyStrikerAce(card)).ToList();
            int maximumPairs = Math.Min(spells.Count, monsters.Count);
            if (maximumPairs == 0)
                return false;

            List<ClientCard> bounceTargets = GetUsefulLemnisgateBounceTargets();
            usefulBounces = bounceTargets.Count;
            pairCount = GetLemnisgateRecyclePairCount(usefulBounces,
                maximumPairs);
            if (pairCount == 0)
                return false;

            int projectedSpellCount = GetSpellCountInGrave() - pairCount + 1;
            if (projectedSpellCount < 3 && usefulBounces == 0)
                return false;

            int bounceCount = Math.Min(bounceTargets.Count,
                GetLemnisgateReturnCount(pairCount));
            bounceValue = bounceTargets.Take(bounceCount)
                .Sum(GetLemnisgateBounceValue);
            recycleCost = spells.OrderByDescending(GetLemnisgateRecycleValue)
                .Take(pairCount).Sum(GetLemnisgateRecycleOpportunityCost) +
                monsters.OrderByDescending(GetLemnisgateRecycleValue)
                    .Take(pairCount).Sum(GetLemnisgateRecycleOpportunityCost);
            return ShouldActivateLemnisgateForNetValue(bounceValue, recycleCost);
        }

        private static bool HasLemnisgateTempoWindow(int enemyFieldCount)
        {
            return enemyFieldCount > 0;
        }

        private static bool ShouldActivateLemnisgateForNetValue(int bounceValue,
            int recycleCost)
        {
            return bounceValue >= 2500 && bounceValue - recycleCost >= 1200;
        }

        private int GetLemnisgateRecycleOpportunityCost(ClientCard card)
        {
            if (card == null)
                return 0;
            if (card.IsMonster())
            {
                if (card.IsCode(CardId.Zero))
                    return -1800;
                if (card.IsCode(CardId.Kagari))
                    return -1500;
                if (IsSkyStrikerAceLink(card))
                    return -900;
                if (card.IsCode(CardId.Raye))
                    return Bot.Graveyard.Count(target => target.IsCode(CardId.Raye)) > 1
                        ? 1000 : 4200;
                if (card.IsCode(CardId.Roze))
                    return Bot.Graveyard.Count(target => target.IsCode(CardId.Roze)) > 1
                        ? 800 : 2800;
                return 900;
            }

            bool kagariCanRecover = !KagariSummoned && Bot.HasInExtra(CardId.Kagari);
            if (card.IsCode(CardId.Engage))
                return kagariCanRecover ? 3800 : 700;
            if (card.IsCode(CardId.Linkage))
                return kagariCanRecover ? 3400 : 900;
            if (card.IsCode(CardId.SharkCannon))
                return 1500;
            if (card.IsCode(CardId.WidowAnchor))
                return 1200;
            if (card.IsCode(CardId.Multirole))
                return 1800;
            return 700;
        }

        private static int GetLemnisgateRecyclePairCount(int usefulBounces,
            int maximumPairs)
        {
            if (maximumPairs <= 0)
                return 0;
            if (usefulBounces >= 2 && maximumPairs >= 3)
                return 3;
            if (usefulBounces >= 1 && maximumPairs >= 2)
                return 2;
            return 0;
        }

        private static int GetLemnisgateReturnCount(int recyclePairs)
        {
            return Math.Max(0, recyclePairs * 2 / 3);
        }

        private static bool ShouldSelectLemnisgateOptionalEffect(int option,
            bool hasUsefulBounce, bool hasUsefulGraveLink)
        {
            // The supplied script uses StringId 1 for the post-recycle return.
            // Keep StringId 2 for the optional delayed GY Link prompt used by
            // compatible clients.
            return option == 1 ? hasUsefulBounce :
                option == 2 && hasUsefulGraveLink;
        }

        private bool ShouldUseLemnisgateGraveLink()
        {
            List<ClientCard> materials = Bot.GetMonsters().Where(card =>
                card.IsFaceup()).ToList();
            if (materials.Count == 0 || GetLemnisgateExtraDeckPriority().Length == 0)
                return false;

            if (HasPlannedSkyStrikerSpell() && materials.Count > 1 &&
                HornetSetupStage != HornetZeroSetupStage.NeedZero)
                return false;

            int planValue = GetLemnisgateGraveLinkPlanValue(materials);
            int materialCount = HornetSetupStage == HornetZeroSetupStage.NeedZero
                ? 2 : 1;
            if (materials.Count < materialCount)
                return false;
            int materialCost = materials.OrderBy(GetLinkMaterialValue)
                .Take(materialCount).Sum(GetLinkMaterialValue) + 700;
            bool use = ShouldUseLemnisgateGraveLinkForValue(planValue, materialCost);
            Logger.DebugWriteLine("[SkyStriker2026] Lemnisgate GY Link: value=" +
                planValue + ", material cost=" + materialCost + ", use=" + use);
            return use;
        }

        private int GetLemnisgateGraveLinkPlanValue(List<ClientCard> materials)
        {
            bool battle = Duel.Phase > DuelPhase.Main1 && Duel.Phase < DuelPhase.Main2;
            if (battle && Duel.Player == 0)
                return materials.Any(card => card.Attacked &&
                    (IsSkyStrikerAce(card) || ControlledEnemyCards.Contains(card)))
                    ? 5600 : 0;

            ClientCard liveThreat = Enemy.GetMonsters().Where(card => card.IsFaceup() &&
                card.IsAttack() && !card.Attacked)
                .OrderByDescending(GetObservedThreatScore).FirstOrDefault();
            if (Duel.Player == 1)
            {
                if (liveThreat == null ||
                    !Bot.HasInExtra(new[] { CardId.Kaina, CardId.Amatsu }))
                    return 0;
                return Math.Max(0, GetObservedThreatScore(liveThreat)) +
                    Math.Min(3000, Math.Max(0, liveThreat.Attack));
            }

            if (HornetSetupStage == HornetZeroSetupStage.NeedZero &&
                materials.Count >= 2 && Bot.HasInExtra(CardId.Zero))
                return 7600;

            if (Duel.Phase == DuelPhase.Main1 && !KagariSummoned &&
                Bot.HasInExtra(CardId.Kagari))
            {
                if (Bot.HasInGraveyard(CardId.Engage))
                    return 5600;
                if (Bot.HasInGraveyard(CardId.Linkage))
                    return 4700;
                if (Bot.HasInGraveyard(CardId.WidowAnchor))
                    return 4100;
                if (Bot.HasInGraveyard(CardId.HornetDrones))
                    return 3500;
            }

            if ((Duel.Phase == DuelPhase.Main2 || Duel.Phase == DuelPhase.End) &&
                !ShizukuSummoned && Bot.HasInExtra(CardId.Shizuku) &&
                materials.Any(card => card.Attacked ||
                    ControlledEnemyCards.Contains(card) || card.IsCode(CardId.Token)))
                return 4700;
            return 0;
        }

        private static bool ShouldUseLemnisgateGraveLinkForValue(int planValue,
            int materialCost)
        {
            return planValue >= 3500 && planValue - materialCost >= 1600;
        }

        private List<ClientCard> GetUsefulLemnisgateBounceTargets()
        {
            return Enemy.GetMonsters().Concat(Enemy.GetSpells())
                .Where(card => GetLemnisgateBounceValue(card) >= 2500)
                .OrderByDescending(GetLemnisgateBounceValue)
                .ToList();
        }

        private int GetLemnisgateBounceValue(ClientCard card)
        {
            if (card == null)
                return 0;
            if (card.Controller == 0)
            {
                if (ControlledEnemyCards.Contains(card))
                    return 7500;
                if (IsSkyStrikerAceLink(card) && card.Attacked)
                    return 2600 + card.LinkCount * 300;
                return 0;
            }

            if (card.IsMonster())
            {
                int score = card.IsFaceup() ? GetObservedThreatScore(card) : 1200;
                if (card.HasType(CardType.Fusion | CardType.Synchro | CardType.Xyz |
                    CardType.Link))
                    score += 1800;
                score -= GetLemnisgateMirrorReturnPenalty(
                    SkyStrikerMirrorObserved, IsSkyStrikerAceLink(card),
                    card.IsCode(CardId.Zero), card.IsDisabled(),
                    Enemy.GetGraveyardMonsters().Any(target =>
                        target.IsCode(CardId.Raye, CardId.Roze)));
                return score;
            }
            if (IsPersistentSpellTrap(card))
                return card.IsFloodgate() ? 9000 : 4500;

            // Returning a resolving Normal/Quick-Play Spell or Trap does not undo
            // its effect, so it is deliberately worth nothing here.
            return card.IsFacedown() ? 800 : 0;
        }

        private static int GetLemnisgateMirrorReturnPenalty(bool mirrorObserved,
            bool aceLink, bool zero, bool disabled, bool rayeOrRozeReady)
        {
            if (!mirrorObserved || !aceLink || zero)
                return 0;
            return (disabled ? 7000 : 4800) + (rayeOrRozeReady ? 1800 : 0);
        }

        private bool ZeroSummon()
        {
            if (ZeroSummoned)
                return false;

            if (HornetSetupStage == HornetZeroSetupStage.NeedBridgeLink)
                return false;

            if (!StrategicAllows(SkyStrategicActionKind.LinkZero))
                return false;

            List<ClientCard> candidates = Bot.GetMonsters()
                .Where(card => card.IsFaceup() && IsSkyStrikerAce(card) && !card.IsCode(CardId.Zero))
                .OrderBy(GetLinkMaterialValue)
                .ToList();
            List<ClientCard> materials = candidates.Take(2).ToList();

            if (ZeroExitClearancePending)
            {
                ClientCard raye = candidates.FirstOrDefault(card =>
                    card.IsCode(CardId.Raye));
                ClientCard roze = candidates.FirstOrDefault(card =>
                    card.IsCode(CardId.Roze));
                materials = raye != null && roze != null
                    ? new List<ClientCard> { raye, roze }
                    : new List<ClientCard>();
            }

            if (HornetSetupStage == HornetZeroSetupStage.NeedZero)
            {
                ClientCard token = candidates.FirstOrDefault(card => card.IsCode(CardId.Token));
                ClientCard bridge = candidates.FirstOrDefault(card =>
                    card.IsCode(CardId.Amatsu, CardId.Hayate, CardId.Kaina,
                        CardId.Shizuku));
                materials = token != null && bridge != null
                    ? new List<ClientCard> { bridge, token }
                    : new List<ClientCard>();
            }

            if (materials.Count != 2 || Util.GetBotAvailZonesFromExtraDeck(materials) == 0)
                return false;
            if (!SkyStrikerLinkPreservesMainMonsterZone(materials, false))
                return false;
            if (!ZeroExitClearancePending && ShouldDeferZeroForBattleDamage(materials))
                return false;
            AI.SelectMaterials(materials);
            SelectSkyStrikerLinkPlace();
            return true;
        }

        private bool ZekeSummon()
        {
            if (ZekeSummoned || HornetSetupStage != HornetZeroSetupStage.None ||
                StrategicReservesModeledLink() ||
                !Enemy.GetMonsters().Any(card => card.IsFaceup()))
                return false;
            List<ClientCard> materials = GetModernLinkTwoMaterials(card => true, true);
            if (materials == null || !materials.Any(IsSkyStrikerAceLink))
                return false;
            AI.SelectMaterials(materials);
            SelectSkyStrikerLinkPlace();
            return true;
        }

        private bool AzaleaSummon()
        {
            if (AzaleaSummoned || HornetSetupStage != HornetZeroSetupStage.None ||
                StrategicReservesModeledLink())
                return false;
            List<ClientCard> materials = GetModernLinkTwoMaterials(card =>
                card.HasAttribute(CardAttribute.Light | CardAttribute.Dark), false);
            if (materials == null)
                return false;

            AzaleaDestroyTarget = GetBestAzaleaDestroyTarget();
            bool linkageBridge = CanUseAzaleaAsLinkageBridge(materials);
            if (AzaleaDestroyTarget == null && !linkageBridge)
                return false;
            if (AzaleaDestroyTarget != null)
            {
                int targetScore = GetAzaleaDestroyScore(AzaleaDestroyTarget);
                int materialValue = materials.Sum(GetLinkMaterialValue);
                bool azaleaWillLeave = GetSpellCountInGrave() <= 3;
                if (azaleaWillLeave && targetScore + 1200 < materialValue && !linkageBridge)
                {
                    AzaleaDestroyTarget = null;
                    return false;
                }
            }

            if (linkageBridge)
                Logger.DebugWriteLine("[SkyStriker2026] Azalea: convert controlled monster " +
                    "into Linkage OTK bridge");
            AI.SelectMaterials(materials);
            SelectSkyStrikerLinkPlace();
            return true;
        }

        private bool CamelliaSummon()
        {
            if (CamelliaSummoned || HornetSetupStage != HornetZeroSetupStage.None ||
                StrategicReservesModeledLink() || GetSpellCountInGrave() > 3)
                return false;
            List<ClientCard> materials = GetModernLinkTwoMaterials(card => card.HasType(CardType.Effect), false);
            if (materials == null)
                return false;
            AI.SelectMaterials(materials);
            SelectSkyStrikerLinkPlace();
            return true;
        }

        private bool AmatsuSummon()
        {
            if (AmatsuSummoned)
                return false;

            if (HornetSetupStage == HornetZeroSetupStage.NeedBridgeLink)
            {
                if (ShouldPreferKainaBridge(true, Bot.HasInExtra(CardId.Kaina),
                    KainaSummoned))
                    return false;
                ClientCard kagari = Bot.GetMonsters().FirstOrDefault(card =>
                    card.IsFaceup() && card.IsCode(CardId.Kagari));
                if (kagari == null || Util.GetBotAvailZonesFromExtraDeck(new[] { kagari }) == 0)
                    return false;
                AI.SelectMaterials(new[] { kagari });
                SelectSkyStrikerLinkPlace();
                return true;
            }

            if (StrategicReservesModeledLink())
                return false;

            ClientCard material = GetSafeSkyStrikerLinkOneMaterial(card =>
                IsSkyStrikerAceLink(card) && !card.IsCode(CardId.Amatsu), false);
            if (material == null)
                return false;
            bool usefulInterruption = Enemy.GetMonsters().Any(card => card.IsFaceup() && card.Attack >= 2000);
            if (!Util.IsTurn1OrMain2() && !usefulInterruption)
                return false;
            AI.SelectMaterials(new[] { material });
            SelectSkyStrikerLinkPlace();
            return true;
        }

        private bool SuperStarslayerTyphonSummon()
        {
            if (Duel.Phase != DuelPhase.Main1 && Duel.Phase != DuelPhase.Main2)
                return false;
            ClientCard threat = Util.GetProblematicEnemyMonster(0, false) ??
                Enemy.GetMonsters().Where(card => card.IsFaceup())
                    .OrderByDescending(card => card.Attack).FirstOrDefault();
            if (threat == null || (!threat.IsFloodgate() && threat.Attack < 3000))
                return false;
            return Duel.Phase == DuelPhase.Main2 || Bot.GetMonsterCount() == 0;
        }

        private bool SuperStarslayerTyphonEffect()
        {
            if (DefaultCheckWhetherCardIsNegated(Card) || Card.Overlays.Count == 0 ||
                !Enemy.GetMonsters().Any())
                return false;
            AI.SelectCard(Card.Overlays);
            return true;
        }

        private bool CamelliaEffect()
        {
            if (DefaultCheckWhetherCardIsNegated(Card))
                return false;
            if (ActivateDescription == Util.GetStringId(CardId.Camellia, 0))
                return GetSpellCountInGrave() <= 3;
            return ActivateDescription == Util.GetStringId(CardId.Camellia, 1) &&
                Enemy.GetMonsters().Any();
        }

        private bool ZekeEffect()
        {
            if (DefaultCheckWhetherCardIsNegated(Card))
                return false;
            if (ActivateDescription == Util.GetStringId(CardId.Zeke, 0))
                return Enemy.GetMonsters().Any(card => card.IsFaceup());
            return ActivateDescription == Util.GetStringId(CardId.Zeke, 1) &&
                Bot.GetMonsters().Concat(Bot.GetSpells())
                    .Any(card => card != Card && GetFieldResourceValue(card) < 1000) &&
                (Enemy.LifePoints <= Card.GetAttackPower() + 1000 || Duel.Phase == DuelPhase.Main2);
        }

        private bool AzaleaEffect()
        {
            if (DefaultCheckWhetherCardIsNegated(Card))
                return false;

            bool battleTrigger = ActivateDescription == Util.GetStringId(CardId.Azalea, 1) ||
                ActivateDescription == -1 && Bot.BattlingMonster == Card &&
                Enemy.BattlingMonster != null;
            if (battleTrigger)
                return Bot.Graveyard.Any(card => card.IsSpell());

            bool summonTrigger = ActivateDescription == Util.GetStringId(CardId.Azalea, 0) ||
                ActivateDescription == -1 && AI.SelectingEffectYn &&
                Duel.LastSummonedCards.Contains(Card);
            if (!summonTrigger)
                return false;

            if (!IsLiveAzaleaDestroyTarget(AzaleaDestroyTarget))
                AzaleaDestroyTarget = GetBestAzaleaDestroyTarget();
            if (AzaleaDestroyTarget == null)
                return false;
            MarkCurrentChainHandled(AzaleaDestroyTarget);
            AI.SelectCard(AzaleaDestroyTarget);
            Logger.DebugWriteLine("[SkyStriker2026] Azalea summon trigger: destroy " +
                (AzaleaDestroyTarget.Name ?? AzaleaDestroyTarget.Id.ToString()) +
                ", spells in GY=" + GetSpellCountInGrave());
            return true;
        }

        private ClientCard GetBestAzaleaDestroyTarget()
        {
            return Enemy.GetMonsters().Concat(Enemy.GetSpells())
                .Where(IsLiveAzaleaDestroyTarget)
                .OrderByDescending(GetAzaleaDestroyScore)
                .FirstOrDefault();
        }

        private bool IsLiveAzaleaDestroyTarget(ClientCard target)
        {
            return target != null && target.Controller == 1 &&
                (target.Location == CardLocation.MonsterZone ||
                target.Location == CardLocation.SpellZone) &&
                !target.IsShouldNotBeTarget() && !target.IsShouldNotBeMonsterTarget() &&
                !IsCurrentChainHandled(target);
        }

        private int GetAzaleaDestroyScore(ClientCard target)
        {
            if (!IsLiveAzaleaDestroyTarget(target))
                return 0;
            int score;
            if (target.IsMonster())
            {
                score = target.IsFaceup() ? GetObservedThreatScore(target) : 1400;
                if (target.HasType(CardType.Fusion | CardType.Synchro | CardType.Xyz |
                    CardType.Link))
                    score += 1400;
            }
            else if (IsPersistentSpellTrap(target))
                score = target.IsFloodgate() ? 9000 : 4500;
            else
                score = target.IsFacedown() ? 1800 : 300;

            if (target == Util.GetProblematicEnemyMonster() ||
                target == Util.GetBestEnemySpell() || target.IsFloodgate())
                score += 3000;
            return score;
        }

        private IList<ClientCard> SelectAzaleaDestroyTarget(
            IList<ClientCard> cards, int min, int max)
        {
            List<ClientCard> ordered = new List<ClientCard>();
            ClientCard intended = FindMatchingOfferedCard(cards, AzaleaDestroyTarget);
            if (intended != null)
                ordered.Add(intended);
            ordered.AddRange(cards.Where(card => !ordered.Contains(card))
                .OrderByDescending(GetAzaleaDestroyScore));
            AzaleaDestroyTarget = null;
            return Util.CheckSelectCount(ordered, cards, min, max);
        }

        private bool RozeSummon()
        {
            if (NormalSummoned || Bot.HasInHand(CardId.Raye))
                return false;
            if (!StrategicAllows(SkyStrategicActionKind.NormalRoze))
                return false;

            // If an Ace already occupies the Extra Monster Zone, Roze's next
            // Link would be forced into the Main Monster Zone and switch off the
            // Sky Striker Spells that the planner still intends to use.
            if (HasPlannedSkyStrikerSpell() &&
                (Bot.GetMonstersExtraZoneCount() > 0 ||
                Bot.GetMonstersInMainZone().Count > 0))
                return false;
            return true;
        }

        private bool SphereModeSummon()
        {
            if (Duel.Player != 0 || Duel.Phase != DuelPhase.Main1 || NormalSummoned ||
                Enemy.GetMonsterCount() < 3 || ShouldUseEvenlyMatched())
                return false;
            SphereModeTributesPending = true;
            return true;
        }

        private bool ShouldUseEvenlyMatched()
        {
            return Bot.HasInHand(CardId.EvenlyMatched) && Duel.Turn >= 2 &&
                Bot.GetFieldCount() == 0 && Enemy.GetFieldCount() >= 2;
        }

        private bool EvenlyMatchedToBattle()
        {
            return ShouldUseEvenlyMatched();
        }

        private bool EvenlyMatchedEffect()
        {
            return Enemy.GetFieldCount() - Bot.GetFieldCount() > 1;
        }

        private bool FoolishBurialGoodsEffect()
        {
            if (Bot.HasInDeck(CardId.MetalfoesFusion))
                AI.SelectCard(CardId.MetalfoesFusion);
            else if (!KagariSummoned && Bot.HasInExtra(CardId.Kagari))
                AI.SelectCard(CardId.Engage, CardId.WidowAnchor, CardId.HornetDrones);
            else
                AI.SelectCard(CardId.ReinforcementOfTheArmy, CardId.UpstartGoblin, CardId.TwinTwisters);
            return true;
        }

        private bool PotOfDesiresEffect()
        {
            if (Bot.Deck.Count <= 15 || ShouldReserveKagariForLinkageOtk() ||
                ShouldPrepareWidowLinkageOtk())
                return false;

            if (!StrategicAllows(SkyStrategicActionKind.ActivatePot))
                return false;

            // Resolve deterministic access before risking ten unknown cards. Pot is
            // a recovery tool here, not an automatic opener.
            if (Bot.HasInHand(new[] { CardId.Engage, CardId.ReinforcementOfTheArmy }))
                return false;

            bool aceSecured = HasAccessibleCoreCard(CardId.Raye) ||
                HasAccessibleCoreCard(CardId.HornetDrones);
            bool linkageSecured = HasAccessibleCoreCard(CardId.Linkage);
            bool rozeSecured = HasAccessibleCoreCard(CardId.Roze);
            if (!aceSecured || !linkageSecured || !rozeSecured)
                return Bot.Hand.Count <= 2 && !aceSecured;

            return Bot.Hand.Count <= 3 || Duel.Turn >= 5 && Bot.Hand.Count <= 4;
        }

        private bool UpstartGoblinEffect()
        {
            // Do not raise the opponent from 8000 to 9000 while the exact Linkage
            // damage line is already prepared.
            return !ShouldReserveKagariForLinkageOtk() &&
                !ShouldPrepareWidowLinkageOtk();
        }

        private bool HasAccessibleCoreCard(int cardId)
        {
            return Bot.HasInHand(cardId) || Bot.HasInSpellZone(cardId) ||
                Bot.HasInMonstersZone(cardId) || Bot.HasInGraveyard(cardId);
        }

        private bool TwinTwistersEffect()
        {
            if (Util.ChainContainsCard(CardId.TwinTwisters))
                return false;

            List<ClientCard> targets = Enemy.GetSpells()
                .Where(card => card.IsFloodgate() || card.IsFacedown() ||
                    IsPersistentSpellTrap(card) || IsAzaminaFusionResourceDisruption(card))
                .Where(card => !IsCurrentChainHandled(card))
                .Distinct()
                .ToList();
            if (targets.Count == 0)
                return false;

            bool hasPriorityTarget = targets.Any(card => card.IsFloodgate() || card.IsFaceup());
            if (Duel.Player == 1 && !hasPriorityTarget && Duel.Phase != DuelPhase.End &&
                targets.Count < 2)
                return false;

            targets = targets
                .OrderByDescending(GetSpellTrapDestructionScore)
                .Take(2)
                .ToList();

            foreach (ClientCard target in targets)
                MarkCurrentChainHandled(target);
            SelectDiscard();
            AI.SelectNextCard(targets);
            return true;
        }

        private bool MultiroleActivation()
        {
            return Card.Location == CardLocation.Hand || Card.IsFacedown();
        }

        private bool MultiroleIgnitionEffect()
        {
            if (Card.Location != CardLocation.SpellZone || Card.IsFacedown() ||
                (Duel.Phase != DuelPhase.Main1 && Duel.Phase != DuelPhase.Main2))
                return false;

            ClientCard stolen = Bot.GetMonsters().FirstOrDefault(ControlledEnemyCards.Contains);
            if (stolen != null && Duel.Phase == DuelPhase.Main2)
            {
                AI.SelectCard(stolen);
                return true;
            }

            ClientCard raye = Bot.GetMonsters().FirstOrDefault(card =>
                card.IsCode(CardId.Raye) && Bot.GetMonstersExtraZoneCount() == 0);
            if (raye != null && HasAvailableRayeExtraDeckTarget())
            {
                AI.SelectCard(raye);
                return true;
            }

            ClientCard areaZero = Bot.GetFieldSpellCard();
            if (areaZero != null && areaZero.IsCode(CardId.AreaZero) &&
                Bot.GetMonsterCount() == 0 && Bot.HasInDeck(CardId.Raye))
            {
                AI.SelectCard(areaZero);
                return true;
            }

            bool preserveHornetDrones = HasTopologicComboPayoff() &&
                (TopologicComboStarted || ShouldStartTopologicCombo());
            ClientCard spentSpell = Bot.GetSpells().Where(card =>
                card != Card && card.IsSpell() && !MultiroleSetCards.Contains(card) &&
                !card.IsCode(CardId.WidowAnchor, CardId.Engage) &&
                (!preserveHornetDrones || !card.IsCode(CardId.HornetDrones)))
                .OrderBy(card => card.Id == CardId.MetalfoesFusion ? 0 : 1).FirstOrDefault();
            if (spentSpell != null &&
                (Bot.Graveyard.Count(card => card.IsSpell()) == 2 || spentSpell.Id == CardId.MetalfoesFusion))
            {
                AI.SelectCard(spentSpell);
                return true;
            }
            return false;
        }

        private bool MultiroleEndPhaseEffect()
        {
            return Duel.Phase == DuelPhase.End;
        }

        private bool AfterburnersEffect()
        {
            ClientCard target = GetEnemyMonsterTarget(false);
            if (target == null)
                return false;
            MarkCurrentChainHandled(target);
            AI.SelectCard(target);
            return true;
        }

        private bool JammingWavesEffect()
        {
            ClientCard target = Enemy.GetSpells().FirstOrDefault(card => card.IsFacedown());
            if (target == null)
            {
                ClientCard field = Bot.GetFieldSpellCard();
                if (field != null && field.IsFacedown())
                    target = field;
            }
            if (target == null)
                return false;
            AreaZeroSetForJamming = false;
            MarkCurrentChainHandled(target);
            AI.SelectCard(target);
            return true;
        }

        private bool EngageEffect()
        {
            if (!StrategicAllows(SkyStrategicActionKind.ActivateEngage))
                return false;
            int plannedTarget = GetStrategicSearchTarget(
                SkyStrategicActionKind.ActivateEngage);
            if (plannedTarget != 0)
            {
                List<int> planned = new List<int> { plannedTarget };
                planned.AddRange(GetSkyStrikerSearchPriority(false));
                AI.SelectCard(planned.Distinct().ToArray());
            }
            else
                AI.SelectCard(GetSkyStrikerSearchPriority(false));
            return true;
        }

        private bool HornetDronesEffect()
        {
            if (Bot.HasInMonstersZone(CardId.TopologicBomberDragon, true) &&
                Enemy.GetMonstersInMainZone().Count >= 2 &&
                !Duel.CurrentChainInfo.Any(chain => chain.ActivatePlayer == 0 &&
                    chain.IsActivateCode(CardId.TopologicBomberDragon, CardId.HornetDrones)))
                return true;

            if (Duel.CurrentChain.Count > 0 || Duel.Player == 1)
                return false;

            if (Duel.Phase != DuelPhase.Main1)
                return false;

            if (HornetSetupStage == HornetZeroSetupStage.KagariReady &&
                Bot.HasInMonstersZone(CardId.Kagari) && Bot.GetMonstersInMainZone().Count == 0)
            {
                HornetSetupStage = HornetZeroSetupStage.AwaitSecondToken;
                return true;
            }

            if (Bot.GetMonstersExtraZoneCount() == 0)
            {
                if (HornetSetupStage == HornetZeroSetupStage.None &&
                    !StrategicAllows(SkyStrategicActionKind.ActivateHornet))
                    return false;
                if (ShouldStartHornetZeroSetup())
                    HornetSetupStage = HornetZeroSetupStage.AwaitFirstToken;
                return true;
            }

            if (!Bot.HasInMonstersZone(CardId.Linkuriboh))
            {
                if (!ShouldStartBorrelswordCombo(true))
                    return false;
                BorrelswordComboStarted = true;
                return true;
            }

            return false;
        }

        private bool WidowAnchorEffectFirst()
        {
            if (Duel.Player != 0 || Duel.CurrentChain.Count > 0)
                return false;
            TacticalDecision decision = GetBestTacticalDecision();
            if (decision.Kind != TacticalActionKind.WidowAnchor ||
                decision.Targets.Count == 0)
                return false;
            ClientCard target = decision.Targets[0];
            WidowAnchorTarget = target;
            WidowAnchorTakeControl = decision.TakeControl;
            MarkCurrentChainHandled(target);
            AI.SelectCard(target);
            LogTacticalDecision(decision);
            return true;
        }

        private bool WidowAnchorEffect()
        {
            TacticalDecision decision = GetBestTacticalDecision();
            if (decision.Kind != TacticalActionKind.WidowAnchor ||
                decision.Targets.Count == 0)
                return false;
            ClientCard target = decision.Targets[0];
            WidowAnchorTakeControl = decision.TakeControl;
            WidowAnchorTarget = target;
            MarkCurrentChainHandled(target);
            AI.SelectCard(target);
            LogTacticalDecision(decision);
            return true;
        }

        private bool CanImmediatelyUseControlledMonster(ClientCard target, bool canAttack)
        {
            if (target == null || target.Controller != 1 || !HaveThreeSpellsInGrave() || Duel.Phase == DuelPhase.End)
                return false;
            if (Duel.Player == 1)
                return false;

            if (target.IsCode(CardId.Raye, CardId.Kagari, CardId.Shizuku,
                CardId.Hayate, CardId.Kaina))
            {
                if (target.IsCode(CardId.Raye) && EmptyMainMonsterZone() &&
                    !ZeroSummoned && Bot.HasInExtra(CardId.Zero))
                    return true;
                bool canSummonKagari = !target.IsCode(CardId.Kagari) && !KagariSummoned &&
                    Bot.HasInExtra(CardId.Kagari);
                bool canSummonHayate = !target.IsCode(CardId.Hayate) && !HayateSummoned &&
                    Bot.HasInExtra(CardId.Hayate);
                if (canSummonKagari || canSummonHayate)
                    return true;
            }

            // A successful mirror replay demonstrated that Widow Anchor can turn
            // the opponent's DARK Ace into Azalea together with our existing Ace,
            // then use Azalea as the first Linkage attacker. This is useful even
            // when Azalea has no destruction target, but only when the complete
            // conservative lethal route is already public and available.
            if (CanPrepareAzaleaLinkageBridge(target))
                return true;

            List<ClientCard> monsters = Bot.GetMonsters()
                .Where(card => card.IsFaceup() && !card.IsCode(CardId.BorrelswordDragon))
                .ToList();
            bool hasDifferentNameMaterial = monsters.Any(card => card.Id != target.Id);
            if (hasDifferentNameMaterial)
            {
                bool canUsePhoenix = Bot.HasInExtra(CardId.KnightmarePhoenix) &&
                    Util.GetProblematicEnemySpell() != null;
                ClientCard cerberusTarget = GetProblematicKnightmareCerberusTarget();
                bool canUseCerberus = Bot.HasInExtra(CardId.KnightmareCerberus) &&
                    cerberusTarget != null && cerberusTarget != target;
                bool canUnlockSpell =
                    Bot.HasInHandOrInSpellZone(new[]
                    {
                        CardId.Engage,
                        CardId.HornetDrones,
                        CardId.Afterburners,
                        CardId.JammingWaves,
                        CardId.SharkCannon
                    }) && Bot.HasInExtra(new[]
                    {
                        CardId.KnightmarePhoenix,
                        CardId.KnightmareCerberus
                    });
                if (canUsePhoenix || canUseCerberus || canUnlockSpell)
                    return true;
            }

            if (canAttack && target.Attack >= 2000)
                return true;

            if (Bot.HasInExtra(CardId.KnightmareUnicorn) &&
                Util.GetProblematicEnemyCard(0, true) != null)
            {
                if (monsters.Any(card => card.Id != target.Id &&
                    (card.HasType(CardType.Link) && card.LinkCount == 2 ||
                        target.HasType(CardType.Link) && target.LinkCount == 2)))
                    return true;
                if (monsters.Where(card => card.Id != target.Id)
                    .Select(card => card.Id).Distinct().Count() >= 2)
                    return true;
            }

            if (!Bot.HasInExtra(CardId.BorrelswordDragon) || !target.HasType(CardType.Effect))
                return false;
            List<ClientCard> effectMonsters = monsters
                .Where(card => card.HasType(CardType.Effect))
                .ToList();
            effectMonsters.Add(target);
            int subsetCount = 1 << effectMonsters.Count;
            for (int mask = 1; mask < subsetCount; mask++)
            {
                List<ClientCard> selected = new List<ClientCard>();
                for (int i = 0; i < effectMonsters.Count; i++)
                {
                    if ((mask & (1 << i)) != 0)
                        selected.Add(effectMonsters[i]);
                }
                if (selected.Count >= 3 && selected.Count <= 4 &&
                    selected.Contains(target) && Util.CanMakeLinkRating(selected, 4))
                    return true;
            }
            return false;
        }

        private bool ShouldTakeControlForEmergencyDefense(ClientCard target)
        {
            if (Duel.Player != 1 || target == null || !Bot.UnderAttack ||
                target != Enemy.BattlingMonster)
                return false;
            int defendingPower = Bot.BattlingMonster == null
                ? 0 : Bot.BattlingMonster.GetDefensePower();
            int projectedDamage = Math.Max(0, target.Attack - defendingPower);
            return projectedDamage >= Bot.LifePoints;
        }

        private bool SharkCannonEffect()
        {
            if (Util.ChainContainsCard(CardId.SharkCannon))
                return false;

            List<ClientCard> targets = Enemy.GetGraveyardMonsters()
                .OrderBy(card => SkyStrikerMirrorObserved && card.IsCode(CardId.Raye) ? 0 :
                    SkyStrikerMirrorObserved && card.IsCode(CardId.Roze) ? 1 : 2)
                .ThenByDescending(card => card.Attack)
                .ToList();

            ChainInfo lastChain = Duel.CurrentChainInfo.LastOrDefault();
            if (lastChain != null && lastChain.ActivatePlayer == 1)
            {
                ClientCard chainTarget = targets.FirstOrDefault(card =>
                    lastChain.Targets.Contains(card));
                if (chainTarget == null && lastChain.HasLocation(CardLocation.Grave) &&
                    lastChain.IsActivateCode(CardId.Raye, CardId.Roze, CardId.Camellia) &&
                    lastChain.RelatedCard != null && targets.Contains(lastChain.RelatedCard))
                    chainTarget = lastChain.RelatedCard;
                if (chainTarget != null)
                {
                    bool canConvertImmediately = ShouldReviveWithSharkCannon(chainTarget);
                    bool revive = ShouldReviveSharkChainTarget(Duel.Player == 0,
                        Duel.Phase == DuelPhase.Main1 || Duel.Phase == DuelPhase.Main2,
                        EmptyMainMonsterZone(), HaveThreeSpellsInGrave(),
                        chainTarget.IsCanRevive(), canConvertImmediately);
                    SharkCannonBanishOnly = !revive;
                    SharkCannonTarget = chainTarget;
                    AI.SelectCard(chainTarget);
                    if (revive)
                        AI.SelectPosition(CardPosition.FaceUpAttack);
                    Logger.DebugWriteLine("[SkyStriker2026] Shark Cannon chain target " +
                        chainTarget.Name + ": " + (revive
                            ? "revive for the current turn route"
                            : "banish because no immediate conversion exists"));
                    return true;
                }
            }

            if (Duel.CurrentChain.Count > 0)
                return false;

            if (SkyStrikerMirrorObserved && Duel.Player == 0 &&
                Duel.Phase == DuelPhase.Main1 && EmptyMainMonsterZone())
            {
                ClientCard mirrorAce = targets.FirstOrDefault(card =>
                    card.IsCode(CardId.Raye, CardId.Roze));
                if (mirrorAce != null)
                {
                    bool canTakeForLink = HaveThreeSpellsInGrave() &&
                        ShouldReviveWithSharkCannon(mirrorAce);
                    SharkCannonBanishOnly = !canTakeForLink;
                    SharkCannonTarget = mirrorAce;
                    AI.SelectCard(mirrorAce);
                    if (canTakeForLink)
                        AI.SelectPosition(CardPosition.FaceUpAttack);
                    return true;
                }
            }

            if (Duel.Player != 0 || Duel.Phase != DuelPhase.Main1 ||
                !EmptyMainMonsterZone() || !HaveThreeSpellsInGrave())
                return false;

            if (Bot.HasInMonstersZone(CardId.BorrelswordDragon, true) &&
                !BorrelswordPositionEffectUsed)
            {
                ClientCard borrelswordTarget = targets.FirstOrDefault(card =>
                    card.IsCanRevive() && IsBorrelswordTargetableMonster(card));
                if (borrelswordTarget != null)
                {
                    SharkCannonBanishOnly = false;
                    SharkCannonTarget = borrelswordTarget;
                    AI.SelectCard(borrelswordTarget);
                    AI.SelectPosition(CardPosition.FaceUpAttack);
                    return true;
                }
            }

            if (Bot.HasInMonstersZone(CardId.TopologicBomberDragon, true))
            {
                bool shouldTriggerTopologic = Enemy.GetMonstersInMainZone().Count >= 2 ||
                        Util.GetProblematicEnemyMonster()?.Sequence < 5;
                if (!shouldTriggerTopologic)
                    return false;
            }

            ClientCard target = targets.FirstOrDefault(ShouldReviveWithSharkCannon);
            if (target == null)
                return false;

            SharkCannonBanishOnly = false;
            SharkCannonTarget = target;
            AI.SelectCard(target);
            return true;
        }

        private static bool ShouldReviveSharkChainTarget(bool botTurn,
            bool mainPhase, bool emptyMainZone, bool threeSpells,
            bool targetCanRevive, bool canConvertImmediately)
        {
            return botTurn && mainPhase && emptyMainZone && threeSpells &&
                targetCanRevive && canConvertImmediately;
        }

        private bool ShouldReviveWithSharkCannon(ClientCard target)
        {
            if (!target.IsCanRevive())
                return false;
            if (Bot.HasInMonstersZone(CardId.BorrelswordDragon, true) &&
                !BorrelswordPositionEffectUsed && IsBorrelswordTargetableMonster(target))
                return true;
            return CanImmediatelyUseControlledMonster(target, false);
        }

        private bool HerculesBaseEffect()
        {
            if (Card.Location == CardLocation.SpellZone && !Card.IsFacedown())
                return true;

            if (Card.Location == CardLocation.Grave)
            {
                List<ClientCard> targets = Bot.Graveyard
                    .Where(card => !card.IsCode(CardId.HerculesBase) && card.HasSetcode(0x115))
                    .ToList();
                AI.SelectCard(OrderCardsById(targets, new[]
                {
                    CardId.Engage,
                    CardId.Kagari,
                    CardId.Shizuku,
                    CardId.Kaina,
                    CardId.WidowAnchor,
                    CardId.HornetDrones,
                    CardId.SharkCannon,
                    CardId.Afterburners,
                    CardId.JammingWaves,
                    CardId.Hayate,
                    CardId.Raye,
                    CardId.AreaZero,
                    CardId.Multirole
                }, false));
                return true;
            }

            if (Util.IsTurn1OrMain2())
                return false;
            ClientCard monster = Util.GetBestBotMonster(true);
            if (monster == null)
                return false;

            int beatable = Enemy.GetMonsters().Count(target =>
                target.GetDefensePower() < monster.Attack && !target.IsMonsterInvincible());
            if (beatable == 0 || (beatable < 2 && !HaveThreeSpellsInGrave()))
                return false;
            AI.SelectCard(monster);
            return true;
        }

        private bool AreaZeroSetForJammingWaves()
        {
            bool shouldSet = Duel.Player == 0 && Duel.Phase == DuelPhase.Main1 && EmptyMainMonsterZone() &&
                GetSpellCountInGrave() <= 2 && Bot.HasInHand(CardId.JammingWaves) &&
                Bot.HasInDeck(CardId.Raye) && !Enemy.GetSpells().Any(card => card.IsFacedown());
            if (shouldSet)
                AreaZeroSetForJamming = true;
            return shouldSet;
        }

        private bool AreaZeroEffect()
        {
            if (Card.Location == CardLocation.Hand)
                return Bot.GetFieldSpellCard() == null;
            if (Card.IsFacedown())
                return !AreaZeroSetForJamming;
            if (Card.Location == CardLocation.Grave)
            {
                return Bot.GetMonsterCount() == 0 || !EmptyMainMonsterZone() || Duel.Player == 1;
            }

            if (Card.Location != CardLocation.SpellZone && Card.Location != CardLocation.FieldZone)
                return false;

            List<ClientCard> mainMonsters = Bot.GetMonstersInMainZone();
            bool hasBlockedSpell = HasUsefulBlockedSkyStrikerSpell();
            ClientCard controlledMonster = mainMonsters.FirstOrDefault(card =>
                ControlledEnemyCards.Contains(card));
            if (controlledMonster != null &&
                (hasBlockedSpell || Duel.Phase == DuelPhase.Main2))
            {
                AI.SelectCard(controlledMonster);
                return true;
            }

            ClientCard disposableMonster = mainMonsters
                .Where(IsDisposableMonster)
                .OrderBy(GetLinkMaterialValue)
                .FirstOrDefault();
            if (disposableMonster != null &&
                (hasBlockedSpell || Duel.Phase == DuelPhase.Main2))
            {
                AI.SelectCard(disposableMonster);
                return true;
            }

            ClientCard raye = mainMonsters.FirstOrDefault(card => card.IsCode(CardId.Raye));
            if (raye != null && mainMonsters.Count == 1 && Bot.GetMonstersExtraZoneCount() == 0 &&
                HasAvailableRayeExtraDeckTarget())
            {
                AI.SelectCard(raye);
                return true;
            }

            if (Duel.Phase != DuelPhase.Main2 && !SetWhenHandIsFull())
                return false;
            ClientCard spentSpell = Bot.GetSpells().FirstOrDefault(card =>
                card != Card && card.IsSpell() &&
                !card.IsCode(CardId.Multirole, CardId.Engage, CardId.WidowAnchor,
                    CardId.HornetDrones, CardId.SharkCannon));
            if (spentSpell == null)
                return false;
            AI.SelectCard(spentSpell);
            return true;
        }

        private bool RayeSummon()
        {
            if (!StrategicAllows(SkyStrategicActionKind.NormalRaye))
                return false;
            if (HasPlannedSkyStrikerSpell() &&
                (Bot.GetMonstersExtraZoneCount() > 0 ||
                Bot.GetMonstersInMainZone().Count > 0))
                return false;
            return Bot.GetMonstersExtraZoneCount() == 0 ||
                (Enemy.GetMonsterCount() == 0 && Duel.Phase == DuelPhase.Main1);
        }

        private bool HasAvailableRayeExtraDeckTarget()
        {
            return (!KagariSummoned && Bot.HasInExtra(CardId.Kagari)) ||
                (!ShizukuSummoned && Bot.HasInExtra(CardId.Shizuku)) ||
                (!HayateSummoned && Bot.HasInExtra(CardId.Hayate)) ||
                (!KainaSummoned && Bot.HasInExtra(CardId.Kaina)) ||
                (!AmatsuSummoned && Bot.HasInExtra(CardId.Amatsu)) ||
                (!ZeroSummoned && Bot.HasInExtra(CardId.Zero));
        }

        private bool RayeEffect()
        {
            if (DefaultCheckWhetherCardIsNegated(Card))
                return false;
            if (Card.Location == CardLocation.Grave)
            {
                if (RayeGraveEffectUsed)
                    return false;
                if (Duel.Player == 1 && Bot.GetSpells().Any(card =>
                    card.IsFacedown() && card.IsSpell() && IsSkyStrikerCard(card)))
                    return false;
                RayeGraveEffectUsed = true;
                AI.SelectPosition(Duel.Player == 0 ? CardPosition.FaceUpAttack : CardPosition.FaceUpDefence);
                return true;
            }

            if (LinkageOtk == LinkageOtkStage.RayeRozeReady)
                return false;

            if (HasPlannedSkyStrikerSpell() && Bot.GetMonstersExtraZoneCount() > 0)
                return false;

            if (ShouldUseRayeForZeroSetup())
            {
                RayeToZeroSetupPending = true;
                RayeSelectTarget();
                return true;
            }

            bool shouldTag = Util.IsChainTarget(Card) ||
                Card == Bot.BattlingMonster && Duel.Player == 1 ||
                Duel.Player == 0 && Duel.Phase == DuelPhase.Main2;

            if (Duel.Player == 0 && Duel.Phase > DuelPhase.Main1 && Duel.Phase < DuelPhase.Main2 && Card.Attacked)
            {
                if (Bot.HasInMonstersZone(CardId.BorrelswordDragon, true) &&
                    !BorrelswordPositionEffectUsed)
                    return false;
                shouldTag = true;
            }

            if (!shouldTag)
                return false;
            RayeSelectTarget();
            return true;
        }

        private void RayeSelectTarget()
        {
            SelectSkyStrikerLinkPlace();
            if (RayeToZeroSetupPending && !ZeroSummoned && Bot.HasInExtra(CardId.Zero))
            {
                AI.SelectCard(CardId.Zero);
                return;
            }

            bool battleDefense = Duel.Player == 1 &&
                Duel.Phase > DuelPhase.Main1 && Duel.Phase < DuelPhase.Main2 &&
                Enemy.GetMonsters().Any(card => card.IsFaceup() && card.IsAttack() &&
                    !card.Attacked);
            if (battleDefense && !KainaSummoned && Bot.HasInExtra(CardId.Kaina))
            {
                AI.SelectCard(CardId.Kaina, CardId.Amatsu, CardId.Shizuku,
                    CardId.Kagari, CardId.Hayate);
                return;
            }

            if (Duel.Player == 1 && !AmatsuSummoned && Bot.HasInExtra(CardId.Amatsu))
            {
                AI.SelectCard(CardId.Amatsu, CardId.Kaina, CardId.Shizuku,
                    CardId.Kagari, CardId.Hayate);
                return;
            }

            if (Duel.Phase > DuelPhase.Main1 && Duel.Phase < DuelPhase.Main2 &&
                Bot.HasInMonstersZone(CardId.BorrelswordDragon, true) && !ShizukuSummoned)
            {
                AI.SelectCard(CardId.Shizuku, CardId.Kagari, CardId.Hayate);
                return;
            }

            if (!KagariSummoned && Bot.HasInGraveyard(new[]
            {
                CardId.Engage,
                CardId.HornetDrones,
                CardId.WidowAnchor,
                CardId.Afterburners,
                CardId.JammingWaves
            }))
                AI.SelectCard(CardId.Kagari, CardId.Zero, CardId.Amatsu,
                    CardId.Kaina, CardId.Shizuku, CardId.Hayate);
            else
                AI.SelectCard(CardId.Shizuku, CardId.Amatsu, CardId.Kaina,
                    CardId.Kagari, CardId.Hayate);
        }

        private bool KagariSummon()
        {
            if (KagariSummoned)
                return false;

            if (HornetSetupStage == HornetZeroSetupStage.NeedKagari)
            {
                ClientCard token = Bot.GetMonsters().FirstOrDefault(card =>
                    card.IsFaceup() && card.IsCode(CardId.Token));
                if (token == null || Util.GetBotAvailZonesFromExtraDeck(new[] { token }) == 0)
                    return false;
                KagariSummoned = true;
                AI.SelectMaterials(new[] { token });
                SelectSkyStrikerLinkPlace();
                return true;
            }

            if (ShouldReserveKagariForLinkageOtk())
                return false;

            if (!StrategicAllows(SkyStrategicActionKind.LinkKagari))
                return false;

            if (!Bot.HasInGraveyard(new[]
            {
                CardId.Engage,
                CardId.HornetDrones,
                CardId.WidowAnchor,
                CardId.Afterburners,
                CardId.JammingWaves
            }))
                return false;
            ClientCard material = GetSafeSkyStrikerLinkOneMaterial(card =>
                IsSkyStrikerAce(card) && !card.HasAttribute(CardAttribute.Fire), false);
            if (material == null)
                return false;
            KagariSummoned = true;
            AI.SelectMaterials(new[] { material });
            SelectSkyStrikerLinkPlace();
            return true;
        }

        private bool KagariEffect()
        {
            if (DefaultCheckWhetherCardIsNegated(Card))
                return false;
            if (LinkageOtk == LinkageOtkStage.KagariReady)
            {
                ClientCard linkage = Bot.GetGraveyardSpells().FirstOrDefault(card =>
                    card.IsCode(CardId.Linkage));
                if (linkage == null)
                {
                    LinkageOtk = LinkageOtkStage.None;
                    return false;
                }
                AI.SelectCard(linkage);
                return true;
            }

            if (HornetSetupStage == HornetZeroSetupStage.KagariReady)
            {
                ClientCard hornet = Bot.GetGraveyardSpells().FirstOrDefault(card =>
                    card.IsCode(CardId.HornetDrones));
                if (hornet != null)
                {
                    AI.SelectCard(hornet);
                    return true;
                }
                HornetSetupStage = HornetZeroSetupStage.None;
            }

            ClientCard target = Bot.GetGraveyardSpells().FirstOrDefault(card => card.IsCode(CardId.Engage));
            if (target == null && EmptyMainMonsterZone() && Util.GetProblematicEnemyMonster() != null)
                target = Bot.GetGraveyardSpells().FirstOrDefault(card => card.IsCode(CardId.Afterburners));
            if (target == null && EmptyMainMonsterZone() && Util.GetProblematicEnemySpell() != null)
                target = Bot.GetGraveyardSpells().FirstOrDefault(card => card.IsCode(CardId.JammingWaves));
            if (target != null)
                AI.SelectCard(target);
            else
                AI.SelectCard(CardId.HornetDrones, CardId.WidowAnchor, CardId.SharkCannon, CardId.Engage);
            return true;
        }

        private bool HayateSummon()
        {
            if (HayateSummoned)
                return false;
            if (HornetSetupStage == HornetZeroSetupStage.NeedBridgeLink)
            {
                if (ShouldPreferKainaBridge(true, Bot.HasInExtra(CardId.Kaina),
                    KainaSummoned))
                    return false;
                ClientCard kagari = Bot.GetMonsters().FirstOrDefault(card =>
                    card.IsFaceup() && card.IsCode(CardId.Kagari));
                if (kagari == null || Util.GetBotAvailZonesFromExtraDeck(new[] { kagari }) == 0)
                    return false;
                HayateSummoned = true;
                AI.SelectMaterials(new[] { kagari });
                SelectSkyStrikerLinkPlace();
                return true;
            }

            ClientCard otkBridge = GetLinkageOtkBridgeMaterial();
            if (otkBridge != null)
            {
                HayateSummoned = true;
                AI.SelectMaterials(new[] { otkBridge });
                SelectSkyStrikerLinkPlace();
                return true;
            }

            if (Util.IsTurn1OrMain2())
                return false;
            if (HaveThreeSpellsInGrave() && !Util.IsAllEnemyBetter())
                return false;
            ClientCard material = GetSafeSkyStrikerLinkOneMaterial(card =>
                IsSkyStrikerAce(card) && !card.HasAttribute(CardAttribute.Wind), false);
            if (material == null)
                return false;
            HayateSummoned = true;
            AI.SelectMaterials(new[] { material });
            SelectSkyStrikerLinkPlace();
            return true;
        }

        private bool HayateEffect()
        {
            if (DefaultCheckWhetherCardIsNegated(Card))
                return false;
            if (Bot.HasInMonstersZone(CardId.BorrelswordDragon, true) &&
                !Bot.HasInGraveyard(CardId.Raye) && Bot.HasInDeck(CardId.Raye))
                AI.SelectCard(CardId.Raye);
            else if (!KagariSummoned && !Bot.HasInGraveyard(CardId.Engage) && Bot.HasInDeck(CardId.Engage))
                AI.SelectCard(CardId.Engage);
            else if (!HaveThreeSpellsInGrave())
                AI.SelectCard(CardId.Engage, CardId.WidowAnchor, CardId.HornetDrones, CardId.SharkCannon);
            else if (!Bot.HasInGraveyard(CardId.Raye) && Bot.HasInDeck(CardId.Raye))
                AI.SelectCard(CardId.Raye);
            else
                AI.SelectCard(CardId.Engage, CardId.WidowAnchor, CardId.HornetDrones, CardId.SharkCannon);
            return true;
        }

        private bool KainaSummon()
        {
            if (KainaSummoned)
                return false;

            bool setupBridge = HornetSetupStage == HornetZeroSetupStage.NeedBridgeLink;
            ClientCard material = GetSafeSkyStrikerLinkOneMaterial(card =>
                IsSkyStrikerAce(card) && !card.HasAttribute(CardAttribute.Earth), setupBridge);
            if (material == null)
                return false;

            bool defensiveEndBoard = Duel.Player == 0 && Duel.Phase == DuelPhase.Main2 &&
                GetBestKainaTarget(Enemy.GetMonsters()) != null;
            if (!setupBridge && !defensiveEndBoard)
                return false;

            KainaSummoned = true;
            AI.SelectMaterials(new[] { material });
            SelectSkyStrikerLinkPlace();
            return true;
        }

        private static bool ShouldPreferKainaBridge(bool needsBridge,
            bool kainaAvailable, bool kainaAlreadyUsed)
        {
            return needsBridge && kainaAvailable && !kainaAlreadyUsed;
        }

        private bool KainaEffect()
        {
            if (DefaultCheckWhetherCardIsNegated(Card))
                return false;
            ClientCard target = GetBestKainaTarget(Enemy.GetMonsters());
            if (target == null)
                return false;
            AI.SelectCard(target);
            return true;
        }

        private ClientCard GetBestKainaTarget(IEnumerable<ClientCard> cards)
        {
            return cards.Where(card => card != null && card.Controller == 1 &&
                card.IsFaceup() && card.IsMonster())
                .OrderByDescending(card => !card.Attacked && card.IsAttack() ? 1 : 0)
                .ThenByDescending(card => card.Attack)
                .ThenByDescending(GetObservedThreatScore)
                .FirstOrDefault();
        }

        private IList<ClientCard> SelectKainaTarget(
            IList<ClientCard> cards, int min, int max)
        {
            ClientCard preferred = GetBestKainaTarget(cards);
            List<ClientCard> ordered = new List<ClientCard>();
            if (preferred != null)
                ordered.Add(preferred);
            ordered.AddRange(cards.Where(card => !ordered.Contains(card))
                .OrderByDescending(card => !card.Attacked && card.IsAttack() ? 1 : 0)
                .ThenByDescending(card => card.Attack));
            return Util.CheckSelectCount(ordered, cards, min, max);
        }

        private ClientCard GetHayateCrashTarget(ClientCard hayate, IList<ClientCard> defenders)
        {
            if (Duel.Player != 0 || Duel.Phase <= DuelPhase.Main1 || Duel.Phase >= DuelPhase.Main2)
                return null;
            if (!Bot.HasInMonstersZone(CardId.BorrelswordDragon, true) ||
                RayeGraveEffectUsed || hayate.IsDisabled() ||
                !Bot.HasInGraveyard(CardId.Raye))
                return null;

            return defenders
                .Where(card => IsHayateCrashTarget(hayate, card))
                .OrderBy(card => card.GetDefensePower())
                .FirstOrDefault();
        }

        private bool IsHayateCrashTarget(ClientCard hayate, ClientCard target)
        {
            if (hayate == null || target == null || !target.IsAttack() ||
                target.IsMonsterHasPreventActivationEffectInBattle())
                return false;
            int hayatePower = hayate.GetAttackPower();
            return target.GetDefensePower() >= hayatePower &&
                target.GetDefensePower() - hayatePower < Bot.LifePoints;
        }

        private bool IsBorrelswordPositionTarget(ClientCard target)
        {
            return target != null && target.IsFaceup() && target.IsAttack() &&
                IsBorrelswordTargetableMonster(target);
        }

        private bool IsBorrelswordTargetableMonster(ClientCard target)
        {
            return target != null && !target.HasType(CardType.Link) &&
                !target.IsShouldNotBeTarget() && !target.IsShouldNotBeMonsterTarget();
        }

        private bool CanPrepareBorrelswordTargetWithSharkCannon()
        {
            return HaveThreeSpellsInGrave() &&
                Bot.HasInHandOrInSpellZone(CardId.SharkCannon) &&
                Enemy.GetGraveyardMonsters().Any(card =>
                    card.IsCanRevive() && IsBorrelswordTargetableMonster(card));
        }

        private bool CanUseGlowUpBulbEffect()
        {
            return !GlowUpBulbEffectUsed &&
                (Bot.HasInDeck(CardId.GlowUpBulb) ||
                    Bot.HasInHandOrInGraveyard(CardId.GlowUpBulb));
        }

        private bool ShouldStartBorrelswordCombo(bool hornetWillProvideMaterial)
        {
            if (Duel.Player != 0 || Duel.Turn == 1 || Duel.Phase != DuelPhase.Main1 ||
                NormalSummoned ||
                !Bot.HasInExtra(CardId.CrystronHalqifibrax) ||
                !Bot.HasInExtra(CardId.BorrelswordDragon) ||
                !Bot.Hand.Any(card => card.IsTuner()) ||
                !CanUseGlowUpBulbEffect())
                return false;

            bool canBuildCombo = Bot.GetMonsterCount() >= 2 ||
                Bot.HasInMonstersZone(CardId.Linkuriboh) ||
                Bot.HasInMonstersZone(CardId.Token) && Bot.HasInExtra(CardId.Linkuriboh) ||
                hornetWillProvideMaterial && Bot.HasInExtra(CardId.Linkuriboh);
            if (!canBuildCombo)
                return false;

            ClientCard hayate = Bot.GetMonsters().FirstOrDefault(card => card.IsCode(CardId.Hayate));
            if (hayate != null)
            {
                return !hayate.IsDisabled() && !RayeGraveEffectUsed &&
                    Bot.HasInGraveyard(CardId.Raye) &&
                    Enemy.GetMonsters().Any(card => IsHayateCrashTarget(hayate, card));
            }

            int enemyPositionTargets = Enemy.GetMonsters().Count(IsBorrelswordPositionTarget);
            bool hasOwnPositionTarget = Bot.GetMonsters().Any(IsBorrelswordPositionTarget);
            return enemyPositionTargets >= 3 || hasOwnPositionTarget ||
                CanPrepareBorrelswordTargetWithSharkCannon();
        }

        private bool ShouldStartTopologicCombo()
        {
            ClientCard material = Bot.GetMonsters().FirstOrDefault(card =>
                card.IsFaceup() && (IsDisposableMonster(card) ||
                    card.IsCode(CardId.Raye, CardId.Kagari, CardId.Hayate, CardId.Shizuku)));
            bool hasNormalSummonTuner = Bot.Hand.Any(card => card.IsTuner());
            return Duel.Player == 0 && Duel.Phase == DuelPhase.Main1 &&
                !BorrelswordComboStarted && !TopologicComboStarted && !NormalSummoned &&
                material != null && hasNormalSummonTuner &&
                HasTopologicComboPayoff() &&
                Bot.HasInExtra(CardId.CrystronHalqifibrax) &&
                Bot.HasInExtra(CardId.KnightmareUnicorn) &&
                Bot.HasInExtra(CardId.TopologicBomberDragon) &&
                CanUseGlowUpBulbEffect();
        }

        private bool HasTopologicComboPayoff()
        {
            return (Util.IsTurn1OrMain2() || Enemy.GetMonstersInMainZone().Count >= 2) &&
                Bot.HasInHandOrInSpellZone(CardId.HornetDrones);
        }

        private bool MetalfoesFusionEffect()
        {
            return Bot.Graveyard.Count(card => card.IsSpell()) > 3 || Bot.GetHandCount() < 3;
        }

        private bool LinkuribohSummon()
        {
            ClientCard token = Bot.GetMonsters().FirstOrDefault(card => card.IsCode(CardId.Token));
            if (token == null || !BorrelswordComboStarted ||
                !Bot.HasInExtra(CardId.BorrelswordDragon))
                return false;
            AI.SelectMaterials(token);
            return true;
        }

        private bool TunerSummon()
        {
            if (!Bot.HasInExtra(CardId.CrystronHalqifibrax)) return false;
            if (Bot.GetMonsterCount() == 0) return false;

            bool borrelswordCombo = BorrelswordComboStarted &&
                Duel.Player == 0 && Duel.Phase == DuelPhase.Main1 &&
                Bot.HasInMonstersZone(CardId.Linkuriboh) &&
                Bot.HasInExtra(CardId.BorrelswordDragon);
            if (borrelswordCombo)
                return true;

            if (Card.IsCode(CardId.GlowUpBulb) &&
                !Bot.HasInDeck(new[] { CardId.EffectVeiler, CardId.GhostOgre, CardId.AshBlossom }) &&
                !Bot.HasInHand(new[] { CardId.EffectVeiler, CardId.GhostOgre, CardId.AshBlossom }))
                return false;

            bool topologicCombo = ShouldStartTopologicCombo();
            if (topologicCombo)
            {
                TopologicComboStarted = true;
                return true;
            }

            borrelswordCombo = ShouldStartBorrelswordCombo(false);
            if (borrelswordCombo)
            {
                BorrelswordComboStarted = true;
                return true;
            }

            return Bot.GetMonsterCount() > 0 && !Bot.ExtraDeck.Any(card => card.HasSetcode(0x115));
        }

        private bool CrystronHalqifibraxSummon()
        {
            if (Duel.Player != 0 || Duel.Phase != DuelPhase.Main1)
                return false;

            if (BorrelswordComboStarted && Bot.HasInMonstersZone(CardId.Linkuriboh))
            {
                ClientCard tuner = Bot.GetMonsters().FirstOrDefault(card =>
                    card.IsTuner() && card.IsFaceup() && !card.IsCode(CardId.Hayate));
                ClientCard linkuriboh = Bot.GetMonsters().FirstOrDefault(card => card.IsCode(CardId.Linkuriboh));
                if (tuner == null || linkuriboh == null)
                    return false;
                AI.SelectMaterials(new[] { tuner, linkuriboh });
                return true;
            }

            if (BorrelswordComboStarted ||
                (TopologicComboStarted && HasTopologicComboPayoff()))
            {
                ClientCard tuner = Bot.GetMonsters().FirstOrDefault(card => card.IsTuner() && card.IsFaceup());
                ClientCard material = Bot.GetMonsters()
                    .Where(card => card != tuner && card.IsFaceup())
                    .OrderBy(GetLinkMaterialValue)
                    .FirstOrDefault();
                if (tuner == null || material == null)
                    return false;
                AI.SelectMaterials(new[] { tuner, material });
                return true;
            }

            List<ClientCard> comboMaterials = Bot.GetMonsters()
                .Where(card => card.IsFaceup())
                .OrderBy(card => card.Attack)
                .ToList();
            AI.SelectMaterials(comboMaterials);
            return true;
        }

        private bool CrystronHalqifibraxEffect()
        {
            if (DefaultCheckWhetherCardIsNegated(Card))
                return false;
            AI.SelectCard(CardId.GlowUpBulb, CardId.EffectVeiler, CardId.GhostOgre, CardId.AshBlossom);
            return true;
        }

        private bool LinkuribohEffect()
        {
            if (DefaultCheckWhetherCardIsNegated(Card))
                return false;
            if (Card.Location != CardLocation.Grave)
                return Duel.Player == 1 && Bot.BattlingMonster != null;
            if (IsTopologicComboLinkSummonReady())
                return false;
            if (Duel.Player != 0 || Duel.Phase != DuelPhase.Main1 ||
                !Bot.HasInMonstersZone(CardId.CrystronHalqifibrax))
                return false;

            ClientCard tribute = Bot.GetMonsters().FirstOrDefault(card =>
                card.Level == 1 && card.IsTuner() && card.IsFaceup() && !card.IsCode(CardId.Linkuriboh));
            if (tribute == null)
                return false;
            AI.SelectCard(tribute);
            return true;
        }

        private bool GlowUpBulbEffect()
        {
            bool summonForBorrelsword = Duel.Player == 0 && Duel.Phase == DuelPhase.Main1 &&
                !BorrelswordPositionEffectUsed &&
                Bot.MonsterZone.Concat(Enemy.MonsterZone).Count(
                    card => card != null && card.IsAttack() && !card.HasType(CardType.Link)) == 0 &&
                Bot.HasInMonstersZone(CardId.BorrelswordDragon, true);
            bool summon = summonForBorrelsword ||
                Bot.HasInMonstersZone(CardId.KnightmareUnicorn) ||
                (Bot.HasInMonstersZone(CardId.CrystronHalqifibrax) &&
                    Bot.HasInMonstersZone(CardId.Linkuriboh));
            if (summon)
            {
                GlowUpBulbEffectUsed = true;
                AI.SelectPosition(summonForBorrelsword ?
                    CardPosition.FaceUpAttack : CardPosition.FaceUpDefence);
                return true;
            }
            return false;
        }

        private bool BorrelswordDragonSummon()
        {
            if (Util.IsTurn1OrMain2())
                return false;

            bool committedCombo = BorrelswordComboStarted;
            bool canFinishDirectly = Enemy.GetMonsterCount() == 0 && Enemy.LifePoints <= 3000;
            bool worthwhileBattle = Enemy.GetMonsters().Count(IsBorrelswordPositionTarget) >= 3 ||
                Bot.GetMonsters().Any(IsBorrelswordPositionTarget) ||
                CanPrepareBorrelswordTargetWithSharkCannon();
            if (!committedCombo && !canFinishDirectly && !worthwhileBattle)
                return false;

            ClientCard halqifibrax = Bot.GetMonsters().FirstOrDefault(card =>
                card.IsCode(CardId.CrystronHalqifibrax));
            ClientCard linkuriboh = Bot.GetMonsters().FirstOrDefault(card => card.IsCode(CardId.Linkuriboh));
            ClientCard bulb = Bot.GetMonsters().FirstOrDefault(card => card.IsCode(CardId.GlowUpBulb));
            if (halqifibrax != null && linkuriboh != null && bulb != null)
            {
                List<ClientCard> comboMaterials = new List<ClientCard> { halqifibrax, linkuriboh, bulb };
                if (Util.GetBotAvailZonesFromExtraDeck(comboMaterials) > 0)
                {
                    AI.SelectMaterials(comboMaterials);
                    return true;
                }
            }

            ClientCard positionTarget = Bot.GetMonsters()
                .Where(IsBorrelswordPositionTarget)
                .OrderBy(card => ControlledEnemyCards.Contains(card) ? 0 : 1)
                .ThenBy(card => card.Attack)
                .FirstOrDefault();
            List<ClientCard> materials = positionTarget == null ? null :
                GetLinkMaterials(4, 3, 4, true, false, positionTarget);
            if (materials == null)
                materials = GetLinkMaterials(4, 3, 4, true, false);
            if (materials == null)
                return false;
            AI.SelectMaterials(materials);
            return true;
        }

        private bool BorrelswordDragonEffect()
        {
            if (ActivateDescription == -1 ||
                ActivateDescription == Util.GetStringId(CardId.BorrelswordDragon, 1))
            {
                BorrelswordAttackEffectUsed = true;
                return true;
            }

            if (ActivateDescription != Util.GetStringId(CardId.BorrelswordDragon, 0))
                return false;

            ClientCard target = null;
            if (Duel.Player == 0 && Duel.Phase > DuelPhase.Main1 && Duel.Phase < DuelPhase.Main2)
            {
                target = Bot.GetMonsters().FirstOrDefault(card =>
                    card.IsCode(CardId.Raye) && card.IsAttack() && card.Attacked);
                if (target == null)
                    target = Bot.GetMonsters().FirstOrDefault(card =>
                        card.IsAttack() && card.Attacked && !card.HasType(CardType.Link));
                if (target == null && Card.Attacked)
                    target = Enemy.GetMonsters()
                        .Where(IsBorrelswordPositionTarget)
                        .OrderBy(card => card.Attack)
                        .FirstOrDefault();
            }
            else if (Duel.Player == 1)
            {
                target = Enemy.GetMonsters()
                    .Where(IsBorrelswordPositionTarget)
                    .OrderByDescending(card => card.Attack)
                    .FirstOrDefault();
            }

            if (target == null)
                return false;
            BorrelswordPositionEffectUsed = true;
            AI.SelectCard(target);
            return true;
        }

        private bool KnightmarePhoenixSummon()
        {
            if (IsTopologicComboLinkSummonReady())
                return false;
            if (Util.GetProblematicEnemySpell() == null)
                return false;
            return SelectKnightmareLinkMaterials(false);
        }

        private bool KnightmarePhoenixEffect()
        {
            if (DefaultCheckWhetherCardIsNegated(Card))
                return false;
            ClientCard target = Util.GetProblematicEnemySpell();
            if (target == null || IsCurrentChainHandled(target) || Bot.Hand.Count == 0)
                return false;
            MarkCurrentChainHandled(target);
            SelectDiscard();
            AI.SelectNextCard(target);
            return true;
        }

        private bool KnightmareCerberusSummon()
        {
            if (IsTopologicComboLinkSummonReady())
                return false;
            if (GetProblematicKnightmareCerberusTarget() == null)
                return false;
            return SelectKnightmareLinkMaterials(false);
        }

        private bool KnightmareCerberusEffect()
        {
            if (DefaultCheckWhetherCardIsNegated(Card))
                return false;
            ClientCard target = GetProblematicKnightmareCerberusTarget();
            if (target == null || Bot.Hand.Count == 0)
                return false;
            MarkCurrentChainHandled(target);
            SelectDiscard();
            AI.SelectNextCard(target);
            return true;
        }

        private bool KnightmarePhoenixUnlockSummon()
        {
            if (!ShouldUnlockMainMonsterZones())
                return false;
            if (Util.GetProblematicEnemySpell() == null &&
                GetProblematicKnightmareCerberusTarget() != null)
                return false;
            return SelectKnightmareLinkMaterials(true);
        }

        private bool KnightmareCerberusUnlockSummon()
        {
            return ShouldUnlockMainMonsterZones() && SelectKnightmareLinkMaterials(true);
        }

        private bool ShouldUnlockMainMonsterZones()
        {
            if (Duel.Player != 0 || Duel.CurrentChain.Count > 0 ||
                Duel.Phase != DuelPhase.Main1 && Duel.Phase != DuelPhase.Main2 ||
                Bot.GetMonsterCount() > 2 || EmptyMainMonsterZone())
                return false;

            return HasUsefulBlockedSkyStrikerSpell() ||
                (Bot.GetMonsters().Any(IsDisposableMonster) && Enemy.GetMonsterCount() > 0);
        }

        private bool SelectKnightmareLinkMaterials(bool requireExtraMonsterZone)
        {
            List<ClientCard> materials;
            if (requireExtraMonsterZone)
            {
                materials = Bot.GetMonsters()
                    .Where(card => card.IsFaceup() && !card.IsCode(CardId.BorrelswordDragon))
                    .OrderBy(GetLinkMaterialValue)
                    .ToList();
                if (materials.Count != 2 || materials.Select(card => card.Id).Distinct().Count() != 2 ||
                    !Util.CanMakeLinkRating(materials, 2))
                    return false;
            }
            else
            {
                materials = GetLinkMaterials(2, 2, 2, false, true);
            }
            if (materials == null)
                return false;
            int available = Util.GetBotAvailZonesFromExtraDeck(materials);
            if (requireExtraMonsterZone && !Duel.IsNewRule2020 &&
                (available & Zones.ExtraMonsterZones) == 0)
                return false;
            AI.SelectMaterials(materials);
            if (requireExtraMonsterZone)
                AI.SelectPlace(Zones.ExtraMonsterZones);
            return true;
        }

        private ClientCard GetProblematicKnightmareCerberusTarget()
        {
            List<ClientCard> targets = Enemy.GetMonstersInMainZone()
                .Where(card => card.IsSpecialSummoned && !card.IsShouldNotBeTarget() &&
                    !card.IsShouldNotBeMonsterTarget() && !IsCurrentChainHandled(card))
                .ToList();
            ClientCard problematic = Util.GetProblematicEnemyMonster(0, true);
            if (problematic != null && targets.Contains(problematic))
                return problematic;
            return null;
        }

        private bool IsTopologicComboLinkSummonReady()
        {
            if (!TopologicComboStarted || !HasTopologicComboPayoff())
                return false;
            ClientCard halqifibrax = Bot.GetMonsters().FirstOrDefault(card =>
                card.IsCode(CardId.CrystronHalqifibrax));
            bool canSummonUnicorn = halqifibrax != null &&
                Bot.GetMonsters().Any(card => card != halqifibrax && card.IsTuner() && card.IsFaceup()) &&
                Bot.HasInExtra(CardId.KnightmareUnicorn);
            bool canSummonTopologic = Bot.HasInMonstersZone(CardId.KnightmareUnicorn) &&
                Bot.HasInMonstersZone(CardId.GlowUpBulb) &&
                Bot.HasInExtra(CardId.TopologicBomberDragon);
            return canSummonUnicorn || canSummonTopologic;
        }

        private bool KnightmareUnicornSummon()
        {
            ClientCard halqifibrax = Bot.GetMonsters().FirstOrDefault(card =>
                card.IsCode(CardId.CrystronHalqifibrax));
            ClientCard tuner = Bot.GetMonsters().FirstOrDefault(card =>
                card != halqifibrax && card.IsTuner() && card.IsFaceup());
            if (halqifibrax != null && tuner != null)
            {
                List<ClientCard> comboMaterials = new List<ClientCard> { halqifibrax, tuner };
                if (Util.GetBotAvailZonesFromExtraDeck(comboMaterials) != 0)
                {
                    AI.SelectMaterials(comboMaterials);
                    return true;
                }
            }

            if (Util.GetProblematicEnemyCard(0, true) == null)
                return false;
            List<ClientCard> materials = GetLinkMaterials(3, 2, 3, false, true);
            if (materials == null)
                return false;
            AI.SelectMaterials(materials);
            return true;
        }

        private bool KnightmareUnicornEffect()
        {
            if (DefaultCheckWhetherCardIsNegated(Card))
                return false;
            if (TopologicComboStarted && HasTopologicComboPayoff() && Bot.Hand.Count < 2)
                return false;

            ClientCard target = Util.GetProblematicEnemyCard(0, true) ?? Util.GetBestEnemyCard(false, true);
            if (target == null || IsCurrentChainHandled(target) ||
                BorrelswordComboStarted && Enemy.GetMonsterCount() == 1)
                return false;
            MarkCurrentChainHandled(target);
            SelectDiscard();
            AI.SelectNextCard(target);
            return true;
        }

        private bool TopologicBomberDragonSummon()
        {
            if (TopologicComboStarted && HasTopologicComboPayoff())
            {
                ClientCard unicorn = Bot.GetMonsters().FirstOrDefault(card =>
                    card.IsCode(CardId.KnightmareUnicorn));
                ClientCard bulb = Bot.GetMonsters().FirstOrDefault(card =>
                    card.IsCode(CardId.GlowUpBulb));
                if (unicorn != null && bulb != null)
                {
                    List<ClientCard> comboMaterials = new List<ClientCard> { unicorn, bulb };
                    if (Util.GetBotAvailZonesFromExtraDeck(comboMaterials) != 0)
                    {
                        AI.SelectMaterials(comboMaterials);
                        return true;
                    }
                }
            }

            if (Bot.HasInMonstersZone(CardId.Shizuku) ||
                Bot.HasInExtra(CardId.Shizuku) && Bot.GetMonsters().Any(card =>
                    card.IsCode(CardId.Raye, CardId.Kagari, CardId.Hayate,
                        CardId.Kaina)))
                return false;
            List<ClientCard> materials = GetLinkMaterials(4, 2, 4, true, false);
            if (materials == null)
                return false;
            AI.SelectMaterials(materials);
            return true;
        }

        private bool ShizukuSummon()
        {
            if (ShizukuSummoned)
                return false;
            if (HornetSetupStage == HornetZeroSetupStage.NeedBridgeLink)
            {
                ClientCard kagari = Bot.GetMonsters().FirstOrDefault(card =>
                    card.IsFaceup() && card.IsCode(CardId.Kagari));
                if (kagari == null || Util.GetBotAvailZonesFromExtraDeck(new[] { kagari }) == 0)
                    return false;
                ShizukuSummoned = true;
                AI.SelectMaterials(new[] { kagari });
                SelectSkyStrikerLinkPlace();
                return true;
            }

            if (!Util.IsTurn1OrMain2())
                return false;
            if (Duel.Turn == 1 && Bot.HasInMonstersZone(CardId.Zero))
                return false;
            if (!StrategicAllows(SkyStrategicActionKind.LinkShizuku))
                return false;
            ClientCard material = GetSafeSkyStrikerLinkOneMaterial(card =>
                IsSkyStrikerAce(card) && !card.HasAttribute(CardAttribute.Water), false);
            if (material == null)
                return false;
            ShizukuSummoned = true;
            AI.SelectMaterials(new[] { material });
            SelectSkyStrikerLinkPlace();
            return true;
        }

        private bool ShizukuFirstEndPhaseEffect()
        {
            if (Duel.Player != 0 || Duel.Phase != DuelPhase.End ||
                DefaultCheckWhetherCardIsNegated(Card) ||
                !Bot.HasInSpellZone(CardId.Multirole, true, true) ||
                Bot.HasInGraveyard(CardId.WidowAnchor) || !Bot.HasInDeck(CardId.WidowAnchor) ||
                Bot.GetSpellCountWithoutField() >= 5)
                return false;
            AI.SelectCard(CardId.WidowAnchor);
            return true;
        }

        private bool WidowAnchorEndPhaseEffect()
        {
            if (Duel.Player != 0 || Duel.Phase != DuelPhase.End ||
                Card.Location != CardLocation.Hand ||
                !Bot.HasInSpellZone(CardId.Multirole, true, true) ||
                Bot.HasInGraveyard(CardId.WidowAnchor) ||
                Bot.GetSpellCountWithoutField() >= 5)
                return false;
            AI.SelectCard(CardId.Shizuku);
            return true;
        }

        private bool ShizukuEffect()
        {
            if (DefaultCheckWhetherCardIsNegated(Card))
                return false;
            AI.SelectCard(GetSkyStrikerSearchPriority(true));
            return true;
        }

        private int[] GetSkyStrikerSearchPriority(bool includeEngage)
        {
            List<int> result = new List<int>();
            if (ShouldPrioritizeLinkageForOtkSearch() || ShouldPrepareWidowLinkageOtk())
                result.Add(CardId.Linkage);

            // Shizuku should normally take Engage when the server offers it. Engage
            // itself cannot search another Engage, so this entry is ignored there.
            if (includeEngage)
                result.Add(CardId.Engage);

            bool needStarter = !HasLiveSkyStrikerAccess();
            if (needStarter)
            {
                result.Add(CardId.Raye);
                result.Add(CardId.HornetDrones);
                result.Add(CardId.Roze);
            }

            // Replay 2026-11-04: once Zero/starter access was already secured,
            // Engage searched another Raye and ended with no interaction. A live
            // first-turn engine should convert Engage into Widow before redundant
            // access such as Hornet Drones.
            bool establishedFirstTurn = !needStarter && Duel.Player == 0 &&
                Duel.Phase == DuelPhase.Main1 && Duel.Turn == 1;
            if (establishedFirstTurn &&
                !Bot.HasInHandOrInSpellZone(CardId.WidowAnchor))
                result.Add(CardId.WidowAnchor);

            ClientCard problemMonster = Util.GetProblematicEnemyMonster();
            if (problemMonster != null)
            {
                if (!Bot.HasInHandOrInSpellZone(CardId.WidowAnchor))
                    result.Add(CardId.WidowAnchor);
                if (EmptyMainMonsterZone())
                    result.Add(CardId.Afterburners);
            }

            if (EmptyMainMonsterZone() && Enemy.GetSpells().Any(card =>
                card.IsFacedown() && !IsCurrentChainHandled(card)))
                result.Add(CardId.JammingWaves);

            if (CanProfitablySearchLemnisgate())
                result.Add(CardId.Lemnisgate);

            if (SkyStrikerMirrorObserved)
            {
                if (Enemy.GetGraveyardMonsters().Any(card =>
                    card.IsCode(CardId.Raye, CardId.Roze)) &&
                    !Bot.HasInHandOrInSpellZone(CardId.SharkCannon))
                    result.Add(CardId.SharkCannon);
                if (Enemy.GetMonsters().Any(card => card.IsFaceup() &&
                    IsSkyStrikerAce(card)) &&
                    !Bot.HasInHandOrInSpellZone(CardId.WidowAnchor))
                    result.Add(CardId.WidowAnchor);
                if (!Bot.HasInHandOrInSpellZone(CardId.Multirole))
                    result.Add(CardId.Multirole);
            }

            bool graveyardThreat = Enemy.GetGraveyardMonsters().Any(card =>
                card.IsMonsterDangerous() || card.IsFloodgate());
            if (graveyardThreat && !Bot.HasInHandOrInSpellZone(CardId.SharkCannon))
                result.Add(CardId.SharkCannon);

            if (!Bot.HasInHandOrInSpellZone(CardId.Multirole) &&
                (Duel.Turn == 1 || GetSpellCountInGrave() >= 2))
                result.Add(CardId.Multirole);
            if (!Bot.HasInHandOrInSpellZone(CardId.Linkage))
                result.Add(CardId.Linkage);
            if (!Bot.HasInHandOrInSpellZone(CardId.HornetDrones))
                result.Add(CardId.HornetDrones);
            if (problemMonster != null)
                result.Add(CardId.WidowAnchor);
            if (EmptyMainMonsterZone() && problemMonster != null)
                result.Add(CardId.Afterburners);
            if (EmptyMainMonsterZone() && Enemy.GetSpells().Any(card =>
                card.IsFacedown() && !IsCurrentChainHandled(card)))
                result.Add(CardId.JammingWaves);
            if (!Bot.HasInHand(CardId.Raye) && !Bot.HasInMonstersZone(CardId.Raye) &&
                !Bot.HasInGraveyard(CardId.Raye))
                result.Add(CardId.Raye);
            if (!Bot.HasInHandOrInSpellZone(CardId.Multirole))
                result.Add(CardId.Multirole);
            if (!Bot.HasInHandOrInSpellZone(CardId.WidowAnchor))
                result.Add(CardId.WidowAnchor);
            if (!Bot.HasInHandOrInSpellZone(CardId.Lemnisgate) &&
                Bot.Graveyard.Any(card => IsSkyStrikerAce(card)))
                result.Add(CardId.Lemnisgate);
            result.AddRange(new[]
            {
                CardId.Linkage,
                CardId.HornetDrones,
                CardId.WidowAnchor,
                CardId.SharkCannon,
                CardId.Lemnisgate,
                CardId.Afterburners,
                CardId.JammingWaves,
                CardId.AreaZero,
                CardId.Multirole,
                CardId.Raye,
                CardId.Roze
            });
            return result.Distinct().ToArray();
        }

        private bool HasLiveSkyStrikerAccess()
        {
            if (Bot.HasInHandOrHasInMonstersZone(CardId.Raye) ||
                Bot.HasInHandOrHasInMonstersZone(CardId.Roze) ||
                Bot.GetMonsters().Any(IsSkyStrikerAce))
                return true;
            return EmptyMainMonsterZone() &&
                Bot.HasInHandOrInSpellZone(CardId.HornetDrones);
        }

        private bool CanProfitablySearchLemnisgate()
        {
            if (Bot.HasInHandOrInSpellZone(CardId.Lemnisgate))
                return false;
            return HasProfitableLemnisgatePlan();
        }

        private ClientCard GetEnemyMonsterTarget(bool problematicOnly)
        {
            List<ClientCard> candidates = Enemy.GetMonsters()
                .Where(card => card.IsFaceup() && !card.IsShouldNotBeTarget() &&
                    !card.IsShouldNotBeSpellTrapTarget() && !IsCurrentChainHandled(card) &&
                    (card != WidowAnchorTarget || !card.IsDisabled()))
                .ToList();

            ClientCard target = candidates.GetFloodgate() ??
                candidates.GetDangerousMonster() ??
                candidates.GetInvincibleMonster() ??
                candidates.GetShouldBeDisabledBeforeItUseEffectMonster();
            if (target != null)
                return target;

            if (SkyStrikerMirrorObserved && Duel.Player == 0 &&
                HaveThreeSpellsInGrave())
            {
                ClientCard mirrorLink = candidates
                    .Where(card => IsSkyStrikerAce(card) && !card.IsCode(CardId.Zero) &&
                        CanImmediatelyUseControlledMonster(card, true))
                    .OrderByDescending(GetMirrorMonsterPriority)
                    .FirstOrDefault();
                if (mirrorLink != null)
                    return mirrorLink;
            }

            if (problematicOnly)
            {
                int bestBotAttack = Util.GetBestAttack(Bot);
                return candidates
                    .Where(card => card.IsAttack() && card.GetDefensePower() >= bestBotAttack)
                    .OrderByDescending(card => card.GetDefensePower())
                    .FirstOrDefault();
            }

            return candidates.GetHighestAttackMonster();
        }

        private List<ClientCard> GetForbiddenDropletTargets()
        {
            List<ClientCard> candidates = Enemy.GetMonsters()
                .Where(card => card.IsFaceup() && card.HasType(CardType.Effect) &&
                    !card.IsDisabled() && !IsCurrentChainHandled(card))
                .ToList();
            List<ClientCard> result = new List<ClientCard>();
            if (candidates.Count == 0)
                return result;

            ClientCard last = Util.GetLastChainCard();
            if (Duel.LastChainPlayer == 1 && last != null && candidates.Contains(last) &&
                IsImportantForbiddenDropletTarget(last))
                result.Add(last);

            int projectedAttack = GetRemainingEnemyBattleAttack();
            if (Duel.Player == 1 && projectedAttack >= Bot.LifePoints)
            {
                foreach (ClientCard attacker in candidates.Where(card =>
                    card.IsAttack() && !card.Attacked)
                    .OrderByDescending(card => card.Attack))
                {
                    if (projectedAttack < Bot.LifePoints)
                        break;
                    if (!result.Contains(attacker))
                        result.Add(attacker);
                    projectedAttack -= Math.Max(0, attacker.Attack / 2);
                }
            }

            if (Duel.CurrentChain.Count > 0 && result.Count == 0)
                return result;

            if (result.Count == 0)
            {
                ClientCard threat = candidates.FirstOrDefault(card =>
                    IsImportantForbiddenDropletTarget(card));
                if (threat != null)
                    result.Add(threat);
            }

            return result;
        }

        private bool IsImportantForbiddenDropletTarget(ClientCard target)
        {
            if (target == null)
                return false;
            ClientCard last = Util.GetLastChainCard();
            bool liveMirrorAceEffect = SkyStrikerMirrorObserved &&
                Duel.LastChainPlayer == 1 && last == target && Duel.CurrentChain.Contains(last) &&
                IsSkyStrikerAce(target) &&
                target.HasType(CardType.Link);
            return liveMirrorAceEffect || target.IsFloodgate() || target.IsMonsterDangerous() ||
                target.IsMonsterInvincible() ||
                target.IsMonsterShouldBeDisabledBeforeItUseEffect();
        }

        private int GetRemainingEnemyBattleAttack()
        {
            if (Duel.Player != 1 || Duel.Phase <= DuelPhase.Main1 ||
                Duel.Phase >= DuelPhase.Main2)
                return 0;
            return Enemy.GetMonsters()
                .Where(card => card.IsFaceup() && card.IsAttack() && !card.Attacked)
                .Sum(card => Math.Max(0, card.Attack));
        }

        private bool CanUseWidowAnchorInstead(ClientCard target)
        {
            if (target == null || !EmptyMainMonsterZone() || target.IsDisabled() ||
                target.IsShouldNotBeTarget() || target.IsShouldNotBeSpellTrapTarget() ||
                Duel.CurrentChain.Any(card => card.Controller == 0 &&
                    card.IsCode(CardId.WidowAnchor)))
                return false;

            return Duel.Player == 0
                ? Bot.HasInHandOrInSpellZone(CardId.WidowAnchor)
                : Bot.HasInSpellZone(CardId.WidowAnchor);
        }

        private List<ClientCard> GetAvailableForbiddenDropletCosts()
        {
            return Bot.Hand.Concat(Bot.GetMonsters()).Concat(Bot.GetSpells())
                .Where(card => card != null && card != Card && !card.IsCode(CardId.Token) &&
                    GetDropletCostValue(card) < 5000)
                .OrderBy(GetDropletCostValue)
                .ToList();
        }

        private IList<ClientCard> SelectForbiddenDropletTargets(
            IList<ClientCard> cards, int min, int max)
        {
            List<ClientCard> ordered = new List<ClientCard>();
            foreach (ClientCard target in ForbiddenDropletTargets)
            {
                ClientCard offered = FindMatchingOfferedCard(cards, target);
                if (offered != null && !ordered.Contains(offered))
                    ordered.Add(offered);
            }
            ordered.AddRange(OrderEnemyTargets(cards).Where(card => !ordered.Contains(card)));
            ForbiddenDropletTargets.Clear();
            return Util.CheckSelectCount(ordered, cards, min, max);
        }

        private bool IsCurrentChainHandled(ClientCard card)
        {
            return card != null && CurrentChainHandledCards.Contains(card);
        }

        private void MarkCurrentChainHandled(ClientCard card)
        {
            if (card != null && !CurrentChainHandledCards.Contains(card))
                CurrentChainHandledCards.Add(card);
        }

        private bool HasUsefulBlockedSkyStrikerSpell()
        {
            if (EmptyMainMonsterZone())
                return false;
            ClientCard problematicMonster = GetEnemyMonsterTarget(true);
            return Bot.HasInHandOrInSpellZone(CardId.Engage) ||
                (Bot.HasInHandOrInSpellZone(CardId.Afterburners) && problematicMonster != null) ||
                (Bot.HasInHandOrInSpellZone(CardId.JammingWaves) &&
                    Enemy.GetSpells().Any(card => card.IsFacedown() && !IsCurrentChainHandled(card))) ||
                (Bot.HasInHandOrInSpellZone(CardId.WidowAnchor) && problematicMonster != null &&
                    !problematicMonster.IsDisabled() && !problematicMonster.HasType(CardType.Normal));
        }

        private bool IsDisposableMonster(ClientCard card)
        {
            if (card == null)
                return false;
            if (ControlledEnemyCards.Contains(card) ||
                card.IsCode(CardId.Token, CardId.GlowUpBulb, CardId.Linkuriboh, CardId.AshBlossom,
                    CardId.GhostOgre, CardId.EffectVeiler, CardId.MaxxC))
                return true;
            return !card.IsCode(CardId.Raye, CardId.Roze, CardId.Kagari, CardId.Hayate,
                CardId.Kaina, CardId.Shizuku, CardId.Zero, CardId.Camellia,
                CardId.Amatsu, CardId.Azalea, CardId.Zeke,
                CardId.CrystronHalqifibrax, CardId.BorrelswordDragon,
                CardId.TopologicBomberDragon, CardId.KnightmareUnicorn);
        }

        private void SelectDiscard()
        {
            List<int> priorities = new List<int>
            {
                CardId.MetalfoesFusion,
                CardId.GlowUpBulb
            };
            if (!Bot.HasInGraveyard(CardId.Raye))
                priorities.Add(CardId.Raye);
            priorities.AddRange(new[]
            {
                CardId.ReinforcementOfTheArmy,
                CardId.FoolishBurialGoods,
                CardId.UpstartGoblin,
                CardId.HerculesBase
            });
            if (HasTopologicComboPayoff() &&
                (TopologicComboStarted || ShouldStartTopologicCombo()) &&
                Bot.HasInHand(CardId.HornetDrones))
            {
                ClientCard discard = Bot.Hand
                    .Where(card => card != Card && !card.IsCode(CardId.HornetDrones))
                    .OrderBy(card =>
                    {
                        int index = priorities.IndexOf(card.Id);
                        return index < 0 ? int.MaxValue : index;
                    })
                    .FirstOrDefault();
                if (discard != null)
                {
                    AI.SelectCard(discard);
                    return;
                }
            }
            priorities.Add(CardId.HornetDrones);
            AI.SelectCard(priorities);
        }

        private List<ClientCard> OrderCardsById(IList<ClientCard> cards, IList<int> ids, bool uniqueNames)
        {
            List<ClientCard> result = new List<ClientCard>();
            HashSet<int> addedIds = new HashSet<int>();
            foreach (int id in ids)
            {
                ClientCard card = cards.FirstOrDefault(candidate =>
                    candidate.IsCode(id) && (!uniqueNames || !addedIds.Contains(candidate.Id)));
                if (card != null)
                {
                    result.Add(card);
                    addedIds.Add(card.Id);
                }
            }
            foreach (ClientCard card in cards)
            {
                if (!result.Contains(card) && (!uniqueNames || !addedIds.Contains(card.Id)))
                {
                    result.Add(card);
                    addedIds.Add(card.Id);
                }
            }
            return result;
        }

        private IList<ClientCard> SelectZeroSearch(IList<ClientCard> cards, int min, int max)
        {
            List<ClientCard> ordered = new List<ClientCard>();
            if (StrategicSearchTargetPending != 0)
            {
                ordered.AddRange(cards.Where(card =>
                    card.IsCode(StrategicSearchTargetPending)));
                StrategicSearchTargetPending = 0;
            }
            int otherSpellsInGrave = Bot.Graveyard.Count(card =>
                card.IsSpell() && !card.IsCode(CardId.Engage));
            CardLocation preferredEngageLocation = otherSpellsInGrave >= 3
                ? CardLocation.Grave : CardLocation.Deck;

            ordered.AddRange(cards.Where(card => card.IsCode(CardId.Engage) &&
                card.Location == preferredEngageLocation && !ordered.Contains(card)));
            ordered.AddRange(cards.Where(card => card.IsCode(CardId.Engage) &&
                !ordered.Contains(card)));
            ordered.AddRange(OrderCardsById(cards, GetSkyStrikerSearchPriority(true), false)
                .Where(card => !ordered.Contains(card)));
            return Util.CheckSelectCount(ordered, cards, min, max);
        }

        private ClientCard FindMatchingOfferedCard(IList<ClientCard> cards, ClientCard intended)
        {
            if (intended == null)
                return null;
            return cards.FirstOrDefault(card => card == intended ||
                card.Controller == intended.Controller && card.Location == intended.Location &&
                card.Sequence == intended.Sequence && card.IsCode(intended.Id));
        }

        private bool ShouldUseRayeForZeroSetup()
        {
            bool legalSetup = Card.Location == CardLocation.MonsterZone && Duel.Player == 0 &&
                Duel.Phase == DuelPhase.Main1 && Duel.CurrentChain.Count == 0 &&
                LinkageOtk == LinkageOtkStage.None && !ZeroSummoned &&
                Bot.HasInExtra(CardId.Zero) && Bot.GetMonsterCount() == 1 &&
                (Duel.Turn == 1 || Enemy.GetMonsterCount() == 0);
            if (!legalSetup)
                return false;
            SkyStrategicAction recommendation = GetStrategicRecommendation();
            return recommendation == null ||
                recommendation.Kind == SkyStrategicActionKind.RayeTagZero;
        }

        private bool ShouldDeferZeroForBattleDamage(IList<ClientCard> materials)
        {
            if (Duel.Turn == 1 || Duel.Phase != DuelPhase.Main1 ||
                Enemy.GetMonsterCount() > 0 || LinkageOtk != LinkageOtkStage.None)
                return false;

            int materialDamage = materials
                .Where(card => !ControlledEnemyCards.Contains(card) && card.IsAttack() &&
                    !card.Attacked)
                .Sum(card => Math.Max(0, card.GetAttackPower()));
            bool defer = materialDamage > 2000;
            if (defer)
            {
                Logger.DebugWriteLine("[SkyStriker2026] defer Zero until Main2; " +
                    "material battle damage=" + materialDamage);
            }
            return defer;
        }

        private bool ShouldStartHornetZeroSetup()
        {
            return HornetSetupStage == HornetZeroSetupStage.None &&
                LinkageOtk == LinkageOtkStage.None && !KagariSummoned && !ZeroSummoned &&
                Bot.HasInExtra(CardId.Kagari) && Bot.HasInExtra(CardId.Zero) &&
                Bot.HasInExtra(new[] { CardId.Amatsu, CardId.Hayate,
                    CardId.Kaina, CardId.Shizuku }) &&
                (Duel.Turn == 1 || Enemy.GetMonsterCount() == 0);
        }

        private bool HasBetterZeroStarterThanLinkage()
        {
            if (NormalSummoned)
                return false;
            if (Bot.HasInHand(CardId.Raye))
                return true;
            if (Bot.HasInHandOrInSpellZone(CardId.HornetDrones) &&
                Bot.GetMonstersExtraZoneCount() == 0 && Duel.Phase == DuelPhase.Main1)
                return true;
            if (Bot.HasInDeck(CardId.Raye) &&
                Bot.HasInHandOrInSpellZone(new[]
                {
                    CardId.Engage,
                    CardId.ReinforcementOfTheArmy
                }))
                return true;
            return false;
        }

        private ClientCard GetLinkageOtkStarter()
        {
            if (Enemy.GetFieldCount() > 0 || KagariSummoned || ZeroSummoned ||
                !Bot.HasInExtra(CardId.Kagari) || !Bot.HasInExtra(CardId.Zero) ||
                !(Bot.HasInDeck(CardId.Raye) || Bot.HasInGraveyard(CardId.Raye)) ||
                !(Bot.HasInDeck(CardId.Roze) || Bot.HasInGraveyard(CardId.Roze)))
                return null;

            ClientCard attacker = Bot.GetMonsters().FirstOrDefault(card =>
                card.IsFaceup() && card.Sequence >= 5 && card.HasType(CardType.Link) &&
                card.Attacked && !card.IsCode(CardId.Kagari, CardId.Zero) &&
                card.GetAttackPower() >= 1500);
            if (attacker == null || attacker.GetAttackPower() + 6500 < Enemy.LifePoints)
                return null;
            return attacker;
        }

        private bool ShouldReserveKagariForLinkageOtk()
        {
            return GetPreparedLinkageOtkStarter() != null ||
                GetLinkageOtkBridgeMaterial() != null;
        }

        private bool CanPursuePreparedLinkageOtk()
        {
            return Duel.Player == 0 && Duel.Turn > 1 && Duel.Phase == DuelPhase.Main1 &&
                Duel.CurrentChain.Count == 0 && LinkageOtk == LinkageOtkStage.None &&
                Enemy.GetFieldCount() == 0 && !KagariSummoned && !ZeroSummoned &&
                (Bot.HasInHandOrInSpellZone(CardId.Linkage) ||
                Bot.HasInHand(CardId.Engage) && Bot.HasInDeck(CardId.Linkage)) &&
                Bot.HasInExtra(CardId.Kagari) && Bot.HasInExtra(CardId.Zero) &&
                (Bot.HasInDeck(CardId.Raye) || Bot.HasInGraveyard(CardId.Raye)) &&
                (Bot.HasInDeck(CardId.Roze) || Bot.HasInGraveyard(CardId.Roze));
        }

        private bool ShouldPrioritizeLinkageForOtkSearch()
        {
            if (Duel.Player != 0 || Duel.Turn == 1 || Duel.Phase != DuelPhase.Main1 ||
                Enemy.GetFieldCount() > 0 || KagariSummoned || ZeroSummoned ||
                !Bot.HasInDeck(CardId.Linkage) || !Bot.HasInExtra(CardId.Kagari) ||
                !Bot.HasInExtra(CardId.Zero) || !EmptyMainMonsterZone() ||
                !(Bot.HasInDeck(CardId.Raye) || Bot.HasInGraveyard(CardId.Raye)) ||
                !(Bot.HasInDeck(CardId.Roze) || Bot.HasInGraveyard(CardId.Roze)))
                return false;
            return Bot.GetMonsters().Any(card => card.IsFaceup() && card.Sequence >= 5 &&
                IsSkyStrikerAceLink(card) && !card.Attacked &&
                !card.IsCode(CardId.Kagari, CardId.Zero) &&
                card.GetAttackPower() + 6500 >= Enemy.LifePoints);
        }

        private bool ShouldPrepareWidowLinkageOtk()
        {
            if (Duel.Player != 0 || Duel.Turn == 1 || Duel.Phase != DuelPhase.Main1 ||
                !EmptyMainMonsterZone() || Enemy.GetFieldCount() != 1 ||
                !Bot.HasInHandOrInSpellZone(CardId.WidowAnchor) ||
                !Bot.HasInExtra(CardId.Hayate) || !Bot.HasInExtra(CardId.Kagari) ||
                !Bot.HasInExtra(CardId.Zero) || KagariSummoned || ZeroSummoned ||
                !(Bot.HasInDeck(CardId.Raye) || Bot.HasInGraveyard(CardId.Raye)) ||
                !(Bot.HasInDeck(CardId.Roze) || Bot.HasInGraveyard(CardId.Roze)) ||
                1500 + 6500 < Enemy.LifePoints)
                return false;

            ClientCard target = Enemy.GetMonsters().FirstOrDefault(card => card.IsFaceup() &&
                IsSkyStrikerAceLink(card) && !card.IsDisabled() &&
                !card.IsShouldNotBeTarget() && !card.IsShouldNotBeSpellTrapTarget());
            if (target == null)
                return false;

            bool engageLive = Bot.HasInHand(CardId.Engage) ||
                Duel.CurrentChain.Any(card => card.Controller == 0 && card.IsCode(CardId.Engage));
            bool canReachThreeSpells = HaveThreeSpellsInGrave() ||
                GetSpellCountInGrave() == 2 && engageLive;
            bool canAccessLinkage = Bot.HasInHandOrInSpellZone(CardId.Linkage) ||
                Bot.HasInDeck(CardId.Linkage) && engageLive;
            return canReachThreeSpells && canAccessLinkage;
        }

        private bool CanPrepareAzaleaLinkageBridge(ClientCard target)
        {
            if (target == null || target.Controller != 1 || Duel.Player != 0 ||
                Duel.Turn == 1 || Duel.Phase != DuelPhase.Main1 ||
                Enemy.GetFieldCount() != 1 || AzaleaSummoned || KagariSummoned ||
                ZeroSummoned || !target.HasAttribute(CardAttribute.Light | CardAttribute.Dark) ||
                !Bot.HasInExtra(CardId.Azalea) || !Bot.HasInExtra(CardId.Kagari) ||
                !Bot.HasInExtra(CardId.Zero) ||
                !(Bot.HasInDeck(CardId.Raye) || Bot.HasInGraveyard(CardId.Raye)) ||
                !(Bot.HasInDeck(CardId.Roze) || Bot.HasInGraveyard(CardId.Roze)) ||
                1500 + 6500 < Enemy.LifePoints)
                return false;

            bool canAccessLinkage = Bot.HasInHandOrInSpellZone(CardId.Linkage) ||
                Bot.HasInHand(CardId.Engage) && Bot.HasInDeck(CardId.Linkage);
            if (!canAccessLinkage)
                return false;

            return Bot.GetMonsters().Any(card => card.IsFaceup() &&
                !card.IsCode(CardId.Zero) &&
                card.HasAttribute(CardAttribute.Light | CardAttribute.Dark) &&
                Bot.GetMonstersInMainZone().All(main => main == card) &&
                Util.GetBotAvailZonesFromExtraDeck(new[] { card, target }) != 0);
        }

        private bool CanUseAzaleaAsLinkageBridge(IList<ClientCard> materials)
        {
            if (materials == null || materials.Count != 2 || Duel.Player != 0 ||
                Duel.Turn == 1 || Duel.Phase != DuelPhase.Main1 ||
                Enemy.GetFieldCount() != 0 || AzaleaSummoned || KagariSummoned ||
                ZeroSummoned || !materials.Any(ControlledEnemyCards.Contains) ||
                !Bot.HasInHandOrInSpellZone(CardId.Linkage) ||
                !Bot.HasInExtra(CardId.Kagari) || !Bot.HasInExtra(CardId.Zero) ||
                !(Bot.HasInDeck(CardId.Raye) || Bot.HasInGraveyard(CardId.Raye)) ||
                !(Bot.HasInDeck(CardId.Roze) || Bot.HasInGraveyard(CardId.Roze)) ||
                1500 + 6500 < Enemy.LifePoints)
                return false;

            return !Bot.GetMonstersInMainZone().Any(card => !materials.Contains(card)) &&
                Util.GetBotAvailZonesFromExtraDeck(materials) != 0;
        }

        private ClientCard GetPreparedLinkageOtkStarter()
        {
            if (!CanPursuePreparedLinkageOtk() || !EmptyMainMonsterZone())
                return null;
            return Bot.GetMonsters().FirstOrDefault(card => card.IsFaceup() &&
                card.Sequence >= 5 && IsSkyStrikerAceLink(card) && !card.Attacked &&
                !card.IsCode(CardId.Kagari, CardId.Zero) &&
                card.GetAttackPower() + 6500 >= Enemy.LifePoints);
        }

        private ClientCard GetLinkageOtkBridgeMaterial()
        {
            if (!CanPursuePreparedLinkageOtk() || HayateSummoned ||
                !Bot.HasInExtra(CardId.Hayate))
                return null;
            List<ClientCard> mainMonsters = Bot.GetMonstersInMainZone();
            if (mainMonsters.Count != 1)
                return null;
            ClientCard material = mainMonsters[0];
            if (!material.IsFaceup() || !IsSkyStrikerAce(material) ||
                material.HasAttribute(CardAttribute.Wind) ||
                Util.GetBotAvailZonesFromExtraDeck(new[] { material }) == 0 ||
                1500 + 6500 < Enemy.LifePoints)
                return null;
            return material;
        }

        private List<ClientCard> OrderEnemyTargets(IList<ClientCard> cards)
        {
            ClientCard problematic = Util.GetProblematicEnemyMonster();
            ClientCard bestSpell = Util.GetBestEnemySpell();
            return cards
                .OrderByDescending(card => card.Controller == 1 ? 1 : 0)
                .ThenByDescending(card => SkyStrikerMirrorObserved &&
                    card.IsCode(CardId.Multirole) ? 1 : 0)
                .ThenByDescending(card => card == problematic || card == bestSpell || card.IsFloodgate() ? 1 : 0)
                .ThenByDescending(card => card.IsFaceup() ? 1 : 0)
                .ThenByDescending(GetCardUtility)
                .ToList();
        }

        private ClientCard GetFallenAndVirtuousDestroyTarget()
        {
            List<ClientCard> candidates = Enemy.GetMonsters().Concat(Enemy.GetSpells())
                .Where(card => card.IsFaceup() && !card.IsShouldNotBeTarget() &&
                    !card.IsShouldNotBeSpellTrapTarget() && !IsCurrentChainHandled(card))
                .ToList();
            if (candidates.Count == 0)
                return null;
            ClientCard best = candidates.OrderByDescending(GetFallenAndVirtuousDestroyScore)
                .FirstOrDefault();
            return GetFallenAndVirtuousDestroyScore(best) >= 3500 ? best : null;
        }

        private bool IsLiveFallenAndVirtuousDestroyTarget(ClientCard target)
        {
            return target != null && target.Controller == 1 && target.IsFaceup() &&
                (target.Location == CardLocation.MonsterZone ||
                target.Location == CardLocation.SpellZone);
        }

        private IList<ClientCard> SelectFallenAndVirtuousDestroyTarget(
            IList<ClientCard> cards, int min, int max)
        {
            List<ClientCard> ordered = new List<ClientCard>();
            ClientCard intended = FindMatchingOfferedCard(cards, FallenAndVirtuousDestroyTarget);
            if (intended != null)
                ordered.Add(intended);
            ordered.AddRange(OrderEnemyTargets(cards).Where(card => !ordered.Contains(card)));
            FallenAndVirtuousDestroyTarget = null;
            return Util.CheckSelectCount(ordered, cards, min, max);
        }

        private int[] GetFallenAndVirtuousExtraDeckPriority()
        {
            List<int> result = new List<int>();
            // Albion is the default first send while another TFTV remains in Deck:
            // it replaces the spent interaction in the End Phase.
            if (Bot.HasInExtra(CardId.AlbionTheBrandedDragon) &&
                Bot.HasInDeck(CardId.TheFallenAndTheVirtuous))
                result.Add(CardId.AlbionTheBrandedDragon);

            bool canUseEcclesiaNow = Duel.Player == 0 &&
                (Duel.Phase == DuelPhase.Main1 || Duel.Phase == DuelPhase.Main2) &&
                Enemy.GetFieldCount() > 1 &&
                Bot.Graveyard.Concat(Bot.Banished).Any(card =>
                    card.IsCode(CardId.AlbionTheBrandedDragon,
                        CardId.DragonThatDevoursTheDogma));

            if (canUseEcclesiaNow)
                result.Add(CardId.EcclesiaAndTheDarkDragon);

            if (Bot.HasInDeck(CardId.TheFallenAndTheVirtuous))
            {
                result.Add(CardId.DragonThatDevoursTheDogma);
            }

            if (!result.Contains(CardId.EcclesiaAndTheDarkDragon))
                result.Add(CardId.EcclesiaAndTheDarkDragon);
            if (!result.Contains(CardId.AlbionTheBrandedDragon))
                result.Add(CardId.AlbionTheBrandedDragon);
            if (!result.Contains(CardId.DragonThatDevoursTheDogma))
                result.Add(CardId.DragonThatDevoursTheDogma);
            return result.ToArray();
        }

        private IList<ClientCard> SelectLemnisgateRecycleCards(IList<ClientCard> cards, int min, int max)
        {
            // MDPro4 implements this effect with SelectUnselect: each callback
            // contains only the cards that may be clicked next, not the whole
            // monster/Spell pool. Select one offered card at a time and finish
            // only after the planned number of pairs has been completed.
            if (max <= 1)
            {
                int desiredCount = Math.Max(2, LemnisgateRecyclePairCount * 2);
                if (LemnisgateRecycleSelectionCount >= desiredCount && min == 0)
                {
                    LemnisgateRecycleSelectionCount = 0;
                    return new List<ClientCard>();
                }

                ClientCard next = cards.OrderByDescending(GetLemnisgateRecycleValue)
                    .FirstOrDefault();
                if (next == null)
                {
                    if (min == 0)
                    {
                        LemnisgateRecycleSelectionCount = 0;
                        return new List<ClientCard>();
                    }
                    return null;
                }

                LemnisgateRecycleSelectionCount++;
                return new List<ClientCard> { next };
            }

            List<ClientCard> spells = cards.Where(card => card.IsSpell())
                .OrderByDescending(GetLemnisgateRecycleValue).ToList();
            List<ClientCard> monsters = cards.Where(card => card.IsMonster())
                .OrderByDescending(GetLemnisgateRecycleValue).ToList();
            int maximumPairs = Math.Min(Math.Min(spells.Count, monsters.Count), max / 2);
            int minimumPairs = (min + 1) / 2;
            if (maximumPairs < minimumPairs)
                return null;
            int pairCount = LemnisgateRecyclePairCount > 0
                ? Math.Min(LemnisgateRecyclePairCount, maximumPairs)
                : Math.Min(1, maximumPairs);
            pairCount = Math.Max(minimumPairs, pairCount);
            List<ClientCard> selected = new List<ClientCard>();
            for (int i = 0; i < pairCount; ++i)
            {
                selected.Add(spells[i]);
                selected.Add(monsters[i]);
            }
            int exactCount = pairCount * 2;
            return Util.CheckSelectCount(selected, cards, exactCount, exactCount);
        }

        private int GetLemnisgateRecycleValue(ClientCard card)
        {
            if (card == null)
                return 0;
            if (card.IsMonster())
            {
                if (card.IsCode(CardId.Zero))
                    return 11000;
                if (card.IsCode(CardId.Kagari))
                    return 10000;
                if (card.IsCode(CardId.Shizuku))
                    return 9000;
                if (card.IsCode(CardId.Hayate, CardId.Amatsu))
                    return 8200;
                if (card.IsCode(CardId.Kaina, CardId.Camellia, CardId.Azalea,
                    CardId.Zeke))
                    return 7400;
                if (card.IsCode(CardId.Roze))
                    return Bot.Graveyard.Count(candidate => candidate.IsCode(CardId.Roze)) > 1
                        ? 2200 : 300;
                if (card.IsCode(CardId.Raye))
                    return Bot.Graveyard.Count(candidate => candidate.IsCode(CardId.Raye)) > 1
                        ? 1800 : 100;
                return 1000;
            }

            bool kagariCanRecover = !KagariSummoned && Bot.HasInExtra(CardId.Kagari);
            if (card.IsCode(CardId.Engage))
                return kagariCanRecover ? 2600 : 11000;
            if (card.IsCode(CardId.Linkage))
                return kagariCanRecover && (LinkageOtk != LinkageOtkStage.None ||
                    GetLinkageOtkStarter() != null) ? 2400 : 10000;
            if (card.IsCode(CardId.WidowAnchor))
                return 9000;
            if (card.IsCode(CardId.HornetDrones))
                return 8500;
            if (card.IsCode(CardId.Lemnisgate))
                return 8000;
            if (card.IsCode(CardId.SharkCannon))
                return 7000;
            if (card.IsCode(CardId.Multirole))
                return 6500;
            return 4000;
        }

        private IList<ClientCard> SelectLemnisgateBounceTargets(
            IList<ClientCard> cards, int min, int max)
        {
            List<ClientCard> ordered = cards.OrderByDescending(GetLemnisgateBounceValue)
                .ToList();
            int usefulCount = ordered.Count(card => GetLemnisgateBounceValue(card) >= 2500);
            int desired = Math.Min(max, Math.Max(min, usefulCount));
            IList<ClientCard> selected = Util.CheckSelectCount(ordered, cards,
                desired, desired);
            if (selected != null)
            {
                foreach (ClientCard target in selected.Where(card => card.Controller == 1))
                    MarkCurrentChainHandled(target);
            }
            return selected;
        }

        private int[] GetLinkageExtraDeckPriority()
        {
            List<int> result = new List<int>();
            if (LinkageOtk == LinkageOtkStage.SummonKagari)
                return Bot.HasInExtra(CardId.Kagari)
                    ? new[] { CardId.Kagari }
                    : new int[0];
            if (LinkageOtk == LinkageOtkStage.SummonZero)
                return Bot.HasInExtra(CardId.Zero)
                    ? new[] { CardId.Zero }
                    : new int[0];
            if (LinkageSetupToZeroPending)
                return Bot.HasInExtra(CardId.Zero)
                    ? new[] { CardId.Zero }
                    : new int[0];

            bool battle = Duel.Phase > DuelPhase.Main1 && Duel.Phase < DuelPhase.Main2;
            if (battle && !KagariSummoned && Bot.HasInGraveyard(new[]
            {
                CardId.Linkage,
                CardId.Engage,
                CardId.WidowAnchor
            }))
                result.Add(CardId.Kagari);
            if (!ZeroSummoned)
                result.Add(CardId.Zero);
            if (battle && !HayateSummoned)
                result.Add(CardId.Hayate);
            if (!AmatsuSummoned)
                result.Add(CardId.Amatsu);
            if (!KagariSummoned)
                result.Add(CardId.Kagari);
            if (!ShizukuSummoned)
                result.Add(CardId.Shizuku);
            if (!KainaSummoned)
                result.Add(CardId.Kaina);
            result.AddRange(new[]
            {
                CardId.Azalea,
                CardId.Zeke,
                CardId.Camellia,
                CardId.Hayate
            });
            return result.Distinct().Where(Bot.HasInExtra).ToArray();
        }

        private int[] GetLemnisgateExtraDeckPriority()
        {
            List<int> result = new List<int>();
            bool battle = Duel.Phase > DuelPhase.Main1 && Duel.Phase < DuelPhase.Main2;
            if (Duel.Player == 1)
            {
                if (battle)
                    result.Add(CardId.Kaina);
                result.Add(CardId.Amatsu);
                result.Add(CardId.Kaina);
            }
            else if (battle)
            {
                result.Add(CardId.Zero);
                if (!KagariSummoned && Bot.Graveyard.Any(card => card.IsSpell()))
                    result.Add(CardId.Kagari);
                result.Add(CardId.Amatsu);
                result.Add(CardId.Hayate);
                result.Add(CardId.Kaina);
            }
            else if (Duel.Phase == DuelPhase.Main2 || Duel.Phase == DuelPhase.End)
            {
                result.Add(CardId.Shizuku);
                result.Add(CardId.Kaina);
                result.Add(CardId.Amatsu);
            }
            else
            {
                if (!KagariSummoned && Bot.Graveyard.Any(card => card.IsCode(
                    CardId.Engage, CardId.Linkage, CardId.WidowAnchor,
                    CardId.HornetDrones)))
                    result.Add(CardId.Kagari);
                result.Add(CardId.Zero);
                result.Add(CardId.Hayate);
                result.Add(CardId.Amatsu);
            }
            result.AddRange(new[]
            {
                CardId.Shizuku,
                CardId.Kagari,
                CardId.Zero,
                CardId.Camellia,
                CardId.Zeke,
                CardId.Hayate,
                CardId.Amatsu,
                CardId.Kaina
            });
            List<ClientCard> azaleaMaterials = GetModernLinkTwoMaterials(card =>
                card.HasAttribute(CardAttribute.Light | CardAttribute.Dark), false);
            if (GetBestAzaleaDestroyTarget() != null ||
                CanUseAzaleaAsLinkageBridge(azaleaMaterials))
                result.Add(CardId.Azalea);
            return result.Distinct().Where(Bot.HasInExtra).ToArray();
        }

        private List<ClientCard> GetModernLinkTwoMaterials(Func<ClientCard, bool> filter, bool requireAceLink)
        {
            List<ClientCard> candidates = Bot.GetMonsters()
                .Where(card => card.IsFaceup() && !card.IsCode(CardId.Zero) && filter(card))
                .OrderBy(card => ControlledEnemyCards.Contains(card) || card.Controller == 1 ? 0 : 1)
                .ThenBy(GetLinkMaterialValue)
                .ToList();
            for (int i = 0; i < candidates.Count; ++i)
            {
                for (int j = i + 1; j < candidates.Count; ++j)
                {
                    List<ClientCard> materials = new List<ClientCard> { candidates[i], candidates[j] };
                    if (requireAceLink && !materials.Any(IsSkyStrikerAceLink))
                        continue;
                    if (Util.GetBotAvailZonesFromExtraDeck(materials) != 0 &&
                        SkyStrikerLinkPreservesMainMonsterZone(materials, false))
                        return materials;
                }
            }
            return null;
        }

        private bool IsSkyStrikerCard(ClientCard card)
        {
            return card != null && (card.HasSetcode(0x115) || card.HasSetcode(0x1115) ||
                card.IsCode(CardId.Raye, CardId.Roze, CardId.Kagari, CardId.Shizuku,
                    CardId.Hayate, CardId.Kaina, CardId.Zero, CardId.Camellia, CardId.Amatsu,
                    CardId.Azalea, CardId.Zeke, CardId.Engage, CardId.Linkage,
                    CardId.Lemnisgate, CardId.HornetDrones, CardId.WidowAnchor,
                    CardId.SharkCannon));
        }

        private void ObserveSkyStrikerMirror(ClientCard card, bool isPublic)
        {
            if (SkyStrikerMirrorObserved || !isPublic || card == null || card.Id == 0 ||
                !IsSkyStrikerCard(card))
                return;
            SkyStrikerMirrorObserved = true;
            Logger.DebugWriteLine("[SkyStriker2026] Public Sky Striker card observed; " +
                "mirror policy enabled by " + (card.Name ?? card.Id.ToString()));
        }

        private bool IsPublicOpponentLocation(ClientCard card, int location)
        {
            if (card == null || card.Id == 0)
                return false;
            if ((location & (int)CardLocation.Grave) != 0)
                return true;
            if ((location & (int)CardLocation.Removed) != 0)
                return card.IsFaceup();
            if ((location & ((int)CardLocation.MonsterZone | (int)CardLocation.SpellZone)) != 0)
                return card.IsFaceup();
            return false;
        }

        private ClientCard GetMirrorPriorityEnemySpell()
        {
            if (!SkyStrikerMirrorObserved)
                return null;
            return Enemy.GetSpells()
                .Where(card => card.IsFaceup() && !IsCurrentChainHandled(card) &&
                    card.IsCode(CardId.Multirole, CardId.AreaZero))
                .OrderBy(card => card.IsCode(CardId.Multirole) ? 0 : 1)
                .FirstOrDefault();
        }

        private int GetMirrorMonsterPriority(ClientCard card)
        {
            if (card == null)
                return 0;
            if (card.IsCode(CardId.Raye))
                return 8;
            if (card.IsCode(CardId.Kagari))
                return 7;
            if (card.IsCode(CardId.Shizuku))
                return 6;
            if (card.IsCode(CardId.Hayate))
                return 5;
            if (card.IsCode(CardId.Kaina))
                return 4;
            if (card.IsCode(CardId.Camellia, CardId.Azalea, CardId.Zeke))
                return 3;
            if (card.IsCode(CardId.Roze))
                return 2;
            return 1;
        }

        private bool IsSkyStrikerAce(ClientCard card)
        {
            return card != null && card.IsMonster() && (card.HasSetcode(0x1115) ||
                card.IsCode(CardId.Raye, CardId.Roze, CardId.Token, CardId.Kagari,
                    CardId.Shizuku, CardId.Hayate, CardId.Kaina, CardId.Zero, CardId.Camellia,
                    CardId.Amatsu, CardId.Azalea, CardId.Zeke));
        }

        private bool IsSkyStrikerAceLink(ClientCard card)
        {
            return IsSkyStrikerAce(card) && card.HasType(CardType.Link);
        }

        private int GetCardUtility(ClientCard card)
        {
            if (card == null)
                return 0;
            int result = Math.Max(0, card.GetAttackPower());
            if (card.IsFloodgate())
                result += 5000;
            if (card.HasType(CardType.Link))
                result += card.LinkCount * 500;
            if (card.IsFacedown())
                result = Math.Max(result, 500);
            return result;
        }

        private int GetFieldResourceValue(ClientCard card)
        {
            if (card == null)
                return int.MaxValue;
            if (ControlledEnemyCards.Contains(card) || card.Controller == 1)
                return 0;
            if (card.IsCode(CardId.Token))
                return 10;
            if (card.IsCode(CardId.RadiantTyphoonVision, CardId.UpstartGoblin,
                CardId.PotOfDesires, CardId.ReinforcementOfTheArmy))
                return 50;
            int reserve = GetStrategicReserveValue(card);
            if (reserve > 0)
                return reserve;
            return 500 + GetCardUtility(card);
        }

        private int GetGraveResourceValue(ClientCard card)
        {
            if (card == null)
                return int.MaxValue;
            int reserve = GetStrategicReserveValue(card);
            if (reserve > 0)
                return reserve;
            return GetCardUtility(card);
        }

        private int GetHandResourceValue(ClientCard card)
        {
            if (card == null)
                return int.MaxValue;
            int copies = Bot.Hand.Count(candidate => candidate.IsCode(card.Id));
            if (copies > 1)
                return 100;
            int reserve = GetStrategicReserveValue(card);
            if (reserve > 0)
                return reserve;
            return card.IsMonster() ? Math.Max(100, card.Attack) : 500;
        }

        private int GetDropletCostValue(ClientCard card)
        {
            if (card == null)
                return int.MaxValue;
            if (ControlledEnemyCards.Contains(card))
                return 0;
            if (card.Location == CardLocation.SpellZone && card.IsFaceup() && card != Card &&
                !card.HasType(CardType.Continuous | CardType.Equip | CardType.Field))
                return 25;
            int value = card.Location == CardLocation.Hand
                ? GetHandResourceValue(card) : GetFieldResourceValue(card);
            ClientCard last = Util.GetLastChainCard();
            bool matchesResponseType = Duel.LastChainPlayer == 1 && last != null &&
                (last.IsMonster() && card.IsMonster() || last.IsSpell() && card.IsSpell() ||
                last.HasType(CardType.Trap) && card.HasType(CardType.Trap));
            if (value < 5000 && matchesResponseType)
                value = Math.Max(0, value - 1000);
            return value;
        }

        private int GetEnemyGraveResourceValue(ClientCard card)
        {
            if (card == null)
                return 0;
            return card.Controller == 1 ? 5000 + GetCardUtility(card) : -GetGraveResourceValue(card);
        }

        private List<ClientCard> GetLinkMaterials(int linkRating, int minCount, int maxCount,
            bool effectOnly, bool uniqueNames, ClientCard preservedCard = null)
        {
            List<ClientCard> candidates = Bot.GetMonsters()
                .Where(card => card.IsFaceup() && (!effectOnly || card.HasType(CardType.Effect)) &&
                    card != preservedCard && !card.IsCode(CardId.BorrelswordDragon))
                .OrderBy(GetLinkMaterialValue)
                .ToList();

            List<ClientCard> best = null;
            int bestScore = int.MaxValue;
            int subsetCount = 1 << candidates.Count;
            for (int mask = 1; mask < subsetCount; mask++)
            {
                List<ClientCard> selected = new List<ClientCard>();
                for (int i = 0; i < candidates.Count; i++)
                {
                    if ((mask & (1 << i)) != 0)
                        selected.Add(candidates[i]);
                }
                if (selected.Count < minCount || selected.Count > maxCount)
                    continue;
                if (uniqueNames && selected.Select(card => card.Id).Distinct().Count() != selected.Count)
                    continue;
                if (!Util.CanMakeLinkRating(selected, linkRating) ||
                    Util.GetBotAvailZonesFromExtraDeck(selected) == 0)
                    continue;

                int score = selected.Sum(GetLinkMaterialValue);
                if (score < bestScore)
                {
                    best = selected;
                    bestScore = score;
                }
            }
            return best;
        }

        private int GetLinkMaterialValue(ClientCard card)
        {
            if (ControlledEnemyCards.Contains(card) || card.Controller == 1 ||
                card.IsCode(CardId.Token, CardId.GlowUpBulb))
                return 0;
            if (card.IsCode(CardId.Linkuriboh))
                return 100;
            if (card.IsCode(CardId.AshBlossom, CardId.GhostOgre, CardId.EffectVeiler))
                return 200;
            if (card.IsCode(CardId.Zero))
            {
                if (PreserveEmptyMainZoneForZero || !card.Attacked)
                    return 6000;
                return 2400;
            }
            if (card.IsCode(CardId.Kagari, CardId.Hayate, CardId.Shizuku,
                CardId.Kaina, CardId.Camellia, CardId.Amatsu, CardId.Azalea, CardId.Zeke))
                return (card.Attacked ? 1300 : 2100) + card.LinkCount * 100;
            return Math.Max(300, card.Attack) + card.LinkCount * 100;
        }

        private bool SetHornetDrones()
        {
            return Util.IsTurn1OrMain2() && (SetWhenHandIsFull() || Bot.HasInMonstersZone(CardId.TopologicBomberDragon, true));
        }

        private bool SetLinkage()
        {
            return Util.IsTurn1OrMain2() && Bot.GetSpellCountWithoutField() < 4 &&
                (Bot.Hand.Count(card => card.IsCode(CardId.Linkage)) >= 2 || SetWhenHandIsFull());
        }

        private bool SetWhenHandIsFull()
        {
            return Bot.GetSpellCountWithoutField() < 4 && Bot.Hand.Count > 6;
        }

        private bool EmptyMainMonsterZone()
        {
            return Bot.GetMonstersInMainZone().Count == 0;
        }

        private bool HasPlannedSkyStrikerSpell()
        {
            return Bot.Hand.Concat(Bot.GetSpells()).Any(card =>
                    card.IsSpell() && IsSkyStrikerCard(card)) ||
                ZeroSearchPending || LinkageOtk != LinkageOtkStage.None ||
                HornetSetupStage != HornetZeroSetupStage.None;
        }

        private bool SkyStrikerLinkPreservesMainMonsterZone(
            IList<ClientCard> materials, bool trackedImmediateClearance)
        {
            if (!HasPlannedSkyStrikerSpell() || trackedImmediateClearance)
                return true;
            if (materials == null || materials.Count == 0)
                return false;

            bool leavesMainMonster = Bot.GetMonstersInMainZone().Any(card =>
                !materials.Contains(card));
            bool leavesExistingExtraMonster = Bot.GetMonsters().Any(card =>
                card.Sequence >= 5 && !materials.Contains(card));
            return !leavesMainMonster && !leavesExistingExtraMonster;
        }

        private ClientCard GetSafeSkyStrikerLinkOneMaterial(
            Func<ClientCard, bool> filter, bool trackedImmediateClearance)
        {
            return Bot.GetMonsters()
                .Where(card => card.IsFaceup() && filter(card) &&
                    Util.GetBotAvailZonesFromExtraDeck(new[] { card }) != 0 &&
                    SkyStrikerLinkPreservesMainMonsterZone(
                        new[] { card }, trackedImmediateClearance))
                .OrderBy(GetLinkMaterialValue)
                .FirstOrDefault();
        }

        private void SelectSkyStrikerLinkPlace()
        {
            if (HasPlannedSkyStrikerSpell())
                AI.SelectPlace(Zones.ExtraMonsterZones);
        }

        private int GetSpellCountInGrave()
        {
            return Bot.GetGraveyardSpells().Count;
        }

        private bool HaveThreeSpellsInGrave()
        {
            return GetSpellCountInGrave() >= 3;
        }
    }
}

