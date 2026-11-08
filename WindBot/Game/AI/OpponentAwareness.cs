using System;
using System.Collections.Generic;
using System.Linq;
using YGOSharp.OCGWrapper.Enums;

namespace WindBot.Game.AI
{
    [Flags]
    public enum OpponentOutcome
    {
        None = 0, Search = 1, Summon = 2, Send = 4, Destroy = 8,
        Negate = 16, Control = 32, Set = 64, Recover = 128, Draw = 256
    }

    [Flags]
    public enum OpponentResponse { None = 0, FieldMonster = 1, HandGraveMonster = 2, Spell = 4 }
    public enum OpponentWindow { Activation, Ignition, Quick, Summoned, Destroyed }
    public enum OpponentLimit { NameTurn, InstanceTurn, UnknownActivation, None }

    // Reviewed semantics, not card-name priorities. Weights below are shared outcome
    // utilities, not probabilities or a prediction of private resources/legal offers.
    public sealed class OpponentEffect
    {
        public int CardId, Offset, Group, RequiredOverlay;
        public string Name;
        public CardLocation Location;
        public OpponentWindow Window;
        public OpponentLimit Limit = OpponentLimit.NameTurn;
        public OpponentOutcome Outcome;
        public OpponentResponse RespondsTo;
        public bool RequiresFaceup, HiddenCost, OverlayCost, OnePerChain;
        public double Units = 1;

        public double Utility
        {
            get
            {
                double value = 0;
                if ((Outcome & (OpponentOutcome.Search | OpponentOutcome.Draw | OpponentOutcome.Recover | OpponentOutcome.Set)) != 0) value += 8;
                if ((Outcome & OpponentOutcome.Summon) != 0) value += 10;
                if ((Outcome & OpponentOutcome.Send) != 0) value += 8;
                if ((Outcome & (OpponentOutcome.Destroy | OpponentOutcome.Negate)) != 0) value += 12;
                if ((Outcome & OpponentOutcome.Control) != 0) value += 18;
                return value * Units;
            }
        }
    }

    public sealed class OpponentEffectUse
    {
        public int Activated, Resolved, NegatedOrDisabled, Unidentified;
        // Protocol currently combines activation-negated and effect-disabled. An
        // activation-only oath's remaining availability cannot always be established.
        public bool ConsumptionUncertain;
    }

    public sealed class OpponentThreat
    {
        public double Body, Future, PreventedResolution, DestructionBenefit, NegateNow;
        public bool Supported;
        public readonly List<string> Effects = new List<string>();
        public double Removal { get { return Body + Future + PreventedResolution - DestructionBenefit; } }
        public string Explain()
        {
            return "body=" + Body.ToString("0.0") + " future=" + Future.ToString("0.0") +
                " prevents-resolution=" + PreventedResolution.ToString("0.0") +
                " destruction-followup=" + DestructionBenefit.ToString("0.0") +
                " negate-now=" + NegateNow.ToString("0.0") + " confidence=" +
                (Supported ? "reviewed-semantics/conditional-resources" : "unknown-effect/bounded-reserve") +
                " effects=[" + string.Join(";", Effects.Take(6)) + "]";
        }
    }

    public sealed class OpponentAwareness
    {
        private sealed class Entry
        {
            public OpponentEffect Effect;
            public OpponentEffectUse Use;
            public bool Finished;
        }

        private readonly List<OpponentEffect> _effects;
        private readonly Dictionary<string, OpponentEffectUse> _usage = new Dictionary<string, OpponentEffectUse>();
        private readonly Dictionary<ClientCard, int> _instances = new Dictionary<ClientCard, int>();
        private readonly Dictionary<ChainInfo, Entry> _links = new Dictionary<ChainInfo, Entry>();
        private int _serial;

        public OpponentAwareness(IEnumerable<OpponentEffect> effects = null)
        {
            _effects = (effects ?? VerifiedEffects()).ToList();
        }

        public void NewTurn()
        {
            _usage.Clear(); _links.Clear(); _instances.Clear(); _serial = 0;
        }

        public void ResetInstance(ClientCard card)
        {
            if (card != null) _instances.Remove(card);
        }

        private string Key(OpponentEffect effect, ClientCard card, int player)
        {
            int instance = 0;
            if (effect.Limit == OpponentLimit.InstanceTurn && card != null && !_instances.TryGetValue(card, out instance))
            { instance = ++_serial; _instances[card] = instance; }
            return player + ":" + effect.CardId + ":" + effect.Group + ":" + instance;
        }

        public OpponentEffectUse Usage(OpponentEffect effect, ClientCard card, int player)
        {
            OpponentEffectUse result;
            return _usage.TryGetValue(Key(effect, card, player), out result) ? result : new OpponentEffectUse();
        }

        public OpponentEffect Identify(ChainInfo link)
        {
            List<OpponentEffect> candidates = _effects.Where(e => e.CardId == link.ActivateId &&
                (e.Location & link.ActivateLocation) != 0).ToList();
            OpponentEffect exact = candidates.FirstOrDefault(e => link.ActivateDescription == e.CardId * 16 + e.Offset);
            if (exact != null) return exact;
            // 0/-1 carry no effect identity. Infer only if the location is unambiguous.
            return (link.ActivateDescription == 0 || link.ActivateDescription == -1) && candidates.Count == 1 ? candidates[0] : null;
        }

        public void Observe(IList<ChainInfo> chain)
        {
            foreach (ChainInfo link in chain)
            {
                if (_links.ContainsKey(link)) continue;
                OpponentEffect effect = Identify(link);
                Entry entry = new Entry { Effect = effect };
                if (effect != null)
                {
                    string key = Key(effect, link.RelatedCard, link.ActivatePlayer);
                    if (!_usage.TryGetValue(key, out entry.Use)) _usage[key] = entry.Use = new OpponentEffectUse();
                    entry.Use.Activated++;
                }
                else if (link.ActivateDescription == 0 || link.ActivateDescription == -1)
                {
                    // An ambiguous notification is evidence of activity, not proof
                    // that every mode was used (nor that every mode remains unused).
                    foreach (OpponentEffect candidate in _effects.Where(e => e.CardId == link.ActivateId &&
                        (e.Location & link.ActivateLocation) != 0).GroupBy(e => e.Group).Select(g => g.First()))
                    {
                        string key = Key(candidate, link.RelatedCard, link.ActivatePlayer);
                        OpponentEffectUse uncertain;
                        if (!_usage.TryGetValue(key, out uncertain)) _usage[key] = uncertain = new OpponentEffectUse();
                        uncertain.Unidentified++;
                    }
                }
                _links.Add(link, entry);
            }
        }

        public void Solved(IList<ChainInfo> chain, int index, bool negated)
        {
            Observe(chain);
            if (index < 1 || index > chain.Count) return;
            Entry entry = _links[chain[index - 1]];
            if (entry.Finished) return;
            entry.Finished = true;
            if (entry.Use == null) return;
            if (negated) entry.Use.NegatedOrDisabled++; else entry.Use.Resolved++;
            if (negated && entry.Effect.Limit == OpponentLimit.UnknownActivation) entry.Use.ConsumptionUncertain = true;
        }

        public void EndChain() { _links.Clear(); }

        private bool Available(OpponentEffect effect, ClientCard card)
        {
            if (effect.OnePerChain && _links.Any(p => p.Key.ActivateId == effect.CardId &&
                p.Key.ActivatePlayer == card.Controller)) return false;
            OpponentEffectUse use = Usage(effect, card, card.Controller);
            return effect.Limit == OpponentLimit.None || use.Activated == 0 || use.ConsumptionUncertain;
        }

        private static bool PublicCondition(OpponentEffect effect, ClientCard card)
        {
            return (!effect.OverlayCost || card.Overlays.Count > 0) &&
                (effect.RequiredOverlay == 0 || card.Overlays.Contains(effect.RequiredOverlay));
        }

        public OpponentThreat Assess(ClientCard card, IList<ChainInfo> chain, ISet<int> negatedZeroBased,
            bool projectedDisabled, bool ownerTurn)
        {
            Observe(chain);
            OpponentThreat result = new OpponentThreat();
            if (card == null || card.Id == 0 || card.IsFacedown())
            { result.Body = 2; result.Future = 3; return result; }
            result.Body = card.IsMonster() ? 2 + Math.Min(3, Math.Max(0, card.Attack) / 1500.0) : 1;
            List<OpponentEffect> effects = _effects.Where(e => e.CardId == card.Id).ToList();
            result.Supported = effects.Count > 0;
            if (!result.Supported) { result.Future = card.IsDisabled() || projectedDisabled ? 0 : 4; return result; }
            Dictionary<int, double> future = new Dictionary<int, double>();
            foreach (OpponentEffect effect in effects)
            {
                OpponentEffectUse used = Usage(effect, card, card.Controller);
                bool live = !card.IsDisabled() && !projectedDisabled;
                string status = used.Activated > 0 ? "committed/used" : "not-observed-used";
                if (used.ConsumptionUncertain) status += "/oath-uncertain";
                if (used.Unidentified > 0) status += "/unidentified-activation-availability-uncertain";
                result.Effects.Add(effect.Name + ":" + status + "/resolved=" + used.Resolved +
                    "/negated-or-disabled=" + used.NegatedOrDisabled + (effect.HiddenCost ? "/hidden-cost-unknown" : ""));
                if (effect.Window == OpponentWindow.Destroyed && Available(effect, card) &&
                    card.Location == CardLocation.SpellZone)
                    result.DestructionBenefit += effect.Utility * .75; // target in hidden Deck is not guaranteed
                if ((effect.Location & card.Location) == 0 || !PublicCondition(effect, card)) continue;
                if (live && Available(effect, card) &&
                    (effect.Window == OpponentWindow.Quick || effect.Window == OpponentWindow.Ignition))
                {
                    double confidence = effect.HiddenCost ? .35 : .75;
                    if (used.Unidentified > 0) confidence *= .5;
                    double value = effect.Utility * confidence * (effect.Window == OpponentWindow.Ignition && !ownerTurn ? .4 : 1);
                    double prior; future.TryGetValue(effect.Group, out prior);
                    future[effect.Group] = Math.Max(prior, value); // alternate modes share one use
                }
                for (int i = 0; i < chain.Count; i++)
                {
                    ChainInfo link = chain[i];
                    if (!ReferenceEquals(link.RelatedCard, card) || Identify(link) != effect ||
                        negatedZeroBased.Contains(i) || !live) continue;
                    // A committed activation establishes its costs/conditions. Removal
                    // stops it only for a reviewed field-presence dependency.
                    if (effect.RequiresFaceup) result.PreventedResolution += effect.Utility;
                    result.NegateNow += effect.Utility;
                }
            }
            result.Future = future.Values.Sum();
            return result;
        }

        public OpponentResponse VisibleResponses(IEnumerable<ClientCard> cards)
        {
            OpponentResponse result = OpponentResponse.None;
            foreach (ClientCard card in cards.Where(c => c.IsFaceup() && !c.IsDisabled()))
                foreach (OpponentEffect effect in _effects.Where(e => e.CardId == card.Id && (e.Location & card.Location) != 0))
                    if (Available(effect, card) && PublicCondition(effect, card) && !effect.HiddenCost)
                        result |= effect.RespondsTo;
            return result;
        }

        private static OpponentEffect E(int id, int offset, int group, string name, CardLocation location,
            OpponentWindow window, OpponentOutcome outcome)
        {
            return new OpponentEffect { CardId = id, Offset = offset, Group = group, Name = name,
                Location = location, Window = window, Outcome = outcome };
        }

        public static IEnumerable<OpponentEffect> VerifiedEffects()
        {
            // Provenance: configured MDPro4 script.zip, reviewed 2026-11-08; hashes
            // and native receipts in README. No downloaded text is executed here.
            yield return E(28642461, 1, 0, "reveal two Level5 -> summon pair", CardLocation.Hand, OpponentWindow.Ignition, OpponentOutcome.Summon);
            yield return E(28642461, 2, 1, "non-Aqua K9 search", CardLocation.MonsterZone, OpponentWindow.Ignition, OpponentOutcome.Search);
            yield return E(92248362, 1, 0, "conditional hand summon", CardLocation.Hand, OpponentWindow.Quick, OpponentOutcome.Summon);
            yield return E(92248362, 2, 1, "on-summon K9 send", CardLocation.MonsterZone, OpponentWindow.Summoned, OpponentOutcome.Send);
            OpponentEffect caseSearch = E(80181649, 0, 0, "activation-only K9 search", CardLocation.SpellZone, OpponentWindow.Activation, OpponentOutcome.Search);
            caseSearch.RequiresFaceup = true; caseSearch.Limit = OpponentLimit.UnknownActivation;
            yield return caseSearch;
            yield return E(80181649, 2, 1, "destroyed in S/T zone -> set K9 Quick-Play", CardLocation.Grave | CardLocation.Removed,
                OpponentWindow.Destroyed, OpponentOutcome.Set);
            OpponentEffect hand = E(93156774, 0, 0, "reveal VS -> hand summon", CardLocation.Hand, OpponentWindow.Quick, OpponentOutcome.Summon);
            hand.OnePerChain = true; yield return hand;
            OpponentEffect control = E(93156774, 1, 1, "EARTH+DARK reveal -> lowest ATK control", CardLocation.MonsterZone, OpponentWindow.Quick, OpponentOutcome.Control);
            control.HiddenCost = true; control.OnePerChain = true; yield return control;
            OpponentEffect summon = E(93156774, 2, 1, "FIRE+DARK reveal -> non-Psychic VS summon", CardLocation.MonsterZone, OpponentWindow.Quick, OpponentOutcome.Summon);
            summon.HiddenCost = true; summon.OnePerChain = true; yield return summon;
            OpponentEffect c104 = E(49456901, 1, 1, "Number104 material -> negate field monster / optional random send", CardLocation.MonsterZone,
                OpponentWindow.Quick, OpponentOutcome.Negate | OpponentOutcome.Send);
            c104.RequiredOverlay = 2061963; c104.OverlayCost = true; c104.Limit = OpponentLimit.InstanceTurn;
            c104.RespondsTo = OpponentResponse.FieldMonster; yield return c104;
            OpponentEffect onSummon = E(49456901, 0, 0, "on-summon S/T destruction", CardLocation.MonsterZone, OpponentWindow.Summoned, OpponentOutcome.Destroy);
            onSummon.Limit = OpponentLimit.None; yield return onSummon;
            OpponentEffect jacks = E(67515699, 0, 0, "Xyz summon or hostile hand/GY monster -> destroy", CardLocation.MonsterZone,
                OpponentWindow.Quick, OpponentOutcome.Destroy);
            jacks.OverlayCost = true; jacks.RespondsTo = OpponentResponse.HandGraveMonster; yield return jacks;
        }
    }
}
