using UnityEngine;
using UnityEditor;

public class AudioZoneExample : MonoBehaviour
{
    [Header("Демонстрация аудио зон")]
    [Tooltip("Создать демо-зоны при запуске")]
    public bool createDemoZones = true;
    
    [Header("Настройки демо-зон")]
    public Vector3[] zonePositions = {
        new Vector3(0, 0, 0),
        new Vector3(20, 0, 0),
        new Vector3(-20, 0, 0),
        new Vector3(0, 0, 20),
        new Vector3(0, 0, -20)
    };
    
    void Start()
    {
        if (createDemoZones)
        {
            CreateDemoAudioZones();
        }
    }
    
    void CreateDemoAudioZones()
    {
        Debug.Log("[AudioZoneExample] Создаю демонстрационные аудио зоны...");
        
        // Создаем папку для демо-зон
        GameObject demoParent = new GameObject("Demo_AudioZones");
        
        // 1. Музыкальный автомат
        CreateAudioZone(demoParent, "Музыкальный автомат", zonePositions[0], 
            "Фоновая музыка", "Популярные песни", 0.3f, 0.8f, 180f, 12f);
        
        // 2. Радиостанция
        CreateAudioZone(demoParent, "Радиостанция", zonePositions[1], 
            "Радиошум", "Новости", 0.2f, 0.9f, 600f, 25f);
        
        // 3. Атмосферный звук
        CreateAudioZone(demoParent, "Атмосферный звук", zonePositions[2], 
            "Тихие звуки", "Случайные события", 0.1f, 0.6f, 120f, 8f);
        
        // 4. Магазин
        CreateAudioZone(demoParent, "Магазин", zonePositions[3], 
            "Тихая музыка", "Объявления", 0.25f, 0.7f, 300f, 15f);
        
        // 5. Парк
        CreateAudioZone(demoParent, "Парк", zonePositions[4], 
            "Звуки природы", "Пение птиц", 0.15f, 0.5f, 240f, 20f);
        
        Debug.Log("[AudioZoneExample] ✅ Демонстрационные аудио зоны созданы!");
        Debug.Log("[AudioZoneExample] Подойдите к любой зоне, чтобы услышать аудио");
    }
    
    private void CreateAudioZone(GameObject parent, string name, Vector3 position, 
        string backgroundDesc, string periodicDesc, float bgVol, float perVol, 
        float interval, float radius)
    {
        // Создаем GameObject для аудио зоны
        GameObject audioZone = new GameObject(name);
        audioZone.transform.SetParent(parent.transform);
        audioZone.transform.position = position;
        
        // Добавляем компонент AudioZoneController
        AudioZoneController controller = audioZone.AddComponent<AudioZoneController>();
        
        // Создаем тестовые аудиофайлы для этой зоны
        string bgPath = $"Assets/Audio/Test/{name}_Background.ogg";
        string perPath = $"Assets/Audio/Test/{name}_Periodic.ogg";
        
        CreateSimpleAudioClip(bgPath, 44100, 15f, 0.3f, backgroundDesc);
        CreateSimpleAudioClip(perPath, 44100, 8f, 0.6f, periodicDesc);
        
        // Загружаем созданные аудиофайлы
        AudioClip backgroundClip = AssetDatabase.LoadAssetAtPath<AudioClip>(bgPath);
        AudioClip periodicClip = AssetDatabase.LoadAssetAtPath<AudioClip>(perPath);
        
        // Настраиваем контроллер
        controller.backgroundAudio = backgroundClip;
        controller.periodicAudio = periodicClip;
        controller.backgroundVolume = bgVol;
        controller.periodicVolume = perVol;
        controller.periodicInterval = interval;
        controller.audioRadius = radius;
        controller.useFadeEffect = true;
        controller.fadeSpeed = 1.5f;
        controller.playerTag = "Player";
        
        // Добавляем визуальный компонент
        CreateVisualComponent(audioZone, name);
        
        Debug.Log($"[AudioZoneExample] Создана зона '{name}' с радиусом {radius}м");
    }
    
    private void CreateVisualComponent(GameObject audioZone, string zoneName)
    {
        // Создаем визуальный объект
        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        visual.name = "Visual";
        visual.transform.SetParent(audioZone.transform);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localScale = new Vector3(0.5f, 0.1f, 0.5f);
        
        // Настраиваем материал
        Renderer renderer = visual.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material material = new Material(Shader.Find("Standard"));
            
            // Разные цвета для разных зон
            Color zoneColor = GetZoneColor(zoneName);
            material.color = new Color(zoneColor.r, zoneColor.g, zoneColor.b, 0.4f);
            material.SetFloat("_Mode", 3);
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = 3000;
            
            renderer.material = material;
        }
        
        // Удаляем коллайдер
        Collider collider = visual.GetComponent<Collider>();
        if (collider != null)
        {
            Object.DestroyImmediate(collider);
        }
    }
    
    private Color GetZoneColor(string zoneName)
    {
        switch (zoneName)
        {
            case "Музыкальный автомат": return Color.magenta;
            case "Радиостанция": return Color.blue;
            case "Атмосферный звук": return Color.green;
            case "Магазин": return Color.yellow;
            case "Парк": return Color.cyan;
            default: return Color.white;
        }
    }
    
    private void CreateSimpleAudioClip(string path, int sampleRate, float duration, float frequency, string description)
    {
        // Проверяем, существует ли уже файл
        if (AssetDatabase.LoadAssetAtPath<AudioClip>(path) != null)
        {
            return; // Файл уже существует
        }
        
        // Создаем папку если её нет
        string folder = System.IO.Path.GetDirectoryName(path);
        if (!System.IO.Directory.Exists(folder))
        {
            System.IO.Directory.CreateDirectory(folder);
        }
        
        // Создаем простой синусоидальный тон
        int samples = (int)(sampleRate * duration);
        float[] audioData = new float[samples];
        
        for (int i = 0; i < samples; i++)
        {
            float time = (float)i / sampleRate;
            audioData[i] = Mathf.Sin(2f * Mathf.PI * frequency * time) * 0.3f;
        }
        
        // Создаем AudioClip
        AudioClip clip = AudioClip.Create(description, samples, 1, sampleRate, false);
        clip.SetData(audioData, 0);
        
        // Сохраняем как asset
        AssetDatabase.CreateAsset(clip, path);
    }
    
    // Публичные методы для внешнего управления
    public void CreateSingleAudioZone()
    {
        CreateDemoAudioZones();
    }
    
    public void ClearDemoZones()
    {
        GameObject demoParent = GameObject.Find("Demo_AudioZones");
        if (demoParent != null)
        {
            Object.DestroyImmediate(demoParent);
            Debug.Log("[AudioZoneExample] Демо-зоны удалены");
        }
    }
} 