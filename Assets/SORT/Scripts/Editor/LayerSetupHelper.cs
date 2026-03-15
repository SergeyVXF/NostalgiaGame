using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// Вспомогательный скрипт для настройки слоев и тегов в проекте.
/// Используется только в редакторе Unity.
/// </summary>
public class LayerSetupHelper
{
    [MenuItem("Tools/Setup/Add WalkableWall Layer")]
    public static void AddWalkableWallLayer()
    {
        // Проверяем, существует ли уже слой "WalkableWall"
        int layerId = LayerMask.NameToLayer("WalkableWall");
        if (layerId != -1)
        {
            Debug.Log("Слой 'WalkableWall' уже существует с ID: " + layerId);
            return;
        }

        // Пытаемся добавить слой "WalkableWall"
        SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty layersProp = tagManager.FindProperty("layers");

        bool found = false;
        int firstEmptyIndex = -1;

        // Ищем первый свободный слот для пользовательских слоев (8-31)
        for (int i = 8; i < 32; i++)
        {
            SerializedProperty sp = layersProp.GetArrayElementAtIndex(i);
            if (sp.stringValue == "")
            {
                if (firstEmptyIndex == -1)
                    firstEmptyIndex = i;
            }

            if (sp.stringValue == "WalkableWall")
            {
                found = true;
                Debug.Log("Слой 'WalkableWall' уже существует с ID: " + i);
                break;
            }
        }

        // Если слой не найден и есть свободное место, добавляем
        if (!found && firstEmptyIndex != -1)
        {
            layersProp.GetArrayElementAtIndex(firstEmptyIndex).stringValue = "WalkableWall";
            tagManager.ApplyModifiedProperties();
            Debug.Log("Слой 'WalkableWall' добавлен с ID: " + firstEmptyIndex);
        }
        else if (!found)
        {
            Debug.LogError("Не удалось добавить слой 'WalkableWall' - нет свободных слотов!");
        }
    }

    [MenuItem("Tools/Setup/Add WalkableWall Tag")]
    public static void AddWalkableWallTag()
    {
        // Получаем список всех существующих тегов
        SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty tagsProp = tagManager.FindProperty("tags");

        bool found = false;
        List<string> existingTags = new List<string>();

        // Проверяем существующие теги
        for (int i = 0; i < tagsProp.arraySize; i++)
        {
            SerializedProperty t = tagsProp.GetArrayElementAtIndex(i);
            existingTags.Add(t.stringValue);
            if (t.stringValue == "WalkableWall")
            {
                found = true;
                Debug.Log("Тег 'WalkableWall' уже существует");
                break;
            }
        }

        // Если тег не найден, добавляем его
        if (!found)
        {
            int index = tagsProp.arraySize;
            tagsProp.InsertArrayElementAtIndex(index);
            SerializedProperty newTag = tagsProp.GetArrayElementAtIndex(index);
            newTag.stringValue = "WalkableWall";
            tagManager.ApplyModifiedProperties();
            Debug.Log("Тег 'WalkableWall' успешно добавлен");
        }
    }

    [MenuItem("Tools/Setup/Setup WallWalker Project")]
    public static void SetupWallWalkerProject()
    {
        AddWalkableWallLayer();
        AddWalkableWallTag();

        // Создаем материал для отладки, если его нет
        string debugMaterialPath = "Assets/Materials/WallWalkDebug.mat";
        Material debugMaterial = AssetDatabase.LoadAssetAtPath<Material>(debugMaterialPath);
        
        if (debugMaterial == null)
        {
            // Проверяем существование директории Materials
            if (!System.IO.Directory.Exists("Assets/Materials"))
            {
                System.IO.Directory.CreateDirectory("Assets/Materials");
                AssetDatabase.Refresh();
            }
            
            // Создаем материал
            debugMaterial = new Material(Shader.Find("Standard"));
            debugMaterial.color = new Color(0, 1, 0, 0.5f);
            debugMaterial.SetFloat("_Mode", 3); // Transparent
            debugMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            debugMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            debugMaterial.SetInt("_ZWrite", 0);
            debugMaterial.DisableKeyword("_ALPHATEST_ON");
            debugMaterial.EnableKeyword("_ALPHABLEND_ON");
            debugMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            debugMaterial.renderQueue = 3000;
            
            AssetDatabase.CreateAsset(debugMaterial, debugMaterialPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            Debug.Log("Создан материал для отладки: " + debugMaterialPath);
        }
        else
        {
            Debug.Log("Материал для отладки уже существует: " + debugMaterialPath);
        }
        
        // Показываем сообщение пользователю
        EditorUtility.DisplayDialog("Настройка WallWalker", 
            "Настройка завершена!\n\n" +
            "Добавлены:\n" +
            "- Слой 'WalkableWall'\n" +
            "- Тег 'WalkableWall'\n" +
            "- Материал для отладки\n\n" +
            "Теперь вы можете использовать WallWalker для хождения по стенам.", 
            "ОК");
    }

    // Применяем слой WalkableWall к объекту
    public static void MakeObjectWalkable(GameObject obj)
    {
        if (obj == null) return;
        
        // Проверяем существование слоя
        int layerId = LayerMask.NameToLayer("WalkableWall");
        if (layerId == -1)
        {
            Debug.LogError("Слой 'WalkableWall' не найден! Используйте меню Tools > Setup > Setup WallWalker Project");
            return;
        }
        
        // Устанавливаем слой
        obj.layer = layerId;
        
        // Добавляем тег, если он существует
        if (IsTagDefined("WalkableWall"))
        {
            obj.tag = "WalkableWall";
        }
        
        // Добавляем компонент WallWalkableSurface, если его еще нет
        if (obj.GetComponent<WallWalkableSurface>() == null)
        {
            obj.AddComponent<WallWalkableSurface>();
        }
        
        Debug.Log($"Объект '{obj.name}' настроен для хождения по стене");
    }
    
    // Метод для проверки существования тега
    private static bool IsTagDefined(string tagName)
    {
        try
        {
            GameObject testObject = new GameObject();
            testObject.tag = tagName;
            Object.DestroyImmediate(testObject);
            return true;
        }
        catch
        {
            return false;
        }
    }
} 