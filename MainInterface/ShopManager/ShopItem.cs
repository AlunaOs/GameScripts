using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ShopItem : MonoBehaviour
{
    public ShopItemData itemData;

    private Button button;
    private TextMeshProUGUI buttonText;

    void Awake()
    {
        button = GetComponent<Button>();
        buttonText = GetComponentInChildren<TextMeshProUGUI>();

        if (buttonText != null && itemData != null)
        {
            buttonText.text = itemData.itemName;
        }
    }

    void Start()
    {
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                if (ShopManagers.Instance != null)
                {
                    ShopManagers.Instance.ShowItemDetails(this);
                }
            });
        }
    }
}