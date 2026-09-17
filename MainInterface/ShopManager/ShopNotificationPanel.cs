using System.Collections;
using UnityEngine;
using TMPro;

public class ShopNotificationPanel : MonoBehaviour
{
    public static ShopNotificationPanel Instance { get; private set; }

    [Header("UI Component References")]
    [SerializeField] private GameObject notificationPanel; 
    [SerializeField] private TextMeshProUGUI messageText;   

    [Header("Display Settings")]
    [SerializeField] private float displayDuration = 2.0f;

    private Coroutine activeNoticeCoroutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Ensure the notification panel is hidden when the game starts
        if (notificationPanel != null)
        {
            notificationPanel.SetActive(false);
        }
    }

public void ShowMessage(string message)
{
    if (notificationPanel == null || messageText == null) return;

    messageText.text = message;

    // Turn the panel on FIRST so it is active when the coroutine starts
    notificationPanel.SetActive(true);

    if (activeNoticeCoroutine != null)
    {
        StopCoroutine(activeNoticeCoroutine);
    }

    activeNoticeCoroutine = StartCoroutine(DisplayRoutine());
}

private IEnumerator DisplayRoutine()
{
    // Panel is already active here
    
    yield return new WaitForSeconds(displayDuration);
    
    // Turn the panel off after the duration finishes
    notificationPanel.SetActive(false);
}
}