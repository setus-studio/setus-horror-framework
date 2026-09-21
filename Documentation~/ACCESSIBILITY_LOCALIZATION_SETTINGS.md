# Accessibility, Localization, and Settings

## Ownership

`RuntimeSettingsModel` is the only durable settings owner. UI controls write to that model. Player,
camera, audio, display, localization, and input adapters subscribe to or read the model and do not
persist parallel copies. Runtime bootstrap loads and atomically updates app-wide preferences at
`Application.persistentDataPath/SetusHorrorFramework/user-settings.json`. A `.bak` document is the
last-known-good fallback when the primary file is corrupt. Runtime changes apply immediately; disk
writes are coalesced until the Settings panel closes, a rebind completes, the app pauses/quits, or
the framework context shuts down.

Save schema 4 retains optional `ui.runtime-settings` with stable type key
`ui.runtime-settings.v2` for old slots and readers. App settings are authoritative once present, so
New Game and Continue never replace them. If no app settings document has ever existed, the first
successfully completed legacy restore imports its slot settings once; a failed restore never commits
that import. The v3 to v4 migration converts the previous three-field settings record and assigns
these defaults:

- category volumes: `1`
- brightness: `0.5` (neutral exposure)
- camera shake and head bob intensity: `1`
- head bob enabled: `true`
- sprint mode: `Hold`
- locale: `en`
- input binding overrides: empty
- display profile: absent; the first Display page open uses the device/project configuration

## Localization

Game-facing keys live in the `Game Text` String Table Collection under `Assets/Game/Localization`.
Use stable semantic keys such as `objective.<id>.title`, `phone.<id>.message`, and
`scare.<id>.cue`; do not derive identity from an asset path or scene hierarchy.

Definitions retain authored fallback text for development and backward compatibility. Missing keys
must be visible during content review, while runtime presentation falls back rather than showing an
internal key to the player. Critical atmosphere cues continue to emit subtitle fallback events and
also retain their visual camera/light presentation.

## Locale fonts

The framework does not ship a game-specific final font. Run the Phase 5 localization rollout,
then configure `Assets/Game/Localization/Fonts/GameLocaleFontProfile.asset` per standalone game.
The canonical uGUI shell swaps `Font` assets only when locale changes, never per frame.

For the current legacy uGUI renderer:

1. Import licensed fonts under `Assets/Game/Localization/Fonts`.
2. Assign a locale-specific override for Vietnamese, Spanish, Japanese, Korean, Simplified Chinese,
   and Traditional Chinese. `Use Authored Font` is valid only when every authored label font passes
   the representative glyph validation.
3. Do not depend on a platform font that is absent from the shipped project.

If a game replaces the canonical labels with TextMeshPro:

1. Use a static SDF primary font when the complete glyph set is known.
2. Use a dynamic SDF atlas only for scripts that require runtime glyph discovery.
3. Add script-specific fallback assets in TMP Settings in deterministic order; do not rely on OS
   fonts in a release build.
4. Keep atlas size and multi-atlas behavior bounded, and profile CJK memory after exercising the
   actual localized UI.
5. Enable wrapping for body text. Reserve bounded Auto Size for compact labels and buttons.

The canonical shell currently uses uGUI `Text` for compatibility with existing milestones. Its
builder enables wrapping, bounded best-fit sizing, and scrollable settings pages. A game may replace
those labels with `TextMeshProUGUI` without changing localization keys or settings ownership, but it
must provide an equivalent locale font adapter before enabling those locales.

## Tabbed Settings template

`TabbedSettingsPresenter` and `TabbedSettingsLayoutBuilder` live in the framework package. After
creating the M11 UI shell, run `Setus > Horror Framework > Settings > Apply Tabbed Settings Layout`.
The M12 sample builder also runs this step. The generated prefab stays at
`Assets/Game/Prefabs/UI/SetusUiShell.prefab` so each game can customize its visual design without
editing package files. Re-running the layout command preserves the prefab GUID and does not add
duplicate tabs. Audio, Controls, Accessibility, Language, and Display are the template pages; only installed
locales are shown. UI and Voice volume rows remain hidden until the game wires audio sources for
those categories.

Run `Setus > Horror Framework > Settings > Apply Production Controls` to wire shared Interact and
Pause actions, then run `Setus > Horror Framework > Settings > Apply Phase 6 Graphics Settings`.
The graphics command requires an assigned URP asset and creates missing `Low`, `Medium`, and `High`
Quality levels and URP assets in `Assets/Game/Settings/Graphics`. It preserves existing authored
levels/assets; a wrong existing mapping must be corrected in Project Settings > Quality. The M12
sample builder also performs this setup. The Display page and confirmation modal use
`RuntimeSettingsModel` as the only durable owner. Display
controls hold a draft, preview it through `UnityDisplaySettingsDevice`, and persist only after Keep.
Timeout, Revert, panel disable, unsupported resolution, or rejected mode restores the exact runtime
display snapshot taken before Apply. Existing settings files without a display profile remain valid.

The Windows V1 Display surface includes Windowed, Borderless, Exclusive Fullscreen, resolution,
fullscreen refresh rate, VSync, FPS cap, and the host project's authored Quality levels. Borderless
uses the desktop resolution. Unity only supports Exclusive Fullscreen in a Windows standalone
player, so that path must be validated in a build rather than inferred from the Editor Game view.

## Manual validation

1. Open the Settings screen from Main Menu and Pause Menu; Back returns to the correct screen.
2. Change each volume category and verify only the intended source category changes.
3. Set brightness low/high and confirm the gameplay Volume changes exposure without mutating a
   shared profile asset.
   The canonical player Camera must have URP post-processing enabled for this setting to apply.
4. Set camera shake to zero and trigger M7; camera shake/FOV kick is removed while light/subtitle
   fallback still runs.
5. Disable head bob, then set intensity to partial values while walking and crouching.
6. Verify sprint Hold and Toggle modes.
7. Rebind movement, look, sprint, crouch, Interact, and Pause; test conflict/Cancel/Reset, then quit
   and relaunch before Continue and verify bindings.
8. Start New Game and Continue two different slots; neither may override the current app preferences.
9. Change locale after adding another locale table and confirm UI/objective/phone/note/subtitle text
   refreshes or uses authored fallback.
10. Use long localized strings at 1920x1080 and a small supported resolution; labels must wrap or
   scroll without covering adjacent controls.
11. In a Windows player build, apply Windowed, Borderless, and Exclusive Fullscreen profiles. Keep
    one profile and verify it survives relaunch before Continue.
12. Apply another resolution and wait 15 seconds without confirming; the exact previous mode,
    resolution, VSync, FPS cap, and Quality level must return. Repeat with explicit Revert.
