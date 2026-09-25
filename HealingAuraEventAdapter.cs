using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;

namespace REPOJP.StagePhysicsEvents;

internal sealed class HealingAuraEventAdapter
{
    private static readonly FieldInfo? TotalDurationField =
        AccessTools.Field(typeof(EnemyTickHealAura), "totalDuration");
    private readonly MonoBehaviour _coroutineOwner;
    private readonly VanillaSpawnEffectResolver _resolver;
    private readonly RandomStagePointSelector _pointSelector;
    private readonly List<GameObject> _auras = new();
    private bool _active;
    private int _spawnCount;
    private int _healthPool;
    private int _spawnIntervalSeconds;
    private int _minimumPlayerDistance;
    private int _maximumActiveInstances;
    private int _generation;
    private int _pendingSpawnCount;
    private float _nextSpawnAt;

    internal HealingAuraEventAdapter(
        MonoBehaviour coroutineOwner,
        VanillaSpawnEffectResolver resolver,
        RandomStagePointSelector pointSelector)
    {
        _coroutineOwner = coroutineOwner;
        _resolver = resolver;
        _pointSelector = pointSelector;
    }

    internal bool EnsureAvailable() =>
        _resolver.TryResolve(StageEffect.HealingAura, out _);

    internal void Begin(
        int spawnCount,
        int healthPool,
        int spawnIntervalSeconds,
        int minimumPlayerDistance,
        int maximumActiveInstances)
    {
        Stop();
        _spawnCount = Mathf.Clamp(spawnCount, 1, 30);
        _healthPool = Mathf.Clamp(healthPool, 1, 1000);
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
        _auras.RemoveAll(aura => aura == null);
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
        DeferredObjectCleanupQueue.Enqueue(_auras);
        _auras.Clear();
        _pendingSpawnCount = 0;
        _nextSpawnAt = 0f;
    }

    private void SpawnWave(int generation)
    {
        if (!_active || !_resolver.TryResolve(StageEffect.HealingAura, out ResolvedSpawnPrefab resolved))
        {
            return;
        }
        _auras.RemoveAll(aura => aura == null);
        int amount = Mathf.Min(
            _spawnCount,
            Mathf.Max(0, _maximumActiveInstances - _auras.Count - _pendingSpawnCount));
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
                    out Quaternion rotation))
            {
                _pendingSpawnCount = Mathf.Max(
                    0,
                    _pendingSpawnCount - (amount - index));
                break;
            }
            try
            {
                GameObject aura = SemiFunc.IsMultiplayer()
                    ? PhotonNetwork.Instantiate(resolved.ResourcePath, position, rotation, 0)
                    : UnityEngine.Object.Instantiate(resolved.Prefab, position, rotation);
                aura.name = "StagePhysicsEvents_HealingAura";
                EnemyTickHealAura? healAura = aura.GetComponent<EnemyTickHealAura>() ??
                                               aura.GetComponentInChildren<EnemyTickHealAura>(true);
                if (healAura == null)
                {
                    DestroyNetworkObject(aura);
                    continue;
                }
                healAura.healthPool = _healthPool;
                TotalDurationField?.SetValue(
                    healAura,
                    Mathf.Max(4f, _spawnIntervalSeconds + 1f));
                _auras.Add(aura);
                created++;
            }
            catch (Exception exception)
            {
                StagePhysicsEventsPlugin.ModLogger.LogWarning(
                    $"Could not create Healing Aura at {position}: {exception.Message}");
            }
            _pendingSpawnCount = Mathf.Max(0, _pendingSpawnCount - 1);
            yield return new WaitForSeconds(0.05f);
        }
        StagePhysicsEventsPlugin.ModLogger.LogDebug(
            $"Healing Aura wave created {created}/{amount} aura(s).");
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
                $"Healing Aura cleanup deferred to scene unload: {exception.Message}");
        }
    }
}
