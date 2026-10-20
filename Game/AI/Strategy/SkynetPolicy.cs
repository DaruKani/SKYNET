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
        private readonly SwordsoulStrategy _swordsoul;
        private readonly ChimeraStrategy _chimera;

        public SkynetPolicy(Duel duel, SwordsoulStrategy swordsoul = null, ChimeraStrategy chimera = null)
        {
            if (duel == null)
                throw new ArgumentNullException("duel");
            _duel = duel;
            _swordsoul = swordsoul ?? new SwordsoulStrategy(duel);
            _chimera = chimera ?? new ChimeraStrategy(duel);
        }

        public ActionEvaluation Evaluate(LegalAction action)
        {
            string reason;
            return Evaluate(action, out reason);
        }

        public ActionEvaluation Evaluate(LegalAction action, out string reason)
        {
            reason = "No reviewed behavior for this offer.";
            if (action == null)
                return null;
            // Mandatory offers cannot be declined, including unknown effects.
            // Their subsequent selections use the real prompt and chain snapshot.
            if (action.IsMandatory)
                return new ActionEvaluation(0, new Dictionary<string, double>(), "Mandatory effect: the core requires a response.");

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
                    return new ActionEvaluation(allies.Any(c => c.IsAttack()) ? 1 + pressure : 0, features,
                        "Compare current attacking bodies and visible pressure before leaving Main Phase.");
                }
                features["conserve"] = 1;
                return new ActionEvaluation(0, features, "Conserve resources by declining this window or ending the phase.");
            }

            ClientCard card = action.Card;
            if (card.Id == 0)
                return null;
            double value = CardValue(card);
            if (action.Type == ExecutorType.Activate)
                return EvaluateEffect(action, allies, opponents, out reason);

            ActionEvaluation strategyEvaluation;
            if (_swordsoul.TryEvaluate(action, out strategyEvaluation, out reason))
                return strategyEvaluation;
            if (_chimera.TryEvaluate(action, out strategyEvaluation, out reason))
                return strategyEvaluation;

            if (action.Type == ExecutorType.Summon || action.Type == ExecutorType.MonsterSet)
            {
                int level = CardSemantics.Level(card);
                int tributeCount = level > 6 ? 2 : level > 4 ? 1 : 0;
                double tributeValue = allies.OrderBy(CardValue).Take(tributeCount).Sum(CardValue);
                // The offer proves legality, but spending superior bodies is rarely
                // useful without a reviewed effect or a known deck combination.
                reason = "The new body does not repay the visible tribute resources.";
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
                return new ActionEvaluation(score, features, setting
                    ? "Develop a defender, accounting for its stats, tribute cost and the visible enemy board."
                    : "Develop an attacker, accounting for its stats, tribute cost and available race support.");
            }

            if (action.Type == ExecutorType.SpSummon)
            {
                // Extra-deck procedures have materially different material/value
                // requirements. Bootstrap play does not blindly consume a board
                // for an unreviewed procedure. Effect-driven summons are handled by
                // the reviewed effect and its actual selection prompt instead.
                reason = "This special-summon procedure has no reviewed material and follow-up plan.";
                if (!CardSemantics.HasType(card, CardType.Normal)
                    || card.Location == CardLocation.SpellZone
                    || card.Location == CardLocation.Extra)
                    return null;
                features["develop"] = Math.Min(1, value / 4);
                features["pressure"] = Math.Min(1, Math.Max(0, CardSemantics.Attack(card)) / 3000.0);
                features["synergy"] = Synergy(card);
                return new ActionEvaluation(2 + value * 0.5, features, "Develop an offered normal monster without an unreviewed Extra Deck procedure.");
            }

            if (action.Type == ExecutorType.SpellSet)
            {
                CardSemantics semantics = CardSemantics.Find(card);
                reason = "Setting requires a supported trap/Quick-Play and space reserved in the back row.";
                if (semantics == null || bot.GetSpellCountWithoutField() >= 4
                    || (!CardSemantics.HasType(card, CardType.Trap) && !CardSemantics.HasType(card, CardType.QuickPlay)))
                    return null;
                features["protection"] = 1;
                return new ActionEvaluation(0.8, features, "Set a supported response for a later activation window.");
            }

            if (action.Type == ExecutorType.Repos)
            {
                reason = "The visible board gives no useful attack/defense position change.";
                int attack = CardSemantics.Attack(card);
                int defense = CardSemantics.Defense(card);
                int enemyPower = opponents.Where(c => c.IsFaceup()).Select(c => c.GetDefensePower()).DefaultIfEmpty(0).Max();
                if (card.IsAttack())
                {
                    if (defense <= attack || enemyPower <= attack)
                        return null;
                    features["protection"] = Math.Min(1, defense / 3000.0);
                    return new ActionEvaluation(1, features, "Use the higher DEF to protect this body from the visible attack threat.");
                }
                if (attack <= 0 || (enemyPower >= attack && opponents.Count > 0))
                    return null;
                features["pressure"] = Math.Min(1, attack / 3000.0);
                return new ActionEvaluation(1.2, features, "The visible board permits useful attack pressure.");
            }

            return null;
        }

        private ActionEvaluation EvaluateEffect(LegalAction action, IList<ClientCard> allies,
            IList<ClientCard> opponents, out string reason)
        {
            ClientCard card = action.Card;
            // The server refreshes field status after chains, but a moved card
            // can retain its earlier disabled bit in the client snapshot. That
            // bit alone does not establish negation of a new off-field effect.
            // Keep the query state intact; only use this generic veto on field.
            reason = "The field source is currently disabled.";
            if (card.IsOnField() && card.IsDisabled())
                return null;
            bool spell = CardSemantics.HasType(card, CardType.Spell);
            bool trap = CardSemantics.HasType(card, CardType.Trap);
            if ((spell || trap) && (card.Location == CardLocation.Hand || card.Location == CardLocation.SpellZone))
            {
                int suppressor = spell ? 61740673 : 51452091;
                bool suppressed = _duel.Fields[0].GetSpells().Concat(_duel.Fields[1].GetSpells())
                    .Any(c => c.IsFaceup() && !c.IsDisabled() && c.IsCode(suppressor)
                        && !_duel.CurrentChain.Contains(c));
                reason = "An active public spell/trap suppressor would negate this effect.";
                if (suppressed)
                    return null;
            }
            ActionEvaluation strategyEvaluation;
            if (_swordsoul.TryEvaluate(action, out strategyEvaluation, out reason))
                return strategyEvaluation;
            if (_chimera.TryEvaluate(action, out strategyEvaluation, out reason))
                return strategyEvaluation;
            CardSemantics semantics = CardSemantics.Find(card);
            reason = "No reviewed optional effect is available for this card.";
            if (semantics == null)
                return null;
            var features = new Dictionary<string, double>();
            double enemyValue = opponents.Sum(CardValue);
            double ownValue = allies.Sum(CardValue);
            switch (semantics.Role)
            {
                case EffectRole.Draw:
                    reason = "Too few deck cards remain for this draw effect.";
                    if (_duel.Fields[0].Deck.Count <= 2)
                        return null;
                    features["advantage"] = 1;
                    return new ActionEvaluation(3.5, features, "Gain cards while retaining enough cards in the deck.");
                case EffectRole.Search:
                    features["advantage"] = 0.5;
                    features["synergy"] = 0.5;
                    return new ActionEvaluation(2.8, features, "Gain a supported search target from the actual resolution choices.");
                case EffectRole.DestroyEnemyMonsters:
                case EffectRole.DestroyAllMonsters:
                    reason = "Visible removal value does not outweigh the allied resources lost.";
                    if (semantics.Role == EffectRole.DestroyAllMonsters)
                        enemyValue -= ownValue;
                    if (enemyValue <= 0)
                        return null;
                    features["interaction"] = Math.Min(1, enemyValue / 6);
                    features["advantage"] = Math.Min(1, opponents.Count / 3.0);
                    features["risk"] = semantics.Role == EffectRole.DestroyAllMonsters ? Math.Min(1, ownValue / 6) : 0;
                    return new ActionEvaluation(2 + enemyValue * 0.6, features, "Remove visible opposing bodies; subtract allied losses for a symmetric wipe.");
                case EffectRole.DestroyFaceupMonster:
                    reason = "No face-up opposing monster is available for this removal.";
                    if (!opponents.Any(c => c.IsFaceup()))
                        return null;
                    features["interaction"] = 0.5;
                    return new ActionEvaluation(2.6, features, "Remove a face-up opposing monster; the core supplies any tied resolution choices.");
                case EffectRole.DestroyStrongestAttacker:
                    reason = "The global highest-ATK face-up attackers do not include an opposing monster.";
                    // Hammer Shot includes both fields, but only face-up attack
                    // positions. The core recomputes ties when it resolves.
                    IList<ClientCard> attackers = allies.Concat(opponents)
                        .Where(c => c.IsFaceup() && c.IsAttack()).ToList();
                    if (attackers.Count == 0)
                        return null;
                    int highestAttack = attackers.Max(CardSemantics.Attack);
                    ClientCard removal = attackers.Where(c => c.Controller == 1
                        && CardSemantics.Attack(c) == highestAttack)
                        .OrderByDescending(CardValue).FirstOrDefault();
                    if (removal == null)
                        return null;
                    features["interaction"] = Math.Min(1, CardValue(removal) / 4);
                    return new ActionEvaluation(2.6 + CardValue(removal) * 0.2, features, "Hammer Shot can remove an opposing highest-ATK attacker without choosing our own body.");
                case EffectRole.AdditionalNormalSummon:
                    reason = "Double Summon needs our Main Phase, two free main zones and two distinct offered low-level developments.";
                    // Reuse the expert's pre-summon intent with actual current
                    // offers, without its speculative tribute line or probe-time
                    // usage flag. Successful resolution changes the core's summon
                    // cap; a negated activation may legitimately be offered again.
                    if (_duel.Player != 0 || (_duel.Phase != DuelPhase.Main1 && _duel.Phase != DuelPhase.Main2)
                        || _duel.CurrentChain.Count != 0
                        || _duel.Fields[0].MonsterZone.Take(5).Count(c => c == null) < 2)
                        return null;
                    MainPhase main = _duel.MainPhase;
                    var developments = new Dictionary<ClientCard, double>();
                    foreach (ClientCard candidate in main.SummonableCards.Concat(main.MonsterSetableCards).Distinct())
                    {
                        if (candidate.Controller != 0 || candidate.Location != CardLocation.Hand
                            || CardSemantics.Level(candidate) < 1 || CardSemantics.Level(candidate) > 4)
                            continue;
                        double bestScore = 0;
                        if (main.SummonableCards.Contains(candidate))
                        {
                            ActionEvaluation summon = Evaluate(new LegalAction(ExecutorType.Summon, candidate, -1,
                                candidate.ActionIndex[(int)MainPhaseAction.MainAction.Summon]));
                            if (summon != null)
                                bestScore = summon.Score;
                        }
                        if (main.MonsterSetableCards.Contains(candidate))
                        {
                            ActionEvaluation set = Evaluate(new LegalAction(ExecutorType.MonsterSet, candidate, -1,
                                candidate.ActionIndex[(int)MainPhaseAction.MainAction.SetMonster]));
                            if (set != null)
                                bestScore = Math.Max(bestScore, set.Score);
                        }
                        if (bestScore > 0)
                            developments.Add(candidate, bestScore);
                    }
                    if (developments.Count < 2)
                        return null;
                    features["develop"] = 1;
                    features["advantage"] = 0.5;
                    // Like the expert's registration priority, make room for the
                    // second development before spending the ordinary summon.
                    return new ActionEvaluation(developments.Values.Max() + 1, features, "Use Double Summon before spending the ordinary summon; two useful developments are currently offered.");
                case EffectRole.AttackLock:
                    reason = "An enabled Swords copy already protects the field.";
                    if (_duel.Fields[0].GetSpells().Any(c => !ReferenceEquals(c, card)
                        && c.IsFaceup() && !c.IsDisabled()
                        && CardSemantics.Find(c) != null && CardSemantics.Find(c).Role == EffectRole.AttackLock))
                        return null;
                    int ownDefense = allies.Select(c => c.IsDefense()
                        ? CardSemantics.Defense(c) : CardSemantics.Attack(c)).DefaultIfEmpty(0).Max();
                    bool reveal = opponents.Any(c => c.IsFacedown());
                    bool threatened = opponents.Any(c => c.IsFaceup() && CardSemantics.Attack(c) > ownDefense);
                    reason = "There is no facedown monster to reveal or visible attack threat exceeding our best defender.";
                    if (!reveal && !threatened)
                        return null;
                    features["protection"] = threatened ? 1 : 0.5;
                    return new ActionEvaluation(threatened ? 3.2 : 2.4, features,
                        threatened ? "Use Swords to delay the visible battle threat." : "Use Swords to reveal an opposing facedown monster.");
                case EffectRole.BanishAttacker:
                    reason = "No observed opposing attacker is currently attacking our side.";
                    // Prison's Lua targets the attacker automatically. It has no
                    // SelectCard prompt and must never enqueue a guessed target.
                    ClientCard attacker = _duel.Fields[1].BattlingMonster;
                    if (_duel.Player != 1 || !_duel.Fields[0].UnderAttack
                        || attacker == null || attacker.Controller != 1
                        || attacker.Location != CardLocation.MonsterZone || !attacker.IsFaceup()
                        || !opponents.Contains(attacker))
                        return null;
                    for (int i = 0; i < _duel.CurrentChainInfo.Count; ++i)
                    {
                        ChainInfo chain = _duel.CurrentChainInfo[i];
                        if (chain.ActivatePlayer != 0 || _duel.NegatedChainIndexList.Contains(i + 1))
                            continue;
                        // This is conservation based on a pending answer, not an
                        // assertion that the monster has already left the field.
                        if ((chain.IsActivateCode(70342110) && chain.Targets.Contains(attacker))
                            || (chain.IsActivateCode(44095762) && attacker.IsAttack()))
                        {
                            reason = "An own pending response already covers this attacker; conserve the extra trap.";
                            return null;
                        }
                    }
                    features["protection"] = 1;
                    features["interaction"] = Math.Min(1, CardValue(attacker) / 4);
                    return new ActionEvaluation(3.2 + CardValue(attacker) * 0.2, features, "Banish the observed attacker through Prison's automatic target.");
                case EffectRole.DestroyEnemySpell:
                    reason = "No facedown or useful persistent opposing back-row card is available.";
                    // Destroying a normal spell already on the chain does not negate
                    // it. Prefer facedown cards and face-up persistent spell/traps.
                    if (!_duel.Fields[1].GetSpells().Any(IsUsefulSpellTarget))
                        return null;
                    features["interaction"] = 0.5;
                    return new ActionEvaluation(_duel.Player == 0 ? 2.5 : 1.5, features, "Remove a facedown or persistent opposing back-row card; destroying a normal spell does not negate it.");
                case EffectRole.Revive:
                    reason = "Preserve the Chimera graveyard answers when no other known revivable resource is available.";
                    if (_duel.Fields[0].Graveyard.Any(_chimera.PreserveGraveyardProtection)
                        && !_duel.Fields[0].Graveyard.Concat(_duel.Fields[1].Graveyard)
                            .Any(c => c != null && CardSemantics.HasType(c, CardType.Monster)
                                && c.IsCanRevive() && !_chimera.PreserveGraveyardProtection(c)))
                        return null;
                    features["recovery"] = 1;
                    features["develop"] = 0.7;
                    return new ActionEvaluation(3, features, "Recover an offered graveyard body through the supported revival prompt.");
                case EffectRole.BattleTrap:
                    reason = "No opposing attack-position body is available for this battle trap.";
                    if (!opponents.Any(c => c.IsAttack()))
                        return null;
                    features["protection"] = 1;
                    features["interaction"] = Math.Min(1, opponents.Count(c => c.IsAttack()) / 3.0);
                    return new ActionEvaluation(3 + opponents.Count(c => c.IsAttack()) * 0.5, features, "Protect the board by clearing opposing attack-position bodies.");
                case EffectRole.SummonTrap:
                    features["interaction"] = 0.7;
                    return new ActionEvaluation(3, features, "Answer the offered summon-response window with supported removal.");
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
                || CardSemantics.HasType(card, CardType.Pendulum)
                || (card.IsFaceup() && !card.IsDisabled() && CardSemantics.Find(card) != null
                    && CardSemantics.Find(card).Role == EffectRole.AttackLock);
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
