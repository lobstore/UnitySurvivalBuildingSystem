using System.Collections.Generic;
using UnityEngine;

namespace LOGIYGames.SurvivalBuilding
{
    /// <summary>Stair piece rules. Stairs occupy a dedicated slot in a floor cell.</summary>
    [CreateAssetMenu(menuName = "Building/Stair")]
    public class StairPiece : PieceDefinition
    {
        /// <summary>Reserves the stair slot in the anchor cell.</summary>
        /// <param name="key">Candidate stair key.</param>
        public override IEnumerable<Slot> GetSlots(PieceKey key)
        {
            yield return Slot.StairSlot(key.Anchor);
        }

        /// <summary>Returns the two upper floor cells that may receive support from this stair.</summary>
        /// <param name="key">Stair key; its variant selects the stair direction.</param>
        public override IEnumerable<Vector3Int> GetSupportedFloorCells(PieceKey key)
        {
            yield return key.Anchor + Vector3Int.up;
            yield return key.Anchor + BuildGrid.SideOffset(key.Variant) + Vector3Int.up;
        }

        /// <summary>Checks that the target cell contains a floor and has no stair already.</summary>
        /// <param name="key">Candidate stair key.</param>
        /// <param name="grid">Target grid.</param>
        public override bool ValidatePlacement(PieceKey key, BuildGrid grid)
            => CheckPlacement(key, grid) == PlacementFailReason.None;

        /// <summary>Returns the specific failure reason for a stair placement.</summary>
        /// <param name="key">Candidate stair key.</param>
        /// <param name="grid">Target grid.</param>
        public override PlacementFailReason CheckPlacement(PieceKey key, BuildGrid grid)
        {
            if (grid == null)
                return PlacementFailReason.NoSupport;

            if (grid.IsSlotOccupied(Slot.StairSlot(key.Anchor)))
                return PlacementFailReason.SlotOccupied;

            if (!grid.TryGetPieceAtSlot(Slot.CellSlot(key.Anchor), out var floor)
                || floor.Definition is not FloorPiece)
            {
                return PlacementFailReason.NoSupport;
            }

            return PlacementFailReason.None;
        }

        /// <summary>Creates markers above every eligible floor without an existing stair.</summary>
        /// <param name="grid">Grid to inspect.</param>
        public override IEnumerable<MarkerSpot> GetMarkerSpots(BuildGrid grid)
        {
            if (grid == null)
                yield break;

            foreach (var piece in grid.Occupied.Values)
            {
                if (piece == null || piece.Definition is not FloorPiece)
                    continue;

                var cell = piece.Key.Anchor;
                if (CheckPlacement(new PieceKey(PieceId, cell, 0), grid) != PlacementFailReason.None)
                    continue;

                yield return new MarkerSpot
                {
                    Piece = this,
                    Grid = grid,
                    Key = new PieceKey(PieceId, cell, 0),
                    Position = grid.CellCenter(cell),
                    Rotation = grid.Rotation
                };
            }
        }

        /// <summary>Enumerates the horizontal columns occupied by the stair footprint.</summary>
        /// <param name="key">Stair key including direction.</param>
        public override IEnumerable<Vector2Int> GetOccupiedColumns(PieceKey key)
        {
            yield return new Vector2Int(key.Anchor.x, key.Anchor.z);
            var adjacent = key.Anchor + BuildGrid.SideOffset(key.Variant);
            yield return new Vector2Int(adjacent.x, adjacent.z);
        }

        /// <summary>Stairs are placed from markers rather than directly on arbitrary raycast surfaces.</summary>
        /// <param name="context">Current placement context.</param>
        /// <param name="material">Selected material.</param>
        public override PlacementResult Evaluate(PlacementContext context, MaterialDefinition material)
        {
            if (GetPrefab(material) == null)
            {
                return new PlacementResult
                {
                    Show = true,
                    Valid = false,
                    FailReason = PlacementFailReason.NoMaterialVariant,
                    Position = context.Hit.point
                };
            }

            return FreeInvalid(context, material, PlacementFailReason.WrongSurface);
        }

        /// <summary>Applies quarter-turn orientation changes to this stair definition.</summary>
        /// <param name="sign">Positive or negative scroll direction.</param>
        /// <param name="state">Current input state.</param>
        public override void OnScroll(int sign, BuildInputState state)
        {
            if (state == null || sign == 0)
                return;

            state.SetRot(this, (state.GetRot(this) + sign + 4) & 3);
        }

        /// <summary>Creates a placement result using the current rotation state for a marker.</summary>
        /// <param name="spot">Candidate marker.</param>
        /// <param name="material">Selected material.</param>
        /// <param name="input">Current input state.</param>
        public override PlacementResult FromMarker(MarkerSpot spot, MaterialDefinition material, BuildInputState input)
        {
            var direction = input != null ? input.GetRot(this) : 0;
            var key = new PieceKey(PieceId, spot.Key.Anchor, direction);
            var prefab = GetPrefab(material);
            var reason = prefab == null
                ? PlacementFailReason.NoMaterialVariant
                : CheckPlacement(key, spot.Grid);

            return new PlacementResult
            {
                Show = true,
                Valid = reason == PlacementFailReason.None,
                FailReason = reason,
                Key = key,
                Position = spot.Grid.CellCenter(key.Anchor),
                Rotation = spot.Grid.Rotation * Quaternion.Euler(0f, direction * 90f, 0f),
                TargetGrid = spot.Grid,
                Prefab = prefab,
                GhostPrefab = GetGhostPrefab(material)
            };
        }

        /// <summary>Returns the stair's local-space overlap bounds.</summary>
        /// <param name="cell">Horizontal grid cell size.</param>
        /// <param name="level">Vertical grid level size.</param>
        /// <param name="thickness">Unused by the stair bounds.</param>
        public override Bounds GetLocalBounds(float cell, float level, float thickness)
            => new Bounds(Vector3.up * (level * 0.5f), new Vector3(cell, level, cell));

        /// <summary>Prevents removing a stair while a floor above still depends on it.</summary>
        /// <param name="key">Stair key.</param>
        /// <param name="grid">Owning grid.</param>
        public override DestroyFailReason CanBeDestroyed(PieceKey key, BuildGrid grid)
            => grid == null || grid.WouldRemainSupported(key)
                ? DestroyFailReason.None
                : DestroyFailReason.HasFloorOnStair;

        /// <summary>Enumerates upper floors supported by this stair.</summary>
        /// <param name="key">Stair key.</param>
        /// <param name="grid">Owning grid.</param>
        public override IEnumerable<BuildPiece> GetDependents(PieceKey key, BuildGrid grid)
        {
            if (grid == null)
                yield break;

            foreach (var above in GetSupportedFloorCells(key))
            {
                if (grid.TryGetPieceAtSlot(Slot.CellSlot(above), out var piece)
                    && piece.Definition is FloorPiece)
                {
                    yield return piece;
                }
            }
        }
    }
}
