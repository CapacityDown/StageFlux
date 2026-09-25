using HarmonyLib;

namespace REPOJP.StagePhysicsEvents;

internal static class NotificationEnemyReactionPatches
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(PlayerVoiceChat), "Update")]
    private static void PlayerVoiceChatUpdatePrefix(PlayerVoiceChat __instance, out bool __state)
    {
        __state = NotificationEnemyReactionGuard.PrepareVoiceUpdate(__instance);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PlayerVoiceChat), "Update")]
    private static void PlayerVoiceChatUpdatePostfix(PlayerVoiceChat __instance, bool __state)
    {
        if (__state)
        {
            NotificationEnemyReactionGuard.RestoreMicrophoneInvestigation(__instance);
        }
    }
}
