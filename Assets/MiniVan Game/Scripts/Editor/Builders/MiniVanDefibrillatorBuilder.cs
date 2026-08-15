using Unity.Netcode;
using UnityEditor;
using UnityEngine;

namespace MiniVanGame.EditorTools
{
    public static class MiniVanDefibrillatorBuilder
    {
        public const string PrefabFolder = "Assets/MiniVan Game/Prefabs/Equipment";
        public const string PrefabPath = PrefabFolder + "/DefibrillatorPickup.prefab";
        public const string ResourcesFolder = "Assets/MiniVan Game/Resources/Defibrillator";
        public const string SuitcaseFbx = "Assets/MiniVan Game/Art/Defibrillator/MiniVan_Defib_Suitcase.fbx";
        public const string TubeFbx = "Assets/MiniVan Game/Art/Defibrillator/MiniVan_Defib_Tube.fbx";

        [MenuItem("MiniVan/Equipment/Build Defibrillator Prefab")]
        public static void BuildPrefab()
        {
            EnsureFolders();
            CopyMeshToResources(SuitcaseFbx, ResourcesFolder + "/MiniVan_Defib_Suitcase.fbx");
            CopyMeshToResources(TubeFbx, ResourcesFolder + "/MiniVan_Defib_Tube.fbx");
            AssetDatabase.Refresh();

            GameObject root = new GameObject("DefibrillatorPickup");
            try
            {
                BoxCollider box = root.AddComponent<BoxCollider>();
                box.size = new Vector3(0.55f, 0.28f, 0.42f);
                box.center = new Vector3(0f, 0.12f, 0f);
                box.isTrigger = true;

                NetworkObject net = root.AddComponent<NetworkObject>();
                root.AddComponent<MiniVanDefibrillatorPickup>();
                MiniVanDefibrillatorPickup.EnsureSuitcaseVisual(root);

                if (!AssetDatabase.IsValidFolder(PrefabFolder))
                {
                    AssetDatabase.CreateFolder("Assets/MiniVan Game/Prefabs", "Equipment");
                }

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                CopyPrefabToResources(PrefabPath, ResourcesFolder + "/DefibrillatorPickup.prefab");
                RegisterNetworkPrefab(PrefabPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("[Defibrillator] Prefab ready: " + PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [MenuItem("MiniVan/Equipment/Place Defibrillator In Scene")]
        public static void PlaceInScene()
        {
            BuildPrefab();
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                Debug.LogError("[Defibrillator] Prefab missing after build.");
                return;
            }

            GameObject existing = GameObject.Find("DefibrillatorPickup");
            if (existing != null)
            {
                Object.DestroyImmediate(existing);
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = "DefibrillatorPickup";
            MiniVanPlayer player = Object.FindFirstObjectByType<MiniVanPlayer>();
            if (player != null)
            {
                instance.transform.position = player.transform.position + player.transform.forward * 2f + Vector3.up * 0.15f;
            }
            else
            {
                instance.transform.position = new Vector3(0f, 0.15f, 2f);
            }

            Undo.RegisterCreatedObjectUndo(instance, "Place Defibrillator");
            Selection.activeGameObject = instance;
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/MiniVan Game/Resources"))
            {
                AssetDatabase.CreateFolder("Assets/MiniVan Game", "Resources");
            }

            if (!AssetDatabase.IsValidFolder(ResourcesFolder))
            {
                AssetDatabase.CreateFolder("Assets/MiniVan Game/Resources", "Defibrillator");
            }

            if (!AssetDatabase.IsValidFolder("Assets/MiniVan Game/Prefabs/Equipment"))
            {
                if (!AssetDatabase.IsValidFolder("Assets/MiniVan Game/Prefabs"))
                {
                    AssetDatabase.CreateFolder("Assets/MiniVan Game", "Prefabs");
                }

                AssetDatabase.CreateFolder("Assets/MiniVan Game/Prefabs", "Equipment");
            }
        }

        private static void CopyMeshToResources(string sourcePath, string destPath)
        {
            if (AssetDatabase.LoadAssetAtPath<Object>(sourcePath) == null)
            {
                Debug.LogWarning("[Defibrillator] Missing mesh: " + sourcePath);
                return;
            }

            AssetDatabase.DeleteAsset(destPath);
            AssetDatabase.CopyAsset(sourcePath, destPath);
        }

        private static void CopyPrefabToResources(string sourcePath, string destPath)
        {
            AssetDatabase.DeleteAsset(destPath);
            AssetDatabase.CopyAsset(sourcePath, destPath);
        }

        private static void RegisterNetworkPrefab(string prefabPath)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                return;
            }

            NetworkPrefabsList list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>("Assets/DefaultNetworkPrefabs.asset");
            if (list == null)
            {
                Debug.LogWarning("[Defibrillator] DefaultNetworkPrefabs.asset not found.");
                return;
            }

            SerializedObject so = new SerializedObject(list);
            SerializedProperty arr = so.FindProperty("m_Prefabs");
            if (arr == null || !arr.isArray)
            {
                return;
            }

            for (int i = 0; i < arr.arraySize; i++)
            {
                SerializedProperty el = arr.GetArrayElementAtIndex(i);
                SerializedProperty prefabProp = el.FindPropertyRelative("Prefab") ??
                                               el.FindPropertyRelative("m_Prefab") ??
                                               el.FindPropertyRelative("prefab");
                if (prefabProp != null && prefabProp.objectReferenceValue == prefab)
                {
                    return;
                }
            }

            arr.arraySize++;
            SerializedProperty newEl = arr.GetArrayElementAtIndex(arr.arraySize - 1);
            SerializedProperty newPrefab = newEl.FindPropertyRelative("Prefab") ??
                                           newEl.FindPropertyRelative("m_Prefab") ??
                                           newEl.FindPropertyRelative("prefab");
            if (newPrefab != null)
            {
                newPrefab.objectReferenceValue = prefab;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(list);
            Debug.Log("[Defibrillator] Registered network prefab in DefaultNetworkPrefabs.asset");
        }
    }
}
