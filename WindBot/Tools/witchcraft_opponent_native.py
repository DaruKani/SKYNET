"""Real installed-card chains against one fixture-only destruction response.

Not a WindBot game or strategic oracle. The driver accepts only named, actually
offered actions in this fixture; no external game settings/scripts are modified.
"""
import struct
from witchcraft_outcome_native import main

LUA = r'''
Debug.ReloadFieldBegin(0)
Debug.SetPlayerInfo(0,8000,0,0)
Debug.SetPlayerInfo(1,8000,0,0)
local case=Debug.AddCard(80181649,0,0,LOCATION_HAND,0,POS_FACEDOWN)
local izuna=Debug.AddCard(92248362,0,0,LOCATION_HAND,0,POS_FACEDOWN)
local jokul=Debug.AddCard(28642461,0,0,LOCATION_MZONE,0,POS_FACEUP_ATTACK)
local send=Debug.AddCard(28642461,0,0,LOCATION_DECK,0,POS_FACEDOWN)
local search=Debug.AddCard(92248362,0,0,LOCATION_DECK,0,POS_FACEDOWN)
local quick=Debug.AddCard(53792930,0,0,LOCATION_DECK,0,POS_FACEDOWN)
local responder=Debug.AddCard(69247929,1,1,LOCATION_MZONE,0,POS_FACEUP_ATTACK)
Debug.AddCard(69247929,1,1,LOCATION_HAND,0,POS_FACEDOWN)
Debug.AddCard(69247929,1,1,LOCATION_HAND,0,POS_FACEDOWN)
Debug.ReloadFieldEnd()
local function check(name,ok) Debug.Message('CHECK|'..name..'|'..tostring(not not ok)) end
local e=Effect.CreateEffect(responder)
e:SetDescription(900001)
e:SetType(EFFECT_TYPE_QUICK_O)
e:SetCode(EVENT_CHAINING)
e:SetRange(LOCATION_MZONE)
e:SetCategory(CATEGORY_DESTROY)
e:SetProperty(EFFECT_FLAG_CARD_TARGET)
e:SetCondition(function(e,tp,eg,ep,ev,re)
    return ep==0 and (re:GetHandler()==case or re:GetHandler()==izuna)
        and re:GetHandler():IsOnField() and ev==1
end)
e:SetTarget(function(e,tp,eg,ep,ev,re,r,rp,chk)
    if chk==0 then return re:GetHandler():IsOnField() end
    Duel.SetTargetCard(re:GetHandler())
    Duel.SetOperationInfo(0,CATEGORY_DESTROY,re:GetHandler(),1,0,0)
end)
e:SetOperation(function(e)
    local tc=Duel.GetFirstTarget()
    if tc and tc:IsRelateToEffect(e) then Duel.Destroy(tc,REASON_EFFECT) end
end)
responder:RegisterEffect(e)
local seenCase,seenSet,seenIzuna=false,false,false
local finish=Effect.CreateEffect(responder)
finish:SetType(EFFECT_TYPE_FIELD+EFFECT_TYPE_CONTINUOUS)
finish:SetCode(EVENT_CHAIN_END)
finish:SetOperation(function()
    if case:IsLocation(LOCATION_GRAVE) and not seenCase then
        seenCase=true
        check('Case destroyed by higher chain link',case:IsReason(REASON_EFFECT))
        check('Case activation search fails without faceup source',search:IsLocation(LOCATION_DECK) and send:IsLocation(LOCATION_DECK))
    end
    if quick:IsLocation(LOCATION_SZONE) and not seenSet then
        seenSet=true
        check('destroyed Case still sets installed K9 Quick-Play',quick:IsFacedown())
    end
    if izuna:IsLocation(LOCATION_GRAVE) and not seenIzuna then
        seenIzuna=true
        check('Izuna destroyed by higher chain link',izuna:IsReason(REASON_EFFECT))
        check('destroyed Izuna still sends K9 from Deck',send:IsLocation(LOCATION_GRAVE))
    end
    if seenIzuna and search:IsLocation(LOCATION_HAND) then
        check('remaining Jokul ignition searches after Izuna chain',jokul:IsLocation(LOCATION_MZONE))
        check('complete separate chains include Case destruction followup',seenCase and seenSet)
        Debug.Message('PROBE_COMPLETE')
        Duel.Win(0,0)
    end
end)
Duel.RegisterEffect(finish,0)
'''


def respond(packet):
    """Strict classic-core prompt decoder for this fixture, not a bot policy."""
    if not packet:
        return None
    code = packet[0]
    if code == 18 and len(packet) == 7 and packet[2] == 1:
        mask = struct.unpack_from('<I', packet, 3)[0]
        for location, offset in ((4, 0), (8, 8)):
            for sequence in range(5):
                if not mask & (1 << (offset + sequence)):
                    return bytes([packet[1], location, sequence])
    if code == 11:  # SELECT_IDLECMD: five 7-byte lists, then 11-byte activations
        pos, groups = 2, []
        for _ in range(5):
            count = packet[pos]; pos += 1
            groups.append([struct.unpack_from('<I', packet, pos + 7*i)[0] for i in range(count)])
            pos += 7 * count
        count = packet[pos]; pos += 1
        offered = [(struct.unpack_from('<I', packet, pos + 11*i)[0],
                    struct.unpack_from('<I', packet, pos + 11*i + 7)[0]) for i in range(count)]
        if pos + 11 * count + 3 != len(packet):
            return None
        for i, (card, desc) in enumerate(offered):
            if card == 80181649:
                return (i << 16) | 5
        if 92248362 in groups[0]:
            return groups[0].index(92248362) << 16
        for i, (card, desc) in enumerate(offered):
            if card == 28642461 and desc == 28642461 * 16 + 2:
                return (i << 16) | 5
    if code == 16:  # SELECT_CHAIN, current host's per-offer forced flag
        count = packet[2]
        if len(packet) != 12 + count * 14:
            return None
        for i in range(count):
            card = struct.unpack_from('<I', packet, 14 + i*14)[0]
            if card in (69247929, 80181649, 92248362):
                return i
        return -1
    if code == 13 and len(packet) == 6:  # optional yes/no, fixture has no unrelated offers
        return 1
    if code == 14 and len(packet) == 3 + packet[2]*4:
        options = [struct.unpack_from('<I', packet, 3 + 4*i)[0] for i in range(packet[2])]
        if 92248362 * 16 in options:  # installed Izuna no-tribute summon procedure
            return options.index(92248362 * 16)
    if code == 12 and len(packet) == 14:  # effect yes/no: verify source identity
        return 1 if struct.unpack_from('<I', packet, 2)[0] in (80181649, 92248362) else 0
    if code == 15:  # SELECT_CARD, only choose named fixture targets
        count = packet[5]
        if len(packet) != 6 + count*8:
            return None
        ids = [struct.unpack_from('<I', packet, 6 + 8*i)[0] for i in range(count)]
        for wanted in (53792930, 28642461, 92248362):
            if wanted in ids and packet[3] <= 1 <= packet[4]:
                return bytes([1, ids.index(wanted)])
    return None


if __name__ == '__main__':
    raise SystemExit(main(LUA, 7, respond))
