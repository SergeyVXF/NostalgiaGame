using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class ScreenFade : MonoBehaviour
{
    [SerializeField] private float fadeDuration = 1f;
    [SerializeField] private Color fadeColor = Color.black;
    
    private Image fadeImage;
    private Canvas fadeCanvas;
    
    private void Awake()
    {
        CreateFadeCanvas();
    }
    
    private void CreateFadeCanvas()
    {
        // Создаем Canvas
        GameObject canvasObj = new GameObject("FadeCanvas");
        fadeCanvas = canvasObj.AddComponent<Canvas>();
        fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        fadeCanvas.sortingOrder = 999; // Убеждаемся, что затемнение будет поверх всего
        
        // Добавляем CanvasScaler
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        
        // Добавляем GraphicRaycaster
        canvasObj.AddComponent<GraphicRaycaster>();
        
        // Создаем Image для затемнения
        GameObject imageObj = new GameObject("FadeImage");
        imageObj.transform.SetParent(canvasObj.transform, false);
        
        fadeImage = imageObj.AddComponent<Image>();
        fadeImage.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, 0);
        
        // Устанавливаем размер Image на весь экран
        RectTransform rectTransform = fadeImage.rectTransform;
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.sizeDelta = Vector2.zero;
        rectTransform.anchoredPosition = Vector2.zero;
        
        // Скрываем затемнение по умолчанию
        fadeImage.gameObject.SetActive(false);
    }
    
    public IEnumerator FadeInAndOut(System.Action onFadeIn)
    {
        // Показываем затемнение
        fadeImage.gameObject.SetActive(true);
        
        // Затемняем экран
        float elapsedTime = 0f;
        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;
            float alpha = Mathf.Clamp01(elapsedTime / fadeDuration);
            fadeImage.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, alpha);
            yield return null;
        }
        
        // Выполняем действие после полного затемнения
        onFadeIn?.Invoke();
        
        // Осветляем экран
        elapsedTime = 0f;
        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;
            float alpha = Mathf.Clamp01(1 - (elapsedTime / fadeDuration));
            fadeImage.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, alpha);
            yield return null;
        }
        
        // Скрываем затемнение
        fadeImage.gameObject.SetActive(false);
    }
} 