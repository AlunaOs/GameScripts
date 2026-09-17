using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class DialoguePuzzleManager : MonoBehaviour
{
    // A custom structure that bundles a text line with its specific movement sprite expression
    [System.Serializable]
    public struct DialogueData
    {
        [TextArea(2, 4)]
        public string textLine;
        public Sprite movementSprite; // Assign Movement1, Movement2, etc. here per line
    }

    [Header("Player & Proximity Setup")]
    public Transform player;
    public GameObject openButtonUI;       // Mobile HUD interact prompt button
    public GameObject dialogueCanvasPanel; // Your 'pnl_Dialogue_Root' or Canvas object itself
    public GameObject mobileControlsCanvas; // Drag your Player HUD/Joystick Canvas here

    [Header("UI Component Links")]
    public Image imgCharacterAvatar;           // Drag your img_Character_Avatar here
    public TextMeshProUGUI txtDialogueDisplay; // Text inside the orange bubble
    public Button btnNext;                     // Next button (bottom right)
    public Button btnPrevious;                 // Previous button (bottom left)
    public Button btnSkip;                     // Skip button to complete dialogue instantly
    public TextMeshProUGUI txtNextBtnLabel;    // Text child inside next button

    [Header("Dialogue Content Configuration")]
    public DialogueData[] dialogueSequence; // Expandable custom list in the Inspector

    [Header("Text Typing Performance")]
    [Tooltip("Time delay in seconds between each individual character appearing.")]
    public float typingSpeed = 0.04f;

    [Header("Distance Configurations")]
    public float maxInteractionDistance = 6.0f;
    public float autoCloseDistance = 8.0f;

    [Header("3D Obstacle Mechanics")]
    public Transform worldWall;            // The 3D wall to lift
    public float wallLiftHeight = 5.0f;
    public float wallLiftSpeed = 2.0f;

    [HideInInspector]
    public bool isCompleted = false;

    private int currentLineIndex = 0;
    private Coroutine typingCoroutine;
    private bool canClickNext = false;

    // Unique key to identify this specific dialogue completion in PlayerPrefs
    private string saveKey;

    void Start()
    {
        saveKey = "DialogueCompleted_" + gameObject.name;

        if (dialogueCanvasPanel != null) dialogueCanvasPanel.SetActive(false);
        if (openButtonUI != null) openButtonUI.SetActive(false);

        if (btnNext != null) btnNext.onClick.AddListener(HandleNextClick);
        if (btnPrevious != null) btnPrevious.onClick.AddListener(HandlePreviousClick);
        if (btnSkip != null) btnSkip.onClick.AddListener(HandleSkipClick);

        // Read updated category from GameManager or saved PlayerPrefs key
        string currentCategory = (GameManager.Instance != null)
            ? GameManager.Instance.currentCategory
            : PlayerPrefs.GetString("Game_Category", "Algebra");

        Debug.Log($"[Dialogue] Active category for dialogue: {currentCategory}");

        LoadDialogueState();
    }

    void Update()
    {
        if (player == null || isCompleted) return;

        // Track real-time distance between this NPC/Trigger point and the player
        float distance = Vector3.Distance(transform.position, player.position);

        if (distance <= maxInteractionDistance)
        {
            if (!dialogueCanvasPanel.activeSelf)
            {
                openButtonUI.SetActive(true);
            }
        }
        else
        {
            openButtonUI.SetActive(false);

            // Close automatically if player walks away mid-conversation
            if (distance > autoCloseDistance && dialogueCanvasPanel.activeSelf)
            {
                CloseDialogueWindow();
            }
        }
    }

    public void OpenDialogueSystem()
    {
        if (isCompleted) return;

        if (openButtonUI != null) openButtonUI.SetActive(false);
        if (dialogueCanvasPanel != null) dialogueCanvasPanel.SetActive(true);

        // Hide mobile movement controls when dialogue opens
        if (mobileControlsCanvas != null) mobileControlsCanvas.SetActive(false);

        currentLineIndex = 0;
        UpdateDialogueUI();
    }

    void UpdateDialogueUI()
    {
        if (dialogueSequence.Length == 0) return;

        // Stop any currently running typing routine before starting a new line
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
        }

        // Dynamically swap the Avatar Sprite image based on the current line configuration
        if (imgCharacterAvatar != null && dialogueSequence[currentLineIndex].movementSprite != null)
        {
            imgCharacterAvatar.sprite = dialogueSequence[currentLineIndex].movementSprite;
        }

        // Toggle Previous Button visibility state
        if (btnPrevious != null)
        {
            btnPrevious.gameObject.SetActive(currentLineIndex > 0);
        }

        // Setup the Next Button label safely
        if (txtNextBtnLabel != null)
        {
            if (currentLineIndex == dialogueSequence.Length - 1)
                txtNextBtnLabel.text = "OK";
            else
                txtNextBtnLabel.text = "NEXT";
        }

        // Start typing effect routine asynchronously
        typingCoroutine = StartCoroutine(TypeTextRoutine(dialogueSequence[currentLineIndex].textLine));
    }

    IEnumerator TypeTextRoutine(string fullText)
    {
        txtDialogueDisplay.text = "";
        canClickNext = false;

        // Force Next button non-interactive immediately
        if (btnNext != null) btnNext.interactable = false;

        int totalCharacters = fullText.Length;
        int targetThreshold80Percent = Mathf.CeilToInt(totalCharacters * 0.8f);

        for (int i = 0; i < totalCharacters; i++)
        {
            txtDialogueDisplay.text += fullText[i];

            // Once the text string output crosses 80% progression rule check
            if (i >= targetThreshold80Percent && !canClickNext)
            {
                canClickNext = true;
                if (btnNext != null) btnNext.interactable = true; // Unlock interaction cleanly!
            }

            yield return new WaitForSeconds(typingSpeed);
        }

        // Double-check rule validation at the tail end execution blocks
        canClickNext = true;
        if (btnNext != null) btnNext.interactable = true;
    }

    void HandleNextClick()
    {
        // Guard clause ensuring no rapid execution bypassing threshold rules
        if (!canClickNext) return;

        if (currentLineIndex < dialogueSequence.Length - 1)
        {
            currentLineIndex++;
            UpdateDialogueUI();
        }
        else
        {
            CompleteDialogueSequence();
        }
    }

    void HandlePreviousClick()
    {
        if (currentLineIndex > 0)
        {
            currentLineIndex--;
            UpdateDialogueUI();
        }
    }

    // Handles skipping the whole dialogue instantly
    void HandleSkipClick()
    {
        CompleteDialogueSequence();
    }

    private void CompleteDialogueSequence()
    {
        if (typingCoroutine != null) StopCoroutine(typingCoroutine);

        isCompleted = true;
        SaveDialogueState(); 
        CloseDialogueWindow(); 
        StartCoroutine(LiftWallRoutine());
    }

    IEnumerator LiftWallRoutine()
    {
        yield return new WaitForSeconds(1.5f); // 1.5 second delay after closing before lift begins

        Vector3 startPos = worldWall.position;
        Vector3 targetPos = startPos + new Vector3(0, wallLiftHeight, 0);

        while (Vector3.Distance(worldWall.position, targetPos) > 0.01f)
        {
            worldWall.position = Vector3.MoveTowards(worldWall.position, targetPos, wallLiftSpeed * Time.deltaTime);
            yield return null;
        }
        worldWall.position = targetPos;
    }

    public void ResetDialoguePuzzleState()
    {
        // 1. Delete this specific NPC's save key (just in case)
        PlayerPrefs.DeleteKey(saveKey);

        // 2. Set the status back to incomplete
        isCompleted = false;

        // 3. Lower the wall back to its original starting position
        if (worldWall != null)
        {
            Vector3 groundPosition = worldWall.position - new Vector3(0, wallLiftHeight, 0);
            worldWall.position = groundPosition;
        }

        // 4. Reset HUD interactions
        if (openButtonUI != null) openButtonUI.SetActive(false);
        if (dialogueCanvasPanel != null) dialogueCanvasPanel.SetActive(false);

        Debug.Log($"[Dialogue System] {gameObject.name} dialogue and wall have been reset to default!");
    }

    public void CloseDialogueWindow()
    {
        if (typingCoroutine != null) StopCoroutine(typingCoroutine);
        if (dialogueCanvasPanel != null) dialogueCanvasPanel.SetActive(false);

        // Restore mobile movement controls when dialogue window shuts
        if (mobileControlsCanvas != null) mobileControlsCanvas.SetActive(true);
    }

    #region Save and Load Systems

    private void SaveDialogueState()
    {
        PlayerPrefs.SetInt(saveKey, 1); // 1 means completed
        PlayerPrefs.Save();
        Debug.Log($"[Dialogue System] Dialogue for {gameObject.name} saved as COMPLETED.");
    }

    private void LoadDialogueState()
    {
        if (PlayerPrefs.HasKey(saveKey) && PlayerPrefs.GetInt(saveKey) == 1)
        {
            isCompleted = true;

            // Make sure the open HUD interaction button stays permanently hidden
            if (openButtonUI != null) openButtonUI.SetActive(false);

            // Instantly snap the wall to its raised position so the player doesn't have to wait!
            if (worldWall != null)
            {
                Vector3 raisedPosition = worldWall.position + new Vector3(0, wallLiftHeight, 0);
                worldWall.position = raisedPosition;
                Debug.Log($"[Dialogue System] Dialogue previously completed. Wall snapped into the air.");
            }
        }
    }

    #endregion
}