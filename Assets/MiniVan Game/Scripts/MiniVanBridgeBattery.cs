using System.Collections.Generic;
using UnityEngine;

namespace MiniVanGame
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class MiniVanBridgeBattery : MonoBehaviour, IMiniVanGameModeInteractable
    {
        private const float PickupReach = 2.2f;
        private static readonly Vector3 CarryLocalPosition = new Vector3(0.32f, -0.38f, 0.78f);
        private static readonly Vector3 CarryLocalEuler = new Vector3(8f, 0f, 0f);
        private static readonly Vector3 RidingCarryLocalPosition = new Vector3(0.48f, 1.05f, 0.1f);
        private static readonly Vector3 RidingCarryLocalEuler = new Vector3(0f, 90f, 0f);

        private static readonly Dictionary<MiniVanPlayer, MiniVanBridgeBattery> carriedByPlayer =
            new Dictionary<MiniVanPlayer, MiniVanBridgeBattery>();

        private Rigidbody body;
        private Collider[] colliders;
        private MiniVanPlayer carrier;
        private MiniVanBridgeBatteryReceiver installedReceiver;

        public bool IsCarried => carrier != null;
        public bool IsInstalled => installedReceiver != null;

        public static MiniVanBridgeBattery GetCarriedBy(MiniVanPlayer player)
        {
            return player != null && carriedByPlayer.TryGetValue(player, out MiniVanBridgeBattery battery)
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
                ? "E - take battery"
                : string.Empty;
        }

        public void Interact(MiniVanPlayer player)
        {
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

            MiniVanBridgeBattery alreadyCarried = GetCarriedBy(player);
            if (alreadyCarried != null && alreadyCarried != this)
            {
                return false;
            }

            if (MiniVanBridgePowerCable.HasCarriedEnd(player))
            {
                return false;
            }

            if (!IsInstalled && Vector3.Distance(player.transform.position, transform.position) > PickupReach)
            {
                return false;
            }

            if (installedReceiver != null)
            {
                installedReceiver.DetachBattery(this);
                installedReceiver = null;
            }

            carriedByPlayer[player] = this;
            carrier = player;
            transform.SetParent(null, true);
            ConfigureCarriedBody();
            return true;
        }

        public bool PlaceInto(MiniVanBridgeBatteryReceiver receiver)
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

            installedReceiver = receiver;
            Transform socket = receiver.BatterySocket != null ? receiver.BatterySocket : receiver.transform;
            transform.SetParent(socket, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            ConfigureInstalledBody();
            return true;
        }

        public void DropNear(MiniVanPlayer player)
        {
            if (installedReceiver != null)
            {
                MiniVanBridgeBatteryReceiver receiver = installedReceiver;
                installedReceiver = null;
                receiver.DetachBattery(this);
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

        internal void ClearInstalledReceiver(MiniVanBridgeBatteryReceiver receiver)
        {
            if (installedReceiver == receiver)
            {
                installedReceiver = null;
            }
        }

        private void ConfigureCarriedBody()
        {
            if (body == null)
            {
                return;
            }

            body.isKinematic = true;
            body.useGravity = false;
            SetCollidersEnabled(false);
        }

        private void ConfigureInstalledBody()
        {
            if (body == null)
            {
                return;
            }

            body.isKinematic = true;
            body.useGravity = false;
            SetCollidersEnabled(true);
        }

        private void ConfigureFreeBody()
        {
            if (body == null)
            {
                return;
            }

            body.mass = 6f;
            body.linearDamping = 0.25f;
            body.angularDamping = 0.5f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.isKinematic = false;
            body.useGravity = true;
            SetCollidersEnabled(true);
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
