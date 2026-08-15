using System.Globalization;
using Unity.Netcode;
using UnityEngine;

namespace MiniVanGame
{
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(Rigidbody))]
    public class MiniVanHoverboardM : NetworkBehaviour
    {
        public const ulong EmptyClientId = ulong.MaxValue;
        public const string PromptLowBattery = "low battery";
        public const string PromptStillCharging = "still charging";

        [Header("Ride")]
        public Transform RidePoint;
        public float MountRadius = 2.2f;
        public float MaxSpeedKph = 34f;
        public float Acceleration = 32f;
        public float ReverseAcceleration = 18f;
        public float AutoBrakeAcceleration = 5f;
        public float TurnTorque = 32f;
        public float TurnVelocityAssist = 5.5f;
        public float JumpVelocity = 5.2f;
        public float PickupRadius = 2.2f;
        public Vector3 HeldLocalOffset = new Vector3(0.52f, -0.28f, 0.95f);
        public Vector3 HeldLocalEuler = new Vector3(-90f, 0f, 8f);

        [Header("Battery")]
        [Tooltip("Seconds to empty while riding with throttle.")]
        public float RideDrainSecondsToEmpty = 180f;
        [Tooltip("Seconds to empty while standing on the board without throttle.")]
        public float StandDrainSecondsToEmpty = 360f;
        public Transform BatteryDisplayRoot;
        public TextMesh BatteryTextMesh;
        public float BatteryDisplayLocalZ = 0.02f;

        [Header("Rider Collision")]
        public float RiderCollisionRadius = 0.34f;
        public float RiderCollisionHeight = 1.65f;
        public Vector3 RiderCollisionCenter = new Vector3(0f, 1.05f, 0f);

        [Header("Tow Rope")]
        public float TowAttachExtraRadius = 0.35f;
        public float TowRopeLength = 20f;
        public float TowRopeCastRadius = 0.16f;
        public float TowMaxCorrectionPerTick = 0.18f;
        public float TowEmergencyMaxCorrectionPerTick = 0.85f;
        public float TowCornerSlideAssist = 24f;
        public float TowSoftCorrectionSpeed = 7.5f;
        public float TowSoftCorrectionMaxAcceleration = 34f;
        public float TowWrapSlipExcess = 0.35f;
        public int TowHardLimitIterations = 8;
        public bool DebugTowRope = true;
        public float DebugTowRopeInterval = 0.35f;

        [Header("Hover Engines")]
        public Transform[] HoverPoints;
        public float HoverHeight = 0.7f;
        public float HoverRayDistance = 1.45f;
        public float HoverForce = 78f;
        public float HoverDamping = 11f;
        [Tooltip("Extra damping while the board is moving upward (kills ball-like bounce).")]
        public float ReboundDampingMultiplier = 1.85f;
        public float MaxHoverAcceleration = 70f;
        [Tooltip("Must match MiniVanPlayer.Gravity so freefall speed feels the same.")]
        public float MatchPlayerFallGravity = -18f;
        public float JumpGroundGrace = 0.16f;
        public float GroundNormalSharpness = 12f;
        public float GroundSettleSeconds = 0.22f;
        public LayerMask SurfaceMask = ~0;

        [Header("Stability")]
        public float SurfaceAlignTorque = 24f;
        public float UprightTorque = 7f;
        public float AngularDamping = 4.5f;
        public float LandingStabilizeVerticalSpeed = 5.5f;
        [Range(0f, 1f)] public float LandingVerticalRetain = 0.15f;
        public float LandingAngularVelocityLimit = 5.5f;
        public float VisualLeanDegrees = 14f;
        public float LeanSharpness = 8f;
        [Tooltip("How hard an unridden board stops after the rider jumps off.")]
        public float UnriddenBrakeSharpness = 22f;

        [Header("Networking")]
        public float RemoteSmoothTime = 0.08f;
        public float RiderRemoteSmoothTime = 0.035f;
        public float TowRemoteSmoothTime = 0.12f;
        public float RemoteTeleportDistance = 5f;
        public readonly NetworkVariable<ulong> RiderClientId = new NetworkVariable<ulong>(
            EmptyClientId,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        public readonly NetworkVariable<ulong> HolderClientId = new NetworkVariable<ulong>(
            EmptyClientId,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        public readonly NetworkVariable<Vector3> NetworkPosition = new NetworkVariable<Vector3>();
        public readonly NetworkVariable<Quaternion> NetworkRotation = new NetworkVariable<Quaternion>(Quaternion.identity);
        public readonly NetworkVariable<bool> NetworkPoseInitialized = new NetworkVariable<bool>(false);
        public readonly NetworkVariable<bool> TowAttached = new NetworkVariable<bool>(false);
        public readonly NetworkVariable<Vector3> TowAnchorPosition = new NetworkVariable<Vector3>();
        public readonly NetworkVariable<bool> IsOnShelf = new NetworkVariable<bool>(false);
        public readonly NetworkVariable<bool> IsOnCharger = new NetworkVariable<bool>(false);
        public readonly NetworkVariable<float> Battery01 = new NetworkVariable<float>(
            1f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        [Header("Kickflip")]
        [Tooltip("Air clearance above a normal jump that triggers a kickflip (meters).")]
        public float KickflipExtraClearance = 0.4f;
        public float KickflipDuration = 0.58f;
        public float KickflipGroundProbe = 18f;

        [Header("Debug")]
        public bool DebugHoverboardM;
        public float DebugLogInterval = 0.5f;

        private Rigidbody body;
        private Collider[] colliders;
        private float throttleInput;
        private float steerInput;
        private bool jumpRequested;
        private float lastInputTime;
        private float nextDebugLogTime;
        private MiniVanPlayer holderPlayer;
        private Vector3 remoteVelocity;
        private bool localDropPredictionActive;
        private MiniVanTowHook towHook;
        private LineRenderer towRopeRenderer;
        private Material towRopeMaterial;
        private CapsuleCollider riderCollision;
        private MiniVanSkateboardShelf shelf;
        private MiniVanBoardCharger boundCharger;
        private int chargerSlotIndex = -1;
        private Renderer[] hoverboardRenderers = System.Array.Empty<Renderer>();
        private bool[] colliderDefaultEnabledStates = System.Array.Empty<bool>();
        private bool carriedColliderStateEnabled = true;
        private readonly Vector3[] towRopePath = new Vector3[MiniVanTowRopeUtility.MaxPathPoints];
        private readonly MiniVanTowRopeUtility.RopeState towRopeState = new MiniVanTowRopeUtility.RopeState();
        private float nextTowDebugLogTime;
        private Vector3 smoothedSurfaceNormal = Vector3.up;
        private bool grounded;
        private bool wasGrounded;
        private float lastGroundedTime;
        private float groundSettleUntil;
        private float smoothedLean;
        private readonly MiniVanBoardKickflip kickflip = new MiniVanBoardKickflip();
        private bool kickflipAirborne;
        private bool kickflipTriggeredThisAir;
        private float kickflipMaxClearance;
        private float lastShownBattery = -1f;

        public bool HasRider => RiderClientId.Value != EmptyClientId;
        public bool IsHeld => HolderClientId.Value != EmptyClientId;
        public bool IsAvailable => IsSpawned && !HasRider && !IsHeld && !IsOnShelf.Value && !IsOnCharger.Value;
        public bool IsFullyCharged => Battery01.Value >= 0.999f;
        public bool HasBatteryPower => Battery01.Value > 0.001f;
        public bool IsStillChargingOnDock => IsSpawned && IsOnCharger.Value && !IsFullyCharged;
        public bool IsSurfaceGrounded => grounded;
        public MiniVanBoardCharger BoundCharger => boundCharger;
        public int ChargerSlotIndex => chargerSlotIndex;

        public bool ProbeNearGround(float maxDistance = 1.25f)
        {
            Vector3 origin = transform.position + Vector3.up * 0.35f;
            if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, Mathf.Max(0.4f, maxDistance), SurfaceMask, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            return hit.collider == null || !hit.collider.transform.IsChildOf(transform);
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            EnsureRidePoint();
            EnsureHoverPoints();
            EnsureRiderCollision();
            EnsureBatteryDisplay();
            CacheColliders();
            CacheRenderers();
            ConfigureBody();
            BindKickflipVisuals();
        }

        public override void OnNetworkSpawn()
        {
            body = GetComponent<Rigidbody>();
            EnsureRidePoint();
            EnsureHoverPoints();
            EnsureRiderCollision();
            EnsureBatteryDisplay();
            CacheColliders();
            CacheRenderers();
            ConfigureBody();

            Battery01.OnValueChanged += OnBatteryChanged;
            UpdateBatteryDisplayVisual(Battery01.Value);

            if (IsServer)
            {
                if (Battery01.Value <= 0f)
                {
                    Battery01.Value = 1f;
                }

                NetworkPosition.Value = transform.position;
                NetworkRotation.Value = transform.rotation;
                NetworkPoseInitialized.Value = true;
            }
            else if (body != null)
            {
                body.isKinematic = true;
                body.useGravity = false;
            }

            if (IsOnCharger.Value)
            {
                TryRebindChargerFromWorld();
            }
        }

        public override void OnNetworkDespawn()
        {
            Battery01.OnValueChanged -= OnBatteryChanged;
            base.OnNetworkDespawn();
        }

        private void OnBatteryChanged(float previous, float current)
        {
            UpdateBatteryDisplayVisual(current);
        }

        private void Update()
        {
            SetPhysicsCollidersEnabled(!IsHeld && !IsOnShelf.Value && !IsOnCharger.Value || localDropPredictionActive);
            UpdateHeldVisualVisibility();
            UpdateTowRopeVisual();
            UpdateBatteryDisplayVisual(Battery01.Value);

            if (localDropPredictionActive)
            {
                if (!IsHeld)
                {
                    localDropPredictionActive = false;
                }

                remoteVelocity = Vector3.zero;
                return;
            }

            if (IsHeld && MiniVanPlayer.LocalPlayer != null && HolderClientId.Value == MiniVanPlayer.LocalPlayer.OwnerClientId)
            {
                ApplyLocalHeldPose(MiniVanPlayer.LocalPlayer);
                remoteVelocity = Vector3.zero;
                return;
            }

            if (IsServer || body == null)
            {
                return;
            }

            if (!NetworkPoseInitialized.Value)
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

            float smoothTime = RemoteSmoothTime;
            if (HasRider && MiniVanPlayer.LocalPlayer != null && RiderClientId.Value == MiniVanPlayer.LocalPlayer.OwnerClientId)
            {
                smoothTime = RiderRemoteSmoothTime;
            }
            else if (TowAttached.Value)
            {
                smoothTime = TowRemoteSmoothTime;
            }

            transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref remoteVelocity, Mathf.Max(0.001f, smoothTime), Mathf.Infinity, Time.deltaTime);
            float rotationSharpness = HasRider && MiniVanPlayer.LocalPlayer != null && RiderClientId.Value == MiniVanPlayer.LocalPlayer.OwnerClientId ? 34f : TowAttached.Value ? 12f : 18f;
            float blend = 1f - Mathf.Exp(-rotationSharpness * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, blend);
        }

        private void LateUpdate()
        {
            if (localDropPredictionActive)
            {
                return;
            }

            if (IsHeld && MiniVanPlayer.LocalPlayer != null && HolderClientId.Value == MiniVanPlayer.LocalPlayer.OwnerClientId)
            {
                ApplyLocalHeldPose(MiniVanPlayer.LocalPlayer);
            }

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

            if (IsHeld)
            {
                UpdateHeldPose();
                NetworkPosition.Value = transform.position;
                NetworkRotation.Value = transform.rotation;
                if (!NetworkPoseInitialized.Value)
                {
                    NetworkPoseInitialized.Value = true;
                }
                return;
            }

            if (IsOnShelf.Value)
            {
                SimulateShelfed();
                NetworkPosition.Value = transform.position;
                NetworkRotation.Value = transform.rotation;
                if (!NetworkPoseInitialized.Value)
                {
                    NetworkPoseInitialized.Value = true;
                }
                return;
            }

            if (IsOnCharger.Value)
            {
                SimulateChargingDock();
                NetworkPosition.Value = transform.position;
                NetworkRotation.Value = transform.rotation;
                if (!NetworkPoseInitialized.Value)
                {
                    NetworkPoseInitialized.Value = true;
                }
                return;
            }

            if (body.isKinematic)
            {
                body.isKinematic = false;
                body.useGravity = true;
                body.detectCollisions = true;
            }

            if (!HasRider || Time.time - lastInputTime > 0.35f)
            {
                throttleInput = 0f;
                steerInput = 0f;
            }

            if (HasRider)
            {
                TickBatteryDrain(Time.fixedDeltaTime);
            }

            SimulateHover();
            SimulateDrive();
            if (!HasRider)
            {
                BrakeUnriddenBoard();
            }

            UpdateKickflipAirTracking(grounded);
            ApplyTowRopePhysics();
            ApplyHeldWinchCableConstraint();

            NetworkPosition.Value = transform.position;
            NetworkRotation.Value = transform.rotation;
            if (!NetworkPoseInitialized.Value)
            {
                NetworkPoseInitialized.Value = true;
            }
            LogDebug();
        }

        public bool CanMount(MiniVanPlayer player)
        {
            if (player == null || HasRider)
            {
                return false;
            }

            if (HolderClientId.Value == player.OwnerClientId)
            {
                return true;
            }

            return IsAvailable && Vector3.Distance(player.transform.position, transform.position) <= MountRadius;
        }

        public bool CanPickup(MiniVanPlayer player)
        {
            if (player == null || HasRider || IsHeld)
            {
                return false;
            }

            if (IsOnCharger.Value && !IsFullyCharged)
            {
                return false;
            }

            float allowedRadius = (IsOnShelf.Value || IsOnCharger.Value) ? PickupRadius + 1.2f : PickupRadius;
            return Vector3.Distance(player.transform.position, transform.position) <= allowedRadius;
        }

        public bool TryPickup(ulong clientId, MiniVanPlayer player)
        {
            if (!IsServer || !CanPickup(player))
            {
                return false;
            }

            if (IsOnCharger.Value)
            {
                ClearChargerDockState();
            }

            HolderClientId.Value = clientId;
            RiderClientId.Value = EmptyClientId;
            IsOnShelf.Value = false;
            shelf = null;
            DetachTowRope();
            holderPlayer = player;
            throttleInput = 0f;
            steerInput = 0f;
            jumpRequested = false;

            if (body != null)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.detectCollisions = false;
                body.useGravity = false;
                body.isKinematic = true;
            }
            SetPhysicsCollidersEnabled(false);
            SetPhysicsCollidersEnabled(false);

            if (NetworkObject != null && NetworkObject.IsSpawned && NetworkObject.OwnerClientId != clientId)
            {
                NetworkObject.ChangeOwnership(clientId);
            }

            UpdateHeldPose();
            return true;
        }

        public bool TryDrop(ulong clientId, MiniVanPlayer player)
        {
            if (!IsServer || HolderClientId.Value != clientId)
            {
                return false;
            }

            HolderClientId.Value = EmptyClientId;
            IsOnShelf.Value = false;
            IsOnCharger.Value = false;
            shelf = null;
            boundCharger = null;
            chargerSlotIndex = -1;
            holderPlayer = null;
            Vector3 dropPosition = GetDropPosition(player);
            Quaternion dropRotation = player != null ? Quaternion.Euler(0f, player.transform.eulerAngles.y, 0f) : Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            transform.SetPositionAndRotation(dropPosition, dropRotation);

            SetPhysicsCollidersEnabled(true);
            if (body != null)
            {
                body.detectCollisions = true;
                body.isKinematic = false;
                body.useGravity = true;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.WakeUp();
            }

            NetworkPosition.Value = transform.position;
            NetworkRotation.Value = transform.rotation;
            NetworkPoseInitialized.Value = true;
            return true;
        }

        public void BeginLocalDropPrediction(MiniVanPlayer player)
        {
            if (player == null || !IsHeld || HolderClientId.Value != player.OwnerClientId)
            {
                return;
            }

            localDropPredictionActive = true;
            holderPlayer = null;
            Vector3 dropPosition = GetDropPosition(player);
            Quaternion dropRotation = Quaternion.Euler(0f, player.transform.eulerAngles.y, 0f);
            transform.SetPositionAndRotation(dropPosition, dropRotation);
            SetPhysicsCollidersEnabled(true);
            if (body != null)
            {
                body.detectCollisions = true;
                body.isKinematic = false;
                body.useGravity = true;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.WakeUp();
            }
        }

        public bool TryPlaceOnShelf(ulong clientId, MiniVanSkateboardShelf targetShelf)
        {
            if (!IsServer || targetShelf == null || HolderClientId.Value != clientId)
            {
                return false;
            }

            if (!targetShelf.IsInRange(transform.position) && holderPlayer != null && !targetShelf.IsInRange(holderPlayer.transform.position))
            {
                return false;
            }

            HolderClientId.Value = EmptyClientId;
            RiderClientId.Value = EmptyClientId;
            IsOnShelf.Value = true;
            IsOnCharger.Value = false;
            shelf = targetShelf;
            boundCharger = null;
            chargerSlotIndex = -1;
            holderPlayer = null;
            throttleInput = 0f;
            steerInput = 0f;
            jumpRequested = false;
            DetachTowRope();

            SimulateShelfed();

            if (body != null)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.detectCollisions = false;
                body.useGravity = false;
                body.isKinematic = true;
            }
            SetPhysicsCollidersEnabled(false);
            SetPhysicsCollidersEnabled(false);

            if (NetworkObject != null && NetworkObject.IsSpawned && NetworkObject.OwnerClientId == clientId)
            {
                NetworkObject.RemoveOwnership();
            }

            NetworkPosition.Value = transform.position;
            NetworkRotation.Value = transform.rotation;
            NetworkPoseInitialized.Value = true;
            return true;
        }

        public bool TryPlaceOnCharger(ulong clientId, MiniVanBoardCharger charger, int preferredSlotIndex = -1)
        {
            if (!IsServer || charger == null || HolderClientId.Value != clientId)
            {
                return false;
            }

            if (!charger.IsInRange(transform.position) &&
                (holderPlayer == null || !charger.IsInRange(holderPlayer.transform.position)))
            {
                return false;
            }

            int slotIndex = preferredSlotIndex;
            if (slotIndex < 0 || !charger.IsSlotEmpty(slotIndex))
            {
                if (!charger.TryFindEmptySlot(out slotIndex))
                {
                    return false;
                }
            }

            if (!charger.TryGetSlotAnchor(slotIndex, out _))
            {
                return false;
            }

            HolderClientId.Value = EmptyClientId;
            RiderClientId.Value = EmptyClientId;
            IsOnShelf.Value = false;
            IsOnCharger.Value = true;
            shelf = null;
            boundCharger = charger;
            chargerSlotIndex = slotIndex;
            holderPlayer = null;
            throttleInput = 0f;
            steerInput = 0f;
            jumpRequested = false;
            DetachTowRope();
            charger.RegisterDockedBoard(slotIndex, this);
            charger.HidePlacementGhost();

            SimulateChargingDock();

            if (body != null)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.detectCollisions = false;
                body.useGravity = false;
                body.isKinematic = true;
            }

            SetPhysicsCollidersEnabled(false);

            if (NetworkObject != null && NetworkObject.IsSpawned && NetworkObject.OwnerClientId == clientId)
            {
                NetworkObject.RemoveOwnership();
            }

            NetworkPosition.Value = transform.position;
            NetworkRotation.Value = transform.rotation;
            NetworkPoseInitialized.Value = true;
            return true;
        }

        public void ServerApplyCharge(float amount01)
        {
            if (!IsServer || amount01 <= 0f)
            {
                return;
            }

            Battery01.Value = Mathf.Clamp01(Battery01.Value + amount01);
        }

        private void ClearChargerDockState()
        {
            if (boundCharger != null && chargerSlotIndex >= 0)
            {
                boundCharger.UnregisterDockedBoard(chargerSlotIndex, this);
            }

            IsOnCharger.Value = false;
            boundCharger = null;
            chargerSlotIndex = -1;
        }

        private void TryRebindChargerFromWorld()
        {
            MiniVanBoardCharger[] chargers = FindObjectsByType<MiniVanBoardCharger>(FindObjectsSortMode.None);
            MiniVanBoardCharger best = null;
            float bestDistance = float.MaxValue;
            int bestSlot = -1;
            for (int i = 0; i < chargers.Length; i++)
            {
                MiniVanBoardCharger charger = chargers[i];
                if (charger == null)
                {
                    continue;
                }

                for (int slot = 0; slot < MiniVanBoardCharger.SlotCount; slot++)
                {
                    float distance = Vector3.Distance(transform.position, charger.GetSlotWorldPosition(slot));
                    if (distance < bestDistance)
                    {
                        best = charger;
                        bestDistance = distance;
                        bestSlot = slot;
                    }
                }
            }

            if (best == null || bestSlot < 0 || bestDistance > best.InteractRadius + 1.5f)
            {
                return;
            }

            boundCharger = best;
            chargerSlotIndex = bestSlot;
            best.RegisterDockedBoard(bestSlot, this);
        }

        private void TickBatteryDrain(float deltaTime)
        {
            if (!IsServer || !HasRider || IsHeld || IsOnShelf.Value || IsOnCharger.Value)
            {
                return;
            }

            bool throttleActive = Mathf.Abs(throttleInput) > 0.12f;
            float seconds = throttleActive
                ? Mathf.Max(30f, RideDrainSecondsToEmpty)
                : Mathf.Max(30f, StandDrainSecondsToEmpty);
            Battery01.Value = Mathf.Clamp01(Battery01.Value - (deltaTime / seconds));
        }

        private void SimulateChargingDock()
        {
            if (boundCharger == null || chargerSlotIndex < 0)
            {
                TryRebindChargerFromWorld();
            }

            if (boundCharger == null || chargerSlotIndex < 0)
            {
                return;
            }

            boundCharger.GetDockWorldPose(chargerSlotIndex, out Vector3 position, out Quaternion rotation);
            transform.SetPositionAndRotation(position, rotation);
            if (body != null)
            {
                body.position = position;
                body.rotation = rotation;
                if (!body.isKinematic)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
            }
        }

        private void EnsureBatteryDisplay()
        {
            if (BatteryDisplayRoot == null)
            {
                Transform display = transform.Find("HoverBoard Controller/Chassis/Vehicle Mesh Parts/display");
                if (display != null)
                {
                    BatteryDisplayRoot = display;
                }
            }

            if (BatteryTextMesh == null)
            {
                if (BatteryDisplayRoot != null)
                {
                    Transform existing = BatteryDisplayRoot.Find("BatteryPercentText");
                    if (existing != null)
                    {
                        BatteryTextMesh = existing.GetComponent<TextMesh>();
                    }
                }

                if (BatteryTextMesh == null)
                {
                    BatteryTextMesh = GetComponentInChildren<TextMesh>(true);
                }
            }

            if (BatteryTextMesh == null)
            {
                // Prefab should already contain BatteryPercentText under display — do not create at runtime.
                return;
            }

            UpdateBatteryDisplayVisual(IsSpawned ? Battery01.Value : 1f);
        }

        private void UpdateBatteryDisplayVisual(float battery01)
        {
            if (BatteryTextMesh == null)
            {
                EnsureBatteryDisplay();
            }

            if (BatteryTextMesh == null)
            {
                return;
            }

            if (Mathf.Abs(battery01 - lastShownBattery) < 0.0005f && lastShownBattery >= 0f)
            {
                return;
            }

            lastShownBattery = battery01;
            int percent = Mathf.Clamp(Mathf.RoundToInt(battery01 * 100f), 0, 100);
            BatteryTextMesh.text = percent.ToString(CultureInfo.InvariantCulture) + "%";
            BatteryTextMesh.color = Color.Lerp(
                new Color(1f, 0.15f, 0.12f, 1f),
                new Color(0.2f, 1f, 0.25f, 1f),
                Mathf.Clamp01(battery01));
        }

        private void SimulateShelfed()
        {
            if (shelf == null)
            {
                MiniVanSkateboardShelf[] shelves = FindObjectsByType<MiniVanSkateboardShelf>(FindObjectsSortMode.None);
                float bestDistance = float.MaxValue;
                for (int i = 0; i < shelves.Length; i++)
                {
                    if (shelves[i] == null)
                    {
                        continue;
                    }

                    float distance = Vector3.Distance(transform.position, shelves[i].AnchorPosition);
                    if (distance < bestDistance)
                    {
                        shelf = shelves[i];
                        bestDistance = distance;
                    }
                }
            }

            if (shelf == null)
            {
                return;
            }

            Vector3 position = shelf.AnchorPosition + shelf.transform.up * 0.08f;
            Quaternion rotation = shelf.AnchorRotation * Quaternion.Euler(0f, 90f, 0f);
            transform.SetPositionAndRotation(position, rotation);
            if (body != null)
            {
                body.position = position;
                body.rotation = rotation;
                if (!body.isKinematic)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
            }
        }

        private void UpdateHeldPose()
        {
            if (holderPlayer == null || HolderClientId.Value == EmptyClientId)
            {
                MiniVanPlayer[] players = FindObjectsByType<MiniVanPlayer>(FindObjectsSortMode.None);
                for (int i = 0; i < players.Length; i++)
                {
                    if (players[i] != null && players[i].OwnerClientId == HolderClientId.Value)
                    {
                        holderPlayer = players[i];
                        break;
                    }
                }
            }

            if (holderPlayer == null)
            {
                return;
            }

            ApplyHeldPose(holderPlayer);
        }

        public void ApplyLocalHeldPose(MiniVanPlayer player)
        {
            if (player == null || HolderClientId.Value != player.OwnerClientId)
            {
                return;
            }

            ApplyHeldPose(player);
        }

        private void ApplyHeldPose(MiniVanPlayer player)
        {
            if (player == null)
            {
                return;
            }

            Transform reference = player.PlayerCamera != null ? player.PlayerCamera.transform : player.transform;
            Vector3 position = reference.TransformPoint(HeldLocalOffset);
            Quaternion rotation = reference.rotation * Quaternion.Euler(HeldLocalEuler);
            transform.SetPositionAndRotation(position, rotation);
            if (body != null)
            {
                body.position = position;
                body.rotation = rotation;
                if (!body.isKinematic)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
            }
        }

        private Vector3 GetDropPosition(MiniVanPlayer player)
        {
            Transform reference = player != null && player.PlayerCamera != null ? player.PlayerCamera.transform : player != null ? player.transform : transform;
            Vector3 raw = reference.position + Vector3.ProjectOnPlane(reference.forward, Vector3.up).normalized * 1.25f + Vector3.up * 0.5f;
            if (Physics.Raycast(raw + Vector3.up * 2.5f, Vector3.down, out RaycastHit hit, 6f, ~0, QueryTriggerInteraction.Ignore)
                && hit.collider != null
                && !hit.collider.transform.IsChildOf(transform))
            {
                return hit.point + Vector3.up * 0.28f;
            }

            return raw;
        }


        public bool TryMount(ulong clientId, MiniVanPlayer player)
        {
            if (!IsServer || !CanMount(player))
            {
                return false;
            }

            HolderClientId.Value = EmptyClientId;
            IsOnShelf.Value = false;
            if (IsOnCharger.Value)
            {
                ClearChargerDockState();
            }

            shelf = null;
            holderPlayer = null;
            RiderClientId.Value = clientId;
            throttleInput = 0f;
            steerInput = 0f;
            jumpRequested = false;
            lastInputTime = Time.time;

            SetPhysicsCollidersEnabled(true);
            if (body != null)
            {
                body.isKinematic = false;
                body.useGravity = true;
                body.detectCollisions = true;
                body.WakeUp();
            }

            if (NetworkObject != null && NetworkObject.IsSpawned && NetworkObject.OwnerClientId != clientId)
            {
                NetworkObject.ChangeOwnership(clientId);
            }

            Debug.Log("[MiniVanHoverboardM] Mounted client=" + clientId + " pos=" + transform.position.ToString("F2"));
            return true;
        }

        public bool TryDismount(ulong clientId)
        {
            if (!IsServer || RiderClientId.Value != clientId)
            {
                return false;
            }

            RiderClientId.Value = EmptyClientId;
            DetachTowRope();
            throttleInput = 0f;
            steerInput = 0f;
            jumpRequested = false;

            if (NetworkObject != null && NetworkObject.IsSpawned && NetworkObject.OwnerClientId == clientId)
            {
                NetworkObject.RemoveOwnership();
            }

            if (body != null)
            {
                body.detectCollisions = true;
                body.isKinematic = false;
                body.useGravity = true;
                // Stop almost immediately so the rider can pick it back up.
                Vector3 vertical = Vector3.Project(body.linearVelocity, Vector3.up);
                body.linearVelocity = vertical * 0.2f;
                body.angularVelocity = Vector3.zero;
                body.WakeUp();
            }

            Debug.Log("[MiniVanHoverboardM] Dismounted client=" + clientId + " pos=" + transform.position.ToString("F2"));
            return true;
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

        private void ApplyHeldWinchCableConstraint()
        {
            if (!HasRider || body == null)
            {
                return;
            }

            MiniVanPlayer rider = FindPlayerForClient(RiderClientId.Value);
            MiniVanWinchCable.ConstrainBoardWhileHoldingFreeEnd(rider, body);
        }

        private void DetachTowRope()
        {
            towHook = null;
            towRopeState.Clear();
            nextTowDebugLogTime = 0f;
            if (IsServer)
            {
                TowAttached.Value = false;
                TowAnchorPosition.Value = Vector3.zero;
            }

            if (towRopeRenderer != null)
            {
                towRopeRenderer.enabled = false;
            }
        }

        private void ApplyTowRopePhysics()
        {
            if (!IsServer || body == null || !TowAttached.Value)
            {
                return;
            }

            if (towHook == null)
            {
                towHook = MiniVanTowHook.FindNearest(TowAnchorPosition.Value, 0.75f);
                if (towHook == null)
                {
                    DetachTowRope();
                    return;
                }
            }

            int pathCount = 0;
            Vector3 direction = Vector3.zero;
            float totalDistance = 0f;
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
                if (!MiniVanTowRopeUtility.IsFinite(totalDistance) || !MiniVanTowRopeUtility.IsFinite(excess) || direction.sqrMagnitude <= 0.000001f)
                {
                    DetachTowRope();
                    return;
                }

                if (excess <= 0.015f)
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

                float maxStep = badlyStretched ? Mathf.Max(TowMaxCorrectionPerTick, TowEmergencyMaxCorrectionPerTick) : TowMaxCorrectionPerTick;
                float correctionStep = Mathf.Min(excess, Mathf.Min(correctionBudget, maxStep));
                if (badlyStretched || excess > 1.25f)
                {
                    MiniVanTowRopeUtility.MoveBodyWithSliding(body, correctionDirection * correctionStep, transform, towHook.transform.root, 0.018f, 18, TowCornerSlideAssist);
                }
                else
                {
                    Vector3 correctionNormal = correctionDirection.normalized;
                    float targetCorrectionSpeed = Mathf.Min(Mathf.Max(0.25f, TowSoftCorrectionSpeed), excess / Mathf.Max(Time.fixedDeltaTime, 0.001f));
                    Vector3 wantedVelocity = correctionNormal * targetCorrectionSpeed;
                    Vector3 currentAlongCorrection = Vector3.Project(body.linearVelocity, correctionNormal);
                    Vector3 velocityDelta = wantedVelocity - currentAlongCorrection;
                    body.linearVelocity += Vector3.ClampMagnitude(velocityDelta, Mathf.Max(1f, TowSoftCorrectionMaxAcceleration) * Time.fixedDeltaTime);
                }
                correctionBudget -= correctionStep;
                if (correctionBudget <= 0.001f)
                {
                    break;
                }
            }

            if (direction.sqrMagnitude > 0.000001f)
            {
                float velocityAwayFromTarget = -Vector3.Dot(body.linearVelocity, direction);
                if (velocityAwayFromTarget > 0f)
                {
                    body.linearVelocity += direction * velocityAwayFromTarget;
                }
            }

            if (DebugTowRope && Time.time >= nextTowDebugLogTime)
            {
                nextTowDebugLogTime = Time.time + Mathf.Max(0.05f, DebugTowRopeInterval);
                Debug.Log("[MiniVanHoverboardM][TowDebug] pathCount=" + pathCount + " length=" + totalDistance.ToString("0.00") + " limit=" + TowRopeLength.ToString("0.00") + " pos=" + transform.position.ToString("F2"));
            }
        }

        private Vector3 GetTowAttachPosition()
        {
            return transform.position + transform.up * 0.28f;
        }

        private void UpdateTowRopeVisual()
        {
            if (!TowAttached.Value)
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
        public void SubmitInputServerRpc(float throttle, float steer, bool jump, ServerRpcParams rpcParams = default)
        {
            if (RiderClientId.Value != rpcParams.Receive.SenderClientId)
            {
                return;
            }

            throttleInput = Mathf.Clamp(throttle, -1f, 1f);
            steerInput = Mathf.Clamp(steer, -1f, 1f);
            if (jump)
            {
                jumpRequested = true;
            }
            lastInputTime = Time.time;
            if (body != null)
            {
                body.WakeUp();
            }
        }

        public Vector3 GetRidePosition()
        {
            EnsureRidePoint();
            return RidePoint != null ? RidePoint.position : transform.position + transform.up * 0.75f;
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
            Vector3 rawPosition = transform.position + side * 1.65f + Vector3.up * 1.35f;
            if (Physics.Raycast(rawPosition + Vector3.up * 2.5f, Vector3.down, out RaycastHit hit, 6f, ~0, QueryTriggerInteraction.Ignore)
                && hit.collider != null
                && !hit.collider.transform.IsChildOf(transform))
            {
                return hit.point + Vector3.up * 1.2f;
            }
            return rawPosition;
        }

        private void SimulateHover()
        {
            Vector3 normalSum = Vector3.zero;
            int normalCount = 0;
            grounded = false;
            bool settling = Time.time < groundSettleUntil;

            for (int i = 0; i < HoverPoints.Length; i++)
            {
                Transform point = HoverPoints[i];
                if (point == null)
                {
                    continue;
                }

                Vector3 rayDirection = -point.up;
                if (Physics.Raycast(point.position, rayDirection, out RaycastHit hit, HoverRayDistance, SurfaceMask, QueryTriggerInteraction.Ignore)
                    && hit.collider != null
                    && !hit.collider.transform.IsChildOf(transform))
                {
                    grounded = true;
                    float impactSpeed = Vector3.Dot(body.linearVelocity, -hit.normal);
                    if (impactSpeed > LandingStabilizeVerticalSpeed)
                    {
                        float retain = Mathf.Clamp01(LandingVerticalRetain);
                        body.linearVelocity = Vector3.ProjectOnPlane(body.linearVelocity, hit.normal)
                            + Vector3.Project(body.linearVelocity, hit.normal) * retain;
                        body.angularVelocity = Vector3.ClampMagnitude(body.angularVelocity, LandingAngularVelocityLimit);
                        // Only start settle after a hard landing — not every tiny hover contact.
                        if (!wasGrounded)
                        {
                            groundSettleUntil = Time.time + Mathf.Max(0.05f, GroundSettleSeconds);
                        }
                    }

                    normalSum += hit.normal.normalized;
                    normalCount++;

                    float compression = Mathf.Clamp01((HoverHeight - hit.distance) / Mathf.Max(0.05f, HoverHeight));
                    Vector3 pointVelocity = body.GetPointVelocity(point.position);
                    float velocityAlongUp = Vector3.Dot(pointVelocity, point.up);

                    float damp = Mathf.Max(0.01f, HoverDamping);
                    if (velocityAlongUp > 0f)
                    {
                        damp *= Mathf.Max(1f, ReboundDampingMultiplier);
                    }

                    if (settling)
                    {
                        damp *= 1.6f;
                    }

                    // One-way spring only (push up). Pull-down was causing constant jitter.
                    float force = compression * HoverForce - velocityAlongUp * damp;
                    force = Mathf.Clamp(force, 0f, Mathf.Max(1f, MaxHoverAcceleration));
                    body.AddForceAtPosition(point.up * force, point.position, ForceMode.Acceleration);
                }
            }

            if (grounded)
            {
                lastGroundedTime = Time.time;
                if (!wasGrounded && settling)
                {
                    float upSpeed = Vector3.Dot(body.linearVelocity, Vector3.up);
                    if (upSpeed > 0.2f)
                    {
                        body.linearVelocity -= Vector3.up * (upSpeed * 0.75f);
                    }
                }
            }
            else
            {
                // Match on-foot freefall (MiniVanPlayer.Gravity).
                float extraGravity = MatchPlayerFallGravity - Physics.gravity.y;
                if (Mathf.Abs(extraGravity) > 0.01f)
                {
                    body.AddForce(Vector3.up * extraGravity, ForceMode.Acceleration);
                }
            }

            wasGrounded = grounded;

            Vector3 targetNormal = normalCount > 0 ? (normalSum / normalCount).normalized : Vector3.up;
            float normalBlend = 1f - Mathf.Exp(-Mathf.Max(0.1f, GroundNormalSharpness) * Time.fixedDeltaTime);
            smoothedSurfaceNormal = Vector3.Slerp(smoothedSurfaceNormal, targetNormal, normalBlend);

            Vector3 alignAxis = Vector3.Cross(transform.up, smoothedSurfaceNormal);
            if (alignAxis.sqrMagnitude > 0.000001f)
            {
                body.AddTorque(alignAxis * SurfaceAlignTorque, ForceMode.Acceleration);
            }

            if (!grounded)
            {
                Vector3 uprightAxis = Vector3.Cross(transform.up, Vector3.up);
                body.AddTorque(uprightAxis * UprightTorque, ForceMode.Acceleration);
            }

            body.angularVelocity *= Mathf.Exp(-AngularDamping * Time.fixedDeltaTime);
        }

        private void BrakeUnriddenBoard()
        {
            if (body == null)
            {
                return;
            }

            Vector3 up = Vector3.up;
            Vector3 planar = Vector3.ProjectOnPlane(body.linearVelocity, up);
            float vertical = Vector3.Dot(body.linearVelocity, up);
            float factor = Mathf.Exp(-Mathf.Max(1f, UnriddenBrakeSharpness) * Time.fixedDeltaTime);
            planar *= factor;
            if (planar.sqrMagnitude < 0.01f)
            {
                planar = Vector3.zero;
            }

            // Keep a little downward motion so it settles onto hover, but kill sideways coasting.
            body.linearVelocity = up * vertical + planar;
            body.angularVelocity *= factor;
            throttleInput = 0f;
            steerInput = 0f;
        }

        private void SimulateDrive()
        {
            Vector3 surfaceUp = smoothedSurfaceNormal.sqrMagnitude > 0.001f ? smoothedSurfaceNormal.normalized : Vector3.up;
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, surfaceUp);
            if (forward.sqrMagnitude < 0.001f)
            {
                forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            }
            forward = forward.sqrMagnitude > 0.001f ? forward.normalized : transform.forward;

            Vector3 planarVelocity = Vector3.ProjectOnPlane(body.linearVelocity, surfaceUp);
            float maxSpeed = Mathf.Max(1f, MaxSpeedKph / 3.6f);
            float driveThrottle = HasBatteryPower ? throttleInput : 0f;

            if (Mathf.Abs(driveThrottle) > 0.01f)
            {
                float accel = driveThrottle >= 0f ? Acceleration : ReverseAcceleration;
                body.AddForce(forward * driveThrottle * accel, ForceMode.Acceleration);
            }
            else if (grounded && planarVelocity.sqrMagnitude > 0.01f)
            {
                body.AddForce(-planarVelocity.normalized * AutoBrakeAcceleration, ForceMode.Acceleration);
            }

            if (Mathf.Abs(steerInput) > 0.01f)
            {
                body.AddTorque(surfaceUp * steerInput * TurnTorque, ForceMode.Acceleration);
                body.AddForce(Vector3.ProjectOnPlane(transform.right, surfaceUp).normalized * steerInput * TurnVelocityAssist, ForceMode.Acceleration);
            }

            if (jumpRequested && Time.time - lastGroundedTime <= Mathf.Max(0.01f, JumpGroundGrace))
            {
                float currentUpSpeed = Vector3.Dot(body.linearVelocity, surfaceUp);
                float velocityBoost = Mathf.Max(0f, JumpVelocity - currentUpSpeed);
                body.linearVelocity += surfaceUp * velocityBoost;
                grounded = false;
                wasGrounded = false;
                lastGroundedTime = -999f;
                groundSettleUntil = 0f;
            }
            jumpRequested = false;

            planarVelocity = Vector3.ProjectOnPlane(body.linearVelocity, surfaceUp);
            if (planarVelocity.magnitude > maxSpeed)
            {
                body.linearVelocity = planarVelocity.normalized * maxSpeed + Vector3.Project(body.linearVelocity, surfaceUp);
            }

            float targetLean = grounded ? -steerInput * VisualLeanDegrees * Mathf.Clamp01(planarVelocity.magnitude / maxSpeed) : 0f;
            float leanBlend = 1f - Mathf.Exp(-Mathf.Max(0.1f, LeanSharpness) * Time.fixedDeltaTime);
            smoothedLean = Mathf.Lerp(smoothedLean, targetLean, leanBlend);
        }

        private void BindKickflipVisuals()
        {
            Transform chassis = transform.Find("HoverBoard Controller/Chassis");
            Transform meshParts = chassis != null ? chassis.Find("Vehicle Mesh Parts") : null;
            Transform engines = transform.Find("HoverBoard Controller/Engines");
            kickflip.Bind(meshParts != null ? meshParts : chassis, engines);
            kickflip.LocalSpinAxis = Vector3.forward;
            kickflip.Duration = Mathf.Max(0.12f, KickflipDuration);
        }

        private void UpdateKickflipAirTracking(bool groundedNow)
        {
            if (!HasRider || IsHeld || IsOnShelf.Value || IsOnCharger.Value)
            {
                kickflipAirborne = false;
                kickflipTriggeredThisAir = false;
                kickflipMaxClearance = 0f;
                return;
            }

            if (groundedNow)
            {
                kickflipAirborne = false;
                kickflipTriggeredThisAir = false;
                kickflipMaxClearance = 0f;
                return;
            }

            float clearance = MeasureAirClearance(HoverHeight);
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
            Vector3 origin = transform.position + Vector3.up * 0.2f;
            if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, Mathf.Max(2f, KickflipGroundProbe), SurfaceMask, QueryTriggerInteraction.Ignore))
            {
                return Mathf.Max(2f, KickflipGroundProbe);
            }

            if (hit.collider != null && hit.collider.transform.IsChildOf(transform))
            {
                return 0f;
            }

            return Mathf.Max(0f, hit.distance - 0.2f - Mathf.Max(0f, groundedClearance));
        }

        [ClientRpc]
        private void PlayKickflipClientRpc()
        {
            if (!kickflip.IsPlaying)
            {
                kickflip.StartFlip();
            }
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
            ridePoint.transform.localPosition = new Vector3(0f, 0.78f, 0f);
            ridePoint.transform.localRotation = Quaternion.identity;
            RidePoint = ridePoint.transform;
        }

        private void EnsureHoverPoints()
        {
            if (HoverPoints != null && HoverPoints.Length >= 4)
            {
                return;
            }

            HoverPoints = new Transform[4];
            HoverPoints[0] = EnsureHoverPoint("Hover Point FL", new Vector3(-0.42f, 0f, 0.62f));
            HoverPoints[1] = EnsureHoverPoint("Hover Point FR", new Vector3(0.42f, 0f, 0.62f));
            HoverPoints[2] = EnsureHoverPoint("Hover Point RL", new Vector3(-0.42f, 0f, -0.62f));
            HoverPoints[3] = EnsureHoverPoint("Hover Point RR", new Vector3(0.42f, 0f, -0.62f));
        }

        private Transform EnsureHoverPoint(string pointName, Vector3 localPosition)
        {
            Transform existing = transform.Find(pointName);
            if (existing != null)
            {
                return existing;
            }

            GameObject point = new GameObject(pointName);
            point.transform.SetParent(transform, false);
            point.transform.localPosition = localPosition;
            point.transform.localRotation = Quaternion.identity;
            return point.transform;
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

            riderCollision.radius = RiderCollisionRadius;
            riderCollision.height = RiderCollisionHeight;
            riderCollision.center = RiderCollisionCenter;
            riderCollision.direction = 1;
            riderCollision.isTrigger = false;
            riderCollision.enabled = false;
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
            riderCollision.enabled = HasRider && !IsHeld;
        }

        private void CacheColliders()
        {
            colliders = GetComponentsInChildren<Collider>(true);
            colliderDefaultEnabledStates = new bool[colliders.Length];
            for (int i = 0; i < colliders.Length; i++)
            {
                colliderDefaultEnabledStates[i] = colliders[i] != null && colliders[i].enabled;
            }
            carriedColliderStateEnabled = true;
        }

        private void SetPhysicsCollidersEnabled(bool enabled)
        {
            if (colliders == null || colliders.Length == 0)
            {
                CacheColliders();
            }

            if (carriedColliderStateEnabled == enabled)
            {
                return;
            }

            carriedColliderStateEnabled = enabled;
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider == null)
                {
                    continue;
                }

                bool defaultEnabled = colliderDefaultEnabledStates != null && i < colliderDefaultEnabledStates.Length ? colliderDefaultEnabledStates[i] : true;
                collider.enabled = enabled && defaultEnabled;
            }
        }

        private void CacheRenderers()
        {
            hoverboardRenderers = GetComponentsInChildren<Renderer>(true);
        }

        private bool lastHeldVisualShouldShow;
        private bool hasHeldVisualShouldShow;

        private void UpdateHeldVisualVisibility()
        {
            bool shouldShow = true;
            if (IsHeld && !localDropPredictionActive)
            {
                MiniVanPlayer holder = FindPlayerForClient(HolderClientId.Value);
                shouldShow = holder == null || holder.IsInventoryItemSelectedForWorld(MiniVanInventoryItem.HoverboardM);
            }

            if (hasHeldVisualShouldShow && lastHeldVisualShouldShow == shouldShow)
            {
                return;
            }

            hasHeldVisualShouldShow = true;
            lastHeldVisualShouldShow = shouldShow;
            SetHeldVisualVisible(shouldShow);
        }

        private MiniVanPlayer FindPlayerForClient(ulong clientId)
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

        private void SetHeldVisualVisible(bool visible)
        {
            if (hoverboardRenderers == null || hoverboardRenderers.Length == 0)
            {
                CacheRenderers();
            }

            for (int i = 0; i < hoverboardRenderers.Length; i++)
            {
                if (hoverboardRenderers[i] != null)
                {
                    hoverboardRenderers[i].enabled = visible;
                }
            }
        }

        private void ConfigureBody()
        {
            if (body == null)
            {
                return;
            }

            body.mass = 22f;
            body.linearDamping = 0.12f;
            body.angularDamping = 0.55f;
            body.centerOfMass = new Vector3(0f, -0.18f, 0f);
            body.solverIterations = 12;
            body.solverVelocityIterations = 8;
            body.maxAngularVelocity = 18f;
            body.useGravity = true;
            body.isKinematic = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.constraints = RigidbodyConstraints.None;
        }

        private void LogDebug()
        {
            if (!DebugHoverboardM || Time.time < nextDebugLogTime)
            {
                return;
            }

            nextDebugLogTime = Time.time + Mathf.Max(0.1f, DebugLogInterval);
            Debug.Log("[MiniVanHoverboardM] rider=" + RiderClientId.Value
                + " grounded=" + grounded
                + " throttle=" + throttleInput.ToString("0.00")
                + " steer=" + steerInput.ToString("0.00")
                + " pos=" + transform.position.ToString("F2")
                + " vel=" + (body != null ? body.linearVelocity.ToString("F2") : "no-rb")
                + " speedKph=" + (body != null ? (body.linearVelocity.magnitude * 3.6f).ToString("0.0") : "0"));
        }
    }
}


