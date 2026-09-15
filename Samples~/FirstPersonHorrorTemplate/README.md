# First Person Horror Template

This sample is a regeneratable, code-free scenario built from the framework's canonical
authoring assets. Game-specific output is written to `Assets/Game`; the package sample does
not contain final game art or audio.

## Fresh Import

1. Import this sample from Package Manager.
2. Run `Setus > Horror Framework > Samples > Build First Person Horror Template`.
3. Inspect `Assets/Game/Scenes/Gameplay.unity` and the generated assets listed below.
4. Open `Assets/Game/Scenes/Boot.unity` and enter Play Mode.

The command is safe to run again. It repairs missing canonical content, keeps existing asset
GUIDs, merges its Stable ID declarations without deleting unrelated entries, and validates the
generated Gameplay scene before reporting success.

## Play Flow

- Main Menu: start a new game or Continue from a compatible save.
- Gameplay: move through the room and interact with the door, drawer, key pickup, locked
  container, phone, and note.
- Objective chain: enter the story trigger, read the phone, read the note, then use the
  unlocked route.
- Atmosphere: enter the hallway scare trigger once; consumed scare state survives reload.
- AI: the stalker patrols a baked NavMesh and exposes sight/state/navigation diagnostics.
- Pause and save: press Escape, open Save / Load, and create the `m12.room.checkpoint` save.
  Return to Main Menu before Quick Load; Continue uses the same canonical transition path.

## Authoring Examples

| Contract | Generated example |
| --- | --- |
| Objective graph and scenario | `Assets/Game/Content/Scenarios/M6_DebugScenario.asset` |
| Objective definitions | `Assets/Game/Content/Objectives/M6_*.asset` |
| Phone message | `Assets/Game/Content/Messages/M6_ReturnCall.asset` |
| Inspectable note | `Assets/Game/Content/Notes/M6_MaintenanceLog.asset` |
| Scare definitions | `Assets/Game/Content/Scares/M7_*.asset` |
| AI tuning and patrol route | `M8EnemyAiFixtures/M8_PatrolRoute` in Gameplay |
| Save manifest | `Assets/Game/Settings/GameplayStableIdManifest.asset` |
| Menu, pause, save/load, Continue | `Assets/Game/Prefabs/UI/SetusUiShell.prefab` |

The locked container uses `DrawerInteractable` plus an authored lock requirement for
`m5.debug.key`. This demonstrates composition of an existing framework behavior without new
runtime code.

## Debug

The framework debug overlay starts disabled. Press Backquote to toggle it. The AI diagnostics
are constrained to the upper-left debug region and do not acquire input, cursor, or gameplay
state ownership.

## Validation

Run these owner-executed checks after generation:

1. `Setus > Horror Framework > Validation > Validate Active Scene` with the Gameplay manifest
   selected.
2. EditMode tests, including `FirstPersonHorrorTemplateBuilderTests`.
3. Play from Boot through New Game, pause, Quick Save, Main Menu, Continue, and restored state.
4. Confirm the scare does not replay, the locked container remains unlocked/open as saved, the
   objective/phone/note states restore, and the AI encounter does not confirm sight through walls.

No final art/audio is shipped by this sample. Primitive geometry and fallback presentation are
intentional so the package remains reusable across standalone games.
