using UnityEngine;

public class RopeSlideParticleSpawner : MonoBehaviour
{
    [Header("Particle System Settings")]
    [Tooltip("Particle System для спавна во время движения по веревке")]
    public ParticleSystem ropeSlideParticles;
    
    [Tooltip("Смещение Particle System по Y относительно игрока")]
    public float yOffset = 0f;
    
    [Tooltip("Автоматически создавать Particle System если не указан")]
    public bool autoCreateParticleSystem = true;
    
    private ParticleSystem spawnedParticleSystem;
    private bool isRopeSliding = false;
    
    void Start()
    {
        // Если Particle System не указан и включено автосоздание
        if (ropeSlideParticles == null && autoCreateParticleSystem)
        {
            CreateDefaultParticleSystem();
        }
    }
    
    void Update()
    {
        if (isRopeSliding && spawnedParticleSystem != null)
        {
            // Обновляем позицию под игроком с настраиваемым смещением по Y
            Vector3 targetPosition = transform.position + new Vector3(0f, yOffset, 0f);
            spawnedParticleSystem.transform.position = targetPosition;
            
            // Поворачиваем Particle System за игроком
            spawnedParticleSystem.transform.rotation = transform.rotation;
        }
    }
    
    private void CreateDefaultParticleSystem()
    {
        // Создаем GameObject для Particle System
        GameObject particleObject = new GameObject("RopeSlideParticles");
        particleObject.transform.SetParent(transform);
        particleObject.transform.localPosition = Vector3.zero;
        
        // Добавляем Particle System
        spawnedParticleSystem = particleObject.AddComponent<ParticleSystem>();
        
        // Настраиваем базовые параметры
        var main = spawnedParticleSystem.main;
        main.startLifetime = 2f;
        main.startSpeed = 1f;
        main.startSize = 0.2f;
        main.maxParticles = 50;
        
        var emission = spawnedParticleSystem.emission;
        emission.rateOverTime = 20f;
        
        var shape = spawnedParticleSystem.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.5f;
        
        // Устанавливаем ссылку
        ropeSlideParticles = spawnedParticleSystem;
        
        Debug.Log("RopeSlideParticleSpawner: Создан автоматический Particle System!");
    }
    
    public void StartRopeSliding()
    {
        isRopeSliding = true;
        
        // Уничтожаем старую копию если есть
        if (spawnedParticleSystem != null)
        {
            Destroy(spawnedParticleSystem.gameObject);
            spawnedParticleSystem = null;
        }
        
        // Если указан Particle System - создаем копию GameObject
        if (ropeSlideParticles != null)
        {
            // Создаем копию GameObject с Particle System
            GameObject particleObject = Instantiate(ropeSlideParticles.gameObject);
            particleObject.name = "RopeSlideParticles_Copy";
            
            // Устанавливаем позицию под игроком с настраиваемым смещением по Y
            Vector3 targetPosition = transform.position + new Vector3(0f, yOffset, 0f);
            particleObject.transform.position = targetPosition;
            
            // Получаем Particle System из копии
            spawnedParticleSystem = particleObject.GetComponent<ParticleSystem>();
            
            spawnedParticleSystem.Play();
            Debug.Log($"RopeSlideParticleSpawner: Создана копия твоего Particle System под игроком!");
        }
        else if (spawnedParticleSystem != null)
        {
            // Используем созданный автоматически
            spawnedParticleSystem.Play();
            Debug.Log("RopeSlideParticleSpawner: Автоматический Particle System активирован!");
        }
        else
        {
            Debug.LogError("RopeSlideParticleSpawner: Нет Particle System для активации!");
        }
    }
    
    public void StopRopeSliding()
    {
        isRopeSliding = false;
        
        // Останавливаем Particle System
        if (spawnedParticleSystem != null)
        {
            spawnedParticleSystem.Stop();
            Debug.Log("RopeSlideParticleSpawner: Particle System остановлен!");
        }
    }
    
    // Публичные методы для внешнего управления
    public void SetParticleSystem(ParticleSystem particleSystem)
    {
        ropeSlideParticles = particleSystem;
    }
    
    public bool IsRopeSliding()
    {
        return isRopeSliding;
    }
    
    public ParticleSystem GetCurrentParticleSystem()
    {
        return ropeSlideParticles != null ? ropeSlideParticles : spawnedParticleSystem;
    }
} 