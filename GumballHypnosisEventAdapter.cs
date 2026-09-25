using System;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

namespace REPOJP.StagePhysicsEvents;

internal sealed class GumballHypnosisEventAdapter
{
    private const float ScanIntervalSeconds = 0.1f;
    private const float CarrierOverrideSeconds = 0.25f;
    private const float CarrierScale = 0.01f;
    private readonly MonoBehaviour _coroutineOwner;
    private readonly VanillaSpawnEffectResolver _resolver;
    private readonly Dictionary<int, Carrier> _carriers = new();
    private readonly Dictionary<int, HeldTarget> _heldTargets = new();
    private readonly List<int> _staleKeys = new();
    private bool _active;
    private int _generation;
    private int _playerCount;
    private float _nextScanAt;
    private TargetFilter _filter;

    internal GumballHypnosisEventAdapter(
        MonoBehaviour coroutineOwner,
        VanillaSpawnEffectResolver resolver)
    {
        _coroutineOwner = coroutineOwner;
        _resolver = resolver;
    }

    internal bool EnsureAvailable() =>
        _resolver.TryResolve(StageEffect.GumballHypnosis, out _);

    internal void Begin()
    {
        Stop();
        _filter = TargetFilter.HeldObjects;

        _active = true;
        _generation++;
        _playerCount = SafePlayerCount();
        _nextScanAt = 0f;
        Tick();
    }

    internal void Tick()
    {
        if (!_active)
        {
            return;
        }

        int playerCount = SafePlayerCount();
        if (playerCount != _playerCount)
        {
            // A vanilla Gumball snapshots the player list in Awake. Recreate carriers when the
            // roster changes so late joiners always exist in every active carrier's RPC list.
            ClearCarriers();
            _playerCount = playerCount;
            _nextScanAt = 0f;
        }

        FollowCarriers();
        if (Time.time < _nextScanAt)
        {
            return;
        }
        _nextScanAt = Time.time + ScanIntervalSeconds;
        CollectHeldTargets();
        SynchronizeCarriers();
    }

    internal void Stop()
    {
        _active = false;
        _generation++;
        ClearCarriers();
        _heldTargets.Clear();
        _nextScanAt = 0f;
    }

    private void CollectHeldTargets()
    {
        _heldTargets.Clear();
        foreach (PhysGrabObject target in PhysGrabObjectRegistry.Snapshot(_filter, discoverEnemies: false))
        {
            if (target == null || IsGumball(target) || target.playerGrabbing == null)
            {
                continue;
            }

            foreach (PhysGrabber grabber in target.playerGrabbing)
            {
                PlayerAvatar? player = grabber != null ? grabber.playerAvatar : null;
                if (player == null || player.photonView == null || !player.gameObject.activeInHierarchy)
                {
                    continue;
                }
                int key = PlayerKey(player);
                if (!_heldTargets.ContainsKey(key))
                {
                    _heldTargets[key] = new HeldTarget(player, target);
                }
            }
        }
    }

    private void SynchronizeCarriers()
    {
        _staleKeys.Clear();
        foreach (KeyValuePair<int, Carrier> pair in _carriers)
        {
            if (!_heldTargets.TryGetValue(pair.Key, out HeldTarget held) ||
                pair.Value.Player == null || pair.Value.Source == null)
            {
                DeactivateAndDestroy(pair.Value);
                _staleKeys.Add(pair.Key);
                continue;
            }
            pair.Value.Target = held.Target;
        }
        foreach (int key in _staleKeys)
        {
            _carriers.Remove(key);
        }

        foreach (KeyValuePair<int, HeldTarget> pair in _heldTargets)
        {
            if (_carriers.ContainsKey(pair.Key))
            {
                continue;
            }
            Carrier? carrier = CreateCarrier(pair.Value.Player, pair.Value.Target);
            if (carrier != null)
            {
                _carriers[pair.Key] = carrier;
            }
        }
    }

    private Carrier? CreateCarrier(PlayerAvatar player, PhysGrabObject target)
    {
        if (!_resolver.TryResolve(StageEffect.GumballHypnosis, out ResolvedSpawnPrefab resolved))
        {
            return null;
        }

        try
        {
            Vector3 position = TargetPosition(target);
            GameObject source;
            if (SemiFunc.IsMultiplayer())
            {
                object[] scale = { CarrierScale, CarrierScale, CarrierScale };
                source = PhotonNetwork.Instantiate(
                    resolved.ResourcePath,
                    position,
                    target.transform.rotation,
                    0,
                    scale);
            }
            else
            {
                source = UnityEngine.Object.Instantiate(
                    resolved.Prefab,
                    position,
                    target.transform.rotation);
                source.transform.localScale = Vector3.one * CarrierScale;
            }

            source.transform.localScale = Vector3.one * CarrierScale;
            source.name = "StagePhysicsEvents_GumballHypnosisCarrier";
            source.AddComponent<StagePhysicsEffectCarrierMarker>();
            Carrier carrier = new(player, target, source);
            _coroutineOwner.StartCoroutine(InitializeAfterAwake(carrier, _generation));
            return carrier;
        }
        catch (Exception exception)
        {
            StagePhysicsEventsPlugin.ModLogger.LogWarning(
                $"Could not create Gumball Hypnosis carrier: {exception.Message}");
            return null;
        }
    }

    private IEnumerator InitializeAfterAwake(Carrier carrier, int generation)
    {
        yield return null;
        if (!_active || generation != _generation || carrier.Source == null ||
            carrier.Player == null || carrier.Target == null ||
            !IsHeldBy(carrier.Target, carrier.Player))
        {
            DeactivateAndDestroy(carrier);
            yield break;
        }

        try
        {
            carrier.Gumball = carrier.Source.GetComponentInChildren<GumballValuable>(true);
            carrier.PhysObject = carrier.Source.GetComponentInChildren<PhysGrabObject>(true);
            if (carrier.Gumball == null || carrier.PhysObject == null)
            {
                throw new InvalidOperationException("The resolved prefab has no initialized Gumball components.");
            }
            if (!StagePhysicsEffectCarrierUtility.IsInternalCarrier(carrier.PhysObject))
            {
                carrier.PhysObject.gameObject.AddComponent<StagePhysicsEffectCarrierMarker>();
            }
            PrepareCarrier(carrier);
            carrier.Gumball.PlayerStateChanged(true, carrier.Player.photonView.ViewID);
            carrier.EffectApplied = true;
            StagePhysicsEventsPlugin.ModLogger.LogDebug(
                $"Gumball Hypnosis activated: playerView={carrier.Player.photonView.ViewID}, " +
                $"target={carrier.Target.name}, carrierView={carrier.Source.GetComponentInChildren<PhotonView>(true)?.ViewID ?? 0}.");
        }
        catch (Exception exception)
        {
            StagePhysicsEventsPlugin.ModLogger.LogWarning(
                $"Could not initialize Gumball Hypnosis carrier: {exception.Message}");
            DeactivateAndDestroy(carrier);
        }
    }

    private void FollowCarriers()
    {
        foreach (Carrier carrier in _carriers.Values)
        {
            if (carrier.Source == null || carrier.Target == null)
            {
                continue;
            }
            PrepareCarrier(carrier);
        }
    }

    private static void PrepareCarrier(Carrier carrier)
    {
        if (carrier.Source == null || carrier.Target == null)
        {
            return;
        }
        Vector3 position = TargetPosition(carrier.Target);
        Quaternion rotation = carrier.Target.transform.rotation;
        PhysGrabObject? physObject = carrier.PhysObject ??
            carrier.Source.GetComponentInChildren<PhysGrabObject>(true);
        carrier.PhysObject = physObject;
        if (physObject?.rb != null)
        {
            physObject.OverrideKinematic(CarrierOverrideSeconds);
            physObject.OverrideGrabDisable(CarrierOverrideSeconds);
            physObject.OverrideIndestructible(CarrierOverrideSeconds);
            physObject.rb.position = position;
            physObject.rb.rotation = rotation;
            physObject.rb.velocity = Vector3.zero;
            physObject.rb.angularVelocity = Vector3.zero;
            physObject.rb.detectCollisions = false;
        }
        carrier.Source.transform.SetPositionAndRotation(position, rotation);
    }

    private void ClearCarriers()
    {
        foreach (Carrier carrier in new List<Carrier>(_carriers.Values))
        {
            DeactivateAndDestroy(carrier);
        }
        _carriers.Clear();
    }

    private static void DeactivateAndDestroy(Carrier carrier)
    {
        if (carrier.Source == null)
        {
            return;
        }
        try
        {
            if (carrier.EffectApplied && carrier.Gumball != null &&
                carrier.Player != null && carrier.Player.photonView != null)
            {
                carrier.Gumball.PlayerStateChanged(false, carrier.Player.photonView.ViewID);
            }

            DeferredObjectCleanupQueue.Enqueue(carrier.Source);
        }
        catch (Exception exception)
        {
            StagePhysicsEventsPlugin.ModLogger.LogDebug(
                $"Gumball Hypnosis cleanup deferred to scene unload: {exception.Message}");
        }
    }

    private static bool IsHeldBy(PhysGrabObject target, PlayerAvatar player) =>
        target != null && player != null && target.playerGrabbing != null &&
        target.playerGrabbing.Contains(player.physGrabber);

    private static bool IsGumball(PhysGrabObject target) =>
        target.GetComponent<GumballValuable>() != null ||
        target.GetComponentInParent<GumballValuable>() != null ||
        target.GetComponentInChildren<GumballValuable>(true) != null;

    private static Vector3 TargetPosition(PhysGrabObject target)
    {
        if (target.rb != null)
        {
            return target.rb.worldCenterOfMass;
        }
        return target.centerPoint != Vector3.zero ? target.centerPoint : target.transform.position;
    }

    private static int PlayerKey(PlayerAvatar player) =>
        player.photonView != null && player.photonView.ViewID != 0
            ? player.photonView.ViewID
            : player.GetInstanceID();

    private static int SafePlayerCount()
    {
        try
        {
            return SemiFunc.PlayerGetList()?.Count ?? 0;
        }
        catch
        {
            return 0;
        }
    }

    private readonly struct HeldTarget
    {
        internal HeldTarget(PlayerAvatar player, PhysGrabObject target)
        {
            Player = player;
            Target = target;
        }

        internal PlayerAvatar Player { get; }
        internal PhysGrabObject Target { get; }
    }

    private sealed class Carrier
    {
        internal Carrier(PlayerAvatar player, PhysGrabObject target, GameObject source)
        {
            Player = player;
            Target = target;
            Source = source;
        }

        internal PlayerAvatar Player { get; }
        internal PhysGrabObject Target { get; set; }
        internal GameObject Source { get; }
        internal GumballValuable? Gumball { get; set; }
        internal PhysGrabObject? PhysObject { get; set; }
        internal bool EffectApplied { get; set; }
    }
}
