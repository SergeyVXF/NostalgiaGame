using UnityEngine;
using System.Collections.Generic;

public class CarSpawner : MonoBehaviour
{
    public List<GameObject> carPrefabs; // Список префабов машин
    public List<Transform> waypoints;
    [Tooltip("Индексы точек, на которых будет остановка")] public List<int> stopAtIndices;
    [Tooltip("Длительность остановки на каждой выбранной точке (сек), по порядку")] public List<float> stopDurations;
    public float spawnInterval = 15f;
    public int maxCars = 5;

    private List<GameObject> cars = new List<GameObject>();
    private float timer = 0f;
    private bool firstSpawned = false;
    private Queue<int> lastSpawnedIndices = new Queue<int>(2); // Для контроля подряд

    void Start()
    {
        // Спавним первую машину сразу
        SpawnCar();
        firstSpawned = true;
        timer = 0f;
    }

    void Update()
    {
        // Удаляем машины, которые дошли до последней точки
        for (int i = cars.Count - 1; i >= 0; i--)
        {
            var car = cars[i];
            if (car == null)
            {
                cars.RemoveAt(i);
                continue;
            }
            var ai = car.GetComponent<CarAIController>();
            if (ai != null && ai.IsAtLastWaypointReached())
            {
                Destroy(car);
                cars.RemoveAt(i);
            }
        }

        // Спавним новые машины, если нужно
        timer += Time.deltaTime;
        if (firstSpawned && timer >= spawnInterval && cars.Count < maxCars)
        {
            SpawnCar();
            timer = 0f;
        }
    }

    void SpawnCar()
    {
        if (carPrefabs == null || carPrefabs.Count == 0 || waypoints == null || waypoints.Count == 0) return;

        // Собираем индексы, которые можно использовать
        List<int> availableIndices = new List<int>();
        for (int i = 0; i < carPrefabs.Count; i++)
        {
            int count = 0;
            foreach (int idx in lastSpawnedIndices)
                if (idx == i) count++;
            if (count < 2) // Можно спавнить, если подряд было меньше 2
                availableIndices.Add(i);
        }
        // Если все варианты заняты, разрешаем любой
        if (availableIndices.Count == 0)
        {
            for (int i = 0; i < carPrefabs.Count; i++)
                availableIndices.Add(i);
        }
        int randomIndex = availableIndices[Random.Range(0, availableIndices.Count)];
        GameObject prefab = carPrefabs[randomIndex];
        GameObject car = Instantiate(prefab, waypoints[0].position, waypoints[0].rotation);
        var ai = car.GetComponent<CarAIController>();
        if (ai != null)
        {
            // Преобразуем Transform в WaypointInfo: пауза только на выбранных точках
            var wpList = new List<WaypointInfo>();
            for (int i = 0; i < waypoints.Count; i++)
            {
                float stop = 0f;
                if (stopAtIndices != null && stopAtIndices.Contains(i))
                {
                    int idx = stopAtIndices.IndexOf(i);
                    if (stopDurations != null && idx < stopDurations.Count)
                        stop = stopDurations[idx];
                }
                wpList.Add(new WaypointInfo { point = waypoints[i], stopDuration = stop });
            }
            ai.SetWaypoints(wpList);
        }
        cars.Add(car);
        // Обновляем очередь последних спавнов
        lastSpawnedIndices.Enqueue(randomIndex);
        if (lastSpawnedIndices.Count > 2)
            lastSpawnedIndices.Dequeue();
    }
} 