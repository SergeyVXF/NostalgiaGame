using UnityEngine;

namespace MiniVanGame
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class MiniVanPoweredBridgePanel : MonoBehaviour, IMiniVanGameModeInteractable
    {
        private const float InteractionReach = 2.45f;

        public MiniVanPoweredBridgeController BridgeController;
        public MiniVanBridgeCableSocket PowerSocket;
        public Renderer ButtonRenderer;
        public Renderer ModeLightRenderer;
        public Material ButtonOffMaterial;
        public Material ButtonOnMaterial;

        private void Awake()
        {
            BoxCollider box = GetComponent<BoxCollider>();
            box.isTrigger = true;
            UpdateVisual();
        }

        private void Update()
        {
            UpdateVisual();
        }

        public string GetPrompt(MiniVanPlayer player)
        {
            if (player == null || Vector3.Distance(player.transform.position, transform.position) > InteractionReach)
            {
                return string.Empty;
            }

            if (BridgeController == null || !BridgeController.IsPowered)
            {
                return "No battery power";
            }

            return BridgeController.IsLowered ? "E - raise bridge" : "E - lower bridge";
        }

        public void Interact(MiniVanPlayer player)
        {
            if (player == null || BridgeController == null ||
                Vector3.Distance(player.transform.position, transform.position) > InteractionReach)
            {
                return;
            }

            BridgeController.ToggleBridge();
            UpdateVisual();
        }

        public void PrimaryAction(MiniVanPlayer player)
        {
        }

        private void UpdateVisual()
        {
            bool powered = BridgeController != null && BridgeController.IsPowered;
            Material material = powered ? ButtonOnMaterial : ButtonOffMaterial;
            if (material == null)
            {
                return;
            }

            if (ButtonRenderer != null && ButtonRenderer.sharedMaterial != material)
            {
                ButtonRenderer.sharedMaterial = material;
            }

            if (ModeLightRenderer != null && ModeLightRenderer.sharedMaterial != material)
            {
                ModeLightRenderer.sharedMaterial = material;
            }
        }
    }
}
