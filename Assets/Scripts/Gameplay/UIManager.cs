using DG.Tweening;
using TMPro;
using UnityEngine;

namespace tmkoc.lunchforbuilders
{
    // Listens only to GameManager events for gameplay feedback -- no FindObjectOfType and no
    // static lookups; every reference here is assigned in the Inspector. Same shape as
    // BuildABot's UIManager.
    public class UIManager : MonoBehaviour
    {
        [Header("Manager Reference")]
        [SerializeField] private GameManager gameManager;

        [Header("HUD")]
        [SerializeField] private TMP_Text recipeProgressText;

        [Header("Correct / Incorrect Feedback")]
        [SerializeField] private GameObject feedbackPopup;
        [SerializeField] private CanvasGroup feedbackCanvasGroup;
        [SerializeField] private TMP_Text feedbackText;
        [SerializeField] private string correctMessage = "Yum! Great counting!";
        [SerializeField] private string incorrectMessage = "Not quite -- try again!";
        [SerializeField] private float feedbackHoldDuration = 1.2f;

        private Sequence feedbackSequence;

        private void OnEnable()
        {
            if (gameManager == null) return;
            gameManager.OnRecipeProgress += HandleRecipeProgress;
            gameManager.OnIngredientResolved += HandleIngredientResolved;
        }

        private void OnDisable()
        {
            if (gameManager == null) return;
            gameManager.OnRecipeProgress -= HandleRecipeProgress;
            gameManager.OnIngredientResolved -= HandleIngredientResolved;
        }

        private void HandleRecipeProgress(int placed, int required)
        {
            if (recipeProgressText != null) recipeProgressText.text = $"{placed}/{required}";
        }

        private void HandleIngredientResolved(string ingredientId, bool wasCorrect)
        {
            if (feedbackPopup == null || feedbackCanvasGroup == null || feedbackText == null) return;

            feedbackText.text = wasCorrect ? correctMessage : incorrectMessage;

            feedbackSequence?.Kill();
            feedbackPopup.SetActive(true);
            feedbackCanvasGroup.alpha = 0f;
            feedbackPopup.transform.localScale = Vector3.one * 0.8f;

            feedbackSequence = DOTween.Sequence();
            feedbackSequence.Append(feedbackCanvasGroup.DOFade(1f, 0.2f));
            feedbackSequence.Join(feedbackPopup.transform.DOScale(1f, 0.25f).SetEase(Ease.OutBack));
            feedbackSequence.AppendInterval(feedbackHoldDuration);
            feedbackSequence.Append(feedbackCanvasGroup.DOFade(0f, 0.2f));
            feedbackSequence.OnComplete(() => feedbackPopup.SetActive(false));
        }

        private void OnDestroy()
        {
            feedbackSequence?.Kill();
        }
    }
}
