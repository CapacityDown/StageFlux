using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace REPOJP.StagePhysicsEvents;

internal readonly struct ResolvedSpawnPrefab
{
    internal ResolvedSpawnPrefab(GameObject prefab, string resourcePath)
    {
        Prefab = prefab;
        ResourcePath = resourcePath;
    }

    internal GameObject Prefab { get; }
    internal string ResourcePath { get; }
}

internal readonly struct ResolvedMinePrefab
{
    internal ResolvedMinePrefab(ResolvedSpawnPrefab spawnPrefab, ItemMine.MineType mineType)
    {
        SpawnPrefab = spawnPrefab;
        MineType = mineType;
    }

    internal ResolvedSpawnPrefab SpawnPrefab { get; }
    internal ItemMine.MineType MineType { get; }
}

internal enum DangerousValuableKind
{
    IceSaw,
    Blender,
    Flamethrower,
    Egg,
    Car,
    Plane,
    Broom
}

internal enum LevitationActivationKind
{
    None,
    ImpactLight,
    ImpactMedium,
    ImpactHeavy,
    BreakLight,
    BreakMedium,
    BreakHeavy,
    Destroy
}

internal sealed class VanillaSpawnEffectResolver
{
    private const int MaxAttempts = 5;
    private readonly Dictionary<StageEffect, ResolvedSpawnPrefab> _prefabs = new();
    private readonly Dictionary<StageEffect, int> _attempts = new();
    private readonly HashSet<StageEffect> _failures = new();
    private readonly List<ResolvedMinePrefab> _minePrefabs = new();
    private int _mineAttempts;
    private bool _mineFailed;

    internal LevitationActivationKind LevitationActivation { get; private set; }

    internal bool TryResolve(StageEffect effect, out ResolvedSpawnPrefab resolved)
    {
        if (_prefabs.TryGetValue(effect, out resolved))
        {
            return true;
        }
        if (_failures.Contains(effect))
        {
            return false;
        }

        bool success = effect switch
        {
            StageEffect.Levitation => TryResolveLevitation(out resolved),
            StageEffect.GumballHypnosis => TryResolveGumball(out resolved),
            StageEffect.HealingAura => TryResolveHealingAura(out resolved),
            StageEffect.StarBarrage => TryResolveStarProjectile(out resolved),
            StageEffect.SpiderScare => TryResolveValuableComponent<ValuableSpiderPotion>(out resolved),
            StageEffect.TrafficShock => TryResolveValuableComponent<TrafficLightValuable>(out resolved),
            StageEffect.Flicker => TryResolveValuableComponent<TrafficLightValuable>(out resolved),
            StageEffect.EnemyHunt => TryResolveValuableComponent<ScreamDollValuable>(out resolved),
            _ => TryResolveItem(effect, out resolved)
        };
        if (success)
        {
            _prefabs[effect] = resolved;
            StagePhysicsEventsPlugin.ModLogger.LogInfo(
                $"Resolved vanilla {StageEffectSet.Format(effect, string.Empty)} prefab: {resolved.ResourcePath}");
            return true;
        }

        int attempts = _attempts.TryGetValue(effect, out int count) ? count + 1 : 1;
        _attempts[effect] = attempts;
        if (attempts >= MaxAttempts)
        {
            _failures.Add(effect);
            StagePhysicsEventsPlugin.ModLogger.LogWarning(
                $"Vanilla {StageEffectSet.Format(effect, string.Empty)} prefab could not be resolved; the event is unavailable.");
        }
        resolved = default;
        return false;
    }

    internal bool TryResolveMines(StagePhysicsConfig config, out IReadOnlyList<ResolvedMinePrefab> prefabs)
    {
        if (_minePrefabs.Count > 0)
        {
            prefabs = EnabledMinePrefabs(config);
            return prefabs.Count > 0;
        }
        if (_mineFailed)
        {
            prefabs = Array.Empty<ResolvedMinePrefab>();
            return false;
        }

        HashSet<string> paths = new(StringComparer.Ordinal);
        foreach (Item item in EnumerateItems())
        {
            if (!TryGetVanillaPrefab(item, out ResolvedSpawnPrefab candidate))
            {
                continue;
            }
            ItemMine? mine = candidate.Prefab.GetComponentInChildren<ItemMine>(true);
            if (mine == null || mine.mineType == ItemMine.MineType.None || !paths.Add(candidate.ResourcePath))
            {
                continue;
            }
            _minePrefabs.Add(new ResolvedMinePrefab(candidate, mine.mineType));
        }

        if (_minePrefabs.Count == 0 && ++_mineAttempts >= MaxAttempts)
        {
            _mineFailed = true;
            StagePhysicsEventsPlugin.ModLogger.LogWarning(
                "Vanilla mine prefabs could not be resolved; Minefield is unavailable.");
        }
        else if (_minePrefabs.Count > 0)
        {
            StagePhysicsEventsPlugin.ModLogger.LogInfo(
                $"Resolved {_minePrefabs.Count} vanilla mine prefab(s): {string.Join(", ", _minePrefabs.ConvertAll(entry => entry.MineType.ToString()))}");
        }

        prefabs = EnabledMinePrefabs(config);
        return prefabs.Count > 0;
    }

    private IReadOnlyList<ResolvedMinePrefab> EnabledMinePrefabs(StagePhysicsConfig config)
    {
        List<ResolvedMinePrefab> enabled = new();
        foreach (ResolvedMinePrefab entry in _minePrefabs)
        {
            bool allowed = entry.MineType switch
            {
                ItemMine.MineType.Explosive => config.MinefieldExplosiveEnabled.Value,
                ItemMine.MineType.Shockwave => config.MinefieldShockwaveEnabled.Value,
                ItemMine.MineType.Stun => config.MinefieldStunEnabled.Value,
                _ => false
            };
            if (allowed)
            {
                enabled.Add(entry);
            }
        }
        return enabled;
    }

    private bool TryResolveItem(StageEffect effect, out ResolvedSpawnPrefab resolved)
    {
        Type? requiredType = effect switch
        {
            StageEffect.Shockwave => typeof(ItemGrenadeShockwave),
            StageEffect.StunBlast => typeof(ItemGrenadeStun),
            StageEffect.ExplosionRain => typeof(ItemGrenadeExplosive),
            _ => null
        };
        if (requiredType == null)
        {
            resolved = default;
            return false;
        }

        foreach (Item item in EnumerateItems())
        {
            if (!TryGetVanillaPrefab(item, out ResolvedSpawnPrefab candidate) ||
                candidate.Prefab.GetComponentInChildren(requiredType, true) == null ||
                candidate.Prefab.GetComponentInChildren<ItemGrenade>(true) == null ||
                candidate.Prefab.GetComponentInChildren<ItemToggle>(true) == null)
            {
                continue;
            }
            resolved = candidate;
            return true;
        }
        resolved = default;
        return false;
    }

    private bool TryResolveLevitation(out ResolvedSpawnPrefab resolved)
    {
        foreach (LevelValuables preset in Resources.FindObjectsOfTypeAll<LevelValuables>())
        {
            foreach (PrefabRef prefabRef in EnumerateValuableRefs(preset))
            {
                if (!TryGetVanillaPrefab(prefabRef, out ResolvedSpawnPrefab candidate))
                {
                    continue;
                }
                ValuableLevitationPotion? potion =
                    candidate.Prefab.GetComponentInChildren<ValuableLevitationPotion>(true);
                PhysGrabObjectImpactDetector? detector =
                    candidate.Prefab.GetComponentInChildren<PhysGrabObjectImpactDetector>(true);
                if (potion == null || detector == null)
                {
                    continue;
                }
                LevitationActivationKind activation = FindLevitationActivation(detector);
                if (activation == LevitationActivationKind.None)
                {
                    continue;
                }
                LevitationActivation = activation;
                resolved = candidate;
                return true;
            }
        }
        resolved = default;
        return false;
    }

    private static bool TryResolveGumball(out ResolvedSpawnPrefab resolved)
    {
        foreach (LevelValuables preset in Resources.FindObjectsOfTypeAll<LevelValuables>())
        {
            foreach (PrefabRef prefabRef in EnumerateValuableRefs(preset))
            {
                if (!TryGetVanillaPrefab(prefabRef, out ResolvedSpawnPrefab candidate) ||
                    candidate.Prefab.GetComponentInChildren<GumballValuable>(true) == null ||
                    candidate.Prefab.GetComponentInChildren<PhysGrabObject>(true) == null)
                {
                    continue;
                }
                resolved = candidate;
                return true;
            }
        }
        resolved = default;
        return false;
    }

    private static bool TryResolveHealingAura(out ResolvedSpawnPrefab resolved)
    {
        foreach (LevelValuables preset in Resources.FindObjectsOfTypeAll<LevelValuables>())
        {
            foreach (PrefabRef prefabRef in EnumerateValuableRefs(preset))
            {
                if (!TryGetVanillaPrefab(prefabRef, out ResolvedSpawnPrefab candidate))
                {
                    continue;
                }
                ValuableSmallPotion? potion =
                    candidate.Prefab.GetComponentInChildren<ValuableSmallPotion>(true);
                if (potion?.healAura == null || potion.healAura.GetComponent<EnemyTickHealAura>() == null)
                {
                    continue;
                }
                resolved = new ResolvedSpawnPrefab(potion.healAura, $"Enemies/{potion.healAura.name}");
                return true;
            }
        }
        resolved = default;
        return false;
    }

    private static bool TryResolveStarProjectile(out ResolvedSpawnPrefab resolved)
    {
        foreach (LevelValuables preset in Resources.FindObjectsOfTypeAll<LevelValuables>())
        {
            foreach (PrefabRef prefabRef in EnumerateValuableRefs(preset))
            {
                if (!TryGetVanillaPrefab(prefabRef, out ResolvedSpawnPrefab candidate))
                {
                    continue;
                }
                ValuableStarWand? wand =
                    candidate.Prefab.GetComponentInChildren<ValuableStarWand>(true);
                if (wand == null || !TryGetVanillaPrefab(wand.bulletPrefab, out ResolvedSpawnPrefab projectile) ||
                    projectile.Prefab.GetComponentInChildren<SlowProjectile>(true) == null)
                {
                    continue;
                }
                resolved = projectile;
                return true;
            }
        }
        resolved = default;
        return false;
    }

    private static bool TryResolveValuableComponent<T>(out ResolvedSpawnPrefab resolved)
        where T : Component
    {
        foreach (LevelValuables preset in Resources.FindObjectsOfTypeAll<LevelValuables>())
        {
            foreach (PrefabRef prefabRef in EnumerateValuableRefs(preset))
            {
                if (TryGetVanillaPrefab(prefabRef, out ResolvedSpawnPrefab candidate) &&
                    candidate.Prefab.GetComponentInChildren<T>(true) != null)
                {
                    resolved = candidate;
                    return true;
                }
            }
        }
        resolved = default;
        return false;
    }

    private static LevitationActivationKind FindLevitationActivation(PhysGrabObjectImpactDetector detector)
        => FindActivation(detector, "ActivateSphere");

    private static LevitationActivationKind FindActivation(
        PhysGrabObjectImpactDetector detector,
        string methodName)
    {
        (UnityEvent Event, LevitationActivationKind Kind)[] events =
        {
            (detector.onImpactLight, LevitationActivationKind.ImpactLight),
            (detector.onImpactMedium, LevitationActivationKind.ImpactMedium),
            (detector.onImpactHeavy, LevitationActivationKind.ImpactHeavy),
            (detector.onBreakLight, LevitationActivationKind.BreakLight),
            (detector.onBreakMedium, LevitationActivationKind.BreakMedium),
            (detector.onBreakHeavy, LevitationActivationKind.BreakHeavy),
            (detector.onDestroy, LevitationActivationKind.Destroy)
        };
        foreach ((UnityEvent unityEvent, LevitationActivationKind kind) in events)
        {
            if (unityEvent == null)
            {
                continue;
            }
            for (int index = 0; index < unityEvent.GetPersistentEventCount(); index++)
            {
                if (string.Equals(unityEvent.GetPersistentMethodName(index), methodName, StringComparison.Ordinal))
                {
                    return kind;
                }
            }
        }
        return LevitationActivationKind.None;
    }

    internal static IEnumerable<Item> EnumerateItems()
    {
        HashSet<Item> seen = new();
        if (StatsManager.instance != null)
        {
            foreach (Item item in StatsManager.instance.itemDictionary.Values)
            {
                if (item != null && seen.Add(item))
                {
                    yield return item;
                }
            }
        }
        foreach (Item item in Resources.FindObjectsOfTypeAll<Item>())
        {
            if (item != null && seen.Add(item))
            {
                yield return item;
            }
        }
    }

    private static IEnumerable<PrefabRef> EnumerateValuableRefs(LevelValuables preset)
    {
        List<PrefabRef>[] groups =
        {
            preset.tiny,
            preset.small,
            preset.medium,
            preset.big,
            preset.wide,
            preset.tall,
            preset.veryTall
        };
        foreach (List<PrefabRef> group in groups)
        {
            if (group == null)
            {
                continue;
            }
            foreach (PrefabRef prefabRef in group)
            {
                if (prefabRef != null)
                {
                    yield return prefabRef;
                }
            }
        }
    }

    internal static bool TryGetVanillaPrefab(Item item, out ResolvedSpawnPrefab resolved)
    {
        if (item == null)
        {
            resolved = default;
            return false;
        }
        return TryGetVanillaPrefab(item.prefab, out resolved);
    }

    private static bool TryGetVanillaPrefab(PrefabRef prefabRef, out ResolvedSpawnPrefab resolved)
    {
        try
        {
            if (prefabRef == null || prefabRef.Bundle != null || !prefabRef.IsValid() ||
                string.IsNullOrEmpty(prefabRef.ResourcePath) || prefabRef.Prefab == null)
            {
                resolved = default;
                return false;
            }
            resolved = new ResolvedSpawnPrefab(prefabRef.Prefab, prefabRef.ResourcePath);
            return true;
        }
        catch (Exception)
        {
            resolved = default;
            return false;
        }
    }
}
