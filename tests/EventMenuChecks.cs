using System.Globalization;
using REPOJP.StagePhysicsEvents;

internal static class EventMenuChecks
{
    internal static int Run(string root)
    {
        int checks = 0;
        void Check(bool ok, string name) { checks++; if (!ok) throw new Exception(name); }
        Check(EventMenuLayout.LineHeight * 3 == 97.5f, "Roles-compatible three-line wheel distance");
        Check(EventMenuLayout.NavigationTop == 272 && EventMenuLayout.NavigationStep == 32, "Navigation geometry");
        Check(EventMenuLayout.FirstPadding == 20 && EventMenuLayout.RowPadding == 3 && EventMenuLayout.RowSpacing == 1.5f, "Row spacing");
        Check(EventMenuLayout.BodyFont == 18 && EventMenuLayout.TitleFont == 20 && EventMenuLayout.GuideTitleHeight == 64, "Text and guide icon geometry");
        foreach (int width in new[] { 1, 4, 8, 20, 260, 520 })
        foreach (string text in new[] { "Hello world", "No host settings available.", "One\n\nTwo", "https://example.invalid/long/path", "A🙂B", "  leading  and  trailing  " })
        {
            List<string> lines = EventMenuLayout.Wrap(text, width, s => new StringInfo(s).LengthInTextElements);
            Check(lines.All(l => new StringInfo(l).LengthInTextElements <= width), "No clipped body line");
            Check(string.Concat(lines).Replace(" ", "") == text.Replace(" ", "").Replace("\n", ""), "Wrapping preserves all text");
        }
        long mask = (1L << 49) - 1;
        var rows = new Dictionary<long, EventGuideDetails>();
        for (int i = 0; i < 49; i++) rows.Add(1L << i, new EventGuideDetails("Chance: 6%\nON\tUnicode 日本語", "Conditional"));
        string encoded = EventGuideSnapshot.Encode(3, rows);
        Check(EventGuideSnapshot.TryDecode(encoded, 3, mask, out var decoded) && decoded.Count == 49, "Full guide snapshot roundtrip");
        Check(decoded[1].Text == rows[1].Text, "Snapshot text roundtrip");
        foreach (string? bad in new[] { null, "", "2:3", "1:3\n1\t!!!\tQQ==", encoded.Replace("1:3\n", "1:4\n"), encoded + "\n" + encoded.Split('\n')[1], new string('x', EventGuideSnapshot.MaxLength + 1) })
            Check(!EventGuideSnapshot.TryDecode(bad, 3, mask, out var rejected) && rejected.Count == 0, "Invalid, stale or partial guide rejected atomically");
        Check(!EventGuideSnapshot.TryDecode(encoded, 0, mask, out _), "No master means guide unavailable");
        Check(!EventGuideSnapshot.TryDecode("1:3\n1\tQQ==\tQQ==", 3, mask, out _), "Missing event rows are not accepted");
        Check(!EventGuideSnapshot.TryDecode("1:3\n3\tQQ==\tQQ==", 3, 3, out _), "Composite event IDs rejected");
        Check(!EventGuideSnapshot.TryDecode("1:3\n1\t/w==\tQQ==", 3, 1, out _), "Invalid UTF-8 rejected");
        string temporary = Path.Combine(Path.GetTempPath(), "StageFlux-MenuChecks-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporary);
        try
        {
            string path = Path.Combine(temporary, "presets.json");
            var presets = new CustomEventPresets(path);
            Check(presets.Available && presets.Get(0) == null, "Unused slots differ from all-off");
            Check(presets.TrySave(0, Array.Empty<string>(), false), "All-off preset can be saved");
            Check(!presets.TrySave(0, new[] { "Feather" }, false), "Overwrite requires confirmation");
            Check(presets.Get(0)!.Length == 0, "Rejected overwrite preserves data");
            Check(presets.TrySave(1, new[] { "Feather", "ZeroGravity", "Feather", "RemovedFutureEvent" }, false), "Stable event names saved");
            Check(presets.TrySave(2, new[] { "Rockfall" }, false), "Third custom slot available");
            var reloaded = new CustomEventPresets(path);
            Check(reloaded.Get(0)!.Length == 0 && reloaded.Get(1)!.Length == 3 && reloaded.Get(2)![0] == "Rockfall", "All slots persist across restart");
            string[] copy = reloaded.Get(2)!; copy[0] = "Feather";
            Check(reloaded.Get(2)![0] == "Rockfall", "Callers cannot modify stored selection");
            Check(reloaded.TrySave(0, new[] { "Feather" }, true) && File.Exists(path + ".bak"), "Confirmed overwrite retains backup");
            Check(!reloaded.TrySave(-1, Array.Empty<string>(), true) && !reloaded.TrySave(3, Array.Empty<string>(), true), "Invalid slots rejected");
            foreach (string contents in new[] { "bad json", "{\"version\":2,\"slots\":[null,null,null]}", "{\"version\":1,\"slots\":[null]}" })
            {
                File.WriteAllText(path, contents);
                var invalid = new CustomEventPresets(path);
                Check(!invalid.Available && !invalid.TrySave(0, Array.Empty<string>(), true), "Corrupt/future presets never overwritten");
                Check(File.ReadAllText(path) == contents, "Invalid original preserved");
            }
            Check(!Directory.EnumerateFiles(temporary, "*.tmp").Any(), "No pending preset writes remain");
        }
        finally { Directory.Delete(temporary, true); }
        string menu = File.ReadAllText(Path.Combine(root, "EventMenu.cs"));
        foreach (string required in new[] { "currentEnabled ? View.Current : View.Guide", "GuideView.Overview", "GuideView.Details", "GuideView.Eligibility", "Confirm overwrite: ", "_settings.TryReadDetails", "_settings.SaveCustomPreset", "_settings.ApplyCustomPreset", "selected == pair.Key ? \"> \"", "_view != view", "_dialogClosedFrame == Time.frameCount", "EventMenuLayout.Wrap", "row.Visual.Configure", "_icons.Get(entry.Icon, entry.Off)", "state.RemainingSeconds <= 10" })
            Check(menu.Contains(required), "UI lifecycle/format wiring: " + required);
        string state = File.ReadAllText(Path.Combine(root, "EventMenuState.cs"));
        Check(!state.Contains("RollEffects(") && !state.Contains("EnsureAvailable(") && !state.Contains("Random.Range"), "Guide is RNG-free and never prepares effects");
        Check(state.Contains("if (!CanEdit || Presets.Get(slot)") && state.Contains("if (!CanEdit || !TryRead(out StageEffect enabled"), "Custom preset actions recheck authority");
        Check(state.Contains("OnMasterClientSwitched(Player newMasterClient) => ResetDisplayState()"), "Host-change cache invalidation");
        return checks;
    }
}
