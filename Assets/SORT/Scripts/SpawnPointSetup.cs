using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class SpawnPointSetup : MonoBehaviour
{
    [Header("Настройки создания точки спавна")]
    [Tooltip("Имя для создаваемой точки спавна")]
    public string spawnPointName = "PlayerSpawnPoint";
    
    [Tooltip("Цвет для визуализации точки спавна")]
    public Color gizmoColor = Color.green;
    
    [Tooltip("Размер для визуализации точки спавна")]
    public float gizmoSize = 0.5f;
    
    // Метод для отладки - визуализация точки спавна в редакторе
    private void OnDrawGizmos()
    {
        Gizmos.color = gizmoColor;
        Gizmos.DrawSphere(transform.position, gizmoSize);
        Gizmos.DrawRay(transform.position, transform.forward * 2f);
    }
    
    #if UNITY_EDITOR
    [ContextMenu("Создать точку спавна здесь")]
    private void CreateSpawnPoint()
    {
        // Создаем новый объект для точки спавна
        GameObject spawnPoint = new GameObject(spawnPointName);
        
        // Устанавливаем позицию и поворот
        spawnPoint.transform.position = transform.position;
        spawnPoint.transform.rotation = transform.rotation;
        
        // Добавляем компонент для визуализации в редакторе
        var setup = spawnPoint.AddComponent<SpawnPointSetup>();
        setup.gizmoColor = this.gizmoColor;
        setup.gizmoSize = this.gizmoSize;
        
        // Находим или создаем объект с PlayerSpawnManager
        GameObject managerObject = GameObject.Find("PlayerSpawnManager");
        
        if (managerObject == null)
        {
            managerObject = new GameObject("PlayerSpawnManager");
            Debug.Log("[SpawnPointSetup] Создан новый объект PlayerSpawnManager");
        }
        
        // Добавляем компонент PlayerSpawnManager, если его нет
        PlayerSpawnManager manager = managerObject.GetComponent<PlayerSpawnManager>();
        if (manager == null)
        {
            manager = managerObject.AddComponent<PlayerSpawnManager>();
            Debug.Log("[SpawnPointSetup] Добавлен компонент PlayerSpawnManager");
        }
        
        // Устанавливаем точку спавна в менеджере через SerializedObject
        SerializedObject serializedObject = new SerializedObject(manager);
        SerializedProperty spawnPointProperty = serializedObject.FindProperty("spawnPoint");
        spawnPointProperty.objectReferenceValue = spawnPoint.transform;
        serializedObject.ApplyModifiedProperties();
        
        Debug.Log($"[SpawnPointSetup] Точка спавна создана и установлена в PlayerSpawnManager: {spawnPoint.name}");
        
        // Выделяем созданный объект
        Selection.activeGameObject = spawnPoint;
    }
    #endif
} 