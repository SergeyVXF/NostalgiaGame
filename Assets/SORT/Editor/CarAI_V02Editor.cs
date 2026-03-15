using UnityEditor;
using UnityEngine;
using Invector;
using System.Collections.Generic;

[CustomEditor(typeof(CarAI_V02))]
public class CarAI_V02Editor : Editor
{
    public override void OnInspectorGUI()
    {
        CarAI_V02 platform = (CarAI_V02)target;
        DrawDefaultInspector();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Управление точками (Waypoints)", EditorStyles.boldLabel);

        if (GUILayout.Button("Добавить точку в конец"))
        {
            platform.points.Add(new CarAI_V02.vPlatformPoint());
        }

        for (int i = 0; i < platform.points.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Точка {i}", GUILayout.Width(60));
            if (GUILayout.Button("Вставить выше", GUILayout.Width(90)))
            {
                platform.points.Insert(i, new CarAI_V02.vPlatformPoint());
                break;
            }
            if (GUILayout.Button("Удалить", GUILayout.Width(60)))
            {
                platform.points.RemoveAt(i);
                break;
            }
            EditorGUILayout.EndHorizontal();
        }

        if (GUI.changed)
        {
            EditorUtility.SetDirty(platform);
        }
    }
} 