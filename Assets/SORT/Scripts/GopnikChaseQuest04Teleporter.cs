using UnityEngine;
using System.Collections;

/// <summary>
/// Скрипт для телепортации игрока после активации Gopnik_Chase_quest_04
/// </summary>
public class GopnikChaseQuest04Teleporter : MonoBehaviour
{
    [Header("Настройки телепортации")]
    [Tooltip("Координаты для телепортации игрока")]
    public Vector3 teleportPosition = new Vector3(-186f, 2f, 46f);
    
    [Tooltip("Задержка перед телепортацией (в секундах)")]
    public float teleportDelay = 2f;
    
    [Tooltip("Тег объекта, который может активировать триггер")]
    public string targetTag = "Player";
    
    [Tooltip("Показывать сообщения в консоли")]
    public bool debugMessages = true;
    
    [Tooltip("Отключить этот триггер после активации")]
    public bool disableSelfAfterActivation = true;
    
    // Флаг для отслеживания, был ли активирован триггер
    private bool wasActivated = false;
    
    // Ссылка на игрока
    private GameObject player;
    
    private void Start()
    {
        // Находим игрока
        player = GameObject.FindGameObjectWithTag(targetTag);
        
        if (player == null)
        {
            if (debugMessages)
            {
                Debug.LogError($"[GopnikChaseQuest04Teleporter] ❌ Игрок с тегом '{targetTag}' не найден!");
            }
        }
        else
        {
            if (debugMessages)
            {
                Debug.Log($"[GopnikChaseQuest04Teleporter] ✅ Игрок найден: {player.name}");
                Debug.Log($"[GopnikChaseQuest04Teleporter] 📍 Целевые координаты: {teleportPosition}");
                Debug.Log($"[GopnikChaseQuest04Teleporter] ⏱️ Задержка телепортации: {teleportDelay} сек");
            }
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        // Проверяем, что объект с правильным тегом вошел в триггер и триггер ещё не был активирован
        if (other.CompareTag(targetTag) && !wasActivated)
        {
            if (debugMessages)
            {
                Debug.Log($"[GopnikChaseQuest04Teleporter] 🎯 Игрок вошел в триггер {gameObject.name}");
                Debug.Log($"[GopnikChaseQuest04Teleporter] ⏱️ Запланирована телепортация через {teleportDelay} секунд");
            }
            
            // Устанавливаем флаг, что триггер был активирован
            wasActivated = true;
            
            // Запускаем телепортацию с задержкой
            StartCoroutine(TeleportPlayerWithDelay());
            
            // Дублируем через Invoke для надежности
            Invoke(nameof(TeleportPlayerDirectly), teleportDelay + 0.1f);
            
            // НЕ отключаем объект сразу, чтобы корутина могла выполниться
            // Отключаем только коллайдер
            Collider triggerCollider = GetComponent<Collider>();
            if (triggerCollider != null)
            {
                triggerCollider.enabled = false;
            }
            
            // Отключаем объект с задержкой, чтобы корутина успела выполниться
            Invoke(nameof(DisableObject), teleportDelay + 1f);
        }
    }
    
    /// <summary>
    /// Телепортация игрока с задержкой
    /// </summary>
    private IEnumerator TeleportPlayerWithDelay()
    {
        if (debugMessages)
        {
            Debug.Log($"[GopnikChaseQuest04Teleporter] ⏱️ Начинаю ожидание {teleportDelay} секунд...");
        }
        
        yield return new WaitForSeconds(teleportDelay);
        
        if (debugMessages)
        {
            Debug.Log($"[GopnikChaseQuest04Teleporter] ⏱️ Задержка завершена, проверяю игрока...");
        }
        
        // Повторно находим игрока на случай, если ссылка потерялась
        if (player == null)
        {
            player = GameObject.FindGameObjectWithTag(targetTag);
            if (debugMessages)
            {
                Debug.Log($"[GopnikChaseQuest04Teleporter] 🔍 Повторный поиск игрока: {(player != null ? "найден" : "не найден")}");
            }
        }
        
        if (player != null)
        {
            if (debugMessages)
            {
                Debug.Log($"[GopnikChaseQuest04Teleporter] 🚀 Телепортирую игрока в позицию: {teleportPosition}");
                Debug.Log($"[GopnikChaseQuest04Teleporter] 📍 Текущая позиция: {player.transform.position}");
                Debug.Log($"[GopnikChaseQuest04Teleporter] ⚡ Игрок активен: {player.activeInHierarchy}");
            }
            
            // Проверяем, что игрок активен
            if (player.activeInHierarchy)
            {
                // Телепортируем игрока
                player.transform.position = teleportPosition;
                
                if (debugMessages)
                {
                    Debug.Log($"[GopnikChaseQuest04Teleporter] ✅ Игрок телепортирован в позицию: {player.transform.position}");
                }
            }
            else
            {
                if (debugMessages)
                {
                    Debug.LogWarning("[GopnikChaseQuest04Teleporter] ⚠️ Игрок неактивен, жду активации...");
                }
                
                // Ждем активации игрока
                yield return new WaitUntil(() => player.activeInHierarchy);
                
                if (debugMessages)
                {
                    Debug.Log("[GopnikChaseQuest04Teleporter] ✅ Игрок активирован, телепортирую...");
                }
                
                // Телепортируем игрока
                player.transform.position = teleportPosition;
                
                if (debugMessages)
                {
                    Debug.Log($"[GopnikChaseQuest04Teleporter] ✅ Игрок телепортирован в позицию: {player.transform.position}");
                }
            }
        }
        else
        {
            if (debugMessages)
            {
                Debug.LogError("[GopnikChaseQuest04Teleporter] ❌ Игрок не найден для телепортации!");
            }
        }
    }
    
    /// <summary>
    /// Прямая телепортация игрока (резервный метод)
    /// </summary>
    private void TeleportPlayerDirectly()
    {
        if (debugMessages)
        {
            Debug.Log($"[GopnikChaseQuest04Teleporter] 🔄 Резервная телепортация игрока...");
        }
        
        // Повторно находим игрока
        if (player == null)
        {
            player = GameObject.FindGameObjectWithTag(targetTag);
        }
        
        if (player != null && player.activeInHierarchy)
        {
            if (debugMessages)
            {
                Debug.Log($"[GopnikChaseQuest04Teleporter] 🚀 Резервная телепортация в позицию: {teleportPosition}");
            }
            
            player.transform.position = teleportPosition;
            
            if (debugMessages)
            {
                Debug.Log($"[GopnikChaseQuest04Teleporter] ✅ Резервная телепортация завершена: {player.transform.position}");
            }
        }
        else
        {
            if (debugMessages)
            {
                Debug.LogError("[GopnikChaseQuest04Teleporter] ❌ Резервная телепортация не удалась - игрок не найден или неактивен!");
            }
        }
    }
    
    /// <summary>
    /// Отключить объект с задержкой
    /// </summary>
    private void DisableObject()
    {
        if (disableSelfAfterActivation)
        {
            if (debugMessages)
            {
                Debug.Log($"[GopnikChaseQuest04Teleporter] 🔄 Отключаю объект {gameObject.name}");
            }
            gameObject.SetActive(false);
        }
    }
    
    /// <summary>
    /// Принудительная телепортация игрока (контекстное меню)
    /// </summary>
    [ContextMenu("Принудительно телепортировать игрока")]
    public void ForceTeleportPlayer()
    {
        if (player != null)
        {
            if (debugMessages)
            {
                Debug.Log($"[GopnikChaseQuest04Teleporter] 🚀 Принудительная телепортация игрока в позицию: {teleportPosition}");
            }
            
            player.transform.position = teleportPosition;
            
            if (debugMessages)
            {
                Debug.Log($"[GopnikChaseQuest04Teleporter] ✅ Игрок телепортирован в позицию: {player.transform.position}");
            }
        }
        else
        {
            if (debugMessages)
            {
                Debug.LogError("[GopnikChaseQuest04Teleporter] ❌ Игрок не найден!");
            }
        }
    }
    
    /// <summary>
    /// Сбросить состояние триггера (контекстное меню)
    /// </summary>
    [ContextMenu("Сбросить состояние триггера")]
    public void ResetTrigger()
    {
        wasActivated = false;
        gameObject.SetActive(true);
        
        if (debugMessages)
        {
            Debug.Log($"[GopnikChaseQuest04Teleporter] 🔄 Состояние триггера сброшено");
        }
    }
    
    /// <summary>
    /// Принудительно телепортировать игрока сейчас (контекстное меню)
    /// </summary>
    [ContextMenu("Телепортировать игрока сейчас")]
    public void TeleportPlayerNow()
    {
        if (debugMessages)
        {
            Debug.Log($"[GopnikChaseQuest04Teleporter] 🚀 Принудительная телепортация сейчас...");
        }
        
        TeleportPlayerDirectly();
    }
    
    /// <summary>
    /// Показать информацию о настройках (контекстное меню)
    /// </summary>
    [ContextMenu("Показать информацию")]
    public void ShowInfo()
    {
        if (debugMessages)
        {
            Debug.Log($"[GopnikChaseQuest04Teleporter] 📊 Информация о телепортере:");
            Debug.Log($"[GopnikChaseQuest04Teleporter] 📍 Имя: {gameObject.name}");
            Debug.Log($"[GopnikChaseQuest04Teleporter] 🎯 Целевой тег: {targetTag}");
            Debug.Log($"[GopnikChaseQuest04Teleporter] 📍 Целевые координаты: {teleportPosition}");
            Debug.Log($"[GopnikChaseQuest04Teleporter] ⏱️ Задержка телепортации: {teleportDelay} сек");
            Debug.Log($"[GopnikChaseQuest04Teleporter] ⚡ Активирован: {wasActivated}");
            Debug.Log($"[GopnikChaseQuest04Teleporter] 🔧 Отключить после активации: {disableSelfAfterActivation}");
            
            if (player != null)
            {
                Debug.Log($"[GopnikChaseQuest04Teleporter] 👤 Игрок найден: {player.name}");
                Debug.Log($"[GopnikChaseQuest04Teleporter] 📍 Текущая позиция игрока: {player.transform.position}");
            }
            else
            {
                Debug.Log($"[GopnikChaseQuest04Teleporter] ❌ Игрок не найден");
            }
        }
    }
    
    /// <summary>
    /// Отображение в редакторе
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        // Рисуем линию от триггера к целевой позиции
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, teleportPosition);
        
        // Рисуем сферу в целевой позиции
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(teleportPosition, 1f);
        
        // Рисуем куб в целевой позиции
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(teleportPosition, Vector3.one * 2f);
        
        #if UNITY_EDITOR
        // Подпись с координатами
        UnityEditor.Handles.Label(teleportPosition + Vector3.up * 3f, $"Телепорт\n{teleportPosition}");
        #endif
    }
}
