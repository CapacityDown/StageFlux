using System.Globalization;
using System.Text.RegularExpressions;
using Mono.Cecil;
using REPOJP.StagePhysicsEvents;

internal static class EventConfigOrderChecks
{
    private sealed record Entry(string Section, string Key, object Value);

    internal static void Run(string root, string[] dependencies, Action<bool, string> check)
    {
        string models = File.ReadAllText(Path.Combine(root, "EventModels.cs"));
        string individual = models.Split("IndividualEffects = new[]", StringSplitOptions.None)[1].Split("};")[0];
        var names = Regex.Matches(models, "StageEffect\\.(\\w+) => \"([^\"]+)\"")
            .ToDictionary(match => match.Groups[1].Value, match => match.Groups[2].Value);
        string[] guide = Regex.Matches(individual, "StageEffect\\.(\\w+)")
            .Select(match => names.GetValueOrDefault(match.Groups[1].Value, match.Groups[1].Value)).ToArray();
        var order = new EventConfigOrder(guide);
        string[] common = { "General", "Timing", "Targets", "Safety", "Notifications", "HUD" };
        string[] expectedSections = common.Concat(guide).ToArray();
        check(guide.Length == 49 && guide.Distinct().Count() == 49, "All 49 guide event sections are unique");
        check(order.Sort(expectedSections.Reverse(), value => value, _ => "Enabled").SequenceEqual(expectedSections),
            "Common settings followed by exact in-game guide order, not source binding or alphabetical order");
        for (int index = 0; index < expectedSections.Length; index++)
            check(order.SectionRank(expectedSections[index]) == index, "Section rank: " + expectedSections[index]);

        string source = string.Join("\n", new[] { "StagePhysicsConfig.cs", "ExtendedEventConfig.cs", "SunstoneEventConfig.cs" }
            .Select(file => File.ReadAllText(Path.Combine(root, file))));
        var removed = Regex.Matches(source, "(?:config.Remove\\(new ConfigDefinition\\(|RemoveObsoleteConfigEntry\\(config,\\s*)\"([^\"]+)\",\\s*\"([^\"]+)\"")
            .Select(match => (Section: match.Groups[1].Value, Key: match.Groups[2].Value)).ToHashSet();
        var current = Regex.Matches(source, "(?:Bind(?:Bool|Int)\\(\\s*config,\\s*|config.Bind\\(\\s*)\"([^\"]+)\",\\s*\"([^\"]+)\"")
            .Select(match => (Section: match.Groups[1].Value, Key: match.Groups[2].Value))
            .Where(pair => !removed.Contains(pair) && expectedSections.Contains(pair.Section))
            .Concat(guide.SelectMany(section => new[] { (Section: section, Key: "Enabled"), (Section: section, Key: "ChancePercent") }))
            .Concat(new[] { "Recharge Cloud", "Rockfall", "Arcane Burst" }.SelectMany(section =>
                new[] { "SpawnCountMin", "SpawnCountMax", "SpawnIntervalSeconds", "MaximumActiveInstances", "MinimumPlayerDistance" }
                .Select(key => (Section: section, Key: key))))
            .Distinct().ToArray();
        check(current.Length == 292, "Audit covers all 292 current settings (found " + current.Length + ")");
        foreach (var pair in current)
            check(EventConfigOrder.KeyRank(pair.Section, pair.Key) != int.MaxValue, "Explicit setting order: " + pair.Section + "." + pair.Key);
        check(!current.Any(pair => pair.Key is "ConfigRevision" or "MapDoorsEnabled" or "IndestructibleReleaseDelaySeconds"),
            "Internal and superseded settings remain absent");

        Entry[] entries = current.Select((pair, index) => new Entry(pair.Section, pair.Key, index)).ToArray();
        Entry[] sorted = order.Sort(entries, entry => entry.Section, entry => entry.Key);
        check(sorted.Select(entry => entry.Section).Distinct().SequenceEqual(expectedSections), "Every live section keeps its guide position");
        check(sorted.Length == entries.Length && sorted.Distinct().Count() == entries.Length &&
            sorted.All(entry => entries.Any(original => ReferenceEquals(original, entry))), "Sorting preserves every setting object and value");
        check(entries.Select(entry => (int)entry.Value).SequenceEqual(Enumerable.Range(0, entries.Length)), "Sorting does not mutate its input");
        foreach (string section in guide)
        {
            string[] keys = sorted.Where(entry => entry.Section == section).Select(entry => entry.Key).ToArray();
            check(keys.Take(2).SequenceEqual(new[] { "Enabled", "ChancePercent" }), section + " begins with enable/chance");
            foreach (string key in keys.Where(key => key.EndsWith("Min")))
                check(Array.IndexOf(keys, key[..^3] + "Max") == Array.IndexOf(keys, key) + 1, section + " keeps min/max adjacent");
            foreach (string key in keys.Where(key => key.EndsWith("MinSeconds")))
                check(Array.IndexOf(keys, key.Replace("MinSeconds", "MaxSeconds")) == Array.IndexOf(keys, key) + 1, section + " keeps time ranges adjacent");
        }
        var rng = new Random(72451);
        for (int iteration = 0; iteration < 20; iteration++)
            check(order.Sort(entries.OrderBy(_ => rng.Next()), entry => entry.Section, entry => entry.Key).SequenceEqual(sorted),
                "Order independent of dictionary insertion order " + iteration);
        CultureInfo saved = CultureInfo.CurrentCulture;
        try
        {
            foreach (string locale in new[] { "en-US", "ja-JP", "ar-SA", "tr-TR" })
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(locale);
                check(order.Sort(entries.Reverse(), entry => entry.Section, entry => entry.Key).SequenceEqual(sorted), "Stable order in " + locale);
            }
        }
        finally { CultureInfo.CurrentCulture = saved; }
        var unknown = new[] { new Entry("Unknown", "Foo", 1), new Entry("Feather", "FutureSetting", 2), new Entry("Feather", "Enabled", true) };
        check(order.Sort(unknown, entry => entry.Section, entry => entry.Key).SequenceEqual(new[] { unknown[2], unknown[1], unknown[0] }), "Unknown settings remain visible after known settings");
        check(order.Sort(Array.Empty<Entry>(), entry => entry.Section, entry => entry.Key).Length == 0, "Empty settings safe");

        string hook = File.ReadAllText(Path.Combine(root, "StageFluxConfigMenu.cs"));
        check(hook.Contains("StageEffectSet.IndividualEffects.Select") && hook.Contains("StageEffectSet.Format(effect, string.Empty)"), "Runtime guide and config share the event list and names");
        check(hook.Contains("ReferenceEquals(entry.ConfigFile, _config)") && hook.Contains("__1.Any("), "Every entry must belong to Stage Flux; other mod and mixed arrays are unchanged");
        check(hook.Contains("__1 = OrderEntries(__1)") && !hook.Contains(".BoxedValue =") && !hook.Contains(".Bind("), "UI adapter only replaces the display array");
        check(hook.Contains("catch (Exception") && hook.Contains("if (type == null) return") && hook.Contains("method.ReturnType != typeof(void)"), "Absent or incompatible REPOConfig cannot disable gameplay");
        check(File.ReadAllText(Path.Combine(root, "EventMenuState.cs")).Contains("StageFluxConfigMenu.OrderEntries("), "Guide Details uses the same ordering");
        string plugin = File.ReadAllText(Path.Combine(root, "StagePhysicsEventsPlugin.cs"));
        int start = plugin.IndexOf("private void Start()", StringComparison.Ordinal);
        int install = plugin.IndexOf("StageFluxConfigMenu.Install(", StringComparison.Ordinal);
        check(start >= 0 && install > start && install < plugin.IndexOf("private void OnDestroy()", StringComparison.Ordinal),
            "Optional REPOConfig hook installs in Start after all plugins have loaded, independent of Awake order");
        check(source.Contains("StageActivationChancePercent\", 50, 0, 100") && source.Contains("DefaultEventChancePercent = 6"), "Chance defaults unchanged");

        string? dependency = dependencies.FirstOrDefault(path => Path.GetFileName(path) == "REPOConfig.dll");
        check(dependency != null, "Installed REPOConfig supplied for the display contract audit");
        using var module = ModuleDefinition.ReadModule(dependency!);
        var method = module.Types.Single(type => type.FullName == "REPOConfig.ConfigMenu").Methods.Single(method => method.Name == "CreateModEntries");
        check(method.IsStatic && method.IsPrivate && method.ReturnType.FullName == "System.Void" &&
            method.Parameters.Select(parameter => parameter.ParameterType.FullName).SequenceEqual(new[] { "MenuLib.MonoBehaviors.REPOPopupPage", "BepInEx.Configuration.ConfigEntryBase[]" }),
            "Installed REPOConfig matches reflection/Harmony argument contract");
        var calls = method.Body.Instructions.Select(instruction => instruction.Operand).OfType<MethodReference>().ToArray();
        check(calls.Any(call => call.DeclaringType.FullName == "System.Linq.Enumerable" && call.Name == "GroupBy") &&
            !calls.Any(call => call.Name.StartsWith("OrderBy") || call.Name == "Sort"), "REPOConfig preserves the sorted input section and entry order");
        using var mod = ModuleDefinition.ReadModule(Path.Combine(root, "bin/Release/netstandard2.1/StagePhysicsEvents.dll"));
        check(!mod.AssemblyReferences.Any(reference => reference.Name is "REPOConfig" or "RoleShuffle" or "StageRoles"), "No additional compile-time mod dependency");
    }
}
