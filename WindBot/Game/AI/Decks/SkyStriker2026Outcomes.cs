using System.Collections.Generic;
using System.Linq;
using WindBot.Game;
using YGOSharp.OCGWrapper.Enums;

namespace WindBot.Game.AI.Decks
{
    public partial class SkyStriker2026Executor
    {
        // Like Witchcraft2026, forecasts are receipts, never protocol state.
        // A mismatch retires the dependent route; it is not automatic training.
        private sealed class OutcomeReceipt
        {
            public int Source, Summon;
            public string Effect;
            public List<ClientCard> Targets = new List<ClientCard>();
            public CardLocation Destination;
        }

        private readonly List<OutcomeReceipt> OutcomeReceipts = new List<OutcomeReceipt>();

        private void RecordAcceptedOutcome(int player, ClientCard card)
        {
            if (player != 0 || card == null)
                return;
            OutcomeReceipt receipt = null;
            if (card.IsCode(CardId.SharkCannon) && SharkCannonBanishOnly && SharkCannonTarget != null)
                receipt = new OutcomeReceipt { Source = card.Id, Effect = "banish-public-GY-target",
                    Targets = new List<ClientCard> { SharkCannonTarget }, Destination = CardLocation.Removed };
            else if (card.IsCode(CardId.UltimateSky) && UltimateSkyTargets.Count > 0)
                receipt = new OutcomeReceipt { Source = card.Id, Effect = "field-negation",
                    Targets = new List<ClientCard>(UltimateSkyTargets) };
            else if (card.IsCode(CardId.Linkage) && BattleSummonTarget != 0)
                receipt = new OutcomeReceipt { Source = card.Id, Effect = "battle-conversion",
                    Summon = BattleSummonTarget };
            else if (card.IsCode(CardId.Lemnisgate) && LemnisgateLinkDestination != 0)
                receipt = new OutcomeReceipt { Source = card.Id, Effect = "timed-link-conversion",
                    Summon = LemnisgateLinkDestination };
            if (receipt == null)
                return;
            OutcomeReceipts.Add(receipt);
            Logger.DebugWriteLine("[SkyStriker2026:prediction] accepted=" + receipt.Source +
                " effect=" + receipt.Effect + " targets=" + string.Join(",", receipt.Targets.Select(c => c.Id)) +
                " summon=" + receipt.Summon + "; expected only, awaiting engine");
        }

        private void ReconcileAcceptedOutcomes()
        {
            foreach (OutcomeReceipt receipt in OutcomeReceipts)
            {
                bool matched = receipt.Summon != 0 ? Bot.HasInMonstersZone(receipt.Summon) :
                    receipt.Targets.All(c => receipt.Destination == CardLocation.Removed ?
                        c.Location == CardLocation.Removed : c.IsOnField() && c.IsDisabled());
                Logger.DebugWriteLine("[SkyStriker2026:reconcile] source=" + receipt.Source +
                    " effect=" + receipt.Effect + " status=" + (matched ? "observed" : "changed-or-interrupted") +
                    " actualTargets=" + string.Join(",", receipt.Targets.Select(c =>
                        c.Id + "@" + c.Location + "/disabled=" + c.IsDisabled())) +
                    " actualOwnMonsters=" + string.Join(",", Bot.GetMonsters().Select(c => c.Id)) +
                    "; whole-chain outcome, not causal proof; retire dependent forecast");
            }
            OutcomeReceipts.Clear();
            BattlePlanKey = null;
        }
    }
}
