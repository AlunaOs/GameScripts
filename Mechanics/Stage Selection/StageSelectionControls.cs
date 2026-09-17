using UnityEngine;
using UnityEngine.SceneManagement;

public class StageSelectionControls : MonoBehaviour
{
    [Header("Scene Names")]
    [SerializeField] private string loadingSceneName = "LoadingScreenScene";

    [Header("Default Stage Names")]
    [SerializeField] private string stage2SceneName = "Stage2";
    [SerializeField] private string stage3SceneName = "Stage3";
    [SerializeField] private string stage4SceneName = "Stage4";

    public void ProceedToStage2()
    {
        LoadStage(stage2SceneName, 2);
    }

    public void ProceedToStage3()
    {
        LoadStage(stage3SceneName, 3);
    }

    public void ProceedToStage4()
    {
        LoadStage(stage4SceneName, 4);
    }

    public void ProceedToStageByName(string sceneName)
    {
        int stageNum = ExtractStageNumber(sceneName);
        LoadStage(sceneName, stageNum);
    }

    private void LoadStage(string targetSceneName, int stageNumber)
    {
        Time.timeScale = 1f;

        PlayerPrefs.SetInt("CurrentStageNumber", stageNumber);
        PlayerPrefs.SetInt("HasPlayedFirstTime", 1);

        PlayerPrefs.SetString("TargetSceneToLoad", targetSceneName);
        PlayerPrefs.Save();

        SceneManager.LoadScene(loadingSceneName);
    }

    private int ExtractStageNumber(string sceneName)
    {
        int stageNumber = 2; 
        
        string digitsOnly = System.Text.RegularExpressions.Regex.Match(sceneName, @"\d+").Value;
        if (!string.IsNullOrEmpty(digitsOnly))
        {
            int.TryParse(digitsOnly, out stageNumber);
        }

        return stageNumber;
    }
}