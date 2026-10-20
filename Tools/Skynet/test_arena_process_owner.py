"""Windows lifetime checks using only subprocesses created by this test."""
import ctypes
from ctypes import wintypes
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import time
import unittest
from unittest import mock

from arena_process_owner import OwnedProcessJob


WORKER = r"""
import json, os, pathlib, subprocess, sys, time
directory = pathlib.Path(sys.argv[1])
deadline = time.monotonic() + 10
while not (directory / 'attached').exists():
    if time.monotonic() > deadline: raise RuntimeError('attachment timed out')
    time.sleep(0.02)
child = subprocess.Popen([sys.executable, '-c', 'import time; time.sleep(60)'],
                         creationflags=subprocess.CREATE_NO_WINDOW)
(directory / 'tree.tmp').write_text(json.dumps([os.getpid(), child.pid]))
(directory / 'tree.tmp').replace(directory / 'tree.json')
time.sleep(60)
"""
SUPERVISOR = r"""
import pathlib, subprocess, sys, time, os
sys.path.insert(0, sys.argv[2])
from arena_process_owner import OwnedProcessJob
directory = pathlib.Path(sys.argv[1])
owner = OwnedProcessJob()
child = subprocess.Popen([sys.executable, str(directory / 'worker.py'), str(directory)],
                         creationflags=subprocess.CREATE_NO_WINDOW)
owner.attach(child)
(directory / 'attached').touch()
deadline = time.monotonic() + 10
while not (directory / 'crash').exists():
    if time.monotonic() > deadline: raise RuntimeError('crash signal timed out')
    time.sleep(0.02)
os._exit(23)
"""


def wait_file(path):
    deadline = time.monotonic() + 10
    while not path.exists():
        if time.monotonic() > deadline:
            raise AssertionError("Owned fixture did not become ready.")
        time.sleep(0.02)
    return json.loads(path.read_text())


@unittest.skipUnless(os.name == "nt", "Windows job-object fixtures")
class ProcessOwnerTests(unittest.TestCase):
    def setUp(self):
        self.kernel = ctypes.WinDLL("kernel32", use_last_error=True)
        self.kernel.OpenProcess.argtypes = [wintypes.DWORD, wintypes.BOOL, wintypes.DWORD]
        self.kernel.OpenProcess.restype = wintypes.HANDLE
        self.kernel.WaitForSingleObject.argtypes = [wintypes.HANDLE, wintypes.DWORD]
        self.kernel.WaitForSingleObject.restype = wintypes.DWORD
        self.kernel.CloseHandle.argtypes = [wintypes.HANDLE]
        self.kernel.CloseHandle.restype = wintypes.BOOL
        self.handles = []
        self.processes = []

    def tearDown(self):
        for process in self.processes:
            if process.poll() is None:
                process.terminate()
            process.wait(timeout=5)
        for handle in self.handles:
            self.kernel.CloseHandle(handle)

    def start(self, *args):
        process = subprocess.Popen([sys.executable, *args], stdin=subprocess.DEVNULL,
                                   stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
                                   creationflags=subprocess.CREATE_NO_WINDOW)
        self.processes.append(process)
        return process

    def owned_handle(self, pid):
        # The PID came directly from our gated fixture. Open it before termination
        # so waiting cannot accidentally observe a later reuse of that PID.
        handle = self.kernel.OpenProcess(0x00100000, False, pid)
        self.assertTrue(handle, "Could not open owned fixture process")
        self.handles.append(handle)
        return handle

    def test_close_retires_assigned_tree_and_leaves_unassigned_process_alive(self):
        with tempfile.TemporaryDirectory(prefix="skynet-job-test-") as directory:
            root = Path(directory)
            root.joinpath("worker.py").write_text(WORKER, encoding="utf-8")
            sentinel = self.start("-c", "import time; time.sleep(60)")
            with OwnedProcessJob() as owner:
                worker = self.start(str(root / "worker.py"), str(root))
                owner.attach(worker)
                root.joinpath("attached").touch()
                parent_pid, child_pid = wait_file(root / "tree.json")
                self.assertEqual(worker.pid, parent_pid)
                child_handle = self.owned_handle(child_pid)
                owner.close()
                worker.wait(timeout=5)
                self.assertEqual(0, self.kernel.WaitForSingleObject(child_handle, 5000))
                self.assertIsNone(sentinel.poll())
            owner.close()  # idempotent

    def test_supervisor_abrupt_exit_closes_job_and_descendants(self):
        with tempfile.TemporaryDirectory(prefix="skynet-job-crash-") as directory:
            root = Path(directory)
            root.joinpath("worker.py").write_text(WORKER, encoding="utf-8")
            root.joinpath("supervisor.py").write_text(SUPERVISOR, encoding="utf-8")
            supervisor = self.start(str(root / "supervisor.py"), str(root), str(Path(__file__).parent))
            parent_pid, child_pid = wait_file(root / "tree.json")
            parent_handle = self.owned_handle(parent_pid)
            child_handle = self.owned_handle(child_pid)
            root.joinpath("crash").touch()
            self.assertEqual(23, supervisor.wait(timeout=5))
            self.assertEqual(0, self.kernel.WaitForSingleObject(parent_handle, 5000))
            self.assertEqual(0, self.kernel.WaitForSingleObject(child_handle, 5000))

    def test_failed_attachment_retires_exact_child(self):
        owner = OwnedProcessJob()
        owner.close()
        process = self.start("-c", "import time; time.sleep(60)")
        with self.assertRaisesRegex(RuntimeError, "closed"):
            owner.attach(process)
        self.assertIsNotNone(process.poll())
        with self.assertRaises(TypeError):
            owner.attach(object())

    def test_native_assignment_error_retires_exact_child(self):
        with OwnedProcessJob() as owner:
            process = self.start("-c", "import time; time.sleep(60)")
            with mock.patch.object(owner._kernel, "AssignProcessToJobObject", return_value=0):
                with self.assertRaises(OSError):
                    owner.attach(process)
            self.assertIsNotNone(process.poll())


if __name__ == "__main__":
    unittest.main()
