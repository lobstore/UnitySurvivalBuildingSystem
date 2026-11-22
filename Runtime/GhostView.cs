using UnityEngine;

namespace LOGIYGames.SurvivalBuilding
{
    public class GhostView : MonoBehaviour
    {
        [Header("Tint")]
        public bool TintGhost = true;
        public Color ValidColor = new Color(0.3f, 0.85f, 1f, 0.35f);
        public Color InvalidColor = new Color(1f, 0.25f, 0.35f, 0.35f);
        public bool UsePropertyBlock = true;

        [Header("Destroy")]
        public Color DestroyTargetColor = new Color(1f, 0.6f, 0.2f, 0.5f);

        GameObject ghost;
        GameObject ghostPrefab;
        BuildPiece hiddenOriginal;

        bool lastValid;
        bool hasLastValid;

        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        // =========================================================
        // Build-ghost
        // =========================================================
        public void Show(PlacementResult p)
        {
            if (!p.Show || p.GhostPrefab == null) { Hide(); return; }

            bool samePrefabAndPlace =
                ghost != null
                && ghostPrefab == p.GhostPrefab
                && Vector3.SqrMagnitude(ghost.transform.position - p.Position) < 0.0001f
                && Quaternion.Angle(ghost.transform.rotation, p.Rotation) < 0.1f;

            if (samePrefabAndPlace)
            {
                RestoreHidden();   // no-op если ничего не скрывали
                if (TintGhost && (!hasLastValid || lastValid != p.Valid))
                    ApplyTint(p.Valid ? ValidColor : InvalidColor);
                lastValid = p.Valid;
                hasLastValid = true;
                return;
            }

            RestoreHidden();
            DestroyGhost();

            ghost = Instantiate(p.GhostPrefab);
            ghostPrefab = p.GhostPrefab;
            foreach (var c in ghost.GetComponentsInChildren<Collider>()) c.enabled = false;
            ghost.transform.SetPositionAndRotation(p.Position, p.Rotation);

            if (TintGhost) ApplyTint(p.Valid ? ValidColor : InvalidColor);
            lastValid = p.Valid;
            hasLastValid = true;
        }

        // =========================================================
        // Destroy-ghost (логика та же, кэш не нужен — смена цели редкая)
        // =========================================================
        public void ShowDestroy(BuildPiece piece, bool canDestroy)
        {
            if (piece == null) { Hide(); return; }
            if (hiddenOriginal == piece && ghost != null)
            {
                if (TintGhost) ApplyTint(canDestroy ? DestroyTargetColor : InvalidColor);
                return;
            }

            RestoreHidden();
            DestroyGhost();

            var prefab = piece.Definition.GetGhostPrefab(piece.Material);
            if (prefab == null) { Hide(); return; }

            ghost = Instantiate(prefab);
            ghostPrefab = prefab;
            foreach (var c in ghost.GetComponentsInChildren<Collider>()) c.enabled = false;
            ghost.transform.SetPositionAndRotation(piece.transform.position, piece.transform.rotation);

            hiddenOriginal = piece;
            SetRenderersEnabled(piece, false);

            if (TintGhost) ApplyTint(canDestroy ? DestroyTargetColor : InvalidColor);

            hasLastValid = false;   // сбрасываем кэш валидности — режим другой
        }

        // =========================================================
        // State
        // =========================================================
        public void Hide()
        {
            RestoreHidden();
            DestroyGhost();
            hasLastValid = false;
        }

        public void RestoreHidden()
        {
            if (hiddenOriginal != null)
            {
                SetRenderersEnabled(hiddenOriginal, true);
                hiddenOriginal = null;
            }
        }

        void DestroyGhost()
        {
            if (ghost) Destroy(ghost);
            ghost = null;
            ghostPrefab = null;
        }

        static void SetRenderersEnabled(Component c, bool enabled)
        {
            foreach (var r in c.GetComponentsInChildren<Renderer>()) r.enabled = enabled;
        }

        void ApplyTint(Color color)
        {
            if (ghost == null) return;

            if (UsePropertyBlock)
            {
                var block = new MaterialPropertyBlock();
                foreach (var r in ghost.GetComponentsInChildren<Renderer>())
                {
                    r.GetPropertyBlock(block);
                    block.SetColor(ColorId, color);
                    block.SetColor(BaseColorId, color);
                    r.SetPropertyBlock(block);
                }
            }
            else
            {
                foreach (var r in ghost.GetComponentsInChildren<Renderer>())
                {
                    var mat = r.material;
                    if (mat.HasProperty(ColorId)) mat.SetColor(ColorId, color);
                    if (mat.HasProperty(BaseColorId)) mat.SetColor(BaseColorId, color);
                }
            }
        }
    }
}