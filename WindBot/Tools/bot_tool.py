#!/usr/bin/env python3
"""Build, diagnose and check the normal WindBot without shell-specific wrappers."""

import argparse
from contextlib import closing
import ctypes.util
import os
from pathlib import Path
import platform
import shutil
import sqlite3
import subprocess
import sys


ROOT = Path(__file__).resolve().parents[1]
WINDOWS = os.name == "nt"
SUITES = {
    "runtime": "RuntimeChecks.cs",
    "rda": "RdaPlannerChecks.cs",
    "voiceless": "VoicelessChecks.cs",
    "metalmorph": "MetalmorphChecks.cs",
    "destiny-board": "DestinyBoardChecks.cs",
    "shaddoll-2025": "Shaddoll2025Checks.cs",
}


def existing_file(value):
    path = Path(value).expanduser().resolve()
    if not path.is_file():
        raise ValueError("File not found: " + str(path))
    return path


def find_msbuild(explicit=None):
    if explicit:
        return str(existing_file(explicit))
    found = shutil.which("msbuild")
    if found:
        return found
    if WINDOWS:
        installer = Path(os.environ.get("ProgramFiles(x86)", "C:/Program Files (x86)"))
        vswhere = installer / "Microsoft Visual Studio/Installer/vswhere.exe"
        if vswhere.is_file():
            result = subprocess.run(
                [str(vswhere), "-latest", "-products", "*", "-requires",
                 "Microsoft.Component.MSBuild", "-find", "MSBuild/**/Bin/MSBuild.exe"],
                capture_output=True, text=True, timeout=20, check=False,
            )
            for line in result.stdout.splitlines():
                if Path(line).is_file():
                    return line
    else:
        # Some distributions still package xbuild alongside Mono's current compiler.
        found = shutil.which("xbuild")
        if found:
            return found
    return None


def find_compiler(explicit=None):
    if explicit:
        return str(existing_file(explicit))
    if WINDOWS:
        msbuild = find_msbuild()
        if msbuild:
            candidate = Path(msbuild).parent / "Roslyn/csc.exe"
            if candidate.is_file():
                return str(candidate)
    for name in ("csc", "mono-csc", "mcs"):
        found = shutil.which(name)
        if found:
            return found
    return None


def runtime_command(executable):
    if WINDOWS:
        return [str(executable)]
    mono = shutil.which("mono")
    if not mono:
        raise ValueError("Mono runtime not found. Install your distribution's Mono runtime/development packages.")
    return [mono, str(executable)]


def native_status(directory):
    if WINDOWS:
        paths = [directory / arch / "sqlite3.dll" for arch in ("x64", "x86")]
        return all(path.is_file() for path in paths), ", ".join(str(path) for path in paths)
    native = ctypes.util.find_library("sqlite3")
    return bool(native), native or "libsqlite3.so.0 not found; install your distribution's SQLite runtime"


def inspect_database(path):
    # No discovery, merging, creation or writes to the selected database.
    with closing(sqlite3.connect(path.as_uri() + "?mode=ro", uri=True)) as connection:
        count = connection.execute("SELECT COUNT(*) FROM datas JOIN texts USING (id)").fetchone()[0]
    if not count:
        raise ValueError("The selected card database has no matching datas/texts rows.")
    return count


def execute(command, cwd, timeout, stdout=None, stderr=None):
    process = subprocess.Popen(command, cwd=str(cwd), stdout=stdout, stderr=stderr)
    try:
        return process.wait(timeout=timeout)
    except (subprocess.TimeoutExpired, KeyboardInterrupt):
        # This handle belongs to this invocation; never enumerate or stop other bots.
        process.terminate()
        try:
            process.wait(timeout=5)
        except subprocess.TimeoutExpired:
            process.kill()
            process.wait()
        raise


def doctor(args):
    tool = find_msbuild(args.msbuild)
    compiler = find_compiler(args.compiler)
    checks = [
        ("Project", (ROOT / "WindBot.sln").is_file(), str(ROOT)),
        ("Build tool", bool(tool), tool or "Install Visual Studio Build Tools (.NET desktop build tools + 4.8 targeting pack), or Mono development tools on Linux."),
        ("Harness compiler", bool(compiler), compiler or "csc / mono-csc / mcs not found"),
        ("Runtime", WINDOWS or bool(shutil.which("mono")), ".NET Framework 4.8 on Windows" if WINDOWS else shutil.which("mono") or "mono not found"),
    ]
    native_ok, native_detail = native_status(ROOT / "ThirdParty/sqlite")
    checks.append(("SQLite native library", native_ok, native_detail))
    if args.database:
        path = existing_file(args.database)
        checks.append(("Selected database", True, "{} joined cards: {}".format(inspect_database(path), path)))
    for label, okay, detail in checks:
        print("{} {}: {}".format("OK" if okay else "MISSING", label, detail))
    print("Python: " + platform.python_version())
    print("New code uses C# 6. The imported full executor collection already needs C# 7+; use recent Mono/compiler packages.")
    print("Doctor checks prerequisites only. Run build and check to verify framework references and actual SQLite loading.")
    if tool and Path(tool).stem.lower() == "xbuild":
        print("NOTE: xbuild is a deprecated compatibility fallback; prefer Mono MSBuild where available.")
    return 0 if all(okay for _, okay, _ in checks) else 1


def build(args):
    tool = find_msbuild(args.msbuild)
    if not tool:
        raise ValueError("MSBuild not found. Run doctor for prerequisites, or pass --msbuild /path/to/MSBuild.")
    print("Build tool: " + tool, flush=True)
    if Path(tool).stem.lower() == "xbuild":
        print("Using deprecated xbuild fallback; framework/compiler support depends on your Mono installation.", flush=True)
    configurations = ["Debug", "Release"] if args.configuration == "Both" else [args.configuration]
    for configuration in configurations:
        command = [tool, str(ROOT / "WindBot.sln"), "/nologo", "/verbosity:minimal",
                   "/p:Configuration=" + configuration, "/p:Platform=Any CPU"]
        result = execute(command, ROOT, args.timeout)
        if result:
            return result
        print("Built: " + str(ROOT / "bin" / configuration / "WindBot.exe"), flush=True)
    return 0


def binary_path(args):
    return existing_file(args.bot_exe or ROOT / "bin" / args.configuration / "WindBot.exe")


def copy_runtime(executable, output):
    shutil.copy2(executable, output / "WindBot.exe")
    config = executable.with_suffix(".exe.config")
    if config.is_file():
        shutil.copy2(config, output / config.name)
    if WINDOWS:
        for arch in ("x64", "x86"):
            native = existing_file(executable.parent / arch / "sqlite3.dll")
            (output / arch).mkdir()
            shutil.copy2(native, output / arch / native.name)
    dialogs = executable.parent / "Dialogs/default.json"
    if dialogs.is_file():
        (output / "Dialogs").mkdir()
        shutil.copy2(dialogs, output / "Dialogs/default.json")


def check(args):
    executable = binary_path(args)
    database = existing_file(args.database)
    deck = existing_file(args.deck)
    inspect_database(database)
    compiler = find_compiler(args.compiler)
    if not compiler:
        raise ValueError("C# compiler not found. Run doctor or pass --compiler /path/to/csc.")
    output = Path(args.output).expanduser().resolve()
    output.mkdir(parents=True, exist_ok=False)
    copy_runtime(executable, output)
    source = ROOT / "Tools" / SUITES[args.suite]
    harness = output / (source.stem + ".exe")
    command = [compiler, "/nologo", "/langversion:6", "/target:exe", "/platform:anycpu",
               "/r:System.Core.dll", "/r:System.Web.Extensions.dll", "/r:" + str(output / "WindBot.exe"),
               "/out:" + str(harness), str(source)]
    result = execute(command, output, args.timeout)
    if result:
        return result
    # Mono applies dllmap per containing assembly. Keep both configs for diagnostics
    # and the harness's application configuration (e.g. existing appSettings).
    config = output / "WindBot.exe.config"
    if config.is_file():
        shutil.copy2(config, harness.with_suffix(".exe.config"))
    report = output / "report.json"
    arguments = [str(database), str(deck), str(report)]
    if args.suite == "rda":
        arguments.insert(0, str(output / "WindBot.exe"))
    elif args.suite == "runtime":
        arguments.append(args.registration or "")
    with (output / "stdout.txt").open("w", encoding="utf-8") as stdout, (output / "stderr.txt").open("w", encoding="utf-8") as stderr:
        result = execute(runtime_command(harness) + arguments, output, args.timeout, stdout, stderr)
    for name in ("stdout.txt", "stderr.txt"):
        print((output / name).read_text(encoding="utf-8", errors="replace"), end="")
    print(("Receipt: " if report.is_file() else "No report was produced; inspect logs in: ") + str(report if report.is_file() else output))
    print("These checks do not start a duel server or measure playing strength.")
    return result


def run(args):
    executable = binary_path(args)
    database = existing_file(args.database)
    inspect_database(database)
    if not 1 <= args.port <= 65535:
        raise ValueError("Port must be between 1 and 65535.")
    command = runtime_command(executable) + [
        "DbPath=" + str(database), "Deck=" + args.deck, "Host=" + args.host,
        "Port=" + str(args.port), "Dialog=" + args.dialog, "Name=" + args.name,
        "Debug=" + str(args.debug), "Chat=False",
    ]
    if args.version is not None:
        command.append("Version=" + str(args.version))
    if args.host_info:
        command.append("HostInfo=" + args.host_info)
    print("Starting one WindBot client in {} (timeout {} seconds).".format(executable.parent, args.timeout), flush=True)
    return execute(command, executable.parent, args.timeout)


def parser():
    result = argparse.ArgumentParser(description=__doc__)
    commands = result.add_subparsers(dest="command", required=True)
    diagnostic = commands.add_parser("doctor", help="Inspect build/runtime prerequisites; no downloads or changes")
    diagnostic.add_argument("--database", help="Optional explicit CDB to inspect read-only")
    diagnostic.add_argument("--msbuild")
    diagnostic.add_argument("--compiler")
    diagnostic.set_defaults(action=doctor)
    builder = commands.add_parser("build", help="Build the existing solution, including BotWrapper")
    builder.add_argument("--configuration", choices=("Debug", "Release", "Both"), default="Both")
    builder.add_argument("--msbuild", help="Explicit build tool path; otherwise use PATH / Visual Studio discovery")
    builder.add_argument("--timeout", type=int, default=300)
    builder.set_defaults(action=build)
    checks = commands.add_parser("check", help="Run a bounded offline fixture with fresh receipts")
    checks.add_argument("--suite", choices=tuple(SUITES), default="runtime")
    checks.add_argument("--database", required=True)
    checks.add_argument("--deck", required=True, help="Explicit .ydk path")
    checks.add_argument("--registration", help="Runtime suite: also require this exact compiled Deck name")
    checks.add_argument("--output", required=True, help="New output directory; existing receipts are never overwritten")
    checks.add_argument("--compiler")
    checks.set_defaults(action=check)
    runner = commands.add_parser("run", help="Connect one client to an explicitly selected compatible duel host")
    runner.add_argument("--database", required=True)
    runner.add_argument("--deck", required=True, help="Registered Deck name, not a .ydk filename")
    runner.add_argument("--host", required=True)
    runner.add_argument("--port", required=True, type=int)
    runner.add_argument("--version", type=int)
    runner.add_argument("--host-info", help="HostInfo / room password; not printed by this launcher")
    runner.add_argument("--dialog", default="Universal.EN")
    runner.add_argument("--name", default="WindBot")
    runner.add_argument("--debug", action="store_true")
    runner.set_defaults(action=run)
    for command in (checks, runner):
        command.add_argument("--bot-exe", help="Explicit binary; default uses WindBot/bin/<configuration>/WindBot.exe")
        command.add_argument("--configuration", choices=("Debug", "Release"), default="Debug")
        command.add_argument("--timeout", type=int, default=90 if command is checks else 300)
    return result


def main(argv=None):
    args = parser().parse_args(argv)
    if hasattr(args, "timeout") and not 1 <= args.timeout <= 86400:
        raise ValueError("Timeout must be between 1 and 86400 seconds.")
    return args.action(args)


if __name__ == "__main__":
    try:
        sys.exit(main())
    except KeyboardInterrupt:
        print("Cancelled; the owned child process was stopped.", file=sys.stderr)
        sys.exit(130)
    except subprocess.TimeoutExpired:
        print("Timed out; the owned child process was stopped. Inspect the output directory if checking.", file=sys.stderr)
        sys.exit(124)
    except (OSError, ValueError, sqlite3.Error) as error:
        print("ERROR: " + str(error), file=sys.stderr)
        sys.exit(1)
