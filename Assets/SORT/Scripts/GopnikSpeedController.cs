using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Скрипт для управления скоростью движения гопника
/// </summary>
public class GopnikSpeedController : MonoBehaviour
{
    [Tooltip("Множитель скорости движения")]
    [Range(0.5f, 5f)]
    public float speedMultiplier = 1.5f;
    
    [Tooltip("Множитель скорости при прохождении NavMesh Link")]
    [Range(1f, 10f)]
    public float linkSpeedMultiplier = 2f;
    
    [Tooltip("Применить скорость сразу при старте")]
    public bool applyOnStart = true;
    
    // Ссылки на компоненты
    private NavMeshAgent navAgent;
    private Animator animator;
    
    private float originalSpeed = 0f;
    private bool speedApplied = false;
    
    private void Start()
    {
        // Пытаемся найти компоненты для управления движением
        navAgent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        
        if (navAgent != null)
        {
            // Запоминаем изначальную скорость
            originalSpeed = navAgent.speed;
            Debug.Log($"[GopnikSpeedController] Найден NavMeshAgent на {gameObject.name}, базовая скорость: {originalSpeed}");
            
            if (applyOnStart)
            {
                ApplySpeedMultiplier();
            }
        }
        else
        {
            Debug.LogWarning($"[GopnikSpeedController] NavMeshAgent не найден на {gameObject.name}. Проверьте, как реализовано движение.");
        }
    }
    
    private void Update()
    {
        if (navAgent != null)
        {
            // Если AI находится на NavMesh Link, увеличиваем скорость
            if (navAgent.isOnOffMeshLink)
            {
                navAgent.speed = originalSpeed * linkSpeedMultiplier;
            }
            else
            {
                navAgent.speed = originalSpeed * speedMultiplier;
            }
        }
    }
    
    /// <summary>
    /// Применяет множитель к скорости движения
    /// </summary>
    public void ApplySpeedMultiplier()
    {
        if (navAgent != null && !speedApplied)
        {
            navAgent.speed = originalSpeed * speedMultiplier;
            speedApplied = true;
            Debug.Log($"[GopnikSpeedController] Скорость изменена на {navAgent.speed}");
        }
    }
    
    /// <summary>
    /// Сбрасывает скорость к оригинальному значению
    /// </summary>
    public void ResetSpeed()
    {
        if (navAgent != null)
        {
            navAgent.speed = originalSpeed;
            speedApplied = false;
            Debug.Log($"[GopnikSpeedController] Скорость сброшена к {originalSpeed}");
        }
    }
    
    /// <summary>
    /// Устанавливает новый множитель скорости
    /// </summary>
    public void SetSpeedMultiplier(float multiplier)
    {
        speedMultiplier = Mathf.Clamp(multiplier, 0.5f, 5f);
        
        if (speedApplied)
        {
            ApplySpeedMultiplier(); // Пересчитываем скорость с новым множителем
        }
    }
}