using System;
using System.Collections.Generic;
using System.Linq;
using Skynet.Game.AI;
using Skynet.Game.AI.Strategy;
using YGOSharp.OCGWrapper.Enums;

namespace Skynet.Game
{
    public partial class GameAI
    {
        private bool HasRankedPolicy
        {
            get { return Executor is IRankedDecisionPolicy; }
        }

        private void OnPolicyDuelStarted()
        {
            IRankedDecisionPolicy policy = Executor as IRankedDecisionPolicy;
            if (policy != null)
            {
                _activatedCards.Clear();
                policy.OnDuelStarted();
            }
            Logger.WriteLine("SKYNET_START");
        }

        public void OnDuelResult(int result)
        {
            IRankedDecisionPolicy policy = Executor as IRankedDecisionPolicy;
            if (policy != null)
            {
                policy.OnDuelFinished(result);
            }
            Logger.WriteLine("SKYNET_RESULT result=" + result);
        }

        private bool CanOfferPolicyActivation(ClientCard card)
        {
            int count;
            return card != null && (!_activatedCards.TryGetValue(card.Id, out count) || count < 9);
        }

        // A policy chooses an offered object, never a fabricated response/index.
        // Only the accepted action consumes the repeated-activation allowance.
        private LegalAction ChoosePolicyAction(IList<LegalAction> offers)
        {
            if (offers.Count == 0)
                throw new InvalidOperationException("SKYNET received an empty legal-action offer.");
            LegalAction selected = ((IRankedDecisionPolicy)Executor).ChooseAction(offers);
            if (selected == null || !offers.Contains(selected))
                throw new InvalidOperationException("SKYNET returned an action outside the server offer.");
            SkynetAgent general = Executor as SkynetAgent;
            if (general != null)
                general.OnActionCommitted(selected);
            if (selected.Type == ExecutorType.Activate && selected.Card != null && selected.Card.Id != 0)
            {
                int count;
                _activatedCards.TryGetValue(selected.Card.Id, out count);
                _activatedCards[selected.Card.Id] = count + (selected.Card.IsDisabled() ? 3 : 1);
            }
            return selected;
        }

        private int SelectPolicyChain(IList<ClientCard> cards, IList<int> descriptions, IList<bool> forces, int timing)
        {
            List<LegalAction> offers = new List<LegalAction>();
            bool forced = forces.Any(value => value);
            for (int i = 0; i < cards.Count; ++i)
            {
                // Mandatory responses are never suppressed by a heuristic loop guard.
                if (forced ? forces[i] : CanOfferPolicyActivation(cards[i]))
                    offers.Add(new LegalAction(ExecutorType.Activate, cards[i], descriptions[i], i, timing, forced));
            }
            if (!forced)
                offers.Add(new LegalAction(ExecutorType.Activate, null, -1, -1, timing));
            LegalAction selected = ChoosePolicyAction(offers);
            if (selected.Card != null)
                _dialogs.SendChaining(selected.Card.Name);
            return selected.Index;
        }

        private bool SelectPolicyEffect(ClientCard card, int description)
        {
            List<LegalAction> offers = new List<LegalAction>();
            if (CanOfferPolicyActivation(card))
                offers.Add(new LegalAction(ExecutorType.Activate, card, description, 0));
            offers.Add(new LegalAction(ExecutorType.Activate, null, -1, -1));
            return ChoosePolicyAction(offers).Card != null;
        }

        private BattlePhaseAction SelectPolicyBattleActivation(BattlePhase battle)
        {
            if (!HasRankedPolicy || battle.ActivableCards.Count == 0)
                return null;
            List<LegalAction> offers = new List<LegalAction>();
            for (int i = 0; i < battle.ActivableCards.Count; ++i)
            {
                ClientCard card = battle.ActivableCards[i];
                if (CanOfferPolicyActivation(card))
                    offers.Add(new LegalAction(ExecutorType.Activate, card, battle.ActivableDescs[i], i));
            }
            // Passing this activation window resumes the existing battle-target policy.
            offers.Add(new LegalAction(ExecutorType.Activate, null, -1, -1));
            LegalAction selected = ChoosePolicyAction(offers);
            if (selected.Card == null)
                return null;
            _dialogs.SendChaining(selected.Card.Name);
            int[] indexes = new int[2];
            indexes[0] = selected.Index;
            return new BattlePhaseAction(BattlePhaseAction.BattleAction.Activate, indexes);
        }

        private MainPhaseAction SelectPolicyMain(MainPhase main)
        {
            List<LegalAction> offers = new List<LegalAction>();
            for (int i = 0; i < main.ActivableCards.Count; ++i)
            {
                ClientCard card = main.ActivableCards[i];
                if (CanOfferPolicyActivation(card))
                    offers.Add(new LegalAction(ExecutorType.Activate, card, main.ActivableDescs[i], i));
            }
            AddPolicyOffers(offers, main.SummonableCards, ExecutorType.Summon, MainPhaseAction.MainAction.Summon);
            AddPolicyOffers(offers, main.SpecialSummonableCards, ExecutorType.SpSummon, MainPhaseAction.MainAction.SpSummon);
            AddPolicyOffers(offers, main.ReposableCards, ExecutorType.Repos, MainPhaseAction.MainAction.Repos);
            AddPolicyOffers(offers, main.MonsterSetableCards, ExecutorType.MonsterSet, MainPhaseAction.MainAction.SetMonster);
            AddPolicyOffers(offers, main.SpellSetableCards, ExecutorType.SpellSet, MainPhaseAction.MainAction.SetSpell);
            if (main.CanBattlePhase)
                offers.Add(new LegalAction(ExecutorType.GoToBattlePhase, null, -1, 0));
            if (main.CanEndPhase)
                offers.Add(new LegalAction(ExecutorType.GoToEndPhase, null, -1, 0));

            LegalAction selected = ChoosePolicyAction(offers);
            MainPhaseAction.MainAction action;
            switch (selected.Type)
            {
                case ExecutorType.Activate:
                    _dialogs.SendActivate(selected.Card.Name);
                    action = MainPhaseAction.MainAction.Activate;
                    break;
                case ExecutorType.Summon:
                    _dialogs.SendSummon(selected.Card.Name);
                    action = MainPhaseAction.MainAction.Summon;
                    break;
                case ExecutorType.SpSummon:
                    ClientCard left = Executor.Util.GetPZone(0, 0);
                    ClientCard right = Executor.Util.GetPZone(0, 1);
                    _selectingPendulumSummon = selected.Card.HasType(CardType.Pendulum)
                        && (selected.Card == left || selected.Card == right);
                    if (!_selectingPendulumSummon)
                        _dialogs.SendSummon(selected.Card.Name);
                    action = MainPhaseAction.MainAction.SpSummon;
                    break;
                case ExecutorType.Repos:
                    action = MainPhaseAction.MainAction.Repos;
                    break;
                case ExecutorType.MonsterSet:
                    _dialogs.SendSetMonster();
                    action = MainPhaseAction.MainAction.SetMonster;
                    break;
                case ExecutorType.SpellSet:
                    action = MainPhaseAction.MainAction.SetSpell;
                    break;
                case ExecutorType.GoToBattlePhase:
                    action = MainPhaseAction.MainAction.ToBattlePhase;
                    break;
                case ExecutorType.GoToEndPhase:
                    _dialogs.SendEndTurn();
                    action = MainPhaseAction.MainAction.ToEndPhase;
                    break;
                default:
                    throw new InvalidOperationException("Unsupported Skynet main-phase action.");
            }
            return new MainPhaseAction(action, selected.Index);
        }

        private static void AddPolicyOffers(IList<LegalAction> offers, IList<ClientCard> cards,
            ExecutorType type, MainPhaseAction.MainAction action)
        {
            foreach (ClientCard card in cards)
                offers.Add(new LegalAction(type, card, -1, card.ActionIndex[(int)action]));
        }
    }
}
