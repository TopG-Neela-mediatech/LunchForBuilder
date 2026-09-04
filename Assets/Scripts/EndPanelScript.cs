using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System;


namespace tmkoc.lunchforbuilders
{
    public class EndPanelScript : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Image winChrachterImage;
        [SerializeField] private Image loseChrachterImage;
        [SerializeField] private RectTransform winPanel;
        [SerializeField] private RectTransform losePanel;
        [SerializeField] private Button retryButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button[] homeButtons;
        [SerializeField] private RectTransform winRaysT;
        [SerializeField] private RectTransform loseRaysT;

        [Header("Slide Anim")]
        [SerializeField] private Vector2 onScreenAnchoredPos = Vector2.zero;
        [SerializeField] private float slideInDuration = 0.9f;
        [SerializeField] private float slideOutDuration = 0.5f;
        [SerializeField] private Ease slideInEase = Ease.OutBack;
        [SerializeField] private Ease slideOutEase = Ease.InBack;


        public bool IsShowing => (winPanel != null && winPanel.gameObject.activeSelf) || (losePanel != null && losePanel.gameObject.activeSelf);

        private RectTransform activePanel;
        private Button activeButton;

        private void Awake()
        {
            winPanel.gameObject.SetActive(false);
            losePanel.gameObject.SetActive(false);
            retryButton.gameObject.SetActive(false);
            nextButton.gameObject.SetActive(false);
            retryButton.onClick.AddListener(OnButtonClicked);
            nextButton.onClick.AddListener(OnButtonClicked);
        }
        private void Start()
        {
            foreach (var homeButton in homeButtons)
            {
                homeButton.onClick.AddListener(() => SceneManager.LoadScene(TMKOCPlaySchoolConstants.TMKOCPlayMainMenu));
            }
        }
        private void OnDisable()
        {
            winPanel.DOKill();
            losePanel.DOKill();
        }

        // Public API
        public void ShowWin()
        {
            Show(winPanel, nextButton); 
         //S   Sprite winSprite = GameManager.Instance.LevelManager.CurrentLevelData.winPanelSprite;
          //  winChrachterImage.sprite = winSprite;
            nextButton.onClick.AddListener(()=>GameManager.Instance.LevelManager.LoadNextLevel());
            RotateRaysLoop(winRaysT, 8f, true);          
        }

        public void ShowLose()
        {
            Show(losePanel, retryButton);
       //     Sprite loseSprite = GameManager.Instance.LevelManager.CurrentLevelData.losePanelSprite;
          //  loseChrachterImage.sprite = loseSprite;
          //  retryButton.onClick.AddListener(() => GameManager.Instance.LevelManager.StartLevel());
            RotateRaysLoop(loseRaysT, 8f, false);
        }

        private void Show(RectTransform panel, Button button)
        {
           // GameManager.Instance.SoundManager.StopAllExceptBGM();

            activePanel = panel;
            activeButton = button;

            panel.gameObject.SetActive(true);
            panel.DOKill();
            panel.anchoredPosition = OffScreenPos(panel);
            panel.DOAnchorPos(onScreenAnchoredPos, slideInDuration)
                .SetEase(slideInEase)
                .OnComplete(() => button.gameObject.SetActive(true));
        }

        // Panel's RectTransform is stretched to the canvas, so its own height is a resolution-independent slide distance.
        private Vector2 OffScreenPos(RectTransform panel)
        {
            return new Vector2(0f, -panel.rect.height);
        }
        private void RotateRaysLoop(RectTransform rays, float duration = 8f, bool clockwise = true)
        {
            float angle = clockwise ? -360f : 360f;
            rays.DORotate(new Vector3(0f, 0f, angle), duration, RotateMode.FastBeyond360)
                .SetEase(Ease.Linear)
                .SetLoops(-1, LoopType.Incremental)
                .SetRelative(true);
        }
        private void OnButtonClicked()
        {
            activeButton.gameObject.SetActive(false);

            activePanel.DOKill();
            activePanel.DOAnchorPos(OffScreenPos(activePanel), slideOutDuration)
                .SetEase(slideOutEase)
                .OnComplete(() => activePanel.gameObject.SetActive(false));
        }
    }
}
