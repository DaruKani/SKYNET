"""Build an isolated localhost-only MDPro4 native server for Skynet Arena.

Exports exact committed server/core inputs and reuses explicitly selected local
dependency sources from a prior reviewed Windows candidate. It never modifies
the repositories, installs into MDPro4, downloads code, or starts a server.
"""
import argparse
import hashlib
import importlib.util
import io
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import tarfile


def run(command, **kwargs):
    return subprocess.run([str(value) for value in command], check=True, **kwargs)


def file_hash(filename):
    with filename.open("rb") as source:
        return hashlib.file_digest(source, "sha256").hexdigest()


def tree_hash(directory):
    digest = hashlib.sha256()
    count = 0
    for filename in sorted(directory.rglob("*")):
        if filename.is_symlink():
            raise ValueError("Dependency snapshots must not contain symlinks: " + str(filename))
        if filename.is_file():
            relative = filename.relative_to(directory).as_posix()
            digest.update(relative.encode("utf-8") + b"\0")
            digest.update(bytes.fromhex(file_hash(filename)))
            count += 1
    return {"sha256": digest.hexdigest(), "files": count}


def export_git(repository, commit, destination):
    if not re.fullmatch(r"[0-9a-f]{40}", commit):
        raise ValueError("Use an exact full lowercase Git commit")
    base = ["git", "-c", "safe.directory=" + repository.as_posix(), "-C", repository]
    actual = run(base + ["rev-parse", commit + "^{commit}"], capture_output=True, text=True).stdout.strip()
    if actual != commit:
        raise ValueError("Commit did not resolve to the requested object")
    archive = run(base + ["archive", "--format=tar", commit], capture_output=True).stdout
    destination.mkdir(parents=True, exist_ok=True)
    with tarfile.open(fileobj=io.BytesIO(archive)) as package:
        package.extractall(destination, filter="data")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--server-repository", required=True, type=Path)
    parser.add_argument("--server-commit", required=True)
    parser.add_argument("--core-repository", required=True, type=Path)
    parser.add_argument("--core-commit", required=True)
    parser.add_argument("--dependency-candidate", required=True, type=Path,
                        help="Explicit previous candidate with build-receipt.json, unpacked/, and source/irrlicht")
    parser.add_argument("--work-dir", required=True, type=Path, help="New nonexistent destination")
    parser.add_argument("--msbuild", required=True, type=Path)
    args = parser.parse_args()
    work = args.work_dir.resolve()
    if work.exists():
        raise ValueError("Use a new destination; existing candidate evidence is never overwritten")
    work.mkdir(parents=True)
    source = work / "source"
    server_repository = args.server_repository.resolve()
    core_repository = args.core_repository.resolve()
    export_git(server_repository, args.server_commit, source)
    export_git(core_repository, args.core_commit, source / "ocgcore")

    builder_path = source / "mdpro4/build/BuildWindowsServer.py"
    lock_path = source / "mdpro4/build/windows-server.lock.json"
    lock = json.loads(lock_path.read_text(encoding="utf-8"))
    if args.core_commit != lock["core"]["commit"]:
        raise ValueError("Arena bootstrap requires the committed server's reviewed embedded-core pin")
    dependency_candidate = args.dependency_candidate.resolve()
    previous_receipt_path = dependency_candidate / "build-receipt.json"
    previous_receipt = json.loads(previous_receipt_path.read_text(encoding="utf-8"))
    previous_inputs = previous_receipt["inputs"]
    if previous_inputs["dependencies"] != lock["dependencies"]:
        raise ValueError("Previous dependency versions do not match the selected server lock")
    if previous_inputs["irrlicht"] != lock["irrlicht"]:
        raise ValueError("Previous Irrlicht source pin does not match the selected server lock")
    if previous_inputs["toolchain"] != lock["toolchain"]:
        raise ValueError("Previous toolchain does not match the selected server lock")

    dependency_snapshots = []
    for dependency in lock["dependencies"]:
        parent = dependency_candidate / "unpacked" / dependency["name"]
        if dependency["name"] == "premake":
            executable = parent / "premake5.exe"
            shutil.copy2(executable, source / "premake5.exe")
            dependency_snapshots.append({
                "name": dependency["name"], "path": str(executable),
                "sha256": file_hash(executable),
            })
        else:
            roots = list(parent.iterdir())
            if len(roots) != 1 or not roots[0].is_dir():
                raise ValueError("Unexpected dependency snapshot layout: " + str(parent))
            snapshot = tree_hash(roots[0])
            shutil.copytree(roots[0], source / dependency["name"])
            dependency_snapshots.append({
                "name": dependency["name"], "path": str(roots[0]), **snapshot,
            })
    irrlicht = dependency_candidate / "source/irrlicht"
    dependency_snapshots.append({"name": "irrlicht", "path": str(irrlicht), **tree_hash(irrlicht)})
    shutil.copytree(irrlicht, source / "irrlicht")

    for overlay in ("premake", "resource"):
        for child in (source / overlay).iterdir():
            if child.is_dir():
                shutil.copytree(child, source / child.name, dirs_exist_ok=True)
    shutil.copy2(source / "premake/event/msvc-event-config.h", source / "event/include/event2/event-config.h")
    shutil.copy2(source / "event/WIN32-Code/nmake/evconfig-private.h", source / "event/include/evconfig-private.h")

    binding_file = source / "gframe/game.cpp"
    before = binding_file.read_bytes()
    old = b"unsigned int listen_ip = INADDR_ANY;"
    new = b"unsigned int listen_ip = INADDR_LOOPBACK;"
    if before.count(old) != 1:
        raise ValueError("Native listen binding no longer matches the reviewed one-line patch")
    after = before.replace(old, new)
    binding_file.write_bytes(after)
    patch = {
        "file": "gframe/game.cpp",
        "before": old.decode(),
        "after": new.decode(),
        "sourceSha256": hashlib.sha256(before).hexdigest(),
        "patchedSha256": hashlib.sha256(after).hexdigest(),
    }
    (work / "localhost-only.patch").write_text(
        "--- a/gframe/game.cpp\n+++ b/gframe/game.cpp\n@@\n-\t" + old.decode()
        + "\n+\t" + new.decode() + "\n", encoding="utf-8")
    receipt = {
        "schema": "skynet-arena-native-v1",
        "serverRepository": str(server_repository), "serverCommit": args.server_commit,
        "coreRepository": str(core_repository), "coreCommit": args.core_commit,
        "reviewedLock": lock,
        "dependencyCandidate": str(dependency_candidate),
        "dependencyReceiptSha256": file_hash(previous_receipt_path),
        "dependencySnapshots": dependency_snapshots,
        "dependencyProvenance": "Explicit prior local candidate sources; versions checked against both receipts and content hashes recorded. Archives are not re-downloaded.",
        "patch": patch, "builderSourceSha256": file_hash(builder_path),
        "status": "prepared",
    }
    receipt_path = work / "arena-native-receipt.json"
    receipt_path.write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")

    # Reuse the selected committed builder's project-closure and static-CRT audit.
    spec = importlib.util.spec_from_file_location("reviewed_windows_builder", builder_path)
    builder = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(builder)
    environment = {key.upper(): value for key, value in os.environ.items()}
    environment.pop("CL", None)
    environment.pop("_CL_", None)
    environment.update(lock["environment"])
    run([source / "premake5.exe", lock["toolchain"]["generator"]], cwd=source, env=environment)
    projects = builder.audit_projects(source / "build")
    command = [
        args.msbuild.resolve(), source / "build/YGOPro.sln", "/m:4", "/nr:false",
        "/p:Configuration=Release", "/p:Platform=x64",
        "/p:VCToolsVersion=" + lock["toolchain"]["vctoolsVersion"],
        "/p:WindowsTargetPlatformVersion=" + lock["toolchain"]["windowsSdkVersion"],
        "/v:minimal", "/nologo", "/bl:" + str(work / "build.binlog"),
    ]
    receipt["projects"] = projects
    receipt["command"] = [str(value) for value in command]
    receipt["status"] = "building"
    receipt_path.write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")
    try:
        with (work / "build.log").open("w", encoding="utf-8") as log:
            run(command, env=environment, stdout=log, stderr=subprocess.STDOUT)
    except subprocess.CalledProcessError as error:
        receipt.update({"status": "failed", "exitCode": error.returncode})
        receipt_path.write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")
        raise
    candidate = source / "bin/release/x64/ygoserver.dll"
    receipt.update({
        "status": "built",
        "candidate": str(candidate),
        "sha256": file_hash(candidate),
        "listenAddress": "127.0.0.1",
        "bytes": candidate.stat().st_size,
        "runtimeValidation": "Not run. Verify listening addresses and real Arena matches separately.",
    })
    receipt_path.write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")
    print("Localhost-only native candidate:", candidate)
    print("SHA256:", receipt["sha256"])
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
