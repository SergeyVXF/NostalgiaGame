using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Скрипт для тестирования системы условного спавна
/// </summary>
public class SpawnPointTester : MonoBehaviour
{
    [Header("Тестирование")]
    [Tooltip("Показать сообщения в консоли")]
    public bool debugMessages = true;
    
    [Tooltip("Автоматически тестировать при старте")]
    public bool autoTestOnStart = true;
    
    private void Start()
    {
        if (autoTestOnStart)
        {
            TestSpawnPointSystem();
        }
    }
    
    /// <summary>
    /// Тестировать систему точек спавна
    /// </summary>
    [ContextMenu("Тестировать систему")]
    public void TestSpawnPointSystem()
    {
        if (debugMessages)
        {
            Debug.Log($"[SpawnPointTester] 🧪 Начинаем тестирование системы точек спавна");
            Debug.Log($"[SpawnPointTester] 📍 Текущая сцена: {SceneManager.GetActiveScene().name}");
        }
        
        // Находим все точки спавна
        SpawnPoint[] spawnPoints = FindObjectsOfType<SpawnPoint>();
        
        if (debugMessages)
        {
            Debug.Log($"[SpawnPointTester] 🔍 Найдено точек спавна: {spawnPoints.Length}");
        }
        
        foreach (SpawnPoint sp in spawnPoints)
        {
            if (debugMessages)
            {
                Debug.Log($"[SpawnPointTester] 📊 Точка спавна: {sp.spawnPointID}");
                Debug.Log($"[SpawnPointTester] ⚡ Активна: {sp.isActive}");
                Debug.Log($"[SpawnPointTester] 🔒 Только из SwordQuest: {sp.onlyFromSwordQuest}");
                Debug.Log($"[SpawnPointTester] 🎯 Ожидаемая сцена: {sp.swordQuestSceneName}");
            }
        }
        
        // Находим PlayerSpawnManager
        PlayerSpawnManager spawnManager = FindObjectOfType<PlayerSpawnManager>();
        if (spawnManager != null)
        {
            if (debugMessages)
            {
                Debug.Log($"[SpawnPointTester] 🎮 PlayerSpawnManager найден");
                Debug.Log($"[SpawnPointTester] 🎯 Целевая сцена: {spawnManager.targetScene}");
                Debug.Log($"[SpawnPointTester] 🔧 Проверка активности: {spawnManager.checkSpawnPointActivity}");
            }
        }
        else
        {
            if (debugMessages)
            {
                Debug.Log($"[SpawnPointTester] ❌ PlayerSpawnManager не найден");
            }
        }
        
        // Проверяем информацию о предыдущей сцене
        string previousScene = PlayerPrefs.GetString("PreviousScene", "");
        if (debugMessages)
        {
            Debug.Log($"[SpawnPointTester] 🔄 Предыдущая сцена: '{previousScene}'");
        }
    }
    
    /// <summary>
    /// Установить предыдущую сцену как SwordQuest для тестирования
    /// </summary>
    [ContextMenu("Установить предыдущую сцену как SwordQuest")]
    public void SetPreviousSceneAsSwordQuest()
    {
        PlayerPrefs.SetString("PreviousScene", "Sword_Quest_Scene");
        PlayerPrefs.Save();
        
        if (debugMessages)
        {
            Debug.Log($"[SpawnPointTester] 🔧 Предыдущая сцена установлена как 'Sword_Quest_Scene'");
        }
    }
    
    /// <summary>
    /// Очистить информацию о предыдущей сцене
    /// </summary>
    [ContextMenu("Очистить информацию о предыдущей сцене")]
    public void ClearPreviousSceneInfo()
    {
        PlayerPrefs.DeleteKey("PreviousScene");
        PlayerPrefs.Save();
        
        if (debugMessages)
        {
            Debug.Log($"[SpawnPointTester] 🗑️ Информация о предыдущей сцене очищена");
        }
    }
    
    /// <summary>
    /// Принудительно активировать все точки спавна
    /// </summary>
    [ContextMenu("Принудительно активировать все точки спавна")]
    public void ForceActivateAllSpawnPoints()
    {
        SpawnPoint[] spawnPoints = FindObjectsOfType<SpawnPoint>();
        
        foreach (SpawnPoint sp in spawnPoints)
        {
            sp.ForceActivate();
        }
        
        if (debugMessages)
        {
            Debug.Log($"[SpawnPointTester] 🔧 Принудительно активированы {spawnPoints.Length} точек спавна");
        }
    }
    
    /// <summary>
    /// Принудительно деактивировать все точки спавна
    /// </summary>
    [ContextMenu("Принудительно деактивировать все точки спавна")]
    public void ForceDeactivateAllSpawnPoints()
    {
        SpawnPoint[] spawnPoints = FindObjectsOfType<SpawnPoint>();
        
        foreach (SpawnPoint sp in spawnPoints)
        {
            sp.ForceDeactivate();
        }
        
        if (debugMessages)
        {
            Debug.Log($"[SpawnPointTester] 🔧 Принудительно деактивированы {spawnPoints.Length} точек спавна");
        }
    }
}
