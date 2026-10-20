"""Stage and supervise an explicitly supplied, pinned local arena-srvpro.

No downloads, global services, firewall rules or game-installation writes.
The caller installs its single npm dependency in the fresh runtime afterwards.
The authoritative native library must have a loopback-only build receipt.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import secrets
import shutil
import subprocess
import time
from arena_process_owner import OwnedProcessJob

WORKSPACE = Path(__file__).resolve().parents[2]
SOURCE_COMMIT = "af87d3a8c4269c105144a67221728aa25a747910"
INDEX_SHA256 = "5860d273580d0a98b33902bef4b077c49a18761bf80f70cbb0cbf216ac8a89e9"
LOCK_SHA256 = "e64cbb1ec688d465026896114de0624c3d449287f0d7683a336814601e1553b6"
SOURCE_TREE_SHA256 = "5321c52d1c7ef73bd2788d34dac9f07bb186e6c8ac4b506895fec33079f66600"
RUNTIME_FILES = (
    "LICENSE", "README.md", "data/constants.json", "data/default_config.json",
    "data/default_data.json", "data/i18n.json", "index.js", "logger.js",
    "package-lock.json", "package.json", "room-observer-stream.js",
    "room-registry.js", "utility.js", "ygopro.js",
)
MAX_LOG_BYTES = 32 * 1024 * 1024


def sha256(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def workspace_path(value):
    path = Path(value).resolve()
    artifact_root = (WORKSPACE / "Artifacts").resolve()
    if not path.is_relative_to(artifact_root) or path == artifact_root:
        raise ValueError("Runtime output must be a dedicated directory under the ignored Artifacts directory.")
    return path


def replace_once(text, old, new):
    if text.count(old) != 1:
        raise ValueError("Pinned SRVPro patch context no longer matches.")
    return text.replace(old, new, 1)


def patch_gateway(text):
    text = replace_once(text, 'spawn("./ygopro", param, {',
                        'spawn("./SKYNET_DUEL_HOST.exe", param, {')
    text = replace_once(text, 'cwd: "ygopro",\n        windowsHide: true,',
                        'cwd: "ygopro",\n        windowsHide: true,\n'
                        '        env: { ...process.env, SKYNET_DUEL_PARENT_PID: String(process.pid) },')
    text = replace_once(text, '  settings.port,\n  () => {',
                        '  settings.port,\n  "127.0.0.1",\n  () => {')
    text = replace_once(text, 'http_server.listen(settings.modules.http.port);',
                        'http_server.listen(settings.modules.http.port, "127.0.0.1");')
    text = replace_once(text, 'https_server.listen(settings.modules.http.ssl.port);',
                        'https_server.listen(settings.modules.http.ssl.port, "127.0.0.1");')
    return text


def prepare(args):
    source = args.source_root.resolve(strict=True)
    if sha256(source / "index.js") != INDEX_SHA256 or sha256(source / "package-lock.json") != LOCK_SHA256:
        raise ValueError("Source does not match the reviewed arena-srvpro pin.")
    for filename in (args.native_host, args.native_library, args.database, args.scripts, args.native_receipt):
        if not filename.is_file():
            raise ValueError("Missing explicit input: " + str(filename))
    receipt = json.loads(args.native_receipt.read_text(encoding="utf-8-sig"))
    # This assertion is deliberately part of the generated local build contract.
    if receipt.get("listenAddress") != "127.0.0.1" or receipt.get("sha256") != sha256(args.native_library):
        raise ValueError("Native receipt must certify this exact library as loopback-only.")
    if args.port == args.admin_port:
        raise ValueError("Duel and management ports must differ.")
    # Hash only the complete, explicit runtime allowlist: no recursive discovery.
    digest = hashlib.sha256()
    hashes = {}
    for name in sorted(RUNTIME_FILES):
        path = source / name
        if path.is_symlink() or not path.resolve().is_relative_to(source) or path.stat().st_size > 8 * 1024 * 1024:
            raise ValueError("Unexpected upstream runtime file: " + str(path))
        hashes[name] = sha256(path)
        digest.update(name.encode("utf-8") + b"\0")
        digest.update(bytes.fromhex(hashes[name]))
    if digest.hexdigest() != SOURCE_TREE_SHA256:
        raise ValueError("Runtime sources do not match the complete reviewed SRVPro file set.")
    patched_gateway = patch_gateway((source / "index.js").read_text(encoding="utf-8"))
    runtime = workspace_path(args.runtime_dir)
    runtime.mkdir(parents=True, exist_ok=False)
    for relative in RUNTIME_FILES:
        target = runtime / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(source / relative, target)
    (runtime / "index.js").write_text(patched_gateway, encoding="utf-8")
    config_dir = runtime / "config"
    config_dir.mkdir()
    password = secrets.token_urlsafe(32)
    config = {
        "port": args.port, "version": 4962,
        "hostinfo": {"no_check_deck": True, "no_shuffle_deck": False, "no_watch": True,
                     "time_limit": 180, "replay_mode": 0},
        "modules": {
            "welcome": "SKYNET Arena Duel Server",
            "update": "Please update your client protocol version.",
            "wait_update": "The client protocol is newer than this server.",
            "full": "The duel server is full.",
            "enable_halfway_watch": False, "max_rooms_count": 4,
            "i18n": {"auto_pick": False, "default": "en-us", "fallback": "en-us", "map": {}},
            "http": {"port": args.admin_port, "public_roomlist": False, "ssl": {"enabled": False}},
        },
    }
    (config_dir / "config.json").write_text(json.dumps(config, indent=2) + "\n", encoding="utf-8")
    (config_dir / "admin_user.json").write_text(
        json.dumps({"users": {"skynet-local": {"password": password, "enabled": True}}}, indent=2) + "\n",
        encoding="utf-8")
    native = runtime / "ygopro"
    (native / "Data/locales/zh-CN").mkdir(parents=True)
    for src, relative in ((args.native_host, "SKYNET_DUEL_HOST.exe"),
                          (args.native_library, "ygoserver.dll"),
                          (args.database, "cards.cdb"), (args.scripts, "Data/script.zip")):
        shutil.copyfile(src, native / relative)
    manifest = {
        "format": "skynet-arena-server-v1",
        "upstream": {"repository": "https://github.com/mercury233/arena-srvpro", "commit": SOURCE_COMMIT,
                     "license": "AGPL-3.0", "sourceFiles": hashes},
        "gatewaySha256": sha256(runtime / "index.js"),
        "nativeLibrarySha256": sha256(native / "ygoserver.dll"),
        "nativeHostSha256": sha256(native / "SKYNET_DUEL_HOST.exe"),
        "databaseSha256": sha256(native / "cards.cdb"),
        "scriptsSha256": sha256(native / "Data/script.zip"),
        "listenAddress": "127.0.0.1", "port": args.port, "adminPort": args.admin_port,
        "noDeckCheck": True, "shuffleEnabled": True, "coreSeed": None,
        "nativeBuild": receipt,
    }
    (runtime / "skynet-server.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    print("Staged dedicated server:", runtime)
    print("Run npm ci --ignore-scripts --no-audit --no-fund inside that directory.")
    print("Credentials stay in config/admin_user.json; never commit the runtime directory.")


def serve(args):
    runtime = workspace_path(args.runtime_dir)
    manifest = json.loads((runtime / "skynet-server.json").read_text(encoding="utf-8"))
    if manifest["gatewaySha256"] != sha256(runtime / "index.js"):
        raise ValueError("Staged gateway changed; prepare a new reviewed runtime.")
    for relative, expected in manifest["upstream"]["sourceFiles"].items():
        if relative != "index.js" and sha256(runtime / relative) != expected:
            raise ValueError("Staged SRVPro input changed: " + relative)
    for key, relative in (("nativeLibrarySha256", "ygopro/ygoserver.dll"),
                          ("nativeHostSha256", "ygopro/SKYNET_DUEL_HOST.exe"),
                          ("databaseSha256", "ygopro/cards.cdb"),
                          ("scriptsSha256", "ygopro/Data/script.zip")):
        if sha256(runtime / relative) != manifest[key]:
            raise ValueError("Staged native input changed: " + relative)
    if not (runtime / "node_modules/deepmerge").is_dir():
        raise ValueError("Install the pinned runtime dependency with npm ci first.")
    node = args.node.resolve(strict=True)
    stop = runtime / "stop.request"
    if stop.exists():
        raise ValueError("This supervisor was already stopped; remove its stop.request explicitly to resume.")
    # Exclusive lease prevents two supervisors racing to restart the same server.
    lease = runtime / "supervisor.lock"
    fd = os.open(lease, os.O_WRONLY | os.O_CREAT | os.O_EXCL)
    os.write(fd, str(os.getpid()).encode("ascii"))
    os.close(fd)
    child = None
    owner = None
    deadline = time.monotonic() + args.minutes * 60
    restarts = 0
    try:
        owner = OwnedProcessJob()
        with (runtime / "server.log").open("a", encoding="utf-8") as output:
            while time.monotonic() < deadline and not stop.exists():
                started = time.monotonic()
                child = subprocess.Popen([str(node), "index.js"], cwd=runtime,
                                         stdout=output, stderr=subprocess.STDOUT,
                                         creationflags=subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0)
                owner.attach(child)
                (runtime / "node.pid").write_text(str(child.pid), encoding="ascii")
                while child.poll() is None and time.monotonic() < deadline and not stop.exists():
                    if (runtime / "server.log").stat().st_size >= MAX_LOG_BYTES:
                        raise RuntimeError("Dedicated server log limit reached; stopped owned processes.")
                    time.sleep(0.2)
                if child.poll() is None:
                    break
                if child.returncode != 0:
                    raise RuntimeError("Dedicated server failed; inspect server.log. No crash restart was attempted.")
                # Arena's authenticated reboot exits normally, then expects a fresh instance ID.
                restarts += 1
                if restarts > 20 or time.monotonic() - started < 1:
                    raise RuntimeError("Dedicated server restart limit reached.")
                child = None
                time.sleep(0.25)
    finally:
        try:
            if child and child.poll() is None:
                child.terminate()
                try:
                    child.wait(timeout=5)
                except subprocess.TimeoutExpired:
                    child.kill()
                    child.wait(timeout=5)
        finally:
            if owner is not None:
                owner.close()
            # Only remove the lease created by this invocation. A stale lease
            # encountered on startup is never removed automatically.
            lease.unlink()
    print("Dedicated server supervisor stopped.")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="command", required=True)
    stage = commands.add_parser("prepare")
    for name in ("source-root", "runtime-dir", "native-host", "native-library", "native-receipt", "database", "scripts"):
        stage.add_argument("--" + name, type=Path, required=True)
    stage.add_argument("--port", type=int, default=17911)
    stage.add_argument("--admin-port", type=int, default=17922)
    run = commands.add_parser("serve")
    run.add_argument("--runtime-dir", type=Path, required=True)
    run.add_argument("--node", type=Path, required=True)
    run.add_argument("--minutes", type=int, default=60)
    args = parser.parse_args()
    try:
        if args.command == "prepare":
            if not all(1024 <= port <= 65535 for port in (args.port, args.admin_port)):
                raise ValueError("Ports must be between 1024 and 65535.")
            prepare(args)
        else:
            if not 1 <= args.minutes <= 1440:
                raise ValueError("Supervisor duration must be 1..1440 minutes.")
            serve(args)
    except (OSError, ValueError, RuntimeError, KeyError) as error:
        parser.exit(2, "SKYNET Arena server: " + str(error) + "\n")


if __name__ == "__main__":
    main()
