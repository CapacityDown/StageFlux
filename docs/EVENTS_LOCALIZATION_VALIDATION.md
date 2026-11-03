# Events localization and build accounting

Stage Flux v5.0.0; implementation limited to StagePhysicsEvents.

## Presentation

Reference: existing RoleShuffle Roles UI, build592 (`9da051e`). Same navigation
geometry, Tools y=112, version/back placement, row dimensions, 18/20-unit text,
OFF/hover borders, icons, and native-wheel behavior. The event-specific sections
and HUD editor remain, without importing role gameplay features.

All 21 native R.E.P.O. 0.5.0 locales have 170 translation entries, covering all
49 event explanations, menu controls, warnings and local HUD-editor labels.
The local game locale is read when opening and refreshing the menu. Locale
changes rebuild the view, clear transient feedback and reset scroll position.
Event names, configuration identifiers, mode identifiers and diagnostics remain
canonical English; the existing stage HUD and spoken notifications are unchanged.
Host snapshots are not translated before transmission. Unknown/future host text
falls back to the original rather than substituting local settings.

Private cloned fonts preserve the native primary face. Japanese uses the same
Checkpoint Revenge bundle; Noto fallback fonts cover the other writing systems,
with Roles' 0.72 face scaling. Arabic shaping/bidi and Thai marks use native TMP
text shaping. CJK/Thai tokens and grapheme-safe wrapping preserve characters.
No global font settings, required optional-mod references or gameplay behavior
were changed. Font notices are embedded and included in package/LICENSES.

## Retrospective counter

`docs/build-history.json` contains 240 confirmed earlier main-project builds:
219 successful and 21 failed. Matching execution records were deduplicated by
tool-call identity, with asynchronous wait results joined to their original
invocation. Compile failures count. Two commands that never reached dotnet
(approval rejection and an earlier patch failure) do not. Test-project builds,
documentation mentions, and inspection commands do not count. External records
that are unavailable cannot be reconstructed; the ledger does not claim otherwise.
Public evidence is limited to timestamps, outcomes and hashed identifiers.

`tools/build-stageflux.mjs` reserves a new number under an exclusive lock before
each main build, then records success or failure. The first new build is 241.
The number appears in Events and local bug reports, not in PluginVersion,
manifest/package version or assembly version. Test-only runs do not advance it.

## Verification

- Release build-241: zero warnings, zero errors.
- 10,619 regression checks: event contracts, embedded translations, all locale
  mappings, aliases, placeholders, fallback, canonical host numbers/mode IDs,
  grapheme wrapping, menu authority and build-ledger consistency.
- 269 native-wheel checks, including peer patch orders.
- 3,570 catalog entries checked for equal keys and placeholder integrity;
  bundled Noto cmap coverage checked. All Japanese catalog glyphs exist in both
  Checkpoint Revenge faces (7,140 characters each).
- No RoleShuffle or EliteEnemyVariants compile-time reference added.

Live Unity rendering, native-speaker proofreading, controller/pointer operation,
language switching and vanilla-client multiplayer acceptance remain unperformed.
At 1080p/1440p, compare Roles and Events in all views, including Arabic alignment,
Thai marks, long headers, host-only toggles, Save/Cancel and the GitHub modal.

## Deployment

Updated package and SF_TEST on 2026-11-03; Default and other profiles untouched.
Backups: `output/deploy-backups/events-localization-20261103-231753/`.
The SF_TEST configuration SHA-256 remained unchanged before and after copying.
Fonts are embedded in the DLL; notices are also copied under plugin/LICENSES.

Build, package and SF_TEST DLL SHA-256:

`6425D38C754CD402DD59DB5CF4264FFA2E63CBE7C088064A9F051646FFEB5281`
