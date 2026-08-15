using Unity.Netcode;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MiniVanGame
{
    /// <summary>
    /// World pickup for the fire extinguisher inventory item.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class MiniVanFireExtinguisherPickup : NetworkBehaviour, IMiniVanGameModeInteractable
    {
        public const string PrefabAssetPath = "Assets/MiniVan Game/Prefabs/Equipment/FireExtinguisherPickup.prefab";
        public const string MaterialFolder = "Assets/MiniVan Game/Materials/Equipment/FireExtinguisher";
        public const string ResourcesMaterialFolder = "FireExtinguisher";
        public const float DefaultCharge = 100f;

        public float PickupRadius = 2.4f;

        private readonly NetworkVariable<float> remainingCharge = new NetworkVariable<float>(
            DefaultCharge,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public float RemainingCharge => IsSpawned ? remainingCharge.Value : DefaultCharge;
        public bool IsAvailable => IsSpawned;

        public string GetPrompt(MiniVanPlayer player)
        {
            if (player == null || !IsAvailable || !IsInReach(player.transform.position))
            {
                return string.Empty;
            }

            if (player.HasFireExtinguisherInInventory())
            {
                return "Already have extinguisher";
            }

            return "E - take fire extinguisher";
        }

        public void Interact(MiniVanPlayer player)
        {
            if (player == null || Input.GetMouseButton(1) || !IsAvailable)
            {
                return;
            }

            if (player.HasFireExtinguisherInInventory())
            {
                return;
            }

            player.RequestTakeFireExtinguisher(this);
        }

        public void PrimaryAction(MiniVanPlayer player)
        {
        }

        public bool IsInReach(Vector3 worldPosition)
        {
            return Vector3.Distance(worldPosition, transform.position) <= PickupRadius;
        }

        public void ServerSetCharge(float charge)
        {
            if (!IsServer)
            {
                return;
            }

            remainingCharge.Value = Mathf.Clamp(charge, 0f, DefaultCharge);
        }

        public bool TryClaim(out float charge)
        {
            charge = 0f;
            if (!IsServer || !IsSpawned)
            {
                return false;
            }

            charge = Mathf.Clamp(remainingCharge.Value, 0f, DefaultCharge);
            if (charge <= 0.001f)
            {
                NetworkObject.Despawn(true);
                return false;
            }

            NetworkObject.Despawn(true);
            return true;
        }

        public static GameObject ResolvePrefab()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabAssetPath);
            }
#endif
            return Resources.Load<GameObject>("FireExtinguisher/FireExtinguisherPickup");
        }

        public static MiniVanFireExtinguisherPickup ServerSpawn(Vector3 position, Quaternion rotation, float charge)
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
                // Runtime: resolve from network prefabs list by name.
                prefab = FindRegisteredPrefab();
            }

            if (prefab == null)
            {
                Debug.LogWarning("[FireExtinguisher] Prefab missing.");
                return null;
            }

            GameObject instance = Object.Instantiate(prefab, position, rotation);
            NetworkObject net = instance.GetComponent<NetworkObject>();
            MiniVanFireExtinguisherPickup pickup = instance.GetComponent<MiniVanFireExtinguisherPickup>();
            if (net == null || pickup == null)
            {
                Object.Destroy(instance);
                return null;
            }

            net.Spawn(true);
            pickup.ServerSetCharge(Mathf.Clamp(charge, 0f, DefaultCharge));
            return pickup;
        }

        private static GameObject FindRegisteredPrefab()
        {
            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null || nm.NetworkConfig == null || nm.NetworkConfig.Prefabs == null)
            {
                return null;
            }

            foreach (NetworkPrefab entry in nm.NetworkConfig.Prefabs.Prefabs)
            {
                if (entry == null || entry.Prefab == null)
                {
                    continue;
                }

                if (entry.Prefab.GetComponent<MiniVanFireExtinguisherPickup>() != null)
                {
                    return entry.Prefab;
                }
            }

            return null;
        }

        public static void EnsureBuiltVisual(GameObject root)
        {
            if (root == null || root.transform.Find("Extinguisher Visual") != null)
            {
                return;
            }

            Material red = LoadOrCreateMaterial("FireExtinguisher_Red", new Color(0.78f, 0.07f, 0.05f, 1f), 0.15f, 0.45f);
            Material black = LoadOrCreateMaterial("FireExtinguisher_Black", new Color(0.07f, 0.07f, 0.08f, 1f), 0.35f, 0.55f);
            Material silver = LoadOrCreateMaterial("FireExtinguisher_Silver", new Color(0.72f, 0.73f, 0.76f, 1f), 0.75f, 0.65f);

            GameObject visual = new GameObject("Extinguisher Visual");
            visual.transform.SetParent(root.transform, false);

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.name = "Body";
            DestroyCollider(body);
            body.transform.SetParent(visual.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.38f, 0f);
            body.transform.localScale = new Vector3(0.22f, 0.38f, 0.22f);
            body.GetComponent<Renderer>().sharedMaterial = red;

            GameObject cap = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cap.name = "Cap";
            DestroyCollider(cap);
            cap.transform.SetParent(visual.transform, false);
            cap.transform.localPosition = new Vector3(0f, 0.78f, 0f);
            cap.transform.localScale = new Vector3(0.16f, 0.05f, 0.16f);
            cap.GetComponent<Renderer>().sharedMaterial = black;

            GameObject hose = GameObject.CreatePrimitive(PrimitiveType.Cube);
            hose.name = "Hose";
            DestroyCollider(hose);
            hose.transform.SetParent(visual.transform, false);
            hose.transform.localPosition = new Vector3(0.14f, 0.72f, 0.02f);
            hose.transform.localRotation = Quaternion.Euler(0f, 0f, -35f);
            hose.transform.localScale = new Vector3(0.04f, 0.22f, 0.04f);
            hose.GetComponent<Renderer>().sharedMaterial = black;

            GameObject nozzle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            nozzle.name = "Nozzle";
            DestroyCollider(nozzle);
            nozzle.transform.SetParent(visual.transform, false);
            nozzle.transform.localPosition = new Vector3(0.22f, 0.58f, 0.02f);
            nozzle.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            nozzle.transform.localScale = new Vector3(0.05f, 0.06f, 0.05f);
            nozzle.GetComponent<Renderer>().sharedMaterial = silver;

            GameObject label = GameObject.CreatePrimitive(PrimitiveType.Cube);
            label.name = "Label";
            DestroyCollider(label);
            label.transform.SetParent(visual.transform, false);
            label.transform.localPosition = new Vector3(0f, 0.4f, -0.105f);
            label.transform.localScale = new Vector3(0.14f, 0.18f, 0.02f);
            label.GetComponent<Renderer>().sharedMaterial = silver;
        }

        public static void ApplySharedMaterials(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            Material red = LoadOrCreateMaterial("FireExtinguisher_Red", new Color(0.78f, 0.07f, 0.05f, 1f), 0.15f, 0.45f);
            Material black = LoadOrCreateMaterial("FireExtinguisher_Black", new Color(0.07f, 0.07f, 0.08f, 1f), 0.35f, 0.55f);
            Material silver = LoadOrCreateMaterial("FireExtinguisher_Silver", new Color(0.72f, 0.73f, 0.76f, 1f), 0.75f, 0.65f);

            SetChildMaterial(root.transform, "Body", red);
            SetChildMaterial(root.transform, "Cap", black);
            SetChildMaterial(root.transform, "Hose", black);
            SetChildMaterial(root.transform, "Nozzle", silver);
            SetChildMaterial(root.transform, "Label", silver);
        }

        private static void SetChildMaterial(Transform root, string childName, Material material)
        {
            if (root == null || material == null)
            {
                return;
            }

            Transform child = FindDeepChild(root, childName);
            if (child == null)
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

        private static Material LoadOrCreateMaterial(string name, Color color, float metallic, float smoothness)
        {
            Material loaded = Resources.Load<Material>(ResourcesMaterialFolder + "/" + name);
#if UNITY_EDITOR
            if (loaded == null)
            {
                loaded = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "/" + name + ".mat");
            }
#endif
            if (loaded != null)
            {
                return loaded;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            Material mat = new Material(shader) { name = name, color = color };
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

        private static void DestroyCollider(GameObject go)
        {
            Collider col = go != null ? go.GetComponent<Collider>() : null;
            if (col == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(col);
            }
            else
            {
                Object.DestroyImmediate(col);
            }
        }
    }
}
