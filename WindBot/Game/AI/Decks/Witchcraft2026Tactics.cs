using System;
using System.Collections.Generic;
using System.Linq;
using YGOSharp.OCGWrapper.Enums;

namespace WindBot.Game.AI.Decks
{
    public partial class Witchcraft2026Executor
    {
        // Deliberately bounded, public-information forecast. Unknown effects stay unknown;
        // this is not an invocation of the engine or a universal card-effect interpreter.
        internal sealed class WChainForecast
        {
            public readonly HashSet<ClientCard> Removed = new HashSet<ClientCard>();
            public readonly HashSet<ClientCard> Disabled = new HashSet<ClientCard>();
            public readonly HashSet<int> Negated = new HashSet<int>();
            public readonly List<string> Trace = new List<string>();
            public readonly HashSet<ClientCard> GraveFollowups = new HashSet<ClientCard>();
        }

        private string _lastTacticalDiagnostic;
        private ClientCard _viceRemoval;

        internal static WChainForecast ForecastChain(IList<ChainInfo> links, IEnumerable<int> negated,
            ClientCard viceRemoval, IEnumerable<ClientCard> publicMonsters = null)
        {
            WChainForecast f = new WChainForecast();
            foreach (int index in negated) f.Negated.Add(index - 1);
            for (int i = links.Count - 1; i >= 0; i--)
            {
                ChainInfo link = links[i];
                ClientCard source = link.RelatedCard;
                if (f.Negated.Contains(i)) { f.Trace.Add(i + ":negated"); continue; }
                if (link.ActivateLocation == CardLocation.MonsterZone && source != null &&
                    (f.Disabled.Contains(source) || (source.Location == CardLocation.MonsterZone && source.IsDisabled())))
                { f.Trace.Add(i + ":disabled"); continue; }
                if ((link.ActivateId == CardId.RedEyesDarkDragoon &&
                    link.ActivateDescription == UtilString(CardId.RedEyesDarkDragoon, 1)) ||
                    (link.ActivateId == CardId.Distortion && link.ActivateLocation == CardLocation.SpellZone) ||
                    link.ActivateId == CardId.AshBlossom)
                {
                    if (i > 0)
                    {
                        f.Negated.Add(i - 1);
                        ClientCard destroyed = links[i - 1].RelatedCard;
                        if (link.ActivateId != CardId.AshBlossom && destroyed != null &&
                            (destroyed.Location == CardLocation.MonsterZone || destroyed.Location == CardLocation.SpellZone ||
                            destroyed.Location == CardLocation.Hand) &&
                            !DestructionProtected(destroyed, link.ActivatePlayer, f.Disabled.Contains(destroyed)))
                        {
                            f.Removed.Add(destroyed);
                            if (HasGraveFusionFollowup(destroyed)) f.GraveFollowups.Add(destroyed);
                        }
                    }
                    f.Trace.Add(i + ":negate preceding link");
                }
                else if (link.ActivateId == CardId.Haine || link.ActivateId == CardId.Seed ||
                    (link.ActivateId == CardId.MaidGenni && link.ActivateDescription == UtilString(CardId.MaidGenni, 1)))
                {
                    foreach (ClientCard target in link.Targets)
                    {
                        if (f.Removed.Contains(target) || (target.Location != CardLocation.MonsterZone &&
                            target.Location != CardLocation.SpellZone))
                        { f.Trace.Add(i + ":target already absent"); continue; }
                        if (link.ActivateId == CardId.Haine && DestructionProtected(target, link.ActivatePlayer, f.Disabled.Contains(target)))
                        { f.Trace.Add(i + ":destruction uncertain/protected"); continue; }
                        f.Removed.Add(target);
                        f.Trace.Add(i + ":remove " + target.Id);
                    }
                }
                else if (link.ActivateId == CardId.ViceMadame &&
                    link.ActivateDescription == UtilString(CardId.ViceMadame, 0) && viceRemoval != null)
                {
                    if (!DestructionProtected(viceRemoval, link.ActivatePlayer, f.Disabled.Contains(viceRemoval))) f.Removed.Add(viceRemoval);
                    f.Trace.Add(i + ":resolution-choice removal " + viceRemoval.Id);
                }
                else if (link.ActivateId == CardId.MadameVerre &&
                    link.ActivateDescription == UtilString(CardId.MadameVerre, 1))
                {
                    foreach (ClientCard target in publicMonsters ?? Enumerable.Empty<ClientCard>())
                        if (target.Controller != link.ActivatePlayer && target.IsFaceup() &&
                            target.Location == CardLocation.MonsterZone && !f.Removed.Contains(target)) f.Disabled.Add(target);
                    f.Trace.Add(i + ":negate currently surviving opposing monsters");
                }
                else if (link.ActivateId == 98338152) // Widow Anchor: removing its spell is NOT negation.
                {
                    foreach (ClientCard target in link.Targets)
                        if (!f.Removed.Contains(target) && target.Location == CardLocation.MonsterZone)
                            f.Disabled.Add(target);
                    f.Trace.Add(i + ":Anchor checks surviving monster, not spell presence");
                }
                else f.Trace.Add(i + ":unknown/other outcome " + link.ActivateId);
            }
            return f;
        }

        private WChainForecast ChainForecast()
        {
            if (Duel.SolvingChainIndex > 0)
                return ForecastChain(Duel.CurrentChainInfo.Take(Duel.SolvingChainIndex - 1).ToList(),
                    Duel.NegatedChainIndexList, null, Bot.GetMonsters().Concat(Enemy.GetMonsters()));
            return ForecastChain(Duel.CurrentChainInfo, Duel.NegatedChainIndexList, _viceRemoval,
                Bot.GetMonsters().Concat(Enemy.GetMonsters()));
        }

        private bool TacticalDecision(string action, double gain, double cost, string reason, ClientCard target = null)
        {
            bool accept = gain > cost;
            string text = "Witchcraft2026 tactic: " + action + " target=" + (target == null ? 0 : target.Id) +
                " pass=0 gain=" + gain.ToString("0.0") + " cost/opportunity=" + cost.ToString("0.0") +
                " choose=" + (accept ? action : "Pass") + " reason=" + reason +
                " chain=" + string.Join(",", Duel.CurrentChainInfo.Select((c, i) =>
                    (i + 1) + ":" + c.ActivatePlayer + "/" + c.ActivateId + "/targets=" +
                    string.Join("+", c.Targets.Select(t => t.Id)))) +
                " reverse=" + string.Join(";", ChainForecast().Trace);
            if (text != _lastTacticalDiagnostic) Logger.DebugWriteLine(text);
            _lastTacticalDiagnostic = text;
            return accept;
        }

        private double RemovalValue(ClientCard target, bool targeting)
        {
            if (target == null || target.Controller != 1 || KnownDestructionProtected(target) ||
                (targeting && (target.IsFacedown() || target.IsShouldNotBeTarget() || target.IsShouldNotBeMonsterTarget()))) return 0;
            WChainForecast forecast = ChainForecast();
            if (forecast.Removed.Contains(target)) return 0;
            // A resolving normal/quick-play spell will go to GY anyway, and destruction
            // does not stop its effect. Continuous/field/equip spells are different.
            if (Duel.CurrentChainInfo.Any(c => ReferenceEquals(c.RelatedCard, target)) &&
                target.IsSpell() && !target.HasType(CardType.Continuous) &&
                !target.HasType(CardType.Field) && !target.HasType(CardType.Equip)) return 0;
            return target.IsFloodgate() ? 28 : target.IsMonster() ? 10 + Math.Max(0, target.Attack) / 500.0 : 8;
        }

        private static bool KnownDestructionProtected(ClientCard card)
        {
            // Battle invincibility helpers do not imply effect-destruction immunity.
            return DestructionProtected(card, 0, false);
        }

        private static bool HasGraveFusionFollowup(ClientCard card)
        {
            return card != null && card.Id == GraveFusionTrigger && card.Location != CardLocation.Grave &&
                card.Location != CardLocation.Removed && card.Location != CardLocation.Deck &&
                card.Location != CardLocation.Extra;
        }

        private ClientCard RemovalTarget(bool targeting)
        {
            return Enemy.GetMonsters().Concat(Enemy.GetSpells()).Where(c => RemovalValue(c, targeting) > 0)
                .OrderByDescending(c => RemovalValue(c, targeting)).FirstOrDefault();
        }

        private bool ValuableNegate(string name, double cost)
        {
            if (Duel.LastChainPlayer != 1 || Duel.CurrentChainInfo.Count == 0) return false;
            ChainInfo threat = Duel.CurrentChainInfo.Last();
            WChainForecast forecast = ChainForecast();
            double gain = 12; // unknown live effect: one interaction, never assumed a board wipe
            if (forecast.Negated.Contains(Duel.CurrentChainInfo.Count - 1)) gain = 0;
            else if (threat.ActivateId == 98338152 && threat.Targets.Count > 0 &&
                threat.Targets.All(t => forecast.Removed.Contains(t) || t.Location != CardLocation.MonsterZone)) gain = 0;
            else if (threat.ActivateId == CardId.AshBlossom)
            {
                ChainInfo protectedLink = Duel.CurrentChainInfo.Count > 1 ?
                    Duel.CurrentChainInfo[Duel.CurrentChainInfo.Count - 2] : null;
                // A search on an established board is optional advantage. An opening
                // search with no functioning boss is an engine survival requirement.
                bool established = Bot.GetMonsters().Count(c => c.IsFaceup() && !c.IsDisabled() &&
                    c.IsCode(CardId.RedEyesDarkDragoon, CardId.ViceMadame, CardId.Haine, CardId.MadameVerre)) >= 2;
                gain = protectedLink != null && protectedLink.ActivatePlayer == 0 ? (established ? 3 : 24) : 0;
            }
            else if (threat.ActivateId == 76072561 && threat.ActivateDescription == UtilString(76072561, 1))
                gain = 30; // verified Zero: two recruits plus removal, after tribute already paid
            else if (threat.Targets.Any(t => t.Controller == 0 && !forecast.Removed.Contains(t)))
                gain = 24;
            double reserve = Enemy.GetMonsters().Any(c => c.IsFaceup() && !c.IsDisabled() && c.Id == 76072561) ? 6 : 0;
            // Destroying the known hand activation by EFFECT sends it to GY and
            // enables a new Fusion trigger. Its materials/Extra are hidden: do not
            // claim Winda is guaranteed. With no identified urgent protected target,
            // retain this negate for the actual GY offer instead of trading it for
            // another live activation. A higher-priority observed threat can override.
            if (HasGraveFusionFollowup(threat.RelatedCard) && !_graveFusionSpent.Contains(threat.ActivateId) && gain <= 12)
                return TacticalDecision(name, 0, cost + reserve,
                    "negate+destroy -> GY by effect -> possible Fusion trigger; hidden materials unknown; reserve for actual follow-up");
            return TacticalDecision(name, gain, cost + reserve, "live threat vs paid resource and reserved interruption");
        }

        private bool QuickResourceAction()
        {
            if (Duel.Phase != DuelPhase.Main1 && Duel.Phase != DuelPhase.Main2) return false;
            bool lacrima = Card.Id == CardId.Lacrima && Duel.LastChainPlayer == 1;
            bool low = LowIds.Contains(Card.Id) && Card.Location == CardLocation.MonsterZone &&
                ActivateDescription == UtilString(Card.Id, 0);
            bool maid = Card.Id == CardId.MaidGenni && Card.Location == CardLocation.MonsterZone &&
                ActivateDescription == UtilString(Card.Id, 1);
            bool fieldThreat = Duel.LastChainPlayer == 1 && Duel.CurrentChainInfo.Count > 0 &&
                Duel.CurrentChainInfo.Last().Targets.Any(t => t.Controller == 0 && t.Id == CardId.Walpurgis);
            if (!lacrima && !(Duel.Player == 1 && (low || maid)) && !(low && fieldThreat)) return false;
            WState state = Observe();
            // Quick responses compare only executable consequences, not an imagined
            // own Main Phase after a hostile chain. Costs and material loss are included.
            WAction best = null;
            double delta = 0;
            List<string> choices = new List<string> { "Pass=0 (retain current board/resources)" };
            foreach (WAction action in WPlanner.Actions(state).Where(a => Matches(a, Card, ActivateDescription)))
            {
                if (lacrima && action.Kind != WKind.LacrimaRecruit) continue;
                WState after = WPlanner.Apply(state, action);
                // A resolved Maid Verre summon offers its real search trigger next.
                if (after.Pending == CardId.MaidVerre)
                {
                    WAction search = WPlanner.Actions(after).FirstOrDefault(a => a.Kind == WKind.MaidSearch);
                    if (search != null) after = WPlanner.Apply(after, search);
                }
                double value = WPlanner.Evaluate(after) - WPlanner.Evaluate(state);
                choices.Add(action + " delta=" + value.ToString("0.0") + " endpoint=" + WPlanner.BoardSummary(after));
                if (value > delta) { delta = value; best = action; }
            }
            if (!TacticalDecision("quick-resource", delta, .01,
                (best == null ? "no improvement over retaining board" : best.ToString()) +
                " candidates=" + string.Join(" | ", choices.Take(5)) + " total=" + choices.Count)) return false;
            AcceptAction(best);
            return true;
        }

        private bool TryBattleForLethal()
        {
            if (Duel.Player != 0 || Duel.Phase != DuelPhase.Main1 || Duel.MainPhase == null ||
                !Duel.MainPhase.CanBattlePhase || Duel.CurrentChain.Count != 0) return false;
            // Conservative empty-field case only. No assumed removal of blockers,
            // no attacks from defense position, no extra attacks, no unknown backrow.
            int damage = Bot.GetMonsters().Where(c => c.IsFaceup() && c.IsAttack() && !c.Attacked &&
                IsSpellcasterMaterial(c.Id)).Sum(c => Math.Max(0, c.Attack));
            bool clear = Enemy.GetMonsters().Count == 0 && Enemy.GetSpells().Count == 0;
            bool lethal = clear && damage >= Enemy.LifePoints && Enemy.LifePoints > 0;
            if (lethal) Logger.DebugWriteLine("Witchcraft2026 lethal: potential=" + damage +
                " LP=" + Enemy.LifePoints + " empty public field; enter Battle, engine attack offers remain authoritative; hidden responses unknown");
            return lethal;
        }

        public override BattlePhaseAction OnBattle(IList<ClientCard> attackers, IList<ClientCard> defenders)
        {
            // Unlike the Main Phase forecast these are authoritative attack offers.
            // Recompute after every attack/response; never execute a cached attack list.
            if (defenders.Count == 0 && Enemy.GetSpells().Count == 0)
            {
                List<ClientCard> direct = attackers.Where(c => c.CanDirectAttack && !c.Attacked)
                    .OrderByDescending(c => c.Attack).ToList();
                if (direct.Count > 0 && direct.Sum(c => Math.Max(0, c.Attack)) >= Enemy.LifePoints)
                {
                    Logger.DebugWriteLine("Witchcraft2026 lethal: engine-offered direct attack=" +
                        direct[0].Id + " attack=" + direct[0].Attack + " remainingLP=" + Enemy.LifePoints);
                    return AI.Attack(direct[0], null);
                }
            }
            return base.OnBattle(attackers, defenders);
        }

        internal double EqualTradeValue(ClientCard attacker, ClientCard defender)
        {
            WState before = Observe();
            if (!before.Has(attacker.Id, WZone.Board)) return double.NegativeInfinity;
            WState after = before.Copy(); after.Move(attacker.Id, WZone.Board, WZone.Grave);
            WRestriction removed = after.Restrictions.FirstOrDefault(r => r.Id == defender.Id && r.Player == 1);
            if (removed != null) after.Restrictions.Remove(removed);
            after.IncomingAttack = Math.Max(0, after.IncomingAttack - defender.Attack);
            // Compare actual modeled continuations, not a blanket floodgate bonus.
            double preserved, traded;
            using (var budget = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token))
            {
                budget.CancelAfter(180); // one budget for both sides of this comparison
                preserved = WPlanner.Plan(before, null, budget.Token).Score;
                traded = WPlanner.Plan(after, null, budget.Token).Score;
            }
            bool recovers = defender.HasSetcode(0x9d) && Enemy.Graveyard.Any(c =>
                c.HasSetcode(0x9d) && (c.IsSpell() || c.IsTrap()));
            return traded - preserved + 10 + Math.Max(0, defender.Attack) / 500.0 - (recovers ? 2 : 0);
        }

        public override bool OnPreBattleBetween(ClientCard attacker, ClientCard defender)
        {
            if (!base.OnPreBattleBetween(attacker, defender)) return false;
            if (defender.IsFaceup() && defender.IsAttack() && attacker.RealPower == defender.RealPower &&
                attacker.RealPower > 0 && IsSpellcasterMaterial(attacker.Id))
            {
                double value = EqualTradeValue(attacker, defender);
                return TacticalDecision("equal-ATK trade", value, 0,
                    "both bodies lost; public GY recovery and legal Main2 continuation vs preserve; unexposed protection not assumed", defender);
            }
            return true;
        }
    }
}
