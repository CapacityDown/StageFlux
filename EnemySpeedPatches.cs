using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.AI;

namespace REPOJP.StagePhysicsEvents;

internal static class EnemySpeedRuntime
{
    private static readonly FieldInfo? DefaultSpeedField =
        AccessTools.Field(typeof(EnemyNavMeshAgent), "DefaultSpeed");
    private static readonly FieldInfo? DefaultAccelerationField =
        AccessTools.Field(typeof(EnemyNavMeshAgent), "DefaultAcceleration");
    private static readonly Dictionary<int, NavValues> NavOriginals = new();
    private static readonly Dictionary<int, RigidbodyValues> RigidbodyOriginals = new();
    private static float _multiplier = 1f;

    internal static bool Active => !Mathf.Approximately(_multiplier, 1f);

    internal static void SetMultiplier(float multiplier)
    {
        _multiplier = Mathf.Clamp(multiplier, 0.1f, 5f);
        if (!Active)
        {
            return;
        }
        (int navCount, int rigidbodyCount) = RefreshExistingEnemies();
        StagePhysicsEventsPlugin.ModLogger.LogInfo(
            $"Enemy movement multiplier set to {_multiplier:0.##}x for " +
            $"{navCount} NavMesh and {rigidbodyCount} Rigidbody agent(s).");
    }

    internal static (int NavCount, int RigidbodyCount) RefreshExistingEnemies()
    {
        if (!Active || !SemiFunc.IsMasterClientOrSingleplayer())
        {
            return (0, 0);
        }

        int navCount = 0;
        foreach (EnemyNavMeshAgent enemyAgent in
                 Resources.FindObjectsOfTypeAll<EnemyNavMeshAgent>())
        {
            if (ApplyNav(enemyAgent))
            {
                navCount++;
            }
        }

        int rigidbodyCount = 0;
        foreach (EnemyRigidbody enemyRigidbody in
                 Resources.FindObjectsOfTypeAll<EnemyRigidbody>())
        {
            if (ApplyRigidbody(enemyRigidbody))
            {
                rigidbodyCount++;
            }
        }
        return (navCount, rigidbodyCount);
    }

    internal static void ApplyNewNav(EnemyNavMeshAgent enemyAgent)
    {
        if (Active && SemiFunc.IsMasterClientOrSingleplayer())
        {
            ApplyNav(enemyAgent);
        }
    }

    internal static void ApplyNewRigidbody(EnemyRigidbody enemyRigidbody)
    {
        if (Active && SemiFunc.IsMasterClientOrSingleplayer())
        {
            ApplyRigidbody(enemyRigidbody);
        }
    }

    internal static void ApplyNavOverride(
        EnemyNavMeshAgent enemyAgent,
        ref float speed,
        ref float acceleration)
    {
        if (!CanApply(enemyAgent))
        {
            return;
        }
        speed = Mathf.Max(0.1f, speed * _multiplier);
        acceleration = Mathf.Max(0f, acceleration * _multiplier);
    }

    internal static void ApplyRigidbodyOverride(
        EnemyRigidbody enemyRigidbody,
        ref float speed)
    {
        if (!CanApply(enemyRigidbody))
        {
            return;
        }
        speed = Mathf.Max(0.1f, speed * _multiplier);
    }

    internal static void Clear()
    {
        bool wasActive = Active;
        _multiplier = 1f;

        foreach (NavValues original in NavOriginals.Values)
        {
            EnemyNavMeshAgent enemyAgent = original.Agent;
            if (enemyAgent == null)
            {
                continue;
            }
            DefaultSpeedField?.SetValue(enemyAgent, original.DefaultSpeed);
            DefaultAccelerationField?.SetValue(enemyAgent, original.DefaultAcceleration);
            NavMeshAgent? navAgent = enemyAgent.GetComponent<NavMeshAgent>();
            if (navAgent != null)
            {
                navAgent.speed = original.DefaultSpeed;
                navAgent.acceleration = original.DefaultAcceleration;
            }
        }

        foreach (RigidbodyValues original in RigidbodyOriginals.Values)
        {
            EnemyRigidbody enemyRigidbody = original.Agent;
            if (enemyRigidbody == null)
            {
                continue;
            }
            enemyRigidbody.positionSpeedIdle = original.PositionSpeedIdle;
            enemyRigidbody.positionSpeedChase = original.PositionSpeedChase;
            enemyRigidbody.rotationSpeedIdle = original.RotationSpeedIdle;
            enemyRigidbody.rotationSpeedChase = original.RotationSpeedChase;
        }

        NavOriginals.Clear();
        RigidbodyOriginals.Clear();
        if (wasActive)
        {
            StagePhysicsEventsPlugin.ModLogger.LogInfo(
                "Enemy NavMesh and Rigidbody movement restored to pre-event values; " +
                "external movement multipliers remain independently controlled.");
        }
    }

    private static bool ApplyNav(EnemyNavMeshAgent enemyAgent)
    {
        if (!CanApply(enemyAgent))
        {
            return false;
        }
        int key = enemyAgent.GetInstanceID();
        NavMeshAgent? navAgent = enemyAgent.GetComponent<NavMeshAgent>();
        if (!NavOriginals.TryGetValue(key, out NavValues original))
        {
            float currentSpeed = DefaultSpeedField?.GetValue(enemyAgent) is float reflectedSpeed
                ? reflectedSpeed
                : navAgent != null
                    ? navAgent.speed
                    : 0.1f;
            float currentAcceleration =
                DefaultAccelerationField?.GetValue(enemyAgent) is float reflectedAcceleration
                    ? reflectedAcceleration
                    : navAgent != null
                        ? navAgent.acceleration
                        : 0f;
            original = new NavValues(
                enemyAgent,
                currentSpeed,
                currentAcceleration);
            NavOriginals[key] = original;
        }

        float speed = Mathf.Max(0.1f, original.DefaultSpeed * _multiplier);
        float acceleration = Mathf.Max(0f, original.DefaultAcceleration * _multiplier);
        DefaultSpeedField?.SetValue(enemyAgent, speed);
        DefaultAccelerationField?.SetValue(enemyAgent, acceleration);
        if (navAgent != null)
        {
            navAgent.speed = speed;
            navAgent.acceleration = acceleration;
        }
        return true;
    }

    private static bool ApplyRigidbody(EnemyRigidbody enemyRigidbody)
    {
        if (!CanApply(enemyRigidbody))
        {
            return false;
        }
        int key = enemyRigidbody.GetInstanceID();
        if (!RigidbodyOriginals.TryGetValue(key, out RigidbodyValues original))
        {
            original = new RigidbodyValues(
                enemyRigidbody,
                enemyRigidbody.positionSpeedIdle,
                enemyRigidbody.positionSpeedChase,
                enemyRigidbody.rotationSpeedIdle,
                enemyRigidbody.rotationSpeedChase);
            RigidbodyOriginals[key] = original;
        }

        enemyRigidbody.positionSpeedIdle =
            Mathf.Max(0.1f, original.PositionSpeedIdle * _multiplier);
        enemyRigidbody.positionSpeedChase =
            Mathf.Max(0.1f, original.PositionSpeedChase * _multiplier);
        enemyRigidbody.rotationSpeedIdle =
            Mathf.Max(0.1f, original.RotationSpeedIdle * _multiplier);
        enemyRigidbody.rotationSpeedChase =
            Mathf.Max(0.1f, original.RotationSpeedChase * _multiplier);
        return true;
    }

    private static bool CanApply(Component component) =>
        Active &&
        SemiFunc.IsMasterClientOrSingleplayer() &&
        component != null &&
        component.gameObject != null &&
        component.gameObject.scene.IsValid();

    private readonly struct NavValues
    {
        internal NavValues(
            EnemyNavMeshAgent agent,
            float defaultSpeed,
            float defaultAcceleration)
        {
            Agent = agent;
            DefaultSpeed = defaultSpeed;
            DefaultAcceleration = defaultAcceleration;
        }

        internal EnemyNavMeshAgent Agent { get; }
        internal float DefaultSpeed { get; }
        internal float DefaultAcceleration { get; }
    }

    private readonly struct RigidbodyValues
    {
        internal RigidbodyValues(
            EnemyRigidbody agent,
            float positionSpeedIdle,
            float positionSpeedChase,
            float rotationSpeedIdle,
            float rotationSpeedChase)
        {
            Agent = agent;
            PositionSpeedIdle = positionSpeedIdle;
            PositionSpeedChase = positionSpeedChase;
            RotationSpeedIdle = rotationSpeedIdle;
            RotationSpeedChase = rotationSpeedChase;
        }

        internal EnemyRigidbody Agent { get; }
        internal float PositionSpeedIdle { get; }
        internal float PositionSpeedChase { get; }
        internal float RotationSpeedIdle { get; }
        internal float RotationSpeedChase { get; }
    }
}

internal static class EnemySpeedPatches
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(EnemyNavMeshAgent), "Awake")]
    private static void EnemyNavMeshAgentAwakePostfix(EnemyNavMeshAgent __instance)
    {
        EnemySpeedRuntime.ApplyNewNav(__instance);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(EnemyNavMeshAgent), "OnEnable")]
    private static void EnemyNavMeshAgentOnEnablePostfix(EnemyNavMeshAgent __instance)
    {
        EnemySpeedRuntime.ApplyNewNav(__instance);
    }

    [HarmonyPrefix]
    [HarmonyBefore(EliteEnemyVariantsCompatibility.PluginGuid)]
    [HarmonyPatch(typeof(EnemyNavMeshAgent), nameof(EnemyNavMeshAgent.OverrideAgent))]
    private static void EnemyNavMeshAgentOverrideAgentPrefix(
        EnemyNavMeshAgent __instance,
        ref float speed,
        ref float acceleration)
    {
        EnemySpeedRuntime.ApplyNavOverride(__instance, ref speed, ref acceleration);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(EnemyRigidbody), "Awake")]
    private static void EnemyRigidbodyAwakePostfix(EnemyRigidbody __instance)
    {
        EnemySpeedRuntime.ApplyNewRigidbody(__instance);
    }

    [HarmonyPrefix]
    [HarmonyBefore(EliteEnemyVariantsCompatibility.PluginGuid)]
    [HarmonyPatch(typeof(EnemyRigidbody), nameof(EnemyRigidbody.OverrideFollowPosition))]
    private static void EnemyRigidbodyOverrideFollowPositionPrefix(
        EnemyRigidbody __instance,
        ref float speed)
    {
        EnemySpeedRuntime.ApplyRigidbodyOverride(__instance, ref speed);
    }

    [HarmonyPrefix]
    [HarmonyBefore(EliteEnemyVariantsCompatibility.PluginGuid)]
    [HarmonyPatch(typeof(EnemyRigidbody), nameof(EnemyRigidbody.OverrideFollowRotation))]
    private static void EnemyRigidbodyOverrideFollowRotationPrefix(
        EnemyRigidbody __instance,
        ref float speed)
    {
        EnemySpeedRuntime.ApplyRigidbodyOverride(__instance, ref speed);
    }
}
