using UnityEngine;

public class ThrowableBone : MonoBehaviour
{
    [Header("Настройки подбора")]
    [Tooltip("Расстояние для подбора косточки")]
    public float pickupDistance = 2f;
    
    [Tooltip("Тег игрока")]
    public string playerTag = "Player";
    
    [Header("Отладка")]
    [Tooltip("Показывать отладочную информацию")]
    public bool showDebugLog = true;
    
    private BoneBehavior boneBehavior;
    private bool canBePickedUp = true;
    
    private void Awake()
    {
        // Устанавливаем тег косточки
        if (!gameObject.CompareTag("Bone"))
        {
            gameObject.tag = "Bone";
            if (showDebugLog)
                Debug.Log($"[ThrowableBone] Установлен тег 'Bone' для {gameObject.name}");
        }
        
        // Получаем компонент BoneBehavior
        boneBehavior = GetComponent<BoneBehavior>();
        if (boneBehavior == null)
        {
            boneBehavior = gameObject.AddComponent<BoneBehavior>();
            if (showDebugLog)
                Debug.Log($"[ThrowableBone] Добавлен компонент BoneBehavior к {gameObject.name}");
        }
        
        // Убеждаемся, что у косточки есть триггер для подбора
        SetupPickupTrigger();
    }
    
    /// <summary>
    /// Настраивает триггер для подбора косточки
    /// </summary>
    private void SetupPickupTrigger()
    {
        // Ищем существующий SphereCollider с isTrigger = true
        SphereCollider[] sphereColliders = GetComponents<SphereCollider>();
        SphereCollider pickupTrigger = null;
        
        foreach (var col in sphereColliders)
        {
            if (col.isTrigger)
            {
                pickupTrigger = col;
                break;
            }
        }
        
        // Если нет триггера - создаем
        if (pickupTrigger == null)
        {
            pickupTrigger = gameObject.AddComponent<SphereCollider>();
            pickupTrigger.isTrigger = true;
            if (showDebugLog)
                Debug.Log($"[ThrowableBone] Создан SphereCollider триггер для {gameObject.name}");
        }
        
        // Настраиваем радиус
        pickupTrigger.radius = pickupDistance;
        if (showDebugLog)
            Debug.Log($"[ThrowableBone] Триггер настроен: радиус {pickupTrigger.radius}м");
    }
    
    private void Start()
    {
        if (showDebugLog)
            Debug.Log($"[ThrowableBone] Кидаемая косточка {gameObject.name} готова к использованию");
        
        // Ждем 1 секунду после создания, затем активируем собак
        Invoke("ActivateDogsAfterThrow", 1f);
    }
    
    /// <summary>
    /// Активирует собак через секунду после броска
    /// </summary>
    private void ActivateDogsAfterThrow()
    {
        if (boneBehavior != null)
        {
            if (showDebugLog)
                Debug.Log($"[ThrowableBone] 🚀 Активирую собак для косточки {gameObject.name}");
            boneBehavior.ForceNearestDogToChasePublic();
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (showDebugLog)
            Debug.Log($"[ThrowableBone] OnTriggerEnter: {other.name}, tag: '{other.tag}', ожидаемый тег: '{playerTag}'");
        
        // Проверяем, что это игрок и косточку можно подобрать
        if (canBePickedUp && !boneBehavior.IsBeingEaten() && other.CompareTag(playerTag))
        {
            if (showDebugLog)
                Debug.Log($"[ThrowableBone] 🎯 Игрок {other.name} коснулся косточки {gameObject.name} - ПОДБИРАЮ!");
            
            PickupBone(other.gameObject);
        }
        else
        {
            if (showDebugLog)
            {
                string reason = "";
                if (!canBePickedUp) reason += "нельзя подбирать, ";
                if (boneBehavior.IsBeingEaten()) reason += "собака ест, ";
                if (!other.CompareTag(playerTag)) reason += $"не игрок (тег: '{other.tag}'), ";
                
                Debug.Log($"[ThrowableBone] ❌ НЕ подбираю {gameObject.name}: {reason.TrimEnd(',', ' ')}");
            }
        }
    }
    
    /// <summary>
    /// Подбор косточки игроком
    /// </summary>
    private void PickupBone(GameObject player)
    {
        if (showDebugLog)
            Debug.Log($"[ThrowableBone] 🦴 Игрок {player.name} подбирает косточку {gameObject.name}");
        
        // Добавляем косточку в инвентарь
        InventorySystem inventory = InventorySystem.Instance;
        if (inventory != null)
        {
            inventory.AddBone();
            if (showDebugLog)
                Debug.Log("[ThrowableBone] ✅ Косточка добавлена в инвентарь игрока");
        }
        else
        {
            Debug.LogError("[ThrowableBone] ❌ InventorySystem не найден!");
        }
        
        // Уничтожаем косточку
        Destroy(gameObject);
    }
    
    /// <summary>
    /// Запретить подбор косточки (когда собака её ест)
    /// </summary>
    public void DisablePickup()
    {
        canBePickedUp = false;
        if (showDebugLog)
            Debug.Log($"[ThrowableBone] Подбор косточки {gameObject.name} отключен");
    }
    
    /// <summary>
    /// Разрешить подбор косточки
    /// </summary>
    public void EnablePickup()
    {
        canBePickedUp = true;
        if (showDebugLog)
            Debug.Log($"[ThrowableBone] Подбор косточки {gameObject.name} включен");
    }
    
    private void OnDrawGizmos()
    {
        // Рисуем радиус подбора в редакторе
        Gizmos.color = canBePickedUp ? Color.green : Color.red;
        Gizmos.DrawWireSphere(transform.position, pickupDistance);
        
        // Рисуем иконку косточки
        Gizmos.color = Color.cyan;
        Gizmos.DrawCube(transform.position + Vector3.up * 0.8f, Vector3.one * 0.3f);
    }
}
