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
        public const int Longyuan = 93490856;
        public const int Taia = 56495147;
        public const int Chengying = 96633955;
        private readonly Duel _duel;
        private int _pendingSynchroLevel;
        private ClientCard _pendingReplacement;
        private bool _longyuanCommitted;
        private bool _normalSummonCommitted;

        public SwordsoulStrategy(Duel duel)
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
            ClientCard card = action.Card;
            if (action.Type == ExecutorType.SpSummon && card.Location == CardLocation.Extra)
                _pendingSynchroLevel = card.IsCode(Chixiao) ? 8 : card.IsCode(Chengying) ? 10 : 0;
            if (action.Type == ExecutorType.Activate && card.IsCode(Chengying)
                && card.Location == CardLocation.MonsterZone && action.Description == 96)
                _pendingReplacement = card;
            if (action.Type == ExecutorType.Activate && card.IsCode(Longyuan) && card.Location == CardLocation.Hand)
                _longyuanCommitted = true;
            if (action.Type == ExecutorType.Summon || action.Type == ExecutorType.MonsterSet)
                _normalSummonCommitted = true;
        }

        public void ResetPendingProcedure()
        {
            _pendingSynchroLevel = 0;
            _pendingReplacement = null;
        }

        public void ResetTurnState()
        {
            ResetPendingProcedure();
            // These record accepted actions for search preferences, not an
            // invented remaining-summon count or a substitute for legal offers.
            _longyuanCommitted = false;
            _normalSummonCommitted = false;
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
            if (action.Type == ExecutorType.Summon && card.IsCode(Taia))
            {
                if (!bot.Graveyard.Any(IsRevealResource)
                    || bot.MonsterZone.Take(5).Count(c => c == null) < 2
                    || !bot.HasInExtra(Chixiao))
                    return false;
                features["develop"] = 1;
                features["synergy"] = 1;
                reason = "Normal summon Taia: a visible graveyard resource supports its token and the level-eight route.";
                evaluation = new ActionEvaluation(5.9, features, reason);
                return true;
            }
            if (action.Type == ExecutorType.SpSummon && card.IsCode(Chixiao, Chengying)
                && card.Location == CardLocation.Extra)
            {
                int level = card.IsCode(Chixiao) ? 8 : 10;
                if (!HasRouteMaterials(level))
                {
                    reason = "This legal Synchro offer needs materials outside the reviewed level-four token and Wyrm pair.";
                    return true;
                }
                features["develop"] = 1;
                features["synergy"] = 1;
                features["pressure"] = 0.9;
                reason = level == 8
                    ? "Use the level-four Swordsoul token and a level-four Wyrm for Chixiao; prefer Mo Ye for its draw trigger."
                    : "Use the level-four Swordsoul token and level-six Longyuan for Chengying, enabling the graveyard burn trigger.";
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
                int search = PreferredSearch();
                if (search == 0)
                {
                    reason = "No tracked Swordsoul starter or extender improves the reviewed routes in this state.";
                    return true;
                }
                features["advantage"] = 0.5;
                features["synergy"] = 1;
                reason = search == Longyuan
                    ? "Search Longyuan with Emergence to extend into the reviewed level-ten route."
                    : search == Taia ? "Search Taia with Emergence to turn the available graveyard resource into a level-eight route."
                    : "Search Mo Ye with Emergence to assemble the reviewed token and Synchro route.";
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
                    || !bot.HasInExtra(Chixiao) || !bot.GetMonsters().Any(c => IsRouteWyrm(c, 4)))
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
            if (card.IsCode(Taia))
            {
                if (card.Location == CardLocation.Grave && (action.Description == -1 || action.Description == 0))
                {
                    if (bot.Deck.Count < 2)
                    {
                        reason = "Conserve the last deck card instead of taking Taia's optional graveyard setup.";
                        return true;
                    }
                    features["advantage"] = 0.2;
                    features["synergy"] = 0.5;
                    reason = "Resolve Taia's material trigger to place an offered resource in the graveyard; preserve scarce starters.";
                    evaluation = new ActionEvaluation(3, features, reason);
                    return true;
                }
                if (card.Location != CardLocation.MonsterZone
                    || (action.Description != -1 && action.Description != Taia * 16)
                    || !bot.HasInExtra(Chixiao) || !bot.GetMonsters().Any(c => IsRouteWyrm(c, 4)))
                {
                    reason = "Taia has no reviewed level-eight continuation with a current level-four Wyrm.";
                    return true;
                }
                features["develop"] = 1;
                features["synergy"] = 1;
                reason = "Spend an offered graveyard Swordsoul/Wyrm resource to create Taia's tuner token for Chixiao.";
                evaluation = new ActionEvaluation(6.5, features, reason);
                return true;
            }
            if (card.IsCode(Longyuan))
            {
                if (card.Location == CardLocation.Grave
                    && (action.Description == -1 || action.Description == Longyuan * 16 + 1))
                {
                    features["pressure"] = 0.4;
                    reason = "Take Longyuan's offered 1,200-damage trigger after it became Synchro material.";
                    evaluation = new ActionEvaluation(5, features, reason);
                    return true;
                }
                if (card.Location != CardLocation.Hand
                    || (action.Description != -1 && action.Description != Longyuan * 16)
                    || !bot.HasInExtra(Chengying) || bot.MonsterZone.Take(5).Count(c => c == null) < 2)
                {
                    reason = "Conserve Longyuan without a reviewed Chengying payoff and room for both it and its token.";
                    return true;
                }
                features["develop"] = 1;
                features["synergy"] = 1;
                features["risk"] = 0.2;
                reason = "Discard an offered resource to summon Longyuan and its token; develop a ready level-four starter first.";
                evaluation = new ActionEvaluation(5.8, features, reason);
                return true;
            }
            if (card.IsCode(Chengying))
            {
                if (card.Location != CardLocation.MonsterZone)
                {
                    reason = "Chengying's reviewed effects require its face-up field presence.";
                    return true;
                }
                if (action.Description == 96)
                {
                    features["protection"] = 1;
                    reason = "Prevent Chengying's effect destruction by banishing one offered own graveyard card.";
                    evaluation = new ActionEvaluation(6, features, reason);
                    return true;
                }
                if (action.Description != -1 && action.Description != Chengying * 16)
                {
                    reason = "This Chengying prompt is outside its reviewed removal and destruction replacement effects.";
                    return true;
                }
                features["interaction"] = 1;
                features["advantage"] = 1;
                reason = "After a banish, remove an offered enemy field card and graveyard card without targeting.";
                evaluation = new ActionEvaluation(8, features, reason);
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
            if (_pendingReplacement != null)
            {
                // Destroy replacement is a nested non-chain procedure. Its
                // enclosing solving link may belong to the opponent; it must
                // not be mistaken for Chengying's later removal trigger.
                ClientCard sourceCard = _pendingReplacement;
                _pendingReplacement = null;
                if (hint == HintMsg.Remove && min == 1 && max == 1
                    && sourceCard.Controller == 0 && sourceCard.Location == CardLocation.MonsterZone
                    && sourceCard.IsFaceup() && sourceCard.IsCode(Chengying)
                    && cards.All(c => c.Controller == 0 && c.Location == CardLocation.Grave))
                    return cards.OrderBy(GraveResourceValue).Take(1).ToList();
            }
            if (hint == HintMsg.SynchroMaterial && _pendingSynchroLevel != 0 && _duel.CurrentChain.Count == 0)
            {
                return cards.OrderByDescending(c => IsRouteTuner(c) ? 3
                    : IsRouteWyrm(c, _pendingSynchroLevel - 4) ? c.IsCode(MoYe, Longyuan) ? 2 : 1 : 0)
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
                int search = PreferredSearch();
                ordered = cards.OrderByDescending(c => c.IsCode(search) ? 2 : c.IsCode(MoYe, Taia, Longyuan) ? 1 : 0)
                    .ThenByDescending(SkynetPolicy.CardValue);
            }
            else if (source.IsActivateCode(Longyuan) && !resolving && hint == HintMsg.Discard
                && source.ActivateLocation == CardLocation.Hand
                && (source.ActivateDescription == Longyuan * 16 || source.ActivateDescription == -1))
            {
                ordered = cards.OrderBy(HandResourceValue);
            }
            else if (source.IsActivateCode(Taia))
            {
                if (!resolving && hint == HintMsg.Remove && source.ActivateLocation == CardLocation.MonsterZone
                    && (source.ActivateDescription == Taia * 16 || source.ActivateDescription == -1))
                    ordered = cards.OrderBy(GraveResourceValue);
                else if (resolving && hint == HintMsg.ToGrave && source.ActivateLocation == CardLocation.Grave
                    && (source.ActivateDescription == 0 || source.ActivateDescription == -1))
                {
                    // No unsupported Tenyi graveyard ability is assumed here.
                    // Prefer inexpensive fuel and keep the last tracked copy
                    // of a reviewed starter or extender available for searches.
                    ordered = cards.OrderBy(c => c.IsCode(MoYe, Taia, Longyuan)
                        && _duel.Fields[0].GetCardCountInDeck(c.Id) <= 1 ? 1 : 0)
                        .ThenBy(GraveResourceValue);
                }
            }
            else if (source.IsActivateCode(Chengying) && resolving && hint == HintMsg.Remove
                && source.ActivateLocation == CardLocation.MonsterZone
                && (source.ActivateDescription == Chengying * 16 || source.ActivateDescription == -1)
                && cards.All(c => c.Controller == 1))
            {
                // Both selections are non-targeting. Hidden field identities
                // contribute no card-specific value; the real candidates alone
                // determine which field/graveyard prompt is being answered.
                ordered = cards.OrderByDescending(c => c.Location == CardLocation.Grave || c.IsFaceup()
                    ? SkynetPolicy.CardValue(c) : 1);
            }
            else if (source.IsActivateCode(Chixiao))
            {
                if (resolving && source.ActivateDescription == Chixiao * 16 && hint == HintMsg.OperateCard)
                {
                    int search = PreferredSearch();
                    if (search == 0)
                        search = _duel.Fields[0].Hand.Any(c => c.IsCode(MoYe)) ? Emergence : MoYe;
                    ordered = cards.OrderByDescending(c => c.IsCode(search) ? 3
                        : c.IsCode(Emergence) ? 2 : c.IsCode(MoYe, Taia, Longyuan) ? 1 : 0)
                        .ThenByDescending(SkynetPolicy.CardValue);
                }
                else if (!resolving && source.ActivateDescription == Chixiao * 16 + 1 && hint == HintMsg.Remove)
                {
                    ordered = cards.OrderBy(c => c.Location == CardLocation.Grave ? 0 : 1)
                        .ThenBy(c => c.Location == CardLocation.Grave ? GraveResourceValue(c) : HandResourceValue(c));
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

        public bool? SelectYesNo(int description)
        {
            ChainInfo source = _duel.GetCurrentSolvingChainInfo();
            if (source == null || source.ActivatePlayer != 0 || !source.IsActivateCode(Longyuan)
                || source.ActivateLocation != CardLocation.Hand
                || (source.ActivateDescription != Longyuan * 16 && source.ActivateDescription != -1)
                || description != Longyuan * 16 + 2)
                return null;
            ClientField bot = _duel.Fields[0];
            return (bot.HasInExtra(Chengying) && bot.GetMonsters().Any(c => IsRouteWyrm(c, 6)))
                || (bot.HasInExtra(Chixiao) && bot.GetMonsters().Any(c => IsRouteWyrm(c, 4)));
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
            if (_pendingSynchroLevel == 0 || _duel.CurrentChain.Count != 0 || cards == null || mandatoryCards == null
                || sum != _pendingSynchroLevel || mandatoryCards.Count > 2 || min < 0 || max < min)
                return null;
            var pool = mandatoryCards.Concat(cards).Distinct().ToList();
            foreach (ClientCard tuner in pool.Where(IsRouteTuner))
            {
                foreach (ClientCard wyrm in pool.Where(c => IsRouteWyrm(c, sum - 4))
                    .OrderByDescending(c => c.IsCode(MoYe, Longyuan)).ThenBy(SkynetPolicy.CardValue))
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

        private bool HasRouteMaterials(int level)
        {
            IList<ClientCard> monsters = _duel.Fields[0].GetMonsters();
            return monsters.Any(IsRouteTuner) && monsters.Any(c => IsRouteWyrm(c, level - 4));
        }

        private static bool IsRouteTuner(ClientCard card)
        {
            return card != null && card.Controller == 0 && card.Location == CardLocation.MonsterZone
                && card.IsFaceup() && card.IsCode(Token, Longyuan + 1, Taia + 1) && card.IsTuner() && card.Level == 4;
        }

        private static bool IsRouteWyrm(ClientCard card, int level)
        {
            return card != null && card.Controller == 0 && card.Location == CardLocation.MonsterZone
                && card.IsFaceup() && !card.IsTuner() && card.HasRace(CardRace.Wyrm)
                && !card.HasType(CardType.Xyz | CardType.Link) && card.Level == level;
        }

        private int PreferredSearch()
        {
            ClientField bot = _duel.Fields[0];
            bool haveMoYe = bot.Hand.Any(c => c.IsCode(MoYe));
            bool readyMoYe = haveMoYe && bot.Hand.Count(IsRevealResource) > 1;
            bool readyTaia = bot.Hand.Any(c => c.IsCode(Taia)) && bot.Graveyard.Any(IsRevealResource);
            bool levelEight = bot.HasInExtra(Chixiao);
            if (!_normalSummonCommitted && levelEight && !readyMoYe && !readyTaia)
            {
                if (!haveMoYe && bot.HasInDeck(MoYe) && bot.Hand.Any(IsRevealResource))
                    return MoYe;
                if (!bot.Hand.Any(c => c.IsCode(Taia)) && bot.HasInDeck(Taia) && bot.Graveyard.Any(IsRevealResource))
                    return Taia;
                if (!haveMoYe && bot.HasInDeck(MoYe))
                    return MoYe;
            }
            if (!_longyuanCommitted && !bot.Hand.Any(c => c.IsCode(Longyuan))
                && bot.HasInDeck(Longyuan) && bot.HasInExtra(Chengying) && bot.Hand.Any(IsRevealResource)
                && bot.MonsterZone.Take(5).Count(c => c == null) >= 2)
                return Longyuan;
            if (!_normalSummonCommitted && levelEight && haveMoYe && !readyMoYe)
            {
                // The searched card is also a reveal resource for the Mo Ye
                // already in hand; it need not be normal summoned itself.
                if (bot.HasInDeck(Taia))
                    return Taia;
                if (bot.HasInDeck(MoYe))
                    return MoYe;
            }
            return 0;
        }

        private double HandResourceValue(ClientCard card)
        {
            double value = SkynetPolicy.CardValue(card);
            if (!_normalSummonCommitted)
                value += card.IsCode(MoYe) ? 4 : card.IsCode(Emergence) ? 3 : card.IsCode(Taia) ? 2 : 0;
            if (_duel.Fields[0].Hand.Count(c => c.IsCode(card.Id)) > 1)
                value -= 2;
            return value;
        }

        private double GraveResourceValue(ClientCard card)
        {
            // Prefer spent spells and duplicate fuel over a unique revivable
            // route body. These are preferences over server-filtered resources.
            double value = SkynetPolicy.CardValue(card);
            if (card.IsCode(Emergence))
                value -= 3;
            if (card.IsCode(MoYe, Taia, Longyuan, Chixiao, Chengying))
                value += 2;
            if (_duel.Fields[0].Graveyard.Count(c => c.IsCode(card.Id)) > 1)
                value -= 1;
            return value;
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
