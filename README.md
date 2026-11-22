# LOGIYGames SurvivalBuilding — Runtime refactor

This archive contains the refactored `Runtime/` source layer for **Unity 6000.6.4f1 (Unity 6.6)**.
It is intended as a source overlay for the original package `com.logiygames.building-survival`.

## Installation

1. Back up the original package.
2. Replace its `Runtime/` folder and `Runtime.meta` with the corresponding entries from this archive.
3. Replace `package.json` with the manifest from this archive.
4. Keep the original `Samples~/` folder and project assets. Sample prefabs/assets were not changed and are deliberately not duplicated in this Runtime overlay.
5. Re-open the project in Unity 6000.6.4f1 and let Unity reimport the package.

All `.meta` GUIDs for the original runtime C# scripts, the `Runtime` folder, the assembly definition and `package.json` have been retained so existing serialized references remain stable.

## Main changes

- Added XML documentation to the runtime classes and methods.
- Refactored grid registration/unregistration to validate slots before mutating indexes.
- Fixed slot cleanup so unregistering one piece cannot remove a slot entry owned by another piece.
- Fixed the grid replacement lifecycle: replacing an occupied slot no longer destroys the grid that is being reused.
- Centralized piece detachment and preserved Rigidbody physics settings for placed and falling pieces.
- Corrected stair validation so an already occupied stair slot is rejected during preview and marker commits.
- Corrected marker placement results to carry explicit failure reasons, including missing material prefabs.
- Removed duplicate destroy-preview rendering and paired event subscriptions correctly.
- Added null/configuration guards around planner, HUD, marker and hint workflows.
- Fixed enum hotkey arithmetic in the sample input controller.
- Kept the original serialized public field names and component class names.

## Unity API baseline

The package manifest uses `unity: 6000.6` and `unityRelease: 4f1`.
The code uses Unity 6 APIs such as `Rigidbody.linearVelocity` and `Object.FindAnyObjectByType<T>()`.
The legacy `UnityEngine.Input` sample controller remains intentionally unchanged in its input backend; replace it with Input System actions only if the consuming project has chosen that package and input architecture.

## Validation status

Source-level consistency checks are included in this delivery, but an actual Unity Editor import, compile and Play Mode test could not be run in this environment. The package depends on the external assembly GUID references retained in the original `.asmdef`.
