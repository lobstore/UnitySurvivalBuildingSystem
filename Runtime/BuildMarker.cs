using UnityEngine;

namespace LOGIYGames.SurvivalBuilding
{
    /// <summary>
    /// Scene marker representing a candidate placement location for one piece definition.
    /// </summary>
    public class BuildMarker : MonoBehaviour
    {
        /// <summary>Gets or sets the placement information represented by this marker.</summary>
        public MarkerSpot Spot;

        private Renderer[] _renderers;

        private void Awake()
        {
            CacheRenderers();
        }

        /// <summary>Enables or disables all renderers belonging to this marker and its children.</summary>
        /// <param name="visible">Whether the marker should be rendered.</param>
        public void SetVisible(bool visible)
        {
            if (_renderers == null)
                CacheRenderers();

            foreach (var renderer in _renderers)
            {
                if (renderer != null)
                    renderer.enabled = visible;
            }
        }

        /// <summary>Refreshes the cached renderer list, including inactive child objects.</summary>
        private void CacheRenderers()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
        }
    }
}
