using System.Collections.Generic;
using MiniVanGame;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Bakes the locator device model into AntonLocator.prefab so it can be edited by hand
/// instead of being generated at runtime.
/// </summary>
internal static class MiniVanAntonLocatorPrefabBuilder
{
    private const string PrefabPath = "Assets/MiniVan Game/Resources/MiniVan/AntonLocator.prefab";
    private const string MaterialFolder = "Assets/MiniVan Game/Materials/AntonLocator";

    [MenuItem("MiniVan Game/Anton/Rebuild Locator Model In Prefab")]
    public static void RebuildLocatorModel()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogError("[MiniVanAntonLocatorPrefabBuilder] Prefab not found: " + PrefabPath);
            return;
        }

        EnsureMaterialFolder();

        GameObject contents = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Transform existing = contents.transform.Find(MiniVanAntonLocatorVisual.VisualChildName);
            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
            }

            Transform visual = MiniVanAntonLocatorVisual.Build(
                contents.transform,
                MiniVanAntonLocatorVisual.VisualChildName,
                worldScale: 1f);

            SaveMaterialsAsAssets(visual);
            PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[MiniVanAntonLocatorPrefabBuilder] Locator model baked into " + PrefabPath);
    }

    private static void EnsureMaterialFolder()
    {
        if (AssetDatabase.IsValidFolder(MaterialFolder))
        {
            return;
        }

        if (!AssetDatabase.IsValidFolder("Assets/MiniVan Game/Materials"))
        {
            AssetDatabase.CreateFolder("Assets/MiniVan Game", "Materials");
        }

        AssetDatabase.CreateFolder("Assets/MiniVan Game/Materials", "AntonLocator");
    }

    /// <summary>
    /// Runtime-created materials are not serializable inside a prefab, so persist each unique one.
    /// </summary>
    private static void SaveMaterialsAsAssets(Transform visualRoot)
    {
        Dictionary<Material, Material> saved = new Dictionary<Material, Material>();
        Renderer[] renderers = visualRoot.GetComponentsInChildren<Renderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            Material source = renderer.sharedMaterial;
            if (source == null || EditorUtility.IsPersistent(source))
            {
                continue;
            }

            if (!saved.TryGetValue(source, out Material asset))
            {
                string path = AssetDatabase.GenerateUniqueAssetPath(
                    MaterialFolder + "/Locator" + renderer.gameObject.name + ".mat");
                AssetDatabase.CreateAsset(source, path);
                asset = AssetDatabase.LoadAssetAtPath<Material>(path);
                saved[source] = asset;
            }

            renderer.sharedMaterial = asset;
        }
    }
}
