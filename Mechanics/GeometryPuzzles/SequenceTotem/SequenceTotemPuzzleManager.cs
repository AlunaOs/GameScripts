using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SequenceTotemPuzzleManager : MonoBehaviour
{
    public bool isCleared = false;

    [Header("Data & Difficulty")]
    [SerializeField] private PuzzleLoader puzzleLoader;
    [SerializeField] private string currentDifficulty = "easy";

    [Header("Totems")]
    [SerializeField] private SequenceTotem[] totems;

    [Header("Moving Wall / Gate Settings")]
    [SerializeField] private Transform movingWall;
    [SerializeField] private float targetRiseHeight = 5f;
    [SerializeField] private float liftSpeed = 5f;

    [Header("UI Components")]
    [SerializeField] private TextMeshProUGUI questionText;
    [SerializeField] private TextMeshProUGUI progressText;
    [SerializeField] private TextMeshProUGUI guideText;
    [SerializeField] private TextMeshProUGUI attemptsText;
    [SerializeField] private Button submitButton;
    [SerializeField] private Button closePanelButton;

    [Header("Question Sliding Panel UI")]
    [SerializeField] private GameObject questionPanel;
    [SerializeField] private Button btnOpenQuestion;
    [SerializeField] private Button btnCloseQuestion;
    [SerializeField] private float questionAnimDuration = 0.3f;

    [Header("Feedback Panel System")]
    [SerializeField] private GameObject feedbackPanel;
    [SerializeField] private TextMeshProUGUI feedbackText;
    [SerializeField] private GameObject iconCorrect;
    [SerializeField] private GameObject iconWrong;
    [SerializeField] private float feedbackDisplayDuration = 1.5f;

    [Header("Per-Question Attempts")]
    [SerializeField] private int maxAttemptsPerQuestion = 3;

    [Header("Player Movement HUD")]
    [SerializeField] private GameObject playerControlsCanvas;

    [Header("Reward & Star Animations")]
    public StarRewardAnimation starRewardAnimator;
    public RectTransform targetStarSlot;

    // ── HINT SYSTEM (separate canvas — reads only the 'hint' field) ─────────
    [Header("Hint Panel (separate canvas — reads only the 'hint' field)")]
    public HintScrollUI hintScrollUI;
    public GameObject hintExplanationPanel;
    public TMP_Text hintExplanationText;
    public Button hintGotItButton;

    private List<PuzzleItem> activeSessionPuzzles = new List<PuzzleItem>();
    private List<int> randomizedTotemIndices = new List<int> { 0, 1, 2 };
    private int currentQuestionIndex = 0;
    private int questionAttemptsRemaining;
    private bool datasetLoaded = false;
    private bool isSubmitting = false;
    private bool hintUsedThisPuzzle = false;
    private Coroutine currentFeedbackCoroutine;
    private float questionStartTime;

    private Vector3 initialWallPosition;
    private bool isWallMoving = false;

    private RectTransform questionRectTransform;
    private Vector2 questionOriginalAnchoredPos;
    private Coroutine questionAnimCoroutine;

    private void Awake()
    {
        if (movingWall != null) initialWallPosition = movingWall.position;

        if (questionPanel != null)
        {
            questionRectTransform = questionPanel.GetComponent<RectTransform>();
            if (questionRectTransform != null)
                questionOriginalAnchoredPos = questionRectTransform.anchoredPosition;
        }
    }

    private void Start()
    {
        if (submitButton != null) submitButton.onClick.AddListener(SubmitCurrentAnswer);
        if (closePanelButton != null) closePanelButton.onClick.AddListener(ClosePuzzleManually);
        if (btnOpenQuestion != null) btnOpenQuestion.onClick.AddListener(OpenQuestionPanel);
        if (btnCloseQuestion != null) btnCloseQuestion.onClick.AddListener(CloseQuestionPanel);
        if (hintGotItButton != null) hintGotItButton.onClick.AddListener(HideHintExplanation);

        HideFeedback();
        HideHintExplanation();
        DisableHintButton();
    }

    public void StartPuzzleSystem()
    {
        if (isCleared) return;

        if (playerControlsCanvas != null) playerControlsCanvas.SetActive(false);

        if (!datasetLoaded)
        {
            PrepareSessionQuestions();
        }

        if (GameplayTelemetry.Instance != null)
        {
            string puzzleTopic = (GameManager.Instance != null && !string.IsNullOrEmpty(GameManager.Instance.currentCategory))
                ? GameManager.Instance.currentCategory
                : "Geometry Sequences";

            int diffLevel = currentDifficulty.ToLower() == "hard" ? 3 : (currentDifficulty.ToLower() == "medium" ? 2 : 1);
            GameplayTelemetry.Instance.BeginPuzzle(puzzleTopic, diffLevel);
            GameplayTelemetry.Instance.LogQuestionTier(diffLevel);
            GameplayTelemetry.Instance.LogAttemptNumber(1);
        }

        DisplayCurrentQuestion();
        OpenQuestionPanel();
        CheckAndUnlockHint();
    }

    private void PrepareSessionQuestions()
    {
        if (puzzleLoader == null) puzzleLoader = GetComponent<PuzzleLoader>();

        if (puzzleLoader != null && puzzleLoader.Database == null)
            puzzleLoader.LoadData();

        if (puzzleLoader == null || puzzleLoader.Database == null)
        {
            Debug.LogError("PuzzleLoader failed to load database!");
            return;
        }

        List<PuzzleItem> pool = currentDifficulty.ToLower() switch
        {
            "easy" => puzzleLoader.Database.easy,
            "medium" => puzzleLoader.Database.medium,
            "hard" => puzzleLoader.Database.hard,
            _ => puzzleLoader.Database.easy
        };

        if (pool != null && pool.Count >= 3)
        {
            activeSessionPuzzles.Clear();
            List<PuzzleItem> tempPool = new List<PuzzleItem>(pool);

            for (int i = 0; i < 3; i++)
            {
                int index = UnityEngine.Random.Range(0, tempPool.Count);
                activeSessionPuzzles.Add(tempPool[index]);
                tempPool.RemoveAt(index);
            }

            ShuffleTotemIndices();

            for (int i = 0; i < 3 && i < activeSessionPuzzles.Count; i++)
            {
                int totemIndex = randomizedTotemIndices[i];
                totems[totemIndex].SetupTotemOptions(activeSessionPuzzles[i].answerOptions);
            }

            datasetLoaded = true;
            currentQuestionIndex = 0;
            ResetQuestionAttempts();
        }
    }

    private void ShuffleTotemIndices()
    {
        for (int i = 0; i < randomizedTotemIndices.Count; i++)
        {
            int temp = randomizedTotemIndices[i];
            int randomIndex = UnityEngine.Random.Range(i, randomizedTotemIndices.Count);
            randomizedTotemIndices[i] = randomizedTotemIndices[randomIndex];
            randomizedTotemIndices[randomIndex] = temp;
        }
    }

    private void DisplayCurrentQuestion()
    {
        if (activeSessionPuzzles.Count < 3) return;

        questionStartTime = Time.time;
        hintUsedThisPuzzle = false;

        int activeTotemIndex = randomizedTotemIndices[currentQuestionIndex];
        char totemLabel = (char)('A' + activeTotemIndex);

        if (questionText != null)
            questionText.text = activeSessionPuzzles[currentQuestionIndex].question;

        if (progressText != null)
            progressText.text = $"Question {currentQuestionIndex + 1}/3";

        if (guideText != null)
            guideText.text = $"Rotate Totem {totemLabel} by touching them to match the answer.";

        UpdateAttemptsUI();

        for (int i = 0; i < totems.Length; i++)
            totems[i].SetActiveState(i == activeTotemIndex);

        if (GameplayTelemetry.Instance != null)
        {
            int diffLevel = currentDifficulty.ToLower() == "hard" ? 3 : (currentDifficulty.ToLower() == "medium" ? 2 : 1);
            GameplayTelemetry.Instance.LogQuestionTier(diffLevel);
            GameplayTelemetry.Instance.LogAttemptNumber(maxAttemptsPerQuestion - questionAttemptsRemaining + 1);
        }

        // Refresh hint button visibility for the new question
        CheckAndUnlockHint();
    }

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

    private void ResetQuestionAttempts()
    {
        questionAttemptsRemaining = maxAttemptsPerQuestion;
        UpdateAttemptsUI();
        if (GameplayTelemetry.Instance != null)
            GameplayTelemetry.Instance.LogAttemptNumber(1);
    }

    private void UpdateAttemptsUI()
    {
        if (attemptsText != null)
            attemptsText.text = $"Attempts: {questionAttemptsRemaining}/{maxAttemptsPerQuestion}";
    }

    public void SubmitCurrentAnswer()
    {
        if (isCleared || isSubmitting || activeSessionPuzzles.Count < 3) return;

        float timeSpent = Time.time - questionStartTime;
        int activeTotemIndex = randomizedTotemIndices[currentQuestionIndex];
        float playerSelection = totems[activeTotemIndex].SelectedValue;
        float targetAnswer = activeSessionPuzzles[currentQuestionIndex].correctAnswer;
        string currentQuestionText = activeSessionPuzzles[currentQuestionIndex].question;

        bool isCorrect = Mathf.Abs(playerSelection - targetAnswer) < 0.01f;
        int currentAttemptNumber = maxAttemptsPerQuestion - questionAttemptsRemaining + 1;

        if (GameplayTelemetry.Instance != null)
        {
            GameplayTelemetry.Instance.LogAttemptNumber(currentAttemptNumber);
            GameplayTelemetry.Instance.LogAttempt(isCorrect, timeSpent, hintUsedThisPuzzle);
        }

        if (GameManager.Instance != null)
        {
            // Single call — TrackQuestionPerformance already runs EvaluatePerformance internally.
            GameManager.Instance.TrackQuestionPerformance(currentQuestionText, 1, isCorrect, timeSpent);
        }

        if (isCorrect)
        {
            StartCoroutine(HandleCorrectAnswerSequence());
        }
        else
        {
            questionAttemptsRemaining--;
            UpdateAttemptsUI();

            if (GameplayTelemetry.Instance != null)
            {
                int nextAttemptNumber = maxAttemptsPerQuestion - questionAttemptsRemaining + 1;
                GameplayTelemetry.Instance.LogAttemptNumber(nextAttemptNumber);
            }

            questionStartTime = Time.time;

            if (questionAttemptsRemaining <= 0)
                StartCoroutine(HandleQuestionFailedSequence());
            else
                ShowFeedback("Incorrect degree!\nTry rotating again.", false, false);
        }
    }

    private IEnumerator HandleQuestionFailedSequence()
    {
        isSubmitting = true;

        if (closePanelButton != null) closePanelButton.interactable = false;
        if (submitButton != null) submitButton.interactable = false;

        ShowFeedback("Out of attempts!\nClosing puzzle...", false, true);

        yield return new WaitForSeconds(feedbackDisplayDuration);

        ResetQuestionAttempts();

        if (closePanelButton != null) closePanelButton.interactable = true;
        if (submitButton != null) submitButton.interactable = true;

        isSubmitting = false;
        ClosePuzzleManually();
    }

    private void ShowFeedback(string message, bool isCorrect, bool isPermanent)
    {
        if (currentFeedbackCoroutine != null) StopCoroutine(currentFeedbackCoroutine);
        currentFeedbackCoroutine = StartCoroutine(ShowFeedbackRoutine(message, isCorrect, isPermanent));
    }

    private IEnumerator ShowFeedbackRoutine(string message, bool isCorrect, bool isPermanent)
    {
        if (feedbackPanel != null) feedbackPanel.SetActive(true);

        if (feedbackText != null)
        {
            feedbackText.text = message;
            feedbackText.color = isCorrect ? new Color(0f, 0.96f, 0.63f, 1f) : new Color(1f, 0.42f, 0.42f, 1f);
        }

        if (iconCorrect != null) iconCorrect.SetActive(isCorrect);
        if (iconWrong != null) iconWrong.SetActive(!isCorrect);

        if (!isPermanent)
        {
            yield return new WaitForSeconds(feedbackDisplayDuration);
            HideFeedback();
        }
    }

    private void HideFeedback()
    {
        if (feedbackPanel != null) feedbackPanel.SetActive(false);
        if (iconCorrect != null) iconCorrect.SetActive(false);
        if (iconWrong != null) iconWrong.SetActive(false);
        if (feedbackText != null) feedbackText.text = "";
    }

    private IEnumerator HandleCorrectAnswerSequence()
    {
        isSubmitting = true;
        ShowFeedback("Correct!", true, false);

        yield return new WaitForSeconds(feedbackDisplayDuration);

        currentQuestionIndex++;

        if (currentQuestionIndex >= 3)
        {
            CompletePuzzle();
        }
        else
        {
            isSubmitting = false;
            ResetQuestionAttempts();
            DisplayCurrentQuestion();
        }
    }

    private void CompletePuzzle()
    {
        if (closePanelButton != null) closePanelButton.interactable = false;
        isCleared = true;

        ShowFeedback("PUZZLE CLEARED!", true, true);

        if (ShopManagers.Instance != null)
            ShopManagers.Instance.AddStars(1);

        if (RankManager.Instance != null)
            RankManager.Instance.ProcessPuzzleSuccess(1.0f);

        if (movingWall != null)
            StartCoroutine(LiftMovingWallRoutine());

        StartCoroutine(CompletePuzzleSequenceRoutine());
    }

    private IEnumerator LiftMovingWallRoutine()
    {
        isWallMoving = true;
        Vector3 startPos = movingWall.position;
        Vector3 targetPos = startPos + Vector3.up * targetRiseHeight;

        while (Vector3.Distance(movingWall.position, targetPos) > 0.01f)
        {
            movingWall.position = Vector3.MoveTowards(movingWall.position, targetPos, liftSpeed * Time.deltaTime);
            yield return null;
        }
        movingWall.position = targetPos;
        isWallMoving = false;
    }

    private IEnumerator CompletePuzzleSequenceRoutine()
    {
        bool isStarAnimationDone = false;
        TriggerStarRewardSequence(() => isStarAnimationDone = true);

        float timeoutTimer = 0f;
        while (!isStarAnimationDone && timeoutTimer < 2.0f)
        {
            timeoutTimer += Time.deltaTime;
            yield return null;
        }

        yield return new WaitForSeconds(0.5f);
        ClosePuzzleManually();
    }

    private void TriggerStarRewardSequence(Action onComplete)
    {
        if (starRewardAnimator != null)
        {
            if (!starRewardAnimator.gameObject.activeInHierarchy)
                starRewardAnimator.gameObject.SetActive(true);

            if (targetStarSlot != null)
                starRewardAnimator.PlayStarRewardSequence(targetStarSlot, () => onComplete?.Invoke());
            else
                starRewardAnimator.PlayStarRewardSequence(() => onComplete?.Invoke());
        }
        else onComplete?.Invoke();
    }

    // ── HINT SYSTEM ─────────────────────────────────────────────────────────
    public void CheckAndUnlockHint()
    {
        if (ShopManagers.Instance != null && ShopManagers.Instance.hasHintScroll && !isCleared)
            EnableHintButton();
        else
            DisableHintButton();
    }

    public void EnableHintButton()
    {
        if (isCleared) return;
        if (hintScrollUI == null) return;

        hintScrollUI.gameObject.SetActive(true);
        hintScrollUI.EnableHint(() =>
        {
            if (ShopManagers.Instance != null)
                ShopManagers.Instance.UseHintScroll();

            ShowHintExplanation();
            DisableHintButton();
        });
    }

    public void DisableHintButton()
    {
        if (hintScrollUI != null)
            hintScrollUI.gameObject.SetActive(false);
    }

    private void ShowHintExplanation()
    {
        if (activeSessionPuzzles.Count < 3) return;
        if (currentQuestionIndex < 0 || currentQuestionIndex >= activeSessionPuzzles.Count) return;

        hintUsedThisPuzzle = true;

        if (hintExplanationPanel != null)
            hintExplanationPanel.SetActive(true);

        string hintText = activeSessionPuzzles[currentQuestionIndex].hint;
        if (string.IsNullOrWhiteSpace(hintText))
            hintText = "Read the question carefully and think about the geometry rule involved.";

        if (hintExplanationText != null)
            hintExplanationText.text = hintText;
    }

    public void HideHintExplanation()
    {
        if (hintExplanationPanel != null)
            hintExplanationPanel.SetActive(false);
    }
    // ────────────────────────────────────────────────────────────────────────

    public void ResetPuzzleForReplay()
    {
        isCleared = false;
        datasetLoaded = false;
        isSubmitting = false;
        currentQuestionIndex = 0;
        hintUsedThisPuzzle = false;

        if (movingWall != null) movingWall.position = initialWallPosition;
        if (closePanelButton != null) closePanelButton.interactable = true;

        HideFeedback();
        HideHintExplanation();
        DisableHintButton();
        CloseQuestionPanel();
        ResetQuestionAttempts();
        PrepareSessionQuestions();
    }

    public void ClosePuzzleManually()
    {
        StopAllCoroutines();
        isSubmitting = false;

        if (questionAttemptsRemaining <= 0)
            ResetQuestionAttempts();

        if (playerControlsCanvas != null)
            playerControlsCanvas.SetActive(true);

        HideFeedback();
        HideHintExplanation();
        CloseQuestionPanel();
        gameObject.SetActive(false);
    }
}
