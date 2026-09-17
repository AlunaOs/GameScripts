using System.Collections;
using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class QuestionPanelSlide : MonoBehaviour
{
    [Header("Transition Settings")]
    public float slideDuration = 0.3f;
    
    private RectTransform rectTransform;
    private Coroutine slideCoroutine;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    private void OnEnable()
    {
        // Automatically slide in from the right every time the panel is turned on
        if (slideCoroutine != null) StopCoroutine(slideCoroutine);
        slideCoroutine = StartCoroutine(SlideInFromRight());
    }

    private IEnumerator SlideInFromRight()
    {
        // Start off-screen to the right
        float offScreenX = Screen.width;
        Vector2 startPos = new Vector2(offScreenX, rectTransform.anchoredPosition.y);
        Vector2 targetPos = new Vector2(0f, rectTransform.anchoredPosition.y); // Center

        rectTransform.anchoredPosition = startPos;

        float elapsed = 0f;
        while (elapsed < slideDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / slideDuration);
            t = t * t * (3f - 2f * t); // Smooth easing curve

            rectTransform.anchoredPosition = Vector2.Lerp(startPos, targetPos, t);
            yield return null;
        }

        rectTransform.anchoredPosition = targetPos;
    }
}