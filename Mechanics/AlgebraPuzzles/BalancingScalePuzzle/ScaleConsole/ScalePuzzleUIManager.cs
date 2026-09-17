using UnityEngine;

public class ScalePuzzleUIManager : MonoBehaviour
{
    [Header("UI References")]
    public GameObject puzzleCanvas;
    public GameObject scale3DSetup; // Parent containing ScaleCamera and Scale 3D Model

    [Header("Puzzle Settings")]
    public ScalePuzzleManager scaleLogicManager;

    public void OpenScalePuzzle()
    {
        if (scale3DSetup != null) scale3DSetup.SetActive(true);
        if (puzzleCanvas != null) puzzleCanvas.SetActive(true);

        if (scaleLogicManager != null)
        {
            scaleLogicManager.StartPuzzle();
        }
    }
    public void CloseScalePuzzle()
    {
        if (puzzleCanvas != null) puzzleCanvas.SetActive(false);
        if (scale3DSetup != null) scale3DSetup.SetActive(false);
    }
}