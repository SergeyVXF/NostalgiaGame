using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class WorkingButton : MonoBehaviour
{
    void Start()
    {
        // Создаем кнопку программно
        CreateWorkingButton();
    }

    void CreateWorkingButton()
    {
        // Создаем Canvas если его нет
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("Canvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
        }

        // Создаем EventSystem если его нет
        if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        // Создаем кнопку
        GameObject buttonObj = new GameObject("WorkingButton");
        buttonObj.transform.SetParent(canvas.transform, false);

        Image buttonImage = buttonObj.AddComponent<Image>();
        buttonImage.color = Color.green;

        Button button = buttonObj.AddComponent<Button>();
        button.onClick.AddListener(() => {
            Debug.Log("Рабочая кнопка нажата!");
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
        });

        RectTransform buttonRect = buttonObj.GetComponent<RectTransform>();
        buttonRect.sizeDelta = new Vector2(200, 50);
        buttonRect.anchoredPosition = new Vector2(0, 100);

        // Создаем текст
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(buttonObj.transform, false);

        Text textComponent = textObj.AddComponent<Text>();
        textComponent.text = "РАБОЧАЯ КНОПКА";
        textComponent.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        textComponent.fontSize = 16;
        textComponent.color = Color.black;
        textComponent.alignment = TextAnchor.MiddleCenter;

        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        Debug.Log("Рабочая кнопка создана!");
    }
}