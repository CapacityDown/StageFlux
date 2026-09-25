using System;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

namespace REPOJP.StagePhysicsEvents;

/// <summary>
/// Spreads network/local object destruction across frames. Destroying a whole
/// event's spawned object set in one frame causes a visible hitch, especially
/// when Photon has to serialize several destroy operations at once.
/// </summary>
internal static class DeferredObjectCleanupQueue
{
    private readonly struct PendingObject
    {
        internal PendingObject(GameObject instance, int instanceId)
        {
            Instance = instance;
            InstanceId = instanceId;
        }

        internal GameObject Instance { get; }
        internal int InstanceId { get; }
    }

    private const int MaximumDestroysPerFrame = 2;
    private static readonly Queue<PendingObject> Pending = new();
    private static readonly Queue<Action> PendingActions = new();
    private static readonly HashSet<int> PendingIds = new();

    internal static void Enqueue(GameObject? instance)
    {
        if (instance == null)
        {
            return;
        }

        int instanceId = instance.GetInstanceID();
        if (PendingIds.Add(instanceId))
        {
            Pending.Enqueue(new PendingObject(instance, instanceId));
        }
    }

    internal static void Enqueue(IEnumerable<GameObject> instances)
    {
        foreach (GameObject instance in instances)
        {
            Enqueue(instance);
        }
    }

    internal static void Enqueue(Action action)
    {
        if (action != null)
        {
            PendingActions.Enqueue(action);
        }
    }

    internal static void ProcessFrame()
    {
        int processed = 0;
        while (processed < MaximumDestroysPerFrame &&
               (Pending.Count > 0 || PendingActions.Count > 0))
        {
            if (PendingActions.Count > 0)
            {
                try
                {
                    PendingActions.Dequeue().Invoke();
                }
                catch (Exception exception)
                {
                    StagePhysicsEventsPlugin.ModLogger.LogDebug(
                        $"Deferred event cleanup action failed: {exception.GetBaseException().Message}");
                }
                processed++;
                continue;
            }

            PendingObject pending = Pending.Dequeue();
            PendingIds.Remove(pending.InstanceId);
            GameObject instance = pending.Instance;
            if (instance == null)
            {
                continue;
            }

            DestroyNow(instance);
            processed++;
        }
    }

    internal static void ClearReferences()
    {
        Pending.Clear();
        PendingActions.Clear();
        PendingIds.Clear();
    }

    private static void DestroyNow(GameObject instance)
    {
        try
        {
            PhotonView? view = instance.GetComponent<PhotonView>() ??
                               instance.GetComponentInParent<PhotonView>() ??
                               instance.GetComponentInChildren<PhotonView>(true);
            if (SemiFunc.IsMultiplayer() &&
                PhotonNetwork.IsMasterClient &&
                view != null &&
                view.ViewID != 0)
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
                $"Deferred event-object cleanup left to scene unload: {exception.Message}");
        }
    }
}
