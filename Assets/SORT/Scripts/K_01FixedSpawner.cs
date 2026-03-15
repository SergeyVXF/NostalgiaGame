using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Исправленная версия спавнера K_01, решающая проблемы с видимостью и генерацией объектов
/// </summary>
public class K_01FixedSpawner : MonoBehaviour
{
    [Header("Основные настройки")]
    [Tooltip("Префаб объекта K_01 для спавна")]
    public GameObject k01Prefab;
    
    [Tooltip("Максимальное количество объектов одновременно в сцене")]
    [Range(1, 500)]
    public int maxObjects = 100;
    
    [Tooltip("Максимальное количество объектов, созданных за игровую сессию (0 = без ограничений)")]
    [Range(0, 1000)]
    public int maxTotalObjectsPerSession = 150;
    
    [Tooltip("Минимальное расстояние между объектами")]
    public float minDistanceBetweenObjects = 0.35f;
    
    [Header("Настройки спавна")]
    [Tooltip("Дистанция спавна перед игроком")]
    public float spawnDistance = 2f;
    
    [Tooltip("Высота спавна над землей")]
    public float heightAboveGround = 0.5f;
    
    [Tooltip("Ширина области спавна перед игроком")]
    public float spawnWidth = 1f;
    
    [Tooltip("Задержка между спавном объектов (в секундах)")]
    public float spawnDelay = 0.5f;
    
    [Header("Настройки анимации")]
    [Tooltip("Включить анимацию увеличения по Y")]
    public bool enableScaleAnimation = true;
    
    [Tooltip("Время анимации увеличения (в секундах)")]
    [Range(0.1f, 5f)]
    public float scaleAnimationDuration = 1f;
    
    [Tooltip("Кривая анимации увеличения")]
    public AnimationCurve scaleAnimationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    [Header("Настройки объектов")]
    [Tooltip("Размер объектов K_01 (масштаб)")]
    public float objectScale = 1.0f;
    
    [Tooltip("Случайное вращение при создании")]
    public bool randomRotation = false;
    
    [Tooltip("Время жизни объектов (0 = бесконечно)")]
    public float objectLifetime = 0f;
    
    [Header("Отладка")]
    [Tooltip("Включить режим отладки")]
    public bool debugMode = true;
    
    [Tooltip("Использовать отдельный физический слой для объектов")]
    public bool useCustomLayer = true;
    
    [Tooltip("Включить подсветку объектов для лучшей видимости")]
    public bool highlightObjects = true;
    
    [Tooltip("Материал для подсветки объектов")]
    public Material highlightMaterial;
    
    // Приватные переменные
    private List<GameObject> spawnedObjects = new List<GameObject>();
    private List<Vector3> spawnPositions = new List<Vector3>();
    private float timeSinceLastSpawn = 0f;
    private Transform playerTransform;
    private bool playerInSpawnZone = false;
    private LayerMask groundLayerMask;
    
    // Счетчик всех созданных объектов за сессию
    private int totalSpawnedCount = 0;
    
    // Словарь для хранения информации об анимации для каждого объекта
    private Dictionary<GameObject, float> objectAnimationTimes = new Dictionary<GameObject, float>();
    
    void Start()
    {
        // Находим игрока в сцене
        playerTransform = GameObject.FindGameObjectWithTag("Player")?.transform;
        
        if (playerTransform == null)
        {
            Debug.LogError("K_01FixedSpawner: Игрок с тегом 'Player' не найден");
            enabled = false;
            return;
        }
        
        if (k01Prefab == null)
        {
            Debug.LogError("K_01FixedSpawner: Не указан префаб K_01");
            enabled = false;
            return;
        }
        
        // Устанавливаем маску слоя земли (все слои, кроме игнорируемых)
        groundLayerMask = ~(1 << LayerMask.NameToLayer("Ignore Raycast") | 1 << LayerMask.NameToLayer("UI"));
        
        // Создаем материал подсветки, если он не указан
        if (highlightObjects && highlightMaterial == null)
        {
            highlightMaterial = new Material(Shader.Find("Standard"));
            highlightMaterial.color = new Color(1f, 0.5f, 0f, 1f);
            highlightMaterial.EnableKeyword("_EMISSION");
            highlightMaterial.SetColor("_EmissionColor", new Color(1f, 0.5f, 0f, 1f));
        }
        
        // Проверяем наличие зон спавна
        K_01SpawnZone[] spawnZones = FindObjectsOfType<K_01SpawnZone>();
        if (spawnZones.Length == 0)
        {
            Debug.LogWarning("K_01FixedSpawner: Не найдены зоны спавна K_01SpawnZone. Создаем зону вокруг спавнера.");
            CreateDefaultSpawnZone();
        }
        
        if (debugMode)
        {
            Debug.Log("K_01FixedSpawner: Система спавна инициализирована");
        }
    }
    
    void Update()
    {
        // Обновляем анимации для всех объектов
        if (enableScaleAnimation)
        {
            UpdateScaleAnimations();
        }
        
        // Проверяем, находится ли игрок в зоне спавна
        if (!playerInSpawnZone || playerTransform == null) return;
        
        // Проверяем, движется ли игрок вперед (используем более низкий порог срабатывания)
        Vector3 playerVelocity = playerTransform.GetComponent<Rigidbody>()?.linearVelocity ?? Vector3.zero;
        
        // Используем более низкий порог для движения вперед (было 0.1f)
        bool isMovingForward = Vector3.Dot(playerTransform.forward, playerVelocity) > 0.05f;
        
        // Также проверяем общее движение игрока
        bool isMovingAtAll = playerVelocity.magnitude > 0.1f;
        
        if ((!isMovingForward || !isMovingAtAll) && debugMode)
        {
            Debug.Log($"K_01FixedSpawner: Игрок не движется вперед. Скорость: {playerVelocity.magnitude}, Направление: {Vector3.Dot(playerTransform.forward, playerVelocity)}");
            
            // Продолжаем выполнение - убираем return здесь, чтобы спавн происходил даже при слабом движении
            // Это временное решение для отладки
        }
        
        // Обновляем таймер спавна
        timeSinceLastSpawn += Time.deltaTime;
        
        // Проверяем, прошло ли достаточно времени с последнего спавна
        if (timeSinceLastSpawn >= spawnDelay)
        {
            // Проверяем оба ограничения: текущее количество объектов и общее количество за сессию
            bool canSpawnMore = spawnedObjects.Count < maxObjects;
            bool hasReachedTotalLimit = maxTotalObjectsPerSession > 0 && totalSpawnedCount >= maxTotalObjectsPerSession;
            
            if (canSpawnMore && !hasReachedTotalLimit)
            {
                TrySpawnObject();
                timeSinceLastSpawn = 0f;
            }
            else if (debugMode)
            {
                if (!canSpawnMore)
                {
                    Debug.Log($"K_01FixedSpawner: Достигнут лимит одновременных объектов ({maxObjects})");
                }
                
                if (hasReachedTotalLimit)
                {
                    Debug.Log($"K_01FixedSpawner: Достигнут общий лимит объектов за сессию ({maxTotalObjectsPerSession})");
                }
            }
        }
        
        // Очищаем список от уничтоженных объектов
        CleanupDestroyedObjects();
    }
    
    private void UpdateScaleAnimations()
    {
        // Создаем временный список для хранения объектов, которые нужно удалить
        List<GameObject> objectsToRemove = new List<GameObject>();

        // Создаем копию ключей словаря для безопасного перебора
        var objectKeys = new List<GameObject>(objectAnimationTimes.Keys);

        foreach (var obj in objectKeys)
        {
            if (obj == null)
            {
                objectsToRemove.Add(obj);
                continue;
            }

            float animationTime = objectAnimationTimes[obj];
            animationTime += Time.deltaTime;
            float normalizedTime = Mathf.Clamp01(animationTime / scaleAnimationDuration);
            float scaleValue = scaleAnimationCurve.Evaluate(normalizedTime);

            // Применяем масштаб только по оси Y
            Vector3 currentScale = obj.transform.localScale;
            currentScale.y = scaleValue * objectScale;
            obj.transform.localScale = currentScale;

            if (normalizedTime >= 1f)
            {
                objectsToRemove.Add(obj);
            }
            else
            {
                objectAnimationTimes[obj] = animationTime;
            }
        }

        // Удаляем завершенные анимации
        foreach (var obj in objectsToRemove)
        {
            objectAnimationTimes.Remove(obj);
        }
    }
    
    /// <summary>
    /// Создает зону спавна по умолчанию вокруг спавнера
    /// </summary>
    private void CreateDefaultSpawnZone()
    {
        GameObject defaultZone = new GameObject("DefaultSpawnZone");
        defaultZone.transform.position = transform.position;
        
        // Добавляем сферический коллайдер радиусом 10 метров
        SphereCollider collider = defaultZone.AddComponent<SphereCollider>();
        collider.radius = 10f;
        collider.isTrigger = true;
        
        // Добавляем компонент зоны спавна
        K_01SpawnZone zone = defaultZone.AddComponent<K_01SpawnZone>();
        zone.spawnerObject = this.gameObject;
        
        if (debugMode)
        {
            Debug.Log("K_01FixedSpawner: Создана зона спавна по умолчанию");
        }
    }
    
    /// <summary>
    /// Пытается создать новый объект K_01 перед игроком
    /// </summary>
    private void TrySpawnObject()
    {
        // Проверяем ограничение на общее количество объектов за сессию
        if (maxTotalObjectsPerSession > 0 && totalSpawnedCount >= maxTotalObjectsPerSession)
        {
            if (debugMode)
            {
                Debug.Log($"K_01FixedSpawner: Достигнут лимит объектов за сессию ({maxTotalObjectsPerSession})");
            }
            return;
        }
        
        if (k01Prefab == null)
        {
            Debug.LogError("K_01FixedSpawner: Отсутствует префаб K_01!");
            return;
        }
        
        // Получаем позицию перед игроком
        Vector3 spawnPos = GetSpawnPositionInFrontOfPlayer();
        
        // Проверяем, можно ли спавнить объект на этой позиции
        if (CanSpawnAtPosition(spawnPos))
        {
            try
            {
                // Создаем объект с явным указанием родительского объекта = null
                GameObject newObject = Instantiate(k01Prefab, spawnPos, Quaternion.identity, null);
                
                if (newObject == null)
                {
                    Debug.LogError("K_01FixedSpawner: Не удалось создать объект K_01!");
                    return;
                }
                
                // Увеличиваем счетчик созданных объектов
                totalSpawnedCount++;
                
                // Убедимся, что объект активен
                newObject.SetActive(true);
                
                // Настраиваем начальный масштаб объекта
                Vector3 initialScale = Vector3.one * objectScale;
                if (enableScaleAnimation)
                {
                    initialScale.y = 0f; // Начинаем с нулевой высоты
                }
                newObject.transform.localScale = initialScale;
                
                // Добавляем объект в список для анимации
                if (enableScaleAnimation)
                {
                    objectAnimationTimes[newObject] = 0f;
                }
                
                // Настраиваем масштаб объекта
                newObject.transform.localScale = Vector3.one * objectScale;
                
                // Устанавливаем фиксированную ротацию (0, 0, 0)
                newObject.transform.rotation = Quaternion.identity;
                
                // Устанавливаем физический слой, если необходимо
                if (useCustomLayer)
                {
                    // Пытаемся найти слой "K_01" или создать его программно (в редакторе)
                    int layer = LayerMask.NameToLayer("K_01");
                    if (layer != -1)
                    {
                        newObject.layer = layer;
                        
                        // Применяем этот же слой ко всем дочерним объектам
                        foreach (Transform child in newObject.GetComponentsInChildren<Transform>(true))
                        {
                            child.gameObject.layer = layer;
                        }
                    }
                }
                
                // Добавляем подсветку для лучшей видимости
                if (highlightObjects && highlightMaterial != null)
                {
                    Renderer[] renderers = newObject.GetComponentsInChildren<Renderer>();
                    
                    if (renderers.Length == 0)
                    {
                        Debug.LogWarning("K_01FixedSpawner: У объекта K_01 нет компонентов Renderer!");
                    }
                    
                    foreach (Renderer renderer in renderers)
                    {
                        // Сохраняем оригинальные материалы
                        Material[] originalMaterials = renderer.materials;
                        
                        // Создаем новый набор материалов с добавлением подсветки
                        Material[] newMaterials = new Material[originalMaterials.Length + 1];
                        for (int i = 0; i < originalMaterials.Length; i++)
                        {
                            newMaterials[i] = originalMaterials[i];
                        }
                        
                        // Добавляем материал подсветки
                        newMaterials[originalMaterials.Length] = highlightMaterial;
                        
                        // Применяем новые материалы
                        renderer.materials = newMaterials;
                    }
                }
                
                // Настраиваем время жизни объекта
                if (objectLifetime > 0)
                {
                    Destroy(newObject, objectLifetime);
                }
                
                // Добавляем компонент K_01Controller, если его нет
                K_01Controller controller = newObject.GetComponent<K_01Controller>();
                if (controller == null)
                {
                    controller = newObject.AddComponent<K_01Controller>();
                    controller.lifeTime = objectLifetime;
                }
                
                // Удаляем добавление Rigidbody, так как он не нужен
                // Если у префаба есть Rigidbody, удаляем его
                Rigidbody rb = newObject.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    Destroy(rb);
                }
                
                // Проверим, есть ли у объекта коллайдер
                Collider coll = newObject.GetComponent<Collider>();
                if (coll == null)
                {
                    // Добавляем сферический коллайдер по умолчанию
                    SphereCollider sphereCollider = newObject.AddComponent<SphereCollider>();
                    sphereCollider.radius = 0.5f;
                    sphereCollider.isTrigger = false;
                }
                
                // Добавляем компонент улучшенной видимости, если его нет
                K_01EnhancedVisibility visibility = newObject.GetComponent<K_01EnhancedVisibility>();
                if (visibility == null && newObject.GetComponentInChildren<Renderer>() != null)
                {
                    try
                    {
                        visibility = newObject.AddComponent<K_01EnhancedVisibility>();
                        // По умолчанию используем красноватый цвет для лучшей видимости
                        if (visibility != null)
                        {
                            Debug.Log("K_01FixedSpawner: Добавлен компонент улучшенной видимости");
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"K_01FixedSpawner: Не удалось добавить компонент улучшенной видимости: {ex.Message}");
                    }
                }
                
                // Добавляем объект и его позицию в списки
                spawnedObjects.Add(newObject);
                spawnPositions.Add(spawnPos);
                
                if (debugMode)
                {
                    Debug.Log($"K_01FixedSpawner: Создан объект K_01 на позиции {spawnPos}, всего: {spawnedObjects.Count}, за сессию: {totalSpawnedCount}");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"K_01FixedSpawner: Ошибка при создании объекта: {ex.Message}");
            }
        }
        else if (debugMode)
        {
            Debug.Log($"K_01FixedSpawner: Не удалось создать объект, найдено препятствие или слишком близко к другим объектам");
        }
    }
    
    /// <summary>
    /// Получает позицию для спавна перед игроком
    /// </summary>
    private Vector3 GetSpawnPositionInFrontOfPlayer()
    {
        // Получаем направление перед игроком (игнорируя Y-компоненту)
        Vector3 forward = playerTransform.forward;
        forward.y = 0;
        forward.Normalize();
        
        // Добавляем случайное смещение по ширине
        Vector3 right = playerTransform.right;
        float randomOffset = Random.Range(-spawnWidth / 2, spawnWidth / 2);
        
        // Вычисляем направление спавна
        Vector3 spawnDirection = forward + right * (randomOffset / spawnDistance);
        spawnDirection.Normalize();
        
        // Вычисляем базовую позицию
        Vector3 basePosition = playerTransform.position + spawnDirection * spawnDistance;
        
        // Поднимаем позицию на уровень глаз игрока для лучшей видимости
        float eyeHeight = playerTransform.position.y + 1.6f; // Примерная высота глаз
        basePosition.y = eyeHeight;
        
        // Опускаем луч вниз для нахождения земли или другой поверхности
        RaycastHit hit;
        if (Physics.Raycast(basePosition, Vector3.down, out hit, 5f, groundLayerMask))
        {
            // Устанавливаем позицию чуть выше поверхности
            return hit.point + Vector3.up * heightAboveGround;
        }
        
        // Если поверхность не найдена, используем позицию на той же высоте, что и игрок
        return new Vector3(basePosition.x, playerTransform.position.y + heightAboveGround, basePosition.z);
    }
    
    /// <summary>
    /// Проверяет, можно ли создать объект на указанной позиции
    /// </summary>
    private bool CanSpawnAtPosition(Vector3 position)
    {
        // Проверяем расстояние до других объектов
        foreach (Vector3 existingPos in spawnPositions)
        {
            float distance = Vector3.Distance(position, existingPos);
            if (distance < minDistanceBetweenObjects)
            {
                return false;
            }
        }
        
        // Проверяем наличие препятствий
        Collider[] colliders = Physics.OverlapSphere(position, 0.2f);
        foreach (Collider collider in colliders)
        {
            // Игнорируем триггеры и объекты в слое Ignore Raycast
            if (!collider.isTrigger && collider.gameObject.layer != LayerMask.NameToLayer("Ignore Raycast"))
            {
                return false;
            }
        }
        
        return true;
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
    /// Обработчик входа в тригерную зону
    /// </summary>
    public void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInSpawnZone = true;
            
            if (debugMode)
            {
                Debug.Log("K_01FixedSpawner: Игрок вошел в зону спавна");
            }
        }
    }
    
    /// <summary>
    /// Обработчик выхода из тригерной зоны
    /// </summary>
    public void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInSpawnZone = false;
            
            if (debugMode)
            {
                Debug.Log("K_01FixedSpawner: Игрок вышел из зоны спавна");
            }
        }
    }
    
    /// <summary>
    /// Визуализация в редакторе Unity
    /// </summary>
    private void OnDrawGizmos()
    {
        if (playerTransform == null) return;
        
        // Визуализируем зону спавна перед игроком
        Gizmos.color = new Color(0, 1, 0, 0.3f);
        Gizmos.DrawSphere(playerTransform.position + playerTransform.forward * spawnDistance, spawnWidth / 2);
        
        // Визуализируем позиции созданных объектов
        Gizmos.color = Color.yellow;
        foreach (Vector3 pos in spawnPositions)
        {
            Gizmos.DrawWireSphere(pos, minDistanceBetweenObjects / 2);
        }
    }
    
    /// <summary>
    /// Принудительно создает объект перед игроком (для отладки)
    /// </summary>
    public void ForceSpawn()
    {
        if (playerTransform == null)
        {
            playerTransform = GameObject.FindGameObjectWithTag("Player")?.transform;
            if (playerTransform == null)
            {
                Debug.LogError("K_01FixedSpawner: Не найден игрок для принудительного спавна!");
                return;
            }
        }
        
        Debug.Log("K_01FixedSpawner: Принудительный спавн объекта...");
        TrySpawnObject();
    }
    
    /// <summary>
    /// Переключает режим случайного вращения
    /// </summary>
    public void ToggleRandomRotation()
    {
        randomRotation = !randomRotation;
        Debug.Log($"K_01FixedSpawner: Случайное вращение {(randomRotation ? "включено" : "выключено")}");
    }
    
    /// <summary>
    /// Возвращает количество созданных объектов за текущую сессию
    /// </summary>
    public int GetTotalSpawnedCount()
    {
        return totalSpawnedCount;
    }
    
    /// <summary>
    /// Сбрасывает счетчик созданных объектов за сессию
    /// </summary>
    public void ResetTotalSpawnedCount()
    {
        totalSpawnedCount = 0;
        Debug.Log("K_01FixedSpawner: Счетчик созданных объектов за сессию сброшен");
    }
} 