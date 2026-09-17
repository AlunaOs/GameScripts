using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine.Networking;

public class ScalePuzzleManager : BasePuzzleManager
{
    [Header("Display Links")]
    [SerializeField] private TextMeshProUGUI txtQuestionDisplay;
    [SerializeField] private TextMeshProUGUI txtAttemptsDisplay;
    [SerializeField] private TextMeshProUGUI txtCorrectDisplay;
    [SerializeField] private GameObject puzzleCanvasPanel;

    [Header("Question Sliding Panel")]
    [SerializeField] private GameObject questionPanel;
    [SerializeField] private Button btnOpenQuestion;
    [SerializeField] private Button btnCloseQuestion;
    [SerializeField] private float questionAnimDuration = 0.3f;

    [Header("Feedback System")]
    [SerializeField] private GameObject feedbackPanel;
    [SerializeField] private TextMeshProUGUI feedbackText;
    [SerializeField] private GameObject iconCorrect;
    [SerializeField] private GameObject iconWrong;
    [SerializeField] private float feedbackDisplayDuration = 1.5f;

    [Header("Pan Weights & Choices")]
    [SerializeField] private TextMeshProUGUI txtLeftPanWeight;
    [SerializeField] private TextMeshProUGUI txtRightPanWeight;
    [SerializeField] private List<Button> choiceButtons = new List<Button>();

    [Header("Visual Feedback")]
    [SerializeField] private Color colorBtnPress = new Color(0.8f, 0.9f, 1f, 1f);
    [SerializeField] private Color colorCorrectHighlight = new Color(0f, 0.96f, 0.63f, 1f);
    [SerializeField] private float buttonPressScale = 0.88f;

    [Header("Hint & Rewards")]
    [SerializeField] private HintScrollUI hintScrollUI;
    [SerializeField] private Transform movingBlock;
    [SerializeField] private float targetRiseHeight = 5.0f;
    [SerializeField] private float liftSpeed = 2.0f;

    [Header("Dataset & DDA")]
    [SerializeField] private TextAsset puzzleDatasetJson;
    [SerializeField] private string streamingAssetsFileName = "ScalePuzzleData.json";
    [SerializeField] private int correctAnswersRequired = 3;
    [SerializeField] private DifficultyNotifier difficultyNotifier;

    private static readonly ScaleDDAController dda = new ScaleDDAController();
    private static readonly ScaleQuestionGenerator generator = new ScaleQuestionGenerator();

    private ScaleQuestion currentQuestion;
    private int attemptsLeft = 3;
    private const int maxAttempts = 3;
    private int correctAnswersGiven = 0;
    
    private RectTransform questionRectTransform;
    private Vector2 questionOriginalPos;
    private Coroutine activeAnimCoroutine;
    private Coroutine feedbackCoroutine;
    private Coroutine highlightCoroutine;
    private bool isWaitingForNext = false;

    private void Awake()
    {
        InitializeDataset();
        HideFeedbackPanel();
        CachePanelRect();
    }

    private void Start()
    {
        btnOpenQuestion?.onClick.AddListener(OpenQuestionPanel);
        btnCloseQuestion?.onClick.AddListener(CloseQuestionPanel);
    }

    private void CachePanelRect()
    {
        if (questionPanel != null)
        {
            questionRectTransform = questionPanel.GetComponent<RectTransform>();
            if (questionRectTransform != null)
                questionOriginalPos = questionRectTransform.anchoredPosition;
        }
    }

    private void InitializeDataset()
    {
        if (generator.IsLoaded) return;

        if (puzzleDatasetJson != null)
        {
            generator.LoadFromJson(puzzleDatasetJson.text);
        }
        else
        {
            StartCoroutine(LoadDatasetFromStreamingAssets());
        }
    }

    private IEnumerator LoadDatasetFromStreamingAssets()
    {
        string path = Path.Combine(Application.streamingAssetsPath, streamingAssetsFileName);
        using UnityWebRequest request = UnityWebRequest.Get(path);
        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            generator.LoadFromJson(request.downloadHandler.text);
        }
        else
        {
            Debug.LogError($"[ScalePuzzleManager] Dataset load failed: {request.error}");
        }
    }

    public void StartPuzzle()
    {
        if (isCompleted) return;

        isWaitingForNext = false;
        TogglePlayerControls(false);
        if (puzzleCanvasPanel != null) puzzleCanvasPanel.SetActive(true);

        StartCoroutine(StartPuzzleRoutine());
    }

    private IEnumerator StartPuzzleRoutine()
    {
        while (!generator.IsLoaded) yield return null;

        attemptsLeft = maxAttempts;
        GenerateNewQuestion(false);
        UpdateStatusUI();
        SetButtonsInteractable(true);
        CheckAndUnlockHint();
        OpenQuestionPanel();
    }

    public void OpenQuestionPanel() => TriggerPanelSlide(true);
    public void CloseQuestionPanel() => TriggerPanelSlide(false);

    private void TriggerPanelSlide(bool slideIn)
    {
        if (questionPanel == null || questionRectTransform == null) return;
        if (activeAnimCoroutine != null) StopCoroutine(activeAnimCoroutine);
        activeAnimCoroutine = StartCoroutine(SlidePanelRoutine(slideIn));
    }

    private IEnumerator SlidePanelRoutine(bool slideIn)
    {
        if (slideIn) questionPanel.SetActive(true);

        float screenW = questionRectTransform.parent is RectTransform p ? p.rect.width : Screen.width;
        Vector2 startPos = questionRectTransform.anchoredPosition;
        Vector2 targetPos = slideIn ? questionOriginalPos : new Vector2(screenW, questionOriginalPos.y);

        if (slideIn)
        {
            startPos = new Vector2(screenW, questionOriginalPos.y);
            questionRectTransform.anchoredPosition = startPos;
        }

        float elapsed = 0f;
        while (elapsed < questionAnimDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / questionAnimDuration);
            t = t * t * (3f - 2f * t); // Smoothstep
            questionRectTransform.anchoredPosition = Vector2.Lerp(startPos, targetPos, t);
            yield return null;
        }

        questionRectTransform.anchoredPosition = targetPos;
        if (!slideIn) questionPanel.SetActive(false);
    }

    private void GenerateNewQuestion(bool preferEasier)
    {
        currentQuestion = generator.GetNextQuestion(dda.CurrentDifficulty, preferEasier);
        puzzleStartTime = Time.time;
        hintUsed = false;
        ApplyQuestionToUI();

        GameplayTelemetry.Instance?.BeginPuzzle("Linear Equations and Inequalities", (int)dda.CurrentDifficulty);
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
        List<string> selected = new List<string> { correct };
        
        List<string> distractors = new List<string>();
        foreach (var opt in options)
        {
            string trimmed = opt.Trim();
            if (trimmed != correct && !distractors.Contains(trimmed)) distractors.Add(trimmed);
        }

        distractors.Shuffle();
        for (int i = 0; i < Mathf.Min(3, distractors.Count); i++) selected.Add(distractors[i]);
        selected.Shuffle();

        for (int i = 0; i < choiceButtons.Count; i++)
        {
            var btn = choiceButtons[i];
            if (btn == null) continue;

            if (i < selected.Count)
            {
                btn.gameObject.SetActive(true);
                btn.interactable = true;
                BindButtonAction(btn, selected[i]);
            }
            else
            {
                btn.gameObject.SetActive(false);
            }
        }
    }

    private void BindButtonAction(Button btn, string label)
    {
        btn.onClick.RemoveAllListeners();
        if (btn.GetComponentInChildren<TMP_Text>() is TMP_Text tmp) tmp.text = label;
        
        Image img = btn.GetComponent<Image>();
        btn.onClick.AddListener(() => StartCoroutine(AnimateTapAndVerify(btn.transform, img, label)));
    }

    private IEnumerator AnimateTapAndVerify(Transform tr, Image img, string label)
    {
        if (tr == null) { VerifyAnswer(label); yield break; }

        Vector3 origScale = Vector3.one;
        Color origColor = img != null ? img.color : Color.white;
        if (img != null) img.color = colorBtnPress;

        float elapsed = 0f;
        while (elapsed < 0.05f)
        {
            elapsed += Time.deltaTime;
            tr.localScale = Vector3.Lerp(origScale, origScale * buttonPressScale, elapsed / 0.05f);
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < 0.1f)
        {
            elapsed += Time.deltaTime;
            tr.localScale = Vector3.Lerp(origScale * buttonPressScale, origScale, elapsed / 0.1f);
            yield return null;
        }

        tr.localScale = origScale;
        if (img != null) img.color = origColor;

        VerifyAnswer(label);
    }

    private void CheckAndUnlockHint()
    {
        if (ShopManagers.Instance != null && ShopManagers.Instance.hasHintScroll && !isCompleted && hintScrollUI != null)
        {
            hintScrollUI.EnableHint(() =>
            {
                ShopManagers.Instance.UseHintScroll();
                HighlightCorrectAnswer();
            });
        }
    }

    private void HighlightCorrectAnswer()
    {
        if (currentQuestion == null) return;
        string target = currentQuestion.correctAnswer.Trim().ToLower();

        foreach (var btn in choiceButtons)
        {
            if (btn == null || !btn.gameObject.activeInHierarchy) continue;
            if (btn.GetComponentInChildren<TMP_Text>()?.text.Trim().ToLower() == target)
            {
                if (highlightCoroutine != null) StopCoroutine(highlightCoroutine);
                highlightCoroutine = StartCoroutine(FlashButtonRoutine(btn));
                hintUsed = true;
                break;
            }
        }
    }

    private IEnumerator FlashButtonRoutine(Button btn)
    {
        Image img = btn.GetComponent<Image>();
        if (img == null) yield break;

        for (int i = 0; i < 3; i++)
        {
            img.color = colorCorrectHighlight;
            btn.transform.localScale = Vector3.one * 1.1f;
            yield return new WaitForSeconds(0.25f);
            img.color = Color.white;
            btn.transform.localScale = Vector3.one;
            yield return new WaitForSeconds(0.2f);
        }
    }

    private void VerifyAnswer(string selectedValue)
    {
        if (isWaitingForNew || currentQuestion == null) return;

        if (highlightCoroutine != null) { StopCoroutine(highlightCoroutine); highlightCoroutine = null; }
        ResetChoiceButtonVisuals();

        float timeSpent = Time.time - puzzleStartTime;
        bool isCorrect = selectedValue.Trim().Equals(currentQuestion.correctAnswer.Trim(), System.StringComparison.OrdinalIgnoreCase);

        LogTelemetryAttempt(isCorrect, timeSpent);
        GameManager.Instance?.TrackQuestionPerformance(currentQuestion.problemText, (int)dda.CurrentDifficulty, isCorrect, timeSpent);
        GameManager.Instance?.EvaluatePerformance(isCorrect, timeSpent);

        DifficultyLevel prevDiff = dda.CurrentDifficulty;
        bool wantsEasier = dda.EvaluateAnswer(isCorrect, timeSpent);
        NotifyDifficultyChange(prevDiff, dda.CurrentDifficulty);

        if (isCorrect)
        {
            SetButtonsInteractable(false);
            correctAnswersGiven++;
            attemptsLeft = maxAttempts;
            UpdateStatusUI();
            RankManager.Instance?.ProcessPuzzleSuccess(dda.LastSuccessRate);

            if (correctAnswersGiven >= correctAnswersRequired) CompletePuzzle();
            else StartCoroutine(FeedbackRoutine(true, $"CORRECT! ({correctAnswersGiven}/{correctAnswersRequired})", () => GenerateNewQuestion(wantsEasier)));
        }
        else
        {
            attemptsLeft = Mathf.Max(0, attemptsLeft - 1);
            UpdateStatusUI();
            RankManager.Instance?.ProcessPuzzleFailure();

            if (attemptsLeft <= 0) StartCoroutine(HandleGameOverRoutine(wantsEasier));
            else StartCoroutine(FeedbackRoutine(false, "WRONG ANSWER! TRY AGAIN...", () => SetButtonsInteractable(true)));
        }
    }

    private IEnumerator FeedbackRoutine(bool isCorrect, string msg, System.Action onComplete)
    {
        isWaitingForNew = true;
        SetButtonsInteractable(false);
        ShowFeedback(isCorrect, msg, feedbackDisplayDuration);
        yield return new WaitForSeconds(feedbackDisplayDuration);
        isWaitingForNew = false;
        onComplete?.Invoke();
        SetButtonsInteractable(true);
    }

    private IEnumerator HandleGameOverRoutine(bool wantsEasier)
    {
        isWaitingForNew = true;
        SetButtonsInteractable(false);
        ShowFeedback(false, "OUT OF ATTEMPTS! CLOSING...", 2.0f);
        yield return new WaitForSeconds(2.0f);
        GenerateNewQuestion(wantsEasier);
        ClosePuzzle();
    }

    private void CompletePuzzle()
    {
        isCompleted = true;
        if (txtQuestionDisplay != null) txtQuestionDisplay.text = "<color=#00F5A0>SCALE BALANCED SUCCESSFULLY!</color>";
        RewardPlayer();

        StartCoroutine(CompleteSequenceRoutine());
    }

    private IEnumerator CompleteSequenceRoutine()
    {
        bool animDone = false;
        PlayStarRewardSequence(() => animDone = true);

        float timer = 0f;
        while (!animDone && timer < 2.0f) { timer += Time.deltaTime; yield return null; }

        if (movingBlock != null)
        {
            Vector3 targetPos = movingBlock.localPosition + new Vector3(0, targetRiseHeight, 0);
            while (Vector3.Distance(movingBlock.localPosition, targetPos) > 0.01f)
            {
                movingBlock.localPosition = Vector3.MoveTowards(movingBlock.localPosition, targetPos, liftSpeed * Time.deltaTime);
                yield return null;
            }
        }
        ClosePuzzle();
    }

    private void NotifyDifficultyChange(DifficultyLevel before, DifficultyLevel after)
    {
        if (before == after || difficultyNotifier == null) return;
        if ((int)after > (int)before) difficultyNotifier.ShowIncrease();
        else difficultyNotifier.ShowDecrease();
    }

    private void UpdateStatusUI()
    {
        if (txtAttemptsDisplay != null) txtAttemptsDisplay.text = $"ATTEMPTS: {attemptsLeft}/{maxAttempts}";
        if (txtCorrectDisplay != null) txtCorrectDisplay.text = $"CORRECT: {correctAnswersGiven}/{correctAnswersRequired}";
    }

    private void ShowFeedback(bool correct, string msg, float duration)
    {
        if (feedbackCoroutine != null) StopCoroutine(feedbackCoroutine);
        feedbackCoroutine = StartCoroutine(DisplayFeedbackRoutine(correct, msg, duration));
    }

    private IEnumerator DisplayFeedbackRoutine(bool correct, string msg, float duration)
    {
        if (feedbackPanel == null) yield break;
        feedbackPanel.SetActive(true);
        if (feedbackText != null) feedbackText.text = msg;
        if (iconCorrect != null) iconCorrect.SetActive(correct);
        if (iconWrong != null) iconWrong.SetActive(!correct);
        yield return new WaitForSeconds(duration);
        HideFeedbackPanel();
    }

    private void HideFeedbackPanel()
    {
        feedbackPanel?.SetActive(false);
        iconCorrect?.SetActive(false);
        iconWrong?.SetActive(false);
    }

    private void SetButtonsInteractable(bool state)
    {
        foreach (var btn in choiceButtons) if (btn != null) btn.interactable = state;
    }

    private void ResetChoiceButtonVisuals()
    {
        foreach (var btn in choiceButtons)
        {
            if (btn == null) continue;
            if (btn.GetComponent<Image>() is Image img) img.color = Color.white;
            btn.transform.localScale = Vector3.one;
        }
    }

    public override void ClosePuzzle()
    {
        if (highlightCoroutine != null) { StopCoroutine(highlightCoroutine); highlightCoroutine = null; }
        ResetChoiceButtonVisuals();
        HideFeedbackPanel();
        isWaitingForNew = false;
        TriggerPanelSlide(false);
        puzzleCanvasPanel?.SetActive(false);
        TogglePlayerControls(true);
    }

    public override void ResetPuzzleState()
    {
        attemptsLeft = maxAttempts;
        correctAnswersGiven = 0;
        currentQuestion = null;
        isWaitingForNew = false;
        ResetChoiceButtonVisuals();
        SetButtonsInteractable(true);
        HideFeedbackPanel();
        UpdateStatusUI();
    }
}

// Extension helper for list shuffling
public static class ListExtensions
{
    private static readonly System.Random rng = new System.Random();
    public static void Shuffle<T>(this IList<T> list)
    {
        int n = list.Count;
        while (n > 1)
        {
            n--;
            int k = rng.Next(n + 1);
            (list[k], list[n]) = (list[n], list[k]);
        }
    }
}