using UnityEngine;

/// <summary>
/// Триггер для запуска катсцены GopnikChase_03 при столкновении с EnemyGopnik_runner
/// </summary>
public class GopnikChaseCutsceneTrigger : MonoBehaviour
{
    [Tooltip("Ключ катсцены в CutsceneManager")]
    public string cutsceneKey = "GopnikChase_03";
    
    [Tooltip("Тег объекта, который активирует триггер")]
    public string targetTag = "Enemy";
    
    [Tooltip("Запустить катсцену только один раз")]
    public bool triggerOnce = true;
    
    [Tooltip("Деактивировать объект, который вошел в триггер")]
    public bool deactivateColliderObject = true;
    
    private bool wasTriggered = false;
    private CutsceneManager cutsceneManager;
    private CutsceneFadeManager fadeManager;
    
    private void Start()
    {
        // Получаем ссылку на CutsceneManager
        cutsceneManager = FindObjectOfType<CutsceneManager>();
        if (cutsceneManager == null)
        {
            Debug.LogError($"[GopnikChaseCutsceneTrigger] CutsceneManager не найден в сцене! Отключаю скрипт на {gameObject.name}");
            enabled = false;
            return;
        }
        
        // Получаем ссылку на CutsceneFadeManager
        fadeManager = FindObjectOfType<CutsceneFadeManager>();
        if (fadeManager == null)
        {
            Debug.LogError($"[GopnikChaseCutsceneTrigger] CutsceneFadeManager не найден в сцене! Отключаю скрипт на {gameObject.name}");
            enabled = false;
            return;
        }
        
        // Проверяем наличие Collider
        Collider collider = GetComponent<Collider>();
        if (collider == null)
        {
            Debug.LogWarning($"[GopnikChaseCutsceneTrigger] На объекте {gameObject.name} нет коллайдера. Добавляю BoxCollider.");
            collider = gameObject.AddComponent<BoxCollider>();
        }
        
        // Убеждаемся, что коллайдер - триггер
        if (!collider.isTrigger)
        {
            Debug.LogWarning($"[GopnikChaseCutsceneTrigger] Коллайдер на {gameObject.name} не является триггером. Устанавливаю isTrigger = true.");
            collider.isTrigger = true;
        }
        
        Debug.Log($"[GopnikChaseCutsceneTrigger] Инициализирован на {gameObject.name}, ожидаю объект с тегом {targetTag}");
    }
    
    private void OnTriggerEnter(Collider other)
    {
        // Проверяем условия для запуска катсцены
        if (other.CompareTag(targetTag) && (!triggerOnce || !wasTriggered))
        {
            Debug.Log($"[GopnikChaseCutsceneTrigger] Объект {other.name} с тегом {targetTag} вошел в триггер {gameObject.name}");
            
            // Запускаем катсцену
            fadeManager.StartCutsceneWithFade(cutsceneKey);
            
            // Обновляем флаг
            wasTriggered = true;
            
            // Деактивируем объект, который вошел в триггер (например, EnemyGopnik_runner)
            if (deactivateColliderObject)
            {
                Debug.Log($"[GopnikChaseCutsceneTrigger] Деактивирую объект {other.gameObject.name} после входа в триггер");
                other.gameObject.SetActive(false);
            }
            
            // Опционально можно отключить триггер
            if (triggerOnce)
            {
                // Отключаем только коллайдер, чтобы скрипт продолжал работать
                GetComponent<Collider>().enabled = false;
            }
        }
    }
    
    // Публичный метод для сброса состояния
    public void ResetTrigger()
    {
        wasTriggered = false;
        GetComponent<Collider>().enabled = true;
    }
}