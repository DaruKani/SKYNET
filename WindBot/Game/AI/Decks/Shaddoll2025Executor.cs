using System;
using System.Collections.Generic;
using System.Linq;
using YGOSharp.OCGWrapper;
using YGOSharp.OCGWrapper.Enums;
using WindBot.Game;
using WindBot.Game.AI;

namespace WindBot.Game.AI.Decks
{
    [Deck("Shaddoll2025", "AI_Shaddoll2025")]
    public class Shaddoll2025Executor : DefaultExecutor
    {
        public static class CardId
        {
            public const int ReeshaddollWendi = 51023024;
            public const int TohushaddollGrysta = 95072744;
            public const int ShaddollBeast = 3717252;
            public const int ShaddollDragon = 77723643;
            public const int ShaddollSquamata = 30328508;
            public const int BlazingCartesia = 95515789;
            public const int MulcharmyPurulia = 84192580;
            public const int MulcharmyFuwalos = 42141493;
            public const int ShaddollHedgehog = 4939890;
            public const int NaelshaddollAriel = 97518132;
            public const int DrollLockBird = 94145021;
            public const int EffectVeiler = 97268402;
            public const int VoidUnleashing = 61345801;
            public const int ElShaddollFusion = 6417578;
            public const int FusionDeployment = 6498706;
            public const int LevShaddollFusion = 34950192;
            public const int ShaddollFusion = 44394295;
            public const int ShaddollSchism = 21011044;
            public const int DracotailFaimena = 1498449;
            public const int TearlamentsScheiren = 572850;
            public const int MaxxC = 23434538;
            public const int ReshShaddollIncarnation = 23912837;
            public const int ArmageddonKnight = 28985331;
            public const int AshBlossomJoyousSpring = 14558127;

            public const int ElShaddollMeshachrer = 32467459;
            public const int ElShaddollShekhinaga = 74822425;
            public const int ElShaddollAnoyatyllis = 19261966;
            public const int ElShaddollConstruct = 20366274;
            public const int GranguignolDuskDragon = 24915933;
            public const int ElShaddollApkallone = 50907446;
            public const int LunalightPerfumeDancer = 81196066;
            public const int ReeshaddollWendikuruhu = 8852158;
            public const int ElShaddollWinda = 94977269;
            public const int InfernoidEvil = 97051536;
            public const int ShaddollConstruct = 86938484;

            public const int WhiteStoneOfAncients = 71039903;
            public const int BlueEyesSpiritDragon = 59822133;
            public const int ReturnOfTheDragonLords = 6853254;
            public const int DarkMagician = 46986414;
            public const int DarkMagicianTheDragonKnight = 41721210;
            public const int MagicianNavigation = 7922915;

            public const int OrcustKnightmare = 4055337;
            public const int OrcustHarpHorror = 57835716;
            public const int OrcustCymbalSkeleton = 21441617;
            public const int WorldLegacyWorldWand = 93920420;
            public const int OrcustratedClimax = 703897;
            public const int PhantomKnightsFogBlade = 25542642;
            public const int DingirsuOrcustEveningStar = 93854893;
        }

        private const int ShaddollSetcode = 0x9d;

        private enum LevRouteStage
        {
            None,
            ApkalloneSent,
            SquamataDiscarded,
            TohushaddollSent,
            MeshachrerSummoned,
            VoidSearched,
            ConstructSummoned,
            HedgehogSent,
            WendiReady,
            ControlSpellsReady,
            WendikuruhuSummoned,
            ControlShellPrepared
        }

        [Flags]
        private enum OpponentCapability
        {
            None = 0,
            Negate = 1,
            Removal = 2,
            Recursion = 4,
            Protection = 8,
            Floodgate = 16,
            QuickInteraction = 32,
            GraveInteraction = 64,
            RepeatableEngine = 128,
            AdvantageEngine = 256
        }

        private readonly HashSet<int> _usedEffects = new HashSet<int>();
        private int _specialSummonsThisTurn;
        private int _opponentSpecialSummonsThisTurn;
        private int _coveredOpponentChainLength;
        private int _pendingFusionTargetId;
        private CardType _voidRevealedType;
        private int _voidDiscardedMonsterId;
        private bool _shaddollExtraDeckLock;
        private bool _levTributedMesh;
        private bool _levOpponentTurnBranch;
        private bool _levMeshSearchResolved;
        private bool _waitingForLevWendi;
        private ClientCard _preferredSpiritProtectedTrigger;
        private LevRouteStage _levRouteStage;

        private static readonly int[] ShaddollMainMonsterIds =
        {
            CardId.ShaddollSquamata,
            CardId.TohushaddollGrysta,
            CardId.ShaddollHedgehog,
            CardId.ReeshaddollWendi,
            CardId.NaelshaddollAriel,
            CardId.ShaddollDragon,
            CardId.ShaddollBeast
        };

        private static readonly int[] ShaddollSpellTrapIds =
        {
            CardId.LevShaddollFusion,
            CardId.ShaddollFusion,
            CardId.ElShaddollFusion,
            CardId.ShaddollSchism,
            CardId.ReshShaddollIncarnation
        };

        private static readonly int[] ShaddollFusionIds =
        {
            CardId.ElShaddollMeshachrer,
            CardId.ElShaddollConstruct,
            CardId.ElShaddollApkallone,
            CardId.ElShaddollWinda,
            CardId.ElShaddollShekhinaga,
            CardId.ElShaddollAnoyatyllis,
            CardId.ReeshaddollWendikuruhu
        };

        public Shaddoll2025Executor(GameAI ai, Duel duel)
            : base(ai, duel)
        {
            // Opponent-turn cards are evaluated first. Each predicate declines
            // redundant responses when the current chain is already covered.
            AddExecutor(ExecutorType.Activate, CardId.MaxxC, DefaultMaxxC);
            AddExecutor(ExecutorType.Activate, CardId.MulcharmyFuwalos, MulcharmyActivate);
            AddExecutor(ExecutorType.Activate, CardId.MulcharmyPurulia, MulcharmyActivate);
            AddExecutor(ExecutorType.Activate, CardId.DrollLockBird, DrollActivate);
            AddExecutor(ExecutorType.Activate, CardId.AshBlossomJoyousSpring,
                DefaultAshBlossomAndJoyousSpring);
            AddExecutor(ExecutorType.Activate, CardId.EffectVeiler, EffectVeilerActivate);
            AddExecutor(ExecutorType.Activate, CardId.ElShaddollShekhinaga, ShekhinagaActivate);
            AddExecutor(ExecutorType.Activate, CardId.ReeshaddollWendikuruhu, WendikuruhuActivate);
            AddExecutor(ExecutorType.Activate, CardId.ShaddollSchism, SchismActivate);
            AddExecutor(ExecutorType.Activate, CardId.ReshShaddollIncarnation, ReshActivate);
            // Route actions use separate Lev executors so the real idle-command
            // scheduler cannot skip a stage. Lev starts first, Mesh searches,
            // Void converts the reveal, and only then may Lev tribute Mesh.
            AddExecutor(ExecutorType.Activate, CardId.LevShaddollFusion, LevPrimaryActivate);
            AddExecutor(ExecutorType.Activate, CardId.ElShaddollMeshachrer, MeshachrerActivate);
            AddExecutor(ExecutorType.Activate, CardId.VoidUnleashing, VoidUnleashingActivate);
            AddExecutor(ExecutorType.Activate, CardId.LevShaddollFusion, LevTributeActivate);

            // Shaddoll and Fusion trigger effects. Selection is owned by the
            // actual resolving chain link. Keep them ahead of optional Quick
            // Fusions so Construct -> Hedgehog finishes the Wendi search before
            // El Shaddoll Fusion is allowed to consume the control materials.
            AddExecutor(ExecutorType.Activate, CardId.ShaddollSquamata, SquamataActivate);
            AddExecutor(ExecutorType.Activate, CardId.ShaddollHedgehog, HedgehogActivate);
            AddExecutor(ExecutorType.Activate, CardId.ReeshaddollWendi, WendiActivate);
            AddExecutor(ExecutorType.Activate, CardId.ShaddollDragon, DragonActivate);
            AddExecutor(ExecutorType.Activate, CardId.ShaddollBeast, BeastActivate);
            AddExecutor(ExecutorType.Activate, CardId.NaelshaddollAriel, ArielActivate);
            AddExecutor(ExecutorType.Activate, CardId.TohushaddollGrysta, TohushaddollGrystaActivate);
            AddExecutor(ExecutorType.Activate, CardId.ElShaddollConstruct, ConstructActivate);
            AddExecutor(ExecutorType.Activate, CardId.ElShaddollApkallone, ApkalloneActivate);
            AddExecutor(ExecutorType.Activate, CardId.ElShaddollWinda, FusionRecoveryActivate);
            AddExecutor(ExecutorType.Activate, CardId.ElShaddollAnoyatyllis, FusionRecoveryActivate);
            AddExecutor(ExecutorType.Activate, CardId.InfernoidEvil, InfernoidEvilActivate);
            AddExecutor(ExecutorType.Activate, CardId.LunalightPerfumeDancer, PerfumeDancerActivate);
            AddExecutor(ExecutorType.Activate, CardId.GranguignolDuskDragon, GranguignolActivate);

            // Flexible Fusion effects and extenders are intentionally after the
            // committed Lev route and its triggers. They remain available on the
            // opponent's turn, where Lev's ignition effects are not legal offers.
            AddExecutor(ExecutorType.Activate, CardId.ElShaddollFusion, ElShaddollFusionActivate);
            AddExecutor(ExecutorType.Activate, CardId.DracotailFaimena, FaimenaActivate);
            AddExecutor(ExecutorType.Activate, CardId.BlazingCartesia, CartesiaActivate);
            AddExecutor(ExecutorType.Activate, CardId.FusionDeployment, FusionDeploymentActivate);
            AddExecutor(ExecutorType.Activate, CardId.ShaddollFusion, ShaddollFusionActivate);
            AddExecutor(ExecutorType.Activate, CardId.TearlamentsScheiren, ScheirenActivate);
            AddExecutor(ExecutorType.Activate, CardId.ArmageddonKnight, ArmageddonKnightActivate);

            // Establish a flip body only after the non-Normal-Summon starters.
            AddExecutor(ExecutorType.Summon, CardId.ArmageddonKnight, ArmageddonKnightSummon);
            AddExecutor(ExecutorType.MonsterSet, CardId.ShaddollSquamata, SetShaddollMonster);
            AddExecutor(ExecutorType.MonsterSet, CardId.ShaddollDragon, SetShaddollMonster);
            AddExecutor(ExecutorType.MonsterSet, CardId.ReeshaddollWendi, SetShaddollMonster);
            AddExecutor(ExecutorType.MonsterSet, CardId.ShaddollHedgehog, SetShaddollMonster);
            AddExecutor(ExecutorType.MonsterSet, CardId.NaelshaddollAriel, SetShaddollMonster);
            AddExecutor(ExecutorType.MonsterSet, CardId.ShaddollBeast, SetShaddollMonster);
            AddExecutor(ExecutorType.MonsterSet, CardId.TohushaddollGrysta, SetShaddollMonster);

            AddExecutor(ExecutorType.SpellSet, SetSpellTrap);
            AddExecutor(ExecutorType.Repos, ShaddollMonsterRepos);
        }

        public override bool OnSelectHand()
        {
            return true;
        }

        public override void OnNewTurn()
        {
            base.OnNewTurn();
            _usedEffects.Clear();
            _specialSummonsThisTurn = 0;
            _opponentSpecialSummonsThisTurn = 0;
            _coveredOpponentChainLength = 0;
            _pendingFusionTargetId = 0;
            _voidRevealedType = 0;
            _voidDiscardedMonsterId = 0;
            _shaddollExtraDeckLock = false;
            _levTributedMesh = false;
            _levOpponentTurnBranch = false;
            _levMeshSearchResolved = false;
            _waitingForLevWendi = false;
            _preferredSpiritProtectedTrigger = null;
            _levRouteStage = LevRouteStage.None;
        }

        public override void OnSelectChain(IList<ClientCard> cards)
        {
            base.OnSelectChain(cards);
            _preferredSpiritProtectedTrigger = null;
            if (cards == null || !Enemy.GetMonsters().Any(c => c.IsFaceup() &&
                !c.IsDisabled() && c.IsCode(CardId.BlueEyesSpiritDragon)))
                return;
            List<ClientCard> graveTriggers = cards.Where(c => c != null &&
                c.Controller == 0 && c.Location == CardLocation.Grave &&
                IsSpiritOrderableGraveTrigger(c)).ToList();
            if (graveTriggers.Count > 1)
                _preferredSpiritProtectedTrigger = graveTriggers
                    .OrderByDescending(SpiritProtectedTriggerScore)
                    .First();
        }

        public override void OnMove(
            ClientCard card,
            int previousControler,
            int previousLocation,
            int currentControler,
            int currentLocation)
        {
            if (card != null && currentControler == 0)
            {
                ChainInfo solving = Duel.GetCurrentSolvingChainInfo();
                if (solving != null && solving.ActivatePlayer == 0)
                {
                    if (solving.IsActivateCode(CardId.LevShaddollFusion) &&
                        card.IsCode(CardId.ElShaddollApkallone) &&
                        previousLocation == (int)CardLocation.Extra &&
                        currentLocation == (int)CardLocation.Grave)
                        AdvanceLevRoute(LevRouteStage.ApkalloneSent);
                    else if (solving.IsActivateCode(CardId.ElShaddollApkallone) &&
                        card.IsCode(CardId.ShaddollSquamata) &&
                        currentLocation == (int)CardLocation.Grave)
                        AdvanceLevRoute(LevRouteStage.SquamataDiscarded);
                    else if (solving.IsActivateCode(CardId.ShaddollSquamata) &&
                        card.IsCode(CardId.TohushaddollGrysta) &&
                        currentLocation == (int)CardLocation.Grave)
                        AdvanceLevRoute(LevRouteStage.TohushaddollSent);
                    else if (solving.IsActivateCode(CardId.TohushaddollGrysta) &&
                        card.IsCode(CardId.ElShaddollMeshachrer) &&
                        currentLocation == (int)CardLocation.MonsterZone)
                        AdvanceLevRoute(LevRouteStage.MeshachrerSummoned);
                    else if (solving.IsActivateCode(CardId.ElShaddollMeshachrer) &&
                        currentLocation == (int)CardLocation.Hand)
                    {
                        _levMeshSearchResolved = true;
                        if (card.IsCode(CardId.VoidUnleashing))
                            AdvanceLevRoute(LevRouteStage.VoidSearched);
                    }
                    else if (solving.IsActivateCode(CardId.LevShaddollFusion) &&
                        card.IsCode(CardId.ElShaddollConstruct) &&
                        currentLocation == (int)CardLocation.MonsterZone)
                        AdvanceLevRoute(LevRouteStage.ConstructSummoned);
                    else if (solving.IsActivateCode(CardId.ElShaddollConstruct) &&
                        card.IsCode(CardId.ShaddollHedgehog) &&
                        currentLocation == (int)CardLocation.Grave)
                    {
                        _waitingForLevWendi = true;
                        AdvanceLevRoute(LevRouteStage.HedgehogSent);
                    }
                    else if (solving.IsActivateCode(CardId.ShaddollHedgehog) &&
                        card.IsCode(CardId.ReeshaddollWendi) &&
                        currentLocation == (int)CardLocation.Hand)
                    {
                        _waitingForLevWendi = false;
                        AdvanceLevRoute(LevRouteStage.WendiReady);
                    }
                    else if (solving.IsActivateCode(CardId.VoidUnleashing) &&
                        card.IsCode(CardId.ElShaddollFusion, CardId.ShaddollSchism) &&
                        currentLocation == (int)CardLocation.Hand &&
                        Bot.HasInHandOrInSpellZone(CardId.ElShaddollFusion) &&
                        Bot.HasInHandOrInSpellZone(CardId.ShaddollSchism))
                        AdvanceLevRoute(LevRouteStage.ControlSpellsReady);
                    else if (solving.IsActivateCode(CardId.ElShaddollFusion) &&
                        card.IsCode(CardId.ReeshaddollWendikuruhu) &&
                        currentLocation == (int)CardLocation.MonsterZone)
                        AdvanceLevRoute(LevRouteStage.WendikuruhuSummoned);
                    else if (solving.IsActivateCode(CardId.NaelshaddollAriel) &&
                        card.IsCode(CardId.ShaddollSquamata) &&
                        currentLocation == (int)CardLocation.MonsterZone)
                        AdvanceLevRoute(LevRouteStage.ControlShellPrepared);
                }
            }
            base.OnMove(card, previousControler, previousLocation, currentControler, currentLocation);
        }

        public override void OnChainEnd()
        {
            base.OnChainEnd();
            // If Hedgehog resolved without adding Wendi (negated or no longer
            // available), release the planned wait so legal fallback Fusions
            // can be reconsidered instead of deadlocking the turn.
            if (_levRouteStage == LevRouteStage.HedgehogSent &&
                EffectUsed(CardId.ShaddollHedgehog) &&
                !Bot.HasInHand(CardId.ReeshaddollWendi))
                _levRouteStage = LevRouteStage.ConstructSummoned;
            if (_waitingForLevWendi && EffectUsed(CardId.ShaddollHedgehog) &&
                !Bot.HasInHand(CardId.ReeshaddollWendi))
                _waitingForLevWendi = false;
            _coveredOpponentChainLength = 0;
            _pendingFusionTargetId = 0;
            _voidRevealedType = 0;
            _levTributedMesh = false;
        }

        public override void OnSpSummoned()
        {
            if (Duel.LastSummonPlayer == 0)
                _specialSummonsThisTurn++;
            else if (Duel.LastSummonPlayer == 1)
                _opponentSpecialSummonsThisTurn++;
            base.OnSpSummoned();
        }

        public override IList<ClientCard> OnSelectCard(
            IList<ClientCard> cards, int min, int max, int hint, bool cancelable)
        {
            if (cards == null || cards.Count == 0)
                return base.OnSelectCard(cards, min, max, hint, cancelable);

            // SelectSubGroup issues repeated SelectUnselect packets after the
            // original chain owner is no longer available through PromptIs.
            // _voidRevealedType stays live until chain end and identifies every
            // search/discard step of the same Void resolution.
            if (PromptIs(CardId.VoidUnleashing) || _voidRevealedType != 0)
            {
                if (hint == HintMsg.AddToHand)
                    return SelectVoidSearch(cards, min, max);
                if (hint == HintMsg.Discard)
                {
                    IList<ClientCard> selected = SelectByScore(cards, min, max, VoidDiscardScore);
                    ClientCard discarded = selected.FirstOrDefault();
                    _voidDiscardedMonsterId = discarded != null && IsShaddollMonster(discarded)
                        ? discarded.Id : 0;
                    return selected;
                }
            }

            if (PromptIs(CardId.LevShaddollFusion))
            {
                if (hint == HintMsg.ToGrave && cards.All(c => c.Location == CardLocation.Extra))
                    return SelectByScore(cards, min, max, LevDumpScore);
                if (hint == HintMsg.SpSummon)
                    return SelectFusionTarget(cards, min, max, CardId.LevShaddollFusion);
            }

            if (PromptIs(CardId.FusionDeployment))
            {
                if (hint == HintMsg.Confirm)
                    return SelectIds(cards, min, max, CardId.GranguignolDuskDragon);
                if (hint == HintMsg.SpSummon)
                    return SelectIds(cards, min, max, CardId.BlazingCartesia);
            }

            if (IsFusionPromptOwner())
            {
                if (hint == HintMsg.SpSummon && cards.Any(c => c.Location == CardLocation.Extra))
                    return SelectFusionTarget(cards, min, max, GetPromptOwnerId());
                if (hint == HintMsg.FusionMaterial)
                    return SelectByScore(cards, min, max, FusionMaterialScore);
            }

            if (PromptIs(CardId.ElShaddollMeshachrer))
            {
                if (hint == HintMsg.AddToHand)
                    return SelectByScore(cards, min, max, MeshSearchScore);
                if (hint == HintMsg.SpSummon)
                    return SelectByScore(cards, min, max, RevivalScore);
            }

            if (PromptIs(CardId.ElShaddollApkallone))
            {
                if (hint == HintMsg.Disable || hint == HintMsg.Target)
                    return SelectEnemyThreat(cards, min, max);
                if (hint == HintMsg.AddToHand)
                    return SelectByScore(cards, min, max, ApkalloneSearchScore);
                if (hint == HintMsg.Discard)
                    return SelectByScore(cards, min, max, ApkalloneDiscardScore);
            }

            if (PromptIs(CardId.ReshShaddollIncarnation))
            {
                if (hint == HintMsg.SpSummon || hint == HintMsg.Target)
                    return SelectByScore(cards, min, max, RevivalScore);
                if (hint == HintMsg.Remove)
                    return SelectByScore(cards, min, max, ReshCostScore);
                if (hint == HintMsg.PosChange || hint == HintMsg.Position)
                {
                    if (cards.Any(c => c.Controller == 0 && c.IsFacedown()))
                        return SelectByScore(cards, min, max, FlipUpScore);
                    return SelectByScore(cards, min, max, ResetFlipScore);
                }
            }

            if (PromptIs(CardId.InfernoidEvil) && hint == HintMsg.AddToHand)
                return SelectIds(cards, min, max, CardId.VoidUnleashing);

            if (PromptIs(CardId.ElShaddollConstruct) && hint == HintMsg.ToGrave)
                return SelectByScore(cards, min, max, ConstructSendScore);

            if (PromptIs(CardId.GranguignolDuskDragon) && hint == HintMsg.ToGrave)
                return SelectByScore(cards, min, max, LevDumpScore);

            if (PromptIs(CardId.ShaddollSquamata))
            {
                if (hint == HintMsg.ToGrave)
                    return SelectByScore(cards, min, max, SquamataSendScore);
                if (hint == HintMsg.Destroy || hint == HintMsg.Target)
                    return SelectEnemyThreat(cards, min, max);
            }

            if (PromptIs(CardId.ShaddollHedgehog))
            {
                if (hint == HintMsg.AddToHand)
                {
                    bool monstersOnly = cards.All(c => c.IsMonster());
                    return SelectByScore(cards, min, max,
                        monstersOnly ? (Func<ClientCard, int>)HedgehogMonsterSearchScore : ShaddollSearchScore);
                }
            }

            if (PromptIs(CardId.ArmageddonKnight) && hint == HintMsg.ToGrave)
                return SelectByScore(cards, min, max, ArmageddonSendScore);

            if (PromptIs(CardId.ReeshaddollWendi) && hint == HintMsg.SpSummon)
                return SelectByScore(cards, min, max, WendiSummonScore);

            if (PromptIs(CardId.ShaddollDragon) &&
                (hint == HintMsg.Destroy || hint == HintMsg.ReturnToHand || hint == HintMsg.Target))
                return SelectEnemyThreat(cards, min, max);

            if (PromptIs(CardId.NaelshaddollAriel))
            {
                if (hint == HintMsg.Remove)
                    return SelectArielTargets(cards, min, max);
                if (hint == HintMsg.SpSummon)
                    return SelectByScore(cards, min, max, ArielRevivalScore);
            }

            if (PromptIs(CardId.TohushaddollGrysta) && hint == HintMsg.Target)
                return SelectByScore(cards, min, max, FlipCopyScore);

            if (PromptIs(CardId.ReeshaddollWendikuruhu))
            {
                if (hint == HintMsg.PosChange || hint == HintMsg.Position)
                {
                    if (cards.Any(c => c.Controller == 0 && c.IsFacedown()))
                        return SelectByScore(cards, min, max, FlipUpScore);
                    return SelectEnemyThreat(cards, min, max);
                }
                if (hint == HintMsg.AddToHand || hint == HintMsg.Target)
                    return SelectByScore(cards, min, max, ShaddollRecoveryScore);
            }

            if (PromptIs(CardId.ElShaddollShekhinaga) && hint == HintMsg.ToGrave)
                return SelectByScore(cards, min, max, DiscardScore);

            if (IsFusionRecoveryPrompt() && (hint == HintMsg.AddToHand || hint == HintMsg.Target))
                return SelectByScore(cards, min, max, SpellTrapRecoveryScore);

            if (PromptIs(CardId.TearlamentsScheiren) && hint == HintMsg.ToGrave)
                return SelectByScore(cards, min, max, ScheirenSendScore);

            return base.OnSelectCard(cards, min, max, hint, cancelable);
        }

        public override bool OnSelectYesNo(int desc)
        {
            if (desc == Util.GetStringId(CardId.ShaddollSchism, 0))
            {
                if (_pendingFusionTargetId == CardId.ElShaddollWinda || FunctionalWinda())
                    return Enemy.GetMonsters().Any(c => c.IsFaceup() && c.HasAttribute(CardAttribute.Dark));
                NamedCard summoned = NamedCard.Get(_pendingFusionTargetId);
                return summoned != null && Enemy.GetMonsters().Any(c =>
                    c.IsFaceup() && (c.Attribute & summoned.Attribute) != 0);
            }
            if (desc == Util.GetStringId(CardId.ReeshaddollWendikuruhu, 2))
                return Enemy.GetMonsters().Any(c => c.IsFaceup() && !c.HasType(CardType.Link));
            return base.OnSelectYesNo(desc);
        }

        public override int OnSelectOption(IList<int> options)
        {
            int flipUp = options.IndexOf(Util.GetStringId(CardId.ReshShaddollIncarnation, 2));
            if (flipUp >= 0 && Bot.GetMonsters().Any(IsUsefulFacedownFlip))
                return flipUp;
            int reset = options.IndexOf(Util.GetStringId(CardId.ReshShaddollIncarnation, 3));
            if (reset >= 0)
                return reset;
            return base.OnSelectOption(options);
        }

        public override CardPosition OnSelectPosition(int cardId, IList<CardPosition> positions)
        {
            // Lev sets the summoned Fusion's ATK to 0 after this prompt. Keep
            // that combo body out of Attack Position even though card data still
            // contains its printed ATK here.
            if (PromptIs(CardId.LevShaddollFusion) && positions.Contains(CardPosition.FaceUpDefence))
                return CardPosition.FaceUpDefence;
            // Ariel's revived Squamata is Wendikuruhu's required Flip fuel.
            // Leaving it face-up makes that interruption legally unusable on
            // an otherwise empty board.
            if (PromptIs(CardId.NaelshaddollAriel) && cardId == CardId.ShaddollSquamata &&
                Bot.HasInMonstersZone(CardId.ReeshaddollWendikuruhu) &&
                positions.Contains(CardPosition.FaceDownDefence))
                return CardPosition.FaceDownDefence;
            if (ShaddollMainMonsterIds.Contains(cardId) && positions.Contains(CardPosition.FaceDownDefence))
                return CardPosition.FaceDownDefence;

            NamedCard data = NamedCard.Get(cardId);
            if (data != null && positions.Contains(CardPosition.FaceUpDefence))
            {
                int strongestEnemy = Enemy.GetMonsters()
                    .Where(c => c.IsFaceup())
                    .Select(c => c.GetDefensePower())
                    .DefaultIfEmpty(0)
                    .Max();
                int expectedDamage = Math.Max(0, strongestEnemy - data.Attack);
                bool lethalExposure = Bot.LifePoints > 0 && expectedDamage >= Bot.LifePoints;
                bool lowAttackUtility = data.Attack <= 1000 || cardId == CardId.ReeshaddollWendikuruhu;
                if (lethalExposure || lowAttackUtility)
                    return CardPosition.FaceUpDefence;
            }
            if (positions.Contains(CardPosition.FaceUpAttack))
                return CardPosition.FaceUpAttack;
            return base.OnSelectPosition(cardId, positions);
        }

        private bool MulcharmyActivate()
        {
            if (Duel.Player != 1 || Duel.Phase > DuelPhase.Main1 ||
                DefaultCheckWhetherCardIsNegated(Card))
                return false;
            int used = 0;
            if (_usedEffects.Contains(CardId.MulcharmyFuwalos)) used++;
            if (_usedEffects.Contains(CardId.MulcharmyPurulia)) used++;
            if (used >= 2)
                return false;
            _usedEffects.Add(Card.Id);
            return true;
        }

        private bool DrollActivate()
        {
            if (Duel.Player != 1 || DefaultCheckWhetherCardIsNegated(Card))
                return false;
            return !_usedEffects.Contains(CardId.DrollLockBird) && MarkUsed(CardId.DrollLockBird);
        }

        private bool EffectVeilerActivate()
        {
            if (CurrentOpponentChainIsCovered() || !DefaultEffectVeiler())
                return false;
            CoverCurrentOpponentChain();
            return true;
        }

        private bool ShekhinagaActivate()
        {
            if (Card.Location == CardLocation.Grave)
                return FusionRecoveryActivate();
            if (CurrentOpponentChainIsCovered() || !Bot.Hand.Any(IsShaddollCard))
                return false;
            ClientCard last = Util.GetLastChainCard();
            if (Duel.LastChainPlayer != 1 || last == null || last.IsDisabled())
                return false;
            CoverCurrentOpponentChain();
            return MarkUsed(CardId.ElShaddollShekhinaga);
        }

        private bool WendikuruhuActivate()
        {
            if (Card.Location == CardLocation.Grave)
                return CanUseSpiritOrderedTrigger(Card) &&
                    HasRecoverableShaddollCard() && MarkUsed(CardId.ReeshaddollWendikuruhu + 1);
            if (Card.Location != CardLocation.MonsterZone ||
                !Bot.GetMonsters().Any(c => c.IsFacedown() && c.HasType(CardType.Flip)))
                return false;
            bool usefulNow = Enemy.GetMonsters().Any(c => c.IsFaceup() && !c.HasType(CardType.Link));
            // The prepared Winda shell needs this Quick Effect to turn the
            // opponent's one permitted Special Summon face-down. Do not expose
            // the Flip body at an empty opponent board merely because a broad
            // end-of-step timing hint was offered; the recorded Blue-Eyes loss
            // did exactly that before Alternative White Dragon was summoned.
            if (Duel.Player == 1 && FunctionalWinda())
                return usefulNow && MarkUsed(CardId.ReeshaddollWendikuruhu);
            bool mainEnd = CurrentTiming != -1 && (CurrentTiming & 0x200) != 0;
            return (usefulNow || Duel.Player == 0 || mainEnd) && MarkUsed(CardId.ReeshaddollWendikuruhu);
        }

        private bool SchismActivate()
        {
            if (Card.IsFacedown())
            {
                if (Duel.Player != 1 || !IsMainPhase() || !HasSchismFusionResources())
                    return false;
                bool mainEnd = CurrentTiming != -1 && (CurrentTiming & 0x200) != 0;
                bool threatened = DefaultOnBecomeTarget();
                bool exposedControlEngine = HasUrgentPersistentSpellTrapEngine();
                bool preSummonWinda = ShouldEstablishWindaBeforeFirstSummon();
                bool removeMaterial = ShouldUseSchismMaterialRemovalWindow();
                // Winda does not retroactively count a Special Summon made before
                // it became face-up. Establish it proactively whenever Schism has
                // legal Winda materials. If the first summon already happened, use
                // Schism's Attribute-matching send to remove that material instead
                // of pretending a late Winda stops it.
                if (!threatened && !preSummonWinda && !removeMaterial &&
                    ShouldLeadWithElFusion())
                    return false;
                return threatened ||
                    preSummonWinda ||
                    removeMaterial ||
                    exposedControlEngine ||
                    _opponentSpecialSummonsThisTurn > 0 ||
                    (mainEnd && Enemy.GetMonsterCount() > 0);
            }
            if (!IsMainPhase() || !HasSchismFusionResources() ||
                (FunctionalWinda() && _specialSummonsThisTurn >= 1))
                return false;
            if (Duel.Player == 0 && !Enemy.GetMonsters().Any(c => c.IsFaceup()))
                return false;
            if (Duel.Player == 1 && Enemy.GetMonsterCount() == 0 && Duel.CurrentChain.Count == 0)
                return ShouldEstablishWindaBeforeFirstSummon() &&
                    MarkUsed(CardId.ShaddollSchism);
            return MarkUsed(CardId.ShaddollSchism);
        }

        private bool ReshActivate()
        {
            if (DefaultCheckWhetherCardIsNegated(Card))
                return false;
            if (Card.Location == CardLocation.Grave)
            {
                bool hasCost = Bot.Graveyard.Any(c => c != Card && IsShaddollCard(c));
                bool flipNow = Bot.GetMonsters().Any(IsUsefulFacedownFlip);
                bool reset = Bot.GetMonsters().Any(c => c.IsFaceup() && IsShaddollMonster(c) &&
                    !c.IsCode(CardId.ElShaddollWinda) && EffectUsed(c.Id));
                return hasCost && (flipNow || reset) && MarkUsed(CardId.ReshShaddollIncarnation + 1);
            }
            if (Card.Location != CardLocation.SpellZone || Bot.GetMonsterCount() >= 5)
                return false;
            return Bot.Graveyard.Any(c => IsShaddollMonster(c) ||
                c.IsCode(CardId.ElShaddollWinda, CardId.ElShaddollConstruct,
                    CardId.ElShaddollApkallone, CardId.ReeshaddollWendikuruhu)) &&
                MarkUsed(CardId.ReshShaddollIncarnation);
        }

        private bool ElShaddollFusionActivate()
        {
            return CanActivateElShaddollFusionNow();
        }

        private bool CanActivateElShaddollFusionNow()
        {
            if (!HasFusionResources(false) || ShouldStopSpecialSummoning(CardLocation.Extra))
                return false;
            if (MustResolveLevMeshFirst() || MustWaitForLevWendiSearch())
                return false;
            if (Duel.SolvingChainIndex == 0 && Duel.CurrentChain.Count > 0 &&
                Duel.LastChainPlayer == 0)
                return false;
            if (HasSafeLethalOnBoard() || (Duel.Player == 0 && FunctionalWinda()))
                return false;
            if (Duel.Player == 1)
            {
                bool schismPriority = ShouldEstablishWindaBeforeFirstSummon() ||
                    ShouldUseSchismMaterialRemovalWindow();
                bool threatened = Card != null &&
                    Card.IsCode(CardId.ElShaddollFusion) && DefaultOnBecomeTarget();
                bool answerPersistentThreat = Bot.HasInExtra(CardId.ElShaddollApkallone) &&
                    BestPersistentOpponentThreatScore() >= 750 &&
                    (!schismPriority || HasUrgentPersistentSpellTrapEngine());
                bool schismAvailable = Bot.HasInSpellZone(CardId.ShaddollSchism);
                bool mainEnd = CurrentTiming != -1 && (CurrentTiming & 0x200) != 0;
                bool fallbackWindaWindow = !schismAvailable && Enemy.GetMonsterCount() > 0 &&
                    (_opponentSpecialSummonsThisTurn > 0 || mainEnd);
                return threatened || answerPersistentThreat || fallbackWindaWindow;
            }
            return Duel.Phase == DuelPhase.Main1 || Duel.Phase == DuelPhase.Main2 ||
                Duel.Phase == DuelPhase.BattleStart;
        }

        private bool FaimenaActivate()
        {
            if (Card.Location != CardLocation.Hand || !IsMainPhase() ||
                ShouldStopSpecialSummoning(CardLocation.Extra) || FunctionalWinda() || HasSafeLethalOnBoard())
                return false;
            if (MustResolveLevMeshFirst())
                return false;
            if (Duel.SolvingChainIndex == 0 && Duel.CurrentChain.Count > 0 &&
                Duel.LastChainPlayer == 0)
                return false;
            if (Duel.Player == 0 && Enemy.GetMonsterCount() == 0)
                return false;
            // Faimena discards itself as cost before Fusion materials are chosen.
            // Require two other monsters so the turn-zero Quick Fusion is real.
            return Bot.Hand.Concat(Bot.GetMonsters())
                .Count(c => c != Card && c.IsMonster()) >= 2 &&
                Bot.HasInExtra(ShaddollFusionIds);
        }

        private bool CartesiaActivate()
        {
            if (Card.Location == CardLocation.Grave)
                return Duel.Phase == DuelPhase.End;
            if (Card.Location != CardLocation.MonsterZone || !IsMainPhase() ||
                ShouldStopSpecialSummoning(CardLocation.Extra) || FunctionalWinda() || HasSafeLethalOnBoard())
                return false;
            if (MustResolveLevMeshFirst())
                return false;
            if (Duel.SolvingChainIndex == 0 && Duel.CurrentChain.Count > 0 &&
                Duel.LastChainPlayer == 0)
                return false;
            return Bot.Hand.Concat(Bot.GetMonsters()).Count(c => c != null && c.IsMonster()) >= 2 &&
                (Bot.HasInExtra(CardId.GranguignolDuskDragon) || Bot.HasInExtra(ShaddollFusionIds));
        }

        private bool VoidUnleashingActivate()
        {
            if (Card.Location == CardLocation.Grave)
                return false; // Infernoid Evil is Extra Deck-only and cannot be added from there.
            if (DefaultCheckWhetherCardIsNegated(Card) || HasSafeLethalOnBoard())
                return false;
            if (Duel.Player == 0 && IsLevRouteActive() &&
                Bot.GetMonsters().Any(c => c.IsCode(CardId.ElShaddollMeshachrer) &&
                    c.IsFaceup() && !c.IsDisabled()) &&
                !_levMeshSearchResolved)
                return false;
            ClientCard reveal = Bot.Hand
                .Where(c => c != Card && IsShaddollCard(c))
                .OrderBy(c => c.IsMonster() ? 0 : c.IsTrap() ? 1 : 2)
                .ThenByDescending(ShaddollMonsterSearchScore)
                .FirstOrDefault();
            if (reveal == null)
                return false;
            _voidRevealedType = reveal.IsMonster() ? CardType.Monster : reveal.IsSpell() ? CardType.Spell : CardType.Trap;
            AI.SelectCard(reveal);
            return MarkUsed(CardId.VoidUnleashing);
        }

        private bool LevPrimaryActivate()
        {
            if (DefaultCheckWhetherCardIsNegated(Card) || HasSafeLethalOnBoard())
                return false;
            if (Card.Location == CardLocation.SpellZone && Card.IsFaceup())
                return false;
            if (Bot.ExtraDeck.Count == 0)
                return false;
            _shaddollExtraDeckLock = true;
            return MarkUsed(CardId.LevShaddollFusion);
        }

        private bool LevTributeActivate()
        {
            if (Card.Location != CardLocation.SpellZone || !Card.IsFaceup() ||
                DefaultCheckWhetherCardIsNegated(Card) || HasSafeLethalOnBoard())
                return false;
            ClientCard tribute = Bot.GetMonsters()
                .Where(c => c.HasType(CardType.Fusion) && c.IsFaceup() && !ShouldPreserveMonster(c))
                .OrderByDescending(c => c.IsCode(CardId.ElShaddollMeshachrer) && IsLevRouteActive() ? 1 : 0)
                .ThenBy(c => LevTributeScore(c))
                .FirstOrDefault();
            if (tribute == null || ShouldStopSpecialSummoning(CardLocation.Extra))
                return false;
            AI.SelectCard(tribute);
            _levTributedMesh = tribute.IsCode(CardId.ElShaddollMeshachrer);
            _shaddollExtraDeckLock = true;
            return MarkUsed(CardId.LevShaddollFusion + 1);
        }

        private bool FusionDeploymentActivate()
        {
            if (DefaultCheckWhetherCardIsNegated(Card) ||
                ShouldStopSpecialSummoning(CardLocation.Deck) || HasSafeLethalOnBoard())
                return false;
            return Bot.HasInDeck(CardId.BlazingCartesia) && Bot.HasInExtra(CardId.GranguignolDuskDragon);
        }

        private bool ShaddollFusionActivate()
        {
            if (DefaultCheckWhetherCardIsNegated(Card) || !HasFusionResources(false) ||
                ShouldStopSpecialSummoning(CardLocation.Extra) || HasSafeLethalOnBoard() ||
                FunctionalWinda() || ControlBoardScore() >= 140)
                return false;
            return Duel.Player == 0 && IsMainPhase();
        }

        private bool ScheirenActivate()
        {
            if (Card.Location == CardLocation.Hand)
            {
                if (FunctionalWinda() || ShouldStopSpecialSummoning(CardLocation.Hand) ||
                    HasSafeLethalOnBoard() || Bot.GetMonsterCount() >= 5 ||
                    Bot.Deck.Count < 3 ||
                    !Bot.Hand.Any(c => c != Card && IsShaddollMonster(c)))
                    return false;
                return MarkUsed(CardId.TearlamentsScheiren);
            }
            if (Card.Location == CardLocation.Grave)
                return CanUseSpiritOrderedTrigger(Card) &&
                    !FunctionalWinda() && !ShouldStopSpecialSummoning(CardLocation.Extra) &&
                    MarkUsed(CardId.TearlamentsScheiren + 1);
            return false;
        }

        private bool ArmageddonKnightSummon()
        {
            return IsMainPhase() && Bot.GetMonsterCount() < 5 &&
                (Bot.HasInDeck(CardId.ShaddollSquamata) || HasShaddollSendTarget());
        }

        private bool ArmageddonKnightActivate()
        {
            return Card.Location == CardLocation.MonsterZone && HasShaddollSendTarget();
        }

        private bool SquamataActivate()
        {
            if (Card.Location == CardLocation.MonsterZone)
                return Enemy.GetMonsterCount() > 0 && MarkUsed(CardId.ShaddollSquamata);
            return CanUseSpiritOrderedTrigger(Card) &&
                HasShaddollSendTarget() && MarkUsed(CardId.ShaddollSquamata);
        }

        private bool HedgehogActivate()
        {
            if (Card.Location == CardLocation.MonsterZone)
                return ShaddollSpellTrapIds.Any(Bot.HasInDeck) && MarkUsed(CardId.ShaddollHedgehog);
            return CanUseSpiritOrderedTrigger(Card) &&
                ShaddollMainMonsterIds.Any(Bot.HasInDeck) && MarkUsed(CardId.ShaddollHedgehog);
        }

        private bool WendiActivate()
        {
            return (Card.Location != CardLocation.Grave || CanUseSpiritOrderedTrigger(Card)) &&
                Bot.GetMonsterCount() < 5 && ShaddollMainMonsterIds.Any(Bot.HasInDeck) &&
                MarkUsed(CardId.ReeshaddollWendi);
        }

        private bool DragonActivate()
        {
            bool useful = Card.Location == CardLocation.MonsterZone ?
                Enemy.GetFieldCount() > 0 : Enemy.GetSpellCount() > 0;
            return (Card.Location != CardLocation.Grave || CanUseSpiritOrderedTrigger(Card)) &&
                useful && MarkUsed(CardId.ShaddollDragon);
        }

        private bool BeastActivate()
        {
            bool useful = Card.Location == CardLocation.MonsterZone ? Bot.Hand.Count > 0 : Bot.Deck.Count > 0;
            return (Card.Location != CardLocation.Grave || CanUseSpiritOrderedTrigger(Card)) &&
                useful && MarkUsed(CardId.ShaddollBeast);
        }

        private bool ArielActivate()
        {
            bool useful = Card.Location == CardLocation.MonsterZone ?
                Bot.Banished.Any(c => IsShaddollCard(c)) : Enemy.Graveyard.Any(c =>
                    c.IsMonster() || c.IsCode(CardId.OrcustratedClimax,
                        CardId.PhantomKnightsFogBlade, CardId.ReturnOfTheDragonLords));
            return (Card.Location != CardLocation.Grave || CanUseSpiritOrderedTrigger(Card)) &&
                useful && MarkUsed(CardId.NaelshaddollAriel);
        }

        private bool TohushaddollGrystaActivate()
        {
            if (Card.Location == CardLocation.MonsterZone)
                return Bot.Graveyard.Any(c => IsShaddollCard(c) && c.HasType(CardType.Flip)) &&
                    MarkUsed(CardId.TohushaddollGrysta);
            if (!CanUseSpiritOrderedTrigger(Card) || FunctionalWinda() ||
                ShouldStopSpecialSummoning(CardLocation.Extra) || !HasFusionResources(true))
                return false;
            _shaddollExtraDeckLock = true;
            return MarkUsed(CardId.TohushaddollGrysta);
        }

        private bool ConstructActivate()
        {
            if (Card.Location == CardLocation.MonsterZone)
                return HasShaddollSendTarget();
            return CanUseSpiritOrderedTrigger(Card) && HasRecoverableSpellTrap();
        }

        private bool ApkalloneActivate()
        {
            if (Card.Location == CardLocation.MonsterZone)
                return !Card.IsDisabled() && Enemy.GetMonsters().Concat(Enemy.GetSpells())
                    .Any(c => c.IsFaceup() && !c.IsDisabled());
            return CanUseSpiritOrderedTrigger(Card) && HasRecoverableShaddollCard();
        }

        private bool InfernoidEvilActivate()
        {
            return (Card.Location == CardLocation.Grave || Card.Location == CardLocation.Removed) &&
                (Card.Location != CardLocation.Grave || CanUseSpiritOrderedTrigger(Card)) &&
                Bot.HasInDeck(CardId.VoidUnleashing);
        }

        private bool MeshachrerActivate()
        {
            if (Card.Location == CardLocation.MonsterZone)
                return Bot.LifePoints > 1600 && HasUsefulMeshSearch() &&
                    MarkUsed(CardId.ElShaddollMeshachrer);
            return Bot.GetMonsterCount() < 5 && Bot.Graveyard.Any(IsShaddollMonster) &&
                MarkUsed(CardId.ElShaddollMeshachrer + 1);
        }

        private bool FusionRecoveryActivate()
        {
            return Card.Location == CardLocation.Grave &&
                CanUseSpiritOrderedTrigger(Card) && HasRecoverableSpellTrap();
        }

        private bool PerfumeDancerActivate()
        {
            if (Card.Location != CardLocation.Grave || Duel.Player != 0 || Duel.Phase > DuelPhase.Main2)
                return false;
            return Enemy.GetMonsters().Any(c => c.IsFaceup() && c.Defense >= 2000) &&
                Bot.GetMonsters().Any(c => c.IsFaceup() && c.Attack > 0);
        }

        private bool GranguignolActivate()
        {
            if (Card.Location == CardLocation.MonsterZone)
                return Bot.ExtraDeck.Any(c => c.Level >= 6 && c.HasAttribute(CardAttribute.Light | CardAttribute.Dark));
            return false; // No Dogmatika or Despia summon target exists in the supplied deck.
        }

        private bool SetShaddollMonster()
        {
            if (Bot.GetMonsterCount() >= 5)
                return false;
            if (Bot.HasInMonstersZone(CardId.ReeshaddollWendikuruhu))
                return true;
            return !HasImmediateFusionStarter() || Bot.GetMonsterCount() == 0;
        }

        private bool SetSpellTrap()
        {
            if (Card.IsCode(CardId.ShaddollSchism, CardId.ElShaddollFusion))
                return Bot.GetSpellCountWithoutField() < 4;
            return DefaultSpellSet();
        }

        private bool ShaddollMonsterRepos()
        {
            if (Card != null && Card.IsFacedown() && Card.HasType(CardType.Flip))
            {
                bool liveWendikuruhu = Bot.GetMonsters().Any(c => c.IsFaceup() &&
                    !c.IsDisabled() && c.IsCode(CardId.ReeshaddollWendikuruhu));
                int flipFuel = Bot.GetMonsters().Count(c =>
                    c.IsFacedown() && c.HasType(CardType.Flip));
                // Wendikuruhu cannot perform its opponent-turn position change
                // without one face-down Flip monster. Develop surplus bodies,
                // but never let the generic battle routine expose the last fuel.
                if (liveWendikuruhu && flipFuel <= 1)
                    return false;
            }
            return DefaultMonsterRepos();
        }

        private IList<ClientCard> SelectVoidSearch(IList<ClientCard> cards, int min, int max)
        {
            List<ClientCard> selected = new List<ClientCard>();
            CardType usedTypes = _voidRevealedType;
            while (selected.Count < max)
            {
                ClientCard choice = cards
                    .Where(c => !selected.Contains(c) && (usedTypes & GetBaseType(c)) == 0)
                    .OrderByDescending(VoidSearchScore)
                    .FirstOrDefault();
                if (choice == null)
                    break;
                selected.Add(choice);
                CardType type = GetBaseType(choice);
                usedTypes |= type;
                // SelectSubGroup normally asks for one card per SelectUnselect
                // packet. Remember committed types when the lifecycle permits;
                // global value ordering also remains correct if that transient
                // state is cleared between packets.
                _voidRevealedType |= type;
            }
            return FillSelection(selected, cards, min, max);
        }

        private IList<ClientCard> SelectFusionTarget(IList<ClientCard> cards, int min, int max, int ownerId)
        {
            List<ClientCard> ordered = cards.OrderByDescending(c => FusionTargetScore(c, ownerId)).ToList();
            IList<ClientCard> result = FillSelection(ordered, cards, min, max);
            if (result.Count > 0)
            {
                _pendingFusionTargetId = result[0].Id;
                if (ownerId == CardId.ElShaddollFusion && Duel.Player == 1 &&
                    result[0].IsCode(CardId.ElShaddollApkallone) && HasPreparedLevControlBoard())
                    _levOpponentTurnBranch = true;
            }
            return result;
        }

        private IList<ClientCard> SelectEnemyThreat(IList<ClientCard> cards, int min, int max)
        {
            List<ClientCard> ordered = cards
                .OrderByDescending(OpponentThreatScore)
                .ToList();
            return FillSelection(ordered, cards, min, max);
        }

        private IList<ClientCard> SelectArielTargets(IList<ClientCard> cards, int min, int max)
        {
            List<ClientCard> enemyTargets = cards
                .Where(c => c.Controller == 1)
                .OrderByDescending(GraveDisruptionScore)
                .Take(Math.Min(3, max))
                .ToList();
            int targetCount = Math.Max(min, enemyTargets.Count);
            targetCount = Math.Min(targetCount, Math.Min(3, max));
            List<ClientCard> ordered = enemyTargets
                .Concat(cards.Where(c => !enemyTargets.Contains(c)).OrderByDescending(GraveDisruptionScore))
                .ToList();
            return FillSelection(ordered, cards, targetCount, targetCount);
        }

        private IList<ClientCard> SelectIds(IList<ClientCard> cards, int min, int max, params int[] ids)
        {
            List<ClientCard> ordered = new List<ClientCard>();
            foreach (int id in ids)
                ordered.AddRange(cards.Where(c => c.IsCode(id) && !ordered.Contains(c)));
            return FillSelection(ordered, cards, min, max);
        }

        private IList<ClientCard> SelectByScore(
            IList<ClientCard> cards, int min, int max, Func<ClientCard, int> score)
        {
            return FillSelection(cards.OrderByDescending(score).ToList(), cards, min, max);
        }

        private IList<ClientCard> FillSelection(
            IList<ClientCard> preferred, IList<ClientCard> offered, int min, int max)
        {
            List<ClientCard> result = new List<ClientCard>();
            int targetCount = Math.Min(max, Math.Max(min, 1));
            foreach (ClientCard card in preferred)
            {
                if (card != null && offered.Contains(card) && !result.Contains(card))
                    result.Add(card);
                if (result.Count == targetCount)
                    break;
            }
            foreach (ClientCard card in offered)
            {
                if (result.Count >= targetCount)
                    break;
                if (!result.Contains(card))
                    result.Add(card);
            }
            return result;
        }

        private int FusionTargetScore(ClientCard card, int ownerId)
        {
            if (_shaddollExtraDeckLock && !card.HasSetcode(ShaddollSetcode))
                return -1000;
            if (ownerId == CardId.DracotailFaimena && Duel.Player == 1 &&
                card.IsCode(CardId.ElShaddollWinda))
                return 245;
            if (ownerId == CardId.TohushaddollGrysta && IsLevRouteActive() &&
                card.IsCode(CardId.ElShaddollMeshachrer))
                return 240;
            if (ownerId == CardId.LevShaddollFusion &&
                (_levTributedMesh || Bot.HasInMonstersZone(CardId.ElShaddollMeshachrer)))
            {
                if (CanReachLevControlExtension() && card.IsCode(CardId.ElShaddollConstruct))
                    return 240;
                if (!CanReachLevControlExtension() && card.IsCode(CardId.ElShaddollWinda))
                    return 235;
            }
            if (ownerId == CardId.ElShaddollFusion && HasLevControlExtension() &&
                Bot.HasInMonstersZone(CardId.ElShaddollConstruct) &&
                card.IsCode(CardId.ReeshaddollWendikuruhu))
                return 240;
            if (ownerId == CardId.ElShaddollFusion && Duel.Player == 1 &&
                card.IsCode(CardId.ElShaddollApkallone) &&
                BestPersistentOpponentThreatScore() >= 750)
                return 280;
            if (ownerId == CardId.TohushaddollGrysta && Duel.Player == 1 &&
                _levOpponentTurnBranch && Bot.Hand.Any(IsShaddollCard) &&
                card.IsCode(CardId.ElShaddollShekhinaga))
                return 255;
            if (ownerId == CardId.ShaddollSchism && Duel.Player == 1)
            {
                if (ShouldEstablishWindaBeforeFirstSummon() &&
                    card.IsCode(CardId.ElShaddollWinda))
                    return 400;
                if (ShouldUseSchismMaterialRemovalWindow())
                {
                    int materialThreat = Enemy.GetMonsters()
                        .Where(c => c.IsFaceup() && card.Attribute != 0 &&
                            (c.Attribute & card.Attribute) != 0)
                        .Select(OpponentThreatScore)
                        .DefaultIfEmpty(-1000)
                        .Max();
                    if (materialThreat > -1000)
                        return 370 +
                            (card.IsCode(CardId.ElShaddollWinda) ? 20 : 0) +
                            Math.Min(25, Math.Max(0, materialThreat - 500) / 20);
                    if (card.IsCode(CardId.ElShaddollWinda))
                        return 225;
                }
                // Schism's post-Fusion send is often stronger than a Winda lock
                // once a live Extra Deck negate is already established. Prefer
                // a legally offered Fusion whose Attribute removes that threat;
                // Apkallone is the fallback answer to Hope Harbinger when no
                // matching LIGHT Fusion is legal from the current materials.
                int matchingThreat = Enemy.GetMonsters()
                    .Where(c => c.IsFaceup() && !c.IsDisabled() &&
                        card.Attribute != 0 && (c.Attribute & card.Attribute) != 0)
                    .Select(OpponentThreatScore)
                    .DefaultIfEmpty(-1000)
                    .Max();
                int faceupEngineThreat = Enemy.GetSpells()
                    .Where(IsPersistentOpponentThreat)
                    .Select(OpponentThreatScore)
                    .DefaultIfEmpty(-1000)
                    .Max();
                if (card.IsCode(CardId.ElShaddollApkallone) && faceupEngineThreat >= 800)
                    return 335;
                if (matchingThreat >= 750)
                    return 300 + Math.Min(40, (matchingThreat - 750) / 10);
                if (card.IsCode(CardId.ElShaddollApkallone) &&
                    BestPersistentOpponentThreatScore() >= 750)
                    return 295;
                if (card.IsCode(CardId.ElShaddollWinda))
                    return 250;
            }
            int score = 0;
            if (card.IsCode(CardId.ElShaddollWinda))
            {
                score = Duel.Player == 1 ? 130 : Duel.Phase == DuelPhase.Main2 ? 82 : 35;
                if (FunctionalWinda()) score -= 100;
            }
            else if (card.IsCode(CardId.ElShaddollConstruct))
                score = Duel.Player == 0 && Enemy.GetMonsterCount() > 0 ? 108 : 70;
            else if (card.IsCode(CardId.ElShaddollApkallone))
            {
                bool liveFaceup = Enemy.GetMonsters().Concat(Enemy.GetSpells())
                    .Any(c => c.IsFaceup() && !c.IsDisabled());
                score = liveFaceup ? (Duel.Player == 1 ? 125 : 104) : 72;
                if (FunctionalWinda() && liveFaceup) score += 15;
            }
            else if (card.IsCode(CardId.ElShaddollMeshachrer))
                score = Bot.LifePoints > 1600 && Bot.Hand.Count >= 2 ? 92 : 58;
            else if (card.IsCode(CardId.ReeshaddollWendikuruhu))
                score = Bot.GetMonsters().Any(c => c.IsFacedown() && c.HasType(CardType.Flip)) ? 90 : 55;
            else if (card.IsCode(CardId.ElShaddollShekhinaga))
                score = Bot.Hand.Any(IsShaddollCard) ? 85 : 60;
            else if (card.IsCode(CardId.ElShaddollAnoyatyllis))
                score = Enemy.Graveyard.Count + Enemy.Hand.Count > 5 ? 68 : 48;
            else if (card.IsCode(CardId.GranguignolDuskDragon))
                score = ownerId == CardId.BlazingCartesia || ownerId == CardId.FusionDeployment ? 96 : 40;

            if (ShouldStopSpecialSummoning(CardLocation.Extra) && FunctionalBoard())
                score -= 60;
            return score;
        }

        private int FusionMaterialScore(ClientCard card)
        {
            if (_pendingFusionTargetId == CardId.ElShaddollMeshachrer &&
                IsLevRouteActive() && _levRouteStage >= LevRouteStage.TohushaddollSent)
            {
                if (card.Location == CardLocation.Grave && card.IsCode(
                    CardId.ElShaddollApkallone,
                    CardId.ShaddollSquamata,
                    CardId.TohushaddollGrysta))
                    return 500;
                if (card.Location == CardLocation.Hand && IsShaddollCard(card))
                    return -600;
                if (card.Location == CardLocation.Hand)
                    return -500;
            }
            if (_pendingFusionTargetId == CardId.GranguignolDuskDragon)
            {
                if (card.IsCode(CardId.BlazingCartesia)) return 280;
                if (IsShaddollMonster(card)) return 220 + ShaddollSendScore(card);
                if (card.IsCode(CardId.AshBlossomJoyousSpring, CardId.EffectVeiler,
                    CardId.DrollLockBird, CardId.MaxxC,
                    CardId.MulcharmyFuwalos, CardId.MulcharmyPurulia)) return -100;
            }
            if (_pendingFusionTargetId == CardId.ReeshaddollWendikuruhu &&
                HasLevControlExtension())
            {
                if (card.IsCode(CardId.ElShaddollConstruct)) return 260;
                if (card.IsCode(CardId.ReeshaddollWendi)) return 250;
            }
            if (_pendingFusionTargetId == CardId.ElShaddollApkallone &&
                _levOpponentTurnBranch)
            {
                if (card.IsCode(CardId.ShaddollSquamata)) return 270;
                if (card.IsCode(CardId.NaelshaddollAriel)) return 260;
            }
            if (_pendingFusionTargetId == CardId.ElShaddollShekhinaga &&
                _levOpponentTurnBranch)
            {
                if (card.HasAttribute(CardAttribute.Earth) && !IsShaddollCard(card)) return 270;
                if (card.IsCode(CardId.TohushaddollGrysta)) return 260;
            }
            int score = 0;
            if (card.Location == CardLocation.Grave) score += 35;
            if (card.Location == CardLocation.Deck) score += 30;
            if (card.Location == CardLocation.MonsterZone && card.IsFacedown()) score += 16;
            if (card.Location == CardLocation.Hand) score -= 8;
            if (IsShaddollMonster(card)) score += ShaddollSendScore(card);
            if (card.IsCode(CardId.AshBlossomJoyousSpring, CardId.EffectVeiler,
                CardId.DrollLockBird, CardId.MaxxC,
                CardId.MulcharmyFuwalos, CardId.MulcharmyPurulia)) score -= 18;
            if (card.IsCode(CardId.BlazingCartesia, CardId.DracotailFaimena)) score -= 25;
            if (ShouldPreserveMonster(card)) score -= 300;
            if (card.Id == _pendingFusionTargetId) score -= 100;
            return score;
        }

        private int LevDumpScore(ClientCard card)
        {
            if (card.IsCode(CardId.LunalightPerfumeDancer) &&
                Enemy.GetMonsters().Any(c => c.IsFaceup() && c.Defense >= 2500)) return 250;
            if (card.IsCode(CardId.InfernoidEvil) && IsArmageddonLevRoute() &&
                Bot.HasInDeck(CardId.VoidUnleashing)) return 245;
            if (card.IsCode(CardId.ElShaddollApkallone) && CanStartLevPrimaryRoute()) return 220;
            if (card.IsCode(CardId.InfernoidEvil) && Bot.HasInDeck(CardId.VoidUnleashing) &&
                HasVoidRevealInHand() && !Bot.HasInHandOrInSpellZone(CardId.VoidUnleashing)) return 126;
            if (card.IsCode(CardId.ElShaddollMeshachrer) && Bot.Graveyard.Any(IsShaddollMonster) &&
                Bot.GetMonsterCount() < 5) return 112;
            if (card.IsCode(CardId.ReeshaddollWendikuruhu) && HasRecoverableShaddollCard()) return 105;
            if (card.IsCode(CardId.ElShaddollConstruct) && HasRecoverableSpellTrap()) return 102;
            if (card.IsCode(CardId.ElShaddollApkallone)) return 100;
            if (card.IsCode(CardId.ElShaddollWinda, CardId.ElShaddollShekhinaga,
                CardId.ElShaddollAnoyatyllis)) return HasRecoverableSpellTrap() ? 88 : 40;
            return 10;
        }

        private int LevTributeScore(ClientCard card)
        {
            int score = card.GetDefensePower();
            if (card.Attack == 0) score -= 1000;
            if (card.IsDisabled()) score -= 800;
            if (card.IsCode(CardId.ElShaddollConstruct, CardId.ElShaddollApkallone)) score -= 300;
            return score;
        }

        private int ShaddollSendScore(ClientCard card)
        {
            if (card.IsCode(CardId.ShaddollDragon) && Enemy.GetSpells().Any(c =>
                IsPersistentOpponentThreat(c) && OpponentThreatScore(c) >= 800) &&
                !EffectUsed(CardId.ShaddollDragon)) return 190;
            if (card.IsCode(CardId.ShaddollDragon) && Enemy.GetSpellCount() > 0 &&
                !EffectUsed(CardId.ShaddollDragon)) return 115;
            if (card.IsCode(CardId.NaelshaddollAriel) && Enemy.Graveyard.Any(c => c.IsMonster()) &&
                !EffectUsed(CardId.NaelshaddollAriel)) return 110;
            if (card.IsCode(CardId.TohushaddollGrysta) && !EffectUsed(CardId.TohushaddollGrysta) &&
                HasFusionResources(true)) return 106;
            if (card.IsCode(CardId.ShaddollSquamata) && !EffectUsed(CardId.ShaddollSquamata)) return 102;
            if (card.IsCode(CardId.ShaddollHedgehog) && !EffectUsed(CardId.ShaddollHedgehog)) return 96;
            if (card.IsCode(CardId.ReeshaddollWendi) && !EffectUsed(CardId.ReeshaddollWendi) &&
                Bot.GetMonsterCount() < 5) return 92;
            if (card.IsCode(CardId.ShaddollBeast) && !EffectUsed(CardId.ShaddollBeast)) return 80;
            return IsShaddollCard(card) ? 25 : 5;
        }

        private int DiscardScore(ClientCard card)
        {
            int score = ShaddollSendScore(card);
            if (card.IsCode(CardId.ShaddollSchism) && !Bot.HasInSpellZone(CardId.ShaddollSchism)) score -= 80;
            if (card.IsCode(CardId.ElShaddollFusion) && !Bot.HasInSpellZone(CardId.ElShaddollFusion)) score -= 50;
            if (card.IsCode(CardId.AshBlossomJoyousSpring, CardId.MaxxC,
                CardId.EffectVeiler, CardId.DrollLockBird,
                CardId.MulcharmyFuwalos, CardId.MulcharmyPurulia)) score -= 40;
            return score;
        }

        private int ApkalloneSearchScore(ClientCard card)
        {
            if (IsLevRouteActive() && CanContinueLevThroughSquamata() &&
                card.IsCode(CardId.ShaddollSquamata)) return 240;
            return ShaddollSearchScore(card);
        }

        private int ApkalloneDiscardScore(ClientCard card)
        {
            if (IsLevRouteActive() && CanContinueLevThroughSquamata() &&
                card.IsCode(CardId.ShaddollSquamata)) return 240;
            return DiscardScore(card);
        }

        private int SquamataSendScore(ClientCard card)
        {
            if (IsLevRouteActive() && Bot.HasInExtra(CardId.ElShaddollMeshachrer) &&
                card.IsCode(CardId.TohushaddollGrysta)) return 240;
            return ShaddollSendScore(card);
        }

        private int ConstructSendScore(ClientCard card)
        {
            if (IsLevRouteActive() && HasLevControlExtension())
            {
                if (!Bot.HasInHand(CardId.ReeshaddollWendi) &&
                    card.IsCode(CardId.ShaddollHedgehog)) return 240;
                if (Bot.HasInHand(CardId.ReeshaddollWendi) &&
                    card.IsCode(CardId.ReshShaddollIncarnation)) return 235;
            }
            return ShaddollSendScore(card);
        }

        private int VoidDiscardScore(ClientCard card)
        {
            // Void may discard any card, not only the revealed Shaddoll. Preserve
            // unique continuation pieces, then compare live GY value with the
            // opportunity cost of spending a non-engine card.
            if (IsLevRouteActive() && _levRouteStage >= LevRouteStage.VoidSearched &&
                _levRouteStage < LevRouteStage.ConstructSummoned)
            {
                if (card.IsCode(CardId.ReeshaddollWendi) &&
                    Bot.Hand.Count(c => c.IsCode(CardId.ReeshaddollWendi)) == 1)
                    return -650;
                if (card.IsCode(CardId.ElShaddollFusion, CardId.ShaddollSchism) &&
                    Bot.Hand.Count(c => c.IsCode(card.Id)) == 1)
                    return -625;
                if (!IsShaddollCard(card))
                    return 300;
            }
            if (card.IsCode(CardId.LevShaddollFusion) &&
                !EffectUsed(CardId.LevShaddollFusion) &&
                !Bot.HasInSpellZone(CardId.LevShaddollFusion)) return -500;
            if (card.IsCode(CardId.ShaddollSchism) &&
                !Bot.HasInSpellZone(CardId.ShaddollSchism) &&
                Bot.Hand.Count(c => c.IsCode(CardId.ShaddollSchism)) == 1) return -480;
            if (card.IsCode(CardId.ElShaddollFusion) && IsLevRouteActive() &&
                !Bot.HasInSpellZone(CardId.ElShaddollFusion) &&
                Bot.Hand.Count(c => c.IsCode(CardId.ElShaddollFusion)) == 1) return -470;
            if (IsLevRouteActive() && Bot.HasInMonstersZone(CardId.ElShaddollConstruct))
            {
                if (card.IsCode(CardId.ReeshaddollWendi, CardId.ElShaddollFusion,
                    CardId.ShaddollSchism)) return -500;
                if (card.IsCode(CardId.LevShaddollFusion)) return 250;
                if (!IsShaddollCard(card)) return 220;
            }
            if (IsShaddollMonster(card) && !EffectUsed(card.Id))
            {
                int graveValue = ShaddollSendScore(card) + 35;
                if (card.IsCode(CardId.ShaddollBeast)) graveValue += 25;
                return graveValue;
            }
            if (IsShaddollCard(card)) return DiscardScore(card);

            if (card.IsCode(CardId.AshBlossomJoyousSpring, CardId.MaxxC,
                CardId.EffectVeiler, CardId.DrollLockBird,
                CardId.MulcharmyFuwalos, CardId.MulcharmyPurulia)) return 15;
            if (card.IsCode(CardId.BlazingCartesia, CardId.DracotailFaimena,
                CardId.TearlamentsScheiren, CardId.FusionDeployment)) return 20;
            if (card.IsCode(CardId.ArmageddonKnight) &&
                !IsLevRouteActive() && !Bot.HasInHandOrInSpellZone(CardId.LevShaddollFusion)) return 10;
            return 55;
        }

        private int ScheirenSendScore(ClientCard card)
        {
            if (!IsShaddollMonster(card)) return -300;
            int score = ShaddollSendScore(card) + 40;
            if (card.IsCode(CardId.ShaddollBeast)) score += 20;
            if (IsLevRouteActive() && Bot.HasInMonstersZone(CardId.ElShaddollConstruct) &&
                card.IsCode(CardId.ReeshaddollWendi)) score -= 300;
            return score;
        }

        private int ArmageddonSendScore(ClientCard card)
        {
            if (card.IsCode(CardId.ShaddollSquamata) && Bot.HasInDeck(CardId.TohushaddollGrysta) &&
                Bot.HasInExtra(CardId.ElShaddollMeshachrer)) return 220;
            return ShaddollSendScore(card);
        }

        private int ReshCostScore(ClientCard card)
        {
            int score = EffectUsed(card.Id) ? 110 : 25;
            if (card.IsCode(CardId.ShaddollBeast, CardId.ShaddollHedgehog,
                CardId.ShaddollDragon, CardId.ShaddollSquamata)) score += 15;
            if (card.IsCode(CardId.ElShaddollWinda, CardId.ElShaddollConstruct,
                CardId.ElShaddollApkallone, CardId.ReeshaddollWendikuruhu)) score -= 100;
            if (card.IsSpell() || card.IsTrap()) score -= 80;
            return score;
        }

        private int ShaddollMonsterSearchScore(ClientCard card)
        {
            if (card.IsCode(CardId.TohushaddollGrysta) && HasFusionResources(true)) return 110;
            if (card.IsCode(CardId.ShaddollSquamata)) return 100;
            if (card.IsCode(CardId.ReeshaddollWendi) && Bot.GetMonsterCount() < 5) return 90;
            if (card.IsCode(CardId.NaelshaddollAriel) && Enemy.Graveyard.Any(c => c.IsMonster())) return 88;
            if (card.IsCode(CardId.ShaddollDragon) && Enemy.GetSpellCount() > 0) return 85;
            if (card.IsCode(CardId.ShaddollBeast)) return 70;
            return 20;
        }

        private int HedgehogMonsterSearchScore(ClientCard card)
        {
            if (IsLevRouteActive() && Bot.HasInMonstersZone(CardId.ElShaddollConstruct) &&
                card.IsCode(CardId.ReeshaddollWendi)) return 240;
            return ShaddollMonsterSearchScore(card);
        }

        private int ShaddollSearchScore(ClientCard card)
        {
            if (card.IsCode(CardId.ShaddollSchism) && !Bot.HasInHandOrInSpellZone(CardId.ShaddollSchism)) return 120;
            if (card.IsCode(CardId.ReshShaddollIncarnation) &&
                (Bot.Graveyard.Any(c => c.IsCode(CardId.ElShaddollWinda)) ||
                 Bot.GetMonsters().Any(c => c.IsFacedown() && c.HasType(CardType.Flip)))) return 116;
            if (card.IsCode(CardId.ElShaddollFusion) && !Bot.HasInHandOrInSpellZone(CardId.ElShaddollFusion)) return 112;
            if (card.IsCode(CardId.ShaddollFusion) && !Bot.HasInHandOrInSpellZone(CardId.ShaddollFusion)) return 108;
            if (card.IsCode(CardId.LevShaddollFusion) && !Bot.HasInHandOrInSpellZone(CardId.LevShaddollFusion)) return 104;
            return card.IsMonster() ? ShaddollMonsterSearchScore(card) : 35;
        }

        private int VoidSearchScore(ClientCard card)
        {
            if (IsLevRouteActive() && Bot.HasInMonstersZone(CardId.ElShaddollMeshachrer))
            {
                if (card.IsCode(CardId.ShaddollSchism)) return 240;
                if (card.IsCode(CardId.ElShaddollFusion)) return 235;
                if ((_voidRevealedType & CardType.Monster) == 0 &&
                    card.IsCode(CardId.ReeshaddollWendi)) return 230;
            }
            if (card.IsCode(CardId.ShaddollSchism)) return 130;
            if (card.IsCode(CardId.LevShaddollFusion) && !_shaddollExtraDeckLock &&
                !Bot.HasInHandOrInSpellZone(CardId.LevShaddollFusion)) return 125;
            if (card.IsCode(CardId.ElShaddollFusion)) return 115;
            if (card.IsCode(CardId.ShaddollFusion)) return Enemy.GetMonsters().Any(IsExtraDeckMonster) ? 122 : 105;
            if (card.IsCode(CardId.TohushaddollGrysta) && HasFusionResources(true)) return 118;
            if (card.IsCode(CardId.ShaddollSquamata)) return 112;
            return ShaddollSearchScore(card);
        }

        private int MeshSearchScore(ClientCard card)
        {
            if (!IsLevRouteActive() && IsArmageddonLevRoute() &&
                card.IsCode(CardId.LevShaddollFusion)) return 260;
            if (IsLevRouteActive() && card.IsCode(CardId.VoidUnleashing) &&
                !EffectUsed(CardId.VoidUnleashing)) return 250;
            if (IsLevRouteActive() && HasVoidRevealInHand() &&
                card.IsCode(CardId.ElShaddollFusion)) return 225;
            if (card.IsCode(CardId.ShaddollSchism) && !Bot.HasInHandOrInSpellZone(CardId.ShaddollSchism)) return 120;
            return ShaddollSearchScore(card);
        }

        private int WendiSummonScore(ClientCard card)
        {
            if (IsLevRouteActive() && Bot.HasInMonstersZone(CardId.ReeshaddollWendikuruhu) &&
                Bot.Banished.Any(c => c.IsCode(CardId.ShaddollSquamata)) &&
                card.IsCode(CardId.NaelshaddollAriel)) return 240;
            if (card.IsCode(CardId.ShaddollSquamata) && Enemy.GetMonsterCount() > 0) return 110;
            if (card.IsCode(CardId.ShaddollDragon) && Enemy.GetFieldCount() > 0) return 105;
            if (card.IsCode(CardId.ShaddollHedgehog)) return 98;
            if (card.IsCode(CardId.NaelshaddollAriel) && Bot.Banished.Any(IsShaddollMonster)) return 94;
            if (card.IsCode(CardId.ShaddollBeast)) return 82;
            if (card.IsCode(CardId.TohushaddollGrysta)) return 70;
            return 20;
        }

        private int FlipUpScore(ClientCard card)
        {
            if (card.Controller == 1) return 5;
            if (card.IsCode(CardId.ShaddollSquamata) && Enemy.GetMonsterCount() > 0) return 120;
            if (card.IsCode(CardId.ShaddollDragon) && Enemy.GetFieldCount() > 0) return 115;
            if (card.IsCode(CardId.ReeshaddollWendi)) return 105;
            if (card.IsCode(CardId.ShaddollHedgehog)) return 98;
            if (card.IsCode(CardId.ShaddollBeast)) return 90;
            return 40;
        }

        private int FlipCopyScore(ClientCard card)
        {
            if (card.IsCode(CardId.ShaddollSquamata) && Enemy.GetMonsterCount() > 0) return 120;
            if (card.IsCode(CardId.ShaddollDragon) && Enemy.GetFieldCount() > 0) return 115;
            if (card.IsCode(CardId.NaelshaddollAriel) && Bot.Banished.Any(IsShaddollMonster)) return 105;
            if (card.IsCode(CardId.ReeshaddollWendi)) return 100;
            if (card.IsCode(CardId.ShaddollHedgehog)) return 90;
            if (card.IsCode(CardId.ShaddollBeast)) return 80;
            return 20;
        }

        private int ResetFlipScore(ClientCard card)
        {
            if (card.Controller != 0 || !card.IsFaceup() || !IsShaddollMonster(card)) return 0;
            int score = EffectUsed(card.Id) ? 100 : 25;
            if (card.IsDisabled()) score += 40;
            if (card.IsCode(CardId.ElShaddollWinda)) score -= 200;
            return score;
        }

        private int GraveDisruptionScore(ClientCard card)
        {
            int score = card.Controller == 1 ? 100 : 0;
            if (card.Controller == 1)
            {
                OpponentCapability capability = GetOpponentCapabilities(card);
                if ((capability & OpponentCapability.GraveInteraction) != 0) score += 300;
                if ((capability & OpponentCapability.Negate) != 0) score += 250;
                if ((capability & OpponentCapability.Recursion) != 0) score += 220;
                if ((capability & OpponentCapability.QuickInteraction) != 0) score += 100;
                if (card.IsCode(CardId.OrcustCymbalSkeleton)) score += 800;
                else if (card.IsCode(CardId.OrcustHarpHorror)) score += 780;
                else if (card.IsCode(CardId.OrcustKnightmare)) score += 760;
                else if (card.IsCode(CardId.WorldLegacyWorldWand)) score += 740;
                else if (card.IsCode(CardId.DingirsuOrcustEveningStar)) score += 720;
                else if (card.IsCode(CardId.OrcustratedClimax)) score += 700;
                else if (card.IsCode(CardId.PhantomKnightsFogBlade)) score += 680;
                else if (card.IsCode(CardId.ReturnOfTheDragonLords)) score += 650;
                else if (card.IsCode(CardId.WhiteStoneOfAncients)) score += 630;
                else if (card.IsCode(CardId.MagicianNavigation)) score += 850;
                else if (card.IsCode(CardId.DarkMagicianTheDragonKnight)) score += 760;
                else if (card.IsCode(CardId.DarkMagician)) score += 720;
            }
            if (card.IsMonster()) score += 25;
            if (card.HasType(CardType.Fusion | CardType.Synchro | CardType.Xyz | CardType.Link)) score += 20;
            return score + card.Level;
        }

        private int OpponentThreatScore(ClientCard card)
        {
            if (card == null || card.Controller != 1)
                return -1000;
            if (!card.IsFaceup() || card.IsDisabled())
                return -500;

            OpponentCapability capability = GetOpponentCapabilities(card);
            int score = 100;
            if ((capability & OpponentCapability.Negate) != 0) score += 350;
            if ((capability & OpponentCapability.Removal) != 0) score += 220;
            if ((capability & OpponentCapability.Recursion) != 0) score += 220;
            if ((capability & OpponentCapability.Protection) != 0) score += 180;
            if ((capability & OpponentCapability.Floodgate) != 0) score += 450;
            if ((capability & OpponentCapability.QuickInteraction) != 0) score += 140;
            if ((capability & OpponentCapability.GraveInteraction) != 0) score += 80;
            if ((capability & OpponentCapability.RepeatableEngine) != 0) score += 350;
            if ((capability & OpponentCapability.AdvantageEngine) != 0) score += 120;
            if (card.HasType(CardType.Continuous | CardType.Field))
                score += 200;
            if (card.HasType(CardType.Fusion | CardType.Synchro | CardType.Xyz | CardType.Link))
                score += 220;
            if (card.IsMonster())
            {
                score += Math.Max(0, card.Attack) / 40;
                score += Math.Max(0, card.Defense) / 80;
            }
            return score;
        }

        private OpponentCapability GetOpponentCapabilities(ClientCard card)
        {
            if (card == null || card.Data == null)
                return OpponentCapability.None;

            string text = (card.Data.Description ?? string.Empty).ToLowerInvariant();
            OpponentCapability capability = OpponentCapability.None;
            if (text.Contains("negate the activation") || text.Contains("negate that effect") ||
                text.Contains("negate the effect") || text.Contains("negate its effect") ||
                text.Contains("negate its effects") || text.Contains("negate their effect") ||
                text.Contains("negate their effects"))
                capability |= OpponentCapability.Negate;
            if (text.Contains("banish it") || text.Contains("banish that") ||
                text.Contains("banish them") ||
                text.Contains("destroy it") || text.Contains("destroy that") ||
                text.Contains("destroy those") || text.Contains("attach it to this card") ||
                text.Contains("attach that card to this card") ||
                text.Contains("send it to the gy") || text.Contains("send that target to the gy") ||
                text.Contains("return it to the hand") || text.Contains("return that target to the hand") ||
                text.Contains("shuffle it into the deck") || text.Contains("shuffle that target into the deck"))
                capability |= OpponentCapability.Removal;
            if (text.Contains("special summon") &&
                (text.Contains("from your gy") || text.Contains("from the gy") ||
                 text.Contains("hand or gy") || text.Contains("deck, and/or gy")))
                capability |= OpponentCapability.Recursion;
            if (text.Contains("cannot be destroyed") || text.Contains("unaffected by") ||
                text.Contains("cannot target") || text.Contains("cannot be targeted"))
                capability |= OpponentCapability.Protection;
            if (card.IsFloodgate() || text.Contains("your opponent cannot special summon") ||
                text.Contains("neither player can special summon") ||
                text.Contains("your opponent can only special summon") ||
                text.Contains("neither player can activate"))
                capability |= OpponentCapability.Floodgate;
            if (text.Contains("(quick effect)") || text.Contains("during either player's turn"))
                capability |= OpponentCapability.QuickInteraction;
            if (text.Contains("from your gy") || text.Contains("from the gy") ||
                text.Contains("banish this card from your gy"))
                capability |= OpponentCapability.GraveInteraction;
            if (text.Contains("add it to your hand") || text.Contains("add 1 ") ||
                text.Contains("draw 1 card"))
                capability |= OpponentCapability.AdvantageEngine;
            bool persistentCard = card.IsMonster() ||
                card.HasType(CardType.Continuous | CardType.Field);
            if (persistentCard &&
                text.Contains("once per turn") &&
                (capability & (OpponentCapability.Negate | OpponentCapability.Removal |
                    OpponentCapability.Recursion | OpponentCapability.Protection |
                    OpponentCapability.AdvantageEngine)) != 0)
                capability |= OpponentCapability.RepeatableEngine;
            return capability;
        }

        private int BestPersistentOpponentThreatScore()
        {
            return Enemy.GetMonsters().Concat(Enemy.GetSpells())
                .Where(IsPersistentOpponentThreat)
                .Select(OpponentThreatScore)
                .DefaultIfEmpty(-1000)
                .Max();
        }

        private bool IsPersistentOpponentThreat(ClientCard card)
        {
            if (card == null || card.Controller != 1 ||
                !card.IsFaceup() || card.IsDisabled())
                return false;
            if (card.IsMonster())
                return true;
            return card.HasType(CardType.Continuous | CardType.Field |
                CardType.Equip | CardType.Pendulum);
        }

        private bool HasUrgentPersistentSpellTrapEngine()
        {
            return Enemy.GetSpells().Any(c =>
                IsPersistentOpponentThreat(c) && OpponentThreatScore(c) >= 800);
        }

        private bool IsSpiritOrderableGraveTrigger(ClientCard card)
        {
            return card != null && (ShaddollMainMonsterIds.Contains(card.Id) ||
                card.IsCode(CardId.TohushaddollGrysta,
                    CardId.ElShaddollConstruct,
                    CardId.ElShaddollApkallone,
                    CardId.ReeshaddollWendikuruhu,
                    CardId.ElShaddollWinda,
                    CardId.ElShaddollShekhinaga,
                    CardId.ElShaddollAnoyatyllis,
                    CardId.InfernoidEvil,
                    CardId.TearlamentsScheiren));
        }

        private bool CanUseSpiritOrderedTrigger(ClientCard card)
        {
            return _preferredSpiritProtectedTrigger == null ||
                card == _preferredSpiritProtectedTrigger;
        }

        private int SpiritProtectedTriggerScore(ClientCard card)
        {
            // Simultaneous grave triggers are placed from the most important
            // effect first. The final, lower-value link is the only activation
            // Blue-Eyes Spirit Dragon can chain to and negate.
            if (card.IsCode(CardId.TohushaddollGrysta) && HasFusionResources(true)) return 1000;
            if (card.IsCode(CardId.ShaddollHedgehog) &&
                !Bot.HasInHand(CardId.ReeshaddollWendi)) return 940;
            if (card.IsCode(CardId.ElShaddollApkallone)) return 920;
            if (card.IsCode(CardId.InfernoidEvil) &&
                Bot.HasInDeck(CardId.VoidUnleashing)) return 900;
            if (card.IsCode(CardId.ReeshaddollWendi) && Bot.GetMonsterCount() < 5) return 880;
            if (card.IsCode(CardId.ShaddollSquamata) && HasShaddollSendTarget()) return 840;
            if (card.IsCode(CardId.ElShaddollConstruct) && HasRecoverableSpellTrap()) return 810;
            if (card.IsCode(CardId.ReeshaddollWendikuruhu)) return 780;
            if (card.IsCode(CardId.NaelshaddollAriel)) return 740;
            if (card.IsCode(CardId.ShaddollDragon)) return 700;
            if (card.IsCode(CardId.TearlamentsScheiren)) return 680;
            if (card.IsCode(CardId.ElShaddollWinda,
                CardId.ElShaddollShekhinaga, CardId.ElShaddollAnoyatyllis)) return 650;
            if (card.IsCode(CardId.ShaddollBeast)) return 500;
            return 600;
        }

        private bool ShouldEstablishWindaBeforeFirstSummon()
        {
            if (Duel.Player != 1 || Duel.CurrentChain.Count != 0 ||
                _opponentSpecialSummonsThisTurn != 0 ||
                FunctionalWinda() ||
                !Bot.HasInExtra(CardId.ElShaddollWinda) ||
                !Bot.HasInSpellZone(CardId.ShaddollSchism) ||
                !HasSchismWindaResources())
                return false;
            // Winda only counts Special Summons made while this copy is face-up.
            // Waiting for the first Blue-Eyes body still permits the Synchro/Xyz
            // summon that the lock was meant to prevent. Its own destruction
            // protection makes the pre-summon lock worthwhile even when the
            // full Wendikuruhu + Flip protection shell was interrupted.
            return !HasUrgentPersistentSpellTrapEngine();
        }

        private bool ShouldUseSchismMaterialRemovalWindow()
        {
            if (Duel.Player != 1 || _opponentSpecialSummonsThisTurn != 1 ||
                FunctionalWinda() || !Bot.HasInSpellZone(CardId.ShaddollSchism) ||
                !HasSchismFusionResources() || HasUrgentPersistentSpellTrapEngine())
                return false;
            return Enemy.GetMonsters().Any(target => target.IsFaceup() &&
                Bot.ExtraDeck.Any(fusion => ShaddollFusionIds.Contains(fusion.Id) &&
                    fusion.Attribute != 0 && (target.Attribute & fusion.Attribute) != 0));
        }

        private bool ShouldLeadWithElFusion()
        {
            if (Duel.Player != 1 || FunctionalWinda() ||
                ShouldEstablishWindaBeforeFirstSummon() ||
                ShouldUseSchismMaterialRemovalWindow() ||
                Bot.GetMonsters().Any(c => c.IsFaceup() && !c.IsDisabled() &&
                    c.IsCode(CardId.ElShaddollApkallone)))
                return false;
            bool readyElFusion = Bot.GetSpells().Any(c =>
                c.IsCode(CardId.ElShaddollFusion) && c.IsFacedown());
            return readyElFusion && CanActivateElShaddollFusionNow() &&
                Bot.HasInExtra(CardId.ElShaddollApkallone) &&
                BestPersistentOpponentThreatScore() >= 750;
        }

        private int RevivalScore(ClientCard card)
        {
            if (_voidDiscardedMonsterId != 0 && card.IsCode(_voidDiscardedMonsterId) &&
                IsShaddollMonster(card)) return 180;
            if (card.IsCode(CardId.ElShaddollWinda)) return 125;
            if (card.IsCode(CardId.ElShaddollConstruct)) return 108;
            if (card.IsCode(CardId.ElShaddollApkallone)) return 104;
            if (card.IsCode(CardId.ReeshaddollWendikuruhu)) return 100;
            if (card.IsCode(CardId.ShaddollSquamata)) return 90;
            return card.GetDefensePower();
        }

        private int ArielRevivalScore(ClientCard card)
        {
            if (card.IsCode(CardId.ShaddollSquamata))
                return 3000;
            return RevivalScore(card);
        }

        private int ShaddollRecoveryScore(ClientCard card)
        {
            if (card.IsCode(CardId.ShaddollSchism)) return 120;
            if (card.IsCode(CardId.ElShaddollFusion)) return 115;
            if (card.IsCode(CardId.ShaddollFusion)) return 110;
            if (card.IsCode(CardId.LevShaddollFusion)) return 105;
            return ShaddollMonsterSearchScore(card);
        }

        private int SpellTrapRecoveryScore(ClientCard card)
        {
            if (IsLevRouteActive() && Bot.HasInMonstersZone(CardId.ReeshaddollWendikuruhu) &&
                Bot.HasInHandOrInSpellZone(CardId.ShaddollSchism) &&
                card.IsCode(CardId.ElShaddollFusion)) return 250;
            if (card.IsCode(CardId.ShaddollSchism) && !Bot.HasInSpellZone(CardId.ShaddollSchism)) return 120;
            if (card.IsCode(CardId.ReshShaddollIncarnation)) return 118;
            if (card.IsCode(CardId.ElShaddollFusion)) return 115;
            if (card.IsCode(CardId.ShaddollFusion)) return 110;
            if (card.IsCode(CardId.LevShaddollFusion)) return 105;
            return 20;
        }

        private bool MarkUsed(int effectId)
        {
            if (_usedEffects.Contains(effectId))
                return false;
            _usedEffects.Add(effectId);
            return true;
        }

        private bool EffectUsed(int effectId)
        {
            return _usedEffects.Contains(effectId);
        }

        private bool CurrentOpponentChainIsCovered()
        {
            return _coveredOpponentChainLength > 0 &&
                _coveredOpponentChainLength == Duel.CurrentChainInfo.Count;
        }

        private void CoverCurrentOpponentChain()
        {
            _coveredOpponentChainLength = Duel.CurrentChainInfo.Count;
        }

        private bool ShouldStopSpecialSummoning(CardLocation source)
        {
            bool maxx = enemyResolvedEffectIdList.Contains(CardId.MaxxC);
            bool purulia = enemyResolvedEffectIdList.Contains(CardId.MulcharmyPurulia) &&
                (source & CardLocation.Hand) != 0;
            bool fuwalos = enemyResolvedEffectIdList.Contains(CardId.MulcharmyFuwalos) &&
                (source & (CardLocation.Deck | CardLocation.Extra)) != 0;
            if (!maxx && !purulia && !fuwalos)
                return false;
            return FunctionalBoard() || _specialSummonsThisTurn >= 1;
        }

        private bool FunctionalBoard()
        {
            return Bot.GetMonsters().Any(c => c.IsFaceup() && c.IsCode(
                CardId.ElShaddollWinda,
                CardId.ElShaddollShekhinaga,
                CardId.ReeshaddollWendikuruhu,
                CardId.ElShaddollMeshachrer)) ||
                Bot.HasInSpellZone(CardId.ShaddollSchism);
        }

        private int ControlBoardScore()
        {
            int score = 0;
            if (FunctionalWinda()) score += 110;
            if (Bot.HasInSpellZone(CardId.ShaddollSchism)) score += 65;
            if (Bot.GetMonsters().Any(c => c.IsFaceup() && !c.IsDisabled() &&
                c.IsCode(CardId.ElShaddollShekhinaga))) score += 55;
            if (Bot.GetMonsters().Any(c => c.IsFaceup() && !c.IsDisabled() &&
                c.IsCode(CardId.ReeshaddollWendikuruhu))) score += 55;
            score += Math.Min(40, Bot.GetMonsters().Count(IsUsefulFacedownFlip) * 20);
            return score;
        }

        private bool HasSafeLethalOnBoard()
        {
            if (Duel.Player != 0 || Duel.Phase != DuelPhase.Main1 || Enemy.LifePoints <= 0 ||
                Enemy.GetMonsterCount() > 0 || Enemy.GetSpellCount() > 0)
                return false;
            int availableAttack = Bot.GetMonsters()
                .Where(c => c.IsFaceup() && c.IsAttack() && !c.IsDisabled())
                .Sum(c => Math.Max(0, c.Attack));
            return availableAttack >= Enemy.LifePoints;
        }

        private bool ShouldPreserveMonster(ClientCard card)
        {
            if (card == null || card.Location != CardLocation.MonsterZone || !card.IsFaceup() || card.IsDisabled())
                return false;
            if (card.IsCode(CardId.ElShaddollWinda))
                return true;
            return card.IsCode(CardId.ElShaddollShekhinaga, CardId.ReeshaddollWendikuruhu) &&
                ControlBoardScore() >= 100;
        }

        private bool IsUsefulFacedownFlip(ClientCard card)
        {
            return card != null && card.Controller == 0 && card.IsFacedown() &&
                card.HasType(CardType.Flip) && FlipUpScore(card) >= 90;
        }

        private bool FunctionalWinda()
        {
            return Bot.GetMonsters().Any(c => c.IsFaceup() && !c.IsDisabled() &&
                c.IsCode(CardId.ElShaddollWinda));
        }

        private bool IsLevRouteActive()
        {
            return EffectUsed(CardId.LevShaddollFusion) ||
                Bot.HasInSpellZone(CardId.LevShaddollFusion);
        }

        private bool MustResolveLevMeshFirst()
        {
            return Duel.Player == 0 &&
                Bot.GetSpells().Any(c => c.IsCode(CardId.LevShaddollFusion) &&
                    c.IsFaceup() && !c.IsDisabled()) &&
                Bot.GetMonsters().Any(c => c.IsCode(CardId.ElShaddollMeshachrer) &&
                    c.IsFaceup() && !c.IsDisabled());
        }

        private bool MustWaitForLevWendiSearch()
        {
            return Duel.Player == 0 && IsLevRouteActive() &&
                _waitingForLevWendi &&
                !Bot.HasInHand(CardId.ReeshaddollWendi);
        }

        private void AdvanceLevRoute(LevRouteStage stage)
        {
            if (stage > _levRouteStage)
                _levRouteStage = stage;
        }

        private bool CanStartLevPrimaryRoute()
        {
            return Bot.HasInExtra(CardId.ElShaddollApkallone) &&
                CanContinueLevThroughSquamata();
        }

        private bool CanContinueLevThroughSquamata()
        {
            return (Bot.HasInDeck(CardId.ShaddollSquamata) ||
                    Bot.HasInHand(CardId.ShaddollSquamata)) &&
                Bot.HasInDeck(CardId.TohushaddollGrysta) &&
                Bot.HasInExtra(CardId.ElShaddollMeshachrer);
        }

        private bool HasVoidRevealInHand()
        {
            return Bot.Hand.Any(IsShaddollCard);
        }

        private bool HasPreparedLevControlBoard()
        {
            return Bot.GetMonsters().Any(c => c.IsCode(CardId.ReeshaddollWendikuruhu)) &&
                Bot.GetMonsters().Any(c => c.IsCode(CardId.NaelshaddollAriel)) &&
                Bot.GetMonsters().Any(c => c.IsCode(CardId.ShaddollSquamata)) &&
                Bot.GetSpells().Any(c => c.IsCode(CardId.ShaddollSchism));
        }

        private bool IsArmageddonLevRoute()
        {
            return Bot.HasInMonstersZone(CardId.ElShaddollMeshachrer) &&
                Bot.Banished.Any(c => c.IsCode(CardId.ArmageddonKnight)) &&
                Bot.HasInExtra(CardId.InfernoidEvil) &&
                Bot.HasInDeck(CardId.VoidUnleashing);
        }

        private bool HasLevControlExtension()
        {
            if (!IsLevRouteActive() || !Bot.HasInHandOrInSpellZone(CardId.ElShaddollFusion))
                return false;
            if (Bot.HasInHand(CardId.ReeshaddollWendi))
                return true;
            return Bot.HasInDeck(CardId.ShaddollHedgehog) &&
                Bot.HasInDeck(CardId.ReeshaddollWendi);
        }

        private bool CanReachLevControlExtension()
        {
            return HasLevControlExtension() ||
                (IsLevRouteActive() && Bot.HasInHand(CardId.VoidUnleashing) &&
                 Bot.HasInDeck(CardId.ShaddollHedgehog) &&
                 Bot.HasInDeck(CardId.ReeshaddollWendi));
        }

        private bool HasImmediateFusionStarter()
        {
            return Bot.HasInHandOrInSpellZone(CardId.ShaddollFusion) ||
                Bot.HasInHandOrInSpellZone(CardId.ElShaddollFusion) ||
                Bot.HasInHandOrInSpellZone(CardId.LevShaddollFusion) ||
                Bot.HasInHandOrInSpellZone(CardId.VoidUnleashing);
        }

        private bool HasFusionResources(bool includeGrave)
        {
            int materialCount = Bot.Hand.Count(c => c.IsMonster()) + Bot.GetMonsterCount();
            if (includeGrave)
                materialCount += Bot.Graveyard.Count(c => c.IsMonster());
            return materialCount >= 2 && Bot.ExtraDeck.Any(c => ShaddollFusionIds.Contains(c.Id));
        }

        private bool HasSchismFusionResources()
        {
            int materialCount = Bot.GetMonsterCount() + Bot.Graveyard.Count(c => c.IsMonster());
            return materialCount >= 2 && Bot.ExtraDeck.Any(c => ShaddollFusionIds.Contains(c.Id));
        }

        private bool HasSchismWindaResources()
        {
            List<ClientCard> materials = Bot.GetMonsters()
                .Concat(Bot.Graveyard)
                .Where(c => c.IsMonster())
                .ToList();
            for (int i = 0; i < materials.Count; ++i)
            {
                for (int j = i + 1; j < materials.Count; ++j)
                {
                    ClientCard first = materials[i];
                    ClientCard second = materials[j];
                    if ((IsShaddollMonster(first) && second.HasAttribute(CardAttribute.Dark)) ||
                        (IsShaddollMonster(second) && first.HasAttribute(CardAttribute.Dark)))
                        return true;
                }
            }
            return false;
        }

        private bool HasShaddollSendTarget()
        {
            return ShaddollMainMonsterIds.Any(id => id != CardId.ShaddollSquamata && Bot.HasInDeck(id)) ||
                ShaddollSpellTrapIds.Any(Bot.HasInDeck);
        }

        private bool HasRecoverableSpellTrap()
        {
            return Bot.Graveyard.Any(c => (c.IsSpell() || c.IsTrap()) && IsShaddollCard(c));
        }

        private bool HasRecoverableShaddollCard()
        {
            return Bot.Graveyard.Any(IsShaddollCard) || ShaddollMainMonsterIds.Any(Bot.HasInDeck) ||
                ShaddollSpellTrapIds.Any(Bot.HasInDeck);
        }

        private bool HasUsefulMeshSearch()
        {
            return Bot.HasInDeck(CardId.VoidUnleashing) || ShaddollSpellTrapIds.Any(Bot.HasInDeck) ||
                ShaddollMainMonsterIds.Any(Bot.HasInDeck);
        }

        private bool IsMainPhase()
        {
            return Duel.Phase == DuelPhase.Main1 || Duel.Phase == DuelPhase.Main2;
        }

        private bool IsShaddollCard(ClientCard card)
        {
            return card != null && card.HasSetcode(ShaddollSetcode);
        }

        private bool IsShaddollMonster(ClientCard card)
        {
            return IsShaddollCard(card) && card.IsMonster();
        }

        private bool IsExtraDeckMonster(ClientCard card)
        {
            return card != null && card.IsMonster() && card.HasType(
                CardType.Fusion | CardType.Synchro | CardType.Xyz | CardType.Link);
        }

        private CardType GetBaseType(ClientCard card)
        {
            if (card.IsMonster()) return CardType.Monster;
            if (card.IsSpell()) return CardType.Spell;
            if (card.IsTrap()) return CardType.Trap;
            return 0;
        }

        private int GetPromptOwnerId()
        {
            ChainInfo solving = Duel.GetCurrentSolvingChainInfo();
            if (solving != null)
                return solving.ActivateId;
            ClientCard chainCard = Duel.GetCurrentChainCard();
            return chainCard == null ? 0 : chainCard.Id;
        }

        private bool PromptIs(int cardId)
        {
            ChainInfo solving = Duel.GetCurrentSolvingChainInfo();
            if (solving != null)
                return solving.IsActivateCode(cardId);
            ClientCard chainCard = Duel.GetCurrentChainCard();
            return chainCard != null && chainCard.IsCode(cardId);
        }

        private bool IsFusionPromptOwner()
        {
            return PromptIs(CardId.ShaddollFusion) ||
                PromptIs(CardId.ElShaddollFusion) ||
                PromptIs(CardId.ShaddollSchism) ||
                PromptIs(CardId.TohushaddollGrysta) ||
                PromptIs(CardId.BlazingCartesia) ||
                PromptIs(CardId.DracotailFaimena) ||
                PromptIs(CardId.TearlamentsScheiren);
        }

        private bool IsFusionRecoveryPrompt()
        {
            return PromptIs(CardId.ElShaddollConstruct) ||
                PromptIs(CardId.ElShaddollWinda) ||
                PromptIs(CardId.ElShaddollShekhinaga) ||
                PromptIs(CardId.ElShaddollAnoyatyllis);
        }
    }
}
