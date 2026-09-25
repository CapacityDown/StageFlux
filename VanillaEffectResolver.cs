using System;
using System.Collections.Generic;
using UnityEngine;

namespace REPOJP.StagePhysicsEvents;

internal sealed class VanillaEffectResolver
{
    private const int MaxAttempts = 5;
    private int _featherAttempts;
    private int _zeroGravityAttempts;
    private int _rollStaffAttempts;
    private int _voidAttempts;
    private bool _featherFailed;
    private bool _zeroGravityFailed;
    private bool _rollStaffFailed;
    private bool _voidFailed;
    private readonly Dictionary<StageEffect, GameObject> _orbPrefabs = new();
    private readonly Dictionary<StageEffect, string> _orbResourcePaths = new();
    private readonly Dictionary<StageEffect, int> _orbAttempts = new();
    private readonly HashSet<StageEffect> _orbFailures = new();

    internal GameObject? FeatherPrefab { get; private set; }
    internal string? FeatherResourcePath { get; private set; }
    internal GameObject? ZeroGravityProjectilePrefab { get; private set; }
    internal GameObject? ZeroGravityAffectPrefab { get; private set; }
    internal string? ZeroGravityProjectileResourcePath { get; private set; }
    internal GameObject? RollStaffProjectilePrefab { get; private set; }
    internal GameObject? RollStaffAffectPrefab { get; private set; }
    internal string? RollStaffProjectileResourcePath { get; private set; }
    internal GameObject? VoidPrefab { get; private set; }
    internal string? VoidResourcePath { get; private set; }

    internal bool TryResolveOrb(StageEffect effect, out GameObject? prefab, out string? resourcePath)
    {
        if (_orbPrefabs.TryGetValue(effect, out GameObject cachedPrefab) &&
            _orbResourcePaths.TryGetValue(effect, out string cachedPath))
        {
            prefab = cachedPrefab;
            resourcePath = cachedPath;
            return true;
        }

        prefab = null;
        resourcePath = null;
        if (_orbFailures.Contains(effect))
        {
            return false;
        }

        Type? componentType = OrbComponentType(effect);
        string? itemName = OrbItemName(effect);
        if (componentType == null || string.IsNullOrEmpty(itemName))
        {
            return false;
        }

        Item? item = Resources.Load<Item>($"Items/{itemName}") ??
                     Resources.Load<Item>($"Items/Removed Items/{itemName}");
        if (!IsOrbItem(item, componentType))
        {
            foreach (Item candidate in Resources.FindObjectsOfTypeAll<Item>())
            {
                if (IsOrbItem(candidate, componentType))
                {
                    item = candidate;
                    break;
                }
            }
        }

        if (IsOrbItem(item, componentType))
        {
            prefab = item!.prefab.Prefab;
            resourcePath = item.prefab.ResourcePath;
            _orbPrefabs[effect] = prefab;
            _orbResourcePaths[effect] = resourcePath;
            StagePhysicsEventsPlugin.ModLogger.LogInfo($"Resolved vanilla {effect} Orb: {resourcePath}");
            return true;
        }

        int attempts = _orbAttempts.TryGetValue(effect, out int count) ? count + 1 : 1;
        _orbAttempts[effect] = attempts;
        if (attempts >= MaxAttempts)
        {
            _orbFailures.Add(effect);
            StagePhysicsEventsPlugin.ModLogger.LogWarning($"Vanilla {effect} Orb could not be resolved; the event is unavailable.");
        }
        return false;
    }

    internal bool ResolveFeather()
    {
        if (FeatherPrefab != null && !string.IsNullOrEmpty(FeatherResourcePath))
        {
            return true;
        }
        if (_featherFailed)
        {
            return false;
        }

        Item? item = Resources.Load<Item>("Items/Item Orb Feather") ??
                     Resources.Load<Item>("Items/Removed Items/Item Orb Feather");

        if (!IsFeatherItem(item))
        {
            foreach (Item candidate in Resources.FindObjectsOfTypeAll<Item>())
            {
                if (IsFeatherItem(candidate))
                {
                    item = candidate;
                    break;
                }
            }
        }

        if (IsFeatherItem(item))
        {
            FeatherPrefab = item!.prefab.Prefab;
            FeatherResourcePath = item.prefab.ResourcePath;
            StagePhysicsEventsPlugin.ModLogger.LogInfo($"Resolved vanilla Feather Orb: {FeatherResourcePath}");
            return true;
        }

        if (++_featherAttempts >= MaxAttempts)
        {
            _featherFailed = true;
            StagePhysicsEventsPlugin.ModLogger.LogWarning("Vanilla Feather Orb could not be resolved; Feather events are unavailable.");
        }
        return false;
    }

    internal bool ResolveZeroGravity()
    {
        if (ZeroGravityProjectilePrefab != null && ZeroGravityAffectPrefab != null &&
            !string.IsNullOrEmpty(ZeroGravityProjectileResourcePath))
        {
            return true;
        }
        if (_zeroGravityFailed || StatsManager.instance == null)
        {
            return false;
        }

        int lookAlikesSkipped = 0;
        foreach (Item item in StatsManager.instance.itemDictionary.Values)
        {
            GameObject? itemPrefab = item != null ? item.prefab?.Prefab : null;
            ItemStaffZeroGravity? staff = itemPrefab != null
                ? itemPrefab.GetComponentInChildren<ItemStaffZeroGravity>(true)
                : null;
            if (staff == null)
            {
                continue;
            }

            PrefabRef projectile = staff.projectilePrefab;
            if (projectile == null || projectile.Bundle != null || !projectile.IsValid() || string.IsNullOrEmpty(projectile.ResourcePath))
            {
                lookAlikesSkipped++;
                continue;
            }

            GameObject projectilePrefab = projectile.Prefab;
            SemiAreaOfEffect? area = projectilePrefab != null
                ? projectilePrefab.GetComponentInChildren<SemiAreaOfEffect>(true)
                : null;
            GameObject? affectPrefab = area?.semiAffectPrefab?.Prefab;
            if (affectPrefab == null || affectPrefab.GetComponentInChildren<SemiAffectZeroGravity>(true) == null)
            {
                continue;
            }

            ZeroGravityProjectilePrefab = projectilePrefab;
            ZeroGravityAffectPrefab = affectPrefab;
            ZeroGravityProjectileResourcePath = projectile.ResourcePath;
            StagePhysicsEventsPlugin.ModLogger.LogInfo(
                $"Resolved vanilla zero-gravity projectile: {ZeroGravityProjectileResourcePath} (skipped look-alikes: {lookAlikesSkipped}).");
            return true;
        }

        if (++_zeroGravityAttempts >= MaxAttempts)
        {
            _zeroGravityFailed = true;
            StagePhysicsEventsPlugin.ModLogger.LogWarning("Vanilla zero-gravity staff could not be resolved; Zero Gravity events are unavailable.");
        }
        return false;
    }

    internal bool ResolveRollStaff()
    {
        if (RollStaffProjectilePrefab != null && RollStaffAffectPrefab != null &&
            !string.IsNullOrEmpty(RollStaffProjectileResourcePath))
        {
            return true;
        }
        if (_rollStaffFailed || StatsManager.instance == null)
        {
            return false;
        }

        int lookAlikesSkipped = 0;
        foreach (Item item in StatsManager.instance.itemDictionary.Values)
        {
            GameObject? itemPrefab = item != null ? item.prefab?.Prefab : null;
            ItemStaffTorque? staff = itemPrefab != null
                ? itemPrefab.GetComponentInChildren<ItemStaffTorque>(true)
                : null;
            if (staff == null)
            {
                continue;
            }

            PrefabRef projectile = staff.projectilePrefab;
            if (projectile == null || projectile.Bundle != null || !projectile.IsValid() ||
                string.IsNullOrEmpty(projectile.ResourcePath))
            {
                lookAlikesSkipped++;
                continue;
            }

            GameObject projectilePrefab = projectile.Prefab;
            SemiAreaOfEffect? area = projectilePrefab != null
                ? projectilePrefab.GetComponentInChildren<SemiAreaOfEffect>(true)
                : null;
            GameObject? affectPrefab = area?.semiAffectPrefab?.Prefab;
            if (affectPrefab == null || affectPrefab.GetComponentInChildren<SemiAffectTorque>(true) == null)
            {
                continue;
            }

            RollStaffProjectilePrefab = projectilePrefab;
            RollStaffAffectPrefab = affectPrefab;
            RollStaffProjectileResourcePath = projectile.ResourcePath;
            StagePhysicsEventsPlugin.ModLogger.LogInfo(
                $"Resolved vanilla Roll Staff projectile: {RollStaffProjectileResourcePath} (skipped look-alikes: {lookAlikesSkipped}).");
            return true;
        }

        if (++_rollStaffAttempts >= MaxAttempts)
        {
            _rollStaffFailed = true;
            StagePhysicsEventsPlugin.ModLogger.LogWarning(
                "Vanilla Roll Staff could not be resolved; Roll Staff events are unavailable.");
        }
        return false;
    }

    internal bool ResolveVoid()
    {
        if (VoidPrefab != null && !string.IsNullOrEmpty(VoidResourcePath))
        {
            return true;
        }
        if (_voidFailed)
        {
            return false;
        }

        Item? item = Resources.Load<Item>("Items/Item Staff Void") ??
                     Resources.Load<Item>("Items/Removed Items/Item Staff Void");
        ItemStaffVoid? staff = ResolveVoidStaff(item);
        if (staff == null)
        {
            foreach (Item candidate in Resources.FindObjectsOfTypeAll<Item>())
            {
                staff = ResolveVoidStaff(candidate);
                if (staff != null)
                {
                    break;
                }
            }
        }

        PrefabRef? voidPrefab = staff?.prefabToInstantiateOnHit;
        if (voidPrefab != null && voidPrefab.Bundle == null && voidPrefab.IsValid() &&
            !string.IsNullOrEmpty(voidPrefab.ResourcePath))
        {
            VoidPrefab = voidPrefab.Prefab;
            VoidResourcePath = voidPrefab.ResourcePath;
            StagePhysicsEventsPlugin.ModLogger.LogInfo($"Resolved vanilla Void Staff effect: {VoidResourcePath}");
            return true;
        }

        if (++_voidAttempts >= MaxAttempts)
        {
            _voidFailed = true;
            StagePhysicsEventsPlugin.ModLogger.LogWarning("Vanilla Void Staff effect could not be resolved; the Void event is unavailable.");
        }
        return false;
    }

    private static bool IsFeatherItem(Item? item)
    {
        try
        {
            if (item == null || item.prefab == null || item.prefab.Bundle != null || !item.prefab.IsValid() || string.IsNullOrEmpty(item.prefab.ResourcePath))
            {
                return false;
            }
            GameObject prefab = item.prefab.Prefab;
            return prefab != null && prefab.GetComponentInChildren<ItemOrbFeather>(true) != null;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static bool IsOrbItem(Item? item, Type componentType)
    {
        try
        {
            if (item == null || item.prefab == null || item.prefab.Bundle != null ||
                !item.prefab.IsValid() || string.IsNullOrEmpty(item.prefab.ResourcePath))
            {
                return false;
            }
            GameObject prefab = item.prefab.Prefab;
            return prefab != null && prefab.GetComponentInChildren(componentType, true) != null;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static ItemStaffVoid? ResolveVoidStaff(Item? item)
    {
        try
        {
            if (item == null || item.prefab == null || item.prefab.Bundle != null ||
                !item.prefab.IsValid() || string.IsNullOrEmpty(item.prefab.ResourcePath))
            {
                return null;
            }
            return item.prefab.Prefab?.GetComponentInChildren<ItemStaffVoid>(true);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static Type? OrbComponentType(StageEffect effect) => effect switch
    {
        StageEffect.Battery => typeof(ItemOrbBattery),
        StageEffect.Heal => typeof(ItemOrbHeal),
        StageEffect.Indestructible => typeof(ItemOrbIndestructible),
        _ => null
    };

    private static string? OrbItemName(StageEffect effect) => effect switch
    {
        StageEffect.Battery => "Item Orb Battery",
        StageEffect.Heal => "Item Orb Heal",
        StageEffect.Indestructible => "Item Orb Indestructible",
        _ => null
    };
}
