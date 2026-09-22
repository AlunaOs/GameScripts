using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using TMPro;
using System.IO;
using System.Collections;
using System.Collections.Generic;

public class ScalePuzzleManager : MonoBehaviour
{
    [Header("UI Display Links")]
    public TextMeshProUGUI txtQuestionDisplay;
    public TextMeshProUGUI txtAttemptsDisplay;
    public TextMeshProUGUI txtCorrectDisplay;

    [Header("Question Sliding Panel UI")]
    [SerializeField] private GameObject questionPanel;
    [SerializeField] private Button btnOpenQuestion;
    [SerializeField] private Button btnCloseQuestion;
    [SerializeField] private float questionAnimDuration = 0.3f;

    [Tooltip("Drag the Master Balancing Scale Puzzle Canvas Panel here.")]
    public GameObject puzzleCanvasPanel;
    [Tooltip("Drag your Player Controls Canvas / On-screen Joystick UI object here.")]
    public GameObject playerControlsCanvas;

    [Header("Feedback Panel System")]
    [SerializeField] private GameObject feedbackPanel;
    [SerializeField] private TextMeshProUGUI feedbackText;
    [SerializeField] private GameObject iconCorrect;
    [SerializeField] private GameObject iconWrong;
    [SerializeField] private float feedbackDisplayDuration = 1.5f;

    [Header("Pan Weight Texts")]
    public TextMeshProUGUI txtLeftPanWeight;
    public TextMeshProUGUI txtRightPanWeight;

    [Header("Choice Buttons")]
    public List<Button> choiceButtons = new List<Button>();

    [Header("Button Visual Feedback Settings")]
    public Color colorBtnPress = new Color(0.8f, 0.9f, 1f, 1f);
    public Color colorCorrectHighlight = new Color(0f, 0.96f, 0.63f, 1f);
    public float buttonPressScale = 0.88f;

    [Header("Hint Prefab Reference")]
    public HintScrollUI hintScrollUI;

    [Header("Connected Moving Block Settings")]
    public Transform movingBlock;
    public float targetRiseHeight = 5.0f;
    public float liftSpeed = 2.0f;

    [Header("Vibration/Shake Settings")]
    [SerializeField] private float shakeMagnitude = 15.0f;
    [SerializeField] private float shakeDuration = 0.5f;

    [Header("Star Reward Controller")]
    public StarRewardAnimation starRewardAnimator;
    public RectTransform[] targetStarSlots;

    [Range(1, 3)]
    public int starToRewardNumber = 1;

    [Header("PMP Dataset")]
    public TextAsset puzzleDatasetJson;
    public string streamingAssetsFileName = "ScalePuzzleData.json";

    [Header("DDA / PMP Settings")]
    [SerializeField] private int correctAnswersRequired = 3;
    public DifficultyNotifier difficultyNotifier;

    [HideInInspector]
    public bool isCompleted = false;

    // The shared DDA (inside GameManager) is the single source of truth.
    // The old private ScaleDDAController has been removed.
    private static readonly ScaleQuestionGenerator generator = new ScaleQuestionGenerator();

    private ScaleQuestion currentQuestion;
    private float questionStartTime;
    private int attemptsLeft = 3;
    private const int maxAttempts = 3;
    private int correctAnswersGiven = 0;

    private Coroutine highlightCoroutine;
    private Coroutine feedbackCoroutine;
    private bool isWaitingForNextQuestion = false;

    private RectTransform questionRectTransform;
    private Vector2 questionOriginalAnchoredPos;
    private Coroutine questionAnimCoroutine;

    private bool hintUsedThisPuzzle = false;

    private void Awake()
    {
        InitializeDataset();
        HideFeedbackPanel();

        if (questionPanel != null)
        {
            questionRectTransform = questionPanel.GetComponent<RectTransform>();
            if (questionRectTransform != null)
                questionOriginalAnchoredPos = questionRectTransform.anchoredPosition;
        }
    }

    private void Start()
    {
        if (btnOpenQuestion != null) btnOpenQuestion.onClick.AddListener(OpenQuestionPanel);
        if (btnCloseQuestion != null) btnCloseQuestion.onClick.AddListener(CloseQuestionPanel);
    }

    private void InitializeDataset()
    {
        if (!generator.IsLoaded)
        {
            if (puzzleDatasetJson != null)
                generator.LoadFromJson(puzzleDatasetJson.text);
            else
                StartCoroutine(LoadDatasetFromStreamingAssets());
        }
    }

    private IEnumerator LoadDatasetFromStreamingAssets()
    {
        string path = Path.Combine(Application.streamingAssetsPath, streamingAssetsFileName);

#if UNITY_ANDROID && !UNITY_EDITOR
        using (UnityWebRequest request = UnityWebRequest.Get(path))
        {
            yield return request.SendWebRequest();
            if (request.result == UnityWebRequest.Result.Success)
                generator.LoadFromJson(request.downloadHandler.text);
            else
                Debug.LogError($"[ScalePuzzleManager] JSON load failed: {path} — {request.error}");
        }
#else
        if (File.Exists(path))
        {
            string jsonText = File.ReadAllText(path);
            generator.LoadFromJson(jsonText);
        }
        else
        {
            Debug.LogError($"[ScalePuzzleManager] Missing file: {path}");
        }
        yield return null;
#endif
    }

    public void StartPuzzle()
    {
        if (isCompleted) return;

        isWaitingForNextQuestion = false;

        if (playerControlsCanvas != null) playerControlsCanvas.SetActive(false);
        if (puzzleCanvasPanel != null) puzzleCanvasPanel.SetActive(true);

        StartCoroutine(StartPuzzleRoutine());
    }

    private IEnumerator StartPuzzleRoutine()
    {
        if (!generator.IsLoaded) InitializeDataset();

        int timeoutFrames = 0;
        while (!generator.IsLoaded && timeoutFrames < 100)
        {
            timeoutFrames++;
            yield return null;
        }

        if (!generator.IsLoaded)
        {
            Debug.LogError("[ScalePuzzleManager] Failed to load puzzle dataset!");
            yield break;
        }

        isWaitingForNextQuestion = false;
        attemptsLeft = maxAttempts;

        if (currentQuestion == null)
            GenerateNewQuestion(preferEasierVariant: false);
        else
        {
            ApplyQuestionToUI();
            questionStartTime = Time.time;
        }

        UpdateStatusUI();
        SetButtonsInteractable(true);
        CheckAndUnlockHint();
        OpenQuestionPanel();
    }

    // ── QUESTION PANEL SLIDING ANIMATIONS ──────────────────────────────────────
    public void OpenQuestionPanel()
    {
        if (questionPanel != null && questionRectTransform != null)
        {
            if (questionAnimCoroutine != null) StopCoroutine(questionAnimCoroutine);
            questionAnimCoroutine = StartCoroutine(SlideInQuestionPanel());
        }
    }

    public void CloseQuestionPanel()
    {
        if (questionPanel != null && questionPanel.activeSelf && questionRectTransform != null)
        {
            if (questionAnimCoroutine != null) StopCoroutine(questionAnimCoroutine);
            questionAnimCoroutine = StartCoroutine(SlideOutQuestionPanel());
        }
    }

    private IEnumerator SlideInQuestionPanel()
    {
        questionPanel.SetActive(true);

        float offscreenX = Screen.width;
        if (questionRectTransform.parent != null)
            offscreenX = ((RectTransform)questionRectTransform.parent).rect.width;

        Vector2 startPos = new Vector2(offscreenX, questionOriginalAnchoredPos.y);
        Vector2 targetPos = questionOriginalAnchoredPos;
        questionRectTransform.anchoredPosition = startPos;

        float elapsed = 0f;
        while (elapsed < questionAnimDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / questionAnimDuration;
            t = t * t * (3f - 2f * t);
            questionRectTransform.anchoredPosition = Vector2.Lerp(startPos, targetPos, t);
            yield return null;
        }
        questionRectTransform.anchoredPosition = targetPos;
    }

    private IEnumerator SlideOutQuestionPanel()
    {
        float offscreenX = Screen.width;
        if (questionRectTransform.parent != null)
            offscreenX = ((RectTransform)questionRectTransform.parent).rect.width;

        Vector2 startPos = questionRectTransform.anchoredPosition;
        Vector2 targetPos = new Vector2(offscreenX, questionOriginalAnchoredPos.y);

        float elapsed = 0f;
        while (elapsed < questionAnimDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / questionAnimDuration;
            t = t * t * (3f - 2f * t);
            questionRectTransform.anchoredPosition = Vector2.Lerp(startPos, targetPos, t);
            yield return null;
        }
        questionRectTransform.anchoredPosition = targetPos;
        questionPanel.SetActive(false);
    }

private void GenerateNewQuestion(bool preferEasierVariant)
{
    int tier = (GameManager.Instance != null) ? GameManager.Instance.currentLevel : 1;

    DifficultyLevel scaleTier = (DifficultyLevel)Mathf.Clamp(tier, 1, 3);

    currentQuestion = generator.GetNextQuestion(scaleTier, preferEasierVariant);
    questionStartTime = Time.time;
    hintUsedThisPuzzle = false;
    ApplyQuestionToUI();

    Debug.Log($"[DDA] ScalePuzzleManager generated question for shared tier {tier} (enum={scaleTier})");

    if (GameplayTelemetry.Instance != null)
        GameplayTelemetry.Instance.BeginPuzzle("Linear Equations and Inequalities", tier);
}

    private void ApplyQuestionToUI()
    {
        if (txtQuestionDisplay != null) txtQuestionDisplay.text = currentQuestion.problemText;
        if (txtLeftPanWeight != null) txtLeftPanWeight.text = currentQuestion.leftPanText;
        if (txtRightPanWeight != null) txtRightPanWeight.text = currentQuestion.rightPanText;

        ConfigureChoiceButtons(currentQuestion.options);
    }

    private void ConfigureChoiceButtons(string[] options)
    {
        if (options == null || options.Length == 0) return;

        ResetChoiceButtonVisuals();

        string correct = currentQuestion.correctAnswer.Trim();
        List<string> selectedOptions = new List<string> { correct };

        List<string> distractors = new List<string>();
        foreach (var opt in options)
        {
            string trimmed = opt.Trim();
            if (trimmed != correct && !distractors.Contains(trimmed))
                distractors.Add(trimmed);
        }

        for (int i = distractors.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (distractors[i], distractors[j]) = (distractors[j], distractors[i]);
        }

        for (int i = 0; i < Mathf.Min(3, distractors.Count); i++)
            selectedOptions.Add(distractors[i]);

        for (int i = selectedOptions.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (selectedOptions[i], selectedOptions[j]) = (selectedOptions[j], selectedOptions[i]);
        }

        for (int i = 0; i < choiceButtons.Count; i++)
        {
            var btn = choiceButtons[i];
            if (btn == null) continue;

            if (i < selectedOptions.Count)
            {
                btn.gameObject.SetActive(true);
                btn.interactable = true;
                ConfigureChoiceButton(btn, selectedOptions[i]);
            }
            else
            {
                btn.gameObject.SetActive(false);
            }
        }
    }

    private void ConfigureChoiceButton(Button targetBtn, string label)
    {
        targetBtn.onClick.RemoveAllListeners();

        TMP_Text tmpText = targetBtn.GetComponentInChildren<TMP_Text>(true);
        if (tmpText != null) tmpText.text = label;

        Image btnImg = targetBtn.GetComponent<Image>();
        Transform btnTransform = targetBtn.transform;

        targetBtn.onClick.AddListener(() =>
        {
            if (gameObject.activeInHierarchy)
                StartCoroutine(AnimateButtonTap(btnTransform, btnImg, () => VerifyAnswer(label)));
            else
                VerifyAnswer(label);
        });
    }

    private IEnumerator AnimateButtonTap(Transform btnTransform, Image btnImage, System.Action onComplete)
    {
        if (btnTransform == null)
        {
            onComplete?.Invoke();
            yield break;
        }

        Vector3 originalScale = Vector3.one;
        Vector3 pressedScale = originalScale * buttonPressScale;
        Color originalColor = btnImage != null ? btnImage.color : Color.white;

        float elapsed = 0f;
        float pressDuration = 0.05f;
        if (btnImage != null) btnImage.color = colorBtnPress;

        while (elapsed < pressDuration)
        {
            elapsed += Time.deltaTime;
            btnTransform.localScale = Vector3.Lerp(originalScale, pressedScale, elapsed / pressDuration);
            yield return null;
        }

        elapsed = 0f;
        float bounceDuration = 0.1f;
        while (elapsed < bounceDuration)
        {
            elapsed += Time.deltaTime;
            btnTransform.localScale = Vector3.Lerp(pressedScale, originalScale, elapsed / bounceDuration);
            yield return null;
        }

        btnTransform.localScale = originalScale;
        if (btnImage != null) btnImage.color = originalColor;

        onComplete?.Invoke();
    }

    public void CheckAndUnlockHint()
    {
        if (ShopManagers.Instance != null && ShopManagers.Instance.hasHintScroll && !isCompleted)
            EnableHintButton();
    }

    public void EnableHintButton()
    {
        if (isCompleted) return;
        if (hintScrollUI == null) return;

        hintScrollUI.EnableHint(() =>
        {
            if (ShopManagers.Instance != null)
                ShopManagers.Instance.UseHintScroll();

            HighlightCorrectAnswer();
        });
    }

    public void HighlightCorrectAnswer()
    {
        if (currentQuestion == null || string.IsNullOrEmpty(currentQuestion.correctAnswer)) return;

        string targetAnswer = currentQuestion.correctAnswer.Trim().ToLower();

        foreach (Button btn in choiceButtons)
        {
            if (btn == null || !btn.gameObject.activeInHierarchy) continue;

            string btnText = "";
            TMP_Text tmp = btn.GetComponentInChildren<TMP_Text>();
            if (tmp != null) btnText = tmp.text.Trim().ToLower();

            if (btnText == targetAnswer)
            {
                if (highlightCoroutine != null) StopCoroutine(highlightCoroutine);
                highlightCoroutine = StartCoroutine(FlashCorrectButtonRoutine(btn));
                hintUsedThisPuzzle = true;
                break;
            }
        }
    }

    private IEnumerator FlashCorrectButtonRoutine(Button targetButton)
    {
        Image btnImg = targetButton.GetComponent<Image>();
        if (btnImg == null) yield break;

        Color originalColor = Color.white;
        try
        {
            for (int i = 0; i < 3; i++)
            {
                btnImg.color = colorCorrectHighlight;
                targetButton.transform.localScale = Vector3.one * 1.1f;
                yield return new WaitForSeconds(0.25f);

                btnImg.color = originalColor;
                targetButton.transform.localScale = Vector3.one;
                yield return new WaitForSeconds(0.2f);
            }
        }
        finally
        {
            if (btnImg != null) btnImg.color = originalColor;
            if (targetButton != null) targetButton.transform.localScale = Vector3.one;
        }
    }

    private void VerifyAnswer(string selectedValue)
    {
        if (isWaitingForNextQuestion || currentQuestion == null) return;

        if (highlightCoroutine != null)
        {
            StopCoroutine(highlightCoroutine);
            highlightCoroutine = null;
        }
        ResetChoiceButtonVisuals();

        float timeSpent = Time.time - questionStartTime;
        bool isCorrect = selectedValue.Trim() == currentQuestion.correctAnswer.Trim();

        if (GameplayTelemetry.Instance != null)
            GameplayTelemetry.Instance.LogAttempt(isCorrect, timeSpent, hintUsedThisPuzzle);

        // Capture tier BEFORE the shared DDA evaluates so we can fire the notifier.
        int levelBefore = (GameManager.Instance != null) ? GameManager.Instance.currentLevel : 1;

        if (GameManager.Instance != null)
        {
            // Single call — this drives the shared DDA, telemetry, persistence,
            // and the ported wrong-and-slow penalty.
            GameManager.Instance.TrackQuestionPerformance(
                currentQuestion.problemText,
                levelBefore,
                isCorrect,
                timeSpent
            );
        }

        int levelAfter = (GameManager.Instance != null) ? GameManager.Instance.currentLevel : levelBefore;

        if (levelBefore != levelAfter && difficultyNotifier != null)
        {
            if (levelAfter > levelBefore) difficultyNotifier.ShowIncrease();
            else difficultyNotifier.ShowDecrease();
        }

        // Decide whether to pick an easier variant on the next question.
        bool preferEasierVariant =
            GameManager.Instance != null && GameManager.Instance.GetLoseStreak() >= 2;

        if (isCorrect)
        {
            SetButtonsInteractable(false);
            correctAnswersGiven++;
            attemptsLeft = maxAttempts;
            UpdateStatusUI();

            if (RankManager.Instance != null)
                RankManager.Instance.ProcessPuzzleSuccess(
                    GameManager.Instance != null ? GameManager.Instance.GetSuccessRate() : 1f);

            if (correctAnswersGiven >= correctAnswersRequired)
                CompletePuzzle();
            else
                StartCoroutine(CorrectAnswerSequenceRoutine(preferEasierVariant));
        }
        else
        {
            attemptsLeft = Mathf.Max(0, attemptsLeft - 1);
            UpdateStatusUI();

            if (RankManager.Instance != null)
                RankManager.Instance.ProcessPuzzleFailure();

            if (attemptsLeft <= 0)
                StartCoroutine(HandleOutOfAttemptsRoutine(preferEasierVariant));
            else
                StartCoroutine(WrongAnswerSequenceRoutine(preferEasierVariant));
        }
    }

    private IEnumerator HandleOutOfAttemptsRoutine(bool preferEasierVariant)
    {
        isWaitingForNextQuestion = true;
        SetButtonsInteractable(false);

        ShowFeedback(false, "OUT OF ATTEMPTS! CLOSING PUZZLE...", 2.0f);
        yield return new WaitForSeconds(2.0f);

        GenerateNewQuestion(preferEasierVariant);
        ClosePuzzle();
    }

    private void CompletePuzzle()
    {
        // Re-entrancy guard: only reward once per completion.
        if (isCompleted) return;

        isCompleted = true;

        if (txtQuestionDisplay != null)
            txtQuestionDisplay.text = "<color=#00F5A0>SCALE BALANCED SUCCESSFULLY!</color>";

        if (ShopManagers.Instance != null)
        {
            ShopManagers.Instance.AddStars(1);
            ShopManagers.Instance.LoadPlayerStars();
        }

        StartCoroutine(CompletePuzzleSequenceRoutine());
    }

    IEnumerator CorrectAnswerSequenceRoutine(bool preferEasierVariant)
    {
        isWaitingForNextQuestion = true;

        ShowFeedback(true, $"CORRECT! ({correctAnswersGiven}/{correctAnswersRequired})", feedbackDisplayDuration);
        yield return new WaitForSeconds(feedbackDisplayDuration);

        isWaitingForNextQuestion = false;
        GenerateNewQuestion(preferEasierVariant);
        SetButtonsInteractable(true);
    }

    IEnumerator WrongAnswerSequenceRoutine(bool preferEasierVariant)
    {
        isWaitingForNextQuestion = true;
        SetButtonsInteractable(false);

        ShowFeedback(false, "WRONG ANSWER! TRY AGAIN...", feedbackDisplayDuration);

        Vector3 originalTextPosition = Vector3.zero;
        if (txtQuestionDisplay != null)
            originalTextPosition = txtQuestionDisplay.transform.localPosition;

        float elapsed = 0f;
        while (elapsed < shakeDuration)
        {
            elapsed += Time.deltaTime;
            float randomX = Random.Range(-1f, 1f) * shakeMagnitude;
            float randomY = Random.Range(-1f, 1f) * shakeMagnitude;

            if (txtQuestionDisplay != null)
                txtQuestionDisplay.transform.localPosition = originalTextPosition + new Vector3(randomX, randomY, 0f);

            yield return null;
        }

        if (txtQuestionDisplay != null)
            txtQuestionDisplay.transform.localPosition = originalTextPosition;

        float remainingDelay = Mathf.Max(0f, feedbackDisplayDuration - shakeDuration);
        yield return new WaitForSeconds(remainingDelay);

        isWaitingForNextQuestion = false;
        SetButtonsInteractable(true);
    }

    private void UpdateStatusUI()
    {
        if (txtAttemptsDisplay != null)
            txtAttemptsDisplay.text = $"ATTEMPTS: {attemptsLeft}/{maxAttempts}";

        if (txtCorrectDisplay != null)
            txtCorrectDisplay.text = $"CORRECT: {correctAnswersGiven}/{correctAnswersRequired}";
    }

    public void ShowFeedback(bool isCorrect, string message, float duration)
    {
        if (feedbackCoroutine != null)
            StopCoroutine(feedbackCoroutine);

        feedbackCoroutine = StartCoroutine(DisplayFeedbackRoutine(isCorrect, message, duration));
    }

    private IEnumerator DisplayFeedbackRoutine(bool isCorrect, string message, float duration)
    {
        if (feedbackPanel == null) yield break;

        feedbackPanel.SetActive(true);

        if (feedbackText != null)
            feedbackText.text = message;

        if (iconCorrect != null) iconCorrect.SetActive(isCorrect);
        if (iconWrong != null) iconWrong.SetActive(!isCorrect);

        yield return new WaitForSeconds(duration);
        HideFeedbackPanel();
    }

    public void HideFeedbackPanel()
    {
        if (feedbackPanel != null) feedbackPanel.SetActive(false);
        if (iconCorrect != null) iconCorrect.SetActive(false);
        if (iconWrong != null) iconWrong.SetActive(false);
    }

    IEnumerator CompletePuzzleSequenceRoutine()
    {
        bool isStarAnimationDone = false;

        if (starRewardAnimator != null)
        {
            int arrayIndex = Mathf.Clamp(starToRewardNumber - 1, 0, targetStarSlots.Length - 1);

            if (targetStarSlots != null && targetStarSlots.Length > arrayIndex && targetStarSlots[arrayIndex] != null)
                starRewardAnimator.PlayStarRewardSequence(targetStarSlots[arrayIndex], () => isStarAnimationDone = true);
            else
                starRewardAnimator.PlayStarRewardSequence(() => isStarAnimationDone = true);
        }
        else
        {
            isStarAnimationDone = true;
        }

        float timeoutTimer = 0f;
        while (!isStarAnimationDone && timeoutTimer < 2.0f)
        {
            timeoutTimer += Time.deltaTime;
            yield return null;
        }

        if (movingBlock != null)
        {
            Vector3 startPosition = movingBlock.localPosition;
            Vector3 targetPosition = startPosition + new Vector3(0, targetRiseHeight, 0);

            if (liftSpeed <= 0f) liftSpeed = 2.0f;

            while (Vector3.Distance(movingBlock.localPosition, targetPosition) > 0.01f)
            {
                movingBlock.localPosition = Vector3.MoveTowards(
                    movingBlock.localPosition, targetPosition, liftSpeed * Time.deltaTime);
                yield return null;
            }

            movingBlock.localPosition = targetPosition;
        }

        ClosePuzzle();
    }

    private void SetButtonsInteractable(bool state)
    {
        foreach (var btn in choiceButtons)
            if (btn != null) btn.interactable = state;
    }

    public void ResetChoiceButtonVisuals()
    {
        foreach (var btn in choiceButtons)
        {
            if (btn == null) continue;

            Image btnImg = btn.GetComponent<Image>();
            if (btnImg != null) btnImg.color = Color.white;

            btn.transform.localScale = Vector3.one;
        }
    }

    public void ResetPuzzleState()
    {
        attemptsLeft = maxAttempts;
        correctAnswersGiven = 0;
        currentQuestion = null;
        isWaitingForNextQuestion = false;
        isCompleted = false;

        if (highlightCoroutine != null)
        {
            StopCoroutine(highlightCoroutine);
            highlightCoroutine = null;
        }

        ResetChoiceButtonVisuals();
        SetButtonsInteractable(true);
        HideFeedbackPanel();
        UpdateStatusUI();
    }

    public void ClosePuzzle()
    {
        if (highlightCoroutine != null)
        {
            StopCoroutine(highlightCoroutine);
            highlightCoroutine = null;
        }

        ResetChoiceButtonVisuals();
        HideFeedbackPanel();
        isWaitingForNextQuestion = false;

        if (questionPanel != null && questionPanel.activeSelf && questionRectTransform != null)
        {
            if (questionAnimCoroutine != null) StopCoroutine(questionAnimCoroutine);
            questionAnimCoroutine = StartCoroutine(SlideOutQuestionPanel());
        }

        if (playerControlsCanvas != null) playerControlsCanvas.SetActive(true);
        if (puzzleCanvasPanel != null) puzzleCanvasPanel.SetActive(false);
    }

    public void RestorePlayerControls()
    {
        if (playerControlsCanvas != null) playerControlsCanvas.SetActive(true);
    }
}
