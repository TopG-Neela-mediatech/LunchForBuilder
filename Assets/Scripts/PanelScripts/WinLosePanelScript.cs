
using DG.Tweening;
using System;
using UnityEngine;
using UnityEngine.UI;


public class WinLosePanelScript : MonoBehaviour
{
    public static WinLosePanelScript Instance;

    [Header("UI References")]
    [SerializeField] private RectTransform cloud1;
    [SerializeField] private RectTransform cloud2;
    [SerializeField] private RectTransform cloud3;
    [SerializeField] private RectTransform cloud4;
    [SerializeField] private RectTransform cloud5;
    [SerializeField] private RectTransform cloud6;
    [SerializeField] private RectTransform star;
    [SerializeField] private RectTransform winloseTextImage;
    [SerializeField] private Button nextOrRestartButton;
    [SerializeField] private Button homeButton;
    [SerializeField] private GameObject starShineEffect;

    [Header("Sprite References")]
    [SerializeField] private Sprite starSprite;
    [SerializeField] private Sprite restartSprite;
    [SerializeField] private Sprite nextButtonSprite;
    [SerializeField] private Sprite restartButtonSprite;
    [SerializeField] private Sprite winTextSprite;
    [SerializeField] private Sprite loseTextSprite;

    [Header("Animation Settings")]
    [SerializeField] private float totalSequenceDuration = 5f;
    [SerializeField] private float cloudBounce = 1.5f;

    [Header("Target Y Positions (Inspector)")]
    [SerializeField] private float cloud1FinalY;
    [SerializeField] private float cloud2FinalY;
    [SerializeField] private float cloud3FinalY;
    [SerializeField] private float cloud4FinalY;
    [SerializeField] private float cloud5FinalY;
    [SerializeField] private float cloud6FinalY;

    [Header("Floating & Pulse Settings")]
    [SerializeField] private float floatAmount = 20f;
    [SerializeField] private float floatDuration = 2f;
    [SerializeField] private float textPulseScale = 1.1f;
    [SerializeField] private float textPulseDuration = 0.8f;

    private bool hasWon = true;
    private Sequence mainSequence;
    private readonly System.Collections.Generic.List<Tween> activeLoops = new System.Collections.Generic.List<Tween>();


    private void Awake() => Instance = this;

 
    private void Start()
    {
        homeButton.onClick.AddListener(() =>
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(TMKOCPlaySchoolConstants.TMKOCPlayMainMenu);
            HidePanel();
        });
    }
    public void ShowNextLevelPopUp(Action callback)
    {
        hasWon = true;
        KillAllTweens();
        ResetUI();
        SetupSprites();
        RunShowSequence();
        AddCallBackInNext(callback);
    }
    public void ShowRetryPopUp(Action callback)
    {
        hasWon = false;
        KillAllTweens();
        ResetUI();
        SetupSprites();
        RunShowSequence();
        AddCallBackInNext(callback);
    }
    private void AddCallBackInNext(Action callback)
    {
        nextOrRestartButton.onClick.RemoveAllListeners(); // Clear previous listeners to avoid stacking
        nextOrRestartButton.onClick.AddListener(() =>
        {
            callback?.Invoke();
            HidePanel();
        });
    }
    private void SetupSprites()
    {
        Image starImg = star.GetComponent<Image>();
        Image btnImg = nextOrRestartButton.GetComponent<Image>();
        Image txtImg = winloseTextImage.GetComponent<Image>();
        Vector2 textImgSize = winloseTextImage.sizeDelta;
        bool isPortrait = (Screen.orientation == ScreenOrientation.Portrait || Screen.orientation == ScreenOrientation.PortraitUpsideDown);
        if (hasWon)
        {
            if (starSprite != null) starImg.sprite = starSprite;
            if (nextButtonSprite != null) btnImg.sprite = nextButtonSprite;
            if (winTextSprite != null) txtImg.sprite = winTextSprite;
            if (isPortrait) textImgSize.x = 800f;
            else textImgSize.x = 1200f;
        }
        else
        {
            if (restartSprite != null) starImg.sprite = restartSprite;
            if (restartButtonSprite != null) btnImg.sprite = restartButtonSprite;
            if (loseTextSprite != null) txtImg.sprite = loseTextSprite;
            if (isPortrait) textImgSize.x = 600f;
            else textImgSize.x = 750f;
        }
        winloseTextImage.sizeDelta = textImgSize;
        if (isPortrait)
        {
            RectTransform nextBtnRect = nextOrRestartButton.GetComponent<RectTransform>();
            RectTransform homeBtnRect = homeButton.GetComponent<RectTransform>();
            nextBtnRect.anchoredPosition = new Vector2(nextBtnRect.anchoredPosition.x, 525f);
            homeBtnRect.anchoredPosition = new Vector2(homeBtnRect.anchoredPosition.x, 525f);
        }
    }
    private void KillAllTweens()
    {
        if (mainSequence != null) mainSequence.Kill();
        foreach (var t in activeLoops) t.Kill();
        activeLoops.Clear();
        star.DOKill();
        winloseTextImage.DOKill();
    }
    private void ResetUI()
    {
        float baseOffScreenY = -2000f;
        float verticalGap = 200f;
        RectTransform[] allClouds = { cloud1, cloud2, cloud3, cloud4, cloud5, cloud6 };
        for (int i = 0; i < allClouds.Length; i++)
        {
            if (allClouds[i] == null) continue;
            float individualStartY = baseOffScreenY - (i * verticalGap);
            allClouds[i].anchoredPosition = new Vector2(0, individualStartY);
        }
        star.anchoredPosition = new Vector2(0, baseOffScreenY - 400f);
        star.localScale = Vector3.zero;
        star.localRotation = Quaternion.identity;
        winloseTextImage.localScale = Vector3.zero;
        nextOrRestartButton.transform.localScale = Vector3.zero;
        homeButton.transform.localScale = Vector3.zero;
        nextOrRestartButton.enabled = false;
        homeButton.enabled = false;
        starShineEffect.SetActive(false);
    }
    private void RunShowSequence()
    {
        float moveDuration = totalSequenceDuration * 0.7f;
        mainSequence = DOTween.Sequence();
        mainSequence.Append(cloud1.DOAnchorPosY(cloud1FinalY, moveDuration).SetEase(Ease.OutBack, cloudBounce));
        mainSequence.Join(cloud2.DOAnchorPosY(cloud2FinalY, moveDuration).SetEase(Ease.OutBack, cloudBounce));
        mainSequence.Join(cloud3.DOAnchorPosY(cloud3FinalY, moveDuration).SetEase(Ease.OutBack, cloudBounce));
        mainSequence.Join(cloud4.DOAnchorPosY(cloud4FinalY, moveDuration).SetEase(Ease.OutBack, cloudBounce));
        mainSequence.Join(cloud5.DOAnchorPosY(cloud5FinalY, moveDuration).SetEase(Ease.OutBack, cloudBounce));
        mainSequence.Join(cloud6.DOAnchorPosY(cloud6FinalY, moveDuration).SetEase(Ease.OutBack, cloudBounce));
        mainSequence.Insert(moveDuration * 0.3f, star.DOAnchorPos(Vector2.zero, 1.5f).SetEase(Ease.OutQuint).OnComplete(() =>
        {
            if (hasWon) starShineEffect.SetActive(true);
            winloseTextImage.DOScale(1f, 1f).SetEase(Ease.OutBack);
        }));
        mainSequence.Join(star.DOScale(1f, 1.2f).SetEase(Ease.OutBack));
        mainSequence.Join(star.DORotate(new Vector3(0, 0, 360), 1.5f, RotateMode.FastBeyond360).SetRelative(true).SetEase(Ease.OutBack));
        mainSequence.Append(nextOrRestartButton.transform.DOScale(1f, 0.5f).SetEase(Ease.OutBack));
        mainSequence.Join(homeButton.transform.DOScale(1f, 0.5f).SetEase(Ease.OutBack));
        mainSequence.OnComplete(() =>
        {
            StartIdleAnimations();
            nextOrRestartButton.enabled = true;
            homeButton.enabled = true;
        });
    }
    private void HidePanel()
    {
        KillAllTweens();
        starShineEffect.SetActive(false);
        nextOrRestartButton.enabled = false;
        homeButton.enabled = false;
        mainSequence = DOTween.Sequence();
        float exitDuration = 0.6f;
        float dynamicOffScreenY = -2000f;
        mainSequence.Append(nextOrRestartButton.transform.DOScale(0f, 0.3f).SetEase(Ease.InBack));
        mainSequence.Join(homeButton.transform.DOScale(0f, 0.3f).SetEase(Ease.InBack));
        mainSequence.Join(winloseTextImage.DOScale(0f, 0.3f).SetEase(Ease.InBack));
        mainSequence.Join(star.DOAnchorPosY(dynamicOffScreenY, exitDuration).SetEase(Ease.InBack));
        mainSequence.Join(star.DOScale(0f, exitDuration).SetEase(Ease.InBack));
        RectTransform[] allClouds = { cloud1, cloud2, cloud3, cloud4, cloud5, cloud6 };
        foreach (var cloud in allClouds)
        {
            mainSequence.Join(cloud.DOAnchorPosY(dynamicOffScreenY, exitDuration).SetEase(Ease.InQuad));
        }
    }
    private void StartIdleAnimations()
    {
        star.DOKill();
        star.DORotate(new Vector3(0, 0, 360), 7f, RotateMode.FastBeyond360)
            .SetRelative(true)
            .SetEase(Ease.Linear)
            .SetLoops(-1, LoopType.Incremental);

        activeLoops.Add(winloseTextImage.DOScale(textPulseScale, textPulseDuration)
            .SetLoops(-1, LoopType.Yoyo)
            .SetEase(Ease.InOutSine));

        activeLoops.Add(cloud1.DOAnchorPosY(cloud1FinalY + floatAmount, floatDuration).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine));
        activeLoops.Add(cloud2.DOAnchorPosY(cloud2FinalY + floatAmount, floatDuration * 1.1f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine));
        activeLoops.Add(cloud3.DOAnchorPosY(cloud3FinalY + floatAmount, floatDuration * 0.9f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine));
        activeLoops.Add(cloud4.DOAnchorPosY(cloud4FinalY + floatAmount, floatDuration * 1.2f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine));
        activeLoops.Add(cloud5.DOAnchorPosY(cloud5FinalY + floatAmount, floatDuration * 0.8f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine));
        activeLoops.Add(cloud6.DOAnchorPosY(cloud6FinalY + floatAmount, floatDuration).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine));
    }
}

