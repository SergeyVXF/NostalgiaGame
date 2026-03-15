using UnityEngine;

public class JumpBoostZone : MonoBehaviour
{
    [Header("Jump Boost Zone Settings")]
    [Tooltip("Сила подбрасывания вверх")]
    [Range(500f, 5000f)] public float jumpForce = 2000f;
    
    [Tooltip("Направление подбрасывания (оставьте 0,0,0 для вертикального подбрасывания)")]
    public Vector3 jumpDirection = Vector3.zero;
    
    [Tooltip("Эффект частиц при активации (необязательно)")]
    public ParticleSystem jumpEffect;
    
    [Tooltip("Звук при активации подбрасывания (необязательно)")]
    public AudioSource jumpSound;
    
    [Tooltip("Время перезарядки зоны в секундах")]
    [Range(1f, 10f)] public float cooldownTime = 3f;
    
    [Tooltip("Визуальный материал зоны (меняется при перезарядке)")]
    public Material activeMaterial;
    public Material cooldownMaterial;
    
    [Header("Advanced Settings")]
    [Tooltip("Минимальная скорость машины для активации подбрасывания")]
    [Range(0f, 20f)] public float minSpeedToActivate = 0f;
    
    [Tooltip("Добавить горизонтальную силу в направлении движения машины")]
    [Range(0f, 2000f)] public float horizontalBoost = 0f;
    
    // Приватные переменные
    private bool isOnCooldown = false;
    private float cooldownEndTime = 0f;
    private Renderer zoneRenderer;
    private Material originalMaterial;
    
    void Start()
    {
        // Получаем renderer для смены материала
        zoneRenderer = GetComponent<Renderer>();
        if (zoneRenderer != null)
        {
            originalMaterial = zoneRenderer.material;
        }
        
        // Устанавливаем триггер
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
        
        Debug.Log($"[JumpBoostZone] Зона подбрасывания настроена! Сила: {jumpForce}, перезарядка: {cooldownTime} сек");
    }
    
    // Визуализация в редакторе
    void OnDrawGizmos()
    {
        // Рисуем направление подбрасывания
        Vector3 jumpDir = jumpDirection;
        if (jumpDirection == Vector3.zero)
        {
            jumpDir = Vector3.up;
        }
        
        Gizmos.color = isOnCooldown ? Color.red : Color.green;
        Gizmos.DrawRay(transform.position, jumpDir * 2f);
        
        // Рисуем сферу в центре зоны
        Gizmos.color = isOnCooldown ? Color.red : Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 0.5f);
        
        // Рисуем границы зоны
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            Gizmos.color = isOnCooldown ? new Color(1f, 0f, 0f, 0.3f) : new Color(0f, 1f, 0f, 0.3f);
            Gizmos.matrix = transform.localToWorldMatrix;
            
            if (col is BoxCollider)
            {
                BoxCollider boxCol = col as BoxCollider;
                Gizmos.DrawCube(boxCol.center, boxCol.size);
            }
            else if (col is SphereCollider)
            {
                SphereCollider sphereCol = col as SphereCollider;
                Gizmos.DrawSphere(sphereCol.center, sphereCol.radius);
            }
        }
    }
    
    void Update()
    {
        // Проверяем окончание перезарядки
        if (isOnCooldown && Time.time >= cooldownEndTime)
        {
            isOnCooldown = false;
            
            // Возвращаем активный материал
            if (zoneRenderer != null && activeMaterial != null)
            {
                zoneRenderer.material = activeMaterial;
            }
            else if (zoneRenderer != null && originalMaterial != null)
            {
                zoneRenderer.material = originalMaterial;
            }
            
            Debug.Log("[JumpBoostZone] Зона перезарядилась!");
        }
    }
    
    void OnTriggerEnter(Collider other)
    {
        Debug.Log($"[JumpBoostZone] Детектирован объект: {other.name}, тег: {other.tag}");
        Debug.Log($"[JumpBoostZone] Позиция объекта: {other.transform.position}, позиция зоны: {transform.position}");
        
        // Ищем VehicleController - сначала на самом объекте, потом в родителях
        VehicleController vehicle = other.GetComponent<VehicleController>();
        if (vehicle == null)
        {
            vehicle = other.GetComponentInParent<VehicleController>();
        }
        
        Debug.Log($"[JumpBoostZone] VehicleController найден: {vehicle != null}, перезарядка: {isOnCooldown}");
        
        // Дополнительная диагностика
        if (vehicle != null)
        {
            Debug.Log($"[JumpBoostZone] Игрок в машине: {vehicle.isPlayerInVehicle}");
            Debug.Log($"[JumpBoostZone] Направление машины: {vehicle.transform.forward}");
            
            Rigidbody vehicleRb = vehicle.GetComponent<Rigidbody>();
            if (vehicleRb != null)
            {
                Debug.Log($"[JumpBoostZone] Скорость машины: {vehicleRb.linearVelocity}");
                Debug.Log($"[JumpBoostZone] Угловая скорость: {vehicleRb.angularVelocity}");
            }
        }
        
        if (vehicle != null && !isOnCooldown)
        {
            // Проверяем минимальную скорость
            Rigidbody vehicleRb = vehicle.GetComponent<Rigidbody>();
            if (vehicleRb != null)
            {
                float currentSpeed = vehicleRb.linearVelocity.magnitude;
                Debug.Log($"[JumpBoostZone] Текущая скорость машины: {currentSpeed:F1}, минимальная: {minSpeedToActivate}");
                
                if (currentSpeed < minSpeedToActivate)
                {
                    Debug.Log($"[JumpBoostZone] Скорость недостаточна: {currentSpeed:F1} < {minSpeedToActivate}");
                    return;
                }
            }
            
            // Проверяем, что игрок в машине
            if (!vehicle.isPlayerInVehicle)
            {
                Debug.Log("[JumpBoostZone] Игрок не в машине, подбрасывание не активировано");
                return;
            }
            
            // Вычисляем направление подбрасывания
            Vector3 finalJumpDirection = jumpDirection;
            if (jumpDirection == Vector3.zero)
            {
                finalJumpDirection = Vector3.up;
            }
            
            // Добавляем горизонтальную составляющую в направлении движения машины
            if (horizontalBoost != 0f && vehicleRb != null)
            {
                Vector3 horizontalDirection = vehicleRb.linearVelocity.normalized;
                horizontalDirection.y = 0f; // Убираем вертикальную составляющую
                
                // Проверяем, что у нас есть горизонтальное движение
                if (horizontalDirection.magnitude > 0.1f)
                {
                    finalJumpDirection += horizontalDirection * (horizontalBoost / jumpForce);
                    Debug.Log($"[JumpBoostZone] Добавлена горизонтальная составляющая: {horizontalDirection * (horizontalBoost / jumpForce)}");
                }
                else
                {
                    // Если нет горизонтального движения, используем направление машины
                    Vector3 vehicleForward = vehicle.transform.forward;
                    vehicleForward.y = 0f;
                    if (vehicleForward.magnitude > 0.1f)
                    {
                        finalJumpDirection += vehicleForward.normalized * (horizontalBoost / jumpForce);
                        Debug.Log($"[JumpBoostZone] Используем направление машины для горизонтальной составляющей: {vehicleForward.normalized * (horizontalBoost / jumpForce)}");
                    }
                    else
                    {
                        Debug.Log("[JumpBoostZone] Нет направления движения, используем только вертикальное подбрасывание");
                    }
                }
            }
            
            // Дополнительная диагностика направления
            Debug.Log($"[JumpBoostZone] Финальное направление подбрасывания: {finalJumpDirection}");
            Debug.Log($"[JumpBoostZone] Угол между направлением машины и зоной: {Vector3.Angle(vehicle.transform.forward, transform.forward)}°");
            Debug.Log($"[JumpBoostZone] Расстояние от центра зоны: {Vector3.Distance(vehicle.transform.position, transform.position)}");
            
            // Активируем подбрасывание
            vehicle.ActivateJumpBoost(jumpForce, finalJumpDirection);
            
            // Запускаем перезарядку
            StartCooldown();
            
            // Воспроизводим эффекты
            PlayEffects();
            
            Debug.Log($"[JumpBoostZone] 🚀 ПОДБРАСЫВАНИЕ АКТИВИРОВАНО для {other.name}! Сила: {jumpForce}, направление: {finalJumpDirection}");
        }
        else if (vehicle == null)
        {
            Debug.LogWarning($"[JumpBoostZone] VehicleController НЕ НАЙДЕН на объекте {other.name}!");
        }
        else if (isOnCooldown)
        {
            Debug.Log("[JumpBoostZone] Зона на перезарядке, подбрасывание не активировано");
        }
    }
    
    private void StartCooldown()
    {
        isOnCooldown = true;
        cooldownEndTime = Time.time + cooldownTime;
        
        // Меняем материал на перезарядку
        if (zoneRenderer != null && cooldownMaterial != null)
        {
            zoneRenderer.material = cooldownMaterial;
        }
    }
    
    private void PlayEffects()
    {
        // Запускаем частицы
        if (jumpEffect != null)
        {
            jumpEffect.Play();
        }
        
        // Воспроизводим звук
        if (jumpSound != null)
        {
            jumpSound.Play();
        }
    }
    
    // Публичный метод для принудительной перезарядки (используется в редакторе)
    public void ForceRecharge()
    {
        isOnCooldown = false;
        cooldownEndTime = 0f;
        
        // Возвращаем активный материал
        if (zoneRenderer != null && activeMaterial != null)
        {
            zoneRenderer.material = activeMaterial;
        }
        else if (zoneRenderer != null && originalMaterial != null)
        {
            zoneRenderer.material = originalMaterial;
        }
        
        Debug.Log("[JumpBoostZone] Принудительная перезарядка выполнена!");
    }
} 