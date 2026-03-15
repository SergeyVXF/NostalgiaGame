using UnityEngine;
using UnityEngine.Events;

public class SaltoQuestManager : MonoBehaviour
{
    public SaltoTriggerZone[] triggerZones;  // Массив триггерных зон
    private int currentZoneIndex = 0;         // Индекс текущей активной зоны
    
    public UnityEvent onQuestComplete;        // Событие завершения квеста
    public UnityEvent<int> onZoneComplete;    // Событие завершения зоны (параметр - номер зоны)

    void Start()
    {
        // Деактивируем все зоны кроме первой
        for (int i = 0; i < triggerZones.Length; i++)
        {
            triggerZones[i].gameObject.SetActive(i == 0);
            triggerZones[i].zoneIndex = i;
            triggerZones[i].onZoneComplete.AddListener(OnTriggerZoneComplete);
        }
    }

    void OnTriggerZoneComplete(int zoneIndex)
    {
        if (zoneIndex != currentZoneIndex) return;

        onZoneComplete?.Invoke(currentZoneIndex);
        currentZoneIndex++;

        // Проверяем, завершен ли квест
        if (currentZoneIndex >= triggerZones.Length)
        {
            onQuestComplete?.Invoke();
            return;
        }

        // Активируем следующую зону
        triggerZones[currentZoneIndex].gameObject.SetActive(true);
    }

    // Метод для сброса квеста
    public void ResetQuest()
    {
        currentZoneIndex = 0;
        for (int i = 0; i < triggerZones.Length; i++)
        {
            triggerZones[i].gameObject.SetActive(i == 0);
            triggerZones[i].ResetZone();
        }
    }
} 