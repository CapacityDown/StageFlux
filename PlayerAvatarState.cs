using System.Reflection;
using HarmonyLib;

namespace REPOJP.StagePhysicsEvents;

/// <summary>
/// Runtime-safe access to fields that the publicized compile-time GameLib exposes but the
/// unmodified game assembly keeps non-public.
/// </summary>
internal static class PlayerAvatarState
{
    private static readonly FieldInfo? DisabledField =
        AccessTools.Field(typeof(PlayerAvatar), "isDisabled");
    private static readonly FieldInfo? DeadField =
        AccessTools.Field(typeof(PlayerAvatar), "deadSet");
    private static readonly FieldInfo? SpectatingField =
        AccessTools.Field(typeof(PlayerAvatar), "spectating");
    private static readonly FieldInfo? HealthField =
        AccessTools.Field(typeof(PlayerHealth), "health");

    internal static bool IsLiving(PlayerAvatar? player)
    {
        if (player == null || !player.gameObject.activeInHierarchy ||
            ReadBool(DisabledField, player) ||
            ReadBool(DeadField, player) ||
            ReadBool(SpectatingField, player))
        {
            return false;
        }
        return player.playerHealth == null ||
               HealthField?.GetValue(player.playerHealth) is not int health ||
               health > 0;
    }

    private static bool ReadBool(FieldInfo? field, object instance) =>
        field?.GetValue(instance) is bool value && value;
}
