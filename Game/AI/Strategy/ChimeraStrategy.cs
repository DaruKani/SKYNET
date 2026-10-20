using System;
using System.Collections.Generic;
using System.Linq;
using YGOSharp.OCGWrapper.Enums;

namespace Skynet.Game.AI.Strategy
{
    /// <summary>
    /// Shared Chimera effects and a bounded two-material Fusion route. Legal
    /// offers and activation snapshots own the decisions; no Lua is simulated.
    /// The compact board keeps Mirror and Coatl available as graveyard answers.
    /// </summary>
    public sealed class ChimeraStrategy
    {
        public const int MirrorSwordknight = 28954097;
        public const int CornfieldCoatl = 92565383;
        public const int Gazelle = 23076639;
        public const int BigWingedBerfomet = 55461744;
        public const int ChimeraFusion = 63136489;
        public const int KingPhantom = 1769875;
        public const int BerfometFusion = 69601012;
        private readonly Duel _duel;
        private bool _normalSummonCommitted;
        private bool _coatlSearchCommitted;
        private ChainInfo _fusionSource;
        private int _fusionTarget;

        public ChimeraStrategy(Duel duel)
        {
            if (duel == null)
                throw new ArgumentNullException("duel");
            _duel = duel;
        }

        public void OnActionCommitted(LegalAction action)
        {
            ResetPendingProcedure();
            if (action == null || action.Card == null || action.Card.Controller != 0)
                return;
            if (action.Type == ExecutorType.Summon || action.Type == ExecutorType.MonsterSet)
                _normalSummonCommitted = true;
            if (action.Type == ExecutorType.Activate && action.Card.IsOriginalCode(CornfieldCoatl)
                && action.Card.Location == CardLocation.Hand)
                _coatlSearchCommitted = true;
        }

        public void ResetTurnState()
        {
            _normalSummonCommitted = false;
            _coatlSearchCommitted = false;
            ResetPendingProcedure();
        }

        public void ResetPendingProcedure()
        {
            _fusionSource = null;
            _fusionTarget = 0;
        }

        public bool TryEvaluate(LegalAction action, out ActionEvaluation evaluation, out string reason)
        {
            evaluation = null;
            reason = null;
            if (action == null || action.Card == null || action.Card.Controller != 0)
                return false;
            ClientCard card = action.Card;
            int code = card.GetNonAltartCode();
            if (code != MirrorSwordknight && code != CornfieldCoatl && code != Gazelle
                && code != BigWingedBerfomet && code != ChimeraFusion
                && code != KingPhantom && code != BerfometFusion)
                return false;
            ClientField bot = _duel.Fields[0];
            bool hasBoss = bot.GetMonsters().Any(IsReviewedBoss);
            reason = "This Chimera offer has no reviewed continuation in the current visible state.";

            if (action.Type == ExecutorType.MonsterSet && hasBoss)
            {
                reason = "Keep the Chimera follow-up in hand instead of setting it behind the established boss.";
                return true;
            }
            if (action.Type == ExecutorType.SpellSet && code == ChimeraFusion)
            {
                if (bot.GetSpellCountWithoutField() < 4)
                    evaluation = Decision(0.9, "Set Chimera Fusion for a later legal fusion window; retain the established board.", "protection");
                return true;
            }
            if (action.Type == ExecutorType.Summon)
            {
                if (hasBoss)
                {
                    reason = "Keep the follow-up body in hand while the compact Chimera board is established.";
                    return true;
                }
                if (code == MirrorSwordknight && (bot.HasInDeck(BigWingedBerfomet) || bot.HasInDeck(Gazelle)))
                    evaluation = Decision(6.5, "Normal summon Mirror Swordknight to recruit a reviewed Chimera searcher from the deck.", "develop", "synergy");
                else if (code == Gazelle)
                    evaluation = Decision(6.4, "Normal summon Gazelle to find Chimera Fusion or a missing Fiend material.", "develop", "synergy");
                else if (code == BigWingedBerfomet)
                {
                    IList<ClientCard> monsters = bot.GetMonsters();
                    if (monsters.Count == 0 || monsters.Min(SkynetPolicy.CardValue) <= SkynetPolicy.CardValue(card))
                        evaluation = Decision(5.5, "Use the offered Berfomet summon with affordable tribute resources to search a Beast and Chimera Fusion.", "develop", "advantage");
                    else
                        reason = "Conserve the more valuable visible body instead of tributing it for Berfomet's search.";
                }
                return true;
            }
            if (action.Type != ExecutorType.Activate)
                return false;

            int description = action.Description;
            bool firstEffect = description == -1 || description == code * 16;
            bool secondEffect = description == -1 || description == code * 16 + 1;
            if (code == MirrorSwordknight)
            {
                if (card.Location == CardLocation.MonsterZone && firstEffect
                    && (description != -1 || _duel.CurrentChain.Count == 0)
                    && (bot.HasInDeck(BigWingedBerfomet) || bot.HasInDeck(Gazelle)) && !hasBoss)
                    evaluation = Decision(7.5, "Tribute Mirror to recruit Berfomet or Gazelle; its graveyard negate remains available after the fusion.", "develop", "synergy");
                else if ((card.Location == CardLocation.Grave || card.Location == CardLocation.MonsterZone)
                    && secondEffect && (description != -1 || card.Location == CardLocation.Grave)
                    && EnemyLink(true) != null)
                    evaluation = Decision(8.2, "Banish Mirror to negate the offered opposing on-field monster effect; the core supplies the legal response window.", "interaction", "protection");
            }
            else if (code == CornfieldCoatl)
            {
                if (card.Location == CardLocation.Hand && firstEffect
                    && (bot.HasInDeck(MirrorSwordknight) || bot.HasInDeck(Gazelle) || bot.HasInDeck(BigWingedBerfomet)))
                    evaluation = Decision(5.7, "Discard Coatl to find the missing starter or next-turn follow-up and prepare its graveyard protection.", "advantage", "synergy");
                else if ((card.Location == CardLocation.Grave || card.Location == CardLocation.MonsterZone)
                    && secondEffect && EnemyLink(false) != null)
                    evaluation = Decision(8.4, "Banish Coatl in the offered targeting-response window to negate the effect and attempt to destroy its source.", "interaction", "protection");
            }
            else if (code == Gazelle)
            {
                if (card.Location == CardLocation.MonsterZone && firstEffect)
                    evaluation = Decision(7.4, "Use Gazelle's summon trigger to find the missing Fusion spell or Fiend material.", "advantage", "synergy");
                else if (card.Location == CardLocation.Grave && secondEffect)
                    evaluation = Decision(7.3, "Use Gazelle's Fusion-material trigger to find Coatl or a follow-up Mirror.", "advantage", "recovery");
            }
            else if (code == BigWingedBerfomet)
            {
                if (card.Location == CardLocation.MonsterZone && firstEffect)
                    evaluation = Decision(7.6, "Search one level-four Beast and Chimera Fusion; accept the core's Fusion-only Extra Deck restriction.", "advantage", "synergy");
                else if (card.Location == CardLocation.Grave && secondEffect && !hasBoss)
                    evaluation = Decision(4, "Recover an offered Illusion when the Chimera board was lost.", "recovery", "develop");
                else if (card.Location == CardLocation.Grave && secondEffect)
                    reason = "Keep the Illusion in the graveyard as protection instead of reviving it without a further reviewed fusion goal.";
            }
            else if (code == ChimeraFusion)
            {
                if (card.Location == CardLocation.Grave && secondEffect)
                    evaluation = Decision(4.5, "Recover Chimera Fusion to hand for a later turn or response; do not select the vanilla-monster branch.", "recovery");
                else if ((card.Location == CardLocation.Hand || card.Location == CardLocation.SpellZone) && firstEffect)
                {
                    bool threatened = bot.GetMonsters().Any(c => IsReviewedBoss(c) && IsTargetedByEnemy(c));
                    if (hasBoss && !threatened)
                        reason = "Conserve Chimera Fusion and materials while the current Chimera board is healthy.";
                    else if (PreferredFusion() != 0)
                        evaluation = Decision(threatened ? 7.8 : 6.3, threatened
                            ? "Use a legal replacement Fusion to move a targeted Chimera out of the pending effect's reach; resolution still belongs to the core."
                            : "Fuse the visible Beast/Fiend or two distinct Chimera races into a reviewed boss.", "develop", "synergy");
                }
            }
            else if (code == KingPhantom || code == BerfometFusion)
            {
                if (card.Location == CardLocation.MonsterZone && firstEffect)
                    evaluation = Decision(code == KingPhantom ? 7.2 : 7.4, code == KingPhantom
                        ? "Register King's Fusion trigger for a random enemy hand send during the End Phase; no hidden card is chosen."
                        : "Use fusion Berfomet to send a missing Mirror or Coatl to the graveyard for protection.", "advantage", "protection");
                else if (card.Location == CardLocation.Grave && secondEffect && _duel.Player == 1)
                    evaluation = Decision(5, code == KingPhantom
                        ? "Banish the fallen King to revive an offered graveyard resource on the opponent's turn."
                        : "Banish the fallen fusion Berfomet to recover an offered banished resource on the opponent's turn.", "recovery", "develop");
            }
            if (evaluation != null)
                reason = evaluation.Reason;
            return true;
        }

        public IList<ClientCard> SelectCard(IList<ClientCard> cards, int min, int max, int hint, bool cancelable)
        {
            if (cards == null || min < 0 || max < min || cards.Count < min || cards.Any(c => c == null))
                return null;
            ChainInfo source = CurrentSource();
            if (source == null || source.ActivatePlayer != 0)
                return null;
            bool resolving = _duel.GetCurrentSolvingChainInfo() != null;
            IEnumerable<ClientCard> ordered = null;
            int count = Math.Min(max, Math.Max(1, min));
            ClientField bot = _duel.Fields[0];

            if (source.IsActivateCode(MirrorSwordknight) && resolving
                && source.ActivateLocation == CardLocation.MonsterZone && hint == HintMsg.SpSummon
                && IsEffect(source, MirrorSwordknight, 0))
                ordered = cards.OrderByDescending(c => c.IsOriginalCode(BigWingedBerfomet) ? 3
                    : c.IsOriginalCode(Gazelle) ? 2 : 0).ThenByDescending(SkynetPolicy.CardValue);
            else if (source.IsActivateCode(CornfieldCoatl) && resolving
                && source.ActivateLocation == CardLocation.Hand && hint == HintMsg.AddToHand
                && IsEffect(source, CornfieldCoatl, 0))
            {
                bool missingStarter = !_normalSummonCommitted
                    && !bot.Hand.Any(c => c.IsOriginalCode(MirrorSwordknight) || c.IsOriginalCode(Gazelle));
                var resources = bot.Hand.Concat(bot.GetMonsters()).ToList();
                bool missingFiend = !bot.GetMonsters().Any(IsReviewedBoss)
                    && bot.Hand.Concat(bot.GetSpells()).Any(c => c.IsOriginalCode(ChimeraFusion))
                    && resources.Any(c => (MaterialRace(c) & (int)CardRace.Beast) != 0)
                    && !resources.Any(c => (MaterialRace(c) & (int)CardRace.Fiend) != 0);
                int preferred = missingFiend ? BigWingedBerfomet : missingStarter ? MirrorSwordknight : Gazelle;
                ordered = cards.OrderByDescending(c => c.IsOriginalCode(preferred) ? 3
                    : c.IsOriginalCode(Gazelle) ? 2 : c.IsOriginalCode(BigWingedBerfomet) ? 1 : 0);
            }
            else if (source.IsActivateCode(Gazelle) && resolving && hint == HintMsg.AddToHand)
            {
                if (source.ActivateLocation == CardLocation.MonsterZone && IsEffect(source, Gazelle, 0))
                {
                    bool haveFusion = bot.Hand.Concat(bot.GetSpells()).Any(c => c.IsOriginalCode(ChimeraFusion));
                    ordered = cards.OrderByDescending(c => c.IsOriginalCode(haveFusion ? BigWingedBerfomet : ChimeraFusion) ? 3
                        : c.IsOriginalCode(ChimeraFusion) ? 2 : 0);
                }
                else if (source.ActivateLocation == CardLocation.Grave && IsEffect(source, Gazelle, 1))
                {
                    int preferred = !_coatlSearchCommitted ? CornfieldCoatl : MirrorSwordknight;
                    ordered = cards.OrderByDescending(c => c.IsOriginalCode(preferred) ? 3
                        : c.IsOriginalCode(CornfieldCoatl) ? 2 : c.IsOriginalCode(MirrorSwordknight) ? 1 : 0);
                }
            }
            else if (source.IsActivateCode(BigWingedBerfomet))
            {
                if (resolving && source.ActivateLocation == CardLocation.MonsterZone
                    && hint == HintMsg.AddToHand && IsEffect(source, BigWingedBerfomet, 0))
                {
                    // SelectSubGroup is iterative: after the first choice min
                    // can be zero. Still choose the other offered category, so
                    // the search obtains both Gazelle and Fusion when possible.
                    var result = new List<ClientCard>();
                    ClientCard beast = cards.Where(c => !c.IsOriginalCode(ChimeraFusion))
                        .OrderByDescending(c => c.IsOriginalCode(Gazelle)).ThenByDescending(SkynetPolicy.CardValue).FirstOrDefault();
                    ClientCard fusion = cards.FirstOrDefault(c => c.IsOriginalCode(ChimeraFusion));
                    if (beast != null && result.Count < max)
                        result.Add(beast);
                    if (fusion != null && result.Count < max)
                        result.Add(fusion);
                    return result.Count >= min ? result : null;
                }
                if (!resolving && source.ActivateLocation == CardLocation.Grave
                    && hint == HintMsg.SpSummon && IsEffect(source, BigWingedBerfomet, 1))
                    ordered = cards.OrderByDescending(c => c.IsOriginalCode(MirrorSwordknight) ? 2
                        : c.IsOriginalCode(CornfieldCoatl) ? 1 : 0);
            }
            else if (source.IsActivateCode(ChimeraFusion) && resolving
                && (source.ActivateLocation == CardLocation.Hand || source.ActivateLocation == CardLocation.SpellZone)
                && IsEffect(source, ChimeraFusion, 0))
            {
                if (hint == HintMsg.SpSummon && min == 1 && max == 1
                    && cards.All(c => c.Controller == 0 && c.Location == CardLocation.Extra))
                {
                    ClientCard selected = cards.FirstOrDefault(c => c.IsOriginalCode(KingPhantom))
                        ?? cards.FirstOrDefault(c => c.IsOriginalCode(BerfometFusion));
                    if (selected == null)
                        return null;
                    _fusionSource = source;
                    _fusionTarget = selected.GetNonAltartCode();
                    return new List<ClientCard> { selected };
                }
                if (hint == HintMsg.FusionMaterial && ReferenceEquals(source, _fusionSource)
                    && (_fusionTarget == KingPhantom || _fusionTarget == BerfometFusion))
                {
                    if (min == 0 && cancelable)
                        return new List<ClientCard>();
                    if (min >= 2)
                    {
                        IList<ClientCard> pair = FindPair(_fusionTarget, cards);
                        return pair != null && pair.Count >= min && pair.Count <= max ? pair : null;
                    }
                    ordered = cards.OrderBy(MaterialValue);
                }
            }
            else if (source.IsActivateCode(BerfometFusion) && resolving
                && source.ActivateLocation == CardLocation.MonsterZone && hint == HintMsg.ToGrave
                && IsEffect(source, BerfometFusion, 0))
            {
                bool haveMirror = bot.Graveyard.Any(c => c.IsOriginalCode(MirrorSwordknight));
                bool haveCoatl = bot.Graveyard.Any(c => c.IsOriginalCode(CornfieldCoatl));
                ordered = cards.OrderByDescending(c => c.IsOriginalCode(MirrorSwordknight) && !haveMirror ? 4
                    : c.IsOriginalCode(CornfieldCoatl) && !haveCoatl ? 3
                    : c.IsOriginalCode(KingPhantom) ? 2 : 0).ThenBy(SkynetPolicy.CardValue);
            }
            else if (!resolving && hint == HintMsg.SpSummon && source.ActivateLocation == CardLocation.Grave
                && ((source.IsActivateCode(KingPhantom) && IsEffect(source, KingPhantom, 1))
                    || (source.IsActivateCode(BerfometFusion) && IsEffect(source, BerfometFusion, 1))))
            {
                bool haveBoss = bot.GetMonsters().Any(IsReviewedBoss);
                ordered = cards.OrderByDescending(c => IsReviewedBoss(c) && !haveBoss ? 4
                    : c.IsOriginalCode(BigWingedBerfomet) ? 3 : c.IsOriginalCode(Gazelle) ? 2
                    : c.IsOriginalCode(MirrorSwordknight) ? 1 : 0).ThenByDescending(SkynetPolicy.CardValue);
            }
            return ordered == null ? null : ordered.Take(count).ToList();
        }

        public int SelectOption(IList<int> options)
        {
            ChainInfo source = CurrentSource();
            if (options == null || source == null || source.ActivatePlayer != 0
                || !source.IsActivateCode(ChimeraFusion) || source.ActivateLocation != CardLocation.Grave
                || !IsEffect(source, ChimeraFusion, 1))
                return -1;
            // String offset 1 identifies the GY activation; offset 2 is the
            // actual return-to-hand option requested inside its target function.
            return options.IndexOf(ChimeraFusion * 16 + 2);
        }

        public bool PreserveGraveyardProtection(ClientCard card)
        {
            return card != null && card.Controller == 0 && card.Location == CardLocation.Grave
                && (card.IsOriginalCode(MirrorSwordknight) || card.IsOriginalCode(CornfieldCoatl))
                && _duel.Fields[0].GetMonsters().Any(IsReviewedBoss);
        }

        private int PreferredFusion()
        {
            ClientField bot = _duel.Fields[0];
            IEnumerable<ClientCard> materials = bot.Hand.Concat(bot.GetMonsters());
            if (bot.HasInExtra(KingPhantom) && FindPair(KingPhantom, materials) != null)
                return KingPhantom;
            if (bot.HasInExtra(BerfometFusion) && FindPair(BerfometFusion, materials) != null)
                return BerfometFusion;
            return 0;
        }

        private IList<ClientCard> FindPair(int fusion, IEnumerable<ClientCard> candidates)
        {
            var cards = candidates.Where(c => c != null && c.Controller == 0
                && (c.Location == CardLocation.Hand || c.Location == CardLocation.MonsterZone)
                && CardSemantics.HasType(c, CardType.Monster)).Distinct().OrderBy(MaterialValue).ToList();
            IList<ClientCard> result = null;
            double best = double.MaxValue;
            for (int i = 0; i < cards.Count; ++i)
            {
                for (int j = i + 1; j < cards.Count; ++j)
                {
                    int left = MaterialRace(cards[i]);
                    int right = MaterialRace(cards[j]);
                    int beast = (int)CardRace.Beast;
                    int fiend = (int)CardRace.Fiend;
                    int races = beast | fiend | (int)CardRace.Illusion;
                    bool valid = fusion == KingPhantom
                        ? ((left & beast) != 0 && (right & fiend) != 0) || ((left & fiend) != 0 && (right & beast) != 0)
                        : (left & races) != 0 && (right & races) != 0 && (left & right) == 0;
                    double value = MaterialValue(cards[i]) + MaterialValue(cards[j]);
                    if (valid && value < best)
                    {
                        result = new List<ClientCard> { cards[i], cards[j] };
                        best = value;
                    }
                }
            }
            return result;
        }

        private double MaterialValue(ClientCard card)
        {
            double value = SkynetPolicy.CardValue(card);
            if (card.IsOriginalCode(Gazelle))
                value -= 6;
            else if (card.IsOriginalCode(BigWingedBerfomet))
                value -= 4;
            else if (card.IsOriginalCode(CornfieldCoatl) || card.IsOriginalCode(MirrorSwordknight))
                value -= 1;
            if (IsReviewedBoss(card))
                value += IsTargetedByEnemy(card) ? -12 : 12;
            return value;
        }

        private static int MaterialRace(ClientCard card)
        {
            return card.Location == CardLocation.MonsterZone && card.IsFaceup()
                ? card.Race : card.Data != null ? card.Data.Race : card.Race;
        }

        private static bool IsReviewedBoss(ClientCard card)
        {
            return card != null && card.Controller == 0
                && (card.Location != CardLocation.MonsterZone || card.IsFaceup())
                && (card.IsOriginalCode(KingPhantom) || card.IsOriginalCode(BerfometFusion));
        }

        private ChainInfo EnemyLink(bool monster)
        {
            if (_duel.GetCurrentChainCard() == null || _duel.CurrentChainInfo.Count == 0
                || _duel.NegatedChainIndexList.Contains(_duel.CurrentChainInfo.Count))
                return null;
            ChainInfo source = _duel.CurrentChainInfo.Last();
            if (source.ActivatePlayer != 1 || (monster && source.ActivateLocation != CardLocation.MonsterZone))
                return null;
            return source;
        }

        private bool IsTargetedByEnemy(ClientCard card)
        {
            ChainInfo source = EnemyLink(false);
            return source != null && source.Targets.Contains(card);
        }

        private ChainInfo CurrentSource()
        {
            return _duel.GetCurrentSolvingChainInfo()
                ?? (_duel.GetCurrentChainCard() == null ? null : _duel.CurrentChainInfo.LastOrDefault());
        }

        private static bool IsEffect(ChainInfo source, int code, int offset)
        {
            return source.ActivateDescription == -1 || source.ActivateDescription == code * 16 + offset;
        }

        private static ActionEvaluation Decision(double score, string reason, params string[] names)
        {
            var features = new Dictionary<string, double>();
            foreach (string name in names)
                features[name] = 1;
            return new ActionEvaluation(score, features, reason);
        }
    }
}
