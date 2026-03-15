using UnityEngine;
using System.Collections;

/// <summary>
/// Простой телепортер для Gopnik_Chase_quest_04
/// </summary>
public class GopnikChaseQuest04SimpleTeleporter : MonoBehaviour
{
    [Header("Настройки телепортации")]
    [Tooltip("Координаты для телепортации")]
    public Vector3 teleportPosition = new Vector3(-186f, 2f, 46f);
    
    [Tooltip("Задержка перед телепортацией")]
    public float teleportDelay = 2f;
    
    [Tooltip("Показывать сообщения")]
    public bool debugMessages = true;
    
    [Tooltip("Отключить после активации")]
    public bool disableAfterActivation = true;
    
    private bool wasActivated = false;
    
    private void Start()
    {
        if (debugMessages)
        {
            Debug.Log($"[GopnikChaseQuest04SimpleTeleporter] ✅ Простой телепортер инициализирован");
            Debug.Log($"[GopnikChaseQuest04SimpleTeleporter] 📍 Позиция: {teleportPosition}");
            Debug.Log($"[GopnikChaseQuest04SimpleTeleporter] ⏱️ Задержка: {teleportDelay} сек");
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !wasActivated)
        {
            if (debugMessages)
            {
                Debug.Log($"[GopnikChaseQuest04SimpleTeleporter] 🎯 Игрок вошел в триггер {gameObject.name}");
                Debug.Log($"[GopnikChaseQuest04SimpleTeleporter] ⏱️ Запланирована телепортация через {teleportDelay} сек");
            }
            
            wasActivated = true;
            
            // Отключаем коллайдер
            Collider triggerCollider = GetComponent<Collider>();
            if (triggerCollider != null)
            {
                triggerCollider.enabled = false;
            }
            
            // Запускаем телепортацию
            StartCoroutine(TeleportWithDelay());
            
            // Резервная телепортация
            Invoke(nameof(TeleportDirectly), teleportDelay + 0.1f);
        }
    }
    
    private IEnumerator TeleportWithDelay()
    {
        if (debugMessages)
        {
            Debug.Log($"[GopnikChaseQuest04SimpleTeleporter] ⏱️ Ожидание {teleportDelay} секунд...");
        }
        
        yield return new WaitForSeconds(teleportDelay);
        
        if (debugMessages)
        {
            Debug.Log($"[GopnikChaseQuest04SimpleTeleporter] ⏱️ Задержка завершена, телепортирую...");
        }
        
        TeleportPlayer();
    }
    
    private void TeleportDirectly()
    {
        if (debugMessages)
        {
            Debug.Log($"[GopnikChaseQuest04SimpleTeleporter] 🔄 Резервная телепортация...");
        }
        
        TeleportPlayer();
    }
    
    private void TeleportPlayer()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        
        if (player != null)
        {
            if (debugMessages)
            {
                Debug.Log($"[GopnikChaseQuest04SimpleTeleporter] 🚀 Телепортирую игрока в {teleportPosition}");
                Debug.Log($"[GopnikChaseQuest04SimpleTeleporter] 📍 Текущая позиция: {player.transform.position}");
                Debug.Log($"[GopnikChaseQuest04SimpleTeleporter] ⚡ Игрок активен: {player.activeInHierarchy}");
            }
            
            if (player.activeInHierarchy)
            {
                player.transform.position = teleportPosition;
                
                if (debugMessages)
                {
                    Debug.Log($"[GopnikChaseQuest04SimpleTeleporter] ✅ Игрок телепортирован в {player.transform.position}");
                }
            }
            else
            {
                if (debugMessages)
                {
                    Debug.LogWarning("[GopnikChaseQuest04SimpleTeleporter] ⚠️ Игрок неактивен!");
                }
                
                // Пытаемся телепортировать даже неактивного игрока
                player.transform.position = teleportPosition;
                
                if (debugMessages)
                {
                    Debug.Log($"[GopnikChaseQuest04SimpleTeleporter] ✅ Игрок телепортирован (неактивный) в {player.transform.position}");
                }
            }
        }
        else
        {
            if (debugMessages)
            {
                Debug.LogError("[GopnikChaseQuest04SimpleTeleporter] ❌ Игрок не найден!");
            }
        }
        
        // Отключаем объект с задержкой
        if (disableAfterActivation)
        {
            Invoke(nameof(DisableObject), 1f);
        }
    }
    
    private void DisableObject()
    {
        if (debugMessages)
        {
            Debug.Log($"[GopnikChaseQuest04SimpleTeleporter] 🔄 Отключаю объект {gameObject.name}");
        }
        gameObject.SetActive(false);
    }
    
    [ContextMenu("Телепортировать игрока сейчас")]
    public void TeleportNow()
    {
        if (debugMessages)
        {
            Debug.Log("[GopnikChaseQuest04SimpleTeleporter] 🚀 Принудительная телепортация");
        }
        
        TeleportPlayer();
    }
    
    [ContextMenu("Показать информацию")]
    public void ShowInfo()
    {
        if (debugMessages)
        {
            Debug.Log($"[GopnikChaseQuest04SimpleTeleporter] 📊 Информация:");
            Debug.Log($"[GopnikChaseQuest04SimpleTeleporter] 📍 Имя: {gameObject.name}");
            Debug.Log($"[GopnikChaseQuest04SimpleTeleporter] 📍 Позиция: {teleportPosition}");
            Debug.Log($"[GopnikChaseQuest04SimpleTeleporter] ⏱️ Задержка: {teleportDelay} сек");
            Debug.Log($"[GopnikChaseQuest04SimpleTeleporter] ⚡ Активирован: {wasActivated}");
            
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                Debug.Log($"[GopnikChaseQuest04SimpleTeleporter] 👤 Игрок: {player.name}");
                Debug.Log($"[GopnikChaseQuest04SimpleTeleporter] 📍 Позиция игрока: {player.transform.position}");
                Debug.Log($"[GopnikChaseQuest04SimpleTeleporter] ⚡ Игрок активен: {player.activeInHierarchy}");
            }
            else
            {
                Debug.Log($"[GopnikChaseQuest04SimpleTeleporter] ❌ Игрок не найден");
            }
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        // Рисуем линию к целевой позиции
        Gizmos.color = Color.magenta;
        Gizmos.DrawLine(transform.position, teleportPosition);
        
        // Рисуем сферу в целевой позиции
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(teleportPosition, 1f);
        
        #if UNITY_EDITOR
        // Подпись с координатами
        UnityEditor.Handles.Label(teleportPosition + Vector3.up * 3f, $"Простой телепорт\n{teleportPosition}");
        #endif
    }
}



