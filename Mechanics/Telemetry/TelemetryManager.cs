using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

public class TelemetryManager : MonoBehaviour
{
    public static TelemetryManager Instance { get; private set; }

    public SessionTelemetry currentSession = new SessionTelemetry();

    // Stats & Counters
    private int totalEasyAttempts = 0, correctEasyAttempts = 0;
    private int totalMediumAttempts = 0, correctMediumAttempts = 0;
    private int totalHardAttempts = 0, correctHardAttempts = 0;
    private int totalHintsUsed = 0;
    private int difficultyChangeCount = 0;

    // Time Tracking
    private float totalGameplayDuration = 0f;
    private float stageStartTime = 0f;
    private bool isTimerRunning = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // Only initialize if a session hasn't been created yet
        if (string.IsNullOrEmpty(currentSession.session_code))
        {
            InitializeSession();
        }
    }

    public void InitializeSession()
    {
        // Reset all counters for a brand new run
        totalEasyAttempts = 0; correctEasyAttempts = 0;
        totalMediumAttempts = 0; correctMediumAttempts = 0;
        totalHardAttempts = 0; correctHardAttempts = 0;
        totalHintsUsed = 0;
        difficultyChangeCount = 0;
        totalGameplayDuration = 0f;
        isTimerRunning = false;

        currentSession = new SessionTelemetry();
        currentSession.session_code = "MS-" + UnityEngine.Random.Range(1000, 9999);
        currentSession.date = DateTime.Now.ToString("yyyy-MM-dd");
        currentSession.dungeon = GameManager.Instance != null ? GameManager.Instance.currentCategory.ToLower() : "algebra";
        currentSession.attempts = new List<AttemptData>();
        currentSession.summary = new SessionSummary();
    }

    // ── Timer Controls ──────────────────────────────────────────────────
    
    public void StartStageTimer()
    {
        stageStartTime = Time.time;
        isTimerRunning = true;
    }

    public void EndStageTimer()
    {
        if (isTimerRunning)
        {
            totalGameplayDuration += (Time.time - stageStartTime);
            isTimerRunning = false;
        }
    }

    // ── Data Logging ────────────────────────────────────────────────────

    public void RecordAttempt(string topic, int levelTier, int attemptNo, bool isCorrect, float responseTimeSec, bool hintUsed)
    {
        string tierName = levelTier == 1 ? "easy" : (levelTier == 2 ? "medium" : "hard");

        AttemptData attempt = new AttemptData
        {
            topic = topic,
            tier = tierName,
            attempt_no = attemptNo,
            correct = isCorrect,
            response_time_ms = Mathf.Round(responseTimeSec * 1000f),
            hint_used = hintUsed
        };
        currentSession.attempts.Add(attempt);

        if (levelTier == 1) { totalEasyAttempts++; if (isCorrect) correctEasyAttempts++; }
        else if (levelTier == 2) { totalMediumAttempts++; if (isCorrect) correctMediumAttempts++; }
        else if (levelTier == 3) { totalHardAttempts++; if (isCorrect) correctHardAttempts++; }

        if (hintUsed) totalHintsUsed++;
    }

    public void RecordDifficultyChange()
    {
        difficultyChangeCount++;
        Debug.Log($"[TelemetryManager] Total DDA Adjustments recorded: {difficultyChangeCount}");
    }

    public bool HasAttemptsInTier(int levelTier)
    {
        if (levelTier == 1) return totalEasyAttempts > 0;
        if (levelTier == 2) return totalMediumAttempts > 0;
        if (levelTier == 3) return totalHardAttempts > 0;
        return false;
    }

    // ── Summary & Export ────────────────────────────────────────────────

    public SessionSummary CompileSummary()
    {
        if (currentSession.summary == null)
            currentSession.summary = new SessionSummary();

        SessionSummary s = currentSession.summary;

        // Rank & XP
        if (RankManager.Instance != null)
        {
            s.final_rank = RankManager.Instance.GetCurrentRank().ToString();
            s.xp_total = Mathf.RoundToInt(RankManager.Instance.GetCurrentProgressNormalized() * 100f);
        }
        else
        {
            s.final_rank = "Bronze";
            s.xp_total = 0;
        }

        // Accuracy
        s.accuracy_easy = totalEasyAttempts > 0 ? (float)correctEasyAttempts / totalEasyAttempts : 0f;
        s.accuracy_medium = totalMediumAttempts > 0 ? (float)correctMediumAttempts / totalMediumAttempts : 0f;
        s.accuracy_hard = totalHardAttempts > 0 ? (float)correctHardAttempts / totalHardAttempts : 0f;

        // Metrics
        s.mean_response_time_ms = GameManager.Instance != null ? Mathf.RoundToInt(GameManager.Instance.GetAverageResponseTime() * 1000f) : 0;
        s.hint_uses_total = totalHintsUsed;
        
        // Locks persistent DDA adjustments
        s.difficulty_changes = difficultyChangeCount;

        // Calculate current duration (locks active time if gameplay stopped)
        float currentTotalDuration = totalGameplayDuration;
        if (isTimerRunning)
        {
            currentTotalDuration += (Time.time - stageStartTime);
        }
        s.session_duration_sec = Mathf.RoundToInt(currentTotalDuration);

        return s;
    }

    public string ExportSessionJSON()
    {
        CompileSummary();
        string json = JsonUtility.ToJson(currentSession, true);

        string path = Path.Combine(Application.persistentDataPath, $"Telemetry_{currentSession.session_code}.json");
        File.WriteAllText(path, json);
        Debug.Log($"[TelemetryManager] Saved session data to {path}");

        return json;
    }

    public void ExportSessionCSV()
    {
        CompileSummary();

        StringBuilder csv = new StringBuilder();

        // 1. Session Summary Section
        csv.AppendLine("SESSION SUMMARY");
        csv.AppendLine("Session Code,Date,Dungeon,Final Rank,XP Total,Easy Acc,Med Acc,Hard Acc,Avg Response (ms),Hints Used,DDA Adjustments,Duration (s)");
        csv.AppendLine($"{currentSession.session_code},{currentSession.date},{currentSession.dungeon},{currentSession.summary.final_rank},{currentSession.summary.xp_total},{currentSession.summary.accuracy_easy:P0},{currentSession.summary.accuracy_medium:P0},{currentSession.summary.accuracy_hard:P0},{currentSession.summary.mean_response_time_ms},{currentSession.summary.hint_uses_total},{currentSession.summary.difficulty_changes},{currentSession.summary.session_duration_sec}");
        csv.AppendLine();

        // 2. Individual Attempts Breakdown
        csv.AppendLine("ATTEMPT LOGS");
        csv.AppendLine("Attempt No,Topic,Tier,Correct,Response Time (ms),Hint Used");

        foreach (var attempt in currentSession.attempts)
        {
            csv.AppendLine($"{attempt.attempt_no},{attempt.topic},{attempt.tier},{attempt.correct},{attempt.response_time_ms},{attempt.hint_used}");
        }

        string path = Path.Combine(Application.persistentDataPath, $"Telemetry_{currentSession.session_code}.csv");
        File.WriteAllText(path, csv.ToString());
        Debug.Log($"[TelemetryManager] Saved CSV report to {path}");
    }
}