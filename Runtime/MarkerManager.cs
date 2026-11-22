using LOGIYGames.Extensions;
using System.Collections.Generic;
using UnityEngine;

namespace LOGIYGames.SurvivalBuilding
{
    public class MarkerManager : MonoBehaviour
    {
        public bool VisibleMarkers;
        readonly List<BuildMarker> markers = new();

        public void Refresh(PieceDefinition current, IReadOnlyList<BuildGrid> grids)
        {
            Clear();
            if (current == null || current.MarkerPrefab == null) return;

            foreach (var grid in grids)
            {
                if (grid == null) continue;
                foreach (var spot in current.GetMarkerSpots(grid))
                {
                    var go = Instantiate(current.MarkerPrefab, spot.Position, spot.Rotation, grid.transform);
                    var bm = go.GetOrAddComponent<BuildMarker>();
                    bm.Spot = spot;
                    bm.SetVisible(VisibleMarkers);
                    markers.Add(bm);
                }
            }
        }

        // Скрывает маркеры в радиусе от позиции (ghost перекрыл их)
        public void SetVisibleNear(Vector3 position, float radius)
        {
            if (!VisibleMarkers) return;
            float r2 = radius * radius;
            foreach (var m in markers)
            {
                if (!m) continue;
                bool near = (m.transform.position - position).sqrMagnitude < r2;
                m.SetVisible(!near);
            }
        }

        // Восстанавливает всем маркерам «дефолтную» видимость
        public void RestoreAll()
        {
            foreach (var m in markers)
                if (m) m.SetVisible(VisibleMarkers);
        }

        public void Clear()
        {
            foreach (var m in markers) if (m) Destroy(m.gameObject);
            markers.Clear();
        }
    }
}