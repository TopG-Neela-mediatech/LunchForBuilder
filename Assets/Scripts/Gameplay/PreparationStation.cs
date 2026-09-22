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
        [Tooltip("Extra margin (all sides) added to the container's hit-test area when deciding whether a drop landed on the station. A vertically-stacked pile (MissionRecipeData.StackVertically, e.g. the sandwich) grows taller with every layer, so without this a player aiming at the current top of the stack could land just outside the container art's own tight rect and get bounced for no clear reason.")]
        [SerializeField] private float dropHitTestPadding = 60f;

        [Header("Container Drop Points")]
        [Tooltip("Already children of Content Anchor -- each marks the BOTTOM of one container's actual visible art (base of the plate/jug), not a bounding area. Ingredients land at whichever one the current mission selects (MissionRecipeData.ContainerBoundary) and pile upward from that single point, so they can never land outside the container's visible shape the way a random point-in-a-bounding-rect could.")]
        [SerializeField] private RectTransform plateBoundary;
        [SerializeField] private RectTransform strawberryLemonadeBoundary;
        [SerializeField] private RectTransform orangeMangoJuiceBoundary;

        [Header("Feedback")]
        [SerializeField] private Image glowImage;
        [SerializeField] private Color idleGlowColor = Color.white;
        [SerializeField] private Color hoverValidColor = new Color(0.4f, 1f, 0.4f);
        [SerializeField] private Color hoverInvalidColor = new Color(1f, 0.4f, 0.4f);
        [Tooltip("The jug/plate/glass itself wobbles on every drop, correct or not -- physical feedback that something just happened, separate from the token's own shake/bounce.")]
        [SerializeField] private float dropShakeDuration = 0.3f;
        [SerializeField] private float dropShakeStrength = 12f;
        [SerializeField] private int dropShakeVibrato = 8;

        private Tween dropShakeTween;

        [Tooltip("Swapped to the current mission's jug/plate/blender/bowl sprite -- one shared Preparation Station renders every mission's container.")]
        [SerializeField] private Image contentImage;
        public RectTransform DropArea => dropArea;
        public RectTransform ContentAnchor => contentAnchor;
        // The completed-dish sprite's own RectTransform -- used for the win celebration's
        // positive-feedback punch-scale, separate from ContentAnchor (which holds the ingredient
        // tokens, not the container/dish image itself).
        public RectTransform ContentImageRectTransform => contentImage != null ? contentImage.rectTransform : null;

        public RectTransform GetDropPoint(ContainerBoundary boundary)
        {
            switch (boundary)
            {
                case ContainerBoundary.StrawberryLemonade: return strawberryLemonadeBoundary;
                case ContainerBoundary.OrangeMangoJuice: return orangeMangoJuiceBoundary;
                default: return plateBoundary;
            }
        }
        // Same fallback as ContainsScreenPoint -- wherever a real drop is actually tested against is
        // also where a drag hint should visually land.
        public RectTransform HintTargetArea => contentImage != null ? contentImage.rectTransform : dropArea;

        private readonly Dictionary<string, int> placedCounts = new Dictionary<string, int>();
        private readonly Dictionary<string, List<IngredientController>> placedTokens = new Dictionary<string, List<IngredientController>>();

        // The station icon (jug/plate/etc.) is what the player actually sees as "the container", so
        // that's what a drop is tested against once it's assigned -- dropArea is only a fallback for
        // before contentImage is wired up. The tested rect is padded out by dropHitTestPadding on
        // every side, so a player doesn't have to land precisely inside the container art's own
        // bounds -- important once a pile has grown taller than that art (a vertical stack especially).
        public bool ContainsScreenPoint(Vector2 screenPoint, Camera cam)
        {
            RectTransform target = contentImage != null ? contentImage.rectTransform : dropArea;
            if (target == null) return false;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(target, screenPoint, cam, out Vector2 localPoint)) return false;

            Rect padded = target.rect;
            padded.xMin -= dropHitTestPadding;
            padded.xMax += dropHitTestPadding;
            padded.yMin -= dropHitTestPadding;
            padded.yMax += dropHitTestPadding;
            return padded.Contains(localPoint);
        }

        public int GetPlacedCount(string ingredientId) => placedCounts.TryGetValue(ingredientId, out int count) ? count : 0;

        // How many tokens (of any ingredient) are currently resting in the station -- used to work
        // out the next one's stack height, since ingredients pile up from the drop point regardless
        // of which ingredient type they are (e.g. a salad's broccoli, tomato and corn all share one pile).
        public int TotalPlacedCount
        {
            get
            {
                int total = 0;
                foreach (var count in placedCounts.Values) total += count;
                return total;
            }
        }

        // A live token currently placed in the station, for the tutorial hand to point at when
        // hinting a removal -- doesn't remove or mutate anything, just looks.
        public IngredientController PeekPlacedToken(string ingredientId)
        {
            if (placedTokens.TryGetValue(ingredientId, out var list) && list.Count > 0) return list[list.Count - 1];
            return null;
        }

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

        public void SetContentSprite(Sprite sprite)
        {
            if (contentImage != null) contentImage.sprite = sprite;
        }

        // Resizes the content anchor itself to match this mission's container art -- purely
        // visual/layout, unrelated to the boundary RectTransforms ingredients actually place within.
        public void SetContentAnchorSize(Vector2 size)
        {
            if (contentAnchor != null) contentAnchor.sizeDelta = size;
        }

        // Hides every currently-placed ingredient token -- used the moment the recipe completes and
        // the station swaps to the finished-dish sprite, so the individual ingredient icons don't
        // keep sitting on top of it.
        public void HidePlacedTokens()
        {
            foreach (var kvp in placedTokens)
                foreach (var token in kvp.Value)
                    if (token != null) token.gameObject.SetActive(false);
        }

        public void SetHoverGlow(bool active, bool valid)
        {
            if (glowImage == null) return;
            glowImage.DOKill();
            glowImage.DOColor(active ? (valid ? hoverValidColor : hoverInvalidColor) : idleGlowColor, 0.15f);
        }

        // Shakes the container itself (jug/plate/glass), called on every resolved drop regardless of
        // whether it was correct -- a physical "something just happened here" wobble.
        public void PlayDropShake()
        {
            if (contentImage == null) return;
            dropShakeTween?.Kill();
            dropShakeTween = contentImage.rectTransform.DOShakeAnchorPos(dropShakeDuration, strength: dropShakeStrength, vibrato: dropShakeVibrato);
        }
    }
}
