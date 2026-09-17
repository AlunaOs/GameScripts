using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class DecodeQuizPuzzleManager : BasePuzzleManager
{
    [Header("UI References")]
    public TMP_Text stageText;
    public TMP_Text questionText;
    public TMP_Text currentAnswerText;
    public TMP_Text correctCounterText;
    public TMP_Text stageCompleteMessage;
    public TMP_Text attemptsLeftText;

    [Header("Panels & Animations")]
    public RectTransform questionPanel;
    public float panelSlideDuration = 0.5f;
    private bool panelInitialPositionCached = false;
    private Vector2 questionPanelTargetPos;

    [Header("Feedback Area Panels")]
    public GameObject correctPanel;
    public GameObject wrongPanel;
    public float feedbackDisplayDuration = 1.0f;

    [Header("Button Containers")]
    public Transform answerButtonsContainer;
    public GameObject answerPieceButtonPrefab;

    [Header("Control Buttons")]
    public Button submitButton;
    public Button undoButton;
    public Button closeButton;

    [Header("Explanation Panel")]
    public GameObject explanationPanel;
    public TMP_Text explanationAnswerText;
    public TMP_Text explanationBodyText;
    public Button gotItButton;

    [Header("Progress Settings")]
    public int correctAnswersNeeded = 3;
    public int maxAttemptsPerQuestion = 3;
    public float stageCompleteDelay = 1.5f;
    public float submitCooldownTime = 0.5f;

    // DDA Integration Object
    private DDAController ddaController = new DDAController(DifficultyLevel.Easy);
    public DifficultyLevel CurrentDifficulty => ddaController.CurrentDifficulty;

    private QuestionGenerator questionGenerator = new QuestionGenerator();
    public bool isDatasetLoaded => questionGenerator.IsLoaded;

    private int correctAnswersCount = 0;
    private int currentQuestionAttempts = 0;
    private int stageCorrectCount = 0;

    private Question currentQ;
    private List<string> currentAnswerPieces = new List<string>();
    private List<string> correctAnswerPieces = new List<string>();
    private List<string> allAnswerPieces = new List<string>();
    private List<GameObject> activeButtons = new List<GameObject>();
    private List<Button> selectedButtons = new List<Button>();

    private bool isQuizActive = false;
    private bool isSubmitting = false;
    private float lastSubmitTime = 0f;
    private bool waitingForNext = false;
    private string saveKey;

    protected override void Awake()
    {
        base.Awake();
        saveKey = "DecodeQuizCompleted_" + gameObject.name;

        if (submitButton) submitButton.onClick.AddListener(SubmitAnswer);
        if (undoButton) undoButton.onClick.AddListener(UndoLastSelection);
        if (closeButton) closeButton.onClick.AddListener(ClosePuzzle);
        if (gotItButton) gotItButton.onClick.AddListener(OnGotItClicked);

        if (questionPanel != null)
        {
            questionPanelTargetPos = questionPanel.anchoredPosition;
            panelInitialPositionCached = true;
        }

        StartCoroutine(questionGenerator.LoadTemplates());
    }

    protected override void Start()
    {
        base.Start();
        HideExplanationPanel();
        HideFeedbackPanels();
        DisableHintButton();
    }

    void Update()
    {
        if (isSubmitting && Time.time - lastSubmitTime > submitCooldownTime)
            isSubmitting = false;
    }

    public override void OpenPuzzle()
    {
        if (isPuzzleCompleted) return;
        base.OpenPuzzle();

        StopAllCoroutines();
        isQuizActive = true;
        waitingForNext = false;

        stageCorrectCount = 0;
        correctAnswersCount = 0;
        isProcessingAction = false;
        isSubmitting = false;

        HideExplanationPanel();
        HideFeedbackPanels();
        DisableHintButton();

        if (questionPanel != null)
        {
            if (!panelInitialPositionCached)
            {
                questionPanelTargetPos = questionPanel.anchoredPosition;
                panelInitialPositionCached = true;
            }
            StartCoroutine(SlidePanelInRoutine(questionPanel, panelSlideDuration));
        }

        StartCoroutine(InitializeQuiz());
    }

    IEnumerator InitializeQuiz()
    {
        while (!questionGenerator.IsLoaded) yield return null;
        ResetForNewQuestion();
    }

    public override void ClosePuzzle()
    {
        isQuizActive = false;
        isProcessingAction = false;
        isSubmitting = false;
        waitingForNext = false;
        ResetAllState();
        HideExplanationPanel();
        HideFeedbackPanels();
        DisableHintButton();

        base.ClosePuzzle();
        gameObject.SetActive(false);
    }

    void ResetAllState()
    {
        correctAnswersCount = 0;
        stageCorrectCount = 0;
        currentQuestionAttempts = 0;
        currentAnswerPieces.Clear();
        selectedButtons.Clear();
        foreach (var btn in activeButtons) Destroy(btn);
        activeButtons.Clear();
        ddaController.Reset();
    }

    void ResetForNewQuestion()
    {
        if (!isQuizActive) return;

        waitingForNext = false;
        foreach (var btn in activeButtons) Destroy(btn);
        activeButtons.Clear();
        selectedButtons.Clear();
        currentAnswerPieces.Clear();
        UpdateCurrentAnswerDisplay();

        currentQuestionAttempts = 0;
        UpdateAttemptsDisplay();

        if (stageCompleteMessage) stageCompleteMessage.gameObject.SetActive(false);
        HideExplanationPanel();
        HideFeedbackPanels();
        DisableHintButton();

        if (questionGenerator.IsLoaded)
        {
            int currentDiffTier = (int)ddaController.CurrentDifficulty;
            currentQ = questionGenerator.GetQuestion(currentDiffTier, "Algebra");
            puzzleStartTime = Time.time;
        }
        else
        {
            currentQ = null;
        }

        if (currentQ != null)
        {
            if (stageText != null) stageText.text = $"DECODE CHALLENGE ({ddaController.CurrentDifficulty})";
            if (questionText) questionText.text = currentQ.text;

            ParseAnswerIntoPieces();
            GenerateAllButtons();
            if (correctCounterText != null)
                correctCounterText.text = $"Progress: {stageCorrectCount}/{correctAnswersNeeded}";

            string activeTopic = (GameManager.Instance != null) ? GameManager.Instance.currentCategory : "Linear Equations and Inequalities";
            LogPuzzleStart(activeTopic, (int)ddaController.CurrentDifficulty);
        }
        else
        {
            if (questionText) questionText.text = "No questions found in dataset.";
        }

        isProcessingAction = false;
        isSubmitting = false;
        CheckAndUnlockHint();
    }

    void UpdateAttemptsDisplay()
    {
        if (attemptsLeftText)
        {
            int left = maxAttemptsPerQuestion - currentQuestionAttempts;
            attemptsLeftText.text = $"Attempts: {left}/{maxAttemptsPerQuestion}";
        }
    }

    void ParseAnswerIntoPieces()
    {
        correctAnswerPieces.Clear();
        string answer = currentQ.answer.Trim();
        if (answer.Contains(" "))
            correctAnswerPieces = answer.Split(' ').ToList();
        else
            correctAnswerPieces = SmartParseAnswer(answer);
    }

    List<string> SmartParseAnswer(string answer)
    {
        List<string> pieces = new List<string>();
        string current = "";
        for (int i = 0; i < answer.Length; i++)
        {
            char c = answer[i];
            if (c == '^')
            {
                bool attachedPower = current.Length > 0 && i + 1 < answer.Length && (char.IsLetterOrDigit(answer[i + 1]) || answer[i + 1] == '(');
                if (attachedPower)
                {
                    current += c;
                    i++;
                    while (i < answer.Length && (char.IsLetterOrDigit(answer[i]) || answer[i] == '-'))
                    {
                        current += answer[i];
                        i++;
                    }
                    i--;
                    continue;
                }
                else
                {
                    if (!string.IsNullOrEmpty(current)) pieces.Add(current);
                    current = "";
                    pieces.Add(c.ToString());
                }
            }
            else if (c == '+' || c == '-' || c == '*' || c == '/')
            {
                if (!string.IsNullOrEmpty(current)) pieces.Add(current);
                current = "";
                pieces.Add(c.ToString());
            }
            else if (c != ' ')
            {
                current += c;
            }
        }
        if (!string.IsNullOrEmpty(current)) pieces.Add(current);
        return pieces;
    }

    private string NormalizeAnswer(string s) => s.ToLower().Replace(" ", "").Replace("×", "*").Replace("÷", "/");

    void GenerateAllButtons()
    {
        allAnswerPieces.Clear();
        allAnswerPieces.AddRange(correctAnswerPieces);

        string[] operators = { "+", "-", "*", "/" };
        List<string> hindranceOps = new List<string>();
        var availableOps = operators.Where(op => !correctAnswerPieces.Contains(op)).ToList();
        System.Random rnd = new System.Random();

        while (hindranceOps.Count < 2 && availableOps.Count > 0)
        {
            string op = availableOps[rnd.Next(availableOps.Count)];
            hindranceOps.Add(op);
            availableOps.Remove(op);
        }
        while (hindranceOps.Count < 2)
            hindranceOps.Add(new[] { "+", "*" }[hindranceOps.Count]);

        allAnswerPieces.AddRange(hindranceOps);

        int numHindrances = Random.Range(2, 5);
        List<string> numericHindrances = new List<string>();
        for (int i = 0; i < numHindrances; i++)
        {
            int candidate = Random.Range(1, 21);
            string candidateStr = candidate.ToString();
            if (!numericHindrances.Contains(candidateStr) && !correctAnswerPieces.Contains(candidateStr))
                numericHindrances.Add(candidateStr);
            else
                i--;
            if (numericHindrances.Count >= 10) break;
        }
        allAnswerPieces.AddRange(numericHindrances);
        allAnswerPieces = allAnswerPieces.OrderBy(x => Random.value).ToList();

        foreach (string piece in allAnswerPieces)
        {
            GameObject buttonObj = Instantiate(answerPieceButtonPrefab, answerButtonsContainer);
            Button btn = buttonObj.GetComponent<Button>();
            TMP_Text txt = buttonObj.GetComponentInChildren<TMP_Text>();
            if (txt) txt.text = piece;
            string val = piece;
            btn.onClick.AddListener(() => OnAnswerPieceClicked(btn, val));
            activeButtons.Add(buttonObj);
        }
    }

    void OnAnswerPieceClicked(Button clickedButton, string piece)
    {
        if (isProcessingAction) return;
        clickedButton.interactable = false;
        currentAnswerPieces.Add(piece);
        selectedButtons.Add(clickedButton);
        UpdateCurrentAnswerDisplay();
    }

    public void UndoLastSelection()
    {
        if (isProcessingAction || selectedButtons.Count == 0) return;
        Button last = selectedButtons[^1];
        last.interactable = true;
        selectedButtons.RemoveAt(selectedButtons.Count - 1);
        currentAnswerPieces.RemoveAt(currentAnswerPieces.Count - 1);
        UpdateCurrentAnswerDisplay();
    }

    void UpdateCurrentAnswerDisplay()
    {
        if (currentAnswerText)
            currentAnswerText.text = string.Join(" ", currentAnswerPieces);
    }

    public void SubmitAnswer()
    {
        if (isProcessingAction || isSubmitting) return;
        if (Time.time - lastSubmitTime < submitCooldownTime) return;
        if (currentQ == null || currentAnswerPieces.Count == 0) return;

        isProcessingAction = true;
        isSubmitting = true;
        lastSubmitTime = Time.time;

        float timeSpent = Time.time - puzzleStartTime;
        string playerAnswer = NormalizeAnswer(string.Join("", currentAnswerPieces));
        string correctAnswer = NormalizeAnswer(currentQ.answer);
        bool isCorrect = (playerAnswer == correctAnswer);

        // CLEAN & DRY: Delegates telemetry and DDA cleanly
        ddaController.EvaluateAnswer(isCorrect, timeSpent);
        LogPuzzleAttempt(isCorrect, timeSpent);

        if (isCorrect)
            HandleCorrectAnswer();
        else
            HandleWrongAnswer();
    }

    protected override void OnHintActivated()
    {
        ShowHintExplanation();
        DisableHintButton();
    }

    private void ShowHintExplanation()
    {
        if (currentQ == null || explanationPanel == null) return;
        explanationPanel.SetActive(true);

        if (explanationAnswerText) explanationAnswerText.text = "Hint / Explanation";
        if (explanationBodyText)
        {
            explanationBodyText.text = string.IsNullOrWhiteSpace(currentQ.explanation)
                ? "No explanation available."
                : currentQ.explanation;
        }
    }

    void HandleCorrectAnswer()
    {
        correctAnswersCount++;
        stageCorrectCount++;

        int attemptsUsed = currentQuestionAttempts;
        float accuracy = 1f - ((float)attemptsUsed / Mathf.Max(1, maxAttemptsPerQuestion - 1));
        ProcessRankSuccess(accuracy);

        if (correctCounterText != null)
            correctCounterText.text = $"Progress: {stageCorrectCount}/{correctAnswersNeeded}";

        StartCoroutine(ShowCorrectThenExplanationFlow());
    }

    IEnumerator ShowCorrectThenExplanationFlow()
    {
        HideFeedbackPanels();
        if (correctPanel != null) correctPanel.SetActive(true);

        yield return new WaitForSeconds(feedbackDisplayDuration);

        if (correctPanel != null) correctPanel.SetActive(false);
        ShowExplanationPanel();
    }

    void HandleWrongAnswer()
    {
        currentQuestionAttempts++;
        UpdateAttemptsDisplay();
        ProcessRankFailure();

        if (currentQuestionAttempts >= maxAttemptsPerQuestion)
            StartCoroutine(DelayedNextQuestion(0.5f));
        else
            StartCoroutine(ShowWrongFeedbackFlow());
    }

    IEnumerator ShowWrongFeedbackFlow()
    {
        HideFeedbackPanels();
        if (wrongPanel != null) wrongPanel.SetActive(true);

        yield return new WaitForSeconds(feedbackDisplayDuration);

        if (wrongPanel != null) wrongPanel.SetActive(false);
        ResetCurrentAnswer();
    }

    void ShowExplanationPanel()
    {
        HideFeedbackPanels();
        if (explanationPanel == null) return;
        explanationPanel.SetActive(true);

        if (explanationAnswerText) explanationAnswerText.text = "Answer:  " + currentQ.answer;
        if (explanationBodyText)
        {
            explanationBodyText.text = string.IsNullOrWhiteSpace(currentQ.explanation)
                ? "No explanation available."
                : currentQ.explanation;
        }
    }

    void HideExplanationPanel()
    {
        if (explanationPanel) explanationPanel.SetActive(false);
    }

    void HideFeedbackPanels()
    {
        if (correctPanel) correctPanel.SetActive(false);
        if (wrongPanel) wrongPanel.SetActive(false);
    }

    public void OnGotItClicked()
    {
        if (waitingForNext) return;
        waitingForNext = true;

        HideExplanationPanel();

        if (stageCorrectCount >= correctAnswersNeeded)
            StartCoroutine(CompleteQuizAndCloseRoutine());
        else
            ResetForNewQuestion();
    }

    IEnumerator DelayedNextQuestion(float delay)
    {
        yield return new WaitForSeconds(delay);
        ResetForNewQuestion();
    }

    void ResetCurrentAnswer()
    {
        foreach (Button btn in selectedButtons)
            if (btn) btn.interactable = true;
        currentAnswerPieces.Clear();
        selectedButtons.Clear();
        UpdateCurrentAnswerDisplay();
        isProcessingAction = false;
    }

    IEnumerator CompleteQuizAndCloseRoutine()
    {
        isPuzzleCompleted = true;
        PlayerPrefs.SetInt(saveKey, 1);
        PlayerPrefs.Save();

        if (stageCompleteMessage)
        {
            stageCompleteMessage.text = "PUZZLE COMPLETED!";
            stageCompleteMessage.gameObject.SetActive(true);
        }

        bool sequenceFinished = false;
        TriggerStarRewardSequence(() => sequenceFinished = true);
        while (!sequenceFinished) yield return null;

        yield return new WaitForSeconds(stageCompleteDelay);
        if (stageCompleteMessage) stageCompleteMessage.gameObject.SetActive(false);

        ClosePuzzle();
    }
}