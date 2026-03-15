using UnityEngine;

/// <summary>
/// Контроллер для объектов K_01, который управляет их поведением
/// </summary>
public class K_01Controller : MonoBehaviour
{
    [Header("Базовые настройки")]
    [Tooltip("Время жизни объекта в секундах (0 = бесконечно)")]
    public float lifeTime = 0f;
    
    [Tooltip("Скорость вращения объекта (в градусах в секунду)")]
    public float rotationSpeed = 15f;
    
    [Tooltip("Случайный диапазон отклонения от стандартной скорости вращения")]
    public float rotationSpeedVariation = 5f;
    
    [Header("Физика")]
    [Tooltip("Сила гравитации для объекта (0 = стандартная физика)")]
    public float gravityMultiplier = 1f;
    
    [Tooltip("Максимальная начальная скорость по осям XZ")]
    public float initialMaxVelocity = 0.5f;
    
    [Header("События")]
    [Tooltip("Разрушать объект при столкновении на большой скорости")]
    public bool destroyOnImpact = false;
    
    [Tooltip("Минимальная сила столкновения для разрушения")]
    public float minImpactForce = 2f;
    
    // Приватные переменные
    private Rigidbody rb;
    private float actualRotationSpeed;
    private float timer = 0f;
    private Vector3 rotationAxis;
    
    void Start()
    {
        // Получаем компонент Rigidbody
        rb = GetComponent<Rigidbody>();
        
        // Настраиваем случайное вращение
        actualRotationSpeed = rotationSpeed + Random.Range(-rotationSpeedVariation, rotationSpeedVariation);
        rotationAxis = new Vector3(
            Random.Range(-1f, 1f),
            Random.Range(-1f, 1f),
            Random.Range(-1f, 1f)
        ).normalized;
        
        // Добавляем начальную скорость, если есть Rigidbody
        if (rb != null && !rb.isKinematic)
        {
            // Случайный импульс при создании
            rb.linearVelocity = new Vector3(
                Random.Range(-initialMaxVelocity, initialMaxVelocity),
                0,
                Random.Range(-initialMaxVelocity, initialMaxVelocity)
            );
        }
    }
    
    void Update()
    {
        // Вращаем объект
        transform.Rotate(rotationAxis, actualRotationSpeed * Time.deltaTime);
        
        // Проверяем время жизни объекта
        if (lifeTime > 0)
        {
            timer += Time.deltaTime;
            if (timer >= lifeTime)
            {
                Destroy(gameObject);
            }
        }
    }
    
    void FixedUpdate()
    {
        // Применяем кастомную гравитацию, если нужно
        if (rb != null && !rb.isKinematic && gravityMultiplier != 1f)
        {
            rb.AddForce(Physics.gravity * gravityMultiplier, ForceMode.Acceleration);
        }
    }
    
    void OnCollisionEnter(Collision collision)
    {
        // Проверяем, должен ли объект разрушаться при столкновении
        if (destroyOnImpact && collision.relativeVelocity.magnitude > minImpactForce)
        {
            // Можно добавить эффекты разрушения
            Destroy(gameObject);
        }
    }
} 