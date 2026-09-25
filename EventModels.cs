using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace REPOJP.StagePhysicsEvents;

internal enum EventRunState
{
    Inactive = 0,
    Waiting = 1,
    Active = 2,
    Countdown = 3
}

internal enum EventMode
{
    RandomEachEvent,
    FixedForStage,
    FixedPerExtraction,
    PersistentForStage
}

[Flags]
internal enum StageEffect : long
{
    None = 0,
    Feather = 1L << 0,
    ZeroGravity = 1L << 1,
    Battery = 1L << 2,
    Heal = 1L << 3,
    Indestructible = 1L << 4,
    Roll = 1L << 5,
    Void = 1L << 6,
    Levitation = 1L << 7,
    Shockwave = 1L << 8,
    StunBlast = 1L << 9,
    ExplosionRain = 1L << 10,
    EnemyWave = 1L << 11,
    Minefield = 1L << 12,
    Freeze = 1L << 13,
    Stun = 1L << 14,
    EnemyWarp = 1L << 15,
    EnemyHunt = 1L << 16,
    EnemyRegen = 1L << 17,
    EnemyPurge = 1L << 18,
    DamagePulse = 1L << 19,
    SecondChance = 1L << 20,
    Knockback = 1L << 21,
    Flicker = 1L << 22,
    Quake = 1L << 23,
    DoorChaos = 1L << 24,
    HealingAura = 1L << 25,
    StarBarrage = 1L << 26,
    ValueSurge = 1L << 27,
    ValueCrash = 1L << 28,
    GumballHypnosis = 1L << 29,
    Fragility = 1L << 30,
    SpiderScare = 1L << 31,
    TrafficShock = 1L << 32,
    DangerousValuables = 1L << 33,
    EnemySpeedUp = 1L << 34,
    EnemySpeedDown = 1L << 35,
    Restoration = 1L << 36,
    BatteryDrain = 1L << 37,
    HeavyCargo = 1L << 38,
    Butterfingers = 1L << 39,
    EnemyBlindness = 1L << 40,
    EnemyArmor = 1L << 41,
    EnemyVulnerability = 1L << 42,
    SupplyDrop = 1L << 43,
    PlayerSwap = 1L << 44,
    SharedPain = 1L << 45
}

internal static class StageEffectSet
{
    internal const StageEffect All =
        StageEffect.Feather |
        StageEffect.ZeroGravity |
        StageEffect.Battery |
        StageEffect.Heal |
        StageEffect.Indestructible |
        StageEffect.Roll |
        StageEffect.Void |
        StageEffect.Levitation |
        StageEffect.Shockwave |
        StageEffect.StunBlast |
        StageEffect.ExplosionRain |
        StageEffect.EnemyWave |
        StageEffect.Minefield |
        StageEffect.Freeze |
        StageEffect.Stun |
        StageEffect.EnemyWarp |
        StageEffect.EnemyHunt |
        StageEffect.EnemyRegen |
        StageEffect.EnemyPurge |
        StageEffect.DamagePulse |
        StageEffect.SecondChance |
        StageEffect.Knockback |
        StageEffect.Flicker |
        StageEffect.Quake |
        StageEffect.DoorChaos |
        StageEffect.ValueSurge |
        StageEffect.ValueCrash |
        StageEffect.GumballHypnosis |
        StageEffect.Fragility |
        StageEffect.HealingAura |
        StageEffect.StarBarrage |
        StageEffect.SpiderScare |
        StageEffect.TrafficShock |
        StageEffect.DangerousValuables |
        StageEffect.EnemySpeedUp |
        StageEffect.EnemySpeedDown |
        ExtendedEventCatalog.All;

    internal static readonly IReadOnlyList<StageEffect> IndividualEffects = new[]
    {
        StageEffect.Feather,
        StageEffect.ZeroGravity,
        StageEffect.Battery,
        StageEffect.Heal,
        StageEffect.Indestructible,
        StageEffect.Fragility,
        StageEffect.Roll,
        StageEffect.Void,
        StageEffect.Levitation,
        StageEffect.Shockwave,
        StageEffect.StunBlast,
        StageEffect.ExplosionRain,
        StageEffect.EnemyWave,
        StageEffect.Minefield,
        StageEffect.Freeze,
        StageEffect.Stun,
        StageEffect.EnemyWarp,
        StageEffect.EnemyHunt,
        StageEffect.EnemyRegen,
        StageEffect.EnemyPurge,
        StageEffect.DamagePulse,
        StageEffect.SecondChance,
        StageEffect.Knockback,
        StageEffect.Flicker,
        StageEffect.Quake,
        StageEffect.DoorChaos,
        StageEffect.ValueSurge,
        StageEffect.ValueCrash,
        StageEffect.GumballHypnosis,
        StageEffect.HealingAura,
        StageEffect.StarBarrage,
        StageEffect.SpiderScare,
        StageEffect.TrafficShock,
        StageEffect.DangerousValuables,
        StageEffect.EnemySpeedUp,
        StageEffect.EnemySpeedDown,
        StageEffect.Restoration,
        StageEffect.BatteryDrain,
        StageEffect.HeavyCargo,
        StageEffect.Butterfingers,
        StageEffect.EnemyBlindness,
        StageEffect.EnemyArmor,
        StageEffect.EnemyVulnerability,
        StageEffect.SupplyDrop,
        StageEffect.PlayerSwap,
        StageEffect.SharedPain
    };

    internal static bool Contains(StageEffect effects, StageEffect effect) =>
        effect != StageEffect.None && (effects & effect) == effect;

    internal static bool IsValid(StageEffect effects) =>
        (effects & ~All) == StageEffect.None;

    internal static string Format(StageEffect effects, string separator)
    {
        StringBuilder text = new();
        foreach (StageEffect effect in IndividualEffects)
        {
            if (!Contains(effects, effect))
            {
                continue;
            }
            if (text.Length > 0)
            {
                text.Append(separator);
            }
            text.Append(effect switch
            {
                StageEffect.Battery => "Battery Charge",
                StageEffect.ZeroGravity => "Zero Gravity",
                StageEffect.StunBlast => "Stun Blast",
                StageEffect.ExplosionRain => "Explosion Rain",
                StageEffect.EnemyWave => "Enemy Wave",
                StageEffect.EnemyWarp => "Enemy Warp",
                StageEffect.EnemyHunt => "Enemy Hunt",
                StageEffect.EnemyRegen => "Enemy Regen",
                StageEffect.EnemyPurge => "Enemy Purge",
                StageEffect.DamagePulse => "Damage Pulse",
                StageEffect.SecondChance => "Second Chance",
                StageEffect.DoorChaos => "Door Chaos",
                StageEffect.ValueSurge => "Value Surge",
                StageEffect.ValueCrash => "Value Crash",
                StageEffect.GumballHypnosis => "Gumball Hypnosis",
                StageEffect.HealingAura => "Healing Aura",
                StageEffect.StarBarrage => "Star Barrage",
                StageEffect.SpiderScare => "Spider Scare",
                StageEffect.TrafficShock => "Traffic Shock",
                StageEffect.DangerousValuables => "Dangerous Valuables",
                StageEffect.EnemySpeedUp => "Enemy Speed Up",
                StageEffect.EnemySpeedDown => "Enemy Speed Down",
                StageEffect.BatteryDrain => "Battery Drain",
                StageEffect.HeavyCargo => "Heavy Cargo",
                StageEffect.EnemyBlindness => "Enemy Blindness",
                StageEffect.EnemyArmor => "Enemy Armor",
                StageEffect.EnemyVulnerability => "Enemy Vulnerability",
                StageEffect.SupplyDrop => "Supply Drop",
                StageEffect.PlayerSwap => "Player Swap",
                StageEffect.SharedPain => "Shared Pain",
                StageEffect.Roll => "Roll",
                _ => effect.ToString()
            });
        }
        return text.Length > 0 ? text.ToString() : "None";
    }

    internal static string FormatForChat(StageEffect effects, string separator) =>
        Format(effects, separator).Replace(" ", string.Empty);
}

internal readonly struct HudState
{
    internal HudState(
        EventRunState state,
        StageEffect effect,
        StageEffect previewEffect,
        EventMode mode,
        int durationSeconds,
        int intervalSeconds,
        int remainingSeconds,
        int phaseDurationSeconds,
        int slotLimit)
    {
        State = state;
        Effect = effect;
        PreviewEffect = previewEffect;
        Mode = mode;
        DurationSeconds = durationSeconds;
        IntervalSeconds = intervalSeconds;
        RemainingSeconds = remainingSeconds;
        PhaseDurationSeconds = phaseDurationSeconds;
        SlotLimit = slotLimit;
    }

    internal EventRunState State { get; }
    internal StageEffect Effect { get; }
    internal StageEffect PreviewEffect { get; }
    internal EventMode Mode { get; }
    internal int DurationSeconds { get; }
    internal int IntervalSeconds { get; }
    internal int RemainingSeconds { get; }
    internal int PhaseDurationSeconds { get; }
    internal int SlotLimit { get; }
}

internal readonly struct TargetFilter
{
    internal static TargetFilter All => new(
        valuables: true,
        cosmeticBoxes: true,
        items: true,
        doors: true,
        weapons: true,
        players: true,
        enemies: true);

    internal static TargetFilter HeldObjects => new(
        valuables: true,
        cosmeticBoxes: true,
        items: true,
        doors: false,
        weapons: true,
        players: false,
        enemies: false);

    internal TargetFilter(bool valuables, bool cosmeticBoxes, bool items, bool doors, bool weapons, bool players, bool enemies)
    {
        Valuables = valuables;
        CosmeticBoxes = cosmeticBoxes;
        Items = items;
        Doors = doors;
        Weapons = weapons;
        Players = players;
        Enemies = enemies;
    }

    internal bool Valuables { get; }
    internal bool CosmeticBoxes { get; }
    internal bool Items { get; }
    internal bool Doors { get; }
    internal bool Weapons { get; }
    internal bool Players { get; }
    internal bool Enemies { get; }

    internal TargetFilter WithDoors(bool doors) => new(
        Valuables,
        CosmeticBoxes,
        Items,
        doors,
        Weapons,
        Players,
        Enemies);

    internal bool Allows(PhysGrabObject physObject)
    {
        if (physObject.GetComponentInParent<PlayerAvatar>() != null)
        {
            return Players;
        }
        if (physObject.GetComponent<EnemyRigidbody>() != null ||
            physObject.GetComponentInParent<EnemyRigidbody>() != null ||
            physObject.GetComponentInParent<EnemyParent>() != null)
        {
            return Enemies;
        }
        if (physObject.GetComponent<CosmeticWorldObject>() != null)
        {
            return CosmeticBoxes;
        }
        if (physObject.GetComponent<PhysGrabHinge>() != null ||
            physObject.GetComponentInParent<PhysGrabHinge>() != null ||
            physObject.GetComponentInChildren<PhysGrabHinge>(true) != null)
        {
            return Doors;
        }
        if (physObject.GetComponent<ValuableObject>() != null)
        {
            return Valuables;
        }
        ItemAttributes? attributes = physObject.GetComponent<ItemAttributes>();
        if (attributes != null && attributes.item != null && IsWeapon(attributes.item.itemType))
        {
            return Weapons;
        }
        return Items;
    }

    private static bool IsWeapon(SemiFunc.itemType type) =>
        type == SemiFunc.itemType.gun ||
        type == SemiFunc.itemType.melee ||
        type == SemiFunc.itemType.launcher ||
        type == SemiFunc.itemType.grenade ||
        type == SemiFunc.itemType.mine;
}

internal sealed class StagePhysicsEffectCarrierMarker : MonoBehaviour
{
}
