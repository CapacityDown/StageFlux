using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;

namespace REPOJP.StagePhysicsEvents;

internal sealed class TrafficShockEventAdapter
{
    private const float CarrierScale = 0.01f;
    private static readonly FieldInfo? PlayerTumbleField =
        AccessTools.Field(typeof(PlayerAvatar), "tumble");
    private readonly MonoBehaviour _coroutineOwner;
    private readonly VanillaSpawnEffectResolver _resolver;
    private readonly List<GameObject> _sources = new();
    private bool _active;
    private int _generation;
    private int _playersPerPulse;
    private int _pulseIntervalSeconds;
    private float _nextPulseAt;

    internal TrafficShockEventAdapter(
        MonoBehaviour coroutineOwner,
        VanillaSpawnEffectResolver resolver)
    {
        _coroutineOwner = coroutineOwner;
        _resolver = resolver;
    }

    internal bool EnsureAvailable() =>
        _resolver.TryResolve(StageEffect.TrafficShock, out _);

    internal void Begin(int playersPerPulse, int pulseIntervalSeconds)
    {
        Stop();
        _playersPerPulse = Mathf.Clamp(playersPerPulse, 1, 30);
        _pulseIntervalSeconds = Mathf.Clamp(pulseIntervalSeconds, 1, 300);
        _active = true;
        _generation++;
        Pulse(_generation);
        _nextPulseAt = Time.time + _pulseIntervalSeconds;
    }

    internal void Tick(float remainingSeconds)
    {
        if (!_active)
        {
            return;
        }
        _sources.RemoveAll(source => source == null);
        if (remainingSeconds <= 3f || Time.time < _nextPulseAt)
        {
            return;
        }
        Pulse(_generation);
        _nextPulseAt = Time.time + _pulseIntervalSeconds;
    }

    internal void Stop()
    {
        _active = false;
        _generation++;
        DeferredObjectCleanupQueue.Enqueue(_sources);
        _sources.Clear();
        _nextPulseAt = 0f;
    }

    private void Pulse(int generation)
    {
        if (!_active || !_resolver.TryResolve(StageEffect.TrafficShock, out ResolvedSpawnPrefab resolved))
        {
            return;
        }
        List<PlayerAvatar> players = CollectLivingPlayers();
        Shuffle(players);
        int amount = Mathf.Min(_playersPerPulse, players.Count);
        _coroutineOwner.StartCoroutine(
            SpawnBatch(generation, amount, players, resolved));
    }

    private IEnumerator SpawnBatch(
        int generation,
        int amount,
        IReadOnlyList<PlayerAvatar> players,
        ResolvedSpawnPrefab resolved)
    {
        yield return null;
        int created = 0;
        for (int index = 0; index < amount; index++)
        {
            if (!_active || generation != _generation)
            {
                break;
            }
            PlayerAvatar player = players[index];
            try
            {
                Vector3 position = PlayerEffectCarrierUtility.Position(player);
                GameObject source;
                if (SemiFunc.IsMultiplayer())
                {
                    object[] scale = { CarrierScale, CarrierScale, CarrierScale };
                    source = PhotonNetwork.Instantiate(
                        resolved.ResourcePath,
                        position,
                        Quaternion.identity,
                        0,
                        scale);
                }
                else
                {
                    source = UnityEngine.Object.Instantiate(
                        resolved.Prefab,
                        position,
                        Quaternion.identity);
                }
                source.transform.localScale = Vector3.one * CarrierScale;
                source.name = "StagePhysicsEvents_TrafficShockSource";
                source.AddComponent<StagePhysicsEffectCarrierMarker>();
                PlayerEffectCarrierUtility.DisablePhysicalInteraction(source);
                _sources.Add(source);
                _coroutineOwner.StartCoroutine(ActivateAfterStart(source, player, generation));
                created++;
            }
            catch (Exception exception)
            {
                StagePhysicsEventsPlugin.ModLogger.LogWarning(
                    $"Could not create Traffic Shock source: {exception.Message}");
            }
            yield return new WaitForSeconds(0.05f);
        }
        StagePhysicsEventsPlugin.ModLogger.LogDebug(
            $"Traffic Shock pulse targeted {created}/{amount} player(s).");
    }

    private IEnumerator ActivateAfterStart(GameObject source, PlayerAvatar player, int generation)
    {
        yield return null;
        if (!_active || generation != _generation || source == null || player == null)
        {
            DestroyNetworkObject(source);
            yield break;
        }
        TrafficLightValuable? traffic = source.GetComponentInChildren<TrafficLightValuable>(true);
        PhysGrabObject? physObject = source.GetComponentInChildren<PhysGrabObject>(true);
        if (traffic == null || physObject == null)
        {
            StagePhysicsEventsPlugin.ModLogger.LogWarning(
                "Traffic Shock source has no initialized Traffic Light components.");
            DestroyNetworkObject(source);
            yield break;
        }
        if (!StagePhysicsEffectCarrierUtility.IsInternalCarrier(physObject))
        {
            physObject.gameObject.AddComponent<StagePhysicsEffectCarrierMarker>();
        }
        try
        {
            source.transform.position = PlayerEffectCarrierUtility.Position(player);
            PlayerEffectCarrierUtility.DisablePhysicalInteraction(source);
            physObject.OverrideKinematic(3f);
            physObject.OverrideGrabDisable(3f);
            physObject.OverrideIndestructible(3f);
            SetTrafficState(traffic, TrafficLightValuable.States.LingeringOnRed);
            ShockPlayer(player);
            StagePhysicsEventsPlugin.ModLogger.LogDebug(
                $"Applied Traffic Shock to player {player.photonView?.ViewID ?? 0}.");
        }
        catch (Exception exception)
        {
            StagePhysicsEventsPlugin.ModLogger.LogWarning(
                $"Could not activate Traffic Shock: {exception.Message}");
        }
        yield return new WaitForSeconds(3f);
        DestroyNetworkObject(source);
    }

    private static void SetTrafficState(
        TrafficLightValuable traffic,
        TrafficLightValuable.States state)
    {
        PhotonView? view = traffic.GetComponent<PhotonView>() ??
                           traffic.GetComponentInParent<PhotonView>();
        if (SemiFunc.IsMultiplayer() && view != null && view.ViewID != 0)
        {
            view.RPC("SetStateRPC", RpcTarget.All, state);
        }
        else
        {
            traffic.SetStateRPC(state);
        }
    }

    private static void ShockPlayer(PlayerAvatar player)
    {
        PlayerTumble? tumble = PlayerTumbleField?.GetValue(player) as PlayerTumble;
        if (tumble == null)
        {
            throw new InvalidOperationException("The target player has no initialized tumble controller.");
        }
        Vector3 force = player.transform.localRotation * Vector3.back;
        Vector3 tumbleForce = force * 15f;
        Vector3 tumbleTorque = -player.transform.right * 45f;
        Vector3 impulse = force * 10f;
        PhotonView? tumbleView = tumble.GetComponent<PhotonView>() ??
                                 tumble.GetComponentInParent<PhotonView>();
        PhotonView? playerView = player.photonView ??
                                 player.GetComponent<PhotonView>();
        if (SemiFunc.IsMultiplayer() &&
            tumbleView != null && tumbleView.ViewID != 0 &&
            playerView != null && playerView.ViewID != 0)
        {
            RpcTarget target = RpcTarget.AllViaServer;
            tumbleView.RPC(nameof(PlayerTumble.TumbleRequestRPC), target, true, false);
            tumbleView.RPC(nameof(PlayerTumble.TumbleForceRPC), target, tumbleForce);
            tumbleView.RPC(nameof(PlayerTumble.TumbleTorqueRPC), target, tumbleTorque);
            tumbleView.RPC(nameof(PlayerTumble.TumbleOverrideTimeRPC), target, 3f);
            tumbleView.RPC(nameof(PlayerTumble.ImpactHurtSetRPC), target, 3f, 10);
            playerView.RPC(nameof(PlayerAvatar.ForceImpulseRPC), target, impulse);
            return;
        }

        tumble.TumbleRequest(_isTumbling: true, _playerInput: false);
        tumble.TumbleForce(tumbleForce);
        tumble.TumbleTorque(tumbleTorque);
        tumble.TumbleOverrideTime(3f);
        tumble.ImpactHurtSet(3f, 10);
        player.ForceImpulse(impulse);
    }

    private static List<PlayerAvatar> CollectLivingPlayers()
    {
        List<PlayerAvatar> players = new();
        if (GameDirector.instance == null)
        {
            return players;
        }
        foreach (PlayerAvatar player in GameDirector.instance.PlayerList)
        {
            if (PlayerAvatarState.IsLiving(player))
            {
                players.Add(player);
            }
        }
        return players;
    }

    private static void Shuffle(List<PlayerAvatar> players)
    {
        for (int index = 0; index < players.Count; index++)
        {
            int selected = UnityEngine.Random.Range(index, players.Count);
            (players[index], players[selected]) = (players[selected], players[index]);
        }
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
                $"Traffic Shock cleanup deferred to scene unload: {exception.Message}");
        }
    }
}
