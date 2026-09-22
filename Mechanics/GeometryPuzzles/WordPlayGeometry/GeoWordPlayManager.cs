using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class GeoWordPlayManager : MonoBehaviour
{
    [Header("UI Data Tracking Links")]
    public TextMeshProUGUI txtAttempts;
    public TextMeshProUGUI txtQuestion;
    public TextMeshProUGUI txtCurrentInput;
    public Button btnSubmit;

    [Header("Congruence Feedback Area")]
    public GameObject feedbackPanel;
    public TextMeshProUGUI txtFeedback;
    public GameObject iconCorrect;
    public GameObject iconWrong;
    public float feedbackDisplayTime = 1.2f;

    [Header("Player Kit UI Settings")]
    public GameObject playerKitCanvas;

    [Header("Player Control Safety Unfreeze")]
    public MonoBehaviour playerMovementScript;

    // ── LEGACY HINT UI (kept for backward compat) ──────────────────────────
    [Header("Legacy Hint UI (optional — prefer Hint Panel below)")]
    public Button btnHint;
    public GameObject hintUI;
    public TextMeshProUGUI txtHintText;
    public Button btnCloseHint;
    public float hintAnimDuration = 0.3f;

    // ── HINT PANEL (separate canvas — reads only the 'hint' field) ─────────
    [Header("Hint Panel (separate canvas — reads only the 'hint' field)")]
    public HintScrollUI hintScrollUI;
    public GameObject hintExplanationPanel;
    public TMP_Text hintExplanationText;
    public Button hintGotItButton;

    [Header("Moving Wall / Gate Settings")]
    [SerializeField] private bool useMovingWall = false;
    [SerializeField] private Transform movingWall;
    [SerializeField] private float targetRiseHeight = 5f;
    [SerializeField] private float liftSpeed = 5f;

    [Header("Difficulty Settings")]
    public string currentDifficulty = "easy";

    [Header("Object Spawning Parents")]
    public Transform gridContainer;
    public GameObject btnLetterPrefab;

    [Header("Star Reward Controller")]
    public StarRewardAnimation starRewardAnimator;
    public RectTransform targetStarSlot;

    [Header("Visual Feedback Settings")]
    public Color colorDefaultInput = Color.white;
    public Color colorCorrectInput = new Color(0f, 0.96f, 0.63f, 1f);
    public Color colorWrongInput = new Color(1f, 0.42f, 0.42f, 1f);
    public Color colorSwipedBtn = new Color(0.5f, 0.5f, 0.5f, 1f);

    public float shakeDuration = 0.35f;
    public float shakeMagnitude = 8f;

    [HideInInspector]
    public bool isCleared = false;
    private bool hintUsedThisPuzzle = false;

    private const int gridWidth = 7;
    private const int gridHeight = 7;
    private char[,] gridMatrix = new char[gridWidth, gridHeight];

    private GeoWordDataSet dataSetLoader;
    private List<GeoWordDataSet.QuestionData> activeQuestions = new List<GeoWordDataSet.QuestionData>();
    private List<bool> answerSolvedStatus = new List<bool>();

    private string playerCurrentInput = "";
    private int wordsSolvedCount = 0;

    private int attemptsLeft = 3;
    private const int maxAttempts = 3;

    private string fillerPool = "abcdefghijklmnopqrstuvwxyz";

    private List<GeoGridSwipe> selectedPath = new List<GeoGridSwipe>();
    private bool isSwiping = false;

    private bool isProcessingAnswer = false;
    private float questionStartTime = 0f;
    private Vector3 originalAttemptsPosition;
    private Vector3 initialWallPosition;
    private Coroutine attemptsShakeCoroutine;
    private Coroutine flashInputCoroutine;
    private Coroutine hintAnimCoroutine;
    private Coroutine feedbackCoroutine;

    private RectTransform hintRectTransform;
    private Vector2 hintOriginalAnchoredPos;

    private List<RaycastResult> raycastResults = new List<RaycastResult>();
    private PointerEventData cachedPointerEventData;

    void Awake()
    {
        if (movingWall != null) initialWallPosition = movingWall.position;

        dataSetLoader = GetComponent<GeoWordDataSet>();
        if (dataSetLoader == null) dataSetLoader = gameObject.AddComponent<GeoWordDataSet>();
        dataSetLoader.LoadData();

        if (txtAttempts != null)
            originalAttemptsPosition = txtAttempts.rectTransform.anchoredPosition;

        if (hintUI != null)
        {
            hintRectTransform = hintUI.GetComponent<RectTransform>();
            if (hintRectTransform != null)
                hintOriginalAnchoredPos = hintRectTransform.anchoredPosition;
        }

        if (btnSubmit != null)
        {
            btnSubmit.onClick.RemoveAllListeners();
            btnSubmit.onClick.AddListener(SubmitCurrentInput);
        }

        if (btnHint != null)
        {
            btnHint.onClick.RemoveAllListeners();
            btnHint.onClick.AddListener(ShowCurrentHint);
        }

        if (btnCloseHint != null)
        {
            btnCloseHint.onClick.RemoveAllListeners();
            btnCloseHint.onClick.AddListener(CloseHintUI);
        }

        if (hintGotItButton != null)
            hintGotItButton.onClick.AddListener(HideHintExplanation);
    }

    void Update()
    {
        if (!isSwiping) return;

        if (Input.GetMouseButtonUp(0) ||
            (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Ended))
        {
            OnSwipeEnd();
            return;
        }

        Vector2 pointerPos = Input.touchCount > 0
            ? Input.GetTouch(0).position
            : (Vector2)Input.mousePosition;

        if (EventSystem.current == null) return;
        if (cachedPointerEventData == null) cachedPointerEventData = new PointerEventData(EventSystem.current);

        cachedPointerEventData.position = pointerPos;
        raycastResults.Clear();
        EventSystem.current.RaycastAll(cachedPointerEventData, raycastResults);

        for (int i = 0; i < raycastResults.Count; i++)
        {
            GeoGridSwipe cell = raycastResults[i].gameObject.GetComponent<GeoGridSwipe>();
            if (cell == null) cell = raycastResults[i].gameObject.GetComponentInParent<GeoGridSwipe>();

            if (cell != null)
            {
                OnSwipeDragEnter(cell);
                break;
            }
        }
    }

    public void StartPuzzleSystem()
    {
        StartCoroutine(StartPuzzleSystemRoutine());
    }

    private IEnumerator StartPuzzleSystemRoutine()
    {
        while (!dataSetLoader.isLoaded)
            yield return null;

        isCleared = false;
        isProcessingAnswer = false;
        isSwiping = false;
        wordsSolvedCount = 0;
        attemptsLeft = maxAttempts;
        hintUsedThisPuzzle = false;

        HideFeedback();
        HideHintExplanation();
        DisableHintButton();
        SetPlayerMovementState(false);

        if (playerKitCanvas != null) playerKitCanvas.SetActive(false);
        if (gridContainer != null) gridContainer.gameObject.SetActive(true);
        if (hintUI != null) hintUI.SetActive(false);

        Initialize3WordPuzzle();
    }

    void Initialize3WordPuzzle()
    {
        playerCurrentInput = "";
        isProcessingAnswer = false;
        isSwiping = false;

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

        if (GameplayTelemetry.Instance != null)
        {
            string puzzleTopic = (GameManager.Instance != null && !string.IsNullOrEmpty(GameManager.Instance.currentCategory))
                ? GameManager.Instance.currentCategory
                : "Geometry Vocabulary";

            int diffLevel = currentDifficulty.ToLower() == "hard" ? 3 : (currentDifficulty.ToLower() == "medium" ? 2 : 1);
            GameplayTelemetry.Instance.BeginPuzzle(puzzleTopic, diffLevel);
            GameplayTelemetry.Instance.LogQuestionTier(diffLevel);
            GameplayTelemetry.Instance.LogAttemptNumber(maxAttempts - attemptsLeft + 1);
        }

        questionStartTime = Time.time;
        BuildWordSearchGrid();
        CheckAndUnlockHint();
    }

    void SelectThreeTargetQuestions()
    {
        activeQuestions.Clear();
        answerSolvedStatus.Clear();

        if (dataSetLoader == null || dataSetLoader.Database == null) return;

        List<GeoWordDataSet.QuestionData> pool = dataSetLoader.Database.questions.FindAll(
            q => q.difficulty.ToLower() == currentDifficulty.ToLower()
                 && q.answer.Trim().Length <= gridWidth
                 && q.answer.Trim().Length >= 3);

        if (pool.Count < 3)
        {
            pool = dataSetLoader.Database.questions.FindAll(
                q => q.answer.Trim().Length <= gridWidth
                     && q.answer.Trim().Length >= 3);
        }

        for (int i = 0; i < pool.Count; i++)
        {
            int rnd = Random.Range(i, pool.Count);
            GeoWordDataSet.QuestionData temp = pool[i];
            pool[i] = pool[rnd];
            pool[rnd] = temp;
        }

        for (int i = 0; i < Mathf.Min(3, pool.Count); i++)
        {
            activeQuestions.Add(pool[i]);
            answerSolvedStatus.Add(false);
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
        if (txtQuestion == null) return;

        int currentIndex = GetCurrentActiveQuestionIndex();
        if (currentIndex != -1)
            txtQuestion.text = activeQuestions[currentIndex].prompt;
    }

    public void DisplayFeedback(string message, bool isCorrect)
    {
        if (feedbackCoroutine != null) StopCoroutine(feedbackCoroutine);
        feedbackCoroutine = StartCoroutine(ShowFeedbackRoutine(message, isCorrect));
    }

    private IEnumerator ShowFeedbackRoutine(string message, bool isCorrect)
    {
        if (feedbackPanel != null) feedbackPanel.SetActive(true);

        if (txtFeedback != null)
        {
            txtFeedback.text = message;
            txtFeedback.color = isCorrect ? colorCorrectInput : colorWrongInput;
        }

        if (iconCorrect != null) iconCorrect.SetActive(isCorrect);
        if (iconWrong != null) iconWrong.SetActive(!isCorrect);

        yield return new WaitForSeconds(feedbackDisplayTime);
        HideFeedback();
    }

    public void HideFeedback()
    {
        if (feedbackPanel != null) feedbackPanel.SetActive(false);
        if (iconCorrect != null) iconCorrect.SetActive(false);
        if (iconWrong != null) iconWrong.SetActive(false);
        if (txtFeedback != null) txtFeedback.text = "";
    }

    // ── HINT SYSTEM ─────────────────────────────────────────────────────────

    // Legacy path (kept for the old hintUI prefab if it's still in the scene).
    public void ShowCurrentHint()
    {
        int currentIndex = GetCurrentActiveQuestionIndex();
        if (currentIndex == -1) return;

        hintUsedThisPuzzle = true;

        if (txtHintText != null)
            txtHintText.text = activeQuestions[currentIndex].hint;

        if (hintUI != null && hintRectTransform != null)
        {
            if (hintAnimCoroutine != null) StopCoroutine(hintAnimCoroutine);
            hintAnimCoroutine = StartCoroutine(SlideInHintUI());
        }
    }

    public void CloseHintUI()
    {
        if (hintUI != null && hintUI.activeSelf && hintRectTransform != null)
        {
            if (hintAnimCoroutine != null) StopCoroutine(hintAnimCoroutine);
            hintAnimCoroutine = StartCoroutine(SlideOutHintUI());
        }
    }

    private IEnumerator SlideInHintUI()
    {
        hintUI.SetActive(true);

        float offscreenX = Screen.width;
        if (hintRectTransform.parent != null)
            offscreenX = ((RectTransform)hintRectTransform.parent).rect.width;

        Vector2 startPos = new Vector2(offscreenX, hintOriginalAnchoredPos.y);
        Vector2 targetPos = hintOriginalAnchoredPos;
        hintRectTransform.anchoredPosition = startPos;

        float elapsed = 0f;
        while (elapsed < hintAnimDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / hintAnimDuration;
            t = t * t * (3f - 2f * t);
            hintRectTransform.anchoredPosition = Vector2.Lerp(startPos, targetPos, t);
            yield return null;
        }
        hintRectTransform.anchoredPosition = targetPos;
    }

    private IEnumerator SlideOutHintUI()
    {
        float offscreenX = Screen.width;
        if (hintRectTransform.parent != null)
            offscreenX = ((RectTransform)hintRectTransform.parent).rect.width;

        Vector2 startPos = hintRectTransform.anchoredPosition;
        Vector2 targetPos = new Vector2(offscreenX, hintOriginalAnchoredPos.y);

        float elapsed = 0f;
        while (elapsed < hintAnimDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / hintAnimDuration;
            t = t * t * (3f - 2f * t);
            hintRectTransform.anchoredPosition = Vector2.Lerp(startPos, targetPos, t);
            yield return null;
        }
        hintRectTransform.anchoredPosition = targetPos;
        hintUI.SetActive(false);
    }

    // New HintScrollUI-based path — matches the other two puzzles.
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
        int idx = GetCurrentActiveQuestionIndex();
        if (idx == -1) return;

        hintUsedThisPuzzle = true;

        if (hintExplanationPanel != null)
            hintExplanationPanel.SetActive(true);

        string hintText = activeQuestions[idx].hint;
        if (string.IsNullOrWhiteSpace(hintText))
            hintText = "Read the definition carefully and match it to the correct geometry term.";

        if (hintExplanationText != null)
            hintExplanationText.text = hintText;
    }

    public void HideHintExplanation()
    {
        if (hintExplanationPanel != null)
            hintExplanationPanel.SetActive(false);
    }
    // ────────────────────────────────────────────────────────────────────────

    void BuildWordSearchGrid()
    {
        if (gridContainer == null || btnLetterPrefab == null)
        {
            Debug.LogError("GeoWordPlayManager fields missing!");
            return;
        }

        foreach (Transform child in gridContainer) Destroy(child.gameObject);

        bool allPlaced = false;
        int rebuildAttempts = 0;
        const int maxRebuildAttempts = 20;

        while (!allPlaced && rebuildAttempts < maxRebuildAttempts)
        {
            rebuildAttempts++;
            allPlaced = true;

            for (int x = 0; x < gridWidth; x++)
                for (int y = 0; y < gridHeight; y++)
                    gridMatrix[x, y] = ' ';

            foreach (var q in activeQuestions)
            {
                string word = q.answer.ToLower().Trim();
                if (word.Length > gridWidth) word = word.Substring(0, gridWidth);

                if (!TryPlaceWord(word))
                {
                    allPlaced = false;
                    break;
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

                GeoGridSwipe swipeLetter = setupButton.GetComponent<GeoGridSwipe>();
                if (swipeLetter == null) swipeLetter = setupButton.AddComponent<GeoGridSwipe>();
                swipeLetter.Setup(x, y, letter.ToString().ToLower().Trim(), this);

                Button baseBtn = setupButton.GetComponent<Button>();
                if (baseBtn != null) baseBtn.onClick.RemoveAllListeners();
            }
        }
    }

    private bool TryPlaceWord(string word)
    {
        const int placementAttempts = 200;

        for (int attempt = 0; attempt < placementAttempts; attempt++)
        {
            int dir = Random.Range(0, 2);

            if (dir == 0 && word.Length <= gridWidth)
            {
                int startX = Random.Range(0, gridWidth - word.Length + 1);
                int startY = Random.Range(0, gridHeight);

                if (CanPlaceWord(word, startX, startY, true))
                {
                    for (int i = 0; i < word.Length; i++)
                        gridMatrix[startX + i, startY] = word[i];
                    return true;
                }
            }
            else if (dir == 1 && word.Length <= gridHeight)
            {
                int startX = Random.Range(0, gridWidth);
                int startY = Random.Range(0, gridHeight - word.Length + 1);

                if (CanPlaceWord(word, startX, startY, false))
                {
                    for (int i = 0; i < word.Length; i++)
                        gridMatrix[startX, startY + i] = word[i];
                    return true;
                }
            }
        }
        return false;
    }

    private bool CanPlaceWord(string word, int startX, int startY, bool horizontal)
    {
        for (int i = 0; i < word.Length; i++)
        {
            char existing = horizontal
                ? gridMatrix[startX + i, startY]
                : gridMatrix[startX, startY + i];

            if (existing != ' ' && existing != word[i])
                return false;
        }
        return true;
    }

    public bool IsSwipingActive() => isSwiping;

    public void OnSwipeStart(GeoGridSwipe cell)
    {
        if (isProcessingAnswer || cell == null) return;

        HideFeedback();
        ClearSwipeHighlights();
        selectedPath.Clear();
        isSwiping = true;
        AddCellToSwipePath(cell);
    }

    public void OnSwipeDragEnter(GeoGridSwipe cell)
    {
        if (!isSwiping || cell == null || isProcessingAnswer) return;

        if (selectedPath.Count == 0)
        {
            AddCellToSwipePath(cell);
            return;
        }

        if (selectedPath.Count >= 2 && selectedPath[selectedPath.Count - 2] == cell)
        {
            GeoGridSwipe last = selectedPath[selectedPath.Count - 1];
            last.ResetColor();
            selectedPath.RemoveAt(selectedPath.Count - 1);
            RebuildCurrentInputFromPath();
            return;
        }

        if (selectedPath.Contains(cell)) return;

        if (IsAdjacent(selectedPath[selectedPath.Count - 1], cell))
            AddCellToSwipePath(cell);
    }

    public void OnSwipeEnd()
    {
        if (!isSwiping) return;
        isSwiping = false;
        RebuildCurrentInputFromPath();
    }

    private bool IsAdjacent(GeoGridSwipe a, GeoGridSwipe b)
    {
        int deltaX = Mathf.Abs(a.gridX - b.gridX);
        int deltaY = Mathf.Abs(a.gridY - b.gridY);
        return deltaX <= 1 && deltaY <= 1;
    }

    private void AddCellToSwipePath(GeoGridSwipe cell)
    {
        selectedPath.Add(cell);
        cell.SetHighlight(true, colorSwipedBtn);
        StartCoroutine(AnimateButtonPunch(cell.transform));
        RebuildCurrentInputFromPath();
    }

    private void RebuildCurrentInputFromPath()
    {
        playerCurrentInput = "";
        foreach (var cell in selectedPath)
            if (cell != null) playerCurrentInput += cell.letter.ToLower().Trim();

        if (txtCurrentInput != null)
            txtCurrentInput.text = playerCurrentInput.ToUpper();
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
        isSwiping = false;
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

        if (currentIndex != -1)
        {
            string targetAnswer = activeQuestions[currentIndex].answer.Trim().ToLower();
            if (targetAnswer == cleanedInput) isCorrect = true;
        }

        if (GameplayTelemetry.Instance != null)
        {
            GameplayTelemetry.Instance.LogAttemptNumber(maxAttempts - attemptsLeft + 1);
            GameplayTelemetry.Instance.LogAttempt(isCorrect, timeSpent, hintUsedThisPuzzle);
        }

        if (GameManager.Instance != null && currentIndex != -1)
        {
            string currentPrompt = activeQuestions[currentIndex].prompt;
            GameManager.Instance.TrackQuestionPerformance(currentPrompt, 1, isCorrect, timeSpent);
        }

        if (isCorrect)
        {
            answerSolvedStatus[currentIndex] = true;
            wordsSolvedCount++;

            DisplayFeedback($"CORRECT! Solved: {activeQuestions[currentIndex].answer.ToUpper()}", true);

            StartFlashInput(colorCorrectInput, 0.25f, () =>
            {
                UpdateActiveQuestionPrompt();
                ClearCurrentInput();
                attemptsLeft = maxAttempts;
                hintUsedThisPuzzle = false;

                if (wordsSolvedCount >= 3)
                {
                    isCleared = true;

                    if (txtQuestion != null)
                        txtQuestion.text = "<color=#00F5A0>ALL 3 GEOMETRY PUZZLES SOLVED!</color>";

                    if (ShopManagers.Instance != null)
                        ShopManagers.Instance.AddStars(1);

                    if (RankManager.Instance != null)
                        RankManager.Instance.ProcessPuzzleSuccess(1.0f);

                    if (useMovingWall && movingWall != null)
                        StartCoroutine(LiftMovingWallRoutine());

                    TriggerStarRewardSequence();
                }
                else
                {
                    questionStartTime = Time.time;
                    isProcessingAnswer = false;
                    if (GameplayTelemetry.Instance != null)
                        GameplayTelemetry.Instance.LogAttemptNumber(1);

                    CheckAndUnlockHint();
                }
            });
        }
        else
        {
            attemptsLeft--;

            DisplayFeedback("INCORRECT WORD ALIGNMENT. TRY AGAIN!", false);

            VibrateAttemptsText();
            StartFlashInput(colorWrongInput, 0.4f, () =>
            {
                if (attemptsLeft <= 0)
                {
                    if (txtAttempts != null) txtAttempts.text = "ATTEMPTS: 0";
                    if (txtQuestion != null)
                        txtQuestion.text = "<color=#FF6B6B>PUZZLE LOCKED. CLOSING AUTOMATICALLY.</color>";
                    StartCoroutine(AutoCloseAfterDelay());
                }
                else
                {
                    if (txtAttempts != null) txtAttempts.text = $"ATTEMPTS: {attemptsLeft}";
                    ClearCurrentInput();
                    questionStartTime = Time.time;
                    isProcessingAnswer = false;
                    if (GameplayTelemetry.Instance != null)
                        GameplayTelemetry.Instance.LogAttemptNumber(maxAttempts - attemptsLeft + 1);
                }
            });
        }
    }

    private IEnumerator LiftMovingWallRoutine()
    {
        Vector3 startPos = movingWall.position;
        Vector3 targetPos = startPos + Vector3.up * targetRiseHeight;

        while (Vector3.Distance(movingWall.position, targetPos) > 0.01f)
        {
            movingWall.position = Vector3.MoveTowards(movingWall.position, targetPos, liftSpeed * Time.deltaTime);
            yield return null;
        }
        movingWall.position = targetPos;
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
            if (targetStarSlot != null)
                starRewardAnimator.PlayStarRewardSequence(targetStarSlot, () => StartCoroutine(AutoCloseAfterDelay()));
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

    public void ClosePuzzleManually()
    {
        ClearCurrentInput();
        isProcessingAnswer = false;

        HideHintExplanation();
        DisableHintButton();

        SetPlayerMovementState(true);
        if (playerKitCanvas != null) playerKitCanvas.SetActive(true);

        gameObject.SetActive(false);
    }

    private void SetPlayerMovementState(bool state)
    {
        if (playerMovementScript != null) playerMovementScript.enabled = state;
    }
}
