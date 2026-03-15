using UnityEngine;
using Invector;

public class MoneyDrop : MonoBehaviour
{
    [SerializeField] private int minMoneyAmount = 15;
    [SerializeField] private int maxMoneyAmount = 25;
    [SerializeField] private GameObject moneyPrefab;
    [SerializeField] private float dropForce = 5f;
    [SerializeField] private float dropRadius = 1f;
    [SerializeField] private int dropCount = 1;
    
    private vHealthController healthController;
    
    private void Start()
    {
        // Получаем компонент vHealthController
        healthController = GetComponent<vHealthController>();
        if (healthController != null)
        {
            // Подписываемся на событие смерти
            healthController.onDead.AddListener(OnEnemyDeath);
        }
        else
        {
            Debug.LogError("vHealthController не найден на объекте " + gameObject.name);
        }
    }
    
    private void OnDestroy()
    {
        // Отписываемся от события смерти
        if (healthController != null)
        {
            healthController.onDead.RemoveListener(OnEnemyDeath);
        }
    }
    
    private void OnEnemyDeath(GameObject enemy)
    {
        if (moneyPrefab == null)
        {
            Debug.LogError("Префаб денег не назначен!");
            return;
        }
        
        // Создаем несколько денег
        for (int i = 0; i < dropCount; i++)
        {
            // Генерируем случайное количество денег
            int moneyAmount = Random.Range(minMoneyAmount, maxMoneyAmount + 1);
            
            // Генерируем случайную позицию в радиусе
            Vector3 randomOffset = Random.insideUnitSphere * dropRadius;
            Vector3 dropPosition = transform.position + randomOffset;
            
            // Создаем объект денег
            GameObject moneyObj = Instantiate(moneyPrefab, dropPosition, Quaternion.Euler(Random.Range(0f, 360f), Random.Range(0f, 360f), Random.Range(0f, 360f)));
            
            // Добавляем компонент для сбора денег
            MoneyCollectible moneyCollectible = moneyObj.GetComponent<MoneyCollectible>();
            if (moneyCollectible == null)
            {
                moneyCollectible = moneyObj.AddComponent<MoneyCollectible>();
            }
            moneyCollectible.SetMoneyAmount(moneyAmount);
            
            // Добавляем физику для разлета
            Rigidbody rb = moneyObj.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = moneyObj.AddComponent<Rigidbody>();
            }
            
            // Настраиваем физику
            rb.mass = 0.1f;
            rb.linearDamping = 0.5f;
            rb.angularDamping = 0.5f;
            
            // Добавляем случайную силу для разлета
            Vector3 randomDirection = new Vector3(
                Random.Range(-1f, 1f),
                Random.Range(0.5f, 1f),
                Random.Range(-1f, 1f)
            ).normalized;
            
            rb.AddForce(randomDirection * dropForce, ForceMode.Impulse);
            rb.AddTorque(Random.insideUnitSphere * dropForce, ForceMode.Impulse);
            
            // Уничтожаем объект через некоторое время, если его не подобрали
            Destroy(moneyObj, 30f);
        }
    }
} 