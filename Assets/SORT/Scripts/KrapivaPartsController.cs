using UnityEngine;

public class KrapivaPartsController : MonoBehaviour
{
    // Ссылка на основной объект крапивы
    public GameObject mainKrapivaObject;
    
    // Флаг, указывающий увеличивать ли счетчик при уничтожении
    public bool incrementCounterOnDestroy = true;

    private void Start()
    {
        // Если ссылка на основной объект не установлена, попробуем найти его автоматически
        if (mainKrapivaObject == null)
        {
            // Пытаемся найти объект с именем "krapiva" или "krapiva(Clone)"
            mainKrapivaObject = GameObject.Find("krapiva") ?? GameObject.Find("krapiva(Clone)");
            
            if (mainKrapivaObject == null)
            {
                Debug.LogWarning("KrapivaPartsController: Не удалось найти основной объект крапивы.");
            }
        }
    }

    private void OnDestroy()
    {
        // Когда часть крапивы уничтожается, уничтожаем основной объект
        if (mainKrapivaObject != null)
        {
            // Получаем имя объекта до его уничтожения
            string objectName = mainKrapivaObject.name;
            
            // Уничтожаем основной объект
            Destroy(mainKrapivaObject);
            Debug.Log("Уничтожен основной объект крапивы из-за уничтожения части: " + gameObject.name);
            
            // Увеличиваем счетчик, если это необходимо
            if (incrementCounterOnDestroy)
            {
                // Увеличиваем счетчик уничтоженных объектов крапивы
                KrapivaCounter.IncrementCounter();
                Debug.Log("Счетчик уничтоженной крапивы увеличен. Объект: " + objectName);
            }
        }
    }
} 