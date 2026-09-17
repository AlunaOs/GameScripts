using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

public class LoadingSceneManager : MonoBehaviour
{
    [Header("UI References")]
    public CanvasGroup logoCanvasGroup;
    public Image progressBarFill;

    [Header("Loading Settings")]
    public float pulseSpeed = 2.0f;
    public float minLoadTimeFastDevice = 5.0f;
    public float minLoadTimeSlowDevice = 9.0f;

    void Start()
    {
        string targetScene = PlayerPrefs.GetString("TargetSceneToLoad", "MainScene");
        StartCoroutine(LoadTargetSceneAsync(targetScene));
    }

    private IEnumerator LoadTargetSceneAsync(string sceneName)
    {
        float targetMinDuration = CalculateAdaptiveMinLoadTime();

        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
        operation.allowSceneActivation = false;

        float timer = 0f;
        float displayedProgress = 0f;

        while (!operation.isDone)
        {
            timer += Time.deltaTime;

            float realTargetProgress = Mathf.Clamp01(operation.progress / 0.9f);
            float timeBasedProgress = Mathf.Clamp01(timer / targetMinDuration);
            float goalProgress = Mathf.Min(realTargetProgress, timeBasedProgress);

            if (operation.progress >= 0.9f)
            {
                goalProgress = timeBasedProgress;
            }

            displayedProgress = Mathf.MoveTowards(displayedProgress, goalProgress, Time.deltaTime * 0.8f);

            if (progressBarFill != null)
            {
                progressBarFill.fillAmount = displayedProgress;
            }

            if (logoCanvasGroup != null)
            {
                logoCanvasGroup.alpha = Mathf.PingPong(timer * pulseSpeed, 1.0f);
            }

            if (operation.progress >= 0.9f && timer >= targetMinDuration && displayedProgress >= 0.99f)
            {
                if (logoCanvasGroup != null) logoCanvasGroup.alpha = 1.0f;
                if (progressBarFill != null) progressBarFill.fillAmount = 1.0f;

                yield return new WaitForSeconds(0.2f);
                operation.allowSceneActivation = true;
            }

            yield return null;
        }
    }

    private float CalculateAdaptiveMinLoadTime()
    {
        int cores = SystemInfo.processorCount;
        int ramMB = SystemInfo.systemMemorySize;

        if (cores <= 4 || ramMB < 3000) return minLoadTimeSlowDevice; // ~9s
        else if (cores <= 6 || ramMB < 6000) return Mathf.Lerp(minLoadTimeFastDevice, minLoadTimeSlowDevice, 0.5f); // ~7s
        return minLoadTimeFastDevice; // ~5s
    }
}