using LOGIYGames.Audio;
using UnityEngine;

namespace LOGIYGames.SurvivalBuilding
{
    /// <summary>
    /// Plays configured sound effects in response to building-system events.
    /// Subscriptions are paired in OnEnable/OnDisable to avoid stale handlers.
    /// </summary>
    public class BuildAudioFeedback : MonoBehaviour
    {
        [Header("Placement")]
        public SoundData PlaceFloor;
        public SoundData PlaceWall;
        public SoundData PlaceStair;
        public SoundData PlaceDoorway;
        public SoundData PlacePillar;

        [Header("Errors")]
        public SoundData Rejected;
        public SoundData SlotOccupied;
        public SoundData NoSupport;

        [Header("UI")]
        public SoundData SelectPiece;
        public SoundData SelectMaterial;
        public SoundData Rotate;

        [Header("Destroy")]
        public SoundData EnterDestroyMode;
        public SoundData ExitDestroyMode;
        public SoundData DestroyHover;
        public SoundData Destroyed;

        private BuildEvents _subscribedEvents;

        private Vector3 SelfPosition => transform.position;

        /// <summary>Starts listening for building-system events.</summary>
        private void OnEnable()
        {
            Subscribe(BuildEvents.Instance);
        }

        /// <summary>Stops listening and releases the cached event hub reference.</summary>
        private void OnDisable()
        {
            Subscribe(null);
        }

        /// <summary>
        /// Attaches this component to the supplied event hub and removes any previous subscriptions.
        /// </summary>
        /// <param name="events">The event hub to subscribe to, or <see langword="null"/> to unsubscribe.</param>
        private void Subscribe(BuildEvents events)
        {
            if (_subscribedEvents == events)
                return;

            if (_subscribedEvents != null)
            {
                _subscribedEvents.Placed -= OnPlaced;
                _subscribedEvents.PlacementRejected -= OnRejected;
                _subscribedEvents.PieceSelected -= OnPieceSelected;
                _subscribedEvents.MaterialSelected -= OnMaterialSelected;
                _subscribedEvents.OrientationChanged -= OnOrientationChanged;
                _subscribedEvents.ModeChanged -= OnModeChanged;
                _subscribedEvents.Removed -= OnRemoved;
            }

            _subscribedEvents = events;

            if (_subscribedEvents == null)
                return;

            _subscribedEvents.Placed += OnPlaced;
            _subscribedEvents.PlacementRejected += OnRejected;
            _subscribedEvents.PieceSelected += OnPieceSelected;
            _subscribedEvents.MaterialSelected += OnMaterialSelected;
            _subscribedEvents.OrientationChanged += OnOrientationChanged;
            _subscribedEvents.ModeChanged += OnModeChanged;
            _subscribedEvents.Removed += OnRemoved;
        }

        /// <summary>Plays the sound associated with the concrete type of the placed piece.</summary>
        /// <param name="definition">The placed piece definition.</param>
        /// <param name="material">The selected material (reserved for material-specific feedback).</param>
        /// <param name="position">The placement position.</param>
        private void OnPlaced(PieceDefinition definition, MaterialDefinition material, Vector3 position)
        {
            if (definition == null)
                return;

            var clip = definition switch
            {
                DoorwayPiece => PlaceDoorway != null ? PlaceDoorway : PlaceWall,
                PillarPiece => PlacePillar != null ? PlacePillar : PlaceFloor,
                WallPiece => PlaceWall,
                StairPiece => PlaceStair,
                _ => PlaceFloor,
            };

            Play(clip, position);
        }

        /// <summary>Plays feedback when the selected piece changes.</summary>
        /// <param name="piece">The newly selected piece.</param>
        private void OnPieceSelected(PieceDefinition piece) => Play(SelectPiece, SelfPosition);

        /// <summary>Plays feedback when the selected material changes.</summary>
        /// <param name="material">The newly selected material.</param>
        private void OnMaterialSelected(MaterialDefinition material) => Play(SelectMaterial, SelfPosition);

        /// <summary>Plays feedback when a piece's orientation changes.</summary>
        /// <param name="piece">The rotated piece definition.</param>
        /// <param name="rotationStep">The resulting rotation step.</param>
        private void OnOrientationChanged(PieceDefinition piece, int rotationStep) => Play(Rotate, SelfPosition);

        /// <summary>Plays the sound associated with entering or leaving destroy mode.</summary>
        /// <param name="mode">The newly active building mode.</param>
        private void OnModeChanged(BuildMode mode)
            => Play(mode == BuildMode.Destroy ? EnterDestroyMode : ExitDestroyMode, SelfPosition);

        /// <summary>Plays the removal sound at the removed piece's last world position.</summary>
        /// <param name="piece">The removed piece.</param>
        private void OnRemoved(BuildPiece piece)
        {
            if (piece != null)
                Play(Destroyed, piece.transform.position);
        }

        /// <summary>Plays rejection feedback at the attempted placement position.</summary>
        /// <param name="piece">The requested piece definition.</param>
        /// <param name="material">The selected material.</param>
        /// <param name="position">The attempted placement position.</param>
        private void OnRejected(PieceDefinition piece, MaterialDefinition material, Vector3 position)
            => Play(Rejected, position);

        /// <summary>Creates and plays the supplied sound at a world-space position.</summary>
        /// <param name="data">The sound configuration.</param>
        /// <param name="position">The playback position.</param>
        private static void Play(SoundData data, Vector3 position)
        {
            if (data == null || !data.clip)
                return;

            var manager = SoundManager.Instance;
            if (manager == null)
                return;

            manager.CreateSoundBuilder().WithPosition(position).Play(data);
        }
    }
}
