using System;
using System.Collections;
using DG.Tweening;
using UnityEngine;

namespace tmkoc.lunchforbuilders
{
    // Idle-hint "hand" for Count & Cook. Deliberately dumb about game rules -- it only knows how to
    // move a hand between two points (or mime a tap on one) and loop that until told to stop.
    // CookingManager is the only thing that decides WHAT to point at (which pantry slot, which
    // placed token, the Serve button); this class just needs a hand image, wired in the Inspector,
    // and is reachable from anywhere via GameManager.Instance.TutorialManager.
    public class TutorialManager : MonoBehaviour
    {
        [SerializeField] private RectTransform handImage;
        [Tooltip("Horizontal/vertical offset applied to every point the hand moves to. The hand graphic's fingertip usually isn't at its own pivot/center, so without this the hand doesn't visually line up with what it's pointing at -- positive X moves the hand right, positive Y moves it up, relative to the target.")]
        [SerializeField] private float handPointerXOffset;
        [SerializeField] private float handPointerYOffset;

        [Header("Timing")]
        [Tooltip("How long the player can stay idle before a hint hand appears. The very first hint of the whole session skips this wait entirely.")]
        [SerializeField] private float idleDelay = 15f;
        [SerializeField] private float dragTweenDuration = 0.9f;
        [Tooltip("How far the hand pulls a removal token out/up, since there's no fixed 'put it here' target for a removal.")]
        [SerializeField] private float removeHintDistance = 220f;
        [SerializeField] private float tapPressOffset = 18f;
        [Tooltip("Pause at each end of the loop (hand back at the start / hand back at rest) before repeating.")]
        [SerializeField] private float loopPauseDuration = 0.3f;

        private RectTransform handParent;
        private Coroutine waitRoutine;
        private Sequence handSequence;

        private void Awake()
        {
            handParent = handImage != null ? handImage.parent as RectTransform : null;
            if (handImage != null) handImage.gameObject.SetActive(false);
        }

        // Drag `from` (a pantry slot) to `to` (the station) -- the "add an ingredient" hint used by
        // every mission except a removal step.
        public void ShowAddHint(RectTransform from, RectTransform to, bool immediate) =>
            RestartWait(() => PlayDragHint(from, to), immediate);

        // Drag `from` (a token already sitting in the station) outward -- Mission 4's "remove an
        // ingredient" hint.
        public void ShowRemoveHint(RectTransform from, bool immediate) =>
            RestartWait(() => PlayRemoveHint(from), immediate);

        // Points at (and mimes tapping) a button -- used once the recipe is complete, to nudge Serve.
        public void ShowTapHint(RectTransform target, bool immediate) =>
            RestartWait(() => PlayTapHint(target), immediate);

        public void CancelHint()
        {
            if (waitRoutine != null)
            {
                StopCoroutine(waitRoutine);
                waitRoutine = null;
            }
            HideHand();
        }

        private void RestartWait(Action playHint, bool immediate)
        {
            CancelHint();
            if (handImage == null || handParent == null) return;
            waitRoutine = StartCoroutine(WaitThenShowRoutine(playHint, immediate ? 0f : idleDelay));
        }

        // Waits out the idle delay (or not at all, for the session's very first hint), then starts
        // the hint animation -- which loops on its own (DOTween SetLoops(-1)) for as long as it stays
        // the "next correct action". CookingManager calls CancelHint the instant the player actually
        // does something, then immediately asks for a fresh hint pointed at whatever the new next
        // action is, so this never needs to re-wait/re-show on its own.
        private IEnumerator WaitThenShowRoutine(Action playHint, float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            playHint();
            waitRoutine = null;
        }

        private void HideHand()
        {
            handSequence?.Kill();
            if (handImage == null) return;
            handImage.gameObject.SetActive(false);
            handImage.localScale = Vector3.one;
        }

        // Drags from start to end, pauses, snaps back to start, pauses, and repeats forever --
        // SetLoops(-1) keeps this going until CancelHint() kills the sequence, so the player sees the
        // same drag demonstrated over and over rather than just once.
        private void PlayDragHint(RectTransform from, RectTransform to)
        {
            if (from == null || to == null) return;
            handImage.gameObject.SetActive(true);
            handImage.localScale = Vector3.one;
            Vector2 start = ToLocalAnchoredPos(from, handParent);
            Vector2 end = ToLocalAnchoredPos(to, handParent);
            handImage.anchoredPosition = start;

            handSequence?.Kill();
            handSequence = DOTween.Sequence();
            handSequence.Append(handImage.DOAnchorPos(end, dragTweenDuration).SetEase(Ease.InOutSine));
            handSequence.AppendInterval(loopPauseDuration);
            handSequence.AppendCallback(() => handImage.anchoredPosition = start);
            handSequence.AppendInterval(loopPauseDuration);
            handSequence.SetLoops(-1);
        }

        private void PlayRemoveHint(RectTransform from)
        {
            if (from == null) return;
            handImage.gameObject.SetActive(true);
            handImage.localScale = Vector3.one;
            Vector2 start = ToLocalAnchoredPos(from, handParent);
            Vector2 end = start + new Vector2(0f, removeHintDistance);
            handImage.anchoredPosition = start;

            handSequence?.Kill();
            handSequence = DOTween.Sequence();
            handSequence.Append(handImage.DOAnchorPos(end, dragTweenDuration).SetEase(Ease.InOutSine));
            handSequence.AppendInterval(loopPauseDuration);
            handSequence.AppendCallback(() => handImage.anchoredPosition = start);
            handSequence.AppendInterval(loopPauseDuration);
            handSequence.SetLoops(-1);
        }

        // Hand pops onto the target and mimes two quick presses -- reads as "tap here" far more
        // clearly than a scale-only pulse in place.
        private void PlayTapHint(RectTransform target)
        {
            if (target == null) return;
            handImage.gameObject.SetActive(true);
            handImage.localScale = Vector3.one;
            Vector2 restPos = ToLocalAnchoredPos(target, handParent);
            Vector2 pressPos = restPos + new Vector2(0f, -tapPressOffset);
            handImage.anchoredPosition = restPos;

            handSequence?.Kill();
            handSequence = DOTween.Sequence();
            AppendTapPress(handSequence, restPos, pressPos);
            handSequence.AppendInterval(0.15f);
            AppendTapPress(handSequence, restPos, pressPos);
            handSequence.AppendInterval(loopPauseDuration + 0.2f);
            handSequence.SetLoops(-1);
        }

        private void AppendTapPress(Sequence seq, Vector2 restPos, Vector2 pressPos)
        {
            seq.Append(handImage.DOAnchorPos(pressPos, 0.12f).SetEase(Ease.OutQuad));
            seq.Join(handImage.DOScale(0.85f, 0.12f).SetEase(Ease.OutQuad));
            seq.Append(handImage.DOAnchorPos(restPos, 0.12f).SetEase(Ease.OutQuad));
            seq.Join(handImage.DOScale(1f, 0.12f).SetEase(Ease.OutQuad));
        }

        private Vector2 ToLocalAnchoredPos(RectTransform target, RectTransform parent)
        {
            Vector3 local = parent.InverseTransformPoint(target.position);
            return new Vector2(local.x + handPointerXOffset, local.y + handPointerYOffset);
        }

        private void OnDestroy()
        {
            handSequence?.Kill();
            if (waitRoutine != null) StopCoroutine(waitRoutine);
        }
    }
}
