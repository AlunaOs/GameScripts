using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class MenuSlideshow : MonoBehaviour
{
    [Header("UI Image Reference")]
    [SerializeField] private Image slideshowImage;

    [Header("Slideshow Settings")]
    [Tooltip("Add as many images as you want here in the Inspector.")]
    [SerializeField] private Sprite[] slides;
    [SerializeField] private float slideDuration = 4f; // How long each slide stays visible
    [SerializeField] private float fadeDuration = 1.2f;  // How fast slides crossfade

    [Header("Movement / Drift Settings")]
    [SerializeField] private bool enableDrift = true;
    [SerializeField] private float driftSpeed = 0.25f;
    [SerializeField] private float driftDistance = 40f;

    [Header("Fade Tint Settings")]
    [Tooltip("HEX #0C1A1B color tint during transition.")]
    [SerializeField] private Color fadeColor = new Color(0.047f, 0.102f, 0.106f, 1f); // #0C1A1B in Normalized RGBA
    [Range(0f, 1f)]
    [Tooltip("Control minimum transparency during fade out (0 = fully transparent, 1 = fully opaque solid color).")]
    [SerializeField] private float fadeMinAlpha = 0.2f;

    private RectTransform bgRectTransform;
    private Vector2 bgStartPos;
    private int currentSlideIndex = 0;
    private Coroutine slideshowCoroutine;

    private void Awake()
    {
        if (slideshowImage == null)
        {
            slideshowImage = GetComponent<Image>();
        }

        if (slideshowImage != null)
        {
            bgRectTransform = slideshowImage.GetComponent<RectTransform>();
            bgStartPos = bgRectTransform.anchoredPosition;
        }
    }

    private void OnEnable()
    {
        if (slideshowImage != null && slides != null && slides.Length > 0)
        {
            slideshowImage.sprite = slides[0];
            slideshowImage.color = Color.white;

            if (slides.Length > 1)
            {
                if (slideshowCoroutine != null) StopCoroutine(slideshowCoroutine);
                slideshowCoroutine = StartCoroutine(SlideshowLoop());
            }
        }
    }

    private void Update()
    {
        // Smooth side-to-side drifting animation
        if (enableDrift && bgRectTransform != null && driftSpeed > 0f)
        {
            float t = (Mathf.Sin(Time.time * driftSpeed) + 1f) * 0.5f;
            float smoothX = Mathf.SmoothStep(0f, driftDistance, t);
            bgRectTransform.anchoredPosition = bgStartPos + new Vector2(smoothX, 0f);
        }
    }

    private IEnumerator SlideshowLoop()
    {
        while (true)
        {
            yield return new WaitForSecondsRealtime(slideDuration);

            if (slides == null || slides.Length <= 1 || slideshowImage == null) yield break;

            int nextIndex = (currentSlideIndex + 1) % slides.Length;
            if (slides[nextIndex] == null) continue;

            // FADE OUT: Lerp color to #0C1A1B with customizable alpha transparency
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = elapsed / fadeDuration;
                float smoothT = Mathf.SmoothStep(0f, 1f, progress);

                // Lerp RGB towards tint color, and Alpha towards fadeMinAlpha
                Color targetColor = Color.Lerp(Color.white, fadeColor, smoothT);
                targetColor.a = Mathf.SmoothStep(1f, fadeMinAlpha, progress);

                slideshowImage.color = targetColor;
                yield return null;
            }

            // SWAP SPRITE AT LOWEST TRANSPARENCY
            currentSlideIndex = nextIndex;
            slideshowImage.sprite = slides[currentSlideIndex];

            // FADE IN: Lerp back to pure white / full opacity
            elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = elapsed / fadeDuration;
                float smoothT = Mathf.SmoothStep(0f, 1f, progress);

                Color targetColor = Color.Lerp(fadeColor, Color.white, smoothT);
                targetColor.a = Mathf.SmoothStep(fadeMinAlpha, 1f, progress);

                slideshowImage.color = targetColor;
                yield return null;
            }

            slideshowImage.color = Color.white;
        }
    }
}