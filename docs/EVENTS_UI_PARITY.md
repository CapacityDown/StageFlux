# Events UI format alignment

Date: 2026-11-03. Stage Flux remains v5.0.0.
Reference: RoleShuffle v5.0.1 build592, source commit `9da051e`.
Only StagePhysicsEvents sources were changed.

## Aligned presentation and behavior

- Right-side MenuLib popup, 24-unit top mask padding, navigation starting at
  y=272 with a 32-unit step, 190 x 32 navigation buttons, selected `>` markers,
  and the version/back controls at y=54/y=18.
- Body text at 18 units, headings at 20, 25-unit body rows, 3-unit padding,
  1.5-unit row spacing, and 20-unit padding before the first row.
- Guide headings have 64-unit rows and 56-unit icons; settings use 56-unit
  rows with 48-unit icons. Disabled settings use separately cached grayscale
  textures without altering normal HUD/guide icons.
- Button borders, hover colors, OFF blue, unavailable gray, and section-divider
  styling match the Roles implementation. The button visual class and wheel
  helper were compared after identifier-only renaming and matched exactly.
- Pause entry opens Current Events; lobby entry opens the guide and disables
  the current-events tab. Overview/Details/Draw eligibility use the same selected
  mode convention. The initial alignment retained English; the later localization
  update below supersedes that presentation choice.
- Native two-option external-site confirmation, per-page callback checks, and
  Escape guards prevent actions from stale pages or closing the underlying page.
- Scrolling moves three body rows (97.5 canvas units) per wheel notch and
  preserves fractional/batched wheel magnitude. Keyboard/dragging/native bounds
  remain untouched; unrelated menu scroll boxes retain their original result.

## Event-specific limits and safety

- The menu retains four event-specific navigation sections. Role assignments,
  base upgrades, role testing, and upgrade history are not transplanted into Events.
- Event-specific HUD editor controls remain available with existing preview,
  Save/Cancel, layout and opacity options; no gameplay effect starts in the editor.
- Details are the host's current event configuration, not a claim that an already
  selected stage plan has been updated. Guests never fall back to their own local
  values as host values. Missing/unsupported/stale host snapshots show unavailable.
- Draw eligibility is explicitly configuration-based. It does not resolve
  prefabs, initialize adapters, consume RNG, reveal scheduled events early, or
  guarantee that stage resources/targets exist when an actual draw is performed.
- `SF.Menu` remains unchanged. An optional, bounded `SF.Guide` room property
  carries display-only values. No required client RPC, RoleShuffle assembly
  reference, or new mandatory dependency was added.
- Three local custom slots save stable event names only. Save/apply rechecks host
  authority. Saved all-OFF differs from an unused slot; unknown names cannot enable
  another event. Existing slots require confirmation, writes retain a backup,
  and unreadable/future-format files are preserved rather than overwritten.
- Current-event previews still respect the final-ten-second reveal threshold;
  crossing that threshold now refreshes the page even if the event mask is unchanged.

## Automated verification

- Release rebuild: zero warnings and errors.
- 3,030 arithmetic, integration, resource, version, UI/preset/snapshot and
  installed-game contract checks passed.
- 269 native-wheel checks passed, including executing the actual installed-game
  expression with fractional/batched input and both independently scoped
  peer-patch ordering cases. The peer is a minimal test model, not a live loaded mod.
- README is under 100,000 characters and contains no embedded images; CHANGELOG
  remains English with no blank line after version headings.

## Runtime acceptance still required

No live Unity rendering, pointer/controller, or vanilla-client multiplayer test
was performed for this change. At 1080p and 1440p, compare the two menus' spacing,
hover/selection states, scrolling, wrapping, and icon masking. Exercise preset
save/reload/overwrite/cancel, host changes, older hosts, local HUD save/cancel,
and confirm/cancel/Escape on the external-site dialog. See DEVELOPMENT.md.

Deployed to SF_TEST with user configuration unchanged. Build, package, and
SF_TEST DLL SHA-256 match. Prior DLLs and deployed documents were backed up under
`output/deploy-backups/events-ui-parity-20261103-205502/`. Default and other
profiles were not modified.

Validated SHA-256:

`FB4BD38B45F8CA2046264105A1CCCB620246009BCCFDCD7A432CA782408EDF2C`

## Subsequent localization and cumulative build-number update

See [EVENTS_LOCALIZATION_VALIDATION.md](EVENTS_LOCALIZATION_VALIDATION.md).
Tools now shares Roles' y=112 position. All menu labels, explanations and
HUD-editor controls follow the local game language with the same private font
fallbacks and native text shaping. The native corner button stays `Events`.
The version/footer now includes a cumulative UI-only build number; normal MOD
version, configuration and package identity remain v5.0.0.
