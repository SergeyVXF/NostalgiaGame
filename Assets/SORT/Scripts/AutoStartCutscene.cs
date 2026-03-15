using UnityEngine;
using System.Collections;

public class AutoStartCutscene : MonoBehaviour
{
    [Header("Настройки автозапуска катсцены")]
    [Tooltip("Ключ катсцены для автоматического запуска")]
    public string cutsceneKey = "";
    
    [Tooltip("Задержка перед запуском катсцены (в секундах)")]
    public float startDelay = 0f;
    
    [Tooltip("Использовать эффект затемнения при запуске")]
    public bool useFade = true;
    
    [Tooltip("Запускать катсцену только один раз")]
    public bool playOnce = true;
    
    [Header("Настройки игрока")]
    [Tooltip("Скрыть игрока до запуска катсцены")]
    public bool hidePlayerUntilCutscene = true;
    
    [Tooltip("Тег игрока для поиска")]
    public string playerTag = "Player";
    
    [Header("Отладка")]
    [Tooltip("Показывать отладочную информацию")]
    public bool showDebugLog = true;
    
    private bool hasPlayed = false;
    private CutsceneManager cutsceneManager;
    private CutsceneFadeManager fadeManager;
    private GameObject playerObject;
    
    private void Awake()
    {
        if (showDebugLog)
            Debug.Log($"[AutoStartCutscene] 🎬 Инициализация автозапуска катсцены: {cutsceneKey}");
        
        // Находим менеджеры катсцен
        cutsceneManager = FindObjectOfType<CutsceneManager>();
        fadeManager = FindObjectOfType<CutsceneFadeManager>();
        
        if (cutsceneManager == null)
        {
            Debug.LogError("[AutoStartCutscene] ❌ CutsceneManager не найден в сцене!");
            return;
        }
        
        if (string.IsNullOrEmpty(cutsceneKey))
        {
            Debug.LogWarning("[AutoStartCutscene] ⚠️ Ключ катсцены не указан!");
            return;
        }
        
        // Находим игрока, но не скрываем его сразу
        if (hidePlayerUntilCutscene)
        {
            playerObject = GameObject.FindGameObjectWithTag(playerTag);
            if (playerObject != null)
            {
                if (showDebugLog)
                    Debug.Log($"[AutoStartCutscene] 👤 Игрок найден, будет скрыт после инициализации");
            }
            else
            {
                if (showDebugLog)
                    Debug.LogWarning($"[AutoStartCutscene] ⚠️ Игрок с тегом '{playerTag}' не найден");
            }
        }
        
        if (showDebugLog)
        {
            Debug.Log($"[AutoStartCutscene] ✅ CutsceneManager найден");
            Debug.Log($"[AutoStartCutscene] ✅ CutsceneFadeManager найден: {(fadeManager != null ? "Да" : "Нет")}");
            Debug.Log($"[AutoStartCutscene] ⏰ Запуск катсцены '{cutsceneKey}' через {startDelay}с");
        }
        
        // Запускаем катсцену с задержкой
        if (startDelay > 0)
        {
            StartCoroutine(StartCutsceneWithDelay());
        }
        else
        {
            // Запускаем катсцену мгновенно
            StartCutsceneImmediately();
        }
    }
    
    private void OnEnable()
    {
        // Подписываемся на события окончания катсцены
        if (cutsceneManager != null)
        {
            // Примечание: здесь нужно подписаться на события CutsceneManager
            // если они доступны. Если нет - используем альтернативный метод
        }
    }
    
    private void OnDisable()
    {
        // Отписываемся от событий
        if (cutsceneManager != null)
        {
            // Отписка от событий
        }
    }
    
    /// <summary>
    /// Корутина для запуска катсцены с задержкой
    /// </summary>
    private IEnumerator StartCutsceneWithDelay()
    {
        yield return new WaitForSeconds(startDelay);
        
        // Скрываем игрока перед запуском катсцены
        if (hidePlayerUntilCutscene && playerObject != null)
        {
            playerObject.SetActive(false);
            if (showDebugLog)
                Debug.Log($"[AutoStartCutscene] 👤 Игрок скрыт перед запуском катсцены");
        }
        
        StartCutscene();
    }
    
    /// <summary>
    /// Корутина для ожидания инициализации базы данных катсцен
    /// </summary>
    private IEnumerator WaitForCutsceneDatabase()
    {
        // Ждем, пока база данных катсцен не будет инициализирована
        while (CutsceneManager.cutsceneDataBase.Count == 0)
        {
            yield return null; // Ждем один кадр
        }
        
        if (showDebugLog)
            Debug.Log($"[AutoStartCutscene] ✅ База данных катсцен инициализирована, запускаю катсцену");
        
        // Скрываем игрока перед запуском катсцены
        if (hidePlayerUntilCutscene && playerObject != null)
        {
            playerObject.SetActive(false);
            if (showDebugLog)
                Debug.Log($"[AutoStartCutscene] 👤 Игрок скрыт перед запуском катсцены");
        }
        
        StartCutscene();
    }
    
    /// <summary>
    /// Метод для немедленного запуска катсцены
    /// </summary>
    private void StartCutsceneImmediately()
    {
        // Проверяем, инициализирована ли база данных катсцен
        if (CutsceneManager.cutsceneDataBase.Count == 0)
        {
            if (showDebugLog)
                Debug.LogWarning("[AutoStartCutscene] ⚠️ База данных катсцен еще не инициализирована, ждем...");
            
            // Запускаем корутину для ожидания инициализации
            StartCoroutine(WaitForCutsceneDatabase());
            return;
        }
        
        // Скрываем игрока перед запуском катсцены
        if (hidePlayerUntilCutscene && playerObject != null)
        {
            playerObject.SetActive(false);
            if (showDebugLog)
                Debug.Log($"[AutoStartCutscene] 👤 Игрок скрыт перед запуском катсцены");
        }
        
        StartCutscene();
    }
    
    /// <summary>
    /// Запускает катсцену
    /// </summary>
    private void StartCutscene()
    {
        if (playOnce && hasPlayed)
        {
            if (showDebugLog)
                Debug.Log($"[AutoStartCutscene] ⏭️ Катсцена '{cutsceneKey}' уже была запущена");
            return;
        }
        
        if (showDebugLog)
            Debug.Log($"[AutoStartCutscene] 🎬 Запускаю катсцену: {cutsceneKey}");
        
        // Проверяем, есть ли катсцена в базе данных
        if (!CutsceneManager.cutsceneDataBase.ContainsKey(cutsceneKey))
        {
            Debug.LogError($"[AutoStartCutscene] ❌ Катсцена с ключом '{cutsceneKey}' не найдена в базе данных!");
            
            // Показываем доступные катсцены
            ShowAvailableCutscenes();
            
            // Если катсцена не найдена, показываем игрока
            if (hidePlayerUntilCutscene && playerObject != null)
            {
                playerObject.SetActive(true);
                if (showDebugLog)
                    Debug.Log($"[AutoStartCutscene] 👤 Игрок показан (катсцена не найдена)");
            }
            return;
        }
        
        // Запускаем катсцену
        if (useFade && fadeManager != null)
        {
            if (showDebugLog)
                Debug.Log($"[AutoStartCutscene] 🎬 Запускаю катсцену с эффектом затемнения: {cutsceneKey}");
            fadeManager.StartCutsceneWithFade(cutsceneKey);
        }
        else
        {
            if (showDebugLog)
                Debug.Log($"[AutoStartCutscene] 🎬 Запускаю катсцену напрямую: {cutsceneKey}");
            cutsceneManager.StartCutscene(cutsceneKey);
        }
        
        hasPlayed = true;
        
        // НЕ показываем игрока сразу после запуска катсцены
        // Игрок будет показан только после окончания катсцены через CutsceneManager
        
        // Запасной вариант: показываем игрока через 5 секунд, если катсцена не закончилась
        if (hidePlayerUntilCutscene && playerObject != null)
        {
            StartCoroutine(ShowPlayerAfterDelayCoroutine(5f));
        }
        
        if (showDebugLog)
            Debug.Log($"[AutoStartCutscene] ✅ Катсцена '{cutsceneKey}' успешно запущена (игрок остается скрытым до окончания)");
    }
    
    /// <summary>
    /// Показывает список доступных катсцен в консоли
    /// </summary>
    private void ShowAvailableCutscenes()
    {
        if (CutsceneManager.cutsceneDataBase.Count == 0)
        {
            Debug.LogWarning("[AutoStartCutscene] ⚠️ База данных катсцен пуста! Проверьте настройки CutsceneManager.");
            return;
        }
        
        Debug.Log("[AutoStartCutscene] 📋 Доступные катсцены:");
        foreach (var cutscene in CutsceneManager.cutsceneDataBase)
        {
            string status = cutscene.Value != null ? "✅" : "❌";
            Debug.Log($"[AutoStartCutscene] {status} '{cutscene.Key}' -> {cutscene.Value?.name ?? "NULL"}");
        }
        
        Debug.LogWarning($"[AutoStartCutscene] 💡 Используйте один из доступных ключей выше вместо '{cutsceneKey}'");
    }
    
    /// <summary>
    /// Метод для ручного запуска катсцены (можно вызвать из других скриптов)
    /// </summary>
    public void StartCutsceneManually()
    {
        if (showDebugLog)
            Debug.Log($"[AutoStartCutscene] 🎬 Ручной запуск катсцены: {cutsceneKey}");
        
        StartCutscene();
    }
    
    /// <summary>
    /// Показывает игрока после окончания катсцены
    /// </summary>
    public void ShowPlayerAfterCutscene()
    {
        if (hidePlayerUntilCutscene && playerObject != null)
        {
            playerObject.SetActive(true);
            if (showDebugLog)
                Debug.Log($"[AutoStartCutscene] 👤 Игрок показан после окончания катсцены");
        }
    }
    
    /// <summary>
    /// Корутина для автоматического показа игрока через заданное время
    /// </summary>
    private IEnumerator ShowPlayerAfterDelayCoroutine(float delay)
    {
        yield return new WaitForSeconds(delay);
        
        if (hidePlayerUntilCutscene && playerObject != null)
        {
            playerObject.SetActive(true);
            if (showDebugLog)
                Debug.Log($"[AutoStartCutscene] 👤 Игрок автоматически показан через {delay}с");
        }
    }
    
    /// <summary>
    /// Автоматически показывает игрока через заданное время (для вызова из других скриптов)
    /// </summary>
    public void ShowPlayerAfterDelay(float delay)
    {
        StartCoroutine(ShowPlayerAfterDelayCoroutine(delay));
    }
    
    /// <summary>
    /// Сброс флага воспроизведения (для повторного запуска)
    /// </summary>
    public void ResetPlayFlag()
    {
        hasPlayed = false;
        if (showDebugLog)
            Debug.Log($"[AutoStartCutscene] 🔄 Флаг воспроизведения сброшен");
    }
    
    /// <summary>
    /// Изменение ключа катсцены во время выполнения
    /// </summary>
    public void SetCutsceneKey(string newKey)
    {
        cutsceneKey = newKey;
        if (showDebugLog)
            Debug.Log($"[AutoStartCutscene] 🔑 Ключ катсцены изменен на: {newKey}");
    }
    
    /// <summary>
    /// Проверяет доступные катсцены (для вызова из редактора)
    /// </summary>
    [ContextMenu("Проверить доступные катсцены")]
    public void CheckAvailableCutscenes()
    {
        if (CutsceneManager.Instance == null)
        {
            Debug.LogError("[AutoStartCutscene] ❌ CutsceneManager не найден в сцене!");
            return;
        }
        
        ShowAvailableCutscenes();
    }
    
    /// <summary>
    /// Тестирует запуск катсцены (для вызова из редактора)
    /// </summary>
    [ContextMenu("Тест запуска катсцены")]
    public void TestCutsceneStart()
    {
        if (CutsceneManager.Instance == null)
        {
            Debug.LogError("[AutoStartCutscene] ❌ CutsceneManager не найден в сцене!");
            return;
        }
        
        if (string.IsNullOrEmpty(cutsceneKey))
        {
            Debug.LogWarning("[AutoStartCutscene] ⚠️ Ключ катсцены не указан!");
            return;
        }
        
        Debug.Log($"[AutoStartCutscene] 🧪 Тестирую запуск катсцены: {cutsceneKey}");
        StartCutscene();
    }
}
