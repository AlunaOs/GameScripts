using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class StarRewardAnimation : MonoBehaviour
{
    [Header("UI Star References")]
    public RectTransform animatedStar;
    public RectTransform targetGreyStarSlot; // Default slot if none is passed

    [Header("Animation Tweaks")]
    public float moveDuration = 1.0f;
    public AnimationCurve motionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    // Dynamic target overload!
    public void PlayStarRewardSequence(RectTransform specificTargetSlot, Action onComplete = null)
    {
        StartCoroutine(AnimateStarRoutine(specificTargetSlot, onComplete));
    }

    public void PlayStarRewardSequence(Action onComplete = null)
    {
        StartCoroutine(AnimateStarRoutine(targetGreyStarSlot, onComplete));
    }

    private IEnumerator AnimateStarRoutine(RectTransform targetSlot, Action onComplete)
    {
        if (animatedStar == null || targetSlot == null)
        {
            onComplete?.Invoke();
            yield break;
        }

        animatedStar.gameObject.SetActive(true);
        Vector3 startPos = animatedStar.position;
        Vector3 endPos = targetSlot.position;

        float elapsed = 0f;
        while (elapsed < moveDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / moveDuration;
            float curvedT = motionCurve.Evaluate(t);

            animatedStar.position = Vector3.Lerp(startPos, endPos, curvedT);
            yield return null;
        }

        animatedStar.position = endPos;

        // Change target star color to yellow/filled
        Image targetImage = targetSlot.GetComponent<Image>();
        if (targetImage != null)
        {
            targetImage.color = Color.yellow; // Or your filled star color
        }

        animatedStar.gameObject.SetActive(false);

        // --- ADD THIS HERE ---
        StageClearManager stageClearManager = FindFirstObjectByType<StageClearManager>();
        if (stageClearManager != null)
        {
            stageClearManager.NotifyStarFilled();
        }

        onComplete?.Invoke();
    }
}