using UnityEngine;

/// <summary>
/// Простое решение проблемы с коллайдером оружия и vGenericAction
/// </summary>
public class SimpleWeaponColliderFix : MonoBehaviour
{
    [Header("Настройки")]
    [Tooltip("Автоматически найти коллайдеры оружия")]
    public bool autoFindColliders = true;
    
    [Tooltip("Дистанция проверки vGenericAction объектов")]
    public float checkRadius = 2f;
    
    [Tooltip("Показывать сообщения")]
    public bool debugMessages = true;
    
    private Collider[] weaponColliders;
    private bool[] originalStates;
    private bool collidersDisabled = false;
    
    void Start()
    {
        if (autoFindColliders)
        {
            FindWeaponColliders();
        }
        
        if (debugMessages)
        {
            Debug.Log($"[SimpleWeaponColliderFix] ✅ Система готова для {gameObject.name}");
        }
    }
    
    void Update()
    {
        CheckForGenericAction();
    }
    
    void FindWeaponColliders()
    {
        // Находим все коллайдеры оружия (кроме триггеров)
        Collider[] allColliders = GetComponentsInChildren<Collider>();
        System.Collections.Generic.List<Collider> weaponList = new System.Collections.Generic.List<Collider>();
        
        foreach (Collider col in allColliders)
        {
            if (!col.isTrigger && col.gameObject != gameObject)
            {
                weaponList.Add(col);
            }
        }
        
        weaponColliders = weaponList.ToArray();
        originalStates = new bool[weaponColliders.Length];
        
        // Сохраняем исходные состояния
        for (int i = 0; i < weaponColliders.Length; i++)
        {
            if (weaponColliders[i] != null)
            {
                originalStates[i] = weaponColliders[i].enabled;
            }
        }
        
        if (debugMessages)
        {
            Debug.Log($"[SimpleWeaponColliderFix] 🔍 Найдено коллайдеров оружия: {weaponColliders.Length}");
        }
    }
    
    void CheckForGenericAction()
    {
        // Ищем vGenericAction объекты в радиусе
        Collider[] nearbyColliders = Physics.OverlapSphere(transform.position, checkRadius);
        
        bool hasGenericAction = false;
        
        foreach (Collider col in nearbyColliders)
        {
            if (col.isTrigger && col.GetComponent("vGenericAction") != null)
            {
                hasGenericAction = true;
                break;
            }
        }
        
        // Управляем коллайдерами
        if (hasGenericAction && !collidersDisabled)
        {
            DisableColliders();
        }
        else if (!hasGenericAction && collidersDisabled)
        {
            EnableColliders();
        }
    }
    
    void DisableColliders()
    {
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
            Debug.Log($"[SimpleWeaponColliderFix] 🔧 Коллайдеры оружия отключены");
        }
    }
    
    void EnableColliders()
    {
        for (int i = 0; i < weaponColliders.Length; i++)
        {
            if (weaponColliders[i] != null && i < originalStates.Length)
            {
                weaponColliders[i].enabled = originalStates[i];
            }
        }
        
        collidersDisabled = false;
        
        if (debugMessages)
        {
            Debug.Log($"[SimpleWeaponColliderFix] 🔧 Коллайдеры оружия включены");
        }
    }
    
    [ContextMenu("Отключить коллайдеры")]
    public void ForceDisable()
    {
        DisableColliders();
    }
    
    [ContextMenu("Включить коллайдеры")]
    public void ForceEnable()
    {
        EnableColliders();
    }
    
    [ContextMenu("Показать информацию")]
    public void ShowInfo()
    {
        if (debugMessages)
        {
            Debug.Log($"[SimpleWeaponColliderFix] 📊 Информация:");
            Debug.Log($"[SimpleWeaponColliderFix] 📍 Оружие: {gameObject.name}");
            Debug.Log($"[SimpleWeaponColliderFix] 🔧 Коллайдеров: {weaponColliders.Length}");
            Debug.Log($"[SimpleWeaponColliderFix] ⚡ Состояние: {(collidersDisabled ? "Отключены" : "Включены")}");
        }
    }
    
    void OnDrawGizmosSelected()
    {
        // Показываем радиус проверки
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, checkRadius);
    }
}



