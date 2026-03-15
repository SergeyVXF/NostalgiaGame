using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class LetterDisplayUI : MonoBehaviour
{
    [Header("UI элементы")]
    public TMPro.TextMeshProUGUI letterDisplayText;
    public TMPro.TextMeshProUGUI counterText;
    public UnityEngine.UI.Image progressBar;
    
    [Header("Настройки отображения")]
    public string counterFormat = "Собрано: {0}/{1}";
    public string letterDisplayFormat = "Собранные буквы: {0}";
    public Color collectedLetterColor = Color.green;
    public Color uncollectedLetterColor = Color.gray;
    public string uncollectedLetterSymbol = "?";
    
    [Header("Анимация")]
    public float letterAppearDuration = 0.5f;
    public float letterScaleEffect = 1.2f;
    
    private LetterCollectorSystem collectorSystem;
    private List<string> collectedLetters = new List<string>();
    private string[] allLetters = { "A", "N", "T", "I", "G", "O", "P" };
    private Dictionary<string, float> letterAppearTimes = new Dictionary<string, float>();
    
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
        UpdateLetterAnimations();
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
            
            // Обновляем прогресс бар
            if (progressBar != null)
            {
                float progress = (float)collected / total;
                progressBar.fillAmount = progress;
            }
            
            // Обновляем отображение букв
            UpdateLetterDisplay();
        }
    }
    
    void UpdateLetterDisplay()
    {
        if (letterDisplayText != null)
        {
            string displayText = "";
            
            for (int i = 0; i < allLetters.Length; i++)
            {
                string letter = allLetters[i];
                bool isCollected = collectedLetters.Contains(letter);
                
                if (isCollected)
                {
                    // Проверяем, нужно ли добавить анимацию
                    if (letterAppearTimes.ContainsKey(letter))
                    {
                        float timeSinceAppear = Time.time - letterAppearTimes[letter];
                        if (timeSinceAppear < letterAppearDuration)
                        {
                            // Анимация появления буквы
                            float scale = Mathf.Lerp(letterScaleEffect, 1f, timeSinceAppear / letterAppearDuration);
                            displayText += $"<size={scale * 100}%><color=#{ColorUtility.ToHtmlStringRGB(collectedLetterColor)}>{letter}</color></size>";
                        }
                        else
                        {
                            displayText += $"<color=#{ColorUtility.ToHtmlStringRGB(collectedLetterColor)}>{letter}</color>";
                        }
                    }
                    else
                    {
                        displayText += $"<color=#{ColorUtility.ToHtmlStringRGB(collectedLetterColor)}>{letter}</color>";
                    }
                }
                else
                {
                    displayText += $"<color=#{ColorUtility.ToHtmlStringRGB(uncollectedLetterColor)}>{uncollectedLetterSymbol}</color>";
                }
                
                // Добавляем пробел между буквами
                if (i < allLetters.Length - 1)
                {
                    displayText += " ";
                }
            }
            
            letterDisplayText.text = string.Format(letterDisplayFormat, displayText);
        }
    }
    
    void UpdateLetterAnimations()
    {
        // Удаляем старые записи анимаций
        List<string> lettersToRemove = new List<string>();
        foreach (var kvp in letterAppearTimes)
        {
            if (Time.time - kvp.Value > letterAppearDuration)
            {
                lettersToRemove.Add(kvp.Key);
            }
        }
        
        foreach (string letter in lettersToRemove)
        {
            letterAppearTimes.Remove(letter);
        }
    }
    
    // Метод для добавления собранной буквы
    public void AddCollectedLetter(string letter)
    {
        Debug.Log($"=== LetterDisplayUI.AddCollectedLetter: '{letter}' ===");
        Debug.Log($"Текущие собранные буквы: [{string.Join(", ", collectedLetters)}]");
        
        if (!collectedLetters.Contains(letter))
        {
            collectedLetters.Add(letter);
            letterAppearTimes[letter] = Time.time;
            
            Debug.Log($"✓ Буква '{letter}' добавлена в UI");
            Debug.Log($"Новые собранные буквы: [{string.Join(", ", collectedLetters)}]");
            
            // Эффект появления буквы отключен
            // CreateLetterAppearEffect(letter);
        }
        else
        {
            Debug.LogWarning($"Буква '{letter}' уже была добавлена ранее!");
        }
    }
    
    // Метод создания эффекта появления буквы отключен
    /*
    void CreateLetterAppearEffect(string letter)
    {
        // Создаем временный объект для эффекта
        GameObject effectObject = new GameObject($"LetterAppearEffect_{letter}");
        effectObject.transform.SetParent(transform);
        
        // Добавляем компонент эффекта
        LetterAppearEffect effect = effectObject.AddComponent<LetterAppearEffect>();
        effect.Initialize(letter, collectedLetterColor, letterAppearDuration);
    }
    */
    
    // Метод для сброса собранных букв
    public void ResetCollectedLetters()
    {
        collectedLetters.Clear();
        letterAppearTimes.Clear();
        UpdateUI();
    }
    
    // Метод для получения списка собранных букв
    public List<string> GetCollectedLetters()
    {
        return new List<string>(collectedLetters);
    }
    
    // Метод для проверки, собрана ли определенная буква
    public bool IsLetterCollected(string letter)
    {
        return collectedLetters.Contains(letter);
    }
    
    // Метод для получения количества собранных букв
    public int GetCollectedLettersCount()
    {
        return collectedLetters.Count;
    }
} 