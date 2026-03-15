using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

public class SwordQuestLoaderDirect : MonoBehaviour
{
    [Tooltip("Имя сцены для загрузки")]
    [SerializeField] private string sceneToLoad = "Sword_Quest_Scene";
    
    [Tooltip("Используйте это поле, если не добавлена сцена в Build Settings (только в редакторе)")]
    [SerializeField] private string sceneAssetPath = "Assets/ANTIGOP/Sword_Quest_Scene.unity";
    
    [Tooltip("Сохранять игрока при переходе между сценами")]
    [SerializeField] private bool dontDestroyPlayer = true;
    
    [Tooltip("Тег объекта игрока")]
    [SerializeField] private string playerTag = "Player";
    
    [Tooltip("Возможные имена/части имен катсцены для проверки")]
    [SerializeField] private string[] validCutsceneNames = new string[] { 
        "QuestSword_01", 
        "Quest_sword_01",
        "Sword_quest/01",
        "Sword_quest" 
    };
    
    [Tooltip("Включить подробное логирование")]
    [SerializeField] private bool debugMode = true;
    
    [Tooltip("Исправить проблему с черным освещением при переходах")]
    [SerializeField] private bool fixLightingOnLoad = true;
    
    private void Start()
    {
        if (CutsceneManager.Instance != null)
        {
            CutsceneManager.OnCutsceneEnded += OnCutsceneEnded;
            Debug.Log("[SwordQuestLoaderDirect] Скрипт инициализирован и готов к загрузке сцены");
            
            if (debugMode)
            {
                LogBuildSettingsInfo();
            }
        }
        else
        {
            Debug.LogError("[SwordQuestLoaderDirect] CutsceneManager не найден!");
        }
    }
    
    // Этот метод будет вызван после загрузки сцены
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == sceneToLoad && fixLightingOnLoad)
        {
            StartCoroutine(FixSceneLighting());
        }
    }
    
    // Корутина для исправления освещения
    private IEnumerator FixSceneLighting()
    {
        Debug.Log("[SwordQuestLoaderDirect] Начинаем исправление освещения в сцене");
        
        // Ждем один кадр, чтобы сцена полностью загрузилась
        yield return null;
        
        // Перезагружаем освещение сцены
        RenderSettings.ambientMode = RenderSettings.ambientMode;
        RenderSettings.ambientIntensity += 0.001f;
        RenderSettings.ambientIntensity -= 0.001f;
        
        // Обновляем все источники света в сцене
        Light[] lights = GameObject.FindObjectsOfType<Light>();
        foreach (Light light in lights)
        {
            // Небольшое изменение интенсивности для обновления источника света
            float originalIntensity = light.intensity;
            light.intensity += 0.001f;
            light.intensity = originalIntensity;
            
            if (debugMode)
                Debug.Log($"[SwordQuestLoaderDirect] Обновлен источник света: {light.name}");
        }
        
        // Обновляем материалы на объектах сцены
        Renderer[] renderers = GameObject.FindObjectsOfType<Renderer>();
        foreach (Renderer renderer in renderers)
        {
            if (renderer.materials != null && renderer.materials.Length > 0)
            {
                // Обновляем шейдеры на материалах
                Material[] materials = renderer.materials;
                foreach (Material mat in materials)
                {
                    if (mat != null)
                    {
                        // Небольшое изменение цвета для обновления материала
                        if (mat.HasProperty("_Color"))
                        {
                            Color originalColor = mat.color;
                            mat.color = originalColor * 0.999f;
                            mat.color = originalColor;
                        }
                    }
                }
            }
        }
        
        // Явно запрашиваем обновление освещения
        DynamicGI.UpdateEnvironment();
        
        Debug.Log("[SwordQuestLoaderDirect] Освещение в сцене обновлено");
    }
    
    private void LogBuildSettingsInfo()
    {
        Debug.Log($"[SwordQuestLoaderDirect] Проверка Build Settings: {SceneManager.sceneCountInBuildSettings} сцен найдено");
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i);
            Debug.Log($"[SwordQuestLoaderDirect] Сцена {i}: {path}");
        }
    }
    
    private void OnEnable()
    {
        // Подписываемся на событие загрузки сцены
        SceneManager.sceneLoaded += OnSceneLoaded;
    }
    
    private void OnDisable()
    {
        // Отписываемся от события загрузки сцены
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
    
    private void OnDestroy()
    {
        // Отписываемся от события
        CutsceneManager.OnCutsceneEnded -= OnCutsceneEnded;
        // Отписываемся от события загрузки сцены для предотвращения утечек памяти
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
    
    private void OnCutsceneEnded(GameObject cutscene)
    {
        string cutscenePath = cutscene.transform.GetPath();
        
        if (debugMode)
        {
            Debug.Log($"[SwordQuestLoaderDirect] Завершена катсцена: '{cutscene.name}', путь: '{cutscenePath}'");
        }
        
        // Проверяем по имени катсцены и по пути
        bool isSwordQuestCutscene = cutscene.name == "01" && cutscenePath.Contains("Sword_quest");
        
        if (isSwordQuestCutscene)
        {
            Debug.Log($"[SwordQuestLoaderDirect] Найдено совпадение для катсцены '{cutscene.name}' по пути '{cutscenePath}'");
        }
        else if (debugMode)
        {
            Debug.Log($"[SwordQuestLoaderDirect] Катсцена '{cutscene.name}' с путём '{cutscenePath}' не соответствует искомой");
            return;
        }
        
        Debug.Log("[SwordQuestLoaderDirect] Нужная катсцена завершена, загружаем сцену...");
        LoadScene();
    }
    
    private void LoadScene()
    {
        if (!Application.isPlaying)
        {
            Debug.LogError("[SwordQuestLoaderDirect] LoadScene может быть вызван только в режиме игры!");
            #if UNITY_EDITOR
            Debug.Log("[SwordQuestLoaderDirect] Попробуйте запустить игру или используйте кнопку 'Тест загрузки сцены (Редактор)'");
            #endif
            return;
        }
        
        // Сохраняем игрока при переходе между сценами, если это включено
        if (dontDestroyPlayer)
        {
            GameObject player = GameObject.FindGameObjectWithTag(playerTag);
            if (player != null)
            {
                Debug.Log("[SwordQuestLoaderDirect] Сохраняем игрока между сценами (DontDestroyOnLoad)");
                DontDestroyOnLoad(player);
            }
            else
            {
                Debug.LogWarning($"[SwordQuestLoaderDirect] Игрок с тегом '{playerTag}' не найден для сохранения между сценами");
            }
        }
        
        Debug.Log($"[SwordQuestLoaderDirect] Пытаемся загрузить сцену: {sceneToLoad}");
        
        // Пробуем три разных способа загрузки сцены
        
        // Способ 1: По имени через LoadScene
        try
        {
            Debug.Log($"[SwordQuestLoaderDirect] Способ 1: LoadScene(name)");
            SceneManager.LoadScene(sceneToLoad);
            return;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SwordQuestLoaderDirect] Ошибка способа 1: {e.Message}");
        }
        
        // Способ 2: Поиск индекса в билде
        try
        {
            int buildIndex = -1;
            
            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                string scenePath = SceneUtility.GetScenePathByBuildIndex(i);
                if (System.IO.Path.GetFileNameWithoutExtension(scenePath) == sceneToLoad)
                {
                    buildIndex = i;
                    break;
                }
            }
            
            if (buildIndex >= 0)
            {
                Debug.Log($"[SwordQuestLoaderDirect] Способ 2: LoadScene(buildIndex={buildIndex})");
                SceneManager.LoadScene(buildIndex);
                return;
            }
            else
            {
                Debug.LogError("[SwordQuestLoaderDirect] Способ 2: Не найден индекс сцены в билде");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SwordQuestLoaderDirect] Ошибка способа 2: {e.Message}");
        }
        
        // Способ 3: Через EditorSceneManager (только в редакторе)
        #if UNITY_EDITOR
        try
        {
            Debug.Log($"[SwordQuestLoaderDirect] Способ 3: EditorSceneManager.LoadSceneInPlayMode");
            
            // Сначала проверим, существует ли сцена
            if (System.IO.File.Exists(sceneAssetPath))
            {
                Debug.Log($"[SwordQuestLoaderDirect] Файл сцены найден: {sceneAssetPath}");
                
                EditorSceneManager.LoadSceneInPlayMode(
                    sceneAssetPath,
                    new LoadSceneParameters(LoadSceneMode.Single)
                );
                return;
            }
            else
            {
                Debug.LogError($"[SwordQuestLoaderDirect] Файл сцены не найден: {sceneAssetPath}");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SwordQuestLoaderDirect] Ошибка способа 3: {e.Message}");
        }
        #endif
        
        // Если все способы не сработали, выводим сообщение и помощь
        Debug.LogError("[SwordQuestLoaderDirect] Все способы загрузки сцены не удались!");
        Debug.LogError("[SwordQuestLoaderDirect] Убедитесь, что сцена добавлена в Build Settings (File > Build Settings)");
        Debug.LogError("[SwordQuestLoaderDirect] Проверьте указанный путь к сцене в компоненте SwordQuestLoaderDirect");
    }
    
    // Кнопка для тестирования загрузки сцены
    [ContextMenu("Тест загрузки сцены (Play Mode)")]
    private void TestLoadScene()
    {
        if (!Application.isPlaying)
        {
            Debug.LogError("[SwordQuestLoaderDirect] Этот метод может быть использован только в режиме игры!");
            Debug.LogError("[SwordQuestLoaderDirect] Запустите игру (нажмите Play) и попробуйте снова.");
            return;
        }
        
        Debug.Log("[SwordQuestLoaderDirect] Запущен тест загрузки сцены");
        LoadScene();
    }
    
    #if UNITY_EDITOR
    // Кнопка для открытия сцены в редакторе (без необходимости запуска игры)
    [ContextMenu("Тест загрузки сцены (Редактор)")]
    private void TestLoadSceneInEditor()
    {
        if (Application.isPlaying)
        {
            Debug.LogError("[SwordQuestLoaderDirect] Этот метод предназначен для использования в режиме редактирования, а не в режиме игры!");
            Debug.LogError("[SwordQuestLoaderDirect] Остановите игру и попробуйте снова.");
            return;
        }
        
        Debug.Log("[SwordQuestLoaderDirect] Тестирование открытия сцены в режиме редактирования...");
        
        // Проверяем наличие несохраненных изменений в текущей сцене
        if (EditorSceneManager.GetActiveScene().isDirty)
        {
            bool save = EditorUtility.DisplayDialog(
                "Сохранить текущую сцену?",
                "В текущей сцене есть несохраненные изменения. Сохранить перед открытием другой сцены?",
                "Сохранить", "Не сохранять"
            );
            
            if (save)
            {
                EditorSceneManager.SaveOpenScenes();
            }
        }
        
        // Сначала пробуем открыть по пути
        if (System.IO.File.Exists(sceneAssetPath))
        {
            Debug.Log($"[SwordQuestLoaderDirect] Открываем сцену по пути: {sceneAssetPath}");
            EditorSceneManager.OpenScene(sceneAssetPath, OpenSceneMode.Single);
        }
        else
        {
            Debug.LogError($"[SwordQuestLoaderDirect] Файл сцены не найден по указанному пути: {sceneAssetPath}");
            
            // Поиск сцены по имени в проекте
            string[] guids = AssetDatabase.FindAssets("t:Scene " + sceneToLoad);
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                Debug.Log($"[SwordQuestLoaderDirect] Найдена сцена: {path}");
                EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            }
            else
            {
                Debug.LogError($"[SwordQuestLoaderDirect] Сцена '{sceneToLoad}' не найдена в проекте.");
                Debug.LogError("[SwordQuestLoaderDirect] Пожалуйста, укажите правильное имя сцены или путь к файлу сцены.");
            }
        }
    }
    
    // Метод для добавления сцены в Build Settings
    [ContextMenu("Добавить сцену в Build Settings")]
    private void AddSceneToBuildSettings()
    {
        if (System.IO.File.Exists(sceneAssetPath))
        {
            Debug.Log($"[SwordQuestLoaderDirect] Добавляем сцену в Build Settings: {sceneAssetPath}");
            
            // Получаем текущие сцены в Build Settings
            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            
            // Проверяем, не добавлена ли уже сцена
            bool sceneExists = false;
            foreach (var scene in scenes)
            {
                if (scene.path == sceneAssetPath)
                {
                    sceneExists = true;
                    break;
                }
            }
            
            // Если сцены еще нет, добавляем ее
            if (!sceneExists)
            {
                scenes.Add(new EditorBuildSettingsScene(sceneAssetPath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
                Debug.Log($"[SwordQuestLoaderDirect] Сцена успешно добавлена в Build Settings: {sceneAssetPath}");
            }
            else
            {
                Debug.Log($"[SwordQuestLoaderDirect] Сцена уже присутствует в Build Settings: {sceneAssetPath}");
            }
        }
        else
        {
            Debug.LogError($"[SwordQuestLoaderDirect] Файл сцены не найден по указанному пути: {sceneAssetPath}");
            
            // Поиск сцены по имени в проекте
            string[] guids = AssetDatabase.FindAssets("t:Scene " + sceneToLoad);
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                Debug.Log($"[SwordQuestLoaderDirect] Найдена сцена: {path}");
                
                // Получаем текущие сцены в Build Settings
                List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
                
                // Проверяем, не добавлена ли уже сцена
                bool sceneExists = false;
                foreach (var scene in scenes)
                {
                    if (scene.path == path)
                    {
                        sceneExists = true;
                        break;
                    }
                }
                
                // Если сцены еще нет, добавляем ее
                if (!sceneExists)
                {
                    scenes.Add(new EditorBuildSettingsScene(path, true));
                    EditorBuildSettings.scenes = scenes.ToArray();
                    Debug.Log($"[SwordQuestLoaderDirect] Сцена успешно добавлена в Build Settings: {path}");
                }
                else
                {
                    Debug.Log($"[SwordQuestLoaderDirect] Сцена уже присутствует в Build Settings: {path}");
                }
            }
            else
            {
                Debug.LogError($"[SwordQuestLoaderDirect] Сцена '{sceneToLoad}' не найдена в проекте.");
            }
        }
    }
    #endif

    // Публичный метод для вызова загрузки сцены без затухания из других скриптов
    public void LoadSceneDirectly(string sceneName)
    {
        sceneToLoad = sceneName;
        LoadScene();
    }
}

public static class TransformExtensions
{
    // Метод для получения полного пути к объекту в иерархии
    public static string GetPath(this Transform transform)
    {
        if (transform.parent == null)
            return transform.name;
        return transform.parent.GetPath() + "/" + transform.name;
    }
} 