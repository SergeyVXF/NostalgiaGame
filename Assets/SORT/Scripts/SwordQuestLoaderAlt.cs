using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SwordQuestLoaderAlt : MonoBehaviour
{
    [Tooltip("Путь к сцене для загрузки (с расширением .unity)")]
    [SerializeField] private string sceneToLoadPath = "Assets/ANTIGOP/Sword_Quest_Scene.unity";
    
    [Tooltip("Использовать эффект затемнения при переходе")]
    [SerializeField] private bool useFade = true;
    
    [Tooltip("Длительность эффекта затемнения")]
    [SerializeField] private float fadeDuration = 1.0f;
    
    private void Start()
    {
        // Подписываемся на событие завершения катсцены
        if (CutsceneManager.Instance != null)
        {
            CutsceneManager.OnCutsceneEnded += OnCutsceneEnded;
            Debug.Log("[SwordQuestLoaderAlt] Подписка на события успешна");
            Debug.Log($"[SwordQuestLoaderAlt] Будет загружаться сцена: {sceneToLoadPath}");
            Debug.Log($"[SwordQuestLoaderAlt] Количество сцен в Build Settings: {SceneManager.sceneCountInBuildSettings}");
            
            // Вывод списка всех сцен в билде
            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                string path = SceneUtility.GetScenePathByBuildIndex(i);
                Debug.Log($"[SwordQuestLoaderAlt] Сцена #{i}: {path}");
            }
        }
        else
        {
            Debug.LogError("[SwordQuestLoaderAlt] CutsceneManager не найден");
        }
    }
    
    private void OnDestroy()
    {
        // Отписываемся от события
        CutsceneManager.OnCutsceneEnded -= OnCutsceneEnded;
    }
    
    private void OnCutsceneEnded(GameObject cutscene)
    {
        Debug.Log($"[SwordQuestLoaderAlt] Завершена катсцена: {cutscene.name}");
        
        // Проверяем, завершилась ли нужная катсцена
        if (cutscene.name.Contains("QuestSword_01"))
        {
            Debug.Log($"[SwordQuestLoaderAlt] Катсцена QuestSword_01 завершена, загружаем сцену {sceneToLoadPath}");
            
            // Получаем имя сцены без пути и расширения .unity
            string sceneName = System.IO.Path.GetFileNameWithoutExtension(sceneToLoadPath);
            Debug.Log($"[SwordQuestLoaderAlt] Имя сцены для загрузки: {sceneName}");
            
            // Проверяем, существует ли сцена в списке билда
            bool sceneExists = false;
            int sceneIndex = -1;
            
            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                string path = SceneUtility.GetScenePathByBuildIndex(i);
                string name = System.IO.Path.GetFileNameWithoutExtension(path);
                
                if (name == sceneName)
                {
                    sceneExists = true;
                    sceneIndex = i;
                    Debug.Log($"[SwordQuestLoaderAlt] Сцена найдена в билде: {path} (индекс: {i})");
                    break;
                }
            }
            
            if (!sceneExists)
            {
                Debug.LogError($"[SwordQuestLoaderAlt] ОШИБКА: Сцена {sceneName} не найдена в Build Settings!");
                Debug.LogError("[SwordQuestLoaderAlt] Пожалуйста, добавьте сцену в Build Settings (File > Build Settings > Add Open Scenes)");
                return;
            }
            
            // Вариант 1: Загрузка по имени
            if (useFade)
            {
                StartCoroutine(LoadSceneWithFade(sceneName));
            }
            else
            {
                Debug.Log($"[SwordQuestLoaderAlt] Загружаем сцену по имени: {sceneName}");
                try
                {
                    SceneManager.LoadScene(sceneName);
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[SwordQuestLoaderAlt] Ошибка при загрузке сцены по имени: {e.Message}");
                    
                    // Вариант 2: Загрузка по индексу
                    Debug.Log($"[SwordQuestLoaderAlt] Пробуем загрузить сцену по индексу: {sceneIndex}");
                    try
                    {
                        SceneManager.LoadScene(sceneIndex);
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"[SwordQuestLoaderAlt] Ошибка при загрузке сцены по индексу: {ex.Message}");
                    }
                }
            }
        }
    }
    
    private IEnumerator LoadSceneWithFade(string sceneName)
    {
        Debug.Log($"[SwordQuestLoaderAlt] Начинаем загрузку сцены с затемнением: {sceneName}");
        
        // Создаем Canvas для затемнения
        GameObject fadeObj = new GameObject("FadeCanvas");
        Canvas fadeCanvas = fadeObj.AddComponent<Canvas>();
        fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        fadeCanvas.sortingOrder = 999; // Поверх всего UI
        
        // Создаем Image для затемнения
        GameObject imageObj = new GameObject("FadeImage");
        imageObj.transform.SetParent(fadeCanvas.transform, false);
        UnityEngine.UI.Image fadeImage = imageObj.AddComponent<UnityEngine.UI.Image>();
        fadeImage.color = new Color(0, 0, 0, 0);
        
        // Растягиваем Image на весь экран
        RectTransform rectTransform = fadeImage.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.sizeDelta = Vector2.zero;
        
        // Затемняем экран
        float elapsedTime = 0;
        Color color = Color.black;
        
        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;
            float alpha = Mathf.Lerp(0, 1, elapsedTime / fadeDuration);
            color.a = alpha;
            fadeImage.color = color;
            yield return null;
        }
        
        // Экран полностью затемнен, загружаем сцену
        Debug.Log($"[SwordQuestLoaderAlt] Экран затемнен, загружаем сцену: {sceneName}");
        try
        {
            SceneManager.LoadScene(sceneName);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SwordQuestLoaderAlt] Ошибка загрузки сцены: {e.Message}");
            
            // Пробуем загрузить сцену по индексу
            int sceneIndex = -1;
            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                string path = SceneUtility.GetScenePathByBuildIndex(i);
                if (System.IO.Path.GetFileNameWithoutExtension(path) == sceneName)
                {
                    sceneIndex = i;
                    break;
                }
            }
            
            if (sceneIndex >= 0)
            {
                Debug.Log($"[SwordQuestLoaderAlt] Пробуем загрузить сцену по индексу: {sceneIndex}");
                SceneManager.LoadScene(sceneIndex);
            }
        }
    }
} 