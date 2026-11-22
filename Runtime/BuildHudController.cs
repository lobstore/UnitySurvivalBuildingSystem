using UnityEngine;

namespace LOGIYGames.SurvivalBuilding
{
    /// <summary>Keeps the build panel visibility synchronized with the planner enabled state.</summary>
    public class BuildHudController : MonoBehaviour
    {
        public GameObject BuildPanel;
        public BuildPlanner Planner;

        private void OnEnable()
        {
            if (Planner != null)
            {
                Planner.EnabledChanged += OnEnabledChanged;
                OnEnabledChanged(Planner.Enabled);
            }
        }

        /// <summary>Removes the planner event subscription.</summary>
        private void OnDisable()
        {
            if (Planner != null)
                Planner.EnabledChanged -= OnEnabledChanged;
        }

        /// <summary>Updates the build panel active state.</summary>
        /// <param name="isEnabled">Whether the building planner is enabled.</param>
        private void OnEnabledChanged(bool isEnabled)
        {
            if (BuildPanel != null)
                BuildPanel.SetActive(isEnabled);
        }
    }
}
