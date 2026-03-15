using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Управляет коллайдером оружия при взаимодействиях
/// Автоматически отключает коллайдер при взаимодействии с trigger объектами
/// </summary>
public class WeaponColliderManager : MonoBehaviour
{
    [Header("Настройки оружия")]
    [Tooltip("Коллайдеры оружия для отключения")]
    public Collider[] weaponColliders;
    
    [Tooltip("Автоматически найти все коллайдеры оружия")]
    public bool autoFindColliders = true;
    
    [Header("Настройки взаимодействий")]
    [Tooltip("Дистанция для проверки trigger объектов")]
    public float checkDistance = 3f;
    
    [Tooltip("Слои для поиска trigger объектов")]
    public LayerMask triggerLayers = -1;
    
    [Header("Настройки отладки")]
    [Tooltip("Показывать сообщения в консоли")]
    public bool debugMessages = true;
    
    private GameObject player;
    private bool collidersDisabled = false;
    private List<bool> originalColliderStates = new List<bool>();
    
    void Start()
    {
        // Ищем игрока
        player = GameObject.FindGameObjectWithTag("Player");
        
        // Автоматически находим коллайдеры если включено
        if (autoFindColliders)
        {
            FindWeaponColliders();
        }
        
        // Сохраняем исходные состояния коллайдеров
        SaveColliderStates();
        
        if (debugMessages)
        {
            Debug.Log($"[WeaponColliderManager] ✅ Система готова: {gameObject.name}");
            Debug.Log($"[WeaponColliderManager] 🔧 Коллайдеров оружия: {weaponColliders.Length}");
        }
    }
    
    void Update()
    {
        // Проверяем наличие trigger объектов рядом
        CheckForTriggerObjects();
    }
    
    void FindWeaponColliders()
    {
        // Получаем все коллайдеры оружия
        Collider[] allColliders = GetComponentsInChildren<Collider>();
        List<Collider> weaponColliderList = new List<Collider>();
        
        foreach (Collider col in allColliders)
        {
            // Исключаем коллайдеры которые не должны отключаться
            if (col.gameObject != gameObject && !col.isTrigger)
            {
                weaponColliderList.Add(col);
            }
        }
        
        weaponColliders = weaponColliderList.ToArray();
        
        if (debugMessages)
        {
            Debug.Log($"[WeaponColliderManager] 🔍 Найдено коллайдеров оружия: {weaponColliders.Length}");
        }
    }
    
    void SaveColliderStates()
    {
        originalColliderStates.Clear();
        
        foreach (Collider col in weaponColliders)
        {
            if (col != null)
            {
                originalColliderStates.Add(col.enabled);
            }
        }
    }
    
    void CheckForTriggerObjects()
    {
        if (player == null) return;
        
        // Ищем trigger объекты в радиусе
        Collider[] nearbyColliders = Physics.OverlapSphere(player.transform.position, checkDistance, triggerLayers);
        
        bool hasTriggerObjects = false;
        
        foreach (Collider col in nearbyColliders)
        {
            // Проверяем что это trigger объект с vGenericAction
            if (col.isTrigger && col.GetComponent("vGenericAction") != null)
            {
                hasTriggerObjects = true;
                
                if (debugMessages)
                {
                    Debug.Log($"[WeaponColliderManager] 🎯 Найден vGenericAction: {col.gameObject.name}");
                }
                break;
            }
        }
        
        // Управляем коллайдерами оружия
        if (hasTriggerObjects && !collidersDisabled)
        {
            DisableWeaponColliders();
        }
        else if (!hasTriggerObjects && collidersDisabled)
        {
            EnableWeaponColliders();
        }
        
        // Дополнительная отладка
        if (debugMessages && hasTriggerObjects)
        {
            Debug.Log($"[WeaponColliderManager] 📍 Игрок в зоне vGenericAction, коллайдеры: {(collidersDisabled ? "отключены" : "включены")}");
        }
    }
    
    void DisableWeaponColliders()
    {
        if (collidersDisabled) return;
        
        for (int i = 0; i < weaponColliders.Length; i++)
        {
            if (weaponColliders[i] != null)
            {
                weaponColliders[i].enabled = false;
            }
        }
        
        collidersDisabled = true;
        
        if (debugMessages)
        {
            Debug.Log($"[WeaponColliderManager] 🔧 Коллайдеры оружия отключены");
        }
    }
    
    void EnableWeaponColliders()
    {
        if (!collidersDisabled) return;
        
        for (int i = 0; i < weaponColliders.Length; i++)
        {
            if (weaponColliders[i] != null && i < originalColliderStates.Count)
            {
                weaponColliders[i].enabled = originalColliderStates[i];
            }
        }
        
        collidersDisabled = false;
        
        if (debugMessages)
        {
            Debug.Log($"[WeaponColliderManager] 🔧 Коллайдеры оружия включены");
        }
    }
    
    /// <summary>
    /// Принудительно отключить коллайдеры оружия
    /// </summary>
    [ContextMenu("Отключить коллайдеры оружия")]
    public void ForceDisableColliders()
    {
        DisableWeaponColliders();
    }
    
    /// <summary>
    /// Принудительно включить коллайдеры оружия
    /// </summary>
    [ContextMenu("Включить коллайдеры оружия")]
    public void ForceEnableColliders()
    {
        EnableWeaponColliders();
    }
    
    /// <summary>
    /// Показать информацию о коллайдерах
    /// </summary>
    [ContextMenu("Показать информацию")]
    public void ShowColliderInfo()
    {
        if (debugMessages)
        {
            Debug.Log($"[WeaponColliderManager] 📊 Информация о коллайдерах:");
            Debug.Log($"[WeaponColliderManager] 📍 Оружие: {gameObject.name}");
            Debug.Log($"[WeaponColliderManager] 🔧 Всего коллайдеров: {weaponColliders.Length}");
            Debug.Log($"[WeaponColliderManager] ⚡ Состояние: {(collidersDisabled ? "Отключены" : "Включены")}");
            
            for (int i = 0; i < weaponColliders.Length; i++)
            {
                if (weaponColliders[i] != null)
                {
                    string status = weaponColliders[i].enabled ? "✅" : "❌";
                    Debug.Log($"[WeaponColliderManager] {status} Коллайдер {i + 1}: {weaponColliders[i].name}");
                }
            }
        }
    }
    
    void OnDrawGizmosSelected()
    {
        if (player != null)
        {
            // Показываем зону проверки trigger объектов
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(player.transform.position, checkDistance);
        }
    }
}
