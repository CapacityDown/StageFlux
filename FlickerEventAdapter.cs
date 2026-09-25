using System;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

namespace REPOJP.StagePhysicsEvents;

/// <summary>
/// Uses the vanilla Traffic Light Valuable as a short-lived networked light source.
/// Unlike PlayerAvatar.FlashlightFlicker, this remains visible before extraction is complete
/// and runs on vanilla clients because the prefab and its state RPC are part of the base game.
/// </summary>
internal sealed class FlickerEventAdapter
{
    private const float CarrierScale = 0.01f;
    private readonly MonoBehaviour _coroutineOwner;
    private readonly VanillaSpawnEffectResolver _resolver;
    private readonly List<GameObject> _sources = new();
    private bool _active;
    private int _generation;
    private int _intervalSeconds;
    private int _intensityPercent;
    private float _nextPulseAt;

    internal FlickerEventAdapter(
        MonoBehaviour coroutineOwner,
        VanillaSpawnEffectResolver resolver)
    {
        _coroutineOwner = coroutineOwner;
        _resolver = resolver;
    }

    internal bool EnsureAvailable() =>
        _resolver.TryResolve(StageEffect.Flicker, out _);

    internal void Begin(int intervalSeconds, int intensityPercent)
    {
        Stop();
        _intervalSeconds = Mathf.Clamp(intervalSeconds, 1, 300);
        _intensityPercent = Mathf.Clamp(intensityPercent, 1, 500);
        _active = true;
        _generation++;
        Pulse(_generation);
        _nextPulseAt = Time.time + _intervalSeconds;
    }

    internal void Tick(float remainingSeconds)
    {
        if (!_active)
        {
            return;
        }
        _sources.RemoveAll(source => source == null);
        if (remainingSeconds <= 1f || Time.time < _nextPulseAt)
        {
            return;
        }
        Pulse(_generation);
        _nextPulseAt = Time.time + _intervalSeconds;
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
        if (!_active || !_resolver.TryResolve(StageEffect.Flicker, out ResolvedSpawnPrefab resolved))
        {
            return;
        }
        _coroutineOwner.StartCoroutine(
            SpawnBatch(generation, CollectLivingPlayers(), resolved));
    }

    private IEnumerator SpawnBatch(
        int generation,
        IReadOnlyList<PlayerAvatar> players,
        ResolvedSpawnPrefab resolved)
    {
        yield return null;
        int created = 0;
        foreach (PlayerAvatar player in players)
        {
            if (!_active || generation != _generation)
            {
                break;
            }
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
                source.name = "StagePhysicsEvents_FlickerSource";
                source.AddComponent<StagePhysicsEffectCarrierMarker>();
                PlayerEffectCarrierUtility.DisablePhysicalInteraction(source);
                _sources.Add(source);
                _coroutineOwner.StartCoroutine(ActivateAfterStart(source, player, generation));
                created++;
            }
            catch (Exception exception)
            {
                StagePhysicsEventsPlugin.ModLogger.LogWarning(
                    $"Could not create Flicker source: {exception.Message}");
            }
            yield return new WaitForSeconds(0.05f);
        }
        StagePhysicsEventsPlugin.ModLogger.LogDebug(
            $"Flicker pulse created {created} vanilla light source(s).");
    }

    private IEnumerator ActivateAfterStart(
        GameObject source,
        PlayerAvatar player,
        int generation)
    {
        yield return null;
        if (!_active || generation != _generation || source == null || player == null)
        {
            DestroyNetworkObject(source);
            yield break;
        }

        TrafficLightValuable? traffic =
            source.GetComponentInChildren<TrafficLightValuable>(true);
        PhysGrabObject? physObject =
            source.GetComponentInChildren<PhysGrabObject>(true);
        if (traffic == null || physObject == null)
        {
            StagePhysicsEventsPlugin.ModLogger.LogWarning(
                "Flicker source has no initialized Traffic Light components.");
            DestroyNetworkObject(source);
            yield break;
        }

        try
        {
            source.transform.position = PlayerEffectCarrierUtility.Position(player);
            PlayerEffectCarrierUtility.DisablePhysicalInteraction(source);
            physObject.OverrideKinematic(2f);
            physObject.OverrideGrabDisable(2f);
            physObject.OverrideIndestructible(2f);
        }
        catch (Exception exception)
        {
            StagePhysicsEventsPlugin.ModLogger.LogWarning(
                $"Could not activate Flicker: {exception.Message}");
            DestroyNetworkObject(source);
            yield break;
        }

        // The vanilla flickering material is subtle at its lowest setting. Repeating the
        // state transition increases the visible duty cycle without changing client code.
        int transitions = Mathf.Clamp(
            Mathf.CeilToInt(_intensityPercent / 100f),
            1,
            5);
        for (int index = 0; index < transitions; index++)
        {
            if (!TrySetTrafficState(traffic, TrafficLightValuable.States.Red))
            {
                break;
            }
            yield return new WaitForSeconds(0.06f);
            if (!_active || generation != _generation || source == null)
            {
                break;
            }
            if (!TrySetTrafficState(traffic, TrafficLightValuable.States.RedFlickering))
            {
                break;
            }
            yield return new WaitForSeconds(0.12f);
            if (!_active || generation != _generation || source == null)
            {
                break;
            }
            if (!TrySetTrafficState(traffic, TrafficLightValuable.States.Off))
            {
                break;
            }
            yield return new WaitForSeconds(0.08f);
        }
        DestroyNetworkObject(source);
    }

    private static bool TrySetTrafficState(
        TrafficLightValuable traffic,
        TrafficLightValuable.States state)
    {
        try
        {
            SetTrafficState(traffic, state);
            return true;
        }
        catch (Exception exception)
        {
            StagePhysicsEventsPlugin.ModLogger.LogWarning(
                $"Could not update Flicker state: {exception.Message}");
            return false;
        }
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
            if (SemiFunc.IsMultiplayer() && PhotonNetwork.IsMasterClient &&
                view != null && view.ViewID != 0)
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
                $"Flicker cleanup deferred to scene unload: {exception.Message}");
        }
    }
}
