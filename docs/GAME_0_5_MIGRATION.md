# Stage Flux 5.0.0 — R.E.P.O. 0.5.0 migration

## Scope

The unreleased 4.3.0 content is carried forward into 5.0.0: all 46 events, Events menu, event settings/presets, HUD editor, reports, icons, and optional integrations. The plugin/configuration identity remains `REPOJP.StagePhysicsEvents`; configuration defaults, migration revision, event flags and optional compatibility API version 2 are unchanged. No RoleShuffle or Elite Enemy Variants source was modified.

## Verified target

- Installed game version: `v0.5.0` (game version cache).
- Steam build: `26294889`.
- UnityPlayer: `6000.3.17f1`.
- Original `Assembly-CSharp.dll` SHA-256: `AA6B1CE0DEEF8D2B7104A3722864C4BA94C107257726471F8E44B45C99E36888`.
- Official UI dependencies: REPOModding MenuLib 2.6.0 and REPOConfig 1.2.7. Their plugin identities are unchanged after the package-author transfer.

## Implemented adaptations

- Build references now use the installed game/Unity/Photon DLLs. Old 0.4.x game and Unity 2022 NuGet references are removed. Only a generated intermediate reference is publicized; installed game DLLs are never rewritten.
- Rigidbody access uses `linearVelocity`; scene enumeration uses `FindObjectsByType` with the original InstanceID ordering; TMP uses `textWrappingMode`.
- Battery Drain uses the game's `BatteryFullPercentChange` path, including exact charge snapshots, saved battery state and vanilla-client synchronization. The former two-argument reflection target no longer exists.
- Battery Charge uses `ReceiveCharge` plus native synchronization. It no longer estimates a per-tick amount through a frame-dependent continuous-charge rate.
- Zero Gravity and Roll send the new six-value `CreateAffectsRPC` payload: target IDs, durations, game-issued effect IDs, network end times, position and direction. Singleplayer supplies both new arrays as well. Effect IDs come from the vanilla allocator, not a mod-local counter.
- Traffic Shock uses the host-authoritative `TumbleSet` path before applying forces. The old tumble-request RPC omitted the newly required state revision.
- Player Swap compiles against the new Spawn signature (including the default false debug argument).
- Notifications explicitly choose `en-US` through the new localized chat API. Enemy-reaction filtering still isolates notification TTS and retains normal microphone investigation.
- Events scrolling uses the new native `scrollWheelStep` setting. The old IL-pattern replacement is removed; native mouse/gamepad navigation remains in control.
- MenuLib's minimum plugin dependency is 2.6.0. This prevents loading against the old library's incompatible MenuEffectClick call.

Host-only operation continues to use vanilla game RPCs. No new client-required RPC, direct RoleShuffle/EEV reference, mandatory integration dependency or extra integration HUD information was added.

## Automated verification — 2026-11-03

- Full Release rebuild with warnings treated as errors: **0 warnings, 0 errors**.
- Regression/API audit: **1,255 checks passed**, including the actual Default-profile MenuLib/REPOConfig binaries.
- Checks cover all 46 event flags/descriptions, existing arithmetic and authority rules, embedded resources, release identity, absence of mandatory optional-mod references, compiled game/Unity/Photon/MenuLib references, reflection member existence, Harmony target/argument names, and explicit vanilla RPC parameter contracts.
- Build, package and Default-profile `StagePhysicsEvents.dll` match:
  `CF3E0DA4E83B0998B7526024D6805CE28F57CE95C8D0BAE463826C5CC6A1D4F0`.
- Default's MenuLib/REPOConfig already matched the downloaded official versions; they were verified without replacing them. No configuration file was edited during deployment.
- Previous package and deployed Stage Flux DLLs are backed up locally under `output/before-v5-deploy-20261103-173224/` (not committed or distributed).

These are static and offline checks, not proof of successful Unity gameplay or multiplayer synchronization. No game session was launched as part of this migration.

## Remaining in-game acceptance tests

1. Launch on R.E.P.O. 0.5.0, inspect BepInEx logs, open Events/REPOConfig, and verify all 46 entries. Check menu clicks, scrolling, HUD editor and existing saved settings.
2. Test host-only multiplayer with an unmodded participant: Zero Gravity/Roll must start, expire and reapply after revival correctly; Traffic Shock must tumble the remote player without RPC errors.
3. Test Battery Charge/Drain at configured amounts and minimum/maximum charge, for both host-held and guest-held items. Check host/guest battery visuals and saved charge.
4. Test Player Swap, Second Chance, chat/countdown ordering, English speech, and normal microphone/noise reactions during notification suppression.
5. Exercise all prefab-based events, especially removed-item orbs, mines, grenade projectiles, Levitation, Spider Scare, Healing Aura and Star Barrage. Offline API checks cannot prove asset resolution or effect visuals.
6. With game-compatible optional mods, verify RoleShuffle notification/revival/valuable integrations and EEV Enemy Wave/speed composition. Repeat without those mods.
7. Check a stage transition, departure/rejoin and event end for orphaned carriers, stale notifications and HUD state.

Do not label these gameplay checks as passed until observed in-game.
