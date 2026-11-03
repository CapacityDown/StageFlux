using System;

namespace REPOJP.StagePhysicsEvents;

internal static class SunstoneEventPolicy
{
    internal static int WaveCapacity(int requested, int maximum, int active) =>
        Math.Min(Math.Clamp(requested, 0, 30), Math.Max(0, Math.Clamp(maximum, 1, 30) - Math.Max(0, active)));

    internal static float RollPayloadDuration(float requested, float remaining, bool player)
    {
        if (float.IsNaN(requested) || float.IsNaN(remaining) || float.IsInfinity(requested) || float.IsInfinity(remaining)) return 0f;
        float actual = Math.Max(0f, Math.Min(requested, remaining));
        return player ? actual * 0.5f : actual;
    }
}
