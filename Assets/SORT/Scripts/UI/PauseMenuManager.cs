using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class PauseMenuManager : MonoBehaviour
{
    private bool isPaused = false;
    private bool menuCreated = false;
    private GameObject pauseMenuPanel;

    void Start()
    {
        CreatePauseMenu();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            TogglePause();
        }
    }

    void CreatePauseMenu()
    {
        if (menuCreated) return;
        menuCreated = true;

        // Создаем Canvas
        GameObject canvasObj = new GameObject("PauseCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();

        // Создаем EventSystem
        if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        // Убеждаемся, что у Canvas есть GraphicRaycaster
        GraphicRaycaster raycaster = canvasObj.GetComponent<GraphicRaycaster>();
        if (raycaster == null)
        {
            raycaster = canvasObj.AddComponent<GraphicRaycaster>();
        }

        // Создаем панель
        GameObject panelObj = new GameObject("PausePanel");
        panelObj.transform.SetParent(canvasObj.transform, false);
        Image panelImage = panelObj.AddComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.8f);
        RectTransform panelRect = panelObj.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        pauseMenuPanel = panelObj;

        // Создаем заголовок
        GameObject titleObj = new GameObject("PauseTitle");
        titleObj.transform.SetParent(panelObj.transform, false);
        Text titleText = titleObj.AddComponent<Text>();
        titleText.text = "ПАУЗА";
        titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        titleText.fontSize = 48;
        titleText.color = Color.white;
        titleText.alignment = TextAnchor.MiddleCenter;
        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.2f, 0.7f);
        titleRect.anchorMax = new Vector2(0.8f, 0.9f);
        titleRect.offsetMin = Vector2.zero;
        titleRect.offsetMax = Vector2.zero;

        // Создаем кнопки
        CreateButton(panelObj, "Продолжить", ResumeGame, new Vector2(0.3f, 0.5f), new Vector2(0.7f, 0.6f));
        CreateButton(panelObj, "Выйти из игры", ExitGame, new Vector2(0.3f, 0.35f), new Vector2(0.7f, 0.45f));

        pauseMenuPanel.SetActive(false);
    }

    void CreateButton(GameObject parent, string text, UnityEngine.Events.UnityAction onClick, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject buttonObj = new GameObject("Button_" + text);
        buttonObj.transform.SetParent(parent.transform, false);
        Button button = buttonObj.AddComponent<Button>();
        Image buttonImage = buttonObj.AddComponent<Image>();
        buttonImage.color = new Color(0.1f, 0.1f, 0.1f, 1f); // Темно-серый
        RectTransform buttonRect = buttonObj.GetComponent<RectTransform>();
        buttonRect.anchorMin = anchorMin;
        buttonRect.anchorMax = anchorMax;
        buttonRect.offsetMin = Vector2.zero;
        buttonRect.offsetMax = Vector2.zero;

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(buttonObj.transform, false);
        Text buttonText = textObj.AddComponent<Text>();
        buttonText.text = text;
        buttonText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        buttonText.fontSize = 24;
        buttonText.color = Color.white;
        buttonText.alignment = TextAnchor.MiddleCenter;
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        button.onClick.AddListener(onClick);

        // Настраиваем цвета кнопки с эффектом подсветки
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.1f, 0.1f, 0.1f, 1f); // Темно-серый
        colors.highlightedColor = new Color(0.6f, 0.6f, 0.6f, 1f); // Светло-серый при наведении
        colors.pressedColor = new Color(0.05f, 0.05f, 0.05f, 1f); // Очень темный при нажатии
        colors.selectedColor = new Color(0.6f, 0.6f, 0.6f, 1f); // Цвет при выборе
        colors.fadeDuration = 0.2f; // Скорость перехода между цветами
        button.colors = colors;

        // Добавляем компонент для обработки событий мыши
        var pointerHandler = buttonObj.AddComponent<ButtonPointerHandler>();
        pointerHandler.Initialize(buttonImage, text);
    }

    void TogglePause()
    {
        if (isPaused)
        {
            ResumeGame();
        }
        else
        {
            PauseGame();
        }
    }

    void PauseGame()
    {
        isPaused = true;
        Time.timeScale = 0f;
        pauseMenuPanel.SetActive(true);
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        Debug.Log("Игра поставлена на паузу");
    }

    void ResumeGame()
    {
        isPaused = false;
        Time.timeScale = 1f;
        pauseMenuPanel.SetActive(false);
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        Debug.Log("Игра возобновлена");
    }

    void ExitGame()
    {
        Debug.Log("Возврат в главное меню");
        Time.timeScale = 1f; // Возобновляем время
        SceneManager.LoadScene(0); // Загружаем главное меню (индекс 0)
    }
}