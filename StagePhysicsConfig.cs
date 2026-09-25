using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;

namespace REPOJP.StagePhysicsEvents;

internal sealed partial class StagePhysicsConfig
{
    private const int CurrentConfigRevision = 14;
    private const int DefaultMinimumPlayerDistance = 3;
    private const int DefaultEventChancePercent = 6;
    private static readonly PropertyInfo? OrphanedEntriesProperty = typeof(ConfigFile).GetProperty(
        "OrphanedEntries",
        BindingFlags.Instance | BindingFlags.NonPublic);
    private readonly ManualLogSource _logger;
    private string _lastProbabilityWarning = string.Empty;
    private string _lastRangeWarning = string.Empty;

    internal StagePhysicsConfig(ConfigFile config, ManualLogSource logger)
    {
        _logger = logger;
        bool saveOnConfigSet = config.SaveOnConfigSet;
        config.SaveOnConfigSet = false;
        ConfigDefinition configRevisionDefinition = new("Internal", "ConfigRevision");
        int configRevision = ReadConfigRevision(config, configRevisionDefinition);
        ConfigDefinition legacyStageEffectChanceDefinition = new(
            "General",
            "StageEffectChancePercent");
        ConfigDefinition stageActivationChanceDefinition = new(
            "General",
            "StageActivationChancePercent");
        bool legacyStageEffectChanceWasConfigured = HasOrphanedConfigEntry(
            config,
            legacyStageEffectChanceDefinition);
        bool stageActivationChanceWasConfigured = HasOrphanedConfigEntry(
            config,
            stageActivationChanceDefinition);
        string legacyFeatherSection = string.Concat("Light", "weight");
        ConfigEntry<bool> legacyFeatherEnabled = BindBool(
            config, legacyFeatherSection, "Enabled", true, "Legacy setting migrated to Feather.");
        ConfigEntry<int> legacyFeatherChance = BindInt(
            config, legacyFeatherSection, "ChancePercent", DefaultEventChancePercent, 0, 100, "Legacy setting migrated to Feather.");
        BindBool(config, "Magnet", "Enabled", true, "Obsolete setting removed in 4.1.0.");
        BindInt(config, "Magnet", "ChancePercent", 10, 0, 100, "Obsolete setting removed in 4.1.0.");
        ConfigEntry<bool> legacyTorqueEnabled = BindBool(
            config, "Torque", "Enabled", false, "Legacy Torque setting migrated to Roll.");
        ConfigEntry<int> legacyTorqueChance = BindInt(
            config, "Torque", "ChancePercent", DefaultEventChancePercent, 0, 100, "Legacy Torque setting migrated to Roll.");
        ConfigEntry<bool> legacyRollStaffEnabled = BindBool(
            config, "Roll Staff", "Enabled", legacyTorqueEnabled.Value, "Legacy Roll Staff setting migrated to Roll.");
        ConfigEntry<int> legacyRollStaffChance = BindInt(
            config, "Roll Staff", "ChancePercent", legacyTorqueChance.Value, 0, 100, "Legacy Roll Staff setting migrated to Roll.");
        BindBool(config, "Boost", "Enabled", true, "Obsolete setting removed in 4.1.0.");
        BindInt(config, "Boost", "ChancePercent", 3, 0, 100, "Obsolete setting removed in 4.1.0.");

        ConfigEntry<int> legacyStageEffectChance = BindInt(config, "General", "StageEffectChancePercent", 20, 0, 100, "Legacy setting migrated to StageActivationChancePercent.");
        Enabled = BindBool(config, "General", "Enabled", true, "Enables the mod. Stage event behavior uses the host's settings.");
        Mode = config.Bind("General", "Mode", "RandomEachEvent", new ConfigDescription(
            "Except for PersistentForStage, every enabled stage begins with one full configured interval before the first event. AllMode selects one of the other four modes with equal probability when each stage begins. RandomEachEvent rerolls every event. FixedForStage keeps the stage's first effect combination, duration, and interval. FixedPerExtraction keeps the stage's duration and interval but rerolls the effect combination after each completed extraction. PersistentForStage keeps the selected effect combination active until the stage ends.",
            new AcceptableValueList<string>("AllMode", "RandomEachEvent", "FixedForStage", "FixedPerExtraction", "PersistentForStage")));
        StageActivationChancePercent = BindInt(config, "General", "StageActivationChancePercent", 50, 0, 100, "Chance that stage events are enabled for a stage. A failed roll disables effects, notifications, and the HUD for that stage.");
        if (!stageActivationChanceWasConfigured && legacyStageEffectChanceWasConfigured)
        {
            StageActivationChancePercent.Value = legacyStageEffectChance.Value;
            _logger.LogInfo(
                "Migrated legacy General.StageEffectChancePercent to StageActivationChancePercent.");
        }
        if (configRevision < 14 &&
            stageActivationChanceWasConfigured &&
            StageActivationChancePercent.Value == 20)
        {
            StageActivationChancePercent.Value = 50;
            _logger.LogInfo(
                "Migrated the previous default stage activation chance from 20% to 50%.");
        }
        MaxSimultaneousEffects = BindInt(config, "General", "MaxSimultaneousEffects", 3, 1, 5, "Maximum number of effects that can be selected for one event. Successful effects above this limit are reduced randomly.");
        AllowDangerousCombinations = BindBool(config, "Safety", "AllowDangerousCombinations", false, "Allows combinations that can greatly increase player death, uncontrolled movement, enemy pressure, visual disruption, or valuable loss.");
        ConfigEntry<int> legacyHazardProtectionDelay = BindInt(config, "Safety", "HazardIndestructibleReleaseDelaySeconds", 2, 1, 5, "Legacy setting migrated to ValuableProtectionReleaseDelaySeconds.");
        ValuableProtectionReleaseDelaySeconds = BindInt(config, "Safety", "ValuableProtectionReleaseDelaySeconds", legacyHazardProtectionDelay.Value, 1, 5, "Seconds to keep targeted valuables Indestructible after a hazard event ends.");

        EffectDurationMinSeconds = BindInt(config, "Timing", "EffectDurationMinSeconds", 15, 10, 300, "Minimum effect duration in seconds.");
        EffectDurationMaxSeconds = BindInt(config, "Timing", "EffectDurationMaxSeconds", 30, 10, 300, "Maximum effect duration in seconds.");
        IntervalMinSeconds = BindInt(config, "Timing", "IntervalMinSeconds", 45, 10, 300, "Minimum number of seconds before the next event roll.");
        IntervalMaxSeconds = BindInt(config, "Timing", "IntervalMaxSeconds", 90, 10, 300, "Maximum number of seconds before the next event roll.");

        FeatherEnabled = BindBool(config, "Feather", "Enabled", legacyFeatherEnabled.Value, "Includes Feather in each event's independent rolls.");
        FeatherChance = BindInt(config, "Feather", "ChancePercent", legacyFeatherChance.Value, 0, 100, "Independent chance for Feather in each event.");
        if (configRevision < 1)
        {
            if (FeatherChance.Value == 30)
            {
                FeatherChance.Value = 15;
                _logger.LogInfo("Migrated the previous default Feather chance from 30% to 15%.");
            }
        }
        ZeroGravityEnabled = BindBool(config, "Zero Gravity", "Enabled", true, "Includes Zero Gravity in each event's independent rolls.");
        ZeroGravityChance = BindInt(config, "Zero Gravity", "ChancePercent", DefaultEventChancePercent, 0, 100, "Independent chance for Zero Gravity in each event.");
        ZeroGravityProtectValuables = BindBool(config, "Zero Gravity", "ProtectValuables", true, "Temporarily protects targeted valuables from damage caused by Zero Gravity.");
        int legacyZeroGravityProtectionDelay = configRevision < 9
            ? BindInt(config, "Zero Gravity", "IndestructibleReleaseDelaySeconds", 2, 1, 5, "Legacy setting migrated to Safety.ValuableProtectionReleaseDelaySeconds.").Value
            : 2;
        ConfigEntry<bool> legacyBatteryEnabled = BindBool(config, "Battery", "Enabled", true, "Legacy setting migrated to Battery Charge.");
        ConfigEntry<int> legacyBatteryChance = BindInt(config, "Battery", "ChancePercent", DefaultEventChancePercent, 0, 100, "Legacy setting migrated to Battery Charge.");
        ConfigEntry<int> legacyBatteryChargeAmount = BindInt(config, "Battery", "ChargeAmount", 5, 1, 100, "Legacy setting migrated to Battery Charge.");
        ConfigEntry<int> legacyBatteryChargeIntervalSeconds = BindInt(config, "Battery", "ChargeIntervalSeconds", 4, 1, 300, "Legacy setting migrated to Battery Charge.");
        if (configRevision < 2)
        {
            if (legacyBatteryChargeAmount.Value == 10)
            {
                legacyBatteryChargeAmount.Value = 5;
                _logger.LogInfo("Migrated the previous default Battery charge amount from 10 to 5.");
            }
            if (legacyBatteryChargeIntervalSeconds.Value == 2)
            {
                legacyBatteryChargeIntervalSeconds.Value = 4;
                _logger.LogInfo("Migrated the previous default Battery charge interval from 2 seconds to 4 seconds.");
            }
        }
        ConfigEntry<bool> legacyCompactBatteryEnabled = BindBool(config, "BatteryCharge", "Enabled", legacyBatteryEnabled.Value, "Legacy setting migrated to Battery Charge.");
        ConfigEntry<int> legacyCompactBatteryChance = BindInt(config, "BatteryCharge", "ChancePercent", legacyBatteryChance.Value, 0, 100, "Legacy setting migrated to Battery Charge.");
        ConfigEntry<int> legacyCompactBatteryChargeAmount = BindInt(config, "BatteryCharge", "ChargeAmount", legacyBatteryChargeAmount.Value, 1, 100, "Legacy setting migrated to Battery Charge.");
        ConfigEntry<int> legacyCompactBatteryChargeIntervalSeconds = BindInt(config, "BatteryCharge", "ChargeIntervalSeconds", legacyBatteryChargeIntervalSeconds.Value, 1, 300, "Legacy setting migrated to Battery Charge.");
        BatteryEnabled = BindBool(config, "Battery Charge", "Enabled", legacyCompactBatteryEnabled.Value, "Includes Battery Charge in each event's independent rolls.");
        BatteryChance = BindInt(config, "Battery Charge", "ChancePercent", legacyCompactBatteryChance.Value, 0, 100, "Independent chance for Battery Charge in each event.");
        BatteryChargeAmount = BindInt(config, "Battery Charge", "ChargeAmount", legacyCompactBatteryChargeAmount.Value, 1, 100, "Battery percentage points restored to each targeted battery per charge tick.");
        BatteryChargeIntervalSeconds = BindInt(config, "Battery Charge", "ChargeIntervalSeconds", legacyCompactBatteryChargeIntervalSeconds.Value, 1, 300, "Seconds between Battery Charge ticks.");
        HealEnabled = BindBool(config, "Heal", "Enabled", true, "Includes the Heal Orb effect in each event's independent rolls.");
        HealChance = BindInt(config, "Heal", "ChancePercent", DefaultEventChancePercent, 0, 100, "Independent chance for Heal in each event.");
        HealAmount = BindInt(config, "Heal", "HealAmount", 10, 1, 100, "Health restored to each targeted player per Heal tick.");
        HealIntervalSeconds = BindInt(config, "Heal", "HealIntervalSeconds", 2, 1, 300, "Seconds between Heal ticks.");
        IndestructibleEnabled = BindBool(config, "Indestructible", "Enabled", true, "Includes the Indestructible Orb effect in each event's independent rolls.");
        IndestructibleChance = BindInt(config, "Indestructible", "ChancePercent", DefaultEventChancePercent, 0, 100, "Independent chance for Indestructible in each event.");
        FragilityEnabled = BindBool(config, "Fragility", "Enabled", true, "Includes the stage-wide valuable Fragility effect in each event's independent rolls.");
        FragilityChance = BindInt(config, "Fragility", "ChancePercent", DefaultEventChancePercent, 0, 100, "Independent chance for Fragility in each event.");
        FragilityMultiplierPercent = BindInt(config, "Fragility", "FragilityMultiplierPercent", 1000, 101, 5000, "Valuable impact-fragility multiplier. 100 is vanilla; this event accepts only values above 100 so valuables always become easier to break.");
        if (configRevision < 3 && FragilityMultiplierPercent.Value == 200)
        {
            FragilityMultiplierPercent.Value = 1000;
            _logger.LogInfo("Migrated the previous default Fragility multiplier from 200% to 1000%.");
        }
        GumballHypnosisEnabled = BindBool(config, "Gumball Hypnosis", "Enabled", true, "Includes Gumball Hypnosis in each event's independent rolls.");
        GumballHypnosisChance = BindInt(config, "Gumball Hypnosis", "ChancePercent", DefaultEventChancePercent, 0, 100, "Independent chance for Gumball Hypnosis. Holding a supported valuable, Cosmetic Box, item, or weapon applies the vanilla Gumball screen and gaze effect until it is released. This fixed-target event ignores Targets.");
        HealingAuraEnabled = BindBool(config, "Healing Aura", "Enabled", true, "Includes Healing Aura in each event's independent rolls.");
        HealingAuraChance = BindInt(config, "Healing Aura", "ChancePercent", DefaultEventChancePercent, 0, 100, "Independent chance for Healing Aura.");
        HealingAuraHealthPool = BindInt(config, "Healing Aura", "HealthPool", 50, 1, 1000, "Total health available from each spawned Healing Aura.");
        HealingAuraSpawnCountMin = BindInt(config, "Healing Aura", "SpawnCountMin", 1, 1, 30, "Minimum number of Healing Auras created in each wave.");
        HealingAuraSpawnCountMax = BindInt(config, "Healing Aura", "SpawnCountMax", 3, 1, 30, "Maximum number of Healing Auras created in each wave.");
        HealingAuraMaximumActiveInstances = BindInt(config, "Healing Aura", "MaximumActiveInstances", 30, 1, 30, "Maximum number of tracked Healing Auras.");
        HealingAuraSpawnIntervalSeconds = BindInt(config, "Healing Aura", "SpawnIntervalSeconds", 10, 1, 300, "Seconds between Healing Aura spawn waves.");
        HealingAuraMinimumPlayerDistance = BindInt(config, "Healing Aura", "MinimumPlayerDistance", DefaultMinimumPlayerDistance, 0, 100, "Minimum distance from a living player when selecting a Healing Aura spawn point.");

        StarBarrageEnabled = BindBool(config, "Star Barrage", "Enabled", true, "Includes Star Barrage in each event's independent rolls.");
        StarBarrageChance = BindInt(config, "Star Barrage", "ChancePercent", DefaultEventChancePercent, 0, 100, "Independent chance for Star Barrage.");
        StarBarrageProjectileCountMin = BindInt(config, "Star Barrage", "ProjectileCountMin", 3, 1, 30, "Minimum number of Star Wand projectiles launched in each wave.");
        StarBarrageProjectileCountMax = BindInt(config, "Star Barrage", "ProjectileCountMax", 6, 1, 30, "Maximum number of Star Wand projectiles launched in each wave.");
        StarBarrageMaximumActiveInstances = BindInt(config, "Star Barrage", "MaximumActiveInstances", 30, 1, 30, "Maximum number of tracked Star Wand projectiles.");
        StarBarrageSpawnIntervalSeconds = BindInt(config, "Star Barrage", "SpawnIntervalSeconds", 2, 1, 300, "Seconds between Star Barrage waves.");
        StarBarrageMinimumPlayerDistance = BindInt(config, "Star Barrage", "MinimumPlayerDistance", DefaultMinimumPlayerDistance, 0, 100, "Minimum distance from a living player when selecting a projectile origin.");
        StarBarrageProtectValuables = BindBool(config, "Star Barrage", "ProtectValuables", true, "Temporarily protects targeted valuables from damage caused by Star Barrage.");

        SpiderScareEnabled = BindBool(config, "Spider Scare", "Enabled", true, "Includes Spider Scare in each event's independent rolls.");
        SpiderScareChance = BindInt(config, "Spider Scare", "ChancePercent", DefaultEventChancePercent, 0, 100, "Independent chance for Spider Scare.");
        SpiderScarePlayersPerWaveMin = BindInt(config, "Spider Scare", "PlayersPerWaveMin", 1, 1, 30, "Minimum number of random living player positions targeted in each wave.");
        SpiderScarePlayersPerWaveMax = BindInt(config, "Spider Scare", "PlayersPerWaveMax", 3, 1, 30, "Maximum number of random living player positions targeted in each wave.");
        SpiderScareSpawnIntervalSeconds = BindInt(config, "Spider Scare", "SpawnIntervalSeconds", 10, 1, 300, "Seconds between Spider Scare waves.");

        TrafficShockEnabled = BindBool(config, "Traffic Shock", "Enabled", true, "Includes Traffic Shock in each event's independent rolls.");
        TrafficShockChance = BindInt(config, "Traffic Shock", "ChancePercent", DefaultEventChancePercent, 0, 100, "Independent chance for Traffic Shock.");
        TrafficShockPlayersPerPulseMin = BindInt(config, "Traffic Shock", "PlayersPerPulseMin", 1, 1, 30, "Minimum number of random living players shocked in each pulse.");
        TrafficShockPlayersPerPulseMax = BindInt(config, "Traffic Shock", "PlayersPerPulseMax", 3, 1, 30, "Maximum number of random living players shocked in each pulse.");
        TrafficShockPulseIntervalSeconds = BindInt(config, "Traffic Shock", "PulseIntervalSeconds", 10, 1, 300, "Seconds between Traffic Shock pulses.");

        DangerousValuablesEnabled = BindBool(config, "Dangerous Valuables", "Enabled", true, "Includes Dangerous Valuables in each event's independent rolls.");
        DangerousValuablesChance = BindInt(config, "Dangerous Valuables", "ChancePercent", DefaultEventChancePercent, 0, 100, "Independent chance for Dangerous Valuables.");
        ConfigEntry<int> legacyDangerousValuablesSpawnCountMin = BindInt(
            config, "Dangerous Valuables", "SpawnCountMin", 5, 1, 30,
            "Legacy setting migrated to ActivationCountMin.");
        ConfigEntry<int> legacyDangerousValuablesSpawnCountMax = BindInt(
            config, "Dangerous Valuables", "SpawnCountMax", 10, 1, 30,
            "Legacy setting migrated to ActivationCountMax.");
        if (configRevision < 5)
        {
            MigrateSpawnCountDefaults(
                legacyDangerousValuablesSpawnCountMin,
                legacyDangerousValuablesSpawnCountMax,
                1,
                3,
                5,
                10,
                "Dangerous Valuables");
        }
        ConfigEntry<int> legacyDangerousValuablesReplenishInterval = BindInt(
            config, "Dangerous Valuables", "ReplenishIntervalSeconds", 10, 1, 300,
            "Legacy setting migrated to ReactivationIntervalSeconds.");
        BindInt(config, "Dangerous Valuables", "MinimumPlayerDistance", 5, 0, 100, "Obsolete spawn setting.");
        BindBool(config, "Dangerous Valuables", "ReplenishDestroyed", true, "Obsolete spawn setting.");
        DangerousValuablesActivationCountMin = BindInt(
            config, "Dangerous Valuables", "ActivationCountMin",
            legacyDangerousValuablesSpawnCountMin.Value, 1, 30,
            "Minimum number of already-placed dangerous valuables activated.");
        DangerousValuablesActivationCountMax = BindInt(
            config, "Dangerous Valuables", "ActivationCountMax",
            legacyDangerousValuablesSpawnCountMax.Value, 1, 30,
            "Maximum number of already-placed dangerous valuables activated.");
        DangerousValuablesMaximumActiveInstances = BindInt(config, "Dangerous Valuables", "MaximumActiveInstances", 15, 1, 30, "Maximum number of tracked dangerous valuables.");
        DangerousValuablesReactivationIntervalSeconds = BindInt(
            config, "Dangerous Valuables", "ReactivationIntervalSeconds",
            legacyDangerousValuablesReplenishInterval.Value, 1, 300,
            "Seconds between reactivation checks for the selected existing valuables.");
        DangerousValuablesBlenderEnabled = BindBool(config, "Dangerous Valuables", "BlenderEnabled", true, "Allows already-placed Blender valuables to be activated.");
        DangerousValuablesBroomEnabled = BindBool(config, "Dangerous Valuables", "BroomEnabled", true, "Allows already-placed Broom valuables to be activated.");
        DangerousValuablesCarEnabled = BindBool(config, "Dangerous Valuables", "CarEnabled", true, "Allows already-placed Car valuables to be activated.");
        DangerousValuablesEggEnabled = BindBool(config, "Dangerous Valuables", "EggEnabled", true, "Allows already-placed Egg valuables to be activated.");
        DangerousValuablesFlamethrowerEnabled = BindBool(config, "Dangerous Valuables", "FlamethrowerEnabled", true, "Allows already-placed Flamethrower valuables to be activated.");
        DangerousValuablesIceSawEnabled = BindBool(config, "Dangerous Valuables", "IceSawEnabled", true, "Allows already-placed Ice Saw valuables to be activated.");
        DangerousValuablesPlaneEnabled = BindBool(config, "Dangerous Valuables", "PlaneEnabled", true, "Allows already-placed Plane valuables to be activated.");
        DangerousValuablesProtectValuables = BindBool(config, "Dangerous Valuables", "ProtectValuables", false, "Temporarily protects targeted valuables from damage caused by Dangerous Valuables.");
        RollEnabled = BindBool(config, "Roll", "Enabled", legacyRollStaffEnabled.Value, "Includes Roll in each event's independent rolls.");
        RollChance = BindInt(config, "Roll", "ChancePercent", legacyRollStaffChance.Value, 0, 100, "Independent chance for Roll in each event.");
        RollProtectValuables = BindBool(config, "Roll", "ProtectValuables", true, "Temporarily protects targeted valuables from damage caused by Roll.");
        int legacyRollProtectionDelay = configRevision < 9
            ? BindInt(config, "Roll", "IndestructibleReleaseDelaySeconds", 2, 1, 5, "Legacy setting migrated to Safety.ValuableProtectionReleaseDelaySeconds.").Value
            : 2;
        VoidEnabled = BindBool(config, "Void", "Enabled", false, "Includes the Void Staff effect in each event's independent rolls.");
        VoidChance = BindInt(config, "Void", "ChancePercent", DefaultEventChancePercent, 0, 100, "Independent chance for Void in each event.");
        int legacyVoidProtectionDelay = configRevision < 9
            ? BindInt(config, "Void", "IndestructibleReleaseDelaySeconds", 2, 1, 5, "Legacy setting migrated to Safety.ValuableProtectionReleaseDelaySeconds.").Value
            : 2;
        VoidSpawnCountMin = BindInt(config, "Void", "SpawnCountMin", 3, 2, 30, "Minimum number of Void effects created at random stage points.");
        VoidSpawnCountMax = BindInt(config, "Void", "SpawnCountMax", 5, 2, 30, "Maximum number of Void effects created at random stage points.");
        VoidSpawnIntervalSeconds = BindInt(config, "Void", "SpawnIntervalSeconds", 10, 1, 300, "Seconds between Void effect regeneration waves.");
        VoidProtectValuables = BindBool(config, "Void", "ProtectValuables", true, "Temporarily protects targeted valuables from damage caused by Void.");

        LevitationEnabled = BindBool(config, "Levitation", "Enabled", true, "Includes Levitation in each event's independent rolls.");
        LevitationChance = BindInt(config, "Levitation", "ChancePercent", DefaultEventChancePercent, 0, 100, "Independent chance for Levitation in each event.");
        LevitationSpawnCountMin = BindInt(config, "Levitation", "SpawnCountMin", 2, 1, 30, "Minimum number of Levitation effects created in each wave.");
        LevitationSpawnCountMax = BindInt(config, "Levitation", "SpawnCountMax", 5, 1, 30, "Maximum number of Levitation effects created in each wave.");
        LevitationMaximumActiveInstances = BindInt(config, "Levitation", "MaximumActiveInstances", 30, 1, 30, "Maximum number of tracked Levitation source instances.");
        LevitationSpawnIntervalSeconds = BindInt(config, "Levitation", "SpawnIntervalSeconds", 10, 1, 300, "Seconds between Levitation spawn waves.");
        LevitationMinimumPlayerDistance = BindInt(config, "Levitation", "MinimumPlayerDistance", DefaultMinimumPlayerDistance, 0, 100, "Minimum distance from a living player when selecting a Levitation spawn point.");
        LevitationProtectValuables = BindBool(config, "Levitation", "ProtectValuables", true, "Temporarily protects targeted valuables from damage caused by Levitation.");

        ShockwaveEnabled = BindBool(config, "Shockwave", "Enabled", true, "Includes Shockwave in each event's independent rolls.");
        ShockwaveChance = BindInt(config, "Shockwave", "ChancePercent", DefaultEventChancePercent, 0, 100, "Independent chance for Shockwave in each event.");
        ShockwaveSpawnCountMin = BindInt(config, "Shockwave", "SpawnCountMin", 5, 1, 30, "Minimum number of Shockwave grenades created in each wave.");
        ShockwaveSpawnCountMax = BindInt(config, "Shockwave", "SpawnCountMax", 10, 1, 30, "Maximum number of Shockwave grenades created in each wave.");
        ShockwaveMaximumActiveInstances = BindInt(config, "Shockwave", "MaximumActiveInstances", 30, 1, 30, "Maximum number of tracked Shockwave grenades.");
        ShockwaveSpawnIntervalSeconds = BindInt(config, "Shockwave", "SpawnIntervalSeconds", 10, 1, 300, "Seconds between Shockwave spawn waves.");
        ShockwaveMinimumPlayerDistance = BindInt(config, "Shockwave", "MinimumPlayerDistance", DefaultMinimumPlayerDistance, 0, 100, "Minimum distance from a living player when selecting a Shockwave spawn point.");
        ShockwaveLaunchForceMin = BindInt(config, "Shockwave", "LaunchForceMin", 6, 0, 100, "Minimum velocity-change force used to launch each Shockwave grenade in a random upward direction.");
        ShockwaveLaunchForceMax = BindInt(config, "Shockwave", "LaunchForceMax", 12, 0, 100, "Maximum velocity-change force used to launch each Shockwave grenade in a random upward direction.");
        ShockwaveProtectValuables = BindBool(config, "Shockwave", "ProtectValuables", true, "Temporarily protects targeted valuables from damage caused by Shockwave.");

        StunBlastEnabled = BindBool(config, "Stun Blast", "Enabled", true, "Includes Stun Blast in each event's independent rolls.");
        StunBlastChance = BindInt(config, "Stun Blast", "ChancePercent", DefaultEventChancePercent, 0, 100, "Independent chance for Stun Blast in each event.");
        StunBlastSpawnCountMin = BindInt(config, "Stun Blast", "SpawnCountMin", 5, 1, 30, "Minimum number of Stun grenades created in each wave.");
        StunBlastSpawnCountMax = BindInt(config, "Stun Blast", "SpawnCountMax", 10, 1, 30, "Maximum number of Stun grenades created in each wave.");
        StunBlastMaximumActiveInstances = BindInt(config, "Stun Blast", "MaximumActiveInstances", 30, 1, 30, "Maximum number of tracked Stun grenades.");
        StunBlastSpawnIntervalSeconds = BindInt(config, "Stun Blast", "SpawnIntervalSeconds", 10, 1, 300, "Seconds between Stun Blast spawn waves.");
        StunBlastMinimumPlayerDistance = BindInt(config, "Stun Blast", "MinimumPlayerDistance", DefaultMinimumPlayerDistance, 0, 100, "Minimum distance from a living player when selecting a Stun Blast spawn point.");
        StunBlastLaunchForceMin = BindInt(config, "Stun Blast", "LaunchForceMin", 6, 0, 100, "Minimum velocity-change force used to launch each Stun grenade in a random upward direction.");
        StunBlastLaunchForceMax = BindInt(config, "Stun Blast", "LaunchForceMax", 12, 0, 100, "Maximum velocity-change force used to launch each Stun grenade in a random upward direction.");
        StunBlastProtectValuables = BindBool(config, "Stun Blast", "ProtectValuables", true, "Temporarily protects targeted valuables from damage caused by Stun Blast.");

        ExplosionRainEnabled = BindBool(config, "Explosion Rain", "Enabled", true, "Includes Explosion Rain in each event's independent rolls.");
        ExplosionRainChance = BindInt(config, "Explosion Rain", "ChancePercent", DefaultEventChancePercent, 0, 100, "Independent chance for Explosion Rain in each event.");
        ExplosionRainSpawnCountMin = BindInt(config, "Explosion Rain", "SpawnCountMin", 5, 1, 30, "Minimum number of explosive grenades created in each wave.");
        ExplosionRainSpawnCountMax = BindInt(config, "Explosion Rain", "SpawnCountMax", 10, 1, 30, "Maximum number of explosive grenades created in each wave.");
        ExplosionRainMaximumActiveInstances = BindInt(config, "Explosion Rain", "MaximumActiveInstances", 30, 1, 30, "Maximum number of tracked explosive grenades.");
        ExplosionRainSpawnIntervalSeconds = BindInt(config, "Explosion Rain", "SpawnIntervalSeconds", 10, 1, 300, "Seconds between Explosion Rain spawn waves.");
        ExplosionRainMinimumPlayerDistance = BindInt(config, "Explosion Rain", "MinimumPlayerDistance", DefaultMinimumPlayerDistance, 0, 100, "Minimum distance from a living player when selecting an Explosion Rain spawn point.");
        ExplosionRainLaunchForceMin = BindInt(config, "Explosion Rain", "LaunchForceMin", 6, 0, 100, "Minimum velocity-change force used to launch each explosive grenade in a random upward direction.");
        ExplosionRainLaunchForceMax = BindInt(config, "Explosion Rain", "LaunchForceMax", 12, 0, 100, "Maximum velocity-change force used to launch each explosive grenade in a random upward direction.");
        ExplosionRainProtectValuables = BindBool(config, "Explosion Rain", "ProtectValuables", true, "Temporarily protects targeted valuables from damage caused by Explosion Rain.");

        EnemyWaveEnabled = BindBool(config, "Enemy Wave", "Enabled", true, "Includes Enemy Wave in each event's independent rolls.");
        EnemyWaveChance = BindInt(config, "Enemy Wave", "ChancePercent", DefaultEventChancePercent, 0, 100, "Independent chance for Enemy Wave in each event.");
        EnemyWaveSpawnCountMin = BindInt(config, "Enemy Wave", "SpawnCountMin", 1, 1, 30, "Minimum number of additional enemies kept active.");
        EnemyWaveSpawnCountMax = BindInt(config, "Enemy Wave", "SpawnCountMax", 3, 1, 30, "Maximum number of additional enemies kept active.");
        EnemyWaveMinimumPlayerDistance = BindInt(config, "Enemy Wave", "MinimumPlayerDistance", DefaultMinimumPlayerDistance, 0, 100, "Minimum requested distance from players when placing an additional enemy.");
        EnemyWaveReplenishIntervalSeconds = BindInt(config, "Enemy Wave", "ReplenishIntervalSeconds", 10, 1, 300, "Seconds between checks that replenish missing wave enemies.");
        EnemyWaveDespawnOnEnd = BindBool(config, "Enemy Wave", "DespawnOnEnd", true, "Despawns surviving enemies activated by the event when it ends.");

        MinefieldEnabled = BindBool(config, "Minefield", "Enabled", true, "Includes Minefield in each event's independent rolls.");
        MinefieldChance = BindInt(config, "Minefield", "ChancePercent", DefaultEventChancePercent, 0, 100, "Independent chance for Minefield in each event.");
        MinefieldExplosiveEnabled = BindBool(config, "Minefield", "ExplosiveEnabled", true, "Allows explosive mines in Minefield.");
        MinefieldShockwaveEnabled = BindBool(config, "Minefield", "ShockwaveEnabled", true, "Allows shockwave mines in Minefield.");
        MinefieldStunEnabled = BindBool(config, "Minefield", "StunEnabled", true, "Allows stun mines in Minefield.");
        MinefieldSpawnCountMin = BindInt(config, "Minefield", "SpawnCountMin", 10, 1, 30, "Minimum number of armed mines kept active.");
        MinefieldSpawnCountMax = BindInt(config, "Minefield", "SpawnCountMax", 15, 1, 30, "Maximum number of armed mines kept active.");
        ConfigEntry<int> legacyMinefieldSpawnInterval = BindInt(config, "Minefield", "SpawnIntervalSeconds", 10, 1, 300, "Legacy setting migrated to ReplenishIntervalSeconds.");
        MinefieldMaximumActiveMines = BindInt(config, "Minefield", "MaximumActiveMines", 30, 1, 30, "Maximum number of event-created mines that may remain active.");
        MinefieldReplenishTriggeredMines = BindBool(config, "Minefield", "ReplenishTriggeredMines", true, "Replenishes triggered or destroyed event mines while Minefield remains active.");
        MinefieldReplenishIntervalSeconds = BindInt(config, "Minefield", "ReplenishIntervalSeconds", legacyMinefieldSpawnInterval.Value, 1, 300, "Seconds between Minefield replenishment checks.");
        MinefieldMinimumPlayerDistance = BindInt(config, "Minefield", "MinimumPlayerDistance", DefaultMinimumPlayerDistance, 0, 100, "Minimum distance from a living player when selecting a mine position.");
        MinefieldProtectValuables = BindBool(config, "Minefield", "ProtectValuables", true, "Temporarily protects targeted valuables from damage caused by Minefield.");

        FreezeEnabled = BindBool(config, "Freeze", "Enabled", true, "Includes Freeze in each event's independent rolls.");
        FreezeChance = BindInt(config, "Freeze", "ChancePercent", DefaultEventChancePercent, 0, 100, "Independent chance for Freeze in each event.");
        StunEnabled = BindBool(config, "Stun", "Enabled", true, "Includes enemy Stun in each event's independent rolls.");
        StunChance = BindInt(config, "Stun", "ChancePercent", DefaultEventChancePercent, 0, 100, "Independent chance for enemy Stun in each event.");
        EnemyWarpEnabled = BindBool(config, "Enemy Warp", "Enabled", true, "Includes Enemy Warp in each event's independent rolls.");
        EnemyWarpChance = BindInt(config, "Enemy Warp", "ChancePercent", DefaultEventChancePercent, 0, 100, "Independent chance for Enemy Warp in each event.");
        EnemyWarpEnemiesPerPulse = BindInt(config, "Enemy Warp", "EnemiesPerPulse", 3, 1, 30, "Maximum enemies teleported by each pulse.");
        EnemyWarpMinimumPlayerDistance = BindInt(config, "Enemy Warp", "MinimumPlayerDistance", DefaultMinimumPlayerDistance, 0, 100, "Minimum requested distance from players for enemy teleport destinations.");
        EnemyWarpIntervalSeconds = BindInt(config, "Enemy Warp", "IntervalSeconds", 10, 1, 300, "Seconds between Enemy Warp pulses.");
        EnemyHuntEnabled = BindBool(config, "Enemy Hunt", "Enabled", true, "Includes Enemy Hunt in each event's independent rolls.");
        EnemyHuntChance = BindInt(config, "Enemy Hunt", "ChancePercent", DefaultEventChancePercent, 0, 100, "Independent chance for Enemy Hunt in each event.");
        EnemyHuntRetargetIntervalSeconds = BindInt(config, "Enemy Hunt", "RetargetIntervalSeconds", 5, 1, 300, "Seconds between synchronized lure sounds in player-occupied rooms after all extractions are complete.");
        EnemySpeedUpEnabled = BindBool(config, "Enemy Speed Up", "Enabled", true, "Includes Enemy Speed Up in each event's independent rolls.");
        EnemySpeedUpChance = BindInt(config, "Enemy Speed Up", "ChancePercent", DefaultEventChancePercent, 0, 100, "Independent chance for Enemy Speed Up in each event.");
        EnemySpeedUpPercent = BindInt(config, "Enemy Speed Up", "SpeedPercent", 150, 101, 500, "Enemy navigation speed and acceleration percentage while Enemy Speed Up is active.");
        EnemySpeedDownEnabled = BindBool(config, "Enemy Speed Down", "Enabled", true, "Includes Enemy Speed Down in each event's independent rolls.");
        EnemySpeedDownChance = BindInt(config, "Enemy Speed Down", "ChancePercent", DefaultEventChancePercent, 0, 100, "Independent chance for Enemy Speed Down in each event.");
        EnemySpeedDownPercent = BindInt(config, "Enemy Speed Down", "SpeedPercent", 50, 10, 99, "Enemy navigation speed and acceleration percentage while Enemy Speed Down is active.");
        EnemyRegenEnabled = BindBool(config, "Enemy Regen", "Enabled", true, "Includes Enemy Regen in each event's independent rolls.");
        EnemyRegenChance = BindInt(config, "Enemy Regen", "ChancePercent", DefaultEventChancePercent, 0, 100, "Independent chance for Enemy Regen in each event.");
        EnemyRegenHealAmount = BindInt(config, "Enemy Regen", "HealAmount", 10, 1, 100, "Health restored to each enemy per pulse.");
        EnemyRegenIntervalSeconds = BindInt(config, "Enemy Regen", "HealIntervalSeconds", 5, 1, 300, "Seconds between Enemy Regen pulses.");
        EnemyPurgeEnabled = BindBool(config, "Enemy Purge", "Enabled", true, "Includes Enemy Purge in each event's independent rolls.");
        EnemyPurgeChance = BindInt(config, "Enemy Purge", "ChancePercent", DefaultEventChancePercent, 0, 100, "Independent chance for Enemy Purge in each event.");
        EnemyPurgeDamageAmount = BindInt(config, "Enemy Purge", "DamageAmount", 10, 1, 1000, "Damage dealt to each enemy per pulse.");
        EnemyPurgeCanKill = BindBool(config, "Enemy Purge", "CanKill", true, "Allows Enemy Purge to reduce an enemy to zero health.");
        EnemyPurgeIntervalSeconds = BindInt(config, "Enemy Purge", "DamageIntervalSeconds", 5, 1, 300, "Seconds between Enemy Purge pulses.");

        DamagePulseEnabled = BindBool(config, "Damage Pulse", "Enabled", true, "Includes Damage Pulse in each event's independent rolls.");
        DamagePulseChance = BindInt(config, "Damage Pulse", "ChancePercent", DefaultEventChancePercent, 0, 100, "Independent chance for Damage Pulse in each event.");
        DamagePulseAmount = BindInt(config, "Damage Pulse", "DamageAmount", 5, 1, 100, "Damage dealt to each living player per pulse.");
        DamagePulseSavingGrace = BindBool(config, "Damage Pulse", "SavingGrace", true, "Prevents Damage Pulse alone from reducing a player below one health.");
        DamagePulseIntervalSeconds = BindInt(config, "Damage Pulse", "DamageIntervalSeconds", 5, 1, 300, "Seconds between Damage Pulse pulses.");
        SecondChanceEnabled = BindBool(config, "Second Chance", "Enabled", true, "Includes Second Chance in each event's independent rolls.");
        SecondChanceChance = BindInt(config, "Second Chance", "ChancePercent", DefaultEventChancePercent, 0, 100, "Independent chance for Second Chance in each event.");
        SecondChanceMaxRevivesPerPlayer = BindInt(config, "Second Chance", "MaxRevivesPerPlayer", 1, 1, 10, "Maximum in-place revives granted to each player during one event.");
        SecondChanceCheckIntervalSeconds = BindInt(config, "Second Chance", "CheckIntervalSeconds", 2, 1, 300, "Seconds between ordinary death checks. A failed-stage retake is intercepted immediately.");
        KnockbackEnabled = BindBool(config, "Knockback", "Enabled", true, "Includes Knockback in each event's independent rolls.");
        KnockbackChance = BindInt(config, "Knockback", "ChancePercent", DefaultEventChancePercent, 0, 100, "Independent chance for Knockback in each event.");
        KnockbackHorizontalForce = BindInt(config, "Knockback", "HorizontalForce", 8, 0, 100, "Horizontal force applied by Knockback.");
        KnockbackVerticalForce = BindInt(config, "Knockback", "VerticalForce", 3, 0, 100, "Upward force applied by Knockback.");
        KnockbackIntervalSeconds = BindInt(config, "Knockback", "IntervalSeconds", 5, 1, 300, "Seconds between Knockback pulses.");
        FlickerEnabled = BindBool(config, "Flicker", "Enabled", true, "Includes Flicker in each event's independent rolls.");
        FlickerChance = BindInt(config, "Flicker", "ChancePercent", DefaultEventChancePercent, 0, 100, "Independent chance for Flicker in each event.");
        FlickerIntensityPercent = BindInt(config, "Flicker", "IntensityPercent", 200, 1, 500, "Flicker strength percentage. Higher values repeat more visible vanilla light transitions per pulse.");
        FlickerIntervalSeconds = BindInt(config, "Flicker", "IntervalSeconds", 2, 1, 300, "Seconds between vanilla light flicker pulses.");

        QuakeEnabled = BindBool(config, "Quake", "Enabled", true, "Includes Quake in each event's independent rolls.");
        QuakeChance = BindInt(config, "Quake", "ChancePercent", DefaultEventChancePercent, 0, 100, "Independent chance for Quake in each event.");
        QuakeForce = BindInt(config, "Quake", "Force", 8, 0, 100, "Force applied by Quake.");
        QuakeIntervalSeconds = BindInt(config, "Quake", "IntervalSeconds", 5, 1, 300, "Seconds between Quake pulses.");
        QuakeProtectValuables = BindBool(config, "Quake", "ProtectValuables", true, "Temporarily protects targeted valuables from damage caused by Quake.");
        DoorChaosEnabled = BindBool(config, "Door Chaos", "Enabled", true, "Includes Door Chaos in each event's independent rolls.");
        DoorChaosChance = BindInt(config, "Door Chaos", "ChancePercent", DefaultEventChancePercent, 0, 100, "Independent chance for Door Chaos in each event.");
        DoorChaosHingedItemsEnabled = BindBool(config, "Door Chaos", "HingedItemsEnabled", true, "Allows lids and other hinged movable items to be affected.");
        DoorChaosAffectedPercent = BindInt(config, "Door Chaos", "AffectedPercent", 30, 1, 100, "Percentage of eligible hinges changed by each pulse.");
        DoorChaosForce = BindInt(config, "Door Chaos", "Force", 12, 1, 30, "Physical force used to swing doors and hinged objects open or closed.");
        DoorChaosIntervalSeconds = BindInt(config, "Door Chaos", "IntervalSeconds", 2, 1, 300, "Seconds between Door Chaos pulses.");
        BindBool(config, "Trap Frenzy", "Enabled", false, "Obsolete setting removed in 4.2.0.");
        BindInt(config, "Trap Frenzy", "ChancePercent", 5, 0, 100, "Obsolete setting removed in 4.2.0.");
        BindInt(config, "Trap Frenzy", "IntervalSeconds", 10, 1, 300, "Obsolete setting removed in 4.2.0.");
        BindInt(config, "Trap Frenzy", "TrapsPerPulse", 3, 1, 30, "Obsolete setting removed in 4.2.0.");
        BindBool(config, "Trap Frenzy", "IncludeExplosiveTraps", false, "Obsolete setting removed in 4.2.0.");
        BindBool(config, "Object Shuffle", "Enabled", false, "Obsolete setting removed in 4.2.0.");
        BindInt(config, "Object Shuffle", "ChancePercent", 5, 0, 100, "Obsolete setting removed in 4.2.0.");
        BindInt(config, "Object Shuffle", "IntervalSeconds", 10, 1, 300, "Obsolete setting removed in 4.2.0.");
        BindInt(config, "Object Shuffle", "ObjectsPerPulse", 3, 1, 30, "Obsolete setting removed in 4.2.0.");
        BindInt(config, "Object Shuffle", "MinimumPlayerDistance", 5, 0, 100, "Obsolete setting removed in 4.2.0.");
        ValueSurgeEnabled = BindBool(config, "Value Surge", "Enabled", true, "Includes Value Surge in each event's independent rolls.");
        ValueSurgeChance = BindInt(config, "Value Surge", "ChancePercent", DefaultEventChancePercent, 0, 100, "Independent chance for Value Surge in each event.");
        ValueSurgeMultiplierPercent = BindInt(config, "Value Surge", "MultiplierPercent", 150, 1, 1000, "Temporary multiplier applied to all active valuable prices, including extraction, cart, and truck areas.");
        ValueSurgeRestoreOnEnd = BindBool(config, "Value Surge", "RestoreOnEnd", true, "Restores surviving valuables to their saved prices when the event ends.");
        ValueCrashEnabled = BindBool(config, "Value Crash", "Enabled", true, "Includes Value Crash in each event's independent rolls.");
        ValueCrashChance = BindInt(config, "Value Crash", "ChancePercent", DefaultEventChancePercent, 0, 100, "Independent chance for Value Crash in each event.");
        ValueCrashMultiplierPercent = BindInt(config, "Value Crash", "MultiplierPercent", 50, 1, 1000, "Temporary multiplier applied to all active valuable prices, including extraction, cart, and truck areas.");
        ValueCrashRestoreOnEnd = BindBool(config, "Value Crash", "RestoreOnEnd", true, "Restores surviving valuables to their saved prices when the event ends.");
        BindBool(config, "Music Override", "Enabled", false, "Obsolete setting removed in 4.2.0.");
        BindInt(config, "Music Override", "ChancePercent", 5, 0, 100, "Obsolete setting removed in 4.2.0.");
        config.Bind("Music Override", "TrackSelection", "Random", "Obsolete setting removed in 4.2.0.");
        BindBool(config, "Music Override", "RestoreOnEnd", true, "Obsolete setting removed in 4.2.0.");

        ConfigEntry<bool> legacyTargetValuables = BindBool(config, "What floats", "Valuables", true, "Legacy target setting.");
        ConfigEntry<bool> legacyTargetItems = BindBool(config, "What floats", "Items", true, "Legacy target setting.");
        ConfigEntry<bool> legacyTargetDoors = BindBool(config, "What floats", "Doors", false, "Legacy target setting.");
        ConfigEntry<bool> legacyTargetWeapons = BindBool(config, "What floats", "Weapons", true, "Legacy target setting.");
        ConfigEntry<bool> legacyTargetPlayers = BindBool(config, "What floats", "Players", true, "Legacy target setting.");
        ConfigEntry<bool> legacyTargetEnemies = BindBool(config, "What floats", "Enemies", true, "Legacy target setting.");
        TargetPlayers = BindBool(config, "Targets", "Players", legacyTargetPlayers.Value, "Allows broad multi-target physics events to affect players. Player-only events ignore this setting.");
        TargetEnemies = BindBool(config, "Targets", "Enemies", legacyTargetEnemies.Value, "Allows broad multi-target physics events to affect enemies. Enemy-only events ignore this setting.");
        TargetValuables = BindBool(config, "Targets", "Valuables", legacyTargetValuables.Value, "Allows broad multi-target physics events to affect valuables and enables automatic valuable protection. Valuable-only events ignore this setting for their primary effect.");
        TargetCosmeticBoxes = BindBool(config, "Targets", "CosmeticBoxes", false, "Allows broad multi-target physics events to affect Cosmetic Boxes independently from general items. Fixed-target events ignore this setting.");
        TargetItems = BindBool(config, "Targets", "Items", legacyTargetItems.Value, "Allows broad multi-target physics events to affect general items, carts, and movable props. Fixed-target events ignore this setting.");
        TargetWeapons = BindBool(config, "Targets", "Weapons", legacyTargetWeapons.Value, "Allows broad multi-target physics events to affect weapons. Fixed-target events ignore this setting.");
        TargetDoors = BindBool(config, "Targets", "Doors", legacyTargetDoors.Value, "Allows broad multi-target physics events to affect doors, lids, and other hinged objects. Door Chaos ignores this setting.");

        ConfigEntry<bool> legacyHudEnabled = BindBool(config, "UI", "Enabled", true, "Legacy HUD setting.");
        ConfigEntry<string> legacyHudAnchor = config.Bind("UI", "Anchor", "BottomRight", new ConfigDescription("Legacy HUD setting.", new AcceptableValueList<string>("TopLeft", "TopCenter", "TopRight", "MiddleLeft", "MiddleCenter", "MiddleRight", "BottomLeft", "BottomCenter", "BottomRight")));
        ConfigEntry<string> legacyHudAlignment = config.Bind("UI", "Alignment", "Right", new ConfigDescription("Legacy HUD setting.", new AcceptableValueList<string>("Left", "Center", "Right")));
        ConfigEntry<int> legacyHudOffsetX = BindInt(config, "UI", "OffsetX", 0, -3840, 3840, "Legacy HUD setting.");
        ConfigEntry<int> legacyHudOffsetY = BindInt(config, "UI", "OffsetY", 0, -2160, 2160, "Legacy HUD setting.");
        ConfigEntry<int> legacyHudScale = BindInt(config, "UI", "ScalePercent", 70, 50, 200, "Legacy HUD setting.");
        ConfigEntry<bool> legacyChatEnabled = BindBool(config, "UI", "StageStartChatEnabled", true, "Legacy notification setting.");
        ConfigEntry<bool> legacyCountdownEnabled = BindBool(config, "UI", "CountdownEnabled", true, "Legacy countdown setting migrated to the separate start and end settings.");
        ConfigEntry<bool> legacyStartCountdownEnabled = BindBool(config, "UI", "StartCountdownEnabled", legacyCountdownEnabled.Value, "Legacy notification setting.");
        ConfigEntry<bool> legacyEndCountdownEnabled = BindBool(config, "UI", "EndCountdownEnabled", legacyCountdownEnabled.Value, "Legacy notification setting.");
        ChatAnnouncementsEnabled = BindBool(config, "Notifications", "ChatAnnouncementsEnabled", legacyChatEnabled.Value, "Broadcasts the stage summary, effect names, countdowns, and End through vanilla chat.");
        StartCountdownEnabled = BindBool(config, "Notifications", "StartCountdownEnabled", legacyStartCountdownEnabled.Value, "Enables the five-second countdown during the final five seconds of the interval. When disabled, only the effect name is announced three seconds before the interval ends.");
        EndCountdownEnabled = BindBool(config, "Notifications", "EndCountdownEnabled", legacyEndCountdownEnabled.Value, "Enables the three-second countdown before an event ends. When disabled, End is announced when the effect ends.");
        EnemyReactionEnabled = BindBool(config, "Notifications", "EnemyReactionEnabled", false, "Allows enemies to investigate sounds generated by Stage Flux chat notifications. Ordinary voice chat and other sounds remain detectable.");
        HudEnabled = BindBool(config, "HUD", "Enabled", legacyHudEnabled.Value, "Shows the synchronized event HUD on this client.");
        HudStyle = config.Bind("HUD", "Style", "Graphical", new ConfigDescription(
            "Graphical shows the five-slot icon HUD. Classic restores the previous text-only HUD.",
            new AcceptableValueList<string>("Graphical", "Classic")));
        HudLayoutDirection = config.Bind("HUD", "LayoutDirection", "Vertical", new ConfigDescription(
            "Direction used by the Graphical HUD. Slots stop from top to bottom in Vertical mode and from left to right in Horizontal mode.",
            new AcceptableValueList<string>("Vertical", "Horizontal")));
        HudAnchor = config.Bind("HUD", "Anchor", legacyHudAnchor.Value, new ConfigDescription(
            "Anchor point used to position the HUD.",
            new AcceptableValueList<string>("TopLeft", "TopCenter", "TopRight", "MiddleLeft", "MiddleCenter", "MiddleRight", "BottomLeft", "BottomCenter", "BottomRight")));
        HudAlignment = config.Bind("HUD", "Alignment", legacyHudAlignment.Value, new ConfigDescription(
            "Horizontal alignment of the HUD text.",
            new AcceptableValueList<string>("Left", "Center", "Right")));
        HudOffsetX = BindInt(config, "HUD", "OffsetX", legacyHudOffsetX.Value, -3840, 3840, "Horizontal offset from the anchor in pixels.");
        HudOffsetY = BindInt(config, "HUD", "OffsetY", legacyHudOffsetY.Value, -2160, 2160, "Vertical offset from the anchor in pixels.");
        HudScalePercent = BindInt(config, "HUD", "ScalePercent", legacyHudScale.Value, 50, 200, "HUD scale percentage.");
        HudBackgroundOpacityPercent = BindInt(config, "HUD", "BackgroundOpacityPercent", 50, 0, 100, "Graphical HUD background opacity percentage. HUD icons and text remain fully visible.");

        if (configRevision < 4)
        {
            MigrateSpawnCountDefaults(
                ShockwaveSpawnCountMin, ShockwaveSpawnCountMax, 1, 3, 5, 10, "Shockwave");
            MigrateSpawnCountDefaults(
                StunBlastSpawnCountMin, StunBlastSpawnCountMax, 1, 3, 5, 10, "Stun Blast");
            MigrateSpawnCountDefaults(
                ExplosionRainSpawnCountMin, ExplosionRainSpawnCountMax, 1, 3, 5, 10, "Explosion Rain");
            MigrateSpawnCountDefaults(
                MinefieldSpawnCountMin, MinefieldSpawnCountMax, 3, 6, 10, 15, "Minefield");
        }
        if (configRevision < 7 && DoorChaosIntervalSeconds.Value == 5)
        {
            DoorChaosIntervalSeconds.Value = 2;
            _logger.LogInfo(
                "Migrated the previous default Door Chaos interval from 5 seconds to 2 seconds.");
        }
        if (configRevision < 9)
        {
            int migratedProtectionDelay = Math.Max(
                ValuableProtectionReleaseDelaySeconds.Value,
                Math.Max(
                    legacyZeroGravityProtectionDelay,
                    Math.Max(legacyRollProtectionDelay, legacyVoidProtectionDelay)));
            if (ValuableProtectionReleaseDelaySeconds.Value != migratedProtectionDelay)
            {
                ValuableProtectionReleaseDelaySeconds.Value = migratedProtectionDelay;
                _logger.LogInfo(
                    $"Migrated the longest event-specific valuable protection release delay to " +
                    $"Safety.ValuableProtectionReleaseDelaySeconds ({migratedProtectionDelay} seconds).");
            }
        }
        if (configRevision < 10)
        {
            MigrateMinimumPlayerDistanceDefault(HealingAuraMinimumPlayerDistance, 0, "Healing Aura");
            MigrateMinimumPlayerDistanceDefault(StarBarrageMinimumPlayerDistance, 5, "Star Barrage");
            MigrateMinimumPlayerDistanceDefault(ShockwaveMinimumPlayerDistance, 5, "Shockwave");
            MigrateMinimumPlayerDistanceDefault(StunBlastMinimumPlayerDistance, 5, "Stun Blast");
            MigrateMinimumPlayerDistanceDefault(ExplosionRainMinimumPlayerDistance, 10, "Explosion Rain");
            MigrateMinimumPlayerDistanceDefault(EnemyWaveMinimumPlayerDistance, 10, "Enemy Wave");
            MigrateMinimumPlayerDistanceDefault(MinefieldMinimumPlayerDistance, 8, "Minefield");
            MigrateMinimumPlayerDistanceDefault(EnemyWarpMinimumPlayerDistance, 10, "Enemy Warp");
        }
        ConfigEntry<int>[] eventChanceSettings =
        {
                FeatherChance,
                ZeroGravityChance,
                BatteryChance,
                HealChance,
                IndestructibleChance,
                FragilityChance,
                GumballHypnosisChance,
                HealingAuraChance,
                StarBarrageChance,
                SpiderScareChance,
                TrafficShockChance,
                DangerousValuablesChance,
                RollChance,
                VoidChance,
                LevitationChance,
                ShockwaveChance,
                StunBlastChance,
                ExplosionRainChance,
                EnemyWaveChance,
                MinefieldChance,
                FreezeChance,
                StunChance,
                EnemyWarpChance,
                EnemyHuntChance,
                EnemySpeedUpChance,
                EnemySpeedDownChance,
                EnemyRegenChance,
                EnemyPurgeChance,
                DamagePulseChance,
                SecondChanceChance,
                KnockbackChance,
                FlickerChance,
                QuakeChance,
                DoorChaosChance,
                ValueSurgeChance,
                ValueCrashChance
        };
        if (configRevision < 11)
        {
            MigrateEventChanceDefaults(5, eventChanceSettings);
        }
        if (configRevision < 12)
        {
            MigrateEventChanceDefaults(8, eventChanceSettings);
        }
        if (configRevision < CurrentConfigRevision)
        {
            WriteConfigRevision(config, configRevisionDefinition);
        }

        config.Remove(new ConfigDefinition(legacyFeatherSection, "Enabled"));
        config.Remove(new ConfigDefinition(legacyFeatherSection, "ChancePercent"));
        config.Remove(new ConfigDefinition("Magnet", "Enabled"));
        config.Remove(new ConfigDefinition("Magnet", "ChancePercent"));
        config.Remove(new ConfigDefinition("Torque", "Enabled"));
        config.Remove(new ConfigDefinition("Torque", "ChancePercent"));
        config.Remove(new ConfigDefinition("Roll Staff", "Enabled"));
        config.Remove(new ConfigDefinition("Roll Staff", "ChancePercent"));
        config.Remove(new ConfigDefinition("Boost", "Enabled"));
        config.Remove(new ConfigDefinition("Boost", "ChancePercent"));
        config.Remove(new ConfigDefinition("Battery", "Enabled"));
        config.Remove(new ConfigDefinition("Battery", "ChancePercent"));
        config.Remove(new ConfigDefinition("Battery", "ChargeAmount"));
        config.Remove(new ConfigDefinition("Battery", "ChargeIntervalSeconds"));
        config.Remove(new ConfigDefinition("BatteryCharge", "Enabled"));
        config.Remove(new ConfigDefinition("BatteryCharge", "ChancePercent"));
        config.Remove(new ConfigDefinition("BatteryCharge", "ChargeAmount"));
        config.Remove(new ConfigDefinition("BatteryCharge", "ChargeIntervalSeconds"));
        config.Remove(new ConfigDefinition("General", "StageEffectChancePercent"));
        config.Remove(new ConfigDefinition("Safety", "HazardIndestructibleReleaseDelaySeconds"));
        RemoveObsoleteConfigEntry(config, "Zero Gravity", "IndestructibleReleaseDelaySeconds");
        RemoveObsoleteConfigEntry(config, "Roll", "IndestructibleReleaseDelaySeconds");
        RemoveObsoleteConfigEntry(config, "Void", "IndestructibleReleaseDelaySeconds");
        config.Remove(new ConfigDefinition("Minefield", "SpawnIntervalSeconds"));
        RemoveObsoleteConfigEntry(config, "Minefield", "RemoveOnEventEnd");
        RemoveObsoleteConfigEntry(config, "Dangerous Valuables", "RemoveOnEventEnd");
        config.Remove(new ConfigDefinition("Dangerous Valuables", "SpawnCountMin"));
        config.Remove(new ConfigDefinition("Dangerous Valuables", "SpawnCountMax"));
        config.Remove(new ConfigDefinition("Dangerous Valuables", "ReplenishIntervalSeconds"));
        config.Remove(new ConfigDefinition("Dangerous Valuables", "MinimumPlayerDistance"));
        config.Remove(new ConfigDefinition("Dangerous Valuables", "ReplenishDestroyed"));
        config.Remove(new ConfigDefinition("Trap Frenzy", "Enabled"));
        config.Remove(new ConfigDefinition("Trap Frenzy", "ChancePercent"));
        config.Remove(new ConfigDefinition("Trap Frenzy", "IntervalSeconds"));
        config.Remove(new ConfigDefinition("Trap Frenzy", "TrapsPerPulse"));
        config.Remove(new ConfigDefinition("Trap Frenzy", "IncludeExplosiveTraps"));
        config.Remove(new ConfigDefinition("Object Shuffle", "Enabled"));
        config.Remove(new ConfigDefinition("Object Shuffle", "ChancePercent"));
        config.Remove(new ConfigDefinition("Object Shuffle", "IntervalSeconds"));
        config.Remove(new ConfigDefinition("Object Shuffle", "ObjectsPerPulse"));
        config.Remove(new ConfigDefinition("Object Shuffle", "MinimumPlayerDistance"));
        config.Remove(new ConfigDefinition("Music Override", "Enabled"));
        config.Remove(new ConfigDefinition("Music Override", "ChancePercent"));
        config.Remove(new ConfigDefinition("Music Override", "TrackSelection"));
        config.Remove(new ConfigDefinition("Music Override", "RestoreOnEnd"));
        RemoveObsoleteConfigEntry(config, "Door Chaos", "MapDoorsEnabled");
        config.Remove(new ConfigDefinition("What floats", "Valuables"));
        config.Remove(new ConfigDefinition("What floats", "Items"));
        config.Remove(new ConfigDefinition("What floats", "Doors"));
        config.Remove(new ConfigDefinition("What floats", "Weapons"));
        config.Remove(new ConfigDefinition("What floats", "Players"));
        config.Remove(new ConfigDefinition("What floats", "Enemies"));
        config.Remove(new ConfigDefinition("UI", "Enabled"));
        config.Remove(new ConfigDefinition("UI", "StageStartChatEnabled"));
        config.Remove(new ConfigDefinition("UI", "CountdownEnabled"));
        config.Remove(new ConfigDefinition("UI", "StartCountdownEnabled"));
        config.Remove(new ConfigDefinition("UI", "EndCountdownEnabled"));
        config.Remove(new ConfigDefinition("UI", "Anchor"));
        config.Remove(new ConfigDefinition("UI", "Alignment"));
        config.Remove(new ConfigDefinition("UI", "OffsetX"));
        config.Remove(new ConfigDefinition("UI", "OffsetY"));
        config.Remove(new ConfigDefinition("UI", "ScalePercent"));
        BindExtendedEvents(config);
        config.SaveOnConfigSet = saveOnConfigSet;
        config.Save();

        FeatherEnabled.SettingChanged += ProbabilitySettingChanged;
        FeatherChance.SettingChanged += ProbabilitySettingChanged;
        ZeroGravityEnabled.SettingChanged += ProbabilitySettingChanged;
        ZeroGravityChance.SettingChanged += ProbabilitySettingChanged;
        BatteryEnabled.SettingChanged += ProbabilitySettingChanged;
        BatteryChance.SettingChanged += ProbabilitySettingChanged;
        HealEnabled.SettingChanged += ProbabilitySettingChanged;
        HealChance.SettingChanged += ProbabilitySettingChanged;
        IndestructibleEnabled.SettingChanged += ProbabilitySettingChanged;
        IndestructibleChance.SettingChanged += ProbabilitySettingChanged;
        FragilityEnabled.SettingChanged += ProbabilitySettingChanged;
        FragilityChance.SettingChanged += ProbabilitySettingChanged;
        GumballHypnosisEnabled.SettingChanged += ProbabilitySettingChanged;
        GumballHypnosisChance.SettingChanged += ProbabilitySettingChanged;
        HealingAuraEnabled.SettingChanged += ProbabilitySettingChanged;
        HealingAuraChance.SettingChanged += ProbabilitySettingChanged;
        StarBarrageEnabled.SettingChanged += ProbabilitySettingChanged;
        StarBarrageChance.SettingChanged += ProbabilitySettingChanged;
        SpiderScareEnabled.SettingChanged += ProbabilitySettingChanged;
        SpiderScareChance.SettingChanged += ProbabilitySettingChanged;
        TrafficShockEnabled.SettingChanged += ProbabilitySettingChanged;
        TrafficShockChance.SettingChanged += ProbabilitySettingChanged;
        DangerousValuablesEnabled.SettingChanged += ProbabilitySettingChanged;
        DangerousValuablesChance.SettingChanged += ProbabilitySettingChanged;
        DangerousValuablesIceSawEnabled.SettingChanged += ProbabilitySettingChanged;
        DangerousValuablesBlenderEnabled.SettingChanged += ProbabilitySettingChanged;
        DangerousValuablesFlamethrowerEnabled.SettingChanged += ProbabilitySettingChanged;
        DangerousValuablesEggEnabled.SettingChanged += ProbabilitySettingChanged;
        DangerousValuablesCarEnabled.SettingChanged += ProbabilitySettingChanged;
        DangerousValuablesPlaneEnabled.SettingChanged += ProbabilitySettingChanged;
        DangerousValuablesBroomEnabled.SettingChanged += ProbabilitySettingChanged;
        RollEnabled.SettingChanged += ProbabilitySettingChanged;
        RollChance.SettingChanged += ProbabilitySettingChanged;
        VoidEnabled.SettingChanged += ProbabilitySettingChanged;
        VoidChance.SettingChanged += ProbabilitySettingChanged;
        LevitationEnabled.SettingChanged += ProbabilitySettingChanged;
        LevitationChance.SettingChanged += ProbabilitySettingChanged;
        ShockwaveEnabled.SettingChanged += ProbabilitySettingChanged;
        ShockwaveChance.SettingChanged += ProbabilitySettingChanged;
        StunBlastEnabled.SettingChanged += ProbabilitySettingChanged;
        StunBlastChance.SettingChanged += ProbabilitySettingChanged;
        ExplosionRainEnabled.SettingChanged += ProbabilitySettingChanged;
        ExplosionRainChance.SettingChanged += ProbabilitySettingChanged;
        EnemyWaveEnabled.SettingChanged += ProbabilitySettingChanged;
        EnemyWaveChance.SettingChanged += ProbabilitySettingChanged;
        MinefieldEnabled.SettingChanged += ProbabilitySettingChanged;
        MinefieldChance.SettingChanged += ProbabilitySettingChanged;
        MinefieldExplosiveEnabled.SettingChanged += ProbabilitySettingChanged;
        MinefieldShockwaveEnabled.SettingChanged += ProbabilitySettingChanged;
        MinefieldStunEnabled.SettingChanged += ProbabilitySettingChanged;
        FreezeEnabled.SettingChanged += ProbabilitySettingChanged;
        FreezeChance.SettingChanged += ProbabilitySettingChanged;
        StunEnabled.SettingChanged += ProbabilitySettingChanged;
        StunChance.SettingChanged += ProbabilitySettingChanged;
        EnemyWarpEnabled.SettingChanged += ProbabilitySettingChanged;
        EnemyWarpChance.SettingChanged += ProbabilitySettingChanged;
        EnemyHuntEnabled.SettingChanged += ProbabilitySettingChanged;
        EnemyHuntChance.SettingChanged += ProbabilitySettingChanged;
        EnemySpeedUpEnabled.SettingChanged += ProbabilitySettingChanged;
        EnemySpeedUpChance.SettingChanged += ProbabilitySettingChanged;
        EnemySpeedDownEnabled.SettingChanged += ProbabilitySettingChanged;
        EnemySpeedDownChance.SettingChanged += ProbabilitySettingChanged;
        EnemyRegenEnabled.SettingChanged += ProbabilitySettingChanged;
        EnemyRegenChance.SettingChanged += ProbabilitySettingChanged;
        EnemyPurgeEnabled.SettingChanged += ProbabilitySettingChanged;
        EnemyPurgeChance.SettingChanged += ProbabilitySettingChanged;
        DamagePulseEnabled.SettingChanged += ProbabilitySettingChanged;
        DamagePulseChance.SettingChanged += ProbabilitySettingChanged;
        SecondChanceEnabled.SettingChanged += ProbabilitySettingChanged;
        SecondChanceChance.SettingChanged += ProbabilitySettingChanged;
        KnockbackEnabled.SettingChanged += ProbabilitySettingChanged;
        KnockbackChance.SettingChanged += ProbabilitySettingChanged;
        FlickerEnabled.SettingChanged += ProbabilitySettingChanged;
        FlickerChance.SettingChanged += ProbabilitySettingChanged;
        QuakeEnabled.SettingChanged += ProbabilitySettingChanged;
        QuakeChance.SettingChanged += ProbabilitySettingChanged;
        DoorChaosEnabled.SettingChanged += ProbabilitySettingChanged;
        DoorChaosChance.SettingChanged += ProbabilitySettingChanged;
        ValueSurgeEnabled.SettingChanged += ProbabilitySettingChanged;
        ValueSurgeChance.SettingChanged += ProbabilitySettingChanged;
        ValueCrashEnabled.SettingChanged += ProbabilitySettingChanged;
        ValueCrashChance.SettingChanged += ProbabilitySettingChanged;
    }

    internal ConfigEntry<bool> Enabled { get; }
    internal ConfigEntry<string> Mode { get; }
    internal ConfigEntry<int> StageActivationChancePercent { get; }
    internal ConfigEntry<int> MaxSimultaneousEffects { get; }
    internal ConfigEntry<bool> AllowDangerousCombinations { get; }
    internal ConfigEntry<int> ValuableProtectionReleaseDelaySeconds { get; }
    internal ConfigEntry<int> IntervalMinSeconds { get; }
    internal ConfigEntry<int> IntervalMaxSeconds { get; }
    internal ConfigEntry<int> EffectDurationMinSeconds { get; }
    internal ConfigEntry<int> EffectDurationMaxSeconds { get; }
    internal ConfigEntry<bool> FeatherEnabled { get; }
    internal ConfigEntry<int> FeatherChance { get; }
    internal ConfigEntry<bool> ZeroGravityEnabled { get; }
    internal ConfigEntry<int> ZeroGravityChance { get; }
    internal ConfigEntry<bool> ZeroGravityProtectValuables { get; }
    internal ConfigEntry<bool> BatteryEnabled { get; }
    internal ConfigEntry<int> BatteryChance { get; }
    internal ConfigEntry<int> BatteryChargeAmount { get; }
    internal ConfigEntry<int> BatteryChargeIntervalSeconds { get; }
    internal ConfigEntry<bool> HealEnabled { get; }
    internal ConfigEntry<int> HealChance { get; }
    internal ConfigEntry<int> HealAmount { get; }
    internal ConfigEntry<int> HealIntervalSeconds { get; }
    internal ConfigEntry<bool> IndestructibleEnabled { get; }
    internal ConfigEntry<int> IndestructibleChance { get; }
    internal ConfigEntry<bool> FragilityEnabled { get; }
    internal ConfigEntry<int> FragilityChance { get; }
    internal ConfigEntry<int> FragilityMultiplierPercent { get; }
    internal ConfigEntry<bool> GumballHypnosisEnabled { get; }
    internal ConfigEntry<int> GumballHypnosisChance { get; }
    internal ConfigEntry<bool> HealingAuraEnabled { get; }
    internal ConfigEntry<int> HealingAuraChance { get; }
    internal ConfigEntry<int> HealingAuraHealthPool { get; }
    internal ConfigEntry<int> HealingAuraSpawnCountMin { get; }
    internal ConfigEntry<int> HealingAuraSpawnCountMax { get; }
    internal ConfigEntry<int> HealingAuraSpawnIntervalSeconds { get; }
    internal ConfigEntry<int> HealingAuraMinimumPlayerDistance { get; }
    internal ConfigEntry<int> HealingAuraMaximumActiveInstances { get; }
    internal ConfigEntry<bool> StarBarrageEnabled { get; }
    internal ConfigEntry<int> StarBarrageChance { get; }
    internal ConfigEntry<bool> StarBarrageProtectValuables { get; }
    internal ConfigEntry<int> StarBarrageProjectileCountMin { get; }
    internal ConfigEntry<int> StarBarrageProjectileCountMax { get; }
    internal ConfigEntry<int> StarBarrageSpawnIntervalSeconds { get; }
    internal ConfigEntry<int> StarBarrageMinimumPlayerDistance { get; }
    internal ConfigEntry<int> StarBarrageMaximumActiveInstances { get; }
    internal ConfigEntry<bool> SpiderScareEnabled { get; }
    internal ConfigEntry<int> SpiderScareChance { get; }
    internal ConfigEntry<int> SpiderScarePlayersPerWaveMin { get; }
    internal ConfigEntry<int> SpiderScarePlayersPerWaveMax { get; }
    internal ConfigEntry<int> SpiderScareSpawnIntervalSeconds { get; }
    internal ConfigEntry<bool> TrafficShockEnabled { get; }
    internal ConfigEntry<int> TrafficShockChance { get; }
    internal ConfigEntry<int> TrafficShockPlayersPerPulseMin { get; }
    internal ConfigEntry<int> TrafficShockPlayersPerPulseMax { get; }
    internal ConfigEntry<int> TrafficShockPulseIntervalSeconds { get; }
    internal ConfigEntry<bool> DangerousValuablesEnabled { get; }
    internal ConfigEntry<int> DangerousValuablesChance { get; }
    internal ConfigEntry<bool> DangerousValuablesProtectValuables { get; }
    internal ConfigEntry<bool> DangerousValuablesIceSawEnabled { get; }
    internal ConfigEntry<bool> DangerousValuablesBlenderEnabled { get; }
    internal ConfigEntry<bool> DangerousValuablesFlamethrowerEnabled { get; }
    internal ConfigEntry<bool> DangerousValuablesEggEnabled { get; }
    internal ConfigEntry<bool> DangerousValuablesCarEnabled { get; }
    internal ConfigEntry<bool> DangerousValuablesPlaneEnabled { get; }
    internal ConfigEntry<bool> DangerousValuablesBroomEnabled { get; }
    internal ConfigEntry<int> DangerousValuablesActivationCountMin { get; }
    internal ConfigEntry<int> DangerousValuablesActivationCountMax { get; }
    internal ConfigEntry<int> DangerousValuablesReactivationIntervalSeconds { get; }
    internal ConfigEntry<int> DangerousValuablesMaximumActiveInstances { get; }
    internal ConfigEntry<bool> RollEnabled { get; }
    internal ConfigEntry<int> RollChance { get; }
    internal ConfigEntry<bool> RollProtectValuables { get; }
    internal ConfigEntry<bool> VoidEnabled { get; }
    internal ConfigEntry<int> VoidChance { get; }
    internal ConfigEntry<bool> VoidProtectValuables { get; }
    internal ConfigEntry<int> VoidSpawnCountMin { get; }
    internal ConfigEntry<int> VoidSpawnCountMax { get; }
    internal ConfigEntry<int> VoidSpawnIntervalSeconds { get; }
    internal ConfigEntry<bool> LevitationEnabled { get; }
    internal ConfigEntry<int> LevitationChance { get; }
    internal ConfigEntry<bool> LevitationProtectValuables { get; }
    internal ConfigEntry<int> LevitationSpawnCountMin { get; }
    internal ConfigEntry<int> LevitationSpawnCountMax { get; }
    internal ConfigEntry<int> LevitationSpawnIntervalSeconds { get; }
    internal ConfigEntry<int> LevitationMinimumPlayerDistance { get; }
    internal ConfigEntry<int> LevitationMaximumActiveInstances { get; }
    internal ConfigEntry<bool> ShockwaveEnabled { get; }
    internal ConfigEntry<int> ShockwaveChance { get; }
    internal ConfigEntry<bool> ShockwaveProtectValuables { get; }
    internal ConfigEntry<int> ShockwaveSpawnCountMin { get; }
    internal ConfigEntry<int> ShockwaveSpawnCountMax { get; }
    internal ConfigEntry<int> ShockwaveSpawnIntervalSeconds { get; }
    internal ConfigEntry<int> ShockwaveMinimumPlayerDistance { get; }
    internal ConfigEntry<int> ShockwaveMaximumActiveInstances { get; }
    internal ConfigEntry<int> ShockwaveLaunchForceMin { get; }
    internal ConfigEntry<int> ShockwaveLaunchForceMax { get; }
    internal ConfigEntry<bool> StunBlastEnabled { get; }
    internal ConfigEntry<int> StunBlastChance { get; }
    internal ConfigEntry<bool> StunBlastProtectValuables { get; }
    internal ConfigEntry<int> StunBlastSpawnCountMin { get; }
    internal ConfigEntry<int> StunBlastSpawnCountMax { get; }
    internal ConfigEntry<int> StunBlastSpawnIntervalSeconds { get; }
    internal ConfigEntry<int> StunBlastMinimumPlayerDistance { get; }
    internal ConfigEntry<int> StunBlastMaximumActiveInstances { get; }
    internal ConfigEntry<int> StunBlastLaunchForceMin { get; }
    internal ConfigEntry<int> StunBlastLaunchForceMax { get; }
    internal ConfigEntry<bool> ExplosionRainEnabled { get; }
    internal ConfigEntry<int> ExplosionRainChance { get; }
    internal ConfigEntry<bool> ExplosionRainProtectValuables { get; }
    internal ConfigEntry<int> ExplosionRainSpawnCountMin { get; }
    internal ConfigEntry<int> ExplosionRainSpawnCountMax { get; }
    internal ConfigEntry<int> ExplosionRainSpawnIntervalSeconds { get; }
    internal ConfigEntry<int> ExplosionRainMinimumPlayerDistance { get; }
    internal ConfigEntry<int> ExplosionRainMaximumActiveInstances { get; }
    internal ConfigEntry<int> ExplosionRainLaunchForceMin { get; }
    internal ConfigEntry<int> ExplosionRainLaunchForceMax { get; }
    internal ConfigEntry<bool> EnemyWaveEnabled { get; }
    internal ConfigEntry<int> EnemyWaveChance { get; }
    internal ConfigEntry<int> EnemyWaveSpawnCountMin { get; }
    internal ConfigEntry<int> EnemyWaveSpawnCountMax { get; }
    internal ConfigEntry<int> EnemyWaveReplenishIntervalSeconds { get; }
    internal ConfigEntry<int> EnemyWaveMinimumPlayerDistance { get; }
    internal ConfigEntry<bool> EnemyWaveDespawnOnEnd { get; }
    internal ConfigEntry<bool> MinefieldEnabled { get; }
    internal ConfigEntry<int> MinefieldChance { get; }
    internal ConfigEntry<bool> MinefieldProtectValuables { get; }
    internal ConfigEntry<bool> MinefieldExplosiveEnabled { get; }
    internal ConfigEntry<bool> MinefieldShockwaveEnabled { get; }
    internal ConfigEntry<bool> MinefieldStunEnabled { get; }
    internal ConfigEntry<int> MinefieldSpawnCountMin { get; }
    internal ConfigEntry<int> MinefieldSpawnCountMax { get; }
    internal ConfigEntry<int> MinefieldReplenishIntervalSeconds { get; }
    internal ConfigEntry<int> MinefieldMaximumActiveMines { get; }
    internal ConfigEntry<int> MinefieldMinimumPlayerDistance { get; }
    internal ConfigEntry<bool> MinefieldReplenishTriggeredMines { get; }
    internal ConfigEntry<bool> FreezeEnabled { get; }
    internal ConfigEntry<int> FreezeChance { get; }
    internal ConfigEntry<bool> StunEnabled { get; }
    internal ConfigEntry<int> StunChance { get; }
    internal ConfigEntry<bool> EnemyWarpEnabled { get; }
    internal ConfigEntry<int> EnemyWarpChance { get; }
    internal ConfigEntry<int> EnemyWarpIntervalSeconds { get; }
    internal ConfigEntry<int> EnemyWarpEnemiesPerPulse { get; }
    internal ConfigEntry<int> EnemyWarpMinimumPlayerDistance { get; }
    internal ConfigEntry<bool> EnemyHuntEnabled { get; }
    internal ConfigEntry<int> EnemyHuntChance { get; }
    internal ConfigEntry<int> EnemyHuntRetargetIntervalSeconds { get; }
    internal ConfigEntry<bool> EnemySpeedUpEnabled { get; }
    internal ConfigEntry<int> EnemySpeedUpChance { get; }
    internal ConfigEntry<int> EnemySpeedUpPercent { get; }
    internal ConfigEntry<bool> EnemySpeedDownEnabled { get; }
    internal ConfigEntry<int> EnemySpeedDownChance { get; }
    internal ConfigEntry<int> EnemySpeedDownPercent { get; }
    internal ConfigEntry<bool> EnemyRegenEnabled { get; }
    internal ConfigEntry<int> EnemyRegenChance { get; }
    internal ConfigEntry<int> EnemyRegenHealAmount { get; }
    internal ConfigEntry<int> EnemyRegenIntervalSeconds { get; }
    internal ConfigEntry<bool> EnemyPurgeEnabled { get; }
    internal ConfigEntry<int> EnemyPurgeChance { get; }
    internal ConfigEntry<int> EnemyPurgeDamageAmount { get; }
    internal ConfigEntry<int> EnemyPurgeIntervalSeconds { get; }
    internal ConfigEntry<bool> EnemyPurgeCanKill { get; }
    internal ConfigEntry<bool> DamagePulseEnabled { get; }
    internal ConfigEntry<int> DamagePulseChance { get; }
    internal ConfigEntry<int> DamagePulseAmount { get; }
    internal ConfigEntry<int> DamagePulseIntervalSeconds { get; }
    internal ConfigEntry<bool> DamagePulseSavingGrace { get; }
    internal ConfigEntry<bool> SecondChanceEnabled { get; }
    internal ConfigEntry<int> SecondChanceChance { get; }
    internal ConfigEntry<int> SecondChanceCheckIntervalSeconds { get; }
    internal ConfigEntry<int> SecondChanceMaxRevivesPerPlayer { get; }
    internal ConfigEntry<bool> KnockbackEnabled { get; }
    internal ConfigEntry<int> KnockbackChance { get; }
    internal ConfigEntry<int> KnockbackIntervalSeconds { get; }
    internal ConfigEntry<int> KnockbackHorizontalForce { get; }
    internal ConfigEntry<int> KnockbackVerticalForce { get; }
    internal ConfigEntry<bool> FlickerEnabled { get; }
    internal ConfigEntry<int> FlickerChance { get; }
    internal ConfigEntry<int> FlickerIntervalSeconds { get; }
    internal ConfigEntry<int> FlickerIntensityPercent { get; }
    internal ConfigEntry<bool> QuakeEnabled { get; }
    internal ConfigEntry<int> QuakeChance { get; }
    internal ConfigEntry<bool> QuakeProtectValuables { get; }
    internal ConfigEntry<int> QuakeIntervalSeconds { get; }
    internal ConfigEntry<int> QuakeForce { get; }
    internal ConfigEntry<bool> DoorChaosEnabled { get; }
    internal ConfigEntry<int> DoorChaosChance { get; }
    internal ConfigEntry<int> DoorChaosIntervalSeconds { get; }
    internal ConfigEntry<int> DoorChaosAffectedPercent { get; }
    internal ConfigEntry<int> DoorChaosForce { get; }
    internal ConfigEntry<bool> DoorChaosHingedItemsEnabled { get; }
    internal ConfigEntry<bool> ValueSurgeEnabled { get; }
    internal ConfigEntry<int> ValueSurgeChance { get; }
    internal ConfigEntry<int> ValueSurgeMultiplierPercent { get; }
    internal ConfigEntry<bool> ValueSurgeRestoreOnEnd { get; }
    internal ConfigEntry<bool> ValueCrashEnabled { get; }
    internal ConfigEntry<int> ValueCrashChance { get; }
    internal ConfigEntry<int> ValueCrashMultiplierPercent { get; }
    internal ConfigEntry<bool> ValueCrashRestoreOnEnd { get; }
    internal ConfigEntry<bool> TargetValuables { get; }
    internal ConfigEntry<bool> TargetCosmeticBoxes { get; }
    internal ConfigEntry<bool> TargetItems { get; }
    internal ConfigEntry<bool> TargetDoors { get; }
    internal ConfigEntry<bool> TargetWeapons { get; }
    internal ConfigEntry<bool> TargetPlayers { get; }
    internal ConfigEntry<bool> TargetEnemies { get; }
    internal ConfigEntry<bool> HudEnabled { get; }
    internal ConfigEntry<bool> ChatAnnouncementsEnabled { get; }
    internal ConfigEntry<bool> EnemyReactionEnabled { get; }
    internal ConfigEntry<bool> StartCountdownEnabled { get; }
    internal ConfigEntry<bool> EndCountdownEnabled { get; }
    internal ConfigEntry<string> HudStyle { get; }
    internal ConfigEntry<string> HudLayoutDirection { get; }
    internal ConfigEntry<string> HudAnchor { get; }
    internal ConfigEntry<string> HudAlignment { get; }
    internal ConfigEntry<int> HudOffsetX { get; }
    internal ConfigEntry<int> HudOffsetY { get; }
    internal ConfigEntry<int> HudScalePercent { get; }
    internal ConfigEntry<int> HudBackgroundOpacityPercent { get; }

    internal (int Min, int Max) EffectiveIntervalRange => OrderedRange(IntervalMinSeconds.Value, IntervalMaxSeconds.Value, "Interval");
    internal (int Min, int Max) EffectiveDurationRange => OrderedRange(EffectDurationMinSeconds.Value, EffectDurationMaxSeconds.Value, "EffectDuration");

    internal int RollIntervalSeconds()
    {
        (int min, int max) = EffectiveIntervalRange;
        return UnityEngine.Random.Range(min, max + 1);
    }

    internal int RollDurationSeconds()
    {
        (int min, int max) = EffectiveDurationRange;
        return UnityEngine.Random.Range(min, max + 1);
    }

    internal int RollVoidSpawnCount()
    {
        (int min, int max) = OrderedRange(VoidSpawnCountMin.Value, VoidSpawnCountMax.Value, "VoidSpawnCount");
        return UnityEngine.Random.Range(min, max + 1);
    }

    internal int RollLevitationSpawnCount() =>
        RollConfiguredCount(LevitationSpawnCountMin, LevitationSpawnCountMax, "LevitationSpawnCount");

    internal int RollShockwaveSpawnCount() =>
        RollConfiguredCount(ShockwaveSpawnCountMin, ShockwaveSpawnCountMax, "ShockwaveSpawnCount");

    internal int RollStunBlastSpawnCount() =>
        RollConfiguredCount(StunBlastSpawnCountMin, StunBlastSpawnCountMax, "StunBlastSpawnCount");

    internal int RollExplosionRainSpawnCount() =>
        RollConfiguredCount(ExplosionRainSpawnCountMin, ExplosionRainSpawnCountMax, "ExplosionRainSpawnCount");

    internal int RollEnemyWaveSpawnCount() =>
        RollConfiguredCount(EnemyWaveSpawnCountMin, EnemyWaveSpawnCountMax, "EnemyWaveSpawnCount");

    internal int RollMinefieldSpawnCount() =>
        RollConfiguredCount(MinefieldSpawnCountMin, MinefieldSpawnCountMax, "MinefieldSpawnCount");

    internal int RollHealingAuraSpawnCount() =>
        RollConfiguredCount(HealingAuraSpawnCountMin, HealingAuraSpawnCountMax, "HealingAuraSpawnCount");

    internal int RollStarBarrageProjectileCount() =>
        RollConfiguredCount(StarBarrageProjectileCountMin, StarBarrageProjectileCountMax, "StarBarrageProjectileCount");

    internal int RollSpiderScarePlayerCount() =>
        RollConfiguredCount(SpiderScarePlayersPerWaveMin, SpiderScarePlayersPerWaveMax, "SpiderScarePlayersPerWave");

    internal int RollTrafficShockPlayerCount() =>
        RollConfiguredCount(TrafficShockPlayersPerPulseMin, TrafficShockPlayersPerPulseMax, "TrafficShockPlayersPerPulse");

    internal int RollDangerousValuableActivationCount() =>
        RollConfiguredCount(
            DangerousValuablesActivationCountMin,
            DangerousValuablesActivationCountMax,
            "DangerousValuablesActivationCount");

    internal StageEffect RollEffects(int? maximumOverride = null)
    {
        List<StageEffect> successful = new();
        foreach (StageEffect effect in StageEffectSet.IndividualEffects)
        {
            int chance = GetChance(effect);
            if (chance >= 100 || (chance > 0 && UnityEngine.Random.Range(1, 101) <= chance))
            {
                successful.Add(effect);
            }
        }

        Shuffle(successful);
        List<StageEffect> compatible = new();
        foreach (StageEffect candidate in successful)
        {
            if (HasLogicalConflict(candidate, compatible))
            {
                _logger.LogDebug($"Discarding {candidate} because it conflicts with an already selected effect.");
                continue;
            }
            if (!AllowDangerousCombinations.Value && HasDangerousConflict(candidate, compatible))
            {
                _logger.LogDebug($"Discarding {candidate} because dangerous combinations are disabled.");
                continue;
            }
            compatible.Add(candidate);
        }

        int maximumEffects = Mathf.Clamp(
            maximumOverride ?? MaxSimultaneousEffects.Value,
            1,
            5);
        if (compatible.Count > maximumEffects)
        {
            _logger.LogDebug(
                $"Effect roll produced {compatible.Count} compatible effects; randomly limiting the event to {maximumEffects}.");
        }

        StageEffect selected = StageEffect.None;
        int selectedCount = Mathf.Min(compatible.Count, maximumEffects);
        for (int index = 0; index < selectedCount; index++)
        {
            selected |= compatible[index];
        }
        return selected;
    }

    internal bool RollStageEnabled() =>
        StageActivationChancePercent.Value >= 100 ||
        (StageActivationChancePercent.Value > 0 && UnityEngine.Random.Range(1, 101) <= StageActivationChancePercent.Value);

    internal bool HasSelectableEffect
    {
        get
        {
            foreach (StageEffect effect in StageEffectSet.IndividualEffects)
            {
                if (GetChance(effect) > 0)
                {
                    return true;
                }
            }
            return false;
        }
    }

    internal bool IsEffectSelectable(StageEffect effect) => GetChance(effect) > 0;

    internal EventMode CaptureMode()
    {
        if (string.Equals(Mode.Value, "AllMode", StringComparison.Ordinal))
        {
            return UnityEngine.Random.Range(0, 4) switch
            {
                1 => EventMode.FixedForStage,
                2 => EventMode.FixedPerExtraction,
                3 => EventMode.PersistentForStage,
                _ => EventMode.RandomEachEvent
            };
        }
        if (string.Equals(Mode.Value, "FixedForStage", StringComparison.Ordinal))
        {
            return EventMode.FixedForStage;
        }
        if (string.Equals(Mode.Value, "FixedPerExtraction", StringComparison.Ordinal))
        {
            return EventMode.FixedPerExtraction;
        }
        if (string.Equals(Mode.Value, "PersistentForStage", StringComparison.Ordinal))
        {
            return EventMode.PersistentForStage;
        }
        return EventMode.RandomEachEvent;
    }

    internal void LogProbabilityPolicy()
    {
        if (!HasSelectableEffect)
        {
            const string fingerprint = "zero";
            if (_lastProbabilityWarning != fingerprint)
            {
                _lastProbabilityWarning = fingerprint;
                _logger.LogWarning("All enabled effect probabilities are 0%; no stage event can be selected.");
            }
        }
        else
        {
            _lastProbabilityWarning = string.Empty;
        }
    }

    internal TargetFilter CaptureTargetFilter() => new(
        TargetValuables.Value,
        TargetCosmeticBoxes.Value,
        TargetItems.Value,
        TargetDoors.Value,
        TargetWeapons.Value,
        TargetPlayers.Value,
        TargetEnemies.Value);

    private void ProbabilitySettingChanged(object sender, EventArgs eventArgs) => LogProbabilityPolicy();

    private int GetChance(StageEffect effect) => effect switch
    {
        StageEffect.Feather => FeatherEnabled.Value ? FeatherChance.Value : 0,
        StageEffect.ZeroGravity => ZeroGravityEnabled.Value ? ZeroGravityChance.Value : 0,
        StageEffect.Battery => BatteryEnabled.Value ? BatteryChance.Value : 0,
        StageEffect.Heal => HealEnabled.Value ? HealChance.Value : 0,
        StageEffect.Indestructible => IndestructibleEnabled.Value ? IndestructibleChance.Value : 0,
        StageEffect.Fragility => FragilityEnabled.Value ? FragilityChance.Value : 0,
        StageEffect.GumballHypnosis => GumballHypnosisEnabled.Value
            ? GumballHypnosisChance.Value
            : 0,
        StageEffect.HealingAura => HealingAuraEnabled.Value
            ? HealingAuraChance.Value
            : 0,
        StageEffect.StarBarrage => StarBarrageEnabled.Value ? StarBarrageChance.Value : 0,
        StageEffect.SpiderScare => SpiderScareEnabled.Value
            ? SpiderScareChance.Value
            : 0,
        StageEffect.TrafficShock => TrafficShockEnabled.Value
            ? TrafficShockChance.Value
            : 0,
        StageEffect.DangerousValuables => DangerousValuablesEnabled.Value && DangerousValuablesHasEnabledType
            ? DangerousValuablesChance.Value
            : 0,
        StageEffect.Roll => RollEnabled.Value ? RollChance.Value : 0,
        StageEffect.Void => VoidEnabled.Value ? VoidChance.Value : 0,
        StageEffect.Levitation => LevitationEnabled.Value ? LevitationChance.Value : 0,
        StageEffect.Shockwave => ShockwaveEnabled.Value ? ShockwaveChance.Value : 0,
        StageEffect.StunBlast => StunBlastEnabled.Value ? StunBlastChance.Value : 0,
        StageEffect.ExplosionRain => ExplosionRainEnabled.Value ? ExplosionRainChance.Value : 0,
        StageEffect.EnemyWave => EnemyWaveEnabled.Value ? EnemyWaveChance.Value : 0,
        StageEffect.Minefield => MinefieldEnabled.Value && MinefieldHasEnabledType ? MinefieldChance.Value : 0,
        StageEffect.Freeze => FreezeEnabled.Value ? FreezeChance.Value : 0,
        StageEffect.Stun => StunEnabled.Value ? StunChance.Value : 0,
        StageEffect.EnemyWarp => EnemyWarpEnabled.Value ? EnemyWarpChance.Value : 0,
        StageEffect.EnemyHunt => EnemyHuntEnabled.Value ? EnemyHuntChance.Value : 0,
        StageEffect.EnemySpeedUp => EnemySpeedUpEnabled.Value ? EnemySpeedUpChance.Value : 0,
        StageEffect.EnemySpeedDown => EnemySpeedDownEnabled.Value ? EnemySpeedDownChance.Value : 0,
        StageEffect.EnemyRegen => EnemyRegenEnabled.Value ? EnemyRegenChance.Value : 0,
        StageEffect.EnemyPurge => EnemyPurgeEnabled.Value ? EnemyPurgeChance.Value : 0,
        StageEffect.DamagePulse => DamagePulseEnabled.Value ? DamagePulseChance.Value : 0,
        StageEffect.SecondChance => SecondChanceEnabled.Value ? SecondChanceChance.Value : 0,
        StageEffect.Knockback => KnockbackEnabled.Value ? KnockbackChance.Value : 0,
        StageEffect.Flicker => FlickerEnabled.Value ? FlickerChance.Value : 0,
        StageEffect.Quake => QuakeEnabled.Value ? QuakeChance.Value : 0,
        StageEffect.DoorChaos => DoorChaosEnabled.Value ? DoorChaosChance.Value : 0,
        StageEffect.ValueSurge => ValueSurgeEnabled.Value ? ValueSurgeChance.Value : 0,
        StageEffect.ValueCrash => ValueCrashEnabled.Value ? ValueCrashChance.Value : 0,
        _ => GetExtendedChance(effect)
    };

    internal bool MinefieldHasEnabledType =>
        MinefieldExplosiveEnabled.Value || MinefieldShockwaveEnabled.Value || MinefieldStunEnabled.Value;

    internal bool DangerousValuablesHasEnabledType =>
        DangerousValuablesIceSawEnabled.Value ||
        DangerousValuablesBlenderEnabled.Value ||
        DangerousValuablesFlamethrowerEnabled.Value ||
        DangerousValuablesEggEnabled.Value ||
        DangerousValuablesCarEnabled.Value ||
        DangerousValuablesPlaneEnabled.Value ||
        DangerousValuablesBroomEnabled.Value;

    private int RollConfiguredCount(ConfigEntry<int> minimum, ConfigEntry<int> maximum, string name)
    {
        (int min, int max) = OrderedRange(minimum.Value, maximum.Value, name);
        return UnityEngine.Random.Range(min, max + 1);
    }

    private static void Shuffle(List<StageEffect> effects)
    {
        for (int index = 0; index < effects.Count; index++)
        {
            int selectedIndex = UnityEngine.Random.Range(index, effects.Count);
            (effects[index], effects[selectedIndex]) = (effects[selectedIndex], effects[index]);
        }
    }

    private static bool HasLogicalConflict(StageEffect candidate, List<StageEffect> selected)
    {
        foreach (StageEffect active in selected)
        {
            if (IsPair(candidate, active, StageEffect.Freeze, StageEffect.Stun) ||
                IsPair(candidate, active, StageEffect.Feather, StageEffect.HeavyCargo) ||
                IsPair(candidate, active, StageEffect.Battery, StageEffect.BatteryDrain) ||
                IsPair(candidate, active, StageEffect.EnemyArmor, StageEffect.EnemyVulnerability) ||
                IsPair(candidate, active, StageEffect.EnemyRegen, StageEffect.EnemyPurge) ||
                IsPair(candidate, active, StageEffect.EnemySpeedUp, StageEffect.EnemySpeedDown) ||
                IsPair(candidate, active, StageEffect.Indestructible, StageEffect.Fragility) ||
                IsPair(candidate, active, StageEffect.ValueSurge, StageEffect.ValueCrash))
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsPair(StageEffect first, StageEffect second, StageEffect left, StageEffect right) =>
        (first == left && second == right) || (first == right && second == left);

    private bool HasDangerousConflict(StageEffect candidate, List<StageEffect> selected)
    {
        EffectRisk candidateRisk = GetRisk(candidate);
        int valuableRiskCount = (candidateRisk & EffectRisk.ValuableRisk) != 0 ? 1 : 0;
        foreach (StageEffect active in selected)
        {
            EffectRisk activeRisk = GetRisk(active);
            if ((activeRisk & EffectRisk.ValuableRisk) != 0)
            {
                valuableRiskCount++;
            }
            if (RiskPairIsDangerous(candidateRisk, activeRisk))
            {
                return true;
            }
        }
        return valuableRiskCount >= 3;
    }

    private EffectRisk GetRisk(StageEffect effect)
    {
        EffectRisk risk = effect switch
        {
            StageEffect.ZeroGravity => EffectRisk.ForcedMovement | EffectRisk.ValuableRisk,
            StageEffect.Roll => EffectRisk.ForcedMovement | EffectRisk.ValuableRisk,
            StageEffect.Void => EffectRisk.ForcedMovement | EffectRisk.LethalHazard | EffectRisk.AreaHazard | EffectRisk.ValuableRisk,
            StageEffect.Levitation => EffectRisk.ForcedMovement | EffectRisk.ValuableRisk,
            StageEffect.Shockwave => EffectRisk.ForcedMovement | EffectRisk.AreaHazard | EffectRisk.ValuableRisk,
            StageEffect.StunBlast => EffectRisk.AreaHazard,
            StageEffect.ExplosionRain => EffectRisk.LethalHazard | EffectRisk.AreaHazard | EffectRisk.ValuableRisk,
            StageEffect.EnemyWave => EffectRisk.EnemyPressure,
            StageEffect.EnemyWarp => EffectRisk.EnemyPressure,
            StageEffect.EnemyHunt => EffectRisk.EnemyPressure,
            StageEffect.EnemySpeedUp => EffectRisk.EnemyPressure,
            StageEffect.EnemyRegen => EffectRisk.EnemyPressure,
            StageEffect.EnemyArmor => EffectRisk.EnemyPressure,
            StageEffect.HeavyCargo => EffectRisk.ControlImpairment | EffectRisk.ValuableRisk,
            StageEffect.Butterfingers => EffectRisk.ControlImpairment | EffectRisk.ValuableRisk,
            StageEffect.PlayerSwap => EffectRisk.ForcedMovement | EffectRisk.ControlImpairment,
            StageEffect.SharedPain => EffectRisk.LethalHazard,
            StageEffect.DamagePulse => EffectRisk.LethalHazard,
            StageEffect.Knockback => EffectRisk.ForcedMovement,
            StageEffect.Quake => EffectRisk.ForcedMovement | EffectRisk.ValuableRisk,
            StageEffect.DoorChaos => EffectRisk.AreaHazard | EffectRisk.ValuableRisk,
            StageEffect.Fragility => EffectRisk.ValuableRisk | EffectRisk.ValueLossAmplifier,
            StageEffect.ValueCrash => EffectRisk.ValueLossAmplifier,
            StageEffect.GumballHypnosis => EffectRisk.ControlImpairment | EffectRisk.VisualImpairment,
            StageEffect.StarBarrage => EffectRisk.LethalHazard | EffectRisk.AreaHazard | EffectRisk.ValuableRisk,
            StageEffect.SpiderScare => EffectRisk.ControlImpairment | EffectRisk.VisualImpairment,
            StageEffect.Flicker => EffectRisk.VisualImpairment,
            StageEffect.TrafficShock => EffectRisk.ForcedMovement | EffectRisk.LethalHazard,
            StageEffect.DangerousValuables => EffectRisk.ForcedMovement | EffectRisk.LethalHazard | EffectRisk.AreaHazard | EffectRisk.ValuableRisk,
            _ => EffectRisk.None
        };
        if (effect == StageEffect.Minefield)
        {
            risk = EffectRisk.AreaHazard;
            if (MinefieldExplosiveEnabled.Value)
            {
                risk |= EffectRisk.LethalHazard | EffectRisk.ValuableRisk;
            }
            if (MinefieldShockwaveEnabled.Value)
            {
                risk |= EffectRisk.ForcedMovement | EffectRisk.ValuableRisk;
            }
        }
        return risk;
    }

    private static bool RiskPairIsDangerous(EffectRisk first, EffectRisk second)
    {
        bool forcedFirst = (first & EffectRisk.ForcedMovement) != 0;
        bool forcedSecond = (second & EffectRisk.ForcedMovement) != 0;
        bool lethalFirst = (first & EffectRisk.LethalHazard) != 0;
        bool lethalSecond = (second & EffectRisk.LethalHazard) != 0;
        bool areaFirst = (first & EffectRisk.AreaHazard) != 0;
        bool areaSecond = (second & EffectRisk.AreaHazard) != 0;
        bool pressureFirst = (first & EffectRisk.EnemyPressure) != 0;
        bool pressureSecond = (second & EffectRisk.EnemyPressure) != 0;
        bool controlFirst = (first & EffectRisk.ControlImpairment) != 0;
        bool controlSecond = (second & EffectRisk.ControlImpairment) != 0;
        bool visualFirst = (first & EffectRisk.VisualImpairment) != 0;
        bool visualSecond = (second & EffectRisk.VisualImpairment) != 0;
        bool valueAmplifierFirst = (first & EffectRisk.ValueLossAmplifier) != 0;
        bool valueAmplifierSecond = (second & EffectRisk.ValueLossAmplifier) != 0;
        bool valuableFirst = (first & EffectRisk.ValuableRisk) != 0;
        bool valuableSecond = (second & EffectRisk.ValuableRisk) != 0;
        return (forcedFirst && forcedSecond) ||
               (lethalFirst && lethalSecond) ||
               (forcedFirst && (lethalSecond || areaSecond)) ||
               (forcedSecond && (lethalFirst || areaFirst)) ||
               (pressureFirst && pressureSecond) ||
               (lethalFirst && pressureSecond) ||
               (lethalSecond && pressureFirst) ||
               (controlFirst && controlSecond) ||
               (controlFirst && (forcedSecond || lethalSecond || areaSecond || pressureSecond)) ||
               (controlSecond && (forcedFirst || lethalFirst || areaFirst || pressureFirst)) ||
               (visualFirst && visualSecond) ||
               (valueAmplifierFirst && valuableSecond) ||
               (valueAmplifierSecond && valuableFirst);
    }

    [Flags]
    private enum EffectRisk
    {
        None = 0,
        ForcedMovement = 1 << 0,
        LethalHazard = 1 << 1,
        AreaHazard = 1 << 2,
        EnemyPressure = 1 << 3,
        ValuableRisk = 1 << 4,
        ControlImpairment = 1 << 5,
        ValueLossAmplifier = 1 << 6,
        VisualImpairment = 1 << 7
    }

    private (int Min, int Max) OrderedRange(int first, int second, string name)
    {
        if (first <= second)
        {
            return (first, second);
        }

        string fingerprint = $"{name}:{first}:{second}";
        if (_lastRangeWarning != fingerprint)
        {
            _lastRangeWarning = fingerprint;
            _logger.LogWarning($"{name} minimum ({first}) exceeds maximum ({second}); using {second}..{first} without rewriting config.");
        }
        return (second, first);
    }

    private static ConfigEntry<bool> BindBool(ConfigFile file, string section, string key, bool value, string description) =>
        file.Bind(section, key, value, description);

    private static ConfigEntry<int> BindInt(ConfigFile file, string section, string key, int value, int min, int max, string description) =>
        file.Bind(section, key, value, new ConfigDescription(description, new AcceptableValueRange<int>(min, max)));

    private void MigrateSpawnCountDefaults(
        ConfigEntry<int> minimum,
        ConfigEntry<int> maximum,
        int previousMinimum,
        int previousMaximum,
        int newMinimum,
        int newMaximum,
        string eventName)
    {
        if (minimum.Value != previousMinimum || maximum.Value != previousMaximum)
        {
            return;
        }
        minimum.Value = newMinimum;
        maximum.Value = newMaximum;
        _logger.LogInfo(
            $"Migrated the previous default {eventName} spawn count from " +
            $"{previousMinimum}-{previousMaximum} to {newMinimum}-{newMaximum}.");
    }

    private void MigrateMinimumPlayerDistanceDefault(
        ConfigEntry<int> setting,
        int previousDefault,
        string eventName)
    {
        if (setting.Value != previousDefault)
        {
            return;
        }
        setting.Value = DefaultMinimumPlayerDistance;
        _logger.LogInfo(
            $"Migrated the previous default {eventName} minimum player distance from " +
            $"{previousDefault}m to {DefaultMinimumPlayerDistance}m.");
    }

    private void MigrateEventChanceDefaults(
        int previousDefault,
        params ConfigEntry<int>[] settings)
    {
        int migratedCount = 0;
        foreach (ConfigEntry<int> setting in settings)
        {
            if (setting.Value != previousDefault)
            {
                continue;
            }
            setting.Value = DefaultEventChancePercent;
            migratedCount++;
        }
        if (migratedCount > 0)
        {
            _logger.LogInfo(
                $"Migrated {migratedCount} previous default event chances from " +
                $"{previousDefault}% to {DefaultEventChancePercent}%.");
        }
    }

    private int ReadConfigRevision(ConfigFile config, ConfigDefinition definition)
    {
        try
        {
            if (GetOrphanedEntries(config) is { } entries &&
                entries.TryGetValue(definition, out string serialized) &&
                int.TryParse(serialized, NumberStyles.Integer, CultureInfo.InvariantCulture, out int revision))
            {
                return revision;
            }
        }
        catch (Exception exception)
        {
            _logger.LogWarning($"Could not read the internal configuration revision: {exception.Message}");
        }
        return 0;
    }

    private void WriteConfigRevision(ConfigFile config, ConfigDefinition definition)
    {
        try
        {
            if (GetOrphanedEntries(config) is { } entries)
            {
                entries[definition] = CurrentConfigRevision.ToString(CultureInfo.InvariantCulture);
                return;
            }
        }
        catch (Exception exception)
        {
            _logger.LogWarning($"Could not write the internal configuration revision: {exception.Message}");
            return;
        }
        _logger.LogWarning("Could not access the internal configuration revision store.");
    }

    private void RemoveObsoleteConfigEntry(ConfigFile config, string section, string key)
    {
        ConfigDefinition definition = new(section, key);
        config.Remove(definition);

        try
        {
            GetOrphanedEntries(config)?.Remove(definition);
        }
        catch (Exception exception)
        {
            _logger.LogWarning($"Could not remove obsolete setting {section}.{key}: {exception.Message}");
        }
    }

    private static IDictionary<ConfigDefinition, string>? GetOrphanedEntries(ConfigFile config) =>
        OrphanedEntriesProperty?.GetValue(config) as IDictionary<ConfigDefinition, string>;

    private static bool HasOrphanedConfigEntry(
        ConfigFile config,
        ConfigDefinition definition) =>
        GetOrphanedEntries(config)?.ContainsKey(definition) == true;
}
