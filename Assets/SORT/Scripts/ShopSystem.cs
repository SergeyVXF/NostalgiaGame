using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class ShopItem
{
    public string itemName;
    public string description;
    public int price;
    public GameObject itemPrefab;
    public Sprite itemIcon;
}

public class ShopSystem : MonoBehaviour
{
    public static ShopSystem Instance { get; private set; }
    
    [SerializeField] private List<ShopItem> shopItems = new List<ShopItem>();
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }
    
    // Событие покупки предмета
    public delegate void ItemPurchasedHandler(ShopItem item);
    public event ItemPurchasedHandler OnItemPurchased;
} 