using UnityEngine;

/// <summary>
/// Тестовый скрипт для проверки работы WeaponColliderManager
/// </summary>
public class WeaponColliderTester : MonoBehaviour
{
    [Header("Тестовые настройки")]
    [Tooltip("Показывать сообщения")]
    public bool debugMessages = true;
    
    [Tooltip("Дистанция поиска оружия")]
    public float searchRadius = 5f;
    
    private void Start()
    {
        if (debugMessages)
        {
            Debug.Log($"[WeaponColliderTester] ✅ Тестер готов");
        }
    }
    
    [ContextMenu("Найти оружие")]
    public void FindWeapons()
    {
        if (debugMessages)
        {
            Debug.Log($"[WeaponColliderTester] 🔍 Поиск оружия в радиусе {searchRadius}...");
        }
        
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            Debug.LogError("[WeaponColliderTester] ❌ Игрок не найден!");
            return;
        }
        
        // Ищем все объекты с коллайдерами
        Collider[] allColliders = Physics.OverlapSphere(player.transform.position, searchRadius);
        
        int weaponCount = 0;
        
        foreach (Collider col in allColliders)
        {
            if (!col.isTrigger && col.gameObject != player)
            {
                // Проверяем есть ли WeaponColliderManager
                WeaponColliderManager weaponManager = col.GetComponent<WeaponColliderManager>();
                if (weaponManager != null)
                {
                    weaponCount++;
                    Debug.Log($"[WeaponColliderTester] ⚔️ Найдено оружие: {col.gameObject.name}");
                    Debug.Log($"[WeaponColliderTester] 📍 Позиция: {col.transform.position}");
                    Debug.Log($"[WeaponColliderTester] 🔧 Коллайдеров: {weaponManager.weaponColliders.Length}");
                }
            }
        }
        
        if (weaponCount == 0)
        {
            Debug.LogWarning("[WeaponColliderTester] ⚠️ Оружие с WeaponColliderManager не найдено!");
        }
        else
        {
            Debug.Log($"[WeaponColliderTester] ✅ Найдено оружия: {weaponCount}");
        }
    }
    
    [ContextMenu("Найти vGenericAction")]
    public void FindGenericActions()
    {
        if (debugMessages)
        {
            Debug.Log($"[WeaponColliderTester] 🔍 Поиск vGenericAction в радиусе {searchRadius}...");
        }
        
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            Debug.LogError("[WeaponColliderTester] ❌ Игрок не найден!");
            return;
        }
        
        // Ищем все объекты с vGenericAction
        Collider[] allColliders = Physics.OverlapSphere(player.transform.position, searchRadius);
        
        int genericActionCount = 0;
        
        foreach (Collider col in allColliders)
        {
            if (col.isTrigger)
            {
                // Проверяем есть ли vGenericAction
                Component genericAction = col.GetComponent("vGenericAction");
                if (genericAction != null)
                {
                    genericActionCount++;
                    Debug.Log($"[WeaponColliderTester] 🎯 Найден vGenericAction: {col.gameObject.name}");
                    Debug.Log($"[WeaponColliderTester] 📍 Позиция: {col.transform.position}");
                }
            }
        }
        
        if (genericActionCount == 0)
        {
            Debug.LogWarning("[WeaponColliderTester] ⚠️ vGenericAction объекты не найдены!");
        }
        else
        {
            Debug.Log($"[WeaponColliderTester] ✅ Найдено vGenericAction: {genericActionCount}");
        }
    }
    
    [ContextMenu("Принудительно отключить оружие")]
    public void ForceDisableWeapons()
    {
        if (debugMessages)
        {
            Debug.Log($"[WeaponColliderTester] 🔧 Принудительное отключение оружия...");
        }
        
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;
        
        Collider[] allColliders = Physics.OverlapSphere(player.transform.position, searchRadius);
        
        foreach (Collider col in allColliders)
        {
            if (!col.isTrigger && col.gameObject != player)
            {
                WeaponColliderManager weaponManager = col.GetComponent<WeaponColliderManager>();
                if (weaponManager != null)
                {
                    weaponManager.ForceDisableColliders();
                    Debug.Log($"[WeaponColliderTester] 🔧 Отключено оружие: {col.gameObject.name}");
                }
            }
        }
    }
    
    [ContextMenu("Принудительно включить оружие")]
    public void ForceEnableWeapons()
    {
        if (debugMessages)
        {
            Debug.Log($"[WeaponColliderTester] 🔧 Принудительное включение оружия...");
        }
        
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;
        
        Collider[] allColliders = Physics.OverlapSphere(player.transform.position, searchRadius);
        
        foreach (Collider col in allColliders)
        {
            if (!col.isTrigger && col.gameObject != player)
            {
                WeaponColliderManager weaponManager = col.GetComponent<WeaponColliderManager>();
                if (weaponManager != null)
                {
                    weaponManager.ForceEnableColliders();
                    Debug.Log($"[WeaponColliderTester] 🔧 Включено оружие: {col.gameObject.name}");
                }
            }
        }
    }
    
    void OnDrawGizmosSelected()
    {
        // Показываем радиус поиска
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, searchRadius);
    }
}



