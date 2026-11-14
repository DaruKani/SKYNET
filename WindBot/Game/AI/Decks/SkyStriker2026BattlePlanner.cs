using System;
using System.Collections.Generic;
using System.Linq;
using WindBot.Game;
using YGOSharp.OCGWrapper.Enums;

namespace WindBot.Game.AI.Decks
{
    public partial class SkyStriker2026Executor
    {
        private bool ZeroEffectSpent;
        private bool RayeTagSpent;
        private int BattleSummonTarget;
        private string BattlePlanKey;
        private BattleRoute CachedBattleRoute;
        private readonly HashSet<ClientCard> SpellsSetThisTurn = new HashSet<ClientCard>();
        private readonly HashSet<string> BattleDecisionTraces = new HashSet<string>();

        private sealed class BattleStep
        {
            public string Kind;
            public ClientCard Source, Target;
            public int Summon;
            public override string ToString()
            {
                return Kind + ":" + (Source == null ? 0 : Source.Id) + ">" +
                    (Target == null ? Summon : Target.Id);
            }
        }
        private sealed class BattleBody
        {
            public ClientCard Card;
            public int Id, Attack, Defense, Attribute;
            public bool Spent, Extra, AttackPosition, Disabled;
            public BattleBody Copy() { return (BattleBody)MemberwiseClone(); }
        }
        private sealed class BattlePosition
        {
            public List<BattleBody> Own = new List<BattleBody>();
            public List<BattleBody> Enemy = new List<BattleBody>();
            public List<BattleStep> Steps = new List<BattleStep>();
            public HashSet<int> Extra = new HashSet<int>();
            public int Damage, Linkages, Spells, GraveAttributes;
            public bool ZeroSpent, RayeSpent, RayeAccess, RozeAccess, Removal;
            public BattlePosition Copy()
            {
                var p = (BattlePosition)MemberwiseClone();
                p.Own = Own.Select(c => c.Copy()).ToList();
                p.Enemy = Enemy.Select(c => c.Copy()).ToList();
                p.Steps = new List<BattleStep>(Steps);
                p.Extra = new HashSet<int>(Extra);
                return p;
            }
        }
        private sealed class BattleRoute
        {
            public List<BattleStep> Steps = new List<BattleStep>();
            public int Damage, Expanded;
            public bool Lethal;
            public string Reason;
        }

        private BattleBody ObserveBattleBody(ClientCard c)
        {
            return new BattleBody { Card = c, Id = c.Id, Attack = Math.Max(0, c.Attack),
                Defense = Math.Max(0, c.Defense), Attribute = c.Attribute,
                Extra = c.Sequence >= 5, Spent = c.Attacked || !c.IsAttack() ||
                    ControlledEnemyCards.Contains(c), AttackPosition = c.IsAttack(), Disabled = c.IsDisabled() };
        }

        private bool HasUnmodeledBattleDefense()
        {
            return Enemy.GetSpellCount() > 0 || Enemy.GetMonsters().Any(c =>
                c.Id == 0 || !c.IsFaceup() || c.HasType(CardType.Effect) ||
                c.IsMonsterInvincible() || c.IsMonsterDangerous()) ||
                Enemy.Graveyard.Any(c => GetPublicGraveDisruptionValue(c) > 0);
        }

        private BattleRoute GetBattleRoute(bool recoverLinkage = false)
        {
            if (Duel.Player != 0 || Duel.Turn == 1 || Duel.Phase >= DuelPhase.Main2 ||
                Duel.Phase < DuelPhase.Main1 || Duel.CurrentChain.Count > 0 && !recoverLinkage)
                return new BattleRoute { Reason = "outside an executable battle planning window" };
            string key = GetDecisionObservationKey() + "/" + ZeroEffectSpent + "/" + RayeTagSpent +
                "/" + KagariSummoned + "/" + ZeroSummoned + "/" + recoverLinkage + "/" +
                string.Join(",", Duel.MainPhase.ActivableCards.Select(c => c.Id));
            if (key == BattlePlanKey && CachedBattleRoute != null)
                return CachedBattleRoute;
            var result = new BattleRoute { Reason = "no lethal within supported observed-state search" };
            if (HasUnmodeledBattleDefense())
                result.Reason = "unknown/unsupported defense; no lethal certificate";
            else
            {
                var root = new BattlePosition
                {
                    Own = Bot.GetMonsters().Select(ObserveBattleBody).ToList(),
                    Enemy = Enemy.GetMonsters().Select(ObserveBattleBody).ToList(),
                    Linkages = Bot.Hand.Concat(Bot.GetSpells()).Count(c => c.IsCode(CardId.Linkage) &&
                        !SpellsSetThisTurn.Contains(c) && !c.IsDisabled()) +
                        (recoverLinkage ? 1 : 0),
                    Spells = Math.Max(0, GetSpellCountInGrave() - (recoverLinkage ? 1 : 0)),
                    GraveAttributes = Bot.Graveyard.Where(IsSkyStrikerAce)
                        .Aggregate(0, (bits, c) => bits | c.Attribute),
                    ZeroSpent = ZeroEffectSpent, RayeSpent = RayeTagSpent,
                    RayeAccess = Bot.HasInDeck(CardId.Raye) || Bot.HasInGraveyard(CardId.Raye),
                    RozeAccess = Bot.HasInDeck(CardId.Roze) || Bot.HasInGraveyard(CardId.Roze),
                    Removal = Duel.Phase == DuelPhase.Main1 && EmptyMainMonsterZone() &&
                        Duel.MainPhase.ActivableCards.Any(c => c.IsCode(CardId.Afterburners) && !c.IsDisabled())
                };
                if (recoverLinkage)
                    foreach (BattleBody body in root.Own.Where(c => c.Id == CardId.Kagari && !c.Disabled))
                        body.Attack = Math.Max(0, body.Attack - 100);
                foreach (ClientCard c in Bot.ExtraDeck)
                    if (c.IsCode(CardId.Kagari) && !KagariSummoned || c.IsCode(CardId.Zero) && !ZeroSummoned ||
                        c.IsCode(CardId.Hayate) && !HayateSummoned || c.IsCode(CardId.Shizuku) && !ShizukuSummoned ||
                        c.IsCode(CardId.Kaina) && !KainaSummoned)
                        root.Extra.Add(c.Id);
                SearchBattle(root, result, Math.Max(1, Enemy.LifePoints));
            }
            BattlePlanKey = key;
            CachedBattleRoute = result;
            if (result.Lethal)
                Logger.DebugWriteLine("[SkyStriker2026:battle] observed-line lethal damage=" +
                    result.Damage + " expanded=" + result.Expanded + " route=" +
                    string.Join(" > ", result.Steps.Select(s => s.ToString())) +
                    "; conditional on no unknown opponent response; replan after every event");
            return result;
        }

        // Deterministic bounds, independent of the setup beam. No random draws,
        // hidden defenders, arbitrary Lua simulation, or increased setup budget.
        private void SearchBattle(BattlePosition p, BattleRoute best, int life)
        {
            if (best.Expanded >= 768 || p.Steps.Count >= 14 || best.Lethal)
                return;
            best.Expanded++;
            if (p.Damage > best.Damage)
            {
                best.Damage = p.Damage;
                best.Steps = new List<BattleStep>(p.Steps);
            }
            if (p.Damage >= life)
            {
                best.Lethal = true;
                best.Reason = "supported line reaches current LP without resource generation";
                return;
            }
            for (int i = 0; i < p.Own.Count; i++)
            {
                BattleBody attacker = p.Own[i];
                if (attacker.Spent || attacker.Attack <= 0)
                    continue;
                if (p.Enemy.Count == 0)
                {
                    BattlePosition next = p.Copy();
                    next.Own[i].Spent = true;
                    next.Damage += attacker.Attack;
                    next.Steps.Add(new BattleStep { Kind = "attack", Source = attacker.Card });
                    SearchBattle(next, best, life);
                }
                for (int j = 0; j < p.Enemy.Count; j++)
                {
                    BattleBody defender = p.Enemy[j];
                    if (attacker.Attack <= (defender.AttackPosition ? defender.Attack : defender.Defense))
                        continue;
                    BattlePosition next = p.Copy();
                    next.Own[i].Spent = true;
                    next.Enemy.RemoveAt(j);
                    next.Damage += defender.AttackPosition ? attacker.Attack - defender.Attack : 0;
                    next.Steps.Add(new BattleStep { Kind = "attack", Source = attacker.Card, Target = defender.Card });
                    SearchBattle(next, best, life);
                }
            }
            if (p.Removal && p.Steps.Count == 0)
            {
                for (int j = 0; j < p.Enemy.Count; j++)
                {
                    ClientCard target = p.Enemy[j].Card;
                    if (target == null || target.IsShouldNotBeTarget() || target.IsShouldNotBeSpellTrapTarget())
                        continue;
                    BattlePosition next = p.Copy();
                    next.Removal = false;
                    next.Enemy.RemoveAt(j);
                    next.Steps.Add(new BattleStep { Kind = "remove", Target = target });
                    SearchBattle(next, best, life);
                }
            }
            for (int i = 0; i < p.Own.Count; i++)
            {
                BattleBody material = p.Own[i];
                if (material.Id == CardId.Zero && !material.Disabled && !p.ZeroSpent &&
                    p.RayeAccess && p.RozeAccess && p.Own.Count(c => !c.Extra) <= 3 && material.Spent)
                {
                    BattlePosition next = p.Copy();
                    next.Own.RemoveAt(i);
                    next.ZeroSpent = true;
                    next.Own.Add(new BattleBody { Id = CardId.Raye, Attack = 1500, AttackPosition = true });
                    next.Own.Add(new BattleBody { Id = CardId.Roze, Attack = 1500, AttackPosition = true });
                    next.Steps.Add(new BattleStep { Kind = "zero-exit", Source = material.Card });
                    // Optional destruction is omitted here: do not guess an
                    // unknown destruction/revival trigger in a lethal certificate.
                    SearchBattle(next, best, life);
                }
                bool linkage = material.Extra && material.Spent && p.Linkages > 0 &&
                    p.Own.All(c => c.Extra);
                // Shizuku leaving can restore opposing ATK/DEF. Until that
                // continuous-effect transition is modeled, do not certify it.
                if (material.Id == CardId.Shizuku && !material.Disabled && p.Enemy.Count > 0)
                    linkage = false;
                bool raye = material.Id == CardId.Raye && !material.Disabled &&
                    material.Spent && !p.RayeSpent && p.Own.All(c => !c.Extra);
                if (!linkage && !raye)
                    continue;
                foreach (int id in p.Extra.OrderBy(c => c == CardId.Kagari ? 0 : c == CardId.Zero ? 1 : 2))
                {
                    BattlePosition next = p.Copy();
                    next.Extra.Remove(id);
                    next.Own.RemoveAt(i);
                    next.GraveAttributes |= material.Attribute;
                    if (material.Id == CardId.Raye) next.RayeAccess = true;
                    if (material.Id == CardId.Roze) next.RozeAccess = true;
                    if (linkage) { next.Linkages--; next.Spells++; }
                    else next.RayeSpent = true;
                    ClientCard extra = Bot.ExtraDeck.First(c => c.Id == id);
                    int attrs = next.GraveAttributes | extra.Attribute;
                    int boost = linkage && (attrs & (int)CardAttribute.Light) != 0 &&
                        (attrs & (int)CardAttribute.Dark) != 0 ? 1000 : 0;
                    int attack = id == CardId.Zero ? 2000 : id == CardId.Kaina ? 1500 : 1500;
                    if (id == CardId.Kagari)
                    {
                        // Recover the spent Linkage, not an imaginary extra card.
                        if (linkage) { next.Linkages++; next.Spells--; }
                        attack += Math.Max(0, next.Spells) * 100;
                    }
                    next.Own.Add(new BattleBody { Id = id, Attack = attack + boost,
                        Extra = true, AttackPosition = true, Attribute = extra.Attribute });
                    next.Steps.Add(new BattleStep { Kind = linkage ? "linkage" : "raye-tag",
                        Source = material.Card, Summon = id });
                    SearchBattle(next, best, life);
                }
            }
        }

        private bool BattleLethalToBattle()
        {
            BattleRoute route = GetBattleRoute();
            return route.Lethal && route.Steps.Count > 0 && route.Steps[0].Kind == "attack";
        }

        public override bool OnPreActivate(ClientCard card)
        {
            if (!base.OnPreActivate(card)) return false;
            if (Duel.Player != 0 || Duel.CurrentChain.Count > 0 || Duel.Phase != DuelPhase.Main1)
                return true;
            BattleRoute route = GetBattleRoute();
            if (!route.Lethal || route.Steps.Count == 0) return true;
            BattleStep step = route.Steps[0];
            bool allowed = step.Kind == "remove" && card.IsCode(CardId.Afterburners) ||
                step.Kind == "zero-exit" && card == step.Source ||
                step.Kind == "raye-tag" && card == step.Source;
            if (!allowed && BattleDecisionTraces.Add(BattlePlanKey + "/" + card.Id))
                Logger.DebugWriteLine("[SkyStriker2026:decision] PASS card=" + card.Id +
                    " reason=preserve supported lethal; next=" + step);
            return allowed;
        }

        private bool HasFreeBattleRemoval(ClientCard target)
        {
            if (Duel.Player != 0 || Duel.Turn <= 1 || Duel.Phase >= DuelPhase.Main2 ||
                target == null || !target.IsFaceup() || Enemy.GetSpellCount() > 0 ||
                target.IsMonsterInvincible() || target.IsMonsterDangerous() ||
                target.HasType(CardType.Effect) || HasUnmodeledBattleDefense())
                return false;
            return Bot.GetMonsters().Any(c => c.IsFaceup() && c.IsAttack() &&
                (!c.Attacked || c == Bot.BattlingMonster) &&
                !ControlledEnemyCards.Contains(c) && c.Attack > target.GetDefensePower());
        }
    }
}
