using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class KeyData
{
    public string keyID;
    public string keyName;
    public string keyDescription;
    public bool isCollected;
    
    public KeyData(string id, string name, string description)
    {
        keyID = id;
        keyName = name;
        keyDescription = description;
        isCollected = true;
    }
}

public class KeyInventory : MonoBehaviour
{
    [Header("Настройки инвентаря")]
    [Tooltip("Максимальное количество ключей")]
    public int maxKeys = 10;
    
    [Header("Отладка")]
    [Tooltip("Показывать отладочную информацию")]
    public bool showDebugLog = true;
    
    [Header("Список ключей (только для просмотра)")]
    [SerializeField]
    private List<KeyData> collectedKeys = new List<KeyData>();
    
    /// <summary>
    /// Добавляет ключ в инвентарь
    /// </summary>
    public bool AddKey(string keyID, string keyName, string keyDescription)
    {
        // Проверяем, есть ли уже такой ключ
        if (HasKey(keyID))
        {
            if (showDebugLog)
                Debug.Log($"[KeyInventory] ⚠️ Ключ '{keyName}' уже есть в инвентаре");
            return false;
        }
        
        // Проверяем лимит ключей
        if (collectedKeys.Count >= maxKeys)
        {
            if (showDebugLog)
                Debug.Log($"[KeyInventory] ❌ Инвентарь ключей переполнен! Максимум: {maxKeys}");
            return false;
        }
        
        // Добавляем ключ
        KeyData newKey = new KeyData(keyID, keyName, keyDescription);
        collectedKeys.Add(newKey);
        
        if (showDebugLog)
        {
            Debug.Log($"[KeyInventory] ✅ Ключ добавлен в инвентарь:");
            Debug.Log($"  🔑 ID: {keyID}");
            Debug.Log($"  📝 Название: {keyName}");
            Debug.Log($"  📄 Описание: {keyDescription}");
            Debug.Log($"  📊 Всего ключей: {collectedKeys.Count}/{maxKeys}");
        }
        
        return true;
    }
    
    /// <summary>
    /// Проверяет, есть ли ключ в инвентаре
    /// </summary>
    public bool HasKey(string keyID)
    {
        if (showDebugLog)
            Debug.Log($"[KeyInventory] 🔍 Проверяю наличие ключа '{keyID}' в инвентаре...");
        
        foreach (var key in collectedKeys)
        {
            if (showDebugLog)
                Debug.Log($"[KeyInventory] 🔑 Проверяю ключ: {key.keyID} (активен: {key.isCollected})");
            
            if (key.keyID == keyID && key.isCollected)
            {
                if (showDebugLog)
                    Debug.Log($"[KeyInventory] ✅ Ключ '{keyID}' найден в инвентаре!");
                return true;
            }
        }
        
        if (showDebugLog)
            Debug.Log($"[KeyInventory] ❌ Ключ '{keyID}' не найден в инвентаре");
        
        return false;
    }
    
    /// <summary>
    /// Удаляет ключ из инвентаря
    /// </summary>
    public bool RemoveKey(string keyID)
    {
        for (int i = 0; i < collectedKeys.Count; i++)
        {
            if (collectedKeys[i].keyID == keyID && collectedKeys[i].isCollected)
            {
                KeyData removedKey = collectedKeys[i];
                collectedKeys.RemoveAt(i);
                
                if (showDebugLog)
                {
                    Debug.Log($"[KeyInventory] 🗑️ Ключ удален из инвентаря:");
                    Debug.Log($"  🔑 ID: {removedKey.keyID}");
                    Debug.Log($"  📝 Название: {removedKey.keyName}");
                    Debug.Log($"  📊 Осталось ключей: {collectedKeys.Count}/{maxKeys}");
                }
                
                return true;
            }
        }
        
        if (showDebugLog)
            Debug.Log($"[KeyInventory] ❌ Ключ с ID '{keyID}' не найден в инвентаре");
        
        return false;
    }
    
    /// <summary>
    /// Получает данные ключа по ID
    /// </summary>
    public KeyData GetKey(string keyID)
    {
        foreach (var key in collectedKeys)
        {
            if (key.keyID == keyID && key.isCollected)
            {
                return key;
            }
        }
        return null;
    }
    
    /// <summary>
    /// Получает список всех ключей
    /// </summary>
    public List<KeyData> GetAllKeys()
    {
        return new List<KeyData>(collectedKeys);
    }
    
    /// <summary>
    /// Очищает инвентарь ключей
    /// </summary>
    public void ClearInventory()
    {
        int count = collectedKeys.Count;
        collectedKeys.Clear();
        
        if (showDebugLog)
            Debug.Log($"[KeyInventory] 🗑️ Инвентарь ключей очищен. Удалено ключей: {count}");
    }
    
    /// <summary>
    /// Получает количество ключей в инвентаре
    /// </summary>
    public int GetKeyCount()
    {
        return collectedKeys.Count;
    }
    
    private void Start()
    {
        if (showDebugLog)
            Debug.Log($"[KeyInventory] 📦 Инвентарь ключей инициализирован. Максимум ключей: {maxKeys}");
    }
}
