using UnityEngine;
using TMPro;
using System.Collections;

public class NotificationManager : MonoBehaviour
{
    private static NotificationManager instance;
    public static NotificationManager Instance => instance;
    
    [Header("UI элементы")]
    [Tooltip("UI объект для отображения уведомлений (создается автоматически если не назначен)")]
    public GameObject notificationUI;
    
    [Tooltip("Текст уведомления (создается автоматически если не назначен)")]
    public TextMeshProUGUI notificationText;
    
    [Header("Настройки")]
    [Tooltip("Время отображения уведомления (секунды)")]
    public float displayTime = 1f;
    
    [Tooltip("Цвет текста")]
    public Color textColor = Color.white;
    
    [Tooltip("Размер шрифта")]
    public int fontSize = 24;
    
    [Header("Отладка")]
    [Tooltip("Показывать отладочную информацию")]
    public bool showDebugLog = true;
    
    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            
            if (showDebugLog)
                Debug.Log("[NotificationManager] ✅ NotificationManager создан и установлен как Instance");
        }
        else
        {
            if (showDebugLog)
                Debug.Log("[NotificationManager] ⚠️ Дубликат NotificationManager уничтожен");
            Destroy(gameObject);
        }
    }
    
    private void Start()
    {
        if (showDebugLog)
            Debug.Log("[NotificationManager] 🚀 Start() вызван");
        
        CreateNotificationUI();
    }
    
    private void CreateNotificationUI()
    {
        if (showDebugLog)
            Debug.Log("[NotificationManager] 🔧 Создаю UI уведомлений...");
        
        if (notificationUI == null)
        {
            // Создаем Canvas если его нет
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                if (showDebugLog)
                    Debug.Log("[NotificationManager] 🔧 Canvas не найден, создаю новый...");
                
                GameObject canvasObj = new GameObject("NotificationCanvas");
                canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
                canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();
                
                if (showDebugLog)
                    Debug.Log("[NotificationManager] ✅ Новый Canvas создан");
            }
            else
            {
                if (showDebugLog)
                    Debug.Log($"[NotificationManager] ✅ Найден существующий Canvas: {canvas.name}");
            }
            
            // Создаем UI для уведомлений
            notificationUI = new GameObject("NotificationUI");
            notificationUI.transform.SetParent(canvas.transform, false);
            
            // Добавляем текст
            notificationText = notificationUI.AddComponent<TextMeshProUGUI>();
            notificationText.text = "Тестовый текст";
            notificationText.fontSize = fontSize;
            notificationText.alignment = TextAlignmentOptions.Center;
            notificationText.color = textColor;
            
            // Позиционируем в верхней части экрана
            RectTransform rect = notificationUI.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.8f);
            rect.anchorMax = new Vector2(0.5f, 0.8f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(600, 100);
            
            notificationUI.SetActive(false);
            
            if (showDebugLog)
            {
                Debug.Log($"[NotificationManager] ✅ UI уведомлений создан автоматически");
                Debug.Log($"[NotificationManager] 📍 Позиция: {rect.anchoredPosition}");
                Debug.Log($"[NotificationManager] 📏 Размер: {rect.sizeDelta}");
                Debug.Log($"[NotificationManager] 🎨 Цвет текста: {textColor}");
                Debug.Log($"[NotificationManager] 📝 Размер шрифта: {fontSize}");
            }
        }
        else
        {
            if (showDebugLog)
                Debug.Log("[NotificationManager] ✅ UI уведомлений уже существует");
        }
    }
    
    /// <summary>
    /// Показывает уведомление на экране
    /// </summary>
    /// <param name="message">Текст уведомления</param>
    public void ShowNotification(string message)
    {
        ShowNotification(message, displayTime);
    }
    
    /// <summary>
    /// Показывает уведомление на экране с указанным временем отображения
    /// </summary>
    /// <param name="message">Текст уведомления</param>
    /// <param name="duration">Время отображения в секундах</param>
    public void ShowNotification(string message, float duration)
    {
        if (showDebugLog)
            Debug.Log($"[NotificationManager] 📢 ShowNotification вызван с сообщением: '{message}' на {duration}с");
        
        if (notificationUI == null)
        {
            if (showDebugLog)
                Debug.Log("[NotificationManager] ⚠️ notificationUI равен null, создаю заново...");
            CreateNotificationUI();
        }
        
        if (notificationText != null)
        {
            notificationText.text = message;
            notificationUI.SetActive(true);
            
            if (showDebugLog)
            {
                Debug.Log($"[NotificationManager] 📢 Показываю уведомление: '{message}' на {duration}с");
                Debug.Log($"[NotificationManager] ✅ notificationUI.activeSelf: {notificationUI.activeSelf}");
                Debug.Log($"[NotificationManager] ✅ notificationText.text: '{notificationText.text}'");
            }
            
            // Запускаем корутину для скрытия уведомления
            StartCoroutine(HideNotificationAfterDelay(duration));
        }
        else
        {
            if (showDebugLog)
                Debug.LogError("[NotificationManager] ❌ notificationText равен null!");
        }
    }
    
    /// <summary>
    /// Корутина для скрытия уведомления через указанное время
    /// </summary>
    private IEnumerator HideNotificationAfterDelay(float delay)
    {
        if (showDebugLog)
            Debug.Log($"[NotificationManager] ⏰ Ожидаю {delay}с перед скрытием уведомления");
        
        yield return new WaitForSeconds(delay);
        
        if (notificationUI != null)
        {
            notificationUI.SetActive(false);
            
            if (showDebugLog)
                Debug.Log($"[NotificationManager] 📢 Уведомление скрыто");
        }
        else
        {
            if (showDebugLog)
                Debug.LogError("[NotificationManager] ❌ notificationUI равен null при скрытии!");
        }
    }
    
    /// <summary>
    /// Скрывает уведомление немедленно
    /// </summary>
    public void HideNotification()
    {
        if (notificationUI != null)
        {
            notificationUI.SetActive(false);
            
            if (showDebugLog)
                Debug.Log($"[NotificationManager] 📢 Уведомление скрыто принудительно");
        }
        else
        {
            if (showDebugLog)
                Debug.LogError("[NotificationManager] ❌ notificationUI равен null при принудительном скрытии!");
        }
    }
    
    /// <summary>
    /// Тестовый метод для проверки работы уведомлений
    /// </summary>
    [ContextMenu("Тест уведомления")]
    public void TestNotification()
    {
        if (showDebugLog)
            Debug.Log("[NotificationManager] 🧪 Запускаю тест уведомления...");
        
        ShowNotification("🧪 Тестовое уведомление!", 3f);
    }
}
