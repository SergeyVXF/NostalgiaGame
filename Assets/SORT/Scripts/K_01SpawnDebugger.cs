using UnityEngine;
using System.Text;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Инструмент для отладки системы спавна K_01
/// </summary>
public class K_01SpawnDebugger : MonoBehaviour
{
    [Header("Компоненты системы")]
    [Tooltip("Спавнер, который нужно отладить (K_01Spawner или K_01FixedSpawner)")]
    public MonoBehaviour targetSpawner;
    
    [Header("Настройки отладки")]
    [Tooltip("Отображать GUI с информацией")]
    public bool showDebugGUI = true;
    
    [Tooltip("Визуализировать спавн в сцене")]
    public bool visualizeInScene = true;
    
    [Tooltip("Показывать границы объектов K_01")]
    public bool showObjectBounds = true;
    
    [Tooltip("Показать путь движения игрока")]
    public bool showPlayerPath = true;
    
    [Header("Отладочные объекты")]
    [Tooltip("Тестовый префаб для симуляции спавна")]
    public GameObject testPrefab;
    
    [Tooltip("Проверить работоспособность системы")]
    public bool testSpawn = false;
    
    // Отладочная информация
    private string debugStatus = "Ожидание...";
    private int totalSpawnAttempts = 0;
    private int successfulSpawns = 0;
    private int failedSpawns = 0;
    private List<string> failureReasons = new List<string>();
    private Vector3 lastPlayerPosition;
    private List<Vector3> playerPath = new List<Vector3>();
    private bool playerInAnyZone = false;
    private List<Vector3> spawnPositions = new List<Vector3>();
    private bool isPlayerMovingForward = false;
    
    private void Start()
    {
        // Если спавнер не указан, пытаемся найти его в сцене
        if (targetSpawner == null)
        {
            // Сначала пробуем найти K_01FixedSpawner (улучшенная версия)
            targetSpawner = FindObjectOfType<K_01FixedSpawner>();
            
            // Если не нашли, ищем обычный K_01Spawner
            if (targetSpawner == null)
            {
                targetSpawner = FindObjectOfType<K_01Spawner>();
                
                if (targetSpawner == null)
                {
                    debugStatus = "ОШИБКА: Спавнер (K_01Spawner или K_01FixedSpawner) не найден!";
                    Debug.LogError("K_01SpawnDebugger: Не найден компонент K_01Spawner или K_01FixedSpawner!");
                    return;
                }
            }
        }
        
        // Проверяем, что это действительно спавнер
        if (!(targetSpawner is K_01Spawner || targetSpawner is K_01FixedSpawner))
        {
            debugStatus = $"ОШИБКА: Компонент {targetSpawner.GetType().Name} не является спавнером!";
            Debug.LogError($"K_01SpawnDebugger: Указанный компонент {targetSpawner.GetType().Name} не является ни K_01Spawner, ни K_01FixedSpawner!");
            targetSpawner = null;
            return;
        }
        
        // Находим все зоны спавна
        K_01SpawnZone[] spawnZones = FindObjectsOfType<K_01SpawnZone>();
        if (spawnZones.Length == 0)
        {
            debugStatus = "ПРЕДУПРЕЖДЕНИЕ: Не найдены зоны спавна K_01SpawnZone!";
            Debug.LogWarning("K_01SpawnDebugger: Не найдены зоны спавна K_01SpawnZone!");
        }
        
        // Проверяем настройки игрока
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            debugStatus = "ОШИБКА: Игрок с тегом 'Player' не найден!";
            Debug.LogError("K_01SpawnDebugger: Не найден объект с тегом 'Player'!");
            return;
        }
        
        Rigidbody playerRb = player.GetComponent<Rigidbody>();
        if (playerRb == null)
        {
            debugStatus = "ПРЕДУПРЕЖДЕНИЕ: У игрока нет компонента Rigidbody!";
            Debug.LogWarning("K_01SpawnDebugger: У игрока нет компонента Rigidbody для определения движения!");
        }
        
        // Проверяем настройки префаба
        GameObject prefab = GetPrefabFromSpawner();
        if (prefab == null)
        {
            debugStatus = "ОШИБКА: Префаб K_01 не указан в спавнере!";
            Debug.LogError("K_01SpawnDebugger: Префаб K_01 не указан в спавнере!");
            return;
        }
        
        // Включаем режим отладки спавнера, если он еще не включен
        EnableDebugModeOnSpawner();
        
        // Инициализация переменных
        lastPlayerPosition = player.transform.position;
        
        // Подписываемся на события
        StartCoroutine(MonitorSpawner());
    }
    
    private IEnumerator MonitorSpawner()
    {
        while (true)
        {
            UpdatePlayerStatus();
            CheckSpawnZones();
            TrackSpawnedObjects();
            
            yield return new WaitForSeconds(0.5f);
        }
    }
    
    private void UpdatePlayerStatus()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;
        
        // Проверяем движение игрока
        Vector3 playerPosition = player.transform.position;
        Vector3 moveDirection = playerPosition - lastPlayerPosition;
        
        // Обновляем путь игрока для визуализации
        if (showPlayerPath && Vector3.Distance(playerPosition, lastPlayerPosition) > 0.5f)
        {
            playerPath.Add(playerPosition);
            // Ограничиваем длину пути
            if (playerPath.Count > 100)
                playerPath.RemoveAt(0);
        }
        
        // Проверяем, движется ли игрок вперед
        Rigidbody playerRb = player.GetComponent<Rigidbody>();
        if (playerRb != null)
        {
            isPlayerMovingForward = Vector3.Dot(player.transform.forward, playerRb.linearVelocity) > 0.1f;
        }
        else
        {
            isPlayerMovingForward = Vector3.Dot(player.transform.forward, moveDirection) > 0.01f;
        }
        
        lastPlayerPosition = playerPosition;
    }
    
    private void CheckSpawnZones()
    {
        K_01SpawnZone[] spawnZones = FindObjectsOfType<K_01SpawnZone>();
        
        playerInAnyZone = false;
        foreach (var zone in spawnZones)
        {
            // Проверяем, правильно ли настроена зона
            if (zone.spawner != targetSpawner)
            {
                debugStatus = $"ОШИБКА: Зона {zone.name} ссылается на другой спавнер!";
                continue;
            }
            
            // Проверяем, находится ли игрок в зоне спавна
            Collider zoneCollider = zone.GetComponent<Collider>();
            if (zoneCollider != null)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    Collider playerCollider = player.GetComponent<Collider>();
                    if (playerCollider != null && zoneCollider.bounds.Intersects(playerCollider.bounds))
                    {
                        playerInAnyZone = true;
                        break;
                    }
                }
            }
        }
        
        if (!playerInAnyZone)
        {
            debugStatus = "Игрок вне зоны спавна!";
        }
        else if (!isPlayerMovingForward)
        {
            debugStatus = "Игрок в зоне спавна, но не движется вперед!";
        }
        else
        {
            debugStatus = "Игрок в зоне спавна и движется вперед";
        }
    }
    
    private void TrackSpawnedObjects()
    {
        // Находим все объекты K_01 в сцене
        K_01Controller[] controllers = FindObjectsOfType<K_01Controller>();
        if (controllers.Length > 0)
        {
            spawnPositions.Clear();
            foreach (var controller in controllers)
            {
                spawnPositions.Add(controller.transform.position);
            }
            
            successfulSpawns = controllers.Length;
        }
    }
    
    private void Update()
    {
        // Обработка тестового спавна
        if (testSpawn && testPrefab != null)
        {
            testSpawn = false;
            TestSpawnObject();
        }
    }
    
    private void TestSpawnObject()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;
        
        // Получаем позицию перед игроком
        Vector3 forward = player.transform.forward;
        forward.y = 0;
        forward.Normalize();
        
        float spawnDist = GetSpawnerParameter("spawnDistance");
        Vector3 spawnPos = player.transform.position + forward * spawnDist;
        
        // Спавним тестовый объект
        GameObject testObj = Instantiate(testPrefab, spawnPos, Quaternion.identity);
        testObj.name = "TEST_K_01";
        
        Debug.Log($"K_01SpawnDebugger: Тестовый объект создан на позиции {spawnPos}");
        totalSpawnAttempts++;
        successfulSpawns++;
    }
    
    private void OnGUI()
    {
        if (!showDebugGUI) return;
        
        float width = 400;
        float height = 300;
        float x = Screen.width - width - 10;
        float y = 10;
        
        // Основная область отладки
        GUILayout.BeginArea(new Rect(x, y, width, height), "Отладка системы спавна K_01", GUI.skin.window);
        
        // Статус и основная информация
        GUILayout.Label($"Статус: {debugStatus}");
        GUILayout.Label($"Тип спавнера: {(targetSpawner == null ? "Не определен" : targetSpawner.GetType().Name)}");
        GUILayout.Label($"Игрок в зоне спавна: {playerInAnyZone}");
        GUILayout.Label($"Игрок движется вперед: {isPlayerMovingForward}");
        GUILayout.Label($"Объектов K_01 в сцене: {successfulSpawns}");
        
        // Информация о спавнере
        if (targetSpawner != null)
        {
            GUILayout.Label("--- Настройки спавнера ---");
            GameObject prefab = GetPrefabFromSpawner();
            GUILayout.Label($"Префаб K_01: {(prefab != null ? prefab.name : "Не указан!")}");
            GUILayout.Label($"Макс. количество объектов: {GetSpawnerParameter("maxObjects")}");
            GUILayout.Label($"Мин. расстояние между объектами: {GetSpawnerParameter("minDistanceBetweenObjects")}");
            GUILayout.Label($"Задержка между спавном: {GetSpawnerParameter("spawnDelay")} с");
        }
        
        // Кнопки управления отладкой
        GUILayout.Space(10);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Тестовый спавн"))
        {
            testSpawn = true;
        }
        if (GUILayout.Button("Очистить K_01"))
        {
            ClearAllK01Objects();
        }
        GUILayout.EndHorizontal();
        
        GUILayout.EndArea();
    }
    
    private void ClearAllK01Objects()
    {
        K_01Controller[] controllers = FindObjectsOfType<K_01Controller>();
        foreach (var controller in controllers)
        {
            Destroy(controller.gameObject);
        }
        Debug.Log($"K_01SpawnDebugger: Удалено {controllers.Length} объектов K_01");
        spawnPositions.Clear();
        successfulSpawns = 0;
    }
    
    private void OnDrawGizmos()
    {
        if (!visualizeInScene) return;
        
        // Визуализируем путь игрока
        if (showPlayerPath && playerPath.Count > 1)
        {
            Gizmos.color = Color.green;
            for (int i = 1; i < playerPath.Count; i++)
            {
                Gizmos.DrawLine(playerPath[i-1], playerPath[i]);
            }
        }
        
        // Визуализируем позиции спавна объектов
        if (showObjectBounds && spawnPositions.Count > 0)
        {
            Gizmos.color = Color.yellow;
            foreach (var pos in spawnPositions)
            {
                Gizmos.DrawWireSphere(pos, GetSpawnerParameter("minDistanceBetweenObjects"));
            }
        }
        
        // Визуализируем зоны спавна
        K_01SpawnZone[] spawnZones = FindObjectsOfType<K_01SpawnZone>();
        foreach (var zone in spawnZones)
        {
            if (zone.spawner != targetSpawner)
            {
                // Подсвечиваем неверно настроенные зоны
                Gizmos.color = Color.red;
            }
            else
            {
                Gizmos.color = new Color(0, 0.8f, 1, 0.2f);
            }
            
            Collider zoneCollider = zone.GetComponent<Collider>();
            if (zoneCollider != null)
            {
                Gizmos.DrawWireCube(zoneCollider.bounds.center, zoneCollider.bounds.size);
            }
        }
    }
    
    // Получаем префаб из любого типа спавнера
    private GameObject GetPrefabFromSpawner()
    {
        if (targetSpawner is K_01Spawner)
        {
            return ((K_01Spawner)targetSpawner).k01Prefab;
        }
        else if (targetSpawner is K_01FixedSpawner)
        {
            return ((K_01FixedSpawner)targetSpawner).k01Prefab;
        }
        return null;
    }
    
    // Включаем режим отладки на любом типе спавнера
    private void EnableDebugModeOnSpawner()
    {
        if (targetSpawner is K_01Spawner)
        {
            K_01Spawner spawner = (K_01Spawner)targetSpawner;
            if (!spawner.debugMode)
            {
                spawner.debugMode = true;
                Debug.Log("K_01SpawnDebugger: Включен режим отладки спавнера K_01Spawner");
            }
        }
        else if (targetSpawner is K_01FixedSpawner)
        {
            K_01FixedSpawner spawner = (K_01FixedSpawner)targetSpawner;
            if (!spawner.debugMode)
            {
                spawner.debugMode = true;
                Debug.Log("K_01SpawnDebugger: Включен режим отладки спавнера K_01FixedSpawner");
            }
        }
    }
    
    // Получаем значение параметра из любого типа спавнера
    private float GetSpawnerParameter(string paramName)
    {
        if (targetSpawner is K_01Spawner)
        {
            K_01Spawner spawner = (K_01Spawner)targetSpawner;
            switch (paramName)
            {
                case "maxObjects": return spawner.maxObjects;
                case "minDistanceBetweenObjects": return spawner.minDistanceBetweenObjects;
                case "spawnDistance": return spawner.spawnDistance;
                case "spawnDelay": return spawner.spawnDelay;
                default: return 0f;
            }
        }
        else if (targetSpawner is K_01FixedSpawner)
        {
            K_01FixedSpawner spawner = (K_01FixedSpawner)targetSpawner;
            switch (paramName)
            {
                case "maxObjects": return spawner.maxObjects;
                case "minDistanceBetweenObjects": return spawner.minDistanceBetweenObjects;
                case "spawnDistance": return spawner.spawnDistance;
                case "spawnDelay": return spawner.spawnDelay;
                default: return 0f;
            }
        }
        return 0f;
    }
} 