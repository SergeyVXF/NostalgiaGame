using UnityEngine;

namespace MiniVanGame
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class MiniVanBridgeBatteryReceiver : MonoBehaviour, IMiniVanGameModeInteractable
    {
        private const float InteractionReach = 2.35f;

        public Transform BatterySocket;
        public MiniVanBridgeBattery InstalledBattery;
        public MiniVanBridgeCableSocket PowerSocket;
        public Renderer IndicatorRenderer;
        public Material EmptyMaterial;
        public Material PoweredMaterial;

        public bool HasBattery => InstalledBattery != null && InstalledBattery.IsInstalled;

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

            if (MiniVanBridgeBattery.GetCarriedBy(player) != null && !HasBattery)
            {
                return "E - insert battery";
            }

            return HasBattery ? "E - remove battery" : "Battery receiver empty";
        }

        public void Interact(MiniVanPlayer player)
        {
            if (player == null || Vector3.Distance(player.transform.position, transform.position) > InteractionReach)
            {
                return;
            }

            ClearStaleInstalledBattery();
            MiniVanBridgeBattery carried = MiniVanBridgeBattery.GetCarriedBy(player);
            if (carried != null && !HasBattery)
            {
                AttachBattery(carried);
                return;
            }

            if (InstalledBattery != null)
            {
                InstalledBattery.TryPickup(player);
            }
        }

        public void PrimaryAction(MiniVanPlayer player)
        {
        }

        public bool AttachBattery(MiniVanBridgeBattery battery)
        {
            ClearStaleInstalledBattery();
            if (battery == null || HasBattery)
            {
                return false;
            }

            InstalledBattery = battery;
            battery.PlaceInto(this);
            UpdateVisual();
            return true;
        }

        public void DetachBattery(MiniVanBridgeBattery battery)
        {
            if (InstalledBattery == battery)
            {
                InstalledBattery = null;
                battery.ClearInstalledReceiver(this);
                UpdateVisual();
            }
        }

        private void ClearStaleInstalledBattery()
        {
            if (InstalledBattery != null && !InstalledBattery.IsInstalled)
            {
                InstalledBattery = null;
            }
        }

        private void UpdateVisual()
        {
            if (IndicatorRenderer == null)
            {
                return;
            }

            Material material = HasBattery ? PoweredMaterial : EmptyMaterial;
            if (material != null && IndicatorRenderer.sharedMaterial != material)
            {
                IndicatorRenderer.sharedMaterial = material;
            }
        }
    }
}
