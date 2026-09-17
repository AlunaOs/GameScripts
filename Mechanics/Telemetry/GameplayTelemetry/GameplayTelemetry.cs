using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;

public class GameplayTelemetry : MonoBehaviour
{
    public static GameplayTelemetry Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private GameObject telemetryHUDPanel; // The toggleable panel
    [SerializeField] private TMP_Text telemetryContentText;   // The single text field
    [SerializeField] private Button toggleButton;             // The sliding button

    [Header("Animation Settings")]
    [SerializeField] private float slideDuration = 0.3f;      // How fast it slides
    [SerializeField] private float rightXPosition = 50f;      // Resting position on the far right of the screen
    [SerializeField] private float leftXPosition = -250f;     // Position next to the panel edge when open (adjust as needed)

    private RectTransform buttonRect;
    private bool isOpen = false;

    // Tracking variables (Cumulative across puzzles in the session)
    private string session_code;
    private string currentTopic = "N/A";
    private string currentTier = "easy";
    private int correctPerPuzzleCount = 0;
    private float currentResponseTimeMs = 0f;
    private bool hintUsedThisPuzzle = false;
    private int currentAttemptNumber = 1;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        if (toggleButton != null)
        {
            buttonRect = toggleButton.GetComponent<RectTransform>();
            toggleButton.onClick.AddListener(ToggleTelemetryHUD);
        }

        session_code = "MS-" + UnityEngine.Random.Range(1000, 9999);
    }

    private void Start()
    {
        // Force-hide the panel on startup
        isOpen = false;
        if (telemetryHUDPanel != null)
        {
            telemetryHUDPanel.SetActive(false);
        }

        // Set initial button position to the far right
        if (buttonRect != null)
        {
            Vector2 pos = buttonRect.anchoredPosition;
            pos.x = rightXPosition;
            buttonRect.anchoredPosition = pos;
        }

        UpdateHUDDisplay();
    }

    public void ToggleTelemetryHUD()
    {
        isOpen = !isOpen;

        if (telemetryHUDPanel != null)
        {
            telemetryHUDPanel.SetActive(isOpen);
            if (isOpen)
            {
                UpdateHUDDisplay();
            }
        }

        // Stop any ongoing slides and animate the button position
        StopAllCoroutines();
        float targetX = isOpen ? leftXPosition : rightXPosition;
        StartCoroutine(SlideButton(targetX));
    }

    IEnumerator SlideButton(float targetX)
    {
        if (buttonRect == null) yield break;

        float startX = buttonRect.anchoredPosition.x;
        float elapsed = 0f;

        while (elapsed < slideDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / slideDuration);
            t = t * t * (3f - 2f * t);

            Vector2 pos = buttonRect.anchoredPosition;
            pos.x = Mathf.Lerp(startX, targetX, t);
            buttonRect.anchoredPosition = pos;
            yield return null;
        }

        Vector2 finalPos = buttonRect.anchoredPosition;
        finalPos.x = targetX;
        buttonRect.anchoredPosition = finalPos;
    }

    public void BeginPuzzle(string topic, int tierLevel)
    {
        currentTopic = topic;
        currentTier = tierLevel == 1 ? "easy" : (tierLevel == 2 ? "medium" : "hard");
        currentAttemptNumber = 1;
        UpdateHUDDisplay();
    }

    public void ResetSessionTelemetry()
    {
        correctPerPuzzleCount = 0;
        currentResponseTimeMs = 0f;
        hintUsedThisPuzzle = false;
        currentAttemptNumber = 1;
        session_code = "MS-" + UnityEngine.Random.Range(1000, 9999);
        UpdateHUDDisplay();
    }

    // Updates the current question tier level dynamically during gameplay
    public void LogQuestionTier(int tierLevel)
    {
        currentTier = tierLevel == 1 ? "easy" : (tierLevel == 2 ? "medium" : "hard");
        UpdateHUDDisplay();
    }

    // Tracks which attempt (1-3) the player is currently executing on this question
    public void LogAttemptNumber(int attemptNum)
    {
        currentAttemptNumber = Mathf.Clamp(attemptNum, 1, 3);
        UpdateHUDDisplay();
    }

    public void LogAttempt(bool isCorrect, float responseTimeSec, bool hintUsed)
    {
        if (isCorrect)
        {
            correctPerPuzzleCount++;
        }
        currentResponseTimeMs = Mathf.Round(responseTimeSec * 1000f);
        if (hintUsed)
        {
            hintUsedThisPuzzle = true;
        }

        UpdateHUDDisplay();
    }

    private void UpdateHUDDisplay()
    {
        if (telemetryContentText == null) return;

        telemetryContentText.text = 
            $"Session Code: {session_code}\n" +
            $"Topic: {currentTopic}\n" +
            $"Tier: {currentTier}\n" +
            $"Attempt: {currentAttemptNumber}/3\n" +
            $"Correct/Puzzle: {correctPerPuzzleCount}\n" +
            $"Response Time: {currentResponseTimeMs} ms\n" +
            $"Hint Used: {(hintUsedThisPuzzle ? "Yes" : "No")}";
    }
}