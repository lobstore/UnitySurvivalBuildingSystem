using System.Collections.Generic;
using UnityEngine;
namespace LOGIYGames.SurvivalBuilding
{
    [CreateAssetMenu(menuName = "Building/Wall")]
    public class WallPiece : PieceDefinition
    {
        public override IEnumerable<Slot> GetSlots(PieceKey key)
        {
            yield return Slot.Edge(key.Anchor, key.Variant);
        }

        public override IEnumerable<Vector3Int> GetSupportedFloorCells(PieceKey key)
        {
            yield return key.Anchor + new Vector3Int(0, 1, 0);
            yield return key.Anchor + BuildGrid.SideOffset(key.Variant) + new Vector3Int(0, 1, 0);
        }

        public override bool ValidatePlacement(PieceKey key, BuildGrid grid)
        {
            // слот уже занят?
            if (grid.IsSlotOccupied(Slot.Edge(key.Anchor, key.Variant))) return false;

            // опора снизу: пол в клетке anchor
            bool hasFloor = grid.TryGetPieceAtSlot(Slot.CellSlot(key.Anchor), out var floor)
                         && floor.Definition is FloorPiece;

            // опора снизу: стена на том же ребре уровнем ниже
            var belowAnchor = key.Anchor + Vector3Int.down;
            bool hasWall = grid.TryGetPieceAtSlot(Slot.Edge(belowAnchor, key.Variant), out var below)
                        && below.Definition is WallPiece;

            return hasFloor || hasWall;
        }
        public override IEnumerable<Vector2Int> GetOccupiedColumns(PieceKey key)
        {
            yield return new Vector2Int(key.Anchor.x, key.Anchor.z);
            var b = key.Anchor + BuildGrid.SideOffset(key.Variant);
            yield return new Vector2Int(b.x, b.z);
        }
        public override IEnumerable<MarkerSpot> GetMarkerSpots(BuildGrid grid)
        {
            foreach (var piece in grid.Occupied.Values)
            {
                // --- стены на полу ---
                if (piece.Definition is FloorPiece)
                {
                    var cell = piece.Key.Anchor;
                    for (int s = 0; s < 4; s++)
                    {
                        if (grid.IsSlotOccupied(Slot.Edge(cell, s))) continue;

                        yield return new MarkerSpot
                        {
                            Piece = this,
                            Grid = grid,
                            Key = new PieceKey(PieceId, cell, s),
                            Position = grid.EdgePoint(cell, s),
                            Rotation = grid.Rotation * Quaternion.Euler(0f, s * 90f, 0f),
                        };
                    }
                }
                // --- стены на стенах ---
                else if (piece.Definition is WallPiece)
                {
                    var above = piece.Key.Anchor + Vector3Int.up;
                    int side = piece.Key.Variant;

                    if (grid.IsSlotOccupied(Slot.Edge(above, side))) continue;

                    yield return new MarkerSpot
                    {
                        Piece = this,
                        Grid = grid,
                        Key = new PieceKey(PieceId, above, side),
                        Position = grid.EdgePoint(above, side),
                        Rotation = grid.Rotation * Quaternion.Euler(0f, side * 90f, 0f),
                    };
                }
            }
        }

        public override PlacementResult Evaluate(PlacementContext ctx, MaterialDefinition mat)
        {
            if (GetPrefab(mat) == null)
                return new PlacementResult { Show = false, FailReason = PlacementFailReason.NoMaterialVariant };

            // Стена не снапится ни к чему, кроме маркера.
            // Всё, что попало сюда, — свободный красный ghost.
            return FreeInvalid(ctx, mat, PlacementFailReason.WrongSurface);
        }
        PlacementFailReason ReasonFor(PieceKey key, BuildGrid grid)
        {
            if (grid.IsSlotOccupied(Slot.Edge(key.Anchor, key.Variant)))
                return PlacementFailReason.SlotOccupied;

            bool hasFloor = grid.TryGetPieceAtSlot(Slot.CellSlot(key.Anchor), out var floor)
                         && floor.Definition is FloorPiece;

            var belowAnchor = key.Anchor + Vector3Int.down;
            bool hasWall = grid.TryGetPieceAtSlot(Slot.Edge(belowAnchor, key.Variant), out var below)
                        && below.Definition is WallPiece;

            if (!hasFloor && !hasWall) return PlacementFailReason.NoSupport;
            return PlacementFailReason.None;
        }
        public override void OnScroll(int sign, BuildInputState state)
        {
            // 180°-поворот: +2 ≡ -2 (mod 4), но пишем симметрично — легче читать
            state.SetRot(this, (state.GetRot(this) + (sign > 0 ? 2 : -2) + 4) & 3);
        }

        static int SideFromWorld(BuildGrid grid, Vector3 worldDir)
            => grid.SideFromWorld(worldDir);   // ← и здесь, и в BuildPlanner


        public override PlacementResult FromMarker(MarkerSpot spot, MaterialDefinition mat, BuildInputState input)
        {
            bool ok = ValidatePlacement(spot.Key, spot.Grid);
            return new PlacementResult
            {
                Show = true,
                Valid = ok,
                FailReason = ok ? PlacementFailReason.None : ReasonFor(spot.Key, spot.Grid),
                Key = spot.Key,
                Position = spot.Position,
                Rotation = spot.Rotation,
                TargetGrid = spot.Grid,
                Prefab = GetPrefab(mat),
                GhostPrefab = GetGhostPrefab(mat),
            };
        }
        public override Bounds GetLocalBounds(float cell, float level, float thickness)
        => new Bounds(Vector3.up * (level * 0.5f), new Vector3(cell, level, thickness));
        public override DestroyFailReason CanBeDestroyed(PieceKey key, BuildGrid grid)
        {
            if (grid.WouldRemainSupported(key)) return DestroyFailReason.None;
            return DestroyFailReason.HasFloorOnWall;
        }
        public override PlacementFailReason CheckPlacement(PieceKey key, BuildGrid grid)
        => ReasonFor(key, grid);
        public override IEnumerable<BuildPiece> GetDependents(PieceKey key, BuildGrid grid)
        {
            // полы сверху (на клетках с обеих сторон ребра)
            foreach (var above in GetSupportedFloorCells(key))
            {
                if (grid.TryGetPieceAtSlot(Slot.CellSlot(above), out var p) && p.Definition is FloorPiece)
                    yield return p;
            }

            // стена сверху — на том же ребре уровнем выше
            var aboveAnchor = key.Anchor + Vector3Int.up;
            if (grid.TryGetPieceAtSlot(Slot.Edge(aboveAnchor, key.Variant), out var wall)
                && wall.Definition is WallPiece)
                yield return wall;
        }
    }

}