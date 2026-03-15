using UnityEngine;
using UnityEditor;

public class AudioZoneCreator
{
    [MenuItem("GameObject/Audio/Audio Zone", false, 10)]
    static void CreateAudioZone()
    {
        // Создаем GameObject для аудио зоны
        GameObject audioZone = new GameObject("Audio Zone");
        
        // Добавляем компонент AudioZoneController
        AudioZoneController controller = audioZone.AddComponent<AudioZoneController>();
        
        // Добавляем визуальный компонент (куб для отображения в редакторе)
        GameObject visualObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visualObject.name = "Visual";
        visualObject.transform.SetParent(audioZone.transform);
        visualObject.transform.localPosition = Vector3.zero;
        visualObject.transform.localScale = new Vector3(1f, 0.1f, 1f); // Плоский куб
        
        // Настраиваем материал для визуального объекта
        Renderer renderer = visualObject.GetComponent<Renderer>();
        if (renderer != null)
        {
            // Создаем полупрозрачный материал
            Material material = new Material(Shader.Find("Standard"));
            material.color = new Color(0f, 1f, 0f, 0.3f); // Зеленый полупрозрачный
            material.SetFloat("_Mode", 3); // Transparent mode
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = 3000;
            
            renderer.material = material;
        }
        
        // Удаляем коллайдер с визуального объекта (он не нужен)
        Collider visualCollider = visualObject.GetComponent<Collider>();
        if (visualCollider != null)
        {
            Object.DestroyImmediate(visualCollider);
        }
        
        // Размещаем объект в центре сцены
        audioZone.transform.position = Vector3.zero;
        
        // Выбираем созданный объект
        Selection.activeGameObject = audioZone;
        
        // Фокусируемся на объекте в Scene View
        SceneView.FrameLastActiveSceneView();
        
        Debug.Log("[AudioZoneCreator] Аудио зона создана! Настройте аудиофайлы в инспекторе.");
    }
    
    [MenuItem("GameObject/Audio/Audio Zone (with Test Audio)", false, 11)]
    static void CreateAudioZoneWithTestAudio()
    {
        // Сначала создаем тестовые аудиофайлы
        CreateTestAudioClips();
        
        // Создаем аудио зону
        CreateAudioZone();
        
        // Находим созданную зону и назначаем тестовые аудио
        AudioZoneController controller = Selection.activeGameObject.GetComponent<AudioZoneController>();
        if (controller != null)
        {
            // Загружаем созданные тестовые аудиофайлы
            AudioClip backgroundClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Test/Background_Test.ogg");
            AudioClip periodicClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Test/Periodic_Test.ogg");
            
            if (backgroundClip != null)
            {
                controller.backgroundAudio = backgroundClip;
            }
            
            if (periodicClip != null)
            {
                controller.periodicAudio = periodicClip;
            }
            
            // Устанавливаем разумные настройки по умолчанию
            controller.backgroundVolume = 0.3f;
            controller.periodicVolume = 0.8f;
            controller.audioRadius = 10f;
            controller.periodicInterval = 300f; // 5 минут
            controller.useFadeEffect = true;
            controller.fadeSpeed = 1f;
            
            EditorUtility.SetDirty(controller);
            
            Debug.Log("[AudioZoneCreator] Аудио зона создана с тестовыми аудиофайлами!");
        }
    }
    
    private static void CreateTestAudioClips()
    {
        // Создаем папку для тестовых аудио если её нет
        if (!AssetDatabase.IsValidFolder("Assets/Audio"))
        {
            AssetDatabase.CreateFolder("Assets", "Audio");
        }
        
        if (!AssetDatabase.IsValidFolder("Assets/Audio/Test"))
        {
            AssetDatabase.CreateFolder("Assets/Audio", "Test");
        }
        
        // Создаем простые аудио клипы для тестирования
        CreateSimpleAudioClip("Assets/Audio/Test/Background_Test.ogg", 44100, 10f, 0.5f, "Фоновое тестовое аудио");
        CreateSimpleAudioClip("Assets/Audio/Test/Periodic_Test.ogg", 44100, 5f, 0.8f, "Периодическое тестовое аудио");
        
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }
    
    private static void CreateSimpleAudioClip(string path, int sampleRate, float duration, float frequency, string description)
    {
        // Проверяем, существует ли уже файл
        if (AssetDatabase.LoadAssetAtPath<AudioClip>(path) != null)
        {
            return; // Файл уже существует
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
} 