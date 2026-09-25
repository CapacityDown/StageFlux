using System;

namespace REPOJP.StagePhysicsEvents;

// Pure arithmetic shared by the runtime and the regression test executable.
internal static class ExtendedEventPolicy
{
    internal static float RepairAmount(float original, float current, int percent)
    {
        if (float.IsNaN(original) || float.IsInfinity(original) || original <= 0f ||
            float.IsNaN(current) || float.IsInfinity(current) || current < 0f || current >= original)
            return 0f;
        return (float)Math.Floor(Math.Min(original - current, original * Math.Clamp(percent, 1, 100) / 100d));
    }

    internal static int SharedDamage(int lost, int percent, int maximum, int recipientHealth, bool canKill)
    {
        if (lost <= 0 || recipientHealth <= 0)
            return 0;
        int amount = (int)Math.Min(Math.Max(1, maximum), Math.Ceiling((double)lost * Math.Clamp(percent, 1, 100) / 100d));
        return canKill ? amount : Math.Min(amount, Math.Max(0, recipientHealth - 1));
    }

    internal static float DrainCharge(float current, int amount, int minimum) =>
        float.IsNaN(current) || float.IsInfinity(current) ? current :
        Math.Max(Math.Min(current, Math.Clamp(minimum, 0, 100)), current - Math.Clamp(amount, 1, 100));

    internal static int EnemyDamage(int damage, int percent) => damage <= 0 ? damage :
        (int)Math.Min(int.MaxValue, Math.Max(1d, Math.Round((double)damage * Math.Clamp(percent, 1, 500) / 100d)));
}
