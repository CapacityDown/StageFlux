using UnityEngine;

namespace REPOJP.StagePhysicsEvents;

/// <summary>
/// Keeps short-lived vanilla effect carriers out of the player's grounded probe.
/// A PhysGrabObject without a fully initialized RoomVolumeCheck can otherwise stop
/// PlayerCollisionGrounded's coroutine on both modded and vanilla clients.
/// </summary>
internal static class PlayerEffectCarrierUtility
{
    private const float VerticalOffset = 0.75f;

    internal static Vector3 Position(PlayerAvatar player) =>
        player.transform.position + Vector3.up * VerticalOffset;

    internal static void DisablePhysicalInteraction(GameObject carrier)
    {
        foreach (Collider collider in carrier.GetComponentsInChildren<Collider>(true))
        {
            collider.enabled = false;
        }

        foreach (Rigidbody body in carrier.GetComponentsInChildren<Rigidbody>(true))
        {
            if (!body.isKinematic)
            {
                body.velocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            body.isKinematic = true;
            body.detectCollisions = false;
        }
    }
}
