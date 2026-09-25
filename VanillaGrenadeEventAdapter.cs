using System;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

namespace REPOJP.StagePhysicsEvents;

internal sealed class VanillaGrenadeEventAdapter
{
    private readonly MonoBehaviour _coroutineOwner;
    private readonly VanillaSpawnEffectResolver _resolver;
    private readonly RandomStagePointSelector _pointSelector;
    private readonly StageEffect _effect;
    private readonly List<GameObject> _instances = new();
    private bool _active;
    private int _generation;
    private int _spawnCount;
    private int _spawnIntervalSeconds;
    private int _minimumPlayerDistance;
    private int _maximumActiveInstances;
    private int _launchForceMin;
    private int _launchForceMax;
    private int _pendingSpawnCount;
    private float _nextSpawnAt;

    internal VanillaGrenadeEventAdapter(
        MonoBehaviour coroutineOwner,
        VanillaSpawnEffectResolver resolver,
        RandomStagePointSelector pointSelector,
        StageEffect effect)
    {
        _coroutineOwner = coroutineOwner;
        _resolver = resolver;
        _pointSelector = pointSelector;
        _effect = effect;
    }

    internal bool EnsureAvailable() => _resolver.TryResolve(_effect, out _);

    internal void Begin(
        int spawnCount,
        int spawnIntervalSeconds,
        int minimumPlayerDistance,
        int maximumActiveInstances,
        int launchForceMin,
        int launchForceMax)
    {
        _spawnCount = Mathf.Clamp(spawnCount, 1, 30);
        _spawnIntervalSeconds = Mathf.Clamp(spawnIntervalSeconds, 1, 300);
        _minimumPlayerDistance = Mathf.Clamp(minimumPlayerDistance, 0, 100);
        _maximumActiveInstances = Mathf.Clamp(maximumActiveInstances, 1, 30);
        _launchForceMin = Mathf.Clamp(Mathf.Min(launchForceMin, launchForceMax), 0, 100);
        _launchForceMax = Mathf.Clamp(Mathf.Max(launchForceMin, launchForceMax), 0, 100);
        if (_active)
        {
            return;
        }
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
        PruneInstances();
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
        DeferredObjectCleanupQueue.Enqueue(_instances);
        _instances.Clear();
        _pendingSpawnCount = 0;
        _nextSpawnAt = 0f;
    }

    private void SpawnWave(int generation)
    {
        if (!_active || !_resolver.TryResolve(_effect, out ResolvedSpawnPrefab resolved))
        {
            return;
        }
        PruneInstances();
        int availableSlots = Mathf.Max(
            0,
            _maximumActiveInstances - _instances.Count - _pendingSpawnCount);
        int amount = Mathf.Min(_spawnCount, availableSlots);
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
            if (!_pointSelector.TryGetPosition(_minimumPlayerDistance, usedPoints, out Vector3 position, out Quaternion rotation))
            {
                _pendingSpawnCount = Mathf.Max(
                    0,
                    _pendingSpawnCount - (amount - index));
                break;
            }
            try
            {
                GameObject instance = SemiFunc.IsMultiplayer()
                    ? PhotonNetwork.Instantiate(resolved.ResourcePath, position, rotation, 0)
                    : UnityEngine.Object.Instantiate(resolved.Prefab, position, rotation);
                instance.name = $"StagePhysicsEvents_{_effect}";
                instance.AddComponent<StagePhysicsEffectCarrierMarker>();
                _instances.Add(instance);
                _coroutineOwner.StartCoroutine(ArmAfterStart(instance, generation));
                created++;
            }
            catch (Exception exception)
            {
                StagePhysicsEventsPlugin.ModLogger.LogWarning(
                    $"Could not create {_effect} grenade at {position}: {exception.Message}");
            }
            _pendingSpawnCount = Mathf.Max(0, _pendingSpawnCount - 1);
            yield return new WaitForSeconds(0.05f);
        }
        StagePhysicsEventsPlugin.ModLogger.LogDebug(
            $"{_effect} wave created {created}/{amount} grenade(s).");
    }

    private IEnumerator ArmAfterStart(GameObject instance, int generation)
    {
        yield return null;
        if (!_active || generation != _generation || instance == null)
        {
            DestroyInstance(instance);
            yield break;
        }
        ItemToggle? toggle = instance.GetComponentInChildren<ItemToggle>(true);
        ItemGrenade? grenade = instance.GetComponentInChildren<ItemGrenade>(true);
        if (toggle == null || grenade == null)
        {
            StagePhysicsEventsPlugin.ModLogger.LogWarning($"{_effect} carrier is missing its vanilla grenade components.");
            DestroyInstance(instance);
            yield break;
        }
        try
        {
            toggle.ToggleItem(true);
            Launch(instance);
            StagePhysicsEventsPlugin.ModLogger.LogDebug(
                $"Armed event grenade: effect={_effect}, view={instance.GetComponentInChildren<PhotonView>(true)?.ViewID ?? 0}.");
        }
        catch (Exception exception)
        {
            StagePhysicsEventsPlugin.ModLogger.LogWarning($"Could not arm {_effect} grenade: {exception.Message}");
            DestroyInstance(instance);
        }
    }

    private void Launch(GameObject instance)
    {
        PhysGrabObject? physObject = instance.GetComponentInChildren<PhysGrabObject>(true);
        Rigidbody? rigidbody = physObject?.rb ?? instance.GetComponentInChildren<Rigidbody>(true);
        if (rigidbody == null)
        {
            StagePhysicsEventsPlugin.ModLogger.LogWarning(
                $"{_effect} grenade is missing a Rigidbody; it was armed without launch force.");
            return;
        }

        if (rigidbody.isKinematic)
        {
            rigidbody.isKinematic = false;
        }

        Vector3 direction = UnityEngine.Random.onUnitSphere;
        direction.y = Mathf.Abs(direction.y) + 0.35f;
        direction.Normalize();
        float launchForce = UnityEngine.Random.Range(_launchForceMin, _launchForceMax + 1);
        rigidbody.AddForce(direction * launchForce, ForceMode.VelocityChange);

        StagePhysicsEventsPlugin.ModLogger.LogDebug(
            $"Launched event grenade: effect={_effect}, force={launchForce:0}, direction={direction}.");
    }

    private void PruneInstances()
    {
        _instances.RemoveAll(instance => instance == null);
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
            StagePhysicsEventsPlugin.ModLogger.LogDebug($"Grenade cleanup deferred to scene unload: {exception.Message}");
        }
    }
}
