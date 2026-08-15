using Unity.Netcode;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MiniVanGame
{
    /// <summary>
    /// World pickup for the defibrillator. Visual: MiniVan_Defib_Suitcase mesh.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class MiniVanDefibrillatorPickup : NetworkBehaviour, IMiniVanGameModeInteractable
    {
        public const string PrefabAssetPath = "Assets/MiniVan Game/Prefabs/Equipment/DefibrillatorPickup.prefab";
        public const string SuitcaseMeshPath = "Assets/MiniVan Game/Art/Defibrillator/MiniVan_Defib_Suitcase.fbx";
        public const string TubeMeshPath = "Assets/MiniVan Game/Art/Defibrillator/MiniVan_Defib_Tube.fbx";
        public const string ResourcesPickupPath = "Defibrillator/DefibrillatorPickup";
        public const string ResourcesTubePath = "Defibrillator/DefibTubeHeld";
        public const string ResourcesSparksPath = "Defibrillator/DefibSparks";
        public const string SparkAnchorName = "SparkAnchor";

        public float PickupRadius = 2.4f;

        private readonly NetworkVariable<bool> available = new NetworkVariable<bool>(
            true,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private Renderer[] cachedRenderers;
        private Collider[] cachedColliders;

        public bool IsAvailable => !IsSpawned || available.Value;

        private void Awake()
        {
            EnsureSuitcaseVisual(gameObject);
            CacheParts();
            ApplyAvailable(true);
        }

        public override void OnNetworkSpawn()
        {
            EnsureSuitcaseVisual(gameObject);
            CacheParts();
            available.OnValueChanged += HandleAvailableChanged;
            ApplyAvailable(available.Value);
        }

        public override void OnNetworkDespawn()
        {
            available.OnValueChanged -= HandleAvailableChanged;
        }

        public string GetPrompt(MiniVanPlayer player)
        {
            if (player == null || !IsAvailable || !IsInReach(player.transform.position))
            {
                return string.Empty;
            }

            if (player.HasDefibrillatorInInventory())
            {
                return "Already have defibrillator";
            }

            return "E - take defibrillator";
        }

        public void Interact(MiniVanPlayer player)
        {
            if (player == null || Input.GetMouseButton(1) || !IsAvailable)
            {
                return;
            }

            if (player.HasDefibrillatorInInventory())
            {
                return;
            }

            player.RequestTakeDefibrillator(this);
        }

        public void PrimaryAction(MiniVanPlayer player)
        {
        }

        public bool IsInReach(Vector3 worldPosition)
        {
            return Vector3.Distance(worldPosition, transform.position) <= PickupRadius;
        }

        public bool TryClaim()
        {
            if (!IsServer || !IsSpawned || !available.Value)
            {
                return false;
            }

            // Hide on all clients via NetworkVariable (Despawn alone can leave ghost visuals
            // when scene / Resources prefab hashes diverge).
            available.Value = false;
            ApplyAvailable(false);
            return true;
        }

        private void HandleAvailableChanged(bool previousValue, bool newValue)
        {
            ApplyAvailable(newValue);
        }

        private void CacheParts()
        {
            cachedRenderers = GetComponentsInChildren<Renderer>(true);
            cachedColliders = GetComponentsInChildren<Collider>(true);
            BoxCollider box = GetComponent<BoxCollider>();
            if (box != null)
            {
                box.isTrigger = true;
            }
        }

        private void ApplyAvailable(bool isAvailable)
        {
            if (cachedRenderers != null)
            {
                for (int i = 0; i < cachedRenderers.Length; i++)
                {
                    if (cachedRenderers[i] != null)
                    {
                        cachedRenderers[i].enabled = isAvailable;
                    }
                }
            }

            if (cachedColliders != null)
            {
                for (int i = 0; i < cachedColliders.Length; i++)
                {
                    if (cachedColliders[i] != null)
                    {
                        cachedColliders[i].enabled = isAvailable;
                    }
                }
            }
        }

        public static GameObject ResolvePrefab()
        {
#if UNITY_EDITOR
            GameObject fromAssets = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabAssetPath);
            if (fromAssets != null)
            {
                return fromAssets;
            }
#endif
            return Resources.Load<GameObject>(ResourcesPickupPath);
        }

        public static MiniVanDefibrillatorPickup ServerSpawn(Vector3 position, Quaternion rotation)
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            {
                return null;
            }

            GameObject prefab = ResolvePrefab();
            if (prefab == null)
            {
                Debug.LogWarning("[Defibrillator] Prefab missing.");
                return null;
            }

            GameObject instance = Object.Instantiate(prefab, position, rotation);
            NetworkObject net = instance.GetComponent<NetworkObject>();
            if (net == null)
            {
                Object.Destroy(instance);
                return null;
            }

            net.Spawn(true);
            return instance.GetComponent<MiniVanDefibrillatorPickup>();
        }

        public static GameObject LoadSuitcaseVisualPrefab()
        {
#if UNITY_EDITOR
            GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(SuitcaseMeshPath);
            if (fbx != null)
            {
                return fbx;
            }
#endif
            return Resources.Load<GameObject>("Defibrillator/MiniVan_Defib_Suitcase");
        }

        public static GameObject LoadHeldTubePrefab()
        {
            return Resources.Load<GameObject>(ResourcesTubePath);
        }

        public static GameObject LoadSparksPrefab()
        {
            return Resources.Load<GameObject>(ResourcesSparksPath);
        }

        public static GameObject LoadTubeVisualPrefab()
        {
            GameObject held = LoadHeldTubePrefab();
            if (held != null)
            {
                return held;
            }

#if UNITY_EDITOR
            GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(TubeMeshPath);
            if (fbx != null)
            {
                return fbx;
            }
#endif
            return Resources.Load<GameObject>("Defibrillator/MiniVan_Defib_Tube");
        }

        public static Transform FindSparkAnchor(Transform heldVisual)
        {
            if (heldVisual == null)
            {
                return null;
            }

            Transform anchor = heldVisual.Find(SparkAnchorName);
            return anchor != null ? anchor : heldVisual;
        }

        public static void EnsureSuitcaseVisual(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            Transform existing = root.transform.Find("SuitcaseVisual");
            if (existing != null)
            {
                return;
            }

            GameObject source = LoadSuitcaseVisualPrefab();
            GameObject visual;
            if (source != null)
            {
                visual = Object.Instantiate(source);
                visual.name = "SuitcaseVisual";
            }
            else
            {
                visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                visual.name = "SuitcaseVisual";
                visual.transform.localScale = new Vector3(0.48f, 0.22f, 0.34f);
                Collider boxCol = visual.GetComponent<Collider>();
                if (boxCol != null)
                {
                    if (Application.isPlaying)
                    {
                        Object.Destroy(boxCol);
                    }
                    else
                    {
                        Object.DestroyImmediate(boxCol);
                    }
                }
            }

            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;
            StripColliders(visual);
        }

        public static Transform CreateHeldTubeVisual(Transform parent)
        {
            GameObject heldPrefab = LoadHeldTubePrefab();
            if (heldPrefab != null)
            {
                GameObject visual = Object.Instantiate(heldPrefab);
                visual.name = "DefibTubeHeld";
                visual.transform.SetParent(parent, false);
                // Pose comes from the prefab root transform — edit DefibTubeHeld in the Inspector.
                StripColliders(visual);
                return visual.transform;
            }

            GameObject source = LoadTubeVisualPrefab();
            GameObject fallback;
            if (source != null)
            {
                fallback = Object.Instantiate(source);
                fallback.name = "DefibTubeHeld";
            }
            else
            {
                fallback = GameObject.CreatePrimitive(PrimitiveType.Cube);
                fallback.name = "DefibTubeHeld";
                fallback.transform.localScale = new Vector3(0.08f, 0.28f, 0.08f);
                Collider boxCol = fallback.GetComponent<Collider>();
                if (boxCol != null)
                {
                    if (Application.isPlaying)
                    {
                        Object.Destroy(boxCol);
                    }
                    else
                    {
                        Object.DestroyImmediate(boxCol);
                    }
                }
            }

            fallback.transform.SetParent(parent, false);
            fallback.transform.localPosition = new Vector3(0.18f, -0.12f, 0.34f);
            fallback.transform.localRotation = Quaternion.Euler(-66.5f, 9.05f, 0.3f);
            fallback.transform.localScale = Vector3.one * 0.95f;
            StripColliders(fallback);
            return fallback.transform;
        }

        private static void StripColliders(GameObject root)
        {
            Collider[] cols = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < cols.Length; i++)
            {
                if (cols[i] == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Object.Destroy(cols[i]);
                }
                else
                {
                    Object.DestroyImmediate(cols[i]);
                }
            }
        }
    }
}
