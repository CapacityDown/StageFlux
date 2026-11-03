# REPOConfig presentation audit

## Scope

- Stage Flux v5.0.0: 49 events, 292 live settings.
- No config key, section identity, default, range, migration revision, event order,
  selection logic, or saved value changes. No additional mod dependency.
- Common sections: General, Timing, Targets, Safety, Notifications, HUD.
- Event sections derive directly from `StageEffectSet.IndividualEffects` and its
  formatted names, exactly as the in-game Event Guide. Do not copy the order of
  the catalog's description switch or the constructor's historical bind calls.
- Enabled/Chance first; amount/strength/counts and caps; event repetition;
  distance; grouped allowed types; safety and end behavior. Each min/max pair
  remains adjacent. Unknown future settings remain visible in a stable order.
- Event Guide Details shares the entry sorter, including the host snapshot
  displayed to modded guests. It does not change how settings are synchronized.

## Integration

The installed REPOConfig 1.2.7 groups `ConfigEntryBase[]` by section in arrival
order and does not read ConfigurationManager ordering tags. A reflection-resolved
Harmony prefix sorts the `CreateModEntries(REPOPopupPage, ConfigEntryBase[])`
display argument only if **every** entry belongs to the actual Stage Flux
ConfigFile. Other mods, mixed-owner arrays, and empty arrays are left untouched.
The same entry objects remain in use for edit, save, revert, and reset-to-default.
No backing dictionary, property metadata, or config file is reordered or rebound.

Absent/incompatible REPOConfig leaves its native order in place without disabling
gameplay. A failure is caught and logged once. Cleanup removes the plugin's patch
and releases its config reference. No RoleShuffle/EEV source or UI is changed.
Installation runs in Unity Start, after Chainloader finishes loading all plugins;
REPOConfig does not have to precede Stage Flux in Awake order.

## Automated verification

`EventConfigOrderChecks` is part of the Release regression suite. It audits all
292 current bindings, all 49 section positions, enable/chance priority, adjacent
min/max entries, randomized insertion orders, culture-independent sorting,
unknown settings, reference/value preservation, and the actual installed
REPOConfig method signature and grouping IL. Assembly references remain optional.
Main Release builds are recorded through `tools/build-stageflux.mjs`.

Verified on 2026-11-03: v5.0.0 build-244, Release warnings/errors 0/0,
11,081 regression checks passed. Build, package and SF_TEST DLL SHA-256:
`3E89732D088F9673CBFE107CF9ECEE87B1716DD8CB35D67F9F83A9730F4C5D7E`.
SF_TEST configuration hash remained unchanged; running EP_TEST was untouched.

This is offline verification, not a live-game UI test. In SF_TEST, check:

1. Open Mods > Stage Flux, compare event section order with Events > Event Guide.
2. Verify grouped types in Dangerous Valuables, Minefield, Supply Drop, Enemy Wave.
3. Change a setting, save/reopen, then revert an unsaved edit. Confirm each action
   still affects the intended setting. Compare Events > Details.
4. On a disposable test configuration, Reset To Default must restore Stage
   Activation Chance to 50 and event Chance to 6, with no hidden revision row.
5. Open another mod's configuration and confirm its presentation is unchanged.
