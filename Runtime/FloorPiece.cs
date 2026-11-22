using System.Collections.Generic;
using UnityEngine;

namespace LOGIYGames.SurvivalBuilding
{
    /// <summary>
    /// Floor placement rules, support propagation and ground-anchor detection.
    /// Floor pieces occupy one cell and may support adjacent floors, walls and stairs.
    /// </summary>
    [CreateAssetMenu(menuName = "Building/Floor")]
    public class FloorPiece : PieceDefinition
    {
        [Header("Cantilever Rule")]
        [Tooltip("Layers treated as ground/support. If zero, every non-building collider is considered ground.")]
        public LayerMask GroundLayers = ~0;

        [Tooltip("When enabled, floors not directly on ground must remain within the configured distance of a support anchor.")]
        public bool EnforceCantilever = false;

        [Min(0)] public int MaxCantileverDistance = 3;

        /// <inheritdoc/>
        public override bool CanStartNewGrid => true;

        /// <summary>Reserves the cell occupied by this floor.</summary>
        /// <param name="key">Floor key.</param>
        public override IEnumerable<Slot> GetSlots(PieceKey key)
        {
            yield return Slot.CellSlot(key.Anchor);
        }

        /// <summary>Returns the four horizontal cells that may receive support from this floor.</summary>
        /// <param name="key">Floor key.</param>
        public override IEnumerable<Vector3Int> GetSupportedFloorCells(PieceKey key)
        {
            for (var side = 0; side < 4; side++)
                yield return key.Anchor + BuildGrid.SideOffset(side);
        }

        /// <summary>Checks occupancy, ground/support and optional cantilever distance.</summary>
        /// <param name="key">Candidate floor key.</param>
        /// <param name="grid">Target building grid.</param>
        public override bool ValidatePlacement(PieceKey key, BuildGrid grid)
            => CheckPlacement(key, grid) == PlacementFailReason.None;

        /// <summary>Returns the specific failure reason for a floor placement candidate.</summary>
        /// <param name="key">Candidate floor key.</param>
        /// <param name="grid">Target building grid.</param>
        public override PlacementFailReason CheckPlacement(PieceKey key, BuildGrid grid)
        {
            if (grid == null)
                return PlacementFailReason.NoSupport;

            if (grid.IsSlotOccupied(Slot.CellSlot(key.Anchor)))
                return PlacementFailReason.SlotOccupied;

            if (!HasFloorSupport(key.Anchor, grid))
                return PlacementFailReason.NoSupport;

            if (EnforceCantilever && !IsWithinCantilever(key.Anchor, grid, MaxCantileverDistance))
                return PlacementFailReason.CantileverTooFar;

            return PlacementFailReason.None;
        }

        /// <summary>Enumerates free neighbouring floor cells supported by existing pieces.</summary>
        /// <param name="grid">Grid whose current pieces determine candidate positions.</param>
        public override IEnumerable<MarkerSpot> GetMarkerSpots(BuildGrid grid)
        {
            if (grid == null)
                yield break;

            var seen = new HashSet<Vector3Int>();
            foreach (var piece in grid.Occupied.Values)
            {
                if (piece == null || piece.Definition == null)
                    continue;

                foreach (var cell in piece.Definition.GetSupportedFloorCells(piece.Key))
                {
                    if (!seen.Add(cell) || grid.IsSlotOccupied(Slot.CellSlot(cell)))
                        continue;

                    var candidateKey = new PieceKey(PieceId, cell, 0);
                    yield return new MarkerSpot
                    {
                        Piece = this,
                        Grid = grid,
                        Key = candidateKey,
                        Position = grid.CellCenter(cell),
                        Rotation = grid.Rotation
                    };
                }
            }
        }

        /// <summary>
        /// Evaluates a hit as a free floor placement or a snapped placement inside an existing grid.
        /// </summary>
        /// <param name="context">Current raycast and placement context.</param>
        /// <param name="material">Selected material.</param>
        public override PlacementResult Evaluate(PlacementContext context, MaterialDefinition material)
        {
            var prefab = GetPrefab(material);
            if (prefab == null)
            {
                return new PlacementResult
                {
                    Show = true,
                    Valid = false,
                    FailReason = PlacementFailReason.NoMaterialVariant,
                    Position = context.Hit.point,
                    Rotation = Quaternion.Euler(0f, context.FirstYaw, 0f),
                    Prefab = null,
                    GhostPrefab = null
                };
            }

            // Floors do not snap through an existing build piece.
            if (context.HitPiece != null)
                return FreeInvalid(context, material, PlacementFailReason.WrongSurface);

            if (context.Grid == null)
            {
                return new PlacementResult
                {
                    Show = true,
                    Valid = true,
                    FailReason = PlacementFailReason.None,
                    Key = new PieceKey(PieceId, Vector3Int.zero, 0),
                    Position = context.Hit.point,
                    Rotation = Quaternion.Euler(0f, context.FirstYaw, 0f),
                    TargetGrid = null,
                    Prefab = prefab,
                    GhostPrefab = GetGhostPrefab(material)
                };
            }

            var cell = context.Grid.WorldToCell(context.Hit.point);
            var key = new PieceKey(PieceId, cell, 0);
            var reason = CheckPlacement(key, context.Grid);

            return new PlacementResult
            {
                Show = true,
                Valid = reason == PlacementFailReason.None,
                FailReason = reason,
                Key = key,
                Position = context.Grid.CellCenter(cell),
                Rotation = context.Grid.Rotation * Quaternion.Euler(0f, context.Input.GetRot(this) * 90f, 0f),
                TargetGrid = context.Grid,
                Prefab = prefab,
                GhostPrefab = GetGhostPrefab(material)
            };
        }

        /// <summary>Applies quarter-turn rotation increments to this floor definition.</summary>
        /// <param name="sign">Positive or negative scroll direction.</param>
        /// <param name="state">Mutable input rotation state.</param>
        public override void OnScroll(int sign, BuildInputState state)
        {
            if (state == null || sign == 0)
                return;

            state.SetRot(this, (state.GetRot(this) + sign + 4) & 3);
        }

        /// <summary>Builds a placement result from one of this definition's markers.</summary>
        /// <param name="spot">Candidate marker.</param>
        /// <param name="material">Selected material.</param>
        /// <param name="input">Current orientation state.</param>
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
                Rotation = spot.Grid.Rotation * Quaternion.Euler(0f, input.GetRot(this) * 90f, 0f),
                TargetGrid = spot.Grid,
                Prefab = prefab,
                GhostPrefab = GetGhostPrefab(material)
            };
        }

        /// <summary>Returns the floor bounds used by world-overlap checks.</summary>
        /// <param name="cell">Horizontal cell size.</param>
        /// <param name="level">Vertical level size.</param>
        /// <param name="thickness">Floor thickness.</param>
        public override Bounds GetLocalBounds(float cell, float level, float thickness)
            => new Bounds(Vector3.up * (thickness * 0.5f), new Vector3(cell, thickness, cell));

        /// <summary>
        /// Checks whether a floor cell has a collider on one of the configured ground layers below it.
        /// </summary>
        /// <param name="cell">Grid cell to inspect.</param>
        /// <param name="grid">Owning grid.</param>
        /// <returns>True when a non-building collider is found below the floor.</returns>
        public bool IsOnGround(Vector3Int cell, BuildGrid grid)
        {
            if (grid == null)
                return false;

            var center = grid.CellCenter(cell);
            var boxCenter = center - Vector3.up * 0.05f;
            var halfExtents = new Vector3(grid.Cell * 0.45f, 0.1f, grid.Cell * 0.45f);
            var mask = GroundLayers.value == 0 ? ~0 : GroundLayers.value;

            var hits = Physics.OverlapBox(boxCenter, halfExtents, grid.Rotation, mask,
                QueryTriggerInteraction.Ignore);

            foreach (var hit in hits)
            {
                if (hit.GetComponentInParent<BuildPiece>() != null)
                    continue;

                return true;
            }

            return false;
        }

        /// <summary>Prevents removal when a stair, wall or adjacent floor would lose support.</summary>
        /// <param name="key">Key of the floor being checked.</param>
        /// <param name="grid">Owning grid.</param>
        public override DestroyFailReason CanBeDestroyed(PieceKey key, BuildGrid grid)
        {
            if (grid == null || grid.WouldRemainSupported(key))
                return DestroyFailReason.None;

            if (grid.IsSlotOccupied(Slot.StairSlot(key.Anchor)))
                return DestroyFailReason.HasStairOnFloor;

            for (var side = 0; side < 4; side++)
            {
                if (grid.IsSlotOccupied(Slot.Edge(key.Anchor, side)))
                    return DestroyFailReason.HasWallOnFloor;
            }

            return DestroyFailReason.WouldOrphanAdjacentFloor;
        }

        /// <summary>Enumerates pieces whose support graph depends on this floor.</summary>
        /// <param name="key">Key of this floor.</param>
        /// <param name="grid">Owning grid.</param>
        public override IEnumerable<BuildPiece> GetDependents(PieceKey key, BuildGrid grid)
        {
            if (grid == null)
                yield break;

            var cell = key.Anchor;

            // Adjacent floor slabs can be supported by this slab.
            for (var side = 0; side < 4; side++)
            {
                var neighbour = cell + BuildGrid.SideOffset(side);
                if (grid.TryGetPieceAtSlot(Slot.CellSlot(neighbour), out var adjacent)
                    && adjacent.Definition is FloorPiece)
                {
                    yield return adjacent;
                }
            }

            // Only walls anchored to this cell are dependent on this floor.
            for (var side = 0; side < 4; side++)
            {
                if (grid.TryGetPieceAtSlot(Slot.Edge(cell, side), out var wall)
                    && wall.Definition is WallPiece
                    && wall.Key.Anchor == cell)
                {
                    yield return wall;
                }
            }

            if (grid.TryGetPieceAtSlot(Slot.StairSlot(cell), out var stair))
                yield return stair;
        }

        /// <summary>Determines whether a floor cell is supported directly or indirectly by a nearby anchor.</summary>
        /// <param name="targetCell">Candidate floor cell.</param>
        /// <param name="grid">Grid to traverse.</param>
        /// <param name="maxDistance">Maximum number of existing floor edges to traverse.</param>
        private bool IsWithinCantilever(Vector3Int targetCell, BuildGrid grid, int maxDistance)
        {
            if (IsAnchor(targetCell, grid))
                return true;

            if (maxDistance <= 0)
                return false;

            var visited = new HashSet<Vector3Int> { targetCell };
            var frontier = new List<Vector3Int> { targetCell };

            for (var distance = 1; distance <= maxDistance; distance++)
            {
                var next = new List<Vector3Int>();

                foreach (var cell in frontier)
                {
                    for (var side = 0; side < 4; side++)
                    {
                        var neighbour = cell + BuildGrid.SideOffset(side);
                        if (!visited.Add(neighbour))
                            continue;

                        if (!grid.TryGetPieceAtSlot(Slot.CellSlot(neighbour), out var piece)
                            || piece.Definition is not FloorPiece)
                        {
                            continue;
                        }

                        if (IsAnchor(neighbour, grid))
                            return true;

                        next.Add(neighbour);
                    }
                }

                if (next.Count == 0)
                    break;

                frontier = next;
            }

            return false;
        }

        /// <summary>Checks for a ground surface or a registered wall/stair below the given cell.</summary>
        /// <param name="cell">Candidate anchor cell.</param>
        /// <param name="grid">Grid to query.</param>
        private bool IsAnchor(Vector3Int cell, BuildGrid grid)
        {
            if (IsOnGround(cell, grid))
                return true;

            var center = grid.CellCenter(cell);
            var belowCenter = center + Vector3.down * (grid.Level * 0.5f);
            var halfExtents = new Vector3(grid.Cell * 0.6f, grid.Level * 0.5f, grid.Cell * 0.6f);
            var hits = Physics.OverlapBox(belowCenter, halfExtents, grid.Rotation, ~0,
                QueryTriggerInteraction.Ignore);

            foreach (var hit in hits)
            {
                var piece = hit.GetComponentInParent<BuildPiece>();
                if (piece == null || !piece.IsInGrid || piece.Definition == null)
                    continue;

                if (piece.Definition is WallPiece || piece.Definition is StairPiece)
                    return true;
            }

            return false;
        }

        /// <summary>Checks whether the candidate cell is supported by ground or an existing support surface.</summary>
        /// <param name="cell">Candidate floor cell.</param>
        /// <param name="grid">Grid to inspect.</param>
        private bool HasFloorSupport(Vector3Int cell, BuildGrid grid)
        {
            if (IsOnGround(cell, grid))
                return true;

            foreach (var piece in grid.Occupied.Values)
            {
                if (piece == null || piece.Definition == null)
                    continue;

                foreach (var supportedCell in piece.Definition.GetSupportedFloorCells(piece.Key))
                {
                    if (supportedCell == cell)
                        return true;
                }
            }

            return false;
        }
    }
}
