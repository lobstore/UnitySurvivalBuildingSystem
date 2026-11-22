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
