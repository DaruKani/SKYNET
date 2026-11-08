using System;
using System.Collections.Generic;
using System.Linq;
using WindBot.Game;
using YGOSharp.OCGWrapper.Enums;

namespace WindBot.Game.AI.Decks
{
    public partial class SkyStriker2026Executor
    {
        private readonly List<ClientCard> ReactionOffers = new List<ClientCard>();
        private string ReactionStateKey;
        private ClientCard LemnisgateLinkMaterial;
        private int LemnisgateLinkDestination;

        // This is an observation of THIS response window, not a list of cards
        // that happen to exist in hand/backrow. No selections are committed here.
        public override void OnSelectChain(IList<ClientCard> cards)
        {
            ReactionOffers.Clear();
            ReactionOffers.AddRange(cards);
            ReactionStateKey = GetDecisionObservationKey();
            Logger.DebugWriteLine("[SkyStriker2026:offers] turn=" + Duel.Turn +
                " phase=" + Duel.Phase + " chain=" + Duel.CurrentChain.Count +
                " cards=" + string.Join(",", cards.Select(c => c.Id + "@" + c.Location)));
            base.OnSelectChain(cards);
        }

        private string GetDecisionObservationKey()
        {
            // Include public combat/negation changes, not just zone counts. Own
            // deck identities are NEVER read from the hidden placeholder list.
            return Duel.Turn + "/" + Duel.Player + "/" + Duel.Phase + "/" +
                Bot.LifePoints + "/" + Enemy.LifePoints + "/" +
                string.Join(";", Bot.Hand.Concat(Bot.Graveyard).Concat(Bot.GetMonsters())
                    .Concat(Bot.GetSpells()).Concat(Bot.ExtraDeck)
                    .Concat(Enemy.GetMonsters()).Concat(Enemy.GetSpells())
                    .Concat(Enemy.Graveyard).Select(c => c.Controller + ":" + c.Id +
                        ":" + c.Location + ":" + c.Sequence + ":" + c.Position +
                        ":" + c.Attack + ":" + c.Defense + ":" + c.Disabled + ":" + c.Attacked)) +
                "/" + Bot.Deck.Count + "/" + Enemy.Hand.Count + "/" +
                string.Join(";", Duel.CurrentChainInfo.Select(c => c.ActivatePlayer + ":" +
                    c.ActivateId + ":" + c.ActivateDescription + ":" + c.ActivateLocation +
                    ":" + string.Join(",", c.Targets.Select(t => t.Controller + ":" + t.Id + ":" + t.Location))));
        }

        private sealed class EffectImpact
        {
            public bool Known;
            public int PreventedValue;
            public string Reason;
        }

        private EffectImpact AssessLiveEffect(ChainInfo chain)
        {
            var result = new EffectImpact { Reason = "unmodeled effect; conservative fallback" };
            if (chain == null || chain.ActivatePlayer != 1)
                return result;
            int offset = chain.ActivateDescription - chain.ActivateId * 16;
            ClientCard source = chain.RelatedCard;
            if (source != null && (IsCurrentChainHandled(source) ||
                chain.HasLocation(CardLocation.MonsterZone) && source.IsDisabled()))
                return new EffectImpact { Known = true, Reason = "already negated/reserved" };

            // Verified against the explicitly selected script.zip. An effect
            // offset identifies a mode, not a general danger rating for its card.
            if (chain.IsActivateCode(95503687) && chain.HasLocation(CardLocation.MonsterZone))
            {
                result.Known = true;
                ClientCard revived = chain.Targets.FirstOrDefault(c => c.Controller == 1 &&
                    c.Location == CardLocation.Grave && c.IsMonster());
                result.PreventedValue = offset == 0 && revived != null
                    ? 2300 + Math.Min(2000, Math.Max(0, revived.Attack)) : 0;
                result.Reason = offset == 0 ? "stop targeted GY extension after discard" :
                    "End Phase mill is not the revival effect";
            }
            else if (chain.IsActivateCode(4779823, 22624373) &&
                chain.HasLocation(CardLocation.MonsterZone))
            {
                result.Known = true;
                result.PreventedValue = offset == 0 ? chain.Targets
                    .Where(c => c.Controller == 0 && c.IsOnField())
                    .Sum(c => 1500 + Math.Max(1000, GetFieldResourceValue(c))) : 0;
                result.Reason = offset == 0 ? "prevent removal of observed own target" :
                    "not the field removal mode";
            }
            else if (chain.IsActivateCode(55623480) && chain.HasLocation(CardLocation.Grave))
            {
                result.Known = true;
                result.PreventedValue = Bot.GetMonsterCount() > 0 ? 4500 : 3000;
                result.Reason = "prevent paid Snow revival and face-down interruption";
            }
            else if (chain.IsActivateCode(57774843) && chain.HasLocation(CardLocation.MonsterZone))
            {
                result.Known = true;
                result.PreventedValue = offset == 0 ? Bot.GetMonsters().Concat(Bot.GetSpells())
                    .Sum(c => Math.Max(1000, GetFieldResourceValue(c))) : 0;
                result.Reason = offset == 0 ? "prevent Judgment Dragon field wipe" : "End Phase mill";
            }
            return result;
        }

        private int GetNegationOutcomeValue(ClientCard target)
        {
            ChainInfo chain = Duel.CurrentChainInfo.LastOrDefault();
            if (chain != null && chain.RelatedCard == target)
            {
                EffectImpact impact = AssessLiveEffect(chain);
                if (impact.Known)
                    return impact.PreventedValue;
            }
            return GetObservedThreatScore(target);
        }

        private bool IsThreatenedByPendingRemoval(ClientCard resource)
        {
            if (resource == null || resource.Controller != 0 || !resource.IsOnField())
                return false;
            foreach (ChainInfo chain in Duel.CurrentChainInfo)
            {
                if (chain.ActivatePlayer != 1 || IsCurrentChainHandled(chain.RelatedCard))
                    continue;
                int index = Duel.CurrentChainInfo.IndexOf(chain) + 1;
                if (Duel.NegatedChainIndexList.Contains(index))
                    continue;
                if (chain.IsActivateCode(CardId.HarpiesFeatherDuster) && resource.IsSpell())
                    return true;
                if (chain.IsActivateCode(57774843) &&
                    chain.ActivateDescription == Util.GetStringId(57774843, 0))
                    return true;
                if (chain.IsActivateCode(CardId.MysticalSpaceTyphoon, CardId.TwinTwisters,
                    4779823, 22624373) && chain.Targets.Contains(resource))
                    return true;
            }
            return false;
        }

        private int GetPublicGraveDisruptionValue(ClientCard target)
        {
            if (target == null || target.Controller != 1 || target.Location != CardLocation.Grave)
                return 0;
            // Banish only: do not clog MZ or grant a banish trigger (e.g. Giant Rex).
            if (target.IsCode(55623480)) return 4500; // Snow
            if (target.IsCode(67441435)) return 2500; // Glow-Up Bulb
            if (target.IsCode(CardId.Raye)) return 3500;
            if (target.IsCode(CardId.Roze)) return 1800;
            return 0;
        }

        private bool TrySalvageSharkCannon(IList<ClientCard> targets)
        {
            if (!EmptyMainMonsterZone() || !IsThreatenedByPendingRemoval(Card))
                return false;
            ClientCard target = targets.Where(c => !Duel.CurrentChainInfo.Any(ch =>
                    ch.ActivatePlayer == 0 && ch.IsActivateCode(CardId.SharkCannon) && ch.Targets.Contains(c)))
                .OrderByDescending(GetPublicGraveDisruptionValue).FirstOrDefault();
            if (GetPublicGraveDisruptionValue(target) <= 0)
                return false;
            SharkCannonTarget = target;
            SharkCannonBanishOnly = true;
            AI.SelectCard(target);
            Logger.DebugWriteLine("[SkyStriker2026:decision] Shark Cannon: banish public GY engine " +
                target.Id + "; card threatened by pending removal; reserve value=0; keep MZ empty");
            return true;
        }

        private bool TryPlanLemnisgateGraveConversion()
        {
            LemnisgateLinkMaterial = null;
            LemnisgateLinkDestination = 0;
            bool main = Duel.Player == 0 &&
                (Duel.Phase == DuelPhase.Main1 || Duel.Phase == DuelPhase.Main2);
            string reason = main ? "PASS: normal Link is available after this trigger window" :
                "PASS: no concrete timing gain exceeds loss of grave resource";
            // A one-material conversion must clear MZ, not strand another body.
            List<ClientCard> materials = Bot.GetMonsters().Where(c => c.IsFaceup() &&
                IsSkyStrikerAce(c) && !c.IsCode(CardId.Zero) &&
                Bot.GetMonstersInMainZone().All(m => m == c))
                .OrderBy(GetLinkMaterialValue).ToList();
            bool battle = Duel.Phase > DuelPhase.Main1 && Duel.Phase < DuelPhase.Main2;
            if (!main)
            {
                foreach (ClientCard material in materials)
                {
                    if (Duel.Player == 1 && battle && !KainaSummoned && Bot.HasInExtra(CardId.Kaina) &&
                        !material.HasAttribute(CardAttribute.Earth))
                    {
                        ClientCard attacker = Enemy.GetMonsters().Where(c => c.IsFaceup() && c.IsAttack() &&
                            !c.Attacked && c.Attack > material.GetDefensePower() && !c.IsShouldNotBeTarget() &&
                            !c.IsShouldNotBeMonsterTarget()).OrderByDescending(c => c.Attack).FirstOrDefault();
                        if (attacker != null)
                        {
                            LemnisgateLinkMaterial = material;
                            LemnisgateLinkDestination = CardId.Kaina;
                            reason = "Kaina stops a remaining attacker that beats this material; normal Link unavailable";
                            break;
                        }
                    }
                    if (Duel.Player == 0 && battle && material.Attacked && !HasUnmodeledBattleDefense() &&
                        Enemy.GetMonsterCount() == 0 && !KagariSummoned && Bot.HasInExtra(CardId.Kagari) &&
                        !material.HasAttribute(CardAttribute.Fire))
                    {
                        int damage = 1500 + GetSpellCountInGrave() * 100;
                        bool linkageFollowUp = Bot.HasInGraveyard(CardId.Linkage) &&
                            !ZeroSummoned && !ZeroEffectSpent && Bot.HasInExtra(CardId.Zero) &&
                            (Bot.HasInDeck(CardId.Raye) || Bot.HasInGraveyard(CardId.Raye)) &&
                            (Bot.HasInDeck(CardId.Roze) || Bot.HasInGraveyard(CardId.Roze));
                        if (damage >= Enemy.LifePoints || linkageFollowUp && damage + 4900 >= Enemy.LifePoints)
                        {
                            LemnisgateLinkMaterial = material;
                            LemnisgateLinkDestination = CardId.Kagari;
                            reason = "spent attacker converts to a concrete remaining-damage line this Battle Phase";
                            break;
                        }
                    }
                }
            }
            Logger.DebugWriteLine("[SkyStriker2026:decision] Lemnisgate-GY target=" +
                LemnisgateLinkDestination + " material=" + (LemnisgateLinkMaterial == null ? 0 :
                LemnisgateLinkMaterial.Id) + " reason=" + reason);
            return LemnisgateLinkDestination != 0;
        }
    }
}
