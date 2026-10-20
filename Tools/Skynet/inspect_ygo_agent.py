"""Inspect a selected YGO Agent source snapshot and deck vocabulary without running it.

Vocabulary membership is only a prerequisite for upstream model experiments. It
does not establish card semantics, checkpoint compatibility or playing strength.
Only fixed source files and explicitly supplied decks are read. No package,
checkpoint, pickle, card database, archive or source module is loaded.
"""
import argparse
import ast
from collections import Counter
import hashlib
import json
from pathlib import Path
import re


MAX_SOURCE_BYTES = 2 * 1024 * 1024
MAX_DECK_BYTES = 256 * 1024
MAX_VOCABULARY = 65535
MAX_DECK_CARDS = 512
MAX_DECKS = 32
FEATURE_PATH = Path("ygoinf/ygoinf/features.py")
CODE_LIST_PATH = Path("scripts/code_list.txt")
LIMIT_NAMES = (
    "MAX_CARDS", "MAX_ACTIONS", "N_CARD_FEATURES", "N_ACTION_FEATURES",
    "N_GLOBAL_FEATURES", "N_HISTORY_ACTIONS", "H_ACTIONS_FEATS",
    "N_RNN_CHANNELS",
)
CARD_CODE = re.compile(r"[0-9]{1,10}\Z")


def bounded_read(path, limit):
    path = Path(path)
    if path.is_symlink() or not path.is_file():
        raise ValueError("Select a regular file, not a symlink: " + str(path))
    if path.stat().st_size > limit:
        raise ValueError("File exceeds the read limit: " + str(path))
    with path.open("rb") as stream:
        raw = stream.read(limit + 1)
    if len(raw) > limit:
        raise ValueError("File grew beyond the read limit: " + str(path))
    return raw


def source_read(root, relative):
    path = root / relative
    resolved = path.resolve(strict=True)
    if not resolved.is_relative_to(root):
        raise ValueError("Source file escapes the explicitly selected root.")
    return bounded_read(path, MAX_SOURCE_BYTES)


def card_code(text, context):
    if not CARD_CODE.fullmatch(text):
        raise ValueError("Invalid card code in " + context + ": " + text[:40])
    code = int(text)
    if not 0 < code <= 0xffffffff:
        raise ValueError("Card codes must be positive uint32 values in " + context)
    return code


def parse_vocabulary(raw):
    codes = []
    seen = set()
    columns = None
    script_flags = {}
    for number, line in enumerate(raw.decode("utf-8-sig").splitlines(), 1):
        value = line.strip()
        if not value:
            continue
        fields = value.split()
        if len(fields) not in (1, 2) or (columns is not None and columns != len(fields)):
            raise ValueError("Expected a consistent one- or two-column code list.")
        columns = len(fields)
        if columns == 2 and fields[1] not in ("0", "1"):
            raise ValueError("Expected a 0/1 script-presence flag.")
        code = card_code(fields[0], "code list line " + str(number))
        if code in seen:
            raise ValueError("Duplicate vocabulary code would change upstream indices: " + value)
        seen.add(code)
        codes.append(code)
        if columns == 2:
            script_flags[code] = fields[1] == "1"
        if len(codes) > MAX_VOCABULARY:
            raise ValueError("Vocabulary exceeds the uint16 index limit.")
    if not codes:
        raise ValueError("The selected vocabulary is empty.")
    return {"codes": codes, "columns": columns, "scriptFlags": script_flags}


def parse_deck(raw):
    sections = {"main": [], "extra": [], "side": []}
    section = None
    total = 0
    for number, line in enumerate(raw.decode("utf-8-sig").splitlines(), 1):
        value = line.strip()
        if not value:
            continue
        if value in ("#main", "#extra", "!side"):
            section = value[1:]
            continue
        if value.startswith("#"):
            continue
        if value.startswith("!") or section is None:
            raise ValueError("Unexpected YDK entry at line " + str(number))
        sections[section].append(card_code(value, "YDK line " + str(number)))
        total += 1
        if total > MAX_DECK_CARDS:
            raise ValueError("Deck exceeds the 512-card inspection limit.")
    if not sections["main"]:
        raise ValueError("A deck must contain a nonempty #main section.")
    return sections


def inspect_features(raw):
    # Parse syntax only. Never import or evaluate the upstream module.
    tree = ast.parse(raw.decode("utf-8-sig"), filename=FEATURE_PATH.as_posix())
    limits = {}
    prompts = {}
    loader_reads_whole_line_as_int = False
    for statement in tree.body:
        if isinstance(statement, ast.FunctionDef) and statement.name == "init_code_list":
            loader_reads_whole_line_as_int = any(
                isinstance(node, ast.Call) and isinstance(node.func, ast.Name)
                and node.func.id == "int" and len(node.args) == 1
                and isinstance(node.args[0], ast.Name) and node.args[0].id == "line"
                for node in ast.walk(statement)
            )
        if not isinstance(statement, ast.Assign) or len(statement.targets) != 1:
            continue
        target = statement.targets[0]
        if not isinstance(target, ast.Name):
            continue
        if target.id in LIMIT_NAMES:
            value = statement.value
            if not isinstance(value, ast.Constant) or type(value.value) is not int:
                raise ValueError("Expected a literal source limit: " + target.id)
            if not 0 < value.value <= 65535:
                raise ValueError("Invalid source limit: " + target.id)
            if target.id in limits:
                raise ValueError("Repeated source limit: " + target.id)
            limits[target.id] = value.value
        elif target.id == "msg_to_id":
            if prompts or not isinstance(statement.value, ast.Dict):
                raise ValueError("Expected one literal prompt-ID mapping.")
            for key, value in zip(statement.value.keys, statement.value.values):
                if not (isinstance(key, ast.Attribute)
                        and isinstance(key.value, ast.Name) and key.value.id == "MsgName"
                        and isinstance(value, ast.Constant) and type(value.value) is int):
                    raise ValueError("Unrecognized prompt-ID mapping.")
                if key.attr in prompts or value.value in prompts.values():
                    raise ValueError("Duplicate prompt-ID mapping.")
                prompts[key.attr] = value.value
    if set(limits) != set(LIMIT_NAMES) or not prompts:
        raise ValueError("Source feature layout is unrecognized; inspect this revision manually.")
    return {
        "declaredLimits": limits,
        "declaredPromptIds": prompts,
        "loaderReadsWholeLineAsInteger": loader_reads_whole_line_as_int,
        "analysis": "Literal declarations only; prompt implementations were not executed or validated.",
    }


def inspect_deck(path, vocabulary):
    raw = bounded_read(path, MAX_DECK_BYTES)
    sections = parse_deck(raw)
    by_section = {}
    combined = Counter()
    for name, codes in sections.items():
        counts = Counter(codes)
        combined.update(counts)
        by_section[name] = {
            "copies": len(codes),
            "uniqueCards": len(counts),
            "vocabularyCopies": sum(count for code, count in counts.items() if code in vocabulary),
            "missingCodes": sorted(set(counts) - vocabulary),
        }
    return {
        "deck": str(Path(path).resolve()),
        "sha256": hashlib.sha256(raw).hexdigest(),
        "sections": by_section,
        "uniqueCards": len(combined),
        "vocabularyUniqueCards": len(set(combined) & vocabulary),
        "allDeckCodesInVocabulary": set(combined).issubset(vocabulary),
        "missing": [
            {"code": code, "copies": combined[code]}
            for code in sorted(set(combined) - vocabulary)
        ],
        "checkpointCompatibility": "not-tested",
        "runtimeSupportImplied": False,
    }


def inspect(source_root, deck_paths):
    if not 1 <= len(deck_paths) <= MAX_DECKS:
        raise ValueError("Select between 1 and 32 deck files explicitly.")
    root = Path(source_root).resolve(strict=True)
    if not root.is_dir():
        raise ValueError("Source root must be a directory.")
    code_raw = source_read(root, CODE_LIST_PATH)
    feature_raw = source_read(root, FEATURE_PATH)
    vocabulary = parse_vocabulary(code_raw)
    codes = vocabulary["codes"]
    features = inspect_features(feature_raw)
    source_mismatches = []
    if vocabulary["columns"] == 2 and features["loaderReadsWholeLineAsInteger"]:
        source_mismatches.append(
            "The supplied code list has two columns (code, script flag), but this inference "
            "loader parses each whole line as one integer. These files cannot be used together "
            "unchanged. Preserve exact checkpoint vocabulary order when resolving the mismatch."
        )
    return {
        "format": "skynet-ygo-agent-inspection-v1",
        "sourceRoot": str(root),
        "mode": "static-source-and-vocabulary-only",
        "sourceFiles": [
            {"path": CODE_LIST_PATH.as_posix(), "sha256": hashlib.sha256(code_raw).hexdigest()},
            {"path": FEATURE_PATH.as_posix(), "sha256": hashlib.sha256(feature_raw).hexdigest()},
        ],
        "vocabularyCards": len(codes),
        "vocabularyColumns": vocabulary["columns"],
        "sourceScriptFlaggedCards": (sum(vocabulary["scriptFlags"].values())
                                    if vocabulary["columns"] == 2 else None),
        "scriptFlagsAreNotRuntimeVerification": True,
        "vocabularyOrderMatters": True,
        "upstreamInference": features,
        "sourceCompatibilityFindings": source_mismatches,
        "decks": [inspect_deck(path, set(codes)) for path in deck_paths],
        "requiredBeforeInference": [
            "Match a trusted checkpoint to its exact ordered vocabulary and feature layout.",
            "Verify local/public-only observations, including unknown-card property redaction.",
            "Map each supported prompt to current server offers and validate every returned response.",
            "Reject excess actions/cards explicitly instead of silently dropping offered choices.",
            "Own recurrent state per duel with deadlines, bounded requests and explicit teardown.",
            "Run frozen real-duel evaluation separately from any training.",
        ],
        "limitations": [
            "Vocabulary membership is not implemented SKYNET card semantics.",
            "Neither upstream code, checkpoints nor embeddings were loaded or run.",
            "A card in the vocabulary may still encounter unsupported prompts or weak play.",
            "SKYNET's nine-feature linear checkpoints are incompatible with this recurrent model.",
        ],
    }


def write_report(report, output, workspace):
    root = Path(workspace).resolve(strict=True)
    path = Path(output).resolve()
    if not path.is_relative_to(root) or path.suffix.lower() != ".json":
        raise ValueError("Report output must be a .json file inside the selected workspace.")
    if path.exists():
        raise ValueError("Report already exists; choose a new output file.")
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("x", encoding="utf-8", newline="\n") as stream:
        json.dump(report, stream, indent=2)
        stream.write("\n")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source-root", required=True, type=Path)
    parser.add_argument("--deck", required=True, action="append", type=Path)
    parser.add_argument("--workspace-root", type=Path, default=Path.cwd())
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    try:
        report = inspect(args.source_root, args.deck)
        if args.output is None:
            print(json.dumps(report, indent=2))
        else:
            write_report(report, args.output, args.workspace_root)
            print("Inspected %d vocabulary entries and %d decks without loading upstream code."
                  % (report["vocabularyCards"], len(report["decks"])))
            for deck in report["decks"]:
                print("%s: %d/%d unique cards in the vocabulary; runtime compatibility untested."
                      % (Path(deck["deck"]).name, deck["vocabularyUniqueCards"], deck["uniqueCards"]))
            for finding in report["sourceCompatibilityFindings"]:
                print("Source compatibility: " + finding)
    except (OSError, ValueError, UnicodeError, SyntaxError, RecursionError) as error:
        parser.exit(2, "SKYNET YGO Agent inspection failed: " + str(error) + "\n")


if __name__ == "__main__":
    main()
