using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Skynet.Game.AI.Shadow
{
    [DataContract]
    public sealed class ShadowRecord
    {
        [DataMember(Name = "schema")]
        public string Schema = "skynet-public-observation-v1";
        [DataMember(Name = "kind")]
        public string Kind;
        [DataMember(Name = "duelId")]
        public string DuelId;
        [DataMember(Name = "sequence")]
        public int Sequence;
        [DataMember(Name = "agent")]
        public string Agent;
        [DataMember(Name = "prompt", EmitDefaultValue = false)]
        public string Prompt;
        [DataMember(Name = "observation", EmitDefaultValue = false)]
        public ShadowObservation Observation;
        [DataMember(Name = "offers", EmitDefaultValue = false)]
        public List<ShadowOffer> Offers;
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
    public sealed class ShadowObservation
    {
        [DataMember(Name = "global")]
        public ShadowGlobal Global;
        [DataMember(Name = "cards")]
        public List<ShadowCard> Cards = new List<ShadowCard>();
        [DataMember(Name = "counts")]
        public List<ShadowCounts> Counts = new List<ShadowCounts>();
        [DataMember(Name = "counterStateAvailable")]
        public bool CounterStateAvailable;
    }

    [DataContract]
    public sealed class ShadowGlobal
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
    public sealed class ShadowCounts
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
    public sealed class ShadowCard
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
    public sealed class ShadowCardReference
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
    public sealed class ShadowOffer
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
        public ShadowCardReference Card;
        [DataMember(Name = "description")]
        public int Description;
        [DataMember(Name = "isMandatory")]
        public bool IsMandatory;
        [DataMember(Name = "fieldOnly")]
        public bool FieldOnly;
    }
}
