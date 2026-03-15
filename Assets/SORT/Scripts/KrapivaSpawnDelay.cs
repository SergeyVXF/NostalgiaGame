using UnityEngine;

public class KrapivaSpawnDelay : MonoBehaviour
{
    [Header("Настройки задержки")]
    [Tooltip("Задержка перед спавном объекта (в секундах)")]
    public float spawnDelay = 2f;

    private void Start()
    {
        // Деактивируем объект при старте
        gameObject.SetActive(false);
        
        // Запускаем таймер для активации объекта
        Invoke("ActivateObject", spawnDelay);
    }

    private void ActivateObject()
    {
        // Активируем объект после задержки
        gameObject.SetActive(true);
    }
} 