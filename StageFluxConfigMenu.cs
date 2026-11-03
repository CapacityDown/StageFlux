using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using MenuLib.MonoBehaviors;

namespace REPOJP.StagePhysicsEvents;

internal static class StageFluxConfigMenu
{
    private static readonly EventConfigOrder Order = new(
        StageEffectSet.IndividualEffects.Select(effect => StageEffectSet.Format(effect, string.Empty)));
    private static ConfigFile? _config;
    private static ManualLogSource? _logger;
    private static bool _warned;

    internal static ConfigEntryBase[] OrderEntries(IEnumerable<ConfigEntryBase> entries) =>
        Order.Sort(entries, entry => entry.Definition.Section, entry => entry.Definition.Key);

    internal static void Install(Harmony harmony, ConfigFile config, ManualLogSource logger)
    {
        _config = config;
        _logger = logger;
        _warned = false;
        try
        {
            // REPOConfig enumerates ConfigFile entries and groups them in arrival order.
            // It does not read ConfigurationManager Order/Category tags. Sort only its
            // display argument, keeping the real entries for Save/Revert/Reset and Events.
            Type? type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("REPOConfig.ConfigMenu", false)).FirstOrDefault(found => found != null);
            if (type == null) return;
            MethodInfo? method = type.GetMethod("CreateModEntries", BindingFlags.NonPublic | BindingFlags.Static,
                null, new[] { typeof(REPOPopupPage), typeof(ConfigEntryBase[]) }, null);
            if (method == null || method.ReturnType != typeof(void) || method.ContainsGenericParameters)
            {
                Warn("Unsupported REPOConfig menu; keeping its original setting order.");
                return;
            }
            harmony.Patch(method, prefix: new HarmonyMethod(typeof(StageFluxConfigMenu), nameof(SortStageFluxEntries)));
        }
        catch (Exception exception)
        {
            Warn("Could not organize the REPOConfig menu: " + exception.Message);
        }
    }

    private static void SortStageFluxEntries(ref ConfigEntryBase[] __1)
    {
        try
        {
            // Check ownership, not section names: other mods may use the same names.
            if (_config == null || __1 == null || __1.Length == 0 ||
                __1.Any(entry => entry == null || !ReferenceEquals(entry.ConfigFile, _config))) return;
            __1 = OrderEntries(__1);
        }
        catch (Exception exception)
        {
            Warn("Could not order Stage Flux settings; keeping the original order: " + exception.Message);
        }
    }

    internal static void Clear()
    {
        _config = null;
        _logger = null;
    }

    private static void Warn(string message)
    {
        if (!_warned) _logger?.LogWarning(message);
        _warned = true;
    }
}
