using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InventorySystem : MonoBehaviour
{
    private static InventorySystem instance;
    public static InventorySystem Instance => instance;
    
    [Header("UI элементы")]
    [SerializeField] private GameObject itemSlot1; // Слот для пустых рук
    [SerializeField] private GameObject itemSlot2; // Слот для темпо
    [SerializeField] private GameObject itemSlot3; // Слот для косточки
    
    [Header("Иконки")]
    [SerializeField] private GameObject slot1Icon; // Объект для иконки пустых рук
    [SerializeField] private GameObject slot3Icon; // Объект для иконки косточки
    
    [Header("Подсветка выбранного слота")]
    [SerializeField] private Image slot1Highlight;
    [SerializeField] private Image slot2Highlight;
    [SerializeField] private Image slot3Highlight;
    [SerializeField] private Color normalColor = Color.gray;
    [SerializeField] private Color selectedColor = Color.white;
    
    // Текущий выбранный слот (0 - пустые руки, 1 - темпо, 2 - косточка)
    private int selectedSlot = 0;
    
    [Header("Косточка")]
    [SerializeField] private bool hasBone = false; // Есть ли косточка у игрока
    [SerializeField] private GameObject bonePrefab; // Префаб косточки для броска
    [Tooltip("Показывать отладочную информацию")]
    public bool showDebugLog = true;
    
    // Кэшированная ссылка на объект tempo
    private GameObject tempoGameObject;
    
    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            Debug.Log("[InventorySystem] Инстанс создан");
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    private void Start()
    {
        // Инициализация UI
        UpdateSlotHighlight();
        
        // Устанавливаем правильную иконку для первого слота (по умолчанию выбран слот 0)
        UpdateSlot1Icon(selectedSlot == 0);
        
        // Ищем и кэшируем объект tempo
        FindTempoGameObject();
        
        // Устанавливаем начальное состояние объекта tempo
        UpdateTempoGameObject(selectedSlot == 1);
    }
    
    private void Update()
    {
        // Обработка выбора слотов нажатием на цифры
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            SelectSlot(0);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            SelectSlot(1);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            SelectSlot(2); // Слот косточки
        }
        
        // Обработка броска косточки (ЛКМ при выбранном слоте косточки)
        if (selectedSlot == 2 && hasBone && Input.GetMouseButtonDown(0))
        {
            ThrowBone();
        }
        
        // Проверяем соответствие состояния объекта tempo выбранному слоту
        // Это гарантирует, что объект tempo будет активен только при выбранном слоте темпо,
        // даже если другие скрипты изменили его состояние
        if (tempoGameObject != null && tempoGameObject.activeSelf != (selectedSlot == 1))
        {
            UpdateTempoGameObject(selectedSlot == 1);
        }
    }
    
    // Выбор слота инвентаря
    public void SelectSlot(int slotIndex)
    {
        if (slotIndex == selectedSlot)
            return;
        
        selectedSlot = slotIndex;
        UpdateSlotHighlight();
        
        // Обновляем видимость иконок в зависимости от выбранного слота
        UpdateSlot1Icon(selectedSlot == 0); // Иконка пустых рук только в слоте 0
        UpdateSlot2Icon(selectedSlot == 1); // Иконка темпо только в слоте 1  
        UpdateSlot3Icon(selectedSlot == 2); // Иконка косточки только в слоте 2
        
        // Активируем/деактивируем GameObject tempo в UI в зависимости от выбранного слота
        UpdateTempoGameObject(selectedSlot == 1);
        
        // Оповещаем TempoItem о смене слота
        if (slotIndex == 1)
        {
            // Выбран слот с темпо
            if (TempoItem.Instance != null && TempoItem.Instance.HasTempo())
            {
                TempoItem.Instance.SendMessage("SelectTempoItem", SendMessageOptions.DontRequireReceiver);
                Debug.Log("[InventorySystem] Выбран слот с темпо");
            }
            else
            {
                Debug.Log("[InventorySystem] Нельзя выбрать слот с темпо - нет предмета");
                // Если нет предмета, возвращаемся к пустым рукам
                selectedSlot = 0;
                UpdateSlotHighlight();
                UpdateSlot1Icon(true); // Показываем иконку пустых рук
                UpdateSlot2Icon(false); // Скрываем иконку темпо
                UpdateSlot3Icon(false); // Скрываем иконку косточки
                TempoItem.Instance?.SendMessage("SelectEmptyHands", SendMessageOptions.DontRequireReceiver);
            }
        }
        else if (slotIndex == 2)
        {
            // Выбран слот с косточкой
            if (hasBone)
            {
                Debug.Log("[InventorySystem] Выбран слот с косточкой");
            }
            else
            {
                Debug.Log("[InventorySystem] Нельзя выбрать слот с косточкой - нет косточки");
                // Если нет косточки, возвращаемся к пустым рукам
                selectedSlot = 0;
                UpdateSlotHighlight();
                UpdateSlot1Icon(true); // Показываем иконку пустых рук
                UpdateSlot2Icon(false); // Скрываем иконку темпо
                UpdateSlot3Icon(false); // Скрываем иконку косточки
            }
        }
        else 
        {
            // Выбраны пустые руки
            TempoItem.Instance?.SendMessage("SelectEmptyHands", SendMessageOptions.DontRequireReceiver);
            Debug.Log("[InventorySystem] Выбраны пустые руки");
        }
    }
    
    // Обновление подсветки слотов в UI
    private void UpdateSlotHighlight()
    {
        if (slot1Highlight != null)
        {
            slot1Highlight.color = (selectedSlot == 0) ? selectedColor : normalColor;
        }
        
        if (slot2Highlight != null)
        {
            slot2Highlight.color = (selectedSlot == 1) ? selectedColor : normalColor;
        }
        
        if (slot3Highlight != null)
        {
            slot3Highlight.color = (selectedSlot == 2) ? selectedColor : normalColor;
        }
    }
    
    // Метод для обновления видимости слота Tempo
    public void UpdateTempoSlotVisibility(bool hasTempoItem)
    {
        if (itemSlot2 != null)
        {
            // Показываем или скрываем слот в зависимости от наличия предмета
            itemSlot2.SetActive(hasTempoItem);
            
            // Если слот скрыт, но он выбран, переключаемся на пустые руки
            if (!hasTempoItem && selectedSlot == 1)
            {
                SelectSlot(0);
            }
        }
    }
    
    // Метод для обновления иконки пустых рук
    // Если handsEmpty = true, показываем иконку hands_free_inv
    // Если handsEmpty = false, скрываем иконку
    public void UpdateSlot1Icon(bool handsEmpty = true)
    {
        if (slot1Icon != null)
        {
            // Просто показываем или скрываем объект handsfree
            slot1Icon.SetActive(handsEmpty);
            Debug.Log($"[InventorySystem] Видимость иконки пустых рук установлена: {handsEmpty}");
        }
        else
        {
            Debug.LogWarning("[InventorySystem] Не найден UI элемент для иконки пустых рук");
        }
    }
    
    // Метод для обновления иконки темпо
    // Если tempoActive = true, показываем иконку темпо
    // Если tempoActive = false, скрываем иконку
    public void UpdateSlot2Icon(bool tempoActive = true)
    {
        if (itemSlot2 != null)
        {
            if (TempoItem.Instance != null && TempoItem.Instance.HasTempo())
            {
                // Активируем или деактивируем иконку темпо
                // Иконка находится внутри слота
                foreach (Transform child in itemSlot2.transform)
                {
                    if (child.gameObject.name.Contains("Icon") || child.gameObject.name.Contains("icon"))
                    {
                        child.gameObject.SetActive(tempoActive);
                        Debug.Log($"[InventorySystem] Видимость иконки темпо установлена: {tempoActive}");
                        break;
                    }
                }
            }
        }
    }
    
    // Метод для поиска и кэширования ссылки на GameObject "tempo"
    private void FindTempoGameObject()
    {
        // Сначала пытаемся найти напрямую
        tempoGameObject = GameObject.Find("tempo");
        
        // Если не нашли напрямую, ищем в UI
        if (tempoGameObject == null)
        {
            GameObject uiObject = GameObject.Find("UI");
            if (uiObject != null)
            {
                Transform tempoTransform = uiObject.transform.Find("tempo");
                if (tempoTransform != null)
                {
                    tempoGameObject = tempoTransform.gameObject;
                    Debug.Log("[InventorySystem] Найден объект 'UI/tempo' в иерархии");
                }
            }
        }
        else
        {
            Debug.Log("[InventorySystem] Найден объект 'tempo' в иерархии");
        }
        
        if (tempoGameObject == null)
        {
            Debug.LogWarning("[InventorySystem] Не удалось найти объект 'tempo' в иерархии!");
        }
    }
    
    // Метод для активации/деактивации GameObject "tempo" в иерархии
    private void UpdateTempoGameObject(bool isActive)
    {
        // Если ссылка не найдена, пробуем найти объект заново
        if (tempoGameObject == null)
        {
            FindTempoGameObject();
        }
        
        // Активируем/деактивируем объект
        if (tempoGameObject != null)
        {
            // Проверяем, изменилось ли состояние
            if (tempoGameObject.activeSelf != isActive)
            {
                tempoGameObject.SetActive(isActive);
                Debug.Log($"[InventorySystem] GameObject 'tempo' в иерархии: {(isActive ? "активирован" : "деактивирован")}");
            }
        }
    }
    
    // Метод для получения индекса текущего выбранного слота
    public int GetSelectedSlot()
    {
        return selectedSlot;
    }
    
    /// <summary>
    /// Обновление иконки 3-го слота (косточка)
    /// </summary>
    private void UpdateSlot3Icon(bool isVisible)
    {
        if (slot3Icon != null)
        {
            slot3Icon.SetActive(isVisible && hasBone);
            Debug.Log($"[InventorySystem] Видимость иконки косточки установлена: {isVisible && hasBone}");
        }
    }
    
    /// <summary>
    /// Добавить косточку в инвентарь
    /// </summary>
    public void AddBone()
    {
        hasBone = true;
        Debug.Log("[InventorySystem] ✅ Косточка добавлена в инвентарь");
        
        // Автоматически переключаемся на слот косточки при подборе
        SelectSlot(2);
    }
    
    /// <summary>
    /// Убрать косточку из инвентаря
    /// </summary>
    public void RemoveBone()
    {
        hasBone = false;
        Debug.Log("[InventorySystem] ❌ Косточка убрана из инвентаря");
        
        // Обновляем иконку
        UpdateSlot3Icon(false);
        
        // Если был выбран слот косточки, переключаемся на пустые руки
        if (selectedSlot == 2)
        {
            SelectSlot(0);
        }
    }
    
    /// <summary>
    /// Проверить, есть ли косточка у игрока
    /// </summary>
    public bool HasBone()
    {
        return hasBone;
    }
    
    /// <summary>
    /// Бросить косточку (по образцу SlowBall)
    /// </summary>
    private void ThrowBone()
    {
        if (!hasBone)
        {
            Debug.LogWarning("[InventorySystem] Нет косточки для броска!");
            return;
        }
        
        Debug.Log("[InventorySystem] 🦴 Бросаю косточку!");
        
        // Находим игрока
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj == null)
        {
            Debug.LogError("[InventorySystem] ❌ Игрок не найден!");
            return;
        }
        
        if (bonePrefab == null)
        {
            Debug.LogError("[InventorySystem] ❌ Префаб косточки не назначен!");
            return;
        }
        
        Transform player = playerObj.transform;
        
        // Создаем косточку на уровне ГОЛОВЫ игрока (как в SlowBall)
        float headHeight = 1.6f; // Высота головы
        Vector3 spawnPosition = player.position + Vector3.up * headHeight + player.forward * 0.5f;
        
        GameObject thrownBone = Instantiate(bonePrefab, spawnPosition, Quaternion.identity);
        
        // Настраиваем физику как в SlowBall
        Rigidbody rb = thrownBone.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = thrownBone.AddComponent<Rigidbody>();
        }
        
        // Настройки физики для предотвращения проваливания
        rb.useGravity = true;
        rb.isKinematic = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; // Лучшее обнаружение столкновений
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.mass = 0.5f; // Легкая косточка
        rb.linearDamping = 1f; // Сопротивление воздуха для более реалистичного полета
        rb.angularDamping = 5f; // Сопротивление вращению
        
        // Получаем направление взгляда игрока (как в SlowBall)
        Vector3 throwDirection = player.forward;
        float throwSpeed = 15f; // Скорость броска как в SlowBall
        
        // Инициализируем движение косточки
        rb.linearVelocity = throwDirection * throwSpeed;
        
        // Добавляем вращение косточки во время полета
        Vector3 randomRotation = new Vector3(
            Random.Range(-360f, 360f), // Вращение по X
            Random.Range(-360f, 360f), // Вращение по Y  
            Random.Range(-360f, 360f)  // Вращение по Z
        );
        rb.angularVelocity = randomRotation * 2f; // Скорость вращения
        
        if (showDebugLog)
            Debug.Log($"[InventorySystem] 🌪️ Добавлено вращение косточки: {randomRotation}");
        
        // Убеждаемся, что у косточки есть коллайдер для столкновений с землей
        Collider mainCollider = thrownBone.GetComponent<Collider>();
        if (mainCollider != null && !mainCollider.isTrigger)
        {
            // Основной коллайдер уже есть - хорошо
            if (showDebugLog)
                Debug.Log($"[InventorySystem] Основной коллайдер найден: {mainCollider.GetType().Name}");
        }
        else
        {
            // Добавляем основной коллайдер для столкновений (увеличенный в 4 раза)
            CapsuleCollider physicsCollider = thrownBone.AddComponent<CapsuleCollider>();
            physicsCollider.isTrigger = false; // НЕ триггер - для физических столкновений
            physicsCollider.radius = 1.2f; // Увеличено в 4 раза (0.3f * 4)
            physicsCollider.height = 4f;   // Увеличено в 4 раза (1f * 4)
            if (showDebugLog)
                Debug.Log("[InventorySystem] Добавлен CapsuleCollider для физики");
        }
        
        // Убеждаемся, что у косточки есть триггер для подбора (отдельный)
        SphereCollider pickupCollider = thrownBone.GetComponent<SphereCollider>();
        if (pickupCollider == null || !pickupCollider.isTrigger)
        {
            pickupCollider = thrownBone.AddComponent<SphereCollider>();
            pickupCollider.isTrigger = true; // Триггер для подбора
            pickupCollider.radius = 2f; // Радиус подбора
            if (showDebugLog)
                Debug.Log("[InventorySystem] Добавлен SphereCollider триггер для подбора");
        }
        
        Debug.Log($"[InventorySystem] ✅ Косточка брошена от головы игрока: позиция {spawnPosition}, направление {throwDirection}, скорость {throwSpeed}");
        
        // Убираем косточку из инвентаря
        RemoveBone();
    }
}