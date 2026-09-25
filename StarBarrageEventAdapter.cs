using System;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

namespace REPOJP.StagePhysicsEvents;

internal sealed class StarBarrageEventAdapter
{
    private readonly MonoBehaviour _coroutineOwner;
    private readonly VanillaSpawnEffectResolver _resolver;
    private readonly RandomStagePointSelector _pointSelector;
    private readonly List<GameObject> _projectiles = new();
    private bool _active;
    private int _projectileCount;
    private int _spawnIntervalSeconds;
    private int _minimumPlayerDistance;
    private int _maximumActiveInstances;
    private int _generation;
    private int _pendingSpawnCount;
    private float _nextSpawnAt;

    internal StarBarrageEventAdapter(
        MonoBehaviour coroutineOwner,
        VanillaSpawnEffectResolver resolver,
        RandomStagePointSelector pointSelector)
    {
        _coroutineOwner = coroutineOwner;
        _resolver = resolver;
        _pointSelector = pointSelector;
    }

    internal bool EnsureAvailable() =>
        _resolver.TryResolve(StageEffect.StarBarrage, out _);

    internal void Begin(
        int projectileCount,
        int spawnIntervalSeconds,
        int minimumPlayerDistance,
        int maximumActiveInstances)
    {
        Stop();
        _projectileCount = Mathf.Clamp(projectileCount, 1, 30);
        _spawnIntervalSeconds = Mathf.Clamp(spawnIntervalSeconds, 1, 300);
        _minimumPlayerDistance = Mathf.Clamp(minimumPlayerDistance, 0, 100);
        _maximumActiveInstances = Mathf.Clamp(maximumActiveInstances, 1, 30);
        _active = true;
        _generation++;
        SpawnWave(_generation);
        _nextSpawnAt = Time.time + _spawnIntervalSeconds;
    }

    internal void Tick(float remainingSeconds)
    {
        if (!_active)
        {
            return;
        }
        _projectiles.RemoveAll(projectile => projectile == null);
        if (remainingSeconds <= 3f || Time.time < _nextSpawnAt)
        {
            return;
        }
        SpawnWave(_generation);
        _nextSpawnAt = Time.time + _spawnIntervalSeconds;
    }

    internal void Stop()
    {
        _active = false;
        _generation++;
        DeferredObjectCleanupQueue.Enqueue(_projectiles);
        _projectiles.Clear();
        _pendingSpawnCount = 0;
        _nextSpawnAt = 0f;
    }

    private void SpawnWave(int generation)
    {
        if (!_active || !_resolver.TryResolve(StageEffect.StarBarrage, out ResolvedSpawnPrefab resolved))
        {
            return;
        }
        _projectiles.RemoveAll(projectile => projectile == null);
        int amount = Mathf.Min(
            _projectileCount,
            Mathf.Max(0, _maximumActiveInstances - _projectiles.Count - _pendingSpawnCount));
        if (amount <= 0)
        {
            return;
        }
        _pendingSpawnCount += amount;
        _coroutineOwner.StartCoroutine(SpawnBatch(generation, amount, resolved));
    }

    private IEnumerator SpawnBatch(
        int generation,
        int amount,
        ResolvedSpawnPrefab resolved)
    {
        yield return null;
        HashSet<int> usedPoints = new();
        int created = 0;
        for (int index = 0; index < amount; index++)
        {
            if (!_active || generation != _generation)
            {
                break;
            }
            if (!_pointSelector.TryGetPosition(
                    _minimumPlayerDistance,
                    usedPoints,
                    out Vector3 position,
                    out _))
            {
                _pendingSpawnCount = Mathf.Max(
                    0,
                    _pendingSpawnCount - (amount - index));
                break;
            }
            Vector3 direction = UnityEngine.Random.onUnitSphere;
            direction.y = Mathf.Clamp(direction.y, -0.25f, 0.75f);
            direction = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.forward;
            position += Vector3.up;
            try
            {
                GameObject projectile = SemiFunc.IsMultiplayer()
                    ? PhotonNetwork.Instantiate(
                        resolved.ResourcePath,
                        position,
                        Quaternion.LookRotation(direction),
                        0)
                    : UnityEngine.Object.Instantiate(
                        resolved.Prefab,
                        position,
                        Quaternion.LookRotation(direction));
                projectile.name = "StagePhysicsEvents_StarBarrage";
                SlowProjectile? slowProjectile = projectile.GetComponent<SlowProjectile>() ??
                                                  projectile.GetComponentInChildren<SlowProjectile>(true);
                if (slowProjectile == null)
                {
                    DestroyNetworkObject(projectile);
                    continue;
                }
                slowProjectile.Launch(direction);
                _projectiles.Add(projectile);
                created++;
            }
            catch (Exception exception)
            {
                StagePhysicsEventsPlugin.ModLogger.LogWarning(
                    $"Could not create Star Barrage projectile at {position}: {exception.Message}");
            }
            _pendingSpawnCount = Mathf.Max(0, _pendingSpawnCount - 1);
            yield return new WaitForSeconds(0.05f);
        }
        StagePhysicsEventsPlugin.ModLogger.LogDebug(
            $"Star Barrage wave launched {created}/{amount} projectile(s).");
    }

    private static void DestroyNetworkObject(GameObject? instance)
    {
        if (instance == null)
        {
            return;
        }
        try
        {
            PhotonView? view = instance.GetComponent<PhotonView>() ??
                               instance.GetComponentInChildren<PhotonView>(true);
            if (SemiFunc.IsMultiplayer() && PhotonNetwork.IsMasterClient && view != null && view.ViewID != 0)
            {
                PhotonNetwork.Destroy(view.gameObject);
            }
            else
            {
                UnityEngine.Object.Destroy(instance);
            }
        }
        catch (Exception exception)
        {
            StagePhysicsEventsPlugin.ModLogger.LogDebug(
                $"Star Barrage cleanup deferred to scene unload: {exception.Message}");
        }
    }
}
