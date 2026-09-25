using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;

namespace REPOJP.StagePhysicsEvents;

internal sealed class EnemyHuntEventAdapter
{
    private const float CarrierScale = 0.01f;
    private const float ActiveStateRefreshSeconds = 0.5f;
    private static readonly FieldInfo? AllExtractionsCompletedField =
        AccessTools.Field(typeof(RoundDirector), "allExtractionPointsCompleted");
    private readonly MonoBehaviour _coroutineOwner;
    private readonly VanillaSpawnEffectResolver _resolver;
    private bool _active;
    private int _generation;
    private int _intervalSeconds;
    private float _nextPulseAt;
    private float _nextStateRefreshAt;
    private GameObject? _source;
    private ScreamDollValuable? _screamDoll;
    private PhysGrabObject? _physObject;

    internal EnemyHuntEventAdapter(
        MonoBehaviour coroutineOwner,
        VanillaSpawnEffectResolver resolver)
    {
        _coroutineOwner = coroutineOwner;
        _resolver = resolver;
    }

    internal bool EnsureAvailable() =>
        AllExtractionsCompleted() &&
        _resolver.TryResolve(StageEffect.EnemyHunt, out _);

    internal void Begin(int intervalSeconds)
    {
        Stop();
        _intervalSeconds = Mathf.Clamp(intervalSeconds, 1, 300);
        _active = true;
        _generation++;
        _nextPulseAt = Time.time;
        _nextStateRefreshAt = 0f;
    }

    internal void Tick(float remainingSeconds)
    {
        if (!_active || remainingSeconds <= 1f)
        {
            return;
        }
        if (!AllExtractionsCompleted())
        {
            return;
        }
        if (Time.time >= _nextPulseAt)
        {
            Pulse(_generation);
            _nextPulseAt = Time.time + _intervalSeconds;
        }
        if (_source != null && _screamDoll != null &&
            Time.time >= _nextStateRefreshAt)
        {
            if (_physObject != null)
            {
                _physObject.grabbed = true;
                _physObject.OverrideKinematic(1f);
                _physObject.OverrideGrabDisable(1f);
                _physObject.OverrideIndestructible(1f);
            }
            SetScreamState(_screamDoll, ScreamDollValuable.States.Active);
            _nextStateRefreshAt = Time.time + ActiveStateRefreshSeconds;
        }
    }

    internal void Stop()
    {
        _active = false;
        _generation++;
        if (_screamDoll != null)
        {
            try
            {
                SetScreamState(_screamDoll, ScreamDollValuable.States.Idle);
            }
            catch
            {
                // Network destruction below is authoritative.
            }
        }
        DestroyNetworkObject(_source);
        _source = null;
        _screamDoll = null;
        _physObject = null;
        _nextPulseAt = 0f;
        _nextStateRefreshAt = 0f;
    }

    private void Pulse(int generation)
    {
        if (!_active ||
            !_resolver.TryResolve(StageEffect.EnemyHunt, out ResolvedSpawnPrefab resolved) ||
            !TryGetPlayerRoomPoint(out Vector3 position))
        {
            return;
        }

        if (EnemyDirector.instance != null)
        {
            EnemyDirector.instance.SetInvestigate(
                position,
                100f,
                pathfindOnly: false);
        }

        DestroyNetworkObject(_source);
        _source = null;
        _screamDoll = null;
        _physObject = null;
        try
        {
            object[] scale = { CarrierScale, CarrierScale, CarrierScale };
            _source = SemiFunc.IsMultiplayer()
                ? PhotonNetwork.Instantiate(
                    resolved.ResourcePath,
                    position,
                    Quaternion.identity,
                    0,
                    scale)
                : UnityEngine.Object.Instantiate(
                    resolved.Prefab,
                    position,
                    Quaternion.identity);
            _source.transform.localScale = Vector3.one * CarrierScale;
            _source.name = "StagePhysicsEvents_EnemyHuntSound";
            _source.AddComponent<StagePhysicsEffectCarrierMarker>();
            _coroutineOwner.StartCoroutine(ActivateAfterStart(_source, generation));
            StagePhysicsEventsPlugin.ModLogger.LogDebug(
                $"Enemy Hunt emitted a synchronized lure in a player room at {position}.");
        }
        catch (Exception exception)
        {
            StagePhysicsEventsPlugin.ModLogger.LogWarning(
                $"Could not create Enemy Hunt room sound: {exception.Message}");
            DestroyNetworkObject(_source);
            _source = null;
        }
    }

    private IEnumerator ActivateAfterStart(GameObject source, int generation)
    {
        yield return null;
        if (!_active || generation != _generation || source == null)
        {
            DestroyNetworkObject(source);
            yield break;
        }
        _screamDoll = source.GetComponentInChildren<ScreamDollValuable>(true);
        _physObject = source.GetComponentInChildren<PhysGrabObject>(true);
        if (_screamDoll == null || _physObject == null)
        {
            StagePhysicsEventsPlugin.ModLogger.LogWarning(
                "Enemy Hunt sound source has no initialized Scream Doll components.");
            DestroyNetworkObject(source);
            _source = null;
            yield break;
        }
        _physObject.grabbed = true;
        _physObject.OverrideKinematic(1f);
        _physObject.OverrideGrabDisable(1f);
        _physObject.OverrideIndestructible(1f);
        SetScreamState(_screamDoll, ScreamDollValuable.States.Active);
        _nextStateRefreshAt = Time.time + ActiveStateRefreshSeconds;
    }

    private static bool TryGetPlayerRoomPoint(out Vector3 position)
    {
        position = default;
        try
        {
            var points = SemiFunc.LevelPointsGetInPlayerRooms();
            if (points != null && points.Count > 0)
            {
                LevelPoint point = points[UnityEngine.Random.Range(0, points.Count)];
                if (point != null)
                {
                    position = point.transform.position + Vector3.up * 0.25f;
                    return true;
                }
            }
        }
        catch (Exception exception)
        {
            StagePhysicsEventsPlugin.ModLogger.LogDebug(
                $"Enemy Hunt could not query player-room points: {exception.Message}");
        }

        if (GameDirector.instance == null)
        {
            return false;
        }
        foreach (PlayerAvatar player in GameDirector.instance.PlayerList)
        {
            if (PlayerAvatarState.IsLiving(player))
            {
                position = player.transform.position;
                return true;
            }
        }
        return false;
    }

    private static bool AllExtractionsCompleted()
    {
        RoundDirector? director = RoundDirector.instance;
        if (director == null || AllExtractionsCompletedField == null)
        {
            return false;
        }
        try
        {
            return AllExtractionsCompletedField.GetValue(director) is true;
        }
        catch
        {
            return false;
        }
    }

    private static void SetScreamState(
        ScreamDollValuable screamDoll,
        ScreamDollValuable.States state)
    {
        PhotonView? view = screamDoll.GetComponent<PhotonView>() ??
                           screamDoll.GetComponentInParent<PhotonView>();
        if (SemiFunc.IsMultiplayer() && view != null && view.ViewID != 0)
        {
            view.RPC("SetStateRPC", RpcTarget.All, state);
        }
        else
        {
            screamDoll.SetStateRPC(state);
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
                $"Enemy Hunt sound cleanup deferred to scene unload: {exception.Message}");
        }
    }
}
