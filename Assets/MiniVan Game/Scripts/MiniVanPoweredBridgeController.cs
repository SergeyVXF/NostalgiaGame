using UnityEngine;

namespace MiniVanGame
{
    [DisallowMultipleComponent]
    public sealed class MiniVanPoweredBridgeController : MonoBehaviour
    {
        [Tooltip("Legacy rotating transform. Used when CustomRotationPivot is not assigned.")]
        public Transform BridgePivot;
        [Tooltip("Optional empty object used as the bridge rotation pivot. Put bridge geometry under this object, then assign it here.")]
        public Transform CustomRotationPivot;
        public MiniVanBridgeBatteryReceiver Receiver;
        public MiniVanBridgeCableSocket ReceiverPowerSocket;
        public MiniVanBridgeCableSocket MechanismPowerSocket;
        public Vector3 RaisedLocalEuler;
        public Vector3 LoweredLocalEuler;
        public float MoveSpeedDegrees = 42f;
        public bool StartLowered;

        private bool targetLowered;
        private Quaternion customPivotBaseLocalRotation;
        private Transform cachedBridgePivot;
        private Transform cachedCustomRotationPivot;
        private Transform RotationPivot => CustomRotationPivot != null ? CustomRotationPivot : BridgePivot;

        public bool IsPowered =>
            Receiver != null &&
            Receiver.HasBattery &&
            ReceiverPowerSocket != null &&
            MechanismPowerSocket != null &&
            ReceiverPowerSocket.IsConnectedTo(MechanismPowerSocket);
        public bool IsLowered => targetLowered;

        private void Awake()
        {
            CacheCustomPivotPose();
            targetLowered = StartLowered;

            Transform pivot = RotationPivot;
            if (pivot != null)
            {
                pivot.localRotation = GetTargetLocalRotation();
            }
        }

        private void Update()
        {
            EnsurePivotCache();

            Transform pivot = RotationPivot;
            if (pivot == null)
            {
                return;
            }

            Quaternion target = GetTargetLocalRotation();
            pivot.localRotation = Quaternion.RotateTowards(
                pivot.localRotation,
                target,
                MoveSpeedDegrees * Time.deltaTime);
        }

        public bool ToggleBridge()
        {
            if (!IsPowered)
            {
                return false;
            }

            targetLowered = !targetLowered;
            return true;
        }

        private void CacheCustomPivotPose()
        {
            cachedBridgePivot = BridgePivot;
            cachedCustomRotationPivot = CustomRotationPivot;
            AttachBridgeToCustomPivot();
            customPivotBaseLocalRotation = CustomRotationPivot != null ? CustomRotationPivot.localRotation : Quaternion.identity;
        }

        private void EnsurePivotCache()
        {
            if (cachedBridgePivot == BridgePivot && cachedCustomRotationPivot == CustomRotationPivot)
            {
                return;
            }

            CacheCustomPivotPose();
        }

        private Quaternion GetTargetLocalRotation()
        {
            Quaternion target = Quaternion.Euler(targetLowered ? LoweredLocalEuler : RaisedLocalEuler);
            return CustomRotationPivot != null ? customPivotBaseLocalRotation * target : target;
        }

        private void AttachBridgeToCustomPivot()
        {
            if (CustomRotationPivot == null ||
                BridgePivot == null ||
                BridgePivot == CustomRotationPivot ||
                BridgePivot.IsChildOf(CustomRotationPivot))
            {
                return;
            }

            BridgePivot.SetParent(CustomRotationPivot, true);
        }
    }
}
