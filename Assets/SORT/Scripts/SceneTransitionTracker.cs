using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Отслеживает переходы между сценами и сохраняет информацию о предыдущей сцене
/// </summary>
public class SceneTransitionTracker : MonoBehaviour
{
    [Header("Настройки")]
    [Tooltip("Автоматически сохранять информацию о сценах")]
    public bool autoTrackScenes = true;
    
    [Tooltip("Показывать сообщения в консоли")]
    public bool debugMessages = true;
    
    [Tooltip("Не уничтожать при переходе между сценами")]
    public bool dontDestroyOnLoad = true;
    
    [Tooltip("Сохранить текущую сцену при уничтожении объекта")]
    public bool saveOnDestroy = true;
    
    private string currentSceneName = "";
    private string previousSceneName = "";
    
    void Awake()
    {
        if (dontDestroyOnLoad)
        {
            DontDestroyOnLoad(gameObject);
        }
        
        // Получаем текущую сцену
        currentSceneName = SceneManager.GetActiveScene().name;
        previousSceneName = PlayerPrefs.GetString("PreviousScene", "");
        
        if (debugMessages)
        {
            Debug.Log($"[SceneTransitionTracker] 🎬 Система отслеживания сцен запущена");
            Debug.Log($"[SceneTransitionTracker] 📍 Текущая сцена: {currentSceneName}");
            Debug.Log($"[SceneTransitionTracker] 🔄 Предыдущая сцена: {previousSceneName}");
        }
    }
    
    void Start()
    {
        if (autoTrackScenes)
        {
            // Сохраняем информацию о текущей сцене как предыдущей для следующего перехода
            SaveCurrentSceneAsPrevious();
        }
    }
    
    void OnDestroy()
    {
        if (saveOnDestroy && !string.IsNullOrEmpty(currentSceneName))
        {
            PlayerPrefs.SetString("PreviousScene", currentSceneName);
            PlayerPrefs.Save();
            
            if (debugMessages)
            {
                Debug.Log($"[SceneTransitionTracker] 💾 Сохранена сцена при уничтожении: {currentSceneName}");
            }
        }
    }
    
    void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus && !string.IsNullOrEmpty(currentSceneName))
        {
            PlayerPrefs.SetString("PreviousScene", currentSceneName);
            PlayerPrefs.Save();
            
            if (debugMessages)
            {
                Debug.Log($"[SceneTransitionTracker] 💾 Сохранена сцена при паузе: {currentSceneName}");
            }
        }
    }
    
    void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus && !string.IsNullOrEmpty(currentSceneName))
        {
            PlayerPrefs.SetString("PreviousScene", currentSceneName);
            PlayerPrefs.Save();
            
            if (debugMessages)
            {
                Debug.Log($"[SceneTransitionTracker] 💾 Сохранена сцена при потере фокуса: {currentSceneName}");
            }
        }
    }
    
    /// <summary>
    /// Сохраняет текущую сцену как предыдущую
    /// </summary>
    public void SaveCurrentSceneAsPrevious()
    {
        if (!string.IsNullOrEmpty(currentSceneName))
        {
            PlayerPrefs.SetString("PreviousScene", currentSceneName);
            PlayerPrefs.Save();
            
            if (debugMessages)
            {
                Debug.Log($"[SceneTransitionTracker] 💾 Сохранена сцена как предыдущая: {currentSceneName}");
            }
        }
    }
    
    /// <summary>
    /// Принудительно сохранить текущую сцену
    /// </summary>
    [ContextMenu("Принудительно сохранить текущую сцену")]
    public void ForceSaveCurrentScene()
    {
        currentSceneName = SceneManager.GetActiveScene().name;
        SaveCurrentSceneAsPrevious();
    }
    
    /// <summary>
    /// Получить название предыдущей сцены
    /// </summary>
    public string GetPreviousSceneName()
    {
        return PlayerPrefs.GetString("PreviousScene", "");
    }
    
    /// <summary>
    /// Получить название текущей сцены
    /// </summary>
    public string GetCurrentSceneName()
    {
        return SceneManager.GetActiveScene().name;
    }
    
    /// <summary>
    /// Проверить что игрок вернулся из определенной сцены
    /// </summary>
    public bool IsReturningFromScene(string sceneName)
    {
        string previous = GetPreviousSceneName();
        bool isReturning = previous == sceneName;
        
        if (debugMessages)
        {
            Debug.Log($"[SceneTransitionTracker] 🔍 Проверка возвращения из '{sceneName}': {isReturning}");
            Debug.Log($"[SceneTransitionTracker] 📊 Предыдущая сцена: '{previous}'");
        }
        
        return isReturning;
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
            Debug.Log($"[SceneTransitionTracker] 🗑️ Информация о предыдущей сцене очищена");
        }
    }
    
    /// <summary>
    /// Показать информацию о сценах
    /// </summary>
    [ContextMenu("Показать информацию о сценах")]
    public void ShowSceneInfo()
    {
        if (debugMessages)
        {
            Debug.Log($"[SceneTransitionTracker] 📊 Информация о сценах:");
            Debug.Log($"[SceneTransitionTracker] 📍 Текущая сцена: {GetCurrentSceneName()}");
            Debug.Log($"[SceneTransitionTracker] 🔄 Предыдущая сцена: {GetPreviousSceneName()}");
            Debug.Log($"[SceneTransitionTracker] ⚡ Автоотслеживание: {autoTrackScenes}");
            Debug.Log($"[SceneTransitionTracker] 🔒 Не уничтожать: {dontDestroyOnLoad}");
            Debug.Log($"[SceneTransitionTracker] 💾 Сохранять при уничтожении: {saveOnDestroy}");
        }
    }
    
    /// <summary>
    /// Установить предыдущую сцену вручную
    /// </summary>
    [ContextMenu("Установить предыдущую сцену как SwordQuest")]
    public void SetPreviousSceneAsSwordQuest()
    {
        PlayerPrefs.SetString("PreviousScene", "Sword_Quest_Scene");
        PlayerPrefs.Save();
        
        if (debugMessages)
        {
            Debug.Log($"[SceneTransitionTracker] 🔧 Предыдущая сцена установлена как 'Sword_Quest_Scene'");
        }
    }
    
    /// <summary>
    /// Проверить возвращение из SwordQuest
    /// </summary>
    [ContextMenu("Проверить возвращение из SwordQuest")]
    public void CheckSwordQuestReturn()
    {
        bool isReturning = IsReturningFromScene("Sword_Quest_Scene");
        
        if (debugMessages)
        {
            if (isReturning)
            {
                Debug.Log($"[SceneTransitionTracker] ✅ Игрок вернулся из SwordQuest!");
            }
            else
            {
                Debug.Log($"[SceneTransitionTracker] ❌ Игрок НЕ возвращался из SwordQuest");
            }
        }
    }
}
