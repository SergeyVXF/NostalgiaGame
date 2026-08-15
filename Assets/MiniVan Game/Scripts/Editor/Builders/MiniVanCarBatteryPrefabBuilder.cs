using MiniVanGame;
using UnityEditor;
using UnityEngine;

public static class MiniVanCarBatteryPrefabBuilder
{
    private const string MiniVanPrefabPath =
        "Assets/MiniVan Game/Prefabs/Vehicles/MiniVan/MiniVan.prefab";
    private const string BatteryPrefabPath =
        "Assets/MiniVan Game/Prefabs/Vehicles/MiniVan/AKB_Car.prefab";

    [InitializeOnLoadMethod]
    private static void BuildMissingPrefabAfterImport()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<GameObject>(BatteryPrefabPath) == null)
            {
                BuildCarBatteryPrefab();
            }
        };
    }

    [MenuItem("MiniVan/Build Car Battery Prefab")]
    public static void BuildCarBatteryPrefab()
    {
        EnsureFolderPath("Assets/MiniVan Game/Prefabs/Vehicles/MiniVan");

        GameObject batteryRoot = new GameObject("AKB_Car");
        try
        {
            batteryRoot.transform.localScale = Vector3.one;
            MiniVanCarBattery battery = batteryRoot.AddComponent<MiniVanCarBattery>();
            battery.Charge01 = 1f;
            Rigidbody body = batteryRoot.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.mass = 18f;
                body.linearDamping = 0.35f;
                body.angularDamping = 0.7f;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            }

            BoxCollider collider = batteryRoot.GetComponent<BoxCollider>();
            if (collider != null)
            {
                collider.size = new Vector3(0.78f, 0.42f, 0.52f);
                collider.center = new Vector3(0f, 0.21f, 0f);
            }

            Material caseMaterial = BuildMaterial("AKB_Car_Case", new Color(0.92f, 0.72f, 0.08f));
            Material darkMaterial = BuildMaterial("AKB_Car_DarkPlastic", new Color(0.055f, 0.055f, 0.05f));
            Material metalMaterial = BuildMaterial("AKB_Car_Terminal", new Color(0.72f, 0.72f, 0.68f));
            Material redMaterial = BuildMaterial("AKB_Car_Positive", new Color(0.85f, 0.05f, 0.035f));

            CreateCube(batteryRoot.transform, "Case", new Vector3(0f, 0.21f, 0f),
                new Vector3(0.78f, 0.42f, 0.52f), caseMaterial);
            CreateCube(batteryRoot.transform, "Top Lid", new Vector3(0f, 0.45f, 0f),
                new Vector3(0.82f, 0.08f, 0.56f), darkMaterial);
            CreateCube(batteryRoot.transform, "Positive Terminal", new Vector3(-0.23f, 0.54f, 0.12f),
                new Vector3(0.13f, 0.08f, 0.13f), redMaterial);
            CreateCube(batteryRoot.transform, "Negative Terminal", new Vector3(0.23f, 0.54f, 0.12f),
                new Vector3(0.13f, 0.08f, 0.13f), metalMaterial);
            CreateCube(batteryRoot.transform, "Handle", new Vector3(0f, 0.62f, -0.02f),
                new Vector3(0.52f, 0.055f, 0.08f), darkMaterial);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(batteryRoot, BatteryPrefabPath);
            AssignToMiniVanPrefab(prefab);
        }
        finally
        {
            Object.DestroyImmediate(batteryRoot);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[MiniVanCarBattery] Built AKB_Car prefab and assigned it to MiniVan.prefab.");
    }

    private static void AssignToMiniVanPrefab(GameObject batteryPrefab)
    {
        if (batteryPrefab == null)
        {
            return;
        }

        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(MiniVanPrefabPath);
        try
        {
            MiniVanVehicle vehicle = prefabRoot.GetComponent<MiniVanVehicle>();
            if (vehicle == null)
            {
                throw new MissingComponentException("MiniVan.prefab has no MiniVanVehicle component.");
            }

            vehicle.CarBatteryPrefab = batteryPrefab;
            vehicle.CarBatteryCharge01.Value = 1f;
            EditorUtility.SetDirty(vehicle);
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, MiniVanPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static GameObject CreateCube(Transform parent, string name, Vector3 localPosition,
        Vector3 localScale, Material material)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.SetParent(parent, false);
        cube.transform.localPosition = localPosition;
        cube.transform.localScale = localScale;
        Renderer renderer = cube.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = material;
        }
        Object.DestroyImmediate(cube.GetComponent<Collider>());
        return cube;
    }

    private static Material BuildMaterial(string name, Color color)
    {
        const string folder = "Assets/MiniVan Game/Materials/Vehicles/MiniVan";
        EnsureFolderPath(folder);
        string path = folder + "/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }

        material.name = name;
        material.color = color;
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void EnsureFolderPath(string folderPath)
    {
        string[] parts = folderPath.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }
            current = next;
        }
    }
}
