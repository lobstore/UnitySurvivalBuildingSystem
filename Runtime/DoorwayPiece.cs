using UnityEngine;

namespace LOGIYGames.SurvivalBuilding
{
    /// <summary>
    /// Wall-derived construction piece that can use a doorway-specific prefab and placement marker.
    /// Inherits edge occupancy, support and orientation rules from <see cref="WallPiece"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Building/Doorway")]
    public class DoorwayPiece : WallPiece
    {
    }
}
