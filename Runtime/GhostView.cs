using UnityEngine;

namespace LOGIYGames.SurvivalBuilding
{
    /// <summary>
    /// Manages the placement ghost and temporary destroy highlight.
    /// The original piece renderer is disabled only while its destroy ghost is visible.
    /// </summary>
    public class GhostView : MonoBehaviour
    {
        [Header("Tint")]
        public bool TintGhost = true;
        public Color ValidColor = new(0.3f, 0.85f, 1f, 0.35f);
        public Color InvalidColor = new(1f, 0.25f, 0.35f, 0.35f);
        public bool UsePropertyBlock = true;

        [Header("Destroy")]
        public Color DestroyTargetColor = new(1f, 0.6f, 0.2f, 0.5f);

        private GameObject _ghost;
        private GameObject _ghostPrefab;
        private BuildPiece _hiddenOriginal;
        private bool _lastValid;
        private bool _hasLastValid;
        private MaterialPropertyBlock _propertyBlock;

        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        /// <summary>Displays or updates the placement ghost for the supplied result.</summary>
        /// <param name="placement">Placement preview result.</param>
        public void Show(PlacementResult placement)
        {
            if (!placement.Show || placement.GhostPrefab == null)
            {
                Hide();
                return;
            }

            var samePreview = _ghost != null
                && _ghostPrefab == placement.GhostPrefab
                && Vector3.SqrMagnitude(_ghost.transform.position - placement.Position) < 0.0001f
                && Quaternion.Angle(_ghost.transform.rotation, placement.Rotation) < 0.1f;

            if (samePreview)
            {
                RestoreHidden();
                if (TintGhost && (!_hasLastValid || _lastValid != placement.Valid))
                    ApplyTint(placement.Valid ? ValidColor : InvalidColor);

                _lastValid = placement.Valid;
                _hasLastValid = true;
                return;
            }

            RestoreHidden();
            DestroyGhost();

            _ghost = Instantiate(placement.GhostPrefab);
            _ghostPrefab = placement.GhostPrefab;
            DisableColliders(_ghost);
            _ghost.transform.SetPositionAndRotation(placement.Position, placement.Rotation);

            if (TintGhost)
                ApplyTint(placement.Valid ? ValidColor : InvalidColor);

            _lastValid = placement.Valid;
            _hasLastValid = true;
        }

        /// <summary>Displays a ghost over a piece being targeted in destroy mode.</summary>
        /// <param name="piece">Piece to highlight.</param>
        /// <param name="canDestroy">Whether the piece can currently be removed.</param>
        public void ShowDestroy(BuildPiece piece, bool canDestroy)
        {
            if (piece == null)
            {
                Hide();
                return;
            }

            if (_hiddenOriginal == piece && _ghost != null)
            {
                if (TintGhost)
                    ApplyTint(canDestroy ? DestroyTargetColor : InvalidColor);
                return;
            }

            RestoreHidden();
            DestroyGhost();

            var prefab = piece.Definition != null
                ? piece.Definition.GetGhostPrefab(piece.Material)
                : null;

            if (prefab == null)
            {
                Hide();
                return;
            }

            _ghost = Instantiate(prefab);
            _ghostPrefab = prefab;
            DisableColliders(_ghost);
            _ghost.transform.SetPositionAndRotation(piece.transform.position, piece.transform.rotation);

            _hiddenOriginal = piece;
            SetRenderersEnabled(piece, false);

            if (TintGhost)
                ApplyTint(canDestroy ? DestroyTargetColor : InvalidColor);

            _hasLastValid = false;
        }

        /// <summary>Restores hidden renderers, destroys the current ghost and clears preview cache state.</summary>
        public void Hide()
        {
            RestoreHidden();
            DestroyGhost();
            _hasLastValid = false;
        }

        /// <summary>Restores renderers on the original piece previously hidden for destroy preview.</summary>
        public void RestoreHidden()
        {
            if (_hiddenOriginal == null)
                return;

            SetRenderersEnabled(_hiddenOriginal, true);
            _hiddenOriginal = null;
        }

        /// <summary>Destroys the generated ghost GameObject and clears its prefab reference.</summary>
        private void DestroyGhost()
        {
            if (_ghost != null)
                Destroy(_ghost);

            _ghost = null;
            _ghostPrefab = null;
        }

        /// <summary>Disables all colliders in a generated ghost hierarchy.</summary>
        /// <param name="ghost">Ghost object to update.</param>
        private static void DisableColliders(GameObject ghost)
        {
            foreach (var collider in ghost.GetComponentsInChildren<Collider>(true))
                collider.enabled = false;
        }

        /// <summary>Enables or disables all renderers in a component's hierarchy.</summary>
        /// <param name="component">Root component.</param>
        /// <param name="enabled">Desired renderer state.</param>
        private static void SetRenderersEnabled(Component component, bool enabled)
        {
            foreach (var renderer in component.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer != null)
                    renderer.enabled = enabled;
            }
        }

        /// <summary>Applies the specified tint without creating per-renderer material instances when possible.</summary>
        /// <param name="color">Tint color.</param>
        private void ApplyTint(Color color)
        {
            if (_ghost == null)
                return;

            if (UsePropertyBlock)
            {
                _propertyBlock ??= new MaterialPropertyBlock();

                foreach (var renderer in _ghost.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer == null)
                        continue;

                    renderer.GetPropertyBlock(_propertyBlock);
                    _propertyBlock.SetColor(ColorId, color);
                    _propertyBlock.SetColor(BaseColorId, color);
                    renderer.SetPropertyBlock(_propertyBlock);
                }

                return;
            }

            foreach (var renderer in _ghost.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null)
                    continue;

                var material = renderer.material;
                if (material == null)
                    continue;

                if (material.HasProperty(ColorId))
                    material.SetColor(ColorId, color);
                if (material.HasProperty(BaseColorId))
                    material.SetColor(BaseColorId, color);
            }
        }
    }
}
