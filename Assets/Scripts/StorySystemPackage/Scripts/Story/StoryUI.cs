
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace tmkoc.lunchforbuilders
{
    /// <summary>Updates Canvas elements per slide. Background, foreground, caption.</summary>
    public class StoryUI : MonoBehaviour
    {
        [Header("Slide UI")]
        [SerializeField] private Image            backgroundImage;
        [SerializeField] private Image            foregroundImage;
        [SerializeField] private TextMeshProUGUI  captionText;
        [SerializeField] private CanvasGroup      slideCanvasGroup;
        [SerializeField] private Image            captionBackground;

        public void ShowSlide(StorySlide slide)
        {
            if (backgroundImage != null) { backgroundImage.sprite = slide.backgroundSprite; backgroundImage.enabled = slide.backgroundSprite != null; }
            if (foregroundImage != null) { foregroundImage.sprite = slide.foregroundSprite; foregroundImage.enabled = slide.foregroundSprite != null; }
            if (captionText     != null) { captionText.text = slide.captionText; captionText.enabled = !string.IsNullOrEmpty(slide.captionText); }
            if (captionBackground != null) captionBackground.enabled = !string.IsNullOrEmpty(slide.captionText);
            if (slideCanvasGroup != null) slideCanvasGroup.alpha = 1f;
        }

        public void HideSlide()  { if (slideCanvasGroup != null) slideCanvasGroup.alpha = 0f; }
        public void SetAlpha(float a) { if (slideCanvasGroup != null) slideCanvasGroup.alpha = a; }
    }
}