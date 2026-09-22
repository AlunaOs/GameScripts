using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using TMPro;
using System.IO;
using System.Collections;
using System.Collections.Generic;

public class PuzzleManager : MonoBehaviour
{
    [Header("UI Data Tracking Links")]
    public TextMeshProUGUI txtAttempts;
    public TextMeshProUGUI txtQuestion;
    public TextMeshProUGUI txtCurrentInput;
    public TextMeshProUGUI txtProgress;      // ── FIX: NEW — shows "Progress: 0/3"
    public Button btnSubmit;

    [Header("Object Spawning Parents")]
    public Transform gridContainer;
    public GameObject btnLetterPrefab;

    [Header("Player Control Safety Unfreeze")]
    public MonoBehaviour playerMovementScript;
    public GameObject playerControlsCanvas;

    [Header("Star Reward Controller")]
    public StarRewardAnimation starRewardAnimator;
    public RectTransform[] targetStarSlots;

    [Range(1, 3)]
    public int starToRewardNumber = 1;

    [Header("Lifting Wall Settings")]
    public bool enableLiftingWall = true;
    public Transform wallToLift;
    public float wallTargetRise = 5f;
    public float wallLiftSpeed = 2f;

    [Header("Visual Feedback Settings")]
    public Color colorDefaultInput = Color.white;
    public Color colorCorrectInput = new Color(0f, 0.96f, 0.63f, 1f);
    public Color colorWrongInput = new Color(1f, 0.42f, 0.42f, 1f);
    public Color colorSwipedBtn = new Color(0.5f, 0.5f, 0.5f, 1f);

    public float shakeDuration = 0.35f;
    public float shakeMagnitude = 8f;

    [Header("Hint Settings")]
    public HintScrollUI hintScrollUI;
    public GameObject hintExplanationPanel;
    public TMP_Text hintExplanationText;
    public Button hintGotItButton;

    [HideInInspector]
    public bool isCleared = false;

    // ── FIX: track whether the current 3-question set has been initialized.
    // Reopening the panel will NOT regenerate questions while this is true.
    private bool sessionInitialized = false;

    private const int gridWidth = 5;
    private const int gridHeight = 5;
    private char[,] gridMatrix = new char[gridWidth, gridHeight];

    private List<QuestionData> activeQuestions = new List<QuestionData>();
    private List<bool> answerSolvedStatus = new List<bool>();

    private string playerCurrentInput = "";
    private int wordsSolvedCount = 0;

    private int attemptsLeft = 3;
    private const int maxAttempts = 3;

    private string fillerPool = "abcdefghijklmnopqrstuvwxyz0123456789";
    private bool hintUsedThisPuzzle = false;

    private List<GridSwipeLetter> selectedPath = new List<GridSwipeLetter>();
    private bool isSwiping = false;
    private bool isProcessingAnswer = false;

    private float questionStartTime = 0f;
    private Vector3 originalAttemptsPosition;
    private Coroutine attemptsShakeCoroutine;
    private Coroutine flashInputCoroutine;

    private string saveKey;

    [System.Serializable]
    public class QuestionData
    {
        public string id;
        public string difficulty;
        public string prompt;
        public string answer;
        public string Hint_Explanation;
    }

    [System.Serializable]
    public class QuestionDataset
    {
        public List<QuestionData> questions;
    }

    private List<QuestionData> fullDataset = new List<QuestionData>();

    [HideInInspector]
    public bool isDatasetLoaded = false;

    void Awake()
    {
        saveKey = "LetterPuzzleCompleted_" + gameObject.name;

        StartCoroutine(LoadDatasetFromJSON());

        if (txtAttempts != null)
            originalAttemptsPosition = txtAttempts.rectTransform.anchoredPosition;

        if (btnSubmit != null)
        {
            btnSubmit.onClick.RemoveAllListeners();
            btnSubmit.onClick.AddListener(SubmitCurrentInput);
        }

        if (hintGotItButton != null)
            hintGotItButton.onClick.AddListener(HideHintExplanation);
    }

    void OnEnable()
    {
        if (PlayerPrefs.HasKey(saveKey) && PlayerPrefs.GetInt(saveKey) == 1)
            isCleared = true;
    }

    void Update()
    {
        if (isSwiping && (Input.GetMouseButtonUp(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Ended)))
            OnSwipeEnd();
    }

    IEnumerator LoadDatasetFromJSON()
    {
        string filePath = Path.Combine(Application.streamingAssetsPath, "Decode_questionSet.json");

        Debug.Log($"[PuzzleManager] Looking for JSON at: {filePath}");
        Debug.Log($"[PuzzleManager] File exists? {File.Exists(filePath)}");

#if UNITY_ANDROID && !UNITY_EDITOR
        using (UnityWebRequest request = UnityWebRequest.Get(filePath))
        {
            yield return request.SendWebRequest();
            if (request.result == UnityWebRequest.Result.Success)
            {
                QuestionDataset loadedData = JsonUtility.FromJson<QuestionDataset>(request.downloadHandler.text);
                if (loadedData == null || loadedData.questions == null)
                {
                    Debug.LogError("[PuzzleManager] JSON parsed but produced no questions list.");
                    yield break;
                }

                fullDataset = loadedData.questions.FindAll(q =>
                    q != null &&
                    !string.IsNullOrWhiteSpace(q.answer) &&
                    q.answer.Trim().Length <= gridWidth);

                isDatasetLoaded = true;
                Debug.Log($"[PuzzleManager] Loaded {fullDataset.Count} questions from Decode_questionSet.json");
            }
            else
            {
                Debug.LogError($"[PuzzleManager] JSON load failed: {filePath} — {request.error}");
            }
        }
#else
        if (File.Exists(filePath))
        {
            string jsonText = File.ReadAllText(filePath);
            QuestionDataset loadedData = JsonUtility.FromJson<QuestionDataset>(jsonText);

            if (loadedData == null || loadedData.questions == null)
            {
                Debug.LogError("[PuzzleManager] JSON parsed but produced no questions list.");
                yield break;
            }

            fullDataset = loadedData.questions.FindAll(q =>
                q != null &&
                !string.IsNullOrWhiteSpace(q.answer) &&
                q.answer.Trim().Length <= gridWidth);

            isDatasetLoaded = true;
            Debug.Log($"[PuzzleManager] Loaded {fullDataset.Count} questions from Decode_questionSet.json");
        }
        else
        {
            Debug.LogError($"[PuzzleManager] Missing file: {filePath}");
        }
        yield return null;
#endif
    }

    public void StartPuzzleSystem()
    {
        StartCoroutine(StartPuzzleSystemRoutine());
    }

    private IEnumerator StartPuzzleSystemRoutine()
    {
        while (!isDatasetLoaded)
            yield return null;

        if (isCleared)
        {
            Debug.Log("[PuzzleManager] Already cleared — skipping start.");
            yield break;
        }

        isProcessingAnswer = false;

        if (gridContainer != null) gridContainer.gameObject.SetActive(true);
        if (playerControlsCanvas != null) playerControlsCanvas.SetActive(false);

        TogglePlayerControls(false);

        // ── FIX: only initialize a fresh set of questions the FIRST time the
        // puzzle opens. On every subsequent open (after ClosePuzzleManually),
        // we keep the same questions and progress.
        if (!sessionInitialized)
        {
            attemptsLeft = maxAttempts;
            wordsSolvedCount = 0;
            Initialize3WordPuzzle();
            sessionInitialized = true;
        }
        else
        {
            // Reopen — restore UI to current state without regenerating anything.
            RestoreSessionUI();
        }
    }

    // ── FIX: NEW — rebuild the visual UI from the persisted state instead of
    // generating a new question set.
    private void RestoreSessionUI()
    {
        playerCurrentInput = "";
        hintUsedThisPuzzle = false;

        if (txtCurrentInput != null)
        {
            txtCurrentInput.text = "";
            txtCurrentInput.color = colorDefaultInput;
        }

        if (txtAttempts != null)
        {
            txtAttempts.text = $"ATTEMPTS: {attemptsLeft}";
            txtAttempts.rectTransform.anchoredPosition = originalAttemptsPosition;
        }

        UpdateActiveQuestionPrompt();
        UpdateProgressUI();

        questionStartTime = Time.time;
        BuildWordSearchGrid();
        CheckAndUnlockHint();

        Debug.Log($"[PuzzleManager] Reopened session — keeping {wordsSolvedCount}/3 solved.");
    }

    private static int ParseDifficulty(string d)
    {
        if (string.IsNullOrWhiteSpace(d)) return 1;
        if (int.TryParse(d, out int num)) return Mathf.Clamp(num, 1, 3);

        switch (d.Trim().ToLowerInvariant())
        {
            case "easy": return 1;
            case "medium": return 2;
            case "hard": return 3;
            default: return 1;
        }
    }

    void Initialize3WordPuzzle()
    {
        playerCurrentInput = "";
        isProcessingAnswer = false;
        hintUsedThisPuzzle = false;

        if (txtCurrentInput != null)
        {
            txtCurrentInput.text = "";
            txtCurrentInput.color = colorDefaultInput;
        }

        if (txtAttempts != null)
        {
            txtAttempts.text = $"ATTEMPTS: {attemptsLeft}";
            txtAttempts.rectTransform.anchoredPosition = originalAttemptsPosition;
        }

        SelectThreeTargetQuestions();
        UpdateActiveQuestionPrompt();
        UpdateProgressUI();          // ── FIX: NEW

        questionStartTime = Time.time;
        BuildWordSearchGrid();

        CheckAndUnlockHint();

        if (GameplayTelemetry.Instance != null)
        {
            int activeIdx = GetCurrentActiveQuestionIndex();
            int currentDiff = (activeIdx != -1) ? ParseDifficulty(activeQuestions[activeIdx].difficulty) : 1;
            string activeTopic = (GameManager.Instance != null) ? GameManager.Instance.currentCategory : "Basic concept of Algebra";
            GameplayTelemetry.Instance.BeginPuzzle(activeTopic, currentDiff);
        }
    }

    void SelectThreeTargetQuestions()
    {
        activeQuestions.Clear();
        answerSolvedStatus.Clear();

        int targetTier = (GameManager.Instance != null) ? GameManager.Instance.currentLevel : 1;
        List<QuestionData> pool = BuildPoolForTier(targetTier);

        for (int i = 0; i < Mathf.Min(3, pool.Count); i++)
        {
            activeQuestions.Add(pool[i]);
            answerSolvedStatus.Add(false);
        }

        Debug.Log($"[PuzzleManager] fullDataset.Count = {fullDataset.Count}, targetTier = {targetTier}, pool.Count = {pool.Count}, activeQuestions.Count = {activeQuestions.Count}");

        string picked = "";
        foreach (QuestionData q in activeQuestions) picked += $"[{q.id}:d{ParseDifficulty(q.difficulty)}] ";
        Debug.Log($"[DDA] PuzzleManager generated questions for DDA tier {targetTier}: {picked}");
    }

    private List<QuestionData> BuildPoolForTier(int tier)
    {
        List<QuestionData> pool = new List<QuestionData>();

        for (int distance = 0; distance <= 2 && pool.Count < 3; distance++)
        {
            List<QuestionData> group = new List<QuestionData>();
            foreach (QuestionData q in fullDataset)
            {
                int d = ParseDifficulty(q.difficulty);
                if (Mathf.Abs(d - tier) == distance) group.Add(q);
            }

            if (distance == 0 && group.Count == 0)
                Debug.LogWarning($"[DDA] Decode_questionSet.json has no questions at tier {tier}. Falling back to nearest tiers.");

            ShuffleQuestions(group);
            pool.AddRange(group);
        }

        if (pool.Count == 0)
            pool = new List<QuestionData>(fullDataset);

        return pool;
    }

    private void ShuffleQuestions(List<QuestionData> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int rnd = Random.Range(i, list.Count);
            (list[i], list[rnd]) = (list[rnd], list[i]);
        }
    }

    private int GetCurrentActiveQuestionIndex()
    {
        for (int i = 0; i < activeQuestions.Count; i++)
            if (!answerSolvedStatus[i]) return i;
        return -1;
    }

    void UpdateActiveQuestionPrompt()
    {
        if (txtQuestion == null)
        {
            Debug.LogError("[PuzzleManager] txtQuestion is not assigned in the Inspector — cannot display the prompt!");
            return;
        }

        int currentIndex = GetCurrentActiveQuestionIndex();
        if (currentIndex != -1)
            txtQuestion.text = activeQuestions[currentIndex].prompt;
        else
            txtQuestion.text = "<color=#00F5A0>ALL 3 PUZZLES SOLVED!</color>";
    }

    // ── FIX: NEW — progress label updater.
    private void UpdateProgressUI()
    {
        if (txtProgress == null) return;
        txtProgress.text = $"Progress: {wordsSolvedCount}/3";
    }

    void BuildWordSearchGrid()
    {
        if (gridContainer == null || btnLetterPrefab == null)
        {
            Debug.LogError("PuzzleManager fields missing! Check Grid Container and Button Prefab references.");
            return;
        }

        foreach (Transform child in gridContainer) Destroy(child.gameObject);

        for (int x = 0; x < gridWidth; x++)
            for (int y = 0; y < gridHeight; y++)
                gridMatrix[x, y] = ' ';

        foreach (QuestionData q in activeQuestions)
        {
            // ── FIX: only place the letters of questions that are NOT yet solved.
            // Previously, already-solved words were still being placed, which
            // made the grid look the same on reopen (ok) but also cluttered it.
            // This keeps the grid consistent with the remaining work.
            int idx = activeQuestions.IndexOf(q);
            if (idx != -1 && answerSolvedStatus[idx]) continue;

            string word = q.answer.ToLower().Trim();
            if (word.Length > gridWidth) word = word.Substring(0, gridWidth);

            bool placed = false;
            int attempts = 0;
            while (!placed && attempts < 100)
            {
                attempts++;
                int dir = Random.Range(0, 2);

                if (dir == 0 && word.Length <= gridWidth)
                {
                    int startX = Random.Range(0, gridWidth - word.Length + 1);
                    int startY = Random.Range(0, gridHeight);

                    bool canPlace = true;
                    for (int i = 0; i < word.Length; i++)
                    {
                        char existing = gridMatrix[startX + i, startY];
                        if (existing != ' ' && existing != word[i]) { canPlace = false; break; }
                    }
                    if (canPlace)
                    {
                        for (int i = 0; i < word.Length; i++)
                            gridMatrix[startX + i, startY] = word[i];
                        placed = true;
                    }
                }
                else if (dir == 1 && word.Length <= gridHeight)
                {
                    int startX = Random.Range(0, gridWidth);
                    int startY = Random.Range(0, gridHeight - word.Length + 1);

                    bool canPlace = true;
                    for (int i = 0; i < word.Length; i++)
                    {
                        char existing = gridMatrix[startX, startY + i];
                        if (existing != ' ' && existing != word[i]) { canPlace = false; break; }
                    }
                    if (canPlace)
                    {
                        for (int i = 0; i < word.Length; i++)
                            gridMatrix[startX, startY + i] = word[i];
                        placed = true;
                    }
                }
            }
        }

        for (int y = 0; y < gridHeight; y++)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                if (gridMatrix[x, y] == ' ')
                    gridMatrix[x, y] = fillerPool[Random.Range(0, fillerPool.Length)];

                GameObject setupButton = Instantiate(btnLetterPrefab, gridContainer);
                TextMeshProUGUI keyDisplayField = setupButton.GetComponentInChildren<TextMeshProUGUI>();

                char letter = gridMatrix[x, y];
                if (keyDisplayField != null)
                    keyDisplayField.text = letter.ToString().ToUpper();

                GridSwipeLetter swipeLetter = setupButton.GetComponent<GridSwipeLetter>();
                if (swipeLetter == null) swipeLetter = setupButton.AddComponent<GridSwipeLetter>();
                swipeLetter.Setup(x, y, letter.ToString().ToLower().Trim(), this);

                Button baseBtn = setupButton.GetComponent<Button>();
                if (baseBtn != null) baseBtn.onClick.RemoveAllListeners();
            }
        }
    }

    public bool IsSwipingActive() => isSwiping;

    public void OnSwipeStart(GridSwipeLetter cell)
    {
        if (isProcessingAnswer) return;
        ClearSwipeHighlights();
        selectedPath.Clear();
        isSwiping = true;
        AddCellToSwipePath(cell);
    }

    public void OnSwipeDragEnter(GridSwipeLetter cell)
    {
        if (!isSwiping || cell == null || isProcessingAnswer) return;

        if (selectedPath.Count >= 2 && selectedPath[selectedPath.Count - 2] == cell)
        {
            GridSwipeLetter last = selectedPath[selectedPath.Count - 1];
            last.ResetColor();
            selectedPath.RemoveAt(selectedPath.Count - 1);
            RebuildCurrentInputFromPath();
            return;
        }

        if (!selectedPath.Contains(cell) && IsAdjacent(selectedPath[selectedPath.Count - 1], cell))
            AddCellToSwipePath(cell);
    }

    public void OnSwipeEnd()
    {
        if (!isSwiping) return;
        isSwiping = false;
        RebuildCurrentInputFromPath();
    }

    private bool IsAdjacent(GridSwipeLetter a, GridSwipeLetter b)
    {
        int deltaX = Mathf.Abs(a.gridX - b.gridX);
        int deltaY = Mathf.Abs(a.gridY - b.gridY);
        return deltaX <= 1 && deltaY <= 1;
    }

    private void AddCellToSwipePath(GridSwipeLetter cell)
    {
        selectedPath.Add(cell);
        cell.SetHighlight(true, colorSwipedBtn);
        StartCoroutine(AnimateButtonPunch(cell.transform));
        RebuildCurrentInputFromPath();
    }

    private void RebuildCurrentInputFromPath()
    {
        playerCurrentInput = "";
        foreach (var cell in selectedPath) playerCurrentInput += cell.letter.ToLower().Trim();
        if (txtCurrentInput != null) txtCurrentInput.text = playerCurrentInput.ToUpper();
    }

    private void ClearSwipeHighlights()
    {
        foreach (var cell in selectedPath)
            if (cell != null) cell.ResetColor();
    }

    IEnumerator AnimateButtonPunch(Transform btnTransform)
    {
        if (btnTransform == null) yield break;

        Vector3 originalScale = Vector3.one;
        Vector3 pressedScale = originalScale * 0.85f;

        float elapsed = 0f;
        float pressDuration = 0.04f;
        while (elapsed < pressDuration)
        {
            elapsed += Time.deltaTime;
            btnTransform.localScale = Vector3.Lerp(originalScale, pressedScale, elapsed / pressDuration);
            yield return null;
        }

        elapsed = 0f;
        float bounceDuration = 0.08f;
        while (elapsed < bounceDuration)
        {
            elapsed += Time.deltaTime;
            btnTransform.localScale = Vector3.Lerp(pressedScale, originalScale, elapsed / bounceDuration);
            yield return null;
        }
        btnTransform.localScale = originalScale;
    }

    public void ClearCurrentInput()
    {
        playerCurrentInput = "";
        ClearSwipeHighlights();
        selectedPath.Clear();

        if (txtCurrentInput != null)
        {
            txtCurrentInput.text = "";
            txtCurrentInput.color = colorDefaultInput;
        }
    }

    public void SubmitCurrentInput()
    {
        if (isProcessingAnswer || string.IsNullOrEmpty(playerCurrentInput)) return;
        isProcessingAnswer = true;

        float timeSpent = Time.time - questionStartTime;
        string cleanedInput = playerCurrentInput.Trim().ToLower();

        int currentIndex = GetCurrentActiveQuestionIndex();
        bool isCorrect = false;
        string questionPrompt = "";
        int difficultyInt = 1;

        if (currentIndex != -1)
        {
            var q = activeQuestions[currentIndex];
            questionPrompt = q.prompt;
            difficultyInt = ParseDifficulty(q.difficulty);

            if (q.answer.Trim().ToLower() == cleanedInput) isCorrect = true;
        }

        if (GameplayTelemetry.Instance != null)
            GameplayTelemetry.Instance.LogAttempt(isCorrect, timeSpent, hintUsedThisPuzzle);

        if (GameManager.Instance != null && currentIndex != -1)
            GameManager.Instance.TrackQuestionPerformance(questionPrompt, difficultyInt, isCorrect, timeSpent);

        if (isCorrect)
        {
            answerSolvedStatus[currentIndex] = true;
            wordsSolvedCount++;
            UpdateProgressUI();                  // ── FIX: refresh progress label immediately

            StartFlashInput(colorCorrectInput, 0.25f, () =>
            {
                UpdateActiveQuestionPrompt();
                ClearCurrentInput();

                if (wordsSolvedCount >= 3)
                {
                    isCleared = true;
                    SaveClearedState();

                    if (txtQuestion != null)
                        txtQuestion.text = "<color=#00F5A0>ALL 3 PUZZLES SOLVED!</color>";

                    if (ShopManagers.Instance != null)
                        ShopManagers.Instance.AddStars(1);

                    if (RankManager.Instance != null)
                    {
                        int attemptsUsed = maxAttempts - attemptsLeft;
                        float accuracy = 1f - ((float)attemptsUsed / (maxAttempts - 1));
                        RankManager.Instance.ProcessPuzzleSuccess(accuracy);
                    }

                    if (enableLiftingWall && wallToLift != null)
                        StartCoroutine(LiftWallRoutine());

                    TriggerStarRewardSequence();
                }
                else
                {
                    // ── FIX: regenerate the grid (without the solved word) so the
                    // remaining two words are the only targets. Progress is kept.
                    BuildWordSearchGrid();

                    questionStartTime = Time.time;
                    isProcessingAnswer = false;
                    CheckAndUnlockHint();
                }
            });
        }
        else
        {
            attemptsLeft--;

            if (RankManager.Instance != null)
                RankManager.Instance.ProcessPuzzleFailure();

            VibrateAttemptsText();
            StartFlashInput(colorWrongInput, 0.4f, () =>
            {
                if (attemptsLeft <= 0)
                {
                    if (txtAttempts != null) txtAttempts.text = "ATTEMPTS: 0";
                    if (txtQuestion != null)
                        txtQuestion.text = "<color=#FF6B6B>PUZZLE LOCKED. CLOSED AUTOMATICALLY.</color>";
                    StartCoroutine(AutoCloseAfterDelay());
                }
                else
                {
                    if (txtAttempts != null) txtAttempts.text = $"ATTEMPTS: {attemptsLeft}";
                    ClearCurrentInput();
                    questionStartTime = Time.time;
                    isProcessingAnswer = false;
                }
            });
        }
    }

    IEnumerator LiftWallRoutine()
    {
        Vector3 startPos = wallToLift.position;
        Vector3 targetPos = startPos + new Vector3(0f, wallTargetRise, 0f);
        float elapsed = 0f;
        float duration = wallTargetRise / Mathf.Max(wallLiftSpeed, 0.001f);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            wallToLift.position = Vector3.Lerp(startPos, targetPos, t);
            yield return null;
        }
        wallToLift.position = targetPos;
    }

    void VibrateAttemptsText()
    {
        if (txtAttempts == null) return;
        if (attemptsShakeCoroutine != null) StopCoroutine(attemptsShakeCoroutine);
        attemptsShakeCoroutine = StartCoroutine(ShakeAttemptsTextRoutine());
    }

    IEnumerator ShakeAttemptsTextRoutine()
    {
        RectTransform rectTransform = txtAttempts.rectTransform;
        float elapsed = 0f;
        while (elapsed < shakeDuration)
        {
            elapsed += Time.deltaTime;
            float offsetX = Random.Range(-shakeMagnitude, shakeMagnitude);
            float offsetY = Random.Range(-shakeMagnitude, shakeMagnitude);
            rectTransform.anchoredPosition = originalAttemptsPosition + new Vector3(offsetX, offsetY, 0f);
            yield return null;
        }
        rectTransform.anchoredPosition = originalAttemptsPosition;
    }

    void StartFlashInput(Color flashColor, float duration, System.Action onComplete)
    {
        if (flashInputCoroutine != null) StopCoroutine(flashInputCoroutine);
        flashInputCoroutine = StartCoroutine(FlashInputTextRoutine(flashColor, duration, onComplete));
    }

    IEnumerator FlashInputTextRoutine(Color flashColor, float duration, System.Action onComplete)
    {
        if (txtCurrentInput != null) txtCurrentInput.color = flashColor;
        yield return new WaitForSeconds(duration);
        if (txtCurrentInput != null) txtCurrentInput.color = colorDefaultInput;
        onComplete?.Invoke();
    }

    void TriggerStarRewardSequence()
    {
        if (starRewardAnimator != null)
        {
            int arrayIndex = Mathf.Clamp(starToRewardNumber - 1, 0, targetStarSlots.Length - 1);
            if (targetStarSlots != null && targetStarSlots.Length > arrayIndex && targetStarSlots[arrayIndex] != null)
                starRewardAnimator.PlayStarRewardSequence(targetStarSlots[arrayIndex], () => StartCoroutine(AutoCloseAfterDelay()));
            else
                starRewardAnimator.PlayStarRewardSequence(() => StartCoroutine(AutoCloseAfterDelay()));
        }
        else StartCoroutine(AutoCloseAfterDelay());
    }

    IEnumerator AutoCloseAfterDelay()
    {
        yield return new WaitForSeconds(0.5f);
        ClosePuzzleManually();
    }

    // ── FIX: closing now HIDES the UI without wiping the session state.
    // The next StartPuzzleSystem() call will just restore the same questions
    // and progress via RestoreSessionUI().
    public void ClosePuzzleManually()
    {
        ClearCurrentInput();
        HideHintExplanation();
        isProcessingAnswer = false;

        if (playerControlsCanvas != null) playerControlsCanvas.SetActive(true);
        TogglePlayerControls(true);
        gameObject.SetActive(false);
    }

    void TogglePlayerControls(bool state)
    {
        if (playerMovementScript != null) playerMovementScript.enabled = state;

        if (state)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    // ────────────────────────────── HINT UI ────────────────────────────────────
    public void CheckAndUnlockHint()
    {
        if (ShopManagers.Instance != null && ShopManagers.Instance.hasHintScroll && !isCleared)
            EnableHintButton();
        else
            DisableHintButton();
    }

    public void EnableHintButton()
    {
        if (isCleared || hintScrollUI == null) return;
        hintScrollUI.gameObject.SetActive(true);
        hintScrollUI.EnableHint(() =>
        {
            if (ShopManagers.Instance != null) ShopManagers.Instance.UseHintScroll();
            ShowHintExplanation();
            DisableHintButton();
        });
    }

    public void DisableHintButton()
    {
        if (hintScrollUI != null) hintScrollUI.gameObject.SetActive(false);
    }

    private void ShowHintExplanation()
    {
        int idx = GetCurrentActiveQuestionIndex();
        if (idx == -1) return;

        hintUsedThisPuzzle = true;

        if (hintExplanationPanel != null) hintExplanationPanel.SetActive(true);

        string txt = activeQuestions[idx].Hint_Explanation;
        if (string.IsNullOrWhiteSpace(txt))
            txt = "Read the question carefully and identify the operation being asked.";

        if (hintExplanationText != null)
            hintExplanationText.text = txt;
    }

    public void HideHintExplanation()
    {
        if (hintExplanationPanel != null) hintExplanationPanel.SetActive(false);
    }
    // ───────────────────────────────────────────────────────────────────────────

    private void SaveClearedState()
    {
        PlayerPrefs.SetInt(saveKey, 1);
        PlayerPrefs.Save();
    }

    // ── FIX: a full reset now also clears sessionInitialized so the next open
    // generates a fresh 3-question set.
    public void ResetPuzzleState()
    {
        PlayerPrefs.DeleteKey(saveKey);
        isCleared = false;
        isProcessingAnswer = false;
        wordsSolvedCount = 0;
        attemptsLeft = maxAttempts;
        sessionInitialized = false;
        activeQuestions.Clear();
        answerSolvedStatus.Clear();
        ClearCurrentInput();
        UpdateProgressUI();
        HideHintExplanation();
        gameObject.SetActive(false);
    }
}
