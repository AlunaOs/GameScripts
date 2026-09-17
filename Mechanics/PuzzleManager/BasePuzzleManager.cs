using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// Abstract base manager that centralizes common puzzle subsystems including Star Rewards,
/// Telemetry logging, Hint integration, Panel sliding animations, and Visual feedback effects.
/// Designed to eliminate boilerplate code across specific puzzle implementations.
/// </summary>
public abstract class BasePuzzleManager : MonoBehaviour
{
    [Header("Base Puzzle UI Links")]
    [Tooltip("Canvas panel containing the main puzzle interface.")]
    public GameObject puzzleCanvasPanel;
    [Tooltip("On-screen joystick or player controls canvas to disable while puzzling.")]
    public GameObject playerControlsCanvas;
    [Tooltip("MonoBehaviour script controlling player movement to unfreeze/freeze.")]
    public MonoBehaviour playerMovementScript;

    [Header("Base Star Reward Controller")]
    public StarRewardAnimation starRewardAnimator;
    public RectTransform[] targetStarSlots;
    [Range(1, 3)]
    public int starToRewardNumber = 1;

    [Header("Base Hint Settings")]
    public HintScrollUI hintScrollUI;
    protected bool hintUsedThisSession = false;

    [Header("Base Visual Feedback Settings")]
    public float defaultShakeDuration = 0.35f;
    public float defaultShakeMagnitude = 8f;

    // Protected State Variables
    protected bool isPuzzleCompleted = false;
    protected bool isProcessingAction = false;
    protected float puzzleStartTime = 0f;
    protected Coroutine panelAnimCoroutine;
    protected Coroutine feedbackFlashCoroutine;
    protected Coroutine shakeCoroutine;

    protected virtual void Awake()
    {
        // Initialization common to all puzzles
    }

    protected virtual void Start()
    {
        // Setup initial states
    }

    #region Puzzle Lifecycle Methods
    
    public virtual void OpenPuzzle()
    {
        if (isPuzzleCompleted) return;

        isProcessingAction = false;
        hintUsedThisSession = false;
        puzzleStartTime = Time.time;

        if (playerControlsCanvas != null)
            playerControlsCanvas.SetActive(false);

        TogglePlayerControls(false);

        if (puzzleCanvasPanel != null)
            puzzleCanvasPanel.SetActive(true);
    }

    public virtual void ClosePuzzle()
    {
        StopAllCoroutines();
        isProcessingAction = false;

        if (puzzleCanvasPanel != null)
            puzzleCanvasPanel.SetActive(false);

        if (playerControlsCanvas != null)
            playerControlsCanvas.SetActive(true);

        TogglePlayerControls(true);
    }

    protected virtual void CompletePuzzle()
    {
        isPuzzleCompleted = true;

        if (ShopManagers.Instance != null)
        {
            ShopManagers.Instance.AddStars(1);
        }

        TriggerStarRewardSequence(() => {
            OnPuzzleRewardSequenceComplete();
        });
    }

    protected virtual void OnPuzzleRewardSequenceComplete()
    {
        ClosePuzzle();
    }

    #endregion

    #region Telemetry & Analytics Integration

    protected void LogPuzzleStart(string topicName, int difficultyLevel)
    {
        if (GameplayTelemetry.Instance != null)
        {
            GameplayTelemetry.Instance.BeginPuzzle(topicName, difficultyLevel);
        }
    }

    protected void LogPuzzleAttempt(bool isCorrect, float timeSpent)
    {
        if (GameplayTelemetry.Instance != null)
        {
            GameplayTelemetry.Instance.LogAttempt(isCorrect, timeSpent, hintUsedThisSession);
        }
    }

    protected void TrackQuestionMetrics(string prompt, int difficulty, bool isCorrect, float timeSpent)
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.TrackQuestionPerformance(prompt, difficulty, isCorrect, timeSpent);
            GameManager.Instance.EvaluatePerformance(isCorrect, timeSpent);
        }
    }

    protected void ProcessRankSuccess(float accuracy)
    {
        if (RankManager.Instance != null)
        {
            RankManager.Instance.ProcessPuzzleSuccess(accuracy);
        }
    }

    protected void ProcessRankFailure()
    {
        if (RankManager.Instance != null)
        {
            RankManager.Instance.ProcessPuzzleFailure();
        }
    }

    #endregion

    #region Hint System Management

    public virtual void CheckAndUnlockHint()
    {
        if (ShopManagers.Instance != null && ShopManagers.Instance.hasHintScroll && !isPuzzleCompleted)
        {
            EnableHintButton();
        }
        else
        {
            DisableHintButton();
        }
    }

    protected virtual void EnableHintButton()
    {
        if (hintScrollUI == null) return;

        hintScrollUI.gameObject.SetActive(true);
        hintScrollUI.EnableHint(() => {
            if (ShopManagers.Instance != null)
            {
                ShopManagers.Instance.UseHintScroll();
            }
            hintUsedThisSession = true;
            OnHintActivated();
        });
    }

    protected virtual void DisableHintButton()
    {
        if (hintScrollUI != null)
        {
            hintScrollUI.gameObject.SetActive(false);
        }
    }

    protected virtual void OnHintActivated()
    {
        // Override in derived classes for specific hint actions
    }

    #endregion

    #region Star Reward Animations

    protected void TriggerStarRewardSequence(System.Action onComplete = null)
    {
        if (starRewardAnimator != null)
        {
            int arrayIndex = Mathf.Clamp(starToRewardNumber - 1, 0, targetStarSlots.Length - 1);

            if (targetStarSlots != null && targetStarSlots.Length > arrayIndex && targetStarSlots[arrayIndex] != null)
            {
                starRewardAnimator.PlayStarRewardSequence(targetStarSlots[arrayIndex], () => {
                    onComplete?.Invoke();
                });
            }
            else
            {
                starRewardAnimator.PlayStarRewardSequence(() => {
                    onComplete?.Invoke();
                });
            }
        }
        else
        {
            onComplete?.Invoke();
        }
    }

    protected RectTransform GetNextAvailableStarSlot()
    {
        if (targetStarSlots == null || targetStarSlots.Length == 0) return null;

        foreach (RectTransform starSlot in targetStarSlots)
        {
            if (starSlot == null) continue;

            if (starSlot.childCount > 0)
            {
                if (!starSlot.GetChild(0).gameObject.activeSelf)
                {
                    return (RectTransform)starSlot.GetChild(0);
                }
            }
            else if (!starSlot.gameObject.activeSelf)
            {
                return starSlot;
            }
        }

        return targetStarSlots[targetStarSlots.Length - 1];
    }

    #endregion

    #region UI Panel Slide Animations

    protected IEnumerator SlidePanelInRoutine(RectTransform panelRect, float duration = 0.3f)
    {
        if (panelRect == null) yield break;

        float offscreenX = Screen.width;
        if (panelRect.parent != null)
        {
            offscreenX = ((RectTransform)panelRect.parent).rect.width;
        }

        Vector2 targetPos = panelRect.anchoredPosition;
        Vector2 startPos = new Vector2(offscreenX, targetPos.y);

        panelRect.anchoredPosition = startPos;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = t * t * (3f - 2f * t); // Smoothstep easing

            panelRect.anchoredPosition = Vector2.Lerp(startPos, targetPos, t);
            yield return null;
        }

        panelRect.anchoredPosition = targetPos;
    }

    protected IEnumerator SlidePanelOutRoutine(RectTransform panelRect, GameObject parentGameObject, float duration = 0.3f)
    {
        if (panelRect == null) yield break;

        float offscreenX = Screen.width;
        if (panelRect.parent != null)
        {
            offscreenX = ((RectTransform)panelRect.parent).rect.width;
        }

        Vector2 startPos = panelRect.anchoredPosition;
        Vector2 targetPos = new Vector2(offscreenX, startPos.y);

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = t * t * (3f - 2f * t);

            panelRect.anchoredPosition = Vector2.Lerp(startPos, targetPos, t);
            yield return null;
        }

        panelRect.anchoredPosition = targetPos;
        if (parentGameObject != null) parentGameObject.SetActive(false);
    }

    #endregion

    #region Reusable Visual Feedback Effects

    public IEnumerator AnimateButtonPunch(Transform btnTransform, float scaleFactor = 0.85f, float pressDuration = 0.04f, float bounceDuration = 0.08f)
    {
        if (btnTransform == null) yield break;

        Vector3 originalScale = Vector3.one;
        Vector3 pressedScale = originalScale * scaleFactor;

        float elapsed = 0f;
        while (elapsed < pressDuration)
        {
            elapsed += Time.deltaTime;
            btnTransform.localScale = Vector3.Lerp(originalScale, pressedScale, elapsed / pressDuration);
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < bounceDuration)
        {
            elapsed += Time.deltaTime;
            btnTransform.localScale = Vector3.Lerp(pressedScale, originalScale, elapsed / bounceDuration);
            yield return null;
        }

        btnTransform.localScale = originalScale;
    }

    protected IEnumerator ShakeRectTransformRoutine(RectTransform targetRect, float duration, float magnitude)
    {
        if (targetRect == null) yield break;

        Vector3 originalPos = targetRect.anchoredPosition;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float offsetX = Random.Range(-magnitude, magnitude);
            float offsetY = Random.Range(-magnitude, magnitude);

            targetRect.anchoredPosition = originalPos + new Vector3(offsetX, offsetY, 0f);
            yield return null;
        }

        targetRect.anchoredPosition = originalPos;
    }

    protected void TogglePlayerControls(bool state)
    {
        if (playerMovementScript != null)
        {
            playerMovementScript.enabled = state;
        }

        if (state)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    #endregion
}