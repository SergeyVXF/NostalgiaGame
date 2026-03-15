using UnityEngine;
using System.Collections.Generic;

public class EnemySpawnManager : MonoBehaviour
{
    [Header("Точки спавна")]
    [SerializeField] private Transform[] spawnPoints; // Массив из 3 точек спавна
    [SerializeField] private GameObject[] enemyPrefabs; // Массив префабов врагов
    [SerializeField] private int enemiesPerSpawn = 3; // Количество врагов для спавна
    [SerializeField] private float spawnDelay = 2f; // Задержка между спавном врагов

    private int lastUsedSpawnIndex = -1; // Индекс последней использованной точки спавна
    private List<GameObject> activeEnemies = new List<GameObject>(); // Список активных врагов
    private bool isSpawning = false; // Флаг процесса спавна

    private void Start()
    {
        SpawnEnemies();
    }

    private void Update()
    {
        // Проверяем, все ли враги уничтожены
        activeEnemies.RemoveAll(enemy => enemy == null);
        
        if (activeEnemies.Count == 0 && !isSpawning)
        {
            SpawnEnemies();
        }
    }

    private void SpawnEnemies()
    {
        if (isSpawning) return;
        isSpawning = true;

        // Выбираем новую точку спавна, отличную от последней использованной
        int spawnIndex;
        do
        {
            spawnIndex = Random.Range(0, spawnPoints.Length);
        } while (spawnIndex == lastUsedSpawnIndex);

        lastUsedSpawnIndex = spawnIndex;
        Transform spawnPoint = spawnPoints[spawnIndex];

        // Спавним врагов с задержкой
        StartCoroutine(SpawnEnemiesWithDelay(spawnPoint));
    }

    private System.Collections.IEnumerator SpawnEnemiesWithDelay(Transform spawnPoint)
    {
        for (int i = 0; i < enemiesPerSpawn; i++)
        {
            // Выбираем случайный префаб врага
            int enemyIndex = Random.Range(0, enemyPrefabs.Length);
            GameObject enemy = Instantiate(enemyPrefabs[enemyIndex], spawnPoint.position, spawnPoint.rotation);
            activeEnemies.Add(enemy);
            yield return new WaitForSeconds(spawnDelay);
        }

        isSpawning = false;
    }
} 