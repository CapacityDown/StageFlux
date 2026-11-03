using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;

namespace REPOJP.StagePhysicsEvents;

[HarmonyPatch(typeof(MenuScrollBox), "Update")]
internal static class EventMenuScroll
{
    private const float BodyLinesPerNotch = 3f;
    // Unity's own UI module uses this internal getter. Cache its delegate,
    // but read the current value each time so input settings can change.
    private static readonly Func<float> ReadWheelUnitsPerNotch = AccessTools.MethodDelegate<Func<float>>(
        AccessTools.PropertyGetter(typeof(InputSystem), "scrollWheelDeltaPerTick"));

    [HarmonyTranspiler]
    internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> code = new(instructions);
        var wheelStep = AccessTools.Field(typeof(MenuScrollBox), "scrollWheelStep");
        var height = AccessTools.Field(typeof(MenuScrollBox), "scrollHeight");
        int matches = 0;
        for (int i = 0; i + 7 < code.Count; i++)
        {
            // v0.5.0 has separate stick/keyboard and wheel branches. Decorate
            // only Sign(wheel) * scrollWheelStep / (scrollHeight * 0.01).
            if (code[i].opcode != OpCodes.Ldarg_0 || !code[i + 1].LoadsField(wheelStep) ||
                code[i + 2].opcode != OpCodes.Mul || code[i + 3].opcode != OpCodes.Ldarg_0 ||
                !code[i + 4].LoadsField(height) || code[i + 5].opcode != OpCodes.Ldc_R4 ||
                !Equals(code[i + 5].operand, 0.01f) || code[i + 6].opcode != OpCodes.Mul ||
                code[i + 7].opcode != OpCodes.Div) continue;

            code.InsertRange(i + 8, new[]
            {
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(MenuScrollBox), "scrollerStartPosition")),
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(MenuScrollBox), "scrollerEndPosition")),
                CodeInstruction.Call(typeof(EventMenuScroll), nameof(WheelStep))
            });
            matches++;
            i += 13;
        }
        if (matches != 1)
            throw new InvalidOperationException($"Expected one MenuScrollBox wheel calculation, found {matches}.");
        return code;
    }

    internal static float WheelStep(float nativeStep, MenuScrollBox box, float start, float end)
    {
        if (!EventMenu.TryGetScrollSettings(box, out float lineHeight)) return nativeStep;
        float wheel = SemiFunc.InputScrollY();
        // Match Unity's UI input module: the runtime reports the current units
        // per tick, including normalized input and platform-specific ranges.
        float wheelUnitsPerNotch = ReadWheelUnitsPerNotch();
        float contentTravel = Mathf.Abs(start - end);
        float handleTravel = box.scrollBarBackground.rect.height - box.scrollHandle.sizeDelta.y;
        if (!IsFinite(wheel) || wheel == 0f || !IsFinite(wheelUnitsPerNotch) || wheelUnitsPerNotch <= 0f ||
            !IsFinite(contentTravel) || contentTravel <= 0f ||
            !IsFinite(handleTravel) || handleTravel <= 0f) return 0f;

        // Convert a fixed content distance to handle distance using the current
        // layout. Native scrollHeight can still describe the previous page.
        // Keep native clamping, animation, keyboard input and dragging intact.
        // Preserve magnitude: multiple detents can arrive in one input frame,
        // especially at lower FPS. Fractional input keeps its proportional distance.
        return wheel / wheelUnitsPerNotch * BodyLinesPerNotch * lineHeight * handleTravel / contentTravel;
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
