using System.Collections.Generic;
using UnityEngine;

namespace MiniVanGame
{
    [ExecuteAlways]
    public sealed class MiniVanGameModeMapGenerator : MonoBehaviour
    {
        [Header("Generation")]
        public int Seed = 101;
        public bool GenerateOnStart;

        [Header("Map")]
        [Min(320f)] public float MapSize = 720f;
        [Min(320f)] public float MapLength = 4320f;
        [Min(30f)] public float TerrainHeight = 64f;
        [Range(129, 513)] public int HeightmapResolution = 513;
        [Min(1f)] public float RoadElevation = 14f;

        [Header("Road")]
        [Min(6f)] public float RoadWidth = 11f;
        [Min(10f)] public float RoadClearRadius = 18f;
        [Min(20f)] public float HillFullDistance = 42f;
        [Min(16f)] public float HillHeight = 38f;
        [Range(8, 32)] public int SamplesPerSegment = 18;

        [Header("River")]
        [Min(8f)] public float RiverHalfWidth = 16f;
        [Min(4f)] public float RiverBankWidth = 10f;
        [Min(0.5f)] public float RiverBedHeight = 1.5f;
        [Min(1f)] public float RiverWaterHeight = 5.5f;

        [Header("Forest")]
        public bool GenerateForest;
        [Min(6f)] public float TreeGridStep = 11f;
        [Min(10f)] public float DenseTreeBand = 54f;
        [Range(0f, 1f)] public float DenseTreeChance = 0.72f;
        [Range(0f, 1f)] public float OuterTreeChance = 0.16f;
        [Min(50)] public int MaximumTrees = 650;

        [Header("Persistent Assets")]
        public TerrainData TerrainDataAsset;
        public TerrainLayer GrassTerrainLayer;
        public TerrainLayer RockTerrainLayer;
        public TerrainLayer RoadTerrainLayer;
        public Material GrassMaterial;
        public Material RoadMaterial;
        public Material WaterMaterial;
        public Material StartMaterial;
        public Material SaveMaterial;
        public Material TreeTrunkMaterial;
        public Material TreeLeavesMaterial;

        public Vector3 StartPosition { get; private set; }
        public Vector3 SavePosition { get; private set; }
        public IReadOnlyList<Vector3> RoadSamples => roadSamples;

        private const string GeneratedRootName = "Generated_GameMode_Map";
        private readonly List<Vector3> roadControls = new List<Vector3>();
        private readonly List<Vector3> roadSamples = new List<Vector3>();
        private readonly List<Vector4> mountainMassifs = new List<Vector4>();
        private Transform generatedRoot;
        private Terrain generatedTerrain;

        private void Start()
        {
            EnsureRuntimeReady();
        }

        public void EnsureRuntimeReady()
        {
            if (!Application.isPlaying)
                return;

            Transform existingRoot = transform.Find(GeneratedRootName);
            Terrain existingTerrain = existingRoot != null
                ? existingRoot.GetComponentInChildren<Terrain>(true)
                : null;

            if (existingTerrain == null || existingTerrain.terrainData == null)
            {
                Rebuild();
                return;
            }

            generatedRoot = existingRoot;
            generatedTerrain = existingTerrain;
            generatedRoot.gameObject.SetActive(true);
            generatedTerrain.enabled = true;
            generatedTerrain.drawHeightmap = true;
            generatedTerrain.drawTreesAndFoliage = GenerateForest;
            generatedTerrain.gameObject.SetActive(true);

            if (roadSamples.Count < 2)
            {
                Random.State previousRandomState = Random.state;
                Random.InitState(Seed);
                try
                {
                    BuildRoute();
                }
                finally
                {
                    Random.state = previousRandomState;
                }
            }
        }

        [ContextMenu("Rebuild Stage 1 Map")]
        public void Rebuild()
        {
            ClearGenerated();

            Random.State previousRandomState = Random.state;
            Random.InitState(Seed);
            try
            {
                generatedRoot = new GameObject(GeneratedRootName).transform;
                generatedRoot.SetParent(transform, false);

                BuildRoute();
                BuildTerrain();
                BuildRiverSurface();
                BuildZoneMarkers();
                if (GenerateForest)
                {
                    BuildForest();
                }
            }
            finally
            {
                Random.state = previousRandomState;
            }
        }

        [ContextMenu("Clear Stage 1 Map")]
        public void ClearGenerated()
        {
            Transform oldRoot = transform.Find(GeneratedRootName);
            if (oldRoot == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(oldRoot.gameObject);
            }
            else
            {
                DestroyImmediate(oldRoot.gameObject);
            }
        }

        private void BuildRoute()
        {
            roadControls.Clear();
            roadSamples.Clear();

            float startX = MapSize * Random.Range(0.075f, 0.11f);
            float saveX = MapSize * Random.Range(0.89f, 0.925f);
            StartPosition = new Vector3(startX, RoadElevation, MapLength * 0.075f);
            SavePosition = new Vector3(saveX, RoadElevation, MapLength * 0.925f);

            roadControls.Add(StartPosition);
            roadControls.Add(new Vector3(MapSize * Random.Range(0.12f, 0.21f), RoadElevation, MapLength * 0.24f));
            roadControls.Add(new Vector3(MapSize * Random.Range(0.68f, 0.79f), RoadElevation, MapLength * 0.31f));
            roadControls.Add(new Vector3(MapSize * Random.Range(0.73f, 0.84f), RoadElevation, MapLength * 0.48f));
            roadControls.Add(new Vector3(MapSize * Random.Range(0.19f, 0.31f), RoadElevation, MapLength * 0.57f));
            roadControls.Add(new Vector3(MapSize * Random.Range(0.16f, 0.27f), RoadElevation, MapLength * 0.75f));
            roadControls.Add(new Vector3(MapSize * Random.Range(0.66f, 0.78f), RoadElevation, MapLength * 0.82f));
            roadControls.Add(SavePosition);

            for (int segment = 0; segment < roadControls.Count - 1; segment++)
            {
                Vector3 a = roadControls[Mathf.Max(0, segment - 1)];
                Vector3 b = roadControls[segment];
                Vector3 c = roadControls[segment + 1];
                Vector3 d = roadControls[Mathf.Min(roadControls.Count - 1, segment + 2)];

                for (int sample = 0; sample < SamplesPerSegment; sample++)
                {
                    float t = sample / (float)SamplesPerSegment;
                    Vector3 point = CatmullRom(a, b, c, d, t);
                    point.y = RoadElevation + 0.18f;
                    roadSamples.Add(point);
                }
            }

            Vector3 finalPoint = SavePosition;
            finalPoint.y = RoadElevation + 0.18f;
            roadSamples.Add(finalPoint);
            BuildMountainMassifs();
        }

        private void BuildMountainMassifs()
        {
            mountainMassifs.Clear();
            const int desiredCount = 6;
            const int maximumAttempts = 240;
            for (int attempt = 0; attempt < maximumAttempts && mountainMassifs.Count < desiredCount; attempt++)
            {
                Vector2 candidate = new Vector2(
                    Random.Range(MapSize * 0.07f, MapSize * 0.93f),
                    Random.Range(MapLength * 0.09f, MapLength * 0.91f));
                if (DistanceToRoad(candidate) < 32f ||
                    Mathf.Abs(candidate.x - RiverCenterX(candidate.y)) < RiverHalfWidth + RiverBankWidth + 30f ||
                    DistanceToRectangle(candidate, new Vector2(StartPosition.x, StartPosition.z), new Vector2(55f, 48f)) < 8f ||
                    DistanceToRectangle(candidate, new Vector2(SavePosition.x, SavePosition.z), new Vector2(55f, 48f)) < 8f)
                {
                    continue;
                }

                bool overlaps = false;
                for (int i = 0; i < mountainMassifs.Count; i++)
                {
                    Vector2 existing = new Vector2(mountainMassifs[i].x, mountainMassifs[i].y);
                    if (Vector2.Distance(candidate, existing) < 88f)
                    {
                        overlaps = true;
                        break;
                    }
                }
                if (overlaps)
                {
                    continue;
                }

                mountainMassifs.Add(new Vector4(
                    candidate.x,
                    candidate.y,
                    Random.Range(68f, 104f),
                    Random.Range(27f, 43f)));
            }
        }

        private void BuildTerrain()
        {
            TerrainData data = TerrainDataAsset != null
                ? (Application.isPlaying ? Instantiate(TerrainDataAsset) : TerrainDataAsset)
                : new TerrainData();
            if (Application.isPlaying)
            {
                data.name = "Game_v01 Runtime Terrain Data";
            }
            int resolution = Mathf.ClosestPowerOfTwo(Mathf.Clamp(HeightmapResolution - 1, 128, 512)) + 1;
            if (data.heightmapResolution != resolution)
            {
                data.heightmapResolution = resolution;
            }
            data.size = new Vector3(MapSize, TerrainHeight, MapLength);
            if (GrassTerrainLayer != null && RockTerrainLayer != null && RoadTerrainLayer != null)
            {
                data.terrainLayers = new[] { GrassTerrainLayer, RockTerrainLayer, RoadTerrainLayer };
            }
            else if (GrassTerrainLayer != null && RockTerrainLayer != null)
            {
                data.terrainLayers = new[] { GrassTerrainLayer, RockTerrainLayer };
            }
            else if (GrassTerrainLayer != null)
            {
                data.terrainLayers = new[] { GrassTerrainLayer };
            }
            data.SetHeights(0, 0, BuildHeightMap(resolution));
            PaintTerrain(data);

            GameObject terrainObject = Terrain.CreateTerrainGameObject(data);
            terrainObject.name = "Generated Terrain";
            terrainObject.transform.SetParent(generatedRoot, false);
            generatedTerrain = terrainObject.GetComponent<Terrain>();
            generatedTerrain.enabled = true;
            generatedTerrain.drawHeightmap = true;
            generatedTerrain.drawTreesAndFoliage = GenerateForest;
            generatedTerrain.drawInstanced = true;

            TerrainCollider terrainCollider = terrainObject.GetComponent<TerrainCollider>();
            if (terrainCollider != null)
            {
                terrainCollider.terrainData = data;
            }
        }

        private float[,] BuildHeightMap(int resolution)
        {
            float[,] heights = new float[resolution, resolution];
            Vector2 start = new Vector2(StartPosition.x, StartPosition.z);
            Vector2 save = new Vector2(SavePosition.x, SavePosition.z);
            Vector2 zoneHalfSize = new Vector2(25f, 21f);

            for (int zIndex = 0; zIndex < resolution; zIndex++)
            {
                float z = zIndex / (float)(resolution - 1) * MapLength;
                for (int xIndex = 0; xIndex < resolution; xIndex++)
                {
                    float x = xIndex / (float)(resolution - 1) * MapSize;
                    Vector2 point = new Vector2(x, z);
                    GetRoadInfo(point, out float roadDistance, out float routeT);
                    float ruggedness = GetRouteRuggedness(routeT);
                    float macroNoise = Mathf.PerlinNoise((x + Seed * 2.3f) * 0.0038f, (z - Seed * 1.1f) * 0.0038f);
                    float rollingNoise = Mathf.PerlinNoise((x - Seed * 3.1f) * 0.011f, (z + Seed * 2.7f) * 0.011f);
                    float detailNoise = Mathf.PerlinNoise((x + Seed) * 0.027f, (z - Seed) * 0.027f);

                    float rollingAmplitude = Mathf.Lerp(3.2f, 10.5f, ruggedness);
                    float rollingCentered = (rollingNoise - 0.5f) * 2f;
                    float detailCentered = (detailNoise - 0.5f) * 2f;
                    float broadCentered = (macroNoise - 0.5f) * 2f;
                    float naturalHeight = RoadElevation + rollingCentered * rollingAmplitude +
                                          detailCentered * 1.4f +
                                          broadCentered * Mathf.Lerp(2.5f, 8f, ruggedness);
                    float mountainNoise = Mathf.SmoothStep(0f, 1f,
                        Mathf.InverseLerp(0.64f, 0.90f, macroNoise));
                    naturalHeight += mountainNoise * Mathf.Lerp(5f, 19f, ruggedness);
                    naturalHeight += GetMountainMassifHeight(point);

                    float ridgeMask = GetMountainRidgeMask(point);
                    naturalHeight += ridgeMask * Mathf.Lerp(19f, 30f, ruggedness);

                    float shortcutBarrierA = GetShortcutBarrierMask(point, 0.395f, 0.70f, true, 17f, 34f, 0.8f);
                    float shortcutBarrierB = GetShortcutBarrierMask(point, 0.655f, 0.29f, false, 19f, 39f, 2.6f);
                    float shortcutBarrierC = GetShortcutBarrierMask(point, 0.865f, 0.73f, true, 16f, 46f, 4.2f);
                    float shortcutBarrierMask = Mathf.Max(shortcutBarrierA,
                        Mathf.Max(shortcutBarrierB, shortcutBarrierC));
                    naturalHeight += shortcutBarrierA * 31f +
                                     shortcutBarrierB * 36f +
                                     shortcutBarrierC * 41f;
                    naturalHeight = Mathf.Clamp(naturalHeight, RiverWaterHeight + 1.5f, TerrainHeight - 3f);

                    float steepness = Mathf.Max(ruggedness, Mathf.Max(ridgeMask, shortcutBarrierMask));
                    float roadInner = Mathf.Max(RoadWidth * 0.5f + 2.5f, RoadClearRadius * 0.55f);
                    float transitionEnd = roadInner + Mathf.Lerp(34f, 14f, steepness);
                    float roadBlend = Mathf.SmoothStep(0f, 1f,
                        Mathf.InverseLerp(roadInner, transitionEnd, roadDistance));
                    float height = Mathf.Lerp(RoadElevation, naturalHeight, roadBlend);

                    float riverDistance = Mathf.Abs(x - RiverCenterX(z));
                    float riverOuter = RiverHalfWidth + RiverBankWidth;
                    if (riverDistance < riverOuter)
                    {
                        float bankT = Mathf.SmoothStep(0f, 1f, riverDistance / riverOuter);
                        float riverHeight = Mathf.Lerp(RiverBedHeight, RoadElevation, bankT);
                        float riverCarve = Mathf.Min(height, riverHeight);
                        float roadCauseway = 1f - Mathf.SmoothStep(0f, 1f,
                            Mathf.InverseLerp(RoadWidth * 0.52f, RoadWidth * 0.82f, roadDistance));
                        height = Mathf.Lerp(riverCarve, RoadElevation, roadCauseway);
                    }

                    bool startZone = DistanceToRectangle(point, start, zoneHalfSize) < 7f;
                    bool saveZone = DistanceToRectangle(point, save, zoneHalfSize) < 7f;
                    if (startZone || saveZone)
                    {
                        height = RoadElevation;
                    }

                    heights[zIndex, xIndex] = Mathf.Clamp01(height / TerrainHeight);
                }
            }

            return heights;
        }

        private void PaintTerrain(TerrainData data)
        {
            if (data.terrainLayers == null || data.terrainLayers.Length < 3)
            {
                return;
            }

            const int resolution = 512;
            if (data.alphamapResolution != resolution)
            {
                data.alphamapResolution = resolution;
            }
            float[,,] map = new float[resolution, resolution, 3];
            Vector2 start = new Vector2(StartPosition.x, StartPosition.z);
            Vector2 save = new Vector2(SavePosition.x, SavePosition.z);
            Vector2 zoneHalfSize = new Vector2(25f, 21f);
            for (int z = 0; z < resolution; z++)
            {
                float normalizedZ = z / (float)(resolution - 1);
                for (int x = 0; x < resolution; x++)
                {
                    float normalizedX = x / (float)(resolution - 1);
                    Vector2 point = new Vector2(normalizedX * MapSize, normalizedZ * MapLength);
                    float slope = data.GetSteepness(normalizedX, normalizedZ);
                    float height = data.GetInterpolatedHeight(normalizedX, normalizedZ);
                    float slopeRock = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(24f, 48f, slope));
                    float highRock = Mathf.SmoothStep(0f, 0.62f,
                        Mathf.InverseLerp(RoadElevation + 17f, TerrainHeight - 4f, height));
                    float rock = Mathf.Clamp01(Mathf.Max(slopeRock, highRock));
                    float roadDistance = DistanceToRoad(point);
                    float road = 1f - Mathf.SmoothStep(0f, 1f,
                        Mathf.InverseLerp(RoadWidth * 0.48f, RoadWidth * 0.68f, roadDistance));
                    float startZone = DistanceToRectangle(point, start, zoneHalfSize) <= 0.01f ? 1f : 0f;
                    float saveZone = DistanceToRectangle(point, save, zoneHalfSize) <= 0.01f ? 1f : 0f;
                    road = Mathf.Max(road, Mathf.Max(startZone, saveZone));
                    float ground = 1f - road;
                    map[z, x, 0] = (1f - rock) * ground;
                    map[z, x, 1] = rock * ground;
                    map[z, x, 2] = road;
                }
            }
            data.SetAlphamaps(0, 0, map);
        }

        private void BuildRoad()
        {
            GameObject road = new GameObject("Generated Road");
            road.transform.SetParent(generatedRoot, false);
            MeshFilter filter = road.AddComponent<MeshFilter>();
            MeshRenderer renderer = road.AddComponent<MeshRenderer>();
            MeshCollider collider = road.AddComponent<MeshCollider>();

            int count = roadSamples.Count;
            Vector3[] vertices = new Vector3[count * 2];
            Vector2[] uv = new Vector2[vertices.Length];
            int[] triangles = new int[(count - 1) * 6];
            float traveled = 0f;

            for (int i = 0; i < count; i++)
            {
                Vector3 previous = roadSamples[Mathf.Max(0, i - 1)];
                Vector3 next = roadSamples[Mathf.Min(count - 1, i + 1)];
                Vector3 tangent = (next - previous).normalized;
                Vector3 right = Vector3.Cross(Vector3.up, tangent).normalized;
                if (i > 0)
                {
                    traveled += Vector3.Distance(roadSamples[i - 1], roadSamples[i]);
                }

                vertices[i * 2] = roadSamples[i] - right * RoadWidth * 0.5f;
                vertices[i * 2 + 1] = roadSamples[i] + right * RoadWidth * 0.5f;
                uv[i * 2] = new Vector2(0f, traveled / 8f);
                uv[i * 2 + 1] = new Vector2(1f, traveled / 8f);

                if (i >= count - 1)
                {
                    continue;
                }

                int triangle = i * 6;
                int vertex = i * 2;
                triangles[triangle] = vertex;
                triangles[triangle + 1] = vertex + 2;
                triangles[triangle + 2] = vertex + 1;
                triangles[triangle + 3] = vertex + 1;
                triangles[triangle + 4] = vertex + 2;
                triangles[triangle + 5] = vertex + 3;
            }

            Mesh mesh = new Mesh { name = "Game_v01 Procedural Road" };
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            filter.sharedMesh = mesh;
            collider.sharedMesh = mesh;
            renderer.sharedMaterial = RoadMaterial != null ? RoadMaterial : CreateFallbackMaterial("Road", new Color(0.18f, 0.19f, 0.20f));
        }

        private void BuildRiverSurface()
        {
            Transform riverRoot = new GameObject("Generated River").transform;
            riverRoot.SetParent(generatedRoot, false);
            const float segmentLength = 42f;
            int segmentCount = Mathf.CeilToInt(MapLength / segmentLength);

            for (int i = 0; i < segmentCount; i++)
            {
                float z0 = i * segmentLength;
                float z1 = Mathf.Min(MapLength, (i + 1) * segmentLength);
                Vector3 from = new Vector3(RiverCenterX(z0), RiverWaterHeight, z0);
                Vector3 to = new Vector3(RiverCenterX(z1), RiverWaterHeight, z1);
                Vector3 direction = to - from;
                GameObject water = GameObject.CreatePrimitive(PrimitiveType.Cube);
                water.name = "Water Segment " + i;
                water.transform.SetParent(riverRoot, false);
                water.transform.position = (from + to) * 0.5f;
                water.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
                water.transform.localScale = new Vector3(RiverHalfWidth * 2f, 0.16f, direction.magnitude + 0.8f);
                water.GetComponent<Renderer>().sharedMaterial = WaterMaterial != null
                    ? WaterMaterial
                    : CreateFallbackMaterial("Water", new Color(0.12f, 0.48f, 0.72f));
                DestroyGeneratedObject(water.GetComponent<Collider>());
            }
        }

        private void BuildZoneMarkers()
        {
            CreateZoneMarker("Start Zone", StartPosition, new Vector3(42f, 0.35f, 32f),
                StartMaterial != null ? StartMaterial : CreateFallbackMaterial("Start", new Color(0.88f, 0.68f, 0.18f)));
            CreateZoneMarker("Save Zone", SavePosition, new Vector3(42f, 0.35f, 32f),
                SaveMaterial != null ? SaveMaterial : CreateFallbackMaterial("Save", new Color(0.18f, 0.72f, 0.30f)));
        }

        private void CreateZoneMarker(string name, Vector3 position, Vector3 size, Material material)
        {
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = name;
            marker.transform.SetParent(generatedRoot, false);
            marker.transform.localPosition = new Vector3(position.x, RoadElevation + size.y * 0.5f + 0.05f, position.z);
            marker.transform.localScale = size;
            marker.GetComponent<Renderer>().sharedMaterial = material;
        }

        private void BuildForest()
        {
            Transform forestRoot = new GameObject("Generated Forest Blockers").transform;
            forestRoot.SetParent(generatedRoot, false);
            List<Vector2> candidates = new List<Vector2>();
            float jitter = TreeGridStep * 0.32f;

            for (float z = TreeGridStep; z < MapLength - TreeGridStep; z += TreeGridStep)
            {
                for (float x = TreeGridStep; x < MapSize - TreeGridStep; x += TreeGridStep)
                {
                    Vector2 point = new Vector2(x + Random.Range(-jitter, jitter), z + Random.Range(-jitter, jitter));
                    float roadDistance = DistanceToRoad(point);
                    if (roadDistance < RoadClearRadius + 2f)
                    {
                        continue;
                    }

                    if (DistanceToRectangle(point, new Vector2(StartPosition.x, StartPosition.z), new Vector2(32f, 27f)) < 2f ||
                        DistanceToRectangle(point, new Vector2(SavePosition.x, SavePosition.z), new Vector2(32f, 27f)) < 2f)
                    {
                        continue;
                    }

                    if (Mathf.Abs(point.x - RiverCenterX(point.y)) < RiverHalfWidth + RiverBankWidth + 4f)
                    {
                        continue;
                    }

                    GetRoadInfo(point, out _, out float routeT);
                    float chance = roadDistance <= DenseTreeBand ? DenseTreeChance : OuterTreeChance;
                    float clusterNoise = Mathf.PerlinNoise(
                        (point.x + Seed * 4.1f) * 0.014f,
                        (point.y - Seed * 2.6f) * 0.014f);
                    float cluster = Mathf.Lerp(0.45f, 1.15f,
                        Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.28f, 0.74f, clusterNoise)));
                    float meadowOpening = Mathf.Lerp(0.58f, 1f, GetRouteRuggedness(routeT));
                    chance *= cluster * meadowOpening;
                    if (Random.value > chance)
                    {
                        continue;
                    }

                    candidates.Add(point);
                }
            }

            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int swapIndex = Random.Range(0, i + 1);
                Vector2 temporary = candidates[i];
                candidates[i] = candidates[swapIndex];
                candidates[swapIndex] = temporary;
            }

            int treeCount = Mathf.Min(MaximumTrees, candidates.Count);
            for (int i = 0; i < treeCount; i++)
            {
                CreateTree(forestRoot, candidates[i], i);
            }
        }

        private void CreateTree(Transform parent, Vector2 point, int index)
        {
            float terrainY = generatedTerrain != null
                ? generatedTerrain.SampleHeight(new Vector3(point.x, 0f, point.y)) + generatedTerrain.transform.position.y
                : RoadElevation;
            float scale = Random.Range(0.82f, 1.28f);
            GameObject root = new GameObject("Tree Blocker " + index);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(point.x, terrainY, point.y);
            root.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            root.transform.localScale = Vector3.one * scale;

            BoxCollider blocker = root.AddComponent<BoxCollider>();
            blocker.center = new Vector3(0f, 2.8f, 0f);
            blocker.size = new Vector3(2.8f, 5.6f, 2.8f);

            GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.name = "Trunk";
            trunk.transform.SetParent(root.transform, false);
            trunk.transform.localPosition = new Vector3(0f, 1.7f, 0f);
            trunk.transform.localScale = new Vector3(0.48f, 1.7f, 0.48f);
            trunk.GetComponent<Renderer>().sharedMaterial = TreeTrunkMaterial != null
                ? TreeTrunkMaterial
                : CreateFallbackMaterial("Trunk", new Color(0.22f, 0.11f, 0.045f));
            DestroyGeneratedObject(trunk.GetComponent<Collider>());

            GameObject leaves = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            leaves.name = "Low Poly Crown";
            leaves.transform.SetParent(root.transform, false);
            leaves.transform.localPosition = new Vector3(0f, 4.6f, 0f);
            leaves.transform.localScale = new Vector3(3.1f, 4.1f, 3.1f);
            leaves.GetComponent<Renderer>().sharedMaterial = TreeLeavesMaterial != null
                ? TreeLeavesMaterial
                : CreateFallbackMaterial("Leaves", new Color(0.13f, 0.35f, 0.07f));
            DestroyGeneratedObject(leaves.GetComponent<Collider>());
        }

        private float DistanceToRoad(Vector2 point)
        {
            GetRoadInfo(point, out float distance, out _);
            return distance;
        }

        private void GetRoadInfo(Vector2 point, out float distance, out float routeT)
        {
            float best = float.MaxValue;
            float bestT = 0f;
            for (int i = 0; i < roadSamples.Count - 1; i++)
            {
                Vector2 a = new Vector2(roadSamples[i].x, roadSamples[i].z);
                Vector2 b = new Vector2(roadSamples[i + 1].x, roadSamples[i + 1].z);
                float segmentT;
                float candidate = DistanceToSegment(point, a, b, out segmentT);
                if (candidate < best)
                {
                    best = candidate;
                    bestT = (i + segmentT) / Mathf.Max(1f, roadSamples.Count - 1f);
                }
            }
            distance = best;
            routeT = bestT;
        }

        private float GetMountainRidgeMask(Vector2 point)
        {
            float normalizedX = point.x / MapSize;
            float ridgeA = CurvedRidge(point, 0.205f, 0.014f, 18f, 0.7f) *
                           Gaussian(normalizedX, 0.18f, 0.13f);
            float ridgeB = CurvedRidge(point, 0.435f, 0.011f, 24f, 2.1f) *
                           Gaussian(normalizedX, 0.80f, 0.14f);
            float ridgeC = CurvedRidge(point, 0.685f, 0.013f, 21f, 4.4f) *
                           Gaussian(normalizedX, 0.42f, 0.12f);
            return Mathf.Max(ridgeA, Mathf.Max(ridgeB, ridgeC));
        }

        private float GetMountainMassifHeight(Vector2 point)
        {
            float strongest = 0f;
            for (int i = 0; i < mountainMassifs.Count; i++)
            {
                Vector4 massif = mountainMassifs[i];
                float normalizedDistance = Vector2.Distance(point, new Vector2(massif.x, massif.y)) / massif.z;
                if (normalizedDistance >= 1f)
                {
                    continue;
                }
                float profile = 1f - Mathf.SmoothStep(0f, 1f,
                    Mathf.InverseLerp(0.08f, 1f, normalizedDistance));
                float irregularity = Mathf.Lerp(0.78f, 1.12f,
                    Mathf.PerlinNoise((point.x + Seed * 5.3f) * 0.021f, (point.y - Seed * 3.7f) * 0.021f));
                strongest = Mathf.Max(strongest, profile * massif.w * irregularity);
            }
            return strongest;
        }

        private float GetShortcutBarrierMask(Vector2 point, float normalizedZ, float openingX,
            bool opensRight, float coreHalfWidth, float height, float phase)
        {
            float centerZ = MapLength * normalizedZ +
                            Mathf.Sin(point.x * 0.012f + Seed * 0.07f + phase) * 13f;
            float distanceZ = Mathf.Abs(point.y - centerZ);
            float crossSection = 1f - Mathf.SmoothStep(0f, 1f,
                Mathf.InverseLerp(coreHalfWidth, coreHalfWidth + 15f, distanceZ));

            float normalizedX = point.x / MapSize;
            float openingFade = opensRight
                ? 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(openingX - 0.07f, openingX, normalizedX))
                : Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(openingX, openingX + 0.07f, normalizedX));
            float surfaceVariation = Mathf.Lerp(0.82f, 1.08f,
                Mathf.PerlinNoise((point.x + Seed * 4.1f) * 0.018f, (point.y - Seed * 2.7f) * 0.018f));
            return Mathf.Clamp01(crossSection * openingFade * surfaceVariation * (height / 36f));
        }

        private float CurvedRidge(Vector2 point, float normalizedZ, float frequency,
            float curveAmount, float phase)
        {
            float center = MapLength * normalizedZ + Mathf.Sin(point.x * frequency + Seed * 0.09f + phase) * curveAmount;
            float distance = Mathf.Abs(point.y - center);
            return 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(18f, 58f, distance));
        }

        private static float GetRouteRuggedness(float routeT)
        {
            float ruggedness = 0.42f;
            ruggedness += Gaussian(routeT, 0.36f, 0.12f) * 0.58f;
            ruggedness += Gaussian(routeT, 0.79f, 0.10f) * 0.48f;
            ruggedness -= Gaussian(routeT, 0.08f, 0.11f) * 0.32f;
            ruggedness -= Gaussian(routeT, 0.61f, 0.10f) * 0.30f;
            return Mathf.Clamp(ruggedness, 0.12f, 1f);
        }

        private static float Gaussian(float value, float center, float width)
        {
            float delta = (value - center) / Mathf.Max(0.001f, width);
            return Mathf.Exp(-0.5f * delta * delta);
        }

        private float RiverCenterX(float z)
        {
            return MapSize * 0.5f + Mathf.Sin(z * 0.018f + Seed * 0.13f) * MapSize * 0.035f;
        }

        private static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            return DistanceToSegment(point, a, b, out _);
        }

        private static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b, out float segmentT)
        {
            Vector2 segment = b - a;
            float denominator = segment.sqrMagnitude;
            if (denominator <= 0.0001f)
            {
                segmentT = 0f;
                return Vector2.Distance(point, a);
            }
            segmentT = Mathf.Clamp01(Vector2.Dot(point - a, segment) / denominator);
            return Vector2.Distance(point, a + segment * segmentT);
        }

        private static float DistanceToRectangle(Vector2 point, Vector2 center, Vector2 halfSize)
        {
            float dx = Mathf.Max(Mathf.Abs(point.x - center.x) - halfSize.x, 0f);
            float dy = Mathf.Max(Mathf.Abs(point.y - center.y) - halfSize.y, 0f);
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        private static Vector3 CatmullRom(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return 0.5f * ((2f * b) + (-a + c) * t +
                           (2f * a - 5f * b + 4f * c - d) * t2 +
                           (-a + 3f * b - 3f * c + d) * t3);
        }

        private static Material CreateFallbackMaterial(string name, Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }
            Material material = new Material(shader) { name = name, color = color };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.05f);
            return material;
        }

        private static void DestroyGeneratedObject(Object target)
        {
            if (target == null)
            {
                return;
            }
            if (Application.isPlaying) Destroy(target);
            else DestroyImmediate(target);
        }

        private void OnDrawGizmosSelected()
        {
            if (roadSamples.Count < 2)
            {
                return;
            }
            Gizmos.color = Color.yellow;
            for (int i = 0; i < roadSamples.Count - 1; i++)
            {
                Gizmos.DrawLine(transform.TransformPoint(roadSamples[i]), transform.TransformPoint(roadSamples[i + 1]));
            }
        }
    }
}
