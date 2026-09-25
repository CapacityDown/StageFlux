# Stage Flux Event Icons

Unified UI icons for every Stage Flux event and the waiting state.

## Files

- `masters/`: high-resolution source images
- `Runtime/`: PNG images embedded in the mod (new artwork is packaged at 512 x 512)
- `256/`: archived earlier exports, not used by the current HUD
- `v4.3.0-generation.json`: built-in image generator prompts and source paths for the ten additional icons
- `contact-sheet.png`: archived overview of the earlier icon set

## Visual Style

- Transparent background; no embedded frame, medallion or enclosing circle
- Worn industrial materials with clear silhouettes
- Cream highlights and restrained cyan, amber, green or red accents
- Stylized 3D game UI rendering
- No text embedded in the runtime icons

## Runtime Mapping

The HUD loads `Runtime/<StageEffect enum name>.png`. The older export table below is kept as an archive. New runtime assets are:

Restoration, BatteryDrain, HeavyCargo, Butterfingers, EnemyBlindness, EnemyArmor,
EnemyVulnerability, SupplyDrop, PlayerSwap and SharedPain.

| State / Event | Icon |
|---|---|
| Waiting | `256/Waiting.png` |
| Feather | `256/Feather.png` |
| ZeroGravity | `256/ZeroGravity.png` |
| Battery Charge | `256/Battery.png` |
| Heal | `256/Heal.png` |
| Indestructible | `256/Indestructible.png` |
| Fragility | `256/Fragility.png` |
| Roll | `256/Roll.png` |
| Void | `256/Void.png` |
| Levitation | `256/Levitation.png` |
| Shockwave | `256/Shockwave.png` |
| StunBlast | `256/StunBlast.png` |
| ExplosionRain | `256/ExplosionRain.png` |
| EnemyWave | `256/EnemyWave.png` |
| Minefield | `256/Minefield.png` |
| Freeze | `256/Freeze.png` |
| Stun | `256/Stun.png` |
| EnemyWarp | `256/EnemyWarp.png` |
| EnemyHunt | `256/EnemyHunt.png` |
| EnemyRegen | `256/EnemyRegen.png` |
| EnemyPurge | `256/EnemyPurge.png` |
| DamagePulse | `256/DamagePulse.png` |
| SecondChance | `256/SecondChance.png` |
| Knockback | `256/Knockback.png` |
| Flicker | `256/Flicker.png` |
| Quake | `256/Quake.png` |
| DoorChaos | `256/DoorChaos.png` |
| ValueSurge | `256/ValueSurge.png` |
| ValueCrash | `256/ValueCrash.png` |
| GumballHypnosis | `256/GumballHypnosis.png` |
| HealingAura | `256/HealingAura.png` |
| StarBarrage | `256/StarBarrage.png` |
| SpiderScare | `256/SpiderScare.png` |
| TrafficShock | `256/TrafficShock.png` |
| DangerousValuables | `256/DangerousValuables.png` |
