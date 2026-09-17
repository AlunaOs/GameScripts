using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

public class IntroMenuManager : MonoBehaviour
{
    [Header("Intro Settings & UI")]
    public GameObject introPanel;        // Drag 'Intro' panel here
    public CanvasGroup introCanvasGroup; // Drag Canvas Group on Intro panel here
    public GameObject mainInterface;     // Drag 'MainInterface' menu panel here

    [Header("Intro Timing Settings")]
    public float fadeInDuration = 2.0f;
    public float visibleDuration = 3.0f;
    public float fadeOutDuration = 1.5f;

    [Header("Loading Screen Settings & UI")]
    public GameObject loadingPanel;       // Drag 'LoadingPanel' here
    public CanvasGroup logoCanvasGroup;   // Drag Logo's Canvas Group here
    public Image progressBarFill;         // (Optional) Drag Image component with Image Type = Filled
    public string targetSceneName = "MainScene"; // Exact name of target scene
    public float pulseSpeed = 2.0f;       // Pulse speed for logo fade in/out

    [Header("Adaptive Loading Duration")]
    [Tooltip("Target minimum load time for high-end devices (seconds).")]
    public float minLoadTimeFastDevice = 5.0f;
    [Tooltip("Target minimum load time for low-end devices (seconds).")]
    public float minLoadTimeSlowDevice = 9.0f;

    private bool isLoading = false;

    void Start()
    {
        // Setup initial UI states
        if (mainInterface != null) mainInterface.SetActive(false);
        if (loadingPanel != null) loadingPanel.SetActive(false);

        if (introPanel != null && introCanvasGroup != null)
        {
            introPanel.SetActive(true);
            introCanvasGroup.alpha = 0; // Start invisible
            StartCoroutine(PlayIntroSequence());
        }
        else
        {
            // Safety fallback: show main menu immediately if intro references are missing
            if (mainInterface != null) mainInterface.SetActive(true);
        }
    }

    IEnumerator PlayIntroSequence()
    {
        // 1. Fade In Intro Logo
        float elapsed = 0;
        while (elapsed < fadeInDuration)
        {
            elapsed += Time.deltaTime;
            introCanvasGroup.alpha = Mathf.Lerp(0, 1, elapsed / fadeInDuration);
            yield return null;
        }
        introCanvasGroup.alpha = 1;

        // 2. Pause
        yield return new WaitForSeconds(visibleDuration);

        // 3. Fade Out Intro Logo
        elapsed = 0;
        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            introCanvasGroup.alpha = Mathf.Lerp(1, 0, elapsed / fadeOutDuration);
            yield return null;
        }
        introCanvasGroup.alpha = 0;

        // 4. Switch to Main Menu
        introPanel.SetActive(false);
        if (mainInterface != null)
        {
            mainInterface.SetActive(true);
        }
    }

    public void OnExitButtonPressed()
    {
        Debug.Log("Exiting Game...");
        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    // --- BUTTON EVENT METHOD ---
    public void LoadMainScene()
    {
        if (isLoading) return; // Prevents spam clicking

        Debug.Log("Play button clicked! Starting scene transition...");
        StartCoroutine(LoadSceneAsyncSequence());
    }

    private IEnumerator LoadSceneAsyncSequence()
    {
        isLoading = true;

        // 1. Hide Menu & Show Loading Panel
        if (mainInterface != null) mainInterface.SetActive(false);
        if (loadingPanel != null) loadingPanel.SetActive(true);

        // 2. Calculate dynamic minimum duration based on device performance
        float targetMinDuration = CalculateAdaptiveMinLoadTime();

        // 3. Start Asynchronous Background Loading
        AsyncOperation operation = SceneManager.LoadSceneAsync(targetSceneName);
        operation.allowSceneActivation = false;

        float timer = 0f;
        float displayedProgress = 0f;

        // 4. Smooth Loading Loop
        while (!operation.isDone)
        {
            timer += Time.deltaTime;

            // Normalize Unity's progress (0.0 to 0.9) to standard range (0.0 to 1.0)
            float realTargetProgress = Mathf.Clamp01(operation.progress / 0.9f);

            // Compute ideal minimum progress based on time elapsed
            float timeBasedProgress = Mathf.Clamp01(timer / targetMinDuration);

            // Progress bar moves toward whichever value is lower to prevent early jump, 
            // ensuring smooth animation over the target duration.
            float goalProgress = Mathf.Min(realTargetProgress, timeBasedProgress);

            // If actual load finishes before target duration, cap progress goal by time
            if (operation.progress >= 0.9f)
            {
                goalProgress = timeBasedProgress;
            }

            // Smoothly advance UI displayed progress
            displayedProgress = Mathf.MoveTowards(displayedProgress, goalProgress, Time.deltaTime * 0.8f);

            // Update Progress Bar UI Fill
            if (progressBarFill != null)
            {
                progressBarFill.fillAmount = displayedProgress;
            }

            // Pulse Logo Alpha while loading
            if (logoCanvasGroup != null)
            {
                logoCanvasGroup.alpha = Mathf.PingPong(timer * pulseSpeed, 1.0f);
            }

            // Trigger Scene Switch only when BOTH real loading and time duration complete
            if (operation.progress >= 0.9f && timer >= targetMinDuration && displayedProgress >= 0.99f)
            {
                if (logoCanvasGroup != null) logoCanvasGroup.alpha = 1.0f;
                if (progressBarFill != null) progressBarFill.fillAmount = 1.0f;

                yield return new WaitForSeconds(0.2f); // Short beat before transition
                operation.allowSceneActivation = true;
            }

            yield return null;
        }
    }

    /// <summary>
    /// Evaluates CPU cores and System RAM to scale load time dynamically.
    /// </summary>
    private float CalculateAdaptiveMinLoadTime()
    {
        int cores = SystemInfo.processorCount;
        int ramMB = SystemInfo.systemMemorySize;

        // Low-end device criteria: 4 cores or fewer, or less than 3GB RAM
        if (cores <= 4 || ramMB < 3000)
        {
            return minLoadTimeSlowDevice; // ~9s
        }
        // Mid-range device criteria: 6 cores or 3GB-6GB RAM
        else if (cores <= 6 || ramMB < 6000)
        {
            return Mathf.Lerp(minLoadTimeFastDevice, minLoadTimeSlowDevice, 0.5f); // ~7s
        }
        // High-end device
        return minLoadTimeFastDevice; // ~5s
    }
}