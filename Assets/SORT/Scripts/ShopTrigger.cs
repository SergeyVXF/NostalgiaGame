using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ShopTrigger : MonoBehaviour
{
    [SerializeField] private string shopName = "Магазин";
    [SerializeField] private string shopDescription = "Нажмите E чтобы купить предмет";
    [SerializeField] private GameObject promptUI;
    [SerializeField] private TextMeshProUGUI promptText;
    [SerializeField] private ShopItem itemToBuy;
    
    private bool isInTrigger = false;
    
    private void Start()
    {
        CreatePromptUI();
    }
    
    private void CreatePromptUI()
    {
        if (promptUI == null)
        {
            // Создаем Canvas если его нет
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasObj = new GameObject("PromptCanvas");
                canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasObj.AddComponent<CanvasScaler>();
                canvasObj.AddComponent<GraphicRaycaster>();
            }
            
            // Создаем UI для подсказки
            promptUI = new GameObject("ShopPrompt");
            promptUI.transform.SetParent(canvas.transform, false);
            
            // Добавляем текст
            promptText = promptUI.AddComponent<TextMeshProUGUI>();
            promptText.text = itemToBuy != null ? 
                $"Нажмите E чтобы купить {itemToBuy.itemName}" : 
                "Нажмите E чтобы купить предмет";
            promptText.fontSize = 24;
            promptText.alignment = TextAlignmentOptions.Center;
            promptText.color = Color.white;
            
            // Позиционируем в центре экрана
            RectTransform rect = promptUI.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            
            promptUI.SetActive(false);
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isInTrigger = true;
            ShowPrompt();
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isInTrigger = false;
            HidePrompt();
        }
    }
    
    private void Update()
    {
        if (isInTrigger && Input.GetKeyDown(KeyCode.E))
        {
            if (itemToBuy != null && itemToBuy.itemName.ToLower() == "tempo")
            {
                if (TempoItem.Instance != null)
                {
                    TempoItem.Instance.AddTempo();
                    Debug.Log("[ShopTrigger] Tempo куплен и добавлен в инвентарь (НЕ используется автоматически)");
                }
                else
                {
                    Debug.LogError("[ShopTrigger] TempoItem.Instance не найден!");
                }
            }
            else
            {
                Debug.Log($"[ShopTrigger] Покупка предмета: {itemToBuy?.itemName ?? "null"} (логика не реализована)");
            }
        }
    }
    
    private void ShowPrompt()
    {
        if (promptUI != null)
        {
            promptUI.SetActive(true);
        }
    }
    
    private void HidePrompt()
    {
        if (promptUI != null)
        {
            promptUI.SetActive(false);
        }
    }
} 