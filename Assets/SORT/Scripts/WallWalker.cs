using UnityEngine;
using Invector.vCharacterController;
using System.Collections;

public class WallWalker : MonoBehaviour
{
    [Header("Wall Walk Settings")]
    [Tooltip("Слой, который содержит стены, по которым можно ходить")]
    public LayerMask walkableWallLayer;
    
    [Tooltip("Минимальный угол стены для хождения (90 = вертикальная стена)")]
    [Range(45f, 90f)]
    public float minWallAngle = 60f;
    
    [Tooltip("Максимальный угол стены для хождения")]
    [Range(90f, 135f)]
    public float maxWallAngle = 120f;
    
    [Tooltip("Расстояние проверки для обнаружения стены")]
    public float wallCheckDistance = 1.2f;
    
    [Tooltip("Гравитация при хождении по стене")]
    public float wallGravity = -0.5f;
    
    [Tooltip("Плавность поворота к стене")]
    public float rotationSpeed = 15f;
    
    [Tooltip("Смещение от стены")]
    public float wallOffset = 0.2f;
    
    [Tooltip("Фиксированная скорость движения по стене")]
    public float wallMoveSpeed = 5f;
    
    [Tooltip("Задержка перед повторной проверкой стены (секунды)")]
    public float wallCheckCooldown = 0.1f;
    
    [Header("Animation Settings")]
    [Tooltip("Имя анимации для бега по стене")]
    public string wallRunAnimationName = "NinjaRun";
    
    [Tooltip("Имя анимации для возврата после хождения по стене")]
    public string returnAnimationName = "FreeLocomotion";
    
    [Header("Input Settings")]
    [Tooltip("Кнопка для активации ходьбы по стене")]
    public KeyCode activateWallWalkKey = KeyCode.E;
    
    [Tooltip("Подсвечивать доступные для хождения стены")]
    public bool highlightAvailableWalls = true;
    
    [Tooltip("Цвет подсветки доступных стен")]
    public Color wallHighlightColor = new Color(0, 1, 0, 0.3f);
    
    [Header("Debug")]
    public bool showDebugRays = true;
    public Color normalDebugColor = Color.green;
    public Color wallCheckDebugColor = Color.red;
    
    // Ссылки на компоненты
    private vThirdPersonController tpController;
    private vThirdPersonInput tpInput;
    private Rigidbody rb;
    private CapsuleCollider capsule;
    private Animator animator;
    
    // Переменные состояния
    private bool isOnWallWalk = false;
    private Vector3 wallNormal;
    private Transform currentWall;
    private Quaternion targetRotation;
    private float originalStepOffset;
    private float wallCheckTimer = 0f;
    private Vector3 originalVelocity;
    private bool wallAvailable = false; // Флаг для отслеживания доступной стены
    private RaycastHit availableWallHit; // Информация о доступной стене
    private GameObject wallHighlightObject; // Объект для подсветки доступной стены
    
    // Кэширование переменных для оптимизации
    private RaycastHit wallHit;
    private Vector3 gravityVector;
    private Vector3[] checkDirections;
    
    // ID и хеши анимаций
    private int wallRunAnimHash;
    private int returnAnimHash;
    private bool wasWallRunningLastFrame = false;
    
    void Start()
    {
        // Получаем ссылки на компоненты
        tpController = GetComponent<vThirdPersonController>();
        tpInput = GetComponent<vThirdPersonInput>();
        rb = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();
        animator = GetComponent<Animator>();
        
        if (tpController == null || rb == null || capsule == null)
        {
            Debug.LogError("WallWalker: Не найдены необходимые компоненты! Убедитесь что на объекте есть vThirdPersonController, Rigidbody и CapsuleCollider");
            enabled = false;
            return;
        }
        
        if (animator == null)
        {
            Debug.LogWarning("WallWalker: Не найден компонент Animator! Анимация бега по стене не будет работать.");
        }
        else
        {
            // Кэшируем хеши анимаций для оптимизации
            wallRunAnimHash = Animator.StringToHash(wallRunAnimationName);
            returnAnimHash = Animator.StringToHash(returnAnimationName);
        }
        
        // Сохраняем оригинальное значение stepOffset
        originalStepOffset = tpController.stepOffsetEnd;
        
        // Добавляем слой walkableWall в GroundLayer контроллера Invector
        tpController.groundLayer.value |= walkableWallLayer.value;
        
        // Инициализируем направления проверки
        checkDirections = new Vector3[5];
        
        // Создаем объект для подсветки стены, если включена опция
        if (highlightAvailableWalls)
        {
            CreateWallHighlight();
        }
    }
    
    // Создаем объект подсветки доступной стены
    private void CreateWallHighlight()
    {
        wallHighlightObject = new GameObject("WallHighlight");
        wallHighlightObject.transform.SetParent(transform);
        
        MeshFilter meshFilter = wallHighlightObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = wallHighlightObject.AddComponent<MeshRenderer>();
        
        // Создаем простую квадратную меш
        Mesh mesh = new Mesh();
        float size = 1.0f;
        
        // Вершины
        Vector3[] vertices = new Vector3[4]
        {
            new Vector3(-size, -size, 0),
            new Vector3(size, -size, 0),
            new Vector3(size, size, 0),
            new Vector3(-size, size, 0)
        };
        
        // Треугольники
        int[] triangles = new int[6]
        {
            0, 2, 1,
            0, 3, 2
        };
        
        // UV-координаты
        Vector2[] uv = new Vector2[4]
        {
            new Vector2(0, 0),
            new Vector2(1, 0),
            new Vector2(1, 1),
            new Vector2(0, 1)
        };
        
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uv;
        mesh.RecalculateNormals();
        
        meshFilter.mesh = mesh;
        
        // Создаем полупрозрачный материал
        Material mat = new Material(Shader.Find("Transparent/Diffuse"));
        mat.color = wallHighlightColor;
        meshRenderer.material = mat;
        
        // Скрываем объект вначале
        wallHighlightObject.SetActive(false);
    }
    
    void Update()
    {
        // Проверяем текущее состояние аниматора, чтобы сбросить таймер при падении
        if (animator != null && animator.GetBool("IsFalling") && wallCheckTimer > 0)
        {
            wallCheckTimer = 0f;
        }
        
        // Проверяем, нажал ли игрок кнопку активации ходьбы по стене
        if (Input.GetKeyDown(activateWallWalkKey))
        {
            if (wallAvailable && !isOnWallWalk)
            {
                // Если стена доступна и мы еще не на стене, активируем режим хождения по стене
                EnterWallWalk(availableWallHit);
            }
            else if (isOnWallWalk)
            {
                // Если уже на стене, выходим из режима
                ExitWallWalk();
            }
        }
    }
    
    void FixedUpdate()
    {
        if (tpController.isDead || tpController.isRolling || tpController.customAction)
        {
            HideWallHighlight();
            return;
        }
        
        wallCheckTimer -= Time.fixedDeltaTime;
        
        // Проверка стены впереди
        if (!isOnWallWalk)
        {
            if (wallCheckTimer <= 0) 
            {
                CheckForAvailableWall();
            }
        }
        else
        {
            // Если уже идем по стене, проверяем, что все еще на ней
            if (!StillOnWall())
                ExitWallWalk();
            else
                ApplyWallGravity();
        }
    }
    
    // Проверяем наличие стены в нескольких направлениях, не активируя хождение автоматически
    private void CheckForAvailableWall()
    {
        wallAvailable = false;
        
        // Убираем проверку на grounded и input.magnitude
        if (tpController.isDead || tpController.isRolling || tpController.customAction)
        {
            HideWallHighlight();
            return;
        }
            
        // Обновляем массив направлений для проверки
        Vector3 forward = transform.forward;
        Vector3 right = transform.right;
        
        // Проверяем больше направлений
        checkDirections[0] = forward;
        checkDirections[1] = (forward + right).normalized;
        checkDirections[2] = (forward - right).normalized;
        checkDirections[3] = right;
        checkDirections[4] = -right;
        
        foreach (Vector3 direction in checkDirections)
        {
            if (Physics.Raycast(transform.position + Vector3.up * 0.5f, direction, out wallHit, wallCheckDistance, walkableWallLayer))
            {
                float wallAngle = Vector3.Angle(wallHit.normal, Vector3.up);
                
                // Делаем проверку угла более мягкой
                if (wallAngle >= minWallAngle && wallAngle <= maxWallAngle)
                {
                    wallAvailable = true;
                    availableWallHit = wallHit;
                    ShowWallHighlight(wallHit);
                    return;
                }
            }
        }
        
        HideWallHighlight();
    }
    
    // Показываем подсветку доступной стены
    private void ShowWallHighlight(RaycastHit hit)
    {
        if (wallHighlightObject == null) return;
        
        // Активируем объект подсветки
        wallHighlightObject.SetActive(true);
        
        // Настраиваем позицию, размер и поворот
        Vector3 position = hit.point + hit.normal * 0.01f; // Немного выше поверхности
        wallHighlightObject.transform.position = position;
        wallHighlightObject.transform.rotation = Quaternion.LookRotation(-hit.normal);
        
        // Настраиваем размер объекта подсветки в зависимости от характеристик персонажа
        float size = Mathf.Max(capsule.height, capsule.radius * 2) * 1.5f;
        wallHighlightObject.transform.localScale = new Vector3(size, size, size);
    }
    
    // Скрываем подсветку
    private void HideWallHighlight()
    {
        if (wallHighlightObject != null)
        {
            wallHighlightObject.SetActive(false);
        }
    }
    
    // Метод для входа в режим хождения по стене
    private void EnterWallWalk(RaycastHit hit)
    {
        isOnWallWalk = true;
        wallNormal = hit.normal;
        currentWall = hit.transform;
        
        // Скрываем подсветку, когда начинаем ходить по стене
        HideWallHighlight();
        
        // Подкидываем игрока вверх при активации ходьбы по стене
        Vector3 jumpPosition = transform.position;
        jumpPosition.y += 0.5f;
        transform.position = jumpPosition;
        
        // Сохраняем целевую ротацию, направленную вдоль стены
        targetRotation = Quaternion.LookRotation(-hit.normal, Vector3.up);
        
        // Уменьшаем stepOffset для корректной работы с гравитацией
        tpController.stepOffsetEnd = 0.05f;
        
        // Сохраняем текущую скорость для плавного перехода
        originalVelocity = rb.linearVelocity;
        
        // Немедленно устанавливаем позицию вплотную к стене
        Vector3 targetPosition = hit.point + hit.normal * (capsule.radius + wallOffset);
        targetPosition.y = transform.position.y; // С учетом подъема, который мы только что добавили
        transform.position = targetPosition;
        
        // Аккуратно останавливаем движение, чтобы избежать отскока
        rb.linearVelocity = new Vector3(0f, 0f, 0f);
        
        // Меняем физику для лучшего скольжения по стене
        rb.useGravity = false;
        
        // Убеждаемся, что контроллер считает стену землей
        tpController.isGrounded = true;
        
        // Сбрасываем параметр falling, чтобы анимация падения не зависала
        if (animator != null)
        {
            animator.SetBool("IsFalling", false);
            animator.SetFloat("VerticalVelocity", 0f);
        }
        
        // Отключаем контроллер персонажа
        tpController.enabled = false;
        
        // Включаем анимацию бега по стене
        PlayWallRunAnimation(true);
        
        // Сбрасываем таймер проверки стены, чтобы избежать немедленного выхода
        wallCheckTimer = 0.5f;
        
        // Временно блокируем контроль Invector, чтобы он не мешал нашему контролю движения 
        // во время начального перехода к стене
        if (tpInput != null)
        {
            tpInput.lockInput = true;
            // Разблокируем ввод через небольшую задержку
            Invoke("UnlockInvectorInput", 0.2f);
        }
        
        // Уведомляем пользователя
        Debug.Log("Вход в режим хождения по стене");
    }
    
    private void UnlockInvectorInput()
    {
        if (tpInput != null)
        {
            tpInput.lockInput = false;
        }
    }
    
    // Проверяем, что мы все еще на стене
    private bool StillOnWall()
    {
        if (currentWall == null) return false;
        
        // Проверяем стену в нескольких точках
        Vector3[] checkPoints = new Vector3[]
        {
            transform.position + Vector3.up * 0.5f,
            transform.position + Vector3.up * 1.0f,
            transform.position + Vector3.up * 1.5f
        };
        
        foreach (Vector3 point in checkPoints)
        {
            if (Physics.Raycast(point, -wallNormal, out wallHit, wallCheckDistance * 1.5f, walkableWallLayer))
            {
                float wallAngle = Vector3.Angle(wallHit.normal, Vector3.up);
                if (wallAngle >= minWallAngle && wallAngle <= maxWallAngle)
                {
                    return true;
                }
            }
        }
        
        return false;
    }
    
    // Применяем гравитацию и контроль движения вдоль стены
    private void ApplyWallGravity()
    {
        // Проверяем столкновение со стеной
        bool isCollidingWithWall = false;
        Vector3 origin = transform.position + Vector3.up * (capsule.height * 0.5f - capsule.radius);
        
        if (Physics.Raycast(origin, -wallNormal, out wallHit, capsule.radius + wallOffset + 0.4f, walkableWallLayer))
        {
            isCollidingWithWall = true;
            
            // Убеждаемся, что параметр isGrounded всегда true при хождении по стене
            tpController.isGrounded = true;
        }
        
        // Получаем ввод напрямую от Input вместо tpController
        float horizontalInput = Input.GetAxis("Horizontal");
        float verticalInput = Input.GetAxis("Vertical");
        
        // Проверяем, нажата ли клавиша "вперед"
        bool isMovingForward = verticalInput > 0.1f;
        bool isInputActive = Mathf.Abs(horizontalInput) > 0.1f || isMovingForward;
        
        // Преобразуем ввод в движение вдоль стены
        Vector3 rightDirection = Vector3.Cross(wallNormal, Vector3.up).normalized;
        Vector3 upDirection = Vector3.up;  // Используем вектор вверх для подъема по стене
        Vector3 forwardDirection = Vector3.Cross(rightDirection, wallNormal).normalized;
        
        // Вектор скорости для движения по стене
        Vector3 wallVelocity = Vector3.zero;
        
        // Используем input только если нажата клавиша вперед
        if (isMovingForward)
        {
            // Используем горизонтальный ввод напрямую (без инвертирования)
            
            // Боковое движение по оси X без наклона, вдвое медленнее чем движение вперед
            Vector3 sideDirection = Vector3.right * horizontalInput * 1.0f;
            
            // Движение "вперед" - это движение вверх по стене!
            Vector3 climbDirection = upDirection;
            
            // Комбинируем для получения окончательного направления
            // Напрямую используем мировые координаты для перемещения
            Vector3 moveDirection = climbDirection.normalized;
            
            // Устанавливаем фиксированную скорость движения вверх
            wallVelocity = moveDirection * wallMoveSpeed;
            
            // Добавляем боковое движение по X напрямую к позиции игрока, делаем его вдвое медленнее
            transform.position += sideDirection * Time.deltaTime * (wallMoveSpeed * 0.25f);
        }
        
        // Включаем анимацию бега по стене только когда есть активный ввод,
        // иначе возвращаемся к FreeLocomotion
        PlayWallRunAnimation(isInputActive);
        
        // Устанавливаем окончательный вектор скорости
        rb.linearVelocity = wallVelocity;
        
        // Поворачиваем игрока на -90 градусов по оси X только если он сталкивается со стеной
        Quaternion targetWallRotation;
        if (isCollidingWithWall)
        {
            // При столкновении со стеной - поворот на -90 по X с ограничением Z до 10 градусов
            float targetZAngle = 0f;
            
            // Если есть горизонтальный ввод, добавляем небольшой наклон по Z (до 10 градусов)
            if (Mathf.Abs(horizontalInput) > 0.1f)
            {
                // Наклон в сторону движения (влево/вправо)
                targetZAngle = -horizontalInput * 10f; // Максимум 10 градусов в любую сторону
            }
            
            targetWallRotation = Quaternion.Euler(-90f, targetRotation.eulerAngles.y, targetZAngle);
        }
        else
        {
            // Без столкновения - нормальная ориентация с поворотом в направлении стены
            targetWallRotation = Quaternion.Euler(0f, targetRotation.eulerAngles.y, 0f);
        }
        
        // Плавно поворачиваем персонажа
        transform.rotation = Quaternion.Slerp(transform.rotation, targetWallRotation, Time.deltaTime * rotationSpeed);
        
        // Держим персонажа на фиксированном расстоянии от стены, только если он сталкивается с ней
        if (isCollidingWithWall)
        {
            Vector3 targetPosition = wallHit.point + wallNormal * (capsule.radius + wallOffset);
            // Не меняем высоту Y, чтобы подъем происходил через velocity
            targetPosition.y = transform.position.y;
            
            // Плавно притягиваем к стене только по горизонтали
            Vector3 currentPos = transform.position;
            Vector3 lerpPos = Vector3.Lerp(currentPos, targetPosition, Time.deltaTime * 10f);
            transform.position = new Vector3(lerpPos.x, currentPos.y, lerpPos.z);
        }
        else
        {
            // Если столкновения нет, но мы всё ещё в режиме хождения по стене, 
            // проверяем, может нам уже нужно выйти из этого режима
            if (!StillOnWall())
            {
                ExitWallWalk();
                return;
            }
        }
        
        // Проверяем, не пытается ли игрок отойти от стены
        if (verticalInput < -0.7f)
        {
            ExitWallWalk();
        }
        
        // Если игрок нажал кнопку E снова, выходим из режима хождения по стене
        if (Input.GetKeyDown(activateWallWalkKey))
        {
            ExitWallWalk();
        }
        
        // Показываем отладочные лучи для проверки столкновения со стеной
        if (showDebugRays)
        {
            Debug.DrawRay(origin, -wallNormal * (capsule.radius + wallOffset + 0.4f), 
                isCollidingWithWall ? Color.green : Color.red);
            Debug.DrawRay(transform.position, upDirection * 2f, Color.blue); // Вектор подъема
        }
    }
    
    // Метод для управления анимацией бега по стене
    private void PlayWallRunAnimation(bool play)
    {
        if (animator == null) return;

        // Сбрасываем состояния анимаций, связанные с падением
        if (play)
        {
            // Жестко сбрасываем все анимационные состояния связанные с падением
            animator.SetBool("IsFalling", false);
            animator.SetBool("IsJumping", false);
            
            // Добавим триггер для принудительного выхода из анимации падения
            animator.SetTrigger("ResetFall");
        }
        
        // Проверяем, нужно ли менять состояние анимации
        if (play != wasWallRunningLastFrame)
        {
            if (play)
            {
                // Запускаем анимацию NinjaRun, с максимальным приоритетом для перебивания падения
                animator.SetBool("IsGrounded", true); // Явно устанавливаем приземление
                animator.SetFloat("VerticalVelocity", 0f);
                
                // Используем более прямой метод для гарантированного перебивания любой анимации
                animator.Play(wallRunAnimHash, 0, 0f);
                
                // Ждем один кадр и еще раз сбрасываем параметры для страховки
                StartCoroutine(ResetFallParametersDelayed());
            }
            else
            {
                // Возвращаемся к настраиваемой анимации (по умолчанию FreeLocomotion)
                animator.CrossFade(returnAnimHash, 0.2f);
                
                // Устанавливаем параметры анимации для корректного возврата к системе Invector
                if (tpController != null && tpController.enabled)
                {
                    // Сбрасываем параметры движения для корректной работы Invector после перехода
                    animator.SetFloat("InputMagnitude", 0f);
                }
            }
            
            // Запоминаем текущее состояние анимации
            wasWallRunningLastFrame = play;
        }
    }
    
    // Корутина для сброса параметров падения с задержкой
    private System.Collections.IEnumerator ResetFallParametersDelayed()
    {
        // Ждем один кадр
        yield return null;
        
        // И снова сбрасываем все параметры
        if (animator != null)
        {
            animator.SetBool("IsFalling", false);
            animator.SetBool("IsJumping", false);
            animator.SetFloat("VerticalVelocity", 0f);
            animator.SetBool("IsGrounded", true);
        }
    }
    
    // Метод для проверки стены при падении, чтобы можно было начать бег по стене во время падения
    void OnAnimatorStateEnter(string stateName)
    {
        // Если входим в состояние падения и у нас активен таймер задержки проверки стены,
        // сбрасываем его для немедленной проверки доступных стен
        if (stateName == "Falling" && wallCheckTimer > 0)
        {
            wallCheckTimer = 0f;
        }
    }
    
    // Выход из режима хождения по стене
    private void ExitWallWalk()
    {
        isOnWallWalk = false;
        currentWall = null;
        
        // Отключаем анимацию бега по стене
        PlayWallRunAnimation(false);
        
        // Восстанавливаем оригинальное значение stepOffset
        tpController.stepOffsetEnd = originalStepOffset;
        
        // Возвращаем физику в нормальный режим
        rb.useGravity = true;
        
        // Снова включаем контроллер персонажа
        tpController.enabled = true;
        
        // Восстанавливаем нормальное вращение (без поворота по X)
        Vector3 currentRotation = transform.rotation.eulerAngles;
        transform.rotation = Quaternion.Euler(0, currentRotation.y, 0);
        
        // Устанавливаем параметры анимации для корректного возврата к системе Invector
        if (animator != null)
        {
            // Сбрасываем параметры анимаций, чтобы Invector мог корректно определить следующую анимацию
            animator.SetFloat("InputMagnitude", 0f);
            animator.SetBool("IsJumping", false);
            animator.SetBool("IsFalling", true); // Устанавливаем IsFalling в true, так как игрок будет падать после отпускания стены
        }
        
        // Сбрасываем явно isGrounded, чтобы контроллер сам определил состояние заземления
        tpController.isGrounded = false;
        
        // Устанавливаем задержку перед повторной проверкой стены
        wallCheckTimer = wallCheckCooldown;
        
        Debug.Log("Выход из режима хождения по стене");
    }
    
    // Возвращает true, если персонаж находится в режиме хождения по стене
    public bool IsWallWalking()
    {
        return isOnWallWalk;
    }
    
    // Очистка ресурсов при уничтожении объекта
    private void OnDestroy()
    {
        if (wallHighlightObject != null)
        {
            Destroy(wallHighlightObject);
        }
    }
} 