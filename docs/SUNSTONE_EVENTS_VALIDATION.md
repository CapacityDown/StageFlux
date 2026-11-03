# Sunstone event additions - Stage Flux 5.0.0

## Scope

- Added Recharge Cloud, Rockfall, and Arcane Burst; 49 events in total.
- Added Snake Can and Explosive Tower switches to Dangerous Valuables. No valuable spawning or end-of-event restoration was added.
- Added individual Human Gun, Human Adventure Cable, and Recharge Orb switches to Supply Drop. Existing per-stage spawn budget is unchanged.
- Added Pony selection to Enemy Wave when an initialized dormant Pony exists on the current stage. External spawn notification remains guarded once per lease.
- No other mod source was changed. RoleShuffle and Elite Enemy Variants remain optional, reflection-only integrations. No custom client RPC was added.

## Native synchronization

- Recharge Cloud: standard energy-crystal prefab and `ChargeCloudStartRPC(float)`. Only Stage Flux charging sources filter internal carriers from the standard battery-charging loop; normal crystals remain unchanged.
- Arcane Burst: standard wizard-crystal prefab, `CrystalDestroyedEffectRPC(int[], float[], float[], float[], float[], Vector3)`, and `DestroyObjectRPC(bool)` for vanilla burst visuals. Only the event carrier's host-side unfiltered listener is removed. Target categories, hierarchical internal markers, and Trickster decoys are respected.
- Player Roll duration accounts for the native torque effect doubling its timer. Existing native Roll effects are not reapplied. Ordinary expiration uses native effect timers, as with the existing Roll event.
- Rockfall: requires scene-owned RockSlide sources and their standard rock prefab. Native warning and LaunchRPC are used. The adapter owns all event spawn coroutines and tracks only event-created rocks; natural rockslides are not stopped or deleted.
- Snake Can: native opening method with a brief host-side retract delay scoped to event-triggered cans. Engineer eligibility is checked before activation. Standard lid animation, hit window, and later closing remain native.
- Explosive Tower: native trigger state; players may still cancel by pulling the handle back.

## Safety and lifecycle

- All new events default enabled with the existing 6% chance.
- Integer wave intervals: 1-300 seconds. Per-wave count and concurrent-source limits: 1-30.
- New sources require at least the configured player separation; no relaxed-distance result is accepted.
- Rockfall is excluded when no compatible rockslide source is present.
- Temporary crystals have zero value before the normal value-initialization coroutine runs.
- Generation checks invalidate old waves; the pending reservation is released only for its own generation.
- Cleanup uses the existing frame-budgeted queue. Charging stops on the host immediately at event stop.
- Arcane Burst protects only affected valuables when its protection switch and Targets.Valuables are both enabled. Rockfall uses the existing whole-event valuable protection.
- Rockfall and Arcane Burst participate in dangerous-combination checks. Recharge Cloud and Battery Drain are always mutually exclusive.

## Verification completed

- Release full rebuild against the installed R.E.P.O. 0.5.0 assembly: 0 warnings, 0 errors.
- 2,886 offline checks passed: arithmetic, menu masks, guide coverage, embedded icons, integration wiring, installed game references, Harmony targets/parameters, native RPC signatures, plugin version, and optional dependency exclusions.
- Generated transparent 512 x 512 icons inspected and embedded. Prompts and master sources are recorded in `Assets/EventIcons/v5.0.0-generation.json`.
- Build, package, and SF_TEST deployment DLL SHA-256 match:

  `1143E1DDC85956547071228BF35A0FB4B766D6A3D72497E5555C3C0D3BEBA864`

- Deployed to SF_TEST at the user's request on 2026-11-03. The empty profile was initialized with BepInExPack, MenuLib, and REPOConfig; no Default user configuration was copied. Stage Flux is installed in `SF_TEST/BepInEx/plugins/CapackMods-StageFlux/StagePhysicsEvents.dll`. Default and the running EEV_TEST profile were not modified. The original SF_TEST package list and prior package DLL are retained under `output/deploy-backups/`. In-game checks remain pending.

## Runtime checks still required

These have not been executed in a Unity game session; offline contract tests do not prove prefab availability or multiplayer visuals.

1. Host plus unmodded guest: charging cloud visuals and actual battery gain; ordinary crystals and player-owned Recharge Orbs unchanged.
2. Arcane Burst: object and player Roll, native camera steering, category exclusions, no stacking, timer expiry, and valuable-protection delay.
3. Rockfall on a supported stage: advance warning, visible rocks, collision damage, player separation, cap, and event-only cleanup. Confirm exclusion on a stage with no rockslide source.
4. Dangerous Valuables: snake-can opening visible to guests; tower trigger and cancellation; no added valuables; Engineer-held objects remain exempt.
5. Supply Drop: each new item usable by vanilla guests; individual category switches, duplicates, per-stage budget, and persistence after event end.
6. Enemy Wave: Pony eligibility and switch, despawn option, and no duplicate EEV lease notification.
7. Persistent mode, early stage exit, host loss, low/high latency, and modded guests: no waves leaking into a later event or stage.
