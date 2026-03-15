using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

public class TriggerBackEditorWindow : EditorWindow
{
    private string targetSceneName = "AG2";
    private bool useSceneFade = true;
    private Vector3 triggerSize = new Vector3(5f, 3f, 1f);
    private Color triggerColor = new Color(1f, 0.5f, 0f, 0.5f); // Оранжевый полупрозрачный
    
    private bool createAsPrefab = true;
    private bool showDebugMessages = false;

    // Новые приватные поля
    private string targetSpawnPointID = "DefaultSpawnPoint";
    private Vector3 fallbackPosition = Vector3.zero;
    private Vector3 fallbackRotation = Vector3.zero;

    [MenuItem("Утилиты/Создатель триггеров возврата")]
    public static void ShowWindow()
    {
        GetWindow<TriggerBackEditorWindow>("Создатель триггеров возврата");
    }

    private void OnGUI()
    {
        GUILayout.Label("Настройки триггера возврата", EditorStyles.boldLabel);
        
        EditorGUILayout.Space();
        
        targetSceneName = EditorGUILayout.TextField("Целевая сцена", targetSceneName);
        useSceneFade = EditorGUILayout.Toggle("Использовать затемнение", useSceneFade);
        showDebugMessages = EditorGUILayout.Toggle("Отладочные сообщения", showDebugMessages);
        
        EditorGUILayout.Space();
        GUILayout.Label("Настройки спавн-поинта", EditorStyles.boldLabel);
        
        // Выпадающий список существующих спавн-поинтов
        string[] spawnPointIDs = GetSpawnPointIDs();
        int selectedIndex = 0;
        
        // Находим индекс текущего спавн-поинта в массиве
        for (int i = 0; i < spawnPointIDs.Length; i++)
        {
            if (spawnPointIDs[i] == targetSpawnPointID)
            {
                selectedIndex = i;
                break;
            }
        }
        
        // Показываем выпадающий список
        selectedIndex = EditorGUILayout.Popup("Выбрать спавн-поинт", selectedIndex, spawnPointIDs);
        
        // Обновляем ID спавн-поинта
        if (spawnPointIDs.Length > 0)
        {
            targetSpawnPointID = spawnPointIDs[selectedIndex];
        }
        
        // Поле для ручного ввода ID
        targetSpawnPointID = EditorGUILayout.TextField("ID спавн-поинта", targetSpawnPointID);
        
        // Fallback позиция и поворот
        fallbackPosition = EditorGUILayout.Vector3Field("Запасная позиция", fallbackPosition);
        fallbackRotation = EditorGUILayout.Vector3Field("Запасный поворот", fallbackRotation);
        
        // Кнопка для использования текущей позиции вида
        if (GUILayout.Button("Использовать позицию вида"))
        {
            UseSceneViewPosition();
        }
        
        EditorGUILayout.Space();
        
        GUILayout.Label("Настройки визуализации", EditorStyles.boldLabel);
        triggerSize = EditorGUILayout.Vector3Field("Размер триггера", triggerSize);
        triggerColor = EditorGUILayout.ColorField("Цвет триггера", triggerColor);
        
        EditorGUILayout.Space();
        
        createAsPrefab = EditorGUILayout.Toggle("Создать как префаб", createAsPrefab);
        
        EditorGUILayout.Space();
        
        if (GUILayout.Button("Создать триггер возврата"))
        {
            if (createAsPrefab)
            {
                CreateTriggerBackPrefab();
            }
            else
            {
                CreateTriggerBack();
            }
        }
        
        EditorGUILayout.Space();
        
        if (GUILayout.Button("Обновить префаб триггера"))
        {
            UpdateTriggerBackPrefab();
        }
        
        EditorGUILayout.Space();
        
        if (GUILayout.Button("Создать/открыть окно спавн-поинтов"))
        {
            OpenSpawnPointWindow();
        }
    }
    
    private void CreateTriggerBack()
    {
        // Создаем новый игровой объект
        GameObject triggerObj = new GameObject("TriggerBack_" + targetSceneName);
        
        // Добавляем BoxCollider и настраиваем его как триггер
        BoxCollider boxCollider = triggerObj.AddComponent<BoxCollider>();
        boxCollider.isTrigger = true;
        boxCollider.size = triggerSize;
        
        // Добавляем компонент TriggerBack и настраиваем его
        TriggerBack triggerBack = triggerObj.AddComponent<TriggerBack>();
        triggerBack.targetSceneName = targetSceneName;
        triggerBack.useSceneFade = useSceneFade;
        triggerBack.showDebugMessages = showDebugMessages;
        
        // Добавляем настройки спавн-поинта
        triggerBack.spawnPointName = targetSpawnPointID;
        triggerBack.fallbackSpawnPosition = fallbackPosition;
        triggerBack.fallbackSpawnRotation = fallbackRotation;
        
        // Создаем визуальное представление
        GameObject visualObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visualObj.transform.SetParent(triggerObj.transform);
        visualObj.transform.localPosition = Vector3.zero;
        visualObj.transform.localScale = triggerSize;
        
        // Настраиваем материал для визуализации
        MeshRenderer renderer = visualObj.GetComponent<MeshRenderer>();
        Material material = new Material(Shader.Find("Standard"));
        material.color = triggerColor;
        renderer.material = material;
        
        // Убираем коллайдер с визуального объекта
        DestroyImmediate(visualObj.GetComponent<Collider>());
        
        // Делаем визуальный объект видимым только в редакторе
        visualObj.name = "TriggerVisual_EditorOnly";
        
        // Размещаем в текущей сцене
        Selection.activeGameObject = triggerObj;
        SceneView.lastActiveSceneView.FrameSelected();
        
        Debug.Log("Триггер возврата для сцены " + targetSceneName + " создан.");
    }
    
    private void CreateTriggerBackPrefab()
    {
        // Создаем объект
        GameObject triggerObj = new GameObject("TriggerBack");
        
        // Добавляем BoxCollider и настраиваем его как триггер
        BoxCollider boxCollider = triggerObj.AddComponent<BoxCollider>();
        boxCollider.isTrigger = true;
        boxCollider.size = triggerSize;
        
        // Добавляем компонент TriggerBack и настраиваем его
        TriggerBack triggerBack = triggerObj.AddComponent<TriggerBack>();
        triggerBack.targetSceneName = targetSceneName;
        triggerBack.useSceneFade = useSceneFade;
        triggerBack.showDebugMessages = showDebugMessages;
        
        // Добавляем настройки спавн-поинта
        triggerBack.spawnPointName = targetSpawnPointID;
        triggerBack.fallbackSpawnPosition = fallbackPosition;
        triggerBack.fallbackSpawnRotation = fallbackRotation;
        
        // Создаем визуальное представление
        GameObject visualObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visualObj.transform.SetParent(triggerObj.transform);
        visualObj.transform.localPosition = Vector3.zero;
        visualObj.transform.localScale = triggerSize;
        
        // Настраиваем материал для визуализации
        MeshRenderer renderer = visualObj.GetComponent<MeshRenderer>();
        Material material = new Material(Shader.Find("Standard"));
        material.color = triggerColor;
        renderer.material = material;
        
        // Убираем коллайдер с визуального объекта
        DestroyImmediate(visualObj.GetComponent<Collider>());
        
        // Делаем визуальный объект видимым только в редакторе
        visualObj.name = "TriggerVisual_EditorOnly";
        
        // Создаем папку для префаба, если её нет
        if (!Directory.Exists("Assets/Prefabs"))
        {
            AssetDatabase.CreateFolder("Assets", "Prefabs");
            AssetDatabase.Refresh();
        }
        
        // Создаем префаб
        string prefabPath = "Assets/Prefabs/TriggerBack.prefab";
        
        // Проверяем, существует ли уже префаб
        bool prefabExists = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null;
        
        GameObject prefab;
        if (prefabExists)
        {
            // Обновляем существующий префаб
            prefab = PrefabUtility.SaveAsPrefabAsset(triggerObj, prefabPath);
            Debug.Log("Существующий префаб триггера возврата обновлен.");
        }
        else
        {
            // Создаем новый префаб
            prefab = PrefabUtility.SaveAsPrefabAsset(triggerObj, prefabPath);
            Debug.Log("Создан новый префаб триггера возврата.");
        }
        
        // Удаляем временный объект из сцены
        DestroyImmediate(triggerObj);
        
        // Создаем экземпляр префаба в сцене
        GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        Selection.activeGameObject = instance;
        SceneView.lastActiveSceneView.FrameSelected();
        
        Debug.Log("Триггер возврата для сцены " + targetSceneName + " создан и добавлен в сцену.");
    }
    
    private void UpdateTriggerBackPrefab()
    {
        string prefabPath = "Assets/Prefabs/TriggerBack.prefab";
        GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        
        if (prefabAsset == null)
        {
            Debug.LogError("Префаб TriggerBack не найден. Сначала создайте префаб.");
            return;
        }
        
        // Создаем временный объект на основе префаба
        GameObject tempObj = PrefabUtility.InstantiatePrefab(prefabAsset) as GameObject;
        
        // Обновляем настройки
        TriggerBack triggerBack = tempObj.GetComponent<TriggerBack>();
        if (triggerBack != null)
        {
            triggerBack.targetSceneName = targetSceneName;
            triggerBack.useSceneFade = useSceneFade;
            triggerBack.showDebugMessages = showDebugMessages;
            
            // Добавляем настройки спавн-поинта
            triggerBack.spawnPointName = targetSpawnPointID;
            triggerBack.fallbackSpawnPosition = fallbackPosition;
            triggerBack.fallbackSpawnRotation = fallbackRotation;
        }
        
        // Обновляем размер коллайдера
        BoxCollider boxCollider = tempObj.GetComponent<BoxCollider>();
        if (boxCollider != null)
        {
            boxCollider.size = triggerSize;
        }
        
        // Обновляем визуальное представление
        Transform visualTransform = tempObj.transform.Find("TriggerVisual_EditorOnly");
        if (visualTransform != null)
        {
            visualTransform.localScale = triggerSize;
            
            MeshRenderer renderer = visualTransform.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial.color = triggerColor;
            }
        }
        
        // Сохраняем изменения в префабе
        PrefabUtility.SaveAsPrefabAsset(tempObj, prefabPath);
        
        // Удаляем временный объект
        DestroyImmediate(tempObj);
        
        Debug.Log("Префаб триггера возврата успешно обновлен.");
    }

    // Метод для получения списка ID спавн-поинтов
    private string[] GetSpawnPointIDs()
    {
        SpawnPoint[] spawnPoints = FindObjectsOfType<SpawnPoint>();
        
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            return new string[] { "DefaultSpawnPoint" };
        }
        
        List<string> spawnPointIDs = new List<string>();
        
        foreach (SpawnPoint point in spawnPoints)
        {
            spawnPointIDs.Add(point.spawnPointID);
        }
        
        return spawnPointIDs.ToArray();
    }

    // Метод для использования текущей позиции вида
    private void UseSceneViewPosition()
    {
        SceneView sceneView = SceneView.lastActiveSceneView;
        if (sceneView != null)
        {
            fallbackPosition = sceneView.camera.transform.position + sceneView.camera.transform.forward * 5f;
            fallbackRotation = new Vector3(0, sceneView.camera.transform.eulerAngles.y, 0);
        }
    }

    // Метод для открытия окна создания спавн-поинтов
    private void OpenSpawnPointWindow()
    {
        SpawnPointCreatorWindow window = EditorWindow.GetWindow<SpawnPointCreatorWindow>();
        window.Show();
    }
} 