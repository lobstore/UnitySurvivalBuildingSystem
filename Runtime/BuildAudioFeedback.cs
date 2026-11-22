using LOGIYGames.Audio;
using UnityEngine;
namespace LOGIYGames.SurvivalBuilding
{
    public class BuildAudioFeedback : MonoBehaviour
    {
        [Header("Placement")]
        public SoundData PlaceFloor;
        public SoundData PlaceWall;
        public SoundData PlaceStair;
        public SoundData PlaceDoorway;

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

        Vector3 SelfPosition => transform.position;
        BuildEvents Events => BuildEvents.Instance;

        void Start()
        {
            var e = Events;
            if (!e) return;

            e.Placed += OnPlaced;
            e.PlacementRejected += OnRejected;
            e.PieceSelected += OnPieceSelected;
            e.MaterialSelected += OnMaterialSelected;
            e.OrientationChanged += OnOrientationChanged;
            e.ModeChanged += OnModeChanged;
            e.Removed += OnRemoved;
        }

        void OnDisable()
        {
            var e = Events;
            if (!e) return;

            e.Placed -= OnPlaced;
            e.PlacementRejected -= OnRejected;
            e.PieceSelected -= OnPieceSelected;
            e.MaterialSelected -= OnMaterialSelected;
            e.OrientationChanged -= OnOrientationChanged;
            e.ModeChanged -= OnModeChanged;
            e.Removed -= OnRemoved;
        }

        void OnPlaced(PieceDefinition def, MaterialDefinition mat, Vector3 pos)
        {
            var clip = def switch
            {
                DoorwayPiece => PlaceDoorway != null ? PlaceDoorway : PlaceWall,
                WallPiece => PlaceWall,
                StairPiece => PlaceStair,
                _ => PlaceFloor,
            };
            Play(clip, pos);
        }

        void OnPieceSelected(PieceDefinition _) => Play(SelectPiece, SelfPosition);
        void OnMaterialSelected(MaterialDefinition _) => Play(SelectMaterial, SelfPosition);
        void OnOrientationChanged(PieceDefinition _, int __) => Play(Rotate, SelfPosition);
        void OnModeChanged(BuildMode m) =>
            Play(m == BuildMode.Destroy ? EnterDestroyMode : ExitDestroyMode, SelfPosition);
        void OnRemoved(BuildPiece piece) => Play(Destroyed, piece.transform.position);
        void OnRejected(PieceDefinition _, MaterialDefinition __, Vector3 pos) => Play(Rejected, pos);

        void Play(SoundData data, Vector3 pos)
        {
            if (data != null && data.clip)
                SoundManager.Instance.CreateSoundBuilder().WithPosition(pos).Play(data);
        }
    }
}