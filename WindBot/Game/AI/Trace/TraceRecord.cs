using System.Collections.Generic;
using System.Runtime.Serialization;

namespace WindBot.Game.AI.Trace
{
    [DataContract]
    public sealed class TraceRecord
    {
        [DataMember(Name = "schema")]
        public string Schema = "windbot-public-observation-v1";
        [DataMember(Name = "kind")]
        public string Kind;
        [DataMember(Name = "duelId")]
        public string DuelId;
        [DataMember(Name = "sequence")]
        public int Sequence;
        [DataMember(Name = "recordedAt")]
        public string RecordedAt;
        [DataMember(Name = "roomName", EmitDefaultValue = false)]
        public string RoomName;
        [DataMember(Name = "matchOrder", EmitDefaultValue = false)]
        public int? MatchOrder;
        [DataMember(Name = "duelNumber")]
        public int DuelNumber;
        [DataMember(Name = "agent")]
        public string Agent;
        [DataMember(Name = "prompt", EmitDefaultValue = false)]
        public string Prompt;
        [DataMember(Name = "observation", EmitDefaultValue = false)]
        public TraceObservation Observation;
        [DataMember(Name = "offers", EmitDefaultValue = false)]
        public List<TraceOffer> Offers;
        [DataMember(Name = "selection", EmitDefaultValue = false)]
        public TraceSelection Selection;
        [DataMember(Name = "selectedCandidateIds", EmitDefaultValue = false)]
        public int[] SelectedCandidateIds;
        [DataMember(Name = "responseBytes", EmitDefaultValue = false)]
        public int[] ResponseBytes;
        [DataMember(Name = "responseSource", EmitDefaultValue = false)]
        public string ResponseSource;
        [DataMember(Name = "decisionSource", EmitDefaultValue = false)]
        public string DecisionSource;
        [DataMember(Name = "cancelled", EmitDefaultValue = false)]
        public bool Cancelled;
        [DataMember(Name = "selectedOfferId")]
        public int? SelectedOfferId;
        [DataMember(Name = "selectedWireResponse")]
        public int? SelectedWireResponse;
        [DataMember(Name = "outcome")]
        public int? Outcome;
        [DataMember(Name = "completed")]
        public bool Completed;
        [DataMember(Name = "reason", EmitDefaultValue = false)]
        public string Reason;
        [DataMember(Name = "unsupported", EmitDefaultValue = false)]
        public List<string> Unsupported;
        [DataMember(Name = "coverage", EmitDefaultValue = false)]
        public string[] Coverage;
        [DataMember(Name = "maxRows", EmitDefaultValue = false)]
        public int MaxRows;
        [DataMember(Name = "maxBytes", EmitDefaultValue = false)]
        public int MaxBytes;
        [DataMember(Name = "maxRowBytes", EmitDefaultValue = false)]
        public int MaxRowBytes;
    }

    [DataContract]
    public sealed class TraceSelection
    {
        [DataMember(Name = "min")]
        public int Min;
        [DataMember(Name = "max")]
        public int Max;
        [DataMember(Name = "cancelable")]
        public bool Cancelable;
        [DataMember(Name = "hint")]
        public int Hint;
        [DataMember(Name = "candidates")]
        public List<TraceSelectionCandidate> Candidates;
        [DataMember(Name = "solvingChainIndex")]
        public int SolvingChainIndex;
        [DataMember(Name = "chain")]
        public List<TraceChainLink> Chain = new List<TraceChainLink>();
    }

    [DataContract]
    public sealed class TraceSelectionCandidate
    {
        [DataMember(Name = "id")]
        public int Id;
        [DataMember(Name = "known")]
        public bool Known;
        [DataMember(Name = "code")]
        public int Code;
        // This is the prompt's wire reference. Deck sequences can be virtual;
        // they must not be treated as persistent field slots or card identities.
        [DataMember(Name = "card")]
        public TraceCardReference Card;
        [DataMember(Name = "position")]
        public int Position;
    }

    [DataContract]
    public sealed class TraceChainLink
    {
        [DataMember(Name = "index")]
        public int Index;
        [DataMember(Name = "activatePlayer")]
        public int ActivatePlayer;
        [DataMember(Name = "code")]
        public int Code;
        [DataMember(Name = "description")]
        public int Description;
        [DataMember(Name = "activation")]
        public TraceCardReference Activation;
    }

    [DataContract]
    public sealed class TraceObservation
    {
        [DataMember(Name = "global")]
        public TraceGlobal Global;
        [DataMember(Name = "cards")]
        public List<TraceCard> Cards = new List<TraceCard>();
        [DataMember(Name = "counts")]
        public List<TraceCounts> Counts = new List<TraceCounts>();
        [DataMember(Name = "counterStateAvailable")]
        public bool CounterStateAvailable;
    }

    [DataContract]
    public sealed class TraceGlobal
    {
        [DataMember(Name = "myLp")]
        public int MyLp;
        [DataMember(Name = "opLp")]
        public int OpLp;
        [DataMember(Name = "turn")]
        public int Turn;
        [DataMember(Name = "phase")]
        public int Phase;
        [DataMember(Name = "isFirst")]
        public bool IsFirst;
        [DataMember(Name = "isMyTurn")]
        public bool IsMyTurn;
    }

    [DataContract]
    public sealed class TraceCounts
    {
        [DataMember(Name = "deck")]
        public int Deck;
        [DataMember(Name = "hand")]
        public int Hand;
        [DataMember(Name = "extra")]
        public int Extra;
        [DataMember(Name = "grave")]
        public int Grave;
        [DataMember(Name = "banished")]
        public int Banished;
        [DataMember(Name = "monster")]
        public int Monster;
        [DataMember(Name = "spell")]
        public int Spell;
    }

    [DataContract]
    public sealed class TraceCard
    {
        [DataMember(Name = "known")]
        public bool Known;
        [DataMember(Name = "code")]
        public int Code;
        [DataMember(Name = "controller")]
        public int Controller;
        [DataMember(Name = "location")]
        public int Location;
        [DataMember(Name = "sequence")]
        public int Sequence;
        [DataMember(Name = "position")]
        public int Position;
        [DataMember(Name = "overlaySequence")]
        public int OverlaySequence = -1;
        [DataMember(Name = "attribute")]
        public int Attribute;
        [DataMember(Name = "race")]
        public int Race;
        [DataMember(Name = "level")]
        public int Level;
        [DataMember(Name = "rank")]
        public int Rank;
        [DataMember(Name = "linkMarker")]
        public int LinkMarker;
        [DataMember(Name = "negated")]
        public bool Negated;
        [DataMember(Name = "attack")]
        public int Attack;
        [DataMember(Name = "defense")]
        public int Defense;
        [DataMember(Name = "type")]
        public int Type;
        [DataMember(Name = "counter")]
        public int Counter;
        [DataMember(Name = "overlayCount")]
        public int OverlayCount;
    }

    [DataContract]
    public sealed class TraceCardReference
    {
        [DataMember(Name = "controller")]
        public int Controller;
        [DataMember(Name = "location")]
        public int Location;
        [DataMember(Name = "sequence")]
        public int Sequence;
        [DataMember(Name = "overlaySequence")]
        public int OverlaySequence = -1;
    }

    [DataContract]
    public sealed class TraceOffer
    {
        [DataMember(Name = "id")]
        public int Id;
        [DataMember(Name = "type")]
        public string Type;
        [DataMember(Name = "actionCode")]
        public int ActionCode;
        [DataMember(Name = "index")]
        public int Index;
        [DataMember(Name = "wireResponse")]
        public int WireResponse;
        [DataMember(Name = "card")]
        public TraceCardReference Card;
        [DataMember(Name = "description")]
        public int Description;
        [DataMember(Name = "isMandatory")]
        public bool IsMandatory;
        [DataMember(Name = "fieldOnly")]
        public bool FieldOnly;
    }
}
