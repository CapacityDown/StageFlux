using UnityEngine;

namespace REPOJP.StagePhysicsEvents;

/// <summary>
/// Optional integration surface for host-side mods such as RoleShuffle.
/// Callers should resolve this type lazily and continue without it when
/// Stage Flux is absent.
/// </summary>
public static class StageFluxCompatibilityApi
{
    public const int ApiVersion = 2;

    /// <summary>
    /// Marks an implementation-only carrier so Stage Flux never treats it as
    /// a normal stage object or event target.
    /// </summary>
    public static bool RegisterInternalCarrier(GameObject? carrier)
    {
        if (carrier == null)
        {
            return false;
        }

        try
        {
            if (!StagePhysicsEffectCarrierUtility.IsInternalCarrier(carrier.transform))
            {
                carrier.AddComponent<StagePhysicsEffectCarrierMarker>();
            }
            return true;
        }
        catch (System.Exception exception)
        {
            StagePhysicsEventsPlugin.ModLogger?.LogDebug(
                $"External internal-carrier registration failed open: {exception.Message}");
            return false;
        }
    }

    /// <summary>
    /// Backward-compatible alias for RegisterInternalCarrier.
    /// </summary>
    public static bool MarkInternalCarrier(GameObject? carrier) =>
        RegisterInternalCarrier(carrier);

    /// <summary>
    /// Returns whether a component belongs to a registered internal carrier.
    /// </summary>
    public static bool IsInternalCarrier(Component? component)
    {
        if (component == null)
        {
            return false;
        }

        try
        {
            return StagePhysicsEffectCarrierUtility.IsInternalCarrier(component);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Registers an external chat/TTS notification whose generated voice must
    /// not attract enemies. Ordinary microphone input remains detectable.
    /// </summary>
    public static bool SuppressNotificationEnemyReaction(
        PlayerAvatar? player,
        string? message)
    {
        try
        {
            return NotificationEnemyReactionGuard.ExpectExternal(player, message);
        }
        catch (System.Exception exception)
        {
            StagePhysicsEventsPlugin.ModLogger?.LogDebug(
                $"External notification suppression failed open: {exception.Message}");
            return false;
        }
    }

    /// <summary>
    /// Backward-compatible alias for external notification registration.
    /// </summary>
    public static bool ExpectNotification(PlayerAvatar? player, string? message) =>
        SuppressNotificationEnemyReaction(player, message);

    /// <summary>
    /// Cancels a previously registered external notification suppression.
    /// </summary>
    public static void CancelNotificationEnemyReactionSuppression(PlayerAvatar? player)
    {
        try
        {
            NotificationEnemyReactionGuard.CancelExternalExpected(player);
        }
        catch (System.Exception exception)
        {
            StagePhysicsEventsPlugin.ModLogger?.LogDebug(
                $"External notification suppression cancellation failed open: {exception.Message}");
        }
    }

    /// <summary>
    /// Backward-compatible alias for external notification cancellation.
    /// </summary>
    public static void CancelExpectedNotification(PlayerAvatar? player) =>
        CancelNotificationEnemyReactionSuppression(player);

    /// <summary>
    /// Clears every notification suppression registered through this external
    /// API without cancelling Stage Flux's own active notifications.
    /// </summary>
    public static void ClearNotificationSoundSuppression()
    {
        try
        {
            NotificationEnemyReactionGuard.ClearExternalSuppressions();
            NotificationWindowCoordinator.ClearExternalReservations();
        }
        catch (System.Exception exception)
        {
            StagePhysicsEventsPlugin.ModLogger?.LogDebug(
                $"External notification suppression cleanup failed open: {exception.Message}");
        }
    }

    /// <summary>
    /// Backward-compatible alias that clears only external notification state.
    /// </summary>
    public static void ClearNotifications() =>
        ClearNotificationSoundSuppression();

    /// <summary>
    /// Returns the Stage Flux value multiplier currently applied to a valuable.
    /// The query never changes the valuable or event state.
    /// </summary>
    public static float GetValuableValueMultiplier(ValuableObject? valuable)
    {
        try
        {
            StagePhysicsEventController? controller =
                StagePhysicsEventsPlugin.Instance?.Controller;
            return controller?.GetValuableValueMultiplier(valuable) ?? 1f;
        }
        catch (System.Exception exception)
        {
            StagePhysicsEventsPlugin.ModLogger?.LogDebug(
                $"Valuable multiplier query failed open: {exception.Message}");
            return 1f;
        }
    }

    /// <summary>
    /// Returns whether Stage Flux will handle the player's current death through
    /// Second Chance. This query never schedules or consumes a revival.
    /// </summary>
    public static bool WillHandleSecondChance(PlayerAvatar? player)
    {
        try
        {
            StagePhysicsEventController? controller =
                StagePhysicsEventsPlugin.Instance?.Controller;
            return controller?.WillHandleSecondChance(player) == true;
        }
        catch (System.Exception exception)
        {
            StagePhysicsEventsPlugin.ModLogger?.LogDebug(
                $"Second Chance ownership query failed open: {exception.Message}");
            return false;
        }
    }

    /// <summary>
    /// Returns whether a Stage Flux notification or a valid shared reservation
    /// currently occupies the notification channel.
    /// </summary>
    public static bool IsNotificationBusy()
    {
        try
        {
            return NotificationWindowCoordinator.IsBusy();
        }
        catch (System.Exception exception)
        {
            StagePhysicsEventsPlugin.ModLogger?.LogDebug(
                $"Notification busy query failed open: {exception.Message}");
            return false;
        }
    }

    /// <summary>
    /// Atomically reserves the shared notification channel for an external owner.
    /// </summary>
    public static bool TryReserveNotificationWindow(string? owner, float seconds)
    {
        try
        {
            return NotificationWindowCoordinator.TryReserveExternal(owner, seconds);
        }
        catch (System.Exception exception)
        {
            StagePhysicsEventsPlugin.ModLogger?.LogDebug(
                $"Notification reservation failed closed: {exception.Message}");
            return false;
        }
    }
}
