using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

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

        public void InitAsPantryToken(CookingManager owningManager, string id, Vector3 worldSpawnPosition, RectTransform dragLayerRoot)
        {
            manager = owningManager;
            IngredientId = id;
            Mode = IngredientDragMode.AddToStation;
            dragLayer = dragLayerRoot;
            isLocked = false;
            rectTransform.position = worldSpawnPosition;
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

        public void InitAsPlacedToken(CookingManager owningManager, string id, RectTransform dragLayerRoot)
        {
            manager = owningManager;
            IngredientId = id;
            Mode = IngredientDragMode.RemoveFromStation;
            dragLayer = dragLayerRoot;
            isLocked = false;
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
            });
        }

        // A successful removal (Mission 4) -- shrink away, matching the GDD's "Ice removal trail" VFX.
        public void PlayRemovedAndDestroy()
        {
            isLocked = true;
            rectTransform.DOKill();
            rectTransform.DOScale(0f, returnDuration).SetEase(Ease.InBack).OnComplete(() => Destroy(gameObject));
        }

        // A pantry-spawned token dropped somewhere invalid (wrong ingredient, wrong step, already
        // at quota) -- shake, then fade back into the pantry it came from and disappear. It never
        // belonged anywhere on screen the way a placed token does, so it's discarded rather than parked.
        public void BounceAndDestroy()
        {
            isLocked = true;
            rectTransform.DOKill();
            DOTween.Sequence()
                .Append(rectTransform.DOShakeAnchorPos(0.25f, strength: 25f, vibrato: 6))
                .Append(rectTransform.DOScale(0f, 0.2f).SetEase(Ease.InBack))
                .OnComplete(() => Destroy(gameObject));
        }

        // A failed removal attempt (dropped back into the station, or not the ingredient/quota the
        // current requirement allows) -- snap back to exactly where it was placed.
        public void ReturnToRest()
        {
            isLocked = false;
            rectTransform.SetParent(restParent, true);
            rectTransform.DOKill();
            DOTween.Sequence()
                .Append(rectTransform.DOShakeAnchorPos(0.25f, strength: 25f, vibrato: 6))
                .Append(rectTransform.DOAnchorPos(restAnchoredPos, returnDuration).SetEase(Ease.OutQuad));
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
