using System.Collections.Generic;
using UnityEngine;

namespace MiniVanGame
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class MiniVanCarBattery : MonoBehaviour, IMiniVanGameModeInteractable
    {
        private const float PickupReach = 2.2f;
        // First-person: slightly right of crosshair so it doesn't float with lag.
        private static readonly Vector3 CarryLocalPosition = new Vector3(0.32f, -0.38f, 0.78f);
        private static readonly Vector3 CarryLocalEuler = new Vector3(8f, 0f, 0f);
        // Riding / third-person: right side of the body.
        private static readonly Vector3 RidingCarryLocalPosition = new Vector3(0.48f, 1.05f, 0.1f);
        private static readonly Vector3 RidingCarryLocalEuler = new Vector3(0f, 90f, 0f);

        private static readonly Dictionary<MiniVanPlayer, MiniVanCarBattery> carriedByPlayer =
            new Dictionary<MiniVanPlayer, MiniVanCarBattery>();

        public float Charge01 = 1f;

        private Rigidbody body;
        private Collider[] colliders;
        private MiniVanPlayer carrier;
        private MiniVanCarBatteryReceiver installedReceiver;
        private MiniVanBatteryCharger installedCharger;

        public bool IsCarried => carrier != null;
        public bool IsInstalled => installedReceiver != null || installedCharger != null;

        public bool IsInstalledOnCharger(MiniVanBatteryCharger charger)
        {
            return charger != null && installedCharger == charger;
        }

        public static MiniVanCarBattery GetCarriedBy(MiniVanPlayer player)
        {
            return player != null && carriedByPlayer.TryGetValue(player, out MiniVanCarBattery battery)
                ? battery
                : null;
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            colliders = GetComponentsInChildren<Collider>(true);
            ConfigureFreeBody();
        }

        private void LateUpdate()
        {
            if (installedReceiver != null)
            {
                SnapToInstalledSocket();
                return;
            }

            if (installedCharger != null)
            {
                SnapToCharger(installedCharger);
                return;
            }

            if (carrier == null)
            {
                return;
            }

            // While riding, Q is tap-drop / hold-exit and handled by the player itself.
            if (carrier == MiniVanPlayer.LocalPlayer && !carrier.IsRidingBoard &&
                MiniVanKeyBindings.GetKeyDown(MiniVanKeyAction.Drop))
            {
                DropNear(carrier);
                return;
            }

            ApplyCarryPose(carrier);
        }

        public string GetPrompt(MiniVanPlayer player)
        {
            if (player == null || IsCarried || IsInstalled)
            {
                return string.Empty;
            }

            return Vector3.Distance(player.transform.position, transform.position) <= PickupReach
                ? "E - take car battery"
                : string.Empty;
        }

        public void Interact(MiniVanPlayer player)
        {
            if (Input.GetMouseButton(1))
            {
                return;
            }

            TryPickup(player);
        }

        public void PrimaryAction(MiniVanPlayer player)
        {
        }

        public bool TryPickup(MiniVanPlayer player)
        {
            if (player == null || IsCarried)
            {
                return false;
            }

            MiniVanCarBattery alreadyCarried = GetCarriedBy(player);
            if (alreadyCarried != null && alreadyCarried != this)
            {
                return false;
            }

            if (MiniVanBridgeBattery.GetCarriedBy(player) != null ||
                MiniVanBridgePowerCable.HasCarriedEnd(player) ||
                MiniVanBatteryCharger.GetCarriedBy(player) != null)
            {
                return false;
            }

            if (!IsInstalled && Vector3.Distance(player.transform.position, transform.position) > PickupReach)
            {
                return false;
            }

            if (installedReceiver != null)
            {
                if (!installedReceiver.CanPlayerAccess(player))
                {
                    return false;
                }

                MiniVanCarBatteryReceiver receiver = installedReceiver;
                installedReceiver = null;
                receiver.DetachBattery(this);
            }

            if (installedCharger != null)
            {
                MiniVanBatteryCharger charger = installedCharger;
                installedCharger = null;
                charger.DetachBattery(this);
            }

            carriedByPlayer[player] = this;
            carrier = player;
            transform.SetParent(null, true);
            ConfigureCarriedBody();
            return true;
        }

        public bool PlaceIntoCharger(MiniVanBatteryCharger charger)
        {
            if (charger == null)
            {
                return false;
            }

            if (carrier != null)
            {
                carriedByPlayer.Remove(carrier);
                carrier = null;
            }

            if (installedReceiver != null)
            {
                MiniVanCarBatteryReceiver receiver = installedReceiver;
                installedReceiver = null;
                receiver.DetachBattery(this);
            }

            installedCharger = charger;
            Transform socket = charger.GetBatterySocket();
            transform.SetParent(socket, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.SetPositionAndRotation(socket.position, socket.rotation);
            if (body != null)
            {
                if (!body.isKinematic)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }

                body.position = socket.position;
                body.rotation = socket.rotation;
            }

            ConfigureInstalledBody();
            SnapToCharger(charger);
            body?.Sleep();
            return true;
        }

        public void SnapToCharger(MiniVanBatteryCharger charger)
        {
            if (charger == null || installedCharger != charger)
            {
                return;
            }

            Transform socket = charger.GetBatterySocket();
            if (transform.parent != socket)
            {
                transform.SetParent(socket, false);
            }

            transform.SetPositionAndRotation(socket.position, socket.rotation);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            if (body != null)
            {
                body.position = socket.position;
                body.rotation = socket.rotation;
                body.Sleep();
            }
        }

        public bool PlaceInto(MiniVanCarBatteryReceiver receiver)
        {
            if (receiver == null)
            {
                return false;
            }

            if (carrier != null)
            {
                carriedByPlayer.Remove(carrier);
                carrier = null;
            }

            if (installedCharger != null)
            {
                MiniVanBatteryCharger charger = installedCharger;
                installedCharger = null;
                charger.DetachBattery(this);
            }

            installedReceiver = receiver;
            Transform socket = receiver.PlacementPoint != null ? receiver.PlacementPoint : receiver.transform;
            transform.SetParent(socket, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.SetPositionAndRotation(socket.position, socket.rotation);
            if (body != null)
            {
                if (!body.isKinematic)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
                body.position = socket.position;
                body.rotation = socket.rotation;
            }
            ConfigureInstalledBody();
            SnapToInstalledSocket();
            body?.Sleep();
            return true;
        }

        private void SnapToInstalledSocket()
        {
            if (installedReceiver == null)
            {
                return;
            }

            Transform socket = installedReceiver.PlacementPoint != null
                ? installedReceiver.PlacementPoint
                : installedReceiver.transform;
            if (transform.parent != socket)
            {
                transform.SetParent(socket, false);
            }

            transform.SetPositionAndRotation(socket.position, socket.rotation);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            if (body != null)
            {
                body.position = socket.position;
                body.rotation = socket.rotation;
                body.Sleep();
            }
        }

        public void DropNear(MiniVanPlayer player)
        {
            if (installedReceiver != null)
            {
                MiniVanCarBatteryReceiver receiver = installedReceiver;
                installedReceiver = null;
                receiver.DetachBattery(this);
            }

            if (installedCharger != null)
            {
                MiniVanBatteryCharger charger = installedCharger;
                installedCharger = null;
                charger.DetachBattery(this);
            }

            if (carrier != null)
            {
                carriedByPlayer.Remove(carrier);
                carrier = null;
            }

            transform.SetParent(null, true);
            if (player != null)
            {
                transform.position = player.transform.position + player.transform.forward * 0.9f + Vector3.up * 0.35f;
            }

            ConfigureFreeBody();
        }

        internal void ClearInstalledReceiver(MiniVanCarBatteryReceiver receiver)
        {
            if (installedReceiver == receiver)
            {
                installedReceiver = null;
            }
        }

        internal void ClearInstalledCharger(MiniVanBatteryCharger charger)
        {
            if (installedCharger == charger)
            {
                installedCharger = null;
            }
        }

        private void ConfigureCarriedBody()
        {
            body.isKinematic = true;
            body.useGravity = false;
            body.detectCollisions = false;
            SetCollidersEnabled(false);
        }

        private void ConfigureInstalledBody()
        {
            body.isKinematic = true;
            body.useGravity = false;
            body.detectCollisions = true;
            SetCollidersEnabled(true);
            SetCollidersTrigger(true);
        }

        private void ConfigureFreeBody()
        {
            body.mass = 18f;
            body.linearDamping = 0.35f;
            body.angularDamping = 0.7f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.isKinematic = false;
            body.useGravity = true;
            body.detectCollisions = true;
            SetCollidersEnabled(true);
            SetCollidersTrigger(false);
        }

        private void SetCollidersEnabled(bool enabled)
        {
            if (colliders == null)
            {
                return;
            }

            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                {
                    colliders[i].enabled = enabled;
                }
            }
        }

        private void SetCollidersTrigger(bool isTrigger)
        {
            if (colliders == null)
            {
                return;
            }

            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                {
                    colliders[i].isTrigger = isTrigger;
                }
            }
        }

        private void ApplyCarryPose(MiniVanPlayer player)
        {
            if (player == null)
            {
                return;
            }

            if (player.IsRidingBoard)
            {
                Vector3 sidePosition = player.transform.TransformPoint(RidingCarryLocalPosition);
                Quaternion sideRotation = player.transform.rotation * Quaternion.Euler(RidingCarryLocalEuler);
                transform.SetPositionAndRotation(sidePosition, sideRotation);
                return;
            }

            Transform cam = player.PlayerCamera != null
                ? player.PlayerCamera.transform
                : (player.CameraRoot != null ? player.CameraRoot : player.transform);
            Vector3 position = cam.TransformPoint(CarryLocalPosition);
            Quaternion rotation = cam.rotation * Quaternion.Euler(CarryLocalEuler);
            transform.SetPositionAndRotation(position, rotation);
        }
    }
}
