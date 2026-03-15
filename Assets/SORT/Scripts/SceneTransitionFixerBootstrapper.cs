using UnityEngine;

// Этот скрипт автоматически создает SceneTransitionFixer при старте игры,
// если его еще нет в сцене. Добавьте его в пустой GameObject в первой сцене.
public class SceneTransitionFixerBootstrapper : MonoBehaviour
{
    [Tooltip("Тег игрока")]
    public string playerTag = "Player";
    
    [Tooltip("Включить отладочные сообщения")]
    public bool enableDebug = true;
    
    // Один экземпляр на всю игру
    private static bool instanceExists = false;
    
    private void Awake()
    {
        // Если экземпляр уже существует - уничтожаем этот
        if (instanceExists)
        {
            Destroy(gameObject);
            return;
        }
        
        // Отмечаем, что экземпляр существует
        instanceExists = true;
        
        // Не уничтожаем при переходе между сценами
        DontDestroyOnLoad(gameObject);
        
        // Проверяем, существует ли уже SceneTransitionFixer
        SceneTransitionFixer[] existingFixers = FindObjectsOfType<SceneTransitionFixer>();
        
        if (existingFixers.Length == 0)
        {
            // Если не существует, создаем новый
            GameObject fixerObj = new GameObject("SceneTransitionFixer");
            SceneTransitionFixer fixer = fixerObj.AddComponent<SceneTransitionFixer>();
            fixer.playerTag = playerTag;
            fixer.enableDebug = enableDebug;
            
            // Не уничтожаем между сценами
            DontDestroyOnLoad(fixerObj);
            
            Debug.Log("[SceneTransitionFixerBootstrapper] Создан новый SceneTransitionFixer");
        }
        else
        {
            Debug.Log($"[SceneTransitionFixerBootstrapper] Найдено {existingFixers.Length} экземпляров SceneTransitionFixer, новый не создается");
        }
    }
} 