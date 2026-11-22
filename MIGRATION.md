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
