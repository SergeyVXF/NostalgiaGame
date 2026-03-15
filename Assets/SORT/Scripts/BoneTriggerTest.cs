using UnityEngine;

public class BoneTriggerTest : MonoBehaviour
{
    [Header("Тестирование косточки")]
    [Tooltip("Принудительно вызвать срабатывание триггера")]
    public bool testTrigger = false;
    
    private BoneBehavior boneBehavior;
    private DogZoneTrigger[] dogTriggers;
    
    private void Start()
    {
        boneBehavior = GetComponent<BoneBehavior>();
        dogTriggers = FindObjectsOfType<DogZoneTrigger>();
        
        Debug.Log($"[BoneTriggerTest] Найдено {dogTriggers.Length} DogZoneTrigger'ов");
        foreach (var trigger in dogTriggers)
        {
            Debug.Log($"[BoneTriggerTest] Триггер: {trigger.name} на позиции {trigger.transform.position}");
        }
    }
    
    private void Update()
    {
        if (testTrigger)
        {
            testTrigger = false;
            TestBoneTrigger();
        }
    }
    
    [ContextMenu("Тестировать триггер косточки")]
    public void TestBoneTrigger()
    {
        Debug.Log($"[BoneTriggerTest] Начинаю тестирование триггера косточки {gameObject.name}");
        Debug.Log($"[BoneTriggerTest] Позиция косточки: {transform.position}");
        Debug.Log($"[BoneTriggerTest] Тег косточки: '{gameObject.tag}'");
        
        if (boneBehavior == null)
        {
            Debug.LogError($"[BoneTriggerTest] BoneBehavior не найден на {gameObject.name}!");
            return;
        }
        
        Debug.Log($"[BoneTriggerTest] BoneBehavior найден, IsBeingEaten: {boneBehavior.IsBeingEaten()}");
        
        // Найдем ближайший триггер
        DogZoneTrigger closestTrigger = null;
        float minDistance = float.MaxValue;
        
        foreach (var trigger in dogTriggers)
        {
            float distance = Vector3.Distance(transform.position, trigger.transform.position);
            Debug.Log($"[BoneTriggerTest] Расстояние до триггера {trigger.name}: {distance:F2}м");
            
            if (distance < minDistance)
            {
                minDistance = distance;
                closestTrigger = trigger;
            }
        }
        
        if (closestTrigger != null)
        {
            Debug.Log($"[BoneTriggerTest] Ближайший триггер: {closestTrigger.name} на расстоянии {minDistance:F2}м");
            Debug.Log($"[BoneTriggerTest] Принудительно вызываю OnTriggerEnter...");
            
            // Принудительно вызываем OnTriggerEnter
            var collider = GetComponent<Collider>();
            if (collider != null)
            {
                closestTrigger.SendMessage("OnTriggerEnter", collider, SendMessageOptions.DontRequireReceiver);
            }
            else
            {
                Debug.LogError($"[BoneTriggerTest] Коллайдер не найден на {gameObject.name}!");
            }
        }
        else
        {
            Debug.LogError($"[BoneTriggerTest] Не найдено ни одного DogZoneTrigger!");
        }
    }
}
