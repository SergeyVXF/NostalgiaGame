using UnityEngine;
using Invector;

public class FinalEnemyAI03DeathHandler : MonoBehaviour
{
    [Header("Настройки")]
    [Tooltip("Префаб FinalQuest_FinalCutsceneTrigger для спавна")]
    public GameObject finalCutsceneTriggerPrefab;
    
    [Tooltip("Высота спавна триггера над позицией врага")]
    public float spawnHeight = 0.5f;
    
    [Header("Отладка")]
    [Tooltip("Показывать отладочную информацию в консоли")]
    public bool showDebugLog = true;
    
    private vHealthController healthController;
    private bool isDead = false;
    
    private void Awake()
    {
        if (showDebugLog)
            Debug.Log($"FinalEnemyAI03DeathHandler: Awake вызван на {gameObject.name}");
        
        // Проверяем, что это действительно Final_EnemyAI_03
        if (gameObject.name != "Final_EnemyAI_03")
        {
            Debug.LogWarning($"FinalEnemyAI03DeathHandler: Скрипт должен использоваться только на Final_EnemyAI_03, но находится на {gameObject.name}");
        }
        
        // Получаем компонент здоровья
        healthController = GetComponent<vHealthController>();
        if (healthController == null)
        {
            Debug.LogError($"FinalEnemyAI03DeathHandler: vHealthController не найден на {gameObject.name}");
            return;
        }
        
        // Подписываемся на событие смерти
        healthController.onDead.AddListener(HandleDeath);
        if (showDebugLog)
            Debug.Log($"FinalEnemyAI03DeathHandler: Подписан на событие смерти для {gameObject.name}");
    }
    
    private void Start()
    {
        // Если префаб не назначен, пытаемся найти его
        if (finalCutsceneTriggerPrefab == null)
        {
            LoadFinalCutsceneTriggerPrefab();
        }
    }
    
    private void LoadFinalCutsceneTriggerPrefab()
    {
        // Пытаемся загрузить префаб из Resources или найти в сцене
        GameObject prefabReference = Resources.Load<GameObject>("FinalQuest_FinalCutsceneTrigger");
        if (prefabReference != null)
        {
            finalCutsceneTriggerPrefab = prefabReference;
            if (showDebugLog)
                Debug.Log("FinalEnemyAI03DeathHandler: Префаб FinalQuest_FinalCutsceneTrigger загружен из Resources");
        }
        else
        {
            Debug.LogWarning("FinalEnemyAI03DeathHandler: Префаб FinalQuest_FinalCutsceneTrigger не найден в Resources. Пожалуйста, назначьте его вручную в инспекторе.");
        }
    }
    
    private void HandleDeath(GameObject deadObject)
    {
        if (isDead) return; // Предотвращаем множественные вызовы
        
        isDead = true;
        if (showDebugLog)
            Debug.Log($"FinalEnemyAI03DeathHandler: {gameObject.name} умер! Спавним FinalQuest_FinalCutsceneTrigger на месте смерти врага.");
        
        SpawnFinalCutsceneTriggerAtDeathPosition();
    }
    
    private void SpawnFinalCutsceneTriggerAtDeathPosition()
    {
        // Проверяем наличие префаба
        if (finalCutsceneTriggerPrefab == null)
        {
            Debug.LogError("FinalEnemyAI03DeathHandler: Префаб FinalQuest_FinalCutsceneTrigger не назначен!");
            return;
        }
        
        // Спавним триггер на позиции смерти врага с небольшим подъемом
        Vector3 spawnPosition = transform.position + Vector3.up * spawnHeight;
        Quaternion spawnRotation = transform.rotation;
        
        GameObject spawnedTrigger = Instantiate(finalCutsceneTriggerPrefab, spawnPosition, spawnRotation);
        
        if (showDebugLog)
        {
            Debug.Log($"FinalEnemyAI03DeathHandler: FinalQuest_FinalCutsceneTrigger успешно создан на позиции смерти {gameObject.name}: {spawnPosition}");
            Debug.Log($"FinalEnemyAI03DeathHandler: Триггер '{spawnedTrigger.name}' создан в мире");
        }
        
        // Опционально: можно добавить эффекты или звуки спавна
        PlaySpawnEffects(spawnPosition);
    }
    
    private void PlaySpawnEffects(Vector3 position)
    {
        // Здесь можно добавить визуальные или звуковые эффекты
        // Например, частицы, свет, звук и т.д.
        if (showDebugLog)
            Debug.Log($"FinalEnemyAI03DeathHandler: Эффекты спавна воспроизведены на позиции {position}");
    }
    
    private void OnDestroy()
    {
        // Отписываемся от события смерти при уничтожении объекта
        if (healthController != null)
        {
            healthController.onDead.RemoveListener(HandleDeath);
        }
    }
    
    // Публичный метод для принудительного спавна (для тестирования)
    [ContextMenu("Принудительно спавнить FinalCutsceneTrigger")]
    public void ForceSpawnTrigger()
    {
        if (showDebugLog)
            Debug.Log("FinalEnemyAI03DeathHandler: Принудительный спавн триггера вызван из контекстного меню");
        
        SpawnFinalCutsceneTriggerAtDeathPosition();
    }
}
