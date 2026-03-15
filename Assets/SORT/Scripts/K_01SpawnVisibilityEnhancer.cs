using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(K_01FixedSpawner))]
public class K_01SpawnVisibilityEnhancer : MonoBehaviour
{
    [Header("Настройки видимости")]
    [SerializeField] private bool autoEnhanceVisibility = true;
    [SerializeField] private LayerMask targetLayers;
    [SerializeField] private float checkRadius = 50f;
    [SerializeField] private float updateInterval = 1.0f;
    
    [Header("Расширенные настройки")]
    [SerializeField] private bool highlightOnPlayerLookAt = true;
    [SerializeField] private float lookAtAngleThreshold = 25f;
    [SerializeField] private Transform playerCamera;
    [SerializeField] private float lookRayDistance = 30f;

    // Ссылки на компоненты
    private K_01FixedSpawner spawner;
    private float nextUpdateTime = 0f;
    
    // Список активных объектов
    private List<GameObject> activeObjects = new List<GameObject>();

    private void Awake()
    {
        spawner = GetComponent<K_01FixedSpawner>();
        
        // Если камера игрока не задана, попробуем найти её
        if (playerCamera == null)
        {
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                playerCamera = mainCamera.transform;
            }
        }
    }

    private void Start()
    {
        // Подписываемся на события спавна, если они есть
        if (spawner != null)
        {
            // Предполагается, что у K_01FixedSpawner есть событие OnObjectSpawned
            // Если его нет, нужно будет регулярно сканировать объекты вокруг
            // Пример кода ниже предполагает, что такого события нет
        }
    }

    private void Update()
    {
        if (Time.time >= nextUpdateTime)
        {
            nextUpdateTime = Time.time + updateInterval;
            
            if (autoEnhanceVisibility)
            {
                ScanAndEnhanceObjects();
            }
            
            if (highlightOnPlayerLookAt && playerCamera != null)
            {
                CheckPlayerLookAt();
            }
        }
    }

    private void ScanAndEnhanceObjects()
    {
        // Очищаем список от уничтоженных объектов
        activeObjects.RemoveAll(obj => obj == null);
        
        // Сканируем объекты вокруг
        Collider[] colliders = Physics.OverlapSphere(transform.position, checkRadius, targetLayers);
        
        foreach (Collider collider in colliders)
        {
            GameObject obj = collider.gameObject;
            
            // Проверяем, есть ли уже компонент видимости
            if (!obj.GetComponent<K_01EnhancedVisibility>() && !activeObjects.Contains(obj))
            {
                // Добавляем компонент улучшенной видимости
                K_01EnhancedVisibility visibility = obj.AddComponent<K_01EnhancedVisibility>();
                
                // Добавляем в список активных объектов
                activeObjects.Add(obj);
                
                // Можно настроить параметры компонента здесь, если это необходимо
                // visibility.customSetting = value;
                
                Debug.Log($"Добавлен эффект видимости к объекту: {obj.name}");
            }
        }
    }

    private void CheckPlayerLookAt()
    {
        if (activeObjects.Count == 0 || playerCamera == null)
            return;
            
        Ray lookRay = new Ray(playerCamera.position, playerCamera.forward);
        RaycastHit hit;
        
        if (Physics.Raycast(lookRay, out hit, lookRayDistance))
        {
            // Проверяем, есть ли у объекта компонент улучшенной видимости
            K_01EnhancedVisibility visibility = hit.collider.GetComponent<K_01EnhancedVisibility>();
            
            if (visibility != null)
            {
                // Вызываем метод подсветки
                visibility.HighlightTemporarily(1.0f, 2.0f);
            }
        }
        
        // Проверяем также все объекты в поле зрения
        foreach (GameObject obj in activeObjects)
        {
            if (obj == null)
                continue;
                
            Vector3 directionToObject = obj.transform.position - playerCamera.position;
            float angle = Vector3.Angle(playerCamera.forward, directionToObject);
            
            // Если объект находится в пределах угла обзора
            if (angle < lookAtAngleThreshold)
            {
                // Проверяем, есть ли препятствия между камерой и объектом
                Ray objRay = new Ray(playerCamera.position, directionToObject.normalized);
                RaycastHit objHit;
                
                if (Physics.Raycast(objRay, out objHit, directionToObject.magnitude))
                {
                    // Если луч попал в этот же объект, значит препятствий нет
                    if (objHit.collider.gameObject == obj)
                    {
                        K_01EnhancedVisibility visibility = obj.GetComponent<K_01EnhancedVisibility>();
                        if (visibility != null)
                        {
                            // Используем меньшую интенсивность, так как это просто объект в поле зрения
                            visibility.HighlightTemporarily(0.5f, 1.5f);
                        }
                    }
                }
            }
        }
    }

    // Публичный метод для обновления объектов извне
    public void UpdateVisibilityEffects()
    {
        ScanAndEnhanceObjects();
    }

    // Метод для очистки списка объектов при необходимости
    public void ClearEnhancedObjects()
    {
        foreach (GameObject obj in activeObjects)
        {
            if (obj != null)
            {
                K_01EnhancedVisibility visibility = obj.GetComponent<K_01EnhancedVisibility>();
                if (visibility != null)
                {
                    Destroy(visibility);
                }
            }
        }
        
        activeObjects.Clear();
    }

    private void OnDrawGizmosSelected()
    {
        // Отображаем радиус действия в редакторе
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, checkRadius);
        
        // Отображаем луч взгляда
        if (playerCamera != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(playerCamera.position, playerCamera.forward * lookRayDistance);
            
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.2f);
            // Рисуем конус обзора
            DrawViewCone(playerCamera.position, playerCamera.forward, lookAtAngleThreshold, lookRayDistance);
        }
    }
    
    private void DrawViewCone(Vector3 origin, Vector3 direction, float angle, float distance)
    {
        // Рисуем конус обзора (упрощенно)
        float halfAngle = angle * 0.5f * Mathf.Deg2Rad;
        float radius = Mathf.Tan(halfAngle) * distance;
        
        Vector3 forward = direction.normalized * distance;
        Vector3 up = Vector3.up;
        
        // Если forward и up параллельны, используем другой вектор
        if (Mathf.Abs(Vector3.Dot(forward.normalized, up)) > 0.99f)
        {
            up = Vector3.right;
        }
        
        Vector3 right = Vector3.Cross(forward, up).normalized;
        up = Vector3.Cross(right, forward).normalized;
        
        // Рисуем окружность на конце конуса
        int segments = 20;
        for (int i = 0; i < segments; i++)
        {
            float angle1 = (float)i / segments * 2f * Mathf.PI;
            float angle2 = (float)(i + 1) / segments * 2f * Mathf.PI;
            
            Vector3 p1 = origin + forward + (right * Mathf.Cos(angle1) + up * Mathf.Sin(angle1)) * radius;
            Vector3 p2 = origin + forward + (right * Mathf.Cos(angle2) + up * Mathf.Sin(angle2)) * radius;
            
            Gizmos.DrawLine(origin, p1);
            Gizmos.DrawLine(p1, p2);
        }
    }
} 