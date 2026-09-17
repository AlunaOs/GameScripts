using System.Collections.Generic;
using UnityEngine;

public class CongruencePuzzleJSONLoader : MonoBehaviour
{
    [SerializeField] private string jsonResourceFileName = "CongruencePuzzleData";

    public List<CongruenceQuestionData> LoadQuestions(int count = 3)
    {
        TextAsset jsonTextAsset = Resources.Load<TextAsset>(jsonResourceFileName);
        
        if (jsonTextAsset == null)
        {
            Debug.LogError($"[CongruencePuzzleJSONLoader] Could not find '{jsonResourceFileName}' in Resources!");
            return new List<CongruenceQuestionData>();
        }

        CongruencePuzzleData parsedData = JsonUtility.FromJson<CongruencePuzzleData>(jsonTextAsset.text);
        
        if (parsedData == null || parsedData.questions == null || parsedData.questions.Count == 0)
        {
            Debug.LogError("[CongruencePuzzleJSONLoader] Failed to parse JSON data or dataset is empty.");
            return new List<CongruenceQuestionData>();
        }

        List<CongruenceQuestionData> pool = new List<CongruenceQuestionData>(parsedData.questions);
        List<CongruenceQuestionData> selectedQuestions = new List<CongruenceQuestionData>();

        // Shuffle and pick requested count safely
        for (int i = 0; i < pool.Count; i++)
        {
            CongruenceQuestionData temp = pool[i];
            int randomIndex = Random.Range(i, pool.Count);
            pool[i] = pool[randomIndex];
            pool[randomIndex] = temp;
        }

        int targetCount = Mathf.Min(count, pool.Count);
        for (int i = 0; i < targetCount; i++)
        {
            selectedQuestions.Add(pool[i]);
        }

        return selectedQuestions;
    }
}