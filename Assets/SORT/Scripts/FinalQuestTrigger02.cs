using UnityEngine;

public class FinalQuestTrigger02 : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"FinalQuestTrigger02: Объект {other.gameObject.name} вошел в триггер");
        
        if (other.CompareTag("Player"))
        {
            Debug.Log("FinalQuestTrigger02: Игрок вошел в триггер");
            
            var ballThrower = other.GetComponent<BallThrower>();
            if (ballThrower != null)
            {
                Debug.Log("FinalQuestTrigger02: BallThrower найден на игроке");
                ballThrower.Activate();
                Debug.Log("FinalQuestTrigger02: BallThrower активирован");
            }
            else
            {
                Debug.LogError("FinalQuestTrigger02: BallThrower не найден на игроке!");
                // Попробуем добавить компонент
                ballThrower = other.gameObject.AddComponent<BallThrower>();
                if (ballThrower != null)
                {
                    Debug.Log("FinalQuestTrigger02: BallThrower добавлен на игрока");
                    ballThrower.Activate();
                }
                else
                {
                    Debug.LogError("FinalQuestTrigger02: Не удалось добавить BallThrower на игрока!");
                }
            }
            
            // Вместо деактивации всего объекта, отключаем только коллайдер
            Collider triggerCollider = GetComponent<Collider>();
            if (triggerCollider != null)
            {
                triggerCollider.enabled = false;
                Debug.Log("FinalQuestTrigger02: Коллайдер деактивирован");
            }
            else
            {
                Debug.LogError("FinalQuestTrigger02: Не найден коллайдер для деактивации!");
            }
        }
    }
} 