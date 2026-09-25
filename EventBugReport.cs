using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using Photon.Pun;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace REPOJP.StagePhysicsEvents;

internal sealed class EventBugReport : ILogListener
{
    internal const string IssuesUrl = "https://github.com/CapacityDown/StageFlux/issues/new";
    private readonly object _gate = new();
    private readonly Queue<string> _logs = new();
    private readonly HashSet<string> _privateValues = new(StringComparer.Ordinal);
    internal string LatestText { get; private set; } = "";
    internal string LatestPath { get; private set; } = "";

    internal void RememberPlayers()
    {
        lock (_gate)
        {
            if (PhotonNetwork.CurrentRoom != null) _privateValues.Add(PhotonNetwork.CurrentRoom.Name);
            foreach (var player in PhotonNetwork.PlayerList)
            {
                if (!string.IsNullOrWhiteSpace(player.NickName)) _privateValues.Add(player.NickName);
                if (!string.IsNullOrWhiteSpace(player.UserId)) _privateValues.Add(player.UserId);
            }
            if (_privateValues.Count > 1000) { _logs.Clear(); _privateValues.Clear(); }
        }
    }

    public void LogEvent(object sender, LogEventArgs args)
    {
        try
        {
            string body = args.Data?.ToString() ?? "";
            if (args.Source.SourceName != StagePhysicsEventsPlugin.PluginName && args.Source.SourceName != StagePhysicsEventsPlugin.PluginGuid &&
                !((args.Level & (LogLevel.Error | LogLevel.Fatal)) != 0 && body.Contains("REPOJP.StagePhysicsEvents"))) return;
            string text = $"[{DateTime.UtcNow:O}] [{args.Level}] {body}";
            lock (_gate)
            {
                _logs.Enqueue(text.Substring(0, Math.Min(2048, text.Length)));
                while (_logs.Count > 300) _logs.Dequeue();
            }
        }
        catch { /* Diagnostics must never interrupt gameplay or recursively log. */ }
    }

    internal void Create(EventMenuState state)
    {
        RememberPlayers();
        var report = new StringBuilder("# Stage Flux problem report\n\n## What happened?\n\n## Steps to reproduce\n1. \n\n## Expected result / actual result\n\n## Environment\n");
        report.AppendLine("Stage Flux: " + StagePhysicsEventsPlugin.PluginVersion);
        report.AppendLine("Game: " + Application.version + "\nUnity: " + Application.unityVersion);
        report.AppendLine("OS: " + SystemInfo.operatingSystem);
        report.AppendLine($"Resolution: {Screen.width} x {Screen.height}");
        report.AppendLine("Scene: " + SceneManager.GetActiveScene().name);
        report.AppendLine("Display status: " + state.Status);
        report.AppendLine("\n## Installed mods");
        foreach (var plugin in Chainloader.PluginInfos.Values)
        { if (report.Length > 12000) break; report.AppendLine(plugin.Metadata.GUID + " " + plugin.Metadata.Version); }
        report.AppendLine("\n## Local Stage Flux settings");
        foreach (var entry in StagePhysicsEventsPlugin.Instance.Config)
        {
            if (report.Length > 45000) break;
            string value = entry.Value.GetSerializedValue();
            report.AppendLine(entry.Key.Section + "." + entry.Key.Key + " = " + value.Substring(0, Math.Min(512, value.Length)));
        }
        report.AppendLine("\n## Recent Stage Flux messages");
        string[] privateValues;
        lock (_gate) { foreach (string line in _logs) report.AppendLine(line); privateValues = new List<string>(_privateValues).ToArray(); }
        string cleaned = Redact(report.ToString(), privateValues);
        string directory = Path.Combine(Paths.BepInExRootPath, "StageFluxReports");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, $"StageFlux-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.md");
        File.WriteAllText(path, cleaned, new UTF8Encoding(false));
        LatestPath = path; LatestText = cleaned;
    }

    private static string Redact(string text, string[] values)
    {
        Array.Sort(values, (left, right) => right.Length.CompareTo(left.Length));
        foreach (string value in values) if (!string.IsNullOrEmpty(value)) text = text.Replace(value, "[PLAYER/ROOM]");
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(home)) text = text.Replace(home, "[USER]");
        // Conservative masking of personal paths, email addresses, identifiers, and connection endpoints.
        foreach (string pattern in new[] { @"[A-Za-z]:[\\/][^\r\n]+", @"\b\d{17}\b", @"[\w.+-]+@[\w.-]+\.[A-Za-z]{2,}", @"\b(?:\d{1,3}\.){3}\d{1,3}(?::\d+)?\b", @"\b(?:token|password|secret)\s*[:=]\s*\S+" })
            text = Regex.Replace(text, pattern, "[REDACTED]", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(200));
        return text;
    }

    public void Dispose() { lock (_gate) { _logs.Clear(); _privateValues.Clear(); } }
}
