# LOGIYGames SurvivalBuilding

A modular, grid-based building system for Unity 6. The package separates reusable building mechanics from optional starter content and sample implementations.

## Package layout

- `Runtime/` — the core API and reusable building mechanics: grids, placement planning, piece/material contracts, build services, marker handling, and system events.
- `Samples~/Scripts/` — optional sample implementations: floor, wall, stair and doorway definitions, a legacy-input controller, sample HUD/hint views, localized sample messages, and audio feedback.
- `Samples~/Data/`, `Samples~/Prefabs/`, `Samples~/Materials/` — sample ScriptableObject assets, prefabs and materials.

The sample scripts compile in the separate `LOGIYGames.SurvivalBuilding.Samples` assembly, which references the core assembly. Core does not reference the sample assembly or any of its concrete piece types or presentation components.

## Importing the Starter Pack

In Unity Package Manager, select **LOGIYGames SurvivalBuilding** and import the **Starter Pack** sample. The sample includes the scripts and assets used by the supplied prefabs.

The example controller uses Unity's legacy `UnityEngine.Input` API. Projects using a different input backend can replace it with their own controller and call the public `BuildPlanner` API.

## Creating custom building pieces

Derive from `PieceDefinition` in your own assembly and implement the placement evaluation, validation, slot occupancy, and marker-generation contracts. Use `GetDependents` to describe support relationships. Override `IsSupportAnchor` for definitions that can anchor a structure (the sample floor definition uses ground contact).

The planner reports optional presentation requests through events such as `PlacementHintRequested`, `DestroyHintRequested`, `MessageHintRequested`, and `HintHidden`. UI is not required by the core and can be implemented independently.

## Unity version

The package manifest targets Unity `6000.6`. Falling building fragments use Unity 6 rigidbody APIs such as `Rigidbody.linearVelocity`.

## Placement marker raycasts

The sample marker prefabs use **trigger colliders**. `ExampleBuildController` queries marker triggers explicitly with `QueryTriggerInteraction.Collide` before querying solid world geometry, then forwards the marker hit to `BuildPlanner.Tick`. A custom input controller must also allow trigger hits; using `QueryTriggerInteraction.Ignore` bypasses marker-based placement for walls, stairs, and supported floor locations.

### Corner pillars

The Starter Pack includes a pillar definition. It can be placed only at a corner shared by four floor tiles. Its four upper neighbouring cells become valid floor candidates; those floors participate in the same support graph as floors supported by walls. Removing a pillar is blocked while any upper floor would lose its support.
