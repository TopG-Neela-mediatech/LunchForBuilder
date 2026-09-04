using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace tmkoc.lunchforbuilders
{
    // The blender/plate/bowl drop target for the current mission -- the Count & Cook equivalent of
    // BuildABot's RobotZone. Pure bookkeeping + visuals: it has no idea whether a given add/remove
    // is actually correct. CookingManager decides that and calls back in here only once a decision
    // has already been made.
    public class PreparationStation : MonoBehaviour
    {
        [Header("Bounds")]
        [Tooltip("Used to test whether a dragged token was dropped on/off this station.")]
        [SerializeField] private RectTransform dropArea;
        [Tooltip("Where placed tokens are anchored/parented once they snap in.")]
        [SerializeField] private RectTransform contentAnchor;

        [Header("Feedback")]
        [SerializeField] private Image glowImage;
        [SerializeField] private Color idleGlowColor = Color.white;
        [SerializeField] private Color hoverValidColor = new Color(0.4f, 1f, 0.4f);
        [SerializeField] private Color hoverInvalidColor = new Color(1f, 0.4f, 0.4f);

        public RectTransform DropArea => dropArea;
        public RectTransform ContentAnchor => contentAnchor;

        private readonly Dictionary<string, int> placedCounts = new Dictionary<string, int>();
        private readonly Dictionary<string, List<IngredientController>> placedTokens = new Dictionary<string, List<IngredientController>>();

        public bool ContainsScreenPoint(Vector2 screenPoint, Camera cam)
            => dropArea != null && RectTransformUtility.RectangleContainsScreenPoint(dropArea, screenPoint, cam);

        public int GetPlacedCount(string ingredientId) => placedCounts.TryGetValue(ingredientId, out int count) ? count : 0;

        public void RegisterPlaced(string ingredientId, IngredientController token)
        {
            placedCounts[ingredientId] = GetPlacedCount(ingredientId) + 1;
            if (!placedTokens.TryGetValue(ingredientId, out var list))
            {
                list = new List<IngredientController>();
                placedTokens[ingredientId] = list;
            }
            list.Add(token);
        }

        // Removes this specific token (the one the player is actively dragging back out) from the
        // bookkeeping -- not just "the last one placed", since the player picks which unit to remove.
        public void RemoveSpecificToken(string ingredientId, IngredientController token)
        {
            if (placedTokens.TryGetValue(ingredientId, out var list)) list.Remove(token);
            placedCounts[ingredientId] = Mathf.Max(0, GetPlacedCount(ingredientId) - 1);
        }

        // Destroys every placed token and resets bookkeeping -- used both when a new mission starts
        // and when the player presses Reset mid-recipe.
        public void Clear()
        {
            foreach (var kvp in placedTokens)
                foreach (var token in kvp.Value)
                    if (token != null) Destroy(token.gameObject);
            placedTokens.Clear();
            placedCounts.Clear();
        }

        public void SetHoverGlow(bool active, bool valid)
        {
            if (glowImage == null) return;
            glowImage.DOKill();
            glowImage.DOColor(active ? (valid ? hoverValidColor : hoverInvalidColor) : idleGlowColor, 0.15f);
        }
    }
}
