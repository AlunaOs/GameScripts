using UnityEngine;
using TMPro;
using System.Collections;

public class DecodeQuizManager : MonoBehaviour
{
    public TMP_Text stageText;
    public TMP_Text questionText;
    public TMP_InputField inputField;
    public TMP_Text chanceText;
    public TMP_Text difficultyText;

    private int chances = 3;
    private DecodeQuestion currentQ;

    void OnEnable()
    {
        StartCoroutine(WaitForData());
    }

    IEnumerator WaitForData()
    {
        var manager = Object.FindAnyObjectByType<GameManager>();
        while (manager == null || !manager.isDataLoaded)
        {
            yield return null;
        }
        ResetStage();
    }

    void UpdateDifficultyUI()
    {
        var manager = Object.FindAnyObjectByType<GameManager>();
        if (manager == null) return;

        switch (manager.currentLevel)
        {
            case 1: difficultyText.text = "DIFFICULTY: <color=green>EASY</color>"; break;
            case 2: difficultyText.text = "DIFFICULTY: <color=yellow>MEDIUM</color>"; break;
            case 3: difficultyText.text = "DIFFICULTY: <color=red>HARD</color>"; break;
        }
    }

    void ResetStage()
    {
        var manager = Object.FindAnyObjectByType<GameManager>();
        chances = 3;
        
        if (manager != null)
        {
            // Convert legacy Question type to DecodeQuestion
            var oldQ = manager.GetQuestion();
            if (oldQ != null)
            {
                currentQ = new DecodeQuestion
                {
                    category = oldQ.category,
                    difficulty = oldQ.difficulty,
                    text = oldQ.text,
                    answer = oldQ.answer,
                    explanation = oldQ.explanation
                };
            }
            else
            {
                currentQ = null;
            }
        }

        if (currentQ != null)
        {
            stageText.text = manager != null ? "STAGE " + manager.currentLevel : "STAGE 1";
            questionText.text = currentQ.text;
            chanceText.text = "CHANCES: " + chances + "/3";
            inputField.text = "";
            inputField.ActivateInputField(); 
        }
        else
        {
            questionText.text = "No questions found for this difficulty.";
        }
        UpdateDifficultyUI();
    }

    public void SubmitAnswer()
    {
        if (string.IsNullOrWhiteSpace(inputField.text)) return;
        if (currentQ == null) return;

        string pAns = inputField.text.Trim().ToLower();
        string cAns = currentQ.answer.Trim().ToLower();
        var manager = Object.FindAnyObjectByType<GameManager>();

        if (pAns == cAns)
        {
            if (manager != null) manager.EvaluatePerformance(true);
            ResetStage();
        }
        else
        {
            chances--;

            if (chances <= 0)
            {
                if (manager != null) manager.HandleTotalFailure();
                ResetStage();
            }
            else
            {
                chanceText.text = "CHANCES: " + chances + "/3";
                inputField.text = "";
                inputField.ActivateInputField();
            }
        }
    }
}