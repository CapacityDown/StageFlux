# Development

## Build

1. Install the .NET SDK (the regression test runner targets .NET 9).
2. Install MenuLib through the R.E.P.O. mod manager, then copy its `MenuLib.dll` into this project's `lib` directory. The dependency is not bundled in this repository or the Stage Flux release DLL.
3. Run `dotnet restore StagePhysicsEvents.csproj` and `dotnet build StagePhysicsEvents.csproj -c Release`.

The project obtains its compile-time BepInEx, Unity and game libraries through its configured NuGet feeds. Never commit copies of the installed game's assemblies.

## Verification

Run the regression suite with the project path and your installed game's managed assembly path:

```text
dotnet run --project tests/StageFlux.ExtendedEvents.Tests.csproj -c Release -- <project-directory> <REPO_Data/Managed/Assembly-CSharp.dll>
```

The suite covers arithmetic, menu authority and display payloads, event resources, optional-mod references, and installed-game method signatures. It does not replace Unity or multiplayer testing.

Manual UI checks:

- With and without RoleShuffle, open the lobby and pause menus. Events must be below Roles when present, or in the top-right corner otherwise.
- Open the event guide, scroll through all 46 entries, and verify text wrapping and icon direction.
- As host, toggle events and reopen REPOConfig. Confirm the same settings changed. As guest, confirm host settings are read-only.
- Connect to a host without menu support: the guide and local tools remain available; unknown host settings must not appear as local defaults.
- Open the HUD editor in both lobby and stage at 1080p, 1440p, and another aspect ratio. Test drag, layout/style, opacity, Save, Cancel, Escape, and resolution changes. Cancel must not save drafts.
- Refresh display data and create a report. Review its redaction; no report is uploaded automatically.
- End a stage, leave a room, change host, and reopen the UI to check stale state handling.

## Release

Keep `StagePhysicsEventsPlugin.PluginVersion`, the project version, and `package/manifest.json` aligned. The Thunderstore package name is StageFlux; the DLL name remains StagePhysicsEvents.dll.

After a clean Release build, update the package and the requested mod-manager profile. Preserve local configuration and back up any DLL being replaced. Verify identical SHA-256 hashes for build, package, and deployed DLLs. Do not publish a release merely because code was pushed to GitHub.

Do not add compile-time references or mandatory dependencies for RoleShuffle or Elite Enemy Variants.
