using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;

namespace Skynet.Tools
{
    // One native room per process. The native server owns rules, replay data,
    // socket responses and the first stdout line containing its listening port.
    internal static class SkynetDuelHost
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private delegate int StartServer([MarshalAs(UnmanagedType.LPStr)] string arguments);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int BindSocket(IntPtr socket, IntPtr address, int addressLength);

        private static BindSocket _originalBind;
        private static BindSocket _loopbackBind;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibraryEx(string filename, IntPtr file, uint flags);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr module, string name);

        [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern bool VirtualProtect(IntPtr address, UIntPtr size, uint newProtection, out uint oldProtection);

        [DllImport("kernel32.dll", ExactSpelling = true)]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern bool FlushInstructionCache(IntPtr process, IntPtr address, UIntPtr size);

        private static int Main(string[] args)
        {
            if (args.Length == 1 && args[0] == "--help")
            {
                Console.WriteLine("SKYNET Duel Host - one authoritative MDPro4 native room per process");
                Console.WriteLine("Usage: SKYNET_DUEL_HOST.exe port banlist rule mode duelRule noCheck noShuffle lp hand draw time replay");
                Console.WriteLine("Native library: ygoserver.dll beside this executable; resources: explicit working directory.");
                return 0;
            }
            try
            {
                ValidateArguments(args);
                ValidateResourceEnvironment();
                WatchExplicitParent();
                if (IntPtr.Size != 8)
                    throw new InvalidOperationException("The SKYNET duel host requires a 64-bit process.");
                string library = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ygoserver.dll");
                if (!File.Exists(library))
                    throw new FileNotFoundException("The explicitly staged native library is missing.", library);

                // Search dependencies only beside the staged DLL and in Windows
                // default loader directories; never search arbitrary parent folders.
                const uint searchDllLoadDirectory = 0x00000100;
                const uint searchDefaultDirectories = 0x00001000;
                IntPtr module = LoadLibraryEx(library, IntPtr.Zero, searchDllLoadDirectory | searchDefaultDirectories);
                if (module == IntPtr.Zero)
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot load the staged ygoserver.dll.");
                InstallLoopbackBindGuard(module);
                IntPtr address = GetProcAddress(module, "start_server");
                if (address == IntPtr.Zero)
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "The native library does not export start_server.");
                StartServer start = (StartServer)Marshal.GetDelegateForFunctionPointer(address, typeof(StartServer));

                // Keep the native module loaded until this owning process exits.
                // There is no reuse across rooms or attempt to unload native worker
                // code while a retiring thread could still be leaving its callback.
                return start(string.Join(" ", args));
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("SKYNET duel host failed: " + exception.Message);
                return 1;
            }
        }

        private static void InstallLoopbackBindGuard(IntPtr module)
        {
            const int dosHeaderOffset = 0x3c;
            const int peSignature = 0x00004550;
            const short pe32PlusMagic = 0x20b;
            int peOffset = Marshal.ReadInt32(module, dosHeaderOffset);
            IntPtr ntHeaders = IntPtr.Add(module, peOffset);
            if (Marshal.ReadInt32(ntHeaders) != peSignature)
                throw new InvalidOperationException("The staged native library has an invalid PE signature.");

            IntPtr optionalHeader = IntPtr.Add(ntHeaders, 24);
            short magic = Marshal.ReadInt16(optionalHeader);
            if (magic != pe32PlusMagic)
                throw new InvalidOperationException("The staged native library must be a 64-bit PE image.");
            const int importDirectoryOffset = 120;

            int importRva = Marshal.ReadInt32(optionalHeader, importDirectoryOffset);
            if (importRva <= 0)
                throw new InvalidOperationException("The staged native library has no import table.");
            IntPtr descriptors = IntPtr.Add(module, importRva);
            IntPtr bindEntry = IntPtr.Zero;
            for (int descriptorIndex = 0; descriptorIndex < 1024; descriptorIndex++)
            {
                IntPtr descriptor = IntPtr.Add(descriptors, descriptorIndex * 20);
                int originalThunkRva = Marshal.ReadInt32(descriptor);
                int nameRva = Marshal.ReadInt32(descriptor, 12);
                int firstThunkRva = Marshal.ReadInt32(descriptor, 16);
                if (originalThunkRva == 0 && nameRva == 0 && firstThunkRva == 0)
                    break;
                if (originalThunkRva == 0 || nameRva <= 0 || firstThunkRva <= 0)
                    continue;
                string libraryName = Marshal.PtrToStringAnsi(IntPtr.Add(module, nameRva));
                if (!string.Equals(libraryName, "WS2_32.dll", StringComparison.OrdinalIgnoreCase))
                    continue;

                IntPtr names = IntPtr.Add(module, originalThunkRva);
                IntPtr entries = IntPtr.Add(module, firstThunkRva);
                for (int thunkIndex = 0; thunkIndex < 65536; thunkIndex++)
                {
                    long value = Marshal.ReadInt64(names, thunkIndex * 8);
                    if (value == 0)
                        break;
                    if ((value & long.MinValue) != 0)
                    {
                        // Microsoft Winsock keeps the classic bind entry at
                        // ordinal 2; release builds may omit its import name.
                        if ((value & 0xffff) == 2)
                        {
                            bindEntry = IntPtr.Add(entries, thunkIndex * 8);
                            break;
                        }
                        continue;
                    }
                    IntPtr importByName = IntPtr.Add(module, checked((int)value));
                    string functionName = Marshal.PtrToStringAnsi(IntPtr.Add(importByName, 2));
                    if (functionName == "bind")
                    {
                        bindEntry = IntPtr.Add(entries, thunkIndex * 8);
                        break;
                    }
                }
                break;
            }
            if (bindEntry == IntPtr.Zero)
                throw new InvalidOperationException("The staged native library does not import WS2_32!bind.");

            IntPtr original = Marshal.ReadIntPtr(bindEntry);
            if (original == IntPtr.Zero)
                throw new InvalidOperationException("The staged native library has an unresolved bind import.");
            _originalBind = (BindSocket)Marshal.GetDelegateForFunctionPointer(original, typeof(BindSocket));
            _loopbackBind = GuardedBind;
            IntPtr replacement = Marshal.GetFunctionPointerForDelegate(_loopbackBind);
            uint oldProtection;
            if (!VirtualProtect(bindEntry, (UIntPtr)IntPtr.Size, 0x04, out oldProtection))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot protect the native bind import entry.");
            try
            {
                Marshal.WriteIntPtr(bindEntry, replacement);
            }
            finally
            {
                uint ignored;
                if (!VirtualProtect(bindEntry, (UIntPtr)IntPtr.Size, oldProtection, out ignored))
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot restore the native bind import protection.");
            }
            if (!FlushInstructionCache(GetCurrentProcess(), bindEntry, (UIntPtr)IntPtr.Size))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot flush the native bind import entry.");
        }

        private static int GuardedBind(IntPtr socket, IntPtr address, int addressLength)
        {
            const short addressFamilyInternet = 2;
            bool rewritten = address != IntPtr.Zero
                && addressLength >= 16
                && Marshal.ReadInt16(address) == addressFamilyInternet
                && Marshal.ReadInt32(address, 4) == 0;
            if (rewritten)
            {
                // sockaddr_in stores IPv4 octets in network order. On little-endian
                // Windows, writing 0x0100007f produces the bytes 7f 00 00 01.
                Marshal.WriteInt32(address, 4, 0x0100007f);
            }
            try
            {
                return _originalBind(socket, address, addressLength);
            }
            finally
            {
                if (rewritten)
                    Marshal.WriteInt32(address, 4, 0);
            }
        }

        private static void ValidateArguments(string[] args)
        {
            // serverapi.cpp allocates exactly 13 argv slots including argv[0].
            // Additional arguments would overrun its array; never forward them.
            if (args.Length != 12)
                throw new ArgumentException("Exactly twelve native room arguments are required.");
            int[] minimum = { 0, -1, 0, 0, 1, 0, 0, 1, 1, 0, 0, 0 };
            int[] maximum = { 65535, 999, 5, 2, int.MaxValue, 0, 0, 99999, 40, 35, 999, 7 };
            for (int index = 0; index < args.Length; index++)
            {
                if (index == 5 || index == 6)
                {
                    if (args[index] != "T" && args[index] != "F")
                        throw new ArgumentException("Native deck-check and shuffle flags must be T or F.");
                    continue;
                }
                int value;
                if (!Regex.IsMatch(args[index], @"^-?\d+$")
                    || !int.TryParse(args[index], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value)
                    || value < minimum[index] || value > maximum[index])
                    throw new ArgumentException("Native argument " + (index + 1) + " is outside its supported range.");
            }
        }

        private static void ValidateResourceEnvironment()
        {
            // The staged database and script archive define this room's inputs.
            // Native overrides would silently load data outside that receipt.
            string[] names = { "YGOPRO_EXPANSIONS", "YGOPRO_EXTRA_SCRIPT" };
            foreach (string name in names)
            {
                if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(name)))
                    throw new ArgumentException(name + " must be unset for an explicitly staged duel host.");
            }
        }

        private static void WatchExplicitParent()
        {
            string configured = Environment.GetEnvironmentVariable("SKYNET_DUEL_PARENT_PID");
            if (string.IsNullOrEmpty(configured))
                return;
            int processId;
            if (!int.TryParse(configured, NumberStyles.None, CultureInfo.InvariantCulture, out processId)
                || processId <= 0 || processId == Process.GetCurrentProcess().Id)
                throw new ArgumentException("SKYNET_DUEL_PARENT_PID must identify the owning supervisor process.");
            Process parent = Process.GetProcessById(processId);
            // Open and retain the exact process handle now so subsequent PID reuse
            // cannot make the child attach itself to a different process.
            IntPtr parentHandle = parent.Handle;
            if (parent.HasExited)
            {
                parent.Dispose();
                throw new InvalidOperationException("The owning supervisor has already exited.");
            }
            Thread monitor = new Thread(() =>
            {
                try
                {
                    parent.WaitForExit();
                }
                finally
                {
                    parent.Dispose();
                    // The OS reclaims this one process's native sockets and worker
                    // threads. Never enumerate or terminate any other process.
                    Environment.Exit(3);
                }
            });
            monitor.IsBackground = true;
            monitor.Name = "SkynetDuelOwner";
            monitor.Start();
        }
    }
}
