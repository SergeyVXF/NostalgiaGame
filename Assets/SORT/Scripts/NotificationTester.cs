using UnityEngine;

public class NotificationTester : MonoBehaviour
{
    [Header("Тестирование")]
    [Tooltip("Тестовое сообщение")]
    public string testMessage = "🧪 Тестовое уведомление!";
    
    [Tooltip("Время отображения")]
    public float testDuration = 3f;
    
    [Header("Отладка")]
    [Tooltip("Показывать отладочную информацию")]
    public bool showDebugLog = true;
    
    private void Start()
    {
        if (showDebugLog)
            Debug.Log("[NotificationTester] 🚀 NotificationTester запущен");
    }
    
    private void Update()
    {
        // Нажмите T для тестирования уведомления
        if (Input.GetKeyDown(KeyCode.T))
        {
            TestNotification();
        }
        
        // Нажмите Y для проверки NotificationManager
        if (Input.GetKeyDown(KeyCode.Y))
        {
            CheckNotificationManager();
        }
    }
    
    /// <summary>
    /// Тестирует уведомление
    /// </summary>
    [ContextMenu("Тест уведомления")]
    public void TestNotification()
    {
        if (showDebugLog)
            Debug.Log("[NotificationTester] 🧪 Запускаю тест уведомления...");
        
        if (NotificationManager.Instance != null)
        {
            NotificationManager.Instance.ShowNotification(testMessage, testDuration);
            
            if (showDebugLog)
                Debug.Log($"[NotificationTester] ✅ Уведомление отправлено: '{testMessage}'");
        }
        else
        {
            if (showDebugLog)
                Debug.LogError("[NotificationTester] ❌ NotificationManager.Instance равен null!");
        }
    }
    
    /// <summary>
    /// Проверяет статус NotificationManager
    /// </summary>
    [ContextMenu("Проверить NotificationManager")]
    public void CheckNotificationManager()
    {
        if (showDebugLog)
            Debug.Log("[NotificationTester] 🔍 Проверяю NotificationManager...");
        
        if (NotificationManager.Instance != null)
        {
            Debug.Log("[NotificationTester] ✅ NotificationManager.Instance найден");
            
            // Проверяем компоненты
            var manager = NotificationManager.Instance;
            Debug.Log($"[NotificationTester] 📊 notificationUI: {(manager.notificationUI != null ? "найден" : "null")}");
            Debug.Log($"[NotificationTester] 📊 notificationText: {(manager.notificationText != null ? "найден" : "null")}");
        }
        else
        {
            Debug.LogError("[NotificationTester] ❌ NotificationManager.Instance равен null!");
            Debug.LogError("[NotificationTester] ❌ Убедитесь, что в сцене есть GameObject с компонентом NotificationManager!");
        }
    }
}
