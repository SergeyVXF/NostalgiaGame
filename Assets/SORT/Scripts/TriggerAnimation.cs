using UnityEngine;

public class TriggerAnimation : MonoBehaviour
{
    [Header("Animation Settings")]
    public string animationTriggerName = "IsTriggered"; // Имя параметра в аниматоре
    public Animator targetAnimator; // Аниматор, который нужно контролировать
    
    [Header("Trigger Settings")]
    public bool isTrigger = true; // Использовать ли триггер
    public bool playOnEnter = true; // Воспроизводить анимацию при входе
    public bool stopOnExit = true; // Останавливать анимацию при выходе

    private void Start()
    {
        Debug.Log($"TriggerAnimation: Скрипт запущен на объекте {gameObject.name}");
        
        // Если аниматор не назначен, пробуем найти его в разных местах
        if (targetAnimator == null)
        {
            // Сначала ищем на текущем объекте
            targetAnimator = GetComponent<Animator>();
            
            // Если не нашли на текущем объекте, ищем на родительском
            if (targetAnimator == null && transform.parent != null)
            {
                targetAnimator = transform.parent.GetComponent<Animator>();
                Debug.Log($"TriggerAnimation: Поиск аниматора на родительском объекте {transform.parent.name}");
            }
            
            // Если все еще не нашли, ищем в дочерних объектах
            if (targetAnimator == null)
            {
                targetAnimator = GetComponentInChildren<Animator>();
                Debug.Log($"TriggerAnimation: Поиск аниматора в дочерних объектах {gameObject.name}");
            }
            
            Debug.Log($"TriggerAnimation: Поиск аниматора на объекте {gameObject.name}");
        }

        // Проверяем наличие аниматора
        if (targetAnimator == null)
        {
            Debug.LogError($"TriggerAnimation: Animator не найден для объекта {gameObject.name}! Пожалуйста, назначьте аниматор в инспекторе или убедитесь, что он есть на объекте/родителе/дочерних объектах.");
            enabled = false; // Отключаем компонент, если аниматор не найден
            return;
        }
        else
        {
            Debug.Log($"TriggerAnimation: Аниматор найден на объекте {targetAnimator.gameObject.name}");
            
            // Проверяем наличие параметра в аниматоре
            bool hasParameter = false;
            foreach (AnimatorControllerParameter param in targetAnimator.parameters)
            {
                if (param.name == animationTriggerName)
                {
                    hasParameter = true;
                    Debug.Log($"TriggerAnimation: Параметр {animationTriggerName} найден в аниматоре");
                    break;
                }
            }
            
            if (!hasParameter)
            {
                Debug.LogError($"TriggerAnimation: Параметр {animationTriggerName} не найден в аниматоре! Пожалуйста, добавьте его в Animator Controller.");
                enabled = false; // Отключаем компонент, если параметр не найден
                return;
            }
        }

        // Настраиваем коллайдер
        BoxCollider boxCollider = GetComponent<BoxCollider>();
        if (boxCollider == null)
        {
            boxCollider = gameObject.AddComponent<BoxCollider>();
            Debug.Log($"TriggerAnimation: Добавлен новый BoxCollider на объект {gameObject.name}");
        }
        boxCollider.isTrigger = isTrigger;
        Debug.Log($"TriggerAnimation: BoxCollider настроен как триггер: {isTrigger}");
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!enabled) return; // Пропускаем, если компонент отключен
        
        Debug.Log($"TriggerAnimation: Объект {other.name} вошел в триггер");
        if (playOnEnter && other.CompareTag("Player"))
        {
            Debug.Log($"TriggerAnimation: Игрок вошел в триггер, пытаемся запустить анимацию");
            if (targetAnimator != null)
            {
                targetAnimator.SetBool(animationTriggerName, true);
                Debug.Log($"TriggerAnimation: Параметр {animationTriggerName} установлен в true");
                
                // Проверяем текущее состояние аниматора
                AnimatorStateInfo stateInfo = targetAnimator.GetCurrentAnimatorStateInfo(0);
                Debug.Log($"TriggerAnimation: Текущее состояние аниматора: {stateInfo.shortNameHash}");
            }
        }
        else
        {
            Debug.Log($"TriggerAnimation: Объект {other.name} не является игроком или playOnEnter отключен");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!enabled) return; // Пропускаем, если компонент отключен
        
        Debug.Log($"TriggerAnimation: Объект {other.name} вышел из триггера");
        if (stopOnExit && other.CompareTag("Player"))
        {
            Debug.Log($"TriggerAnimation: Игрок вышел из триггера, пытаемся остановить анимацию");
            if (targetAnimator != null)
            {
                targetAnimator.SetBool(animationTriggerName, false);
                Debug.Log($"TriggerAnimation: Параметр {animationTriggerName} установлен в false");
                
                // Проверяем текущее состояние аниматора
                AnimatorStateInfo stateInfo = targetAnimator.GetCurrentAnimatorStateInfo(0);
                Debug.Log($"TriggerAnimation: Текущее состояние аниматора: {stateInfo.shortNameHash}");
            }
        }
        else
        {
            Debug.Log($"TriggerAnimation: Объект {other.name} не является игроком или stopOnExit отключен");
        }
    }

    // Для отладки в редакторе
    private void OnDrawGizmos()
    {
        BoxCollider boxCollider = GetComponent<BoxCollider>();
        if (boxCollider != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(boxCollider.center, boxCollider.size);
        }
    }
} 