using Unity.Netcode;
using UnityEngine;

namespace MiniVanGame
{
    public partial class MiniVanPlayer
    {
        private readonly NetworkVariable<int> amberButtonCount = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public int AmberButtonCount => amberButtonCount.Value;

        public bool TryPickupAmberButton(MiniVanAmberButtonPickup pickup)
        {
            if (!IsOwner || pickup == null || !pickup.IsAvailable || IsDowned)
            {
                return false;
            }

            if (!pickup.IsInReach(transform.position))
            {
                return false;
            }

            RequestAmberButtonPickupServerRpc(new NetworkObjectReference(pickup.NetworkObject));
            return true;
        }

        public bool ServerAddAmberButtons(int amount)
        {
            if (amount <= 0 || (IsSpawned && !IsServer))
            {
                return false;
            }

            amberButtonCount.Value = Mathf.Max(0, amberButtonCount.Value + amount);
            return true;
        }

        [ServerRpc]
        private void RequestAmberButtonPickupServerRpc(
            NetworkObjectReference pickupReference,
            ServerRpcParams rpcParams = default)
        {
            if (!pickupReference.TryGet(out NetworkObject pickupObject))
            {
                return;
            }

            MiniVanAmberButtonPickup pickup = pickupObject.GetComponent<MiniVanAmberButtonPickup>();
            if (pickup == null || !pickup.IsAvailable || IsDowned)
            {
                return;
            }

            if (!pickup.IsInReach(transform.position))
            {
                return;
            }

            // TryClaim also rejects mid-pop scatter so buttons finish flying out first.
            if (!pickup.TryClaim())
            {
                return;
            }

            amberButtonCount.Value = Mathf.Max(0, amberButtonCount.Value + 1);
            pickupObject.Despawn(true);
        }
    }
}
