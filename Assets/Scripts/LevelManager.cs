using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace tmkoc.lunchforbuilders
{
    public class LevelManager : MonoBehaviour
    {
        [SerializeField] private Button playSchoolBackButton;

        [Header("Count & Cook Flow")]
        [SerializeField] private StoryController storyController;
        [SerializeField] private CookingManager cookingManager;
        [Tooltip("The 5 missions in play order: Refresh the Workers, Give Them More Energy, Build the Healthy Meal, Fix the Juice, The Final Meal.")]
        [SerializeField] private MissionRecipeData[] missions;

        // The mission index the player is currently on -- doubles as the resume point on a fresh
        // launch and the value persisted via HelperGameCategoryDataSaver.
        public int currentLevelIndex { get; private set; }
        private void StartLevel() => GameManager.Instance.InvokeLevelStart();

        // StoryController never hides its own canvas on finish/skip -- it only stops animating --
        // so whoever activated it is responsible for hiding it again.
        private void HandleStoryFinished()
        {
            if (storyController != null) storyController.gameObject.SetActive(false);
            StartLevel();
        }

        private void Awake()
        {
            if (storyController != null) storyController.OnStoryFinished += HandleStoryFinished;
            SetDataSaver();
            if (playSchoolBackButton != null)
                playSchoolBackButton.onClick.AddListener(() => SceneManager.LoadScene(TMKOCPlaySchoolConstants.TMKOCPlayMainMenu));
        }
        private void Start()
        {
            GameManager.Instance.OnLevelStart += OnLevelStart;
            GameManager.Instance.OnLevelWin += OnLevelWin;
            GameManager.Instance.OnMissionComplete += OnMissionComplete;

            // A returning player who already finished at least one mission skips straight back into
            // gameplay -- the storyboard (broken playground, tired workers) only ever plays once.
            if (currentLevelIndex > 0)
            {
                if (storyController != null) storyController.gameObject.SetActive(false);
                StartLevel();
            }
            else if (storyController != null) storyController.gameObject.SetActive(true);
            else StartLevel();
        }
        private void OnLevelStart()
        {
            cookingManager?.StartMission(missions[currentLevelIndex], currentLevelIndex);
        }
        // Fired by CookingManager once a dish has been served -- advance to the next mission, or
        // finish the game once the Playground's last repair is served.
        private void OnMissionComplete(int missionIndex)
        {
            currentLevelIndex = missionIndex + 1;
            HelperGameCategoryDataSaver.LevelCompleted(currentLevelIndex);

            if (currentLevelIndex >= missions.Length)
            {
                GameManager.Instance.InvokeLevelWin();
                return;
            }
            cookingManager?.StartMission(missions[currentLevelIndex], currentLevelIndex);
        }
        private void OnLevelWin()
        {
            GameManager.Instance.EndPanelScript.ShowWin();
        }
        private void SetDataSaver()
        {
            HelperGameCategoryDataSaver.Init(missions.Length);
            currentLevelIndex = Mathf.Clamp(HelperGameCategoryDataSaver.GetStartLevel(), 0, missions.Length - 1);
        }
        // Restarting the whole game -- reloading the scene is simpler and more reliable than trying
        // to manually reset every piece of mission/station state left over from the win.
        public void LoadNextLevel()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
        private void OnDestroy()
        {
            GameManager.Instance.OnLevelStart -= OnLevelStart;
            GameManager.Instance.OnLevelWin -= OnLevelWin;
            GameManager.Instance.OnMissionComplete -= OnMissionComplete;
            if (storyController != null) storyController.OnStoryFinished -= HandleStoryFinished;
        }
    }
}
