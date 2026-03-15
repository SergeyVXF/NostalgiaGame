using UnityEngine;
using System.Collections;

public class MoneyCollectible : MonoBehaviour
{
    [SerializeField] private int moneyAmount = 20;
    [SerializeField] private float collectRadius = 2f;
    [SerializeField] private float moveSpeed = 10f;
    [SerializeField] private GameObject collectEffect;
    [SerializeField] private float rotationSpeed = 100f;
    [SerializeField] private float bobSpeed = 1f;
    [SerializeField] private float bobHeight = 0.2f;
    
    private bool isCollected = false;
    private bool isMovingToPlayer = false;
    private GameObject player;
    private Vector3 startPosition;
    private float bobTime;
    
    private void Start()
    {
        // Добавляем коллайдер для триггера
        SphereCollider triggerCollider = gameObject.AddComponent<SphereCollider>();
        triggerCollider.isTrigger = true;
        triggerCollider.radius = collectRadius;
        
        // Сохраняем начальную позицию для эффекта покачивания
        startPosition = transform.position;
        
        // Находим игрока
        player = GameObject.FindGameObjectWithTag("Player");
    }
    
    private void Update()
    {
        if (isCollected) return;
        
        // Вращаем объект
        transform.Rotate(Vector3.up * rotationSpeed * Time.deltaTime);
        
        // Эффект покачивания
        if (!isMovingToPlayer)
        {
            bobTime += Time.deltaTime * bobSpeed;
            float newY = startPosition.y + Mathf.Sin(bobTime) * bobHeight;
            transform.position = new Vector3(transform.position.x, newY, transform.position.z);
        }
        
        // Движение к игроку
        if (isMovingToPlayer && player != null)
        {
            Vector3 direction = (player.transform.position - transform.position).normalized;
            transform.position += direction * moveSpeed * Time.deltaTime;
            
            // Проверяем расстояние до игрока
            float distance = Vector3.Distance(transform.position, player.transform.position);
            if (distance < 0.5f)
            {
                CollectMoney();
            }
        }
    }
    
    public void SetMoneyAmount(int amount)
    {
        moneyAmount = amount;
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (isCollected) return;
        
        if (other.CompareTag("Player"))
        {
            isMovingToPlayer = true;
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isMovingToPlayer = false;
        }
    }
    
    private void CollectMoney()
    {
        if (isCollected) return;
        
        isCollected = true;
        
        // Добавляем деньги
        if (MoneySystem.Instance != null)
        {
            MoneySystem.Instance.AddMoney(moneyAmount);
        }
        
        // Создаем эффект сбора
        if (collectEffect != null)
        {
            Instantiate(collectEffect, transform.position, Quaternion.identity);
        }
        
        // Уничтожаем объект
        Destroy(gameObject);
    }
} 