using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace tmkoc.lunchforbuilders
{
    // The row IS the icon -- icon.gameObject doubles as the row's root (shown/hidden as a unit),
    // so there's no separate root reference to keep in sync with it.
    [Serializable]
    public class RecipeCardRow
    {
        public Image icon;
        public TMP_Text counterText;
    }

    // Renders the current mission's requirement rows and, for Memory missions, owns the
    // reveal -> flip -> Peek timing described in the GDD ("Recipe remains visible for 5 seconds,
    // then the card flips over ... A Peek button briefly reveals the recipe again. Using Peek
    // affects the third star.").
    public class RecipeCardController : MonoBehaviour
    {
        [Tooltip("Fixed pool of row UI, sized for the largest recipe (5 rows). Unused rows are hidden.")]
        [SerializeField] private RecipeCardRow[] rows;
        [SerializeField] private GameObject cardFrontFace;
        [SerializeField] private GameObject cardBackFace;
        [SerializeField] private Button peekButton;

        public event Action OnPeekUsed;

        private MissionRecipeData mission;
        private Coroutine memoryRoutine;

        private void Awake()
        {
            if (peekButton != null) peekButton.onClick.AddListener(HandlePeekPressed);
            SetPeekButtonVisible(false);
        }

        public void Setup(MissionRecipeData missionData)
        {
            mission = missionData;
            StopMemoryRoutine();

            var requirements = mission.Requirements;
            for (int i = 0; i < rows.Length; i++)
            {
                bool inUse = requirements != null && i < requirements.Length;
                if (rows[i].icon != null) rows[i].icon.gameObject.SetActive(inUse);
                if (!inUse) continue;

                if (rows[i].icon != null) rows[i].icon.sprite = requirements[i].icon;
                if (rows[i].counterText != null) rows[i].counterText.text = $"0/{requirements[i].requiredCount}";
            }

            ShowFace(true);
            SetPeekButtonVisible(false);

            if (mission.LearningRule == LearningRule.Memory)
                memoryRoutine = StartCoroutine(MemoryRevealRoutine());
        }

        // current/required are already ordered to match mission.Requirements.
        public void UpdateRow(int index, int current, int required)
        {
            if (index < 0 || index >= rows.Length || rows[index]?.icon == null || !rows[index].icon.gameObject.activeSelf) return;
            if (rows[index].counterText != null) rows[index].counterText.text = $"{Mathf.Min(current, required)}/{required}";
        }

        private IEnumerator MemoryRevealRoutine()
        {
            yield return new WaitForSeconds(mission.MemoryRevealSeconds);
            ShowFace(false);
            SetPeekButtonVisible(true);
            memoryRoutine = null;
        }

        private void HandlePeekPressed()
        {
            if (mission == null || mission.LearningRule != LearningRule.Memory) return;
            OnPeekUsed?.Invoke();
            StopMemoryRoutine();
            memoryRoutine = StartCoroutine(PeekRoutine());
        }

        private IEnumerator PeekRoutine()
        {
            ShowFace(true);
            SetPeekButtonVisible(false);
            yield return new WaitForSeconds(mission.PeekRevealSeconds);
            ShowFace(false);
            SetPeekButtonVisible(true);
            memoryRoutine = null;
        }

        private void ShowFace(bool front)
        {
            if (cardFrontFace != null) cardFrontFace.SetActive(front);
            if (cardBackFace != null) cardBackFace.SetActive(!front);
        }

        private void SetPeekButtonVisible(bool visible)
        {
            if (peekButton != null) peekButton.gameObject.SetActive(visible && mission != null && mission.LearningRule == LearningRule.Memory);
        }

        private void StopMemoryRoutine()
        {
            if (memoryRoutine != null) StopCoroutine(memoryRoutine);
            memoryRoutine = null;
        }

        private void OnDestroy() => StopMemoryRoutine();
    }
}
