using System.Collections.Generic;
using UnityEngine;
namespace LOGIYGames.SurvivalBuilding
{
    [CreateAssetMenu(menuName = "Building/Stair")]
    public class StairPiece : PieceDefinition
    {
        public override IEnumerable<Slot> GetSlots(PieceKey key)
        {
            yield return Slot.StairSlot(key.Anchor);
        }

        public override IEnumerable<Vector3Int> GetSupportedFloorCells(PieceKey key)
        {
            yield return key.Anchor + new Vector3Int(0, 1, 0);
            yield return key.Anchor + BuildGrid.SideOffset(key.Variant) + new Vector3Int(0, 1, 0);
        }

        public override bool ValidatePlacement(PieceKey key, BuildGrid grid)
        {
            return grid.TryGetPieceAtSlot(Slot.CellSlot(key.Anchor), out var existing)
                && existing.Definition is FloorPiece;
        }

        public override IEnumerable<MarkerSpot> GetMarkerSpots(BuildGrid grid)
        {
            foreach (var piece in grid.Occupied.Values)
            {
                if (piece.Definition is not FloorPiece) continue;

                if (!grid.IsSlotOccupied(Slot.CellSlot(piece.Key.Anchor))) continue;
                if (grid.IsSlotOccupied(Slot.StairSlot(piece.Key.Anchor))) continue;

                yield return new MarkerSpot
                {
                    Piece = this,
                    Grid = grid,
                    Key = new PieceKey(PieceId, piece.Key.Anchor, 0),
                    Position = grid.CellCenter(piece.Key.Anchor),
                    Rotation = grid.Rotation,
                };
            }
        }
        public override IEnumerable<Vector2Int> GetOccupiedColumns(PieceKey key)
        {
            yield return new Vector2Int(key.Anchor.x, key.Anchor.z);
            var b = key.Anchor + BuildGrid.SideOffset(key.Variant);
            yield return new Vector2Int(b.x, b.z);
        }
        public override PlacementResult Evaluate(PlacementContext ctx, MaterialDefinition mat)
        {
            if (GetPrefab(mat) == null)
                return new PlacementResult { Show = false, FailReason = PlacementFailReason.NoMaterialVariant };

            return FreeInvalid(ctx, mat, PlacementFailReason.WrongSurface);
        }

        PlacementFailReason ReasonFor(PieceKey key, BuildGrid grid)
        {
            if (grid.IsSlotOccupied(Slot.StairSlot(key.Anchor)))
                return PlacementFailReason.SlotOccupied;

            if (!grid.TryGetPieceAtSlot(Slot.CellSlot(key.Anchor), out var floor)
                || floor.Definition is not FloorPiece)
                return PlacementFailReason.NoSupport;

            return PlacementFailReason.None;
        }

        public override void OnScroll(int sign, BuildInputState state)
        {
            state.SetRot(this, (state.GetRot(this) + sign + 4) & 3);
        }
        public override PlacementResult FromMarker(MarkerSpot spot, MaterialDefinition mat, BuildInputState input)
        {
            int dir = input.GetRot(this);
            var key = new PieceKey(PieceId, spot.Key.Anchor, dir);

            return new PlacementResult
            {
                Show = true,
                Valid = ValidatePlacement(key, spot.Grid),
                Key = key,
                Position = spot.Grid.CellCenter(key.Anchor),
                Rotation = spot.Grid.Rotation * Quaternion.Euler(0f, dir * 90f, 0f),
                TargetGrid = spot.Grid,
                Prefab = GetPrefab(mat),
                GhostPrefab = GetGhostPrefab(mat),
            };
        }

        public override Bounds GetLocalBounds(float cell, float level, float thickness)
        => new Bounds(Vector3.up * (level * 0.5f), new Vector3(cell, level, cell));
        public override DestroyFailReason CanBeDestroyed(PieceKey key, BuildGrid grid)
        {
            if (grid.WouldRemainSupported(key)) return DestroyFailReason.None;
            return DestroyFailReason.HasFloorOnStair;
        }
        public override IEnumerable<BuildPiece> GetDependents(PieceKey key, BuildGrid grid)
        {
            foreach (var above in GetSupportedFloorCells(key))
            {
                if (grid.TryGetPieceAtSlot(Slot.CellSlot(above), out var p) && p.Definition is FloorPiece)
                    yield return p;
            }
        }
        public override PlacementFailReason CheckPlacement(PieceKey key, BuildGrid grid)
        {
            if (grid.IsSlotOccupied(Slot.StairSlot(key.Anchor)))
                return PlacementFailReason.SlotOccupied;

            if (!grid.TryGetPieceAtSlot(Slot.CellSlot(key.Anchor), out var floor)
                || floor.Definition is not FloorPiece)
                return PlacementFailReason.NoSupport;

            return PlacementFailReason.None;
        }
    }
}