using System.Collections.Generic;
using UnityEngine;

namespace LOGIYGames.SurvivalBuilding
{
    /// <summary>Wall placement, edge occupancy and vertical support rules.</summary>
    [CreateAssetMenu(menuName = "Building/Wall")]
    public class WallPiece : PieceDefinition
    {
        /// <summary>Reserves the canonical edge represented by the wall's anchor and side variant.</summary>
        /// <param name="key">Wall key.</param>
        public override IEnumerable<Slot> GetSlots(PieceKey key)
        {
            yield return Slot.Edge(key.Anchor, key.Variant);
        }

        /// <summary>Returns the two upper floor cells that may be supported by this wall.</summary>
        /// <param name="key">Wall key.</param>
        public override IEnumerable<Vector3Int> GetSupportedFloorCells(PieceKey key)
        {
            yield return key.Anchor + Vector3Int.up;
            yield return key.Anchor + BuildGrid.SideOffset(key.Variant) + Vector3Int.up;
        }

        /// <summary>Checks edge vacancy and the existence of a floor or lower wall for support.</summary>
        /// <param name="key">Candidate wall key.</param>
        /// <param name="grid">Target grid.</param>
        public override bool ValidatePlacement(PieceKey key, BuildGrid grid)
            => CheckPlacement(key, grid) == PlacementFailReason.None;

        /// <summary>Returns the specific failure reason for a wall placement.</summary>
        /// <param name="key">Candidate wall key.</param>
        /// <param name="grid">Target grid.</param>
        public override PlacementFailReason CheckPlacement(PieceKey key, BuildGrid grid)
        {
            if (grid == null)
                return PlacementFailReason.NoSupport;

            if (grid.IsSlotOccupied(Slot.Edge(key.Anchor, key.Variant)))
                return PlacementFailReason.SlotOccupied;

            var hasFloor = grid.TryGetPieceAtSlot(Slot.CellSlot(key.Anchor), out var floor)
                && floor.Definition is FloorPiece;

            var belowAnchor = key.Anchor + Vector3Int.down;
            var hasLowerWall = grid.TryGetPieceAtSlot(Slot.Edge(belowAnchor, key.Variant), out var lowerWall)
                && lowerWall.Definition is WallPiece;

            return hasFloor || hasLowerWall
                ? PlacementFailReason.None
                : PlacementFailReason.NoSupport;
        }

        /// <summary>Enumerates horizontal columns touched by the wall's edge.</summary>
        /// <param name="key">Wall key.</param>
        public override IEnumerable<Vector2Int> GetOccupiedColumns(PieceKey key)
        {
            yield return new Vector2Int(key.Anchor.x, key.Anchor.z);
            var adjacent = key.Anchor + BuildGrid.SideOffset(key.Variant);
            yield return new Vector2Int(adjacent.x, adjacent.z);
        }

        /// <summary>
        /// Creates marker candidates around existing floors and directly above existing walls.
        /// </summary>
        /// <param name="grid">Grid to inspect.</param>
        public override IEnumerable<MarkerSpot> GetMarkerSpots(BuildGrid grid)
        {
            if (grid == null)
                yield break;

            foreach (var piece in grid.Occupied.Values)
            {
                if (piece == null || piece.Definition == null)
                    continue;

                if (piece.Definition is FloorPiece)
                {
                    var cell = piece.Key.Anchor;
                    for (var side = 0; side < 4; side++)
                    {
                        var key = new PieceKey(PieceId, cell, side);
                        if (CheckPlacement(key, grid) != PlacementFailReason.None)
                            continue;

                        yield return new MarkerSpot
                        {
                            Piece = this,
                            Grid = grid,
                            Key = key,
                            Position = grid.EdgePoint(cell, side),
                            Rotation = grid.Rotation * Quaternion.Euler(0f, side * 90f, 0f)
                        };
                    }
                }
                else if (piece.Definition is WallPiece)
                {
                    var above = piece.Key.Anchor + Vector3Int.up;
                    var side = piece.Key.Variant;
                    var key = new PieceKey(PieceId, above, side);
                    if (CheckPlacement(key, grid) != PlacementFailReason.None)
                        continue;

                    yield return new MarkerSpot
                    {
                        Piece = this,
                        Grid = grid,
                        Key = key,
                        Position = grid.EdgePoint(above, side),
                        Rotation = grid.Rotation * Quaternion.Euler(0f, side * 90f, 0f)
                    };
                }
            }
        }

        /// <summary>Walls require a marker and do not snap directly to arbitrary collider hits.</summary>
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

        /// <summary>Rotates the wall by 180 degrees for each scroll step.</summary>
        /// <param name="sign">Positive or negative scroll direction.</param>
        /// <param name="state">Current orientation state.</param>
        public override void OnScroll(int sign, BuildInputState state)
        {
            if (state == null || sign == 0)
                return;

            var delta = sign > 0 ? 2 : -2;
            state.SetRot(this, (state.GetRot(this) + delta + 4) & 3);
        }

        /// <summary>Builds the placement result for a wall marker.</summary>
        /// <param name="spot">Candidate marker.</param>
        /// <param name="material">Selected material.</param>
        /// <param name="input">Current input state; wall markers define their own side.</param>
        public override PlacementResult FromMarker(MarkerSpot spot, MaterialDefinition material, BuildInputState input)
        {
            var prefab = GetPrefab(material);
            var reason = prefab == null
                ? PlacementFailReason.NoMaterialVariant
                : CheckPlacement(spot.Key, spot.Grid);

            return new PlacementResult
            {
                Show = true,
                Valid = reason == PlacementFailReason.None,
                FailReason = reason,
                Key = spot.Key,
                Position = spot.Position,
                Rotation = spot.Rotation,
                TargetGrid = spot.Grid,
                Prefab = prefab,
                GhostPrefab = GetGhostPrefab(material)
            };
        }

        /// <summary>Returns wall bounds used for collision/overlap validation.</summary>
        /// <param name="cell">Horizontal grid cell size.</param>
        /// <param name="level">Vertical grid level size.</param>
        /// <param name="thickness">Wall thickness.</param>
        public override Bounds GetLocalBounds(float cell, float level, float thickness)
            => new Bounds(Vector3.up * (level * 0.5f), new Vector3(cell, level, thickness));

        /// <summary>Prevents removing this wall when upper floors depend on it.</summary>
        /// <param name="key">Wall key.</param>
        /// <param name="grid">Owning grid.</param>
        public override DestroyFailReason CanBeDestroyed(PieceKey key, BuildGrid grid)
            => grid == null || grid.WouldRemainSupported(key)
                ? DestroyFailReason.None
                : DestroyFailReason.HasFloorOnWall;

        /// <summary>Enumerates upper floors and the wall segment above that depend on this wall.</summary>
        /// <param name="key">Wall key.</param>
        /// <param name="grid">Owning grid.</param>
        public override IEnumerable<BuildPiece> GetDependents(PieceKey key, BuildGrid grid)
        {
            if (grid == null)
                yield break;

            foreach (var aboveFloor in GetSupportedFloorCells(key))
            {
                if (grid.TryGetPieceAtSlot(Slot.CellSlot(aboveFloor), out var floor)
                    && floor.Definition is FloorPiece)
                {
                    yield return floor;
                }
            }

            var aboveAnchor = key.Anchor + Vector3Int.up;
            if (grid.TryGetPieceAtSlot(Slot.Edge(aboveAnchor, key.Variant), out var upperWall)
                && upperWall.Definition is WallPiece)
            {
                yield return upperWall;
            }
        }
    }
}
