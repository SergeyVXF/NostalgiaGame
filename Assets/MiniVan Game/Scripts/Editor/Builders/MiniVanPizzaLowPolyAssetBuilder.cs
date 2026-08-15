using System;
using System.Collections.Generic;
using MiniVanGame;
using UnityEditor;
using UnityEngine;

public static class MiniVanPizzaLowPolyAssetBuilder
{
    private const string PrefabRoot = "Assets/MiniVan Game/Prefabs/Resources/PizzaLoop";
    private const string MaterialRoot = "Assets/MiniVan Game/Materials/Items/Pizza/Generated";
    private const string TextureRoot = "Assets/MiniVan Game/Textures/Items/Pizza";
    private const string ModelRoot = "Assets/MiniVan Game/Models/Items/Pizza";

    [MenuItem("MiniVan/Build Low Poly Pizza Props")]
    public static void BuildAll()
    {
        EnsureFolder(PrefabRoot);
        EnsureFolder(MaterialRoot);
        EnsureFolder(TextureRoot);
        EnsureFolder(ModelRoot);

        Material paper = MakeMaterial("Paper", new Color(0.82f, 0.76f, 0.62f), new Color(0.18f, 0.28f, 0.42f), 0.78f);
        Material glass = MakeMaterial("Glass", new Color(0.54f, 0.76f, 0.82f), new Color(0.22f, 0.36f, 0.42f), 0.24f, true);
        Material metal = MakeMaterial("Metal", new Color(0.55f, 0.57f, 0.53f), new Color(0.25f, 0.27f, 0.24f), 0.42f);
        Material tomato = MakeMaterial("Tomato", new Color(0.66f, 0.08f, 0.045f), new Color(0.42f, 0.025f, 0.02f), 0.72f);
        Material cheese = MakeMaterial("Cheese", new Color(0.96f, 0.65f, 0.10f), new Color(0.76f, 0.38f, 0.04f), 0.66f);
        Material sausage = MakeMaterial("Sausage", new Color(0.62f, 0.18f, 0.13f), new Color(0.38f, 0.07f, 0.05f), 0.72f);
        Material sausageCut = MakeMaterial("SausageCut", new Color(0.82f, 0.38f, 0.34f), new Color(0.58f, 0.15f, 0.12f), 0.74f);
        Material dough = MakeMaterial("Dough", new Color(0.88f, 0.70f, 0.43f), new Color(0.72f, 0.49f, 0.24f), 0.82f);
        Material crust = MakeMaterial("Crust", new Color(0.83f, 0.43f, 0.12f), new Color(0.56f, 0.22f, 0.055f), 0.72f);
        Material cookedCheese = MakeMaterial("CookedCheese", new Color(0.98f, 0.67f, 0.12f), new Color(0.74f, 0.30f, 0.04f), 0.62f);
        Material burned = MakeMaterial("Burned", new Color(0.16f, 0.08f, 0.045f), new Color(0.05f, 0.025f, 0.015f), 0.88f);

        Mesh cylinder12 = SaveMesh("Cylinder12", CreateCylinder(0.5f, 1f, 12));
        Mesh cylinder16 = SaveMesh("Cylinder16", CreateCylinder(0.5f, 1f, 16));
        Mesh doughBall = SaveMesh("DoughBall", CreateUvSphere(12, 6));
        Mesh bottle = SaveMesh("Bottle", CreateLathe(
            new[] { 0.32f, 0.34f, 0.34f, 0.31f, 0.30f, 0.20f, 0.16f, 0.16f },
            new[] { -0.50f, -0.45f, 0.20f, 0.28f, 0.34f, 0.40f, 0.46f, 0.50f }, 12));
        Mesh sausageMesh = SaveMesh("CurvedSausage", CreateCurvedTube(8, 8));
        Mesh cheeseWedge = SaveMesh("CheeseWedge", CreateWedge());

        SavePrefab(MiniVanInventoryItem.Flour, BuildFlour(paper));
        SavePrefab(MiniVanInventoryItem.Water, BuildWater(bottle, cylinder12, glass, metal));
        SavePrefab(MiniVanInventoryItem.TomatoPaste, BuildTomatoPaste(cylinder12, metal, tomato));
        SavePrefab(MiniVanInventoryItem.Cheese, BuildCheese(cheeseWedge, cheese));
        SavePrefab(MiniVanInventoryItem.Sausage, BuildSausage(sausageMesh, sausage));
        SavePrefab(MiniVanInventoryItem.SlicedSausage, BuildSlicedSausage(cylinder12, sausage, sausageCut));
        SavePrefab(MiniVanInventoryItem.Dough, BuildDough(doughBall, dough));
        SavePrefab(MiniVanInventoryItem.RoundDough, BuildRoundDough(cylinder16, dough));
        SavePrefab(MiniVanInventoryItem.GratedCheese, BuildGratedCheese(cheese));
        SavePrefab(MiniVanInventoryItem.RawPizza, BuildPizza(cylinder16, cylinder12, dough, tomato, cheese, sausageCut, false, false));
        SavePrefab(MiniVanInventoryItem.CookedPizza, BuildPizza(cylinder16, cylinder12, crust, tomato, cookedCheese, sausage, true, false));
        SavePrefab(MiniVanInventoryItem.BurnedPizza, BuildPizza(cylinder16, cylinder12, burned, tomato, burned, sausage, true, true));

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[MiniVanPizzaLowPolyAssetBuilder] Built low-poly pizza props in " + PrefabRoot);
    }

    private static GameObject BuildFlour(Material paper)
    {
        GameObject root = NewRoot("LowPoly Flour");
        AddCube(root.transform, "Paper Bag", new Vector3(0f, 0.16f, 0f), new Vector3(0.23f, 0.31f, 0.14f), paper);
        AddCube(root.transform, "Folded Top", new Vector3(0f, 0.325f, 0f), new Vector3(0.21f, 0.035f, 0.13f), paper, new Vector3(0f, 0f, 4f));
        return root;
    }

    private static GameObject BuildWater(Mesh bottle, Mesh cap, Material glass, Material metal)
    {
        GameObject root = NewRoot("LowPoly Water");
        AddMesh(root.transform, "Bottle", bottle, new Vector3(0f, 0.17f, 0f), new Vector3(0.19f, 0.36f, 0.19f), glass);
        AddMesh(root.transform, "Cap", cap, new Vector3(0f, 0.36f, 0f), new Vector3(0.072f, 0.035f, 0.072f), metal);
        return root;
    }

    private static GameObject BuildTomatoPaste(Mesh cylinder, Material metal, Material tomato)
    {
        GameObject root = NewRoot("LowPoly Tomato Paste");
        AddMesh(root.transform, "Tin", cylinder, new Vector3(0f, 0.09f, 0f), new Vector3(0.16f, 0.18f, 0.16f), metal);
        AddMesh(root.transform, "Paste", cylinder, new Vector3(0f, 0.186f, 0f), new Vector3(0.145f, 0.012f, 0.145f), tomato);
        AddMesh(root.transform, "Bent Lid", cylinder, new Vector3(0f, 0.235f, 0.075f), new Vector3(0.145f, 0.008f, 0.145f), metal, new Vector3(55f, 0f, 0f));
        return root;
    }

    private static GameObject BuildCheese(Mesh wedge, Material cheese)
    {
        GameObject root = NewRoot("LowPoly Cheese");
        AddMesh(root.transform, "Cheese Wedge", wedge, new Vector3(0f, 0.10f, 0f), new Vector3(0.30f, 0.20f, 0.22f), cheese);
        return root;
    }

    private static GameObject BuildSausage(Mesh mesh, Material material)
    {
        GameObject root = NewRoot("LowPoly Sausage");
        AddMesh(root.transform, "Curved Sausage", mesh, new Vector3(0f, 0.09f, 0f), new Vector3(0.38f, 0.12f, 0.28f), material);
        return root;
    }

    private static GameObject BuildSlicedSausage(Mesh cylinder, Material casing, Material cut)
    {
        GameObject root = NewRoot("LowPoly Sliced Sausage");
        Vector3[] positions =
        {
            new Vector3(-0.09f, 0.025f, -0.05f), new Vector3(0.01f, 0.026f, -0.07f),
            new Vector3(0.10f, 0.025f, -0.02f), new Vector3(-0.06f, 0.031f, 0.05f),
            new Vector3(0.05f, 0.032f, 0.045f), new Vector3(0f, 0.065f, 0f)
        };
        for (int i = 0; i < positions.Length; i++)
        {
            AddMesh(root.transform, "Slice " + i, cylinder, positions[i], new Vector3(0.085f, 0.022f, 0.085f), i == positions.Length - 1 ? cut : casing, new Vector3(0f, i * 17f, 0f));
        }
        return root;
    }

    private static GameObject BuildDough(Mesh sphere, Material dough)
    {
        GameObject root = NewRoot("LowPoly Dough");
        AddMesh(root.transform, "Dough Ball", sphere, new Vector3(0f, 0.09f, 0f), new Vector3(0.24f, 0.16f, 0.22f), dough);
        return root;
    }

    private static GameObject BuildRoundDough(Mesh cylinder, Material dough)
    {
        GameObject root = NewRoot("LowPoly Round Dough");
        AddMesh(root.transform, "Rolled Dough", cylinder, new Vector3(0f, 0.025f, 0f), new Vector3(0.55f, 0.045f, 0.55f), dough);
        return root;
    }

    private static GameObject BuildGratedCheese(Material cheese)
    {
        GameObject root = NewRoot("LowPoly Grated Cheese");
        for (int i = 0; i < 18; i++)
        {
            float angle = i * 2.39996f;
            float radius = 0.018f + 0.008f * i;
            Vector3 position = new Vector3(Mathf.Cos(angle) * radius, 0.018f + (i % 4) * 0.012f, Mathf.Sin(angle) * radius * 0.72f);
            AddCube(root.transform, "Cheese Shred " + i, position, new Vector3(0.055f + (i % 3) * 0.012f, 0.014f, 0.018f), cheese, new Vector3(0f, i * 37f, (i % 3 - 1) * 12f));
        }
        return root;
    }

    private static GameObject BuildPizza(Mesh disk, Mesh slice, Material baseMaterial, Material tomato, Material cheese, Material sausage, bool cooked, bool charred)
    {
        GameObject root = NewRoot(charred ? "LowPoly Burned Pizza" : cooked ? "LowPoly Cooked Pizza" : "LowPoly Raw Pizza");
        AddMesh(root.transform, "Crust", disk, new Vector3(0f, 0.025f, 0f), new Vector3(0.64f, 0.05f, 0.64f), baseMaterial);
        AddMesh(root.transform, "Sauce", disk, new Vector3(0f, 0.055f, 0f), new Vector3(0.53f, 0.012f, 0.53f), tomato);
        AddMesh(root.transform, "Cheese", disk, new Vector3(0f, 0.066f, 0f), new Vector3(0.49f, 0.012f, 0.49f), cheese);
        for (int i = 0; i < 9; i++)
        {
            float angle = i * 2.39996f;
            float radius = i == 0 ? 0f : 0.09f + 0.018f * i;
            Vector3 position = new Vector3(Mathf.Cos(angle) * radius, 0.083f, Mathf.Sin(angle) * radius);
            AddMesh(root.transform, "Sausage Topping " + i, slice, position, new Vector3(0.075f, 0.018f, 0.075f), sausage, new Vector3(0f, i * 23f, 0f));
        }
        return root;
    }

    private static GameObject NewRoot(string name)
    {
        GameObject root = new GameObject(name);
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;
        return root;
    }

    private static void SavePrefab(MiniVanInventoryItem item, GameObject root)
    {
        MiniVanPizzaItem pizzaItem = root.AddComponent<MiniVanPizzaItem>();
        pizzaItem.Item = item;
        BoxCollider box = root.AddComponent<BoxCollider>();
        Bounds bounds = CalculateBounds(root);
        box.center = bounds.center;
        box.size = bounds.size + Vector3.one * 0.015f;
        PrefabUtility.SaveAsPrefabAsset(
            root, PrefabRoot + "/PizzaItem_" + item + ".prefab");
        UnityEngine.Object.DestroyImmediate(root);
    }

    private static GameObject AddMesh(Transform parent, string name, Mesh mesh, Vector3 position, Vector3 scale, Material material, Vector3 rotation = default)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localEulerAngles = rotation;
        go.transform.localScale = scale;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = material;
        return go;
    }

    private static GameObject AddCube(Transform parent, string name, Vector3 position, Vector3 scale, Material material, Vector3 rotation = default)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localEulerAngles = rotation;
        go.transform.localScale = scale;
        UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        go.GetComponent<Renderer>().sharedMaterial = material;
        return go;
    }

    private static Bounds CalculateBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        Bounds world = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) world.Encapsulate(renderers[i].bounds);
        return new Bounds(root.transform.InverseTransformPoint(world.center), world.size);
    }

    private static Material MakeMaterial(string name, Color a, Color b, float smoothness, bool transparent = false)
    {
        Texture2D texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        texture.name = "TX_Pizza_" + name;
        texture.wrapMode = TextureWrapMode.Repeat;
        texture.filterMode = FilterMode.Point;
        for (int y = 0; y < 64; y++)
        {
            for (int x = 0; x < 64; x++)
            {
                float wave = Mathf.PerlinNoise(x * 0.13f + name.Length, y * 0.13f + name.Length * 0.7f);
                float stripe = ((x / 8 + y / 8) & 1) == 0 ? 0.08f : -0.04f;
                Color color = Color.Lerp(a, b, Mathf.Clamp01(wave * 0.45f + 0.18f + stripe));
                color.a = transparent ? 0.72f : 1f;
                texture.SetPixel(x, y, color);
            }
        }
        texture.Apply();
        string texturePath = TextureRoot + "/TX_Pizza_" + name + ".asset";
        ReplaceAsset(texture, texturePath);

        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material material = new Material(shader) { name = "MAT_Pizza_" + name };
        material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        material.color = Color.white;
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", material.mainTexture);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 1f - smoothness);
        if (transparent)
        {
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_ZWrite", 0f);
            material.renderQueue = 3000;
        }
        string materialPath = MaterialRoot + "/MAT_Pizza_" + name + ".mat";
        ReplaceAsset(material, materialPath);
        return AssetDatabase.LoadAssetAtPath<Material>(materialPath);
    }

    private static Mesh SaveMesh(string name, Mesh mesh)
    {
        mesh.name = "MESH_Pizza_" + name;
        string path = ModelRoot + "/MESH_Pizza_" + name + ".asset";
        ReplaceAsset(mesh, path);
        return AssetDatabase.LoadAssetAtPath<Mesh>(path);
    }

    private static void ReplaceAsset(UnityEngine.Object asset, string path)
    {
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(asset, path);
    }

    private static Mesh CreateCylinder(float radius, float height, int segments)
    {
        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uv = new List<Vector2>();
        List<int> triangles = new List<int>();
        float half = height * 0.5f;
        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            float angle = t * Mathf.PI * 2f;
            Vector3 ring = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            vertices.Add(ring + Vector3.down * half); uv.Add(new Vector2(t, 0f));
            vertices.Add(ring + Vector3.up * half); uv.Add(new Vector2(t, 1f));
            if (i < segments)
            {
                int v = i * 2;
                triangles.Add(v); triangles.Add(v + 1); triangles.Add(v + 3);
                triangles.Add(v); triangles.Add(v + 3); triangles.Add(v + 2);
            }
        }
        int bottomCenter = vertices.Count; vertices.Add(Vector3.down * half); uv.Add(new Vector2(0.5f, 0.5f));
        int topCenter = vertices.Count; vertices.Add(Vector3.up * half); uv.Add(new Vector2(0.5f, 0.5f));
        for (int i = 0; i < segments; i++)
        {
            int b0 = i * 2; int b1 = (i + 1) * 2;
            triangles.Add(bottomCenter); triangles.Add(b1); triangles.Add(b0);
            triangles.Add(topCenter); triangles.Add(b0 + 1); triangles.Add(b1 + 1);
        }
        return FinishMesh(vertices, uv, triangles);
    }

    private static Mesh CreateLathe(float[] radii, float[] heights, int segments)
    {
        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uv = new List<Vector2>();
        List<int> triangles = new List<int>();
        for (int y = 0; y < radii.Length; y++)
        {
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                float a = t * Mathf.PI * 2f;
                vertices.Add(new Vector3(Mathf.Cos(a) * radii[y], heights[y], Mathf.Sin(a) * radii[y]));
                uv.Add(new Vector2(t, y / (float)(radii.Length - 1)));
            }
        }
        int row = segments + 1;
        for (int y = 0; y < radii.Length - 1; y++)
        for (int i = 0; i < segments; i++)
        {
            int v = y * row + i;
            triangles.Add(v); triangles.Add(v + row); triangles.Add(v + row + 1);
            triangles.Add(v); triangles.Add(v + row + 1); triangles.Add(v + 1);
        }
        return FinishMesh(vertices, uv, triangles);
    }

    private static Mesh CreateUvSphere(int longitude, int latitude)
    {
        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uv = new List<Vector2>();
        List<int> triangles = new List<int>();
        for (int y = 0; y <= latitude; y++)
        {
            float v = y / (float)latitude;
            float phi = v * Mathf.PI;
            for (int x = 0; x <= longitude; x++)
            {
                float u = x / (float)longitude;
                float theta = u * Mathf.PI * 2f;
                vertices.Add(new Vector3(Mathf.Sin(phi) * Mathf.Cos(theta), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(theta)) * 0.5f);
                uv.Add(new Vector2(u, 1f - v));
            }
        }
        int row = longitude + 1;
        for (int y = 0; y < latitude; y++)
        for (int x = 0; x < longitude; x++)
        {
            int p = y * row + x;
            triangles.Add(p); triangles.Add(p + row); triangles.Add(p + row + 1);
            triangles.Add(p); triangles.Add(p + row + 1); triangles.Add(p + 1);
        }
        return FinishMesh(vertices, uv, triangles);
    }

    private static Mesh CreateCurvedTube(int lengthSegments, int sides)
    {
        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uv = new List<Vector2>();
        List<int> triangles = new List<int>();
        for (int i = 0; i <= lengthSegments; i++)
        {
            float t = i / (float)lengthSegments;
            float curve = Mathf.Lerp(-0.72f, 0.72f, t);
            Vector3 center = new Vector3(Mathf.Sin(curve) * 0.9f, 0f, (Mathf.Cos(curve) - 0.82f) * 0.65f);
            Vector3 tangent = new Vector3(Mathf.Cos(curve), 0f, -Mathf.Sin(curve)).normalized;
            Vector3 side = Vector3.Cross(Vector3.up, tangent).normalized;
            for (int s = 0; s <= sides; s++)
            {
                float a = s / (float)sides * Mathf.PI * 2f;
                vertices.Add(center + Vector3.up * Mathf.Cos(a) * 0.18f + side * Mathf.Sin(a) * 0.18f);
                uv.Add(new Vector2(t, s / (float)sides));
            }
        }
        int row = sides + 1;
        for (int i = 0; i < lengthSegments; i++)
        for (int s = 0; s < sides; s++)
        {
            int p = i * row + s;
            triangles.Add(p); triangles.Add(p + row); triangles.Add(p + row + 1);
            triangles.Add(p); triangles.Add(p + row + 1); triangles.Add(p + 1);
        }
        return FinishMesh(vertices, uv, triangles);
    }

    private static Mesh CreateWedge()
    {
        Vector3[] vertices =
        {
            new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(-0.5f, -0.5f, 0.5f), new Vector3(0.5f, -0.5f, 0f),
            new Vector3(-0.5f, 0.5f, -0.5f), new Vector3(-0.5f, 0.5f, 0.5f), new Vector3(0.5f, 0.5f, 0f)
        };
        int[] triangles = { 0, 2, 1, 3, 4, 5, 0, 3, 5, 0, 5, 2, 1, 2, 5, 1, 5, 4, 0, 1, 4, 0, 4, 3 };
        Vector2[] uv = { Vector2.zero, Vector2.up, Vector2.right, Vector2.zero, Vector2.up, Vector2.right };
        Mesh mesh = new Mesh { vertices = vertices, triangles = triangles, uv = uv };
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        return mesh;
    }

    private static Mesh FinishMesh(List<Vector3> vertices, List<Vector2> uv, List<int> triangles)
    {
        Mesh mesh = new Mesh();
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uv);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
