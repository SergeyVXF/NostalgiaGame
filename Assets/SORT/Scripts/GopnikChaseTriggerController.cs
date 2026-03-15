using UnityEngine;

/// <summary>
/// Контроллер для триггерных зон катсцен gopnik_chase
/// Позволяет активировать вторую зону (Gopnik_Chase_quest_02) только после активации первой (GopnikChase_01)
/// </summary>
public class GopnikChaseTriggerController : MonoBehaviour
{
    // Ссылка на первую триггерную зону (активна по умолчанию)
    [SerializeField] private GameObject gopnikChase01Trigger;
    
    // Ссылка на вторую триггерную зону (должна быть неактивна по умолчанию)
    [SerializeField] private GameObject gopnikChase02Trigger;
    
    // Ключ первой катсцены (должен соответствовать ключу в CutsceneManager)
    [SerializeField] private string firstCutsceneKey = "GopnikChase_01";
    
    // Тег объекта, который может активировать триггер
    [SerializeField] private string targetTag = "Player";
    
    // Включить подробное логирование для отладки
    [SerializeField] private bool debugMode = true;
    
    // Разрешить отладочные клавиши
    [SerializeField] private bool enableDebugKeys = true;
    
    // Принудительно активировать вторую зону сразу при старте
    [SerializeField] private bool forceActivateSecondTrigger = false;
    
    // Флаг, указывающий, была ли активирована первая катсцена
    private bool firstCutsceneWasPlayed = false;
    
    // Флаг, указывающий, столкнулся ли игрок с первым триггером
    private bool playerEnteredFirstTrigger = false;
    
    // Ссылка на CutsceneManager
    private CutsceneManager cutsceneManager;
    
    // Таймер для периодической проверки состояния
    private float debugTimer = 0f;
    private float debugInterval = 5f; // Проверка каждые 5 секунд

    private void Awake()
    {
        // Проверка компонентов
        if (gopnikChase01Trigger == null || gopnikChase02Trigger == null)
        {
            Debug.LogError("[GopnikChaseTriggerController] Не указаны необходимые триггеры! Отключаю скрипт.");
            enabled = false;
            return;
        }
        
        // Выводим отладочную информацию о триггерах
        if (debugMode)
        {
            Debug.Log($"[GopnikChaseTriggerController] Инициализация: первый триггер = {gopnikChase01Trigger.name}, второй триггер = {gopnikChase02Trigger.name}");
            Debug.Log($"[GopnikChaseTriggerController] Первый триггер активен: {gopnikChase01Trigger.activeInHierarchy}, второй триггер активен: {gopnikChase02Trigger.activeInHierarchy}");
            Debug.Log($"[GopnikChaseTriggerController] Ключ первой катсцены: {firstCutsceneKey}");
        }
        
        // При старте скрываем вторую триггерную зону, если не включен режим принудительной активации
        if (!forceActivateSecondTrigger)
        {
            gopnikChase02Trigger.SetActive(false);
        }
        else
        {
            Debug.LogWarning("[GopnikChaseTriggerController] ВНИМАНИЕ: Включен режим принудительной активации второй зоны!");
            gopnikChase02Trigger.SetActive(true);
        }
        
        // Находим CutsceneManager в сцене
        cutsceneManager = FindObjectOfType<CutsceneManager>();
        if (cutsceneManager == null)
        {
            Debug.LogError("[GopnikChaseTriggerController] CutsceneManager не найден в сцене! Отключаю скрипт.");
            enabled = false;
            return;
        }
    }

    private void OnEnable()
    {
        // Подписываемся на событие окончания катсцены
        if (cutsceneManager != null)
        {
            CutsceneManager.OnCutsceneEnded += OnCutsceneEnded;
            Debug.Log("[GopnikChaseTriggerController] Успешно подписался на событие OnCutsceneEnded");
        }
        else
        {
            // Повторная попытка найти CutsceneManager
            cutsceneManager = FindObjectOfType<CutsceneManager>();
            
            if (cutsceneManager != null)
            {
                CutsceneManager.OnCutsceneEnded += OnCutsceneEnded;
                Debug.Log("[GopnikChaseTriggerController] Успешно подписался на событие OnCutsceneEnded (после повторной попытки)");
            }
            else
            {
                Debug.LogError("[GopnikChaseTriggerController] CutsceneManager не найден! Отключаю скрипт.");
                enabled = false;
            }
        }
    }

    private void OnDisable()
    {
        // Отписываемся от события без проверки на null
        CutsceneManager.OnCutsceneEnded -= OnCutsceneEnded;
    }
    
    private void Start()
    {
        // Добавляем компоненты BoxCollider к триггерам, если их нет
        SetupTriggerCollider(gopnikChase01Trigger);
        SetupTriggerCollider(gopnikChase02Trigger);
        
        // Принудительно вызываем проверку состояния при старте
        if (debugMode)
        {
            Debug.Log("[GopnikChaseTriggerController] Проверка конфигурации при старте:");
            Debug.Log($"  - Первый триггер ({gopnikChase01Trigger.name}): {(gopnikChase01Trigger.activeInHierarchy ? "активен" : "неактивен")}");
            Debug.Log($"  - Второй триггер ({gopnikChase02Trigger.name}): {(gopnikChase02Trigger.activeInHierarchy ? "активен" : "неактивен")}");
            Debug.Log($"  - CutsceneManager: {(cutsceneManager != null ? "найден" : "не найден")}");
            
            // Проверяем коллайдеры на триггерах
            Collider col1 = gopnikChase01Trigger.GetComponent<Collider>();
            Collider col2 = gopnikChase02Trigger.GetComponent<Collider>();
            
            Debug.Log($"  - Коллайдер на первом триггере: {(col1 != null ? (col1.isTrigger ? "OK (триггер)" : "ОШИБКА (не триггер)") : "ОТСУТСТВУЕТ")}");
            Debug.Log($"  - Коллайдер на втором триггере: {(col2 != null ? (col2.isTrigger ? "OK (триггер)" : "ОШИБКА (не триггер)") : "ОТСУТСТВУЕТ")}");
        }
    }
    
    private void Update()
    {
        // Периодическая проверка состояния для отладки
        if (debugMode)
        {
            debugTimer += Time.deltaTime;
            if (debugTimer >= debugInterval)
            {
                debugTimer = 0;
                Debug.Log($"[GopnikChaseTriggerController] Текущее состояние: firstCutsceneWasPlayed={firstCutsceneWasPlayed}, playerEnteredFirstTrigger={playerEnteredFirstTrigger}");
                Debug.Log($"  - Второй триггер ({gopnikChase02Trigger.name}): {(gopnikChase02Trigger.activeInHierarchy ? "активен" : "неактивен")}");
            }
        }
        
        // Обработка отладочных клавиш
        if (enableDebugKeys)
        {
            // Нажатие F1 - симуляция входа игрока в первую зону
            if (Input.GetKeyDown(KeyCode.F1))
            {
                Debug.Log("[GopnikChaseTriggerController] Ручная симуляция входа в первую зону (F1)");
                PlayerEnteredFirstTrigger();
            }
            
            // Нажатие F2 - симуляция окончания первой катсцены
            if (Input.GetKeyDown(KeyCode.F2))
            {
                Debug.Log("[GopnikChaseTriggerController] Ручная симуляция окончания первой катсцены (F2)");
                firstCutsceneWasPlayed = true;
                CheckSecondTriggerActivation();
            }
            
            // Нажатие F3 - принудительная активация второй зоны
            if (Input.GetKeyDown(KeyCode.F3))
            {
                Debug.Log("[GopnikChaseTriggerController] Ручная активация второй зоны (F3)");
                ActivateSecondTrigger();
            }
            
            // Нажатие F4 - сброс состояния
            if (Input.GetKeyDown(KeyCode.F4))
            {
                Debug.Log("[GopnikChaseTriggerController] Сброс состояния (F4)");
                ResetProgress();
            }
            
            // Нажатие F5 - вывод состояния триггеров в иерархии
            if (Input.GetKeyDown(KeyCode.F5))
            {
                PrintHierarchyPath(gopnikChase01Trigger);
                PrintHierarchyPath(gopnikChase02Trigger);
            }
        }
    }
    
    private void PrintHierarchyPath(GameObject obj)
    {
        string path = GetHierarchyPath(obj.transform);
        Debug.Log($"[GopnikChaseTriggerController] Полный путь объекта {obj.name}: {path}");
    }
    
    private string GetHierarchyPath(Transform transform)
    {
        string path = transform.name;
        while (transform.parent != null)
        {
            transform = transform.parent;
            path = transform.name + "/" + path;
        }
        return path;
    }
    
    private void SetupTriggerCollider(GameObject triggerObject)
    {
        if (!triggerObject.GetComponent<Collider>())
        {
            if (debugMode) Debug.Log($"[GopnikChaseTriggerController] Добавляю BoxCollider к {triggerObject.name}");
            BoxCollider collider = triggerObject.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.size = new Vector3(3, 3, 3); // Размер по умолчанию
        }
        else if (debugMode)
        {
            Collider col = triggerObject.GetComponent<Collider>();
            if (!col.isTrigger)
            {
                Debug.LogWarning($"[GopnikChaseTriggerController] Коллайдер на {triggerObject.name} не является триггером! Устанавливаю isTrigger = true");
                col.isTrigger = true;
            }
            else
            {
                Debug.Log($"[GopnikChaseTriggerController] Коллайдер на {triggerObject.name} уже настроен как триггер.");
            }
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (debugMode)
        {
            Debug.Log($"[GopnikChaseTriggerController] OnTriggerEnter вызван на объекте {gameObject.name}. Столкновение с {other.name} (тег: {other.tag})");
        }
        
        // Если столкновение произошло с игроком, активируем первый триггер (это для случая, когда скрипт находится на контроллере)
        if (other.CompareTag(targetTag))
        {
            PlayerEnteredFirstTrigger();
        }
    }
    
    // Метод для отслеживания входа в триггерную зону GopnikChase_01
    // Можно вызывать из GopnikChaseTriggerHelper
    public void PlayerEnteredFirstTrigger()
    {
        Debug.Log("[GopnikChaseTriggerController] Игрок вошел в первую триггерную зону!");
        playerEnteredFirstTrigger = true;
        
        // Проверяем, можно ли активировать вторую зону
        CheckSecondTriggerActivation();
    }
    
    private void CheckSecondTriggerActivation()
    {
        if (playerEnteredFirstTrigger && firstCutsceneWasPlayed)
        {
            Debug.Log("[GopnikChaseTriggerController] Активирую вторую триггерную зону, все условия выполнены!");
            gopnikChase02Trigger.SetActive(true);
            
            // Дополнительная проверка, что зона действительно активирована
            if (!gopnikChase02Trigger.activeInHierarchy)
            {
                Debug.LogError("[GopnikChaseTriggerController] ОШИБКА! Не удалось активировать вторую зону несмотря на SetActive(true)!");
                // Проверяем, не отключены ли родительские объекты
                Transform parent = gopnikChase02Trigger.transform.parent;
                if (parent != null && !parent.gameObject.activeInHierarchy)
                {
                    Debug.LogError($"[GopnikChaseTriggerController] Родительский объект {parent.name} неактивен!");
                }
            }
            else
            {
                Debug.Log("[GopnikChaseTriggerController] Вторая зона успешно активирована.");
            }
        }
        else if (debugMode)
        {
            Debug.Log($"[GopnikChaseTriggerController] Не могу активировать вторую зону: firstCutsceneWasPlayed={firstCutsceneWasPlayed}, playerEnteredFirstTrigger={playerEnteredFirstTrigger}");
        }
    }

    private void OnCutsceneEnded(GameObject cutsceneObject)
    {
        if (debugMode)
        {
            Debug.Log($"[GopnikChaseTriggerController] OnCutsceneEnded вызван. Объект катсцены: {cutsceneObject.name}");
        }
        
        // Проверяем, закончилась ли первая катсцена
        if (cutsceneObject != null && cutsceneObject.name.Contains(firstCutsceneKey))
        {
            Debug.Log($"[GopnikChaseTriggerController] Катсцена {firstCutsceneKey} завершена.");
            
            // Устанавливаем флаг, что первая катсцена была активирована
            firstCutsceneWasPlayed = true;
            
            // Проверяем, можно ли активировать вторую зону
            CheckSecondTriggerActivation();
        }
        else if (debugMode && cutsceneObject != null)
        {
            Debug.Log($"[GopnikChaseTriggerController] Завершилась катсцена {cutsceneObject.name}, но ожидалась {firstCutsceneKey}");
        }
    }

    // Публичный метод для проверки состояния катсцен (может использоваться другими скриптами)
    public bool WasFirstCutscenePlayed()
    {
        return firstCutsceneWasPlayed;
    }
    
    // Публичный метод для проверки состояния первого триггера
    public bool DidPlayerEnterFirstTrigger()
    {
        return playerEnteredFirstTrigger;
    }
    
    // Публичный метод для ручной активации второго триггера (может быть вызван из редактора или другого скрипта)
    public void ActivateSecondTrigger()
    {
        Debug.Log("[GopnikChaseTriggerController] Принудительная активация второго триггера.");
        gopnikChase02Trigger.SetActive(true);
        
        // Дополнительная проверка, что триггер действительно активен
        if (debugMode)
        {
            if (gopnikChase02Trigger.activeInHierarchy)
            {
                Debug.Log("[GopnikChaseTriggerController] Второй триггер успешно активирован.");
            }
            else
            {
                Debug.LogError("[GopnikChaseTriggerController] ОШИБКА! Второй триггер не активен после SetActive(true)!");
            }
        }
    }
    
    // Метод для сброса состояния (может быть вызван из редактора или другого скрипта)
    public void ResetProgress()
    {
        firstCutsceneWasPlayed = false;
        playerEnteredFirstTrigger = false;
        gopnikChase02Trigger.SetActive(false);
        Debug.Log("[GopnikChaseTriggerController] Прогресс сброшен.");
    }
}