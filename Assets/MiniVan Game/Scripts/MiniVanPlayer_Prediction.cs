using UnityEngine;

namespace MiniVanGame
{
    /// <summary>
    /// Owner-side optimistic inventory + held-item release so drop/throw feels instant
    /// while ServerRpc remains authoritative.
    /// </summary>
    public partial class MiniVanPlayer
    {
        private readonly bool[] optimisticSlotActive = new bool[4];
        private readonly MiniVanInventoryItem[] optimisticSlots = new MiniVanInventoryItem[4];

        private void BindOptimisticInventoryHandlers()
        {
            networkSlot0.OnValueChanged += (previous, current) => ReconcileOptimisticSlot(0, current);
            networkSlot1.OnValueChanged += (previous, current) => ReconcileOptimisticSlot(1, current);
            networkSlot2.OnValueChanged += (previous, current) => ReconcileOptimisticSlot(2, current);
            networkSlot3.OnValueChanged += (previous, current) => ReconcileOptimisticSlot(3, current);
        }

        private void ReconcileOptimisticSlot(int slotIndex, int networkValue)
        {
            if (slotIndex < 0 || slotIndex > 3 || !optimisticSlotActive[slotIndex])
            {
                return;
            }

            if (optimisticSlots[slotIndex] == (MiniVanInventoryItem)networkValue)
            {
                optimisticSlotActive[slotIndex] = false;
            }
        }

        /// <summary>Instant owner-visible inventory write; server remains source of truth.</summary>
        public void PredictInventorySlot(int slotIndex, MiniVanInventoryItem item)
        {
            if (!IsOwner || IsServer)
            {
                return;
            }

            slotIndex = Mathf.Clamp(slotIndex, 0, 3);
            optimisticSlots[slotIndex] = item;
            optimisticSlotActive[slotIndex] = true;
        }

        public void PredictClearInventoryItem(MiniVanInventoryItem item)
        {
            if (!IsOwner || item == MiniVanInventoryItem.None)
            {
                return;
            }

            for (int i = 0; i < 4; i++)
            {
                if (GetInventorySlot(i) == item)
                {
                    PredictInventorySlot(i, MiniVanInventoryItem.None);
                }
            }
        }

        public void PredictClearInventorySlot(int slotIndex)
        {
            PredictInventorySlot(slotIndex, MiniVanInventoryItem.None);
        }

        private Vector3 GetPredictedDropPosition(float forward = 0.95f, float up = 0.2f)
        {
            Vector3 flatForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (flatForward.sqrMagnitude < 0.01f)
            {
                flatForward = Vector3.forward;
            }

            flatForward.Normalize();
            Vector3 dropPosition = transform.position + flatForward * forward + Vector3.up * up;
            Vector3 rayOrigin = transform.position + flatForward * (forward * 0.7f) + Vector3.up * 0.7f;
            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 2.6f, ~0, QueryTriggerInteraction.Ignore))
            {
                dropPosition = hit.point + hit.normal * 0.08f;
            }

            return dropPosition;
        }

        private Quaternion GetPredictedDropRotation()
        {
            Vector3 flatForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (flatForward.sqrMagnitude < 0.01f)
            {
                flatForward = Vector3.forward;
            }

            return Quaternion.LookRotation(flatForward.normalized, Vector3.up);
        }
    }
}
