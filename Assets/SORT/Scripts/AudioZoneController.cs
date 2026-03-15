using UnityEngine;
using System.Collections;

public class AudioZoneController : MonoBehaviour
{
    [Header("Аудио файлы")]
    [Tooltip("Аудиофайл, который играет постоянно и тихо")]
    public AudioClip backgroundAudio;
    
    [Tooltip("Аудиофайл, который воспроизводится каждые 5 минут")]
    public AudioClip periodicAudio;
    
    [Header("Настройки воспроизведения")]
    [Tooltip("Громкость фонового аудио (0-1)")]
    [Range(0f, 1f)] public float backgroundVolume = 0.3f;
    
    [Tooltip("Громкость периодического аудио (0-1)")]
    [Range(0f, 1f)] public float periodicVolume = 0.8f;
    
    [Tooltip("Интервал воспроизведения периодического аудио в секундах")]
    [Range(10f, 600f)] public float periodicInterval = 300f; // 5 минут = 300 секунд
    
    [Header("Зона слышимости")]
    [Tooltip("Радиус зоны, в которой слышно аудио")]
    [Range(1f, 50f)] public float audioRadius = 10f;
    
    [Tooltip("Тег игрока для определения близости")]
    public string playerTag = "Player";
    
    [Header("Настройки затухания")]
    [Tooltip("Включить плавное затухание при входе/выходе из зоны")]
    public bool useFadeEffect = true;
    
    [Tooltip("Скорость затухания (секунды)")]
    [Range(0.1f, 3f)] public float fadeSpeed = 1f;
    
    // Компоненты
    private AudioSource backgroundSource;
    private AudioSource periodicSource;
    private Transform playerTransform;
    private bool isPlayerInRange = false;
    private bool isInitialized = false;
    
    // Состояние воспроизведения
    private float nextPeriodicTime = 0f;
    private float currentBackgroundVolume = 0f;
    private float currentPeriodicVolume = 0f;
    
    void Awake()
    {
        InitializeAudioSources();
        isInitialized = true;
        
        Debug.Log($"[AudioZoneController] Аудио зона инициализирована: {gameObject.name}");
        Debug.Log($"[AudioZoneController] Фоновое аудио: {(backgroundAudio != null ? backgroundAudio.name : "НЕ НАЗНАЧЕНО")}");
        Debug.Log($"[AudioZoneController] Периодическое аудио: {(periodicAudio != null ? periodicAudio.name : "НЕ НАЗНАЧЕНО")}");
        Debug.Log($"[AudioZoneController] Периодическое аудио будет воспроизводиться каждые {periodicInterval} секунд");
    }
    
    void Start()
    {
        // Запускаем корутину в Start для правильной инициализации
        StartCoroutine(PeriodicAudioCoroutine());
    }
    
    void Update()
    {
        if (!isInitialized) return;
        
        CheckPlayerDistance();
        UpdateAudioVolumes();
    }
    
    void InitializeAudioSources()
    {
        // Создаем два AudioSource компонента
        backgroundSource = gameObject.AddComponent<AudioSource>();
        periodicSource = gameObject.AddComponent<AudioSource>();
        
        // Настраиваем фоновый источник
        backgroundSource.clip = backgroundAudio;
        backgroundSource.volume = 0f; // Начинаем с тишины
        backgroundSource.loop = true; // Зацикливаем
        backgroundSource.spatialBlend = 1f; // 3D звук
        backgroundSource.rolloffMode = AudioRolloffMode.Linear;
        backgroundSource.maxDistance = audioRadius;
        backgroundSource.minDistance = 1f;
        backgroundSource.dopplerLevel = 0f; // Отключаем эффект Допплера
        
        // Настраиваем периодический источник
        periodicSource.clip = periodicAudio;
        periodicSource.volume = 0f; // Начинаем с тишины
        periodicSource.loop = false; // Не зацикливаем
        periodicSource.spatialBlend = 1f; // 3D звук
        periodicSource.rolloffMode = AudioRolloffMode.Linear;
        periodicSource.maxDistance = audioRadius;
        periodicSource.minDistance = 1f;
        periodicSource.dopplerLevel = 0f; // Отключаем эффект Допплера
        
        // Запускаем фоновое аудио
        backgroundSource.Play();
        
        Debug.Log("[AudioZoneController] AudioSource компоненты созданы и настроены");
    }
    
    void CheckPlayerDistance()
    {
        // Ищем игрока если еще не найден
        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag(playerTag);
            if (player != null)
            {
                playerTransform = player.transform;
                Debug.Log($"[AudioZoneController] Игрок найден: {player.name}");
            }
        }
        
        if (playerTransform == null) return;
        
        // Проверяем расстояние до игрока
        float distance = Vector3.Distance(transform.position, playerTransform.position);
        bool wasInRange = isPlayerInRange;
        isPlayerInRange = distance <= audioRadius;
        
        // Логируем изменение состояния
        if (wasInRange != isPlayerInRange)
        {
            if (isPlayerInRange)
            {
                Debug.Log($"[AudioZoneController] Игрок вошел в зону аудио (расстояние: {distance:F1})");
                // Воспроизводим периодическое аудио сразу при входе в зону
                PlayPeriodicAudio();
            }
            else
            {
                Debug.Log($"[AudioZoneController] Игрок вышел из зоны аудио (расстояние: {distance:F1})");
            }
        }
    }
    
    void UpdateAudioVolumes()
    {
        if (!useFadeEffect)
        {
            // Простое переключение без затухания
            float targetBackgroundVolume = isPlayerInRange ? backgroundVolume : 0f;
            float targetPeriodicVolume = isPlayerInRange ? periodicVolume : 0f;
            
            backgroundSource.volume = targetBackgroundVolume;
            periodicSource.volume = targetPeriodicVolume;
        }
        else
        {
            // Плавное затухание
            float targetBackgroundVolume = isPlayerInRange ? backgroundVolume : 0f;
            float targetPeriodicVolume = isPlayerInRange ? periodicVolume : 0f;
            
            currentBackgroundVolume = Mathf.MoveTowards(currentBackgroundVolume, targetBackgroundVolume, 
                (backgroundVolume / fadeSpeed) * Time.deltaTime);
            currentPeriodicVolume = Mathf.MoveTowards(currentPeriodicVolume, targetPeriodicVolume, 
                (periodicVolume / fadeSpeed) * Time.deltaTime);
            
            backgroundSource.volume = currentBackgroundVolume;
            periodicSource.volume = currentPeriodicVolume;
        }
    }
    
    IEnumerator PeriodicAudioCoroutine()
    {
        // Не воспроизводим сразу при старте - только по таймеру или при входе в зону
        Debug.Log("[AudioZoneController] Корутина периодического аудио запущена - ожидание входа в зону или таймера");
        
        while (true)
        {
            // Ждем до следующего воспроизведения
            yield return new WaitForSeconds(periodicInterval);
            
            // Ждем, пока текущее аудио закончится (если оно играет)
            while (periodicSource.isPlaying)
            {
                yield return new WaitForSeconds(0.1f);
            }
            
            // Воспроизводим периодическое аудио только если игрок в зоне
            if (isPlayerInRange)
            {
                PlayPeriodicAudio();
            }
            else
            {
                Debug.Log($"[AudioZoneController] Таймер сработал, но игрок не в зоне - аудио пропущено (время: {Time.time:F1}с)");
            }
        }
    }
    
    void PlayPeriodicAudio()
    {
        if (periodicAudio == null)
        {
            Debug.LogWarning("[AudioZoneController] Периодическое аудио не назначено!");
            return;
        }
        
        // Проверяем, не играет ли уже периодическое аудио
        if (periodicSource.isPlaying)
        {
            Debug.Log($"[AudioZoneController] Периодическое аудио уже играет, пропускаем воспроизведение (время: {Time.time:F1}с)");
            return;
        }
        
        if (isPlayerInRange)
        {
            periodicSource.Play();
            Debug.Log($"[AudioZoneController] 🎵 Воспроизводится периодическое аудио: {periodicAudio.name} (время: {Time.time:F1}с)");
        }
        else
        {
            Debug.Log($"[AudioZoneController] Игрок не в зоне, периодическое аудио пропущено (время: {Time.time:F1}с)");
        }
    }
    
    // Публичные методы для внешнего управления
    public void SetBackgroundVolume(float volume)
    {
        backgroundVolume = Mathf.Clamp01(volume);
        Debug.Log($"[AudioZoneController] Громкость фонового аудио изменена на: {backgroundVolume}");
    }
    
    public void SetPeriodicVolume(float volume)
    {
        periodicVolume = Mathf.Clamp01(volume);
        Debug.Log($"[AudioZoneController] Громкость периодического аудио изменена на: {periodicVolume}");
    }
    
    public void SetAudioRadius(float radius)
    {
        audioRadius = Mathf.Max(1f, radius);
        
        // Обновляем настройки AudioSource
        if (backgroundSource != null)
        {
            backgroundSource.maxDistance = audioRadius;
        }
        if (periodicSource != null)
        {
            periodicSource.maxDistance = audioRadius;
        }
        
        Debug.Log($"[AudioZoneController] Радиус аудио зоны изменен на: {audioRadius}");
    }
    
    public void ForcePlayPeriodicAudio()
    {
        if (periodicAudio != null)
        {
            // Останавливаем текущее воспроизведение если оно играет
            if (periodicSource.isPlaying)
            {
                periodicSource.Stop();
            }
            
            periodicSource.Play();
            Debug.Log("[AudioZoneController] Принудительное воспроизведение периодического аудио");
        }
    }
    
    public void StopPeriodicAudio()
    {
        if (periodicSource.isPlaying)
        {
            periodicSource.Stop();
            Debug.Log("[AudioZoneController] Периодическое аудио остановлено");
        }
    }
    
    // Визуализация в редакторе
    void OnDrawGizmos()
    {
        // Рисуем зону слышимости
        Gizmos.color = isPlayerInRange ? Color.green : Color.yellow;
        Gizmos.DrawWireSphere(transform.position, audioRadius);
        
        // Рисуем линию к игроку если он в зоне
        if (isPlayerInRange && playerTransform != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, playerTransform.position);
        }
        
        // Рисуем иконку аудио
        Gizmos.color = Color.blue;
        Gizmos.DrawWireCube(transform.position + Vector3.up * 2f, Vector3.one * 0.5f);
    }
    
    void OnDrawGizmosSelected()
    {
        // При выделении объекта показываем дополнительную информацию
        Gizmos.color = new Color(0f, 1f, 1f, 0.3f);
        Gizmos.DrawSphere(transform.position, audioRadius);
    }
} 