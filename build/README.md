# CI/CD

Two GitHub Actions workflows live in `.github/workflows`. They're built on Pathoschild's
[SMAPI-ModBuildWorkflow](https://github.com/Pathoschild/SMAPI-ModBuildWorkflow) actions.

| Workflow | Runs on | Does |
|---|---|---|
| **Build** (`build.yml`) | pushes to `master`, `release/**`, `deployCheck/**`; pull requests; manual | Builds the whole solution and uploads each mod's zip as its own artifact. Non-release builds get preview versions (`2.0.1-alpha.202609222359`). A release branch builds its mod with the release version, creates a [build attestation](https://docs.github.com/en/actions/concepts/security/artifact-attestations) and uploads a `Release` artifact. |
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
- To put several mods in one GitHub Release, join them with `+`, each with its own version:
  `release/UIFramework/1.2.0+ProfitCalculator/2.1.0`. The tag is `UIFramework/v1.2.0+ProfitCalculator/v2.1.0`.
- To release mods separately but at the same time, push one branch each:
  `git push origin master:release/UIFramework/1.2.0 master:release/ProfitCalculator/2.1.0`.
- The build writes the version into the mod's `.csproj` (`build/Set-ReleaseVersion.ps1`). That change only happens in CI, so also commit the version bump if you want it in the repository.
- Publishing fails if the tag already exists, so an existing release is never overwritten.
- Upload the zip from the GitHub Release (or the build's artifact) to Nexus rather than rezipping it yourself, so it still matches the attestation.

## Notes

- The reference assemblies are only updated upstream when the game or SMAPI changes. If a mod starts using an API newer than they cover, CI fails until they're updated.
