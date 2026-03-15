using UnityEngine;
using UnityEngine.SceneManagement;

public class SpawnPoint : MonoBehaviour
{
    [Tooltip("Уникальный идентификатор точки спавна")]
    public string spawnPointID = "";
    
    [Tooltip("Телепортировать ли объект на точную высоту точки или только X/Z")]
    public bool useExactHeight = true;
    
    [Tooltip("Применять ли вращение точки к объекту")]
    public bool applyRotation = true;
    
    [Header("Условия активации")]
    [Tooltip("Активировать только при возвращении из SwordQuest")]
    public bool onlyFromSwordQuest = false;
    
    [Tooltip("Название сцены SwordQuest")]
    public string swordQuestSceneName = "Sword_Quest_Scene";
    
    [Tooltip("Активировать автоматически при загрузке сцены")]
    public bool autoActivateOnSceneLoad = true;
    
    [Header("Параметры отображения в редакторе")]
    [Tooltip("Цвет отображения в редакторе")]
    public Color gizmoColor = new Color(0, 1, 0, 0.5f);
    
    [Tooltip("Размер отображения в редакторе")]
    public float gizmoSize = 1f;
    
    [Tooltip("Отображать направление")]
    public bool showDirection = true;
    
    [Tooltip("Длина линии направления")]
    public float directionLength = 2f;
    
    [Header("Настройки отладки")]
    [Tooltip("Показывать сообщения в консоли")]
    public bool debugMessages = true;
    
    [HideInInspector]
    public bool isActive = false;
    private string previousSceneName = "";
    
    private void Awake()
    {
        // Если ID не задан, генерируем случайный
        if (string.IsNullOrEmpty(spawnPointID))
        {
            spawnPointID = System.Guid.NewGuid().ToString().Substring(0, 8);
        }
        
        // Получаем название предыдущей сцены
        previousSceneName = PlayerPrefs.GetString("PreviousScene", "");
        
        if (debugMessages)
        {
            Debug.Log($"[SpawnPoint] 📍 Точка спавна: {spawnPointID}");
            Debug.Log($"[SpawnPoint] 🔄 Предыдущая сцена: {previousSceneName}");
            Debug.Log($"[SpawnPoint] ⚡ Только из SwordQuest: {onlyFromSwordQuest}");
        }
    }
    
    private void Start()
    {
        if (autoActivateOnSceneLoad)
        {
            CheckAndActivateSpawnPoint();
        }
    }
    
    /// <summary>
    /// Проверяет условия и активирует точку спавна
    /// </summary>
    public void CheckAndActivateSpawnPoint()
    {
        // Если не требуется активация только из SwordQuest
        if (!onlyFromSwordQuest)
        {
            isActive = true;
            if (debugMessages)
            {
                Debug.Log($"[SpawnPoint] ✅ Точка спавна активирована (без ограничений): {spawnPointID}");
            }
            return;
        }
        
        // Проверяем что игрок вернулся из SwordQuest
        if (previousSceneName == swordQuestSceneName)
        {
            isActive = true;
            if (debugMessages)
            {
                Debug.Log($"[SpawnPoint] ✅ Точка спавна активирована (возвращение из SwordQuest): {spawnPointID}");
            }
        }
        else
        {
            isActive = false;
            if (debugMessages)
            {
                Debug.Log($"[SpawnPoint] ❌ Точка спавна неактивна (не из SwordQuest): {spawnPointID}");
                Debug.Log($"[SpawnPoint] 📊 Предыдущая сцена: '{previousSceneName}', Ожидалась: '{swordQuestSceneName}'");
            }
        }
    }
    
    // Метод для телепортации объекта на точку спавна
    public void TeleportObjectToSpawnPoint(GameObject obj)
    {
        if (obj == null)
        {
            Debug.LogError("[SpawnPoint] Невозможно телепортировать NULL объект!");
            return;
        }
        
        // Проверяем активность точки спавна
        if (!isActive)
        {
            Debug.LogWarning($"[SpawnPoint] ❌ Точка спавна неактивна: {spawnPointID}");
            Debug.LogWarning($"[SpawnPoint] 📊 Текущее состояние: isActive={isActive}, onlyFromSwordQuest={onlyFromSwordQuest}");
            return;
        }
        
        // Получаем текущую позицию объекта
        Vector3 objPosition = obj.transform.position;
        
        // Создаем новую позицию
        Vector3 newPosition;
        
        if (useExactHeight)
        {
            // Используем точную позицию точки спавна
            newPosition = transform.position;
        }
        else
        {
            // Используем только X и Z координаты, сохраняя Y объекта
            newPosition = new Vector3(
                transform.position.x,
                objPosition.y,
                transform.position.z
            );
        }
        
        // Устанавливаем новую позицию
        obj.transform.position = newPosition;
        
        // Применяем вращение, если нужно
        if (applyRotation)
        {
            obj.transform.rotation = transform.rotation;
        }
        
        if (debugMessages)
        {
            Debug.Log($"[SpawnPoint] 🚀 Объект телепортирован на точку: {spawnPointID}");
            Debug.Log($"[SpawnPoint] 📍 Новая позиция: {newPosition}");
        }
    }
    
    /// <summary>
    /// Принудительно активировать точку спавна
    /// </summary>
    [ContextMenu("Принудительно активировать")]
    public void ForceActivate()
    {
        isActive = true;
        if (debugMessages)
        {
            Debug.Log($"[SpawnPoint] 🔧 Точка спавна принудительно активирована: {spawnPointID}");
        }
    }
    
    /// <summary>
    /// Принудительно деактивировать точку спавна
    /// </summary>
    [ContextMenu("Принудительно деактивировать")]
    public void ForceDeactivate()
    {
        isActive = false;
        if (debugMessages)
        {
            Debug.Log($"[SpawnPoint] 🔧 Точка спавна принудительно деактивирована: {spawnPointID}");
        }
    }
    
    /// <summary>
    /// Показать информацию о точке спавна
    /// </summary>
    [ContextMenu("Показать информацию")]
    public void ShowSpawnPointInfo()
    {
        if (debugMessages)
        {
            Debug.Log($"[SpawnPoint] 📊 Информация о точке спавна:");
            Debug.Log($"[SpawnPoint] 🆔 ID: {spawnPointID}");
            Debug.Log($"[SpawnPoint] 📍 Позиция: {transform.position}");
            Debug.Log($"[SpawnPoint] ⚡ Активна: {isActive}");
            Debug.Log($"[SpawnPoint] 🔒 Только из SwordQuest: {onlyFromSwordQuest}");
            Debug.Log($"[SpawnPoint] 🔄 Предыдущая сцена: {previousSceneName}");
            Debug.Log($"[SpawnPoint] 🎯 Ожидаемая сцена: {swordQuestSceneName}");
        }
    }
    
    // Отображение точки спавна в редакторе Unity
    private void OnDrawGizmos()
    {
        // Сохраняем предыдущий цвет Gizmo
        Color previousColor = Gizmos.color;
        
        // Устанавливаем цвет для отображения точки спавна
        // Если точка неактивна, используем красный цвет
        if (onlyFromSwordQuest && !isActive)
        {
            Gizmos.color = new Color(1, 0, 0, 0.5f); // Красный для неактивной
        }
        else
        {
            Gizmos.color = gizmoColor;
        }
        
        // Рисуем сферу в позиции точки спавна
        Gizmos.DrawSphere(transform.position, gizmoSize);
        
        // Если нужно отображать направление
        if (showDirection)
        {
            // Рисуем линию, показывающую направление точки спавна
            Gizmos.DrawRay(transform.position, transform.forward * directionLength);
            
            // Рисуем маленькую сферу на конце линии
            Gizmos.DrawSphere(transform.position + transform.forward * directionLength, gizmoSize * 0.3f);
        }
        
        // Восстанавливаем предыдущий цвет Gizmo
        Gizmos.color = previousColor;
    }
    
    // Отображение имени точки спавна в редакторе Unity
    private void OnDrawGizmosSelected()
    {
        // Отображаем ID точки спавна над ней в редакторе
        #if UNITY_EDITOR
        string status = onlyFromSwordQuest ? (isActive ? "✅" : "❌") : "🔓";
        UnityEditor.Handles.Label(transform.position + Vector3.up * (gizmoSize + 0.5f), $"SpawnPoint: {spawnPointID} {status}");
        #endif
    }
    
    // Метод для установки случайного ID точки спавна
    [ContextMenu("Генерировать новый ID")]
    public void GenerateNewID()
    {
        spawnPointID = System.Guid.NewGuid().ToString().Substring(0, 8);
        
        #if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
        #endif
    }
} 