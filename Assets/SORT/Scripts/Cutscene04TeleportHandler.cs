using UnityEngine;
using System.Collections;

public class Cutscene04TeleportHandler : MonoBehaviour
{
    [Tooltip("Задержка перед телепортацией (в секундах)")]
    [SerializeField] private float teleportDelay = 3f;
    
    [Tooltip("Целевая позиция для телепортации")]
    [SerializeField] private Vector3 targetPosition = new Vector3(-77f, -5.21f, 80.8f);
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            StartCoroutine(TeleportPlayerAfterDelay(other.gameObject));
        }
    }
    
    private IEnumerator TeleportPlayerAfterDelay(GameObject player)
    {
        yield return new WaitForSeconds(teleportDelay);
        
        // Телепортируем игрока на целевую позицию
        player.transform.position = targetPosition;
        Debug.Log($"Игрок телепортирован на позицию: {targetPosition}");
    }
} 