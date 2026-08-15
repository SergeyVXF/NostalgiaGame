using Unity.Netcode;
using UnityEngine;


// Gameplay pickup shared by the normal bat and the red PvP test baton.
namespace MiniVanGame
{
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(BoxCollider))]
    public class MiniVanBatPickup : NetworkBehaviour
    {
        public float PickupRadius = 2.2f;
        public bool IsTestBaton;

        private readonly NetworkVariable<bool> available = new NetworkVariable<bool>(
            true,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private Renderer[] renderers;
        private Collider[] colliders;

        public bool IsAvailable => !IsSpawned || available.Value;

        private void Awake()
        {
            EnsureVisual();
            CacheParts();
            ApplyAvailable(true);
        }

        public override void OnNetworkSpawn()
        {
            EnsureVisual();
            CacheParts();
            available.OnValueChanged += HandleAvailableChanged;
            ApplyAvailable(available.Value);
        }

        public override void OnNetworkDespawn()
        {
            available.OnValueChanged -= HandleAvailableChanged;
        }

        public bool IsInReach(Vector3 worldPosition)
        {
            return Vector3.Distance(worldPosition, transform.position) <= PickupRadius;
        }

        public bool TryClaim()
        {
            if (!IsServer || !available.Value)
            {
                return false;
            }

            available.Value = false;
            ApplyAvailable(false);
            return true;
        }

        public void ServerRelease(Vector3 position, Quaternion rotation)
        {
            if (!IsServer)
            {
                return;
            }

            transform.SetPositionAndRotation(position, rotation);
            available.Value = true;
            CacheParts();
            ApplyAvailable(true);
        }

        public static MiniVanBatPickup ServerSpawn(Vector3 position, Quaternion rotation, bool testBaton)
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            {
                return null;
            }

            GameObject prefab = FindRegisteredPrefab(testBaton ? "RedBaton" : "BatPickup");
            if (prefab == null)
            {
                prefab = FindRegisteredPrefab("BatPickup");
            }

            if (prefab == null)
            {
                Debug.LogWarning("[BatPickup] Prefab missing.");
                return null;
            }

            GameObject instance = Object.Instantiate(prefab, position, rotation);
            NetworkObject net = instance.GetComponent<NetworkObject>();
            MiniVanBatPickup pickup = instance.GetComponent<MiniVanBatPickup>();
            if (net == null || pickup == null)
            {
                Object.Destroy(instance);
                return null;
            }

            pickup.IsTestBaton = testBaton;
            net.Spawn(true);
            pickup.ServerRelease(position, rotation);
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

        private void HandleAvailableChanged(bool previousValue, bool newValue)
        {
            ApplyAvailable(newValue);
        }

        private void EnsureVisual()
        {
            if (transform.Find("Bat Visual") != null)
            {
                return;
            }

            GameObject visual = new GameObject("Bat Visual");
            visual.transform.SetParent(transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.Euler(0f, 0f, 78f);

            Material wood = CreateMaterial(IsTestBaton
                ? new Color(0.92f, 0.025f, 0.018f, 1f)
                : new Color(0.55f, 0.31f, 0.13f, 1f));
            Material grip = CreateMaterial(new Color(0.06f, 0.055f, 0.05f, 1f));

            GameObject barrel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            barrel.name = "Bat Barrel";
            barrel.transform.SetParent(visual.transform, false);
            barrel.transform.localPosition = new Vector3(0f, 0.36f, 0f);
            barrel.transform.localRotation = Quaternion.identity;
            barrel.transform.localScale = new Vector3(0.09f, 0.52f, 0.09f);
            SetMaterial(barrel, wood);
            DisableCollider(barrel);

            GameObject handle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            handle.name = "Bat Handle";
            handle.transform.SetParent(visual.transform, false);
            handle.transform.localPosition = new Vector3(0f, -0.28f, 0f);
            handle.transform.localRotation = Quaternion.identity;
            handle.transform.localScale = new Vector3(0.045f, 0.22f, 0.045f);
            SetMaterial(handle, grip);
            DisableCollider(handle);
        }

        private void CacheParts()
        {
            renderers = GetComponentsInChildren<Renderer>(true);
            colliders = GetComponentsInChildren<Collider>(true);
            BoxCollider box = GetComponent<BoxCollider>();
            if (box != null)
            {
                box.size = new Vector3(0.35f, 1.45f, 0.35f);
                box.center = new Vector3(0f, 0.15f, 0f);
                box.isTrigger = true;
            }
        }

        public void RefreshBatonAppearance()
        {
            Renderer[] visuals = GetComponentsInChildren<Renderer>(true);
            if (!IsTestBaton || visuals == null) return;
            Material red = CreateMaterial(new Color(0.92f, 0.025f, 0.018f, 1f));
            for (int i = 0; i < visuals.Length; i++)
            {
                if (visuals[i] != null) visuals[i].sharedMaterial = red;
            }
        }

        private void ApplyAvailable(bool isAvailable)
        {
            if (renderers != null)
            {
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i] != null)
                    {
                        renderers[i].enabled = isAvailable;
                    }
                }
            }

            if (colliders != null)
            {
                for (int i = 0; i < colliders.Length; i++)
                {
                    if (colliders[i] != null)
                    {
                        colliders[i].enabled = isAvailable;
                    }
                }
            }
        }

        private static Material CreateMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            Material material = new Material(shader);
            material.color = color;
            return material;
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
            if (collider != null)
            {
                collider.enabled = false;
            }
        }
    }
}
