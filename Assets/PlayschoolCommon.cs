using UnityEngine;


public class PlayschoolCommon : MonoBehaviour
{
    public static PlayschoolCommon Instance;

    [SerializeField] private GameObject playschoolWinLosePanel;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
    }

    public void SpawnplayschoolWinLosePanel()
    {

        Instantiate(playschoolWinLosePanel);

    }


}

