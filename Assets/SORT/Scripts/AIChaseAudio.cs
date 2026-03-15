using UnityEngine;
using System.Collections;

/// <summary>
/// AI враг с аудио при преследовании
/// Проигрывает случайные аудиофайлы когда AI заметил игрока и начал преследование
/// </summary>
public class AIChaseAudio : MonoBehaviour
{
    [Header("Настройки аудио")]
    [Tooltip("Массив аудиоклипов для преследования (максимум 6)")]
    public AudioClip[] chaseAudioClips = new AudioClip[6];
    
    [Tooltip("Громкость аудио")]
    [Range(0f, 10f)]
    public float volume = 1.6f;
    
    [Tooltip("Задержка между аудио (секунды)")]
    public float audioCooldown = 3f;
    
    [Header("Настройки отладки")]
    [Tooltip("Показывать сообщения в консоли")]
    public bool debugMessages = true;
    
    [Header("Радиус активации")]
    [Tooltip("Радиус активации звука (метры)")]
    [Range(0.5f, 20f)]
    public float activationRadius = 5f;
    
    [Header("Визуальный радиус")]
    [Tooltip("Показывать радиус активации звука")]
    public bool showAudioRadius = true;
    
    [Tooltip("Размер визуального радиуса (метры)")]
    [Range(0.5f, 10f)]
    public float radiusSize = 2f;
    
    [Tooltip("Цвет радиуса когда звук активен")]
    public Color activeRadiusColor = Color.red;
    
    [Tooltip("Цвет радиуса когда звук неактивен")]
    public Color inactiveRadiusColor = Color.yellow;
    
    private GameObject player;
    private AudioSource audioSource;
    private bool canPlayAudio = true;
    private bool isAudioActive = false;
    private Coroutine audioCoroutine;
    
    // Invector AI компоненты
    private Component aiController;
    private Component aiMotor;
    private Component aiMeleeManager;
    
    // Состояния AI
    private bool wasChasing = false;
    private bool isCurrentlyChasing = false;
    private bool isMovingTowardsPlayer = false;
    private float lastAudioTime = 0f;
    
    // Визуальный радиус
    private GameObject radiusVisual;
    private Material radiusMaterial;
    
    void Start()
    {
        // Ищем игрока
        player = GameObject.FindGameObjectWithTag("Player");
        
        // Создаем и настраиваем AudioSource
        SetupAudioSource();
        
        // Находим Invector AI компоненты
        FindInvectorComponents();
        
        // Создаем визуальный радиус
        CreateAudioRadius();
        
        if (debugMessages)
        {
            Debug.Log($"[AIChaseAudio] ✅ AI готов: {gameObject.name}");
            Debug.Log($"[AIChaseAudio] 🔊 Аудиофайлов: {GetValidAudioCount()}");
        }
    }
    
    void Update()
    {
        // Проверяем состояние преследования AI
        CheckAIChaseState();
    }
    
    void SetupAudioSource()
    {
        // Автоматически создаем AudioSource
        audioSource = gameObject.AddComponent<AudioSource>();
        
        // Настраиваем AudioSource
        audioSource.volume = volume;
        audioSource.spatialBlend = 1f; // 3D звук
        audioSource.maxDistance = 20f;
        audioSource.rolloffMode = AudioRolloffMode.Linear;
        audioSource.loop = false;
        
        if (debugMessages)
        {
            Debug.Log($"[AIChaseAudio] 🔊 AudioSource создан автоматически");
        }
    }
    
    void CreateAudioRadius()
    {
        if (!showAudioRadius) return;
        
        // Создаем GameObject для радиуса
        radiusVisual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        radiusVisual.name = "AudioRadius";
        radiusVisual.transform.SetParent(transform);
        radiusVisual.transform.localPosition = Vector3.zero;
        radiusVisual.transform.localScale = Vector3.one * radiusSize; // Настраиваемый радиус
        
        // Убираем коллайдер
        DestroyImmediate(radiusVisual.GetComponent<Collider>());
        
        // Создаем материал для радиуса
        radiusMaterial = new Material(Shader.Find("Standard"));
        radiusMaterial.SetFloat("_Mode", 3); // Transparent mode
        radiusMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        radiusMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        radiusMaterial.SetInt("_ZWrite", 0);
        radiusMaterial.DisableKeyword("_ALPHATEST_ON");
        radiusMaterial.EnableKeyword("_ALPHABLEND_ON");
        radiusMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        radiusMaterial.renderQueue = 3000;
        
        // Применяем материал
        radiusVisual.GetComponent<Renderer>().material = radiusMaterial;
        
        // Устанавливаем начальный цвет
        radiusMaterial.color = inactiveRadiusColor;
        
        if (debugMessages)
        {
            Debug.Log($"[AIChaseAudio] 🎯 Визуальный радиус создан");
        }
    }
    
    void UpdateAudioRadius()
    {
        if (!showAudioRadius || radiusVisual == null || radiusMaterial == null) return;
        
        // Обновляем размер радиуса если изменился
        if (radiusVisual.transform.localScale.x != radiusSize)
        {
            radiusVisual.transform.localScale = Vector3.one * radiusSize;
        }
        
        // Определяем цвет радиуса
        Color targetColor;
        
        if (isAudioActive)
        {
            // Красный когда звук активен
            targetColor = activeRadiusColor;
        }
        else if (isCurrentlyChasing && isMovingTowardsPlayer)
        {
            // Оранжевый когда AI преследует и движется
            targetColor = Color.Lerp(inactiveRadiusColor, activeRadiusColor, 0.5f);
        }
        else if (isCurrentlyChasing)
        {
            // Желтый когда AI преследует но не движется
            targetColor = inactiveRadiusColor;
        }
        else
        {
            // Прозрачный когда AI не преследует
            targetColor = new Color(inactiveRadiusColor.r, inactiveRadiusColor.g, inactiveRadiusColor.b, 0.3f);
        }
        
        // Плавно изменяем цвет
        radiusMaterial.color = Color.Lerp(radiusMaterial.color, targetColor, Time.deltaTime * 5f);
        
        // Показываем/скрываем радиус
        radiusVisual.SetActive(isCurrentlyChasing || isAudioActive);
    }
    
    void FindInvectorComponents()
    {
        // Ищем Invector AI компоненты
        aiController = gameObject.GetComponent("v_AIController");
        aiMotor = gameObject.GetComponent("v_AIMotor");
        aiMeleeManager = gameObject.GetComponent("vMeleeManager");
        
        if (debugMessages)
        {
            Debug.Log($"[AIChaseAudio] 🎮 AI компоненты найдены:");
            Debug.Log($"[AIChaseAudio] 🎮 v_AIController: {(aiController != null ? "✅" : "❌")}");
            Debug.Log($"[AIChaseAudio] 🎮 v_AIMotor: {(aiMotor != null ? "✅" : "❌")}");
            Debug.Log($"[AIChaseAudio] 🎮 vMeleeManager: {(aiMeleeManager != null ? "✅" : "❌")}");
        }
    }
    
    void CheckAIChaseState()
    {
        if (player == null) return;
        
        // Проверяем текущее состояние преследования
        isCurrentlyChasing = IsAIChasing();
        isMovingTowardsPlayer = IsAIMovingTowardsPlayer();
        
        // Если AI преследует игрока И движется к нему
        if (isCurrentlyChasing && isMovingTowardsPlayer)
        {
            // Проверяем можно ли проиграть аудио (кулдаун)
            if (canPlayAudio && !isAudioActive && Time.time - lastAudioTime >= audioCooldown)
            {
                if (debugMessages)
                {
                    Debug.Log($"[AIChaseAudio] 🎯 AI преследует и движется к игроку!");
                }
                
                PlayRandomChaseAudio();
                lastAudioTime = Time.time;
            }
        }
        
        // Обновляем предыдущее состояние
        wasChasing = isCurrentlyChasing;
        
        // Обновляем визуальный радиус
        UpdateAudioRadius();
    }
    
    bool IsAIChasing()
    {
        // Проверяем состояние AI через Invector компоненты
        if (aiController != null)
        {
            var type = aiController.GetType();
            
            // Проверяем различные поля состояния AI
            var fields = new string[] { "isChasing", "isPursuing", "isFollowing", "isAttacking", "isInCombat", "currentState" };
            
            foreach (var fieldName in fields)
            {
                var field = type.GetField(fieldName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null)
                {
                    var value = field.GetValue(aiController);
                    if (value is bool && (bool)value)
                    {
                        return true;
                    }
                    else if (value is string && (string)value == "Chase")
                    {
                        return true;
                    }
                }
            }
        }
        
        // Альтернативная проверка через дистанцию до игрока
        if (player != null)
        {
            float distance = Vector3.Distance(transform.position, player.transform.position);
            return distance <= activationRadius; // Используем настраиваемый радиус
        }
        
        return false;
    }
    
    bool IsAIMovingTowardsPlayer()
    {
        if (player == null) return false;
        
        // Проверяем движение AI через NavMeshAgent
        var navAgent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (navAgent != null)
        {
            // Если AI имеет путь к игроку и движется
            if (navAgent.hasPath && navAgent.velocity.magnitude > 0.1f)
            {
                // Проверяем направление движения
                Vector3 directionToPlayer = (player.transform.position - transform.position).normalized;
                Vector3 aiVelocity = navAgent.velocity.normalized;
                
                // Если AI движется в сторону игрока (угол меньше 90 градусов)
                float dotProduct = Vector3.Dot(aiVelocity, directionToPlayer);
                return dotProduct > 0.3f; // Порог для определения движения к игроку
            }
        }
        
        // Альтернативная проверка через дистанцию
        float distance = Vector3.Distance(transform.position, player.transform.position);
        return distance <= activationRadius * 0.8f; // 80% от радиуса активации для движения
    }
    
    void PlayRandomChaseAudio()
    {
        if (!canPlayAudio || isAudioActive) return;
        
        // Выбираем случайный аудиофайл
        int randomIndex = GetRandomAudioIndex();
        
        if (randomIndex >= 0)
        {
            if (debugMessages)
            {
                Debug.Log($"[AIChaseAudio] 🔊 Проигрываю аудио {randomIndex + 1}: {chaseAudioClips[randomIndex].name}");
            }
            
            // Запускаем корутину аудио
            audioCoroutine = StartCoroutine(PlayAudioCoroutine(randomIndex));
        }
    }
    
    int GetRandomAudioIndex()
    {
        int validCount = GetValidAudioCount();
        
        if (validCount == 0)
        {
            if (debugMessages)
                Debug.LogWarning("[AIChaseAudio] ⚠️ Нет доступных аудиофайлов!");
            return -1;
        }
        
        return Random.Range(0, validCount);
    }
    
    int GetValidAudioCount()
    {
        int count = 0;
        for (int i = 0; i < chaseAudioClips.Length; i++)
        {
            if (chaseAudioClips[i] != null)
            {
                count++;
            }
        }
        return count;
    }
    
    IEnumerator PlayAudioCoroutine(int audioIndex)
    {
        isAudioActive = true;
        
        AudioClip audioClip = chaseAudioClips[audioIndex];
        
        // Проигрываем звук
        if (audioClip != null && audioSource != null)
        {
            audioSource.clip = audioClip;
            audioSource.Play();
            
            if (debugMessages)
            {
                Debug.Log($"[AIChaseAudio] 🔊 Проигрываю аудио: {audioClip.name}");
            }
        }
        
        // Ждем пока звук закончится
        yield return new WaitForSeconds(audioClip.length);
        
        isAudioActive = false;
        
        if (debugMessages)
        {
            Debug.Log("[AIChaseAudio] ✅ Аудио завершено");
        }
    }
    
    /// <summary>
    /// Принудительно проиграть случайное аудио
    /// </summary>
    [ContextMenu("Проиграть случайное аудио")]
    public void ForcePlayRandomAudio()
    {
        if (canPlayAudio && !isAudioActive)
        {
            PlayRandomChaseAudio();
        }
    }
    
    /// <summary>
    /// Остановить текущий аудио
    /// </summary>
    [ContextMenu("Остановить аудио")]
    public void StopAudio()
    {
        if (audioCoroutine != null)
        {
            StopCoroutine(audioCoroutine);
        }
        
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
        }
        
        isAudioActive = false;
        canPlayAudio = true;
        
        if (debugMessages)
        {
            Debug.Log("[AIChaseAudio] ⏹️ Аудио остановлено");
        }
    }
    
    /// <summary>
    /// Добавить аудиофайл в массив
    /// </summary>
    [ContextMenu("Добавить аудиофайл")]
    public void AddAudioClip()
    {
        // Расширяем массив
        AudioClip[] newClips = new AudioClip[chaseAudioClips.Length + 1];
        
        // Копируем старые данные
        for (int i = 0; i < chaseAudioClips.Length; i++)
        {
            newClips[i] = chaseAudioClips[i];
        }
        
        // Добавляем новую пустую ячейку
        newClips[chaseAudioClips.Length] = null;
        
        chaseAudioClips = newClips;
        
        if (debugMessages)
        {
            Debug.Log($"[AIChaseAudio] ➕ Добавлен новый аудиофайл. Всего: {chaseAudioClips.Length}");
        }
    }
    
    /// <summary>
    /// Показать информацию о AI аудио
    /// </summary>
    [ContextMenu("Показать информацию")]
    public void ShowAudioInfo()
    {
        if (debugMessages)
        {
            Debug.Log($"[AIChaseAudio] 📊 Информация о AI аудио:");
            Debug.Log($"[AIChaseAudio] 📍 AI: {gameObject.name}");
            Debug.Log($"[AIChaseAudio] 📏 Радиус активации: {activationRadius}м");
            Debug.Log($"[AIChaseAudio] ⏱️ Кулдаун: {audioCooldown}с");
            Debug.Log($"[AIChaseAudio] 🔊 Всего аудиофайлов: {chaseAudioClips.Length}");
            Debug.Log($"[AIChaseAudio] ✅ Валидных аудиофайлов: {GetValidAudioCount()}");
            Debug.Log($"[AIChaseAudio] 🎯 Текущее состояние преследования: {isCurrentlyChasing}");
            
            for (int i = 0; i < chaseAudioClips.Length; i++)
            {
                string status = chaseAudioClips[i] != null ? "✅" : "❌";
                string audioName = chaseAudioClips[i] != null ? chaseAudioClips[i].name : "Пусто";
                Debug.Log($"[AIChaseAudio] {status} Аудио {i + 1}: {audioName}");
            }
        }
    }
    
    /// <summary>
    /// Проверить состояние AI
    /// </summary>
    [ContextMenu("Проверить состояние AI")]
    public void CheckAIState()
    {
        if (debugMessages)
        {
            Debug.Log($"[AIChaseAudio] 🔍 Проверка состояния AI:");
            Debug.Log($"[AIChaseAudio] 🎯 Преследует игрока: {IsAIChasing()}");
            Debug.Log($"[AIChaseAudio] 🏃 Движется к игроку: {IsAIMovingTowardsPlayer()}");
            Debug.Log($"[AIChaseAudio] 🎮 v_AIController: {(aiController != null ? "Найден" : "Не найден")}");
            Debug.Log($"[AIChaseAudio] 🎮 v_AIMotor: {(aiMotor != null ? "Найден" : "Не найден")}");
            Debug.Log($"[AIChaseAudio] 🎮 vMeleeManager: {(aiMeleeManager != null ? "Найден" : "Не найден")}");
            
            // Проверяем NavMeshAgent
            var navAgent = GetComponent<UnityEngine.AI.NavMeshAgent>();
            Debug.Log($"[AIChaseAudio] 🎮 NavMeshAgent: {(navAgent != null ? "Найден" : "Не найден")}");
            
            if (navAgent != null)
            {
                Debug.Log($"[AIChaseAudio] 🏃 Скорость AI: {navAgent.velocity.magnitude:F2}");
                Debug.Log($"[AIChaseAudio] 🛤️ Есть путь: {navAgent.hasPath}");
            }
            
            if (player != null)
            {
                float distance = Vector3.Distance(transform.position, player.transform.position);
                Debug.Log($"[AIChaseAudio] 📏 Дистанция до игрока: {distance:F2}");
                
                // Показываем направление движения
                Vector3 directionToPlayer = (player.transform.position - transform.position).normalized;
                Debug.Log($"[AIChaseAudio] 🧭 Направление к игроку: {directionToPlayer}");
                
                if (navAgent != null && navAgent.velocity.magnitude > 0.1f)
                {
                    Vector3 aiVelocity = navAgent.velocity.normalized;
                    float dotProduct = Vector3.Dot(aiVelocity, directionToPlayer);
                    Debug.Log($"[AIChaseAudio] 📐 Угол движения к игроку: {dotProduct:F2}");
                }
            }
        }
    }
    
    void OnDrawGizmosSelected()
    {
        // Показываем зону активации в редакторе
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, activationRadius); // Радиус активации
        
        // Показываем зону движения
        Gizmos.color = new Color(1f, 0.5f, 0f); // Оранжевый цвет
        Gizmos.DrawWireSphere(transform.position, activationRadius * 0.8f); // Зона движения
        
        // Показываем визуальный радиус если включен
        if (showAudioRadius)
        {
            Gizmos.color = activeRadiusColor;
            Gizmos.DrawWireSphere(transform.position, radiusSize);
        }
    }
    
    void OnDestroy()
    {
        // Очищаем ресурсы
        if (radiusMaterial != null)
        {
            DestroyImmediate(radiusMaterial);
        }
    }
}
