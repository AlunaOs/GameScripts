using UnityEngine;

public class LeverWorldTrigger : MonoBehaviour
{
    [Header("Player & Distance Settings")]
    public Transform player;
    public float interactionDistance = 5.0f;

    [Header("UI Canvas & Interact Button")]
    [Tooltip("The floating/screen Interact Button (e.g., 'Press to Open')")]
    public GameObject interactButtonUI;

    [Tooltip("The full Lever Puzzle UI Panel (containing your RawImage and 3D View)")]
    public GameObject leverPuzzlePanel;

    [Header("Player Kit / Controls UI Canvas")]
    [Tooltip("Drag your Player Kit Canvas / Movement Controls UI here so it hides when puzzle is opened and reappears when closed.")]
    public GameObject playerKitCanvas;

    private bool isPlayerNear = false;

    void Update()
    {
        if (player == null) return;

        // Calculate distance between Player and this spawned Lever
        float distance = Vector3.Distance(transform.position, player.position);

        if (distance <= interactionDistance)
        {
            if (!isPlayerNear)
            {
                isPlayerNear = true;
                
                // Show Interact Button only if the puzzle panel isn't already active
                if (interactButtonUI != null && (leverPuzzlePanel == null || !leverPuzzlePanel.activeSelf))
                {
                    interactButtonUI.SetActive(true);
                }
            }
        }
        else
        {
            if (isPlayerNear)
            {
                isPlayerNear = false;

                // Hide Interact Button when walking away
                if (interactButtonUI != null)
                {
                    interactButtonUI.SetActive(false);
                }

                // If player walks away, close puzzle and restore player kit canvas
                if (leverPuzzlePanel != null && leverPuzzlePanel.activeSelf)
                {
                    CloseLeverPuzzleUI();
                }
            }
        }
    }

    public void OpenLeverPuzzleUI()
    {
        // Hide the world interact button
        if (interactButtonUI != null)
            interactButtonUI.SetActive(false);

        // Show the puzzle UI panel
        if (leverPuzzlePanel != null)
            leverPuzzlePanel.SetActive(true);

        // Hide the Player Kit Canvas
        if (playerKitCanvas != null)
            playerKitCanvas.SetActive(false);
    }

    public void CloseLeverPuzzleUI()
    {
        // Hide the puzzle UI panel
        if (leverPuzzlePanel != null)
            leverPuzzlePanel.SetActive(false);

        // Show the Player Kit Canvas again
        if (playerKitCanvas != null)
            playerKitCanvas.SetActive(true);

        // Restore interact button if player is still nearby
        if (isPlayerNear && interactButtonUI != null)
            interactButtonUI.SetActive(true);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactionDistance);
    }
}