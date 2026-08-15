using SK.Libretro;
using SK.Libretro.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MiniVanGame.EditorTools
{
    /// <summary>
    /// Builds a pot-bellied CRT + SNES console/cartridge setup for Libretro play.
    /// </summary>
    public static class MiniVanSnesTelevisionBuilder
    {
        public const string PrefabFolder = "Assets/MiniVan Game/Prefabs/Props";
        public const string PrefabPath = PrefabFolder + "/SnesTelevision.prefab";
        public const string MaterialFolder = "Assets/MiniVan Game/Materials/Props/SnesTelevision";
        public const string InstanceVariablePath =
            "Packages/com.sk.libretro/Unity/ScriptableObjects/LibretroInstanceVariable.asset";

        private static readonly Vector3 DefaultWorldPosition = new Vector3(-5.6f, 3.04f, 1.21f);
        private static readonly Vector3 DefaultWorldEuler = new Vector3(0f, -90f, 0f);

        [MenuItem("MiniVan Game/SNES TV/Build Prefab And Place In Active Scene")]
        public static void BuildAndPlace()
        {
            EnsureFolder(PrefabFolder);
            EnsureFolder(MaterialFolder);

            Material bodyMat = CreateLitMaterial("SnesTv_Body", new Color(0.18f, 0.18f, 0.2f, 1f), 0.15f, 0.35f);
            Material bezelMat = CreateLitMaterial("SnesTv_Bezel", new Color(0.1f, 0.1f, 0.11f, 1f), 0.2f, 0.25f);
            Material screenMat = CreateLitMaterial("SnesTv_Screen", new Color(0.05f, 0.08f, 0.12f, 1f), 0.05f, 0.9f);
            Material standMat = CreateLitMaterial("SnesTv_Stand", new Color(0.35f, 0.27f, 0.18f, 1f), 0.1f, 0.25f);
            Material ledMat = CreateUnlitMaterial("SnesTv_Led", new Color(0.95f, 0.12f, 0.1f, 1f));
            Material knobMat = CreateLitMaterial("SnesTv_Knob", new Color(0.22f, 0.22f, 0.24f, 1f), 0.3f, 0.45f);
            Material snesTop = CreateLitMaterial("Snes_Top", new Color(0.72f, 0.72f, 0.74f, 1f), 0.05f, 0.35f);
            Material snesBase = CreateLitMaterial("Snes_Base", new Color(0.42f, 0.42f, 0.45f, 1f), 0.08f, 0.3f);
            Material snesPurple = CreateLitMaterial("Snes_Purple", new Color(0.55f, 0.28f, 0.72f, 1f), 0.05f, 0.4f);
            Material cartMat = CreateLitMaterial("Snes_Cart", new Color(0.78f, 0.78f, 0.8f, 1f), 0.05f, 0.3f);
            Material labelMat = CreateLitMaterial("Snes_CartLabel", new Color(0.25f, 0.12f, 0.45f, 1f), 0.05f, 0.45f);

            GameObject root = BuildSetup(
                bodyMat, bezelMat, screenMat, standMat, ledMat, knobMat,
                snesTop, snesBase, snesPurple, cartMat, labelMat);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            PlaceInActiveScene(prefab);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorGUIUtility.PingObject(prefab);
            Debug.Log("[SnesTelevision] Prefab ready: " + PrefabPath);
        }

        private static GameObject BuildSetup(
            Material bodyMat,
            Material bezelMat,
            Material screenMat,
            Material standMat,
            Material ledMat,
            Material knobMat,
            Material snesTop,
            Material snesBase,
            Material snesPurple,
            Material cartMat,
            Material labelMat)
        {
            GameObject root = new GameObject("SnesTelevision");
            MiniVanSnesTelevision tv = root.AddComponent<MiniVanSnesTelevision>();
            tv.InteractRadius = 2.8f;
            tv.CoreName = "snes9x";
            tv.GamesSubdirectory = "snes";
            tv.InstanceVariable = LoadInstanceVariable();

            // Wooden table under CRT + console.
            CreateBox(root.transform, "Table", new Vector3(0f, 0.22f, 0.05f), new Vector3(1.7f, 0.08f, 1.05f), standMat);
            CreateBox(root.transform, "Table Leg FL", new Vector3(-0.7f, 0.1f, 0.4f), new Vector3(0.08f, 0.2f, 0.08f), standMat);
            CreateBox(root.transform, "Table Leg FR", new Vector3(0.7f, 0.1f, 0.4f), new Vector3(0.08f, 0.2f, 0.08f), standMat);
            CreateBox(root.transform, "Table Leg BL", new Vector3(-0.7f, 0.1f, -0.3f), new Vector3(0.08f, 0.2f, 0.08f), standMat);
            CreateBox(root.transform, "Table Leg BR", new Vector3(0.7f, 0.1f, -0.3f), new Vector3(0.08f, 0.2f, 0.08f), standMat);

            Transform crt = BuildPotbellyCrt(root.transform, bodyMat, bezelMat, screenMat, ledMat, knobMat, out Renderer screenRenderer, out LibretroInstance libretro);
            BuildSnesConsole(root.transform, snesTop, snesBase, snesPurple, cartMat, labelMat, ledMat);

            BoxCollider trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 0.85f, 0.1f);
            trigger.size = new Vector3(1.9f, 1.7f, 1.35f);

            BoxCollider bodyCollider = root.AddComponent<BoxCollider>();
            bodyCollider.center = new Vector3(0f, 0.95f, -0.05f);
            bodyCollider.size = new Vector3(1.45f, 1.35f, 1.05f);

            tv.Libretro = libretro;
            tv.ScreenRenderer = screenRenderer;
            tv.LookAnchor = screenRenderer.transform;
            return root;
        }

        private static Transform BuildPotbellyCrt(
            Transform parent,
            Material bodyMat,
            Material bezelMat,
            Material screenMat,
            Material ledMat,
            Material knobMat,
            out Renderer screenRenderer,
            out LibretroInstance libretro)
        {
            GameObject crt = new GameObject("CRT_Potbelly");
            crt.transform.SetParent(parent, false);
            crt.transform.localPosition = new Vector3(0f, 0.26f, -0.12f);

            // Deep pot-belly: wide front, deeper rear, stacked shells.
            CreateBox(crt.transform, "Body Front", new Vector3(0f, 0.72f, 0.22f), new Vector3(1.42f, 1.05f, 0.42f), bodyMat);
            CreateBox(crt.transform, "Body Mid", new Vector3(0f, 0.74f, -0.08f), new Vector3(1.28f, 1.0f, 0.55f), bodyMat);
            CreateBox(crt.transform, "Body Rear", new Vector3(0f, 0.78f, -0.42f), new Vector3(1.05f, 0.88f, 0.48f), bodyMat);
            CreateBox(crt.transform, "Top Cap", new Vector3(0f, 1.3f, -0.05f), new Vector3(1.2f, 0.12f, 0.85f), bodyMat);

            // Feet
            CreateBox(crt.transform, "Foot FL", new Vector3(-0.55f, 0.05f, 0.28f), new Vector3(0.14f, 0.1f, 0.14f), bodyMat);
            CreateBox(crt.transform, "Foot FR", new Vector3(0.55f, 0.05f, 0.28f), new Vector3(0.14f, 0.1f, 0.14f), bodyMat);
            CreateBox(crt.transform, "Foot BL", new Vector3(-0.45f, 0.05f, -0.35f), new Vector3(0.14f, 0.1f, 0.14f), bodyMat);
            CreateBox(crt.transform, "Foot BR", new Vector3(0.45f, 0.05f, -0.35f), new Vector3(0.14f, 0.1f, 0.14f), bodyMat);

            // Bezel + control strip
            CreateBox(crt.transform, "Bezel", new Vector3(0f, 0.78f, 0.42f), new Vector3(1.22f, 0.88f, 0.08f), bezelMat);
            CreateBox(crt.transform, "Control Strip", new Vector3(0f, 0.28f, 0.43f), new Vector3(1.2f, 0.14f, 0.06f), bezelMat);
            CreateBox(crt.transform, "Speaker Grill", new Vector3(-0.42f, 0.28f, 0.47f), new Vector3(0.28f, 0.06f, 0.02f), bodyMat);
            CreateBox(crt.transform, "Power LED", new Vector3(-0.62f, 0.28f, 0.47f), new Vector3(0.04f, 0.04f, 0.02f), ledMat);

            CreateCylinder(crt.transform, "Knob Volume", new Vector3(0.38f, 0.28f, 0.48f), new Vector3(0.08f, 0.03f, 0.08f), knobMat);
            CreateCylinder(crt.transform, "Knob Tune", new Vector3(0.55f, 0.28f, 0.48f), new Vector3(0.06f, 0.025f, 0.06f), knobMat);

            // Side vents
            for (int i = 0; i < 5; i++)
            {
                float z = -0.15f - i * 0.08f;
                CreateBox(crt.transform, "Vent R " + i, new Vector3(0.66f, 0.75f, z), new Vector3(0.02f, 0.35f, 0.03f), bezelMat);
                CreateBox(crt.transform, "Vent L " + i, new Vector3(-0.66f, 0.75f, z), new Vector3(0.02f, 0.35f, 0.03f), bezelMat);
            }

            GameObject screen = GameObject.CreatePrimitive(PrimitiveType.Quad);
            screen.name = "Screen";
            screen.transform.SetParent(crt.transform, false);
            screen.transform.localPosition = new Vector3(0f, 0.82f, 0.47f);
            screen.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            screen.transform.localScale = new Vector3(1.05f, 0.72f, 1f);
            Object.DestroyImmediate(screen.GetComponent<Collider>());
            screenRenderer = screen.GetComponent<MeshRenderer>();
            screenRenderer.sharedMaterial = screenMat;

            libretro = screen.AddComponent<LibretroInstance>();
            libretro.Renderer = screenRenderer;
            libretro.Settings = new InstanceSettings
            {
                ShaderTextureName = "_BaseMap",
                AudioDistanceControl = true,
                AudioMinDistance = 1.5f,
                AudioMaxDistance = 12f,
                LeftStickBehaviour = LeftStickBehaviour.AnalogAndDigital,
                AllowGLCoresInEditor = true
            };
            libretro.CoreName = "snes9x";
            libretro.GameNames = new[] { "ClassicKong" };
            return crt.transform;
        }

        private static void BuildSnesConsole(
            Transform parent,
            Material snesTop,
            Material snesBase,
            Material snesPurple,
            Material cartMat,
            Material labelMat,
            Material ledMat)
        {
            GameObject console = new GameObject("SNES_Console");
            console.transform.SetParent(parent, false);
            // Front of table, facing player (+Z).
            console.transform.localPosition = new Vector3(0.15f, 0.26f, 0.42f);
            console.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
            console.transform.localScale = Vector3.one * 0.85f;

            CreateBox(console.transform, "Base", new Vector3(0f, 0.05f, 0f), new Vector3(0.95f, 0.1f, 0.55f), snesBase);
            CreateBox(console.transform, "Top", new Vector3(0f, 0.14f, 0f), new Vector3(0.92f, 0.1f, 0.52f), snesTop);
            CreateBox(console.transform, "Cart Slot", new Vector3(0f, 0.2f, -0.02f), new Vector3(0.42f, 0.04f, 0.28f), snesBase);

            CreateBox(console.transform, "Power Switch", new Vector3(-0.22f, 0.2f, 0.12f), new Vector3(0.08f, 0.03f, 0.05f), snesPurple);
            CreateBox(console.transform, "Eject", new Vector3(-0.08f, 0.2f, 0.12f), new Vector3(0.06f, 0.025f, 0.04f), snesTop);
            CreateBox(console.transform, "Reset Switch", new Vector3(0.06f, 0.2f, 0.12f), new Vector3(0.08f, 0.03f, 0.05f), snesPurple);
            CreateBox(console.transform, "Power LED", new Vector3(-0.38f, 0.12f, 0.27f), new Vector3(0.03f, 0.02f, 0.02f), ledMat);

            CreateBox(console.transform, "Port 1", new Vector3(-0.18f, 0.08f, 0.28f), new Vector3(0.12f, 0.05f, 0.04f), snesBase);
            CreateBox(console.transform, "Port 2", new Vector3(0.05f, 0.08f, 0.28f), new Vector3(0.12f, 0.05f, 0.04f), snesBase);

            // Cartridge seated in slot.
            GameObject cart = new GameObject("SNES_Cartridge");
            cart.transform.SetParent(console.transform, false);
            cart.transform.localPosition = new Vector3(0f, 0.3f, -0.02f);
            cart.transform.localRotation = Quaternion.identity;

            CreateBox(cart.transform, "Shell", new Vector3(0f, 0.12f, 0f), new Vector3(0.34f, 0.28f, 0.18f), cartMat);
            CreateBox(cart.transform, "Label", new Vector3(0f, 0.16f, 0.095f), new Vector3(0.28f, 0.16f, 0.01f), labelMat);
            for (int i = 0; i < 4; i++)
            {
                float y = 0.04f + i * 0.05f;
                CreateBox(cart.transform, "Groove L " + i, new Vector3(-0.175f, y, 0f), new Vector3(0.02f, 0.02f, 0.14f), snesBase);
                CreateBox(cart.transform, "Groove R " + i, new Vector3(0.175f, y, 0f), new Vector3(0.02f, 0.02f, 0.14f), snesBase);
            }
        }

        private static void PlaceInActiveScene(GameObject prefab)
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogWarning("[SnesTelevision] No active scene to place into.");
                return;
            }

            MiniVanSnesTelevision existing = Object.FindFirstObjectByType<MiniVanSnesTelevision>();
            Vector3 pos = DefaultWorldPosition;
            Quaternion rot = Quaternion.Euler(DefaultWorldEuler);
            if (existing != null)
            {
                pos = existing.transform.position;
                rot = existing.transform.rotation;
                Undo.DestroyObjectImmediate(existing.gameObject);
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            Undo.RegisterCreatedObjectUndo(instance, "Place SnesTelevision");
            instance.transform.SetPositionAndRotation(pos, rot);
            Selection.activeGameObject = instance;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static LibretroInstanceVariable LoadInstanceVariable()
        {
            LibretroInstanceVariable instanceVariable =
                AssetDatabase.LoadAssetAtPath<LibretroInstanceVariable>(InstanceVariablePath);
            if (instanceVariable != null)
            {
                return instanceVariable;
            }

            string[] guids = AssetDatabase.FindAssets("t:LibretroInstanceVariable");
            if (guids != null && guids.Length > 0)
            {
                return AssetDatabase.LoadAssetAtPath<LibretroInstanceVariable>(
                    AssetDatabase.GUIDToAssetPath(guids[0]));
            }

            return null;
        }

        private static void CreateBox(Transform parent, string name, Vector3 localPos, Vector3 scale, Material material)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = localPos;
            box.transform.localScale = scale;
            Object.DestroyImmediate(box.GetComponent<Collider>());
            box.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static void CreateCylinder(Transform parent, string name, Vector3 localPos, Vector3 scale, Material material)
        {
            GameObject cyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cyl.name = name;
            cyl.transform.SetParent(parent, false);
            cyl.transform.localPosition = localPos;
            cyl.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            cyl.transform.localScale = scale;
            Object.DestroyImmediate(cyl.GetComponent<Collider>());
            cyl.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static Material CreateLitMaterial(string name, Color color, float metallic, float smoothness)
        {
            string path = MaterialFolder + "/" + name + ".mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            Material mat = existing != null ? existing : new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            mat.name = name;
            mat.color = color;
            if (mat.HasProperty("_BaseColor"))
            {
                mat.SetColor("_BaseColor", color);
            }

            if (mat.HasProperty("_Metallic"))
            {
                mat.SetFloat("_Metallic", metallic);
            }

            if (mat.HasProperty("_Smoothness"))
            {
                mat.SetFloat("_Smoothness", smoothness);
            }

            if (existing == null)
            {
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                EditorUtility.SetDirty(mat);
            }

            return mat;
        }

        private static Material CreateUnlitMaterial(string name, Color color)
        {
            string path = MaterialFolder + "/" + name + ".mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            Material mat = existing != null ? existing : new Material(shader);
            mat.name = name;
            if (mat.HasProperty("_BaseColor"))
            {
                mat.SetColor("_BaseColor", color);
            }

            mat.color = color;
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                EditorUtility.SetDirty(mat);
            }

            return mat;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            string[] parts = folder.Split('/');
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
}
