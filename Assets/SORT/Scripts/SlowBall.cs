using UnityEngine;
using Invector;
using Invector.vCharacterController;
using Invector.vCharacterController.AI;

public class SlowBall : MonoBehaviour
{
    [Header("Настройки")]
    public float speed = 25f;
    public float slowAmount = 0.2f;
    public float slowDuration = 3f;
    public float minAnimationSpeed = 0.2f; // Минимальная скорость анимации
    
    private Rigidbody rb;
    private Vector3 initialDirection;
    private bool isInitialized = false;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            // Настраиваем физику
            rb.useGravity = true;
            rb.isKinematic = false;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
        }
    }

    private void Start()
    {
        Debug.Log($"SlowBall: Start вызван на {gameObject.name}");
        
        // Если направление было установлено до Start, применяем его сейчас
        if (isInitialized)
        {
            rb.linearVelocity = initialDirection * speed;
            Debug.Log($"SlowBall: Применена отложенная скорость {rb.linearVelocity}");
        }
        
        // Уничтожаем шарик через 5 секунд
        Destroy(gameObject, 5f);
    }

    public void Initialize(Vector3 direction)
    {
        Debug.Log($"SlowBall: Initialize вызван. Направление = {direction}, Скорость = {speed}");
        
        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }

        // Сохраняем направление и устанавливаем скорость
        initialDirection = direction;
        isInitialized = true;

        // Если Start уже был вызван, применяем скорость сразу
        if (rb != null)
        {
            rb.linearVelocity = direction * speed;
            Debug.Log($"SlowBall: Установлена скорость {rb.linearVelocity}");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"SlowBall: Столкновение с {other.gameObject.name}");
        
        if (other.CompareTag("Enemy") && other.gameObject.name == "Final_EnemyAI_03")
        {
            Debug.Log("SlowBall: Попадание в Final_EnemyAI_03!");
            var enemyController = other.GetComponent<vHealthController>();
            if (enemyController != null)
            {
                var aiController = other.GetComponent<v_AIController>();
                if (aiController != null)
                {
                    // Замедляем AI контроллер через изменение скорости анимации
                    float newSpeed = aiController.animator.speed * (1f - slowAmount);
                    // Проверяем минимальную скорость
                    newSpeed = Mathf.Max(newSpeed, minAnimationSpeed);
                    aiController.animator.speed = newSpeed;
                    
                    Debug.Log($"SlowBall: Применено замедление. Новая скорость: {newSpeed}");
                    
                    // Сохраняем текущую скорость для восстановления
                    StartCoroutine(ResetSpeed(aiController, aiController.animator.speed));
                }
                else
                {
                    Debug.LogError("SlowBall: ОШИБКА - v_AIController не найден на враге!");
                }
            }
            else
            {
                Debug.LogError("SlowBall: ОШИБКА - vHealthController не найден на враге!");
            }
            
            Destroy(gameObject);
        }
    }

    private System.Collections.IEnumerator ResetSpeed(v_AIController aiController, float slowedSpeed)
    {
        yield return new WaitForSeconds(slowDuration);
        if (aiController != null)
        {
            // Восстанавливаем оригинальную скорость
            aiController.animator.speed = slowedSpeed / (1f - slowAmount);
            Debug.Log($"SlowBall: Скорость врага восстановлена до {aiController.animator.speed}");
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Уничтожаем шарик при столкновении с любым объектом, кроме врага
        if (!collision.gameObject.CompareTag("Enemy"))
        {
            Debug.Log($"SlowBall: Физическое столкновение с {collision.gameObject.name}");
            Destroy(gameObject);
        }
    }
} 