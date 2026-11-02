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

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibraryEx(string filename, IntPtr file, uint flags);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr module, string name);

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
