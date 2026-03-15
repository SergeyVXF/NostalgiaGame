using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    [Header("Кнопки меню")]
    public Button newGameButton;
    public Button settingsButton;
    public Button loadGameButton;
    public Button exitButton;

    [Header("Настройки")]
    public string gameSceneName = "SampleScene";

    void Start()
    {
        Debug.Log("MainMenuController запущен");
        
        // Привязываем обработчики кнопок
        if (newGameButton != null)
            newGameButton.onClick.AddListener(StartNewGame);
        else
            Debug.LogError("newGameButton не назначена!");

        if (settingsButton != null)
            settingsButton.onClick.AddListener(OpenSettings);
        else
            Debug.LogError("settingsButton не назначена!");

        if (loadGameButton != null)
            loadGameButton.onClick.AddListener(LoadGame);
        else
            Debug.LogError("loadGameButton не назначена!");

        if (exitButton != null)
            exitButton.onClick.AddListener(ExitGame);
        else
            Debug.LogError("exitButton не назначена!");
    }

    public void StartNewGame()
    {
        Debug.Log("Запуск новой игры...");
        SceneManager.LoadScene(gameSceneName);
    }

    public void LoadGame()
    {
        Debug.Log("Загрузка игры...");
        // TODO: Реализовать загрузку сохранений
        Debug.Log("Функция загрузки будет добавлена позже");
    }

    public void OpenSettings()
    {
        Debug.Log("Открытие настроек...");
        // TODO: Реализовать меню настроек
        Debug.Log("Функция настроек будет добавлена позже");
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