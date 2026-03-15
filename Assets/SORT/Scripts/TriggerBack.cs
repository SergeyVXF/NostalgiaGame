using UnityEngine;
using UnityEngine.SceneManagement;

public class TriggerBack : MonoBehaviour
{
    [Tooltip("Имя сцены, на которую нужно вернуть игрока")]
    public string targetSceneName = "AG2";
    
    [Tooltip("Использовать эффект затемнения при переходе между сценами")]
    public bool useSceneFade = true;
    
    [Header("Настройки спавна игрока")]
    [Tooltip("Имя спавн-поинта, на котором должен появиться игрок при возвращении")]
    public string spawnPointName = "DefaultSpawnPoint";
    
    [Tooltip("Если спавн-поинт не найден, использовать эти координаты")]
    public Vector3 fallbackSpawnPosition = Vector3.zero;
    
    [Tooltip("Если спавн-поинт не найден, использовать это вращение")]
    public Vector3 fallbackSpawnRotation = Vector3.zero;
    
    [Header("Дебаг")]
    [Tooltip("Выводить отладочные сообщения в консоль")]
    public bool showDebugMessages = false;
    
    // Сохраняем данные для спавна между сценами
    private static Vector3 savedSpawnPosition = Vector3.zero;
    private static Vector3 savedSpawnRotation = Vector3.zero;
    private static bool hasSpawnData = false;
    private static string savedSpawnPointName = "";
    
    private void OnTriggerEnter(Collider other)
    {
        // Проверяем, является ли вошедший объект игроком
        if (other.CompareTag("Player"))
        {
            if (showDebugMessages)
            {
                Debug.Log("Игрок вошел в триггер возврата. Возвращаемся на сцену: " + targetSceneName);
            }
            
            // Находим точку спавна в сцене
            SpawnPoint spawnPoint = FindSpawnPointByName(spawnPointName);
            
            if (spawnPoint != null)
            {
                // Сохраняем данные о спавн-поинте
                savedSpawnPointName = spawnPointName;
                savedSpawnPosition = spawnPoint.transform.position;
                savedSpawnRotation = spawnPoint.transform.eulerAngles;
                hasSpawnData = true;
                
                if (showDebugMessages)
                {
                    Debug.Log($"Сохранена позиция спавна: {savedSpawnPosition}, вращение: {savedSpawnRotation}");
                }
            }
            else
            {
                // Если точка спавна не найдена, используем запасные значения
                savedSpawnPointName = spawnPointName;
                savedSpawnPosition = fallbackSpawnPosition;
                savedSpawnRotation = fallbackSpawnRotation;
                hasSpawnData = true;
                
                if (showDebugMessages)
                {
                    Debug.LogWarning($"Точка спавна '{spawnPointName}' не найдена. Используем запасные значения.");
                }
            }
            
            // Возвращаемся на сцену
            ReturnToScene();
        }
    }
    
    private SpawnPoint FindSpawnPointByName(string name)
    {
        SpawnPoint[] spawnPoints = FindObjectsOfType<SpawnPoint>();
        foreach (SpawnPoint point in spawnPoints)
        {
            if (point.gameObject.name == name)
            {
                return point;
            }
        }
        return null;
    }
    
    private void ReturnToScene()
    {
        // Проверяем существование сцены
        bool sceneExists = false;
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            string scenePath = SceneUtility.GetScenePathByBuildIndex(i);
            string sceneName = System.IO.Path.GetFileNameWithoutExtension(scenePath);
            if (sceneName == targetSceneName)
            {
                sceneExists = true;
                break;
            }
        }

        if (!sceneExists)
        {
            Debug.LogError("Сцена '" + targetSceneName + "' не найдена в настройках сборки!");
            return;
        }

        // Ищем объект SwordQuestLoaderDirect для загрузки сцены
        SwordQuestLoaderDirect loader = FindObjectOfType<SwordQuestLoaderDirect>();
        
        if (loader != null)
        {
            loader.LoadSceneDirectly(targetSceneName);
            
            if (showDebugMessages)
            {
                Debug.Log("Загружаем сцену через SwordQuestLoaderDirect: " + targetSceneName);
            }
        }
        else
        {
            // Если загрузчик не найден - используем стандартный метод
            SceneManager.LoadScene(targetSceneName);
            
            if (showDebugMessages)
            {
                Debug.Log("Загружаем сцену стандартным способом: " + targetSceneName);
            }
        }
    }
    
    // Метод для проверки сохраненных данных точки спавна
    public static bool HasSavedSpawnData()
    {
        return hasSpawnData;
    }
    
    // Метод для получения имени сохраненной точки спавна
    public static string GetSavedSpawnPointName()
    {
        return savedSpawnPointName;
    }
    
    // Метод для получения сохраненной позиции спавна
    public static Vector3 GetSavedSpawnPosition()
    {
        return savedSpawnPosition;
    }
    
    // Метод для получения сохраненного вращения спавна
    public static Vector3 GetSavedSpawnRotation()
    {
        return savedSpawnRotation;
    }
    
    // Метод для сброса сохраненных данных спавна
    public static void ClearSavedSpawnData()
    {
        hasSpawnData = false;
        savedSpawnPointName = "";
        savedSpawnPosition = Vector3.zero;
        savedSpawnRotation = Vector3.zero;
    }
} 