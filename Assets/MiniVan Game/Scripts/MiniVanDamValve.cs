using System.Collections.Generic;
using UnityEngine;

namespace MiniVanGame
{
    /// <summary>
    /// Pickup valve wheel. Player carries it (E to take, Q to drop),
    /// then inserts it into a MiniVanDamValveSocket.
    /// Pattern follows MiniVanCarBattery (carried physical object, not inventory).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class MiniVanDamValve : MonoBehaviour, IMiniVanGameModeInteractable
    {
        private const float PickupReach = 2.2f;
        private const float CarryHeight = 0.42f;
        private const float CarryForward = 0.55f;
        private const float CarryRight = 0.38f;

        private static readonly Dictionary<MiniVanPlayer, MiniVanDamValve> carriedByPlayer =
            new Dictionary<MiniVanPlayer, MiniVanDamValve>();

        private Rigidbody body;
        private Collider[] colliders;
        private MiniVanPlayer carrier;
        private MiniVanDamValveSocket installedSocket;

        public bool IsCarried => carrier != null;
        public bool IsInstalled => installedSocket != null;

        public static MiniVanDamValve GetCarriedBy(MiniVanPlayer player)
        {
            return player != null && carriedByPlayer.TryGetValue(player, out MiniVanDamValve valve)
                ? valve
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
            if (installedSocket != null)
            {
                SnapToInstalledSocket();
                return;
            }

            if (carrier == null)
            {
                return;
            }

            if (carrier == MiniVanPlayer.LocalPlayer && MiniVanKeyBindings.GetKeyDown(MiniVanKeyAction.Drop))
            {
                DropNear(carrier);
                return;
            }

            Vector3 handPosition = GetCarryPosition(carrier);
            Quaternion handRotation = GetCarryRotation(carrier);
            transform.SetPositionAndRotation(handPosition, handRotation);
            if (body != null)
            {
                body.position = handPosition;
                body.rotation = handRotation;
            }
        }

        public string GetPrompt(MiniVanPlayer player)
        {
            if (player == null || IsCarried || IsInstalled)
            {
                return string.Empty;
            }

            return Vector3.Distance(player.transform.position, transform.position) <= PickupReach
                ? "E - take valve"
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

            MiniVanDamValve alreadyCarried = GetCarriedBy(player);
            if (alreadyCarried != null && alreadyCarried != this)
            {
                return false;
            }

            if (!IsInstalled && Vector3.Distance(player.transform.position, transform.position) > PickupReach)
            {
                return false;
            }

            if (installedSocket != null)
            {
                MiniVanDamValveSocket socket = installedSocket;
                installedSocket = null;
                socket.DetachValve(this);
            }

            carriedByPlayer[player] = this;
            carrier = player;
            transform.SetParent(null, true);
            ConfigureCarriedBody();
            return true;
        }

        public bool PlaceInto(MiniVanDamValveSocket socket)
        {
            if (socket == null)
            {
                return false;
            }

            if (carrier != null)
            {
                carriedByPlayer.Remove(carrier);
                carrier = null;
            }

            installedSocket = socket;
            Transform point = socket.PlacementPoint != null ? socket.PlacementPoint : socket.transform;
            transform.SetParent(point, false);
            transform.localPosition = Vector3.zero;
            // Handwheel lies flat on the spindle (ring faces up).
            transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            transform.SetPositionAndRotation(point.position, point.rotation * Quaternion.Euler(90f, 0f, 0f));
            if (body != null)
            {
                if (!body.isKinematic)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
                body.position = transform.position;
                body.rotation = transform.rotation;
            }
            ConfigureInstalledBody();
            SnapToInstalledSocket();
            body?.Sleep();
            return true;
        }

        private void SnapToInstalledSocket()
        {
            if (installedSocket == null)
            {
                return;
            }

            Transform point = installedSocket.PlacementPoint != null
                ? installedSocket.PlacementPoint
                : installedSocket.transform;
            if (transform.parent != point)
            {
                transform.SetParent(point, false);
            }

            transform.SetPositionAndRotation(point.position, point.rotation * Quaternion.Euler(90f, 0f, 0f));
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            if (body != null)
            {
                body.position = transform.position;
                body.rotation = transform.rotation;
                body.Sleep();
            }
        }

        public void DropNear(MiniVanPlayer player)
        {
            if (installedSocket != null)
            {
                MiniVanDamValveSocket socket = installedSocket;
                installedSocket = null;
                socket.DetachValve(this);
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

        internal void ClearInstalledSocket(MiniVanDamValveSocket socket)
        {
            if (installedSocket == socket)
            {
                installedSocket = null;
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
            body.mass = 6f;
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
            if (colliders == null) return;
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null) colliders[i].enabled = enabled;
            }
        }

        private void SetCollidersTrigger(bool isTrigger)
        {
            if (colliders == null) return;
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null) colliders[i].isTrigger = isTrigger;
            }
        }

        private static Vector3 GetCarryPosition(MiniVanPlayer player)
        {
            Transform cameraRoot = player.CameraRoot != null ? player.CameraRoot : player.transform;
            Vector3 forward = cameraRoot.forward.sqrMagnitude > 0.001f
                ? Vector3.ProjectOnPlane(cameraRoot.forward, Vector3.up).normalized
                : player.transform.forward;
            if (forward.sqrMagnitude < 0.001f)
            {
                forward = player.transform.forward;
            }

            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            if (right.sqrMagnitude < 0.001f)
            {
                right = player.transform.right;
            }

            return player.transform.position
                   + Vector3.up * CarryHeight
                   + forward * CarryForward
                   + right * CarryRight;
        }

        private static Quaternion GetCarryRotation(MiniVanPlayer player)
        {
            Transform cameraRoot = player.CameraRoot != null ? player.CameraRoot : player.transform;
            Vector3 forward = Vector3.ProjectOnPlane(cameraRoot.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.001f)
            {
                forward = player.transform.forward;
            }

            // Wheel faces the player slightly, held like in the right hand.
            return Quaternion.LookRotation(forward.normalized, Vector3.up) * Quaternion.Euler(75f, -20f, 15f);
        }
    }
}
