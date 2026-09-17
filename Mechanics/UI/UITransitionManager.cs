using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class UITransitionManager : MonoBehaviour
{
    [Header("Question Panel (Scale Pop-In)")]
    public RectTransform questionPanel;
    public Button blackboardButton;
    public float scaleDuration = 0.4f;

    [Header("Puzzle Panel (Slide Up Animation)")]
    public RectTransform puzzlePanel;
    public Button showPuzzleButton;
    public float slideDuration = 0.5f;
    public float hiddenYPosition = -1080f; // Y position off-screen at bottom
    public float visibleYPosition = 0f;    // Y position when fully visible

    private Coroutine questionCoroutine;
    private Coroutine puzzleCoroutine;

    void Start()
    {
        // Set initial hidden states
        if (questionPanel != null)
        {
            questionPanel.localScale = Vector3.zero;
            questionPanel.gameObject.SetActive(false);
        }

        if (puzzlePanel != null)
        {
            puzzlePanel.anchoredPosition = new Vector2(puzzlePanel.anchoredPosition.x, hiddenYPosition);
            puzzlePanel.gameObject.SetActive(false);
        }

        // Attach button click events
        if (blackboardButton != null) blackboardButton.onClick.AddListener(ShowQuestion);
        if (showPuzzleButton != null) showPuzzleButton.onClick.AddListener(ShowPuzzle);
    }

    // --- QUESTION POP-IN TRANSITION ---
    public void ShowQuestion()
    {
        if (questionPanel == null) return;

        questionPanel.gameObject.SetActive(true);
        if (questionCoroutine != null) StopCoroutine(questionCoroutine);
        questionCoroutine = StartCoroutine(AnimateScale(questionPanel, Vector3.zero, Vector3.one, scaleDuration));
    }

    IEnumerator AnimateScale(RectTransform panel, Vector3 startScale, Vector3 endScale, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            
            // Smooth ease-out effect
            t = Mathf.Sin(t * Mathf.PI * 0.5f);

            panel.localScale = Vector3.Lerp(startScale, endScale, t);
            yield return null;
        }
        panel.localScale = endScale;
    }

    // --- PUZZLE SLIDE-UP TRANSITION ---
    public void ShowPuzzle()
    {
        if (puzzlePanel == null) return;

        puzzlePanel.gameObject.SetActive(true);
        if (puzzleCoroutine != null) StopCoroutine(puzzleCoroutine);

        Vector2 startPos = new Vector2(puzzlePanel.anchoredPosition.x, hiddenYPosition);
        Vector2 targetPos = new Vector2(puzzlePanel.anchoredPosition.x, visibleYPosition);

        puzzleCoroutine = StartCoroutine(AnimateSlide(puzzlePanel, startPos, targetPos, slideDuration));
    }

    IEnumerator AnimateSlide(RectTransform panel, Vector2 start, Vector2 target, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // Ease-out curve for paper slide effect
            t = 1f - Mathf.Pow(1f - t, 3f);

            panel.anchoredPosition = Vector2.Lerp(start, target, t);
            yield return null;
        }
        panel.anchoredPosition = target;
    }
}