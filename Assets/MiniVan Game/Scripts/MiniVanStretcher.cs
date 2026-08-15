using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace MiniVanGame
{
    public enum MiniVanStretcherEnd
    {
        Front = 0,
        Rear = 1
    }

    /// <summary>
    /// Portable stretcher: empty → inventory; with Anton → two players must grip ends to carry.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class MiniVanStretcher : NetworkBehaviour, IMiniVanGameModeInteractable
    {
        public const ulong NoCarrier = ulong.MaxValue;
        public float PickupRadius = 2.4f;
        public float GripReach = 3.4f;

        // Height of the gripped end relative to player origin (origin ≈ waist).
        public float CarryHeightOffset = -0.1f;
        // Height of the free/grounded end above the terrain.
        public float GroundEndHeight = 0.18f;
        // Distance from stretcher center to each handle along local Z.
        public float HandleHalfLength = 1.05f;
        // Max horizontal distance between the two carriers while the stretcher is lifted.
        public float MaxCarrierSeparation = 3.2f;

        private static readonly List<MiniVanStretcher> Active = new List<MiniVanStretcher>(8);

        private readonly NetworkVariable<bool> available = new NetworkVariable<bool>(
            true,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<bool> hasAnton = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<ulong> frontCarrier = new NetworkVariable<ulong>(
            NoCarrier,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<ulong> rearCarrier = new NetworkVariable<ulong>(
            NoCarrier,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<Vector3> networkPosition = new NetworkVariable<Vector3>(
            Vector3.zero,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<Quaternion> networkRotation = new NetworkVariable<Quaternion>(
            Quaternion.identity,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private Renderer[] renderers;
        private Collider[] colliders;
        private Transform frontHandle;
        private Transform rearHandle;
        private Transform antonAttach;
        private Vector3 partialGripAnchor;
        private bool partialGripAnchorValid;

        public bool IsAvailable => !IsSpawned || available.Value;
        public bool HasAnton => hasAnton.Value;
        public bool IsLifted =>
            frontCarrier.Value != NoCarrier &&
            rearCarrier.Value != NoCarrier;
        public ulong FrontCarrierId => frontCarrier.Value;
        public ulong RearCarrierId => rearCarrier.Value;

        public override void OnNetworkSpawn()
        {
            if (!Active.Contains(this))
            {
                Active.Add(this);
            }

            EnsureVisual();
            CacheParts();
            available.OnValueChanged += HandleAvailableChanged;
            ApplyAvailable(available.Value);

            if (IsServer && networkPosition.Value == Vector3.zero)
            {
                networkPosition.Value = transform.position;
                networkRotation.Value = transform.rotation;
            }

            ApplyPose(networkPosition.Value, networkRotation.Value);
        }

        public override void OnNetworkDespawn()
        {
            available.OnValueChanged -= HandleAvailableChanged;
            Active.Remove(this);
        }

        private void Update()
        {
            if (IsServer)
            {
                UpdateServerPose();
            }

            ApplyPose(networkPosition.Value, networkRotation.Value);
        }

        public bool IsInReach(Vector3 worldPosition)
        {
            return Vector3.Distance(worldPosition, transform.position) <= PickupRadius;
        }

        public bool IsGrippedBy(ulong clientId)
        {
            return frontCarrier.Value == clientId || rearCarrier.Value == clientId;
        }

        public bool IsPinned(ulong clientId)
        {
            // Holding one end while the other end is free = stuck in place.
            return hasAnton.Value && IsGrippedBy(clientId) && !IsLifted;
        }

        public Vector3 GetAntonAttachPosition()
        {
            EnsureVisual();
            return antonAttach != null
                ? antonAttach.position
                : transform.position + Vector3.up * 0.55f;
        }

        public Quaternion GetAntonAttachRotation()
        {
            EnsureVisual();
            // Capsule long axis (local Y) laid along the bed length (local Z).
            Quaternion baseRot = antonAttach != null ? antonAttach.rotation : transform.rotation;
            return baseRot * Quaternion.Euler(90f, 0f, 0f);
        }

        public string GetPrompt(MiniVanPlayer player)
        {
            if (player == null || !IsAvailable)
            {
                return string.Empty;
            }

            if (!HasAnton)
            {
                if (player.IsCarryingAnton)
                {
                    return IsInReach(player.transform.position) ? "E - put Anton on stretcher" : string.Empty;
                }

                return IsInReach(player.transform.position) ? "E - take stretcher" : string.Empty;
            }

            if (IsGrippedBy(player.OwnerClientId))
            {
                return IsLifted
                    ? "Q - release stretcher"
                    : "Waiting for partner on other end... Q - release";
            }

            if (player.IsCarryingAnton || player.IsGrippingStretcher)
            {
                return string.Empty;
            }

            if (IsLifted)
            {
                return string.Empty;
            }

            // Aiming at Anton himself → his hold-E prompt wins.
            MiniVanAnton myAnton = MiniVanAnton.FindOnStretcher(NetworkObjectId);
            if (myAnton != null && myAnton.IsAimedBy(player))
            {
                return string.Empty;
            }

            if (!TryResolveLookedAtEnd(player, out _))
            {
                // Still show a hint when close to the loaded stretcher.
                if (Vector3.Distance(player.transform.position, transform.position) <= GripReach)
                {
                    return "E - lift stretcher end (stand at tip)";
                }

                return string.Empty;
            }

            return "E - lift stretcher end";
        }

        public void Interact(MiniVanPlayer player)
        {
            if (player == null || !IsAvailable)
            {
                return;
            }

            if (!HasAnton)
            {
                if (player.IsCarryingAnton)
                {
                    player.RequestPlaceAntonOnStretcher(this);
                    return;
                }

                player.TryPickupStretcher(this);
                return;
            }

            if (player.IsCarryingAnton || player.IsGrippingStretcher)
            {
                return;
            }

            // Aiming at Anton → that's a hold-E take, not a lift.
            MiniVanAnton myAnton = MiniVanAnton.FindOnStretcher(NetworkObjectId);
            if (myAnton != null && myAnton.IsAimedBy(player))
            {
                return;
            }

            // Prefer nearest end by standing position — aim is optional.
            if (!TryResolveLookedAtEnd(player, out MiniVanStretcherEnd end))
            {
                return;
            }

            player.RequestGripStretcher(this, end);
        }

        public void PrimaryAction(MiniVanPlayer player)
        {
        }

        public bool TryClaim()
        {
            if (!IsServer || !available.Value || hasAnton.Value)
            {
                return false;
            }

            if (frontCarrier.Value != NoCarrier || rearCarrier.Value != NoCarrier)
            {
                return false;
            }

            available.Value = false;
            ApplyAvailable(false);
            return true;
        }

        public bool ServerAcceptAnton(MiniVanAnton anton)
        {
            if (!IsServer || anton == null || hasAnton.Value || !available.Value)
            {
                return false;
            }

            hasAnton.Value = true;
            return true;
        }

        public void ServerClearAnton()
        {
            if (!IsServer)
            {
                return;
            }

            hasAnton.Value = false;
            ServerClearAllGrips();
        }

        public void ServerTryGrip(ulong clientId, MiniVanStretcherEnd end)
        {
            if (!IsServer || !hasAnton.Value || !available.Value)
            {
                return;
            }

            if (IsGrippedBy(clientId))
            {
                return;
            }

            if (end == MiniVanStretcherEnd.Front)
            {
                if (frontCarrier.Value != NoCarrier)
                {
                    return;
                }

                frontCarrier.Value = clientId;
            }
            else
            {
                if (rearCarrier.Value != NoCarrier)
                {
                    return;
                }

                rearCarrier.Value = clientId;
            }

            // Grip layout changed → re-anchor the free end on next pose update.
            partialGripAnchorValid = false;
        }

        public void ServerReleaseGrip(ulong clientId)
        {
            if (!IsServer || !IsGrippedBy(clientId))
            {
                return;
            }

            if (frontCarrier.Value == clientId)
            {
                frontCarrier.Value = NoCarrier;
            }

            if (rearCarrier.Value == clientId)
            {
                rearCarrier.Value = NoCarrier;
            }

            partialGripAnchorValid = false;
            if (frontCarrier.Value == NoCarrier && rearCarrier.Value == NoCarrier)
            {
                SnapToGround(networkPosition.Value);
            }
        }

        public bool TryResolveLookedAtEnd(MiniVanPlayer player, out MiniVanStretcherEnd end)
        {
            end = MiniVanStretcherEnd.Front;
            if (player == null)
            {
                return false;
            }

            EnsureVisual();
            if (frontHandle == null || rearHandle == null)
            {
                return false;
            }

            Vector3 playerPos = player.transform.position;
            float frontDist = Vector3.Distance(playerPos, frontHandle.position);
            float rearDist = Vector3.Distance(playerPos, rearHandle.position);

            // Prefer the nearer end by feet position; also accept camera aim as a soft hint.
            if (player.PlayerCamera != null)
            {
                Vector3 from = player.PlayerCamera.transform.position;
                Vector3 forward = player.PlayerCamera.transform.forward;
                float frontScore = ScoreHandle(from, forward, frontHandle, GripReach);
                float rearScore = ScoreHandle(from, forward, rearHandle, GripReach);
                if (frontScore >= 0f || rearScore >= 0f)
                {
                    if (frontScore >= 0f && (rearScore < 0f || frontScore <= rearScore))
                    {
                        end = MiniVanStretcherEnd.Front;
                        return true;
                    }

                    end = MiniVanStretcherEnd.Rear;
                    return true;
                }
            }

            float nearest = Mathf.Min(frontDist, rearDist);
            if (nearest > GripReach)
            {
                return false;
            }

            // Must be clearly nearer to one end (standing at that tip).
            end = frontDist <= rearDist ? MiniVanStretcherEnd.Front : MiniVanStretcherEnd.Rear;
            return true;
        }

        private static float ScoreHandle(Vector3 from, Vector3 forward, Transform handle, float gripReach)
        {
            if (handle == null)
            {
                return -1f;
            }

            Vector3 to = handle.position - from;
            float along = Vector3.Dot(to, forward);
            if (along < 0.2f || along > gripReach + 0.5f)
            {
                return -1f;
            }

            float lateral = Vector3.Cross(forward, to).magnitude;
            if (lateral > 1.1f)
            {
                return -1f;
            }

            return along + lateral * 0.35f;
        }

        private void UpdateServerPose()
        {
            if (!available.Value)
            {
                return;
            }

            bool partialGrip = hasAnton.Value && !IsLifted &&
                (frontCarrier.Value != NoCarrier || rearCarrier.Value != NoCarrier);
            if (partialGrip)
            {
                UpdatePartialGripPose();
                return;
            }

            if (IsLifted)
            {
                MiniVanPlayer front = FindPlayer(frontCarrier.Value);
                MiniVanPlayer rear = FindPlayer(rearCarrier.Value);
                if (front == null || rear == null)
                {
                    if (front == null && frontCarrier.Value != NoCarrier)
                    {
                        ServerReleaseGrip(frontCarrier.Value);
                    }

                    if (rear == null && rearCarrier.Value != NoCarrier)
                    {
                        ServerReleaseGrip(rearCarrier.Value);
                    }

                    return;
                }

                // Player origin is near capsule middle (~waist), so no extra height.
                Vector3 frontPos = front.transform.position + Vector3.up * CarryHeightOffset;
                Vector3 rearPos = rear.transform.position + Vector3.up * CarryHeightOffset;
                Vector3 mid = (frontPos + rearPos) * 0.5f;
                Vector3 axis = frontPos - rearPos;
                if (axis.sqrMagnitude < 0.01f)
                {
                    axis = transform.forward;
                }

                networkPosition.Value = mid;
                networkRotation.Value = Quaternion.LookRotation(axis.normalized, Vector3.up);
                return;
            }

            // Idle on ground: never re-copy transform (that froze mid-air poses).
        }

        private void UpdatePartialGripPose()
        {
            bool frontHeld = frontCarrier.Value != NoCarrier;
            ulong holderId = frontHeld ? frontCarrier.Value : rearCarrier.Value;
            MiniVanPlayer holder = FindPlayer(holderId);
            if (holder == null)
            {
                ServerReleaseGrip(holderId);
                return;
            }

            if (!partialGripAnchorValid)
            {
                CapturePartialGripAnchor(frontHeld);
            }

            // Hinge: free end locked on ground, gripped end at holder waist.
            Vector3 freeEnd = partialGripAnchor;
            freeEnd.y = SampleGroundY(freeEnd, freeEnd.y) + GroundEndHeight;
            partialGripAnchor = freeEnd;

            Vector3 heldEnd = holder.transform.position
                + holder.transform.forward * 0.55f
                + Vector3.up * CarryHeightOffset;

            Vector3 mid = (freeEnd + heldEnd) * 0.5f;
            // transform.forward points toward the front handle.
            Vector3 along = frontHeld ? (heldEnd - freeEnd) : (freeEnd - heldEnd);
            if (along.sqrMagnitude < 0.0001f)
            {
                along = transform.forward;
            }

            networkPosition.Value = mid;
            networkRotation.Value = Quaternion.LookRotation(along.normalized, Vector3.up);
        }

        private void CapturePartialGripAnchor(bool frontHeld)
        {
            EnsureVisual();
            // Free end = the end NOT being held. Lock XZ from current pose, Y from ground.
            Vector3 freeLocal = frontHeld
                ? new Vector3(0f, 0f, -HandleHalfLength)
                : new Vector3(0f, 0f, HandleHalfLength);
            Vector3 freeWorld = transform.TransformPoint(freeLocal);
            freeWorld.y = SampleGroundY(freeWorld, transform.position.y) + GroundEndHeight;

            partialGripAnchor = freeWorld;
            partialGripAnchorValid = true;
        }

        private float SampleGroundY(Vector3 near, float fallbackY)
        {
            // Cast from high so airborne / tilted poses still find the floor.
            if (TryGroundHit(new Vector3(near.x, near.y + 8f, near.z), 24f, out float groundY))
            {
                return groundY;
            }

            if (TryGroundHit(new Vector3(near.x, 50f, near.z), 100f, out groundY))
            {
                return groundY;
            }

            return fallbackY;
        }

        private bool TryGroundHit(Vector3 origin, float distance, out float groundY)
        {
            groundY = 0f;
            RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, distance, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            bool found = false;
            for (int i = 0; i < hits.Length; i++)
            {
                Collider col = hits[i].collider;
                if (col == null)
                {
                    continue;
                }

                // Ignore ourselves, Anton and players — only real ground/props count.
                if (col.transform.IsChildOf(transform) ||
                    col.GetComponentInParent<MiniVanAnton>() != null ||
                    col.GetComponentInParent<MiniVanPlayer>() != null)
                {
                    continue;
                }

                if (hits[i].distance < best)
                {
                    best = hits[i].distance;
                    groundY = hits[i].point.y;
                    found = true;
                }
            }

            return found;
        }

        private void SnapToGround(Vector3 approximate)
        {
            float groundY = SampleGroundY(approximate, approximate.y);
            Vector3 pos = new Vector3(approximate.x, groundY + GroundEndHeight, approximate.z);

            Vector3 flatForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (flatForward.sqrMagnitude < 0.001f)
            {
                flatForward = Vector3.forward;
            }

            networkPosition.Value = pos;
            networkRotation.Value = Quaternion.LookRotation(flatForward.normalized, Vector3.up);
            ApplyPose(networkPosition.Value, networkRotation.Value);
            partialGripAnchorValid = false;
        }

        private void ServerClearAllGrips()
        {
            frontCarrier.Value = NoCarrier;
            rearCarrier.Value = NoCarrier;
            partialGripAnchorValid = false;
        }

        private void ApplyPose(Vector3 position, Quaternion rotation)
        {
            transform.SetPositionAndRotation(position, rotation);
        }

        private void HandleAvailableChanged(bool previousValue, bool newValue)
        {
            ApplyAvailable(newValue);
        }

        private void ApplyAvailable(bool isAvailable)
        {
            CacheParts();
            if (renderers != null)
            {
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i] != null)
                    {
                        renderers[i].enabled = isAvailable;
                    }
                }
            }

            if (colliders != null)
            {
                for (int i = 0; i < colliders.Length; i++)
                {
                    if (colliders[i] != null)
                    {
                        colliders[i].enabled = isAvailable;
                    }
                }
            }
        }

        private void CacheParts()
        {
            renderers = GetComponentsInChildren<Renderer>(true);
            colliders = GetComponentsInChildren<Collider>(true);
        }

        private void EnsureVisual()
        {
            if (transform.Find("StretcherVisual") != null)
            {
                frontHandle = transform.Find("StretcherVisual/FrontHandle");
                rearHandle = transform.Find("StretcherVisual/RearHandle");
                antonAttach = transform.Find("StretcherVisual/AntonAttach");
                return;
            }

            GameObject root = new GameObject("StretcherVisual");
            root.transform.SetParent(transform, false);

            Material frameMat = CreateMat(new Color(0.55f, 0.55f, 0.52f, 1f));
            Material clothMat = CreateMat(new Color(0.75f, 0.12f, 0.12f, 1f));
            Material handleMat = CreateMat(new Color(0.18f, 0.18f, 0.2f, 1f));

            GameObject bed = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bed.name = "Bed";
            bed.transform.SetParent(root.transform, false);
            bed.transform.localPosition = new Vector3(0f, 0.22f, 0f);
            bed.transform.localScale = new Vector3(0.72f, 0.08f, 1.85f);
            SetMat(bed, clothMat);
            Object.Destroy(bed.GetComponent<Collider>());

            GameObject leftRail = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leftRail.name = "LeftRail";
            leftRail.transform.SetParent(root.transform, false);
            leftRail.transform.localPosition = new Vector3(-0.34f, 0.32f, 0f);
            leftRail.transform.localScale = new Vector3(0.06f, 0.12f, 1.9f);
            SetMat(leftRail, frameMat);
            Object.Destroy(leftRail.GetComponent<Collider>());

            GameObject rightRail = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rightRail.name = "RightRail";
            rightRail.transform.SetParent(root.transform, false);
            rightRail.transform.localPosition = new Vector3(0.34f, 0.32f, 0f);
            rightRail.transform.localScale = new Vector3(0.06f, 0.12f, 1.9f);
            SetMat(rightRail, frameMat);
            Object.Destroy(rightRail.GetComponent<Collider>());

            GameObject front = GameObject.CreatePrimitive(PrimitiveType.Cube);
            front.name = "FrontHandle";
            front.transform.SetParent(root.transform, false);
            front.transform.localPosition = new Vector3(0f, 0.28f, HandleHalfLength);
            front.transform.localScale = new Vector3(0.85f, 0.08f, 0.12f);
            SetMat(front, handleMat);
            frontHandle = front.transform;

            GameObject rear = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rear.name = "RearHandle";
            rear.transform.SetParent(root.transform, false);
            rear.transform.localPosition = new Vector3(0f, 0.28f, -HandleHalfLength);
            rear.transform.localScale = new Vector3(0.85f, 0.08f, 0.12f);
            SetMat(rear, handleMat);
            rearHandle = rear.transform;

            GameObject attach = new GameObject("AntonAttach");
            attach.transform.SetParent(root.transform, false);
            attach.transform.localPosition = new Vector3(0f, 0.42f, 0f);
            antonAttach = attach.transform;

            if (GetComponent<Collider>() == null)
            {
                BoxCollider box = gameObject.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, 0.28f, 0f);
                box.size = new Vector3(0.9f, 0.5f, 2.2f);
            }
        }

        private static void SetMat(GameObject go, Material mat)
        {
            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = mat;
            }
        }

        private static Material CreateMat(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            Material material = new Material(shader) { color = color };
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            return material;
        }

        private static MiniVanPlayer FindPlayer(ulong clientId)
        {
            if (clientId == NoCarrier)
            {
                return null;
            }

            MiniVanPlayer[] players = FindObjectsByType<MiniVanPlayer>(FindObjectsSortMode.None);
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] != null && players[i].OwnerClientId == clientId)
                {
                    return players[i];
                }
            }

            return null;
        }

        /// <summary>
        /// Leash for lifted stretcher carriers: cuts the part of the movement that would
        /// pull the carrier too far away from the partner, so both must move together.
        /// </summary>
        public static Vector3 ClampCarrierDelta(MiniVanPlayer player, Vector3 delta)
        {
            if (player == null)
            {
                return delta;
            }

            MiniVanStretcher stretcher = FindGrippedBy(player.OwnerClientId);
            if (stretcher == null || !stretcher.IsLifted)
            {
                return delta;
            }

            ulong partnerId = stretcher.frontCarrier.Value == player.OwnerClientId
                ? stretcher.rearCarrier.Value
                : stretcher.frontCarrier.Value;
            MiniVanPlayer partner = FindPlayer(partnerId);
            if (partner == null)
            {
                return delta;
            }

            Vector3 playerPos = player.transform.position;
            Vector3 partnerPos = partner.transform.position;

            Vector3 toNew = (playerPos + delta) - partnerPos;
            toNew.y = 0f;
            float newDist = toNew.magnitude;

            Vector3 toCurrent = playerPos - partnerPos;
            toCurrent.y = 0f;
            // If they were already stretched apart when lifting, don't teleport —
            // just forbid increasing the gap any further.
            float limit = Mathf.Max(stretcher.MaxCarrierSeparation, toCurrent.magnitude);
            if (newDist <= limit || newDist < 0.001f)
            {
                return delta;
            }

            // Slide along the leash circle around the partner instead of stretching it.
            Vector3 clamped = toNew * (limit / newDist);
            return new Vector3(
                partnerPos.x + clamped.x - playerPos.x,
                delta.y,
                partnerPos.z + clamped.z - playerPos.z);
        }

        public static MiniVanStretcher FindGrippedBy(ulong clientId)
        {
            for (int i = 0; i < Active.Count; i++)
            {
                MiniVanStretcher stretcher = Active[i];
                if (stretcher != null && stretcher.IsSpawned && stretcher.IsGrippedBy(clientId))
                {
                    return stretcher;
                }
            }

            return null;
        }

        public static MiniVanStretcher FindPinnedFor(ulong clientId)
        {
            for (int i = 0; i < Active.Count; i++)
            {
                MiniVanStretcher stretcher = Active[i];
                if (stretcher != null && stretcher.IsSpawned && stretcher.IsPinned(clientId))
                {
                    return stretcher;
                }
            }

            return null;
        }
    }
}
