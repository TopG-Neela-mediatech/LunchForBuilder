using System;
using UnityEngine;

namespace tmkoc.lunchforbuilders
{
    public class GameManager : MonoBehaviour
    {
        [SerializeField] private LevelManager levelManager;
        [SerializeField] private EndPanelScript endPanelScript;
        [SerializeField] private TutorialManager tutorialManager;
        private static GameManager instance;


        public static GameManager Instance { get { return instance; } }
        public LevelManager LevelManager { get { return levelManager; } }
        public EndPanelScript EndPanelScript { get { return endPanelScript; } }
        public TutorialManager TutorialManager { get { return tutorialManager; } }
     
      
        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }


        #region Events
        public event Action OnLevelWin;
        public event Action OnLevelStart;
        public event Action OnLevelLose;

        // Count & Cook mission flow
        public event Action<int> OnMissionStarted;
        public event Action<string, bool> OnIngredientResolved;
        public event Action<int, int> OnRecipeProgress;
        public event Action<int> OnMissionComplete;
        public event Action<int> OnPeekUsed;

        public void InvokeLevelStart() => OnLevelStart?.Invoke();
        public void InvokeLevelWin() => OnLevelWin?.Invoke();
        public void InvokeLevelLose() => OnLevelLose?.Invoke();

        public void InvokeMissionStarted(int missionIndex) => OnMissionStarted?.Invoke(missionIndex);
        public void InvokeIngredientResolved(string ingredientId, bool wasCorrect) => OnIngredientResolved?.Invoke(ingredientId, wasCorrect);
        public void InvokeRecipeProgress(int placed, int required) => OnRecipeProgress?.Invoke(placed, required);
        public void InvokeMissionComplete(int missionIndex) => OnMissionComplete?.Invoke(missionIndex);
        public void InvokePeekUsed(int missionIndex) => OnPeekUsed?.Invoke(missionIndex);
        #endregion
    }
}
