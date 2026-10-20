"""Index explicitly selected executor sources without compiling or executing them.

This is a lexical migration inventory, not a C# parser, effect importer or list of
cards supported by SKYNET. Registration excerpts preserve source order. A reviewer
must follow helper dependencies, prompt routing, state resets and matching Lua.
No scripts, archives, parent directories or sibling repositories are discovered.
"""
import argparse
import hashlib
import json
from pathlib import Path
import re


MAX_FILES = 256
MAX_SOURCE_BYTES = 2 * 1024 * 1024
# Preserve strings and line numbers while hiding comments from lexical searches.
TOKENS = re.compile(
    r'@"(?:""|[^"])*"|"(?:\\.|[^"\\])*"|'
    r"'(?:\\.|[^'\\])*'|//[^\r\n]*|/\*[\s\S]*?\*/"
)
REGISTRATION = re.compile(r"^[ \t]*AddExecutor\s*\(.*$", re.MULTILINE)
DECK = re.compile(r'^[ \t]*\[Deck\s*\(.*$', re.MULTILINE)
HOOK = re.compile(
    r"^[ \t]*(?:public|protected)\s+override\s+"
    r"[\w<>, \[\]?]+\s+(On\w+)\s*\(", re.MULTILINE
)


def without_comments(source):
    def replace(match):
        value = match.group()
        if value.startswith(("//", "/*")):
            return "".join(char if char in "\r\n" else " " for char in value)
        return value
    return TOKENS.sub(replace, source)


def inspect_source(path, root, kind):
    resolved = path.resolve(strict=True)
    if not resolved.is_relative_to(root) or path.is_symlink():
        raise ValueError("Source must remain inside the explicitly selected workspace: " + str(path))
    if resolved.stat().st_size > MAX_SOURCE_BYTES:
        raise ValueError("Source exceeds the 2 MiB inventory limit: " + str(path))
    with resolved.open("rb") as stream:
        raw = stream.read(MAX_SOURCE_BYTES + 1)
    if len(raw) > MAX_SOURCE_BYTES:
        raise ValueError("Source grew beyond the inventory limit: " + str(path))
    source = raw.decode("utf-8-sig")
    searchable = without_comments(source)
    registrations = [
        {"line": searchable.count("\n", 0, match.start()) + 1,
         "excerpt": match.group().strip()}
        for match in REGISTRATION.finditer(searchable)
    ]
    hooks = [
        {"line": searchable.count("\n", 0, match.start()) + 1,
         "name": match.group(1)}
        for match in HOOK.finditer(searchable)
    ]
    return {
        "source": resolved.relative_to(root).as_posix(),
        "sha256": hashlib.sha256(raw).hexdigest(),
        "kind": kind,
        "migrationStatus": "source-only",
        "qualityStatus": "unreviewed",
        "sourceHistoryVerified": False,
        "verifiedUpstreamCommit": None,
        "runtimeSupportImplied": False,
        "lineCount": len(source.splitlines()),
        "deckAttributeExcerpts": [match.group().strip() for match in DECK.finditer(searchable)],
        "registrationLineCount": len(registrations),
        "registrationLines": registrations,
        "overrideHooks": hooks,
        "reviewRequired": [
            "helper and inherited behavior dependencies",
            "selection queues and effect prompt stages",
            "per-duel state, resets and action priority",
            "Lua costs, targets, restrictions and effect identity",
            "real duel equivalence and resource lifetimes",
        ],
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--workspace-root", type=Path, default=Path.cwd())
    parser.add_argument("--executor-dir", type=Path, required=True)
    parser.add_argument("--shared-source", type=Path, action="append", default=[])
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    try:
        root = args.workspace_root.resolve(strict=True)
        directory = (root / args.executor_dir).resolve(strict=True)
        if not directory.is_dir() or not directory.is_relative_to(root):
            raise ValueError("Choose an executor directory inside the workspace.")
        # Direct children only; never follow directory trees or inspect archives.
        files = sorted(directory.glob("*.cs"), key=lambda path: path.name.casefold())
        if not files or len(files) + len(args.shared_source) > MAX_FILES:
            raise ValueError("Choose between 1 and 256 source files.")
        entries = [inspect_source(path, root, "archived-executor") for path in files]
        entries.extend(inspect_source(root / path, root, "shared-helper")
                       for path in args.shared_source)
        if len({entry["source"] for entry in entries}) != len(entries):
            raise ValueError("A source file was selected more than once.")
        result = {
            "format": "skynet-source-catalog-v1",
            "purpose": "Source discovery for reviewed migration; no runtime support is inferred.",
            "extraction": "Lexical line hints; not full parsing, dependency analysis or semantic import.",
            "executorFiles": len(files),
            "sharedFiles": len(args.shared_source),
            "registrationLineCount": sum(entry["registrationLineCount"] for entry in entries),
            "sources": entries,
        }
        output = (root / args.output).resolve()
        if not output.is_relative_to(root):
            raise ValueError("Write the inventory inside the workspace.")
        if output.suffix.lower() != ".json":
            raise ValueError("The inventory output must be a .json file.")
        if output.exists():
            raise ValueError("Output already exists; choose a new filename.")
        output.parent.mkdir(parents=True, exist_ok=True)
        # Exclusive creation avoids accidentally replacing a source or prior catalog.
        with output.open("x", encoding="utf-8", newline="\n") as stream:
            json.dump(result, stream, ensure_ascii=False, indent=2)
            stream.write("\n")
        print("Indexed %d executor files and %d shared sources; %d registration line hints."
              % (len(files), len(args.shared_source), result["registrationLineCount"]))
        print("Source inventory only. No executor was run or imported into SKYNET.")
    except (OSError, ValueError, UnicodeError) as error:
        parser.exit(2, "SKYNET source catalog failed: " + str(error) + "\n")


if __name__ == "__main__":
    main()
