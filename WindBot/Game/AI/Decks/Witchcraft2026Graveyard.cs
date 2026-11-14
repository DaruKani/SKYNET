using System;
using System.Collections.Generic;
using System.Linq;
using YGOSharp.OCGWrapper.Enums;

namespace WindBot.Game.AI.Decks
{
    public partial class Witchcraft2026Executor
    {
        private readonly Dictionary<ClientCard, int> _graveEntered = new Dictionary<ClientCard, int>();
        private readonly HashSet<ClientCard> _markaRevived = new HashSet<ClientCard>();
        private readonly List<ClientCard> _seedPicked = new List<ClientCard>();
        private bool _unveilingProtection;

        private static bool IsGraveSupported(int id)
        {
            return LowIds.Contains(id) || id == CardId.Seed || id == CardId.Marka ||
                id == CardId.Distortion || id == CardId.MadameRillie || id == CardId.DespianLuluwalilith;
        }

        private void ObserveGraveMove(ClientCard card, int previous, int controller, int current)
        {
            if (previous == current) return;
            if (controller == 0 && current == (int)CardLocation.Grave) _graveEntered[card] = Duel.Turn;
            else _graveEntered.Remove(card);
            if (previous == (int)CardLocation.MonsterZone) _markaRevived.Remove(card);
        }

        private void ObserveGraveResolution(ChainInfo info)
        {
            if (info.ActivatePlayer == 0 && info.ActivateId == CardId.Marka &&
                info.ActivateLocation == CardLocation.Grave && info.RelatedCard != null &&
                info.RelatedCard.Location == CardLocation.MonsterZone) _markaRevived.Add(info.RelatedCard);
        }

        private void ObserveGraveState(WState s)
        {
            s.SpellcasterProtected = _unveilingProtection;
            s.DistortionReady = Bot.Graveyard.Any(c => c.Id == CardId.Distortion &&
                _graveEntered.ContainsKey(c) && _graveEntered[c] < Duel.Turn);
            // A current engine offer is stronger evidence than a missing history
            // snapshot. Never use an idle list retained during a chain window.
            if (Card != null && Card.Id == CardId.Distortion && Card.Location == CardLocation.Grave &&
                ActivateDescription == UtilString(CardId.Distortion, 1) &&
                (AI.SelectingEffectYn || (_chainOffers != null && _chainOffers.Contains(Card)) ||
                (Duel.CurrentChain.Count == 0 && Duel.MainPhase != null && Duel.MainPhase.ActivableCards.Contains(Card))))
                s.DistortionReady = true;
            foreach (ClientCard c in _markaRevived.Where(c => c.Location == CardLocation.MonsterZone))
                s.BanishOnLeave.Add(c.Id);
        }

        // Marginal hand utility for cycling only; it does not rewrite the main
        // board evaluator or pretend an unknown draw is a specific combo card.
        internal static double CycleValue(WState s, int id)
        {
            bool duplicate = s.Count(id, WZone.Hand) > 1;
            bool spent = s.Used.Any(u => u.StartsWith(id + ":"));
            if (id == CardId.AshBlossom || id == CardId.MaxxC) return spent ? 1 : 4;
            if (id == CardId.Walpurgis) return s.Has(id, WZone.Backrow) || duplicate ? .5 : 5;
            if (id == CardId.Bystreet && s.Has(id, WZone.Backrow)) return .5;
            if (spent && IsModelSpell(id)) return 1;
            if (id == CardId.Creation || id == CardId.SpellPowerMastery || id == CardId.Terraforming ||
                id == CardId.RainbowBridgeBifrost) return duplicate ? 1 : 4;
            if (LowIds.Contains(id)) return s.Normal ? 3 : 1;
            if (id == CardId.Marka || id == CardId.MaidGenni || id == CardId.MaidVerre || id == CardId.Terracottan)
                return spent || duplicate ? 1 : 3;
            if (id == CardId.Haine || id == CardId.MadameVerre || id == CardId.Edel)
                return s.Has(CardId.Unveiling, WZone.Hand) || s.Active(CardId.Edel) ? 3 : 1;
            return IsModelSpell(id) ? 2.5 : 1.5;
        }

        private static double AverageDrawValue(WState s)
        {
            int total = s.Size(WZone.Deck);
            return total == 0 ? 0 : s.In(WZone.Deck).Sum(id => CycleValue(s, id) * s.Count(id, WZone.Deck)) / total;
        }

        private static double DrawRisk(WState s, int draws)
        {
            double mean = AverageDrawValue(s);
            double variance = s.In(WZone.Deck).Sum(id => s.Count(id, WZone.Deck) *
                Math.Pow(CycleValue(s, id) - mean, 2)) / Math.Max(1, s.Size(WZone.Deck));
            // Downside sensitivity, not a probability or a promised next hand.
            return Math.Sqrt(variance * draws);
        }

        private static IEnumerable<WAction> GraveActions(WState s)
        {
            if (!s.OpponentTurn && s.Has(CardId.Potterie, WZone.Grave) && s.Size(WZone.Hand) + s.UnknownHand == 0)
                foreach (int id in s.In(WZone.Grave).Where(id => IsWitchCard(id) &&
                    (id != CardId.Potterie || s.Count(id, WZone.Grave) > 1)))
                    yield return GraveAction(WKind.PotterieRecover, CardId.Potterie, id, -1);
            if (s.Has(CardId.Distortion, WZone.Grave) && s.DistortionReady)
                foreach (int id in s.In(WZone.Deck).Where(IsHighSpellcaster))
                    foreach (int discard in s.In(WZone.Hand).Concat(new[] { id }).Distinct())
                    {
                        WAction a = GraveAction(WKind.DistortionSearch, CardId.Distortion, id, 1);
                        a.Cost = discard; a.CostZone = WZone.Hand;
                        yield return a;
                    }
            if (!s.OpponentTurn && s.Has(CardId.Seed, WZone.Grave) && s.Size(WZone.Deck) > 0)
            {
                List<int> hand = s.In(WZone.Hand).SelectMany(id => Enumerable.Repeat(id, s.Count(id, WZone.Hand)))
                    .OrderBy(id => CycleValue(s, id)).Take(12).ToList();
                foreach (int spell in hand.Where(IsModelSpell).Distinct())
                {
                    List<int> returns = new List<int> { spell };
                    List<int> rest = new List<int>(hand); rest.Remove(spell);
                    for (int i = 0; i <= rest.Count; i++)
                    {
                        if (i > 0) returns.Add(rest[i - 1]);
                        WAction a = GraveAction(WKind.SeedRedraw, CardId.Seed, 0, 1);
                        a.Materials = returns.ToArray(); yield return a;
                    }
                }
            }
            if (!s.OpponentTurn && s.Has(CardId.Pittore, WZone.Grave) && s.Size(WZone.Deck) > 0)
            {
                // With a nonempty non-Witch hand, a miss banishes the whole hand.
                // Deliberate policy omission, not a missing activation handler.
                foreach (int discard in s.In(WZone.Hand).Where(IsWitchCard))
                {
                    WAction a = GraveAction(WKind.PittoreDraw, CardId.Pittore, 0, -1);
                    a.Cost = discard; a.CostZone = WZone.Hand; yield return a;
                }
                if (s.Size(WZone.Hand) + s.UnknownHand == 0)
                    yield return GraveAction(WKind.PittoreDraw, CardId.Pittore, 0, -1);
            }
            if (!s.OpponentTurn && s.Has(CardId.Genni, WZone.Grave))
                foreach (WAction a in ExtraGenniCopies(s)) yield return a;
        }

        private static bool IsWitchCard(int id)
        {
            return IsWitchMaterial(id) || WitchSpells.Contains(id) || id == CardId.Lacrima || id == CardId.Distortion;
        }

        private static bool IsHighSpellcaster(int id)
        {
            // All Level 5+ main-deck Spellcasters in the selected 40/15/7 list.
            // Pure planning must not call the runtime-only NamedCardsManager.
            return id == CardId.Marka || id == CardId.Edel || id == CardId.Haine || id == CardId.MadameVerre;
        }

        private static WAction GraveAction(WKind kind, int source, int target, int desc)
        {
            return new WAction { Kind = kind, Source = source, Target = target, Location = WZone.Grave,
                Description = desc, Route = "grave effect; actual resources and replan after resolution" };
        }

        private static IEnumerable<WAction> ExtraGenniCopies(WState s)
        {
            // Remove both costs BEFORE selecting recycle materials. Creation and
            // Lacrima recruit retain their existing edges; add their other choices.
            foreach (int spell in new[] { CardId.Celebration, CardId.ConfusionConfession,
                CardId.Unveiling, CardId.Lacrima, CardId.Draping })
            {
                if (!s.Has(spell, WZone.Grave)) continue;
                WState available = s.Copy();
                available.Move(CardId.Genni, WZone.Grave, WZone.Banished);
                available.Move(spell, WZone.Grave, WZone.Banished);
                List<WAction> candidates = new List<WAction>();
                if (spell == CardId.Celebration || spell == CardId.ConfusionConfession)
                {
                    available.Add(spell, WZone.Hand);
                    candidates.AddRange(WPlanner.Fusions(available).Where(a => a.Source == spell));
                    if (spell == CardId.Celebration)
                    {
                        available.OccupiedBackrow = 0; // copying does not place a Spell
                        candidates.AddRange(CoverageActions(available).Where(a => a.Kind == WKind.CelebrationDestroy));
                    }
                }
                if (spell == CardId.Unveiling && WPlanner.MainMonsters(available) < 5)
                    candidates.AddRange(available.In(WZone.Hand).Where(IsWitchMaterial).Select(id =>
                        GraveAction(WKind.Unveiling, CardId.Genni, id, 1)));
                if (spell == CardId.Lacrima)
                    candidates.AddRange(available.In(WZone.Deck).Where(id => IsModelSpell(id) || IsSpellcasterMaterial(id))
                        .Select(id => GraveAction(WKind.LacrimaSend, CardId.Genni, id, 1)));
                if (spell == CardId.Draping && available.In(WZone.Board).Any(IsWitchMaterial))
                    foreach (WRemovalTarget target in available.EnemyTargets.Where(t => t.Key >= 128 && t.Value > 0))
                    {
                        WAction a = GraveAction(WKind.CopiedDraping, CardId.Genni, target.Id, 1);
                        a.EnemyKey = target.Key; candidates.Add(a);
                    }
                foreach (WAction a in candidates)
                {
                    a.Source = CardId.Genni; a.Location = WZone.Grave; a.Description = 1;
                    a.CopiedSpell = spell; a.Route = "Genni copies installed Spell activation mode";
                    yield return a;
                }
            }
        }

        private static void ApplyGraveAction(WState s, WAction a)
        {
            if (a.CopiedSpell == 0) s.Move(a.Source, WZone.Grave, WZone.Banished);
            if (a.Kind == WKind.PotterieRecover)
                s.Move(a.Target, WZone.Grave, IsWitchMaterial(a.Target) && !WitchMonsters.Contains(a.Target) ? WZone.Extra : WZone.Hand);
            if (a.Kind == WKind.DistortionSearch)
            {
                s.Move(a.Target, WZone.Deck, WZone.Hand);
                s.Move(a.Cost, WZone.Hand, WZone.Grave); // resolution, NOT activation cost
            }
            if (a.Kind == WKind.CopiedDraping)
            {
                WRemovalTarget t = s.EnemyTargets.First(e => e.Key == a.EnemyKey);
                s.RemovedThreat += t.Value * .5; // bounce preserves their card, unlike destruction
                s.EnemyTargets.Remove(t);
                s.OpposingResponses &= s.EnemyTargets.Aggregate(OpponentResponse.None, (m, e) => m | e.Responses);
            }
            if (a.Kind == WKind.SeedRedraw)
            {
                double returned = a.Materials.Sum(id => CycleValue(s, id));
                foreach (int id in a.Materials) s.Move(id, WZone.Hand, WZone.Deck);
                s.UnknownHand += a.Materials.Length;
                s.DrawUtility += AverageDrawValue(s) * a.Materials.Length - returned - DrawRisk(s, a.Materials.Length);
                s.RandomBoundary = true;
            }
            if (a.Kind == WKind.PittoreDraw)
            {
                if (a.Cost != 0)
                {
                    s.DrawUtility += AverageDrawValue(s) - CycleValue(s, a.Cost) - DrawRisk(s, 1);
                    s.Move(a.Cost, WZone.Hand, WZone.Grave); s.UnknownHand++;
                }
                else
                {
                    // Empty hand: a Witch draw is sent to GY; another draw is
                    // banished. No retained hand card or invented combo access.
                    s.DrawUtility += s.In(WZone.Deck).Where(IsWitchCard).Sum(id => s.Count(id, WZone.Deck)) /
                        (double)Math.Max(1, s.Size(WZone.Deck));
                }
                s.RandomBoundary = true;
            }
        }

        private bool GraveWindowAction()
        {
            if (Card == null || Card.Controller != 0 || Card.Location != CardLocation.Grave) return false;
            if (Card.Id == CardId.Distortion)
            {
                if (Duel.Phase == DuelPhase.Main1 || Duel.Phase == DuelPhase.Main2) return PlannedAction();
                WState s = Observe();
                WAction best = GraveActions(s).Where(a => a.Kind == WKind.DistortionSearch && !s.Used.Contains(a.Usage))
                    .OrderByDescending(a => CycleValue(s, a.Target) - CycleValue(s, a.Cost)).FirstOrDefault();
                if (best == null || CycleValue(s, best.Target) <= CycleValue(s, best.Cost)) return false;
                AcceptAction(best); return true;
            }
            if (Card.Id == CardId.Marka && (ActivateDescription <= 0 || ActivateDescription == UtilString(Card.Id, 1)))
            {
                ClientCard target = Enemy.GetMonsters().OrderByDescending(c => OpponentAssessment(c, ChainForecast()).Removal).FirstOrDefault();
                if (target == null || Bot.GetMonsters().Count(c => c.Sequence < 5) >= 5) return false;
                AcceptAction(new WAction { Kind = WKind.Tactical, Source = Card.Id, Location = WZone.Grave,
                    Description = 1, Target = target.Id, Route = "offered Marka trigger: revive then non-targeting tribute" });
                return true;
            }
            if (Duel.Phase != DuelPhase.End) return false;
            if (Card.Id == CardId.MadameRillie && Duel.Player == 0 && Bot.Hand.Any(c => c.IsSpell()) &&
                (ActivateDescription <= 0 || ActivateDescription == UtilString(Card.Id, 1)))
            {
                AcceptAction(new WAction { Kind = WKind.Tactical, Source = Card.Id, Location = WZone.Grave,
                    Description = 1, Route = "offered properly-summoned Rilliona revival; reveal not discard" });
                return true;
            }
            if (Card.Id == CardId.DespianLuluwalilith &&
                (ActivateDescription <= 0 || ActivateDescription == UtilString(Card.Id, 1)))
            {
                AcceptAction(new WAction { Kind = WKind.Tactical, Source = Card.Id, Location = WZone.Grave,
                    Description = 1, Route = "offered sent-this-turn Luluwalilith End Phase recruit" });
                return true;
            }
            return false;
        }

        private IList<ClientCard> SelectGraveCards(IList<ClientCard> cards, int min, int max, int hint, bool cancelable)
        {
            WAction a = PromptAction();
            ChainInfo resolving = Duel.GetCurrentSolvingChainInfo();
            int id = a != null && a.Location == WZone.Grave ? a.Source :
                resolving != null && resolving.ActivatePlayer == 0 && resolving.ActivateLocation == CardLocation.Grave ? resolving.ActivateId : 0;
            if (id == 0) return null;
            if (id == CardId.Seed && hint == HintMsg.ToDeck)
            {
                // SelectUnselect can offer one card at a time. Always start with a
                // Spell so the subgroup constraint is satisfiable; finish legally.
                List<int> remaining = a == null ? cards.Select(c => c.Id).ToList() : a.Materials.ToList();
                foreach (ClientCard picked in _seedPicked) remaining.Remove(picked.Id);
                List<ClientCard> desired = new List<ClientCard>();
                foreach (ClientCard offered in cards)
                    if (remaining.Remove(offered.Id)) desired.Add(offered);
                List<ClientCard> ordered = desired.OrderByDescending(c => c.IsSpell()).ThenBy(c => CycleValue(Observe(), c.Id)).ToList();
                if (ordered.Count == 0 && cancelable && min == 0) return new List<ClientCard>();
                if (ordered.Count < min) ordered.AddRange(cards.Where(c => !ordered.Contains(c)).OrderByDescending(c => c.IsSpell()));
                List<ClientCard> selected = ordered.Take(Math.Min(max, Math.Max(min, ordered.Count))).ToList();
                _seedPicked.AddRange(selected);
                return selected;
            }
            if ((id == CardId.Pittore && hint == HintMsg.ToGrave) || (id == CardId.Distortion && hint == HintMsg.Discard))
            {
                WState s = Observe();
                // Re-evaluate after the actual draw/search, not a queued pre-draw ID.
                return cards.OrderBy(c => CycleValue(s, c.Id) - (IsWitchCard(c.Id) ? 1 : 0))
                    .Take(Math.Min(max, Math.Max(1, min))).ToList();
            }
            if (id == CardId.Marka && hint == HintMsg.Release)
                return cards.OrderByDescending(c => OpponentAssessment(c, ChainForecast()).Removal)
                    .Take(Math.Min(max, Math.Max(1, min))).ToList();
            if (id == CardId.MadameRillie && hint == HintMsg.Confirm)
                return cards.OrderBy(c => CycleValue(Observe(), c.Id)).Take(Math.Min(max, Math.Max(1, min))).ToList();
            if (id == CardId.DespianLuluwalilith && hint == HintMsg.SpSummon)
                return cards.OrderByDescending(c => c.Id == CardId.MaidVerre).ThenByDescending(c => c.Attack)
                    .Take(Math.Min(max, Math.Max(1, min))).ToList();
            if (a != null && a.Kind == WKind.CopiedDraping && hint == HintMsg.ReturnToHand)
                return cards.OrderByDescending(c => ((int)c.Location * 16) + c.Sequence + 1 == a.EnemyKey)
                    .Take(Math.Min(max, Math.Max(1, min))).ToList();
            return null;
        }
    }
}
