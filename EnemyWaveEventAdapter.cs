using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace REPOJP.StagePhysicsEvents;

internal sealed class EnemyWaveEventAdapter
{
    private static readonly MethodInfo? SpawnMethod = AccessTools.Method(typeof(EnemyParent), "Spawn");
    private static readonly FieldInfo? SetupDoneField = AccessTools.Field(typeof(EnemyParent), "SetupDone");
    private static readonly FieldInfo? SpawnedField = AccessTools.Field(typeof(EnemyParent), "Spawned");
    private static readonly FieldInfo? EnemyField = AccessTools.Field(typeof(EnemyParent), "Enemy");
    private readonly MonoBehaviour _coroutineOwner;
    private readonly StagePhysicsConfig _config;
    private readonly Dictionary<int, EnemyParent> _leasedEnemies = new();
    private readonly HashSet<int> _pendingLeaseKeys = new();
    private readonly HashSet<int> _externallyNotifiedLeaseKeys = new();
    private bool _active;
    private int _generation;
    private int _targetCount;
    private int _pendingSpawnCount;
    private float _nextReplenishAt;

    internal EnemyWaveEventAdapter(MonoBehaviour coroutineOwner, StagePhysicsConfig config)
    {
        _coroutineOwner = coroutineOwner;
        _config = config;
    }

    internal bool EnsureAvailable() => EnemyDirector.instance != null && SpawnMethod != null;

    internal void Begin(int targetCount)
    {
        _targetCount = Mathf.Clamp(targetCount, 1, 30);
        if (_active)
        {
            return;
        }
        _active = true;
        _generation++;
        Replenish(_generation);
        _nextReplenishAt = Time.time + _config.EnemyWaveReplenishIntervalSeconds.Value;
    }

    internal void Tick(float remainingSeconds)
    {
        if (!_active)
        {
            return;
        }
        PruneLeases();
        if (remainingSeconds <= 3f || Time.time < _nextReplenishAt)
        {
            return;
        }
        Replenish(_generation);
        _nextReplenishAt = Time.time + _config.EnemyWaveReplenishIntervalSeconds.Value;
    }

    internal void Stop()
    {
        _active = false;
        _generation++;
        if (_config.EnemyWaveDespawnOnEnd.Value)
        {
            foreach (EnemyParent enemyParent in _leasedEnemies.Values)
            {
                if (enemyParent == null || !IsSpawned(enemyParent))
                {
                    continue;
                }
                EnemyParent queuedEnemy = enemyParent;
                DeferredObjectCleanupQueue.Enqueue(() =>
                {
                    if (queuedEnemy != null && IsSpawned(queuedEnemy))
                    {
                        queuedEnemy.Despawn();
                    }
                });
            }
        }
        _leasedEnemies.Clear();
        _pendingLeaseKeys.Clear();
        _externallyNotifiedLeaseKeys.Clear();
        _pendingSpawnCount = 0;
        _nextReplenishAt = 0f;
    }

    private void Replenish(int generation)
    {
        if (!_active || SpawnMethod == null)
        {
            return;
        }
        PruneLeases();
        int amount = Mathf.Max(0, _targetCount - _leasedEnemies.Count - _pendingSpawnCount);
        if (amount == 0)
        {
            return;
        }

        List<EnemyParent> candidates = CollectDormantEnemies();
        List<EnemyParent> selectedCandidates = ReserveCandidates(candidates, amount);
        if (selectedCandidates.Count == 0)
        {
            return;
        }
        _pendingSpawnCount += selectedCandidates.Count;
        _coroutineOwner.StartCoroutine(SpawnStaged(generation, selectedCandidates));
    }

    private IEnumerator SpawnStaged(int generation, List<EnemyParent> candidates)
    {
        int created = 0;
        int amount = candidates.Count;
        for (int index = 0; index < amount; index++)
        {
            if (!_active || generation != _generation)
            {
                if (generation == _generation)
                {
                    ReleasePendingCandidates(candidates, index);
                }
                break;
            }
            EnemyParent selected = candidates[index];
            int key = EnemyKey(selected);
            try
            {
                SpawnMethod!.Invoke(selected, Array.Empty<object>());
                _leasedEnemies[key] = selected;
                if (_externallyNotifiedLeaseKeys.Add(key))
                {
                    EliteEnemyVariantsCompatibility.NotifyExternalSpawn(selected);
                }
                _coroutineOwner.StartCoroutine(PlaceAfterSpawn(selected, generation));
                created++;
            }
            catch (Exception exception)
            {
                StagePhysicsEventsPlugin.ModLogger.LogWarning(
                    $"Could not activate Enemy Wave enemy {selected.enemyName}: {exception.GetBaseException().Message}");
            }
            _pendingLeaseKeys.Remove(key);
            _pendingSpawnCount = Mathf.Max(0, _pendingSpawnCount - 1);
            yield return null;
        }
        StagePhysicsEventsPlugin.ModLogger.LogDebug(
            $"Enemy Wave activated {created}/{amount} additional enemy/enemies; target={_targetCount}.");
    }

    private List<EnemyParent> ReserveCandidates(List<EnemyParent> candidates, int requestedAmount)
    {
        List<EnemyParent> selected = new();
        int amount = Mathf.Min(requestedAmount, candidates.Count);
        while (selected.Count < amount && candidates.Count > 0)
        {
            int selectedIndex = UnityEngine.Random.Range(0, candidates.Count);
            EnemyParent enemyParent = candidates[selectedIndex];
            candidates.RemoveAt(selectedIndex);
            int key = EnemyKey(enemyParent);
            if (_leasedEnemies.ContainsKey(key) || !_pendingLeaseKeys.Add(key))
            {
                continue;
            }
            selected.Add(enemyParent);
        }
        return selected;
    }

    private void ReleasePendingCandidates(List<EnemyParent> candidates, int startIndex)
    {
        for (int index = startIndex; index < candidates.Count; index++)
        {
            _pendingLeaseKeys.Remove(EnemyKey(candidates[index]));
            _pendingSpawnCount = Mathf.Max(0, _pendingSpawnCount - 1);
        }
    }

    private IEnumerator PlaceAfterSpawn(EnemyParent enemyParent, int generation)
    {
        yield return new WaitForSeconds(0.5f);
        Enemy? enemy = enemyParent != null ? GetEnemy(enemyParent) : null;
        if (!_active || generation != _generation || enemyParent == null || enemy == null || !IsSpawned(enemyParent))
        {
            yield break;
        }
        try
        {
            enemy.TeleportToPoint(
                _config.EnemyWaveMinimumPlayerDistance.Value,
                Mathf.Max(_config.EnemyWaveMinimumPlayerDistance.Value + 10, 100));
        }
        catch (Exception exception)
        {
            StagePhysicsEventsPlugin.ModLogger.LogDebug(
                $"Enemy Wave kept the vanilla spawn point for {enemyParent.enemyName}: {exception.Message}");
        }
    }

    private List<EnemyParent> CollectDormantEnemies()
    {
        List<EnemyParent> result = new();
        foreach (EnemyParent enemyParent in Resources.FindObjectsOfTypeAll<EnemyParent>())
        {
            if (enemyParent == null || !enemyParent.gameObject.scene.IsValid() || !IsSetupDone(enemyParent) ||
                IsSpawned(enemyParent) || _leasedEnemies.ContainsKey(EnemyKey(enemyParent)) ||
                _pendingLeaseKeys.Contains(EnemyKey(enemyParent)))
            {
                continue;
            }
            result.Add(enemyParent);
        }
        return result;
    }

    private void PruneLeases()
    {
        List<int> keys = new(_leasedEnemies.Keys);
        foreach (int key in keys)
        {
            if (!_leasedEnemies.TryGetValue(key, out EnemyParent enemyParent) ||
                enemyParent == null || !IsSpawned(enemyParent))
            {
                _leasedEnemies.Remove(key);
                _externallyNotifiedLeaseKeys.Remove(key);
            }
        }
    }

    private static bool IsSetupDone(EnemyParent enemyParent)
    {
        try
        {
            return SetupDoneField?.GetValue(enemyParent) is not bool setupDone || setupDone;
        }
        catch
        {
            return true;
        }
    }

    private static bool IsSpawned(EnemyParent enemyParent)
    {
        try
        {
            return SpawnedField?.GetValue(enemyParent) is bool spawned && spawned;
        }
        catch
        {
            return false;
        }
    }

    private static Enemy? GetEnemy(EnemyParent enemyParent)
    {
        try
        {
            return EnemyField?.GetValue(enemyParent) as Enemy;
        }
        catch
        {
            return null;
        }
    }

    private static int EnemyKey(EnemyParent enemyParent) =>
        enemyParent.photonView != null && enemyParent.photonView.ViewID != 0
            ? enemyParent.photonView.ViewID
            : enemyParent.GetInstanceID();
}
