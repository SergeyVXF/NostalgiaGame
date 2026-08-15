using UnityEngine;

namespace MiniVanGame
{
    [DisallowMultipleComponent]
    public sealed class MiniVanPanelkaDoorCollisionProxy : MonoBehaviour,
        IMiniVanGameModeInteractable
    {
        public MiniVanPanelkaRoomDoor Owner;
        public Renderer PanelRenderer;
        public bool ForwardOnly;

        private BoxCollider box;

        public void ConfigureForwardOnly(MiniVanPanelkaRoomDoor owner)
        {
            Owner = owner;
            PanelRenderer = null;
            ForwardOnly = true;
            box = GetComponent<BoxCollider>();
            if (box != null)
            {
                box.enabled = false;
            }
        }

        public void Configure(MiniVanPanelkaRoomDoor owner, Renderer panelRenderer)
        {
            Owner = owner;
            PanelRenderer = panelRenderer;
            ForwardOnly = false;
            EnsureCollider();
            RefreshProxy();
        }

        private void Awake()
        {
            if (ForwardOnly)
            {
                box = GetComponent<BoxCollider>();
                if (box != null)
                {
                    box.enabled = false;
                }
                return;
            }

            EnsureCollider();
            RefreshProxy();
        }

        private void LateUpdate()
        {
            if (ForwardOnly)
            {
                return;
            }

            RefreshProxy();
        }

        public string GetPrompt(MiniVanPlayer player)
        {
            return Owner != null ? Owner.GetPrompt(player) : string.Empty;
        }

        public void Interact(MiniVanPlayer player)
        {
            if (Owner != null)
            {
                Owner.Interact(player);
            }
        }

        public void PrimaryAction(MiniVanPlayer player)
        {
            if (Owner != null)
            {
                Owner.PrimaryAction(player);
            }
        }

        private void EnsureCollider()
        {
            if (box == null)
            {
                box = GetComponent<BoxCollider>();
            }

            if (box == null)
            {
                box = gameObject.AddComponent<BoxCollider>();
            }

            box.isTrigger = false;
            box.enabled = true;
        }

        private void RefreshProxy()
        {
            if (PanelRenderer == null)
            {
                return;
            }

            EnsureCollider();
            Bounds localBounds = PanelRenderer.localBounds;
            if (PanelRenderer.transform == transform)
            {
                box.center = localBounds.center;
                box.size = new Vector3(
                    Mathf.Max(0.03f, localBounds.size.x),
                    Mathf.Max(0.03f, localBounds.size.y),
                    Mathf.Max(0.03f, localBounds.size.z));
                return;
            }

            Vector3 scale = PanelRenderer.transform.lossyScale;
            transform.position = PanelRenderer.transform.TransformPoint(localBounds.center);
            transform.rotation = PanelRenderer.transform.rotation;
            transform.localScale = Vector3.one;
            box.center = Vector3.zero;
            box.size = new Vector3(
                Mathf.Max(0.03f, Mathf.Abs(scale.x) * localBounds.size.x),
                Mathf.Max(0.03f, Mathf.Abs(scale.y) * localBounds.size.y),
                Mathf.Max(0.03f, Mathf.Abs(scale.z) * localBounds.size.z));
        }
    }
}
