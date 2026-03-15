using UnityEngine;
using System;

public class CollectibleSphere : MonoBehaviour
{
    public event Action<GameObject> OnCollected;
    
    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"Сфера столкнулась с: {other.gameObject.name}, тег: {other.tag}");
        if (other.CompareTag("Player"))
        {
            Debug.Log("Сфера собрана игроком!");
            OnCollected?.Invoke(gameObject);
            Destroy(gameObject);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        Debug.Log($"Сфера столкнулась с: {collision.gameObject.name}, тег: {collision.gameObject.tag}");
        if (collision.gameObject.CompareTag("Player"))
        {
            Debug.Log("Сфера собрана игроком!");
            OnCollected?.Invoke(gameObject);
            Destroy(gameObject);
        }
    }
} 