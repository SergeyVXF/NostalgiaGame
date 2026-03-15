using UnityEngine;
using System.Collections;
using Invector;
using Invector.vMelee;
using Invector.vEventSystems;
using UnityEngine.Events;

/// <summary>
/// Улучшенная версия vBreakableObject, которая дополнительно поддерживает удары руками
/// </summary>
public class vBreakableObjectEnhanced : MonoBehaviour, vIDamageReceiver
{
    public Transform brokenObject;
    
    [Header("Break Object Settings")]
    [Tooltip("Разрушать объект при перекатывании игрока")]
    public bool breakOnPlayerRoll = true;
    [Tooltip("Разрушать объект при столкновении с другими объектами")]
    public bool breakOnCollision = true;
    [Tooltip("Скорость Rigidbody, необходимая для разрушения при столкновении")]
    public float maxVelocityToBreak = 5f;
    [Tooltip("Разрушать объект при ударе руками")]
    public bool breakOnHandAttack = true;
    
    [Header("Attack Detection")]
    [Tooltip("Слой, в котором находятся хитбоксы рук (обычно 2 - IgnoreRaycast)")]
    public LayerMask handHitBoxLayer = 1 << 2;
    [Tooltip("Минимальная сила удара для разрушения")]
    public float minHandAttackForce = 3f;
    
    [Header("Debug")]
    public bool debugMode = false;
    
    public UnityEngine.Events.UnityEvent OnBroken;
    [SerializeField] protected OnReceiveDamage _onReceiveDamage = new OnReceiveDamage();
    public OnReceiveDamage onReceiveDamage { get { return _onReceiveDamage; } protected set { _onReceiveDamage = value; } }
    
    private bool isBroken;
    private Collider _collider;
    private Rigidbody _rigidBody;

    [Header("Events")]
    [Tooltip("Событие, вызываемое перед разрушением объекта")]
    public UnityEvent onPreBreak;
    [Tooltip("Событие, вызываемое после разрушения объекта")]
    public UnityEvent onPostBreak;

    void Start()
    {
        _collider = GetComponent<Collider>();
        _rigidBody = GetComponent<Rigidbody>();
        
        // Рекомендуется использовать триггер для лучшего обнаружения ударов
        if (_collider != null && !_collider.isTrigger && breakOnHandAttack)
        {
            if (debugMode)
                Debug.Log($"vBreakableObjectEnhanced: Для объекта {gameObject.name} рекомендуется установить коллайдер как триггер");
        }

        if (brokenObject == null)
        {
            if (debugMode) Debug.LogWarning("vBreakableObjectEnhanced: brokenObject не указан!");
        }
        else if (brokenObject.gameObject.activeSelf)
        {
            brokenObject.gameObject.SetActive(false);
        }
    }

    public void TakeDamage(vDamage damage)
    {
        if (debugMode)
            Debug.Log($"vBreakableObjectEnhanced: Получен урон {damage.damageValue} от {damage.sender?.name}");
            
        if (!isBroken)
        {
            isBroken = true;
            StartCoroutine(BreakObject(damage.hitPosition));
        }
    }

    IEnumerator BreakObject(Vector3 force)
    {
        if (isBroken) yield break;
        isBroken = true;

        if (debugMode) Debug.Log("vBreakableObjectEnhanced: Разрушаем объект");

        // Вызываем событие перед разрушением
        onPreBreak?.Invoke();

        // Ждем один кадр для обработки событий
        yield return null;

        if (brokenObject != null)
        {
            brokenObject.gameObject.SetActive(true);
            brokenObject.transform.parent = null;

            // Применяем силу ко всем Rigidbody в разбитом объекте
            foreach (Rigidbody rb in brokenObject.GetComponentsInChildren<Rigidbody>())
            {
                if (rb != null && force != Vector3.zero)
                {
                    rb.AddForce(force, ForceMode.Impulse);
                }
            }
        }

        // Отключаем коллайдер и рендереры основного объекта
        if (_collider != null) _collider.enabled = false;
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>())
        {
            renderer.enabled = false;
        }

        // Вызываем событие после разрушения
        onPostBreak?.Invoke();

        // Отключаем ригидбоди если он есть
        if (_rigidBody != null) _rigidBody.isKinematic = true;

        // Удаляем объект через некоторое время
        Destroy(gameObject, 3f);
    }

    void OnTriggerEnter(Collider other)
    {
        // Проверка на перекатывание игрока
        if (breakOnPlayerRoll && other.gameObject.CompareTag("Player"))
        {
            var thirdPerson = other.gameObject.GetComponent<Invector.vCharacterController.vThirdPersonController>();
            if (thirdPerson && thirdPerson.isRolling && !isBroken)
            {
                if (debugMode)
                    Debug.Log($"vBreakableObjectEnhanced: Игрок перекатился через объект {gameObject.name}");
                    
                isBroken = true;
                StartCoroutine(BreakObject(Vector3.zero));
                return;
            }
        }
        
        // Проверка на удар руками
        if (breakOnHandAttack && handHitBoxLayer == (handHitBoxLayer | (1 << other.gameObject.layer)))
        {
            // Проверяем, является ли объект хитбоксом
            vHitBox hitBox = other.GetComponent<vHitBox>();
            if (hitBox != null)
            {
                // Получаем объект атаки, к которому прикреплен хитбокс
                vMeleeAttackObject attackObject = hitBox.attackObject;
                if (attackObject != null && attackObject.canApplyDamage)
                {
                    if (debugMode)
                        Debug.Log($"vBreakableObjectEnhanced: Обнаружен удар рукой по объекту {gameObject.name}");
                        
                    // Создаем данные урона
                    vDamage damage = new vDamage();
                    damage.damageValue = 10; // Стандартный урон для удара руками
                    
                    // Указываем отправителя
                    damage.sender = attackObject.transform;
                    // Устанавливаем позицию удара
                    damage.hitPosition = transform.position;
                    
                    // Применяем урон
                    TakeDamage(damage);
                }
            }
        }
    }

    void OnCollisionEnter(Collision other)
    {
        // Проверка на столкновение
        if (breakOnCollision && _rigidBody && _rigidBody.linearVelocity.magnitude > maxVelocityToBreak && !isBroken)
        {
            if (debugMode)
                Debug.Log($"vBreakableObjectEnhanced: Обнаружено столкновение с объектом {other.gameObject.name}");
                
            isBroken = true;
            StartCoroutine(BreakObject(other.relativeVelocity));
            return;
        }
        
        // Проверка на удар руками
        if (breakOnHandAttack && !isBroken)
        {
            string objectName = other.gameObject.name.ToLower();
            if (objectName.Contains("hand") || objectName.Contains("fist") || objectName.Contains("arm") || 
                objectName.Contains("forearm") || objectName.Contains("palm"))
            {
                float impactForce = other.relativeVelocity.magnitude;
                if (impactForce > minHandAttackForce)
                {
                    if (debugMode)
                        Debug.Log($"vBreakableObjectEnhanced: Обнаружен удар рукой (через столкновение) по объекту {gameObject.name}");
                        
                    // Создаем данные урона
                    vDamage damage = new vDamage(10);
                    damage.sender = other.transform;
                    damage.hitPosition = other.contacts[0].point;
                    
                    // Применяем урон
                    TakeDamage(damage);
                }
            }
        }
    }
} 