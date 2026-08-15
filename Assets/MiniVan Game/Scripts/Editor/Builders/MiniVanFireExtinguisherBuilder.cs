using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MiniVanGame.EditorTools
{
    /// <summary>
    /// Builds materials + networked fire-extinguisher pickup prefab and places it next to the minivan.
    /// </summary>
    public static class MiniVanFireExtinguisherBuilder
    {
        public const string PrefabFolder = "Assets/MiniVan Game/Prefabs/Equipment";
        public const string PrefabPath = PrefabFolder + "/FireExtinguisherPickup.prefab";
        public const string MaterialFolder = "Assets/MiniVan Game/Materials/Equipment/FireExtinguisher";
        public const string ResourcesFolder = "Assets/MiniVan Game/Resources/FireExtinguisher";

        [MenuItem("MiniVan Game/Fire Extinguisher/Build Prefab And Place Near MiniVan")]
        public static void BuildAndPlace()
        {
            EnsureFolder(PrefabFolder);
            EnsureFolder(MaterialFolder);
            EnsureFolder(ResourcesFolder);

            CreateMaterials();
            GameObject prefab = BuildPrefab();
            MiniVanEquipmentUiBuilder.RegisterNetworkPrefab(prefab);
            PlaceNearMiniVan(prefab);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorGUIUtility.PingObject(prefab);
            Debug.Log("[FireExtinguisher] Prefab ready: " + PrefabPath);
        }

        [MenuItem("MiniVan Game/Fire Extinguisher/Rebuild Materials And Apply To Prefab")]
        public static void RebuildMaterialsOnly()
        {
            EnsureFolder(MaterialFolder);
            EnsureFolder(ResourcesFolder);
            CreateMaterials();

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab != null)
            {
                GameObject contents = PrefabUtility.LoadPrefabContents(PrefabPath);
                MiniVanFireExtinguisherPickup.ApplySharedMaterials(contents);
                PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
                PrefabUtility.UnloadPrefabContents(contents);
                EditorUtility.SetDirty(prefab);
            }

            GameObject scenePickup = GameObject.Find("FireExtinguisherPickup");
            if (scenePickup != null)
            {
                MiniVanFireExtinguisherPickup.ApplySharedMaterials(scenePickup);
                EditorUtility.SetDirty(scenePickup);
                EditorSceneManager.MarkSceneDirty(scenePickup.scene);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[FireExtinguisher] Materials rebuilt and applied.");
        }

        private static void CreateMaterials()
        {
            CreateLitMaterial("FireExtinguisher_Red", new Color(0.78f, 0.07f, 0.05f, 1f), 0.15f, 0.45f);
            CreateLitMaterial("FireExtinguisher_Black", new Color(0.07f, 0.07f, 0.08f, 1f), 0.35f, 0.55f);
            CreateLitMaterial("FireExtinguisher_Silver", new Color(0.72f, 0.73f, 0.76f, 1f), 0.75f, 0.65f);
        }

        private static Material CreateLitMaterial(string name, Color color, float metallic, float smoothness)
        {
            string artPath = MaterialFolder + "/" + name + ".mat";
            string resPath = ResourcesFolder + "/" + name + ".mat";

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            Material mat = AssetDatabase.LoadAssetAtPath<Material>(artPath);
            if (mat == null)
            {
                mat = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(mat, artPath);
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

            // Keep a Resources copy so runtime held-visual can load the same materials.
            Material resMat = AssetDatabase.LoadAssetAtPath<Material>(resPath);
            if (resMat == null)
            {
                AssetDatabase.CopyAsset(artPath, resPath);
                resMat = AssetDatabase.LoadAssetAtPath<Material>(resPath);
            }
            else
            {
                EditorUtility.CopySerialized(mat, resMat);
                EditorUtility.SetDirty(resMat);
            }

            return mat;
        }

        private static GameObject BuildPrefab()
        {
            GameObject root = new GameObject("FireExtinguisherPickup");
            root.transform.position = Vector3.zero;
            root.transform.rotation = Quaternion.identity;

            BoxCollider box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.4f, 0f);
            box.size = new Vector3(0.35f, 0.9f, 0.35f);
            box.isTrigger = true;

            NetworkObject net = root.AddComponent<NetworkObject>();
            net.SynchronizeTransform = true;

            MiniVanFireExtinguisherPickup pickup = root.AddComponent<MiniVanFireExtinguisherPickup>();
            pickup.PickupRadius = 2.4f;

            MiniVanFireExtinguisherPickup.EnsureBuiltVisual(root);
            MiniVanFireExtinguisherPickup.ApplySharedMaterials(root);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void PlaceNearMiniVan(GameObject prefab)
        {
            if (prefab == null)
            {
                return;
            }

            MiniVanVehicle vehicle = Object.FindFirstObjectByType<MiniVanVehicle>();
            Vector3 position;
            Quaternion rotation;
            if (vehicle != null)
            {
                position = vehicle.transform.TransformPoint(new Vector3(1.55f, 0f, 0.35f));
                rotation = vehicle.transform.rotation * Quaternion.Euler(0f, -25f, 0f);
            }
            else
            {
                position = Vector3.zero;
                rotation = Quaternion.identity;
                Debug.LogWarning("[FireExtinguisher] MiniVan not found in scene; placed at origin.");
            }

            if (Physics.Raycast(position + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 6f))
            {
                position = hit.point;
            }

            GameObject existing = GameObject.Find("FireExtinguisherPickup");
            if (existing != null)
            {
                Object.DestroyImmediate(existing);
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = "FireExtinguisherPickup";
            instance.transform.SetPositionAndRotation(position, rotation);
            EditorSceneManager.MarkSceneDirty(instance.scene);
            Selection.activeGameObject = instance;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            string parent = System.IO.Path.GetDirectoryName(folder).Replace('\\', '/');
            string leaf = System.IO.Path.GetFileName(folder);
            if (!AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
