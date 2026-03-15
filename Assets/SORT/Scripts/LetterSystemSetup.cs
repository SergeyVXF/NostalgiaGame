using UnityEngine;
using TMPro;

public class LetterSystemSetup : MonoBehaviour
{
    [Header("Быстрая настройка")]
    public bool setupOnStart = true;
    public bool createSimpleLetters = true;
    public bool createUI = true;
    
    [Header("Настройки букв")]
    public Material letterMaterial;
    public Color letterColor = Color.yellow;
    public float letterSize = 2f;
    
    [Header("Эффекты")]
    public GameObject customParticlePrefab; // Кастомный ParticleSystem префаб
    
    [Header("Настройки размещения")]
    public float spawnRadius = 30f;
    public float minDistance = 8f;
    public float heightOffset = 2f;
    
    [Header("UI настройки")]
    public Canvas uiCanvas;
    public Vector2 uiPosition = new Vector2(50, 50);
    
    private LetterCollectorSystem collectorSystem;
    private LetterSpawnPointCreator spawnCreator;
    
    void Start()
    {
        if (setupOnStart)
        {
            SetupLetterSystem();
        }
    }
    
    [ContextMenu("Настроить систему букв")]
    public void SetupLetterSystem()
    {
        // Создаем основной объект системы
        GameObject systemObject = new GameObject("LetterSystem");
        systemObject.transform.position = Vector3.zero;
        
        // Добавляем компоненты системы
        collectorSystem = systemObject.AddComponent<LetterCollectorSystem>();
        spawnCreator = systemObject.AddComponent<LetterSpawnPointCreator>();
        
        // Настраиваем создатель точек спавна
        spawnCreator.spawnRadius = spawnRadius;
        spawnCreator.minDistance = minDistance;
        spawnCreator.heightOffset = heightOffset;
        spawnCreator.createSpawnPointsOnStart = false; // Создадим вручную
        
        // Создаем точки спавна
        Transform[] spawnPoints = spawnCreator.CreateSpawnPoints();
        
        // Назначаем точки спавна в систему
        collectorSystem.spawnPoints = spawnPoints;
        
        // Настраиваем слоты букв в системе
        SetupLetterSlots();
        
        // Настраиваем эффекты
        collectorSystem.useParticleEffects = true; // Включаем ParticleSystem эффекты
        collectorSystem.customParticlePrefab = customParticlePrefab; // Передаем кастомный префаб
        
        // Создаем UI если нужно
        if (createUI)
        {
            CreateUI();
        }
        
        Debug.Log("Система букв успешно настроена!");
    }
    
    void SetupLetterMaterials(GameObject[] letters)
    {
        foreach (GameObject letter in letters)
        {
            if (letter != null)
            {
                // Находим все рендереры в букве
                Renderer[] renderers = letter.GetComponentsInChildren<Renderer>();
                
                foreach (Renderer renderer in renderers)
                {
                    if (letterMaterial != null)
                    {
                        renderer.material = letterMaterial;
                    }
                    
                    // Настраиваем свечение
                    Material glowMat = new Material(renderer.material);
                    
                    // Проверяем, поддерживает ли материал эмиссию
                    if (glowMat.HasProperty("_EmissionColor"))
                    {
                        glowMat.EnableKeyword("_EMISSION");
                        glowMat.SetColor("_EmissionColor", letterColor * 1.5f);
                    }
                    
                    renderer.material = glowMat;
                }
            }
        }
    }
    
    void SetupLetterSlots()
    {
        // Настраиваем слоты букв в LetterCollectorSystem
        if (collectorSystem != null)
        {
            // Создаем слоты по умолчанию
            collectorSystem.letterSlots = new LetterCollectorSystem.LetterSlot[]
            {
                new LetterCollectorSystem.LetterSlot { letter = "A", prefab = null },
                new LetterCollectorSystem.LetterSlot { letter = "B", prefab = null },
                new LetterCollectorSystem.LetterSlot { letter = "C", prefab = null },
                new LetterCollectorSystem.LetterSlot { letter = "D", prefab = null },
                new LetterCollectorSystem.LetterSlot { letter = "E", prefab = null },
                new LetterCollectorSystem.LetterSlot { letter = "F", prefab = null },
                new LetterCollectorSystem.LetterSlot { letter = "G", prefab = null }
            };
            
            Debug.Log("Слоты букв настроены в LetterCollectorSystem");
        }
    }
    
    void CreateUI()
    {
        // Создаем Canvas если его нет
        if (uiCanvas == null)
        {
            GameObject canvasObject = new GameObject("LetterSystemCanvas");
            uiCanvas = canvasObject.AddComponent<Canvas>();
            uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<UnityEngine.UI.CanvasScaler>();
            canvasObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        }
        
        // Создаем панель для UI
        GameObject panelObject = new GameObject("LetterDisplayPanel");
        panelObject.transform.SetParent(uiCanvas.transform, false);
        
        UnityEngine.UI.Image panelImage = panelObject.AddComponent<UnityEngine.UI.Image>();
        panelImage.color = new Color(0, 0, 0, 0.8f);
        
        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0, 1);
        panelRect.anchorMax = new Vector2(0, 1);
        panelRect.pivot = new Vector2(0, 1);
        panelRect.anchoredPosition = uiPosition;
        panelRect.sizeDelta = new Vector2(300, 120);
        
        // Создаем текст для отображения букв
        GameObject letterTextObject = new GameObject("LetterDisplayText");
        letterTextObject.transform.SetParent(panelObject.transform, false);
        
        TMPro.TextMeshProUGUI letterDisplayText = letterTextObject.AddComponent<TMPro.TextMeshProUGUI>();
        letterDisplayText.text = "Собранные буквы: ? ? ? ? ? ? ?";
        letterDisplayText.color = Color.white;
        letterDisplayText.fontSize = 18;
        letterDisplayText.alignment = TextAlignmentOptions.Center;
        
        RectTransform letterTextRect = letterTextObject.GetComponent<RectTransform>();
        letterTextRect.anchorMin = new Vector2(0, 0.5f);
        letterTextRect.anchorMax = new Vector2(1, 1);
        letterTextRect.offsetMin = new Vector2(10, 10);
        letterTextRect.offsetMax = new Vector2(-10, -5);
        
        // Создаем текст счетчика
        GameObject counterTextObject = new GameObject("LetterCounterText");
        counterTextObject.transform.SetParent(panelObject.transform, false);
        
        TMPro.TextMeshProUGUI counterText = counterTextObject.AddComponent<TMPro.TextMeshProUGUI>();
        counterText.text = "Собрано: 0/7";
        counterText.color = Color.white;
        counterText.fontSize = 14;
        counterText.alignment = TextAlignmentOptions.Center;
        
        RectTransform counterTextRect = counterTextObject.GetComponent<RectTransform>();
        counterTextRect.anchorMin = new Vector2(0, 0);
        counterTextRect.anchorMax = new Vector2(1, 0.5f);
        counterTextRect.offsetMin = new Vector2(10, 5);
        counterTextRect.offsetMax = new Vector2(-10, -10);
        
        // Создаем прогресс бар
        GameObject progressBarObject = new GameObject("ProgressBar");
        progressBarObject.transform.SetParent(panelObject.transform, false);
        
        UnityEngine.UI.Image progressBar = progressBarObject.AddComponent<UnityEngine.UI.Image>();
        progressBar.color = Color.green;
        progressBar.type = UnityEngine.UI.Image.Type.Filled;
        progressBar.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
        progressBar.fillAmount = 0f;
        
        RectTransform progressBarRect = progressBarObject.GetComponent<RectTransform>();
        progressBarRect.anchorMin = new Vector2(0, 0);
        progressBarRect.anchorMax = new Vector2(1, 0.1f);
        progressBarRect.offsetMin = new Vector2(10, 0);
        progressBarRect.offsetMax = new Vector2(-10, 0);
        
        // Добавляем компонент UI для отображения букв
        LetterDisplayUI displayUI = panelObject.AddComponent<LetterDisplayUI>();
        displayUI.letterDisplayText = letterDisplayText;
        displayUI.counterText = counterText;
        displayUI.progressBar = progressBar;
        
        // Назначаем текст в систему (для обратной совместимости)
        collectorSystem.letterCounterText = counterText;
    }
    
    [ContextMenu("Очистить систему")]
    public void ClearSystem()
    {
        // Находим и уничтожаем все объекты системы
        LetterCollectorSystem[] collectors = FindObjectsOfType<LetterCollectorSystem>();
        foreach (LetterCollectorSystem collector in collectors)
        {
            if (collector != null)
            {
                DestroyImmediate(collector.gameObject);
            }
        }
        
        // Уничтожаем UI
        LetterCounterUI[] counterUIs = FindObjectsOfType<LetterCounterUI>();
        foreach (LetterCounterUI counterUI in counterUIs)
        {
            if (counterUI != null)
            {
                DestroyImmediate(counterUI.gameObject);
            }
        }
        
        Debug.Log("Система букв очищена!");
    }
    
    [ContextMenu("Проверить настройки")]
    public void CheckSetup()
    {
        // Проверяем наличие игрока
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            Debug.LogWarning("Игрок с тегом 'Player' не найден! Система может не работать корректно.");
        }
        else
        {
            Debug.Log("Игрок найден: " + player.name);
        }
        
        // Проверяем систему букв
        LetterCollectorSystem collector = FindObjectOfType<LetterCollectorSystem>();
        if (collector == null)
        {
            Debug.LogWarning("LetterCollectorSystem не найден!");
        }
        else
        {
            Debug.Log("Система букв найдена: " + collector.name);
            Debug.Log("Собранных букв: " + collector.GetCollectedLetters() + "/" + collector.GetTotalLetters());
        }
        
        // Проверяем UI
        LetterCounterUI counterUI = FindObjectOfType<LetterCounterUI>();
        if (counterUI == null)
        {
            Debug.LogWarning("LetterCounterUI не найден!");
        }
        else
        {
            Debug.Log("UI счетчика найден: " + counterUI.name);
        }
    }
} 