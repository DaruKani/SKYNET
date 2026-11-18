"""Validate and summarize opt-in WindBot structured duel traces."""

import argparse
import collections
import csv
import datetime as dt
import json
from pathlib import Path


SCHEMA = "windbot-public-observation-v1"
REPORT_FORMAT = "windbot-trace-analysis-v1"


def require(condition, message):
    if not condition:
        raise ValueError(message)


def trace_files(inputs):
    files = []
    for source in inputs:
        path = Path(source).resolve()
        require(path.exists(), "Trace input does not exist: " + str(path))
        if path.is_file():
            require(path.suffix.lower() == ".jsonl", "Trace input must be a .jsonl file: " + str(path))
            files.append(path)
        else:
            files.extend(sorted(item for item in path.rglob("*.jsonl") if item.is_file()))
    unique = sorted(set(files))
    require(unique, "No .jsonl trace files were found.")
    return unique


def read_capture(path):
    rows = []
    with path.open("r", encoding="utf-8-sig") as stream:
        for line_number, line in enumerate(stream, 1):
            if not line.strip():
                continue
            try:
                row = json.loads(line)
            except json.JSONDecodeError as error:
                raise ValueError("{}:{}: invalid JSON: {}".format(path, line_number, error.msg))
            require(row.get("schema") == SCHEMA,
                    "{}:{}: unsupported schema".format(path, line_number))
            require(row.get("sequence") == len(rows),
                    "{}:{}: non-contiguous sequence".format(path, line_number))
            rows.append(row)
    require(rows, "Trace is empty: " + str(path))
    duel_ids = {row.get("duelId") for row in rows}
    require(len(duel_ids) == 1 and next(iter(duel_ids)), "Trace has inconsistent duel IDs: " + str(path))
    require(rows[0].get("kind") == "start", "Trace does not begin with a start record: " + str(path))
    return rows


def selected_offer(row):
    selected_id = row.get("selectedOfferId")
    for offer in row.get("offers") or []:
        if offer.get("id") == selected_id:
            return offer
    return None


def observed_code(row, reference):
    if not reference:
        return None
    observation = row.get("observation") or {}
    for card in observation.get("cards") or []:
        if all(card.get(key) == reference.get(key) for key in ("controller", "location", "sequence")):
            return card.get("code") if card.get("known") else None
    return None


def record_kind(row):
    # TraceSession builds card-selection snapshots from the ordinary decision
    # snapshot helper. Builds before the writer fix left kind="decision" even
    # though the full selection contract and response were present. Normalize
    # those captures so completed historical runs remain auditable.
    if row.get("prompt") == "select_card" and row.get("selection") is not None:
        return "selection"
    return row.get("kind")


def summarize_capture(path, rows):
    start = rows[0]
    end = next((row for row in reversed(rows) if row.get("kind") == "end"), None)
    outcome = end.get("outcome") if end else None
    completed = bool(end and end.get("completed") and outcome in (0, 1, 2))
    unsupported = collections.Counter()
    action_types = collections.Counter()
    response_sources = collections.Counter()
    decision_sources = collections.Counter()
    decision_rows = []
    for row in rows:
        unsupported.update(row.get("unsupported") or [])
        source = row.get("responseSource")
        if source:
            response_sources[source] += 1
        decision_source = row.get("decisionSource")
        if decision_source:
            decision_sources[decision_source] += 1
        offer = selected_offer(row)
        if offer:
            action_types[offer.get("type") or "unknown"] += 1
        kind = record_kind(row)
        if kind not in ("decision", "selection"):
            continue
        global_state = (row.get("observation") or {}).get("global") or {}
        selected_codes = []
        candidates = ((row.get("selection") or {}).get("candidates") or [])
        candidate_by_id = {candidate.get("id"): candidate for candidate in candidates}
        for candidate_id in row.get("selectedCandidateIds") or []:
            candidate = candidate_by_id.get(candidate_id)
            if candidate and candidate.get("known"):
                selected_codes.append(candidate.get("code"))
        decision_rows.append({
            "file": str(path),
            "duelId": row.get("duelId"),
            "agent": row.get("agent"),
            "sequence": row.get("sequence"),
            "recordedAt": row.get("recordedAt"),
            "matchOrder": row.get("matchOrder"),
            "roomName": row.get("roomName"),
            "duelNumber": row.get("duelNumber"),
            "kind": kind,
            "prompt": row.get("prompt"),
            "turn": global_state.get("turn"),
            "phase": global_state.get("phase"),
            "selectedType": offer.get("type") if offer else None,
            "selectedCardCode": observed_code(row, offer.get("card")) if offer else None,
            "selectedCandidateCodes": ";".join(str(code) for code in selected_codes),
            "responseSource": source,
            "decisionSource": decision_source,
            "unsupported": ";".join(row.get("unsupported") or []),
            "outcome": outcome,
            "completed": completed,
        })
    return {
        "file": str(path),
        "duelId": rows[0].get("duelId"),
        "agent": rows[0].get("agent"),
        "startedAt": start.get("recordedAt"),
        "matchOrder": start.get("matchOrder"),
        "roomName": start.get("roomName"),
        "duelNumber": start.get("duelNumber"),
        "completed": completed,
        "outcome": outcome,
        "records": len(rows),
        "decisions": sum(record_kind(row) == "decision" for row in rows),
        "selections": sum(record_kind(row) == "selection" for row in rows),
        "actionTypes": dict(sorted(action_types.items())),
        "responseSources": dict(sorted(response_sources.items())),
        "decisionSources": dict(sorted(decision_sources.items())),
        "unsupported": dict(sorted(unsupported.items())),
        "rows": decision_rows,
    }


def merge_counts(target, values):
    for key, value in values.items():
        target[key] += value


def analyze(inputs):
    captures = [summarize_capture(path, read_capture(path)) for path in trace_files(inputs)]
    captures.sort(key=lambda capture: (
        capture["startedAt"] is None,
        capture["startedAt"] or "",
        capture["matchOrder"] is None,
        capture["matchOrder"] or 0,
        capture["file"],
    ))
    for start_order, capture in enumerate(captures, 1):
        capture["startOrder"] = start_order
        for row in capture["rows"]:
            row["startOrder"] = start_order
    action_types = collections.Counter()
    response_sources = collections.Counter()
    decision_sources = collections.Counter()
    unsupported = collections.Counter()
    outcomes = collections.Counter()
    agents = collections.Counter()
    for capture in captures:
        merge_counts(action_types, capture["actionTypes"])
        merge_counts(response_sources, capture["responseSources"])
        merge_counts(decision_sources, capture["decisionSources"])
        merge_counts(unsupported, capture["unsupported"])
        agents[capture["agent"] or "unknown"] += 1
        if capture["completed"]:
            outcomes[{0: "win", 1: "loss", 2: "draw"}[capture["outcome"]]] += 1
    return {
        "format": REPORT_FORMAT,
        "generatedAt": dt.datetime.now(dt.timezone.utc).isoformat(),
        "scope": "Observed correlations and protocol coverage only; this report does not prove that an action caused an outcome.",
        "summary": {
            "captures": len(captures),
            "completedCaptures": sum(capture["completed"] for capture in captures),
            "incompleteCaptures": sum(not capture["completed"] for capture in captures),
            "decisions": sum(capture["decisions"] for capture in captures),
            "selections": sum(capture["selections"] for capture in captures),
            "outcomes": dict(sorted(outcomes.items())),
            "agents": dict(sorted(agents.items())),
            "actionTypes": dict(sorted(action_types.items())),
            "responseSources": dict(sorted(response_sources.items())),
            "decisionSources": dict(sorted(decision_sources.items())),
            "unsupported": dict(sorted(unsupported.items())),
        },
        "captures": [{key: value for key, value in capture.items() if key != "rows"} for capture in captures],
        "decisionRows": [row for capture in captures for row in capture["rows"]],
    }


def write_csv(path, rows):
    columns = [
        "file", "duelId", "agent", "startOrder", "matchOrder", "roomName", "duelNumber",
        "recordedAt", "sequence", "kind", "prompt", "turn", "phase",
        "selectedType", "selectedCardCode", "selectedCandidateCodes", "responseSource", "decisionSource",
        "unsupported", "outcome", "completed",
    ]
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8-sig", newline="") as stream:
        writer = csv.DictWriter(stream, fieldnames=columns)
        writer.writeheader()
        writer.writerows(rows)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--traces", action="append", required=True,
                        help="Trace .jsonl file or directory; repeat for multiple inputs")
    parser.add_argument("--output", required=True, type=Path, help="JSON analysis report")
    parser.add_argument("--decisions-csv", type=Path, help="Optional flat decision/selection CSV")
    args = parser.parse_args()
    try:
        report = analyze(args.traces)
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        if args.decisions_csv:
            write_csv(args.decisions_csv, report["decisionRows"])
        summary = report["summary"]
        print("captures={} completed={} decisions={} selections={} fallbacks={}".format(
            summary["captures"], summary["completedCaptures"], summary["decisions"],
            summary["selections"], summary["responseSources"].get("legal-fallback", 0)))
    except (OSError, ValueError) as error:
        parser.error(str(error))


if __name__ == "__main__":
    main()
