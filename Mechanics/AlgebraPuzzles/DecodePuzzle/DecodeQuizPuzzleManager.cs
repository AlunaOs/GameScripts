using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class DecodeQuizPuzzleManager : MonoBehaviour
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
    private Vector2 questionPanelTargetPos;
    private bool panelInitialPositionCached = false;

    [Header("Feedback Area Panels")]
    public GameObject correctPanel;
    public GameObject wrongPanel;
    public float feedbackDisplayDuration = 1.0f;

    public GameObject characterUIControls;

    [Header("Button Containers")]
    public Transform answerButtonsContainer;
    public GameObject answerPieceButtonPrefab;

    [Header("Control Buttons")]
    public Button submitButton;
    public Button undoButton;
    public Button closeButton;

    // ── POST-ANSWER EXPLANATION CARD ────────────────────────────────────────
    // Shown AFTER the player answers correctly. Contains the worked solution
    // (can safely reveal the answer since the player already solved it).
    [Header("Explanation Panel (post-answer card)")]
    public GameObject explanationPanel;
    public TMP_Text explanationAnswerText;
    public TMP_Text explanationBodyText;
    public Button gotItButton;

    // ── HINT SYSTEM (separate from explanation) ─────────────────────────────
    // Shown when the player presses the Hint button, BEFORE answering.
    // Reads only the `hint` field — never `explanation`.
    [Header("Hint Panel (separate canvas — reads only the 'hint' field)")]
    public HintScrollUI hintScrollUI;
    public GameObject hintExplanationPanel;
    public TMP_Text hintExplanationText;
    public Button hintGotItButton;

    [Header("Star Reward Controller")]
    public StarRewardAnimation starRewardAnimator;
    public RectTransform[] targetStarSlots;
    public GameObject objectToActivateOnCompletion;

    [Header("Progress Settings")]
    public int correctAnswersNeeded = 3;
    public int maxAttemptsPerQuestion = 3;
    public float stageCompleteDelay = 1.5f;
    public float submitCooldownTime = 0.5f;

    [HideInInspector]
    public bool isCompleted = false;
    private bool hintUsedThisQuiz = false;

    private QuestionGenerator questionGenerator = new QuestionGenerator();
    [HideInInspector]
    public bool isDatasetLoaded => questionGenerator.IsLoaded;

    // Private state
    private int correctAnswersCount = 0;
    private int currentQuestionAttempts = 0;
    private int stageCorrectCount = 0;
    private float questionStartTime;
    private int currentQuestionTier = 1;

    private Question currentQ;
    private List<string> currentAnswerPieces = new List<string>();
    private List<string> correctAnswerPieces = new List<string>();
    private List<string> allAnswerPieces = new List<string>();
    private List<GameObject> activeButtons = new List<GameObject>();
    private List<Button> selectedButtons = new List<Button>();

    private bool isQuizActive = false;
    private bool isSubmitting = false;
    private bool isProcessingAnswer = false;
    private float lastSubmitTime = 0f;
    private bool waitingForNext = false;
    private string saveKey;

    void Awake()
    {
        saveKey = "DecodeQuizCompleted_" + gameObject.name;

        if (submitButton) submitButton.onClick.AddListener(SubmitAnswer);
        if (undoButton) undoButton.onClick.AddListener(UndoLastSelection);
        if (closeButton) closeButton.onClick.AddListener(OnCloseButtonPressed);
        if (gotItButton) gotItButton.onClick.AddListener(OnGotItClicked);
        if (hintGotItButton) hintGotItButton.onClick.AddListener(HideHintExplanation);

        if (questionPanel != null && !panelInitialPositionCached)
        {
            questionPanelTargetPos = questionPanel.anchoredPosition;
            panelInitialPositionCached = true;
        }

        StartCoroutine(questionGenerator.LoadTemplates());
    }

    void OnEnable()
    {
        LoadQuizState();
    }

    void Start()
    {
        HideExplanationPanel();
        HideHintExplanation();
        HideFeedbackPanels();
        DisableHintButton();
    }

    void Update()
    {
        if (isSubmitting && Time.time - lastSubmitTime > submitCooldownTime)
            isSubmitting = false;
    }

    public void OnQuizOpened()
    {
        if (isCompleted) return;

        StopAllCoroutines();

        isQuizActive = true;
        waitingForNext = false;
        gameObject.SetActive(true);

        if (characterUIControls != null)
            characterUIControls.SetActive(false);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        stageCorrectCount = 0;
        correctAnswersCount = 0;
        isProcessingAnswer = false;
        isSubmitting = false;
        hintUsedThisQuiz = false;

        HideExplanationPanel();
        HideHintExplanation();
        HideFeedbackPanels();
        DisableHintButton();

        if (questionPanel != null)
        {
            if (!panelInitialPositionCached)
            {
                questionPanelTargetPos = questionPanel.anchoredPosition;
                panelInitialPositionCached = true;
            }
            StartCoroutine(SlideInQuestionPanel());
        }

        StartCoroutine(InitializeQuiz());
    }

    IEnumerator SlideInQuestionPanel()
    {
        float screenWidth = Screen.width;
        Vector2 startPos = questionPanelTargetPos + new Vector2(screenWidth, 0);
        questionPanel.anchoredPosition = startPos;

        float elapsed = 0f;
        while (elapsed < panelSlideDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / panelSlideDuration);
            t = t * t * (3f - 2f * t);
            questionPanel.anchoredPosition = Vector2.Lerp(startPos, questionPanelTargetPos, t);
            yield return null;
        }
        questionPanel.anchoredPosition = questionPanelTargetPos;
    }

    IEnumerator InitializeQuiz()
    {
        while (!questionGenerator.IsLoaded)
        {
            yield return null;
        }
        ResetForNewQuestion();
    }

    public void CloseQuiz()
    {
        StopAllCoroutines();
        isQuizActive = false;
        isProcessingAnswer = false;
        isSubmitting = false;
        waitingForNext = false;
        ResetAllState();
        HideExplanationPanel();
        HideHintExplanation();
        HideFeedbackPanels();
        DisableHintButton();

        if (characterUIControls != null)
            characterUIControls.SetActive(true);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

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
    }

    public void OnCloseButtonPressed() => CloseQuiz();

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
        hintUsedThisQuiz = false;
        UpdateAttemptsDisplay();

        if (stageCompleteMessage) stageCompleteMessage.gameObject.SetActive(false);
        HideExplanationPanel();
        HideHintExplanation();
        HideFeedbackPanels();
        DisableHintButton();

        if (questionGenerator.IsLoaded)
        {
            // DDA (through GameManager) is the single source of truth for difficulty.
            currentQuestionTier = (GameManager.Instance != null) ? GameManager.Instance.currentLevel : 1;
            currentQ = questionGenerator.GetQuestion(currentQuestionTier, "Algebra");
            questionStartTime = Time.time;
            Debug.Log($"[DDA] DecodeQuizPuzzleManager: requested tier {currentQuestionTier}, question difficulty = {(currentQ != null ? currentQ.difficulty.ToString() : "none")}");
        }
        else
        {
            currentQ = null;
        }

        if (currentQ != null)
        {
            if (stageText != null) stageText.text = "DECODE CHALLENGE";
            if (questionText) questionText.text = currentQ.text;

            ParseAnswerIntoPieces();
            GenerateAllButtons();
            if (correctCounterText != null)
                correctCounterText.text = $"Progress: {stageCorrectCount}/{correctAnswersNeeded}";

            if (GameplayTelemetry.Instance != null)
            {
                string activeTopic = (GameManager.Instance != null) ? GameManager.Instance.currentCategory : "Linear Equations and Inequalities";
                GameplayTelemetry.Instance.BeginPuzzle(activeTopic, currentQuestionTier);
            }
        }
        else
        {
            if (questionText) questionText.text = "No questions found in dataset.";
            Debug.LogError("[DecodeQuizPuzzleManager] currentQ is NULL from QuestionGenerator.");
        }

        isProcessingAnswer = false;
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
        if (isProcessingAnswer) return;
        clickedButton.interactable = false;
        currentAnswerPieces.Add(piece);
        selectedButtons.Add(clickedButton);
        UpdateCurrentAnswerDisplay();
    }

    public void UndoLastSelection()
    {
        if (isProcessingAnswer || selectedButtons.Count == 0) return;
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
        if (isProcessingAnswer || isSubmitting) return;
        if (Time.time - lastSubmitTime < submitCooldownTime) return;
        if (currentQ == null || currentAnswerPieces.Count == 0) return;

        isProcessingAnswer = true;
        isSubmitting = true;
        lastSubmitTime = Time.time;

        float timeSpent = Time.time - questionStartTime;
        string playerAnswer = NormalizeAnswer(string.Join("", currentAnswerPieces));
        string correctAnswer = NormalizeAnswer(currentQ.answer);
        bool isCorrect = (playerAnswer == correctAnswer);

        if (GameplayTelemetry.Instance != null)
        {
            GameplayTelemetry.Instance.LogAttempt(isCorrect, timeSpent, hintUsedThisQuiz);
        }

        // Single source of truth for DDA + telemetry.
        if (GameManager.Instance != null)
        {
            GameManager.Instance.TrackQuestionPerformance(
                currentQ.text,
                currentQuestionTier,
                isCorrect,
                timeSpent
            );
        }

        if (isCorrect)
        {
            HandleCorrectAnswer();
        }
        else
        {
            questionStartTime = Time.time;   // time each attempt separately
            HandleWrongAnswer();
        }
    }

    // ── HINT SYSTEM (separate from explanation) ─────────────────────────────
    public void CheckAndUnlockHint()
    {
        if (ShopManagers.Instance != null && ShopManagers.Instance.hasHintScroll && !isCompleted)
            EnableHintButton();
        else
            DisableHintButton();
    }

    public void EnableHintButton()
    {
        if (isCompleted) return;
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
        if (currentQ == null) return;

        hintUsedThisQuiz = true;

        if (hintExplanationPanel != null)
            hintExplanationPanel.SetActive(true);
        string hintText = currentQ.hint;
        if (string.IsNullOrWhiteSpace(hintText))
            hintText = "Read the question carefully and identify the operation being asked.";

        if (hintExplanationText != null)
            hintExplanationText.text = hintText;
    }

    public void HideHintExplanation()
    {
        if (hintExplanationPanel != null)
            hintExplanationPanel.SetActive(false);
    }
    // ────────────────────────────────────────────────────────────────────────

    void HandleCorrectAnswer()
    {
        correctAnswersCount++;
        stageCorrectCount++;

        if (RankManager.Instance != null)
        {
            int attemptsUsed = currentQuestionAttempts;
            float accuracy = 1f - ((float)attemptsUsed / (maxAttemptsPerQuestion - 1));
            RankManager.Instance.ProcessPuzzleSuccess(accuracy);
        }

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

        if (RankManager.Instance != null)
            RankManager.Instance.ProcessPuzzleFailure();

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

    // This is the POST-ANSWER card. It is allowed to reveal the answer because
    // the player already solved the question.
    void ShowExplanationPanel()
    {
        HideFeedbackPanels();
        if (explanationPanel == null) return;
        explanationPanel.SetActive(true);

        if (explanationAnswerText)
            explanationAnswerText.text = "Answer:  " + currentQ.answer;

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
            StartCoroutine(CompleteQuizAndClose());
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
        isProcessingAnswer = false;
        isSubmitting = false;
    }

    IEnumerator CompleteQuizAndClose()
    {
        if (isCompleted)
        {
            CloseQuiz();
            yield break;
        }

        isCompleted = true;
        SaveQuizState();

        if (ShopManagers.Instance != null)
            ShopManagers.Instance.AddStars(1);

        if (stageCompleteMessage)
        {
            stageCompleteMessage.text = "PUZZLE COMPLETED!";
            stageCompleteMessage.gameObject.SetActive(true);
        }

        bool starAnimationFinished = false;

        if (starRewardAnimator != null && targetStarSlots != null && targetStarSlots.Length > 0)
        {
            RectTransform nextAvailableStarSlot = GetNextAvailableStarSlot();

            if (nextAvailableStarSlot != null)
            {
                starRewardAnimator.PlayStarRewardSequence(nextAvailableStarSlot, () =>
                {
                    starAnimationFinished = true;
                });

                while (!starAnimationFinished)
                    yield return null;
            }
        }

        yield return new WaitForSeconds(stageCompleteDelay);

        if (stageCompleteMessage) stageCompleteMessage.gameObject.SetActive(false);

        CloseQuiz();

        if (objectToActivateOnCompletion != null)
            objectToActivateOnCompletion.SetActive(true);
    }

    private RectTransform GetNextAvailableStarSlot()
    {
        foreach (RectTransform starSlot in targetStarSlots)
        {
            if (starSlot == null) continue;

            if (starSlot.childCount > 0)
            {
                if (!starSlot.GetChild(0).gameObject.activeSelf)
                    return starSlot.childCount > 0 ? (RectTransform)starSlot.GetChild(0) : starSlot;
            }
            else if (!starSlot.gameObject.activeSelf)
            {
                return starSlot;
            }
        }

        return targetStarSlots[targetStarSlots.Length - 1];
    }

    private void SaveQuizState()
    {
        PlayerPrefs.SetInt(saveKey, 1);
        PlayerPrefs.Save();
    }

    private void LoadQuizState()
    {
        if (PlayerPrefs.HasKey(saveKey) && PlayerPrefs.GetInt(saveKey) == 1)
        {
            isCompleted = true;

            if (objectToActivateOnCompletion != null)
                objectToActivateOnCompletion.SetActive(true);
        }
    }

    public void ResetQuizState()
    {
        PlayerPrefs.DeleteKey(saveKey);
        isCompleted = false;
        stageCorrectCount = 0;
        correctAnswersCount = 0;
        isQuizActive = false;
        hintUsedThisQuiz = false;
        ResetAllState();
        DisableHintButton();
        HideHintExplanation();
        HideExplanationPanel();

        if (objectToActivateOnCompletion != null)
            objectToActivateOnCompletion.SetActive(false);

        gameObject.SetActive(false);
    }
}
