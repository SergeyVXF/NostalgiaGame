using UnityEngine;

public class ParticleEffectAutoDestroy : MonoBehaviour
{
    [Header("Настройки")]
    public float destroyDelay = 3f; // Задержка перед уничтожением
    public bool destroyOnParticleSystemEnd = true; // Уничтожать когда ParticleSystem завершится
    
    private ParticleSystem particleSystem;
    private float startTime;
    
    void Start()
    {
        startTime = Time.time;
        particleSystem = GetComponent<ParticleSystem>();
        
        if (destroyOnParticleSystemEnd && particleSystem != null)
        {
            // Уничтожаем когда ParticleSystem завершится
            StartCoroutine(DestroyWhenParticleSystemEnds());
        }
        else
        {
            // Уничтожаем через заданное время
            Destroy(gameObject, destroyDelay);
        }
    }
    
    System.Collections.IEnumerator DestroyWhenParticleSystemEnds()
    {
        // Ждем пока ParticleSystem не завершится
        while (particleSystem != null && particleSystem.IsAlive())
        {
            yield return null;
        }
        
        // Добавляем небольшую задержку для завершения анимации
        yield return new WaitForSeconds(0.5f);
        
        // Уничтожаем объект
        Destroy(gameObject);
    }
    
    // Метод для ручного уничтожения
    public void DestroyEffect()
    {
        Destroy(gameObject);
    }
    
    // Метод для установки задержки уничтожения
    public void SetDestroyDelay(float delay)
    {
        destroyDelay = delay;
    }
} 