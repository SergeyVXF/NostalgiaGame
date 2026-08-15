using System.Collections.Generic;
using UnityEngine;

namespace MiniVanGame
{
    [DisallowMultipleComponent]
    public sealed class MiniVanBridgePowerCable : MonoBehaviour
    {
        private const float PickupReach = 3.2f;
        private const float CarryHeight = 0.66f;
        private const float CarryForward = 0.92f;

        private static readonly Dictionary<MiniVanPlayer, CarriedEnd> carriedByPlayer =
            new Dictionary<MiniVanPlayer, CarriedEnd>();

        public Transform EndA;
        public Transform EndB;
        public MiniVanBridgeCableVisual CableVisual;

        [Tooltip("0 or 1 locks that end so it can never be unplugged or carried. -1 = both ends free.")]
        public int PermanentlyAnchoredEndIndex = -1;

        private readonly MiniVanBridgeCableSocket[] sockets = new MiniVanBridgeCableSocket[2];
        private readonly Rigidbody[] endBodies = new Rigidbody[2];
        private readonly Collider[][] endColliders = new Collider[2][];

        public struct CarriedEnd
        {
            public MiniVanBridgePowerCable Cable;
            public int EndIndex;
            public bool IsValid => Cable != null && EndIndex >= 0;
        }

        public static CarriedEnd GetCarriedBy(MiniVanPlayer player)
        {
            if (player != null && carriedByPlayer.TryGetValue(player, out CarriedEnd carried))
            {
                return carried;
            }

            return default;
        }

        public static bool HasCarriedEnd(MiniVanPlayer player)
        {
            return GetCarriedBy(player).IsValid;
        }

        public static bool DropCarriedEndFor(MiniVanPlayer player)
        {
            CarriedEnd carried = GetCarriedBy(player);
            if (!carried.IsValid)
            {
                return false;
            }

            carried.Cable.DropEnd(carried.EndIndex, player);
            return true;
        }

        private void Awake()
        {
            CacheEndParts();
            if (CableVisual != null)
            {
                CableVisual.StartPoint = EndA;
                CableVisual.EndPoint = EndB;
            }
        }

        private void Update()
        {
            MiniVanPlayer player = MiniVanPlayer.LocalPlayer;
            CarriedEnd carried = GetCarriedBy(player);
            if (carried.Cable == this && MiniVanKeyBindings.GetKeyDown(MiniVanKeyAction.Drop))
            {
                DropEnd(carried.EndIndex, player);
            }
        }

        private void LateUpdate()
        {
            foreach (KeyValuePair<MiniVanPlayer, CarriedEnd> pair in carriedByPlayer)
            {
                if (pair.Value.Cable != this)
                {
                    continue;
                }

                Transform end = GetEnd(pair.Value.EndIndex);
                if (end == null || pair.Key == null)
                {
                    continue;
                }

                Vector3 handPosition = GetClampedCarryPosition(pair.Value.EndIndex, GetCarryPosition(pair.Key));
                end.position = Vector3.Lerp(end.position, handPosition, 1f - Mathf.Exp(-32f * Time.deltaTime));
                end.rotation = Quaternion.Slerp(end.rotation, pair.Key.transform.rotation, 1f - Mathf.Exp(-22f * Time.deltaTime));
            }
        }

        private void OnDisable()
        {
            List<MiniVanPlayer> toClear = new List<MiniVanPlayer>();
            foreach (KeyValuePair<MiniVanPlayer, CarriedEnd> pair in carriedByPlayer)
            {
                if (pair.Value.Cable == this)
                {
                    toClear.Add(pair.Key);
                }
            }

            for (int i = 0; i < toClear.Count; i++)
            {
                carriedByPlayer.Remove(toClear[i]);
            }
        }

        public bool IsEndPermanentlyAnchored(int endIndex)
        {
            return PermanentlyAnchoredEndIndex >= 0 && PermanentlyAnchoredEndIndex == endIndex;
        }

        public string GetEndPrompt(int endIndex, MiniVanPlayer player)
        {
            if (IsEndPermanentlyAnchored(endIndex))
            {
                return string.Empty;
            }

            Transform end = GetEnd(endIndex);
            if (player == null || end == null || Vector3.Distance(player.transform.position, end.position) > PickupReach)
            {
                return string.Empty;
            }

            CarriedEnd carried = GetCarriedBy(player);
            if (carried.IsValid)
            {
                return carried.Cable == this && carried.EndIndex == endIndex ? "Q - drop cable" : string.Empty;
            }

            if (MiniVanBridgeBattery.GetCarriedBy(player) != null ||
                MiniVanCarBattery.GetCarriedBy(player) != null ||
                MiniVanBatteryCharger.GetCarriedBy(player) != null)
            {
                return string.Empty;
            }

            return sockets[endIndex] != null ? "E - unplug cable" : "E - take cable end";
        }

        public void InteractEnd(int endIndex, MiniVanPlayer player)
        {
            if (player == null || IsEndPermanentlyAnchored(endIndex))
            {
                return;
            }

            if (GetCarriedBy(player).IsValid ||
                MiniVanBridgeBattery.GetCarriedBy(player) != null ||
                MiniVanCarBattery.GetCarriedBy(player) != null ||
                MiniVanBatteryCharger.GetCarriedBy(player) != null)
            {
                return;
            }

            Transform end = GetEnd(endIndex);
            if (end == null || Vector3.Distance(player.transform.position, end.position) > PickupReach)
            {
                return;
            }

            DetachEndToPlayer(endIndex, player);
        }

        public bool ConnectEndToSocket(int endIndex, MiniVanBridgeCableSocket socket)
        {
            Transform end = GetEnd(endIndex);
            if (end == null || socket == null || socket.IsConnected)
            {
                return false;
            }

            // A charger socket may only accept its permanently-anchored end — never the free plug.
            if (socket.OwnerCharger != null && !IsEndPermanentlyAnchored(endIndex))
            {
                return false;
            }

            ClearCarriedEnd(endIndex);

            ClearSocket(endIndex);
            if (!socket.Attach(this, endIndex))
            {
                return false;
            }

            sockets[endIndex] = socket;
            Vector3 plugPosition = socket.GetPlugWorldPosition();
            Quaternion plugRotation = socket.GetPlugWorldRotation();
            if (Application.isPlaying)
            {
                end.SetParent(socket.transform, true);
                end.position = plugPosition;
                end.rotation = plugRotation;
            }
            else
            {
                end.position = plugPosition;
                end.rotation = plugRotation;
            }
            ConfigureEnd(endIndex, false, true);
            if (CableVisual != null && !IsEndPermanentlyAnchored(endIndex))
            {
                CableVisual.SuspendPhysics = false;
                CableVisual.ResumePointPhysics();
            }

            return true;
        }

        public bool DetachEndToPlayer(int endIndex, MiniVanPlayer player)
        {
            if (IsEndPermanentlyAnchored(endIndex))
            {
                return false;
            }

            if (player == null ||
                GetCarriedBy(player).IsValid ||
                MiniVanBridgeBattery.GetCarriedBy(player) != null ||
                MiniVanCarBattery.GetCarriedBy(player) != null ||
                MiniVanBatteryCharger.GetCarriedBy(player) != null)
            {
                return false;
            }

            Transform end = GetEnd(endIndex);
            if (end == null)
            {
                return false;
            }

            ClearSocket(endIndex);
            if (Application.isPlaying)
            {
                end.SetParent(null, true);
            }
            carriedByPlayer[player] = new CarriedEnd { Cable = this, EndIndex = endIndex };
            ConfigureEnd(endIndex, true, false);
            ZeroEndVelocity(endIndex);
            SetEndRenderersEnabled(end, true);
            if (CableVisual != null)
            {
                CableVisual.SuspendPhysics = false;
                CableVisual.ResumePointPhysics();
            }

            return true;
        }

        public void DropEnd(int endIndex, MiniVanPlayer player)
        {
            Transform end = GetEnd(endIndex);
            if (end == null)
            {
                return;
            }

            if (player != null)
            {
                CarriedEnd carried = GetCarriedBy(player);
                if (carried.Cable == this && carried.EndIndex == endIndex)
                {
                    carriedByPlayer.Remove(player);
                }
            }

            ClearSocket(endIndex);
            if (Application.isPlaying)
            {
                end.SetParent(null, true);
            }
            if (player != null)
            {
                end.position = player.transform.position + player.transform.forward * 0.85f + Vector3.up * 0.28f;
            }

            ConfigureEnd(endIndex, false, false);
        }

        public void DetachEndInPlace(int endIndex)
        {
            if (IsEndPermanentlyAnchored(endIndex))
            {
                return;
            }

            List<MiniVanPlayer> toClear = new List<MiniVanPlayer>();
            foreach (KeyValuePair<MiniVanPlayer, CarriedEnd> pair in carriedByPlayer)
            {
                if (pair.Value.Cable == this && pair.Value.EndIndex == endIndex)
                {
                    toClear.Add(pair.Key);
                }
            }

            for (int i = 0; i < toClear.Count; i++)
            {
                carriedByPlayer.Remove(toClear[i]);
            }

            ClearSocket(endIndex);
            Transform end = GetEnd(endIndex);
            if (end != null && Application.isPlaying)
            {
                // Keep under cable root — never leave an orphan rigidbody in the scene.
                end.SetParent(transform, true);
            }

            ConfigureEnd(endIndex, false, true);
            ZeroEndVelocity(endIndex);
        }

        /// <summary>
        /// Reparents every unplugged end under this cable, snaps them to a local rest pose,
        /// and freezes physics. Call before SetActive(false) so plugs cannot linger in the world.
        /// </summary>
        public void StowEndsUnderCable()
        {
            for (int i = 0; i < 2; i++)
            {
                if (IsEndPermanentlyAnchored(i))
                {
                    continue;
                }

                ClearCarriedEnd(i);
                if (sockets[i] != null)
                {
                    ClearSocket(i);
                }

                Transform end = GetEnd(i);
                if (end == null)
                {
                    continue;
                }

                end.SetParent(transform, false);
                end.localPosition = new Vector3(0.35f, 0.12f, 0f);
                end.localRotation = Quaternion.identity;
                ConfigureEnd(i, true, false);
                ZeroEndVelocity(i);
                SetEndRenderersEnabled(end, false);
            }
        }

        /// <summary>
        /// Parks an unplugged free end at a world pose, still parented under the cable so it
        /// always hides/moves with the cable object. Always clears a stale socket link first so
        /// a plug stuck in the charger socket can be yanked back out.
        /// </summary>
        public void RestUnpluggedEnd(int endIndex, Vector3 worldPosition, Quaternion worldRotation)
        {
            if (IsEndPermanentlyAnchored(endIndex) || endIndex < 0 || endIndex >= sockets.Length)
            {
                return;
            }

            foreach (KeyValuePair<MiniVanPlayer, CarriedEnd> pair in carriedByPlayer)
            {
                if (pair.Value.Cable == this && pair.Value.EndIndex == endIndex)
                {
                    return;
                }
            }

            // Free ends must never stay plugged into a charger self-socket (or any socket)
            // while we are trying to park them — that was leaving the plug inside the chair.
            if (sockets[endIndex] != null)
            {
                ClearSocket(endIndex);
            }

            Transform end = GetEnd(endIndex);
            if (end == null)
            {
                return;
            }

            end.SetParent(transform, false);
            end.position = worldPosition;
            end.rotation = worldRotation;
            ConfigureEnd(endIndex, false, true);
            ZeroEndVelocity(endIndex);
            SetEndRenderersEnabled(end, true);

            Rigidbody body = endBodies[endIndex];
            if (body != null)
            {
                body.isKinematic = true;
                body.useGravity = false;
                body.detectCollisions = false;
            }

            // Colliders stay on so the player can raycast-pick the plug up.
            Collider[] colliders = endColliders[endIndex];
            if (colliders != null)
            {
                for (int i = 0; i < colliders.Length; i++)
                {
                    if (colliders[i] != null)
                    {
                        colliders[i].enabled = true;
                        colliders[i].isTrigger = true;
                    }
                }
            }
        }

        public void RestAllUnpluggedEnds(Vector3 worldPosition, Quaternion worldRotation)
        {
            for (int i = 0; i < 2; i++)
            {
                RestUnpluggedEnd(i, worldPosition, worldRotation);
            }

            if (CableVisual != null)
            {
                CableVisual.SuspendPhysics = true;
                CableVisual.RedrapeBetweenEnds();
            }
        }

        public void SetFreeEndPhysicsSuspended(bool suspended)
        {
            if (CableVisual != null)
            {
                CableVisual.SuspendPhysics = suspended;
                if (suspended)
                {
                    CableVisual.RedrapeBetweenEnds();
                }
            }
        }

        public bool IsEndCarried(int endIndex)
        {
            foreach (KeyValuePair<MiniVanPlayer, CarriedEnd> pair in carriedByPlayer)
            {
                if (pair.Value.Cable == this && pair.Value.EndIndex == endIndex)
                {
                    return true;
                }
            }

            return false;
        }

        public int GetFreeEndIndex()
        {
            for (int i = 0; i < 2; i++)
            {
                if (!IsEndPermanentlyAnchored(i))
                {
                    return i;
                }
            }

            return 1;
        }

        public void SnapEndToSocketPose(int endIndex, MiniVanBridgeCableSocket socket)
        {
            Transform end = GetEnd(endIndex);
            if (end == null || socket == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                end.SetParent(socket.transform, true);
            }

            end.position = socket.GetPlugWorldPosition();
            end.rotation = socket.GetPlugWorldRotation();
            ConfigureEnd(endIndex, false, true);
            ZeroEndVelocity(endIndex);
            SetEndRenderersEnabled(end, true);
            if (endIndex >= 0 && endIndex < sockets.Length)
            {
                sockets[endIndex] = socket;
            }
        }

        public void EnsureFreeEndPickupCollider(int endIndex)
        {
            Transform end = GetEnd(endIndex);
            if (end == null)
            {
                return;
            }

            BoxCollider box = end.GetComponent<BoxCollider>();
            if (box == null)
            {
                box = end.gameObject.AddComponent<BoxCollider>();
            }

            box.enabled = true;
            box.isTrigger = true;
            box.size = new Vector3(0.7f, 0.7f, 0.7f);
            box.center = Vector3.zero;

            if (endColliders[endIndex] == null || endColliders[endIndex].Length == 0)
            {
                endColliders[endIndex] = end.GetComponentsInChildren<Collider>(true);
            }
        }

        private void ZeroEndVelocity(int endIndex)
        {
            if (endIndex < 0 || endIndex >= endBodies.Length)
            {
                return;
            }

            Rigidbody body = endBodies[endIndex];
            if (body == null || body.isKinematic)
            {
                return;
            }

            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        private static void SetEndRenderersEnabled(Transform end, bool enabled)
        {
            if (end == null)
            {
                return;
            }

            Renderer[] renderers = end.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    renderers[i].enabled = enabled;
                }
            }
        }

        public bool IsFreeEndConnectedTo(MiniVanBridgeCableSocket socket)
        {
            if (socket == null)
            {
                return false;
            }

            for (int i = 0; i < sockets.Length; i++)
            {
                if (IsEndPermanentlyAnchored(i))
                {
                    continue;
                }

                if (sockets[i] == socket)
                {
                    return true;
                }
            }

            return false;
        }

        public MiniVanBridgeCableSocket GetFreeEndSocket()
        {
            for (int i = 0; i < sockets.Length; i++)
            {
                if (!IsEndPermanentlyAnchored(i) && sockets[i] != null)
                {
                    return sockets[i];
                }
            }

            return null;
        }

        private void ClearCarriedEnd(int endIndex)
        {
            List<MiniVanPlayer> toClear = new List<MiniVanPlayer>();
            foreach (KeyValuePair<MiniVanPlayer, CarriedEnd> pair in carriedByPlayer)
            {
                if (pair.Value.Cable == this && pair.Value.EndIndex == endIndex)
                {
                    toClear.Add(pair.Key);
                }
            }

            for (int i = 0; i < toClear.Count; i++)
            {
                carriedByPlayer.Remove(toClear[i]);
            }
        }

        private void ClearSocket(int endIndex)
        {
            if (endIndex < 0 || endIndex >= sockets.Length || sockets[endIndex] == null)
            {
                return;
            }

            sockets[endIndex].ClearConnection(this, endIndex);
            sockets[endIndex] = null;
        }

        private Transform GetEnd(int endIndex)
        {
            if (endIndex == 0)
            {
                return EndA;
            }

            return endIndex == 1 ? EndB : null;
        }

        public Vector3 GetEndWorldPosition(int endIndex)
        {
            Transform end = GetEnd(endIndex);
            return end != null ? end.position : transform.position;
        }

        public Vector3 GetClampedCarryPosition(int carriedEndIndex, Vector3 target)
        {
            Transform otherEnd = GetEnd(carriedEndIndex == 0 ? 1 : 0);
            if (otherEnd == null || CableVisual == null)
            {
                return target;
            }

            float maxLength = Mathf.Max(0.25f, CableVisual.CableLengthLimit);
            Vector3 fromOther = target - otherEnd.position;
            float distance = fromOther.magnitude;
            if (distance <= maxLength || distance <= 0.001f)
            {
                return target;
            }

            return otherEnd.position + fromOther / distance * maxLength;
        }

        private void CacheEndParts()
        {
            CacheEnd(0, EndA);
            CacheEnd(1, EndB);
            ConfigureEnd(0, false, false);
            ConfigureEnd(1, false, false);
        }

        private void CacheEnd(int endIndex, Transform end)
        {
            if (end == null)
            {
                endColliders[endIndex] = System.Array.Empty<Collider>();
                return;
            }

            endBodies[endIndex] = end.GetComponent<Rigidbody>();
            endColliders[endIndex] = end.GetComponentsInChildren<Collider>(true);
        }

        private void ConfigureEnd(int endIndex, bool carried, bool connected)
        {
            Rigidbody body = endBodies[endIndex];
            if (body != null)
            {
                body.isKinematic = carried || connected;
                body.useGravity = !carried && !connected;
                body.detectCollisions = !carried;
                body.mass = 0.7f;
                body.linearDamping = 0.18f;
                body.angularDamping = 0.35f;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                if (carried || connected)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
            }

            Collider[] colliders = endColliders[endIndex];
            if (colliders == null)
            {
                return;
            }

            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                {
                    colliders[i].enabled = !carried;
                }
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
