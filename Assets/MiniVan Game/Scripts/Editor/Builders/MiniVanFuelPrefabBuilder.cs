using MiniVanGame;
using UnityEditor;
using UnityEngine;

public static class MiniVanFuelPrefabBuilder
{
    private const string TextureFolder = "Assets/MiniVan Game/Textures/Vehicles/FuelSystem";
    private const string MaterialFolder =
        "Assets/MiniVan Game/Materials/Resources/FuelSystem/Materials";
    private const string MiniVanPrefabPath =
        "Assets/MiniVan Game/Prefabs/Vehicles/MiniVan/MiniVan.prefab";

    [MenuItem("MiniVan/Build Fuel Visuals Into Prefab")]
    public static void BuildFuelVisualsIntoPrefab()
    {
        EnsureFolderPath(TextureFolder);
        EnsureFolderPath(MaterialFolder);

        BuildMaterial("GaugeMetal", new Color(0.045f, 0.050f, 0.055f), new Color(0.22f, 0.16f, 0.10f), 11, 0.72f, 0.24f, false);
        BuildMaterial("GaugeEdge", new Color(0.10f, 0.095f, 0.085f), new Color(0.32f, 0.16f, 0.07f), 19, 0.78f, 0.30f, false);
        BuildMaterial("GaugeFace", new Color(0.018f, 0.020f, 0.021f), new Color(0.075f, 0.080f, 0.078f), 23, 0.15f, 0.12f, false);
        BuildMaterial("GaugeEmpty", new Color(0.26f, 0.010f, 0.008f), new Color(0.58f, 0.035f, 0.020f), 29, 0.12f, 0.16f, false);
        BuildMaterial("GaugeMarks", new Color(0.76f, 0.71f, 0.52f), new Color(0.98f, 0.91f, 0.66f), 31, 0.05f, 0.20f, true);
        BuildMaterial("GaugeRed", new Color(0.48f, 0.012f, 0.008f), new Color(0.95f, 0.06f, 0.025f), 37, 0.10f, 0.18f, true);
        BuildMaterial("GaugeAmber", new Color(0.66f, 0.24f, 0.008f), new Color(1.0f, 0.62f, 0.035f), 41, 0.10f, 0.18f, true);
        BuildMaterial("GaugeGreen", new Color(0.08f, 0.42f, 0.035f), new Color(0.30f, 0.95f, 0.16f), 43, 0.10f, 0.18f, true);

        BuildMaterial("FurnaceSteel", new Color(0.035f, 0.038f, 0.040f), new Color(0.25f, 0.11f, 0.045f), 53, 0.72f, 0.18f, false);
        BuildMaterial("FurnaceEdge", new Color(0.075f, 0.070f, 0.062f), new Color(0.34f, 0.16f, 0.060f), 59, 0.78f, 0.22f, false);
        BuildMaterial("FurnaceSoot", new Color(0.006f, 0.005f, 0.004f), new Color(0.055f, 0.035f, 0.020f), 61, 0.05f, 0.03f, false);
        BuildMaterial("FurnaceRust", new Color(0.18f, 0.055f, 0.018f), new Color(0.52f, 0.21f, 0.055f), 67, 0.30f, 0.12f, false);
        BuildMaterial("FurnaceFire", new Color(0.92f, 0.08f, 0.004f), new Color(1.0f, 0.62f, 0.025f), 71, 0.02f, 0.08f, true);
        BuildMaterial("FurnaceLamp", new Color(0.82f, 0.23f, 0.005f), new Color(1.0f, 0.72f, 0.035f), 73, 0.08f, 0.20f, true);
        BuildMaterial("FurnaceText", new Color(0.68f, 0.63f, 0.48f), new Color(0.93f, 0.88f, 0.67f), 79, 0.04f, 0.18f, true);

        AssetDatabase.SaveAssets();

        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(MiniVanPrefabPath);
        try
        {
            MiniVanVehicle vehicle = prefabRoot.GetComponent<MiniVanVehicle>();
            if (vehicle == null)
            {
                throw new MissingComponentException("MiniVan.prefab has no MiniVanVehicle component.");
            }

            vehicle.RebuildFuelVisuals();
            EditorUtility.SetDirty(vehicle);
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, MiniVanPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[MiniVanFuel] Textured gauge and furnace baked into MiniVan.prefab.");
    }

    private static void BuildMaterial(string name, Color baseColor, Color wearColor, int seed,
        float metallic, float smoothness, bool emission)
    {
        Texture2D texture = BuildTexture(name, baseColor, wearColor, seed);
        string path = MaterialFolder + "/" + name + ".mat";
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
        else if (material.shader != shader)
        {
            material.shader = shader;
        }

        material.name = name;
        material.color = Color.white;
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
        if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
        if (emission && material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", wearColor * 1.8f);
            if (material.HasProperty("_EmissionMap")) material.SetTexture("_EmissionMap", texture);
        }
        else
        {
            material.DisableKeyword("_EMISSION");
        }

        EditorUtility.SetDirty(material);
    }

    private static Texture2D BuildTexture(string name, Color baseColor, Color wearColor, int seed)
    {
        string path = TextureFolder + "/" + name + ".asset";
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (texture == null)
        {
            texture = new Texture2D(64, 64, TextureFormat.RGBA32, true)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Repeat
            };
            AssetDatabase.CreateAsset(texture, path);
        }
        else if (texture.width != 64 || texture.height != 64)
        {
            texture.Reinitialize(64, 64, TextureFormat.RGBA32, true);
        }

        Color[] pixels = new Color[64 * 64];
        for (int y = 0; y < 64; y++)
        {
            for (int x = 0; x < 64; x++)
            {
                float noise = Hash01(x, y, seed);
                float broad = Hash01(x / 5, y / 5, seed + 101);
                float wear = Mathf.Clamp01((noise - 0.72f) * 2.9f + (broad - 0.62f) * 0.65f);
                bool scratch = ((x * 13 + y * 7 + seed) % 97 == 0) ||
                               (y % 19 == seed % 19 && x > 7 && x < 57 && noise > 0.42f);
                bool edgeWear = x < 3 || x > 60 || y < 3 || y > 60;
                float blend = Mathf.Clamp01(wear + (scratch ? 0.55f : 0f) + (edgeWear ? 0.18f : 0f));
                Color color = Color.Lerp(baseColor * Mathf.Lerp(0.78f, 1.12f, noise), wearColor, blend);
                pixels[y * 64 + x] = new Color(color.r, color.g, color.b, 1f);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(true, false);
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Repeat;
        EditorUtility.SetDirty(texture);
        return texture;
    }

    private static float Hash01(int x, int y, int seed)
    {
        unchecked
        {
            uint value = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
            value = (value ^ (value >> 13)) * 1274126177u;
            return (value ^ (value >> 16)) / (float)uint.MaxValue;
        }
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
