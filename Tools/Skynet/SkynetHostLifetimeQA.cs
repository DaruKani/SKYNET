// Compile as a disposable Editor probe. Do not import into production Assets.
// Start a native_smoke.py --server-child beforehand in an isolated fixture cwd,
// then pass its announced port. The caller owns that native server process.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using MDPro4;
using MDPro4.Net;
using MDPro4.Servant;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

public static class SkynetHostLifetimeQA
{
    public static object Run(int port, string directory)
    {
        const string guard = "Codex.Skynet.HostLifetime.Active";
        var p = MDPro4.Program.instance;
        var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        var versionProperty = typeof(TcpHelper).GetProperty("ConnectionVersion", flags);
        var processField = typeof(SoloSelector).GetField("skynetProcess", flags);
        var launch = typeof(SoloSelector).GetMethod("RunSkynetAsync", flags);
        if (!Application.isPlaying || p == null || !p.IsInitialized || p.currentServant != p.menu
            || p.solo == null || TcpHelper.tcpClient != null || TcpHelper.deck != null
            || TcpHelper.onDisConnected || !TcpHelper.canJoin || TcpHelper.IsLanConnection
            || YgoServer.serverThread != null || OcgCore.cards.Count != 0
            || SessionState.GetBool(guard, false) || processField.GetValue(p.solo) != null)
            throw new InvalidOperationException("Requires initialized idle Main Menu with no borrowed transport or bot.");
        if (MDPro4.Adapter.MasterDuel.DuelEngineBridge.UseMasterDuelDuelDll())
            throw new InvalidOperationException("Select ocgcore normally before the probe; this fixture never changes engine settings.");
        if (port < 1 || port > 65535) throw new ArgumentOutOfRangeException("port");
        var root = Path.GetFullPath(directory);
        var boundary = Path.GetFullPath("Library/Codex_QA/Skynet") + Path.DirectorySeparatorChar;
        if (!root.StartsWith(boundary, StringComparison.OrdinalIgnoreCase) || Directory.Exists(root))
            throw new InvalidOperationException("Use a fresh run inside Library/Codex_QA/Skynet.");
        Directory.CreateDirectory(root);
        var files = new[] { "Data/config.conf", "Data/Settings.json" };
        var before = new Dictionary<string, string>();
        foreach (var path in files) before[path] = Hash(path);
        int version = (int)versionProperty.GetValue(null, null);
        var checks = new List<object>();
        var logs = new List<object>();
        string phase = "Launching", failure = null;
        double began = EditorApplication.timeSinceStartup, phaseAt = began;
        bool retired = false, finished = false;
        int processId = -1;
        Process captured = null;
        Task task = null;
        Action<string, bool> check = (name, pass) =>
        {
            checks.Add(new { name, pass });
            if (!pass) throw new InvalidOperationException(name);
        };
        Application.LogCallback logger = (message, stack, type) =>
        {
            if (type != LogType.Log) logs.Add(new { type = type.ToString(), message, stack });
        };
        Action write = () => File.WriteAllText(Path.Combine(root, "report.json"),
            JsonConvert.SerializeObject(new
            {
                phase, failure, processId, port, retired, finished, checks, logs,
                taskCompleted = task != null && task.IsCompleted,
                seconds = EditorApplication.timeSinceStartup - began,
                scope = "Actual host process launch and superseded-connection cleanup; not a visual or complete-duel check."
            }, Formatting.Indented));
        EditorApplication.CallbackFunction tick = null;
        Action finish = () =>
        {
            if (finished) return;
            finished = true;
            EditorApplication.update -= tick;
            Application.logMessageReceived -= logger;
            SessionState.SetBool(guard, false);
            write();
        };
        Action retireIdleGeneration = () =>
        {
            if (retired) return;
            // Never close a transport installed by unrelated work during QA.
            if (TcpHelper.tcpClient == null && (int)versionProperty.GetValue(null, null) == version)
                TcpHelper.CloseConnection();
            retired = true;
        };
        tick = () =>
        {
            try
            {
                if (!Application.isPlaying)
                    throw new InvalidOperationException("Play stopped before cleanup acceptance.");
                if (EditorApplication.timeSinceStartup - began > 12)
                    throw new TimeoutException("Host launch/retirement exceeded twelve seconds.");
                if (phase == "Launching")
                {
                    captured = (Process)processField.GetValue(p.solo);
                    if (captured != null)
                    {
                        processId = captured.Id;
                        check("Actual standalone process launched", !captured.HasExited);
                        check("Executable identity is Skynet", Path.GetFileName(captured.StartInfo.FileName) == "SKYNET.exe");
                        check("Published-play training is disabled", captured.StartInfo.Arguments.Contains("PolicyTraining=false"));
                        phase = "Lobby";
                        phaseAt = EditorApplication.timeSinceStartup;
                    }
                }
                else if (phase == "Lobby" && EditorApplication.timeSinceStartup - phaseAt > 1)
                {
                    check("Opponent remains alive in native lobby", !captured.HasExited);
                    retireIdleGeneration();
                    check("Connection generation retired", (int)versionProperty.GetValue(null, null) != version);
                    phase = "Retiring";
                }
                else if (phase == "Retiring" && task.IsCompleted)
                {
                    task.GetAwaiter().GetResult();
                    check("Production owner released its Process", processField.GetValue(p.solo) == null);
                    bool alive;
                    try { using (var remaining = Process.GetProcessById(processId)) alive = !remaining.HasExited; }
                    catch (ArgumentException) { alive = false; }
                    check("Exact launched PID exited", !alive);
                    check("Main Menu and transport preserved", p.currentServant == p.menu && TcpHelper.tcpClient == null);
                    foreach (var path in files) check("Unchanged " + path, Hash(path) == before[path]);
                    check("No Unity warnings or errors", logs.Count == 0);
                    phase = "Passed";
                    finish();
                }
            }
            catch (Exception exception)
            {
                failure = exception.ToString();
                retireIdleGeneration();
                if (captured != null)
                {
                    try { if (!captured.HasExited) captured.Kill(); }
                    catch (InvalidOperationException) { }
                }
                phase = "Failed";
                finish();
            }
        };
        SessionState.SetBool(guard, true);
        Application.logMessageReceived += logger;
        EditorApplication.update += tick;
        try
        {
            var args = new[] { "Deck=SKYNET", "Name=SKYNETQA", "Host=127.0.0.1",
                "Port=" + port, "Hand=1", "Chat=false" };
            task = ((UniTask)launch.Invoke(p.solo, new object[] { args, version, false, null, port, 0 })).AsTask();
            write();
        }
        catch
        {
            retireIdleGeneration();
            finish();
            throw;
        }
        return new { started = true, root, port, connectionVersion = version };
    }

    private static string Hash(string path)
    {
        if (!File.Exists(path)) return null;
        using (var stream = File.OpenRead(path))
        using (var sha = SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(stream));
    }
}
