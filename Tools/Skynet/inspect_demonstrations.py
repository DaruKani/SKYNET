"""Audit explicitly selected duel captures before using them as teaching data.

This checks card-selection wire labels and completion, not expert optimality or
complete neural observations. It does not train, import a model, or run an agent.
"""
import argparse
from collections import Counter
import json
from pathlib import Path

from ygo_agent_shadow import SCHEMA, boolean, integer, read_capture, require


def selection_label(record):
    selection = record["selection"]
    minimum = integer(selection["min"], "selection minimum", 0, 255)
    maximum = integer(selection["max"], "selection maximum", minimum, 255)
    cancelable = boolean(selection["cancelable"], "cancelable")
    integer(selection["hint"], "selection hint", -2147483648)
    candidates = selection["candidates"]
    require(isinstance(candidates, list) and len(candidates) <= 255, "Invalid candidate list")
    ids = []
    known_count = 0
    for candidate in candidates:
        ids.append(integer(candidate["id"], "candidate ID", 0, 254))
        known = boolean(candidate["known"], "candidate visibility")
        code = integer(candidate["code"], "candidate code")
        card = candidate["card"]
        controller = integer(card["controller"], "candidate controller", 0, 1)
        location = integer(card["location"], "candidate location", 1, 255)
        integer(card["sequence"], "prompt sequence", 0, 255)
        overlay = integer(card["overlaySequence"], "overlay sequence", -1, 255)
        position = integer(candidate["position"], "candidate position", 0, 15)
        require((location & ~128) in (1, 2, 4, 8, 16, 32, 64), "Invalid candidate location")
        require(bool(location & 128) == (overlay >= 0), "Overlay reference mismatch")
        require(not (location in (1, 2) or overlay >= 0) or position == 0, "Private position")
        visible = controller == 0 or (
            overlay < 0 and location not in (1, 2) and (location == 16 or position & 5)
        )
        require((known and visible and code > 0) or (not known and code == 0),
                "Hidden candidate identity or inconsistent visibility")
        known_count += int(known)
    require(ids == list(range(len(candidates))), "Candidate IDs must preserve raw wire order")

    chain = selection["chain"]
    require(isinstance(chain, list) and len(chain) <= 512, "Invalid chain")
    integer(selection["solvingChainIndex"], "solving chain index", 0, len(chain))
    for index, link in enumerate(chain, 1):
        require(integer(link["index"], "chain index", 1, 512) == index, "Chain order mismatch")
        integer(link["activatePlayer"], "activation player", 0, 1)
        code = integer(link["code"], "activation code")
        description = integer(link["description"], "activation description", -1)
        require(code > 0 or description == -1, "Unknown chain source leaks description")
        reference = link["activation"]
        integer(reference["controller"], "activation controller", 0, 1)
        integer(reference["location"], "activation location", 0, 255)
        integer(reference["sequence"], "activation sequence", -1, 255)

    response = record["responseBytes"]
    chosen = record["selectedCandidateIds"]
    require(isinstance(response, list) and 1 <= len(response) <= 256, "Invalid response bytes")
    require(isinstance(chosen, list), "Invalid chosen IDs")
    for value in response:
        integer(value, "response byte", 0, 255)
    for value in chosen:
        integer(value, "chosen candidate", 0, 254)
    source = record["responseSource"]
    require(source in ("policy", "legal-fallback", "automatic-field-first-cancel"),
            "Unknown response source")
    cancelled = boolean(record.get("cancelled", False), "cancelled")
    if cancelled:
        require(cancelable and not chosen and response == [255] * 4, "Invalid cancellation")
    else:
        require(source != "automatic-field-first-cancel", "Automatic cancellation mismatch")
        require(minimum <= len(chosen) <= maximum, "Selection count violates prompt")
        require(len(chosen) == len(set(chosen)) and all(i in ids for i in chosen),
                "Selection contains duplicate or unoffered candidates")
        require(response == [len(chosen)] + chosen, "Wire bytes disagree with chosen candidates")
    return {"source": source, "cancelled": cancelled, "knownCandidates": known_count,
            "unknownCandidates": len(ids) - known_count}


def inspect_records(records):
    require(records and records[0].get("kind") == "start", "Capture must begin with start")
    duel_id, agent = records[0].get("duelId"), records[0].get("agent")
    require(isinstance(duel_id, str) and 0 < len(duel_id) <= 64, "Invalid duel identity")
    require(isinstance(agent, str) and 0 < len(agent) <= 80, "Invalid agent identity")
    kinds, prompts, sources = Counter(), Counter(), Counter()
    cancelled = known = unknown = 0
    outcome = None
    unsupported = Counter()
    saw_end = False
    for index, record in enumerate(records):
        require(not saw_end, "Data after terminal end")
        require(record.get("schema") == SCHEMA
                and integer(record.get("sequence"), "record sequence", 0, 4095) == index,
                "Schema or sequence mismatch")
        require(record.get("duelId") == duel_id and record.get("agent") == agent, "Mixed duel identity")
        kind = record.get("kind")
        require(kind in ("start", "decision", "outcome", "end", "unsupported-prompt"), "Unknown record kind")
        require(kind != "start" or index == 0, "Duplicate start")
        kinds[kind] += 1
        if kind in ("decision", "unsupported-prompt"):
            require(outcome is None, "Decision after authoritative outcome")
            prompt = record.get("prompt")
            require(isinstance(prompt, str) and len(prompt) <= 80, "Invalid prompt")
            prompts[prompt] += 1
            reasons = record.get("unsupported", [])
            require(isinstance(reasons, list) and len(reasons) <= 64
                    and all(isinstance(x, str) and len(x) <= 160 for x in reasons), "Invalid limitations")
            unsupported.update(reasons)
            if kind == "decision" and prompt == "select_card":
                require("invalid-card-selection-response" not in reasons, "Invalid captured selection")
                label = selection_label(record)
                sources[label["source"]] += 1
                cancelled += int(label["cancelled"])
                known += label["knownCandidates"]
                unknown += label["unknownCandidates"]
        elif kind == "outcome":
            require(outcome is None, "Duplicate outcome")
            outcome = integer(record.get("outcome"), "outcome", 0, 2)
        elif kind == "end":
            saw_end = True
            complete = boolean(record.get("completed"), "completed")
            terminal_outcome = record.get("outcome")
            if terminal_outcome is not None:
                integer(terminal_outcome, "terminal outcome", 0, 2)
            require(terminal_outcome == outcome, "Terminal outcome mismatch")
            require(not complete or outcome is not None, "Completed capture lacks outcome")
    complete = saw_end and records[-1].get("completed", False) and outcome is not None
    return {
        "duelId": duel_id, "agent": agent, "completed": complete, "outcome": outcome,
        "recordKinds": dict(kinds), "prompts": dict(prompts), "selectionSources": dict(sources),
        "auditedSelections": sum(sources.values()), "cancelledSelections": cancelled,
        "knownCandidateMentions": known, "unknownCandidateMentions": unknown,
        "completedPolicySelections": sources["policy"] if complete else 0,
        "observationLimitations": dict(unsupported),
        "readyForNeuralTraining": False,
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--capture", required=True, action="append", type=Path,
                        help="Explicit JSONL file; repeat up to 32 times.")
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()
    try:
        require(len(args.capture) <= 32, "At most 32 explicit captures per audit")
        results, identities = [], set()
        for path in args.capture:
            records, digest = read_capture(path)
            result = inspect_records(records)
            require(result["duelId"] not in identities, "Duplicate duel capture")
            identities.add(result["duelId"])
            results.append({"path": str(path), "sha256": digest, **result})
        report = {
            "format": "skynet-demonstration-audit-v1",
            "captures": results,
            "completedCaptures": sum(x["completed"] for x in results),
            "auditedSelections": sum(x["auditedSelections"] for x in results),
            "completedPolicySelections": sum(x["completedPolicySelections"] for x in results),
            "readyForNeuralTraining": False,
            "limitations": [
                "Wire-valid labels are not proof of expert quality or complete observations.",
                "Fallback and automatic responses are separated from policy choices.",
                "Incomplete captures contribute no completed policy selections.",
                "Only ordinary select_card labels are audited here; other prompts still need capture and mapping.",
                "Counter, relation and complete history encoding remain prerequisites for neural training.",
            ],
        }
        with args.output.open("x", encoding="utf-8") as stream:
            json.dump(report, stream, indent=2, ensure_ascii=False)
            stream.write("\n")
        print("Audited %d selections in %d completed client captures. No model trained." %
              (report["auditedSelections"], report["completedCaptures"]))
    except (ValueError, OSError, KeyError, TypeError, json.JSONDecodeError) as error:
        parser.exit(2, "SKYNET demonstration audit failed: " + str(error) + "\n")


if __name__ == "__main__":
    main()
