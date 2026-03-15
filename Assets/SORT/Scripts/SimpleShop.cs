using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class SimpleShop : MonoBehaviour
{
    [SerializeField] private int tempoPrice = 100;
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private float messageDuration = 2f;
    
    private bool isInShopZone = false;
    private float messageTimer = 0f;
    
    private void Update()
    {
        // Проверяем нажатие клавиши E в зоне магазина
        if (isInShopZone && Input.GetKeyDown(KeyCode.E))
        {
            TryBuyTempo();
        }
        
        // Обновляем таймер сообщения
        if (messageTimer > 0)
        {
            messageTimer -= Time.deltaTime;
            if (messageTimer <= 0)
            {
                HideMessage();
            }
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isInShopZone = true;
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isInShopZone = false;
        }
    }
    
    private void TryBuyTempo()
    {
        if (MoneySystem.Instance == null)
        {
            ShowMessage("Ошибка: система денег не найдена");
            return;
        }
        
        if (TempoItem.Instance == null)
        {
            ShowMessage("Ошибка: система предметов не найдена");
            return;
        }
        
        // Проверяем достаточно ли денег
        if (MoneySystem.Instance.GetCurrentMoney() >= tempoPrice)
        {
            // Списываем деньги
            MoneySystem.Instance.SpendMoney(tempoPrice);
            
            // Добавляем предмет
            TempoItem.Instance.AddTempo();
            
            // Обновляем статус предмета в DEDQuest
            DEDQuest dedQuest = FindObjectOfType<DEDQuest>();
            if (dedQuest != null)
            {
                // Используем публичный метод вместо прямого доступа к полю
                dedQuest.SetHasRequiredItem(true);
                Debug.Log("[DEBUG] SimpleShop: Установлен флаг наличия предмета в DEDQuest");
            }
            else
            {
                Debug.LogError("[DEBUG] SimpleShop: DEDQuest не найден в сцене!");
            }
            
            // Показываем сообщение об успешной покупке
            ShowMessage("Вы купили tempo!");
        }
        else
        {
            // Показываем сообщение о недостатке денег
            ShowMessage("У тебя не хватает денег");
        }
    }
    
    private void ShowMessage(string message)
    {
        if (messageText != null)
        {
            messageText.text = message;
            messageText.gameObject.SetActive(true);
            messageTimer = messageDuration;
        }
    }
    
    private void HideMessage()
    {
        if (messageText != null)
        {
            messageText.gameObject.SetActive(false);
        }
    }
} 