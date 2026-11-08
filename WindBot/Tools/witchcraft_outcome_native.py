"""Isolated installed-core mechanics probes, not bot gameplay or a strength test.

Loads the explicitly selected ocgcore.dll, CDB and script.zip read-only. The legacy
76-byte card-reader ABI uses the core's 16-entry ushort setcode array, not the
obsolete managed wrapper's packed ulong. Lua checks the populated card metadata.
No game configuration is edited; fixtures exist only inside this owned duel.
"""
import argparse
import ctypes as C
import hashlib
import json
from pathlib import Path
import sqlite3
import struct
import time
import zipfile


LUA = r'''
Debug.ReloadFieldBegin(0)
Debug.SetPlayerInfo(0,8000,0,0)
Debug.SetPlayerInfo(1,8000,0,0)
local w=Debug.AddCard(94977269,1,1,LOCATION_MZONE,0,POS_FACEUP_ATTACK,true)
local a=Debug.AddCard(19261966,1,1,LOCATION_MZONE,1,POS_FACEDOWN_DEFENSE,true)
local w2=Debug.AddCard(94977269,1,1,LOCATION_MZONE,2,POS_FACEDOWN_DEFENSE,true)
local v=Debug.AddCard(9603252,0,0,LOCATION_MZONE,0,POS_FACEUP_ATTACK,true)
local h1=Debug.AddCard(69247929,0,0,LOCATION_HAND,0,POS_FACEDOWN)
local h2=Debug.AddCard(69247929,0,0,LOCATION_HAND,0,POS_FACEDOWN)
local gy=Debug.AddCard(69247929,0,0,LOCATION_GRAVE,0,POS_FACEUP)
local deck=Debug.AddCard(69247929,0,0,LOCATION_DECK,0,POS_FACEDOWN)
local spell=Debug.AddCard(6958567,0,0,LOCATION_HAND,0,POS_FACEDOWN)
local ex=Debug.AddCard(9603252,0,0,LOCATION_EXTRA,0,POS_FACEDOWN)
Debug.ReloadFieldEnd()
local function check(name,ok)
    Debug.Message('CHECK|'..name..'|'..tostring(not not ok))
end
local e=Effect.CreateEffect(v)
e:SetType(EFFECT_TYPE_FIELD+EFFECT_TYPE_CONTINUOUS)
e:SetCode(EVENT_ADJUST)
e:SetOperation(function(e,tp)
    e:Reset()
    check('native card metadata ABI',h1:IsType(TYPE_NORMAL) and h1:GetAttack()==2000 and v:IsSetCard(0x128))
    if not h1:IsType(TYPE_NORMAL) or h1:GetAttack()~=2000 or not v:IsSetCard(0x128) then return end
    check('Winda exposes installed destruction protection',w:IsHasEffect(EFFECT_INDESTRUCTABLE_EFFECT))
    Duel.Destroy(Group.FromCards(v,w),REASON_EFFECT)
    check('paired effect destroys friendly Vice but not enemy Winda',v:IsLocation(LOCATION_GRAVE) and w:IsLocation(LOCATION_MZONE))
    check('first summon under Winda legal',h1:IsCanBeSpecialSummoned(e,0,0,false,false))
    Duel.SpecialSummon(h1,0,0,0,false,false,POS_FACEUP)
    check('second summon under Winda illegal',not h2:IsCanBeSpecialSummoned(e,0,0,false,false))
    local dis=Effect.CreateEffect(v)
    dis:SetType(EFFECT_TYPE_SINGLE)
    dis:SetCode(EFFECT_DISABLE)
    dis:SetReset(RESET_EVENT+RESETS_STANDARD)
    w:RegisterEffect(dis)
    Duel.AdjustInstantly(w)
    check('negated Winda restores summon legality',h2:IsCanBeSpecialSummoned(e,0,0,false,false))
    Duel.SpecialSummon(h2,0,0,0,false,false,POS_FACEUP)
    dis:Reset()
    Duel.AdjustAll()
    Debug.Message('Winda disabled after reset='..tostring(w:IsDisabled()))
    check('summons while disabled still counted when restriction returns',not gy:IsCanBeSpecialSummoned(e,0,0,false,false))
    local dis2=Effect.CreateEffect(v)
    dis2:SetType(EFFECT_TYPE_SINGLE)
    dis2:SetCode(EFFECT_DISABLE)
    w:RegisterEffect(dis2)
    Duel.AdjustInstantly(w)
    Duel.Destroy(w,REASON_EFFECT)
    check('negated Winda actually destroyed',w:IsLocation(LOCATION_GRAVE))
    check('removed restriction restores summons',gy:IsCanBeSpecialSummoned(e,0,0,false,false))
    Duel.ChangePosition(w2,POS_FACEUP_ATTACK)
    Duel.AdjustInstantly(w2)
    check('new Winda does not count earlier summons retroactively',gy:IsCanBeSpecialSummoned(e,0,0,false,false))
    Duel.SendtoGrave(w2,REASON_EFFECT)
    Duel.ChangePosition(a,POS_FACEUP_ATTACK)
    Duel.AdjustInstantly(a)
    local se=Effect.CreateEffect(spell)
    se:SetType(EFFECT_TYPE_ACTIVATE)
    se:SetCode(EVENT_FREE_CHAIN)
    spell:RegisterEffect(se)
    local me=Effect.CreateEffect(v)
    me:SetType(EFFECT_TYPE_IGNITION)
    v:RegisterEffect(me)
    Duel.SendtoHand(h2,nil,REASON_EFFECT)
    check('Anoyatyllis blocks spell hand summon',not h2:IsCanBeSpecialSummoned(se,0,0,false,false))
    check('Anoyatyllis blocks spell grave summon',not gy:IsCanBeSpecialSummoned(se,0,0,false,false))
    check('Anoyatyllis permits spell deck summon',deck:IsCanBeSpecialSummoned(se,0,0,false,false))
    check('Anoyatyllis permits monster hand summon',h2:IsCanBeSpecialSummoned(me,0,0,false,false))
    check('Anoyatyllis permits monster grave summon',gy:IsCanBeSpecialSummoned(me,0,0,false,false))
    check('Anoyatyllis permits spell Extra fusion',ex:IsCanBeSpecialSummoned(se,SUMMON_TYPE_FUSION,0,false,false))
    Debug.Message('PROBE_COMPLETE')
    Duel.Win(0,0)
end)
Duel.RegisterEffect(e,0)
'''


class CardData(C.Structure):
    _fields_ = [('code', C.c_uint32), ('alias', C.c_uint32), ('setcode', C.c_uint16 * 16)] + [
        (n, C.c_uint32) for n in ('type', 'level', 'attribute', 'race')] + [
        ('attack', C.c_int32), ('defense', C.c_int32)] + [
        (n, C.c_uint32) for n in ('lscale', 'rscale', 'link_marker')]


def main(lua=LUA, expected_checks=16, responder=None):
    p = argparse.ArgumentParser()
    p.add_argument('--core', type=Path, required=True)
    p.add_argument('--database', type=Path, required=True)
    p.add_argument('--scripts', type=Path, required=True)
    p.add_argument('--output', type=Path, required=True)
    args = p.parse_args()
    if args.output.exists():
        raise SystemExit('Use a fresh receipt path')
    db = sqlite3.connect(args.database.resolve().as_uri() + '?mode=ro', uri=True)
    archive = zipfile.ZipFile(args.scripts)
    entries = {n.removeprefix('./').removeprefix('script/'): n for n in archive.namelist()}
    buffers, messages, checks = {}, [], []
    core = C.CDLL(str(args.core.resolve()))
    pointer = C.c_void_p
    script_cb = C.CFUNCTYPE(pointer, C.c_char_p, C.POINTER(C.c_int))
    card_cb = C.CFUNCTYPE(C.c_uint32, C.c_uint32, C.POINTER(CardData))
    message_cb = C.CFUNCTYPE(C.c_uint32, pointer, C.c_uint32)
    signatures = {
        'set_script_reader': (None, [script_cb]), 'set_card_reader': (None, [card_cb]),
        'set_message_handler': (None, [message_cb]), 'create_duel': (pointer, [C.c_uint32]),
        'preload_script': (C.c_int, [pointer, C.c_char_p]), 'start_duel': (None, [pointer, C.c_uint32]),
        'process': (C.c_uint32, [pointer]), 'get_message': (C.c_int, [pointer, pointer]),
        'get_log_message': (None, [pointer, pointer]), 'end_duel': (None, [pointer]),
        'set_responseb': (None, [pointer, pointer]), 'set_responsei': (None, [pointer, C.c_int]),
    }
    for name, (result, params) in signatures.items():
        fn = getattr(core, name); fn.restype, fn.argtypes = result, params

    @script_cb
    def read_script(raw, length):
        name = raw.decode().removeprefix('./').removeprefix('script/')
        if name not in buffers:
            if name == 'witchcraft_probe.lua': content = lua.encode()
            elif name in entries: content = archive.read(entries[name])
            else: return None
            buffers[name] = C.create_string_buffer(content)
        length[0] = len(buffers[name]) - 1
        return C.addressof(buffers[name])

    @card_cb
    def read_card(code, target):
        row = db.execute('select alias,setcode,type,level,attribute,race,atk,def from datas where id=?', (code,)).fetchone()
        if row:
            alias, sets, kind, level, attr, race, atk, defense = row
            setcodes = (C.c_uint16 * 16)(*[(sets >> (16 * i)) & 65535 for i in range(4)])
            target[0] = CardData(code, alias, setcodes, kind, level & 255,
                                 attr, race, atk, 0 if kind & 0x4000000 else defense,
                                 (level >> 24) & 255, (level >> 16) & 255, defense if kind & 0x4000000 else 0)
        return code

    @message_cb
    def message(duel, kind):
        buf = C.create_string_buffer(4096); core.get_log_message(duel, buf)
        line = buf.value.decode(errors='replace'); messages.append(line)
        if line.startswith('CHECK|'):
            _, name, value = line.split('|'); checks.append({'name': name, 'passed': value == 'true'})
            print(line, flush=True)
        else: print(line, flush=True)
        return 0

    core.set_script_reader(read_script); core.set_card_reader(read_card); core.set_message_handler(message)
    duel = core.create_duel(7)
    try:
        loaded = core.preload_script(duel, b'witchcraft_probe.lua')
        core.start_duel(duel, 0)
        buf = C.create_string_buffer(1024 * 128)
        deadline = time.monotonic() + 10
        while time.monotonic() < deadline:
            result = core.process(duel)
            size = core.get_message(duel, buf)
            if 'PROBE_COMPLETE' in messages or result & 0x20000000: break
            if result & 0x10000000:
                packet = bytes(buf[:size])
                if not packet:
                    continue  # core can yield between confirmation and chain-end without a response prompt
                if responder is not None:
                    messages.append('FIXTURE_PROMPT:' + packet.hex())
                    response = responder(packet)
                    if isinstance(response, int):
                        core.set_responsei(duel, response); continue
                    if isinstance(response, bytes):
                        core.set_responseb(duel, C.create_string_buffer(response)); continue
                # Only placement/position for the probe's explicit summons. Never
                # guess strategic effect, material or target responses.
                if size == 7 and packet[0] == 18 and packet[2] == 1:
                    mask = struct.unpack_from('<I', packet, 3)[0]
                    sequence = next((i for i in range(5) if not mask & (1 << i)), None)
                    if sequence is not None:
                        response = C.create_string_buffer(bytes([packet[1], 4, sequence]))
                        core.set_responseb(duel, response); continue
                if size == 7 and packet[0] == 19 and packet[6] & 1:
                    core.set_responsei(duel, 1); continue
                messages.append('UNEXPECTED_WAIT:' + packet.hex()); break
    except (OSError, RuntimeError) as exc:
        messages.append('NATIVE_ERROR:' + str(exc))
    finally:
        core.end_duel(duel)
    receipt = {'purpose': 'Installed-core scripted mechanics, not executor gameplay',
               'fixture_sha256': hashlib.sha256(lua.encode()).hexdigest(),
               'loaded': loaded, 'checks': checks, 'messages': messages,
               'inputs': {k: {'path': str(v.resolve()), 'sha256': hashlib.sha256(v.read_bytes()).hexdigest()}
                          for k, v in [('core', args.core), ('database', args.database), ('scripts', args.scripts)]}}
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(receipt, indent=2), encoding='utf-8')
    return 0 if len(checks) == expected_checks and all(c['passed'] for c in checks) and 'PROBE_COMPLETE' in messages else 1


if __name__ == '__main__':
    raise SystemExit(main())
