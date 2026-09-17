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

    private const int WIN_STREAK_THRESHOLD = 3;  // Exactly 3 correct answers required to increase tier
    private const int LOSE_STREAK_THRESHOLD = 2; // 2 wrong/struggles to decrease tier
    private const int N_MIN = 5;
    private const float ALPHA_LOW = 0.4f;
    private const float ALPHA_HIGH = 0.8f;



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

    public void EvaluatePerformance(bool isCorrect, float timeSpent, string currentCategory, DifficultyNotifier notifier = null)
    {
        TotalResponseTime += timeSpent;
        totalResponsesSubmitted++;

        float tLim = GetStruggleTimeLimit();
        bool struggled = isCorrect && (timeSpent > tLim);
        LastStruggled = struggled;

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
            Debug.Log($"[DDA] Correct. WinStreak={winStreak}/3 L={loseStreak}");
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
            Debug.Log($"[DDA] Wrong. W={winStreak} L={loseStreak}");
        }

        UpdateMovingAverageBuffer(isCorrect ? 1 : 0);

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

        // Strict 3/3 Milestone Rule Check
        if (winStreak >= WIN_STREAK_THRESHOLD)
        {
            deltaL = +1;
            reason = "Reached 3/3 correct streak milestone";
            winStreak = 0; // Reset streak after successful tier shift
        }
        else if (loseStreak >= LOSE_STREAK_THRESHOLD)
        {
            deltaL = -1;
            reason = $"Lose streak reached {LOSE_STREAK_THRESHOLD}";
            loseStreak = 0;
        }
        else if (struggled && !isCorrect)
        {
            deltaL = -1;
            reason = "Struggle detected on wrong answer";
            loseStreak = 0;
        }
        else
        {
            float ma = GetMovingAverage();
            if (ma >= 0f)
            {
                LastMovingAverage = ma;
                if (ma < ALPHA_LOW)
                {
                    deltaL = -1;
                    reason = $"MA={ma:P0} < α_low";
                }
                else if (ma > ALPHA_HIGH)
                {
                    deltaL = +1;
                    reason = $"MA={ma:P0} > α_high";
                }
            }
        }

        CurrentLevel = Mathf.Clamp(CurrentLevel + deltaL, 1, 3);
        LastDDADecision = reason;

        if (previousLevel != CurrentLevel)
        {
            if (TelemetryManager.Instance != null)
                TelemetryManager.Instance.RecordDifficultyChange();

            if (notifier != null)
            {
                if (CurrentLevel > previousLevel) notifier.ShowIncrease();
                else notifier.ShowDecrease();
            }

            Debug.Log($"[DDA] Difficulty Level shifted: Tier {previousLevel} -> Tier {CurrentLevel} | Reason: {reason}");
        }
    }

    public void HandleTotalFailure()
    {
        CurrentLevel = 1;
        winStreak = 0;
        loseStreak++;
        Debug.Log("[DDA] Total failure — reset level back to Tier 1.");
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
        return totalQuestionsAttempted > 0 ? (float)totalResponsesSubmitted / totalQuestionsAttempted : 0f;
    }

    public void Reset()
    {
        CurrentLevel = 1;
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
    }
}