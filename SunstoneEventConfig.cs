using System.Collections.Generic;
using BepInEx.Configuration;

namespace REPOJP.StagePhysicsEvents;

internal static class SunstoneEventCatalog
{
    internal const StageEffect All = StageEffect.RechargeCloud | StageEffect.Rockfall | StageEffect.ArcaneBurst;
}

internal sealed class SunstoneEventSettings
{
    internal ConfigEntry<int> CountMin = null!;
    internal ConfigEntry<int> CountMax = null!;
    internal ConfigEntry<int> Interval = null!;
    internal ConfigEntry<int> MaximumActive = null!;
    internal ConfigEntry<int> MinimumDistance = null!;
}

internal sealed partial class StagePhysicsConfig
{
    internal readonly Dictionary<StageEffect, SunstoneEventSettings> SunstoneSettings = new();
    internal ConfigEntry<int> RechargeCloudDurationSeconds { get; private set; } = null!;
    internal ConfigEntry<bool> ArcaneBurstProtectValuables { get; private set; } = null!;
    internal ConfigEntry<bool> RockfallProtectValuables { get; private set; } = null!;
    internal ConfigEntry<bool> DangerousValuablesSnakeCanEnabled { get; private set; } = null!;
    internal ConfigEntry<bool> DangerousValuablesExplosiveTowerEnabled { get; private set; } = null!;
    internal ConfigEntry<bool> SupplyDropHumanGun { get; private set; } = null!;
    internal ConfigEntry<bool> SupplyDropAdventureCable { get; private set; } = null!;
    internal ConfigEntry<bool> SupplyDropRechargeOrb { get; private set; } = null!;
    internal ConfigEntry<bool> EnemyWavePonyEnabled { get; private set; } = null!;

    private void BindSunstoneEvents(ConfigFile config)
    {
        foreach (StageEffect effect in new[] { StageEffect.RechargeCloud, StageEffect.Rockfall, StageEffect.ArcaneBurst })
        {
            string section = StageEffectSet.Format(effect, string.Empty);
            var enabled = BindBool(config, section, "Enabled", true, $"Includes {section} in independent event rolls when available on the stage.");
            var chance = BindInt(config, section, "ChancePercent", DefaultEventChancePercent, 0, 100, $"Independent chance for {section}.");
            _extendedChances.Add(effect, (enabled, chance));
            enabled.SettingChanged += ProbabilitySettingChanged;
            chance.SettingChanged += ProbabilitySettingChanged;
            SunstoneSettings.Add(effect, new SunstoneEventSettings
            {
                CountMin = BindInt(config, section, "SpawnCountMin", effect == StageEffect.Rockfall ? 4 : 1, 1, 30, "Minimum number created per wave."),
                CountMax = BindInt(config, section, "SpawnCountMax", effect == StageEffect.Rockfall ? 8 : 3, 1, 30, "Maximum number created per wave."),
                Interval = BindInt(config, section, "SpawnIntervalSeconds", 10, 1, 300, "Seconds between waves while the event is active."),
                MaximumActive = BindInt(config, section, "MaximumActiveInstances", effect == StageEffect.Rockfall ? 30 : 10, 1, 30, "Maximum number of event sources alive or awaiting creation."),
                MinimumDistance = BindInt(config, section, "MinimumPlayerDistance", DefaultMinimumPlayerDistance, 0, 100, "Minimum spawn distance from living players. Unsafe points are skipped.")
            });
        }
        RechargeCloudDurationSeconds = BindInt(config, "Recharge Cloud", "CloudDurationSeconds", 10, 1, 300, "Lifetime of each charging cloud, capped by the remaining event time. Charges eligible items within the crystal's standard radius. Ignores Targets.");
        ArcaneBurstProtectValuables = BindBool(config, "Arcane Burst", "ProtectValuables", true, "Protects affected valuables during the brief Roll effect and for Safety.ValuableProtectionReleaseDelaySeconds afterwards. Requires Targets.Valuables.");
        RockfallProtectValuables = BindBool(config, "Rockfall", "ProtectValuables", true, "Protects valuables during Rockfall and briefly afterwards. Requires Targets.Valuables. Rocks can still hurt players and enemies.");
        DangerousValuablesSnakeCanEnabled = BindBool(config, "Dangerous Valuables", "SnakeCanEnabled", true, "Allows already-placed snake cans to spring open.");
        DangerousValuablesExplosiveTowerEnabled = BindBool(config, "Dangerous Valuables", "ExplosiveTowerEnabled", true, "Allows already-placed dynamite towers to start their dangerous trigger sequence.");
        SupplyDropHumanGun = BindBool(config, "Supply Drop", "HumanGunEnabled", true, "Allows the standard Human Gun. Independent of EquipmentEnabled.");
        SupplyDropAdventureCable = BindBool(config, "Supply Drop", "AdventureCableEnabled", true, "Allows the standard Human Adventure Cable. Independent of EquipmentEnabled.");
        SupplyDropRechargeOrb = BindBool(config, "Supply Drop", "RechargeOrbEnabled", true, "Allows the standard Recharge Orb. Independent of EquipmentEnabled.");
        EnemyWavePonyEnabled = BindBool(config, "Enemy Wave", "PonyEnabled", true, "Allows Pony when an unused, initialized Pony is available on the current stage. Does not create unavailable enemies.");
    }
}
