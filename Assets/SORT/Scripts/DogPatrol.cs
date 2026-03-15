using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

public class DogPatrol : MonoBehaviour
{
    [Header("Патрульные точки (waypoints)")]
    public List<Transform> patrolPoints = new List<Transform>();
    [Tooltip("Расстояние до waypoint, при котором считается что точка достигнута")]
    public float pointReachDistance = 2.0f;
    public float patrolWaitTime = 1.0f;

    [Header("Обнаружение игрока")]
    public string playerTag = "Player";
    public string vehicleTag = "Vehicle"; // Тег для машин
    public float chaseDistance = 15f;
    public Transform spawnPoint; // Куда переносить игрока после поимки

    [Header("Скорость движения")]
    public float patrolSpeed = 3.5f;
    public float chaseSpeed = 70f; // ЭКСТРЕМАЛЬНАЯ скорость - в 20 раз быстрее обычной

    [Header("Поимка игрока")]
    public float catchDistance = 1.5f;
    [Tooltip("Расстояние между игроком и машиной при телепортации")]
    public float vehicleSpawnOffset = 3f;
    private bool playerCaught = false;
    
    [Header("Настройки косточки")]
    [Tooltip("Может ли собака видеть и реагировать на косточки")]
    public bool canSeeBones = true;
    [Tooltip("Радиус обнаружения косточки (в метрах)")]
    public float boneDetectionRadius = 3f;
    
    [Header("Настройки NavMeshAgent")]
    [Tooltip("Минимальное расстояние для обновления пути при преследовании")]
    public float pathUpdateDistance = 1.5f; // Уменьшено для более частого обновления
    [Tooltip("Частота обновления пути (в секундах)")]
    public float pathUpdateInterval = 0.1f; // Увеличена частота обновления
    [Tooltip("Расстояние торможения при приближении к цели")]
    public float slowDownDistance = 2.0f; // Уменьшено для более агрессивного подхода
    
    [Header("Система точного преследования")]
    [Tooltip("Включить предиктивное преследование")]
    public bool predictiveChasing = true;
    [Tooltip("Время предсказания движения игрока (секунды)")]
    public float predictionTime = 0.5f;
    [Tooltip("Дистанция начала торможения")]
    public float brakingDistance = 5f;
    [Tooltip("Минимальная скорость при торможении (% от максимальной)")]
    [Range(0.1f, 1f)] public float minBrakingSpeed = 0.2f;
    [Tooltip("Множитель торможения (чем больше, тем резче тормозит)")]
    public float brakingMultiplier = 3f;

    // Удаляю все поля и переменные, связанные с лаем

    private NavMeshAgent agent;
    private int currentPoint = 0;
    private float waitTimer = 0f;
    private bool waiting = false;

    private Transform player;
    private bool chasing = false;
    private bool playerInZone = false;
    private Animator animator;
    
    // Переменные для оптимизации преследования
    private float lastPathUpdateTime = 0f;
    private Vector3 lastTargetPosition = Vector3.zero;
    
    // Переменные для предиктивного преследования
    private Vector3 playerVelocity = Vector3.zero;
    private Vector3 lastPlayerPosition = Vector3.zero;
    private float lastVelocityUpdateTime = 0f;
    
    // Переменные для системы косточек
    private BoneBehavior targetBone = null;
    private bool chasingBone = false;
    private bool eatingBone = false;
    private float eatingStartTime = 0f;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.speed = patrolSpeed;
        
        // ЭКСТРЕМАЛЬНЫЕ настройки для сверхбыстрой но точной поимки
        agent.stoppingDistance = 0.05f; // Максимально близко к цели
        agent.angularSpeed = 720f; // Сверхбыстрые повороты для мгновенных разворотов
        agent.acceleration = 50f; // Экстремальное ускорение для мгновенной реакции
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance; // Отключаем избегание
        agent.radius = 0.25f; // Минимальный радиус для максимальной маневренности
        agent.avoidancePriority = 99; // Максимальный приоритет
        agent.autoBraking = true; // ВКЛЮЧАЕМ автоторможение для точной остановки
        agent.autoRepath = true; // Автоматический пересчет пути
        
        animator = GetComponent<Animator>();
        // audioSource = GetComponent<AudioSource>(); // удаляю
        
        // Проверяем, что агент находится на NavMesh перед установкой цели
        if (agent.isOnNavMesh && patrolPoints.Count > 0)
        {
            agent.SetDestination(patrolPoints[0].position);
        }
        else
        {
            Debug.LogWarning($"[DogPatrol] Агент {gameObject.name} не на NavMesh или нет точек патруля!");
        }
    }

    public void SetChasing(bool chase, Transform target)
    {
        chasing = chase;
        player = target;
        if (chasing)
        {
            // Устанавливаем базовую скорость преследования
            agent.speed = chaseSpeed;
            
            // Сбрасываем таймеры для немедленного обновления пути
            lastPathUpdateTime = 0f;
            lastTargetPosition = Vector3.zero;
            
            // Инициализируем систему предиктивного преследования
            if (target != null)
            {
                lastPlayerPosition = target.position;
                playerVelocity = Vector3.zero;
                lastVelocityUpdateTime = Time.time;
            }
            
            // Немедленно устанавливаем цель при начале преследования
            if (target != null && agent.isOnNavMesh)
            {
                Transform chaseTarget = GetChaseTarget();
                if (chaseTarget != null)
                {
                    agent.SetDestination(chaseTarget.position);
                    lastPathUpdateTime = Time.time;
                    lastTargetPosition = chaseTarget.position;
                }
            }
        }
        else
        {
            agent.speed = patrolSpeed;
            // Сбрасываем переменные преследования
            lastPathUpdateTime = 0f;
            lastTargetPosition = Vector3.zero;
            
            // Возврат к патрулю (только если не едим косточку)
            if (!eatingBone && patrolPoints.Count > 0 && agent.isOnNavMesh)
                agent.SetDestination(patrolPoints[currentPoint].position);
        }
    }

    void Update()
    {
        float currentSpeed = agent.velocity.magnitude;
        if (animator != null)
            animator.SetFloat("Speed", currentSpeed);
        
        // Обработка преследования (игрока или косточки - определяется ТОЛЬКО через DogTrigger!)
        if (chasing && player != null && !playerCaught)
        {
            // Определяем цель для преследования (игрок или машина)
            Transform chaseTarget = GetChaseTarget();
            
            if (chaseTarget != null && agent.isOnNavMesh)
            {
                // Вычисляем скорость движения игрока для предсказания
                UpdatePlayerVelocity(chaseTarget);
                
                // Получаем предсказанную позицию цели
                Vector3 predictedPosition = GetPredictedTargetPosition(chaseTarget);
                
                // Проверяем расстояние до РЕАЛЬНОЙ цели для поимки
                float distanceToTarget = Vector3.Distance(transform.position, chaseTarget.position);
                
                if (distanceToTarget <= catchDistance)
                {
                    CatchPlayer();
                    return;
                }
                
                // Обновляем путь к предсказанной позиции
                bool shouldUpdatePath = false;
                
                if (Time.time - lastPathUpdateTime >= pathUpdateInterval)
                {
                    shouldUpdatePath = true;
                }
                else if (Vector3.Distance(predictedPosition, lastTargetPosition) >= pathUpdateDistance)
                {
                    shouldUpdatePath = true;
                }
                
                if (shouldUpdatePath)
                {
                    agent.SetDestination(predictedPosition);
                    lastPathUpdateTime = Time.time;
                    lastTargetPosition = predictedPosition;
                }
                
                // Интеллектуальное управление скоростью с учетом расстояния до РЕАЛЬНОЙ цели
                ApplyIntelligentSpeedControl(distanceToTarget);
            }
            return;
        }
        if (patrolPoints.Count == 0) return;
        
        // Проверяем, что агент на NavMesh перед проверкой расстояния
        if (!agent.isOnNavMesh) return;

        // Проверяем достижение точки патруля
        if (!waiting && !agent.pathPending)
        {
            float distanceToTarget = Vector3.Distance(transform.position, patrolPoints[currentPoint].position);
            
            // Достигли точки если: осталось мало пути ИЛИ близко к цели
            if (agent.remainingDistance <= pointReachDistance || distanceToTarget <= pointReachDistance)
            {
                waiting = true;
                waitTimer = 0f;
                Debug.Log($"[DogPatrol] Достигнута точка {currentPoint}: remainingDistance={agent.remainingDistance:F2}, distanceToTarget={distanceToTarget:F2}");
            }
        }

        if (waiting)
        {
            waitTimer += Time.deltaTime;
            if (waitTimer >= patrolWaitTime)
            {
                waiting = false;
                currentPoint = (currentPoint + 1) % patrolPoints.Count;
                if (agent.isOnNavMesh)
                {
                    agent.SetDestination(patrolPoints[currentPoint].position);
                }
            }
        }
    }
    
    private Transform GetChaseTarget()
    {
        if (player == null) return null;
        
        Debug.Log($"[DogPatrol] 🔍 GetChaseTarget: target={player.name}");
        
        // ПРИОРИТЕТ 1: Если цель - косточка, просто преследуем её
        if (player.CompareTag("Bone"))
        {
            Debug.Log($"[DogPatrol] 🦴 Преследую косточку: {player.name}");
            return player;
        }
        
        // ПРИОРИТЕТ 2: Проверяем, находится ли игрок в машине
        VehicleController vehicle = player.GetComponent<VehicleController>();
        if (vehicle == null)
        {
            vehicle = player.GetComponentInParent<VehicleController>();
        }
        
        // Если игрок в машине, преследуем машину
        if (vehicle != null && vehicle.isPlayerInVehicle)
        {
            Debug.Log($"[DogPatrol] ✅ Игрок в машине, преследую машину: {vehicle.name}");
            return vehicle.transform;
        }
        
        // Иначе преследуем игрока
        Debug.Log("[DogPatrol] Игрок не в машине, преследую игрока");
        return player;
    }

    private void CatchPlayer()
    {
        Debug.Log($"[DogPatrol] 🚨 ПОИМКА! target={player?.name}, playerCaught={playerCaught}");
        
        // ПРИОРИТЕТ 1: Если поймали косточку - едим её
        if (player != null && player.CompareTag("Bone"))
        {
            Debug.Log($"[DogPatrol] 🦴 ПОЙМАЛ КОСТОЧКУ! Начинаю есть {player.name}");
            EatBone();
            return;
        }
        
        // ПРИОРИТЕТ 2: Поймали игрока
        playerCaught = true;
        agent.isStopped = true;
        if (animator != null)
            animator.SetTrigger("Attack");
        
        // Вызовем UI и затемнение через DogPatrolUI.Instance.ShowCatchScreen(...)
        if (DogPatrolUI.Instance != null)
        {
            Debug.Log("[DogPatrol] Используем UI для поимки");
            DogPatrolUI.Instance.ShowCatchScreen(() => {
                Debug.Log("[DogPatrol] UI завершен, вызываю телепортацию");
                TeleportPlayerAndVehicle();
                playerCaught = false;
                agent.isStopped = false;
                SetChasing(false, null);
            });
        }
        else
        {
            Debug.Log("[DogPatrol] UI не найден, вызываю телепортацию сразу");
            TeleportPlayerAndVehicle();
            playerCaught = false;
            agent.isStopped = false;
            SetChasing(false, null);
        }
    }
    
    private void TeleportPlayerAndVehicle()
    {
        Debug.Log($"[DogPatrol] 🔍 TeleportPlayerAndVehicle вызван! spawnPoint={spawnPoint?.name}, player={player?.name}");
        
        if (spawnPoint == null)
        {
            Debug.LogError("[DogPatrol] ❌ Spawn Point не назначен!");
            return;
        }
        
        if (player == null)
        {
            Debug.LogError("[DogPatrol] ❌ Player не найден!");
            return;
        }
        
        Debug.Log("[DogPatrol] Начинаю телепортацию игрока и машины...");
        
        // Сначала ищем машину по тегу
        VehicleController vehicle = FindVehicleByTag();
        
        if (vehicle == null)
        {
            // Если не нашли по тегу, ищем через игрока
            vehicle = player.GetComponent<VehicleController>();
            Debug.Log($"[DogPatrol] VehicleController на игроке: {vehicle?.name}");
            
            if (vehicle == null)
            {
                // Ищем машину в родителях игрока
                vehicle = player.GetComponentInParent<VehicleController>();
                Debug.Log($"[DogPatrol] VehicleController в родителях: {vehicle?.name}");
            }
        }
        
        if (vehicle != null)
        {
            Debug.Log($"[DogPatrol] Найдена машина: {vehicle.name}, isPlayerInVehicle={vehicle.isPlayerInVehicle}");
            
            if (vehicle.isPlayerInVehicle)
            {
                // Игрок в машине - сначала выводим его из машины
                Debug.Log("[DogPatrol] ✅ Игрок в машине, сначала вывожу его из машины");
                
                // Выходим из машины
                vehicle.ExitVehicle();
                
                // Ждем один кадр, чтобы убедиться, что игрок вышел
                StartCoroutine(TeleportAfterExit(vehicle));
            }
            else
            {
                Debug.Log("[DogPatrol] ❌ Игрок не в машине (isPlayerInVehicle = false)");
                TeleportPlayerOnly();
            }
        }
        else
        {
            Debug.Log("[DogPatrol] ❌ Машина не найдена, переношу только игрока");
            TeleportPlayerOnly();
        }
    }
    
    private VehicleController FindVehicleByTag()
    {
        // Ищем все объекты с тегом Vehicle
        GameObject[] vehicles = GameObject.FindGameObjectsWithTag(vehicleTag);
        Debug.Log($"[DogPatrol] Найдено машин с тегом '{vehicleTag}': {vehicles.Length}");
        
        foreach (GameObject vehicleObj in vehicles)
        {
            VehicleController vehicle = vehicleObj.GetComponent<VehicleController>();
            if (vehicle != null && vehicle.isPlayerInVehicle)
            {
                Debug.Log($"[DogPatrol] ✅ Найдена машина с игроком: {vehicle.name}");
                return vehicle;
            }
        }
        
        Debug.Log("[DogPatrol] Машина с игроком не найдена по тегу");
        return null;
    }
    
    // Методы для предиктивного преследования
    private void UpdatePlayerVelocity(Transform target)
    {
        if (!predictiveChasing) return;
        
        float deltaTime = Time.time - lastVelocityUpdateTime;
        if (deltaTime > 0.05f) // Обновляем скорость каждые 50ms
        {
            Vector3 currentPosition = target.position;
            playerVelocity = (currentPosition - lastPlayerPosition) / deltaTime;
            lastPlayerPosition = currentPosition;
            lastVelocityUpdateTime = Time.time;
        }
    }
    
    private Vector3 GetPredictedTargetPosition(Transform target)
    {
        if (!predictiveChasing)
            return target.position;
        
        // Предсказываем где будет игрок через predictionTime секунд
        Vector3 predictedPos = target.position + playerVelocity * predictionTime;
        
        // Проверяем, что предсказанная позиция находится на NavMesh
        UnityEngine.AI.NavMeshHit hit;
        if (UnityEngine.AI.NavMesh.SamplePosition(predictedPos, out hit, 5f, UnityEngine.AI.NavMesh.AllAreas))
        {
            return hit.position;
        }
        
        // Если предсказанная позиция недоступна, возвращаем текущую позицию игрока
        return target.position;
    }
    
    private void ApplyIntelligentSpeedControl(float distanceToTarget)
    {
        if (distanceToTarget <= brakingDistance)
        {
            // Применяем интеллектуальное торможение
            float brakingFactor = 1f - (distanceToTarget / brakingDistance);
            brakingFactor = Mathf.Pow(brakingFactor, brakingMultiplier); // Нелинейное торможение
            
            float targetSpeed = Mathf.Lerp(chaseSpeed, chaseSpeed * minBrakingSpeed, brakingFactor);
            agent.speed = targetSpeed;
            
            // Увеличиваем частоту обновления пути при торможении
            pathUpdateInterval = 0.05f; // 20 раз в секунду при торможении
        }
        else
        {
            // Максимальная скорость на дальней дистанции
            agent.speed = chaseSpeed;
            pathUpdateInterval = 0.1f; // Обычная частота обновления
        }
    }
    
    // ========== МЕТОДЫ ДЛЯ РАБОТЫ С КОСТОЧКАМИ ==========
    
    /// <summary>
    /// Устанавливает косточку как цель для преследования
    /// </summary>
    public void SetChasingBone(BoneBehavior bone)
    {
        if (bone == null) return;
        
        Debug.Log($"[DogPatrol] 🦴 КОСТОЧКА! {bone.name} теперь цель собаки! Прерываю все другие действия!");
        
        // Прерываем все текущие действия
        chasing = false;
        player = null;
        playerCaught = false;
        eatingBone = false;
        
        // Устанавливаем новую цель
        targetBone = bone;
        chasingBone = true;
        
        // Устанавливаем максимальную скорость для достижения косточки
        agent.speed = chaseSpeed;
        
        // Немедленно направляемся к косточке
        if (agent.isOnNavMesh)
        {
            agent.SetDestination(bone.GetPosition());
            Debug.Log($"[DogPatrol] Бегу к косточке на позиции: {bone.GetPosition()}");
        }
    }
    
    /// <summary>
    /// Обрабатывает поведение с косточкой
    /// </summary>
    private void HandleBoneBehavior()
    {
        if (targetBone == null)
        {
            // Косточка исчезла, возвращаемся к обычному поведению
            ResetBoneState();
            return;
        }
        
        if (eatingBone)
        {
            // Собака ест косточку - просто стоим на месте
            agent.isStopped = true;
            return;
        }
        
        if (chasingBone)
        {
            // Преследуем косточку
            float distanceToBone = Vector3.Distance(transform.position, targetBone.GetPosition());
            
            if (targetBone.CanBeEaten(transform.position))
            {
                // Достигли косточки, начинаем есть
                StartEatingBone();
            }
            else
            {
                // Продолжаем движение к косточке
                if (agent.isOnNavMesh)
                {
                    agent.SetDestination(targetBone.GetPosition());
                }
            }
        }
    }
    
    /// <summary>
    /// Начинает процесс поедания косточки
    /// </summary>
    private void StartEatingBone()
    {
        if (targetBone == null) return;
        
        Debug.Log($"[DogPatrol] 🍽️ Начинаю есть косточку {targetBone.name}!");
        
        chasingBone = false;
        eatingBone = true;
        eatingStartTime = Time.time;
        
        // Останавливаем движение
        agent.isStopped = true;
        
        // Уведомляем косточку о начале поедания
        targetBone.StartEating(transform);
    }
    
    /// <summary>
    /// Вызывается косточкой когда она съедена
    /// </summary>
    public void OnBoneEaten(BoneBehavior bone)
    {
        Debug.Log($"[DogPatrol] ✅ Косточка {bone.name} съедена! Возвращаюсь к обычному поведению.");
        
        ResetBoneState();
    }
    
    /// <summary>
    /// Сбрасывает состояние связанное с косточками
    /// </summary>
    private void ResetBoneState()
    {
        chasingBone = false;
        eatingBone = false;
        targetBone = null;
        eatingStartTime = 0f;
        
        // Возобновляем движение
        agent.isStopped = false;
        
        // Возвращаемся к патрулю
        agent.speed = patrolSpeed;
        if (patrolPoints.Count > 0 && agent.isOnNavMesh)
        {
            agent.SetDestination(patrolPoints[currentPoint].position);
        }
        
        Debug.Log("[DogPatrol] Состояние косточки сброшено, возвращаюсь к патрулю");
    }
    
    /// <summary>
    /// Проверяет, преследует ли собака сейчас косточку
    /// </summary>
    public bool IsChasingBone()
    {
        return chasingBone || eatingBone;
    }
    
    private System.Collections.IEnumerator TeleportAfterExit(VehicleController vehicle)
    {
        Debug.Log("[DogPatrol] 🚀 TeleportAfterExit запущен");
        
        // Ждем один кадр, чтобы игрок точно вышел из машины
        yield return null;
        
        Debug.Log("[DogPatrol] Телепортирую игрока и машину на спавн поинт");
        
        // Переносим игрока на спавн поинт
        if (player != null)
        {
            Vector3 oldPlayerPos = player.position;
            player.position = spawnPoint.position;
            player.rotation = spawnPoint.rotation;
            
            // Останавливаем движение игрока
            Rigidbody playerRb = player.GetComponent<Rigidbody>();
            if (playerRb != null)
            {
                playerRb.linearVelocity = Vector3.zero;
                playerRb.angularVelocity = Vector3.zero;
            }
            
            Debug.Log($"[DogPatrol] ✅ Игрок перенесен с {oldPlayerPos} на {spawnPoint.position}");
        }
        else
        {
            Debug.LogError("[DogPatrol] ❌ Player стал null после выхода из машины!");
        }
        
        // Переносим машину на спавн поинт (немного в стороне от игрока)
        if (vehicle != null)
        {
            Vector3 oldVehiclePos = vehicle.transform.position;
            Vector3 vehicleSpawnPosition = spawnPoint.position + spawnPoint.right * vehicleSpawnOffset;
            vehicle.transform.position = vehicleSpawnPosition;
            vehicle.transform.rotation = spawnPoint.rotation;
            
            // Останавливаем движение машины
            Rigidbody vehicleRb = vehicle.GetComponent<Rigidbody>();
            if (vehicleRb != null)
            {
                vehicleRb.linearVelocity = Vector3.zero;
                vehicleRb.angularVelocity = Vector3.zero;
            }
            
            Debug.Log($"[DogPatrol] ✅ Машина перенесена с {oldVehiclePos} на {vehicleSpawnPosition}");
        }
        else
        {
            Debug.LogError("[DogPatrol] ❌ Vehicle стал null!");
        }
        
        // Добавляем небольшую задержку, чтобы игрок не мог сразу сесть обратно в машину
        yield return new WaitForSeconds(0.5f);
        
        Debug.Log("[DogPatrol] ✅ Телепортация завершена, игрок может снова садиться в машину");
    }
    
    private void TeleportPlayerOnly()
    {
        if (player != null)
        {
            player.position = spawnPoint.position;
            player.rotation = spawnPoint.rotation;
            
            // Останавливаем движение игрока
            Rigidbody playerRb = player.GetComponent<Rigidbody>();
            if (playerRb != null)
            {
                playerRb.linearVelocity = Vector3.zero;
                playerRb.angularVelocity = Vector3.zero;
            }
            
            Debug.Log($"[DogPatrol] ✅ Игрок перенесен на позицию: {spawnPoint.position}");
        }
    }
    
    /// <summary>
    /// Метод для поедания косточки
    /// </summary>
    private void EatBone()
    {
        if (player == null) return;
        
        Debug.Log($"[DogPatrol] 🍽️ Начинаю есть косточку {player.name}");
        
        // Останавливаем собаку
        agent.isStopped = true;
        
        // Получаем компонент BoneBehavior
        BoneBehavior bone = player.GetComponent<BoneBehavior>();
        if (bone != null)
        {
            // Вызываем метод поедания косточки
            bone.StartEating(transform);
            Debug.Log($"[DogPatrol] ✅ Косточка {player.name} начинает поедаться");
        }
        else
        {
            Debug.LogError($"[DogPatrol] ❌ BoneBehavior не найден на {player.name}!");
        }
        
        // Через 5 секунд возвращаемся к патрулированию
        Invoke("FinishEatingBone", 5f);
    }
    
    /// <summary>
    /// Завершение поедания косточки
    /// </summary>
    private void FinishEatingBone()
    {
        Debug.Log("[DogPatrol] ✅ Закончил есть косточку, возвращаюсь к патрулированию");
        
        // Возобновляем движение
        agent.isStopped = false;
        
        // Прекращаем преследование
        SetChasing(false, null);
    }
    

} 