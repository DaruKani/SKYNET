using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using YGOSharp.OCGWrapper.Enums;

namespace Skynet.Game.AI.Strategy
{
    public sealed class DecisionTraceOffer
    {
        public LegalAction Action { get; private set; }
        public ActionEvaluation Evaluation { get; private set; }
        public double EffectiveScore { get; private set; }
        public string ExclusionReason { get; private set; }

        public DecisionTraceOffer(LegalAction action, ActionEvaluation evaluation, double effectiveScore,
            string exclusionReason = null)
        {
            Action = action;
            Evaluation = evaluation;
            EffectiveScore = effectiveScore;
            ExclusionReason = exclusionReason;
        }
    }

    /// <summary>
    /// Opt-in explanation of a choice already made. Records are transient; only
    /// counters survive the call. This never evaluates a card, queues a response,
    /// samples RNG or reads identities from opponent hidden zones.
    /// </summary>
    public sealed class DecisionTrace
    {
        public const int MaximumDecisions = 128;
        private const int MaximumAlternatives = 4;
        private const int MaximumExclusions = 4;
        private const int MaximumLineLength = 480;
        private readonly Duel _duel;
        private int _decisions;
        private string _duelId;

        public bool CanWrite { get { return _decisions < MaximumDecisions; } }

        public DecisionTrace(Duel duel)
        {
            if (duel == null)
                throw new ArgumentNullException("duel");
            _duel = duel;
            Reset();
        }

        public void Reset()
        {
            _decisions = 0;
            _duelId = Guid.NewGuid().ToString("N").Substring(0, 8);
        }

        public void Write(IList<DecisionTraceOffer> offers, LegalAction selected, bool sampled, string modelVersion)
        {
            if (!CanWrite || offers == null)
                return;

            ++_decisions;
            string prefix = "[Explain " + _duelId + ":" + _decisions.ToString(CultureInfo.InvariantCulture) + "] ";
            int admitted = offers.Count(offer => offer != null && offer.Evaluation != null);
            int selectedOffer = -1;
            for (int i = 0; i < offers.Count; ++i)
            {
                if (selected != null && offers[i] != null && ReferenceEquals(offers[i].Action, selected))
                {
                    selectedOffer = i;
                    break;
                }
            }

            WriteLine(prefix + "turn=" + _duel.Turn.ToString(CultureInfo.InvariantCulture)
                + " phase=" + _duel.Phase + " policy=" + SkynetLearningSession.PolicyRevision
                + " model=" + Clean(modelVersion, 64) + " mode="
                + (sampled ? "training sample" : "highest score")
                + " offers=" + offers.Count.ToString(CultureInfo.InvariantCulture)
                + " admitted=" + admitted.ToString(CultureInfo.InvariantCulture)
                + " excluded=" + (offers.Count - admitted).ToString(CultureInfo.InvariantCulture));
            if (selectedOffer >= 0)
                WriteLine(prefix + "CHOSEN " + Describe(offers[selectedOffer], selectedOffer));
            else
                WriteLine(prefix + "CHOSEN none: no admitted offer; caller retains its protocol fallback.");

            // Sort only for presentation. Offer numbers retain their zero-based
            // input positions, distinct from the server's per-action wire index.
            var alternatives = Enumerable.Range(0, offers.Count)
                .Where(i => i != selectedOffer && offers[i] != null && offers[i].Evaluation != null)
                .OrderByDescending(i => offers[i].EffectiveScore).Take(MaximumAlternatives);
            foreach (int i in alternatives)
                WriteLine(prefix + "OTHER " + Describe(offers[i], i));
            int exclusions = 0;
            for (int i = 0; i < offers.Count && exclusions < MaximumExclusions; ++i)
            {
                if (offers[i] != null && offers[i].Evaluation != null)
                    continue;
                WriteLine(prefix + "EXCLUDED " + Describe(offers[i], i));
                ++exclusions;
            }
            if (admitted - (selectedOffer >= 0 ? 1 : 0) > MaximumAlternatives
                || offers.Count - admitted > MaximumExclusions)
                WriteLine(prefix + "Showing at most 4 other admitted and 4 excluded offers; totals are above.");
            if (!CanWrite)
                WriteLine(prefix + "Decision explanation limit reached (128); further choices remain silent until the next duel.");
        }

        private static string Describe(DecisionTraceOffer offer, int index)
        {
            string label = "offer[" + index.ToString(CultureInfo.InvariantCulture) + "] ";
            if (offer == null || offer.Action == null)
                return label + "missing action; " + Clean(offer == null ? "invalid offer" : offer.ExclusionReason, 180);
            LegalAction action = offer.Action;
            label += action.Type + " wire=" + action.Index.ToString(CultureInfo.InvariantCulture);
            if (action.Card != null)
            {
                ClientCard card = action.Card;
                bool identityVisible = card.Controller == 0 || IsPublicOpponentCard(card);
                label += " " + (card.Controller == 0 ? "own" : card.Controller == 1 ? "opponent" : "unknown controller")
                    + " " + card.Location + "[" + card.Sequence.ToString(CultureInfo.InvariantCulture) + "]";
                if (identityVisible && card.Id > 0)
                {
                    label += " " + Clean(card.Name ?? "card", 64) + "#" + card.Id.ToString(CultureInfo.InvariantCulture);
                    if (action.Description >= 0)
                        label += " description=" + action.Description.ToString(CultureInfo.InvariantCulture);
                }
                else
                    label += " hidden/unknown card";
            }
            else if (action.Type == ExecutorType.Activate && action.Index < 0)
                label += " decline chain";
            if (action.IsMandatory)
                label += " mandatory";
            if (offer.Evaluation == null)
                return label + "; " + Clean(offer.ExclusionReason ?? "excluded by policy", 180);
            return label + "; base=" + Number(offer.Evaluation.Score)
                + " residual=" + Number(offer.EffectiveScore - offer.Evaluation.Score)
                + " total=" + Number(offer.EffectiveScore)
                + "; " + Clean(offer.Evaluation.Reason ?? "admitted by shared policy", 180);
        }

        private static bool IsPublicOpponentCard(ClientCard card)
        {
            if (card.Controller != 1)
                return false;
            return card.Location == CardLocation.Grave
                || (card.IsFaceup() && (card.Location == CardLocation.MonsterZone
                    || card.Location == CardLocation.SpellZone || card.Location == CardLocation.Removed));
        }

        private static string Number(double value)
        {
            return value.ToString("G6", CultureInfo.InvariantCulture);
        }

        private static string Clean(string value, int limit)
        {
            if (string.IsNullOrEmpty(value))
                return "unspecified";
            var clean = new StringBuilder(Math.Min(value.Length, limit));
            int count = Math.Min(value.Length, limit);
            for (int i = 0; i < count; ++i)
            {
                char character = value[i];
                clean.Append(char.IsControl(character) || char.IsWhiteSpace(character)
                    || char.GetUnicodeCategory(character) == UnicodeCategory.Format
                    ? ' ' : character);
            }
            if (value.Length > limit)
                clean.Append("...");
            return clean.ToString();
        }

        private static void WriteLine(string line)
        {
            Logger.WriteLine(line.Length <= MaximumLineLength ? line : line.Substring(0, MaximumLineLength - 3) + "...");
        }
    }
}
