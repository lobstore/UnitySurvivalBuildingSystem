# Changelog

## 1.1.0 — Unity 6000.6.4f1

- Updated the package editor baseline to Unity 6.6 (`6000.6`, release `4f1`).
- Documented the runtime API with XML comments.
- Hardened grid registration, replacement and removal lifecycle.
- Improved placement validation and feedback for stairs and missing material variants.
- Improved singleton/event subscription lifecycle and null handling.
- Updated the sample controller's numeric hotkey conversion.
- Preserved existing runtime `.meta` GUIDs.

## Compatibility fix
- Restored `BuildMode` as a dedicated public enum so `BuildEvents`, `BuildPlanner`, and input controllers resolve the mode type in the runtime assembly.


## FIX3 — placement marker raycasts
- Fixed marker-based wall, stair and supported-floor placement.
- Query marker trigger colliders explicitly, independent of `Physics.queriesHitTriggers`.
- Prefer a nearby marker trigger over its supporting solid surface while still respecting nearer solid occluders.
