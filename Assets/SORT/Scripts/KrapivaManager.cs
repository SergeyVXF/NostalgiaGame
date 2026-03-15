using System.Collections.Generic;
using UnityEngine;

public class KrapivaManager : MonoBehaviour
{
    // Список всех частей крапивы
    public List<GameObject> krapivaParts = new List<GameObject>();
    
    // Основной объект крапивы
    public GameObject mainKrapivaObject;
    
    // Флаг, указывающий увеличивать ли счетчик при уничтожении
    public bool incrementCounterOnDestroy = true;
    
    // Имена частей для автоматического поиска
    private readonly string[] partNames = new string[] 
    { 
        "Krapiva_part_01", 
        "Krapiva_part_02", 
        "Krapiva_part_03", 
        "Krapiva_part_04", 
        "krapiva_parts" 
    };

    private void Start()
    {
        // Если основной объект не установлен, ищем его
        if (mainKrapivaObject == null)
        {
            mainKrapivaObject = gameObject.name.ToLower() == "krapiva" || gameObject.name.ToLower() == "krapiva(clone)" 
                ? gameObject 
                : GameObject.Find("krapiva") ?? GameObject.Find("krapiva(Clone)");
            
            if (mainKrapivaObject == null)
            {
                Debug.LogWarning("KrapivaManager: Не удалось найти основной объект крапивы.");
            }
        }
        
        // Если список частей пуст, ищем все части автоматически
        if (krapivaParts.Count == 0)
        {
            foreach (string partName in partNames)
            {
                GameObject part = GameObject.Find(partName);
                if (part != null && !krapivaParts.Contains(part))
                {
                    krapivaParts.Add(part);
                    // Добавляем компонент для отслеживания уничтожения
                    KrapivaPartObserver observer = part.AddComponent<KrapivaPartObserver>();
                    observer.manager = this;
                }
            }
            
            Debug.Log($"KrapivaManager: Найдено {krapivaParts.Count} частей крапивы.");
        }
    }
    
    // Вызывается, когда одна из частей уничтожена
    public void OnPartDestroyed(GameObject destroyedPart)
    {
        Debug.Log($"KrapivaManager: Часть {destroyedPart.name} была уничтожена.");
        
        // Удаляем уничтоженную часть из списка
        krapivaParts.Remove(destroyedPart);
        
        // Уничтожаем основной объект, если он еще существует
        if (mainKrapivaObject != null && mainKrapivaObject != gameObject)
        {
            // Запоминаем имя объекта до уничтожения
            string krapivaName = mainKrapivaObject.name;
            
            // Уничтожаем основной объект
            Destroy(mainKrapivaObject);
            Debug.Log("KrapivaManager: Основной объект крапивы уничтожен.");
            
            // Увеличиваем счетчик, если флаг включен
            if (incrementCounterOnDestroy)
            {
                KrapivaCounter.IncrementCounter();
                Debug.Log($"KrapivaManager: Счетчик уничтоженной крапивы увеличен. Объект: {krapivaName}");
            }
        }
        else if (mainKrapivaObject == gameObject)
        {
            // Запоминаем имя текущего объекта
            string krapivaName = gameObject.name;
            
            // Если этот скрипт прикреплен к основному объекту, уничтожаем его со следующего кадра
            Destroy(gameObject, 0.01f);
            Debug.Log("KrapivaManager: Основной объект крапивы будет уничтожен.");
            
            // Увеличиваем счетчик, если флаг включен
            if (incrementCounterOnDestroy)
            {
                KrapivaCounter.IncrementCounter();
                Debug.Log($"KrapivaManager: Счетчик уничтоженной крапивы увеличен. Объект: {krapivaName}");
            }
        }
    }
}

// Вспомогательный класс для отслеживания уничтожения частей
public class KrapivaPartObserver : MonoBehaviour
{
    // Ссылка на менеджер
    public KrapivaManager manager;
    
    private void OnDestroy()
    {
        // Уведомляем менеджер об уничтожении части
        if (manager != null)
        {
            manager.OnPartDestroyed(gameObject);
        }
    }
} 