using UnityEngine;
using Invector.vCharacterController;

public class BallThrower : MonoBehaviour
{
    [Header("Настройки")]
    public GameObject slowBallPrefab;
    public Transform throwPoint;
    public float throwForce = 20f;
    public float cooldown = 0.5f;
    [Tooltip("Высота спавна шарика относительно игрока")]
    public float spawnHeight = 1.6f; // Примерно уровень головы
    [Tooltip("Активировать сразу при старте (для тестирования)")]
    public bool activateOnStart = true;
    
    private float lastThrowTime;
    private bool isActive = false;

    private void Awake()
    {
        Debug.Log($"BallThrower: Awake вызван на {gameObject.name}");
    }

    private void Start()
    {
        Debug.Log($"BallThrower: Start вызван на {gameObject.name}");
        if (slowBallPrefab == null)
        {
            Debug.LogError($"BallThrower: ОШИБКА - slowBallPrefab не назначен на {gameObject.name}!");
        }
        if (throwPoint == null)
        {
            Debug.LogError($"BallThrower: ОШИБКА - throwPoint не назначен на {gameObject.name}!");
        }

        if (activateOnStart)
        {
            Debug.Log("BallThrower: Активация через activateOnStart");
            Activate();
        }
    }

    public void Activate()
    {
        isActive = true;
        Debug.Log($"BallThrower: Компонент активирован на {gameObject.name}");
    }

    private void OnEnable()
    {
        Debug.Log($"BallThrower: OnEnable вызван на {gameObject.name}");
    }

    private void Update()
    {
        if (!isActive)
        {
            if (Input.GetKeyDown(KeyCode.G))
            {
                Debug.Log($"BallThrower: Кнопка G нажата, но компонент не активирован на {gameObject.name}");
            }
            return;
        }

        if (Input.GetKeyDown(KeyCode.G))
        {
            Debug.Log($"BallThrower: Кнопка G нажата, пытаемся бросить шар с {gameObject.name}");
            ThrowBall();
        }
    }

    private void ThrowBall()
    {
        if (!isActive)
        {
            Debug.LogError($"BallThrower: Попытка бросить шар, но компонент не активирован на {gameObject.name}");
            return;
        }

        if (slowBallPrefab == null)
        {
            Debug.LogError($"BallThrower: ОШИБКА - slowBallPrefab не назначен на {gameObject.name}");
            return;
        }

        if (throwPoint == null)
        {
            Debug.LogError($"BallThrower: ОШИБКА - throwPoint не назначен на {gameObject.name}");
            return;
        }

        Debug.Log($"BallThrower: Создаем шар на {gameObject.name}");
        
        // Создаем шар на уровне головы игрока
        Vector3 spawnPosition = transform.position + Vector3.up * spawnHeight + transform.forward * 0.5f;
        GameObject ball = Instantiate(slowBallPrefab, spawnPosition, Quaternion.identity);
        
        if (ball == null)
        {
            Debug.LogError($"BallThrower: ОШИБКА - не удалось создать шар на {gameObject.name}");
            return;
        }

        // Инициализируем направление полета
        Vector3 throwDirection = transform.forward + Vector3.up * 0.2f;
        throwDirection.Normalize();
        
        SlowBall slowBall = ball.GetComponent<SlowBall>();
        if (slowBall != null)
        {
            slowBall.Initialize(throwDirection);
            Debug.Log($"BallThrower: Шар инициализирован с направлением {throwDirection} на {gameObject.name}");
        }
        else
        {
            Debug.LogError($"BallThrower: ОШИБКА - компонент SlowBall не найден на префабе на {gameObject.name}");
        }
    }

    // Визуализация направления броска в редакторе
    private void OnDrawGizmos()
    {
        if (throwPoint != null && isActive)
        {
            Gizmos.color = Color.red;
            Vector3 direction = transform.forward;
            Gizmos.DrawRay(throwPoint.position, direction * 5f);
        }
    }

    // Для отладки
    private void OnValidate()
    {
        if (slowBallPrefab == null)
        {
            Debug.LogWarning("BallThrower: Префаб шарика не назначен в инспекторе!");
        }
    }
} 