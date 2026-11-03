# Development

## Source and GitHub

Follow [WORKFLOW.md](WORKFLOW.md) for the version-branch workflow shared with RoleShuffle. Work in `StagePhysicsEvents` on `version/5.0.0`; commit and push current development to that branch rather than directly to `main`. The remote remains `https://github.com/CapacityDown/StageFlux.git`.

## Build

1. Install the .NET SDK (the regression test runner targets .NET 9).
2. Install R.E.P.O. **0.5.0** (Unity 6), MenuLib **2.6.0**, and REPOConfig **1.2.7**. Copy MenuLib's `MenuLib.dll` into this project's `lib` directory. These dependencies are not bundled in this repository or the Stage Flux release DLL.
3. Set `RepoGameManagedDir` to the installed game's `REPO_Data/Managed` directory. Use `-p:RepoGameManagedDir="..."` on build commands, or create the Git-ignored `Directory.Repo.props` locally:

```xml
<Project>
  <PropertyGroup>
    <RepoGameManagedDir>C:/Path/To/REPO/REPO_Data/Managed</RepoGameManagedDir>
  </PropertyGroup>
</Project>
```

4. Install Node.js, then run `dotnet restore StagePhysicsEvents.csproj` and
   `node tools/build-stageflux.mjs -c Release --no-restore -t:Rebuild -warnaserror`.
   Use the wrapper for every main-project build. It increments the UI-only
   build number **before** invoking the compiler, including failed attempts,
   and records the result in `docs/build-history.json`. Test-only builds do not
   increment it. Never reset it when changing the package version.

Only BepInEx and build tooling come from NuGet. Game, Unity, and Photon references come from the selected installation; there is no fallback to outdated game NuGet packages. The build publicizer creates an intermediate reference copy in `obj` and does not change the installed game. Never commit or distribute game assemblies, publicized references, decompiled source, or the local path configuration.

## Verification

Run the regression suite with the project path and your installed game's managed assembly path:

```text
dotnet run --project tests/StageFlux.ExtendedEvents.Tests.csproj -c Release -- <project-directory> <REPO_Data/Managed/Assembly-CSharp.dll>
```

The suite covers arithmetic, menu authority and display payloads, event resources, optional-mod references, and installed-game method signatures. The 0.5.0 audit also resolves Stage Flux/MenuLib member references, reflection targets, Harmony injection parameters, and vanilla RPC contracts against the original game DLLs. Additional dependency DLL paths can be appended to the test command. It does not replace Unity or multiplayer testing.

The Events format follows the Roles menu: selected navigation markers,
outlined hover controls, disabled icons, fixed body rows, and three body lines
per wheel notch. The independent native-wheel harness also exercises both peer
patch orders without requiring RoleShuffle:

```text
dotnet run --project tools/EventMenuScrollChecks/EventMenuScrollChecks.csproj -c Release -- <REPO_Data/Managed/Assembly-CSharp.dll>
```

Custom presets store stable event names in a separate profile-local file, not
REPOConfig entries. The optional guide snapshot is display-only and never calls
event draws, RNG, or effect availability initialization. Eligibility is explicitly
configuration-based; it does not promise live target/resource availability.

Migration notes and the required in-game acceptance checks are in [docs/GAME_0_5_MIGRATION.md](docs/GAME_0_5_MIGRATION.md).

Manual UI checks:

- With and without RoleShuffle, open the lobby and pause menus. Events must be below Roles when present, or in the top-right corner otherwise.
- Open the event guide, scroll through all 49 entries, and verify text wrapping and icon direction.
- As host, toggle events and reopen REPOConfig. Confirm the same settings changed. As guest, confirm host settings are read-only.
- Connect to a host without menu support: the guide and local tools remain available; unknown host settings must not appear as local defaults.
- Open the HUD editor in both lobby and stage at 1080p, 1440p, and another aspect ratio. Test drag, layout/style, opacity, Save, Cancel, Escape, and resolution changes. Cancel must not save drafts.
- Refresh display data and create a report. Review its redaction; no report is uploaded automatically.
- End a stage, leave a room, change host, and reopen the UI to check stale state handling.
- Compare Roles and Events at 1080p and 1440p: navigation spacing, selected arrows,
  control borders/hover colors, OFF icons, dividers, 56-unit guide icons, and body
  wrapping. The menu follows each local player's native game language. Test
  Japanese, English, Chinese, Korean, Arabic and Thai at minimum, then switch
  languages while the menu is open. Event names and configuration identifiers
  remain English; the live event HUD and spoken notifications are unchanged.
- Save/apply all three custom slots, confirm/cancel overwrites, restart the
  profile, and verify only ON/OFF settings change. Guests must not save/apply
  host selections. Test all-OFF, unknown older names, and unreadable preset files.
- On a modded guest, compare Details with the host's REPOConfig. Change host or
  join an older host and confirm unavailable details never use guest-local values.
- Test the native GitHub dialog with confirm, cancel, and Escape; the underlying
  Events page must remain open until explicitly closed.

Localization resources, font provenance and checks are documented in
[Assets/Localization/README.md](Assets/Localization/README.md) and
[Assets/Fonts/Localization/README.md](Assets/Fonts/Localization/README.md).
The initial build counter was reconstructed from available execution logs:
240 confirmed main-project builds (219 successful, 21 failed). Two commands
that never reached the build process were excluded. No claim is made about
unavailable external build logs. Public history contains timestamps and hashed
evidence identifiers only, never raw conversation or command output.

## Release

Keep `StagePhysicsEventsPlugin.PluginVersion`, the project version, and `package/manifest.json` aligned. The Thunderstore package name is StageFlux; the DLL name remains StagePhysicsEvents.dll.

After a clean Release build, update the package and the requested mod-manager profile. Preserve local configuration and back up any DLL being replaced. Verify identical SHA-256 hashes for build, package, and deployed DLLs. Do not publish a release merely because code was pushed to GitHub.

The current deployment target is **SF_TEST**, following the user's override of Default. Do not modify Default or another active profile. If R.E.P.O. is running, identify its profile before deployment; defer replacement if it uses SF_TEST or its profile cannot be established. Do not stop the game automatically.

Do not add compile-time references or mandatory dependencies for RoleShuffle or Elite Enemy Variants.
