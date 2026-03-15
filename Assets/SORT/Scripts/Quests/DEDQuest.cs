using UnityEngine;
using TMPro;
using System.Collections;
using UnityEngine.UI;
using Invector;

public class DEDQuest : MonoBehaviour
{
    [Header("Настройки квеста")]
    [SerializeField] private string questName = "DED";
    [SerializeField] private TextMeshProUGUI questText;
    [SerializeField] private string requiredItemName = "Специальный предмет";
    [SerializeField] private int requiredItemPrice = 100;
    
    [Header("Настройки сообщений")]
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private float messageDuration = 2f;
    [SerializeField] private float messageInterval = 3f;
    private float messageTimer = 0f;
    private float messageIntervalTimer = 0f;
    private bool playerInQuestZone = false;
    
    [Header("Настройки магазина")]
    [SerializeField] private ShopTrigger shopTrigger;
    [SerializeField] private string shopItemName = "Специальный предмет";
    
    [Header("Настройки локации")]
    [SerializeField] private Transform questLocation;
    [SerializeField] private float locationRadius = 5f;
    [SerializeField] private string cutsceneKey = "CutScene_02";
    
    [Header("Настройки UI")]
    [SerializeField] private GameObject moneyText;
    
    [Header("Настройки врагов")]
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private float enemyCheckRadius = 10f;
    
    [Header("Зоны квеста")]
    [Tooltip("Зона, где игрок должен отдать темпо деду")]
    [SerializeField] private GameObject questDedEndZone;
    [Tooltip("Зона, где дед просит темпо")]
    [SerializeField] private GameObject dedAskZone;
    [Tooltip("Зона начала квеста (первая катсцена)")]
    [SerializeField] private GameObject questDedStartZone;
    [Tooltip("Автоматически деактивировать зону Ask после первой катсцены")]
    [SerializeField] private bool deactivateAskZoneAfterCutscene01 = true;
    [Tooltip("Автоматически деактивировать зону End после финальной катсцены")]
    [SerializeField] private bool deactivateEndZoneAfterCutscene02 = true;
    
    // Состояния квеста
    private enum QuestState
    {
        NotStarted,
        WaitingForCutscene,
        NeedToBuyItem,
        NeedToGoToLocation,
        Completed
    }
    
    private QuestState currentState = QuestState.NotStarted;
    private bool hasRequiredItem = false;
    private bool hasVisitedLocation = false;
    private bool hasPlayedCutscene = false;
    private GameObject player;
    
    private void Start()
    {
        // Находим игрока
        player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            Debug.LogError("Игрок не найден в сцене! Убедитесь, что у игрока установлен тег 'Player'");
            return;
        }
        
        // Подписываемся на событие завершения катсцены
        if (CutsceneManager.Instance != null)
        {
            CutsceneManager.OnCutsceneEnded += OnCutsceneEnded;
        }
        else
        {
            Debug.LogError("CutsceneManager не найден в сцене!");
        }
        
        // Подписываемся на событие покупки предмета
        if (ShopSystem.Instance != null)
        {
            ShopSystem.Instance.OnItemPurchased += OnItemPurchased;
        }
        else
        {
            Debug.LogError("ShopSystem не найден в сцене!");
        }
        
        // Проверяем наличие необходимых компонентов
        ValidateComponents();
        
        // Если нет зоны начала квеста, попробуем найти её
        if (questDedStartZone == null)
        {
            questDedStartZone = GameObject.Find("Quest_ded_start");
            if (questDedStartZone != null)
            {
                Debug.Log("[DEDQuest] Quest_ded_start найден автоматически");
            }
        }
    }
    
    private void ValidateComponents()
    {
        if (questText == null)
        {
            Debug.LogError("Quest Text не назначен в DEDQuest!");
        }
        
        if (messageText == null)
        {
            Debug.LogError("Message Text не назначен в DEDQuest!");
        }
        
        if (shopTrigger == null)
        {
            Debug.LogError("Shop Trigger не назначен в DEDQuest!");
        }
        
        if (questLocation == null)
        {
            Debug.LogError("Quest Location не назначен в DEDQuest!");
        }
        
        if (moneyText == null)
        {
            Debug.LogError("Money Text не назначен в DEDQuest!");
        }
        
        // Проверяем наличие зон квеста
        if (questDedEndZone == null)
        {
            Debug.LogWarning("Quest_ded_end зона не назначена в DEDQuest! Автоматическая деактивация не будет работать.");
        }
        
        if (dedAskZone == null)
        {
            Debug.LogWarning("Ded_AskZone не назначена в DEDQuest! Автоматическая деактивация не будет работать.");
        }
        
        if (questDedStartZone == null)
        {
            Debug.LogWarning("Quest_ded_start зона не назначена в DEDQuest! Скрытие зоны после CutScene_01 не будет работать.");
        }
    }
    
    private void OnDestroy()
    {
        // Отписываемся от событий
        if (CutsceneManager.Instance != null)
        {
            CutsceneManager.OnCutsceneEnded -= OnCutsceneEnded;
        }
        
        if (ShopSystem.Instance != null)
        {
            ShopSystem.Instance.OnItemPurchased -= OnItemPurchased;
        }
    }
    
    private void Update()
    {
        if (currentState == QuestState.NotStarted || currentState == QuestState.Completed)
            return;
            
        // Проверяем наличие Tempo в каждом кадре, если квест активен
        CheckForTempoItem();
        
        // Проверяем, находится ли игрок в нужной локации
        if (currentState == QuestState.NeedToGoToLocation && !hasVisitedLocation)
        {
            CheckPlayerLocation();
        }
        
        // Проверяем наличие врагов поблизости
        CheckNearbyEnemies();
        
        // Обновляем UI квеста
        UpdateQuestUI();
        
        // Обновляем таймер сообщения
        if (messageTimer > 0)
        {
            messageTimer -= Time.deltaTime;
            if (messageTimer <= 0)
            {
                HideMessage();
            }
        }
        
        // Если игрок в зоне квеста и у него нет предмета, показываем сообщение периодически
        if (playerInQuestZone && !hasRequiredItem)
        {
            messageIntervalTimer -= Time.deltaTime;
            if (messageIntervalTimer <= 0)
            {
                ShowMessage("дай поесть");
                messageIntervalTimer = messageInterval;
            }
        }
    }
    
    private void CheckNearbyEnemies()
    {
        if (player == null) return;
        
        // Ищем врагов в радиусе
        Collider[] colliders = Physics.OverlapSphere(player.transform.position, enemyCheckRadius, enemyLayer);
        
        foreach (Collider collider in colliders)
        {
            // Проверяем, что это враг с компонентом vHealthController
            vHealthController healthController = collider.GetComponent<vHealthController>();
            if (healthController != null && !healthController.isDead)
            {
                // Если враг жив и у него нет компонента MoneyDrop, добавляем его
                if (collider.GetComponent<MoneyDrop>() == null)
                {
                    collider.gameObject.AddComponent<MoneyDrop>();
                }
            }
        }
    }
    
    private void CheckPlayerLocation()
    {
        if (questLocation == null || player == null) return;
        
        float distance = Vector3.Distance(player.transform.position, questLocation.position);
        Debug.Log($"[DEBUG] Расстояние до локации: {distance}, Радиус: {locationRadius}");
        
        if (distance <= locationRadius)
        {
            // Указываем, что игрок в зоне квеста
            playerInQuestZone = true;
            
            // Проверяем наличие Tempo через TempoItem.Instance
            CheckForTempoItem();
            
            // Проверяем наличие предмета "tempo" перед запуском катсцены
            if (!hasRequiredItem)
            {
                Debug.Log("[DEBUG] Попытка запуска катсцены без предмета tempo!");
                // Сообщение теперь будет показываться через Update с интервалом
                if (messageIntervalTimer <= 0)
                {
                    ShowMessage("дай поесть");
                    messageIntervalTimer = messageInterval;
                }
                return;
            }
            
            Debug.Log("[DEBUG] Игрок в зоне локации и имеет предмет tempo!");
            hasVisitedLocation = true;
            
            // Запускаем катсцену
            if (CutsceneManager.Instance != null)
            {
                Debug.Log($"[DEBUG] Запускаем катсцену {cutsceneKey}...");
                CutsceneManager.Instance.StartCutscene(cutsceneKey);
                hasPlayedCutscene = true;
                Debug.Log("[DEBUG] Катсцена запущена!");
            }
            else
            {
                Debug.LogError("CutsceneManager не найден в сцене!");
            }
        }
        else
        {
            // Игрок вышел из зоны
            playerInQuestZone = false;
        }
    }
    
    // Метод для вызова из CutsceneTrigger
    public void CheckPlayerLocationManually(Vector3 triggerPosition)
    {
        if (player == null) return;
        
        // Указываем, что игрок в зоне квеста
        playerInQuestZone = true;
        
        // Проверяем наличие Tempo через TempoItem.Instance
        CheckForTempoItem();
        
        // Проверяем наличие предмета "tempo" перед запуском катсцены
        if (!hasRequiredItem)
        {
            Debug.Log("[DEBUG] Попытка запуска катсцены без предмета tempo!");
            // Сообщение теперь будет показываться через Update с интервалом
            if (messageIntervalTimer <= 0)
            {
                ShowMessage("дай поесть");
                messageIntervalTimer = messageInterval;
            }
            return;
        }
        
        Debug.Log("[DEBUG] Игрок в зоне локации и имеет предмет tempo!");
        hasVisitedLocation = true;
        
        // Запускаем катсцену
        if (CutsceneManager.Instance != null)
        {
            Debug.Log($"[DEBUG] Запускаем катсцену {cutsceneKey}...");
            CutsceneManager.Instance.StartCutscene(cutsceneKey);
            hasPlayedCutscene = true;
            Debug.Log("[DEBUG] Катсцена запущена!");
        }
        else
        {
            Debug.LogError("CutsceneManager не найден в сцене!");
        }
    }
    
    // Метод для вызова из CutsceneTrigger при выходе игрока из зоны
    public void PlayerExitQuestZone()
    {
        playerInQuestZone = false;
        Debug.Log("[DEBUG] Игрок вышел из зоны квеста");
    }
    
    private void OnCutsceneEnded(GameObject cutscene)
    {
        // Проверяем, что это наша катсцена
        if (cutscene.name.Contains("CutScene_01"))
        {
            // Запускаем квест после CutScene_01
            StartQuest();
            
            // Скрываем объект Quest_ded_start (делаем невидимым, но оставляем активным)
            if (questDedStartZone != null)
            {
                MeshRenderer renderer = questDedStartZone.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    renderer.enabled = false;
                    Debug.Log("[DEDQuest] MeshRenderer объекта Quest_ded_start отключен после CutScene_01");
                }
                else
                {
                    Debug.LogWarning("[DEDQuest] Не удалось найти MeshRenderer у объекта Quest_ded_start");
                }
                // Делаем объект неактивным после активации квеста
                questDedStartZone.SetActive(false);
                Debug.Log("[DEDQuest] Объект Quest_ded_start деактивирован после CutScene_01");
            }
            else
            {
                Debug.LogWarning("[DEDQuest] Quest_ded_start не найден, невозможно скрыть его после CutScene_01");
            }
            
            // Деактивируем зону Ded_AskZone после первой катсцены, если указано
            // if (deactivateAskZoneAfterCutscene01 && dedAskZone != null)
            // {
            //     dedAskZone.SetActive(false);
            //     Debug.Log("[DEBUG] Ded_AskZone деактивирована после CutScene_01");
            // }
        }
        else if (cutscene.name.Contains(cutsceneKey) && hasVisitedLocation)
        {
            // Завершаем квест после финальной катсцены
            CompleteQuest();
            
            // Деактивируем триггер квеста (Quest_ded_end)
            if (deactivateEndZoneAfterCutscene02 && questDedEndZone != null)
            {
                questDedEndZone.SetActive(false);
                Debug.Log("[DEBUG] Quest_ded_end деактивирован после финальной катсцены");
            }
            
            // Деактивируем также Ded_AskZone после финальной катсцены, для полного завершения квеста
            if (dedAskZone != null && dedAskZone.activeSelf)
            {
                dedAskZone.SetActive(false);
                Debug.Log("[DEBUG] Ded_AskZone деактивирована после финальной катсцены (для полного завершения квеста)");
            }
            
            // Полностью деактивируем объект Quest_ded_start после активации cutscene_02
            if (questDedStartZone != null)
            {
                questDedStartZone.SetActive(false);
                Debug.Log("[DEDQuest] Объект Quest_ded_start полностью деактивирован после CutScene_02");
            }
            else
            {
                Debug.LogWarning("[DEDQuest] Quest_ded_start не найден, невозможно деактивировать его после CutScene_02");
            }
        }
    }
    
    private void OnItemPurchased(ShopItem item)
    {
        Debug.Log($"[DEBUG] Получено событие покупки предмета: {item.itemName}");
        
        // Проверяем, что куплен предмет "tempo" (без учета регистра)
        if (item.itemName.ToLower() == "tempo")
        {
            Debug.Log("[DEBUG] Куплен предмет tempo для квеста!");
            hasRequiredItem = true;
            Debug.Log($"[DEBUG] hasRequiredItem установлен в {hasRequiredItem}");
            
            // Обновляем состояние квеста
            if (currentState == QuestState.NeedToBuyItem)
            {
                Debug.Log("[DEBUG] Квест переходит в состояние NeedToGoToLocation");
                currentState = QuestState.NeedToGoToLocation;
            }
        }
        else
        {
            Debug.Log($"[DEBUG] Куплен предмет {item.itemName}, но он не подходит для квеста (нужен tempo)");
        }
    }
    
    private void StartQuest()
    {
        if (currentState == QuestState.NotStarted)
        {
            currentState = QuestState.NeedToBuyItem;
            
            // Обновляем UI
            UpdateQuestUI();
        }
    }
    
    private void CompleteQuest()
    {
        if (currentState != QuestState.Completed)
        {
            currentState = QuestState.Completed;
            
            Debug.Log("[DEDQuest] Начало завершения квеста...");
            
            // Удаляем предмет темпо из инвентаря
            if (TempoItem.Instance != null)
            {
                Debug.Log("[DEDQuest] TempoItem.Instance найден, удаляем предмет темпо...");
                
                // Проверяем, есть ли предмет у игрока
                bool hadTempoItem = TempoItem.Instance.HasTempo();
                Debug.Log($"[DEDQuest] У игрока был предмет темпо? {hadTempoItem}");
                
                // Принудительно сбрасываем количество темпо
                TempoItem.Instance.SetTempoCount(-1);
                
                // Принудительно обновляем UI после удаления
                TempoItem.Instance.ForceUpdateUI();
                
                // Проверяем, что предмет действительно удален
                bool stillHasTempoItem = TempoItem.Instance.HasTempo();
                Debug.Log($"[DEDQuest] После удаления - у игрока все еще есть предмет темпо? {stillHasTempoItem}");
                
                if (stillHasTempoItem) {
                    Debug.LogError("[DEDQuest] ОШИБКА: Не удалось удалить предмет темпо, пробуем еще раз!");
                    // Повторная попытка удаления с принудительным вызовом
                    TempoItem.Instance.SetTempoCount(0);
                    TempoItem.Instance.ForceUpdateUI();
                }
                
                Debug.Log("[DEDQuest] Предмет темпо удален из инвентаря после завершения квеста");
            }
            else
            {
                Debug.LogError("[DEDQuest] Невозможно удалить предмет темпо - TempoItem.Instance не найден!");
            }
            
            // Обновляем UI
            UpdateQuestUI();
            
            // Вызываем событие завершения квеста
            OnQuestCompleted?.Invoke();
            
            Debug.Log("[DEDQuest] Квест DEDQuest успешно завершен!");
        }
    }
    
    private void UpdateQuestUI()
    {
        if (questText == null) return;
        
        string moneyInfo = "";
        if (MoneySystem.Instance != null)
        {
            moneyInfo = $"\nДеньги: {MoneySystem.Instance.GetCurrentMoney()} ₽";
        }
        
        switch (currentState)
        {
            case QuestState.NotStarted:
                questText.text = "";
                break;
                
            case QuestState.WaitingForCutscene:
                questText.text = "Квест DED: Ожидание начала квеста...";
                break;
                
            case QuestState.NeedToBuyItem:
                questText.text = $"Квест DED: Купите {requiredItemName} в магазине за {requiredItemPrice} ₽{moneyInfo}";
                break;
                
            case QuestState.NeedToGoToLocation:
                questText.text = "Квест DED: Отправьтесь в указанную локацию";
                break;
                
            case QuestState.Completed:
                questText.text = "Квест DED: Завершен";
                break;
        }
    }
    
    // Событие завершения квеста
    public delegate void QuestCompletedHandler();
    public event QuestCompletedHandler OnQuestCompleted;
    
    // Публичные методы для доступа к состоянию квеста
    public bool IsQuestActive()
    {
        return currentState != QuestState.NotStarted && currentState != QuestState.Completed;
    }
    
    public bool IsQuestCompleted()
    {
        return currentState == QuestState.Completed;
    }
    
    public string GetQuestName()
    {
        return questName;
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
    
    public bool HasRequiredItem()
    {
        Debug.Log($"[DEBUG] DEDQuest.HasRequiredItem() вернул {hasRequiredItem}");
        return hasRequiredItem;
    }
    
    // Публичный метод для установки предмета
    public void SetHasRequiredItem(bool value)
    {
        Debug.Log($"[DEBUG] DEDQuest.SetHasRequiredItem({value}) вызван");
        hasRequiredItem = value;
        
        // Если устанавливаем предмет и находимся в нужном состоянии квеста, обновляем его
        if (value && currentState == QuestState.NeedToBuyItem)
        {
            Debug.Log("[DEBUG] Квест переходит в состояние NeedToGoToLocation");
            currentState = QuestState.NeedToGoToLocation;
            UpdateQuestUI();
        }
    }
    
    // Метод для проверки наличия Tempo у игрока
    private void CheckForTempoItem()
    {
        // Проверяем наличие Tempo через TempoItem.Instance
        if (TempoItem.Instance != null && TempoItem.Instance.HasTempo())
        {
            // Если у игрока есть Tempo, устанавливаем флаг наличия требуемого предмета
            hasRequiredItem = true;
            Debug.Log("[DEDQuest] Обнаружен предмет Tempo у игрока, hasRequiredItem установлен в true");
            
            // Обновляем состояние квеста если необходимо
            if (currentState == QuestState.NeedToBuyItem)
            {
                Debug.Log("[DEDQuest] Квест переходит в состояние NeedToGoToLocation из-за наличия Tempo");
                currentState = QuestState.NeedToGoToLocation;
                UpdateQuestUI();
            }
        }
        else
        {
            // Если у игрока нет Tempo, сбрасываем флаг
            hasRequiredItem = false;
            Debug.Log("[DEDQuest] Предмет Tempo не обнаружен, hasRequiredItem установлен в false");
        }
    }
} 