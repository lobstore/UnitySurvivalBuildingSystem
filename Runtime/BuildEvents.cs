using System;
using UnityEngine;
namespace LOGIYGames.SurvivalBuilding
{
    public class BuildEvents : MonoBehaviour
    {
        static BuildEvents _instance;
        public static BuildEvents Instance
        {
            get
            {
                if (_instance == null)
                {
                    // найти на сцене, даже если Awake ещё не прошёл
                    _instance = FindAnyObjectByType<BuildEvents>();
                    if (_instance == null)
                    {
                        var go = new GameObject(nameof(BuildEvents));
                        _instance = go.AddComponent<BuildEvents>();
                    }
                }
                return _instance;
            }
        }

        void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            _instance = this;
        }
        public event System.Action<BuildPiece> Orphaned;
        public void RaiseOrphaned(BuildPiece p) => Orphaned?.Invoke(p);
        public event Action<BuildMode> ModeChanged;
        public void RaiseModeChanged(BuildMode m) => ModeChanged?.Invoke(m);
        // Установка
        public event Action<PieceDefinition, MaterialDefinition, Vector3> Placed;
        public event Action<PieceDefinition, MaterialDefinition, Vector3> PlacementRejected;   // + причина
        public event Action<BuildPiece> Removed;

        // Выбор
        public event Action<PieceDefinition> PieceSelected;
        public event Action<MaterialDefinition> MaterialSelected;
        public event Action<PieceDefinition, int> OrientationChanged;

        // API для планировщика
        public void RaisePlaced(PieceDefinition p, MaterialDefinition m, Vector3 pos) => Placed?.Invoke(p, m, pos);
        public void RaiseRejected(PieceDefinition p, MaterialDefinition m, Vector3 pos) => PlacementRejected?.Invoke(p, m, pos);
        public void RaiseRemoved(BuildPiece piece) => Removed?.Invoke(piece);
        public void RaisePieceSelected(PieceDefinition p) => PieceSelected?.Invoke(p);
        public void RaiseMaterialSelected(MaterialDefinition m) => MaterialSelected?.Invoke(m);
        public void RaiseOrientationChanged(PieceDefinition p, int rot) => OrientationChanged?.Invoke(p, rot);
    }
}