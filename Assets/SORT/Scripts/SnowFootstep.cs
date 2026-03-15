using UnityEngine;

public class SnowFootstep : MonoBehaviour
{
    [Header("Префаб системы частиц снега")]
    public ParticleSystem snowParticlePrefab;
    
    [Header("Настройки слоев террейна")]
    [Tooltip("Имя слоя террейна для снега (как в Terrain Layers)")]
    public string snowLayerName = "Snow Layer 03";
    
    [Tooltip("Дополнительные слои террейна для активации эффектов")]
    public TerrainLayerConfig[] additionalLayers = new TerrainLayerConfig[]
    {
        new TerrainLayerConfig { layerName = "Grass Layer", particlePrefab = null, minLayerValue = 0.3f },
        new TerrainLayerConfig { layerName = "Dirt Layer", particlePrefab = null, minLayerValue = 0.4f },
        new TerrainLayerConfig { layerName = "Sand Layer", particlePrefab = null, minLayerValue = 0.3f }
    };
    
    [Header("Настройки обычных слоев Unity")]
    [Tooltip("Обычные слои Unity для активации эффектов")]
    public UnityLayerConfig[] unityLayers = new UnityLayerConfig[]
    {
        new UnityLayerConfig { layerName = "Ground", particlePrefab = null },
        new UnityLayerConfig { layerName = "Water", particlePrefab = null },
        new UnityLayerConfig { layerName = "Metal", particlePrefab = null }
    };
    
    [Header("Дистанция луча вниз")]
    public float raycastDistance = 2f;
    
    [Header("Минимальное значение слоя для срабатывания (0.5 = только чистый снег)")]
    [Range(0f, 1f)] public float minLayerValue = 0.5f;
    
    [Header("Объект, к которому будет прикреплён снег (например, нога игрока)")]
    public Transform attachTo;
    
    [System.Serializable]
    public class TerrainLayerConfig
    {
        [Tooltip("Имя слоя террейна")]
        public string layerName;
        
        [Tooltip("Префаб частиц для этого слоя (если null, используется основной snowParticlePrefab)")]
        public ParticleSystem particlePrefab;
        
        [Tooltip("Минимальное значение слоя для срабатывания")]
        [Range(0f, 1f)] public float minLayerValue = 0.3f;
        
        [Tooltip("Активирован ли этот слой")]
        public bool isActive = true;
        
        [HideInInspector] public int layerIndex = -1;
        [HideInInspector] public ParticleSystem activeParticle;
    }
    
    [System.Serializable]
    public class UnityLayerConfig
    {
        [Tooltip("Имя слоя Unity (как в Layer Settings)")]
        public string layerName;
        
        [Tooltip("Префаб частиц для этого слоя (если null, используется основной snowParticlePrefab)")]
        public ParticleSystem particlePrefab;
        
        [Tooltip("Активирован ли этот слой")]
        public bool isActive = true;
        
        [HideInInspector] public int layerIndex = -1;
        [HideInInspector] public ParticleSystem activeParticle;
    }

    private Terrain terrain;
    private int snowLayerIndex = -1;
    private Vector3 lastPosition;
    public float minMoveDistance = 0.1f; // Минимальное смещение для срабатывания
    public float stepInterval = 0.3f; // Интервал между спавнами частиц (сек)
    private float stepTimer = 0f;
    private ParticleSystem activeSnowParticle;
    private bool wasOnSnowLayer = false;
    private bool forceSnowActive = false;
    private TerrainLayerConfig currentActiveLayer = null;
    private UnityLayerConfig currentActiveUnityLayer = null;

    void Start()
    {
        terrain = Terrain.activeTerrain;
        if (terrain == null)
        {
            Debug.LogError("Terrain не найден!");
            return;
        }
        
        // Инициализируем основной слой снега
        InitializeSnowLayer();
        
        // Инициализируем дополнительные слои
        InitializeAdditionalLayers();
        
        // Инициализируем обычные слои Unity
        InitializeUnityLayers();
        
        lastPosition = transform.position;
    }
    
    private void InitializeSnowLayer()
    {
        var layers = terrain.terrainData.terrainLayers;
        for (int i = 0; i < layers.Length; i++)
        {
            if (layers[i].name == snowLayerName)
            {
                snowLayerIndex = i;
                break;
            }
        }
        if (snowLayerIndex == -1)
            Debug.LogError($"Слой '{snowLayerName}' не найден в Terrain Layers!");
    }
    
    private void InitializeAdditionalLayers()
    {
        var layers = terrain.terrainData.terrainLayers;
        
        for (int i = 0; i < additionalLayers.Length; i++)
        {
            var layerConfig = additionalLayers[i];
            if (!layerConfig.isActive) continue;
            
            // Ищем индекс слоя в террейне
            for (int j = 0; j < layers.Length; j++)
            {
                if (layers[j].name == layerConfig.layerName)
                {
                    layerConfig.layerIndex = j;
                    Debug.Log($"Найден слой '{layerConfig.layerName}' с индексом {j}");
                    break;
                }
            }
            
            if (layerConfig.layerIndex == -1)
            {
                Debug.LogWarning($"Слой '{layerConfig.layerName}' не найден в Terrain Layers!");
            }
        }
    }
    
    private void InitializeUnityLayers()
    {
        for (int i = 0; i < unityLayers.Length; i++)
        {
            var layerConfig = unityLayers[i];
            if (!layerConfig.isActive) continue;
            
            // Получаем индекс слоя Unity по имени
            layerConfig.layerIndex = LayerMask.NameToLayer(layerConfig.layerName);
            
            if (layerConfig.layerIndex == -1)
            {
                Debug.LogWarning($"Слой Unity '{layerConfig.layerName}' не найден! Проверьте Layer Settings.");
            }
            else
            {
                Debug.Log($"Найден слой Unity '{layerConfig.layerName}' с индексом {layerConfig.layerIndex}");
            }
        }
    }

    void Update()
    {
        float moveDist = Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z), new Vector3(lastPosition.x, 0, lastPosition.z));
        bool isMoving = moveDist > 0.001f;
        
        // Проверяем все слои (террейн и Unity слои)
        TerrainLayerConfig activeTerrainLayer = CheckAllTerrainLayers();
        UnityLayerConfig activeUnityLayer = CheckAllUnityLayers();
        
        bool shouldShowEffect = ((activeTerrainLayer != null || activeUnityLayer != null) && isMoving) || forceSnowActive;

        if (shouldShowEffect)
        {
            // Приоритет: сначала террейн слои, потом Unity слои
            if (activeTerrainLayer != null)
            {
                ActivateTerrainLayerEffect(activeTerrainLayer);
            }
            else if (activeUnityLayer != null)
            {
                ActivateUnityLayerEffect(activeUnityLayer);
            }
        }
        else
        {
            // Деактивируем все эффекты
            DeactivateAllEffects();
        }
        
        wasOnSnowLayer = ((activeTerrainLayer != null || activeUnityLayer != null) && isMoving);
        if (moveDist > 0.0001f)
            lastPosition = transform.position;
    }
    
    private TerrainLayerConfig CheckAllTerrainLayers()
    {
        // Сначала проверяем основной слой снега
        if (IsOnSnowLayer())
        {
            return new TerrainLayerConfig 
            { 
                layerName = snowLayerName, 
                particlePrefab = snowParticlePrefab, 
                minLayerValue = minLayerValue 
            };
        }
        
        // Затем проверяем дополнительные слои
        foreach (var layerConfig in additionalLayers)
        {
            if (!layerConfig.isActive || layerConfig.layerIndex == -1) continue;
            
            if (IsOnTerrainLayer(layerConfig.layerIndex, layerConfig.minLayerValue))
            {
                return layerConfig;
            }
        }
        
        return null;
    }
    
    private UnityLayerConfig CheckAllUnityLayers()
    {
        // Проверяем все Unity слои
        foreach (var layerConfig in unityLayers)
        {
            if (!layerConfig.isActive || layerConfig.layerIndex == -1) continue;
            
            if (IsOnUnityLayer(layerConfig.layerIndex))
            {
                return layerConfig;
            }
        }
        
        return null;
    }
    
    private void ActivateTerrainLayerEffect(TerrainLayerConfig layerConfig)
    {
        if (layerConfig == null) return;
        
        // Если это новый слой, деактивируем предыдущий
        if (currentActiveLayer != layerConfig)
        {
            DeactivateAllEffects();
            currentActiveLayer = layerConfig;
        }
        
        // Определяем какой префаб частиц использовать
        ParticleSystem particlePrefab = layerConfig.particlePrefab != null ? 
            layerConfig.particlePrefab : snowParticlePrefab;
        
        // Активируем эффект
        if (particlePrefab != null)
        {
            if (activeSnowParticle == null)
            {
                Transform parent = attachTo != null ? attachTo : transform;
                activeSnowParticle = Instantiate(particlePrefab, parent);
                activeSnowParticle.transform.localPosition = Vector3.zero;
            }
            
            if (activeSnowParticle != null)
            {
                var emission = activeSnowParticle.emission;
                emission.enabled = true;
            }
        }
    }
    
    private void ActivateUnityLayerEffect(UnityLayerConfig layerConfig)
    {
        if (layerConfig == null) return;
        
        // Если это новый слой, деактивируем предыдущий
        if (currentActiveUnityLayer != layerConfig)
        {
            DeactivateAllEffects();
            currentActiveUnityLayer = layerConfig;
        }
        
        // Определяем какой префаб частиц использовать
        ParticleSystem particlePrefab = layerConfig.particlePrefab != null ? 
            layerConfig.particlePrefab : snowParticlePrefab;
        
        // Активируем эффект
        if (particlePrefab != null)
        {
            if (activeSnowParticle == null)
            {
                Transform parent = attachTo != null ? attachTo : transform;
                activeSnowParticle = Instantiate(particlePrefab, parent);
                activeSnowParticle.transform.localPosition = Vector3.zero;
            }
            
            if (activeSnowParticle != null)
            {
                var emission = activeSnowParticle.emission;
                emission.enabled = true;
            }
        }
    }
    
    private void DeactivateAllEffects()
    {
        if (activeSnowParticle != null)
        {
            var emission = activeSnowParticle.emission;
            emission.enabled = false;
        }
        
        // Деактивируем эффекты дополнительных слоев террейна
        foreach (var layerConfig in additionalLayers)
        {
            if (layerConfig.activeParticle != null)
            {
                var emission = layerConfig.activeParticle.emission;
                emission.enabled = false;
            }
        }
        
        // Деактивируем эффекты Unity слоев
        foreach (var layerConfig in unityLayers)
        {
            if (layerConfig.activeParticle != null)
            {
                var emission = layerConfig.activeParticle.emission;
                emission.enabled = false;
            }
        }
        
        currentActiveLayer = null;
        currentActiveUnityLayer = null;
    }

    // Вызовите этот метод из анимации шага или системы движения
    public void TrySpawnSnowFootstep()
    {
        TerrainLayerConfig activeTerrainLayer = CheckAllTerrainLayers();
        UnityLayerConfig activeUnityLayer = CheckAllUnityLayers();
        
        if (activeTerrainLayer != null)
        {
            SpawnTerrainLayerParticles(activeTerrainLayer);
        }
        else if (activeUnityLayer != null)
        {
            SpawnUnityLayerParticles(activeUnityLayer);
        }
    }

    // Вызовите этот метод при взаимодействии с объектом с тегом SnowRT
    public void ActivateSnowByTag(float duration = 1.5f)
    {
        if (!forceSnowActive)
        {
            forceSnowActive = true;
            if (activeSnowParticle == null && snowParticlePrefab != null)
            {
                Transform parent = attachTo != null ? attachTo : transform;
                activeSnowParticle = Instantiate(snowParticlePrefab, parent);
                activeSnowParticle.transform.localPosition = Vector3.zero;
            }
            if (activeSnowParticle != null)
            {
                var emission = activeSnowParticle.emission;
                emission.enabled = true;
            }
            // Отключить через duration секунд
            Invoke(nameof(DeactivateSnowByTag), duration);
        }
    }
    
    // Активирует эффект для конкретного слоя террейна
    public void ActivateTerrainLayerByTag(string layerName, float duration = 1.5f)
    {
        TerrainLayerConfig targetLayer = null;
        
        // Ищем слой по имени
        foreach (var layerConfig in additionalLayers)
        {
            if (layerConfig.layerName == layerName && layerConfig.isActive)
            {
                targetLayer = layerConfig;
                break;
            }
        }
        
        if (targetLayer != null)
        {
            ActivateTerrainLayerEffect(targetLayer);
            Invoke(nameof(DeactivateSnowByTag), duration);
        }
    }
    
    // Активирует эффект для конкретного Unity слоя
    public void ActivateUnityLayerByTag(string layerName, float duration = 1.5f)
    {
        UnityLayerConfig targetLayer = null;
        
        // Ищем слой по имени
        foreach (var layerConfig in unityLayers)
        {
            if (layerConfig.layerName == layerName && layerConfig.isActive)
            {
                targetLayer = layerConfig;
                break;
            }
        }
        
        if (targetLayer != null)
        {
            ActivateUnityLayerEffect(targetLayer);
            Invoke(nameof(DeactivateSnowByTag), duration);
        }
    }
    
    private void DeactivateSnowByTag()
    {
        forceSnowActive = false;
    }

    bool IsOnSnowLayer()
    {
        return IsOnTerrainLayer(snowLayerIndex, minLayerValue);
    }
    
    bool IsOnTerrainLayer(int layerIndex, float minValue)
    {
        if (layerIndex == -1) return false;
        
        RaycastHit hit;
        if (Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down, out hit, raycastDistance))
        {
            if (hit.collider.GetComponent<Terrain>() != null)
            {
                Vector3 terrainPos = hit.point - terrain.transform.position;
                TerrainData tData = terrain.terrainData;
                int mapX = Mathf.RoundToInt((terrainPos.x / tData.size.x) * tData.alphamapWidth);
                int mapZ = Mathf.RoundToInt((terrainPos.z / tData.size.z) * tData.alphamapHeight);
                float[,,] splatmapData = tData.GetAlphamaps(mapX, mapZ, 1, 1);
                float value = splatmapData[0, 0, layerIndex];
                return value >= minValue;
            }
        }
        return false;
    }
    
    bool IsOnUnityLayer(int layerIndex)
    {
        if (layerIndex == -1) return false;
        
        RaycastHit hit;
        if (Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down, out hit, raycastDistance))
        {
            // Проверяем, находится ли объект на нужном слое
            return hit.collider.gameObject.layer == layerIndex;
        }
        return false;
    }

    void SpawnSnowParticles()
    {
        if (snowParticlePrefab != null)
        {
            Instantiate(snowParticlePrefab, transform.position, Quaternion.identity);
        }
    }
    
    void SpawnTerrainLayerParticles(TerrainLayerConfig layerConfig)
    {
        ParticleSystem particlePrefab = layerConfig.particlePrefab != null ? 
            layerConfig.particlePrefab : snowParticlePrefab;
            
        if (particlePrefab != null)
        {
            Instantiate(particlePrefab, transform.position, Quaternion.identity);
        }
    }
    
    void SpawnUnityLayerParticles(UnityLayerConfig layerConfig)
    {
        ParticleSystem particlePrefab = layerConfig.particlePrefab != null ? 
            layerConfig.particlePrefab : snowParticlePrefab;
            
        if (particlePrefab != null)
        {
            Instantiate(particlePrefab, transform.position, Quaternion.identity);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Безопасная проверка тегов - избегаем ошибок с неопределенными тегами
        string tag = other.tag;
        
        if (tag == "SnowRT")
        {
            ActivateSnowByTag();
        }
        else if (tag == "GrassRT")
        {
            ActivateTerrainLayerByTag("Grass Layer");
        }
        else if (tag == "DirtRT")
        {
            ActivateTerrainLayerByTag("Dirt Layer");
        }
        else if (tag == "SandRT")
        {
            ActivateTerrainLayerByTag("Sand Layer");
        }
        else if (tag == "GroundRT")
        {
            ActivateUnityLayerByTag("Ground");
        }
        else if (tag == "WaterRT")
        {
            ActivateUnityLayerByTag("Water");
        }
        else if (tag == "MetalRT")
        {
            ActivateUnityLayerByTag("Metal");
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Безопасная проверка тегов - избегаем ошибок с неопределенными тегами
        string tag = collision.collider.tag;
        
        if (tag == "SnowRT")
        {
            ActivateSnowByTag();
        }
        else if (tag == "GrassRT")
        {
            ActivateTerrainLayerByTag("Grass Layer");
        }
        else if (tag == "DirtRT")
        {
            ActivateTerrainLayerByTag("Dirt Layer");
        }
        else if (tag == "SandRT")
        {
            ActivateTerrainLayerByTag("Sand Layer");
        }
        else if (tag == "GroundRT")
        {
            ActivateUnityLayerByTag("Ground");
        }
        else if (tag == "WaterRT")
        {
            ActivateUnityLayerByTag("Water");
        }
        else if (tag == "MetalRT")
        {
            ActivateUnityLayerByTag("Metal");
        }
    }
} 