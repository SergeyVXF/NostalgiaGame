using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Очищает дублирующиеся SceneTransitionTracker
/// </summary>
public class SceneTransitionTrackerCleaner : MonoBehaviour
{
    [Header("Очистка")]
    [Tooltip("Показать сообщения в консоли")]
    public bool debugMessages = true;
    
    [Tooltip("Автоматически очищать при старте")]
    public bool autoCleanOnStart = true;
    
    private void Start()
    {
        if (autoCleanOnStart)
        {
            CleanDuplicateTrackers();
        }
    }
    
    /// <summary>
    /// Очистить дублирующиеся трекеры
    /// </summary>
    [ContextMenu("Очистить дублирующиеся трекеры")]
    public void CleanDuplicateTrackers()
    {
        if (debugMessages)
        {
            Debug.Log($"[SceneTransitionTrackerCleaner] 🧹 Начинаем очистку дублирующихся трекеров");
        }
        
        // Находим все трекеры
        SceneTransitionTracker[] allTrackers = FindObjectsOfType<SceneTransitionTracker>();
        
        if (debugMessages)
        {
            Debug.Log($"[SceneTransitionTrackerCleaner] 📊 Найдено трекеров: {allTrackers.Length}");
        }
        
        if (allTrackers.Length <= 1)
        {
            if (debugMessages)
            {
                Debug.Log($"[SceneTransitionTrackerCleaner] ✅ Дублирующихся трекеров нет");
            }
            return;
        }
        
        // Оставляем только первый трекер, остальные удаляем
        SceneTransitionTracker mainTracker = allTrackers[0];
        
        if (debugMessages)
        {
            Debug.Log($"[SceneTransitionTrackerCleaner] 🎯 Главный трекер: {mainTracker.name}");
        }
        
        for (int i = 1; i < allTrackers.Length; i++)
        {
            SceneTransitionTracker duplicateTracker = allTrackers[i];
            
            if (debugMessages)
            {
                Debug.Log($"[SceneTransitionTrackerCleaner] 🗑️ Удаляем дубликат: {duplicateTracker.name}");
            }
            
            // Уничтожаем дубликат
            DestroyImmediate(duplicateTracker.gameObject);
        }
        
        if (debugMessages)
        {
            Debug.Log($"[SceneTransitionTrackerCleaner] ✅ Очистка завершена. Остался 1 трекер: {mainTracker.name}");
        }
    }
    
    /// <summary>
    /// Показать информацию о всех трекерах
    /// </summary>
    [ContextMenu("Показать информацию о трекерах")]
    public void ShowTrackersInfo()
    {
        SceneTransitionTracker[] allTrackers = FindObjectsOfType<SceneTransitionTracker>();
        
        if (debugMessages)
        {
            Debug.Log($"[SceneTransitionTrackerCleaner] 📊 Информация о трекерах:");
            Debug.Log($"[SceneTransitionTrackerCleaner] 📍 Всего трекеров: {allTrackers.Length}");
            
            for (int i = 0; i < allTrackers.Length; i++)
            {
                var tracker = allTrackers[i];
                Debug.Log($"[SceneTransitionTrackerCleaner] {i + 1}. {tracker.name} в сцене: {tracker.gameObject.scene.name}");
                Debug.Log($"[SceneTransitionTrackerCleaner]    DontDestroyOnLoad: {tracker.dontDestroyOnLoad}");
                Debug.Log($"[SceneTransitionTrackerCleaner]    Автоотслеживание: {tracker.autoTrackScenes}");
            }
        }
    }
    
    /// <summary>
    /// Принудительно сохранить текущую сцену
    /// </summary>
    [ContextMenu("Принудительно сохранить текущую сцену")]
    public void ForceSaveCurrentScene()
    {
        PlayerPrefs.SetString("PreviousScene", SceneManager.GetActiveScene().name);
        PlayerPrefs.Save();
        
        if (debugMessages)
        {
            Debug.Log($"[SceneTransitionTrackerCleaner] 💾 Принудительно сохранена сцена: {SceneManager.GetActiveScene().name}");
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
            Debug.Log($"[SceneTransitionTrackerCleaner] 🗑️ Информация о предыдущей сцене очищена");
        }
    }
}
