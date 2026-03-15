using UnityEngine;

public class AudioTrigger : MonoBehaviour
{
    [Header("Настройки аудио")]
    [Tooltip("MP3 файл для воспроизведения")]
    public AudioClip audioClip;
    
    [Tooltip("Громкость воспроизведения (0-1)")]
    [Range(0f, 1f)]
    public float volume = 1f;
    
    [Tooltip("Проигрывать только один раз")]
    public bool playOnce = true;
    
    // Приватные переменные
    private AudioSource audioSource;
    private bool hasPlayed = false;
    
    void Start()
    {
        // Создаем компонент AudioSource если его нет
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        // Настраиваем AudioSource
        audioSource.clip = audioClip;
        audioSource.volume = volume;
        audioSource.playOnAwake = false;
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (!playOnce || !hasPlayed)
            {
                PlayAudio();
            }
        }
    }
    
    private void PlayAudio()
    {
        if (audioSource != null && audioClip != null)
        {
            audioSource.Play();
            hasPlayed = true;
        }
        else
        {
            Debug.LogWarning("AudioTrigger: Отсутствует AudioSource или AudioClip!");
        }
    }
} 