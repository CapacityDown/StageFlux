using System;
using System.Collections.Generic;
using System.Linq;

namespace REPOJP.StagePhysicsEvents;

// Presentation only: never rename, rebind, or write an existing setting.
internal sealed class EventConfigOrder
{
    private readonly Dictionary<string, int> _sections = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, string[]> CommonKeys = new(StringComparer.Ordinal)
    {
        ["General"] = new[] { "Enabled", "Mode", "StageActivationChancePercent", "MaxSimultaneousEffects" },
        ["Timing"] = new[] { "IntervalMinSeconds", "IntervalMaxSeconds", "EffectDurationMinSeconds", "EffectDurationMaxSeconds" },
        ["Targets"] = new[] { "Players", "Enemies", "Valuables", "CosmeticBoxes", "Items", "Weapons", "Doors" },
        ["Safety"] = new[] { "AllowDangerousCombinations", "ValuableProtectionReleaseDelaySeconds" },
        ["Notifications"] = new[] { "ChatAnnouncementsEnabled", "StartCountdownEnabled", "EndCountdownEnabled", "EnemyReactionEnabled" },
        ["HUD"] = new[] { "Enabled", "Style", "LayoutDirection", "Anchor", "Alignment", "OffsetX", "OffsetY", "ScalePercent", "BackgroundOpacityPercent" }
    };

    private static readonly string[] EventKeys =
    {
        "Enabled", "ChancePercent",
        // Amounts, strength, and limits; keep each minimum directly before its maximum.
        "ChargeAmount", "HealAmount", "HealthPool", "DrainAmount", "MinimumChargePercent",
        "DamageAmount", "DamagePercent", "MaximumDamagePerHit", "RepairPercent",
        "MultiplierPercent", "FragilityMultiplierPercent", "MassPercent", "SpeedPercent", "VisionPercent",
        "IntensityPercent", "AffectedPercent", "Force", "HorizontalForce", "VerticalForce",
        "LaunchForceMin", "LaunchForceMax", "MaxRevivesPerPlayer",
        "SpawnCountMin", "SpawnCountMax", "ProjectileCountMin", "ProjectileCountMax",
        "ActivationCountMin", "ActivationCountMax", "PlayersPerWaveMin", "PlayersPerWaveMax",
        "PlayersPerPulseMin", "PlayersPerPulseMax", "EnemiesPerPulse",
        "MaximumActiveInstances", "MaximumActiveMines", "MaximumSpawnsPerStage",
        // Event-specific repetition is separate from Timing's between-event interval.
        "CloudDurationSeconds", "ChargeIntervalSeconds", "HealIntervalSeconds", "DamageIntervalSeconds",
        "SpawnIntervalSeconds", "PulseIntervalSeconds", "IntervalSeconds", "CheckIntervalSeconds",
        "RetargetIntervalSeconds", "ReactivationIntervalSeconds", "ReplenishTriggeredMines", "ReplenishIntervalSeconds",
        "MinimumPlayerDistance",
        // Allowed object types stay together, including types added in later game versions.
        "ExplosiveEnabled", "ShockwaveEnabled", "StunEnabled", "PonyEnabled", "HingedItemsEnabled",
        "HealthPacksEnabled", "EquipmentEnabled", "HumanGunEnabled", "AdventureCableEnabled", "RechargeOrbEnabled",
        "BlenderEnabled", "BroomEnabled", "CarEnabled", "EggEnabled", "FlamethrowerEnabled", "IceSawEnabled",
        "PlaneEnabled", "SnakeCanEnabled", "ExplosiveTowerEnabled",
        "CanKill", "SavingGrace", "ProtectValuables", "RestoreOnEnd", "DespawnOnEnd"
    };

    internal EventConfigOrder(IEnumerable<string> eventSections)
    {
        foreach (string section in new[] { "General", "Timing", "Targets", "Safety", "Notifications", "HUD" }.Concat(eventSections))
            _sections.Add(section, _sections.Count);
    }

    internal int SectionRank(string section) => _sections.TryGetValue(section, out int rank) ? rank : int.MaxValue;

    internal static int KeyRank(string section, string key)
    {
        int index = Array.IndexOf(CommonKeys.TryGetValue(section, out string[]? keys) ? keys : EventKeys, key);
        return index < 0 ? int.MaxValue : index;
    }

    internal T[] Sort<T>(IEnumerable<T> entries, Func<T, string> section, Func<T, string> key) =>
        entries.OrderBy(entry => SectionRank(section(entry)))
            .ThenBy(section, StringComparer.Ordinal)
            .ThenBy(entry => KeyRank(section(entry), key(entry)))
            .ThenBy(key, StringComparer.Ordinal).ToArray();
}
