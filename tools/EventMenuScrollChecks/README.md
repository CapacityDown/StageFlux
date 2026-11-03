# Events menu wheel checks

Run from the StagePhysicsEvents project directory:

```powershell
dotnet run --project tools/EventMenuScrollChecks/EventMenuScrollChecks.csproj -c Release -- <REPO_Data/Managed/Assembly-CSharp.dll>
```

The harness reads the installed native wheel expression and executes it after
applying the production Events transpiler, with minimal Unity input/geometry
stand-ins. It checks three body lines (97.5 canvas units) per wheel notch,
fractional and batched input, stale native height, clamping prerequisites,
unrelated menus, and both orderings of an independently scoped peer-menu patch.
No RoleShuffle assembly or source is required to run these checks.

Native input polling, actual Harmony installation, masking, pointer/controller
interaction and rendered appearance still require live-game verification.
