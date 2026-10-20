using System;
using System.Collections.Generic;
using System.Linq;
using YGOSharp.OCGWrapper.Enums;

namespace Skynet.Game.AI.Strategy
{
    /// <summary>
    /// Side-effect-free utility evaluation of observed legal offers. This baseline
    /// does not simulate card scripts or construct future/hidden game states.
    /// A null evaluation excludes an unsupported or strategically unsafe offer
    /// from both greedy play and training exploration.
    /// </summary>
    public sealed class SkynetPolicy
    {
        private readonly Duel _duel;

        public SkynetPolicy(Duel duel)
        {
            if (duel == null)
                throw new ArgumentNullException("duel");
            _duel = duel;
        }

        public ActionEvaluation Evaluate(LegalAction action)
        {
            if (action == null)
                return null;
            // Mandatory offers cannot be declined, including unknown effects.
            // Their subsequent selections use the real prompt and chain snapshot.
            if (action.IsMandatory)
                return new ActionEvaluation(0, new Dictionary<string, double>());

            ClientField bot = _duel.Fields[0];
            ClientField enemy = _duel.Fields[1];
            IList<ClientCard> allies = bot.GetMonsters();
            IList<ClientCard> opponents = enemy.GetMonsters();
            var features = new Dictionary<string, double>();
            if (action.Card == null)
            {
                if (action.Type == ExecutorType.GoToBattlePhase)
                {
                    double pressure = allies.Where(c => c.IsAttack()).Sum(c => Math.Max(0, CardSemantics.Attack(c))) / 8000.0;
                    features["pressure"] = Math.Min(1, pressure);
                    return new ActionEvaluation(allies.Any(c => c.IsAttack()) ? 1 + pressure : 0, features);
                }
                features["conserve"] = 1;
                return new ActionEvaluation(0, features);
            }

            ClientCard card = action.Card;
            if (card.Id == 0)
                return null;
            double value = CardValue(card);
            if (action.Type == ExecutorType.Activate)
                return EvaluateEffect(card, allies, opponents);

            if (action.Type == ExecutorType.Summon || action.Type == ExecutorType.MonsterSet)
            {
                int level = CardSemantics.Level(card);
                int tributeCount = level > 6 ? 2 : level > 4 ? 1 : 0;
                double tributeValue = allies.OrderBy(CardValue).Take(tributeCount).Sum(CardValue);
                // The offer proves legality, but spending superior bodies is rarely
                // useful without a reviewed effect or a known deck combination.
                if (tributeCount > 0 && value <= tributeValue)
                    return null;
                bool setting = action.Type == ExecutorType.MonsterSet;
                int attack = CardSemantics.Attack(card);
                int defense = CardSemantics.Defense(card);
                double strongestEnemy = opponents.Where(c => c.IsFaceup()).Select(c => c.GetDefensePower()).DefaultIfEmpty(0).Max();
                features["develop"] = Math.Min(1, value / 4);
                features["pressure"] = setting ? 0 : Math.Min(1, Math.Max(0, attack) / 3000.0);
                features["protection"] = setting ? Math.Min(1, Math.Max(0, defense) / 3000.0) : 0;
                features["risk"] = Math.Min(1, tributeValue / 4);
                features["synergy"] = Synergy(card);
                double score = 1.5 + value * 0.5 - tributeValue * 0.6 + features["synergy"] * 0.3;
                if (setting)
                {
                    score -= 0.6;
                    if (allies.Count == 0 && strongestEnemy > attack && defense > attack)
                        score += 1;
                }
                return new ActionEvaluation(score, features);
            }

            if (action.Type == ExecutorType.SpSummon)
            {
                // Extra-deck procedures have materially different material/value
                // requirements. Bootstrap play does not blindly consume a board
                // for an unreviewed procedure. Effect-driven summons are handled by
                // the reviewed effect and its actual selection prompt instead.
                if (!CardSemantics.HasType(card, CardType.Normal)
                    || card.Location == CardLocation.SpellZone
                    || card.Location == CardLocation.Extra)
                    return null;
                features["develop"] = Math.Min(1, value / 4);
                features["pressure"] = Math.Min(1, Math.Max(0, CardSemantics.Attack(card)) / 3000.0);
                features["synergy"] = Synergy(card);
                return new ActionEvaluation(2 + value * 0.5, features);
            }

            if (action.Type == ExecutorType.SpellSet)
            {
                CardSemantics semantics = CardSemantics.Find(card);
                if (semantics == null || bot.GetSpellCountWithoutField() >= 4
                    || (!CardSemantics.HasType(card, CardType.Trap) && !CardSemantics.HasType(card, CardType.QuickPlay)))
                    return null;
                features["protection"] = 1;
                return new ActionEvaluation(0.8, features);
            }

            if (action.Type == ExecutorType.Repos)
            {
                int attack = CardSemantics.Attack(card);
                int defense = CardSemantics.Defense(card);
                int enemyPower = opponents.Where(c => c.IsFaceup()).Select(c => c.GetDefensePower()).DefaultIfEmpty(0).Max();
                if (card.IsAttack())
                {
                    if (defense <= attack || enemyPower <= attack)
                        return null;
                    features["protection"] = Math.Min(1, defense / 3000.0);
                    return new ActionEvaluation(1, features);
                }
                if (attack <= 0 || (enemyPower >= attack && opponents.Count > 0))
                    return null;
                features["pressure"] = Math.Min(1, attack / 3000.0);
                return new ActionEvaluation(1.2, features);
            }

            return null;
        }

        private ActionEvaluation EvaluateEffect(ClientCard card, IList<ClientCard> allies, IList<ClientCard> opponents)
        {
            CardSemantics semantics = CardSemantics.Find(card);
            if (semantics == null || card.IsDisabled())
                return null;
            bool spell = CardSemantics.HasType(card, CardType.Spell);
            bool trap = CardSemantics.HasType(card, CardType.Trap);
            if ((spell || trap) && (card.Location == CardLocation.Hand || card.Location == CardLocation.SpellZone))
            {
                int suppressor = spell ? 61740673 : 51452091;
                bool suppressed = _duel.Fields[0].GetSpells().Concat(_duel.Fields[1].GetSpells())
                    .Any(c => c.IsFaceup() && !c.IsDisabled() && c.IsCode(suppressor)
                        && !_duel.CurrentChain.Contains(c));
                if (suppressed)
                    return null;
            }
            var features = new Dictionary<string, double>();
            double enemyValue = opponents.Sum(CardValue);
            double ownValue = allies.Sum(CardValue);
            switch (semantics.Role)
            {
                case EffectRole.Draw:
                    if (_duel.Fields[0].Deck.Count <= 2)
                        return null;
                    features["advantage"] = 1;
                    return new ActionEvaluation(3.5, features);
                case EffectRole.Search:
                    features["advantage"] = 0.5;
                    features["synergy"] = 0.5;
                    return new ActionEvaluation(2.8, features);
                case EffectRole.DestroyEnemyMonsters:
                case EffectRole.DestroyAllMonsters:
                    if (semantics.Role == EffectRole.DestroyAllMonsters)
                        enemyValue -= ownValue;
                    if (enemyValue <= 0)
                        return null;
                    features["interaction"] = Math.Min(1, enemyValue / 6);
                    features["advantage"] = Math.Min(1, opponents.Count / 3.0);
                    features["risk"] = semantics.Role == EffectRole.DestroyAllMonsters ? Math.Min(1, ownValue / 6) : 0;
                    return new ActionEvaluation(2 + enemyValue * 0.6, features);
                case EffectRole.DestroyFaceupMonster:
                    if (!opponents.Any(c => c.IsFaceup()))
                        return null;
                    features["interaction"] = 0.5;
                    return new ActionEvaluation(2.6, features);
                case EffectRole.DestroyEnemySpell:
                    // Destroying a normal spell already on the chain does not negate
                    // it. Prefer facedown cards and face-up persistent spell/traps.
                    if (!_duel.Fields[1].GetSpells().Any(IsUsefulSpellTarget))
                        return null;
                    features["interaction"] = 0.5;
                    return new ActionEvaluation(_duel.Player == 0 ? 2.5 : 1.5, features);
                case EffectRole.Revive:
                    features["recovery"] = 1;
                    features["develop"] = 0.7;
                    return new ActionEvaluation(3, features);
                case EffectRole.BattleTrap:
                    if (!opponents.Any(c => c.IsAttack()))
                        return null;
                    features["protection"] = 1;
                    features["interaction"] = Math.Min(1, opponents.Count(c => c.IsAttack()) / 3.0);
                    return new ActionEvaluation(3 + opponents.Count(c => c.IsAttack()) * 0.5, features);
                case EffectRole.SummonTrap:
                    features["interaction"] = 0.7;
                    return new ActionEvaluation(3, features);
                default:
                    return null;
            }
        }

        public bool IsUsefulSpellTarget(ClientCard card)
        {
            if (card == null || card.Controller != 1)
                return false;
            if (card.IsFacedown())
                return true;
            return CardSemantics.HasType(card, CardType.Continuous)
                || CardSemantics.HasType(card, CardType.Equip)
                || CardSemantics.HasType(card, CardType.Field)
                || CardSemantics.HasType(card, CardType.Pendulum);
        }

        public double Synergy(ClientCard card)
        {
            if (card == null || card.Data == null)
                return 0;
            // Only the bot's already-visible hand/field contributes. Race is a
            // modest cohesion signal, not an inference about an unseen deck.
            int matches = _duel.Fields[0].Hand.Concat(_duel.Fields[0].GetMonsters())
                .Count(c => !ReferenceEquals(c, card) && c.Data != null
                    && CardSemantics.HasType(c, CardType.Monster)
                    && c.Data.Race == card.Data.Race);
            return Math.Min(1, matches / 4.0);
        }

        public static double CardValue(ClientCard card)
        {
            if (card == null)
                return 0;
            if (card.Id == 0 || (card.Controller == 1 && card.IsFacedown()))
                return 1;
            if (CardSemantics.HasType(card, CardType.Monster))
            {
                double attack = Math.Max(0, CardSemantics.Attack(card));
                double defense = Math.Max(0, CardSemantics.Defense(card));
                return 1 + Math.Max(attack, defense * 0.75) / 2000.0;
            }
            return CardSemantics.Find(card) == null ? 0.6 : 1.2;
        }
    }
}
