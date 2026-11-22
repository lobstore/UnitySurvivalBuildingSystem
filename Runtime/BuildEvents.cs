using System;
using UnityEngine;

namespace LOGIYGames.SurvivalBuilding
{
    /// <summary>
    /// Central event hub for the survival building system.
    /// Keeps systems loosely coupled by publishing placement, selection and destruction events.
    /// </summary>
    [DisallowMultipleComponent]
    public class BuildEvents : MonoBehaviour
    {
        private static BuildEvents _instance;

        /// <summary>
        /// Gets the active event hub, creating one when the current scene has none.
        /// </summary>
        public static BuildEvents Instance
        {
            get
            {
                if (_instance != null)
                    return _instance;

                _instance = FindAnyObjectByType<BuildEvents>();
                if (_instance != null)
                    return _instance;

                var host = new GameObject(nameof(BuildEvents));
                _instance = host.AddComponent<BuildEvents>();
                return _instance;
            }
        }

        /// <summary>Raised when a placed piece loses support and becomes a falling fragment.</summary>
        public event Action<BuildPiece> Orphaned;

        /// <summary>Raised when the planner changes between build and destroy modes.</summary>
        public event Action<BuildMode> ModeChanged;

        /// <summary>Raised after a piece has been successfully placed.</summary>
        public event Action<PieceDefinition, MaterialDefinition, Vector3> Placed;

        /// <summary>Raised when a placement attempt is rejected.</summary>
        public event Action<PieceDefinition, MaterialDefinition, Vector3> PlacementRejected;

        /// <summary>Raised after a piece is removed from a building.</summary>
        public event Action<BuildPiece> Removed;

        /// <summary>Raised when the selected piece definition changes.</summary>
        public event Action<PieceDefinition> PieceSelected;

        /// <summary>Raised when the selected material definition changes.</summary>
        public event Action<MaterialDefinition> MaterialSelected;

        /// <summary>Raised when a piece's orientation changes.</summary>
        public event Action<PieceDefinition, int> OrientationChanged;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
        }

        /// <summary>Clears the static singleton reference when this hub is destroyed.</summary>
        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        /// <summary>Publishes that a piece has become an unsupported falling fragment.</summary>
        /// <param name="piece">The affected piece.</param>
        public void RaiseOrphaned(BuildPiece piece) => Orphaned?.Invoke(piece);

        /// <summary>Publishes a build-mode change.</summary>
        /// <param name="mode">The newly active mode.</param>
        public void RaiseModeChanged(BuildMode mode) => ModeChanged?.Invoke(mode);

        /// <summary>Publishes a successful placement.</summary>
        /// <param name="piece">The placed piece definition.</param>
        /// <param name="material">The material used for the piece.</param>
        /// <param name="position">The world-space placement position.</param>
        public void RaisePlaced(PieceDefinition piece, MaterialDefinition material, Vector3 position)
            => Placed?.Invoke(piece, material, position);

        /// <summary>Publishes a rejected placement attempt.</summary>
        /// <param name="piece">The requested piece definition.</param>
        /// <param name="material">The selected material.</param>
        /// <param name="position">The attempted world-space placement position.</param>
        public void RaiseRejected(PieceDefinition piece, MaterialDefinition material, Vector3 position)
            => PlacementRejected?.Invoke(piece, material, position);

        /// <summary>Publishes the removal of a placed piece.</summary>
        /// <param name="piece">The removed piece.</param>
        public void RaiseRemoved(BuildPiece piece) => Removed?.Invoke(piece);

        /// <summary>Publishes a piece selection change.</summary>
        /// <param name="piece">The newly selected piece definition.</param>
        public void RaisePieceSelected(PieceDefinition piece) => PieceSelected?.Invoke(piece);

        /// <summary>Publishes a material selection change.</summary>
        /// <param name="material">The newly selected material definition.</param>
        public void RaiseMaterialSelected(MaterialDefinition material) => MaterialSelected?.Invoke(material);

        /// <summary>Publishes a piece orientation change.</summary>
        /// <param name="piece">The affected piece definition.</param>
        /// <param name="rotationStep">The resulting quarter-turn rotation step.</param>
        public void RaiseOrientationChanged(PieceDefinition piece, int rotationStep)
            => OrientationChanged?.Invoke(piece, rotationStep);
    }
}
