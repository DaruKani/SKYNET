"""Own an Arena process tree with a private Windows kill-on-close job object.

Attach a freshly started subprocess.Popen before it can create duel workers.
Keep this owner alive until shutdown. The job handle is unnamed and explicitly
non-inheritable: closing it, or abrupt termination of this Python supervisor,
terminates processes assigned to this job and their inherited descendants.
"""
import ctypes
from ctypes import wintypes
import os
import subprocess


JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x00002000
JOB_OBJECT_EXTENDED_LIMIT_INFORMATION = 9
HANDLE_FLAG_INHERIT = 0x00000001


class _BasicLimitInformation(ctypes.Structure):
    _fields_ = [
        ("PerProcessUserTimeLimit", ctypes.c_longlong),
        ("PerJobUserTimeLimit", ctypes.c_longlong),
        ("LimitFlags", wintypes.DWORD),
        ("MinimumWorkingSetSize", ctypes.c_size_t),
        ("MaximumWorkingSetSize", ctypes.c_size_t),
        ("ActiveProcessLimit", wintypes.DWORD),
        ("Affinity", ctypes.c_size_t),
        ("PriorityClass", wintypes.DWORD),
        ("SchedulingClass", wintypes.DWORD),
    ]


class _IoCounters(ctypes.Structure):
    _fields_ = [
        ("ReadOperationCount", ctypes.c_ulonglong),
        ("WriteOperationCount", ctypes.c_ulonglong),
        ("OtherOperationCount", ctypes.c_ulonglong),
        ("ReadTransferCount", ctypes.c_ulonglong),
        ("WriteTransferCount", ctypes.c_ulonglong),
        ("OtherTransferCount", ctypes.c_ulonglong),
    ]


class _ExtendedLimitInformation(ctypes.Structure):
    _fields_ = [
        ("BasicLimitInformation", _BasicLimitInformation),
        ("IoInfo", _IoCounters),
        ("ProcessMemoryLimit", ctypes.c_size_t),
        ("JobMemoryLimit", ctypes.c_size_t),
        ("PeakProcessMemoryUsed", ctypes.c_size_t),
        ("PeakJobMemoryUsed", ctypes.c_size_t),
    ]


def _terminate_unattached(process):
    """Only clean up the exact process whose job assignment failed."""
    if process.poll() is not None:
        return
    process.terminate()
    try:
        process.wait(timeout=5)
    except subprocess.TimeoutExpired:
        process.kill()
        process.wait(timeout=5)


class OwnedProcessJob:
    def __init__(self):
        if os.name != "nt":
            raise RuntimeError("Arena process ownership requires Windows job objects.")
        self._handle = None
        self._kernel = ctypes.WinDLL("kernel32", use_last_error=True)
        self._kernel.CreateJobObjectW.argtypes = [ctypes.c_void_p, wintypes.LPCWSTR]
        self._kernel.CreateJobObjectW.restype = wintypes.HANDLE
        self._kernel.SetInformationJobObject.argtypes = [
            wintypes.HANDLE, ctypes.c_int, ctypes.c_void_p, wintypes.DWORD,
        ]
        self._kernel.SetInformationJobObject.restype = wintypes.BOOL
        self._kernel.SetHandleInformation.argtypes = [wintypes.HANDLE, wintypes.DWORD, wintypes.DWORD]
        self._kernel.SetHandleInformation.restype = wintypes.BOOL
        self._kernel.AssignProcessToJobObject.argtypes = [wintypes.HANDLE, wintypes.HANDLE]
        self._kernel.AssignProcessToJobObject.restype = wintypes.BOOL
        self._kernel.CloseHandle.argtypes = [wintypes.HANDLE]
        self._kernel.CloseHandle.restype = wintypes.BOOL
        handle = self._kernel.CreateJobObjectW(None, None)
        if not handle:
            raise ctypes.WinError(ctypes.get_last_error())
        try:
            if not self._kernel.SetHandleInformation(handle, HANDLE_FLAG_INHERIT, 0):
                raise ctypes.WinError(ctypes.get_last_error())
            information = _ExtendedLimitInformation()
            information.BasicLimitInformation.LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
            if not self._kernel.SetInformationJobObject(
                    handle, JOB_OBJECT_EXTENDED_LIMIT_INFORMATION,
                    ctypes.byref(information), ctypes.sizeof(information)):
                raise ctypes.WinError(ctypes.get_last_error())
        except BaseException:
            self._kernel.CloseHandle(handle)
            raise
        self._handle = handle

    def attach(self, process):
        """Attach an exact live Popen; failed attachment retires that Popen."""
        if not isinstance(process, subprocess.Popen):
            raise TypeError("attach requires a subprocess.Popen owned by this supervisor.")
        try:
            if self._handle is None:
                raise RuntimeError("Cannot attach a process to a closed Arena job.")
            if process.poll() is not None:
                raise RuntimeError("Arena process exited before job assignment.")
            # Popen owns this existing Windows process handle. No PID lookup,
            # process discovery, command-name matching or new process handle.
            process_handle = getattr(process, "_handle", None)
            if process_handle is None:
                raise RuntimeError("Popen has no Windows process handle.")
            if not self._kernel.AssignProcessToJobObject(self._handle, int(process_handle)):
                raise ctypes.WinError(ctypes.get_last_error())
        except BaseException:
            _terminate_unattached(process)
            raise
        return process

    def close(self):
        """Close only this job; Windows retires its assigned process tree."""
        if self._handle is None:
            return
        handle = self._handle
        if not self._kernel.CloseHandle(handle):
            raise ctypes.WinError(ctypes.get_last_error())
        self._handle = None

    def __enter__(self):
        if self._handle is None:
            raise RuntimeError("Arena process job is closed.")
        return self

    def __exit__(self, exc_type, exc_value, traceback):
        self.close()

    def __del__(self):
        try:
            self.close()
        except (AttributeError, OSError):
            pass
