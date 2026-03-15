using UnityEngine;

/// <summary>
/// Вспомогательный скрипт для триггерных зон Gopnik Chase
/// Устанавливается на объект триггерной зоны и сообщает контроллеру о входе игрока
/// </summary>
public class GopnikChaseTriggerHelper : MonoBehaviour
{
    [Tooltip("Контроллер, управляющий триггерными зонами")]
    [SerializeField] private GopnikChaseTriggerController controller;
    
    [Tooltip("Является ли этот триггер первой зоной (Gopnik_Chase_quest)")]
    [SerializeField] private bool isFirstTrigger = true;
    
    [Tooltip("Тег объекта, который может активировать триггер")]
    [SerializeField] private string targetTag = "Player";
    
    private void Start()
    {
        // Если контроллер не назначен, пытаемся найти его в сцене
        if (controller == null)
        {
            controller = FindObjectOfType<GopnikChaseTriggerController>();
            if (controller == null)
            {
                Debug.LogError($"[GopnikChaseTriggerHelper] GopnikChaseTriggerController не найден! Отключаю скрипт на {gameObject.name}");
                enabled = false;
                return;
            }
        }
        
        // Проверяем наличие коллайдера
        Collider collider = GetComponent<Collider>();
        if (collider == null)
        {
            Debug.LogWarning($"[GopnikChaseTriggerHelper] Объект {gameObject.name} не имеет компонента Collider. Добавляю BoxCollider.");
            collider = gameObject.AddComponent<BoxCollider>();
            collider.isTrigger = true;
        }
        
        // Убеждаемся, что коллайдер является триггером
        if (!collider.isTrigger)
        {
            Debug.LogWarning($"[GopnikChaseTriggerHelper] Коллайдер на {gameObject.name} не является триггером. Устанавливаю isTrigger = true.");
            collider.isTrigger = true;
        }
        
        Debug.Log($"[GopnikChaseTriggerHelper] Инициализация скрипта на {gameObject.name}, isFirstTrigger={isFirstTrigger}");
    }
    
    private void OnTriggerEnter(Collider other)
    {
        // Проверяем, что объект имеет правильный тег
        if (other.CompareTag(targetTag))
        {
            Debug.Log($"[GopnikChaseTriggerHelper] Игрок вошел в триггер {gameObject.name}");
            
            if (isFirstTrigger && controller != null)
            {
                // Сообщаем контроллеру, что игрок вошел в первую триггерную зону
                controller.PlayerEnteredFirstTrigger();
            }
        }
    }
}