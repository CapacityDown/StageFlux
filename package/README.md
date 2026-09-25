# Stage Flux

## English

### Overview

Stage Flux adds 46 configurable, random stage-wide events to R.E.P.O. Events can change physics, support or endanger players, alter enemy behavior, activate stage objects, change valuable prices and fragility, and create vanilla hazards.

Only the host needs the mod for gameplay effects. Players without the mod are still affected and receive chat announcements. Players who also install the mod can use the event HUD.

### Events menu

Open **Events** in the lobby or the pause menu. If RoleShuffle is installed, the button appears below **Roles**.

- **Current Events** shows the current stage's events.
- **Event Guide** explains every event with its icon and risk level.
- **Event Settings** lets the host turn events on or off. Changes affect future draws; active events and already selected stage plans stay unchanged. Players with the mod can view the host's selection. Detailed settings remain available in REPOConfig.
- **Presets** offers Defaults, Low Risk, and All Off. Presets only change event ON/OFF.
- **Tools** includes display refresh, a draggable HUD editor with Save/Cancel, and a local problem report you can copy or open. Reports are not uploaded automatically; review them before sharing.

The HUD editor only changes your own display. It does not start events. Guests without the mod do not need this menu for gameplay effects.

### Event Catalog

All 46 events are listed below with their risk, default state, default chance, and a short description.

| Event | Risk | Default | Chance | Description |
|---|---:|---:|---:|---|
| Feather | Low | On | 6% | Makes selected stage targets lighter, easier to lift, and more buoyant. |
| Zero Gravity | Medium | On | 6% | Removes gravity from selected stage targets so they float more easily. |
| Battery Charge | Low | On | 6% | Repeatedly restores charge to battery-powered items during the event. |
| Heal | Low | On | 6% | Repeatedly restores health to every living player during the event. |
| Indestructible | Low | On | 6% | Prevents selected valuables and items from breaking during the event. |
| Fragility | High | On | 6% | Makes valuables extremely fragile, allowing light impacts to reduce their value. |
| Gumball Hypnosis | Medium | On | 6% | Adds a screen effect and pulls a holder's gaze toward the object being held. |
| Healing Aura | Low | On | 6% | Creates glowing areas at random stage locations that heal players who touch them. |
| Star Barrage | High | On | 6% | Launches dangerous star projectiles from random stage positions and directions. |
| Spider Scare | Medium | On | 6% | Triggers the vanilla spider screen effect for randomly selected players. |
| Traffic Shock | High | On | 6% | Shocks and tumbles random players using the vanilla red traffic-light effect. |
| Dangerous Valuables | High | On | 6% | Activates compatible dangerous valuables that were already placed on the stage. |
| Roll | High | Off | 6% | Violently rotates and moves targets. Players can steer toward their view direction. |
| Void | High | Off | 6% | Creates multiple dangerous voids and moves them to new random locations repeatedly. |
| Levitation | Medium | On | 6% | Creates areas at random stage locations that lift objects and players. |
| Shockwave | Medium | On | 6% | Launches knockback grenades in random directions across the stage. |
| Stun Blast | Medium | On | 6% | Launches grenades that stun nearby targets in random directions. |
| Explosion Rain | High | On | 6% | Launches explosive grenades into the stage from random positions and directions. |
| Enemy Wave | High | On | 6% | Activates 1-3 additional enemies that are available for the current stage. |
| Minefield | High | On | 6% | Places armed vanilla mines and replenishes mines that trigger or are destroyed. |
| Freeze | Medium | On | 6% | Continuously freezes targeted enemies so they cannot move during the event. |
| Stun | Medium | On | 6% | Continuously stuns compatible enemies so they cannot act during the event. |
| Enemy Warp | High | On | 6% | Teleports enemies to different valid locations across the stage. |
| Enemy Hunt | High | On | 6% | After all extractions, plays a lure sound in an occupied room and sends enemies there. |
| Enemy Speed Up | High | On | 6% | Increases movement and acceleration for all enemies, including later spawns. |
| Enemy Speed Down | Low | On | 6% | Reduces movement and acceleration for all enemies, including later spawns. |
| Enemy Regen | High | On | 6% | Gradually restores health to every enemy while the event is active. |
| Enemy Purge | Low | On | 6% | Gradually damages every enemy while the event is active. |
| Damage Pulse | High | On | 6% | Repeatedly damages every living player while the event is active. |
| Second Chance | Low | On | 6% | Revives players at their death position 2 seconds after they die during the event. |
| Knockback | Medium | On | 6% | Periodically launches every living player in a random direction. |
| Flicker | Medium | On | 6% | Repeatedly flashes a red light around every living player. |
| Quake | High | On | 6% | Shakes players and loose floor objects in random directions like an earthquake. |
| Door Chaos | Medium | On | 6% | Repeatedly opens and closes normal doors, large doors, and hinged objects. |
| Value Surge | Low | On | 6% | Raises the price of every valuable, including those in extraction, carts, and trucks. |
| Value Crash | High | On | 6% | Lowers the price of every valuable, including those in extraction, carts, and trucks. |
| Restoration | Low | On | 6% | Repairs damage to surviving valuables at regular intervals. |
| Battery Drain | Medium | On | 6% | Repeatedly drains battery-powered items. |
| Heavy Cargo | Medium | On | 6% | Makes selected valuables and items heavier to carry. |
| Butterfingers | Medium | On | 6% | Makes players periodically drop held valuables and items. |
| Enemy Blindness | Low | On | 6% | Reduces enemy sight range without removing hearing or existing pursuit. |
| Enemy Armor | High | On | 6% | Reduces damage received by enemies. |
| Enemy Vulnerability | Low | On | 6% | Increases damage received by enemies. |
| Supply Drop | Low | On | 6% | Places useful standard health packs and equipment around the stage. |
| Player Swap | Medium | On | 6% | Exchanges two eligible living players' positions. |
| Shared Pain | High | On | 6% | Shares part of one player's damage with other living players. |

### Gameplay warnings

- Roll can move enemies violently and may kill them.
- Void can kill players.
- Explosion Rain and explosive Minefield mines can kill players and damage valuables.
- Damage Pulse, Knockback, and Quake can also cause player death or valuable loss.
- Fragility values above `100%` make valuables easier to break through ordinary impacts.
- Gumball Hypnosis temporarily controls the camera and gaze of players holding applicable objects.
- Star Barrage, Traffic Shock, and Dangerous Valuables can damage or kill players and damage valuables.
- Spider Scare displays the vanilla spider screen effect and follows the game's arachnophobia setting.
- Roll and Void remain disabled by default.
- Shared Pain can endanger the whole team. Its default safety cap uses the host's latest health information; simultaneous damage can still kill players.
- Player Swap changes your location, not the stage or extraction progress. It skips players who are holding something, crouching, airborne, or tumbling, and skips obstructed destinations.
- All other events are enabled by default. Review their behavior and risks before playing.

### Requirements

- BepInExPack 5.4.2305 or a later compatible version
- REPOConfig 1.2.6 or a later compatible version
- MenuLib 2.5.2 or a later compatible version

### Installation

Install with a compatible mod manager, or place `StagePhysicsEvents.dll` in the profile's `BepInEx/plugins` directory.

### Multiplayer

- The host's settings control event selection, timing, targets, and chat announcements.
- Other players do not need the mod to receive gameplay effects.
- Modded participants receive the event HUD and can configure their own HUD layout.
- Singleplayer uses the same event behavior as hosting a multiplayer session.

### Event modes

| Value | Description |
|---|---|
| `AllMode` | Selects one of the other four modes with equal probability when the stage begins. |
| `RandomEachEvent` | Rerolls the effect combination, duration, and interval for every event. |
| `FixedForStage` | Keeps the first effect combination, duration, and interval for the entire stage. |
| `FixedPerExtraction` | Keeps duration and interval fixed for the stage, but rerolls the effect combination after each completed extraction. The new combination is used from the next event. |
| `PersistentForStage` | Keeps the selected effect combination active until the stage ends. |

### Event selection

- `StageActivationChancePercent` is rolled once when a stage begins. A failed roll disables effects, the HUD, and the stage-start announcement for that stage.
- Every enabled effect's `ChancePercent` is rolled independently. The probabilities are not added or normalized, so a total above 100% is valid.
- `MaxSimultaneousEffects` limits how many successful effects can activate together. If the limit is exceeded, the active effects are selected randomly from the successful rolls.
- When `AllowDangerousCombinations` is disabled, the selector avoids overlapping forced movement, lethal hazards, enemy pressure, control or visual disruption, and valuable-loss amplification.
- Fragility and Value Crash no longer overlap events that can physically endanger valuables. No more than two ordinary valuable-risk events can activate together.
- Freeze / Stun, Enemy Speed Up / Enemy Speed Down, Enemy Regen / Enemy Purge, Indestructible / Fragility, and Value Surge / Value Crash are always mutually exclusive.
- Feather / Heavy Cargo, Battery Charge / Battery Drain, and Enemy Armor / Enemy Vulnerability are also always mutually exclusive.
- In `RandomEachEvent`, a roll with no successful effect simply skips that event.
- In `FixedForStage`, `FixedPerExtraction`, and `PersistentForStage`, a stage whose initial effect roll selects nothing remains inactive for that stage.
- Except for `PersistentForStage`, every enabled stage begins with one full configured interval before the first event.
- In `FixedPerExtraction`, an empty reroll after an extraction produces no further effects until another extraction changes the selection.

### Configuration

All settings are available through REPOConfig. Gameplay settings use the host's values. HUD layout settings are local to each player who has the mod installed.

#### General

| Key | Default | Range / Values | Description |
|---|---:|---|---|
| `Enabled` | `true` | `true`, `false` | Enables or disables the mod. |
| `Mode` | `RandomEachEvent` | `AllMode`, `RandomEachEvent`, `FixedForStage`, `FixedPerExtraction`, `PersistentForStage` | Selects the event mode. |
| `StageActivationChancePercent` | `50` | `0`–`100` | Chance that stage events are enabled when a stage begins. Existing custom values are kept. |
| `MaxSimultaneousEffects` | `3` | `1`–`5` | Maximum number of effects that can activate in one event. Changes during a stage update the HUD immediately and apply to the next unplanned roll. |

#### Timing

All timing values are whole seconds. If a minimum is greater than its maximum, the values are treated as an ascending range without rewriting the config.

| Key | Default | Range | Description |
|---|---:|---:|---|
| `IntervalMinSeconds` | `45` | `10`–`300` | Minimum wait before the next event. |
| `IntervalMaxSeconds` | `90` | `10`–`300` | Maximum wait before the next event. |
| `EffectDurationMinSeconds` | `15` | `10`–`300` | Minimum effect duration. |
| `EffectDurationMaxSeconds` | `30` | `10`–`300` | Maximum effect duration. |

#### Safety

| Key | Default | Range / Values | Description |
|---|---:|---|---|
| `AllowDangerousCombinations` | `false` | `true`, `false` | Allows combinations involving overlapping forced movement, lethal hazards, enemy pressure, control or visual disruption, and amplified valuable loss. |
| `ValuableProtectionReleaseDelaySeconds` | `2` | `1`–`5` | Seconds to keep targeted valuables protected after Zero Gravity, Roll, Void, or another protected hazard event ends. Protection is disabled when `Targets > Valuables` is `false`. |

#### Feather

Makes applicable targets lighter for the duration of the event.

| Key | Default | Range / Values | Description |
|---|---:|---|---|
| `Enabled` | `true` | `true`, `false` | Includes Feather in event rolls. |
| `ChancePercent` | `6` | `0`–`100` | Independent chance for Feather. |

#### Zero Gravity

Applies zero gravity to applicable targets.

| Key | Default | Range / Values | Description |
|---|---:|---|---|
| `Enabled` | `true` | `true`, `false` | Includes Zero Gravity in event rolls. |
| `ChancePercent` | `6` | `0`–`100` | Independent chance for Zero Gravity. |
| `ProtectValuables` | `true` | `true`, `false` | Enables automatic valuable protection for Zero Gravity. Requires `Targets > Valuables = true`. |

#### Battery Charge

Restores charge to applicable battery-powered items at a configurable amount and interval.

| Key | Default | Range / Values | Description |
|---|---:|---|---|
| `Enabled` | `true` | `true`, `false` | Includes Battery Charge in event rolls. |
| `ChancePercent` | `6` | `0`–`100` | Independent chance for Battery Charge. |
| `ChargeAmount` | `5` | `1`–`100` | Battery percentage points restored per charge tick. |
| `ChargeIntervalSeconds` | `4` | `1`–`300` | Seconds between charge ticks. |

#### Heal

Restores health to applicable players at a configurable amount and interval.

| Key | Default | Range / Values | Description |
|---|---:|---|---|
| `Enabled` | `true` | `true`, `false` | Includes Heal in event rolls. |
| `ChancePercent` | `6` | `0`–`100` | Independent chance for Heal. |
| `HealAmount` | `10` | `1`–`100` | Health restored per healing tick. |
| `HealIntervalSeconds` | `2` | `1`–`300` | Seconds between healing ticks. |

#### Indestructible

Temporarily protects applicable physical targets from damage.

| Key | Default | Range / Values | Description |
|---|---:|---|---|
| `Enabled` | `true` | `true`, `false` | Includes Indestructible in event rolls. |
| `ChancePercent` | `6` | `0`–`100` | Independent chance for Indestructible. |

#### Fragility

Temporarily makes valuables easier to break through ordinary impacts. Breakage and value loss are visible to all players.

| Key | Default | Range / Values | Description |
|---|---:|---|---|
| `Enabled` | `true` | `true`, `false` | Includes Fragility in event rolls. This valuable-only event ignores `Targets`. |
| `ChancePercent` | `6` | `0`–`100` | Independent chance for Fragility. |
| `FragilityMultiplierPercent` | `1000` | `101`–`5000` | Impact-fragility multiplier. `100` is the vanilla reference; this event only accepts higher values so valuables always become easier to break. |

#### Gumball Hypnosis

While this event is active, holding a supported valuable, Cosmetic Box, item, or weapon applies the vanilla Gumball screen effect and softly pulls the holder's gaze toward that object. Releasing the object removes the effect. The original Gumball valuable is excluded to prevent its normal behavior from triggering twice.

This fixed-target event ignores `Targets`. Doors, players, and enemies never act as hypnosis triggers.

| Key | Default | Range / Values | Description |
|---|---:|---|---|
| `Enabled` | `true` | `true`, `false` | Includes Gumball Hypnosis in event rolls. |
| `ChancePercent` | `6` | `0`–`100` | Independent chance for Gumball Hypnosis. |

#### Healing Aura

Creates the vanilla Small Potion healing aura at random stage points in repeated waves. This player-only event ignores `Targets`.

| Key | Default | Range / Values | Description |
|---|---:|---|---|
| `Enabled` | `true` | `true`, `false` | Includes Healing Aura in event rolls. |
| `ChancePercent` | `6` | `0`–`100` | Independent event chance. |
| `HealthPool` | `50` | `1`–`1000` | Total health available from each aura. |
| `SpawnCountMin` / `SpawnCountMax` | `1` / `3` | `1`–`30` | Number of auras created per wave. |
| `SpawnIntervalSeconds` | `10` | `1`–`300` | Seconds between waves. |
| `MinimumPlayerDistance` | `3` | `0`–`100` | Requested minimum distance from living players. |
| `MaximumActiveInstances` | `30` | `1`–`30` | Maximum number of healing areas active at once. |

#### Star Barrage

Launches vanilla Star Wand projectiles from random stage points and directions in repeated waves.

| Key | Default | Range / Values | Description |
|---|---:|---|---|
| `Enabled` | `true` | `true`, `false` | Includes Star Barrage in event rolls. |
| `ChancePercent` | `6` | `0`–`100` | Independent event chance. |
| `ProtectValuables` | `true` | `true`, `false` | Enables automatic valuable protection for Star Barrage. |
| `ProjectileCountMin` / `ProjectileCountMax` | `3` / `6` | `1`–`30` | Number of projectiles launched per wave. |
| `SpawnIntervalSeconds` | `2` | `1`–`300` | Seconds between waves. |
| `MinimumPlayerDistance` | `3` | `0`–`100` | Requested minimum distance from living players for projectile origins. |
| `MaximumActiveInstances` | `30` | `1`–`30` | Maximum number of projectiles active at once. |

#### Spider Scare

Breaks vanilla Spider Potions at random living player positions. Nearby players receive the standard screen effect; the game's arachnophobia setting selects the corresponding presentation. This player-only event ignores `Targets`.

| Key | Default | Range / Values | Description |
|---|---:|---|---|
| `Enabled` | `true` | `true`, `false` | Includes Spider Scare in event rolls. |
| `ChancePercent` | `6` | `0`–`100` | Independent event chance. |
| `PlayersPerWaveMin` / `PlayersPerWaveMax` | `1` / `3` | `1`–`30` | Number of random player positions targeted per wave. |
| `SpawnIntervalSeconds` | `10` | `1`–`300` | Seconds between waves. |

#### Traffic Shock

Uses the vanilla Traffic Light red-state effect to shock and tumble random living players. This player-only event ignores `Targets`.

| Key | Default | Range / Values | Description |
|---|---:|---|---|
| `Enabled` | `true` | `true`, `false` | Includes Traffic Shock in event rolls. |
| `ChancePercent` | `6` | `0`–`100` | Independent event chance. |
| `PlayersPerPulseMin` / `PlayersPerPulseMax` | `1` / `3` | `1`–`30` | Number of random players shocked per pulse. |
| `PulseIntervalSeconds` | `10` | `1`–`300` | Seconds between pulses. |

#### Dangerous Valuables

Activates only compatible vanilla hazard valuables that are already placed on the stage. It never creates additional valuables. Enabled types are selected randomly and reactivated at each reactivation check. Their active state is not restored when the event ends.

| Key | Default | Range / Values | Description |
|---|---:|---|---|
| `Enabled` | `true` | `true`, `false` | Includes Dangerous Valuables in event rolls. |
| `ChancePercent` | `6` | `0`–`100` | Independent event chance. |
| `ProtectValuables` | `false` | `true`, `false` | Enables automatic protection for targeted valuables during Dangerous Valuables. |
| `IceSawEnabled`, `BlenderEnabled`, `FlamethrowerEnabled`, `EggEnabled`, `CarEnabled`, `PlaneEnabled`, `BroomEnabled` | `true` | `true`, `false` | Selects already-placed valuable types that may be activated. At least one type must be enabled. |
| `ActivationCountMin` / `ActivationCountMax` | `5` / `10` | `1`–`30` | Number of already-placed dangerous valuables selected for activation. |
| `ReactivationIntervalSeconds` | `10` | `1`–`300` | Seconds between reactivation checks. |
| `MaximumActiveInstances` | `15` | `1`–`30` | Maximum number of dangerous valuables active at once. |

#### Roll

Moves and tumbles applicable targets. Players can move toward their viewing direction, while other targets move in random directions.

**Warning:** Roll may kill enemies through movement and collisions.

| Key | Default | Range / Values | Description |
|---|---:|---|---|
| `Enabled` | `false` | `true`, `false` | Includes Roll in event rolls. |
| `ChancePercent` | `6` | `0`–`100` | Independent chance for Roll. |
| `ProtectValuables` | `true` | `true`, `false` | Enables automatic valuable protection for Roll. |

#### Void

Creates multiple Void effects at random locations across the stage. While the event is active, the current effects are replaced with a new set at newly randomized locations at the configured interval. The effects disappear when the event ends.

**Warning:** Void may kill players.

| Key | Default | Range / Values | Description |
|---|---:|---|---|
| `Enabled` | `false` | `true`, `false` | Includes Void in event rolls. |
| `ChancePercent` | `6` | `0`–`100` | Independent chance for Void. |
| `ProtectValuables` | `true` | `true`, `false` | Enables automatic valuable protection for Void. |
| `SpawnCountMin` | `3` | `2`–`30` | Minimum number of Void effects created. |
| `SpawnCountMax` | `5` | `2`–`30` | Maximum number of Void effects created. |
| `SpawnIntervalSeconds` | `10` | `1`–`300` | Seconds between Void regeneration waves. |

#### Levitation

Creates vanilla Levitation Potion effects at random stage points in repeated waves.

| Key | Default | Range | Description |
|---|---:|---:|---|
| `Enabled` | `true` | `true`, `false` | Includes Levitation in event rolls. |
| `ChancePercent` | `6` | `0`–`100` | Independent event chance. |
| `ProtectValuables` | `true` | `true`, `false` | Enables automatic valuable protection for Levitation. |
| `SpawnCountMin` / `SpawnCountMax` | `2` / `5` | `1`–`30` | Number created per wave. |
| `SpawnIntervalSeconds` | `10` | `1`–`300` | Seconds between waves. |
| `MinimumPlayerDistance` | `3` | `0`–`100` | Requested minimum distance from living players. |
| `MaximumActiveInstances` | `30` | `1`–`30` | Maximum number of levitation areas active at once. |

#### Shockwave / Stun Blast / Explosion Rain

Creates and arms the matching vanilla grenade at randomized stage points, then launches it in a randomized upward direction. Each event has the same setting layout below, including a default minimum player distance of `3`.

| Key | Default | Range | Description |
|---|---:|---:|---|
| `Enabled` | `true` | `true`, `false` | Includes the event in event rolls. |
| `ChancePercent` | `6` | `0`–`100` | Independent event chance. |
| `ProtectValuables` | `true` | `true`, `false` | Enables automatic valuable protection independently for each of these three events. |
| `SpawnCountMin` / `SpawnCountMax` | `5` / `10` | `1`–`30` | Number of grenades created per wave. |
| `SpawnIntervalSeconds` | `10` | `1`–`300` | Seconds between waves. |
| `MinimumPlayerDistance` | `3` | `0`–`100` | Requested minimum distance from living players. |
| `MaximumActiveInstances` | `30` | `1`–`30` | Maximum number of grenades active at once. |
| `LaunchForceMin` / `LaunchForceMax` | `6` / `12` | `0`–`100` | Minimum and maximum launch strength. |

#### Enemy Wave

Activates additional vanilla enemies that already belong to the current level, then replenishes missing event enemies while the event remains active.

| Key | Default | Range / Values | Description |
|---|---:|---|---|
| `Enabled` | `true` | `true`, `false` | Includes Enemy Wave in event rolls. |
| `ChancePercent` | `6` | `0`–`100` | Independent event chance. |
| `SpawnCountMin` / `SpawnCountMax` | `1` / `3` | `1`–`30` | Number of additional enemies kept active. |
| `ReplenishIntervalSeconds` | `10` | `1`–`300` | Seconds between replenishment checks. |
| `MinimumPlayerDistance` | `3` | `0`–`100` | Requested minimum placement distance from players. |
| `DespawnOnEnd` | `true` | `true`, `false` | Despawns surviving enemies activated by this event when it ends. |

#### Minefield

Places armed vanilla mines at randomized stage points and can replenish mines that have triggered or been destroyed. At event end, only mines whose detonation countdown has started remain; untriggered mines are removed.

| Key | Default | Range / Values | Description |
|---|---:|---|---|
| `Enabled` | `true` | `true`, `false` | Includes Minefield in event rolls. |
| `ChancePercent` | `6` | `0`–`100` | Independent event chance. |
| `ProtectValuables` | `true` | `true`, `false` | Enables automatic valuable protection for Minefield. |
| `ExplosiveEnabled` / `ShockwaveEnabled` / `StunEnabled` | `true` | `true`, `false` | Selects mine types that may appear. At least one type must be enabled. |
| `SpawnCountMin` / `SpawnCountMax` | `10` / `15` | `1`–`30` | Number of armed mines kept active. |
| `ReplenishIntervalSeconds` | `10` | `1`–`300` | Seconds between replenishment checks. |
| `MaximumActiveMines` | `30` | `1`–`30` | Maximum event-created mines kept active. |
| `MinimumPlayerDistance` | `3` | `0`–`100` | Requested minimum distance from living players. |
| `ReplenishTriggeredMines` | `true` | `true`, `false` | Replaces triggered or destroyed event mines. |

#### Enemy control events

All events in this section default to `Enabled=true` and `ChancePercent=6`.

| Event | Behavior | Additional settings (default; range) |
|---|---|---|
| `Freeze` | Keeps applicable enemies frozen until the event ends. | None. |
| `Stun` | Keeps applicable enemies stunned until the event ends. | None. |
| `Enemy Warp` | Teleports a random group of enemies to different places on the stage. | `IntervalSeconds=10` (`1`–`300`), `EnemiesPerPulse=3` (`1`–`30`), `MinimumPlayerDistance=3` (`0`–`100`). |
| `Enemy Hunt` | After all extractions are complete, plays a lure sound in a player-occupied room and sends enemies to investigate it. | `RetargetIntervalSeconds=5` (`1`–`300`). |
| `Enemy Speed Up` | Makes enemies move faster. Enemies that appear during the event are also affected. | `Enabled=true`, `ChancePercent=6`, `SpeedPercent=150` (`101`–`500`). |
| `Enemy Speed Down` | Makes enemies move slower. Enemies that appear during the event are also affected. | `Enabled=true`, `ChancePercent=6`, `SpeedPercent=50` (`10`–`99`). |
| `Enemy Regen` | Restores enemy health without exceeding the enemy's configured maximum. | `HealAmount=10` (`1`–`100`), `HealIntervalSeconds=5` (`1`–`300`). |
| `Enemy Purge` | Periodically damages enemies and can kill them when `CanKill` is enabled. | `DamageAmount=10` (`1`–`1000`), `DamageIntervalSeconds=5` (`1`–`300`), `CanKill=true`. |

#### Player events

All events in this section default to `Enabled=true` and `ChancePercent=6`. They are player-only events and ignore `Targets > Players`.

| Event | Behavior | Additional settings (default; range) |
|---|---|---|
| `Damage Pulse` | Damages every living player at each pulse. | `DamageAmount=5` (`1`–`100`), `DamageIntervalSeconds=5` (`1`–`300`), `SavingGrace=true`. |
| `Second Chance` | Revives players in place two seconds after death. It prevents a failed-stage retake while that delayed revive is pending. Players already dead at event start are not revived. | `CheckIntervalSeconds=2` (`1`–`300`), `MaxRevivesPerPlayer=1` (`1`–`10`). |
| `Knockback` | Launches every living player in a random sideways and upward direction. | `IntervalSeconds=5` (`1`–`300`), `HorizontalForce=8` (`0`–`100`), `VerticalForce=3` (`0`–`100`). |
| `Flicker` | Creates a short red-light flicker around every living player, including players without the mod. | `IntervalSeconds=2` (`1`–`300`), `IntensityPercent=200` (`1`–`500`). |

#### Stage and object events

All events in this section default to `Enabled=true` and `ChancePercent=6`.

| Event | Behavior | Additional settings (default; range) |
|---|---|---|
| `Quake` | Shakes applicable players and loose objects in random directions. | `IntervalSeconds=5` (`1`–`300`), `Force=8` (`0`–`100`), `ProtectValuables=true`. |
| `Door Chaos` | Repeatedly opens and closes selected doors. It works even when `Targets > Doors` is off and does not change that setting. Truck, shop, and extraction doors are excluded. Hinged valuables and items follow `HingedItemsEnabled`. | `IntervalSeconds=2` (`1`–`300`), `AffectedPercent=30` (`1`–`100`), `Force=12` (`1`–`30`), `HingedItemsEnabled=true`. |
| `Value Surge` | Temporarily multiplies all active valuable prices, including valuables in extraction areas, carts, and the truck. New valuables are included during the event, except tax-return money bags generated by an extraction during that event. | `MultiplierPercent=150` (`1`–`1000`), `RestoreOnEnd=true`. |
| `Value Crash` | Temporarily reduces all active valuable prices, including valuables in extraction areas, carts, and the truck. New valuables are included during the event, except tax-return money bags generated by an extraction during that event. | `MultiplierPercent=50` (`1`–`1000`), `RestoreOnEnd=true`. |

#### Restoration / Battery Drain / Heavy Cargo / Butterfingers

Each of these events has `Enabled=true` (`true`/`false`) and `ChancePercent=6` (`0`–`100`). Repeated effects happen when the event begins and then at their configured interval. Repairs and consumed charge are not undone when the event ends.

| Section | Key | Default | Range | Description |
|---|---|---:|---|---|
| Restoration | `RepairPercent` | `5` | `1`–`100` | Repairs this percentage of each surviving valuable's full value per pulse, capped at its full value. Does not recreate destroyed valuables or cancel Value Surge / Value Crash. |
| Restoration | `IntervalSeconds` | `5` | `1`–`300` | Seconds between repairs. |
| Battery Drain | `DrainAmount` | `5` | `1`–`100` | Charge percentage points removed per pulse. |
| Battery Drain | `IntervalSeconds` | `4` | `1`–`300` | Seconds between drain pulses. |
| Battery Drain | `MinimumChargePercent` | `0` | `0`–`100` | Lowest charge this event will leave. Normal use may drain charge further. |
| Heavy Cargo | `MassPercent` | `200` | `101`–`500` | Object weight percentage; `200` means twice the normal weight. Excludes players, enemies and doors. |
| Heavy Cargo | `ProtectValuables` | `true` | `true`, `false` | Protects affected valuables while heavy and briefly afterward. Requires `Targets > Valuables`. |
| Butterfingers | `IntervalSeconds` | `8` | `1`–`300` | Seconds between forced drops. Inventory slots, players, enemies and doors are excluded. |
| Butterfingers | `ProtectValuables` | `true` | `true`, `false` | Briefly protects dropped valuables. Requires `Targets > Valuables`. |

Heavy Cargo follows `Targets` for valuables, Cosmetic Boxes, items and weapons. The other three events have fixed targets and ignore those filters for their main effect. Both protection options use `Safety > ValuableProtectionReleaseDelaySeconds` (default `2`, range `1`–`5` seconds); Butterfingers counts this from each drop.

#### Enemy Blindness / Enemy Armor / Enemy Vulnerability

All three default to `Enabled=true` (`true`/`false`) and `ChancePercent=6` (`0`–`100`). They affect enemies regardless of `Targets`, include enemies that appear during the event, and last until the event ends.

| Section | Key | Default | Range | Description |
|---|---|---:|---|---|
| Enemy Blindness | `VisionPercent` | `25` | `1`–`99` | Remaining sight range. `25` means one quarter of normal. Hearing and already-started pursuit remain active. |
| Enemy Armor | `DamagePercent` | `50` | `1`–`99` | Incoming enemy damage percentage. `50` means half damage. |
| Enemy Vulnerability | `DamagePercent` | `200` | `101`–`500` | Incoming enemy damage percentage. `200` means double damage. |

Enemy damage changes work alongside existing defenses; they do not remove immunity. Very small hits may stay unchanged after rounding.

#### Supply Drop

Creates usable standard health packs and trackers/tools at random suitable stage locations. Items remain after the event and can be collected normally. Weapons, explosives, upgrades and custom items are excluded. This event ignores `Targets`.

| Key | Default | Range / Values | Description |
|---|---:|---|---|
| `Enabled` | `true` | `true`, `false` | Includes Supply Drop in event rolls. |
| `ChancePercent` | `6` | `0`–`100` | Independent event chance. |
| `SpawnCountMin` / `SpawnCountMax` | `1` / `2` | `1`–`10` | Items created per wave. Reversed limits are treated in ascending order. |
| `IntervalSeconds` | `15` | `1`–`300` | Seconds between waves. |
| `MaximumSpawnsPerStage` | `10` | `1`–`50` | Total items that may be created during a stage, including used or destroyed items. The limit also applies across repeated activations and persistent mode. |
| `HealthPacksEnabled` | `true` | `true`, `false` | Allows standard health packs. |
| `EquipmentEnabled` | `true` | `true`, `false` | Allows standard trackers and tools. At least one supply category must be enabled. |

#### Player Swap / Shared Pain

Both default to `Enabled=true` (`true`/`false`) and `ChancePercent=6` (`0`–`100`), ignore `Targets`, and require at least two living players to be selected. Player Swap exchanges one pair each pulse; eligible players must be standing, grounded and empty-handed. It does not move stored inventory out of its slots.

| Section | Key | Default | Range / Values | Description |
|---|---|---:|---|---|
| Player Swap | `IntervalSeconds` | `15` | `1`–`300` | Seconds between attempts to swap two eligible players. Unsafe or obstructed destinations are skipped. |
| Shared Pain | `DamagePercent` | `25` | `1`–`100` | Part of actual health lost that each other living player receives. Shared damage and health donations do not trigger another round of sharing. |
| Shared Pain | `MaximumDamagePerHit` | `25` | `1`–`100` | Maximum shared damage received per player for one hit. |
| Shared Pain | `CanKill` | `false` | `true`, `false` | Allows lethal shared damage. When off, damage is capped to leave at least 1 HP using the latest known health and saving grace is enabled. Simultaneous damage can still be fatal. |

Shared Pain responds to damage, not a timer. There is no interval setting for it.

#### Valuable protection

Each applicable event has its own `ProtectValuables` switch: Zero Gravity, Roll, Void, Levitation, Shockwave, Stun Blast, Explosion Rain, Minefield, Star Barrage, Dangerous Valuables, Quake, Heavy Cargo, and Butterfingers. Dangerous Valuables defaults to `false`; all other switches default to `true`. Protection is applied only when both the active event's switch and `Targets > Valuables` are enabled. If several effects run together, protection remains active when at least one of their switches is enabled. All protected events use `Safety > ValuableProtectionReleaseDelaySeconds`; Butterfingers protects only dropped valuables and counts the delay from each drop.

#### Targets

These filters control broad events that can affect multiple physical categories, such as Feather, Zero Gravity, Indestructible, Roll, and Quake. Events with an inherent fixed target ignore `Targets`: Battery Charge, Heal, Fragility, Gumball Hypnosis, Healing Aura, Spider Scare, Traffic Shock, Flicker, enemy control events, player events, Door Chaos, Value Surge, Value Crash, and Dangerous Valuables. Cosmetic Boxes remain independent from general items for broad events. `Valuables` still controls automatic valuable protection even when a valuable-only event ignores it for the event's primary effect.

Heavy Cargo also follows these filters, but cannot affect players, enemies or doors. Restoration, Battery Drain, Butterfingers, Enemy Blindness, Enemy Armor, Enemy Vulnerability, Supply Drop, Player Swap and Shared Pain use their own fixed targets instead.

| Key | Default | Description |
|---|---:|---|
| `Valuables` | `true` | Allows broad multi-target events to affect valuables and enables automatic valuable protection. |
| `CosmeticBoxes` | `false` | Allows broad multi-target events to affect Cosmetic Boxes independently from general items. |
| `Items` | `true` | Allows broad multi-target events to affect general items, carts, and movable props, excluding Cosmetic Boxes. |
| `Doors` | `false` | Allows broad multi-target events to affect doors, lids, and other hinged movable objects. Door Chaos ignores this value. |
| `Weapons` | `true` | Allows broad multi-target events to affect weapons. |
| `Players` | `true` | Allows broad multi-target events to affect players. Player-only events ignore this value. |
| `Enemies` | `true` | Allows broad multi-target events to affect enemies. Enemy-only events ignore this value. |

#### Notifications

| Key | Default | Range / Values | Description |
|---|---:|---|---|
| `ChatAnnouncementsEnabled` | `true` | `true`, `false` | Enables stage-start, effect-name, countdown, and End chat announcements. Uses the host's setting. |
| `EnemyReactionEnabled` | `false` | `true`, `false` | Allows enemies to investigate Stage Flux announcement voices. Ordinary voice chat and other sounds are unaffected. Uses the host's setting. |
| `StartCountdownEnabled` | `true` | `true`, `false` | Enables `5` through `0` during the final five seconds of the interval. When disabled, only the effect name is announced three seconds before the interval ends. Uses the host's setting. |
| `EndCountdownEnabled` | `true` | `true`, `false` | Enables the three-second countdown before an event ends. Uses the host's setting. |

#### HUD

| Key | Default | Range / Values | Description |
|---|---:|---|---|
| `Enabled` | `true` | `true`, `false` | Shows the event HUD for you. |
| `Style` | `Graphical` | `Graphical`, `Classic` | Selects the five-slot graphical HUD or restores the previous text-only HUD. |
| `LayoutDirection` | `Vertical` | `Vertical`, `Horizontal` | Selects the graphical slot direction and sequential stop order. |
| `Anchor` | `BottomRight` | `TopLeft`, `TopCenter`, `TopRight`, `MiddleLeft`, `MiddleCenter`, `MiddleRight`, `BottomLeft`, `BottomCenter`, `BottomRight` | Selects the HUD anchor. |
| `Alignment` | `Right` | `Left`, `Center`, `Right` | Selects text alignment for the Classic HUD. |
| `OffsetX` | `0` | `-3840`–`3840` | Horizontal offset from the anchor in pixels. |
| `OffsetY` | `0` | `-2160`–`2160` | Vertical offset from the anchor in pixels. |
| `ScalePercent` | `70` | `50`–`200` | HUD scale percentage. |
| `BackgroundOpacityPercent` | `50` | `0`–`100` | Graphical HUD background opacity. Icons and text remain fully visible. |

### Notifications and HUD

- When stage events are enabled, active player avatars announce `Effects` one second after the stage becomes active.
- The next event is selected during the final 10 seconds of the interval. With the start countdown enabled, effect names are announced with 8 seconds remaining, followed by `5` through `0`; the effect begins when the interval ends.
- Before a non-persistent event ends, player avatars announce `3`, `2`, `1`, and then `End`.
- The HUD remains visible during intervals. It shows `NEXT`/`READY` until the final 10 seconds, then spins to the selected event icons; if an interval begins with 10 seconds or less, the waiting icon is skipped. The graphical HUD always has five slots and updates locked slots when the host changes `MaxSimultaneousEffects`. Active or already-selected effects remain visible until they end.
- During icon changes, usable slots spin together and stop from top to bottom or left to right. The animation never delays gameplay effects or chat announcements.
- All in-game mod text is in English.

### Compatibility

- Compatible with DroneToOrbItem. Orb items added by that mod keep their normal item behavior and can still be affected by stage events.
- Compatible with Elite Enemy Variants. Enemy Wave and enemy-speed events work alongside its enemy changes.
- Compatible with RoleShuffle. Value changes, revival effects, dangerous valuables, and announcements work together automatically.
- Players revived during an event receive applicable active effects.
- Newly created physical objects can receive applicable active effects.
- Target coverage follows current stage objects, including stages expanded by other mods.

## 日本語

### Eventsメニュー

ロビーまたはポーズメニューの **Events** から開きます。RoleShuffleを同時導入している場合は **Roles** の下に配置されます。

- **Current Events**：現在のステージで発生しているイベントを確認できます。
- **Event Guide**：全イベントのアイコン、説明、危険度を確認できます。
- **Event Settings**：ホストが各イベントをON/OFFできます。今後の抽選に反映され、実行中のイベントや選択済みのステージ計画は変わりません。MOD導入済みの参加者はホストの選択を閲覧できます。詳細設定は引き続きREPOConfigで変更できます。
- **Presets**：標準設定、低危険度のみ、すべてOFFから選択できます。変更するのはイベントのON/OFFのみです。
- **Tools**：表示データの再取得、ドラッグ操作に対応したHUD編集、コピーまたは開くことができる不具合レポートを利用できます。レポートは自動送信されません。共有前に内容を確認してください。

HUD編集は自分の表示のみを変更し、イベントを発動しません。保存せずにキャンセルすることもできます。ゲームプレイの効果を受けるために、参加者がこのメニューを導入する必要はありません。メニューの利用にはMenuLibが必要です。

### 概要

Stage Fluxは、R.E.P.O.のステージ全体に46種類のランダムイベントを発生させるMODです。物理挙動、プレイヤー支援・危険、敵の行動、ステージ設備、貴重品の価格・壊れやすさ、バニラのハザードなどを変化させます。

ゲームプレイ上の効果はホストが導入すれば利用できます。MOD未導入の参加者にも効果とチャット通知が適用されます。MOD導入済みの参加者はイベントHUDも利用できます。

### イベント一覧

全46イベントについて、危険度、デフォルト状態、デフォルト確率、簡潔な動作を掲載しています。

| イベント | 危険度 | デフォルト | 確率 | 内容 |
|---|---:|---:|---:|---|
| Feather | 低 | 有効 | 6% | ステージ内の対象が軽くなり、持ち上げやすく、ふわりと動くようになります。 |
| Zero Gravity | 中 | 有効 | 6% | ステージ内の対象から重力をなくし、空中に浮かびやすくします。 |
| Battery Charge | 低 | 有効 | 6% | 電池を使うアイテムの残量を、イベント中に繰り返し回復します。 |
| Heal | 低 | 有効 | 6% | 生きているプレイヤーの体力を、イベント中に繰り返し回復します。 |
| Indestructible | 低 | 有効 | 6% | 対象になった貴重品やアイテムが、イベント中は壊れなくなります。 |
| Fragility | 高 | 有効 | 6% | 貴重品が非常に壊れやすくなり、軽い衝突でも価値を失いやすくなります。 |
| Gumball Hypnosis | 中 | 有効 | 6% | 対象物を持つプレイヤーへ画面効果を与え、視線を対象へ引き寄せます。 |
| Healing Aura | 低 | 有効 | 6% | 触れると回復できる光のエリアが、ステージのランダムな場所に現れます。 |
| Star Barrage | 高 | 有効 | 6% | ステージのランダムな場所と方向から、危険な星の弾が飛んできます。 |
| Spider Scare | 中 | 有効 | 6% | ランダムなプレイヤーの画面に、蜘蛛が現れる恐怖演出を発生させます。 |
| Traffic Shock | 高 | 有効 | 6% | 信号機の赤信号と同じ効果で、ランダムなプレイヤーを感電・転倒させます。 |
| Dangerous Valuables | 高 | 有効 | 6% | ステージに最初から置かれている、危険な仕掛け付き貴重品を作動させます。 |
| Roll | 高 | 無効 | 6% | 対象が激しく回転・移動します。プレイヤーは見ている方向へ動けます。 |
| Void | 高 | 無効 | 6% | 危険なボイドが複数現れ、一定時間ごとに別の場所へ出現し直します。 |
| Levitation | 中 | 有効 | 6% | 物やプレイヤーを浮かせるエリアが、ステージのランダムな場所に現れます。 |
| Shockwave | 中 | 有効 | 6% | 吹き飛ばし効果のあるグレネードが、ランダムな方向へ投げ込まれます。 |
| Stun Blast | 中 | 有効 | 6% | 周囲をスタンさせるグレネードが、ランダムな方向へ投げ込まれます。 |
| Explosion Rain | 高 | 有効 | 6% | 爆発するグレネードが、ステージ内へランダムな方向から投げ込まれます。 |
| Enemy Wave | 高 | 有効 | 6% | 現在のステージに登場できる敵が、追加で1-3体活動を始めます。 |
| Minefield | 高 | 有効 | 6% | 起動済みの地雷がステージ内に置かれ、作動・破壊されると補充されます。 |
| Freeze | 中 | 有効 | 6% | イベント中、対象の敵が凍りついて動けなくなります。 |
| Stun | 中 | 有効 | 6% | イベント中、対応している敵が気絶して行動できなくなります。 |
| Enemy Warp | 高 | 有効 | 6% | 敵が突然ワープし、ステージ内の別の場所から現れます。 |
| Enemy Hunt | 高 | 有効 | 6% | 全納品完了後、プレイヤーがいる部屋で誘導音を鳴らし敵を向かわせます。 |
| Enemy Speed Up | 高 | 有効 | 6% | すべての敵の移動と加速が速くなり、途中から現れた敵にも適用されます。 |
| Enemy Speed Down | 低 | 有効 | 6% | すべての敵の移動と加速が遅くなり、途中から現れた敵にも適用されます。 |
| Enemy Regen | 高 | 有効 | 6% | イベント中、すべての敵が少しずつ体力を回復します。 |
| Enemy Purge | 低 | 有効 | 6% | イベント中、すべての敵が少しずつダメージを受けます。 |
| Damage Pulse | 高 | 有効 | 6% | イベント中、生きているすべてのプレイヤーが繰り返しダメージを受けます。 |
| Second Chance | 低 | 有効 | 6% | イベント開始後に死亡したプレイヤーを、死亡地点で2秒後に復活させます。 |
| Knockback | 中 | 有効 | 6% | 生きているプレイヤーが、ランダムな方向へ定期的に吹き飛ばされます。 |
| Flicker | 中 | 有効 | 6% | 生きているプレイヤーの周囲で、赤いライトが繰り返し点滅します。 |
| Quake | 高 | 有効 | 6% | プレイヤーや床に置かれた物が、地震のようにランダムな方向へ揺さぶられます。 |
| Door Chaos | 中 | 有効 | 6% | 通常の扉や大型扉、ふた付きの物が、イベント中に何度も開閉します。 |
| Value Surge | 低 | 有効 | 6% | 納品所やカート、トラック内を含む、すべての貴重品の価格が上がります。 |
| Value Crash | 高 | 有効 | 6% | 納品所やカート、トラック内を含む、すべての貴重品の価格が下がります。 |
| Restoration | 低 | 有効 | 6% | 残っている貴重品の損傷を定期的に修復します。 |
| Battery Drain | 中 | 有効 | 6% | 電池を使うアイテムの残量が繰り返し減ります。 |
| Heavy Cargo | 中 | 有効 | 6% | 対象の貴重品やアイテムが重くなります。 |
| Butterfingers | 中 | 有効 | 6% | 手に持っている貴重品やアイテムを定期的に落としてしまいます。 |
| Enemy Blindness | 低 | 有効 | 6% | 敵の視認距離が短くなります。聴覚や開始済みの追跡は残ります。 |
| Enemy Armor | 高 | 有効 | 6% | 敵が受けるダメージが減ります。 |
| Enemy Vulnerability | 低 | 有効 | 6% | 敵が受けるダメージが増えます。 |
| Supply Drop | 低 | 有効 | 6% | バニラの回復アイテムや道具がステージ内に出現します。 |
| Player Swap | 中 | 有効 | 6% | 条件を満たす生存プレイヤー2人の位置が入れ替わります。 |
| Shared Pain | 高 | 有効 | 6% | 1人が受けたダメージの一部を、ほかの生存プレイヤーも受けます。 |

### ゲームプレイ上の注意

- Rollにより敵が激しく移動し、死亡する可能性があります。
- Voidによりプレイヤーが死亡する可能性があります。
- Explosion RainおよびMinefieldの爆発地雷により、プレイヤーの死亡や貴重品の破損が発生する可能性があります。
- Damage Pulse、Knockback、Quakeでもプレイヤーの死亡や貴重品の損失が発生する可能性があります。
- Fragilityを`100%`より大きくすると、通常の衝突で貴重品が壊れやすくなります。
- Gumball Hypnosisは対象物を持ったプレイヤーのカメラと視線を一時的に操作します。
- Star Barrage、Traffic Shock、Dangerous Valuablesはプレイヤーの死亡や貴重品の破損を引き起こす可能性があります。
- Spider Scareはバニラの蜘蛛画面演出を表示し、ゲーム本体のクモ恐怖症設定に従います。
- RollとVoidは引き続きデフォルトで無効です。
- Shared Painはチーム全体を危険にさらします。デフォルトの致死回避はホストが把握している最新の体力を使用するため、同時に別のダメージを受けると死亡する可能性があります。
- Player Swapは位置だけを交換し、ステージや納品状況を変更しません。物を持っている、しゃがんでいる、空中にいる、転倒しているプレイヤーや、移動先に障害物がある場合は対象から外します。
- その他のイベントはすべてデフォルトで有効です。プレイ前に動作とリスクを確認してください。

### 必須MOD

- BepInExPack 5.4.2305以降の互換バージョン
- REPOConfig 1.2.6以降の互換バージョン

### 導入方法

対応するMODマネージャーから導入するか、`StagePhysicsEvents.dll`をプロファイルの`BepInEx/plugins`フォルダーへ配置してください。

### マルチプレイ

- イベントの抽選、時間、対象、チャット通知にはホスト側の設定を使用します。
- 参加者側はMOD未導入でもゲームプレイ上の効果を受けます。
- MOD導入済みの参加者はイベントHUDを利用でき、HUDの表示位置は各自で設定できます。
- シングルプレイでもマルチプレイのホストと同じイベント処理を使用します。

### イベントモード

| 設定値 | 内容 |
|---|---|
| `AllMode` | ステージ開始時に他の4モードから等確率で1つを選択します。 |
| `RandomEachEvent` | イベントごとにエフェクトの組み合わせ、効果時間、インターバルを再抽選します。 |
| `FixedForStage` | 最初のエフェクト組み合わせ、効果時間、インターバルをステージ中固定します。 |
| `FixedPerExtraction` | 効果時間とインターバルをステージ中固定し、エフェクト組み合わせだけを納品完了ごとに再抽選します。新しい組み合わせは次回イベントから使用します。 |
| `PersistentForStage` | 選択されたエフェクト組み合わせをステージ終了まで維持します。 |

### イベント抽選

- `StageActivationChancePercent`はステージ開始時に1回だけ抽選します。抽選に外れたステージでは、エフェクト、HUD、ステージ開始通知を無効にします。
- 有効な各エフェクトの`ChancePercent`を個別に抽選します。確率は合算・正規化しないため、合計が100%を超えても有効です。
- `MaxSimultaneousEffects`で、同時に発生できるエフェクト数を制限します。上限を超えた場合は、当選したエフェクトからランダムに選択します。
- `AllowDangerousCombinations`が無効の場合、強制移動・致死性ハザード・敵の脅威・操作や視界の妨害・貴重品損失の増幅が重なる組み合わせを避けます。
- FragilityとValue Crashは、貴重品を物理的に危険にするイベントと重なりません。通常の貴重品危険イベントも同時に2個までです。
- Freeze / Stun、Enemy Speed Up / Enemy Speed Down、Enemy Regen / Enemy Purge、Indestructible / Fragility、Value Surge / Value Crashは設定に関係なく同時発生しません。
- Feather / Heavy Cargo、Battery Charge / Battery Drain、Enemy Armor / Enemy Vulnerabilityも同時発生しません。
- `RandomEachEvent`では、1つも当選しなかった回のイベントをスキップします。
- `FixedForStage`、`FixedPerExtraction`、`PersistentForStage`では、初回のエフェクト抽選で1つも当選しなかった場合、そのステージではエフェクトとHUDを無効にします。
- `PersistentForStage`を除き、イベントが有効なステージは最初のイベント前に必ず設定されたインターバルを1回挟みます。
- `FixedPerExtraction`で納品後の再抽選が1つも当選しなかった場合、次の納品で再抽選されるまでエフェクトは発生しません。

### 設定

すべての設定はREPOConfigから変更できます。ゲームプレイ設定にはホスト側の値を使用し、HUDの表示設定はMOD導入済みの各プレイヤーが個別に使用します。

#### General

| 設定キー | デフォルト | 範囲 / 設定値 | 内容 |
|---|---:|---|---|
| `Enabled` | `true` | `true`, `false` | MOD全体の有効・無効を切り替えます。 |
| `Mode` | `RandomEachEvent` | `AllMode`, `RandomEachEvent`, `FixedForStage`, `FixedPerExtraction`, `PersistentForStage` | イベントモードを選択します。 |
| `StageActivationChancePercent` | `50` | `0`–`100` | ステージ開始時にイベントを有効にする確率です。既存のカスタム値は維持されます。 |
| `MaxSimultaneousEffects` | `3` | `1`–`5` | 1回のイベントで同時発生できる最大エフェクト数です。ステージ途中の変更はHUDへ即時反映し、次の未確定抽選から適用します。 |

#### Timing

時間設定はすべて整数秒です。最小値が最大値を超えている場合は、設定を書き換えず小さい値から大きい値までの範囲として使用します。

| 設定キー | デフォルト | 範囲 | 内容 |
|---|---:|---:|---|
| `IntervalMinSeconds` | `45` | `10`–`300` | 次のイベントまでの最小待機秒数です。 |
| `IntervalMaxSeconds` | `90` | `10`–`300` | 次のイベントまでの最大待機秒数です。 |
| `EffectDurationMinSeconds` | `15` | `10`–`300` | エフェクトの最小効果秒数です。 |
| `EffectDurationMaxSeconds` | `30` | `10`–`300` | エフェクトの最大効果秒数です。 |

#### Safety

| 設定キー | デフォルト | 範囲 / 設定値 | 内容 |
|---|---:|---|---|
| `AllowDangerousCombinations` | `false` | `true`, `false` | 強制移動、致死性ハザード、敵の脅威、操作・視界妨害、貴重品損失の増幅が重なる組み合わせを許可します。 |
| `ValuableProtectionReleaseDelaySeconds` | `2` | `1`–`5` | Zero Gravity、Roll、Void、その他の保護対象ハザード終了後も、対象の貴重品を保護する秒数です。`Targets > Valuables = false`では保護しません。 |

#### Feather

適用可能な対象をイベント中軽量化します。

| 設定キー | デフォルト | 範囲 / 設定値 | 内容 |
|---|---:|---|---|
| `Enabled` | `true` | `true`, `false` | Featherをイベント抽選に含めます。 |
| `ChancePercent` | `6` | `0`–`100` | Featherの独立発生確率です。 |

#### Zero Gravity

適用可能な対象を無重力化します。

| 設定キー | デフォルト | 範囲 / 設定値 | 内容 |
|---|---:|---|---|
| `Enabled` | `true` | `true`, `false` | Zero Gravityをイベント抽選に含めます。 |
| `ChancePercent` | `6` | `0`–`100` | Zero Gravityの独立発生確率です。 |
| `ProtectValuables` | `true` | `true`, `false` | Zero Gravityによる貴重品保護を有効にします。`Targets > Valuables = true`が必要です。 |

#### Battery Charge

適用可能なバッテリー式アイテムの充電量を、設定した量と間隔で回復します。

| 設定キー | デフォルト | 範囲 / 設定値 | 内容 |
|---|---:|---|---|
| `Enabled` | `true` | `true`, `false` | Battery Chargeをイベント抽選に含めます。 |
| `ChancePercent` | `6` | `0`–`100` | Battery Chargeの独立発生確率です。 |
| `ChargeAmount` | `5` | `1`–`100` | 1回の充電で回復するバッテリー量（パーセントポイント）です。 |
| `ChargeIntervalSeconds` | `4` | `1`–`300` | 充電処理を行う間隔秒数です。 |

#### Heal

適用可能なプレイヤーの体力を、設定した量と間隔で回復します。

| 設定キー | デフォルト | 範囲 / 設定値 | 内容 |
|---|---:|---|---|
| `Enabled` | `true` | `true`, `false` | Healをイベント抽選に含めます。 |
| `ChancePercent` | `6` | `0`–`100` | Healの独立発生確率です。 |
| `HealAmount` | `10` | `1`–`100` | 1回の回復で増加する体力です。 |
| `HealIntervalSeconds` | `2` | `1`–`300` | 回復処理を行う間隔秒数です。 |

#### Indestructible

適用可能な物理対象を一時的に破壊されない状態にします。

| 設定キー | デフォルト | 範囲 / 設定値 | 内容 |
|---|---:|---|---|
| `Enabled` | `true` | `true`, `false` | Indestructibleをイベント抽選に含めます。 |
| `ChancePercent` | `6` | `0`–`100` | Indestructibleの独立発生確率です。 |

#### Fragility

通常の衝突で貴重品が一時的に壊れやすくなります。破損と価値減少は全プレイヤーに反映されます。

| 設定キー | デフォルト | 範囲 / 設定値 | 内容 |
|---|---:|---|---|
| `Enabled` | `true` | `true`, `false` | Fragilityをイベント抽選に含めます。貴重品専用イベントのため`Targets`を無視します。 |
| `ChancePercent` | `6` | `0`–`100` | Fragilityの独立発生確率です。 |
| `FragilityMultiplierPercent` | `1000` | `101`–`5000` | 衝突時の壊れやすさ倍率です。`100`をバニラ基準とし、必ず壊れやすくなるよう`100`より大きい値だけを設定できます。 |

#### Gumball Hypnosis

イベント中、対応する貴重品、Cosmetic Box、アイテム、武器を持つと、バニラのGumball画面効果が発生し、持っている物体へ視線が緩やかに引き寄せられます。物体を手放すと解除します。通常のGumball貴重品は、本来の効果との二重発動を防ぐため対象外です。

このイベントは固有対象を持つため`Targets`を無視します。扉、プレイヤー、敵は催眠の発動対象になりません。

| 設定キー | デフォルト | 範囲 / 設定値 | 内容 |
|---|---:|---|---|
| `Enabled` | `true` | `true`, `false` | Gumball Hypnosisをイベント抽選に含めます。 |
| `ChancePercent` | `6` | `0`–`100` | Gumball Hypnosisの独立発生確率です。 |

#### Healing Aura

ステージ上のランダム地点に、Small Potionのバニラ回復エリアを繰り返し生成します。プレイヤー専用イベントのため`Targets`を無視します。

| 設定キー | デフォルト | 範囲 / 設定値 | 内容 |
|---|---:|---|---|
| `Enabled` | `true` | `true`, `false` | Healing Auraをイベント抽選に含めます。 |
| `ChancePercent` | `6` | `0`–`100` | 独立発生確率です。 |
| `HealthPool` | `50` | `1`–`1000` | 各回復エリアが回復できる合計体力です。 |
| `SpawnCountMin` / `SpawnCountMax` | `1` / `3` | `1`–`30` | 1ウェーブで生成する回復エリア数です。 |
| `SpawnIntervalSeconds` | `10` | `1`–`300` | ウェーブ間隔です。 |
| `MinimumPlayerDistance` | `3` | `0`–`100` | 生成地点に要求する生存プレイヤーとの最小距離です。 |
| `MaximumActiveInstances` | `30` | `1`–`30` | 同時に存在できる回復エリアの最大数です。 |

#### Star Barrage

ステージ上のランダム地点と方向から、Star Wandのバニラ弾を繰り返し発射します。

| 設定キー | デフォルト | 範囲 / 設定値 | 内容 |
|---|---:|---|---|
| `Enabled` | `true` | `true`, `false` | Star Barrageをイベント抽選に含めます。 |
| `ChancePercent` | `6` | `0`–`100` | 独立発生確率です。 |
| `ProtectValuables` | `true` | `true`, `false` | Star Barrageによる貴重品保護を有効にします。 |
| `ProjectileCountMin` / `ProjectileCountMax` | `3` / `6` | `1`–`30` | 1ウェーブで発射する弾数です。 |
| `SpawnIntervalSeconds` | `2` | `1`–`300` | ウェーブ間隔です。 |
| `MinimumPlayerDistance` | `3` | `0`–`100` | 発射地点に要求する生存プレイヤーとの最小距離です。 |
| `MaximumActiveInstances` | `30` | `1`–`30` | 同時に存在できる弾の最大数です。 |

#### Spider Scare

ランダムな生存プレイヤーの位置でSpider Potionを破壊し、周囲へバニラの画面演出を発生させます。ゲーム本体のクモ恐怖症設定に対応します。プレイヤー専用イベントのため`Targets`を無視します。

| 設定キー | デフォルト | 範囲 / 設定値 | 内容 |
|---|---:|---|---|
| `Enabled` | `true` | `true`, `false` | Spider Scareをイベント抽選に含めます。 |
| `ChancePercent` | `6` | `0`–`100` | 独立発生確率です。 |
| `PlayersPerWaveMin` / `PlayersPerWaveMax` | `1` / `3` | `1`–`30` | 1ウェーブで対象地点に選ぶプレイヤー数です。 |
| `SpawnIntervalSeconds` | `10` | `1`–`300` | ウェーブ間隔です。 |

#### Traffic Shock

Traffic Lightの赤信号効果を使用し、ランダムな生存プレイヤーを感電・転倒させます。プレイヤー専用イベントのため`Targets`を無視します。

| 設定キー | デフォルト | 範囲 / 設定値 | 内容 |
|---|---:|---|---|
| `Enabled` | `true` | `true`, `false` | Traffic Shockをイベント抽選に含めます。 |
| `ChancePercent` | `6` | `0`–`100` | 独立発生確率です。 |
| `PlayersPerPulseMin` / `PlayersPerPulseMax` | `1` / `3` | `1`–`30` | 1回の処理で感電させるランダムプレイヤー数です。 |
| `PulseIntervalSeconds` | `10` | `1`–`300` | 感電処理の間隔です。 |

#### Dangerous Valuables

ステージ上に配置済みの対応する危険なバニラ貴重品だけを起動します。新しい貴重品は生成しません。有効な種類からランダムに選択して一定間隔で再起動し、イベント終了時も状態を復元しません。

| 設定キー | デフォルト | 範囲 / 設定値 | 内容 |
|---|---:|---|---|
| `Enabled` | `true` | `true`, `false` | Dangerous Valuablesをイベント抽選に含めます。 |
| `ChancePercent` | `6` | `0`–`100` | 独立発生確率です。 |
| `ProtectValuables` | `false` | `true`, `false` | Dangerous Valuables中の対象貴重品保護を有効にします。 |
| `IceSawEnabled`, `BlenderEnabled`, `FlamethrowerEnabled`, `EggEnabled`, `CarEnabled`, `PlaneEnabled`, `BroomEnabled` | `true` | `true`, `false` | 起動を許可する配置済み貴重品の種類です。1種類以上を有効にする必要があります。 |
| `ActivationCountMin` / `ActivationCountMax` | `5` / `10` | `1`–`30` | 起動対象として選択する配置済みの危険な貴重品数です。 |
| `ReactivationIntervalSeconds` | `10` | `1`–`300` | 再起動確認の間隔です。 |
| `MaximumActiveInstances` | `15` | `1`–`30` | 同時に作動できる危険な貴重品の最大数です。 |

#### Roll

適用可能な対象を移動・回転させます。プレイヤーは目線方向へ移動でき、その他の対象はランダムな方向へ移動します。

**注意：** Rollの移動や衝突により、敵が死亡する可能性があります。

| 設定キー | デフォルト | 範囲 / 設定値 | 内容 |
|---|---:|---|---|
| `Enabled` | `false` | `true`, `false` | Rollをイベント抽選に含めます。 |
| `ChancePercent` | `6` | `0`–`100` | Rollの独立発生確率です。 |
| `ProtectValuables` | `true` | `true`, `false` | Rollによる貴重品保護を有効にします。 |

#### Void

ステージ上のランダムな場所に複数のVoidエフェクトを生成します。イベント中は設定した間隔ごとに現在のエフェクトを入れ替え、新しいランダム地点へ再生成します。生成したエフェクトはイベント終了時に消失します。

**注意：** Voidによりプレイヤーが死亡する可能性があります。

| 設定キー | デフォルト | 範囲 / 設定値 | 内容 |
|---|---:|---|---|
| `Enabled` | `false` | `true`, `false` | Voidをイベント抽選に含めます。 |
| `ChancePercent` | `6` | `0`–`100` | Voidの独立発生確率です。 |
| `ProtectValuables` | `true` | `true`, `false` | Voidによる貴重品保護を有効にします。 |
| `SpawnCountMin` | `3` | `2`–`30` | 生成するVoidエフェクト数の最小値です。 |
| `SpawnCountMax` | `5` | `2`–`30` | 生成するVoidエフェクト数の最大値です。 |
| `SpawnIntervalSeconds` | `10` | `1`–`300` | Voidエフェクトを再生成する間隔です。 |

#### Levitation

ステージ上のランダム地点に、バニラのLevitation Potionエフェクトを一定間隔で生成します。

| 設定キー | デフォルト | 範囲 | 内容 |
|---|---:|---:|---|
| `Enabled` | `true` | `true`, `false` | Levitationをイベント抽選に含めます。 |
| `ChancePercent` | `6` | `0`–`100` | 独立発生確率です。 |
| `ProtectValuables` | `true` | `true`, `false` | Levitationによる貴重品保護を有効にします。 |
| `SpawnCountMin` / `SpawnCountMax` | `2` / `5` | `1`–`30` | 1回に生成する数です。 |
| `SpawnIntervalSeconds` | `10` | `1`–`300` | 再生成までの間隔秒数です。 |
| `MinimumPlayerDistance` | `3` | `0`–`100` | 生存プレイヤーから確保する最小距離の目安です。 |
| `MaximumActiveInstances` | `30` | `1`–`30` | 同時に存在できる浮遊エリアの最大数です。 |

#### Shockwave / Stun Blast / Explosion Rain

ステージ上のランダム地点に対応するバニラグレネードを生成・起動し、ランダムな上向き方向へ投射します。3イベントは同じ設定構成で、`MinimumPlayerDistance`のデフォルトはすべて`3`です。

| 設定キー | デフォルト | 範囲 | 内容 |
|---|---:|---:|---|
| `Enabled` | `true` | `true`, `false` | 対象イベントを抽選に含めます。 |
| `ChancePercent` | `6` | `0`–`100` | 独立発生確率です。 |
| `ProtectValuables` | `true` | `true`, `false` | 3イベントそれぞれで貴重品保護を個別に有効化します。 |
| `SpawnCountMin` / `SpawnCountMax` | `5` / `10` | `1`–`30` | 1回に生成するグレネード数です。 |
| `SpawnIntervalSeconds` | `10` | `1`–`300` | 再生成までの間隔秒数です。 |
| `MinimumPlayerDistance` | `3` | `0`–`100` | 生存プレイヤーから確保する最小距離の目安です。 |
| `MaximumActiveInstances` | `30` | `1`–`30` | 同時に存在できるグレネードの最大数です。 |
| `LaunchForceMin` / `LaunchForceMax` | `6` / `12` | `0`–`100` | 起動済みグレネードを投射するときに適用するランダムな速度変化量です。 |

#### Enemy Wave

現在のレベルに用意されているバニラの待機中エネミーを追加で出現させ、イベント中に不足した数を補充します。

| 設定キー | デフォルト | 範囲 / 設定値 | 内容 |
|---|---:|---|---|
| `Enabled` | `true` | `true`, `false` | Enemy Waveをイベント抽選に含めます。 |
| `ChancePercent` | `6` | `0`–`100` | 独立発生確率です。 |
| `SpawnCountMin` / `SpawnCountMax` | `1` / `3` | `1`–`30` | 追加で維持する敵の数です。 |
| `ReplenishIntervalSeconds` | `10` | `1`–`300` | 不足数を確認する間隔秒数です。 |
| `MinimumPlayerDistance` | `3` | `0`–`100` | 配置時にプレイヤーから確保する最小距離の目安です。 |
| `DespawnOnEnd` | `true` | `true`, `false` | イベントが出現させた生存中の敵を終了時に消します。 |

#### Minefield

ステージ上のランダム地点に起動済みのバニラ地雷を配置し、設定に応じて作動・破壊された地雷を補充します。イベント終了時は起爆カウント開始済みの地雷だけを残し、未作動の地雷は削除します。

| 設定キー | デフォルト | 範囲 / 設定値 | 内容 |
|---|---:|---|---|
| `Enabled` | `true` | `true`, `false` | Minefieldをイベント抽選に含めます。 |
| `ChancePercent` | `6` | `0`–`100` | 独立発生確率です。 |
| `ProtectValuables` | `true` | `true`, `false` | Minefieldによる貴重品保護を有効にします。 |
| `ExplosiveEnabled` / `ShockwaveEnabled` / `StunEnabled` | `true` | `true`, `false` | 出現可能な地雷種類を選びます。少なくとも1種類を有効にしてください。 |
| `SpawnCountMin` / `SpawnCountMax` | `10` / `15` | `1`–`30` | 起動状態で維持する地雷数です。 |
| `ReplenishIntervalSeconds` | `10` | `1`–`300` | 補充を確認する間隔秒数です。 |
| `MaximumActiveMines` | `30` | `1`–`30` | イベントが同時に維持する地雷の最大数です。 |
| `MinimumPlayerDistance` | `3` | `0`–`100` | 生存プレイヤーから確保する最小距離の目安です。 |
| `ReplenishTriggeredMines` | `true` | `true`, `false` | 作動・破壊された地雷をイベント中に補充します。 |

#### 敵制御イベント

この項目のイベントはすべて`Enabled=true`、`ChancePercent=6`がデフォルトです。

| イベント | 動作 | 追加設定（デフォルト、範囲） |
|---|---|---|
| `Freeze` | 対象の敵をイベント終了まで凍結します。 | 追加設定なし。 |
| `Stun` | 対象の敵をイベント終了までスタンさせます。 | 追加設定なし。 |
| `Enemy Warp` | 一部の敵を有効なランダム地点へ転送します。 | `IntervalSeconds=10`（`1`–`300`）、`EnemiesPerPulse=3`（`1`–`30`）、`MinimumPlayerDistance=3`（`0`–`100`）。 |
| `Enemy Hunt` | 全納品完了後、プレイヤーがいる部屋で誘導音を鳴らし、敵をその場所へ向かわせます。 | `RetargetIntervalSeconds=5`（`1`–`300`）。 |
| `Enemy Speed Up` | 敵の移動速度を上げます。イベント中に現れた敵も対象です。 | `Enabled=true`、`ChancePercent=6`、`SpeedPercent=150`（`101`–`500`）。 |
| `Enemy Speed Down` | 敵の移動速度を下げます。イベント中に現れた敵も対象です。 | `Enabled=true`、`ChancePercent=6`、`SpeedPercent=50`（`10`–`99`）。 |
| `Enemy Regen` | 敵ごとの最大体力を超えない範囲で定期回復します。 | `HealAmount=10`（`1`–`100`）、`HealIntervalSeconds=5`（`1`–`300`）。 |
| `Enemy Purge` | 敵へ定期ダメージを与え、`CanKill`が有効なら倒すこともできます。 | `DamageAmount=10`（`1`–`1000`）、`DamageIntervalSeconds=5`（`1`–`300`）、`CanKill=true`。 |

#### プレイヤーイベント

この項目のイベントはすべて`Enabled=true`、`ChancePercent=6`がデフォルトです。プレイヤー専用イベントのため`Targets > Players`を無視します。

| イベント | 動作 | 追加設定（デフォルト、範囲） |
|---|---|---|
| `Damage Pulse` | 生存プレイヤー全員へ定期ダメージを与えます。 | `DamageAmount=5`（`1`–`100`）、`DamageIntervalSeconds=5`（`1`–`300`）、`SavingGrace=true`。 |
| `Second Chance` | 死亡から2秒後に、その場でプレイヤーを復活させます。遅延復活の待機中はステージ失敗によるリテイクを防止します。開始時点ですでに死亡しているプレイヤーは対象外です。 | `CheckIntervalSeconds=2`（`1`–`300`）、`MaxRevivesPerPlayer=1`（`1`–`10`）。 |
| `Knockback` | 生存プレイヤー全員へランダムな水平方向と上方向の衝撃を与えます。 | `IntervalSeconds=5`（`1`–`300`）、`HorizontalForce=8`（`0`–`100`）、`VerticalForce=3`（`0`–`100`）。 |
| `Flicker` | 生存プレイヤー全員の周囲で、短い赤色ライト点滅を発生させます。MOD未導入の参加者にも表示されます。 | `IntervalSeconds=2`（`1`–`300`）、`IntensityPercent=200`（`1`–`500`）。 |

#### ステージ・物品イベント

この項目のイベントはすべて`Enabled=true`、`ChancePercent=6`がデフォルトです。

| イベント | 動作 | 追加設定（デフォルト、範囲） |
|---|---|---|
| `Quake` | 対象プレイヤーと掴まれていない物理対象へランダムな衝撃を与えます。 | `IntervalSeconds=5`（`1`–`300`）、`Force=8`（`0`–`100`）、`ProtectValuables=true`。 |
| `Door Chaos` | 選ばれた扉を繰り返し開閉します。`Targets > Doors`がオフでも動作し、その設定値は変更しません。トラック、ショップ、納品所の重要扉は除外し、ヒンジ付き貴重品・アイテムは`HingedItemsEnabled`に従います。 | `IntervalSeconds=2`（`1`–`300`）、`AffectedPercent=30`（`1`–`100`）、`Force=12`（`1`–`30`）、`HingedItemsEnabled=true`。 |
| `Value Surge` | 納品所、カート、トラック内を含む有効な貴重品すべての価格を一時的に増加させ、イベント中に生成された貴重品も対象にします。ただし、そのイベント中の納品で生成された金袋には、その時点の倍率を適用しません。 | `MultiplierPercent=150`（`1`–`1000`）、`RestoreOnEnd=true`。 |
| `Value Crash` | 納品所、カート、トラック内を含む有効な貴重品すべての価格を一時的に低下させ、イベント中に生成された貴重品も対象にします。ただし、そのイベント中の納品で生成された金袋には、その時点の倍率を適用しません。 | `MultiplierPercent=50`（`1`–`1000`）、`RestoreOnEnd=true`。 |

#### Restoration / Battery Drain / Heavy Cargo / Butterfingers

各イベントの共通設定は`Enabled=true`（`true`／`false`）、`ChancePercent=6`（`0`～`100`）です。繰り返す効果はイベント開始時と、その後の設定間隔ごとに発生します。修復した価値や消費した電池はイベント終了後もそのままです。

| セクション | 設定 | デフォルト | 範囲 | 説明 |
|---|---|---:|---|---|
| Restoration | `RepairPercent` | `5` | `1`～`100` | 1回につき貴重品の満額の何％を修復するか。満額を超えず、壊れて消えた貴重品は復元しません。Value Surge／Value Crashの価格倍率も解除しません。 |
| Restoration | `IntervalSeconds` | `5` | `1`～`300` | 修復間隔の秒数です。 |
| Battery Drain | `DrainAmount` | `5` | `1`～`100` | 1回で減らす電池残量の割合です。5なら残量が5ポイント減ります。 |
| Battery Drain | `IntervalSeconds` | `4` | `1`～`300` | 電池を減らす間隔の秒数です。 |
| Battery Drain | `MinimumChargePercent` | `0` | `0`～`100` | このイベントによる残量の下限です。通常のアイテム使用ではさらに減ることがあります。 |
| Heavy Cargo | `MassPercent` | `200` | `101`～`500` | 物の重さです。200なら通常の2倍。プレイヤー、敵、扉は対象外です。 |
| Heavy Cargo | `ProtectValuables` | `true` | `true`, `false` | 対象の貴重品を効果中と終了直後に保護します。`Targets > Valuables`も有効にする必要があります。 |
| Butterfingers | `IntervalSeconds` | `8` | `1`～`300` | 手に持った物を落とす間隔です。インベントリ内の物、プレイヤー、敵、扉は対象外です。 |
| Butterfingers | `ProtectValuables` | `true` | `true`, `false` | 落とした貴重品を短時間保護します。`Targets > Valuables`も有効にする必要があります。 |

Heavy Cargoは貴重品、Cosmetic Boxes、アイテム、武器の`Targets`設定に従います。ほかの3種類は固有対象を持つため、主効果は`Targets`を無視します。保護時間はいずれも`Safety > ValuableProtectionReleaseDelaySeconds`（デフォルト2秒、1～5秒）を使用し、Butterfingersでは落とすたびにその時点から数えます。

#### Enemy Blindness / Enemy Armor / Enemy Vulnerability

共通設定は`Enabled=true`（`true`／`false`）、`ChancePercent=6`（`0`～`100`）です。`Targets`に関係なく敵に適用され、途中で現れた敵にも効果が付き、イベント終了時に解除されます。

| セクション | 設定 | デフォルト | 範囲 | 説明 |
|---|---|---:|---|---|
| Enemy Blindness | `VisionPercent` | `25` | `1`～`99` | 敵の視認距離を通常の何％にするか。25なら4分の1です。聴覚と開始済みの追跡は残ります。 |
| Enemy Armor | `DamagePercent` | `50` | `1`～`99` | 敵が受けるダメージの割合。50なら半分です。 |
| Enemy Vulnerability | `DamagePercent` | `200` | `101`～`500` | 敵が受けるダメージの割合。200なら2倍です。 |

敵が元から持つ防御効果と組み合わせて適用し、無敵状態は解除しません。小さなダメージでは端数処理により変化しない場合があります。

#### Supply Drop

ステージ内の適切な場所に、バニラの回復アイテムやトラッカー・道具を生成します。通常のアイテムとして拾うことができ、イベント終了後も残ります。武器、爆発物、アップグレード、他MOD固有アイテムは生成しません。`Targets`を無視します。

| 設定 | デフォルト | 範囲 | 説明 |
|---|---:|---|---|
| `Enabled` | `true` | `true`, `false` | イベント抽選に含めます。 |
| `ChancePercent` | `6` | `0`～`100` | 個別の発生確率です。 |
| `SpawnCountMin` / `SpawnCountMax` | `1` / `2` | `1`～`10` | 1回で生成する数です。最小値と最大値が逆の場合は小さい順に扱います。 |
| `IntervalSeconds` | `15` | `1`～`300` | 生成間隔の秒数です。 |
| `MaximumSpawnsPerStage` | `10` | `1`～`50` | ステージ全体で生成する合計数の上限です。使用・破壊された分も数え、再発動や常時発動モードでも上限は共通です。 |
| `HealthPacksEnabled` | `true` | `true`, `false` | バニラの回復アイテムを生成候補に含めます。 |
| `EquipmentEnabled` | `true` | `true`, `false` | バニラのトラッカー・道具を含めます。少なくとも片方の種類を有効にしてください。 |

#### Player Swap / Shared Pain

共通設定は`Enabled=true`（`true`／`false`）、`ChancePercent=6`（`0`～`100`）です。`Targets`を無視し、抽選時に2人以上の生存プレイヤーが必要です。Player Swapは1回に1組を交換します。立って接地しており、物をつかんでいないプレイヤーが対象で、インベントリ内のアイテムは取り出しません。

| セクション | 設定 | デフォルト | 範囲 | 説明 |
|---|---|---:|---|---|
| Player Swap | `IntervalSeconds` | `15` | `1`～`300` | 位置交換を試みる間隔です。移動先が危険・閉塞している場合は見送ります。 |
| Shared Pain | `DamagePercent` | `25` | `1`～`100` | 実際に失った体力の何％を、ほかの生存プレイヤー各自が受けるか。共有されたダメージや体力の譲渡は再共有しません。 |
| Shared Pain | `MaximumDamagePerHit` | `25` | `1`～`100` | 1回の被弾で各プレイヤーが受ける共有ダメージの上限です。 |
| Shared Pain | `CanKill` | `false` | `true`, `false` | 共有ダメージでの死亡を許可します。無効時は把握している最新体力を基に1以上残るよう制限し、ゲームの致死回避も有効にします。同時に別のダメージを受けると死亡する可能性があります。 |

Shared Painは被弾に反応するため、発生間隔の設定はありません。

#### 貴重品の保護

対象イベントごとに`ProtectValuables`を設定できます。対象はZero Gravity、Roll、Void、Levitation、Shockwave、Stun Blast、Explosion Rain、Minefield、Star Barrage、Dangerous Valuables、Quake、Heavy Cargo、Butterfingersです。Dangerous Valuablesはデフォルト`false`、その他はデフォルト`true`です。イベント側の設定と`Targets > Valuables`の両方が有効な場合だけ保護します。複数エフェクトの同時発生時は、いずれか1つの保護設定が有効なら保護を維持します。すべての保護対象イベントで`Safety > ValuableProtectionReleaseDelaySeconds`を使用します。Butterfingersは落とした貴重品だけを対象に、落とすたびにその時点から保護時間を数えます。

#### Targets

各設定は、Feather、Zero Gravity、Indestructible、Roll、Quakeなど、複数の物理カテゴリへ適用できるイベントの対象を選択します。固有対象を持つBattery Charge、Heal、Fragility、Gumball Hypnosis、Healing Aura、Spider Scare、Traffic Shock、Flicker、敵制御イベント、プレイヤーイベント、Door Chaos、Value Surge、Value Crash、Dangerous Valuablesは`Targets`を無視します。Cosmetic Boxesは複数対象イベントでは一般アイテムと別に指定できます。`Valuables`は貴重品専用イベントの主効果には影響しませんが、貴重品の自動保護は引き続き制御します。

Heavy Cargoもこの設定に従いますが、プレイヤー、敵、扉には適用しません。Restoration、Battery Drain、Butterfingers、Enemy Blindness、Enemy Armor、Enemy Vulnerability、Supply Drop、Player Swap、Shared Painは固有対象へ適用します。

| 設定キー | デフォルト | 内容 |
|---|---:|---|
| `Valuables` | `true` | 複数対象イベントで貴重品を対象にし、適用可能なエフェクトおよび生成型ハザード中の自動保護を有効にします。 |
| `CosmeticBoxes` | `false` | 複数対象イベントでCosmetic Boxesを一般アイテムとは別に対象指定します。 |
| `Items` | `true` | 複数対象イベントで、Cosmetic Boxesを除く一般アイテム、カート、移動可能な小物を対象にします。 |
| `Doors` | `false` | 複数対象イベントで扉、蓋、その他のヒンジで動く物理オブジェクトを対象にします。Door Chaosは無視します。 |
| `Weapons` | `true` | 複数対象イベントで武器を対象にします。 |
| `Players` | `true` | 複数対象イベントでプレイヤーを対象にします。プレイヤー専用イベントは無視します。 |
| `Enemies` | `true` | 複数対象イベントで敵を対象にします。敵専用イベントは無視します。 |

#### Notifications

| 設定キー | デフォルト | 範囲 / 設定値 | 内容 |
|---|---:|---|---|
| `ChatAnnouncementsEnabled` | `true` | `true`, `false` | ステージ開始、エフェクト名、カウントダウン、`End`のチャット通知を有効にします。ホスト側の設定を使用します。 |
| `EnemyReactionEnabled` | `false` | `true`, `false` | Stage Fluxの通知音声に敵が反応するか設定します。通常VCやその他の物音には影響しません。ホスト側の設定を使用します。 |
| `StartCountdownEnabled` | `true` | `true`, `false` | インターバル最後の5秒間に`5`から`0`を通知します。無効時はインターバル終了3秒前にエフェクト名だけを通知します。ホスト側の設定を使用します。 |
| `EndCountdownEnabled` | `true` | `true`, `false` | イベント終了前の3秒カウントダウンを有効にします。ホスト側の設定を使用します。 |

#### HUD

| 設定キー | デフォルト | 範囲 / 設定値 | 内容 |
|---|---:|---|---|
| `Enabled` | `true` | `true`, `false` | 自分の画面にイベントHUDを表示します。 |
| `Style` | `Graphical` | `Graphical`, `Classic` | 5枠のグラフィカルHUDを使用するか、従来のテキストHUDへ戻すかを選択します。 |
| `LayoutDirection` | `Vertical` | `Vertical`, `Horizontal` | グラフィカルHUDの並びと順次停止する方向を選択します。 |
| `Anchor` | `BottomRight` | `TopLeft`, `TopCenter`, `TopRight`, `MiddleLeft`, `MiddleCenter`, `MiddleRight`, `BottomLeft`, `BottomCenter`, `BottomRight` | HUD位置の基準点を選択します。 |
| `Alignment` | `Right` | `Left`, `Center`, `Right` | Classic HUDの文字列の揃え方を選択します。 |
| `OffsetX` | `0` | `-3840`–`3840` | 基準点からの水平方向オフセット（ピクセル）です。 |
| `OffsetY` | `0` | `-2160`–`2160` | 基準点からの垂直方向オフセット（ピクセル）です。 |
| `ScalePercent` | `70` | `50`–`200` | HUDの表示倍率（%）です。 |
| `BackgroundOpacityPercent` | `50` | `0`–`100` | グラフィカルHUD背景の不透明度（%）です。アイコンと文字の不透明度には影響しません。 |

### 通知とHUD

- ステージイベントが有効な場合、ステージ開始から1秒後に有効なプレイヤーのアバターが`Effects`と発言します。
- 次回イベントはインターバル最後の10秒間に抽選します。開始カウントダウン有効時は残り8秒でエフェクト名を通知し、続けて`5`から`0`まで通知して、インターバル終了時に発動します。
- Persistent以外のイベント終了前に、プレイヤーのアバターが`3`、`2`、`1`、`End`と発言します。
- HUDはインターバル中も表示します。残り10秒までは`NEXT`／`READY`、最後の10秒では抽選済みイベントアイコンへスピンします。インターバル開始時点で残り10秒以下なら待機アイコンを省略します。グラフィカルHUDは常に5枠を維持し、ホストが`MaxSimultaneousEffects`を変更するとロック枠を更新します。進行中または確定済みのエフェクトは終了まで表示を維持します。
- アイコン切替時は使用可能枠が同時に回転し、上から下または左から右へ順番に停止します。この表示アニメーションはゲーム上の効果やチャット通知を遅延させません。
- ゲーム内のMOD文字列はすべて英語です。

### 互換性

- DroneToOrbItemと併用できます。DroneToOrbItemが追加するオーブアイテムは通常のアイテム動作を維持し、ステージエフェクトの対象にもなります。
- Elite Enemy Variantsと併用できます。Enemy Waveや敵速度イベントは、敵の変化と共存します。
- RoleShuffleと併用できます。価値変化、復活効果、危険な貴重品、通知が自動的に共存します。
- イベント中に復活したプレイヤーにも、適用可能な発動中エフェクトを適用します。
- イベント中に新しく生成された物理オブジェクトも、適用可能な発動中エフェクトの対象になります。
- 現在のステージ上にある対象を使用するため、他MODで拡張されたステージにも対応します。
