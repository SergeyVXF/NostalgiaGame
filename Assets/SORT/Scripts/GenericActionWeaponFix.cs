using UnityEngine;

/// <summary>
/// Скрипт для vGenericAction который отключает коллайдеры оружия при взаимодействии
/// </summary>
public class GenericActionWeaponFix : MonoBehaviour
{
    [Header("Настройки")]
    [Tooltip("Дистанция для поиска оружия")]
    public float weaponSearchRadius = 3f;
    
    [Tooltip("Показывать сообщения")]
    public bool debugMessages = true;
    
    private bool weaponsDisabled = false;
    private Collider[] disabledWeaponColliders;
    private bool[] originalColliderStates;
    
    void Start()
    {
        if (debugMessages)
        {
            Debug.Log($"[GenericActionWeaponFix] ✅ Система готова для {gameObject.name}");
        }
    }
    
    void OnTriggerEnter(Collider other)
    {
        // Проверяем что это игрок
        if (other.CompareTag("Player") && !weaponsDisabled)
        {
            if (debugMessages)
            {
                Debug.Log($"[GenericActionWeaponFix] 🎯 Игрок вошел в зону {gameObject.name}");
            }
            
            DisableWeaponColliders();
        }
    }
    
    void OnTriggerExit(Collider other)
    {
        // Проверяем что это игрок
        if (other.CompareTag("Player") && weaponsDisabled)
        {
            if (debugMessages)
            {
                Debug.Log($"[GenericActionWeaponFix] 🚶 Игрок вышел из зоны {gameObject.name}");
            }
            
            EnableWeaponColliders();
        }
    }
    
    void DisableWeaponColliders()
    {
        // Ищем оружие у игрока
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;
        
        // Ищем все коллайдеры оружия в радиусе
        Collider[] allColliders = Physics.OverlapSphere(player.transform.position, weaponSearchRadius);
        System.Collections.Generic.List<Collider> weaponColliders = new System.Collections.Generic.List<Collider>();
        
        foreach (Collider col in allColliders)
        {
            // Проверяем что это коллайдер оружия (не триггер)
            if (!col.isTrigger && col.gameObject != player && col.gameObject != gameObject)
            {
                // Проверяем что это оружие (по имени или тегу)
                if (IsWeapon(col.gameObject))
                {
                    weaponColliders.Add(col);
                }
            }
        }
        
        if (weaponColliders.Count > 0)
        {
            disabledWeaponColliders = weaponColliders.ToArray();
            originalColliderStates = new bool[disabledWeaponColliders.Length];
            
            // Сохраняем состояния и отключаем коллайдеры
            for (int i = 0; i < disabledWeaponColliders.Length; i++)
            {
                if (disabledWeaponColliders[i] != null)
                {
                    originalColliderStates[i] = disabledWeaponColliders[i].enabled;
                    disabledWeaponColliders[i].enabled = false;
                }
            }
            
            weaponsDisabled = true;
            
            if (debugMessages)
            {
                Debug.Log($"[GenericActionWeaponFix] 🔧 Отключено коллайдеров оружия: {disabledWeaponColliders.Length}");
            }
        }
    }
    
    void EnableWeaponColliders()
    {
        if (disabledWeaponColliders != null)
        {
            // Восстанавливаем состояния коллайдеров
            for (int i = 0; i < disabledWeaponColliders.Length; i++)
            {
                if (disabledWeaponColliders[i] != null && i < originalColliderStates.Length)
                {
                    disabledWeaponColliders[i].enabled = originalColliderStates[i];
                }
            }
            
            weaponsDisabled = false;
            
            if (debugMessages)
            {
                Debug.Log($"[GenericActionWeaponFix] 🔧 Включено коллайдеров оружия: {disabledWeaponColliders.Length}");
            }
        }
    }
    
    bool IsWeapon(GameObject obj)
    {
        // Проверяем по имени объекта
        string objName = obj.name.ToLower();
        
        // Список ключевых слов для оружия
        string[] weaponKeywords = {
            "sword", "меч", "weapon", "оружие", "blade", "клинок",
            "knife", "нож", "axe", "топор", "hammer", "молот",
            "spear", "копье", "bow", "лук", "gun", "пистолет"
        };
        
        foreach (string keyword in weaponKeywords)
        {
            if (objName.Contains(keyword))
            {
                return true;
            }
        }
        
        // Проверяем по тегу
        if (obj.CompareTag("Weapon") || obj.CompareTag("Sword"))
        {
            return true;
        }
        
        // Проверяем по компонентам
        if (obj.GetComponent<Collider>() != null && !obj.GetComponent<Collider>().isTrigger)
        {
            // Если у объекта есть коллайдер и он не триггер, считаем его оружием
            return true;
        }
        
        return false;
    }
    
    [ContextMenu("Отключить оружие сейчас")]
    public void ForceDisableWeapons()
    {
        DisableWeaponColliders();
    }
    
    [ContextMenu("Включить оружие сейчас")]
    public void ForceEnableWeapons()
    {
        EnableWeaponColliders();
    }
    
    [ContextMenu("Показать информацию")]
    public void ShowInfo()
    {
        if (debugMessages)
        {
            Debug.Log($"[GenericActionWeaponFix] 📊 Информация:");
            Debug.Log($"[GenericActionWeaponFix] 📍 Объект: {gameObject.name}");
            Debug.Log($"[GenericActionWeaponFix] ⚡ Состояние: {(weaponsDisabled ? "Оружие отключено" : "Оружие включено")}");
            
            if (disabledWeaponColliders != null)
            {
                Debug.Log($"[GenericActionWeaponFix] 🔧 Отключенных коллайдеров: {disabledWeaponColliders.Length}");
            }
        }
    }
    
    void OnDrawGizmosSelected()
    {
        // Показываем радиус поиска оружия
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, weaponSearchRadius);
    }
}



