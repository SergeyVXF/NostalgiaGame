using UnityEngine;
using UnityEngine.AI;
using Invector.vCharacterController;
using Invector;

public class RepulsiveSphere : MonoBehaviour
{
    [Header("Настройки отталкивания")]
    [Tooltip("Сила отталкивания AI")]
    public float repulsionForce = 100f;
    
    [Tooltip("Радиус действия отталкивания")]
    public float repulsionRadius = 15f;
    
    [Header("Множители силы")]
    [Tooltip("Множитель силы для Ragdoll AI")]
    public float ragdollForceMultiplier = 15f;
    
    [Tooltip("Множитель силы для NavMesh AI")]
    public float navMeshForceMultiplier = 12f;
    
    [Tooltip("Время жизни сферы (секунды)")]
    public float lifetime = 2.0f;
    
    [Header("Движение сферы")]
    [Tooltip("Скорость движения сферы вперед")]
    public float moveSpeed = 15f;
    
    [Tooltip("Направление движения (устанавливается при создании)")]
    public Vector3 moveDirection = Vector3.forward;
    
    [Header("Визуальные эффекты")]
    [Tooltip("Скорость вращения сферы")]
    public float rotationSpeed = 360f;
    
    [Tooltip("Скорость изменения размера")]
    public float scaleSpeed = 2f;
    
    [Header("Отладка")]
    [Tooltip("Показывать отладочную информацию")]
    public bool showDebugLog = true;
    
    private float startTime;
    private Vector3 initialScale;
    
    private void Start()
    {
        startTime = Time.time;
        initialScale = transform.localScale;
        
        if (showDebugLog)
        {
            Debug.Log($"[RepulsiveSphere] 💥 ОТТАЛКИВАЮЩАЯ СФЕРА СОЗДАНА!");
            Debug.Log($"[RepulsiveSphere] 📍 Позиция: {transform.position}");
            Debug.Log($"[RepulsiveSphere] 📏 Радиус: {repulsionRadius}м");
            Debug.Log($"[RepulsiveSphere] 🚀 Сила: {repulsionForce}");
        }
        
        // Немедленно отталкиваем всех AI в радиусе
        RepelAllAI();
        
        // Уничтожаем через заданное время
        Destroy(gameObject, lifetime);
    }
    
    /// <summary>
    /// Устанавливает направление движения сферы
    /// </summary>
    public void SetMoveDirection(Vector3 direction)
    {
        // Проверяем валидность направления
        if (direction.magnitude < 0.1f)
        {
            Debug.LogWarning("[RepulsiveSphere] ⚠️ Получено нулевое направление, использую Vector3.forward");
            moveDirection = Vector3.forward;
        }
        else
        {
            moveDirection = direction.normalized;
        }
        
        if (showDebugLog)
        {
            Debug.Log($"[RepulsiveSphere] 🎯 Направление движения установлено: {moveDirection}");
            Debug.Log($"[RepulsiveSphere] 📍 Текущая позиция: {transform.position}");
            Debug.Log($"[RepulsiveSphere] 🚀 Скорость движения: {moveSpeed}");
        }
    }
    
    private void Update()
    {
        // Проверяем валидность направления движения
        if (moveDirection.magnitude < 0.1f)
        {
            if (showDebugLog)
                Debug.LogWarning("[RepulsiveSphere] ⚠️ Направление движения не установлено, использую Vector3.forward");
            moveDirection = Vector3.forward;
        }
        
        // Движение сферы вперед
        if (moveSpeed > 0)
        {
            transform.Translate(moveDirection * moveSpeed * Time.deltaTime, Space.World);
        }
        
        // Вращение сферы
        if (rotationSpeed > 0)
        {
            transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);
        }
        
        // Пульсация размера
        if (scaleSpeed > 0)
        {
            float elapsed = Time.time - startTime;
            float scaleMultiplier = 1f + Mathf.Sin(elapsed * scaleSpeed) * 0.3f;
            transform.localScale = initialScale * scaleMultiplier;
        }
        
        // Постоянное отталкивание AI во время движения
        RepelAllAI();
    }
    
    /// <summary>
    /// Отталкивает всех AI в радиусе действия
    /// </summary>
    private void RepelAllAI()
    {
        // Ищем всех AI с ragdoll системой
        RepelRagdollAI();
        
        // Ищем всех AI с NavMeshAgent
        RepelNavMeshAI();
    }
    
    /// <summary>
    /// Отталкивает AI с ragdoll системой
    /// </summary>
    private void RepelRagdollAI()
    {
        vRagdoll[] allRagdolls = FindObjectsOfType<vRagdoll>();
        
        foreach (var ragdoll in allRagdolls)
        {
            if (ragdoll == null || !ragdoll.gameObject.activeInHierarchy)
                continue;
            
            // ИСКЛЮЧАЕМ ИГРОКА из отталкивания
            if (ragdoll.CompareTag("Player"))
                continue;
            
            float distance = Vector3.Distance(transform.position, ragdoll.transform.position);
            
            if (distance <= repulsionRadius)
            {
                // Проверяем состояние AI
                Animator animator = ragdoll.GetComponent<Animator>();
                
                // ОТТАЛКИВАЕМ ТОЛЬКО АКТИВНЫХ AI (не лежащих)
                if (animator != null && animator.enabled)
                {
                    if (showDebugLog)
                        Debug.Log($"[RepulsiveSphere] ✅ Отталкиваю АКТИВНОГО AI {ragdoll.name}");
                    
                    // ПРИНУДИТЕЛЬНО активируем ragdoll
                    vDamage knockdownDamage = new vDamage();
                    knockdownDamage.damageValue = 0; // Без урона
                    knockdownDamage.activeRagdoll = true; // Принудительная активация ragdoll
                    
                    // Активируем ragdoll
                    ragdoll.ActivateRagdoll(knockdownDamage);
                    
                    // Применяем силу через корутину
                    StartCoroutine(ApplyForceAfterRagdollActivation(ragdoll, distance));
                }
                else
                {
                    if (showDebugLog)
                        Debug.Log($"[RepulsiveSphere] ⏭️ Пропускаю лежащего AI {ragdoll.name}");
                }
            }
        }
    }
    
    /// <summary>
    /// Отталкивает AI с NavMeshAgent
    /// </summary>
    private void RepelNavMeshAI()
    {
        NavMeshAgent[] allAgents = FindObjectsOfType<NavMeshAgent>();
        
        foreach (var agent in allAgents)
        {
            if (agent == null || !agent.gameObject.activeInHierarchy)
                continue;
            
            // ИСКЛЮЧАЕМ ИГРОКА из отталкивания
            if (agent.CompareTag("Player"))
                continue;
            
            // Пропускаем AI с ragdoll (они обрабатываются отдельно)
            if (agent.GetComponent<vRagdoll>() != null)
                continue;
            
            float distance = Vector3.Distance(transform.position, agent.transform.position);
            
            if (distance <= repulsionRadius)
            {
                // Проверяем, есть ли у AI ragdoll компонент
                vRagdoll ragdoll = agent.GetComponent<vRagdoll>();
                if (ragdoll != null)
                {
                    // Если есть ragdoll, активируем его
                    Animator animator = agent.GetComponent<Animator>();
                    if (animator != null && animator.enabled)
                    {
                        if (showDebugLog)
                            Debug.Log($"[RepulsiveSphere] ✅ Активирую ragdoll у NavMesh AI {agent.name}");
                        
                        // ПРИНУДИТЕЛЬНО активируем ragdoll
                        vDamage knockdownDamage = new vDamage();
                        knockdownDamage.damageValue = 0; // Без урона
                        knockdownDamage.activeRagdoll = true; // Принудительная активация ragdoll
                        
                        ragdoll.ActivateRagdoll(knockdownDamage);
                        
                        // Применяем силу через корутину
                        StartCoroutine(ApplyForceAfterRagdollActivation(ragdoll, distance));
                    }
                    else
                    {
                        if (showDebugLog)
                            Debug.Log($"[RepulsiveSphere] ⏭️ Пропускаю лежащего NavMesh AI {agent.name}");
                    }
                }
                else
                {
                    // Если нет ragdoll, применяем обычную силу
                    if (showDebugLog)
                        Debug.Log($"[RepulsiveSphere] ✅ Отталкиваю NavMesh AI {agent.name}");
                    
                    ApplyForceToNavMeshAI(agent, distance);
                }
            }
        }
    }
    
    /// <summary>
    /// Применяет силу к ragdoll AI после активации ragdoll
    /// </summary>
    private System.Collections.IEnumerator ApplyForceAfterRagdollActivation(vRagdoll ragdoll, float distance)
    {
        // Ждем несколько кадров, чтобы ragdoll активировался
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        
        // Находим главную часть (Hips/Pelvis)
        Rigidbody mainBody = null;
        Rigidbody[] parts = ragdoll.GetComponentsInChildren<Rigidbody>();
        
        foreach (var part in parts)
        {
            if (part.name.ToLower().Contains("hips") || 
                part.name.ToLower().Contains("pelvis") || 
                part.name.ToLower().Contains("spine") ||
                part.name.ToLower().Contains("chest"))
            {
                mainBody = part;
                break;
            }
        }
        
        // Если не нашли главную часть, берем первую
        if (mainBody == null && parts.Length > 0)
        {
            mainBody = parts[0];
        }
        
        if (mainBody != null)
        {
            // Вычисляем силу отталкивания
            float force = repulsionForce * ragdollForceMultiplier;
            float distanceMultiplier = 1f - (distance / repulsionRadius);
            force *= distanceMultiplier;
            
            // Ограничиваем максимальную силу
            float safeForce = Mathf.Min(force, 150f); // Увеличили лимит
            
            // Направление от сферы к AI
            Vector3 direction = (ragdoll.transform.position - transform.position).normalized;
            
            // Добавляем немного вверх
            direction.y += 0.3f;
            direction.Normalize();
            
            // Применяем силу
            mainBody.AddForce(direction * safeForce, ForceMode.Impulse);
            
            if (showDebugLog)
                Debug.Log($"[RepulsiveSphere] 💥 Применена сила {safeForce:F1} к {ragdoll.name}");
        }
    }
    
    /// <summary>
    /// Применяет силу к NavMesh AI
    /// </summary>
    private void ApplyForceToNavMeshAI(NavMeshAgent agent, float distance)
    {
        // Вычисляем силу отталкивания
        float force = repulsionForce * navMeshForceMultiplier;
        float distanceMultiplier = 1f - (distance / repulsionRadius);
        force *= distanceMultiplier;
        
        // Ограничиваем максимальную силу
        float safeForce = Mathf.Min(force, 100f); // Увеличили лимит
        
        // Направление от сферы к AI
        Vector3 direction = (agent.transform.position - transform.position).normalized;
        
        // Добавляем немного вверх
        direction.y += 0.3f;
        direction.Normalize();
        
        // Применяем силу к Rigidbody
        Rigidbody rb = agent.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.AddForce(direction * safeForce, ForceMode.Impulse);
            
            if (showDebugLog)
                Debug.Log($"[RepulsiveSphere] 💥 Применена сила {safeForce:F1} к {agent.name}");
        }
        else
        {
            // Если нет Rigidbody, перемещаем NavMeshAgent
            if (agent.enabled)
            {
                Vector3 newPosition = agent.transform.position + direction * safeForce * 0.1f;
                NavMeshHit hit;
                if (NavMesh.SamplePosition(newPosition, out hit, 5f, NavMesh.AllAreas))
                {
                    agent.Warp(hit.position);
                    agent.ResetPath();
                }
                
                if (showDebugLog)
                    Debug.Log($"[RepulsiveSphere] 💥 Перемещен NavMeshAgent {agent.name}");
            }
        }
    }
    
    private void OnDrawGizmos()
    {
        // Рисуем радиус действия
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, repulsionRadius);
        
        // Рисуем центр
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(transform.position, 0.5f);
    }
}
