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

        [Header("Reaction Particles")]
        [Tooltip("One is picked at random and played for every correct action, and once the recipe is complete.")]
        [SerializeField] private ParticleSystem[] happyParticles;
        [Tooltip("One is picked at random and played for every incorrect action, whenever the player goes idle, and once at mission start.")]
        [SerializeField] private ParticleSystem[] sadParticles;

        private MissionRecipeData mission;

        public void Setup(MissionRecipeData missionData) => mission = missionData;

        public void SetSprite(CharacterMood mood)
        {
            if (mission == null || characterImage == null) return;
            Sprite sprite = mood switch
            {
                CharacterMood.Happy => mission.HappyCharacterSprite,
                CharacterMood.Sad => mission.SadCharacterSprite,
                _ => mission.HungryCharacterSprite,
            };
            if (sprite != null) characterImage.sprite = sprite;

            // A small physical "reaction" bump every time the mood changes, not just a flat sprite swap.
            characterImage.rectTransform.DOKill();
            characterImage.rectTransform.DOShakeAnchorPos(shakeDuration, strength: shakeStrength, vibrato: shakeVibrato);
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
    }
}
