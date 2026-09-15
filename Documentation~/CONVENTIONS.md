# Setus Horror Framework Conventions

## Package Boundary

- Framework source lives in `Packages/com.setus.horror-framework`.
- Game-specific content lives in `Assets/Game`.
- Do not put reusable framework scripts under `Assets/Game` or demo folders.
- Do not place runtime `.cs` files directly in `Runtime/`; use a module subfolder.

## Namespaces

- Runtime: `Setus.HorrorFramework`
- Editor: `Setus.HorrorFramework.Editor`
- EditMode tests: `Setus.HorrorFramework.Tests.EditMode`
- PlayMode tests: `Setus.HorrorFramework.Tests.PlayMode`

Module-specific namespaces should append the folder path, for example:

```csharp
Setus.HorrorFramework.SaveProgression.StableIds
Setus.HorrorFramework.Interaction.Interactables
Setus.HorrorFramework.Atmosphere.Tension
```

## Runtime Architecture

- `MonoBehaviour` types are Unity adapters for serialized references, lifecycle hooks, collision, triggers, and presentation binding.
- Durable gameplay logic belongs in plain C# services, state machines, runtime models, or ScriptableObject definitions.
- Systems communicate through explicit interfaces, events, or local context services.
- Avoid repeated scene lookup in hot paths.

## Persistence

- Any authored object with persistent state must have a stable, inspectable ID.
- Required and optional persistent IDs must be declared by contract or manifest.
- Each subsystem that owns runtime state must document save/load behavior before production use.

## Debug Surface

- Debug overlays, labels, gizmos, and logs must be toggleable, bounded, and off by default for production/demo scenes.
- Designer tooling should reveal invalid authoring data instead of hiding scene or prefab errors.

## Asset and Content Pipeline

The production folder template, prefab and ScriptableObject conventions, audio import matrix,
asset-reference strategy, ID naming rules, and release checklist are defined in
[`ASSET_CONTENT_PIPELINE.md`](ASSET_CONTENT_PIPELINE.md).
