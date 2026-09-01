using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneLoader : MonoBehaviour
{
    [SerializeField] private RectTransform canvasParent;
    [SerializeField] private Button buttonPrefab;
    [SerializeField] private List<string> sceneNames;
    [SerializeField] private float verticalSpacing = 120f;

    private void Start()
    {
        SpawnButtons();
    }

    private void SpawnButtons()
    {
        for (int i = 0; i < sceneNames.Count; i++)
        {
            string sceneName = sceneNames[i];
            Button button = Instantiate(buttonPrefab, canvasParent);
            button.gameObject.SetActive(true);

            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchoredPosition = new Vector2(0f, -i * verticalSpacing);

            TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
            {
                label.text = sceneName;
            }

            button.onClick.AddListener(() => LoadScene(sceneName));
        }
    }

    private void LoadScene(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }
}
