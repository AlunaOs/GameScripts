using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class DungeonContentManager : MonoBehaviour
{
    [System.Serializable]
    public class DungeonData
    {
        public string dungeonName;
        public PuzzleCategory category;
        public string sceneName;
        public Sprite dungeonImage;
        [TextArea(3, 10)] public string description;
        public Sprite currencyRewardSprite;
        public Sprite expRewardSprite;
    }

    public List<DungeonData> dungeons = new List<DungeonData>();

    [Header("UI References")]
    public GameObject contentPanel;
    public CanvasGroup contentCanvasGroup;
    public Image dungeonImage;
    public TMP_Text descriptionText;

    [Header("Rewards Area")]
    public TMP_Text rewardsLabel;
    public Image currencyRewardImage;
    public Image expRewardImage;

    public Button startButton;

    [Header("Loading System Link")]
    public MenuManager menuManager;

    [Header("Animation Settings")]
    public float fadeDuration = 0.2f;

    [Header("Dungeon Select Buttons")]
    [Tooltip("Drag Algebra Button, Geometry Button, etc. here in the same order as 'dungeons'. " +
             "Leave empty if you're wiring OnClick() manually in the Inspector instead - " +
             "this array only exists to defensively re-validate their raycast/interactable state.")]
    public List<Button> dungeonSelectButtons = new List<Button>();

    private int currentDungeonIndex = -1;
    private Coroutine currentFadeCoroutine;

    void Start()
    {
        if (startButton != null)
        {
            startButton.onClick.RemoveAllListeners();
            startButton.onClick.AddListener(OnStartButtonClicked);
        }

        for (int i = 0; i < dungeonSelectButtons.Count; i++)
        {
            Button b = dungeonSelectButtons[i];
            if (b == null) continue;
            b.interactable = true;
            Image img = b.GetComponent<Image>();
            if (img != null) img.raycastTarget = true;
        }

        if (contentPanel != null)
        {
            contentPanel.SetActive(false);

            EnsurePanelCanvasGroup();
            contentCanvasGroup.blocksRaycasts = false;
        }
    }

    private void EnsurePanelCanvasGroup()
    {
        if (contentCanvasGroup == null)
        {
            contentCanvasGroup = contentPanel.GetComponent<CanvasGroup>();
            if (contentCanvasGroup == null)
                contentCanvasGroup = contentPanel.AddComponent<CanvasGroup>();
        }
    }

    public void SelectDungeon(int index)
    {
        if (index < 0 || index >= dungeons.Count) return;

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);

        for (int i = 0; i < dungeonSelectButtons.Count; i++)
        {
            Button b = dungeonSelectButtons[i];
            if (b == null) continue;
            b.interactable = true;
        }

        currentDungeonIndex = index;
        DungeonData data = dungeons[index];

        // 1. Update text, images, and rewards
        if (dungeonImage != null) dungeonImage.sprite = data.dungeonImage;
        if (descriptionText != null) descriptionText.text = data.description;
        if (currencyRewardImage != null) currencyRewardImage.sprite = data.currencyRewardSprite;
        if (expRewardImage != null) expRewardImage.sprite = data.expRewardSprite;

        // 2. Refresh & fade in content panel
        if (contentPanel != null)
        {
            contentPanel.SetActive(true);

            if (currentFadeCoroutine != null)
            {
                StopCoroutine(currentFadeCoroutine);
            }
            currentFadeCoroutine = StartCoroutine(FadeInContent());
        }
    }

    private IEnumerator FadeInContent()
    {
        EnsurePanelCanvasGroup();

        contentCanvasGroup.alpha = 0f;
        contentCanvasGroup.blocksRaycasts = true;
        contentCanvasGroup.interactable = true;

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            contentCanvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsed / fadeDuration);
            yield return null;
        }
        contentCanvasGroup.alpha = 1f;
    }

    private void OnStartButtonClicked()
    {
        DungeonData currentDungeon = GetCurrentDungeon();
        if (currentDungeon == null) return;

        string savedName = PlayerPrefs.GetString("PlayerName", "");
        if (string.IsNullOrEmpty(savedName))
        {
            if (menuManager != null)
            {
                menuManager.ShowNameWarning();
            }
            return;
        }

        if (startButton != null) startButton.interactable = false;

        // 1. Pass the category name directly as a string
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetCategory(currentDungeon.category.ToString());
        }

        // 2. Load the stage scene
        if (!string.IsNullOrEmpty(currentDungeon.sceneName))
        {
            if (menuManager != null)
            {
                menuManager.LoadSceneWithLoadingScreen(currentDungeon.sceneName);
            }
            else
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(currentDungeon.sceneName);
            }
        }
    }

    public DungeonData GetCurrentDungeon()
    {
        if (currentDungeonIndex >= 0 && currentDungeonIndex < dungeons.Count)
            return dungeons[currentDungeonIndex];
        return null;
    }
}