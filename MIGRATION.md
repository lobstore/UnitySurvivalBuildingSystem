# Migration notes

## Core and sample separation

The package now keeps reusable mechanics in `Runtime/` and moves optional/example implementations into `Samples~/Scripts/`.

Moved scripts (their original `.meta` files and GUIDs are retained):

- `ExampleBuildController`
- `BuildHintView` and `BuildMessages`
- `BuildHudController`
- `BuildAudioFeedback`
- Concrete sample definitions: `FloorPiece`, `WallPiece`, `StairPiece`, and `DoorwayPiece`

The sample scripts compile into `LOGIYGames.SurvivalBuilding.Samples` and reference `LOGIYGames.SurvivalBuilding`. Import the **Starter Pack** sample to use its prefabs and starter assets.

## API changes

- `PieceDefinition.IsSupportAnchor(PieceKey, BuildGrid)` is the extension point used by the core support scan. Definitions that can anchor a structure should override it.
- `BuildPlanner` no longer holds a direct reference to `BuildHintView` or `BuildMessages`. It raises presentation events instead; optional views subscribe to those events.
- Existing placement, grid, service, selection and destruction APIs remain in the core assembly.

## Serialized-reference compatibility

Moved scripts retain their original `.meta` GUIDs. References from the supplied sample prefabs and ScriptableObject assets therefore continue to resolve when the Starter Pack is imported. The new sample assembly definition is separate from the core assembly definition.

## Unity API baseline

- Minimum Editor version: `6000.6` (Unity 6.6).
- Falling rigidbodies use `Rigidbody.linearVelocity`.
- The event hub uses `Object.FindAnyObjectByType<T>()`.

## Existing behavior retained in Core

- Grid registration validates all requested slots before changing indexes.
- Unregistering a piece removes only slot mappings owned by that piece.
- Replacing an occupied slot does not destroy the grid being reused.
- Unsupported fragments are detached from their grid before physics conversion or destruction.
- Stair placement rejects an occupied stair slot during both preview and marker commits.
- Missing material prefabs produce the explicit `NoMaterialVariant` placement reason.
- The planner clears stale placement state after selection, orientation and mode changes, and does not allow commits while in destroy mode.

## Marker trigger raycasts

Starter marker prefabs use trigger colliders. The sample controller queries them with `QueryTriggerInteraction.Collide` even when global trigger queries are disabled. A custom controller must do the same and pass the `BuildMarker` raycast hit to `BuildPlanner.Tick`.
