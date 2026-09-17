using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class StageClearManager : MonoBehaviour
{
    [Header("UI Canvas References")]
    [Tooltip("Drag your StageClear_Panel GameObject here (initially deactivated).")]
    [SerializeField] private GameObject stageClearPanel;

    [Tooltip("Drag your Player Controls Canvas / HUD (Joystick, Jump, Sprint, Crouch).")]
    [SerializeField] private GameObject playerControlsCanvas;

    [Header("Timing & Game State Settings")]
    [Tooltip("Delay in seconds after the 3rd star finishes animating before showing Stage Clear.")]
    [SerializeField] private float delayBeforeStageClear = 2.5f;

    [Tooltip("Pause the game time scale when Stage Clear panel pops up?")]
    [SerializeField] private bool pauseGameOnClear = true;

    [Header("Level Navigation")]
    [Tooltip("Type the exact scene name of the next level to load. Leave empty to automatically load the next scene index in Build Settings.")]
    [SerializeField] private string nextSceneName = "";

    [Tooltip("Fallback scene to load if no next level exists in Build Settings (e.g., 'MainMenu').")]
    [SerializeField] private string fallbackMainMenuScene = "";

    [Header("Telemetry Panel Reference")]
    [SerializeField] private TelemetryUIPanel telemetryPanelController;

    [Header("Required Stars To Clear Stage")]
    [Tooltip("How many stars must be collected before Stage Clear triggers.")]
    [SerializeField] private int starsRequired = 3;

    private int collectedStarsCount = 0;
    private bool isStageCleared = false;

    private void Start()
    {
        // Ensure time scale is normal on scene load
        Time.timeScale = 1f;

        // Hide StageClear_Panel at start
        if (stageClearPanel != null)
        {
            stageClearPanel.SetActive(false);
        }

        // Enable HUD controls on level start
        if (playerControlsCanvas != null)
        {
            playerControlsCanvas.SetActive(true);
        }
    }

    /// <summary>
    /// Call this method every time a star finishes animating/filling.
    /// This is the ONLY thing that should advance progress toward Stage Clear.
    /// </summary>
    public void NotifyStarFilled()
    {
        if (isStageCleared) return;

        collectedStarsCount++;

        Debug.Log($"StageClearManager: Star filled. Progress = {collectedStarsCount}/{starsRequired}");

        if (collectedStarsCount >= starsRequired)
        {
            isStageCleared = true;
            StartCoroutine(TriggerStageClearSequence());
        }
    }

    private IEnumerator TriggerStageClearSequence()
    {
        yield return new WaitForSecondsRealtime(delayBeforeStageClear);

        // Export telemetry session log file when stage clear completes
        if (TelemetryManager.Instance != null)
        {
            TelemetryManager.Instance.ExportSessionJSON();
        }

        if (playerControlsCanvas != null) playerControlsCanvas.SetActive(false);
        if (stageClearPanel != null) stageClearPanel.SetActive(true);
        if (pauseGameOnClear) Time.timeScale = 0f;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
    public void OnProceedButtonClicked()
    {
        Time.timeScale = 1f;

        // Increment stage and save persistently
        int currentStage = PlayerPrefs.GetInt("CurrentStageNumber", 1);
        PlayerPrefs.SetInt("CurrentStageNumber", currentStage + 1);
        PlayerPrefs.Save();

        if (!string.IsNullOrEmpty(nextSceneName))
        {
            SceneManager.LoadScene(nextSceneName);
        }
        else
        {
            int nextSceneIndex = SceneManager.GetActiveScene().buildIndex + 1;
            if (nextSceneIndex < SceneManager.sceneCountInBuildSettings)
            {
                SceneManager.LoadScene(nextSceneIndex);
            }
            else if (!string.IsNullOrEmpty(fallbackMainMenuScene))
            {
                SceneManager.LoadScene(fallbackMainMenuScene);
            }
        }
    }
}