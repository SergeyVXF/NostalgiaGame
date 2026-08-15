using UnityEngine;

namespace MiniVanGame
{
    public class MiniVanPizzaBoxStation : MonoBehaviour
    {
        public float InteractRadius = 2.2f;
        public bool HasBox = true;

        private void Awake()
        {
            EnsureCollider();
        }

        public bool IsInRange(Vector3 worldPosition)
        {
            return Vector3.Distance(worldPosition, transform.position) <= InteractRadius;
        }

        public bool TryUseBox()
        {
            if (!HasBox)
            {
                return false;
            }

            HasBox = false;
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in renderers)
            {
                if (renderer != null)
                {
                    renderer.enabled = false;
                }
            }

            return true;
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
            box.size = new Vector3(1.1f, 0.45f, 1.1f);
        }
    }
}
