using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SwordQuestLoader : MonoBehaviour
{
    [Tooltip("Имя сцены для загрузки")]
    [SerializeField] private string sceneToLoad = "Sword_Quest_Scene";
    
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
            Debug.Log("[SwordQuestLoader] Подписка на события успешна");
            
            // Проверка наличия сцены в билде
            CheckSceneExistenceInBuild();
        }
        else
        {
            Debug.LogError("[SwordQuestLoader] CutsceneManager не найден");
        }
    }
    
    private void CheckSceneExistenceInBuild()
    {
        bool sceneExists = false;
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            string scenePath = SceneUtility.GetScenePathByBuildIndex(i);
            string sceneName = System.IO.Path.GetFileNameWithoutExtension(scenePath);
            
            if (sceneName == sceneToLoad)
            {
                sceneExists = true;
                Debug.Log($"[SwordQuestLoader] Сцена {sceneToLoad} найдена в Build Settings с индексом {i}");
                break;
            }
        }
        
        if (!sceneExists)
        {
            Debug.LogError($"[SwordQuestLoader] ОШИБКА: Сцена {sceneToLoad} НЕ ДОБАВЛЕНА в Build Settings!");
            Debug.LogError("[SwordQuestLoader] Добавьте сцену через File > Build Settings... > Add Open Scenes");
        }
    }
    
    private void OnDestroy()
    {
        // Отписываемся от события
        CutsceneManager.OnCutsceneEnded -= OnCutsceneEnded;
    }
    
    private void OnCutsceneEnded(GameObject cutscene)
    {
        // Выводим информацию о каждой завершенной катсцене
        Debug.Log($"[SwordQuestLoader] Завершена катсцена: {cutscene.name}");
        
        // Проверяем, завершилась ли нужная катсцена
        if (cutscene.name.Contains("QuestSword_01"))
        {
            Debug.Log($"[SwordQuestLoader] Катсцена QuestSword_01 завершена, загружаем сцену {sceneToLoad}");
            
            // Проверка наличия сцены в билде
            bool sceneExists = false;
            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                string scenePath = SceneUtility.GetScenePathByBuildIndex(i);
                if (System.IO.Path.GetFileNameWithoutExtension(scenePath) == sceneToLoad)
                {
                    sceneExists = true;
                    break;
                }
            }
            
            if (!sceneExists)
            {
                Debug.LogError($"[SwordQuestLoader] ОШИБКА: Сцена {sceneToLoad} не найдена в Build Settings!");
                return;
            }
            
            if (useFade)
            {
                StartCoroutine(LoadSceneWithFade());
            }
            else
            {
                // Загружаем сцену без эффекта затемнения
                Debug.Log($"[SwordQuestLoader] Запускаем загрузку сцены: {sceneToLoad}");
                try
                {
                    SceneManager.LoadScene(sceneToLoad);
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[SwordQuestLoader] Ошибка загрузки сцены: {e.Message}");
                }
            }
        }
    }
    
    private IEnumerator LoadSceneWithFade()
    {
        Debug.Log("[SwordQuestLoader] Начинаем загрузку сцены с затемнением");
        
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
        Debug.Log($"[SwordQuestLoader] Экран затемнен, загружаем сцену: {sceneToLoad}");
        try
        {
            SceneManager.LoadScene(sceneToLoad);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SwordQuestLoader] Ошибка загрузки сцены: {e.Message}");
        }
    }
} 