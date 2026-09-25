using System;
using System.Collections.Generic;
using UnityEngine;

namespace REPOJP.StagePhysicsEvents;

internal static class NotificationWindowCoordinator
{
    private const string StageFluxOwner = "REPOJP.StageFlux.Internal";
    private const float MinimumReservationSeconds = 0.25f;
    private const float MaximumReservationSeconds = 60f;
    private const int MaximumOwnerLength = 128;
    private static readonly object Sync = new();
    private static readonly Dictionary<string, Reservation> Reservations =
        new(StringComparer.Ordinal);

    private sealed class Reservation
    {
        internal Reservation(float expiresAt, bool stageFluxOwned)
        {
            ExpiresAt = expiresAt;
            StageFluxOwned = stageFluxOwned;
        }

        internal float ExpiresAt { get; set; }
        internal bool StageFluxOwned { get; }
    }

    internal static bool IsBusy()
    {
        lock (Sync)
        {
            RemoveExpiredReservations();
            return Reservations.Count > 0 ||
                   NotificationEnemyReactionGuard.HasActiveNotifications();
        }
    }

    internal static bool TryReserveExternal(string? owner, float seconds) =>
        TryReserve(owner, seconds, false, false);

    internal static bool TryReserveStageFlux(float seconds) =>
        TryReserve(StageFluxOwner, seconds, true, false);

    internal static bool TryReserveStageFluxCountdown(float seconds) =>
        TryReserve(StageFluxOwner, seconds, true, true);

    internal static void ReleaseStageFluxReservation()
    {
        lock (Sync)
        {
            Reservations.Remove(StageFluxOwner);
        }
    }

    internal static void ClearExternalReservations()
    {
        lock (Sync)
        {
            List<string> keys = new();
            foreach (KeyValuePair<string, Reservation> pair in Reservations)
            {
                if (!pair.Value.StageFluxOwned)
                {
                    keys.Add(pair.Key);
                }
            }
            foreach (string key in keys)
            {
                Reservations.Remove(key);
            }
        }
    }

    internal static void ClearAll()
    {
        lock (Sync)
        {
            Reservations.Clear();
        }
    }

    private static bool TryReserve(
        string? owner,
        float seconds,
        bool stageFluxOwned,
        bool allowStageFluxActiveNotifications)
    {
        if (string.IsNullOrWhiteSpace(owner) ||
            float.IsNaN(seconds) ||
            float.IsInfinity(seconds))
        {
            return false;
        }

        string normalizedOwner = owner.Trim();
        if (normalizedOwner.Length > MaximumOwnerLength)
        {
            normalizedOwner = normalizedOwner.Substring(0, MaximumOwnerLength);
        }
        float safeSeconds = Mathf.Clamp(
            seconds,
            MinimumReservationSeconds,
            MaximumReservationSeconds);

        lock (Sync)
        {
            RemoveExpiredReservations();
            if (Reservations.TryGetValue(normalizedOwner, out Reservation existing))
            {
                if (existing.StageFluxOwned != stageFluxOwned)
                {
                    return false;
                }
                existing.ExpiresAt = Time.realtimeSinceStartup + safeSeconds;
                return true;
            }

            if (Reservations.Count > 0)
            {
                return false;
            }

            bool notificationConflict = allowStageFluxActiveNotifications
                ? NotificationEnemyReactionGuard.HasActiveExternalNotifications()
                : NotificationEnemyReactionGuard.HasActiveNotifications();
            if (notificationConflict)
            {
                return false;
            }

            Reservations[normalizedOwner] = new Reservation(
                Time.realtimeSinceStartup + safeSeconds,
                stageFluxOwned);
            return true;
        }
    }

    private static void RemoveExpiredReservations()
    {
        float now = Time.realtimeSinceStartup;
        List<string>? expired = null;
        foreach (KeyValuePair<string, Reservation> pair in Reservations)
        {
            if (now < pair.Value.ExpiresAt)
            {
                continue;
            }
            expired ??= new List<string>();
            expired.Add(pair.Key);
        }
        if (expired == null)
        {
            return;
        }
        foreach (string key in expired)
        {
            Reservations.Remove(key);
        }
    }
}
