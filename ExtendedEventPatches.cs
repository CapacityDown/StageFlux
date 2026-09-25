using System;
using System.Collections;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;

namespace REPOJP.StagePhysicsEvents;

internal static class ExtendedEventPatches
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(EnemyHealth), nameof(EnemyHealth.Hurt))]
    private static void EnemyHurtPrefix(EnemyHealth __instance, ref int _damage)
    {
        try
        {
            if (ExtendedEventAdapter.Active is { } active && ExtendedEventAdapter.IsStageObject(__instance))
                _damage = ExtendedEventPolicy.EnemyDamage(_damage, active.EnemyDamagePercent);
        }
        catch (Exception exception) { LogFailure(exception); }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(EnemyVision), "Vision")]
    private static void VisionPostfix(EnemyVision __instance, ref IEnumerator __result)
    {
        __result = ScaledVision(__instance, __result);
    }

    private static IEnumerator ScaledVision(EnemyVision vision, IEnumerator original)
    {
        try
        {
            while (vision != null)
            {
                float multiplier = ExtendedEventAdapter.Active?.VisionMultiplier ?? 1f;
                if (!ExtendedEventAdapter.IsStageObject(vision))
                    multiplier = 1f;
                float distance = vision.VisionDistance;
                float close = vision.VisionDistanceClose;
                float crouch = vision.VisionDistanceCloseCrouch;
                bool next;
                try
                {
                    vision.VisionDistance = distance * multiplier;
                    vision.VisionDistanceClose = close * multiplier;
                    vision.VisionDistanceCloseCrouch = crouch * multiplier;
                    next = original.MoveNext();
                }
                finally
                {
                    // Restore after every coroutine step, including exceptions. Never
                    // persist our multiplier over EEV/other mods' current base values.
                    if (vision != null)
                    {
                        vision.VisionDistance = distance;
                        vision.VisionDistanceClose = close;
                        vision.VisionDistanceCloseCrouch = crouch;
                    }
                }
                if (!next)
                    yield break;
                yield return original.Current;
            }
        }
        finally { (original as IDisposable)?.Dispose(); }
    }

    internal readonly struct HealthSnapshot
    {
        internal HealthSnapshot(PlayerHealth health)
        {
            Health = ExtendedEventAdapter.HealthValue?.GetValue(health) is int current ? current : 0;
            Maximum = ExtendedEventAdapter.HealthMaximum?.GetValue(health) is int maximum ? maximum : 0;
            Ready = ExtendedEventAdapter.HealthReady?.GetValue(health) is true;
        }
        internal int Health { get; }
        internal int Maximum { get; }
        internal bool Ready { get; }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(PlayerHealth), nameof(PlayerHealth.Hurt))]
    private static void HealthPrefix(PlayerHealth __instance, out HealthSnapshot __state)
    {
        __state = default;
        try
        {
            if (ExtendedEventAdapter.Active?.Has(StageEffect.SharedPain) == true)
                __state = new HealthSnapshot(__instance);
        }
        catch (Exception exception) { LogFailure(exception); }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(PlayerHealth), nameof(PlayerHealth.UpdateHealthRPC))]
    private static void HealthRpcPrefix(PlayerHealth __instance, out HealthSnapshot __state) =>
        HealthPrefix(__instance, out __state);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PlayerHealth), nameof(PlayerHealth.Hurt))]
    private static void HurtPostfix(PlayerHealth __instance, bool hurtByHeal, HealthSnapshot __state)
    {
        Record(__instance, __state, !hurtByHeal);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PlayerHealth), nameof(PlayerHealth.UpdateHealthRPC))]
    private static void HealthRpcPostfix(PlayerHealth __instance, bool effect, bool hurtByHeal,
        PhotonMessageInfo _info, HealthSnapshot __state)
    {
        // Only record accepted owner/host updates; initialization and maximum-health
        // changes are not damage, and hurtByHeal includes our own propagated hits.
        PhotonView? view = __instance.GetComponent<PhotonView>();
        bool accepted = AcceptHealthUpdate(view, _info);
        Record(__instance, __state, accepted && effect && !hurtByHeal);
    }

    private static bool AcceptHealthUpdate(PhotonView? view, PhotonMessageInfo info) =>
        !SemiFunc.IsMultiplayer() || (view != null && info.Sender != null &&
            (info.Sender == view.Owner || info.Sender.IsMasterClient));

    private static void Record(PlayerHealth health, HealthSnapshot snapshot, bool eligible)
    {
        try
        {
            ExtendedEventAdapter.Active?.RecordHealthLoss(health, snapshot.Health, snapshot.Maximum, snapshot.Ready, eligible);
        }
        catch (Exception exception) { LogFailure(exception); }
    }

    private static bool _failureLogged;
    private static void LogFailure(Exception exception)
    {
        if (_failureLogged)
            return;
        _failureLogged = true;
        StagePhysicsEventsPlugin.ModLogger.LogWarning($"An optional event callback was skipped: {exception.GetBaseException().Message}");
    }
}
