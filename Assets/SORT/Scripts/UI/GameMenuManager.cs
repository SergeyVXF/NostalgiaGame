using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class GameMenuManager : MonoBehaviour
{
    [Header("UI Elements")]
    public GameObject gameMenuPanel;
    public Button resumeButton;
    public Button exitToMenuButton;
    public Button exitGameButton;
    
    [Header("Settings")]
    public string mainMenuSceneName = "MainMenu"; // Имя сцены главного меню
    public KeyCode menuKey = KeyCode.Escape;
    
    private bool isMenuOpen = false;
    
    void Start()
    {
        // Настраиваем кнопки
        if (resumeButton != null)
            resumeButton.onClick.AddListener(ResumeGame);
            
        if (exitToMenuButton != null)
            exitToMenuButton.onClick.AddListener(ExitToMainMenu);
            
        if (exitGameButton != null)
            exitGameButton.onClick.AddListener(ExitGame);
            
        // Скрываем меню при запуске
        if (gameMenuPanel != null)
            gameMenuPanel.SetActive(false);
    }
    
    void Update()
    {
        // Проверяем нажатие клавиши ESC
        if (Input.GetKeyDown(menuKey))
        {
            ToggleMenu();
        }
    }
    
    public void ToggleMenu()
    {
        isMenuOpen = !isMenuOpen;
        
        if (gameMenuPanel != null)
        {
            gameMenuPanel.SetActive(isMenuOpen);
        }
        
        // Останавливаем/возобновляем время игры
        Time.timeScale = isMenuOpen ? 0f : 1f;
        
        // Показываем/скрываем курсор мыши
        Cursor.visible = isMenuOpen;
        Cursor.lockState = isMenuOpen ? CursorLockMode.None : CursorLockMode.Locked;
    }
    
    public void ResumeGame()
    {
        ToggleMenu();
    }
    
    public void ExitToMainMenu()
    {
        Debug.Log("Выход в главное меню...");
        
        // Возобновляем время игры
        Time.timeScale = 1f;
        
        // Скрываем курсор
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        
        // Загружаем главное меню
        SceneManager.LoadScene(mainMenuSceneName);
    }
    
    public void ExitGame()
    {
        Debug.Log("Выход из игры...");
        
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }
} 