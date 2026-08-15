using System.Collections.Generic;
using SK.Libretro.Unity;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MiniVanGame.EditorTools
{
    /// <summary>
    /// One-shot wiring for split SNES TV / Console / Cartridge prefabs + scene instances.
    /// </summary>
    public static class MiniVanSnesSplitPrefabWirer
    {
        public const string TvPath = "Assets/MiniVan Game/Prefabs/Props/Snes/SnesTelevision.prefab";
        public const string ConsolePath = "Assets/MiniVan Game/Prefabs/Props/Snes/SnesConsole.prefab";
        public const string CartPath = "Assets/MiniVan Game/Prefabs/Props/Snes/SnesCartridge.prefab";
        public const string InstanceVariablePath =
            "Packages/com.sk.libretro/Unity/ScriptableObjects/LibretroInstanceVariable.asset";

        [MenuItem("MiniVan Game/SNES TV/Wire Split Prefabs And Scene")]
        public static void WireAll()
        {
            try
            {
                WirePrefab(TvPath, WireTelevision);
                WirePrefab(ConsolePath, WireConsole);
                WirePrefab(CartPath, WireCartridge);

                ApplyToScene();
                RegisterNetworkPrefabs();

                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                AssetDatabase.SaveAssets();
                Debug.Log("[SnesSplit] Prefabs + scene wired.");
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[SnesSplit] WireAll failed: " + ex);
            }
        }

        private static void WirePrefab(string path, System.Action<GameObject> wire)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                wire(root);
                bool ok = PrefabUtility.SaveAsPrefabAsset(root, path);
                if (!ok)
                {
                    Debug.LogError("[SnesSplit] SaveAsPrefabAsset failed: " + path);
                }
                else
                {
                    int count = root.GetComponents<Component>().Length;
                    Debug.Log("[SnesSplit] Saved " + path + " with " + count + " root components.");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[SnesSplit] WirePrefab failed for " + path + ": " + ex);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void WireTelevision(GameObject root)
        {
            Rigidbody rb = EnsureRigidbody(root);
            EnsureNetworkObject(root);

            MiniVanSnesTelevision tv = root.GetComponent<MiniVanSnesTelevision>();
            if (tv == null)
            {
                tv = Undo.AddComponent<MiniVanSnesTelevision>(root);
            }

            if (root.GetComponent<MiniVanSnesOutline>() == null)
            {
                Undo.AddComponent<MiniVanSnesOutline>(root);
            }

            Transform screen = FindDeep(root.transform, "Screen");
            LibretroInstance lib = root.GetComponentInChildren<LibretroInstance>(true);
            if (lib == null && screen != null)
            {
                lib = screen.gameObject.AddComponent<LibretroInstance>();
            }
            else if (lib == null)
            {
                lib = root.AddComponent<LibretroInstance>();
            }

            Renderer screenRenderer = screen != null ? screen.GetComponent<Renderer>() : null;
            if (screenRenderer != null)
            {
                lib.Renderer = screenRenderer;
            }

            tv.Libretro = lib;
            tv.InstanceVariable = AssetDatabase.LoadAssetAtPath<LibretroInstanceVariable>(InstanceVariablePath);
            tv.ScreenRenderer = screenRenderer;
            tv.LookAnchor = screen != null ? screen : root.transform;

            if (root.GetComponentsInChildren<Collider>(true).Length == 0)
            {
                BoxCollider box = root.AddComponent<BoxCollider>();
                box.size = new Vector3(0.7f, 0.7f, 0.55f);
                box.center = new Vector3(0f, 0.35f, 0f);
            }
        }

        private static void WireConsole(GameObject root)
        {
            EnsureRigidbody(root);
            EnsureNetworkObject(root);

            MiniVanSnesConsole console = root.GetComponent<MiniVanSnesConsole>();
            if (console == null)
            {
                console = Undo.AddComponent<MiniVanSnesConsole>(root);
            }

            if (root.GetComponent<MiniVanSnesOutline>() == null)
            {
                Undo.AddComponent<MiniVanSnesOutline>(root);
            }

            Transform power = FindDeep(root.transform, "ON/OFF") ?? FindDeep(root.transform, "Power Switch");
            if (power == null)
            {
                GameObject go = new GameObject("ON/OFF");
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = new Vector3(0.12f, 0.08f, -0.18f);
                BoxCollider col = go.AddComponent<BoxCollider>();
                col.size = new Vector3(0.04f, 0.03f, 0.04f);
                power = go.transform;
            }
            else
            {
                power.name = "ON/OFF";
                if (power.GetComponent<Collider>() == null)
                {
                    BoxCollider col = power.gameObject.AddComponent<BoxCollider>();
                    col.size = new Vector3(0.04f, 0.03f, 0.04f);
                }
            }

            Transform slot = FindDeep(root.transform, "Cart Slot");
            if (slot == null)
            {
                GameObject go = new GameObject("Cart Slot");
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = new Vector3(0f, 0.12f, -0.02f);
                slot = go.transform;
            }

            console.OnOffButton = power;
            console.CartridgeSlot = slot;

            if (root.GetComponentsInChildren<Collider>(true).Length == 0)
            {
                root.AddComponent<BoxCollider>().size = new Vector3(0.35f, 0.12f, 0.25f);
            }
        }

        private static void WireCartridge(GameObject root)
        {
            EnsureRigidbody(root);
            EnsureNetworkObject(root);

            MiniVanSnesCartridge cart = root.GetComponent<MiniVanSnesCartridge>();
            if (cart == null)
            {
                cart = Undo.AddComponent<MiniVanSnesCartridge>(root);
            }

            if (root.GetComponent<MiniVanSnesOutline>() == null)
            {
                Undo.AddComponent<MiniVanSnesOutline>(root);
            }

            cart.GameName = MiniVanSnesCartridge.DefaultGameName;
            cart.RomFileName = MiniVanSnesCartridge.DefaultRomFileName;
            cart.GamesSubdirectory = "snes";
            cart.SourceRomProjectRelativePath =
                "Assets/MiniVan Game/Resources/Rooms/Ultimate Mortal Kombat 3 (Europe).sfc";
            cart.VisualRoot = root.transform;
            cart.Colliders = root.GetComponentsInChildren<Collider>(true);
            if (cart.Colliders == null || cart.Colliders.Length == 0)
            {
                BoxCollider box = root.AddComponent<BoxCollider>();
                box.size = new Vector3(0.12f, 0.03f, 0.16f);
                cart.Colliders = new[] { box };
            }
        }

        private static void ApplyToScene()
        {
            GameObject[] roots = SceneManager.GetActiveScene().GetRootGameObjects();
            for (int r = 0; r < roots.Length; r++)
            {
                Transform[] transforms = roots[r].GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < transforms.Length; i++)
                {
                    Transform t = transforms[i];
                    string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject);
                    if (!string.IsNullOrEmpty(path) && path.Contains("/Snes/") &&
                        PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject))
                    {
                        PrefabUtility.RevertPrefabInstance(t.gameObject, InteractionMode.AutomatedAction);
                        continue;
                    }

                    string name = t.name;
                    if (name.IndexOf("SnesTelevision", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        WireTelevision(t.gameObject);
                        EditorUtility.SetDirty(t.gameObject);
                    }
                    else if (name.IndexOf("SnesConsole", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        WireConsole(t.gameObject);
                        EditorUtility.SetDirty(t.gameObject);
                    }
                    else if (name.IndexOf("SnesCartridge", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        WireCartridge(t.gameObject);
                        EditorUtility.SetDirty(t.gameObject);
                    }
                }
            }
        }

        private static void RegisterNetworkPrefabs()
        {
            GameObject tv = AssetDatabase.LoadAssetAtPath<GameObject>(TvPath);
            GameObject console = AssetDatabase.LoadAssetAtPath<GameObject>(ConsolePath);
            GameObject cart = AssetDatabase.LoadAssetAtPath<GameObject>(CartPath);

            NetworkPrefabsList list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>("Assets/DefaultNetworkPrefabs.asset");
            if (list != null)
            {
                TryAddNetworkPrefab(list, tv);
                TryAddNetworkPrefab(list, console);
                TryAddNetworkPrefab(list, cart);
                EditorUtility.SetDirty(list);
            }

            MiniVanNetworkBootstrap[] bootstraps =
                Object.FindObjectsByType<MiniVanNetworkBootstrap>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < bootstraps.Length; i++)
            {
                MiniVanNetworkBootstrap bootstrap = bootstraps[i];
                HashSet<GameObject> set = new HashSet<GameObject>();
                if (bootstrap.ExtraNetworkPrefabs != null)
                {
                    for (int p = 0; p < bootstrap.ExtraNetworkPrefabs.Length; p++)
                    {
                        if (bootstrap.ExtraNetworkPrefabs[p] != null)
                        {
                            set.Add(bootstrap.ExtraNetworkPrefabs[p]);
                        }
                    }
                }

                if (tv != null) set.Add(tv);
                if (console != null) set.Add(console);
                if (cart != null) set.Add(cart);

                Undo.RecordObject(bootstrap, "Register SNES network prefabs");
                bootstrap.ExtraNetworkPrefabs = new List<GameObject>(set).ToArray();
                EditorUtility.SetDirty(bootstrap);
            }
        }

        private static void TryAddNetworkPrefab(NetworkPrefabsList list, GameObject prefab)
        {
            if (list == null || prefab == null)
            {
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
            else
            {
                SerializedProperty iterator = newEl.Copy();
                SerializedProperty end = newEl.GetEndProperty();
                bool enter = true;
                while (iterator.NextVisible(enter) && !SerializedProperty.EqualContents(iterator, end))
                {
                    enter = false;
                    if (iterator.propertyType == SerializedPropertyType.ObjectReference &&
                        iterator.name.ToLowerInvariant().Contains("prefab"))
                    {
                        iterator.objectReferenceValue = prefab;
                        break;
                    }
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Rigidbody EnsureRigidbody(GameObject root)
        {
            Rigidbody rb = root.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = Undo.AddComponent<Rigidbody>(root);
            }

            rb.isKinematic = true;
            rb.useGravity = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
            return rb;
        }

        private static void EnsureNetworkObject(GameObject root)
        {
            if (root.GetComponent<NetworkObject>() == null)
            {
                Undo.AddComponent<NetworkObject>(root);
            }
        }

        private static Transform FindDeep(Transform root, string name)
        {
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].name == name)
                {
                    return all[i];
                }
            }

            return null;
        }
    }
}
