using UnityEngine;

public class BoneBehavior : MonoBehaviour
{
    [Header("Настройки косточки")]
    [Tooltip("Тег косточки для идентификации")]
    public string boneTag = "Bone";
    
    [Tooltip("Время поедания косточки (секунды)")]
    public float eatingTime = 5f;
    
    [Tooltip("Расстояние, на котором собака начинает есть косточку")]
    public float eatingDistance = 1f;
    
    [Header("Визуальные эффекты")]
    [Tooltip("Эффект частиц при поедании (опционально)")]
    public ParticleSystem eatingEffect;
    
    [Tooltip("Звук поедания (опционально)")]
    public AudioClip eatingSound;
    
    [Header("Подбор игроком")]
    [Tooltip("Может ли игрок подобрать эту косточку")]
    public bool canBePickedUp = true;
    [Tooltip("Тег игрока для подбора")]
    public string playerTag = "Player";
    
    [Header("Визуальные эффекты")]
    [Tooltip("Скорость вращения косточки (градусы в секунду)")]
    public float rotationSpeed = 90f;
    [Tooltip("Ось вращения косточки")]
    public Vector3 rotationAxis = Vector3.up;
    
    [Header("Отладка")]
    [Tooltip("Показывать отладочную информацию")]
    public bool showDebugLog = true;
    
    private bool isBeingEaten = false;
    private AudioSource audioSource;
    
    private void Awake()
    {
        // Устанавливаем тег, если он не установлен
        if (!gameObject.CompareTag(boneTag))
        {
            gameObject.tag = boneTag;
            if (showDebugLog)
                Debug.Log($"[BoneBehavior] Установлен тег '{boneTag}' для {gameObject.name}");
        }
        
        // Получаем или добавляем AudioSource
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null && eatingSound != null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.clip = eatingSound;
            audioSource.playOnAwake = false;
        }
        
        // Убеждаемся, что у косточки есть триггер для подбора
        SetupPickupTrigger();
    }
    
    private void Start()
    {
        if (showDebugLog)
            Debug.Log($"[BoneBehavior] Косточка {gameObject.name} готова к использованию");
            
        // Проверяем, является ли это кидаемой косточкой
        ThrowableBone throwableBone = GetComponent<ThrowableBone>();
        if (throwableBone == null)
        {
            // Это обычная косточка - заставляем собаку бежать немедленно
            if (showDebugLog)
                Debug.Log($"[BoneBehavior] Обычная косточка - заставляю собаку бежать к {gameObject.name}");
            ForceNearestDogToChase();
        }
        else
        {
            // Это кидаемая косточка - ждем пока игрок её бросит
            if (showDebugLog)
                Debug.Log($"[BoneBehavior] Кидаемая косточка {gameObject.name} - ожидаю броска игрока");
        }
    }
    
    private void Update()
    {
        // Постоянное вращение косточки (если не поедается)
        if (!isBeingEaten && rotationSpeed > 0)
        {
            transform.Rotate(rotationAxis, rotationSpeed * Time.deltaTime, Space.World);
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (showDebugLog)
            Debug.Log($"[BoneBehavior] OnTriggerEnter: {other.name}, tag: '{other.tag}', ожидаемый тег игрока: '{playerTag}'");
        
        // Проверяем, что это игрок и косточку можно подобрать
        if (canBePickedUp && !isBeingEaten && other.CompareTag(playerTag))
        {
            if (showDebugLog)
                Debug.Log($"[BoneBehavior] 🎯 Игрок {other.name} коснулся косточки {gameObject.name} - ПОДБИРАЮ!");
            
            PickupBoneByPlayer(other.gameObject);
        }
        else
        {
            if (showDebugLog)
            {
                string reason = "";
                if (!canBePickedUp) reason += "нельзя подбирать, ";
                if (isBeingEaten) reason += "собака ест, ";
                if (!other.CompareTag(playerTag)) reason += $"не игрок (тег: '{other.tag}'), ";
                
                Debug.Log($"[BoneBehavior] ❌ НЕ подбираю {gameObject.name}: {reason.TrimEnd(',', ' ')}");
            }
        }
    }
    
    /// <summary>
    /// Настраивает триггер для подбора косточки игроком
    /// </summary>
    private void SetupPickupTrigger()
    {
        // Проверяем, есть ли уже основной коллайдер для физики
        Collider[] allColliders = GetComponents<Collider>();
        bool hasPhysicsCollider = false;
        
        foreach (var col in allColliders)
        {
            if (!col.isTrigger)
            {
                hasPhysicsCollider = true;
                if (showDebugLog)
                    Debug.Log($"[BoneBehavior] Найден основной коллайдер: {col.GetType().Name}");
                break;
            }
        }
        
        // Если нет основного коллайдера - добавляем увеличенный
        if (!hasPhysicsCollider)
        {
            CapsuleCollider physicsCollider = gameObject.AddComponent<CapsuleCollider>();
            physicsCollider.isTrigger = false;
            physicsCollider.radius = 1.2f; // Увеличено в 4 раза
            physicsCollider.height = 4f;   // Увеличено в 4 раза
            if (showDebugLog)
                Debug.Log($"[BoneBehavior] Создан увеличенный CapsuleCollider для физики {gameObject.name}");
        }
        
        // Ищем или создаем триггер для подбора
        SphereCollider[] sphereColliders = GetComponents<SphereCollider>();
        SphereCollider pickupTrigger = null;
        
        foreach (var col in sphereColliders)
        {
            if (col.isTrigger)
            {
                pickupTrigger = col;
                break;
            }
        }
        
        // Если нет триггера - создаем
        if (pickupTrigger == null)
        {
            pickupTrigger = gameObject.AddComponent<SphereCollider>();
            pickupTrigger.isTrigger = true;
            pickupTrigger.radius = 2f;
            if (showDebugLog)
                Debug.Log($"[BoneBehavior] Создан SphereCollider триггер для подбора {gameObject.name}");
        }
    }
    
    /// <summary>
    /// Подбор косточки игроком
    /// </summary>
    private void PickupBoneByPlayer(GameObject player)
    {
        if (showDebugLog)
            Debug.Log($"[BoneBehavior] 🦴 Игрок {player.name} подбирает косточку {gameObject.name}");
        
        // Добавляем косточку в инвентарь
        InventorySystem inventory = InventorySystem.Instance;
        if (inventory != null)
        {
            inventory.AddBone();
            if (showDebugLog)
                Debug.Log("[BoneBehavior] ✅ Косточка добавлена в инвентарь игрока");
        }
        else
        {
            Debug.LogError("[BoneBehavior] ❌ InventorySystem не найден!");
        }
        
        // Уничтожаем косточку
        Destroy(gameObject);
    }
    
    private void DiagnoseBone()
    {
        Debug.Log($"=== ДИАГНОСТИКА КОСТОЧКИ {gameObject.name} ===");
        Debug.Log($"Позиция: {transform.position}");
        Debug.Log($"Тег: '{gameObject.tag}'");
        Debug.Log($"Слой: {gameObject.layer} ({LayerMask.LayerToName(gameObject.layer)})");
        
        // Проверяем коллайдер
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            Debug.Log($"Коллайдер: {col.GetType().Name}, Is Trigger: {col.isTrigger}");
            if (col is SphereCollider sphere)
            {
                Debug.Log($"Радиус сферы: {sphere.radius}");
            }
        }
        else
        {
            Debug.LogError("❌ Коллайдер не найден!");
        }
        
        // Ищем ближайшие DogZoneTrigger
        DogZoneTrigger[] zones = FindObjectsOfType<DogZoneTrigger>();
        Debug.Log($"Найдено зон собаки: {zones.Length}");
        
        foreach (var zone in zones)
        {
            float distance = Vector3.Distance(transform.position, zone.transform.position);
            Debug.Log($"  Зона: {zone.name}, расстояние: {distance:F2}м");
            
            Collider zoneCol = zone.GetComponent<Collider>();
            if (zoneCol != null)
            {
                Debug.Log($"    Коллайдер зоны: {zoneCol.GetType().Name}, Is Trigger: {zoneCol.isTrigger}");
            }
        }
    }
    
    /// <summary>
    /// Перемещает косточку в зону собаки для тестирования
    /// </summary>
    [ContextMenu("Переместить в зону собаки")]
    public void MoveToDogsZone()
    {
        DogZoneTrigger[] zones = FindObjectsOfType<DogZoneTrigger>();
        if (zones.Length > 0)
        {
            // Берем первую найденную зону
            DogZoneTrigger zone = zones[0];
            Vector3 newPosition = zone.transform.position + Vector3.up * 2f; // Поднимаем на 2 метра
            
            transform.position = newPosition;
            Debug.Log($"[BoneBehavior] Косточка перемещена в зону собаки: {newPosition}");
        }
        else
        {
            Debug.LogError("[BoneBehavior] Зоны собаки не найдены!");
        }
    }
    
    [ContextMenu("Тестировать систему косточки")]
    public void TestBoneSystem()
    {
        Debug.Log($"[BoneBehavior] 🧪 ТЕСТИРОВАНИЕ системы косточки для {gameObject.name}");
        Debug.Log($"[BoneBehavior] Позиция косточки: {transform.position}");
        Debug.Log($"[BoneBehavior] Тег косточки: '{gameObject.tag}'");
        
        // Найдем все DogZoneTrigger'ы
        DogZoneTrigger[] triggers = FindObjectsOfType<DogZoneTrigger>();
        Debug.Log($"[BoneBehavior] Найдено {triggers.Length} DogZoneTrigger'ов");
        
        if (triggers.Length > 0)
        {
            DogZoneTrigger closestTrigger = triggers[0];
            float distance = Vector3.Distance(transform.position, closestTrigger.transform.position);
            Debug.Log($"[BoneBehavior] Ближайший триггер: {closestTrigger.name} на расстоянии {distance:F2}м");
            Debug.Log($"[BoneBehavior] 🚀 ПРИНУДИТЕЛЬНО вызываю OnTriggerEnter...");
            
            // Принудительно вызываем OnTriggerEnter
            Collider myCollider = GetComponent<Collider>();
            if (myCollider != null)
            {
                closestTrigger.SendMessage("OnTriggerEnter", myCollider, SendMessageOptions.DontRequireReceiver);
                Debug.Log($"[BoneBehavior] ✅ OnTriggerEnter вызван успешно!");
            }
            else
            {
                Debug.LogError($"[BoneBehavior] ❌ Коллайдер не найден на косточке!");
            }
        }
        else
        {
            Debug.LogError($"[BoneBehavior] ❌ DogZoneTrigger'ы не найдены!");
        }
    }
    
    /// <summary>
    /// Вызывается собакой когда она начинает есть косточку
    /// </summary>
    /// <param name="dog">Transform собаки</param>
    public void StartEating(Transform dog)
    {
        if (isBeingEaten)
        {
            if (showDebugLog)
                Debug.LogWarning($"[BoneBehavior] Косточка {gameObject.name} уже поедается!");
            return;
        }
        
        isBeingEaten = true;
        
        if (showDebugLog)
            Debug.Log($"[BoneBehavior] 🦴 Собака {dog.name} начала есть косточку {gameObject.name}");
        
        // Отключаем возможность подбора косточки игроком
        canBePickedUp = false;
        if (showDebugLog)
            Debug.Log($"[BoneBehavior] 🚫 Подбор косточки {gameObject.name} отключен - собака ест!");
        
        // Также отключаем ThrowableBone если есть
        ThrowableBone throwableBone = GetComponent<ThrowableBone>();
        if (throwableBone != null)
        {
            throwableBone.DisablePickup();
        }
        
        // Запускаем визуальные и звуковые эффекты
        StartEffects();
        
        // Запускаем корутину поедания
        StartCoroutine(EatingCoroutine(dog));
    }
    
    /// <summary>
    /// Проверяет, может ли собака есть эту косточку
    /// </summary>
    /// <param name="dogPosition">Позиция собаки</param>
    /// <returns>True если собака достаточно близко</returns>
    public bool CanBeEaten(Vector3 dogPosition)
    {
        if (isBeingEaten) return false;
        
        float distance = Vector3.Distance(transform.position, dogPosition);
        return distance <= eatingDistance;
    }
    
    /// <summary>
    /// Проверяет, поедается ли сейчас косточка
    /// </summary>
    public bool IsBeingEaten()
    {
        return isBeingEaten;
    }
    
    /// <summary>
    /// Получить позицию косточки
    /// </summary>
    public Vector3 GetPosition()
    {
        return transform.position;
    }
    
    private void StartEffects()
    {
        // Запускаем эффект частиц
        if (eatingEffect != null)
        {
            eatingEffect.Play();
            if (showDebugLog)
                Debug.Log($"[BoneBehavior] Запущен эффект частиц для {gameObject.name}");
        }
        
        // Воспроизводим звук
        if (audioSource != null && eatingSound != null)
        {
            audioSource.Play();
            if (showDebugLog)
                Debug.Log($"[BoneBehavior] Воспроизведен звук поедания для {gameObject.name}");
        }
    }
    
    private void StopEffects()
    {
        // Останавливаем эффект частиц
        if (eatingEffect != null)
        {
            eatingEffect.Stop();
        }
        
        // Останавливаем звук
        if (audioSource != null)
        {
            audioSource.Stop();
        }
    }
    
    private System.Collections.IEnumerator EatingCoroutine(Transform dog)
    {
        float elapsedTime = 0f;
        
        if (showDebugLog)
            Debug.Log($"[BoneBehavior] Начинается поедание косточки {gameObject.name}, время: {eatingTime} сек");
        
        while (elapsedTime < eatingTime)
        {
            elapsedTime += Time.deltaTime;
            
            // Опционально: можно добавить проверку, что собака все еще рядом
            if (dog == null)
            {
                if (showDebugLog)
                    Debug.LogWarning($"[BoneBehavior] Собака исчезла во время поедания косточки!");
                break;
            }
            
            yield return null;
        }
        
        // Поедание завершено
        FinishEating(dog);
    }
    
    private void FinishEating(Transform dog)
    {
        if (showDebugLog)
            Debug.Log($"[BoneBehavior] ✅ Косточка {gameObject.name} съедена собакой {dog?.name}!");
        
        // Останавливаем эффекты
        StopEffects();
        
        // Уведомляем собаку о завершении поедания
        if (dog != null)
        {
            DogPatrol dogPatrol = dog.GetComponent<DogPatrol>();
            if (dogPatrol != null)
            {
                dogPatrol.OnBoneEaten(this);
            }
        }
        
        // Уничтожаем косточку
        Destroy(gameObject);
    }
    
    /// <summary>
    /// Принудительно останавливает поедание (если собака отвлеклась)
    /// </summary>
    public void StopEating()
    {
        if (isBeingEaten)
        {
            if (showDebugLog)
                Debug.Log($"[BoneBehavior] Поедание косточки {gameObject.name} прервано");
            
            StopAllCoroutines();
            StopEffects();
            isBeingEaten = false;
        }
    }
    
    /// <summary>
    /// ПРИНУДИТЕЛЬНО заставляет ближайшую собаку бежать к косточке (публичный метод)
    /// </summary>
    public void ForceNearestDogToChasePublic()
    {
        ForceNearestDogToChase();
    }
    
    /// <summary>
    /// ПРИНУДИТЕЛЬНО заставляет ближайшую собаку бежать к косточке
    /// </summary>
    private void ForceNearestDogToChase()
    {
        Debug.Log($"[BoneBehavior] 🔍 ПРИНУДИТЕЛЬНЫЙ ПОИСК собак для косточки {gameObject.name}");
        
        // Найдем ВСЕ собаки в сцене
        DogPatrol[] dogs = FindObjectsOfType<DogPatrol>();
        Debug.Log($"[BoneBehavior] Найдено собак: {dogs.Length}");
        
        if (dogs.Length == 0)
        {
            Debug.LogError("[BoneBehavior] ❌ НИ ОДНОЙ СОБАКИ НЕ НАЙДЕНО!");
            return;
        }
        
        // Найдем ближайшую собаку
        DogPatrol nearestDog = null;
        float minDistance = float.MaxValue;
        
        foreach (var dog in dogs)
        {
            // Проверяем, может ли собака видеть косточки
            if (!dog.canSeeBones)
            {
                Debug.Log($"[BoneBehavior] Собака {dog.name} НЕ МОЖЕТ видеть косточки (canSeeBones = false)");
                continue;
            }
            
            float distance = Vector3.Distance(transform.position, dog.transform.position);
            Debug.Log($"[BoneBehavior] Собака {dog.name} на расстоянии {distance:F1}м (радиус обнаружения: {dog.boneDetectionRadius}м)");
            
            // Проверяем, находится ли косточка в радиусе обнаружения собаки
            if (distance <= dog.boneDetectionRadius && distance < minDistance)
            {
                minDistance = distance;
                nearestDog = dog;
                Debug.Log($"[BoneBehavior] ✅ Собака {dog.name} МОЖЕТ обнаружить косточку!");
            }
            else if (distance > dog.boneDetectionRadius)
            {
                Debug.Log($"[BoneBehavior] ❌ Собака {dog.name} слишком далеко (>{dog.boneDetectionRadius}м)");
            }
        }
        
        if (nearestDog != null)
        {
            Debug.Log($"[BoneBehavior] 🎯 БЛИЖАЙШАЯ СОБАКА: {nearestDog.name} на расстоянии {minDistance:F1}м");
            Debug.Log($"[BoneBehavior] 🚀 ПРИНУДИТЕЛЬНО ЗАСТАВЛЯЮ СОБАКУ БЕЖАТЬ К КОСТОЧКЕ!");
            
            // ПРИНУДИТЕЛЬНО заставляем собаку преследовать косточку
            nearestDog.SetChasing(true, transform);
        }
        else
        {
            Debug.LogError("[BoneBehavior] ❌ Ближайшая собака не найдена!");
        }
    }
    
    private void OnDrawGizmos()
    {
        // Рисуем радиус поедания в редакторе
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, eatingDistance);
        
        // Рисуем иконку косточки
        Gizmos.color = Color.white;
        Gizmos.DrawCube(transform.position + Vector3.up * 0.5f, Vector3.one * 0.2f);
    }
}
