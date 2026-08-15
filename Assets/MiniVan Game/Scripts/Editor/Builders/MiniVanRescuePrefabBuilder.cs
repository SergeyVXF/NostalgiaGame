using MiniVanGame;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
internal static class MiniVanRescuePrefabBuilder
{
    private const string PrefabFolder = "Assets/MiniVan Game/Prefabs/World/Rescue";
    private const string SavePlacePrefabPath = PrefabFolder + "/SavePlace.prefab";
    private const string BunkerPrefabPath = PrefabFolder + "/Bunker.prefab";

    static MiniVanRescuePrefabBuilder()
    {
        EditorApplication.delayCall += CreateMissingPrefabs;
    }

    [MenuItem("MiniVan Game/Rescue/Create Rescue Prefabs")]
    private static void CreateMissingPrefabs()
    {
        if (!AssetDatabase.IsValidFolder(PrefabFolder))
        {
            EnsureFolderPath(PrefabFolder);
        }

        if (AssetDatabase.LoadAssetAtPath<GameObject>(SavePlacePrefabPath) == null)
        {
            CreateSavePlacePrefab();
        }

        if (AssetDatabase.LoadAssetAtPath<GameObject>(BunkerPrefabPath) == null)
        {
            CreateBunkerPrefab();
        }
    }

    private static void CreateSavePlacePrefab()
    {
        GameObject root = new GameObject("SavePlace");
        MiniVanRescueSavePlace savePlace = root.AddComponent<MiniVanRescueSavePlace>();
        savePlace.CallRadius = 24f;
        savePlace.ZombieBlockRadius = 12f;

        GameObject building = AddCube(root.transform, "SavePlace_Body", new Vector3(0f, 1.5f, 0f), new Vector3(4f, 3f, 4f), new Color(0.25f, 0.75f, 0.38f));
        building.isStatic = true;

        GameObject door = AddCube(root.transform, "SavePlace_Door", new Vector3(0f, 0.9f, -2.05f), new Vector3(1f, 1.8f, 0.12f), new Color(0.08f, 0.28f, 0.13f));
        door.AddComponent<MiniVanRescueDoor>().OpenAngle = -88f;
        savePlace.Door = door.transform;

        SavePrefab(root, SavePlacePrefabPath);
    }

    private static void CreateBunkerPrefab()
    {
        GameObject root = new GameObject("Bunker");
        MiniVanRescueBunker bunker = root.AddComponent<MiniVanRescueBunker>();
        bunker.DeliveryRadius = 30f;
        bunker.ZombieBlockRadius = 12f;

        GameObject body = AddCube(root.transform, "Bunker_Body", new Vector3(0f, 1.35f, 0f), new Vector3(5f, 2.7f, 5f), new Color(0.35f, 0.38f, 0.42f));
        body.isStatic = true;

        GameObject door = AddCube(root.transform, "Bunker_Door", new Vector3(0f, 0.9f, -2.55f), new Vector3(1.2f, 1.8f, 0.12f), new Color(0.08f, 0.1f, 0.12f));
        door.AddComponent<MiniVanRescueDoor>().OpenAngle = -88f;
        bunker.Door = door.transform;

        SavePrefab(root, BunkerPrefabPath);
    }

    private static GameObject AddCube(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Color color)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.SetParent(parent, false);
        cube.transform.localPosition = localPosition;
        cube.transform.localScale = localScale;

        Renderer renderer = cube.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material material = new Material(FindUnlitShader());
            material.color = color;
            renderer.sharedMaterial = material;
        }

        return cube;
    }

    private static Shader FindUnlitShader()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }

        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        return shader;
    }

    private static void SavePrefab(GameObject root, string path)
    {
        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
    }

    private static void EnsureFolderPath(string path)
    {
        string[] parts = path.Split('/');
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
