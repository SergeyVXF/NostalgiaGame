using Unity.Netcode;
using UnityEditor;
using UnityEngine;

namespace MiniVanGame.EditorTools
{
    public static class MiniVanVampireWeaponsBuilder
    {
        public const string PrefabFolder = "Assets/MiniVan Game/Prefabs/Weapons/Melee";
        public const string NetworkPrefabsListPath = "Assets/DefaultNetworkPrefabs.asset";
        public const string HolyCrossMaterialFolder = MiniVanHolyCrossPickup.MaterialFolder;
        public const string AspenStakeMaterialFolder = MiniVanAspenStakePickup.MaterialFolder;

        [MenuItem("MiniVan/Weapons/Build Vampire Weapons Prefabs")]
        public static void BuildPrefabs()
        {
            EnsureFolder(PrefabFolder);
            EnsureFolder(HolyCrossMaterialFolder);
            EnsureFolder(AspenStakeMaterialFolder);
            CreateMaterials();

            BuildHolyCrossPickup();
            BuildHolyCrossHeld();
            BuildHolyCrossParticles();
            BuildHolyCrossCone();
            // Never overwrite authored AspenStake visuals — only create if missing.
            if (AssetDatabase.LoadAssetAtPath<GameObject>(MiniVanAspenStakePickup.PrefabAssetPath) == null)
            {
                BuildAspenStakePickup();
            }

            if (AssetDatabase.LoadAssetAtPath<GameObject>(MiniVanAspenStakePickup.HeldPrefabAssetPath) == null)
            {
                BuildAspenStakeHeld();
            }

            RegisterNetworkPrefab(MiniVanHolyCrossPickup.PrefabAssetPath);
            RegisterNetworkPrefab(MiniVanAspenStakePickup.PrefabAssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[VampireWeapons] Prefabs built and registered (AspenStake preserved if already authored).");
        }

        [MenuItem("MiniVan/Weapons/Place Vampire Weapon Pickups In Scene")]
        public static void PlaceInScene()
        {
            EnsureFolder(PrefabFolder);
            CreateMaterials();
            RegisterNetworkPrefab(MiniVanHolyCrossPickup.PrefabAssetPath);
            RegisterNetworkPrefab(MiniVanAspenStakePickup.PrefabAssetPath);

            Vector3 basePos = new Vector3(26f, 0.9f, -14.5f);
            MiniVanVampireSpawner spawner = Object.FindFirstObjectByType<MiniVanVampireSpawner>();
            if (spawner != null)
            {
                basePos = spawner.transform.position + Vector3.right * 1.6f + Vector3.up * 0.15f;
            }

            PlaceOrReplace(
                "HolyCrossPickup",
                MiniVanHolyCrossPickup.PrefabAssetPath,
                basePos,
                Quaternion.identity);
            PlaceOrReplace(
                "AspenStakePickup",
                MiniVanAspenStakePickup.PrefabAssetPath,
                basePos + Vector3.forward * 1.2f,
                Quaternion.Euler(0f, 25f, 0f));

            WireHeldPrefabsOnPlayers();
            Debug.Log("[VampireWeapons] Pickups placed near vampire spawner.");
        }

        private static void CreateMaterials()
        {
            CreateLitMaterial(
                HolyCrossMaterialFolder,
                "HolyCross_Wood",
                new Color(0.72f, 0.58f, 0.28f, 1f),
                0.05f,
                0.35f);
            CreateParticleMaterial(
                HolyCrossMaterialFolder,
                "HolyCross_Particles",
                new Color(1f, 0.9f, 0.5f, 0.8f));
            CreateTransparentMaterial(
                HolyCrossMaterialFolder,
                "HolyCross_Cone",
                new Color(1f, 0.92f, 0.25f, 0.22f));

            CreateLitMaterial(
                AspenStakeMaterialFolder,
                "AspenStake_Wood",
                new Color(0.62f, 0.42f, 0.2f, 1f),
                0.05f,
                0.3f);
            CreateLitMaterial(
                AspenStakeMaterialFolder,
                "AspenStake_Tip",
                new Color(0.78f, 0.72f, 0.58f, 1f),
                0.1f,
                0.45f);
        }

        private static Material CreateLitMaterial(
            string folder,
            string name,
            Color color,
            float metallic,
            float smoothness)
        {
            string path = folder + "/" + name + ".mat";
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
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

        private static Material CreateParticleMaterial(string folder, string name, Color color)
        {
            string path = folder + "/" + name + ".mat";
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ??
                            Shader.Find("Particles/Standard Unlit");
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

            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material CreateTransparentMaterial(string folder, string name, Color color)
        {
            string path = folder + "/" + name + ".mat";
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ??
                            Shader.Find("Unlit/Color") ??
                            Shader.Find("Standard");
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

            if (mat.HasProperty("_Surface"))
            {
                mat.SetFloat("_Surface", 1f);
            }

            mat.SetFloat("_Mode", 3f);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = 3000;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void BuildHolyCrossPickup()
        {
            GameObject root = new GameObject("HolyCrossPickup");
            try
            {
                BoxCollider box = root.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.center = new Vector3(0f, 0.35f, 0f);
                box.size = new Vector3(0.45f, 0.85f, 0.25f);
                root.AddComponent<NetworkObject>();
                root.AddComponent<MiniVanHolyCrossPickup>();
                MiniVanHolyCrossPickup.EnsureBuiltVisual(root);
                PrefabUtility.SaveAsPrefabAsset(root, MiniVanHolyCrossPickup.PrefabAssetPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void BuildHolyCrossHeld()
        {
            GameObject root = MiniVanHolyCrossPickup.CreateHeldVisual(null);
            root.name = "HolyCrossHeld";
            try
            {
                PrefabUtility.SaveAsPrefabAsset(root, MiniVanHolyCrossPickup.HeldPrefabAssetPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void BuildHolyCrossParticles()
        {
            GameObject root = MiniVanHolyCrossPickup.CreateParticlesObject(null);
            root.name = "HolyCrossParticles";
            try
            {
                PrefabUtility.SaveAsPrefabAsset(root, MiniVanHolyCrossPickup.ParticlesPrefabAssetPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void BuildHolyCrossCone()
        {
            GameObject root = MiniVanHolyCrossPickup.CreateConeVisual(null, 12f, 35f);
            root.name = "HolyCrossCone";
            try
            {
                // Persist mesh as a sub-asset so the prefab does not lose it (fileID 0).
                MeshFilter filter = root.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null)
                {
                    string meshPath = PrefabFolder + "/HolyCrossConeMesh.asset";
                    Mesh meshCopy = Object.Instantiate(filter.sharedMesh);
                    meshCopy.name = "HolyCrossConeMesh";
                    AssetDatabase.DeleteAsset(meshPath);
                    AssetDatabase.CreateAsset(meshCopy, meshPath);
                    filter.sharedMesh = meshCopy;
                }

                PrefabUtility.SaveAsPrefabAsset(root, MiniVanHolyCrossPickup.ConePrefabAssetPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void BuildAspenStakePickup()
        {
            GameObject root = new GameObject("AspenStakePickup");
            try
            {
                BoxCollider box = root.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.center = new Vector3(0f, 0.1f, 0.4f);
                box.size = new Vector3(0.25f, 0.25f, 0.95f);
                root.AddComponent<NetworkObject>();
                root.AddComponent<MiniVanAspenStakePickup>();
                MiniVanAspenStakePickup.EnsureBuiltVisual(root);
                PrefabUtility.SaveAsPrefabAsset(root, MiniVanAspenStakePickup.PrefabAssetPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void BuildAspenStakeHeld()
        {
            // Force a fresh stake mesh (delete any leftover bat-like children first).
            GameObject root = MiniVanAspenStakePickup.CreateHeldVisual(null);
            root.name = "AspenStakeHeld";
            try
            {
                PrefabUtility.SaveAsPrefabAsset(root, MiniVanAspenStakePickup.HeldPrefabAssetPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void PlaceOrReplace(string name, string prefabPath, Vector3 position, Quaternion rotation)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogError("[VampireWeapons] Missing prefab: " + prefabPath);
                return;
            }

            GameObject existing = GameObject.Find(name);
            if (existing != null)
            {
                Object.DestroyImmediate(existing);
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = name;
            instance.transform.position = position;
            instance.transform.rotation = rotation;
            Undo.RegisterCreatedObjectUndo(instance, "Place " + name);
            EditorUtility.SetDirty(instance);
        }

        private static void WireHeldPrefabsOnPlayers()
        {
            GameObject crossHeld = AssetDatabase.LoadAssetAtPath<GameObject>(MiniVanHolyCrossPickup.HeldPrefabAssetPath);
            GameObject crossFx = AssetDatabase.LoadAssetAtPath<GameObject>(MiniVanHolyCrossPickup.ParticlesPrefabAssetPath);
            GameObject crossCone = AssetDatabase.LoadAssetAtPath<GameObject>(MiniVanHolyCrossPickup.ConePrefabAssetPath);
            GameObject stakeHeld = AssetDatabase.LoadAssetAtPath<GameObject>(MiniVanAspenStakePickup.HeldPrefabAssetPath);

            MiniVanPlayer[] players = Object.FindObjectsByType<MiniVanPlayer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < players.Length; i++)
            {
                MiniVanPlayer player = players[i];
                if (player == null)
                {
                    continue;
                }

                Undo.RecordObject(player, "Wire vampire weapon held prefabs");
                player.HolyCrossHeldPrefab = crossHeld;
                player.HolyCrossParticlesPrefab = crossFx;
                player.HolyCrossConePrefab = crossCone;
                player.ShowHolyCrossConeDebug = false;
                player.AspenStakeHeldPrefab = stakeHeld;
                EditorUtility.SetDirty(player);
            }

            // Also wire player prefab if present in common locations.
            string[] playerPrefabPaths =
            {
                "Assets/MiniVan Game/Prefabs/Characters/Players/MiniVanPlayer.prefab"
            };
            for (int i = 0; i < playerPrefabPaths.Length; i++)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(playerPrefabPaths[i]);
                if (prefab == null)
                {
                    continue;
                }

                MiniVanPlayer player = prefab.GetComponent<MiniVanPlayer>();
                if (player == null)
                {
                    continue;
                }

                player.HolyCrossHeldPrefab = crossHeld;
                player.HolyCrossParticlesPrefab = crossFx;
                player.HolyCrossConePrefab = crossCone;
                player.ShowHolyCrossConeDebug = false;
                player.AspenStakeHeldPrefab = stakeHeld;
                EditorUtility.SetDirty(player);
                PrefabUtility.SavePrefabAsset(prefab);
            }
        }

        private static void RegisterNetworkPrefab(string prefabPath)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                return;
            }

            NetworkPrefabsList list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(NetworkPrefabsListPath);
            if (list == null)
            {
                Debug.LogWarning("[VampireWeapons] DefaultNetworkPrefabs.asset not found.");
                return;
            }

            SerializedObject so = new SerializedObject(list);
            SerializedProperty arr = so.FindProperty("List") ?? so.FindProperty("m_Prefabs");
            if (arr == null || !arr.isArray)
            {
                Debug.LogWarning("[VampireWeapons] Could not find prefab list property.");
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
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

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
}
