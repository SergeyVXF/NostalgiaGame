using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;

public class MainMenuScript : MonoBehaviour
{
    [Header("Кнопки меню")]
    [Tooltip("Кнопка новой игры")]
    public Button newGameButton;
    
    [Tooltip("Кнопка загрузки")]
    public Button loadGameButton;
    
    [Tooltip("Кнопка настроек")]
    public Button settingsButton;
    
    [Tooltip("Кнопка выхода")]
    public Button exitButton;
    
    [Header("Настройки")]
    [Tooltip("Имя сцены для новой игры")]
    public string gameSceneName = "SampleScene";
    
    [Tooltip("Создавать UI автоматически")]
    public bool createUIAutomatically = true;
    
    [Header("Эффекты")]
    [Tooltip("Простой объект-эффект для кнопки Start")]
    public GameObject simpleEffectPrefab;
    
    [Tooltip("Позиция спавна эффекта")]
    public Vector3 effectSpawnPosition = new Vector3(-104.7f, 2.6f, 111.0546f);
    
    [Tooltip("Звук при нажатии кнопки Start")]
    public AudioClip startButtonSound;
    
    [Tooltip("Задержка перед загрузкой сцены (секунды)")]
    [Range(0f, 5f)]
    public float sceneLoadDelay = 1f;
    
    [Header("Loading Screen")]
    [Tooltip("Показывать изображение с камеры во время загрузки")]
    public bool showCameraOnLoadingScreen = true;
    
    void Start()
    {
        // Если слоты пустые и включено автосоздание - создаем UI
        if (HasEmptySlots() && createUIAutomatically)
        {
            CreateMenu();
        }
        else
        {
            // Используем назначенные кнопки
            SetupExistingButtons();
        }
    }
    
    bool HasEmptySlots()
    {
        return newGameButton == null || loadGameButton == null || settingsButton == null || exitButton == null;
    }
    
    void SetupExistingButtons()
    {
        Debug.Log("[MainMenuScript] 🔧 Настройка существующих кнопок...");
        
        // КРИТИЧНО: Создаем EventSystem если его нет!
        CreateEventSystemIfNeeded();
        
        if (newGameButton != null)
        {
            newGameButton.onClick.RemoveAllListeners();
            newGameButton.onClick.AddListener(StartNewGame);
            SetupButtonHover(newGameButton);
            Debug.Log("[MainMenuScript] ✅ Кнопка новой игры настроена");
        }
        
        if (loadGameButton != null)
        {
            loadGameButton.onClick.RemoveAllListeners();
            loadGameButton.onClick.AddListener(LoadGame);
            SetupButtonHover(loadGameButton);
            Debug.Log("[MainMenuScript] ✅ Кнопка загрузки настроена");
        }
        
        if (settingsButton != null)
        {
            settingsButton.onClick.RemoveAllListeners();
            settingsButton.onClick.AddListener(OpenSettings);
            SetupButtonHover(settingsButton);
            Debug.Log("[MainMenuScript] ✅ Кнопка настроек настроена");
        }
        
        if (exitButton != null)
        {
            exitButton.onClick.RemoveAllListeners();
            exitButton.onClick.AddListener(ExitGame);
            SetupButtonHover(exitButton);
            Debug.Log("[MainMenuScript] ✅ Кнопка выхода настроена");
        }
    }
    
    void CreateEventSystemIfNeeded()
    {
        // Проверяем есть ли EventSystem
        var eventSystem = FindObjectOfType<UnityEngine.EventSystems.EventSystem>();
        if (eventSystem == null)
        {
            Debug.Log("[MainMenuScript] 🚨 EventSystem НЕ НАЙДЕН! Создаю...");
            
            GameObject esObj = new GameObject("EventSystem");
            esObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            
            Debug.Log("[MainMenuScript] ✅ EventSystem создан!");
        }
        else
        {
            Debug.Log("[MainMenuScript] ✅ EventSystem найден");
        }
    }
    
    void SetupButtonHover(Button button)
    {
        // Ищем ТЕКСТ в кнопке
        Text buttonText = button.GetComponentInChildren<Text>();
        
        if (buttonText != null)
        {
            // Выключаем стандартные эффекты кнопки
            button.transition = Selectable.Transition.None;
            
            // Добавляем EventTrigger для обработки наведения
            EventTrigger trigger = button.gameObject.GetComponent<EventTrigger>();
            if (trigger == null)
                trigger = button.gameObject.AddComponent<EventTrigger>();
            
            trigger.triggers.Clear();
            
            // Запоминаем изначальный цвет
            Color originalColor = buttonText.color;
            
            // При наведении - alpha = 0%
            EventTrigger.Entry pointerEnter = new EventTrigger.Entry();
            pointerEnter.eventID = EventTriggerType.PointerEnter;
            pointerEnter.callback.AddListener((data) => {
                Color hoverColor = originalColor;
                hoverColor.a = 0f; // Alpha = 0%
                buttonText.color = hoverColor;
                Debug.Log($"[MainMenuScript] 👻 Кнопка {button.name} стала прозрачной");
            });
            trigger.triggers.Add(pointerEnter);
            
            // При уходе - alpha = 100%
            EventTrigger.Entry pointerExit = new EventTrigger.Entry();
            pointerExit.eventID = EventTriggerType.PointerExit;
            pointerExit.callback.AddListener((data) => {
                buttonText.color = originalColor; // Возвращаем исходный цвет
                Debug.Log($"[MainMenuScript] 🔄 Кнопка {button.name} стала видимой");
            });
            trigger.triggers.Add(pointerExit);
            
            Debug.Log($"[MainMenuScript] ✅ Настроен alpha-эффект для кнопки: {button.name}");
        }
        else
        {
            Debug.LogWarning($"[MainMenuScript] ⚠️ Текст не найден в кнопке: {button.name}");
        }
    }

    void CreateMenu()
    {
        // Создаем Canvas
        GameObject canvasObj = new GameObject("Canvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();

        // Создаем EventSystem
        GameObject eventSystem = new GameObject("EventSystem");
        eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
        eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

        // Создаем панель
        GameObject panel = new GameObject("Panel");
        panel.transform.SetParent(canvasObj.transform, false);
        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0, 0, 0, 0.5f);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        // Создаем кнопки
        CreateButton(panel, "Новая игра", StartNewGame);
        CreateButton(panel, "Настройки", OpenSettings);
        CreateButton(panel, "Загрузить игру", LoadGame);
        CreateButton(panel, "Выход", ExitGame);
    }

    void CreateButton(GameObject parent, string text, System.Action onClick)
    {
        GameObject buttonObj = new GameObject(text + "Button");
        buttonObj.transform.SetParent(parent.transform, false);

        Image buttonImage = buttonObj.AddComponent<Image>();
        buttonImage.color = new Color(0.8f, 0.8f, 0.8f, 0.9f);

        Button button = buttonObj.AddComponent<Button>();
        button.onClick.AddListener(() => onClick?.Invoke());

        RectTransform buttonRect = buttonObj.GetComponent<RectTransform>();
        buttonRect.sizeDelta = new Vector2(300, 60);
        buttonRect.anchoredPosition = new Vector2(0, 200 - (parent.transform.childCount - 1) * 80);

        // Текст
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(buttonObj.transform, false);

        Text textComponent = textObj.AddComponent<Text>();
        textComponent.text = text;
        textComponent.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        textComponent.fontSize = 20;
        textComponent.color = Color.black;
        textComponent.alignment = TextAnchor.MiddleCenter;

        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        
        // Настраиваем alpha-эффект для автоматически созданных кнопок
        button.transition = Selectable.Transition.None;
        
        EventTrigger trigger = buttonObj.AddComponent<EventTrigger>();
        Color originalTextColor = textComponent.color;
        
        // При наведении - alpha = 0%
        EventTrigger.Entry pointerEnter = new EventTrigger.Entry();
        pointerEnter.eventID = EventTriggerType.PointerEnter;
        pointerEnter.callback.AddListener((data) => {
            Color hoverColor = originalTextColor;
            hoverColor.a = 0f; // Alpha = 0%
            textComponent.color = hoverColor;
        });
        trigger.triggers.Add(pointerEnter);
        
        // При уходе - alpha = 100%
        EventTrigger.Entry pointerExit = new EventTrigger.Entry();
        pointerExit.eventID = EventTriggerType.PointerExit;
        pointerExit.callback.AddListener((data) => {
            textComponent.color = originalTextColor; // Возвращаем исходный цвет
        });
        trigger.triggers.Add(pointerExit);
    }
    
    // Функции для кнопок
    void StartNewGame()
    {
        Debug.Log("[MainMenuScript] 🎮 КНОПКА НОВАЯ ИГРА НАЖАТА!");
        
        // ВОСПРОИЗВОДИМ ЗВУК
        if (startButtonSound != null)
        {
            // Создаем временный AudioSource для воспроизведения звука
            GameObject soundObject = new GameObject("StartButtonSound");
            AudioSource audioSource = soundObject.AddComponent<AudioSource>();
            
            audioSource.clip = startButtonSound;
            audioSource.volume = 1f;
            audioSource.Play();
            
            // Защищаем от уничтожения при смене сцены
            DontDestroyOnLoad(soundObject);
            
            // Уничтожаем после воспроизведения
            Destroy(soundObject, startButtonSound.length + 1f);
            
            Debug.Log($"[MainMenuScript] 🔊 Воспроизвожу звук кнопки Start (длительность: {startButtonSound.length}с)");
        }
        else
        {
            Debug.LogWarning("[MainMenuScript] ⚠️ Звук для кнопки Start не назначен!");
        }
        
        // ПРОСТОЙ ЭФФЕКТ - создаем обычный объект!
        if (simpleEffectPrefab != null)
        {
            Debug.Log("[MainMenuScript] 🎆 СОЗДАЮ ПРОСТОЙ ЭФФЕКТ!");
            
            // Создаем объект на нужной позиции
            GameObject effect = Instantiate(simpleEffectPrefab, effectSpawnPosition, Quaternion.identity);
            
            // Защищаем от уничтожения при смене сцены
            DontDestroyOnLoad(effect);
            
            Debug.Log($"[MainMenuScript] ✅ Эффект создан на позиции: {effectSpawnPosition}");
            Debug.Log($"[MainMenuScript] 🛡️ Эффект защищен от уничтожения при смене сцены");
            
            // Уничтожаем через 8 секунд
            Destroy(effect, 8f);
            Debug.Log("[MainMenuScript] 🗑️ Эффект будет уничтожен через 8 секунд");
        }
        else
        {
            Debug.LogWarning("[MainMenuScript] ⚠️ Префаб эффекта не назначен!");
        }
        
        // Запускаем корутину с задержкой
        StartCoroutine(LoadSceneWithDelay());
    }
    
    System.Collections.IEnumerator LoadSceneWithDelay()
    {
        Debug.Log($"[MainMenuScript] ⏰ Ожидание {sceneLoadDelay} секунд перед загрузкой сцены...");
        
        // Ждем указанное время
        yield return new WaitForSeconds(sceneLoadDelay);
        
        Debug.Log($"[MainMenuScript] 📂 Имя сцены: '{gameSceneName}'");
        
        try
        {
            if (!string.IsNullOrEmpty(gameSceneName))
            {
                Debug.Log($"[MainMenuScript] 🚀 Загружаю сцену: {gameSceneName}");
                SceneManager.LoadScene(gameSceneName);
            }
            else
            {
                Debug.Log("[MainMenuScript] 🚀 Загружаю сцену по индексу 1");
                SceneManager.LoadScene(1);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[MainMenuScript] ❌ ОШИБКА загрузки сцены: {e.Message}");
            
            // Показываем все доступные сцены
            Debug.Log("[MainMenuScript] 📋 Доступные сцены:");
            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                string scenePath = SceneUtility.GetScenePathByBuildIndex(i);
                Debug.Log($"  {i}: {scenePath}");
            }
        }
    }
    
    void LoadGame()
    {
        Debug.Log("[MainMenuScript] 💾 КНОПКА ЗАГРУЗИТЬ НАЖАТА!");
        // TODO: Добавить логику загрузки
    }
    
    void OpenSettings()
    {
        Debug.Log("[MainMenuScript] ⚙️ КНОПКА НАСТРОЙКИ НАЖАТА!");
        // TODO: Добавить логику настроек
    }
    
    void ExitGame()
    {
        Debug.Log("[MainMenuScript] 🚪 КНОПКА ВЫХОД НАЖАТА!");
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }
}