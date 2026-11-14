"""Actual installed GY effects with strict offered-response fixture drivers.

These are mechanics tests, not full WindBot games or a competitive evaluation.
All setup stays inside this isolated duel; installed scripts remain read-only.
"""
import struct
import sys
from witchcraft_outcome_native import main
from witchcraft_coverage_native import respond as placement

SCENARIOS = {
    'seed': (59106048, '''
local source=add(59106048,0,LOCATION_GRAVE)
local spell=add(83289866,0,LOCATION_HAND)
local keep=add(14558127,0,LOCATION_HAND)
add(57916305,0,LOCATION_DECK)
''', '''
check('Seed banished as activation cost',source:IsLocation(LOCATION_REMOVED))
check('Seed returns one then draws one without losing protected hand card',Duel.GetFieldGroupCount(0,LOCATION_HAND,0)==2 and keep:IsLocation(LOCATION_HAND))
''', [83289866]),
    'pittore': (95245544, '''
local source=add(95245544,0,LOCATION_GRAVE)
local spell=add(83289866,0,LOCATION_HAND)
add(57916305,0,LOCATION_DECK)
''', '''
check('Pittore banished as cost',source:IsLocation(LOCATION_REMOVED))
check('Pittore draws then sends selected Witch to GY',spell:IsLocation(LOCATION_GRAVE) and Duel.GetFieldGroupCount(0,LOCATION_HAND,0)==1)
''', [83289866]),
    'potterie': (59851535, '''
local source=add(59851535,0,LOCATION_GRAVE)
local spell=add(57916305,0,LOCATION_GRAVE)
''', '''
check('Potterie banished as cost',source:IsLocation(LOCATION_REMOVED))
check('Potterie recovers target into initially empty hand',spell:IsLocation(LOCATION_HAND))
''', [57916305]),
    'distortion': (69748261, '''
local source=add(69748261,0,LOCATION_GRAVE)
local discard=add(58139997,0,LOCATION_HAND)
local target=add(6071005,0,LOCATION_DECK)
''', '''
check('Distortion banishes itself',source:IsLocation(LOCATION_REMOVED))
check('Distortion searches then discards',target:IsLocation(LOCATION_HAND) and discard:IsLocation(LOCATION_GRAVE))
''', [6071005, 58139997]),
    'rilliona': (33475154, '''
local source=add(33475154,0,LOCATION_GRAVE,true)
local spell=add(83289866,0,LOCATION_HAND)
''', '''
check('Rilliona revives at own End Phase in defense',source:IsLocation(LOCATION_MZONE) and source:IsDefensePos() and Duel.GetCurrentPhase()==PHASE_END)
check('Rilliona reveal does not spend Spell',spell:IsLocation(LOCATION_HAND))
''', [83289866]),
    'luluwalilith': (53971455, '''
local source=add(53971455,0,LOCATION_MZONE,true)
local target=add(97268402,0,LOCATION_DECK)
''', '''
check('Luluwalilith remains GY after trigger',source:IsLocation(LOCATION_GRAVE))
check('Luluwalilith recruits legal LIGHT equal-stat Spellcaster',target:IsLocation(LOCATION_MZONE))
''', [97268402]),
    'marka': (6071005, '''
local source=add(6071005,1,LOCATION_GRAVE)
local victim=add(59106048,1,LOCATION_MZONE)
local tribute=add(69247929,0,LOCATION_MZONE)
add(12580477,0,LOCATION_HAND)
''', '''
check('Marka real opponent-removal trigger revives',source:IsLocation(LOCATION_MZONE) and victim:IsLocation(LOCATION_GRAVE))
check('Marka tributes opposing monster by effect',tribute:IsLocation(LOCATION_GRAVE) and tribute:IsReason(REASON_RELEASE))
Duel.SendtoGrave(source,REASON_EFFECT)
check('revived Marka redirects leaving field to banishment',source:IsLocation(LOCATION_REMOVED))
''', [69247929]),
    'genni-celebration': (64756282, '''
local source=add(64756282,0,LOCATION_GRAVE)
local spell=add(6958567,0,LOCATION_GRAVE)
local material=add(21744288,0,LOCATION_GRAVE)
local fusion=add(9603252,0,LOCATION_EXTRA)
''', '''
check('Genni copy pays Spell banish',spell:IsLocation(LOCATION_REMOVED))
check('copied Celebration recycles banished Genni and GY material',source:IsLocation(LOCATION_DECK) and material:IsLocation(LOCATION_DECK) and fusion:IsLocation(LOCATION_MZONE))
''', [6958567, 9603252, 64756282, 21744288]),
    'genni-confusion': (64756282, '''
local source=add(64756282,0,LOCATION_GRAVE)
local spell=add(35098357,0,LOCATION_GRAVE)
local mat1=add(21744288,0,LOCATION_MZONE)
local mat2=add(58139997,0,LOCATION_HAND)
local fusion=add(9603252,0,LOCATION_EXTRA)
''', '''
check('Genni Confusion pays both banishes',source:IsLocation(LOCATION_REMOVED) and spell:IsLocation(LOCATION_REMOVED))
check('copied Confusion uses actual hand/field materials',mat1:IsLocation(LOCATION_GRAVE) and mat2:IsLocation(LOCATION_GRAVE) and fusion:IsLocation(LOCATION_MZONE))
''', [35098357, 9603252, 21744288, 58139997]),
    'genni-unveiling': (64756282, '''
local source=add(64756282,0,LOCATION_GRAVE)
local spell=add(70226289,0,LOCATION_GRAVE)
local target=add(84523092,0,LOCATION_HAND)
''', '''
check('Genni Unveiling pays both banishes',source:IsLocation(LOCATION_REMOVED) and spell:IsLocation(LOCATION_REMOVED))
check('copied Unveiling summons real hand Witch',target:IsLocation(LOCATION_MZONE))
''', [70226289, 84523092]),
    'genni-send': (64756282, '''
local source=add(64756282,0,LOCATION_GRAVE)
local spell=add(73664385,0,LOCATION_GRAVE)
local target=add(95245544,0,LOCATION_DECK)
''', '''
check('Genni Lacrima pays both banishes',source:IsLocation(LOCATION_REMOVED) and spell:IsLocation(LOCATION_REMOVED))
check('copied Lacrima send mode sends actual Deck target',target:IsLocation(LOCATION_GRAVE))
''', [73664385, 95245544]),
    'genni-destroy': (64756282, '''
local source=add(64756282,0,LOCATION_GRAVE)
local spell=add(6958567,0,LOCATION_GRAVE)
local seed=add(59106048,0,LOCATION_MZONE)
add(83289866,0,LOCATION_SZONE)
local target=add(2606417,1,LOCATION_SZONE)
''', '''
check('Genni destruction mode pays both banishes',source:IsLocation(LOCATION_REMOVED) and spell:IsLocation(LOCATION_REMOVED))
check('copied Celebration destroys enemy while Bystreet preserves Seed',seed:IsLocation(LOCATION_MZONE) and target:IsLocation(LOCATION_GRAVE))
''', [6958567, 59106048, 2606417]),
    'genni-draping': (64756282, '''
local source=add(64756282,0,LOCATION_GRAVE)
local spell=add(56894757,0,LOCATION_GRAVE)
add(59106048,0,LOCATION_MZONE)
local target=add(2606417,1,LOCATION_SZONE)
''', '''
check('Genni Draping pays both banishes',source:IsLocation(LOCATION_REMOVED) and spell:IsLocation(LOCATION_REMOVED))
check('copied Draping returns opposing Spell Trap to hand',target:IsLocation(LOCATION_HAND))
''', [56894757, 2606417]),
}

for base, assertion in (
    ('seed', "check('negated Seed keeps original hand',spell:IsLocation(LOCATION_HAND) and keep:IsLocation(LOCATION_HAND))"),
    ('pittore', "check('negated Pittore does not send its resolution discard',spell:IsLocation(LOCATION_HAND))"),
    ('distortion', "check('negated Distortion does not search or discard',discard:IsLocation(LOCATION_HAND) and target:IsLocation(LOCATION_DECK))"),
):
    source, setup, _, prefs = SCENARIOS[base]
    SCENARIOS[base + '-negated'] = (source, setup + '\nadd(14558127,1,LOCATION_HAND)\n',
        "check('negated GY effect still pays banish cost',source:IsLocation(LOCATION_REMOVED))\n" + assertion, prefs)

for spell_id in (57916305, 70226289, 35098357, 6958567, 56894757):
    SCENARIOS[str(spell_id) + '-recovery'] = (spell_id,
        'local source=add(' + str(spell_id) + ',0,LOCATION_GRAVE)\nlocal witch=add(59106048,0,LOCATION_MZONE)\n',
        "check('installed Witch Spell recovers during own End Phase',source:IsLocation(LOCATION_HAND) and Duel.GetCurrentPhase()==PHASE_END)\n"
        "check('recovery does not consume Witch monster',witch:IsLocation(LOCATION_MZONE))\n", [])

def run(scenario):
    source, setup, assertions, preferences = SCENARIOS[scenario]
    lua = '''
Debug.ReloadFieldBegin(0)
Debug.SetPlayerInfo(0,8000,0,0)
Debug.SetPlayerInfo(1,8000,0,0)
local function add(id,p,loc,proper)
    return Debug.AddCard(id,p,p,loc,0,(loc==LOCATION_HAND or loc==LOCATION_DECK or loc==LOCATION_EXTRA) and POS_FACEDOWN or POS_FACEUP_ATTACK,proper or false)
end
''' + setup + '''
Debug.ReloadFieldEnd()
local function check(name,ok) Debug.Message('CHECK|'..name..'|'..tostring(not not ok)) end
local accepted=false
local watch=Effect.CreateEffect(source)
watch:SetType(EFFECT_TYPE_FIELD+EFFECT_TYPE_CONTINUOUS)
watch:SetCode(EVENT_CHAINING)
watch:SetOperation(function(e,tp,eg,ep,ev,re)
    if re:GetHandler()==source then accepted=true end
end)
Duel.RegisterEffect(watch,0)
local finish=Effect.CreateEffect(source)
finish:SetType(EFFECT_TYPE_FIELD+EFFECT_TYPE_CONTINUOUS)
finish:SetCode(EVENT_CHAIN_END)
finish:SetOperation(function()
    if not accepted then return end
''' + assertions + '''
    Debug.Message('PROBE_COMPLETE')
    Duel.Win(0,0)
end)
Duel.RegisterEffect(finish,0)
'''
    if scenario == 'luluwalilith':
        lua += '''
local send=Effect.CreateEffect(source)
send:SetType(EFFECT_TYPE_FIELD+EFFECT_TYPE_CONTINUOUS)
send:SetCode(EVENT_ADJUST)
send:SetOperation(function(e) e:Reset() Duel.SendtoGrave(source,REASON_EFFECT) end)
Duel.RegisterEffect(send,0)
'''

    def respond(packet):
        code = packet[0]
        if code == 11:
            pos = 2
            for _ in range(5):
                pos += 1 + packet[pos] * 7
            count = packet[pos]; pos += 1
            if pos + count * 11 + 3 != len(packet): return None
            for i in range(count):
                card = struct.unpack_from('<I', packet, pos + 11*i)[0]
                if card == source or scenario == 'marka' and card == 12580477:
                    return (i << 16) | 5
            if (scenario in ('rilliona', 'luluwalilith') or scenario.endswith('-recovery')) and packet[-2]: return 7
        if code == 16 and len(packet) == 12 + packet[2] * 14:
            for i in range(packet[2]):
                card = struct.unpack_from('<I', packet, 14 + i*14)[0]
                desc = struct.unpack_from('<I', packet, 22 + i*14)[0]
                if card == source: return i
                if scenario.endswith('-negated') and card == 14558127: return i
                if card == 2606417 and desc == 2606417 * 16: return i
            return -1
        if code == 12 and len(packet) == 14:
            return int(struct.unpack_from('<I', packet, 2)[0] == source)
        if code == 14 and len(packet) == 3 + packet[2] * 4:
            options = [struct.unpack_from('<I', packet, 3 + i*4)[0] for i in range(packet[2])]
            for option in (6958567 * 16 + 3, 6958567 * 16 + 2, 73664385 * 16 + 1):
                if option in options: return options.index(option)
        if code == 15 and len(packet) == 6 + packet[5] * 8:
            ids = [struct.unpack_from('<I', packet, 6 + 8*i)[0] for i in range(packet[5])]
            if scenario == 'distortion' and 58139997 in ids:
                return bytes([1, ids.index(58139997)])
            chosen = [ids.index(card) for card in preferences if card in ids][:packet[4]]
            if packet[3] <= len(chosen) <= packet[4]: return bytes([len(chosen)] + chosen)
        if code == 26:
            count = packet[6]
            pos = 7 + count*8
            if pos >= len(packet) or pos + 1 + packet[pos]*8 != len(packet): return None
            ids = [struct.unpack_from('<I', packet, 7+8*i)[0] for i in range(count)]
            for card in preferences:
                if card in ids: return bytes([1, ids.index(card)])
            if packet[2]: return -1
        return placement(packet) if code in (18, 143) else None

    return main(lua, 3 if scenario == 'marka' else 2, respond)

if __name__ == '__main__':
    index = sys.argv.index('--scenario')
    scenario = sys.argv[index + 1]
    del sys.argv[index:index + 2]
    raise SystemExit(run(scenario))
