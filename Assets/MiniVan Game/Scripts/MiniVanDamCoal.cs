using System.Collections.Generic;
using UnityEngine;

namespace MiniVanGame
{
    /// <summary>
    /// Pickup coal chunk. Player carries one at a time (E to take, Q to drop),
    /// brings it to the boiler furnace. Carried physical object (not inventory).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class MiniVanDamCoal : MonoBehaviour, IMiniVanGameModeInteractable
    {
        private const float PickupReach = 2.2f;
        private const float CarryHeight = 0.6f;
        private const float CarryForward = 0.8f;

        private static readonly Dictionary<MiniVanPlayer, MiniVanDamCoal> carriedByPlayer =
            new Dictionary<MiniVanPlayer, MiniVanDamCoal>();

        private Rigidbody body;
        private Collider[] colliders;
        private MiniVanPlayer carrier;
        private bool consumed;

        public bool IsCarried => carrier != null;
        public bool IsConsumed => consumed;

        public static MiniVanDamCoal GetCarriedBy(MiniVanPlayer player)
        {
            return player != null && carriedByPlayer.TryGetValue(player, out MiniVanDamCoal coal)
                ? coal
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
            if (consumed || carrier == null)
            {
                return;
            }

            if (carrier == MiniVanPlayer.LocalPlayer && MiniVanKeyBindings.GetKeyDown(MiniVanKeyAction.Drop))
            {
                DropNear(carrier);
                return;
            }

            Vector3 handPosition = GetCarryPosition(carrier);
            transform.position = Vector3.Lerp(transform.position, handPosition, 1f - Mathf.Exp(-28f * Time.deltaTime));
            transform.rotation = Quaternion.Slerp(transform.rotation, carrier.transform.rotation, 1f - Mathf.Exp(-18f * Time.deltaTime));
        }

        public string GetPrompt(MiniVanPlayer player)
        {
            if (player == null || IsCarried || consumed)
            {
                return string.Empty;
            }

            return Vector3.Distance(player.transform.position, transform.position) <= PickupReach
                ? "E - take coal"
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
            if (player == null || IsCarried || consumed)
            {
                return false;
            }

            MiniVanDamCoal alreadyCarried = GetCarriedBy(player);
            if (alreadyCarried != null && alreadyCarried != this)
            {
                return false;
            }

            if (Vector3.Distance(player.transform.position, transform.position) > PickupReach)
            {
                return false;
            }

            carriedByPlayer[player] = this;
            carrier = player;
            transform.SetParent(null, true);
            ConfigureCarriedBody();
            return true;
        }

        /// <summary>Called by the furnace when the coal is loaded. Removes from world.</summary>
        public void Consume()
        {
            if (carrier != null)
            {
                carriedByPlayer.Remove(carrier);
                carrier = null;
            }
            consumed = true;
            gameObject.SetActive(false);
        }

        public void DropNear(MiniVanPlayer player)
        {
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

        private void ConfigureCarriedBody()
        {
            body.isKinematic = true;
            body.useGravity = false;
            body.detectCollisions = false;
            SetCollidersEnabled(false);
        }

        private void ConfigureFreeBody()
        {
            body.mass = 3f;
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
            Vector3 forward = cameraRoot.forward.sqrMagnitude > 0.001f ? cameraRoot.forward.normalized : player.transform.forward;
            return player.transform.position + Vector3.up * CarryHeight + forward * CarryForward;
        }
    }
}
