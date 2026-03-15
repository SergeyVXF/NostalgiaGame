using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class SimpleMenuSetup : MonoBehaviour
{
    [Header("Menu Type")]
    public bool isMainMenu = true;
    
    [Header("Settings")]
    public string gameSceneName = "SampleScene";
    public string mainMenuSceneName = "MainMenu";
    public KeyCode menuKey = KeyCode.Escape;
    
    void Awake()
    {
        if (isMainMenu)
        {
            SetupMainMenu();
        }
        else
        {
            SetupGameMenu();
        }
    }
    
    void SetupMainMenu()
    {
        // Создаем Canvas если его нет
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("Canvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            
            // Добавляем CanvasScaler
            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            
            // Добавляем GraphicRaycaster
            canvasObj.AddComponent<GraphicRaycaster>();
            
            // Добавляем EventSystem если его нет
            if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                GameObject eventSystem = new GameObject("EventSystem");
                eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
                eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }
        }
        
        // Создаем UI элементы главного меню
        CreateMainMenuUI(canvas.gameObject);
    }
    
    void SetupGameMenu()
    {
        // Создаем Canvas если его нет
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("Canvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            
            // Добавляем CanvasScaler
            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            
            // Добавляем GraphicRaycaster
            canvasObj.AddComponent<GraphicRaycaster>();
            
            // Добавляем EventSystem если его нет
            if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                GameObject eventSystem = new GameObject("EventSystem");
                eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
                eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }
        }
        
        // Создаем UI элементы игрового меню
        CreateGameMenuUI(canvas.gameObject);
    }
    
    void CreateMainMenuUI(GameObject canvas)
    {
        // Создаем панель для меню
        GameObject menuPanel = new GameObject("MenuPanel");
        menuPanel.transform.SetParent(canvas.transform, false);
        
        // Добавляем Image компонент для фона
        Image panelImage = menuPanel.AddComponent<Image>();
        panelImage.color = new Color(0, 0, 0, 0.8f);
        
        // Настраиваем RectTransform
        RectTransform panelRect = menuPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        
        // Создаем вертикальную группу для кнопок
        GameObject buttonGroup = new GameObject("ButtonGroup");
        buttonGroup.transform.SetParent(menuPanel.transform, false);
        
        VerticalLayoutGroup layoutGroup = buttonGroup.AddComponent<VerticalLayoutGroup>();
        layoutGroup.spacing = 20;
        layoutGroup.childAlignment = TextAnchor.MiddleCenter;
        layoutGroup.childControlWidth = true;
        layoutGroup.childControlHeight = true;
        layoutGroup.childForceExpandWidth = false;
        layoutGroup.childForceExpandHeight = false;
        
        // Настраиваем RectTransform для группы кнопок
        RectTransform groupRect = buttonGroup.GetComponent<RectTransform>();
        groupRect.anchorMin = new Vector2(0.3f, 0.2f);
        groupRect.anchorMax = new Vector2(0.7f, 0.8f);
        groupRect.offsetMin = Vector2.zero;
        groupRect.offsetMax = Vector2.zero;
        
        // Создаем кнопки
        CreateButton(buttonGroup, "Новая игра", () => StartNewGame());
        CreateButton(buttonGroup, "Загрузить", () => LoadGame());
        CreateButton(buttonGroup, "Настройки", () => OpenSettings());
        CreateButton(buttonGroup, "Выход из игры", () => ExitGame());
    }
    
    void CreateGameMenuUI(GameObject canvas)
    {
        // Создаем панель для игрового меню
        GameObject menuPanel = new GameObject("GameMenuPanel");
        menuPanel.transform.SetParent(canvas.transform, false);
        
        // Добавляем Image компонент для фона
        Image panelImage = menuPanel.AddComponent<Image>();
        panelImage.color = new Color(0, 0, 0, 0.8f);
        
        // Настраиваем RectTransform
        RectTransform panelRect = menuPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        
        // Создаем вертикальную группу для кнопок
        GameObject buttonGroup = new GameObject("ButtonGroup");
        buttonGroup.transform.SetParent(menuPanel.transform, false);
        
        VerticalLayoutGroup layoutGroup = buttonGroup.AddComponent<VerticalLayoutGroup>();
        layoutGroup.spacing = 20;
        layoutGroup.childAlignment = TextAnchor.MiddleCenter;
        layoutGroup.childControlWidth = true;
        layoutGroup.childControlHeight = true;
        layoutGroup.childForceExpandWidth = false;
        layoutGroup.childForceExpandHeight = false;
        
        // Настраиваем RectTransform для группы кнопок
        RectTransform groupRect = buttonGroup.GetComponent<RectTransform>();
        groupRect.anchorMin = new Vector2(0.3f, 0.2f);
        groupRect.anchorMax = new Vector2(0.7f, 0.8f);
        groupRect.offsetMin = Vector2.zero;
        groupRect.offsetMax = Vector2.zero;
        
        // Создаем кнопки
        CreateButton(buttonGroup, "Продолжить", () => ResumeGame());
        CreateButton(buttonGroup, "Выйти в главное меню", () => ExitToMainMenu());
        CreateButton(buttonGroup, "Выйти из игры", () => ExitGame());
        
        // Добавляем GameMenuManager
        GameMenuManager menuManager = menuPanel.AddComponent<GameMenuManager>();
        menuManager.gameMenuPanel = menuPanel;
        menuManager.mainMenuSceneName = mainMenuSceneName;
        menuManager.menuKey = menuKey;
        
        // Настраиваем ссылки на кнопки
        Button[] buttons = buttonGroup.GetComponentsInChildren<Button>();
        if (buttons.Length >= 3)
        {
            menuManager.resumeButton = buttons[0];
            menuManager.exitToMenuButton = buttons[1];
            menuManager.exitGameButton = buttons[2];
        }
    }
    
    void CreateButton(GameObject parent, string text, System.Action onClick)
    {
        GameObject buttonObj = new GameObject(text + "Button");
        buttonObj.transform.SetParent(parent.transform, false);
        
        // Добавляем Image компонент
        Image buttonImage = buttonObj.AddComponent<Image>();
        buttonImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);
        
        // Добавляем Button компонент
        Button button = buttonObj.AddComponent<Button>();
        
        // Настраиваем цвета кнопки
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.2f, 0.2f, 0.2f, 1f);
        colors.highlightedColor = new Color(0.3f, 0.3f, 0.3f, 1f);
        colors.pressedColor = new Color(0.1f, 0.1f, 0.1f, 1f);
        button.colors = colors;
        
        // Настраиваем RectTransform
        RectTransform buttonRect = buttonObj.GetComponent<RectTransform>();
        buttonRect.sizeDelta = new Vector2(300, 60);
        
        // Создаем текст
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(buttonObj.transform, false);
        
        Text textComponent = textObj.AddComponent<Text>();
        textComponent.text = text;
        textComponent.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        textComponent.fontSize = 24;
        textComponent.color = Color.white;
        textComponent.alignment = TextAnchor.MiddleCenter;
        
        // Настраиваем RectTransform для текста
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        
        // Добавляем обработчик клика если передан
        if (onClick != null)
        {
            button.onClick.AddListener(() => onClick?.Invoke());
        }
    }
    
    void StartNewGame()
    {
        Debug.Log("Запуск новой игры...");
        SceneManager.LoadScene(gameSceneName);
    }
    
    void LoadGame()
    {
        Debug.Log("Загрузка игры...");
        Debug.Log("Функция загрузки будет добавлена позже");
    }
    
    void OpenSettings()
    {
        Debug.Log("Открытие настроек...");
        Debug.Log("Функция настроек будет добавлена позже");
    }
    
    void ResumeGame()
    {
        Debug.Log("Продолжение игры...");
        // Находим GameMenuManager и вызываем ResumeGame
        GameMenuManager menuManager = FindObjectOfType<GameMenuManager>();
        if (menuManager != null)
        {
            menuManager.ResumeGame();
        }
    }
    
    void ExitToMainMenu()
    {
        Debug.Log("Выход в главное меню...");
        SceneManager.LoadScene(mainMenuSceneName);
    }
    
    void ExitGame()
    {
        Debug.Log("Выход из игры...");
        
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }
} 