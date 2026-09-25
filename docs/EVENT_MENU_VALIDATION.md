# Events menu validation

Version remains 4.3.0. RoleShuffle source was read for UI behavior but not modified.

## Implemented

- Lobby and Escape-menu Events entry. Its position is recalculated from an existing Roles button's bounds; no direct RoleShuffle reference is used.
- Current events, all 46 icon/description/risk entries, host-only toggles and ON/OFF presets.
- Same config entries as REPOConfig; settings are saved once per action, with rollback on save failure.
- Display-only host settings shared through versioned room data. Clients never mutate event settings; unsupported or stale-host data is shown as unavailable.
- Display refresh, real-renderer HUD editing with a separate draft, Save/Cancel/defaults, and local bounded/redacted problem reports.
- The HUD editor never starts events. It supports Graphical/Classic, orientation, anchor, alignment, position, scale, opacity, and local visibility.
- Reports and support-page actions require a user click. No report upload occurs.
- MenuLib is a shared dependency; RoleShuffle and Elite Enemy Variants remain optional. No new gameplay RPCs are required from vanilla clients.

## Automated checks

Release build: zero warnings and zero errors. Regression checks include authority, single-player and guest behavior, full 46-bit settings payload, all-off state, host changes, malformed payloads, guide coverage, config-key mapping, optional-mod assembly references, resource coverage and existing event/game contracts.

## Not verified in this session

Unity-rendered menu appearance, in-game mouse/controller interaction, resolution changes, and multiplayer lobby/host-migration behavior require the manual checks in DEVELOPMENT.md. A successful compile and static/isolated tests do not constitute an in-game playtest.
