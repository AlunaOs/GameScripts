using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
public class PuzzleLoader : MonoBehaviour
{
    public PuzzleDatabase Database { get; private set; }

    public void LoadData()
    {
        // Avoid redundant loading if already parsed
        if (Database != null) return;

        string filePath = Path.Combine(Application.streamingAssetsPath, "GeometryDataSets", "totem_puzzles.json");
        string jsonText = "";

#if UNITY_ANDROID && !UNITY_EDITOR
    UnityWebRequest request = UnityWebRequest.Get(filePath);
    request.SendWebRequest();
    while (!request.isDone) { }

    if (request.result == UnityWebRequest.Result.Success)
    {
        jsonText = request.downloadHandler.text;
    }
    else
    {
        Debug.LogError("Failed to load JSON: " + request.error);
        return;
    }
#else
        if (File.Exists(filePath))
        {
            jsonText = File.ReadAllText(filePath);
        }
        else
        {
            Debug.LogError("JSON file not found at: " + filePath);
            return;
        }
#endif

        if (!string.IsNullOrEmpty(jsonText))
        {
            Database = JsonUtility.FromJson<PuzzleDatabase>(jsonText);
            Debug.Log("Successfully parsed totem puzzles dataset!");
        }
    }

    public PuzzleItem GetRandomPuzzle(string difficulty)
    {
        if (Database == null) LoadData();

        List<PuzzleItem> targetList = difficulty.ToLower() switch
        {
            "easy" => Database.easy,
            "medium" => Database.medium,
            "hard" => Database.hard,
            _ => Database.easy
        };

        if (targetList != null && targetList.Count > 0)
        {
            int randomIndex = Random.Range(0, targetList.Count);
            return targetList[randomIndex];
        }

        return null;
    }
}