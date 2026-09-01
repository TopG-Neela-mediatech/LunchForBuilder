using System.Collections;
using UnityEngine;


namespace tmkoc.lunchforbuilders
{
    /// <summary>Slide-in and slide-out transitions: Fade, Slide, Scale, Instant.</summary>
    public class StoryAnimator : MonoBehaviour
    {
        [SerializeField] private CanvasGroup    slideCanvasGroup;
        [SerializeField] private RectTransform  slideRect;
        [SerializeField] private float          transitionDuration = 0.4f;
        [SerializeField] private float          slideOffsetPx = 1200f;

        private Coroutine _active;

        public void AnimateSlideIn(SlideTransitionType t)  { if (_active != null) StopCoroutine(_active); _active = StartCoroutine(InRoutine(t)); }
        public void AnimateSlideOut(SlideTransitionType t) { if (_active != null) StopCoroutine(_active); _active = StartCoroutine(OutRoutine(t)); }

        public void StopImmediate()
        {
            if (_active != null) { StopCoroutine(_active); _active = null; }
            SetAlpha(1f);
            SetPos(Vector2.zero);
            SetScale(Vector3.one);
        }

        private IEnumerator InRoutine(SlideTransitionType t)
        {
            float e = 0f;
            switch (t) {
                case SlideTransitionType.FadeIn:
                    SetAlpha(0f);
                    while (e < transitionDuration) { e += Time.deltaTime; SetAlpha(Mathf.SmoothStep(0,1,e/transitionDuration)); yield return null; }
                    SetAlpha(1f); break;
                case SlideTransitionType.SlideFromRight:
                    SetPos(new Vector2(slideOffsetPx,0)); SetAlpha(1f);
                    while (e < transitionDuration) { e += Time.deltaTime; SetPos(Vector2.Lerp(new Vector2(slideOffsetPx,0), Vector2.zero, Mathf.SmoothStep(0,1,e/transitionDuration))); yield return null; }
                    SetPos(Vector2.zero); break;
                case SlideTransitionType.SlideFromLeft:
                    SetPos(new Vector2(-slideOffsetPx,0)); SetAlpha(1f);
                    while (e < transitionDuration) { e += Time.deltaTime; SetPos(Vector2.Lerp(new Vector2(-slideOffsetPx,0), Vector2.zero, Mathf.SmoothStep(0,1,e/transitionDuration))); yield return null; }
                    SetPos(Vector2.zero); break;
                case SlideTransitionType.ScaleUp:
                    SetScale(Vector3.zero); SetAlpha(1f);
                    while (e < transitionDuration) { e += Time.deltaTime; SetScale(Vector3.Lerp(Vector3.zero, Vector3.one, Mathf.SmoothStep(0,1,e/transitionDuration))); yield return null; }
                    SetScale(Vector3.one); break;
                default: SetAlpha(1f); SetPos(Vector2.zero); SetScale(Vector3.one); break;
            }
        }

        private IEnumerator OutRoutine(SlideTransitionType t)
        {
            float e = 0f;
            switch (t) {
                case SlideTransitionType.SlideFromRight:
                    while (e < transitionDuration) { e += Time.deltaTime; SetPos(Vector2.Lerp(Vector2.zero, new Vector2(-slideOffsetPx,0), Mathf.SmoothStep(0,1,e/transitionDuration))); yield return null; }
                    SetPos(Vector2.zero); break;
                case SlideTransitionType.SlideFromLeft:
                    while (e < transitionDuration) { e += Time.deltaTime; SetPos(Vector2.Lerp(Vector2.zero, new Vector2(slideOffsetPx,0), Mathf.SmoothStep(0,1,e/transitionDuration))); yield return null; }
                    SetPos(Vector2.zero); break;
                case SlideTransitionType.Instant:
                    SetAlpha(0f); break;
                default:
                    while (e < transitionDuration) { e += Time.deltaTime; SetAlpha(Mathf.SmoothStep(1,0,e/transitionDuration)); yield return null; }
                    SetAlpha(0f); break;
            }
        }

        private void SetAlpha(float a) { if (slideCanvasGroup != null) slideCanvasGroup.alpha = a; }
        private void SetPos(Vector2 p)  { if (slideRect != null) slideRect.anchoredPosition = p; }
        private void SetScale(Vector3 s){ if (slideRect != null) slideRect.localScale = s; }
    }
}