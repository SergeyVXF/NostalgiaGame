using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DEDAskTrigger : MonoBehaviour
{
    [Header("Настройки сообщения")]
    [Tooltip("Содержание сообщения")]
    [SerializeField] private string messageContent = "дай поесть";
    [Tooltip("Длительность отображения сообщения (в секундах)")]
    [SerializeField] private float messageDuration = 2f;
    [Tooltip("Интервал между сообщениями (в секундах)")]
    [SerializeField] private float messageInterval = 3f;
    
    [Header("Аудио")]
    [Tooltip("Источник аудио для воспроизведения фразы")] 
    [SerializeField] private AudioSource audioSource;
    [Tooltip("Аудио-клип для воспроизведения")] 
    [SerializeField] private AudioClip askClip;
    
    private bool playerInZone = false;
    private float messageTimer = 0f;
    private float intervalTimer = 0f;
    
    private void Awake()
    {
        try
        {
            // Принудительно устанавливаем коллайдеры как триггеры
            SetAllCollidersAsTriggers();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[DEDAskTrigger] Ошибка в Awake: {e.Message}");
        }
    }
    
    private void OnEnable()
    {
        try
        {
            // Проверяем и устанавливаем коллайдеры как триггеры при каждой активации объекта
            SetAllCollidersAsTriggers();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[DEDAskTrigger] Ошибка в OnEnable: {e.Message}");
        }
    }
    
    private void Start()
    {
        try
        {
            // Если текст не назначен, попробуем найти его в сцене
            if (audioSource == null && askClip == null)
            {
                FindMessageText();
            }
            
            // Проверка, что все коллайдеры - триггеры
            SetAllCollidersAsTriggers();
            
            // Пробуем установить слой безопасно
            SetSafeLayer();
            
            // Убедимся, что сообщение скрыто в начале
            HideMessage();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[DEDAskTrigger] Ошибка в Start: {e.Message}");
        }
    }
    
    private void SetSafeLayer()
    {
        try
        {
            // Пробуем использовать существующие слои по порядку приоритета
            string[] layerOptions = new string[] { "Trigger", "Ignore Raycast", "UI" };
            
            foreach (string layerName in layerOptions)
            {
                int layerIndex = LayerMask.NameToLayer(layerName);
                if (layerIndex != -1)
                {
                    gameObject.layer = layerIndex;
                    Debug.Log($"[DEDAskTrigger] Установлен слой: {layerName}");
                    return;
                }
            }
            
            // Если ни один из слоев не найден, оставляем текущий
            Debug.LogWarning($"[DEDAskTrigger] Не найдены слои из списка приоритетов. Текущий слой: {LayerMask.LayerToName(gameObject.layer)}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[DEDAskTrigger] Ошибка при установке слоя: {e.Message}");
        }
    }
    
    private void SetAllCollidersAsTriggers()
    {
        try
        {
            // Получаем все коллайдеры, включая дочерние
            Collider[] allColliders = GetComponentsInChildren<Collider>(true);
            
            if (allColliders == null || allColliders.Length == 0)
            {
                Debug.LogWarning("[DEDAskTrigger] На объекте нет коллайдеров! Добавляем BoxCollider.");
                // Автоматически добавляем BoxCollider, если нет коллайдеров
                BoxCollider newCollider = gameObject.AddComponent<BoxCollider>();
                if (newCollider != null)
                {
                    newCollider.isTrigger = true;
                    newCollider.size = new Vector3(3, 2, 3); // Размер по умолчанию
                    Debug.Log("[DEDAskTrigger] Автоматически добавлен BoxCollider с isTrigger = true");
                }
            }
            else
            {
                // Устанавливаем все коллайдеры как триггеры
                foreach (Collider col in allColliders)
                {
                    if (col != null && !col.isTrigger)
                    {
                        col.isTrigger = true;
                        Debug.Log($"[DEDAskTrigger] Коллайдер {col.GetType().Name} на объекте {col.gameObject.name} установлен как триггер");
                    }
                }
            }
            
            // Отключаем компоненты Rigidbody, если они есть, чтобы избежать физических взаимодействий
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null && !rb.isKinematic)
            {
                rb.isKinematic = true;
                Debug.Log("[DEDAskTrigger] Найден Rigidbody, установлен как Kinematic для избежания физических взаимодействий");
            }
            
            // Проверяем наличие активных физических коллайдеров родителя
            Transform parentTransform = transform.parent;
            if (parentTransform != null)
            {
                Collider parentCollider = parentTransform.GetComponent<Collider>();
                if (parentCollider != null && !parentCollider.isTrigger)
                {
                    Debug.LogWarning($"[DEDAskTrigger] Родительский объект {parentTransform.name} имеет физический коллайдер! Это может вызывать столкновения.");
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[DEDAskTrigger] Ошибка при настройке коллайдеров: {e.Message}");
        }
    }
    
    private void FindMessageText()
    {
        Debug.Log("[DEDAskTrigger] FindMessageText вызван, но текстовые компоненты больше не используются. Используется только аудио.");
    }
    
    private void Update()
    {
        try
        {
            // Если игрок в зоне, обновляем таймеры
            if (playerInZone)
            {
                if (intervalTimer > 0)
                {
                    intervalTimer -= Time.deltaTime;
                    
                    // Если интервал закончился, показываем сообщение снова
                    if (intervalTimer <= 0)
                    {
                        ShowMessage();
                    }
                }
                
                if (messageTimer > 0)
                {
                    messageTimer -= Time.deltaTime;
                    
                    // Если время отображения закончилось, скрываем сообщение
                    if (messageTimer <= 0)
                    {
                        HideMessage();
                        // Запускаем отсчет интервала до следующего сообщения
                        intervalTimer = messageInterval;
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[DEDAskTrigger] Ошибка в Update: {e.Message}");
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        try
        {
            // Проверяем, что вошел игрок
            if (other != null && other.CompareTag("Player"))
            {
                playerInZone = true;
                // Сразу показываем сообщение
                ShowMessage();
                Debug.Log("[DEDAskTrigger] Игрок вошел в зону 'дай поесть'");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[DEDAskTrigger] Ошибка в OnTriggerEnter: {e.Message}");
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        try
        {
            // Проверяем, что вышел игрок
            if (other != null && other.CompareTag("Player"))
            {
                playerInZone = false;
                // Скрываем сообщение при выходе из зоны
                HideMessage();
                Debug.Log("[DEDAskTrigger] Игрок вышел из зоны 'дай поесть'");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[DEDAskTrigger] Ошибка в OnTriggerExit: {e.Message}");
        }
    }
    
    private void ShowMessage()
    {
        try
        {
            if (audioSource != null && askClip != null)
            {
                audioSource.clip = askClip;
                audioSource.Play();
            }
            else
            {
                Debug.LogWarning("[DEDAskTrigger] Не назначен AudioSource или AudioClip!");
            }
            messageTimer = messageDuration;
            intervalTimer = 0f;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[DEDAskTrigger] Ошибка при воспроизведении аудио: {e.Message}");
        }
    }
    
    private void HideMessage()
    {
        try
        {
            if (audioSource != null && audioSource.isPlaying)
            {
                audioSource.Stop();
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[DEDAskTrigger] Ошибка при остановке аудио: {e.Message}");
        }
    }
    
    // Для редактора Unity - визуализация зоны триггера
    private void OnDrawGizmos()
    {
        try
        {
            Gizmos.color = new Color(0, 1, 0, 0.3f); // Полупрозрачный зеленый
            Collider col = GetComponent<Collider>();
            
            if (col != null)
            {
                if (col is BoxCollider)
                {
                    BoxCollider boxCol = col as BoxCollider;
                    Gizmos.matrix = transform.localToWorldMatrix;
                    Gizmos.DrawCube(boxCol.center, boxCol.size);
                }
                else if (col is SphereCollider)
                {
                    SphereCollider sphereCol = col as SphereCollider;
                    Gizmos.DrawSphere(transform.position + sphereCol.center, sphereCol.radius);
                }
            }
            else
            {
                // Если коллайдера нет, рисуем условную область
                Gizmos.DrawSphere(transform.position, 1.5f);
            }
        }
        catch (System.Exception)
        {
            // Ошибки в OnDrawGizmos не выводим, так как они будут спамить консоль в редакторе
        }
    }
} 