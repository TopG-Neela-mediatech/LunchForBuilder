using System;
using System.Collections;
using UnityEngine;



namespace tmkoc.lunchforbuilders
{
    public class StoryController : MonoBehaviour
    {
        [Header("Story Data")]
        [SerializeField] private StoryData storyData;

       /* [Header("Root Canvas Group — assign the CanvasGroup on the story prefab root")]
        [SerializeField] private CanvasGroup rootCanvasGroup;*/

        [Header("Sub-components (auto-found if empty)")]
        [SerializeField] private StoryUI       storyUI;
        [SerializeField] private StoryAnimator storyAnimator;
       
        public Action OnStoryFinished;
        private int  _slideIndex;
        private bool _isSkipped;

        private void Awake()
        {
            if (storyUI       == null) storyUI       = GetComponentInChildren<StoryUI>(true);
            if (storyAnimator == null) storyAnimator = GetComponentInChildren<StoryAnimator>(true);
        }
        private void OnEnable()
        {
            _slideIndex = 0;
            _isSkipped  = false;
           // if (rootCanvasGroup != null) rootCanvasGroup.alpha = 1f;
            StartCoroutine(PlayRoutine());
        }
        private void OnDisable()
        {
            StopAllCoroutines();           
        }
        public void SkipStory()
        {
            if (_isSkipped) return;
            _isSkipped = true;
          //  if (rootCanvasGroup != null) rootCanvasGroup.alpha = 0f;
            StopAllCoroutines();
            storyAnimator?.StopImmediate();
         //   RuntimeAudioLoader.Instance?.StopCommonAudioSource();
            Finish();
        }
        private IEnumerator PlayRoutine()
        {
            if (storyData == null || storyData.slides == null || storyData.slides.Length == 0)
            {
                Finish(); yield break;
            }
            while (_slideIndex < storyData.slides.Length && !_isSkipped)
            {
                var slide = storyData.slides[_slideIndex];              
                storyUI?.ShowSlide(slide);
                storyAnimator?.AnimateSlideIn(slide.transitionIn);
               // float dur = GameManager.Instance.SoundManager.PlayIntroSlide(_slideIndex);
              /*  if (dur < 2)
                {
                    dur = 3;
                }
                yield return new WaitForSeconds(dur+0.5f);*/
                if (_isSkipped) break;
                storyAnimator?.AnimateSlideOut(slide.transitionOut);
                yield return new WaitForSeconds(0.5f);
                _slideIndex++;
            }
            Finish();
        }
        private void Finish()
        {
            OnStoryFinished?.Invoke();
        }
    }
}
