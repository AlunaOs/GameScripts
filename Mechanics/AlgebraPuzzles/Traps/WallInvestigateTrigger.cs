using UnityEngine;

public class WallInvestigateTrigger : MonoBehaviour
{
    [Header("References")]
    public Transform player;
    public GameObject investigateButtonUI;
    public WallNotificationPanel notificationPanel;

    [Header("Distance Settings")]
    public float maxInteractionDistance = 6.0f;

    private AutomaticMovingWall movingWall;
    private bool isActivated = false;
    private Coroutine activeNotificationCoroutine;

    void Start()
    {
        movingWall = GetComponent<AutomaticMovingWall>();
        if (investigateButtonUI != null) investigateButtonUI.SetActive(false);
    }

    void Update()
    {
        if (player == null || isActivated) return;

        float distance = Vector3.Distance(transform.position, player.position);

        if (investigateButtonUI != null)
        {
            investigateButtonUI.SetActive(distance <= maxInteractionDistance);
        }
    }

    public void OnInvestigateClicked()
    {
        if (isActivated || notificationPanel == null) return;

        // Stop existing animation on this active object if clicked multiple times
        if (activeNotificationCoroutine != null)
        {
            StopCoroutine(activeNotificationCoroutine);
        }

        // Start coroutine on THIS object (Wall) which is active in scene
        activeNotificationCoroutine = StartCoroutine(
            notificationPanel.AnimateNotificationRoutine("Wall can't be moved, activate lever first.")
        );
    }

    public void OnLeverActivated()
    {
        isActivated = true;

        if (investigateButtonUI != null) investigateButtonUI.SetActive(false);
        if (movingWall != null) movingWall.ActivateWallMovement();
    }
}