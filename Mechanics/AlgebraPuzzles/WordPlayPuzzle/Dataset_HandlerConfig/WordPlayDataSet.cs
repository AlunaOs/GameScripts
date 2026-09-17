using UnityEngine;
using UnityEngine.Networking;
using System.IO;
using System.Collections;
using System.Collections.Generic;

[System.Serializable]
public class QuestionData
{
    public string id;
    public string difficulty;
    public string prompt;
    public string answer;
}

[System.Serializable]
public class QuestionDataset
{
    public List<QuestionData> questions;
}

public class WordPlayDataSet
{
    private List<QuestionData> fullDataset = new List<QuestionData>();
    public bool IsLoaded { get; private set; } = false;

    public IEnumerator LoadDatasetRoutine(string fileName = "Decode_questionSet.json", int maxAnswerLength = 5, System.Action onComplete = null)
    {
        string filePath = Path.Combine(Application.streamingAssetsPath, fileName);

        using (UnityWebRequest request = UnityWebRequest.Get(filePath))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                QuestionDataset loadedData = JsonUtility.FromJson<QuestionDataset>(request.downloadHandler.text);
                if (loadedData != null && loadedData.questions != null)
                {
                    fullDataset = loadedData.questions.FindAll(q => q.answer.Trim().Length <= maxAnswerLength);
                }
                IsLoaded = true;
            }
            else
            {
                Debug.LogError($"[WordPlayDataSet] JSON Dataset File missing at path: {filePath} — {request.error}");
                IsLoaded = true; // Mark as loaded to prevent permanent hanging coroutines
            }
        }

        onComplete?.Invoke();
    }

    public List<QuestionData> GetFilteredDataset()
    {
        return fullDataset;
    }
}