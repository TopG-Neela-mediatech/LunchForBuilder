using System;
using System.Collections;
using System.Text;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace tmkoc.lunchforbuilders
{
    // The row IS the icon -- icon.gameObject doubles as the row's root (shown/hidden as a unit),
    // so there's no separate root reference to keep in sync with it. progressFill is expected to be
    // a child of that same icon GameObject (an Image set to Type = Filled), so it shows/hides for
    // free along with the rest of the row. Only used for Memory missions -- see showAllRowsAtOnce.
    [Serializable]
    public class RecipeCardRow
    {
        public Image icon;
        public TMP_Text counterText;
        public Image progressFill;
    }

    // Renders the current mission's requirements and, for Memory missions, owns the
    // reveal -> flip -> Peek timing described in the GDD ("Recipe remains visible for 5 seconds,
    // then the card flips over ... A Peek button briefly reveals the recipe again. Using Peek
    // affects the third star.").
    //
    // Every mission except Memory shows one ingredient at a time on a single shared set of
    // widgets (name / icon / progress fill / counter) and flips to the next the moment the
    // current one is finished. Memory still shows its whole recipe at once on the old row-list
    // layout, since the entire mechanic is memorizing the full list before it hides.
    public class RecipeCardController : MonoBehaviour
    {
        [Tooltip("Fixed pool of row UI used ONLY for Memory missions -- sized for the largest recipe (5 rows). Unused rows are hidden.")]
        [SerializeField] private RecipeCardRow[] rows;

        [Header("Single-Ingredient Display (every mission except Memory)")]
        [Tooltip("Parent of the 4 fields below -- flipped as one unit when advancing to the next ingredient.")]
        [SerializeField] private RectTransform singleDisplayRoot;
        [SerializeField] private TMP_Text ingredientNameText;
        [SerializeField] private Image ingredientIcon;
        [SerializeField] private Image ingredientProgressFill;
        [SerializeField] private TMP_Text ingredientCounterText;

        [Header("Card Face (Memory only)")]
        [SerializeField] private GameObject cardFrontFace;

        [Header("Row/Ingredient Feedback")]
        [Tooltip("Small punch on the icon every time its count actually increases.")]
        [SerializeField] private float rowTickPunchScale = 0.15f;
        [SerializeField] private float rowTickPunchDuration = 0.25f;
        [Tooltip("Bigger punch + a brief colour flash the moment an ingredient hits its target.")]
        [SerializeField] private float rowCompletePunchScale = 0.35f;
        [SerializeField] private float rowCompletePunchDuration = 0.4f;
        [SerializeField] private Color rowCompleteFlashColor = new Color(0.6f, 1f, 0.6f);
        [SerializeField] private float rowCompleteFlashDuration = 0.3f;

        [Header("Card Flip")]
        [Tooltip("Half the total flip duration -- one half shrinks the outgoing content to nothing, the other grows the incoming content back to full width.")]
        [SerializeField] private float flipHalfDuration = 0.2f;

        [Header("Row Layout (Memory only)")]
        [Tooltip("Vertical distance between rows -- matches the 100-unit spacing already baked into the 5 row slots in the editor (200, 100, 0, -100, -200), so a full 5-row recipe repositions to exactly where it already sits.")]
        [SerializeField] private float rowSpacing = 100f;
        [Tooltip("How long the progress fill bar takes to animate to its new amount every time the count changes.")]
        [SerializeField] private float fillBarTweenDuration = 0.3f;

        private MissionRecipeData mission;
        private Coroutine memoryRoutine;
        // Bookkeeping per requirement (index-aligned with mission.Requirements), kept regardless of
        // which one is currently displayed -- a mission with no fixed ingredient order lets the
        // player add anything that's still needed, not just whatever the card happens to be showing.
        private bool[] rowIsRemoval;
        private int[] rowStartingCount;
        private int[] lastRowCurrent;
        private bool[] rowInUse;

        // Memory missions show every requirement at once (the whole point is memorizing the full
        // list before it flips away); every other mission shows one at a time on the single shared
        // display and flips ahead as each is finished.
        private bool showAllRowsAtOnce;
        private int currentDisplayIndex;

        private RectTransform cardFrontRect;
        private Sequence flipSequence;
        private Sequence singleFlipSequence;

        private void Awake()
        {
            cardFrontRect = cardFrontFace != null ? cardFrontFace.GetComponent<RectTransform>() : null;
        }

        public void Setup(MissionRecipeData missionData)
        {
            mission = missionData;
            StopMemoryRoutine();
            singleFlipSequence?.Kill();

            int slotCount = rows.Length;
            if (rowIsRemoval == null || rowIsRemoval.Length != slotCount)
            {
                rowIsRemoval = new bool[slotCount];
                rowStartingCount = new int[slotCount];
                lastRowCurrent = new int[slotCount];
                rowInUse = new bool[slotCount];
            }

            showAllRowsAtOnce = mission.LearningRule == LearningRule.Memory;
            currentDisplayIndex = 0;

            var requirements = mission.Requirements;
            int usedCount = requirements?.Length ?? 0;

            for (int i = 0; i < slotCount; i++)
            {
                bool inUse = i < usedCount;
                rowInUse[i] = inUse;
                lastRowCurrent[i] = 0;

                if (!showAllRowsAtOnce)
                {
                    // The single shared display renders ingredients instead -- old per-row visuals stay hidden.
                    if (rows[i].icon != null) rows[i].icon.gameObject.SetActive(false);
                }

                if (!inUse) continue;

                var req = requirements[i];
                rowIsRemoval[i] = req.isRemoval;
                rowStartingCount[i] = req.isRemoval ? FindStartingCount(req.ingredientId) : 0;

                if (showAllRowsAtOnce && rows[i].icon != null)
                {
                    // Reset any flash/punch left over from the previous mission's last-completed row.
                    rows[i].icon.DOKill();
                    rows[i].icon.rectTransform.DOKill();
                    rows[i].icon.rectTransform.localScale = Vector3.one;
                    rows[i].icon.sprite = req.icon;
                    rows[i].icon.color = Color.white;

                    if (rows[i].progressFill != null)
                    {
                        rows[i].progressFill.DOKill();
                        rows[i].progressFill.fillAmount = 0f;
                    }

                    // Rows are baked in the editor as a fixed top-to-bottom stack for the full 5-row
                    // case; a recipe using fewer rows re-centers them around the same midpoint.
                    Vector2 pos = rows[i].icon.rectTransform.anchoredPosition;
                    pos.y = ((usedCount - 1) / 2f - i) * rowSpacing;
                    rows[i].icon.rectTransform.anchoredPosition = pos;
                    rows[i].icon.gameObject.SetActive(true);

                    if (rows[i].counterText != null)
                        rows[i].counterText.text = req.isRemoval
                            ? $"{rowStartingCount[i]}/{rowStartingCount[i] - req.requiredCount}"
                            : $"0/{req.requiredCount}";
                }
            }

            // The two card fronts are alternatives, not layers on the same face -- exactly one of
            // them is ever active. Memory gets CardFrontFace (with its timed reveal/hide below);
            // every other mission gets the single-ingredient display instead, and CardFrontFace
            // stays off rather than sitting there active-but-empty.
            if (singleDisplayRoot != null) singleDisplayRoot.gameObject.SetActive(!showAllRowsAtOnce);
            if (showAllRowsAtOnce)
            {
                ShowFace(true, animate: false);
            }
            else
            {
                if (cardFrontFace != null) cardFrontFace.SetActive(false);
                if (singleDisplayRoot != null)
                {
                    singleDisplayRoot.DOKill();
                    singleDisplayRoot.localScale = Vector3.one;
                }
                if (usedCount > 0) ApplySingleDisplay(0);
            }

            if (mission.LearningRule == LearningRule.Memory)
                memoryRoutine = StartCoroutine(MemoryRevealRoutine());
        }

        // current/required are already ordered to match mission.Requirements. For a removal
        // ingredient, current is "removed so far" -- shown instead as "remaining in the station /
        // target remaining" (e.g. 6 ice cubes down to a target of 4), which reads far more clearly
        // as "take some out" than a bare "0/2" removed-count would.
        public void UpdateRow(int index, int current, int required)
        {
            if (index < 0 || index >= rows.Length || rowInUse == null || !rowInUse[index]) return;

            bool increased = lastRowCurrent != null && current > lastRowCurrent[index];
            if (lastRowCurrent != null) lastRowCurrent[index] = current;

            if (showAllRowsAtOnce)
            {
                UpdateMultiRowVisual(index, current, required, increased);
                return;
            }

            // Single-display mode: only the currently-shown ingredient has live widgets to update --
            // everything else just keeps its bookkeeping (above) accurate for whenever its turn comes.
            if (index != currentDisplayIndex) return;

            if (ingredientCounterText != null)
            {
                ingredientCounterText.text = rowIsRemoval[index]
                    ? $"{rowStartingCount[index] - Mathf.Min(current, required)}/{rowStartingCount[index] - required}"
                    : $"{Mathf.Min(current, required)}/{required}";
            }

            if (ingredientProgressFill != null)
            {
                float progress = required > 0 ? Mathf.Clamp01((float)Mathf.Min(current, required) / required) : 0f;
                ingredientProgressFill.DOKill();
                ingredientProgressFill.DOFillAmount(progress, fillBarTweenDuration);
            }

            if (increased) PlayTickEffect(ingredientIcon, current >= required);

            if (current >= required) AdvanceToNextIncompleteRow();
        }

        private void UpdateMultiRowVisual(int index, int current, int required, bool increased)
        {
            if (rows[index]?.counterText == null) return;

            if (rowIsRemoval[index])
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

            if (rows[index].progressFill != null)
            {
                float progress = required > 0 ? Mathf.Clamp01((float)Mathf.Min(current, required) / required) : 0f;
                rows[index].progressFill.DOKill();
                rows[index].progressFill.DOFillAmount(progress, fillBarTweenDuration);
            }

            if (increased) PlayTickEffect(rows[index].icon, current >= required);
        }

        // Finds the next in-use ingredient after the one just finished and flips the card to it. If
        // there isn't one, this was the last ingredient -- stays showing it; CookingManager's own
        // win celebration takes over from here once the whole recipe is done.
        private void AdvanceToNextIncompleteRow()
        {
            for (int i = currentDisplayIndex + 1; i < rows.Length; i++)
            {
                if (!rowInUse[i]) continue;
                FlipToRequirement(i);
                return;
            }
        }

        // Same pinch-flip feel as ShowFace's front/back animation, but on the single shared display
        // root -- shrink it to nothing on its own X axis, swap in the next ingredient's name/icon/
        // counter/fill at that pinch point, then grow it back out.
        private void FlipToRequirement(int newIndex)
        {
            if (singleDisplayRoot == null)
            {
                ApplySingleDisplay(newIndex);
                return;
            }

            singleFlipSequence?.Kill();
            singleFlipSequence = DOTween.Sequence();
            singleFlipSequence.Append(singleDisplayRoot.DOScaleX(0f, flipHalfDuration).SetEase(Ease.InQuad));
            singleFlipSequence.AppendCallback(() => ApplySingleDisplay(newIndex));
            singleFlipSequence.Append(singleDisplayRoot.DOScaleX(1f, flipHalfDuration).SetEase(Ease.OutQuad));
        }

        // Populates the single shared display with requirement `index`'s data -- name, icon,
        // counter and fill all pulled from whatever bookkeeping has already accumulated for it
        // (lastRowCurrent), so an ingredient the player made progress on before it was ever shown
        // still displays correctly the moment the card flips to it.
        private void ApplySingleDisplay(int index)
        {
            currentDisplayIndex = index;
            var req = mission.Requirements[index];

            if (ingredientNameText != null) ingredientNameText.text = ToDisplayName(req.ingredientId);

            if (ingredientIcon != null)
            {
                ingredientIcon.DOKill();
                ingredientIcon.rectTransform.DOKill();
                ingredientIcon.rectTransform.localScale = Vector3.one;
                ingredientIcon.sprite = req.icon;
                ingredientIcon.color = Color.white;
            }

            int startingCount = rowStartingCount[index];
            int lastCurrent = lastRowCurrent[index];

            if (ingredientCounterText != null)
                ingredientCounterText.text = req.isRemoval
                    ? $"{startingCount - Mathf.Min(lastCurrent, req.requiredCount)}/{startingCount - req.requiredCount}"
                    : $"{Mathf.Min(lastCurrent, req.requiredCount)}/{req.requiredCount}";

            if (ingredientProgressFill != null)
            {
                ingredientProgressFill.DOKill();
                ingredientProgressFill.fillAmount = req.requiredCount > 0
                    ? Mathf.Clamp01((float)Mathf.Min(lastCurrent, req.requiredCount) / req.requiredCount)
                    : 0f;
            }
        }

        // "IceCube" -> "Ice Cube", "BottomBreadSlice" -> "Bottom Bread Slice" -- ingredientId is
        // camelCase with no dedicated display-name field of its own, so a space before every
        // internal capital is enough to read as a proper name.
        private static string ToDisplayName(string ingredientId)
        {
            if (string.IsNullOrEmpty(ingredientId)) return ingredientId;
            var sb = new StringBuilder();
            for (int i = 0; i < ingredientId.Length; i++)
            {
                char c = ingredientId[i];
                if (i > 0 && char.IsUpper(c)) sb.Append(' ');
                sb.Append(c);
            }
            return sb.ToString();
        }

        // A small punch every time an ingredient's count ticks up, and a bigger punch + a quick
        // green flash the moment it actually completes -- direct visual feedback tied to counting
        // correctly. Shared by both the Memory row list and the single-ingredient display.
        private void PlayTickEffect(Image icon, bool justCompleted)
        {
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

        // Reveals the recipe, then hides it for good once MemoryRevealSeconds is up -- no Peek, so
        // there's no way back to it once it's hidden.
        private IEnumerator MemoryRevealRoutine()
        {
            yield return new WaitForSeconds(mission.MemoryRevealSeconds);
            ShowFace(false);
            memoryRoutine = null;
        }

        // Hides/reveals the recipe by pinching the front face shut (scale X to 0) then back open
        // again -- no separate "back" object needed, hidden just means nothing is shown while it's
        // collapsed. animate=false (Setup's initial reset) snaps straight to the target state instead.
        private void ShowFace(bool front, bool animate = true)
        {
            flipSequence?.Kill();
            if (cardFrontFace == null) return;

            if (!animate || cardFrontRect == null)
            {
                cardFrontFace.SetActive(front);
                if (cardFrontRect != null) cardFrontRect.localScale = Vector3.one;
                return;
            }

            flipSequence = DOTween.Sequence();
            if (front)
            {
                cardFrontFace.SetActive(true);
                cardFrontRect.localScale = new Vector3(0f, 1f, 1f);
                flipSequence.Append(cardFrontRect.DOScaleX(1f, flipHalfDuration * 2f).SetEase(Ease.OutQuad));
            }
            else
            {
                flipSequence.Append(cardFrontRect.DOScaleX(0f, flipHalfDuration * 2f).SetEase(Ease.InQuad));
                flipSequence.AppendCallback(() => cardFrontFace.SetActive(false));
            }
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
            singleFlipSequence?.Kill();
        }
    }
}
