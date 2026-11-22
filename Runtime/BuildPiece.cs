using LOGIYGames.Extensions;
using UnityEngine;

namespace LOGIYGames.SurvivalBuilding
{

    public class BuildPiece : MonoBehaviour
    {
        public PieceKey Key { get; private set; }
        public BuildGrid Grid { get; private set; }
        public PieceDefinition Definition { get; private set; }
        public MaterialDefinition Material { get; private set; }
        public bool IsInGrid => Grid != null;

        // ---- falling state ----
        Rigidbody rb;

        void Awake()
        {
            rb = gameObject.GetOrAddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        public void Init(PieceKey k, BuildGrid g, PieceDefinition d, MaterialDefinition m)
        {

            Key = k; Grid = g; Definition = d; Material = m;
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }
        }

        public void BeginFalling()
        {
            Grid = null;              // отвязка от сетки — с этого момента это обломок
            enabled = true;

            if (rb == null) rb = gameObject.AddComponent<Rigidbody>();   // fallback
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.mass = 1f;
            rb.linearVelocity = Vector3.zero;
        }

    }
}