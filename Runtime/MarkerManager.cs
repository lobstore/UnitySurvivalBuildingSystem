using LOGIYGames.Extensions;
using System.Collections.Generic;
using UnityEngine;

namespace LOGIYGames.SurvivalBuilding
{
    /// <summary>Creates and manages candidate placement markers for all active building grids.</summary>
    public class MarkerManager : MonoBehaviour
    {
        /// <summary>Controls the default visibility of all generated markers.</summary>
        public bool VisibleMarkers;

        private readonly List<BuildMarker> _markers = new();

        /// <summary>
        /// Regenerates candidate markers for the selected definition across the supplied grids.
        /// </summary>
        /// <param name="current">Definition whose candidate positions should be shown.</param>
        /// <param name="grids">Building grids to query.</param>
        public void Refresh(PieceDefinition current, IReadOnlyList<BuildGrid> grids)
        {
            Clear();

            if (current == null || current.MarkerPrefab == null || grids == null)
                return;

            foreach (var grid in grids)
            {
                if (grid == null)
                    continue;

                foreach (var spot in current.GetMarkerSpots(grid))
                {
                    var markerObject = Instantiate(current.MarkerPrefab, spot.Position, spot.Rotation, grid.transform);
                    var marker = markerObject.GetOrAddComponent<BuildMarker>();
                    marker.Spot = spot;
                    marker.SetVisible(VisibleMarkers);
                    _markers.Add(marker);
                }
            }
        }

        /// <summary>Hides markers within a radius around the active ghost preview.</summary>
        /// <param name="position">World-space center of the preview.</param>
        /// <param name="radius">World-space exclusion radius.</param>
        public void SetVisibleNear(Vector3 position, float radius)
        {
            if (!VisibleMarkers)
                return;

            var radiusSquared = Mathf.Max(0f, radius) * Mathf.Max(0f, radius);
            foreach (var marker in _markers)
            {
                if (marker == null)
                    continue;

                var isNear = (marker.transform.position - position).sqrMagnitude < radiusSquared;
                marker.SetVisible(!isNear);
            }
        }

        /// <summary>Restores the configured default visibility for all generated markers.</summary>
        public void RestoreAll()
        {
            foreach (var marker in _markers)
            {
                if (marker != null)
                    marker.SetVisible(VisibleMarkers);
            }
        }

        /// <summary>Destroys all generated marker objects and clears the internal cache.</summary>
        public void Clear()
        {
            foreach (var marker in _markers)
            {
                if (marker != null)
                    Destroy(marker.gameObject);
            }

            _markers.Clear();
        }
    }
}
