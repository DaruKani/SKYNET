using System;
using System.IO;

namespace WindBot
{
    public static class Logger
    {
        private static readonly object _syncRoot = new object();

        // Each server-mode bot is driven by its own worker thread, so its log context must not be shared.
        [ThreadStatic]
        private static Func<string> _contextProvider;

        private static string _logFilePath;

        public static void SetLogFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return;

            lock (_syncRoot)
            {
                try
                {
                    string absolutePath = Path.GetFullPath(filePath);
                    string directory = Path.GetDirectoryName(absolutePath);
                    if (!string.IsNullOrEmpty(directory))
                        Directory.CreateDirectory(directory);
                    _logFilePath = absolutePath;
                    File.AppendAllText(_logFilePath, Environment.NewLine +
                        "=== WindBot session " +
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " ===" +
                        Environment.NewLine);
                }
                catch (Exception error)
                {
                    _logFilePath = null;
                    Console.Error.WriteLine("Unable to open WindBot log file: " +
                        error.Message);
                }
            }
        }

        public static void SetContext(Func<string> contextProvider)
        {
            _contextProvider = contextProvider;
        }

        public static void ClearContext()
        {
            _contextProvider = null;
        }

        public static void WriteLine(string message)
        {
            lock (_syncRoot)
            {
                string line = "[" + DateTime.Now.ToString("yy-MM-dd HH:mm:ss") +
                    "] " + message;
                Console.WriteLine(line);
                AppendFile(line);
            }
        }

        public static void DebugWriteLine(string message, bool printStackTrace = false)
        {
#if DEBUG
            lock (_syncRoot)
            {
                string line = "[" + DateTime.Now.ToString("yy-MM-dd HH:mm:ss") +
                    "] " + message;
                Console.WriteLine(line);
                AppendFile(line);
                if (printStackTrace)
                {
                    string stackTrace = new System.Diagnostics.StackTrace(1, true).ToString();
                    Console.WriteLine(stackTrace);
                    AppendFile(stackTrace);
                }
            }
#endif
        }

        public static void WriteErrorLine(string message)
        {
            WriteErrorLine(message, null, new System.Diagnostics.StackTrace(1, true).ToString());
        }

        public static void WriteErrorLine(string message, Exception exception)
        {
            WriteErrorLine(message, exception, null);
        }

        private static void WriteErrorLine(string message, Exception exception, string callStack)
        {
            lock (_syncRoot)
            {
                string line = "[" + DateTime.Now.ToString("yy-MM-dd HH:mm:ss") +
                    "] " + message;
                Console.BackgroundColor = ConsoleColor.Red;
                Console.ForegroundColor = ConsoleColor.White;
                Console.Error.WriteLine(line);
                Console.ResetColor();
                AppendFile(line);

                string context = _contextProvider?.Invoke();
                if (!string.IsNullOrEmpty(context))
                {
                    Console.Error.WriteLine("Context: " + context);
                    AppendFile("Context: " + context);
                }
                if (exception != null)
                {
                    Console.Error.WriteLine(exception);
                    AppendFile(exception.ToString());
                }
                else if (!string.IsNullOrEmpty(callStack))
                {
                    Console.Error.WriteLine("Call stack:");
                    Console.Error.WriteLine(callStack);
                    AppendFile("Call stack:" + Environment.NewLine + callStack);
                }
            }
        }

        private static void AppendFile(string text)
        {
            if (string.IsNullOrEmpty(_logFilePath))
                return;
            try
            {
                File.AppendAllText(_logFilePath, text + Environment.NewLine);
            }
            catch (Exception error)
            {
                _logFilePath = null;
                Console.Error.WriteLine("WindBot file logging disabled: " + error.Message);
            }
        }
    }
}
