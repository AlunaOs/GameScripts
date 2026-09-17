using UnityEngine;
using TMPro;

public class MainInterfaceTelemetryDisplay : MonoBehaviour
{
    [Header("Telemetry Text Holders")]
    [Tooltip("Drag the 'Average Response Time' text holder here.")]
    public TextMeshProUGUI txtAvgResponseTime;

    [Tooltip("Drag the 'Average Responses Per Question' text holder here.")]
    public TextMeshProUGUI txtAvgResponsesPerQuestion;

    [Tooltip("Drag the 'Accuracy' text holder here.")]
    public TextMeshProUGUI txtAccuracy;

    void OnEnable()
    {
        ConnectAndRefresh();
    }

    void OnDisable()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnTelemetryUpdated -= RefreshDisplay;
        }
    }

    private void ConnectAndRefresh()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnTelemetryUpdated -= RefreshDisplay; // Prevent duplicate hooks
            GameManager.Instance.OnTelemetryUpdated += RefreshDisplay;
            RefreshDisplay();
        }
        else
        {
            StartCoroutine(WaitForGameManagerThenSubscribe());
        }
    }

    private System.Collections.IEnumerator WaitForGameManagerThenSubscribe()
    {
        while (GameManager.Instance == null)
            yield return null;

        GameManager.Instance.OnTelemetryUpdated -= RefreshDisplay;
        GameManager.Instance.OnTelemetryUpdated += RefreshDisplay;
        RefreshDisplay();
    }

    public void RefreshDisplay()
    {
        if (GameManager.Instance != null)
        {
            if (txtAvgResponseTime != null)
            {
                float avgTime = GameManager.Instance.GetAverageResponseTime();
                txtAvgResponseTime.text = $"<b>AVG RESPONSE TIME:</b>  <color=#00E5FF>{avgTime:F1}s</color>";
            }

            if (txtAvgResponsesPerQuestion != null)
            {
                float avgAttempts = GameManager.Instance.GetAverageResponsesPerQuestion();
                txtAvgResponsesPerQuestion.text = $"<b>RESPONSES / QUESTION:</b>  <color=#FFD600>{avgAttempts:F1}</color>";
            }

            if (txtAccuracy != null)
            {
                float accuracy = GameManager.Instance.GetAccuracyPercent();
                string colorHex = accuracy >= 70f ? "#00FF66" : accuracy >= 40f ? "#FFAB00" : "#FF1744";
                txtAccuracy.text = $"<b>ACCURACY:</b>  <color={colorHex}>{accuracy:F0}%</color>";
            }
        }
        else
        {
            if (txtAvgResponseTime != null) txtAvgResponseTime.text = "<b>AVG RESPONSE TIME:</b>  <color=#00E5FF>0.0s</color>";
            if (txtAvgResponsesPerQuestion != null) txtAvgResponsesPerQuestion.text = "<b>RESPONSES / QUESTION:</b>  <color=#FFD600>0.0</color>";
            if (txtAccuracy != null) txtAccuracy.text = "<b>ACCURACY:</b><color=#00FF66>0%</color>";
        }
    }
}