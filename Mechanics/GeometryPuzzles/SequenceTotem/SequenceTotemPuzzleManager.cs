using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Manages the Sequence Totem interactive puzzle system, extending BasePuzzleManager
/// to handle state-machine question tracking, totem rotation selection, and moving gate logic.
/// </summary>
public class SequenceTotemPuzzleManager : BasePuzzleManager
{
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

    private List<PuzzleItem> activeSessionPuzzles = new List<PuzzleItem>();
    private List<int> randomizedTotemIndices = new List<int> { 0, 1, 2 };
    private int currentQuestionIndex = 0;
    private int questionAttemptsRemaining;
    private bool datasetLoaded = false;
    private Coroutine currentFeedbackCoroutine;

    // Moving Wall Variables
    private Vector3 initialWallPosition;
    private bool isWallMoving = false;

    // Question Panel Transition Variables
    private RectTransform questionRectTransform;

    protected override void Awake()
    {
        base.Awake();

        if (movingWall != null)
        {
            initialWallPosition = movingWall.position;
        }

        if (questionPanel != null)
        {
            questionRectTransform = questionPanel.GetComponent<RectTransform>();
        }
    }

    protected override void Start()
    {
        base.Start();

        if (submitButton != null) submitButton.onClick.AddListener(SubmitCurrentAnswer);
        if (closePanelButton != null) closePanelButton.onClick.AddListener(ClosePuzzleManually);
        if (btnOpenQuestion != null) btnOpenQuestion.onClick.AddListener(OpenQuestionPanel);
        if (btnCloseQuestion != null) btnCloseQuestion.onClick.AddListener(CloseQuestionPanel);

        HideFeedback();
    }

    public void StartPuzzleSystem()
    {
        if (isPuzzleCompleted) return;

        OpenPuzzle();

        if (!datasetLoaded)
        {
            PrepareSessionQuestions();
        }

        string puzzleTopic = (GameManager.Instance != null && !string.IsNullOrEmpty(GameManager.Instance.currentCategory))
            ? GameManager.Instance.currentCategory
            : "Geometry Sequences";

        int diffLevel = currentDifficulty.ToLower() == "hard" ? 3 : (currentDifficulty.ToLower() == "medium" ? 2 : 1);
        LogPuzzleStart(puzzleTopic, diffLevel);
        
        if (GameplayTelemetry.Instance != null)
        {
            GameplayTelemetry.Instance.LogQuestionTier(diffLevel);
            GameplayTelemetry.Instance.LogAttemptNumber(1);
        }

        DisplayCurrentQuestion();
        OpenQuestionPanel();
    }

    private void PrepareSessionQuestions()
    {
        if (puzzleLoader == null) puzzleLoader = GetComponent<PuzzleLoader>();

        if (puzzleLoader != null && puzzleLoader.Database == null)
        {
            puzzleLoader.LoadData();
        }

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

        puzzleStartTime = Time.time;

        int activeTotemIndex = randomizedTotemIndices[currentQuestionIndex];
        char totemLabel = (char)('A' + activeTotemIndex);

        if (questionText != null) questionText.text = activeSessionPuzzles[currentQuestionIndex].question;
        if (progressText != null) progressText.text = $"Question {currentQuestionIndex + 1}/3";
        if (guideText != null) guideText.text = $"Rotate Totem {totemLabel} by touching them to match the answer. Every totem you see a have a corresponding count of Dead Bush, what could those Dead Bush represent? can you identify them?";

        UpdateAttemptsUI();

        for (int i = 0; i < totems.Length; i++)
        {
            totems[i].SetActiveState(i == activeTotemIndex);
        }

        if (GameplayTelemetry.Instance != null)
        {
            int diffLevel = currentDifficulty.ToLower() == "hard" ? 3 : (currentDifficulty.ToLower() == "medium" ? 2 : 1);
            GameplayTelemetry.Instance.LogQuestionTier(diffLevel);
            GameplayTelemetry.Instance.LogAttemptNumber(maxAttemptsPerQuestion - questionAttemptsRemaining + 1);
        }
    }

    public void OpenQuestionPanel()
    {
        if (questionRectTransform != null)
        {
            if (panelAnimCoroutine != null) StopCoroutine(panelAnimCoroutine);
            questionPanel.SetActive(true);
            panelAnimCoroutine = StartCoroutine(SlidePanelInRoutine(questionRectTransform, questionAnimDuration));
        }
    }

    public void CloseQuestionPanel()
    {
        if (questionPanel != null && questionPanel.activeSelf && questionRectTransform != null)
        {
            if (panelAnimCoroutine != null) StopCoroutine(panelAnimCoroutine);
            panelAnimCoroutine = StartCoroutine(SlidePanelOutRoutine(questionRectTransform, questionPanel, questionAnimDuration));
        }
    }

    private void ResetQuestionAttempts()
    {
        questionAttemptsRemaining = maxAttemptsPerQuestion;
        UpdateAttemptsUI();
        if (GameplayTelemetry.Instance != null)
        {
            GameplayTelemetry.Instance.LogAttemptNumber(1);
        }
    }

    private void UpdateAttemptsUI()
    {
        if (attemptsText != null)
        {
            attemptsText.text = $"Attempts: {questionAttemptsRemaining}/{maxAttemptsPerQuestion}";
        }
    }

    public void SubmitCurrentAnswer()
    {
        if (isPuzzleCompleted || isProcessingAction || activeSessionPuzzles.Count < 3) return;

        float timeSpent = Time.time - puzzleStartTime;
        int activeTotemIndex = randomizedTotemIndices[currentQuestionIndex];
        float playerSelection = totems[activeTotemIndex].SelectedValue;
        float targetAnswer = activeSessionPuzzles[currentQuestionIndex].correctAnswer;
        string currentQuestionText = activeSessionPuzzles[currentQuestionIndex].question;

        bool isCorrect = Mathf.Abs(playerSelection - targetAnswer) < 0.01f;
        int currentAttemptNumber = maxAttemptsPerQuestion - questionAttemptsRemaining + 1;

        LogPuzzleAttempt(isCorrect, timeSpent);
        TrackQuestionMetrics(currentQuestionText, 1, isCorrect, timeSpent);

        if (GameplayTelemetry.Instance != null)
        {
            GameplayTelemetry.Instance.LogAttemptNumber(currentAttemptNumber);
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

            puzzleStartTime = Time.time;

            if (questionAttemptsRemaining <= 0)
            {
                StartCoroutine(HandleQuestionFailedSequence());
            }
            else
            {
                ShowFeedback("Incorrect degree!\nTry rotating again.", false, false);
            }
        }
    }

    private IEnumerator HandleQuestionFailedSequence()
    {
        isProcessingAction = true;

        if (closePanelButton != null) closePanelButton.interactable = false;
        if (submitButton != null) submitButton.interactable = false;

        ShowFeedback("Out of attempts!\nClosing puzzle...", false, true);

        yield return new WaitForSeconds(feedbackDisplayDuration);

        ResetQuestionAttempts();

        if (closePanelButton != null) closePanelButton.interactable = true;
        if (submitButton != null) submitButton.interactable = true;

        isProcessingAction = false;
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
        isProcessingAction = true;
        ShowFeedback("Correct!", true, false);

        yield return new WaitForSeconds(feedbackDisplayDuration);

        currentQuestionIndex++;

        if (currentQuestionIndex >= 3)
        {
            CompletePuzzle();
        }
        else
        {
            isProcessingAction = false;
            ResetQuestionAttempts();
            DisplayCurrentQuestion();
        }
    }

    protected override void CompletePuzzle()
    {
        if (closePanelButton != null) closePanelButton.interactable = false;
        isPuzzleCompleted = true;

        ShowFeedback("PUZZLE CLEARED!", true, true);

        if (ShopManagers.Instance != null) ShopManagers.Instance.AddStars(1);
        ProcessRankSuccess(1.0f);

        if (movingWall != null) StartCoroutine(LiftMovingWallRoutine());

        TriggerStarRewardSequence(() => {
            StartCoroutine(AutoCloseAfterDelayRoutine());
        });
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

    private IEnumerator AutoCloseAfterDelayRoutine()
    {
        yield return new WaitForSeconds(0.5f);
        ClosePuzzleManually();
    }

    public void ResetPuzzleForReplay()
    {
        isPuzzleCompleted = false;
        datasetLoaded = false;
        isProcessingAction = false;
        currentQuestionIndex = 0;

        if (movingWall != null) movingWall.position = initialWallPosition;
        if (closePanelButton != null) closePanelButton.interactable = true;

        HideFeedback();
        CloseQuestionPanel();
        ResetQuestionAttempts();
        PrepareSessionQuestions();
    }

    public void ClosePuzzleManually()
    {
        StopAllCoroutines();
        isProcessingAction = false;

        if (questionAttemptsRemaining <= 0) ResetQuestionAttempts();

        HideFeedback();
        CloseQuestionPanel();
        ClosePuzzle();
    }
}