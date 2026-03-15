using UnityEngine;
using System.Collections.Generic;

public class LetterCollectorSystem : MonoBehaviour
{
    [Header("Настройки системы")]
    public Transform[] spawnPoints; // Точки спавна букв
    public float letterRotationSpeed = 50f; // Скорость вращения букв
    public float letterBobSpeed = 2f; // Скорость покачивания букв
    public float letterBobHeight = 0.5f; // Высота покачивания
    
    [Header("Слоты букв")]
    [Tooltip("Настройте префабы и буквы для каждого слота")]
    public LetterSlot[] letterSlots = new LetterSlot[]
    {
        new LetterSlot { letter = "A", prefab = null },
        new LetterSlot { letter = "B", prefab = null },
        new LetterSlot { letter = "C", prefab = null },
        new LetterSlot { letter = "D", prefab = null },
        new LetterSlot { letter = "E", prefab = null },
        new LetterSlot { letter = "F", prefab = null },
        new LetterSlot { letter = "G", prefab = null }
    };
    
    [System.Serializable]
    public class LetterSlot
    {
        [Tooltip("Буква, которая будет отображаться в UI")]
        public string letter = "A";
        [Tooltip("Префаб буквы для этого слота")]
        public GameObject prefab;
    }
    
    [Header("UI")]
    public TMPro.TextMeshProUGUI letterCounterText; // Текст счетчика собранных букв
    
    [Header("Эффекты")]
    public bool useParticleEffects = true; // Использовать ParticleSystem эффекты
    public GameObject customParticlePrefab; // Кастомный ParticleSystem префаб для эффекта сбора
    
    private List<GameObject> spawnedLetters = new List<GameObject>();
    private int collectedLetters = 0;
    private const int TOTAL_LETTERS = 7;
    
    void Start()
    {
        SpawnLetters();
        UpdateLetterCounter();
    }
    
    void Update()
    {
        // Вращение и покачивание букв
        foreach (GameObject letter in spawnedLetters)
        {
            if (letter != null)
            {
                // Вращение
                letter.transform.Rotate(0, letterRotationSpeed * Time.deltaTime, 0);
                
                // Покачивание
                float bobOffset = Mathf.Sin(Time.time * letterBobSpeed) * letterBobHeight;
                Vector3 originalPosition = letter.GetComponent<LetterBehavior>().originalPosition;
                letter.transform.position = originalPosition + Vector3.up * bobOffset;
            }
        }
    }
    
    void SpawnLetters()
    {
        Debug.Log($"=== Начинаем создание букв ===");
        Debug.Log($"Количество слотов: {letterSlots.Length}");
        Debug.Log($"Количество точек спавна: {spawnPoints?.Length ?? 0}");
        
        if (letterSlots.Length != TOTAL_LETTERS || spawnPoints.Length != TOTAL_LETTERS)
        {
            Debug.LogError($"Количество слотов букв ({letterSlots.Length}) и точек спавна ({spawnPoints?.Length ?? 0}) должно быть равно {TOTAL_LETTERS}!");
            return;
        }
        
        for (int i = 0; i < TOTAL_LETTERS; i++)
        {
            Debug.Log($"Проверяем слот {i}:");
            Debug.Log($"  - Буква: '{letterSlots[i].letter}'");
            Debug.Log($"  - Префаб: {(letterSlots[i].prefab != null ? letterSlots[i].prefab.name : "НЕ НАЗНАЧЕН")}");
            Debug.Log($"  - Точка спавна: {(spawnPoints[i] != null ? spawnPoints[i].name : "НЕ НАЗНАЧЕНА")}");
            
            if (letterSlots[i].prefab != null && spawnPoints[i] != null)
            {
                GameObject letter = Instantiate(letterSlots[i].prefab, spawnPoints[i].position, spawnPoints[i].rotation);
                letter.name = $"Letter_{letterSlots[i].letter}";
                
                // Добавляем компонент поведения буквы
                LetterBehavior letterBehavior = letter.AddComponent<LetterBehavior>();
                letterBehavior.originalPosition = spawnPoints[i].position;
                letterBehavior.letterIndex = i; // Индекс по порядку
                letterBehavior.letterText = letterSlots[i].letter; // Устанавливаем текст буквы
                letterBehavior.collectorSystem = this;
                
                spawnedLetters.Add(letter);
                
                Debug.Log($"✓ Создана буква '{letterSlots[i].letter}' в слоте {i}");
            }
            else
            {
                Debug.LogWarning($"✗ Слот {i}: префаб или точка спавна не назначены!");
            }
        }
        
        Debug.Log($"=== Создано букв: {spawnedLetters.Count}/{TOTAL_LETTERS} ===");
    }
    
    public void CollectLetter(int letterIndex)
    {
        Debug.Log($"=== Попытка сбора буквы с индексом {letterIndex} ===");
        Debug.Log($"Всего букв в списке: {spawnedLetters.Count}");
        Debug.Log($"Уже собрано: {collectedLetters}");
        
        if (letterIndex < 0 || letterIndex >= spawnedLetters.Count)
        {
            Debug.LogError($"Неверный индекс буквы: {letterIndex}. Допустимый диапазон: 0-{spawnedLetters.Count - 1}");
            return;
        }
        
        if (spawnedLetters[letterIndex] == null)
        {
            Debug.LogWarning($"Буква с индексом {letterIndex} уже была собрана!");
            return;
        }
        
        collectedLetters++;
        UpdateLetterCounter();
        
        // Получаем букву напрямую из LetterBehavior
        string collectedLetter = GetLetterByIndex(letterIndex); // Fallback
        
        GameObject letterObject = spawnedLetters[letterIndex];
        if (letterObject != null)
        {
            LetterBehavior letterBehavior = letterObject.GetComponent<LetterBehavior>();
            if (letterBehavior != null && !string.IsNullOrEmpty(letterBehavior.letterText))
            {
                collectedLetter = letterBehavior.letterText;
            }
        }
        
        // Уведомляем UI о собранной букве
        NotifyUIAboutCollectedLetter(collectedLetter);
        
        // Отладочная информация
        Debug.Log($"✓ Собрана буква: {collectedLetter} (индекс: {letterIndex})");
        
        // Эффект сбора
        GameObject letter = spawnedLetters[letterIndex];
        if (letter != null)
        {
            // Создаем эффект в зависимости от настроек
            if (useParticleEffects)
            {
                CreateCollectEffect(letter.transform.position);
            }
            else
            {
                CreateSimpleCollectEffect(letter.transform.position);
            }
            
            // Уничтожаем букву
            Destroy(letter);
            spawnedLetters[letterIndex] = null;
        }
        
        // Проверяем завершение
        if (collectedLetters >= TOTAL_LETTERS)
        {
            OnAllLettersCollected();
        }
        
        Debug.Log($"=== Сбор завершен. Всего собрано: {collectedLetters}/{TOTAL_LETTERS} ===");
    }
    
    string GetLetterByIndex(int index)
    {
        // Получаем букву по индексу из слотов
        if (index >= 0 && index < letterSlots.Length)
        {
            return letterSlots[index].letter;
        }
        
        // Fallback - возвращаем индекс как букву
        return $"Letter_{index}";
    }
    
    void NotifyUIAboutCollectedLetter(string letter)
    {
        // Находим UI компонент и уведомляем его
        LetterDisplayUI displayUI = FindObjectOfType<LetterDisplayUI>();
        if (displayUI != null)
        {
            displayUI.AddCollectedLetter(letter);
        }
    }
    
    void CreateCollectEffect(Vector3 position)
    {
        // Проверяем, есть ли кастомный префаб
        if (customParticlePrefab != null)
        {
            // Создаем кастомный эффект из префаба
            GameObject effect = Instantiate(customParticlePrefab, position, Quaternion.identity);
            effect.name = "CustomCollectEffect";
            
            // Добавляем компонент для автоматического уничтожения
            ParticleEffectAutoDestroy autoDestroy = effect.GetComponent<ParticleEffectAutoDestroy>();
            if (autoDestroy == null)
            {
                autoDestroy = effect.AddComponent<ParticleEffectAutoDestroy>();
            }
            
            // Настраиваем автоматическое уничтожение
            autoDestroy.destroyOnParticleSystemEnd = true;
            autoDestroy.destroyDelay = 3f;
        }
        else
        {
            // Создаем стандартный эффект сбора
            GameObject effect = new GameObject("CollectEffect");
            effect.transform.position = position;
            
            // Добавляем компонент эффекта
            CollectEffect collectEffect = effect.AddComponent<CollectEffect>();
            collectEffect.Initialize(position);
        }
    }
    
    // Метод автоматического уничтожения эффектов перенесен в ParticleEffectAutoDestroy
    
    void CreateSimpleCollectEffect(Vector3 position)
    {
        // Создаем простой визуальный эффект без ParticleSystem
        GameObject effect = new GameObject("SimpleCollectEffect");
        effect.transform.position = position;
        
        // Создаем сферу для эффекта
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.transform.SetParent(effect.transform);
        sphere.transform.localScale = Vector3.one * 0.5f;
        
        // Настраиваем материал
        Renderer sphereRenderer = sphere.GetComponent<Renderer>();
        Material effectMaterial = new Material(Shader.Find("Standard"));
        effectMaterial.color = Color.yellow;
        effectMaterial.EnableKeyword("_EMISSION");
        effectMaterial.SetColor("_EmissionColor", Color.yellow * 2f);
        sphereRenderer.material = effectMaterial;
        
        // Удаляем коллайдер
        DestroyImmediate(sphere.GetComponent<Collider>());
        
        // Запускаем корутину для анимации
        StartCoroutine(SimpleEffectAnimation(effect));
    }
    
    System.Collections.IEnumerator SimpleEffectAnimation(GameObject effect)
    {
        float duration = 1f;
        float elapsedTime = 0f;
        Vector3 startScale = effect.transform.localScale;
        
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / duration;
            
            // Увеличиваем размер
            effect.transform.localScale = Vector3.Lerp(startScale, startScale * 3f, progress);
            
            // Уменьшаем прозрачность
            Renderer renderer = effect.GetComponentInChildren<Renderer>();
            if (renderer != null)
            {
                Color color = renderer.material.color;
                color.a = Mathf.Lerp(1f, 0f, progress);
                renderer.material.color = color;
            }
            
            yield return null;
        }
        
        Destroy(effect);
    }
    
    void UpdateLetterCounter()
    {
        if (letterCounterText != null)
        {
            letterCounterText.text = $"Буквы: {collectedLetters}/{TOTAL_LETTERS}";
        }
    }
    
    void OnAllLettersCollected()
    {
        Debug.Log("Все буквы собраны! Поздравляем!");
        
        // Здесь можно добавить логику завершения уровня
        // Например, показать сообщение о победе, загрузить следующую сцену и т.д.
    }
    
    // Метод для получения количества собранных букв
    public int GetCollectedLetters()
    {
        return collectedLetters;
    }
    
    // Метод для получения общего количества букв
    public int GetTotalLetters()
    {
        return TOTAL_LETTERS;
    }
    
    [ContextMenu("Проверить позиции букв")]
    public void CheckLetterPositions()
    {
        Debug.Log("=== Позиции букв ===");
        for (int i = 0; i < spawnedLetters.Count; i++)
        {
            if (spawnedLetters[i] != null)
            {
                Debug.Log($"Буква {i}: {spawnedLetters[i].name} в позиции {spawnedLetters[i].transform.position}");
            }
            else
            {
                Debug.Log($"Буква {i}: УЖЕ СОБРАНА");
            }
        }
        Debug.Log($"Всего букв: {spawnedLetters.Count}, Собрано: {collectedLetters}");
    }
}