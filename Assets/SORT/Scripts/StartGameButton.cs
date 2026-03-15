using UnityEngine;
using UnityEngine.SceneManagement;

public class StartGameButton : MonoBehaviour
{
    public void StartGame()
    {
        Debug.Log("Кнопка СТАРТ нажата!");
        SceneManager.LoadScene(1); // Загружаем сцену по индексу
    }
}