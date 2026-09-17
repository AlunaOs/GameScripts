using System.Collections;
using TMPro;
using UnityEngine;

public class StageIntroManager : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Drag the Text component here that displays the stage name.")]
    [SerializeField] private TMP_Text stageText; 
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Stage Settings")]
    [SerializeField] private string stageThemeName = "Desert";

    [Header("Animation Timings")]
    [SerializeField] private float fadeInDuration = 0.5f;
    [SerializeField] private float displayDuration = 2.0f;
    [SerializeField] private float fadeOutDuration = 0.5f;

    private void Start()
    {
        // Force-check PlayerPrefs directly to see what stage it holds
        int currentStage = PlayerPrefs.GetInt("CurrentStageNumber", 1);
        Debug.Log("Current stage pulled from PlayerPrefs is: " + currentStage);

        if (stageText != null)
        {
            stageText.text = $"Stage {currentStage} - {stageThemeName}";
        }

        StartCoroutine(PlayIntroSequence());
    }

    private IEnumerator PlayIntroSequence()
    {
        if (canvasGroup == null) yield break;

        canvasGroup.alpha = 0f;
        gameObject.SetActive(true);

        float elapsedTime = 0f;
        while (elapsedTime < fadeInDuration)
        {
            elapsedTime += Time.deltaTime;
            canvasGroup.alpha = Mathf.Clamp01(elapsedTime / fadeInDuration);
            yield return null;
        }
        canvasGroup.alpha = 1f;

        yield return new WaitForSeconds(displayDuration);

        elapsedTime = 0f;
        while (elapsedTime < fadeOutDuration)
        {
            elapsedTime += Time.deltaTime;
            canvasGroup.alpha = Mathf.Clamp01(1f - (elapsedTime / fadeOutDuration));
            yield return null;
        }
        canvasGroup.alpha = 0f;

        gameObject.SetActive(false);
    }
}