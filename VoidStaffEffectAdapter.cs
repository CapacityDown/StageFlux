using System;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

namespace REPOJP.StagePhysicsEvents;

internal sealed class VoidStaffEffectAdapter
{
    private readonly MonoBehaviour _coroutineOwner;
    private readonly VanillaEffectResolver _resolver;
    private readonly List<GameObject> _spawned = new();
    private int _generation;

    internal VoidStaffEffectAdapter(
        MonoBehaviour coroutineOwner,
        VanillaEffectResolver resolver)
    {
        _coroutineOwner = coroutineOwner;
        _resolver = resolver;
    }

    internal bool EnsureAvailable() => _resolver.ResolveVoid();

    internal void ApplyOnce(
        IReadOnlyList<PhysGrabObject> objects,
        IReadOnlyList<PlayerAvatar> players,
        int spawnCount)
    {
        if (!EnsureAvailable() || spawnCount <= 0)
        {
            return;
        }

        Stop();
        List<Vector3> positions = CollectStagePositions(objects, players);
        if (positions.Count == 0)
        {
            StagePhysicsEventsPlugin.ModLogger.LogWarning("Void event found no valid stage positions.");
            return;
        }

        Shuffle(positions);
        int generation = _generation;
        _coroutineOwner.StartCoroutine(
            SpawnStaged(generation, spawnCount, positions));
    }

    private IEnumerator SpawnStaged(
        int generation,
        int spawnCount,
        IReadOnlyList<Vector3> positions)
    {
        yield return null;
        int created = 0;
        for (int index = 0; index < spawnCount; index++)
        {
            if (generation != _generation)
            {
                break;
            }
            Vector3 position = positions[index % positions.Count] + Vector3.up * 0.1f;
            try
            {
                GameObject instance = SemiFunc.IsMultiplayer()
                    ? PhotonNetwork.Instantiate(_resolver.VoidResourcePath!, position, Quaternion.identity, 0)
                    : UnityEngine.Object.Instantiate(_resolver.VoidPrefab!, position, Quaternion.identity);
                instance.name = "StagePhysicsEvents_Void";
                instance.AddComponent<StagePhysicsEffectCarrierMarker>();
                _spawned.Add(instance);
                created++;
            }
            catch (Exception exception)
            {
                StagePhysicsEventsPlugin.ModLogger.LogWarning($"Could not create Void Staff effect at {position}: {exception.Message}");
            }
            yield return new WaitForSeconds(0.05f);
        }

        StagePhysicsEventsPlugin.ModLogger.LogInfo(
            $"Void Staff event created {created}/{spawnCount} effect(s) at random stage points.");
    }

    internal void Stop()
    {
        _generation++;
        DeferredObjectCleanupQueue.Enqueue(_spawned);
        _spawned.Clear();
    }

    private static List<Vector3> CollectStagePositions(
        IReadOnlyList<PhysGrabObject> objects,
        IReadOnlyList<PlayerAvatar> players)
    {
        List<Vector3> positions = new();
        HashSet<int> seen = new();
        try
        {
            List<LevelPoint>? levelPoints = SemiFunc.LevelPointsGetAll();
            if (levelPoints != null)
            {
                foreach (LevelPoint point in levelPoints)
                {
                    if (point != null && point.gameObject.activeInHierarchy && seen.Add(point.GetInstanceID()))
                    {
                        positions.Add(point.transform.position);
                    }
                }
            }
        }
        catch (Exception exception)
        {
            StagePhysicsEventsPlugin.ModLogger.LogDebug($"Level-point collection for Void event failed: {exception.Message}");
        }

        if (positions.Count > 0)
        {
            return positions;
        }
        foreach (PhysGrabObject target in objects)
        {
            if (target != null && seen.Add(target.GetInstanceID()))
            {
                positions.Add(target.transform.position);
            }
        }
        foreach (PlayerAvatar player in players)
        {
            if (player != null && seen.Add(player.GetInstanceID()))
            {
                positions.Add(player.transform.position);
            }
        }
        return positions;
    }

    private static void Shuffle(IList<Vector3> positions)
    {
        for (int index = positions.Count - 1; index > 0; index--)
        {
            int swapIndex = UnityEngine.Random.Range(0, index + 1);
            (positions[index], positions[swapIndex]) = (positions[swapIndex], positions[index]);
        }
    }
}
