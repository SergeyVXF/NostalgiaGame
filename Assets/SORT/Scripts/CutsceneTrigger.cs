using System.Collections;
using UnityEngine;

public class CutsceneTrigger : MonoBehaviour
{
    [Header("Настройки катсцены")]
    [Tooltip("Ключ катсцены, который соответствует ключу в CutsceneManager")]
    [SerializeField] private string cutsceneKey;
    
    [Tooltip("Должна ли катсцена запускаться только один раз")]
    [SerializeField] private bool triggerOnce = true;
    
    [Tooltip("Задержка перед запуском катсцены (в секундах)")]
    [SerializeField] private float delay = 0f;
    
    [Header("Настройки триггера")]
    [Tooltip("Тег объекта, который может активировать триггер")]
    [SerializeField] private string targetTag = "Player";
    
    [Header("Проверка предметов")]
    [Tooltip("Требуется ли предмет Tempo для запуска этой катсцены")]
    [SerializeField] private bool requiresTempoItem = false;
    
    [Tooltip("Является ли это триггером Quest_ded_end для квеста DED")]
    [SerializeField] private bool isQuestDedEndTrigger = false;
    
    [Header("Деактивация триггера")]
    [Tooltip("Деактивировать триггер после воспроизведения катсцены")]
    [SerializeField] private bool deactivateAfterPlay = true;
    
    private bool hasTriggered = false;
    private CutsceneFadeManager fadeManager;
    private DEDQuest dedQuest;
    
    private void Awake()
    {
        InitializeComponents();
    }
    
    private void OnEnable()
    {
        // Подписываемся на событие окончания катсцены
        if (CutsceneManager.Instance != null)
        {
            CutsceneManager.OnCutsceneEnded += OnCutsceneEnded;
        }
    }
    
    private void OnDisable()
    {
        // Отписываемся от события
        if (CutsceneManager.Instance != null)
        {
            CutsceneManager.OnCutsceneEnded -= OnCutsceneEnded;
        }
    }
    
    private void InitializeComponents()
    {
        // Поиск CutsceneFadeManager
        fadeManager = FindObjectOfType<CutsceneFadeManager>();
        if (fadeManager == null)
        {
            Debug.LogError($"[CutsceneTrigger] CutsceneFadeManager не найден в сцене! Отключаю скрипт на объекте {gameObject.name}");
            enabled = false;
            return;
        }
        
        // Проверка наличия CutsceneManager
        var cutsceneManager = FindObjectOfType<CutsceneManager>();
        if (cutsceneManager == null)
        {
            Debug.LogError($"[CutsceneTrigger] CutsceneManager не найден в сцене! Отключаю скрипт на объекте {gameObject.name}");
            enabled = false;
            return;
        }
        
        // Проверка наличия ключа катсцены
        if (string.IsNullOrEmpty(cutsceneKey))
        {
            Debug.LogError($"[CutsceneTrigger] Ключ катсцены не указан! Отключаю скрипт на объекте {gameObject.name}");
            enabled = false;
            return;
        }
        
        // Если триггер является Quest_ded_end или требуется предмет Tempo, найдем DEDQuest
        if (requiresTempoItem || isQuestDedEndTrigger)
        {
            dedQuest = FindObjectOfType<DEDQuest>();
            if (dedQuest == null)
            {
                Debug.LogError($"[CutsceneTrigger] DEDQuest не найден в сцене, но requiresTempoItem = {requiresTempoItem} или isQuestDedEndTrigger = {isQuestDedEndTrigger}! Отключаю скрипт на объекте {gameObject.name}");
                enabled = false;
                return;
            }
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (!enabled) return;
        
        // Проверяем, что объект имеет правильный тег и триггер еще не был активирован
        if (other.CompareTag(targetTag) && (!triggerOnce || !hasTriggered))
        {
            // Для Quest_ded_end, это обрабатывается в DEDQuest
            if (isQuestDedEndTrigger && dedQuest != null)
            {
                // DEDQuest сам отслеживает игрока в зоне и запускает катсцену
                Debug.Log("[CutsceneTrigger] Вход в зону Quest_ded_end, обработка в DEDQuest");
                return;
            }
            
            // Если требуется предмет Tempo, проверяем его наличие
            if (requiresTempoItem)
            {
                if (dedQuest != null && !dedQuest.HasRequiredItem())
                {
                    Debug.LogWarning($"[CutsceneTrigger] Попытка запустить катсцену {cutsceneKey}, но у игрока нет предмета Tempo!");
                    return; // Не запускаем катсцену, если нет предмета
                }
                else
                {
                    Debug.Log($"[CutsceneTrigger] Запуск катсцены {cutsceneKey} - предмет Tempo найден!");
                }
            }
            
            hasTriggered = true;
            
            if (delay > 0)
            {
                StartCoroutine(StartCutsceneWithDelay());
            }
            else
            {
                ActivateCutscene();
            }
        }
    }
    
    private void OnTriggerStay(Collider other)
    {
        // Только для Quest_ded_end
        if (!enabled || !isQuestDedEndTrigger || dedQuest == null) return;
        
        if (other.CompareTag(targetTag))
        {
            // Вручную вызываем CheckPlayerLocation для обеспечения работы функционала
            dedQuest.CheckPlayerLocationManually(transform.position);
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        // Только для Quest_ded_end
        if (!enabled || !isQuestDedEndTrigger || dedQuest == null) return;
        
        if (other.CompareTag(targetTag))
        {
            // Сообщаем DEDQuest, что игрок вышел из зоны
            dedQuest.PlayerExitQuestZone();
        }
    }
    
    private void OnCutsceneEnded(GameObject cutscene)
    {
        // Проверяем, наша ли это катсцена
        if (deactivateAfterPlay && cutscene.name.Contains(cutsceneKey))
        {
            // Деактивируем триггер
            gameObject.SetActive(false);
            Debug.Log($"[CutsceneTrigger] Триггер {gameObject.name} деактивирован после воспроизведения катсцены {cutsceneKey}");
        }
    }
    
    private IEnumerator StartCutsceneWithDelay()
    {
        yield return new WaitForSeconds(delay);
        ActivateCutscene();
    }
    
    private void ActivateCutscene()
    {
        if (fadeManager == null)
        {
            Debug.LogError($"[CutsceneTrigger] CutsceneFadeManager не инициализирован! Проверьте настройки на объекте {gameObject.name}");
            return;
        }
        
        // Запускаем катсцену через CutsceneFadeManager
        fadeManager.StartCutsceneWithFade(cutsceneKey);
    }
    
    // Метод для сброса триггера (может быть вызван из редактора или из другого скрипта)
    public void ResetTrigger()
    {
        hasTriggered = false;
    }
    
    private void OnDrawGizmos()
    {
        // Отображаем зону триггера в редакторе
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f); // Оранжевый полупрозрачный
        
        BoxCollider boxCollider = GetComponent<BoxCollider>();
        if (boxCollider != null)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(boxCollider.center, boxCollider.size);
            
            // Контур
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.8f); // Более непрозрачный оранжевый
            Gizmos.DrawWireCube(boxCollider.center, boxCollider.size);
        }
        else
        {
            // Если нет BoxCollider, рисуем просто сферу
            Gizmos.DrawSphere(transform.position, 1f);
        }
    }
} 