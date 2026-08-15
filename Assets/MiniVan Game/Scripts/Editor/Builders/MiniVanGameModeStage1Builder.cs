using System.Collections.Generic;
using MiniVanGame;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;

public static class MiniVanGameModeStage1Builder
{
    private const string ScenePath = "Assets/MiniVan Game/Scenes/Game_v01.unity";
    private const string TerrainFolder =
        "Assets/MiniVan Game/Settings/World/GameMode/Terrain";
    private const string TextureFolder = "Assets/MiniVan Game/Textures/World/GameMode";
    private const string MaterialsFolder = "Assets/MiniVan Game/Materials/World/GameMode";
    private const string TerrainPath = TerrainFolder + "/Game_v01_TerrainData.asset";
    private const string LegacyRoadMeshPath =
        "Assets/MiniVan Game/Models/World/GameMode/Game_v01_Road.asset";
    private const string GrassTexturePath = TextureFolder + "/Game_v01_GrassTexture.asset";
    private const string GrassLayerPath = TerrainFolder + "/Game_v01_Grass.terrainlayer";
    private const string RockTexturePath = TextureFolder + "/Game_v01_RockTexture.asset";
    private const string RockLayerPath = TerrainFolder + "/Game_v01_Rock.terrainlayer";
    private const string RoadTexturePath = TextureFolder + "/Game_v01_RoadTexture.asset";
    private const string RoadLayerPath = TerrainFolder + "/Game_v01_Road.terrainlayer";
    private const string WindowTexturePath = TextureFolder + "/Game_v01_Window_Glare.asset";
    private const string MiniVanPrefabPath =
        "Assets/MiniVan Game/Prefabs/Vehicles/MiniVan/MiniVan.prefab";
    private const string ZombiePrefabPath =
        "Assets/MiniVan Game/Prefabs/Characters/Zombies/Zombie.prefab";
    private const string BatPrefabPath =
        "Assets/MiniVan Game/Prefabs/Weapons/Melee/BatPickup.prefab";
    private const string NetworkSourceScenePath = "Assets/MiniVan Game/Scenes/MiniVan_MVP.unity";
    private const string NetworkPrefabPath =
        "Assets/MiniVan Game/Prefabs/Network/GameMode/Game_v01_Network.prefab";
    private const string PanelkaMaterialFolder =
        "Assets/MiniVan Game/Materials/Panelka/Stage1";
    private const string PanelkaPropMaterialFolder =
        "Assets/MiniVan Game/Materials/Panelka/Generated";
    private const string GameModePrefabFolder = "Assets/MiniVan Game/Prefabs/World/GameMode";
    private const string StartCompoundPrefabPath = GameModePrefabFolder + "/START_COMPOUND_FUNCTIONAL.prefab";
    private const string SaveZonePrefabPath = GameModePrefabFolder + "/SAVE_ZONE_FUNCTIONAL.prefab";

    [MenuItem("MiniVan/Game Mode/Build Stage 1 - Game_v01")]
    public static void BuildStage1()
    {
        EnsureFolder("Assets/MiniVan Game/Settings/World/GameMode", "Terrain");
        EnsureFolder("Assets/MiniVan Game/Textures/World", "GameMode");
        EnsureFolder("Assets/MiniVan Game/Materials/World", "GameMode");
        EnsureFolder("Assets/MiniVan Game/Prefabs/World", "GameMode");
        EnsureFolder("Assets/MiniVan Game/Models", "World");
        EnsureFolder("Assets/MiniVan Game/Models/World", "GameMode");

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        CreateLighting();
        CreateOverviewCamera();

        GameObject generatorObject = new GameObject("Game Mode Map Generator");
        MiniVanGameModeMapGenerator generator = generatorObject.AddComponent<MiniVanGameModeMapGenerator>();
        generator.Seed = 101;
        generator.GenerateOnStart = false;
        generator.GenerateForest = false;
        generator.TerrainDataAsset = GetOrCreateTerrainData();
        generator.GrassTerrainLayer = GetOrCreateGrassTerrainLayer();
        generator.RockTerrainLayer = GetOrCreateRockTerrainLayer();
        generator.RoadTerrainLayer = GetOrCreateRoadTerrainLayer();
        generator.GrassMaterial = GetOrCreateMaterial("GM_Grass", new Color(0.23f, 0.43f, 0.12f));
        generator.WaterMaterial = GetOrCreateMaterial("GM_Water", new Color(0.08f, 0.42f, 0.68f), true);
        generator.StartMaterial = GetOrCreateMaterial("GM_StartZone", new Color(0.88f, 0.60f, 0.10f));
        generator.SaveMaterial = GetOrCreateMaterial("GM_SaveZone", new Color(0.12f, 0.72f, 0.28f));
        generator.TreeTrunkMaterial = GetOrCreateMaterial("GM_TreeTrunk", new Color(0.22f, 0.11f, 0.045f));
        generator.TreeLeavesMaterial = GetOrCreateMaterial("GM_TreeLeaves", new Color(0.10f, 0.31f, 0.055f));
        generator.Rebuild();

        GameObject zombiePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ZombiePrefabPath);
        GameObject batPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BatPrefabPath);
        CreateGameModeContent(generator, zombiePrefab, batPrefab);
        CreateNetworkSetup(scene, zombiePrefab, batPrefab);
        CreateStartingMiniVan(scene, generator);
        RemoveMissingGeneratedBehaviours(scene);

        AssetDatabase.DeleteAsset(LegacyRoadMeshPath);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AddSceneToBuildSettings();
        Selection.activeGameObject = generatorObject;
        SceneView.lastActiveSceneView?.FrameSelected();
        AssetDatabase.SaveAssets();
        Debug.Log("[GameMode Stage 1] Game_v01 generated and saved.");
    }

    private static void RemoveMissingGeneratedBehaviours(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i] != null)
                {
                    GameObjectUtility.RemoveMonoBehavioursWithMissingScript(transforms[i].gameObject);
                }
            }
        }
    }

    private static void CreateGameModeContent(MiniVanGameModeMapGenerator mapGenerator,
        GameObject zombiePrefab, GameObject batPrefab)
    {
        GameObject contentObject = new GameObject("Game Mode World Generator");
        MiniVanGameModeWorldGenerator content = contentObject.AddComponent<MiniVanGameModeWorldGenerator>();
        content.MapGenerator = mapGenerator;
        content.ZombiePrefab = zombiePrefab;
        content.BatPickupPrefab = batPrefab;
        content.GenerateOnStart = false;
        content.ExteriorMaterial = LoadPanelkaMaterial("PanelkaStage1_Exterior.mat", "GM_PanelkaExterior", new Color(0.44f, 0.49f, 0.53f));
        content.InteriorMaterial = LoadPanelkaMaterial("PanelkaStage1_Interior.mat", "GM_PanelkaInterior", new Color(0.68f, 0.66f, 0.59f));
        content.FloorMaterial = LoadPanelkaMaterial("PanelkaStage1_Floor.mat", "GM_PanelkaFloor", new Color(0.25f, 0.23f, 0.20f));
        content.DoorMaterial = LoadPanelkaMaterial("PanelkaStage1_Door.mat", "GM_PanelkaDoor", new Color(0.31f, 0.16f, 0.08f));
        content.ApartmentDoorMaterials = LoadApartmentDoorPalette();
        content.MetalMaterial = LoadPanelkaMaterial("PanelkaStage1_Metal.mat", "GM_PanelkaMetal", new Color(0.22f, 0.24f, 0.25f));
        content.OpaqueWindowMaterial = GetOrCreateWindowMaterial();
        content.WoodMaterial = LoadPanelkaPropMaterial("Panelka_Wood_Pixel.mat", "GM_Wood", new Color(0.32f, 0.16f, 0.07f));
        content.FabricMaterial = LoadPanelkaPropMaterial("Panelka_Fabric_Red_Pixel.mat", "GM_Fabric", new Color(0.42f, 0.08f, 0.07f));
        content.PaperMaterial = LoadPanelkaPropMaterial("Panelka_Paper_Pixel.mat", "GM_Paper", new Color(0.72f, 0.69f, 0.55f));
        content.DarkMaterial = LoadPanelkaPropMaterial("Panelka_DarkPlastic_Pixel.mat", "GM_Dark", new Color(0.08f, 0.08f, 0.07f));
        content.HouseMaterial = GetOrCreateMaterial("GM_SmallHouse", new Color(0.51f, 0.38f, 0.25f));
        content.RoofMaterial = GetOrCreateMaterial("GM_Roof", new Color(0.14f, 0.15f, 0.16f));
        content.CrateMaterial = GetOrCreateMaterial("GM_Crate", new Color(0.45f, 0.24f, 0.08f));
        content.CoinMaterial = LoadPanelkaPropMaterial("Panelka_Key_Yellow.mat", "GM_Coin", new Color(0.96f, 0.72f, 0.08f));
        content.ShopMaterial = GetOrCreateMaterial("GM_Shop", new Color(0.32f, 0.47f, 0.34f));
        content.Rebuild();
        CreateAndInstallCompoundPrefabs(content);

        EnableInstancingOnGeneratedMaterials(contentObject);
    }

    private static void CreateAndInstallCompoundPrefabs(MiniVanGameModeWorldGenerator content)
    {
        if (content == null)
        {
            return;
        }

        Transform generatedRoot = content.transform.Find("Generated_GameMode_Content");
        if (generatedRoot == null)
        {
            return;
        }

        content.StartCompoundPrefab = SaveAndReplaceCompound(generatedRoot,
            "START_COMPOUND_FUNCTIONAL", StartCompoundPrefabPath);
        content.SaveZonePrefab = SaveAndReplaceCompound(generatedRoot,
            "SAVE_ZONE_FUNCTIONAL", SaveZonePrefabPath);
        EditorUtility.SetDirty(content);
    }

    private static GameObject SaveAndReplaceCompound(Transform generatedRoot, string compoundName,
        string prefabPath)
    {
        Transform source = generatedRoot.Find(compoundName);
        if (source == null)
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        }

        Vector3 worldPosition = source.position;
        Quaternion worldRotation = source.rotation;
        Vector3 localScale = source.localScale;

        GameObject prefabSource = Object.Instantiate(source.gameObject);
        prefabSource.name = compoundName;
        prefabSource.transform.position = Vector3.zero;
        prefabSource.transform.rotation = Quaternion.identity;
        prefabSource.transform.localScale = Vector3.one;
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(prefabSource, prefabPath);
        Object.DestroyImmediate(prefabSource);
        if (prefab == null)
        {
            return null;
        }

        Object.DestroyImmediate(source.gameObject);
        GameObject instance = PrefabUtility.InstantiatePrefab(prefab, generatedRoot.gameObject.scene) as GameObject;
        if (instance != null)
        {
            instance.name = compoundName;
            instance.transform.SetParent(generatedRoot, true);
            instance.transform.position = worldPosition;
            instance.transform.rotation = worldRotation;
            instance.transform.localScale = localScale;
        }
        return prefab;
    }

    private static void EnableInstancingOnGeneratedMaterials(GameObject root)
    {
        if (root == null)
        {
            return;
        }

        HashSet<Material> materials = new HashSet<Material>();
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Material[] sharedMaterials = renderers[i].sharedMaterials;
            for (int materialIndex = 0; materialIndex < sharedMaterials.Length; materialIndex++)
            {
                Material material = sharedMaterials[materialIndex];
                if (material != null && materials.Add(material))
                {
                    material.enableInstancing = true;
                    EditorUtility.SetDirty(material);
                }
            }
        }
    }

    private static Material LoadPanelkaMaterial(string fileName, string fallbackName, Color fallbackColor)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(PanelkaMaterialFolder + "/" + fileName);
        return material != null ? material : GetOrCreateMaterial(fallbackName, fallbackColor);
    }

    private static Material LoadPanelkaPropMaterial(string fileName, string fallbackName, Color fallbackColor)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(PanelkaPropMaterialFolder + "/" + fileName);
        return material != null ? material : GetOrCreateMaterial(fallbackName, fallbackColor);
    }

    private static void CreateNetworkSetup(Scene targetScene, GameObject zombiePrefab,
        GameObject batPrefab)
    {
        GameObject sourceRoot = AssetDatabase.LoadAssetAtPath<GameObject>(NetworkPrefabPath);
        if (sourceRoot != null)
        {
            GameObject clone = PrefabUtility.InstantiatePrefab(sourceRoot, targetScene) as GameObject;
            if (clone == null) return;
            clone.name = "Game_v01 Network";
            MiniVanNetworkBootstrap bootstrap = clone.GetComponentInChildren<MiniVanNetworkBootstrap>(true);
            if (bootstrap != null && zombiePrefab != null)
            {
                List<GameObject> prefabs = new List<GameObject>();
                if (bootstrap.ExtraNetworkPrefabs != null) prefabs.AddRange(bootstrap.ExtraNetworkPrefabs);
                if (!prefabs.Contains(zombiePrefab)) prefabs.Add(zombiePrefab);
                if (batPrefab != null && !prefabs.Contains(batPrefab)) prefabs.Add(batPrefab);
                bootstrap.ExtraNetworkPrefabs = prefabs.ToArray();
            }
        }
    }

    private static void CreateStartingMiniVan(Scene scene, MiniVanGameModeMapGenerator generator)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MiniVanPrefabPath);
        if (prefab == null)
        {
            return;
        }

        GameObject miniVan = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
        if (miniVan == null)
        {
            return;
        }

        Vector3 tangent = generator.RoadSamples.Count > 2
            ? generator.RoadSamples[2] - generator.RoadSamples[0]
            : Vector3.forward;
        tangent.y = 0f;
        miniVan.name = "Starting MiniVan";
        miniVan.transform.position = generator.StartPosition + Vector3.up * 1.2f;
        miniVan.transform.rotation = Quaternion.LookRotation(tangent.normalized, Vector3.up);
        CreatePlayerSpawnPoint(miniVan.transform, generator.StartPosition.y);
    }

    private static void CreatePlayerSpawnPoint(Transform miniVan, float fallbackGroundHeight)
    {
        Physics.SyncTransforms();
        Vector3 desired = miniVan.position + miniVan.right * 5.2f - miniVan.forward * 1.8f;
        Vector3 rayOrigin = new Vector3(desired.x, miniVan.position.y + 16f, desired.z);
        float surfaceHeight = fallbackGroundHeight;
        RaycastHit[] hits = Physics.RaycastAll(rayOrigin, Vector3.down, 40f, ~0,
            QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].transform.IsChildOf(miniVan) || hits[i].normal.y < 0.55f)
            {
                continue;
            }

            surfaceHeight = hits[i].point.y;
            break;
        }

        GameObject spawn = new GameObject("Player Spawn Point");
        spawn.transform.position = new Vector3(desired.x, surfaceHeight + 1.18f, desired.z);
        spawn.transform.rotation = miniVan.rotation;
    }

    private static void CreateLighting()
    {
        GameObject lightObject = new GameObject("Directional Light");
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.15f;
        light.color = new Color(1f, 0.95f, 0.86f);
        light.shadows = LightShadows.Soft;
        lightObject.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
        RenderSettings.ambientLight = new Color(0.56f, 0.61f, 0.68f);
    }

    private static void CreateOverviewCamera()
    {
        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.enabled = false;
        camera.clearFlags = CameraClearFlags.Skybox;
        camera.fieldOfView = 52f;
        camera.farClipPlane = 1800f;
        cameraObject.transform.position = new Vector3(360f, 850f, 360f);
        cameraObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
    }

    private static TerrainData GetOrCreateTerrainData()
    {
        TerrainData data = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainPath);
        if (data != null)
        {
            return data;
        }
        data = new TerrainData { name = "Game_v01 Terrain Data" };
        AssetDatabase.CreateAsset(data, TerrainPath);
        return data;
    }

    private static TerrainLayer GetOrCreateGrassTerrainLayer()
    {
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(GrassTexturePath);
        if (texture == null)
        {
            texture = new Texture2D(4, 4, TextureFormat.RGB24, false)
            {
                name = "Game_v01 Grass Texture",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };
            Color[] pixels = new Color[16];
            for (int i = 0; i < pixels.Length; i++)
            {
                float variation = i % 3 == 0 ? 0.025f : i % 3 == 1 ? -0.018f : 0f;
                pixels[i] = new Color(0.22f + variation, 0.41f + variation, 0.11f + variation);
            }
            texture.SetPixels(pixels);
            texture.Apply();
            AssetDatabase.CreateAsset(texture, GrassTexturePath);
        }

        TerrainLayer layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(GrassLayerPath);
        if (layer == null)
        {
            layer = new TerrainLayer { name = "Game_v01 Grass" };
            AssetDatabase.CreateAsset(layer, GrassLayerPath);
        }
        layer.diffuseTexture = texture;
        layer.tileSize = new Vector2(18f, 18f);
        layer.smoothness = 0f;
        layer.metallic = 0f;
        EditorUtility.SetDirty(layer);
        return layer;
    }

    private static TerrainLayer GetOrCreateRockTerrainLayer()
    {
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(RockTexturePath);
        if (texture == null)
        {
            texture = new Texture2D(4, 4, TextureFormat.RGB24, false)
            {
                name = "Game_v01 Rock Texture",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };
            Color[] pixels = new Color[16];
            for (int i = 0; i < pixels.Length; i++)
            {
                float variation = i % 4 == 0 ? 0.05f : i % 4 == 1 ? -0.035f : 0f;
                pixels[i] = new Color(0.31f + variation, 0.32f + variation, 0.30f + variation);
            }
            texture.SetPixels(pixels);
            texture.Apply();
            AssetDatabase.CreateAsset(texture, RockTexturePath);
        }

        TerrainLayer layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(RockLayerPath);
        if (layer == null)
        {
            layer = new TerrainLayer { name = "Game_v01 Rock" };
            AssetDatabase.CreateAsset(layer, RockLayerPath);
        }
        layer.diffuseTexture = texture;
        layer.tileSize = new Vector2(14f, 14f);
        layer.smoothness = 0f;
        layer.metallic = 0f;
        EditorUtility.SetDirty(layer);
        return layer;
    }

    private static TerrainLayer GetOrCreateRoadTerrainLayer()
    {
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(RoadTexturePath);
        if (texture == null)
        {
            texture = new Texture2D(4, 4, TextureFormat.RGB24, false)
            {
                name = "Game_v01 Road Texture",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };
            Color[] pixels = new Color[16];
            for (int i = 0; i < pixels.Length; i++)
            {
                float variation = i % 4 == 0 ? 0.035f : i % 4 == 1 ? -0.025f : 0f;
                pixels[i] = new Color(0.90f + variation, 0.67f + variation, 0.12f);
            }
            texture.SetPixels(pixels);
            texture.Apply();
            AssetDatabase.CreateAsset(texture, RoadTexturePath);
        }

        TerrainLayer layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(RoadLayerPath);
        if (layer == null)
        {
            layer = new TerrainLayer { name = "Game_v01 Road" };
            AssetDatabase.CreateAsset(layer, RoadLayerPath);
        }
        layer.diffuseTexture = texture;
        layer.tileSize = new Vector2(7f, 7f);
        layer.smoothness = 0f;
        layer.metallic = 0f;
        EditorUtility.SetDirty(layer);
        return layer;
    }

    private static Material[] LoadApartmentDoorPalette()
    {
        const string folder = "Assets/MiniVan Game/Materials/Panelka/Interior/LowPolyPack/";
        string[] names =
        {
            "Door_DarkBrownPanels_01.mat",
            "Door_FadedTeal_02.mat",
            "Door_MustardVinyl_03.mat",
            "Door_GrayMetal_04.mat",
            "Door_RedBrownVeneer_05.mat"
        };
        Material[] palette = new Material[names.Length];
        int count = 0;
        for (int i = 0; i < names.Length; i++)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(folder + names[i]);
            if (material != null)
            {
                palette[count++] = material;
            }
        }

        System.Array.Resize(ref palette, count);
        return palette;
    }

    private static Material GetOrCreateMaterial(string name, Color color, bool transparent = false)
    {
        string path = MaterialsFolder + "/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            material = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }

        material.color = color;
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", transparent ? 0.35f : 0.04f);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material GetOrCreateWindowMaterial()
    {
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(WindowTexturePath);
        if (texture == null)
        {
            texture = new Texture2D(32, 32, TextureFormat.RGBA32, false)
            {
                name = "Game_v01 Light Blue Pixel Window",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat
            };
            AssetDatabase.CreateAsset(texture, WindowTexturePath);
        }

        Color baseBlue = new Color(0.38f, 0.72f, 0.88f, 1f);
        Color paleBlue = new Color(0.58f, 0.86f, 0.96f, 1f);
        Color shadowBlue = new Color(0.23f, 0.57f, 0.76f, 1f);
        Color glare = new Color(0.88f, 0.98f, 1f, 1f);
        Color[] pixels = new Color[32 * 32];
        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                int block = (x / 8 + y / 8) % 3;
                Color color = block == 0 ? baseBlue : block == 1 ? paleBlue : shadowBlue;
                int diagonal = x + y;
                if ((diagonal >= 20 && diagonal <= 23 && x >= 5 && y >= 5) ||
                    (diagonal >= 39 && diagonal <= 42 && x <= 26 && y <= 26))
                {
                    color = glare;
                }
                pixels[y * 32 + x] = color;
            }
        }
        texture.SetPixels(pixels);
        texture.Apply(false, false);
        EditorUtility.SetDirty(texture);

        Material material = GetOrCreateMaterial("GM_OpaqueWindow", Color.white);
        material.mainTexture = texture;
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.28f);
        material.color = Color.white;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void AddSceneToBuildSettings()
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        for (int i = 0; i < scenes.Count; i++)
        {
            if (scenes[i].path == ScenePath)
            {
                scenes[i].enabled = true;
                EditorBuildSettings.scenes = scenes.ToArray();
                return;
            }
        }
        scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    private static void EnsureFolder(string parent, string name)
    {
        string path = parent + "/" + name;
        if (!AssetDatabase.IsValidFolder(path))
        {
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
