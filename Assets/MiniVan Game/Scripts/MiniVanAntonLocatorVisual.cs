using UnityEngine;

namespace MiniVanGame
{
    /// <summary>
    /// Locator device model: body + textured CRT screen + TextMesh height labels + center blip.
    /// Used by the editor prefab builder and as a runtime fallback.
    /// </summary>
    public static class MiniVanAntonLocatorVisual
    {
        public const string ScreenTextureResource = "MiniVan/AntonLocatorScreen";
        public const string VisualChildName = "LocatorVisual";
        public const string WorldTextDepthMaterial = "Panelka_WorldTextDepth";

        private static Texture2D cachedScreenTexture;

        /// <summary>
        /// Instantiates the model from the locator prefab so edits to the prefab apply everywhere.
        /// Falls back to procedural construction when the prefab has no model yet.
        /// </summary>
        public static Transform Spawn(Transform parent, string name, float worldScale)
        {
            GameObject prefab = MiniVanAntonTestSpawner.ResolveAntonLocatorPrefab();
            Transform template = prefab != null ? prefab.transform.Find(VisualChildName) : null;
            if (template == null)
            {
                return Build(parent, name, worldScale);
            }

            GameObject copy = Object.Instantiate(template.gameObject, parent);
            copy.name = name;
            copy.transform.localPosition = Vector3.zero;
            copy.transform.localRotation = Quaternion.identity;
            copy.transform.localScale = Vector3.one * worldScale;
            StripColliders(copy.transform);
            return copy.transform;
        }

        public static Transform Build(Transform parent, string name, float worldScale)
        {
            GameObject root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localScale = Vector3.one * worldScale;

            Material frameMat = MakeLit(new Color(0.12f, 0.12f, 0.13f));
            Material screwMat = MakeLit(new Color(0.35f, 0.35f, 0.36f));

            GameObject frame = MakePrimitive(PrimitiveType.Cube, "Frame", root.transform);
            frame.transform.localScale = new Vector3(0.42f, 0.42f, 0.08f);
            SetMat(frame, frameMat);

            GameObject bezel = MakePrimitive(PrimitiveType.Cube, "Bezel", root.transform);
            bezel.transform.localPosition = new Vector3(0f, 0f, -0.042f);
            bezel.transform.localScale = new Vector3(0.34f, 0.34f, 0.02f);
            SetMat(bezel, MakeLit(new Color(0.08f, 0.08f, 0.09f)));

            // Unity quads face local -Z, which is the side the holder looks at.
            GameObject screen = MakePrimitive(PrimitiveType.Quad, "Screen", root.transform);
            screen.transform.localPosition = new Vector3(0f, 0f, -0.055f);
            screen.transform.localScale = new Vector3(0.30f, 0.30f, 1f);
            SetMat(screen, MakeUnlitTextured(ResolveScreenTexture()));

            GameObject blip = MakePrimitive(PrimitiveType.Quad, "Blip", screen.transform);
            blip.transform.localPosition = new Vector3(0f, 0f, -0.01f);
            blip.transform.localScale = new Vector3(0.09f, 0.09f, 1f);
            SetMat(blip, MakeUnlitColor(new Color(0.9f, 0.15f, 0.1f)));

            Font font = ResolveFont();
            CreateLabel(screen.transform, "LabelVyshe", "ВЫШЕ", new Vector3(-0.42f, 0.32f, -0.012f), font);
            CreateLabel(screen.transform, "LabelUroven", "УРОВЕНЬ", new Vector3(-0.42f, 0f, -0.012f), font);
            CreateLabel(screen.transform, "LabelNizhe", "НИЖЕ", new Vector3(-0.42f, -0.32f, -0.012f), font);

            root.AddComponent<MiniVanPanelkaWorldTextDepth>();

            PlaceScrew(root.transform, screwMat, new Vector3(-0.17f, 0.17f, -0.045f));
            PlaceScrew(root.transform, screwMat, new Vector3(0.17f, 0.17f, -0.045f));
            PlaceScrew(root.transform, screwMat, new Vector3(-0.17f, -0.17f, -0.045f));
            PlaceScrew(root.transform, screwMat, new Vector3(0.17f, -0.17f, -0.045f));

            return root.transform;
        }

        public static Renderer GetScreenRenderer(Transform visualRoot)
        {
            Transform screen = visualRoot != null ? visualRoot.Find("Screen") : null;
            return screen != null ? screen.GetComponent<Renderer>() : null;
        }

        public static Renderer GetBlipRenderer(Transform visualRoot)
        {
            Transform blip = visualRoot != null ? visualRoot.Find("Screen/Blip") : null;
            return blip != null ? blip.GetComponent<Renderer>() : null;
        }

        public static TextMesh GetLabel(Transform visualRoot, string name)
        {
            Transform t = visualRoot != null ? visualRoot.Find("Screen/" + name) : null;
            return t != null ? t.GetComponent<TextMesh>() : null;
        }

        public static Texture2D ResolveScreenTexture()
        {
            if (cachedScreenTexture != null)
            {
                return cachedScreenTexture;
            }

            Texture2D fromResources = Resources.Load<Texture2D>(ScreenTextureResource);
            cachedScreenTexture = fromResources != null ? fromResources : BuildProceduralRadarTexture(256);
            return cachedScreenTexture;
        }

        private static void StripColliders(Transform root)
        {
            Collider[] found = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < found.Length; i++)
            {
                DestroyObject(found[i]);
            }
        }

        private static Texture2D BuildProceduralRadarTexture(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "AntonLocatorScreenProcedural",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            Color bg = new Color(0.02f, 0.10f, 0.05f, 1f);
            Color ring = new Color(0.25f, 0.85f, 0.40f, 1f);
            Color dim = new Color(0.08f, 0.28f, 0.14f, 1f);
            Color corner = new Color(0.18f, 0.55f, 0.28f, 1f);

            float center = (size - 1) * 0.5f;
            float maxR = size * 0.42f;

            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = (x - center) / maxR;
                    float ny = (y - center) / maxR;
                    float r = Mathf.Sqrt(nx * nx + ny * ny);
                    float scan = ((y & 1) == 0) ? 0.92f : 1.05f;
                    float vignette = Mathf.Clamp01(1.15f - r * 0.55f);
                    Color c = bg * scan * vignette;

                    for (int i = 1; i <= 4; i++)
                    {
                        float d = Mathf.Abs(r - i / 4f);
                        if (d < 0.018f)
                        {
                            c = Color.Lerp(c, ring, (1f - d / 0.018f) * 0.85f);
                        }
                    }

                    if ((Mathf.Abs(nx) < 0.012f || Mathf.Abs(ny) < 0.012f) && r < 1.02f)
                    {
                        c = Color.Lerp(c, dim, 0.65f);
                    }

                    if (r < 0.08f && (Mathf.Abs(nx) < 0.015f || Mathf.Abs(ny) < 0.015f || Mathf.Abs(r - 0.05f) < 0.012f))
                    {
                        c = Color.Lerp(c, ring, 0.9f);
                    }

                    float ux = x / (float)(size - 1);
                    float uy = y / (float)(size - 1);
                    if (IsCornerBracket(ux, uy) || IsCornerBracket(1f - ux, uy) ||
                        IsCornerBracket(ux, 1f - uy) || IsCornerBracket(1f - ux, 1f - uy))
                    {
                        c = Color.Lerp(c, corner, 0.8f);
                    }

                    pixels[y * size + x] = c;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply(false, true);
            return tex;
        }

        private static bool IsCornerBracket(float u, float v)
        {
            if (u > 0.14f || v > 0.14f)
            {
                return false;
            }

            bool outer = (u < 0.03f && v < 0.12f) || (v < 0.03f && u < 0.12f);
            bool gap = u > 0.05f && v > 0.05f;
            return outer && !gap;
        }

        private static void CreateLabel(Transform screen, string name, string text, Vector3 localPos, Font font)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(screen, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = new Vector3(0.012f, 0.012f, 0.012f);

            TextMesh tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.fontSize = 48;
            tm.characterSize = 1f;
            tm.anchor = TextAnchor.MiddleLeft;
            tm.alignment = TextAlignment.Left;
            tm.color = new Color(0.15f, 0.4f, 0.18f, 1f);
            tm.fontStyle = FontStyle.Bold;

            MeshRenderer mr = go.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }

            if (font != null)
            {
                tm.font = font;
            }

            if (mr != null)
            {
                // Default font material ignores depth, so the words shine through the case.
                Material depthMaterial = Resources.Load<Material>(WorldTextDepthMaterial);
                if (depthMaterial != null)
                {
                    mr.sharedMaterial = depthMaterial;
                }
                else if (font != null && font.material != null)
                {
                    mr.sharedMaterial = font.material;
                }
            }
        }

        private static Font ResolveFont()
        {
            // Builtin font is a real asset, so it survives prefab serialization.
            Font builtin = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (builtin != null && builtin.HasCharacter('Ы'))
            {
                return builtin;
            }

            Font osFont = Font.CreateDynamicFontFromOSFont(new[] { "Arial", "Segoe UI", "Tahoma", "Roboto" }, 48);
            return osFont != null ? osFont : builtin;
        }

        private static void PlaceScrew(Transform parent, Material mat, Vector3 localPos)
        {
            GameObject screw = MakePrimitive(PrimitiveType.Cylinder, "Screw", parent);
            screw.transform.localPosition = localPos;
            screw.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            screw.transform.localScale = new Vector3(0.028f, 0.008f, 0.028f);
            SetMat(screw, mat);
        }

        private static GameObject MakePrimitive(PrimitiveType type, string name, Transform parent)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            Collider collider = go.GetComponent<Collider>();
            if (collider != null)
            {
                DestroyObject(collider);
            }

            return go;
        }

        private static void DestroyObject(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(target);
            }
            else
            {
                Object.DestroyImmediate(target);
            }
        }

        private static void SetMat(GameObject go, Material mat)
        {
            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = mat;
            }
        }

        private static Material MakeLit(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            Material material = new Material(shader) { color = color };
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            return material;
        }

        private static Material MakeUnlitColor(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            if (shader == null)
            {
                return MakeLit(color);
            }

            Material material = new Material(shader) { color = color };
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            // Unlit/Texture without a map renders pure black, so always feed it something.
            if (material.HasProperty("_MainTex") && material.GetTexture("_MainTex") == null)
            {
                material.SetTexture("_MainTex", Texture2D.whiteTexture);
                if (material.HasProperty("_BaseMap"))
                {
                    material.SetTexture("_BaseMap", Texture2D.whiteTexture);
                }
            }

            return material;
        }

        private static Material MakeUnlitTextured(Texture2D tex)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Texture");
            }

            if (shader == null)
            {
                return MakeUnlitColor(new Color(0.05f, 0.22f, 0.08f));
            }

            Material material = new Material(shader) { color = Color.white };
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", Color.white);
            }

            if (tex != null)
            {
                material.mainTexture = tex;
                if (material.HasProperty("_BaseMap"))
                {
                    material.SetTexture("_BaseMap", tex);
                }

                if (material.HasProperty("_MainTex"))
                {
                    material.SetTexture("_MainTex", tex);
                }
            }

            return material;
        }
    }
}
