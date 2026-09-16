using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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
        [Tooltip("This slot's own icon -- already set to the ingredient's sprite. Read at spawn time and copied onto every token this slot creates, so one shared token prefab can render every ingredient.")]
        [SerializeField] private Image icon;
        [SerializeField] private RectTransform rectTransform;
        [Tooltip("Dimmed/non-interactable while this ingredient isn't valid for the current sequence step (Mission 3 only). Untouched otherwise.")]
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Idle Pulse")]
        [Tooltip("Gentle continuous scale breathing while this slot is interactable -- a quiet 'you can pick me up' invitation, off while dimmed or mid-drag.")]
        [SerializeField] private float pulseScale = 1.06f;
        [SerializeField] private float pulseDuration = 0.9f;

        public string IngredientId => ingredientId;

        private CookingManager manager;
        private RectTransform dragLayer;
        private IngredientController activeToken;
        private Tween pulseTween;

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

            if (interactable) StartPulse();
            else StopPulse();
        }

        private void StartPulse()
        {
            if (rectTransform == null) return;
            pulseTween?.Kill();
            rectTransform.localScale = Vector3.one;
            pulseTween = rectTransform.DOScale(pulseScale, pulseDuration)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo);
        }

        // Killed rather than paused -- a dimmed/mid-drag slot should snap back to a neutral 1x
        // scale immediately, not freeze mid-breath.
        private void StopPulse()
        {
            pulseTween?.Kill();
            pulseTween = null;
            if (rectTransform != null) rectTransform.localScale = Vector3.one;
        }

        // Used only to seed a mission's startingIngredients (Mission 4's 6 starting ice cubes) --
        // creates a token already in RemoveFromStation mode rather than spawning it via a drag.
        public IngredientController SpawnPlacedToken(CookingManager owningManager, RectTransform dragLayerRoot)
        {
            if (tokenPrefab == null) return null;
            var token = Instantiate(tokenPrefab, dragLayerRoot);
            token.InitAsPlacedToken(owningManager, ingredientId, IconSprite, dragLayerRoot);
            return token;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (tokenPrefab == null || manager == null) return;
            StopPulse();
            activeToken = Instantiate(tokenPrefab, dragLayer);
            activeToken.InitAsPantryToken(manager, ingredientId, IconSprite, rectTransform.position, dragLayer);
            activeToken.BeginExternalDrag(eventData);
        }

        private Sprite IconSprite => icon != null ? icon.sprite : null;

        public void OnDrag(PointerEventData eventData) => activeToken?.ContinueExternalDrag(eventData);

        public void OnEndDrag(PointerEventData eventData)
        {
            activeToken?.EndExternalDrag(eventData);
            activeToken = null;
            if (canvasGroup == null || canvasGroup.interactable) StartPulse();
        }

        private void OnDestroy() => pulseTween?.Kill();
    }
}
