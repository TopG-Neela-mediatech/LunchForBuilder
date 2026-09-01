using UnityEngine;

namespace tmkoc.lunchforbuilders
{
    public enum SlideTransitionType { FadeIn, FadeOut, SlideFromRight, SlideFromLeft, ScaleUp, Instant }

    [System.Serializable]
    public class StorySlide
    {
        [Header("Visuals")]
        public Sprite  backgroundSprite;
        public Sprite  foregroundSprite;
        [TextArea(2, 4)]
        public string  captionText;

        [Header("Timing")]
        public float   displayDuration = 3f;

        [Header("Transition")]
        public SlideTransitionType transitionIn  = SlideTransitionType.FadeIn;
        public SlideTransitionType transitionOut = SlideTransitionType.FadeOut;

        [Header("Audio")]
        public AudioClip narrationClip;
        public AudioClip slideEnterSFX;
    }

    [CreateAssetMenu(fileName = "Story_New", menuName = "StorySystem/Story Data", order = 1)]
    public class StoryData : ScriptableObject
    {
        [Header("Slides")]
        public StorySlide[] slides;

        [Header("Skip")]
        public bool isSkippable = true;

        [Header("Music")]
        public AudioClip storyBGM;
        [Range(0f, 1f)] public float bgmVolume = 0.7f;
    }
}
