using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Спавнер объектов K_01 перед игроком с указанными ограничениями
/// </summary>
public class K_01Spawner : MonoBehaviour
{
    [Header("Параметры спавна")]
    [Tooltip("Префаб объекта K_01 для спавна")]
    public GameObject k01Prefab;
    
    [Tooltip("Максимальное количество объектов K_01 (ограничение: 100)")]
    [Range(1, 100)]
    public int maxObjects = 100;
    
    [Tooltip("Минимальное расстояние между спавнящимися объектами")]
    public float minDistanceBetweenObjects = 0.35f;
    
    [Tooltip("Дистанция спавна перед игроком")]
    public float spawnDistance = 2f;
    
    [Tooltip("Ширина области спавна перед игроком")]
    public float spawnWidth = 1f;
    
    [Header("Настройки спавна")]
    [Tooltip("Слои, которые блокируют спавн")]
    public LayerMask blockingLayers;
    
    [Tooltip("Задержка между попытками спавна (в секундах)")]
    public float spawnDelay = 0.5f;
    
    [Header("Отладка")]
    [Tooltip("Включить отладочные сообщения")]
    public bool debugMode = false;
    
    // Приватные переменные
    private List<GameObject> spawnedObjects = new List<GameObject>();
    private List<Vector3> spawnPositions = new List<Vector3>();
    private float timeSinceLastSpawn = 0f;
    private Transform playerTransform;
    private bool playerInSpawnZone = false;
    
    void Start()
    {
        // Находим игрока в сцене
        playerTransform = GameObject.FindGameObjectWithTag("Player")?.transform;
        
        if (playerTransform == null)
        {
            Debug.LogError("K_01Spawner: Не найден игрок с тегом 'Player'");
            enabled = false;
            return;
        }
        
        if (k01Prefab == null)
        {
            Debug.LogError("K_01Spawner: Не указан префаб K_01");
            enabled = false;
            return;
        }
    }
    
    void Update()
    {
        if (!playerInSpawnZone || playerTransform == null) return;
        
        // Проверяем, движется ли игрок вперед
        bool isMovingForward = Vector3.Dot(playerTransform.forward, playerTransform.GetComponent<Rigidbody>()?.linearVelocity ?? Vector3.zero) > 0.1f;
        
        if (!isMovingForward)
        {
            if (debugMode)
                Debug.Log("K_01Spawner: Игрок не движется вперед");
            return;
        }
        
        // Обновляем таймер спавна
        timeSinceLastSpawn += Time.deltaTime;
        
        // Проверяем, прошло ли достаточно времени с последнего спавна
        if (timeSinceLastSpawn >= spawnDelay)
        {
            // Проверяем, не достигли ли мы лимита объектов
            if (spawnedObjects.Count < maxObjects)
            {
                TrySpawnObject();
                timeSinceLastSpawn = 0f;
            }
            else if (debugMode)
            {
                Debug.Log($"K_01Spawner: Достигнут лимит объектов ({maxObjects})");
            }
        }
        
        // Очищаем список от уничтоженных объектов
        CleanupDestroyedObjects();
    }
    
    /// <summary>
    /// Попытка спавна нового объекта перед игроком
    /// </summary>
    private void TrySpawnObject()
    {
        // Получаем позицию перед игроком
        Vector3 spawnPos = GetSpawnPositionInFrontOfPlayer();
        
        // Проверяем, можно ли спавнить объект на этой позиции
        if (CanSpawnAtPosition(spawnPos))
        {
            // Спавним объект
            GameObject newObject = Instantiate(k01Prefab, spawnPos, Quaternion.identity);
            
            // Добавляем объект и его позицию в списки
            spawnedObjects.Add(newObject);
            spawnPositions.Add(spawnPos);
            
            if (debugMode)
                Debug.Log($"K_01Spawner: Создан объект на позиции {spawnPos}, всего: {spawnedObjects.Count}");
        }
        else if (debugMode)
        {
            Debug.Log($"K_01Spawner: Не удалось создать объект, найдено препятствие или слишком близко к другим объектам");
        }
    }
    
    /// <summary>
    /// Получает позицию спавна перед игроком
    /// </summary>
    private Vector3 GetSpawnPositionInFrontOfPlayer()
    {
        // Базовая позиция перед игроком
        Vector3 forward = playerTransform.forward;
        forward.y = 0; // Обнуляем Y компонент для спавна на одной высоте
        forward.Normalize();
        
        // Добавляем случайное смещение по ширине
        Vector3 right = playerTransform.right;
        float randomOffset = Random.Range(-spawnWidth / 2, spawnWidth / 2);
        
        // Вычисляем итоговую позицию
        Vector3 spawnDirection = forward + right * randomOffset;
        spawnDirection.Normalize();
        
        // Получаем позицию на земле
        Vector3 basePosition = playerTransform.position + spawnDirection * spawnDistance;
        
        // Опускаем луч вниз для нахождения земли
        RaycastHit hit;
        if (Physics.Raycast(basePosition + Vector3.up * 2, Vector3.down, out hit, 4f, ~blockingLayers))
        {
            return hit.point;
        }
        
        // Если земля не найдена, используем базовую высоту
        return new Vector3(basePosition.x, playerTransform.position.y, basePosition.z);
    }
    
    /// <summary>
    /// Проверяет, можно ли спавнить объект на указанной позиции
    /// </summary>
    private bool CanSpawnAtPosition(Vector3 position)
    {
        // Проверяем расстояние до других спавненных объектов
        foreach (Vector3 existingPos in spawnPositions)
        {
            float distance = Vector3.Distance(position, existingPos);
            if (distance < minDistanceBetweenObjects)
            {
                if (debugMode)
                    Debug.Log($"K_01Spawner: Слишком близко к другому объекту ({distance} < {minDistanceBetweenObjects})");
                return false;
            }
        }
        
        // Проверяем, нет ли препятствий на позиции спавна
        Collider[] colliders = Physics.OverlapSphere(position, 0.1f, blockingLayers);
        return colliders.Length == 0;
    }
    
    /// <summary>
    /// Очищает список от уничтоженных объектов
    /// </summary>
    private void CleanupDestroyedObjects()
    {
        for (int i = spawnedObjects.Count - 1; i >= 0; i--)
        {
            if (spawnedObjects[i] == null)
            {
                spawnPositions.RemoveAt(i);
                spawnedObjects.RemoveAt(i);
            }
        }
    }
    
    /// <summary>
    /// Вызывается при входе игрока в зону спавна
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInSpawnZone = true;
            if (debugMode)
                Debug.Log("K_01Spawner: Игрок вошел в зону спавна");
        }
    }
    
    /// <summary>
    /// Вызывается при выходе игрока из зоны спавна
    /// </summary>
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInSpawnZone = false;
            if (debugMode)
                Debug.Log("K_01Spawner: Игрок вышел из зоны спавна");
        }
    }
    
    /// <summary>
    /// Отображение визуальных элементов в редакторе
    /// </summary>
    private void OnDrawGizmos()
    {
        if (playerTransform == null) return;
        
        // Рисуем зону спавна
        Gizmos.color = new Color(0, 1, 0, 0.2f);
        Gizmos.DrawSphere(playerTransform.position + playerTransform.forward * spawnDistance, spawnWidth / 2);
        
        // Рисуем направление спавна
        Gizmos.color = Color.blue;
        Gizmos.DrawLine(playerTransform.position, playerTransform.position + playerTransform.forward * spawnDistance);
        
        // Рисуем границы области спавна
        Gizmos.color = Color.yellow;
        Vector3 rightOffset = playerTransform.right * (spawnWidth / 2);
        Gizmos.DrawLine(
            playerTransform.position + playerTransform.forward * spawnDistance - rightOffset,
            playerTransform.position + playerTransform.forward * spawnDistance + rightOffset
        );
    }
} 