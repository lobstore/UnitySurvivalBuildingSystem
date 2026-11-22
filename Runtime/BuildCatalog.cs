using System.Collections.Generic;
using UnityEngine;

namespace LOGIYGames.SurvivalBuilding
{
    /// <summary>Serializable catalog of buildable piece and material definitions.</summary>
    [CreateAssetMenu(menuName = "Building/Catalog")]
    public class BuildCatalog : ScriptableObject
    {
        public List<PieceDefinition> Pieces = new();
        public List<MaterialDefinition> Materials = new();

        /// <summary>Gets the piece assigned to a zero-based hotkey index.</summary>
        /// <param name="index">Zero-based index in <see cref="Pieces"/>.</param>
        /// <returns>The matching definition, or <see langword="null"/> for an invalid index.</returns>
        public PieceDefinition ByHotkey(int index)
            => Pieces != null && index >= 0 && index < Pieces.Count ? Pieces[index] : null;

        /// <summary>Gets the first configured material, or <see langword="null"/> when no material exists.</summary>
        public MaterialDefinition DefaultMaterial
            => Materials != null && Materials.Count > 0 ? Materials[0] : null;
    }
}
