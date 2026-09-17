using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuControls : MonoBehaviour
{
    [Header("Scene Transition Settings")]
    [SerializeField] private string targetSceneName = "MainScene";
    [SerializeField] private string loadingSceneName = "LoadingScreenScene";

    public void GoToMainMenu()
    {
        // 1. Unpause time scale
        Time.timeScale = 1f;

        // 2. Save stage progress
        int currentStage = PlayerPrefs.GetInt("CurrentStageNumber", 1);
        PlayerPrefs.SetInt("CurrentStageNumber", currentStage + 1);
        PlayerPrefs.SetInt("HasPlayedFirstTime", 1);

        // 3. Store target scene destination for LoadingSceneManager
        PlayerPrefs.SetString("TargetSceneToLoad", targetSceneName);
        PlayerPrefs.Save();

        // 4. Load the dedicated loading screen scene immediately
        SceneManager.LoadScene(loadingSceneName);
    }
}