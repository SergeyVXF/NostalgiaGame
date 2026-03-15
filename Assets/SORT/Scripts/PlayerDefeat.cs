using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class PlayerDefeat : MonoBehaviour
{
    [SerializeField] private float fadeSpeed = 1f;
    [SerializeField] private float delayBeforeReset = 2f;
    [SerializeField] private Vector3 playerStartPosition;
    [SerializeField] private Vector3 playerStartRotation;
    
    private Canvas defeatCanvas;
    private Image fadeImage;
    private TextMeshProUGUI defeatText;
    private GameObject player;
    private GameObject finishLine;
    private AIOpponent aiOpponent;
    private bool isDefeatActive = false;
    
    private void Start()
    {
        Debug.Log("PlayerDefeat: Инициализация");
        
        // Создаем Canvas для поражения
        CreateDefeatCanvas();
        
        // Находим игрока
        player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            Debug.LogError("PlayerDefeat: Игрок не найден! Убедитесь, что у игрока установлен тег 'Player'");
            return;
        }
        
        // Находим финишную линию
        finishLine = GameObject.FindGameObjectWithTag("FinishLine");
        if (finishLine == null)
        {
            Debug.LogWarning("PlayerDefeat: Финишная линия не найдена. Она будет найдена позже.");
        }
        
        // Находим AI
        aiOpponent = FindObjectOfType<AIOpponent>();
        if (aiOpponent == null)
        {
            Debug.LogWarning("PlayerDefeat: AI не найден. Он будет найден позже.");
        }
        
        // Сохраняем начальную позицию и поворот
        playerStartPosition = player.transform.position;
        playerStartRotation = player.transform.eulerAngles;
        Debug.Log($"PlayerDefeat: Сохранена начальная позиция игрока: {playerStartPosition}");
        
        // Подписываемся на событие победы AI
        AIOpponent.OnAIVictory += HandleDefeat;
        Debug.Log("PlayerDefeat: Подписался на событие победы AI");
    }
    
    private void Update()
    {
        // Пытаемся найти финишную линию и AI, если они еще не найдены
        if (finishLine == null)
        {
            finishLine = GameObject.FindGameObjectWithTag("FinishLine");
            if (finishLine != null)
            {
                Debug.Log("PlayerDefeat: Финишная линия найдена");
            }
        }
        
        if (aiOpponent == null)
        {
            aiOpponent = FindObjectOfType<AIOpponent>();
            if (aiOpponent != null)
            {
                Debug.Log("PlayerDefeat: AI найден");
            }
        }
    }
    
    private void OnDestroy()
    {
        AIOpponent.OnAIVictory -= HandleDefeat;
        Debug.Log("PlayerDefeat: Отписался от события победы AI");
    }
    
    private void CreateDefeatCanvas()
    {
        Debug.Log("PlayerDefeat: Создание Canvas для поражения");
        
        // Создаем Canvas
        GameObject canvasObj = new GameObject("DefeatCanvas");
        defeatCanvas = canvasObj.AddComponent<Canvas>();
        defeatCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();
        
        // Создаем затемнение
        GameObject fadeObj = new GameObject("FadeImage");
        fadeObj.transform.SetParent(canvasObj.transform, false);
        fadeImage = fadeObj.AddComponent<Image>();
        fadeImage.color = new Color(0, 0, 0, 0);
        fadeImage.rectTransform.anchorMin = Vector2.zero;
        fadeImage.rectTransform.anchorMax = Vector2.one;
        fadeImage.rectTransform.sizeDelta = Vector2.zero;
        
        // Создаем текст поражения
        GameObject textObj = new GameObject("DefeatText");
        textObj.transform.SetParent(canvasObj.transform, false);
        defeatText = textObj.AddComponent<TextMeshProUGUI>();
        defeatText.text = "ПОРАЖЕНИЕ";
        defeatText.fontSize = 72;
        defeatText.alignment = TextAlignmentOptions.Center;
        defeatText.color = Color.red;
        defeatText.rectTransform.anchoredPosition = Vector2.zero;
        
        // Скрываем Canvas
        defeatCanvas.gameObject.SetActive(false);
        Debug.Log("PlayerDefeat: Canvas создан и скрыт");
    }
    
    private void HandleDefeat()
    {
        Debug.Log("PlayerDefeat: Получено событие поражения");
        if (isDefeatActive)
        {
            Debug.Log("PlayerDefeat: Поражение уже активно, игнорирую");
            return;
        }
        
        isDefeatActive = true;
        defeatCanvas.gameObject.SetActive(true);
        Debug.Log("PlayerDefeat: Начинаю последовательность поражения");
        
        // Находим DialogueSystem и вызываем OnPlayerDefeat
        DialogueSystem dialogueSystem = FindObjectOfType<DialogueSystem>();
        if (dialogueSystem != null)
        {
            dialogueSystem.OnPlayerDefeat();
            Debug.Log("PlayerDefeat: Вызван OnPlayerDefeat в DialogueSystem");
        }
        else
        {
            Debug.LogError("PlayerDefeat: DialogueSystem не найден!");
        }
        
        StartCoroutine(DefeatSequence());
    }
    
    private IEnumerator DefeatSequence()
    {
        Debug.Log("PlayerDefeat: Начало затемнения");
        // Затемняем экран
        float fadeTime = 0f;
        while (fadeTime < 1f)
        {
            fadeTime += Time.deltaTime * fadeSpeed;
            fadeImage.color = new Color(0, 0, 0, fadeTime);
            yield return null;
        }
        Debug.Log("PlayerDefeat: Экран затемнен");
        
        // Ждем перед сбросом
        Debug.Log($"PlayerDefeat: Ожидание {delayBeforeReset} секунд перед сбросом");
        yield return new WaitForSeconds(delayBeforeReset);
        
        // Скрываем финишную линию
        if (finishLine != null)
        {
            Debug.Log("PlayerDefeat: Скрываю финишную линию");
            finishLine.SetActive(false);
        }
        
        // Останавливаем AI вместо его скрытия
        if (aiOpponent != null)
        {
            Debug.Log("PlayerDefeat: Останавливаю AI");
            aiOpponent.StopAI();
        }
        
        // Возвращаем игрока на старт
        if (player != null)
        {
            Debug.Log($"PlayerDefeat: Возвращаю игрока на позицию {playerStartPosition}");
            player.transform.position = playerStartPosition;
            player.transform.eulerAngles = playerStartRotation;
            
            // Сбрасываем физику
            Rigidbody playerRb = player.GetComponent<Rigidbody>();
            if (playerRb != null)
            {
                playerRb.linearVelocity = Vector3.zero;
                playerRb.angularVelocity = Vector3.zero;
                Debug.Log("PlayerDefeat: Сброшена физика игрока");
            }
        }
        
        Debug.Log("PlayerDefeat: Начало возврата прозрачности");
        // Возвращаем прозрачность
        fadeTime = 1f;
        while (fadeTime > 0f)
        {
            fadeTime -= Time.deltaTime * fadeSpeed;
            fadeImage.color = new Color(0, 0, 0, fadeTime);
            yield return null;
        }
        Debug.Log("PlayerDefeat: Экран вернулся к нормальному виду");
        
        // Скрываем Canvas
        defeatCanvas.gameObject.SetActive(false);
        isDefeatActive = false;
        Debug.Log("PlayerDefeat: Последовательность поражения завершена");
    }

    public Vector3 GetPlayerStartPosition()
    {
        return playerStartPosition;
    }
} 