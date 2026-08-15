using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MiniVanGame.EditorTools
{
    /// <summary>
    /// Builds the roof BoardCharger prefab (4 slots + status lights) and nests it on MiniVan.
    /// </summary>
    public static class MiniVanBoardChargerBuilder
    {
        public const string PrefabFolder = "Assets/MiniVan Game/Prefabs/Vehicles/MiniVan";
        public const string PrefabPath = PrefabFolder + "/BoardCharger.prefab";
        public const string MaterialFolder = "Assets/MiniVan Game/Materials/Vehicles/BoardCharger";
        public const string MiniVanPrefabPath = PrefabFolder + "/MiniVan.prefab";

        // Rear-roof placement on MiniVan local space (near Roof (1)).
        private static readonly Vector3 MiniVanLocalPosition = new Vector3(-0.15f, 3.42f, -3.35f);
        private static readonly Vector3 MiniVanLocalEuler = new Vector3(0f, 0f, 0f);

        [MenuItem("MiniVan Game/Board Charger/Build Prefab And Place On MiniVan")]
        public static void BuildAndPlace()
        {
            EnsureFolder(PrefabFolder);
            EnsureFolder(MaterialFolder);

            Material frameMat = CreateLitMaterial("BoardCharger_Frame", new Color(0.18f, 0.19f, 0.21f, 1f), 0.45f, 0.35f);
            Material railMat = CreateLitMaterial("BoardCharger_Rail", new Color(0.28f, 0.29f, 0.31f, 1f), 0.55f, 0.4f);
            Material boxMat = CreateLitMaterial("BoardCharger_Box", new Color(0.22f, 0.23f, 0.25f, 1f), 0.4f, 0.3f);
            Material greenMat = CreateUnlitMaterial("BoardCharger_LightGreen", new Color(0.15f, 0.95f, 0.28f, 1f));
            Material redMat = CreateUnlitMaterial("BoardCharger_LightRed", new Color(0.95f, 0.14f, 0.12f, 1f));

            GameObject prefabRoot = BuildChargerRoot(frameMat, railMat, boxMat, greenMat, redMat);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
            Object.DestroyImmediate(prefabRoot);

            PlaceOnMiniVanPrefab(prefab);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorGUIUtility.PingObject(prefab);
            Debug.Log("[BoardCharger] Prefab ready: " + PrefabPath);
        }

        private static GameObject BuildChargerRoot(
            Material frameMat,
            Material railMat,
            Material boxMat,
            Material greenMat,
            Material redMat)
        {
            GameObject root = new GameObject("BoardCharger");
            MiniVanBoardCharger charger = root.AddComponent<MiniVanBoardCharger>();
            charger.ChargeSecondsToFull = 900f;
            charger.InteractRadius = 2.6f;
            charger.DockedBoardLocalOffset = new Vector3(0f, -0.12f, 0f);
            charger.LightChargedMaterial = greenMat;
            charger.LightChargingMaterial = redMat;
            charger.SlotAnchors = new Transform[MiniVanBoardCharger.SlotCount];
            charger.SlotLights = new Renderer[MiniVanBoardCharger.SlotCount];

            // Frame: wide across van (X), deep along length (Z). Slots stacked fore-aft.
            CreateBox(root.transform, "Frame Base", new Vector3(0f, 0.04f, 0f), new Vector3(2.35f, 0.08f, 2.05f), frameMat);

            CreateBox(root.transform, "Side Rail L", new Vector3(-1.12f, 0.14f, 0f), new Vector3(0.08f, 0.12f, 2.0f), railMat);
            CreateBox(root.transform, "Side Rail R", new Vector3(1.12f, 0.14f, 0f), new Vector3(0.08f, 0.12f, 2.0f), railMat);
            CreateBox(root.transform, "Front Rail", new Vector3(0f, 0.14f, 0.96f), new Vector3(2.3f, 0.12f, 0.08f), railMat);
            CreateBox(root.transform, "Rear Rail", new Vector3(0f, 0.14f, -0.96f), new Vector3(2.3f, 0.12f, 0.08f), railMat);

            // Junction box + conduits on +X side.
            CreateBox(root.transform, "Junction Box", new Vector3(1.38f, 0.22f, 0.05f), new Vector3(0.3f, 0.34f, 0.7f), boxMat);
            CreateBox(root.transform, "Conduit A", new Vector3(1.2f, 0.18f, 0.45f), new Vector3(0.22f, 0.08f, 0.08f), railMat);
            CreateBox(root.transform, "Conduit B", new Vector3(1.2f, 0.18f, 0.05f), new Vector3(0.22f, 0.08f, 0.08f), railMat);
            CreateBox(root.transform, "Conduit C", new Vector3(1.2f, 0.18f, -0.35f), new Vector3(0.22f, 0.08f, 0.08f), railMat);

            // Slot 0 = front-most, slot 3 = rear-most (matches concept rear occupancy).
            float slotStartZ = 0.72f;
            float slotSpacing = -0.48f;
            for (int i = 0; i < MiniVanBoardCharger.SlotCount; i++)
            {
                float z = slotStartZ + i * slotSpacing;
                CreateBox(
                    root.transform,
                    "Slot Divider " + i,
                    new Vector3(0f, 0.16f, z + 0.22f),
                    new Vector3(2.15f, 0.16f, 0.05f),
                    railMat);

                CreateBox(
                    root.transform,
                    "Slot Cradle " + i,
                    new Vector3(0f, 0.1f, z),
                    new Vector3(2.0f, 0.05f, 0.36f),
                    frameMat);

                GameObject anchorObject = new GameObject("Slot Anchor " + i);
                anchorObject.transform.SetParent(root.transform, false);
                // Boards lie across van width; Y keeps deck in the cradle.
                anchorObject.transform.localPosition = new Vector3(0f, 0.14f, z);
                anchorObject.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                charger.SlotAnchors[i] = anchorObject.transform;

                GameObject light = GameObject.CreatePrimitive(PrimitiveType.Cube);
                light.name = "Slot Light " + i;
                light.transform.SetParent(root.transform, false);
                light.transform.localPosition = new Vector3(-1.0f, 0.2f, z);
                light.transform.localScale = new Vector3(0.1f, 0.08f, 0.1f);
                Object.DestroyImmediate(light.GetComponent<Collider>());
                Renderer lightRenderer = light.GetComponent<Renderer>();
                lightRenderer.sharedMaterial = redMat;
                charger.SlotLights[i] = lightRenderer;
            }

            CreateBox(
                root.transform,
                "Slot Divider End",
                new Vector3(0f, 0.16f, slotStartZ + 4f * slotSpacing + 0.22f),
                new Vector3(2.15f, 0.16f, 0.05f),
                railMat);

            BoxCollider trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 0.25f, 0f);
            trigger.size = new Vector3(2.6f, 0.7f, 2.3f);

            return root;
        }

        private static void PlaceOnMiniVanPrefab(GameObject chargerPrefab)
        {
            if (chargerPrefab == null)
            {
                Debug.LogError("[BoardCharger] Prefab missing.");
                return;
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(MiniVanPrefabPath);
            try
            {
                Transform existing = contents.transform.Find("BoardCharger");
                if (existing == null)
                {
                    foreach (Transform child in contents.GetComponentsInChildren<Transform>(true))
                    {
                        if (child != null && child.name == "BoardCharger" && child.parent == contents.transform)
                        {
                            existing = child;
                            break;
                        }
                    }
                }

                if (existing != null)
                {
                    Object.DestroyImmediate(existing.gameObject);
                }

                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(chargerPrefab, contents.transform);
                instance.name = "BoardCharger";
                instance.transform.localPosition = MiniVanLocalPosition;
                instance.transform.localRotation = Quaternion.Euler(MiniVanLocalEuler);
                instance.transform.localScale = Vector3.one;

                PrefabUtility.SaveAsPrefabAsset(contents, MiniVanPrefabPath);
                Debug.Log("[BoardCharger] Nested under MiniVan prefab at local " + MiniVanLocalPosition);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            // Refresh open scene instances if any.
            MiniVanBoardCharger[] sceneChargers = Object.FindObjectsByType<MiniVanBoardCharger>(FindObjectsSortMode.None);
            for (int i = 0; i < sceneChargers.Length; i++)
            {
                if (sceneChargers[i] != null)
                {
                    EditorSceneManager.MarkSceneDirty(sceneChargers[i].gameObject.scene);
                }
            }
        }

        private static GameObject CreateBox(Transform parent, string name, Vector3 localPos, Vector3 scale, Material material)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = localPos;
            box.transform.localRotation = Quaternion.identity;
            box.transform.localScale = scale;
            Object.DestroyImmediate(box.GetComponent<Collider>());
            Renderer renderer = box.GetComponent<Renderer>();
            if (renderer != null && material != null)
            {
                renderer.sharedMaterial = material;
            }

            return box;
        }

        private static Material CreateLitMaterial(string name, Color color, float metallic, float smoothness)
        {
            string path = MaterialFolder + "/" + name + ".mat";
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.shader = shader;
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

            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material CreateUnlitMaterial(string name, Color color)
        {
            string path = MaterialFolder + "/" + name + ".mat";
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.shader = shader;
            mat.color = color;
            if (mat.HasProperty("_BaseColor"))
            {
                mat.SetColor("_BaseColor", color);
            }

            if (mat.HasProperty("_Color"))
            {
                mat.SetColor("_Color", color);
            }

            EditorUtility.SetDirty(mat);
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
