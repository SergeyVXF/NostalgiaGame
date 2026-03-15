using UnityEngine;
using UnityEngine.AI;
using Invector.vCharacterController.AI;

/// <summary>
/// Модификатор скорости для системы Invector AI
/// </summary>
public class InvectorSpeedModifier : MonoBehaviour
{
    [Tooltip("Множитель скорости для преследования (chase)")]
    [Range(0.5f, 3.0f)]
    public float chaseSpeedMultiplier = 1.5f;
    
    [Tooltip("Множитель скорости для патрулирования (patrol)")]
    [Range(0.5f, 3.0f)]
    public float patrolSpeedMultiplier = 1.2f;
    
    [Tooltip("Множитель скорости для стрейфа (strafe)")]
    [Range(0.5f, 3.0f)]
    public float strafeSpeedMultiplier = 1.3f;
    
    [Tooltip("Применить множители скорости сразу при старте")]
    public bool applyOnStart = true;
    
    // Компоненты Invector
    private v_AIMotor aiMotor;
    private NavMeshAgent navAgent;
    
    // Оригинальные значения скоростей
    private float originalChaseSpeed;
    private float originalPatrolSpeed;
    private float originalStrafeSpeed;
    private float originalNavSpeed;
    
    private void Start()
    {
        // Получаем необходимые компоненты
        aiMotor = GetComponent<v_AIMotor>();
        navAgent = GetComponent<NavMeshAgent>();
        
        if (aiMotor != null)
        {
            // Сохраняем оригинальные значения скоростей
            originalChaseSpeed = aiMotor.chaseSpeed;
            originalPatrolSpeed = aiMotor.patrolSpeed;
            originalStrafeSpeed = aiMotor.strafeSpeed;
            
            Debug.Log($"[InvectorSpeedModifier] Найден v_AIMotor на объекте {gameObject.name}. Оригинальные скорости: chase={originalChaseSpeed}, patrol={originalPatrolSpeed}, strafe={originalStrafeSpeed}");
            
            if (applyOnStart)
            {
                ApplySpeedMultipliers();
            }
        }
        else
        {
            Debug.LogWarning($"[InvectorSpeedModifier] Компонент v_AIMotor не найден на объекте {gameObject.name}. Модификатор скорости не будет работать.");
        }
        
        if (navAgent != null)
        {
            originalNavSpeed = navAgent.speed;
            Debug.Log($"[InvectorSpeedModifier] Найден NavMeshAgent на объекте {gameObject.name}. Оригинальная скорость: {originalNavSpeed}");
        }
    }
    
    /// <summary>
    /// Применяет множители ко всем типам скоростей
    /// </summary>
    public void ApplySpeedMultipliers()
    {
        if (aiMotor != null)
        {
            // Применяем множители к скоростям AI
            aiMotor.chaseSpeed = originalChaseSpeed * chaseSpeedMultiplier;
            aiMotor.patrolSpeed = originalPatrolSpeed * patrolSpeedMultiplier; 
            aiMotor.strafeSpeed = originalStrafeSpeed * strafeSpeedMultiplier;
            
            // Если есть NavMeshAgent, устанавливаем ему максимальную скорость
            if (navAgent != null)
            {
                // Выбираем наибольшую скорость как базовую для NavMeshAgent
                float maxSpeed = Mathf.Max(
                    aiMotor.chaseSpeed,
                    aiMotor.patrolSpeed,
                    aiMotor.strafeSpeed
                );
                
                navAgent.speed = originalNavSpeed * chaseSpeedMultiplier;
            }
            
            Debug.Log($"[InvectorSpeedModifier] Скорости изменены: chase={aiMotor.chaseSpeed}, patrol={aiMotor.patrolSpeed}, strafe={aiMotor.strafeSpeed}");
        }
    }
    
    /// <summary>
    /// Сбрасывает скорости к оригинальным значениям
    /// </summary>
    public void ResetSpeeds()
    {
        if (aiMotor != null)
        {
            aiMotor.chaseSpeed = originalChaseSpeed;
            aiMotor.patrolSpeed = originalPatrolSpeed;
            aiMotor.strafeSpeed = originalStrafeSpeed;
            
            if (navAgent != null)
            {
                navAgent.speed = originalNavSpeed;
            }
            
            Debug.Log($"[InvectorSpeedModifier] Скорости сброшены к оригинальным значениям");
        }
    }
    
    /// <summary>
    /// Устанавливает новый множитель скорости преследования
    /// </summary>
    public void SetChaseSpeedMultiplier(float multiplier)
    {
        chaseSpeedMultiplier = Mathf.Clamp(multiplier, 0.5f, 3.0f);
        if (aiMotor != null)
        {
            aiMotor.chaseSpeed = originalChaseSpeed * chaseSpeedMultiplier;
            Debug.Log($"[InvectorSpeedModifier] Установлен новый множитель скорости преследования: {chaseSpeedMultiplier}");
        }
    }
    
    /// <summary>
    /// Устанавливает новый множитель для всех типов скорости
    /// </summary>
    public void SetAllSpeedMultipliers(float multiplier)
    {
        chaseSpeedMultiplier = Mathf.Clamp(multiplier, 0.5f, 3.0f);
        patrolSpeedMultiplier = Mathf.Clamp(multiplier, 0.5f, 3.0f);
        strafeSpeedMultiplier = Mathf.Clamp(multiplier, 0.5f, 3.0f);
        
        ApplySpeedMultipliers();
    }
}