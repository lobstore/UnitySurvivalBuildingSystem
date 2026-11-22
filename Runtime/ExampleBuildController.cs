using UnityEngine;

namespace LOGIYGames.SurvivalBuilding
{
    public class ExampleBuildController : MonoBehaviour
    {
        [Header("Refs")]
        public Camera Cam;
        public BuildPlanner Planner;
        public BuildCatalog Catalog;
        [Header("Tuning")]
        public float MaxDistance = 12f;
        public LayerMask RaycastLayers = ~0;
        public bool BuildingEnabled => Planner.Enabled;
        void Update()
        {

            if (Input.GetKeyDown(KeyCode.B))
                Planner.SetEnabled(!Planner.Enabled);
            if (Cam == null || Planner == null) return;
            if (!Planner.Enabled)
            {
                // На случай, если флаг выключили напрямую — подстраховаться
                // (SetEnabled должен был скрыть ghost и маркеры сам)
                return;
            }
            // хоткей переключения режима
            if (Input.GetKeyDown(KeyCode.X)) Planner.ToggleMode();

            // выбор детали / материала — только в режиме Build
            if (Planner.Mode == BuildMode.Build)
            {
                if (Catalog)
                {
                    for (int i = 0; i < Catalog.Pieces.Count && i < 9; i++)
                        if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                            Planner.SetPiece(Catalog.Pieces[i]);
                }

                if (Input.GetKeyDown(KeyCode.Tab))
                    Planner.NextMaterial((Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) ? -1 : 1);

                float s = Input.mouseScrollDelta.y;
                if (Mathf.Abs(s) > 0.01f) Planner.StepOrientation(s > 0 ? 1 : -1);
            }

            // луч
            Vector3 centerPoint = new Vector3(Screen.width / 2f, Screen.height / 2f, 0f);
            var ray = Cam.ScreenPointToRay(centerPoint);
            if (!Physics.Raycast(ray, out var hit, MaxDistance, RaycastLayers))
            {
                Planner.ClearPreview();
                return;
            }

            Planner.Tick(hit, Cam.transform.position);

            if (Input.GetMouseButtonDown(0))
            {
                if (Planner.Mode == BuildMode.Destroy) Planner.TryDestroy();
                else Planner.TryCommit();
            }
        }
    }
}