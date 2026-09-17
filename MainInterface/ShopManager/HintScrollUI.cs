using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class HintScrollUI : MonoBehaviour
{
    [Header("UI Component References")]
    [Tooltip("The actual clickable Button UI for the hint scroll")]
    public Button hintButton;

    [Tooltip("Image display in the center of the screen that pops in when the hint is used")]
    public Image centerDisplayImage;

    [Header("Shine Sweep (optional)")]
    [Tooltip("Optional: a thin, angled, semi-transparent white bar (child of centerDisplayImage, " +
             "clipped by a RectMask2D on its parent) that sweeps across the image for a shine/shimmer " +
             "effect. Leave unassigned to skip this entirely — everything else still works without it.")]
    public RectTransform shineStreak;
    [Tooltip("Local X position the streak starts at (off-screen to the left of the image).")]
    public float shineStartX = -400f;
    [Tooltip("Local X position the streak ends at (off-screen to the right of the image).")]
    public float shineEndX = 400f;
    [Tooltip("How long the shine sweep takes to cross the image.")]
    public float shineSweepDuration = 0.5f;
    [Tooltip("Delay after the pop-in finishes before the shine starts sweeping.")]
    public float shineStartDelay = 0.05f;

    [Header("Animation Settings")]
    [Tooltip("How long the pop-in image stays on screen before disappearing")]
    public float imageDisplayDuration = 1.5f;

    [Tooltip("Duration of the pop-in scale animation")]
    public float popAnimationSpeed = 0.35f;

    [Tooltip("How pronounced the pop-in overshoot/bounce is. 0 = no bounce (straight ease), " +
             "higher = bouncier. ~1.7 is a nice punchy default.")]
    public float popOvershoot = 1.7f;

    private System.Action onHintUsedCallback;
    private bool isHintUnlocked = false;

    void Awake()
    {
        // Keep the center display image hidden initially
        if (centerDisplayImage != null)
        {
            centerDisplayImage.gameObject.SetActive(false);
        }

        // Only deactivate the button if it hasn't been unlocked before Awake ran
        if (hintButton != null && !isHintUnlocked)
        {
            hintButton.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Call this from ShopManagers or ScalePuzzleManager to show and enable the hint scroll button.
    /// </summary>
    /// <param name="onUsedCallback">The method in your puzzle manager that highlights the correct answer.</param>
    public void EnableHint(System.Action onUsedCallback)
    {
        // Reactivate this whole parent GameObject first, in case it (or this
        // script's own GameObject) started disabled in the hierarchy.
        gameObject.SetActive(true);

        isHintUnlocked = true;
        onHintUsedCallback = onUsedCallback;

        if (hintButton != null)
        {
            hintButton.onClick.RemoveAllListeners();
            hintButton.onClick.AddListener(OnButtonClicked);
            hintButton.gameObject.SetActive(true);

            Debug.Log("[HintScrollUI] Hint Scroll button successfully enabled and displayed!", gameObject);
        }
        else
        {
            Debug.LogError("[HintScrollUI] Cannot enable hint because 'hintButton' is missing/unassigned in the Inspector!", gameObject);
        }
    }

    /// <summary>
    /// Triggered when the user taps/clicks the Hint Scroll button.
    /// </summary>
    private void OnButtonClicked()
    {
        // 1. Instantly hide the hint button so it cannot be tapped twice
        if (hintButton != null)
        {
            hintButton.gameObject.SetActive(false);
        }

        // 2. Play the pop-in animation and trigger answer highlighting
        StartCoroutine(PlayAnimationAndCallbackRoutine());
    }

    private IEnumerator PlayAnimationAndCallbackRoutine()
    {
        if (centerDisplayImage != null)
        {
            centerDisplayImage.gameObject.SetActive(true);
            centerDisplayImage.transform.localScale = Vector3.zero;

            Color c = centerDisplayImage.color;
            c.a = 0f;
            centerDisplayImage.color = c;

            if (shineStreak != null)
            {
                var pos = shineStreak.anchoredPosition;
                pos.x = shineStartX;
                shineStreak.anchoredPosition = pos;
            }

            float elapsed = 0f;

            // Bouncy "pop" scale-up (overshoots past 1x, then settles) instead
            // of a flat linear lerp, plus a quick alpha fade-in.
            while (elapsed < popAnimationSpeed)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / popAnimationSpeed);

                float scale = BackEaseOut(t, popOvershoot);
                centerDisplayImage.transform.localScale = new Vector3(scale, scale, 1f);

                // Fade finishes a bit earlier than the scale so the image is
                // fully opaque while it's still settling into place.
                Color fadeColor = centerDisplayImage.color;
                fadeColor.a = Mathf.Clamp01(t / 0.6f);
                centerDisplayImage.color = fadeColor;

                yield return null;
            }

            centerDisplayImage.transform.localScale = Vector3.one;
            Color finalColor = centerDisplayImage.color;
            finalColor.a = 1f;
            centerDisplayImage.color = finalColor;

            // Optional shine sweep, once the pop-in has settled
            if (shineStreak != null)
            {
                yield return new WaitForSeconds(shineStartDelay);
                yield return StartCoroutine(SweepShineRoutine());
            }

            // Hold image on screen
            yield return new WaitForSeconds(imageDisplayDuration);

            centerDisplayImage.gameObject.SetActive(false);
        }

        // Step B: Call back to ScalePuzzleManager to highlight/flash the correct choice button green
        onHintUsedCallback?.Invoke();
    }

    private IEnumerator SweepShineRoutine()
    {
        float elapsed = 0f;
        var pos = shineStreak.anchoredPosition;

        while (elapsed < shineSweepDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / shineSweepDuration);
            pos.x = Mathf.Lerp(shineStartX, shineEndX, t);
            shineStreak.anchoredPosition = pos;
            yield return null;
        }

        pos.x = shineEndX;
        shineStreak.anchoredPosition = pos;
    }

    /// <summary>"Back ease out" — overshoots past the target then settles back, giving a springy pop feel.</summary>
    private static float BackEaseOut(float t, float overshoot)
    {
        t -= 1f;
        return t * t * ((overshoot + 1f) * t + overshoot) + 1f;
    }
}