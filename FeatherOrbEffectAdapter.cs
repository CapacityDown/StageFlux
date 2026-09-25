using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;

namespace REPOJP.StagePhysicsEvents;

internal sealed class FeatherOrbEffectAdapter
{
    // Keep the hidden carrier inside the Feather Orb's normal player range while
    // keeping its networked prefab copy outside the player's body and foot probe.
    private const float FollowOrbVerticalOffset = 0.75f;
    private const float FollowOrbHorizontalOffset = 0.6f;

    private sealed class FollowOrb
    {
        internal FollowOrb(PlayerAvatar player, GameObject orb)
        {
            Player = player;
            Orb = orb;
        }

        internal PlayerAvatar Player { get; }
        internal GameObject Orb { get; }
    }

    private static readonly FieldInfo? ObjectAffectedField = AccessTools.Field(typeof(ItemOrb), "objectAffected");
    private static readonly FieldInfo? LocalPlayerAffectedField = AccessTools.Field(typeof(ItemOrb), "localPlayerAffected");
    private readonly MonoBehaviour _coroutineOwner;
    private readonly VanillaEffectResolver _resolver;
    private readonly Dictionary<int, FollowOrb> _followOrbs = new();
    private readonly List<int> _staleKeys = new();
    private readonly List<PhysGrabObject> _targets = new();
    private GameObject? _virtualOrb;
    private ItemOrb? _virtualItemOrb;

    internal FeatherOrbEffectAdapter(MonoBehaviour coroutineOwner, VanillaEffectResolver resolver)
    {
        _coroutineOwner = coroutineOwner;
        _resolver = resolver;
    }

    internal bool EnsureAvailable() => ObjectAffectedField != null && _resolver.ResolveFeather();

    internal void ApplyOnce(IReadOnlyList<PhysGrabObject> targets, IReadOnlyList<PlayerAvatar> players)
    {
        if (!EnsureAvailable())
        {
            return;
        }

        EnsureVirtualOrb();
        _targets.Clear();
        HashSet<int> selectedTargets = new();
        foreach (PhysGrabObject target in targets)
        {
            if (target != null &&
                !StagePhysicsEffectCarrierUtility.IsInternalCarrier(target) &&
                selectedTargets.Add(target.GetInstanceID()))
            {
                _targets.Add(target);
            }
        }
        if (_virtualItemOrb != null)
        {
            try
            {
                ObjectAffectedField!.SetValue(_virtualItemOrb, new List<PhysGrabObject>(_targets));
                _virtualItemOrb.itemActive = true;
            }
            catch (Exception exception)
            {
                StagePhysicsEventsPlugin.ModLogger.LogWarning($"Could not refresh virtual Feather Orb targets: {exception.Message}");
            }
        }

        SyncFollowOrbs(players);
        UpdateFollowOrbPositions();
    }

    internal void AddTarget(PhysGrabObject target)
    {
        if (!EnsureAvailable() || target == null ||
            StagePhysicsEffectCarrierUtility.IsInternalCarrier(target))
        {
            return;
        }
        foreach (PhysGrabObject existing in _targets)
        {
            if (existing == target)
            {
                return;
            }
        }
        _targets.Add(target);
        EnsureVirtualOrb();
        if (_virtualItemOrb != null)
        {
            try
            {
                ObjectAffectedField!.SetValue(_virtualItemOrb, new List<PhysGrabObject>(_targets));
                _virtualItemOrb.itemActive = true;
            }
            catch (Exception exception)
            {
                StagePhysicsEventsPlugin.ModLogger.LogWarning(
                    $"Could not add a new Feather target: {exception.Message}");
            }
        }
    }

    internal void TickFollowOrbs()
    {
        _staleKeys.Clear();
        UpdateFollowOrbPositions();
        foreach (KeyValuePair<int, FollowOrb> pair in _followOrbs)
        {
            FollowOrb follow = pair.Value;
            if (follow.Player == null || follow.Orb == null)
            {
                DestroyFollowOrb(follow.Orb);
                _staleKeys.Add(pair.Key);
                continue;
            }
            ItemBattery? battery = follow.Orb.GetComponent<ItemBattery>();
            if (battery != null)
            {
                battery.autoDrain = false;
                battery.batteryLife = 100f;
            }
        }
        foreach (int key in _staleKeys)
        {
            _followOrbs.Remove(key);
        }
    }

    internal void RefreshPlayer(PlayerAvatar player)
    {
        if (!EnsureAvailable() || player == null || !player.gameObject.activeInHierarchy)
        {
            return;
        }
        if (player.photonView != null)
        {
            int key = player.photonView.ViewID != 0 ? player.photonView.ViewID : player.GetInstanceID();
            if (_followOrbs.TryGetValue(key, out FollowOrb existing))
            {
                DestroyFollowOrb(existing.Orb);
                _followOrbs.Remove(key);
            }
        }
        EnsureFollowOrb(player);
        UpdateFollowOrbPositions();
    }

    internal void Stop()
    {
        if (_virtualItemOrb != null)
        {
            _virtualItemOrb.itemActive = false;
            try
            {
                ObjectAffectedField?.SetValue(_virtualItemOrb, new List<PhysGrabObject>());
            }
            catch
            {
                // Unity is already tearing this object down.
            }
        }
        if (_virtualOrb != null)
        {
            UnityEngine.Object.Destroy(_virtualOrb);
        }
        _virtualOrb = null;
        _virtualItemOrb = null;
        _targets.Clear();

        foreach (FollowOrb follow in _followOrbs.Values)
        {
            DestroyFollowOrb(follow.Orb);
        }
        _followOrbs.Clear();
    }

    private void EnsureVirtualOrb()
    {
        if (_virtualOrb != null)
        {
            return;
        }

        _virtualOrb = UnityEngine.Object.Instantiate(
            _resolver.FeatherPrefab!,
            new Vector3(0f, -1000f, 0f),
            Quaternion.identity);
        _virtualOrb.name = "StagePhysicsEvents_VirtualFeatherOrb";
        _virtualOrb.hideFlags = HideFlags.HideAndDontSave;
        _virtualItemOrb = _virtualOrb.GetComponent<ItemOrb>();
        if (_virtualItemOrb == null)
        {
            StagePhysicsEventsPlugin.ModLogger.LogWarning("Resolved Feather Orb prefab has no ItemOrb component.");
            UnityEngine.Object.Destroy(_virtualOrb);
            _virtualOrb = null;
            return;
        }

        _virtualItemOrb.itemActive = true;
        foreach (Behaviour behaviour in _virtualOrb.GetComponentsInChildren<Behaviour>(true))
        {
            behaviour.enabled = behaviour is ItemOrbFeather;
        }
        foreach (Renderer renderer in _virtualOrb.GetComponentsInChildren<Renderer>(true))
        {
            renderer.enabled = false;
        }
        foreach (Collider collider in _virtualOrb.GetComponentsInChildren<Collider>(true))
        {
            collider.enabled = false;
        }
        foreach (AudioSource audio in _virtualOrb.GetComponentsInChildren<AudioSource>(true))
        {
            audio.enabled = false;
        }
        foreach (Rigidbody rigidbody in _virtualOrb.GetComponentsInChildren<Rigidbody>(true))
        {
            rigidbody.isKinematic = true;
            rigidbody.detectCollisions = false;
        }
    }

    private void SyncFollowOrbs(IReadOnlyList<PlayerAvatar> players)
    {
        HashSet<int> current = new();
        foreach (PlayerAvatar player in players)
        {
            if (player == null || player.photonView == null || !player.gameObject.activeInHierarchy)
            {
                continue;
            }

            int key = player.photonView.ViewID != 0 ? player.photonView.ViewID : player.GetInstanceID();
            current.Add(key);
            EnsureFollowOrb(player);
        }

        _staleKeys.Clear();
        foreach (KeyValuePair<int, FollowOrb> pair in _followOrbs)
        {
            if (!current.Contains(pair.Key) || pair.Value.Player == null || pair.Value.Orb == null)
            {
                DestroyFollowOrb(pair.Value.Orb);
                _staleKeys.Add(pair.Key);
            }
        }
        foreach (int key in _staleKeys)
        {
            _followOrbs.Remove(key);
        }
    }

    private void EnsureFollowOrb(PlayerAvatar player)
    {
        if (player.photonView == null)
        {
            return;
        }
        int key = player.photonView.ViewID != 0 ? player.photonView.ViewID : player.GetInstanceID();
        if (_followOrbs.TryGetValue(key, out FollowOrb existing) && existing.Orb != null)
        {
            return;
        }
        GameObject? orb = CreateFollowOrb(player);
        if (orb != null)
        {
            _followOrbs[key] = new FollowOrb(player, orb);
        }
    }

    private GameObject? CreateFollowOrb(PlayerAvatar player)
    {
        try
        {
            Vector3 position = FollowOrbPosition(player);
            GameObject orb;
            if (SemiFunc.IsMultiplayer())
            {
                object[] scale = { 0.01f, 0.01f, 0.01f };
                orb = PhotonNetwork.Instantiate(_resolver.FeatherResourcePath!, position, Quaternion.identity, 0, scale);
            }
            else
            {
                orb = UnityEngine.Object.Instantiate(_resolver.FeatherPrefab!, position, Quaternion.identity);
                orb.transform.localScale = Vector3.one * 0.01f;
            }

            orb.name = "StagePhysicsEvents_PlayerFeatherOrb";
            orb.AddComponent<StagePhysicsEffectCarrierMarker>();
            DisableFollowOrbPhysicalInteraction(orb);
            // ItemOrb and ItemOrbFeather initialize their private component references in Start().
            // Enabling the orb in the instantiate frame calls into those components too early.
            _coroutineOwner.StartCoroutine(InitializeFollowOrbAfterStart(orb, player));
            return orb;
        }
        catch (Exception exception)
        {
            StagePhysicsEventsPlugin.ModLogger.LogWarning($"Could not create player Feather Orb: {exception}");
            return null;
        }
    }

    private static IEnumerator InitializeFollowOrbAfterStart(GameObject orb, PlayerAvatar player)
    {
        yield return null;

        if (orb == null || player == null)
        {
            yield break;
        }

        try
        {
            ItemOrb? itemOrb = orb.GetComponent<ItemOrb>();
            if (itemOrb == null)
            {
                StagePhysicsEventsPlugin.ModLogger.LogWarning("Player Feather Orb has no ItemOrb component after initialization.");
                yield break;
            }

            itemOrb.targetEnemies = false;
            itemOrb.targetNonValuables = false;
            itemOrb.targetValuables = false;
            itemOrb.targetPlayers = true;

            ItemBattery? battery = orb.GetComponent<ItemBattery>();
            if (battery != null)
            {
                battery.autoDrain = false;
                battery.batteryLife = 100f;
            }

            ItemToggle? toggle = orb.GetComponent<ItemToggle>();
            if (toggle == null)
            {
                StagePhysicsEventsPlugin.ModLogger.LogWarning("Player Feather Orb has no ItemToggle component after initialization.");
                yield break;
            }

            // This is a game-standard RPC, so vanilla participants activate their local prefab copy too.
            toggle.ToggleItem(true, 0);
            DisableFollowOrbPhysicalInteraction(orb);
            PhysGrabObject? physObject = orb.GetComponent<PhysGrabObject>() ??
                                         orb.GetComponentInChildren<PhysGrabObject>(true);
            physObject?.PhysRidingDisabledSet(true);

            // Keep DroneToOrbItem's host-side ItemOrb patches away from this internal carrier.
            // Participants retain the standard ItemOrb.Update and local-player overlap behavior.
            ObjectAffectedField?.SetValue(itemOrb, new List<PhysGrabObject>());
            bool affectsHostPlayer = PlayerController.instance?.playerAvatarScript == player;
            LocalPlayerAffectedField?.SetValue(itemOrb, affectsHostPlayer);
            itemOrb.itemActive = true;
            itemOrb.enabled = false;
            StagePhysicsEventsPlugin.ModLogger.LogDebug(
                $"Player Feather Orb activated: playerView={player.photonView?.ViewID ?? 0}, hostLocal={affectsHostPlayer}, orbView={orb.GetComponent<PhotonView>()?.ViewID ?? 0}");
        }
        catch (Exception exception)
        {
            StagePhysicsEventsPlugin.ModLogger.LogWarning($"Could not initialize player Feather Orb: {exception}");
        }
    }

    private void UpdateFollowOrbPositions()
    {
        foreach (FollowOrb follow in _followOrbs.Values)
        {
            if (follow.Orb == null || follow.Player == null)
            {
                continue;
            }
            Vector3 position = FollowOrbPosition(follow.Player);
            PhysGrabObject? physObject = follow.Orb.GetComponent<PhysGrabObject>();
            if (physObject != null && physObject.rb != null)
            {
                physObject.rb.isKinematic = true;
                physObject.rb.detectCollisions = false;
                physObject.rb.position = position;
            }
            follow.Orb.transform.position = position;
        }
    }

    private static Vector3 FollowOrbPosition(PlayerAvatar player) =>
        player.transform.position +
        Vector3.up * FollowOrbVerticalOffset +
        Vector3.right * FollowOrbHorizontalOffset;

    private static void DisableFollowOrbPhysicalInteraction(GameObject orb)
    {
        foreach (Collider collider in orb.GetComponentsInChildren<Collider>(true))
        {
            collider.enabled = false;
        }

        PhysGrabObject? physObject = orb.GetComponent<PhysGrabObject>() ??
                                     orb.GetComponentInChildren<PhysGrabObject>(true);
        if (physObject?.rb != null)
        {
            if (!physObject.rb.isKinematic)
            {
                physObject.rb.velocity = Vector3.zero;
                physObject.rb.angularVelocity = Vector3.zero;
            }
            physObject.rb.isKinematic = true;
            physObject.rb.detectCollisions = false;
        }
    }

    private static void DestroyFollowOrb(GameObject? orb)
    {
        if (orb == null)
        {
            return;
        }
        try
        {
            ItemOrb? itemOrb = orb.GetComponent<ItemOrb>();
            if (itemOrb != null)
            {
                itemOrb.itemActive = false;
                ObjectAffectedField?.SetValue(itemOrb, new List<PhysGrabObject>());
                LocalPlayerAffectedField?.SetValue(itemOrb, false);
            }
            orb.GetComponent<ItemToggle>()?.ToggleItem(false, 0);
            DeferredObjectCleanupQueue.Enqueue(orb);
        }
        catch (Exception exception)
        {
            StagePhysicsEventsPlugin.ModLogger.LogDebug($"Feather Orb cleanup deferred to scene unload: {exception.Message}");
        }
    }
}
