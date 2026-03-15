using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;

public class KrapivaTracker : MonoBehaviour
{
    [Header("Настройки сообщения")]
    [Tooltip("Текст сообщения, которое появится после уничтожения всей крапивы")]
    public string completionMessage = "Вся крапива уничтожена!";
    
    [Tooltip("Время отображения сообщения в секундах")]
    public float messageDisplayTime = 3f;
    
    [Tooltip("Время появления/исчезновения текста в секундах")]
    public float fadeDuration = 0.5f;
    
    [Header("UI элементы")]
    [Tooltip("Текстовый компонент для отображения сообщения")]
    public TextMeshProUGUI messageText;
    
    // Приватные переменные
    private WallWalker wallWalker;
    private bool messageShown = false;
    private Coroutine fadeCoroutine;
    private bool wasWallWalkerEnabled = false;
    
    void Start()
    {
        // Находим компонент WallWalker у игрока
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            wallWalker = player.GetComponent<WallWalker>();
            if (wallWalker != null)
            {
                wasWallWalkerEnabled = wallWalker.enabled;
            }
        }
        
        // Если текстовый компонент не назначен, создаем его
        if (messageText == null)
        {
            CreateMessageUI();
        }
        
        // Скрываем сообщение при старте
        if (messageText != null)
        {
            messageText.gameObject.SetActive(false);
            messageText.alpha = 0f;
        }
    }
    
    void Update()
    {
        // Проверяем наличие крапивы на сцене
        bool hasKrapiva = CheckForKrapiva();
        
        // Управляем WallWalker
        if (wallWalker != null)
        {
            if (hasKrapiva)
            {
                // Сохраняем состояние перед отключением
                if (wallWalker.enabled)
                {
                    wasWallWalkerEnabled = true;
                    wallWalker.enabled = false;
                }
            }
            else
            {
                // Восстанавливаем предыдущее состояние
                if (wasWallWalkerEnabled)
                {
                    wallWalker.enabled = true;
                    // Даем небольшую задержку для инициализации
                    StartCoroutine(ReinitializeWallWalker());
                }
            }
        }
        
        // Если крапивы нет и сообщение еще не показывалось
        if (!hasKrapiva && !messageShown)
        {
            ShowCompletionMessage();
        }
    }

    private IEnumerator ReinitializeWallWalker()
    {
        // Даем время на инициализацию
        yield return new WaitForSeconds(0.1f);
        
        if (wallWalker != null)
        {
            // Перезапускаем компонент
            wallWalker.enabled = false;
            yield return new WaitForEndOfFrame();
            wallWalker.enabled = true;
        }
    }
    
    private bool CheckForKrapiva()
    {
        // Ищем все объекты с именем Krapiva_mainPREFAB
        GameObject[] krapivaObjects = GameObject.FindObjectsOfType<GameObject>();
        foreach (GameObject obj in krapivaObjects)
        {
            if (obj.name.Contains("Krapiva_mainPREFAB") && obj.activeInHierarchy)
            {
                return true;
            }
        }
        return false;
    }
    
    private void ShowCompletionMessage()
    {
        messageShown = true;
        
        if (messageText != null)
        {
            messageText.text = completionMessage;
            messageText.gameObject.SetActive(true);
            
            // Запускаем fade in
            if (fadeCoroutine != null)
            {
                StopCoroutine(fadeCoroutine);
            }
            fadeCoroutine = StartCoroutine(FadeIn());
            
            // Запускаем таймер для скрытия сообщения
            StartCoroutine(HideMessageAfterDelay());
        }
    }
    
    private IEnumerator HideMessageAfterDelay()
    {
        yield return new WaitForSeconds(messageDisplayTime);
        if (messageText != null)
        {
            // Запускаем fade out
            if (fadeCoroutine != null)
            {
                StopCoroutine(fadeCoroutine);
            }
            fadeCoroutine = StartCoroutine(FadeOut());
        }
    }
    
    private IEnumerator FadeIn()
    {
        float elapsedTime = 0f;
        Color startColor = messageText.color;
        startColor.a = 0f;
        Color targetColor = messageText.color;
        targetColor.a = 1f;
        
        messageText.color = startColor;
        
        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;
            float normalizedTime = elapsedTime / fadeDuration;
            messageText.color = Color.Lerp(startColor, targetColor, normalizedTime);
            yield return null;
        }
        
        messageText.color = targetColor;
    }
    
    private IEnumerator FadeOut()
    {
        float elapsedTime = 0f;
        Color startColor = messageText.color;
        Color targetColor = messageText.color;
        targetColor.a = 0f;
        
        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;
            float normalizedTime = elapsedTime / fadeDuration;
            messageText.color = Color.Lerp(startColor, targetColor, normalizedTime);
            yield return null;
        }
        
        messageText.color = targetColor;
        messageText.gameObject.SetActive(false);
    }
    
    private void CreateMessageUI()
    {
        // Создаем Canvas если его нет
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("MessageCanvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
        }
        
        // Создаем текстовый объект
        GameObject textObj = new GameObject("CompletionMessage");
        textObj.transform.SetParent(canvas.transform, false);
        
        messageText = textObj.AddComponent<TextMeshProUGUI>();
        messageText.fontSize = 36;
        messageText.alignment = TextAlignmentOptions.Center;
        messageText.color = new Color(1f, 1f, 1f, 0f); // Начинаем с прозрачного текста
        
        // Настраиваем RectTransform
        RectTransform rectTransform = messageText.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = new Vector2(600, 100);
    }
} 