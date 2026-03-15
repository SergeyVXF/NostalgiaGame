using UnityEngine;
using System.Collections;

/// <summary>
/// NPC с случайными аудио репликами
/// Проигрывает 1 из 6 аудиофайлов когда игрок проходит рядом
/// </summary>
public class NPCRandomDialogue : MonoBehaviour
{
    [Header("Настройки диалога")]
    [Tooltip("Дистанция активации диалога")]
    public float activationDistance = 3f;
    
    [Tooltip("Задержка между репликами (секунды)")]
    public float dialogueCooldown = 5f;
    
    [Header("Настройки звука")]
    [Tooltip("Массив аудиоклипов реплик (максимум 6)")]
    public AudioClip[] dialogueAudioClips = new AudioClip[6];
    
    [Tooltip("Громкость реплик")]
    [Range(0f, 10f)]
    public float volume = 1.6f;
    
    [Header("Настройки отладки")]
    [Tooltip("Показывать сообщения в консоли")]
    public bool debugMessages = true;
    
    private GameObject player;
    private AudioSource audioSource;
    private bool canPlayDialogue = true;
    private bool isDialogueActive = false;
    private Coroutine dialogueCoroutine;
    
    void Start()
    {
        // Ищем игрока
        player = GameObject.FindGameObjectWithTag("Player");
        
        // Создаем и настраиваем AudioSource
        SetupAudioSource();
        
        if (debugMessages)
        {
            Debug.Log($"[NPCRandomDialogue] ✅ NPC готов: {gameObject.name}");
            Debug.Log($"[NPCRandomDialogue] 🔊 Аудиофайлов: {GetValidAudioCount()}");
        }
    }
    
    void Update()
    {
        // Проверяем дистанцию до игрока
        CheckPlayerDistance();
    }
    
    void SetupAudioSource()
    {
        // Автоматически создаем AudioSource
        audioSource = gameObject.AddComponent<AudioSource>();
        
        // Настраиваем AudioSource
        audioSource.volume = volume;
        audioSource.spatialBlend = 1f; // 3D звук
        audioSource.maxDistance = activationDistance * 2f;
        audioSource.rolloffMode = AudioRolloffMode.Linear;
        
        if (debugMessages)
        {
            Debug.Log($"[NPCRandomDialogue] 🔊 AudioSource создан автоматически");
        }
    }
    

    
    void CheckPlayerDistance()
    {
        if (player == null || !canPlayDialogue) return;
        
        float distance = Vector3.Distance(transform.position, player.transform.position);
        
        if (distance <= activationDistance && !isDialogueActive)
        {
            PlayRandomDialogue();
        }
    }
    
    void PlayRandomDialogue()
    {
        if (!canPlayDialogue || isDialogueActive) return;
        
        // Выбираем случайный аудиофайл
        int randomIndex = GetRandomAudioIndex();
        
        if (randomIndex >= 0)
        {
            if (debugMessages)
            {
                Debug.Log($"[NPCRandomDialogue] 🔊 Проигрываю аудио {randomIndex + 1}: {dialogueAudioClips[randomIndex].name}");
            }
            
            // Запускаем корутину диалога
            dialogueCoroutine = StartCoroutine(PlayDialogueCoroutine(randomIndex));
        }
    }
    
    int GetRandomAudioIndex()
    {
        int validCount = GetValidAudioCount();
        
        if (validCount == 0)
        {
            if (debugMessages)
                Debug.LogWarning("[NPCRandomDialogue] ⚠️ Нет доступных аудиофайлов!");
            return -1;
        }
        
        return Random.Range(0, validCount);
    }
    
    int GetValidAudioCount()
    {
        int count = 0;
        for (int i = 0; i < dialogueAudioClips.Length; i++)
        {
            if (dialogueAudioClips[i] != null)
            {
                count++;
            }
        }
        return count;
    }
    
    IEnumerator PlayDialogueCoroutine(int audioIndex)
    {
        isDialogueActive = true;
        canPlayDialogue = false;
        
        AudioClip audioClip = dialogueAudioClips[audioIndex];
        
        // Проигрываем звук
        if (audioClip != null && audioSource != null)
        {
            audioSource.clip = audioClip;
            audioSource.Play();
            
            if (debugMessages)
            {
                Debug.Log($"[NPCRandomDialogue] 🔊 Проигрываю аудио: {audioClip.name}");
            }
        }
        
        // Ждем пока звук закончится
        yield return new WaitForSeconds(audioClip.length);
        
        isDialogueActive = false;
        
        // Ждем кулдаун
        yield return new WaitForSeconds(dialogueCooldown);
        
        canPlayDialogue = true;
        
        if (debugMessages)
        {
            Debug.Log("[NPCRandomDialogue] ✅ Аудио завершено, готов к следующему");
        }
    }
    
    /// <summary>
    /// Принудительно проиграть случайное аудио
    /// </summary>
    [ContextMenu("Проиграть случайное аудио")]
    public void ForcePlayRandomDialogue()
    {
        if (canPlayDialogue && !isDialogueActive)
        {
            PlayRandomDialogue();
        }
    }
    
    /// <summary>
    /// Остановить текущий диалог
    /// </summary>
    [ContextMenu("Остановить диалог")]
    public void StopDialogue()
    {
        if (dialogueCoroutine != null)
        {
            StopCoroutine(dialogueCoroutine);
        }
        
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
        }
        
        isDialogueActive = false;
        canPlayDialogue = true;
        
        if (debugMessages)
        {
            Debug.Log("[NPCRandomDialogue] ⏹️ Аудио остановлено");
        }
    }
    
    /// <summary>
    /// Добавить аудиофайл в массив
    /// </summary>
    [ContextMenu("Добавить аудиофайл")]
    public void AddAudioClip()
    {
        // Расширяем массив
        AudioClip[] newClips = new AudioClip[dialogueAudioClips.Length + 1];
        
        // Копируем старые данные
        for (int i = 0; i < dialogueAudioClips.Length; i++)
        {
            newClips[i] = dialogueAudioClips[i];
        }
        
        // Добавляем новую пустую ячейку
        newClips[dialogueAudioClips.Length] = null;
        
        dialogueAudioClips = newClips;
        
        if (debugMessages)
        {
            Debug.Log($"[NPCRandomDialogue] ➕ Добавлен новый аудиофайл. Всего: {dialogueAudioClips.Length}");
        }
    }
    
    /// <summary>
    /// Показать информацию о диалоге
    /// </summary>
    [ContextMenu("Показать информацию")]
    public void ShowDialogueInfo()
    {
        if (debugMessages)
        {
            Debug.Log($"[NPCRandomDialogue] 📊 Информация о диалоге:");
            Debug.Log($"[NPCRandomDialogue] 📍 NPC: {gameObject.name}");
            Debug.Log($"[NPCRandomDialogue] 📏 Дистанция активации: {activationDistance}");
            Debug.Log($"[NPCRandomDialogue] ⏱️ Кулдаун: {dialogueCooldown}с");
            Debug.Log($"[NPCRandomDialogue] 🔊 Всего аудиофайлов: {dialogueAudioClips.Length}");
            Debug.Log($"[NPCRandomDialogue] ✅ Валидных аудиофайлов: {GetValidAudioCount()}");
            
            for (int i = 0; i < dialogueAudioClips.Length; i++)
            {
                string status = dialogueAudioClips[i] != null ? "✅" : "❌";
                string audioName = dialogueAudioClips[i] != null ? dialogueAudioClips[i].name : "Пусто";
                Debug.Log($"[NPCRandomDialogue] {status} Аудио {i + 1}: {audioName}");
            }
        }
    }
    
    void OnDrawGizmosSelected()
    {
        // Показываем зону активации в редакторе
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, activationDistance);
    }
}
