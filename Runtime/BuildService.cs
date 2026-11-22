using LOGIYGames.Extensions;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace LOGIYGames.SurvivalBuilding
{
    /// <summary>
    /// Owns building-grid lifecycle and performs the authoritative commit/removal operations.
    /// Placement evaluation remains in piece definitions and the planner.
    /// </summary>
    public class BuildService : MonoBehaviour
    {
        [Header("Refs")]
        public Transform GridParent;
        public GridSettings Settings = new GridSettings();

        [Tooltip("If enabled, unsupported pieces become physical falling fragments.")]
        public bool EnablePhysicalDestruction = true;

        /// <summary>Gets the grids currently managed by this service.</summary>
        public readonly List<BuildGrid> Grids = new();

        /// <summary>Raised after the registered building state changes.</summary>
        public event Action OnChanged;

        private BuildEvents Events => BuildEvents.Instance;

        /// <summary>
        /// Creates and registers a grid using the current cell and level dimensions.
        /// </summary>
        /// <param name="origin">World-space position of the grid's anchor cell.</param>
        /// <param name="rotation">World-space orientation of the grid.</param>
        /// <returns>The newly registered grid.</returns>
        public BuildGrid CreateGrid(Vector3 origin, Quaternion rotation)
        {
            EnsureSettings();

            var host = new GameObject("BuildingGrid");
            if (GridParent != null)
                host.transform.SetParent(GridParent, false);

            var grid = host.AddComponent<BuildGrid>();
            grid.Origin = origin;
            grid.Rotation = rotation;
            grid.Cell = Settings.Cell;
            grid.Level = Settings.Level;
            Grids.Add(grid);
            return grid;
        }

        /// <summary>Finds the nearest managed grid whose bounds are within the supplied distance.</summary>
        /// <param name="worldPosition">The query position in world space.</param>
        /// <param name="maxDistance">Maximum accepted distance in world units.</param>
        /// <returns>The nearest grid, or <see langword="null"/> when none is close enough.</returns>
        public BuildGrid FindNearest(Vector3 worldPosition, float maxDistance)
        {
            BuildGrid best = null;
            var bestDistance = Mathf.Max(0f, maxDistance);

            foreach (var grid in Grids)
            {
                if (grid == null)
                    continue;

                var distance = grid.DistanceToBounds(worldPosition);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = grid;
                }
            }

            return best;
        }

        /// <summary>
        /// Instantiates a validated placement, resolves occupied-slot replacements and registers the piece.
        /// </summary>
        /// <param name="placement">The validated placement result.</param>
        /// <param name="definition">The definition of the piece being created.</param>
        /// <param name="material">The material selected for the piece.</param>
        /// <returns>The registered runtime piece.</returns>
        /// <exception cref="ArgumentException">The placement is invalid or has no prefab.</exception>
        /// <exception cref="ArgumentNullException">A required definition or material is missing.</exception>
        public BuildPiece Commit(PlacementResult placement, PieceDefinition definition, MaterialDefinition material)
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));
            if (material == null)
                throw new ArgumentNullException(nameof(material));
            if (!placement.Show || !placement.Valid || placement.Prefab == null)
                throw new ArgumentException("Only a visible, valid placement with a prefab can be committed.", nameof(placement));

            var grid = placement.TargetGrid;
            var createdGrid = grid == null;
            if (createdGrid)
                grid = CreateGrid(placement.Position, placement.Rotation);

            var slots = new List<Slot>(definition.GetSlots(placement.Key));
            var replaced = new HashSet<BuildPiece>();

            // Gather first, then remove: a multi-slot piece can be encountered through several slots.
            foreach (var slot in slots)
            {
                if (grid.TryGetPieceAtSlot(slot, out var existing) && existing != null)
                    replaced.Add(existing);
            }

            foreach (var existing in replaced)
                RemoveInternal(existing, cleanupGrid: false, notifyChanged: false);

            var instance = Instantiate(placement.Prefab, placement.Position, placement.Rotation, grid.transform);
            var piece = instance.GetOrAddComponent<BuildPiece>();
            piece.Init(placement.Key, grid, definition, material);
            grid.Register(placement.Key, piece, slots);

            Events?.RaisePlaced(definition, material, placement.Position);
            OnChanged?.Invoke();
            return piece;
        }

        /// <summary>Removes a piece from its grid without running the orphan scan.</summary>
        /// <param name="piece">The piece to remove.</param>
        public void Remove(BuildPiece piece)
        {
            RemoveInternal(piece, cleanupGrid: true, notifyChanged: true);
        }

        /// <summary>
        /// Removes a piece and converts any newly unsupported pieces into falling fragments or destroys them.
        /// </summary>
        /// <param name="piece">The piece whose removal may invalidate support relationships.</param>
        public void RemoveWithPhysics(BuildPiece piece)
        {
            if (piece == null || piece.Grid == null)
                return;

            var grid = piece.Grid;
            RemoveInternal(piece, cleanupGrid: false, notifyChanged: false);

            var orphans = FindOrphanedPieces(grid);
            foreach (var orphan in orphans)
            {
                if (orphan == null || orphan.Grid != grid)
                    continue;

                grid.Unregister(orphan.Key);

                if (EnablePhysicalDestruction)
                {
                    orphan.BeginFalling();
                    Events?.RaiseOrphaned(orphan);
                }
                else
                {
                    orphan.DetachFromGrid();
                    Events?.RaiseRemoved(orphan);
                    Destroy(orphan.gameObject);
                }
            }

            CleanupGridIfEmpty(grid);
            OnChanged?.Invoke();
        }

        /// <summary>Removes a piece from grid indices and optionally cleans up the now-empty grid.</summary>
        /// <param name="piece">The piece to unregister and destroy.</param>
        /// <param name="cleanupGrid">Whether to destroy the grid if it becomes empty.</param>
        /// <param name="notifyChanged">Whether to raise the service change event.</param>
        private void RemoveInternal(BuildPiece piece, bool cleanupGrid, bool notifyChanged)
        {
            if (piece == null || piece.Grid == null)
                return;

            var grid = piece.Grid;
            grid.Unregister(piece.Key);
            piece.DetachFromGrid();
            Destroy(piece.gameObject);

            if (cleanupGrid)
                CleanupGridIfEmpty(grid);

            Events?.RaiseRemoved(piece);
            if (notifyChanged)
                OnChanged?.Invoke();
        }

        /// <summary>Removes an empty grid from the service and schedules its GameObject for destruction.</summary>
        /// <param name="grid">The grid to inspect.</param>
        private void CleanupGridIfEmpty(BuildGrid grid)
        {
            if (grid == null || grid.HasCells)
                return;

            Grids.Remove(grid);
            Destroy(grid.gameObject);
        }

        /// <summary>Finds all pieces that cannot be reached from a ground-supported floor.</summary>
        /// <param name="grid">The grid to inspect.</param>
        /// <returns>The set of unsupported pieces.</returns>
        private HashSet<BuildPiece> FindOrphanedPieces(BuildGrid grid)
        {
            var supported = new HashSet<BuildPiece>();
            var queue = new Queue<BuildPiece>();

            foreach (var piece in grid.Occupied.Values)
            {
                if (piece == null || piece.Definition == null)
                    continue;

                if (piece.Definition is FloorPiece floor && floor.IsOnGround(piece.Key.Anchor, grid))
                {
                    if (supported.Add(piece))
                        queue.Enqueue(piece);
                }
            }

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var dependent in current.Definition.GetDependents(current.Key, grid))
                {
                    if (dependent != null && supported.Add(dependent))
                        queue.Enqueue(dependent);
                }
            }

            var orphans = new HashSet<BuildPiece>();
            foreach (var piece in grid.Occupied.Values)
            {
                if (piece != null && !supported.Contains(piece))
                    orphans.Add(piece);
            }

            return orphans;
        }

        /// <summary>Validates and repairs runtime grid dimensions before grid creation.</summary>
        private void EnsureSettings()
        {
            if (Settings == null)
                Settings = new GridSettings();

            Settings.Cell = Mathf.Max(0.25f, Settings.Cell);
            Settings.Level = Mathf.Max(0.25f, Settings.Level);
        }
    }
}
