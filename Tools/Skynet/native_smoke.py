"""Run bounded real ocgcore duels with isolated Skynet/WindBot processes.

This checks protocol/gameplay completion, not competitive playing strength.
The default forty-copy vanilla decks deliberately require no-deck-check mode.
The native server RNG seed is unavailable through start_server; PolicySeed only
controls the policy, and results.csv leaves the duel seed empty.
"""
import argparse
import csv
import ctypes
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import time
import uuid


def digest(path):
    with path.open("rb") as source:
        return hashlib.file_digest(source, "sha256").hexdigest()


def launch(command, cwd, log):
    options = {"creationflags": subprocess.CREATE_NO_WINDOW} if os.name == "nt" else {}
    return subprocess.Popen(command, cwd=cwd, stdout=log, stderr=subprocess.STDOUT, **options)


def retire(process):
    if process is None or process.poll() is not None:
        return False
    process.terminate()
    try:
        process.wait(timeout=5)
    except subprocess.TimeoutExpired:
        process.kill()
        process.wait(timeout=5)
    return True


def stage_bot(executable, directory):
    """Private cwd prevents legacy logs/configuration from touching installations."""
    directory.mkdir()
    dialogs = executable.parent / "Dialogs"
    if dialogs.is_dir():
        shutil.copytree(dialogs, directory / "Dialogs")


def write_fixture(path, code):
    path.write_text("#created by Skynet isolated smoke\n#main\n"
                    + (str(code) + "\n") * 40 + "#extra\n!side\n", encoding="utf-8")


def find_port(path):
    if path.exists():
        for line in path.read_text(encoding="utf-8", errors="replace").splitlines():
            if line.strip().isdigit():
                value = int(line.strip())
                if 0 < value <= 65535:
                    return value
    return None


def run_duel(args, root, number, deck, opponent_deck):
    directory = root / ("duel-%02d" % number)
    directory.mkdir()
    server_dir = directory / "server"
    (server_dir / "Data" / "locales" / "zh-CN").mkdir(parents=True)
    shutil.copy2(root / "inputs" / "script.zip", server_dir / "Data" / "script.zip")
    shutil.copy2(root / "inputs" / "cards.cdb", server_dir / "cards.cdb")
    target_seat = (number - 1) % 2
    row = {"duel": number, "matchId": str(uuid.uuid4()), "targetSeat": target_seat, "policySeed": args.policy_seed + number,
           "agent": args.agent, "opponentAgent": args.opponent_agent,
           "coreSeed": None, "noDeckCheck": True, "noShuffle": True, "status": "failed"}
    processes = []
    logs = []
    server = None
    try:
        server_log_path = directory / "server.log"
        server_log = server_log_path.open("w", encoding="utf-8")
        logs.append(server_log)
        server = launch([sys.executable, str(Path(__file__).resolve()), "--server-child",
                         "--server", str(args.server), "--server-args",
                         "0 0 0 0 5 T T 8000 5 1 0 0"], server_dir, server_log)
        deadline = time.monotonic() + args.timeout
        port = None
        while time.monotonic() < deadline and server.poll() is None:
            port = find_port(server_log_path)
            if port is not None:
                break
            time.sleep(.05)
        if port is None:
            raise RuntimeError("Native server did not announce a listening port")
        row["port"] = port
        # Launch first seat before second. Both clients still establish their
        # authoritative seat from STOC_TYPE_CHANGE, never from this intention.
        for seat in range(2):
            target = seat == target_seat
            executable = args.bot_exe if target else args.opponent_exe
            policy = args.policy if target else args.opponent_policy
            name = "SkynetQA" if target else "OpponentQA"
            bot_dir = directory / ("client-%d" % seat)
            stage_bot(executable, bot_dir)
            log_path = directory / ("client-%d.log" % seat)
            output = log_path.open("w", encoding="utf-8")
            logs.append(output)
            command = [str(executable), "Name=" + name, "Deck=" + policy,
                       "DeckFile=" + str(deck if target else opponent_deck),
                       "DbPath=" + str(root / "inputs" / "cards.cdb"),
                       "Host=127.0.0.1", "Port=" + str(port), "Version=4962",
                       "Hand=" + str(seat + 1), "Chat=false", "Debug=true",
                       "PolicySeed=" + str(args.policy_seed + number),
                       "PolicyTraining=" + str(args.train and target),
                       "PolicyData=" + str(bot_dir / "learning"),
                       "PolicyGroup=" + args.policy_group]
            if target and args.model:
                command.append("PolicyModel=" + str(args.model))
            agent = args.agent if target else args.opponent_agent
            if agent:
                command.append("Agent=" + agent)
            if args.shadow:
                command.append("ShadowData=" + str(bot_dir / "shadow"))
            if args.explain and target:
                command.append("Explain=True")
            process = launch(command, bot_dir, output)
            processes.append((process, log_path, target))
            # Server accepts each client before we launch the next; waiting for
            # the real joined log also avoids assigning seats from a TCP probe.
            if seat == 0:
                admitted = time.monotonic() + min(10, args.timeout)
                while process.poll() is None and time.monotonic() < admitted:
                    text = log_path.read_text(encoding="utf-8", errors="replace")
                    if "SKYNET_SEAT " in text or "started." in text:
                        break
                    time.sleep(.05)
        while time.monotonic() < deadline and any(p.poll() is None for p, _, _ in processes):
            time.sleep(.1)
        if any(p.poll() is None for p, _, _ in processes):
            raise TimeoutError("Duel exceeded %s seconds" % args.timeout)
        results = []
        for process, path, target in processes:
            text = path.read_text(encoding="utf-8", errors="replace")
            markers = re.findall(r"SKYNET_RESULT\s+result=([012])", text)
            matches = re.findall(r"Duel finished against .+?, result: (Win|Lose|Draw)", text)
            started = "SKYNET_START" in text or "Duel started:" in text
            if process.returncode != 0 or not started or (len(markers) != 1 and len(matches) != 1):
                raise RuntimeError("Client did not complete exactly one real duel: " + path.name)
            if markers and "SKYNET_END" not in text:
                raise RuntimeError("Skynet did not receive authoritative DuelEnd: " + path.name)
            if re.search(r"Tick Error|Run Error|Unhandled Exception|Duel retry", text, re.I):
                raise RuntimeError("Client reported a protocol/runtime error: " + path.name)
            result = ({"0": "win", "1": "loss", "2": "draw"}[markers[0]] if markers
                      else {"Win": "win", "Lose": "loss", "Draw": "draw"}[matches[0]])
            results.append(result)
            if target:
                row["result"] = result
                seats = re.findall(r"SKYNET_SEAT\s+seat=(\d+)", text)
                if seats:
                    row["targetSeat"] = int(seats[-1])
                    row["seatSource"] = "authoritative STOC_TYPE_CHANGE"
                else:
                    row["targetSeat"] = None
                    row["seatSource"] = "unavailable in legacy client"
        if sorted(results) not in (["loss", "win"], ["draw", "draw"]):
            raise RuntimeError("Client terminal results disagree: " + repr(results))
        try:
            server.wait(timeout=8)
        except subprocess.TimeoutExpired as exception:
            raise RuntimeError("Native server did not retire after both clients exited") from exception
        if server.returncode != 0:
            raise RuntimeError("Native server exited with " + str(server.returncode))
        row["status"] = "passed"
    except Exception as exception:
        row["error"] = str(exception)
    finally:
        forced = [str(path.name) for process, path, _ in processes if retire(process)]
        if retire(server):
            forced.append("server")
        row["forcedCleanup"] = forced
        row["exitCodes"] = [process.poll() for process, _, _ in processes]
        row["serverExitCode"] = server.poll() if server else None
        for output in logs:
            output.close()
        (directory / "result.json").write_text(json.dumps(row, indent=2) + "\n", encoding="utf-8")
    return row


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--server", type=Path, required=True)
    parser.add_argument("--server-child", action="store_true", help=argparse.SUPPRESS)
    parser.add_argument("--server-args", help=argparse.SUPPRESS)
    parser.add_argument("--data-root", type=Path)
    parser.add_argument("--bot-exe", type=Path)
    parser.add_argument("--opponent-exe", type=Path)
    parser.add_argument("--policy", default="SKYNET")
    parser.add_argument("--opponent-policy", default="SKYNET")
    parser.add_argument("--agent", choices=("skynet", "expert-oldschool"),
                        help="Explicit SKYNET runtime mode; omitted for unchanged legacy compatibility")
    parser.add_argument("--opponent-agent", choices=("skynet", "expert-oldschool"))
    parser.add_argument("--deck", type=Path)
    parser.add_argument("--opponent-deck", type=Path)
    parser.add_argument("--model", type=Path)
    parser.add_argument("--model-label", default="baseline")
    parser.add_argument("--opponent-label", default="baseline")
    parser.add_argument("--policy-seed", type=int, default=20261020)
    parser.add_argument("--policy-group", help="Stable deck/encounter cohort; defaults to both deck hashes")
    parser.add_argument("--train", action="store_true")
    parser.add_argument("--shadow", action="store_true",
                        help="Capture bounded public main-phase observations from both SKYNET clients")
    parser.add_argument("--explain", action="store_true",
                        help="Print bounded target-policy action explanations in its client log")
    parser.add_argument("--games", type=int, default=2)
    parser.add_argument("--timeout", type=float, default=90)
    parser.add_argument("--output-dir", type=Path)
    args = parser.parse_args()
    args.server = args.server.resolve(strict=True)
    if args.server_child:
        native = ctypes.CDLL(str(args.server))
        native.start_server.argtypes = [ctypes.c_char_p]
        native.start_server.restype = ctypes.c_int
        return native.start_server(args.server_args.encode("ascii"))
    if not args.data_root or not args.bot_exe or not args.output_dir:
        parser.error("--data-root, --bot-exe and --output-dir are required")
    if not 1 <= args.games <= 100 or not 10 <= args.timeout <= 600:
        parser.error("Use 1..100 games and a 10..600 second per-duel timeout")
    if args.shadow and (not args.agent or not args.opponent_agent):
        parser.error("--shadow requires explicit --agent and --opponent-agent SKYNET policies")
    args.bot_exe = args.bot_exe.resolve(strict=True)
    args.opponent_exe = (args.opponent_exe or args.bot_exe).resolve(strict=True)
    args.data_root = args.data_root.resolve(strict=True)
    if args.model:
        args.model = args.model.resolve(strict=True)
    root = args.output_dir.resolve()
    root.mkdir(parents=True, exist_ok=False)
    inputs = root / "inputs"
    inputs.mkdir()
    shutil.copy2(args.data_root / "script.zip", inputs / "script.zip")
    shutil.copy2(args.data_root / "locales" / "en-US" / "cards.cdb", inputs / "cards.cdb")
    deck = args.deck.resolve(strict=True) if args.deck else inputs / "target.ydk"
    opponent_deck = args.opponent_deck.resolve(strict=True) if args.opponent_deck else inputs / "opponent.ydk"
    if not args.deck:
        write_fixture(deck, 69247929)  # Gene-Warped Warwolf: normal Level 4.
    if not args.opponent_deck:
        write_fixture(opponent_deck, 5053103)  # Battle Ox: normal Level 4.
    if not args.policy_group:
        args.policy_group = "native:" + digest(deck)[:16] + ":" + digest(opponent_deck)[:16]
    receipt = {"purpose": "Real native protocol/gameplay smoke; not a strength benchmark",
               "server": str(args.server), "serverSha256": digest(args.server),
               "botSha256": digest(args.bot_exe), "opponentSha256": digest(args.opponent_exe),
               "databaseSha256": digest(inputs / "cards.cdb"), "scriptsSha256": digest(inputs / "script.zip"),
               "deckSha256": digest(deck), "opponentDeckSha256": digest(opponent_deck),
               "coreSeed": None, "policy": args.policy, "opponentPolicy": args.opponent_policy,
               "shadowCapture": args.shadow,
               "explainDecisions": args.explain,
               "policyGroup": args.policy_group,
               "duels": []}
    for number in range(1, args.games + 1):
        row = run_duel(args, root, number, deck, opponent_deck)
        receipt["duels"].append(row)
        (root / "result.json").write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")
        print("Duel %d: %s %s" % (number, row["status"], row.get("result", row.get("error", ""))), flush=True)
        if row["status"] != "passed":
            break
    with (root / "results.csv").open("w", newline="", encoding="utf-8") as output:
        writer = csv.DictWriter(output, fieldnames=["model", "opponent", "seed", "seat", "matchId", "result"])
        writer.writeheader()
        for row in receipt["duels"]:
            if row["status"] == "passed":
                writer.writerow({"model": args.model_label, "opponent": args.opponent_label, "seed": "",
                                 "matchId": row["matchId"],
                                 "seat": row["targetSeat"], "result": row["result"]})
    return 0 if len(receipt["duels"]) == args.games and all(
        row["status"] == "passed" for row in receipt["duels"]) else 1


if __name__ == "__main__":
    raise SystemExit(main())
