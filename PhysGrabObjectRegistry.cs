using System.Collections.Generic;
using UnityEngine;

namespace REPOJP.StagePhysicsEvents;

internal static class PhysGrabObjectRegistry
{
    private static readonly HashSet<PhysGrabObject> Objects = new();
    private static readonly List<PhysGrabObject> Stale = new();

    internal static void Register(PhysGrabObject? physObject)
    {
        if (physObject != null)
        {
            Objects.Add(physObject);
        }
    }

    internal static void SeedFromScene()
    {
        foreach (PhysGrabObject physObject in Object.FindObjectsOfType<PhysGrabObject>())
        {
            Register(physObject);
        }
    }

    internal static List<PhysGrabObject> Snapshot(TargetFilter filter, bool discoverEnemies = true)
    {
        List<PhysGrabObject> result = new(Objects.Count);
        HashSet<PhysGrabObject> selected = new();
        Stale.Clear();
        foreach (PhysGrabObject physObject in Objects)
        {
            if (physObject == null)
            {
                continue;
            }

            try
            {
                if (!StagePhysicsEffectCarrierUtility.IsInternalCarrier(physObject) &&
                    physObject.gameObject.activeInHierarchy &&
                    physObject.rb != null &&
                    filter.Allows(physObject) &&
                    selected.Add(physObject))
                {
                    result.Add(physObject);
                }
            }
            catch
            {
                Stale.Add(physObject);
            }
        }

        foreach (PhysGrabObject physObject in Stale)
        {
            Objects.Remove(physObject);
        }
        Objects.RemoveWhere(physObject => physObject == null);

        if (filter.Enemies && discoverEnemies)
        {
            foreach (EnemyRigidbody enemyRigidbody in Object.FindObjectsOfType<EnemyRigidbody>())
            {
                if (enemyRigidbody == null || !enemyRigidbody.gameObject.activeInHierarchy)
                {
                    continue;
                }
                PhysGrabObject? physObject = enemyRigidbody.GetComponent<PhysGrabObject>() ??
                                             enemyRigidbody.GetComponentInChildren<PhysGrabObject>(true) ??
                                             enemyRigidbody.GetComponentInParent<PhysGrabObject>();
                if (physObject == null || physObject.rb == null ||
                    StagePhysicsEffectCarrierUtility.IsInternalCarrier(physObject) ||
                    !selected.Add(physObject))
                {
                    continue;
                }
                Objects.Add(physObject);
                result.Add(physObject);
            }
        }
        return result;
    }

    internal static void Clear()
    {
        Objects.Clear();
        Stale.Clear();
    }
}
