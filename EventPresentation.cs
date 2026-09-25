using UnityEngine;

namespace REPOJP.StagePhysicsEvents;

internal enum EventDanger
{
    Neutral,
    Low,
    Medium,
    High,
    Locked
}

internal static class EventPresentation
{
    internal static string Name(StageEffect effect) =>
        StageEffectSet.Format(effect, " + ");

    internal static EventDanger Danger(StageEffect effect)
    {
        if (effect == StageEffect.None)
        {
            return EventDanger.Neutral;
        }

        EventDanger danger = EventDanger.Low;
        foreach (StageEffect individual in StageEffectSet.IndividualEffects)
        {
            if (!StageEffectSet.Contains(effect, individual))
            {
                continue;
            }
            EventDanger current = individual switch
            {
                StageEffect.ZeroGravity or
                StageEffect.Levitation or
                StageEffect.Freeze or
                StageEffect.Stun or
                StageEffect.Shockwave or
                StageEffect.StunBlast or
                StageEffect.Knockback or
                StageEffect.Flicker or
                StageEffect.DoorChaos or
                StageEffect.GumballHypnosis or
                StageEffect.SpiderScare => EventDanger.Medium,
                StageEffect.BatteryDrain or
                StageEffect.HeavyCargo or
                StageEffect.Butterfingers or
                StageEffect.PlayerSwap => EventDanger.Medium,
                StageEffect.EnemyArmor or StageEffect.SharedPain => EventDanger.High,

                StageEffect.Fragility or
                StageEffect.Roll or
                StageEffect.Void or
                StageEffect.ExplosionRain or
                StageEffect.EnemyWave or
                StageEffect.Minefield or
                StageEffect.EnemyWarp or
                StageEffect.EnemyHunt or
                StageEffect.EnemyRegen or
                StageEffect.DamagePulse or
                StageEffect.Quake or
                StageEffect.ValueCrash or
                StageEffect.StarBarrage or
                StageEffect.TrafficShock or
                StageEffect.DangerousValuables => EventDanger.High,

                StageEffect.EnemySpeedUp => EventDanger.High,
                StageEffect.EnemySpeedDown => EventDanger.Low,

                _ => EventDanger.Low
            };
            if ((int)current > (int)danger)
            {
                danger = current;
            }
        }
        return danger;
    }

    internal static Color Color(EventDanger danger) => danger switch
    {
        EventDanger.Low => FromRgb(0x70, 0xA6, 0x50),
        EventDanger.Medium => FromRgb(0xCF, 0x9E, 0x35),
        EventDanger.High => FromRgb(0xB8, 0x41, 0x43),
        EventDanger.Locked => FromRgb(0x5B, 0x5E, 0x5B),
        _ => FromRgb(0xB0, 0xB3, 0xAA)
    };

    private static Color FromRgb(byte red, byte green, byte blue) =>
        new(red / 255f, green / 255f, blue / 255f, 1f);
}
