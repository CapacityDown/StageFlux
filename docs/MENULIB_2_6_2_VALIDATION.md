# MenuLib dependency update — 2026-11-05

- Thunderstore's official package API reported **REPOModding-MenuLib-2.6.2** as
  the latest active release on 2026-11-05. Source:
  <https://thunderstore.io/api/experimental/package/REPOModding/MenuLib/>.
- Downloaded the official 2.6.2 package and verified its manifest and embedded
  BepInPlugin identity: `nickklmao.menulib`, `Menu Lib`, `2.6.2`.
- Updated Stage Flux's package dependency, BepInEx minimum dependency, local
  build reference, development instructions and metadata contract tests together.
- Stage Flux remains **5.0.0**, now **build-247**. MenuLib is not bundled in the
  Stage Flux release package or committed to Git. REPOConfig remains unchanged.

## Verification

- Release: **0 warnings, 0 errors**.
- **11,129** regression, resource, version and installed-game checks passed,
  including MenuLib and REPOConfig references against R.E.P.O. 0.5.1.
- **269** native scroll checks passed, including both peer patch orders.
- The test suite now verifies the actual build-reference MenuLib plugin identity
  and version as well as the Stage Flux manifest and dependency attribute.
- Live Unity rendering and multiplayer were not exercised in this update.

## Deployment

Updated package and SF_TEST Stage Flux DLL/manifest/changelog, plus SF_TEST's
MenuLib DLL and official package files. Updated only the MenuLib installed-version
entry and Stage Flux dependency in SF_TEST's `mods.yml`; REPOConfig's declared
minimum dependency remains its original value. All profile config files retain
their hashes. Default, other profiles, RoleShuffle and EEV were not modified.

Stage Flux build/package/SF_TEST DLL SHA-256:
`73128B2736720261FDB94D40B298D642E75F5B89EB6C5B090B7FB6675261C33E`.

MenuLib official download/build reference/SF_TEST DLL SHA-256:
`173C7E41193FF1A7F9B1BBBC93CDB917860A6D37E389802FFA2F05223647982C`.

Previous build reference and profile files are recoverable from the local,
untracked `output/deploy-backups/menulib-262-20261105-232450` and
`output/deploy-backups/menulib-262-deploy-20261105-232556` directories.
No release ZIP or public release was created.
