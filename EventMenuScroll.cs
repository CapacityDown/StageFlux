using HarmonyLib;
using UnityEngine;

namespace REPOJP.StagePhysicsEvents;

// Use 0.5.0's wheel-step setting, preserving its mouse/gamepad navigation.
[HarmonyPatch(typeof(MenuScrollBox), "Update")]
internal static class EventMenuScroll
{
    [HarmonyPrefix]
    private static void Prefix(MenuScrollBox __instance)
    {
        if (!EventMenu.OwnsScrollBox(__instance) ||
            __instance.scrollBarBackground == null || __instance.scrollHandle == null)
            return;
        float travel = Mathf.Abs(__instance.scrollerStartPosition - __instance.scrollerEndPosition);
        float handle = __instance.scrollBarBackground.rect.height - __instance.scrollHandle.sizeDelta.y;
        float height = __instance.scrollHeight;
        if (travel <= 0f || handle <= 0f || height <= 0f)
            return;
        // Native step is divided by scrollHeight * .01 before moving the handle.
        __instance.scrollWheelStep = 75f * handle / travel * height * 0.01f;
    }
}
