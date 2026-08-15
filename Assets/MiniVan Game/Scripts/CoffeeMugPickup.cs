using Unity.Netcode;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif


namespace MiniVanGame
{
    [RequireComponent(typeof(NetworkObject))]
    public class CoffeeMugPickup : NetworkBehaviour
    {
        public float PickupRadius = 2.25f;
        public GameObject VisualPrefab;
        public string VisualChildName = "Coffee Mug Visual";


        private readonly NetworkVariable<bool> available = new NetworkVariable<bool>(
            true,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private Renderer[] renderers;
        private Collider[] colliders;

        
        [ContextMenu("Rebuild Visual From Prefab")]
[ContextMenu("Rebuild Visual From Prefab")]
        public void RebuildVisualFromPrefab()
        {
            if (VisualPrefab == null)
            {
                return;
            }

            string childName = string.IsNullOrWhiteSpace(VisualChildName) ? "Coffee Mug Visual" : VisualChildName;
            Transform existing = transform.Find(childName);
            if (existing != null)
            {
                DestroyImmediate(existing.gameObject);
            }

            GameObject instance = CreateVisualInstance();
            instance.name = childName;
            instance.transform.SetParent(transform, false);
            instance.transform.localPosition = Vector3.zero;
                    instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            DisableVisualColliders(instance);
            CacheVisuals();
            ApplyAvailable(IsAvailable);
        }
public bool IsAvailable => !IsSpawned || available.Value;

private void Awake()
        {
            EnsureVisualInstance();
            CacheVisuals();
            ApplyAvailable(true);
        }

public override void OnNetworkSpawn()
        {
            EnsureVisualInstance();
            CacheVisuals();
            available.OnValueChanged += HandleAvailableChanged;
            ApplyAvailable(available.Value);
        }

        public override void OnNetworkDespawn()
        {
            available.OnValueChanged -= HandleAvailableChanged;
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
            CacheVisuals();
            ApplyAvailable(true);
        }

        private void HandleAvailableChanged(bool previousValue, bool newValue)
        {
            ApplyAvailable(newValue);
        }

private void CacheVisuals()
        {
            renderers = GetComponentsInChildren<Renderer>(true);
            colliders = GetComponentsInChildren<Collider>(true);
        }

private void EnsureVisualInstance()
        {
            if (VisualPrefab == null)
            {
                return;
            }

            string childName = string.IsNullOrWhiteSpace(VisualChildName) ? "Coffee Mug Visual" : VisualChildName;
            Transform visual = transform.Find(childName);

            if (visual != null && visual.gameObject.name == childName)
            {
                return;
            }

            GameObject instance = CreateVisualInstance();
            instance.name = childName;
            instance.transform.SetParent(transform, false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            DisableVisualColliders(instance);
        }

        private GameObject CreateVisualInstance()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                GameObject prefabInstance = PrefabUtility.InstantiatePrefab(VisualPrefab) as GameObject;
                if (prefabInstance != null)
                {
                    return prefabInstance;
                }
            }
#endif
            return Instantiate(VisualPrefab);
        }

        private static void DisableVisualColliders(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            Collider[] visualColliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < visualColliders.Length; i++)
            {
                visualColliders[i].enabled = false;
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
    }
}
