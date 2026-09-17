using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RankProgressBarUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Slider slider; 
    [SerializeField] private TextMeshProUGUI percentageText; 
    [SerializeField] private TextMeshProUGUI rankValueText; 

    [Header("Animation Settings")]
    [SerializeField] private float animationDuration = 0.5f; 

    private Coroutine fillAnimationCoroutine;

    private void OnEnable()
    {
        RankManager.OnProgressChanged += UpdateProgressBar;
        RankManager.OnRankUp += UpdateRankText;
    }

    private void OnDisable()
    {
        RankManager.OnProgressChanged -= UpdateProgressBar;
        RankManager.OnRankUp -= UpdateRankText;
    }

    private IEnumerator Start()
    {
        if (slider == null) slider = GetComponent<Slider>();

        // Wait for 1 frame to let duplicate RankManagers clean up
        yield return null;

        if (RankManager.Instance != null)
        {
            float initialProgress = RankManager.Instance.GetCurrentProgressNormalized();
            slider.value = initialProgress;
            
            if (percentageText != null)
            {
                percentageText.text = $"{Mathf.RoundToInt(initialProgress * 100f)}%";
            }
            
            UpdateRankText(RankManager.Instance.GetCurrentRank());
        }
    }

    private void UpdateProgressBar(float targetProgress)
    {
        if (fillAnimationCoroutine != null)
        {
            StopCoroutine(fillAnimationCoroutine);
        }
        fillAnimationCoroutine = StartCoroutine(AnimateProgressBar(targetProgress));
    }

    private IEnumerator AnimateProgressBar(float targetProgress)
    {
        float startProgress = slider.value;
        float elapsed = 0f;

        float duration = animationDuration <= 0f ? 0.1f : animationDuration;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            
            float currentProgress = Mathf.Lerp(startProgress, targetProgress, t);
            
            if (slider != null) slider.value = currentProgress;
            if (percentageText != null) percentageText.text = $"{Mathf.RoundToInt(currentProgress * 100f)}%";
            
            yield return null; 
        }

        if (slider != null) slider.value = targetProgress;
        if (percentageText != null) percentageText.text = $"{Mathf.RoundToInt(targetProgress * 100f)}%";
    }

    private void UpdateRankText(RankTier newRank)
    {
        if (rankValueText != null)
        {
            rankValueText.text = $"Rank: {newRank.ToString().ToUpper()}";
        }
    }
}