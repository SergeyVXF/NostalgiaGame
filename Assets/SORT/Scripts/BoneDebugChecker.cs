using UnityEngine;

public class BoneDebugChecker : MonoBehaviour
{
    [Header("Диагностика косточки")]
    [Tooltip("Автоматически найти все компоненты в сцене")]
    public bool autoFindComponents = true;
    
    [Header("Компоненты для проверки")]
    public GameObject boneObject;
    public DogPatrol dogPatrol;
    public DogZoneTrigger dogZoneTrigger;
    
    [Header("Управление")]
    [Tooltip("Запустить полную диагностику")]
    public bool runFullDiagnostic = false;
    
    private void Start()
    {
        if (autoFindComponents)
        {
            AutoFindComponents();
        }
        
        // Запускаем диагностику через 2 секунды после старта
        Invoke("RunDiagnostic", 2f);
    }
    
    private void Update()
    {
        if (runFullDiagnostic)
        {
            runFullDiagnostic = false;
            RunDiagnostic();
        }
    }
    
    private void AutoFindComponents()
    {
        // Ищем косточку
        if (boneObject == null)
        {
            GameObject[] bones = GameObject.FindGameObjectsWithTag("Bone");
            if (bones.Length > 0)
            {
                boneObject = bones[0];
                Debug.Log($"[BoneDebugChecker] Найдена косточка: {boneObject.name}");
            }
        }
        
        // Ищем DogPatrol
        if (dogPatrol == null)
        {
            dogPatrol = FindObjectOfType<DogPatrol>();
            if (dogPatrol != null)
                Debug.Log($"[BoneDebugChecker] Найден DogPatrol: {dogPatrol.name}");
        }
        
        // Ищем DogZoneTrigger
        if (dogZoneTrigger == null)
        {
            dogZoneTrigger = FindObjectOfType<DogZoneTrigger>();
            if (dogZoneTrigger != null)
                Debug.Log($"[BoneDebugChecker] Найден DogZoneTrigger: {dogZoneTrigger.name}");
        }
    }
    
    [ContextMenu("Запустить диагностику")]
    public void RunDiagnostic()
    {
        Debug.Log("=== 🦴 ДИАГНОСТИКА СИСТЕМЫ КОСТОЧКИ ===");
        
        CheckBoneObject();
        CheckDogPatrol();
        CheckDogZoneTrigger();
        CheckTags();
        CheckDistances();
        
        Debug.Log("=== 🔍 ДИАГНОСТИКА ЗАВЕРШЕНА ===");
    }
    
    private void CheckBoneObject()
    {
        Debug.Log("--- Проверка косточки ---");
        
        if (boneObject == null)
        {
            Debug.LogError("❌ Косточка не найдена! Создайте GameObject с тегом 'Bone'");
            return;
        }
        
        Debug.Log($"✅ Косточка найдена: {boneObject.name}");
        Debug.Log($"   Позиция: {boneObject.transform.position}");
        Debug.Log($"   Тег: {boneObject.tag}");
        
        // Проверяем BoneBehavior
        BoneBehavior boneBehavior = boneObject.GetComponent<BoneBehavior>();
        if (boneBehavior == null)
        {
            Debug.LogError("❌ BoneBehavior скрипт не найден на косточке!");
        }
        else
        {
            Debug.Log("✅ BoneBehavior найден");
            Debug.Log($"   Eating Time: {boneBehavior.eatingTime}");
            Debug.Log($"   Eating Distance: {boneBehavior.eatingDistance}");
            Debug.Log($"   Is Being Eaten: {boneBehavior.IsBeingEaten()}");
        }
        
        // Проверяем коллайдер
        Collider boneCollider = boneObject.GetComponent<Collider>();
        if (boneCollider == null)
        {
            Debug.LogError("❌ Коллайдер не найден на косточке!");
        }
        else
        {
            Debug.Log($"✅ Коллайдер найден: {boneCollider.GetType().Name}");
            Debug.Log($"   Is Trigger: {boneCollider.isTrigger}");
            if (!boneCollider.isTrigger)
            {
                Debug.LogError("❌ ПРОБЛЕМА: Is Trigger должен быть включен!");
            }
        }
    }
    
    private void CheckDogPatrol()
    {
        Debug.Log("--- Проверка DogPatrol ---");
        
        if (dogPatrol == null)
        {
            Debug.LogError("❌ DogPatrol не найден!");
            return;
        }
        
        Debug.Log($"✅ DogPatrol найден: {dogPatrol.name}");
        Debug.Log($"   Позиция: {dogPatrol.transform.position}");
        Debug.Log($"   Chase Speed: {dogPatrol.chaseSpeed}");
        Debug.Log($"   Is Chasing Bone: {dogPatrol.IsChasingBone()}");
        
        // Проверяем NavMeshAgent
        UnityEngine.AI.NavMeshAgent agent = dogPatrol.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent == null)
        {
            Debug.LogError("❌ NavMeshAgent не найден!");
        }
        else
        {
            Debug.Log($"✅ NavMeshAgent найден");
            Debug.Log($"   Speed: {agent.speed}");
            Debug.Log($"   Is On NavMesh: {agent.isOnNavMesh}");
        }
    }
    
    private void CheckDogZoneTrigger()
    {
        Debug.Log("--- Проверка DogZoneTrigger ---");
        
        if (dogZoneTrigger == null)
        {
            Debug.LogError("❌ DogZoneTrigger не найден!");
            return;
        }
        
        Debug.Log($"✅ DogZoneTrigger найден: {dogZoneTrigger.name}");
        Debug.Log($"   Bone Tag: '{dogZoneTrigger.boneTag}'");
        Debug.Log($"   Dog Patrol: {dogZoneTrigger.dogPatrol?.name}");
        
        if (dogZoneTrigger.dogPatrol == null)
        {
            Debug.LogError("❌ ПРОБЛЕМА: DogPatrol не назначен в DogZoneTrigger!");
        }
        
        // Проверяем коллайдер зоны
        Collider zoneCollider = dogZoneTrigger.GetComponent<Collider>();
        if (zoneCollider == null)
        {
            Debug.LogError("❌ Коллайдер зоны не найден!");
        }
        else
        {
            Debug.Log($"✅ Коллайдер зоны найден: {zoneCollider.GetType().Name}");
            Debug.Log($"   Is Trigger: {zoneCollider.isTrigger}");
            if (!zoneCollider.isTrigger)
            {
                Debug.LogError("❌ ПРОБЛЕМА: Is Trigger зоны должен быть включен!");
            }
        }
    }
    
    private void CheckTags()
    {
        Debug.Log("--- Проверка тегов ---");
        
        // Проверяем существование тега Bone
        try
        {
            GameObject.FindGameObjectsWithTag("Bone");
            Debug.Log("✅ Тег 'Bone' существует");
        }
        catch
        {
            Debug.LogError("❌ Тег 'Bone' не создан! Создайте его в Tags & Layers");
        }
        
        // Проверяем количество объектов с тегом Bone
        GameObject[] bones = GameObject.FindGameObjectsWithTag("Bone");
        Debug.Log($"   Объектов с тегом 'Bone': {bones.Length}");
        
        if (bones.Length == 0)
        {
            Debug.LogError("❌ Нет объектов с тегом 'Bone'!");
        }
    }
    
    private void CheckDistances()
    {
        Debug.Log("--- Проверка расстояний ---");
        
        if (boneObject == null || dogPatrol == null || dogZoneTrigger == null)
        {
            Debug.LogWarning("⚠️ Не все компоненты найдены для проверки расстояний");
            return;
        }
        
        float distanceBoneToDog = Vector3.Distance(boneObject.transform.position, dogPatrol.transform.position);
        float distanceBoneToZone = Vector3.Distance(boneObject.transform.position, dogZoneTrigger.transform.position);
        
        Debug.Log($"   Расстояние косточка → собака: {distanceBoneToDog:F2}");
        Debug.Log($"   Расстояние косточка → зона: {distanceBoneToZone:F2}");
        
        // Проверяем радиус зоны
        Collider zoneCollider = dogZoneTrigger.GetComponent<Collider>();
        if (zoneCollider is SphereCollider sphere)
        {
            float zoneRadius = sphere.radius * dogZoneTrigger.transform.localScale.x;
            Debug.Log($"   Радиус зоны: {zoneRadius:F2}");
            
            if (distanceBoneToZone <= zoneRadius)
            {
                Debug.Log("✅ Косточка ВНУТРИ зоны обнаружения");
            }
            else
            {
                Debug.LogWarning($"⚠️ Косточка СНАРУЖИ зоны! Нужно переместить ближе или увеличить радиус зоны");
            }
        }
    }
    
    [ContextMenu("Принудительно активировать косточку")]
    public void ForceActivateBone()
    {
        if (boneObject != null && dogZoneTrigger != null)
        {
            Debug.Log("🔧 Принудительная активация косточки...");
            
            BoneBehavior bone = boneObject.GetComponent<BoneBehavior>();
            if (bone != null)
            {
                dogZoneTrigger.dogPatrol.SetChasingBone(bone);
                Debug.Log("✅ Косточка принудительно активирована!");
            }
        }
    }
}

