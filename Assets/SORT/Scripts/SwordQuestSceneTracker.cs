using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Автоматически добавляет SceneTransitionTracker в сцену SwordQuest
/// </summary>
public class SwordQuestSceneTracker : MonoBehaviour
{
    [Header("Настройки")]
    [Tooltip("Показывать сообщения в консоли")]
    public bool debugMessages = true;
    
    [Tooltip("Автоматически создать SceneTransitionTracker")]
    public bool autoCreateTracker = true;
    
    private void Start()
    {
        if (autoCreateTracker)
        {
            CreateSceneTransitionTracker();
        }
    }
    
    /// <summary>
    /// Создать SceneTransitionTracker если его нет
    /// </summary>
    [ContextMenu("Создать SceneTransitionTracker")]
    public void CreateSceneTransitionTracker()
    {
        // Проверяем что мы в сцене SwordQuest
        if (SceneManager.GetActiveScene().name != "Sword_Quest_Scene")
        {
            if (debugMessages)
            {
                Debug.Log($"[SwordQuestSceneTracker] ❌ Не в сцене SwordQuest. Текущая сцена: {SceneManager.GetActiveScene().name}");
            }
            return;
        }
        
        // Ищем ВСЕ существующие SceneTransitionTracker (включая DontDestroyOnLoad)
        SceneTransitionTracker[] allTrackers = FindObjectsOfType<SceneTransitionTracker>();
        
        if (allTrackers.Length > 0)
        {
            if (debugMessages)
            {
                Debug.Log($"[SwordQuestSceneTracker] ✅ SceneTransitionTracker уже существует: {allTrackers[0].name}");
                Debug.Log($"[SwordQuestSceneTracker] 📊 Всего трекеров: {allTrackers.Length}");
                
                foreach (var existingTracker in allTrackers)
                {
                    Debug.Log($"[SwordQuestSceneTracker] 📍 Трекер: {existingTracker.name} в сцене: {existingTracker.gameObject.scene.name}");
                }
            }
            return;
        }
        
        // Создаем новый GameObject с SceneTransitionTracker только если нет ни одного
        GameObject trackerObject = new GameObject("SceneTransitionTracker_SwordQuest");
        SceneTransitionTracker newTracker = trackerObject.AddComponent<SceneTransitionTracker>();
        
        // Настраиваем параметры
        newTracker.autoTrackScenes = true;
        newTracker.debugMessages = true;
        newTracker.dontDestroyOnLoad = true;
        newTracker.saveOnDestroy = true;
        
        if (debugMessages)
        {
            Debug.Log($"[SwordQuestSceneTracker] ✅ Создан SceneTransitionTracker: {trackerObject.name}");
            Debug.Log($"[SwordQuestSceneTracker] 📍 Сцена: {SceneManager.GetActiveScene().name}");
        }
    }
    
    /// <summary>
    /// Принудительно сохранить текущую сцену как предыдущую
    /// </summary>
    [ContextMenu("Принудительно сохранить сцену")]
    public void ForceSaveCurrentScene()
    {
        PlayerPrefs.SetString("PreviousScene", SceneManager.GetActiveScene().name);
        PlayerPrefs.Save();
        
        if (debugMessages)
        {
            Debug.Log($"[SwordQuestSceneTracker] 💾 Принудительно сохранена сцена: {SceneManager.GetActiveScene().name}");
        }
    }
    
    /// <summary>
    /// Показать информацию о текущем состоянии
    /// </summary>
    [ContextMenu("Показать информацию")]
    public void ShowInfo()
    {
        if (debugMessages)
        {
            Debug.Log($"[SwordQuestSceneTracker] 📊 Информация:");
            Debug.Log($"[SwordQuestSceneTracker] 📍 Текущая сцена: {SceneManager.GetActiveScene().name}");
            Debug.Log($"[SwordQuestSceneTracker] 🔄 Предыдущая сцена: {PlayerPrefs.GetString("PreviousScene", "")}");
            
            SceneTransitionTracker tracker = FindObjectOfType<SceneTransitionTracker>();
            if (tracker != null)
            {
                Debug.Log($"[SwordQuestSceneTracker] ✅ SceneTransitionTracker найден: {tracker.name}");
            }
            else
            {
                Debug.Log($"[SwordQuestSceneTracker] ❌ SceneTransitionTracker не найден");
            }
        }
    }
}
