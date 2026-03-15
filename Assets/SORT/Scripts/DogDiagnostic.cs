using UnityEngine;
using UnityEngine.AI;

public class DogDiagnostic : MonoBehaviour
{
    [Header("Диагностика собаки")]
    [Tooltip("Запустить полную диагностику")]
    public bool runDiagnostic = false;
    
    [Tooltip("Показывать диагностику каждую секунду")]
    public bool continuousDiagnostic = true;
    
    [Header("Найденные компоненты")]
    public DogPatrol dogPatrol;
    public NavMeshAgent navAgent;
    public Animator animator;
    
    private float lastDiagnosticTime = 0f;
    
    private void Start()
    {
        AutoFindComponents();
        Invoke("RunFullDiagnostic", 1f);
    }
    
    private void Update()
    {
        if (runDiagnostic)
        {
            runDiagnostic = false;
            RunFullDiagnostic();
        }
        
        if (continuousDiagnostic && Time.time - lastDiagnosticTime > 1f)
        {
            RunContinuousDiagnostic();
            lastDiagnosticTime = Time.time;
        }
    }
    
    private void AutoFindComponents()
    {
        // Ищем компоненты на этом объекте
        dogPatrol = GetComponent<DogPatrol>();
        navAgent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        
        Debug.Log($"=== AUTO FIND COMPONENTS НА {gameObject.name} ===");
        Debug.Log($"DogPatrol: {(dogPatrol ? "✅ НАЙДЕН" : "❌ НЕ НАЙДЕН")}");
        Debug.Log($"NavMeshAgent: {(navAgent ? "✅ НАЙДЕН" : "❌ НЕ НАЙДЕН")}");
        Debug.Log($"Animator: {(animator ? "✅ НАЙДЕН" : "❌ НЕ НАЙДЕН")}");
    }
    
    private void RunFullDiagnostic()
    {
        Debug.Log($"========== ПОЛНАЯ ДИАГНОСТИКА СОБАКИ {gameObject.name} ==========");
        
        // Базовая информация
        Debug.Log($"Позиция: {transform.position}");
        Debug.Log($"Поворот: {transform.eulerAngles}");
        Debug.Log($"Активен: {gameObject.activeInHierarchy}");
        Debug.Log($"Слой: {gameObject.layer} ({LayerMask.LayerToName(gameObject.layer)})");
        
        // Диагностика DogPatrol
        if (dogPatrol)
        {
            Debug.Log($"--- DOGPATROL ---");
            Debug.Log($"Patrol Points: {(dogPatrol.patrolPoints?.Count ?? 0)} точек");
            Debug.Log($"Patrol Speed: {dogPatrol.patrolSpeed}");
            Debug.Log($"Chase Speed: {dogPatrol.chaseSpeed}");
            // Current Point недоступен (приватный), пропускаем
            Debug.Log($"Current Point: [приватное поле, недоступно]");
            
            if (dogPatrol.patrolPoints != null)
            {
                for (int i = 0; i < dogPatrol.patrolPoints.Count; i++)
                {
                    var point = dogPatrol.patrolPoints[i];
                    if (point)
                    {
                        float dist = Vector3.Distance(transform.position, point.position);
                        Debug.Log($"  Точка {i}: {point.name} на расстоянии {dist:F2}м");
                    }
                    else
                    {
                        Debug.Log($"  Точка {i}: ❌ NULL");
                    }
                }
            }
        }
        
        // Диагностика NavMeshAgent
        if (navAgent)
        {
            Debug.Log($"--- NAVMESHAGENT ---");
            Debug.Log($"Enabled: {navAgent.enabled}");
            Debug.Log($"IsOnNavMesh: {navAgent.isOnNavMesh}");
            Debug.Log($"HasPath: {navAgent.hasPath}");
            Debug.Log($"PathPending: {navAgent.pathPending}");
            Debug.Log($"IsStopped: {navAgent.isStopped}");
            Debug.Log($"Speed: {navAgent.speed}");
            Debug.Log($"Velocity: {navAgent.velocity} (magnitude: {navAgent.velocity.magnitude:F2})");
            Debug.Log($"Remaining Distance: {navAgent.remainingDistance:F2}");
            Debug.Log($"Stopping Distance: {navAgent.stoppingDistance}");
            
            if (navAgent.hasPath)
            {
                Debug.Log($"Path Status: {navAgent.pathStatus}");
                Debug.Log($"Destination: {navAgent.destination}");
                float distToDest = Vector3.Distance(transform.position, navAgent.destination);
                Debug.Log($"Distance to Destination: {distToDest:F2}м");
            }
            else
            {
                Debug.Log($"❌ НЕТ ПУТИ!");
            }
        }
        
        // Диагностика NavMesh
        NavMeshHit hit;
        if (NavMesh.SamplePosition(transform.position, out hit, 5f, NavMesh.AllAreas))
        {
            Debug.Log($"--- NAVMESH ---");
            Debug.Log($"✅ NavMesh найден на расстоянии {hit.distance:F2}м");
            Debug.Log($"NavMesh позиция: {hit.position}");
        }
        else
        {
            Debug.Log($"❌ NAVMESH НЕ НАЙДЕН в радиусе 5м!");
        }
        
        Debug.Log($"========== КОНЕЦ ДИАГНОСТИКИ ==========");
    }
    
    private void RunContinuousDiagnostic()
    {
        if (!navAgent) return;
        
        string status = "";
        if (!navAgent.enabled) status = "❌ ОТКЛЮЧЕН";
        else if (!navAgent.isOnNavMesh) status = "❌ НЕ НА NAVMESH";
        else if (navAgent.isStopped) status = "🛑 ОСТАНОВЛЕН";
        else if (!navAgent.hasPath) status = "❌ НЕТ ПУТИ";
        else if (navAgent.pathPending) status = "⏳ ПУТЬ ЗАГРУЖАЕТСЯ";
        else if (navAgent.velocity.magnitude < 0.1f) status = "🐌 СТОИТ НА МЕСТЕ";
        else status = $"✅ ДВИЖЕТСЯ ({navAgent.velocity.magnitude:F1})";
        
        Debug.Log($"[DOG STATUS] {status} | Pos: {transform.position} | Dest: {navAgent.destination}");
    }
    
    [ContextMenu("Принудительно запустить диагностику")]
    public void ForceDiagnostic()
    {
        RunFullDiagnostic();
    }
    
    [ContextMenu("Телепортировать на NavMesh")]
    public void TeleportToNavMesh()
    {
        NavMeshHit hit;
        if (NavMesh.SamplePosition(transform.position, out hit, 10f, NavMesh.AllAreas))
        {
            transform.position = hit.position;
            Debug.Log($"✅ Телепортирован на NavMesh: {hit.position}");
        }
        else
        {
            Debug.Log($"❌ NavMesh не найден для телепортации!");
        }
    }
    
    [ContextMenu("Перезапустить NavMeshAgent")]
    public void RestartNavMeshAgent()
    {
        if (navAgent)
        {
            navAgent.enabled = false;
            navAgent.enabled = true;
            Debug.Log($"✅ NavMeshAgent перезапущен");
        }
    }
}
