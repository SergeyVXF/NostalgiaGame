using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerSpawnManager : MonoBehaviour
{
    [Tooltip("Позиция спавна игрока на сцене")]
    [SerializeField] private Transform spawnPoint;
    
    [Tooltip("Тег игрока для поиска")]
    [SerializeField] private string playerTag = "Player";
    
    [Tooltip("Имя сцены, для которой применяется точка спавна")]
    [SerializeField] public string targetScene = "Sword_Quest_Scene";
    
    [Tooltip("Добавить небольшое смещение вверх при спавне (чтобы игрок не застрял в земле)")]
    [SerializeField] private float heightOffset = 0.1f;
    
    [Header("Условия активации")]
    [Tooltip("Проверять активность SpawnPoint компонента")]
    [SerializeField] public bool checkSpawnPointActivity = true;
    
    [Tooltip("Показывать сообщения в консоли")]
    [SerializeField] private bool debugMessages = true;
    
    private SpawnPoint spawnPointComponent;
    
    private void Start()
    {
        // Получаем компонент SpawnPoint если есть
        if (spawnPoint != null)
        {
            spawnPointComponent = spawnPoint.GetComponent<SpawnPoint>();
        }
        
        // Проверяем, находимся ли мы на целевой сцене
        if (SceneManager.GetActiveScene().name == targetScene)
        {
            // Ждем один кадр перед телепортацией игрока, чтобы все компоненты успели инициализироваться
            Invoke("TeleportPlayerToSpawnPoint", 0.1f);
        }
        else
        {
            if (debugMessages)
            {
                Debug.Log($"[PlayerSpawnManager] Не на целевой сцене. Текущая: {SceneManager.GetActiveScene().name}, Целевая: {targetScene}");
            }
        }
    }
    
    private void TeleportPlayerToSpawnPoint()
    {
        // Проверяем наличие точки спавна
        if (spawnPoint == null)
        {
            Debug.LogError("[PlayerSpawnManager] Точка спавна не назначена в инспекторе!");
            return;
        }
        
        // Проверяем активность SpawnPoint компонента если включено
        if (checkSpawnPointActivity && spawnPointComponent != null)
        {
            if (!spawnPointComponent.isActive)
            {
                if (debugMessages)
                {
                    Debug.Log($"[PlayerSpawnManager] ❌ Точка спавна неактивна, телепортация отменена");
                    Debug.Log($"[PlayerSpawnManager] 📊 SpawnPoint ID: {spawnPointComponent.spawnPointID}");
                }
                return;
            }
            else
            {
                if (debugMessages)
                {
                    Debug.Log($"[PlayerSpawnManager] ✅ Точка спавна активна, продолжаем телепортацию");
                }
            }
        }
        
        // Находим игрока по тегу
        GameObject player = GameObject.FindGameObjectWithTag(playerTag);
        
        if (player != null)
        {
            Debug.Log($"[PlayerSpawnManager] Игрок найден, телепортируем в точку спавна на сцене {targetScene}");
            
            // Отключаем CharacterController перед телепортацией, если он есть
            var characterController = player.GetComponent<CharacterController>();
            if (characterController != null)
            {
                characterController.enabled = false;
            }
            
            // Отключаем Rigidbody перед телепортацией, если он есть
            var rigidbody = player.GetComponent<Rigidbody>();
            if (rigidbody != null)
            {
                rigidbody.isKinematic = true;
            }
            
            // Телепортируем игрока на точку спавна
            Vector3 spawnPosition = spawnPoint.position + new Vector3(0, heightOffset, 0);
            player.transform.position = spawnPosition;
            player.transform.rotation = spawnPoint.rotation;
            
            Debug.Log($"[PlayerSpawnManager] Игрок телепортирован в позицию: {spawnPosition}");
            
            // Включаем компоненты обратно
            if (characterController != null)
            {
                characterController.enabled = true;
            }
            
            if (rigidbody != null)
            {
                rigidbody.isKinematic = false;
            }
        }
        else
        {
            Debug.LogError($"[PlayerSpawnManager] Игрок с тегом '{playerTag}' не найден на сцене!");
        }
    }
    
    /// <summary>
    /// Принудительно телепортировать игрока (игнорируя проверки активности)
    /// </summary>
    [ContextMenu("Принудительно телепортировать игрока")]
    public void ForceTeleportPlayer()
    {
        bool originalCheck = checkSpawnPointActivity;
        checkSpawnPointActivity = false;
        
        TeleportPlayerToSpawnPoint();
        
        checkSpawnPointActivity = originalCheck;
        
        if (debugMessages)
        {
            Debug.Log($"[PlayerSpawnManager] 🔧 Принудительная телепортация выполнена");
        }
    }
    
    /// <summary>
    /// Показать информацию о состоянии
    /// </summary>
    [ContextMenu("Показать информацию")]
    public void ShowInfo()
    {
        if (debugMessages)
        {
            Debug.Log($"[PlayerSpawnManager] 📊 Информация:");
            Debug.Log($"[PlayerSpawnManager] 📍 Текущая сцена: {SceneManager.GetActiveScene().name}");
            Debug.Log($"[PlayerSpawnManager] 🎯 Целевая сцена: {targetScene}");
            Debug.Log($"[PlayerSpawnManager] 🔧 Проверка активности: {checkSpawnPointActivity}");
            
            if (spawnPointComponent != null)
            {
                Debug.Log($"[PlayerSpawnManager] 🆔 SpawnPoint ID: {spawnPointComponent.spawnPointID}");
                Debug.Log($"[PlayerSpawnManager] ⚡ Только из SwordQuest: {spawnPointComponent.onlyFromSwordQuest}");
            }
            else
            {
                Debug.Log($"[PlayerSpawnManager] ❌ SpawnPoint компонент не найден");
            }
        }
    }
    
    // Метод для отладки - визуализация точки спавна в редакторе
    private void OnDrawGizmos()
    {
        if (spawnPoint != null)
        {
            // Проверяем активность SpawnPoint для цвета
            SpawnPoint sp = spawnPoint.GetComponent<SpawnPoint>();
            if (sp != null && sp.onlyFromSwordQuest)
            {
                Gizmos.color = sp.isActive ? Color.green : Color.red;
            }
            else
            {
                Gizmos.color = Color.green;
            }
            
            Gizmos.DrawSphere(spawnPoint.position, 0.5f);
            Gizmos.DrawRay(spawnPoint.position, spawnPoint.forward * 2f);
        }
    }
} 