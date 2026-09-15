# Setus Horror Framework

Reusable Unity 6 framework package for standalone first-person narrative horror games.

Framework source belongs in `Packages/com.setus.horror-framework`. A standalone game's
scenes, content, art, audio, UI assets, and settings belong in `Assets/Game`.

## Current Status

| Milestone | Status | Implemented boundary |
| --- | --- | --- |
| M0 Project Bootstrap | Complete | Local package, assemblies, folders, and conventions. |
| M1 Core Foundation | Complete | Bootstrap, context/services, events, runtime state, logging, and ID validation. |
| M2 Player and Input | Complete | First-person movement, camera, input, state policy, pose save, and safe crouch. |
| M3 Save and Progression | Complete | Manifest-validated durable slots, restore preflight, migrations, and checkpoints. |
| M4 Scene Flow and UI Shell | Complete | Boot, menus, canonical transitions, loading recovery, persistent UI shell, and EventSystem ownership. |
| M5 Interaction and Inventory | Complete | Raycast interaction, prompts, interactables, triggers, inventory, and persistent interaction state. |
| M6 Narrative and Objectives | Complete | Standalone scenario progression, objective gates, phone/note flow, route/branch state, and narrative UI hooks. |
| M7 Atmosphere and Scares | Complete | Tension/scare lifecycle, durable state, presentation hooks, and authoring/debug surfaces. |
| M8 Enemy AI and Navigation | Complete | Stalker perception/state, NavMesh patrol/search/chase, stable scene-owned save state, tuning, feedback, and debug. |
| M9 Authoring Tools and Validators | Complete | Code-free authoring commands, focused inspectors, scene validation, report window, and an isolated mini-scenario builder. |
| M10 Asset and Content Pipeline | Complete | Standalone-game folders, content conventions, scoped audio import policy, asset validation, and serialized-reference guidance. |
| M11 Accessibility, Localization, and Settings | Complete | Persistent accessibility settings, Unity Localization text keys, remapping UI, audio categories, brightness, and camera comfort controls. |
| M12 Framework Sample and Game Template | Complete | Regeneratable first-person room proving interaction, narrative, scare, AI, checkpoint, save/load, Continue, and authoring examples. |
| M13 Build, Release, and Production Hardening | Complete | Production/development build profiles, composed validation, CLI build, performance/logging policy, smoke flow, and release checklist. |

Milestones M0-M13 are implemented by this package revision.

## Build And Release

M13 adds an Editor-only release boundary. Run `Setus > Horror Framework > Release > Create Missing
M13 Defaults` when release configuration is absent, then `Validate Production Readiness`.
Default creation preserves existing authored configuration, and validation/build never repairs
or overwrites invalid values. Production profiles disable the
default debug overlay and framework category logs, while development profiles retain explicit
debugging. Readiness validation inspects every enabled Build Settings scene with the shared
framework rules; M12-specific checks are conditional on the sample root being present. See
[Production Release](Documentation~/PRODUCTION_RELEASE.md) for CLI build usage,
performance budgets, the standalone smoke flow, and the per-game release checklist.

## Composition and Ownership

`HorrorGameBootstrapper` is the only runtime authority that creates
`HorrorGameContext`. It requires an authored `HorrorFrameworkConfig`, persists across scene
loads, and rejects duplicate bootstrappers. The fallback creation path in
`HorrorGameContext.Ensure()` is for isolated EditMode tests only; runtime callers may use it
only after bootstrap has initialized the active context.

`HorrorGameContext` owns the service registry, gameplay event bus, debug manager, logger,
save services, scene transition service, inventory, UI shell state, cursor input mode, and
pause flow. Scene-specific MonoBehaviours adapt those services to Unity lifecycle and
serialized references; they do not own replacement global state.

The Boot scene uses `BootFlowLoader` to verify that its framework and game configurations
match the active bootstrap, configure save progression, and request the initial Main Menu
transition.

## Save and Progression

`SaveGameOwner` captures and restores global and stable owner state through
`SaveableRegistry`. `SaveLoadCoordinator` is the production boundary for durable capture,
non-mutating slot validation, Continue preparation, and post-load restore.

The V1 game-facing UI uses a single production slot, `primary`, defined by
`SingleSaveSlotContract`. New Game, Quick Save, Quick Load, and Continue share that policy;
the UI does not expose slot selection. Lower-level stores retain explicit `SaveSlotId` support
for tests, migration tooling, and a future deliberately designed multi-slot UX.
`SaveSlotMetadata`, `SaveGameSnapshot`, and slot-validation results are the active slot data
models. `InMemorySaveSlotStore` remains a test double; runtime uses
`PersistentSaveSlotStore`.

Durable slots are versioned JSON documents stored below:

```text
Application.persistentDataPath/SetusHorrorFramework/Saves/slot-<slot-id>.json
```

The file contains its format version, save schema version, slot ID, timestamp, checkpoint
metadata, and deterministic global/stable state collections. Writes use a temporary file and
replace or move strategy. Missing, unreadable, or invalid documents return an invalid slot;
they do not crash the game or fake a Continue path.

Format version 2 identifies each state payload with a framework-owned stable type key such as
`player.pose.v1` or `interaction.object.v1`. `SaveStateTypeRegistry` resolves that key through
an explicit codec; C# type names, namespaces, and assembly names are not the current persistence
identity contract. New persistent state types must register one unique, versioned key and codec
in the composition catalog before they can be captured.

Format version 1 saves remain readable through explicit legacy CLR-identity aliases registered
against the current codec. These aliases are compatibility input only and are never written by
the current format. When a persistent state class moves or is renamed, retain its old literal
identity as a legacy alias; an unknown stable key or legacy identity invalidates the load rather
than silently dropping that state.

`StandaloneGameConfig` owns one game-level authored `StableIdManifest`. Each authored entry can
declare a scene ID; required and optional checks then apply only while that scene is being
captured or restored. Stable IDs remain globally unique. Entries with an empty scene ID retain
the legacy single-scene behavior and apply everywhere, so new multi-scene content should always
author the scene ID. `BootFlowLoader` passes the manifest into `SaveLoadCoordinator`; production
capture and load are unavailable until this configuration succeeds.

`SaveGameOwner` also owns the in-memory retained stable-state ledger for the current game session.
Before a scene transition, live scene owners replace only their own records in that ledger. A
slot capture merges all currently live scene records with retained unloaded-scene records in
stable-ID order and persists the visited scene IDs. After load, only records applicable to the loaded scene
are applied. New Game clears the ledger; Continue initializes it from the migrated slot. The
ledger stores serialized state and scene IDs only, never Unity object references. Runtime-spawned
owners are retained when registered, but optional spawned objects still need game-specific
reconstruction if they do not exist when their scene is restored. Retired manifest entries are
kept as compatibility declarations and are not applied.

`GlobalSaveStateRegistry` is the framework-owned declaration catalog for non-stable-object
state. A declaration binds a stable state key to its codec key, marks the record Required or
Optional, identifies whether its live owner belongs to the persistent context or destination
scene, and records the schema version that introduced it. The default catalog requires
`inventory.state`, `player.pose`, `objective.state`, `narrative.state`,
`atmosphere.scare-state`, and `atmosphere.tension-state`; `ui.runtime-settings` is optional.
Objective and narrative records are required from schema 2; atmosphere records are required from
schema 3. Capture refuses to write
a slot when a required owner is absent or cannot produce a valid record. Slot validation and
scene-level restore preflight both reject missing required records. Required state introduced by
a later schema must be supplied by an explicit migration; normal restore never silently creates
it. Future global state such as objectives or narrative progression must register its declaration,
stable codec key, migration rule for older schemas, runtime owner, and semantic validator when
the domain has additional invariants.

Schema 2 is introduced by `HorrorSaveMigrations` with an explicit `v1 -> v2` rule. It adds
empty, durable `objective.state` and `narrative.state` records to a schema 1 snapshot before
restore. The destination scene's authored scenario initializes that explicit empty state only
after the scene is ready; normal restore never fabricates a missing current-schema record. Story
trigger StableIds are optional world records because their one-shot progression authority is
the required `narrative.state`, not fixture-local trigger state.

Schema 3 is introduced by an explicit `v2 -> v3` migration that adds empty atmosphere scare and
tension records. A scare captured in Triggered, Playing, or Restored is normalized to Consumed.
Schema 5 adds scene applicability to stable records and persists visited scene IDs. The explicit
`v4 -> v5` migration scopes every legacy stable record to its checkpoint scene, matching the
single-scene capture contract used by schema 4.
Load therefore resets transient presentation instead of replaying or half-restoring audio, lights,
camera motion, or a Volume reaction. M7 also uses a consume-on-interruption policy: disabling the
scene-bound lifecycle presentation or beginning a single-mode scene transition consumes an active
scare, clears its tension source, and prevents a replacement presentation from resuming orphaned
Playing state.

### Stable ID Policy

- Authored persistent objects must have a non-empty, inspectable `StableId`.
- Persistent interactables and trigger zones reject missing authored IDs at runtime; they do
  not generate a replacement random ID during gameplay.
- The manifest classifies IDs as Required, Optional, RuntimeSpawned, or Retired.
- Missing Required records or required live owners are restore errors. Missing Optional IDs
  are reported and skipped. Retired saved records are skipped with a warning.
- Persistent runtime-spawn reconstruction is not implemented. Do not depend on a
  RuntimeSpawned object being reconstructed from a save until an explicit spawn allocator and
  reconstruction contract are added.

### Restore Contract

Continue performs a non-mutating slot-level validation before it is exposed or a scene request
is created. This validates the readable document, file format, schema migration, stable type-key
and codec resolution, payload decode, required manifest records, and semantic validation for
currently registered global owners. Target-scene stable owners are deliberately not assumed to
exist in Main Menu.

After the destination scene registers its save owners, restore follows a fixed order:

1. Migrate the snapshot to the current schema.
2. Build a scene-level preflight plan before mutating live state: duplicate records, manifest
   requirements, live owners, active-state policy, state key, declared state type, payload
   compatibility, and optional owner semantic validation.
3. Capture rollback state while building the restore plan.
4. Apply global runtime state in deterministic state-key order.
5. Apply stable world state in deterministic stable-ID/state-key order.
6. If an apply throws, roll back already-applied operations in reverse order and report the
   failure. `SaveRestoreCompleted` is published only after a successful restore.

## Scene Flow, UI, and Spawn

`SceneTransitionRunner` is the production owner of `SceneManager.LoadSceneAsync`. The flow is
begin transition, show loading state, load scene, resolve spawn, restore a prepared save when
requested, complete transition, then restore gameplay input. Re-entrant requests are rejected
while a transition routine is active. Transition and post-load failures clear pending restore
and return UI/input to a defined safe state rather than leaving Loading active.

Continue validates the durable slot without mutating live state, then creates a canonical
`SceneTransitionRequest` from validated checkpoint metadata and schedules restore for the
matching completed transition. It does not directly load a scene from a menu button. Scene-level
owner validation runs after the target scene has registered its owners and before restore apply.

`PlayerSpawnResolver` consumes the request `SpawnId` after the destination scene loads. It
applies exactly one matching `PlayerSpawnPoint`, or exactly one `default` spawn point as an
explicit fallback. Ambiguous or absent spawn points leave the scene-authored player pose in
place and emit a SceneFlow warning. A restored player pose is applied after spawn and therefore
has final authority for a loaded save.

`UiShellLifetime` owns one persistent UI shell. Duplicate shells deactivate before sibling
components can create UI state. `UiShellEventSystem` owns the intended Input System UI EventSystem
and disables competing active EventSystems. Scene-authored UI must not introduce another
persistent shell authority.

## Player and Interaction

`FirstPersonPlayerController` owns player pose, camera pitch, locomotion, crouch posture, and
the semantic `PlayerControlState`. `PauseFlowController` is an overlay owner: it pauses time,
audio, cursor, and gameplay input without replacing a prior Inspecting, Cutscene, or Disabled
semantic state. Cursor restoration uses the current player state policy.

Interaction permission has one policy boundary in `PlayerInteractionPermissionPolicy`.
World interaction is allowed only when the player state, pause state, visible UI state, and
scene transition state permit it. `RaycastInteractor` uses a reusable component list for focus
resolution; it does not allocate a component array each focused frame.

Persistent interactables own their concrete world state plus `InteractionObjectState.IsEnabled`.
Disabled means interaction is rejected; it does not silently deactivate the world object.
Trigger zones own `hasFired` and persist it for fire-once behavior.

## Narrative and Objectives

`NarrativeRuntimeModel` owns durable scenario facts: consumed story beats, phone delivery/read/
reply progress, read notes, unlocked routes, and lightweight branch flags. `ObjectiveRuntimeModel`
owns the active/completed objective graph state. Both are required global records with stable
codec keys (`narrative.runtime.v1`, `objective.runtime.v1`) and semantic restore validation.

`NarrativeScenarioDefinition` represents one story sequence inside a standalone game, not an
episode/chapter system. Objective definitions advance only when their configured gate kind and
ID match an event. The runtime ignores a repeated story beat, so an already-completed objective
cannot advance a second time. Supported gate sources are gameplay signals, interaction triggers,
story beats, phone read/reply events, note read events, route unlocks, and inventory-item changes.

Story triggers, phone messages, notes, route unlockers, and branch-flag adapters reference
stable narrative IDs rather than other scene object implementations. A checkpoint capture already
contains all global state, so saving after an objective change restores objective/narrative state
with the checkpoint pose and target scene.

`ObjectivePresenter`, `PhoneMessagePresenter`, and `SubtitlePresenter` are passive uGUI overlays.
They subscribe to narrative events, do not create an EventSystem, and do not acquire cursor or
player-control ownership. The M6 authoring command is `Setus > Horror Framework > Narrative >
Apply M6 Narrative Foundation`; it safely creates/updates M6 content under `Assets/Game`, updates
the existing UI shell, adds the Gameplay fixture, and adds its required story-trigger ID to the
authored manifest without deleting production assets.

## Atmosphere and Scares

`ScareRuntimeModel` owns durable per-scare lifecycle state and `TensionRuntimeModel` owns named
tension sources. The lifecycle is Disabled, Armed, Triggered, Playing, Consumed, and the transient
Restored synchronization phase. `ScareScenarioTrigger` adapts a gameplay trigger ID to a scare ID;
it does not hard-reference a scene-specific narrative object. A consumed scare does not re-arm on
normal restore.

`ScareScenarioAdapter` registers scene-scoped definition assets while enabled. Registration lifetime
does not own durable lifecycle history: unloading a scene removes its definition references but
preserves Armed, Disabled, and Consumed records for later scene re-entry. `Configure` remains the
global catalog replacement API and follows the same state-preservation rule.

`ScareAudioCueHook`, `AmbienceLayerController`, `ScareLightFlickerHook`, `ScarePowerStateHook`,
`ScareCameraReactionCoordinator`, and `ScareVolumeReactionHook` are presentation-only adapters.
They reset their AudioSource, Light, Camera, or `Volume.weight` response when a scare completes or
restores. Audio hooks use normal main-thread `AudioSource` APIs only; there is no
`OnAudioFilterRead` integration. Critical information also emits `AtmosphereSubtitleCueRequested`,
which the existing subtitle presenter respects through the runtime subtitle setting.

`ScareDebugTrigger` is separate from authored production triggers, gated by the global debug state,
and provides Inspector actions to trigger or reset its configured debug scare. The M7 authoring
command is `Setus > Horror Framework > Atmosphere > Apply M7 Atmosphere Foundation`; it creates or
updates M7 content in `Assets/Game`, adds a separate Gameplay fixture, and updates the existing
manifest without deleting production assets. Enable the `Atmosphere` log category together with
the global debug toggle to inspect scare transitions and tension source changes.

## Enemy AI and Navigation

`StalkerAiRuntimeModel` owns one stalker's semantic state: patrol, suspicion, search, chase,
last-known target position, and finite search/lost-sight timers. It is intentionally a stable
scene owner rather than a new global context service. `StalkerAiController` is the Unity adapter:
it performs the fixed-rate physics sight query, listens for player footsteps, and drives one
`NavMeshAgent` from the model's selected destination. `StalkerPatrolRoute` owns authored waypoint
references, while `StalkerAiTuningProfile` owns inspector-tunable perception, suspicion, timing,
movement, and feedback values.

Sight confirmation requires an actual collider on the configured target mask to be the nearest
relevant hit. The occluder mask is a separate serialized field; an occluder hit before the target
produces `TargetBlocked` rather than chase. The controller retains the final sight reason and
blocker for debug output. Every model transition publishes `StalkerAiStateChanged` with its reason.

`StalkerAiSaveAdapter` registers the authored `StableId` with `SaveableRegistry` and uses the
stable codec key `ai.stalker-runtime.v1`. Search restores with its remaining duration. A saved
chase restores as a search at the saved last-known position because a scene-bound target reference
cannot be considered confirmed after loading. The M8 fixture's stable-manifest entry is Optional:
slots created before M8 retain their compatibility and an unrecorded enemy starts in its authored
patrol state; every M8-era capture includes its state. A game that makes an enemy progression
critical must introduce a Required entry together with an explicit migration/default policy.

`StalkerAiFeedbackHook` plays an assigned cue when available and always emits the subtitle fallback
for entering chase, so the high-stakes transition is not audio-only. `StalkerAiDebugView` is gated
by the global debug manager and reports state, transition reason, target, sight result, blocker,
blocker layer, and navigation diagnostic. The authoring command is
`Setus > Horror Framework > AI > Apply M8 Enemy AI Foundation`; it creates the tuning profile under
`Assets/Game/Content/AIProfiles`, creates a separate Gameplay fixture, updates the existing stable
manifest without deleting assets, and bakes its `NavMeshSurface`.

## Authoring Tools and Validation

M9 editor tooling lives entirely in the package `Editor` assembly; it does not add a runtime
service or a second state owner. The `GameObject > Setus Horror` menu creates an authored
inspectable, a scare trigger linked to a selected or newly created scare definition, and a
three-point AI patrol route. `Assets > Create > Setus > Horror Framework > Narrative > Scenario
Skeleton` creates a valid one-objective standalone scenario under `Assets/Game` without requiring
new game code.

`Setus > Horror Framework > Validation > Open Report Window` validates the active scene against an
optional selected `StableIdManifest`. Its unified report covers duplicate or missing stable IDs,
required and undeclared manifest records, missing narrative/scare references, invalid interaction
colliders and layer masks, empty AI perception masks, target masks that exclude the assigned target,
missing AI save adapters, invalid patrol routes, unresolved scare trigger IDs, bootstrap
configuration, and debug-overlay availability. Target and occluder masks may overlap because the
runtime resolves target ownership before treating a hit as an occluder. Important framework,
standalone game, and narrative scenario configs expose focused inspector diagnostics using the same
runtime contracts.

`Setus > Horror Framework > Authoring > Build or Inspect M9 Mini Scenario` regenerates only the
M9-owned sample scene and assets under `Assets/Game`. The generated scenario includes an
interactable, objective trigger, scare trigger/catalog, patrol route, and dedicated stable-ID
manifest, then runs unified validation before reporting success. It never mutates a shared runtime
`ScriptableObject`; authored sample definitions and the manifest are isolated M9 assets.

## Asset and Content Pipeline

M10 keeps final game content under `Assets/Game` and framework code/assets in the package. Use
`Setus > Horror Framework > Content > Create or Repair Standalone Game Folders` to create the
production folder template without replacing existing assets. Prefab, ScriptableObject, asset-name,
gameplay-ID, texture, model, and release-check conventions are documented in
`Documentation~/ASSET_CONTENT_PIPELINE.md`.

Audio imported under the Ambience, Music, OneShots, Stingers, UI, or Voice category folders receives
a bounded category-specific load/compression policy. The content validation checklist checks those
settings, required prefab components and StableIds, definition IDs, required folders, and basic
texture/model memory risks. The current single-game architecture uses serialized GUID/fileID
references plus stable gameplay IDs. Addressables is now an indirect dependency of Unity
Localization and owns localized table loading. It is not used as a general gameplay-content or DLC
authority.

## Accessibility, Localization, and Settings

`RuntimeSettingsModel` remains the single global owner of user settings. At runtime bootstrap it
loads an atomic, app-wide `user-settings.json` from `Application.persistentDataPath`; preference
changes are persisted independently of gameplay slots, with a last-known-good backup. Save schema 4
keeps the optional stable codec key `ui.runtime-settings.v2` for backward compatibility. When no app
settings have ever been authored, the first successfully restored legacy slot imports that record
once; an existing app settings file always wins over every slot. Schema 3 saves migrate volume,
sensitivity, and subtitle values explicitly while receiving documented M11 defaults. The state includes master/ambience/SFX/
UI/voice volume, brightness, camera-shake intensity, head-bob enable/intensity, sprint hold/toggle,
locale code, mouse sensitivity, and Input System binding overrides.

`FirstPersonPlayerController`, `ScareCameraReactionCoordinator`, ambience/scare/AI audio hooks,
`BrightnessVolumeSettingsApplier`, `RuntimeLocaleController`, and `InputSystemPlayerInputSource`
consume settings without owning duplicate state. The canonical UI shell exposes a scrollable
Settings screen and keyboard/mouse rebinding surface. Rebinding changes are runtime overrides on
the Input Action asset and are persisted through the same app-wide `RuntimeSettingsModel` store.

UI, objective, phone, note, and critical scare subtitle content uses stable entries in the
`Game Text` Unity Localization table. Authored fallback text keeps older assets and missing-table
development setups readable; presenters never infer a localized string from a hierarchy path.
Run `Setus > Horror Framework > Accessibility > Apply M11 Accessibility And Localization
Foundation` to create/update the English locale/table, assign content keys, upgrade the canonical
UI prefab in place, and add the gameplay brightness Volume.

The current shell remains uGUI-compatible. For TextMeshPro production fonts, use Unicode dynamic
SDF only where runtime glyph discovery is required, keep Latin UI fonts static when the character
set is known, and configure CJK/Arabic/other script assets as explicit fallback assets in TMP
Settings. Keep body copy wrapping enabled, use bounded Auto Size only for compact labels, and test
the longest supported locale at reference and minimum resolutions. See
`Documentation~/ACCESSIBILITY_LOCALIZATION_SETTINGS.md`.

## First Person Horror Template

The Package Manager sample `Samples~/FirstPersonHorrorTemplate` documents the canonical M12
scenario. Run `Setus > Horror Framework > Samples > Build First Person Horror Template` after a
fresh package import. The idempotent builder composes the existing milestone builders, creates a
primitive room plus a player spawn and locked container, merges required Stable ID declarations,
enables Boot/MainMenu/Gameplay in Build Settings, and validates the generated Gameplay artifact.

The resulting `Assets/Game` content demonstrates a door, drawer, pickup, locked container,
inspectable note, objective chain, phone message, scare, NavMesh AI encounter, checkpoint-backed
save, Continue, pause, and the persistent UI shell. It deliberately adds no new runtime owner or
sample-specific gameplay code. Debug starts disabled and remains toggleable with the configured
framework hotkey.

## Debug and Designer Surface

`HorrorFrameworkConfig` controls the initial debug state, enabled log categories, and Input
System debug hotkey. Debug is off by default in production configuration. `HorrorDebugManager`,
`HorrorLogger`, stable-ID validators, and component gizmos are the supported diagnostics.

Internal stable IDs are not shown to players by trigger feedback unless global debug is enabled.
The debug save/load panel reports slot status and save/restore outcomes, but no longer emits
temporary per-object SaveTrace logs.

## Validation

The package includes EditMode tests for lifecycle, event bus, stable IDs, manifest/save
validation, restore preflight, builders, interaction, player state policy, durable slot
validation, legacy save migration, and atmosphere scare lifecycle/migration. PlayMode tests cover player movement and pose restore,
save-owner pose restoration, bootstrap scene reload, raycast prompt focus, trigger behavior,
spawn resolution, crouch clearance, and scare camera/light/Volume reset. Full coroutine-level scene-transition failure/reentrancy
coverage and end-to-end UI lifecycle coverage remain manual/source-level validation, not current
PlayMode coverage.

M8 adds focused EditMode state-machine/save-semantic tests and real-NavMesh PlayMode integration
coverage for perception fairness, Patrol/Search/Chase/lost-sight navigation, destination memory,
controller re-enable, and save-adapter lifecycle. The retained full PlayMode result is 34/34 passed,
including 13/13 M8 PlayMode tests. Authored Gameplay-scene presentation remains manually validated.

M9 adds EditMode regression coverage for scene-scoped and duplicate stable IDs, missing required
manifest IDs, valid overlapping and invalid AI perception masks, missing AI save adapters, patrol
route validity, scare trigger cross-references, additive-scene ownership, atomic scare-command
Undo/Redo, test-asset cleanup, and regeneration of the isolated mini-scenario artifact. Test
execution remains the project owner's explicit Unity Test Runner step.

M10 adds EditMode coverage for idempotent folder generation, importer policies, content-ID format,
persistent prefab requirements, and the executable asset-validation checklist. Audio importer and
folder-template behavior must also be inspected manually with representative game assets.

M11 adds EditMode coverage for app-wide settings round-trip, legacy slot import/conflict policy,
corrupt-file recovery, settings state validation, stable codec/schema migration, and localization
fallback. PlayMode covers persisted Input System overrides, invalid binding restore, sprint-toggle,
and zero-head-bob behavior. Brightness, camera shake, locale switching, audio category response, and
long-text layout remain explicit owner-run manual checks until their Unity integration results are
reported.

M12 adds EditMode coverage for idempotent room/locked-container generation, canonical spawn reuse,
Stable ID manifest declaration, and Package Manager sample metadata. The project owner regenerated
the Gameplay artifact and manually validated the Boot/Main Menu/Gameplay/pause/save/Continue flow,
interaction set, objective/phone/note progression, scare persistence, AI encounter, and debug toggle.

The project owner runs validation manually in the Unity Editor. Test builders must use their
test-owned asset paths; normal builder operations preserve existing production asset GUIDs.
Destructive regeneration is an explicit editor authoring action.

## Deferred Performance Work

`GameplayEventBus` snapshots handlers with `ToArray()` during publish so handlers can safely
subscribe or unsubscribe during dispatch. This is intentionally unchanged:

```text
DEFERRED - NO CURRENT PERFORMANCE EVIDENCE
```

Do not replace it with pooling or a custom dispatch structure without profiler evidence that
event publication is a relevant allocation or CPU cost.
