using UnityEngine;
using System.Collections;

/// <summary>
/// Простой тестовый скрипт для телепортации игрока
/// </summary>
public class TestTeleporter : MonoBehaviour
{
    [Header("Тестовые настройки")]
    [Tooltip("Координаты для телепортации")]
    public Vector3 testPosition = new Vector3(-186f, 2f, 46f);
    
    [Tooltip("Задержка перед телепортацией")]
    public float testDelay = 2f;
    
    [Tooltip("Показывать сообщения")]
    public bool debugMessages = true;
    
    private void Start()
    {
        if (debugMessages)
        {
            Debug.Log($"[TestTeleporter] ✅ Тестовый телепортер инициализирован");
            Debug.Log($"[TestTeleporter] 📍 Тестовая позиция: {testPosition}");
            Debug.Log($"[TestTeleporter] ⏱️ Тестовая задержка: {testDelay} сек");
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (debugMessages)
            {
                Debug.Log($"[TestTeleporter] 🎯 Игрок вошел в триггер {gameObject.name}");
                Debug.Log($"[TestTeleporter] ⏱️ Запускаю тестовую телепортацию через {testDelay} сек");
            }
            
            // Отключаем коллайдер, чтобы избежать повторных срабатываний
            Collider triggerCollider = GetComponent<Collider>();
            if (triggerCollider != null)
            {
                triggerCollider.enabled = false;
            }
            
            StartCoroutine(TestTeleport());
        }
    }
    
    private IEnumerator TestTeleport()
    {
        yield return new WaitForSeconds(testDelay);
        
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        
        if (player != null)
        {
            if (debugMessages)
            {
                Debug.Log($"[TestTeleporter] 🚀 ТЕСТ: Телепортирую игрока в {testPosition}");
                Debug.Log($"[TestTeleporter] 📍 Текущая позиция: {player.transform.position}");
                Debug.Log($"[TestTeleporter] ⚡ Игрок активен: {player.activeInHierarchy}");
            }
            
            if (player.activeInHierarchy)
            {
                player.transform.position = testPosition;
                
                if (debugMessages)
                {
                    Debug.Log($"[TestTeleporter] ✅ ТЕСТ: Игрок телепортирован в {player.transform.position}");
                }
            }
            else
            {
                if (debugMessages)
                {
                    Debug.LogWarning("[TestTeleporter] ⚠️ ТЕСТ: Игрок неактивен!");
                }
                
                // Ждем активации
                yield return new WaitUntil(() => player.activeInHierarchy);
                
                if (debugMessages)
                {
                    Debug.Log("[TestTeleporter] ✅ ТЕСТ: Игрок активирован, телепортирую...");
                }
                
                player.transform.position = testPosition;
                
                if (debugMessages)
                {
                    Debug.Log($"[TestTeleporter] ✅ ТЕСТ: Игрок телепортирован в {player.transform.position}");
                }
            }
        }
        else
        {
            if (debugMessages)
            {
                Debug.LogError("[TestTeleporter] ❌ ТЕСТ: Игрок не найден!");
            }
        }
    }
    
    [ContextMenu("Телепортировать игрока сейчас")]
    public void TeleportNow()
    {
        if (debugMessages)
        {
            Debug.Log("[TestTeleporter] 🚀 Принудительная тестовая телепортация");
        }
        
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            player.transform.position = testPosition;
            
            if (debugMessages)
            {
                Debug.Log($"[TestTeleporter] ✅ Принудительная телепортация в {player.transform.position}");
            }
        }
    }
}
