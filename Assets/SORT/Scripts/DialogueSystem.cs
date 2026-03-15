using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class DialogueSystem : MonoBehaviour
{
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private TextMeshProUGUI dialogueText;
    [SerializeField] private Button continueButton;
    [SerializeField] private float fadeSpeed = 1f;
    [SerializeField] private Vector3 teleportPosition;
    [SerializeField] private float typingSpeed = 0.05f;
    [SerializeField] private float delayBetweenDialogues = 1f;
    [SerializeField] private float teleportDelay = 1f;
    [SerializeField] private Vector3 raceStartPosition;
    [SerializeField] private Vector3 raceStartRotation;
    [SerializeField] private Vector3 aiStartPosition;
    [SerializeField] private Vector3 aiStartRotation;
    
    private string[] dialogueLines;
    private int currentLine = 0;
    private Image fadePanel;
    private GameObject player;
    private GameObject aiOpponent;
    private Canvas fadeCanvas;
    private RaceTimer raceTimer;
    private ScreenFade screenFade;
    private Queue<string> dialogues = new Queue<string>();
    private bool isDialogueActive = false;
    private bool isTyping = false;
    private bool isRaceStarted = false;
    
    private void Start()
    {
        Debug.Log("DialogueSystem: Start");
        SetupUI();
        dialoguePanel.SetActive(false);
        
        // Находим игрока
        player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            Debug.LogError("DialogueSystem: Игрок не найден! Убедитесь, что у игрока установлен тег 'Player'");
            return;
        }
        
        // Находим AI
        aiOpponent = FindObjectOfType<AIOpponent>()?.gameObject;
        if (aiOpponent == null)
        {
            Debug.LogWarning("DialogueSystem: AI не найден. Он будет найден позже.");
        }
        
        // Находим RaceTimer
        raceTimer = FindObjectOfType<RaceTimer>();
        if (raceTimer == null)
        {
            Debug.LogWarning("DialogueSystem: RaceTimer не найден. Он будет создан автоматически.");
            CreateRaceTimer();
        }
        
        // Проверяем наличие RaceTimer
        if (raceTimer == null)
        {
            Debug.Log("DialogueSystem: Создаю RaceTimer");
            GameObject raceTimerObj = new GameObject("RaceTimer");
            raceTimer = raceTimerObj.AddComponent<RaceTimer>();
            
            // Создаем UI для таймера
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas != null)
            {
                GameObject timerTextObj = new GameObject("TimerText");
                timerTextObj.transform.SetParent(canvas.transform, false);
                
                RectTransform timerRect = timerTextObj.AddComponent<RectTransform>();
                timerRect.anchorMin = new Vector2(0.5f, 0.8f);
                timerRect.anchorMax = new Vector2(0.5f, 0.8f);
                timerRect.sizeDelta = new Vector2(200, 50);
                
                TextMeshProUGUI timerText = timerTextObj.AddComponent<TextMeshProUGUI>();
                timerText.fontSize = 48;
                timerText.alignment = TextAlignmentOptions.Center;
                timerText.color = Color.white;
                
                raceTimer.SetTimerText(timerText);
            }
        }
        
        // Проверяем и настраиваем кнопку
        if (continueButton != null)
        {
            Debug.Log("DialogueSystem: Настройка кнопки продолжения");
            continueButton.onClick.RemoveAllListeners();
            continueButton.onClick.AddListener(ShowNextLine);
            
            continueButton.interactable = true;
            
            if (continueButton.GetComponent<Image>() == null)
            {
                Debug.LogWarning("DialogueSystem: Добавляем Image компонент на кнопку");
                continueButton.gameObject.AddComponent<Image>();
            }
        }
        else
        {
            Debug.LogError("DialogueSystem: Кнопка продолжения не назначена!");
        }
        
        // Скрываем панель диалога
        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(false);
        }
        
        // Добавляем диалоги
        AddDialogues();
        
        // Добавляем компонент ScreenFade
        screenFade = gameObject.AddComponent<ScreenFade>();
    }
    
    private void Update()
    {
        // Пытаемся найти AI, если он еще не найден
        if (aiOpponent == null)
        {
            aiOpponent = FindObjectOfType<AIOpponent>()?.gameObject;
            if (aiOpponent != null)
            {
                Debug.Log("DialogueSystem: AI найден");
            }
        }
        
        // Если диалог активен и игрок нажал E
        if (isDialogueActive && Input.GetKeyDown(KeyCode.E))
        {
            if (isTyping)
            {
                // Если текст печатается, показываем его полностью
                StopAllCoroutines();
                dialogueText.text = dialogues.Peek();
                isTyping = false;
            }
            else
            {
                // Если текст показан полностью, переходим к следующему
                DisplayNextDialogue();
            }
        }
    }
    
    private void SetupUI()
    {
        Debug.Log("DialogueSystem: SetupUI начат");
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            Debug.Log("DialogueSystem: Создаю новый Canvas");
            GameObject canvasObj = new GameObject("DialogueCanvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            
            // Проверяем наличие EventSystem
            if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                GameObject eventSystemObj = new GameObject("EventSystem");
                eventSystemObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
                eventSystemObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }
        }
        
        // Создаем панель затемнения
        GameObject fadePanelObj = new GameObject("FadePanel");
        fadePanelObj.transform.SetParent(canvas.transform, false);
        
        RectTransform fadeRect = fadePanelObj.AddComponent<RectTransform>();
        fadeRect.anchorMin = Vector2.zero;
        fadeRect.anchorMax = Vector2.one;
        fadeRect.offsetMin = Vector2.zero;
        fadeRect.offsetMax = Vector2.zero;
        
        fadePanel = fadePanelObj.AddComponent<Image>();
        fadePanel.color = new Color(0, 0, 0, 0);
        fadePanel.raycastTarget = false;
        
        // Создаем панель если её нет
        if (dialoguePanel == null)
        {
            Debug.Log("DialogueSystem: Создаю новую панель");
            GameObject panelObj = new GameObject("DialoguePanel");
            panelObj.transform.SetParent(canvas.transform, false);
            dialoguePanel = panelObj;
            
            RectTransform panelRect = panelObj.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0, 0);
            panelRect.anchorMax = new Vector2(1, 0.2f);
            panelRect.offsetMin = new Vector2(0, 0);
            panelRect.offsetMax = new Vector2(0, 0);
            
            Image panelImage = panelObj.AddComponent<Image>();
            panelImage.color = new Color(0, 0, 0, 0.8f);
        }
        
        // Создаем текст если его нет
        if (dialogueText == null)
        {
            Debug.Log("DialogueSystem: Создаю новый текст");
            GameObject textObj = new GameObject("DialogueText");
            textObj.transform.SetParent(dialoguePanel.transform, false);
            dialogueText = textObj.AddComponent<TextMeshProUGUI>();
            
            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0, 0);
            textRect.anchorMax = new Vector2(1, 0.8f);
            textRect.offsetMin = new Vector2(20, 20);
            textRect.offsetMax = new Vector2(-20, -20);
            
            dialogueText.fontSize = 24;
            dialogueText.alignment = TextAlignmentOptions.Center;
            dialogueText.color = Color.white;
        }
        
        // Создаем кнопку если её нет
        if (continueButton == null)
        {
            Debug.Log("DialogueSystem: Создаю новую кнопку");
            GameObject buttonObj = new GameObject("ContinueButton");
            buttonObj.transform.SetParent(dialoguePanel.transform, false);
            
            RectTransform buttonRect = buttonObj.AddComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(1, 0);
            buttonRect.anchorMax = new Vector2(1, 0.2f);
            buttonRect.offsetMin = new Vector2(-160, 10);
            buttonRect.offsetMax = new Vector2(-10, -10);
            
            continueButton = buttonObj.AddComponent<Button>();
            Image buttonImage = buttonObj.AddComponent<Image>();
            buttonImage.color = new Color(0.2f, 0.2f, 0.2f);
            
            GameObject buttonTextObj = new GameObject("ButtonText");
            buttonTextObj.transform.SetParent(buttonObj.transform, false);
            
            RectTransform buttonTextRect = buttonTextObj.AddComponent<RectTransform>();
            buttonTextRect.anchorMin = Vector2.zero;
            buttonTextRect.anchorMax = Vector2.one;
            buttonTextRect.offsetMin = Vector2.zero;
            buttonTextRect.offsetMax = Vector2.zero;
            
            TextMeshProUGUI buttonText = buttonTextObj.AddComponent<TextMeshProUGUI>();
            buttonText.text = "Продолжить";
            buttonText.fontSize = 20;
            buttonText.alignment = TextAlignmentOptions.Center;
            buttonText.color = Color.white;
            
            continueButton.interactable = true;
            continueButton.transition = Selectable.Transition.ColorTint;
            var colors = continueButton.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.gray;
            colors.pressedColor = Color.gray;
            continueButton.colors = colors;
        }
        
        Debug.Log("DialogueSystem: SetupUI завершен");
    }
    
    public void StartDialogue(string[] lines)
    {
        Debug.Log($"DialogueSystem: Начало диалога, количество строк: {lines.Length}");
        dialogueLines = lines;
        currentLine = 0;
        dialoguePanel.SetActive(true);
        
        // Логируем все строки диалога
        for (int i = 0; i < dialogueLines.Length; i++)
        {
            Debug.Log($"DialogueSystem: Строка {i}: {dialogueLines[i]}");
        }
        
        // Убеждаемся, что кнопка активна
        if (continueButton != null)
        {
            continueButton.interactable = true;
            continueButton.gameObject.SetActive(true);
        }
        
        // Показываем первую строку сразу
        if (dialogueLines != null && dialogueLines.Length > 0)
        {
            dialogueText.text = dialogueLines[0];
            currentLine = 1;
        }
    }
    
    public void ShowNextLine()
    {
        Debug.Log($"DialogueSystem: ShowNextLine вызван. Текущая строка: {currentLine}");
        
        // Проверяем, что диалог активен и есть строки
        if (dialogueLines == null || dialogueLines.Length == 0)
        {
            Debug.LogWarning("DialogueSystem: Нет строк диалога для показа");
            EndDialogue();
            return;
        }
        
        if (currentLine < dialogueLines.Length)
        {
            Debug.Log($"DialogueSystem: Показ строки {currentLine + 1} из {dialogueLines.Length}");
            dialogueText.text = dialogueLines[currentLine];
            currentLine++;
        }
        else
        {
            Debug.Log("DialogueSystem: Диалог завершен");
            EndDialogue();
        }
    }
    
    public void EndDialogue()
    {
        Debug.Log("DialogueSystem: Завершение диалога");
        dialoguePanel.SetActive(false);
        currentLine = 0;
        dialogueLines = null;
        
        // Запускаем последовательность затемнения и телепортации только если гонка не была завершена
        if (!isRaceStarted)
        {
            StartCoroutine(FadeAndTeleport());
        }
    }
    
    public IEnumerator FadeAndTeleport()
    {
        // Затемнение
        while (fadePanel.color.a < 1)
        {
            fadePanel.color = new Color(0, 0, 0, fadePanel.color.a + Time.deltaTime * fadeSpeed);
            yield return null;
        }

        // Телепортация игрока
        if (player != null)
        {
            player.transform.position = teleportPosition;
            Debug.Log("DialogueSystem: Игрок телепортирован");
        }

        // Телепортация AI
        if (aiOpponent != null)
        {
            aiOpponent.transform.position = aiStartPosition;
            aiOpponent.transform.eulerAngles = aiStartRotation;
            aiOpponent.SetActive(true);
            
            // Сбрасываем состояние AI
            AIOpponent aiScript = aiOpponent.GetComponent<AIOpponent>();
            if (aiScript != null)
            {
                aiScript.ResetAI();
                Debug.Log("DialogueSystem: Состояние AI сброшено");
            }
            
            Debug.Log("DialogueSystem: AI телепортирован");
        }

        // Появляем финишную линию
        GameObject finishLine = GameObject.FindGameObjectWithTag("FinishLine");
        if (finishLine != null)
        {
            finishLine.SetActive(true);
            Debug.Log("DialogueSystem: Финишная линия активирована");
            
            // Сбрасываем состояние финишной линии
            FinishLine finishLineScript = finishLine.GetComponent<FinishLine>();
            if (finishLineScript != null)
            {
                // Состояние сбрасывается в OnRaceStart, который вызывается при начале гонки
                Debug.Log("DialogueSystem: Финишная линия готова к новой гонке");
            }
        }
        else
        {
            Debug.LogWarning("DialogueSystem: Финишная линия не найдена! Проверьте тег 'FinishLine'");
            // Создаем финишную линию, если она не найдена
            CreateFinishLine();
        }

        // Задержка перед появлением
        yield return new WaitForSeconds(0.5f);

        // Появление
        while (fadePanel.color.a > 0)
        {
            fadePanel.color = new Color(0, 0, 0, fadePanel.color.a - Time.deltaTime * fadeSpeed);
            yield return null;
        }

        // Запускаем таймер обратного отсчета
        if (raceTimer != null)
        {
            raceTimer.ResetTimer();
            raceTimer.StartCountdown();
            Debug.Log("DialogueSystem: Запущен таймер обратного отсчета");
            // Устанавливаем флаг начала новой гонки
            isRaceStarted = true;
        }
        else
        {
            Debug.LogError("DialogueSystem: RaceTimer не найден!");
        }
    }

    private void CreateFinishLine()
    {
        // Создаем объект финишной линии
        GameObject finishLine = new GameObject("FinishLine");
        finishLine.tag = "FinishLine";
        
        // Добавляем коллайдер
        BoxCollider collider = finishLine.AddComponent<BoxCollider>();
        collider.isTrigger = true;
        collider.size = new Vector3(2f, 1f, 0.1f);
        
        // Добавляем визуальный компонент
        MeshRenderer renderer = finishLine.AddComponent<MeshRenderer>();
        MeshFilter meshFilter = finishLine.AddComponent<MeshFilter>();
        
        // Создаем простой куб
        Mesh mesh = new Mesh();
        Vector3[] vertices = new Vector3[]
        {
            new Vector3(-1, 0, -0.05f),
            new Vector3(1, 0, -0.05f),
            new Vector3(1, 1, -0.05f),
            new Vector3(-1, 1, -0.05f),
            new Vector3(-1, 0, 0.05f),
            new Vector3(1, 0, 0.05f),
            new Vector3(1, 1, 0.05f),
            new Vector3(-1, 1, 0.05f)
        };
        
        int[] triangles = new int[]
        {
            0, 1, 2,
            0, 2, 3,
            1, 5, 6,
            1, 6, 2,
            5, 4, 7,
            5, 7, 6,
            4, 0, 3,
            4, 3, 7,
            3, 2, 6,
            3, 6, 7
        };
        
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        
        meshFilter.mesh = mesh;
        
        // Создаем материал
        Material material = new Material(Shader.Find("Standard"));
        material.color = Color.green;
        renderer.material = material;
        
        // Устанавливаем позицию
        finishLine.transform.position = new Vector3(0f, 0.5f, 0f);
        
        Debug.Log("DialogueSystem: Создана новая финишная линия");
    }

    public void SetTeleportPosition(Vector3 position)
    {
        teleportPosition = position;
        Debug.Log($"DialogueSystem: Установлена новая позиция телепортации: {position}");
    }
    
    private void DisplayNextDialogue()
    {
        if (dialogues.Count == 0)
        {
            EndDialogue();
            return;
        }
        
        string dialogue = dialogues.Dequeue();
        StartCoroutine(TypeDialogue(dialogue));
    }
    
    private IEnumerator TypeDialogue(string dialogue)
    {
        isTyping = true;
        dialogueText.text = "";
        
        foreach (char letter in dialogue)
        {
            dialogueText.text += letter;
            yield return new WaitForSeconds(typingSpeed);
        }
        
        isTyping = false;
        yield return new WaitForSeconds(delayBetweenDialogues);
    }
    
    private void CreateRaceTimer()
    {
        GameObject timerObj = new GameObject("RaceTimer");
        raceTimer = timerObj.AddComponent<RaceTimer>();
        
        // Создаем UI для таймера
        GameObject canvasObj = new GameObject("TimerCanvas");
        Canvas timerCanvas = canvasObj.AddComponent<Canvas>();
        timerCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();
        
        GameObject textObj = new GameObject("TimerText");
        textObj.transform.SetParent(canvasObj.transform, false);
        TextMeshProUGUI timerText = textObj.AddComponent<TextMeshProUGUI>();
        timerText.fontSize = 72;
        timerText.alignment = TextAlignmentOptions.Center;
        timerText.rectTransform.anchoredPosition = new Vector2(0, 200);
        
        raceTimer.SetTimerText(timerText);
    }
    
    private void AddDialogues()
    {
        dialogues.Clear();
        dialogues.Enqueue("Привет! Хочешь участвовать в гонке?");
        dialogues.Enqueue("Правила простые: догони меня до финиша!");
        dialogues.Enqueue("Готов? Тогда поехали!");
    }
    
    public void OnPlayerVictory()
    {
        Debug.Log("DialogueSystem: Обработка победы игрока");
        isRaceStarted = false;
        
        // Возвращаем игрока к NPC с эффектом затемнения
        if (player != null)
        {
            // Находим позицию NPC
            Vector3 npcPosition = transform.position;
            // Устанавливаем позицию игрока немного впереди NPC
            Vector3 playerPosition = npcPosition + transform.forward * 2f;
            playerPosition.y = player.transform.position.y; // Сохраняем текущую высоту игрока
            
            // Обновляем позицию телепортации
            teleportPosition = playerPosition;
            Debug.Log($"DialogueSystem: Обновлена позиция телепортации: {teleportPosition}");
            
            // Запускаем эффект затемнения и телепортации
            StartCoroutine(screenFade.FadeInAndOut(() => {
                // Телепортируем игрока
                player.transform.position = playerPosition;
                Debug.Log("DialogueSystem: Игрок возвращен к NPC после победы");
                
                // Сбрасываем физику игрока
                Rigidbody playerRb = player.GetComponent<Rigidbody>();
                if (playerRb != null)
                {
                    playerRb.linearVelocity = Vector3.zero;
                    playerRb.angularVelocity = Vector3.zero;
                }
            }));
        }

        // Деактивируем AI и финишную линию
        if (aiOpponent != null)
        {
            aiOpponent.SetActive(false);
            Debug.Log("DialogueSystem: AI деактивирован после победы игрока");
        }

        GameObject finishLine = GameObject.FindGameObjectWithTag("FinishLine");
        if (finishLine != null)
        {
            finishLine.SetActive(false);
            Debug.Log("DialogueSystem: Финишная линия деактивирована после победы игрока");
        }
        
        // Здесь больше не вызываем никаких методов, которые могли бы перезапустить гонку
    }

    public void OnPlayerDefeat()
    {
        isRaceStarted = false;
        
        // Возвращаем игрока к NPC
        if (player != null)
        {
            // Находим позицию NPC
            Vector3 npcPosition = transform.position;
            // Устанавливаем позицию игрока немного впереди NPC
            Vector3 playerPosition = npcPosition + transform.forward * 2f;
            playerPosition.y = player.transform.position.y; // Сохраняем текущую высоту игрока
            
            // Обновляем позицию телепортации
            teleportPosition = playerPosition;
            Debug.Log($"DialogueSystem: Обновлена позиция телепортации: {teleportPosition}");
            
            // Телепортируем игрока
            player.transform.position = playerPosition;
            Debug.Log("DialogueSystem: Игрок возвращен к NPC после поражения");
            
            // Сбрасываем физику игрока
            Rigidbody playerRb = player.GetComponent<Rigidbody>();
            if (playerRb != null)
            {
                playerRb.linearVelocity = Vector3.zero;
                playerRb.angularVelocity = Vector3.zero;
            }
        }
    }

    public void OnDialogueEnd()
    {
        Debug.Log("DialogueSystem: Завершение диалога");
        dialoguePanel.SetActive(false);
        currentLine = 0;
        dialogueLines = null;
        
        // Запускаем последовательность затемнения и телепортации
        StartCoroutine(FadeAndTeleport());
    }
} 