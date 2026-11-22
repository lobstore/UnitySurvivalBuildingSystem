using LOGIYGames.Extensions;
using System.Collections.Generic;
using UnityEngine;

namespace LOGIYGames.SurvivalBuilding
{
    public class BuildService : MonoBehaviour
    {

        [Header("Refs")]
        public Transform GridParent;
        public GridSettings Settings = new GridSettings();

        [Tooltip("Если включено — потерявшие опору части падают как физические обломки. Если выключено — сразу удаляются.")]
        public bool EnablePhysicalDestruction = true;

        public readonly List<BuildGrid> Grids = new();

        public event System.Action OnChanged;

        // lazy — не зависит от порядка инициализации
        BuildEvents Events => BuildEvents.Instance;

        // =========================================================
        // Grid lifecycle
        // =========================================================

        public BuildGrid CreateGrid(Vector3 origin, Quaternion rotation)
        {
            var go = new GameObject("BuildingGrid");
            if (GridParent != null) go.transform.SetParent(GridParent, false);

            var g = go.AddComponent<BuildGrid>();
            g.Origin = origin;
            g.Rotation = rotation;
            g.Cell = Settings.Cell;
            g.Level = Settings.Level;
            Grids.Add(g);
            return g;
        }

        public BuildGrid FindNearest(Vector3 world, float maxDist)
        {
            BuildGrid best = null; float bestD = maxDist;
            foreach (var g in Grids)
            {
                if (g == null) continue;
                float d = g.DistanceToBounds(world);
                if (d < bestD) { bestD = d; best = g; }
            }
            return best;
        }

        // =========================================================
        // Commit
        // =========================================================

        public BuildPiece Commit(PlacementResult p, PieceDefinition def, MaterialDefinition mat)
        {
            var grid = p.TargetGrid;
            if (grid == null) grid = CreateGrid(p.Position, p.Rotation);

            // Удалить всё, что занимает те же слоты (лестница вместо пола и т.п.)
            var slots = new List<Slot>(def.GetSlots(p.Key));
            foreach (var slot in slots)
                if (grid.TryGetPieceAtSlot(slot, out var existing))
                    Remove(existing);

            var go = Instantiate(p.Prefab, p.Position, p.Rotation, grid.transform);
            var piece = go.GetOrAddComponent<BuildPiece>();
            piece.Init(p.Key, grid, def, mat);
            grid.Register(p.Key, piece, slots);

            Events?.RaisePlaced(def, mat, p.Position);   // ← теперь звук/партиклы работают
            OnChanged?.Invoke();
            return piece;
        }

        // =========================================================
        // Remove
        // =========================================================

        public void Remove(BuildPiece piece) => RemoveInternal(piece);

        public void RemoveWithPhysics(BuildPiece piece)
        {
            if (piece == null || piece.Grid == null) return;
            var grid = piece.Grid;

            RemoveInternal(piece);

            var orphans = FindOrphanedPieces(grid);
            foreach (var orphan in orphans)
            {
                if (orphan == null || orphan.Grid != grid) continue;
                grid.Unregister(orphan.Key, orphan.Definition.GetSlots(orphan.Key));

                if (EnablePhysicalDestruction)
                {
                    ConvertToFalling(orphan);
                    Events?.RaiseOrphaned(orphan);
                }
                else
                {
                    Events?.RaiseRemoved(orphan);
                    Destroy(orphan.gameObject);
                }
            }

            // после массового удаления обломков — почистить сетку, если она опустела
            CleanupGridIfEmpty(grid);
            OnChanged?.Invoke();
        }

        void RemoveInternal(BuildPiece piece)
        {
            if (piece == null || piece.Grid == null) return;
            var grid = piece.Grid;

            grid.Unregister(piece.Key, piece.Definition.GetSlots(piece.Key));
            Destroy(piece.gameObject);

            CleanupGridIfEmpty(grid);

            Events?.RaiseRemoved(piece);
            OnChanged?.Invoke();
        }

        void CleanupGridIfEmpty(BuildGrid grid)
        {
            if (grid == null || grid.HasCells) return;
            Grids.Remove(grid);
            Destroy(grid.gameObject);
        }

        // =========================================================
        // Orphan detection / falling
        // =========================================================

        HashSet<BuildPiece> FindOrphanedPieces(BuildGrid grid)
        {
            var valid = new HashSet<BuildPiece>();
            var queue = new Queue<BuildPiece>();

            // якоря: полы на земле
            foreach (var p in grid.Occupied.Values)
            {
                if (p.Definition is FloorPiece fp && fp.IsOnGround(p.Key.Anchor, grid))
                {
                    if (valid.Add(p)) queue.Enqueue(p);
                }
            }

            while (queue.Count > 0)
            {
                var curr = queue.Dequeue();
                foreach (var dep in curr.Definition.GetDependents(curr.Key, grid))
                {
                    if (dep == null) continue;
                    if (valid.Add(dep)) queue.Enqueue(dep);
                }
            }

            var orphans = new HashSet<BuildPiece>();
            foreach (var p in grid.Occupied.Values)
                if (!valid.Contains(p)) orphans.Add(p);
            return orphans;
        }

        void ConvertToFalling(BuildPiece piece)
        {
            piece.BeginFalling();
        }
    }
}