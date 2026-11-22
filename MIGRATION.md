# Migration notes — Unity 6000.6.4f1

## Package integration

This ZIP is a refactored Runtime overlay, not a byte-for-byte repository mirror. It contains all 20 original Runtime C# source files and their retained `.meta` GUIDs, the assembly definition, package metadata and documentation. It does not duplicate the original `Samples~/` prefabs, materials, ScriptableObject assets or their metadata because those assets are unchanged.

Merge the archive into a clean checkout/copy of the original package. Keep `Samples~/` from the original package at the same level as `Runtime/`; `package.json` continues to register that sample directory.

## API baseline

- Minimum Editor version: `6000.6`.
- Minimum release: `4f1` (Unity 6000.6.4f1).
- Falling rigidbodies use `Rigidbody.linearVelocity`.
- Singleton lookup uses `Object.FindAnyObjectByType<T>()`, not the deprecated `FindObjectOfType<T>()`.

## Serialized-reference compatibility

The original `.meta` GUIDs for every Runtime script, the Runtime folder, package manifest and assembly definition are preserved. Public serialized field names were retained. Private implementation fields were renamed where appropriate because they are not serialized.

## Important behavior changes

- `BuildService.Commit` removes conflicting slot occupants without allowing the temporary empty state to destroy the grid being reused.
- `BuildGrid.Unregister` removes only slot mappings owned by the specified piece.
- Unsupported fragments are detached from their old grid regardless of whether physical destruction is enabled.
- Stair placement now rejects an occupied stair slot at both validation and marker-commit stages.
- Marker-generated placement results report `NoMaterialVariant` explicitly when the selected material has no prefab.
- The planner clears stale placement results after selection, orientation and mode changes, and no longer permits commit while in destroy mode.
