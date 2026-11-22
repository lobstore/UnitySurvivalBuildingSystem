using System;
using System.Collections.Generic;
using UnityEngine;

namespace LOGIYGames.SurvivalBuilding
{
    /// <summary>
    /// Coordinates the placement preview, player selection and destruction workflow.
    /// Input devices should call this class rather than manipulating grids directly.
    /// </summary>
    [RequireComponent(typeof(BuildService))]
    [RequireComponent(typeof(GhostView))]
    [RequireComponent(typeof(MarkerManager))]
    [RequireComponent(typeof(BuildEvents))]
    public class BuildPlanner : MonoBehaviour
    {
        [Header("Refs")]
        public BuildService Service;
        public GhostView Ghost;
        public MarkerManager Markers;
        public BuildCatalog Catalog;
        public BuildHintView Hint;
        public BuildEvents Events;
        /// <summary>Localized placement and destruction messages displayed by the hint view.</summary>
        public BuildMessages Messages;

        [Header("State")]
        public PieceDefinition CurrentPiece;
        public MaterialDefinition CurrentMaterial;

        /// <summary>Raised whenever the current destroy target changes.</summary>
        public event Action<BuildPiece> DestroyTargetChanged;

        /// <summary>Gets the currently active planner mode.</summary>
        public BuildMode Mode { get; private set; } = BuildMode.Build;

        /// <summary>Gets whether building input and previews are currently enabled.</summary>
        public bool Enabled { get; private set; } = true;

        /// <summary>Raised whenever the enabled state changes.</summary>
        public event Action<bool> EnabledChanged;

        [Header("Tuning")]
        [Min(0f)] public float FirstRotStep = 90f;
        [Min(0f)] public float GridAdoptionRadius = 3f;
        public LayerMask BlockerLayers = 0;
        [Min(0f)] public float FloorThickness = 0.1f;
        [Min(0f)] public float WallThickness = 0.1f;

        [Header("Cross-grid rules")]
        [Tooltip("Prevent building in an XZ column occupied by another grid.")]
        public bool PreventColumnOverlap = true;

        [Tooltip("Number of levels allowed above the top of another grid.")]
        [Min(0)] public int AllowedLevelsAboveOtherGrid = 0;

        [Tooltip("Number of levels allowed below the bottom of another grid.")]
        [Min(0)] public int AllowedLevelsBelowOtherGrid = 0;

        /// <summary>Per-piece rotation and first-grid orientation state.</summary>
        public readonly BuildInputState Input = new();

        /// <summary>Gets the most recent evaluated placement result.</summary>
        public PlacementResult LastPlacement { get; private set; }

        /// <summary>Gets the grid currently targeted by the placement preview.</summary>
        public BuildGrid LastTargetGrid { get; private set; }

        private BuildPiece _destroyTarget;

        private void Reset()
        {
            ResolveReferences();
        }

        /// <summary>Resolves required component references before the planner starts.</summary>
        private void Awake()
        {
            ResolveReferences();
        }

        /// <summary>Initializes default selections and subscribes to grid changes.</summary>
        private void Start()
        {
            if (CurrentPiece == null && Catalog != null)
                CurrentPiece = Catalog.ByHotkey(0);

            if (CurrentMaterial == null && Catalog != null)
                CurrentMaterial = Catalog.DefaultMaterial;

            if (Service != null)
                Service.OnChanged += RefreshMarkers;

            RefreshMarkers();
        }

        /// <summary>Removes the grid change subscription when the planner is destroyed.</summary>
        private void OnDestroy()
        {
            if (Service != null)
                Service.OnChanged -= RefreshMarkers;
        }

        /// <summary>
        /// Enables or disables the planner and clears transient preview state when disabled.
        /// </summary>
        /// <param name="isEnabled">New enabled state.</param>
        public void SetEnabled(bool isEnabled)
        {
            if (Enabled == isEnabled)
                return;

            Enabled = isEnabled;

            if (!isEnabled)
            {
                SetDestroyTarget(null);
                Ghost?.RestoreHidden();
                Ghost?.Hide();
                Hint?.Hide();
                Markers?.Clear();
                LastPlacement = default;
                LastTargetGrid = null;
                ForceSetMode(BuildMode.Build);
            }
            else
            {
                RefreshMarkers();
            }

            EnabledChanged?.Invoke(isEnabled);
        }

        /// <summary>
        /// Evaluates the current ray hit and updates the placement or destruction preview.
        /// </summary>
        /// <param name="hit">Raycast result for the current cursor or crosshair position.</param>
        /// <param name="observerPosition">World-space position of the camera or observing actor.</param>
        /// <returns>The latest placement result, or a default result in destroy/disabled mode.</returns>
        public PlacementResult Tick(RaycastHit hit, Vector3 observerPosition)
        {
            if (!Enabled)
            {
                Ghost?.Hide();
                return default;
            }

            if (Mode == BuildMode.Destroy)
            {
                TickDestroy(hit);
                return default;
            }

            if (CurrentPiece == null || Service == null || Ghost == null)
            {
                LastPlacement = default;
                LastTargetGrid = null;
                Ghost?.Hide();
                return default;
            }

            var hitPiece = hit.collider != null
                ? hit.collider.GetComponentInParent<BuildPiece>()
                : null;
            var hitMarker = hit.collider != null
                ? hit.collider.GetComponentInParent<BuildMarker>()
                : null;

            var targetGrid = hitPiece?.Grid
                ?? hitMarker?.Spot.Grid
                ?? Service.FindNearest(hit.point, GridAdoptionRadius);

            LastTargetGrid = targetGrid;

            PlacementResult placement;
            if (hitMarker != null && hitMarker.Spot.Piece == CurrentPiece)
            {
                placement = CurrentPiece.FromMarker(hitMarker.Spot, CurrentMaterial, Input);
            }
            else
            {
                var context = new PlacementContext
                {
                    Hit = hit,
                    HitPiece = hitPiece,
                    HitMarker = hitMarker,
                    Grid = targetGrid,
                    ObserverPosition = observerPosition,
                    Material = CurrentMaterial,
                    Input = Input,
                    FirstYaw = Input.FirstYawAccum,
                    FloorThickness = FloorThickness
                };

                placement = CurrentPiece.Evaluate(context, CurrentMaterial);
            }

            ValidateAgainstWorld(ref placement, targetGrid);

            if (!placement.Show)
            {
                Ghost.Hide();
                Markers?.RestoreAll();
                Hint?.Hide();
                LastPlacement = placement;
                return placement;
            }

            Ghost.Show(placement);
            Markers?.SetVisibleNear(placement.Position, (Service.Settings != null ? Service.Settings.Cell : 2f) * 0.5f);

            if (!placement.Valid && placement.FailReason != PlacementFailReason.None)
                Hint?.Show(placement, CurrentPiece);
            else
                Hint?.Hide();

            LastPlacement = placement;
            return placement;
        }

        /// <summary>Updates the selected piece and destroy preview for the current ray hit.</summary>
        /// <param name="hit">Current physics raycast result.</param>
        private void TickDestroy(RaycastHit hit)
        {
            var piece = hit.collider != null
                ? hit.collider.GetComponentInParent<BuildPiece>()
                : null;

            if (piece == null || !piece.IsInGrid || piece.Definition == null)
            {
                SetDestroyTarget(null);
                Ghost?.Hide();
                Hint?.Hide();
                return;
            }

            SetDestroyTarget(piece);
            var reason = piece.Definition.CanBeDestroyed(piece.Key, piece.Grid);
            var canDestroy = reason == DestroyFailReason.None;

            Ghost?.ShowDestroy(piece, canDestroy);

            if (Hint == null)
                return;

            if (!canDestroy)
                Hint.ShowMessage(Messages != null ? Messages.ForDestroy(reason) : "Cannot remove this piece.");
            else
                Hint.Hide();
        }

        /// <summary>Attempts to remove the currently highlighted piece in destroy mode.</summary>
        /// <returns><see langword="true"/> when the piece was removed.</returns>
        public bool TryDestroy()
        {
            if (!Enabled || Mode != BuildMode.Destroy || _destroyTarget == null
                || !_destroyTarget.IsInGrid || Service == null)
                return false;

            if (_destroyTarget.Definition == null
                || _destroyTarget.Definition.CanBeDestroyed(_destroyTarget.Key, _destroyTarget.Grid) != DestroyFailReason.None)
                return false;

            var target = _destroyTarget;
            SetDestroyTarget(null);

            Ghost?.RestoreHidden();
            Service.Remove(target);
            Ghost?.Hide();
            return true;
        }

        /// <summary>Changes the active planner mode.</summary>
        /// <param name="mode">Mode to activate.</param>
        public void SetMode(BuildMode mode)
        {
            if (!Enabled || Mode == mode)
                return;

            SetDestroyTarget(null);
            LastPlacement = default;
            Ghost?.Hide();
            Hint?.Hide();

            ForceSetMode(mode);
            RefreshMarkers();
        }

        /// <summary>Toggles between build mode and destroy mode.</summary>
        public void ToggleMode()
            => SetMode(Mode == BuildMode.Build ? BuildMode.Destroy : BuildMode.Build);

        /// <summary>Clears the active preview and restores all placement markers.</summary>
        public void ClearPreview()
        {
            if (!Enabled)
                return;

            SetDestroyTarget(null);
            Ghost?.Hide();
            Hint?.Hide();
            Markers?.RestoreAll();
            LastPlacement = default;
            LastTargetGrid = null;
        }

        /// <summary>
        /// Rotates the current definition or changes the initial yaw when no grid is targeted.
        /// </summary>
        /// <param name="direction">Positive or negative rotation direction; zero is ignored.</param>
        public void StepOrientation(int direction)
        {
            if (!Enabled || direction == 0 || CurrentPiece == null)
                return;

            if (LastTargetGrid == null)
                Input.FirstYawAccum += direction * FirstRotStep;
            else
                CurrentPiece.OnScroll(direction, Input);

            LastPlacement = default;
            Events?.RaiseOrientationChanged(CurrentPiece, Input.GetRot(CurrentPiece));
            RefreshMarkers();
        }

        /// <summary>Rebuilds placement markers for the selected piece and currently managed grids.</summary>
        public void RefreshMarkers()
        {
            if (Markers == null || Service == null)
                return;

            var definition = Enabled && Mode == BuildMode.Build ? CurrentPiece : null;
            Markers.Refresh(definition, Service.Grids);
        }

        /// <summary>
        /// Commits the most recently evaluated placement if it is valid.
        /// </summary>
        /// <returns><see langword="true"/> when a piece was instantiated successfully.</returns>
        public bool TryCommit()
        {
            if (!Enabled || Mode != BuildMode.Build || Service == null
                || CurrentPiece == null || CurrentMaterial == null)
                return false;

            var placement = LastPlacement;
            if (placement.Prefab == null)
            {
                if (placement.FailReason == PlacementFailReason.NoMaterialVariant)
                    Hint?.ShowMessage(Messages != null
                        ? Messages.For(PlacementFailReason.NoMaterialVariant)
                        : "No prefab is configured for this material.");
                return false;
            }

            if (!placement.Valid)
            {
                Events?.RaiseRejected(CurrentPiece, CurrentMaterial, placement.Position);
                if (placement.FailReason != PlacementFailReason.None)
                    Hint?.Show(placement, CurrentPiece);
                return false;
            }

            Service.Commit(placement, CurrentPiece, CurrentMaterial);
            Hint?.Hide();

            if (placement.TargetGrid == null)
                Input.FirstYawAccum = 0f;

            LastPlacement = default;
            return true;
        }

        /// <summary>Selects a new piece definition.</summary>
        /// <param name="next">Definition to select.</param>
        public void SetPiece(PieceDefinition next)
        {
            if (!Enabled || next == null || next == CurrentPiece)
                return;

            CurrentPiece = next;
            LastPlacement = default;
            Events?.RaisePieceSelected(next);
            RefreshMarkers();
        }

        /// <summary>Selects a new material definition.</summary>
        /// <param name="next">Material to select.</param>
        public void SetMaterial(MaterialDefinition next)
        {
            if (!Enabled || next == null || next == CurrentMaterial)
                return;

            CurrentMaterial = next;
            LastPlacement = default;
            Events?.RaiseMaterialSelected(next);
            RefreshMarkers();
        }

        /// <summary>Moves the selected material through the catalog, wrapping at either end.</summary>
        /// <param name="direction">Positive to move forward, negative to move backward.</param>
        public void NextMaterial(int direction)
        {
            if (!Enabled || Catalog == null || Catalog.Materials == null || Catalog.Materials.Count == 0)
                return;

            var index = Catalog.Materials.IndexOf(CurrentMaterial);
            if (index < 0)
                index = 0;

            var count = Catalog.Materials.Count;
            index = ((index + direction) % count + count) % count;
            SetMaterial(Catalog.Materials[index]);
        }

        /// <summary>Validates a placement against colliders and the occupied columns of other grids.</summary>
        /// <param name="placement">Placement result to validate and update.</param>
        /// <param name="ownGrid">The grid the placement belongs to, if any.</param>
        private void ValidateAgainstWorld(ref PlacementResult placement, BuildGrid ownGrid)
        {
            if (!placement.Show || !placement.Valid || placement.Prefab == null || CurrentPiece == null || Service == null)
                return;

            var cell = Mathf.Max(Service.Settings != null ? Service.Settings.Cell : 2f, 0.25f);
            var level = Mathf.Max(Service.Settings != null ? Service.Settings.Level : 2f, 0.25f);

            var bounds = CurrentPiece.GetLocalBounds(cell, level, WallThickness);
            var center = placement.Position + placement.Rotation * bounds.center;
            var hits = Physics.OverlapBox(center, bounds.extents, placement.Rotation, ~0,
                QueryTriggerInteraction.Ignore);

            foreach (var collider in hits)
            {
                var existingPiece = collider.GetComponentInParent<BuildPiece>();
                if (existingPiece != null)
                {
                    if (existingPiece.Grid == null || existingPiece.Grid == ownGrid)
                        continue;

                    placement.Valid = false;
                    placement.FailReason = PlacementFailReason.CrossesOtherGrid;
                    return;
                }

                if (collider.GetComponentInParent<BuildMarker>() != null)
                    continue;

                if ((BlockerLayers.value & (1 << collider.gameObject.layer)) != 0)
                {
                    placement.Valid = false;
                    placement.FailReason = PlacementFailReason.BlockedByWorld;
                    return;
                }
            }

            if (!PreventColumnOverlap)
                return;

            var worldColumns = new List<Vector3>();
            if (ownGrid != null)
            {
                foreach (var column in CurrentPiece.GetOccupiedColumns(placement.Key))
                {
                    var cell3 = new Vector3Int(column.x, placement.Key.Anchor.y, column.y);
                    worldColumns.Add(ownGrid.CellCenter(cell3));
                }
            }
            else
            {
                worldColumns.Add(placement.Position);
            }

            foreach (var otherGrid in Service.Grids)
            {
                if (otherGrid == null || otherGrid == ownGrid || otherGrid.Columns.Count == 0)
                    continue;

                foreach (var worldPosition in worldColumns)
                {
                    var otherCell = otherGrid.WorldToCell(worldPosition);
                    var column = new Vector2Int(otherCell.x, otherCell.z);
                    if (!otherGrid.Columns.Contains(column))
                        continue;

                    var aboveAllowed = AllowedLevelsAboveOtherGrid > 0
                        && otherCell.y > otherGrid.MaxCell.y + (AllowedLevelsAboveOtherGrid - 1);
                    var belowAllowed = AllowedLevelsBelowOtherGrid > 0
                        && otherCell.y < otherGrid.MinCell.y - (AllowedLevelsBelowOtherGrid - 1);

                    if (aboveAllowed || belowAllowed)
                        continue;

                    placement.Valid = false;
                    placement.FailReason = PlacementFailReason.CrossesOtherGrid;
                    return;
                }
            }
        }

        /// <summary>Changes the mode without applying the public enabled-state guard.</summary>
        /// <param name="mode">Mode to activate.</param>
        private void ForceSetMode(BuildMode mode)
        {
            if (Mode == mode)
                return;

            Mode = mode;
            Events?.RaiseModeChanged(mode);
        }

        /// <summary>Updates the current destroy target and notifies listeners only when it changes.</summary>
        /// <param name="target">New target or <see langword="null"/>.</param>
        private void SetDestroyTarget(BuildPiece target)
        {
            if (_destroyTarget == target)
                return;

            _destroyTarget = target;
            DestroyTargetChanged?.Invoke(target);
        }

        /// <summary>Resolves required components from this GameObject when inspector references are empty.</summary>
        private void ResolveReferences()
        {
            Service ??= GetComponent<BuildService>();
            Ghost ??= GetComponent<GhostView>();
            Markers ??= GetComponent<MarkerManager>();
            Events ??= GetComponent<BuildEvents>();
        }
    }
}
