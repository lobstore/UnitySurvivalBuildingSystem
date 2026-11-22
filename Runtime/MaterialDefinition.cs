using UnityEngine;

namespace LOGIYGames.SurvivalBuilding
{
    [CreateAssetMenu(menuName = "Building/Material")]
    public class MaterialDefinition : ScriptableObject
    {
        public string Id = "concrete";
        public Color UiColor = Color.gray;
    }
}