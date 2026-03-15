using UnityEngine;
using TMPro;

public class LetterCounterUI : MonoBehaviour
{
    [Header("UI элементы")]
    public TMPro.TextMeshProUGUI counterText;
    public TMPro.TextMeshProUGUI progressText;
    public UnityEngine.UI.Image progressBar;
    
    [Header("Настройки отображения")]
    public string counterFormat = "Буквы: {0}/{1}";
    public string progressFormat = "Прогресс: {0}%";
    public Color progressBarColor = Color.green;
    public Color progressBarBackgroundColor = Color.gray;
    
    private LetterCollectorSystem collectorSystem;
    private int totalLetters = 7;
    
    void Start()
    {
        // Находим систему сбора букв
        collectorSystem = FindObjectOfType<LetterCollectorSystem>();
        
        if (collectorSystem == null)
        {
            Debug.LogWarning("LetterCollectorSystem не найден на сцене!");
        }
        
        UpdateUI();
    }
    
    void Update()
    {
        UpdateUI();
    }
    
    void UpdateUI()
    {
        if (collectorSystem != null)
        {
            int collected = collectorSystem.GetCollectedLetters();
            int total = collectorSystem.GetTotalLetters();
            
            // Обновляем счетчик
            if (counterText != null)
            {
                counterText.text = string.Format(counterFormat, collected, total);
            }
            
            // Обновляем прогресс
            if (progressText != null)
            {
                float progressPercent = (float)collected / total * 100f;
                progressText.text = string.Format(progressFormat, Mathf.RoundToInt(progressPercent));
            }
            
            // Обновляем прогресс бар
            if (progressBar != null)
            {
                float progress = (float)collected / total;
                progressBar.fillAmount = progress;
                progressBar.color = progressBarColor;
            }
        }
    }
    
    // Метод для ручного обновления UI
    public void RefreshUI()
    {
        UpdateUI();
    }
    
    // Метод для установки цветов
    public void SetProgressBarColors(Color fillColor, Color backgroundColor)
    {
        progressBarColor = fillColor;
        progressBarBackgroundColor = backgroundColor;
        
        if (progressBar != null)
        {
            progressBar.color = fillColor;
        }
    }
}