using UnityEngine;

/// <summary>
/// Простой скрипт для принудительной телепортации игрока
/// </summary>
public class ForceTeleporter : MonoBehaviour
{
    [Header("Настройки телепортации")]
    [Tooltip("Координаты для телепортации")]
    public Vector3 teleportPosition = new Vector3(-186f, 2f, 46f);
    
    [Tooltip("Показывать сообщения")]
    public bool debugMessages = true;
    
    private void Start()
    {
        if (debugMessages)
        {
            Debug.Log($"[ForceTeleporter] ✅ Принудительный телепортер инициализирован");
            Debug.Log($"[ForceTeleporter] 📍 Позиция телепортации: {teleportPosition}");
        }
    }
    
    /// <summary>
    /// Принудительно телепортировать игрока (контекстное меню)
    /// </summary>
    [ContextMenu("Телепортировать игрока")]
    public void TeleportPlayer()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        
        if (player != null)
        {
            if (debugMessages)
            {
                Debug.Log($"[ForceTeleporter] 🚀 Принудительная телепортация игрока");
                Debug.Log($"[ForceTeleporter] 📍 Текущая позиция: {player.transform.position}");
                Debug.Log($"[ForceTeleporter] 📍 Целевая позиция: {teleportPosition}");
                Debug.Log($"[ForceTeleporter] ⚡ Игрок активен: {player.activeInHierarchy}");
            }
            
            if (player.activeInHierarchy)
            {
                player.transform.position = teleportPosition;
                
                if (debugMessages)
                {
                    Debug.Log($"[ForceTeleporter] ✅ Игрок телепортирован в позицию: {player.transform.position}");
                }
            }
            else
            {
                if (debugMessages)
                {
                    Debug.LogWarning("[ForceTeleporter] ⚠️ Игрок неактивен, но телепортирую...");
                }
                
                player.transform.position = teleportPosition;
                
                if (debugMessages)
                {
                    Debug.Log($"[ForceTeleporter] ✅ Игрок телепортирован в позицию: {player.transform.position}");
                }
            }
        }
        else
        {
            if (debugMessages)
            {
                Debug.LogError("[ForceTeleporter] ❌ Игрок не найден!");
            }
        }
    }
    
    /// <summary>
    /// Показать информацию (контекстное меню)
    /// </summary>
    [ContextMenu("Показать информацию")]
    public void ShowInfo()
    {
        if (debugMessages)
        {
            Debug.Log($"[ForceTeleporter] 📊 Информация:");
            Debug.Log($"[ForceTeleporter] 📍 Имя: {gameObject.name}");
            Debug.Log($"[ForceTeleporter] 📍 Позиция телепортации: {teleportPosition}");
            
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                Debug.Log($"[ForceTeleporter] 👤 Игрок найден: {player.name}");
                Debug.Log($"[ForceTeleporter] 📍 Текущая позиция игрока: {player.transform.position}");
                Debug.Log($"[ForceTeleporter] ⚡ Игрок активен: {player.activeInHierarchy}");
            }
            else
            {
                Debug.Log($"[ForceTeleporter] ❌ Игрок не найден");
            }
        }
    }
    
    /// <summary>
    /// Отображение в редакторе
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        // Рисуем линию к целевой позиции
        Gizmos.color = Color.red;
        Gizmos.DrawLine(transform.position, teleportPosition);
        
        // Рисуем сферу в целевой позиции
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(teleportPosition, 1f);
        
        #if UNITY_EDITOR
        // Подпись с координатами
        UnityEditor.Handles.Label(teleportPosition + Vector3.up * 3f, $"Принудительный телепорт\n{teleportPosition}");
        #endif
    }
}



