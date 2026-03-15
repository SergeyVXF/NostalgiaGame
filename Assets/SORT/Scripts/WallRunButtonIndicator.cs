using UnityEngine;
using TMPro;

public class WallRunButtonIndicator : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI indicatorText;
    private bool isPlayerInTrigger = false;
    
    private void Start()
    {
        if (indicatorText == null)
        {
            indicatorText = GetComponentInChildren<TextMeshProUGUI>();
            if (indicatorText == null)
            {
                Debug.LogError("TextMeshProUGUI компонент не найден!");
                return;
            }
        }
        
        indicatorText.gameObject.SetActive(false);
        gameObject.SetActive(false);
        
        // Подписываемся на событие изменения счетчика
        KrapivaCounter.OnCounterChanged += CheckKrapivaCount;
    }
    
    private void OnDestroy()
    {
        // Отписываемся от события при уничтожении объекта
        KrapivaCounter.OnCounterChanged -= CheckKrapivaCount;
    }
    
    private void CheckKrapivaCount(int count)
    {
        // Если уничтожено достаточно крапивы, активируем объект
        if (count >= 3) // Измените это число на нужное количество
        {
            gameObject.SetActive(true);
            Debug.Log("WallRunButtonIndicator: Активирован после уничтожения крапивы");
        }
    }
    
    private void Update()
    {
        if (isPlayerInTrigger)
        {
            indicatorText.gameObject.SetActive(true);
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInTrigger = true;
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInTrigger = false;
            indicatorText.gameObject.SetActive(false);
        }
    }
} 