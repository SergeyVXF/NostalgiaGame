using UnityEngine;

public class WallRunMusicTrigger : MonoBehaviour
{
    [Header("Настройки аудио")]
    [SerializeField] private AudioClip musicClip;
    [SerializeField] private float volume = 1f;
    [SerializeField] private bool playOnce = true;
    
    [Header("Настройки триггера")]
    [SerializeField] private float triggerHeight = 5f; // Высота для запуска музыки
    [SerializeField] private bool showDebugSphere = true; // Показывать сферу в редакторе
    
    private AudioSource audioSource;
    private bool hasPlayed = false;
    private Transform playerTransform;
    
    private void Awake()
    {
        // Создаем и настраиваем AudioSource заранее
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.clip = musicClip;
        audioSource.volume = volume;
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        
        // Предварительно загружаем аудио
        if (musicClip != null)
        {
            // Устанавливаем приоритет загрузки
            audioSource.priority = 0;
            // Устанавливаем пространственное смешивание
            audioSource.spatialBlend = 0f;
            // Отключаем эффекты
            audioSource.reverbZoneMix = 0f;
            audioSource.dopplerLevel = 0f;
        }
    }
    
    private void Start()
    {
        // Находим игрока
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
        }
        else
        {
            Debug.LogError("Игрок не найден! Убедитесь, что у игрока установлен тег 'Player'");
        }
    }
    
    private void Update()
    {
        if (playerTransform != null && !hasPlayed)
        {
            if (playerTransform.position.y >= transform.position.y + triggerHeight)
            {
                PlayMusic();
                hasPlayed = true;
            }
        }
    }
    
    private void PlayMusic()
    {
        if (audioSource != null && musicClip != null)
        {
            // Используем PlayOneShot для более плавного воспроизведения
            audioSource.PlayOneShot(musicClip, volume);
        }
        else
        {
            Debug.LogWarning("AudioSource или MusicClip не назначены!");
        }
    }
    
    private void OnDrawGizmos()
    {
        if (!showDebugSphere) return;
        
        // Рисуем сферу для визуализации триггерной зоны
        Gizmos.color = new Color(1f, 1f, 1f, 1f);
        Gizmos.DrawSphere(transform.position + Vector3.up * triggerHeight, 0.5f);
        
        // Рисуем линию от земли до точки триггера
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * triggerHeight);
        
        // Рисуем горизонтальную плоскость на высоте триггера
        Gizmos.color = Color.green;
        Vector3 triggerPoint = transform.position + Vector3.up * triggerHeight;
        float planeSize = 2f;
        Gizmos.DrawLine(triggerPoint + Vector3.left * planeSize, triggerPoint + Vector3.right * planeSize);
        Gizmos.DrawLine(triggerPoint + Vector3.forward * planeSize, triggerPoint + Vector3.back * planeSize);
    }
} 