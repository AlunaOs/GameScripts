using UnityEngine;

public class ObstacleTrigger : MonoBehaviour
{
    public Transform player; 
    public GameObject openButtonUI;  
    public GameObject puzzleCanvas;   
    
    [Header("Distance Settings")] 
    public float maxInteractionDistance = 6.0f; 
    public float autoCloseDistance = 8.0f;      

    private PuzzleManager puzzleManager; 

    void Start() 
    {
        if (puzzleCanvas != null) 
        {
            puzzleManager = puzzleCanvas.GetComponent<PuzzleManager>(); 
        }

        if (puzzleCanvas != null) 
        {
            puzzleCanvas.SetActive(false); 
        }
    }

    void Update() 
    {
        if (player == null || puzzleManager == null) return; 

        if (puzzleManager.isCleared)
        {
            if (openButtonUI.activeSelf) openButtonUI.SetActive(false); 
            return; //
        }

        float distance = Vector3.Distance(transform.position, player.position); 

        if (distance <= maxInteractionDistance) 
        {
            if (!puzzleCanvas.activeSelf) 
            {
                openButtonUI.SetActive(true); 
            }
        }
        else 
        {
            openButtonUI.SetActive(false); 

            if (distance > autoCloseDistance && puzzleCanvas.activeSelf) 
            {
                puzzleManager.ClosePuzzleManually();
            }
        }
    }

    public void OpenPuzzle() 
    {
        if (puzzleManager != null && puzzleManager.isCleared) return; 

        openButtonUI.SetActive(false); 
        puzzleCanvas.SetActive(true); 

        if (puzzleManager != null) 
        {
            puzzleManager.StartPuzzleSystem(); 
        }
    }

    void OnDrawGizmosSelected() 
    {
        Gizmos.color = Color.green; 
        Gizmos.DrawWireSphere(transform.position, maxInteractionDistance); 
        Gizmos.color = Color.red; 
        Gizmos.DrawWireSphere(transform.position, autoCloseDistance); 
    }
}