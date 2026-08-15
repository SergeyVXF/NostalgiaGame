using Unity.Netcode;
using UnityEngine;

namespace MiniVanGame
{
    public partial class MiniVanPlayer
    {
        private Transform antonLocatorHeldVisual;
        private MiniVanAntonLocatorScreen antonLocatorScreen;

        private void UpdateAntonLocator()
        {
            if (!IsOwner)
            {
                return;
            }

            // Live compass only while the held visual is already shown (pose/slot refresh is dirty-driven).
            if (antonLocatorHeldVisual != null && antonLocatorHeldVisual.gameObject.activeSelf)
            {
                RefreshAntonLocatorScreen();
            }

            if (MiniVanKeyBindings.GetKeyDown(MiniVanKeyAction.Drop) &&
                !antonInteractionConsumedThisFrame &&
                HandleAntonLocatorDropInput())
            {
                antonInteractionConsumedThisFrame = true;
            }
        }

        private void UpdateAntonLocatorHeldVisual()
        {
            bool show = IsOwner &&
                        IsSelectedInventoryItem(MiniVanInventoryItem.AntonLocator) &&
                        currentSeat == null &&
                        !IsFacePainting() &&
                        !IsDowned;

            if (!show)
            {
                if (antonLocatorHeldVisual != null)
                {
                    antonLocatorHeldVisual.gameObject.SetActive(false);
                }

                return;
            }

            EnsureAntonLocatorHeldVisual();
            antonLocatorHeldVisual.gameObject.SetActive(true);
        }

        private void EnsureAntonLocatorHeldVisual()
        {
            if (antonLocatorHeldVisual != null)
            {
                return;
            }

            Transform parent = PlayerCamera != null ? PlayerCamera.transform : transform;
            antonLocatorHeldVisual = MiniVanAntonLocatorVisual.Spawn(parent, "AntonLocatorHeld", worldScale: 0.85f);
            antonLocatorHeldVisual.localPosition = new Vector3(0.26f, -0.2f, 0.48f);
            antonLocatorHeldVisual.localRotation = Quaternion.Euler(32.7f, 18.5f, 0f);

            antonLocatorScreen = new MiniVanAntonLocatorScreen(antonLocatorHeldVisual);
        }

        private void RefreshAntonLocatorScreen()
        {
            if (antonLocatorScreen == null)
            {
                return;
            }

            MiniVanAnton anton = MiniVanAnton.FindNearest(transform.position);
            if (anton == null)
            {
                antonLocatorScreen.ApplyInactive();
                return;
            }

            Vector3 antonPos = anton.ReplicatedPosition;
            Vector3 from = transform.position;
            Vector3 flatDelta = antonPos - from;
            flatDelta.y = 0f;
            float xzDist = flatDelta.magnitude;

            if (xzDist > MiniVanAntonLocatorScreen.ActiveRangeMeters)
            {
                antonLocatorScreen.ApplyInactive();
                return;
            }

            Vector3 look = PlayerCamera != null ? PlayerCamera.transform.forward : transform.forward;
            look.y = 0f;
            if (look.sqrMagnitude < 0.0001f)
            {
                look = transform.forward;
                look.y = 0f;
            }

            look.Normalize();
            Vector3 toAnton = flatDelta.sqrMagnitude > 0.0001f ? flatDelta.normalized : look;
            float angle = Vector3.Angle(look, toAnton);
            float half = MiniVanAntonLocatorScreen.GetHotHalfAngleDegrees(xzDist);
            float hotness = MiniVanAntonLocatorScreen.ComputeHotness(angle, half);

            float dy = antonPos.y - from.y;
            int heightMode = 0;
            if (dy > MiniVanAntonLocatorScreen.HeightLevelMeters)
            {
                heightMode = 1;
            }
            else if (dy < -MiniVanAntonLocatorScreen.HeightLevelMeters)
            {
                heightMode = -1;
            }

            antonLocatorScreen.ApplyActive(xzDist, hotness, heightMode);
        }

        public bool TryPickupAntonLocator(MiniVanAntonLocatorPickup pickup)
        {
            if (pickup == null ||
                HasInventoryItem(MiniVanInventoryItem.AntonLocator) ||
                !pickup.IsAvailable ||
                pickup.NetworkObject == null ||
                !pickup.NetworkObject.IsSpawned)
            {
                return false;
            }

            RequestAntonLocatorPickupServerRpc(new NetworkObjectReference(pickup.NetworkObject));
            return true;
        }

        private bool HandleAntonLocatorDropInput()
        {
            if (currentSeat != null || currentSkateboard != null || currentHoverboardM != null)
            {
                return false;
            }

            if (!IsSelectedInventoryItem(MiniVanInventoryItem.AntonLocator))
            {
                return false;
            }

            Vector3 dropPosition = GetLooseItemDropPosition();
            Quaternion dropRotation = GetLooseItemDropRotation();
            if (!IsServer)
            {
                PredictClearInventoryItem(MiniVanInventoryItem.AntonLocator);
                InvalidateStaticHeldVisuals();
                RefreshStaticHeldVisualsIfNeeded(true);
            }

            RequestDropSelectedAntonLocatorServerRpc(dropPosition, dropRotation);
            return true;
        }

        [ServerRpc]
        private void RequestAntonLocatorPickupServerRpc(NetworkObjectReference locatorReference, ServerRpcParams rpcParams = default)
        {
            if (HasInventoryItem(MiniVanInventoryItem.AntonLocator) ||
                !locatorReference.TryGet(out NetworkObject locatorObject))
            {
                return;
            }

            MiniVanAntonLocatorPickup pickup = locatorObject.GetComponent<MiniVanAntonLocatorPickup>();
            if (pickup == null || !pickup.IsAvailable || !pickup.IsInReach(transform.position))
            {
                return;
            }

            int emptySlot = FindFirstEmptyInventorySlot();
            if (emptySlot < 0 || !pickup.TryClaim())
            {
                return;
            }

            SetInventorySlot(emptySlot, MiniVanInventoryItem.AntonLocator);
            networkSelectedSlot.Value = emptySlot;
            SetLocalInventorySlotClientRpc(emptySlot, (int)MiniVanInventoryItem.AntonLocator, BuildOwnerTarget());
        }

        [ServerRpc]
        private void RequestDropSelectedAntonLocatorServerRpc(Vector3 dropPosition, Quaternion dropRotation, ServerRpcParams rpcParams = default)
        {
            if (currentSeat != null || currentSkateboard != null || currentHoverboardM != null)
            {
                return;
            }

            int slot = FindInventorySlot(MiniVanInventoryItem.AntonLocator);
            if (slot < 0 || !ServerSpawnAntonLocator(dropPosition, dropRotation))
            {
                return;
            }

            SetInventorySlot(slot, MiniVanInventoryItem.None);
            SetLocalInventorySlotClientRpc(slot, (int)MiniVanInventoryItem.None, BuildOwnerTarget());
        }

        private bool ServerSpawnAntonLocator(Vector3 worldPosition, Quaternion rotation)
        {
            if (!IsServer)
            {
                return false;
            }

            GameObject prefab = MiniVanAntonTestSpawner.ResolveAntonLocatorPrefab();
            if (prefab == null)
            {
                Debug.LogWarning("[MiniVan] Anton locator prefab missing.");
                return false;
            }

            GameObject instance = Instantiate(prefab, worldPosition, rotation);
            NetworkObject net = instance.GetComponent<NetworkObject>();
            if (net == null)
            {
                Object.Destroy(instance);
                return false;
            }

            net.Spawn(true);
            return true;
        }
    }
}
