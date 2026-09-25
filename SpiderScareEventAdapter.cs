using System;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

namespace REPOJP.StagePhysicsEvents;

internal sealed class SpiderScareEventAdapter
{
    private readonly MonoBehaviour _coroutineOwner;
    private readonly VanillaSpawnEffectResolver _resolver;
    private readonly List<GameObject> _sources = new();
    private bool _active;
    private int _generation;
    private int _playersPerWave;
    private int _spawnIntervalSeconds;
    private float _nextSpawnAt;

    internal SpiderScareEventAdapter(
        MonoBehaviour coroutineOwner,
        VanillaSpawnEffectResolver resolver)
    {
        _coroutineOwner = coroutineOwner;
        _resolver = resolver;
    }

    internal bool EnsureAvailable() =>
        _resolver.TryResolve(StageEffect.SpiderScare, out _);

    internal void Begin(int playersPerWave, int spawnIntervalSeconds)
    {
        Stop();
        _playersPerWave = Mathf.Clamp(playersPerWave, 1, 30);
        _spawnIntervalSeconds = Mathf.Clamp(spawnIntervalSeconds, 1, 300);
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
        _sources.RemoveAll(source => source == null);
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
        _nextSpawnAt = 0f;
    }

    private void SpawnWave(int generation)
    {
        if (!_active || !_resolver.TryResolve(StageEffect.SpiderScare, out ResolvedSpawnPrefab resolved))
        {
            return;
        }
        List<PlayerAvatar> players = CollectLivingPlayers();
        Shuffle(players);
        int amount = Mathf.Min(_playersPerWave, players.Count);
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
                GameObject source = SemiFunc.IsMultiplayer()
                    ? PhotonNetwork.Instantiate(
                        resolved.ResourcePath,
                        position,
                        Quaternion.identity,
                        0)
                    : UnityEngine.Object.Instantiate(
                        resolved.Prefab,
                        position,
                        Quaternion.identity);
                source.name = "StagePhysicsEvents_SpiderScareSource";
                source.AddComponent<StagePhysicsEffectCarrierMarker>();
                PlayerEffectCarrierUtility.DisablePhysicalInteraction(source);
                _sources.Add(source);
                _coroutineOwner.StartCoroutine(ActivateAfterStart(source, player, generation));
                created++;
            }
            catch (Exception exception)
            {
                StagePhysicsEventsPlugin.ModLogger.LogWarning(
                    $"Could not create Spider Scare source: {exception.Message}");
            }
            yield return new WaitForSeconds(0.05f);
        }
        StagePhysicsEventsPlugin.ModLogger.LogDebug(
            $"Spider Scare wave targeted {created}/{amount} player position(s).");
    }

    private IEnumerator ActivateAfterStart(GameObject source, PlayerAvatar player, int generation)
    {
        yield return null;
        // Give vanilla participants time to instantiate and initialize the prefab before
        // its destruction-effect RPC is delivered.
        yield return new WaitForSeconds(0.2f);
        if (!_active || generation != _generation || source == null || player == null)
        {
            DestroyNetworkObject(source);
            yield break;
        }
        PhysGrabObjectImpactDetector? detector =
            source.GetComponentInChildren<PhysGrabObjectImpactDetector>(true);
        PhysGrabObject? physObject = source.GetComponentInChildren<PhysGrabObject>(true);
        if (detector == null || physObject == null)
        {
            StagePhysicsEventsPlugin.ModLogger.LogWarning(
                "Spider Scare source has no initialized impact detector.");
            DestroyNetworkObject(source);
            yield break;
        }
        if (!StagePhysicsEffectCarrierUtility.IsInternalCarrier(physObject))
        {
            physObject.gameObject.AddComponent<StagePhysicsEffectCarrierMarker>();
        }
        try
        {
            source.transform.SetPositionAndRotation(
                PlayerEffectCarrierUtility.Position(player),
                Quaternion.identity);
            PlayerEffectCarrierUtility.DisablePhysicalInteraction(source);
            physObject.OverrideKinematic(1f);
            physObject.OverrideGrabDisable(1f);
            PhotonView? view = detector.GetComponent<PhotonView>() ??
                               detector.GetComponentInParent<PhotonView>();
            if (SemiFunc.IsMultiplayer() && view != null && view.ViewID != 0)
            {
                view.RPC(
                    nameof(PhysGrabObjectImpactDetector.DestroyObjectRPC),
                    RpcTarget.AllViaServer,
                    true);
            }
            else
            {
                detector.DestroyObjectRPC(effects: true);
            }
            StagePhysicsEventsPlugin.ModLogger.LogDebug(
                $"Activated Spider Scare at player {player.photonView?.ViewID ?? 0}.");
        }
        catch (Exception exception)
        {
            StagePhysicsEventsPlugin.ModLogger.LogWarning(
                $"Could not activate Spider Scare: {exception.Message}");
            DestroyNetworkObject(source);
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
                $"Spider Scare cleanup deferred to scene unload: {exception.Message}");
        }
    }
}
