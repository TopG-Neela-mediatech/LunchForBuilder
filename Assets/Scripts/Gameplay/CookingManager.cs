using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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
        [SerializeField] private PreparationStation station;
        [SerializeField] private PantrySlot[] pantrySlots;
        [SerializeField] private RecipeCardController recipeCard;
        [SerializeField] private RectTransform dragLayer;
        [SerializeField] private Button serveButton;
        [SerializeField] private Button resetButton;

        [Header("Pacing")]
        [Tooltip("How long the serve/gratification beat holds before the mission is reported complete.")]
        [SerializeField] private float serveCompleteDelay = 1.5f;

        private MissionRecipeData currentMission;
        private int currentMissionIndex;
        private int currentSequenceStep;
        private readonly Dictionary<string, int> removedSoFar = new Dictionary<string, int>();

        private void Awake()
        {
            foreach (var slot in pantrySlots) slot.Init(this, dragLayer);
            if (serveButton != null) serveButton.onClick.AddListener(OnServePressed);
            if (resetButton != null) resetButton.onClick.AddListener(OnResetPressed);
            if (recipeCard != null) recipeCard.OnPeekUsed += HandlePeekUsed;
        }

        public void StartMission(MissionRecipeData mission, int missionIndex)
        {
            currentMission = mission;
            currentMissionIndex = missionIndex;
            currentSequenceStep = mission.LearningRule == LearningRule.OrderAndCounting ? 0 : -1;
            removedSoFar.Clear();

            station.Clear();
            SeedStartingIngredients();

            recipeCard?.Setup(mission);
            UpdatePantryInteractivity();
            RefreshServeButton();
            RaiseProgress();

            gameManager.InvokeMissionStarted(missionIndex);
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

        // Reserved for a future idle-hint hand, same hook shape as RobotAssemblyManager.NotifyInteractionStarted.
        public void NotifyInteractionStarted(IngredientController token) { }

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
                RefreshServeButton();
            }
            else
            {
                token.BounceAndDestroy();
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
                RefreshServeButton();
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

        private void RefreshServeButton()
        {
            if (serveButton != null) serveButton.interactable = IsRecipeComplete();
        }

        // Completion is a player action (tap Serve), not automatic, matching the GDD's
        // "Serve active/inactive state" and "Serve button" UI assets.
        private void OnServePressed()
        {
            if (!IsRecipeComplete()) return;
            if (serveButton != null) serveButton.interactable = false;
            StartCoroutine(ServeCompleteRoutine());
        }

        private IEnumerator ServeCompleteRoutine()
        {
            yield return new WaitForSeconds(serveCompleteDelay);
            gameManager.InvokeMissionComplete(currentMissionIndex);
        }

        // Clears the station and restarts the current recipe from scratch, no penalty -- the GDD's
        // "Reset button" / "Reset interaction" assets.
        private void OnResetPressed() => StartMission(currentMission, currentMissionIndex);

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
