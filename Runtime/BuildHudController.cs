using UnityEngine;

namespace LOGIYGames.SurvivalBuilding
{
    public class BuildHudController : MonoBehaviour
    {
        public GameObject BuildPanel;
        public BuildPlanner Planner;

        void OnEnable() => Planner.EnabledChanged += OnEnabledChanged;
        void OnDisable() => Planner.EnabledChanged -= OnEnabledChanged;

        void Start() => OnEnabledChanged(Planner.Enabled);

        void OnEnabledChanged(bool isEnabled) => BuildPanel.SetActive(isEnabled);
    }
}