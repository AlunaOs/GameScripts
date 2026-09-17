using UnityEngine;
using TMPro;

public class TelemetryUIPanel : MonoBehaviour
{
    [Header("UI Text References")]
    [SerializeField] private TextMeshProUGUI sessionCodeText;
    [SerializeField] private TextMeshProUGUI dateText;
    [SerializeField] private TextMeshProUGUI dungeonText;
    [SerializeField] private TextMeshProUGUI rankXpText;
    [SerializeField] private TextMeshProUGUI accuracyBreakdownText;
    [SerializeField] private TextMeshProUGUI avgResponseText;
    [SerializeField] private TextMeshProUGUI durationText;
    [SerializeField] private TextMeshProUGUI difficultyChangesText;

    [Header("Panel Root Canvas/Object")]
    [SerializeField] private GameObject telemetryPanel;

    public void ToggleTelemetryPanel()
    {
        bool isActive = !telemetryPanel.activeSelf;
        telemetryPanel.SetActive(isActive);

        if (isActive)
        {
            PopulateTelemetryData();
        }
    }

    public void PopulateTelemetryData()
    {
        // 1. Fetch session metrics if available
        bool hasData = TelemetryManager.Instance != null
                    && TelemetryManager.Instance.currentSession != null
                    && !string.IsNullOrEmpty(TelemetryManager.Instance.currentSession.session_code);

        SessionTelemetry session = hasData ? TelemetryManager.Instance.currentSession : null;
        SessionSummary summary = hasData ? TelemetryManager.Instance.CompileSummary() : null;

        // 2. Assign text components safely, pulling from GameManager as the reliable source for live stats
        if (sessionCodeText) sessionCodeText.text = session != null ? $"Session: {session.session_code}" : "Session: Live Play";
        if (dateText) dateText.text = session != null ? $"Date: {session.date}" : $"Date: {System.DateTime.Now:yyyy-MM-dd}";
        if (dungeonText) dungeonText.text = session != null ? $"Dungeon: {session.dungeon.ToUpper()}" : $"Dungeon: {GameManager.Instance.currentCategory.ToUpper()}";
        if (rankXpText) rankXpText.text = summary != null ? $"Rank: {summary.final_rank} | XP: {summary.xp_total}" : "Rank: -- | XP: 0";

        // Accuracy breakdown from GameManager / DDA controller
        float easyAcc = GameManager.Instance != null ? GameManager.Instance.GetTierAccuracy(1) : -1f;
        float medAcc = GameManager.Instance != null ? GameManager.Instance.GetTierAccuracy(2) : -1f;
        float hardAcc = GameManager.Instance != null ? GameManager.Instance.GetTierAccuracy(3) : -1f;

        string easyStr = easyAcc >= 0f ? $"{easyAcc:F0}%" : "N/A";
        string medStr = medAcc >= 0f ? $"{medAcc:F0}%" : "N/A";
        string hardStr = hardAcc >= 0f ? $"{hardAcc:F0}%" : "N/A";

        if (accuracyBreakdownText) accuracyBreakdownText.text = $"Easy: {easyStr} | Med: {medStr} | Hard: {hardStr}";

        if (avgResponseText)
        {
            float avgResp = GameManager.Instance != null ? GameManager.Instance.GetAverageResponseTime() : 0f;
            avgResponseText.text = $"Avg Response: {avgResp:F1}s";
        }

        // FIX: Pulls duration directly from GameManager's active timer string
        if (durationText)
        {
            string durationFormatted = GameManager.Instance != null ? GameManager.Instance.FormattedDuration : "0m 0s";
            durationText.text = $"Duration: {durationFormatted}";
        }

        // FIX: Pulls DDA adjustments directly from GameManager counter
        if (difficultyChangesText)
        {
            int ddaChanges = GameManager.Instance != null ? GameManager.Instance.GetDDAAdjustmentCount() : 0;
            difficultyChangesText.text = $"DDA Adjustments: {ddaChanges}";
        }
    }
    // h
    private void SetFallbackText()
    {
        if (sessionCodeText) sessionCodeText.text = "Session: No Data";
        if (dateText) dateText.text = "Date: --/--/----";
        if (dungeonText) dungeonText.text = "Dungeon: N/A";
        if (rankXpText) rankXpText.text = "Rank: -- | XP: 0";
        if (accuracyBreakdownText) accuracyBreakdownText.text = "Easy: N/A | Med: N/A | Hard: N/A";
        if (avgResponseText) avgResponseText.text = "Avg Response: 0.0s";
        if (durationText) durationText.text = "Duration: 0m 0s";
        if (difficultyChangesText) difficultyChangesText.text = "DDA Adjustments: 0";
    }
}