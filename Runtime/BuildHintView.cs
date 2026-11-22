using System.Collections;
using TMPro;
using UnityEngine;

namespace LOGIYGames.SurvivalBuilding
{
    public class BuildHintView : MonoBehaviour
    {
        public TMP_Text Label;
        public BuildMessages Messages;
        public CanvasGroup Group;

        [SerializeField] float displayDuration = 1f;
        [SerializeField] float fadeDuration = 0.5f;

        Coroutine disappearCoroutine;

        void Start() => Group.alpha = 0f;

        public void Show(PlacementResult p, PieceDefinition def = null)
        {
            if (p.Valid || p.FailReason == PlacementFailReason.None) { Hide(); return; }

            var text = Messages
                ? (def ? Messages.For(def, p.FailReason) : Messages.For(p.FailReason))
                : p.FailReason.ToString();

            ShowMessage(text);
        }

        public void ShowMessage(string text)
        {
            if (string.IsNullOrEmpty(text)) { Hide(); return; }

            Label.text = text;
            Group.alpha = 1f;

            if (disappearCoroutine != null) StopCoroutine(disappearCoroutine);
            disappearCoroutine = StartCoroutine(DisappearCoroutine());
        }

        public void Hide()
        {
            if (disappearCoroutine != null) { StopCoroutine(disappearCoroutine); disappearCoroutine = null; }
            Group.alpha = 0f;
        }

        IEnumerator DisappearCoroutine()
        {
            yield return new WaitForSeconds(displayDuration);

            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                Group.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
                yield return null;
            }
            Group.alpha = 0f;
            disappearCoroutine = null;
        }
    }
}