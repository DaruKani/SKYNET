using System;
using System.Collections.Generic;
using System.Linq;
using YGOSharp.OCGWrapper;
using YGOSharp.OCGWrapper.Enums;

namespace Skynet.Game.AI.Strategy
{
    public sealed class SkynetAgent : DefaultExecutor, IRankedDecisionPolicy
    {
        private readonly SkynetPolicy _policy;
        public SkynetLearningSession Learning { get; set; }

        public SkynetAgent(GameAI ai, Duel duel)
            : base(ai, duel)
        {
            // DefaultExecutor owns useful observed-state and battle handling.
            // Its constructor's ordered card rules do not participate here.
            Executors.Clear();
            _policy = new SkynetPolicy(duel);
        }

        public LegalAction ChooseAction(IList<LegalAction> actions)
        {
            if (actions == null)
                throw new ArgumentNullException("actions");
            var admitted = new List<LegalAction>();
            var features = new List<IDictionary<string, double>>();
            var scores = new List<double>();
            bool hasMandatory = actions.Any(action => action != null && action.IsMandatory);
            foreach (LegalAction action in actions)
            {
                if (hasMandatory && (action == null || !action.IsMandatory))
                    continue;
                ActionEvaluation evaluation = _policy.Evaluate(action);
                if (evaluation == null)
                    continue;
                admitted.Add(action);
                features.Add(evaluation.Features);
                scores.Add(evaluation.Score);
            }

            if (admitted.Count == 0)
                return null;
            int chosen = 0;
            if (Learning != null)
                chosen = Learning.Choose(features, scores);
            else
            {
                for (int i = 1; i < scores.Count; ++i)
                    if (scores[i] > scores[chosen])
                        chosen = i;
            }
            if (chosen < 0 || chosen >= admitted.Count)
                throw new InvalidOperationException("SKYNET returned an index outside its admitted offers.");
            return admitted[chosen];
        }

        public void OnDuelStarted()
        {
            if (Learning != null)
                Learning.BeginDuel();
        }

        public void OnDuelFinished(int result)
        {
            if (Learning != null)
                Learning.CompleteDuel(result);
        }

        public override bool OnSelectHand()
        {
            return false;
        }

        public override IList<ClientCard> OnSelectCard(IList<ClientCard> cards, int min, int max, int hint, bool cancelable)
        {
            if (cards == null || max < min || min < 0 || cards.Count < min)
                return null;

            ChainInfo solving = Duel.GetCurrentSolvingChainInfo();
            ClientCard activating = Duel.GetCurrentChainCard();
            CardSemantics semantics = null;
            bool ownSource = false;
            if (solving != null)
            {
                ownSource = solving.ActivatePlayer == 0;
                semantics = CardSemantics.Find(solving.ActivateId);
                if (semantics == null && NamedCard.IsAltartAlias(solving.ActivateId, solving.ActivateAlias))
                    semantics = CardSemantics.Find(solving.ActivateAlias);
            }
            else if (activating != null)
            {
                ownSource = activating.Controller == 0;
                semantics = CardSemantics.Find(activating);
            }

            IEnumerable<ClientCard> ordered = null;
            int count = min;
            if (ownSource && semantics != null && semantics.SelectionHint == hint
                && semantics.SelectDuringResolution == (solving != null))
            {
                count = Math.Min(max, Math.Max(1, min));
                if (semantics.Role == EffectRole.Search || semantics.Role == EffectRole.Revive)
                {
                    ordered = cards.OrderByDescending(c => SkynetPolicy.CardValue(c) + _policy.Synergy(c) * 0.3);
                }
                else if (semantics.Role == EffectRole.DestroyEnemySpell)
                {
                    ordered = cards.OrderByDescending(c => _policy.IsUsefulSpellTarget(c) ? 2 : c.Controller == 1 ? 1 : 0)
                        .ThenByDescending(SkynetPolicy.CardValue);
                }
                else if (semantics.Role == EffectRole.DestroyFaceupMonster
                    || semantics.Role == EffectRole.DestroyStrongestAttacker)
                {
                    // The core supplies the actual tied group at resolution.
                    // If the board changed, an unavoidable own-card choice must
                    // still come from that group, with the cheapest loss first.
                    ordered = cards.OrderByDescending(c => c.Controller == 1 ? SkynetPolicy.CardValue(c) : -SkynetPolicy.CardValue(c));
                }
            }

            // These prompts state what resource is being spent, even when a
            // mandatory effect did not pass through an activation decision.
            if (ordered == null && (hint == HintMsg.Discard || hint == HintMsg.Release
                || hint == HintMsg.FusionMaterial || hint == HintMsg.SynchroMaterial
                || hint == HintMsg.XyzMaterial || hint == HintMsg.LinkMaterial
                || hint == HintMsg.RemoveXyz))
                ordered = cards.OrderBy(SkynetPolicy.CardValue);

            if (ordered == null && ownSource && (hint == HintMsg.AddToHand || hint == HintMsg.SpSummon))
            {
                ordered = cards.OrderByDescending(c => SkynetPolicy.CardValue(c) + _policy.Synergy(c) * 0.3);
                count = Math.Min(max, Math.Max(1, min));
            }

            if (ordered == null)
                return base.OnSelectCard(cards, min, max, hint, cancelable);
            return ordered.Take(Math.Min(count, cards.Count)).ToList();
        }

        public override IList<ClientCard> OnSelectTribute(IList<ClientCard> cards, int min, int max, int hint, bool cancelable)
        {
            return AI.FindTributeSelection(cards.OrderBy(SkynetPolicy.CardValue).ToList(), min, max);
        }

        public override IList<ClientCard> OnSelectPendulumSummon(IList<ClientCard> cards, int min, int max)
        {
            return cards.OrderByDescending(SkynetPolicy.CardValue).Take(Math.Min(max, cards.Count)).ToList();
        }

        public override CardPosition OnSelectPosition(int cardId, IList<CardPosition> positions)
        {
            NamedCard data = NamedCard.Get(cardId);
            if (data == null || positions.Count == 0)
                return base.OnSelectPosition(cardId, positions);
            int strongestEnemy = Enemy.GetMonsters().Where(c => c.IsFaceup())
                .Select(c => c.GetDefensePower()).DefaultIfEmpty(0).Max();
            bool defend = data.Attack == 0 || (data.Defense > data.Attack && strongestEnemy >= data.Attack);
            CardPosition preferred = defend ? CardPosition.FaceUpDefence : CardPosition.FaceUpAttack;
            if (positions.Contains(preferred))
                return preferred;
            return positions[0];
        }

        public override bool OnSelectYesNo(int desc)
        {
            // Unknown optional sub-effects are not safe exploration candidates.
            // Card-activation prompts use the ranked legal-offer path instead.
            return false;
        }
    }
}
