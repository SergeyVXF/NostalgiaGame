using UnityEngine;
using System.Collections;

public class MilestoneObjectSpawner : MonoBehaviour
{
    [Header("Объекты для milestone 2 (50%)")]
    [Tooltip("Объект для milestone 2")]
    public GameObject milestone2Object;
    
    [Tooltip("Мировая позиция спавна объекта milestone 2")]
    public Vector3 milestone2Position = Vector3.zero;
    
    [Tooltip("Поворот объекта milestone 2")]
    public Vector3 milestone2Rotation = Vector3.zero;
    
    [Header("Объекты для milestone 3 (75%)")]
    [Tooltip("Объект для milestone 3")]
    public GameObject milestone3Object;
    
    [Tooltip("Мировая позиция спавна объекта milestone 3")]
    public Vector3 milestone3Position = Vector3.zero;
    
    [Tooltip("Поворот объекта milestone 3")]
    public Vector3 milestone3Rotation = Vector3.zero;
    
    [Header("Объекты для milestone 4 (100%)")]
    [Tooltip("Объект для milestone 4")]
    public GameObject milestone4Object;
    
    [Tooltip("Мировая позиция спавна объекта milestone 4")]
    public Vector3 milestone4Position = Vector3.zero;
    
    [Tooltip("Поворот объекта milestone 4")]
    public Vector3 milestone4Rotation = Vector3.zero;
    
    [Header("Объекты на позиции игрока")]
    [Tooltip("Объект для игрока при milestone 2")]
    public GameObject playerMilestone2Object;
    
    [Tooltip("Объект для игрока при milestone 3")]
    public GameObject playerMilestone3Object;
    
    [Tooltip("Объект для игрока при milestone 4")]
    public GameObject playerMilestone4Object;
    
    [Tooltip("Смещение от позиции игрока")]
    public Vector3 playerOffset = Vector3.up * 0.5f;
    
    [Header("Звуковые эффекты")]
    [Tooltip("Звук появления объекта")]
    public AudioClip spawnSound;
    
    [Tooltip("Громкость звука")]
    [Range(0f, 1f)]
    public float soundVolume = 0.8f;
    
    [Header("Визуальные эффекты")]
    [Tooltip("Эффект появления объекта")]
    public ParticleSystem spawnEffect;
    
    [Header("Отладка")]
    [Tooltip("Показывать отладочную информацию")]
    public bool showDebugLog = true;
    
    private bool milestone2Spawned = false;
    private bool milestone3Spawned = false;
    private bool milestone4Spawned = false;
    private bool playerMilestone2Spawned = false;
    private bool playerMilestone3Spawned = false;
    private bool playerMilestone4Spawned = false;
    
    private void Start()
    {
        if (showDebugLog)
            Debug.Log("[MilestoneObjectSpawner] ✅ Инициализирован");
    }
    
    private void Update()
    {
        // Проверяем появление milestone сообщений
        CheckMilestoneMessages();
    }
    
    /// <summary>
    /// Проверяет появление milestone сообщений и спавнит объекты
    /// </summary>
    private void CheckMilestoneMessages()
    {
        // Ищем все активные milestone сообщения
        MilestoneMessageUI[] milestoneMessages = FindObjectsOfType<MilestoneMessageUI>();
        
        foreach (MilestoneMessageUI message in milestoneMessages)
        {
            if (message.gameObject.activeInHierarchy)
            {
                // Определяем какой это milestone по имени или позиции в иерархии
                int milestoneNumber = GetMilestoneNumber(message);
                
                if (showDebugLog)
                    Debug.Log($"[MilestoneObjectSpawner] 🔍 Найден активный milestone: {milestoneNumber}, объект: {message.gameObject.name}");
                
                if (milestoneNumber == 2 && !milestone2Spawned) // Milestone 2 = 50% = спавним milestone 2 object
                {
                    SpawnMilestoneObject(2, milestone2Object, milestone2Position, milestone2Rotation);
                    SpawnPlayerMilestoneObject(2, playerMilestone2Object);
                    milestone2Spawned = true;
                    playerMilestone2Spawned = true;
                }
                else if (milestoneNumber == 3 && !milestone3Spawned) // Milestone 3 = 75% = спавним milestone 3 object
                {
                    SpawnMilestoneObject(3, milestone3Object, milestone3Position, milestone3Rotation);
                    SpawnPlayerMilestoneObject(3, playerMilestone3Object);
                    milestone3Spawned = true;
                    playerMilestone3Spawned = true;
                }
                else if (milestoneNumber == 4 && !milestone4Spawned) // Milestone 4 = 100% = спавним milestone 4 object
                {
                    SpawnMilestoneObject(4, milestone4Object, milestone4Position, milestone4Rotation);
                    SpawnPlayerMilestoneObject(4, playerMilestone4Object);
                    milestone4Spawned = true;
                    playerMilestone4Spawned = true;
                }
            }
        }
    }
    
    /// <summary>
    /// Определяет номер milestone по сообщению
    /// </summary>
    private int GetMilestoneNumber(MilestoneMessageUI message)
    {
        // Попробуем определить по имени объекта
        string objectName = message.gameObject.name.ToLower();
        
        if (showDebugLog)
            Debug.Log($"[MilestoneObjectSpawner] 🔍 Анализирую объект: {message.gameObject.name}");
        
        // Проверяем конкретные паттерны для milestone сообщений
        if (objectName.Contains("25%") || objectName.Contains("milestone_1") || objectName.Contains("milestone1"))
        {
            if (showDebugLog)
                Debug.Log($"[MilestoneObjectSpawner] ✅ Milestone 1 (25%) найден в имени: {objectName}");
            return 1;
        }
        else if (objectName.Contains("50%") || objectName.Contains("milestone_2") || objectName.Contains("milestone2"))
        {
            if (showDebugLog)
                Debug.Log($"[MilestoneObjectSpawner] ✅ Milestone 2 (50%) найден в имени: {objectName}");
            return 2;
        }
        else if (objectName.Contains("75%") || objectName.Contains("milestone_3") || objectName.Contains("milestone3"))
        {
            if (showDebugLog)
                Debug.Log($"[MilestoneObjectSpawner] ✅ Milestone 3 (75%) найден в имени: {objectName}");
            return 3;
        }
        else if (objectName.Contains("100%") || objectName.Contains("milestone_4") || objectName.Contains("milestone4"))
        {
            if (showDebugLog)
                Debug.Log($"[MilestoneObjectSpawner] ✅ Milestone 4 (100%) найден в имени: {objectName}");
            return 4;
        }
        
        // Если не удалось определить по имени, попробуем по позиции в иерархии
        // Предполагаем, что milestone сообщения идут по порядку
        Transform parent = message.transform.parent;
        if (parent != null)
        {
            int childIndex = message.transform.GetSiblingIndex();
            if (childIndex >= 0 && childIndex <= 3) // 0, 1, 2, 3 для milestone 1, 2, 3, 4
            {
                if (showDebugLog)
                    Debug.Log($"[MilestoneObjectSpawner] ✅ Определен по позиции в иерархии: {childIndex + 1}");
                return childIndex + 1;
            }
        }
        
        if (showDebugLog)
            Debug.LogWarning($"[MilestoneObjectSpawner] ⚠️ Не удалось определить номер milestone для: {message.gameObject.name}");
        return 0; // Не удалось определить
    }
    
    /// <summary>
    /// Спавнит объект для указанного milestone
    /// </summary>
    private void SpawnMilestoneObject(int milestoneNumber, GameObject objectToSpawn, Vector3 position, Vector3 rotation)
    {
        if (objectToSpawn == null)
        {
            if (showDebugLog)
                Debug.LogWarning($"[MilestoneObjectSpawner] ⚠️ Объект для milestone {milestoneNumber} не назначен");
            return;
        }
        
        // Создаем объект
        Quaternion spawnRotationQuat = Quaternion.Euler(rotation);
        GameObject spawnedObject = Instantiate(objectToSpawn, position, spawnRotationQuat);
        
        if (showDebugLog)
        {
            Debug.Log($"[MilestoneObjectSpawner] 🎯 Объект для milestone {milestoneNumber} создан:");
            Debug.Log($"  📦 Префаб: {objectToSpawn.name}");
            Debug.Log($"  📍 Мировая позиция: {position}");
            Debug.Log($"  🔄 Поворот: {rotation}");
        }
        
        // Воспроизводим звук
        PlaySpawnSound();
        
        // Воспроизводим эффект
        PlaySpawnEffect(position);
    }
    
    /// <summary>
    /// Спавнит объект на позиции игрока для указанного milestone
    /// </summary>
    private void SpawnPlayerMilestoneObject(int milestoneNumber, GameObject objectToSpawn)
    {
        if (objectToSpawn == null)
        {
            if (showDebugLog)
                Debug.LogWarning($"[MilestoneObjectSpawner] ⚠️ Объект для игрока при milestone {milestoneNumber} не назначен");
            return;
        }
        
        // Находим игрока
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            if (showDebugLog)
                Debug.LogWarning($"[MilestoneObjectSpawner] ⚠️ Игрок не найден для milestone {milestoneNumber}");
            return;
        }
        
        // Создаем объект на позиции игрока со смещением
        Vector3 spawnPosition = player.transform.position + playerOffset;
        GameObject spawnedObject = Instantiate(objectToSpawn, spawnPosition, Quaternion.identity);
        
        if (showDebugLog)
        {
            Debug.Log($"[MilestoneObjectSpawner] 🎯 Объект для игрока при milestone {milestoneNumber} создан:");
            Debug.Log($"  📦 Префаб: {objectToSpawn.name}");
            Debug.Log($"  📍 Позиция игрока: {player.transform.position}");
            Debug.Log($"  📍 Позиция спавна: {spawnPosition}");
        }
        
        // Воспроизводим звук
        PlaySpawnSound();
        
        // Воспроизводим эффект
        PlaySpawnEffect(spawnPosition);
    }
    
    /// <summary>
    /// Воспроизводит звук появления объекта
    /// </summary>
    private void PlaySpawnSound()
    {
        if (spawnSound == null) return;
        
        GameObject audioObject = new GameObject("MilestoneSpawnSound_Temp");
        audioObject.transform.position = transform.position;
        
        AudioSource audioSource = audioObject.AddComponent<AudioSource>();
        audioSource.clip = spawnSound;
        audioSource.volume = soundVolume;
        audioSource.spatialBlend = 1f;
        audioSource.rolloffMode = AudioRolloffMode.Linear;
        audioSource.maxDistance = 30f;
        
        audioSource.Play();
        
        if (showDebugLog)
            Debug.Log($"[MilestoneObjectSpawner] 🔊 Звук появления воспроизведен: {spawnSound.name}");
        
        Destroy(audioObject, spawnSound.length + 0.1f);
    }
    
    /// <summary>
    /// Воспроизводит эффект появления объекта
    /// </summary>
    private void PlaySpawnEffect(Vector3 position)
    {
        if (spawnEffect == null) return;
        
        ParticleSystem effect = Instantiate(spawnEffect, position, Quaternion.identity);
        effect.Play();
        
        if (showDebugLog)
            Debug.Log($"[MilestoneObjectSpawner] ✨ Эффект появления воспроизведен");
        
        Destroy(effect.gameObject, effect.main.duration + 1f);
    }
    
    /// <summary>
    /// Принудительно спавнит объект для указанного milestone (для тестирования)
    /// </summary>
    [ContextMenu("Тест Milestone 2")]
    public void TestMilestone2()
    {
        if (showDebugLog)
            Debug.Log("[MilestoneObjectSpawner] 🧪 Тестирую milestone 2...");
        
        SpawnMilestoneObject(2, milestone2Object, milestone2Position, milestone2Rotation);
    }
    
    [ContextMenu("Тест Milestone 3")]
    public void TestMilestone3()
    {
        if (showDebugLog)
            Debug.Log("[MilestoneObjectSpawner] 🧪 Тестирую milestone 3...");
        
        SpawnMilestoneObject(3, milestone3Object, milestone3Position, milestone3Rotation);
    }
    
    [ContextMenu("Тест Milestone 4")]
    public void TestMilestone4()
    {
        if (showDebugLog)
            Debug.Log("[MilestoneObjectSpawner] 🧪 Тестирую milestone 4...");
        
        SpawnMilestoneObject(4, milestone4Object, milestone4Position, milestone4Rotation);
    }
    
    private void OnDrawGizmos()
    {
        // Рисуем позиции спавна для всех milestone
        if (milestone2Object != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireCube(milestone2Position, Vector3.one);
            #if UNITY_EDITOR
            UnityEditor.Handles.Label(milestone2Position + Vector3.up * 1.5f, "Milestone 2 (50%)");
            #endif
        }
        
        if (milestone3Object != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(milestone3Position, Vector3.one);
            #if UNITY_EDITOR
            UnityEditor.Handles.Label(milestone3Position + Vector3.up * 1.5f, "Milestone 3 (75%)");
            #endif
        }
        
        if (milestone4Object != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(milestone4Position, Vector3.one);
            #if UNITY_EDITOR
            UnityEditor.Handles.Label(milestone4Position + Vector3.up * 1.5f, "Milestone 4 (100%)");
            #endif
        }
    }
}
