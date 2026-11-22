using System.Collections.Generic;
using UnityEngine;

namespace LOGIYGames.SurvivalBuilding
{
    public abstract class PieceDefinition : ScriptableObject
    {
        [Header("Identity")]
        public int PieceId;
        public string DisplayName;

        [Header("UI")]
        [TextArea] public string SurfaceRequirementText = "Сюда нельзя поставить";

        [Header("Marker (общий для всех материалов)")]
        public GameObject MarkerPrefab;

        [Header("Variants by Material")]
        public List<MaterialVariant> Variants = new();

        public GameObject GetPrefab(MaterialDefinition mat)
        {
            foreach (var v in Variants) if (v.Material == mat) return v.Prefab;
            return null;
        }
        public GameObject GetAnyPrefab()
        {
            foreach (var v in Variants) if (v.Prefab) return v.Prefab;
            return null;
        }
        public virtual DestroyFailReason CanBeDestroyed(PieceKey key, BuildGrid grid)
        => DestroyFailReason.None;
        public GameObject GetGhostPrefab(MaterialDefinition mat)
        {
            foreach (var v in Variants) if (v.Material == mat && v.GhostPrefab) return v.GhostPrefab;
            foreach (var v in Variants) if (v.GhostPrefab) return v.GhostPrefab;
            return GetPrefab(mat);
        }

        // ---- правила (переопределяются) ----
        public virtual PlacementFailReason CheckPlacement(PieceKey key, BuildGrid grid)
        => ValidatePlacement(key, grid)
            ? PlacementFailReason.None
            : PlacementFailReason.WrongSurface;
        public abstract IEnumerable<Slot> GetSlots(PieceKey key);
        protected PlacementResult Invalid(
        PieceKey key, Vector3 pos, Quaternion rot, BuildGrid grid,
        MaterialDefinition mat, PlacementFailReason reason)
        {
            return new PlacementResult
            {
                Show = true,
                Valid = false,
                FailReason = reason,
                Key = key,
                Position = pos,
                Rotation = rot,
                TargetGrid = grid,
                Prefab = GetPrefab(mat),
                GhostPrefab = GetGhostPrefab(mat),
            };
        }
        protected PlacementResult FreeInvalid(
            PlacementContext ctx,
            MaterialDefinition mat,
            PlacementFailReason reason)
        {
            return new PlacementResult
            {
                Show = true,
                Valid = false,
                FailReason = reason,
                Key = new PieceKey(PieceId, Vector3Int.zero, 0),
                Position = ctx.Hit.point,                    // ← точка попадания, без сетки
                Rotation = Quaternion.Euler(0f, ctx.FirstYaw, 0f),
                TargetGrid = null,                             // ← отключает любой снап
                Prefab = GetPrefab(mat),
                GhostPrefab = GetGhostPrefab(mat),
            };
        }
        public abstract bool ValidatePlacement(PieceKey key, BuildGrid grid);

        public abstract PlacementResult Evaluate(PlacementContext ctx, MaterialDefinition mat);

        public abstract IEnumerable<MarkerSpot> GetMarkerSpots(BuildGrid grid);

        public virtual void OnScroll(int sign, BuildInputState state) { }
        public virtual IEnumerable<Vector2Int> GetOccupiedColumns(PieceKey key)
        {
            yield return new Vector2Int(key.Anchor.x, key.Anchor.z);
        }
        public virtual bool CanStartNewGrid => false;

        // Какие клетки могут опираться на ЭТОТ кусок (для полов)
        public virtual IEnumerable<Vector3Int> GetSupportedFloorCells(PieceKey key) { yield break; }
        public virtual PlacementResult FromMarker(MarkerSpot spot, MaterialDefinition mat, BuildInputState input)
        {
            bool ok = ValidatePlacement(spot.Key, spot.Grid);
            return new PlacementResult
            {
                Show = true,
                Valid = ok,
                FailReason = ok ? PlacementFailReason.None : PlacementFailReason.SlotOccupied,
                Key = spot.Key,
                Position = spot.Position,
                Rotation = spot.Rotation,
                TargetGrid = spot.Grid,
                Prefab = GetPrefab(mat),
                GhostPrefab = GetGhostPrefab(mat),
            };
        }
        public virtual Bounds GetLocalBounds(float cell, float level, float thickness)
        {
            return new Bounds(Vector3.zero, new Vector3(cell, level, cell));
        }
        public virtual IEnumerable<BuildPiece> GetDependents(PieceKey key, BuildGrid grid)
        {
            yield break;
        }
    }
}