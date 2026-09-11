using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace tmkoc.lunchforbuilders
{
    // Orchestrates one mission (one recipe) of the "drag ingredients, count them, serve the dish"
    // gameplay loop -- the Count & Cook equivalent of BuildABot's RobotAssemblyManager. Every
    // ingredient-correctness decision lives here; IngredientController/PantrySlot/PreparationStation
    // are all deliberately dumb about game rules, exactly like RobotPartController/RobotZone were.
    public class CookingManager : MonoBehaviour
    {
        [Header("Manager Reference")]
        [SerializeField] private GameManager gameManager;

        [Header("Scene References")]
        [Tooltip("Parent of everything gameplay-visual (pantry, station, recipe card). Starts inactive in the scene; shown the moment the first mission starts, whether that's right after the storyboard or immediately if there's no storyboard.")]
        [SerializeField] private GameObject gameplayRoot;
        [SerializeField] private PreparationStation station;
        [SerializeField] private PantrySlot[] pantrySlots;
        [SerializeField] private RecipeCardController recipeCard;
        [SerializeField] private RectTransform dragLayer;
        [SerializeField] private CharacterReactionController characterReaction;
        [Tooltip("MM:SS countdown display. Starts once the level-start animation finishes, stops the moment the recipe is complete or time runs out.")]
        [SerializeField] private TMP_Text timerText;

        [Header("Pacing")]
        [Tooltip("How long the gratification beat holds (dish swaps to its completed sprite) before the mission is reported complete -- fires automatically the instant the recipe is finished, no button press.")]
        [SerializeField] private float serveCompleteDelay = 1.5f;

        [Header("Intro Reveal")]
        [Tooltip("How long the Recipe Card (sliding in from off-screen left) and the Preparation Station's content (sliding up from off-screen bottom) take to reach their resting position -- both run at the same time.")]
        [SerializeField] private float introSlideDuration = 1f;
        [Tooltip("Delay before each pantry ingredient scales in, one after another, once the slide-in finishes.")]
        [SerializeField] private float draggableStaggerDelay = 0.1f;
        [Tooltip("How long each individual pantry ingredient's own scale-up (0 -> 1) takes.")]
        [SerializeField] private float draggableScaleInDuration = 0.3f;

        private MissionRecipeData currentMission;
        private int currentMissionIndex;
        private int currentSequenceStep;
        private readonly Dictionary<string, int> removedSoFar = new Dictionary<string, int>();

        // Only the very first hint shown in the whole play session is immediate (the player hasn't
        // been taught the drag yet); every hint after that -- including later ones within mission
        // 1 itself -- waits out the normal idle delay. Same idea as TutorialController's
        // isFirstHintEver in BuildABot.
        private bool isFirstHintEver = true;
        // Guards the auto-serve from firing more than once per mission -- RaiseProgress runs on
        // every resolved drop, and the recipe stays "complete" for every one of them after the first.
        private bool hasCompletedThisMission;

        private RectTransform recipeCardRect;
        private RectTransform canvasRect;
        private Vector2 recipeCardRestPos;
        private Vector2 stationRestPos;
        private Coroutine introRoutine;
        private Coroutine timerRoutine;

        private void Awake()
        {
            foreach (var slot in pantrySlots) slot.Init(this, dragLayer);
            if (recipeCard != null) recipeCard.OnPeekUsed += HandlePeekUsed;

            // Rest positions and the canvas they're measured against are captured once, up front --
            // every later reveal computes its off-screen start from these, never from wherever the
            // element happens to be mid-animation.
            recipeCardRect = recipeCard != null ? recipeCard.GetComponent<RectTransform>() : null;
            canvasRect = station.ContentAnchor.GetComponentInParent<Canvas>()?.GetComponent<RectTransform>();
            if (recipeCardRect != null) recipeCardRestPos = recipeCardRect.anchoredPosition;
            stationRestPos = station.ContentAnchor.anchoredPosition;
        }

        public void StartMission(MissionRecipeData mission, int missionIndex)
        {
            currentMission = mission;
            currentMissionIndex = missionIndex;
            currentSequenceStep = mission.LearningRule == LearningRule.OrderAndCounting ? 0 : -1;
            removedSoFar.Clear();
            hasCompletedThisMission = false;
            StopTimer();

            if (gameplayRoot != null) gameplayRoot.SetActive(true);

            if (introRoutine != null) StopCoroutine(introRoutine);
            introRoutine = StartCoroutine(IntroRevealRoutine());
        }

        // Recipe Card slides in from the left, the Preparation Station's content slides in from the
        // bottom (both at once), then the pantry ingredients pop in one by one, and only once all of
        // that has finished does the mission actually become interactive.
        private IEnumerator IntroRevealRoutine()
        {
            gameManager.TutorialManager?.CancelHint();

            // Everything is prepared while off-screen/invisible, so nothing visibly "pops" the
            // moment it slides or scales into view.
            station.Clear();
            station.SetContentSprite(currentMission.StationIcon);
            SeedStartingIngredients();
            recipeCard?.Setup(currentMission);
            characterReaction?.Setup(currentMission);
            UpdatePantryVisibility();

            foreach (var slot in pantrySlots)
            {
                slot.SetInteractable(false);
                if (!slot.gameObject.activeSelf) continue;
                slot.transform.DOKill();
                slot.transform.localScale = Vector3.zero;
            }

            if (recipeCardRect != null)
            {
                recipeCardRect.DOKill();
                recipeCardRect.anchoredPosition = GetOffscreenLeftPos(recipeCardRect, recipeCardRestPos);
                recipeCardRect.DOAnchorPosX(recipeCardRestPos.x, introSlideDuration).SetEase(Ease.OutBack);
            }
            // X stays wherever it was set in the editor; only Y is per-mission (different container
            // art -- jug vs plate vs blender -- can sit at a different height).
            Vector2 stationTargetPos = new Vector2(stationRestPos.x, currentMission.ContentAnchorRestY);
            station.ContentAnchor.DOKill();
            station.ContentAnchor.anchoredPosition = GetOffscreenBottomPos(station.ContentAnchor, stationTargetPos);
            station.ContentAnchor.DOAnchorPosY(stationTargetPos.y, introSlideDuration).SetEase(Ease.OutBack);

            yield return new WaitForSeconds(introSlideDuration);
            yield return RevealPantrySlotsRoutine();

            UpdatePantryInteractivity();
            characterReaction?.ShowHungry();
            StartTimer();
            RaiseProgress();

            gameManager.InvokeMissionStarted(currentMissionIndex);
            introRoutine = null;
        }

        private void StartTimer()
        {
            StopTimer();
            timerRoutine = StartCoroutine(TimerRoutine());
        }

        private void StopTimer()
        {
            if (timerRoutine != null)
            {
                StopCoroutine(timerRoutine);
                timerRoutine = null;
            }
        }

        private IEnumerator TimerRoutine()
        {
            float remaining = currentMission.TimeInSeconds;
            UpdateTimerText(remaining);
            while (remaining > 0f)
            {
                yield return null;
                remaining -= Time.deltaTime;
                UpdateTimerText(remaining);
            }
            timerRoutine = null;
            gameManager.InvokeLevelLose();
        }

        private void UpdateTimerText(float secondsRemaining)
        {
            if (timerText == null) return;
            int wholeSeconds = Mathf.CeilToInt(Mathf.Max(secondsRemaining, 0f));
            timerText.text = $"{wholeSeconds / 60:00}:{wholeSeconds % 60:00}";
        }

        private IEnumerator RevealPantrySlotsRoutine()
        {
            float lastTweenDuration = 0f;
            foreach (var slot in pantrySlots)
            {
                if (!slot.gameObject.activeSelf) continue;
                slot.transform.DOScale(1f, draggableScaleInDuration).SetEase(Ease.OutBack);
                lastTweenDuration = draggableScaleInDuration;
                yield return new WaitForSeconds(draggableStaggerDelay);
            }
            // The last slot's own tween keeps running after its stagger delay -- wait out whatever's left of it.
            float remaining = lastTweenDuration - draggableStaggerDelay;
            if (remaining > 0f) yield return new WaitForSeconds(remaining);
        }

        // Fully off the left edge regardless of resolution/aspect: half the canvas's own width plus
        // half the target's own width, so it clears the screen no matter how big either one is.
        private Vector2 GetOffscreenLeftPos(RectTransform target, Vector2 restPos)
        {
            if (canvasRect == null) return restPos;
            float offsetX = canvasRect.rect.width * 0.5f + target.rect.width * 0.5f;
            return restPos + new Vector2(-offsetX, 0f);
        }

        private Vector2 GetOffscreenBottomPos(RectTransform target, Vector2 restPos)
        {
            if (canvasRect == null) return restPos;
            float offsetY = canvasRect.rect.height * 0.5f + target.rect.height * 0.5f;
            return restPos + new Vector2(0f, -offsetY);
        }

        private void SeedStartingIngredients()
        {
            if (currentMission.StartingIngredients == null) return;
            foreach (var starting in currentMission.StartingIngredients)
            {
                var slot = FindPantrySlot(starting.ingredientId);
                if (slot == null) continue;
                for (int i = 0; i < starting.requiredCount; i++)
                {
                    var token = slot.SpawnPlacedToken(this, dragLayer);
                    if (token == null) continue;
                    token.PlaceInstantly(station.ContentAnchor);
                    station.RegisterPlaced(starting.ingredientId, token);
                }
            }
        }

        // The instant the player touches ANYTHING, the current hint hand is dismissed -- a fresh one
        // (pointed at whatever the new next-correct-action is) gets scheduled the next time
        // RefreshTutorialHint runs, which happens right after this interaction resolves.
        public void NotifyInteractionStarted(IngredientController token) => gameManager.TutorialManager?.CancelHint();

        public void UpdateHoverFeedback(IngredientController token, PointerEventData eventData)
        {
            Camera cam = eventData.pressEventCamera;
            bool over = station.ContainsScreenPoint(eventData.position, cam);
            bool valid = token.Mode == IngredientDragMode.AddToStation
                ? FindActiveRequirement(token.IngredientId) != null
                : FindRemovalRequirement(token.IngredientId) != null;
            station.SetHoverGlow(over, over && valid);
        }

        public void ResolveDrop(IngredientController token, PointerEventData eventData)
        {
            Camera cam = eventData.pressEventCamera;
            bool overStation = station.ContainsScreenPoint(eventData.position, cam);
            station.SetHoverGlow(false, false);

            bool success = token.Mode == IngredientDragMode.AddToStation
                ? ResolveAdd(token, overStation)
                : ResolveRemove(token, overStation);

            if (!success) characterReaction?.ShowSad();

            gameManager.InvokeIngredientResolved(token.IngredientId, success);
        }

        private bool ResolveAdd(IngredientController token, bool overStation)
        {
            var requirement = overStation ? FindActiveRequirement(token.IngredientId) : null;
            bool success = requirement != null && station.GetPlacedCount(token.IngredientId) < requirement.requiredCount;

            if (success)
            {
                station.RegisterPlaced(token.IngredientId, token);
                token.SnapIntoStation(station.ContentAnchor);
                AdvanceSequenceIfStepComplete(requirement);
                RaiseProgress();
            }
            else
            {
                token.ReturnToRest();
            }
            return success;
        }

        private bool ResolveRemove(IngredientController token, bool overStation)
        {
            var requirement = !overStation ? FindRemovalRequirement(token.IngredientId) : null;
            int removed = removedSoFar.TryGetValue(token.IngredientId, out int r) ? r : 0;
            bool success = requirement != null && removed < requirement.requiredCount;

            if (success)
            {
                removedSoFar[token.IngredientId] = removed + 1;
                station.RemoveSpecificToken(token.IngredientId, token);
                token.PlayRemovedAndDestroy();
                RaiseProgress();
            }
            else
            {
                token.ReturnToRest();
            }
            return success;
        }

        // Only ever returns a requirement whose ingredient is being ADDED and, for an ordered
        // (OrderAndCounting) mission, only when it's the current step -- everything else (wrong
        // ingredient, wrong step, or already at quota since a satisfied requirement stops matching
        // once GetPlacedCount reaches requiredCount) returns null, which ResolveAdd treats as a bounce.
        private IngredientRequirement FindActiveRequirement(string ingredientId)
        {
            foreach (var req in currentMission.Requirements)
            {
                if (req.isRemoval || req.ingredientId != ingredientId) continue;
                if (req.sequenceOrder >= 0 && req.sequenceOrder != currentSequenceStep) continue;
                if (station.GetPlacedCount(ingredientId) >= req.requiredCount) continue;
                return req;
            }
            return null;
        }

        private IngredientRequirement FindRemovalRequirement(string ingredientId)
        {
            foreach (var req in currentMission.Requirements)
                if (req.isRemoval && req.ingredientId == ingredientId) return req;
            return null;
        }

        private void AdvanceSequenceIfStepComplete(IngredientRequirement requirement)
        {
            if (requirement.sequenceOrder < 0) return;
            if (station.GetPlacedCount(requirement.ingredientId) < requirement.requiredCount) return;

            currentSequenceStep = requirement.sequenceOrder + 1;
            UpdatePantryInteractivity();
        }

        // All 5 missions share one scene/pantry tray, so a pantry slot must be hidden entirely
        // whenever its ingredient isn't part of the CURRENT mission's add-requirements -- e.g. the
        // Ice Cube slot used to add 6 cubes in Mission 1 has no business appearing in Mission 4,
        // where Ice Cube only ever appears as a removal requirement (the 6 starting cubes are
        // seeded directly into the station, never dragged in from this pantry).
        private void UpdatePantryVisibility()
        {
            foreach (var slot in pantrySlots)
            {
                bool usedThisMission = false;
                foreach (var req in currentMission.Requirements)
                {
                    if (!req.isRemoval && req.ingredientId == slot.IngredientId) { usedThisMission = true; break; }
                }
                slot.gameObject.SetActive(usedThisMission);
            }
        }

        // Only meaningful for OrderAndCounting missions -- every other mission leaves every pantry
        // slot interactable throughout (over-adding is allowed; it just bounces back, matching the
        // GDD's "Too-many Ingredient bounce" / "Extra Ingredient Boing" feedback).
        private void UpdatePantryInteractivity()
        {
            bool isOrdered = currentMission.LearningRule == LearningRule.OrderAndCounting;
            foreach (var slot in pantrySlots)
            {
                if (!isOrdered) { slot.SetInteractable(true); continue; }
                slot.SetInteractable(FindActiveRequirement(slot.IngredientId) != null);
            }
        }

        private void RaiseProgress()
        {
            int placedTotal = 0;
            int requiredTotal = 0;
            var requirements = currentMission.Requirements;
            for (int i = 0; i < requirements.Length; i++)
            {
                var req = requirements[i];
                int current = req.isRemoval
                    ? (removedSoFar.TryGetValue(req.ingredientId, out int r) ? r : 0)
                    : station.GetPlacedCount(req.ingredientId);
                current = Mathf.Min(current, req.requiredCount);
                placedTotal += current;
                requiredTotal += req.requiredCount;
                recipeCard?.UpdateRow(i, current, req.requiredCount);
            }
            gameManager.InvokeRecipeProgress(placedTotal, requiredTotal);

            // Completion is automatic now -- the instant every requirement is satisfied, serve the
            // dish and end the level, no button press needed. Guarded so it only fires once even
            // though RaiseProgress keeps running (e.g. a stray drop attempt) after that.
            if (!hasCompletedThisMission && IsRecipeComplete())
            {
                hasCompletedThisMission = true;
                AutoServe();
                return;
            }

            RefreshTutorialHint();
        }

        // Swaps in the finished-dish sprite and starts the gratification hold -- the same beat that
        // used to wait for a Serve tap, now triggered automatically the moment the recipe is done.
        private void AutoServe()
        {
            StopTimer();
            gameManager.TutorialManager?.CancelHint();
            characterReaction?.ShowHappy();
            station.SetContentSprite(currentMission.CompletedRecipeSprite);
            // The individual ingredient icons were only ever a stand-in for "this dish is being
            // built" -- once it's done, the completed-dish sprite replaces them, so the leftover
            // icons need to stop sitting on top of it.
            station.HidePlacedTokens();
            StartCoroutine(ServeCompleteRoutine());
        }

        // Works out the single next correct action -- a removal outstanding anywhere takes priority
        // (there's no fixed ordering between add/remove requirements in the data), then the first
        // unmet add requirement respecting Mission 3's sequence gate. Called after every mission
        // start and every resolved drop (as long as the recipe isn't already complete -- see
        // RaiseProgress), so the hint always tracks the current game state rather than going stale.
        private void RefreshTutorialHint()
        {
            var tutorial = gameManager.TutorialManager;
            if (tutorial == null || currentMission == null) return;

            bool immediate = isFirstHintEver;

            foreach (var req in currentMission.Requirements)
            {
                if (!req.isRemoval) continue;
                int removed = removedSoFar.TryGetValue(req.ingredientId, out int r) ? r : 0;
                if (removed >= req.requiredCount) continue;
                var placedToken = station.PeekPlacedToken(req.ingredientId);
                if (placedToken == null) continue;
                tutorial.ShowRemoveHint(placedToken.GetComponent<RectTransform>(), immediate);
                isFirstHintEver = false;
                return;
            }

            foreach (var req in currentMission.Requirements)
            {
                if (req.isRemoval) continue;
                if (station.GetPlacedCount(req.ingredientId) >= req.requiredCount) continue;
                if (req.sequenceOrder >= 0 && req.sequenceOrder != currentSequenceStep) continue;
                var slot = FindPantrySlot(req.ingredientId);
                if (slot == null || !slot.gameObject.activeSelf) continue;
                tutorial.ShowAddHint(slot.GetComponent<RectTransform>(), station.HintTargetArea, immediate);
                isFirstHintEver = false;
                return;
            }

            tutorial.CancelHint();
        }

        private bool IsRecipeComplete()
        {
            foreach (var req in currentMission.Requirements)
            {
                int current = req.isRemoval
                    ? (removedSoFar.TryGetValue(req.ingredientId, out int r) ? r : 0)
                    : station.GetPlacedCount(req.ingredientId);
                if (current < req.requiredCount) return false;
            }
            return true;
        }

        // Holds on the gratification beat, then reports the mission done -- LevelManager reacts by
        // showing the existing win panel (EndPanelScript.ShowWin()) as the "Mission Complete" beat,
        // whether this was mission 1 or mission 5.
        private IEnumerator ServeCompleteRoutine()
        {
            yield return new WaitForSeconds(serveCompleteDelay);
            gameManager.InvokeMissionComplete(currentMissionIndex);
        }

        private void HandlePeekUsed() => gameManager.InvokePeekUsed(currentMissionIndex);

        private PantrySlot FindPantrySlot(string ingredientId)
        {
            foreach (var slot in pantrySlots)
                if (slot.IngredientId == ingredientId) return slot;
            return null;
        }

        private void OnDestroy()
        {
            if (recipeCard != null) recipeCard.OnPeekUsed -= HandlePeekUsed;
        }
    }
}
