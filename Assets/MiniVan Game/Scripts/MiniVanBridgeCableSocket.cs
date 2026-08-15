using UnityEngine;

namespace MiniVanGame
{
    public enum MiniVanBridgeCableSocketRole
    {
        Receiver,
        Mechanism
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class MiniVanBridgeCableSocket : MonoBehaviour, IMiniVanGameModeInteractable
    {
        public float InteractionReach = 1.15f;
        public float CableEndInsertReach = 0.78f;

        public MiniVanBridgeCableSocketRole Role;
        public Transform PlugPose;
        public Renderer IndicatorRenderer;
        public Material EmptyMaterial;
        public Material ConnectedMaterial;

        [Tooltip("When set, this socket only accepts the permanently-anchored end of that charger's cable.")]
        public MiniVanBatteryCharger OwnerCharger;

        public MiniVanBridgePowerCable ConnectedCable { get; private set; }
        public int ConnectedEndIndex { get; private set; } = -1;
        public bool IsConnected => ConnectedCable != null;

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

        public bool IsConnectedTo(MiniVanBridgeCableSocket other)
        {
            return other != null &&
                ConnectedCable != null &&
                ConnectedCable == other.ConnectedCable &&
                ConnectedEndIndex >= 0 &&
                other.ConnectedEndIndex >= 0 &&
                ConnectedEndIndex != other.ConnectedEndIndex;
        }

        public string GetPrompt(MiniVanPlayer player)
        {
            if (!IsPlayerInInteractionReach(player))
            {
                return string.Empty;
            }

            if (MiniVanBridgeBattery.GetCarriedBy(player) != null ||
                MiniVanCarBattery.GetCarriedBy(player) != null ||
                MiniVanBatteryCharger.GetCarriedBy(player) != null)
            {
                return string.Empty;
            }

            MiniVanBridgePowerCable.CarriedEnd carried = MiniVanBridgePowerCable.GetCarriedBy(player);
            if (carried.IsValid && !IsConnected)
            {
                if (OwnerCharger != null &&
                    (carried.Cable == null || !carried.Cable.IsEndPermanentlyAnchored(carried.EndIndex)))
                {
                    return string.Empty;
                }

                if (!IsCarriedEndInInsertReach(carried))
                {
                    return string.Empty;
                }

                return "E - insert cable";
            }

            if (IsConnected && !carried.IsValid)
            {
                if (ConnectedCable != null && ConnectedCable.IsEndPermanentlyAnchored(ConnectedEndIndex))
                {
                    return string.Empty;
                }

                return "E - unplug cable";
            }

            return string.Empty;
        }

        public void Interact(MiniVanPlayer player)
        {
            if (!IsPlayerInInteractionReach(player))
            {
                return;
            }

            MiniVanBridgePowerCable.CarriedEnd carried = MiniVanBridgePowerCable.GetCarriedBy(player);
            if (carried.IsValid && !IsConnected)
            {
                if (OwnerCharger != null &&
                    (carried.Cable == null || !carried.Cable.IsEndPermanentlyAnchored(carried.EndIndex)))
                {
                    return;
                }

                if (!IsCarriedEndInInsertReach(carried))
                {
                    return;
                }

                carried.Cable.ConnectEndToSocket(carried.EndIndex, this);
                return;
            }

            if (IsConnected && !carried.IsValid &&
                MiniVanBridgeBattery.GetCarriedBy(player) == null &&
                MiniVanCarBattery.GetCarriedBy(player) == null &&
                MiniVanBatteryCharger.GetCarriedBy(player) == null)
            {
                MiniVanBridgePowerCable cable = ConnectedCable;
                int endIndex = ConnectedEndIndex;
                if (cable != null && cable.IsEndPermanentlyAnchored(endIndex))
                {
                    return;
                }

                cable.DetachEndToPlayer(endIndex, player);
            }
        }

        public void PrimaryAction(MiniVanPlayer player)
        {
        }

        internal bool Attach(MiniVanBridgePowerCable cable, int endIndex)
        {
            if (cable == null || IsConnected)
            {
                return false;
            }

            ConnectedCable = cable;
            ConnectedEndIndex = endIndex;
            UpdateVisual();
            return true;
        }

        internal void ClearConnection(MiniVanBridgePowerCable cable, int endIndex)
        {
            if (ConnectedCable == cable && ConnectedEndIndex == endIndex)
            {
                ConnectedCable = null;
                ConnectedEndIndex = -1;
                UpdateVisual();
            }
        }

        public Vector3 GetPlugWorldPosition()
        {
            BoxCollider box = GetComponent<BoxCollider>();
            if (box != null)
            {
                return transform.TransformPoint(box.center);
            }

            return PlugPose != null ? PlugPose.position : transform.position;
        }

        public Quaternion GetPlugWorldRotation()
        {
            return PlugPose != null ? PlugPose.rotation : transform.rotation;
        }

        private bool IsPlayerInInteractionReach(MiniVanPlayer player)
        {
            return player != null &&
                Vector3.Distance(player.transform.position, GetPlugWorldPosition()) <= Mathf.Max(0.05f, InteractionReach);
        }

        private bool IsCarriedEndInInsertReach(MiniVanBridgePowerCable.CarriedEnd carried)
        {
            if (!carried.IsValid)
            {
                return false;
            }

            Vector3 endPosition = carried.Cable.GetEndWorldPosition(carried.EndIndex);
            return Vector3.Distance(endPosition, GetPlugWorldPosition()) <= Mathf.Max(0.05f, CableEndInsertReach);
        }

        private void UpdateVisual()
        {
            if (IndicatorRenderer == null)
            {
                return;
            }

            Material material = IsConnected ? ConnectedMaterial : EmptyMaterial;
            if (material != null && IndicatorRenderer.sharedMaterial != material)
            {
                IndicatorRenderer.sharedMaterial = material;
            }
        }
    }
}
