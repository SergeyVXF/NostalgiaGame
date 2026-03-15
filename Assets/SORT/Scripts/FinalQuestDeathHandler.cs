using UnityEngine;
using Invector;
using Invector.vCharacterController;

public class FinalQuestDeathHandler : MonoBehaviour
{
    [Header("Настройки")]
    public GameObject finishingMoveTriggerPrefab;
    public float triggerHeight = 1.5f;
    public GameObject finalQuestTrigger02Prefab;
    
    private vHealthController healthController;
    private bool isDead = false;
    private static bool finalEnemyAI02Dead = false;
    private static bool finalEnemyAI01Dead = false;
    private static bool animationStarted = false;
    private static FinalQuestDeathHandler instance;
    private GameObject finishingMoveTrigger;
    
    // Сохраняем позиции врагов
    private static Vector3 enemy1Position;
    private static Vector3 enemy2Position;
    private static GameObject staticFinalQuestTrigger02Prefab;

    private void Awake()
    {
        Debug.Log($"FinalQuestDeathHandler: Awake вызван на {gameObject.name}");
        
        if (instance == null)
        {
            instance = this;
            Debug.Log($"FinalQuestDeathHandler: Установлен новый instance на {gameObject.name}");
        }
        
        if (staticFinalQuestTrigger02Prefab == null)
        {
            staticFinalQuestTrigger02Prefab = finalQuestTrigger02Prefab;
            Debug.Log($"FinalQuestDeathHandler: Префаб FinalQuest_Trigger_02 {(staticFinalQuestTrigger02Prefab != null ? "назначен" : "НЕ назначен")}");
        }
        
        healthController = GetComponent<vHealthController>();
        if (healthController == null)
        {
            Debug.LogError($"FinalQuestDeathHandler: vHealthController не найден на {gameObject.name}");
            return;
        }
        
        healthController.onDead.AddListener(HandleDeath);
        Debug.Log($"FinalQuestDeathHandler: Подписан на событие смерти для {gameObject.name}");
    }

    private void HandleDeath(GameObject deadObject)
    {
        if (!isDead)
        {
            isDead = true;
            Debug.Log($"FinalQuestDeathHandler: {gameObject.name} умер");
            CreateFinishingMoveTrigger();

            if (gameObject.name == "Final_EnemyAI_02")
            {
                finalEnemyAI02Dead = true;
                enemy2Position = transform.position;
                Debug.Log($"FinalQuestDeathHandler: Final_EnemyAI_02 умер. Позиция сохранена: {enemy2Position}");
            }
            else if (gameObject.name == "Final_EnemyAI_01")
            {
                finalEnemyAI01Dead = true;
                enemy1Position = transform.position;
                Debug.Log($"FinalQuestDeathHandler: Final_EnemyAI_01 умер. Позиция сохранена: {enemy1Position}");
            }
            
            Debug.Log($"FinalQuestDeathHandler: Статус смерти - Enemy1={finalEnemyAI01Dead}, Enemy2={finalEnemyAI02Dead}");
            
            // Проверяем условия для создания FinalQuest_Trigger_02
            if (finalEnemyAI01Dead && finalEnemyAI02Dead && animationStarted)
            {
                Debug.Log("FinalQuestDeathHandler: Все условия выполнены, создаем FinalQuest_Trigger_02");
                CreateFinalQuestTrigger02();
            }
        }
    }

    private void CreateFinishingMoveTrigger()
    {
        Debug.Log($"Creating FinishingMoveTrigger for {gameObject.name}");
        // Создаем триггерную зону над врагом
        Vector3 triggerPosition = transform.position + Vector3.up * triggerHeight;
        finishingMoveTrigger = Instantiate(finishingMoveTriggerPrefab, triggerPosition, Quaternion.identity);
        
        // Настраиваем триггер
        FinishingMoveTrigger finishingTrigger = finishingMoveTrigger.GetComponent<FinishingMoveTrigger>();
        if (finishingTrigger != null)
        {
            finishingTrigger.SetTargetEnemy(gameObject);
            Debug.Log($"FinishingMoveTrigger configured for {gameObject.name}");
        }
        else
        {
             Debug.LogError("FinishingMoveTrigger component not found on prefab!");
        }
    }

    public static void OnGreatSwordCastingComplete()
    {
        Debug.Log("FinalQuestDeathHandler: Анимация GreatSwordCasting завершена");
        animationStarted = true;
        
        if (finalEnemyAI01Dead && finalEnemyAI02Dead)
        {
            Debug.Log("FinalQuestDeathHandler: Оба врага мертвы, создаем FinalQuest_Trigger_02");
            CreateFinalQuestTrigger02();
        }
        else
        {
            Debug.Log($"FinalQuestDeathHandler: Не все враги мертвы. Enemy1={finalEnemyAI01Dead}, Enemy2={finalEnemyAI02Dead}");
        }
    }

    private static void CreateFinalQuestTrigger02()
    {
        Debug.Log("FinalQuestDeathHandler: Создание FinalQuest_Trigger_02");
        
        if (staticFinalQuestTrigger02Prefab == null)
        {
            Debug.LogError("FinalQuestDeathHandler: Префаб FinalQuest_Trigger_02 не назначен!");
            return;
        }

        if (enemy1Position == Vector3.zero)
        {
            Debug.LogError("FinalQuestDeathHandler: Позиция Final_EnemyAI_01 не сохранена!");
            return;
        }

        Debug.Log($"FinalQuestDeathHandler: Создаем FinalQuest_Trigger_02 в позиции {enemy1Position}");
        GameObject trigger = Instantiate(staticFinalQuestTrigger02Prefab, enemy1Position, Quaternion.identity);
        
        if (trigger == null)
        {
            Debug.LogError("FinalQuestDeathHandler: Не удалось создать FinalQuest_Trigger_02!");
            return;
        }
        trigger.SetActive(true);
        Debug.Log("FinalQuestDeathHandler: FinalQuest_Trigger_02 успешно создан");
    }

    private void OnDestroy()
    {
         Debug.Log($"FinalQuestDeathHandler destroyed on {gameObject.name}.");
        if (healthController != null)
        {
            healthController.onDead.RemoveListener(HandleDeath);
        }
        // Сбрасываем instance, если это был он
        if (instance == this)
        {
            Debug.Log($"Instance reference cleared by {gameObject.name}.");
            instance = null;
        }
    }
} 