using UnityEngine;

namespace MiniVanGame
{
    public class MiniVanPizzaToolShelf : MonoBehaviour
    {
        public float InteractRadius = 2.3f;
        public MiniVanInventoryItem StoredItem = MiniVanInventoryItem.None;
        public Vector3 VisualLocalOffset = new Vector3(0f, 0.12f, 0f);
        public Vector3 VisualLocalEuler = Vector3.zero;
        public Vector3 VisualLocalScale = Vector3.one;

        private Transform visualRoot;

        private void Awake()
        {
            EnsureCollider();
            RefreshVisual();
        }

        private void OnValidate()
        {
            InteractRadius = Mathf.Max(0.5f, InteractRadius);
        }

        public bool IsInRange(Vector3 worldPosition)
        {
            return Vector3.Distance(worldPosition, transform.position) <= InteractRadius;
        }

        public bool CanStore(MiniVanInventoryItem item)
        {
            return item == MiniVanInventoryItem.RollingPin || item == MiniVanInventoryItem.Grater;
        }

        public bool TryPlace(MiniVanInventoryItem item, out string status)
        {
            if (StoredItem != MiniVanInventoryItem.None)
            {
                status = "Shelf occupied";
                return false;
            }

            if (!CanStore(item))
            {
                status = "Hold pin or grater";
                return false;
            }

            StoredItem = item;
            RefreshVisual();
            status = GetToolLabel(item) + " shelved";
            return true;
        }

        public bool TryTake(out MiniVanInventoryItem item, out string status)
        {
            item = StoredItem;
            if (item == MiniVanInventoryItem.None)
            {
                status = "Shelf empty";
                return false;
            }

            StoredItem = MiniVanInventoryItem.None;
            RefreshVisual();
            status = GetToolLabel(item) + " taken";
            return true;
        }

        public void SetStoredItem(MiniVanInventoryItem item)
        {
            StoredItem = CanStore(item) ? item : MiniVanInventoryItem.None;
            RefreshVisual();
        }

        private void EnsureCollider()
        {
            Collider existing = GetComponent<Collider>();
            if (existing != null)
            {
                existing.isTrigger = true;
                return;
            }

            BoxCollider box = gameObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(0.7f, 0.24f, 0.45f);
            box.center = new Vector3(0f, 0.08f, 0f);
        }

        private void RefreshVisual()
        {
            EnsureVisualRoot();
            for (int i = visualRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(visualRoot.GetChild(i).gameObject);
            }

            if (StoredItem == MiniVanInventoryItem.None)
            {
                return;
            }

            GameObject prefab = Resources.Load<GameObject>("PizzaLoop/PizzaItem_" + StoredItem);
            GameObject visual = prefab != null ? Instantiate(prefab, visualRoot) : GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Shelf Visual " + StoredItem;
            visual.transform.localPosition = VisualLocalOffset;
            visual.transform.localRotation = Quaternion.Euler(VisualLocalEuler);
            visual.transform.localScale = VisualLocalScale;

            MiniVanPizzaItem pizzaItem = visual.GetComponentInChildren<MiniVanPizzaItem>(true);
            if (pizzaItem != null)
            {
                pizzaItem.enabled = false;
            }

            Rigidbody[] bodies = visual.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < bodies.Length; i++)
            {
                bodies[i].isKinematic = true;
                bodies[i].detectCollisions = false;
            }

            Collider[] colliders = visual.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = false;
            }
        }

        private void EnsureVisualRoot()
        {
            if (visualRoot != null)
            {
                return;
            }

            Transform existing = transform.Find("ToolShelfVisual");
            if (existing != null)
            {
                visualRoot = existing;
                return;
            }

            GameObject root = new GameObject("ToolShelfVisual");
            root.transform.SetParent(transform, false);
            visualRoot = root.transform;
        }

        private static string GetToolLabel(MiniVanInventoryItem item)
        {
            return item == MiniVanInventoryItem.Grater ? "Grater" : "Rolling pin";
        }
    }
}
