using UnityEngine;

namespace MiniVanGame
{
    public class MiniVanPizzaChest : MonoBehaviour
    {
        public float InteractRadius = 2.4f;
        public int SlotCount = 12;
        public int[] Slots;

        private void Awake()
        {
            EnsureSlots();
            EnsureCollider();
        }

        private void OnValidate()
        {
            EnsureSlots();
        }

        public void EnsureSlots()
        {
            SlotCount = Mathf.Clamp(SlotCount, 4, 24);
            if (Slots == null || Slots.Length != SlotCount)
            {
                int[] next = new int[SlotCount];
                if (Slots != null)
                {
                    for (int i = 0; i < Mathf.Min(Slots.Length, next.Length); i++)
                    {
                        next[i] = Slots[i];
                    }
                }

                Slots = next;
            }
        }

        public bool IsInRange(Vector3 worldPosition)
        {
            return Vector3.Distance(worldPosition, transform.position) <= InteractRadius;
        }

        public MiniVanInventoryItem GetSlot(int index)
        {
            EnsureSlots();
            if (index < 0 || index >= Slots.Length)
            {
                return MiniVanInventoryItem.None;
            }

            return (MiniVanInventoryItem)Slots[index];
        }

        public void SetSlot(int index, MiniVanInventoryItem item)
        {
            EnsureSlots();
            if (index < 0 || index >= Slots.Length)
            {
                return;
            }

            Slots[index] = (int)item;
        }

        private void EnsureCollider()
        {
            Collider existing = GetComponent<Collider>();
            if (existing != null)
            {
                return;
            }

            BoxCollider box = gameObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(1.2f, 1f, 1.2f);
        }
    }
}
