using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages geometric congruence interactive matching puzzles, inheriting from BasePuzzleManager
/// to streamline UI event subscriptions, triangle mesh placement, and star reward callbacks.
/// </summary>
public class CongruencePuzzleManager : BasePuzzleManager
{
    [Header("Core References")]
    [SerializeField] private CongruencePuzzleUI puzzleUI;
    [SerializeField] private CongruencePuzzleJSONLoader jsonLoader;
    [SerializeField] private GameObject puzzleRootObject;

    [Header("2D Dynamic UI Triangle (Single)")]
    [SerializeField] private DynamicTriangleMesh uiTriangleA;

    [Header("Player Kit Systems To Disable")]
    [SerializeField] private MonoBehaviour[] playerControlScripts;
    [SerializeField] private GameObject playerHUD;

    [Header("Connected Moving Block Settings")]
    [SerializeField] private Transform movingBlock;
    [SerializeField] private float targetRiseHeight = 5.0f;
    [SerializeField] private float liftSpeed = 5.0f;

    private const int MAX_ATTEMPTS = 3;
    private const int REQUIRED_QUESTIONS = 3;

    private List<CongruenceQuestionData> currentQuestions;
    private int currentQuestionIndex = 0;
    private int currentAttempts = 3;

    private bool isClosing = false;
    private Vector3 blockStartPosition;

    protected override void Awake()
    {
        base.Awake();
        isPuzzleCompleted = false;

        if (movingBlock != null) blockStartPosition = movingBlock.localPosition;
    }

    protected override void Start()
    {
        base.Start();
        SubscribeUIEvents();
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
        if (isPuzzleCompleted || isClosing) return;

        currentQuestionIndex = 0;
        isProcessingAction = false;
        isClosing = false;

        if (jsonLoader != null) currentQuestions = jsonLoader.LoadQuestions(REQUIRED_QUESTIONS);

        string puzzleTopic = (GameManager.Instance != null && !string.IsNullOrEmpty(GameManager.Instance.currentCategory))
            ? GameManager.Instance.currentCategory
            : "Geometric Congruence";
        int currentTierLevel = (GameManager.Instance != null) ? GameManager.Instance.currentLevel : 1;
        
        LogPuzzleStart(puzzleTopic, currentTierLevel);

        if (playerHUD != null) playerHUD.SetActive(false);
        OpenPuzzle();

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
            puzzleStartTime = Time.time;

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
                    qData.triangleA_Vertices[2].ToVector3()
                );

                UIDragHandler dragA = uiTriangleA.GetComponent<UIDragHandler>();
                if (dragA != null) dragA.ResetPosition();
            }
        }
    }

    private void HandleAnswerSubmitted(int answerIndex)
    {
        if (isProcessingAction || isClosing || currentAttempts <= 0) return;

        if (uiTriangleA != null)
        {
            UIDragHandler dragHandler = uiTriangleA.GetComponent<UIDragHandler>();
            if (dragHandler == null || dragHandler.CurrentTarget == null || dragHandler.CurrentTarget.optionIndex != answerIndex)
            {
                return;
            }
        }

        StartCoroutine(ProcessAnswerRoutine(answerIndex));
    }

    private IEnumerator ProcessAnswerRoutine(int answerIndex)
    {
        isProcessingAction = true;
        if (puzzleUI != null) puzzleUI.SetAnswerButtonsInteractable(false);

        float timeSpent = Time.time - puzzleStartTime;
        CongruenceQuestionData question = currentQuestions[currentQuestionIndex];
        bool isCorrect = (answerIndex == question.correctAnswerIndex);

        LogPuzzleAttempt(isCorrect, timeSpent);
        TrackQuestionMetrics(question.questionText, 1, isCorrect, timeSpent);

        if (puzzleUI != null) yield return puzzleUI.DisplayFeedbackRoutine(isCorrect);

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

            if (uiTriangleA != null)
            {
                UIDragHandler dragA = uiTriangleA.GetComponent<UIDragHandler>();
                if (dragA != null) dragA.ResetPosition();
            }

            puzzleStartTime = Time.time;

            if (currentAttempts <= 0)
            {
                if (puzzleUI != null) puzzleUI.SetAnswerButtonsInteractable(false);
                ClosePuzzleManually();
                yield break;
            }
        }

        isProcessingAction = false;
        if (puzzleUI != null) puzzleUI.SetAnswerButtonsInteractable(true);
    }

    private IEnumerator CompletePuzzleSequenceRoutine()
    {
        isPuzzleCompleted = true;
        if (puzzleUI != null) puzzleUI.SetAnswerButtonsInteractable(false);

        if (ShopManagers.Instance != null) ShopManagers.Instance.AddStars(1);
        ProcessRankSuccess(1.0f);

        TriggerStarRewardSequence(() => {
            StartCoroutine(FinishPuzzleBlockRoutine());
        });
    }

    private IEnumerator FinishPuzzleBlockRoutine()
    {
        yield return new WaitForSeconds(0.5f);

        if (movingBlock != null)
        {
            Vector3 targetPosition = blockStartPosition + new Vector3(0, targetRiseHeight, 0);
            while (Vector3.Distance(movingBlock.localPosition, targetPosition) > 0.01f)
            {
                movingBlock.localPosition = Vector3.MoveTowards(movingBlock.localPosition, targetPosition, liftSpeed * Time.deltaTime);
                yield return null;
            }
            movingBlock.localPosition = targetPosition;
        }

        ClosePuzzleManually();
    }

    public void ClosePuzzleManually()
    {
        if (isClosing) return;
        
        isClosing = true;
        if (puzzleUI != null) puzzleUI.SetAnswerButtonsInteractable(false);
        if (puzzleRootObject != null) puzzleRootObject.SetActive(false);
        if (playerHUD != null) playerHUD.SetActive(true);

        ClosePuzzle();
        isClosing = false;
    }
}