using System.Text.RegularExpressions;
using Mono.Cecil;

internal static class Game050Contracts
{
    internal static void Run(string root, string gamePath, Action<bool, string> check, params string[] dependencies)
    {
        using var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(Path.GetDirectoryName(gamePath));
        resolver.AddSearchDirectory(Path.Combine(root, "lib"));
        var parameters = new ReaderParameters { AssemblyResolver = resolver };
        using var game = ModuleDefinition.ReadModule(gamePath, parameters);
        using var mod = ModuleDefinition.ReadModule(Path.Combine(root, "bin/Release/netstandard2.1/StagePhysicsEvents.dll"), parameters);
        var failures = new List<string>();
        void Verify(bool passed, string message)
        {
            if (!passed) failures.Add(message);
            else check(true, message);
        }
        TypeDefinition? FindType(string name) => game.GetTypes().FirstOrDefault(t => t.FullName.Replace('/', '.') == name);

        // Resolve compiled calls in both our DLL and the installed menu dependency.
        // Compiling Stage Flux alone cannot detect binary breaks in MenuLib.
        foreach (string path in new[] { mod.FileName, Path.Combine(root, "lib/MenuLib.dll") }.Concat(dependencies))
        {
            using var module = ModuleDefinition.ReadModule(path, parameters);
            foreach (var member in module.GetMemberReferences())
            {
                string scope = member.DeclaringType.Scope.Name;
                if (scope != "Assembly-CSharp" && scope != "MenuLib" && !scope.StartsWith("Unity") && !scope.StartsWith("Photon")) continue;
                try
                {
                    bool found = member switch {
                        MethodReference method => method.Resolve() != null,
                        FieldReference field => field.Resolve() != null,
                        _ => true
                    };
                    Verify(found, $"{module.Name}: unresolved game member {member.FullName}");
                }
                catch (Exception e) { Verify(false, $"{module.Name}: {member.FullName}: {e.Message}"); }
            }
        }

        // String-based reflection needs a separate check against the real game.
        foreach (string file in Directory.EnumerateFiles(root, "*.cs"))
        {
            string source = File.ReadAllText(file);
            foreach (Match match in Regex.Matches(source,
                "AccessTools\\.(Field|Method)\\(typeof\\(([^)]+)\\),\\s*\"([^\"]+)\""))
            {
                var type = FindType(match.Groups[2].Value);
                if (type == null) { Verify(false, $"Reflection type missing: {match.Groups[2].Value}"); continue; }
                string name = match.Groups[3].Value;
                bool found = match.Groups[1].Value == "Field" ? type.Fields.Any(f => f.Name == name) : type.Methods.Any(m => m.Name == name);
                Verify(found, $"Reflection target missing: {type.Name}.{name}");
            }
        }

        // Harmony injects original arguments by name, not just by position/type.
        foreach (var type in mod.GetTypes())
        foreach (var method in type.Methods)
        {
            if (!method.CustomAttributes.Any(a => a.AttributeType.FullName is "HarmonyLib.HarmonyPrefix" or "HarmonyLib.HarmonyPostfix")) continue;
            var targets = method.CustomAttributes.Concat(type.CustomAttributes)
                .Where(a => a.AttributeType.FullName == "HarmonyLib.HarmonyPatch");
            foreach (var target in targets)
            {
                var targetType = target.ConstructorArguments.Select(a => a.Value).OfType<TypeReference>().FirstOrDefault();
                string? targetName = target.ConstructorArguments.Select(a => a.Value).OfType<string>().FirstOrDefault();
                if (targetType == null || targetName == null) continue;
                var original = FindType(targetType.FullName)?.Methods.Where(m => m.Name == targetName).ToArray() ?? Array.Empty<MethodDefinition>();
                Verify(original.Length == 1, $"Harmony target missing/ambiguous: {targetType.FullName}.{targetName}");
                if (original.Length != 1) continue;
                foreach (var parameter in method.Parameters.Where(p => !p.Name.StartsWith("__")))
                {
                    Verify(original[0].Parameters.Any(p => p.Name == parameter.Name &&
                        p.ParameterType.FullName.TrimEnd('&') == parameter.ParameterType.FullName.TrimEnd('&')),
                        $"Harmony parameter mismatch: {method.Name}: {parameter.Name}");
                }
            }
        }

        void Rpc(string typeName, string name, params string[] arguments)
        {
            var method = FindType(typeName)?.Methods.FirstOrDefault(m => m.Name == name);
            Verify(method != null && method.CustomAttributes.Any(a => a.AttributeType.FullName == "Photon.Pun.PunRPC") &&
                method.Parameters.Where(p => p.ParameterType.Name != "PhotonMessageInfo").Select(p => p.ParameterType.Name).SequenceEqual(arguments),
                $"Vanilla RPC payload mismatch: {typeName}.{name}");
        }
        Rpc("ItemBattery", "BatteryFullPercentChangeRPC", "Int32", "Boolean", "Single", "Boolean");
        Rpc("PlayerAvatar", "ChatMessageSendRPC", "String", "Boolean", "String");
        Rpc("PlayerAvatar", "ForceImpulseRPC", "Vector3");
        Rpc("PlayerAvatar", "SpawnRPC", "Vector3", "Quaternion", "Boolean");
        Rpc("PlayerTumble", "TumbleRequestRPC", "Boolean", "Boolean", "Int32");
        Rpc("PlayerTumble", "TumbleSetRPC", "Boolean", "Boolean", "Int32");
        Rpc("PlayerTumble", "TumbleForceRPC", "Vector3");
        Rpc("PlayerTumble", "TumbleTorqueRPC", "Vector3");
        Rpc("PlayerTumble", "TumbleOverrideTimeRPC", "Single");
        Rpc("PlayerTumble", "ImpactHurtSetRPC", "Single", "Int32");
        Rpc("SemiAreaOfEffect", "CreateAffectsRPC", "Int32[]", "Single[]", "Int32[]", "Double[]", "Vector3", "Vector3");
        Rpc("ItemMine", "StateSetRPC", "Int32");
        Rpc("ValuableObject", "DollarValueSetRPC", "Single");
        Rpc("PhysGrabObjectImpactDetector", "HealRPC", "Single", "Vector3");
        Rpc("PhysGrabObjectImpactDetector", "ImpactLightRPC", "Single", "Vector3");
        Rpc("PhysGrabObjectImpactDetector", "ImpactMediumRPC", "Single", "Vector3");
        Rpc("PhysGrabObjectImpactDetector", "ImpactHeavyRPC", "Single", "Vector3");
        Rpc("PhysGrabObjectImpactDetector", "BreakRPC", "Single", "Vector3", "Int32", "Boolean");
        Rpc("PhysGrabObjectImpactDetector", "DestroyObjectRPC", "Boolean");
        Rpc("ValuableEgg", "SetStateRPC", "EggState");
        Rpc("ValuableCar", "UpdateStateRPC", "State");
        Rpc("ValuablePlane", "UpdateStateRPC", "Int32");
        Rpc("ScreamDollValuable", "SetStateRPC", "States");
        Rpc("TrafficLightValuable", "SetStateRPC", "States");

        using var manifest = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "package/manifest.json")));
        Verify(manifest.RootElement.GetProperty("version_number").GetString() == "5.0.0", "Manifest release version");
        string[] required = manifest.RootElement.GetProperty("dependencies").EnumerateArray().Select(e => e.GetString()!).ToArray();
        Verify(required.Contains("REPOModding-MenuLib-2.6.0") && required.Contains("REPOModding-REPOConfig-1.2.7"),
            "Manifest requires Unity 6 compatible menu dependencies");
        Verify(!required.Any(d => d.Contains("RoleShuffle") || d.Contains("EliteEnemyVariants")), "Integration mods remain optional");
        var plugin = mod.GetTypes().Single(t => t.Name == "StagePhysicsEventsPlugin");
        var identity = plugin.CustomAttributes.Single(a => a.AttributeType.FullName == "BepInEx.BepInPlugin");
        Verify((string)identity.ConstructorArguments[0].Value == "REPOJP.StagePhysicsEvents", "Keep the configuration/plugin identity");
        Verify((string)identity.ConstructorArguments[2].Value == "5.0.0", "Plugin release version");
        var menuDependency = plugin.CustomAttributes.Single(a => a.AttributeType.FullName == "BepInEx.BepInDependency");
        Verify((string)menuDependency.ConstructorArguments[1].Value == "2.6.0", "Refuse incompatible old MenuLib at load time");

        Verify(FindType("MenuScrollBox")?.Fields.Any(f => f.Name == "scrollWheelStep" && f.FieldType.Name == "Single") == true,
            "Native wheel-step setting exists");
        Verify(!File.ReadAllText(Path.Combine(root, "EventMenuScroll.cs")).Contains("HarmonyTranspiler"),
            "Events scroll must not depend on the old menu IL pattern");
        Verify(File.ReadAllText(Path.Combine(root, "VanillaEventNotifier.cs")).Contains("ChatMessageSend(message, \"en-US\")"),
            "Event announcements use English TTS independently of game language");
        Verify(File.ReadAllText(Path.Combine(root, "ExtendedEventAdapter.cs")).Contains("battery.BatteryFullPercentChange(battery.BatteryBarsGet(), false)"),
            "Battery Drain uses native exact-charge synchronization");
        Verify(File.ReadAllText(Path.Combine(root, "AdditionalOrbEffectAdapter.cs")).Contains("battery.ReceiveCharge(_config.BatteryChargeAmount.Value)"),
            "Battery Charge uses an exact amount, not a frame-dependent charger rate");
        string staff = File.ReadAllText(Path.Combine(root, "ZeroGravityAffectBroadcaster.cs"));
        Verify(staff.Contains("ids, times, affectIds, endTimes, position, direction"), "All staff RPC arrays are sent in native order");
        Verify(staff.Contains("PunManager.instance.SemiAffectNetworkIDGet()"), "Staff effects use the game's ID allocator");
        Verify(staff.Contains("Array.Empty<double>()"), "Singleplayer staff call includes the new timing array");
        string traffic = File.ReadAllText(Path.Combine(root, "TrafficShockEventAdapter.cs"));
        Verify(traffic.Contains("tumble.TumbleSet(_isTumbling: true, _playerInput: false)"), "Traffic Shock publishes revised native tumble state");
        Verify(!traffic.Contains("RPC(nameof(PlayerTumble.TumbleRequestRPC)"), "No legacy tumble request payload remains");
        if (failures.Count != 0) throw new InvalidOperationException(string.Join(Environment.NewLine, failures));
    }
}
