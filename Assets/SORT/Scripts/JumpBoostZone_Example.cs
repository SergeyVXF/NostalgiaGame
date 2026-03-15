using UnityEngine;

/// <summary>
/// Пример создания различных типов зон подбрасывания
/// Этот скрипт можно использовать для автоматического создания демо-сцены
/// </summary>
public class JumpBoostZone_Example : MonoBehaviour
{
    [Header("Демо настройки")]
    [Tooltip("Создать демо-зоны при запуске")]
    public bool createDemoZonesOnStart = true;
    
    [Tooltip("Префаб зоны подбрасывания")]
    public GameObject jumpZonePrefab;
    
    void Start()
    {
        if (createDemoZonesOnStart)
        {
            CreateDemoZones();
        }
    }
    
    [ContextMenu("Создать демо-зоны")]
    public void CreateDemoZones()
    {
        Debug.Log("[JumpBoostZone_Example] Создаю демо-зоны подбрасывания...");
        
        // Создаем папку для демо-зон
        GameObject demoParent = new GameObject("Demo_JumpBoostZones");
        
        // 1. Вертикальная зона (платформер)
        CreateJumpZone(demoParent, "Vertical_Jump", new Vector3(0, 0, 0), new Vector3(3, 1, 3), 2000f, Vector3.zero, 0f, 3f);
        
        // 2. Трамплин вперед
        CreateJumpZone(demoParent, "Forward_Jump", new Vector3(10, 0, 0), new Vector3(4, 1, 2), 2500f, new Vector3(0, 0.7f, 0.7f), 800f, 4f);
        
        // 3. Трамплин назад
        CreateJumpZone(demoParent, "Backward_Jump", new Vector3(-10, 0, 0), new Vector3(4, 1, 2), 2500f, new Vector3(0, 0.7f, -0.7f), -800f, 4f);
        
        // 4. Слабая зона с минимальной скоростью
        CreateJumpZone(demoParent, "Weak_SpeedGate", new Vector3(0, 0, 10), new Vector3(2, 1, 2), 1200f, Vector3.zero, 0f, 2f, 5f);
        
        // 5. Сильная зона для больших прыжков
        CreateJumpZone(demoParent, "Strong_Jump", new Vector3(0, 0, -10), new Vector3(5, 1, 5), 3500f, Vector3.zero, 0f, 6f);
        
        Debug.Log("[JumpBoostZone_Example] ✅ Демо-зоны созданы! Проверьте объект 'Demo_JumpBoostZones' на сцене.");
    }
    
    private void CreateJumpZone(GameObject parent, string name, Vector3 position, Vector3 scale, float jumpForce, Vector3 direction, float horizontalBoost, float cooldown, float minSpeed = 0f)
    {
        GameObject zone;
        
        if (jumpZonePrefab != null)
        {
            zone = Instantiate(jumpZonePrefab, parent.transform);
        }
        else
        {
            // Создаем зону с нуля
            zone = GameObject.CreatePrimitive(PrimitiveType.Cube);
            zone.transform.SetParent(parent.transform);
            
            // Настраиваем коллайдер
            BoxCollider collider = zone.GetComponent<BoxCollider>();
            if (collider != null)
            {
                collider.isTrigger = true;
            }
        }
        
        // Настраиваем трансформ
        zone.name = name;
        zone.transform.position = position;
        zone.transform.localScale = scale;
        
        // Добавляем компонент зоны подбрасывания
        JumpBoostZone jumpZone = zone.GetComponent<JumpBoostZone>();
        if (jumpZone == null)
        {
            jumpZone = zone.AddComponent<JumpBoostZone>();
        }
        
        // Настраиваем параметры
        jumpZone.jumpForce = jumpForce;
        jumpZone.jumpDirection = direction;
        jumpZone.horizontalBoost = horizontalBoost;
        jumpZone.cooldownTime = cooldown;
        jumpZone.minSpeedToActivate = minSpeed;
        
        // Создаем простой материал для зоны
        CreateSimpleMaterial(zone, name);
        
        Debug.Log($"[JumpBoostZone_Example] Создана зона '{name}' с силой {jumpForce}");
    }
    
    private void CreateSimpleMaterial(GameObject zone, string zoneName)
    {
        Renderer renderer = zone.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material mat = new Material(Shader.Find("Standard"));
            
            // Разные цвета для разных типов зон
            switch (zoneName)
            {
                case "Vertical_Jump":
                    mat.color = new Color(0f, 1f, 0f, 0.3f); // Зеленый
                    break;
                case "Forward_Jump":
                    mat.color = new Color(0f, 0f, 1f, 0.3f); // Синий
                    break;
                case "Backward_Jump":
                    mat.color = new Color(1f, 0f, 1f, 0.3f); // Пурпурный
                    break;
                case "Weak_SpeedGate":
                    mat.color = new Color(1f, 1f, 0f, 0.3f); // Желтый
                    break;
                case "Strong_Jump":
                    mat.color = new Color(1f, 0.5f, 0f, 0.3f); // Оранжевый
                    break;
                default:
                    mat.color = new Color(0.5f, 0.5f, 0.5f, 0.3f); // Серый
                    break;
            }
            
            // Настраиваем прозрачность
            mat.SetFloat("_Mode", 3); // Transparent mode
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = 3000;
            
            renderer.material = mat;
        }
    }
    
    [ContextMenu("Удалить демо-зоны")]
    public void RemoveDemoZones()
    {
        GameObject demoParent = GameObject.Find("Demo_JumpBoostZones");
        if (demoParent != null)
        {
            DestroyImmediate(demoParent);
            Debug.Log("[JumpBoostZone_Example] Демо-зоны удалены.");
        }
        else
        {
            Debug.Log("[JumpBoostZone_Example] Демо-зоны не найдены.");
        }
    }
    
    void OnDrawGizmos()
    {
        // Показываем подсказку в Scene View
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(transform.position, Vector3.one * 2f);
        
        // Текст подсказки
        #if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * 2f, "JumpBoostZone Example\nПКМ → Создать демо-зоны");
        #endif
    }
} 