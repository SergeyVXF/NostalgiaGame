using UnityEngine;

public class LetterSpawnPointCreator : MonoBehaviour
{
    [Header("Настройки размещения")]
    public float spawnRadius = 20f; // Радиус области размещения (уменьшен с 50f)
    public float minDistance = 5f; // Минимальное расстояние между точками (уменьшено с 10f)
    public LayerMask obstacleLayer = 1; // Слой препятствий для проверки
    public float heightOffset = 1f; // Высота над землей (уменьшена с 2f)
    
    [Header("Автоматическое создание")]
    public bool createSpawnPointsOnStart = true;
    public Transform spawnPointsParent; // Родитель для точек спавна
    
    private Transform[] spawnPoints;
    
    void Start()
    {
        if (createSpawnPointsOnStart)
        {
            CreateSpawnPoints();
        }
    }
    
    public Transform[] CreateSpawnPoints()
    {
        Debug.Log("=== Создание точек спавна букв ===");
        Debug.Log($"Радиус размещения: {spawnRadius}");
        Debug.Log($"Минимальное расстояние: {minDistance}");
        Debug.Log($"Высота над землей: {heightOffset}");
        
        spawnPoints = new Transform[7];
        
        // Создаем родительский объект для точек спавна
        if (spawnPointsParent == null)
        {
            GameObject parent = new GameObject("LetterSpawnPoints");
            spawnPointsParent = parent.transform;
            spawnPointsParent.SetParent(transform);
        }
        
        // Создаем 7 точек спавна
        for (int i = 0; i < 7; i++)
        {
            Vector3 spawnPosition = FindValidSpawnPosition();
            spawnPoints[i] = CreateSpawnPoint($"SpawnPoint_{i + 1}", spawnPosition);
            
            Debug.Log($"Создана точка спавна {i + 1}: {spawnPoints[i].name} в позиции {spawnPosition}");
        }
        
        Debug.Log("=== Создание точек спавна завершено ===");
        return spawnPoints;
    }
    
    Vector3 FindValidSpawnPosition()
    {
        int maxAttempts = 100;
        int attempts = 0;
        
        while (attempts < maxAttempts)
        {
            // Генерируем случайную позицию в круге
            Vector2 randomCircle = Random.insideUnitCircle * spawnRadius;
            Vector3 position = transform.position + new Vector3(randomCircle.x, 0, randomCircle.y);
            
            // Проверяем, что позиция не слишком близко к другим точкам
            bool isValidPosition = true;
            for (int i = 0; i < 7; i++)
            {
                if (spawnPoints[i] != null)
                {
                    float distance = Vector3.Distance(position, spawnPoints[i].position);
                    if (distance < minDistance)
                    {
                        isValidPosition = false;
                        break;
                    }
                }
            }
            
            // Проверяем, что нет препятствий
            if (isValidPosition)
            {
                RaycastHit hit;
                if (Physics.Raycast(position + Vector3.up * 10f, Vector3.down, out hit, 20f, obstacleLayer))
                {
                    position.y = hit.point.y + heightOffset;
                    return position;
                }
                else
                {
                    // Если нет препятствий, размещаем на уровне земли
                    position.y = transform.position.y + heightOffset;
                    return position;
                }
            }
            
            attempts++;
        }
        
        // Если не удалось найти подходящую позицию, возвращаем случайную
        Vector2 fallbackCircle = Random.insideUnitCircle * spawnRadius;
        Vector3 fallbackPosition = transform.position + new Vector3(fallbackCircle.x, heightOffset, fallbackCircle.y);
        return fallbackPosition;
    }
    
    Transform CreateSpawnPoint(string name, Vector3 position)
    {
        GameObject spawnPoint = new GameObject(name);
        spawnPoint.transform.SetParent(spawnPointsParent);
        spawnPoint.transform.position = position;
        
        // Добавляем визуальный маркер (опционально)
        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        marker.transform.SetParent(spawnPoint.transform);
        marker.transform.localPosition = Vector3.zero;
        marker.transform.localScale = Vector3.one * 0.5f;
        
        // Настраиваем материал маркера
        Renderer markerRenderer = marker.GetComponent<Renderer>();
        Material markerMaterial = new Material(Shader.Find("Standard"));
        markerMaterial.color = Color.green;
        markerMaterial.EnableKeyword("_EMISSION");
        markerMaterial.SetColor("_EmissionColor", Color.green * 0.5f);
        markerRenderer.material = markerMaterial;
        
        // Удаляем коллайдер маркера
        DestroyImmediate(marker.GetComponent<Collider>());
        
        return spawnPoint.transform;
    }
    
    // Метод для получения созданных точек спавна
    public Transform[] GetSpawnPoints()
    {
        return spawnPoints;
    }
    
    // Метод для очистки точек спавна
    public void ClearSpawnPoints()
    {
        if (spawnPointsParent != null)
        {
            DestroyImmediate(spawnPointsParent.gameObject);
            spawnPoints = null;
        }
    }
    
    // Метод для ручного создания точек спавна в определенных позициях
    public Transform[] CreateSpawnPointsAtPositions(Vector3[] positions)
    {
        if (positions.Length != 7)
        {
            Debug.LogError("Количество позиций должно быть равно 7!");
            return null;
        }
        
        spawnPoints = new Transform[7];
        
        if (spawnPointsParent == null)
        {
            GameObject parent = new GameObject("LetterSpawnPoints");
            spawnPointsParent = parent.transform;
            spawnPointsParent.SetParent(transform);
        }
        
        for (int i = 0; i < 7; i++)
        {
            spawnPoints[i] = CreateSpawnPoint($"SpawnPoint_{i + 1}", positions[i]);
        }
        
        return spawnPoints;
    }
} 