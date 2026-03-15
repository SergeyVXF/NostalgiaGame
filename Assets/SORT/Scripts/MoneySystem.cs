using UnityEngine;
using TMPro;
using System.Collections;

public class MoneySystem : MonoBehaviour
{
    public static MoneySystem Instance { get; private set; }
    
    [SerializeField] private TextMeshProUGUI moneyText;
    [SerializeField] private int startingMoney = 0;
    
    private int currentMoney = 0;
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        
        currentMoney = startingMoney;
        UpdateMoneyUI();
    }
    
    public void AddMoney(int amount)
    {
        currentMoney += amount;
        UpdateMoneyUI();
    }
    
    public bool SpendMoney(int amount)
    {
        if (currentMoney >= amount)
        {
            currentMoney -= amount;
            UpdateMoneyUI();
            return true;
        }
        
        return false;
    }
    
    public int GetCurrentMoney()
    {
        return currentMoney;
    }
    
    private void UpdateMoneyUI()
    {
        if (moneyText != null)
        {
            moneyText.text = currentMoney.ToString();
        }
    }
} 