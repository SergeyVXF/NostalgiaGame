using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class K_01EnhancedVisibility : MonoBehaviour
{
    [Header("Подсветка")]
    [SerializeField] private bool useEmission = true;
    [SerializeField] private Color emissionColor = new Color(0.8f, 0.2f, 0.2f);
    [SerializeField] private float emissionIntensity = 2.0f;
    
    [Header("Эффекты")]
    [SerializeField] private bool useOutline = true;
    [SerializeField] private float outlineWidth = 0.02f;
    [SerializeField] private Color outlineColor = Color.white;
    [SerializeField] private bool pulse = true;
    [SerializeField] private float pulseSpeed = 1.0f;
    [SerializeField] private float pulseMinIntensity = 0.5f;
    [SerializeField] private float pulseMaxIntensity = 1.5f;

    // Компоненты
    private Renderer objectRenderer;
    private Material originalMaterial;
    private Material instancedMaterial;
    
    // Для пульсации
    private float pulseTime = 0f;

    private void Awake()
    {
        objectRenderer = GetComponent<Renderer>();
        
        if (objectRenderer != null && objectRenderer.material != null)
        {
            // Создаем экземпляр материала, чтобы не модифицировать оригинал
            originalMaterial = objectRenderer.material;
            instancedMaterial = new Material(originalMaterial);
            objectRenderer.material = instancedMaterial;
            
            // Применяем начальные настройки
            ApplyEmission();
            
            if (useOutline)
            {
                CreateOutline();
            }
        }
    }

    private void Update()
    {
        if (pulse && instancedMaterial != null)
        {
            // Рассчитываем интенсивность эмиссии на основе синусоиды
            pulseTime += Time.deltaTime * pulseSpeed;
            float pulseValue = Mathf.Lerp(pulseMinIntensity, pulseMaxIntensity, 
                (Mathf.Sin(pulseTime) + 1f) * 0.5f);
            
            // Применяем пульсацию к эмиссии
            if (useEmission)
            {
                instancedMaterial.SetColor("_EmissionColor", emissionColor * pulseValue * emissionIntensity);
            }
        }
    }

    private void ApplyEmission()
    {
        if (useEmission && instancedMaterial != null)
        {
            // Включаем эмиссию на материале
            instancedMaterial.EnableKeyword("_EMISSION");
            instancedMaterial.SetColor("_EmissionColor", emissionColor * emissionIntensity);
            
            // Проверяем, поддерживает ли рендерер глобальное освещение
            if (gameObject.activeInHierarchy)
            {
                RendererExtensions.UpdateGIMaterials(objectRenderer);
            }
        }
    }

    private void CreateOutline()
    {
        // Создаем дочерний объект для контура
        GameObject outlineObj = new GameObject("Outline");
        outlineObj.transform.SetParent(transform);
        outlineObj.transform.localPosition = Vector3.zero;
        outlineObj.transform.localRotation = Quaternion.identity;
        outlineObj.transform.localScale = Vector3.one * (1f + outlineWidth);
        
        // Добавляем компоненты для контура
        MeshFilter sourceMF = GetComponent<MeshFilter>();
        if (sourceMF != null && sourceMF.sharedMesh != null)
        {
            // Копируем меш
            MeshFilter outlineMF = outlineObj.AddComponent<MeshFilter>();
            outlineMF.sharedMesh = sourceMF.sharedMesh;
            
            // Создаем материал контура
            MeshRenderer outlineMR = outlineObj.AddComponent<MeshRenderer>();
            Material outlineMaterial = new Material(Shader.Find("Standard"));
            outlineMaterial.color = outlineColor;
            
            // Настраиваем свойства контура
            outlineMaterial.SetFloat("_Mode", 3); // Transparent mode
            outlineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            outlineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            outlineMaterial.SetInt("_ZWrite", 0);
            outlineMaterial.DisableKeyword("_ALPHATEST_ON");
            outlineMaterial.EnableKeyword("_ALPHABLEND_ON");
            outlineMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            outlineMaterial.renderQueue = 3000;
            
            // Устанавливаем прозрачный материал
            Color outlineColorWithAlpha = outlineColor;
            outlineColorWithAlpha.a = 0.5f;
            outlineMaterial.color = outlineColorWithAlpha;
            
            outlineMR.material = outlineMaterial;
            
            // Перемещаем контур назад на слой сортировки
            outlineMR.sortingOrder = objectRenderer.sortingOrder - 1;
        }
    }

    // Публичный метод для временного усиления подсветки (можно вызывать при взгляде на объект)
    public void HighlightTemporarily(float duration = 1.0f, float intensityMultiplier = 3.0f)
    {
        if (instancedMaterial != null && useEmission)
        {
            // Усиливаем подсветку
            instancedMaterial.SetColor("_EmissionColor", emissionColor * emissionIntensity * intensityMultiplier);
            
            // Возвращаем к обычной подсветке через определенное время
            Invoke("ResetHighlight", duration);
        }
    }

    private void ResetHighlight()
    {
        if (instancedMaterial != null && useEmission)
        {
            instancedMaterial.SetColor("_EmissionColor", emissionColor * emissionIntensity);
        }
    }

    private void OnDestroy()
    {
        // Очищаем экземпляры материалов
        if (instancedMaterial != null)
        {
            Destroy(instancedMaterial);
        }
    }
} 