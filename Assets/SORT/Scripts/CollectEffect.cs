using UnityEngine;

public class CollectEffect : MonoBehaviour
{
    [Header("Настройки эффекта")]
    public float effectDuration = 1f;
    public float particleSpeed = 5f;
    public int particleCount = 10;
    public Color particleColor = Color.yellow;
    
    private ParticleSystem particleSystem;
    private float elapsedTime = 0f;
    
    public void Initialize()
    {
        CreateParticleSystem();
        StartCoroutine(EffectCoroutine());
    }
    
    public void Initialize(Vector3 position)
    {
        transform.position = position;
        CreateParticleSystem();
        StartCoroutine(EffectCoroutine());
    }
    
    void CreateParticleSystem()
    {
        // Создаем систему частиц
        particleSystem = gameObject.AddComponent<ParticleSystem>();
        
        // Настраиваем основные параметры
        var main = particleSystem.main;
        main.loop = false;
        main.startLifetime = effectDuration;
        main.startSpeed = particleSpeed;
        main.startColor = particleColor;
        main.maxParticles = particleCount;
        
        // Настраиваем эмиссию
        var emission = particleSystem.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new ParticleSystem.Burst[]
        {
            new ParticleSystem.Burst(0.0f, particleCount)
        });
        
        // Настраиваем форму эмиссии
        var shape = particleSystem.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.1f;
        
        // Настраиваем размер частиц
        var sizeOverLifetime = particleSystem.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(0.1f, 0.5f);
        
        // Настраиваем цвет частиц
        var colorOverLifetime = particleSystem.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(particleColor, Color.clear);
        
        // Запускаем систему частиц
        particleSystem.Play();
    }
    
    System.Collections.IEnumerator EffectCoroutine()
    {
        while (elapsedTime < effectDuration)
        {
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        
        // Уничтожаем эффект
        Destroy(gameObject);
    }
}