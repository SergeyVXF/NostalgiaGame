using UnityEngine;
using TMPro;
using Invector;
using System.Linq;

public class TempoItem : MonoBehaviour
{
    private static TempoItem instance;
    public static TempoItem Instance => instance;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI tempoCountText;
    [SerializeField] private GameObject tempoIcon;
    [SerializeField] private GameObject handItemObjectPrefab; // Префаб объекта предмета в руке
    [SerializeField] private string tempoHandPrefabPath = "Assets/ANTIGOP/TempoHand.prefab"; // Резервный путь к префабу
    
    [Header("Настройки использования")]
    [SerializeField] private int healAmount = 25; // Количество восстанавливаемого здоровья
    [SerializeField] private AudioClip useSound; // Звук использования предмета
    [SerializeField] private GameObject useEffect; // Эффект использования
    
    private int tempoCount = 0;
    private bool isItemSelected = false; // Выбран ли предмет
    private vHealthController playerHealth; // Ссылка на компонент здоровья игрока
    private GameObject handItemInstance; // Экземпляр объекта в руке (не префаб)
    
    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            Debug.Log("[TempoItem] Инстанс создан");
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    private void Start()
    {
        // Находим компонент здоровья игрока
        playerHealth = GetComponentInParent<vHealthController>();
        if (playerHealth == null)
        {
            playerHealth = GameObject.FindGameObjectWithTag("Player")?.GetComponent<vHealthController>();
            if (playerHealth == null)
            {
                Debug.LogError("[TempoItem] Не удалось найти компонент здоровья игрока!");
            }
        }
        
        // Скрываем иконку и текст при старте
        if (tempoIcon != null)
        {
            tempoIcon.SetActive(false);
            Debug.Log("[TempoItem] Иконка скрыта при старте");
        }
        else
        {
            Debug.LogError("[TempoItem] tempoIcon не назначен в инспекторе!");
        }
        
        if (tempoCountText == null)
        {
            Debug.LogError("[TempoItem] tempoCountText не назначен в инспекторе!");
        }
        
        // Инициализация объекта предмета в руке
        InitializeHandItem();
        
        UpdateUI();
    }
    
    // Инициализация объекта предмета в руке
    private void InitializeHandItem()
    {
        // Загружаем префаб, если он не был назначен в инспекторе
        if (handItemObjectPrefab == null)
        {
            #if UNITY_EDITOR
            // В редакторе используем AssetDatabase для загрузки
            handItemObjectPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(tempoHandPrefabPath);
            if (handItemObjectPrefab != null)
            {
                Debug.Log($"[TempoItem] Префаб загружен из '{tempoHandPrefabPath}'");
            }
            #else
            // В сборке пытаемся загрузить из Resources
            handItemObjectPrefab = Resources.Load<GameObject>("TempoHand");
            if (handItemObjectPrefab != null)
            {
                Debug.Log("[TempoItem] Префаб загружен из Resources");
            }
            #endif
            
            if (handItemObjectPrefab == null)
            {
                Debug.LogError("[TempoItem] Не удалось загрузить префаб TempoHand!");
                return;
            }
        }
        
        // Не создаем экземпляр здесь, а будем создавать его только когда нужно
        // в методе SelectTempoItem
    }
    
    private void Update()
    {
        // Обработка выбора предметов
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            // Переключаемся на пустые руки
            SelectEmptyHands();
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2) && tempoCount > 0)
        {
            // Переключаемся на предмет темпо
            SelectTempoItem();
        }
        
        // Использование предмета (только если предмет выбран и есть в наличии)
        if (isItemSelected && tempoCount > 0 && Input.GetKeyDown(KeyCode.E))
        {
            UseTempoItem();
        }
    }
    
    public void AddTempo()
    {
        tempoCount++;
        Debug.Log($"[TempoItem] Добавлен предмет темпо. Текущее количество: {tempoCount}");
        UpdateUI();
        
        // Показываем иконку при получении первого предмета
        if (tempoIcon != null && tempoCount == 1)
        {
            tempoIcon.SetActive(true);
            Debug.Log("[TempoItem] Иконка активирована");
            
            // Обновляем видимость слота темпо в системе инвентаря
            if (InventorySystem.Instance != null)
            {
                InventorySystem.Instance.UpdateTempoSlotVisibility(true);
                
                // Автоматически выбираем слот темпо при подборе предмета
                InventorySystem.Instance.SelectSlot(1);
                Debug.Log("[TempoItem] Автоматически выбран слот темпо после подбора предмета");
            }
        }
        else if (tempoCount > 1 && InventorySystem.Instance != null)
        {
            // Если это не первый предмет, но текущий выбранный слот - пустые руки,
            // и игрок поднял темпо, то автоматически выбираем слот темпо
            if (InventorySystem.Instance.GetSelectedSlot() == 0)
            {
                InventorySystem.Instance.SelectSlot(1);
                Debug.Log("[TempoItem] Автоматически выбран слот темпо после подбора дополнительного предмета");
            }
        }
    }
    
    /// <summary>
    /// Устанавливает количество предмета темпо.
    /// При значении -1 скрывает иконку и сбрасывает счетчик.
    /// </summary>
    /// <param name="count">Новое количество предмета или -1 для удаления</param>
    public void SetTempoCount(int count)
    {
        Debug.Log($"[TempoItem] SetTempoCount вызван с параметром: {count}. Текущее значение: {tempoCount}");
        
        bool hadTempo = tempoCount > 0;
        
        // Если установлено -1, то предмет удаляется из инвентаря
        if (count == -1)
        {
            Debug.Log("[TempoItem] Удаление предмета темпо из инвентаря...");
            tempoCount = 0;
            
            // Если предмет был выбран, деактивируем его
            if (isItemSelected)
            {
                SelectEmptyHands();
            }
        }
        else
        {
            // Установка количества
            tempoCount = count;
            Debug.Log($"[TempoItem] Установлено новое количество темпо: {tempoCount}");
        }
        
        // Проверяем, изменилось ли наличие предмета
        bool hasTempo = tempoCount > 0;
        if (hadTempo != hasTempo)
        {
            // Обновляем видимость слота темпо в системе инвентаря
            if (InventorySystem.Instance != null)
            {
                InventorySystem.Instance.UpdateTempoSlotVisibility(hasTempo);
            }
        }
        
        // Принудительно обновляем UI
        ForceUpdateUI();
    }
    
    private void UpdateUI()
    {
        try
        {
            if (tempoCountText != null)
            {
                // Отображаем количество, если оно больше 0, иначе пустую строку
                tempoCountText.text = tempoCount > 0 ? tempoCount.ToString() : "";
                Debug.Log($"[TempoItem] Обновлен текст количества: '{tempoCountText.text}'");
            }
            
            // Обновляем видимость иконки
            if (tempoIcon != null)
            {
                bool shouldBeVisible = tempoCount > 0;
                tempoIcon.SetActive(shouldBeVisible);
                Debug.Log($"[TempoItem] Обновлена видимость иконки: {shouldBeVisible}");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[TempoItem] Ошибка при обновлении UI: {e.Message}");
        }
    }
    
    // Метод для принудительного обновления UI
    public void ForceUpdateUI()
    {
        Debug.Log("[TempoItem] Принудительное обновление UI...");
        
        // Проверяем компоненты
        if (tempoCountText == null)
        {
            Debug.LogError("[TempoItem] tempoCountText не найден!");
            // Пытаемся найти текст
            tempoCountText = GetComponentInChildren<TextMeshProUGUI>();
            if (tempoCountText != null)
            {
                Debug.Log("[TempoItem] tempoCountText найден автоматически");
            }
        }
        
        if (tempoIcon == null)
        {
            Debug.LogError("[TempoItem] tempoIcon не найден!");
            // Можно попытаться найти через GameObject.Find, но это не рекомендуется
        }
        
        // Принудительно устанавливаем видимость иконки
        if (tempoIcon != null)
        {
            bool shouldBeVisible = tempoCount > 0;
            tempoIcon.SetActive(shouldBeVisible);
            Debug.Log($"[TempoItem] Принудительно установлена видимость иконки: {shouldBeVisible}");
        }
        
        // Принудительно обновляем текст
        if (tempoCountText != null)
        {
            tempoCountText.text = tempoCount > 0 ? tempoCount.ToString() : "";
            Debug.Log($"[TempoItem] Принудительно обновлен текст: '{tempoCountText.text}'");
        }
    }
    
    public bool HasTempo()
    {
        bool result = tempoCount > 0;
        Debug.Log($"[TempoItem] HasTempo вернул: {result}");
        return result;
    }
    
    // Метод для выбора пустых рук
    private void SelectEmptyHands()
    {
        Debug.Log("[TempoItem] Выбраны пустые руки");
        
        // Скрываем предмет в руке, если он есть
        if (handItemInstance != null)
        {
            handItemInstance.SetActive(false);
        }
        
        // Сбрасываем флаг выбора предмета
        isItemSelected = false;
        
        // Обновляем иконку в интерфейсе на hands_free_inv
        if (InventorySystem.Instance != null)
        {
            InventorySystem.Instance.UpdateSlot1Icon(true);
        }
    }
    
    // Метод для выбора предмета темпо
    private void SelectTempoItem()
    {
        Debug.Log("[TempoItem] Вызван метод SelectTempoItem");
        
        // Проверяем, что предмет есть
        if (tempoCount <= 0)
        {
            Debug.Log("[TempoItem] Нельзя выбрать Tempo - его нет в инвентаре");
            return;
        }
        
        // Прекращаем выполнение, если предмет уже выбран
        if (isItemSelected)
        {
            Debug.Log("[TempoItem] Tempo уже выбран");
            return;
        }
        
        // Устанавливаем флаг, что предмет выбран
        isItemSelected = true;
        
        // Обновляем иконку в интерфейсе на hands_free_inv, так как предмет теперь в руках
        if (InventorySystem.Instance != null)
        {
            InventorySystem.Instance.UpdateSlot1Icon(false);
        }
        
        // Проверяем, что у нас есть префаб
        if (handItemObjectPrefab != null)
        {
            // Получаем компонент аниматора для доступа к костям
            Animator animator = GetComponentInParent<Animator>();
            if (animator == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    animator = player.GetComponent<Animator>();
                }
            }
            
            if (animator != null)
            {
                Transform leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
                if (leftHand != null)
                {
                    // Ищем/создаем defaultHandler в левой руке, чтобы правильно прикрепить предмет
                    Transform defaultHandler = leftHand.Find("defaultHandler");
                    if (defaultHandler == null)
                    {
                        // Создаем defaultHandler если его нет
                        GameObject handler = new GameObject("defaultHandler");
                        handler.transform.SetParent(leftHand);
                        handler.transform.localPosition = Vector3.zero;
                        handler.transform.localRotation = Quaternion.identity;
                        defaultHandler = handler.transform;
                    }
                    
                    // Удаляем старый экземпляр, если он существует
                    if (handItemInstance != null)
                    {
                        Destroy(handItemInstance);
                    }
                    
                    // Создаем новый экземпляр из префаба, сохраняя его оригинальные настройки
                    handItemInstance = Instantiate(handItemObjectPrefab, defaultHandler);
                    handItemInstance.name = "TempoHandInstance";
                    
                    // Позиционируем в руке, но сохраняем оригинальный масштаб
                    handItemInstance.transform.localPosition = new Vector3(0.02f, -0.1f, 0.01f);
                    handItemInstance.transform.localRotation = Quaternion.Euler(0, 90, 0);
                    // Масштаб префаба сохраняется при инстанцировании
                    
                    // Отключаем все коллайдеры
                    Collider[] colliders = handItemInstance.GetComponents<Collider>();
                    foreach (Collider collider in colliders)
                    {
                        collider.enabled = false;
                    }
                    
                    Collider[] childColliders = handItemInstance.GetComponentsInChildren<Collider>();
                    foreach (Collider collider in childColliders)
                    {
                        collider.enabled = false;
                    }
                    
                    // Отключаем Rigidbody
                    Rigidbody rb = handItemInstance.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        rb.isKinematic = true;
                        rb.detectCollisions = false;
                    }
                    
                    // Показываем объект
                    handItemInstance.SetActive(true);
                    Debug.Log("[TempoItem] Предмет прикреплен к левой руке игрока");
                }
                else
                {
                    Debug.LogError("[TempoItem] Не удалось найти левую руку игрока!");
                }
            }
            else
            {
                Debug.LogError("[TempoItem] Не удалось найти аниматор игрока!");
            }
        }
        else
        {
            Debug.LogError("[TempoItem] handItemObjectPrefab не назначен!");
        }
        
        Debug.Log("[TempoItem] Выбран предмет Tempo");
    }
    
    // Метод для использования предмета темпо
    private void UseTempoItem()
    {
        Debug.Log("[TempoItem] Попытка использования предмета Tempo");
        
        // Проверяем, есть ли предмет и выбран ли он
        if (!isItemSelected || tempoCount <= 0)
        {
            Debug.Log("[TempoItem] Невозможно использовать Tempo - предмет не выбран или его нет в инвентаре");
            return;
        }
        
        bool hadMultipleItems = tempoCount > 1;
        
        // Проверяем, нужно ли лечение
        if (playerHealth != null && playerHealth.currentHealth < playerHealth.maxHealth)
        {
            // Лечим игрока
            playerHealth.AddHealth(healAmount);
            Debug.Log($"[TempoItem] Игрок исцелен на {healAmount} единиц здоровья");
            
            // Проигрываем звук, если он назначен
            if (useSound != null)
            {
                AudioSource.PlayClipAtPoint(useSound, transform.position);
            }
            
            // Создаем эффект, если он назначен
            if (useEffect != null)
            {
                Instantiate(useEffect, transform.position, transform.rotation);
            }
            
            // Уменьшаем количество предметов
            tempoCount--;
            Debug.Log($"[TempoItem] Предмет Tempo использован. Осталось: {tempoCount}");
            
            // Если предметов больше нет, скрываем объект в руке
            if (tempoCount <= 0 && handItemInstance != null)
            {
                handItemInstance.SetActive(false);
                isItemSelected = false;
                
                // Обновляем систему инвентаря - скрываем слот темпо
                if (InventorySystem.Instance != null)
                {
                    InventorySystem.Instance.UpdateTempoSlotVisibility(false);
                    
                    // Переключаемся на пустые руки
                    InventorySystem.Instance.SelectSlot(0);
                }
            }
        }
        else
        {
            Debug.Log("[TempoItem] Невозможно использовать Tempo - у игрока полное здоровье");
            
            // Даже если здоровье полное, предмет все равно используется и исчезает
            tempoCount--;
            Debug.Log($"[TempoItem] Предмет Tempo использован впустую. Осталось: {tempoCount}");
            
            // Если предметов больше нет, скрываем объект в руке
            if (tempoCount <= 0 && handItemInstance != null)
            {
                handItemInstance.SetActive(false);
                isItemSelected = false;
                
                // Обновляем систему инвентаря - скрываем слот темпо
                if (InventorySystem.Instance != null)
                {
                    InventorySystem.Instance.UpdateTempoSlotVisibility(false);
                    
                    // Переключаемся на пустые руки
                    InventorySystem.Instance.SelectSlot(0);
                }
            }
        }
        
        // Обновляем UI
        UpdateUI();
    }
} 