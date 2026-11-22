using LOGIYGames.Extensions;
using UnityEngine;

namespace LOGIYGames.SurvivalBuilding
{
    /// <summary>
    /// Runtime instance of a placed construction piece.
    /// Stores its logical grid identity and switches to rigidbody simulation when detached.
    /// </summary>
    [DisallowMultipleComponent]
    public class BuildPiece : MonoBehaviour
    {
        /// <summary>Gets the logical key assigned to this piece in its building grid.</summary>
        public PieceKey Key { get; private set; }

        /// <summary>Gets the owning grid, or <see langword="null"/> for a falling fragment.</summary>
        public BuildGrid Grid { get; private set; }

        /// <summary>Gets the definition that controls placement and support rules for this piece.</summary>
        public PieceDefinition Definition { get; private set; }

        /// <summary>Gets the material definition used to instantiate this piece.</summary>
        public MaterialDefinition Material { get; private set; }

        /// <summary>Indicates whether this object is currently registered in a building grid.</summary>
        public bool IsInGrid => Grid != null;

        private Rigidbody _rigidbody;

        private void Awake()
        {
            _rigidbody = gameObject.GetOrAddComponent<Rigidbody>();
            ConfigureAsPlacedPiece();
        }

        /// <summary>
        /// Assigns the logical identity and ownership data to an instantiated piece.
        /// </summary>
        /// <param name="key">Unique logical key inside the target grid.</param>
        /// <param name="grid">Grid that owns the piece.</param>
        /// <param name="definition">Definition used to create the piece.</param>
        /// <param name="material">Material variant used to create the piece.</param>
        public void Init(PieceKey key, BuildGrid grid, PieceDefinition definition, MaterialDefinition material)
        {
            if (grid == null) throw new System.ArgumentNullException(nameof(grid));
            if (definition == null) throw new System.ArgumentNullException(nameof(definition));
            if (material == null) throw new System.ArgumentNullException(nameof(material));

            Key = key;
            Grid = grid;
            Definition = definition;
            Material = material;

            _rigidbody ??= GetComponent<Rigidbody>();
            ConfigureAsPlacedPiece();
        }

        /// <summary>
        /// Detaches the piece from its logical grid without destroying its GameObject.
        /// This is used by the service before destruction or physics conversion.
        /// </summary>
        internal void DetachFromGrid()
        {
            Grid = null;
        }

        /// <summary>Detaches the piece and enables its falling physics simulation.</summary>
        public void BeginFalling()
        {
            DetachFromGrid();
            enabled = true;

            _rigidbody ??= gameObject.GetOrAddComponent<Rigidbody>();
            _rigidbody.isKinematic = false;
            _rigidbody.useGravity = true;
            _rigidbody.mass = 1f;
            _rigidbody.linearVelocity = Vector3.zero;
        }

        /// <summary>Applies the rigidbody configuration expected for a piece registered in a grid.</summary>
        private void ConfigureAsPlacedPiece()
        {
            if (_rigidbody == null)
                return;

            _rigidbody.isKinematic = true;
            _rigidbody.useGravity = false;
        }
    }
}
