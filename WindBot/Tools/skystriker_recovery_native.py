"""Isolated script-backed decision fixtures; artificial setup/termination, not wins.

The fixture adds one test-only setup card. All real card Lua entries are copied
unchanged. Legal offers and subsequent resolution come from the selected server.
"""
import argparse
import json
from pathlib import Path
import shutil
import sqlite3
from types import SimpleNamespace
import zipfile
from skystriker_native_checks import load_module


def main():
    p = argparse.ArgumentParser(description=__doc__)
    for key in ('bot-exe', 'data-root', 'server', 'output-dir'):
        p.add_argument('--' + key, type=Path, required=True)
    a = p.parse_args()
    root = a.output_dir.resolve()
    root.mkdir(parents=True, exist_ok=False)
    repo = Path(__file__).resolve().parents[2]
    smoke = load_module('recovery_smoke', repo / 'SKYNET/Tools/Skynet/native_smoke.py')
    decks = load_module('recovery_decks', Path(__file__).with_name('voiceless_native_checks.py'))
    binary = a.bot_exe.resolve(strict=True)
    shutil.copytree(binary.parent, root / 'runtime')
    setup = Path(__file__).with_name('skystriker_recovery_fixture.lua').read_text(encoding='utf-8')
    results = {'kind': 'native offers/resolution; artificial initial state and termination; not strength',
               'binarySha256': smoke.digest(binary), 'fixtureSha256': smoke.digest(Path(__file__).with_name('skystriker_recovery_fixture.lua')),
               'sourceScriptSha256': smoke.digest(a.data_root / 'script.zip'), 'cases': []}
    for number, name in enumerate(('duster-snow', 'ultimate-lumina', 'ultimate-michael',
                                 'multi-attacker-lethal', 'lemn-free-battle', 'ultimate-mill-hold'), 1):
        case = root / name
        inputs = case / 'inputs'
        inputs.mkdir(parents=True)
        shutil.copy2(a.server, inputs / 'ygoserver.dll')
        shutil.copy2(a.data_root / 'script.zip', inputs / 'script.zip')
        shutil.copy2(a.data_root / 'locales/en-US/cards.cdb', inputs / 'cards.cdb')
        fixture_id = 99999990
        with sqlite3.connect(inputs / 'cards.cdb') as db:
            assert not db.execute('select 1 from datas where id=?', (fixture_id,)).fetchone()
            db.execute('insert into datas select ?,ot,alias,setcode,type,atk,def,level,race,attribute,category from datas where id=89631139', (fixture_id,))
            db.execute('update datas set type=33 where id=?', (fixture_id,))
            columns = [r[1] for r in db.execute('pragma table_info(texts)')]
            values = [fixture_id, 'Recovery Fixture', 'Test-only initial state; not a real card.'] + [''] * (len(columns) - 3)
            db.execute('insert into texts values (' + ','.join('?' for _ in values) + ')', values)
        with zipfile.ZipFile(inputs / 'script.zip', 'a') as z:
            sample = next(n for n in z.namelist() if n.endswith('c38817295.lua'))
            prefix = sample[:-len('c38817295.lua')]
            z.writestr(prefix + 'c99999990.lua', setup.replace('CASE_ID', str(number)))
        with zipfile.ZipFile(a.data_root / 'script.zip') as original_scripts, zipfile.ZipFile(inputs / 'script.zip') as fixture_scripts:
            assert all(original_scripts.read(n) == fixture_scripts.read(n) for n in original_scripts.namelist())
        target = inputs / 'target.ydk'
        other = inputs / 'opponent.ydk'
        decks.write_deck(target, [fixture_id, 51227866, 98338152, 98829635, 38817295, 26077387] + [89631139] * 34, [])
        decks.write_deck(other, [18144506, 55623480, 80280944, 95503687, 77558536] + [89631139] * 35, [4779823])
        if number == 5:
            decks.write_deck(target, [fixture_id] + [89631139] * 39, [63288573])
            decks.write_deck(other, [89631139] * 40, [41462083])
        original = smoke.launch
        def launch(command, cwd, log):
            if 'Name=SkynetQA' in command:
                command = ['Hand=3' if s == 'Hand=1' else s for s in command]
            return original(command, cwd, log)
        smoke.launch = launch
        settings = SimpleNamespace(server=inputs / 'ygoserver.dll', timeout=35,
            bot_exe=root / 'runtime' / binary.name, opponent_exe=root / 'runtime' / binary.name,
            policy='SkyStriker2026', opponent_policy='Lightsworn', policy_seed=20261108,
            train=False, policy_group='recovery-fixture', model=None, agent=None,
            opponent_agent=None, shadow=False, explain=False)
        duel = smoke.run_duel(settings, case, 1, target, other)
        smoke.launch = original
        log = (case / 'duel-01/client-0.log').read_text(encoding='utf-8', errors='replace')
        if number == 1:
            trigger = "(1 's Harpie's Feather Duster activate effect"
            response = "(0 's Sky Striker Mecha - Shark Cannon activate effect"
            outcome = "(1 's Fairy Tail - Snow from Grave move to Removed)"
        elif number in (2, 3):
            trigger = "(1 's " + ('Lumina, Lightsworn Summoner' if number == 2 else 'Michael, the Arch-Lightsworn') + " activate effect"
            response = "(0 's Ultimate Sky activate effect"
            outcome = "(MonsterZone 's " + ('Lumina, Lightsworn Summoner' if number == 2 else 'Michael, the Arch-Lightsworn') + " become target)"
        if number <= 3:
            checks = {'trigger observed': trigger in log, 'response after trigger': trigger in log and response in log and log.index(response) > log.index(trigger),
                  'expected target/outcome observed': outcome in log,
                  'no protocol/runtime error': not any(x in log for x in ('Tick Error', 'MSG_RETRY', 'Invalid card selection'))}
        else:
            checks = {'no protocol/runtime error': not any(x in log for x in ('Tick Error', 'MSG_RETRY', 'Invalid card selection'))}
            if number == 4:
                checks.update({'Raye attacks': '(Sky Striker Ace - Raye direct attack!!)' in log,
                               'Roze attacks': '(Sky Striker Ace - Roze direct attack!!)' in log,
                               'Upstart not activated': "(0 's Upstart Goblin activate effect" not in log,
                               'actual damage reduces LP to zero': '(1 got damage , LifePoint left = 0)' in log})
            elif number == 5:
                checks.update({'Lemnisgate retained': "(0 's Sky Striker Special Maneuver - Lemnisgate! activate effect" not in log,
                               'known 3000 attacker battles blocker': "(0 's Blue-Eyes White Dragon attack  1 's Thousand Dragon)" in log,
                               'actual damage reduces LP to zero': '(1 got damage , LifePoint left = 0)' in log})
            else:
                checks.update({'Lumina effect exercised': "(1 's Lumina, Lightsworn Summoner activate effect" in log,
                               'Ultimate preserved on insignificant mill': "(0 's Ultimate Sky activate effect" not in log})
        if number == 2:
            checks['Raiden not revived'] = "(1 's Raiden, Hand of the Lightsworn from Grave move to MonsterZone)" not in log
        if number == 3:
            checks['Raye not banished'] = "(0 's Sky Striker Ace - Raye from MonsterZone move to Removed)" not in log
        row = {'name': name, 'duel': duel, 'checks': checks, 'passed': duel['status'] == 'passed' and all(checks.values())}
        results['cases'].append(row)
        results['passed'] = all(r['passed'] for r in results['cases'])
        (root / 'result.json').write_text(json.dumps(results, indent=2), encoding='utf-8')
        print(name, 'PASS' if row['passed'] else 'FAIL', checks, flush=True)
    return 0 if results['passed'] else 1


if __name__ == '__main__':
    raise SystemExit(main())
