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
        [Tooltip("Even if a recipe only needs one or two ingredient types, at least this many pantry slots stay visible -- extra decoy ingredients (picked randomly from whatever's left) mean the player has to actually read the recipe instead of just clearing out the only options on the shelf.")]
        [SerializeField] private int minPantrySlotsVisible = 5;
        [SerializeField] private RecipeCardController recipeCard;
        [SerializeField] private RectTransform dragLayer;
        [SerializeField] private CharacterReactionController characterReaction;
        [Tooltip("MM:SS countdown display. Starts once the level-start animation finishes, stops the moment the recipe is complete or time runs out.")]
        [SerializeField] private TMP_Text timerText;

        [Header("Pacing")]
        [Tooltip("How long the gratification beat holds (dish swaps to its completed sprite) before the mission is reported complete -- fires automatically the instant the recipe is finished, no button press.")]
        [SerializeField] private float serveCompleteDelay = 1.5f;
        [Tooltip("Floor on how long the win/lose panel waits before popping up, regardless of how short the outro/lose VO line is (or if it's missing entirely) -- keeps the celebration/fail beat from feeling rushed.")]
        [SerializeField] private float minPanelPopupDelay = 3f;

        [Header("Intro Reveal")]
        [Tooltip("How long the Recipe Card (sliding in from off-screen left) and the Preparation Station's content (sliding up from off-screen bottom) take to reach their resting position -- both run at the same time.")]
        [SerializeField] private float introSlideDuration = 1f;
        [Tooltip("Delay before each pantry ingredient scales in, one after another, once the slide-in finishes.")]
        [SerializeField] private float draggableStaggerDelay = 0.1f;
        [Tooltip("How long each individual pantry ingredient's own scale-up (0 -> 1) takes.")]
        [SerializeField] private float draggableScaleInDuration = 0.3f;

        [Header("Character Mood")]
        [Tooltip("How long the player can go without touching anything before a Sad particle burst nudges them -- repeats for as long as they stay idle.")]
        [SerializeField] private float idleParticleDelay = 6f;
        [Tooltip("How long the player can go without touching anything before the character's sprite reverts to Hungry.")]
        [SerializeField] private float idleSpriteRevertDelay = 3f;

        [Header("Win Celebration")]
        [Tooltip("Punch-scale strength for the character portrait and the completed-dish icon, played once the outro line finishes.")]
        [SerializeField] private float winPunchScale = 0.25f;
        [SerializeField] private float winPunchDuration = 0.4f;
        [Tooltip("How long the confetti gets to burst on screen before the win panel slides up and covers it.")]
        [SerializeField] private float confettiLeadTime = 0.6f;

        [Header("Timer Urgency")]
        [Tooltip("Once the countdown drops to/below this many seconds, the timer text pulses and turns red -- a wordless 'hurry up' cue.")]
        [SerializeField] private float timerUrgentThreshold = 10f;
        [SerializeField] private Color timerUrgentColor = new Color(1f, 0.3f, 0.3f);
        [SerializeField] private float timerPulseScale = 1.15f;
        [SerializeField] private float timerPulseDuration = 0.4f;

        private MissionRecipeData currentMission;
        private int currentMissionIndex;
        private int currentSequenceStep;
        private readonly Dictionary<string, int> removedSoFar = new Dictionary<string, int>();

        // Only the very first hint shown in the whole play session is immediate (the player hasn't
        // been taught the drag yet); every hint after that -- including later ones within mission
        // 1 itself -- waits out the normal idle delay. Same idea as TutorialController's
        // isFirstHintEver in BuildABot.
        private bool isFirstHintEver = true;
        // Dragging something OUT of the station (Mission 4's ice cubes) is a brand new interaction
        // the player has never needed before, so it gets the same "don't make them wait" treatment
        // as the very first hint of the whole session, the first time it's ever needed -- otherwise
        // they'd have to sit idle for the normal delay before finding out removal is even possible.
        private bool isFirstRemovalHintEver = true;
        // Guards the auto-serve from firing more than once per mission -- RaiseProgress runs on
        // every resolved drop, and the recipe stays "complete" for every one of them after the first.
        private bool hasCompletedThisMission;

        private RectTransform recipeCardRect;
        private RectTransform characterRect;
        private RectTransform canvasRect;
        private Vector2 recipeCardRestPos;
        private Vector2 stationRestPos;
        private Vector2 characterRestPos;
        private Coroutine introRoutine;
        private Coroutine timerRoutine;
        private Coroutine idleParticleRoutine;
        private Coroutine idleSpriteRevertRoutine;
        private Tween timerPulseTween;
        private Color timerNormalColor;
        private bool timerUrgentActive;
        // Which of PreparationStation's 3 drop-point RectTransforms the current mission's ingredients
        // fall onto and pile up from -- resolved once per mission start from MissionRecipeData.ContainerBoundary.
        private RectTransform activeDropPoint;

        private void Awake()
        {
            foreach (var slot in pantrySlots) slot.Init(this, dragLayer);

            // Rest positions and the canvas they're measured against are captured once, up front --
            // every later reveal computes its off-screen start from these, never from wherever the
            // element happens to be mid-animation.
            recipeCardRect = recipeCard != null ? recipeCard.GetComponent<RectTransform>() : null;
            characterRect = characterReaction != null ? characterReaction.ImageRectTransform : null;
            canvasRect = station.ContentAnchor.GetComponentInParent<Canvas>()?.GetComponent<RectTransform>();
            if (recipeCardRect != null) recipeCardRestPos = recipeCardRect.anchoredPosition;
            if (characterRect != null) characterRestPos = characterRect.anchoredPosition;
            stationRestPos = station.ContentAnchor.anchoredPosition;
            if (timerText != null) timerNormalColor = timerText.color;
        }

        public void StartMission(MissionRecipeData mission, int missionIndex)
        {
            currentMission = mission;
            currentMissionIndex = missionIndex;
            currentSequenceStep = mission.LearningRule == LearningRule.OrderAndCounting ? 0 : -1;
            removedSoFar.Clear();
            hasCompletedThisMission = false;
            StopTimer();
            StopIdleParticleTimer();
            StopIdleSpriteRevertTimer();

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
            station.SetContentAnchorSize(currentMission.ContentAnchorSize);
            activeDropPoint = station.GetDropPoint(currentMission.ContainerBoundary);
            SeedStartingIngredients();
            recipeCard?.Setup(currentMission);
            characterReaction?.Setup(currentMission);
            UpdatePantryVisibility();

            // Character moves off-screen and gets its new sprite BEFORE anything is visible again --
            // previously the sprite only swapped at the very end of this routine, so the player saw
            // the previous mission's portrait sitting there for the whole intro before it popped.
            if (characterRect != null)
            {
                characterReaction?.PauseIdleBob();
                characterRect.anchoredPosition = GetOffscreenLeftPos(characterRect, characterRestPos);
            }
            characterReaction?.SetSpriteSilently(CharacterMood.Hungry);

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
            if (characterRect != null)
            {
                // SetSpriteSilently above deliberately skips the shake, so there's nothing fighting
                // this slide-in -- the idle bob just resumes once the character reaches rest position.
                characterRect.DOAnchorPosX(characterRestPos.x, introSlideDuration).SetEase(Ease.OutBack)
                    .OnComplete(() => characterReaction?.ResumeIdleBob());
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
            characterReaction?.PlaySadBurst();
            gameManager.SoundManager?.PlayMissionIntro(currentMissionIndex);
            StartTimer();
            RestartIdleParticleTimer();
            RestartIdleSpriteRevertTimer();
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
            StopTimerUrgency();
        }

        // Once the countdown crosses the urgency threshold, the text pulses red for the rest of the
        // mission -- a continuous loop rather than a one-shot flash, so it keeps reading as "still
        // urgent" for as long as it stays true.
        private void StartTimerUrgency()
        {
            if (timerUrgentActive || timerText == null) return;
            timerUrgentActive = true;
            timerText.color = timerUrgentColor;
            timerPulseTween?.Kill();
            timerPulseTween = timerText.rectTransform
                .DOScale(timerPulseScale, timerPulseDuration)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo);
        }

        private void StopTimerUrgency()
        {
            timerUrgentActive = false;
            timerPulseTween?.Kill();
            timerPulseTween = null;
            if (timerText != null)
            {
                timerText.color = timerNormalColor;
                timerText.rectTransform.localScale = Vector3.one;
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
            // The mission is over (lost) -- nothing should still be draggable while the lose VO/
            // panel-popup delay plays out below.
            DisableAllPantrySlots();
            // The lose panel is about to slide up -- stop any reaction burst still fading out so it
            // can't render on top of it (its particle renderer sorts above the UI otherwise).
            characterReaction?.DisableReactionParticles();
            StartCoroutine(LoseRoutine());
        }

        // Pantry slots otherwise stay interactable for the rest of the mission (only Mission 3's
        // ordering ever turns some of them off) -- called once the mission is over (won or lost) so
        // nothing can still be dragged while the celebration/fail beat and panel popup play out.
        private void DisableAllPantrySlots()
        {
            foreach (var slot in pantrySlots) slot.SetInteractable(false);
        }

        // Same idea as the win celebration's own wait -- give the "time's up" line room to finish
        // (or, if it's short or missing, at least minPanelPopupDelay) before LevelManager reacts to
        // InvokeLevelLose() by sliding the lose panel up, so it never pops up mid-sentence or feels rushed.
        private IEnumerator LoseRoutine()
        {
            float len = gameManager.SoundManager != null ? gameManager.SoundManager.PlayMissionLose(currentMissionIndex) : -1f;
            yield return new WaitForSeconds(Mathf.Max(len, minPanelPopupDelay));
            gameManager.InvokeLevelLose();
        }

        private void UpdateTimerText(float secondsRemaining)
        {
            if (timerText == null) return;
            int wholeSeconds = Mathf.CeilToInt(Mathf.Max(secondsRemaining, 0f));
            timerText.text = $"{wholeSeconds / 60:00}:{wholeSeconds % 60:00}";

            if (secondsRemaining <= timerUrgentThreshold) StartTimerUrgency();
        }

        // Repeats for as long as the player goes untouched -- reset (via NotifyInteractionStarted)
        // the instant they touch anything, so this only ever fires while genuinely idle.
        private void RestartIdleParticleTimer()
        {
            StopIdleParticleTimer();
            idleParticleRoutine = StartCoroutine(IdleParticleRoutine());
        }

        private void StopIdleParticleTimer()
        {
            if (idleParticleRoutine != null)
            {
                StopCoroutine(idleParticleRoutine);
                idleParticleRoutine = null;
            }
        }

        private IEnumerator IdleParticleRoutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(idleParticleDelay);
                characterReaction?.PlaySadBurst();
                gameManager.SoundManager?.PlayIdleNudge();
            }
        }

        // Reverts the character sprite to Hungry after a few seconds of no interaction -- separate
        // from the (longer, repeating) idle particle nudge above. One-shot: once it fires the
        // sprite just stays Hungry, no need to keep re-firing while still idle.
        private void RestartIdleSpriteRevertTimer()
        {
            StopIdleSpriteRevertTimer();
            idleSpriteRevertRoutine = StartCoroutine(IdleSpriteRevertRoutine());
        }

        private void StopIdleSpriteRevertTimer()
        {
            if (idleSpriteRevertRoutine != null)
            {
                StopCoroutine(idleSpriteRevertRoutine);
                idleSpriteRevertRoutine = null;
            }
        }

        private IEnumerator IdleSpriteRevertRoutine()
        {
            yield return new WaitForSeconds(idleSpriteRevertDelay);
            idleSpriteRevertRoutine = null;
            characterReaction?.SetSprite(CharacterMood.Hungry);
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
                    float startingScale = currentMission.PlacedScaleMultiplier * starting.placedScaleMultiplier;
                    token.PlaceInstantly(activeDropPoint, station.TotalPlacedCount, currentMission.StackVertically, startingScale);
                    station.RegisterPlaced(starting.ingredientId, token);
                }
            }
        }

        // The instant the player touches ANYTHING, the current hint hand is dismissed -- a fresh one
        // (pointed at whatever the new next-correct-action is) gets scheduled the next time
        // RefreshTutorialHint runs, which happens right after this interaction resolves. Also counts
        // as "not idle", so both idle timers (the Sad-particle nudge and the sprite-revert-to-Hungry)
        // wait out a fresh delay from here.
        public void NotifyInteractionStarted(IngredientController token)
        {
            gameManager.TutorialManager?.CancelHint();
            gameManager.SoundManager?.PlayIngredientName(token.IngredientId);
            RestartIdleParticleTimer();
            RestartIdleSpriteRevertTimer();
        }

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

            // RaiseProgress() runs synchronously inside ResolveAdd/ResolveRemove below, and on the
            // drop that finishes the recipe it already starts AutoServe -> the mission outro VO
            // line (on the same shared RuntimeAudioLoader audio source as the reinforcement bark
            // played just underneath). Capturing the before/after state lets us skip that bark when
            // this exact drop is what completed the mission, so it doesn't Stop() the outro that
            // just started playing.
            bool wasCompletedBefore = hasCompletedThisMission;
            bool success = token.Mode == IngredientDragMode.AddToStation
                ? ResolveAdd(token, overStation)
                : ResolveRemove(token, overStation);
            bool justCompletedMission = !wasCompletedBefore && hasCompletedThisMission;

            if (success)
            {
                characterReaction?.SetSprite(CharacterMood.Happy);
                characterReaction?.PlayHappyBurst();
                gameManager.SoundManager?.PlaySFX(sfxEnum.Correct);           
            }
            else
            {
                characterReaction?.SetSprite(CharacterMood.Sad);
                characterReaction?.PlaySadBurst();
                gameManager.SoundManager?.PlaySFX(sfxEnum.Incorrect);
                // Memory missions only (a no-op everywhere else) -- a wrong drop briefly re-reveals
                // the recipe as a free reminder, instead of the mistake just being unexplained.
                recipeCard?.NotifyIncorrectDrop();
            }

            gameManager.InvokeIngredientResolved(token.IngredientId, success);
        }

        private bool ResolveAdd(IngredientController token, bool overStation)
        {
            var requirement = overStation ? FindActiveRequirement(token.IngredientId) : null;
            bool success = requirement != null && station.GetPlacedCount(token.IngredientId) < requirement.requiredCount;

            if (success)
            {
                int stackIndex = station.TotalPlacedCount;
                station.RegisterPlaced(token.IngredientId, token);
                float placedScale = currentMission.PlacedScaleMultiplier * requirement.placedScaleMultiplier;
                token.SnapIntoStation(activeDropPoint, stackIndex, currentMission.StackVertically, placedScale);
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
        // seeded directly into the station, never dragged in from this pantry). On top of the
        // actually-needed slots, a handful of decoy ingredients stay visible too (up to
        // minPantrySlotsVisible total) -- a shelf with only the exact right answers on it doesn't
        // ask the player to identify anything. A decoy that gets dragged in just bounces back like
        // any other wrong drop; nothing extra needed for that part.
        private void UpdatePantryVisibility()
        {
            var decoys = new List<PantrySlot>();
            int requiredCount = 0;
            foreach (var slot in pantrySlots)
            {
                bool usedThisMission = false;
                foreach (var req in currentMission.Requirements)
                {
                    if (!req.isRemoval && req.ingredientId == slot.IngredientId) { usedThisMission = true; break; }
                }
                slot.gameObject.SetActive(usedThisMission);
                if (usedThisMission) requiredCount++;
                else decoys.Add(slot);
            }

            int decoysNeeded = Mathf.Max(0, minPantrySlotsVisible - requiredCount);
            ShuffleInPlace(decoys);
            for (int i = 0; i < decoysNeeded && i < decoys.Count; i++)
                decoys[i].gameObject.SetActive(true);
        }

        private static void ShuffleInPlace(List<PantrySlot> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
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
            StopIdleParticleTimer();
            StopIdleSpriteRevertTimer();
            gameManager.TutorialManager?.CancelHint();
            // The recipe is done -- nothing should still be draggable from here. Placed
            // RemoveFromStation tokens lose their drag along with everything else once
            // HidePlacedTokens() below deactivates them (Unity doesn't fire drag events on an
            // inactive GameObject); pantry slots need an explicit push (see DisableAllPantrySlots).
            DisableAllPantrySlots();
            characterReaction?.SetSprite(CharacterMood.Happy);
            characterReaction?.PlayHappyBurst();
            station.SetContentSprite(currentMission.CompletedRecipeSprite);
            // The individual ingredient icons were only ever a stand-in for "this dish is being
            // built" -- once it's done, the completed-dish sprite replaces them, so the leftover
            // icons need to stop sitting on top of it.
            station.HidePlacedTokens();

            // Win punch + confetti fire right here, the instant the dish swaps to its completed
            // sprite -- this IS the moment of victory, not something that should wait for the
            // outro line to finish first.
            PlayWinPunchFeedback();
            gameManager.EndPanelScript?.PlayConfetti();

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
                tutorial.ShowRemoveHint(placedToken.GetComponent<RectTransform>(), immediate || isFirstRemovalHintEver);
                isFirstHintEver = false;
                isFirstRemovalHintEver = false;
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

        // The win punch and confetti already fired in AutoServe, right as the dish swapped to its
        // completed sprite. This just holds the celebration on screen -- at least as long as the
        // outro line, the win punch + confetti need to actually be seen, and minPanelPopupDelay's
        // own floor -- before reporting the mission complete, which is what LevelManager reacts to
        // by sliding up the win panel.
        private IEnumerator ServeCompleteRoutine()
        {
            float len = gameManager.SoundManager != null ? gameManager.SoundManager.PlayMissionOutro(currentMissionIndex) : -1f;
            float minDelay = Mathf.Max(serveCompleteDelay, minPanelPopupDelay, winPunchDuration + confettiLeadTime);
            yield return new WaitForSeconds(Mathf.Max(len, minDelay));

            // The win panel is about to slide up over the confetti -- stop any reaction burst still
            // fading out so it can't render on top of it (its particle renderer sorts above the UI
            // otherwise).
            characterReaction?.DisableReactionParticles();
            gameManager.InvokeMissionComplete(currentMissionIndex);
        }

        // A little "pop" on the character portrait and the completed-dish icon -- cheap, readable
        // positive feedback that doesn't need any new art, just a punch-scale on what's already there.
        private void PlayWinPunchFeedback()
        {
            RectTransform charRect = characterReaction != null ? characterReaction.ImageRectTransform : null;
            RectTransform contentRect = station.ContentImageRectTransform;

            if (charRect != null)
            {
                charRect.DOKill();
                charRect.DOPunchScale(Vector3.one * winPunchScale, winPunchDuration, 6, 0.8f);
            }
            if (contentRect != null)
            {
                contentRect.DOKill();
                contentRect.DOPunchScale(Vector3.one * winPunchScale, winPunchDuration, 6, 0.8f);
            }
        }

        private PantrySlot FindPantrySlot(string ingredientId)
        {
            foreach (var slot in pantrySlots)
                if (slot.IngredientId == ingredientId) return slot;
            return null;
        }

        private void OnDestroy()
        {
            timerPulseTween?.Kill();
        }
    }
}
