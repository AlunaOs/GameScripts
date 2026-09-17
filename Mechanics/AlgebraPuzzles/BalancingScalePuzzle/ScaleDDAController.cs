using System.Collections.Generic;
public class ScaleDDAController
{
    private const int WIN_STREAK_THRESHOLD = 3;
    private const int LOSE_STREAK_THRESHOLD = 2;
    private const int N_MIN = 5;
    private const float ALPHA_LOW = 0.4f;
    private const float ALPHA_HIGH = 0.8f;
    private const int MA_WINDOW_SIZE = 5; 

    public DifficultyLevel CurrentDifficulty { get; private set; } = DifficultyLevel.Easy;

    private int winStreak;  
    private int loseStreak; 
    private int totalAttempted; 
    private int totalCorrect; 
    private readonly Queue<int> maBuffer = new Queue<int>(); 

    public string LastDecisionReason { get; private set; } = "None";
    public float LastMovingAverage { get; private set; } = -1f; 
    public float LastSuccessRate { get; private set; }
    public bool LastAnswerStruggled { get; private set; }

    public float GetStruggleTimeLimit()
    {
        switch (CurrentDifficulty)
        {
            case DifficultyLevel.Easy: return 15f;
            case DifficultyLevel.Medium: return 25f;
            default: return 40f;
        }
    }

    public bool EvaluateAnswer(bool isCorrect, float timeSpentSeconds)
    {
        float tLim = GetStruggleTimeLimit();

        bool struggledButCorrect = isCorrect && timeSpentSeconds > tLim;
        bool struggledAndWrong = !isCorrect && timeSpentSeconds > tLim;
        LastAnswerStruggled = struggledButCorrect;

        totalAttempted++;
        if (isCorrect) totalCorrect++;
        float successRate = (float)totalCorrect / totalAttempted;
        LastSuccessRate = successRate;

        if (isCorrect && !struggledButCorrect)
        {
            winStreak++;
            loseStreak = 0;
        }
        else if (isCorrect && struggledButCorrect)
        {

            loseStreak = 0;
        }
        else
        {
            loseStreak++;
            winStreak = 0;
        }

        maBuffer.Enqueue(isCorrect ? 1 : 0);
        while (maBuffer.Count > MA_WINDOW_SIZE) maBuffer.Dequeue();

        int previousLevel = (int)CurrentDifficulty;
        int deltaL = 0;
        string reason = "No change";
        bool regenEasier = false;

        if (winStreak >= WIN_STREAK_THRESHOLD)
        {
            deltaL = +1;
            reason = $"Win streak reached {WIN_STREAK_THRESHOLD}";
            winStreak = 0;
        }
        else if (loseStreak >= LOSE_STREAK_THRESHOLD)
        {
            deltaL = -1;
            reason = $"Lose streak reached {LOSE_STREAK_THRESHOLD}";
            loseStreak = 0;
            regenEasier = true;
        }
        else if (struggledAndWrong)
        {
            deltaL = -1;
            reason = "Struggle detected on wrong answer";
            regenEasier = true;
        }
        else if (maBuffer.Count >= MA_WINDOW_SIZE)
        {
            float ma = SumOf(maBuffer) / (float)MA_WINDOW_SIZE;
            LastMovingAverage = ma;

            if (ma < ALPHA_LOW)
            {
                deltaL = -1;
                reason = $"Moving Average={ma:P0} < alpha_low={ALPHA_LOW:P0}";
                regenEasier = true;
            }
            else if (ma > ALPHA_HIGH)
            {
                deltaL = +1;
                reason = $"Moving Average={ma:P0} > alpha_high={ALPHA_HIGH:P0}";
            }
            else
            {
                reason = $"Moving Average={ma:P0} within [{ALPHA_LOW:P0}, {ALPHA_HIGH:P0}] - maintain";
            }
        }
        else if (totalAttempted >= N_MIN)
        {
            if (successRate < ALPHA_LOW && (int)CurrentDifficulty > 1)
            {
                deltaL = -1;
                reason = $"Global success rate={successRate:P0} < alpha_low (MA buffer not yet full)";
                regenEasier = true;
            }
            else if (successRate > ALPHA_HIGH && (int)CurrentDifficulty < 3)
            {
                deltaL = +1;
                reason = $"Global success rate={successRate:P0} > alpha_high (MA buffer not yet full)";
            }
        }

        if (deltaL == 0 && struggledButCorrect) regenEasier = true;

        int newLevel = Clamp(previousLevel + deltaL, 1, 3);
        CurrentDifficulty = (DifficultyLevel)newLevel;
        LastDecisionReason = reason;

        return regenEasier;
    }

    public void Reset()
    {
        CurrentDifficulty = DifficultyLevel.Easy;
        winStreak = 0;
        loseStreak = 0;
        totalAttempted = 0;
        totalCorrect = 0;
        maBuffer.Clear();
        LastDecisionReason = "None";
        LastMovingAverage = -1f;
        LastSuccessRate = 0f;
        LastAnswerStruggled = false;
    }

    private static int SumOf(Queue<int> q)
    {
        int total = 0;
        foreach (var v in q) total += v;
        return total;
    }

    private static int Clamp(int v, int min, int max) => v < min ? min : (v > max ? max : v);
}