using UnityEngine;
using Invector.vCharacterController;
using Invector.vCamera;
using Invector;
using System.Collections;

public class VehicleController : MonoBehaviour
{
    [Header("Vehicle Physics")]
    [Tooltip("Сила мотора - влияет на ускорение")]
    [Range(1000f, 15000f)] public float motorForce = 8000f;
    
    [Tooltip("МАКСИМАЛЬНАЯ СКОРОСТЬ - уменьши чтобы замедлить машину")]
    [Range(3f, 25f)] public float maxSpeed = 15f;
    
    [Tooltip("Угол поворота руля - больше = круче повороты")]
    [Range(15f, 250f)] public float steerAngle = 45f;
    
    [Tooltip("Скорость поворота на месте - больше = быстрее поворот")]
    [Range(30f, 200f)] public float stationaryTurnSpeed = 100f;
    
    [Tooltip("Множитель скорости заднего хода (1 = такая же как вперед, 0.5 = в 2 раза медленнее)")]
    [Range(0.1f, 1f)] public float reverseSpeedMultiplier = 0.25f;
    
    [Tooltip("Включить ограничение скорости заднего хода")]
    public bool limitReverseSpeed = true;
    
    [Tooltip("Прижимная сила для лучшего сцепления")]
    [Range(100f, 500f)] public float downForce = 300f;
    
    [Tooltip("Боковое трение для заноса")]
    [Range(0.1f, 15f)] public float sideFriction = 0.1f;
    
    [Tooltip("Сила торможения")]
    [Range(2000f, 8000f)] public float brakeForce = 5000f;
    
    [Tooltip("Сила постепенного торможения на Space")]
    [Range(0.1f, 1f)] public float gradualBrakeStrength = 0.3f;
    
    [Tooltip("Сила прыжка при нажатии LeftShift")]
    [Range(500f, 3000f)] public float jumpForce = 1500f;
    
    [Tooltip("Минимальное время между прыжками (сек)")]
    [Range(0.1f, 1f)] public float jumpCooldown = 0.2f;
    
    [Header("Boost System")]
    [Tooltip("Множитель скорости во время буста")]
    [Range(1.5f, 5f)] public float boostMultiplier = 2.5f;
    
    [Tooltip("Длительность буста в секундах")]
    [Range(0.5f, 3f)] public float boostDuration = 1f;
    
    [Header("Ground Detection")]
    public LayerMask groundLayer = -1; // Все слои по умолчанию
    public float groundRayLength = 5f; // Увеличил длину луча
    [Tooltip("Высота парения над землей - больше = выше над поверхностью")]
    [Range(0.5f, 3f)] public float hoverHeight = 1.5f;
    public float hoverForce = 500f;
    public Transform[] groundRayPoints;
    
    [Header("Stability")]
    public float uprightForce = 300f;
    public float uprightTorque = 50f;
    public float maxTiltAngle = 30f;
    
    [Header("Player Settings")]
    public Transform playerSeat;
    
    [Header("Input")]
    public KeyCode actionKey = KeyCode.E;
    
    [Header("Camera Settings")]
    [Tooltip("Настройки камеры для машины (изменяются в реальном времени)")]
    [Range(2f, 8f)] public float cameraDistance = 3.33f;  // Расстояние от машины
    [Range(0.5f, 4f)] public float cameraHeight = 2f;     // Высота камеры
    [Range(0.5f, 5f)] public float cameraSmooth = 1.5f;   // Сглаживание (меньше = плавнее)
    [Range(0.1f, 3f)] public float cameraSmoothDamp = 0.5f; // Демпфирование (меньше = без дерганий)
    [Range(50f, 90f)] public float cameraFOV = 70f;       // Поле зрения
    
    // Components
    private Rigidbody rb;
    private vThirdPersonController playerController;
    private vThirdPersonInput playerInput;
    private Animator playerAnimator;
    [HideInInspector] public bool isPlayerInVehicle = false;
    private Vector3 originalPlayerPosition;
    private Quaternion originalPlayerRotation;
    private Transform originalPlayerParent;
    
    // Camera
    private MonoBehaviour originalCameraController;
    private string originalCameraState;
    
    // Input values
    private float motorInput = 0f;
    private float steerInput = 0f;
    private bool isBraking = false;
    
    // Physics state
    private bool isGrounded = false;
    private Vector3 groundNormal = Vector3.up;
    private float currentSpeed = 0f;
    
    // Boost system
    private bool isBoosted = false;
    private float boostEndTime = 0f;
    
    // Jump system
    private bool canJump = true; // Может ли машина прыгать
    private bool wasGroundedLastFrame = true; // Была ли на земле в прошлом кадре
    private float lastJumpTime = 0f; // Время последнего прыжка
    
    // Anti-spam protection for vehicle entry/exit
    private float lastVehicleActionTime = 0f;
    private float vehicleActionCooldown = 1f;
    
    // Сохранение оригинальных настроек камеры
    private float originalSmoothBetweenState;
    private float originalSmoothCameraRotation;
    
    void Start()
    {
        Debug.Log("[VehicleController] ===== START ВЫЗВАН! =====");
        
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }
        
        // НАСТРОЙКА ФИЗИКИ С ВКЛЮЧЕННОЙ ГРАВИТАЦИЕЙ
        rb.useGravity = true; // ВКЛЮЧАЕМ гравитацию!
        rb.isKinematic = false; // НЕ кинематический!
        rb.mass = 400f; // Уменьшили массу с 800 до 400 для лучшего отклика
        rb.linearDamping = 0.1f; // Уменьшили сопротивление воздуха с 0.3 до 0.1
        rb.angularDamping = 2f; // Уменьшили с 3 до 2
        rb.centerOfMass = new Vector3(0, -0.5f, 0); // Низкий центр масс
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.constraints = RigidbodyConstraints.None; // УБИРАЕМ ВСЕ ОГРАНИЧЕНИЯ!
        
        // Создаем точки для ground detection если их нет
        if (groundRayPoints == null || groundRayPoints.Length == 0)
        {
            CreateGroundRayPoints();
        }
        
        // Инициализация системы прыжков
        canJump = true;
        wasGroundedLastFrame = true;
        lastJumpTime = 0f;
        
        Debug.Log($"[VehicleController] Инициализация завершена: useGravity={rb.useGravity}, mass={rb.mass}, isKinematic={rb.isKinematic}, constraints={rb.constraints}");
        Debug.Log($"[VehicleController] Time settings: timeScale={Time.timeScale}, fixedDeltaTime={Time.fixedDeltaTime}");
        Debug.Log($"[VehicleController] Система прыжков инициализирована - доступен 1 прыжок с земли, cooldown: {jumpCooldown} сек");

    }
    
    void CreateGroundRayPoints()
    {
        // Создаем 4 точки под углами vehicle для ground detection
        GameObject rayPointsParent = new GameObject("GroundRayPoints");
        rayPointsParent.transform.SetParent(transform);
        rayPointsParent.transform.localPosition = Vector3.zero;
        
        groundRayPoints = new Transform[4];
        Vector3[] positions = new Vector3[]
        {
            new Vector3(-1f, 0.5f, 1.5f),   // передний левый
            new Vector3(1f, 0.5f, 1.5f),    // передний правый
            new Vector3(-1f, 0.5f, -1.5f),  // задний левый
            new Vector3(1f, 0.5f, -1.5f)    // задний правый
        };
        
        for (int i = 0; i < positions.Length; i++)
        {
            GameObject rayPoint = new GameObject($"RayPoint_{i}");
            rayPoint.transform.SetParent(rayPointsParent.transform);
            rayPoint.transform.localPosition = positions[i];
            groundRayPoints[i] = rayPoint.transform;
        }
        
        Debug.Log($"[VehicleController] Создано {groundRayPoints.Length} точек ground detection");
    }
    
    void Update()
    {
        if (isPlayerInVehicle)
        {
            GetInput();
            currentSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);
            
            // Прыжок на LeftShift - только один раз до приземления
            if (Input.GetKeyDown(KeyCode.LeftShift) && isGrounded && canJump)
            {
                rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
                canJump = false; // Блокируем прыжки до приземления
                lastJumpTime = Time.time; // Запоминаем время прыжка
                Debug.Log($"[VehicleController] 🚀 ПРЫЖОК! Сила: {jumpForce}, время: {lastJumpTime:F2}");
            }
            else if (Input.GetKeyDown(KeyCode.LeftShift) && !canJump)
            {
                Debug.Log("[VehicleController] ❌ Прыжок недоступен - приземлись сначала!");
            }
            else if (Input.GetKeyDown(KeyCode.LeftShift) && !isGrounded)
            {
                Debug.Log("[VehicleController] ❌ Прыжок недоступен - не на земле!");
            }
            
            // Диагностика состояния прыжка при нажатии Shift (но не прыжке)
            if (Input.GetKeyDown(KeyCode.LeftShift) && !isGrounded && canJump)
            {
                Debug.Log($"[VehicleController] 📊 Диагностика: canJump={canJump}, isGrounded={isGrounded}, timeSinceJump={(Time.time - lastJumpTime):F2}");
            }
            
            // УЛУЧШЕННАЯ ЛОГИКА восстановления прыжка
            if (isGrounded && !canJump)
            {
                // Разрешаем прыгать если:
                // 1. Сейчас на земле
                // 2. Прошло минимальное время с последнего прыжка
                float timeSinceJump = Time.time - lastJumpTime;
                if (timeSinceJump >= jumpCooldown)
                {
                    canJump = true;
                    Debug.Log($"[VehicleController] ✅ Прыжок восстановлен! Прошло времени: {timeSinceJump:F2} сек");
                }
            }
            
            // Запоминаем состояние земли для следующего кадра
            wasGroundedLastFrame = isGrounded;
            
            // Проверка окончания буста
            if (isBoosted && Time.time >= boostEndTime)
            {
                isBoosted = false;
                Debug.Log("[VehicleController] ⏰ БУСТ ЗАКОНЧИЛСЯ!");
            }
            
            // Показываем статус буста каждые 0.5 секунды при активном бусте
            if (isBoosted && Time.time % 0.5f < Time.deltaTime)
            {
                float remainingTime = boostEndTime - Time.time;
                Debug.Log($"[VehicleController] 🚀 БУСТ АКТИВЕН! Осталось: {remainingTime:F1} сек");
            }
        }
    }
    
    void FixedUpdate()
    {
        if (isPlayerInVehicle)
        {
            Vector3 velocityBefore = rb.linearVelocity;
            
            GroundCheck();
            Motor();
            Steering(); // ✅ ВКЛЮЧИЛИ ПОВОРОТ!
            
            if (isGrounded)
            {
                SideFriction(); // ✅ ВКЛЮЧИЛИ ЗАНОС!
                DownForce();
            }
            
                 
             HandlePlayerAnimations();
             
             // Диагностика изменения скорости - только изредка
            if (Time.fixedTime < 3f && Time.fixedTime % 2f < Time.fixedDeltaTime && Mathf.Abs(motorInput) > 0.1f)
            {
                Vector3 velocityAfter = rb.linearVelocity;
                Debug.Log($"[VehicleController] FixedUpdate: velocity = {velocityAfter}");
            }
        }
    }
    
    void LateUpdate()
    {
        // Синхронизация позиции игрока с сиденьем
        if (isPlayerInVehicle && playerController != null && playerSeat != null)
        {
            playerController.transform.position = playerSeat.position;
            playerController.transform.rotation = playerSeat.rotation;
        }
        
        // ДОПОЛНИТЕЛЬНАЯ СИНХРОНИЗАЦИЯ КАМЕРЫ С МАШИНОЙ
        if (isPlayerInVehicle)
        {
            var tpCamera = vThirdPersonCamera.instance;
            if (tpCamera != null)
            {
                // Принудительно привязываем камеру к машине если она сбилась
                if (tpCamera.currentTarget != this.transform)
                {
                    tpCamera.SetMainTarget(this.transform);
                }
                
                // ОБНОВЛЯЕМ НАСТРОЙКИ КАМЕРЫ В РЕАЛЬНОМ ВРЕМЕНИ
                UpdateCameraSettings(tpCamera);
            }
        }
        
        // Проверка на выход из машины
        if (isPlayerInVehicle && Input.GetKeyDown(actionKey) && Time.time > lastVehicleActionTime + vehicleActionCooldown)
        {
            ExitVehicle();
        }
    }
    
    void GetInput()
    {
        motorInput = 0f;
        if (Input.GetKey(KeyCode.W)) motorInput = 1f;
        else if (Input.GetKey(KeyCode.S)) 
        {
            // Запрещаем движение назад в воздухе
            if (isGrounded)
            {
                motorInput = -1f;
            }
            else
            {
                Debug.Log("[VehicleController] ❌ Движение назад заблокировано - машина в воздухе!");
            }
        }
        
        steerInput = Input.GetAxis("Horizontal");
        isBraking = Input.GetKey(KeyCode.Space);
        
        // ТЕСТ: принудительный прыжок на клавишу T
        if (Input.GetKeyDown(KeyCode.T))
        {
            Debug.Log("[VehicleController] Test jump: applying upward force!");
            rb.AddForce(Vector3.up * 10000f, ForceMode.Impulse);
        }
        
        // ОТЛАДКА ВВОДА
        if (Mathf.Abs(motorInput) > 0f || Mathf.Abs(steerInput) > 0f || isBraking)
        {
            // ДИАГНОСТИКА INPUT - только раз в секунду
        if (Time.time % 1f < Time.deltaTime)
        {
            Debug.Log($"[VehicleController] Input: motor={motorInput}, steer={steerInput}, brake={isBraking}, grounded={isGrounded}");
        }
        }
        
        // Выход из машины - с защитой от спама
        if (Input.GetKeyDown(actionKey) && Time.time - lastVehicleActionTime > vehicleActionCooldown)
        {
            Debug.Log("[VehicleController] Клавиша E нажата для выхода из машины");
            lastVehicleActionTime = Time.time;
            ExitVehicle();
        }
    }
    
    void GroundCheck()
    {
        isGrounded = false;
        groundNormal = Vector3.up;
        
        int groundContacts = 0;
        Vector3 averageNormal = Vector3.zero;
        
        // ОТЛАДКА: проверяем, есть ли точки
        if (groundRayPoints == null || groundRayPoints.Length == 0)
        {
            Debug.LogError("[VehicleController] groundRayPoints не созданы!");
            return;
        }
        
        // Проверяем контакт с землей в нескольких точках
        foreach (Transform rayPoint in groundRayPoints)
        {
            if (rayPoint == null) continue;
            
            RaycastHit hit;
            Vector3 rayOrigin = rayPoint.position;
            
            // ОТЛАДКА: показываем лучи в Scene view
            Debug.DrawRay(rayOrigin, Vector3.down * groundRayLength, Color.yellow);
            
            if (Physics.Raycast(rayOrigin, Vector3.down, out hit, groundRayLength, groundLayer))
            {
                groundContacts++;
                averageNormal += hit.normal;
                
                // ОТЛАДКА: показываем попадания
                Debug.DrawRay(rayOrigin, Vector3.down * hit.distance, Color.green);
                
                // Применяем hover силу для поддержания высоты
                float distanceFromGround = hit.distance;
                if (distanceFromGround < hoverHeight)
                {
                    float hoverRatio = 1f - (distanceFromGround / hoverHeight);
                    Vector3 hoverForceVector = hit.normal * hoverForce * hoverRatio;
                    rb.AddForceAtPosition(hoverForceVector, rayPoint.position);
                }
            }
            else
            {
                // ОТЛАДКА: показываем пропуски
                Debug.DrawRay(rayOrigin, Vector3.down * groundRayLength, Color.red);
            }
        }
        
        if (groundContacts > 0)
        {
            isGrounded = true;
            groundNormal = (averageNormal / groundContacts).normalized;
        }
        
        // ДИАГНОСТИКА GROUND CHECK - только раз в секунду
        if (Time.fixedTime < 5f && Time.fixedTime % 2f < Time.fixedDeltaTime)
        {
            Debug.Log($"[VehicleController] GroundCheck: isGrounded={isGrounded}, contacts={groundContacts}");
        }
        
        Debug.DrawRay(transform.position, groundNormal * 2f, isGrounded ? Color.green : Color.red);
    }
    
    void Motor()
    {
        if (Mathf.Abs(motorInput) > 0.1f)
        {
            // Дополнительная проверка: запрещаем движение назад в воздухе
            if (motorInput < 0 && !isGrounded)
            {
                Debug.Log("[VehicleController] ❌ Движение назад заблокировано в Motor - машина в воздухе!");
                return;
            }
            
            // ЭКСТРЕННОЕ РЕШЕНИЕ - ПРЯМАЯ УСТАНОВКА VELOCITY
            Vector3 forceDirection = transform.forward;
            
            // СГЛАЖЕННАЯ УСТАНОВКА СКОРОСТИ против дерганий  
            // Настраиваемая скорость заднего хода
            float baseSpeed = maxSpeed;
            float targetSpeed;
            if (motorInput > 0) // Вперед
            {
                targetSpeed = motorInput * baseSpeed;
            }
            else // Назад
            {
                if (limitReverseSpeed)
                {
                    // С настраиваемым множителем
                    targetSpeed = motorInput * (baseSpeed * reverseSpeedMultiplier);
                }
                else
                {
                    // Без ограничений - такая же скорость как вперед
                    targetSpeed = motorInput * baseSpeed;
                }
            }
            
            // Применяем буст-множитель
            if (isBoosted)
            {
                float originalSpeed = targetSpeed;
                targetSpeed *= boostMultiplier;
                Debug.Log($"[VehicleController] БУСТ ПРИМЕНЁН! Скорость: {originalSpeed:F1} → {targetSpeed:F1} (x{boostMultiplier})");
            }
            Vector3 targetVelocity = forceDirection * targetSpeed;
            targetVelocity.y = rb.linearVelocity.y; // Сохраняем Y для гравитации
            
            // Сглаживание против дерганий
            Vector3 smoothedVelocity = Vector3.Lerp(rb.linearVelocity, targetVelocity, Time.fixedDeltaTime * 8f);
            smoothedVelocity.y = rb.linearVelocity.y; // Оставляем гравитацию нетронутой
            rb.linearVelocity = smoothedVelocity;
            
            // ДИАГНОСТИКА - только изредка
            if (Time.fixedTime < 5f && Time.fixedTime % 1f < Time.fixedDeltaTime)
            {
                Debug.Log($"[VehicleController] Motor: motorInput={motorInput}, targetSpeed={targetSpeed}, finalSpeed={rb.linearVelocity.magnitude}");
            }
        }
        
        // Постепенное торможение на Space
        if (isBraking && Mathf.Abs(currentSpeed) > 0.1f)
        {
            // Мягкое торможение с настраиваемой силой
            Vector3 brakeForceVector = -rb.linearVelocity.normalized * brakeForce * gradualBrakeStrength;
            brakeForceVector.y = 0; // Не тормозим вертикальную скорость
            rb.AddForce(brakeForceVector);
        }
    }
    
    void Steering()
    {
        if (Mathf.Abs(steerInput) > 0.1f)
        {
            float steer;
            
            // Если машина движется (скорость > 0.5), используем обычный поворот
            if (Mathf.Abs(currentSpeed) > 0.5f)
            {
                // Поворот зависит от скорости - чем быстрее, тем медленнее поворот
                float speedFactor = Mathf.Abs(currentSpeed) / maxSpeed;
                float adjustedSteerAngle = steerAngle * (1f - speedFactor * 0.5f);
                
                // Поворачиваем машину с учетом направления движения
                steer = steerInput * adjustedSteerAngle * Mathf.Sign(currentSpeed);
            }
            else
            {
                // ПОВОРОТ НА МЕСТЕ - используем специальную скорость поворота
                steer = steerInput * stationaryTurnSpeed;
            }
            
            // Применяем поворот
            Quaternion deltaRotation = Quaternion.Euler(0, steer * Time.fixedDeltaTime, 0);
            rb.MoveRotation(rb.rotation * deltaRotation);
            
            // ДИАГНОСТИКА поворота
            if (Time.fixedTime < 15f)
            {
                string turnType = Mathf.Abs(currentSpeed) > 0.5f ? "ДВИЖЕНИЕ" : "НА МЕСТЕ";
                Debug.Log($"[VehicleController] Steering ({turnType}): steerInput={steerInput}, currentSpeed={currentSpeed}, steer={steer}");
            }
        }
    }
    
    void SideFriction()
    {
        // УЛУЧШЕННАЯ СИСТЕМА ЗАНОСА ДЛЯ ДРИФТА
        Vector3 rightVelocity = Vector3.Project(rb.linearVelocity, transform.right);
        
        // Только если есть боковая скорость
        if (rightVelocity.magnitude > 0.1f)
        {
            // Уменьшаем боковое трение для заноса - чем больше скорость, тем меньше трение
            float speedFactor = Mathf.Clamp01(rb.linearVelocity.magnitude / maxSpeed);
            float adjustedFriction = sideFriction * (1f - speedFactor * 0.7f); // На большой скорости трение падает до 30%
            
            Vector3 sideFrictionForce = -rightVelocity * adjustedFriction;
            rb.AddForce(sideFrictionForce);
            
            // ДИАГНОСТИКА заноса
            if (Time.fixedTime < 5f && rightVelocity.magnitude > 2f)
            {
                Debug.Log($"[VehicleController] DRIFT! rightVel={rightVelocity.magnitude:F1}, friction={adjustedFriction:F1}");
            }
        }
    }
    
    void DownForce()
    {
        // Прижимная сила для лучшего сцепления с дорогой
        rb.AddForce(-groundNormal * downForce * Mathf.Abs(currentSpeed) / maxSpeed);
    }
    
    void Stabilization()
    {
        // Стабилизация ориентации по нормали поверхности
        if (isGrounded)
        {
            Vector3 targetUp = Vector3.Slerp(transform.up, groundNormal, uprightForce * Time.fixedDeltaTime);
            Vector3 targetForward = Vector3.ProjectOnPlane(transform.forward, targetUp).normalized;
            
            Quaternion targetRotation = Quaternion.LookRotation(targetForward, targetUp);
            Quaternion deltaRotation = Quaternion.RotateTowards(transform.rotation, targetRotation, uprightTorque * Time.fixedDeltaTime);
            
            rb.MoveRotation(deltaRotation);
        }
        else
        {
            // В воздухе - медленно выравниваем горизонтально
            Vector3 targetUp = Vector3.up;
            Vector3 targetForward = Vector3.ProjectOnPlane(transform.forward, targetUp).normalized;
            
            Quaternion targetRotation = Quaternion.LookRotation(targetForward, targetUp);
            Quaternion deltaRotation = Quaternion.RotateTowards(transform.rotation, targetRotation, uprightTorque * 0.5f * Time.fixedDeltaTime);
            
            rb.MoveRotation(deltaRotation);
        }
    }
    
    void HandlePlayerAnimations()
    {
        if (playerAnimator == null) return;
        
        // Проверяем движется ли машина
        bool isMoving = Mathf.Abs(currentSpeed) > 1f; // Порог скорости для анимации движения
        
        if (isPlayerInVehicle)
        {
            // Игрок в машине - проигрываем анимации снегохода
            if (isMoving)
            {
                // Восстанавливаем нормальную скорость для анимации движения
                playerAnimator.speed = 1f;
                playerAnimator.Play("Snegokat_move");
            }
            else
            {
                // Статичный кадр - останавливаем анимацию на первом кадре
                playerAnimator.Play("Snegokat_idle", 0, 0f);
                playerAnimator.speed = 0f; // Останавливаем воспроизведение
            }
        }
    }
    

    
    public void EnterVehicle(vThirdPersonController player, vThirdPersonInput input, Animator animator)
    {
        if (isPlayerInVehicle) return;
        
        Debug.Log("[VehicleController] ===== ENTER VEHICLE ВЫЗВАН! =====");
        Debug.Log("[VehicleController] Игрок садится в машину");
        
        playerController = player;
        playerInput = input;
        playerAnimator = animator;
        
        // Сбрасываем состояние прыжка при входе в машину
        canJump = true;
        wasGroundedLastFrame = isGrounded;
        lastJumpTime = 0f;
        
        // Сохраняем состояние игрока
        originalPlayerPosition = player.transform.position;
        originalPlayerRotation = player.transform.rotation;
        originalPlayerParent = player.transform.parent;
        
        // Переключаем камеру ПЕРЕД отключением управления
        SwitchCameraToVehicle();
        
        // Отключаем управление игроком
        playerController.enabled = false;
        
        // Перемещаем игрока в сиденье
        player.transform.position = playerSeat.position;
        player.transform.rotation = playerSeat.rotation;
        
        // Отключаем физику игрока
        var playerRigidbody = player.GetComponent<Rigidbody>();
        if (playerRigidbody != null)
        {
            playerRigidbody.isKinematic = true;
        }
        
        isPlayerInVehicle = true;

        

        
        // Устанавливаем время входа для защиты от спама
        lastVehicleActionTime = Time.time;
        
        Debug.Log("[VehicleController] Игрок успешно сел в машину");
    }
    
    void UpdateCameraSettings(vThirdPersonCamera tpCamera)
    {
        // Обновляем настройки состояния Vehicle если оно существует
        if (tpCamera.CameraStateList != null)
        {
            var vehicleState = tpCamera.CameraStateList.tpCameraStates.Find(state => state.Name == "Vehicle");
            if (vehicleState != null && tpCamera.currentStateName == "Vehicle")
            {
                // Применяем новые настройки из инспектора
                vehicleState.defaultDistance = cameraDistance;
                vehicleState.maxDistance = cameraDistance * 2.4f;
                vehicleState.minDistance = cameraDistance * 0.4f;
                vehicleState.height = cameraHeight;
                vehicleState.smooth = cameraSmooth;
                vehicleState.smoothDamp = cameraSmoothDamp;
                vehicleState.fov = cameraFOV;
                vehicleState.cullingHeight = cameraHeight;
                
                // Принудительно обновляем текущее состояние
                tpCamera.currentState.defaultDistance = cameraDistance;
                tpCamera.currentState.height = cameraHeight;
                tpCamera.currentState.smooth = cameraSmooth;
                tpCamera.currentState.smoothDamp = cameraSmoothDamp;
                tpCamera.currentState.fov = cameraFOV;
            }
        }
    }
    
    void SwitchCameraToVehicle()
    {
        // Переключаем камеру через систему Invector
        if (playerInput != null)
        {
            // Получаем камеру Invector
            var tpCamera = vThirdPersonCamera.instance;
            if (tpCamera != null && tpCamera.CameraStateList != null)
            {
                // Проверяем есть ли состояние "Vehicle"
                var vehicleState = tpCamera.CameraStateList.tpCameraStates.Find(state => state.Name == "Vehicle");
                if (vehicleState == null)
                {
                    // Создаем состояние Vehicle на лету
                    vehicleState = new vThirdPersonCameraState("Vehicle");
                    // НАСТРОЙКИ ДЛЯ МАШИНЫ - используем настройки из инспектора!
                    vehicleState.defaultDistance = cameraDistance;      // Настраиваемое расстояние
                    vehicleState.maxDistance = cameraDistance * 2.4f;   // Максимальное расстояние
                    vehicleState.minDistance = cameraDistance * 0.4f;   // Минимальное расстояние
                    vehicleState.height = cameraHeight;                  // Настраиваемая высота
                    vehicleState.smooth = cameraSmooth;                  // Настраиваемое сглаживание
                    vehicleState.smoothDamp = cameraSmoothDamp;          // Настраиваемое демпфирование
                    vehicleState.xMouseSensitivity = 2f;                 // Чувствительность мыши
                    vehicleState.yMouseSensitivity = 2f;
                    vehicleState.yMinLimit = -30f;
                    vehicleState.yMaxLimit = 60f;
                    vehicleState.xMinLimit = -360f;
                    vehicleState.xMaxLimit = 360f;
                    vehicleState.fov = cameraFOV;                        // Настраиваемое поле зрения
                    vehicleState.cullingHeight = cameraHeight;
                    vehicleState.cullingMinDist = 0.1f;
                    vehicleState.forward = -1f;
                    vehicleState.right = 0f;
                    
                    // Добавляем в список состояний
                    tpCamera.CameraStateList.tpCameraStates.Add(vehicleState);
                    Debug.Log("[VehicleController] Создал новое состояние камеры Vehicle");
                }
                
                // Переключаемся на состояние Vehicle
                playerInput.ChangeCameraState("Vehicle");
                
                // ВАЖНО: Устанавливаем МАШИНУ как цель для камеры!
                tpCamera.SetMainTarget(this.transform);
                
                // СОХРАНЯЕМ ОРИГИНАЛЬНЫЕ НАСТРОЙКИ
                originalSmoothBetweenState = tpCamera.smoothBetweenState;
                originalSmoothCameraRotation = tpCamera.smoothCameraRotation;
                
                // ДОПОЛНИТЕЛЬНОЕ СГЛАЖИВАНИЕ НА УРОВНЕ КАМЕРЫ
                tpCamera.smoothBetweenState = 2f;      // Плавный переход между состояниями
                tpCamera.smoothCameraRotation = 8f;    // Плавное вращение камеры
                
                Debug.Log("[VehicleController] Переключил камеру на состояние Vehicle и установил машину как цель");
            }
            else
            {
                // Fallback на Default если что-то не так
                playerInput.ChangeCameraState("Default");
                Debug.LogWarning("[VehicleController] Камера Invector не найдена, использую Default");
            }
        }
    }
    
    public void ExitVehicle()
    {
        if (!isPlayerInVehicle || playerController == null) return;
        
        Debug.Log("[VehicleController] Игрок выходит из машины");
        
        // Сбрасываем состояние прыжка при выходе из машины
        canJump = true;
        wasGroundedLastFrame = true;
        lastJumpTime = 0f;
        
        // Переключаем камеру обратно через систему Invector
        if (playerInput != null)
        {
            // Возвращаем камеру обратно на игрока
            var tpCamera = vThirdPersonCamera.instance;
            if (tpCamera != null)
            {
                tpCamera.SetMainTarget(playerController.transform);
                
                // ВОССТАНАВЛИВАЕМ ОРИГИНАЛЬНЫЕ НАСТРОЙКИ КАМЕРЫ
                tpCamera.smoothBetweenState = originalSmoothBetweenState;
                tpCamera.smoothCameraRotation = originalSmoothCameraRotation;
            }
            
            playerInput.ResetCameraState(); // Возвращаем к стандартному состоянию
            Debug.Log("[VehicleController] Переключил камеру обратно на игрока и состояние Default");
        }
        
        // Восстанавливаем физику игрока
        var playerRigidbody = playerController.GetComponent<Rigidbody>();
        if (playerRigidbody != null)
        {
            playerRigidbody.isKinematic = false;
        }
        
        // Позиционируем игрока рядом с машиной
        Vector3 exitPosition = transform.position + transform.right * 2f;
        playerController.transform.position = exitPosition;
        playerController.transform.rotation = transform.rotation;
        
        // Включаем управление игроком
        playerController.enabled = true;
        
        // 🎬 ВОЗВРАЩАЕМ СТАНДАРТНЫЕ АНИМАЦИИ ПРИ ВЫХОДЕ
        if (playerAnimator != null)
        {
            // Восстанавливаем нормальную скорость аниматора
            playerAnimator.speed = 1f;
            // Проигрываем стандартную анимацию idle для возврата к нормальному состоянию
            playerAnimator.Play("Idle");
            Debug.Log("[VehicleController] 🎬 Возврат к стандартным анимациям через Idle");
        }
        
        isPlayerInVehicle = false;
        
        Debug.Log("[VehicleController] Игрок успешно вышел из машины");
    }
    
    void OnDrawGizmos()
    {
        // Показываем сиденье игрока
        if (playerSeat != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(playerSeat.position, 0.3f);
        }
        
        // Показываем центр масс
        if (rb != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position + transform.TransformPoint(rb.centerOfMass), 0.2f);
        }
        
        // Показываем точки ground detection
        if (groundRayPoints != null)
        {
            Gizmos.color = Color.yellow;
            foreach (Transform rayPoint in groundRayPoints)
            {
                if (rayPoint != null)
                {
                    Gizmos.DrawWireSphere(rayPoint.position, 0.1f);
                    Gizmos.DrawRay(rayPoint.position, Vector3.down * groundRayLength);
                }
            }
        }
        
        // Показываем нормаль поверхности
        Gizmos.color = isGrounded ? Color.green : Color.red;
        Gizmos.DrawRay(transform.position, groundNormal * 2f);
    }
    
    // ПУБЛИЧНЫЙ МЕТОД ДЛЯ АКТИВАЦИИ БУСТА (вызывается зонами ускорения)
    public void ActivateBoost()
    {
        Debug.Log($"[VehicleController] ActivateBoost вызван! isPlayerInVehicle={isPlayerInVehicle}");
        
        if (isPlayerInVehicle)
        {
            isBoosted = true;
            boostEndTime = Time.time + boostDuration;
            Debug.Log($"[VehicleController] ✅ БУСТ АКТИВИРОВАН! Множитель: {boostMultiplier}x на {boostDuration} сек, закончится в {boostEndTime:F2}");
        }
        else
        {
            Debug.LogWarning("[VehicleController] ❌ БУСТ НЕ АКТИВИРОВАН - игрок не в машине!");
        }
    }
    
    // ПУБЛИЧНЫЙ МЕТОД ДЛЯ ПОДБРАСЫВАНИЯ МАШИНЫ (вызывается зонами подбрасывания)
    public void ActivateJumpBoost(float jumpForce, Vector3 jumpDirection = default)
    {
        Debug.Log($"[VehicleController] ActivateJumpBoost вызван! isPlayerInVehicle={isPlayerInVehicle}, сила={jumpForce}");
        
        if (isPlayerInVehicle)
        {
            // Если направление не указано, используем вверх
            if (jumpDirection == default)
            {
                jumpDirection = Vector3.up;
            }
            
            // Применяем силу подбрасывания
            rb.AddForce(jumpDirection * jumpForce, ForceMode.Impulse);
            Debug.Log($"[VehicleController] 🚀 ПОДБРАСЫВАНИЕ АКТИВИРОВАНО! Сила: {jumpForce}, направление: {jumpDirection}");
        }
        else
        {
            Debug.LogWarning("[VehicleController] ❌ ПОДБРАСЫВАНИЕ НЕ АКТИВИРОВАНО - игрок не в машине!");
        }
    }
} 