using UnityEngine;

public class TempoPickup : MonoBehaviour
{
    [Header("Настройки")]
    [Tooltip("Эффект при подборе предмета")]
    [SerializeField] private GameObject pickupEffect;
    
    [Tooltip("Звук при подборе предмета")]
    [SerializeField] private AudioClip pickupSound;
    
    [Header("Анимация")]
    [Tooltip("Скорость вращения объекта (градусов в секунду)")]
    [SerializeField] private float rotationSpeed = 90f;
    
    [Tooltip("Амплитуда движения вверх-вниз")]
    [SerializeField] private float hoverAmplitude = 0.2f;
    
    [Tooltip("Скорость движения вверх-вниз")]
    [SerializeField] private float hoverSpeed = 1f;
    
    // Начальная позиция объекта
    private Vector3 initialPosition;
    
    private void Start()
    {
        // Запоминаем начальную позицию
        initialPosition = transform.position;
    }
    
    private void Update()
    {
        // Вращение объекта
        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);
        
        // Движение вверх-вниз
        float newY = initialPosition.y + Mathf.Sin(Time.time * hoverSpeed) * hoverAmplitude;
        transform.position = new Vector3(initialPosition.x, newY, initialPosition.z);
    }
    
    private void OnTriggerEnter(Collider other)
    {
        // Проверяем, является ли объект игроком
        if (other.CompareTag("Player"))
        {
            // Добавляем предмет в инвентарь
            if (TempoItem.Instance != null)
            {
                TempoItem.Instance.AddTempo();
                Debug.Log("[TempoPickup] Игрок подобрал предмет Tempo");
                
                // Проигрываем звук, если он назначен
                if (pickupSound != null)
                {
                    AudioSource.PlayClipAtPoint(pickupSound, transform.position);
                }
                
                // Создаем эффект, если он назначен
                if (pickupEffect != null)
                {
                    Instantiate(pickupEffect, transform.position, transform.rotation);
                }
                
                // Уничтожаем объект
                Destroy(gameObject);
            }
            else
            {
                Debug.LogError("[TempoPickup] TempoItem.Instance не найден!");
            }
        }
    }
}