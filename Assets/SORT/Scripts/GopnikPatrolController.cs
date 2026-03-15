using UnityEngine;
using UnityEngine.AI;
using Invector.vCharacterController.AI;

/// <summary>
/// Скрипт для управления патрулированием гопника
/// </summary>
public class GopnikPatrolController : MonoBehaviour
{
    [Tooltip("Включить непрерывное патрулирование без остановок")]
    public bool continuousPatrol = true;
    
    [Tooltip("Расстояние для смены waypoint'а")]
    [Range(0.1f, 5f)]
    public float waypointChangeDistance = 0.5f;
    
    // Ссылки на компоненты
    private v_AIMotor aiMotor;
    private NavMeshAgent navAgent;
    
    private void Start()
    {
        // Получаем необходимые компоненты
        aiMotor = GetComponent<v_AIMotor>();
        navAgent = GetComponent<NavMeshAgent>();
        
        if (aiMotor != null)
        {
            // Устанавливаем минимальное расстояние для смены waypoint'а
            aiMotor.distanceToChangeWaypoint = waypointChangeDistance;
            
            // Отключаем остановки на waypoint'ах
            if (continuousPatrol)
            {
                aiMotor.patrollingStopDistance = 0.1f;
            }
            
            Debug.Log($"[GopnikPatrolController] Найден v_AIMotor на {gameObject.name}");
        }
        else
        {
            Debug.LogWarning($"[GopnikPatrolController] Компонент v_AIMotor не найден на {gameObject.name}");
        }
    }
    
    private void Update()
    {
        if (aiMotor != null && continuousPatrol && navAgent != null)
        {
            // Проверяем, что агент активен и находится на NavMesh
            if (navAgent.isOnNavMesh && navAgent.isStopped)
            {
                navAgent.isStopped = false;
            }
        }
    }
    
    /// <summary>
    /// Включает/выключает непрерывное патрулирование
    /// </summary>
    public void SetContinuousPatrol(bool enabled)
    {
        continuousPatrol = enabled;
        if (aiMotor != null)
        {
            aiMotor.patrollingStopDistance = enabled ? 0.1f : 0.5f;
        }
    }
} 