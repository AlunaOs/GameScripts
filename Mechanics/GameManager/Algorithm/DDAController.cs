using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class DDAController
{
    // ─────────────────────────────────────────────────────────────────────────
    //  PROPERTIES & STATE
    // ─────────────────────────────────────────────────────────────────────────
    public int CurrentLevel { get; private set; } = 1;
    public int WinStreak => winStreak;
    public int LoseStreak => loseStreak;
    public int TotalQuestionsAttempted => totalQuestionsAttempted;
    public int TotalCorrectAnswers => totalCorrectAnswers;
    public float TotalResponseTime { get; private set; } = 0f;

    private int winStreak = 0;
    private int loseStreak = 0;
    private int totalQuestionsAttempted = 0;
    private int totalCorrectAnswers = 0;

    private int totalResponsesSubmitted = 0;
    public int TotalResponsesSubmitted => totalResponsesSubmitted;

    public const int MIN_LEVEL = 1;
    public const int MAX_LEVEL = 3;

    private const int WIN_STREAK_THRESHOLD = 3;
    private const int LOSE_STREAK_THRESHOLD = 2;
    private const int N_MIN = 5;              // ported from ScaleDDAController
    private const float ALPHA_LOW = 0.4f;
    private const float ALPHA_HIGH = 0.8f;

    public bool streakCanPromoteAtHighSuccess = false;

    private float GetStruggleTimeLimit() =>
        CurrentLevel == 1 ? 15f : CurrentLevel == 2 ? 25f : 40f;

    private const int MA_WINDOW_SIZE = 5;
    private Queue<int> maBuffer = new Queue<int>();
    private Dictionary<int, int> difficultyFailCount = new Dictionary<int, int>();

    private Dictionary<int, int> tierAttempts = new Dictionary<int, int>() { { 1, 0 }, { 2, 0 }, { 3, 0 } };
    private Dictionary<int, int> tierCorrect = new Dictionary<int, int>() { { 1, 0 }, { 2, 0 }, { 3, 0 } };

    public string LastDDADecision { get; private set; } = "None";
    public float LastMovingAverage { get; private set; } = 0f;
    public float LastSuccessRate { get; private set; } = 0f;
    public bool LastStruggled { get; private set; } = false;

    // NEW — ported from ScaleDDAController: slow wrong answers are a strong signal.
    public bool LastStruggledAndWrong { get; private set; } = false;

    // ── PlayerPrefs keys ─────────────────────────────────────────────────────
    private const string KEY_LEVEL = "DDA_Level";
    private const string KEY_WIN = "DDA_WinStreak";
    private const string KEY_LOSE = "DDA_LoseStreak";
    private const string KEY_MA = "DDA_MABuffer";
    private const string KEY_TOTAL_ATT = "DDA_TotalAttempts";
    private const string KEY_TOTAL_CORRECT = "DDA_TotalCorrect";
    private const string KEY_TOTAL_TIME = "DDA_TotalTime";
    private const string KEY_TOTAL_RESP = "DDA_TotalResp";

    // ─────────────────────────────────────────────────────────────────────────
    //  RECORD AN ANSWER
    // ─────────────────────────────────────────────────────────────────────────
    public void EvaluatePerformance(bool isCorrect, float timeSpent, string currentCategory, DifficultyNotifier notifier = null)
    {
        Debug.Log($"[DDA] Current Difficulty: {CurrentLevel}");
        Debug.Log($"[DDA] Recorded Answer: {(isCorrect ? "Correct" : "Incorrect")} ({timeSpent:F1}s)");

        TotalResponseTime += timeSpent;
        totalResponsesSubmitted++;

        float tLim = GetStruggleTimeLimit();
        bool struggled = isCorrect && (timeSpent > tLim);
        bool struggledAndWrong = !isCorrect && (timeSpent > tLim);

        LastStruggled = struggled;
        LastStruggledAndWrong = struggledAndWrong;

        totalQuestionsAttempted++;
        if (isCorrect) totalCorrectAnswers++;

        if (!tierAttempts.ContainsKey(CurrentLevel)) tierAttempts[CurrentLevel] = 0;
        tierAttempts[CurrentLevel]++;

        if (isCorrect)
        {
            if (!tierCorrect.ContainsKey(CurrentLevel)) tierCorrect[CurrentLevel] = 0;
            tierCorrect[CurrentLevel]++;
        }

        float successRate = totalQuestionsAttempted > 0
            ? (float)totalCorrectAnswers / totalQuestionsAttempted
            : 0f;
        LastSuccessRate = successRate;

        if (isCorrect && !struggled)
        {
            winStreak++;
            loseStreak = 0;
            Debug.Log($"[DDA] Correct. WinStreak={winStreak}/{WIN_STREAK_THRESHOLD} L={loseStreak}");
        }
        else if (isCorrect && struggled)
        {
            loseStreak = 0;
            Debug.Log($"[DDA] Correct BUT struggled. Win streak preserved. W={winStreak}");
        }
        else
        {
            loseStreak++;
            winStreak = 0;
            if (!difficultyFailCount.ContainsKey(CurrentLevel))
                difficultyFailCount[CurrentLevel] = 0;
            difficultyFailCount[CurrentLevel]++;
            Debug.Log($"[DDA] Wrong. W={winStreak} L={loseStreak} (struggled={struggledAndWrong})");
        }

        UpdateMovingAverageBuffer(isCorrect ? 1 : 0);

        float ma = GetMovingAverage();
        if (ma >= 0f)
        {
            Debug.Log($"[DDA] Moving Average: {ma:F2}");
            Debug.Log($"[DDA] Success Rate: {ma * 100f:F0}%");
        }
        else
        {
            Debug.Log($"[DDA] Moving Average: not ready yet ({maBuffer.Count}/{MA_WINDOW_SIZE} answers)");
        }

        if (TelemetryManager.Instance != null)
        {
            TelemetryManager.Instance.RecordAttempt(
                currentCategory,
                CurrentLevel,
                totalQuestionsAttempted,
                isCorrect,
                timeSpent,
                false
            );
        }

        AdjustDifficultyAdaptively(isCorrect, struggled, successRate, notifier);
    }

    private void AdjustDifficultyAdaptively(bool isCorrect, bool struggled, float successRate, DifficultyNotifier notifier)
    {
        int previousLevel = CurrentLevel;
        int deltaL = 0;
        string reason = "No change";

        float ma = GetMovingAverage();
        bool maReady = ma >= 0f;
        if (maReady) LastMovingAverage = ma;

        if (loseStreak >= LOSE_STREAK_THRESHOLD)
        {
            deltaL = -1;
            reason = $"Lose streak reached {LOSE_STREAK_THRESHOLD}";
        }
        else if (maReady && ma <= ALPHA_LOW)
        {
            deltaL = -1;
            reason = $"Success rate {ma * 100f:F0}% <= {ALPHA_LOW * 100f:F0}%";
        }
        else if (winStreak >= WIN_STREAK_THRESHOLD)
        {
            if (maReady && ma >= ALPHA_HIGH && !streakCanPromoteAtHighSuccess)
            {
                reason = $"3/3 streak reached, but success rate {ma * 100f:F0}% >= {ALPHA_HIGH * 100f:F0}% so difficulty holds";
                winStreak = 0;
            }
            else
            {
                deltaL = +1;
                reason = "Reached 3/3 correct streak milestone";
            }
        }
        // NEW — ported from ScaleDDAController: N_MIN fallback while MA window fills
        else if (!maReady && totalQuestionsAttempted >= N_MIN)
        {
            if (successRate < ALPHA_LOW && CurrentLevel > MIN_LEVEL)
            {
                deltaL = -1;
                reason = $"Global success rate {successRate * 100f:F0}% < {ALPHA_LOW * 100f:F0}% (MA not yet full)";
            }
            else if (successRate > ALPHA_HIGH && CurrentLevel < MAX_LEVEL)
            {
                deltaL = +1;
                reason = $"Global success rate {successRate * 100f:F0}% > {ALPHA_HIGH * 100f:F0}% (MA not yet full)";
            }
            else
            {
                reason = $"Success rate {successRate * 100f:F0}% between thresholds (MA not yet full): hold";
            }
        }
        else if (maReady && ma >= ALPHA_HIGH)
        {
            reason = $"Success rate {ma * 100f:F0}% >= {ALPHA_HIGH * 100f:F0}%: hold";
        }
        else if (maReady)
        {
            reason = $"Success rate {ma * 100f:F0}% is between {ALPHA_LOW * 100f:F0}% and {ALPHA_HIGH * 100f:F0}%: hold";
        }

        // NEW — ported from ScaleDDAController: wrong-and-slow forces a drop
        // if no higher-priority rule already fired.
        if (deltaL == 0 && LastStruggledAndWrong && CurrentLevel > MIN_LEVEL)
        {
            deltaL = -1;
            reason = "Struggle detected on wrong answer";
        }

        if (deltaL != 0)
        {
            winStreak = 0;
            loseStreak = 0;
        }

        CurrentLevel = Mathf.Clamp(CurrentLevel + deltaL, MIN_LEVEL, MAX_LEVEL);
        LastDDADecision = reason;

        if (previousLevel != CurrentLevel)
        {
            // Fresh evidence for the new tier
            maBuffer.Clear();

            if (TelemetryManager.Instance != null)
                TelemetryManager.Instance.RecordDifficultyChange();

            if (notifier != null)
            {
                if (CurrentLevel > previousLevel) notifier.ShowIncrease();
                else notifier.ShowDecrease();
            }

            string verb = CurrentLevel > previousLevel ? "increased" : "decreased";
            Debug.Log($"[DDA] Difficulty {verb}: {previousLevel} → {CurrentLevel} | Reason: {reason}");
        }
        else if (deltaL != 0)
        {
            Debug.Log($"[DDA] Difficulty remains: {CurrentLevel} (already at limit) | Reason: {reason}");
        }
        else
        {
            Debug.Log($"[DDA] Difficulty remains: {CurrentLevel} | Reason: {reason}");
        }
    }

    public void HandleTotalFailure()
    {
        int previousLevel = CurrentLevel;

        CurrentLevel = MIN_LEVEL;
        winStreak = 0;
        loseStreak++;

        if (previousLevel != CurrentLevel)
        {
            maBuffer.Clear();
            if (TelemetryManager.Instance != null)
                TelemetryManager.Instance.RecordDifficultyChange();
        }

        Debug.Log($"[DDA] Total failure — difficulty {previousLevel} → {CurrentLevel} (Tier 1).");
    }

    private void UpdateMovingAverageBuffer(int binaryOutcome)
    {
        maBuffer.Enqueue(binaryOutcome);
        while (maBuffer.Count > MA_WINDOW_SIZE)
            maBuffer.Dequeue();
    }

    public float GetMovingAverage()
    {
        if (maBuffer.Count < MA_WINDOW_SIZE) return -1f;
        return (float)maBuffer.Sum() / MA_WINDOW_SIZE;
    }

    public float GetAccuracyPercent()
    {
        return totalQuestionsAttempted > 0
            ? ((float)totalCorrectAnswers / totalQuestionsAttempted) * 100f
            : 0f;
    }

    public float GetTierAccuracy(int tier)
    {
        if (tierAttempts.ContainsKey(tier) && tierAttempts[tier] > 0)
        {
            return ((float)tierCorrect[tier] / tierAttempts[tier]) * 100f;
        }
        return -1f;
    }

    public float GetAverageResponsesPerQuestion()
    {
        return totalQuestionsAttempted > 0
            ? (float)totalResponsesSubmitted / totalQuestionsAttempted
            : 0f;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  PERSISTENCE
    // ─────────────────────────────────────────────────────────────────────────
    public void SaveState()
    {
        PlayerPrefs.SetInt(KEY_LEVEL, CurrentLevel);
        PlayerPrefs.SetInt(KEY_WIN, winStreak);
        PlayerPrefs.SetInt(KEY_LOSE, loseStreak);
        PlayerPrefs.SetString(KEY_MA, string.Join("", maBuffer));

        PlayerPrefs.SetInt(KEY_TOTAL_ATT, totalQuestionsAttempted);
        PlayerPrefs.SetInt(KEY_TOTAL_CORRECT, totalCorrectAnswers);
        PlayerPrefs.SetFloat(KEY_TOTAL_TIME, TotalResponseTime);
        PlayerPrefs.SetInt(KEY_TOTAL_RESP, totalResponsesSubmitted);

        PlayerPrefs.Save();
    }

    public void LoadState()
    {
        if (!PlayerPrefs.HasKey(KEY_LEVEL)) return;

        CurrentLevel = Mathf.Clamp(PlayerPrefs.GetInt(KEY_LEVEL, MIN_LEVEL), MIN_LEVEL, MAX_LEVEL);
        winStreak = PlayerPrefs.GetInt(KEY_WIN, 0);
        loseStreak = PlayerPrefs.GetInt(KEY_LOSE, 0);

        maBuffer.Clear();
        foreach (char c in PlayerPrefs.GetString(KEY_MA, ""))
        {
            if (c == '0' || c == '1') maBuffer.Enqueue(c == '1' ? 1 : 0);
        }
        while (maBuffer.Count > MA_WINDOW_SIZE) maBuffer.Dequeue();

        totalQuestionsAttempted = PlayerPrefs.GetInt(KEY_TOTAL_ATT, 0);
        totalCorrectAnswers = PlayerPrefs.GetInt(KEY_TOTAL_CORRECT, 0);
        TotalResponseTime = PlayerPrefs.GetFloat(KEY_TOTAL_TIME, 0f);
        totalResponsesSubmitted = PlayerPrefs.GetInt(KEY_TOTAL_RESP, 0);

        Debug.Log($"[DDA] Restored difficulty: {CurrentLevel} (W={winStreak}, L={loseStreak}, " +
                  $"MA window={maBuffer.Count}/{MA_WINDOW_SIZE}, " +
                  $"Attempts={totalQuestionsAttempted}, Correct={totalCorrectAnswers}, " +
                  $"TotalTime={TotalResponseTime:F1}, Responses={totalResponsesSubmitted})");
    }

    public void Reset()
    {
        Debug.LogWarning($"[DDA] Reset() called — difficulty {CurrentLevel} → {MIN_LEVEL}");

        CurrentLevel = MIN_LEVEL;
        winStreak = 0;
        loseStreak = 0;
        totalQuestionsAttempted = 0;
        totalCorrectAnswers = 0;
        TotalResponseTime = 0f;
        totalResponsesSubmitted = 0;
        maBuffer.Clear();
        difficultyFailCount.Clear();
        tierAttempts.Clear();
        tierCorrect.Clear();
        tierAttempts = new Dictionary<int, int>() { { 1, 0 }, { 2, 0 }, { 3, 0 } };
        tierCorrect = new Dictionary<int, int>() { { 1, 0 }, { 2, 0 }, { 3, 0 } };

        SaveState();
    }
}
