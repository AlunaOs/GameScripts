using UnityEngine;

public class ScalePuzzleTrigger : MonoBehaviour
{
    [Header("Player & HUD Links")]
    [Tooltip("Drag your Player character object here from the hierarchy.")]
    public Transform player;
    [Tooltip("Drag your mobile HUD interactive prompt button/panel here.")]
    public GameObject interactButtonUI;
    [Tooltip("Drag your master BalancingScalePuzzle Canvas panel here.")]
    public GameObject puzzleCanvasPanel;

    [Header("Distance Settings")]
    public float maxInteractionDistance = 6.0f;
    public float autoCloseDistance = 8.0f;

    private ScalePuzzleManager puzzleManager;

    void Start()
    {
        if (puzzleCanvasPanel != null)
        {
            puzzleManager = puzzleCanvasPanel.GetComponent<ScalePuzzleManager>();
            puzzleCanvasPanel.SetActive(false);
        }

        if (interactButtonUI != null)
        {
            interactButtonUI.SetActive(false);
        }
    }

    void Update()
    {
        if (player == null || puzzleManager == null) return;

        if (puzzleManager.isCompleted)
        {
            if (interactButtonUI != null && interactButtonUI.activeSelf) 
                interactButtonUI.SetActive(false);
            return;
        }

        float distance = Vector3.Distance(transform.position, player.position);

        if (distance <= maxInteractionDistance)
        {
            if (!puzzleCanvasPanel.activeSelf)
            {
                interactButtonUI.SetActive(true);
            }
        }
        else
        {
            if (interactButtonUI != null) interactButtonUI.SetActive(false);

            if (distance > autoCloseDistance && puzzleCanvasPanel.activeSelf)
            {
                ClosePuzzleSystem();
            }
        }
    }

    public void OpenPuzzle()
    {
        if (puzzleManager != null && puzzleManager.isCompleted) return;

        ExecuteOpenPuzzle();
    }

    private void ExecuteOpenPuzzle()
    {
        if (interactButtonUI != null) interactButtonUI.SetActive(false);
        if (puzzleCanvasPanel != null) puzzleCanvasPanel.SetActive(true);

        if (puzzleManager != null)
        {
            puzzleManager.StartPuzzle();
        }
    }

    private void ClosePuzzleSystem()
    {
        if (puzzleCanvasPanel != null) puzzleCanvasPanel.SetActive(false);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, maxInteractionDistance);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, autoCloseDistance);
    }
}