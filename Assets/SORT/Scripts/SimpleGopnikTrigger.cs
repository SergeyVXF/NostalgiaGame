using UnityEngine;
using System.Collections;

/// <summary>
/// Простой скрипт для активации второго триггера после контакта с первым
/// </summary>
public class SimpleGopnikTrigger : MonoBehaviour
{
    [Tooltip("Объект, который нужно активировать")]
    public GameObject objectToActivate;
    
    [Tooltip("Объект, который нужно активировать с задержкой (из сцены, не из Assets)")]
    public GameObject delayedObjectToActivate;
    
    [Tooltip("Префаб, который нужно создать с задержкой")]
    public GameObject delayedPrefabToInstantiate;
    
    [Tooltip("Задержка перед активацией второго объекта (в секундах)")]
    public float activationDelay = 3f;
    
    [Tooltip("Тег объекта, который может активировать триггер")]
    public string targetTag = "Player";
    
    [Tooltip("Отключить этот триггер после активации")]
    public bool disableSelfAfterActivation = false;
    
    [Tooltip("Смещение точки появления относительно триггера")]
    public Vector3 spawnOffset = Vector3.zero;
    
    [Header("Телепортация игрока")]
    [Tooltip("Телепортировать игрока в указанные координаты")]
    public bool teleportPlayer = false;
    
    [Tooltip("Координаты для телепортации игрока")]
    public Vector3 playerTeleportPosition = Vector3.zero;
    
    [Tooltip("Задержка перед телепортацией игрока (в секундах)")]
    public float playerTeleportDelay = 0f;
    
    [Header("Аудио настройки")]
    [Tooltip("Аудиофайл для воспроизведения при активации триггера")]
    public AudioClip activationAudio;
    
    [Tooltip("Задержка перед воспроизведением аудио (в секундах)")]
    public float audioDelay = 0f;
    
    [Tooltip("Громкость аудио (0-1)")]
    [Range(0f, 1f)]
    public float audioVolume = 1f;
    
    [Tooltip("Показывать сообщения в консоли")]
    public bool debugMessages = true;
    
    // Флаг для отслеживания, был ли активирован триггер
    private bool wasActivated = false;
    
    // Компонент AudioSource
    private AudioSource audioSource;
    
    private void Start()
    {
        // Инициализируем AudioSource если есть аудиофайл
        if (activationAudio != null)
        {
            SetupAudioSource();
        }
    }
    
    /// <summary>
    /// Настройка AudioSource для воспроизведения аудио
    /// </summary>
    private void SetupAudioSource()
    {
        // Получаем или создаем AudioSource
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        // Настраиваем AudioSource
        audioSource.clip = activationAudio;
        audioSource.volume = audioVolume;
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        
        if (debugMessages)
        {
            Debug.Log($"[SimpleGopnikTrigger] 🔊 AudioSource настроен для {gameObject.name}");
            Debug.Log($"[SimpleGopnikTrigger] 📊 Аудиофайл: {activationAudio.name}");
            Debug.Log($"[SimpleGopnikTrigger] ⏱️ Задержка аудио: {audioDelay} сек");
            Debug.Log($"[SimpleGopnikTrigger] 🔊 Громкость: {audioVolume}");
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        // Проверяем, что объект с правильным тегом вошел в триггер и триггер ещё не был активирован
        if (other.CompareTag(targetTag) && !wasActivated)
        {
            if (debugMessages)
            {
                Debug.Log($"[SimpleGopnikTrigger] Игрок вошел в триггер {gameObject.name}");
            }
            
            // Активируем первый объект сразу
            if (objectToActivate != null)
            {
                if (debugMessages)
                {
                    Debug.Log($"[SimpleGopnikTrigger] Активирую {objectToActivate.name}");
                }
                objectToActivate.SetActive(true);
            }
            
            // Воспроизводим аудио с задержкой
            if (activationAudio != null && audioSource != null)
            {
                if (debugMessages)
                {
                    Debug.Log($"[SimpleGopnikTrigger] Запланировано воспроизведение аудио через {audioDelay} секунд");
                }
                StartCoroutine(PlayAudioWithDelay());
            }
            
            // Телепортируем игрока с задержкой
            if (teleportPlayer)
            {
                if (debugMessages)
                {
                    Debug.Log($"[SimpleGopnikTrigger] Запланирована телепортация игрока через {playerTeleportDelay} секунд");
                }
                StartCoroutine(TeleportPlayerWithDelay());
                
                // Резервная телепортация через Invoke
                Invoke(nameof(TeleportPlayerDirectly), playerTeleportDelay + 0.1f);
            }
            
            // Новый вариант: если указан объект из сцены — активируем его с задержкой
            if (delayedObjectToActivate != null)
            {
                if (debugMessages)
                {
                    Debug.Log($"[SimpleGopnikTrigger] Запланирована активация {delayedObjectToActivate.name} через {activationDelay} секунд");
                }
                StartCoroutine(ActivateWithDelay(delayedObjectToActivate, activationDelay));
            }
            
            // Если указан префаб — инстанцируем его с задержкой
            if (delayedPrefabToInstantiate != null)
            {
                if (debugMessages)
                {
                    Debug.Log($"[SimpleGopnikTrigger] Запланировано создание {delayedPrefabToInstantiate.name} через {activationDelay} секунд");
                }
                StartCoroutine(InstantiateWithDelay(delayedPrefabToInstantiate, activationDelay));
            }
            
            // Устанавливаем флаг, что триггер был активирован
            wasActivated = true;
            
            // Отключаем коллайдер, чтобы избежать повторных срабатываний
            Collider triggerCollider = GetComponent<Collider>();
            if (triggerCollider != null)
            {
                triggerCollider.enabled = false;
            }
            
            // Отключаем этот триггер с задержкой, если нужно
            if (disableSelfAfterActivation)
            {
                // Используем максимальную задержку из всех операций
                float maxDelay = Mathf.Max(audioDelay, playerTeleportDelay, activationDelay);
                Invoke(nameof(DisableObject), maxDelay + 1f);
            }
        }
    }
    
    /// <summary>
    /// Воспроизведение аудио с задержкой
    /// </summary>
    private IEnumerator PlayAudioWithDelay()
    {
        yield return new WaitForSeconds(audioDelay);
        
        if (audioSource != null && activationAudio != null)
        {
            if (debugMessages)
            {
                Debug.Log($"[SimpleGopnikTrigger] 🔊 Воспроизводится аудио: {activationAudio.name}");
            }
            
            audioSource.Play();
        }
        else
        {
            if (debugMessages)
            {
                Debug.LogError("[SimpleGopnikTrigger] ❌ AudioSource или аудиофайл не найден!");
            }
        }
    }
    
    /// <summary>
    /// Телепортация игрока с задержкой
    /// </summary>
    private IEnumerator TeleportPlayerWithDelay()
    {
        if (debugMessages)
        {
            Debug.Log($"[SimpleGopnikTrigger] ⏱️ Начинаю ожидание {playerTeleportDelay} секунд для телепортации...");
        }
        
        yield return new WaitForSeconds(playerTeleportDelay);
        
        if (debugMessages)
        {
            Debug.Log($"[SimpleGopnikTrigger] ⏱️ Задержка завершена, проверяю игрока...");
        }
        
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        
        if (player != null)
        {
            if (debugMessages)
            {
                Debug.Log($"[SimpleGopnikTrigger] 🚀 Телепортирую игрока в позицию: {playerTeleportPosition}");
                Debug.Log($"[SimpleGopnikTrigger] 📍 Текущая позиция: {player.transform.position}");
                Debug.Log($"[SimpleGopnikTrigger] ⚡ Игрок активен: {player.activeInHierarchy}");
            }
            
            // Проверяем, что игрок активен
            if (player.activeInHierarchy)
            {
                // Телепортируем игрока
                player.transform.position = playerTeleportPosition;
                
                if (debugMessages)
                {
                    Debug.Log($"[SimpleGopnikTrigger] ✅ Игрок телепортирован в позицию: {player.transform.position}");
                }
            }
            else
            {
                if (debugMessages)
                {
                    Debug.LogWarning("[SimpleGopnikTrigger] ⚠️ Игрок неактивен, жду активации...");
                }
                
                // Ждем активации игрока
                yield return new WaitUntil(() => player.activeInHierarchy);
                
                if (debugMessages)
                {
                    Debug.Log("[SimpleGopnikTrigger] ✅ Игрок активирован, телепортирую...");
                }
                
                // Телепортируем игрока
                player.transform.position = playerTeleportPosition;
                
                if (debugMessages)
                {
                    Debug.Log($"[SimpleGopnikTrigger] ✅ Игрок телепортирован в позицию: {player.transform.position}");
                }
            }
        }
        else
        {
            if (debugMessages)
            {
                Debug.LogError("[SimpleGopnikTrigger] ❌ Игрок не найден для телепортации!");
            }
        }
    }
    
    /// <summary>
    /// Прямая телепортация игрока (резервный метод)
    /// </summary>
    private void TeleportPlayerDirectly()
    {
        if (debugMessages)
        {
            Debug.Log($"[SimpleGopnikTrigger] 🔄 Резервная телепортация игрока...");
        }
        
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        
        if (player != null && player.activeInHierarchy)
        {
            if (debugMessages)
            {
                Debug.Log($"[SimpleGopnikTrigger] 🚀 Резервная телепортация в позицию: {playerTeleportPosition}");
            }
            
            player.transform.position = playerTeleportPosition;
            
            if (debugMessages)
            {
                Debug.Log($"[SimpleGopnikTrigger] ✅ Резервная телепортация завершена: {player.transform.position}");
            }
        }
        else
        {
            if (debugMessages)
            {
                Debug.LogError("[SimpleGopnikTrigger] ❌ Резервная телепортация не удалась - игрок не найден или неактивен!");
            }
        }
    }
    
    private IEnumerator ActivateWithDelay(GameObject objectToActivate, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (objectToActivate != null)
        {
            if (debugMessages)
            {
                Debug.Log($"[SimpleGopnikTrigger] Активирую {objectToActivate.name} после задержки");
            }
            objectToActivate.SetActive(true);
        }
    }
    
    private IEnumerator InstantiateWithDelay(GameObject prefab, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (prefab != null)
        {
            Vector3 spawnPos = transform.position + spawnOffset;
            GameObject obj = Instantiate(prefab, spawnPos, Quaternion.identity);
            if (debugMessages)
            {
                Debug.Log($"[SimpleGopnikTrigger] Инстанцирован объект {obj.name} после задержки");
            }
        }
        else
        {
            if (debugMessages)
            {
                Debug.LogError("[SimpleGopnikTrigger] Префаб для инстанцирования не назначен!");
            }
        }
    }
    
    /// <summary>
    /// Тестирование аудио (контекстное меню)
    /// </summary>
    [ContextMenu("Тестировать аудио")]
    public void TestAudio()
    {
        if (activationAudio != null && audioSource != null)
        {
            if (debugMessages)
            {
                Debug.Log($"[SimpleGopnikTrigger] 🧪 Тестирование аудио: {activationAudio.name}");
            }
            audioSource.Play();
        }
        else
        {
            if (debugMessages)
            {
                Debug.LogError("[SimpleGopnikTrigger] ❌ Аудиофайл или AudioSource не настроен!");
            }
        }
    }
    
    /// <summary>
    /// Остановить аудио (контекстное меню)
    /// </summary>
    [ContextMenu("Остановить аудио")]
    public void StopAudio()
    {
        if (audioSource != null)
        {
            audioSource.Stop();
            if (debugMessages)
            {
                Debug.Log($"[SimpleGopnikTrigger] ⏹️ Аудио остановлено");
            }
        }
    }
    
    /// <summary>
    /// Принудительно телепортировать игрока (контекстное меню)
    /// </summary>
    [ContextMenu("Принудительно телепортировать игрока")]
    public void ForceTeleportPlayer()
    {
        if (teleportPlayer)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            
            if (player != null)
            {
                if (debugMessages)
                {
                    Debug.Log($"[SimpleGopnikTrigger] 🚀 Принудительная телепортация игрока в позицию: {playerTeleportPosition}");
                }
                
                player.transform.position = playerTeleportPosition;
                
                if (debugMessages)
                {
                    Debug.Log($"[SimpleGopnikTrigger] ✅ Игрок телепортирован в позицию: {player.transform.position}");
                }
            }
            else
            {
                if (debugMessages)
                {
                    Debug.LogError("[SimpleGopnikTrigger] ❌ Игрок не найден!");
                }
            }
        }
        else
        {
            if (debugMessages)
            {
                Debug.LogWarning("[SimpleGopnikTrigger] ⚠️ Телепортация игрока отключена!");
            }
        }
    }
    
    /// <summary>
    /// Телепортировать игрока сейчас (контекстное меню)
    /// </summary>
    [ContextMenu("Телепортировать игрока сейчас")]
    public void TeleportPlayerNow()
    {
        if (debugMessages)
        {
            Debug.Log($"[SimpleGopnikTrigger] 🚀 Принудительная телепортация сейчас...");
        }
        
        TeleportPlayerDirectly();
    }
    
    /// <summary>
    /// Показать информацию о настройках (контекстное меню)
    /// </summary>
    [ContextMenu("Показать информацию")]
    public void ShowInfo()
    {
        if (debugMessages)
        {
            Debug.Log($"[SimpleGopnikTrigger] 📊 Информация о триггере:");
            Debug.Log($"[SimpleGopnikTrigger] 📍 Имя: {gameObject.name}");
            Debug.Log($"[SimpleGopnikTrigger] 🎯 Целевой тег: {targetTag}");
            Debug.Log($"[SimpleGopnikTrigger] ⚡ Активирован: {wasActivated}");
            
            if (activationAudio != null)
            {
                Debug.Log($"[SimpleGopnikTrigger] 🔊 Аудиофайл: {activationAudio.name}");
                Debug.Log($"[SimpleGopnikTrigger] ⏱️ Задержка аудио: {audioDelay} сек");
                Debug.Log($"[SimpleGopnikTrigger] 🔊 Громкость: {audioVolume}");
            }
            else
            {
                Debug.Log($"[SimpleGopnikTrigger] 🔊 Аудиофайл: НЕ НАЗНАЧЕН");
            }
            
            Debug.Log($"[SimpleGopnikTrigger] ⏱️ Задержка активации: {activationDelay} сек");
            Debug.Log($"[SimpleGopnikTrigger] 🔧 Отключить после активации: {disableSelfAfterActivation}");
            
            if (teleportPlayer)
            {
                Debug.Log($"[SimpleGopnikTrigger] 🚀 Телепортация игрока: ВКЛЮЧЕНА");
                Debug.Log($"[SimpleGopnikTrigger] 📍 Целевые координаты: {playerTeleportPosition}");
                Debug.Log($"[SimpleGopnikTrigger] ⏱️ Задержка телепортации: {playerTeleportDelay} сек");
            }
            else
            {
                Debug.Log($"[SimpleGopnikTrigger] 🚀 Телепортация игрока: ОТКЛЮЧЕНА");
            }
        }
    }
    
    /// <summary>
    /// Отключить объект с задержкой
    /// </summary>
    private void DisableObject()
    {
        if (debugMessages)
        {
            Debug.Log($"[SimpleGopnikTrigger] 🔄 Отключаю объект {gameObject.name}");
        }
        gameObject.SetActive(false);
    }
    
    /// <summary>
    /// Отображение в редакторе
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        // Визуализация телепортации игрока
        if (teleportPlayer)
        {
            // Рисуем линию от триггера к целевой позиции
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, playerTeleportPosition);
            
            // Рисуем сферу в целевой позиции
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(playerTeleportPosition, 1f);
            
            // Рисуем куб в целевой позиции
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(playerTeleportPosition, Vector3.one * 2f);
            
            #if UNITY_EDITOR
            // Подпись с координатами
            UnityEditor.Handles.Label(playerTeleportPosition + Vector3.up * 3f, $"Телепорт игрока\n{playerTeleportPosition}");
            #endif
        }
    }
}