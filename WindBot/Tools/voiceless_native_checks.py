"""Run fixed-opening Voiceless routes through the real native duel host.

These are no-shuffle, no-deck-check scenarios, not tournament deck validation or
a strength benchmark. Assertions use the tested client's public/own move log.
The core RNG seed is unknown. Opponent hands are fixture inputs only, never bot
features. Use a Debug WindBot build because native_smoke needs result markers.
"""
import argparse
from collections import Counter
import hashlib
import importlib.util
import json
from pathlib import Path
import re
import shutil
import sqlite3
import sys
from types import SimpleNamespace

LO, SKULL, SAFFIRA = 25801745, 10774240, 51296484
BARRIER, RADIANCE, BLESSING = 98477480, 86310763, 39114494
SAGE, SAURAVIS, PRAYERS = 88284599, 4810828, 52472775
DIVINER, TRIAS, PREPREP = 92919429, 26866984, 13048472
MONDO, LINKURIBOH, GARDNA = 73898890, 41999284, 2220237
ASH, VEILER, IMPERM, STRIKE, CALLED, DROLL = 14558127, 97268402, 10045474, 40605147, 24224830, 94145021
WARWOLF, SWORDS = 69247929, 72302403
SCENARIOS = [
    ("combo1-lo-mondo", [LO, ASH, VEILER, IMPERM, STRIKE], "mondo", None),
    ("combo2-diviner-mondo", [DIVINER, ASH, VEILER, IMPERM, STRIKE], "diviner", None),
    ("combo3-lo-spell-sage", [LO, CALLED, ASH, VEILER, IMPERM], "sage", None),
    ("combo4-preprep-sage", [PREPREP, TRIAS, ASH, VEILER, IMPERM], "preprep", None),
    ("combo5-lo-skull-blessing", [LO, SKULL, ASH, VEILER, IMPERM], "blessing", None),
    ("recovery-ash", [LO, SAFFIRA, ASH, VEILER, IMPERM], "recovery", ASH),
    ("protection-veiler", [LO, SAFFIRA, ASH, VEILER, IMPERM], "protection", VEILER),
    # Veiler stops Lo before access to Blessing. With no other live starter the
    # legal recovery is next turn's fixed Saffira draw, not an invented extension.
    ("recovery-veiler", [LO, SKULL, ASH, VEILER, IMPERM], "recovery", VEILER),
    ("recovery-droll", [LO, SKULL, ASH, VEILER, IMPERM], "recovery", DROLL),
]


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def read_deck(path):
    zones = {"main": [], "extra": [], "side": []}
    zone = "main"
    for line in path.read_text(encoding="utf-8-sig").splitlines():
        line = line.strip()
        if line == "#main": zone = "main"
        elif line == "#extra": zone = "extra"
        elif line == "!side": zone = "side"
        elif line.isdigit(): zones[zone].append(int(line))
    return zones


def write_deck(path, main, extra):
    path.write_text("#created by Voiceless native scenario\n#main\n"
                    + "\n".join(map(str, main)) + "\n#extra\n"
                    + "\n".join(map(str, extra)) + "\n!side\n", encoding="utf-8")


def inspect_log(path, names, hand, route, interruption):
    lines = [re.sub(r"^\[[^\]]+\] ", "", line)
             for line in path.read_text(encoding="utf-8", errors="replace").splitlines()]
    checks = []
    recovery_turn = 3 if route == "recovery" and interruption == VEILER else 2
    def check(name, passed):
        checks.append({"name": name, "passed": bool(passed)})
    begin = lines.index("*********Bot Hand*********") if "*********Bot Hand*********" in lines else -1
    end = lines.index("*********Bot Spell*********", begin) if begin >= 0 else -1
    check("exact opening hand observed", begin >= 0 and Counter(lines[begin + 1:end]) == Counter(names[x] for x in hand))
    turn = 0
    moves = []
    activations = []
    ordered_moves = []
    ordered_activations = []
    snapshots = []
    field = Counter()
    for line_index, line in enumerate(lines):
        if line == "(Go to Draw)": turn += 1
        move = re.fullmatch(r"\(0 's (.+) from (\w+) move to (\w+)\)", line)
        if move:
            name, before, after = move.groups()
            moves.append((turn, name, before, after))
            ordered_moves.append((line_index, turn, name, before, after))
            if before in ("MonsterZone", "SpellZone"):
                field[(name, before)] -= 1
            if after in ("MonsterZone", "SpellZone"):
                field[(name, after)] += 1
            snapshots.append((turn, line_index, field.copy()))
        act = re.fullmatch(r"\(([01]) 's (.+) activate effect from (\w+)\)", line)
        if act:
            activations.append((turn,) + act.groups())
            ordered_activations.append((line_index, turn) + act.groups())
    def moved(code, before, after, at):
        return (at, names[code], before, after) in moves
    def together(codes, at):
        return any(t == at and all(board[(names[code], "SpellZone" if code in (BARRIER, BLESSING, RADIANCE) else "MonsterZone")] > 0
                                    for code in codes) for t, _, board in snapshots)
    if recovery_turn == 3:
        check("Skull Guardian established on next own turn", any(t == 3 and n == names[SKULL] and after == "MonsterZone" for t, n, _, after in moves))
    else:
        check("Skull Guardian reached field on turn one", any(t == 1 and n == names[SKULL] and after == "MonsterZone" for t, n, _, after in moves))
    if route in ("mondo", "diviner"):
        check("Dyna Mondo Link Summoned on turn one", moved(MONDO, "Extra", "MonsterZone", 1))
        check("Skull revived on opponent turn", moved(SKULL, "Grave", "MonsterZone", 2))
        check("Lo revived on opponent turn", moved(LO, "Grave", "MonsterZone", 2))
        check("Skull + Lo + Radiance established on opponent turn", together([SKULL, LO, RADIANCE], 2))
        check("Mondo optional shuffle skipped on turn one", not any(t == 1 and player == "0" and n == names[MONDO] for t, player, n, _ in activations))
        if route == "diviner":
            check("Diviner sent Trias from deck", moved(TRIAS, "Deck", "Grave", 1))
            check("Diviner was tributed", moved(DIVINER, "MonsterZone", "Grave", 1))
            check("Diviner recruited Lo from deck", moved(LO, "Deck", "MonsterZone", 1))
    elif route in ("sage", "preprep"):
        check("Linkuriboh Link Summoned on turn one", moved(LINKURIBOH, "Extra", "MonsterZone", 1))
        check("Sage summoned from hand on turn one", moved(SAGE, "Hand", "MonsterZone", 1))
        check("Sage summoned Sauravis from deck on opponent turn", moved(SAURAVIS, "Deck", "MonsterZone", 2))
        check("two Rituals + Lo + Radiance established", together([SKULL, SAURAVIS, LO, RADIANCE], 2))
        if route == "preprep":
            check("Pre-Preparation activated", any(t == 1 and player == "0" and n == names[PREPREP] for t, player, n, _ in activations))
            check("LIGHT high-level tribute consumed", moved(TRIAS, "Hand", "Grave", 1))
    elif route == "blessing":
        check("Blessing activated from hand", moved(BLESSING, "Hand", "SpellZone", 1))
        check("Linkuriboh Link Summoned", moved(LINKURIBOH, "Extra", "MonsterZone", 1))
        check("Blessing recovered Lo", moved(LO, "Grave", "Hand", 1))
        check("Secure Gardna Link Summoned", moved(GARDNA, "Extra", "MonsterZone", 1))
        check("Sauravis Ritual Summoned from hand on opponent turn", moved(SAURAVIS, "Hand", "MonsterZone", 2))
        check("two Rituals + Lo + Radiance established", together([SKULL, SAURAVIS, LO, RADIANCE], 2))
    if interruption:
        interruption_at = next((index for index, t, player, n, _ in ordered_activations
                                if t == 1 and player == "1" and n == names[interruption]), None)
        check("opponent used configured interruption on turn one", interruption_at is not None)
        cut = interruption_at if interruption_at is not None else len(lines)
        ready = lambda board: board[(names[LO], "MonsterZone")] > 0 and board[(names[SKULL], "MonsterZone")] > 0
        before = [board for _, index, board in snapshots if index < cut]
        if route == "protection":
            check("Skull Guardian answered Veiler", any(index > cut and t == 1 and player == "0" and n == names[SKULL]
                                                        for index, t, player, n, _ in ordered_activations))
            check("Lo placement resolved after protection", any(index > cut and t == 1 and n == names[BARRIER]
                  and origin == "Deck" and target == "SpellZone" for index, t, n, origin, target in ordered_moves))
        else:
            check("core board was incomplete before interruption", not before or not ready(before[-1]))
            check("Skull had not been established before interruption", not any(index < cut and t == 1 and n == names[SKULL]
                  and target == "MonsterZone" for index, t, n, _, target in ordered_moves))
            check("Lo and Skull established after interruption", any(index > cut and t <= recovery_turn and ready(board)
                                                                      for t, index, board in snapshots))
            if interruption == ASH:
                check("Ash stopped Saffira's Ritual Spell mill", not any(t == 1 and n == names[PRAYERS] and origin == "Deck" and target == "Grave"
                                                                       for t, n, origin, target in moves))
            elif interruption == VEILER:
                check("Veiler prevented Lo's first-turn placement", not any(t == 1 and n in (names[BARRIER], names[BLESSING], names[RADIANCE])
                      and origin == "Deck" and target == "SpellZone" for t, n, origin, target in moves))
                check("recovery used Saffira on the next own turn", any(t == 3 and player == "0" and n == names[SAFFIRA]
                                                                      for t, player, n, _ in activations))
            elif interruption == DROLL:
                check("no deck-to-hand movement after Droll this turn", not any(index > cut and t == 1 and origin == "Deck" and target == "Hand"
                                                                              for index, t, _, origin, target in ordered_moves))
    check("no retry/invalid selection/runtime error", not any(re.search(r"MSG_RETRY|Duel retry|Invalid card selection|Fail to select|Tick Error|Run Error|Unhandled Exception", line, re.I) for line in lines))
    return {"passed": all(c["passed"] for c in checks), "checks": checks,
            "observedTurns": turn, "recoveryTurnLimit": recovery_turn,
            "firstTwoTurnOwnMoves": [m for m in moves if m[0] <= 2],
            "recoveryOwnMoves": [m for m in moves if m[0] <= recovery_turn] if interruption else []}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for arg in ("bot-exe", "opponent-exe", "data-root", "server", "deck", "output-dir"):
        parser.add_argument("--" + arg, type=Path, required=True)
    parser.add_argument("--scenarios", help="Comma-separated scenario names; omitted runs all nine")
    parser.add_argument("--timeout", type=float, default=90)
    args = parser.parse_args()
    if not 10 <= args.timeout <= 300: parser.error("timeout must be 10..300 seconds")
    root = Path(__file__).resolve().parents[2]
    tool = root / "SKYNET/Tools/Skynet/native_smoke.py"
    spec = importlib.util.spec_from_file_location("voiceless_native_smoke", tool)
    smoke = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(smoke)
    # native_smoke fixes seat-zero RPS to 1 and seat-one to 2. Its winning
    # opponent can randomly choose play order. Fix our fixture's RPS to 3 vs 2
    # so Voiceless actually chooses to go first; never assume seat means turn.
    original_launch = smoke.launch
    def launch_fixture(command, cwd, log):
        if "Name=SkynetQA" in command:
            command = ["Hand=3" if arg == "Hand=1" else arg for arg in command]
        return original_launch(command, cwd, log)
    smoke.launch = launch_fixture
    for attr in ("bot_exe", "opponent_exe", "data_root", "server", "deck"):
        setattr(args, attr, getattr(args, attr).resolve(strict=True))
    out = args.output_dir.resolve()
    out.mkdir(parents=True, exist_ok=False)
    deck = read_deck(args.deck)
    with sqlite3.connect((args.data_root / "locales/en-US/cards.cdb").as_uri() + "?mode=ro", uri=True) as db:
        names = dict(db.execute("select id,name from texts"))
    chosen = args.scenarios.split(",") if args.scenarios else [s[0] for s in SCENARIOS]
    if set(chosen) - {s[0] for s in SCENARIOS}: parser.error("Unknown scenario name")
    result = {"format": "voiceless-native-checks-v2",
              "purpose": "Real engine route milestones and bounded completion; not strength evaluation",
              "botSha256": digest(args.bot_exe), "opponentSha256": digest(args.opponent_exe),
              "serverSha256": digest(args.server), "databaseSha256": digest(args.data_root / "locales/en-US/cards.cdb"),
              "scriptsSha256": digest(args.data_root / "script.zip"), "deckSha256": digest(args.deck),
              "coreSeed": None, "noShuffle": True, "noDeckCheck": True,
              "targetRps": 3, "opponentRps": 2, "scenarios": []}
    for name, hand, route, interruption in SCENARIOS:
        if name not in chosen: continue
        case = out / name
        inputs = case / "inputs"
        inputs.mkdir(parents=True)
        for src, target in [(args.data_root / "script.zip", "script.zip"),
                            (args.data_root / "locales/en-US/cards.cdb", "cards.cdb")]:
            shutil.copy2(src, inputs / target)
        remaining = list(deck["main"])
        for code in hand: remaining.remove(code)
        target_deck = inputs / "target.ydk"
        write_deck(target_deck, hand + remaining, deck["extra"])
        opponent_hand = [interruption, WARWOLF, WARWOLF, WARWOLF, WARWOLF] if interruption else [SWORDS, WARWOLF, WARWOLF, WARWOLF, WARWOLF]
        opponent_deck = inputs / "opponent.ydk"
        write_deck(opponent_deck, opponent_hand + [WARWOLF] * 35, [])
        settings = SimpleNamespace(server=args.server, bot_exe=args.bot_exe, opponent_exe=args.opponent_exe,
            timeout=args.timeout, policy="Voiceless", opponent_policy="Voiceless" if interruption else "OldSchool",
            policy_seed=20261102, train=False, policy_group="voiceless-fixture:" + name,
            model=None, agent=None, opponent_agent=None, shadow=False, explain=False)
        duel = smoke.run_duel(settings, case, 1, target_deck, opponent_deck)
        trace = inspect_log(case / "duel-01/client-0.log", names, hand, route, interruption)
        row = {"name": name, "hand": hand, "route": route, "interruption": interruption,
               "targetDeckSha256": digest(target_deck), "opponentDeckSha256": digest(opponent_deck),
               "duel": duel, "trace": trace,
               "passed": duel["status"] == "passed" and trace["passed"]}
        result["scenarios"].append(row)
        result["passed"] = all(r["passed"] for r in result["scenarios"])
        (out / "result.json").write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
        failed = [c["name"] for c in trace["checks"] if not c["passed"]]
        print(name + ": " + ("PASS" if row["passed"] else "FAIL") + " " + "; ".join(failed), flush=True)
    return 0 if result.get("passed") else 1


if __name__ == "__main__":
    raise SystemExit(main())
