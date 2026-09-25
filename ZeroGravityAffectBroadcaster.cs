using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;

namespace REPOJP.StagePhysicsEvents;

internal enum StaffAffectKind
{
    ZeroGravity,
    Roll
}

internal sealed class StaffAffectBroadcaster
{
    private const int BatchSize = 96;
    private static readonly FieldInfo? SingleplayerObjectsField =
        AccessTools.Field(typeof(SemiAreaOfEffect), "targetPhysObjectsForSingleplayer");
    private static readonly FieldInfo? SingleplayerPlayersField =
        AccessTools.Field(typeof(SemiAreaOfEffect), "playerAvatars");
    private static readonly MethodInfo? CreateAffectsMethod =
        AccessTools.Method(typeof(SemiAreaOfEffect), "CreateAffectsRPC");
    private readonly MonoBehaviour _coroutineOwner;
    private readonly VanillaEffectResolver _resolver;
    private readonly StaffAffectKind _kind;

    internal StaffAffectBroadcaster(
        MonoBehaviour coroutineOwner,
        VanillaEffectResolver resolver,
        StaffAffectKind kind)
    {
        _coroutineOwner = coroutineOwner;
        _resolver = resolver;
        _kind = kind;
    }

    internal bool EnsureAvailable() => _kind == StaffAffectKind.ZeroGravity
        ? _resolver.ResolveZeroGravity()
        : _resolver.ResolveRollStaff();

    private GameObject? ProjectilePrefab => _kind == StaffAffectKind.ZeroGravity
        ? _resolver.ZeroGravityProjectilePrefab
        : _resolver.RollStaffProjectilePrefab;

    private GameObject? AffectPrefab => _kind == StaffAffectKind.ZeroGravity
        ? _resolver.ZeroGravityAffectPrefab
        : _resolver.RollStaffAffectPrefab;

    private string? ProjectileResourcePath => _kind == StaffAffectKind.ZeroGravity
        ? _resolver.ZeroGravityProjectileResourcePath
        : _resolver.RollStaffProjectileResourcePath;

    private string EffectLabel => _kind == StaffAffectKind.ZeroGravity ? "Zero Gravity" : "Roll";

    internal void ApplyOnce(IReadOnlyList<PhysGrabObject> objects, IReadOnlyList<PlayerAvatar> players, float durationSeconds)
    {
        if (!EnsureAvailable() || durationSeconds <= 0)
        {
            return;
        }

        // Zero Gravity doubles every target's timer. Roll doubles only player timers.
        float objectAffectTime = Mathf.Max(
            0.05f,
            durationSeconds * (_kind == StaffAffectKind.ZeroGravity ? 0.5f : 1f));
        float playerAffectTime = Mathf.Max(0.05f, durationSeconds * 0.5f);
        Vector3 direction = EffectDirection();

        BuildSingleplayerTargets(
            objects,
            players,
            out List<PhysGrabObject> targetObjects,
            out List<PlayerAvatar> targetPlayers);

        if (!SemiFunc.IsMultiplayer())
        {
            if (_kind == StaffAffectKind.Roll)
            {
                foreach (PhysGrabObject targetObject in targetObjects)
                {
                    ApplySingleplayer(
                        new[] { targetObject },
                        Array.Empty<PlayerAvatar>(),
                        objectAffectTime,
                        RandomHorizontalDirection());
                }
                ApplySingleplayer(Array.Empty<PhysGrabObject>(), targetPlayers, playerAffectTime, direction);
            }
            else
            {
                ApplySingleplayer(targetObjects, targetPlayers, objectAffectTime, direction);
            }
            return;
        }

        HashSet<int> objectViewIds = new();
        HashSet<int> playerViewIds = new();
        foreach (PlayerAvatar player in targetPlayers)
        {
            if (player != null && player.photonView != null && player.photonView.ViewID != 0)
            {
                playerViewIds.Add(player.photonView.ViewID);
            }
        }

        foreach (PhysGrabObject physObject in targetObjects)
        {
            PhotonView? view = ResolveTargetView(physObject);
            if (view != null && view.ViewID != 0)
            {
                objectViewIds.Add(view.ViewID);
            }
        }

        if (_kind == StaffAffectKind.Roll)
        {
            FireViewIds(objectViewIds, objectAffectTime, direction, individualRandomDirections: true);
            FireViewIds(playerViewIds, playerAffectTime, direction);
            return;
        }

        objectViewIds.UnionWith(playerViewIds);
        FireViewIds(objectViewIds, objectAffectTime, direction);
    }

    private void FireViewIds(
        HashSet<int> viewIds,
        float affectTime,
        Vector3 direction,
        bool individualRandomDirections = false)
    {
        List<int> batch = new(BatchSize);
        foreach (int viewId in viewIds)
        {
            batch.Add(viewId);
            if (batch.Count == BatchSize)
            {
                FireBatch(batch, affectTime, direction, individualRandomDirections);
                batch.Clear();
            }
        }
        if (batch.Count > 0)
        {
            FireBatch(batch, affectTime, direction, individualRandomDirections);
        }
    }

    internal void Stop()
    {
        // Standard SemiAffect instances expire naturally. Do not overwrite or forcibly restore game physics.
    }

    private void FireBatch(
        List<int> viewIds,
        float affectTime,
        Vector3 direction,
        bool individualRandomDirections)
    {
        try
        {
            Vector3 hiddenPosition = new(0f, -1000f, 0f);
            GameObject carrier = PhotonNetwork.Instantiate(
                ProjectileResourcePath!,
                hiddenPosition,
                Quaternion.identity,
                0);
            carrier.AddComponent<StagePhysicsEffectCarrierMarker>();
            SlowProjectile? projectile = carrier.GetComponent<SlowProjectile>();
            if (projectile != null)
            {
                projectile.enabled = false;
            }

            SemiAreaOfEffect? area = carrier.GetComponentInChildren<SemiAreaOfEffect>(true);
            PhotonView? areaView = area != null
                ? area.GetComponent<PhotonView>() ??
                  area.GetComponentInParent<PhotonView>() ??
                  area.GetComponentInChildren<PhotonView>(true)
                : null;
            if (area == null || areaView == null)
            {
                PhotonNetwork.Destroy(carrier);
                StagePhysicsEventsPlugin.ModLogger.LogWarning(
                    $"{EffectLabel} carrier has no accessible SemiAreaOfEffect PhotonView; RPC batch was skipped.");
                return;
            }

            if (individualRandomDirections)
            {
                foreach (int viewId in viewIds)
                {
                    areaView.RPC(
                        "CreateAffectsRPC",
                        RpcTarget.All,
                        new[] { viewId },
                        new[] { affectTime },
                        hiddenPosition,
                        RandomHorizontalDirection());
                }
                StagePhysicsEventsPlugin.ModLogger.LogDebug(
                    $"{EffectLabel} RPC batch sent: targets={viewIds.Count}, effectTimer={affectTime:0.##}s, direction=random-horizontal, view={areaView.ViewID}.");
            }
            else
            {
                int[] ids = viewIds.ToArray();
                float[] times = new float[ids.Length];
                Array.Fill(times, affectTime);
                areaView.RPC("CreateAffectsRPC", RpcTarget.All, ids, times, hiddenPosition, direction);
                StagePhysicsEventsPlugin.ModLogger.LogDebug(
                    $"{EffectLabel} RPC batch sent: targets={ids.Length}, effectTimer={affectTime:0.##}s, direction={direction}, view={areaView.ViewID}.");
            }
            _coroutineOwner.StartCoroutine(DestroyCarrierLater(carrier, true));
        }
        catch (Exception exception)
        {
            StagePhysicsEventsPlugin.ModLogger.LogWarning($"{EffectLabel} RPC batch failed: {exception.Message}");
        }
    }

    private void ApplySingleplayer(
        IReadOnlyList<PhysGrabObject> objects,
        IReadOnlyList<PlayerAvatar> players,
        float affectTime,
        Vector3 direction)
    {
        if (SingleplayerObjectsField == null || SingleplayerPlayersField == null || CreateAffectsMethod == null)
        {
            StagePhysicsEventsPlugin.ModLogger.LogWarning(
                $"Standard singleplayer {EffectLabel} carrier API is unavailable; using direct vanilla SemiAffect fallback.");
            ApplySingleplayerDirect(objects, players, affectTime, direction);
            return;
        }

        try
        {
            Vector3 hiddenPosition = new(0f, -1000f, 0f);
            GameObject carrier = UnityEngine.Object.Instantiate(
                ProjectilePrefab!,
                hiddenPosition,
                Quaternion.identity);
            carrier.AddComponent<StagePhysicsEffectCarrierMarker>();
            SlowProjectile? projectile = carrier.GetComponent<SlowProjectile>();
            if (projectile != null)
            {
                projectile.enabled = false;
            }

            SemiAreaOfEffect? area = carrier.GetComponentInChildren<SemiAreaOfEffect>(true);
            if (area == null)
            {
                UnityEngine.Object.Destroy(carrier);
                StagePhysicsEventsPlugin.ModLogger.LogWarning(
                    $"Singleplayer {EffectLabel} carrier has no SemiAreaOfEffect; using direct vanilla SemiAffect fallback.");
                ApplySingleplayerDirect(objects, players, affectTime, direction);
                return;
            }

            BuildSingleplayerTargets(objects, players, out List<PhysGrabObject> targetObjects, out List<PlayerAvatar> targetPlayers);
            if (targetObjects.Count == 0 && targetPlayers.Count == 0)
            {
                UnityEngine.Object.Destroy(carrier);
                return;
            }

            SingleplayerObjectsField.SetValue(area, targetObjects);
            SingleplayerPlayersField.SetValue(area, targetPlayers);
            area.affectTimeMin = affectTime;
            area.affectTimeMax = affectTime;
            area.enemyLowestDifficultyTimeMin = affectTime;
            area.enemyLowestDifficultyTimeMax = affectTime;
            area.enemyHighestDifficultyTimeMin = affectTime;
            area.enemyHighestDifficultyTimeMax = affectTime;
            CreateAffectsMethod.Invoke(area, new object[]
            {
                Array.Empty<int>(),
                Array.Empty<float>(),
                hiddenPosition,
                direction,
                default(PhotonMessageInfo)
            });
            StagePhysicsEventsPlugin.ModLogger.LogDebug(
                $"Singleplayer {EffectLabel} carrier applied: objects={targetObjects.Count}, players={targetPlayers.Count}, effectTimer={affectTime:0.##}s.");
            _coroutineOwner.StartCoroutine(DestroyCarrierLater(carrier, false));
        }
        catch (Exception exception)
        {
            StagePhysicsEventsPlugin.ModLogger.LogWarning(
                $"Singleplayer {EffectLabel} carrier failed; using direct vanilla SemiAffect fallback: {exception}");
            ApplySingleplayerDirect(objects, players, affectTime, direction);
        }
    }

    private static void BuildSingleplayerTargets(
        IReadOnlyList<PhysGrabObject> objects,
        IReadOnlyList<PlayerAvatar> players,
        out List<PhysGrabObject> targetObjects,
        out List<PlayerAvatar> targetPlayers)
    {
        targetObjects = new List<PhysGrabObject>();
        targetPlayers = new List<PlayerAvatar>();
        HashSet<int> seenObjects = new();
        HashSet<int> seenPlayers = new();
        foreach (PhysGrabObject physObject in objects)
        {
            if (physObject == null || !seenObjects.Add(physObject.GetInstanceID()))
            {
                continue;
            }
            PlayerAvatar? player = physObject.GetComponentInParent<PlayerAvatar>();
            if (player != null)
            {
                if (seenPlayers.Add(player.GetInstanceID()))
                {
                    targetPlayers.Add(player);
                }
                continue;
            }
            targetObjects.Add(physObject);
        }

        foreach (PlayerAvatar player in players)
        {
            if (player != null && seenPlayers.Add(player.GetInstanceID()))
            {
                targetPlayers.Add(player);
            }
        }
    }

    private void ApplySingleplayerDirect(
        IReadOnlyList<PhysGrabObject> objects,
        IReadOnlyList<PlayerAvatar> players,
        float affectTime,
        Vector3 direction)
    {
        HashSet<int> seen = new();
        foreach (PhysGrabObject physObject in objects)
        {
            if (physObject == null || !seen.Add(physObject.GetInstanceID()))
            {
                continue;
            }
            PlayerAvatar? player = physObject.GetComponentInParent<PlayerAvatar>();
            SpawnSingleplayerAffect(physObject, player, affectTime, direction);
        }

        foreach (PlayerAvatar player in players)
        {
            if (player == null)
            {
                continue;
            }
            PhysGrabObject? tumbleObject = player.GetComponentInChildren<PhysGrabObject>(true);
            if (tumbleObject != null && seen.Add(tumbleObject.GetInstanceID()))
            {
                SpawnSingleplayerAffect(tumbleObject, player, affectTime, direction);
            }
        }
    }

    private void SpawnSingleplayerAffect(
        PhysGrabObject target,
        PlayerAvatar? player,
        float affectTime,
        Vector3 direction)
    {
        try
        {
            GameObject effect = UnityEngine.Object.Instantiate(
                AffectPrefab!,
                target.transform.position,
                Quaternion.identity);
            SemiAffect? semiAffect = effect.GetComponent<SemiAffect>();
            if (semiAffect == null)
            {
                UnityEngine.Object.Destroy(effect);
                return;
            }
            semiAffect.direction = direction;
            semiAffect.positionOfOriginalAreaOfEffect = target.transform.position;
            semiAffect.SetupSingleplayer(target.transform, target, affectTime, player);
        }
        catch (Exception exception)
        {
            StagePhysicsEventsPlugin.ModLogger.LogDebug($"Singleplayer {EffectLabel} target skipped: {exception.Message}");
        }
    }

    private Vector3 EffectDirection()
    {
        if (_kind == StaffAffectKind.ZeroGravity)
        {
            return Vector3.up;
        }
        // SemiAffectTorque ignores this value for players and follows each player's
        // current camera forward direction instead.
        return Vector3.forward;
    }

    private static Vector3 RandomHorizontalDirection()
    {
        float angle = UnityEngine.Random.Range(0f, 360f);
        return Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
    }

    private static PhotonView? ResolveTargetView(PhysGrabObject physObject)
    {
        PlayerAvatar? player = physObject.GetComponentInParent<PlayerAvatar>();
        if (player != null)
        {
            return player.photonView;
        }
        return physObject.GetComponent<PhotonView>() ??
               physObject.GetComponentInParent<PhotonView>() ??
               physObject.GetComponentInChildren<PhotonView>(true);
    }

    private static IEnumerator DestroyCarrierLater(GameObject carrier, bool networked)
    {
        yield return new WaitForSeconds(1f);
        if (carrier == null)
        {
            yield break;
        }
        if (networked && PhotonNetwork.IsMasterClient)
        {
            PhotonNetwork.Destroy(carrier);
        }
        else if (!networked)
        {
            UnityEngine.Object.Destroy(carrier);
        }
    }
}
