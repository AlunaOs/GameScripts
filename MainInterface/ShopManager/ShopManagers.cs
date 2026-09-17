using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class ShopManagers : MonoBehaviour
{
    public static ShopManagers Instance { get; private set; }

    [Header("Currency Settings")]
    public int currentStars = 0;
    public TextMeshProUGUI txtStarCount;

    [Header("Inventory / Item States")]
    public bool hasHintScroll = false;
    public int expPotionCount = 0;

    [Header("Active Puzzle Connection")]
    public ScalePuzzleManager scalePuzzleManager;

    [Header("Item Detail Display UI")]
    public CanvasGroup itemDetailPanelCanvasGroup;
    public Image detailItemImage;
    public TextMeshProUGUI detailDescText;
    public TextMeshProUGUI detailPriceText;
    public ShopItem currentItem;
    public float fadeDuration = 0.3f;

    private Coroutine fadeCoroutine;

    void Awake()
    {
        // Ensure proper Singleton setup to prevent duplicate instances
        if (Instance != null && Instance != this)
        {
            return;
        }

        Instance = this;
        LoadPlayerStars();
    }

    void OnEnable()
    {
        if (Instance == null)
        {
            Instance = this;
        }

        // Always fetch the freshest saved total when enabled
        LoadPlayerStars();

        if (itemDetailPanelCanvasGroup != null)
        {
            itemDetailPanelCanvasGroup.alpha = 0f;
            itemDetailPanelCanvasGroup.blocksRaycasts = false;
        }
    }

    void Start()
    {
        UpdateStarUI();

        if (itemDetailPanelCanvasGroup != null)
        {
            itemDetailPanelCanvasGroup.alpha = 0f;
            itemDetailPanelCanvasGroup.blocksRaycasts = false;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  CURRENCY & INVENTORY MANAGEMENT
    // ─────────────────────────────────────────────────────────────────────────

    public void LoadPlayerStars()
    {
        // Always read directly from PlayerPrefs
        currentStars = PlayerPrefs.GetInt("PlayerStars", 2);
        expPotionCount = PlayerPrefs.GetInt("ExpPotionCount", 0);
        hasHintScroll = PlayerPrefs.GetInt("HasHintScroll", 0) == 1;

        UpdateStarUI();
    }

    public void AddStars(int amount)
    {
        // Read direct state
        currentStars = PlayerPrefs.GetInt("PlayerStars", 2);
        currentStars += amount;

        // Save state
        PlayerPrefs.SetInt("PlayerStars", currentStars);
        PlayerPrefs.Save();

        // Force immediate UI refresh
        UpdateStarUI();

        Debug.Log($"[ShopManagers] Stars added! New total: {currentStars}");
    }

    public void UpdateStarUI()
    {
        if (txtStarCount != null)
        {
            txtStarCount.text = currentStars.ToString();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  PURCHASING LOGIC
    // ─────────────────────────────────────────────────────────────────────────

    public void BuyCurrentItem()
    {
        if (currentItem == null || currentItem.itemData == null)
        {
            Debug.LogError("[ShopManagers] Cannot buy: currentItem or itemData is null!");
            return;
        }

        int itemPrice = currentItem.itemData.price;
        string rawName = currentItem.itemData.itemName ?? "";
        string gameObjectObjectName = currentItem.gameObject.name;

        string combinedSearchText = (rawName + " " + gameObjectObjectName).ToLower().Trim();

        bool isHintScroll = combinedSearchText.Contains("hint") || combinedSearchText.Contains("scroll");
        bool isExpPotion = combinedSearchText.Contains("exp") || combinedSearchText.Contains("potion");

        if (isHintScroll && hasHintScroll)
        {
            Debug.LogWarning("[ShopManagers] BLOCK PURCHASE: User already owns a Hint Scroll!");
            ShopNotificationPanel.Instance?.ShowMessage("You can only hold 1 Hint Scroll at a time!");
            return;
        }

        if (currentStars < itemPrice)
        {
            Debug.LogWarning("[ShopManagers] BLOCK PURCHASE: Not enough stars!");
            ShopNotificationPanel.Instance?.ShowMessage("Not enough stars to purchase!");
            return;
        }

        currentStars -= itemPrice;
        PlayerPrefs.SetInt("PlayerStars", currentStars);

        if (isHintScroll)
        {
            hasHintScroll = true;
            PlayerPrefs.SetInt("HasHintScroll", 1);

            bool unlockedAny = false;

            if (scalePuzzleManager != null)
            {
                scalePuzzleManager.EnableHintButton();
                unlockedAny = true;
            }

            ScalePuzzleManager[] allScalePuzzles =
                FindObjectsByType<ScalePuzzleManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (var puzzle in allScalePuzzles)
            {
                if (puzzle == scalePuzzleManager) continue;
                puzzle.EnableHintButton();
                unlockedAny = true;
            }

            if (!unlockedAny)
            {
                Debug.LogWarning("[ShopManagers] Bought Hint Scroll, but no active ScalePuzzleManager was found.");
            }
        }
        else if (isExpPotion)
        {
            expPotionCount++;
            PlayerPrefs.SetInt("ExpPotionCount", expPotionCount);
        }

        PlayerPrefs.Save();
        UpdateStarUI();

        ShopNotificationPanel.Instance?.ShowMessage($"Bought {rawName}!");
    }

    public void UseHintScroll()
    {
        hasHintScroll = false;
        PlayerPrefs.SetInt("HasHintScroll", 0);
        PlayerPrefs.Save();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  SHOP UI ANIMATION & DETAIL DISPLAY
    // ─────────────────────────────────────────────────────────────────────────

    public void ShowItemDetails(ShopItem item)
    {
        if (item == null || item.itemData == null) return;

        currentItem = item;

        if (detailItemImage != null)
        {
            detailItemImage.sprite = item.itemData.icon;
            detailItemImage.enabled = item.itemData.icon != null;
        }

        if (detailDescText != null)
            detailDescText.text = item.itemData.description;

        if (detailPriceText != null)
        {
            string starLabel = item.itemData.price == 1 ? "Star" : "Stars";
            detailPriceText.text = $"x{item.itemData.price} {starLabel}";
        }

        if (itemDetailPanelCanvasGroup != null)
        {
            if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
            fadeCoroutine = StartCoroutine(FadeCanvasGroup(itemDetailPanelCanvasGroup, itemDetailPanelCanvasGroup.alpha, 1f, fadeDuration, true));
        }
    }

    public void OnItemSelected(ShopItem item)
    {
        ShowItemDetails(item);
    }

    public void HideItemDetails()
    {
        if (itemDetailPanelCanvasGroup != null)
        {
            if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
            fadeCoroutine = StartCoroutine(FadeCanvasGroup(itemDetailPanelCanvasGroup, itemDetailPanelCanvasGroup.alpha, 0f, fadeDuration, false));
        }
    }

    private IEnumerator FadeCanvasGroup(CanvasGroup cg, float startAlpha, float endAlpha, float duration, bool interactable)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            cg.alpha = Mathf.Lerp(startAlpha, endAlpha, elapsed / duration);
            yield return null;
        }

        cg.alpha = endAlpha;
        cg.blocksRaycasts = interactable;
        cg.interactable = interactable;
    }

    public void ResetProgress()
    {
        currentStars = 2;
        hasHintScroll = false;
        expPotionCount = 0;

        PlayerPrefs.DeleteKey("PlayerStars");
        PlayerPrefs.DeleteKey("ExpPotionCount");
        PlayerPrefs.DeleteKey("HasHintScroll");
        PlayerPrefs.Save();

        UpdateStarUI();
    }
}