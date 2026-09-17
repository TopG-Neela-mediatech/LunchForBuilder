using System;
using System.Collections;
using DG.Tweening;
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

        [Header("Row Feedback")]
        [Tooltip("Small punch on a row's icon every time its count actually increases.")]
        [SerializeField] private float rowTickPunchScale = 0.15f;
        [SerializeField] private float rowTickPunchDuration = 0.25f;
        [Tooltip("Bigger punch + a brief colour flash the moment a row hits its target.")]
        [SerializeField] private float rowCompletePunchScale = 0.35f;
        [SerializeField] private float rowCompletePunchDuration = 0.4f;
        [SerializeField] private Color rowCompleteFlashColor = new Color(0.6f, 1f, 0.6f);
        [SerializeField] private float rowCompleteFlashDuration = 0.3f;

        [Header("Card Flip")]
        [Tooltip("Half the total flip duration -- one half shrinks the outgoing face to nothing, the other grows the incoming face back to full width.")]
        [SerializeField] private float flipHalfDuration = 0.2f;

        [Header("Row Layout")]
        [Tooltip("Vertical distance between rows -- matches the 100-unit spacing already baked into the 5 row slots in the editor (200, 100, 0, -100, -200), so a full 5-row recipe repositions to exactly where it already sits.")]
        [SerializeField] private float rowSpacing = 100f;

        public event Action OnPeekUsed;

        private MissionRecipeData mission;
        private Coroutine memoryRoutine;
        // Per-row removal bookkeeping (Mission 4's "Remove 2 Ice Cubes") -- a removal row shows how
        // many are left in the station counting DOWN toward a target (e.g. "6/4"), not how many have
        // been removed counting up, since that reads more clearly as "you need to take some out".
        private bool[] rowIsRemoval;
        private int[] rowStartingCount;
        private int[] lastRowCurrent;

        private RectTransform cardFrontRect;
        private RectTransform cardBackRect;
        private Sequence flipSequence;

        private void Awake()
        {
            if (peekButton != null) peekButton.onClick.AddListener(HandlePeekPressed);
            SetPeekButtonVisible(false);
            cardFrontRect = cardFrontFace != null ? cardFrontFace.GetComponent<RectTransform>() : null;
            cardBackRect = cardBackFace != null ? cardBackFace.GetComponent<RectTransform>() : null;
        }

        public void Setup(MissionRecipeData missionData)
        {
            mission = missionData;
            StopMemoryRoutine();

            if (rowIsRemoval == null || rowIsRemoval.Length != rows.Length)
            {
                rowIsRemoval = new bool[rows.Length];
                rowStartingCount = new int[rows.Length];
                lastRowCurrent = new int[rows.Length];
            }

            var requirements = mission.Requirements;
            int usedCount = requirements?.Length ?? 0;
            for (int i = 0; i < rows.Length; i++)
            {
                bool inUse = i < usedCount;
                if (rows[i].icon != null) rows[i].icon.gameObject.SetActive(inUse);
                lastRowCurrent[i] = 0;
                if (!inUse) continue;

                var req = requirements[i];
                if (rows[i].icon != null)
                {
                    // Reset any flash/punch left over from the previous mission's last-completed row.
                    rows[i].icon.DOKill();
                    rows[i].icon.rectTransform.DOKill();
                    rows[i].icon.rectTransform.localScale = Vector3.one;
                    rows[i].icon.sprite = req.icon;
                    rows[i].icon.color = Color.white;

                    // Rows are baked in the editor as a fixed top-to-bottom stack for the full 5-row
                    // case; a recipe using fewer rows re-centers them around the same midpoint
                    // instead of leaving them pinned to the top slots with empty space below.
                    Vector2 pos = rows[i].icon.rectTransform.anchoredPosition;
                    pos.y = ((usedCount - 1) / 2f - i) * rowSpacing;
                    rows[i].icon.rectTransform.anchoredPosition = pos;
                }

                rowIsRemoval[i] = req.isRemoval;
                rowStartingCount[i] = req.isRemoval ? FindStartingCount(req.ingredientId) : 0;

                if (rows[i].counterText != null)
                    rows[i].counterText.text = req.isRemoval
                        ? $"{rowStartingCount[i]}/{rowStartingCount[i] - req.requiredCount}"
                        : $"0/{req.requiredCount}";
            }

            ShowFace(true, animate: false);
            SetPeekButtonVisible(false);

            if (mission.LearningRule == LearningRule.Memory)
                memoryRoutine = StartCoroutine(MemoryRevealRoutine());
        }

        // current/required are already ordered to match mission.Requirements. For a removal row,
        // current is "removed so far" -- converted here into "remaining in the station / target
        // remaining" (e.g. 6 ice cubes down to a target of 4), which reads far more clearly as
        // "take some out" than a bare "0/2" removed-count would.
        public void UpdateRow(int index, int current, int required)
        {
            if (index < 0 || index >= rows.Length || rows[index]?.icon == null || !rows[index].icon.gameObject.activeSelf) return;
            if (rows[index].counterText == null) return;

            bool increased = lastRowCurrent != null && current > lastRowCurrent[index];
            if (lastRowCurrent != null) lastRowCurrent[index] = current;

            if (rowIsRemoval != null && rowIsRemoval[index])
            {
                int startingCount = rowStartingCount[index];
                int remaining = startingCount - Mathf.Min(current, required);
                int target = startingCount - required;
                rows[index].counterText.text = $"{remaining}/{target}";
            }
            else
            {
                rows[index].counterText.text = $"{Mathf.Min(current, required)}/{required}";
            }

            if (increased) PlayRowTick(index, current >= required);
        }

        // A small punch every time a row's count ticks up, and a bigger punch + a quick green flash
        // the moment it actually completes -- direct visual feedback tied to counting correctly.
        private void PlayRowTick(int index, bool justCompleted)
        {
            var icon = rows[index].icon;
            if (icon == null) return;

            icon.rectTransform.DOKill();
            float punch = justCompleted ? rowCompletePunchScale : rowTickPunchScale;
            float duration = justCompleted ? rowCompletePunchDuration : rowTickPunchDuration;
            icon.rectTransform.DOPunchScale(Vector3.one * punch, duration, 6, 0.8f);

            if (justCompleted)
            {
                icon.DOKill();
                Color original = icon.color;
                DOTween.Sequence()
                    .Append(icon.DOColor(rowCompleteFlashColor, rowCompleteFlashDuration * 0.5f))
                    .Append(icon.DOColor(original, rowCompleteFlashDuration * 0.5f));
            }
        }

        private int FindStartingCount(string ingredientId)
        {
            var starting = mission.StartingIngredients;
            if (starting == null) return 0;
            foreach (var s in starting)
                if (s.ingredientId == ingredientId) return s.requiredCount;
            return 0;
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

        // Animates like an actual card flip: shrink the visible face to nothing on its own X axis,
        // swap which face is active at that pinch-point, then grow the new face back out -- cheap
        // and reads convincingly on a flat UI card without needing a real 3D/perspective camera.
        // animate=false (Setup's initial reset) just snaps straight to the target face instead.
        private void ShowFace(bool front, bool animate = true)
        {
            flipSequence?.Kill();

            if (!animate)
            {
                if (cardFrontFace != null) { cardFrontFace.SetActive(front); if (cardFrontRect != null) cardFrontRect.localScale = Vector3.one; }
                if (cardBackFace != null) { cardBackFace.SetActive(!front); if (cardBackRect != null) cardBackRect.localScale = Vector3.one; }
                return;
            }

            GameObject fromFace = front ? cardBackFace : cardFrontFace;
            RectTransform fromRect = front ? cardBackRect : cardFrontRect;
            GameObject toFace = front ? cardFrontFace : cardBackFace;
            RectTransform toRect = front ? cardFrontRect : cardBackRect;

            flipSequence = DOTween.Sequence();
            if (fromRect != null) flipSequence.Append(fromRect.DOScaleX(0f, flipHalfDuration).SetEase(Ease.InQuad));
            flipSequence.AppendCallback(() =>
            {
                if (fromFace != null) fromFace.SetActive(false);
                if (toFace != null) toFace.SetActive(true);
                if (toRect != null) toRect.localScale = new Vector3(0f, 1f, 1f);
            });
            if (toRect != null) flipSequence.Append(toRect.DOScaleX(1f, flipHalfDuration).SetEase(Ease.OutQuad));
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

        private void OnDestroy()
        {
            StopMemoryRoutine();
            flipSequence?.Kill();
        }
    }
}
