using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    [Tooltip("Имя сцены для загрузки")]
    [SerializeField] private string sceneToLoad = "Sword_Quest_Scene";
    
    [Tooltip("Ключ катсцены, после которой нужно загрузить сцену")]
    [SerializeField] private string cutsceneKey = "QuestSword_01";
    
    [Tooltip("Использовать эффект затемнения при переходе")]
    [SerializeField] private bool useFade = true;
    
    [Tooltip("Длительность эффекта затемнения")]
    [SerializeField] private float fadeDuration = 1.0f;
    
    private CutsceneFadeManager fadeManager;
    
    private void Start()
    {
        // Проверяем наличие CutsceneManager
        if (CutsceneManager.Instance == null)
        {
            Debug.LogError("[SceneLoader] CutsceneManager не найден в сцене!");
            return;
        }
        
        // Подписываемся на событие завершения катсцены
        CutsceneManager.OnCutsceneEnded += OnCutsceneEnded;
        
        // Находим менеджер затемнения
        fadeManager = FindObjectOfType<CutsceneFadeManager>();
        if (fadeManager == null && useFade)
        {
            Debug.LogWarning("[SceneLoader] CutsceneFadeManager не найден в сцене! Эффект затемнения не будет применен.");
            useFade = false;
        }
    }
    
    private void OnDestroy()
    {
        // Отписываемся от события
        CutsceneManager.OnCutsceneEnded -= OnCutsceneEnded;
    }
    
    private void OnCutsceneEnded(GameObject cutscene)
    {
        // Проверяем, является ли завершенная катсцена нужной нам
        if (cutscene.name.Contains(cutsceneKey))
        {
            Debug.Log($"[SceneLoader] Катсцена {cutsceneKey} завершена, загружаем сцену {sceneToLoad}");
            
            if (useFade && fadeManager != null)
            {
                StartCoroutine(LoadSceneWithFade());
            }
            else
            {
                // Загружаем сцену без эффекта затемнения
                SceneManager.LoadScene(sceneToLoad);
            }
        }
    }
    
    private IEnumerator LoadSceneWithFade()
    {
        // Создаем временный Canvas с Image для затемнения, если fadeManager недоступен
        Canvas fadeCanvas = null;
        UnityEngine.UI.Image fadeImage = null;
        
        if (fadeManager == null)
        {
            // Создаем временный Canvas для затемнения
            GameObject fadeObj = new GameObject("FadeCanvas");
            fadeCanvas = fadeObj.AddComponent<Canvas>();
            fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            fadeCanvas.sortingOrder = 999; // Поверх всего UI
            
            // Создаем Image для затемнения
            GameObject imageObj = new GameObject("FadeImage");
            imageObj.transform.SetParent(fadeCanvas.transform, false);
            fadeImage = imageObj.AddComponent<UnityEngine.UI.Image>();
            fadeImage.color = new Color(0, 0, 0, 0);
            
            // Растягиваем Image на весь экран
            RectTransform rectTransform = fadeImage.GetComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.sizeDelta = Vector2.zero;
        }
        
        // Затемняем экран
        float elapsedTime = 0;
        Color color = Color.black;
        color.a = 0;
        
        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;
            float alpha = Mathf.Lerp(0, 1, elapsedTime / fadeDuration);
            color.a = alpha;
            
            if (fadeManager == null && fadeImage != null)
            {
                fadeImage.color = color;
            }
            
            yield return null;
        }
        
        // Экран полностью затемнен, загружаем сцену
        SceneManager.LoadScene(sceneToLoad);
    }
} 