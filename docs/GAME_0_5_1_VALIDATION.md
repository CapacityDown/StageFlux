# R.E.P.O. 0.5.1 compatibility — Stage Flux 5.0.0

## Verified installation — 2026-11-05

- Installed PlayerSettings `bundleVersion`: `0.5.1` (read from the original
  `globalgamemanagers`, not the previous session's stale `Cache/version.txt`).
- Steam build ID: `26407089`.
- UnityPlayer file version: `6000.3.17.13566802` (6000.3.17f1).
- Original `Assembly-CSharp.dll` SHA-256:
  `7DF0FF1D03755A71A6755D01FC4C58C3CC044F8AA740F3D1C1379A6D6BD66724`.
- Existing MenuLib 2.6.0 and REPOConfig 1.2.7 pass installed-game member checks;
  no library replacement or dependency change is needed.

The [official patch announcement](https://steamstore-a.akamaihd.net/news/externalpost/steam_community_announcements/1848664643340421)
was also checked through Steam's official news API. The local game data above is
the build target; the game has not been launched merely to refresh its cache.

## Changes and audit findings

- Retain Stage Flux **5.0.0**, plugin/config identity, all 49 events, current
  settings/defaults, host-only operation, and optional RoleShuffle/EEV integration.
- Rebuild against the installed 0.5.1 assemblies and update development and
  player-facing support documentation. No game or other MOD source is changed.
- Door Chaos had a stale one-argument reflection invocation of
  `PhysGrabHinge.CloseImpulse`; the current native signature requires `heavy`
  and `_sound` booleans. It already had that signature in the available 0.5.0
  source snapshot, so this is a pre-existing migration gap, not a new 0.5.1 break.
  The old call could throw before the closing torque ran. Use compile-checked
  native OpenImpulse/CloseImpulse calls, with a light close and its normal sound.
  Native host-authorized RPC propagation and configured angular force remain.
- Compared PlayerVoiceChat against the available 0.5.0 snapshot. The 0.5.1 mute
  fix is in native TTS playback volume gating. Stage Flux does not replace that
  logic, change recorder transmission, or mute the TTS AudioSource. Its separate
  microphone investigation path and notification-only suppression remain intact.
- PlayerAvatar chat RPC payload remains unchanged; additional native player-name,
  enemy registration and null checks do not require a Stage Flux override.
- Existing Zero Gravity/Roll, Recharge Cloud/Rockfall/Arcane Burst, revival,
  chat, battery and menu contracts still resolve against original game assemblies.
  No client-required custom RPC or new mandatory optional-mod dependency is added.

## Automated checks

- Main Release build **246**: **0 warnings, 0 errors**.
- Regression/metadata/resources: **11,127 checks passed**.
- Native wheel harness: **269 checks passed**, including both peer patch orders.
- Added full reflected method signatures and field-type checks, rather than
  relying only on member names. Door Chaos checks inspect the compiled calls,
  arguments, retained angular-force operation, and native master-only RPCs.
- Build/package/SF_TEST DLL SHA-256:
  `41CCD3DCC9AA758E3D8469AD77A9530D9C0F079FAF4CC6DB531919C4FEA3A499`.
- Deployed to SF_TEST with the game stopped; configuration SHA-256 remained
  `81E1CE9613CEE5BDC9124DC58EBBF32E0C92776834D67597593EA93FCECE709F`.
  Previous DLLs and profile documents were retained in the local, untracked
  `output/deploy-backups/repo-051-20261105-225633` directory. Default and other
  profiles were not changed. No release ZIP or public release was created.

These are static and offline results. They do not establish live Unity gameplay,
visual correctness, or unmodded-client synchronization by themselves.

## Live acceptance checks still required

1. Open Events and REPOConfig, scroll, change the game language and verify the
   event guide, host settings and HUD layout remain usable.
2. Exercise Door Chaos for several open/close cycles on large doors and enabled
   hinged items. Verify both directions and absence of parameter-count errors.
3. Test host-only multiplayer announcements with an unmodded participant, while
   muted and with push-to-talk. Normal microphone/physical sounds must still
   attract enemies while notification sounds are suppressed.
4. Test native staff effects, Second Chance and the three Sunstone events across
   event end, death/revival, and stage changes, with and without optional mods.
