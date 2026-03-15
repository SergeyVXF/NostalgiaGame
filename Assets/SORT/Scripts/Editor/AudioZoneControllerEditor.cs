using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(AudioZoneController))]
public class AudioZoneControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        
        AudioZoneController audioZone = (AudioZoneController)target;
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Быстрые настройки", EditorStyles.boldLabel);
        
        // Настройки громкости
        EditorGUILayout.BeginHorizontal();
        
        if (GUILayout.Button("Тихий фон"))
        {
            audioZone.backgroundVolume = 0.2f;
            audioZone.periodicVolume = 0.6f;
            EditorUtility.SetDirty(audioZone);
        }
        
        if (GUILayout.Button("Средняя громкость"))
        {
            audioZone.backgroundVolume = 0.4f;
            audioZone.periodicVolume = 0.8f;
            EditorUtility.SetDirty(audioZone);
        }
        
        if (GUILayout.Button("Громкий"))
        {
            audioZone.backgroundVolume = 0.6f;
            audioZone.periodicVolume = 1f;
            EditorUtility.SetDirty(audioZone);
        }
        
        EditorGUILayout.EndHorizontal();
        
        // Настройки интервала
        EditorGUILayout.BeginHorizontal();
        
        if (GUILayout.Button("1 минута"))
        {
            audioZone.periodicInterval = 60f;
            EditorUtility.SetDirty(audioZone);
        }
        
        if (GUILayout.Button("5 минут"))
        {
            audioZone.periodicInterval = 300f;
            EditorUtility.SetDirty(audioZone);
        }
        
        if (GUILayout.Button("10 минут"))
        {
            audioZone.periodicInterval = 600f;
            EditorUtility.SetDirty(audioZone);
        }
        
        EditorGUILayout.EndHorizontal();
        
        // Настройки радиуса
        EditorGUILayout.BeginHorizontal();
        
        if (GUILayout.Button("Маленькая зона (5м)"))
        {
            audioZone.audioRadius = 5f;
            EditorUtility.SetDirty(audioZone);
        }
        
        if (GUILayout.Button("Средняя зона (15м)"))
        {
            audioZone.audioRadius = 15f;
            EditorUtility.SetDirty(audioZone);
        }
        
        if (GUILayout.Button("Большая зона (30м)"))
        {
            audioZone.audioRadius = 30f;
            EditorUtility.SetDirty(audioZone);
        }
        
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Тестирование", EditorStyles.boldLabel);
        
                       // Кнопки тестирования
               EditorGUILayout.BeginHorizontal();

               if (GUILayout.Button("Тест периодического аудио"))
               {
                   if (Application.isPlaying)
                   {
                       audioZone.ForcePlayPeriodicAudio();
                   }
                   else
                   {
                       EditorUtility.DisplayDialog("Тестирование", "Запустите игру для тестирования аудио", "OK");
                   }
               }

               if (GUILayout.Button("Остановить периодическое аудио"))
               {
                   if (Application.isPlaying)
                   {
                       audioZone.StopPeriodicAudio();
                   }
                   else
                   {
                       EditorUtility.DisplayDialog("Тестирование", "Запустите игру для тестирования аудио", "OK");
                   }
               }

               EditorGUILayout.EndHorizontal();

               if (GUILayout.Button("Создать тестовые аудио"))
               {
                   CreateTestAudioClips();
               }
        
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "💡 Советы:\n" +
            "• Фоновое аудио играет постоянно и зациклено\n" +
            "• Периодическое аудио воспроизводится сразу при входе в зону\n" +
            "• Затем периодическое аудио играет через заданные интервалы\n" +
            "• Периодическое аудио играет только когда игрок в зоне\n" +
            "• Используйте плавное затухание для лучшего эффекта\n" +
            "• Радиус зоны отображается в Scene View\n" +
            "• Убедитесь, что у игрока установлен правильный тег",
            MessageType.Info
        );
        
        // Предупреждения
        if (audioZone.backgroundAudio == null)
        {
            EditorGUILayout.HelpBox("⚠️ Фоновое аудио не назначено!", MessageType.Warning);
        }
        
        if (audioZone.periodicAudio == null)
        {
            EditorGUILayout.HelpBox("⚠️ Периодическое аудио не назначено!", MessageType.Warning);
        }
        
        if (string.IsNullOrEmpty(audioZone.playerTag))
        {
            EditorGUILayout.HelpBox("⚠️ Тег игрока не установлен!", MessageType.Warning);
        }
    }
    
    private void CreateTestAudioClips()
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
        
        EditorUtility.DisplayDialog("Тестовые аудио", "Созданы тестовые аудиофайлы в папке Assets/Audio/Test/", "OK");
    }
    
    private void CreateSimpleAudioClip(string path, int sampleRate, float duration, float frequency, string description)
    {
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