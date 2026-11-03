using System;
using System.Collections.Generic;
using System.Linq;
using YGOSharp.OCGWrapper.Enums;
using WindBot.Game;

namespace WindBot.Game.AI.Decks
{
    // Tactical RDA layer for choices that compete for the same threat. It captures
    // only observed cards, scores every known response without mutating duel state,
    // then lets the normal executor commit selections for the winning legal offer.
    public partial class SkyStriker2026Executor
    {
        private enum TacticalActionKind
        {
            None,
            ForbiddenCrown,
            WidowAnchor,
            ForbiddenDroplet
        }

        private sealed class TacticalDecision
        {
            public TacticalActionKind Kind;
            public readonly List<ClientCard> Targets = new List<ClientCard>();
            public int Score;
            public bool TakeControl;
            public string Reason;
        }

        private TacticalDecision GetBestTacticalDecision()
        {
            var decisions = new List<TacticalDecision>();
            TacticalDecision crown = BuildForbiddenCrownDecision();
            TacticalDecision widow = BuildWidowAnchorDecision();
            TacticalDecision droplet = BuildForbiddenDropletDecision();
            if (crown != null)
                decisions.Add(crown);
            if (widow != null)
                decisions.Add(widow);
            if (droplet != null)
                decisions.Add(droplet);

            TacticalDecision best = decisions
                .Where(decision => decision.Score > 0)
                .OrderByDescending(decision => decision.Score)
                .ThenByDescending(decision => GetTacticalTiePriority(decision.Kind))
                .FirstOrDefault();
            return best ?? new TacticalDecision
            {
                Kind = TacticalActionKind.None,
                Score = 0,
                Reason = "preserve interaction"
            };
        }

        private TacticalDecision BuildForbiddenCrownDecision()
        {
            if (!CanAccessTacticalCard(CardId.ForbiddenCrown) ||
                Duel.CurrentChain.Any(card => card.Controller == 0 &&
                    card.IsCode(CardId.ForbiddenCrown)))
                return null;

            ClientCard target = GetBestTacticalMonsterTarget(false);
            if (!IsUsableForbiddenCrownTarget(target))
                return null;

            ClientCard last = Util.GetLastChainCard();
            bool answeringMonster = Duel.LastChainPlayer == 1 && last == target;
            bool stoppingLethal = target == Enemy.BattlingMonster && Bot.UnderAttack &&
                target.Attack >= Bot.LifePoints;
            bool persistentThreat = target.IsFloodgate() ||
                target.IsMonsterShouldBeDisabledBeforeItUseEffect();
            if (!answeringMonster && !stoppingLethal && !persistentThreat)
                return null;

            int score = GetObservedThreatScore(target);
            if (answeringMonster)
                score += 1700;
            if (stoppingLethal)
                score += 8000;
            // Crown also protects the target and prevents using it as material. That
            // downside matters most during our own turn when a take/removal line exists.
            score -= Duel.Player == 0 ? 1500 : 500;
            if (score < 2600)
                return null;

            var result = new TacticalDecision
            {
                Kind = TacticalActionKind.ForbiddenCrown,
                Score = score,
                Reason = stoppingLethal ? "stop lethal attack" :
                    answeringMonster ? "unanswerable monster-effect stop" : "disable persistent threat"
            };
            result.Targets.Add(target);
            return result;
        }

        private TacticalDecision BuildWidowAnchorDecision()
        {
            if (!CanAccessTacticalCard(CardId.WidowAnchor) || !EmptyMainMonsterZone() ||
                Duel.CurrentChain.Any(card => card.Controller == 0 &&
                    card.IsCode(CardId.WidowAnchor)))
                return null;

            ClientCard target = GetBestTacticalMonsterTarget(true);
            if (target == null || target.IsDisabled() || target.HasType(CardType.Normal) ||
                target.IsShouldNotBeTarget() || target.IsShouldNotBeSpellTrapTarget())
                return null;

            bool emergencyControl = HaveThreeSpellsInGrave() &&
                ShouldTakeControlForEmergencyDefense(target);
            bool takeControl = HaveThreeSpellsInGrave() &&
                (emergencyControl || CanImmediatelyUseControlledMonster(target,
                    Duel.Player == 0 && Duel.Phase < DuelPhase.Main2));
            ClientCard last = Util.GetLastChainCard();
            bool answeringMonster = Duel.LastChainPlayer == 1 && last == target;
            bool stoppingBattle = target == Enemy.BattlingMonster && Bot.UnderAttack;
            int score = GetObservedThreatScore(target) - 650;
            if (answeringMonster)
                score += 1200;
            if (takeControl)
                score += 2400;
            if (stoppingBattle && takeControl)
            {
                int projectedDamage = Math.Max(0, target.Attack -
                    (Bot.BattlingMonster == null ? 0 : Bot.BattlingMonster.GetDefensePower()));
                score += projectedDamage >= Bot.LifePoints ? 8000 : Math.Min(2500, projectedDamage);
            }
            if (Duel.CurrentChain.Count == 0 && Duel.Player == 0 && !takeControl &&
                !target.IsFloodgate() && !target.IsMonsterDangerous() &&
                !target.IsMonsterShouldBeDisabledBeforeItUseEffect())
                score -= 2200;
            if (score < 2400)
                return null;

            var result = new TacticalDecision
            {
                Kind = TacticalActionKind.WidowAnchor,
                Score = score,
                TakeControl = takeControl,
                Reason = emergencyControl ? "take control to stop lethal damage" :
                    takeControl ? "negate and convert target into tempo/material" :
                    stoppingBattle ? "negate an attacking monster effect" : "efficient single-target negate"
            };
            result.Targets.Add(target);
            return result;
        }

        private TacticalDecision BuildForbiddenDropletDecision()
        {
            if (!CanAccessTacticalCard(CardId.ForbiddenDroplet) ||
                Duel.CurrentChain.Any(card => card.Controller == 0 &&
                    card.IsCode(CardId.ForbiddenDroplet)))
                return null;

            List<ClientCard> costs = GetAvailableForbiddenDropletCosts();
            if (costs.Count == 0)
                return null;
            List<ClientCard> targets = GetForbiddenDropletTargets()
                .Take(costs.Count)
                .ToList();
            if (targets.Count == 0)
                return null;

            int remainingAttack = GetRemainingEnemyBattleAttack();
            int preventedDamage = targets
                .Where(target => target.IsAttack() && !target.Attacked)
                .Sum(target => Math.Max(0, target.Attack / 2));
            bool preventsLethal = remainingAttack >= Bot.LifePoints &&
                remainingAttack - preventedDamage < Bot.LifePoints;
            ClientCard last = Util.GetLastChainCard();
            bool stopsLiveBattleEffect = Duel.Player == 1 &&
                Duel.Phase > DuelPhase.Main1 && Duel.Phase < DuelPhase.Main2 &&
                Duel.LastChainPlayer == 1 && last != null && Duel.CurrentChain.Contains(last) &&
                targets.Contains(last);
            int score = targets.Sum(GetObservedThreatScore) - 1100;
            score -= costs.Take(targets.Count).Sum(GetDropletCostValue) / 3;
            if (targets.Count > 1)
                score += 1400 * (targets.Count - 1);
            if (preventsLethal)
                score += 8000;
            if (Duel.LastChainPlayer == 1 && last != null && targets.Contains(last))
                score += 1000;
            if (stopsLiveBattleEffect)
                score += 1800;
            if (score < 3000)
                return null;

            var result = new TacticalDecision
            {
                Kind = TacticalActionKind.ForbiddenDroplet,
                Score = score,
                Reason = preventsLethal ? "halve remaining attackers to prevent lethal" :
                    stopsLiveBattleEffect ? "negate a live Battle Phase monster effect" :
                    targets.Count > 1 ? "positive multi-negate exchange" : "best available untargeted negate"
            };
            result.Targets.AddRange(targets);
            return result;
        }

        private ClientCard GetBestTacticalMonsterTarget(bool requireTargetable)
        {
            List<ClientCard> candidates = Enemy.GetMonsters()
                .Where(card => card.IsFaceup() && card.HasType(CardType.Effect) &&
                    !card.IsDisabled() && !IsCurrentChainHandled(card) &&
                    (!requireTargetable || !card.IsShouldNotBeTarget() &&
                    !card.IsShouldNotBeSpellTrapTarget()))
                .ToList();
            if (candidates.Count == 0)
                return null;

            ClientCard last = Util.GetLastChainCard();
            if (Duel.CurrentChain.Count > 0)
            {
                if (Duel.LastChainPlayer == 1 && last != null && candidates.Contains(last) &&
                    GetObservedThreatScore(last) >= 2400)
                    return last;
                return null;
            }
            if (Bot.UnderAttack && Enemy.BattlingMonster != null &&
                candidates.Contains(Enemy.BattlingMonster))
                return Enemy.BattlingMonster;
            return candidates.OrderByDescending(GetObservedThreatScore).FirstOrDefault();
        }

        private int GetObservedThreatScore(ClientCard card)
        {
            if (card == null || IsCurrentChainHandled(card))
                return 0;
            int score = Math.Min(2200, Math.Max(0, card.GetAttackPower()) / 2);
            if (card.IsFloodgate())
                score += 5200;
            if (card.IsMonsterShouldBeDisabledBeforeItUseEffect())
                score += 3900;
            if (card.IsMonsterDangerous())
                score += 3000;
            if (card.IsMonsterInvincible())
                score += 2200;
            if (card.HasType(CardType.Link))
                score += card.LinkCount * 300;
            if (SkyStrikerMirrorObserved)
                score += GetMirrorMonsterPriority(card) * 300;
            ClientCard last = Util.GetLastChainCard();
            if (Duel.LastChainPlayer == 1 && last == card)
                score += 1600;
            if (Bot.UnderAttack && Enemy.BattlingMonster == card)
                score += card.Attack >= Bot.LifePoints ? 7000 : Math.Min(2200, card.Attack / 2);
            return score;
        }

        private bool CanAccessTacticalCard(int id)
        {
            if (Card != null && Card.IsCode(id))
                return true;
            return Duel.Player == 0
                ? Bot.HasInHandOrInSpellZone(id)
                : Bot.HasInSpellZone(id);
        }

        private int GetTacticalTiePriority(TacticalActionKind kind)
        {
            if (kind == TacticalActionKind.WidowAnchor)
                return 3;
            if (kind == TacticalActionKind.ForbiddenCrown)
                return 2;
            if (kind == TacticalActionKind.ForbiddenDroplet)
                return 1;
            return 0;
        }

        private int GetStrategicReserveValue(ClientCard card)
        {
            if (card == null)
                return 0;
            if (card.IsCode(CardId.Engage))
                return HaveThreeSpellsInGrave() ? 5000 : 4300;
            if (card.IsCode(CardId.Linkage))
                return LinkageOtk != LinkageOtkStage.None || GetLinkageOtkStarter() != null
                    ? 5000 : 3900;
            if (card.IsCode(CardId.Raye))
            {
                bool hasSecondAccess = Bot.Hand.Count(candidate => candidate.IsCode(CardId.Raye)) > 1 ||
                    Bot.HasInHandOrInSpellZone(CardId.HornetDrones) ||
                    Bot.HasInHandOrInSpellZone(CardId.Engage);
                return hasSecondAccess ? 3200 : 5000;
            }
            if (card.IsCode(CardId.Roze))
                return 3000;
            if (card.IsCode(CardId.WidowAnchor))
                return Enemy.GetMonsters().Any(candidate => candidate.IsFaceup() &&
                    candidate.HasType(CardType.Effect) && !candidate.IsDisabled()) ? 4500 : 3200;
            if (card.IsCode(CardId.ForbiddenDroplet))
                return GetForbiddenDropletTargets().Count > 1 ? 4700 : 3400;
            if (card.IsCode(CardId.Zero))
                return 5000;
            if (card.IsCode(CardId.Multirole))
                return 4700;
            if (card.IsCode(CardId.Lemnisgate))
                return 3800;
            if (card.IsCode(CardId.HornetDrones))
                return 3500;
            if (card.IsCode(CardId.TheFallenAndTheVirtuous))
                return Bot.HasInExtra(CardId.AlbionTheBrandedDragon) ? 3300 : 2200;
            if (card.IsCode(CardId.ForbiddenCrown))
                return 2400;
            return 0;
        }

        private void LogTacticalDecision(TacticalDecision decision)
        {
            Logger.DebugWriteLine("[SkyStriker2026] tactical=" + decision.Kind +
                " score=" + decision.Score + " reason=" + decision.Reason + " targets=" +
                string.Join(",", decision.Targets.Select(card =>
                    card.Name ?? card.Id.ToString()).ToArray()));
        }

        private int GetFallenAndVirtuousDestroyScore(ClientCard target)
        {
            if (!IsLiveFallenAndVirtuousDestroyTarget(target))
                return 0;
            int score;
            if (target.IsMonster())
            {
                score = GetObservedThreatScore(target);
            }
            else
            {
                score = target.IsFloodgate() ? 6500 : 1000;
                if (target.HasType(CardType.Continuous | CardType.Equip | CardType.Field | CardType.Pendulum))
                    score += 2500;
                if (SkyStrikerMirrorObserved && target.IsCode(CardId.Multirole, CardId.AreaZero))
                    score += target.IsCode(CardId.Multirole) ? 5000 : 2800;
                ClientCard last = Util.GetLastChainCard();
                if (Duel.LastChainPlayer == 1 && last == target)
                    score += 1600;
            }
            // Sending Albion while another TFTV remains converts this removal into
            // an End Phase replacement, so it carries almost no long-term cost.
            if (Bot.HasInExtra(CardId.AlbionTheBrandedDragon) &&
                Bot.HasInDeck(CardId.TheFallenAndTheVirtuous))
                score += 2200;
            else
                score -= 900;
            return score;
        }

        private ClientCard GetBestFallenAndVirtuousReviveTarget()
        {
            return Bot.Graveyard.Concat(Enemy.Graveyard)
                .Where(card => card.IsMonster() && card.IsCanRevive())
                .OrderByDescending(GetFallenAndVirtuousReviveScore)
                .FirstOrDefault();
        }

        private int GetFallenAndVirtuousReviveScore(ClientCard target)
        {
            if (target == null || !target.IsMonster() || !target.IsCanRevive())
                return 0;
            int score = Math.Max(0, target.GetAttackPower());
            if (target.HasType(CardType.Link))
                score += target.LinkCount * 600;
            if (target.IsCode(CardId.Raye, CardId.Roze))
                score += 2800;
            if (target.IsCode(CardId.Kagari, CardId.Zero, CardId.Shizuku, CardId.Hayate))
                score += 2200;
            if (target.IsFloodgate())
                score += 3000;
            if (EmptyMainMonsterZone() && target.HasType(CardType.Effect))
                score += 700;
            return score;
        }

        private IList<ClientCard> SelectFallenAndVirtuousReviveTarget(
            IList<ClientCard> cards, int min, int max)
        {
            var ordered = new List<ClientCard>();
            ClientCard intended = FindMatchingOfferedCard(cards, FallenAndVirtuousReviveTarget);
            if (intended != null)
                ordered.Add(intended);
            ordered.AddRange(cards.Where(card => !ordered.Contains(card))
                .OrderByDescending(GetFallenAndVirtuousReviveScore));
            FallenAndVirtuousReviveTarget = null;
            return Util.CheckSelectCount(ordered, cards, min, max);
        }
    }
}
