using UnityEngine;

namespace LOGIYGames.SurvivalBuilding
{
    /// <summary>ScriptableObject describing a selectable construction material.</summary>
    [CreateAssetMenu(menuName = "Building/Material")]
    public class MaterialDefinition : ScriptableObject
    {
        /// <summary>Stable identifier suitable for save data and external configuration.</summary>
        public string Id = "concrete";

        /// <summary>Color used by UI elements to represent this material.</summary>
        public Color UiColor = Color.gray;
    }
}
