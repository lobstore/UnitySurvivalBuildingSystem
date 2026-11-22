using System.Collections;
using TMPro;
using UnityEngine;

namespace LOGIYGames.SurvivalBuilding
{
    /// <summary>Displays temporary placement and destruction messages in a CanvasGroup.</summary>
    public class BuildHintView : MonoBehaviour
    {
        public TMP_Text Label;
        public BuildMessages Messages;
        public CanvasGroup Group;

        [SerializeField, Min(0f)] private float displayDuration = 1f;
        [SerializeField, Min(0f)] private float fadeDuration = 0.5f;

        private Coroutine _disappearCoroutine;

        /// <summary>Initializes the hint as hidden.</summary>
        private void Start()
        {
            if (Group != null)
                Group.alpha = 0f;
        }

        /// <summary>Displays the placement failure message associated with a result.</summary>
        /// <param name="placement">Placement evaluation result.</param>
        /// <param name="definition">Definition used to resolve a piece-specific surface message.</param>
        public void Show(PlacementResult placement, PieceDefinition definition = null)
        {
            if (placement.Valid || placement.FailReason == PlacementFailReason.None)
            {
                Hide();
                return;
            }

            var message = Messages != null
                ? (definition != null
                    ? Messages.For(definition, placement.FailReason)
                    : Messages.For(placement.FailReason))
                : placement.FailReason.ToString();

            ShowMessage(message);
        }

        /// <summary>Displays arbitrary text and restarts the automatic hide timer.</summary>
        /// <param name="message">Message to display. Empty text hides the hint.</param>
        public void ShowMessage(string message)
        {
            if (Label == null || Group == null)
                return;

            if (string.IsNullOrWhiteSpace(message))
            {
                Hide();
                return;
            }

            Label.text = message;
            Group.alpha = 1f;

            if (_disappearCoroutine != null)
                StopCoroutine(_disappearCoroutine);

            _disappearCoroutine = StartCoroutine(DisappearCoroutine());
        }

        /// <summary>Immediately hides the hint and stops any active fade coroutine.</summary>
        public void Hide()
        {
            if (_disappearCoroutine != null)
            {
                StopCoroutine(_disappearCoroutine);
                _disappearCoroutine = null;
            }

            if (Group != null)
                Group.alpha = 0f;
        }

        /// <summary>Waits for the display duration, then fades the hint out.</summary>
        private IEnumerator DisappearCoroutine()
        {
            yield return new WaitForSeconds(Mathf.Max(0f, displayDuration));

            if (Group == null)
            {
                _disappearCoroutine = null;
                yield break;
            }

            if (fadeDuration <= 0f)
            {
                Group.alpha = 0f;
                _disappearCoroutine = null;
                yield break;
            }

            var elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                Group.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
                yield return null;
            }

            Group.alpha = 0f;
            _disappearCoroutine = null;
        }
    }
}
