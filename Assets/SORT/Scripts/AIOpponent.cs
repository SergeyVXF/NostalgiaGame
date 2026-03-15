using UnityEngine;
using System.Collections;

public class AIOpponent : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 10f; // Увеличиваем скорость
    [SerializeField] private float rotationSpeed = 10f;
    [SerializeField] private float startDelay = 0.5f; // Небольшая задержка перед началом движения
    [SerializeField] private float jumpForce = 4f; // Уменьшаем силу прыжка
    [SerializeField] private float groundCheckDistance = 2f; // Увеличиваем расстояние проверки
    [SerializeField] private float obstacleCheckDistance = 3f; // Увеличиваем дистанцию проверки препятствий
    [SerializeField] private float maxObstacleHeight = 1f; // Максимальная высота препятствия, по которому можно ходить
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private LayerMask obstacleLayer; // Слой препятствий
    [SerializeField] private Animator animator; // Добавляем ссылку на компонент Animator
    
    [Header("Настройки определения застревания")]
    [Tooltip("Максимальное время (в секундах), в течение которого AI может оставаться неподвижным")]
    [SerializeField] private float maxStuckTime = 1.5f;
    
    [Tooltip("Минимальное расстояние (в единицах), которое AI должен пройти между проверками")]
    [SerializeField] private float minMovementDistance = 0.1f;
    
    [Tooltip("Множитель силы прыжка для выхода из застревания")]
    [SerializeField] private float stuckJumpMultiplier = 1.5f;
    
    [Tooltip("Максимальное количество последовательных прыжков для выхода из застревания")]
    [SerializeField] private int maxConsecutiveJumps = 3;
    
    [Tooltip("Максимальное отклонение от прямого пути к финишу (в градусах)")]
    [SerializeField] private float maxPathDeviation = 45f;
    
    [Tooltip("Время, после которого АИ принудительно считается на земле, если он долго в 'воздухе'")]
    [SerializeField] private float forceGroundedTime = 3f;
    
    private bool isMoving = false;
    private bool isGrounded = true;
    private bool isJumping = false;
    private Transform finishLine;
    private RaceTimer raceTimer;
    private Rigidbody rb;
    private Vector3 moveDirection;
    private Vector3 lastMoveDirection;
    private float currentObstacleHeight = 0f;
    private float timeInAir = 0f;
    private float maxTimeInAir = 2f;
    private float jumpCooldown = 0.5f;
    private float lastJumpTime = 0f;
    private Vector3 lastPosition;
    private Vector3 lastStuckPosition;
    private float stuckTime = 0f;
    private int consecutiveJumps = 0;
    private float lastStuckCheckTime = 0f;
    private bool hasWon = false;
    private bool isCollidingWithGround = false; // Флаг для отслеживания столкновения с землей через физику
    private float stuckInAirTime = 0f; // Время, в течение которого AI застрял и считает, что он в воздухе
    public static event System.Action OnAIVictory;
    
    private void Start()
    {
        Debug.Log("AIOpponent: Инициализация");
        
        // Получаем компонент Animator
        animator = GetComponent<Animator>();
        if (animator == null)
        {
            Debug.LogWarning("AIOpponent: Компонент Animator не найден!");
        }
        
        // Проверяем настройки слоя земли
        if (groundLayer.value == 0)
        {
            Debug.LogWarning("AIOpponent: Слой земли не установлен! Установите слой земли в инспекторе.");
            groundLayer = LayerMask.GetMask("Default"); // Устанавливаем Default слой по умолчанию
        }
        
        // Проверяем настройки слоя препятствий
        if (obstacleLayer.value == 0)
        {
            Debug.LogWarning("AIOpponent: Слой препятствий не установлен! Установите слой препятствий в инспекторе.");
            obstacleLayer = LayerMask.GetMask("Default");
        }
        
        // Получаем компонент Rigidbody
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
            Debug.Log("AIOpponent: Добавлен компонент Rigidbody");
        }
        
        // Настраиваем Rigidbody
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.useGravity = true;
        rb.mass = 100f; // Увеличиваем массу, чтобы AI было сложнее сдвинуть
        rb.linearDamping = 1f; // Добавляем сопротивление для стабильности
        
        // Настраиваем коллайдер
        Collider collider = GetComponent<Collider>();
        if (collider != null)
        {
            // Устанавливаем физический материал для уменьшения трения
            PhysicsMaterial physicMaterial = new PhysicsMaterial("AIOpponentMaterial");
            physicMaterial.dynamicFriction = 0.6f;
            physicMaterial.staticFriction = 0.6f;
            physicMaterial.bounciness = 0f;
            physicMaterial.frictionCombine = PhysicsMaterialCombine.Average;
            physicMaterial.bounceCombine = PhysicsMaterialCombine.Average;
            collider.material = physicMaterial;
        }
        
        // Находим RaceTimer
        raceTimer = FindObjectOfType<RaceTimer>();
        if (raceTimer == null)
        {
            Debug.LogError("AIOpponent: RaceTimer не найден!");
            return;
        }
        
        // Подписываемся на событие начала гонки
        raceTimer.OnRaceStarted += OnRaceStart;
        
        // Отключаем движение при старте
        isMoving = false;
        
        Debug.Log($"AIOpponent: Настройки - Скорость: {moveSpeed}, Слой земли: {groundLayer.value}, Слой препятствий: {obstacleLayer.value}");
    }
    
    private void OnDestroy()
    {
        if (raceTimer != null)
        {
            raceTimer.OnRaceStarted -= OnRaceStart;
        }
    }
    
    public void StartRacing()
    {
        Debug.Log("AIOpponent: Подготовка к гонке");
        isMoving = false;
        if (raceTimer != null && raceTimer.IsRaceStarted())
        {
            StartCoroutine(StartMoving());
        }
    }
    
    private void OnRaceStart()
    {
        Debug.Log("AIOpponent: Получено событие начала гонки");
        StartCoroutine(StartMoving());
    }
    
    private IEnumerator StartMoving()
    {
        yield return new WaitForSeconds(startDelay);
        
        finishLine = FindObjectOfType<FinishLine>()?.transform;
        if (finishLine == null)
        {
            Debug.LogError("AIOpponent: Финишная линия не найдена!");
            yield break;
        }
        
        isMoving = true;
        lastPosition = transform.position;
        Debug.Log($"AIOpponent: Начинаю движение к финишу. Расстояние: {Vector3.Distance(transform.position, finishLine.position)}");
    }
    
    private bool IsStuck()
    {
        // Если это первая проверка
        if (lastStuckPosition == Vector3.zero)
        {
            lastStuckPosition = transform.position;
            return false;
        }

        // Проверяем расстояние от последней позиции
        float distance = Vector3.Distance(transform.position, lastStuckPosition);
        
        // Если AI двигается - обновляем позицию и сбрасываем таймер
        if (distance > minMovementDistance)
        {
            lastStuckPosition = transform.position;
            stuckTime = 0f;
            return false;
        }

        // Если AI не двигается
        stuckTime += Time.deltaTime;
        
        // Если AI не двигается больше 1.5 секунды
        if (stuckTime >= maxStuckTime)
        {
            Debug.Log($"AIOpponent: Застрял! Время: {stuckTime:F2}, Расстояние: {distance:F2}");
            
            // Поднимаем AI вверх
            transform.position += Vector3.up;
            
            // Сбрасываем таймер и позицию
            stuckTime = 0f;
            lastStuckPosition = transform.position;
            
            return true;
        }

        return false;
    }
    
    private (bool hasObstacle, float height) CheckObstacleAhead()
    {
        RaycastHit hit;
        Vector3 rayStart = transform.position + Vector3.up * 0.5f;
        Vector3 rayDirection = transform.forward;
        
        // Проверяем препятствия на разных высотах
        for (float height = 0.5f; height <= maxObstacleHeight; height += 0.5f)
        {
            Vector3 checkStart = rayStart + Vector3.up * height;
            if (Physics.Raycast(checkStart, rayDirection, out hit, obstacleCheckDistance, obstacleLayer))
            {
                float obstacleHeight = hit.point.y - transform.position.y;
                Debug.Log($"AIOpponent: Обнаружено препятствие впереди на высоте {height}: {hit.collider.gameObject.name}, Высота: {obstacleHeight}");
                return (true, obstacleHeight);
            }
        }
        
        return (false, 0f);
    }
    
    private bool CheckGround()
    {
        // Если есть физический контакт с землей через OnCollisionStay
        if (isCollidingWithGround)
        {
            return true;
        }
        
        RaycastHit hit;
        Vector3 rayStart = transform.position + Vector3.up * 0.1f;
        
        // Проверяем землю под AI с помощью нескольких лучей
        // Центральный луч
        if (Physics.Raycast(rayStart, Vector3.down, out hit, groundCheckDistance))
        {
            return true;
        }
        
        // Лучи со смещением вперед, назад, влево и вправо для более надежного определения
        Vector3[] directions = new Vector3[]
        {
            transform.forward * 0.3f,
            -transform.forward * 0.3f,
            transform.right * 0.3f,
            -transform.right * 0.3f
        };
        
        foreach (Vector3 dir in directions)
        {
            if (Physics.Raycast(rayStart + dir, Vector3.down, out hit, groundCheckDistance))
            {
                return true;
            }
        }
        
        // Если AI долго находится "в воздухе" и при этом не перемещается значительно по вертикали,
        // вероятно, он застрял - принудительно считаем его на земле
        if (timeInAir > 0.5f)
        {
            Vector3 currentVerticalVelocity = new Vector3(0, rb.linearVelocity.y, 0);
            
            // Если вертикальная скорость низкая и мы не прыгаем
            if (currentVerticalVelocity.magnitude < 1f && !isJumping)
            {
                stuckInAirTime += Time.deltaTime;
                
                if (stuckInAirTime > forceGroundedTime)
                {
                    Debug.Log("AIOpponent: Принудительно считаем AI на земле после застревания в 'воздухе'");
                    stuckInAirTime = 0f;
                    return true;
                }
            }
            else
            {
                stuckInAirTime = 0f;
            }
        }
        else
        {
            stuckInAirTime = 0f;
        }
        
        return false;
    }
    
    private void FixedUpdate()
    {
        if (!isMoving || finishLine == null || hasWon) return;
        
        isGrounded = CheckGround();
        
        moveDirection = (finishLine.position - transform.position).normalized;
        
        Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        
        if (isGrounded)
        {
            timeInAir = 0f;
            var (hasObstacle, obstacleHeight) = CheckObstacleAhead();
            currentObstacleHeight = obstacleHeight;
            
            // Проверяем наличие препятствий сбоку, чтобы избегать узких мест
            bool hasObstacleLeft = Physics.Raycast(transform.position + Vector3.up * 0.5f, -transform.right, 1f, obstacleLayer);
            bool hasObstacleRight = Physics.Raycast(transform.position + Vector3.up * 0.5f, transform.right, 1f, obstacleLayer);
            
            // Если есть препятствия по обе стороны, возможно, мы в узком месте
            bool isInNarrowSpace = hasObstacleLeft && hasObstacleRight;
            
            // Проверяем застревание и прыгаем если застрял
            if (IsStuck() && Time.time - lastJumpTime >= jumpCooldown)
            {
                Debug.Log("AIOpponent: Застрял, пытаюсь выпрыгнуть");
                Jump(true);
                return;
            }
            
            // Также прыгаем если в узком месте и не можем двигаться нормально
            if (isInNarrowSpace && rb.linearVelocity.magnitude < moveSpeed * 0.5f && Time.time - lastJumpTime >= jumpCooldown)
            {
                Debug.Log("AIOpponent: В узком месте, пробую перепрыгнуть");
                Jump(true);
                return;
            }
            
            Vector3 targetVelocity = moveDirection * moveSpeed;
            targetVelocity.y = rb.linearVelocity.y;
            rb.linearVelocity = targetVelocity;
            
            // Немного снижаем дополнительную силу, чтобы не было рывков
            rb.AddForce(moveDirection * moveSpeed * 1.5f, ForceMode.Force);
            
            // Обновляем анимацию бега
            if (animator != null)
            {
                // Нормализуем скорость для аниматора (0-1)
                float normalizedSpeed = Mathf.Clamp01(rb.linearVelocity.magnitude / moveSpeed);
                animator.SetFloat("Speed", normalizedSpeed, 0.1f, Time.deltaTime);
                Debug.Log($"AIOpponent: Анимация - Normalized Speed: {normalizedSpeed:F2}, Raw Speed: {rb.linearVelocity.magnitude:F2}");
            }
            
            Debug.Log($"AIOpponent: Движение - Скорость: {rb.linearVelocity.magnitude:F2}, На земле: {isGrounded}");
        }
        else
        {
            timeInAir += Time.fixedDeltaTime;
            
            // Обновляем анимацию в воздухе
            if (animator != null)
            {
                animator.SetFloat("Speed", 0f, 0.1f, Time.deltaTime);
                Debug.Log("AIOpponent: Анимация - Speed: 0 (в воздухе)");
            }
            
            // Проверяем, застрял ли AI в "воздухе" (не двигается значительно)
            if (timeInAir > 1.5f && rb.linearVelocity.magnitude < 2f && !isJumping)
            {
                Debug.Log("AIOpponent: Застрял в 'воздухе', применяем силу вниз");
                // Применяем силу вниз, чтобы помочь AI достичь земли
                rb.AddForce(Vector3.down * 10f, ForceMode.Force);
                // Небольшой импульс в направлении цели
                rb.AddForce(moveDirection * moveSpeed * 0.5f, ForceMode.Impulse);
            }
            
            // Небольшая коррекция направления в воздухе
            rb.AddForce(moveDirection * moveSpeed * 0.3f * Time.fixedDeltaTime, ForceMode.Force);
            
            Debug.Log($"AIOpponent: В воздухе {timeInAir:F2} сек. Позиция: {transform.position}");
            
            // Если слишком долго в воздухе, пытаемся приземлиться
            if (timeInAir > maxTimeInAir)
            {
                Debug.Log("AIOpponent: Слишком долго в воздухе, пытаюсь приземлиться");
                rb.linearVelocity = new Vector3(rb.linearVelocity.x, -5f, rb.linearVelocity.z);
                timeInAir = 0f;
            }
        }
    }
    
    private void OnCollisionEnter(Collision collision)
    {
        // Игнорируем столкновения с игроком
        if (collision.gameObject.CompareTag("Player"))
        {
            Physics.IgnoreCollision(GetComponent<Collider>(), collision.collider, true);
            return;
        }
        
        Debug.Log($"AIOpponent: Столкновение с {collision.gameObject.name}, Слой: {collision.gameObject.layer}");
        
        // Проверяем, является ли объект землей или считаем его опорой
        if (((1<<collision.gameObject.layer) & groundLayer) != 0 || collision.contacts[0].normal.y > 0.3f)
        {
            isCollidingWithGround = true;
            Debug.Log("AIOpponent: Контакт с землей установлен");
        }
        
        // Проверяем, является ли объект препятствием
        bool isObstacle = collision.gameObject.CompareTag("Obstacle");
        
        // Теперь проверяем, находится ли точка контакта впереди AI
        bool isInFront = false;
        
        foreach (ContactPoint contact in collision.contacts)
        {
            // Проверяем, находится ли точка контакта в направлении движения AI
            Vector3 contactDirFromAI = contact.point - transform.position;
            float forwardDot = Vector3.Dot(transform.forward, contactDirFromAI.normalized);
            
            // Если точка контакта перед AI (угол < 45 градусов)
            if (forwardDot > 0.7f)
            {
                isInFront = true;
                break;
            }
        }
        
        // Прыгаем только если столкнулись с препятствием спереди
        if (isObstacle && isInFront && isGrounded && Time.time - lastJumpTime >= jumpCooldown)
        {
            Debug.Log("AIOpponent: Столкнулся с препятствием впереди, прыгаю");
            Jump(false); // Обычный прыжок, не с увеличенной силой
        }
    }
    
    private void OnCollisionStay(Collision collision)
    {
        // Игнорируем столкновения с игроком
        if (collision.gameObject.CompareTag("Player"))
        {
            return;
        }
        
        // Обновляем состояние контакта с землей
        if (((1<<collision.gameObject.layer) & groundLayer) != 0 || collision.contacts[0].normal.y > 0.3f)
        {
            isCollidingWithGround = true;
        }
    }
    
    private void OnCollisionExit(Collision collision)
    {
        // Игнорируем столкновения с игроком
        if (collision.gameObject.CompareTag("Player"))
        {
            return;
        }
        
        // Сбрасываем флаг контакта с землей при выходе из коллизии
        if (((1<<collision.gameObject.layer) & groundLayer) != 0 || 
            (collision.contacts.Length > 0 && collision.contacts[0].normal.y > 0.3f))
        {
            isCollidingWithGround = false;
            Debug.Log("AIOpponent: Контакт с землей прерван");
        }
    }
    
    private void Jump(bool isStuckJump = false)
    {
        if (Time.time - lastJumpTime >= jumpCooldown)
        {
            // Уменьшаем множитель прыжка при застревании
            float jumpMultiplier = isStuckJump ? 1.2f : 1f;
            
            // Применяем силу прыжка вверх и почти без движения вперед
            Vector3 jumpDirection = Vector3.up + moveDirection * 0.1f;
            rb.AddForce(jumpDirection * jumpForce * jumpMultiplier, ForceMode.Impulse);
            
            isGrounded = false;
            isJumping = true;
            lastJumpTime = Time.time;
            stuckTime = 0f;
            
            Debug.Log($"AIOpponent: Прыжок! Сила: {jumpForce * jumpMultiplier:F2}, Направление: {jumpDirection}");
            
            StartCoroutine(ResetJumpFlag());
        }
    }
    
    private IEnumerator ResetJumpFlag()
    {
        yield return new WaitForSeconds(jumpCooldown);
        isJumping = false;
    }
    
    private void OnDrawGizmos()
    {
        // Визуализация луча проверки земли в редакторе
        Gizmos.color = Color.red;
        Gizmos.DrawRay(transform.position + Vector3.up * 0.1f, Vector3.down * groundCheckDistance);
        
        // Визуализация лучей проверки препятствий на разных высотах
        Gizmos.color = Color.yellow;
        for (float height = 0.5f; height <= maxObstacleHeight; height += 0.5f)
        {
            Gizmos.DrawRay(transform.position + Vector3.up * (0.5f + height), transform.forward * obstacleCheckDistance);
        }
        
        // Визуализация максимальной высоты препятствия
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position + Vector3.up * maxObstacleHeight, 0.5f);
        
        // Визуализация направления движения
        if (isMoving && finishLine != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(transform.position, (finishLine.position - transform.position).normalized * 2f);
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("FinishLine") && !hasWon)
        {
            hasWon = true;
            isMoving = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            Debug.Log("AIOpponent: AI достиг финиша!");
            
            // Деактивируем AI и финишную линию
            gameObject.SetActive(false);
            other.gameObject.SetActive(false);
            Debug.Log("AIOpponent: AI и финишная линия деактивированы");
            
            // Вызываем событие победы AI
            OnAIVictory?.Invoke();
        }
    }
    
    public void ResetAI()
    {
        hasWon = false;
        isMoving = false;
        currentObstacleHeight = 0f;
        stuckTime = 0f;
        lastPosition = transform.position;
        lastJumpTime = 0f;
        jumpCooldown = 0f;
        
        // Сбрасываем анимацию
        if (animator != null)
        {
            animator.SetFloat("Speed", 0f);
        }
        
        // Сбрасываем физику
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        
        Debug.Log("AIOpponent: AI сброшен");
    }
    
    public void StopAI()
    {
        isMoving = false;
        hasWon = false;
        
        // Останавливаем анимацию
        if (animator != null)
        {
            animator.SetFloat("Speed", 0f);
        }
        
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        Debug.Log("AIOpponent: AI остановлен");
    }
} 