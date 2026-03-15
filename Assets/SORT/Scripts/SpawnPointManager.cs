using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SpawnPointManager : MonoBehaviour
{
    [Tooltip("Список всех точек спавна в сцене")]
    public List<SpawnPoint> spawnPoints = new List<SpawnPoint>();
    
    [Tooltip("Спавн-поинт по умолчанию, если не указан другой")]
    public SpawnPoint defaultSpawnPoint;
    
    [Tooltip("Автоматически искать все спавн-поинты при старте")]
    public bool autoFindSpawnPoints = true;
    
    [Tooltip("Тег игрока для поиска")]
    public string playerTag = "Player";
    
    [Tooltip("Автоматически телепортировать игрока при загрузке сцены")]
    public bool autoTeleportOnLoad = true;
    
    [Header("Дебаг")]
    [Tooltip("Выводить отладочные сообщения")]
    public bool showDebugMessages = true;
    
    // Синглтон для доступа из других скриптов
    private static SpawnPointManager _instance;
    public static SpawnPointManager Instance 
    { 
        get 
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<SpawnPointManager>();
                
                if (_instance == null)
                {
                    GameObject manager = new GameObject("SpawnPointManager");
                    _instance = manager.AddComponent<SpawnPointManager>();
                }
            }
            return _instance;
        }
    }
    
    private void Awake()
    {
        // Реализация синглтона
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        _instance = this;
        
        // Не уничтожаем объект при переходе между сценами
        DontDestroyOnLoad(gameObject);
        
        // Проверяем, есть ли уже SceneTransitionFixer
        if (GetComponent<SceneTransitionFixer>() == null)
        {
            // Если нет, добавляем его
            SceneTransitionFixer fixer = gameObject.AddComponent<SceneTransitionFixer>();
            fixer.playerTag = playerTag;
            
            if (showDebugMessages)
            {
                Debug.Log("[SpawnPointManager] Добавлен компонент SceneTransitionFixer для исправления ссылок при переходе между сценами");
            }
        }
    }
    
    private void Start()
    {
        // Подписываемся на событие загрузки сцены
        SceneManager.sceneLoaded += OnSceneLoaded;
        
        // Собираем точки спавна при старте, если включено автозаполнение
        if (autoFindSpawnPoints)
        {
            RefreshSpawnPoints();
        }
    }
    
    private void OnDestroy()
    {
        // Отписываемся от события при уничтожении
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
    
    // Обработчик события загрузки сцены
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        StartCoroutine(OnSceneLoadedDelayed(scene));
    }
    
    // Корутина с задержкой для обработки загрузки сцены
    private IEnumerator OnSceneLoadedDelayed(Scene scene)
    {
        // Ждем 2 кадра, чтобы сцена и все объекты успели инициализироваться
        yield return null;
        yield return null;
        
        // Обновляем список точек спавна
        RefreshSpawnPoints();
        
        // Проверяем, нужно ли телепортировать игрока
        if (autoTeleportOnLoad && TriggerBack.HasSavedSpawnData())
        {
            TeleportPlayerToSavedSpawnPoint();
        }
    }
    
    // Метод для обновления списка точек спавна
    public void RefreshSpawnPoints()
    {
        // Очищаем текущий список
        spawnPoints.Clear();
        
        // Находим все точки спавна в сцене
        SpawnPoint[] foundPoints = FindObjectsOfType<SpawnPoint>();
        spawnPoints.AddRange(foundPoints);
        
        if (showDebugMessages)
        {
            Debug.Log($"[SpawnPointManager] Найдено {spawnPoints.Count} точек спавна");
            
            // Выводим имена точек
            foreach (SpawnPoint point in spawnPoints)
            {
                Debug.Log($"[SpawnPointManager] Спавн-поинт: {point.gameObject.name}, ID: {point.spawnPointID}");
            }
        }
        
        // Если нет точки по умолчанию, пробуем найти ее
        if (defaultSpawnPoint == null && spawnPoints.Count > 0)
        {
            defaultSpawnPoint = spawnPoints[0];
            
            if (showDebugMessages)
            {
                Debug.Log($"[SpawnPointManager] Установлен спавн-поинт по умолчанию: {defaultSpawnPoint.gameObject.name}");
            }
        }
    }
    
    // Метод для телепортации игрока на сохраненную точку спавна
    public void TeleportPlayerToSavedSpawnPoint()
    {
        if (!TriggerBack.HasSavedSpawnData())
        {
            if (showDebugMessages)
            {
                Debug.LogWarning("[SpawnPointManager] Нет сохраненных данных о точке спавна");
            }
            return;
        }
        
        string spawnPointName = TriggerBack.GetSavedSpawnPointName();
        
        // Ищем игрока
        GameObject player = GameObject.FindGameObjectWithTag(playerTag);
        
        if (player == null)
        {
            Debug.LogError($"[SpawnPointManager] Игрок с тегом '{playerTag}' не найден!");
            return;
        }
        
        // Ищем точку спавна по имени
        SpawnPoint targetPoint = GetSpawnPointByID(spawnPointName);
        
        if (targetPoint != null)
        {
            // Телепортируем на указанную точку спавна
            targetPoint.TeleportObjectToSpawnPoint(player);
            
            if (showDebugMessages)
            {
                Debug.Log($"[SpawnPointManager] Игрок телепортирован на точку спавна '{spawnPointName}'");
            }
        }
        else
        {
            // Если точка не найдена, используем запасные координаты
            Vector3 fallbackPosition = TriggerBack.GetSavedSpawnPosition();
            Vector3 fallbackRotation = TriggerBack.GetSavedSpawnRotation();
            
            player.transform.position = fallbackPosition;
            player.transform.eulerAngles = fallbackRotation;
            
            if (showDebugMessages)
            {
                Debug.LogWarning($"[SpawnPointManager] Точка спавна '{spawnPointName}' не найдена. Использована запасная позиция.");
            }
        }
        
        // Сбрасываем сохраненные данные
        TriggerBack.ClearSavedSpawnData();
    }
    
    // Метод для получения точки спавна по ID
    public SpawnPoint GetSpawnPointByID(string spawnPointID)
    {
        foreach (SpawnPoint point in spawnPoints)
        {
            if (point.spawnPointID == spawnPointID)
            {
                return point;
            }
        }
        
        return null;
    }
    
    // Метод для ручной телепортации игрока на указанную точку спавна
    public void TeleportPlayerToSpawnPoint(string spawnPointID)
    {
        // Ищем игрока
        GameObject player = GameObject.FindGameObjectWithTag(playerTag);
        
        if (player == null)
        {
            Debug.LogError($"[SpawnPointManager] Игрок с тегом '{playerTag}' не найден!");
            return;
        }
        
        // Ищем точку спавна по имени
        SpawnPoint targetPoint = GetSpawnPointByID(spawnPointID);
        
        if (targetPoint != null)
        {
            // Телепортируем на указанную точку спавна
            targetPoint.TeleportObjectToSpawnPoint(player);
            
            if (showDebugMessages)
            {
                Debug.Log($"[SpawnPointManager] Игрок телепортирован на точку спавна '{spawnPointID}'");
            }
        }
        else
        {
            Debug.LogError($"[SpawnPointManager] Точка спавна '{spawnPointID}' не найдена!");
        }
    }
    
    // Метод для ручной телепортации игрока на точку спавна по умолчанию
    public void TeleportPlayerToDefaultSpawnPoint()
    {
        if (defaultSpawnPoint == null)
        {
            Debug.LogError("[SpawnPointManager] Точка спавна по умолчанию не задана!");
            return;
        }
        
        // Ищем игрока
        GameObject player = GameObject.FindGameObjectWithTag(playerTag);
        
        if (player == null)
        {
            Debug.LogError($"[SpawnPointManager] Игрок с тегом '{playerTag}' не найден!");
            return;
        }
        
        // Телепортируем на точку спавна по умолчанию
        defaultSpawnPoint.TeleportObjectToSpawnPoint(player);
        
        if (showDebugMessages)
        {
            Debug.Log($"[SpawnPointManager] Игрок телепортирован на точку спавна по умолчанию");
        }
    }
    
    // Метод для создания новой точки спавна в указанной позиции
    public SpawnPoint CreateSpawnPointAtPosition(Vector3 position, Vector3 rotation, string spawnPointID = "")
    {
        // Создаем новый объект
        GameObject newSpawnPoint = new GameObject("SpawnPoint_" + (string.IsNullOrEmpty(spawnPointID) ? System.Guid.NewGuid().ToString().Substring(0, 8) : spawnPointID));
        
        // Устанавливаем позицию и поворот
        newSpawnPoint.transform.position = position;
        newSpawnPoint.transform.eulerAngles = rotation;
        
        // Добавляем компонент точки спавна
        SpawnPoint point = newSpawnPoint.AddComponent<SpawnPoint>();
        
        // Устанавливаем ID точки
        if (!string.IsNullOrEmpty(spawnPointID))
        {
            point.spawnPointID = spawnPointID;
        }
        
        // Добавляем в список
        spawnPoints.Add(point);
        
        return point;
    }
} 