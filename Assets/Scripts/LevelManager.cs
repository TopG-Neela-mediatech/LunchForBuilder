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
            GameManager.Instance.OnMissionComplete += OnMissionComplete;
            GameManager.Instance.OnLevelLose += OnLevelLose;

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
        // Fired by CookingManager once a dish has been served. Missions 1-4 all show this game's own
        // win panel as the "Mission Complete" beat, regardless of build -- that's purely internal to
        // this mini-game. The 5th (final) completion is different: if this build is embedded in the
        // main PlaySchool app, hand off to ITS end-of-game panel instead of showing our own.
        private void OnMissionComplete(int missionIndex)
        {
            currentLevelIndex = missionIndex + 1;
            HelperGameCategoryDataSaver.LevelCompleted(currentLevelIndex);

            if (currentLevelIndex >= missions.Length)
            {
#if PLAYSCHOOL_MAIN
                EffectParticleControll.Instance.SpawnGameEndPanel();
                GameOverEndPanel.Instance.AddTheListnerRetryGame();
#else
                GameManager.Instance.EndPanelScript.ShowWin();
#endif
                return;
            }
            GameManager.Instance.EndPanelScript.ShowWin();
        }
        // Fired by CookingManager's timer running out -- currentLevelIndex is left untouched (this
        // mission wasn't completed), so RetryCurrentLevel() below restarts the exact same recipe.
        private void OnLevelLose()
        {
            GameManager.Instance.EndPanelScript.ShowLose();
        }
        private void SetDataSaver()
        {
            HelperGameCategoryDataSaver.Init(missions.Length);
            currentLevelIndex = Mathf.Clamp(HelperGameCategoryDataSaver.GetStartLevel(), 0, missions.Length - 1);
        }
        // Called by the win panel's Next button once it's dismissed. If there's another recipe
        // left, start it; otherwise every mission is done, so reload the scene -- simpler and more
        // reliable than manually resetting every piece of mission/station state left over from the win.
        public void LoadNextLevel()
        {
            if (currentLevelIndex < missions.Length)
                cookingManager?.StartMission(missions[currentLevelIndex], currentLevelIndex);
            else
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
        // Called by the lose panel's Retry button -- re-runs StartMission on the same recipe, which
        // cleans up and rebuilds everything (station, pantry, character, timer) the same way any
        // fresh mission start does.
        public void RetryCurrentLevel()
        {
            cookingManager?.StartMission(missions[currentLevelIndex], currentLevelIndex);
        }
        private void OnDestroy()
        {
            GameManager.Instance.OnLevelStart -= OnLevelStart;
            GameManager.Instance.OnMissionComplete -= OnMissionComplete;
            GameManager.Instance.OnLevelLose -= OnLevelLose;
            if (storyController != null) storyController.OnStoryFinished -= HandleStoryFinished;
        }
    }
}
