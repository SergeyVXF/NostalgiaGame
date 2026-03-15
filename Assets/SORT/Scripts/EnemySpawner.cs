using UnityEngine;
using System.Collections.Generic;

public class EnemySpawner : MonoBehaviour
{
    [Header("Префабы врагов и их количество")]
    public GameObject enemyPrefab1;
    public int count1 = 0;
    public GameObject enemyPrefab2;
    public int count2 = 0;
    public GameObject enemyPrefab3;
    public int count3 = 0;

    private List<GameObject> spawnedEnemies = new List<GameObject>();
    public System.Action<EnemySpawner> OnCleared;

    public void SpawnEnemies()
    {
        Spawn(enemyPrefab1, count1);
        Spawn(enemyPrefab2, count2);
        Spawn(enemyPrefab3, count3);
    }

    private void Spawn(GameObject prefab, int count)
    {
        for (int i = 0; i < count; i++)
        {
            if (prefab != null)
            {
                var enemy = Instantiate(prefab, transform.position, Quaternion.identity);
                spawnedEnemies.Add(enemy);
                var enemyComponent = enemy.GetComponent<Enemy>();
                if (enemyComponent != null)
                    enemyComponent.OnDeath += () => OnEnemyDeath(enemy);
            }
        }
    }

    private void OnEnemyDeath(GameObject enemy)
    {
        spawnedEnemies.Remove(enemy);
        if (spawnedEnemies.Count == 0)
        {
            OnCleared?.Invoke(this);
        }
    }
} 