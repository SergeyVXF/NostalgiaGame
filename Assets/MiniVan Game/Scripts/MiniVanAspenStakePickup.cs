using Unity.Netcode;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MiniVanGame
{
    /// <summary>
    /// World pickup for the aspen stake (2 durable hits, strong vs vampires).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class MiniVanAspenStakePickup : NetworkBehaviour, IMiniVanGameModeInteractable
    {
        public const string PrefabAssetPath = "Assets/MiniVan Game/Prefabs/Weapons/Melee/AspenStakePickup.prefab";
        public const string HeldPrefabAssetPath = "Assets/MiniVan Game/Prefabs/Weapons/Melee/AspenStakeHeld.prefab";
        public const string MaterialFolder = "Assets/MiniVan Game/Materials/Weapons/AspenStake";
        public const int DefaultHits = 2;

        public float PickupRadius = 2.4f;

        public string GetPrompt(MiniVanPlayer player)
        {
            if (player == null || !IsAvailable || !IsInReach(player.transform.position))
            {
                return string.Empty;
            }

            if (player.HasInventoryItemPublic(MiniVanInventoryItem.AspenStake))
            {
                return "Already have aspen stake";
            }

            return "E - take aspen stake";
        }

        public void Interact(MiniVanPlayer player)
        {
            if (player == null || Input.GetMouseButton(1) || !IsAvailable)
            {
                return;
            }

            player.RequestTakeAspenStake(this);
        }

        public void PrimaryAction(MiniVanPlayer player)
        {
        }

        public bool IsAvailable => !IsSpawned || gameObject.activeInHierarchy;

        public bool IsInReach(Vector3 worldPosition)
        {
            return Vector3.Distance(worldPosition, transform.position) <= PickupRadius;
        }

        public bool TryClaim()
        {
            if (!IsServer || !IsSpawned)
            {
                return false;
            }

            NetworkObject.Despawn(true);
            return true;
        }

        public static MiniVanAspenStakePickup ServerSpawn(Vector3 position, Quaternion rotation)
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            {
                return null;
            }

            GameObject prefab = null;
#if UNITY_EDITOR
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabAssetPath);
#endif
            if (prefab == null)
            {
                prefab = FindRegisteredPrefab("AspenStakePickup");
            }

            if (prefab == null)
            {
                Debug.LogWarning("[AspenStake] Prefab missing.");
                return null;
            }

            GameObject instance = Object.Instantiate(prefab, position, rotation);
            NetworkObject net = instance.GetComponent<NetworkObject>();
            MiniVanAspenStakePickup pickup = instance.GetComponent<MiniVanAspenStakePickup>();
            if (net == null || pickup == null)
            {
                Object.Destroy(instance);
                return null;
            }

            net.Spawn(true);
            return pickup;
        }

        private static GameObject FindRegisteredPrefab(string name)
        {
            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null || nm.NetworkConfig == null || nm.NetworkConfig.Prefabs == null)
            {
                return null;
            }

            foreach (NetworkPrefab entry in nm.NetworkConfig.Prefabs.Prefabs)
            {
                if (entry != null && entry.Prefab != null && entry.Prefab.name == name)
                {
                    return entry.Prefab;
                }
            }

            return null;
        }

        private void Awake()
        {
            // Keep authored prefab visuals; only build a procedural fallback if the prefab is empty.
            if (!HasAuthoredVisual(gameObject))
            {
                EnsureBuiltVisual(gameObject);
            }

            BoxCollider box = GetComponent<BoxCollider>();
            if (box != null)
            {
                box.isTrigger = true;
                if (box.size.sqrMagnitude < 0.01f)
                {
                    box.center = new Vector3(0f, 0.1f, 0.4f);
                    box.size = new Vector3(0.25f, 0.25f, 0.95f);
                }
            }
        }

        public static bool HasAuthoredVisual(GameObject root)
        {
            if (root == null)
            {
                return false;
            }

            return root.GetComponentInChildren<MeshRenderer>(true) != null ||
                   root.GetComponentInChildren<SkinnedMeshRenderer>(true) != null ||
                   root.GetComponentInChildren<MeshFilter>(true) != null;
        }

        public static void EnsureBuiltVisual(GameObject root)
        {
            if (root == null || HasAuthoredVisual(root))
            {
                return;
            }

            if (root.transform.Find("Aspen Stake Visual") == null)
            {
                GameObject visual = new GameObject("Aspen Stake Visual");
                visual.transform.SetParent(root.transform, false);
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;

                Material wood = LoadWoodMaterial();
                Material tip = LoadTipMaterial();

                GameObject shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                shaft.name = "Shaft";
                shaft.transform.SetParent(visual.transform, false);
                shaft.transform.localPosition = new Vector3(0f, 0f, 0.34f);
                shaft.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                shaft.transform.localScale = new Vector3(0.038f, 0.34f, 0.038f);
                SetMaterial(shaft, wood);
                DisableCollider(shaft);

                GameObject butt = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                butt.name = "Butt";
                butt.transform.SetParent(visual.transform, false);
                butt.transform.localPosition = new Vector3(0f, 0f, 0.02f);
                butt.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                butt.transform.localScale = new Vector3(0.048f, 0.035f, 0.048f);
                SetMaterial(butt, wood);
                DisableCollider(butt);

                GameObject point = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                point.name = "Tip";
                point.transform.SetParent(visual.transform, false);
                point.transform.localPosition = new Vector3(0f, 0f, 0.72f);
                point.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                point.transform.localScale = new Vector3(0.02f, 0.07f, 0.02f);
                SetMaterial(point, tip);
                DisableCollider(point);

                GameObject tipEnd = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                tipEnd.name = "TipPoint";
                tipEnd.transform.SetParent(visual.transform, false);
                tipEnd.transform.localPosition = new Vector3(0f, 0f, 0.8f);
                tipEnd.transform.localScale = new Vector3(0.018f, 0.018f, 0.045f);
                SetMaterial(tipEnd, tip);
                DisableCollider(tipEnd);
            }

            ApplySharedMaterials(root);
        }

        public static GameObject ResolveHeldPrefab()
        {
#if UNITY_EDITOR
            GameObject editorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HeldPrefabAssetPath);
            if (editorPrefab != null)
            {
                return editorPrefab;
            }
#endif
            return Resources.Load<GameObject>("Weapons/Melee/AspenStakeHeld");
        }

        public static void ApplySharedMaterials(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            Material wood = LoadWoodMaterial();
            Material tip = LoadTipMaterial();
            SetChildMaterial(root.transform, "Shaft", wood);
            SetChildMaterial(root.transform, "Butt", wood);
            SetChildMaterial(root.transform, "Tip", tip);
            SetChildMaterial(root.transform, "TipPoint", tip);
        }

        public static GameObject CreateHeldVisual(Transform parent)
        {
            GameObject root = new GameObject("AspenStakeHeld");
            root.transform.SetParent(parent, false);
            EnsureBuiltVisual(root);
            Collider[] cols = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < cols.Length; i++)
            {
                cols[i].enabled = false;
            }

            return root;
        }

        public static Material LoadWoodMaterial()
        {
            return LoadOrCreateLitMaterial("AspenStake_Wood", new Color(0.62f, 0.42f, 0.2f, 1f), 0.05f, 0.3f);
        }

        public static Material LoadTipMaterial()
        {
            return LoadOrCreateLitMaterial("AspenStake_Tip", new Color(0.78f, 0.72f, 0.58f, 1f), 0.1f, 0.45f);
        }

        private static Material LoadOrCreateLitMaterial(string name, Color color, float metallic, float smoothness)
        {
            Material loaded = LoadMaterialAsset(name);
            if (loaded != null)
            {
                return loaded;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            Material mat = new Material(shader) { name = name };
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

            return mat;
        }

        private static Material LoadMaterialAsset(string name)
        {
#if UNITY_EDITOR
            Material editorMat = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "/" + name + ".mat");
            if (editorMat != null)
            {
                return editorMat;
            }
#endif
            return Resources.Load<Material>("Weapons/AspenStake/" + name);
        }

        private static void SetChildMaterial(Transform root, string childName, Material material)
        {
            Transform child = FindDeepChild(root, childName);
            if (child == null || material == null)
            {
                return;
            }

            Renderer renderer = child.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
            }
        }

        private static Transform FindDeepChild(Transform parent, string name)
        {
            if (parent.name == name)
            {
                return parent;
            }

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform found = FindDeepChild(parent.GetChild(i), name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static void SetMaterial(GameObject target, Material material)
        {
            Renderer renderer = target != null ? target.GetComponent<Renderer>() : null;
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
            }
        }

        private static void DisableCollider(GameObject target)
        {
            Collider collider = target != null ? target.GetComponent<Collider>() : null;
            if (collider == null)
            {
                return;
            }

            collider.enabled = false;
            if (Application.isPlaying)
            {
                Object.Destroy(collider);
            }
            else
            {
                Object.DestroyImmediate(collider);
            }
        }
    }
}
