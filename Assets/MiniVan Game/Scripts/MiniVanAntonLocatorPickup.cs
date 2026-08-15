using Unity.Netcode;
using UnityEngine;

namespace MiniVanGame
{
    /// <summary>
    /// World pickup for the Anton direction/height locator.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class MiniVanAntonLocatorPickup : NetworkBehaviour, IMiniVanGameModeInteractable
    {
        public float PickupRadius = 2.2f;

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

        public string GetPrompt(MiniVanPlayer player)
        {
            if (player == null || !IsAvailable)
            {
                return string.Empty;
            }

            return IsInReach(player.transform.position) ? "E - take Anton locator" : string.Empty;
        }

        public void Interact(MiniVanPlayer player)
        {
            if (player != null)
            {
                player.TryPickupAntonLocator(this);
            }
        }

        public void PrimaryAction(MiniVanPlayer player)
        {
        }

        private void HandleAvailableChanged(bool previousValue, bool newValue)
        {
            ApplyAvailable(newValue);
        }

        private void EnsureVisual()
        {
            if (transform.Find(MiniVanAntonLocatorVisual.VisualChildName) != null)
            {
                return;
            }

            MiniVanAntonLocatorVisual.Build(transform, MiniVanAntonLocatorVisual.VisualChildName, worldScale: 1f);
        }

        private void CacheParts()
        {
            renderers = GetComponentsInChildren<Renderer>(true);
            BoxCollider box = GetComponent<BoxCollider>();
            if (box != null)
            {
                box.center = new Vector3(0f, 0.12f, 0f);
                box.size = new Vector3(0.55f, 0.35f, 0.55f);
                box.isTrigger = true;
                colliders = new Collider[] { box };
            }
            else
            {
                colliders = System.Array.Empty<Collider>();
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
