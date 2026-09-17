using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using System.IO;

public class GeoWordDataSet : MonoBehaviour
{
    [System.Serializable]
    public class QuestionData
    {
        public string id;
        public string topic;
        public string difficulty; // "easy", "medium", or "hard"
        public string prompt;
        public string answer;
        public string hint;
    }

    [System.Serializable]
    public class QuestionDataset
    {
        public List<QuestionData> questions;
    }

    public QuestionDataset Database { get; private set; }
    public bool isLoaded { get; private set; } = false;

    public void LoadData()
    {
        if (isLoaded) return;
        StartCoroutine(LoadDatasetFromJSON());
    }

    private IEnumerator LoadDatasetFromJSON()
    {
        // Points to Assets/StreamingAssets/GeometryDataSets/Geometry_questionSet.json
        string filePath = Path.Combine(Application.streamingAssetsPath, "GeometryDataSets", "Geometry_questionSet.json");

        using (UnityWebRequest request = UnityWebRequest.Get(filePath))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Database = JsonUtility.FromJson<QuestionDataset>(request.downloadHandler.text);
                isLoaded = true;
            }
            else
            {
                Debug.LogError($"JSON Dataset File missing at: {filePath} — {request.error}");
            }
        }
    }
}