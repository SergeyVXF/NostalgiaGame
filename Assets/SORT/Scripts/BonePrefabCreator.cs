using UnityEngine;

public class BonePrefabCreator : MonoBehaviour
{
    [Header("Создание префаба косточки")]
    [Tooltip("Создать тестовую косточку")]
    public bool createTestBone = false;
    
    private void Update()
    {
        if (createTestBone)
        {
            createTestBone = false;
            CreateTestBone();
        }
    }
    
    [ContextMenu("Создать тестовую косточку")]
    public void CreateTestBone()
    {
        Debug.Log("[BonePrefabCreator] Создаю тестовую косточку...");
        
        // Создаем GameObject косточки
        GameObject bone = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        bone.name = "ThrowableBone";
        bone.tag = "Bone";
        
        // Настраиваем позицию
        Transform player = GameObject.FindGameObjectWithTag("Player")?.transform;
        if (player != null)
        {
            bone.transform.position = player.position + player.forward * 3f + Vector3.up * 1f;
        }
        else
        {
            bone.transform.position = Vector3.up * 2f;
        }
        
        // Добавляем компоненты
        Rigidbody rb = bone.AddComponent<Rigidbody>();
        bone.AddComponent<ThrowableBone>();
        
        // Настраиваем физику для предотвращения проваливания
        rb.mass = 0.5f;
        rb.linearDamping = 1f;
        rb.angularDamping = 5f;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        
        // Увеличиваем основной коллайдер Capsule в 4 раза для предотвращения проваливания
        CapsuleCollider mainCollider = bone.GetComponent<CapsuleCollider>();
        if (mainCollider != null)
        {
            mainCollider.radius *= 4f; // Увеличиваем радиус в 4 раза
            mainCollider.height *= 4f; // Увеличиваем высоту в 4 раза
        }
        
        // Добавляем дополнительный триггер для подбора
        SphereCollider pickupTrigger = bone.AddComponent<SphereCollider>();
        pickupTrigger.isTrigger = true;
        pickupTrigger.radius = 2f; // Радиус подбора при касании
        
        Debug.Log($"[BonePrefabCreator] ✅ Тестовая косточка создана: {bone.name} на позиции {bone.transform.position}");
    }
    
    [ContextMenu("Дать игроку косточку")]
    public void GivePlayerBone()
    {
        if (InventorySystem.Instance != null)
        {
            InventorySystem.Instance.AddBone();
            Debug.Log("[BonePrefabCreator] ✅ Косточка добавлена в инвентарь игрока");
        }
        else
        {
            Debug.LogError("[BonePrefabCreator] ❌ InventorySystem не найден!");
        }
    }
}
