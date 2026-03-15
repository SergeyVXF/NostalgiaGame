using UnityEngine;
using UnityEditor;

/// <summary>
/// Скрипт для автоматического добавления тега "Vehicle" в Unity
/// Запускается при компиляции проекта
/// </summary>
public class VehicleTagSetup
{
    [InitializeOnLoadMethod]
    static void SetupVehicleTag()
    {
        // Проверяем, есть ли уже тег "Vehicle"
        SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty tags = tagManager.FindProperty("tags");
        
        bool vehicleTagExists = false;
        for (int i = 0; i < tags.arraySize; i++)
        {
            SerializedProperty tag = tags.GetArrayElementAtIndex(i);
            if (tag.stringValue == "Vehicle")
            {
                vehicleTagExists = true;
                break;
            }
        }
        
        // Если тега нет, добавляем его
        if (!vehicleTagExists)
        {
            tags.InsertArrayElementAtIndex(tags.arraySize);
            SerializedProperty newTag = tags.GetArrayElementAtIndex(tags.arraySize - 1);
            newTag.stringValue = "Vehicle";
            tagManager.ApplyModifiedProperties();
            
            Debug.Log("✅ Тег 'Vehicle' добавлен в проект!");
        }
    }
} 