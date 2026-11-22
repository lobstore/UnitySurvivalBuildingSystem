using System.Collections.Generic;
using UnityEngine;

namespace LOGIYGames.SurvivalBuilding
{
    [CreateAssetMenu(menuName = "Building/Catalog")]
    public class BuildCatalog : ScriptableObject
    {
        public List<PieceDefinition> Pieces = new();
        public List<MaterialDefinition> Materials = new();

        public PieceDefinition ByHotkey(int index) =>
            index >= 0 && index < Pieces.Count ? Pieces[index] : null;

        public MaterialDefinition DefaultMaterial =>
            Materials.Count > 0 ? Materials[0] : null;
    }
}