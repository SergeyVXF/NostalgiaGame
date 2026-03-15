using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class MainMenuTest : MonoBehaviour
{
    void Start()
    {
        Debug.Log("MainMenuTest Start() работает!");
        
        // Создаем простую кнопку для теста
        CreateTestButton();
    }
    
    void CreateTestButton()
    {
        // Создаем Canvas
        GameObject canvasObj = new GameObject("TestCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();
        
        // Создаем EventSystem
        if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }
        
        // Создаем кнопку
        GameObject buttonObj = new GameObject("TestButton");
        buttonObj.transform.SetParent(canvasObj.transform, false);
        
        Image buttonImage = buttonObj.AddComponent<Image>();
        buttonImage.color = Color.red;
        
        Button button = buttonObj.AddComponent<Button>();
        button.onClick.AddListener(() => {
            Debug.Log("ТЕСТ КНОПКА нажата!");
            SceneManager.LoadScene("SampleScene");
        });
        
        RectTransform buttonRect = buttonObj.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.4f, 0.4f);
        buttonRect.anchorMax = new Vector2(0.6f, 0.6f);
        buttonRect.offsetMin = Vector2.zero;
        buttonRect.offsetMax = Vector2.zero;
        
        // Создаем текст
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(buttonObj.transform, false);
        
        Text textComponent = textObj.AddComponent<Text>();
        textComponent.text = "ТЕСТ КНОПКА";
        textComponent.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        textComponent.fontSize = 24;
        textComponent.color = Color.white;
        textComponent.alignment = TextAnchor.MiddleCenter;
        
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        
        Debug.Log("Тестовая кнопка создана!");
    }
}