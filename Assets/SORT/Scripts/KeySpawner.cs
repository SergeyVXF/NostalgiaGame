using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class KeySpawner : MonoBehaviour
{
    [Header("Настройки спавнера")]
    [Tooltip("ID ключа, необходимого для активации")]
    public string requiredKeyID = "garage_key_01";
    
    [Tooltip("Название места (для отображения в UI)")]
    public string locationName = "Желтый гараж";
    
    [Tooltip("Описание действия")]
    public string actionDescription = "Нажмите E для открытия гаража";
    
    [Header("Объекты для спавна")]
    [Tooltip("Первый объект, который будет создан")]
    public GameObject objectToSpawn1;
    
    [Tooltip("Мировая позиция спавна первого объекта")]
    public Vector3 worldSpawnPosition1 = Vector3.zero;
    
    [Tooltip("Поворот первого объекта при спавне")]
    public Vector3 spawnRotation1 = Vector3.zero;
    
    [Tooltip("Задержка спавна первого объекта (секунды)")]
    public float spawnDelay1 = 0f;
    
    [Tooltip("Второй объект, который будет создан")]
    public GameObject objectToSpawn2;
    
    [Tooltip("Мировая позиция спавна второго объекта")]
    public Vector3 worldSpawnPosition2 = Vector3.zero;
    
    [Tooltip("Поворот второго объекта при спавне")]
    public Vector3 spawnRotation2 = Vector3.zero;
    
    [Tooltip("Задержка спавна второго объекта (секунды)")]
    public float spawnDelay2 = 0.5f;
    
    [Tooltip("Третий объект, который будет создан")]
    public GameObject objectToSpawn3;
    
    [Tooltip("Мировая позиция спавна третьего объекта")]
    public Vector3 worldSpawnPosition3 = Vector3.zero;
    
    [Tooltip("Поворот третьего объекта при спавне")]
    public Vector3 spawnRotation3 = Vector3.zero;
    
    [Tooltip("Задержка спавна третьего объекта (секунды)")]
    public float spawnDelay3 = 1f;
    
    [Tooltip("Четвертый объект, который будет создан")]
    public GameObject objectToSpawn4;
    
    [Tooltip("Мировая позиция спавна четвертого объекта")]
    public Vector3 worldSpawnPosition4 = Vector3.zero;
    
    [Tooltip("Поворот четвертого объекта при спавне")]
    public Vector3 spawnRotation4 = Vector3.zero;
    
    [Tooltip("Задержка спавна четвертого объекта (секунды)")]
    public float spawnDelay4 = 1.5f;
    
    [Header("Интеракция")]
    [Tooltip("Расстояние для активации интеракции")]
    public float interactionDistance = 3f;
    
    [Tooltip("Клавиша для активации")]
    public KeyCode activationKey = KeyCode.E;
    
    [Header("Звуковые эффекты")]
    [Tooltip("Звук успешной активации")]
    public AudioClip successSound;
    
    [Tooltip("Звук ошибки (нет ключа)")]
    public AudioClip errorSound;
    
    [Tooltip("Громкость звуков")]
    [Range(0f, 1f)]
    public float soundVolume = 0.8f;
    
    [Header("Визуальные эффекты")]
    [Tooltip("Эффект частиц при успешной активации")]
    public ParticleSystem successEffect;
    
    [Tooltip("Эффект частиц при ошибке")]
    public ParticleSystem errorEffect;
    
    [Header("UI иконка взаимодействия")]
    [Tooltip("UI объект для отображения подсказки (создается автоматически если не назначен)")]
    public GameObject promptUI;
    
    [Tooltip("Текст подсказки (создается автоматически если не назначен)")]
    public TMPro.TextMeshProUGUI promptText;
    
    [Header("Отладка")]
    [Tooltip("Показывать отладочную информацию")]
    public bool showDebugLog = true;
    
    private bool isActivated = false;
    private bool isInTrigger = false;
    private GameObject spawnedObject1 = null;
    private GameObject spawnedObject2 = null;
    private GameObject spawnedObject3 = null;
    private GameObject spawnedObject4 = null;
    private KeyInventory playerKeyInventory = null;
    
    private void Start()
    {
        if (showDebugLog)
            Debug.Log($"[KeySpawner] 🏠 Спавнер '{locationName}' инициализирован. Требуемый ключ: {requiredKeyID}");
        
        CreatePromptUI();
    }
    
    private void CreatePromptUI()
    {
        if (promptUI == null)
        {
            // Создаем Canvas если его нет
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasObj = new GameObject("PromptCanvas");
                canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasObj.AddComponent<CanvasScaler>();
                canvasObj.AddComponent<GraphicRaycaster>();
            }
            
            // Создаем UI для подсказки
            promptUI = new GameObject("KeySpawnerPrompt");
            promptUI.transform.SetParent(canvas.transform, false);
            
            // Добавляем текст
            promptText = promptUI.AddComponent<TextMeshProUGUI>();
            promptText.text = actionDescription;
            promptText.fontSize = 24;
            promptText.alignment = TextAlignmentOptions.Center;
            promptText.color = Color.white;
            
            // Позиционируем в центре экрана
            RectTransform rect = promptUI.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            
            promptUI.SetActive(false);
            
            if (showDebugLog)
                Debug.Log($"[KeySpawner] ✅ UI создан автоматически");
        }
    }
    
    private void Update()
    {
        // Проверяем, не активирован ли уже спавнер
        if (isActivated) return;
        
        // Если игрок в триггере, проверяем наличие ключа и обновляем подсказку
        if (isInTrigger)
        {
            CheckAndUpdatePrompt();
            
            // Проверяем нажатие клавиши
            if (Input.GetKeyDown(activationKey))
            {
                HandleActivationAttempt();
            }
        }
        else
        {
            // Если игрок не в триггере, но нажимает E рядом с гаражом
            if (Input.GetKeyDown(activationKey))
            {
                // Проверяем расстояние до игрока
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    float distance = Vector3.Distance(transform.position, player.transform.position);
                    if (distance <= interactionDistance * 1.5f) // Немного увеличенная зона
                    {
                        HandleActivationAttempt();
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// Обрабатывает попытку активации спавнера
    /// </summary>
    private void HandleActivationAttempt()
    {
        // Получаем инвентарь ключей игрока
        if (playerKeyInventory == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerKeyInventory = player.GetComponent<KeyInventory>();
            }
        }
        
        // Проверяем наличие ключа
        if (playerKeyInventory != null && playerKeyInventory.HasKey(requiredKeyID))
        {
            TryActivateSpawner();
        }
        else
        {
            if (showDebugLog)
                Debug.Log($"[KeySpawner] ❌ У игрока нет ключа '{requiredKeyID}' для активации '{locationName}'");
            
            // Воспроизводим звук ошибки
            PlayErrorSound();
            PlayErrorEffect();
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isInTrigger = true;
            CheckAndUpdatePrompt();
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isInTrigger = false;
            HidePrompt();
        }
    }
    
    private void ShowPrompt()
    {
        if (promptUI != null)
        {
            promptUI.SetActive(true);
            if (showDebugLog)
                Debug.Log($"[KeySpawner] 🎯 Показываю подсказку для '{locationName}' (есть ключ)");
        }
    }
    
    private void HidePrompt()
    {
        if (promptUI != null)
        {
            promptUI.SetActive(false);
            if (showDebugLog)
                Debug.Log($"[KeySpawner] 🎯 Скрываю подсказку для '{locationName}'");
        }
    }
    
    /// <summary>
    /// Проверяет наличие ключа и показывает/скрывает подсказку
    /// </summary>
    private void CheckAndUpdatePrompt()
    {
        // Получаем инвентарь ключей игрока
        if (playerKeyInventory == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerKeyInventory = player.GetComponent<KeyInventory>();
            }
        }
        
        // Проверяем наличие ключа
        bool hasKey = playerKeyInventory != null && playerKeyInventory.HasKey(requiredKeyID);
        
        if (hasKey)
        {
            ShowPrompt();
            if (showDebugLog)
                Debug.Log($"[KeySpawner] ✅ У игрока есть ключ '{requiredKeyID}' - показываю подсказку");
        }
        else
        {
            HidePrompt();
            if (showDebugLog)
                Debug.Log($"[KeySpawner] ❌ У игрока нет ключа '{requiredKeyID}' - скрываю подсказку");
        }
    }
    
    /// <summary>
    /// Пытается активировать спавнер
    /// </summary>
    private void TryActivateSpawner()
    {
        if (isActivated)
        {
            if (showDebugLog)
                Debug.Log($"[KeySpawner] ⚠️ Спавнер '{locationName}' уже активирован");
            return;
        }
        
        if (showDebugLog)
            Debug.Log($"[KeySpawner] ✅ Активирую спавнер '{locationName}' с ключом '{requiredKeyID}'");
        
        // Активируем спавнер
        ActivateSpawner();
    }
    
    /// <summary>
    /// Активирует спавнер и создает объекты с задержками
    /// </summary>
    private void ActivateSpawner()
    {
        // Удаляем ключ из инвентаря
        playerKeyInventory.RemoveKey(requiredKeyID);
        
        // Отмечаем как активированный
        isActivated = true;
        
        // Скрываем UI после активации
        HidePrompt();
        
        // Воспроизводим эффекты
        PlaySuccessSound();
        PlaySuccessEffect();
        
        if (showDebugLog)
        {
            Debug.Log($"[KeySpawner] ✅ Спавнер '{locationName}' активирован!");
            Debug.Log($"  🔑 Ключ '{requiredKeyID}' удален из инвентаря");
            Debug.Log($"  ⏰ Начинаю спавн объектов с задержками...");
        }
        
        // Запускаем корутины для спавна объектов с задержками
        StartCoroutine(SpawnObjectWithDelay(1, objectToSpawn1, worldSpawnPosition1, spawnRotation1, spawnDelay1));
        StartCoroutine(SpawnObjectWithDelay(2, objectToSpawn2, worldSpawnPosition2, spawnRotation2, spawnDelay2));
        StartCoroutine(SpawnObjectWithDelay(3, objectToSpawn3, worldSpawnPosition3, spawnRotation3, spawnDelay3));
        StartCoroutine(SpawnObjectWithDelay(4, objectToSpawn4, worldSpawnPosition4, spawnRotation4, spawnDelay4));
    }
    
    /// <summary>
    /// Создает объект с задержкой
    /// </summary>
    private System.Collections.IEnumerator SpawnObjectWithDelay(int objectNumber, GameObject objectToSpawn, Vector3 worldPosition, Vector3 rotation, float delay)
    {
        if (objectToSpawn == null)
        {
            if (showDebugLog)
                Debug.Log($"[KeySpawner] ⚠️ Объект {objectNumber} не назначен - пропускаю");
            yield break;
        }
        
        // Ждем указанную задержку
        if (delay > 0f)
        {
            if (showDebugLog)
                Debug.Log($"[KeySpawner] ⏰ Ожидаю {delay}с перед спавном объекта {objectNumber}");
            yield return new WaitForSeconds(delay);
        }
        
        // Создаем объект
        Quaternion spawnRotationQuat = Quaternion.Euler(rotation);
        GameObject spawnedObject = Instantiate(objectToSpawn, worldPosition, spawnRotationQuat);
        
        // Сохраняем ссылку на созданный объект
        switch (objectNumber)
        {
            case 1:
                spawnedObject1 = spawnedObject;
                break;
            case 2:
                spawnedObject2 = spawnedObject;
                break;
            case 3:
                spawnedObject3 = spawnedObject;
                break;
            case 4:
                spawnedObject4 = spawnedObject;
                break;
        }
        
        if (showDebugLog)
        {
            Debug.Log($"[KeySpawner] 🎯 Объект {objectNumber} создан:");
            Debug.Log($"  📦 Префаб: {objectToSpawn.name}");
            Debug.Log($"  📍 Мировая позиция: {worldPosition}");
            Debug.Log($"  🔄 Поворот: {rotation}");
            Debug.Log($"  ⏰ Задержка: {delay}с");
        }
    }
    
    /// <summary>
    /// Воспроизводит звук успеха
    /// </summary>
    private void PlaySuccessSound()
    {
        if (successSound == null) return;
        
        GameObject audioObject = new GameObject("SuccessSound_Temp");
        audioObject.transform.position = transform.position;
        
        AudioSource audioSource = audioObject.AddComponent<AudioSource>();
        audioSource.clip = successSound;
        audioSource.volume = soundVolume;
        audioSource.spatialBlend = 1f;
        audioSource.rolloffMode = AudioRolloffMode.Linear;
        audioSource.maxDistance = 30f;
        
        audioSource.Play();
        
        if (showDebugLog)
            Debug.Log($"[KeySpawner] 🔊 Звук успеха воспроизведен: {successSound.name}");
        
        Destroy(audioObject, successSound.length + 0.1f);
    }
    
    /// <summary>
    /// Воспроизводит звук ошибки
    /// </summary>
    private void PlayErrorSound()
    {
        if (errorSound == null) return;
        
        GameObject audioObject = new GameObject("ErrorSound_Temp");
        audioObject.transform.position = transform.position;
        
        AudioSource audioSource = audioObject.AddComponent<AudioSource>();
        audioSource.clip = errorSound;
        audioSource.volume = soundVolume;
        audioSource.spatialBlend = 1f;
        audioSource.rolloffMode = AudioRolloffMode.Linear;
        audioSource.maxDistance = 30f;
        
        audioSource.Play();
        
        if (showDebugLog)
            Debug.Log($"[KeySpawner] 🔊 Звук ошибки воспроизведен: {errorSound.name}");
        
        Destroy(audioObject, errorSound.length + 0.1f);
    }
    
    /// <summary>
    /// Воспроизводит эффект успеха
    /// </summary>
    private void PlaySuccessEffect()
    {
        if (successEffect == null) return;
        
        ParticleSystem effect = Instantiate(successEffect, transform.position, transform.rotation);
        effect.Play();
        
        if (showDebugLog)
            Debug.Log($"[KeySpawner] ✨ Эффект успеха воспроизведен");
        
        Destroy(effect.gameObject, effect.main.duration + 1f);
    }
    
    /// <summary>
    /// Воспроизводит эффект ошибки
    /// </summary>
    private void PlayErrorEffect()
    {
        if (errorEffect == null) return;
        
        ParticleSystem effect = Instantiate(errorEffect, transform.position, transform.rotation);
        effect.Play();
        
        if (showDebugLog)
            Debug.Log($"[KeySpawner] ✨ Эффект ошибки воспроизведен");
        
        Destroy(effect.gameObject, effect.main.duration + 1f);
    }
    
    private void OnDrawGizmos()
    {
        // Рисуем область интеракции
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, interactionDistance);
        
        // Рисуем мировые позиции спавна для всех объектов
        if (objectToSpawn1 != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireCube(worldSpawnPosition1, Vector3.one);
            Gizmos.DrawLine(transform.position, worldSpawnPosition1);
            
            // Рисуем текст с задержкой
            #if UNITY_EDITOR
            UnityEditor.Handles.Label(worldSpawnPosition1 + Vector3.up * 1.5f, $"Объект 1\nЗадержка: {spawnDelay1}с");
            #endif
        }
        
        if (objectToSpawn2 != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(worldSpawnPosition2, Vector3.one);
            Gizmos.DrawLine(transform.position, worldSpawnPosition2);
            
            #if UNITY_EDITOR
            UnityEditor.Handles.Label(worldSpawnPosition2 + Vector3.up * 1.5f, $"Объект 2\nЗадержка: {spawnDelay2}с");
            #endif
        }
        
        if (objectToSpawn3 != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(worldSpawnPosition3, Vector3.one);
            Gizmos.DrawLine(transform.position, worldSpawnPosition3);
            
            #if UNITY_EDITOR
            UnityEditor.Handles.Label(worldSpawnPosition3 + Vector3.up * 1.5f, $"Объект 3\nЗадержка: {spawnDelay3}с");
            #endif
        }
        
        if (objectToSpawn4 != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireCube(worldSpawnPosition4, Vector3.one);
            Gizmos.DrawLine(transform.position, worldSpawnPosition4);
            
            #if UNITY_EDITOR
            UnityEditor.Handles.Label(worldSpawnPosition4 + Vector3.up * 1.5f, $"Объект 4\nЗадержка: {spawnDelay4}с");
            #endif
        }
    }
}
