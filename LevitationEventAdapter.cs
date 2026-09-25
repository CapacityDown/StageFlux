using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;

namespace REPOJP.StagePhysicsEvents;

internal sealed class LevitationEventAdapter
{
    private static readonly FieldInfo? BreakLevelLightField =
        AccessTools.Field(typeof(PhysGrabObjectImpactDetector), "breakLevelLight");
    private static readonly FieldInfo? BreakLevelMediumField =
        AccessTools.Field(typeof(PhysGrabObjectImpactDetector), "breakLevelMedium");
    private static readonly FieldInfo? BreakLevelHeavyField =
        AccessTools.Field(typeof(PhysGrabObjectImpactDetector), "breakLevelHeavy");
    private readonly MonoBehaviour _coroutineOwner;
    private readonly VanillaSpawnEffectResolver _resolver;
    private readonly RandomStagePointSelector _pointSelector;
    private readonly List<GameObject> _sources = new();
    private bool _active;
    private int _generation;
    private int _spawnCount;
    private int _spawnIntervalSeconds;
    private int _minimumPlayerDistance;
    private int _maximumActiveInstances;
    private int _pendingSpawnCount;
    private float _nextSpawnAt;

    internal LevitationEventAdapter(
        MonoBehaviour coroutineOwner,
        VanillaSpawnEffectResolver resolver,
        RandomStagePointSelector pointSelector)
    {
        _coroutineOwner = coroutineOwner;
        _resolver = resolver;
        _pointSelector = pointSelector;
    }

    internal bool EnsureAvailable() => _resolver.TryResolve(StageEffect.Levitation, out _);

    internal void Begin(int spawnCount, int spawnIntervalSeconds, int minimumPlayerDistance, int maximumActiveInstances)
    {
        _spawnCount = Mathf.Clamp(spawnCount, 1, 30);
        _spawnIntervalSeconds = Mathf.Clamp(spawnIntervalSeconds, 1, 300);
        _minimumPlayerDistance = Mathf.Clamp(minimumPlayerDistance, 0, 100);
        _maximumActiveInstances = Mathf.Clamp(maximumActiveInstances, 1, 30);
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
        PruneSources();
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
        DeferredObjectCleanupQueue.Enqueue(_sources);
        _sources.Clear();
        _pendingSpawnCount = 0;
        _nextSpawnAt = 0f;
    }

    private void SpawnWave(int generation)
    {
        if (!_active || !_resolver.TryResolve(StageEffect.Levitation, out ResolvedSpawnPrefab resolved))
        {
            return;
        }
        PruneSources();
        int amount = Mathf.Min(
            _spawnCount,
            Mathf.Max(0, _maximumActiveInstances - _sources.Count - _pendingSpawnCount));
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
                GameObject source = SemiFunc.IsMultiplayer()
                    ? PhotonNetwork.Instantiate(resolved.ResourcePath, position, rotation, 0)
                    : UnityEngine.Object.Instantiate(resolved.Prefab, position, rotation);
                source.name = "StagePhysicsEvents_LevitationSource";
                source.AddComponent<StagePhysicsEffectCarrierMarker>();
                PlayerEffectCarrierUtility.DisablePhysicalInteraction(source);
                _sources.Add(source);
                _coroutineOwner.StartCoroutine(ActivateAfterStart(source, generation));
                created++;
            }
            catch (Exception exception)
            {
                StagePhysicsEventsPlugin.ModLogger.LogWarning(
                    $"Could not create Levitation source at {position}: {exception.Message}");
            }
            _pendingSpawnCount = Mathf.Max(0, _pendingSpawnCount - 1);
            yield return new WaitForSeconds(0.05f);
        }
        StagePhysicsEventsPlugin.ModLogger.LogDebug($"Levitation wave created {created}/{amount} source(s).");
    }

    private IEnumerator ActivateAfterStart(GameObject source, int generation)
    {
        yield return null;
        if (!_active || generation != _generation || source == null)
        {
            DestroySource(source);
            yield break;
        }
        PhysGrabObjectImpactDetector? detector =
            source.GetComponentInChildren<PhysGrabObjectImpactDetector>(true);
        if (detector == null)
        {
            StagePhysicsEventsPlugin.ModLogger.LogWarning("Levitation source has no impact detector.");
            DestroySource(source);
            yield break;
        }

        PhysGrabObject? physObject =
            source.GetComponentInChildren<PhysGrabObject>(true);
        if (physObject == null)
        {
            StagePhysicsEventsPlugin.ModLogger.LogWarning("Levitation source has no physics object.");
            DestroySource(source);
            yield break;
        }

        // Keep the carrier at the selected stage point while vanilla clients finish
        // instantiating it. Sending the activation through AllViaServer afterwards
        // guarantees that the local-only LevitationSphere is created on every client.
        PlayerEffectCarrierUtility.DisablePhysicalInteraction(source);
        physObject.OverrideKinematic(2f);
        physObject.OverrideGrabDisable(2f);
        if (SemiFunc.IsMultiplayer())
        {
            physObject.PhysRidingDisabledSet(true);
            yield return new WaitForSeconds(0.25f);
        }

        if (!_active || generation != _generation || source == null)
        {
            DestroySource(source);
            yield break;
        }

        Vector3 contactPoint = source.transform.position;
        try
        {
            ActivateSynchronized(detector, _resolver.LevitationActivation, contactPoint);
        }
        catch (Exception exception)
        {
            StagePhysicsEventsPlugin.ModLogger.LogWarning($"Could not activate Levitation source: {exception.Message}");
            DestroySource(source);
            yield break;
        }

        yield return new WaitForSeconds(0.75f);
        DestroySource(source);
    }

    private static void ActivateSynchronized(
        PhysGrabObjectImpactDetector detector,
        LevitationActivationKind activation,
        Vector3 contactPoint)
    {
        PhotonView? view =
            detector.GetComponent<PhotonView>() ??
            detector.GetComponentInParent<PhotonView>();
        if (SemiFunc.IsMultiplayer() && view != null && view.ViewID != 0)
        {
            switch (activation)
            {
                case LevitationActivationKind.ImpactLight:
                    view.RPC(
                        nameof(PhysGrabObjectImpactDetector.ImpactLightRPC),
                        RpcTarget.AllViaServer,
                        100f,
                        contactPoint);
                    return;
                case LevitationActivationKind.ImpactMedium:
                    view.RPC(
                        nameof(PhysGrabObjectImpactDetector.ImpactMediumRPC),
                        RpcTarget.AllViaServer,
                        100f,
                        contactPoint);
                    return;
                case LevitationActivationKind.ImpactHeavy:
                    view.RPC(
                        nameof(PhysGrabObjectImpactDetector.ImpactHeavyRPC),
                        RpcTarget.AllViaServer,
                        100f,
                        contactPoint);
                    return;
                case LevitationActivationKind.BreakLight:
                    SendBreakRpc(
                        view,
                        contactPoint,
                        ReadBreakLevel(BreakLevelLightField, detector, 1));
                    return;
                case LevitationActivationKind.BreakMedium:
                    SendBreakRpc(
                        view,
                        contactPoint,
                        ReadBreakLevel(BreakLevelMediumField, detector, 2));
                    return;
                case LevitationActivationKind.BreakHeavy:
                    SendBreakRpc(
                        view,
                        contactPoint,
                        ReadBreakLevel(BreakLevelHeavyField, detector, 3));
                    return;
                case LevitationActivationKind.Destroy:
                    view.RPC(
                        nameof(PhysGrabObjectImpactDetector.DestroyObjectRPC),
                        RpcTarget.AllViaServer,
                        false);
                    return;
                default:
                    throw new InvalidOperationException("Levitation activation event was not resolved.");
            }
        }

        switch (activation)
        {
            case LevitationActivationKind.ImpactLight:
                detector.ImpactLight(100f, contactPoint);
                break;
            case LevitationActivationKind.ImpactMedium:
                detector.ImpactMedium(100f, contactPoint);
                break;
            case LevitationActivationKind.ImpactHeavy:
                detector.ImpactHeavy(100f, contactPoint);
                break;
            case LevitationActivationKind.BreakLight:
                detector.BreakLight(contactPoint, true);
                break;
            case LevitationActivationKind.BreakMedium:
                detector.BreakMedium(contactPoint, true);
                break;
            case LevitationActivationKind.BreakHeavy:
                detector.BreakHeavy(contactPoint, true);
                break;
            case LevitationActivationKind.Destroy:
                detector.DestroyObject(false);
                break;
            default:
                throw new InvalidOperationException("Levitation activation event was not resolved.");
        }
    }

    private static void SendBreakRpc(
        PhotonView view,
        Vector3 contactPoint,
        int breakLevel)
    {
        view.RPC(
            nameof(PhysGrabObjectImpactDetector.BreakRPC),
            RpcTarget.AllViaServer,
            0f,
            contactPoint,
            breakLevel,
            true);
    }

    private static int ReadBreakLevel(
        FieldInfo? field,
        PhysGrabObjectImpactDetector detector,
        int fallback)
    {
        try
        {
            return field?.GetValue(detector) is int value ? value : fallback;
        }
        catch
        {
            return fallback;
        }
    }

    private void PruneSources()
    {
        _sources.RemoveAll(source => source == null);
    }

    private static void DestroySource(GameObject? source)
    {
        if (source == null)
        {
            return;
        }
        try
        {
            PhotonView? view = source.GetComponent<PhotonView>() ?? source.GetComponentInChildren<PhotonView>(true);
            if (SemiFunc.IsMultiplayer() && PhotonNetwork.IsMasterClient && view != null && view.ViewID != 0)
            {
                PhotonNetwork.Destroy(view.gameObject);
            }
            else
            {
                UnityEngine.Object.Destroy(source);
            }
        }
        catch (Exception exception)
        {
            StagePhysicsEventsPlugin.ModLogger.LogDebug($"Levitation source cleanup deferred to scene unload: {exception.Message}");
        }
    }
}
