using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    void Start()
    {
        Debug.Log("MainMenu запущен!");
        SetupButtons();
    }

    void SetupButtons()
    {
        // Находим кнопки
        Button newGameBtn = GameObject.Find("NewGameButton")?.GetComponent<Button>();
        Button settingsBtn = GameObject.Find("LoadGameButton")?.GetComponent<Button>();
        Button loadGameBtn = GameObject.Find("SettingsButton")?.GetComponent<Button>();
        Button exitBtn = GameObject.Find("ExitButton")?.GetComponent<Button>();

        // Настраиваем кнопки
        if (newGameBtn != null)
        {
            newGameBtn.onClick.RemoveAllListeners();
            newGameBtn.onClick.AddListener(StartNewGame);
            Debug.Log("Кнопка 'Новая игра' настроена");
        }

        if (settingsBtn != null)
        {
            settingsBtn.onClick.RemoveAllListeners();
            settingsBtn.onClick.AddListener(OpenSettings);
            Debug.Log("Кнопка 'Настройки' настроена");
        }

        if (loadGameBtn != null)
        {
            loadGameBtn.onClick.RemoveAllListeners();
            loadGameBtn.onClick.AddListener(LoadGame);
            Debug.Log("Кнопка 'Загрузить игру' настроена");
        }

        if (exitBtn != null)
        {
            exitBtn.onClick.RemoveAllListeners();
            exitBtn.onClick.AddListener(ExitGame);
            Debug.Log("Кнопка 'Выход' настроена");
        }
    }

    public void StartNewGame()
    {
        Debug.Log("Запуск новой игры!");
        // Попробуем разные варианты имени сцены
        try
        {
            SceneManager.LoadScene("SampleScene");
        }
        catch
        {
            try
            {
                SceneManager.LoadScene(1); // Загружаем по индексу
            }
            catch
            {
                Debug.LogError("Не удалось загрузить сцену! Проверьте Build Settings.");
            }
        }
    }

    public void OpenSettings()
    {
        Debug.Log("Открытие настроек!");
    }

    public void LoadGame()
    {
        Debug.Log("Загрузка игры!");
    }

    public void ExitGame()
    {
        Debug.Log("Выход из игры!");
        
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }
}