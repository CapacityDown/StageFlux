using UnityEngine;

namespace REPOJP.StagePhysicsEvents;

internal static class StagePhysicsEffectCarrierUtility
{
    internal static bool IsInternalCarrier(Component? component)
    {
        if (component == null)
        {
            return false;
        }

        try
        {
            return component.GetComponent<StagePhysicsEffectCarrierMarker>() != null ||
                   component.GetComponentInParent<StagePhysicsEffectCarrierMarker>() != null ||
                   component.GetComponentInChildren<StagePhysicsEffectCarrierMarker>(true) != null;
        }
        catch
        {
            return false;
        }
    }
}
