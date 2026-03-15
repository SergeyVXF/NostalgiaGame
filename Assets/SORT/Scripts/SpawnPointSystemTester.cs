using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Комплексный тестер для системы условного спавна
/// </summary>
public class SpawnPointSystemTester : MonoBehaviour
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
            TestCompleteSystem();
        }
    }
    
    /// <summary>
    /// Комплексное тестирование всей системы
    /// </summary>
    [ContextMenu("Тестировать всю систему")]
    public void TestCompleteSystem()
    {
        if (debugMessages)
        {
            Debug.Log($"[SpawnPointSystemTester] 🧪 === КОМПЛЕКСНОЕ ТЕСТИРОВАНИЕ СИСТЕМЫ ===");
            Debug.Log($"[SpawnPointSystemTester] 📍 Текущая сцена: {SceneManager.GetActiveScene().name}");
        }
        
        // 1. Проверяем SceneTransitionTracker
        TestSceneTransitionTracker();
        
        // 2. Проверяем SpawnPoint
        TestSpawnPoints();
        
        // 3. Проверяем PlayerSpawnManager
        TestPlayerSpawnManager();
        
        // 4. Проверяем общую логику
        TestOverallLogic();
        
        if (debugMessages)
        {
            Debug.Log($"[SpawnPointSystemTester] 🧪 === ТЕСТИРОВАНИЕ ЗАВЕРШЕНО ===");
        }
    }
    
    void TestSceneTransitionTracker()
    {
        if (debugMessages)
        {
            Debug.Log($"[SpawnPointSystemTester] 🔍 1. Тестирование SceneTransitionTracker:");
        }
        
        SceneTransitionTracker[] allTrackers = FindObjectsOfType<SceneTransitionTracker>();
        
        if (debugMessages)
        {
            Debug.Log($"[SpawnPointSystemTester] 📊 Найдено трекеров: {allTrackers.Length}");
        }
        
        if (allTrackers.Length == 0)
        {
            if (debugMessages)
            {
                Debug.Log($"[SpawnPointSystemTester] ❌ SceneTransitionTracker не найден!");
            }
            return;
        }
        
        if (allTrackers.Length > 1)
        {
            if (debugMessages)
            {
                Debug.Log($"[SpawnPointSystemTester] ⚠️ Обнаружены дублирующиеся трекеры!");
            }
        }
        
        // Показываем информацию о каждом трекере
        for (int i = 0; i < allTrackers.Length; i++)
        {
            var tracker = allTrackers[i];
            if (debugMessages)
            {
                Debug.Log($"[SpawnPointSystemTester] 📍 Трекер {i + 1}: {tracker.name}");
                Debug.Log($"[SpawnPointSystemTester] 📊 Автоотслеживание: {tracker.autoTrackScenes}");
                Debug.Log($"[SpawnPointSystemTester] 🔒 Не уничтожать: {tracker.dontDestroyOnLoad}");
                Debug.Log($"[SpawnPointSystemTester] 💾 Сохранять при уничтожении: {tracker.saveOnDestroy}");
            }
        }
    }
    
    void TestSpawnPoints()
    {
        if (debugMessages)
        {
            Debug.Log($"[SpawnPointSystemTester] 🔍 2. Тестирование SpawnPoint:");
        }
        
        SpawnPoint[] spawnPoints = FindObjectsOfType<SpawnPoint>();
        
        if (debugMessages)
        {
            Debug.Log($"[SpawnPointSystemTester] 📊 Найдено точек спавна: {spawnPoints.Length}");
        }
        
        foreach (SpawnPoint sp in spawnPoints)
        {
            if (debugMessages)
            {
                Debug.Log($"[SpawnPointSystemTester] 📍 Точка: {sp.spawnPointID}");
                Debug.Log($"[SpawnPointSystemTester] ⚡ Активна: {sp.isActive}");
                Debug.Log($"[SpawnPointSystemTester] 🔒 Только из SwordQuest: {sp.onlyFromSwordQuest}");
                Debug.Log($"[SpawnPointSystemTester] 🎯 Ожидаемая сцена: {sp.swordQuestSceneName}");
            }
        }
    }
    
    void TestPlayerSpawnManager()
    {
        if (debugMessages)
        {
            Debug.Log($"[SpawnPointSystemTester] 🔍 3. Тестирование PlayerSpawnManager:");
        }
        
        PlayerSpawnManager spawnManager = FindObjectOfType<PlayerSpawnManager>();
        if (spawnManager != null)
        {
            if (debugMessages)
            {
                Debug.Log($"[SpawnPointSystemTester] ✅ PlayerSpawnManager найден");
                Debug.Log($"[SpawnPointSystemTester] 🎯 Целевая сцена: {spawnManager.targetScene}");
                Debug.Log($"[SpawnPointSystemTester] 🔧 Проверка активности: {spawnManager.checkSpawnPointActivity}");
            }
        }
        else
        {
            if (debugMessages)
            {
                Debug.Log($"[SpawnPointSystemTester] ❌ PlayerSpawnManager не найден");
            }
        }
    }
    
    void TestOverallLogic()
    {
        if (debugMessages)
        {
            Debug.Log($"[SpawnPointSystemTester] 🔍 4. Тестирование общей логики:");
        }
        
        string previousScene = PlayerPrefs.GetString("PreviousScene", "");
        string currentScene = SceneManager.GetActiveScene().name;
        
        if (debugMessages)
        {
            Debug.Log($"[SpawnPointSystemTester] 📍 Текущая сцена: {currentScene}");
            Debug.Log($"[SpawnPointSystemTester] 🔄 Предыдущая сцена: {previousScene}");
        }
        
        // Проверяем логику активации
        if (currentScene == "AG2" && previousScene == "Sword_Quest_Scene")
        {
            if (debugMessages)
            {
                Debug.Log($"[SpawnPointSystemTester] ✅ Логика корректна: возвращение из SwordQuest в AG2");
            }
        }
        else if (currentScene == "AG2" && string.IsNullOrEmpty(previousScene))
        {
            if (debugMessages)
            {
                Debug.Log($"[SpawnPointSystemTester] ⚠️ Логика частично корректна: AG2 без предыдущей сцены");
            }
        }
        else
        {
            if (debugMessages)
            {
                Debug.Log($"[SpawnPointSystemTester] ❓ Неожиданная комбинация сцен");
            }
        }
    }
    
    /// <summary>
    /// Симуляция возвращения из SwordQuest
    /// </summary>
    [ContextMenu("Симулировать возвращение из SwordQuest")]
    public void SimulateReturnFromSwordQuest()
    {
        PlayerPrefs.SetString("PreviousScene", "Sword_Quest_Scene");
        PlayerPrefs.Save();
        
        if (debugMessages)
        {
            Debug.Log($"[SpawnPointSystemTester] 🔧 Симулировано возвращение из SwordQuest");
        }
        
        // Перезапускаем тестирование
        TestCompleteSystem();
    }
    
    /// <summary>
    /// Симуляция первого запуска
    /// </summary>
    [ContextMenu("Симулировать первый запуск")]
    public void SimulateFirstLaunch()
    {
        PlayerPrefs.DeleteKey("PreviousScene");
        PlayerPrefs.Save();
        
        if (debugMessages)
        {
            Debug.Log($"[SpawnPointSystemTester] 🔧 Симулирован первый запуск");
        }
        
        // Перезапускаем тестирование
        TestCompleteSystem();
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
            Debug.Log($"[SpawnPointSystemTester] 🔧 Принудительно активированы {spawnPoints.Length} точек спавна");
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
            Debug.Log($"[SpawnPointSystemTester] 🔧 Принудительно деактивированы {spawnPoints.Length} точек спавна");
        }
    }
}
