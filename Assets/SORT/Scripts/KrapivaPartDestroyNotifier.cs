using UnityEngine;

public class KrapivaPartDestroyNotifier : MonoBehaviour
{
    // Имя главного объекта крапивы
    public string mainKrapivaName = "krapiva";
    
    // Включить уничтожение с именем krapiva(Clone)
    public bool destroyClones = true;
    
    // Ссылка на главный объект (если известна заранее)
    public GameObject mainKrapivaObject;
    
    // Флаг, указывающий увеличивать ли счетчик при уничтожении
    public bool incrementCounterOnDestroy = true;
    
    private void Start()
    {
        // Если объект не задан вручную, найдем его по имени
        if (mainKrapivaObject == null)
        {
            mainKrapivaObject = GameObject.Find(mainKrapivaName);
            
            // Если нужно также искать клонов
            if (mainKrapivaObject == null && destroyClones)
            {
                mainKrapivaObject = GameObject.Find(mainKrapivaName + "(Clone)");
            }
        }
    }
    
    private void OnDestroy()
    {
        // При уничтожении части крапивы уничтожаем основной объект
        if (mainKrapivaObject != null)
        {
            // Запоминаем имя объекта до уничтожения
            string objectName = mainKrapivaObject.name;
            
            // Уничтожаем основной объект
            Destroy(mainKrapivaObject);
            Debug.Log($"Объект {mainKrapivaName} уничтожен из-за уничтожения части {gameObject.name}");
            
            // Увеличиваем счетчик, если включен соответствующий флаг
            if (incrementCounterOnDestroy)
            {
                KrapivaCounter.IncrementCounter();
                Debug.Log($"Счетчик уничтоженной крапивы увеличен. Объект: {objectName}");
            }
        }
    }
} 