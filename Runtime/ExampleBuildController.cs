using UnityEngine;

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

            if (!Physics.Raycast(ray, out var hit, MaxDistance, RaycastLayers, QueryTriggerInteraction.Ignore))
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
