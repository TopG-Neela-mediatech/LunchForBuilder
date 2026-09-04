using UnityEngine;
using UnityEngine.EventSystems;

namespace tmkoc.lunchforbuilders
{
    // One Inspector-wired button per ingredient type in the current mission's pantry tray. The
    // pantry is an infinite source -- every drag spawns a fresh IngredientController token rather
    // than depleting a fixed pile, which is what lets a player attempt to over-add an ingredient
    // (the GDD's "Too-many Ingredient bounce" / "Overfill feedback" only make sense if that attempt
    // is possible in the first place). CookingManager toggles SetInteractable per slot to gate the
    // pantry during Mission 3's ordered sequence.
    public class PantrySlot : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private string ingredientId;
        [SerializeField] private IngredientController tokenPrefab;
        [SerializeField] private RectTransform rectTransform;
        [Tooltip("Dimmed/non-interactable while this ingredient isn't valid for the current sequence step (Mission 3 only). Untouched otherwise.")]
        [SerializeField] private CanvasGroup canvasGroup;

        public string IngredientId => ingredientId;

        private CookingManager manager;
        private RectTransform dragLayer;
        private IngredientController activeToken;

        public void Init(CookingManager owningManager, RectTransform dragLayerRoot)
        {
            manager = owningManager;
            dragLayer = dragLayerRoot;
        }

        public void SetInteractable(bool interactable)
        {
            if (canvasGroup == null) return;
            canvasGroup.interactable = interactable;
            canvasGroup.alpha = interactable ? 1f : 0.4f;
        }

        // Used only to seed a mission's startingIngredients (Mission 4's 6 starting ice cubes) --
        // creates a token already in RemoveFromStation mode rather than spawning it via a drag.
        public IngredientController SpawnPlacedToken(CookingManager owningManager, RectTransform dragLayerRoot)
        {
            if (tokenPrefab == null) return null;
            var token = Instantiate(tokenPrefab, dragLayerRoot);
            token.InitAsPlacedToken(owningManager, ingredientId, dragLayerRoot);
            return token;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (tokenPrefab == null || manager == null) return;
            activeToken = Instantiate(tokenPrefab, dragLayer);
            activeToken.InitAsPantryToken(manager, ingredientId, rectTransform.position, dragLayer);
            activeToken.BeginExternalDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData) => activeToken?.ContinueExternalDrag(eventData);

        public void OnEndDrag(PointerEventData eventData)
        {
            activeToken?.EndExternalDrag(eventData);
            activeToken = null;
        }
    }
}
