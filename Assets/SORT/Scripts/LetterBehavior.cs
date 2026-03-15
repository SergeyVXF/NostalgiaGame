using UnityEngine;

public class LetterBehavior : MonoBehaviour
{
    [Header("Настройки буквы")]
    public float glowIntensity = 1.5f; // Интенсивность свечения
    public Color glowColor = Color.yellow; // Цвет свечения
    public float pulseSpeed = 2f; // Скорость пульсации
    
    [Header("Звуки")]
    public AudioClip collectSound; // Звук сбора
    
    [HideInInspector]
    public Vector3 originalPosition; // Исходная позиция для покачивания
    [HideInInspector]
    public int letterIndex; // Индекс буквы
    [HideInInspector]
    public string letterText; // Текст буквы (A, B, C, D, E, F, G)
    [HideInInspector]
    public LetterCollectorSystem collectorSystem; // Ссылка на систему сбора
    
    private Renderer letterRenderer;
    private Material originalMaterial;
    private Material glowMaterial;
    private AudioSource audioSource;
    private bool isCollected = false;
    
    void Start()
    {
        // Получаем компоненты
        letterRenderer = GetComponent<Renderer>();
        audioSource = GetComponent<AudioSource>();
        
        // Если нет AudioSource, создаем его
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        // Настраиваем AudioSource
        audioSource.playOnAwake = false;
        audioSource.volume = 0.7f;
        
        // Создаем материал для свечения
        SetupGlowMaterial();
        
        // Добавляем коллайдер для триггера
        SetupCollider();
    }
    
    void SetupGlowMaterial()
    {
        if (letterRenderer != null)
        {
            originalMaterial = letterRenderer.material;
            
            // Создаем копию материала для свечения
            glowMaterial = new Material(originalMaterial);
            glowMaterial.EnableKeyword("_EMISSION");
            glowMaterial.SetColor("_EmissionColor", glowColor * glowIntensity);
            
            letterRenderer.material = glowMaterial;
        }
    }
    
    void SetupCollider()
    {
        // Проверяем, есть ли уже коллайдер
        Collider existingCollider = GetComponent<Collider>();
        
        if (existingCollider == null)
        {
            // Создаем триггер коллайдер
            BoxCollider boxCollider = gameObject.AddComponent<BoxCollider>();
            boxCollider.isTrigger = true;
            
            // Увеличиваем размер коллайдера для лучшего взаимодействия
            boxCollider.size *= 1.5f;
        }
        else
        {
            // Делаем существующий коллайдер триггером
            existingCollider.isTrigger = true;
        }
    }
    
    void Update()
    {
        if (!isCollected)
        {
            // Пульсация свечения
            float pulse = Mathf.Sin(Time.time * pulseSpeed) * 0.5f + 0.5f;
            if (glowMaterial != null)
            {
                glowMaterial.SetColor("_EmissionColor", glowColor * (glowIntensity * pulse));
            }
        }
    }
    
    void OnTriggerEnter(Collider other)
    {
        Debug.Log($"=== Буква '{letterText}' (индекс {letterIndex}) - OnTriggerEnter ===");
        Debug.Log($"Объект: {other.name}, тег: {other.tag}");
        Debug.Log($"isCollected: {isCollected}");
        
        // Проверяем, что это игрок
        if (other.CompareTag("Player") && !isCollected)
        {
            Debug.Log($"✓ Игрок касается буквы '{letterText}' (индекс {letterIndex})");
            CollectLetter();
        }
        else
        {
            Debug.Log($"✗ Не игрок или буква уже собрана");
        }
    }
    
    void CollectLetter()
    {
        Debug.Log($"=== LetterBehavior.CollectLetter для буквы '{letterText}' (индекс {letterIndex}) ===");
        
        if (isCollected) 
        {
            Debug.LogWarning("Буква уже собрана!");
            return;
        }
        
        isCollected = true;
        Debug.Log($"✓ Буква '{letterText}' помечена как собранная");
        
        // Проигрываем звук сбора
        if (collectSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(collectSound);
            Debug.Log("✓ Звук сбора проигран");
        }
        else
        {
            Debug.Log("✗ Звук сбора не проигран (отсутствует звук или AudioSource)");
        }
        
        // Уведомляем систему сбора
        if (collectorSystem != null)
        {
            Debug.Log($"✓ Уведомляем LetterCollectorSystem о сборе буквы с индексом {letterIndex}");
            collectorSystem.CollectLetter(letterIndex);
        }
        else
        {
            Debug.LogError("✗ LetterCollectorSystem не найден!");
        }
        
        // Визуальный эффект исчезновения
        Debug.Log("✓ Запускаем анимацию исчезновения");
        StartCoroutine(FadeOutAndDestroy());
    }
    
    System.Collections.IEnumerator FadeOutAndDestroy()
    {
        float fadeTime = 0.5f;
        float elapsedTime = 0f;
        
        // Проверяем, есть ли свойство _Color в материале
        bool hasColorProperty = glowMaterial.HasProperty("_Color");
        Color startColor = hasColorProperty ? glowMaterial.color : Color.white;
        Color endColor = new Color(startColor.r, startColor.g, startColor.b, 0f);
        
        while (elapsedTime < fadeTime)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / fadeTime;
            
            if (glowMaterial != null && hasColorProperty)
            {
                glowMaterial.color = Color.Lerp(startColor, endColor, t);
            }
            
            // Уменьшаем размер
            transform.localScale = Vector3.Lerp(Vector3.one, Vector3.zero, t);
            
            yield return null;
        }
        
        // Уничтожаем объект
        Destroy(gameObject);
    }
    
    void OnDestroy()
    {
        // Очищаем созданные материалы
        if (glowMaterial != null)
        {
            DestroyImmediate(glowMaterial);
        }
    }
}