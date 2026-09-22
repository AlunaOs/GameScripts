using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    public event System.Action OnTelemetryUpdated;

    public int currentLevel => ddaController.CurrentLevel;
    public string currentCategory = "Algebra";

    public bool isDataLoaded => questionGenerator != null && questionGenerator.IsLoaded;
    public DifficultyNotifier difficultyNotifier;

    private DDAController ddaController;
    private QuestionGenerator questionGenerator;
    private float questionTimer = 0f;
    private float sessionDuration = 0f;
    private int ddaAdjustmentCount = 0;

    private float lastRunDuration = 0f;
    private int lastRunDdaAdjustments = 0;
    private bool hasCompletedRun = false;

    public float SessionDuration => sessionDuration;
    public string FormattedDuration => GetFormattedSessionDuration(sessionDuration);

    private const string SAVE_CATEGORY = "Game_Category";

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        ddaController = new DDAController();
        ddaController.LoadState();
        questionGenerator = new QuestionGenerator();

        SceneManager.sceneLoaded += OnSceneLoaded;

        StartCoroutine(questionGenerator.LoadTemplates());
    }

    void OnDestroy()
    {
        if (Instance == this)
            SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (ddaController == null) return;
        Debug.Log($"[DDA] Scene loaded: {scene.name}");
        Debug.Log($"[DDA] Restored difficulty: {ddaController.CurrentLevel}");
    }

    void Update()
    {
        if (!hasCompletedRun)
        {
            questionTimer += Time.deltaTime;
            sessionDuration += Time.deltaTime;
        }
    }

    public void StartNewDungeonSession(string category, bool resetDifficulty = false)
    {
        currentCategory = category;

        if (ddaController != null && resetDifficulty)
        {
            ddaController.Reset();
        }

        sessionDuration = 0f;
        ddaAdjustmentCount = 0;
        questionTimer = 0f;
        hasCompletedRun = false;

        PlayerPrefs.SetString(SAVE_CATEGORY, currentCategory);
        PlayerPrefs.Save();

        Debug.Log($"[GameManager] New dungeon session started for '{category}'. Timers reset. Difficulty {(resetDifficulty ? "RESET" : "kept")} at tier {ddaController.CurrentLevel}.");
    }

    public void CompleteDungeonSession()
    {
        lastRunDuration = sessionDuration;
        lastRunDdaAdjustments = ddaAdjustmentCount;
        hasCompletedRun = true;
        Debug.Log("[GameManager] Dungeon completed. Stats archived for review.");
    }

    public void SetCategory(string category)
    {
        currentCategory = category;
        PlayerPrefs.SetString(SAVE_CATEGORY, currentCategory);
        PlayerPrefs.Save();
    }

    public void TrackQuestionPerformance(string questionText, int difficulty, bool isCorrect, float timeSpent)
    {
        if (timeSpent < 0f) timeSpent = 0f;
        EvaluatePerformance(isCorrect, timeSpent);
    }

    public Question GetQuestion()
    {
        questionTimer = 0f;
        int tier = ddaController.CurrentLevel;
        Question q = questionGenerator.GetQuestion(tier, currentCategory);
        Debug.Log($"[DDA] GetQuestion: requested tier {tier}, category '{currentCategory}', question difficulty = {(q != null ? q.difficulty.ToString() : "none")}");
        return q;
    }

    public void EvaluatePerformance(bool isCorrect, float timeSpent = -1f)
    {
        if (timeSpent < 0f) timeSpent = questionTimer;

        int oldLevel = ddaController.CurrentLevel;
        ddaController.EvaluatePerformance(isCorrect, timeSpent, currentCategory, difficultyNotifier);

        if (ddaController.CurrentLevel != oldLevel)
            ddaAdjustmentCount++;

        questionTimer = 0f;
        SaveProgress();

        Debug.Log($"[Telemetry] EvaluatePerformance: correct={isCorrect}, time={timeSpent:F2}, " +
                  $"totalQ={ddaController.TotalQuestionsAttempted}, " +
                  $"totalCorrect={ddaController.TotalCorrectAnswers}, " +
                  $"totalTime={ddaController.TotalResponseTime:F2}");

        OnTelemetryUpdated?.Invoke();
    }

    public float GetAverageResponseTime()
    {
        if (ddaController == null || ddaController.TotalQuestionsAttempted == 0)
            return 0f;

        return ddaController.TotalResponseTime / ddaController.TotalQuestionsAttempted;
    }

    public void SaveProgress()
    {
        PlayerPrefs.SetString(SAVE_CATEGORY, currentCategory);
        PlayerPrefs.Save();

        if (ddaController != null)
            ddaController.SaveState();
    }

    public void CommitDifficultyForNextStage(string nextSceneName)
    {
        if (ddaController == null) return;

        Debug.Log("[DDA] Stage completed");
        Debug.Log($"[DDA] Saving difficulty: {ddaController.CurrentLevel} (W={ddaController.WinStreak}, L={ddaController.LoseStreak}, MA={ddaController.GetMovingAverage():F2})");
        ddaController.SaveState();
        Debug.Log($"[DDA] Loading next stage '{nextSceneName}' with difficulty: {ddaController.CurrentLevel}");
    }

    public void LoadProgress()
    {
        if (PlayerPrefs.HasKey(SAVE_CATEGORY))
        {
            currentCategory = PlayerPrefs.GetString(SAVE_CATEGORY, "Algebra");
        }
    }

    public void HandleTotalFailure()
    {
        if (ddaController != null)
        {
            int oldLevel = ddaController.CurrentLevel;
            ddaController.HandleTotalFailure();

            if (ddaController.CurrentLevel != oldLevel)
                ddaAdjustmentCount++;

            SaveProgress();
        }
    }

    public void ResetProgress()
    {
        PlayerPrefs.DeleteAll();

        if (ddaController != null)
        {
            ddaController.Reset();
        }

        currentCategory = "Algebra";
        sessionDuration = 0f;
        ddaAdjustmentCount = 0;

        PlayerPrefs.SetInt("CurrentStageNumber", 1);
        PlayerPrefs.Save();

        if (RankManager.Instance != null)
        {
            RankManager.Instance.ResetAllProgress();
        }

        DialoguePuzzleManager dialogueManager = UnityEngine.Object.FindFirstObjectByType<DialoguePuzzleManager>();
        if (dialogueManager != null)
        {
            dialogueManager.ResetDialoguePuzzleState();
        }

        ScalePuzzleManager[] scalePuzzles = UnityEngine.Object.FindObjectsByType<ScalePuzzleManager>(FindObjectsSortMode.None);
        foreach (var scalePuzzle in scalePuzzles)
        {
            scalePuzzle.isCompleted = false;
        }

        DecodeQuizPuzzleManager[] quizPuzzles = UnityEngine.Object.FindObjectsByType<DecodeQuizPuzzleManager>(FindObjectsSortMode.None);
        foreach (var quizPuzzle in quizPuzzles)
        {
            quizPuzzle.ResetQuizState();
        }

        PuzzleManager[] letterPuzzles = UnityEngine.Object.FindObjectsByType<PuzzleManager>(FindObjectsSortMode.None);
        foreach (var letterPuzzle in letterPuzzles)
        {
            letterPuzzle.ResetPuzzleState();
        }

        if (ShopManagers.Instance != null)
        {
            ShopManagers.Instance.ResetProgress();
        }

        Debug.Log("[GameManager] Progress, Ranks, Shop Items, and all Puzzles completely reset!");
    }

    public string GetFormattedSessionDuration()
    {
        float targetDuration = hasCompletedRun ? lastRunDuration : sessionDuration;
        int totalSeconds = Mathf.FloorToInt(targetDuration);
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        return $"{minutes}m {seconds}s";
    }

    private string GetFormattedSessionDuration(float duration)
    {
        int totalSeconds = Mathf.FloorToInt(duration);
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        return $"{minutes}m {seconds}s";
    }

    public void NotifyTelemetryUpdated()
    {
        OnTelemetryUpdated?.Invoke();
    }

    public float GetAccuracyPercent()
    {
        return ddaController != null ? ddaController.GetAccuracyPercent() : 0f;
    }

    public float GetTierAccuracy(int tier)
    {
        return ddaController != null ? ddaController.GetTierAccuracy(tier) : -1f;
    }

    public float GetAverageResponsesPerQuestion()
    {
        return ddaController != null ? ddaController.GetAverageResponsesPerQuestion() : 0f;
    }

    public float GetSessionDuration() => hasCompletedRun ? lastRunDuration : sessionDuration;
    public int GetDDAAdjustmentCount() => hasCompletedRun ? lastRunDdaAdjustments : ddaAdjustmentCount;
    public int GetWinStreak() => ddaController.WinStreak;
    public int GetLoseStreak() => ddaController.LoseStreak;
    public float GetSuccessRate() => ddaController.TotalQuestionsAttempted > 0 ? (float)ddaController.TotalCorrectAnswers / ddaController.TotalQuestionsAttempted : 0f;
    public float GetMovingAverage() => ddaController.GetMovingAverage();
}
