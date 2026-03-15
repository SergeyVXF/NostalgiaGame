using UnityEngine;

public class JumpBoostZoneDebugger : MonoBehaviour
{
    [Header("Диагностика JumpBoost зоны")]
    public JumpBoostZone targetZone;
    public bool showDebugInfo = true;
    public bool testZoneActivation = false;
    
    [Header("Тестовые параметры")]
    public float testJumpForce = 2000f;
    public Vector3 testJumpDirection = Vector3.up;
    
    void Start()
    {
        if (targetZone == null)
        {
            targetZone = FindObjectOfType<JumpBoostZone>();
        }
        
        if (targetZone != null)
        {
            Debug.Log($"[JumpBoostZoneDebugger] Найдена зона: {targetZone.name}");
            Debug.Log($"[JumpBoostZoneDebugger] Настройки зоны:");
            Debug.Log($"  - Сила подбрасывания: {targetZone.jumpForce}");
            Debug.Log($"  - Направление: {targetZone.jumpDirection}");
            Debug.Log($"  - Горизонтальный буст: {targetZone.horizontalBoost}");
            Debug.Log($"  - Минимальная скорость: {targetZone.minSpeedToActivate}");
            Debug.Log($"  - Перезарядка: {targetZone.cooldownTime} сек");
        }
        else
        {
            Debug.LogWarning("[JumpBoostZoneDebugger] JumpBoost зона не найдена на сцене!");
        }
    }
    
    void Update()
    {
        if (!showDebugInfo || targetZone == null) return;
        
        // Показываем информацию о зоне каждые 2 секунды
        if (Time.time % 2f < Time.deltaTime)
        {
            Debug.Log($"[JumpBoostZoneDebugger] Статус зоны {targetZone.name}:");
            Debug.Log($"  - Позиция: {targetZone.transform.position}");
            Debug.Log($"  - Поворот: {targetZone.transform.rotation.eulerAngles}");
            Debug.Log($"  - Размер коллайдера: {GetColliderSize(targetZone)}");
            
            // Проверяем коллайдер
            Collider col = targetZone.GetComponent<Collider>();
            if (col != null)
            {
                Debug.Log($"  - Коллайдер активен: {col.enabled}");
                Debug.Log($"  - Коллайдер триггер: {col.isTrigger}");
            }
        }
        
        // Тестовая активация зоны
        if (testZoneActivation && Input.GetKeyDown(KeyCode.T))
        {
            TestZoneActivation();
        }
    }
    
    private string GetColliderSize(JumpBoostZone zone)
    {
        Collider col = zone.GetComponent<Collider>();
        if (col == null) return "Нет коллайдера";
        
        if (col is BoxCollider)
        {
            BoxCollider boxCol = col as BoxCollider;
            return $"Box: {boxCol.size}";
        }
        else if (col is SphereCollider)
        {
            SphereCollider sphereCol = col as SphereCollider;
            return $"Sphere: r={sphereCol.radius}";
        }
        else
        {
            return col.GetType().Name;
        }
    }
    
    private void TestZoneActivation()
    {
        if (targetZone == null) return;
        
        Debug.Log("[JumpBoostZoneDebugger] Тестируем активацию зоны...");
        
        // Создаем тестовый объект
        GameObject testObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        testObject.name = "TestVehicle";
        testObject.transform.position = targetZone.transform.position + Vector3.up * 2f;
        
        // Добавляем VehicleController
        VehicleController testVehicle = testObject.AddComponent<VehicleController>();
        testVehicle.isPlayerInVehicle = true; // Симулируем, что игрок в машине
        
        // Добавляем Rigidbody
        Rigidbody testRb = testObject.GetComponent<Rigidbody>();
        if (testRb == null)
        {
            testRb = testObject.AddComponent<Rigidbody>();
        }
        testRb.linearVelocity = Vector3.forward * 5f; // Задаем скорость
        
        Debug.Log("[JumpBoostZoneDebugger] Тестовый объект создан, должен активировать зону");
        
        // Удаляем тестовый объект через 3 секунды
        Destroy(testObject, 3f);
    }
    
    void OnDrawGizmos()
    {
        if (!showDebugInfo || targetZone == null) return;
        
        // Рисуем линию к целевой зоне
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, targetZone.transform.position);
        
        // Рисуем сферу вокруг зоны
        Gizmos.color = new Color(0f, 1f, 1f, 0.3f);
        Gizmos.DrawWireSphere(targetZone.transform.position, 1f);
    }
} 