using MiniVanGame;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
internal static class MiniVanHotPotatoPrefabBuilder
{
    private const string PrefabFolder = "Assets/MiniVan Game/Prefabs";
    private const string BombPrefabPath =
        PrefabFolder + "/Weapons/Explosives/HotPotatoBomb.prefab";
    private const string DummyPrefabPath =
        PrefabFolder + "/Characters/Test/HotPotatoDummy.prefab";
    private const string PoopPrefabPath =
        PrefabFolder + "/Items/HotPotato/HotPotatoPoopLowPoly.prefab";
    private const string PlayerPrefabPath =
        PrefabFolder + "/Characters/Players/MiniVanPlayer.prefab";

    static MiniVanHotPotatoPrefabBuilder()
    {
        EditorApplication.delayCall += EnsureHotPotatoPrefabsAndSceneObjects;
    }

    [MenuItem("MiniVan Game/Hot Potato/Create Prefabs And Test Objects")]
    private static void EnsureHotPotatoPrefabsAndSceneObjects()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        if (!AssetDatabase.IsValidFolder(PrefabFolder))
        {
            AssetDatabase.CreateFolder("Assets/MiniVan Game", "Prefabs");
        }

        GameObject bombPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BombPrefabPath);
        if (bombPrefab == null)
        {
            bombPrefab = CreateBombPrefab();
        }

        GameObject dummyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DummyPrefabPath);
        if (dummyPrefab == null)
        {
            dummyPrefab = CreateDummyPrefab();
        }

        GameObject poopPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PoopPrefabPath);
        if (poopPrefab == null)
        {
            poopPrefab = CreatePoopPrefab();
        }

        AssignPoopPrefabToPlayerPrefab(poopPrefab);
        PlaceTestObjectsIfMissing(bombPrefab, dummyPrefab);
    }

    private static GameObject CreateBombPrefab()
    {
        GameObject root = new GameObject("HotPotatoBomb");
        root.AddComponent<NetworkObject>();
        root.AddComponent<NetworkTransform>();
        MiniVanHotPotatoBomb bomb = root.AddComponent<MiniVanHotPotatoBomb>();
        bomb.CatchRadius = 1.6f;
        bomb.ThrowsBeforeExplosion = 3;
        bomb.MinThrowsBeforeExplosion = 3;
        bomb.MaxThrowsBeforeExplosion = 20;
        bomb.PoopSeconds = 20f;

        Rigidbody body = root.GetComponent<Rigidbody>();
        body.mass = 0.7f;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        SphereCollider collider = root.GetComponent<SphereCollider>();
        collider.radius = 0.24f;

        GameObject visual = new GameObject("Bomb Visual");
        visual.transform.SetParent(root.transform, false);
        Material bombMaterial = CreateMaterial(new Color(0.035f, 0.035f, 0.04f, 1f));
        Material fuseMaterial = CreateMaterial(new Color(0.35f, 0.18f, 0.06f, 1f));
        AddPrimitive(visual.transform, PrimitiveType.Sphere, "Bomb Body", Vector3.zero, Quaternion.identity, Vector3.one * 0.48f, bombMaterial);
        AddPrimitive(visual.transform, PrimitiveType.Cylinder, "Bomb Fuse Cap", new Vector3(0f, 0.27f, 0f), Quaternion.identity, new Vector3(0.09f, 0.045f, 0.09f), fuseMaterial);
        AddPrimitive(visual.transform, PrimitiveType.Cylinder, "Bomb Fuse", new Vector3(0f, 0.43f, 0f), Quaternion.Euler(28f, 0f, 18f), new Vector3(0.025f, 0.16f, 0.025f), fuseMaterial);

        return SavePrefab(root, BombPrefabPath);
    }

    private static GameObject CreateDummyPrefab()
    {
        GameObject root = new GameObject("HotPotatoDummy");
        root.AddComponent<NetworkObject>();
        root.AddComponent<NetworkTransform>();
        root.AddComponent<CapsuleCollider>();
        MiniVanHotPotatoDummy dummy = root.AddComponent<MiniVanHotPotatoDummy>();
        dummy.ReturnDelay = 0.85f;

        Material bodyMaterial = CreateMaterial(new Color(0.26f, 0.62f, 0.95f, 1f));
        Material faceMaterial = CreateMaterial(new Color(0.05f, 0.08f, 0.12f, 1f));
        GameObject visual = new GameObject("Hot Potato Dummy Visual");
        visual.transform.SetParent(root.transform, false);
        AddPrimitive(visual.transform, PrimitiveType.Capsule, "Dummy Body", new Vector3(0f, 0.9f, 0f), Quaternion.identity, new Vector3(0.62f, 0.9f, 0.62f), bodyMaterial);
        AddPrimitive(visual.transform, PrimitiveType.Cube, "Dummy Head", new Vector3(0f, 1.92f, 0f), Quaternion.identity, new Vector3(0.56f, 0.42f, 0.5f), bodyMaterial);
        AddPrimitive(visual.transform, PrimitiveType.Cube, "Dummy Face", new Vector3(0f, 1.92f, 0.255f), Quaternion.identity, new Vector3(0.36f, 0.08f, 0.02f), faceMaterial);

        return SavePrefab(root, DummyPrefabPath);
    }

    private static GameObject CreatePoopPrefab()
    {
        GameObject root = new GameObject("HotPotatoPoop");
        Material material = CreateMaterial(new Color(0.36f, 0.19f, 0.08f, 1f));
        AddPrimitive(root.transform, PrimitiveType.Sphere, "Poop Base", new Vector3(0f, 0.28f, 0f), Quaternion.identity, new Vector3(0.82f, 0.28f, 0.82f), material);
        AddPrimitive(root.transform, PrimitiveType.Sphere, "Poop Middle", new Vector3(0f, 0.55f, 0f), Quaternion.identity, new Vector3(0.58f, 0.24f, 0.58f), material);
        AddPrimitive(root.transform, PrimitiveType.Sphere, "Poop Tip", new Vector3(0f, 0.77f, 0f), Quaternion.identity, new Vector3(0.34f, 0.2f, 0.34f), material);
        return SavePrefab(root, PoopPrefabPath);
    }

    private static void AssignPoopPrefabToPlayerPrefab(GameObject poopPrefab)
    {
        if (poopPrefab == null)
        {
            return;
        }

        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        if (playerPrefab == null)
        {
            return;
        }

        MiniVanPlayer player = playerPrefab.GetComponent<MiniVanPlayer>();
        if (player != null && player.HotPotatoPoopPrefab == poopPrefab)
        {
            return;
        }

        GameObject contents = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            MiniVanPlayer contentsPlayer = contents.GetComponent<MiniVanPlayer>();
            if (contentsPlayer != null)
            {
                contentsPlayer.HotPotatoPoopPrefab = poopPrefab;
                PrefabUtility.SaveAsPrefabAsset(contents, PlayerPrefabPath);
                AssetDatabase.SaveAssets();
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    private static void PlaceTestObjectsIfMissing(GameObject bombPrefab, GameObject dummyPrefab)
    {
        if (bombPrefab == null || dummyPrefab == null)
        {
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return;
        }

        if (scene.path.EndsWith("MiniVan_Menu.unity"))
        {
            return;
        }

        bool changed = false;
        Vector3 basePosition = GetDefaultTestPosition();
        if (Object.FindFirstObjectByType<MiniVanHotPotatoBomb>() == null)
        {
            GameObject bomb = PrefabUtility.InstantiatePrefab(bombPrefab) as GameObject;
            if (bomb != null)
            {
                bomb.name = "HotPotatoBomb_Test";
                bomb.transform.position = basePosition + new Vector3(0f, 0.45f, 0f);
                changed = true;
            }
        }

        if (Object.FindFirstObjectByType<MiniVanHotPotatoDummy>() == null)
        {
            GameObject dummy = PrefabUtility.InstantiatePrefab(dummyPrefab) as GameObject;
            if (dummy != null)
            {
                dummy.name = "HotPotatoDummy_Test";
                dummy.transform.position = basePosition + new Vector3(5.5f, 0f, 0f);
                dummy.transform.rotation = Quaternion.Euler(0f, -90f, 0f);
                changed = true;
            }
        }

        if (changed)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }

    private static Vector3 GetDefaultTestPosition()
    {
        GameObject spawn = GameObject.Find("Player Spawn Point");
        if (spawn != null)
        {
            return spawn.transform.position + spawn.transform.right * 4f + spawn.transform.forward * 3f;
        }

        return new Vector3(4f, 0f, 4f);
    }

    private static GameObject AddPrimitive(Transform parent, PrimitiveType type, string name, Vector3 localPosition, Quaternion localRotation, Vector3 localScale, Material material)
    {
        GameObject primitive = GameObject.CreatePrimitive(type);
        primitive.name = name;
        primitive.transform.SetParent(parent, false);
        primitive.transform.localPosition = localPosition;
        primitive.transform.localRotation = localRotation;
        primitive.transform.localScale = localScale;

        Renderer renderer = primitive.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = material;
        }

        Collider collider = primitive.GetComponent<Collider>();
        if (collider != null)
        {
            Object.DestroyImmediate(collider);
        }

        return primitive;
    }

    private static Material CreateMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        Material material = new Material(shader);
        material.color = color;
        return material;
    }

    private static GameObject SavePrefab(GameObject root, string path)
    {
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        return prefab;
    }
}
