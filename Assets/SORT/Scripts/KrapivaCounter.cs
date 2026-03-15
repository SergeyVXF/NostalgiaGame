using UnityEngine;
using UnityEngine.UI;
using System;

public class KrapivaCounter : MonoBehaviour
{
    // Событие изменения счетчика
    public static event Action<int> OnCounterChanged;
    
    // Статический счетчик для доступа из других скриптов
    public static int destroyedKrapivaCount = 0;
    
    // Ссылка на текстовый компонент для отображения счетчика
    public Text counterText;
    
    // UI элемент для отображения на экране
    private Text uiText;
    
    // Формат отображения текста
    public string displayFormat = "Уничтожено крапивы: {0}";
    
    // Объект, который будет активирован, когда счетчик достигнет целевого значения
    public GameObject objectToActivate;
    
    // Целевое значение счетчика
    public int targetCount = 100;
    
    // Был ли объект уже активирован
    private bool objectActivated = false;
    
    private void Awake()
    {
        // Сбрасываем счетчик при загрузке сцены
        destroyedKrapivaCount = 0;
        
        // Если UI текст не назначен, создаем его автоматически
        if (counterText == null)
        {
            // Проверяем, есть ли уже Canvas на сцене
            Canvas canvas = FindObjectOfType<Canvas>();
            
            // Если Canvas не найден, создаем новый
            if (canvas == null)
            {
                GameObject canvasObj = new GameObject("KrapivaCounterCanvas");
                canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasObj.AddComponent<CanvasScaler>();
                canvasObj.AddComponent<GraphicRaycaster>();
            }
            
            // Создаем текстовый объект
            GameObject textObj = new GameObject("KrapivaCounterText");
            textObj.transform.SetParent(canvas.transform);
            uiText = textObj.AddComponent<Text>();
            
            // Настраиваем компонент Text
            uiText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            uiText.fontSize = 24;
            uiText.color = Color.white;
            uiText.alignment = TextAnchor.UpperRight;
            
            // Устанавливаем позицию и размер
            RectTransform rectTransform = uiText.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(1, 1);
            rectTransform.anchorMax = new Vector2(1, 1);
            rectTransform.pivot = new Vector2(1, 1);
            rectTransform.anchoredPosition = new Vector2(-20, -20);
            rectTransform.sizeDelta = new Vector2(300, 50);
            
            counterText = uiText;
        }
        
        // Обновляем отображение счетчика
        UpdateCounterDisplay();
    }
    
    // Метод для увеличения счетчика
    public static void IncrementCounter()
    {
        destroyedKrapivaCount++;
        
        // Вызываем событие изменения счетчика
        OnCounterChanged?.Invoke(destroyedKrapivaCount);
        
        // Находим все экземпляры этого скрипта и обновляем их интерфейс
        KrapivaCounter[] counters = FindObjectsOfType<KrapivaCounter>();
        foreach (KrapivaCounter counter in counters)
        {
            counter.UpdateCounterDisplay();
            counter.CheckTargetCount();
        }
    }
    
    // Проверяет, достигнуто ли целевое значение счетчика
    private void CheckTargetCount()
    {
        if (!objectActivated && destroyedKrapivaCount >= targetCount && objectToActivate != null)
        {
            objectToActivate.SetActive(true);
            objectActivated = true;
            Debug.Log($"Активирован объект {objectToActivate.name}! Уничтожено крапивы: {destroyedKrapivaCount}");
            
            // Можно добавить визуальный эффект или звук здесь
        }
    }
    
    // Обновляет текст на экране
    public void UpdateCounterDisplay()
    {
        if (counterText != null)
        {
            counterText.text = string.Format(displayFormat, destroyedKrapivaCount);
        }
    }
} 