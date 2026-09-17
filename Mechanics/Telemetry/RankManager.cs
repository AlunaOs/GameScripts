using System;
using UnityEngine;

public enum RankTier { Bronze, Silver, Gold, Diamond }

public class RankManager : MonoBehaviour
{
    private static RankManager instance;
    public static RankManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<RankManager>();

                if (instance == null)
                {
                    GameObject autoGo = new GameObject("Runtime_RankManager");
                    instance = autoGo.AddComponent<RankManager>();
                    DontDestroyOnLoad(autoGo);
                }
            }
            return instance;
        }
    }

    [Header("Rank Settings")]
    private static RankTier currentTier = RankTier.Bronze;
    private static float currentProgress = 0f;

    // Keys used to identify save data in PlayerPrefs
    private const string SaveKeyProgress = "PlayerRankProgress";
    private const string SaveKeyTier = "PlayerRankTier";

    public static event Action<float> OnProgressChanged;
    public static event Action<RankTier> OnRankUp;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            LoadGameData();
        }
        else if (instance != this)
        {
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }

    public void ProcessPuzzleSuccess(float accuracyNorm)
    {
        float gain = Mathf.Lerp(2.0f, 8.4f, Mathf.Clamp01(accuracyNorm));
        AddProgress(gain);
    }

    public void ProcessPuzzleFailure()
    {
        AddProgress(-2.3f);
    }

    private void AddProgress(float amount)
    {
        currentProgress += amount;

        if (currentProgress >= 100f)
        {
            RankUp();
        }
        else if (currentProgress < 0f)
        {
            currentProgress = 0f;
        }

        SaveGameData(); // <-- Automatically save whenever progress changes!
        OnProgressChanged?.Invoke(currentProgress / 100f);
    }

    private void RankUp()
    {
        if (currentTier == RankTier.Diamond)
        {
            currentProgress = 100f;
            SaveGameData();
            return;
        }

        currentTier++;
        currentProgress = currentProgress - 100f;

        SaveGameData(); // <-- Automatically save the new rank!
        OnRankUp?.Invoke(currentTier);
    }

    public float GetCurrentProgressNormalized() => currentProgress / 100f;
    public RankTier GetCurrentRank() => currentTier;

    #region Save and Load Logic

    /// <summary>
    /// Saves the current progress and rank tier to the device's storage.
    /// </summary>
    private void SaveGameData()
    {
        PlayerPrefs.SetFloat(SaveKeyProgress, currentProgress);
        PlayerPrefs.SetInt(SaveKeyTier, (int)currentTier);
        PlayerPrefs.Save(); // Writes the data directly to disk
        Debug.Log($"[Save System] Progress Saved: {currentProgress}% | Rank: {currentTier}");
    }

    /// <summary>
    /// Loads the saved progress and rank tier from the device's storage.
    /// </summary>
    private void LoadGameData()
    {
        // Check if we have saved data. If not, defaults to 0% and Bronze.
        if (PlayerPrefs.HasKey(SaveKeyProgress))
        {
            currentProgress = PlayerPrefs.GetFloat(SaveKeyProgress);
            currentTier = (RankTier)PlayerPrefs.GetInt(SaveKeyTier, 0);
            Debug.Log($"[Save System] Loaded Saved Data: {currentProgress}% | Rank: {currentTier}");
        }
        else
        {
            currentProgress = 0f;
            currentTier = RankTier.Bronze;
            Debug.Log("[Save System] No save data found. Starting fresh!");
        }
    }

    /// <summary>
    /// Call this if you ever need to reset the player's progress entirely (e.g., a "Reset Game" button).
    /// </summary>
    public void ResetAllProgress()
    {
        PlayerPrefs.DeleteKey(SaveKeyProgress);
        PlayerPrefs.DeleteKey(SaveKeyTier);
        currentProgress = 0f;
        currentTier = RankTier.Bronze;

        OnProgressChanged?.Invoke(0f);
        OnRankUp?.Invoke(RankTier.Bronze);
        Debug.Log("[Save System] Progress fully wiped!");
    }

    #endregion
}