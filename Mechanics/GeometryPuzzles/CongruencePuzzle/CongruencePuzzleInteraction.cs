using UnityEngine;

public class CongruencePuzzleTrigger : MonoBehaviour
{
    [Header("Player & HUD Links")]
    [Tooltip("Drag FirstPersonController here.")]
    public Transform player;
    [Tooltip("Drag Hud_Button (GameObject container) here.")]
    public GameObject openButtonUI;
    [Tooltip("Drag UI_CongruencePuzzle canvas panel here.")]
    public GameObject puzzleCanvas;

    [Header("Distance Settings")]
    public float maxInteractionDistance = 6.0f;
    public float autoCloseDistance = 8.0f;

    private CongruencePuzzleManager puzzleManager;

    private void Start()
    {
        if (puzzleCanvas != null)
        {
            puzzleManager = puzzleCanvas.GetComponent<CongruencePuzzleManager>();
            puzzleCanvas.SetActive(false);

            if (puzzleManager == null)
            {
                Debug.LogError($"[CongruencePuzzleTrigger] No CongruencePuzzleManager found on '{puzzleCanvas.name}'. " +
                                "The manager script must be on the same GameObject assigned to 'puzzleCanvas'. " +
                                "The interact button will not function correctly until this is fixed.", this);
            }
        }
        else
        {
            Debug.LogError("[CongruencePuzzleTrigger] 'puzzleCanvas' is not assigned in the Inspector.", this);
        }

        if (openButtonUI == null)
        {
            Debug.LogError("[CongruencePuzzleTrigger] 'openButtonUI' is not assigned in the Inspector.", this);
        }
        else
        {
            openButtonUI.SetActive(false);
        }

        if (player == null)
        {
            Debug.LogError("[CongruencePuzzleTrigger] 'player' is not assigned in the Inspector.", this);
        }
    }

    private void Update()
    {
        // Only distance/player are strictly required to show the button.
        // A missing puzzleManager should not silently block the HUD prompt.
        if (player == null || openButtonUI == null) return;

        // If the puzzle is already completed, keep the button hidden and skip everything else.
        if (puzzleManager != null && puzzleManager.IsCompleted)
        {
            if (openButtonUI.activeSelf)
            {
                openButtonUI.SetActive(false);
            }
            return;
        }

        float distance = Vector3.Distance(transform.position, player.position);
        bool canvasOpen = puzzleCanvas != null && puzzleCanvas.activeSelf;

        if (distance <= maxInteractionDistance)
        {
            if (!canvasOpen && !openButtonUI.activeSelf)
            {
                openButtonUI.SetActive(true);
            }
        }
        else
        {
            if (openButtonUI.activeSelf)
            {
                openButtonUI.SetActive(false);
            }

            if (distance > autoCloseDistance && canvasOpen)
            {
                puzzleManager?.ClosePuzzleManually();
            }
        }
    }

    public void OpenPuzzle()
    {
        if (puzzleManager != null && puzzleManager.IsCompleted) return;

        if (openButtonUI != null) openButtonUI.SetActive(false);
        if (puzzleCanvas != null) puzzleCanvas.SetActive(true);

        if (puzzleManager != null)
        {
            puzzleManager.OpenPuzzleSession();
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, maxInteractionDistance);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, autoCloseDistance);
    }
}