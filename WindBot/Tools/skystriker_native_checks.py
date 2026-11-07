"""Real MDPro4 script-backed Sky Striker regression fixtures, not a strength test.

Inputs are explicit and copied to a new output directory. The tested deck's card
multiset is preserved; opponents are deliberately synthetic no-deck-check decks.
No installed game files are edited. Core RNG seed is not controllable here.
"""
import argparse
from collections import Counter
import importlib.util
import json
from pathlib import Path
import re
import random
import shutil
import sqlite3
from types import SimpleNamespace

RAYE, ROZE, ENGAGE, LINKAGE = 26077387, 37351133, 63166095, 9726840
LEMN, HORNET, ASH, DROLL, CROWN = 34433770, 52340444, 14558127, 94145021, 98829635
ZERO, KAGARI, HAYATE, UPSTART = 76072561, 63288573, 8491308, 70368879
BLUE_EYES = 89631139
SCENARIOS = {
    "linkage-otk": ([RAYE, LINKAGE, ASH, DROLL, CROWN], [BLUE_EYES] * 40, False),
    "double-linkage": ([UPSTART, LINKAGE, LINKAGE, ASH, DROLL], [BLUE_EYES] * 40, True),
    "droll-replan": ([ENGAGE, RAYE, ROZE, LEMN, LINKAGE], [DROLL] + [BLUE_EYES] * 39, True),
    "hornet-setup": ([HORNET, LEMN, CROWN, ASH, DROLL], [BLUE_EYES] * 40, True),
    "lemn-grave": ([LINKAGE, LEMN, ASH, DROLL, CROWN], [12580477] + [BLUE_EYES] * 39, True),
}


def load_module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def inspect(path, names, name, opening):
    lines = [re.sub(r"^\[[^\]]+\] ", "", s) for s in
             path.read_text(encoding="utf-8", errors="replace").splitlines()]
    checks = []
    def check(label, passed):
        checks.append({"name": label, "passed": bool(passed)})
    begin = lines.index("*********Bot Hand*********") if "*********Bot Hand*********" in lines else -1
    end = lines.index("*********Bot Spell*********", begin) if begin >= 0 else -1
    observed = Counter(lines[begin + 1:end]) if begin >= 0 else Counter()
    check("configured opening observed", observed == Counter(names[c] for c in opening))
    turn, moves, acts, attacks = 0, [], [], []
    for index, line in enumerate(lines):
        if line == "(Go to Draw)": turn += 1
        m = re.fullmatch(r"\(([01]) 's (.+) from (\w+) move to (\w+)\)", line)
        if m: moves.append((index, turn, *m.groups()))
        a = re.fullmatch(r"\(([01]) 's (.+) activate effect from (\w+)\)", line)
        if a: acts.append((index, turn, *a.groups()))
        if "direct attack!!" in line: attacks.append((index, turn, line))
    def moved(code, before, after, player="0", at=None):
        return any(p == player and n == names[code] and b == before and a == after and
                   (at is None or t == at) for _, t, p, n, b, a in moves)
    check("no engine retry or runtime error", not any(re.search(
        r"MSG_RETRY|Duel retry|Invalid card selection|Fail to select|Tick Error|Run Error|Unhandled Exception", s, re.I) for s in lines))
    if name == "linkage-otk":
        check("Hayate starts the turn-two battle route", moved(HAYATE, "Extra", "MonsterZone", at=2))
        check("Kagari recovers Linkage in turn two", moved(LINKAGE, "Grave", "Hand", at=2))
        check("Zero summoned in turn two", moved(ZERO, "Extra", "MonsterZone", at=2))
        check("Raye and Roze released for final attacks", all(
              moved(code, "Grave", "MonsterZone", at=2) or moved(code, "Deck", "MonsterZone", at=2)
              for code in (RAYE, ROZE)))
        check("five attacks in the same turn", sum(t == 2 for _, t, _ in attacks) >= 5)
        check("opponent defeated on turn two", any("(1 got damage , LifePoint left = 0)" in s for s in lines) and turn == 2)
    elif name == "double-linkage":
        first = [a for a in acts if a[1] == 1 and a[2] == "0" and a[3] == names[LINKAGE]]
        check("one setup Linkage, not two unresolved copies", len(first) == 1)
        check("setup Zero survives the first turn", moved(ZERO, "Extra", "MonsterZone", at=1) and
              not moved(ZERO, "MonsterZone", "Grave", at=1))
        check("Zero summon search accepted", any("Zero summon search" in s and "activate=True" in s for s in lines))
    elif name == "droll-replan":
        droll = next((i for i, t, p, n, _ in acts if t == 1 and p == "1" and n == names[DROLL]), None)
        check("opponent Droll exercised", droll is not None)
        check("setup continues after Droll", droll is not None and any(i > droll and t == 1 and
              p == "0" and a == "MonsterZone" for i, t, p, n, b, a in moves))
        check("no deck-to-hand attempt predicted after Droll", droll is not None and not any(
              "route=" in s and ("Engage ->" in s or "ROTA ->" in s or "Pot of Desires recovery" in s)
              for s in lines[droll + 1:next((i for i in range(droll + 1, len(lines)) if lines[i] == "(Go to Draw)"), len(lines))]))
    elif name == "hornet-setup":
        check("Kaina bridge summoned", moved(12421694, "Extra", "MonsterZone", at=1))
        check("Hornet route reaches Zero", moved(ZERO, "Extra", "MonsterZone", at=1))
        check("no targetless Lemnisgate activation", not any(p == "0" and n == names[LEMN]
              and loc != "Grave" for _, _, p, n, loc in acts))
    elif name == "lemn-grave":
        check("Lemnisgate sent as real setup fuel", moved(LEMN, "SpellZone", "Grave", at=1))
        check("opponent board wipe exercised", any(t == 2 and p == "1" and n == names[12580477]
              for _, t, p, n, _ in acts))
        check("grave resource retained when the same normal Link is free", not any(t == 3 and p == "0" and
              n == names[LEMN] and loc == "Grave" for _, t, p, n, loc in acts))
        check("normal recovery still reaches Kagari and Zero", moved(KAGARI, "Extra", "MonsterZone", at=3)
              and moved(HORNET, "Grave", "Hand", at=3) and moved(ZERO, "Extra", "MonsterZone", at=3))
    return {"passed": all(c["passed"] for c in checks), "checks": checks, "turns": turn}


def audit_mirror(path, names):
    """Observable regressions only; do not infer legality or strength from a win."""
    lines = [re.sub(r"^\[[^\]]+\] ", "", s) for s in
             path.read_text(encoding="utf-8", errors="replace").splitlines()]
    failures, lemma_returns = [], []
    pending_linkage, pairs, recycled, turns, order = False, None, 0, 0, None
    recommendation = None
    for index, line in enumerate(lines, 1):
        if line == "(Go to Draw)":
            turns += 1
        if turns == 2 and order is None and re.fullmatch(r"\([01] draw 1 card\)", line):
            order = "second" if line.startswith("(0") else "first"
        if "route=" in line:
            recommendation = line.split("route=", 1)[1].split(" > ", 1)[0]
        if line == "(Go to End)" and recommendation and recommendation.startswith(
                ("Normal Raye", "Normal Roze", "Pot of Desires", "Linkage fuel")):
            failures.append({"line": index, "reason": "unexecuted recommended root: " + recommendation})
        if re.match(r"\([01] 's .+ (?:activate effect|from )", line) or line.startswith("(Go to "):
            recommendation = None
        if line == "(0 's %s activate effect from SpellZone)" % names[LINKAGE]:
            if pending_linkage:
                failures.append({"line": index, "reason": "duplicate unresolved own Linkage"})
            pending_linkage = True
        if line == "(0 's %s from SpellZone move to Grave)" % names[LINKAGE]:
            pending_linkage = False
        match = re.search(r"Lemnisgate: recycle pairs=(\d+), useful bounces=(\d+)", line)
        if match:
            pairs, useful = map(int, match.groups())
            recycled = 0
            if pairs < 2 or useful < 1:
                failures.append({"line": index, "reason": "Lemnisgate without a payable bounce"})
        if pairs is not None and re.fullmatch(r"\(0 's .+ from Grave move to (?:Extra|Deck)\)", line):
            recycled += 1
        if pairs is not None and line == "(0 's %s from SpellZone move to Grave)" % names[LEMN]:
            lemma_returns.append({"line": index, "pairs": pairs, "returnedCards": recycled})
            if recycled != 2 * pairs:
                failures.append({"line": index, "reason": "Lemnisgate returned a different number than planned"})
            pairs = None
        if re.search(r"MSG_RETRY|Duel retry|Invalid card selection|Fail to select|Tick Error|Run Error|Unhandled Exception", line, re.I):
            failures.append({"line": index, "reason": line})
    return {"passed": not failures, "failures": failures, "turnOrder": order,
            "lemnisgateReturns": lemma_returns}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for arg in ("bot-exe", "data-root", "server", "deck", "output-dir"):
        parser.add_argument("--" + arg, type=Path, required=True)
    parser.add_argument("--scenarios", default=",".join(SCENARIOS))
    parser.add_argument("--mirror-games", type=int, default=0, help="Even count; paired, fixed deck permutations")
    parser.add_argument("--seed", type=int, default=20261107)
    args = parser.parse_args()
    chosen = [s for s in args.scenarios.split(",") if s]
    if set(chosen) - SCENARIOS.keys(): parser.error("unknown scenario")
    if args.mirror_games < 0 or args.mirror_games % 2: parser.error("mirror-games must be nonnegative and even")
    if not chosen and not args.mirror_games: parser.error("select at least one test")
    repo = Path(__file__).resolve().parents[2]
    smoke = load_module("sky_native_smoke", repo / "SKYNET/Tools/Skynet/native_smoke.py")
    deck_tools = load_module("sky_deck_tools", Path(__file__).with_name("voiceless_native_checks.py"))
    for attr in ("bot_exe", "data_root", "server", "deck"):
        setattr(args, attr, getattr(args, attr).resolve(strict=True))
    root = args.output_dir.resolve()
    root.mkdir(parents=True, exist_ok=False)
    shutil.copytree(args.bot_exe.parent, root / "runtime")
    inputs = root / "inputs"
    inputs.mkdir()
    for src, dst in [(args.server, "ygoserver.dll"), (args.data_root / "script.zip", "script.zip"),
                     (args.data_root / "locales/en-US/cards.cdb", "cards.cdb"), (args.deck, "source.ydk")]:
        shutil.copy2(src, inputs / dst)
    with sqlite3.connect((inputs / "cards.cdb").as_uri() + "?mode=ro", uri=True) as db:
        names = dict(db.execute("select id,name from texts"))
    deck = deck_tools.read_deck(args.deck)
    result = {"format": "skystriker-native-v1", "coreSeed": None, "noShuffle": True,
              "noDeckCheck": True, "binarySha256": smoke.digest(root / "runtime" / args.bot_exe.name),
              "deckPermutationSeed": args.seed,
              "sourceHashes": {str(p.relative_to(repo)): smoke.digest(p) for p in
                  [Path(__file__).resolve()] + sorted((repo / "WindBot/Game/AI/Decks").glob("SkyStriker2026*.cs"))},
              "inputHashes": {p.name: smoke.digest(p) for p in inputs.iterdir()}, "scenarios": [], "mirror": []}
    original_launch = smoke.launch
    for name in chosen:
        opening, opponent, first = SCENARIOS[name]
        case = root / name
        shutil.copytree(inputs, case / "inputs")
        remaining = list(deck["main"])
        for code in opening: remaining.remove(code)
        if name == "lemn-grave":
            remaining.remove(HORNET)
            remaining.insert(0, HORNET)
        target = case / "inputs/target.ydk"
        other = case / "inputs/opponent.ydk"
        deck_tools.write_deck(target, opening + remaining, deck["extra"])
        deck_tools.write_deck(other, opponent, [])
        def launch(command, cwd, log):
            if first and "Name=SkynetQA" in command:
                command = ["Hand=3" if arg == "Hand=1" else arg for arg in command]
            return original_launch(command, cwd, log)
        smoke.launch = launch
        settings = SimpleNamespace(server=inputs / "ygoserver.dll", timeout=90,
            bot_exe=root / "runtime" / args.bot_exe.name, opponent_exe=root / "runtime" / args.bot_exe.name,
            policy="SkyStriker2026", opponent_policy="OldSchool" if name == "lemn-grave" else "SkyStriker2026", policy_seed=20261107,
            train=False, policy_group="skystriker-fixture:" + name, model=None,
            agent=None, opponent_agent=None, shadow=False, explain=False)
        duel = smoke.run_duel(settings, case, 1, target, other)
        trace = inspect(case / "duel-01/client-0.log", names, name, opening)
        row = {"name": name, "duel": duel, "trace": trace,
               "passed": duel["status"] == "passed" and trace["passed"]}
        result["scenarios"].append(row)
        result["passed"] = all(r["passed"] for r in result["scenarios"])
        (root / "result.json").write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
        print(name + ": " + ("PASS" if row["passed"] else "FAIL") + " " + "; ".join(
            c["name"] for c in trace["checks"] if not c["passed"]), flush=True)
    smoke.launch = original_launch
    mirror = root / "mirror"
    if args.mirror_games:
        shutil.copytree(inputs, mirror / "inputs")
    for number in range(1, args.mirror_games + 1):
        paths = []
        for side in range(2):
            cards = list(deck["main"])
            random.Random(args.seed + ((number - 1) // 2) * 2 + side).shuffle(cards)
            path = mirror / "inputs" / ("duel-%02d-side-%d.ydk" % (number, side))
            deck_tools.write_deck(path, cards, deck["extra"])
            paths.append(path)
        settings = SimpleNamespace(server=inputs / "ygoserver.dll", timeout=90,
            bot_exe=root / "runtime" / args.bot_exe.name, opponent_exe=root / "runtime" / args.bot_exe.name,
            policy="SkyStriker2026", opponent_policy="SkyStriker2026", policy_seed=args.seed,
            train=False, policy_group="skystriker-mirror", model=None,
            agent=None, opponent_agent=None, shadow=False, explain=False)
        duel = smoke.run_duel(settings, mirror, number, *paths)
        audits = [audit_mirror(mirror / ("duel-%02d" % number) / ("client-%d.log" % side), names)
                  for side in range(2)]
        target_log = (number - 1) % 2
        row = {"duel": duel, "deckHashes": [smoke.digest(p) for p in paths], "audits": audits,
               "designatedClientLog": "client-%d.log" % target_log,
               "designatedTurnOrder": audits[target_log]["turnOrder"],
               "turnOrderSource": "inferred from public turn-two draw, not lobby seat",
               "passed": duel["status"] == "passed" and all(a["passed"] for a in audits)}
        result["mirror"].append(row)
        result["passed"] = all(r["passed"] for r in result["scenarios"] + result["mirror"])
        (root / "result.json").write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
        print("mirror %d: %s" % (number, "PASS" if row["passed"] else "FAIL"), flush=True)
    return 0 if result["passed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
