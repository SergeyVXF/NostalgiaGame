using UnityEngine;

public class SpeedBoostZone : MonoBehaviour
{
    [Header("Boost Zone Settings")]
    [Tooltip("Эффект частиц при активации (необязательно)")]
    public ParticleSystem boostEffect;
    
    [Tooltip("Звук при активации буста (необязательно)")]
    public AudioSource boostSound;
    
    [Tooltip("Время перезарядки зоны в секундах")]
    [Range(1f, 10f)] public float cooldownTime = 3f;
    
    [Tooltip("Визуальный материал зоны (меняется при перезарядке)")]
    public Material activeMaterial;
    public Material cooldownMaterial;
    
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
        
        Debug.Log($"[SpeedBoostZone] Зона ускорения настроена! Перезарядка: {cooldownTime} сек");
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
            
            Debug.Log("[SpeedBoostZone] Зона перезарядилась!");
        }
    }
    
    void OnTriggerEnter(Collider other)
    {
        Debug.Log($"[SpeedBoostZone] Детектирован объект: {other.name}, тег: {other.tag}");
        
        // Ищем VehicleController - сначала на самом объекте, потом в родителях
        VehicleController vehicle = other.GetComponent<VehicleController>();
        if (vehicle == null)
        {
            vehicle = other.GetComponentInParent<VehicleController>();
        }
        
        Debug.Log($"[SpeedBoostZone] VehicleController найден: {vehicle != null}, перезарядка: {isOnCooldown}");
        
        if (vehicle != null && !isOnCooldown)
        {
            // Активируем буст
            vehicle.ActivateBoost();
            
            // Запускаем перезарядку
            StartCooldown();
            
            // Воспроизводим эффекты
            PlayEffects();
            
            Debug.Log($"[SpeedBoostZone] БУСТ АКТИВИРОВАН для {other.name}!");
        }
        else if (vehicle == null)
        {
            Debug.LogWarning($"[SpeedBoostZone] VehicleController НЕ НАЙДЕН на объекте {other.name}!");
        }
        else if (isOnCooldown)
        {
            Debug.Log("[SpeedBoostZone] Зона на перезарядке, буст не активирован");
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
        if (boostEffect != null)
        {
            boostEffect.Play();
        }
        
        // Воспроизводим звук
        if (boostSound != null)
        {
            boostSound.Play();
        }
    }
    
    // Метод для принудительной перезарядки (можно вызвать извне)
    public void ForceRecharge()
    {
        isOnCooldown = false;
        if (zoneRenderer != null && activeMaterial != null)
        {
            zoneRenderer.material = activeMaterial;
        }
        Debug.Log("[SpeedBoostZone] Принудительная перезарядка!");
    }
    
    // Визуализация зоны в редакторе
    void OnDrawGizmos()
    {
        Gizmos.color = isOnCooldown ? Color.red : Color.cyan;
        Gizmos.DrawWireCube(transform.position, transform.localScale);
        
        // Показываем направление (если есть forward)
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(transform.position, transform.forward * 2f);
    }
} 