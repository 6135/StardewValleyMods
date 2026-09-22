# CI/CD

Two GitHub Actions workflows live in `.github/workflows`. They follow Pathoschild's
[SMAPI-ModBuildWorkflow](https://github.com/Pathoschild/SMAPI-ModBuildWorkflow) conventions, but only use GitHub's own
actions (the repository only allows actions owned by 6135 or GitHub), with the version logic in `build/*.ps1`.

| Workflow | Runs on | Does |
|---|---|---|
| **Build** (`build.yml`) | pushes to `master`, `release/**`, `deployCheck/**`; pull requests; manual | Builds the whole solution and uploads the mod zips as the `Build` artifact. Non-release builds get preview versions (`2.0.1-alpha.202609222359`). A release branch builds its mod with the release version, creates a [build attestation](https://docs.github.com/en/actions/concepts/security/artifact-attestations) and uploads a `Release` artifact. |
| **Publish** (`publish.yml`) | a successful **Build** of a pushed `release/**` branch | Creates a GitHub Release (tag `<mod>/v<version>`) with the mod's zip and a link to its attestation. |

No game install or secrets are needed: CI compiles against the public reference assemblies in
[StardewModders/mod-reference-assemblies](https://github.com/StardewModders/mod-reference-assemblies).

## Versions

Each mod's version lives in its `.csproj` (`<Version>`), and its `manifest.json` uses `"Version": "%ProjectVersion%"`.
ModBuildConfig fills the version in when it deploys to your game folder and when it packs the release zip.

## Releasing a mod

Push a branch named `release/<mod>/<version>`:

```bash
git switch -c release/ProfitCalculator/2.1.0
git push -u origin release/ProfitCalculator/2.1.0
```

- `<mod>` is the `.csproj` name, compared ignoring case and punctuation, so `release/cp-sweet-mines/1.0.1` releases `[CP] Sweet Mines`.
- `<version>` may have a `v` prefix. A prerelease suffix (`1.2.0-beta.1`) marks the GitHub Release as a prerelease.
- The build writes the version into the mod's `.csproj` (`build/Set-ReleaseVersion.ps1`). That change only happens in CI, so also commit the version bump if you want it in the repository.
- Publishing fails if the tag already exists, so an existing release is never overwritten.
- Upload the zip from the GitHub Release (or the build's artifact) to Nexus rather than rezipping it yourself, so it still matches the attestation.

## Notes

- The reference assemblies are only updated upstream when the game or SMAPI changes. If a mod starts using an API newer than they cover, CI fails until they're updated.
- The tests in `StardewUIFramework.Tests` need the real game at run time, so CI builds them but doesn't run them.
