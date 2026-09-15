# Production Release

Milestone 13 provides an Editor-only release boundary. It composes existing framework,
content, accessibility, save, AI, atmosphere, and scene validators before Unity's
`BuildPipeline` runs. Every enabled Build Settings scene is inspected against the shared
framework contract. M12 sample-specific validation runs only when that scene contains the
`M12FirstPersonHorrorTemplate` root. It does not create a runtime service or another save/load path.

## Start A Standalone Game

1. Run `Setus > Horror Framework > Content > Create Standalone Game Folder Template`.
2. Run the relevant framework builders; use the First Person Horror Template only when the game
   starts from the M12 sample.
3. Replace the sample definitions and greybox content under `Assets/Game`; keep framework
   source under `Packages/com.setus.horror-framework`.
4. Author globally unique stable IDs and set each manifest entry's scene ID for every durable
   scene object. Empty scene IDs are legacy single-scene declarations and should not be used for
   new multi-scene content.
5. Run the unified scene/content/accessibility validators.
6. Run `Setus > Horror Framework > Release > Create Missing M13 Defaults` once when the
   project does not already contain its release configuration.
7. Run `Validate Production Readiness`, then EditMode and PlayMode suites.
8. Build and execute the standalone player before release.

## Build Profiles And CLI

The M13 defaults command creates missing game-owned profiles at:

- `Assets/Game/Settings/Build/Production.asset`
- `Assets/Game/Settings/Build/Development.asset`
- `Assets/Game/Settings/Build/PerformanceBudget.asset`
- `Assets/Game/Settings/HorrorFrameworkConfig.asset` when the earlier framework setup has not
  created it yet

Creation is non-destructive: existing profile IDs, targets, output paths, performance budgets,
and framework logging configuration are preserved. Production readiness validation and both
menu/CLI builds are read-only with respect to authored configuration. Invalid or missing
configuration fails with a diagnostic; it is never repaired implicitly during validation/build.

Production disables Development Build, script debugging, profiler connection, the default
debug overlay, and framework category logs. Explicit runtime errors remain available for
fail-safe diagnostics. Development enables debugging but still runs strict validation.

From the project root, build with the project-local Unity CLI:

```sh
python3 Packages/com.setus.horror-framework/Tools~/safe-unity-log.py --output TestArtifacts/M13-build.sanitized.log -- unity run . -- -logFile - -executeMethod Setus.HorrorFramework.Editor.Release.HorrorBuildCommand.BuildFromCommandLine -horrorBuildProfile Assets/Game/Settings/Build/Production.asset -horrorBuildOutput Builds/Production/SetusHorrorGame.app
```

The output override must be project-relative. The command exits through an exception on a
validation or build failure, which makes it suitable for CI.

## Credential-Safe Artifacts

Use the wrapper above for retained build/test command output (Python 3 required).
It filters stdout and stderr before writing or displaying them, preserves the child exit
code, and removes credential-bearing lines, separate option values, and JWTs. Always send
native Editor output through `-logFile -`; never redirect raw CLI output to an artifact.
The live Editor launcher uses the same wrapper. Do not enable shell tracing around credentials.

Upload only explicitly selected test-result XML and reviewed `*.sanitized.log` files.
Never zip the project, `Logs`, or `TestArtifacts` wholesale. Raw logs remain ignored by Git;
ignore rules do not protect arbitrary archive/upload commands. Player distribution must contain
only the intended player output, without test logs or Burst `DoNotShip` directories.
Unity Hub/CLI may maintain their own logs outside this wrapper; review those separately before
sharing support bundles. Pattern filtering cannot identify every arbitrary secret format.
If a raw session credential was exposed, invalidate the session before sharing artifacts.

## Performance Budget

The canonical baseline targets 60 FPS, zero steady-state managed allocation, at most 16
authored raycast sources, 32 active AudioSources, 8 realtime lights, and 4 global Volumes in
Gameplay. Authored-count checks are warnings because measured CPU, GPU, memory, audio and GC
data from the target standalone player remains authoritative.

Profile representative patrol/chase, interaction focus, active scare, pause/UI, save and load
flows. A budget warning must be reviewed with standalone Profiler evidence; it must not be
waived only because the Editor appears smooth.

Audio should retain the M10 category policy: decompress short UI/SFX cues, compress longer
voice/stingers as appropriate, and stream ambience beds. URP brightness and scare reactions
must use the existing Volume hooks; do not add a second post-processing owner for release.

## Release Checklist

- Production readiness validator reports no errors.
- Full EditMode and PlayMode suites pass on the release revision.
- Boot -> Main Menu -> New Game -> Pause -> save/load -> Main Menu -> Continue works.
- Quick Save, Quick Load, and Continue operate on the canonical single V1 slot `primary`.
- Corrupt and incompatible saves fail safely without live-state mutation.
- Door, drawer, pickup, note, objective, phone, checkpoint and route state restore correctly.
- Scares do not replay after consumption; interruption cleanup leaves no tension/camera/light state.
- AI cannot confirm or navigate to live player position through occlusion; anticipation precedes Chase.
- Input remap, subtitles, brightness, comfort and audio settings survive restart.
- Debug overlays are off by default and framework category logging is `None` in Production.
- Audio, URP, memory, GC, CPU and GPU budgets are profiled in the standalone player.
- Production player launches outside the Editor and completes the smoke flow without new errors.
- Build output, version, platform identifier, credits, licenses and store metadata are reviewed.
