using UnityEngine;
using TMPro;
using System.Collections;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class MenuManager : MonoBehaviour
{
    [Header("Game Objects")]
    public GameObject mainInterface;
    public GameObject menuCamera;

    [Header("Loading UI")]
    public GameObject loadingScreen;
    public TextMeshProUGUI progressText;
    public Image fillLogoImage;
    public TextMeshProUGUI tipText;

    [Header("Name Warning Popup")]
    public GameObject nameWarningPopup;
    public TextMeshProUGUI nameWarningText;
    public Button nameWarningOkButton;

    [Header("Dynamic Colors")]
    public Color startColor = Color.black;
    public Color midColor = Color.red;
    public Color endColor = Color.white;

    [Header("Pulse Settings")]
    public float pulseSpeed = 3f;
    public float pulseIntensity = 0.2f;

    [Header("Loading Duration Settings")]
    public float minLoadingTime = 3f;
    public float maxLoadingTime = 8f;
    public bool useRandomDelay = true;
    public float randomDelayVariation = 1f;

    [Header("Tip Text Settings")]
    public bool enableAutoSizing = true;
    public float tipMinFontSize = 16f;
    public float tipMaxFontSize = 36f;
    public float tipLineSpacing = 1.3f;
    public bool enableWordWrapping = true;
    public float tipSwitchInterval = 2f;
    public bool shuffleTips = true;

    [Header("Loading Tips (Trivia)")]
    public string[] loadingTips = new string[]
    {
        "Tip: Answer quickly for bonus points!",
        "Tip: Use the undo button to fix mistakes",
        "Tip: Harder questions give more rewards",
        "Tip: Practice makes perfect!",
        "Tip: Geometry questions test spatial thinking",
        "Tip: Algebra helps with problem-solving skills",
        "Tip: 3 correct answers in a row increases difficulty!",
        "Tip: Take your time - there's no penalty for thinking",
        "Tip: Hindrance buttons are distractions! Choose wisely",
        "Tip: MathSoleum means 'Math + Mausoleum'",
        "Tip: The answer pieces can be selected in any order",
        "Tip: Use the Undo button if you make a mistake",
        "Tip: Fast answers help increase difficulty faster",
        "Tip: Explore the world to find more puzzles",
        "Tip: Complete stages to unlock harder challenges",
        "Tip: Your performance affects question difficulty",
        "Tip: 100 percent correct answers = highest difficulty",
        "Tip: Algebra questions test your equation skills"
    };

    private float tipSwitchTimer = 0;
    private int currentTipIndex = 0;
    private string[] shuffledTips;

    void Start()
    {
        if (fillLogoImage != null)
            fillLogoImage.color = startColor;

        SetupTipText();
        InitializeTips();

        // Setup name warning popup
        if (nameWarningPopup != null)
            nameWarningPopup.SetActive(false);
        if (nameWarningOkButton != null)
            nameWarningOkButton.onClick.AddListener(() => nameWarningPopup.SetActive(false));
    }

    void SetupTipText()
    {
        if (tipText == null) return;

        if (enableAutoSizing)
        {
            tipText.enableAutoSizing = true;
            tipText.fontSizeMin = tipMinFontSize;
            tipText.fontSizeMax = tipMaxFontSize;
        }

        tipText.lineSpacing = tipLineSpacing;
        tipText.textWrappingMode = TextWrappingModes.Normal;
        tipText.alignment = TextAlignmentOptions.Center;
        tipText.margin = new Vector4(20, 0, 20, 0);
    }

    void InitializeTips()
    {
        if (loadingTips == null || loadingTips.Length == 0) return;

        if (shuffleTips)
        {
            shuffledTips = (string[])loadingTips.Clone();
            for (int i = 0; i < shuffledTips.Length; i++)
            {
                string temp = shuffledTips[i];
                int randomIndex = Random.Range(i, shuffledTips.Length);
                shuffledTips[i] = shuffledTips[randomIndex];
                shuffledTips[randomIndex] = temp;
            }
        }
        else
        {
            shuffledTips = loadingTips;
        }

        if (tipText != null && shuffledTips.Length > 0)
        {
            tipText.text = shuffledTips[0];
            currentTipIndex = 0;
        }
    }

    void UpdateTip()
    {
        if (tipText == null || shuffledTips == null || shuffledTips.Length == 0) return;

        currentTipIndex = (currentTipIndex + 1) % shuffledTips.Length;
        tipText.text = shuffledTips[currentTipIndex];
        StartCoroutine(FadeTip());
    }

    IEnumerator FadeTip()
    {
        if (tipText == null) yield break;

        Color originalColor = tipText.color;
        float elapsed = 0;
        float fadeDuration = 0.2f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(1, 0, elapsed / fadeDuration);
            tipText.color = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);
            yield return null;
        }

        elapsed = 0;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(0, 1, elapsed / fadeDuration);
            tipText.color = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);
            yield return null;
        }

        tipText.color = originalColor;
    }

    public void StartGame()
    {
        string playerName = PlayerPrefs.GetString("PlayerName", "Player");
        if (string.IsNullOrEmpty(playerName) || playerName == "Player")
        {
            ShowNameWarning();
            return;
        }

        StartCoroutine(LoadLevelProgress());
    }

    public void ShowNameWarning()
    {
        if (nameWarningPopup != null && nameWarningText != null)
        {
            nameWarningText.text = "Please register your name in the Account panel before starting the game!";
            nameWarningPopup.SetActive(true);
        }
        else
        {
            Debug.LogWarning("Name warning popup not set. Cannot start game without a registered name.");
        }
    }

    IEnumerator LoadLevelProgress()
    {
        loadingScreen.SetActive(true);

        float startTime = Time.time;
        float progress = 0;
        float pulseTimer = 0;

        tipSwitchTimer = 0;

        float totalDuration = CalculateLoadingDuration();

        Debug.Log($"Loading duration: {totalDuration} seconds");

        while (progress < 1f)
        {
            float elapsedTime = Time.time - startTime;
            float rawProgress = Mathf.Clamp01(elapsedTime / totalDuration);
            progress = Mathf.SmoothStep(0, 1, rawProgress);

            float percentage = progress * 100f;
            progressText.text = percentage.ToString("F0") + "%";

            pulseTimer += Time.deltaTime * pulseSpeed;

            tipSwitchTimer += Time.deltaTime;
            if (tipSwitchTimer >= tipSwitchInterval)
            {
                tipSwitchTimer = 0;
                UpdateTip();
            }

            UpdateFillLogoDynamicPulse(progress, pulseTimer);

            yield return null;
        }

        progressText.text = "100%";
        if (fillLogoImage != null)
            fillLogoImage.color = endColor;

        if (tipText != null)
        {
            tipText.text = "Ready to begin your adventure!";
            StartCoroutine(FadeTip());
        }

        yield return new WaitForSeconds(0.3f);

        if (mainInterface != null) mainInterface.SetActive(false);
        if (menuCamera != null) menuCamera.SetActive(false);

        loadingScreen.SetActive(false);
    }

    float CalculateLoadingDuration()
    {
        float duration = minLoadingTime;

        if (useRandomDelay)
        {
            duration += Random.Range(0, randomDelayVariation);
        }

        float deviceFactor = CalculateDevicePerformanceFactor();
        duration += deviceFactor;

        duration = Mathf.Clamp(duration, minLoadingTime, maxLoadingTime);

        return duration;
    }

    float CalculateDevicePerformanceFactor()
    {
        float fps = 1f / Time.deltaTime;

        if (fps < 30)
            return 2f;
        else if (fps < 45)
            return 1f;
        else if (fps > 90)
            return 0f;
        else
            return 0.5f;
    }

    public void LoadSceneWithLoadingScreen(string sceneName)
    {
        // Block scene loading if name is not set
        string playerName = PlayerPrefs.GetString("PlayerName", "Player");
        if (string.IsNullOrEmpty(playerName) || playerName == "Player")
        {
            ShowNameWarning();
            return;
        }

        StartCoroutine(LoadSceneAsyncRoutine(sceneName));
    }

    private IEnumerator LoadSceneAsyncRoutine(string sceneName)
    {
        loadingScreen.SetActive(true);

        float startTime = Time.time;
        float progress = 0;
        float pulseTimer = 0;
        tipSwitchTimer = 0;

        float totalDuration = CalculateLoadingDuration();

        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
        asyncLoad.allowSceneActivation = false;

        while (progress < 1f)
        {
            float elapsedTime = Time.time - startTime;
            float rawProgress = Mathf.Clamp01(elapsedTime / totalDuration);

            float asyncProgress = Mathf.Clamp01(asyncLoad.progress / 0.9f);

            progress = Mathf.SmoothStep(0, 1, rawProgress);

            float percentage = progress * 100f;
            progressText.text = percentage.ToString("F0") + "%";

            pulseTimer += Time.deltaTime * pulseSpeed;
            tipSwitchTimer += Time.deltaTime;

            if (tipSwitchTimer >= tipSwitchInterval)
            {
                tipSwitchTimer = 0;
                UpdateTip();
            }

            UpdateFillLogoDynamicPulse(progress, pulseTimer);
            yield return null;
        }

        progressText.text = "100%";
        if (fillLogoImage != null) fillLogoImage.color = endColor;

        yield return new WaitForSeconds(0.3f);

        asyncLoad.allowSceneActivation = true;
    }

    void UpdateFillLogoDynamicPulse(float progress, float pulseTimer)
    {
        if (fillLogoImage == null) return;

        Color baseColor;

        if (progress < 0.5f)
        {
            float t = progress / 0.5f;
            t = Mathf.SmoothStep(0, 1, t);
            baseColor = Color.Lerp(startColor, midColor, t);
        }
        else
        {
            float t = (progress - 0.5f) / 0.5f;
            t = Mathf.SmoothStep(0, 1, t);
            baseColor = Color.Lerp(midColor, endColor, t);
        }

        float pulse = 1 + (Mathf.Sin(pulseTimer) * pulseIntensity);

        Color finalColor = baseColor * pulse;
        finalColor.r = Mathf.Clamp01(finalColor.r);
        finalColor.g = Mathf.Clamp01(finalColor.g);
        finalColor.b = Mathf.Clamp01(finalColor.b);
        finalColor.a = 1f;

        fillLogoImage.color = finalColor;
    }
}