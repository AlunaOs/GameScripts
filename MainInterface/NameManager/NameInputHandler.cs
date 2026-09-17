using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System;

public class NameInputHandler : MonoBehaviour
{
    [Header("UI References")]
    public TMP_Text playerNameDisplay;
    public TMP_Text bottomNameDisplay;
    public GameObject nameInputPopup;
    public TMP_Text accountTelemetryNameDisplay;
    public TMP_InputField nameInputField;
    public Button submitButton;
    public Button closePopupButton;
    public TMP_Text errorMessageText;
    public GameObject errorMessagePanel;

    [Header("Mobile Keyboard Adjustments")]
    public RectTransform popupPanelRect; // Drag your PopupPanel RectTransform here
    public float raisedPosY = 220f;      // How high to shift the panel up when keyboard opens
    private Vector2 originalPanelPos;

    [Header("Confirmation Popup")]
    public GameObject confirmationPopup;
    public TMP_Text confirmationText;
    public Button confirmYesButton;
    public Button confirmNoButton;

    [Header("Reset Progress Button (optional)")]
    public Button resetProgressButton;

    [Header("Cooldown Settings")]
    public int cooldownDays = 3;
    public string errorMessage = "You can change your name once every 3 days!";

    private const string PLAYER_NAME_KEY = "PlayerName";
    private const string LAST_CHANGE_KEY = "LastNameChangeTicks";
    private string pendingName = "";
    private GameManager gameManager;
    private Action pendingAction;

    void Start()
    {
        gameManager = FindFirstObjectByType<GameManager>();

        if (popupPanelRect != null)
        {
            originalPanelPos = popupPanelRect.anchoredPosition;
        }

        // Setup input field listeners for mobile keyboard auto-shift
        if (nameInputField != null)
        {
            nameInputField.onSelect.AddListener((val) => OnInputFieldSelected());
            nameInputField.onDeselect.AddListener((val) => OnInputFieldDeselected());
            nameInputField.onEndEdit.AddListener((val) => OnInputFieldDeselected());
        }

        // Force confirmation popup size & anchors
        if (confirmationPopup != null)
        {
            RectTransform rt = confirmationPopup.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(500, 250);
                rt.anchoredPosition = Vector2.zero;
            }
        }

        string savedName = PlayerPrefs.GetString(PLAYER_NAME_KEY, "Player");
        UpdateAllNameDisplays(savedName);

        if (nameInputPopup != null) nameInputPopup.SetActive(false);
        if (confirmationPopup != null) confirmationPopup.SetActive(false);
        if (errorMessagePanel != null) errorMessagePanel.SetActive(false);

        if (submitButton != null) submitButton.onClick.AddListener(OnSubmitClicked);
        if (closePopupButton != null) closePopupButton.onClick.AddListener(ClosePopup);
        if (confirmYesButton != null) confirmYesButton.onClick.AddListener(OnConfirmYes);
        if (confirmNoButton != null) confirmNoButton.onClick.AddListener(OnConfirmNo);
        if (resetProgressButton != null) resetProgressButton.onClick.AddListener(OnResetClicked);
    }

    void Update()
    {
        // Reset panel position if player dismisses touch keyboard manually
        if (TouchScreenKeyboard.visible == false && popupPanelRect != null && popupPanelRect.anchoredPosition.y != originalPanelPos.y)
        {
            popupPanelRect.anchoredPosition = originalPanelPos;
        }
    }

    private void OnInputFieldSelected()
    {
        if (popupPanelRect != null)
        {
            popupPanelRect.anchoredPosition = new Vector2(originalPanelPos.x, raisedPosY);
        }
    }

    private void OnInputFieldDeselected()
    {
        if (popupPanelRect != null)
        {
            popupPanelRect.anchoredPosition = originalPanelPos;
        }
    }

    void OnEnable()
    {
        string currentName = PlayerPrefs.GetString(PLAYER_NAME_KEY, "Player");
        if (accountTelemetryNameDisplay != null)
        {
            accountTelemetryNameDisplay.text = currentName;
        }
    }

    public void OpenNameInputPopup()
    {
        if (!CanChangeName())
        {
            ShowErrorMessage();
            return;
        }

        nameInputField.text = playerNameDisplay.text;
        nameInputPopup.SetActive(true);
        confirmationPopup.SetActive(false);
        if (errorMessagePanel != null) errorMessagePanel.SetActive(false);
    }

    private bool CanChangeName()
    {
        if (!PlayerPrefs.HasKey(LAST_CHANGE_KEY))
            return true;

        long lastChangeTicks = long.Parse(PlayerPrefs.GetString(LAST_CHANGE_KEY));
        DateTime lastChange = new DateTime(lastChangeTicks);
        DateTime now = DateTime.UtcNow;
        return (now - lastChange).TotalDays >= cooldownDays;
    }

    private void ShowErrorMessage()
    {
        if (errorMessagePanel == null) return;

        long lastChangeTicks = long.Parse(PlayerPrefs.GetString(LAST_CHANGE_KEY));
        DateTime lastChange = new DateTime(lastChangeTicks);
        DateTime now = DateTime.UtcNow;
        DateTime nextAllowed = lastChange.AddDays(cooldownDays);
        TimeSpan remaining = nextAllowed - now;

        int days = Mathf.Max(0, remaining.Days);
        int hours = remaining.Hours;
        int mins = remaining.Minutes;

        if (errorMessageText != null)
            errorMessageText.text = $"{errorMessage}\nWait {days}d {hours}h {mins}m";

        errorMessagePanel.SetActive(true);
        Invoke(nameof(HideErrorMessage), 3f);
    }

    private void HideErrorMessage()
    {
        if (errorMessagePanel != null) errorMessagePanel.SetActive(false);
    }

    void ClosePopup()
    {
        OnInputFieldDeselected();
        if (nameInputPopup != null) nameInputPopup.SetActive(false);
        if (confirmationPopup != null) confirmationPopup.SetActive(false);
    }

    void OnSubmitClicked()
    {
        pendingName = nameInputField.text.Trim();
        if (string.IsNullOrEmpty(pendingName)) return;

        confirmationText.text = $"Are you sure you want to be named\n\"{pendingName}\"?";
        pendingAction = SaveNewName;
        confirmationPopup.SetActive(true);
        confirmationPopup.transform.SetAsLastSibling();
    }

    private void SaveNewName()
    {
        PlayerPrefs.SetString(PLAYER_NAME_KEY, pendingName);
        PlayerPrefs.SetString(LAST_CHANGE_KEY, DateTime.UtcNow.Ticks.ToString());
        PlayerPrefs.Save();
        UpdateAllNameDisplays(pendingName);
        ClosePopup();
    }

    public void OnResetClicked()
    {
        if (confirmationPopup == null) return;
        confirmationText.text = "⚠️ RESET EVERYTHING ⚠️\n\nAll game progress will be permanently deleted.\nThis cannot be undone.\n\nAre you sure?";
        pendingAction = PerformReset;
        confirmationPopup.SetActive(true);
        confirmationPopup.transform.SetAsLastSibling();
    }

    private void PerformReset()
    {
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();

        if (gameManager == null)
            gameManager = FindFirstObjectByType<GameManager>();
        if (gameManager != null)
            gameManager.ResetProgress();

        ResetNameDisplayToDefault();

        DecodeQuizPuzzleManager quiz = FindFirstObjectByType<DecodeQuizPuzzleManager>();
        if (quiz != null && quiz.gameObject.activeSelf)
            quiz.CloseQuiz();

        ClosePopup();
        HideErrorMessage();
    }

    public void ResetNameDisplayToDefault()
    {
        string defaultName = "Player";
        UpdateAllNameDisplays(defaultName);
        if (playerNameDisplay != null) playerNameDisplay.text = defaultName;
        if (bottomNameDisplay != null) bottomNameDisplay.text = defaultName;
        if (accountTelemetryNameDisplay != null) accountTelemetryNameDisplay.text = defaultName;
    }

    public void OnConfirmYes()
    {
        if (pendingAction != null)
        {
            pendingAction.Invoke();
            pendingAction = null;
        }
        confirmationPopup.SetActive(false);
    }

    public void OnConfirmNo()
    {
        pendingAction = null;
        confirmationPopup.SetActive(false);
    }

    private void UpdateAllNameDisplays(string name)
    {
        if (playerNameDisplay != null) playerNameDisplay.text = name;
        if (bottomNameDisplay != null) bottomNameDisplay.text = name;
        if (accountTelemetryNameDisplay != null) accountTelemetryNameDisplay.text = name;
    }
}