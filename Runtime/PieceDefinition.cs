using System.Collections.Generic;
using UnityEngine;

namespace LOGIYGames.SurvivalBuilding
{
    /// <summary>
    /// Base ScriptableObject for all buildable piece types.
    /// Defines prefab variants, placement evaluation, occupancy and support relationships.
    /// </summary>
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

        /// <summary>Gets the prefab associated with the supplied material.</summary>
        /// <param name="material">Material variant to resolve.</param>
        /// <returns>Matching prefab, or <see langword="null"/> if it is not configured.</returns>
        public GameObject GetPrefab(MaterialDefinition material)
        {
            if (material == null || Variants == null)
                return null;

            foreach (var variant in Variants)
            {
                if (variant != null && variant.Material == material)
                    return variant.Prefab;
            }

            return null;
        }

        /// <summary>Gets the first non-null prefab in the variant list.</summary>
        /// <returns>A configured prefab, or <see langword="null"/> when none exists.</returns>
        public GameObject GetAnyPrefab()
        {
            if (Variants == null)
                return null;

            foreach (var variant in Variants)
            {
                if (variant != null && variant.Prefab != null)
                    return variant.Prefab;
            }

            return null;
        }

        /// <summary>Returns the default destruction restriction for this piece type.</summary>
        /// <param name="key">Key of the piece being checked.</param>
        /// <param name="grid">Grid that owns the piece.</param>
        public virtual DestroyFailReason CanBeDestroyed(PieceKey key, BuildGrid grid)
            => DestroyFailReason.None;

        /// <summary>
        /// Gets a ghost prefab for the selected material. When a material-specific ghost is absent,
        /// falls back to another configured ghost and then to the regular material prefab.
        /// </summary>
        /// <param name="material">Selected material.</param>
        /// <returns>Ghost or regular prefab, or <see langword="null"/> when no variant exists.</returns>
        public GameObject GetGhostPrefab(MaterialDefinition material)
        {
            if (Variants == null)
                return null;

            foreach (var variant in Variants)
            {
                if (variant != null && variant.Material == material && variant.GhostPrefab != null)
                    return variant.GhostPrefab;
            }

            foreach (var variant in Variants)
            {
                if (variant != null && variant.GhostPrefab != null)
                    return variant.GhostPrefab;
            }

            return GetPrefab(material);
        }

        /// <summary>Returns a placement failure reason for the supplied key and grid.</summary>
        /// <param name="key">Candidate logical key.</param>
        /// <param name="grid">Target grid.</param>
        /// <returns>None when the definition accepts the candidate.</returns>
        public virtual PlacementFailReason CheckPlacement(PieceKey key, BuildGrid grid)
            => ValidatePlacement(key, grid)
                ? PlacementFailReason.None
                : PlacementFailReason.WrongSurface;

        /// <summary>Enumerates every slot reserved by a piece using the supplied key.</summary>
        /// <param name="key">Candidate or registered piece key.</param>
        public abstract IEnumerable<Slot> GetSlots(PieceKey key);

        /// <summary>Builds a visible invalid placement result at a snapped grid position.</summary>
        /// <param name="key">Candidate piece key.</param>
        /// <param name="position">World-space position for the preview.</param>
        /// <param name="rotation">World-space rotation for the preview.</param>
        /// <param name="grid">Target grid.</param>
        /// <param name="material">Selected material.</param>
        /// <param name="reason">Reason why placement is invalid.</param>
        protected PlacementResult Invalid(
            PieceKey key, Vector3 position, Quaternion rotation, BuildGrid grid,
            MaterialDefinition material, PlacementFailReason reason)
        {
            return new PlacementResult
            {
                Show = true,
                Valid = false,
                FailReason = reason,
                Key = key,
                Position = position,
                Rotation = rotation,
                TargetGrid = grid,
                Prefab = GetPrefab(material),
                GhostPrefab = GetGhostPrefab(material)
            };
        }

        /// <summary>Builds a visible invalid result at the raycast point without snapping to a grid.</summary>
        /// <param name="context">Current placement evaluation context.</param>
        /// <param name="material">Selected material.</param>
        /// <param name="reason">Reason why placement is invalid.</param>
        protected PlacementResult FreeInvalid(
            PlacementContext context, MaterialDefinition material, PlacementFailReason reason)
        {
            return new PlacementResult
            {
                Show = true,
                Valid = false,
                FailReason = reason,
                Key = new PieceKey(PieceId, Vector3Int.zero, 0),
                Position = context.Hit.point,
                Rotation = Quaternion.Euler(0f, context.FirstYaw, 0f),
                TargetGrid = null,
                Prefab = GetPrefab(material),
                GhostPrefab = GetGhostPrefab(material)
            };
        }

        /// <summary>Checks whether a candidate piece can be placed at the specified grid key.</summary>
        /// <param name="key">Candidate logical key.</param>
        /// <param name="grid">Target grid.</param>
        public abstract bool ValidatePlacement(PieceKey key, BuildGrid grid);

        /// <summary>Evaluates the current ray hit and returns preview data for this piece type.</summary>
        /// <param name="context">Raycast, grid, input and observer context.</param>
        /// <param name="material">Selected material.</param>
        public abstract PlacementResult Evaluate(PlacementContext context, MaterialDefinition material);

        /// <summary>Enumerates placement marker positions supported by this definition on a grid.</summary>
        /// <param name="grid">Grid to query.</param>
        public abstract IEnumerable<MarkerSpot> GetMarkerSpots(BuildGrid grid);

        /// <summary>Applies a scroll-based orientation change; derived types can implement their own steps.</summary>
        /// <param name="sign">Positive or negative direction.</param>
        /// <param name="state">Mutable input rotation state.</param>
        public virtual void OnScroll(int sign, BuildInputState state) { }

        /// <summary>Enumerates horizontal grid columns occupied by the piece.</summary>
        /// <param name="key">Piece key.</param>
        public virtual IEnumerable<Vector2Int> GetOccupiedColumns(PieceKey key)
        {
            yield return new Vector2Int(key.Anchor.x, key.Anchor.z);
        }

        /// <summary>Indicates whether this placed piece provides a root support anchor for its grid.</summary>
        /// <param name="key">Logical key of the piece.</param>
        /// <param name="grid">Grid that owns the piece.</param>
        public virtual bool IsSupportAnchor(PieceKey key, BuildGrid grid) => false;

        /// <summary>Indicates whether this piece type can start a new grid.</summary>
        public virtual bool CanStartNewGrid => false;

        /// <summary>Enumerates floor cells that may receive support from this piece.</summary>
        /// <param name="key">Piece key.</param>
        public virtual IEnumerable<Vector3Int> GetSupportedFloorCells(PieceKey key)
        {
            yield break;
        }

        /// <summary>Creates a placement result from a marker candidate.</summary>
        /// <param name="spot">Marker candidate.</param>
        /// <param name="material">Selected material.</param>
        /// <param name="input">Current input state.</param>
        public virtual PlacementResult FromMarker(MarkerSpot spot, MaterialDefinition material, BuildInputState input)
        {
            var reason = CheckPlacement(spot.Key, spot.Grid);
            return new PlacementResult
            {
                Show = true,
                Valid = reason == PlacementFailReason.None,
                FailReason = reason,
                Key = spot.Key,
                Position = spot.Position,
                Rotation = spot.Rotation,
                TargetGrid = spot.Grid,
                Prefab = GetPrefab(material),
                GhostPrefab = GetGhostPrefab(material)
            };
        }

        /// <summary>Gets local-space bounds used for physics overlap validation.</summary>
        /// <param name="cell">Horizontal grid cell size.</param>
        /// <param name="level">Vertical grid level size.</param>
        /// <param name="thickness">Configured piece thickness.</param>
        public virtual Bounds GetLocalBounds(float cell, float level, float thickness)
            => new Bounds(Vector3.zero, new Vector3(cell, level, cell));

        /// <summary>Enumerates pieces whose support depends on this piece.</summary>
        /// <param name="key">Key of this supporting piece.</param>
        /// <param name="grid">Grid containing the support relationship.</param>
        public virtual IEnumerable<BuildPiece> GetDependents(PieceKey key, BuildGrid grid)
        {
            yield break;
        }
    }
}
