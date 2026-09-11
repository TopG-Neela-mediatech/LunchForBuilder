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
