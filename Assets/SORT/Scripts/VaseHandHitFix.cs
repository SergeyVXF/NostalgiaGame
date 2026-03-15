using UnityEngine;
using Invector;
using Invector.vMelee;

[RequireComponent(typeof(vBreakableObject))]
public class VaseHandHitFix : MonoBehaviour
{
    private vBreakableObject breakableObject;
    
    // Слой, на котором находятся хитбоксы рук игрока (обычно 2 - IgnoreRaycast)
    [SerializeField] private LayerMask hitBoxLayer = 1 << 2;
    
    // Минимальная скорость фрагментов вазы при разрушении
    [SerializeField] private float minFragmentVelocity = 3f;
    
    // Дополнительная настройка
    [SerializeField] private bool debugMode = false;

    void Start()
    {
        breakableObject = GetComponent<vBreakableObject>();
        
        if (breakableObject == null)
        {
            Debug.LogError("VaseHandHitFix требует компонент vBreakableObject на том же GameObject");
            enabled = false;
            return;
        }
        
        // Убедимся, что коллайдер настроен правильно
        Collider col = GetComponent<Collider>();
        if (col != null && !col.isTrigger)
        {
            if (debugMode)
                Debug.Log($"VaseHandHitFix: Коллайдер объекта {gameObject.name} не является триггером. Рекомендуется установить isTrigger = true");
        }
    }

    // Обнаруживаем столкновения с хитбоксами рук
    void OnTriggerEnter(Collider other)
    {
        if (!enabled || breakableObject == null)
            return;
            
        // Проверяем, находится ли объект в слое хитбоксов
        if (hitBoxLayer == (hitBoxLayer | (1 << other.gameObject.layer)))
        {
            // Проверяем, является ли объект хитбоксом
            vHitBox hitBox = other.GetComponent<vHitBox>();
            if (hitBox != null)
            {
                if (debugMode)
                    Debug.Log($"VaseHandHitFix: Обнаружен хитбокс {other.name}");
                
                // Получаем объект атаки, к которому прикреплен хитбокс
                vMeleeAttackObject attackObject = hitBox.attackObject;
                if (attackObject != null && attackObject.canApplyDamage)
                {
                    // Создаем данные урона
                    vDamage damage = new vDamage();
                    // Если attackObject принадлежит vMeleeWeapon, используем его урон
                    vMeleeWeapon weapon = attackObject.GetComponentInParent<vMeleeWeapon>();
                    if (weapon != null)
                    {
                        damage.damageValue = weapon.damage.damageValue;
                    }
                    else
                    {
                        // Это удар руками, используем значение по умолчанию
                        damage.damageValue = 10;
                    }
                    
                    // Указываем отправителя
                    damage.sender = attackObject.transform;
                    // Устанавливаем позицию удара
                    damage.hitPosition = transform.position;
                    
                    if (debugMode)
                        Debug.Log($"VaseHandHitFix: Применение урона {damage.damageValue} к объекту {gameObject.name}");
                    
                    // Применяем урон к vBreakableObject
                    breakableObject.TakeDamage(damage);
                }
            }
        }
    }
    
    // Также обнаруживаем столкновения с обычными объектами
    void OnCollisionEnter(Collision collision)
    {
        if (!enabled || breakableObject == null)
            return;
            
        // Проверяем, является ли объект рукой игрока
        string objectName = collision.gameObject.name.ToLower();
        if (objectName.Contains("hand") || objectName.Contains("fist") || objectName.Contains("arm") || 
            objectName.Contains("forearm") || objectName.Contains("palm"))
        {
            // Проверяем силу столкновения
            float impactForce = collision.relativeVelocity.magnitude;
            if (impactForce > minFragmentVelocity)
            {
                if (debugMode)
                    Debug.Log($"VaseHandHitFix: Обнаружено столкновение с {objectName}, сила: {impactForce}");
                
                // Создаем данные урона
                vDamage damage = new vDamage(10);
                damage.sender = collision.transform;
                damage.hitPosition = collision.contacts[0].point;
                
                // Применяем урон к vBreakableObject
                breakableObject.TakeDamage(damage);
            }
        }
    }
} 