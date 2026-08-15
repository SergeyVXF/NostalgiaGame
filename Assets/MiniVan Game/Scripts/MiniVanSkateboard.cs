using Unity.Netcode;
using UnityEngine;

namespace MiniVanGame
{
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(Rigidbody))]
    public class MiniVanSkateboard : NetworkBehaviour
    {
        public const ulong EmptyClientId = ulong.MaxValue;

        [Header("Ride")]
        public Transform RidePoint;
        public float MountRadius = 2.0f;
        public float MaxSpeedKph = 20f;
        public float DownhillMaxSpeedKph = 82f;
        public float Acceleration = 22f;
        public float BrakeAcceleration = 18f;
        public float CoastingFriction = 0.12f;
        public float DownhillCoastingFriction = 0.01f;
        public float SidewaysFriction = 0.22f;
        public float SlopeGravityMultiplier = 4.4f;
        public float SnowboardDownhillBuildUp = 0.65f;
        public float SnowboardCarveAcceleration = 7.5f;
        public float SnowboardDriftStrength = 5.0f;
        public float SnowboardDownhillSideFrictionScale = 0.02f;
        public float SnowboardVelocityTurnAssist = 0.85f;
        public float SnowboardTurnSpeedPreservation = 0.995f;
        public float SnowboardArcadeBoost = 0.75f;
        public float SnowboardVelocityResponse = 5.5f;
        public float SteeringDegreesPerSecond = 420f;
        public float TurnInPlaceDegreesPerSecond = 260f;
        public float MaxSteerDegreesPerFixedStep = 2.6f;
        public float MaxVelocityTurnDegreesPerFixedStep = 6.0f;
        public float TurnLeanDegrees = 19f;
        public float TurnLeanSharpness = 9f;
        public float JumpVelocity = 6.0f;
        public float GroundCheckDistance = 0.75f;
        public float GroundCheckRadius = 0.36f;
        public float GroundStickAcceleration = 6.5f;
        public float GroundedUpVelocityLimit = 0.12f;
        public float GroundRideHeight = 0.08f;
        public float GroundHeightFollowSharpness = 9f;
        public float GroundHeightMaxCorrection = 0.04f;
        public float GroundedMaxSurfaceDistance = 0.68f;
        public float LandingMaxSurfaceDistance = 0.28f;
        public float RampDetachGraceTime = 0.18f;
        public float RampDetachUpVelocityLimit = 0.05f;
        public float JumpGroundGraceTime = 0.32f;
        public float GroundStickDisableAfterJump = 0.35f;
        public float RiderYawFollowSharpness = 0.85f;
        public bool AlignBoardToRiderYaw = false;
        public float SurfaceNormalSharpness = 8f;
        public float SurfaceNormalSampleRadius = 0.9f;
        public float SurfaceTiltSharpness = 10f;
        public float RideSurfaceMinNormalY = 0f;
        public float RideSurfaceProbeExtraDistance = 0.65f;
        public float WallRideStickAcceleration = 13f;
        public float WallRideMaxNormalVelocity = 0.18f;
        public float RiderSurfaceAlignSharpness = 18f;
        public float RiderSurfaceLeanDegrees = 12f;
        public float RiderCollisionRadius = 0.34f;
        public float RiderCollisionHeight = 1.65f;
        public Vector3 RiderCollisionCenter = new Vector3(0f, 1.05f, 0f);
        public bool DisableDeckCollisionWhileRiding = true;
        public float RideGroundCollisionRadius = 0.24f;
        public float RideGroundCollisionHeight = 0.52f;
        public Vector3 RideGroundCollisionCenter = new Vector3(0f, 0.20f, 0f);

        [Header("Tow Rope")]
        public float TowAttachExtraRadius = 0.35f;
        public float TowRopeLength = 20f;
        public float TowSoftLimitDamping = 3f;
        public float TowRopeCastRadius = 0.16f;
        public float TowMaxCorrectionPerTick = 0.55f;
        public float TowEmergencyMaxCorrectionPerTick = 3.0f;
        public float TowCornerSlideAssist = 12f;
        public float TowWrapSlipExcess = 0.35f;
        public int TowHardLimitIterations = 8;
        public float TowBreakDistance = 28f;
        public float UprightSharpness = 3.2f;
        public bool DebugTowRope = true;
        public float DebugTowRopeInterval = 0.35f;
        public float DebugTowRopeSnapLengthDelta = 2.5f;
        public float DebugTowRopeLargeCorrection = 1.25f;
        [Header("Kickflip")]
        [Tooltip("Air clearance above a normal ollie that triggers a kickflip (meters).")]
        public float KickflipExtraClearance = 0.4f;
        public float KickflipDuration = 0.58f;
        public float KickflipGroundProbe = 18f;

        [Header("Debug")]
        public bool DebugSkateboard = true;
        public float DebugLogInterval = 0.5f;

        [Header("Networking")]
        public float RemoteSmoothTime = 0.08f;
        public float RemoteTeleportDistance = 4f;
        public readonly NetworkVariable<ulong> RiderClientId = new NetworkVariable<ulong>(
            EmptyClientId,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public readonly NetworkVariable<bool> TowAttached = new NetworkVariable<bool>(false);
        public readonly NetworkVariable<Vector3> TowAnchorPosition = new NetworkVariable<Vector3>();
        public readonly NetworkVariable<ulong> HolderClientId = new NetworkVariable<ulong>(
            EmptyClientId,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        public readonly NetworkVariable<bool> IsOnShelf = new NetworkVariable<bool>(false);

        public readonly NetworkVariable<Vector3> NetworkPosition = new NetworkVariable<Vector3>();
        public readonly NetworkVariable<Quaternion> NetworkRotation = new NetworkVariable<Quaternion>(Quaternion.identity);

        private Rigidbody body;
        private Renderer[] skateboardRenderers;
        private Collider[] skateboardColliders;
        private bool carriedVisualVisible = true;
        private bool localDropPredictionActive;
        private float throttleInput;
        private float steerInput;
        private MiniVanTowHook towHook;
        private MiniVanSkateboardShelf shelf;
        private LineRenderer towRopeRenderer;
        private Material towRopeMaterial;
        private CapsuleCollider rideGroundCollision;
        private CapsuleCollider riderCollision;
        private readonly Vector3[] towRopePath = new Vector3[MiniVanTowRopeUtility.MaxPathPoints];
        private readonly MiniVanTowRopeUtility.RopeState towRopeState = new MiniVanTowRopeUtility.RopeState();
        private float lastInputTime;
        private bool jumpRequested;
        private float nextDebugLogTime;
        private float nextInputDebugLogTime;
        private float nextTowDebugLogTime;
        private float lastTowPathLength = -1f;
        private int lastTowPathCount = -1;
        private Vector3 rideVelocity;
        private float targetRiderYaw;
        private bool hasTargetRiderYaw;
        private float smoothedTurnLean;

        private Vector3 remoteVelocity;
        private Vector3 smoothedGroundNormal = Vector3.up;
        private Vector3 sampledGroundNormal = Vector3.up;
        private float lastGroundedTime = -999f;
        private float lastJumpRequestTime = -999f;
        private float groundStickDisabledUntil = -999f;
        private readonly MiniVanBoardKickflip kickflip = new MiniVanBoardKickflip();
        private bool kickflipAirborne;
        private bool kickflipTriggeredThisAir;
        private float kickflipMaxClearance;

        public bool HasRider => RiderClientId.Value != EmptyClientId;
        public bool IsCarried => HolderClientId.Value != EmptyClientId;
        public bool IsAvailable => IsSpawned && !HasRider && !IsCarried && !IsOnShelf.Value;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            EnsureRidePoint();
            EnsureRiderCollision();
            EnsureRideGroundCollision();
            CacheSkateboardRenderers();
            CacheSkateboardColliders();
            ConfigureBody();
            UpdateSkateboardBodyCollisionState();
            BindKickflipVisuals();
        }

        public override void OnNetworkSpawn()
        {
            body = GetComponent<Rigidbody>();
            EnsureRidePoint();
            EnsureRiderCollision();
            EnsureRideGroundCollision();
            CacheSkateboardRenderers();
            CacheSkateboardColliders();
            ConfigureBody();
            UpdateSkateboardBodyCollisionState();

            if (IsServer)
            {
                NetworkPosition.Value = transform.position;
                NetworkRotation.Value = transform.rotation;
            }
            else if (body != null)
            {
                body.isKinematic = true;
                body.useGravity = false;
            }
        }

        private void Update()
        {
            if (!IsSpawned)
            {
                return;
            }

            UpdateCarriedVisualVisibility();

            if (localDropPredictionActive)
            {
                if (!IsCarried)
                {
                    localDropPredictionActive = false;
                }

                remoteVelocity = Vector3.zero;
                return;
            }

            if (IsLocalHolder())
            {
                SimulateCarried();
                remoteVelocity = Vector3.zero;
                return;
            }

            if (IsServer)
            {
                return;
            }

            Vector3 targetPosition = NetworkPosition.Value;
            Quaternion targetRotation = NetworkRotation.Value;
            if ((targetPosition - transform.position).sqrMagnitude > RemoteTeleportDistance * RemoteTeleportDistance)
            {
                transform.SetPositionAndRotation(targetPosition, targetRotation);
                remoteVelocity = Vector3.zero;
                return;
            }

            transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref remoteVelocity, RemoteSmoothTime, Mathf.Infinity, Time.deltaTime);
            float blend = 1f - Mathf.Exp(-18f * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, blend);
        }

        private bool IsLocalHolder()
        {
            return !localDropPredictionActive
                && IsCarried
                && NetworkManager.Singleton != null
                && HolderClientId.Value == NetworkManager.Singleton.LocalClientId;
        }

private void LateUpdate()
        {
            if (IsSpawned && IsLocalHolder())
            {
                SimulateCarried();
            }

            UpdateRiderCollisionState();
            UpdateTowRopeVisual();
            kickflip.Duration = Mathf.Max(0.12f, KickflipDuration);
            kickflip.Tick(Time.deltaTime);
        }


private void FixedUpdate()
        {
            if (!IsServer || body == null)
            {
                return;
            }

            UpdateRiderCollisionState();

            if (IsCarried)
            {
                SimulateCarried();
                NetworkPosition.Value = transform.position;
                NetworkRotation.Value = transform.rotation;
                return;
            }

            if (IsOnShelf.Value)
            {
                SimulateShelfed();
                NetworkPosition.Value = transform.position;
                NetworkRotation.Value = transform.rotation;
                return;
            }

            if (body.isKinematic)
            {
                body.isKinematic = false;
                body.useGravity = true;
                LogSkateboardDebug("Server body was kinematic; forced dynamic");
            }

            if (!HasRider || Time.time - lastInputTime > 0.35f)
            {
                if (HasRider && Time.time >= nextDebugLogTime)
                {
                    LogSkateboardDebug("Input timeout: clearing throttle/steer");
                }

                throttleInput = 0f;
                steerInput = 0f;
            }

            SimulateRide();
            ApplyTowRopePhysics();
            PreventMinivanCabinEntry();
            NetworkPosition.Value = transform.position;
            NetworkRotation.Value = transform.rotation;
            LogSkateboardDebug("FixedUpdate");
        }

        public bool CanMount(MiniVanPlayer player)
        {
            if (player == null || HasRider || IsOnShelf.Value)
            {
                return false;
            }

            if (IsCarried)
            {
                return HolderClientId.Value == player.OwnerClientId;
            }

            return Vector3.Distance(player.transform.position, transform.position) <= MountRadius + 0.75f;
        }

public bool TryMount(ulong clientId, MiniVanPlayer player)
        {
            if (!IsServer)
            {
                Debug.LogWarning("[MiniVanSkateboard] TryMount rejected: not server");
                return false;
            }

            if (!CanMount(player))
            {
                float distance = player != null ? Vector3.Distance(player.transform.position, transform.position) : -1f;
                Debug.LogWarning("[MiniVanSkateboard] TryMount rejected: canMount=false client=" + clientId + " distance=" + distance.ToString("0.00") + " hasRider=" + HasRider + " pos=" + transform.position.ToString("F2"));
                return false;
            }

            bool wasCarried = IsCarried;
            if (wasCarried)
            {
                Vector3 forward = Vector3.ProjectOnPlane(player.transform.forward, Vector3.up);
                if (forward.sqrMagnitude < 0.01f)
                {
                    forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
                }

                forward = forward.sqrMagnitude > 0.01f ? forward.normalized : Vector3.forward;
                Vector3 mountPosition = player.transform.position + forward * 0.32f + Vector3.down * 0.82f;
                if (Physics.Raycast(player.transform.position + Vector3.up * 0.35f, Vector3.down, out RaycastHit groundHit, 2.2f, ~0, QueryTriggerInteraction.Ignore))
                {
                    mountPosition = groundHit.point + groundHit.normal * 0.08f + forward * 0.32f;
                }

                Quaternion mountRotation = Quaternion.LookRotation(forward, Vector3.up);
                transform.SetPositionAndRotation(mountPosition, mountRotation);
                if (body != null)
                {
                    body.position = mountPosition;
                    body.rotation = mountRotation;
                }
            }

            HolderClientId.Value = EmptyClientId;
            IsOnShelf.Value = false;
            shelf = null;
            RiderClientId.Value = clientId;
            UpdateSkateboardBodyCollisionState();
            UpdateRiderCollisionState();
            throttleInput = 0f;
            steerInput = 0f;
            lastInputTime = Time.time;
            rideVelocity = Vector3.zero;
            smoothedTurnLean = 0f;

            if (body != null)
            {
                body.isKinematic = false;
                body.useGravity = true;
                body.WakeUp();
            }

            if (NetworkObject != null && NetworkObject.IsSpawned && NetworkObject.OwnerClientId != clientId)
            {
                NetworkObject.ChangeOwnership(clientId);
            }

            Debug.Log("[MiniVanSkateboard] Mounted client=" + clientId + " owner=" + (NetworkObject != null ? NetworkObject.OwnerClientId.ToString() : "none") + " pos=" + transform.position.ToString("F2") + " rbKinematic=" + (body != null && body.isKinematic));
            return true;
        }

public bool TryDismount(ulong clientId)
        {
            if (!IsServer || RiderClientId.Value != clientId)
            {
                Debug.LogWarning("[MiniVanSkateboard] TryDismount rejected client=" + clientId + " rider=" + RiderClientId.Value + " isServer=" + IsServer);
                return false;
            }

            DetachTowRope();
            RiderClientId.Value = EmptyClientId;
            UpdateSkateboardBodyCollisionState();
            UpdateRiderCollisionState();
            throttleInput = 0f;
            steerInput = 0f;
            rideVelocity = Vector3.zero;
            smoothedTurnLean = 0f;

            if (body != null)
            {
                body.WakeUp();
            }

            if (NetworkObject != null && NetworkObject.IsSpawned && NetworkObject.OwnerClientId == clientId)
            {
                NetworkObject.RemoveOwnership();
            }

            Debug.Log("[MiniVanSkateboard] Dismounted client=" + clientId + " pos=" + transform.position.ToString("F2"));
            return true;
        }

        public bool TryPickup(ulong clientId, MiniVanPlayer player)
        {
            if (!IsServer || player == null || HasRider || IsCarried)
            {
                return false;
            }

            if (!IsOnShelf.Value && Vector3.Distance(player.transform.position, transform.position) > MountRadius + 0.9f)
            {
                return false;
            }

            DetachTowRope();
            HolderClientId.Value = clientId;
            IsOnShelf.Value = false;
            shelf = null;
            throttleInput = 0f;
            steerInput = 0f;
            rideVelocity = Vector3.zero;

            if (body != null)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.isKinematic = true;
                body.useGravity = false;
            }

            UpdateSkateboardBodyCollisionState();
            UpdateRiderCollisionState();

            if (NetworkObject != null && NetworkObject.IsSpawned && NetworkObject.OwnerClientId != clientId)
            {
                NetworkObject.ChangeOwnership(clientId);
            }

            SimulateCarried();
            return true;
        }

        public bool TryPlaceOnShelf(ulong clientId, MiniVanSkateboardShelf targetShelf)
        {
            if (!IsServer || targetShelf == null || HolderClientId.Value != clientId)
            {
                return false;
            }

            HolderClientId.Value = EmptyClientId;
            RiderClientId.Value = EmptyClientId;
            IsOnShelf.Value = true;
            shelf = targetShelf;
            throttleInput = 0f;
            steerInput = 0f;
            rideVelocity = Vector3.zero;

            if (body != null)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.isKinematic = true;
                body.useGravity = false;
            }

            UpdateSkateboardBodyCollisionState();
            UpdateRiderCollisionState();
            SimulateShelfed();
            return true;
        }

        public bool TryDrop(ulong clientId, MiniVanPlayer player)
        {
            if (!IsServer || player == null || HolderClientId.Value != clientId)
            {
                return false;
            }

            Vector3 forward = Vector3.ProjectOnPlane(player.transform.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.01f)
            {
                forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            }

            forward = forward.sqrMagnitude > 0.01f ? forward.normalized : Vector3.forward;
            Vector3 dropPosition = player.transform.position + forward * 0.95f + Vector3.up * 0.12f;
            if (Physics.Raycast(player.transform.position + Vector3.up * 0.7f + forward * 0.65f, Vector3.down, out RaycastHit groundHit, 2.6f, ~0, QueryTriggerInteraction.Ignore))
            {
                dropPosition = groundHit.point + groundHit.normal * 0.08f;
            }

            Quaternion dropRotation = Quaternion.LookRotation(forward, Vector3.up);

            HolderClientId.Value = EmptyClientId;
            RiderClientId.Value = EmptyClientId;
            IsOnShelf.Value = false;
            shelf = null;
            throttleInput = 0f;
            steerInput = 0f;
            rideVelocity = Vector3.zero;

            transform.SetPositionAndRotation(dropPosition, dropRotation);
            if (body != null)
            {
                body.isKinematic = false;
                body.useGravity = true;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.position = dropPosition;
                body.rotation = dropRotation;
                body.WakeUp();
            }

            UpdateSkateboardBodyCollisionState();
            UpdateRiderCollisionState();

            if (NetworkObject != null && NetworkObject.IsSpawned && NetworkObject.OwnerClientId == clientId)
            {
                NetworkObject.RemoveOwnership();
            }

            NetworkPosition.Value = transform.position;
            NetworkRotation.Value = transform.rotation;
            return true;
        }

        public void BeginLocalDropPrediction(MiniVanPlayer player)
        {
            if (player == null || !IsCarried || HolderClientId.Value != player.OwnerClientId)
            {
                return;
            }

            Vector3 forward = Vector3.ProjectOnPlane(player.transform.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.01f)
            {
                forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            }

            forward = forward.sqrMagnitude > 0.01f ? forward.normalized : Vector3.forward;
            Vector3 dropPosition = player.transform.position + forward * 0.95f + Vector3.up * 0.12f;
            if (Physics.Raycast(player.transform.position + Vector3.up * 0.7f + forward * 0.65f, Vector3.down, out RaycastHit groundHit, 2.6f, ~0, QueryTriggerInteraction.Ignore))
            {
                dropPosition = groundHit.point + groundHit.normal * 0.08f;
            }

            Quaternion dropRotation = Quaternion.LookRotation(forward, Vector3.up);
            localDropPredictionActive = true;
            transform.SetPositionAndRotation(dropPosition, dropRotation);
            if (body != null)
            {
                body.isKinematic = false;
                body.useGravity = true;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.position = dropPosition;
                body.rotation = dropRotation;
                body.WakeUp();
            }

            UpdateSkateboardBodyCollisionState();
            SetCarriedVisualVisible(true);
        }
        [ServerRpc(RequireOwnership = false)]
        public void ToggleTowRopeServerRpc(ServerRpcParams rpcParams = default)
        {
            // Legacy board tow-rope removed — winch cable is the only tow system.
            if (RiderClientId.Value != rpcParams.Receive.SenderClientId)
            {
                return;
            }

            DetachTowRope();
        }

        public bool HasTowHookInRange()
        {
            return false;
        }

        private void DetachTowRope()
        {
            towHook = null;
            towRopeState.Clear();
            ResetTowDebugHistory();
            if (IsServer)
            {
                TowAttached.Value = false;
                TowAnchorPosition.Value = Vector3.zero;
            }

            Debug.Log("[MiniVanSkateboard] Tow detached");
        }

        private void ApplyTowRopePhysics()
        {
            if (!IsServer || body == null || !TowAttached.Value)
            {
                return;
            }

            if (!MiniVanTowRopeUtility.IsFinite(body.position) || !MiniVanTowRopeUtility.IsFinite(body.linearVelocity))
            {
                RecoverFromInvalidTowState();
                return;
            }

            if (towHook == null)
            {
                return;
            }

            int pathCount = 0;
            Vector3 direction = Vector3.zero;
            float totalDistance = 0f;
            float maxExcess = 0f;
            float maxCorrection = 0f;
            int appliedSolves = 0;
            string pathDebug = "";
            int solveIterations = Mathf.Clamp(TowHardLimitIterations, 1, 12);
            float correctionBudget = Mathf.Max(0.05f, TowEmergencyMaxCorrectionPerTick);
            for (int i = 0; i < solveIterations; i++)
            {
                Vector3 attach = GetTowAttachPosition();
                Vector3 anchor = towHook.AnchorPosition;
                TowAnchorPosition.Value = anchor;

                pathCount = MiniVanTowRopeUtility.BuildPath(attach, anchor, TowRopeCastRadius, transform, towHook.transform.root, towRopeState, towRopePath);
                if (pathCount < 2)
                {
                    return;
                }

                totalDistance = MiniVanTowRopeUtility.GetPathLength(towRopePath, pathCount);
                float excess = totalDistance - TowRopeLength;
                direction = MiniVanTowRopeUtility.GetTensionDirection(towRopePath, pathCount);
                pathDebug = MiniVanTowRopeUtility.LastPathDebug;
                if (!MiniVanTowRopeUtility.IsFinite(totalDistance) || !MiniVanTowRopeUtility.IsFinite(excess))
                {
                    LogTowDebug("invalid distance total=" + totalDistance + " excess=" + excess + " path=" + pathDebug, true);
                    RecoverFromInvalidTowState();
                    return;
                }

                maxExcess = Mathf.Max(maxExcess, excess);
                if (excess <= 0.015f || totalDistance <= 0.001f || direction.sqrMagnitude <= 0.000001f)
                {
                    break;
                }

                bool wrapped = pathCount > 2;
                bool badlyStretched = excess > TowRopeLength * 0.25f;
                Vector3 correctionDirection = wrapped && excess > TowWrapSlipExcess
                    ? MiniVanTowRopeUtility.GetSlidingTensionDirection(towRopePath, pathCount)
                    : direction;

                if (correctionDirection.sqrMagnitude <= 0.000001f)
                {
                    correctionDirection = direction;
                }

                float maxStep = badlyStretched
                    ? Mathf.Max(TowMaxCorrectionPerTick, TowEmergencyMaxCorrectionPerTick)
                    : TowMaxCorrectionPerTick;
                float correctionStep = Mathf.Min(excess, Mathf.Min(correctionBudget, maxStep));
                Vector3 correction = correctionDirection * correctionStep;
                maxCorrection = Mathf.Max(maxCorrection, correction.magnitude);
                appliedSolves++;
                MoveBodyWithSweep(correction);
                correctionBudget -= correctionStep;
                if (correctionBudget <= 0.001f)
                {
                    break;
                }
            }

            if (pathCount < 2 || direction.sqrMagnitude <= 0.000001f)
            {
                LogTowDebug("no tension pathCount=" + pathCount + " dir=" + direction.ToString("F2") + " path=" + pathDebug, false);
                return;
            }

            Vector3 boardVelocity = body.linearVelocity;
            float velocityAwayFromTarget = -Vector3.Dot(boardVelocity, direction);
            if (!MiniVanTowRopeUtility.IsFinite(velocityAwayFromTarget))
            {
                LogTowDebug("invalid velocityAwayFromTarget velocity=" + body.linearVelocity.ToString("F2") + " direction=" + direction.ToString("F2"), true);
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                return;
            }

            if (velocityAwayFromTarget > 0f)
            {
                body.linearVelocity += direction * velocityAwayFromTarget;
            }

            LogTowPhysicsDebug(pathCount, totalDistance, maxExcess, maxCorrection, appliedSolves, velocityAwayFromTarget, direction, pathDebug);
        }

        private void MoveBodyWithSweep(Vector3 correction)
        {
            MiniVanTowRopeUtility.MoveBodyWithSliding(body, correction, transform, towHook != null ? towHook.transform.root : null, 0.025f, 16, TowCornerSlideAssist);
        }

        private void RecoverFromInvalidTowState()
        {
            Vector3 fallback = transform.position;
            if (towHook != null && MiniVanTowRopeUtility.IsFinite(towHook.AnchorPosition))
            {
                fallback = towHook.AnchorPosition + Vector3.down * 0.35f;
            }
            else if (MiniVanTowRopeUtility.IsFinite(TowAnchorPosition.Value))
            {
                fallback = TowAnchorPosition.Value + Vector3.down * 0.35f;
            }
            else if (!MiniVanTowRopeUtility.IsFinite(fallback))
            {
                fallback = Vector3.zero;
            }

            transform.position = fallback;
            if (body != null)
            {
                body.position = fallback;
                body.rotation = Quaternion.identity;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }

            Debug.LogWarning("[MiniVanSkateboard] Invalid tow physics state recovered; detaching rope to avoid corrupt transform.");
            DetachTowRope();
        }

        private void LogTowPhysicsDebug(int pathCount, float totalDistance, float maxExcess, float maxCorrection, int appliedSolves, float velocityAwayFromTarget, Vector3 direction, string pathDebug)
        {
            if (!DebugTowRope)
            {
                return;
            }

            float lengthDelta = lastTowPathLength >= 0f ? Mathf.Abs(totalDistance - lastTowPathLength) : 0f;
            bool pathCountChanged = lastTowPathCount >= 0 && pathCount != lastTowPathCount;
            bool suspicious = lengthDelta >= DebugTowRopeSnapLengthDelta || maxCorrection >= DebugTowRopeLargeCorrection || pathCountChanged;
            if (!suspicious && Time.time < nextTowDebugLogTime)
            {
                return;
            }

            string message = "[MiniVanSkateboard][TowDebug] pos=" + transform.position.ToString("F2")
                + " vel=" + body.linearVelocity.ToString("F2")
                + " pathCount=" + pathCount
                + " length=" + totalDistance.ToString("0.00")
                + " lengthDelta=" + lengthDelta.ToString("0.00")
                + " limit=" + TowRopeLength.ToString("0.00")
                + " maxExcess=" + maxExcess.ToString("0.00")
                + " maxCorrection=" + maxCorrection.ToString("0.00")
                + " solves=" + appliedSolves
                + " awayVel=" + velocityAwayFromTarget.ToString("0.00")
                + " dir=" + direction.ToString("F2")
                + " anchor=" + (towHook != null ? towHook.AnchorPosition.ToString("F2") : TowAnchorPosition.Value.ToString("F2"))
                + " path=" + DescribeTowPath(pathCount)
                + " build=" + pathDebug;

            if (suspicious)
            {
                Debug.LogWarning(message);
            }
            else
            {
                Debug.Log(message);
            }

            lastTowPathLength = totalDistance;
            lastTowPathCount = pathCount;
            nextTowDebugLogTime = Time.time + Mathf.Max(0.05f, DebugTowRopeInterval);
        }

        private void LogTowDebug(string message, bool warning)
        {
            if (!DebugTowRope)
            {
                return;
            }

            string fullMessage = "[MiniVanSkateboard][TowDebug] " + message;
            if (warning)
            {
                Debug.LogWarning(fullMessage);
            }
            else if (Time.time >= nextTowDebugLogTime)
            {
                Debug.Log(fullMessage);
                nextTowDebugLogTime = Time.time + Mathf.Max(0.05f, DebugTowRopeInterval);
            }
        }

        private string DescribeTowPath(int pathCount)
        {
            int count = Mathf.Clamp(pathCount, 0, towRopePath.Length);
            string path = "";
            for (int i = 0; i < count; i++)
            {
                if (i > 0)
                {
                    path += " -> ";
                }

                path += towRopePath[i].ToString("F2");
            }

            return path;
        }

        private void ResetTowDebugHistory()
        {
            lastTowPathLength = -1f;
            lastTowPathCount = -1;
            nextTowDebugLogTime = 0f;
        }

        private Vector3 GetTowAttachPosition()
        {
            return transform.position + Vector3.up * 0.28f;
        }

        private void UpdateTowRopeVisual()
        {
            bool shouldShow = TowAttached.Value;
            if (!shouldShow)
            {
                if (towRopeRenderer != null)
                {
                    towRopeRenderer.enabled = false;
                }
                return;
            }

            EnsureTowRopeRenderer();
            if (towRopeRenderer == null)
            {
                return;
            }

            Vector3 attach = GetTowAttachPosition();
            Vector3 anchor = TowAnchorPosition.Value;
            if (towHook == null)
            {
                towHook = MiniVanTowHook.FindNearest(anchor, 0.75f);
            }

            int pathCount = MiniVanTowRopeUtility.BuildPath(attach, anchor, TowRopeCastRadius, transform, towHook != null ? towHook.transform.root : null, towRopeState, towRopePath);

            towRopeRenderer.enabled = true;
            towRopeRenderer.positionCount = Mathf.Max(2, pathCount);
            if (pathCount < 2)
            {
                towRopeRenderer.SetPosition(0, attach);
                towRopeRenderer.SetPosition(1, anchor);
                return;
            }

            for (int i = 0; i < pathCount; i++)
            {
                towRopeRenderer.SetPosition(i, towRopePath[i]);
            }
        }

        private bool TryGetTowRopeBend(Vector3 attach, Vector3 anchor, out Vector3 bend)
        {
            bend = Vector3.zero;
            Vector3 toAnchor = anchor - attach;
            float distance = toAnchor.magnitude;
            if (distance <= 0.05f)
            {
                return false;
            }

            RaycastHit[] hits = Physics.SphereCastAll(attach, 0.08f, toAnchor / distance, distance, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
            for (int i = 0; i < hits.Length; i++)
            {
                Collider hitCollider = hits[i].collider;
                if (hitCollider == null || hitCollider.transform.IsChildOf(transform))
                {
                    continue;
                }

                if (towHook != null && hitCollider.transform.IsChildOf(towHook.transform.root))
                {
                    continue;
                }

                bend = hits[i].point + hits[i].normal * 0.18f;
                return true;
            }

            return false;
        }
        private void EnsureTowRopeRenderer()
        {
            if (towRopeRenderer != null)
            {
                return;
            }

            GameObject ropeObject = new GameObject("Tow Rope Visual");
            ropeObject.transform.SetParent(transform, false);
            towRopeRenderer = ropeObject.AddComponent<LineRenderer>();
            towRopeRenderer.positionCount = 2;
            towRopeRenderer.useWorldSpace = true;
            towRopeRenderer.startWidth = 0.045f;
            towRopeRenderer.endWidth = 0.035f;
            towRopeRenderer.numCapVertices = 4;

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            towRopeMaterial = new Material(shader);
            towRopeMaterial.color = new Color(0.05f, 0.05f, 0.045f, 1f);
            towRopeRenderer.sharedMaterial = towRopeMaterial;
            towRopeRenderer.enabled = false;
        }



        [ServerRpc(RequireOwnership = false)]
public void SubmitInputServerRpc(float throttle, float steer, bool jump, float riderYaw, ServerRpcParams rpcParams = default)
        {
            if (RiderClientId.Value != rpcParams.Receive.SenderClientId)
            {
                if (DebugSkateboard && Time.time >= nextInputDebugLogTime)
                {
                    nextInputDebugLogTime = Time.time + Mathf.Max(0.1f, DebugLogInterval);
                    Debug.LogWarning("[MiniVanSkateboard] Input rejected sender=" + rpcParams.Receive.SenderClientId + " rider=" + RiderClientId.Value + " throttle=" + throttle.ToString("0.00") + " steer=" + steer.ToString("0.00") + " jump=" + jump);
                }
                return;
            }

            throttleInput = Mathf.Clamp(throttle, -1f, 1f);
            steerInput = Mathf.Clamp(steer, -1f, 1f);
            targetRiderYaw = riderYaw;
            hasTargetRiderYaw = true;
            if (jump)
            {
                jumpRequested = true;
                lastJumpRequestTime = Time.time;
            }

            lastInputTime = Time.time;

            if (body != null)
            {
                body.WakeUp();
            }

            if (DebugSkateboard && (Mathf.Abs(throttleInput) > 0.01f || Mathf.Abs(steerInput) > 0.01f || jump) && Time.time >= nextInputDebugLogTime)
            {
                nextInputDebugLogTime = Time.time + Mathf.Max(0.1f, DebugLogInterval);
                Debug.Log("[MiniVanSkateboard] Input accepted sender=" + rpcParams.Receive.SenderClientId + " throttle=" + throttleInput.ToString("0.00") + " steer=" + steerInput.ToString("0.00") + " jump=" + jump + " riderYaw=" + riderYaw.ToString("0.0") + " rbVel=" + (body != null ? body.linearVelocity.ToString("F2") : "no-rb") + " sleeping=" + (body != null && body.IsSleeping()) + " kinematic=" + (body != null && body.isKinematic));
            }
        }

        public Vector3 GetRidePosition()
        {
            EnsureRidePoint();
            return RidePoint != null ? RidePoint.position : transform.position + Vector3.up * 0.55f;
        }

        public Quaternion GetRideRotation()
        {
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, transform.up);
            if (forward.sqrMagnitude < 0.001f)
            {
                forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            }

            if (forward.sqrMagnitude < 0.001f)
            {
                forward = Vector3.forward;
            }

            return Quaternion.LookRotation(forward.normalized, transform.up);
        }

public Vector3 GetExitPosition(Vector3 preferredSide)
        {
            Vector3 side = preferredSide.sqrMagnitude > 0.01f ? preferredSide.normalized : transform.right;
            Vector3 rawPosition = transform.position + side * 1.85f + Vector3.up * 1.55f;
            Vector3 rayOrigin = rawPosition + Vector3.up * 3.0f;
            RaycastHit[] hits = Physics.RaycastAll(rayOrigin, Vector3.down, 8f, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].collider != null && !hits[i].collider.transform.IsChildOf(transform))
                {
                    Vector3 exit = hits[i].point + Vector3.up * 1.25f;
                    if (DebugSkateboard)
                    {
                        Debug.Log("[MiniVanSkateboard] Exit ground hit collider=" + hits[i].collider.name + " point=" + hits[i].point.ToString("F2") + " exit=" + exit.ToString("F2"));
                    }
                    return exit;
                }
            }

            if (DebugSkateboard)
            {
                Debug.LogWarning("[MiniVanSkateboard] Exit ray missed ground, using rawPosition=" + rawPosition.ToString("F2"));
            }
            return rawPosition;
        }

private void SimulateRide()
        {
            bool grounded = IsGrounded(out RaycastHit groundHit);
            if (grounded && Time.time < groundStickDisabledUntil && body.linearVelocity.y > 0.05f)
            {
                grounded = false;
            }

            if (grounded)
            {
                lastGroundedTime = Time.time;
                Vector3 measuredNormal = sampledGroundNormal.sqrMagnitude > 0.001f ? sampledGroundNormal.normalized : groundHit.normal.normalized;
                float groundBlend = 1f - Mathf.Exp(-Mathf.Max(0.1f, SurfaceNormalSharpness) * Time.fixedDeltaTime);
                smoothedGroundNormal = Vector3.Slerp(smoothedGroundNormal, measuredNormal, groundBlend);
            }
            else
            {
                float airBlend = 1f - Mathf.Exp(-4f * Time.fixedDeltaTime);
                smoothedGroundNormal = Vector3.Slerp(smoothedGroundNormal, Vector3.up, airBlend);
            }

            Vector3 surfaceUp = smoothedGroundNormal.sqrMagnitude > 0.001f ? smoothedGroundNormal.normalized : Vector3.up;
            Vector3 slopeForward = Vector3.ProjectOnPlane(transform.forward, surfaceUp);
            if (slopeForward.sqrMagnitude < 0.001f)
            {
                slopeForward = Vector3.ProjectOnPlane(Vector3.forward, surfaceUp);
                if (slopeForward.sqrMagnitude < 0.001f)
                {
                    slopeForward = Vector3.forward;
                }
            }
            slopeForward.Normalize();

            Vector3 driveForward = Vector3.ProjectOnPlane(slopeForward, Vector3.up);
            if (driveForward.sqrMagnitude < 0.001f)
            {
                driveForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
                if (driveForward.sqrMagnitude < 0.001f)
                {
                    driveForward = transform.forward;
                }
            }
            driveForward.Normalize();
            Vector3 rideForward = grounded ? slopeForward : driveForward;

            float normalMaxSpeed = MaxSpeedKph / 3.6f;
            float downhillMaxSpeed = Mathf.Max(normalMaxSpeed, DownhillMaxSpeedKph / 3.6f);
            Vector3 velocity = body.linearVelocity;
            Vector3 surfaceVelocity = grounded ? Vector3.ProjectOnPlane(velocity, surfaceUp) : Vector3.ProjectOnPlane(velocity, Vector3.up);
            if (rideVelocity.sqrMagnitude < 0.001f || (rideVelocity - surfaceVelocity).sqrMagnitude > 36f)
            {
                rideVelocity = surfaceVelocity;
            }

            float speedBeforeTurn = surfaceVelocity.magnitude;
            float verticalWorldVelocity = velocity.y;
            Vector3 downhillDirection = Vector3.zero;
            Vector3 downhillAcceleration = Vector3.zero;
            float slope01 = 0f;
            bool hasDownhillAcceleration = false;

            if (grounded)
            {
                Vector3 projectedGravity = Vector3.ProjectOnPlane(Physics.gravity, surfaceUp);
                if (projectedGravity.sqrMagnitude > 0.001f)
                {
                    downhillDirection = projectedGravity.normalized;
                    slope01 = Mathf.Clamp01(projectedGravity.magnitude / Mathf.Max(0.01f, Physics.gravity.magnitude));
                    float noticeableSlope = Mathf.Pow(slope01, 0.7f);
                    downhillAcceleration = downhillDirection * Physics.gravity.magnitude * noticeableSlope * SlopeGravityMultiplier;
                    hasDownhillAcceleration = true;
                }
            }

            Vector3 nextHorizontal = surfaceVelocity;
            if (grounded && Mathf.Abs(throttleInput) > 0.01f)
            {
                float inputAcceleration = throttleInput > 0f ? Acceleration : BrakeAcceleration;
                nextHorizontal += rideForward * throttleInput * inputAcceleration * Time.fixedDeltaTime;
            }
            else if (grounded && hasDownhillAcceleration)
            {
                float downhillSpeed = Vector3.Dot(surfaceVelocity, downhillDirection);
                Vector3 downhillVelocity = downhillDirection * Mathf.Max(0f, downhillSpeed);
                Vector3 otherVelocity = surfaceVelocity - downhillVelocity;
                float downhillBuild = Mathf.Lerp(0.15f, 0.75f, Mathf.Clamp01(SnowboardDownhillBuildUp));
                downhillVelocity += downhillAcceleration * downhillBuild * Time.fixedDeltaTime;
                downhillVelocity = Vector3.MoveTowards(downhillVelocity, Vector3.zero, DownhillCoastingFriction * Time.fixedDeltaTime);
                otherVelocity = Vector3.MoveTowards(otherVelocity, Vector3.zero, CoastingFriction * Time.fixedDeltaTime);
                nextHorizontal = downhillVelocity + otherVelocity;
            }
            else if (grounded)
            {
                nextHorizontal = Vector3.MoveTowards(surfaceVelocity, Vector3.zero, CoastingFriction * Time.fixedDeltaTime);
            }

            if (grounded)
            {
                Vector3 sideways = Vector3.ProjectOnPlane(nextHorizontal, rideForward);
                if (hasDownhillAcceleration)
                {
                    sideways -= downhillDirection * Vector3.Dot(sideways, downhillDirection);
                }

                float sideFriction = SidewaysFriction;
                if (hasDownhillAcceleration)
                {
                    float rampSteerRelease = Mathf.Lerp(1f, 0.35f, Mathf.Clamp01(Mathf.Abs(steerInput)));
                    sideFriction *= Mathf.Clamp(SnowboardDownhillSideFrictionScale, 0f, 1f) * rampSteerRelease;
                }

                nextHorizontal -= Vector3.ClampMagnitude(sideways, sideFriction * Time.fixedDeltaTime);
            }

            if (hasDownhillAcceleration && throttleInput > -0.05f)
            {
                float downhillAlignment = nextHorizontal.sqrMagnitude > 0.001f ? Vector3.Dot(nextHorizontal.normalized, downhillDirection) : 0f;
                float downhillBoost = Mathf.Lerp(0.9f, 1.55f, Mathf.Clamp01(downhillAlignment * 0.5f + 0.5f));
                downhillBoost *= Mathf.Max(0.1f, SnowboardArcadeBoost);
                nextHorizontal += downhillAcceleration * downhillBoost * Time.fixedDeltaTime;
            }

            Quaternion workingRotation = body.rotation;
            if (AlignBoardToRiderYaw && hasTargetRiderYaw)
            {
                Vector3 riderForward = Quaternion.Euler(0f, targetRiderYaw, 0f) * Vector3.forward;
                riderForward = Vector3.ProjectOnPlane(riderForward, surfaceUp);
                if (riderForward.sqrMagnitude > 0.001f)
                {
                    Quaternion riderRotation = Quaternion.LookRotation(riderForward.normalized, surfaceUp);
                    float riderBlend = 1f - Mathf.Exp(-Mathf.Max(0.1f, RiderYawFollowSharpness) * Time.fixedDeltaTime);
                    workingRotation = Quaternion.Slerp(workingRotation, riderRotation, riderBlend);
                }
            }

            float turnRate = TurnInPlaceDegreesPerSecond;
            if (Mathf.Abs(steerInput) > 0.01f)
            {
                float speed01 = Mathf.Clamp01(nextHorizontal.magnitude / Mathf.Max(0.01f, normalMaxSpeed));
                turnRate = Mathf.Lerp(TurnInPlaceDegreesPerSecond * 1.1f, SteeringDegreesPerSecond * 1.25f, speed01);
                if (grounded && hasDownhillAcceleration)
                {
                    float downhillTurnScale = Mathf.Lerp(1f, 0.68f, slope01);
                    turnRate *= downhillTurnScale;
                }

                float steerDegrees = steerInput * turnRate * Time.fixedDeltaTime;
                steerDegrees = Mathf.Clamp(steerDegrees, -Mathf.Max(0.1f, MaxSteerDegreesPerFixedStep), Mathf.Max(0.1f, MaxSteerDegreesPerFixedStep));
                Quaternion steered = Quaternion.AngleAxis(steerDegrees, surfaceUp) * workingRotation;
                workingRotation = steered;
            }

            Vector3 steeredSlopeForward = Vector3.ProjectOnPlane(workingRotation * Vector3.forward, surfaceUp);
            if (steeredSlopeForward.sqrMagnitude < 0.001f)
            {
                steeredSlopeForward = slopeForward;
            }
            else
            {
                steeredSlopeForward.Normalize();
            }

            Vector3 steeredDriveForward = Vector3.ProjectOnPlane(steeredSlopeForward, Vector3.up);
            if (steeredDriveForward.sqrMagnitude < 0.001f)
            {
                steeredDriveForward = driveForward;
            }
            else
            {
                steeredDriveForward.Normalize();
            }
            Vector3 steeredRideForward = grounded ? steeredSlopeForward : steeredDriveForward;

            if (grounded && nextHorizontal.sqrMagnitude > 0.04f)
            {
                bool movingBackward = Vector3.Dot(nextHorizontal, steeredRideForward) < -0.05f || throttleInput < -0.01f;
                Vector3 desiredDirection = movingBackward ? -steeredRideForward : steeredRideForward;
                float turnAssist = Mathf.Abs(steerInput) > 0.01f ? 0.85f : 0.55f;
                if (hasDownhillAcceleration)
                {
                    turnAssist *= Mathf.Clamp01(SnowboardVelocityTurnAssist);
                }

                float velocityTurnDegrees = Mathf.Min(turnRate * turnAssist * Time.fixedDeltaTime, Mathf.Max(0.1f, MaxVelocityTurnDegreesPerFixedStep));
                nextHorizontal = Vector3.RotateTowards(nextHorizontal, desiredDirection * nextHorizontal.magnitude, Mathf.Deg2Rad * velocityTurnDegrees, Acceleration * Time.fixedDeltaTime);
            }

            if (grounded && hasDownhillAcceleration && Mathf.Abs(steerInput) > 0.01f)
            {
                Vector3 carveRight = steeredSlopeForward;
                if (carveRight.sqrMagnitude > 0.001f)
                {
                    carveRight = Vector3.Cross(surfaceUp, steeredSlopeForward).normalized;
                    float speed01 = Mathf.Clamp01(nextHorizontal.magnitude / Mathf.Max(0.01f, downhillMaxSpeed));
                    float carveAmount = SnowboardCarveAcceleration * slope01 * Mathf.Lerp(0.35f, 1f, speed01);
                    float driftAmount = SnowboardDriftStrength * slope01 * Mathf.Lerp(0.2f, 1f, speed01);
                    nextHorizontal += carveRight * steerInput * carveAmount * Time.fixedDeltaTime;
                    nextHorizontal += downhillDirection * driftAmount * Time.fixedDeltaTime;
                }
            }

            if (grounded && hasDownhillAcceleration && throttleInput > -0.05f && speedBeforeTurn > 0.1f)
            {
                float steer01 = Mathf.Clamp01(Mathf.Abs(steerInput));
                float preserve01 = Mathf.Clamp01(SnowboardTurnSpeedPreservation);
                float minSpeed = speedBeforeTurn * Mathf.Lerp(1f, preserve01, steer01 * Mathf.Lerp(0.35f, 1f, slope01));
                if (nextHorizontal.magnitude < minSpeed)
                {
                    Vector3 preserveDirection = nextHorizontal.sqrMagnitude > 0.001f ? nextHorizontal.normalized : surfaceVelocity.normalized;
                    nextHorizontal = preserveDirection * minSpeed;
                }
            }

            float allowedSpeed = normalMaxSpeed;
            if (!grounded)
            {
                allowedSpeed = Mathf.Max(normalMaxSpeed, surfaceVelocity.magnitude);
            }
            else if (hasDownhillAcceleration && Vector3.Dot(nextHorizontal, downhillDirection) > 0.02f)
            {
                allowedSpeed = downhillMaxSpeed;
            }

            if (nextHorizontal.magnitude > allowedSpeed)
            {
                nextHorizontal = nextHorizontal.normalized * allowedSpeed;
            }

            bool jumpedThisFrame = false;
            bool jumpRequestFresh = Time.time - lastJumpRequestTime <= 0.25f;
            bool canUseGroundGrace = grounded || Time.time - lastGroundedTime <= JumpGroundGraceTime;

            if (jumpRequested && jumpRequestFresh && canUseGroundGrace)
            {
                verticalWorldVelocity = JumpVelocity;
                jumpRequested = false;
                jumpedThisFrame = true;
                groundStickDisabledUntil = Time.time + GroundStickDisableAfterJump;
                Debug.Log("[MiniVanSkateboard] Jump velocity=" + JumpVelocity.ToString("0.00") + " grounded=" + grounded + " lastGroundedAgo=" + (Time.time - lastGroundedTime).ToString("0.00"));
            }
            else if (jumpRequested && !jumpRequestFresh)
            {
                jumpRequested = false;
            }

            bool groundStickEnabled = grounded && !jumpedThisFrame && Time.time >= groundStickDisabledUntil;
            bool rampDetach = !grounded && Time.time - lastGroundedTime <= RampDetachGraceTime && Time.time >= groundStickDisabledUntil;
            if (rampDetach && verticalWorldVelocity > RampDetachUpVelocityLimit)
            {
                verticalWorldVelocity = RampDetachUpVelocityLimit;
            }

            if (groundStickEnabled && verticalWorldVelocity > GroundedUpVelocityLimit)
            {
                verticalWorldVelocity = GroundedUpVelocityLimit;
            }

            float velocityResponse = grounded ? SnowboardVelocityResponse : SnowboardVelocityResponse * 0.45f;
            float velocityBlend = 1f - Mathf.Exp(-Mathf.Max(0.01f, velocityResponse) * Time.fixedDeltaTime);
            rideVelocity = Vector3.Lerp(rideVelocity, nextHorizontal, velocityBlend);
            Vector3 finalVelocity = rideVelocity;
            if (!grounded || jumpedThisFrame)
            {
                finalVelocity += Vector3.up * verticalWorldVelocity;
            }

            body.linearVelocity = finalVelocity;
            if (groundStickEnabled)
            {
                ApplyGroundRideHeight(groundHit, surfaceUp);
            }

            if (groundStickEnabled)
            {
                float wall01 = Mathf.Clamp01(1f - surfaceUp.y);
                float stickAcceleration = Mathf.Lerp(GroundStickAcceleration, Mathf.Max(GroundStickAcceleration, WallRideStickAcceleration), wall01);
                body.AddForce(-surfaceUp * stickAcceleration, ForceMode.Acceleration);

                float normalVelocity = Vector3.Dot(body.linearVelocity, surfaceUp);
                float maxNormalVelocity = Mathf.Max(0.01f, WallRideMaxNormalVelocity);
                if (normalVelocity > maxNormalVelocity)
                {
                    body.linearVelocity -= surfaceUp * (normalVelocity - maxNormalVelocity);
                }
            }

            float speedForLean = Mathf.Clamp01(nextHorizontal.magnitude / Mathf.Max(1.5f, normalMaxSpeed));
            float targetLean = grounded ? steerInput * TurnLeanDegrees * Mathf.Lerp(0.45f, 1f, speedForLean) : 0f;
            float leanBlend = 1f - Mathf.Exp(-Mathf.Max(0.1f, TurnLeanSharpness) * Time.fixedDeltaTime);
            smoothedTurnLean = Mathf.Lerp(smoothedTurnLean, targetLean, leanBlend);
            Vector3 visualUp = Quaternion.AngleAxis(-smoothedTurnLean, steeredSlopeForward) * surfaceUp;
            if (visualUp.sqrMagnitude < 0.001f)
            {
                visualUp = surfaceUp;
            }

            Quaternion upright = Quaternion.LookRotation(steeredSlopeForward, visualUp.normalized);
            float uprightSharpness = grounded ? Mathf.Max(0.1f, SurfaceTiltSharpness) : Mathf.Max(0.1f, UprightSharpness);
            float uprightBlend = 1f - Mathf.Exp(-uprightSharpness * Time.fixedDeltaTime);
            body.MoveRotation(Quaternion.Slerp(body.rotation, upright, uprightBlend));

            UpdateKickflipAirTracking(grounded);
        }

        private void BindKickflipVisuals()
        {
            Transform deck = transform.Find("Deck");
            Transform grip = transform.Find("Grip Tape");
            kickflip.Bind(deck, grip);
            kickflip.LocalSpinAxis = Vector3.forward;
            kickflip.Duration = Mathf.Max(0.12f, KickflipDuration);
        }

        private void UpdateKickflipAirTracking(bool groundedNow)
        {
            if (!HasRider || IsCarried || IsOnShelf.Value)
            {
                kickflipAirborne = false;
                kickflipTriggeredThisAir = false;
                kickflipMaxClearance = 0f;
                return;
            }

            if (groundedNow)
            {
                if (kickflipAirborne && kickflip.IsPlaying)
                {
                    // Let the flip finish visually; only hard-reset if still spinning long after land.
                }

                kickflipAirborne = false;
                kickflipTriggeredThisAir = false;
                kickflipMaxClearance = 0f;
                return;
            }

            float clearance = MeasureAirClearance(GroundRideHeight);
            if (!kickflipAirborne)
            {
                kickflipAirborne = true;
                kickflipTriggeredThisAir = false;
                kickflipMaxClearance = clearance;
            }
            else
            {
                kickflipMaxClearance = Mathf.Max(kickflipMaxClearance, clearance);
            }

            float standardJumpHeight = (JumpVelocity * JumpVelocity) / (2f * Mathf.Max(0.1f, Mathf.Abs(Physics.gravity.y)));
            float threshold = standardJumpHeight + Mathf.Max(0.05f, KickflipExtraClearance);
            if (!kickflipTriggeredThisAir && kickflipMaxClearance >= threshold)
            {
                kickflipTriggeredThisAir = true;
                if (IsServer)
                {
                    PlayKickflipClientRpc();
                }
            }
        }

        private float MeasureAirClearance(float groundedClearance)
        {
            Vector3 origin = transform.position + Vector3.up * 0.25f;
            if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, Mathf.Max(2f, KickflipGroundProbe), ~0, QueryTriggerInteraction.Ignore))
            {
                return Mathf.Max(2f, KickflipGroundProbe);
            }

            if (hit.collider != null && hit.collider.transform.IsChildOf(transform))
            {
                return 0f;
            }

            return Mathf.Max(0f, hit.distance - 0.25f - Mathf.Max(0f, groundedClearance));
        }

        [ClientRpc]
        private void PlayKickflipClientRpc()
        {
            if (!kickflip.IsPlaying)
            {
                kickflip.StartFlip();
            }
        }

        private void ApplyGroundRideHeight(RaycastHit groundHit, Vector3 targetUp)
        {
            if (body == null || !MiniVanTowRopeUtility.IsFinite(groundHit.point) || targetUp.sqrMagnitude < 0.001f)
            {
                return;
            }

            targetUp.Normalize();
            float desiredHeight = Mathf.Max(0.02f, GroundRideHeight);
            float currentHeight = Vector3.Dot(body.position - groundHit.point, targetUp);
            float error = desiredHeight - currentHeight;
            if (Mathf.Abs(error) > 0.45f)
            {
                return;
            }

            float blend = 1f - Mathf.Exp(-Mathf.Max(0.1f, GroundHeightFollowSharpness) * Time.fixedDeltaTime);
            float correctionAmount = Mathf.Clamp(error * blend, -GroundHeightMaxCorrection, GroundHeightMaxCorrection);
            if (Mathf.Abs(correctionAmount) <= 0.0005f)
            {
                return;
            }

            Vector3 correction = targetUp * correctionAmount;
            if (!MiniVanTowRopeUtility.IsFinite(correction))
            {
                return;
            }

            Vector3 velocity = body.linearVelocity;
            float normalVelocity = Vector3.Dot(velocity, targetUp);
            if ((correctionAmount > 0f && normalVelocity < 0f) || (correctionAmount < 0f && normalVelocity > 0f))
            {
                body.linearVelocity = velocity - targetUp * normalVelocity * 0.65f;
            }

            body.MovePosition(body.position + correction);
        }

private bool IsGrounded(out RaycastHit groundHit)
        {
            Vector3 origin = transform.position + Vector3.up * 0.25f;
            float castDistance = GroundCheckDistance + Mathf.Max(0.25f, RideSurfaceProbeExtraDistance);
            if (TryFindRideSurface(origin, Vector3.down, castDistance, out groundHit)
                || TryFindRideSurface(origin, -transform.up, castDistance, out groundHit)
                || TryFindRideSurface(origin, -smoothedGroundNormal, castDistance, out groundHit))
            {
                sampledGroundNormal = SampleGroundNormal(groundHit);
                return true;
            }

            sampledGroundNormal = Vector3.up;
            groundHit = default;
            return false;
        }

        private bool TryFindRideSurface(Vector3 origin, Vector3 castDirection, float castDistance, out RaycastHit groundHit)
        {
            groundHit = default;
            if (castDirection.sqrMagnitude < 0.001f)
            {
                return false;
            }

            castDirection.Normalize();
            RaycastHit[] hits = Physics.SphereCastAll(origin, GroundCheckRadius, castDirection, castDistance, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
            float minNormalY = Mathf.Clamp(RideSurfaceMinNormalY, -0.05f, 0.95f);

            for (int i = 0; i < hits.Length; i++)
            {
                Collider hitCollider = hits[i].collider;
                if (hitCollider == null || hitCollider.transform.IsChildOf(transform) || hits[i].normal.y < minNormalY)
                {
                    continue;
                }

                Vector3 hitNormal = hits[i].normal.sqrMagnitude > 0.001f ? hits[i].normal.normalized : Vector3.up;
                float surfaceDistance = Vector3.Dot(transform.position - hits[i].point, hitNormal);
                bool recentlyJumped = Time.time - lastJumpRequestTime <= 2.0f;
                bool fallingAfterJump = recentlyJumped && body != null && body.linearVelocity.y <= 0.1f;
                float maxSurfaceDistance = fallingAfterJump ? LandingMaxSurfaceDistance : GroundedMaxSurfaceDistance;
                maxSurfaceDistance = Mathf.Max(maxSurfaceDistance, GroundCheckRadius + 0.2f);
                if (!MiniVanTowRopeUtility.IsFinite(surfaceDistance) || surfaceDistance < -0.18f || surfaceDistance > Mathf.Max(0.05f, maxSurfaceDistance))
                {
                    continue;
                }

                groundHit = hits[i];
                return true;
            }

            return false;
        }

        private Vector3 SampleGroundNormal(RaycastHit centerHit)
        {
            Vector3 normalSum = centerHit.normal.sqrMagnitude > 0.001f ? centerHit.normal.normalized : Vector3.up;
            int normalCount = 1;
            float sampleRadius = Mathf.Max(0.05f, SurfaceNormalSampleRadius);
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.001f)
            {
                forward = Vector3.forward;
            }
            forward.Normalize();

            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            Vector3 center = transform.position + Vector3.up * 0.32f;
            AccumulateGroundNormal(center + forward * sampleRadius, ref normalSum, ref normalCount);
            AccumulateGroundNormal(center - forward * sampleRadius, ref normalSum, ref normalCount);
            AccumulateGroundNormal(center + right * sampleRadius * 0.65f, ref normalSum, ref normalCount);
            AccumulateGroundNormal(center - right * sampleRadius * 0.65f, ref normalSum, ref normalCount);

            if (normalCount <= 0 || normalSum.sqrMagnitude < 0.001f)
            {
                return centerHit.normal.normalized;
            }

            return normalSum.normalized;
        }

        private void AccumulateGroundNormal(Vector3 origin, ref Vector3 normalSum, ref int normalCount)
        {
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, GroundCheckDistance + 0.65f, ~0, QueryTriggerInteraction.Ignore)
                && hit.collider != null
                && !hit.collider.transform.IsChildOf(transform)
                && hit.normal.y >= Mathf.Clamp(RideSurfaceMinNormalY, -0.05f, 0.95f))
            {
                normalSum += hit.normal.normalized;
                normalCount++;
            }
        }


        private void SimulateCarried()
        {
            MiniVanPlayer holder = FindPlayerForClient(HolderClientId.Value);
            if (holder == null)
            {
                return;
            }

            holder.GetSkateboardCarryPose(out Vector3 targetPosition, out Quaternion targetRotation);
            transform.SetPositionAndRotation(targetPosition, targetRotation);
            if (body != null)
            {
                body.isKinematic = true;
                body.useGravity = false;
                body.position = targetPosition;
                body.rotation = targetRotation;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }

            UpdateSkateboardBodyCollisionState();
            UpdateRiderCollisionState();
        }

        private void UpdateCarriedVisualVisibility()
        {
            bool shouldShow = true;
            if (IsCarried && !localDropPredictionActive)
            {
                MiniVanPlayer holder = FindPlayerForClient(HolderClientId.Value);
                shouldShow = holder == null || holder.IsInventoryItemSelectedForWorld(MiniVanInventoryItem.Skateboard);
            }

            SetCarriedVisualVisible(shouldShow);
        }

        private void CacheSkateboardRenderers()
        {
            skateboardRenderers = GetComponentsInChildren<Renderer>(true);
        }

        private void CacheSkateboardColliders()
        {
            skateboardColliders = GetComponentsInChildren<Collider>(true);
        }

        private void SetCarriedVisualVisible(bool visible)
        {
            if (carriedVisualVisible == visible)
            {
                return;
            }

            carriedVisualVisible = visible;
            if (skateboardRenderers == null || skateboardRenderers.Length == 0)
            {
                CacheSkateboardRenderers();
            }

            for (int i = 0; i < skateboardRenderers.Length; i++)
            {
                if (skateboardRenderers[i] != null)
                {
                    skateboardRenderers[i].enabled = visible;
                }
            }
        }

        private void SimulateShelfed()
        {
            if (shelf == null)
            {
                shelf = FindNearestShelf(transform.position, 2.5f);
            }

            if (shelf == null)
            {
                return;
            }

            Vector3 targetPosition = shelf.AnchorPosition;
            Quaternion targetRotation = shelf.AnchorRotation * Quaternion.Euler(0f, 90f, 0f);
            transform.SetPositionAndRotation(targetPosition, targetRotation);
            if (body != null)
            {
                body.position = targetPosition;
                body.rotation = targetRotation;
                if (!body.isKinematic)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
            }
        }

        private static MiniVanPlayer FindPlayerForClient(ulong clientId)
        {
            if (clientId == EmptyClientId)
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

        public static MiniVanSkateboardShelf FindNearestShelf(Vector3 position, float maxDistance)
        {
            MiniVanSkateboardShelf[] shelves = FindObjectsByType<MiniVanSkateboardShelf>(FindObjectsSortMode.None);
            MiniVanSkateboardShelf best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < shelves.Length; i++)
            {
                if (shelves[i] == null)
                {
                    continue;
                }

                float distance = Vector3.Distance(position, shelves[i].AnchorPosition);
                if (distance <= maxDistance && distance < bestDistance)
                {
                    best = shelves[i];
                    bestDistance = distance;
                }
            }

            return best;
        }
private void PreventMinivanCabinEntry()
        {
            if (!HasRider)
            {
                return;
            }

            MiniVanVehicle[] vehicles = FindObjectsByType<MiniVanVehicle>(FindObjectsSortMode.None);
            for (int i = 0; i < vehicles.Length; i++)
            {
                MiniVanVehicle vehicle = vehicles[i];
                if (vehicle == null)
                {
                    continue;
                }

                Transform vehicleTransform = vehicle.transform;
                Vector3 local = vehicleTransform.InverseTransformPoint(transform.position);
                bool insideCabin = Mathf.Abs(local.x) <= 1.8f && local.z >= -3.3f && local.z <= 2.95f && local.y >= 0.55f && local.y <= 2.85f;
                if (!insideCabin)
                {
                    continue;
                }

                local.x = local.x >= 0f ? 2.05f : -2.05f;
                Vector3 correctedPosition = vehicleTransform.TransformPoint(local);
                if (DebugSkateboard && Time.time >= nextDebugLogTime)
                {
                    LogSkateboardDebug("Cabin guard corrected skateboard away from minivan cabin");
                }

                body.position = correctedPosition;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                transform.position = correctedPosition;
                break;
            }
        }

private void LogSkateboardDebug(string reason)
        {
            if (!DebugSkateboard || Time.time < nextDebugLogTime)
            {
                return;
            }

            nextDebugLogTime = Time.time + Mathf.Max(0.1f, DebugLogInterval);
            Debug.Log("[MiniVanSkateboard] " + reason +
                      " rider=" + RiderClientId.Value +
                      " hasRider=" + HasRider +
                      " owner=" + (NetworkObject != null ? NetworkObject.OwnerClientId.ToString() : "none") +
                      " throttle=" + throttleInput.ToString("0.00") +
                      " steer=" + steerInput.ToString("0.00") +
                      " pos=" + transform.position.ToString("F2") +
                      " vel=" + (body != null ? body.linearVelocity.ToString("F2") : "no-rb") +
                      " speedKph=" + (body != null ? (body.linearVelocity.magnitude * 3.6f).ToString("0.0") : "0") +
                      " kinematic=" + (body != null && body.isKinematic) +
                      " sleeping=" + (body != null && body.IsSleeping()) +
                      " isServer=" + IsServer +
                      " isSpawned=" + IsSpawned);
        }



        private void UpdateSkateboardBodyCollisionState()
        {
            EnsureRideGroundCollision();
            if (skateboardColliders == null || skateboardColliders.Length == 0)
            {
                CacheSkateboardColliders();
            }

            bool bodyEnabled = !IsCarried && !IsOnShelf.Value;
            bool deckEnabled = bodyEnabled && (!HasRider || !DisableDeckCollisionWhileRiding);
            bool groundEnabled = bodyEnabled && HasRider && DisableDeckCollisionWhileRiding;
            for (int i = 0; i < skateboardColliders.Length; i++)
            {
                Collider skateboardCollider = skateboardColliders[i];
                if (skateboardCollider == null || skateboardCollider == riderCollision)
                {
                    continue;
                }

                if (skateboardCollider == rideGroundCollision)
                {
                    skateboardCollider.enabled = groundEnabled;
                }
                else
                {
                    skateboardCollider.enabled = deckEnabled;
                }
            }
        }
        private void EnsureRideGroundCollision()
        {
            if (rideGroundCollision != null)
            {
                ConfigureRideGroundCollision();
                return;
            }

            Transform existing = transform.Find("Ride Ground Collision");
            GameObject collisionObject = existing != null ? existing.gameObject : new GameObject("Ride Ground Collision");
            collisionObject.transform.SetParent(transform, false);
            collisionObject.transform.localPosition = Vector3.zero;
            collisionObject.transform.localRotation = Quaternion.identity;
            rideGroundCollision = collisionObject.GetComponent<CapsuleCollider>();
            if (rideGroundCollision == null)
            {
                rideGroundCollision = collisionObject.AddComponent<CapsuleCollider>();
            }

            ConfigureRideGroundCollision();
        }

        private void ConfigureRideGroundCollision()
        {
            if (rideGroundCollision == null)
            {
                return;
            }

            rideGroundCollision.isTrigger = false;
            rideGroundCollision.direction = 1;
            rideGroundCollision.radius = Mathf.Max(0.03f, RideGroundCollisionRadius);
            rideGroundCollision.height = Mathf.Max(rideGroundCollision.radius * 2.1f, RideGroundCollisionHeight);
            rideGroundCollision.center = RideGroundCollisionCenter;
            rideGroundCollision.enabled = !IsCarried && !IsOnShelf.Value && HasRider && DisableDeckCollisionWhileRiding;
        }

        private void EnsureRiderCollision()
        {
            if (riderCollision != null)
            {
                return;
            }

            Transform existing = transform.Find("Rider Collision");
            GameObject collisionObject = existing != null ? existing.gameObject : new GameObject("Rider Collision");
            collisionObject.transform.SetParent(transform, false);
            collisionObject.transform.localPosition = Vector3.zero;
            collisionObject.transform.localRotation = Quaternion.identity;
            riderCollision = collisionObject.GetComponent<CapsuleCollider>();
            if (riderCollision == null)
            {
                riderCollision = collisionObject.AddComponent<CapsuleCollider>();
            }

            riderCollision.isTrigger = false;
            riderCollision.direction = 1;
            riderCollision.radius = RiderCollisionRadius;
            riderCollision.height = RiderCollisionHeight;
            riderCollision.center = RiderCollisionCenter;
            riderCollision.enabled = HasRider;
        }

        private void UpdateRiderCollisionState()
        {
            EnsureRiderCollision();
            if (riderCollision == null)
            {
                return;
            }

            riderCollision.radius = RiderCollisionRadius;
            riderCollision.height = RiderCollisionHeight;
            riderCollision.center = RiderCollisionCenter;
            riderCollision.enabled = HasRider;
        }
private void ConfigureBody()
        {
            if (body == null)
            {
                return;
            }

            body.mass = 18f;
            body.linearDamping = 0.08f;
            body.angularDamping = 6.5f;
            body.centerOfMass = new Vector3(0f, -0.12f, 0f);
            body.solverIterations = 12;
            body.solverVelocityIterations = 8;
            body.useGravity = true;
            body.isKinematic = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.constraints = RigidbodyConstraints.None;
        }

        private void EnsureRidePoint()
        {
            if (RidePoint != null)
            {
                return;
            }

            Transform existing = transform.Find("Ride Point");
            if (existing != null)
            {
                RidePoint = existing;
                return;
            }

            GameObject ridePoint = new GameObject("Ride Point");
            ridePoint.transform.SetParent(transform, false);
            ridePoint.transform.localPosition = new Vector3(0f, 0.42f, 0f);
            ridePoint.transform.localRotation = Quaternion.identity;
            RidePoint = ridePoint.transform;
        }
    }
}



















