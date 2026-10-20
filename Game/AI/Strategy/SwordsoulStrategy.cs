using System;
using System.Collections.Generic;
using System.Linq;
using YGOSharp.OCGWrapper.Enums;

namespace Skynet.Game.AI.Strategy
{
    /// <summary>
    /// A reviewed, bounded route shared by any deck containing these cards.
    /// It replans from legal offers and observed state; it does not simulate Lua
    /// or require a deck profile. Only an accepted summon owns material prompts.
    /// </summary>
    public sealed class SwordsoulStrategy
    {
        public const int Emergence = 56465981;
        public const int MoYe = 20001443;
        public const int Token = 20001444;
        public const int Chixiao = 69248256;
        private readonly Duel _duel;
        private bool _pendingChixiao;

        public SwordsoulStrategy(Duel duel)
        {
            if (duel == null)
                throw new ArgumentNullException("duel");
            _duel = duel;
        }

        public void OnActionCommitted(LegalAction action)
        {
            _pendingChixiao = action != null && action.Type == ExecutorType.SpSummon
                && action.Card != null && action.Card.IsCode(Chixiao)
                && action.Card.Location == CardLocation.Extra;
        }

        public void ResetPendingProcedure()
        {
            _pendingChixiao = false;
        }

        public bool TryEvaluate(LegalAction action, out ActionEvaluation evaluation, out string reason)
        {
            evaluation = null;
            reason = null;
            if (action == null || action.Card == null || action.Card.Controller != 0)
                return false;

            ClientCard card = action.Card;
            ClientField bot = _duel.Fields[0];
            var features = new Dictionary<string, double>();
            if (action.Type == ExecutorType.Summon && card.IsCode(MoYe))
            {
                if (!bot.Hand.Any(c => c != card && IsRevealResource(c))
                    || bot.MonsterZone.Take(5).Count(c => c == null) < 2
                    || !bot.HasInExtra(Chixiao))
                    return false;
                features["develop"] = 1;
                features["synergy"] = 1;
                reason = "Normal summon Mo Ye: a visible reveal card and room for a token support the Chixiao route.";
                evaluation = new ActionEvaluation(6, features, reason);
                return true;
            }
            if (action.Type == ExecutorType.SpSummon && card.IsCode(Chixiao)
                && card.Location == CardLocation.Extra)
            {
                if (!HasRouteMaterials())
                {
                    reason = "The legal Chixiao offer needs a material combination outside the reviewed level-four token route.";
                    return true;
                }
                features["develop"] = 1;
                features["synergy"] = 1;
                features["pressure"] = 0.9;
                reason = "Use the level-four Swordsoul token and a level-four Wyrm for Chixiao; prefer Mo Ye for its draw trigger.";
                evaluation = new ActionEvaluation(7, features, reason);
                return true;
            }
            if (action.Type != ExecutorType.Activate)
                return false;

            if (card.IsCode(Emergence))
            {
                if ((card.Location != CardLocation.Hand && card.Location != CardLocation.SpellZone)
                    || (action.Description != -1 && action.Description != Emergence * 16))
                {
                    reason = "Emergence's banished level-change effect is outside this route; preserve the observed levels.";
                    return true;
                }
                if (!bot.HasInDeck(MoYe))
                {
                    reason = "No tracked Mo Ye remains in the deck for the reviewed Emergence search.";
                    return true;
                }
                bool readyMoYe = bot.Hand.Any(c => c.IsCode(MoYe))
                    && bot.Hand.Count(IsRevealResource) > 1;
                if (readyMoYe)
                {
                    reason = "Mo Ye already has a reveal resource; conserve Emergence for recovery.";
                    return true;
                }
                features["advantage"] = 0.5;
                features["synergy"] = 1;
                reason = "Search Mo Ye with Emergence to assemble the reviewed token and Synchro route.";
                evaluation = new ActionEvaluation(6.3, features, reason);
                return true;
            }
            if (card.IsCode(MoYe))
            {
                if (card.Location == CardLocation.Grave)
                {
                    if (action.Description != -1 && action.Description != 0)
                    {
                        reason = "This described Mo Ye effect is not its undescribed graveyard draw trigger.";
                        return true;
                    }
                    if (bot.Deck.Count < 2)
                    {
                        reason = "Conserve the last deck card instead of taking Mo Ye's optional draw.";
                        return true;
                    }
                    features["advantage"] = 0.5;
                    reason = "Take Mo Ye's offered graveyard draw after it was used as Synchro material.";
                    evaluation = new ActionEvaluation(4, features, reason);
                    return true;
                }
                if (card.Location != CardLocation.MonsterZone
                    || (action.Description != -1 && action.Description != MoYe * 16)
                    || !bot.HasInExtra(Chixiao) || !bot.GetMonsters().Any(IsRouteWyrm))
                {
                    reason = "Mo Ye has no reviewed Chixiao continuation with a current level-four non-Tuner Wyrm.";
                    return true;
                }
                features["develop"] = 1;
                features["synergy"] = 1;
                reason = "Reveal an offered hand resource without spending it, then create Mo Ye's tuner token.";
                evaluation = new ActionEvaluation(6.5, features, reason);
                return true;
            }
            if (!card.IsCode(Chixiao))
                return false;
            if (action.Description == Chixiao * 16)
            {
                features["advantage"] = 0.5;
                features["synergy"] = 0.5;
                reason = "Use Chixiao's Synchro trigger to add a Swordsoul follow-up; the core also locks its negate for this turn.";
                evaluation = new ActionEvaluation(5, features, reason);
                return true;
            }
            if (action.Description != Chixiao * 16 + 1 || NegationTarget() == null)
            {
                reason = "Save Chixiao's shared once-per-turn effect for a visible enemy monster activation it can target.";
                return true;
            }
            features["interaction"] = 1;
            features["risk"] = bot.Graveyard.Any(IsRevealResource) ? 0.1 : 0.4;
            reason = "Respond to the visible enemy monster effect with Chixiao; spend an offered graveyard resource before a hand card.";
            evaluation = new ActionEvaluation(7, features, reason);
            return true;
        }

        public IList<ClientCard> SelectCard(IList<ClientCard> cards, int min, int max, int hint, bool cancelable)
        {
            if (cards == null || min < 0 || max < min || cards.Count < min)
                return null;
            if (hint == HintMsg.SynchroMaterial && _pendingChixiao && _duel.CurrentChain.Count == 0)
            {
                return cards.OrderByDescending(c => c.IsCode(Token) ? 2 : c.IsCode(MoYe) ? 1 : 0)
                    .ThenBy(SkynetPolicy.CardValue).Take(Math.Min(max, Math.Max(1, min))).ToList();
            }
            bool resolving = _duel.GetCurrentSolvingChainInfo() != null;
            ChainInfo source = CurrentSource();
            if (source == null || source.ActivatePlayer != 0)
                return null;

            IEnumerable<ClientCard> ordered = null;
            if (source.IsActivateCode(MoYe) && !resolving && hint == HintMsg.Confirm
                && source.ActivateLocation == CardLocation.MonsterZone)
            {
                // The prompt already excludes public hand cards and applies the
                // script's Swordsoul/Wyrm filter. Revealing does not spend a card.
                ordered = cards.OrderBy(c => c.IsCode(MoYe) ? 1 : 0).ThenBy(SkynetPolicy.CardValue);
            }
            else if (source.IsActivateCode(Emergence) && resolving && hint == HintMsg.AddToHand)
            {
                ordered = cards.OrderByDescending(c => c.IsCode(MoYe) ? 1 : 0)
                    .ThenByDescending(SkynetPolicy.CardValue);
            }
            else if (source.IsActivateCode(Chixiao))
            {
                if (resolving && source.ActivateDescription == Chixiao * 16 && hint == HintMsg.OperateCard)
                {
                    bool haveMoYe = _duel.Fields[0].Hand.Any(c => c.IsCode(MoYe));
                    ordered = cards.OrderByDescending(c => c.IsCode(haveMoYe ? Emergence : MoYe) ? 2
                        : c.IsCode(MoYe, Emergence) ? 1 : 0).ThenByDescending(SkynetPolicy.CardValue);
                }
                else if (!resolving && source.ActivateDescription == Chixiao * 16 + 1 && hint == HintMsg.Remove)
                {
                    ordered = cards.OrderBy(c => c.Location == CardLocation.Grave ? 0 : 1)
                        .ThenBy(c => c.IsCode(MoYe) ? 1 : 0).ThenBy(SkynetPolicy.CardValue);
                }
                else if (!resolving && source.ActivateDescription == Chixiao * 16 + 1 && hint == HintMsg.Disable)
                {
                    // Chixiao is now the latest link; find the most recent enemy
                    // link by its activation snapshot, then rank only real targets.
                    ChainInfo enemy = _duel.CurrentChainInfo.LastOrDefault(c => c.ActivatePlayer == 1);
                    ordered = cards.OrderByDescending(c => c.Controller == 1 ? 1 : 0)
                        .ThenByDescending(c => enemy != null && ReferenceEquals(c, enemy.RelatedCard) ? 1 : 0)
                        .ThenByDescending(SkynetPolicy.CardValue);
                }
            }
            if (ordered == null)
                return null;
            return ordered.Take(Math.Min(max, Math.Max(1, min))).ToList();
        }

        public int SelectOption(IList<int> options)
        {
            ChainInfo source = _duel.GetCurrentSolvingChainInfo();
            if (source == null || source.ActivatePlayer != 0 || !source.IsActivateCode(Chixiao)
                || source.ActivateDescription != Chixiao * 16 || options == null)
                return -1;
            // These are generic operation strings in c69248256.lua, not the
            // card's description offsets: 1190 adds to hand; 1192 banishes.
            return options.IndexOf(1190);
        }

        public IList<ClientCard> SelectSynchroMaterial(IList<ClientCard> cards,
            IList<ClientCard> mandatoryCards, int sum, int min, int max)
        {
            if (!_pendingChixiao || _duel.CurrentChain.Count != 0 || cards == null || mandatoryCards == null
                || sum != 8 || mandatoryCards.Count > 2 || min < 0 || max < min)
                return null;
            var pool = mandatoryCards.Concat(cards).Distinct().ToList();
            foreach (ClientCard tuner in pool.Where(IsRouteTuner))
            {
                foreach (ClientCard wyrm in pool.Where(IsRouteWyrm)
                    .OrderByDescending(c => c.IsCode(MoYe)).ThenBy(SkynetPolicy.CardValue))
                {
                    var pair = new List<ClientCard> { tuner, wyrm };
                    if (mandatoryCards.Any(c => !pair.Contains(c)))
                        continue;
                    var optional = pair.Where(c => !mandatoryCards.Contains(c)).ToList();
                    if (optional.Count < min || optional.Count > max || optional.Any(c => !cards.Contains(c)))
                        continue;
                    // Use the prompt's actual contribution alternatives, not an
                    // assumed material level. GameAI independently validates it.
                    if (tuner.OpParam1 + wyrm.OpParam1 == sum
                        || (tuner.OpParam2 > 0 && tuner.OpParam2 + wyrm.OpParam1 == sum)
                        || (wyrm.OpParam2 > 0 && tuner.OpParam1 + wyrm.OpParam2 == sum)
                        || (tuner.OpParam2 > 0 && wyrm.OpParam2 > 0 && tuner.OpParam2 + wyrm.OpParam2 == sum))
                        return optional;
                }
            }
            return null;
        }

        private bool HasRouteMaterials()
        {
            IList<ClientCard> monsters = _duel.Fields[0].GetMonsters();
            return monsters.Any(IsRouteTuner) && monsters.Any(IsRouteWyrm);
        }

        private static bool IsRouteTuner(ClientCard card)
        {
            return card != null && card.Controller == 0 && card.Location == CardLocation.MonsterZone
                && card.IsFaceup() && card.IsCode(Token) && card.IsTuner() && card.Level == 4;
        }

        private static bool IsRouteWyrm(ClientCard card)
        {
            return card != null && card.Controller == 0 && card.Location == CardLocation.MonsterZone
                && card.IsFaceup() && !card.IsTuner() && card.HasRace(CardRace.Wyrm)
                && !card.HasType(CardType.Xyz | CardType.Link) && card.Level == 4;
        }

        private static bool IsRevealResource(ClientCard card)
        {
            return card != null && card.Controller == 0 && card.Id != 0
                && (card.HasSetcode(0x16b) || (CardSemantics.HasType(card, CardType.Monster)
                    && card.HasRace(CardRace.Wyrm)));
        }

        private ChainInfo CurrentSource()
        {
            ChainInfo solving = _duel.GetCurrentSolvingChainInfo();
            if (solving != null)
                return solving;
            if (_duel.GetCurrentChainCard() == null)
                return null;
            return _duel.CurrentChainInfo.LastOrDefault();
        }

        private ClientCard NegationTarget()
        {
            if (_duel.GetCurrentChainCard() == null || _duel.CurrentChainInfo.Count == 0
                || _duel.NegatedChainIndexList.Contains(_duel.CurrentChainInfo.Count))
                return null;
            ChainInfo source = _duel.CurrentChainInfo.Last();
            ClientCard card = source.RelatedCard;
            if (source.ActivatePlayer != 1 || source.ActivateLocation != CardLocation.MonsterZone
                || card == null || card.Controller != 1 || card.Location != CardLocation.MonsterZone
                || !card.IsFaceup() || !card.HasType(CardType.Effect) || card.IsDisabled()
                || card.IsShouldNotBeTarget() || card.IsShouldNotBeMonsterTarget())
                return null;
            return card;
        }
    }
}
