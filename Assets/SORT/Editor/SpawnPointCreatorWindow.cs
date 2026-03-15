using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class SpawnPointCreatorWindow : EditorWindow
{
    private string spawnPointID = "";
    private bool generateRandomID = true;
    private bool useExactHeight = true;
    private bool applyRotation = true;
    private Color gizmoColor = new Color(0, 1, 0, 0.5f);
    private float gizmoSize = 1f;
    private bool showDirection = true;
    private float directionLength = 2f;
    
    private bool addToManager = true;
    private bool createManager = true;
    private bool setAsDefault = false;
    
    [MenuItem("Утилиты/Создатель точек спавна")]
    public static void ShowWindow()
    {
        GetWindow<SpawnPointCreatorWindow>("Создатель точек спавна");
    }
    
    private void OnGUI()
    {
        GUILayout.Label("Настройки точки спавна", EditorStyles.boldLabel);
        
        EditorGUILayout.Space();
        
        // Идентификатор точки спавна
        generateRandomID = EditorGUILayout.Toggle("Случайный ID", generateRandomID);
        
        GUI.enabled = !generateRandomID;
        spawnPointID = EditorGUILayout.TextField("ID точки спавна", spawnPointID);
        GUI.enabled = true;
        
        EditorGUILayout.Space();
        
        // Основные настройки
        useExactHeight = EditorGUILayout.Toggle("Использовать точную высоту", useExactHeight);
        applyRotation = EditorGUILayout.Toggle("Применять вращение", applyRotation);
        
        EditorGUILayout.Space();
        
        // Настройки отображения
        GUILayout.Label("Отображение в редакторе", EditorStyles.boldLabel);
        gizmoColor = EditorGUILayout.ColorField("Цвет отображения", gizmoColor);
        gizmoSize = EditorGUILayout.Slider("Размер отображения", gizmoSize, 0.1f, 5f);
        showDirection = EditorGUILayout.Toggle("Показывать направление", showDirection);
        
        GUI.enabled = showDirection;
        directionLength = EditorGUILayout.Slider("Длина направления", directionLength, 0.5f, 10f);
        GUI.enabled = true;
        
        EditorGUILayout.Space();
        
        // Настройки менеджера
        GUILayout.Label("Настройки менеджера", EditorStyles.boldLabel);
        addToManager = EditorGUILayout.Toggle("Добавить в менеджер", addToManager);
        
        GUI.enabled = addToManager;
        createManager = EditorGUILayout.Toggle("Создать, если нет", createManager);
        setAsDefault = EditorGUILayout.Toggle("Установить по умолчанию", setAsDefault);
        GUI.enabled = true;
        
        EditorGUILayout.Space();
        
        // Кнопки
        if (GUILayout.Button("Создать точку спавна"))
        {
            CreateSpawnPoint();
        }
        
        EditorGUILayout.Space();
        
        if (GUILayout.Button("Создать SpawnPointManager"))
        {
            CreateSpawnPointManager();
        }
        
        EditorGUILayout.Space();
        
        if (GUILayout.Button("Обновить список точек спавна"))
        {
            RefreshSpawnPoints();
        }
    }
    
    private void CreateSpawnPoint()
    {
        // Генерируем ID, если нужно
        string finalID = generateRandomID ? 
            System.Guid.NewGuid().ToString().Substring(0, 8) : 
            spawnPointID;
        
        // Если ID пустой и не генерируется случайно, используем "SpawnPoint"
        if (string.IsNullOrEmpty(finalID))
        {
            finalID = "SpawnPoint";
        }
        
        // Создаем новый объект
        GameObject spawnPointObj = new GameObject("SpawnPoint_" + finalID);
        
        // Получаем позицию текущей сцены
        SceneView sceneView = SceneView.lastActiveSceneView;
        if (sceneView != null)
        {
            spawnPointObj.transform.position = sceneView.camera.transform.position + 
                                               sceneView.camera.transform.forward * 5f;
            spawnPointObj.transform.rotation = Quaternion.Euler(0, sceneView.camera.transform.eulerAngles.y, 0);
        }
        
        // Добавляем компонент точки спавна
        SpawnPoint spawnPoint = spawnPointObj.AddComponent<SpawnPoint>();
        
        // Устанавливаем параметры
        spawnPoint.spawnPointID = finalID;
        spawnPoint.useExactHeight = useExactHeight;
        spawnPoint.applyRotation = applyRotation;
        spawnPoint.gizmoColor = gizmoColor;
        spawnPoint.gizmoSize = gizmoSize;
        spawnPoint.showDirection = showDirection;
        spawnPoint.directionLength = directionLength;
        
        // Выбираем созданный объект
        Selection.activeGameObject = spawnPointObj;
        
        // Если нужно добавить в менеджер
        if (addToManager)
        {
            // Находим существующий менеджер или создаем новый
            SpawnPointManager manager = FindObjectOfType<SpawnPointManager>();
            
            if (manager == null && createManager)
            {
                manager = CreateSpawnPointManager();
            }
            
            if (manager != null)
            {
                // Обновляем список точек спавна
                if (!manager.spawnPoints.Contains(spawnPoint))
                {
                    manager.spawnPoints.Add(spawnPoint);
                }
                
                // Устанавливаем как точку по умолчанию, если нужно
                if (setAsDefault || manager.defaultSpawnPoint == null)
                {
                    manager.defaultSpawnPoint = spawnPoint;
                }
                
                // Сохраняем изменения в менеджере
                EditorUtility.SetDirty(manager);
            }
        }
        
        Debug.Log($"Создана точка спавна с ID: {finalID}");
    }
    
    private SpawnPointManager CreateSpawnPointManager()
    {
        // Ищем существующий менеджер
        SpawnPointManager manager = FindObjectOfType<SpawnPointManager>();
        
        // Если менеджер уже существует, просто возвращаем его
        if (manager != null)
        {
            Debug.Log("SpawnPointManager уже существует в сцене.");
            return manager;
        }
        
        // Создаем новый объект для менеджера
        GameObject managerObj = new GameObject("SpawnPointManager");
        
        // Добавляем компонент менеджера
        manager = managerObj.AddComponent<SpawnPointManager>();
        
        // Находим все существующие точки спавна
        SpawnPoint[] existingPoints = FindObjectsOfType<SpawnPoint>();
        
        // Добавляем их в менеджер
        foreach (SpawnPoint point in existingPoints)
        {
            if (!manager.spawnPoints.Contains(point))
            {
                manager.spawnPoints.Add(point);
            }
        }
        
        // Если есть точки и нет точки по умолчанию, устанавливаем первую
        if (existingPoints.Length > 0 && manager.defaultSpawnPoint == null)
        {
            manager.defaultSpawnPoint = existingPoints[0];
        }
        
        Debug.Log("Создан SpawnPointManager и добавлены существующие точки спавна.");
        
        return manager;
    }
    
    private void RefreshSpawnPoints()
    {
        // Ищем менеджер
        SpawnPointManager manager = FindObjectOfType<SpawnPointManager>();
        
        if (manager == null)
        {
            Debug.LogWarning("SpawnPointManager не найден в сцене.");
            return;
        }
        
        // Обновляем список точек
        manager.RefreshSpawnPoints();
        
        // Сохраняем изменения
        EditorUtility.SetDirty(manager);
        
        Debug.Log("Список точек спавна обновлен в SpawnPointManager.");
    }
} 