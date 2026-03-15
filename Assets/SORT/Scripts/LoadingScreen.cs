using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

public class LoadingScreen : MonoBehaviour
{
    [Header("Camera Display")]
    [Tooltip("Показывать изображение с камеры")]
    public bool showCameraFeed = true;
    
    private RenderTexture cameraRenderTexture;
    private Camera sceneCamera;
    private RawImage cameraDisplay;
    
    void Start()
    {
        // Сначала настраиваем камеру
        SetupCameraFeed();
        
        CreateLoadingUI();
        StartCoroutine(LoadGameScene());
    }
    
    void SetupCameraFeed()
    {
        if (!showCameraFeed) return;
        
        // Ищем камеру в сцене (любую активную)
        sceneCamera = Camera.main;
        if (sceneCamera == null)
        {
            // Если нет Main Camera, берем первую попавшуюся
            sceneCamera = FindObjectOfType<Camera>();
        }
        
        if (sceneCamera != null)
        {
            Debug.Log($"[LoadingScreen] 📷 Найдена камера: {sceneCamera.name}");
            
            // Создаем RenderTexture для захвата изображения с камеры
            cameraRenderTexture = new RenderTexture(1920, 1080, 24);
            cameraRenderTexture.filterMode = FilterMode.Bilinear;
            
            // ВАЖНО: Сначала делаем снимок с камеры без изменения targetTexture
            RenderTexture currentRT = RenderTexture.active;
            RenderTexture.active = cameraRenderTexture;
            
            // Рендерим камеру в наш RenderTexture без изменения её targetTexture
            sceneCamera.Render();
            
            RenderTexture.active = currentRT;
            
            Debug.Log("[LoadingScreen] ✅ RenderTexture создан и назначен камере");
        }
        else
        {
            Debug.LogWarning("[LoadingScreen] ⚠️ Камера в сцене не найдена!");
        }
    }

    void CreateLoadingUI()
    {
        // Создаем Canvas
        GameObject canvasObj = new GameObject("LoadingCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();

        // Создаем EventSystem
        GameObject eventSystem = new GameObject("EventSystem");
        eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
        eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

        // Создаем отображение камеры (если включено)
        if (showCameraFeed && cameraRenderTexture != null)
        {
            GameObject cameraDisplayObj = new GameObject("CameraDisplay");
            cameraDisplayObj.transform.SetParent(canvasObj.transform, false);
            cameraDisplay = cameraDisplayObj.AddComponent<RawImage>();
            cameraDisplay.texture = cameraRenderTexture;
            
            RectTransform cameraRect = cameraDisplayObj.GetComponent<RectTransform>();
            cameraRect.anchorMin = Vector2.zero;
            cameraRect.anchorMax = Vector2.one;
            cameraRect.offsetMin = Vector2.zero;
            cameraRect.offsetMax = Vector2.zero;
            
            Debug.Log("[LoadingScreen] 📺 Отображение камеры добавлено в UI");
        }
        else
        {
            // Создаем обычный фон если камера не используется
            GameObject background = new GameObject("Background");
            background.transform.SetParent(canvasObj.transform, false);
            Image bgImage = background.AddComponent<Image>();
            bgImage.color = new Color(0.1f, 0.1f, 0.1f, 1f);
            RectTransform bgRect = background.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
        }

        // Заголовок убран по просьбе пользователя

        // Создаем прогресс-бар (внизу экрана)
        GameObject sliderObj = new GameObject("ProgressBar");
        sliderObj.transform.SetParent(canvasObj.transform, false);
        Slider slider = sliderObj.AddComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 0f;
        RectTransform sliderRect = sliderObj.GetComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(0.1f, 0.1f);
        sliderRect.anchorMax = new Vector2(0.9f, 0.15f);
        sliderRect.offsetMin = Vector2.zero;
        sliderRect.offsetMax = Vector2.zero;

        // Текст прогресса (внизу экрана)
        GameObject progressTextObj = new GameObject("ProgressText");
        progressTextObj.transform.SetParent(canvasObj.transform, false);
        Text progressTextComponent = progressTextObj.AddComponent<Text>();
        progressTextComponent.text = "0%";
        progressTextComponent.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        progressTextComponent.fontSize = 32;
        progressTextComponent.color = Color.white;
        progressTextComponent.alignment = TextAnchor.MiddleCenter;
        RectTransform progressTextRect = progressTextObj.GetComponent<RectTransform>();
        progressTextRect.anchorMin = new Vector2(0.1f, 0.05f);
        progressTextRect.anchorMax = new Vector2(0.9f, 0.1f);
        progressTextRect.offsetMin = Vector2.zero;
        progressTextRect.offsetMax = Vector2.zero;
    }

    IEnumerator LoadGameScene()
    {
        float progress = 0f;
        while (progress < 1f)
        {
            progress += Time.deltaTime * 0.3f;
            GameObject.Find("ProgressBar").GetComponent<Slider>().value = progress;
            GameObject.Find("ProgressText").GetComponent<Text>().text = Mathf.RoundToInt(progress * 100) + "%";
            
            // Обновляем изображение с камеры каждый кадр
            UpdateCameraFeed();
            
            yield return null;
        }
        
        yield return new WaitForSeconds(0.5f);
        
        // Очищаем ресурсы перед загрузкой новой сцены
        CleanupResources();
        
        SceneManager.LoadScene(2);
    }
    
    void UpdateCameraFeed()
    {
        if (sceneCamera != null && cameraRenderTexture != null && showCameraFeed)
        {
            // Обновляем изображение с камеры
            RenderTexture currentRT = RenderTexture.active;
            RenderTexture.active = cameraRenderTexture;
            
            sceneCamera.Render();
            
            RenderTexture.active = currentRT;
        }
    }
    
    void CleanupResources()
    {
        // Камера не трогается - она и так рендерит нормально
        
        if (cameraRenderTexture != null)
        {
            // Освобождаем RenderTexture
            cameraRenderTexture.Release();
            DestroyImmediate(cameraRenderTexture);
            Debug.Log("[LoadingScreen] 🗑️ RenderTexture освобожден");
        }
    }
    
    void OnDestroy()
    {
        // Дополнительная очистка при уничтожении объекта
        CleanupResources();
    }
}