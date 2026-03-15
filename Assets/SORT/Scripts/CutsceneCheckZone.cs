using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class CutsceneCheckZone : MonoBehaviour
{
    [SerializeField] private float checkRadius = 5f;
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private float messageDuration = 2f;
    [SerializeField] private float messageRepeatInterval = 5f;
    
    private float messageTimer = 0f;
    private float repeatTimer = 0f;
    private bool isInZone = false;
    private GameObject player;
    private DEDQuest dedQuest;

    private void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player");
        dedQuest = FindObjectOfType<DEDQuest>();
        
        if (messageText == null)
        {
            CreateMessageUI();
        }
    }

    private void CreateMessageUI()
    {
        // Создаем Canvas если его нет
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("MessageCanvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
        }

        // Создаем UI для сообщения
        GameObject messageObj = new GameObject("CutsceneMessage");
        messageObj.transform.SetParent(canvas.transform, false);
        
        messageText = messageObj.AddComponent<TextMeshProUGUI>();
        messageText.text = "дай поесть";
        messageText.fontSize = 32;
        messageText.alignment = TextAlignmentOptions.Center;
        messageText.color = Color.white;
        
        // Позиционируем в центре экрана
        RectTransform rect = messageObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        
        messageObj.SetActive(false);
    }

    private void Update()
    {
        if (player == null || dedQuest == null) return;

        // Проверяем расстояние до игрока
        float distance = Vector3.Distance(transform.position, player.transform.position);
        bool wasInZone = isInZone;
        isInZone = distance <= checkRadius;
        
        // Если игрок только что вошел в зону
        if (isInZone && !wasInZone)
        {
            Debug.Log("[DEBUG] Игрок вошел в зону проверки");
            CheckAndShowMessage();
            repeatTimer = messageRepeatInterval; // Сбрасываем таймер повторения
        }
        
        // Если игрок в зоне, периодически показываем сообщение
        if (isInZone)
        {
            repeatTimer -= Time.deltaTime;
            if (repeatTimer <= 0)
            {
                CheckAndShowMessage();
                repeatTimer = messageRepeatInterval; // Сбрасываем таймер
            }
        }

        // Обновляем таймер скрытия сообщения
        if (messageTimer > 0)
        {
            messageTimer -= Time.deltaTime;
            if (messageTimer <= 0)
            {
                HideMessage();
            }
        }
    }

    private void CheckAndShowMessage()
    {
        // Проверяем наличие предмета через DEDQuest
        if (!dedQuest.HasRequiredItem())
        {
            Debug.Log("[DEBUG] Автоматическая проверка: у игрока нет предмета tempo!");
            Debug.Log($"[DEBUG] Статус предмета tempo: {dedQuest.HasRequiredItem()}");
            ShowMessage("дай поесть");
        }
        else
        {
            Debug.Log("[DEBUG] Автоматическая проверка: у игрока есть предмет tempo");
        }
    }

    private void ShowMessage(string message)
    {
        if (messageText != null)
        {
            messageText.text = message;
            messageText.gameObject.SetActive(true);
            messageTimer = messageDuration;
        }
    }

    private void HideMessage()
    {
        if (messageText != null)
        {
            messageText.gameObject.SetActive(false);
        }
    }

    // Визуализация зоны в редакторе
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, checkRadius);
    }
} 