using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(BoxCollider))]
public class SaltoTriggerZone : MonoBehaviour
{
    [HideInInspector]
    public int zoneIndex;                    // Индекс зоны
    public float minJumpHeight = 2f;         // Минимальная высота прыжка
    
    public UnityEvent<int> onZoneComplete;   // Событие завершения зоны
    public UnityEvent onPlayerEnter;         // Событие входа игрока в зону
    public UnityEvent onPlayerExit;          // Событие выхода игрока из зоны
    public UnityEvent onSaltoFailed;         // Событие неудачного сальто

    private bool isPlayerInside;             // Находится ли игрок в зоне
    private float maxPlayerHeight;           // Максимальная высота игрока в зоне
    private float enterTime;                 // Время входа в зону
    private bool zoneCompleted;              // Зона завершена
    private Transform playerTransform;        // Трансформ игрока
    private Animator playerAnimator;          // Animator игрока

    void Start()
    {
        // Убеждаемся, что коллайдер настроен как триггер
        GetComponent<BoxCollider>().isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (zoneCompleted || !other.CompareTag("Player")) return;

        isPlayerInside = true;
        playerTransform = other.transform;
        maxPlayerHeight = playerTransform.position.y;
        enterTime = Time.time;
        onPlayerEnter?.Invoke();

        // Получаем Animator игрока
        playerAnimator = other.GetComponent<Animator>();
        if (playerAnimator == null)
        {
            playerAnimator = other.GetComponentInChildren<Animator>();
        }

       
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        isPlayerInside = false;
        onPlayerExit?.Invoke();

        // Проверяем условия выполнения сальто
        if (!zoneCompleted)
        {
            float heightDifference = maxPlayerHeight - transform.position.y;

            if (heightDifference >= minJumpHeight)
            {
                zoneCompleted = true;
                onZoneComplete?.Invoke(zoneIndex);
            }
            else
            {
                onSaltoFailed?.Invoke();
            }
        }
    }

    void Update()
    {
        if (isPlayerInside && playerTransform != null)
        {
            // Обновляем максимальную высоту игрока
            maxPlayerHeight = Mathf.Max(maxPlayerHeight, playerTransform.position.y);

            // Проверяем анимацию salto_main
            if (!zoneCompleted && playerAnimator != null)
            {
                AnimatorStateInfo stateInfo = playerAnimator.GetCurrentAnimatorStateInfo(0);
                if (stateInfo.IsName("salto_main"))
                {
                    zoneCompleted = true;
                    onZoneComplete?.Invoke(zoneIndex);
                }
            }
        }
    }

    public void ResetZone()
    {
        zoneCompleted = false;
        isPlayerInside = false;
        maxPlayerHeight = 0f;
        enterTime = 0f;
    }

    // Для отображения зоны в редакторе
    void OnDrawGizmos()
    {
        BoxCollider collider = GetComponent<BoxCollider>();
        if (collider != null)
        {
            Gizmos.color = zoneCompleted ? Color.green : (isPlayerInside ? Color.yellow : Color.red);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(collider.center, collider.size);
            
            // Рисуем линию минимальной высоты прыжка
            Vector3 zonePos = transform.position;
            Gizmos.DrawLine(zonePos, zonePos + Vector3.up * minJumpHeight);
        }
    }
} 