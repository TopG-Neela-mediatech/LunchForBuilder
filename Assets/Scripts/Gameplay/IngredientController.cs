using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace tmkoc.lunchforbuilders
{
    public enum IngredientDragMode { AddToStation, RemoveFromStation }

    // Mobile drag-and-drop token for one unit of one ingredient, driven two different ways:
    // - AddToStation tokens are spawned mid-drag by PantrySlot (see InitAsPantryToken +
    //   BeginExternalDrag/ContinueExternalDrag/EndExternalDrag), which forwards the drag into this
    //   instance directly -- it never receives its own IBeginDragHandler event, since the
    //   pointer-down was captured by the pantry button, not this freshly-created object.
    // - RemoveFromStation tokens already sit inside the station (Mission 4's starting ice cubes,
    //   seeded via InitAsPlacedToken + PlaceInstantly) and are dragged natively through this
    //   component's own IBeginDragHandler/IDragHandler/IEndDragHandler.
    // Either way, every drop is reported to CookingManager.ResolveDrop, which alone decides
    // success/failure and calls back into one of the Snap/Bounce/Return/Remove outcome methods.
    public class IngredientController : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Header("Visual")]
        [SerializeField] private RectTransform rectTransform;
        [SerializeField] private CanvasGroup canvasGroup;
        [Tooltip("Swapped to the matching ingredient sprite at spawn time -- one shared token prefab renders every ingredient.")]
        [SerializeField] private Image iconImage;

        [Header("Drag Feel")]
        [SerializeField] private float dragScale = 1.15f;
        [SerializeField] private float snapDuration = 0.35f;
        [SerializeField] private float returnDuration = 0.3f;

        public string IngredientId { get; private set; }
        public IngredientDragMode Mode { get; private set; }

        private CookingManager manager;
        private RectTransform dragLayer;
        private Transform restParent;
        private Vector2 restAnchoredPos;
        private bool isLocked;

        // ---- Add-to-station tokens: spawned + driven externally by PantrySlot ----

        public void InitAsPantryToken(CookingManager owningManager, string id, Sprite icon, Vector3 worldSpawnPosition, RectTransform dragLayerRoot)
        {
            manager = owningManager;
            IngredientId = id;
            Mode = IngredientDragMode.AddToStation;
            dragLayer = dragLayerRoot;
            isLocked = false;
            rectTransform.position = worldSpawnPosition;
            // So a rejected drop has somewhere to glide back to -- this token's spawn point in the
            // pantry, since it's a fresh instance every drag and never reparents away from dragLayer
            // unless SnapIntoStation succeeds.
            restParent = dragLayer;
            restAnchoredPos = rectTransform.anchoredPosition;
            SetIcon(icon);
        }

        public void BeginExternalDrag(PointerEventData eventData)
        {
            manager?.NotifyInteractionStarted(this);
            rectTransform.DOKill();
            rectTransform.DOScale(dragScale, 0.15f);
            if (canvasGroup != null) canvasGroup.blocksRaycasts = false;
        }

        public void ContinueExternalDrag(PointerEventData eventData)
        {
            ApplyDragDelta(eventData);
            manager?.UpdateHoverFeedback(this, eventData);
        }

        public void EndExternalDrag(PointerEventData eventData) => FinishDrag(eventData);

        // ---- Remove-from-station tokens: placed instantly, then dragged natively ----

        public void InitAsPlacedToken(CookingManager owningManager, string id, Sprite icon, RectTransform dragLayerRoot)
        {
            manager = owningManager;
            IngredientId = id;
            Mode = IngredientDragMode.RemoveFromStation;
            dragLayer = dragLayerRoot;
            isLocked = false;
            SetIcon(icon);
        }

        private void SetIcon(Sprite icon)
        {
            if (iconImage != null) iconImage.sprite = icon;
        }

        // Places this token directly at a station anchor with no tween -- used for
        // startingIngredients seeded before the mission's first drop (Mission 4's ice cubes).
        public void PlaceInstantly(RectTransform stationAnchor)
        {
            restParent = stationAnchor;
            rectTransform.SetParent(stationAnchor, false);
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.localScale = Vector3.one;
            restAnchoredPos = Vector2.zero;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (isLocked || Mode != IngredientDragMode.RemoveFromStation) return;
            manager?.NotifyInteractionStarted(this);

            restParent = rectTransform.parent;
            restAnchoredPos = rectTransform.anchoredPosition;

            rectTransform.SetParent(dragLayer, true);
            rectTransform.DOKill();
            rectTransform.DOScale(dragScale, 0.15f);
            if (canvasGroup != null) canvasGroup.blocksRaycasts = false;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (isLocked || Mode != IngredientDragMode.RemoveFromStation) return;
            ApplyDragDelta(eventData);
            manager?.UpdateHoverFeedback(this, eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (isLocked || Mode != IngredientDragMode.RemoveFromStation) return;
            FinishDrag(eventData);
        }

        private void ApplyDragDelta(PointerEventData eventData)
        {
            float scale = GetCanvasScale();
            rectTransform.anchoredPosition += eventData.delta / scale;
        }

        private void FinishDrag(PointerEventData eventData)
        {
            if (canvasGroup != null) canvasGroup.blocksRaycasts = true;
            rectTransform.DOKill();
            rectTransform.DOScale(1f, 0.15f);
            manager?.ResolveDrop(this, eventData);
        }

        // ---- Outcomes, driven by CookingManager after it resolves the drop ----

        public void SnapIntoStation(RectTransform stationAnchor)
        {
            isLocked = true;
            Vector2 targetAnchored = ToLocalAnchoredPos(stationAnchor, dragLayer);
            rectTransform.DOAnchorPos(targetAnchored, snapDuration).SetEase(Ease.OutBack).OnComplete(() =>
            {
                rectTransform.SetParent(stationAnchor, false);
                rectTransform.anchoredPosition = Vector2.zero;
                rectTransform.localScale = Vector3.one;
                isLocked = false;
                restParent = stationAnchor;
                restAnchoredPos = Vector2.zero;
                // A small "landed!" settle -- same shake used to signal rejection elsewhere, just
                // gentler, so accepting a drop reads as a positive impact rather than a silent stop.
                // Then shrink away entirely -- placed tokens all land at the same content anchor, so
                // left visible they'd just pile up on top of each other; the recipe card's counter
                // is what actually shows the count.
                DOTween.Sequence()
                    .Append(rectTransform.DOShakeAnchorPos(0.2f, strength: 12f, vibrato: 8))
                    .Append(rectTransform.DOScale(0f, 0.3f));
            });
        }

        // A successful removal (Mission 4) -- shrink away, matching the GDD's "Ice removal trail" VFX.
        public void PlayRemovedAndDestroy()
        {
            isLocked = true;
            rectTransform.DOKill();
            rectTransform.DOScale(0f, returnDuration).SetEase(Ease.InBack).OnComplete(() => Destroy(gameObject));
        }

        // Any invalid drop -- wrong ingredient, wrong step, already at quota, a failed removal
        // attempt -- gets the same feedback: shake, then glide back to wherever this token came
        // from. A pantry-spawned (AddToStation) token is a throwaway spun up fresh each drag, so it
        // disappears once it arrives back; a token that was already really sitting in the station
        // (RemoveFromStation) just resettles into its slot, unchanged.
        public void ReturnToRest()
        {
            isLocked = false;
            rectTransform.SetParent(restParent, true);
            rectTransform.DOKill();
            DOTween.Sequence()
                .Append(rectTransform.DOShakeAnchorPos(0.25f, strength: 25f, vibrato: 6))
                .Append(rectTransform.DOAnchorPos(restAnchoredPos, returnDuration).SetEase(Ease.OutQuad))
                .OnComplete(() =>
                {
                    if (Mode == IngredientDragMode.AddToStation) Destroy(gameObject);
                });
        }

        private float GetCanvasScale()
        {
            var canvas = rectTransform.GetComponentInParent<Canvas>();
            return canvas != null ? canvas.scaleFactor : 1f;
        }

        private Vector2 ToLocalAnchoredPos(RectTransform target, RectTransform parent)
        {
            Vector3 local = parent.InverseTransformPoint(target.position);
            return new Vector2(local.x, local.y);
        }
    }
}
