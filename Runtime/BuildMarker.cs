using UnityEngine;

namespace LOGIYGames.SurvivalBuilding
{
    public class BuildMarker : MonoBehaviour
    {
        public MarkerSpot Spot;

        Renderer[] renderers;

        void Awake() => renderers = GetComponentsInChildren<Renderer>(true);

        public void SetVisible(bool visible)
        {
            if (renderers == null || renderers.Length == 0)
                renderers = GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers) r.enabled = visible;
        }
    }
}