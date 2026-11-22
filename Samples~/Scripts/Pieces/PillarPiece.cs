using System.Collections.Generic;
using UnityEngine;

namespace LOGIYGames.SurvivalBuilding
{
    /// <summary>
    /// Corner support placed where four floor tiles meet. A pillar can support one floor cell
    /// above each of the four surrounding floor cells.
    /// </summary>
    [CreateAssetMenu(menuName = "Building/Pillar")]
    public class PillarPiece : PieceDefinition
    {
        /// <summary>Reserves the canonical corner shared by the four surrounding floor cells.</summary>
        public override IEnumerable<Slot> GetSlots(PieceKey key)
        {
            yield return Slot.Corner(key.Anchor, key.Variant);
        }

        /// <summary>Checks that all four floor cells meet at this corner and that it is unoccupied.</summary>
        public override bool ValidatePlacement(PieceKey key, BuildGrid grid)
            => CheckPlacement(key, grid) == PlacementFailReason.None;

        /// <summary>Returns the specific failure reason for this corner placement.</summary>
        public override PlacementFailReason CheckPlacement(PieceKey key, BuildGrid grid)
        {
            if (grid == null)
                return PlacementFailReason.NoSupport;

            if (grid.IsSlotOccupied(Slot.Corner(key.Anchor, key.Variant)))
                return PlacementFailReason.SlotOccupied;

            return HasFourFloorIntersection(key, grid)
                ? PlacementFailReason.None
                : PlacementFailReason.NoSupport;
        }

        /// <summary>Creates one marker for each unique corner shared by four existing floor pieces.</summary>
        public override IEnumerable<MarkerSpot> GetMarkerSpots(BuildGrid grid)
        {
            if (grid == null)
                yield break;

            var seenCorners = new HashSet<Slot>();
            foreach (var piece in grid.Occupied.Values)
            {
                if (piece == null || piece.Definition is not FloorPiece)
                    continue;

                for (var corner = 0; corner < 4; corner++)
                {
                    var key = new PieceKey(PieceId, piece.Key.Anchor, corner);
                    var slot = Slot.Corner(key.Anchor, key.Variant);
                    if (!seenCorners.Add(slot) || CheckPlacement(key, grid) != PlacementFailReason.None)
                        continue;

                    yield return new MarkerSpot
                    {
                        Piece = this,
                        Grid = grid,
                        Key = key,
                        Position = grid.CornerPoint(key.Anchor, key.Variant),
                        Rotation = grid.Rotation
                    };
                }
            }
        }

        /// <summary>Returns the four floor cells one level above this pillar's corner.</summary>
        public override IEnumerable<Vector3Int> GetSupportedFloorCells(PieceKey key)
        {
            var firstSide = BuildGrid.SideOffset(key.Variant);
            var secondSide = BuildGrid.SideOffset((key.Variant + 1) & 3);
            var upperCell = key.Anchor + Vector3Int.up;

            yield return upperCell;
            yield return upperCell + firstSide;
            yield return upperCell + secondSide;
            yield return upperCell + firstSide + secondSide;
        }

        /// <summary>Includes each of the four floor columns surrounding the pillar's base corner.</summary>
        public override IEnumerable<Vector2Int> GetOccupiedColumns(PieceKey key)
        {
            var firstSide = BuildGrid.SideOffset(key.Variant);
            var secondSide = BuildGrid.SideOffset((key.Variant + 1) & 3);

            yield return new Vector2Int(key.Anchor.x, key.Anchor.z);
            yield return new Vector2Int(key.Anchor.x + firstSide.x, key.Anchor.z + firstSide.z);
            yield return new Vector2Int(key.Anchor.x + secondSide.x, key.Anchor.z + secondSide.z);
            yield return new Vector2Int(
                key.Anchor.x + firstSide.x + secondSide.x,
                key.Anchor.z + firstSide.z + secondSide.z);
        }

        /// <summary>Columns are placed from corner markers, never directly on arbitrary collider hits.</summary>
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

        /// <summary>Builds the placement result from a generated corner marker.</summary>
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

        /// <summary>Returns the narrow post bounds used by world-overlap validation.</summary>
        public override Bounds GetLocalBounds(float cell, float level, float thickness)
        {
            var width = Mathf.Max(thickness, cell * 0.125f);
            return new Bounds(Vector3.up * (level * 0.5f), new Vector3(width, level, width));
        }

        /// <summary>Prevents removing a pillar while any of its four upper floors depends on it.</summary>
        public override DestroyFailReason CanBeDestroyed(PieceKey key, BuildGrid grid)
            => grid == null || grid.WouldRemainSupported(key)
                ? DestroyFailReason.None
                : DestroyFailReason.HasFloorOnPillar;

        /// <summary>Enumerates upper floors supported by this pillar.</summary>
        public override IEnumerable<BuildPiece> GetDependents(PieceKey key, BuildGrid grid)
        {
            if (grid == null)
                yield break;

            foreach (var cell in GetSupportedFloorCells(key))
            {
                if (grid.TryGetPieceAtSlot(Slot.CellSlot(cell), out var floor)
                    && floor.Definition is FloorPiece)
                {
                    yield return floor;
                }
            }
        }

        /// <summary>Checks whether all four floor cells at this canonical corner are still present.</summary>
        public bool HasFourFloorIntersection(PieceKey key, BuildGrid grid)
        {
            if (grid == null)
                return false;

            var firstSide = BuildGrid.SideOffset(key.Variant);
            var secondSide = BuildGrid.SideOffset((key.Variant + 1) & 3);

            return IsFloorAt(grid, key.Anchor)
                && IsFloorAt(grid, key.Anchor + firstSide)
                && IsFloorAt(grid, key.Anchor + secondSide)
                && IsFloorAt(grid, key.Anchor + firstSide + secondSide);
        }

        private static bool IsFloorAt(BuildGrid grid, Vector3Int cell)
            => grid.TryGetPieceAtSlot(Slot.CellSlot(cell), out var piece)
                && piece.Definition is FloorPiece;
    }
}
