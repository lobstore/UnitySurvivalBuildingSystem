using System.Collections.Generic;
using UnityEngine;
namespace LOGIYGames.SurvivalBuilding
{
    public enum BuildMode { Build, Destroy }

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
        public BuildMessages Messages;
        [Header("State")]
        public PieceDefinition CurrentPiece;
        public MaterialDefinition CurrentMaterial;
        BuildMarker hiddenMarker;
        public event System.Action<BuildPiece> DestroyTargetChanged;
        public BuildMode Mode { get; private set; } = BuildMode.Build;
        public bool Enabled { get; private set; } = true;
        public event System.Action<bool> EnabledChanged;
        BuildPiece destroyTarget;

        [Header("Tuning")]
        public float FirstRotStep = 90f;
        public float GridAdoptionRadius = 3f;
        public LayerMask BlockerLayers = 0;
        public float FloorThickness = 0.1f;
        public float WallThickness = 0.1f;
        [Header("Cross-grid rules")]
        [Tooltip("Запрещать ставить в XZ-колонку другой сетки (на любой высоте)")]
        public bool PreventColumnOverlap = true;

        [Tooltip("Сколько этажей НАД верхней частью чужой сетки разрешено строить в её колонке")]
        [Min(0)] public int AllowedLevelsAboveOtherGrid = 0;

        [Tooltip("Сколько этажей ПОД нижней частью чужой сетки разрешено строить в её колонке")]
        [Min(0)] public int AllowedLevelsBelowOtherGrid = 0;
        public readonly BuildInputState Input = new();

        public PlacementResult LastPlacement { get; private set; }
        public BuildGrid LastTargetGrid { get; private set; }

        void Start()
        {
            if (CurrentPiece == null && Catalog) CurrentPiece = Catalog.ByHotkey(0);
            if (CurrentMaterial == null && Catalog) CurrentMaterial = Catalog.DefaultMaterial;

            if (Service) Service.OnChanged += RefreshMarkers;
            RefreshMarkers();
        }
        public void SetEnabled(bool isEnabled)
        {
            if (Enabled == isEnabled) return;
            Enabled = isEnabled;

            if (!isEnabled)
            {
                destroyTarget = null;
                Ghost.RestoreHidden();
                Ghost.Hide();
                if (Hint) Hint.Hide();
                Markers.Clear();
                LastPlacement = default;
                LastTargetGrid = null;
                ForceSetMode(BuildMode.Build);       // ← не проверяет Enabled
            }
            else
            {
                RefreshMarkers();
            }

            EnabledChanged?.Invoke(isEnabled);
        }
        void ForceSetMode(BuildMode mode)
        {
            if (Mode == mode) return;
            Mode = mode;
            Events?.RaiseModeChanged(mode);
        }
        // =========================================================
        // Основной вход: «есть точка в мире, есть луч, оцени»
        public PlacementResult Tick(RaycastHit hit, Vector3 observerPosition)
        {
            if (!Enabled) { Ghost.Hide(); return default; }
            if (Mode == BuildMode.Destroy) { TickDestroy(hit); return default; }

            var hitPiece = hit.collider ? hit.collider.GetComponentInParent<BuildPiece>() : null;
            var hitMarker = hit.collider ? hit.collider.GetComponentInParent<BuildMarker>() : null;

            BuildGrid targetGrid =
                hitPiece?.Grid ??
                hitMarker?.Spot.Grid ??
                Service.FindNearest(hit.point, GridAdoptionRadius);

            LastTargetGrid = targetGrid;

            PlacementResult p;

            if (hitMarker != null && hitMarker.Spot.Piece == CurrentPiece)
            {
                p = CurrentPiece.FromMarker(hitMarker.Spot, CurrentMaterial, Input);
            }
            else
            {
                var ctx = new PlacementContext
                {
                    Hit = hit,
                    HitPiece = hitPiece,
                    HitMarker = hitMarker,
                    Grid = targetGrid,
                    ObserverPosition = observerPosition,
                    Material = CurrentMaterial,
                    Input = Input,
                    FirstYaw = Input.FirstYawAccum,
                    FloorThickness = FloorThickness,
                };
                p = CurrentPiece.Evaluate(ctx, CurrentMaterial);
            }

            // ── BuildFallback больше нет ──
            ValidateAgainstWorld(ref p, targetGrid);

            if (!p.Show) { Ghost.Hide(); Markers.RestoreAll(); if (Hint) Hint.Hide(); return p; }

            Ghost.Show(p);
            Markers.SetVisibleNear(p.Position, Service.Settings.Cell * 0.5f);

            if (Hint)
            {
                if (!p.Valid && p.FailReason != PlacementFailReason.None) Hint.Show(p, CurrentPiece);
                else Hint.Hide();
            }

            LastPlacement = p;
            return p;
        }

        void TickDestroy(RaycastHit hit)
        {
            var buildedPiece = hit.collider ? hit.collider.GetComponentInParent<BuildPiece>() : null;

            if (buildedPiece == null || !buildedPiece.IsInGrid)
            {
                destroyTarget = null;
                Ghost.Hide();
                if (Hint) Hint.Hide();
                return;
            }

            destroyTarget = buildedPiece;
            var reason = buildedPiece.Definition.CanBeDestroyed(buildedPiece.Key, buildedPiece.Grid);
            bool canDestroy = reason == DestroyFailReason.None;

            var gprefab = buildedPiece.Definition.GetGhostPrefab(buildedPiece.Material);
            var prefab = buildedPiece.Definition.GetPrefab(buildedPiece.Material);

            Ghost.ShowDestroy(buildedPiece, canDestroy);

            Ghost.ShowDestroy(buildedPiece, canDestroy);

            // fallback if no messages for destroy
            if (Hint)
            {
                if (!canDestroy) Hint.ShowMessage(Messages ? Messages.ForDestroy(reason) : "Нельзя удалить");
                else Hint.Hide();
            }
        }

        public bool TryDestroy()
        {
            if (!Enabled) return false;
            if (Mode != BuildMode.Destroy) return false;
            if (destroyTarget == null) return false;

            if (destroyTarget.Definition.CanBeDestroyed(destroyTarget.Key, destroyTarget.Grid) != DestroyFailReason.None)
                return false;

            var target = destroyTarget;
            destroyTarget = null;

            Ghost.RestoreHidden();
            Service.Remove(target);          // ← событие Removed поднимает сервис
            Ghost.Hide();
            return true;
        }
        void HideMarker(BuildMarker m)
        {
            if (m == hiddenMarker) return;
            RestoreMarker();
            if (m != null) { m.SetVisible(false); hiddenMarker = m; }
        }

        void RestoreMarker()
        {
            if (hiddenMarker != null) hiddenMarker.SetVisible(true);
            hiddenMarker = null;
        }
        public void SetMode(BuildMode mode)
        {
            if (!Enabled || Mode == mode) return;

            destroyTarget = null;
            Ghost.Hide();
            if (Hint) Hint.Hide();

            ForceSetMode(mode);
            RefreshMarkers();
        }
        public void ToggleMode()
        => SetMode(Mode == BuildMode.Build ? BuildMode.Destroy : BuildMode.Build);
        // Не попали ни во что — скрыть всё
        public void ClearPreview()
        {
            if (!Enabled) return;
            destroyTarget = null;
            Ghost.Hide();
            if (Hint) Hint.Hide();
            Markers.RestoreAll();          // ← ключевой фикс для «с маркера на воздух»
            LastPlacement = default;
            LastTargetGrid = null;
        }

        // =========================================================
        // Действия, которые вызывает любой источник ввода
        public void StepOrientation(int dir)
        {
            if (!Enabled || dir == 0) return;

            if (LastTargetGrid == null)
            {
                // Нет сетки — крутим угол «первой плиты / fallback»
                // для любого типа, а не только для пола
                Input.FirstYawAccum += dir * FirstRotStep;
            }
            else
            {
                // Сетка есть — деталь крутит себя по своим правилам
                // (стена 180°, лестница/пол 90°)
                CurrentPiece.OnScroll(dir, Input);
            }
            RefreshMarkers();
        }
        void RefreshMarkers()
        {
            var piece = (Enabled && Mode == BuildMode.Build) ? CurrentPiece : null;
            Markers.Refresh(piece, Service.Grids);
        }
        public bool TryCommit()
        {
            if (!Enabled) return false;

            var p = LastPlacement;
            if (p.Prefab == null) return false;

            if (!p.Valid)
            {
                Events?.RaiseRejected(CurrentPiece, CurrentMaterial, p.Position);
                if (Hint && p.FailReason != PlacementFailReason.None)
                    Hint.Show(p, CurrentPiece);
                return false;
            }

            Service.Commit(p, CurrentPiece, CurrentMaterial);   // событие поднимет сервис
            if (Hint) Hint.Hide();
            if (LastTargetGrid == null) Input.FirstYawAccum = 0f;
            return true;
        }


        public void SetPiece(PieceDefinition next)
        {
            if (!Enabled || next == null || next == CurrentPiece) return;
            CurrentPiece = next;
            Events?.RaisePieceSelected(next);
            RefreshMarkers();                 // было Markers.Refresh(CurrentPiece, ...)
        }

        public void SetMaterial(MaterialDefinition next)
        {
            if (!Enabled || next == null || next == CurrentMaterial) return;
            CurrentMaterial = next;
            Events?.RaiseMaterialSelected(next);
            RefreshMarkers();                 // было Markers.Refresh(CurrentPiece, ...)
        }
        /// <summary>
        /// Shift current material from catalog in cycle 
        /// </summary>
        /// <param name="dir"></param>
        public void NextMaterial(int dir)
        {
            if (!Catalog || Catalog.Materials.Count == 0) return;
            int idx = Catalog.Materials.IndexOf(CurrentMaterial);
            if (idx < 0) idx = 0;
            idx = (idx + dir + Catalog.Materials.Count) % Catalog.Materials.Count;
            SetMaterial(Catalog.Materials[idx]);
        }
        int SideFromWorld(BuildGrid grid, Vector3 worldDir)
        {
            Vector3 l = Quaternion.Inverse(grid.Rotation) * worldDir;
            l.y = 0f;
            if (Mathf.Abs(l.x) > Mathf.Abs(l.z)) return l.x > 0 ? 1 : 3;
            return l.z > 0 ? 0 : 2;
        }
        // =========================================================

        void ValidateAgainstWorld(ref PlacementResult p, BuildGrid ownGrid)
        {
            if (!p.Show || !p.Valid || p.Prefab == null) return;

            var def = FindPieceById(p.Key.PieceId);
            if (def == null) return;

            float cell = Service.Settings.Cell > 0 ? Service.Settings.Cell : 2f;
            float level = Service.Settings.Level > 0 ? Service.Settings.Level : 2f;

            // ==========================================================
            // 1) Физическое пересечение с коллайдерами / BlockerLayers
            // ==========================================================
            var b = def.GetLocalBounds(cell, level, WallThickness);
            Vector3 center = p.Position + p.Rotation * b.center;

            var hits = Physics.OverlapBox(center, b.extents, p.Rotation, ~0,
                                          QueryTriggerInteraction.Ignore);

            foreach (var c in hits)
            {
                var bp = c.GetComponentInParent<BuildPiece>();
                if (bp != null)
                {
                    if (bp.Grid == null) continue;           // обломок — не препятствие
                    if (bp.Grid == ownGrid) continue;         // своя сетка — не пересечение
                    p.Valid = false;
                    p.FailReason = PlacementFailReason.CrossesOtherGrid;
                    return;
                }

                if (c.GetComponentInParent<BuildMarker>() != null) continue;

                if ((BlockerLayers.value & (1 << c.gameObject.layer)) != 0)
                {
                    p.Valid = false;
                    p.FailReason = PlacementFailReason.BlockedByWorld;
                    return;
                }
            }

            // ==========================================================
            // 2) Логическая проверка XZ-колонки
            // ==========================================================
            if (!PreventColumnOverlap) return;

            // Все XZ-колонки, которые займёт новая деталь (в мировых координатах)
            List<Vector3> worldCols = new();
            if (ownGrid != null)
            {
                foreach (var xz in def.GetOccupiedColumns(p.Key))
                {
                    var cell3 = new Vector3Int(xz.x, p.Key.Anchor.y, xz.y);
                    worldCols.Add(ownGrid.CellCenter(cell3));
                }
            }
            else
            {
                // Первая плита новой сетки — одна колонка в p.Position
                worldCols.Add(p.Position);
            }

            foreach (var other in Service.Grids)
            {
                if (other == null || other == ownGrid) continue;
                if (other.Columns.Count == 0) continue;

                foreach (var world in worldCols)
                {
                    var oc = other.WorldToCell(world);
                    var col = new Vector2Int(oc.x, oc.z);

                    if (!other.Columns.Contains(col)) continue;

                    // Колонка занята чужой сеткой. Проверяем Y-запас.
                    // aboveAllowed: деталь ВЫШЕ разрешённого «буфера» над сеткой
                    // belowAllowed: деталь НИЖЕ разрешённого «буфера» под сеткой
                    bool aboveAllowed = AllowedLevelsAboveOtherGrid > 0
                        && oc.y > other.MaxCell.y + (AllowedLevelsAboveOtherGrid - 1);

                    bool belowAllowed = AllowedLevelsBelowOtherGrid > 0
                        && oc.y < other.MinCell.y - (AllowedLevelsBelowOtherGrid - 1);

                    if (aboveAllowed || belowAllowed) continue;

                    p.Valid = false;
                    p.FailReason = PlacementFailReason.CrossesOtherGrid;
                    return;
                }
            }
        }

        PieceDefinition FindPieceById(int id)
        {
            if (!Catalog) return null;
            foreach (var p in Catalog.Pieces) if (p && p.PieceId == id) return p;
            return null;
        }
    }
}