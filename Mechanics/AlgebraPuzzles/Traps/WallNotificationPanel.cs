using UnityEngine;
using System.Collections;
using TMPro;

public class WallNotificationPanel : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI messageText;
    public CanvasGroup canvasGroup;
    public RectTransform panelRectTransform;

    [Header("Animation Positions (Y Coordinates)")]
    public float bottomY = -300f;
    public float middleY = 0f;
    public float topY = 300f;

    [Header("Animation Durations")]
    public float enterDuration = 0.4f;
    public float pauseDuration = 1.0f;
    public float exitDuration = 0.6f;

    void Awake()
    {
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (panelRectTransform == null) panelRectTransform = GetComponent<RectTransform>();

        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
    }

    /// <summary>
    /// Coroutine animation logic executed by caller script
    /// </summary>
    public IEnumerator AnimateNotificationRoutine(string message)
    {
        // Make sure gameobject is active
        gameObject.SetActive(true);

        if (messageText != null) messageText.text = message;

        // Reset positions and alpha
        Vector2 pos = panelRectTransform.anchoredPosition;
        pos.y = bottomY;
        panelRectTransform.anchoredPosition = pos;
        canvasGroup.alpha = 0f;

        // Phase 1: Slide from Bottom to Middle
        float elapsed = 0f;
        while (elapsed < enterDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / enterDuration;
            float currentY = Mathf.Lerp(bottomY, middleY, Mathf.SmoothStep(0f, 1f, t));
            panelRectTransform.anchoredPosition = new Vector2(pos.x, currentY);
            canvasGroup.alpha = Mathf.Lerp(0f, 1f, t);
            yield return null;
        }

        panelRectTransform.anchoredPosition = new Vector2(pos.x, middleY);
        canvasGroup.alpha = 1f;

        // Phase 2: Hold
        yield return new WaitForSeconds(pauseDuration);

        // Phase 3: Slide to Top & Fade Out
        elapsed = 0f;
        while (elapsed < exitDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / exitDuration;
            float currentY = Mathf.Lerp(middleY, topY, Mathf.SmoothStep(0f, 1f, t));
            panelRectTransform.anchoredPosition = new Vector2(pos.x, currentY);
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, t);
            yield return null;
        }

        canvasGroup.alpha = 0f;
    }
}