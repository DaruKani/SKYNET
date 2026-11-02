using Cysharp.Threading.Tasks;
using MDPro4.Net;
using MDPro4.Utility;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using UnityEngine;
using Process = System.Diagnostics.Process;
using ProcessStartInfo = System.Diagnostics.ProcessStartInfo;

namespace MDPro4.Servant
{
    public partial class SoloSelector
    {
        private Process skynetProcess;

        private static bool IsSkynetCommand(string command)
        {
            return Tools.SplitWithPreservedQuotes(command ?? string.Empty)
                .Any(argument => string.Equals(argument, "Deck=SKYNET", StringComparison.OrdinalIgnoreCase));
        }

        private void AppendSkynetBot()
        {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            if (Adapter.MasterDuel.DuelEngineBridge.UseMasterDuelDuelDll()
                || !File.Exists(Path.GetFullPath("Data/Skynet/SKYNET.exe"))) return;
            bots.Add(new BotInfo
            {
                id = "custom:SKYNET",
                name = "SKYNET",
                command = "Name=SKYNET Deck=SKYNET Dialog=default Chat=false",
                desc = string.Empty,
                flags = new[] { "SELECT_DECKFILE", "SUPPORT_MASTER_RULE_3", "SUPPORT_NEW_MASTER_RULE", "SUPPORT_MASTER_RULE_2020" },
                main0 = SoloBotCatalog.DefaultArtwork
            });
#endif
        }

        private bool TryStartSkynet(string[] args, int connection, bool solo, Thread server, int duelPort, int delay)
        {
            if (!args.Any(argument => string.Equals(argument, "Deck=SKYNET", StringComparison.OrdinalIgnoreCase)))
                return false;
            RunSkynetAsync(args, connection, solo, server, duelPort, delay).Forget();
            return true;
        }

        private async UniTask RunSkynetAsync(string[] args, int connection, bool solo, Thread server, int duelPort, int delay)
        {
            Process ownedProcess = null;
            Action quit = null;
            CancellationTokenRegistration destruction = default(CancellationTokenRegistration);
            var diagnostics = new StringBuilder();
            bool failed = false;
            try
            {
#if !UNITY_EDITOR_WIN && !UNITY_STANDALONE_WIN
                throw new PlatformNotSupportedException("The installed Skynet adapter currently supports Windows.");
#else
                if (Adapter.MasterDuel.DuelEngineBridge.UseMasterDuelDuelDll())
                    throw new InvalidOperationException("Skynet requires the ocgcore duel engine.");
                var token = this.GetCancellationTokenOnDestroy();
                if (delay > 0) await UniTask.Delay(delay, ignoreTimeScale: true, cancellationToken: token);
                if (!Program.Running || connection != TcpHelper.ConnectionVersion) return;
                if (skynetProcess != null)
                    throw new InvalidOperationException("The previous Skynet opponent is still retiring.");
                var directory = Path.GetFullPath("Data/Skynet");
                var executable = Path.Combine(directory, "SKYNET.exe");
                if (!File.Exists(executable)) throw new FileNotFoundException("Install Skynet before selecting it.", executable);
                var arguments = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var argument in args)
                {
                    var separator = argument.IndexOf('=');
                    if (separator > 0) arguments[argument.Substring(0, separator)] = argument.Substring(separator + 1);
                }
                string deck;
                if (!arguments.TryGetValue("DeckFile", out deck) || string.IsNullOrWhiteSpace(deck))
                    deck = Path.Combine(directory, "Decks", "AI_Skynet.ydk");
                else if (!Path.IsPathRooted(deck))
                    deck = Path.Combine(Program.PATH_DECK, deck.EndsWith(".ydk", StringComparison.OrdinalIgnoreCase) ? deck : deck + ".ydk");
                deck = Path.GetFullPath(deck);
                if (!File.Exists(deck)) throw new FileNotFoundException("Skynet opponent deck does not exist.", deck);
                arguments["DeckFile"] = deck;
                arguments["DbPath"] = Path.GetFullPath(Program.PATH_LOCALES + LocaleConfig.GetConfig() + "/cards.cdb");
                arguments["Dialog"] = "default";
                arguments["PolicyTraining"] = "false";
                arguments["Version"] = "4962";
                var start = new ProcessStartInfo
                {
                    FileName = executable,
                    WorkingDirectory = directory,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    Arguments = string.Join(" ", arguments.Select(pair => QuoteSkynetArgument(pair.Key + "=" + pair.Value)))
                };
                ownedProcess = new Process { StartInfo = start };
                System.Diagnostics.DataReceivedEventHandler capture = (sender, data) =>
                {
                    if (string.IsNullOrEmpty(data.Data)) return;
                    lock (diagnostics)
                    {
                        if (diagnostics.Length > 8192) diagnostics.Remove(0, diagnostics.Length - 4096);
                        diagnostics.AppendLine(data.Data);
                    }
                };
                ownedProcess.OutputDataReceived += capture;
                ownedProcess.ErrorDataReceived += capture;
                if (!ownedProcess.Start()) throw new IOException("Skynet did not start.");
                skynetProcess = ownedProcess;
                ownedProcess.BeginOutputReadLine();
                ownedProcess.BeginErrorReadLine();
                var captured = ownedProcess;
                quit = () => RetireSkynetProcess(captured);
                Application.quitting += quit;
                destruction = token.Register(quit);
                while (Program.Running && connection == TcpHelper.ConnectionVersion && !ownedProcess.HasExited)
                    await UniTask.Delay(100, ignoreTimeScale: true, cancellationToken: token);
                if (ownedProcess.HasExited)
                {
                    var deadline = Time.realtimeSinceStartup + 2f;
                    while (Program.Running && connection == TcpHelper.ConnectionVersion
                        && Program.instance?.room?.duelEnded != true && Time.realtimeSinceStartup < deadline)
                        await UniTask.Delay(100, ignoreTimeScale: true, cancellationToken: token);
                    failed = ownedProcess.ExitCode != 0
                        || (connection == TcpHelper.ConnectionVersion && Program.instance?.room?.duelEnded != true);
                    if (failed)
                    {
                        string detail;
                        lock (diagnostics) detail = diagnostics.ToString();
                        Debug.LogError("[Skynet] Opponent exited before duel completion. " + detail);
                    }
                }
                else
                {
                    // EOF normally retires the opponent. A superseded connection
                    // gets a bounded grace period before only our process is killed.
                    var deadline = Time.realtimeSinceStartup + 2f;
                    while (Program.Running && !ownedProcess.HasExited && Time.realtimeSinceStartup < deadline)
                        await UniTask.Delay(100, ignoreTimeScale: true, cancellationToken: token);
                }
#endif
            }
            catch (OperationCanceledException) { }
            catch (Exception exception)
            {
                failed = true;
                Debug.LogException(exception);
            }
            finally
            {
                if (quit != null) Application.quitting -= quit;
                destruction.Dispose();
                if (ownedProcess != null)
                {
                    RetireSkynetProcess(ownedProcess);
                    if (ReferenceEquals(skynetProcess, ownedProcess)) skynetProcess = null;
                    ownedProcess.Dispose();
                }
            }
            if (failed)
                await HandleWindBotFailureAsync(connection, solo, server, duelPort);
        }

        private static void RetireSkynetProcess(Process process)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill();
                    if (!process.WaitForExit(1000))
                        Debug.LogWarning("[Skynet] The owned opponent did not retire within one second.");
                }
            }
            catch (InvalidOperationException) { }
            catch (System.ComponentModel.Win32Exception exception)
            {
                Debug.LogWarning("[Skynet] Could not retire the owned opponent: " + exception.Message);
            }
        }

        private static string QuoteSkynetArgument(string argument)
        {
            // Windows CRT quoting, including trailing slashes and embedded quotes.
            var result = new StringBuilder("\"");
            int slashes = 0;
            foreach (char character in argument)
            {
                if (character == '\\') { slashes++; continue; }
                result.Append('\\', character == '"' ? slashes * 2 + 1 : slashes);
                slashes = 0;
                result.Append(character);
            }
            result.Append('\\', slashes * 2);
            return result.Append('"').ToString();
        }
    }
}
