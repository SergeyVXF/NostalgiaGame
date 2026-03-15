using UnityEngine;
using UnityEditor;
using Invector.vCharacterController;
using System.Collections.Generic;
using System.Linq;

public class SugrobController : MonoBehaviour
{
    [Header("Эффекты")]
    public ParticleSystem snowParticles;
    public GameObject sugrobAfterPrefab;
    
    [Header("Настройки")]
    [Tooltip("Минимальная скорость падения для активации (в м/с)")]
    public float minFallSpeed = 2f;
    [Tooltip("Время жизни сугроба после прыжка (в секундах)")]
    public float afterSnowLifetime = 15f;
    
    [Header("Отладка")]
    public bool showDebugInfo = true;

    [Header("Параметры спавна")]
    [Tooltip("Высота, на которой будут появляться эффекты (относительно позиции сугроба)")]
    public float spawnHeight = 0f;
    
    [Tooltip("Смещение позиции для sugrobAfterPrefab")] 
    public Vector3 afterPrefabPositionOffset = Vector3.zero;
    [Tooltip("Поворот (Euler) для sugrobAfterPrefab")] 
    public Vector3 afterPrefabRotationEuler = Vector3.zero;

    private void OnTriggerEnter(Collider other)
    {
        // Получаем компонент игрока
        vThirdPersonController player = other.GetComponent<vThirdPersonController>();
        if (player == null) return;

        // Получаем вертикальную скорость (отрицательная при падении вниз)
        float verticalSpeed = -player.verticalVelocity;

        if (showDebugInfo)
        {
            Debug.Log($"<color=yellow>Столкновение с игроком:\n" +
                $"Вертикальная скорость: {verticalSpeed:F2} м/с\n" +
                $"Требуемая скорость: {minFallSpeed} м/с</color>");
        }

        // Проверяем, падает ли игрок сверху с достаточной скоростью
        if (verticalSpeed >= minFallSpeed)
        {
            if (showDebugInfo)
            {
                Debug.Log($"<color=green>Сугроб активирован!\n" +
                    $"Скорость падения: {verticalSpeed:F2} м/с</color>");
            }

            // Создаем эффект снега
            if (snowParticles != null)
            {
                Vector3 spawnPos = transform.position + Vector3.up * spawnHeight;
                ParticleSystem particles = Instantiate(snowParticles, spawnPos, Quaternion.identity);
                particles.Play();
                Destroy(particles.gameObject, particles.main.duration);
            }

            // Создаем новый сугроб (если задан)
            if (sugrobAfterPrefab != null)
            {
                Vector3 spawnPos = transform.position + Vector3.up * spawnHeight + afterPrefabPositionOffset;
                Quaternion spawnRot = Quaternion.Euler(afterPrefabRotationEuler) * transform.rotation;
                GameObject afterSnow = Instantiate(sugrobAfterPrefab, spawnPos, spawnRot);
                Destroy(afterSnow, afterSnowLifetime);
            }

            // Уничтожаем текущий сугроб
            Destroy(gameObject);
        }
    }

    private void OnDrawGizmos()
    {
        // Рисуем стрелку, показывающую минимальную скорость падения
        Gizmos.color = Color.red;
        Vector3 start = transform.position + Vector3.up * 2;
        Vector3 end = start + Vector3.down * minFallSpeed;
        Gizmos.DrawLine(start, end);
        
        // Рисуем конус стрелки
        float arrowSize = 0.2f;
        Vector3 right = end + Vector3.up * arrowSize + Vector3.right * arrowSize;
        Vector3 left = end + Vector3.up * arrowSize - Vector3.right * arrowSize;
        Gizmos.DrawLine(end, right);
        Gizmos.DrawLine(end, left);
    }
} 