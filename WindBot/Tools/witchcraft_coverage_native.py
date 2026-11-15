"""Installed-card chain fixture for the 232940 interruption and missing modes.

Not a full WindBot match: deterministic offered-action driver, real installed
Lacrima/Ash/Bystreet/Celebration effects. Extra duplicate is a count-limit probe.
"""
import struct
import sys
from witchcraft_outcome_native import main

LUA = r'''
Debug.ReloadFieldBegin(0)
Debug.SetPlayerInfo(0,8000,0,0)
Debug.SetPlayerInfo(1,8000,0,0)
local seed=Debug.AddCard(59106048,0,0,LOCATION_MZONE,0,POS_FACEUP_ATTACK)
local edel=Debug.AddCard(58139997,0,0,LOCATION_HAND,0,POS_FACEDOWN)
local lacrima=Debug.AddCard(73664385,0,0,LOCATION_HAND,0,POS_FACEDOWN)
local by=Debug.AddCard(83289866,0,0,LOCATION_HAND,0,POS_FACEDOWN)
local cel=Debug.AddCard(6958567,0,0,LOCATION_HAND,0,POS_FACEDOWN)
local duplicate=Debug.AddCard(6958567,0,0,LOCATION_HAND,0,POS_FACEDOWN)
local genni=Debug.AddCard(64756282,0,0,LOCATION_DECK,0,POS_FACEDOWN)
local ash=Debug.AddCard(14558127,1,1,LOCATION_HAND,0,POS_FACEDOWN)
local box=Debug.AddCard(2606417,1,1,LOCATION_SZONE,0,POS_FACEUP)
local other=Debug.AddCard(69247929,1,1,LOCATION_MZONE,0,POS_FACEUP_ATTACK)
Debug.ReloadFieldEnd()
local function check(name,ok) Debug.Message('CHECK|'..name..'|'..tostring(not not ok)) end
local stage=0
local finished=Effect.CreateEffect(seed)
finished:SetType(EFFECT_TYPE_FIELD+EFFECT_TYPE_CONTINUOUS)
finished:SetCode(EVENT_CHAIN_END)
finished:SetOperation(function()
    if stage==0 and lacrima:IsLocation(LOCATION_GRAVE) then
        stage=1
        check('actual Ash negates Lacrima send',ash:IsLocation(LOCATION_GRAVE) and genni:IsLocation(LOCATION_DECK))
        check('Lacrima send flag remains spent after Ash',Duel.GetFlagEffect(0,73664385)>0)
        check('Bystreet activation offered from hand after failed starter',by:GetActivateEffect():IsActivatable(0))
        check('Celebration destruction legal without Fusion materials',cel:GetActivateEffect():IsActivatable(0))
        check('Reversal Box generated counters through real Standby effect',box:GetCounter(0x76)>0)
        box:AddCounter(0x76,2)
        local be=Effect.CreateEffect(box)
        be:SetType(EFFECT_TYPE_SINGLE)
        be:SetCode(EFFECT_UPDATE_ATTACK)
        be:SetValue(0)
        box:RegisterEffect(be)
        check('Box cost uses public counter',c2606417.atkcost(be,1,nil,0,0,nil,0,0,0))
    elseif stage==1 and by:IsLocation(LOCATION_SZONE) then
        stage=2
        check('activated Bystreet remains faceup continuous spell',by:IsFaceup())
    elseif stage==2 and box:IsLocation(LOCATION_GRAVE) then
        stage=3
        check('Celebration destroys Box while Bystreet preserves Seed',seed:IsLocation(LOCATION_MZONE) and cel:IsLocation(LOCATION_GRAVE))
        check('Celebration activation consumes shared count for second copy',not duplicate:GetActivateEffect():CheckCountLimit(0))
        Duel.Destroy(seed,REASON_EFFECT)
        check('Bystreet shield does not prevent second destruction this turn',seed:IsLocation(LOCATION_GRAVE))
        local be=Effect.CreateEffect(box)
        local se=Effect.CreateEffect(seed)
        se:SetType(EFFECT_TYPE_IGNITION)
        seed:RegisterEffect(se)
        check('Box cannot respond to source no longer on field',not c2606417.atkcon2(be,1,nil,0,0,se,0,0))
        Debug.Message('PROBE_COMPLETE')
        Duel.Win(0,0)
    end
end)
Duel.RegisterEffect(finished,0)
'''

def respond(packet):
    code = packet[0]
    if code == 18 and len(packet) == 7 and packet[2] == 1:
        mask = struct.unpack_from('<I', packet, 3)[0]
        for location, offset in ((4, 0), (8, 8)):
            for sequence in range(5):
                if not mask & (1 << (offset + sequence)):
                    return bytes([packet[1], location, sequence])
    if code == 11:
        pos = 2
        for _ in range(5):
            count = packet[pos]
            pos += 1 + count * 7
        count = packet[pos]; pos += 1
        if pos + count * 11 + 3 != len(packet):
            return None
        offers = [struct.unpack_from('<I', packet, pos + 11 * i)[0] for i in range(count)]
        for wanted in (73664385, 83289866, 6958567):
            if wanted in offers:
                return (offers.index(wanted) << 16) | 5
    if code == 16 and len(packet) == 12 + packet[2] * 14:
        for i in range(packet[2]):
            card = struct.unpack_from('<I', packet, 14 + i * 14)[0]
            if card == 14558127:
                return i
            if card == 2606417 and struct.unpack_from('<I', packet, 22 + i * 14)[0] == 2606417 * 16:
                return i
        return -1
    if code == 143 and len(packet) == 3 + packet[2] * 4 and packet[2] > 0:
        return 0  # first actual counter-count option, never a fabricated die result
    if code == 14 and len(packet) == 3 + packet[2] * 4:
        options = [struct.unpack_from('<I', packet, 3 + 4*i)[0] for i in range(packet[2])]
        for wanted in (73664385 * 16 + 1, 6958567 * 16 + 2):
            if wanted in options:
                return options.index(wanted)
    if code == 15 and len(packet) == 6 + packet[5] * 8:
        ids = [struct.unpack_from('<I', packet, 6 + i * 8)[0] for i in range(packet[5])]
        chosen = [i for i, card in enumerate(ids) if card in (59106048, 2606417)]
        if packet[3] <= len(chosen) <= packet[4]:
            return bytes([len(chosen)] + chosen)
    if code == 26:  # SELECT_UNSELECT_CARD: fixture's non-targeting SelectSubGroup
        pos = 6
        selectable = packet[pos]; pos += 1
        if pos + selectable * 8 >= len(packet):
            return None
        ids = [struct.unpack_from('<I', packet, pos + i * 8)[0] for i in range(selectable)]
        pos += selectable * 8
        if pos + 1 + packet[pos] * 8 != len(packet):
            return None
        for wanted in (59106048, 2606417):
            if wanted in ids:
                return bytes([1, ids.index(wanted)])
        if packet[2]:
            return -1
    return None

RECOVERY_LUA = r'''
Debug.ReloadFieldBegin(0)
Debug.SetPlayerInfo(0,8000,0,0)
Debug.SetPlayerInfo(1,8000,0,0)
local seed=Debug.AddCard(59106048,0,0,LOCATION_MZONE,0,POS_FACEUP_ATTACK)
local by=Debug.AddCard(83289866,0,0,LOCATION_GRAVE,0,POS_FACEUP)
Debug.AddCard(69247929,0,0,LOCATION_DECK,0,POS_FACEDOWN)
Debug.AddCard(69247929,1,1,LOCATION_DECK,0,POS_FACEDOWN)
Debug.ReloadFieldEnd()
local function check(name,ok) Debug.Message('CHECK|'..name..'|'..tostring(not not ok)) end
local e=Effect.CreateEffect(seed)
e:SetType(EFFECT_TYPE_FIELD+EFFECT_TYPE_CONTINUOUS)
e:SetCode(EVENT_CHAIN_END)
e:SetOperation(function()
    if by:IsLocation(LOCATION_SZONE) then
        check('Bystreet real End Phase recovery places faceup not to hand',by:IsFaceup() and Duel.GetCurrentPhase()==PHASE_END)
        local replacement=by:IsHasEffect(83289866)
        check('End Phase recovery consumes shared discard replacement count',replacement and not replacement:CheckCountLimit(0))
        Debug.Message('PROBE_COMPLETE')
        Duel.Win(0,0)
    end
end)
Duel.RegisterEffect(e,0)
'''

def recover(packet):
    if packet[0] == 11 and packet[-2]:
        return 7  # actually offered End Phase
    if packet[0] == 16 and len(packet) == 12 + packet[2] * 14:
        for i in range(packet[2]):
            if struct.unpack_from('<I', packet, 14 + i*14)[0] == 83289866:
                return i
        return -1
    if packet[0] == 12 and len(packet) == 14 and struct.unpack_from('<I', packet, 2)[0] == 83289866:
        return 1
    return respond(packet)

if __name__ == '__main__':
    if '--end-phase' in sys.argv:
        sys.argv.remove('--end-phase')
        raise SystemExit(main(RECOVERY_LUA, 2, recover))
    raise SystemExit(main(LUA, 11, respond))
