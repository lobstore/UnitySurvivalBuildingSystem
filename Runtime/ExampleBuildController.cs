using UnityEngine;
using UnityEngine.SceneManagement;

namespace LOGIYGames.SurvivalBuilding
{
    /// <summary>
    /// Basic legacy-input sample controller for the building planner.
    /// Projects the screen center into the world and forwards user actions to the planner.
    /// </summary>
    public class ExampleBuildController : MonoBehaviour
    {
        [Header("Refs")]
        public Camera Cam;
        public BuildPlanner Planner;
        public BuildCatalog Catalog;

        [Header("Tuning")]
        [Min(0f)] public float MaxDistance = 12f;
        public LayerMask RaycastLayers = ~0;

        [Header("Placement markers")]
        [Tooltip("Small tolerance in world units that allows a marker trigger to be selected when it overlaps the supporting surface.")]
        [Min(0f)] public float MarkerSurfaceTolerance = 0.35f;

        // Marker prefabs use trigger colliders. Keep a reusable buffer to detect them even when
        // Physics.queriesHitTriggers is disabled globally, without allocating a new array each frame.
        private readonly RaycastHit[] _markerHitBuffer = new RaycastHit[128];

        /// <summary>Indicates whether the connected planner currently accepts building input.</summary>
        public bool BuildingEnabled => Planner != null && Planner.Enabled;

        private void Update()
        {
            if (Planner == null)
                return;

            if (Input.GetKeyDown(KeyCode.B))
                Planner.SetEnabled(!Planner.Enabled);

            if (Cam == null || !Planner.Enabled)
                return;

            if (Input.GetKeyDown(KeyCode.X))
                Planner.ToggleMode();

            if (Planner.Mode == BuildMode.Build)
            {
                HandleBuildInput();
            }

            var screenCenter = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
            var ray = Cam.ScreenPointToRay(screenCenter);

            if (!TryGetBuildHit(ray, out var hit))
            {
                Planner.ClearPreview();
                return;
            }

            Planner.Tick(hit, Cam.transform.position);

            if (Input.GetMouseButtonDown(0))
            {
                if (Planner.Mode == BuildMode.Destroy)
                    Planner.TryDestroy();
                else
                    Planner.TryCommit();
            }
        }


        /// <summary>
        /// Finds the nearest placement-marker trigger or solid world surface. Marker prefabs use
        /// trigger colliders, so they must be queried with <see cref="QueryTriggerInteraction.Collide"/>
        /// regardless of the project's global trigger-query setting.
        /// </summary>
        /// <param name="ray">Camera ray used to select a building target.</param>
        /// <param name="hit">Selected marker or world hit.</param>
        /// <returns>True when a marker or solid surface was hit within the configured range.</returns>
        private bool TryGetBuildHit(Ray ray, out RaycastHit hit)
        {
            hit = default;
            var maxDistance = Mathf.Max(0f, MaxDistance);
            if (maxDistance <= 0f)
                return false;

            var physicsScene = gameObject.scene.GetPhysicsScene();
            var hitCount = physicsScene.Raycast(
                ray.origin, ray.direction, _markerHitBuffer, maxDistance, ~0, QueryTriggerInteraction.Collide);

            var hasMarkerHit = false;
            var nearestMarkerDistance = float.MaxValue;
            var nearestMarkerHit = default(RaycastHit);

            for (var index = 0; index < hitCount; index++)
            {
                var candidate = _markerHitBuffer[index];
                if (candidate.collider == null
                    || candidate.collider.GetComponentInParent<BuildMarker>() == null
                    || candidate.distance >= nearestMarkerDistance)
                {
                    continue;
                }

                hasMarkerHit = true;
                nearestMarkerDistance = candidate.distance;
                nearestMarkerHit = candidate;
            }

            var hasWorldHit = physicsScene.Raycast(
                ray.origin, ray.direction, out var worldHit, maxDistance, RaycastLayers, QueryTriggerInteraction.Ignore);

            // Marker trigger volumes may slightly overlap the floor/wall that supports them.
            // Prefer the marker in that case, but do not select one hidden behind a nearer solid.
            if (hasMarkerHit
                && (!hasWorldHit || nearestMarkerDistance <= worldHit.distance + Mathf.Max(0f, MarkerSurfaceTolerance)))
            {
                hit = nearestMarkerHit;
                return true;
            }

            if (hasWorldHit)
            {
                hit = worldHit;
                return true;
            }

            return false;
        }

        /// <summary>Processes piece, material and orientation shortcuts while in build mode.</summary>
        private void HandleBuildInput()
        {
            if (Catalog != null && Catalog.Pieces != null)
            {
                var hotkeyCount = Mathf.Min(Catalog.Pieces.Count, 9);
                for (var index = 0; index < hotkeyCount; index++)
                {
                    var key = (KeyCode)((int)KeyCode.Alpha1 + index);
                    if (Input.GetKeyDown(key))
                        Planner.SetPiece(Catalog.Pieces[index]);
                }
            }

            if (Input.GetKeyDown(KeyCode.Tab))
            {
                var backwards = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                Planner.NextMaterial(backwards ? -1 : 1);
            }

            var scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.01f)
                Planner.StepOrientation(scroll > 0f ? 1 : -1);
        }
    }
}
