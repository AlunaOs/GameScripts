using UnityEngine;

public class SimpleResetButton : MonoBehaviour
{
    public void ResetEverything()
    {
        // 1. Clear PlayerPrefs
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();

        // 2. Reset GameManager
        GameManager gm = FindFirstObjectByType<GameManager>();
        if (gm != null) gm.ResetProgress();

        // 3. Reset name displays
        NameInputHandler nameHandler = FindFirstObjectByType<NameInputHandler>();
        if (nameHandler != null) nameHandler.ResetNameDisplayToDefault();

        // 4. Close quiz if open
        DecodeQuizPuzzleManager quiz = FindFirstObjectByType<DecodeQuizPuzzleManager>();
        if (quiz != null && quiz.gameObject.activeSelf) quiz.CloseQuiz();

        Debug.Log("All progress has been reset.");
    }
}