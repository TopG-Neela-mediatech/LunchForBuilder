using System;
using UnityEngine;

namespace tmkoc.lunchforbuilders
{
    public class GameManager : MonoBehaviour
    {
        [SerializeField] private LevelManager levelManager;     
        private static GameManager instance;


        public static GameManager Instance { get { return instance; } }
        public LevelManager LevelManager { get { return levelManager; } }
     
      
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
        public void InvokeLevelStart() => OnLevelStart?.Invoke();
        public void InvokeLevelWin() => OnLevelWin?.Invoke();      
        #endregion
    }
}
