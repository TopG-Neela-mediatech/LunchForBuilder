using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace tmkoc.lunchforbuilders
{
    public enum CharacterMood { Hungry, Sad, Happy }

    // The character portrait on the left of screen, plus the particle bursts that play above its
    // head. Deliberately has no timers or state of its own -- CookingManager tracks recipe
    // progress, time remaining, and idle time, and just tells this what to show/play right now.
    public class CharacterReactionController : MonoBehaviour
    {
        [SerializeField] private Image characterImage;

        // Lets CookingManager fold the character into the same off-screen-slide-in intro
        // choreography as the Recipe Card / Preparation Station, reusing the one Image reference
        // already wired here rather than needing a second RectTransform field.
        public RectTransform ImageRectTransform => characterImage != null ? characterImage.rectTransform : null;

        [Header("Sprite Swap Shake")]
        [Tooltip("Subtle shake on the character image's own RectTransform every time its sprite/mood changes -- a little physical 'reaction' bump.")]
        [SerializeField] private float shakeDuration = 0.25f;
        [SerializeField] private float shakeStrength = 8f;
        [SerializeField] private int shakeVibrato = 6;

        [Header("Idle Bob")]
        [Tooltip("Gentle continuous up/down float so the character doesn't look static between reactions.")]
        [SerializeField] private float idleBobDistance = 6f;
        [SerializeField] private float idleBobDuration = 1.2f;

        [Header("Reaction Particles")]
        [Tooltip("One is picked at random and played for every correct action, and once the recipe is complete.")]
        [SerializeField] private ParticleSystem[] happyParticles;
        [Tooltip("One is picked at random and played for every incorrect action, whenever the player goes idle, and once at mission start.")]
        [SerializeField] private ParticleSystem[] sadParticles;

        private MissionRecipeData mission;
        // Two separate tween handles rather than a blanket DOKill() on the RectTransform -- the bob
        // loops forever on the Y axis, the shake is a brief full-vector burst; keeping them as
        // distinct tweens (paused/resumed around each other) avoids them fighting over anchoredPosition.
        private Tween idleBobTween;
        private Tween shakeTween;

        private void Start() => StartIdleBob();

        private void StartIdleBob()
        {
            if (characterImage == null) return;
            idleBobTween?.Kill();
            float baseY = characterImage.rectTransform.anchoredPosition.y;
            idleBobTween = characterImage.rectTransform
                .DOAnchorPosY(baseY + idleBobDistance, idleBobDuration)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo);
        }

        // Explicit pause/resume rather than a blanket DOKill() -- lets CookingManager stop the bob
        // for its own off-screen teleport/slide-in choreography without destroying the tween outright
        // (a killed tween can never be resumed, only recreated).
        public void PauseIdleBob() => idleBobTween?.Pause();

        public void ResumeIdleBob()
        {
            if (idleBobTween != null && idleBobTween.IsActive()) idleBobTween.Play();
            else StartIdleBob();
        }

        public void Setup(MissionRecipeData missionData) => mission = missionData;

        // No shake -- used while the character is off-screen/invisible (the intro reveal, right
        // before it slides into view), where a shake would be pointless and would otherwise fight
        // the intro's own position tween.
        public void SetSpriteSilently(CharacterMood mood) => ApplySprite(mood);

        public void SetSprite(CharacterMood mood)
        {
            if (!ApplySprite(mood)) return;

            // A small physical "reaction" bump every time the mood changes, not just a flat sprite
            // swap. The idle bob pauses for the duration so the two don't both drive anchoredPosition
            // at once, then picks back up right where it left off.
            idleBobTween?.Pause();
            shakeTween?.Kill();
            shakeTween = characterImage.rectTransform
                .DOShakeAnchorPos(shakeDuration, strength: shakeStrength, vibrato: shakeVibrato)
                .OnComplete(() => idleBobTween?.Play());
        }

        private bool ApplySprite(CharacterMood mood)
        {
            if (mission == null || characterImage == null) return false;
            Sprite sprite = mood switch
            {
                CharacterMood.Happy => mission.HappyCharacterSprite,
                CharacterMood.Sad => mission.SadCharacterSprite,
                _ => mission.HungryCharacterSprite,
            };
            if (sprite != null) characterImage.sprite = sprite;
            return true;
        }

        public void PlayHappyBurst() => PlayRandomParticle(happyParticles);
        public void PlaySadBurst() => PlayRandomParticle(sadParticles);

        private void PlayRandomParticle(ParticleSystem[] pool)
        {
            if (pool == null || pool.Length == 0) return;
            var chosen = pool[Random.Range(0, pool.Length)];
            if (chosen == null) return;
            // Restart cleanly even if it's still finishing a previous burst.
            chosen.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            chosen.Play();
        }

        private void OnDestroy()
        {
            idleBobTween?.Kill();
            shakeTween?.Kill();
        }
    }
}
