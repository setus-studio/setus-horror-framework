# Asset and Content Pipeline

## Ownership Boundary

- Reusable framework code, editor tools, tests, and non-game-specific samples belong in
  `Packages/com.setus.horror-framework`.
- Final art, audio, scenes, prefabs, definitions, settings, and UI for one standalone game belong
  in `Assets/Game`.
- A Unity project represents one standalone game. Do not place final game content in the framework
  package or mix content for unrelated games in one `Assets/Game` root.

Run `Setus > Horror Framework > Content > Create or Repair Standalone Game Folders` to create any
missing folders without replacing existing assets.

## Folder Convention

```text
Assets/Game/
  Art/
    Animations/
    Materials/
    Models/
    Textures/
  Audio/
    Ambience/
    Music/
    OneShots/
    Stingers/
    UI/
    Voice/
  Content/
    AIProfiles/
    Inventory/
    Messages/
    Notes/
    Objectives/
    Scares/
    Scenarios/
  Prefabs/
    AI/
    Environment/
    Interaction/
    Player/
    UI/
  Scenes/
    Debug/
  Settings/
  UI/
```

Boot, MainMenu, and primary gameplay scenes remain directly under `Scenes` for the current
single-game scene flow. Additional subfolders may group production scenes when the game grows,
but scene identity must remain configured through `StandaloneGameConfig`, not inferred from paths.

## Prefab Convention

- Prefix production prefab filenames with `PF_`, followed by domain and purpose, for example
  `PF_Interaction_LockedDoor` or `PF_AI_HallwayStalker`.
- Keep required components on the same object expected by the runtime contract.
- Persistent interactables and trigger zones require an authored `StableId` and valid collider.
- Stalker prefabs require `StableId`, `NavMeshAgent`, `StalkerAiSaveAdapter`, and a tuning profile.
- A fixed prefab-level StableId is appropriate only for a unique authored object. Override the ID
  on every scene instance when a prefab is instantiated more than once.
- Scene validation remains authoritative for duplicate IDs, manifest membership, scene references,
  patrol routes, and trigger cross-references after placement.

## ScriptableObject Convention

- Store authoring definitions under the matching `Content` or `Settings` folder.
- Prefix filenames with `SO_` plus type and purpose, for example `SO_Scare_HallwayPower`.
- Treat definitions as immutable at runtime. Mutable session state belongs to runtime models and
  save owners, not shared ScriptableObject assets.
- Use serialized object references. Do not load definitions by filename, hierarchy path, or
  `Resources.Load` string.

## Audio Import Rules

Audio rules apply automatically to clips imported below the category folders. Move an existing
clip into the correct folder and reimport it to apply the policy.

| Category | Load type | Compression | Mono | Background | Preload |
| --- | --- | --- | --- | --- | --- |
| Ambience | Streaming | Vorbis 0.65 | No | Yes | No |
| Music | Streaming | Vorbis 0.70 | No | Yes | No |
| OneShots | Decompress On Load | ADPCM | Yes | No | Yes |
| UI | Decompress On Load | PCM | No | No | Yes |
| Voice | Streaming | Vorbis 0.80 | Yes | Yes | No |
| Stingers | Compressed In Memory | Vorbis 0.75 | No | No | Yes |

Use WAV or AIFF masters. MP3 input is reported because Unity re-encodes an already lossy source.
The category defaults prevent long ambience/voice clips from expanding fully in memory and keep
short latency-sensitive UI/one-shot clips away from per-play streaming overhead. Profile unusually
long one-shots or unusually short voice clips before overriding the category policy.

## Texture and Model Guidelines

- Prefix textures with `T_`, materials with `MAT_`, models with `M_`, animations with `A_`, and
  audio with `AUD_<Category>_`.
- Disable Read/Write on textures and meshes unless runtime CPU access is required.
- Keep texture maximum size at 4096 or below unless visual inspection proves a larger source is
  necessary.
- Use mipmaps for world textures; UI sprites and data textures may intentionally disable them.
- Apply scale, pivots, normals, and collider strategy in the source DCC pipeline before prefab
  authoring. Do not depend on mutable hierarchy paths for gameplay identity.

## Gameplay ID Convention

Use lowercase stable identifiers with dot-separated ownership, for example:

```text
game.objective.restore-power
game.message.power-warning
game.scare.hallway-flicker
game.note.maintenance-log
game.trigger.hallway-entry
```

Hyphens and underscores are allowed inside segments. IDs are durable contracts: do not derive them
from object names, hierarchy paths, instance IDs, or list order, and do not rename persisted IDs
without an explicit save migration/alias.

## Asset Reference Strategy

The current framework targets one standalone game per Unity project and uses serialized
`UnityEngine.Object` references plus stable gameplay IDs. Unity serializes object references by
GUID/fileID, so moving an asset inside the project does not break the reference. Runtime framework
code does not use `AssetDatabase`, `Resources.Load`, or path-based content lookup.

Addressables is intentionally not installed because the current roadmap has no DLC, remote catalog,
or independently downloadable game content. If one of those requirements becomes real, introduce
Addressables through Unity Package Manager and add a narrow asset-provider boundary; do not replace
stable gameplay IDs with address strings.

## Validation Checklist

Open `Setus > Horror Framework > Content > Open Asset Validation Checklist`, then:

1. Create or repair the folder template.
2. Validate all content under `Assets/Game`.
3. Resolve every error for missing folders, invalid IDs, prefab requirements, and audio settings.
4. Review texture/model warnings and document intentional exceptions.
5. Run the existing active-scene validator with the correct `StableIdManifest` for every production
   scene.
6. Inspect representative prefabs and imported audio clips in the Inspector.
7. Run project EditMode tests and the relevant gameplay smoke test before release.

The content validator is read-only. The folder command creates missing directories, and the audio
postprocessor changes only importer settings for assets under recognized `Assets/Game/**/Audio`
category folders.
