using UnityEngine;
using UnityEngine.AI;

public class FindDogAndDiagnose : MonoBehaviour
{
    private void Start()
    {
        // Ждем 2 секунды для загрузки сцены, затем ищем собаку
        Invoke("FindAndDiagnoseDog", 2f);
    }
    
    private void FindAndDiagnoseDog()
    {
        Debug.Log("========== ПОИСК СОБАКИ В СЦЕНЕ ==========");
        
        // Ищем все объекты с DogPatrol
        DogPatrol[] dogPatrols = FindObjectsOfType<DogPatrol>();
        Debug.Log($"Найдено объектов с DogPatrol: {dogPatrols.Length}");
        
        foreach (var dog in dogPatrols)
        {
            Debug.Log($"НАЙДЕНА СОБАКА: {dog.gameObject.name}");
            DiagnoseDog(dog.gameObject);
            
            // Добавляем диагностический компонент если его нет
            if (!dog.GetComponent<DogDiagnostic>())
            {
                dog.gameObject.AddComponent<DogDiagnostic>();
                Debug.Log($"✅ Добавлен DogDiagnostic к {dog.gameObject.name}");
            }
        }
        
        // Ищем все объекты с NavMeshAgent
        NavMeshAgent[] agents = FindObjectsOfType<NavMeshAgent>();
        Debug.Log($"Найдено объектов с NavMeshAgent: {agents.Length}");
        
        foreach (var agent in agents)
        {
            if (agent.GetComponent<DogPatrol>())
            {
                Debug.Log($"NavMeshAgent на собаке: {agent.gameObject.name}");
            }
            else
            {
                Debug.Log($"NavMeshAgent на другом объекте: {agent.gameObject.name}");
            }
        }
        
        // Ищем все объекты с "Dog" в имени
        GameObject[] allObjects = FindObjectsOfType<GameObject>();
        foreach (var obj in allObjects)
        {
            if (obj.name.ToLower().Contains("dog"))
            {
                Debug.Log($"Объект с 'dog' в имени: {obj.name}");
                DiagnoseDog(obj);
            }
        }
        
        Debug.Log("========== ПОИСК ЗАВЕРШЕН ==========");
    }
    
    private void DiagnoseDog(GameObject dogObject)
    {
        Debug.Log($"--- ДИАГНОСТИКА {dogObject.name} ---");
        Debug.Log($"Позиция: {dogObject.transform.position}");
        Debug.Log($"Активен: {dogObject.activeInHierarchy}");
        
        // Проверяем компоненты
        DogPatrol dogPatrol = dogObject.GetComponent<DogPatrol>();
        NavMeshAgent navAgent = dogObject.GetComponent<NavMeshAgent>();
        Animator animator = dogObject.GetComponent<Animator>();
        
        Debug.Log($"DogPatrol: {(dogPatrol ? "✅" : "❌")}");
        Debug.Log($"NavMeshAgent: {(navAgent ? "✅" : "❌")}");
        Debug.Log($"Animator: {(animator ? "✅" : "❌")}");
        
        if (navAgent)
        {
            Debug.Log($"NavMesh статус: enabled={navAgent.enabled}, onNavMesh={navAgent.isOnNavMesh}");
            Debug.Log($"Скорость: {navAgent.velocity.magnitude:F2} (установлена: {navAgent.speed})");
            
            if (!navAgent.isOnNavMesh)
            {
                Debug.Log("❌ СОБАКА НЕ НА NAVMESH! Попытка исправить...");
                FixDogPosition(dogObject);
            }
        }
        
        if (dogPatrol && dogPatrol.patrolPoints != null)
        {
            Debug.Log($"Точки патрулирования: {dogPatrol.patrolPoints.Count}");
            for (int i = 0; i < dogPatrol.patrolPoints.Count; i++)
            {
                if (dogPatrol.patrolPoints[i])
                {
                    float dist = Vector3.Distance(dogObject.transform.position, dogPatrol.patrolPoints[i].position);
                    Debug.Log($"  Точка {i}: {dogPatrol.patrolPoints[i].name} ({dist:F1}м)");
                }
            }
        }
    }
    
    private void FixDogPosition(GameObject dogObject)
    {
        NavMeshHit hit;
        if (NavMesh.SamplePosition(dogObject.transform.position, out hit, 10f, NavMesh.AllAreas))
        {
            dogObject.transform.position = hit.position;
            Debug.Log($"✅ Собака перемещена на NavMesh: {hit.position}");
            
            // Перезапускаем NavMeshAgent
            NavMeshAgent agent = dogObject.GetComponent<NavMeshAgent>();
            if (agent)
            {
                agent.enabled = false;
                agent.enabled = true;
                Debug.Log("✅ NavMeshAgent перезапущен");
            }
        }
        else
        {
            Debug.Log("❌ Не удалось найти NavMesh рядом с собакой!");
        }
    }
    
    [ContextMenu("Найти собаку сейчас")]
    public void FindDogNow()
    {
        FindAndDiagnoseDog();
    }
}

