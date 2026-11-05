using System;
using System.Collections.Generic;
using System.Linq;
using WindBot;
using WindBot.Game;
using WindBot.Game.AI;
using YGOSharp.OCGWrapper.Enums;

namespace WindBot.Game.AI.Decks
{
    [Deck("Witchcraft2026", "AI_Witchcraft2026", "Test")]
    public partial class Witchcraft2026Executor : DefaultExecutor
    {
        public class CardId
        {
            public const int MadameVerre = 21522601;
            public const int Haine = 84523092;
            public const int Marka = 6071005;
            public const int Edel = 58139997;
            public const int Schmietta = 21744288;
            public const int Seed = 59106048;
            public const int MaidVerre = 22623509;
            public const int Pittore = 95245544;
            public const int AshBlossom = 14558127;
            public const int MaxxC = 23434538;
            public const int Potterie = 59851535;
            public const int Terracottan = 70686400;
            public const int MaidGenni = 7656689;
            public const int Genni = 64756282;
            public const int RainbowBridgeBifrost = 32236916;
            public const int Terraforming = 73628505;
            public const int Walpurgis = 32353566;
            public const int Creation = 57916305;
            public const int Lacrima = 73664385;
            public const int Unveiling = 70226289;
            public const int Bystreet = 83289866;
            public const int SpellPowerMastery = 38943357;
            public const int GazeOfTimaeus = 22283204;
            public const int ConfusionConfession = 35098357;
            public const int Celebration = 6958567;
            public const int Distortion = 69748261;

            public const int MadameRillie = 33475154;
            public const int RedEyesDarkDragoon = 37818794;
            public const int DarkMagicianOfDestruction = 59400890;
            public const int ViceMadame = 9603252;
            public const int Pupils = 69964858;
            public const int CosmicQuasarDragon = 37442336;
            public const int DespianLuluwalilith = 53971455;
            public const int ElfnoteJunePride = 5559570;
            public const int CharmerQuartet = 27519978;
            public const int Selene = 45819647;
            public const int SpLittleKnight = 29301451;
            public const int IpMasquerena = 65741789;
            public const int CrossSheep = 50277355;
        }

        private const int WitchcrafterSetcode = 0x128;
        private bool _spellActivatedThisTurn;
        private bool _normalSummonUsed;
        private bool _deckValidated;
        private int _pendingFusionTarget;
        private int _pendingSearchTarget;
        private readonly HashSet<int> _usedLowWitchcrafterEffects = new HashSet<int>();

        public Witchcraft2026Executor(GameAI ai, Duel duel)
            : base(ai, duel)
        {
            if (AI.Game != null && AI.Game.Connection != null)
                AI.Game.Connection.Disconnected += OnDisconnected;
            AddExecutor(ExecutorType.Activate, CardId.AshBlossom, DefaultAshBlossomAndJoyousSpring);
            AddExecutor(ExecutorType.Activate, CardId.MaxxC, DefaultMaxxC);

            AddExecutor(ExecutorType.Activate, CardId.Distortion, DistortionEffect);
            AddExecutor(ExecutorType.Activate, CardId.RedEyesDarkDragoon, DragoonEffect);
            AddExecutor(ExecutorType.Activate, CardId.Haine, HaineEffect);
            AddExecutor(ExecutorType.Activate, CardId.MadameVerre, MadameVerreEffect);
            AddExecutor(ExecutorType.Activate, CardId.ViceMadame, ViceMadameEffect);
            AddExecutor(ExecutorType.Activate, CardId.CharmerQuartet, CharmerQuartetEffect);

            AddExecutor(ExecutorType.Activate, CardId.Terraforming, FieldSpellAccessEffect);
            AddExecutor(ExecutorType.Activate, CardId.RainbowBridgeBifrost, FieldSpellAccessEffect);
            AddExecutor(ExecutorType.Activate, CardId.SpellPowerMastery, SpellPowerMasteryEffect);
            AddExecutor(ExecutorType.Activate, CardId.Creation, CreationEffect);
            AddExecutor(ExecutorType.Activate, CardId.Marka, MarkaEffect);
            AddExecutor(ExecutorType.Activate, CardId.Walpurgis, WalpurgisEffect);
            AddExecutor(ExecutorType.Activate, CardId.Unveiling, UnveilingEffect);
            AddExecutor(ExecutorType.Activate, CardId.MaidVerre, MaidVerreEffect);
            AddExecutor(ExecutorType.SpSummon, CardId.DarkMagicianOfDestruction, DarkMagicianOfDestructionSummon);
            AddExecutor(ExecutorType.Activate, CardId.DarkMagicianOfDestruction, DarkMagicianOfDestructionEffect);
            AddExecutor(ExecutorType.Activate, CardId.GazeOfTimaeus, GazeOfTimaeusEffect);

            AddExecutor(ExecutorType.Summon, CardId.Schmietta, LowWitchcrafterSummon);
            AddExecutor(ExecutorType.Summon, CardId.Pittore, LowWitchcrafterSummon);
            AddExecutor(ExecutorType.Summon, CardId.Genni, LowWitchcrafterSummon);
            AddExecutor(ExecutorType.Summon, CardId.Potterie, LowWitchcrafterSummon);
            AddExecutor(ExecutorType.Summon, CardId.Marka, LowWitchcrafterSummon);
            AddExecutor(ExecutorType.Activate, CardId.Schmietta, LowWitchcrafterEffect);
            AddExecutor(ExecutorType.Activate, CardId.Pittore, LowWitchcrafterEffect);
            AddExecutor(ExecutorType.Activate, CardId.Genni, LowWitchcrafterEffect);
            AddExecutor(ExecutorType.Activate, CardId.Potterie, LowWitchcrafterEffect);
            AddExecutor(ExecutorType.Activate, CardId.Marka, LowWitchcrafterEffect);
            AddExecutor(ExecutorType.Activate, CardId.Seed, SeedEffect);
            AddExecutor(ExecutorType.Activate, CardId.MaidGenni, MaidGenniEffect);
            AddExecutor(ExecutorType.Activate, CardId.Terracottan, TerracottanEffect);
            AddExecutor(ExecutorType.Activate, CardId.Edel, EdelEffect);
            AddExecutor(ExecutorType.Activate, CardId.Lacrima, LacrimaEffect);
            AddExecutor(ExecutorType.Activate, CardId.Celebration, CelebrationEffect);
            AddExecutor(ExecutorType.Activate, CardId.ConfusionConfession, ConfusionEffect);
            AddExecutor(ExecutorType.Activate, CardId.Pupils, PupilsEffect);

            AddExecutor(ExecutorType.SpSummon, CardId.CrossSheep, ExtraDeckSummon);
            AddExecutor(ExecutorType.SpSummon, CardId.CharmerQuartet, ExtraDeckSummon);
            AddExecutor(ExecutorType.SpSummon, CardId.Selene, ExtraDeckSummon);
            AddExecutor(ExecutorType.SpSummon, CardId.SpLittleKnight, ExtraDeckSummon);
            AddExecutor(ExecutorType.SpSummon, CardId.IpMasquerena, ExtraDeckSummon);
            AddExecutor(ExecutorType.Activate, WitchcrafterRecycle);
            AddExecutor(ExecutorType.SpellSet, CardId.Distortion, SetDistortion);
            AddExecutor(ExecutorType.Repos, DefaultMonsterRepos);
        }

        public override bool OnSelectHand()
        {
            return true;
        }

        public override void OnNewTurn()
        {
            _spellActivatedThisTurn = false;
            _normalSummonUsed = false;
            _pendingFusionTarget = 0;
            _pendingSearchTarget = 0;
            _strategicActionTarget = 0;
            _usedLowWitchcrafterEffects.Clear();
            RetireStrategicPlan();
            base.OnNewTurn();
        }

        public override void OnChainSolved(int chainIndex)
        {
            ChainInfo solving = Duel.GetCurrentSolvingChainInfo();
            if (solving != null && !Duel.IsCurrentSolvingChainNegated())
            {
                if (solving.ActivatePlayer == 0 && solving.RelatedCard != null && solving.RelatedCard.IsSpell())
                    _spellActivatedThisTurn = true;
            }
            RetireStrategicPlan();
            base.OnChainSolved(chainIndex);
        }

        public override void OnChainEnd()
        {
            _pendingFusionTarget = 0;
            _pendingSearchTarget = 0;
            RetireStrategicPlan();
            base.OnChainEnd();
        }

        public override void OnMove(ClientCard card, int previousController, int previousLocation,
            int currentController, int currentLocation)
        {
            if (card != null && (previousController == 0 || currentController == 0))
                RetireStrategicPlan();
            base.OnMove(card, previousController, previousLocation, currentController, currentLocation);
        }

        public override void OnSummoning()
        {
            _normalSummonUsed = true;
            RetireStrategicPlan();
            base.OnSummoning();
        }

        private bool ValidateDeck()
        {
            if (_deckValidated)
                return true;
            int[] required =
            {
                CardId.Walpurgis, CardId.Marka, CardId.Schmietta, CardId.Creation,
                CardId.Potterie, CardId.RainbowBridgeBifrost, CardId.Terraforming,
                CardId.Unveiling, CardId.Celebration, CardId.ConfusionConfession,
                CardId.Distortion
            };
            if (required.Any(id => !Bot.HasInDeck(id) && !Bot.HasInHand(id) &&
                !Bot.HasInGraveyard(id) && !Bot.HasInSpellZone(id)))
            {
                Logger.DebugWriteLine("Witchcraft2026: loaded deck does not match AI_Witchcraft2026.");
                return false;
            }
            _deckValidated = true;
            return true;
        }

        private bool DistortionEffect()
        {
            return Duel.LastChainPlayer == 1 && Bot.GetMonsters().Any(card =>
                card.IsFaceup() && card.HasSetcode(WitchcrafterSetcode) && card.Level >= 5);
        }

        private bool DragoonEffect()
        {
            if (Duel.LastChainPlayer == 1)
            {
                AI.SelectCard(GetDiscardPriority());
                return true;
            }
            return Util.GetBestEnemyCard() != null;
        }

        private bool HaineEffect()
        {
            ClientCard target = Util.GetProblematicEnemyCard();
            if (target == null)
                target = Util.GetBestEnemyCard();
            if (target == null || !HasSpellInHand())
                return false;
            AI.SelectCard(GetDiscardPriority());
            AI.SelectNextCard(target);
            return true;
        }

        private bool MadameVerreEffect()
        {
            if (Duel.LastChainPlayer == 1 && Enemy.GetMonsters().Any(card => card.IsFaceup() && !card.IsDisabled()))
                return true;
            return Duel.Phase == DuelPhase.Battle && HasSpellInHand();
        }

        private bool ViceMadameEffect()
        {
            bool recoverGuideDistortion = Bot.HasInMonstersZone(CardId.MadameRillie, true) &&
                Bot.HasInGraveyard(CardId.Distortion);
            if (recoverGuideDistortion)
            {
                if (ActivateDescription == Util.GetStringId(CardId.ViceMadame, 2))
                {
                    LogGuideAction("Vice-Madame chains to Edel and recovers Distortion");
                    return true;
                }
                return false;
            }
            if (ActivateDescription == Util.GetStringId(CardId.ViceMadame, 0))
                return Util.GetBestEnemyCard() != null;
            if (ActivateDescription == Util.GetStringId(CardId.ViceMadame, 1))
                return Bot.HasInDeck(CardId.Schmietta) || Bot.HasInDeck(CardId.MaidGenni);
            if (ActivateDescription == Util.GetStringId(CardId.ViceMadame, 2))
                return Bot.Graveyard.Any(card => card.HasSetcode(WitchcrafterSetcode) && card.IsSpell());
            return true;
        }

        private bool CharmerQuartetEffect()
        {
            return Bot.Graveyard.Count(card => card.IsMonster()) >= 2;
        }

        private bool FieldSpellAccessEffect()
        {
            if (Card.IsCode(CardId.RainbowBridgeBifrost) && !CanPayBifrostCost())
            {
                Logger.DebugWriteLine("Witchcraft2026 rejects Bifrost: the three legal Extra Deck costs are no longer available.");
                return false;
            }
            if (!Bot.HasInDeck(CardId.Walpurgis) ||
                !StrategicAllows(WitchcraftActionKind.ActivateFieldSpellAccess))
                return false;
            _pendingSearchTarget = ConsumeStrategicActionTarget(CardId.Walpurgis);
            _spellActivatedThisTurn = true;
            return true;
        }

        private bool SpellPowerMasteryEffect()
        {
            if (!StrategicAllows(WitchcraftActionKind.ActivateMastery))
                return false;
            int fallback = Bot.HasInHand(CardId.MaidVerre)
                ? CardId.MaidGenni : CardId.MaidVerre;
            _pendingSearchTarget = ConsumeStrategicActionTarget(fallback);
            _spellActivatedThisTurn = true;
            return true;
        }

        private bool CreationEffect()
        {
            if (GuideWalpurgisLineActive())
            {
                LogGuideAction("holds Creation until the Walpurgis guide line is complete");
                return false;
            }
            if (!StrategicAllows(WitchcraftActionKind.ActivateCreation))
                return false;
            int fallback = !Bot.HasInHand(CardId.Marka) && !Bot.HasInSpellZone(CardId.Walpurgis)
                ? CardId.Marka : !Bot.HasInHand(CardId.Schmietta) ? CardId.Schmietta : CardId.Terracottan;
            _pendingSearchTarget = ConsumeStrategicActionTarget(fallback);
            _spellActivatedThisTurn = true;
            return true;
        }

        private bool MarkaEffect()
        {
            if (Card.Location == CardLocation.Grave)
                return true;
            if (!StrategicAllows(WitchcraftActionKind.ActivateMarka))
                return false;
            int fallback = Bot.HasInSpellZone(CardId.Walpurgis, true)
                ? CardId.Bystreet : CardId.Walpurgis;
            _pendingSearchTarget = ConsumeStrategicActionTarget(fallback);
            return true;
        }

        private bool WalpurgisEffect()
        {
            if (Card.Location == CardLocation.SpellZone)
            {
                _pendingSearchTarget = ChooseWalpurgisSearchTarget();
                _spellActivatedThisTurn = true;
                LogGuideAction("Walpurgis searches " + _pendingSearchTarget);
                return true;
            }
            if (!StrategicAllows(WitchcraftActionKind.ActivateWalpurgis))
                return false;
            int fallback = ChooseWalpurgisSearchTarget();
            _pendingSearchTarget = ConsumeStrategicActionTarget(fallback);
            _spellActivatedThisTurn = true;
            return true;
        }

        private bool UnveilingEffect()
        {
            if (Card.Location == CardLocation.Grave)
                return Bot.GetMonsters().Any(card => card.IsFaceup() &&
                    card.HasSetcode(WitchcrafterSetcode));
            if (Duel.Player != 0 || Duel.Phase != DuelPhase.Main1)
                return false;
            IList<ClientCard> targets = Bot.Hand.Where(card => card.IsCode(
                CardId.Haine, CardId.MadameVerre, CardId.Edel, CardId.MaidVerre)).ToList();
            if (targets.Count == 0)
                return false;
            AI.SelectCard(OrderCardsById(targets, new[]
            {
                CardId.Haine, CardId.MadameVerre, CardId.MaidVerre, CardId.Edel
            }));
            return true;
        }

        private bool MaidVerreEffect()
        {
            if (Card.Location == CardLocation.Hand)
            {
                if (GuideWalpurgisLineActive())
                {
                    LogGuideAction("holds Maid Verre so it cannot replace the documented low-Witchcrafter ladder");
                    return false;
                }
                if (!_spellActivatedThisTurn)
                    return false;
                bool allowed = StrategicAllows(WitchcraftActionKind.SummonMaidVerre);
                ConsumeStrategicActionTarget(0);
                return allowed;
            }
            _pendingSearchTarget = !Bot.HasInSpellZone(CardId.Walpurgis) && Bot.HasInDeck(CardId.Walpurgis)
                ? CardId.Walpurgis : !Bot.HasInHand(CardId.Celebration) ? CardId.Celebration : CardId.Creation;
            return true;
        }

        private bool LowWitchcrafterSummon()
        {
            if (_normalSummonUsed)
                return false;
            int preferred = Bot.HasInSpellZone(CardId.Walpurgis, true, true)
                ? ChooseGuideLowId(Bot.Hand.Select(card => card.Id)) : 0;
            if (preferred != 0 && Card.Id != preferred)
                return false;
            WitchcraftActionKind kind = Card.IsCode(CardId.Schmietta)
                ? WitchcraftActionKind.NormalSchmietta : WitchcraftActionKind.NormalLowWitchcrafter;
            return StrategicAllows(kind);
        }

        private bool LowWitchcrafterEffect()
        {
            if (Card.Location == CardLocation.MonsterZone)
            {
                bool hasWalpurgis = Bot.HasInSpellZone(CardId.Walpurgis, true);
                if (_usedLowWitchcrafterEffects.Contains(Card.Id) ||
                    (!hasWalpurgis && !HasSpellInHand()) ||
                    !StrategicAllows(WitchcraftActionKind.ActivateLowWitchcrafter))
                    return false;
                int guideTarget = GetNextGuideLowWitchcrafter(Card.Id);
                _pendingSearchTarget = ConsumeStrategicActionTarget(
                    guideTarget != 0 ? guideTarget : CardId.Haine);
                if (!hasWalpurgis)
                    AI.SelectCard(GetDiscardPriority());
                _usedLowWitchcrafterEffects.Add(Card.Id);
                LogGuideAction(Card.Id + " recruits " + _pendingSearchTarget);
                return true;
            }
            if (Card.Location != CardLocation.Grave)
                return false;
            if (Card.IsCode(CardId.Genni) && Bot.HasInGraveyard(CardId.Creation) &&
                Bot.HasInDeck(CardId.Terracottan))
            {
                _pendingSearchTarget = CardId.Terracottan;
                LogGuideAction("Genni copies Creation to search Terracottan");
                return true;
            }
            if (GuideComboNeedsGraveResources())
            {
                if (Card.IsCode(CardId.Schmietta) &&
                    Bot.HasInMonstersZone(CardId.Pupils, true) &&
                    !Bot.HasInGraveyard(CardId.MadameVerre) &&
                    !Bot.Banished.Any(card => card.IsCode(CardId.MadameVerre)) &&
                    Bot.HasInDeck(CardId.MadameVerre))
                {
                    LogGuideAction("Schmietta sends Madame Verre after Pupils is established");
                    return true;
                }
                return false;
            }
            if (Card.IsCode(CardId.Pittore))
                return Bot.Hand.Count >= 2;
            if (Card.IsCode(CardId.Genni))
                return Bot.Graveyard.Any(card => card.IsCode(CardId.Celebration,
                    CardId.Creation, CardId.ConfusionConfession));
            if (Card.IsCode(CardId.Potterie))
                return Bot.Hand.Count == 0;
            return true;
        }

        private bool SeedEffect()
        {
            if (Card.Location == CardLocation.Grave)
                return Bot.Hand.Count >= 3 && Bot.Hand.Any(card => card.IsSpell());
            return true;
        }

        private bool MaidGenniEffect()
        {
            return Card.Location == CardLocation.Hand || Card.Location == CardLocation.MonsterZone;
        }

        private bool TerracottanEffect()
        {
            if (Card.Location == CardLocation.Hand)
            {
                if (!Bot.Graveyard.Any(card => card.HasSetcode(WitchcrafterSetcode)))
                    return false;
                bool guideRecovery = Bot.HasInGraveyard(CardId.Celebration) &&
                    Bot.HasInMonstersZone(CardId.RedEyesDarkDragoon, true) &&
                    Bot.Banished.Any(card => card.IsCode(CardId.Genni));
                if (!guideRecovery &&
                    !StrategicAllows(WitchcraftActionKind.ActivateTerracottan))
                    return false;
                _pendingSearchTarget = ConsumeStrategicActionTarget(CardId.Celebration);
                if (guideRecovery)
                    LogGuideAction("Terracottan recovers Celebration and Special Summons itself");
                return true;
            }
            if (Card.Location == CardLocation.MonsterZone)
            {
                _pendingFusionTarget = Bot.HasInExtra(CardId.Pupils) &&
                    Bot.HasInMonstersZone(CardId.RedEyesDarkDragoon, true)
                    ? CardId.Pupils : ChooseWitchcrafterFusion();
                if (_pendingFusionTarget == CardId.Pupils)
                    LogGuideAction("Terracottan fuses itself with Dragoon into Pupils");
                return _pendingFusionTarget != 0;
            }
            return false;
        }

        private bool EdelEffect()
        {
            if (ActivateDescription == Util.GetStringId(CardId.Edel, 1))
            {
                bool reviveDragoon = Bot.HasInGraveyard(CardId.RedEyesDarkDragoon);
                if (reviveDragoon)
                    LogGuideAction("Edel tributes itself to revive Dragoon");
                return reviveDragoon || Bot.Graveyard.Any(card =>
                    card.IsCode(CardId.MadameVerre, CardId.Haine));
            }
            if (ActivateDescription == Util.GetStringId(CardId.Edel, 0))
                return HasSpellInHand() && Bot.Hand.Any(card => card.HasSetcode(
                    WitchcrafterSetcode) && card.IsMonster());
            return Card.Location == CardLocation.Hand;
        }

        private bool LacrimaEffect()
        {
            return Card.Location == CardLocation.Hand || Card.Location == CardLocation.SpellZone;
        }

        private bool CelebrationEffect()
        {
            if (Bot.HasInExtra(CardId.ViceMadame) &&
                Bot.HasInMonstersZone(CardId.MadameRillie, true) &&
                Bot.HasInGraveyard(CardId.Terracottan) &&
                Bot.Banished.Any(card => card.IsCode(CardId.Marka)))
            {
                _pendingFusionTarget = CardId.ViceMadame;
                LogGuideAction("Celebration returns Terracottan and Marka to Fusion Summon Vice-Madame");
                return true;
            }
            bool canFuse = Bot.Graveyard.Concat(Bot.Banished).Count(card =>
                card.IsMonster() && card.HasRace(CardRace.SpellCaster)) >= 2;
            if (canFuse && StrategicAllows(WitchcraftActionKind.ActivateCelebration))
            {
                _pendingFusionTarget = ConsumeStrategicActionTarget(
                    ChooseWitchcrafterFusion());
                return true;
            }
            return Util.GetBestEnemyCard() != null && Bot.GetMonsters().Any(card =>
                card.HasSetcode(WitchcrafterSetcode));
        }

        private bool ConfusionEffect()
        {
            if (!StrategicAllows(WitchcraftActionKind.ActivateConfusion))
                return false;
            _pendingFusionTarget = ConsumeStrategicActionTarget(
                ChooseWitchcrafterFusion());
            return _pendingFusionTarget != 0;
        }

        private bool PupilsEffect()
        {
            if (Bot.HasInHand(CardId.Celebration) &&
                (Bot.HasInGraveyard(CardId.MadameVerre) ||
                Bot.Banished.Any(card => card.IsCode(CardId.MadameVerre))) &&
                Bot.HasInExtra(CardId.MadameRillie))
            {
                _pendingFusionTarget = CardId.MadameRillie;
                LogGuideAction("Pupils copies Celebration to Fusion Summon Madame Rilliona");
                return true;
            }
            return !GuideComboNeedsGraveResources();
        }

        private bool DarkMagicianOfDestructionSummon()
        {
            bool result = _spellActivatedThisTurn && Bot.GetMonsters().Any(card =>
                card.IsFaceup() && card.IsCode(CardId.Marka));
            if (result)
                LogGuideAction("banish Marka to Special Summon Dark Magician of Destruction");
            return result;
        }

        private bool DarkMagicianOfDestructionEffect()
        {
            _pendingSearchTarget = CardId.GazeOfTimaeus;
            LogGuideAction("Dark Magician of Destruction searches Gaze of Timaeus");
            return true;
        }

        private bool GazeOfTimaeusEffect()
        {
            _pendingFusionTarget = CardId.RedEyesDarkDragoon;
            bool result = Bot.HasInExtra(CardId.RedEyesDarkDragoon) &&
                (Bot.HasInGraveyard(CardId.DarkMagicianOfDestruction) ||
                Bot.HasInMonstersZone(CardId.DarkMagicianOfDestruction));
            if (result)
                LogGuideAction("Gaze returns Dark Magician of Destruction and summons Dragoon");
            return result;
        }

        private bool ExtraDeckSummon()
        {
            return Duel.Player == 0 && Duel.Phase == DuelPhase.Main1;
        }

        private bool WitchcrafterRecycle()
        {
            return Card != null && Card.Controller == 0 && Card.Location == CardLocation.Grave &&
                Card.HasSetcode(WitchcrafterSetcode) && Card.IsSpell();
        }

        private bool SetDistortion()
        {
            if (!Util.IsTurn1OrMain2() || !Bot.GetMonsters().Any(card =>
                card.IsFaceup() && card.HasSetcode(WitchcrafterSetcode) && card.Level >= 5))
                return false;
            bool allowed = Bot.HasInMonstersZone(CardId.MadameRillie, true) ||
                Bot.HasInMonstersZone(CardId.ViceMadame, true) ||
                StrategicAllows(WitchcraftActionKind.SetDistortion);
            ConsumeStrategicActionTarget(0);
            return allowed;
        }

        private bool CanPayBifrostCost()
        {
            return HasBifrostCost(Bot.ExtraDeck.Select(card => card.Id));
        }

        private static bool HasBifrostCost(IEnumerable<int> extraIds)
        {
            HashSet<int> ids = new HashSet<int>(extraIds);
            return ids.Contains(CardId.CosmicQuasarDragon) &&
                ids.Contains(CardId.DespianLuluwalilith) &&
                ids.Contains(CardId.ElfnoteJunePride);
        }

        private int ChooseWalpurgisSearchTarget()
        {
            if (!_normalSummonUsed && Bot.HasInDeck(CardId.Genni))
                return CardId.Genni;
            if (!Bot.HasInHand(CardId.MaidVerre) && Bot.HasInDeck(CardId.MaidVerre))
                return CardId.MaidVerre;
            if (Bot.HasInDeck(CardId.Schmietta))
                return CardId.Schmietta;
            return CardId.Terracottan;
        }

        private static int GetNextGuideLowWitchcrafter(int source)
        {
            if (source == CardId.Genni) return CardId.Schmietta;
            if (source == CardId.Schmietta) return CardId.Pittore;
            if (source == CardId.Pittore) return CardId.Potterie;
            if (source == CardId.Potterie) return CardId.Marka;
            return 0;
        }

        private static int ChooseGuideLowId(IEnumerable<int> handIds)
        {
            HashSet<int> ids = new HashSet<int>(handIds);
            if (ids.Contains(CardId.Genni)) return CardId.Genni;
            if (ids.Contains(CardId.Schmietta)) return CardId.Schmietta;
            if (ids.Contains(CardId.Pittore)) return CardId.Pittore;
            if (ids.Contains(CardId.Potterie)) return CardId.Potterie;
            return 0;
        }

        private bool GuideWalpurgisLineActive()
        {
            if (!Bot.HasInSpellZone(CardId.Walpurgis, true, true) ||
                Bot.HasInMonstersZone(CardId.ViceMadame, true))
                return false;
            if (!_normalSummonUsed &&
                (Bot.HasInDeck(CardId.Genni) || Bot.HasInHand(CardId.Genni)))
                return true;
            if (Bot.HasInMonstersZone(CardId.Genni, true) ||
                GuideComboNeedsGraveResources())
                return true;
            return Bot.HasInMonstersZone(CardId.MadameRillie, true) &&
                Bot.HasInHand(CardId.Celebration);
        }

        private static int GetWalpurgisDiscardRank(int cardId, bool guideLine)
        {
            if (guideLine && cardId == CardId.MadameVerre) return -100;
            if (cardId == CardId.RainbowBridgeBifrost) return 0;
            if (guideLine && cardId == CardId.MaidVerre) return 1;
            if (cardId == CardId.Distortion) return 2;
            if (cardId == CardId.Celebration) return 3;
            if (cardId == CardId.Bystreet) return 4;
            if (cardId == CardId.Unveiling) return 5;
            if (cardId == CardId.ConfusionConfession) return 6;
            if (cardId == CardId.Creation) return 7;
            if (cardId == CardId.Lacrima) return 8;
            if (cardId == CardId.Terraforming) return 9;
            if (cardId == CardId.SpellPowerMastery) return 10;
            if (cardId == CardId.GazeOfTimaeus) return guideLine ? 900 : 10;
            if (guideLine && cardId == CardId.Genni) return 1000;
            return 100;
        }

        private static int ChooseWalpurgisDiscardId(IEnumerable<int> cardIds,
            bool guideLine)
        {
            return cardIds.OrderBy(cardId => GetWalpurgisDiscardRank(cardId, guideLine))
                .ThenBy(cardId => cardId).FirstOrDefault();
        }

        private IList<ClientCard> OrderWalpurgisDiscards(IEnumerable<ClientCard> cards)
        {
            bool guideLine = GuideWalpurgisLineActive();
            return cards.OrderBy(card => GetWalpurgisDiscardRank(card.Id, guideLine))
                .ThenBy(card => card.Attack).ToList();
        }

        private bool GuideComboNeedsGraveResources()
        {
            return Bot.HasInSpellZone(CardId.Walpurgis, true, true) &&
                (Bot.HasInGraveyard(CardId.Genni) ||
                Bot.Banished.Any(card => card.IsCode(CardId.Genni))) &&
                (Bot.HasInExtra(CardId.DarkMagicianOfDestruction) ||
                Bot.HasInMonstersZone(CardId.RedEyesDarkDragoon, true) ||
                Bot.HasInHand(CardId.Terracottan) ||
                Bot.HasInMonstersZone(CardId.Terracottan, true) ||
                Bot.HasInMonstersZone(CardId.Pupils, true));
        }

        private static void LogGuideAction(string action)
        {
            Logger.DebugWriteLine("Witchcraft2026 guide action: " + action);
        }

        private int ChooseWitchcrafterFusion()
        {
            if (Bot.HasInExtra(CardId.MadameRillie) &&
                Bot.GetMonsters().Concat(Bot.Graveyard).Concat(Bot.Banished)
                    .Any(card => card.IsCode(CardId.MadameVerre)))
                return CardId.MadameRillie;
            if (Bot.HasInExtra(CardId.ViceMadame))
                return CardId.ViceMadame;
            if (Bot.HasInExtra(CardId.Pupils))
                return CardId.Pupils;
            return 0;
        }

        private bool HasSpellInHand()
        {
            return Bot.Hand.Any(card => card.IsSpell());
        }

        private static bool IsLowWitchcrafter(int cardId)
        {
            return cardId == CardId.Schmietta || cardId == CardId.Pittore ||
                cardId == CardId.Genni || cardId == CardId.Potterie ||
                cardId == CardId.Marka;
        }

        private IList<ClientCard> GetDiscardPriority()
        {
            int[] ids =
            {
                CardId.Distortion, CardId.Celebration, CardId.Bystreet,
                CardId.Unveiling, CardId.ConfusionConfession, CardId.Creation,
                CardId.Lacrima, CardId.RainbowBridgeBifrost, CardId.Terraforming
            };
            return OrderCardsById(Bot.Hand, ids);
        }

        private static IList<ClientCard> OrderCardsById(IEnumerable<ClientCard> cards, IEnumerable<int> ids)
        {
            List<int> order = ids.ToList();
            return cards.OrderBy(card =>
            {
                int index = order.IndexOf(card.Id);
                return index < 0 ? 100 : index;
            }).ThenBy(card => card.Attack).ToList();
        }

        private IList<ClientCard> OrderLowWitchcrafterCosts(IEnumerable<ClientCard> cards,
            int source)
        {
            int guideCost = source == CardId.Genni ? CardId.Creation :
                source == CardId.Schmietta ? CardId.Celebration :
                source == CardId.Pittore ? CardId.Distortion :
                source == CardId.Potterie ? CardId.ConfusionConfession : 0;
            int[] ids = guideCost == 0
                ? new[]
                {
                    CardId.Distortion, CardId.Celebration, CardId.Bystreet,
                    CardId.Unveiling, CardId.ConfusionConfession, CardId.Creation,
                    CardId.Lacrima, CardId.RainbowBridgeBifrost, CardId.Terraforming
                }
                : new[]
                {
                    guideCost, CardId.Bystreet, CardId.Unveiling, CardId.Lacrima,
                    CardId.RainbowBridgeBifrost, CardId.Terraforming
                };
            List<int> order = ids.ToList();
            bool canUseDeckCost = Bot.HasInSpellZone(CardId.Walpurgis, true);
            return cards.OrderBy(card => canUseDeckCost &&
                card.Location == CardLocation.Deck ? 0 : 20)
                .ThenBy(card =>
                {
                    int index = order.IndexOf(card.Id);
                    return index < 0 ? 100 : index;
                }).ThenBy(card => card.Attack).ToList();
        }

        private void OnDisconnected(Exception error)
        {
            RetireStrategicPlan();
        }

        private IList<ClientCard> OrderEnemyTargets(IEnumerable<ClientCard> cards)
        {
            return cards.OrderByDescending(card => card.IsFloodgate())
                .ThenByDescending(card => card.Attack).ToList();
        }

        public override IList<ClientCard> OnSelectCard(IList<ClientCard> cards, int min, int max,
            int hint, bool cancelable)
        {
            ClientCard activating = Duel.GetCurrentChainCard();
            ChainInfo solving = Duel.GetCurrentSolvingChainInfo();
            int source = solving != null && solving.ActivatePlayer == 0 && solving.RelatedCard != null
                ? solving.RelatedCard.Id : activating != null && activating.Controller == 0 ? activating.Id : 0;

            if (_pendingSearchTarget != 0 && (hint == HintMsg.AddToHand || hint == HintMsg.SpSummon))
            {
                IList<ClientCard> result = Util.CheckSelectCount(OrderCardsById(cards,
                    new[]
                    {
                        _pendingSearchTarget, CardId.Schmietta, CardId.Potterie,
                        CardId.MaidGenni, CardId.Haine
                    }),
                    cards, min, max);
                _pendingSearchTarget = 0;
                return result;
            }

            if (_pendingFusionTarget != 0 && hint == HintMsg.SpSummon)
                return Util.CheckSelectCount(OrderCardsById(cards, new[] { _pendingFusionTarget }),
                    cards, min, max);

            if (source == CardId.Walpurgis && (hint == HintMsg.Discard || hint == HintMsg.ToGrave))
                return Util.CheckSelectCount(OrderWalpurgisDiscards(cards),
                    cards, min, max);

            if (source == CardId.RainbowBridgeBifrost && hint == HintMsg.Remove)
                return Util.CheckSelectCount(OrderCardsById(cards, new[]
                {
                    CardId.ElfnoteJunePride, CardId.CosmicQuasarDragon,
                    CardId.DespianLuluwalilith
                }), cards, min, max);

            if (IsLowWitchcrafter(source) && hint == HintMsg.Discard)
                return Util.CheckSelectCount(OrderLowWitchcrafterCosts(cards, source),
                    cards, min, max);

            if (IsLowWitchcrafter(source) && hint == HintMsg.SpSummon)
                return Util.CheckSelectCount(OrderCardsById(cards, new[]
                {
                    GetNextGuideLowWitchcrafter(source), CardId.Haine,
                    CardId.MadameVerre, CardId.Edel,
                    CardId.MaidGenni, CardId.MaidVerre
                }), cards, min, max);

            if (source == CardId.Lacrima && hint == HintMsg.ToGrave)
                return Util.CheckSelectCount(OrderCardsById(cards, new[]
                {
                    CardId.MadameVerre, CardId.Celebration, CardId.Distortion,
                    CardId.Bystreet, CardId.Schmietta, CardId.Potterie
                }), cards, min, max);
            if (source == CardId.Lacrima && hint == HintMsg.SpSummon)
                return Util.CheckSelectCount(OrderCardsById(cards,
                    new[] { CardId.Haine, CardId.MadameVerre, CardId.Schmietta }),
                    cards, min, max);

            if (source == CardId.Unveiling && hint == HintMsg.SpSummon)
                return Util.CheckSelectCount(OrderCardsById(cards, new[]
                {
                    CardId.Haine, CardId.MadameVerre, CardId.MaidVerre, CardId.Edel
                }), cards, min, max);

            if (source == CardId.Schmietta && hint == HintMsg.ToGrave)
                return Util.CheckSelectCount(OrderCardsById(cards, new[]
                {
                    Bot.HasInMonstersZone(CardId.Pupils, true)
                        ? CardId.MadameVerre : CardId.Celebration,
                    CardId.MadameVerre, CardId.Celebration,
                    CardId.Distortion, CardId.Bystreet
                }), cards, min, max);

            if (source == CardId.Genni && hint == HintMsg.Remove)
                return Util.CheckSelectCount(OrderCardsById(cards, new[]
                {
                    CardId.Creation, CardId.Celebration, CardId.ConfusionConfession
                }), cards, min, max);

            if (source == CardId.Potterie && hint == HintMsg.AddToHand)
                return Util.CheckSelectCount(OrderCardsById(cards, new[]
                {
                    CardId.Walpurgis, CardId.Creation, CardId.Celebration,
                    CardId.Unveiling, CardId.Distortion
                }), cards, min, max);

            if (source == CardId.MaidGenni && hint == HintMsg.SpSummon)
                return Util.CheckSelectCount(OrderCardsById(cards, Duel.Player == 1
                    ? new[] { CardId.Seed, CardId.Haine, CardId.MadameVerre }
                    : new[] { CardId.Schmietta, CardId.Genni, CardId.Marka }), cards, min, max);

            if (source == CardId.Terracottan && hint == HintMsg.AddToHand)
                return Util.CheckSelectCount(OrderCardsById(cards, new[]
                {
                    CardId.Celebration, CardId.Distortion, CardId.ConfusionConfession, CardId.Creation
                }), cards, min, max);

            if (source == CardId.MadameRillie && hint == HintMsg.SpSummon)
                return Util.CheckSelectCount(OrderCardsById(cards, new[]
                {
                    CardId.Haine, CardId.Edel, CardId.MaidGenni, CardId.MadameVerre
                }), cards, min, max);

            if (source == CardId.Edel && hint == HintMsg.SpSummon)
                return Util.CheckSelectCount(OrderCardsById(cards, new[]
                {
                    CardId.RedEyesDarkDragoon, CardId.MadameVerre, CardId.Haine
                }), cards, min, max);

            if (source == CardId.Pupils && hint == HintMsg.Confirm)
                return Util.CheckSelectCount(OrderCardsById(cards, new[]
                {
                    CardId.Celebration, CardId.Creation, CardId.ConfusionConfession
                }), cards, min, max);

            if (source == CardId.Celebration && hint == HintMsg.Destroy)
            {
                List<ClientCard> ordered = cards.Where(card => card.Controller == 0)
                    .OrderBy(card => card.IsCode(CardId.Seed, CardId.Marka,
                        CardId.Potterie, CardId.Pittore, CardId.Genni) ? 0 : 20)
                    .Concat(OrderEnemyTargets(cards.Where(card => card.Controller == 1)))
                    .ToList();
                return Util.CheckSelectCount(ordered, cards, min, max);
            }

            if (source == CardId.ViceMadame && hint == HintMsg.Destroy)
                return Util.CheckSelectCount(OrderEnemyTargets(cards), cards, min, max);
            if (source == CardId.ViceMadame && hint == HintMsg.SpSummon)
                return Util.CheckSelectCount(OrderCardsById(cards,
                    new[]
                    {
                        CardId.Schmietta, CardId.Potterie, CardId.MaidGenni, CardId.Seed
                    }), cards, min, max);
            if (source == CardId.ViceMadame && hint == HintMsg.AddToHand)
                return Util.CheckSelectCount(OrderCardsById(cards,
                    new[] { CardId.Distortion, CardId.Celebration, CardId.Creation }), cards, min, max);

            if (source == CardId.DarkMagicianOfDestruction && hint == HintMsg.AddToHand)
                return Util.CheckSelectCount(OrderCardsById(cards,
                    new[] { CardId.GazeOfTimaeus }), cards, min, max);
            if (source == CardId.DarkMagicianOfDestruction &&
                hint == HintMsg.Remove)
                return Util.CheckSelectCount(OrderCardsById(cards, new[] { CardId.Marka }), cards, min, max);

            if (source == CardId.GazeOfTimaeus && hint == HintMsg.ToDeck)
                return Util.CheckSelectCount(OrderCardsById(cards,
                    new[] { CardId.DarkMagicianOfDestruction }), cards, min, max);

            if (source == CardId.MadameVerre && hint == HintMsg.Confirm)
            {
                List<ClientCard> spells = cards.Where(card => card.IsSpell()).Take(max).ToList();
                return Util.CheckSelectCount(spells, cards, min, max);
            }

            if (hint == HintMsg.Destroy || hint == HintMsg.Disable || hint == HintMsg.ReturnToHand)
                return Util.CheckSelectCount(OrderEnemyTargets(cards), cards, min, max);

            return base.OnSelectCard(cards, min, max, hint, cancelable);
        }

        public override IList<ClientCard> OnSelectFusionMaterial(IList<ClientCard> cards, int min, int max)
        {
            List<ClientCard> ordered = cards.OrderBy(card =>
            {
                if (_pendingFusionTarget == CardId.Pupils &&
                    card.IsCode(CardId.Terracottan, CardId.RedEyesDarkDragoon))
                    return card.IsCode(CardId.Terracottan) ? -30 : -29;
                if (_pendingFusionTarget == CardId.MadameRillie &&
                    card.IsCode(CardId.MadameVerre, CardId.Genni, CardId.Schmietta))
                    return card.IsCode(CardId.MadameVerre) ? -30 :
                        card.IsCode(CardId.Genni) ? -29 : -28;
                if (_pendingFusionTarget == CardId.ViceMadame &&
                    card.IsCode(CardId.Terracottan, CardId.Marka))
                    return card.IsCode(CardId.Terracottan) ? -30 : -29;
                if (card.Controller == 1)
                    return -10;
                if (card.IsCode(CardId.Marka, CardId.Seed, CardId.Potterie,
                    CardId.Pittore, CardId.Genni))
                    return 0;
                if (card.IsCode(CardId.MadameVerre, CardId.Haine, CardId.RedEyesDarkDragoon))
                    return 50;
                return 10;
            }).ThenBy(card => card.Attack).ToList();
            return Util.CheckSelectCount(ordered, cards, min, max);
        }

        public override IList<ClientCard> OnSelectLinkMaterial(IList<ClientCard> cards, int min, int max)
        {
            List<ClientCard> ordered = cards.OrderBy(card =>
                card.IsCode(CardId.Marka, CardId.Seed, CardId.Potterie,
                    CardId.Pittore, CardId.Genni) ? 0 :
                card.IsCode(CardId.MadameVerre, CardId.Haine, CardId.RedEyesDarkDragoon) ? 50 : 10)
                .ThenBy(card => card.Attack).ToList();
            return Util.CheckSelectCount(ordered, cards, min, max);
        }

        public override int OnSelectOption(IList<int> options)
        {
            int lacrimaSend = options.IndexOf(Util.GetStringId(CardId.Lacrima, 1));
            int lacrimaSummon = options.IndexOf(Util.GetStringId(CardId.Lacrima, 2));
            if (lacrimaSend >= 0 || lacrimaSummon >= 0)
                return Duel.Player == 1 && lacrimaSummon >= 0 ? lacrimaSummon :
                    lacrimaSend >= 0 ? lacrimaSend : lacrimaSummon;

            int celebrationDestroy = options.IndexOf(Util.GetStringId(CardId.Celebration, 2));
            int celebrationFusion = options.IndexOf(Util.GetStringId(CardId.Celebration, 3));
            if (celebrationDestroy >= 0 || celebrationFusion >= 0)
                return _pendingFusionTarget != 0 && celebrationFusion >= 0
                    ? celebrationFusion : celebrationDestroy >= 0 ? celebrationDestroy : celebrationFusion;

            int pupilsSearch = options.IndexOf(Util.GetStringId(CardId.Pupils, 2));
            int pupilsCopy = options.IndexOf(Util.GetStringId(CardId.Pupils, 3));
            if (pupilsSearch >= 0 || pupilsCopy >= 0)
                return Bot.Hand.Any(card => card.IsCode(CardId.Celebration)) && pupilsCopy >= 0
                    ? pupilsCopy : pupilsSearch >= 0 ? pupilsSearch : pupilsCopy;

            return base.OnSelectOption(options);
        }

        public override bool OnSelectYesNo(int desc)
        {
            if (desc == Util.GetStringId(CardId.SpellPowerMastery, 0))
                return true;
            return base.OnSelectYesNo(desc);
        }

        public override CardPosition OnSelectPosition(int cardId, IList<CardPosition> positions)
        {
            if (Duel.Turn <= 1 && cardId != CardId.RedEyesDarkDragoon && cardId != CardId.Haine &&
                positions.Contains(CardPosition.FaceUpDefence))
                return CardPosition.FaceUpDefence;
            return base.OnSelectPosition(cardId, positions);
        }
    }
}
