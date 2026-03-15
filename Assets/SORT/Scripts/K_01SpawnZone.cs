using UnityEngine;

/// <summary>
/// Определяет зону, в которой будут спавниться объекты K_01
/// </summary>
[RequireComponent(typeof(Collider))]
public class K_01SpawnZone : MonoBehaviour
{
    [Tooltip("Спавнер, который будет активироваться в этой зоне")]
    public GameObject spawnerObject;
    
    [HideInInspector]
    public MonoBehaviour spawner;
    
    [Tooltip("Включить подсветку зоны в редакторе")]
    public bool showInEditor = true;
    
    private Collider zoneCollider;
    
    /// <summary>
    /// Проверяет, имеет ли GameObject один из компонентов спавнера
    /// </summary>
    private bool TryGetSpawnerComponent()
    {
        if (spawnerObject == null) return false;
        
        // Пробуем найти компонент K_01Spawner
        spawner = spawnerObject.GetComponent<K_01Spawner>();
        if (spawner != null) return true;
        
        // Если не нашли, пробуем найти K_01FixedSpawner
        spawner = spawnerObject.GetComponent<K_01FixedSpawner>();
        if (spawner != null) return true;
        
        return false;
    }
    
    void Start()
    {
        // Получаем коллайдер зоны и проверяем, что он настроен как триггер
        zoneCollider = GetComponent<Collider>();
        if (!zoneCollider.isTrigger)
        {
            Debug.LogWarning("K_01SpawnZone: Коллайдер не является триггером. Автоматически устанавливаем isTrigger = true");
            zoneCollider.isTrigger = true;
        }
        
        // Проверяем, указан ли объект спавнера
        if (spawnerObject == null)
        {
            Debug.LogError("K_01SpawnZone: Не указан GameObject с компонентом спавнера");
            enabled = false;
            return;
        }
        
        // Пытаемся получить компонент спавнера из указанного объекта
        if (!TryGetSpawnerComponent())
        {
            Debug.LogError($"K_01SpawnZone: Объект {spawnerObject.name} не имеет компонентов K_01Spawner или K_01FixedSpawner");
            enabled = false;
            return;
        }
    }
    
    void OnTriggerEnter(Collider other)
    {
        // Проверяем, входит ли игрок в зону
        if (other.CompareTag("Player"))
        {
            // Активируем событие на спавнере
            if (spawner != null)
            {
                SendPlayerEnteredEventToSpawner(other.gameObject);
            }
        }
    }
    
    void OnTriggerExit(Collider other)
    {
        // Проверяем, выходит ли игрок из зоны
        if (other.CompareTag("Player"))
        {
            // Активируем событие на спавнере
            if (spawner != null)
            {
                SendPlayerExitedEventToSpawner(other.gameObject);
            }
        }
    }
    
    /// <summary>
    /// Отправляет событие входа игрока в зону соответствующему спавнеру
    /// </summary>
    private void SendPlayerEnteredEventToSpawner(GameObject player)
    {
        if (spawner is K_01Spawner)
        {
            // Используем SendMessage вместо прямого вызова protected метода
            spawner.SendMessage("OnTriggerEnter", player.GetComponent<Collider>(), SendMessageOptions.DontRequireReceiver);
        }
        else if (spawner is K_01FixedSpawner)
        {
            // Вызываем метод SendMessage для обработки события
            spawner.SendMessage("OnTriggerEnter", player.GetComponent<Collider>(), SendMessageOptions.DontRequireReceiver);
        }
    }
    
    /// <summary>
    /// Отправляет событие выхода игрока из зоны соответствующему спавнеру
    /// </summary>
    private void SendPlayerExitedEventToSpawner(GameObject player)
    {
        if (spawner is K_01Spawner)
        {
            // Используем SendMessage вместо прямого вызова protected метода
            spawner.SendMessage("OnTriggerExit", player.GetComponent<Collider>(), SendMessageOptions.DontRequireReceiver);
        }
        else if (spawner is K_01FixedSpawner)
        {
            // Вызываем метод SendMessage для обработки события
            spawner.SendMessage("OnTriggerExit", player.GetComponent<Collider>(), SendMessageOptions.DontRequireReceiver);
        }
    }
    
    void OnDrawGizmos()
    {
        if (!showInEditor) return;
        
        // Рисуем контур зоны спавна
        Collider coll = GetComponent<Collider>();
        if (coll != null)
        {
            // Получаем границы коллайдера
            Bounds bounds = coll.bounds;
            
            // Устанавливаем цвет для зоны
            Gizmos.color = new Color(0, 0.5f, 1, 0.3f);
            
            // Рисуем куб для визуализации зоны
            Gizmos.DrawCube(bounds.center, bounds.size);
            
            // Рисуем контур
            Gizmos.color = new Color(0, 0.7f, 1, 0.8f);
            Gizmos.DrawWireCube(bounds.center, bounds.size);
            
            // Отображаем связь со спавнером
            if (spawnerObject != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(bounds.center, spawnerObject.transform.position);
            }
        }
        else
        {
            // Просто рисуем значок, если коллайдер не настроен
            Gizmos.color = Color.cyan;
            Gizmos.DrawIcon(transform.position, "SpawnArea");
        }
    }
} 