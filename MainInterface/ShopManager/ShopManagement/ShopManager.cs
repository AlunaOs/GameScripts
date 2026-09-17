using UnityEngine;
using TMPro;

public class ShopManager : MonoBehaviour
{
    public TextMeshProUGUI itemNameText;
    public TextMeshProUGUI itemDescriptionText;
    public TextMeshProUGUI priceText;

    // Keep your original function
    public void UpdateShopDisplay(string name, string description, int price)
    {
        itemNameText.text = "Item Name: " + name;
        itemDescriptionText.text = "Description: " + description;
        priceText.text = "Price: " + price.ToString();
    }

    public void SetItem1()
    {
        UpdateShopDisplay("Item 1", "item 1 description here", 10);
    }

    public void SetItem2()
    {
        UpdateShopDisplay("Item 2", "item 2 description here", 20);
    }

    public void SetItem3()
    {
        UpdateShopDisplay("Item 3", "item 3 description here", 30);
    }

    public void SetItem4()
    {
        UpdateShopDisplay("Item 4", "item 4 description here", 40);
    }

}