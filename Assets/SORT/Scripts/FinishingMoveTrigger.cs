using UnityEngine;
using Invector.vCharacterController;

public class FinishingMoveTrigger : MonoBehaviour
{
    [Header("Настройки")]
    public float triggerRadius = 2f;
    public KeyCode interactionKey = KeyCode.E;
    public string finishingMoveAnimation = "FinishingMove";
    
    private bool isPlayerInRange = false;
    private vThirdPersonController player;
    private Animator playerAnimator;
    private GameObject targetEnemy;
    private SphereCollider triggerCollider;

    private void Start()
    {
        // Создаем и настраиваем коллайдер
        triggerCollider = gameObject.AddComponent<SphereCollider>();
        triggerCollider.isTrigger = true;
        triggerCollider.radius = triggerRadius;
        
        // Находим игрока
        player = FindObjectOfType<vThirdPersonController>();
        if (player != null)
        {
            playerAnimator = player.GetComponent<Animator>();
        }

        if (triggerCollider == null)
        {
            Debug.LogError("Collider не найден на триггере!");
        }
        else if (!triggerCollider.isTrigger)
        {
            Debug.LogWarning($"Collider на {gameObject.name} не помечен как IsTrigger!");
        }
    }

    private void Update()
    {
        if (isPlayerInRange && Input.GetKeyDown(interactionKey) && targetEnemy != null)
        {
            PerformFinishingMove();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInRange = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInRange = false;
        }
    }

    private void PerformFinishingMove()
    {
        if (playerAnimator != null)
        {
            // Проигрываем анимацию добивания
            playerAnimator.CrossFadeInFixedTime(finishingMoveAnimation, 0.1f);
            
            // Отключаем управление игроком на время анимации
            player.enabled = false;
            
            // Уничтожаем врага после анимации
            Destroy(targetEnemy, 2f);
            
            // Включаем управление игроком после анимации
            Invoke("EnablePlayerControl", 2f);
        }
    }

    private void EnablePlayerControl()
    {
        player.enabled = true;
    }

    public void SetTargetEnemy(GameObject enemy)
    {
        targetEnemy = enemy;
    }

    public bool IsPlayerInTrigger()
    {
        if (triggerCollider == null) return false;

        // Проверяем все объекты с тегом Player внутри триггера
        Collider[] colliders = Physics.OverlapBox(triggerCollider.bounds.center, 
                                                triggerCollider.bounds.extents, 
                                                triggerCollider.transform.rotation);
        
        bool playerFound = false;
        foreach (var collider in colliders)
        {
            if (collider.CompareTag("Player"))
            {
                playerFound = true;
                break;
            }
        }
        
        Debug.Log($"IsPlayerInTrigger on {gameObject.name} called. Result: {playerFound}");
        return playerFound;
    }
} 