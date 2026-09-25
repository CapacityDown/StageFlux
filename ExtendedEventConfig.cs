using System.Collections.Generic;
using BepInEx.Configuration;

namespace REPOJP.StagePhysicsEvents;

internal static class ExtendedEventCatalog
{
    internal const StageEffect All = StageEffect.Restoration | StageEffect.BatteryDrain |
        StageEffect.HeavyCargo | StageEffect.Butterfingers | StageEffect.EnemyBlindness |
        StageEffect.EnemyArmor | StageEffect.EnemyVulnerability | StageEffect.SupplyDrop |
        StageEffect.PlayerSwap | StageEffect.SharedPain;
}

internal sealed partial class StagePhysicsConfig
{
    private readonly Dictionary<StageEffect, (ConfigEntry<bool> Enabled, ConfigEntry<int> Chance)> _extendedChances = new();
    internal ConfigEntry<int> RestorationRepairPercent { get; private set; } = null!;
    internal ConfigEntry<int> RestorationIntervalSeconds { get; private set; } = null!;
    internal ConfigEntry<int> BatteryDrainAmount { get; private set; } = null!;
    internal ConfigEntry<int> BatteryDrainIntervalSeconds { get; private set; } = null!;
    internal ConfigEntry<int> BatteryDrainMinimumChargePercent { get; private set; } = null!;
    internal ConfigEntry<int> HeavyCargoMassPercent { get; private set; } = null!;
    internal ConfigEntry<bool> HeavyCargoProtectValuables { get; private set; } = null!;
    internal ConfigEntry<int> ButterfingersIntervalSeconds { get; private set; } = null!;
    internal ConfigEntry<bool> ButterfingersProtectValuables { get; private set; } = null!;
    internal ConfigEntry<int> EnemyBlindnessVisionPercent { get; private set; } = null!;
    internal ConfigEntry<int> EnemyArmorDamagePercent { get; private set; } = null!;
    internal ConfigEntry<int> EnemyVulnerabilityDamagePercent { get; private set; } = null!;
    internal ConfigEntry<int> SupplyDropCountMin { get; private set; } = null!;
    internal ConfigEntry<int> SupplyDropCountMax { get; private set; } = null!;
    internal ConfigEntry<int> SupplyDropIntervalSeconds { get; private set; } = null!;
    internal ConfigEntry<int> SupplyDropMaximumPerStage { get; private set; } = null!;
    internal ConfigEntry<bool> SupplyDropHealthPacks { get; private set; } = null!;
    internal ConfigEntry<bool> SupplyDropEquipment { get; private set; } = null!;
    internal ConfigEntry<int> PlayerSwapIntervalSeconds { get; private set; } = null!;
    internal ConfigEntry<int> SharedPainDamagePercent { get; private set; } = null!;
    internal ConfigEntry<int> SharedPainMaximumDamage { get; private set; } = null!;
    internal ConfigEntry<bool> SharedPainCanKill { get; private set; } = null!;

    private void BindExtendedEvents(ConfigFile config)
    {
        foreach (StageEffect effect in StageEffectSet.IndividualEffects)
        {
            if ((effect & ExtendedEventCatalog.All) == StageEffect.None)
                continue;
            string section = StageEffectSet.Format(effect, string.Empty);
            ConfigEntry<bool> enabled = BindBool(config, section, "Enabled", true,
                $"Includes {section} in each event's independent rolls.");
            ConfigEntry<int> chance = BindInt(config, section, "ChancePercent", DefaultEventChancePercent, 0, 100,
                $"Independent chance for {section} in each event.");
            _extendedChances.Add(effect, (enabled, chance));
            enabled.SettingChanged += ProbabilitySettingChanged;
            chance.SettingChanged += ProbabilitySettingChanged;
        }
        RestorationRepairPercent = BindInt(config, "Restoration", "RepairPercent", 5, 1, 100, "Percentage of each valuable's full value repaired per pulse. Destroyed valuables are not recreated. Ignores Targets.");
        RestorationIntervalSeconds = BindInt(config, "Restoration", "IntervalSeconds", 5, 1, 300, "Seconds between repair pulses.");
        BatteryDrainAmount = BindInt(config, "Battery Drain", "DrainAmount", 5, 1, 100, "Battery percentage points removed per pulse. Ignores Targets.");
        BatteryDrainIntervalSeconds = BindInt(config, "Battery Drain", "IntervalSeconds", 4, 1, 300, "Seconds between battery drain pulses.");
        BatteryDrainMinimumChargePercent = BindInt(config, "Battery Drain", "MinimumChargePercent", 0, 0, 100, "Battery Drain will not lower charge below this percentage. Normal item use can still drain it further.");
        HeavyCargoMassPercent = BindInt(config, "Heavy Cargo", "MassPercent", 200, 101, 500, "Object mass percentage. Uses Targets for valuables, Cosmetic Boxes, items and weapons only; never players, enemies or doors.");
        HeavyCargoProtectValuables = BindBool(config, "Heavy Cargo", "ProtectValuables", true, "Protects targeted valuables during Heavy Cargo and for Safety.ValuableProtectionReleaseDelaySeconds after it ends. Requires Targets.Valuables.");
        ButterfingersIntervalSeconds = BindInt(config, "Butterfingers", "IntervalSeconds", 8, 1, 300, "Seconds between dropping held valuables and items. Inventory slots, players, enemies and doors are excluded. Ignores Targets.");
        ButterfingersProtectValuables = BindBool(config, "Butterfingers", "ProtectValuables", true, "Briefly protects dropped valuables. Uses Safety.ValuableProtectionReleaseDelaySeconds after each drop. Requires Targets.Valuables.");
        EnemyBlindnessVisionPercent = BindInt(config, "Enemy Blindness", "VisionPercent", 25, 1, 99, "Remaining enemy sight range as a percentage. Hearing and existing pursuit remain active. Ignores Targets.");
        EnemyArmorDamagePercent = BindInt(config, "Enemy Armor", "DamagePercent", 50, 1, 99, "Percentage of incoming damage enemies receive. Combines with other enemy defenses. Ignores Targets.");
        EnemyVulnerabilityDamagePercent = BindInt(config, "Enemy Vulnerability", "DamagePercent", 200, 101, 500, "Percentage of incoming damage enemies receive. Ignores Targets.");
        SupplyDropCountMin = BindInt(config, "Supply Drop", "SpawnCountMin", 1, 1, 10, "Minimum items per supply wave. Items remain after the event.");
        SupplyDropCountMax = BindInt(config, "Supply Drop", "SpawnCountMax", 2, 1, 10, "Maximum items per supply wave.");
        SupplyDropIntervalSeconds = BindInt(config, "Supply Drop", "IntervalSeconds", 15, 1, 300, "Seconds between supply waves.");
        SupplyDropMaximumPerStage = BindInt(config, "Supply Drop", "MaximumSpawnsPerStage", 10, 1, 50, "Total supply items allowed per stage, including items already used or destroyed. Prevents unlimited supplies in persistent mode.");
        SupplyDropHealthPacks = BindBool(config, "Supply Drop", "HealthPacksEnabled", true, "Allows standard health packs in supply waves.");
        SupplyDropEquipment = BindBool(config, "Supply Drop", "EquipmentEnabled", true, "Allows standard trackers and tools in supply waves. Excludes weapons, explosives, upgrades and custom items.");
        PlayerSwapIntervalSeconds = BindInt(config, "Player Swap", "IntervalSeconds", 15, 1, 300, "Seconds between swapping two living, grounded, standing players. Skips unsafe positions and players holding objects. Requires at least two eligible players. Ignores Targets.");
        SharedPainDamagePercent = BindInt(config, "Shared Pain", "DamagePercent", 25, 1, 100, "Percentage of real damage shared with each other living player. Shared damage and health donations do not spread again. Ignores Targets.");
        SharedPainMaximumDamage = BindInt(config, "Shared Pain", "MaximumDamagePerHit", 25, 1, 100, "Maximum shared damage per player for one hit.");
        SharedPainCanKill = BindBool(config, "Shared Pain", "CanKill", false, "Allows shared damage to be lethal. When off, caps damage using the host's latest health and enables saving grace; simultaneous damage can still be dangerous.");
    }

    private int GetExtendedChance(StageEffect effect)
    {
        if (effect == StageEffect.SupplyDrop && !SupplyDropHealthPacks.Value && !SupplyDropEquipment.Value)
            return 0;
        return _extendedChances.TryGetValue(effect, out var entry) && entry.Enabled.Value ? entry.Chance.Value : 0;
    }
}
