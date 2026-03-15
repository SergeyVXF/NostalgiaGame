using UnityEngine;

public class WindController : MonoBehaviour
{
    [Header("Wind Settings")]
    [Range(0.0f, 2.0f)]
    public float windStrength = 0.5f;
    
    [Range(0.1f, 5.0f)]
    public float windSpeed = 1.0f;
    
    public Vector3 windDirection = new Vector3(1, 0, 0);
    
    [Range(0.1f, 2.0f)]
    public float leafStiffness = 1.0f;
    
    [Range(0.1f, 5.0f)]
    public float leafSize = 1.0f;
    
    [Header("Wind Variation")]
    public bool enableWindVariation = true;
    [Range(0.1f, 2.0f)]
    public float variationStrength = 0.3f;
    [Range(0.1f, 5.0f)]
    public float variationSpeed = 0.5f;
    
    [Header("Affected Materials")]
    public Material[] windMaterials;
    
    private void Start()
    {
        // Если материалы не назначены, попробуем найти их автоматически
        if (windMaterials == null || windMaterials.Length == 0)
        {
            FindWindMaterials();
        }
        
        UpdateWindParameters();
    }
    
    private void Update()
    {
        UpdateWindParameters();
    }
    
    private void UpdateWindParameters()
    {
        float currentWindStrength = windStrength;
        float currentWindSpeed = windSpeed;
        
        // Добавляем вариацию ветра если включена
        if (enableWindVariation)
        {
            float variation = Mathf.Sin(Time.time * variationSpeed) * variationStrength;
            currentWindStrength += variation;
            currentWindSpeed += variation * 0.5f;
        }
        
        // Применяем параметры ко всем материалам
        foreach (Material mat in windMaterials)
        {
            if (mat != null)
            {
                mat.SetFloat("_WindStrength", currentWindStrength);
                mat.SetFloat("_WindSpeed", currentWindSpeed);
                mat.SetVector("_WindDirection", windDirection);
                mat.SetFloat("_LeafStiffness", leafStiffness);
                mat.SetFloat("_LeafSize", leafSize);
            }
        }
    }
    
    private void FindWindMaterials()
    {
        // Ищем все рендереры в сцене
        Renderer[] renderers = FindObjectsOfType<Renderer>();
        System.Collections.Generic.List<Material> materials = new System.Collections.Generic.List<Material>();
        
        foreach (Renderer renderer in renderers)
        {
            foreach (Material mat in renderer.materials)
            {
                // Проверяем, использует ли материал наш шейдер
                if (mat.shader.name.Contains("LeafWindShader"))
                {
                    materials.Add(mat);
                }
            }
        }
        
        windMaterials = materials.ToArray();
        
        if (windMaterials.Length > 0)
        {
            Debug.Log($"Found {windMaterials.Length} wind materials automatically");
        }
        else
        {
            Debug.LogWarning("No wind materials found. Please assign materials manually.");
        }
    }
    
    // Методы для изменения параметров ветра из других скриптов
    public void SetWindStrength(float strength)
    {
        windStrength = Mathf.Clamp(strength, 0.0f, 2.0f);
    }
    
    public void SetWindSpeed(float speed)
    {
        windSpeed = Mathf.Clamp(speed, 0.1f, 5.0f);
    }
    
    public void SetWindDirection(Vector3 direction)
    {
        windDirection = direction.normalized;
    }
    
    public void SetLeafStiffness(float stiffness)
    {
        leafStiffness = Mathf.Clamp(stiffness, 0.1f, 2.0f);
    }
    
    public void SetLeafSize(float size)
    {
        leafSize = Mathf.Clamp(size, 0.1f, 5.0f);
    }
    
    // Метод для создания сильного порыва ветра
    public void CreateWindGust(float duration = 2.0f, float strength = 1.5f)
    {
        StartCoroutine(WindGustCoroutine(duration, strength));
    }
    
    private System.Collections.IEnumerator WindGustCoroutine(float duration, float gustStrength)
    {
        float originalStrength = windStrength;
        float originalSpeed = windSpeed;
        
        // Увеличиваем силу и скорость ветра
        windStrength = gustStrength;
        windSpeed = windSpeed * 1.5f;
        
        yield return new WaitForSeconds(duration);
        
        // Возвращаем к исходным значениям
        windStrength = originalStrength;
        windSpeed = originalSpeed;
    }
} 