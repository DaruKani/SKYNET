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
            OpponentAwareness semantics = new OpponentAwareness();
            foreach (int index in negated) f.Negated.Add(index - 1);
            for (int i = links.Count - 1; i >= 0; i--)
            {
                ChainInfo link = links[i];
                ClientCard source = link.RelatedCard;
                if (f.Negated.Contains(i)) { f.Trace.Add(i + ":negated"); continue; }
                OpponentEffect known = semantics.Identify(link);
                if (known != null && known.RequiresFaceup && source != null &&
                    (f.Removed.Contains(source) || !source.IsFaceup() || source.Location != link.ActivateLocation))
                { f.Trace.Add(i + ":reviewed source-presence dependency fails"); continue; }
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
                else f.Trace.Add(i + ":" + (known == null ? "unknown/other outcome " + link.ActivateId :
                    known.Name + (known.RequiresFaceup ? "/requires face-up source" : "/removal alone does not negate")));
            }
            return f;
        }

        private WChainForecast ChainForecast()
        {
            _opponent.Observe(Duel.CurrentChainInfo);
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
            OpponentThreat threat = OpponentAssessment(target, forecast);
            // Floodgate restriction profiles are separately verified by Outcomes.
            double restriction = target.IsCode(SummonLimiter, SpellSummonLimiter) && !target.IsDisabled() &&
                !forecast.Disabled.Contains(target) ? 20 : 0;
            return Math.Max(0, threat.Removal + restriction);
        }

        private OpponentThreat OpponentAssessment(ClientCard target, WChainForecast forecast)
        {
            IList<ChainInfo> links = Duel.SolvingChainIndex > 0 ?
                Duel.CurrentChainInfo.Take(Duel.SolvingChainIndex - 1).ToList() : Duel.CurrentChainInfo;
            return _opponent.Assess(target, links, forecast.Negated,
                forecast.Disabled.Contains(target), Duel.Player == target.Controller);
        }

        private double InterruptionCost(double baseCost)
        {
            int spells = Bot.Hand.Count(c => c.IsSpell());
            // Last Spell also funds the remaining Verre/Haine defense. This is an
            // opportunity cost, not knowledge of an unseen future activation.
            return baseCost + (spells <= 1 ? 4 : 0);
        }

        private bool WorthVerre()
        {
            WChainForecast forecast = ChainForecast();
            double gain = 0;
            List<string> reasons = new List<string>();
            foreach (ClientCard monster in Enemy.GetMonsters().Where(c => c.IsFaceup() && !c.IsDisabled() &&
                !forecast.Removed.Contains(c) && !forecast.Disabled.Contains(c)))
            {
                OpponentThreat threat = OpponentAssessment(monster, forecast);
                double live = threat.NegateNow;
                if (!threat.Supported && Duel.CurrentChainInfo.Select((c, i) => new { c, i }).Any(x =>
                    ReferenceEquals(x.c.RelatedCard, monster) && !forecast.Negated.Contains(x.i))) live = 12;
                gain += live + threat.Future * .25; // temporary disable, not permanent removal
                reasons.Add(monster.Id + ":" + threat.Explain());
            }
            return TacticalDecision("Verre", gain, InterruptionCost(7),
                "only surviving face-up monsters; spell link is not negated; reserve OPT vs " + string.Join(" | ", reasons.Take(5)));
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
            var candidates = Enemy.GetMonsters().Concat(Enemy.GetSpells())
                .Select(c => new { Card = c, Value = RemovalValue(c, targeting) })
                .OrderByDescending(c => c.Value).ToList();
            ClientCard selected = candidates.Where(c => c.Value > 0).Select(c => c.Card).FirstOrDefault();
            WChainForecast forecast = ChainForecast();
            string diagnostic = "Witchcraft2026 opponent targets: " + string.Join(" | ", candidates.Take(8).Select(c =>
                c.Card.Id + " value=" + c.Value.ToString("0.0") + " " + OpponentAssessment(c.Card, forecast).Explain())) +
                " selected=" + (selected == null ? 0 : selected.Id) + " method=largest-legal-marginal-removal; pending removals/protection excluded";
            if (_lastOpponentDiagnostic != diagnostic) Logger.DebugWriteLine(diagnostic);
            _lastOpponentDiagnostic = diagnostic;
            return selected;
        }

        private string _lastOpponentDiagnostic;

        private bool ValuableNegate(string name, double cost)
        {
            if (Duel.LastChainPlayer != 1 || Duel.CurrentChainInfo.Count == 0) return false;
            ChainInfo threat = Duel.CurrentChainInfo.Last();
            WChainForecast forecast = ChainForecast();
            double gain = 12; // unknown live effect: one interaction, never assumed a board wipe
            OpponentEffect semantics = _opponent.Identify(threat);
            if (semantics != null) gain = semantics.Utility;
            if (forecast.Negated.Contains(Duel.CurrentChainInfo.Count - 1)) gain = 0;
            else if (threat.ActivateLocation == CardLocation.MonsterZone && threat.RelatedCard != null &&
                (threat.RelatedCard.IsDisabled() || forecast.Disabled.Contains(threat.RelatedCard))) gain = 0;
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
            double reserve = Enemy.GetMonsters().Where(c => c.IsFaceup() && !c.IsDisabled() &&
                !forecast.Removed.Contains(c)).Select(c => OpponentAssessment(c, forecast).Future * .25).DefaultIfEmpty(0).Max();
            // Destroying the known hand activation by EFFECT sends it to GY and
            // enables a new Fusion trigger. Its materials/Extra are hidden: do not
            // claim Winda is guaranteed. With no identified urgent protected target,
            // retain this negate for the actual GY offer instead of trading it for
            // another live activation. A higher-priority observed threat can override.
            if (HasGraveFusionFollowup(threat.RelatedCard) && !_graveFusionSpent.Contains(threat.ActivateId) && gain <= 12)
                return TacticalDecision(name, 0, cost + reserve,
                    "negate+destroy -> GY by effect -> possible Fusion trigger; hidden materials unknown; reserve for actual follow-up");
            return TacticalDecision(name, gain, cost + reserve, "live effect=" +
                (semantics == null ? "unsupported/uncertain" : semantics.Name) +
                "; committed activation != source removal; future reserve=" + reserve.ToString("0.0"));
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
