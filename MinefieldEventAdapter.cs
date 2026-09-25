using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;

namespace REPOJP.StagePhysicsEvents;

internal sealed class MinefieldEventAdapter
{
    private static readonly FieldInfo? MineStateField = AccessTools.Field(typeof(ItemMine), "state");
    private readonly MonoBehaviour _coroutineOwner;
    private readonly VanillaSpawnEffectResolver _resolver;
    private readonly RandomStagePointSelector _pointSelector;
    private readonly StagePhysicsConfig _config;
    private readonly List<GameObject> _instances = new();
    private bool _active;
    private int _generation;
    private int _targetCount;
    private int _pendingSpawnCount;
    private float _nextReplenishAt;

    internal MinefieldEventAdapter(
        MonoBehaviour coroutineOwner,
        VanillaSpawnEffectResolver resolver,
        RandomStagePointSelector pointSelector,
        StagePhysicsConfig config)
    {
        _coroutineOwner = coroutineOwner;
        _resolver = resolver;
        _pointSelector = pointSelector;
        _config = config;
    }

    internal bool EnsureAvailable() =>
        _config.MinefieldHasEnabledType && _resolver.TryResolveMines(_config, out _);

    internal void Begin(int targetCount)
    {
        _targetCount = Mathf.Clamp(targetCount, 1, _config.MinefieldMaximumActiveMines.Value);
        if (_active)
        {
            return;
        }
        _active = true;
        _generation++;
        Replenish(_generation);
        _nextReplenishAt = Time.time + _config.MinefieldReplenishIntervalSeconds.Value;
    }

    internal void Tick(float remainingSeconds)
    {
        if (!_active)
        {
            return;
        }
        PruneInstances();
        if (!_config.MinefieldReplenishTriggeredMines.Value || remainingSeconds <= 3f ||
            Time.time < _nextReplenishAt)
        {
            return;
        }
        Replenish(_generation);
        _nextReplenishAt = Time.time + _config.MinefieldReplenishIntervalSeconds.Value;
    }

    internal void Stop()
    {
        _active = false;
        _generation++;
        foreach (GameObject instance in _instances)
        {
            if (!HasStartedDetonation(instance))
            {
                GameObject queuedMine = instance;
                DeferredObjectCleanupQueue.Enqueue(() =>
                {
                    // A mine may be triggered during the short cleanup queue delay.
                    // Preserve it if its vanilla detonation countdown has started.
                    if (!HasStartedDetonation(queuedMine))
                    {
                        DestroyInstance(queuedMine);
                    }
                });
            }
        }
        _instances.Clear();
        _pendingSpawnCount = 0;
        _nextReplenishAt = 0f;
    }

    private void Replenish(int generation)
    {
        if (!_active || !_resolver.TryResolveMines(_config, out IReadOnlyList<ResolvedMinePrefab> prefabs))
        {
            return;
        }
        int activeCount = ActiveMineCount() + _pendingSpawnCount;
        int maximum = Mathf.Clamp(_config.MinefieldMaximumActiveMines.Value, 1, 30);
        int amount = Mathf.Min(Mathf.Max(0, _targetCount - activeCount), Mathf.Max(0, maximum - activeCount));
        if (amount <= 0)
        {
            return;
        }
        _pendingSpawnCount += amount;
        _coroutineOwner.StartCoroutine(SpawnBatch(generation, amount, prefabs));
        StagePhysicsEventsPlugin.ModLogger.LogDebug(
            $"Minefield scheduled {amount} staged mine spawn(s); target={_targetCount}.");
    }

    private IEnumerator SpawnBatch(
        int generation,
        int amount,
        IReadOnlyList<ResolvedMinePrefab> prefabs)
    {
        // StartCoroutine advances synchronously to the first yield. Keep the first
        // Photon instantiate off the event-transition frame.
        yield return null;
        HashSet<int> usedPoints = new();
        int created = 0;
        for (int index = 0; index < amount; index++)
        {
            if (!_active || generation != _generation)
            {
                break;
            }
            if (!_pointSelector.TryGetNavigablePosition(
                    _config.MinefieldMinimumPlayerDistance.Value,
                    usedPoints,
                    out Vector3 position,
                    out Quaternion rotation))
            {
                _pendingSpawnCount = Mathf.Max(0, _pendingSpawnCount - (amount - index));
                break;
            }
            ResolvedMinePrefab selected = prefabs[UnityEngine.Random.Range(0, prefabs.Count)];
            try
            {
                GameObject instance = SemiFunc.IsMultiplayer()
                    ? PhotonNetwork.Instantiate(selected.SpawnPrefab.ResourcePath, position, rotation, 0)
                    : UnityEngine.Object.Instantiate(selected.SpawnPrefab.Prefab, position, rotation);
                instance.name = $"StagePhysicsEvents_Minefield_{selected.MineType}";
                instance.AddComponent<StagePhysicsEffectCarrierMarker>();
                _instances.Add(instance);
                _coroutineOwner.StartCoroutine(
                    ArmAfterStart(instance, generation, position, rotation));
                created++;
            }
            catch (Exception exception)
            {
                StagePhysicsEventsPlugin.ModLogger.LogWarning(
                    $"Could not create {selected.MineType} mine at {position}: {exception.Message}");
            }
            _pendingSpawnCount = Mathf.Max(0, _pendingSpawnCount - 1);
            yield return new WaitForSeconds(0.08f);
        }
        StagePhysicsEventsPlugin.ModLogger.LogDebug(
            $"Minefield staged spawn created {created}/{amount} mine(s); target={_targetCount}.");
    }

    private IEnumerator ArmAfterStart(
        GameObject instance,
        int generation,
        Vector3 placementPosition,
        Quaternion placementRotation)
    {
        yield return null;
        yield return null;
        if (!_active || generation != _generation || instance == null)
        {
            DestroyInstance(instance);
            yield break;
        }
        ItemMine? mine = instance.GetComponentInChildren<ItemMine>(true);
        ItemToggle? toggle = instance.GetComponentInChildren<ItemToggle>(true);
        if (mine == null || toggle == null)
        {
            StagePhysicsEventsPlugin.ModLogger.LogWarning(
                "Minefield carrier has no initialized ItemMine or ItemToggle component.");
            DestroyInstance(instance);
            yield break;
        }
        try
        {
            PhysGrabObject? physObject =
                instance.GetComponentInChildren<PhysGrabObject>(true);
            if (physObject != null)
            {
                physObject.Teleport(placementPosition, placementRotation);
                if (physObject.rb != null)
                {
                    physObject.rb.velocity = Vector3.zero;
                    physObject.rb.angularVelocity = Vector3.zero;
                }
                // Newly instantiated items may still be settling or falling. Keep the mine
                // stationary through its native arming sequence so movement cannot trigger it.
                physObject.OverrideKinematic(5f);
                physObject.OverrideGrabDisable(2f);
            }
            mine.triggeredByForces = false;
            toggle.ToggleItem(toggle: true);
            PhotonView? view = mine.GetComponent<PhotonView>() ??
                               mine.GetComponentInParent<PhotonView>();
            if (SemiFunc.IsMultiplayer() && view != null && view.ViewID != 0)
            {
                view.RPC("StateSetRPC", RpcTarget.All, (int)ItemMine.States.Armed);
            }
            else
            {
                mine.StateSetRPC((int)ItemMine.States.Armed);
            }
            StagePhysicsEventsPlugin.ModLogger.LogDebug(
                $"Placed stable armed event mine: type={mine.mineType}, " +
                $"view={instance.GetComponentInChildren<PhotonView>(true)?.ViewID ?? 0}, " +
                $"position={placementPosition}.");
        }
        catch (Exception exception)
        {
            StagePhysicsEventsPlugin.ModLogger.LogWarning($"Could not arm event mine: {exception.GetBaseException().Message}");
            DestroyInstance(instance);
        }
    }

    private int ActiveMineCount()
    {
        int count = 0;
        foreach (GameObject instance in _instances)
        {
            if (instance == null)
            {
                continue;
            }
            ItemMine? mine = instance.GetComponentInChildren<ItemMine>(true);
            if (mine == null)
            {
                continue;
            }
            try
            {
                ItemMine.States state = MineStateField?.GetValue(mine) is ItemMine.States current
                    ? current
                    : ItemMine.States.Armed;
                if (state != ItemMine.States.Triggered && state != ItemMine.States.Triggering)
                {
                    count++;
                }
            }
            catch
            {
                count++;
            }
        }
        return count;
    }

    private void PruneInstances()
    {
        _instances.RemoveAll(instance => instance == null);
    }

    private static bool HasStartedDetonation(GameObject? instance)
    {
        if (instance == null)
        {
            return false;
        }
        ItemMine? mine = instance.GetComponentInChildren<ItemMine>(true);
        if (mine == null)
        {
            return false;
        }
        try
        {
            ItemMine.States state = MineStateField?.GetValue(mine) is ItemMine.States current
                ? current
                : default;
            return state == ItemMine.States.Triggering ||
                   state == ItemMine.States.Triggered;
        }
        catch
        {
            return false;
        }
    }

    private static void DestroyInstance(GameObject? instance)
    {
        if (instance == null)
        {
            return;
        }
        try
        {
            PhotonView? view = instance.GetComponent<PhotonView>() ?? instance.GetComponentInChildren<PhotonView>(true);
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
            StagePhysicsEventsPlugin.ModLogger.LogDebug($"Mine cleanup deferred to scene unload: {exception.Message}");
        }
    }
}
