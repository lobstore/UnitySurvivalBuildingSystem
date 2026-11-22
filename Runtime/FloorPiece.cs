using System.Collections.Generic;
using UnityEngine;

namespace LOGIYGames.SurvivalBuilding
{
    [CreateAssetMenu(menuName = "Building/Floor")]
    public class FloorPiece : PieceDefinition
    {
        [Header("Cantilever Rule")]
        [Tooltip("Слои, которые считаются землёй/опорой для пола. Если Nothing — любой не-BuildPiece коллайдер.")]
        public LayerMask GroundLayers = ~0;
        [Tooltip("Если включено — пол не на земле нельзя строить дальше чем N клеток от вертикальной опоры")]
        public bool EnforceCantilever = false;

        [Min(0)] public int MaxCantileverDistance = 3;
        public override bool CanStartNewGrid => true;

        public override IEnumerable<Slot> GetSlots(PieceKey key)
        {
            yield return Slot.CellSlot(key.Anchor);
        }

        public override IEnumerable<Vector3Int> GetSupportedFloorCells(PieceKey key)
        {
            for (int s = 0; s < 4; s++)
                yield return key.Anchor + BuildGrid.SideOffset(s);
        }

        public override bool ValidatePlacement(PieceKey key, BuildGrid grid)
        {
            if (grid.IsSlotOccupied(Slot.CellSlot(key.Anchor))) return false;

            bool hasSupport = IsOnGround(key.Anchor, grid);

            if (!hasSupport)
            {
                foreach (var piece in grid.Occupied.Values)
                {
                    foreach (var c in piece.Definition.GetSupportedFloorCells(piece.Key))
                        if (c == key.Anchor) { hasSupport = true; break; }
                    if (hasSupport) break;
                }
            }

            if (!hasSupport) return false;

            if (EnforceCantilever && !IsWithinCantilever(key.Anchor, grid, MaxCantileverDistance))
                return false;

            return true;
        }
        public override PlacementFailReason CheckPlacement(PieceKey key, BuildGrid grid)
        => ReasonFor(key, grid);
        bool IsWithinCantilever(Vector3Int targetCell, BuildGrid grid, int maxDist)
        {
            if (IsAnchor(targetCell, grid)) return true;
            if (maxDist <= 0) return false;

            var visited = new HashSet<Vector3Int> { targetCell };
            var frontier = new List<Vector3Int> { targetCell };

            for (int dist = 1; dist <= maxDist; dist++)
            {
                var next = new List<Vector3Int>();
                foreach (var c in frontier)
                {
                    for (int s = 0; s < 4; s++)
                    {
                        var nb = c + BuildGrid.SideOffset(s);
                        if (!visited.Add(nb)) continue;

                        // цепь идёт только через существующие полы
                        if (!grid.TryGetPieceAtSlot(Slot.CellSlot(nb), out var piece)) continue;
                        if (piece.Definition is not FloorPiece) continue;

                        if (IsAnchor(nb, grid)) return true;
                        next.Add(nb);
                    }
                }
                if (next.Count == 0) break;
                frontier = next;
            }
            return false;
        }

        bool IsAnchor(Vector3Int cell, BuildGrid grid)
        {
            if (IsOnGround(cell, grid)) return true;

            Vector3 center = grid.CellCenter(cell);
            Vector3 belowCenter = center + Vector3.down * (grid.Level * 0.5f);
            Vector3 halfExtents = new Vector3(grid.Cell * 0.6f, grid.Level * 0.5f, grid.Cell * 0.6f);

            var hits = Physics.OverlapBox(belowCenter, halfExtents, grid.Rotation, ~0,
                                          QueryTriggerInteraction.Ignore);
            foreach (var h in hits)
            {
                var bp = h.GetComponentInParent<BuildPiece>();
                if (bp == null) continue;
                if (bp.Definition is WallPiece || bp.Definition is StairPiece) return true;
            }
            return false;
        }
        public override IEnumerable<MarkerSpot> GetMarkerSpots(BuildGrid grid)
        {
            var seen = new HashSet<Vector3Int>();

            foreach (var piece in grid.Occupied.Values)
            {
                foreach (var c in piece.Definition.GetSupportedFloorCells(piece.Key))
                {
                    if (!seen.Add(c)) continue;
                    if (grid.IsSlotOccupied(Slot.CellSlot(c))) continue;

                    yield return new MarkerSpot
                    {
                        Piece = this,
                        Grid = grid,
                        Key = new PieceKey(PieceId, c, 0),
                        Position = grid.CellCenter(c),
                        Rotation = grid.Rotation,
                    };
                }
            }
        }
        public override PlacementResult Evaluate(PlacementContext ctx, MaterialDefinition mat)
        {
            var prefab = GetPrefab(mat);
            if (prefab == null)
                return new PlacementResult
                {
                    Show = false,
                    FailReason = PlacementFailReason.NoMaterialVariant,
                };

            // ── Попали в деталь — снапа нет (валидный или невалидный не важно) ──
            if (ctx.HitPiece != null)
                return FreeInvalid(ctx, mat, PlacementFailReason.WrongSurface);

            // ── Сетки нет — новая сетка ──
            if (ctx.Grid == null)
            {
                return new PlacementResult
                {
                    Show = true,
                    Valid = true,
                    FailReason = PlacementFailReason.None,
                    Key = new PieceKey(PieceId, Vector3Int.zero, 0),
                    Position = ctx.Hit.point,
                    Rotation = Quaternion.Euler(0f, ctx.FirstYaw, 0f),
                    TargetGrid = null,
                    Prefab = prefab,
                    GhostPrefab = GetGhostPrefab(mat),
                };
            }

            // ── Попали в землю/проп рядом с сеткой — снап к клетке сетки ──
            var cell = ctx.Grid.WorldToCell(ctx.Hit.point);
            var key = new PieceKey(PieceId, cell, 0);
            bool ok = ValidatePlacement(key, ctx.Grid);

            return new PlacementResult
            {
                Show = true,
                Valid = ok,
                FailReason = ok ? PlacementFailReason.None : ReasonFor(key, ctx.Grid),
                Key = key,
                Position = ctx.Grid.CellCenter(cell),
                Rotation = ctx.Grid.Rotation * Quaternion.Euler(0f, ctx.Input.GetRot(this) * 90f, 0f),
                TargetGrid = ctx.Grid,
                Prefab = prefab,
                GhostPrefab = GetGhostPrefab(mat),
            };
        }

        static Vector3Int AboveWall(BuildGrid grid, BuildPiece wall, Vector3 observer)
        {
            var a = wall.Key.Anchor;
            int side = wall.Key.Variant;
            var b = a + BuildGrid.SideOffset(side);

            Vector3 wallPos = grid.EdgePoint(a, side);
            Vector3 normal = grid.Rotation * BuildGrid.SideDir(side);

            float d = Vector3.Dot(observer - wallPos, normal);
            var cell = (d > 0 ? b : a);
            cell.y += 1;
            return cell;
        }
        static Vector3Int AboveOrBeforeStair(BuildGrid grid, BuildPiece stair, Vector3 observer)
        {
            var a = stair.Key.Anchor;
            int dir = stair.Key.Variant;
            var b = a + BuildGrid.SideOffset(dir);

            Vector3 center = grid.CellCenter(a);
            Vector3 dirWorld = grid.Rotation * BuildGrid.SideDir(dir);

            float d = Vector3.Dot(observer - center, dirWorld);
            var cell = (d > 0 ? b : a);
            cell.y += 1;
            return cell;
        }
        PlacementFailReason ReasonFor(PieceKey key, BuildGrid grid)
        {
            if (grid.IsSlotOccupied(Slot.CellSlot(key.Anchor)))
                return PlacementFailReason.SlotOccupied;

            bool hasSupport = IsOnGround(key.Anchor, grid);
            if (!hasSupport)
            {
                foreach (var piece in grid.Occupied.Values)
                {
                    foreach (var c in piece.Definition.GetSupportedFloorCells(piece.Key))
                        if (c == key.Anchor) { hasSupport = true; break; }
                    if (hasSupport) break;
                }
            }
            if (!hasSupport) return PlacementFailReason.NoSupport;

            if (EnforceCantilever && !IsWithinCantilever(key.Anchor, grid, MaxCantileverDistance))
                return PlacementFailReason.CantileverTooFar;

            return PlacementFailReason.None;
        }
        public override void OnScroll(int sign, BuildInputState state)
        {
            state.SetRot(this, (state.GetRot(this) + sign + 4) & 3);
        }

        static Vector3Int AdjacentFromEdge(BuildGrid grid, Vector3 hitPoint, Vector3Int baseCell)
        {
            Vector3 center = grid.CellCenter(baseCell);
            Vector3 local = Quaternion.Inverse(grid.Rotation) * (hitPoint - center);
            float ax = Mathf.Abs(local.x), az = Mathf.Abs(local.z);

            if (ax < 0.01f && az < 0.01f)
                return baseCell + BuildGrid.SideOffset(0);
            if (ax > az) return baseCell + new Vector3Int((int)Mathf.Sign(local.x), 0, 0);
            return baseCell + new Vector3Int(0, 0, (int)Mathf.Sign(local.z));
        }
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
                Rotation = spot.Grid.Rotation * Quaternion.Euler(0f, input.GetRot(this) * 90f, 0f),
                TargetGrid = spot.Grid,
                Prefab = GetPrefab(mat),
                GhostPrefab = GetGhostPrefab(mat),
            };
        }
        public override Bounds GetLocalBounds(float cell, float level, float thickness)
        => new Bounds(Vector3.up * (thickness * 0.5f), new Vector3(cell, thickness, cell));
        public bool IsOnGround(Vector3Int cell, BuildGrid grid)
        {
            Vector3 center = grid.CellCenter(cell);

            Vector3 boxCenter = center - Vector3.up * 0.05f;
            Vector3 halfExtents = new Vector3(grid.Cell * 0.45f, 0.1f, grid.Cell * 0.45f);

            int mask = GroundLayers.value == 0 ? ~0 : GroundLayers.value;
            var hits = Physics.OverlapBox(boxCenter, halfExtents, grid.Rotation, mask,
                                          QueryTriggerInteraction.Ignore);
            foreach (var h in hits)
            {
                if (h.GetComponentInParent<BuildPiece>() != null) continue;
                return true;
            }
            return false;
        }
        public override DestroyFailReason CanBeDestroyed(PieceKey key, BuildGrid grid)
        {
            // Если после удаления все остальные сохраняют опору — можно
            if (grid.WouldRemainSupported(key)) return DestroyFailReason.None;

            // Иначе — детализируем, что именно упадёт
            var cell = key.Anchor;
            if (grid.IsSlotOccupied(Slot.StairSlot(cell)))
                return DestroyFailReason.HasStairOnFloor;

            for (int s = 0; s < 4; s++)
                if (grid.IsSlotOccupied(Slot.Edge(cell, s)))
                    return DestroyFailReason.HasWallOnFloor;

            return DestroyFailReason.WouldOrphanAdjacentFloor;
        }
        public override IEnumerable<BuildPiece> GetDependents(PieceKey key, BuildGrid grid)
        {
            var cell = key.Anchor;

            // соседние полы
            for (int s = 0; s < 4; s++)
            {
                var nb = cell + BuildGrid.SideOffset(s);
                if (grid.TryGetPieceAtSlot(Slot.CellSlot(nb), out var np) && np.Definition is FloorPiece)
                    yield return np;
            }

            // стены на рёбрах — только те, что опираются на ЭТОТ пол
            for (int s = 0; s < 4; s++)
            {
                if (grid.TryGetPieceAtSlot(Slot.Edge(cell, s), out var w)
                    && w.Definition is WallPiece
                    && w.Key.Anchor == cell)
                    yield return w;
            }

            // лестница в клетке
            if (grid.TryGetPieceAtSlot(Slot.StairSlot(cell), out var st))
                yield return st;
        }
    }
}