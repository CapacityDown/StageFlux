# Stage Flux Compatibility API

Stage Flux v4.2.6 exposes the optional public type
`REPOJP.StagePhysicsEvents.StageFluxCompatibilityApi`.

External mods must resolve this type lazily and continue normally when Stage Flux
is absent. Do not add a required assembly reference or hard dependency.

## API version

```csharp
public const int ApiVersion = 2;
```

## Internal carriers

```csharp
public static bool RegisterInternalCarrier(GameObject carrier);
public static bool MarkInternalCarrier(GameObject carrier);
public static bool IsInternalCarrier(Component component);
```

`RegisterInternalCarrier` prevents Stage Flux from treating an implementation-only
object as a normal physical stage target. Register the object immediately after it
is created and before it can enter Stage Flux's target scan.

## Notification-only enemy-reaction suppression

```csharp
public static bool SuppressNotificationEnemyReaction(
    PlayerAvatar player,
    string message);
public static bool ExpectNotification(PlayerAvatar player, string message);
public static void CancelNotificationEnemyReactionSuppression(
    PlayerAvatar player);
public static void CancelExpectedNotification(PlayerAvatar player);
public static void ClearNotificationSoundSuppression();
public static void ClearNotifications();
public static bool IsNotificationBusy();
public static bool TryReserveNotificationWindow(string owner, float seconds);
```

Register immediately before sending the matching chat/TTS notification. Cancel it
if notification delivery fails. The suppression covers the generated notification
voice while preserving enemy reactions to ordinary microphone input and other
world sounds. `ClearNotificationSoundSuppression` removes all suppression entries
registered through this external API, but does not remove Stage Flux's own active
notification entries.

Notification reservations are atomic, expire automatically, and are cleared when
the stage ends. Repeating a reservation with the same owner refreshes its expiry.
Stage Flux uses this same coordinator for its own pending and active TTS, so an
external reservation and a Stage Flux announcement cannot claim the window at the
same time.

## Value and revival queries

```csharp
public static float GetValuableValueMultiplier(ValuableObject valuable);
public static bool WillHandleSecondChance(PlayerAvatar player);
```

`GetValuableValueMultiplier` returns only the active multiplier that Stage Flux
actually applied to that valuable. It returns `1` for unknown, excluded, null, or
inactive targets and never changes the valuable or event state.

`WillHandleSecondChance` is a read-only ownership query for the player's current
death. It returns false when Second Chance cannot be consumed or when RoleShuffle
reports a pending Bodyguard corrective revival.

## RoleShuffle

Stage Flux lazily resolves
`REPOJP.StageRoles.RoleShuffleCompatibilityApi` from the optional
`REPOJP.RoleShuffle` plugin. The current contract accepts `ApiVersion == 2` and
validates each public static method's exact parameter and return types. A missing
or incompatible method uses its individual safe fallback. A missing, unreadable,
older, or newer API version disables the optional bridge and preserves the
original Stage Flux behavior.

The v4.3.3 integration registers Enemy Purge damage as non-player damage, reports
successful Second Chance revivals, gives Bodyguard corrective revivals priority,
and excludes Engineer-suppressed dangerous valuables both during candidate
selection and immediately before activation. Role and decoy query methods are
resolved for future target-selection use but are not shown in the Stage Flux HUD.

## Elite Enemy Variants

Stage Flux resolves
`REPOJP.EliteEnemyVariants.EliteEnemyVariantsCompatibilityApi` only when Enemy Wave
needs to notify an external spawn. It calls `NotifyExternalSpawn(EnemyParent)`
immediately after the vanilla enemy activation call. The current contract accepts
`ApiVersion == 1` and requires the exact public static signature
`bool NotifyExternalSpawn(EnemyParent)`. Missing, unreadable, older, newer, or
incompatible APIs disable only the EEV bridge; Enemy Wave itself continues.

Stage Flux reserves each Enemy Wave candidate before its staged activation and
records the external notification per active lease. This prevents concurrent
replenishment from notifying EEV more than once for one lease. EEV's own selection
remains idempotent as a second line of defense.

The integration does not read EEV configuration, selection state, random seeds,
enemy counts, enhanced counts, or Threat Budget. EEV remains the sole owner of its
selection and presentation. When EEV or its API is absent, Enemy Wave follows the
existing Stage Flux path.

Stage Flux applies its speed factor before EEV's Harmony prefixes. EEV then applies
its own factor once. Stage Flux stores only the values that existed before its
event, refreshes from those values instead of stacking, and restores those values
when the event ends; EEV's dynamic multiplier remains under EEV's control.
