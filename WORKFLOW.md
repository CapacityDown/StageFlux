# Stage Flux working methods

On 2026-11-03 the user requested the same GitHub workflow as RoleShuffle.
These rules apply to Stage Flux only; do not change the RoleShuffle repository.
Newer user instructions take precedence over the current version or profile.

## Repository and identity

- Work in `StagePhysicsEvents`, connected to the existing public repository
  `https://github.com/CapacityDown/StageFlux.git` through `origin`.
- Use the same existing Git identity as RoleShuffle: `CapacityDown`, with its
  GitHub noreply email. Keep authentication in the existing credential provider;
  never write tokens into source files, remote URLs, logs, or release packages.
- Retain the Stage Flux name, current version **5.0.0**, assembly name
  `StagePhysicsEvents.dll`, and existing configuration identity.

## Version branches

- Develop on `version/<version>`; the current branch is `version/5.0.0`.
  Commit related, verified changes and normal-push them to the matching remote
  branch with upstream tracking. Check that the remote commit matches local HEAD.
- Before changes, inspect the worktree, index, branch, and remote. Fetch before
  synchronization; use fast-forward-only updates when possible. Preserve dirty
  or unrelated work. Do not use destructive resets, forced checkouts, or force pushes.
- On adoption, `main` already contains the initial v5.0.0 migration at
  `0ec35746f74741a8f6d3d80d1c531accf5b896cc`. Preserve that history; place the
  subsequent v5.0.0 event additions and ongoing work on `version/5.0.0`.
- When the user changes the version, finish the appropriate verification,
  integrate the outgoing version branch into `main`, push it, and verify the
  remote. Create the incoming version branch from updated `main`, switch to it,
  and make version changes there. Do not integrate unfinished work into `main`
  after each ordinary commit. If branches diverge, inspect and preserve both
  histories rather than rewriting them.
- Preserve prior version branches and release artifacts. Before any separately
  requested bulk cleanup, create and verify a complete Git bundle and inspect
  branches and worktrees. Never remove unmerged, active, or explicitly retained
  archive/release branches.
- Ordinary commits and pushes do not authorize creating a public release,
  uploading a Thunderstore package, or changing repository visibility.

## Validation and deployment

- Follow [DEVELOPMENT.md](DEVELOPMENT.md) for dependencies, builds, and checks.
  Use `node tools/build-stageflux.mjs` for main-project builds so successful and
  failed attempts both advance the cumulative Events UI build number. Commit
  the counter and sanitized ledger together; test-only builds do not count.
  Compile against installed R.E.P.O. v0.5.1 assemblies and require zero warnings
  and errors in Release. Run the regression suite for code changes; distinguish
  offline validation from live-game and vanilla-client multiplayer testing.
- Back up replaced package/deployment files. Update `package/StagePhysicsEvents.dll`
  and deploy to **SF_TEST**, not the previously used Default profile. Preserve
  user configuration and verify build/package/deploy SHA-256 equality.
- Do not stop a running game. If it uses SF_TEST or its profile is unknown,
  leave deployment pending; an explicitly identified different profile remains
  untouched. Documentation-only changes do not require a DLL rebuild or redeploy.
- Keep RoleShuffle and Elite Enemy Variants optional and reflection-only.
  Preserve host-only operation without requiring mods on vanilla participants.

## Tracked files and public documents

- Track source, project files, tests, developer tools, approved icon masters and
  runtime images, and package documents/manifest/icon. Review explicit file lists
  before committing; do not blindly stage unrelated workspace contents.
- Keep game DLLs, local configuration, logs, reports, credentials, build output,
  deployment backups, generated PDFs, and ZIPs out of Git, as defined by `.gitignore`.
- Keep technical implementation and verification notes in development documents,
  not the player-facing `package/README.md` or `package/CHANGELOG.md`.
- Package README remains English followed by Japanese, without embedded images.
  The Events menu follows the game's local language setting; event names,
  configuration identifiers, the stage HUD and spoken notifications retain
  their existing English naming. Do not change gameplay to localize the menu.
  CHANGELOG remains English only with no blank line between each version heading
  and its first bullet. Compatibility descriptions do not include other mod versions.
- Direct questions, feedback, and bug reports to
  [GitHub Issues](https://github.com/CapacityDown/StageFlux/issues).
  Review reports before sharing; do not upload player reports automatically.
