using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class CongruencePuzzleManager : MonoBehaviour
{
    [Header("Core References")]
    [SerializeField] private CongruencePuzzleUI puzzleUI;
    [SerializeField] private CongruencePuzzleJSONLoader jsonLoader;
    [SerializeField] private GameObject puzzleRootObject;

    [Header("2D Dynamic UI Triangle (Single)")]
    [SerializeField] private DynamicTriangleMesh uiTriangleA;

    [Header("Player Control Safety Unfreeze")]
    public MonoBehaviour playerMovementScript;

    [Header("Player Kit Systems To Disable")]
    [SerializeField] private MonoBehaviour[] playerControlScripts;
    [SerializeField] private GameObject playerHUD;

    [Header("Star Reward Controller")]
    public StarRewardAnimation starRewardAnimator;
    public RectTransform targetStarSlot;

    [Header("Connected Moving Block Settings")]
    [SerializeField] private Transform movingBlock;
    [SerializeField] private float targetRiseHeight = 5.0f;
    [SerializeField] private float liftSpeed = 5.0f;

    // ── HINT PANEL (separate canvas — reads only the 'hintText' field) ──────
    [Header("Hint Panel (separate canvas — reads the question's hintText)")]
    public HintScrollUI hintScrollUI;
    public GameObject hintExplanationPanel;
    public TMP_Text hintExplanationText;
    public Button hintGotItButton;

    private const int MAX_ATTEMPTS = 3;
    private const int REQUIRED_QUESTIONS = 3;

    private List<CongruenceQuestionData> currentQuestions;
    private int currentQuestionIndex = 0;
    private int currentAttempts = 3;

    private bool isPuzzleActive = false;
    private bool isProcessingAnswer = false;
    private bool isClosing = false;
    private bool isCompleted = false;
    private bool hintUsedThisPuzzle = false;

    public bool IsPuzzleActive => isPuzzleActive;
    public bool IsCompleted => isCompleted;
    private float questionStartTime;

    private Vector3 blockStartPosition;

    private void Awake()
    {
        isCompleted = false;

        if (movingBlock != null)
            blockStartPosition = movingBlock.localPosition;
    }

    private void Start()
    {
        SubscribeUIEvents();
        if (hintGotItButton != null) hintGotItButton.onClick.AddListener(HideHintExplanation);
        HideHintExplanation();
        DisableHintButton();
    }

    private void OnDestroy()
    {
        UnsubscribeUIEvents();
    }

    private void SubscribeUIEvents()
    {
        if (puzzleUI != null)
        {
            puzzleUI.OnAnswerSelected -= HandleAnswerSubmitted;
            puzzleUI.OnCloseRequested -= ClosePuzzleManually;

            puzzleUI.OnAnswerSelected += HandleAnswerSubmitted;
            puzzleUI.OnCloseRequested += ClosePuzzleManually;
        }
    }

    private void UnsubscribeUIEvents()
    {
        if (puzzleUI != null)
        {
            puzzleUI.OnAnswerSelected -= HandleAnswerSubmitted;
            puzzleUI.OnCloseRequested -= ClosePuzzleManually;
        }
    }

    public void OpenPuzzleSession()
    {
        if (isCompleted || isClosing) return;

        currentQuestionIndex = 0;
        isPuzzleActive = true;
        isProcessingAnswer = false;
        isClosing = false;
        hintUsedThisPuzzle = false;

        if (jsonLoader != null)
            currentQuestions = jsonLoader.LoadQuestions(REQUIRED_QUESTIONS);

        if (GameplayTelemetry.Instance != null)
        {
            string puzzleTopic = (GameManager.Instance != null && !string.IsNullOrEmpty(GameManager.Instance.currentCategory))
                ? GameManager.Instance.currentCategory
                : "Geometric Congruence";
            int currentTierLevel = (GameManager.Instance != null) ? GameManager.Instance.currentLevel : 1;
            GameplayTelemetry.Instance.BeginPuzzle(puzzleTopic, currentTierLevel);
            GameplayTelemetry.Instance.LogQuestionTier(currentTierLevel);
            GameplayTelemetry.Instance.LogAttemptNumber(1);
        }

        SetPlayerKitState(false);

        if (puzzleRootObject != null) puzzleRootObject.SetActive(true);

        LoadCurrentQuestion();
        CheckAndUnlockHint();
    }

    private void LoadCurrentQuestion()
    {
        if (currentQuestions == null || currentQuestions.Count == 0) return;

        if (currentQuestionIndex < currentQuestions.Count)
        {
            currentAttempts = MAX_ATTEMPTS;
            questionStartTime = Time.time;
            hintUsedThisPuzzle = false;

            CongruenceQuestionData qData = currentQuestions[currentQuestionIndex];

            if (puzzleUI != null)
            {
                puzzleUI.RenderQuestion(qData, currentQuestionIndex, REQUIRED_QUESTIONS);
                puzzleUI.UpdateAttempts(currentAttempts, MAX_ATTEMPTS);
                puzzleUI.SetAnswerButtonsInteractable(true);
            }

            if (uiTriangleA != null && qData.triangleA_Vertices != null && qData.triangleA_Vertices.Length >= 3)
            {
                uiTriangleA.UpdateTriangleVertices(
                    qData.triangleA_Vertices[0].ToVector3(),
                    qData.triangleA_Vertices[1].ToVector3(),
                    qData.triangleA_Vertices[2].ToVector3());

                UIDragHandler dragA = uiTriangleA.GetComponent<UIDragHandler>();
                if (dragA != null) dragA.ResetPosition();
            }

            if (GameplayTelemetry.Instance != null)
            {
                int currentTierLevel = (GameManager.Instance != null) ? GameManager.Instance.currentLevel : 1;
                GameplayTelemetry.Instance.LogQuestionTier(currentTierLevel);
                GameplayTelemetry.Instance.LogAttemptNumber(MAX_ATTEMPTS - currentAttempts + 1);
            }

            CheckAndUnlockHint();
        }
    }

    private void HandleAnswerSubmitted(int answerIndex)
    {
        if (!isPuzzleActive || isProcessingAnswer || isClosing || currentAttempts <= 0) return;

        if (uiTriangleA != null)
        {
            UIDragHandler dragHandler = uiTriangleA.GetComponent<UIDragHandler>();
            if (dragHandler == null || dragHandler.CurrentTarget == null || dragHandler.CurrentTarget.optionIndex != answerIndex)
                return;
        }

        StartCoroutine(ProcessAnswerRoutine(answerIndex));
    }

    private IEnumerator ProcessAnswerRoutine(int answerIndex)
    {
        isProcessingAnswer = true;
        if (puzzleUI != null) puzzleUI.SetAnswerButtonsInteractable(false);

        float timeSpent = Time.time - questionStartTime;
        CongruenceQuestionData question = currentQuestions[currentQuestionIndex];
        bool isCorrect = (answerIndex == question.correctAnswerIndex);

        if (GameplayTelemetry.Instance != null)
        {
            GameplayTelemetry.Instance.LogAttemptNumber(MAX_ATTEMPTS - currentAttempts + 1);
            GameplayTelemetry.Instance.LogAttempt(isCorrect, timeSpent, hintUsedThisPuzzle);
        }

        if (GameManager.Instance != null)
        {
            // Single call — TrackQuestionPerformance already runs EvaluatePerformance internally.
            GameManager.Instance.TrackQuestionPerformance(question.questionText, 1, isCorrect, timeSpent);
        }

        if (puzzleUI != null)
            yield return puzzleUI.DisplayFeedbackRoutine(isCorrect);

        if (isCorrect)
        {
            currentQuestionIndex++;
            if (currentQuestionIndex >= REQUIRED_QUESTIONS)
            {
                yield return CompletePuzzleSequenceRoutine();
                yield break;
            }
            else
            {
                LoadCurrentQuestion();
            }
        }
        else
        {
            currentAttempts = Mathf.Clamp(currentAttempts - 1, 0, MAX_ATTEMPTS);
            if (puzzleUI != null) puzzleUI.UpdateAttempts(currentAttempts, MAX_ATTEMPTS);

            if (GameplayTelemetry.Instance != null)
                GameplayTelemetry.Instance.LogAttemptNumber(MAX_ATTEMPTS - currentAttempts + 1);

            if (uiTriangleA != null)
            {
                UIDragHandler dragA = uiTriangleA.GetComponent<UIDragHandler>();
                if (dragA != null) dragA.ResetPosition();
            }

            questionStartTime = Time.time;

            if (currentAttempts <= 0)
            {
                if (puzzleUI != null) puzzleUI.SetAnswerButtonsInteractable(false);
                ClosePuzzleSession();
                yield break;
            }
        }

        isProcessingAnswer = false;
        if (puzzleUI != null) puzzleUI.SetAnswerButtonsInteractable(true);
    }

    private IEnumerator CompletePuzzleSequenceRoutine()
    {
        isCompleted = true;
        if (puzzleUI != null) puzzleUI.SetAnswerButtonsInteractable(false);

        if (ShopManagers.Instance != null)
            ShopManagers.Instance.AddStars(1);

        if (RankManager.Instance != null)
            RankManager.Instance.ProcessPuzzleSuccess(1.0f);

        bool isStarAnimationDone = false;
        TriggerStarRewardSequence(() => isStarAnimationDone = true);

        float timeoutTimer = 0f;
        while (!isStarAnimationDone && timeoutTimer < 3.0f)
        {
            timeoutTimer += Time.deltaTime;
            yield return null;
        }

        yield return new WaitForSeconds(0.5f);

        if (movingBlock != null)
        {
            Vector3 targetPosition = blockStartPosition + new Vector3(0, targetRiseHeight, 0);

            while (Vector3.Distance(movingBlock.localPosition, targetPosition) > 0.01f)
            {
                movingBlock.localPosition = Vector3.MoveTowards(
                    movingBlock.localPosition, targetPosition, liftSpeed * Time.deltaTime);
                yield return null;
            }
            movingBlock.localPosition = targetPosition;
        }

        ClosePuzzleSession();
    }

    private void TriggerStarRewardSequence(System.Action onComplete)
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

    public void ClosePuzzleManually()
    {
        if (isClosing) return;
        ClosePuzzleSession();
    }

    private void ClosePuzzleSession()
    {
        isClosing = true;
        isPuzzleActive = false;

        HideHintExplanation();
        DisableHintButton();

        if (puzzleUI != null) puzzleUI.SetAnswerButtonsInteractable(false);
        if (puzzleRootObject != null) puzzleRootObject.SetActive(false);

        SetPlayerKitState(true);
        isClosing = false;
    }

    private void SetPlayerKitState(bool active)
    {
        if (playerHUD != null) playerHUD.SetActive(active);

        if (playerMovementScript != null)
            playerMovementScript.enabled = active;

        if (playerControlScripts != null)
        {
            foreach (var script in playerControlScripts)
                if (script != null) script.enabled = active;
        }
    }

    // ── HINT SYSTEM ─────────────────────────────────────────────────────────
    private void CheckAndUnlockHint()
    {
        // Legacy path — keeps the existing puzzleUI hint button alive.
        if (ShopManagers.Instance != null && ShopManagers.Instance.hasHintScroll && !isCompleted)
        {
            if (puzzleUI != null)
            {
                puzzleUI.SetupHintButton(() =>
                {
                    if (currentQuestions != null && currentQuestionIndex < currentQuestions.Count)
                        puzzleUI.HighlightCorrectAnswer(currentQuestions[currentQuestionIndex].correctAnswerIndex);
                });
            }

            EnableHintButton();
        }
        else
        {
            DisableHintButton();
        }
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
        if (currentQuestions == null || currentQuestionIndex >= currentQuestions.Count) return;

        hintUsedThisPuzzle = true;

        if (hintExplanationPanel != null)
            hintExplanationPanel.SetActive(true);

        string hintText = currentQuestions[currentQuestionIndex].hintText;
        if (string.IsNullOrWhiteSpace(hintText))
            hintText = "Look at the given triangle sides and angles, and match them to the correct congruence rule.";

        if (hintExplanationText != null)
            hintExplanationText.text = hintText;
    }

    public void HideHintExplanation()
    {
        if (hintExplanationPanel != null)
            hintExplanationPanel.SetActive(false);
    }
    // ────────────────────────────────────────────────────────────────────────
}
