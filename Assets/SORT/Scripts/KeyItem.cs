using UnityEngine;
using UnityEngine.UI;
using Invector.vCharacterController;

public class KeyItem : MonoBehaviour
{
    [Header("Настройки ключа")]
    [Tooltip("Название ключа")]
    public string keyName = "Гаражный ключ";
    
    [Tooltip("Описание ключа")]
    public string keyDescription = "Ключ от желтого гаража";
    
    [Tooltip("ID ключа (должен совпадать с ID в KeySpawner)")]
    public string keyID = "garage_key_01";
    
    [Header("Визуальные эффекты")]
    [Tooltip("Вращение ключа")]
    public float rotationSpeed = 90f;
    
    [Tooltip("Пульсация ключа")]
    public float pulseSpeed = 2f;
    
    [Tooltip("Амплитуда пульсации")]
    public float pulseAmplitude = 0.2f;
    
    [Header("Звуковые эффекты")]
    [Tooltip("Звук подбора ключа")]
    public AudioClip pickupSound;
    
    [Tooltip("Громкость звука подбора")]
    [Range(0f, 1f)]
    public float pickupVolume = 0.8f;
    
    [Header("Уведомления")]
    [Tooltip("Полный текст уведомления при подборе ключа (оставьте пустым для стандартного формата)")]
    public string customNotificationText = "";
    
    [Tooltip("Использовать только название ключа в уведомлении (если customNotificationText пустой)")]
    public bool useKeyNameOnly = false;
    
    [Header("UI уведомления (в стиле KeySpawner)")]
    [Tooltip("Показывать UI уведомление при подборе ключа")]
    public bool showUINotification = true;
    
    [Tooltip("Ваш собственный TextMeshPro объект для уведомления (если не назначен, создается автоматически)")]
    public TMPro.TextMeshProUGUI customNotificationUI;
    
    [Tooltip("Текст для UI уведомления")]
    public string uiNotificationText = "Ключ поднят!";
    
    [Tooltip("Время показа UI уведомления")]
    public float uiNotificationDuration = 2f;
    
    private GameObject notificationUI;
    private TMPro.TextMeshProUGUI notificationText;
    
    [Header("Отладка")]
    [Tooltip("Показывать отладочную информацию")]
    public bool showDebugLog = true;
    
    private Vector3 initialScale;
    private float startTime;
    private bool isCollected = false;
    
    private void Start()
    {
        initialScale = transform.localScale;
        startTime = Time.time;
        
        if (showDebugLog)
            Debug.Log($"[KeyItem] 🔑 Ключ '{keyName}' создан с ID: {keyID}");
        
        // Создаем UI уведомления если нужно
        if (showUINotification)
        {
            CreateNotificationUI();
        }
    }
    
    private void Update()
    {
        if (isCollected) return;
        
        // Вращение ключа
        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);
        
        // Пульсация
        float elapsed = Time.time - startTime;
        float pulse = 1f + Mathf.Sin(elapsed * pulseSpeed) * pulseAmplitude;
        transform.localScale = initialScale * pulse;
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (isCollected) return;
        
        // Проверяем, что это игрок
        if (other.CompareTag("Player"))
        {
            CollectKey(other.gameObject);
        }
    }
    
    /// <summary>
    /// Подбирает ключ
    /// </summary>
    private void CollectKey(GameObject player)
    {
        if (isCollected) return;
        
        isCollected = true;
        
        // Воспроизводим звук подбора
        PlayPickupSound();
        
        // Добавляем ключ в инвентарь игрока
        KeyInventory keyInventory = player.GetComponent<KeyInventory>();
        if (keyInventory != null)
        {
            bool added = keyInventory.AddKey(keyID, keyName, keyDescription);
            
            if (showDebugLog)
            {
                if (added)
                    Debug.Log($"[KeyItem] ✅ Ключ '{keyName}' успешно добавлен в инвентарь игрока");
                else
                    Debug.Log($"[KeyItem] ⚠️ Ключ '{keyName}' не был добавлен в инвентарь (возможно, уже есть)");
            }
            
                            // Показываем уведомление о подборе ключа
                if (added)
                {
                    if (showDebugLog)
                        Debug.Log($"[KeyItem] ✅ Ключ успешно добавлен в инвентарь");
                    
                    // Показываем UI уведомление (в стиле KeySpawner)
                    ShowUINotification();
                    
                    // Показываем уведомление через NotificationManager (если доступен)
                    if (NotificationManager.Instance != null)
                    {
                        string notificationMessage;
                        
                        // Определяем текст уведомления
                        if (!string.IsNullOrEmpty(customNotificationText))
                        {
                            // Используем полностью настраиваемый текст
                            notificationMessage = customNotificationText;
                        }
                        else if (useKeyNameOnly)
                        {
                            // Используем только название ключа
                            notificationMessage = keyName;
                        }
                        else
                        {
                            // Стандартный формат
                            notificationMessage = $"🔑 Подобран ключ: {keyName}";
                        }
                        
                        if (showDebugLog)
                        {
                            Debug.Log($"[KeyItem] 📢 Показываю уведомление через NotificationManager: '{notificationMessage}'");
                        }
                        
                        NotificationManager.Instance.ShowNotification(notificationMessage, 1f);
                    }
                    else
                    {
                        if (showDebugLog)
                            Debug.Log($"[KeyItem] ℹ️ NotificationManager недоступен, показываю только UI уведомление");
                    }
                }
        }
        else
        {
            Debug.LogError($"[KeyItem] ❌ Компонент KeyInventory не найден на игроке '{player.name}'!");
            Debug.LogError($"[KeyItem] ❌ Убедитесь, что на игроке добавлен компонент KeyInventory!");
        }
        
        // Скрываем ключ
        gameObject.SetActive(false);
        
        if (showDebugLog)
            Debug.Log($"[KeyItem] 🔑 Ключ '{keyName}' поднят игроком");
    }
    
    /// <summary>
    /// Создает UI для уведомлений (в стиле KeySpawner)
    /// </summary>
    private void CreateNotificationUI()
    {
        if (notificationUI == null)
        {
            // Проверяем, назначен ли пользовательский TextMeshPro объект
            if (customNotificationUI != null)
            {
                // Используем пользовательский объект
                notificationUI = customNotificationUI.gameObject;
                notificationText = customNotificationUI;
                
                if (showDebugLog)
                    Debug.Log($"[KeyItem] ✅ Используется пользовательский TextMeshPro объект: {customNotificationUI.name}");
            }
            else
            {
                // Создаем автоматически
                Canvas canvas = FindObjectOfType<Canvas>();
                if (canvas == null)
                {
                    GameObject canvasObj = new GameObject("KeyItemNotificationCanvas");
                    canvas = canvasObj.AddComponent<Canvas>();
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    canvasObj.AddComponent<CanvasScaler>();
                    canvasObj.AddComponent<GraphicRaycaster>();
                }
                
                // Создаем UI для уведомления
                notificationUI = new GameObject("KeyItemNotification");
                notificationUI.transform.SetParent(canvas.transform, false);
                
                // Добавляем текст
                notificationText = notificationUI.AddComponent<TMPro.TextMeshProUGUI>();
                notificationText.text = uiNotificationText;
                notificationText.fontSize = 32;
                notificationText.alignment = TMPro.TextAlignmentOptions.Center;
                notificationText.color = Color.white;
                
                // Позиционируем в центре экрана
                RectTransform rect = notificationUI.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                
                if (showDebugLog)
                    Debug.Log($"[KeyItem] ✅ UI уведомления создан автоматически");
            }
            
            // Убеждаемся что UI скрыт при старте
            notificationUI.SetActive(false);
        }
    }
    
    /// <summary>
    /// Показывает UI уведомление
    /// </summary>
    private void ShowUINotification()
    {
        if (!showUINotification || notificationUI == null) return;
        
        // Останавливаем предыдущую корутину если она запущена
        StopAllCoroutines();
        
        // Устанавливаем текст уведомления
        if (notificationText != null)
        {
            notificationText.text = uiNotificationText;
        }
        
        if (showDebugLog)
            Debug.Log($"[KeyItem] 🎬 Показываю UI уведомление: '{uiNotificationText}' на {uiNotificationDuration}с");
        
        notificationUI.SetActive(true);
        
        // Скрываем уведомление через указанное время (два способа для надежности)
        StartCoroutine(HideUINotificationAfterDelay(uiNotificationDuration));
        Invoke(nameof(HideUINotification), uiNotificationDuration);
    }
    
    /// <summary>
    /// Корутина для скрытия UI уведомления
    /// </summary>
    private System.Collections.IEnumerator HideUINotificationAfterDelay(float delay)
    {
        if (showDebugLog)
            Debug.Log($"[KeyItem] ⏰ Запущена корутина скрытия через {delay}с");
        
        yield return new WaitForSeconds(delay);
        
        if (showDebugLog)
            Debug.Log($"[KeyItem] ⏰ Время истекло, скрываю уведомление");
        
        if (notificationUI != null)
        {
            notificationUI.SetActive(false);
            if (showDebugLog)
                Debug.Log($"[KeyItem] ✅ UI уведомление скрыто");
        }
        else
        {
            if (showDebugLog)
                Debug.LogWarning($"[KeyItem] ⚠️ notificationUI равен null при попытке скрытия");
        }
    }
    
    /// <summary>
    /// Скрывает UI уведомление (вызывается через Invoke)
    /// </summary>
    private void HideUINotification()
    {
        if (notificationUI != null)
        {
            notificationUI.SetActive(false);
            if (showDebugLog)
                Debug.Log($"[KeyItem] ✅ UI уведомление скрыто через Invoke");
        }
    }
    
    /// <summary>
    /// Воспроизводит звук подбора
    /// </summary>
    private void PlayPickupSound()
    {
        if (pickupSound == null) return;
        
        // Создаем временный AudioSource
        GameObject audioObject = new GameObject("KeyPickupSound_Temp");
        audioObject.transform.position = transform.position;
        
        AudioSource audioSource = audioObject.AddComponent<AudioSource>();
        audioSource.clip = pickupSound;
        audioSource.volume = pickupVolume;
        audioSource.spatialBlend = 1f; // 3D звук
        audioSource.rolloffMode = AudioRolloffMode.Linear;
        audioSource.maxDistance = 20f;
        
        audioSource.Play();
        
        if (showDebugLog)
            Debug.Log($"[KeyItem] 🔊 Звук подбора воспроизведен: {pickupSound.name}");
        
        // Уничтожаем объект после воспроизведения
        Destroy(audioObject, pickupSound.length + 0.1f);
    }
    
    private void OnDrawGizmos()
    {
        // Рисуем область подбора
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 1f);
        
        // Рисуем иконку ключа
        Gizmos.color = Color.yellow;
        Gizmos.DrawCube(transform.position, Vector3.one * 0.5f);
    }
}
