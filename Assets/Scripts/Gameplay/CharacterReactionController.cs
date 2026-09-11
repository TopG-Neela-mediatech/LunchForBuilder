using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace tmkoc.lunchforbuilders
{
    // The character portrait on the left of screen, plus the emoji that pops above its head to
    // punctuate the current mood. CookingManager decides WHEN the mood changes (mission start =
    // hungry, a wrong drag = sad, recipe complete = happy); this class only knows how to swap the
    // portrait sprite and pop one random emoji from that mood's pool.
    public class CharacterReactionController : MonoBehaviour
    {
        [SerializeField] private Image characterImage;
        [Tooltip("Child of the character -- the emoji that pops above its head.")]
        [SerializeField] private Image emojiImage;

        [Header("Emoji Pop")]
        [SerializeField] private float popInDuration = 0.3f;
        [SerializeField] private float holdDuration = 1.2f;
        [SerializeField] private float popOutDuration = 0.2f;

        [Header("Sad -> Hungry Auto-Revert")]
        [Tooltip("How long the Sad reaction stays up before reverting back to Hungry -- a wrong drag doesn't end the mission, so the character goes back to waiting.")]
        [SerializeField] private float sadHoldDuration = 1.5f;

        private MissionRecipeData mission;
        private Sequence emojiSequence;
        private Coroutine revertRoutine;

        private void Awake()
        {
            if (emojiImage != null) emojiImage.gameObject.SetActive(false);
        }

        // Called once per mission start -- just caches the sprite/emoji pools for this recipe.
        // Doesn't show anything on its own; call ShowHungry() to actually start the reaction.
        public void Setup(MissionRecipeData missionData)
        {
            mission = missionData;
            StopRevert();
        }

        public void ShowHungry()
        {
            StopRevert();
            Apply(mission.HungryCharacterSprite, mission.HungryEmojis);
        }

        public void ShowHappy()
        {
            StopRevert();
            Apply(mission.HappyCharacterSprite, mission.HappyEmojis);
        }

        // Temporary -- automatically reverts back to Hungry on its own after sadHoldDuration, since
        // the mission keeps going after a wrong drag rather than ending.
        public void ShowSad()
        {
            StopRevert();
            Apply(mission.SadCharacterSprite, mission.SadEmojis);
            revertRoutine = StartCoroutine(RevertToHungryAfterDelay());
        }

        private IEnumerator RevertToHungryAfterDelay()
        {
            yield return new WaitForSeconds(sadHoldDuration);
            revertRoutine = null;
            Apply(mission.HungryCharacterSprite, mission.HungryEmojis);
        }

        private void StopRevert()
        {
            if (revertRoutine != null)
            {
                StopCoroutine(revertRoutine);
                revertRoutine = null;
            }
        }

        private void Apply(Sprite characterSprite, Sprite[] emojiPool)
        {
            if (mission == null) return;
            if (characterImage != null && characterSprite != null) characterImage.sprite = characterSprite;
            PlayEmojiPop(PickRandom(emojiPool));
        }

        private Sprite PickRandom(Sprite[] pool)
        {
            if (pool == null || pool.Length == 0) return null;
            return pool[Random.Range(0, pool.Length)];
        }

        private void PlayEmojiPop(Sprite sprite)
        {
            if (emojiImage == null || sprite == null) return;
            emojiImage.sprite = sprite;
            emojiImage.gameObject.SetActive(true);
            emojiImage.rectTransform.localScale = Vector3.zero;

            emojiSequence?.Kill();
            emojiSequence = DOTween.Sequence();
            emojiSequence.Append(emojiImage.rectTransform.DOScale(1f, popInDuration).SetEase(Ease.OutBack));
            emojiSequence.AppendInterval(holdDuration);
            emojiSequence.Append(emojiImage.rectTransform.DOScale(0f, popOutDuration).SetEase(Ease.InBack));
            emojiSequence.OnComplete(() => emojiImage.gameObject.SetActive(false));
        }

        private void OnDestroy()
        {
            emojiSequence?.Kill();
            StopRevert();
        }
    }
}
