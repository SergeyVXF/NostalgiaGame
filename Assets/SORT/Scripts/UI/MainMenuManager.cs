using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;

public class MainMenuManager : MonoBehaviour
{
    [Header("UI Elements")]
    public Button newGameButton;
    public Button loadGameButton;
    public Button settingsButton;
    public Button exitGameButton;
    
    [Header("Settings")]
    public string gameSceneName = "SampleScene"; // Имя сцены для новой игры
    
    [Header("Debug")]
    public bool showDebugLog = true;
    
    void Start()
    {
        if (showDebugLog)
            Debug.Log("[MainMenuManager] 🎮 Инициализация главного меню");
        
        // Ищем кнопки в сцене, если не назначены
        FindButtonsInScene();
        
        // Настраиваем кнопки
        SetupButtons();
    }
    
    void FindButtonsInScene()
    {
        if (showDebugLog)
            Debug.Log("[MainMenuManager] 🔍 Поиск кнопок в сцене...");
        
        // Ищем кнопки по именам, если они не назначены
        if (newGameButton == null)
            newGameButton = FindButtonByName("Start");
            
        if (loadGameButton == null)
            loadGameButton = FindButtonByName("Load");
            
        if (settingsButton == null)
            settingsButton = FindButtonByName("Settings");
            
        if (exitGameButton == null)
            exitGameButton = FindButtonByName("Exit");
    }
    
    Button FindButtonByName(string buttonName)
    {
        Button[] allButtons = FindObjectsOfType<Button>();
        
        foreach (Button button in allButtons)
        {
            if (button.name == buttonName)
            {
                if (showDebugLog)
                    Debug.Log($"[MainMenuManager] ✅ Найдена кнопка по имени: {buttonName}");
                return button;
            }
        }
        
        if (showDebugLog)
            Debug.LogWarning($"[MainMenuManager] ⚠️ Кнопка '{buttonName}' не найдена");
        return null;
    }
    
    void SetupButtons()
    {
        if (showDebugLog)
            Debug.Log("[MainMenuManager] 🔧 Настройка кнопок...");
        
        // Находим кнопки по именам из сцены
        if (newGameButton == null) newGameButton = FindButtonByName("Start");
        if (loadGameButton == null) loadGameButton = FindButtonByName("Load");
        if (settingsButton == null) settingsButton = FindButtonByName("Settings");
        if (exitGameButton == null) exitGameButton = FindButtonByName("Exit");
        
        // Настраиваем кнопки с проверками
        SetupButton(newGameButton, "Start", StartNewGame);
        SetupButton(loadGameButton, "Load", LoadGame);
        SetupButton(settingsButton, "Settings", OpenSettings);
        SetupButton(exitGameButton, "Exit", ExitGame);
        
        if (showDebugLog)
            Debug.Log("[MainMenuManager] ✅ Настройка кнопок завершена");
    }
    
    void SetupButton(Button button, string buttonName, System.Action action)
    {
        if (button == null)
        {
            Debug.LogError($"[MainMenuManager] ❌ Кнопка '{buttonName}' не найдена!");
            return;
        }
        
        // Очищаем старые обработчики
        button.onClick.RemoveAllListeners();
        
        // Добавляем новый обработчик
        button.onClick.AddListener(() => {
            if (showDebugLog)
                Debug.Log($"[MainMenuManager] 🖱️ Нажата кнопка: {buttonName}");
            action?.Invoke();
        });
        
        // Настраиваем эффект затемнения при наведении
        SetupButtonHoverEffect(button);
        
        if (showDebugLog)
            Debug.Log($"[MainMenuManager] ✅ Кнопка '{buttonName}' настроена");
    }
    
    void SetupButtonHoverEffect(Button button)
    {
        // Получаем текущие цвета кнопки
        ColorBlock colors = button.colors;
        
        // Делаем highlighted цвет темнее нормального
        Color normalColor = colors.normalColor;
        colors.highlightedColor = new Color(
            normalColor.r * 0.7f, 
            normalColor.g * 0.7f, 
            normalColor.b * 0.7f, 
            normalColor.a
        ); // На 30% темнее при наведении
        
        colors.pressedColor = new Color(
            normalColor.r * 0.5f, 
            normalColor.g * 0.5f, 
            normalColor.b * 0.5f, 
            normalColor.a
        ); // На 50% темнее при нажатии
        
        colors.fadeDuration = 0.15f; // Плавная анимация
        button.colors = colors;
        
        // Добавляем EventTrigger для дополнительных эффектов
        EventTrigger trigger = button.gameObject.GetComponent<EventTrigger>();
        if (trigger == null)
            trigger = button.gameObject.AddComponent<EventTrigger>();
        
        // Очищаем старые события
        trigger.triggers.Clear();
        
        // Событие при наведении мыши
        EventTrigger.Entry pointerEnter = new EventTrigger.Entry();
        pointerEnter.eventID = EventTriggerType.PointerEnter;
        pointerEnter.callback.AddListener((data) => {
            if (showDebugLog)
                Debug.Log($"[MainMenuManager] 🖱️ Наведение на кнопку: {button.name}");
        });
        trigger.triggers.Add(pointerEnter);
        
        // Событие при уходе мыши
        EventTrigger.Entry pointerExit = new EventTrigger.Entry();
        pointerExit.eventID = EventTriggerType.PointerExit;
        pointerExit.callback.AddListener((data) => {
            if (showDebugLog)
                Debug.Log($"[MainMenuManager] 🖱️ Уход с кнопки: {button.name}");
        });
        trigger.triggers.Add(pointerExit);
    }
    
    public void StartNewGame()
    {
        Debug.Log("[MainMenuManager] 🎮 Запуск новой игры...");
        
        // Проверяем, существует ли сцена
        if (string.IsNullOrEmpty(gameSceneName))
        {
            Debug.LogError("[MainMenuManager] ❌ Имя сцены не указано!");
            return;
        }
        
        // Пытаемся загрузить сцену
        try
        {
            Debug.Log($"[MainMenuManager] 📂 Загружаю сцену: {gameSceneName}");
            SceneManager.LoadScene(gameSceneName);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[MainMenuManager] ❌ Ошибка загрузки сцены '{gameSceneName}': {e.Message}");
            
            // Попробуем загрузить по индексу
            try
            {
                Debug.Log("[MainMenuManager] 🔄 Пробую загрузить сцену по индексу 1");
                SceneManager.LoadScene(1);
            }
            catch (System.Exception e2)
            {
                Debug.LogError($"[MainMenuManager] ❌ Ошибка загрузки сцены по индексу: {e2.Message}");
            }
        }
    }
    
    public void LoadGame()
    {
        Debug.Log("[MainMenuManager] 💾 Загрузка игры...");
        // TODO: Реализовать систему загрузки сохранений
        // Пока что просто показываем сообщение
        Debug.Log("[MainMenuManager] ⚠️ Функция загрузки будет добавлена позже");
    }
    
    public void OpenSettings()
    {
        Debug.Log("[MainMenuManager] ⚙️ Открытие настроек...");
        // TODO: Реализовать меню настроек
        Debug.Log("[MainMenuManager] ⚠️ Функция настроек будет добавлена позже");
    }
    
    public void ExitGame()
    {
        Debug.Log("[MainMenuManager] 🚪 Выход из игры...");
        
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }
    
    // Метод для проверки настроек кнопок (для вызова из редактора)
    [ContextMenu("Проверить настройки кнопок")]
    public void CheckButtonSettings()
    {
        Debug.Log("[MainMenuManager] 🔍 Проверка настроек кнопок:");
        Debug.Log($"  🎮 Новая игра: {(newGameButton != null ? "✅" : "❌")}");
        Debug.Log($"  💾 Загрузить: {(loadGameButton != null ? "✅" : "❌")}");
        Debug.Log($"  ⚙️ Настройки: {(settingsButton != null ? "✅" : "❌")}");
        Debug.Log($"  🚪 Выход: {(exitGameButton != null ? "✅" : "❌")}");
        Debug.Log($"  📂 Сцена игры: {gameSceneName}");
    }
    
    // Метод для тестирования кнопок (для вызова из редактора)
    [ContextMenu("Тест кнопок")]
    public void TestButtons()
    {
        Debug.Log("[MainMenuManager] 🧪 Тестирование кнопок...");
        
        if (newGameButton != null)
            Debug.Log("  🎮 Кнопка 'Новая игра' найдена");
        if (loadGameButton != null)
            Debug.Log("  💾 Кнопка 'Загрузить' найдена");
        if (settingsButton != null)
            Debug.Log("  ⚙️ Кнопка 'Настройки' найдена");
        if (exitGameButton != null)
            Debug.Log("  🚪 Кнопка 'Выход' найдена");
    }
} 