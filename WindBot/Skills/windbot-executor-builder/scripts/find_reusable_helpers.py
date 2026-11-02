#!/usr/bin/env python3
"""Find reusable helper methods across WindBot executor files.

Examples:
  python find_reusable_helpers.py
  python find_reusable_helpers.py --min-occurrences 5 --top 80
  python find_reusable_helpers.py --contains Negated --with-lines
"""

from __future__ import annotations

import argparse
import json
import re
from dataclasses import dataclass
from pathlib import Path
from typing import Dict, List

METHOD_RE = re.compile(
    r"^\s*(public|private|protected|internal)\s+"
    r"(?:static\s+)?"
    r"[\w<>,\[\]\?\s]+\s+"
    r"(?P<name>\w+)\s*\("
)


@dataclass
class Occurrence:
    file: str
    line: int


def parse_methods(decks_dir: Path) -> Dict[str, List[Occurrence]]:
    methods: Dict[str, List[Occurrence]] = {}
    for file_path in sorted(decks_dir.glob("*.cs")):
        class_name = file_path.stem
        for i, raw in enumerate(file_path.read_text(encoding="utf-8", errors="ignore").splitlines(), start=1):
            match = METHOD_RE.match(raw)
            if not match:
                continue
            name = match.group("name")
            if name == class_name:
                continue
            methods.setdefault(name, []).append(Occurrence(file=file_path.name, line=i))
    return methods


def format_plain(rows: List[dict], with_lines: bool) -> str:
    lines: List[str] = []
    for row in rows:
        if with_lines:
            refs = ", ".join(f"{r['file']}:{r['line']}" for r in row["occurrences"])
        else:
            refs = ", ".join(sorted({r["file"] for r in row["occurrences"]}))
        lines.append(f"{row['count']:>3}x  {row['method']}  [{refs}]")
    return "\n".join(lines)


def main() -> int:
    parser = argparse.ArgumentParser(description="Scan duplicate helper methods in Game/AI/Decks executors.")
    parser.add_argument("--repo-root", default=".", help="Repository root path (default: current directory)")
    parser.add_argument("--decks-dir", default="Game/AI/Decks", help="Deck executor directory")
    parser.add_argument("--min-occurrences", type=int, default=3, help="Minimum count to include")
    parser.add_argument("--top", type=int, default=120, help="Maximum number of helper names to print")
    parser.add_argument("--contains", default="", help="Filter helper names containing this substring")
    parser.add_argument("--with-lines", action="store_true", help="Include line numbers in output")
    parser.add_argument("--json", action="store_true", help="Emit JSON output")
    args = parser.parse_args()

    repo_root = Path(args.repo_root).resolve()
    decks_dir = repo_root / args.decks_dir
    if not decks_dir.exists() or not decks_dir.is_dir():
        raise SystemExit(f"Deck directory not found: {decks_dir}")

    methods = parse_methods(decks_dir)

    rows: List[dict] = []
    for method, occs in methods.items():
        if len(occs) < args.min_occurrences:
            continue
        if args.contains and args.contains.lower() not in method.lower():
            continue
        rows.append(
            {
                "method": method,
                "count": len(occs),
                "occurrences": [{"file": o.file, "line": o.line} for o in occs],
            }
        )

    rows.sort(key=lambda r: (-r["count"], r["method"]))
    rows = rows[: max(args.top, 0)]

    if args.json:
        print(json.dumps(rows, indent=2))
    else:
        print(format_plain(rows, with_lines=args.with_lines))

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
