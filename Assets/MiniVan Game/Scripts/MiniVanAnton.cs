using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace MiniVanGame
{
    public enum MiniVanAntonState
    {
        World = 0,
        BackCarry = 1,
        OnStretcher = 2,
        Seated = 3
    }

    /// <summary>
    /// Main-quest NPC: capsule body carried on back, placed on stretcher, or seated in the minivan.
    /// Server-authoritative pose via NetworkVariables (same pattern as corpse carry).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class MiniVanAnton : NetworkBehaviour, IMiniVanGameModeInteractable
    {
        public const ulong NoCarrier = ulong.MaxValue;
        private const float InteractionReach = 3.2f;
        private const float TakeFromStretcherSeconds = 1f;

        private static readonly List<MiniVanAnton> Active = new List<MiniVanAnton>(4);

        private readonly NetworkVariable<int> networkState = new NetworkVariable<int>(
            (int)MiniVanAntonState.World,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<ulong> networkCarrier = new NetworkVariable<ulong>(
            NoCarrier,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<ulong> networkStretcherId = new NetworkVariable<ulong>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<ulong> networkVehicleId = new NetworkVariable<ulong>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> networkSeatIndex = new NetworkVariable<int>(
            -1,
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

        private Collider bodyCollider;
        private Renderer bodyRenderer;
        private bool localDropPredictionActive;
        private Vector3 localPredictedPosition;
        private Quaternion localPredictedRotation;

        public MiniVanAntonState State => (MiniVanAntonState)networkState.Value;
        public ulong CarrierClientId => networkCarrier.Value;
        public ulong StretcherNetworkId => networkStretcherId.Value;
        public ulong VehicleNetworkId => networkVehicleId.Value;
        public int SeatIndex => networkSeatIndex.Value;
        public Vector3 ReplicatedPosition => networkPosition.Value;
        public Quaternion ReplicatedRotation => networkRotation.Value;
        public static float TakeHoldSeconds => TakeFromStretcherSeconds;

        public override void OnNetworkSpawn()
        {
            if (!Active.Contains(this))
            {
                Active.Add(this);
            }

            EnsureVisual();
            if (IsServer)
            {
                if (networkPosition.Value == Vector3.zero)
                {
                    networkPosition.Value = transform.position;
                }

                if (State == MiniVanAntonState.World)
                {
                    SnapToGround(networkPosition.Value);
                }
            }

            ApplyPose(networkPosition.Value, networkRotation.Value);
            RefreshCollision();
        }

        public override void OnNetworkDespawn()
        {
            Active.Remove(this);
        }

        private void Update()
        {
            if (localDropPredictionActive)
            {
                if (State != MiniVanAntonState.BackCarry)
                {
                    localDropPredictionActive = false;
                }
                else
                {
                    ApplyPose(localPredictedPosition, localPredictedRotation);
                    RefreshCollisionForPredictedWorld();
                    return;
                }
            }

            if (IsServer)
            {
                UpdateServerPose();
            }

            ApplyPose(networkPosition.Value, networkRotation.Value);
            RefreshCollision();
        }

        public void BeginLocalDropPrediction(Vector3 approximate)
        {
            float yaw = transform.eulerAngles.y;
            float groundY = approximate.y;
            Vector3 origin = new Vector3(approximate.x, approximate.y + 8f, approximate.z);
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 24f, ~0, QueryTriggerInteraction.Ignore))
            {
                groundY = hit.point.y;
            }

            localDropPredictionActive = true;
            localPredictedPosition = new Vector3(approximate.x, groundY + 0.36f, approximate.z);
            localPredictedRotation = Quaternion.Euler(-90f, yaw, 0f);
            ApplyPose(localPredictedPosition, localPredictedRotation);
            RefreshCollisionForPredictedWorld();
        }

        private void RefreshCollisionForPredictedWorld()
        {
            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider col = colliders[i];
                if (col == null)
                {
                    continue;
                }

                col.isTrigger = true;
                col.enabled = true;
            }
        }

        public string GetPrompt(MiniVanPlayer player)
        {
            if (player == null || !IsSpawned)
            {
                return string.Empty;
            }

            if (player.IsCarryingAnton || player.IsStretcherPinned || player.IsGrippingStretcher)
            {
                return string.Empty;
            }

            if (State == MiniVanAntonState.OnStretcher)
            {
                if (!IsInReach(player) || player.BlocksAntonPickup())
                {
                    return string.Empty;
                }

                return IsAimedBy(player) ? "Hold E - take Anton" : string.Empty;
            }

            if (State != MiniVanAntonState.World)
            {
                return string.Empty;
            }

            if (!IsInReach(player) || player.BlocksAntonPickup())
            {
                return string.Empty;
            }

            return "E - take Anton";
        }

        public void Interact(MiniVanPlayer player)
        {
            if (player == null || State != MiniVanAntonState.World)
            {
                return;
            }

            player.RequestPickupAnton(this);
        }

        public void PrimaryAction(MiniVanPlayer player)
        {
        }

        public bool IsInReach(MiniVanPlayer player)
        {
            if (player == null)
            {
                return false;
            }

            Vector3 from = player.PlayerCamera != null
                ? player.PlayerCamera.transform.position
                : player.transform.position;
            return Vector3.Distance(from, transform.position) <= InteractionReach;
        }

        public bool IsCarriedBy(ulong clientId)
        {
            return State == MiniVanAntonState.BackCarry && networkCarrier.Value == clientId;
        }

        public bool IsAimedBy(MiniVanPlayer player)
        {
            if (player == null || player.PlayerCamera == null)
            {
                return false;
            }

            Vector3 from = player.PlayerCamera.transform.position;
            Vector3 forward = player.PlayerCamera.transform.forward;
            Vector3 to = ReplicatedPosition - from;
            float along = Vector3.Dot(to, forward);
            if (along < 0.1f || along > 3.6f)
            {
                return false;
            }

            return Vector3.Cross(forward, to).magnitude <= 0.6f;
        }

        public static MiniVanAnton FindOnStretcher(ulong stretcherNetworkId)
        {
            if (stretcherNetworkId == 0)
            {
                return null;
            }

            for (int i = 0; i < Active.Count; i++)
            {
                MiniVanAnton anton = Active[i];
                if (anton != null && anton.IsSpawned &&
                    anton.State == MiniVanAntonState.OnStretcher &&
                    anton.networkStretcherId.Value == stretcherNetworkId)
                {
                    return anton;
                }
            }

            return null;
        }

        public static MiniVanAnton FindCarriedBy(ulong clientId)
        {
            for (int i = 0; i < Active.Count; i++)
            {
                MiniVanAnton anton = Active[i];
                if (anton != null && anton.IsSpawned && anton.IsCarriedBy(clientId))
                {
                    return anton;
                }
            }

            return null;
        }

        public static MiniVanAnton FindNearest(Vector3 from)
        {
            MiniVanAnton best = null;
            float bestDist = float.MaxValue;
            for (int i = 0; i < Active.Count; i++)
            {
                MiniVanAnton anton = Active[i];
                if (anton == null || !anton.IsSpawned)
                {
                    continue;
                }

                float dist = Vector3.Distance(from, anton.ReplicatedPosition);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = anton;
                }
            }

            return best;
        }

        public static MiniVanAnton GetInSeat(MiniVanVehicle vehicle, int seatIndex)
        {
            if (vehicle == null || !vehicle.IsSpawned)
            {
                return null;
            }

            for (int i = 0; i < Active.Count; i++)
            {
                MiniVanAnton anton = Active[i];
                if (anton == null || !anton.IsSpawned || anton.State != MiniVanAntonState.Seated)
                {
                    continue;
                }

                if (anton.networkVehicleId.Value == vehicle.NetworkObjectId &&
                    anton.networkSeatIndex.Value == seatIndex)
                {
                    return anton;
                }
            }

            return null;
        }

        public static bool IsSeatOccupied(MiniVanVehicle vehicle, int seatIndex)
        {
            return GetInSeat(vehicle, seatIndex) != null;
        }

        public void ServerPickup(MiniVanPlayer carrier)
        {
            if (!IsServer || carrier == null || State == MiniVanAntonState.BackCarry || State == MiniVanAntonState.Seated)
            {
                return;
            }

            if (FindCarriedBy(carrier.OwnerClientId) != null || carrier.IsGrippingStretcher)
            {
                return;
            }

            if (State == MiniVanAntonState.OnStretcher)
            {
                MiniVanStretcher stretcher = FindStretcher(networkStretcherId.Value);
                if (stretcher != null)
                {
                    stretcher.ServerClearAnton();
                }
            }

            networkState.Value = (int)MiniVanAntonState.BackCarry;
            networkCarrier.Value = carrier.OwnerClientId;
            networkStretcherId.Value = 0;
            networkVehicleId.Value = 0;
            networkSeatIndex.Value = -1;
            RefreshCollision();
        }

        public void ServerDrop(Vector3 worldPosition)
        {
            if (!IsServer || State != MiniVanAntonState.BackCarry)
            {
                return;
            }

            networkState.Value = (int)MiniVanAntonState.World;
            networkCarrier.Value = NoCarrier;
            networkStretcherId.Value = 0;
            networkVehicleId.Value = 0;
            networkSeatIndex.Value = -1;
            SnapToGround(worldPosition);
            RefreshCollision();
        }

        public void ServerPlaceOnStretcher(MiniVanStretcher stretcher)
        {
            if (!IsServer || stretcher == null || !stretcher.IsSpawned || State != MiniVanAntonState.BackCarry)
            {
                return;
            }

            if (!stretcher.ServerAcceptAnton(this))
            {
                return;
            }

            networkState.Value = (int)MiniVanAntonState.OnStretcher;
            networkCarrier.Value = NoCarrier;
            networkStretcherId.Value = stretcher.NetworkObjectId;
            networkVehicleId.Value = 0;
            networkSeatIndex.Value = -1;
            networkPosition.Value = stretcher.GetAntonAttachPosition();
            networkRotation.Value = stretcher.GetAntonAttachRotation();
            RefreshCollision();
        }

        public void ServerSeat(MiniVanVehicle vehicle, int seatIndex)
        {
            if (!IsServer || vehicle == null || State != MiniVanAntonState.BackCarry)
            {
                return;
            }

            MiniVanSeat seat = vehicle.GetSeat(seatIndex);
            if (seat == null || seat.IsDriverSeat || !vehicle.IsSeatAvailable(seatIndex))
            {
                return;
            }

            networkState.Value = (int)MiniVanAntonState.Seated;
            networkCarrier.Value = NoCarrier;
            networkStretcherId.Value = 0;
            networkVehicleId.Value = vehicle.NetworkObjectId;
            networkSeatIndex.Value = seatIndex;
            RefreshCollision();
        }

        private void UpdateServerPose()
        {
            MiniVanAntonState state = State;
            if (state == MiniVanAntonState.BackCarry)
            {
                MiniVanPlayer carrier = FindPlayer(networkCarrier.Value);
                if (carrier == null)
                {
                    ServerDrop(networkPosition.Value);
                    return;
                }

                Transform t = carrier.transform;
                networkPosition.Value = t.position - t.forward * 0.55f + Vector3.up * 1.05f;
                networkRotation.Value = Quaternion.LookRotation(-t.forward, Vector3.up) * Quaternion.Euler(0f, 0f, 90f);
            }
            else if (state == MiniVanAntonState.OnStretcher)
            {
                MiniVanStretcher stretcher = FindStretcher(networkStretcherId.Value);
                if (stretcher == null)
                {
                    networkState.Value = (int)MiniVanAntonState.World;
                    networkStretcherId.Value = 0;
                    SnapToGround(networkPosition.Value);
                    return;
                }

                networkPosition.Value = stretcher.GetAntonAttachPosition();
                networkRotation.Value = stretcher.GetAntonAttachRotation();
            }
            else if (state == MiniVanAntonState.Seated)
            {
                MiniVanVehicle vehicle = FindVehicle(networkVehicleId.Value);
                MiniVanSeat seat = vehicle != null ? vehicle.GetSeat(networkSeatIndex.Value) : null;
                if (seat == null || seat.SitPoint == null)
                {
                    networkState.Value = (int)MiniVanAntonState.World;
                    networkVehicleId.Value = 0;
                    networkSeatIndex.Value = -1;
                    SnapToGround(networkPosition.Value);
                    return;
                }

                // Upright sitting pose, slight recline against the backrest.
                networkPosition.Value = seat.SitPoint.position + seat.SitPoint.forward * 0.08f + Vector3.up * 0.05f;
                networkRotation.Value = seat.SitPoint.rotation * Quaternion.Euler(-10f, 0f, 0f);
            }
        }

        private void SnapToGround(Vector3 approximate)
        {
            float yaw = networkRotation.Value.eulerAngles.y;
            float groundY = approximate.y;
            Vector3 origin = new Vector3(approximate.x, approximate.y + 8f, approximate.z);
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 24f, ~0, QueryTriggerInteraction.Ignore))
            {
                groundY = hit.point.y;
            }
            else if (Physics.Raycast(new Vector3(approximate.x, 50f, approximate.z), Vector3.down, out hit, 100f, ~0, QueryTriggerInteraction.Ignore))
            {
                groundY = hit.point.y;
            }

            // Lying flat on the ground (capsule long axis along world Z after X=-90).
            networkPosition.Value = new Vector3(approximate.x, groundY + 0.36f, approximate.z);
            networkRotation.Value = Quaternion.Euler(-90f, yaw, 0f);
        }

        private void ApplyPose(Vector3 position, Quaternion rotation)
        {
            transform.SetPositionAndRotation(position, rotation);
        }

        private void RefreshCollision()
        {
            // Anton is visual-only physics: triggers for interaction, never solid.
            // Disabled entirely while carried / seated so nothing inside the van is poked.
            bool useTrigger = State == MiniVanAntonState.World || State == MiniVanAntonState.OnStretcher;
            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider col = colliders[i];
                if (col == null)
                {
                    continue;
                }

                col.isTrigger = true;
                col.enabled = useTrigger;
            }

            bodyCollider = null;
            Transform body = transform.Find("AntonBody");
            if (body != null)
            {
                bodyCollider = body.GetComponent<Collider>();
            }
        }

        private void EnsureVisual()
        {
            Transform visual = transform.Find("AntonBody");
            if (visual == null)
            {
                GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                body.name = "AntonBody";
                body.transform.SetParent(transform, false);
                body.transform.localPosition = Vector3.zero;
                body.transform.localRotation = Quaternion.identity;
                body.transform.localScale = new Vector3(0.72f, 0.9f, 0.72f);
                bodyCollider = body.GetComponent<Collider>();
                if (bodyCollider != null)
                {
                    bodyCollider.isTrigger = true;
                }

                bodyRenderer = body.GetComponent<Renderer>();
                Material mat = CreateMat(new Color(0.2f, 0.55f, 0.95f, 1f));
                if (bodyRenderer != null)
                {
                    bodyRenderer.sharedMaterial = mat;
                }
            }
            else
            {
                bodyCollider = visual.GetComponent<Collider>();
                bodyRenderer = visual.GetComponent<Renderer>();
            }

            CapsuleCollider rootCapsule = GetComponent<CapsuleCollider>();
            if (rootCapsule == null)
            {
                rootCapsule = gameObject.AddComponent<CapsuleCollider>();
                rootCapsule.height = 1.8f;
                rootCapsule.radius = 0.36f;
                rootCapsule.center = Vector3.zero;
            }

            rootCapsule.isTrigger = true;
            RefreshCollision();
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

        private static MiniVanStretcher FindStretcher(ulong networkObjectId)
        {
            if (networkObjectId == 0)
            {
                return null;
            }

            MiniVanStretcher[] stretchers = FindObjectsByType<MiniVanStretcher>(FindObjectsSortMode.None);
            for (int i = 0; i < stretchers.Length; i++)
            {
                if (stretchers[i] != null && stretchers[i].IsSpawned &&
                    stretchers[i].NetworkObjectId == networkObjectId)
                {
                    return stretchers[i];
                }
            }

            return null;
        }

        private static MiniVanVehicle FindVehicle(ulong networkObjectId)
        {
            if (networkObjectId == 0)
            {
                return null;
            }

            MiniVanVehicle[] vehicles = FindObjectsByType<MiniVanVehicle>(FindObjectsSortMode.None);
            for (int i = 0; i < vehicles.Length; i++)
            {
                if (vehicles[i] != null && vehicles[i].IsSpawned &&
                    vehicles[i].NetworkObjectId == networkObjectId)
                {
                    return vehicles[i];
                }
            }

            return null;
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
    }
}
