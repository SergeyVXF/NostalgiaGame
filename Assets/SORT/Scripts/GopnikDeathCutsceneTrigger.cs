using UnityEngine;
using Invector;

/// <summary>
/// Скрипт для запуска катсцены после смерти гопника
/// </summary>
public class GopnikDeathCutsceneTrigger : MonoBehaviour
{
    [Tooltip("Ключ катсцены для запуска после смерти")]
    public string cutsceneKey = "GopnikChase_05";
    
    [Tooltip("Задержка перед запуском катсцены (в секундах)")]
    public float cutsceneDelay = 0.5f;
    
    [Tooltip("Запускать катсцену только один раз")]
    public bool triggerOnce = true;
    
    // Флаг отслеживания первого запуска
    private bool wasTriggered = false;
    
    // Ссылки на компоненты
    private vHealthController healthController;
    private CutsceneManager cutsceneManager;
    private CutsceneFadeManager fadeManager;
    
    private void Start()
    {
        // Находим компонент здоровья (в системе Invector)
        healthController = GetComponent<vHealthController>();
        if (healthController == null)
        {
            Debug.LogError($"[GopnikDeathCutsceneTrigger] Компонент vHealthController не найден на объекте {gameObject.name}. Скрипт не будет работать!");
            enabled = false;
            return;
        }
        
        // Находим менеджер катсцен
        cutsceneManager = FindObjectOfType<CutsceneManager>();
        if (cutsceneManager == null)
        {
            Debug.LogError($"[GopnikDeathCutsceneTrigger] CutsceneManager не найден в сцене! Скрипт не будет работать!");
            enabled = false;
            return;
        }
        
        // Находим менеджер затемнения
        fadeManager = FindObjectOfType<CutsceneFadeManager>();
        if (fadeManager == null)
        {
            Debug.LogWarning($"[GopnikDeathCutsceneTrigger] CutsceneFadeManager не найден в сцене! Катсцена будет запускаться без затемнения.");
        }
        
        // Подписываемся на событие смерти
        healthController.onDead.AddListener(OnGopnikDeath);
        
        Debug.Log($"[GopnikDeathCutsceneTrigger] Скрипт инициализирован на объекте {gameObject.name}. Ожидаю смерть для запуска катсцены {cutsceneKey}");
    }
    
    private void OnDestroy()
    {
        // Отписываемся от события при уничтожении объекта
        if (healthController != null)
        {
            healthController.onDead.RemoveListener(OnGopnikDeath);
        }
    }
    
    /// <summary>
    /// Вызывается при смерти гопника
    /// </summary>
    private void OnGopnikDeath(GameObject deadGopnik)
    {
        // Проверяем, был ли уже запущен триггер и надо ли запускать только один раз
        if (triggerOnce && wasTriggered)
        {
            return;
        }
        
        Debug.Log($"[GopnikDeathCutsceneTrigger] Гопник {gameObject.name} умер! Запускаю катсцену {cutsceneKey} с задержкой {cutsceneDelay} сек.");
        
        // Устанавливаем флаг запуска
        wasTriggered = true;
        
        // Запускаем катсцену с указанной задержкой
        Invoke("PlayCutscene", cutsceneDelay);
    }
    
    /// <summary>
    /// Запускает катсцену
    /// </summary>
    private void PlayCutscene()
    {
        if (fadeManager != null)
        {
            // Запускаем катсцену через CutsceneFadeManager, если он доступен
            fadeManager.StartCutsceneWithFade(cutsceneKey);
        }
        else if (cutsceneManager != null)
        {
            // Запускаем катсцену напрямую через CutsceneManager
            cutsceneManager.StartCutscene(cutsceneKey);
        }
    }
}