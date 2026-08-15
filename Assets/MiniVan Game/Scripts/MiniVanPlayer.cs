using Unity.Netcode;
using System.Collections;
using UnityEngine;

namespace MiniVanGame
{
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(CharacterController))]
    public partial class MiniVanPlayer : NetworkBehaviour
    {
        public static MiniVanPlayer LocalPlayer { get; private set; }

        [Header("References")]
        public Camera PlayerCamera;
        public Transform CameraRoot;
        public CharacterController CharacterController;

        [Header("Walking")]
        public float WalkSpeed = 4.2f;
        public float LookSensitivity = 2.2f;
        public float Gravity = -18f;
        public float JumpHeight = 0.85f;
        public float InteractDistance = 4f;
        public float NearbySeatRadius = 1.55f;
        public float GroundedGraceTime = 0.12f;
        public float GroundProbeDistance = 0.22f;

        [Header("Slope Limit")]
        [Tooltip("Hard climb limit in degrees. Jump is allowed up to this angle.")]
        [Range(1f, 89f)] public float MaxWalkableSlopeAngle = 65f;
        [Tooltip("Angle where the player starts sliding downhill.")]
        [Range(1f, 89f)] public float SlopeSlideStartAngle = 50f;
        [Tooltip("Extra degrees below slide start for a short smooth blend-in.")]
        [Min(1f)] public float SlopeSoftZoneDegrees = 3f;
        [Min(0.5f)] public float SteepSlopeSlideSpeed = 6.5f;
        [Min(0.5f)] public float SteepSlopeSlideAcceleration = 14f;
        [Min(0.05f)] public float SteepSlopeProbeDistance = 0.75f;
        [Min(0.1f)] public float SlopeLookAheadDistance = 1.1f;
        [Tooltip("Extra slide speed after landing from a jump on a slope (1.3–1.5 = +30–50%).")]
        [Range(1.1f, 2f)] public float PostJumpSlideSpeedMultiplier = 1.4f;
        [Min(0.05f)] public float PostJumpSlideBoostDuration = 0.55f;

        [Header("Crouching")]
        public float CrouchHeight = 0.70f;
        public float CrouchCameraDrop = 0.92f;
        public float CrouchTransitionSpeed = 8f;
        [Range(0.1f, 1f)] public float CrouchSpeedMultiplier = 0.62f;

        [Header("Gear Shift")]
        public float GearDragSensitivity = 0.13f;

        [Header("Network Movement")]
        [Range(5f, 30f)] public float NetworkTransformRate = 15f;
        [Min(0.001f)] public float NetworkPositionThreshold = 0.02f;
        [Min(0.1f)] public float NetworkRotationThreshold = 1f;

        [Header("Coffee")]
        public float CoffeeBoostDuration = 30f;
        public float CoffeeSpeedMultiplier = 2f;
        public float CoffeePickupRadius = 2.25f;
        public GameObject CoffeeMugPrefab;

        [Header("Bat")]
        public float BatAttackInterval = 0.5f;
        public float BatAttackRange = 2.25f;
        public float BatAttackRadius = 0.55f;
        [Range(0f, 0.95f)] public float BatHitboxStart = 0.32f;
        [Range(0.02f, 0.5f)] public float BatHitboxDuration = 0.16f;
        [Range(0.05f, 0.95f)] public float BatAttackFacingDot = 0.35f;
        public int BatDamage = 1;
        public float BatZombieKnockbackDistance = 2.15f;
        public float BatZombieKnockbackSeconds = 0.2f;
        public Vector3 BatRestPosition = new Vector3(0.46f, -0.48f, 0.86f);
        public Vector3 BatRestRotation = new Vector3(68f, -14f, 18f);
        public Vector3 RemoteBatRestPosition = new Vector3(0.46f, -0.48f, 0.86f);
        public Vector3 RemoteBatRestRotation = new Vector3(68f, -14f, 18f);
        public float RemoteBatScale = 0.78f;
        public Vector3 BatWindupPositionOffset = new Vector3(0f, 0.06f, 0.02f);
        public Vector3 BatWindupRotationOffset = new Vector3(0f, 0f, -26f);
        public Vector3 BatStrikePositionOffset = new Vector3(0f, -0.08f, -0.1f);
        public Vector3 BatStrikeRotationOffset = new Vector3(-8f, -10f, 46f);
        [Range(0.05f, 0.45f)] public float BatWindupEnd = 0.22f;
        [Range(0.15f, 0.85f)] public float BatStrikeEnd = 0.52f;

        [Header("Hot Potato")]
        public float HotPotatoPickupRadius = 2.4f;
        public float HotPotatoPoopSeconds = 20f;
        public float HotPotatoSpeedMultiplier = 1.4f;
        public GameObject HotPotatoPoopPrefab;

        [Header("Winch")]
        public GameObject WinchPickupPrefab;

        [Header("Anton / Stretcher")]
        public GameObject StretcherPickupPrefab;

        [Header("HUD")]
        public Texture2D MiniVanHealthIconImage;
        [Tooltip("Body-only minivan silhouette (no wheels). Falls back to Resources/UI if empty.")]
        public Texture2D MiniVanHudBodyIcon;
        [Tooltip("Single wheel silhouette used for front/rear. Falls back to Resources/UI if empty.")]
        public Texture2D MiniVanHudWheelIcon;
        public Vector2 MiniVanHealthBarOffset = Vector2.zero;
        public Vector2 MiniVanHealthIconOffset = Vector2.zero;
        public Vector2 MiniVanHealthIconSize = new Vector2(58f, 44f);
        public float MiniVanHealthIconGap = 20f;
        public Color MiniVanHudIconOkColor = Color.white;
        public Color MiniVanHudIconLostWheelColor = new Color(0.92f, 0.08f, 0.06f, 1f);

        [Header("Skateboard Camera")]
        public float SkateboardCameraDistance = 4.3f;
        public float SkateboardCameraHeight = 1.55f;
        public float SkateboardCameraLookHeight = 1.05f;
        public float SkateboardCameraPitchMin = -18f;
        public float SkateboardCameraPitchMax = 58f;


        private readonly NetworkVariable<Vector3> networkPosition = new NetworkVariable<Vector3>(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);

        private readonly NetworkVariable<Quaternion> networkRotation = new NetworkVariable<Quaternion>(
            Quaternion.identity,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);

        private readonly NetworkVariable<Vector3> networkHatColor = new NetworkVariable<Vector3>(
            new Vector3(0.12f, 0.62f, 1f),
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> networkHealth = new NetworkVariable<int>(
            5,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> networkSlot0 = new NetworkVariable<int>(
            (int)MiniVanInventoryItem.None,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> networkSlot1 = new NetworkVariable<int>(
            (int)MiniVanInventoryItem.None,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> networkSlot2 = new NetworkVariable<int>(
            (int)MiniVanInventoryItem.None,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> networkSlot3 = new NetworkVariable<int>(
            (int)MiniVanInventoryItem.None,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> networkSelectedSlot = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<float> networkWinchDurability = new NetworkVariable<float>(
            1f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);


        private MiniVanVehicle currentVehicle;
        private MiniVanSeat currentSeat;
        private float pitch;
        private float seatYaw;
        private float verticalVelocity;
        private float lastGroundedTime;
        private float steepSlideSpeed;
        private Vector3 lastGroundNormal = Vector3.up;
        private Vector3 smoothedSlopeNormal = Vector3.up;
        private bool lastGroundWasSteep;
        private bool slopeSlideActive;
        private bool wasAirborneLastWalkFrame;
        private float postJumpSlideBoostUntil = -999f;
        private float postJumpSlideBoostMultiplier = 1f;
        private bool airJumpWantsSlideBoost;
        private float standingControllerHeight;
        private bool fallDamageTracking;
        private float fallApexY;
        private Vector3 standingControllerCenter;
        private float crouchProgress;
        private bool crouchRequested;
        private bool gearDragActive;
        private Vector2 gearDrag;
        private MiniVanGear localGearForUi = MiniVanGear.Neutral;
        private float lastLocalGearChangeTime;
        private bool localHandbrakeLocked;
        private MiniVanVehicle localHandbrakeVehicle;
        private MiniVanVehicle movingPlatformVehicle;
        private Vector3 lastPlatformPosition;
        private Quaternion lastPlatformRotation;
        private bool lastPlatformCarryAlignedToTilt;
        private Vector3 walkingSurfaceUp = Vector3.up;
        private Vector3 smoothedTargetSurfaceUp = Vector3.up;
        private float suppressSurfaceNormalProbeUntil = -999f;
        private float lastCarriedVehicleSeenTime = -999f;
        private MiniVanVehicle ignoredCollisionVehicle;
        private Collider[] ignoredVehicleColliders;
        private float keepVehicleCollisionIgnoredUntil;
        private float nextNetworkTransformSendTime;
        private Vector3 lastPublishedNetworkPosition;
        private Quaternion lastPublishedNetworkRotation = Quaternion.identity;
        private bool hasPublishedNetworkTransform;


        private MiniVanLadder currentLadder;
        private Vector3 lastLadderPosition;
        private Quaternion lastLadderRotation;
        private bool ladderDescending;
        private float ladderDescendStartY;
        private const float MinorPlatformBounceFilter = 0.65f;
        private const float MinorPlatformBounceThreshold = 0.045f;
        private const float MaxPlatformVerticalStep = 0.12f;
        private const float VehicleCarryProbeRadius = 0.42f;
        private const float VehicleCarryStickyGraceTime = 0.75f;
        private const float CabinCarryStickyExtra = 0.42f;
        private const float RoofCarryStickyExtra = 0.85f;
        private const float RoofCarryMinHeight = 2.12f;
        private const float RoofCarryMaxHeight = 3.85f;
        private const float CabinClampMinHeight = 0.86f;
        private const float RoofClampHalfWidth = 1.76f;
        private const float RoofClampFront = 2.88f;
        private const float RoofClampRear = -3.28f;
        private const float RoofHatchCenterX = -0.17f;
        private const float RoofHatchCenterZ = -0.52f;
        private const float RoofHatchHalfWidth = 0.98f;
        private const float RoofHatchHalfLength = 1.12f;
        private const float VehicleExitCollisionGrace = 0.85f;
        private const int VehiclePassengerCollisionIterations = 3;
        private const float VehiclePassengerSkin = 0.012f;
        private const float HotPotatoPoopLocalY = -1.14f;
        private const float WalkingSurfaceAlignSharpness = 8f;
        private const float WalkingSurfaceAirAlignSharpness = 4f;
        private const float WalkingSurfaceRoofAlignSharpness = 7f;
        private const float WalkingSurfaceClimbAlignSharpness = 6f;
        private const float MinWalkableSurfaceUpDot = 0.62f;
        private const float LadderExitSurfaceProbeGrace = 1.1f;
        private const float SurfaceNormalDeadzoneDegrees = 2f;
        // Tilt is visual only (capsule stays upright), so it can track seams quickly without
        // the collider whip that forced the old 35 deg/s cap.
        private const float SurfaceNormalMaxDegreesPerSecond = 120f;
        private const float SurfaceAlignApplyEpsilonDegrees = 0.4f;

        // Camera lags behind sudden grounded height changes (door steps, ladder lips) and
        // catches up smoothly — classic FPS step smoothing.
        private float cameraStepSmoothOffset;
        private const float CameraStepSmoothMaxOffset = 0.42f;
        private const float CameraStepSmoothTriggerRise = 0.02f;
        private const float CameraStepSmoothRecoverSharpness = 9f;
        private const float CameraStepSmoothMinRecoverSpeed = 0.75f;

        // Edge assist: step over low lips and glide along seams instead of catching on them.
        private const float EdgeAssistBlockedRatio = 0.62f;
        private const float EdgeAssistExtraStepHeight = 0.14f;
        private const float EdgeAssistMinPlanarGain = 0.004f;
        private const float EdgeSlideStrength = 0.85f;
        private const float CabinFloorFollowDrop = 0.5f;
        private const float CabinFloorFollowSpeed = 4.5f;
        private const float CabinFloorLiftSpeed = 8f;
        // Door step top sits 0.36 m below the cabin floor top on the MiniVan.
        private const float VehicleSupportSnapUpRange = 0.6f;

        // Door step and cabin floor alternate under the support probe right at the seam; without
        // hysteresis the gravity/cabin-lock mode flips every frame and the player stutters.
        private const float DoorStepStickySeconds = 0.16f;
        private const float DoorStepStickyFlatRadius = 1.05f;
        private float doorStepStickyUntil = -999f;




        private const float LadderProbeRadius = 0.85f;
        private const float LadderExitSupportReach = 1.25f;
        private const float LadderRoofExitBlendSeconds = 0.32f;
        private readonly Collider[] ladderProbeResults = new Collider[24];
        private float ladderRoofExitT = -1f;
        private Vector3 ladderRoofExitStart;
        private Vector3 ladderRoofExitEnd;
        private MiniVanVehicle ladderRoofExitVehicle;
        private MiniVanLadder ladderRoofExitLadder;
        private const float LadderTopExitBlendSeconds = 0.26f;
        private float ladderTopExitT = -1f;
        private Vector3 ladderTopExitStart;
        private Vector3 ladderTopExitEnd;
        private MiniVanLadder ladderTopExitLadder;
        private float measuredPingMs;
        private float smoothedPingMs;
        private float nextPingTime;
        private float lastPingSentTime;
        private const float PingInterval = 1f;
        private const float GoodPingMs = 90f;
        private const float BadPingMs = 180f;



        private MiniVanSeat lookedAtSeat;
        private CoffeeMugPickup lookedAtCoffee;
        private MiniVanBatPickup lookedAtBat;
        private MiniVanSkateboard lookedAtSkateboard;
        private MiniVanHoverboardM lookedAtHoverboardM;
        private MiniVanHoverboardM heldHoverboardM;
        private MiniVanSkateboardShelf lookedAtSkateboardShelf;
        private MiniVanBoardCharger lookedAtBoardCharger;
        private int boardChargerTargetSlot = -1;
        private MiniVanSkateboard heldSkateboard;
        private MiniVanTowCube lookedAtTowCube;
        private MiniVanTowCube heldTowCube;
        private MiniVanWinchPickup lookedAtWinchPickup;
        private MiniVanDoor lookedAtDoor;
        private MiniVanDoor highlightedDoor;
        private float nextInteractionTargetScanTime;
        private const float InteractionTargetScanInterval = 0.14f;
        private readonly RaycastHit[] coreInteractionHits = new RaycastHit[64];
        private const float RidingExitHoldSeconds = 0.55f;
        private bool ridingDropHoldActive;
        private float ridingDropHoldTime;
        private static Texture2D ridingExitHoldRingTexture;
        private static int ridingExitHoldRingBucket = -1;
        private int staticHeldVisualsStateKey = int.MinValue;
        private MiniVanHotPotatoBomb lookedAtHotPotatoBomb;
        private MiniVanHotPotatoBomb heldHotPotatoBomb;
        private MiniVanWoodenBoard lookedAtWoodenBoard;
        private float damWaterSlowDivisor = 1f;
        private bool damWaterImmersed;
        private float damWaterSurfaceY;
        private float damWaterOxygenRemaining = 20f;
        private float nextDamDrownDamageTime;
        private MiniVanSkateboard currentSkateboard;

        private MiniVanHoverboardM currentHoverboardM;
        private int heldSkateboardSlot = -1;
        private int heldCoffeeSlot = -1;
        private int heldHotPotatoBombSlot = -1;
        private int heldHoverboardMSlot = -1;
        private int hotPotatoDropBlockedUntilFrame = -1;
        private int winchUseBlockedUntilFrame = -1;
        private float skateboardCameraYaw;
        private float skateboardCameraPitch;
        private bool hasSkateboardCameraOrbit;
        private Vector3 firstPersonCameraLocalPosition;
        private Quaternion firstPersonCameraLocalRotation;
        private bool hasStoredFirstPersonCamera;
        private Vector3 initialFirstPersonCameraLocalPosition;
        private Quaternion initialFirstPersonCameraLocalRotation;
        private bool hasInitialFirstPersonCameraPose;
        private int localSelectedSlot;
        private float nextLocalBatSwingTime;
        private float nextServerBatSwingTime;
        private float batSwingTimer;
        private float damageFlashUntil;
        private GameObject heldBatVisual;
        private Transform heldBatPivot;

private bool hasCoffee;
        private NetworkObjectReference claimedBatReference;
        private bool claimedBatIsTestBaton;
        private NetworkObjectReference claimedCoffeeReference;
        private Vector3 lastFlamethrowerRackPosition;
        private bool coffeeDrinkActive;
        private float coffeeDrinkTimer;
        private float coffeeBoostEndTime;
        private GameObject heldCoffeeVisual;
        private Transform heldCoffeePivot;
        private Renderer[] heldCoffeeSurfaceRenderers;

        private bool hotPotatoPoopActive;
        private float hotPotatoPoopEndTime;
        private GameObject hotPotatoPoopVisual;
        private Vector3 hotPotatoPoopCameraLocalPosition;
        private Quaternion hotPotatoPoopCameraLocalRotation;
        private bool hasHotPotatoPoopCameraPose;
        private bool hotPotatoBodyHidden;
        private Renderer[] hotPotatoHiddenRenderers = System.Array.Empty<Renderer>();
        private bool[] hotPotatoHiddenRendererStates = System.Array.Empty<bool>();

        private const float CoffeeDrinkSeconds = 1.18f;
        private const float CoffeeSurfaceGoneAt = 0.42f;
        private const float CoffeeReturnStartsAt = 0.46f;
        private static Texture2D gearDotTexture;
        private const int MaxPlayerHealth = 5;
        private const float DamageFlashSeconds = 0.2f;
        private const float FallDamageHeightFactorMild = 2.5f;
        private const float FallDamageHeightFactorLethal = 5f;
        private const float FallDamageHealthFractionMild = 0.25f;
        private const float DefaultPlayerHeightForFall = 1.8f;
        private static Texture2D damageFlashTexture;
        private static Texture2D winchFoldProgressTexture;
        private static int winchFoldProgressBucket = -1;
        private static MiniVanVehicle cachedHudVehicle;

        private const float PlayerHealthBarX = 24f;
        private const float PlayerHealthBarBottom = 86f;
        private const float PlayerHealthBarScreenWidth = 0.13f;
        private const float PlayerHealthBarMinWidth = 150f;
        private const float PlayerHealthBarMaxWidth = 230f;
        private const float PlayerHealthBarHeight = 22f;
        private static readonly Color PlayerHealthBarFillColor = new Color(0.18f, 0.86f, 0.12f);
        private static readonly Color PlayerHealthBarEmptyColor = new Color(0.08f, 0.45f, 0.06f);
        private static readonly Color PlayerOxygenBarFillColor = new Color(0.15f, 0.72f, 0.95f);
        private static readonly Color PlayerOxygenBarEmptyColor = new Color(0.05f, 0.22f, 0.38f);
        private const float DamWaterOxygenSeconds = 20f;
        private const float DamWaterGravity = -3.5f;
        private const float DamWaterIdleSinkSpeed = -1.15f;
        private const float DamWaterRiseAcceleration = 10f;
        private const float DamWaterMaxRiseSpeed = 4.8f;
        private const float DamWaterMaxSinkSpeed = -2.8f;
        private const float DamWaterLookUpRiseThreshold = 0.28f;

        private const float MiniVanHealthBarTop = 16f;
        private const float MiniVanHealthBarScreenWidth = 0.34f;
        private const float MiniVanHealthBarMinWidth = 360f;
        private const float MiniVanHealthBarMaxWidth = 580f;
        private const float MiniVanHealthBarHeight = 28f;
        private const float MiniVanHealthBarCenterOffsetX = 38f;
        private static readonly Color MiniVanHealthBarFillColor = new Color(0.12f, 0.72f, 0.08f);
        private static readonly Color MiniVanHealthBarEmptyColor = new Color(0.62f, 0.04f, 0.03f);
        private static readonly Color HudBarBorderColor = new Color(1f, 1f, 1f, 0.9f);

        public int CurrentHealth => networkHealth.Value;
        public int PlayerMaxHealth => MaxPlayerHealth;
        public MiniVanVehicle CurrentVehicle => currentVehicle;

        public bool IsInsideVehicleCabinForZombieTarget(MiniVanVehicle vehicle)
        {
            if (vehicle == null || !isActiveAndEnabled || IsZombieDead || IsDowned)
            {
                return false;
            }

            if (currentVehicle == vehicle)
            {
                return true;
            }

            Vector3 local = vehicle.transform.InverseTransformPoint(transform.position);
            return Mathf.Abs(local.x) <= RoofClampHalfWidth + 0.45f &&
                   local.z >= RoofClampRear - 0.35f &&
                   local.z <= RoofClampFront + 0.35f &&
                   local.y >= CabinClampMinHeight - 0.55f &&
                   local.y <= RoofCarryMinHeight - 0.08f &&
                   !IsInsideOpenRoofHatchColumn(vehicle, local);
        }

        public override void OnNetworkSpawn()
        {
            CharacterController = CharacterController != null ? CharacterController : GetComponent<CharacterController>();
            ApplyCharacterControllerSlopeLimit();
            EnsurePlayerVisual();

            if (IsServer)
            {
                networkHealth.Value = MaxPlayerHealth;
                InitializeHudIdentityOnServerSpawn();
            }

            InitializeDeathSystemOnNetworkSpawn();

            if (IsOwner)
            {
                LocalPlayer = this;

                if (CharacterController != null)
                {
                    CharacterController.enabled = true;
                }

                MoveToSpawnPoint();
                ConfigureLocalCamera(true);
                StoreInitialFirstPersonCameraPose();
                DisableOverviewCameras();
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                PublishOwnedNetworkTransform(true);
                localSelectedSlot = networkSelectedSlot.Value;
            }
            else
            {
                if (CharacterController != null)
                {
                    // The server needs a live collision proxy for remote players so
                    // authoritative zombie sight, attacks and PvP hitboxes can find them.
                    CharacterController.enabled = IsServer;
                }

                ConfigureLocalCamera(false);
            }

            // Avatar tint + HUD identity hooks (all clients, including owner).
            InitializeHudOnNetworkSpawn();
            InitializeEquipment();
            UpdateCoffeeVisual();
            UpdateHeldBatVisual();
            BindOptimisticInventoryHandlers();
        }

public override void OnNetworkDespawn()
        {
            if (LocalPlayer == this)
            {
                LocalPlayer = null;
            }

            ShutdownHud();
            ShutdownEquipment();

            if (heldCoffeeVisual != null)
            {
                Destroy(heldCoffeeVisual);
                heldCoffeeVisual = null;
                heldCoffeePivot = null;
            }

            SetHotPotatoBodyHidden(false);

            if (heldBatVisual != null)
            {
                Destroy(heldBatVisual);
                heldBatVisual = null;
                heldBatPivot = null;
            }

            if (hotPotatoPoopVisual != null)
            {
                Destroy(hotPotatoPoopVisual);
                hotPotatoPoopVisual = null;
            }

            RestoreVehicleCollisionIgnore();
            ShutdownDeathSystem();

        }

        private void Awake()
        {
            CharacterController = CharacterController != null ? CharacterController : GetComponent<CharacterController>();
            ApplyCharacterControllerSlopeLimit();
            CacheStandingControllerPose();
            EnsurePlayerVisual();
        }

        private void ApplyCharacterControllerSlopeLimit()
        {
            if (CharacterController == null)
            {
                return;
            }

            CharacterController.slopeLimit = Mathf.Clamp(MaxWalkableSlopeAngle, 1f, 89f);
        }

private void Update()
        {
            if (!IsSpawned)
            {
                return;
            }

            if (UpdateDeathSystem())
            {
                StopSnesTelevisionIfNeeded();
                UpdatePingProbe();
                UpdateHudUi();
                UpdatePlayerVisual();
                return;
            }

            if (UpdateKnockdown())
            {
                StopSnesTelevisionIfNeeded();
                UpdatePingProbe();
                UpdateHudUi();
                UpdatePlayerVisual();
                return;
            }

            if (!IsOwner)
            {
                if (currentSeat != null)
                {
                    FollowSeat();
                    UpdateCoffeeVisual();
                    UpdateHeldBatVisual();
                    UpdateAspenStakeHeldVisual();
                    UpdateHolyCrossHeldVisual();
                    UpdateHotPotatoPoopMode();
                    UpdatePlayerVisual();
                    return;
                }

                if (currentHoverboardM != null)
                {
                    FollowHoverboardM();
                    UpdateCoffeeVisual();
                    UpdateHeldBatVisual();
                    UpdateAspenStakeHeldVisual();
                    UpdateHolyCrossHeldVisual();
                    UpdateHotPotatoPoopMode();
                    UpdatePlayerVisual();
                    return;
                }

                if (currentSkateboard != null)
                {
                    FollowSkateboard();
                    UpdateCoffeeVisual();
                    UpdateHeldBatVisual();
                    UpdateAspenStakeHeldVisual();
                    UpdateHolyCrossHeldVisual();
                    UpdateHotPotatoPoopMode();
                    UpdatePlayerVisual();
                    return;
                }

                transform.position = Vector3.Lerp(transform.position, networkPosition.Value, Time.deltaTime * 14f);
                transform.rotation = Quaternion.Slerp(transform.rotation, networkRotation.Value, Time.deltaTime * 14f);
                UpdateCoffeeVisual();
                UpdateHeldBatVisual();
                UpdateAspenStakeHeldVisual();
                UpdateHolyCrossHeldVisual();
                UpdateHotPotatoPoopMode();
                UpdatePlayerVisual();
                return;
            }

            if (MiniVanPauseMenu.IsOpen)
            {
                // Stay glued to seat / moving van; only freeze look and player-controlled motion.
                if (currentSeat != null)
                {
                    FollowSeat();
                    if (currentSeat.IsDriverSeat && currentVehicle != null)
                    {
                        currentVehicle.SubmitDriverInputServerRpc(0f, 0f, 0f, localHandbrakeLocked);
                    }
                }
                else if (currentHoverboardM != null)
                {
                    FollowHoverboardM();
                }
                else if (currentSkateboard != null)
                {
                    FollowSkateboard();
                }
                else
                {
                    ApplyMovingPlatformMotion();
                }

                PublishOwnedNetworkTransform();
                UpdateCoffeeVisual();
                UpdateHeldBatVisual();
                UpdateHotPotatoPoopMode();
                RefreshStaticHeldVisualsIfNeeded();
                UpdatePingProbe();
                UpdateHudUi();
                UpdatePlayerVisual();
                return;
            }

            if (MiniVanGameOverScreen.IsGameOverActive)
            {
                currentLadder = null;
                if (currentSeat != null)
                {
                    FollowSeat();
                }

                PublishOwnedNetworkTransform();
                UpdateCoffeeVisual();
                UpdateHeldBatVisual();
                UpdateHotPotatoPoopMode();
                RefreshStaticHeldVisualsIfNeeded();
                UpdatePingProbe();
                UpdateHudUi();
                UpdatePlayerVisual();
                return;
            }

            UpdateEquipmentInput();
            DecayCameraStepSmoothing();

            if (IsPlayingSnesTelevision())
            {
                currentLadder = null;
                HandleSnesTelevisionMode();
            }
            else if (currentHoverboardM != null)
            {
                HandleHoverboardMMode();
            }
            else if (currentSkateboard != null)
            {
                HandleSkateboardMode();
            }
            else if (currentSeat == null)
            {
                if (IsFacePainting())
                {
                    // Mirror session owns look / cursor; keep transform published only.
                }
                else
                {
                    HandleWalkingLook();
                    UpdateCrouch();

                    if (!HandleLadderClimb())
                    {
                        ApplyMovingPlatformMotion();
                        HandleWalkingMovement();
                        UpdateWalkingSurfaceAlignment();
                        // Body follows the surface; camera keeps a gravity-level horizon (FPS standard).
                        ApplyWalkingCameraLevelHorizon();
                    }
                    else
                    {
                        // Ladder: no surface probing / no horizon counter-tilt fights.
                        UpdateClimbingBodyUpright();
                        ApplyClimbingCameraPitch();
                    }

                    if (!equipmentWindowOpen)
                    {
                        HandleInventoryInput();
                    }

                    UpdateAntonSystem();
                    UpdateAntonLocator();
                    if (!antonInteractionConsumedThisFrame && !equipmentWindowOpen)
                    {
                        HandleInteraction();
                    }

                    UpdateBoardChargerPlacementGhost();
                    UpdateSnesPlacementGhost();

                    if (!BlocksItemUseBecauseAnton() && !equipmentWindowOpen)
                    {
                        HandleBatUse();
                        HandleAspenStakeUse();
                        HandleHolyCrossUse();
                        HandleFlamethrowerUse();
                        HandleFireExtinguisherUse();
                        HandleDefibrillatorUse();
                        HandleCoffeeUse();
                        HandleHotPotatoUse();
                    }
                }
            }
            else
            {
                currentLadder = null;
                FollowSeat();

                if (currentSeat.IsDriverSeat)
                {
                    HandleDriverInput();
                }

                HandleSeatedLook();
                HandleSeatExitInput();
                HandleFireExtinguisherUse();
                HandleDefibrillatorUse();
            }

            EnforceHotPotatoPlayRadius();
            UpdateFallDamageTracking();
            UpdateEquipmentVisibility();
            PublishOwnedNetworkTransform();
            UpdateCoffeeVisual();
            UpdateHeldBatVisual();
            UpdateAspenStakeHeldVisual();
            UpdateHolyCrossHeldVisual();
            UpdateHotPotatoPoopMode();
            RefreshStaticHeldVisualsIfNeeded();
            UpdatePingProbe();
            UpdateHudUi();
            UpdatePlayerVisual();
        }

private void LateUpdate()
        {
            if (!IsSpawned)
            {
                return;
            }

            if (currentSeat != null)
            {
                FollowSeat();
            }
            else if (currentHoverboardM != null)
            {
                FollowHoverboardM();
            }
            else if (currentSkateboard != null)
            {
                FollowSkateboard();
            }
            else
            {
                return;
            }

            if (IsOwner)
            {
                PublishOwnedNetworkTransform();
            }
        }

        private void PublishOwnedNetworkTransform(bool force = false)
        {
            if (!IsOwner || !IsSpawned)
            {
                return;
            }

            float interval = 1f / Mathf.Max(1f, NetworkTransformRate);
            if (!force && Time.unscaledTime < nextNetworkTransformSendTime)
            {
                return;
            }

            Vector3 position = transform.position;
            Quaternion rotation = transform.rotation;
            float positionThresholdSqr = NetworkPositionThreshold * NetworkPositionThreshold;
            bool positionChanged = !hasPublishedNetworkTransform ||
                                   (position - lastPublishedNetworkPosition).sqrMagnitude >= positionThresholdSqr;
            bool rotationChanged = !hasPublishedNetworkTransform ||
                                   Quaternion.Angle(rotation, lastPublishedNetworkRotation) >= NetworkRotationThreshold;
            nextNetworkTransformSendTime = Time.unscaledTime + interval;
            if (!force && !positionChanged && !rotationChanged)
            {
                return;
            }

            networkPosition.Value = position;
            networkRotation.Value = rotation;
            lastPublishedNetworkPosition = position;
            lastPublishedNetworkRotation = rotation;
            hasPublishedNetworkTransform = true;
        }


public void EnterSeatClientSide(MiniVanVehicle vehicle, int seatIndex)
        {
            MiniVanSeat seat = vehicle != null ? vehicle.GetSeat(seatIndex) : null;

            if (seat == null)
            {
                return;
            }

            if (CharacterController != null)
            {
                CharacterController.enabled = false;
            }

            currentVehicle = vehicle;
            RestoreVehicleCollisionIgnore();
            localGearForUi = (MiniVanGear)currentVehicle.CurrentGear.Value;
            lastLocalGearChangeTime = 0f;
            currentSeat = seat;
            seatYaw = 0f;
            pitch = 0f;

            FollowSeat();
        }

public void ExitSeatClientSide(MiniVanVehicle vehicle, int seatIndex)
        {
            if (vehicle != currentVehicle || currentSeat == null || currentSeat.SeatIndex != seatIndex)
            {
                return;
            }

            MiniVanVehicle seatVehicle = currentVehicle;
            MiniVanSeat seat = currentSeat;
            Transform vehicleTransform = seatVehicle != null ? seatVehicle.transform : null;

            Transform exitTransform = seat.ExitPoint != null ? seat.ExitPoint : seat.SitPoint;
            Vector3 standPosition = exitTransform != null ? exitTransform.position : transform.position;

            // Legacy fallback for seats without a dedicated exit point.
            if (seat.ExitPoint == null && vehicleTransform != null)
            {
                Vector3 localPosition = vehicleTransform.InverseTransformPoint(standPosition);
                localPosition.x = Mathf.MoveTowards(localPosition.x, 0f, 0.55f);
                localPosition.y = Mathf.Max(localPosition.y, 1.05f);
                standPosition = vehicleTransform.TransformPoint(localPosition);
            }

            Quaternion standRotation = BuildStandingRotationFromSeatedLook();
            seatYaw = 0f;
            if (CameraRoot != null)
            {
                CameraRoot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            }

            currentVehicle = null;
            currentSeat = null;
            gearDragActive = false;
            verticalVelocity = 0f;

            ApplyStandUpFromSeatPhysics(seatVehicle, standPosition, standRotation);
        }

        private Quaternion BuildStandingRotationFromSeatedLook()
        {
            Vector3 up = GetPreferredWalkingUp(currentVehicle);
            walkingSurfaceUp = up;
            smoothedTargetSurfaceUp = up;
            Vector3 lookForward = transform.rotation * Quaternion.Euler(0f, seatYaw, 0f) * Vector3.forward;
            lookForward = Vector3.ProjectOnPlane(lookForward, up);
            if (lookForward.sqrMagnitude > 0.001f)
            {
                return Quaternion.LookRotation(lookForward.normalized, up);
            }

            return AlignRotationToUp(transform.rotation, up);
        }

        private void ApplyStandUpFromSeatPhysics(MiniVanVehicle vehicle, Vector3 standPosition, Quaternion standRotation)
        {
            if (CharacterController != null)
            {
                CharacterController.enabled = false;
            }

            transform.SetPositionAndRotation(standPosition, standRotation);

            if (vehicle != null)
            {
                movingPlatformVehicle = vehicle;
                lastPlatformPosition = vehicle.transform.position;
                lastPlatformRotation = GetVehicleCarryRotation(vehicle);
                lastPlatformCarryAlignedToTilt = ShouldAlignWalkingToVehicleTilt(vehicle);
                UpdateVehicleCollisionIgnore(vehicle);
                SnapToVehicleSupportSurface(vehicle);
            }

            if (CharacterController != null)
            {
                CharacterController.enabled = true;
            }

            if (vehicle != null)
            {
                ResolveIgnoredVehiclePenetrations();
                ClampToCarriedVehicleBounds(vehicle);
            }
        }

        public void ServerApplySeatPhysics(MiniVanVehicle vehicle, int seatIndex, bool seated)
        {
            if (!IsServer || IsOwner)
            {
                return;
            }

            MiniVanSeat seat = vehicle != null ? vehicle.GetSeat(seatIndex) : null;
            if (seat == null)
            {
                return;
            }

            if (seated)
            {
                if (CharacterController != null)
                {
                    CharacterController.enabled = false;
                }

                RestoreVehicleCollisionIgnore();

                Transform sitPoint = seat.SitPoint;
                if (sitPoint != null)
                {
                    transform.SetPositionAndRotation(sitPoint.position, sitPoint.rotation);
                }

                return;
            }

            Transform exitTransform = seat.ExitPoint != null ? seat.ExitPoint : seat.SitPoint;
            Vector3 standPosition = exitTransform != null ? exitTransform.position : transform.position;
            Quaternion standRotation = exitTransform != null ? exitTransform.rotation : transform.rotation;
            ApplyStandUpFromSeatPhysics(vehicle, standPosition, standRotation);
        }

        private void SnapToVehicleSupportSurface(MiniVanVehicle vehicle)
        {
            if (vehicle == null || CharacterController == null || !TryGetVehicleSupportHit(vehicle, out RaycastHit supportHit))
            {
                return;
            }

            Vector3 up = GetSupportUp(supportHit, vehicle);
            walkingSurfaceUp = up;
            smoothedTargetSurfaceUp = up;
            float footOffset = CharacterController.height * 0.5f - CharacterController.center.y + Mathf.Max(CharacterController.skinWidth, 0.025f);
            Vector3 desiredRoot = supportHit.point + up * footOffset;
            Vector3 position = transform.position;
            float alongUp = Vector3.Dot(desiredRoot - position, up);
            if (alongUp > 0f)
            {
                position += up * alongUp;
                transform.position = position;
            }

            transform.rotation = AlignRotationToUp(transform.rotation, up);
        }


[ClientRpc]
        public void SetSeatStateClientRpc(NetworkObjectReference vehicleReference, int seatIndex, bool seated)
        {
            if (IsOwner)
            {
                return;
            }

            if (CharacterController != null)
            {
                CharacterController.enabled = false;
            }

            if (!seated)
            {
                if (currentSeat != null && currentSeat.SeatIndex == seatIndex)
                {
                    currentVehicle = null;
                    currentSeat = null;
                }

                return;
            }

            if (!vehicleReference.TryGet(out NetworkObject vehicleObject))
            {
                return;
            }

            MiniVanVehicle vehicle = vehicleObject.GetComponent<MiniVanVehicle>();
            MiniVanSeat seat = vehicle != null ? vehicle.GetSeat(seatIndex) : null;
            if (seat == null)
            {
                return;
            }

            currentVehicle = vehicle;
            currentSeat = seat;
            seatYaw = 0f;
            pitch = 0f;

            FollowSeat();
        }


        private void HandleWalkingLook()
        {
            if (IsPizzaChestOpen() || IsFacePainting() || equipmentWindowOpen)
            {
                return;
            }

            float mouseX = Input.GetAxis("Mouse X") * LookSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * LookSensitivity;

            // Yaw around surface up so movement stays natural on slopes.
            // Camera pitch is applied later via ApplyWalkingCameraLevelHorizon.
            transform.Rotate(GetWalkingYawAxis(), mouseX, Space.World);
            pitch = Mathf.Clamp(pitch - mouseY, -82f, 82f);
        }

        private void HandleSeatedLook()
        {
            if (IsPizzaChestOpen() || equipmentWindowOpen)
            {
                return;
            }

            if (gearDragActive)
            {
                return;
            }

            float mouseX = Input.GetAxis("Mouse X") * LookSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * LookSensitivity;

            seatYaw = Mathf.Clamp(seatYaw + mouseX, -115f, 115f);
            pitch = Mathf.Clamp(pitch - mouseY, -72f, 72f);
            CameraRoot.localRotation = Quaternion.Euler(pitch, seatYaw, 0f);
        }

private void HandleWalkingMovement()
        {
            currentLadder = null;
            Vector3 walkStartPosition = transform.position;
            Vector3 input = new Vector3(MiniVanKeyBindings.MoveHorizontal(), 0f, MiniVanKeyBindings.MoveVertical());
            input = Vector3.ClampMagnitude(input, 1f);

            bool carriedByVehicle = movingPlatformVehicle != null;
            bool inOpenHatchShaft = carriedByVehicle && IsInOpenHatchFreeFall(movingPlatformVehicle);
            bool standingOnVehicleRoof = carriedByVehicle && !inOpenHatchShaft &&
                (IsPlayerAtVehicleRoofHeight(movingPlatformVehicle, transform.position) ||
                 IsStandingOnVehicleRoof(movingPlatformVehicle));
            bool standingOnDoorStep = carriedByVehicle && !inOpenHatchShaft &&
                IsStandingOnVehicleDoorStep(movingPlatformVehicle);
            // Cabin walk locks vertical motion; roof / doorstep / hatch free-fall use normal gravity + jump.
            bool lockedInsideMovingVehicle = carriedByVehicle &&
                !standingOnVehicleRoof &&
                !standingOnDoorStep &&
                !inOpenHatchShaft;

            Quaternion walkBasis = GetWalkingYawRotation();
            Vector3 movePlanar = walkBasis * new Vector3(input.x, 0f, input.z);
            if (movePlanar.sqrMagnitude > 0.001f)
            {
                movePlanar.Normalize();
            }

            EvaluateSlopeState(
                movePlanar,
                out float feetAngle,
                out float aheadAngle,
                out Vector3 feetNormal,
                out Vector3 aheadNormal);

            float limit = Mathf.Clamp(MaxWalkableSlopeAngle, 1f, 89f);
            float slideStart = Mathf.Clamp(SlopeSlideStartAngle, 1f, limit);
            float softZone = Mathf.Clamp(SlopeSoftZoneDegrees, 1f, 20f);
            float softStart = Mathf.Max(1f, slideStart - softZone);
            float exitAngle = Mathf.Max(1f, softStart - 1f);

            float controlAngle = Mathf.Max(feetAngle, aheadAngle * 0.9f);
            float uphillFade = 1f - Mathf.InverseLerp(softStart, limit, controlAngle);
            uphillFade = Mathf.SmoothStep(0f, 1f, uphillFade);

            // Start sliding from slideStart (default 50°); hard "too steep to stay" remains at limit (65°).
            bool wantSlide = feetAngle >= slideStart || (slopeSlideActive && feetAngle >= exitAngle);
            if (wantSlide)
            {
                slopeSlideActive = true;
                lastGroundWasSteep = feetAngle >= limit || aheadAngle >= limit;
            }
            else if (feetAngle < exitAngle && aheadAngle < softStart)
            {
                slopeSlideActive = false;
                lastGroundWasSteep = false;
            }

            Vector3 targetNormal = feetAngle >= aheadAngle ? feetNormal : aheadNormal;
            if (targetNormal.sqrMagnitude > 0.001f)
            {
                smoothedSlopeNormal = Vector3.Slerp(smoothedSlopeNormal, targetNormal.normalized, 1f - Mathf.Exp(-10f * Time.deltaTime));
                lastGroundNormal = smoothedSlopeNormal;
            }

            bool physicallyGrounded = CharacterController != null && CharacterController.isGrounded;
            if (!physicallyGrounded)
            {
                physicallyGrounded = ProbeGroundContact(out _);
            }

            bool groundedForJump = IsGroundedForJump() || (physicallyGrounded && controlAngle <= limit + 0.01f);
            bool landingThisFrame = wasAirborneLastWalkFrame && (groundedForJump || physicallyGrounded || slopeSlideActive);

            // 3A: landing on soft+ slope → immediate slide, further than a normal stand-slide.
            if (landingThisFrame && (feetAngle >= softStart || aheadAngle >= softStart || airJumpWantsSlideBoost))
            {
                slopeSlideActive = true;
                lastGroundWasSteep = feetAngle >= limit || aheadAngle >= limit;
                float landMult = airJumpWantsSlideBoost
                    ? Mathf.Max(1.2f, postJumpSlideBoostMultiplier)
                    : 1.25f;
                steepSlideSpeed = Mathf.Max(steepSlideSpeed, SteepSlopeSlideSpeed * 0.55f * landMult);
                if (airJumpWantsSlideBoost)
                {
                    postJumpSlideBoostUntil = Time.time + PostJumpSlideBoostDuration;
                }

                airJumpWantsSlideBoost = false;
                if (verticalVelocity > 0f && physicallyGrounded)
                {
                    verticalVelocity = Mathf.MoveTowards(verticalVelocity, 0f, 28f * Time.deltaTime);
                }
            }
            else if (landingThisFrame)
            {
                airJumpWantsSlideBoost = false;
            }

            if (physicallyGrounded)
            {
                lastGroundedTime = Time.time;
            }

            bool canUseGroundedGrace = physicallyGrounded || groundedForJump ||
                Time.time - lastGroundedTime <= GroundedGraceTime;
            bool swimming = damWaterImmersed && !lockedInsideMovingVehicle;

            if (lockedInsideMovingVehicle)
            {
                verticalVelocity = 0f;
                steepSlideSpeed = 0f;
                slopeSlideActive = false;
                airJumpWantsSlideBoost = false;
                postJumpSlideBoostUntil = -999f;
            }
            else if (swimming)
            {
                ApplyDamWaterSwimVertical(input);
                steepSlideSpeed = 0f;
                slopeSlideActive = false;
                airJumpWantsSlideBoost = false;
            }
            else if (slopeSlideActive && physicallyGrounded && verticalVelocity > 0f)
            {
                // Only bleed upward stick while grounded — never kill an in-air jump.
                verticalVelocity = Mathf.MoveTowards(verticalVelocity, 0f, 16f * Time.deltaTime);
            }
            else if (canUseGroundedGrace && verticalVelocity < 0f && !slopeSlideActive)
            {
                verticalVelocity = -1f;
            }

            // 1B: jump allowed on slopes <= limit; uphill jump arms a stronger post-land slide.
            if (!swimming && !lockedInsideMovingVehicle &&
                canUseGroundedGrace && MiniVanKeyBindings.GetKeyDown(MiniVanKeyAction.Jump) && !IsStretcherPinned)
            {
                if (TryResolveJumpOnSlope(
                        movePlanar,
                        feetAngle,
                        aheadAngle,
                        limit,
                        softStart,
                        feetNormal,
                        aheadNormal,
                        out bool uphillJump))
                {
                    verticalVelocity = Mathf.Sqrt(JumpHeight * -2f * Gravity);
                    lastGroundedTime = -999f;
                    airJumpWantsSlideBoost = controlAngle >= softStart || uphillJump;
                    postJumpSlideBoostMultiplier = uphillJump
                        ? Mathf.Clamp(PostJumpSlideSpeedMultiplier * 1.08f, 1.35f, 1.55f)
                        : Mathf.Clamp(PostJumpSlideSpeedMultiplier, 1.3f, 1.5f);
                }
            }

            if (!swimming && !lockedInsideMovingVehicle)
            {
                verticalVelocity += Gravity * Time.deltaTime;
            }

            float speedMultiplier = Time.time < coffeeBoostEndTime ? CoffeeSpeedMultiplier : 1f;
            speedMultiplier *= Mathf.Lerp(1f, Mathf.Clamp(CrouchSpeedMultiplier, 0.1f, 1f), crouchProgress);
            if (heldHotPotatoBomb != null && HasInventoryItem(MiniVanInventoryItem.HotPotatoBomb))
            {
                speedMultiplier *= Mathf.Max(0.1f, HotPotatoSpeedMultiplier);
            }

            if (damWaterSlowDivisor > 1f)
            {
                speedMultiplier /= damWaterSlowDivisor;
            }

            speedMultiplier *= GetAntonSpeedMultiplier();

            Vector3 motion = walkBasis * input * WalkSpeed * speedMultiplier;
            if (IsStretcherPinned)
            {
                motion.x = 0f;
                motion.z = 0f;
            }

            if (swimming)
            {
                float lookUp = PlayerCamera != null ? PlayerCamera.transform.forward.y : 0f;
                if (input.z > 0.01f && lookUp > DamWaterLookUpRiseThreshold)
                {
                    float flatten = 1f - Mathf.Clamp01((lookUp - DamWaterLookUpRiseThreshold) / 0.7f);
                    motion *= Mathf.Lerp(0.25f, 1f, flatten);
                }
            }
            else if (!lockedInsideMovingVehicle)
            {
                motion = ApplySlopeMotion(motion, uphillFade, softStart, feetAngle, limit);
            }

            if (carriedByVehicle)
            {
                if (!lockedInsideMovingVehicle)
                {
                    motion.y = verticalVelocity;
                }

                Vector3 delta = MiniVanWinchCable.ClampPlayerDeltaWhileHoldingFreeEnd(
                    this,
                    MiniVanStretcher.ClampCarrierDelta(this, motion * Time.deltaTime));
                MoveWithEdgeAssist(delta, !inOpenHatchShaft);

                if (lockedInsideMovingVehicle || (verticalVelocity <= 0f && !inOpenHatchShaft))
                {
                    ClampToCarriedVehicleBounds(movingPlatformVehicle, lockedInsideMovingVehicle);
                }
                else if (inOpenHatchShaft && verticalVelocity <= 0f)
                {
                    TryLandFromHatchFall(movingPlatformVehicle);
                }

                ResolveIgnoredVehiclePenetrations();
                wasAirborneLastWalkFrame = !CharacterController.isGrounded && verticalVelocity > 0.05f;
                UpdateCameraStepSmoothing(walkStartPosition);
                return;
            }

            // One Move only — no post-move height rollback (that caused camera/body jerk).
            motion.y = verticalVelocity;
            MoveWithEdgeAssist(
                MiniVanWinchCable.ClampPlayerDeltaWhileHoldingFreeEnd(
                    this,
                    MiniVanStretcher.ClampCarrierDelta(this, motion * Time.deltaTime)),
                canUseGroundedGrace && !slopeSlideActive);

            wasAirborneLastWalkFrame = !physicallyGrounded && !groundedForJump && verticalVelocity > 0.05f;
            UpdateCameraStepSmoothing(walkStartPosition);
        }

        /// <summary>
        /// CharacterController move with edge assist: low lips (door step, ladder lip, cabin seam)
        /// are stepped over, and blocking corners deflect the motion instead of stopping it dead.
        /// </summary>
        private void MoveWithEdgeAssist(Vector3 delta, bool allowStepAssist)
        {
            if (CharacterController == null || !CharacterController.enabled)
            {
                transform.position += delta;
                return;
            }

            Vector3 beforeMove = transform.position;
            CharacterController.Move(delta);

            Vector3 desiredPlanar = new Vector3(delta.x, 0f, delta.z);
            float desiredDistance = desiredPlanar.magnitude;
            if (desiredDistance < 0.0005f)
            {
                return;
            }

            Vector3 achievedPlanar = transform.position - beforeMove;
            achievedPlanar.y = 0f;
            if (achievedPlanar.magnitude >= desiredDistance * EdgeAssistBlockedRatio)
            {
                return;
            }

            Vector3 remaining = desiredPlanar - achievedPlanar;
            if (remaining.magnitude < EdgeAssistMinPlanarGain)
            {
                return;
            }

            if (allowStepAssist && TryStepOverLedge(remaining))
            {
                return;
            }

            SlideAlongBlockingEdge(remaining);
        }

        private bool TryStepOverLedge(Vector3 remaining)
        {
            float stepHeight = Mathf.Max(0.05f, CharacterController.stepOffset) + EdgeAssistExtraStepHeight;
            // The cabin roof is low; lifting the capsule into it would trade a snag for a wedge.
            if (!HasHeadroomForStep(stepHeight))
            {
                return false;
            }

            Vector3 savedPosition = transform.position;

            CharacterController.enabled = false;
            transform.position = savedPosition + Vector3.up * stepHeight;
            CharacterController.enabled = true;

            CharacterController.Move(remaining);
            CharacterController.Move(Vector3.down * (stepHeight + 0.02f));

            Vector3 gained = transform.position - savedPosition;
            float planarGain = new Vector3(gained.x, 0f, gained.z).magnitude;
            bool climbedSaneHeight = gained.y <= stepHeight + 0.05f && gained.y >= -stepHeight;
            if (planarGain > EdgeAssistMinPlanarGain && climbedSaneHeight)
            {
                return true;
            }

            CharacterController.enabled = false;
            transform.position = savedPosition;
            CharacterController.enabled = true;
            return false;
        }

        private bool HasHeadroomForStep(float stepHeight)
        {
            GetWorldCapsule(out Vector3 bottom, out Vector3 top, out float radius);
            if (!Physics.CapsuleCast(
                    bottom,
                    top,
                    radius,
                    Vector3.up,
                    out RaycastHit hit,
                    stepHeight + CharacterController.skinWidth,
                    ~0,
                    QueryTriggerInteraction.Ignore))
            {
                return true;
            }

            return hit.collider == null || hit.collider.transform.IsChildOf(transform);
        }

        private void GetWorldCapsule(out Vector3 bottom, out Vector3 top, out float radius)
        {
            float horizontalScale = Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.z));
            radius = Mathf.Max(0.02f, CharacterController.radius * horizontalScale - CharacterController.skinWidth);
            float height = Mathf.Max(
                CharacterController.height * Mathf.Abs(transform.lossyScale.y),
                radius * 2f);
            Vector3 center = transform.TransformPoint(CharacterController.center);
            float halfSegment = Mathf.Max(0f, height * 0.5f - radius);
            bottom = center - Vector3.up * halfSegment;
            top = center + Vector3.up * halfSegment;
        }

        private void SlideAlongBlockingEdge(Vector3 remaining)
        {
            if (!TryGetBlockingEdgeNormal(remaining, out Vector3 normal))
            {
                return;
            }

            Vector3 slide = Vector3.ProjectOnPlane(remaining, normal);
            slide.y = 0f;
            if (slide.sqrMagnitude < 0.0000001f)
            {
                return;
            }

            CharacterController.Move(slide * EdgeSlideStrength);
        }

        private bool TryGetBlockingEdgeNormal(Vector3 direction, out Vector3 normal)
        {
            normal = Vector3.zero;
            GetWorldCapsule(out Vector3 bottom, out Vector3 top, out float radius);
            float castDistance = direction.magnitude + CharacterController.skinWidth + 0.02f;

            if (!Physics.CapsuleCast(
                    bottom,
                    top,
                    radius,
                    direction.normalized,
                    out RaycastHit hit,
                    castDistance,
                    ~0,
                    QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            if (hit.collider == null || hit.collider.transform.IsChildOf(transform))
            {
                return false;
            }

            normal = hit.normal;
            normal.y = 0f;
            return normal.sqrMagnitude > 0.0000001f;
        }

        private Vector3 ApplySlopeMotion(Vector3 walkMotion, float uphillFade, float softStart, float feetAngle, float limit)
        {
            Vector3 normal = smoothedSlopeNormal.sqrMagnitude > 0.001f ? smoothedSlopeNormal.normalized : Vector3.up;
            Vector3 slopeUp = Vector3.ProjectOnPlane(Vector3.up, normal);
            Vector3 downhill = Vector3.ProjectOnPlane(Vector3.down, normal);

            if (slopeUp.sqrMagnitude < 0.0001f || downhill.sqrMagnitude < 0.0001f)
            {
                steepSlideSpeed = Mathf.MoveTowards(steepSlideSpeed, 0f, SteepSlopeSlideAcceleration * Time.deltaTime);
                return walkMotion;
            }

            slopeUp.Normalize();
            downhill.Normalize();

            // Keep almost-full planar control; convert height-gaining intent into downhill slide.
            Vector3 planarWalk = new Vector3(walkMotion.x, 0f, walkMotion.z);
            float climbAmount = Vector3.Dot(planarWalk, slopeUp);
            float convertedSlide = 0f;
            if (climbAmount > 0f)
            {
                float strip;
                if (feetAngle >= limit || slopeSlideActive)
                {
                    strip = 1f;
                }
                else if (feetAngle >= softStart)
                {
                    strip = 1f - uphillFade;
                }
                else
                {
                    strip = 0f;
                }

                if (strip > 0f)
                {
                    planarWalk -= slopeUp * (climbAmount * strip);
                    convertedSlide = climbAmount * strip * 0.9f;
                    if (strip > 0.35f)
                    {
                        slopeSlideActive = true;
                    }
                }
            }

            float slideBlend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(softStart, limit + 4f, feetAngle));
            if (slopeSlideActive && feetAngle >= softStart)
            {
                slideBlend = Mathf.Max(slideBlend, 0.28f);
            }

            float boost = Time.time < postJumpSlideBoostUntil
                ? Mathf.Max(1f, postJumpSlideBoostMultiplier)
                : 1f;
            float targetSlide = (SteepSlopeSlideSpeed * slideBlend + convertedSlide) * boost;
            if (!slopeSlideActive && slideBlend < 0.08f && convertedSlide < 0.05f)
            {
                targetSlide = 0f;
            }

            steepSlideSpeed = Mathf.MoveTowards(
                steepSlideSpeed,
                targetSlide,
                SteepSlopeSlideAcceleration * Time.deltaTime);

            Vector3 result = planarWalk + downhill * steepSlideSpeed;

            // Softly bias vertical only while grounded sliding — avoid fighting jump arcs.
            if (steepSlideSpeed > 0.01f && CharacterController != null && CharacterController.isGrounded)
            {
                float slideY = downhill.y * steepSlideSpeed;
                if (slideY < 0f && verticalVelocity <= 0.05f)
                {
                    verticalVelocity = Mathf.Min(verticalVelocity, Mathf.Lerp(verticalVelocity, slideY, 0.22f));
                }
            }

            return result;
        }

        private bool TryResolveJumpOnSlope(
            Vector3 movePlanar,
            float feetAngle,
            float aheadAngle,
            float limit,
            float softStart,
            Vector3 feetNormal,
            Vector3 aheadNormal,
            out bool uphillJump)
        {
            uphillJump = false;
            float worstAngle = Mathf.Max(feetAngle, aheadAngle);

            // Only block jump past the hard limit — convert the press into a soft slide cue.
            if (worstAngle > limit + 0.01f)
            {
                ApplySoftDeniedJumpSlide(feetAngle >= aheadAngle ? feetNormal : aheadNormal);
                return false;
            }

            Vector3 slopeNormal = feetAngle >= aheadAngle ? feetNormal : aheadNormal;
            if (slopeNormal.sqrMagnitude < 0.001f)
            {
                slopeNormal = smoothedSlopeNormal;
            }

            if (slopeNormal.sqrMagnitude > 0.001f && worstAngle >= softStart)
            {
                slopeNormal.Normalize();
                Vector3 slopeUp = Vector3.ProjectOnPlane(Vector3.up, slopeNormal);
                if (slopeUp.sqrMagnitude > 0.0001f)
                {
                    slopeUp.Normalize();
                    Vector3 intent = movePlanar;
                    if (intent.sqrMagnitude < 0.001f)
                    {
                        intent = transform.forward;
                        intent.y = 0f;
                    }

                    if (intent.sqrMagnitude > 0.001f)
                    {
                        intent.Normalize();
                        uphillJump = Vector3.Dot(intent, slopeUp) > 0.08f;
                    }
                    else
                    {
                        // Stationary jump on a soft/steep face still counts as "trying to stay up".
                        uphillJump = true;
                    }
                }
            }

            return true;
        }

        private void ApplySoftDeniedJumpSlide(Vector3 slopeNormal)
        {
            Vector3 normal = slopeNormal.sqrMagnitude > 0.001f ? slopeNormal.normalized : Vector3.up;
            if (normal.sqrMagnitude > 0.001f)
            {
                smoothedSlopeNormal = Vector3.Slerp(smoothedSlopeNormal, normal, 0.35f);
                lastGroundNormal = smoothedSlopeNormal;
            }

            slopeSlideActive = true;
            lastGroundWasSteep = true;
            steepSlideSpeed = Mathf.Max(steepSlideSpeed, SteepSlopeSlideSpeed * 0.45f);
            postJumpSlideBoostUntil = Time.time + PostJumpSlideBoostDuration * 0.65f;
            postJumpSlideBoostMultiplier = Mathf.Clamp(PostJumpSlideSpeedMultiplier, 1.3f, 1.5f);
        }

        private bool ProbeGroundContact(out RaycastHit hit)
        {
            hit = default;
            if (CharacterController == null || !CharacterController.enabled)
            {
                return false;
            }

            Vector3 sphereCenter = transform.position + CharacterController.center +
                Vector3.down * (CharacterController.height * 0.5f - CharacterController.radius + 0.04f);
            float probeRadius = Mathf.Max(0.05f, CharacterController.radius * 0.9f);
            float probeDistance = Mathf.Max(0.08f, GroundProbeDistance + 0.12f);

            if (!Physics.SphereCast(
                    sphereCenter,
                    probeRadius,
                    Vector3.down,
                    out hit,
                    probeDistance,
                    ~0,
                    QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            return hit.collider != null && !hit.collider.transform.IsChildOf(transform);
        }

        private void EvaluateSlopeState(
            Vector3 movePlanar,
            out float feetAngle,
            out float aheadAngle,
            out Vector3 feetNormal,
            out Vector3 aheadNormal)
        {
            feetAngle = 0f;
            aheadAngle = 0f;
            feetNormal = Vector3.up;
            aheadNormal = Vector3.up;

            if (CharacterController == null || !CharacterController.enabled)
            {
                return;
            }

            Vector3 origin = transform.position + CharacterController.center;
            float footOffset = CharacterController.height * 0.5f - CharacterController.radius;
            Vector3 footOrigin = origin + Vector3.down * footOffset;
            float radius = Mathf.Max(0.05f, CharacterController.radius * 0.7f);
            float downDist = Mathf.Max(SteepSlopeProbeDistance, GroundProbeDistance + 0.3f);

            if (TrySlopeProbe(footOrigin, Vector3.down, downDist, radius, out RaycastHit feetHit) &&
                feetHit.collider != null && !feetHit.collider.transform.IsChildOf(transform))
            {
                feetAngle = Vector3.Angle(feetHit.normal, Vector3.up);
                feetNormal = feetHit.normal;
            }

            Vector3 ahead = movePlanar.sqrMagnitude > 0.001f ? movePlanar : transform.forward;
            ahead.y = 0f;
            if (ahead.sqrMagnitude < 0.001f)
            {
                ahead = Vector3.forward;
            }

            ahead.Normalize();
            float look = Mathf.Max(0.1f, SlopeLookAheadDistance);

            float bestAhead = 0f;
            Vector3 bestAheadNormal = Vector3.up;
            Vector3[] aheadOrigins =
            {
                footOrigin + ahead * (look * 0.45f),
                footOrigin + ahead * (look * 0.85f),
                footOrigin + ahead * look + Vector3.up * 0.2f
            };

            for (int i = 0; i < aheadOrigins.Length; i++)
            {
                if (!TrySlopeProbe(aheadOrigins[i], Vector3.down, downDist + 0.35f, radius, out RaycastHit hit) ||
                    hit.collider == null || hit.collider.transform.IsChildOf(transform))
                {
                    continue;
                }

                float angle = Vector3.Angle(hit.normal, Vector3.up);
                if (angle > bestAhead)
                {
                    bestAhead = angle;
                    bestAheadNormal = hit.normal;
                }
            }

            aheadAngle = bestAhead;
            aheadNormal = bestAheadNormal;
        }

        private static bool TrySlopeProbe(Vector3 origin, Vector3 direction, float distance, float radius, out RaycastHit hit)
        {
            if (Physics.SphereCast(origin, radius, direction, out hit, distance, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider != null)
                {
                    return true;
                }
            }

            hit = default;
            return false;
        }

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (!IsOwner || hit.collider == null)
            {
                return;
            }

            float slopeAngle = Vector3.Angle(hit.normal, Vector3.up);
            if (slopeAngle > 0.01f && slopeAngle < 89.5f)
            {
                lastGroundNormal = hit.normal;
                lastGroundWasSteep = slopeAngle > MaxWalkableSlopeAngle;
            }

            MiniVanCoalCart cart = hit.collider.GetComponentInParent<MiniVanCoalCart>();
            if (cart == null)
            {
                return;
            }

            // CharacterController never imparts force on Rigidbodies by itself.
            cart.ReceiveControllerPush(this, hit.moveDirection, hit.point);
        }

        private void ApplyDamWaterSwimVertical(Vector3 moveInput)
        {
            float lookUp = PlayerCamera != null ? PlayerCamera.transform.forward.y : 0f;
            bool riseByLook = moveInput.z > 0.01f && lookUp > DamWaterLookUpRiseThreshold;
            bool riseBySpace = MiniVanKeyBindings.GetKey(MiniVanKeyAction.Jump);
            bool rising = riseByLook || riseBySpace;

            if (rising)
            {
                verticalVelocity = Mathf.MoveTowards(
                    verticalVelocity,
                    DamWaterMaxRiseSpeed,
                    DamWaterRiseAcceleration * Time.deltaTime);
            }
            else
            {
                // Idle in water: soft water gravity and a steady sink.
                verticalVelocity += DamWaterGravity * Time.deltaTime;
                verticalVelocity = Mathf.MoveTowards(
                    verticalVelocity,
                    DamWaterIdleSinkSpeed,
                    2.4f * Time.deltaTime);
            }

            verticalVelocity = Mathf.Clamp(verticalVelocity, DamWaterMaxSinkSpeed, DamWaterMaxRiseSpeed);
            UpdateDamWaterOxygen();
        }

        private bool IsDamWaterHeadSubmerged()
        {
            if (!damWaterImmersed)
            {
                return false;
            }

            float headY = PlayerCamera != null
                ? PlayerCamera.transform.position.y
                : transform.position.y + (CharacterController != null ? CharacterController.height * 0.85f : 1.5f);
            return headY < damWaterSurfaceY - 0.05f;
        }

        private void UpdateDamWaterOxygen()
        {
            if (!IsOwner || IsZombieDead || IsDowned)
            {
                return;
            }

            bool submerged = IsDamWaterHeadSubmerged();
            if (submerged)
            {
                damWaterOxygenRemaining = Mathf.Max(0f, damWaterOxygenRemaining - Time.deltaTime);
                if (damWaterOxygenRemaining <= 0f && Time.time >= nextDamDrownDamageTime)
                {
                    nextDamDrownDamageTime = Time.time + 1f;
                    RequestDamDrownDamageServerRpc();
                }
            }
            else
            {
                damWaterOxygenRemaining = Mathf.MoveTowards(
                    damWaterOxygenRemaining,
                    DamWaterOxygenSeconds,
                    Time.deltaTime * 5f);
            }
        }

        [ServerRpc]
        private void RequestDamDrownDamageServerRpc()
        {
            ReceiveZombieDamageServer(1);
        }

        private bool CanFindInteriorRoofDoorForCurrentInput()
        {
            return MiniVanKeyBindings.GetKeyDown(MiniVanKeyAction.Interact) &&
                   FindNearbyRoofDoorForInteraction() != null;
        }

        private MiniVanDoor cachedInteractableDoor;
        private int cachedInteractableDoorFrame = -1;

        private MiniVanDoor GetInteractableDoor()
        {
            // OnGUI runs multiple passes per frame and this does raycasts +
            // vehicle lookups, so resolve at most once per frame.
            if (cachedInteractableDoorFrame == Time.frameCount)
            {
                return cachedInteractableDoor;
            }

            cachedInteractableDoorFrame = Time.frameCount;
            cachedInteractableDoor = ResolveInteractableDoor();
            return cachedInteractableDoor;
        }

        private MiniVanDoor ResolveInteractableDoor()
        {
            if (MiniVanGameModeInteractionSystem.IsLocalRoofAttach())
            {
                return null;
            }

            if (lookedAtDoor != null && lookedAtDoor.IsInRange(transform.position))
            {
                return lookedAtDoor;
            }

            return FindNearbyRoofDoorForInteraction();
        }

        private void SetHighlightedDoor(MiniVanDoor door)
        {
            if (highlightedDoor == door)
            {
                return;
            }

            if (highlightedDoor != null)
            {
                highlightedDoor.SetHighlighted(false);
            }

            highlightedDoor = door;
            if (highlightedDoor != null)
            {
                highlightedDoor.SetHighlighted(true);
            }
        }

        private MiniVanDoor FindNearbyRoofDoorForInteraction()
        {
            if (PlayerCamera == null || MiniVanGameModeInteractionSystem.IsLocalRoofAttach())
            {
                return null;
            }

            MiniVanVehicle vehicle = movingPlatformVehicle != null ? movingPlatformVehicle : FindInteriorVehicle();
            if (vehicle == null)
            {
                // Roof standing may not yet be carried (e.g. just climbed up) — still allow hatch toggle.
                vehicle = FindRoofInteractionVehicle();
            }

            if (vehicle == null)
            {
                return null;
            }

            bool onRoof = IsPlayerAtVehicleRoofHeight(vehicle, transform.position) || IsStandingOnVehicleRoof(vehicle);
            bool inCabin = IsInsideVehicleCabin(vehicle) || IsInsideVehicleCabinForZombieTarget(vehicle);
            if (!onRoof && !inCabin)
            {
                return null;
            }

            float lookY = PlayerCamera.transform.forward.y;
            // Cabin: look up at the hatch. Roof: look somewhat down / level at the lid.
            if (onRoof)
            {
                if (lookY > 0.55f)
                {
                    return null;
                }
            }
            else if (lookY < 0.12f)
            {
                return null;
            }

            Vector3 cameraPosition = PlayerCamera.transform.position;
            Vector3 cameraForward = PlayerCamera.transform.forward;

            // 1) Prefer a real ray hit on the lid (skip RoofAttach under the crosshair).
            Ray ray = new Ray(cameraPosition, cameraForward);
            RaycastHit[] hits = Physics.RaycastAll(ray, 3.5f, ~0, QueryTriggerInteraction.Collide);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            for (int i = 0; i < hits.Length; i++)
            {
                if (ShouldIgnoreAimCollider(hits[i].collider))
                {
                    continue;
                }

                if (hits[i].collider.GetComponentInParent<MiniVanRoofAttachPoint>() != null)
                {
                    break;
                }

                MiniVanDoor hitDoor = hits[i].collider.GetComponentInParent<MiniVanDoor>();
                if (hitDoor != null &&
                    hitDoor.IsRoofDoor &&
                    hitDoor.GetComponentInParent<MiniVanVehicle>() == vehicle &&
                    hitDoor.IsInRange(transform.position))
                {
                    return hitDoor;
                }
            }

            // 2) Fallback: thin lid is easy to miss with a ray — allow nearby aim cone.
            MiniVanDoor[] doors = vehicle.GetComponentsInChildren<MiniVanDoor>(true);
            MiniVanDoor best = null;
            float bestScore = float.MaxValue;
            for (int i = 0; i < doors.Length; i++)
            {
                MiniVanDoor door = doors[i];
                if (door == null || !door.IsRoofDoor || !door.IsInRange(transform.position))
                {
                    continue;
                }

                if (!IsAimingAtRoofDoor(door, cameraPosition, cameraForward, out float aimScore))
                {
                    continue;
                }

                if (aimScore < bestScore)
                {
                    best = door;
                    bestScore = aimScore;
                }
            }

            return best;
        }

        private static bool IsAimingAtRoofDoor(
            MiniVanDoor door,
            Vector3 cameraPosition,
            Vector3 cameraForward,
            out float aimScore)
        {
            aimScore = float.MaxValue;
            if (door == null)
            {
                return false;
            }

            Collider doorCollider = door.GetComponent<Collider>();
            Bounds bounds = doorCollider != null
                ? doorCollider.bounds
                : new Bounds(door.transform.position, Vector3.one);

            Vector3 target = bounds.ClosestPoint(cameraPosition + cameraForward * 1.2f);
            Vector3 toTarget = target - cameraPosition;
            if (toTarget.sqrMagnitude < 0.0001f)
            {
                aimScore = 0f;
                return true;
            }

            float forwardDistance = Vector3.Dot(toTarget, cameraForward);
            if (forwardDistance < 0.05f)
            {
                return false;
            }

            float align = Vector3.Dot(toTarget.normalized, cameraForward);
            // ~25° cone toward the lid / hatch opening.
            if (align < 0.90f)
            {
                return false;
            }

            float lateral = (toTarget - cameraForward * forwardDistance).magnitude;
            aimScore = lateral + (1f - align) * 2f;
            return lateral <= 0.85f;
        }

        private MiniVanVehicle FindRoofInteractionVehicle()
        {
            MiniVanVehicle[] vehicles = MiniVanSceneScan.Get<MiniVanVehicle>();
            MiniVanVehicle best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < vehicles.Length; i++)
            {
                MiniVanVehicle vehicle = vehicles[i];
                if (vehicle == null)
                {
                    continue;
                }

                if (!IsPlayerAtVehicleRoofHeight(vehicle, transform.position) && !IsStandingOnVehicleRoof(vehicle))
                {
                    continue;
                }

                float distance = Vector3.Distance(transform.position, vehicle.transform.position);
                if (distance < bestDistance)
                {
                    best = vehicle;
                    bestDistance = distance;
                }
            }

            return best;
        }

        private void CacheStandingControllerPose()
        {
            if (CharacterController == null || standingControllerHeight > 0f)
            {
                return;
            }

            standingControllerHeight = CharacterController.height;
            standingControllerCenter = CharacterController.center;
        }

        private void UpdateCrouch()
        {
            if (CharacterController == null || !CharacterController.enabled)
            {
                return;
            }

            CacheStandingControllerPose();
            bool controlHeld = MiniVanKeyBindings.GetKey(MiniVanKeyAction.Crouch);
            if (controlHeld)
            {
                crouchRequested = true;
            }
            else if (crouchRequested && CanUseStandingCapsule())
            {
                crouchRequested = false;
            }

            float target = crouchRequested ? 1f : 0f;
            crouchProgress = Mathf.MoveTowards(
                crouchProgress,
                target,
                Mathf.Max(0.1f, CrouchTransitionSpeed) * Time.deltaTime);

            float minimumHeight = CharacterController.radius * 2f + 0.02f;
            float crouchedHeight = Mathf.Clamp(CrouchHeight, minimumHeight, standingControllerHeight);
            float currentHeight = Mathf.Lerp(standingControllerHeight, crouchedHeight, crouchProgress);
            float standingBottom = standingControllerCenter.y - standingControllerHeight * 0.5f;
            CharacterController.height = currentHeight;
            CharacterController.center = new Vector3(
                standingControllerCenter.x,
                standingBottom + currentHeight * 0.5f,
                standingControllerCenter.z);

            if (CameraRoot != null && hasInitialFirstPersonCameraPose && !hotPotatoPoopActive)
            {
                Vector3 cameraPosition = initialFirstPersonCameraLocalPosition;
                cameraPosition.y -= Mathf.Max(0f, CrouchCameraDrop) * crouchProgress;
                CameraRoot.localPosition = cameraPosition;
            }
        }

        private bool CanUseStandingCapsule()
        {
            if (CharacterController == null || standingControllerHeight <= 0f)
            {
                return true;
            }

            float radiusScale = Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.z));
            float radius = Mathf.Max(0.02f, CharacterController.radius * radiusScale - CharacterController.skinWidth);
            Vector3 worldCenter = transform.TransformPoint(standingControllerCenter);
            Vector3 worldUp = Vector3.up;
            float halfSegment = Mathf.Max(0f, standingControllerHeight * Mathf.Abs(transform.lossyScale.y) * 0.5f - radius);
            Collider[] overlaps = Physics.OverlapCapsule(
                worldCenter - worldUp * halfSegment,
                worldCenter + worldUp * halfSegment,
                radius,
                ~0,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < overlaps.Length; i++)
            {
                Collider overlap = overlaps[i];
                if (overlap != null && !overlap.transform.IsChildOf(transform))
                {
                    return false;
                }
            }

            return true;
        }
        private bool IsGroundedForJump()
        {
            if (CharacterController == null || !CharacterController.enabled)
            {
                return false;
            }

            float minWalkableNormalY = Mathf.Cos(MaxWalkableSlopeAngle * Mathf.Deg2Rad);

            if (CharacterController.isGrounded)
            {
                // Controller reports grounded on steep faces too — only trust walkable normals.
                if (lastGroundWasSteep || lastGroundNormal.y < minWalkableNormalY)
                {
                    return false;
                }

                return true;
            }

            Vector3 sphereCenter = transform.position + CharacterController.center + Vector3.down * (CharacterController.height * 0.5f - CharacterController.radius + 0.04f);
            float probeRadius = Mathf.Max(0.05f, CharacterController.radius * 0.92f);
            // Only lengthen the probe while falling — never while jumping up (that cut roof jumps short).
            float probeDistance = Mathf.Max(0.05f, GroundProbeDistance + Mathf.Max(0f, -verticalVelocity) * Time.deltaTime);

            if (Physics.SphereCast(sphereCenter, probeRadius, Vector3.down, out RaycastHit hit, probeDistance, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider != null && !hit.collider.transform.IsChildOf(transform))
                {
                    lastGroundNormal = hit.normal;
                    lastGroundWasSteep = hit.normal.y < minWalkableNormalY;
                    return hit.normal.y >= minWalkableNormalY;
                }
            }

            return false;
        }
















        private void RefreshCoreInteractionTargetsFast(
            bool selectedHeldSkateboard,
            bool selectedHeldHoverboardM)
        {
            lookedAtCoffee = null;
            lookedAtBat = null;
            lookedAtSkateboardShelf = null;
            lookedAtBoardCharger = null;
            lookedAtTowCube = null;
            lookedAtWinchPickup = null;
            lookedAtSkateboard = null;
            lookedAtHoverboardM = null;
            lookedAtDoor = null;
            lookedAtHotPotatoBomb = null;
            lookedAtWoodenBoard = null;
            lookedAtSeat = null;
            ClearSnesHighlights();
            lookedAtSnesTelevision = null;
            lookedAtSnesConsole = null;
            lookedAtSnesCartridge = null;
            lookedAtSnesPowerButton = null;

            Camera camera = PlayerCamera;
            if (camera == null)
            {
                SetHighlightedDoor(null);
                return;
            }

            Ray ray = new Ray(camera.transform.position, camera.transform.forward);
            // Third-person board cam: the body blocks screen center and small ground items are
            // near impossible to hit with a thin ray, so aim with a thick sphere while riding.
            RaycastHit[] hits;
            int hitCount;
            if (currentHoverboardM != null || currentSkateboard != null)
            {
                hits = Physics.SphereCastAll(
                    ray, 0.6f, InteractDistance, ~0, QueryTriggerInteraction.Collide);
                hitCount = hits.Length;
            }
            else
            {
                hitCount = Physics.RaycastNonAlloc(
                    ray, coreInteractionHits, InteractDistance, ~0, QueryTriggerInteraction.Collide);
                hits = coreInteractionHits;
            }

            float bestAnyDistance = float.MaxValue;
            float bestUsefulDistance = float.MaxValue;
            Collider bestAnyCollider = null;
            Collider bestUsefulCollider = null;
            MiniVanDoor bestDoorAlongRay = null;
            float bestDoorDistance = float.MaxValue;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = hits[i];
                if (ShouldIgnoreAimCollider(hit.collider))
                {
                    continue;
                }

                // Roof hatch: direct look at the lid while standing close.
                // Do not steal aim from RoofAttach / other cargo sockets along the same ray.
                if (hit.collider.GetComponentInParent<MiniVanRoofAttachPoint>() == null)
                {
                    MiniVanDoor door = hit.collider.GetComponentInParent<MiniVanDoor>();
                    if (door != null &&
                        door.IsInRange(transform.position) &&
                        hit.distance < bestDoorDistance)
                    {
                        bestDoorAlongRay = door;
                        bestDoorDistance = hit.distance;
                    }
                }

                if (hit.distance < bestAnyDistance)
                {
                    bestAnyDistance = hit.distance;
                    bestAnyCollider = hit.collider;
                }

                // Prefer pickups over ground/walls that steal aim in 3rd person.
                if (hit.distance < bestUsefulDistance && IsUsefulAimCollider(hit.collider))
                {
                    bestUsefulDistance = hit.distance;
                    bestUsefulCollider = hit.collider;
                }
            }

            Collider bestCollider = bestUsefulCollider != null ? bestUsefulCollider : bestAnyCollider;

            if (bestCollider == null && bestDoorAlongRay == null)
            {
                return;
            }

            lookedAtCoffee = bestCollider != null ? bestCollider.GetComponentInParent<CoffeeMugPickup>() : null;
            lookedAtSkateboardShelf = bestCollider != null
                ? bestCollider.GetComponentInParent<MiniVanSkateboardShelf>()
                : null;
            lookedAtBoardCharger = bestCollider != null
                ? bestCollider.GetComponentInParent<MiniVanBoardCharger>()
                : null;
            if (lookedAtBoardCharger != null && !lookedAtBoardCharger.IsInRange(transform.position))
            {
                lookedAtBoardCharger = null;
            }

            if (bestCollider != null || hitCount > 0)
            {
                // Prefer ON/OFF from any hit along the ray (root console trigger often blocks it).
                for (int i = 0; i < hitCount; i++)
                {
                    Transform hitTransform = hits[i].collider != null
                        ? hits[i].collider.transform
                        : null;
                    if (hitTransform == null || ShouldIgnoreAimCollider(hits[i].collider))
                    {
                        continue;
                    }

                    if (hitTransform.name == "ON/OFF" || hitTransform.name == "Power Switch" ||
                        (hitTransform.parent != null &&
                         (hitTransform.parent.name == "ON/OFF" || hitTransform.parent.name == "Power Switch")))
                    {
                        Transform power = hitTransform.name == "ON/OFF" || hitTransform.name == "Power Switch"
                            ? hitTransform
                            : hitTransform.parent;
                        lookedAtSnesPowerButton = power;
                        lookedAtSnesConsole = power.GetComponentInParent<MiniVanSnesConsole>();
                        break;
                    }
                }

                if (bestCollider != null)
                {
                    lookedAtSnesTelevision = bestCollider.GetComponentInParent<MiniVanSnesTelevision>();
                    lookedAtSnesConsole = lookedAtSnesConsole != null
                        ? lookedAtSnesConsole
                        : bestCollider.GetComponentInParent<MiniVanSnesConsole>();
                    lookedAtSnesCartridge = bestCollider.GetComponentInParent<MiniVanSnesCartridge>();
                }
            }

            if (lookedAtSnesTelevision != null && !lookedAtSnesTelevision.IsInRange(transform.position))
            {
                lookedAtSnesTelevision = null;
            }

            if (lookedAtSnesConsole != null && !lookedAtSnesConsole.IsInRange(transform.position))
            {
                lookedAtSnesConsole = null;
                lookedAtSnesPowerButton = null;
            }

            // Aim assist for power button when looking at the console body.
            if (lookedAtSnesPowerButton == null && lookedAtSnesConsole != null &&
                lookedAtSnesConsole.IsAimingAtPowerButton(ray, InteractDistance))
            {
                lookedAtSnesPowerButton = lookedAtSnesConsole.OnOffButton;
            }

            // With a cart inserted, console body aims at the cartridge (can't pick console up).
            if (lookedAtSnesConsole != null &&
                lookedAtSnesPowerButton == null &&
                lookedAtSnesConsole.HasInsertedCartridge() &&
                lookedAtSnesConsole.IsInsertedCartridge(out MiniVanSnesCartridge insertedCart))
            {
                lookedAtSnesCartridge = insertedCart;
            }

            if (lookedAtSnesCartridge != null && !lookedAtSnesCartridge.IsInRange(transform.position))
            {
                lookedAtSnesCartridge = null;
            }

            ApplySnesHighlight();

            if (lookedAtBoardCharger == null &&
                (selectedHeldHoverboardM ||
                 (heldHoverboardM == null && heldSkateboard == null && heldTowCube == null)))
            {
                MiniVanBoardCharger nearestCharger = MiniVanBoardCharger.FindNearest(transform.position, 2.6f);
                if (nearestCharger != null)
                {
                    lookedAtBoardCharger = nearestCharger;
                }
            }

            if (heldTowCube == null && !selectedHeldSkateboard && !selectedHeldHoverboardM)
            {
                lookedAtBat = bestCollider != null ? bestCollider.GetComponentInParent<MiniVanBatPickup>() : null;
                if (!CanPickUpBatType(lookedAtBat))
                {
                    lookedAtBat = null;
                }
                lookedAtTowCube = bestCollider != null ? bestCollider.GetComponentInParent<MiniVanTowCube>() : null;
                lookedAtWinchPickup = bestCollider != null
                    ? bestCollider.GetComponentInParent<MiniVanWinchPickup>()
                    : null;
                if (lookedAtWinchPickup == null || !lookedAtWinchPickup.IsAvailable ||
                    !lookedAtWinchPickup.IsInReach(transform.position) ||
                    HasInventoryItem(MiniVanInventoryItem.Winch))
                {
                    lookedAtWinchPickup = null;
                }

                lookedAtDoor = MiniVanGameModeInteractionSystem.IsLocalRoofAttach()
                    ? null
                    : bestDoorAlongRay;
                if (lookedAtDoor == null &&
                    !MiniVanGameModeInteractionSystem.IsLocalRoofAttach() &&
                    bestCollider != null)
                {
                    lookedAtDoor = bestCollider.GetComponentInParent<MiniVanDoor>();
                    if (lookedAtDoor != null && !lookedAtDoor.IsInRange(transform.position))
                    {
                        lookedAtDoor = null;
                    }
                }

                SetHighlightedDoor(GetInteractableDoor());
            }
            else
            {
                SetHighlightedDoor(null);
            }
            if (heldSkateboard == null && heldHoverboardM == null && heldTowCube == null)
            {
                lookedAtSkateboard = bestCollider != null
                    ? bestCollider.GetComponentInParent<MiniVanSkateboard>()
                    : null;
                lookedAtHoverboardM = bestCollider != null
                    ? bestCollider.GetComponentInParent<MiniVanHoverboardM>()
                    : null;
                lookedAtHotPotatoBomb = bestCollider != null
                    ? bestCollider.GetComponentInParent<MiniVanHotPotatoBomb>()
                    : null;
                lookedAtWoodenBoard = bestCollider != null
                    ? bestCollider.GetComponentInParent<MiniVanWoodenBoard>()
                    : null;
                if (lookedAtWoodenBoard != null &&
                    (lookedAtWoodenBoard.IsCarried ||
                     MiniVanWoodenBoard.GetCarriedBy(this) != null ||
                     string.IsNullOrEmpty(lookedAtWoodenBoard.GetPrompt(this))))
                {
                    lookedAtWoodenBoard = null;
                }
            }
            if (lookedAtBat == null && lookedAtSkateboard == null && lookedAtHoverboardM == null &&
                lookedAtSkateboardShelf == null && lookedAtBoardCharger == null && lookedAtTowCube == null && lookedAtDoor == null &&
                lookedAtWinchPickup == null && lookedAtHotPotatoBomb == null &&
                lookedAtWoodenBoard == null && !selectedHeldSkateboard &&
                !selectedHeldHoverboardM && heldTowCube == null)
            {
                MiniVanSeat seat = bestCollider != null ? bestCollider.GetComponentInParent<MiniVanSeat>() : null;
                lookedAtSeat = seat != null && seat.IsAvailable && seat.IsPlayerInEnterRange(transform.position)
                    ? seat
                    : null;
            }

            // Riding: never treat the board under the player (or any board) as an E-pickup target.
            // Otherwise Interact is consumed by a failed self-pickup and world items are blocked.
            if (currentSkateboard != null || currentHoverboardM != null)
            {
                lookedAtSkateboard = null;
                lookedAtHoverboardM = null;
                lookedAtSkateboardShelf = null;
                lookedAtBoardCharger = null;
                lookedAtSeat = null;
            }
        }

        private bool IsRidingBoardCollider(Collider collider)
        {
            if (collider == null)
            {
                return false;
            }

            return IsRidingBoardTransform(collider.transform);
        }

        public bool IsRidingBoardTransform(Transform target)
        {
            if (target == null)
            {
                return false;
            }

            if (currentHoverboardM != null &&
                (target == currentHoverboardM.transform || target.IsChildOf(currentHoverboardM.transform)))
            {
                return true;
            }

            if (currentSkateboard != null &&
                (target == currentSkateboard.transform || target.IsChildOf(currentSkateboard.transform)))
            {
                return true;
            }

            return false;
        }

        public bool IsRidingBoard => currentHoverboardM != null || currentSkateboard != null;

        /// <summary>
        /// Skip the local player and the board currently being ridden so third-person aim can reach world items.
        /// </summary>
        public bool ShouldIgnoreAimCollider(Collider collider)
        {
            if (collider == null)
            {
                return true;
            }

            Transform target = collider.transform;
            if (target == transform || target.IsChildOf(transform))
            {
                return true;
            }

            return IsRidingBoardTransform(target);
        }

        private static bool IsUsefulAimCollider(Collider collider)
        {
            if (collider == null)
            {
                return false;
            }

            Transform t = collider.transform;
            return t.GetComponentInParent<CoffeeMugPickup>() != null ||
                   t.GetComponentInParent<MiniVanBatPickup>() != null ||
                   t.GetComponentInParent<MiniVanSkateboard>() != null ||
                   t.GetComponentInParent<MiniVanHoverboardM>() != null ||
                   t.GetComponentInParent<MiniVanSkateboardShelf>() != null ||
                   t.GetComponentInParent<MiniVanBoardCharger>() != null ||
                   t.GetComponentInParent<MiniVanTowCube>() != null ||
                   t.GetComponentInParent<MiniVanWinchPickup>() != null ||
                   t.GetComponentInParent<MiniVanHotPotatoBomb>() != null ||
                   t.GetComponentInParent<MiniVanWoodenBoard>() != null ||
                   t.GetComponentInParent<MiniVanSnesTelevision>() != null ||
                   t.GetComponentInParent<MiniVanSnesConsole>() != null ||
                   t.GetComponentInParent<MiniVanSnesCartridge>() != null ||
                   t.GetComponentInParent<MiniVanSeat>() != null ||
                   t.GetComponentInParent<MiniVanDoor>() != null ||
                   t.GetComponentInParent<MiniVanPizzaItem>() != null;
        }

private void HandleInteraction()
        {
            if (deathInteractionConsumedThisFrame || antonInteractionConsumedThisFrame)
            {
                return;
            }

            if (BlocksItemUseBecauseAnton())
            {
                // Anton on back: only Anton seat / stretcher place (handled in UpdateAntonSystem + interactables).
                return;
            }

            if (HandleSnesInteractionInput())
            {
                return;
            }

            bool selectedSkateboard = IsSelectedInventoryItem(MiniVanInventoryItem.Skateboard);
            bool selectedHeldSkateboard = heldSkateboard != null && selectedSkateboard;
            bool selectedHoverboardM = IsSelectedInventoryItem(MiniVanInventoryItem.HoverboardM);
            bool selectedHeldHoverboardM = heldHoverboardM != null && selectedHoverboardM;
            bool forceTargetScan = MiniVanKeyBindings.GetKeyDown(MiniVanKeyAction.Interact) ||
                MiniVanKeyBindings.GetKeyDown(MiniVanKeyAction.Drop) ||
                Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1);
            if (forceTargetScan || Time.unscaledTime >= nextInteractionTargetScanTime)
            {
                RefreshCoreInteractionTargetsFast(
                    selectedHeldSkateboard,
                    selectedHeldHoverboardM);
                UpdatePizzaLookTargets();
                UpdateFuelLookTarget();
                nextInteractionTargetScanTime = Time.unscaledTime + InteractionTargetScanInterval;
            }

            if (HandleFuelInteractionInput())
            {
                return;
            }

            if (HandlePizzaInteractionInput())
            {
                return;
            }

            if (MiniVanKeyBindings.GetKeyDown(MiniVanKeyAction.Drop) && HandleWinchDropInput())
            {
                return;
            }

            if (MiniVanKeyBindings.GetKeyDown(MiniVanKeyAction.Drop) && HandleCosmeticDropInput())
            {
                return;
            }

            if (MiniVanKeyBindings.GetKeyDown(MiniVanKeyAction.Drop) && HandleFireExtinguisherDropInput())
            {
                return;
            }

            if (MiniVanKeyBindings.GetKeyDown(MiniVanKeyAction.Drop) && HandleDefibrillatorDropInput())
            {
                return;
            }

            if (MiniVanKeyBindings.GetKeyDown(MiniVanKeyAction.Drop) && HandleHeldInventoryDropInput())
            {
                return;
            }

            if (MiniVanKeyBindings.GetKeyDown(MiniVanKeyAction.Interact))
            {
                if (Time.frameCount > winchUseBlockedUntilFrame &&
                    !MiniVanGameModeInteractionSystem.IsLocalWinchDashboardButton() &&
                    (MiniVanWinchCable.HasActiveCable(this) ||
                     IsSelectedInventoryItem(MiniVanInventoryItem.Winch) ||
                     MiniVanWinchCable.CanFoldNear(this)) &&
                    MiniVanWinchCable.TryUseSelectedWinch(this, networkWinchDurability.Value))
                {
                    // Successful tap use (attach/take) cancels fold progress.
                    MiniVanWinchCable.UpdateFoldInput(this, false);
                    return;
                }

                if (lookedAtWinchPickup != null)
                {
                    TryPickupWinch(lookedAtWinchPickup);
                    return;
                }
            }

            // Hold E folds the winch (1.5s + ring). Skip while aiming a dashboard winch button.
            bool winchFoldHeld =
                MiniVanKeyBindings.GetKey(MiniVanKeyAction.Interact) &&
                !MiniVanGameModeInteractionSystem.IsLocalWinchDashboardButton() &&
                (MiniVanWinchCable.HasActiveCable(this) || MiniVanWinchCable.CanFoldNear(this));
            MiniVanWinchCable.UpdateFoldInput(this, winchFoldHeld);

            if (heldTowCube != null && Input.GetMouseButtonDown(0))
            {
                RequestTowCubeAttachToHookServerRpc(new NetworkObjectReference(heldTowCube.NetworkObject));
                return;
            }

            if (lookedAtTowCube != null && lookedAtTowCube.TowAttached.Value && Input.GetMouseButtonDown(0))
            {
                RequestTowCubeDetachServerRpc(new NetworkObjectReference(lookedAtTowCube.NetworkObject));
                return;
            }

            if (heldTowCube != null && MiniVanKeyBindings.GetKeyDown(MiniVanKeyAction.Interact))
            {
                MiniVanTowCube cube = heldTowCube;
                if (!IsServer)
                {
                    heldTowCube = null;
                    cube.BeginLocalDropPrediction(this);
                }

                RequestTowCubeDropServerRpc(new NetworkObjectReference(cube.NetworkObject));
                return;
            }

            if (selectedHeldSkateboard && Input.GetMouseButtonDown(1))
            {
                RequestSkateboardMountServerRpc(new NetworkObjectReference(heldSkateboard.NetworkObject));
                return;
            }

            if (!MiniVanGameModeInteractionSystem.IsLocalRoofAttach() &&
                MiniVanKeyBindings.GetKeyDown(MiniVanKeyAction.Interact) &&
                (lookedAtDoor != null || CanFindInteriorRoofDoorForCurrentInput()))
            {
                MiniVanDoor targetDoor = GetInteractableDoor();
                MiniVanVehicle doorVehicle = targetDoor != null && targetDoor.Vehicle != null
                    ? targetDoor.Vehicle
                    : targetDoor != null
                        ? targetDoor.GetComponentInParent<MiniVanVehicle>()
                        : null;
                if (doorVehicle != null && targetDoor != null && targetDoor.IsInRange(transform.position))
                {
                    if (targetDoor.IsRoofDoor)
                    {
                        doorVehicle.RequestToggleRoofDoorServerRpc();
                    }
                    else
                    {
                        doorVehicle.RequestToggleSideDoorServerRpc();
                    }
                }

                return;
            }

            if (lookedAtBoardCharger != null && MiniVanKeyBindings.GetKeyDown(MiniVanKeyAction.Interact))
            {
                MiniVanVehicle chargerVehicle = lookedAtBoardCharger.GetComponentInParent<MiniVanVehicle>();
                if (heldHoverboardM != null && IsSelectedInventoryItem(MiniVanInventoryItem.HoverboardM))
                {
                    if (chargerVehicle != null && boardChargerTargetSlot >= 0)
                    {
                        RequestHoverboardMPlaceOnChargerServerRpc(
                            new NetworkObjectReference(chargerVehicle.NetworkObject),
                            new NetworkObjectReference(heldHoverboardM.NetworkObject),
                            boardChargerTargetSlot);
                    }

                    return;
                }

                MiniVanHoverboardM dockedBoard = lookedAtBoardCharger.FindNearestDockedBoard(transform.position);
                if (dockedBoard != null)
                {
                    if (dockedBoard.IsStillChargingOnDock)
                    {
                        return;
                    }

                    RequestHoverboardMPickupServerRpc(new NetworkObjectReference(dockedBoard.NetworkObject));
                    return;
                }
            }

            if (lookedAtSkateboardShelf != null && MiniVanKeyBindings.GetKeyDown(MiniVanKeyAction.Interact))
            {
                MiniVanVehicle shelfVehicle = lookedAtSkateboardShelf.GetComponentInParent<MiniVanVehicle>();
                if (heldHoverboardM != null && IsSelectedInventoryItem(MiniVanInventoryItem.HoverboardM))
                {
                    if (shelfVehicle != null)
                    {
                        RequestHoverboardMPlaceOnShelfServerRpc(new NetworkObjectReference(shelfVehicle.NetworkObject), new NetworkObjectReference(heldHoverboardM.NetworkObject));
                    }
                    return;
                }

                if (selectedHeldSkateboard)
                {
                    if (shelfVehicle != null)
                    {
                        RequestSkateboardPlaceOnShelfServerRpc(new NetworkObjectReference(shelfVehicle.NetworkObject), new NetworkObjectReference(heldSkateboard.NetworkObject));
                    }
                    return;
                }

                MiniVanHoverboardM storedHoverboard = lookedAtSkateboardShelf.FindStoredHoverboardM();
                if (storedHoverboard != null)
                {
                    RequestHoverboardMPickupServerRpc(new NetworkObjectReference(storedHoverboard.NetworkObject));
                    return;
                }

                MiniVanSkateboard storedSkateboard = lookedAtSkateboardShelf.FindStoredSkateboard();
                if (storedSkateboard != null)
                {
                    RequestSkateboardPickupServerRpc(new NetworkObjectReference(storedSkateboard.NetworkObject));
                    return;
                }
            }

            // Held hoverboard: E = mount, Q = drop (charger/shelf handled above).
            if (selectedHeldHoverboardM && MiniVanKeyBindings.GetKeyDown(MiniVanKeyAction.Interact))
            {
                RequestHoverboardMMountServerRpc(new NetworkObjectReference(heldHoverboardM.NetworkObject));
                return;
            }

            if (selectedHeldHoverboardM && MiniVanKeyBindings.GetKeyDown(MiniVanKeyAction.Drop))
            {
                MiniVanHoverboardM board = heldHoverboardM;
                if (!IsServer)
                {
                    heldHoverboardM = null;
                    heldHoverboardMSlot = -1;
                    PredictClearInventoryItem(MiniVanInventoryItem.HoverboardM);
                    board.BeginLocalDropPrediction(this);
                }

                RequestHoverboardMDropServerRpc(new NetworkObjectReference(board.NetworkObject));
                return;
            }

            if (selectedHeldSkateboard && MiniVanKeyBindings.GetKeyDown(MiniVanKeyAction.Interact))
            {
                MiniVanSkateboard board = heldSkateboard;
                if (!IsServer)
                {
                    heldSkateboard = null;
                    heldSkateboardSlot = -1;
                    PredictClearInventoryItem(MiniVanInventoryItem.Skateboard);
                    board.BeginLocalDropPrediction(this);
                }

                RequestSkateboardDropServerRpc(new NetworkObjectReference(board.NetworkObject));
                return;
            }

            if (lookedAtHoverboardM != null && MiniVanKeyBindings.GetKeyDown(MiniVanKeyAction.Interact))
            {
                if (lookedAtHoverboardM.IsStillChargingOnDock)
                {
                    return;
                }

                RequestHoverboardMPickupServerRpc(new NetworkObjectReference(lookedAtHoverboardM.NetworkObject));
                return;
            }

            if (lookedAtSkateboard != null && MiniVanKeyBindings.GetKeyDown(MiniVanKeyAction.Interact))
            {
                RequestSkateboardPickupServerRpc(new NetworkObjectReference(lookedAtSkateboard.NetworkObject));
                return;
            }

            if (lookedAtTowCube != null && lookedAtTowCube.IsAvailable && MiniVanKeyBindings.GetKeyDown(MiniVanKeyAction.Interact))
            {
                RequestTowCubePickupServerRpc(new NetworkObjectReference(lookedAtTowCube.NetworkObject));
                return;
            }

            if (lookedAtWoodenBoard != null && MiniVanKeyBindings.GetKeyDown(MiniVanKeyAction.Interact))
            {
                lookedAtWoodenBoard.TryPickup(this);
                return;
            }

            if (lookedAtHotPotatoBomb != null && lookedAtHotPotatoBomb.IsAvailable && MiniVanKeyBindings.GetKeyDown(MiniVanKeyAction.Interact))
            {
                hotPotatoDropBlockedUntilFrame = Time.frameCount + 1;
                RequestHotPotatoPickupServerRpc(new NetworkObjectReference(lookedAtHotPotatoBomb.NetworkObject));
                return;
            }

            if (lookedAtCoffee != null && MiniVanKeyBindings.GetKeyDown(MiniVanKeyAction.Interact))
            {
                RequestCoffeePickupServerRpc(new NetworkObjectReference(lookedAtCoffee.NetworkObject));
                return;
            }

            if (lookedAtBat != null && MiniVanKeyBindings.GetKeyDown(MiniVanKeyAction.Interact))
            {
                RequestBatPickupServerRpc(new NetworkObjectReference(lookedAtBat.NetworkObject));
                return;
            }

            if (lookedAtSeat != null && lookedAtSeat.IsAvailable && MiniVanKeyBindings.GetKeyDown(MiniVanKeyAction.Interact))
            {
                lookedAtSeat.Vehicle.RequestSeatServerRpc(lookedAtSeat.SeatIndex);
            }
        }

        private void HandleSeatExitInput()
        {
            if (!MiniVanKeyBindings.GetKeyDown(MiniVanKeyAction.Interact) ||
                currentVehicle == null ||
                currentSeat == null ||
                gearDragActive)
            {
                return;
            }

            // Looking at a dashboard control (winch In/Out, etc.) owns E — don't exit the seat.
            if (MiniVanGameModeInteractionSystem.HasLocalInteractable())
            {
                return;
            }

            currentVehicle.RequestExitSeatServerRpc(currentSeat.SeatIndex);
        }

private void HandleSkateboardMode()
        {
            currentLadder = null;
            HandleSkateboardCameraOrbitInput();
            FollowSkateboard();
            ApplySkateboardCamera();

            float throttle = 0f;
            if (MiniVanKeyBindings.GetKey(MiniVanKeyAction.MoveForward))
            {
                throttle += 1f;
            }

            if (MiniVanKeyBindings.GetKey(MiniVanKeyAction.MoveBack))
            {
                throttle -= 0.65f;
            }

            float steer = 0f;
            if (MiniVanKeyBindings.GetKey(MiniVanKeyAction.MoveLeft))
            {
                steer -= 1f;
            }

            if (MiniVanKeyBindings.GetKey(MiniVanKeyAction.MoveRight))
            {
                steer += 1f;
            }

            if (currentSkateboard != null)
            {
                float riderYaw = transform.eulerAngles.y;
                currentSkateboard.SubmitInputServerRpc(throttle, steer, MiniVanKeyBindings.GetKeyDown(MiniVanKeyAction.Jump), riderYaw);
            }

            if (!equipmentWindowOpen)
            {
                HandleInventoryInput();
            }

            UpdateAntonSystem();
            UpdateAntonLocator();
            bool rmbDown = Input.GetMouseButtonDown(1);
            // Q drops a hand-carried item (stepping off the skateboard is RMB).
            bool droppedHandItem =
                MiniVanKeyBindings.GetKeyDown(MiniVanKeyAction.Drop) && DropCarriedHandItem();
            if (!droppedHandItem && !antonInteractionConsumedThisFrame && !equipmentWindowOpen)
            {
                HandleInteraction();
            }

            UpdateBoardChargerPlacementGhost();
            UpdateSnesPlacementGhost();

            if (!BlocksItemUseBecauseAnton() && !equipmentWindowOpen)
            {
                HandleHotPotatoUse();
            }

            // E = world interact (same as on foot / hoverboard). RMB steps off.
            if (rmbDown &&
                lookedAtDoor == null &&
                !MiniVanWinchCable.HasActiveCable(this) &&
                !IsSelectedInventoryItem(MiniVanInventoryItem.Winch))
            {
                RequestSkateboardExitServerRpc();
            }
        }

        private void FollowSkateboard()
        {
            if (currentSkateboard == null)
            {
                return;
            }

            if (CharacterController != null && CharacterController.enabled)
            {
                CharacterController.enabled = false;
            }

            Vector3 targetPosition = currentSkateboard.GetRidePosition();
            Quaternion targetRotation = currentSkateboard.GetRideRotation();
            float followBlend = 1f - Mathf.Exp(-20f * Time.deltaTime);
            float riderAlignSharpness = Mathf.Max(0.1f, currentSkateboard.RiderSurfaceAlignSharpness);
            float rotationBlend = 1f - Mathf.Exp(-riderAlignSharpness * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, targetPosition, followBlend);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationBlend);
            verticalVelocity = 0f;
        }

        private void HandleHoverboardMMode()
        {
            currentLadder = null;
            HandleSkateboardCameraOrbitInput();
            FollowHoverboardM();
            ApplySkateboardCamera();

            float throttle = 0f;
            if (MiniVanKeyBindings.GetKey(MiniVanKeyAction.MoveForward))
            {
                throttle += 1f;
            }

            if (MiniVanKeyBindings.GetKey(MiniVanKeyAction.MoveBack))
            {
                throttle -= 0.65f;
            }

            float steer = 0f;
            if (MiniVanKeyBindings.GetKey(MiniVanKeyAction.MoveLeft))
            {
                steer -= 1f;
            }

            if (MiniVanKeyBindings.GetKey(MiniVanKeyAction.MoveRight))
            {
                steer += 1f;
            }

            if (currentHoverboardM != null)
            {
                currentHoverboardM.SubmitInputServerRpc(throttle, steer, MiniVanKeyBindings.GetKeyDown(MiniVanKeyAction.Jump));
            }

            if (!equipmentWindowOpen)
            {
                HandleInventoryInput();
            }

            UpdateAntonSystem();
            UpdateAntonLocator();

            // Q: tap drops a hand-carried item, hold steps off; with empty hands tap steps off.
            bool dropKeyHandled = HandleHoverboardDropKey();
            if (!dropKeyHandled && !antonInteractionConsumedThisFrame && !equipmentWindowOpen)
            {
                HandleInteraction();
            }

            UpdateBoardChargerPlacementGhost();
            UpdateSnesPlacementGhost();

            if (!BlocksItemUseBecauseAnton() && !equipmentWindowOpen)
            {
                HandleHotPotatoUse();
            }
        }

        /// <summary>
        /// Hoverboard Q key: with a hand-carried item a tap drops it and holding (~0.55s) steps
        /// off; with empty hands a tap steps off immediately. Returns true when Q was consumed.
        /// </summary>
        private bool HandleHoverboardDropKey()
        {
            if (MiniVanKeyBindings.GetKeyDown(MiniVanKeyAction.Drop))
            {
                if (!HasCarriedHandItem())
                {
                    ridingDropHoldActive = false;
                    RequestHoverboardMExitServerRpc();
                }
                else
                {
                    ridingDropHoldActive = true;
                    ridingDropHoldTime = 0f;
                }

                return true;
            }

            if (ridingDropHoldActive && MiniVanKeyBindings.GetKey(MiniVanKeyAction.Drop))
            {
                ridingDropHoldTime += Time.deltaTime;
                if (ridingDropHoldTime >= RidingExitHoldSeconds)
                {
                    ridingDropHoldActive = false;
                    RequestHoverboardMExitServerRpc();
                }

                return true;
            }

            if (ridingDropHoldActive && MiniVanKeyBindings.GetKeyUp(MiniVanKeyAction.Drop))
            {
                ridingDropHoldActive = false;
                DropCarriedHandItem();
                return true;
            }

            ridingDropHoldActive = false;
            return false;
        }

        private void DrawRidingExitHoldRing()
        {
            if (!ridingDropHoldActive || RidingExitHoldSeconds <= 0.01f)
            {
                return;
            }

            float progress = Mathf.Clamp01(ridingDropHoldTime / RidingExitHoldSeconds);
            if (progress < 0.01f)
            {
                return;
            }

            Texture2D texture = GetRidingExitHoldRingTexture(progress);
            if (texture == null)
            {
                return;
            }

            const float diameter = 78f;
            Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f + 72f);
            Color old = GUI.color;
            GUI.color = Color.white;
            GUI.DrawTexture(
                new Rect(center.x - diameter * 0.5f, center.y - diameter * 0.5f, diameter, diameter),
                texture);
            GUI.color = old;
        }

        private static Texture2D GetRidingExitHoldRingTexture(float progress)
        {
            const int size = 96;
            int bucket = Mathf.Clamp(Mathf.RoundToInt(progress * 100f), 0, 100);
            if (ridingExitHoldRingTexture != null && ridingExitHoldRingBucket == bucket)
            {
                return ridingExitHoldRingTexture;
            }

            if (ridingExitHoldRingTexture == null)
            {
                ridingExitHoldRingTexture = new Texture2D(size, size, TextureFormat.ARGB32, false)
                {
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave
                };
            }

            ridingExitHoldRingBucket = bucket;
            Color clear = new Color(0f, 0f, 0f, 0f);
            Color track = new Color(0f, 0f, 0f, 0.45f);
            Color fill = new Color(0.95f, 0.78f, 0.2f, 0.95f);
            float radius = size * 0.5f - 2f;
            float inner = radius - 10f;
            float angleMax = progress * Mathf.PI * 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - size * 0.5f;
                    float dy = y + 0.5f - size * 0.5f;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    if (dist < inner || dist > radius)
                    {
                        ridingExitHoldRingTexture.SetPixel(x, y, clear);
                        continue;
                    }

                    float angle = Mathf.Atan2(dx, -dy);
                    if (angle < 0f)
                    {
                        angle += Mathf.PI * 2f;
                    }

                    ridingExitHoldRingTexture.SetPixel(x, y, angle <= angleMax ? fill : track);
                }
            }

            ridingExitHoldRingTexture.Apply(false);
            return ridingExitHoldRingTexture;
        }

        public bool HasCarriedHandItem()
        {
            return MiniVanBatteryCharger.GetCarriedBy(this) != null ||
                   MiniVanCarBattery.GetCarriedBy(this) != null ||
                   MiniVanBridgeBattery.GetCarriedBy(this) != null ||
                   MiniVanDetachedWheel.GetCarriedBy(this) != null ||
                   MiniVanWoodenBoard.GetCarriedBy(this) != null ||
                   MiniVanWinchHook.GetCarriedBy(this) != null;
        }

        private bool DropCarriedHandItem()
        {
            MiniVanBatteryCharger carriedCharger = MiniVanBatteryCharger.GetCarriedBy(this);
            if (carriedCharger != null)
            {
                carriedCharger.DropNear(this);
                return true;
            }

            MiniVanCarBattery carriedBattery = MiniVanCarBattery.GetCarriedBy(this);
            if (carriedBattery != null)
            {
                carriedBattery.DropNear(this);
                return true;
            }

            MiniVanBridgeBattery carriedBridgeBattery = MiniVanBridgeBattery.GetCarriedBy(this);
            if (carriedBridgeBattery != null)
            {
                carriedBridgeBattery.DropNear(this);
                return true;
            }

            MiniVanDetachedWheel carriedWheel = MiniVanDetachedWheel.GetCarriedBy(this);
            if (carriedWheel != null)
            {
                carriedWheel.DropNear(this);
                return true;
            }

            MiniVanWoodenBoard carriedWoodenBoard = MiniVanWoodenBoard.GetCarriedBy(this);
            if (carriedWoodenBoard != null)
            {
                carriedWoodenBoard.DropNear(this);
                return true;
            }

            MiniVanWinchHook carriedHook = MiniVanWinchHook.GetCarriedBy(this);
            if (carriedHook != null)
            {
                carriedHook.DropNear(this);
                return true;
            }

            return false;
        }

        private void FollowHoverboardM()
        {
            if (currentHoverboardM == null)
            {
                return;
            }

            if (CharacterController != null && CharacterController.enabled)
            {
                CharacterController.enabled = false;
            }

            Vector3 targetPosition = currentHoverboardM.GetRidePosition();
            Quaternion targetRotation = currentHoverboardM.GetRideRotation();
            float followBlend = 1f - Mathf.Exp(-32f * Time.deltaTime);
            float rotationBlend = 1f - Mathf.Exp(-28f * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, targetPosition, followBlend);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationBlend);
            verticalVelocity = 0f;
        }

        private float GetSkateboardRiderYaw()
        {
            Vector3 forward = PlayerCamera != null ? PlayerCamera.transform.forward : transform.forward;
            forward = Vector3.ProjectOnPlane(forward, Vector3.up);
            if (forward.sqrMagnitude < 0.001f)
            {
                forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            }

            if (forward.sqrMagnitude < 0.001f)
            {
                return transform.eulerAngles.y;
            }

            return Quaternion.LookRotation(forward.normalized, Vector3.up).eulerAngles.y;
        }

        private void ApplySkateboardCamera()
        {
            if (!IsOwner || CameraRoot == null)
            {
                return;
            }

            StoreFirstPersonCameraPose();
            EnsureSkateboardCameraOrbit();

            float distance = Mathf.Max(0.8f, SkateboardCameraDistance);
            Quaternion orbit = Quaternion.Euler(skateboardCameraPitch, skateboardCameraYaw, 0f);
            Vector3 desiredPosition = orbit * new Vector3(0f, 0f, -distance);
            desiredPosition.y += SkateboardCameraHeight;

            Vector3 lookTarget = Vector3.up * SkateboardCameraLookHeight;
            Vector3 lookDirection = lookTarget - desiredPosition;
            if (lookDirection.sqrMagnitude < 0.001f)
            {
                lookDirection = Vector3.forward;
            }

            float positionBlend = 1f - Mathf.Exp(-14f * Time.deltaTime);
            float rotationBlend = 1f - Mathf.Exp(-18f * Time.deltaTime);
            CameraRoot.localPosition = Vector3.Lerp(CameraRoot.localPosition, desiredPosition, positionBlend);
            CameraRoot.localRotation = Quaternion.Slerp(CameraRoot.localRotation, Quaternion.LookRotation(lookDirection.normalized, Vector3.up), rotationBlend);
        }

        private void HandleSkateboardCameraOrbitInput()
        {
            if (!IsOwner || CameraRoot == null)
            {
                return;
            }

            EnsureSkateboardCameraOrbit();
            skateboardCameraYaw += Input.GetAxis("Mouse X") * LookSensitivity;
            skateboardCameraPitch = Mathf.Clamp(skateboardCameraPitch - Input.GetAxis("Mouse Y") * LookSensitivity, SkateboardCameraPitchMin, SkateboardCameraPitchMax);
        }

        private void EnsureSkateboardCameraOrbit()
        {
            if (hasSkateboardCameraOrbit)
            {
                return;
            }

            hasSkateboardCameraOrbit = true;
            skateboardCameraYaw = 0f;
            skateboardCameraPitch = Mathf.Clamp(14f, SkateboardCameraPitchMin, SkateboardCameraPitchMax);
        }

        private void StoreFirstPersonCameraPose()
        {
            if (hasStoredFirstPersonCamera || CameraRoot == null)
            {
                return;
            }

            firstPersonCameraLocalPosition = CameraRoot.localPosition;
            firstPersonCameraLocalRotation = CameraRoot.localRotation;
            hasStoredFirstPersonCamera = true;
        }

        private void StoreInitialFirstPersonCameraPose()
        {
            if (hasInitialFirstPersonCameraPose || CameraRoot == null)
            {
                return;
            }

            initialFirstPersonCameraLocalPosition = CameraRoot.localPosition;
            initialFirstPersonCameraLocalRotation = CameraRoot.localRotation;
            hasInitialFirstPersonCameraPose = true;
        }

        private void RestoreFirstPersonCameraPose()
        {
            if (!hasStoredFirstPersonCamera || CameraRoot == null)
            {
                return;
            }

            CameraRoot.localPosition = firstPersonCameraLocalPosition;
            CameraRoot.localRotation = firstPersonCameraLocalRotation;
            hasStoredFirstPersonCamera = false;
        }

        private void RestoreDefaultFirstPersonCameraPose()
        {
            if (CameraRoot == null)
            {
                return;
            }

            if (hasInitialFirstPersonCameraPose)
            {
                CameraRoot.localPosition = initialFirstPersonCameraLocalPosition;
                CameraRoot.localRotation = initialFirstPersonCameraLocalRotation;
            }
            else
            {
                CameraRoot.localPosition = new Vector3(0f, 0.565f, 0.26f);
                CameraRoot.localRotation = Quaternion.identity;
            }

            hasStoredFirstPersonCamera = false;
            hasHotPotatoPoopCameraPose = false;
        }

        [ClientRpc]
        private void StartHotPotatoPoopClientRpc(float seconds, ClientRpcParams clientRpcParams = default)
        {
            hotPotatoPoopActive = true;
            hotPotatoPoopEndTime = Time.time + Mathf.Max(0.1f, seconds);
            EnsureHotPotatoPoopVisual();
            if (hotPotatoPoopVisual != null)
            {
                hotPotatoPoopVisual.transform.localPosition = new Vector3(0f, HotPotatoPoopLocalY, 0f);
                hotPotatoPoopVisual.SetActive(true);
            }

            SetHotPotatoBodyHidden(true);

            if (IsOwner)
            {
                StoreHotPotatoPoopCameraPose();
            }
        }

        private void UpdateHotPotatoPoopMode()
        {
            if (!hotPotatoPoopActive)
            {
                if (hotPotatoPoopVisual != null && hotPotatoPoopVisual.activeSelf)
                {
                    hotPotatoPoopVisual.SetActive(false);
                }

                SetHotPotatoBodyHidden(false);
                return;
            }

            if (Time.time >= hotPotatoPoopEndTime)
            {
                hotPotatoPoopActive = false;
                if (hotPotatoPoopVisual != null)
                {
                    hotPotatoPoopVisual.SetActive(false);
                }

                SetHotPotatoBodyHidden(false);

                if (IsOwner)
                {
                    if (currentSkateboard == null)
                    {
                        RestoreDefaultFirstPersonCameraPose();
                    }
                    else
                    {
                        RestoreHotPotatoPoopCameraPose();
                    }
                }

                return;
            }

            EnsureHotPotatoPoopVisual();
            if (hotPotatoPoopVisual != null)
            {
                hotPotatoPoopVisual.SetActive(true);
            }

            SetHotPotatoBodyHidden(true);

            if (IsOwner && CameraRoot != null)
            {
                StoreHotPotatoPoopCameraPose();
                CameraRoot.localPosition = Vector3.Lerp(CameraRoot.localPosition, new Vector3(0f, 1.45f, -3.35f), Time.deltaTime * 10f);
            }
        }

        private void StoreHotPotatoPoopCameraPose()
        {
            if (hasHotPotatoPoopCameraPose || CameraRoot == null)
            {
                return;
            }

            hotPotatoPoopCameraLocalPosition = CameraRoot.localPosition;
            hotPotatoPoopCameraLocalRotation = CameraRoot.localRotation;
            hasHotPotatoPoopCameraPose = true;
        }

        private void RestoreHotPotatoPoopCameraPose()
        {
            if (!hasHotPotatoPoopCameraPose || CameraRoot == null)
            {
                return;
            }

            CameraRoot.localPosition = hotPotatoPoopCameraLocalPosition;
            CameraRoot.localRotation = hotPotatoPoopCameraLocalRotation;
            hasHotPotatoPoopCameraPose = false;
        }

        private void SetHotPotatoBodyHidden(bool hidden)
        {
            if (hidden)
            {
                if (!hotPotatoBodyHidden)
                {
                    Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
                    hotPotatoHiddenRenderers = renderers;
                    hotPotatoHiddenRendererStates = new bool[renderers.Length];

                    for (int i = 0; i < renderers.Length; i++)
                    {
                        Renderer renderer = renderers[i];
                        if (renderer == null || IsHotPotatoPoopRenderer(renderer))
                        {
                            continue;
                        }

                        hotPotatoHiddenRendererStates[i] = renderer.enabled;
                        renderer.enabled = false;
                    }

                    hotPotatoBodyHidden = true;
                    return;
                }

                for (int i = 0; i < hotPotatoHiddenRenderers.Length; i++)
                {
                    Renderer renderer = hotPotatoHiddenRenderers[i];
                    if (renderer != null && !IsHotPotatoPoopRenderer(renderer))
                    {
                        renderer.enabled = false;
                    }
                }

                return;
            }

            if (!hotPotatoBodyHidden)
            {
                return;
            }

            for (int i = 0; i < hotPotatoHiddenRenderers.Length; i++)
            {
                Renderer renderer = hotPotatoHiddenRenderers[i];
                if (renderer != null && !IsHotPotatoPoopRenderer(renderer))
                {
                    bool wasEnabled = i < hotPotatoHiddenRendererStates.Length && hotPotatoHiddenRendererStates[i];
                    renderer.enabled = wasEnabled;
                }
            }

            hotPotatoHiddenRenderers = System.Array.Empty<Renderer>();
            hotPotatoHiddenRendererStates = System.Array.Empty<bool>();
            hotPotatoBodyHidden = false;
            UpdateEquipmentVisibility();
        }

        private bool IsHotPotatoPoopRenderer(Renderer renderer)
        {
            return renderer != null
                && hotPotatoPoopVisual != null
                && renderer.transform.IsChildOf(hotPotatoPoopVisual.transform);
        }

        private void EnsureHotPotatoPoopVisual()
        {
            if (hotPotatoPoopVisual != null)
            {
                return;
            }

            hotPotatoPoopVisual = new GameObject("Hot Potato Poop Visual");
            hotPotatoPoopVisual.transform.SetParent(transform, false);
            hotPotatoPoopVisual.transform.localPosition = new Vector3(0f, HotPotatoPoopLocalY, 0f);
            hotPotatoPoopVisual.transform.localRotation = Quaternion.identity;

            if (HotPotatoPoopPrefab != null)
            {
                GameObject prefabVisual = Instantiate(HotPotatoPoopPrefab, hotPotatoPoopVisual.transform);
                prefabVisual.name = "Poop Prefab Visual";
                prefabVisual.transform.localPosition = Vector3.zero;
                prefabVisual.transform.localRotation = Quaternion.identity;
                DisableCollidersInChildren(prefabVisual);
                hotPotatoPoopVisual.SetActive(false);
                return;
            }

            Material material = CreateRuntimeMaterial(new Color(0.36f, 0.19f, 0.08f, 1f));
            AddPoopPiece("Poop Base", new Vector3(0f, 0.28f, 0f), new Vector3(0.82f, 0.28f, 0.82f), material);
            AddPoopPiece("Poop Middle", new Vector3(0f, 0.55f, 0f), new Vector3(0.58f, 0.24f, 0.58f), material);
            AddPoopPiece("Poop Tip", new Vector3(0f, 0.77f, 0f), new Vector3(0.34f, 0.2f, 0.34f), material);
            hotPotatoPoopVisual.SetActive(false);
        }

        private void AddPoopPiece(string pieceName, Vector3 localPosition, Vector3 localScale, Material material)
        {
            if (hotPotatoPoopVisual == null)
            {
                return;
            }

            GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            piece.name = pieceName;
            piece.transform.SetParent(hotPotatoPoopVisual.transform, false);
            piece.transform.localPosition = localPosition;
            piece.transform.localScale = localScale;
            SetPrimitiveMaterial(piece, material);
            DisablePrimitiveCollider(piece);
        }

        private void DisableCollidersInChildren(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                {
                    colliders[i].enabled = false;
                }
            }
        }

        public void GetSkateboardCarryPose(out Vector3 position, out Quaternion rotation)
        {
            Transform carrySource = PlayerCamera != null ? PlayerCamera.transform : transform;
            Vector3 flatForward = Vector3.ProjectOnPlane(carrySource.forward, Vector3.up);
            if (flatForward.sqrMagnitude < 0.01f)
            {
                flatForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            }

            flatForward = flatForward.sqrMagnitude > 0.01f ? flatForward.normalized : Vector3.forward;
            position = carrySource.position
                + flatForward * 0.98f
                + carrySource.right * 0.38f
                + Vector3.down * 0.42f;
            rotation = Quaternion.LookRotation(flatForward, Vector3.up) * Quaternion.Euler(72f, 0f, 90f);
        }

        private void UpdateBoardChargerPlacementGhost()
        {
            boardChargerTargetSlot = -1;
            bool holdingHoverboard =
                heldHoverboardM != null &&
                IsSelectedInventoryItem(MiniVanInventoryItem.HoverboardM) &&
                currentSeat == null &&
                currentHoverboardM == null &&
                currentSkateboard == null;

            MiniVanBoardCharger[] chargers = MiniVanSceneScan.Get<MiniVanBoardCharger>();
            for (int i = 0; i < chargers.Length; i++)
            {
                MiniVanBoardCharger charger = chargers[i];
                if (charger == null)
                {
                    continue;
                }

                if (!holdingHoverboard || lookedAtBoardCharger != charger)
                {
                    charger.HidePlacementGhost();
                    continue;
                }

                charger.UpdatePlacementGhost(heldHoverboardM, PlayerCamera);
                boardChargerTargetSlot = charger.GhostSlotIndex;
            }
        }

        public void GetTowCubeCarryPose(Vector3 carryOffset, out Vector3 position, out Quaternion rotation)
        {
            Transform carrySource = PlayerCamera != null ? PlayerCamera.transform : transform;
            position = carrySource.position
                + carrySource.right * carryOffset.x
                + Vector3.up * carryOffset.y
                + carrySource.forward * carryOffset.z;
            rotation = Quaternion.LookRotation(carrySource.forward, Vector3.up);
        }

        public void GetHotPotatoBombCarryPose(MiniVanHotPotatoBomb bomb, out Vector3 position, out Quaternion rotation)
        {
            Transform carrySource = IsOwner && PlayerCamera != null ? PlayerCamera.transform : transform;
            Vector3 offset = IsOwner && bomb != null ? bomb.PlayerHoldOffset : bomb != null ? bomb.RemoteHoldOffset : new Vector3(0.32f, -0.24f, 0.74f);
            position = carrySource.position
                + carrySource.right * offset.x
                + carrySource.up * offset.y
                + carrySource.forward * offset.z;
            Vector3 forward = Vector3.ProjectOnPlane(carrySource.forward, Vector3.up);
            forward = forward.sqrMagnitude > 0.001f ? forward.normalized : transform.forward;
            rotation = Quaternion.LookRotation(forward, Vector3.up) * Quaternion.Euler(0f, 0f, 10f);
        }

        private MiniVanSkateboardShelf FindLookedAtSkateboardShelf()
        {
            if (PlayerCamera == null || currentSeat != null || currentSkateboard != null)
            {
                return null;
            }

            Ray ray = new Ray(PlayerCamera.transform.position, PlayerCamera.transform.forward);
            RaycastHit[] hits = Physics.RaycastAll(ray, InteractDistance, ~0, QueryTriggerInteraction.Collide);
            System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

            for (int i = 0; i < hits.Length; i++)
            {
                MiniVanSkateboardShelf shelf = hits[i].collider.GetComponentInParent<MiniVanSkateboardShelf>();
                if (shelf != null && shelf.IsInRange(transform.position))
                {
                    return shelf;
                }
            }

            MiniVanSkateboardShelf[] shelves = MiniVanSceneScan.Get<MiniVanSkateboardShelf>();
            MiniVanSkateboardShelf bestShelf = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < shelves.Length; i++)
            {
                if (shelves[i] == null)
                {
                    continue;
                }

                float distance = Vector3.Distance(transform.position, shelves[i].AnchorPosition);
                if (distance < bestDistance && shelves[i].IsInRange(transform.position))
                {
                    bestShelf = shelves[i];
                    bestDistance = distance;
                }
            }

            return bestShelf;
        }
        private MiniVanSkateboard FindLookedAtSkateboard()
        {
            if (PlayerCamera == null || currentSeat != null || currentSkateboard != null)
            {
                return null;
            }

            Ray ray = new Ray(PlayerCamera.transform.position, PlayerCamera.transform.forward);
            RaycastHit[] hits = Physics.RaycastAll(ray, InteractDistance, ~0, QueryTriggerInteraction.Collide);
            System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

            for (int i = 0; i < hits.Length; i++)
            {
                MiniVanSkateboard skateboard = hits[i].collider.GetComponentInParent<MiniVanSkateboard>();
                if (skateboard != null && skateboard.IsAvailable && IsSkateboardInReach(skateboard))
                {
                    return skateboard;
                }
            }

            MiniVanSkateboard[] skateboards = MiniVanSceneScan.Get<MiniVanSkateboard>();
            MiniVanSkateboard bestSkateboard = null;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < skateboards.Length; i++)
            {
                if (skateboards[i] == null || !skateboards[i].IsAvailable)
                {
                    continue;
                }

                float distance = Vector3.Distance(transform.position, skateboards[i].transform.position);
                if (distance < bestDistance && distance <= skateboards[i].MountRadius)
                {
                    bestSkateboard = skateboards[i];
                    bestDistance = distance;
                }
            }

            return bestSkateboard;
        }

        private MiniVanHoverboardM FindLookedAtHoverboardM()
        {
            if (PlayerCamera == null || currentSeat != null || currentSkateboard != null || currentHoverboardM != null)
            {
                return null;
            }

            Ray ray = new Ray(PlayerCamera.transform.position, PlayerCamera.transform.forward);
            RaycastHit[] hits = Physics.RaycastAll(ray, InteractDistance, ~0, QueryTriggerInteraction.Collide);
            System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

            for (int i = 0; i < hits.Length; i++)
            {
                MiniVanHoverboardM hoverboard = hits[i].collider.GetComponentInParent<MiniVanHoverboardM>();
                if (hoverboard != null && hoverboard.IsAvailable && IsHoverboardMInReach(hoverboard))
                {
                    return hoverboard;
                }
            }

            MiniVanHoverboardM[] hoverboards = MiniVanSceneScan.Get<MiniVanHoverboardM>();
            MiniVanHoverboardM bestHoverboard = null;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < hoverboards.Length; i++)
            {
                if (hoverboards[i] == null || !hoverboards[i].IsAvailable)
                {
                    continue;
                }

                float distance = Vector3.Distance(transform.position, hoverboards[i].transform.position);
                if (distance < bestDistance && distance <= hoverboards[i].MountRadius)
                {
                    bestHoverboard = hoverboards[i];
                    bestDistance = distance;
                }
            }

            return bestHoverboard;
        }

        private MiniVanTowCube FindLookedAtTowCube()
        {
            if (PlayerCamera == null || currentSeat != null)
            {
                return null;
            }

            Ray ray = new Ray(PlayerCamera.transform.position, PlayerCamera.transform.forward);
            RaycastHit[] hits = Physics.RaycastAll(ray, InteractDistance, ~0, QueryTriggerInteraction.Collide);
            System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

            for (int i = 0; i < hits.Length; i++)
            {
                MiniVanTowCube cube = hits[i].collider.GetComponentInParent<MiniVanTowCube>();
                if (cube != null && IsTowCubeInReach(cube))
                {
                    return cube;
                }
            }

            MiniVanTowCube[] cubes = MiniVanSceneScan.Get<MiniVanTowCube>();
            MiniVanTowCube bestCube = null;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < cubes.Length; i++)
            {
                if (cubes[i] == null)
                {
                    continue;
                }

                float distance = Vector3.Distance(transform.position, cubes[i].transform.position);
                if (distance < bestDistance && distance <= cubes[i].PickupRadius)
                {
                    bestCube = cubes[i];
                    bestDistance = distance;
                }
            }

            return bestCube;
        }

        private MiniVanHotPotatoBomb FindLookedAtHotPotatoBomb()
        {
            if (PlayerCamera == null || currentSeat != null || heldHotPotatoBomb != null)
            {
                return null;
            }

            Ray ray = new Ray(PlayerCamera.transform.position, PlayerCamera.transform.forward);
            RaycastHit[] hits = Physics.RaycastAll(ray, InteractDistance, ~0, QueryTriggerInteraction.Collide);
            System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

            for (int i = 0; i < hits.Length; i++)
            {
                MiniVanHotPotatoBomb bomb = hits[i].collider.GetComponentInParent<MiniVanHotPotatoBomb>();
                if (bomb != null && bomb.IsAvailable && IsHotPotatoBombInReach(bomb))
                {
                    return bomb;
                }
            }

            MiniVanHotPotatoBomb[] bombs = MiniVanSceneScan.Get<MiniVanHotPotatoBomb>();
            MiniVanHotPotatoBomb bestBomb = null;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < bombs.Length; i++)
            {
                if (bombs[i] == null || !bombs[i].IsAvailable)
                {
                    continue;
                }

                float distance = Vector3.Distance(transform.position, bombs[i].transform.position);
                if (distance < bestDistance && distance <= Mathf.Max(HotPotatoPickupRadius, bombs[i].PickupRadius))
                {
                    bestBomb = bombs[i];
                    bestDistance = distance;
                }
            }

            return bestBomb;
        }

        private MiniVanDoor FindLookedAtDoor()
        {
            if (PlayerCamera == null || currentSeat != null || currentSkateboard != null)
            {
                return null;
            }

            Ray ray = new Ray(PlayerCamera.transform.position, PlayerCamera.transform.forward);
            RaycastHit[] hits = Physics.RaycastAll(ray, InteractDistance, ~0, QueryTriggerInteraction.Collide);
            System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

            for (int i = 0; i < hits.Length; i++)
            {
                MiniVanDoor door = hits[i].collider.GetComponentInParent<MiniVanDoor>();
                if (door != null && door.IsInRange(transform.position))
                {
                    return door;
                }
            }

            return null;
        }

        private bool IsSkateboardInReach(MiniVanSkateboard skateboard)
        {
            if (skateboard == null)
            {
                return false;
            }

            return Vector3.Distance(transform.position, skateboard.transform.position) <= skateboard.MountRadius;
        }

        private bool IsHoverboardMInReach(MiniVanHoverboardM hoverboard)
        {
            return hoverboard != null && Vector3.Distance(transform.position, hoverboard.transform.position) <= hoverboard.MountRadius;
        }

        private bool IsTowCubeInReach(MiniVanTowCube cube)
        {
            return cube != null && cube.IsInReach(transform.position);
        }

        private bool IsHotPotatoBombInReach(MiniVanHotPotatoBomb bomb)
        {
            return bomb != null && Vector3.Distance(transform.position, bomb.transform.position) <= Mathf.Max(HotPotatoPickupRadius, bomb.PickupRadius);
        }

        private void EnforceHotPotatoPlayRadius()
        {
            // The hot-potato circle is only a test/readability marker now; it no longer pulls players back.
        }

        private MiniVanHotPotatoBomb FindActiveHotPotatoBombForRadius()
        {
            MiniVanHotPotatoBomb[] bombs = MiniVanSceneScan.Get<MiniVanHotPotatoBomb>();
            MiniVanHotPotatoBomb bestBomb = null;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < bombs.Length; i++)
            {
                MiniVanHotPotatoBomb bomb = bombs[i];
                if (bomb == null || !bomb.IsPlayRadiusActive)
                {
                    continue;
                }

                float distance = Vector3.Distance(transform.position, bomb.PlayRadiusCenter);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestBomb = bomb;
                }
            }

            return bestBomb;
        }

        [ServerRpc]
        private void RequestSkateboardMountServerRpc(NetworkObjectReference skateboardReference, ServerRpcParams rpcParams = default)
        {
            if (currentSeat != null || currentSkateboard != null || !skateboardReference.TryGet(out NetworkObject skateboardObject))
            {
                return;
            }

            MiniVanSkateboard skateboard = skateboardObject.GetComponent<MiniVanSkateboard>();
            if (skateboard == null || !skateboard.TryMount(rpcParams.Receive.SenderClientId, this))
            {
                return;
            }

            int releasedSlot = FindInventorySlot(MiniVanInventoryItem.Skateboard);
            if (releasedSlot >= 0)
            {
                SetInventorySlot(releasedSlot, MiniVanInventoryItem.None);
            }

            SetHeldSkateboardClientRpc(new NetworkObjectReference(skateboard.NetworkObject), false, -1, BuildOwnerTarget());
            SetSkateboardStateClientRpc(new NetworkObjectReference(skateboard.NetworkObject), true);
        }

        [ServerRpc]
        private void RequestSkateboardPickupServerRpc(NetworkObjectReference skateboardReference, ServerRpcParams rpcParams = default)
        {
            if (currentSeat != null || currentSkateboard != null || !skateboardReference.TryGet(out NetworkObject skateboardObject))
            {
                return;
            }

            int emptySlot = FindFirstEmptyInventorySlot();
            if (emptySlot < 0)
            {
                return;
            }

            MiniVanSkateboard skateboard = skateboardObject.GetComponent<MiniVanSkateboard>();
            if (skateboard == null || !skateboard.TryPickup(rpcParams.Receive.SenderClientId, this))
            {
                return;
            }

            SetInventorySlot(emptySlot, MiniVanInventoryItem.Skateboard);
            networkSelectedSlot.Value = emptySlot;
            SetHeldSkateboardClientRpc(new NetworkObjectReference(skateboard.NetworkObject), true, emptySlot, BuildOwnerTarget());
        }

        [ServerRpc]
        private void RequestSkateboardPlaceOnShelfServerRpc(NetworkObjectReference vehicleReference, NetworkObjectReference skateboardReference, ServerRpcParams rpcParams = default)
        {
            if (!vehicleReference.TryGet(out NetworkObject vehicleObject) || !skateboardReference.TryGet(out NetworkObject skateboardObject))
            {
                return;
            }

            MiniVanSkateboard skateboard = skateboardObject.GetComponent<MiniVanSkateboard>();
            MiniVanSkateboardShelf shelf = vehicleObject.GetComponentInChildren<MiniVanSkateboardShelf>();
            if (skateboard == null || shelf == null || !shelf.IsInRange(transform.position))
            {
                return;
            }

            if (!skateboard.TryPlaceOnShelf(rpcParams.Receive.SenderClientId, shelf))
            {
                return;
            }

            int releasedSlot = FindInventorySlot(MiniVanInventoryItem.Skateboard);
            if (releasedSlot >= 0)
            {
                SetInventorySlot(releasedSlot, MiniVanInventoryItem.None);
            }

            SetHeldSkateboardClientRpc(new NetworkObjectReference(skateboard.NetworkObject), false, -1, BuildOwnerTarget());
        }

        [ServerRpc]
        private void RequestHoverboardMPlaceOnShelfServerRpc(NetworkObjectReference vehicleReference, NetworkObjectReference hoverboardReference, ServerRpcParams rpcParams = default)
        {
            if (!vehicleReference.TryGet(out NetworkObject vehicleObject) || !hoverboardReference.TryGet(out NetworkObject hoverboardObject))
            {
                return;
            }

            MiniVanHoverboardM hoverboard = hoverboardObject.GetComponent<MiniVanHoverboardM>();
            MiniVanSkateboardShelf shelf = vehicleObject.GetComponentInChildren<MiniVanSkateboardShelf>();
            if (hoverboard == null || shelf == null || !shelf.IsInRange(transform.position) || shelf.HasStoredBoard())
            {
                return;
            }

            if (!hoverboard.TryPlaceOnShelf(rpcParams.Receive.SenderClientId, shelf))
            {
                return;
            }

            int releasedSlot = FindInventorySlot(MiniVanInventoryItem.HoverboardM);
            if (releasedSlot >= 0)
            {
                SetInventorySlot(releasedSlot, MiniVanInventoryItem.None);
            }

            SetHeldHoverboardMClientRpc(new NetworkObjectReference(hoverboard.NetworkObject), false, -1, BuildOwnerTarget());
        }

        [ServerRpc]
        private void RequestHoverboardMPlaceOnChargerServerRpc(
            NetworkObjectReference vehicleReference,
            NetworkObjectReference hoverboardReference,
            int slotIndex,
            ServerRpcParams rpcParams = default)
        {
            if (!vehicleReference.TryGet(out NetworkObject vehicleObject) ||
                !hoverboardReference.TryGet(out NetworkObject hoverboardObject))
            {
                return;
            }

            MiniVanHoverboardM hoverboard = hoverboardObject.GetComponent<MiniVanHoverboardM>();
            MiniVanBoardCharger charger = vehicleObject.GetComponentInChildren<MiniVanBoardCharger>();
            if (hoverboard == null || charger == null || !charger.IsInRange(transform.position))
            {
                return;
            }

            if (!hoverboard.TryPlaceOnCharger(rpcParams.Receive.SenderClientId, charger, slotIndex))
            {
                return;
            }

            int releasedSlot = FindInventorySlot(MiniVanInventoryItem.HoverboardM);
            if (releasedSlot >= 0)
            {
                SetInventorySlot(releasedSlot, MiniVanInventoryItem.None);
            }

            SetHeldHoverboardMClientRpc(new NetworkObjectReference(hoverboard.NetworkObject), false, -1, BuildOwnerTarget());
        }

        [ServerRpc]
        private void RequestSkateboardDropServerRpc(NetworkObjectReference skateboardReference, ServerRpcParams rpcParams = default)
        {
            if (currentSeat != null || currentSkateboard != null || !skateboardReference.TryGet(out NetworkObject skateboardObject))
            {
                return;
            }

            MiniVanSkateboard skateboard = skateboardObject.GetComponent<MiniVanSkateboard>();
            if (skateboard == null || !skateboard.TryDrop(rpcParams.Receive.SenderClientId, this))
            {
                return;
            }

            int releasedSlot = FindInventorySlot(MiniVanInventoryItem.Skateboard);
            if (releasedSlot >= 0)
            {
                SetInventorySlot(releasedSlot, MiniVanInventoryItem.None);
            }

            SetHeldSkateboardClientRpc(new NetworkObjectReference(skateboard.NetworkObject), false, -1, BuildOwnerTarget());
        }

        [ClientRpc]
        private void SetHeldSkateboardClientRpc(NetworkObjectReference skateboardReference, bool held, int slotIndex, ClientRpcParams clientRpcParams = default)
        {
            MiniVanSkateboard skateboard = null;
            if (skateboardReference.TryGet(out NetworkObject skateboardObject))
            {
                skateboard = skateboardObject.GetComponent<MiniVanSkateboard>();
            }

            heldSkateboard = held ? skateboard : null;
            heldSkateboardSlot = held ? slotIndex : -1;
            if (held)
            {
                localSelectedSlot = Mathf.Clamp(slotIndex, 0, 3);
            }
        }

        [ServerRpc]
        private void RequestTowCubePickupServerRpc(NetworkObjectReference cubeReference, ServerRpcParams rpcParams = default)
        {
            if (currentSeat != null || heldSkateboard != null || !cubeReference.TryGet(out NetworkObject cubeObject))
            {
                return;
            }

            MiniVanTowCube cube = cubeObject.GetComponent<MiniVanTowCube>();
            if (cube == null || !cube.TryPickup(rpcParams.Receive.SenderClientId, this))
            {
                return;
            }

            SetHeldTowCubeClientRpc(new NetworkObjectReference(cube.NetworkObject), true, BuildOwnerTarget());
        }

        [ServerRpc]
        private void RequestTowCubeDropServerRpc(NetworkObjectReference cubeReference, ServerRpcParams rpcParams = default)
        {
            if (currentSeat != null || currentSkateboard != null || !cubeReference.TryGet(out NetworkObject cubeObject))
            {
                return;
            }

            MiniVanTowCube cube = cubeObject.GetComponent<MiniVanTowCube>();
            if (cube == null || !cube.TryDrop(rpcParams.Receive.SenderClientId, this))
            {
                return;
            }

            SetHeldTowCubeClientRpc(new NetworkObjectReference(cube.NetworkObject), false, BuildOwnerTarget());
        }

        [ServerRpc]
        private void RequestTowCubeAttachToHookServerRpc(NetworkObjectReference cubeReference, ServerRpcParams rpcParams = default)
        {
            if (currentSeat != null || currentSkateboard != null || !cubeReference.TryGet(out NetworkObject cubeObject))
            {
                return;
            }

            MiniVanTowCube cube = cubeObject.GetComponent<MiniVanTowCube>();
            if (cube == null || !cube.TryAttachHeldToHook(rpcParams.Receive.SenderClientId, this))
            {
                return;
            }

            SetHeldTowCubeClientRpc(new NetworkObjectReference(cube.NetworkObject), false, BuildOwnerTarget());
        }

        [ServerRpc]
        private void RequestTowCubeDetachServerRpc(NetworkObjectReference cubeReference, ServerRpcParams rpcParams = default)
        {
            if (!cubeReference.TryGet(out NetworkObject cubeObject))
            {
                return;
            }

            MiniVanTowCube cube = cubeObject.GetComponent<MiniVanTowCube>();
            if (cube == null)
            {
                return;
            }

            cube.TryDetachTowRope(rpcParams.Receive.SenderClientId, this);
        }

        [ClientRpc]
        private void SetHeldTowCubeClientRpc(NetworkObjectReference cubeReference, bool held, ClientRpcParams clientRpcParams = default)
        {
            MiniVanTowCube cube = null;
            if (cubeReference.TryGet(out NetworkObject cubeObject))
            {
                cube = cubeObject.GetComponent<MiniVanTowCube>();
            }

            heldTowCube = held ? cube : null;
        }

[ServerRpc]
        private void RequestHoverboardMPickupServerRpc(NetworkObjectReference hoverboardReference, ServerRpcParams rpcParams = default)
        {
            if (currentSeat != null || currentSkateboard != null || currentHoverboardM != null || heldHoverboardM != null || heldTowCube != null || heldSkateboard != null || !hoverboardReference.TryGet(out NetworkObject hoverboardObject))
            {
                return;
            }

            int emptySlot = FindFirstEmptyInventorySlot();
            if (emptySlot < 0)
            {
                return;
            }

            MiniVanHoverboardM hoverboard = hoverboardObject.GetComponent<MiniVanHoverboardM>();
            if (hoverboard == null || !hoverboard.TryPickup(rpcParams.Receive.SenderClientId, this))
            {
                return;
            }

            SetInventorySlot(emptySlot, MiniVanInventoryItem.HoverboardM);
            networkSelectedSlot.Value = emptySlot;
            SetHeldHoverboardMClientRpc(new NetworkObjectReference(hoverboard.NetworkObject), true, emptySlot, BuildOwnerTarget());
        }

        [ServerRpc]
        private void RequestHoverboardMDropServerRpc(NetworkObjectReference hoverboardReference, ServerRpcParams rpcParams = default)
        {
            if (currentSeat != null || currentSkateboard != null || currentHoverboardM != null || !hoverboardReference.TryGet(out NetworkObject hoverboardObject))
            {
                return;
            }

            MiniVanHoverboardM hoverboard = hoverboardObject.GetComponent<MiniVanHoverboardM>();
            if (hoverboard == null || !hoverboard.TryDrop(rpcParams.Receive.SenderClientId, this))
            {
                return;
            }

            int releasedSlot = FindInventorySlot(MiniVanInventoryItem.HoverboardM);
            if (releasedSlot >= 0)
            {
                SetInventorySlot(releasedSlot, MiniVanInventoryItem.None);
            }

            SetHeldHoverboardMClientRpc(new NetworkObjectReference(hoverboard.NetworkObject), false, -1, BuildOwnerTarget());
        }

        [ClientRpc]
        private void SetHeldHoverboardMClientRpc(NetworkObjectReference hoverboardReference, bool held, int slotIndex, ClientRpcParams clientRpcParams = default)
        {
            MiniVanHoverboardM hoverboard = null;
            if (hoverboardReference.TryGet(out NetworkObject hoverboardObject))
            {
                hoverboard = hoverboardObject.GetComponent<MiniVanHoverboardM>();
            }

            heldHoverboardM = held ? hoverboard : null;
            heldHoverboardMSlot = held ? slotIndex : -1;
            if (held)
            {
                localSelectedSlot = Mathf.Clamp(slotIndex, 0, 3);
            }
        }


[ServerRpc]
        private void RequestHoverboardMMountServerRpc(NetworkObjectReference hoverboardReference, ServerRpcParams rpcParams = default)
        {
            if (currentSeat != null || currentSkateboard != null || currentHoverboardM != null || !hoverboardReference.TryGet(out NetworkObject hoverboardObject))
            {
                return;
            }

            MiniVanHoverboardM hoverboard = hoverboardObject.GetComponent<MiniVanHoverboardM>();
            if (hoverboard == null || !hoverboard.TryMount(rpcParams.Receive.SenderClientId, this))
            {
                return;
            }

            int releasedSlot = FindInventorySlot(MiniVanInventoryItem.HoverboardM);
            if (releasedSlot >= 0)
            {
                SetInventorySlot(releasedSlot, MiniVanInventoryItem.None);
            }

            SetHeldHoverboardMClientRpc(new NetworkObjectReference(hoverboard.NetworkObject), false, -1, BuildOwnerTarget());
            SetHoverboardMStateClientRpc(new NetworkObjectReference(hoverboard.NetworkObject), true);
        }

        [ServerRpc]
        private void RequestHoverboardMExitServerRpc(ServerRpcParams rpcParams = default)
        {
            MiniVanHoverboardM hoverboard = currentHoverboardM;
            if (hoverboard == null || hoverboard.RiderClientId.Value != rpcParams.Receive.SenderClientId)
            {
                MiniVanHoverboardM[] hoverboards = MiniVanSceneScan.Get<MiniVanHoverboardM>();
                for (int i = 0; i < hoverboards.Length; i++)
                {
                    if (hoverboards[i] != null && hoverboards[i].RiderClientId.Value == rpcParams.Receive.SenderClientId)
                    {
                        hoverboard = hoverboards[i];
                        break;
                    }
                }
            }

            if (hoverboard == null || !hoverboard.TryDismount(rpcParams.Receive.SenderClientId))
            {
                return;
            }

            SetHoverboardMStateClientRpc(new NetworkObjectReference(hoverboard.NetworkObject), false);
        }

        [ClientRpc]
        private void SetHoverboardMStateClientRpc(NetworkObjectReference hoverboardReference, bool riding)
        {
            MiniVanHoverboardM hoverboard = null;
            if (hoverboardReference.TryGet(out NetworkObject hoverboardObject))
            {
                hoverboard = hoverboardObject.GetComponent<MiniVanHoverboardM>();
            }

            if (riding && hoverboard == null)
            {
                return;
            }

            currentHoverboardM = riding ? hoverboard : null;
            if (riding)
            {
                heldHoverboardM = null;
                heldHoverboardMSlot = -1;
            }
            currentLadder = null;
            movingPlatformVehicle = null;
            RestoreVehicleCollisionIgnore();

            if (riding)
            {
                currentSkateboard = null;
                currentSeat = null;
                currentVehicle = null;
                gearDragActive = false;
                verticalVelocity = 0f;
                hasSkateboardCameraOrbit = false;
                if (CharacterController != null && CharacterController.enabled)
                {
                    CharacterController.enabled = false;
                }
                StoreFirstPersonCameraPose();
                FollowHoverboardM();
                ApplySkateboardCamera();
            }
            else
            {
                hasSkateboardCameraOrbit = false;
                RestoreFirstPersonCameraPose();
                Vector3 exitSide = PlayerCamera != null ? Vector3.ProjectOnPlane(PlayerCamera.transform.right, Vector3.up) : transform.right;
                if (hoverboard != null)
                {
                    Quaternion exitRotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
                    transform.SetPositionAndRotation(hoverboard.GetExitPosition(exitSide), exitRotation);
                }

                if (CharacterController != null && !CharacterController.enabled)
                {
                    CharacterController.enabled = true;
                }

                verticalVelocity = 0f;
            }
        }

[ServerRpc]
        private void RequestSkateboardExitServerRpc(ServerRpcParams rpcParams = default)
        {
            MiniVanSkateboard skateboard = currentSkateboard;
            if (skateboard == null || skateboard.RiderClientId.Value != rpcParams.Receive.SenderClientId)
            {
                MiniVanSkateboard[] skateboards = MiniVanSceneScan.Get<MiniVanSkateboard>();
                for (int i = 0; i < skateboards.Length; i++)
                {
                    if (skateboards[i] != null && skateboards[i].RiderClientId.Value == rpcParams.Receive.SenderClientId)
                    {
                        skateboard = skateboards[i];
                        break;
                    }
                }
            }

            if (skateboard == null || !skateboard.TryDismount(rpcParams.Receive.SenderClientId))
            {
                return;
            }

            SetSkateboardStateClientRpc(new NetworkObjectReference(skateboard.NetworkObject), false);
        }

[ClientRpc]
        private void SetSkateboardStateClientRpc(NetworkObjectReference skateboardReference, bool riding)
        {
            MiniVanSkateboard skateboard = null;
            if (skateboardReference.TryGet(out NetworkObject skateboardObject))
            {
                skateboard = skateboardObject.GetComponent<MiniVanSkateboard>();
            }

            if (riding && skateboard == null)
            {
                return;
            }

            currentSkateboard = riding ? skateboard : null;
            currentLadder = null;
            movingPlatformVehicle = null;
            RestoreVehicleCollisionIgnore();

            if (riding)
            {
                currentSeat = null;
                currentVehicle = null;
                gearDragActive = false;
                verticalVelocity = 0f;
                hasSkateboardCameraOrbit = false;
                if (CharacterController != null && CharacterController.enabled)
                {
                    CharacterController.enabled = false;
                }
                StoreFirstPersonCameraPose();
                FollowSkateboard();
                ApplySkateboardCamera();
            }
            else
            {
                hasSkateboardCameraOrbit = false;
                RestoreFirstPersonCameraPose();
                Vector3 exitSide = PlayerCamera != null ? Vector3.ProjectOnPlane(PlayerCamera.transform.right, Vector3.up) : transform.right;
                if (skateboard != null)
                {
                    Quaternion exitRotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
                    transform.SetPositionAndRotation(skateboard.GetExitPosition(exitSide), exitRotation);
                }

                if (CharacterController != null && !CharacterController.enabled)
                {
                    CharacterController.enabled = true;
                }

                verticalVelocity = 0f;
            }
        }


private void HandleDriverInput()
        {
            if (currentVehicle == null)
            {
                return;
            }

            if (!gearDragActive && Time.time - lastLocalGearChangeTime > 0.35f)
            {
                localGearForUi = (MiniVanGear)currentVehicle.CurrentGear.Value;
            }

            if (Input.GetKeyDown(KeyCode.F))
            {
                currentVehicle.SetEngineServerRpc(!currentVehicle.EngineOn.Value);
            }

            if (Input.GetKeyDown(KeyCode.G))
            {
                RequestRescueHornServerRpc(new NetworkObjectReference(currentVehicle.NetworkObject));
            }

            if (localHandbrakeVehicle != currentVehicle)
            {
                localHandbrakeVehicle = currentVehicle;
                localHandbrakeLocked = currentVehicle.HandbrakeLocked.Value;
            }

            if (Input.GetKeyDown(KeyCode.P))
            {
                localHandbrakeLocked = !localHandbrakeLocked;
            }

            float steering = 0f;

            if (MiniVanKeyBindings.GetKey(MiniVanKeyAction.MoveLeft))
            {
                steering -= 1f;
            }

            if (MiniVanKeyBindings.GetKey(MiniVanKeyAction.MoveRight))
            {
                steering += 1f;
            }

            bool spaceHeld = MiniVanKeyBindings.GetKey(MiniVanKeyAction.Jump);
            bool spaceDrift = spaceHeld && Mathf.Abs(steering) > 0.05f;
            float throttle = MiniVanKeyBindings.GetKey(MiniVanKeyAction.MoveForward) ? 1f : 0f;
            float brake = spaceHeld && !spaceDrift ? 1f : 0f;
            bool submittedHandbrake = localHandbrakeLocked || spaceDrift;

            currentVehicle.SubmitDriverInputServerRpc(throttle, brake, steering, submittedHandbrake);
            HandleGearDrag();
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestRescueHornServerRpc(NetworkObjectReference vehicleReference, ServerRpcParams rpcParams = default)
        {
            if (!vehicleReference.TryGet(out NetworkObject vehicleObject))
            {
                return;
            }

            MiniVanVehicle vehicle = vehicleObject.GetComponent<MiniVanVehicle>();
            if (vehicle == null || !vehicle.IsDriver(rpcParams.Receive.SenderClientId))
            {
                return;
            }

            PlayRescueHornClientRpc(vehicle.transform.position);
            GameModeServerTryOpenHornGate(vehicle.transform.position);
            MiniVanRescueMission.ServerTryStartBoarding(this, vehicle);
        }

private void HandleGearDrag()
        {
            if (Input.GetMouseButtonDown(0))
            {
                gearDragActive = true;
                gearDrag = GetCurrentGearDragPosition();
            }

            if (gearDragActive && Input.GetMouseButton(0))
            {
                gearDrag += new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * GearDragSensitivity;
                gearDrag = Vector2.ClampMagnitude(gearDrag, 1.15f);
            }

            if (gearDragActive && Input.GetMouseButtonUp(0))
            {
                MiniVanGear requestedGear = ResolveGearFromDrag(gearDrag);

                if (CanRequestGearLocally(requestedGear))
                {
                    localGearForUi = requestedGear;
                    lastLocalGearChangeTime = Time.time;
                    currentVehicle.SetGearServerRpc((int)requestedGear);
                }

                gearDragActive = false;
            }
        }

private Vector2 GetCurrentGearDragPosition()
        {
            MiniVanGear gear = localGearForUi;

            if (gear == MiniVanGear.Neutral && currentVehicle != null)
            {
                gear = (MiniVanGear)currentVehicle.CurrentGear.Value;
            }

            switch (gear)
            {
                case MiniVanGear.First:
                    return new Vector2(-0.78f, 0.88f);
                case MiniVanGear.Second:
                    return new Vector2(-0.78f, -0.88f);
                case MiniVanGear.Third:
                    return new Vector2(0f, 0.88f);
                case MiniVanGear.Fourth:
                    return new Vector2(0f, -0.88f);
                case MiniVanGear.Fifth:
                    return new Vector2(0.78f, 0.88f);
                case MiniVanGear.Reverse:
                    return new Vector2(0.78f, -0.88f);
                default:
                    return Vector2.zero;
            }
        }

private bool CanRequestGearLocally(MiniVanGear gear)
        {
            if (currentVehicle == null)
            {
                return false;
            }

            float speedKph = currentVehicle.SpeedKph.Value;

            switch (gear)
            {
                case MiniVanGear.Reverse:
                    return speedKph <= 8f;
                case MiniVanGear.Park:
                    return speedKph <= 1f;
                default:
                    return true;
            }
        }



        private MiniVanGear ResolveGearFromDrag(Vector2 drag)
        {
            if (drag.magnitude < 0.28f)
            {
                return MiniVanGear.Neutral;
            }

            bool top = drag.y > 0.35f;
            bool bottom = drag.y < -0.35f;
            bool left = drag.x < -0.45f;
            bool center = Mathf.Abs(drag.x) <= 0.45f;
            bool right = drag.x > 0.45f;

            if (left && top)
            {
                return MiniVanGear.First;
            }

            if (left && bottom)
            {
                return MiniVanGear.Second;
            }

            if (center && top)
            {
                return MiniVanGear.Third;
            }

            if (center && bottom)
            {
                return MiniVanGear.Fourth;
            }

            if (right && top)
            {
                return MiniVanGear.Fifth;
            }

            if (right && bottom)
            {
                return MiniVanGear.Reverse;
            }

            return MiniVanGear.Neutral;
        }

        private void FollowSeat()
        {
            if (currentSeat == null || currentSeat.SitPoint == null)
            {
                return;
            }

            transform.SetPositionAndRotation(currentSeat.SitPoint.position, currentSeat.SitPoint.rotation);
        }

        private MiniVanSeat FindLookedAtSeat()
        {
            if (PlayerCamera == null)
            {
                return null;
            }

            Ray ray = new Ray(PlayerCamera.transform.position, PlayerCamera.transform.forward);
            RaycastHit[] hits = Physics.RaycastAll(ray, InteractDistance, ~0, QueryTriggerInteraction.Collide);

            System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

            for (int i = 0; i < hits.Length; i++)
            {
                MiniVanSeat seat = hits[i].collider.GetComponentInParent<MiniVanSeat>();

                if (seat != null && seat.Vehicle != null && seat.IsAvailable &&
                    seat.IsPlayerInEnterRange(transform.position))
                {
                    return seat;
                }
            }

            return FindNearestSeat();
        }

        private MiniVanSeat FindNearestSeat()
        {
            Collider[] colliders = Physics.OverlapSphere(transform.position, NearbySeatRadius, ~0, QueryTriggerInteraction.Collide);
            MiniVanSeat bestSeat = null;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < colliders.Length; i++)
            {
                MiniVanSeat seat = colliders[i].GetComponentInParent<MiniVanSeat>();

                if (seat == null || seat.Vehicle == null || !seat.IsAvailable ||
                    !seat.IsPlayerInEnterRange(transform.position))
                {
                    continue;
                }

                Transform entry = seat.GetEnterPoint();
                float distance = Vector3.Distance(transform.position, entry != null ? entry.position : seat.transform.position);

                if (distance < bestDistance)
                {
                    bestSeat = seat;
                    bestDistance = distance;
                }
            }

            return bestSeat;
        }

        private void ConfigureLocalCamera(bool enabled)
        {
            if (PlayerCamera != null)
            {
                PlayerCamera.enabled = enabled;
            }

            AudioListener listener = PlayerCamera != null ? PlayerCamera.GetComponent<AudioListener>() : null;

            if (listener != null)
            {
                listener.enabled = enabled;
            }

            ApplyFirstPersonVisibility(enabled && !IsFacePainting());
        }

        private void MoveToSpawnPoint()
        {
            GameObject spawnPoint = GameObject.Find("Player Spawn Point");

            if (spawnPoint == null)
            {
                return;
            }

            float offset = ((int)(OwnerClientId % 4) - 1.5f) * 0.75f;
            Quaternion spawnRotation = spawnPoint.transform.rotation;
            Vector3 basePosition = spawnPoint.transform.position + spawnPoint.transform.right * offset;
            Vector3 spawnPosition = FindSafeSpawnPosition(basePosition, spawnRotation,
                spawnPoint.transform.right, spawnPoint.transform.forward);

            bool controllerWasEnabled = CharacterController != null && CharacterController.enabled;
            if (controllerWasEnabled)
            {
                CharacterController.enabled = false;
            }

            transform.SetPositionAndRotation(spawnPosition, spawnRotation);
            // Extra lift so revive/R never starts inside the floor mesh.
            transform.position += Vector3.up * 0.35f;
            Physics.SyncTransforms();

            if (controllerWasEnabled)
            {
                CharacterController.enabled = true;
                CharacterController.Move(Vector3.zero);
            }

            verticalVelocity = 0f;
            fallDamageTracking = false;
            StartCoroutine(ConfirmSpawnAboveSurface());
        }

        private Vector3 FindSafeSpawnPosition(Vector3 basePosition, Quaternion rotation,
            Vector3 right, Vector3 forward)
        {
            Vector3[] offsets =
            {
                Vector3.zero,
                right * 1.4f,
                -right * 1.4f,
                forward * 1.4f,
                -forward * 1.4f,
                (right + forward).normalized * 1.8f,
                (-right + forward).normalized * 1.8f,
                (right - forward).normalized * 1.8f,
                (-right - forward).normalized * 1.8f
            };

            Vector3 fallback = basePosition;
            for (int i = 0; i < offsets.Length; i++)
            {
                Vector3 candidate = basePosition + offsets[i];
                if (!TryPlaceAboveSurface(candidate, out candidate))
                {
                    continue;
                }

                fallback = candidate;
                if (IsSpawnCapsuleClear(candidate, rotation))
                {
                    return candidate;
                }
            }

            return fallback;
        }

        private bool TryPlaceAboveSurface(Vector3 position, out Vector3 safePosition)
        {
            safePosition = position;
            Vector3 origin = position + Vector3.up * 10f;
            RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 40f, ~0,
                QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].transform.IsChildOf(transform) || hits[i].normal.y < 0.55f)
                {
                    continue;
                }

                float bottomOffset = CharacterController != null
                    ? CharacterController.height * 0.5f - CharacterController.center.y
                    : 1f;
                float skin = CharacterController != null ? CharacterController.skinWidth : 0.08f;
                safePosition.y = hits[i].point.y + bottomOffset + skin + 0.45f;
                return true;
            }

            return false;
        }

        private bool IsSpawnCapsuleClear(Vector3 position, Quaternion rotation)
        {
            if (CharacterController == null)
            {
                return true;
            }

            float radius = Mathf.Max(0.05f, CharacterController.radius - CharacterController.skinWidth * 0.5f);
            float halfHeight = Mathf.Max(radius, CharacterController.height * 0.5f);
            Vector3 center = position + rotation * CharacterController.center;
            Vector3 bottom = center + Vector3.down * (halfHeight - radius);
            Vector3 top = center + Vector3.up * (halfHeight - radius);
            Collider[] overlaps = Physics.OverlapCapsule(bottom, top, radius, ~0,
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < overlaps.Length; i++)
            {
                if (overlaps[i] != null && !overlaps[i].transform.IsChildOf(transform))
                {
                    return false;
                }
            }
            return true;
        }

        private IEnumerator ConfirmSpawnAboveSurface()
        {
            for (int step = 0; step < 3; step++)
            {
                yield return new WaitForFixedUpdate();
                if (!IsOwner || currentSeat != null || currentSkateboard != null || currentHoverboardM != null)
                {
                    yield break;
                }

                if (!TryPlaceAboveSurface(transform.position + Vector3.up * 0.5f, out Vector3 corrected))
                {
                    yield break;
                }

                if (transform.position.y >= corrected.y - 0.02f)
                {
                    yield break;
                }

                bool controllerWasEnabled = CharacterController != null && CharacterController.enabled;
                if (controllerWasEnabled)
                {
                    CharacterController.enabled = false;
                }

                transform.position = corrected;
                Physics.SyncTransforms();
                if (controllerWasEnabled)
                {
                    CharacterController.enabled = true;
                    CharacterController.Move(Vector3.zero);
                }

                verticalVelocity = 0f;
                fallDamageTracking = false;
            }
        }

        private void DisableOverviewCameras()
        {
            Camera[] cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);

            for (int i = 0; i < cameras.Length; i++)
            {
                if (cameras[i] != PlayerCamera && cameras[i].name == "Overview Camera")
                {
                    cameras[i].enabled = false;
                }
            }
        }

private void OnGUI()
        {
            if (!IsOwner)
            {
                return;
            }

            if (!equipmentWindowOpen)
            {
                DrawCrosshair();
            }

            // Persistent bars / roster / hotbar / ping live on MiniVanHUD canvases when available.
            if (!HasCanvasHud())
            {
                DrawPingStats();
                DrawInventoryHotbar();
                DrawStatusHud();
            }

            DrawFlamethrowerRechargeBar();
            DrawFireExtinguisherChargeBar();
            DrawWinchFoldProgress();
            DrawDeathSystemGui();
            DrawAntonGui();
            DrawDriverEngineHud();
            if (hudUi == null || !hudUi.HasEnemyCombatWidgets)
            {
                MiniVanEnemyCombatHud.DrawLegacyOnGui();
            }

            DrawInteractionPrompt();
            DrawRidingExitHoldRing();
            DrawPizzaGameplayUi();
            DrawFuelInteractionGui();

            if (gearDragActive)
            {
                DrawGearPattern();
            }

            DrawDamageFlash();
        }

        private void DrawStatusHud()
        {
            DrawPlayerHealthBar();
            DrawPlayerOxygenBar();
            DrawMiniVanHealthBar();
        }

        private void DrawDriverEngineHud()
        {
            // RPM / sweet-spot indication lives on the dashboard "RPM Gauge" now.
        }

        private void DrawPlayerHealthBar()
        {
            float width = Mathf.Clamp(Screen.width * PlayerHealthBarScreenWidth, PlayerHealthBarMinWidth, PlayerHealthBarMaxWidth);
            Rect rect = new Rect(PlayerHealthBarX, Screen.height - PlayerHealthBarBottom, width, PlayerHealthBarHeight);
            float value01 = Mathf.Clamp01(networkHealth.Value / (float)Mathf.Max(1, MaxPlayerHealth));
            DrawHudBar(rect, value01, PlayerHealthBarFillColor, PlayerHealthBarEmptyColor);
        }

        private void DrawPlayerOxygenBar()
        {
            if (!IsDamWaterHeadSubmerged())
            {
                return;
            }

            float width = Mathf.Clamp(Screen.width * PlayerHealthBarScreenWidth, PlayerHealthBarMinWidth, PlayerHealthBarMaxWidth);
            Rect rect = new Rect(
                PlayerHealthBarX,
                Screen.height - PlayerHealthBarBottom - PlayerHealthBarHeight - 10f,
                width,
                PlayerHealthBarHeight);
            float value01 = Mathf.Clamp01(damWaterOxygenRemaining / DamWaterOxygenSeconds);
            DrawHudBar(rect, value01, PlayerOxygenBarFillColor, PlayerOxygenBarEmptyColor);
        }

        private void DrawMiniVanHealthBar()
        {
            MiniVanVehicle vehicle = ResolveHudVehicle();
            if (vehicle == null)
            {
                return;
            }

            float width = Mathf.Clamp(Screen.width * MiniVanHealthBarScreenWidth, MiniVanHealthBarMinWidth, MiniVanHealthBarMaxWidth);
            Rect bar = new Rect(
                Screen.width * 0.5f - width * 0.5f + MiniVanHealthBarCenterOffsetX,
                MiniVanHealthBarTop,
                width,
                MiniVanHealthBarHeight);
            bar.position += MiniVanHealthBarOffset;

            Vector2 iconSize = new Vector2(
                Mathf.Max(1f, MiniVanHealthIconSize.x),
                Mathf.Max(1f, MiniVanHealthIconSize.y));
            Rect iconRect = new Rect(
                bar.x - iconSize.x - Mathf.Max(0f, MiniVanHealthIconGap),
                bar.y - 8f,
                iconSize.x,
                iconSize.y);
            iconRect.position += MiniVanHealthIconOffset;

            DrawMiniVanInteractiveHudIcon(iconRect, vehicle);
            float health01 = Mathf.Clamp01(vehicle.Health01);
            Color fill = EvaluateHudHealthColor(health01, MiniVanHealthBarFillColor);
            DrawHudBar(bar, health01, fill, MiniVanHealthBarEmptyColor);
        }

        private void EnsureMiniVanHudIcons()
        {
            if (MiniVanHudBodyIcon == null)
            {
                MiniVanHudBodyIcon = Resources.Load<Texture2D>("UI/minivan-hud-body");
            }

            if (MiniVanHudWheelIcon == null)
            {
                MiniVanHudWheelIcon = Resources.Load<Texture2D>("UI/minivan-hud-wheel");
            }
        }

        private void DrawMiniVanInteractiveHudIcon(Rect iconRect, MiniVanVehicle vehicle)
        {
            EnsureMiniVanHudIcons();

            int detached = vehicle != null ? vehicle.DetachedWheelIndex.Value : -1;
            // Side view facing right: left wheel = rear axle, right wheel = front axle.
            bool rearLost = detached == 2 || detached == 3;
            bool frontLost = detached == 0 || detached == 1;

            Texture2D body = MiniVanHudBodyIcon;
            Texture2D wheel = MiniVanHudWheelIcon;
            if (body == null && wheel == null)
            {
                if (MiniVanHealthIconImage != null)
                {
                    GUI.DrawTexture(iconRect, MiniVanHealthIconImage, ScaleMode.ScaleToFit, true);
                }
                else
                {
                    DrawMiniVanHudIcon(iconRect, frontLost, rearLost);
                }

                return;
            }

            Color previous = GUI.color;
            if (body != null)
            {
                GUI.color = MiniVanHudIconOkColor;
                GUI.DrawTexture(iconRect, body, ScaleMode.ScaleToFit, true);
            }
            else if (MiniVanHealthIconImage != null)
            {
                GUI.color = MiniVanHudIconOkColor;
                GUI.DrawTexture(iconRect, MiniVanHealthIconImage, ScaleMode.ScaleToFit, true);
            }

            if (wheel != null)
            {
                // Match body wheel-arch anchors (side view facing right: rear left, front right).
                float wheelSize = Mathf.Min(iconRect.width, iconRect.height) * 0.30f;
                float wheelY = iconRect.y + iconRect.height * 0.58f;
                Rect rearWheel = new Rect(
                    iconRect.x + iconRect.width * 0.28f - wheelSize * 0.5f,
                    wheelY,
                    wheelSize,
                    wheelSize);
                Rect frontWheel = new Rect(
                    iconRect.x + iconRect.width * 0.68f - wheelSize * 0.5f,
                    wheelY,
                    wheelSize,
                    wheelSize);

                GUI.color = rearLost ? MiniVanHudIconLostWheelColor : MiniVanHudIconOkColor;
                GUI.DrawTexture(rearWheel, wheel, ScaleMode.ScaleToFit, true);
                GUI.color = frontLost ? MiniVanHudIconLostWheelColor : MiniVanHudIconOkColor;
                GUI.DrawTexture(frontWheel, wheel, ScaleMode.ScaleToFit, true);
            }

            GUI.color = previous;
        }

        private MiniVanVehicle ResolveHudVehicle()
        {
            if (currentVehicle != null)
            {
                cachedHudVehicle = currentVehicle;
                return currentVehicle;
            }

            if (cachedHudVehicle == null || !cachedHudVehicle.gameObject.activeInHierarchy)
            {
                cachedHudVehicle = FindFirstObjectByType<MiniVanVehicle>();
            }

            return cachedHudVehicle;
        }

        private static void DrawHudBar(Rect rect, float value01, Color fill, Color empty)
        {
            Color previous = GUI.color;
            GUI.color = HudBarBorderColor;
            GUI.DrawTexture(new Rect(rect.x - 3f, rect.y - 3f, rect.width + 6f, rect.height + 6f), Texture2D.whiteTexture);
            GUI.color = empty;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = fill;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(value01), rect.height), Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private static Color EvaluateHudHealthColor(float value01, Color fullColor)
        {
            Color mid = new Color(1f, 0.82f, 0.12f, 1f);
            Color low = new Color(0.9f, 0.12f, 0.08f, 1f);
            if (value01 >= 0.5f)
            {
                return Color.Lerp(mid, fullColor, (value01 - 0.5f) * 2f);
            }

            return Color.Lerp(low, mid, value01 * 2f);
        }

        private void DrawMiniVanHudIcon(Rect rect, bool frontLost, bool rearLost)
        {
            Color previous = GUI.color;
            GUI.color = MiniVanHudIconOkColor;
            GUI.DrawTexture(new Rect(rect.x + rect.width * 0.12f, rect.y + rect.height * 0.35f, rect.width * 0.7f, rect.height * 0.34f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x + rect.width * 0.31f, rect.y + rect.height * 0.16f, rect.width * 0.38f, rect.height * 0.24f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x + rect.width * 0.75f, rect.y + rect.height * 0.44f, rect.width * 0.12f, rect.height * 0.25f), Texture2D.whiteTexture);
            float wheel = rect.width * 0.16f;
            GUI.color = rearLost ? MiniVanHudIconLostWheelColor : MiniVanHudIconOkColor;
            GUI.DrawTexture(new Rect(rect.x + rect.width * 0.22f, rect.y + rect.height * 0.66f, wheel, wheel), Texture2D.whiteTexture);
            GUI.color = frontLost ? MiniVanHudIconLostWheelColor : MiniVanHudIconOkColor;
            GUI.DrawTexture(new Rect(rect.x + rect.width * 0.64f, rect.y + rect.height * 0.66f, wheel, wheel), Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private void DrawCrosshair()
        {
            float x = Screen.width * 0.5f;
            float y = Screen.height * 0.5f;
            GUI.Label(new Rect(x - 8f, y - 10f, 20f, 20f), "+");
        }

        private void DrawDamageFlash()
        {
            float remaining = damageFlashUntil - Time.time;
            if (remaining <= 0f)
            {
                return;
            }

            EnsureDamageFlashTexture();
            if (damageFlashTexture == null)
            {
                return;
            }

            float strength = Mathf.Clamp01(remaining / DamageFlashSeconds);
            Color oldColor = GUI.color;
            int bands = 8;
            float maxBand = Mathf.Min(Screen.width, Screen.height) * 0.16f;
            for (int i = 0; i < bands; i++)
            {
                float t = 1f - (float)i / bands;
                float alpha = 0.26f * strength * t * t;
                float band = maxBand * (i + 1f) / bands;
                float previousBand = maxBand * i / bands;
                float thickness = band - previousBand;
                GUI.color = new Color(1f, 0f, 0f, alpha);
                GUI.DrawTexture(new Rect(0f, previousBand, Screen.width, thickness), damageFlashTexture);
                GUI.DrawTexture(new Rect(0f, Screen.height - band, Screen.width, thickness), damageFlashTexture);
                GUI.DrawTexture(new Rect(previousBand, 0f, thickness, Screen.height), damageFlashTexture);
                GUI.DrawTexture(new Rect(Screen.width - band, 0f, thickness, Screen.height), damageFlashTexture);
            }

            GUI.color = oldColor;
        }

        private static void EnsureDamageFlashTexture()
        {
            if (damageFlashTexture != null)
            {
                return;
            }

            damageFlashTexture = new Texture2D(1, 1);
            damageFlashTexture.SetPixel(0, 0, Color.white);
            damageFlashTexture.Apply();
        }

        private void DrawInteractionPrompt()
        {
            if (IsPlayingSnesTelevision())
            {
                DrawSnesControlsHint();
                return;
            }

            string prompt = GetSnesInteractionPrompt();
            if (!string.IsNullOrEmpty(prompt))
            {
                // SNES interaction wins over other world prompts.
            }
            else if (currentSeat == null)
            {
                if (heldHoverboardM != null && IsSelectedInventoryItem(MiniVanInventoryItem.HoverboardM) &&
                    currentHoverboardM == null && currentSkateboard == null)
                {
                    if (lookedAtBoardCharger != null && boardChargerTargetSlot >= 0)
                    {
                        prompt = "E - place on charger, Q - drop";
                    }
                    else if (lookedAtSkateboardShelf != null)
                    {
                        prompt = "E - put hoverboard on shelf, Q - drop";
                    }
                    else
                    {
                        prompt = "E - ride, Q - drop";
                    }
                }
                else if (heldSkateboard != null && IsSelectedInventoryItem(MiniVanInventoryItem.Skateboard) &&
                         currentHoverboardM == null && currentSkateboard == null)
                {
                    prompt = lookedAtSkateboardShelf != null
                        ? "E - put skateboard on shelf, Q - drop, RMB - ride"
                        : "Q - drop skateboard, RMB - ride";
                }
                else if (hasCoffee && IsSelectedInventoryItem(MiniVanInventoryItem.Coffee))
                {
                    prompt = "RMB - drink coffee, Q - drop";
                }
                else if (heldHotPotatoBomb != null && IsSelectedInventoryItem(MiniVanInventoryItem.HotPotatoBomb))
                {
                    prompt = heldHotPotatoBomb.IsActivated ? "LMB - throw bomb" : "LMB - throw bomb, E - drop";
                }
                else if (heldTowCube != null)
                {
                    prompt = heldTowCube.HasTowHookInRange(transform.position) ? "LMB - attach cube to tow hook, E - drop" : "E - drop tow cube";
                }
                else if (IsSelectedInventoryItem(MiniVanInventoryItem.Winch) || MiniVanWinchCable.HasActiveCable(this))
                {
                    prompt = "E - attach winch end, hold E - fold";
                }
                else if (lookedAtBoardCharger != null && currentHoverboardM == null && currentSkateboard == null)
                {
                    MiniVanHoverboardM dockedBoard = lookedAtBoardCharger.FindNearestDockedBoard(transform.position);
                    if (dockedBoard != null && dockedBoard.IsStillChargingOnDock)
                    {
                        prompt = MiniVanHoverboardM.PromptStillCharging;
                    }
                    else if (dockedBoard != null && dockedBoard.IsFullyCharged)
                    {
                        prompt = "E - take hoverboard";
                    }
                }
                else if (lookedAtHoverboardM != null && lookedAtHoverboardM.IsStillChargingOnDock)
                {
                    prompt = MiniVanHoverboardM.PromptStillCharging;
                }
                else if (lookedAtSkateboardShelf != null && lookedAtSkateboardShelf.FindStoredSkateboard() != null)
                {
                    prompt = "E - take skateboard from shelf";
                }
                else if (lookedAtHoverboardM != null)
                {
                    prompt = "E - pick up hoverboardM";
                }
                else if (lookedAtSkateboard != null)
                {
                    prompt = "E - pick up skateboard";
                }
                else if (lookedAtTowCube != null)
                {
                    prompt = lookedAtTowCube.TowAttached.Value ? "LMB - detach tow rope" : "E - pick up tow cube";
                }
                else if (lookedAtWoodenBoard != null)
                {
                    prompt = "E - take board";
                }
                else if (lookedAtWinchPickup != null)
                {
                    prompt = "E - pick up winch";
                }
                else if (lookedAtHotPotatoBomb != null)
                {
                    prompt = "E - pick up bomb";
                }
                else if (!MiniVanGameModeInteractionSystem.IsLocalRoofAttach() &&
                         GetInteractableDoor() != null)
                {
                    MiniVanDoor interactDoor = GetInteractableDoor();
                    prompt = interactDoor.IsRoofDoor
                        ? "E - open/close roof hatch"
                        : "E - open/close door";
                }
                else if (lookedAtCoffee != null)
                {
                    prompt = "E - take coffee";
                }
                else if (lookedAtBat != null)
                {
                    prompt = "E - pick up bat";
                }
                else if (lookedAtPizzaItem != null)
                {
                    prompt = "E - pick up " + GetInventoryLabel(lookedAtPizzaItem.Item);
                }
                else if (IsCarryingAnton)
                {
                    MiniVanSeat antonSeat = FindLookedAtPassengerSeatForAnton();
                    prompt = antonSeat != null ? "E - seat Anton" : null;
                }
                else if (lookedAtSeat != null && currentHoverboardM == null && currentSkateboard == null)
                {
                    prompt = "E - sit";
                }
                else
                {
                    string hoverboardExitHint = HasCarriedHandItem()
                        ? "Q - drop, hold Q - step off"
                        : "Q - step off";
                    string gameModePrompt = MiniVanGameModeInteractionSystem.GetLocalPrompt();
                    if (!string.IsNullOrEmpty(gameModePrompt))
                    {
                        if (currentHoverboardM != null)
                        {
                            prompt = gameModePrompt + "  |  " + hoverboardExitHint;
                        }
                        else if (currentSkateboard != null)
                        {
                            prompt = gameModePrompt + "  |  RMB - step off";
                        }
                        else
                        {
                            prompt = gameModePrompt;
                        }
                    }
                    else if (currentHoverboardM != null && !currentHoverboardM.HasBatteryPower)
                    {
                        prompt = MiniVanHoverboardM.PromptLowBattery;
                    }
                    else if (currentSkateboard != null)
                    {
                        prompt = "RMB - step off";
                    }
                    else if (currentHoverboardM != null)
                    {
                        prompt = hoverboardExitHint;
                    }
                }
            }

            if (string.IsNullOrEmpty(prompt))
            {
                return;
            }

            GUIStyle style = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 18,
                fontStyle = FontStyle.Bold
            };
            style.normal.textColor = Color.white;
            Rect rect = new Rect(Screen.width * 0.5f - 145f, Screen.height * 0.58f, 290f, 36f);
            GUI.Box(rect, prompt, style);
        }

        private void DrawInventoryHotbar()
        {
            if (equipmentWindowOpen)
            {
                // The equipment window shows its own draggable copy of the inventory cells.
                return;
            }

            const float slotSize = 58f;
            const float gap = 8f;
            float totalWidth = slotSize * 4f + gap * 3f;
            float startX = Screen.width * 0.5f - totalWidth * 0.5f;
            float y = Screen.height - 84f;

            GUIStyle labelStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 13,
                fontStyle = FontStyle.Bold
            };
            labelStyle.normal.textColor = Color.white;

            for (int i = 0; i < 4; i++)
            {
                Rect rect = new Rect(startX + i * (slotSize + gap), y, slotSize, slotSize);
                bool selected = i == localSelectedSlot;
                DrawSolidRect(rect, selected ? new Color(0.95f, 0.82f, 0.22f, 0.78f) : new Color(0f, 0f, 0f, 0.54f));
                DrawSolidRect(new Rect(rect.x + 3f, rect.y + 3f, rect.width - 6f, rect.height - 6f), new Color(0.12f, 0.13f, 0.14f, 0.86f));

                MiniVanInventoryItem item = GetInventorySlot(i);
                string label = GetInventoryLabel(item);
                GUI.Label(new Rect(rect.x, rect.y + 16f, rect.width, 22f), label, labelStyle);
                GUI.Label(new Rect(rect.x, rect.y + 36f, rect.width, 18f), (i + 1).ToString(), labelStyle);

                if (item == MiniVanInventoryItem.Winch)
                {
                    Rect barBack = new Rect(rect.x + 7f, rect.y + rect.height - 10f, rect.width - 14f, 4f);
                    float durability = Mathf.Clamp01(networkWinchDurability.Value);
                    DrawSolidRect(barBack, new Color(0f, 0f, 0f, 0.85f));
                    DrawSolidRect(new Rect(barBack.x, barBack.y, barBack.width * durability, barBack.height),
                        Color.Lerp(new Color(0.85f, 0.12f, 0.08f, 1f), new Color(0.16f, 0.84f, 0.22f, 1f), durability));
                }
            }
        }

        private void DrawWinchFoldProgress()
        {
            float progress = MiniVanWinchCable.GetFoldProgress01(this);
            if (progress <= 0f)
            {
                return;
            }

            Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f + 72f);
            DrawCircle(center, 78f, new Color(0f, 0f, 0f, 0.45f));
            DrawWinchFoldRing(center, 78f, progress);

            if (progress >= 0.985f)
            {
                DrawCircle(center, 10f, new Color(0.18f, 0.82f, 0.28f, 0.9f));
            }
        }

        private static void DrawWinchFoldRing(Vector2 center, float diameter, float progress)
        {
            Texture2D texture = GetWinchFoldProgressTexture(progress);
            if (texture == null)
            {
                return;
            }

            Color oldColor = GUI.color;
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(center.x - diameter * 0.5f, center.y - diameter * 0.5f, diameter, diameter), texture);
            GUI.color = oldColor;
        }

        private static Texture2D GetWinchFoldProgressTexture(float progress)
        {
            const int size = 96;
            int bucket = Mathf.Clamp(Mathf.RoundToInt(progress * 100f), 0, 100);
            if (winchFoldProgressTexture != null && winchFoldProgressBucket == bucket)
            {
                return winchFoldProgressTexture;
            }

            if (winchFoldProgressTexture == null)
            {
                winchFoldProgressTexture = new Texture2D(size, size, TextureFormat.ARGB32, false)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Bilinear
                };
            }

            float outerRadius = size * 0.5f - 1.5f;
            float innerRadius = outerRadius - 12f;
            float fillAngle = bucket * 3.6f;
            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            Color empty = new Color(0f, 0f, 0f, 0f);
            Color fill = new Color(0.95f, 0.82f, 0.22f, 0.98f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 pixel = new Vector2(x + 0.5f, y + 0.5f) - center;
                    float distance = pixel.magnitude;
                    bool inRing = distance >= innerRadius && distance <= outerRadius;
                    if (!inRing)
                    {
                        winchFoldProgressTexture.SetPixel(x, y, empty);
                        continue;
                    }

                    float angle = Mathf.Atan2(pixel.y, pixel.x) * Mathf.Rad2Deg + 90f;
                    if (angle < 0f)
                    {
                        angle += 360f;
                    }

                    float edgeAlpha = Mathf.Clamp01(outerRadius - distance + 1f) * Mathf.Clamp01(distance - innerRadius + 1f);
                    winchFoldProgressTexture.SetPixel(x, y, angle <= fillAngle ? new Color(fill.r, fill.g, fill.b, fill.a * edgeAlpha) : empty);
                }
            }

            winchFoldProgressTexture.Apply();
            winchFoldProgressBucket = bucket;
            return winchFoldProgressTexture;
        }


        private void DrawGearPattern()
        {
            const float width = 270f;
            const float height = 220f;
            Rect panel = new Rect((Screen.width - width) * 0.5f, Screen.height - height - 56f, width, height);

            DrawSolidRect(panel, new Color(0.78f, 0.78f, 0.78f, 0.34f));

            float leftX = panel.x + 70f;
            float centerX = panel.x + 135f;
            float rightX = panel.x + 200f;
            float topY = panel.y + 62f;
            float neutralY = panel.y + 113f;
            float bottomY = panel.y + 164f;
            float lineThickness = 6f;

            Color lineColor = new Color(1f, 1f, 1f, 0.96f);
            DrawSolidRect(new Rect(leftX - lineThickness * 0.5f, topY, lineThickness, bottomY - topY), lineColor);
            DrawSolidRect(new Rect(centerX - lineThickness * 0.5f, topY, lineThickness, bottomY - topY), lineColor);
            DrawSolidRect(new Rect(rightX - lineThickness * 0.5f, topY, lineThickness, bottomY - topY), lineColor);
            DrawSolidRect(new Rect(leftX, neutralY - lineThickness * 0.5f, rightX - leftX, lineThickness), lineColor);

            MiniVanGear previewGear = ResolveGearFromDrag(gearDrag);
            Vector2 leverPosition = GetGearLeverPosition(gearDrag, leftX, centerX, rightX, topY, neutralY, bottomY);
            DrawCircle(leverPosition, 28f, new Color(0f, 0f, 0f, 0.34f));
            DrawCircle(leverPosition, 12f, new Color(1f, 1f, 1f, 0.24f));

            GUIStyle labelStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 24,
                fontStyle = FontStyle.Bold
            };
            labelStyle.normal.textColor = Color.white;

            DrawGearLabel("1", new Vector2(leftX, topY - 24f), previewGear == MiniVanGear.First, labelStyle);
            DrawGearLabel("2", new Vector2(leftX, bottomY + 24f), previewGear == MiniVanGear.Second, labelStyle);
            DrawGearLabel("3", new Vector2(centerX, topY - 24f), previewGear == MiniVanGear.Third, labelStyle);
            DrawGearLabel("4", new Vector2(centerX, bottomY + 24f), previewGear == MiniVanGear.Fourth, labelStyle);
            DrawGearLabel("5", new Vector2(rightX, topY - 24f), previewGear == MiniVanGear.Fifth, labelStyle);
            DrawGearLabel("R", new Vector2(rightX, bottomY + 24f), previewGear == MiniVanGear.Reverse, labelStyle);

            GUIStyle neutralStyle = new GUIStyle(labelStyle)
            {
                fontSize = 15
            };
            neutralStyle.normal.textColor = new Color(1f, 1f, 1f, 0.55f);
            GUI.Label(new Rect(centerX + 18f, neutralY - 16f, 32f, 32f), "N", neutralStyle);
        }

        private static void DrawGearLabel(string text, Vector2 center, bool selected, GUIStyle labelStyle)
        {
            Rect labelRect = new Rect(center.x - 20f, center.y - 20f, 40f, 40f);

            if (selected)
            {
                DrawCircle(center, 36f, Color.black);
            }

            GUI.Label(labelRect, text, labelStyle);
        }

        private static Vector2 GetGearLeverPosition(Vector2 drag, float leftX, float centerX, float rightX, float topY, float neutralY, float bottomY)
        {
            const float verticalGateThreshold = 0.35f;
            const float sideLaneThreshold = 0.42f;

            float normalizedX = Mathf.Clamp(drag.x, -1f, 1f);
            float normalizedY = Mathf.Clamp(drag.y, -1f, 1f);

            if (Mathf.Abs(normalizedY) <= verticalGateThreshold)
            {
                float horizontalT = Mathf.InverseLerp(-1f, 1f, normalizedX);
                return new Vector2(Mathf.Lerp(leftX, rightX, horizontalT), neutralY);
            }

            float laneX = centerX;

            if (normalizedX < -sideLaneThreshold)
            {
                laneX = leftX;
            }
            else if (normalizedX > sideLaneThreshold)
            {
                laneX = rightX;
            }

            bool upperGate = normalizedY > 0f;
            float verticalT = upperGate
                ? Mathf.InverseLerp(verticalGateThreshold, 1f, normalizedY)
                : Mathf.InverseLerp(-verticalGateThreshold, -1f, normalizedY);
            float gateY = Mathf.Lerp(neutralY, upperGate ? topY : bottomY, Mathf.Clamp01(verticalT));

            return new Vector2(laneX, gateY);
        }

        private static void DrawSolidRect(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private static void DrawCircle(Vector2 center, float diameter, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(center.x - diameter * 0.5f, center.y - diameter * 0.5f, diameter, diameter), GetGearDotTexture());
            GUI.color = previous;
        }

        private static Texture2D GetGearDotTexture()
        {
            if (gearDotTexture != null)
            {
                return gearDotTexture;
            }

            const int size = 64;
            float radius = size * 0.5f - 1f;
            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            gearDotTexture = new Texture2D(size, size, TextureFormat.ARGB32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear
            };

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                    float alpha = Mathf.Clamp01(radius - distance);
                    gearDotTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            gearDotTexture.Apply();
            return gearDotTexture;
        }
    

private void ApplyMovingPlatformMotion()
        {
            if (movingPlatformVehicle != null)
            {
                Vector3 carriedLocal = movingPlatformVehicle.transform.InverseTransformPoint(transform.position);
                if (ShouldDropCarryThroughOpenRoofHatch(movingPlatformVehicle, carriedLocal))
                {
                    movingPlatformVehicle = null;
                    lastCarriedVehicleSeenTime = -999f;
                    verticalVelocity = Mathf.Min(verticalVelocity, -1f);
                    RestoreVehicleCollisionIgnore();
                    return;
                }
            }

            MiniVanVehicle platform = FindCarriedVehicle();
            if (platform == null && movingPlatformVehicle != null && Time.time - lastCarriedVehicleSeenTime <= VehicleCarryStickyGraceTime && IsNearCarriedVehicleSticky(movingPlatformVehicle))
            {
                platform = movingPlatformVehicle;
            }

            if (platform == null)
            {
                movingPlatformVehicle = null;
                lastPlatformCarryAlignedToTilt = false;

                if (CanRestoreVehicleCollisionIgnore())
                {
                    RestoreVehicleCollisionIgnore();
                }

                return;
            }

            lastCarriedVehicleSeenTime = Time.time;
            UpdateVehicleCollisionIgnore(platform);

            Transform platformTransform = platform.transform;
            Vector3 platformPosition = platformTransform.position;
            bool alignToVehicleTilt = ShouldAlignWalkingToVehicleTilt(platform);
            Quaternion platformRotation = GetVehicleCarryRotation(platform);

            if (movingPlatformVehicle != platform || lastPlatformCarryAlignedToTilt != alignToVehicleTilt)
            {
                movingPlatformVehicle = platform;
                lastPlatformPosition = platformPosition;
                lastPlatformRotation = platformRotation;
                lastPlatformCarryAlignedToTilt = alignToVehicleTilt;
                verticalVelocity = Mathf.Min(verticalVelocity, 0f);
                ClampToCarriedVehicleBounds(platform);
                ResolveIgnoredVehiclePenetrations();
                return;
            }

            Quaternion deltaRotation = platformRotation * Quaternion.Inverse(lastPlatformRotation);
            Vector3 relativePosition = transform.position - lastPlatformPosition;
            Vector3 carriedPosition = platformPosition + deltaRotation * relativePosition;
            Vector3 deltaPosition = carriedPosition - transform.position;
            if (deltaPosition.sqrMagnitude < 0.000001f)
            {
                Rigidbody platformBody = platform.GetComponent<Rigidbody>();
                if (platformBody != null)
                {
                    deltaPosition = platformBody.GetPointVelocity(transform.position) * Time.deltaTime;
                }
            }

            if (Mathf.Abs(deltaPosition.y) < MinorPlatformBounceThreshold)
            {
                deltaPosition.y *= MinorPlatformBounceFilter;
            }
            else
            {
                deltaPosition.y = Mathf.Clamp(deltaPosition.y, -MaxPlatformVerticalStep, MaxPlatformVerticalStep);
            }

            MoveWithCarriedPlatform(deltaPosition);

            // Inherit only yaw from the van; pitch/roll come from the surface underfoot.
            Quaternion yawDelta = GetYawRotation(platformTransform.rotation) *
                                  Quaternion.Inverse(GetYawRotation(lastPlatformRotation));
            transform.rotation = yawDelta * transform.rotation;

            bool inOpenHatchShaft = IsInOpenHatchFreeFall(platform);
            bool standingOnRoof = !inOpenHatchShaft &&
                (IsPlayerAtVehicleRoofHeight(platform, transform.position) || IsStandingOnVehicleRoof(platform));
            bool standingOnDoorStep = !inOpenHatchShaft && IsStandingOnVehicleDoorStep(platform);
            // Keep roof/doorstep jumps intact: never floor-snap or kill upward velocity while airborne on the van.
            if (verticalVelocity <= 0.01f && !inOpenHatchShaft)
            {
                ClampToCarriedVehicleBounds(platform, true);
            }
            else if (inOpenHatchShaft && verticalVelocity <= 0.01f)
            {
                TryLandFromHatchFall(platform);
            }

            ResolveIgnoredVehiclePenetrations();
            if (!standingOnRoof && !standingOnDoorStep && !inOpenHatchShaft)
            {
                verticalVelocity = Mathf.Min(verticalVelocity, 0f);
            }

            lastPlatformPosition = platformPosition;
            lastPlatformRotation = platformRotation;
        }

        private void MoveWithCarriedPlatform(Vector3 deltaPosition)
        {
            if (deltaPosition.sqrMagnitude <= 0.0000001f)
            {
                return;
            }

            if (CharacterController != null && CharacterController.enabled)
            {
                CharacterController.Move(deltaPosition);
            }
            else
            {
                transform.position += deltaPosition;
            }
        }

private void UpdateVehicleCollisionIgnore(MiniVanVehicle vehicle)
        {
            if (CharacterController == null || vehicle == null)
            {
                return;
            }

            keepVehicleCollisionIgnoredUntil = Time.time + VehicleExitCollisionGrace;

            if (ignoredCollisionVehicle == vehicle && ignoredVehicleColliders != null)
            {
                ApplyVehicleCollisionIgnoreState();
                return;
            }

            RestoreVehicleCollisionIgnore();
            ignoredCollisionVehicle = vehicle;
            ignoredVehicleColliders = vehicle.GetComponentsInChildren<Collider>(true);
            ApplyVehicleCollisionIgnoreState();
        }

        private void ApplyVehicleCollisionIgnoreState()
        {
            if (CharacterController == null || ignoredVehicleColliders == null)
            {
                return;
            }

            for (int i = 0; i < ignoredVehicleColliders.Length; i++)
            {
                Collider vehicleCollider = ignoredVehicleColliders[i];
                if (vehicleCollider == null || vehicleCollider.isTrigger || vehicleCollider is WheelCollider)
                {
                    continue;
                }

                Physics.IgnoreCollision(
                    CharacterController,
                    vehicleCollider,
                    ShouldIgnoreVehicleColliderForPassenger(vehicleCollider));
            }
        }

        private bool ShouldIgnoreVehicleColliderForPassenger(Collider vehicleCollider)
        {
            if (vehicleCollider == null || vehicleCollider.isTrigger || vehicleCollider is WheelCollider)
            {
                return false;
            }

            MiniVanLadder ladder = vehicleCollider.GetComponentInParent<MiniVanLadder>();
            if (ladder != null && ladder.OwnsSolidCollider(vehicleCollider))
            {
                // Solid ladder mesh blocks walking; only ghost through it while actively climbing.
                return currentLadder == ladder;
            }

            // Ignore van body/floor/roof so CharacterController cannot shove the Rigidbody.
            // Floor/roof support is handled by raycast clamp instead.
            return true;
        }

        private void RestoreVehicleCollisionIgnore()
        {
            if (CharacterController == null || ignoredVehicleColliders == null)
            {
                ignoredCollisionVehicle = null;
                ignoredVehicleColliders = null;
                return;
            }

            for (int i = 0; i < ignoredVehicleColliders.Length; i++)
            {
                Collider vehicleCollider = ignoredVehicleColliders[i];
                if (vehicleCollider != null && !vehicleCollider.isTrigger && !(vehicleCollider is WheelCollider))
                {
                    Physics.IgnoreCollision(CharacterController, vehicleCollider, false);
                }
            }

            ignoredCollisionVehicle = null;
            ignoredVehicleColliders = null;
        }

private void ResolveIgnoredVehiclePenetrations()
        {
            if (CharacterController == null || !CharacterController.enabled || ignoredVehicleColliders == null)
            {
                return;
            }

            if (currentLadder != null)
            {
                return;
            }

            if (ignoredCollisionVehicle != null)
            {
                Vector3 local = ignoredCollisionVehicle.transform.InverseTransformPoint(transform.position);
                if (IsInsideOpenRoofHatchColumn(ignoredCollisionVehicle, local))
                {
                    return;
                }
            }

            Transform playerTransform = transform;
            for (int iteration = 0; iteration < VehiclePassengerCollisionIterations; iteration++)
            {
                bool moved = false;

                for (int i = 0; i < ignoredVehicleColliders.Length; i++)
                {
                    Collider vehicleCollider = ignoredVehicleColliders[i];
                    if (!ShouldResolveIgnoredVehicleCollider(vehicleCollider))
                    {
                        continue;
                    }

                    if (Physics.ComputePenetration(
                        CharacterController,
                        playerTransform.position,
                        playerTransform.rotation,
                        vehicleCollider,
                        vehicleCollider.transform.position,
                        vehicleCollider.transform.rotation,
                        out Vector3 direction,
                        out float distance) && distance > 0.0001f)
                    {
                        playerTransform.position += direction * (distance + VehiclePassengerSkin);
                        moved = true;
                    }
                }

                if (!moved)
                {
                    return;
                }
            }
        }

private bool ShouldResolveIgnoredVehicleCollider(Collider vehicleCollider)
        {
            if (vehicleCollider == null || !vehicleCollider.enabled || vehicleCollider is WheelCollider)
            {
                return false;
            }

            if (ignoredCollisionVehicle != null && !vehicleCollider.transform.IsChildOf(ignoredCollisionVehicle.transform))
            {
                return false;
            }

            MiniVanPassengerBlocker passengerBlocker = vehicleCollider.GetComponent<MiniVanPassengerBlocker>();
            if (passengerBlocker != null)
            {
                if (!passengerBlocker.BlocksPassengers)
                {
                    return false;
                }

                // The cabin floor lip is a flat slab spanning the whole doorway at deck height.
                // While the player steps up from the door step their capsule crosses it, and the
                // shortest depenetration is sideways — which shoved them straight back outside.
                return !IsBlockerBeingSteppedOver(vehicleCollider);
            }

            if (vehicleCollider.isTrigger || vehicleCollider is MeshCollider || IsVehicleWalkableSupportCollider(vehicleCollider))
            {
                return false;
            }

            return vehicleCollider is BoxCollider || vehicleCollider is CapsuleCollider || vehicleCollider is SphereCollider;
        }


        /// <summary>
        /// True for a flat deck-height blocker the player is currently climbing onto, so the floor
        /// snap can finish the step instead of fighting a sideways push-out.
        /// </summary>
        private bool IsBlockerBeingSteppedOver(Collider vehicleCollider)
        {
            if (CharacterController == null || !IsFlatVehicleSupportCollider(vehicleCollider))
            {
                return false;
            }

            Bounds bounds = vehicleCollider.bounds;
            Vector3 capsuleCenter = transform.TransformPoint(CharacterController.center);
            if (capsuleCenter.y <= bounds.center.y)
            {
                // Below the slab (cabin ceiling): it must keep blocking.
                return false;
            }

            float feetHeight = capsuleCenter.y - CharacterController.height * 0.5f;
            float rise = bounds.max.y - feetHeight;
            return rise > 0f && rise <= VehicleSupportSnapUpRange;
        }

private bool CanRestoreVehicleCollisionIgnore()
        {
            if (ignoredCollisionVehicle == null)
            {
                return true;
            }

            if (Time.time < keepVehicleCollisionIgnoredUntil)
            {
                return false;
            }

            return !IsInsideVehicleCollisionRestoreVolume(ignoredCollisionVehicle);
        }

        private bool IsInsideVehicleCollisionRestoreVolume(MiniVanVehicle vehicle)
        {
            return IsNearVehicleBodyColliders(vehicle, 0.18f) || TryGetVehicleSupportHit(vehicle, out _);
        }


        private void ClampToCarriedVehicleBounds(MiniVanVehicle vehicle, bool continuousFollow = false)
        {
            if (vehicle == null || CharacterController == null || !CharacterController.enabled)
            {
                return;
            }

            Vector3 local = vehicle.transform.InverseTransformPoint(transform.position);
            if (!TryGetVehicleSupportHit(vehicle, out RaycastHit supportHit))
            {
                return;
            }

            // Falling through the hatch uses normal gravity; only snap when already near the floor.
            if (IsInsideOpenRoofHatchColumn(vehicle, local) && local.y > CabinClampMinHeight + 0.55f)
            {
                return;
            }

            Vector3 up = GetSupportUp(supportHit, vehicle);
            float footOffset = CharacterController.height * 0.5f - CharacterController.center.y + Mathf.Max(CharacterController.skinWidth, 0.025f);
            Vector3 desiredRoot = supportHit.point + up * footOffset;
            float alongUp = Vector3.Dot(desiredRoot - transform.position, up);
            if (alongUp > 0f)
            {
                // The van ignores the CharacterController, so this snap is the only thing that can
                // climb the 0.36 m door step into the cabin — the old 0.35 window never allowed it.
                if (alongUp <= VehicleSupportSnapUpRange)
                {
                    float lift = continuousFollow
                        ? Mathf.Min(alongUp, CabinFloorLiftSpeed * Time.deltaTime)
                        : alongUp;
                    transform.position += up * lift;
                }

                return;
            }

            // Cabin walk has no gravity, so a floor that drops away (tilt, step down out of the
            // cabin) has to be followed explicitly or the player floats off the surface.
            // A real fall keeps its own arc — this must not suck the player down mid-jump.
            bool falling = verticalVelocity < -2f;
            if (continuousFollow && !falling && alongUp <= -0.02f && alongUp > -CabinFloorFollowDrop)
            {
                float maxDrop = CabinFloorFollowSpeed * Time.deltaTime;
                transform.position += up * Mathf.Max(alongUp, -maxDrop);
            }
        }

        private bool IsNearCarriedVehicleSticky(MiniVanVehicle vehicle)
        {
            if (vehicle == null)
            {
                return false;
            }

            Vector3 local = vehicle.transform.InverseTransformPoint(transform.position);
            if (IsInsideOpenRoofHatchColumn(vehicle, local) && local.y > CabinClampMinHeight + 0.15f)
            {
                return true;
            }

            if (IsFallingThroughOpenRoofHatch(vehicle, local))
            {
                return false;
            }

            return IsNearVehicleBodyColliders(vehicle, 0.35f) || TryGetVehicleSupportHit(vehicle, out _);
        }
private MiniVanVehicle FindInteriorVehicle()
        {
            return FindCarriedVehicle();
        }

        private MiniVanVehicle FindCarriedVehicle()
        {
            MiniVanVehicle[] vehicles = MiniVanSceneScan.Get<MiniVanVehicle>();
            for (int i = 0; i < vehicles.Length; i++)
            {
                MiniVanVehicle vehicle = vehicles[i];
                if (vehicle == null)
                {
                    continue;
                }

                if (IsInsideVehicleCabin(vehicle) || IsStandingOnVehicleRoof(vehicle))
                {
                    return vehicle;
                }
            }

            return null;
        }

        private bool IsInsideVehicleCabin(MiniVanVehicle vehicle)
        {
            if (vehicle == null)
            {
                return false;
            }

            Vector3 local = vehicle.transform.InverseTransformPoint(transform.position);
            if (IsInsideOpenRoofHatchColumn(vehicle, local) && local.y > CabinClampMinHeight + 0.15f)
            {
                return true;
            }

            if (IsFallingThroughOpenRoofHatch(vehicle, local))
            {
                return false;
            }

            if (TryGetVehicleSupportHit(vehicle, out RaycastHit supportHit))
            {
                if (IsRoofHatchDoorCollider(supportHit.collider))
                {
                    // Open hatch lid is not a cabin floor.
                }
                else
                {
                    Vector3 supportLocal = vehicle.transform.InverseTransformPoint(supportHit.point);
                    return supportLocal.y < RoofCarryMinHeight || IsVehicleCabinSupportCollider(supportHit.collider);
                }
            }

            // Climbing the interior roof ladder keeps cabin carry even when feet briefly leave the floor.
            if (currentLadder != null && currentLadder.GetComponentInParent<MiniVanVehicle>() == vehicle)
            {
                return Mathf.Abs(local.x) <= RoofClampHalfWidth + 0.45f &&
                       local.z >= RoofClampRear - 0.35f &&
                       local.z <= RoofClampFront + 0.35f &&
                       local.y >= CabinClampMinHeight - 0.55f &&
                       local.y <= RoofCarryMaxHeight + 0.35f;
            }

            return movingPlatformVehicle == vehicle && IsNearVehicleBodyColliders(vehicle, 0.25f);
        }

private bool IsStandingOnVehicleRoof(MiniVanVehicle vehicle)
        {
            if (vehicle == null)
            {
                return false;
            }

            Vector3 local = vehicle.transform.InverseTransformPoint(transform.position);
            if (IsOverOpenRoofHatch(vehicle, local) && currentLadder == null)
            {
                // Over the open hole with no ladder — not standing on roof.
                if (!TryGetVehicleSupportHit(vehicle, out RaycastHit holeSupport) ||
                    IsRoofHatchDoorCollider(holeSupport.collider))
                {
                    return false;
                }

                Vector3 holeSupportLocal = vehicle.transform.InverseTransformPoint(holeSupport.point);
                if (holeSupportLocal.y < RoofCarryMinHeight)
                {
                    return false;
                }
            }

            if (TryGetVehicleSupportHit(vehicle, out RaycastHit supportHit))
            {
                if (IsRoofHatchDoorCollider(supportHit.collider))
                {
                    return false;
                }

                Vector3 supportLocal = vehicle.transform.InverseTransformPoint(supportHit.point);
                return supportLocal.y >= RoofCarryMinHeight;
            }

            return movingPlatformVehicle == vehicle && IsNearVehicleBodyColliders(vehicle, 0.25f);
        }

        private static bool IsPlayerAtVehicleRoofHeight(MiniVanVehicle vehicle, Vector3 worldPosition)
        {
            if (vehicle == null)
            {
                return false;
            }

            Vector3 local = vehicle.transform.InverseTransformPoint(worldPosition);
            return Mathf.Abs(local.x) <= RoofClampHalfWidth + RoofCarryStickyExtra &&
                   local.z >= RoofClampRear - RoofCarryStickyExtra &&
                   local.z <= RoofClampFront + RoofCarryStickyExtra &&
                   local.y >= RoofCarryMinHeight - 0.2f &&
                   local.y <= RoofCarryMaxHeight + 0.9f &&
                   !IsOverOpenRoofHatch(vehicle, local);
        }

        private bool TryGetVehicleSupportHit(MiniVanVehicle vehicle, out RaycastHit bestHit)
        {
            bestHit = default;
            if (vehicle == null || CharacterController == null)
            {
                return false;
            }

            Vector3 up = GetPreferredWalkingUp(vehicle);
            Vector3 castOrigin = transform.TransformPoint(CharacterController.center) + up * 0.55f;
            float castDistance = Mathf.Max(1.6f, CharacterController.height + 0.75f);

            // Narrow probe first. The fat sphere below straddles the door step and the cabin floor
            // (0.36 m higher, only 0.15 m apart), so it kept reporting the floor while the player
            // was still standing on the step — flipping gravity mode every frame.
            RaycastHit[] centerHits = Physics.RaycastAll(
                castOrigin,
                -up,
                castDistance,
                ~0,
                QueryTriggerInteraction.Ignore);
            if (TryPickVehicleSupportHit(centerHits, vehicle, up, out bestHit))
            {
                return true;
            }

            float castRadius = Mathf.Max(0.08f, CharacterController.radius * 0.86f);
            RaycastHit[] hits = Physics.SphereCastAll(castOrigin, castRadius, -up, castDistance, ~0, QueryTriggerInteraction.Ignore);
            return TryPickVehicleSupportHit(hits, vehicle, up, out bestHit);
        }

        private bool TryPickVehicleSupportHit(
            RaycastHit[] hits,
            MiniVanVehicle vehicle,
            Vector3 up,
            out RaycastHit bestHit)
        {
            bestHit = default;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit hit = hits[i];
                Collider hitCollider = hit.collider;
                if (hitCollider == null || hitCollider.isTrigger || hitCollider is WheelCollider)
                {
                    continue;
                }

                if (!hitCollider.transform.IsChildOf(vehicle.transform) || hitCollider.transform.IsChildOf(transform))
                {
                    continue;
                }

                if (Vector3.Dot(hit.normal, up) < 0.18f)
                {
                    continue;
                }

                if (IsRoofHatchDoorCollider(hitCollider))
                {
                    continue;
                }

                // Ladder boxes are blockers, not walkable floors.
                if (hitCollider.GetComponentInParent<MiniVanLadder>() != null)
                {
                    continue;
                }

                if (hit.distance < bestDistance)
                {
                    bestDistance = hit.distance;
                    bestHit = hit;
                }
            }

            return bestDistance < float.MaxValue;
        }

        private bool IsNearVehicleBodyColliders(MiniVanVehicle vehicle, float extraRadius)
        {
            if (vehicle == null || CharacterController == null)
            {
                return false;
            }

            Vector3 center = transform.position + CharacterController.center;
            float radius = Mathf.Max(0.08f, CharacterController.radius + extraRadius);
            float halfHeight = Mathf.Max(0f, CharacterController.height * 0.5f - CharacterController.radius);
            Vector3 bottom = center + Vector3.down * halfHeight;
            Vector3 top = center + Vector3.up * halfHeight;
            Collider[] hits = Physics.OverlapCapsule(bottom, top, radius, ~0, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < hits.Length; i++)
            {
                Collider hit = hits[i];
                if (hit == null || hit.isTrigger || hit is WheelCollider)
                {
                    continue;
                }

                if (hit.transform.IsChildOf(vehicle.transform) && !hit.transform.IsChildOf(transform))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsVehicleCabinSupportCollider(Collider vehicleCollider)
        {
            if (vehicleCollider == null)
            {
                return false;
            }

            string path = GetLowerTransformPath(vehicleCollider.transform);
            return path.Contains("floor") || path.Contains("walkable") || path.Contains("driveable");
        }

        private static bool IsVehicleDoorStepCollider(Collider vehicleCollider)
        {
            if (vehicleCollider == null)
            {
                return false;
            }

            string path = GetLowerTransformPath(vehicleCollider.transform);
            return path.Contains("doorstep") ||
                   path.Contains("door_step") ||
                   path.Contains("door step");
        }

        private bool IsStandingOnVehicleDoorStep(MiniVanVehicle vehicle)
        {
            if (vehicle == null || CharacterController == null)
            {
                return false;
            }

            bool hasSupport = TryGetVehicleSupportHit(vehicle, out RaycastHit supportHit);
            if (hasSupport && IsVehicleDoorStepCollider(supportHit.collider))
            {
                doorStepStickyUntil = Time.time + DoorStepStickySeconds;
                return true;
            }

            // Cabin floor underfoot ends step mode at once: keeping gravity on here would fight
            // the floor snap that lifts the player over the step.
            bool standingOnCabinFloor = hasSupport && IsVehicleCabinSupportCollider(supportHit.collider);

            // Seam hysteresis: hold the step mode briefly while still over the step, so one stray
            // probe frame cannot flip gravity on and off mid-stride.
            if (!standingOnCabinFloor && movingPlatformVehicle == vehicle &&
                Time.time < doorStepStickyUntil &&
                IsWithinDoorStepColumn(vehicle, DoorStepStickyFlatRadius))
            {
                return true;
            }

            // Sticky while jumping up from the step so cabin lock doesn't zero verticalVelocity.
            if (movingPlatformVehicle == vehicle && verticalVelocity > 0.05f)
            {
                Vector3 local = vehicle.transform.InverseTransformPoint(transform.position);
                Transform doorStep = vehicle.transform.Find("DoorStep");
                if (doorStep != null)
                {
                    Vector3 stepLocal = vehicle.transform.InverseTransformPoint(doorStep.position);
                    Vector2 flatDelta = new Vector2(local.x - stepLocal.x, local.z - stepLocal.z);
                    if (flatDelta.sqrMagnitude <= 1.6f * 1.6f &&
                        local.y >= stepLocal.y - 0.35f &&
                        local.y <= stepLocal.y + 2.4f)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool IsWithinDoorStepColumn(MiniVanVehicle vehicle, float flatRadius)
        {
            Transform doorStep = vehicle != null ? vehicle.transform.Find("DoorStep") : null;
            if (doorStep == null)
            {
                return false;
            }

            Vector3 local = vehicle.transform.InverseTransformPoint(transform.position);
            Vector3 stepLocal = vehicle.transform.InverseTransformPoint(doorStep.position);
            Vector2 flatDelta = new Vector2(local.x - stepLocal.x, local.z - stepLocal.z);
            return flatDelta.sqrMagnitude <= flatRadius * flatRadius &&
                   local.y >= stepLocal.y - 0.45f &&
                   local.y <= stepLocal.y + 1.9f;
        }

        private static bool IsVehicleWalkableSupportCollider(Collider vehicleCollider)
        {
            if (vehicleCollider == null || IsRoofHatchDoorCollider(vehicleCollider))
            {
                return false;
            }

            if (IsFlatVehicleSupportCollider(vehicleCollider))
            {
                return true;
            }

            string path = GetLowerTransformPath(vehicleCollider.transform);
            return path.Contains("floor") || path.Contains("walkable") || path.Contains("driveable") || path.Contains("roof") || path.Contains("ceiling");
        }

        private static bool IsRoofHatchDoorCollider(Collider vehicleCollider)
        {
            if (vehicleCollider == null)
            {
                return false;
            }

            MiniVanDoor door = vehicleCollider.GetComponentInParent<MiniVanDoor>();
            return door != null && door.IsRoofDoor;
        }

        private static bool IsFlatVehicleSupportCollider(Collider vehicleCollider)
        {
            BoxCollider box = vehicleCollider as BoxCollider;
            if (box == null)
            {
                return false;
            }

            Vector3 scaledSize = Vector3.Scale(box.size, box.transform.lossyScale);
            scaledSize = new Vector3(Mathf.Abs(scaledSize.x), Mathf.Abs(scaledSize.y), Mathf.Abs(scaledSize.z));
            float thinLimit = Mathf.Max(0.5f, Mathf.Min(scaledSize.x, scaledSize.z) * 0.35f);
            return scaledSize.y <= thinLimit && scaledSize.x >= 0.55f && scaledSize.z >= 0.55f;
        }

        private static string GetLowerTransformPath(Transform target)
        {
            if (target == null)
            {
                return string.Empty;
            }

            string path = target.name.ToLowerInvariant();
            Transform current = target.parent;
            while (current != null)
            {
                path = current.name.ToLowerInvariant() + "/" + path;
                current = current.parent;
            }

            return path;
        }

        private static bool IsOverOpenRoofHatch(MiniVanVehicle vehicle, Vector3 local)
        {
            return IsInsideOpenRoofHatchColumn(vehicle, local)
                && local.y >= RoofCarryMinHeight - 0.25f
                && local.y <= RoofCarryMaxHeight + 0.9f;
        }

        private bool ShouldDropCarryThroughOpenRoofHatch(MiniVanVehicle vehicle, Vector3 local)
        {
            if (vehicle == null || currentLadder != null)
            {
                return false;
            }

            // Keep vehicle carry while descending through the open hatch shaft.
            if (IsInsideOpenRoofHatchColumn(vehicle, local))
            {
                return false;
            }

            if (!IsOverOpenRoofHatch(vehicle, local))
            {
                return false;
            }

            // Keep carry while standing on solid roof around the hole; drop only when unsupported over the opening.
            if (TryGetVehicleSupportHit(vehicle, out RaycastHit supportHit) &&
                !IsRoofHatchDoorCollider(supportHit.collider))
            {
                Vector3 supportLocal = vehicle.transform.InverseTransformPoint(supportHit.point);
                if (supportLocal.y >= RoofCarryMinHeight - 0.15f)
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsFallingThroughOpenRoofHatch(MiniVanVehicle vehicle, Vector3 local)
        {
            if (!IsInsideOpenRoofHatchColumn(vehicle, local))
            {
                return false;
            }

            if (local.y <= CabinClampMinHeight + 0.35f || local.y > RoofCarryMaxHeight + 0.9f)
            {
                return false;
            }

            // Interior ladder / controlled climb through the hatch must keep vehicle carry.
            if (currentLadder != null)
            {
                return false;
            }

            if (TryGetVehicleSupportHit(vehicle, out RaycastHit supportHit) &&
                !IsRoofHatchDoorCollider(supportHit.collider))
            {
                Vector3 supportLocal = vehicle.transform.InverseTransformPoint(supportHit.point);
                if (supportLocal.y < RoofCarryMinHeight || IsVehicleCabinSupportCollider(supportHit.collider))
                {
                    return false;
                }

                if (supportLocal.y >= RoofCarryMinHeight)
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsInOpenHatchFreeFall(MiniVanVehicle vehicle)
        {
            if (vehicle == null || currentLadder != null || CharacterController == null)
            {
                return false;
            }

            Vector3 local = vehicle.transform.InverseTransformPoint(transform.position);
            if (!IsInsideOpenRoofHatchColumn(vehicle, local) || local.y <= CabinClampMinHeight + 0.25f)
            {
                return false;
            }

            // Solid roof underfoot means we are still standing at the hatch rim, not falling.
            return !HasImmediateVehicleSupportBelow(vehicle, RoofCarryMinHeight - 0.15f, 0.45f);
        }

        private void TryLandFromHatchFall(MiniVanVehicle vehicle)
        {
            if (vehicle == null || CharacterController == null || !CharacterController.enabled)
            {
                return;
            }

            if (!TryGetVehicleSupportHit(vehicle, out RaycastHit supportHit) || IsRoofHatchDoorCollider(supportHit.collider))
            {
                return;
            }

            Vector3 supportLocal = vehicle.transform.InverseTransformPoint(supportHit.point);
            if (supportLocal.y >= RoofCarryMinHeight - 0.08f && !IsVehicleCabinSupportCollider(supportHit.collider))
            {
                return;
            }

            float desiredRootY = supportHit.point.y + CharacterController.height * 0.5f - CharacterController.center.y + Mathf.Max(CharacterController.skinWidth, 0.025f);
            float deltaY = desiredRootY - transform.position.y;
            // Same landing window as ground / cabin — no soft “glide” snap from far above.
            if (deltaY > -0.08f && deltaY < 0.22f)
            {
                Vector3 position = transform.position;
                position.y = desiredRootY;
                transform.position = position;
                verticalVelocity = -1f;
            }
        }

        private bool HasImmediateVehicleSupportBelow(MiniVanVehicle vehicle, float minSupportLocalY, float probeDistance)
        {
            if (vehicle == null || CharacterController == null)
            {
                return false;
            }

            Vector3 castOrigin = transform.position + CharacterController.center - Vector3.up * (CharacterController.height * 0.5f - 0.05f);
            RaycastHit[] hits = Physics.RaycastAll(castOrigin, Vector3.down, probeDistance, ~0, QueryTriggerInteraction.Ignore);
            float bestDistance = float.MaxValue;
            bool found = false;

            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit hit = hits[i];
                Collider hitCollider = hit.collider;
                if (hitCollider == null || hitCollider.isTrigger || hitCollider is WheelCollider)
                {
                    continue;
                }

                if (!hitCollider.transform.IsChildOf(vehicle.transform) || hitCollider.transform.IsChildOf(transform))
                {
                    continue;
                }

                if (hit.normal.y < 0.18f || IsRoofHatchDoorCollider(hitCollider))
                {
                    continue;
                }

                Vector3 supportLocal = vehicle.transform.InverseTransformPoint(hit.point);
                if (supportLocal.y < minSupportLocalY)
                {
                    continue;
                }

                if (hit.distance < bestDistance)
                {
                    bestDistance = hit.distance;
                    found = true;
                }
            }

            return found;
        }

        private static bool IsInsideOpenRoofHatchColumn(MiniVanVehicle vehicle, Vector3 local)
        {
            if (vehicle == null || !vehicle.RoofDoorOpen.Value)
            {
                return false;
            }

            return Mathf.Abs(local.x - RoofHatchCenterX) <= RoofHatchHalfWidth
                && Mathf.Abs(local.z - RoofHatchCenterZ) <= RoofHatchHalfLength;
        }

        private static Quaternion GetYawRotation(Quaternion rotation)
        {
            Vector3 forward = Vector3.ProjectOnPlane(rotation * Vector3.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f)
            {
                return Quaternion.identity;
            }

            return Quaternion.LookRotation(forward.normalized, Vector3.up);
        }

        private bool ShouldAlignWalkingToVehicleTilt(MiniVanVehicle vehicle)
        {
            if (vehicle == null)
            {
                return false;
            }

            if (IsInOpenHatchFreeFall(vehicle) || IsStandingOnVehicleRoof(vehicle) || IsPlayerAtVehicleRoofHeight(vehicle, transform.position))
            {
                return false;
            }

            return IsInsideVehicleCabin(vehicle) || IsInsideVehicleCabinForZombieTarget(vehicle);
        }

        private Quaternion GetVehicleCarryRotation(MiniVanVehicle vehicle)
        {
            if (vehicle == null)
            {
                return Quaternion.identity;
            }

            // Cabin: track full van pose for position carry. Roof: yaw only.
            // Player pitch/roll is driven separately by surface normals.
            return ShouldAlignWalkingToVehicleTilt(vehicle)
                ? vehicle.transform.rotation
                : GetYawRotation(vehicle.transform.rotation);
        }

        private Vector3 GetPreferredWalkingUp(MiniVanVehicle vehicle)
        {
            if (walkingSurfaceUp.sqrMagnitude > 0.0001f &&
                Vector3.Dot(walkingSurfaceUp.normalized, Vector3.up) >= MinWalkableSurfaceUpDot)
            {
                return walkingSurfaceUp.normalized;
            }

            if (vehicle != null)
            {
                Vector3 vehicleUp = vehicle.transform.up;
                if (vehicleUp.sqrMagnitude > 0.0001f &&
                    Vector3.Dot(vehicleUp.normalized, Vector3.up) >= MinWalkableSurfaceUpDot)
                {
                    return vehicleUp.normalized;
                }
            }

            return Vector3.up;
        }

        private static Vector3 GetSupportUp(RaycastHit supportHit, MiniVanVehicle vehicle)
        {
            Vector3 normal = supportHit.normal;
            if (normal.sqrMagnitude > 0.0001f &&
                Vector3.Dot(normal.normalized, Vector3.up) >= MinWalkableSurfaceUpDot)
            {
                return normal.normalized;
            }

            if (vehicle != null)
            {
                Vector3 vehicleUp = vehicle.transform.up;
                if (vehicleUp.sqrMagnitude > 0.0001f)
                {
                    return vehicleUp.normalized;
                }
            }

            return Vector3.up;
        }

        private Vector3 GetWalkingYawAxis()
        {
            // Root stays world-upright, so yaw is always around gravity — turning on a slope
            // no longer drags the capsule into the surface plane.
            return Vector3.up;
        }

        private void UpdateWalkingSurfaceAlignment()
        {
            if (currentSeat != null || currentSkateboard != null || currentHoverboardM != null || currentLadder != null)
            {
                return;
            }

            // Roof edges return noisy side normals — lock to the van's stable up instead of probing.
            if (movingPlatformVehicle != null && IsOnVehicleRoofForSurfaceAlign(movingPlatformVehicle))
            {
                Vector3 roofUp = movingPlatformVehicle.transform.up;
                if (roofUp.sqrMagnitude < 0.0001f || Vector3.Dot(roofUp.normalized, Vector3.up) < MinWalkableSurfaceUpDot)
                {
                    roofUp = Vector3.up;
                }

                ApplySurfaceUpTarget(roofUp.normalized, WalkingSurfaceRoofAlignSharpness);
                return;
            }

            bool allowProbe = Time.time >= suppressSurfaceNormalProbeUntil;
            Vector3 targetUp = walkingSurfaceUp;
            bool hasSurface = allowProbe && TryGetWalkingSurfaceNormal(out targetUp);
            bool groundedLike = hasSurface || IsGroundedForJump() ||
                (movingPlatformVehicle != null && !IsInOpenHatchFreeFall(movingPlatformVehicle) && verticalVelocity <= 0.01f);

            if (!hasSurface)
            {
                // Cabin without a clean floor hit: prefer stable vehicle up over world up flicker.
                if (movingPlatformVehicle != null && ShouldAlignWalkingToVehicleTilt(movingPlatformVehicle))
                {
                    targetUp = movingPlatformVehicle.transform.up;
                    if (targetUp.sqrMagnitude < 0.0001f ||
                        Vector3.Dot(targetUp.normalized, Vector3.up) < MinWalkableSurfaceUpDot)
                    {
                        targetUp = walkingSurfaceUp;
                    }

                    hasSurface = true;
                }
                else
                {
                    // Keep the previous slope in the air / during probe gaps so leaving the van does not pop upright.
                    targetUp = groundedLike ? Vector3.Slerp(walkingSurfaceUp, Vector3.up, 0.05f) : walkingSurfaceUp;
                }
            }

            float sharpness = hasSurface || groundedLike ? WalkingSurfaceAlignSharpness : WalkingSurfaceAirAlignSharpness;
            ApplySurfaceUpTarget(targetUp.normalized, sharpness);
        }

        private void ApplySurfaceUpTarget(Vector3 targetUp, float sharpness)
        {
            if (targetUp.sqrMagnitude < 0.0001f)
            {
                targetUp = Vector3.up;
            }
            else
            {
                targetUp.Normalize();
            }

            // Deadzone + rate limit so crossing seams / lips does not whip the body.
            float toSmoothed = Vector3.Angle(smoothedTargetSurfaceUp, targetUp);
            if (toSmoothed <= SurfaceNormalDeadzoneDegrees)
            {
                targetUp = smoothedTargetSurfaceUp;
            }
            else
            {
                float maxStep = Mathf.Max(1f, SurfaceNormalMaxDegreesPerSecond) * Time.deltaTime;
                float stepT = Mathf.Clamp01(maxStep / Mathf.Max(toSmoothed, 0.001f));
                smoothedTargetSurfaceUp = Vector3.Slerp(smoothedTargetSurfaceUp, targetUp, stepT).normalized;
                targetUp = smoothedTargetSurfaceUp;
            }

            float blend = 1f - Mathf.Exp(-Mathf.Max(0.1f, sharpness) * Time.deltaTime);
            walkingSurfaceUp = Vector3.Slerp(walkingSurfaceUp, targetUp, blend).normalized;
            if (walkingSurfaceUp.sqrMagnitude < 0.0001f)
            {
                walkingSurfaceUp = Vector3.up;
            }

            // The lean is purely cosmetic: the CharacterController capsule is world-axis-aligned
            // whatever the root rotation is, and walking math below uses a yaw-only basis.
            if (Vector3.Angle(transform.up, walkingSurfaceUp) >= SurfaceAlignApplyEpsilonDegrees)
            {
                transform.rotation = AlignRotationToUp(transform.rotation, walkingSurfaceUp);
            }
        }

        /// <summary>
        /// Yaw-only walking basis. Movement must not inherit the cosmetic surface lean, otherwise
        /// crossing a seam re-aims the whole move vector and the player stutters or catches.
        /// </summary>
        private Quaternion GetWalkingYawRotation()
        {
            Vector3 flatForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (flatForward.sqrMagnitude < 0.0001f)
            {
                flatForward = Vector3.ProjectOnPlane(transform.up, Vector3.up);
            }

            if (flatForward.sqrMagnitude < 0.0001f)
            {
                return Quaternion.identity;
            }

            return Quaternion.LookRotation(flatForward.normalized, Vector3.up);
        }

        /// <summary>
        /// Eye position including step smoothing, so door steps and ladder lips do not punch the
        /// camera upward in a single frame.
        /// </summary>
        public Vector3 GetVisualEyeWorldPosition(Vector3 localOffset)
        {
            return transform.TransformPoint(localOffset) + Vector3.up * cameraStepSmoothOffset;
        }

        private void UpdateCameraStepSmoothing(Vector3 walkStartPosition)
        {
            // Only player-driven height changes are smoothed; platform carry is handled elsewhere.
            float rise = transform.position.y - walkStartPosition.y;
            bool grounded = CharacterController != null && CharacterController.isGrounded;
            bool ballistic = verticalVelocity > 0.05f || verticalVelocity < -3f;

            if ((grounded || movingPlatformVehicle != null) && !ballistic &&
                Mathf.Abs(rise) >= CameraStepSmoothTriggerRise)
            {
                cameraStepSmoothOffset = Mathf.Clamp(
                    cameraStepSmoothOffset - rise,
                    -CameraStepSmoothMaxOffset,
                    CameraStepSmoothMaxOffset);
            }
        }

        /// <summary>
        /// Runs every owner frame, including ladder / seat modes, so a pending step offset can
        /// never freeze the eye away from the head.
        /// </summary>
        private void DecayCameraStepSmoothing()
        {
            if (Mathf.Approximately(cameraStepSmoothOffset, 0f))
            {
                return;
            }

            float recoverSpeed = Mathf.Max(
                CameraStepSmoothMinRecoverSpeed,
                CameraStepSmoothRecoverSharpness * Mathf.Abs(cameraStepSmoothOffset));
            cameraStepSmoothOffset = Mathf.MoveTowards(
                cameraStepSmoothOffset,
                0f,
                recoverSpeed * Time.deltaTime);
        }

        private void UpdateClimbingBodyUpright()
        {
            MiniVanVehicle vehicle = currentLadder != null
                ? currentLadder.GetComponentInParent<MiniVanVehicle>()
                : movingPlatformVehicle;
            Vector3 climbUp = vehicle != null ? vehicle.transform.up : Vector3.up;
            if (climbUp.sqrMagnitude < 0.0001f || Vector3.Dot(climbUp.normalized, Vector3.up) < 0.45f)
            {
                climbUp = Vector3.up;
            }

            // While climbing, ignore floor/edge normals entirely and ease toward a stable van up.
            suppressSurfaceNormalProbeUntil = Mathf.Max(suppressSurfaceNormalProbeUntil, Time.time + 0.05f);
            ApplySurfaceUpTarget(climbUp.normalized, WalkingSurfaceClimbAlignSharpness);
        }

        private void ApplyClimbingCameraPitch()
        {
            if (CameraRoot == null || IsPizzaChestOpen() || IsFacePainting() || hotPotatoPoopActive)
            {
                return;
            }

            CameraRoot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        private bool IsOnVehicleRoofForSurfaceAlign(MiniVanVehicle vehicle)
        {
            if (vehicle == null)
            {
                return false;
            }

            return IsStandingOnVehicleRoof(vehicle) ||
                   IsPlayerAtVehicleRoofHeight(vehicle, transform.position) ||
                   IsInOpenHatchFreeFall(vehicle);
        }

        /// <summary>
        /// Keep the first-person camera horizon level with gravity while the body tilts to the surface.
        /// Same approach as most FPS/third-person games: feet follow the slope, eyes don't roll.
        /// </summary>
        private void ApplyWalkingCameraLevelHorizon()
        {
            if (CameraRoot == null || currentSeat != null || currentSkateboard != null || currentHoverboardM != null || currentLadder != null)
            {
                return;
            }

            if (IsPizzaChestOpen() || IsFacePainting() || hotPotatoPoopActive)
            {
                return;
            }

            Vector3 worldUp = Vector3.up;
            Vector3 flatForward = Vector3.ProjectOnPlane(transform.forward, worldUp);
            if (flatForward.sqrMagnitude < 0.0001f)
            {
                flatForward = Vector3.ProjectOnPlane(-transform.up, worldUp);
            }

            if (flatForward.sqrMagnitude < 0.0001f)
            {
                CameraRoot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
                return;
            }

            Quaternion levelYaw = Quaternion.LookRotation(flatForward.normalized, worldUp);
            CameraRoot.rotation = levelYaw * Quaternion.Euler(pitch, 0f, 0f);
        }

        private bool TryGetWalkingSurfaceNormal(out Vector3 normal)
        {
            normal = Vector3.up;
            if (CharacterController == null)
            {
                return false;
            }

            Vector3 preferredUp = GetPreferredWalkingUp(movingPlatformVehicle);

            // Inside cabin: only accept floor normals that agree with the van up — ignores seats/rims.
            if (movingPlatformVehicle != null &&
                ShouldAlignWalkingToVehicleTilt(movingPlatformVehicle) &&
                TryGetVehicleSupportHit(movingPlatformVehicle, out RaycastHit vehicleHit))
            {
                Vector3 vehicleNormal = vehicleHit.normal;
                if (vehicleNormal.sqrMagnitude > 0.0001f &&
                    Vector3.Dot(vehicleNormal.normalized, preferredUp) >= MinWalkableSurfaceUpDot &&
                    Vector3.Dot(vehicleNormal.normalized, Vector3.up) >= MinWalkableSurfaceUpDot * 0.85f)
                {
                    normal = vehicleNormal.normalized;
                    return true;
                }
            }

            Vector3 probeUp = preferredUp;
            Vector3 castOrigin = transform.TransformPoint(CharacterController.center) + probeUp * 0.35f;
            float castRadius = Mathf.Max(0.06f, CharacterController.radius * 0.55f);
            float castDistance = Mathf.Max(1.1f, CharacterController.height * 0.55f + 0.35f);
            RaycastHit[] hits = Physics.SphereCastAll(castOrigin, castRadius, -probeUp, castDistance, ~0, QueryTriggerInteraction.Ignore);
            float bestScore = float.NegativeInfinity;
            Vector3 bestNormal = Vector3.up;
            bool found = false;

            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit hit = hits[i];
                Collider hitCollider = hit.collider;
                if (hitCollider == null || hitCollider.isTrigger || hitCollider is WheelCollider)
                {
                    continue;
                }

                if (hitCollider.transform.IsChildOf(transform))
                {
                    continue;
                }

                if (hitCollider.GetComponentInParent<MiniVanLadder>() != null)
                {
                    continue;
                }

                // Roof trim / hatch lids create edge wobble while walking on the van.
                if (movingPlatformVehicle != null &&
                    hitCollider.transform.IsChildOf(movingPlatformVehicle.transform) &&
                    IsOnVehicleRoofForSurfaceAlign(movingPlatformVehicle))
                {
                    continue;
                }

                Vector3 hitNormal = hit.normal;
                if (hitNormal.sqrMagnitude < 0.0001f)
                {
                    continue;
                }

                hitNormal.Normalize();
                float upAlign = Vector3.Dot(hitNormal, Vector3.up);
                float preferredAlign = Vector3.Dot(hitNormal, probeUp);
                if (upAlign < MinWalkableSurfaceUpDot || preferredAlign < MinWalkableSurfaceUpDot * 0.9f)
                {
                    continue;
                }

                // Prefer flat floors aligned with our current up over the nearest vertical lip.
                float score = preferredAlign * 4f + upAlign * 2f - hit.distance;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestNormal = hitNormal;
                    found = true;
                }
            }

            if (!found)
            {
                return false;
            }

            normal = bestNormal;
            return true;
        }

        private static Quaternion AlignRotationToUp(Quaternion rotation, Vector3 up)
        {
            if (up.sqrMagnitude < 0.0001f)
            {
                return rotation;
            }

            up.Normalize();
            Vector3 forward = Vector3.ProjectOnPlane(rotation * Vector3.forward, up);
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.ProjectOnPlane(rotation * Vector3.right, up);
            }

            if (forward.sqrMagnitude < 0.0001f)
            {
                return Quaternion.FromToRotation(rotation * Vector3.up, up) * rotation;
            }

            return Quaternion.LookRotation(forward.normalized, up);
        }

private void DrawPingStats()
        {
            float ping = NetworkManager.Singleton != null && (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer) ? 0f : smoothedPingMs;
            Color color;
            string state;

            if (ping >= BadPingMs)
            {
                color = new Color(1f, 0.18f, 0.12f, 1f);
                state = "HIGH";
            }
            else if (ping >= GoodPingMs)
            {
                color = new Color(1f, 0.82f, 0.2f, 1f);
                state = "MID";
            }
            else
            {
                color = new Color(0.25f, 1f, 0.34f, 1f);
                state = "OK";
            }

            GUIStyle style = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleRight,
                fontSize = 16,
                fontStyle = FontStyle.Bold
            };
            style.normal.textColor = color;

            Rect rect = new Rect(Screen.width - 178f, 18f, 160f, 34f);
            GUI.Box(rect, "Ping: " + Mathf.RoundToInt(ping) + " ms  " + state, style);
        }


private void UpdatePingProbe()
        {
            if (!IsOwner || NetworkManager.Singleton == null || Time.unscaledTime < nextPingTime)
            {
                return;
            }

            nextPingTime = Time.unscaledTime + PingInterval;

            if (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer)
            {
                measuredPingMs = 0f;
                smoothedPingMs = 0f;
                return;
            }

            lastPingSentTime = Time.unscaledTime;
            SubmitPingServerRpc(lastPingSentTime);
        }

        [ServerRpc]
        private void SubmitPingServerRpc(float clientSendTime, ServerRpcParams rpcParams = default)
        {
            ClientRpcParams target = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new[] { rpcParams.Receive.SenderClientId }
                }
            };

            ReturnPingClientRpc(clientSendTime, target);
        }

        [ClientRpc]
        private void ReturnPingClientRpc(float clientSendTime, ClientRpcParams clientRpcParams = default)
        {
            if (!IsOwner)
            {
                return;
            }

            measuredPingMs = Mathf.Max(0f, (Time.unscaledTime - clientSendTime) * 1000f);
            smoothedPingMs = smoothedPingMs <= 0.01f ? measuredPingMs : Mathf.Lerp(smoothedPingMs, measuredPingMs, 0.25f);
        }


private CoffeeMugPickup FindLookedAtCoffee()
        {
            if (PlayerCamera == null || hasCoffee)
            {
                return null;
            }

            Ray ray = new Ray(PlayerCamera.transform.position, PlayerCamera.transform.forward);
            RaycastHit[] hits = Physics.RaycastAll(ray, InteractDistance, ~0, QueryTriggerInteraction.Collide);
            System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

            for (int i = 0; i < hits.Length; i++)
            {
                CoffeeMugPickup mug = hits[i].collider.GetComponentInParent<CoffeeMugPickup>();
                if (mug != null && mug.IsAvailable && IsCoffeeInReach(mug))
                {
                    return mug;
                }
            }

            CoffeeMugPickup[] mugs = MiniVanSceneScan.Get<CoffeeMugPickup>();
            CoffeeMugPickup bestMug = null;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < mugs.Length; i++)
            {
                if (mugs[i] == null || !mugs[i].IsAvailable)
                {
                    continue;
                }

                float distance = Vector3.Distance(transform.position, mugs[i].transform.position);
                if (distance < bestDistance && distance <= Mathf.Max(CoffeePickupRadius, mugs[i].PickupRadius))
                {
                    bestMug = mugs[i];
                    bestDistance = distance;
                }
            }

            return bestMug;
        }

        private MiniVanBatPickup FindLookedAtBat()
        {
            if (PlayerCamera == null)
            {
                return null;
            }

            Ray ray = new Ray(PlayerCamera.transform.position, PlayerCamera.transform.forward);
            RaycastHit[] hits = Physics.RaycastAll(ray, InteractDistance, ~0, QueryTriggerInteraction.Collide);
            System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

            for (int i = 0; i < hits.Length; i++)
            {
                MiniVanBatPickup bat = hits[i].collider.GetComponentInParent<MiniVanBatPickup>();
                if (CanPickUpBatType(bat) && bat.IsAvailable && bat.IsInReach(transform.position))
                {
                    return bat;
                }
            }

            MiniVanBatPickup[] bats = MiniVanSceneScan.Get<MiniVanBatPickup>();
            MiniVanBatPickup bestBat = null;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < bats.Length; i++)
            {
                if (!CanPickUpBatType(bats[i]) || !bats[i].IsAvailable)
                {
                    continue;
                }

                float distance = Vector3.Distance(transform.position, bats[i].transform.position);
                if (distance < bestDistance && distance <= bats[i].PickupRadius)
                {
                    bestBat = bats[i];
                    bestDistance = distance;
                }
            }

            return bestBat;
        }

        private bool CanPickUpBatType(MiniVanBatPickup bat)
        {
            if (bat == null)
            {
                return false;
            }

            MiniVanInventoryItem item = bat.IsTestBaton
                ? MiniVanInventoryItem.TestBaton
                : MiniVanInventoryItem.Bat;
            return !HasInventoryItem(item);
        }

        private bool IsCoffeeInReach(CoffeeMugPickup mug)
        {
            if (mug == null)
            {
                return false;
            }

            float allowedDistance = Mathf.Max(CoffeePickupRadius, mug.PickupRadius);
            return Vector3.Distance(transform.position, mug.transform.position) <= allowedDistance;
        }

        [ServerRpc]
        private void RequestCoffeePickupServerRpc(NetworkObjectReference mugReference, ServerRpcParams rpcParams = default)
        {
            if (hasCoffee || !mugReference.TryGet(out NetworkObject mugObject))
            {
                return;
            }

            int emptySlot = FindFirstEmptyInventorySlot();
            if (emptySlot < 0)
            {
                return;
            }

            CoffeeMugPickup mug = mugObject.GetComponent<CoffeeMugPickup>();
            if (mug == null || !mug.IsAvailable)
            {
                return;
            }

            float allowedDistance = Mathf.Max(CoffeePickupRadius, mug.PickupRadius) + 0.75f;
            if (Vector3.Distance(transform.position, mug.transform.position) > allowedDistance)
            {
                return;
            }

            if (!mug.TryClaim())
            {
                return;
            }

            claimedCoffeeReference = new NetworkObjectReference(mug.NetworkObject);
            hasCoffee = true;
            SetInventorySlot(emptySlot, MiniVanInventoryItem.Coffee);
            networkSelectedSlot.Value = emptySlot;
            SetCoffeeHeldClientRpc(true, emptySlot);
        }

        [ServerRpc]
        private void RequestBatPickupServerRpc(NetworkObjectReference batReference, ServerRpcParams rpcParams = default)
        {
            if (!batReference.TryGet(out NetworkObject batObject))
            {
                return;
            }

            MiniVanBatPickup bat = batObject.GetComponent<MiniVanBatPickup>();
            if (!CanPickUpBatType(bat) || !bat.IsAvailable || !bat.IsInReach(transform.position))
            {
                return;
            }

            int emptySlot = FindFirstEmptyInventorySlot();
            if (emptySlot < 0 || !bat.TryClaim())
            {
                return;
            }

            claimedBatReference = new NetworkObjectReference(bat.NetworkObject);
            claimedBatIsTestBaton = bat.IsTestBaton;
            MiniVanInventoryItem batItem = bat.IsTestBaton ? MiniVanInventoryItem.TestBaton : MiniVanInventoryItem.Bat;
            SetInventorySlot(emptySlot, batItem);
            networkSelectedSlot.Value = emptySlot;
            SetLocalInventorySlotClientRpc(emptySlot, (int)batItem, BuildOwnerTarget());
        }

        public bool TryPickupWinch(MiniVanWinchPickup pickup)
        {
            if (pickup == null || HasInventoryItem(MiniVanInventoryItem.Winch) || !pickup.IsAvailable)
            {
                return false;
            }

            if (pickup.NetworkObject == null || !pickup.NetworkObject.IsSpawned)
            {
                return false;
            }

            winchUseBlockedUntilFrame = Time.frameCount + 4;
            RequestWinchPickupServerRpc(new NetworkObjectReference(pickup.NetworkObject));
            return true;
        }

        private bool HandleWinchDropInput()
        {
            if (currentSeat != null)
            {
                return false;
            }

            if (MiniVanWinchCable.TryDropHeldCable(this))
            {
                return true;
            }

            if (!IsSelectedInventoryItem(MiniVanInventoryItem.Winch))
            {
                return false;
            }

            Vector3 dropPosition = GetLooseItemDropPosition();
            Quaternion dropRotation = GetLooseItemDropRotation();
            if (!IsServer)
            {
                PredictClearInventoryItem(MiniVanInventoryItem.Winch);
            }

            RequestDropSelectedWinchServerRpc(dropPosition, dropRotation);
            return true;
        }

        [ServerRpc]
        private void RequestWinchPickupServerRpc(NetworkObjectReference winchReference, ServerRpcParams rpcParams = default)
        {
            if (HasInventoryItem(MiniVanInventoryItem.Winch) || !winchReference.TryGet(out NetworkObject winchObject))
            {
                return;
            }

            MiniVanWinchPickup pickup = winchObject.GetComponent<MiniVanWinchPickup>();
            if (pickup == null || !pickup.IsAvailable || !pickup.IsInReach(transform.position))
            {
                return;
            }

            int emptySlot = FindFirstEmptyInventorySlot();
            if (emptySlot < 0 || !pickup.TryClaim())
            {
                return;
            }

            SetInventorySlot(emptySlot, MiniVanInventoryItem.Winch);
            networkWinchDurability.Value = Mathf.Clamp01(pickup.Durability01);
            networkSelectedSlot.Value = emptySlot;
            SetLocalInventorySlotClientRpc(emptySlot, (int)MiniVanInventoryItem.Winch, BuildOwnerTarget());
        }

        [ServerRpc]
        private void RequestDropSelectedWinchServerRpc(Vector3 dropPosition, Quaternion dropRotation, ServerRpcParams rpcParams = default)
        {
            if (currentSeat != null)
            {
                return;
            }

            int slot = FindInventorySlot(MiniVanInventoryItem.Winch);
            if (slot < 0 || !ServerSpawnWinchPickup(dropPosition, dropRotation, networkWinchDurability.Value))
            {
                return;
            }

            SetInventorySlot(slot, MiniVanInventoryItem.None);
            SetLocalInventorySlotClientRpc(slot, (int)MiniVanInventoryItem.None, BuildOwnerTarget());
        }

        public void SpawnFoldedWinchPickupFromWorld(float durability01, Vector3 worldPosition)
        {
            Quaternion rotation = GetLooseItemDropRotation();
            if (IsServer)
            {
                ServerSpawnWinchPickup(worldPosition, rotation, durability01);
            }
            else
            {
                RequestSpawnFoldedWinchPickupServerRpc(worldPosition, rotation, durability01);
            }
        }

        [ServerRpc]
        private void RequestSpawnFoldedWinchPickupServerRpc(Vector3 worldPosition, Quaternion rotation, float durability01, ServerRpcParams rpcParams = default)
        {
            ServerSpawnWinchPickup(worldPosition, rotation, durability01);
        }

        private bool ServerSpawnWinchPickup(Vector3 worldPosition, Quaternion rotation, float durability01)
        {
            if (!IsServer || WinchPickupPrefab == null)
            {
                return false;
            }

            GameObject instance = Instantiate(WinchPickupPrefab, worldPosition, rotation);
            MiniVanWinchPickup pickup = instance.GetComponent<MiniVanWinchPickup>();
            if (pickup != null)
            {
                pickup.Durability01 = Mathf.Clamp01(durability01);
            }

            NetworkObject networkObject = instance.GetComponent<NetworkObject>();
            if (networkObject != null && !networkObject.IsSpawned)
            {
                networkObject.Spawn(true);
            }

            return true;
        }

        private Vector3 GetLooseItemDropPosition()
        {
            Vector3 forward = CameraRoot != null ? CameraRoot.forward : transform.forward;
            Vector3 flatForward = Vector3.ProjectOnPlane(forward, Vector3.up);
            if (flatForward.sqrMagnitude < 0.001f)
            {
                flatForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            }

            flatForward = flatForward.sqrMagnitude > 0.001f ? flatForward.normalized : Vector3.forward;
            Vector3 dropPosition = transform.position + flatForward * 0.95f + Vector3.up * 0.35f;
            Vector3 rayOrigin = transform.position + flatForward * 0.95f + Vector3.up * 1.35f;
            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 3f, ~0, QueryTriggerInteraction.Ignore))
            {
                dropPosition = hit.point + Vector3.up * 0.16f;
            }

            return dropPosition;
        }

        private Quaternion GetLooseItemDropRotation()
        {
            Vector3 forward = CameraRoot != null ? CameraRoot.forward : transform.forward;
            Vector3 flatForward = Vector3.ProjectOnPlane(forward, Vector3.up);
            if (flatForward.sqrMagnitude < 0.001f)
            {
                flatForward = Vector3.forward;
            }

            return Quaternion.LookRotation(flatForward.normalized, Vector3.up);
        }

        public void SetWinchDurabilityFromWorld(float durability01)
        {
            float clamped = Mathf.Clamp01(durability01);
            if (IsServer)
            {
                networkWinchDurability.Value = clamped;
            }
            else
            {
                RequestSetWinchDurabilityServerRpc(clamped);
            }
        }

        [ServerRpc]
        private void RequestSetWinchDurabilityServerRpc(float durability01, ServerRpcParams rpcParams = default)
        {
            networkWinchDurability.Value = Mathf.Clamp01(durability01);
        }

        public void HideWinchWhileDeployed()
        {
            if (IsServer)
            {
                ServerHideWinchWhileDeployed();
            }
            else
            {
                RequestHideWinchWhileDeployedServerRpc();
            }
        }

        [ServerRpc]
        private void RequestHideWinchWhileDeployedServerRpc(ServerRpcParams rpcParams = default)
        {
            ServerHideWinchWhileDeployed();
        }

        private void ServerHideWinchWhileDeployed()
        {
            if (!IsServer)
            {
                return;
            }

            int slot = FindInventorySlot(MiniVanInventoryItem.Winch);
            if (slot < 0)
            {
                return;
            }

            SetInventorySlot(slot, MiniVanInventoryItem.None);
            SetLocalInventorySlotClientRpc(slot, (int)MiniVanInventoryItem.None, BuildOwnerTarget());
        }

        public void TryRestoreWinchFromWorld(float durability01)
        {
            float clamped = Mathf.Clamp01(durability01);
            if (IsServer)
            {
                ServerRestoreWinch(clamped);
            }
            else
            {
                RequestRestoreWinchServerRpc(clamped);
            }
        }

        [ServerRpc]
        private void RequestRestoreWinchServerRpc(float durability01, ServerRpcParams rpcParams = default)
        {
            ServerRestoreWinch(durability01);
        }

        private void ServerRestoreWinch(float durability01)
        {
            if (!IsServer)
            {
                return;
            }

            int slot = FindInventorySlot(MiniVanInventoryItem.Winch);
            if (slot < 0)
            {
                slot = GetInventorySlot(networkSelectedSlot.Value) == MiniVanInventoryItem.None
                    ? networkSelectedSlot.Value
                    : FindFirstEmptyInventorySlot();
            }

            if (slot < 0)
            {
                return;
            }

            SetInventorySlot(slot, MiniVanInventoryItem.Winch);
            networkWinchDurability.Value = Mathf.Clamp01(durability01);
            networkSelectedSlot.Value = slot;
            SetLocalInventorySlotClientRpc(slot, (int)MiniVanInventoryItem.Winch, BuildOwnerTarget());
        }

        public void NotifyWinchBroken()
        {
            if (IsServer)
            {
                ServerRemoveWinch();
            }
            else
            {
                RequestRemoveBrokenWinchServerRpc();
            }
        }

        [ServerRpc]
        private void RequestRemoveBrokenWinchServerRpc(ServerRpcParams rpcParams = default)
        {
            ServerRemoveWinch();
        }

        private void ServerRemoveWinch()
        {
            if (!IsServer)
            {
                return;
            }

            int slot = FindInventorySlot(MiniVanInventoryItem.Winch);
            if (slot >= 0)
            {
                SetInventorySlot(slot, MiniVanInventoryItem.None);
            }

            networkWinchDurability.Value = 0f;
        }

        [ServerRpc]
        private void RequestHotPotatoPickupServerRpc(NetworkObjectReference bombReference, ServerRpcParams rpcParams = default)
        {
            if (currentSeat != null || heldHotPotatoBomb != null || HasInventoryItem(MiniVanInventoryItem.HotPotatoBomb) || !bombReference.TryGet(out NetworkObject bombObject))
            {
                return;
            }

            MiniVanHotPotatoBomb bomb = bombObject.GetComponent<MiniVanHotPotatoBomb>();
            if (bomb == null || !bomb.IsAvailable || !IsHotPotatoBombInReach(bomb))
            {
                return;
            }

            int emptySlot = FindFirstEmptyInventorySlot();
            if (emptySlot < 0 || !bomb.ServerPickupByPlayer(this))
            {
                return;
            }

            SetInventorySlot(emptySlot, MiniVanInventoryItem.HotPotatoBomb);
            networkSelectedSlot.Value = emptySlot;
            heldHotPotatoBomb = bomb;
            heldHotPotatoBombSlot = emptySlot;
            SetHotPotatoHeldClientRpc(new NetworkObjectReference(bomb.NetworkObject), true, emptySlot);
        }

        [ServerRpc]
        private void RequestHotPotatoThrowServerRpc(NetworkObjectReference bombReference, Vector3 direction, ServerRpcParams rpcParams = default)
        {
            if (currentSeat != null || !bombReference.TryGet(out NetworkObject bombObject))
            {
                return;
            }

            MiniVanHotPotatoBomb bomb = bombObject.GetComponent<MiniVanHotPotatoBomb>();
            if (bomb == null || heldHotPotatoBomb != bomb || FindInventorySlot(MiniVanInventoryItem.HotPotatoBomb) < 0)
            {
                return;
            }

            bomb.ServerThrowFromPlayer(this, direction);
        }

        [ServerRpc]
        private void RequestHotPotatoDropServerRpc(NetworkObjectReference bombReference, Vector3 direction, ServerRpcParams rpcParams = default)
        {
            if (currentSeat != null || !bombReference.TryGet(out NetworkObject bombObject))
            {
                return;
            }

            MiniVanHotPotatoBomb bomb = bombObject.GetComponent<MiniVanHotPotatoBomb>();
            if (bomb == null || bomb.IsActivated || heldHotPotatoBomb != bomb || FindInventorySlot(MiniVanInventoryItem.HotPotatoBomb) < 0)
            {
                return;
            }

            Vector3 flatForward = Vector3.ProjectOnPlane(direction.sqrMagnitude > 0.001f ? direction.normalized : transform.forward, Vector3.up);
            if (flatForward.sqrMagnitude < 0.001f)
            {
                flatForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            }

            flatForward = flatForward.sqrMagnitude > 0.001f ? flatForward.normalized : Vector3.forward;
            Vector3 dropPosition = transform.position + flatForward * 0.95f + Vector3.up * 0.45f;
            Vector3 rayOrigin = transform.position + flatForward * 0.95f + Vector3.up * 1.25f;
            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 3f, ~0, QueryTriggerInteraction.Ignore))
            {
                dropPosition = hit.point + Vector3.up * 0.26f;
            }

            Quaternion dropRotation = Quaternion.LookRotation(flatForward, Vector3.up);
            bomb.ServerDropInactiveFromPlayer(this, dropPosition, dropRotation);
        }

        [ServerRpc]
        private void RequestSelectInventorySlotServerRpc(int slotIndex, ServerRpcParams rpcParams = default)
        {
            networkSelectedSlot.Value = Mathf.Clamp(slotIndex, 0, 3);
        }

        [ServerRpc]
        private void RequestBatAttackServerRpc(int slotIndex, ServerRpcParams rpcParams = default)
        {
            if (Time.time < nextServerBatSwingTime || !IsBatWeapon(GetInventorySlot(slotIndex)))
            {
                return;
            }

            nextServerBatSwingTime = Time.time + GetBatAttackInterval();
            PlayBatSwingClientRpc();
            StartCoroutine(ResolveBatHitboxCoroutine(slotIndex, GetBatAttackInterval()));
        }

        private IEnumerator ResolveBatHitboxCoroutine(int slotIndex, float attackInterval)
        {
            float startDelay = Mathf.Clamp01(BatHitboxStart) * attackInterval;
            if (startDelay > 0f)
            {
                yield return new WaitForSeconds(startDelay);
            }

            float endTime = Time.time + Mathf.Max(0.02f, BatHitboxDuration);
            while (Time.time <= endTime)
            {
                MiniVanInventoryItem weapon = GetInventorySlot(slotIndex);
                if (IsBatWeapon(weapon) && TryResolveBatHit(weapon == MiniVanInventoryItem.TestBaton))
                {
                    yield break;
                }

                yield return null;
            }
        }

private bool TryResolveBatHit(bool allowPlayerDamage)
        {
            Vector3 origin = transform.position + Vector3.up * 1.15f;
            Vector3 direction =
                Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            if (direction.sqrMagnitude < 0.001f)
            {
                direction = transform.forward;
            }

            MiniVanZombie bestZombie = null;
            float bestDistance = float.MaxValue;
            float attackRadius = Mathf.Max(0.05f, BatAttackRadius);
            float attackRange = Mathf.Max(0.1f, BatAttackRange);
            if (TryBreakNearbyPanelkaWindow(
                    origin,
                    direction,
                    attackRadius + 0.48f))
            {
                return true;
            }
            RaycastHit[] swingHits = Physics.SphereCastAll(
                origin,
                attackRadius,
                direction,
                attackRange,
                ~0,
                QueryTriggerInteraction.Ignore);

            if (allowPlayerDamage && TryResolveTestBatonPlayerHit(origin, direction, attackRadius, attackRange))
            {
                return true;
            }

            for (int i = 0; i < swingHits.Length; i++)
            {
                Collider hitCollider = swingHits[i].collider;
                if (hitCollider != null && TryBreakPanelkaWindow(hitCollider))
                {
                    return true;
                }
            }

            MiniVanDestructibleCrate bestCrate = null;
            float bestCrateDistance = float.MaxValue;
            for (int i = 0; i < swingHits.Length; i++)
            {
                Collider hitCollider = swingHits[i].collider;
                MiniVanDestructibleCrate crate = hitCollider != null
                    ? hitCollider.GetComponentInParent<MiniVanDestructibleCrate>()
                    : null;
                if (crate != null && crate.CurrentHealth > 0 && swingHits[i].distance < bestCrateDistance)
                {
                    bestCrate = crate;
                    bestCrateDistance = swingHits[i].distance;
                }
            }

            if (bestCrate != null)
            {
                if (bestCrate.ServerApplyHit() && IsSpawned)
                {
                    GameModeCrateStateClientRpc(bestCrate.CrateId, bestCrate.CurrentHealth, bestCrate.CoinValue, bestCrate.IsCollected);
                }
                return true;
            }

            MiniVanBreakableWoodCrate bestWoodCrate = null;
            float bestWoodCrateDistance = float.MaxValue;
            Vector3 bestWoodHitPoint = origin + direction * Mathf.Min(1f, attackRange);
            for (int i = 0; i < swingHits.Length; i++)
            {
                Collider hitCollider = swingHits[i].collider;
                MiniVanBreakableWoodCrate woodCrate = hitCollider != null
                    ? hitCollider.GetComponentInParent<MiniVanBreakableWoodCrate>()
                    : null;
                if (woodCrate != null && !woodCrate.IsBroken && swingHits[i].distance < bestWoodCrateDistance)
                {
                    bestWoodCrate = woodCrate;
                    bestWoodCrateDistance = swingHits[i].distance;
                    bestWoodHitPoint = swingHits[i].point;
                }
            }

            if (bestWoodCrate == null)
            {
                bestWoodCrate = FindNearbyWoodCrateForBatHit(
                    origin,
                    direction,
                    attackRadius,
                    attackRange,
                    out bestWoodHitPoint);
            }

            if (bestWoodCrate != null)
            {
                bestWoodCrate.ServerApplyBatHit(
                    Mathf.Max(1, BatDamage),
                    bestWoodHitPoint,
                    direction);
                return true;
            }

            MiniVanElectrifiedGenerator bestGenerator = null;
            float bestGeneratorDistance = float.MaxValue;
            for (int i = 0; i < swingHits.Length; i++)
            {
                Collider hitCollider = swingHits[i].collider;
                MiniVanElectrifiedGenerator generator = hitCollider != null
                    ? hitCollider.GetComponentInParent<MiniVanElectrifiedGenerator>()
                    : null;
                if (generator != null && !generator.IsBroken && swingHits[i].distance < bestGeneratorDistance)
                {
                    bestGenerator = generator;
                    bestGeneratorDistance = swingHits[i].distance;
                }
            }

            if (bestGenerator != null)
            {
                bestGenerator.ServerApplyBatHit(Mathf.Max(1, BatDamage));
                return true;
            }

            bestGenerator = FindNearbyGeneratorForBatHit(origin, direction, attackRadius, attackRange);
            if (bestGenerator != null)
            {
                bestGenerator.ServerApplyBatHit(Mathf.Max(1, BatDamage));
                return true;
            }

            for (int i = 0; i < swingHits.Length; i++)
            {
                Collider hitCollider = swingHits[i].collider;
                if (hitCollider == null || swingHits[i].distance >= bestDistance)
                {
                    continue;
                }

                MiniVanZombie zombie =
                    hitCollider.GetComponentInParent<MiniVanZombie>();
                if (zombie != null)
                {
                    bestZombie = zombie;
                    bestDistance = swingHits[i].distance;
                }
            }

            MiniVanZombie[] zombies =
                MiniVanSceneScan.Get<MiniVanZombie>();
            for (int i = 0; i < zombies.Length; i++)
            {
                MiniVanZombie zombie = zombies[i];
                if (zombie == null)
                {
                    continue;
                }

                Vector3 toZombie =
                    zombie.transform.position + Vector3.up - origin;
                float distance = toZombie.magnitude;
                float facing = Vector3.Dot(direction, toZombie.normalized);
                if (distance <= attackRange + attackRadius &&
                    facing > BatAttackFacingDot &&
                    distance < bestDistance)
                {
                    bestZombie = zombie;
                    bestDistance = distance;
                }
            }

            if (bestZombie != null)
            {
                bestZombie.TakeBatHit(
                    Mathf.Max(1, BatDamage),
                    origin,
                    BatZombieKnockbackDistance,
                    BatZombieKnockbackSeconds);
                ReportEnemyCombatHud(bestZombie);
                return true;
            }

            return false;
        }

        private MiniVanBreakableWoodCrate FindNearbyWoodCrateForBatHit(
            Vector3 origin,
            Vector3 direction,
            float attackRadius,
            float attackRange,
            out Vector3 hitPoint)
        {
            hitPoint = origin + direction * Mathf.Min(1f, attackRange);
            Vector3 start = origin - direction * 0.25f;
            Vector3 end = origin + direction * (attackRange + 0.75f);
            float radius = Mathf.Max(0.95f, attackRadius + 0.65f);
            Collider[] overlaps = Physics.OverlapCapsule(
                start,
                end,
                radius,
                ~0,
                QueryTriggerInteraction.Ignore);

            MiniVanBreakableWoodCrate bestCrate = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < overlaps.Length; i++)
            {
                Collider hitCollider = overlaps[i];
                MiniVanBreakableWoodCrate crate = hitCollider != null
                    ? hitCollider.GetComponentInParent<MiniVanBreakableWoodCrate>()
                    : null;
                if (crate == null || crate.IsBroken)
                {
                    continue;
                }

                Vector3 closest = hitCollider.ClosestPoint(origin + direction * (attackRange * 0.45f));
                float forwardDistance = Vector3.Dot(closest - origin, direction);
                if (forwardDistance < -0.45f || forwardDistance > attackRange + 0.75f)
                {
                    continue;
                }

                float lateral = Vector3.Cross(direction, closest - origin).magnitude;
                if (lateral > radius + 0.35f)
                {
                    continue;
                }

                float distance = Vector3.Distance(origin, closest);
                if (distance >= bestDistance)
                {
                    continue;
                }

                bestCrate = crate;
                bestDistance = distance;
                hitPoint = closest;
            }

            return bestCrate;
        }

        private MiniVanElectrifiedGenerator FindNearbyGeneratorForBatHit(
            Vector3 origin,
            Vector3 direction,
            float attackRadius,
            float attackRange)
        {
            Vector3 start = origin - direction * 0.2f;
            Vector3 end = origin + direction * (attackRange + 0.95f);
            float radius = Mathf.Max(1.25f, attackRadius + 0.9f);
            Collider[] overlaps = Physics.OverlapCapsule(
                start,
                end,
                radius,
                ~0,
                QueryTriggerInteraction.Ignore);

            MiniVanElectrifiedGenerator bestGenerator = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < overlaps.Length; i++)
            {
                Collider hitCollider = overlaps[i];
                MiniVanElectrifiedGenerator generator = hitCollider != null
                    ? hitCollider.GetComponentInParent<MiniVanElectrifiedGenerator>()
                    : null;
                if (generator == null || generator.IsBroken)
                {
                    continue;
                }

                Vector3 closest = hitCollider.ClosestPoint(origin + direction * (attackRange * 0.5f));
                float forwardDistance = Vector3.Dot(closest - origin, direction);
                if (forwardDistance < -0.35f || forwardDistance > attackRange + 0.95f)
                {
                    continue;
                }

                Vector3 pointOnSwing = origin + direction * Mathf.Clamp(forwardDistance, 0f, attackRange + 0.75f);
                float distanceFromSwing = Vector3.Distance(closest, pointOnSwing);
                if (distanceFromSwing > radius + 0.35f)
                {
                    continue;
                }

                float distance = (closest - origin).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestGenerator = generator;
                    bestDistance = distance;
                }
            }

            MiniVanElectrifiedGenerator[] generators =
                MiniVanSceneScan.Get<MiniVanElectrifiedGenerator>();
            for (int i = 0; i < generators.Length; i++)
            {
                MiniVanElectrifiedGenerator generator = generators[i];
                if (generator == null || generator.IsBroken)
                {
                    continue;
                }

                Collider generatorCollider = generator.SolidCollider != null
                    ? generator.SolidCollider
                    : generator.GetComponentInChildren<Collider>();
                Vector3 targetPoint = generatorCollider != null
                    ? generatorCollider.ClosestPoint(origin)
                    : generator.transform.position + Vector3.up * 0.6f;
                Vector3 toTarget = targetPoint - origin;
                float forwardDistance = Vector3.Dot(toTarget, direction);
                if (forwardDistance < -0.45f || forwardDistance > attackRange + 1.15f)
                {
                    continue;
                }

                Vector3 pointOnSwing = origin + direction * Mathf.Clamp(
                    forwardDistance,
                    0f,
                    attackRange + 0.95f);
                float distanceFromSwing = Vector3.Distance(targetPoint, pointOnSwing);
                if (distanceFromSwing > radius + 0.55f)
                {
                    continue;
                }

                float facing = toTarget.sqrMagnitude > 0.001f
                    ? Vector3.Dot(direction, toTarget.normalized)
                    : 1f;
                if (facing < -0.1f)
                {
                    continue;
                }

                float distance = toTarget.sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestGenerator = generator;
                    bestDistance = distance;
                }
            }

            return bestGenerator;
        }

        private bool TryBreakNearbyPanelkaWindow(
            Vector3 origin,
            Vector3 direction,
            float radius)
        {
            Vector3 center = origin + direction * 0.16f;
            Collider[] overlaps = Physics.OverlapSphere(
                center,
                radius,
                ~0,
                QueryTriggerInteraction.Collide);
            for (int i = 0; i < overlaps.Length; i++)
            {
                Collider candidate = overlaps[i];
                if (candidate == null)
                    continue;

                MiniVanPanelkaBreakableWindow window =
                    candidate.GetComponentInParent<
                        MiniVanPanelkaBreakableWindow>();
                if (window == null)
                    continue;

                Vector3 toWindow =
                    candidate.bounds.center - origin;
                bool originInside =
                    candidate.bounds.Contains(origin);
                if (!originInside &&
                    toWindow.sqrMagnitude > 0.001f &&
                    Vector3.Dot(direction, toWindow.normalized) < -0.05f)
                {
                    continue;
                }

                if (TryBreakPanelkaWindow(candidate))
                    return true;
            }

            return false;
        }

        [ClientRpc]
        private void SetLocalInventorySlotClientRpc(int slotIndex, int itemValue, ClientRpcParams clientRpcParams = default)
        {
            localSelectedSlot = Mathf.Clamp(slotIndex, 0, 3);
            UpdateHeldBatVisual();
            InvalidateStaticHeldVisuals();
            RefreshStaticHeldVisualsIfNeeded(true);
        }

        [ClientRpc]
        private void SetHotPotatoHeldClientRpc(NetworkObjectReference bombReference, bool held, int slotIndex, ClientRpcParams clientRpcParams = default)
        {
            if (held && bombReference.TryGet(out NetworkObject bombObject))
            {
                heldHotPotatoBomb = bombObject.GetComponent<MiniVanHotPotatoBomb>();
                heldHotPotatoBombSlot = Mathf.Clamp(slotIndex, 0, 3);
                if (IsOwner)
                {
                    localSelectedSlot = heldHotPotatoBombSlot;
                    hotPotatoDropBlockedUntilFrame = Mathf.Max(hotPotatoDropBlockedUntilFrame, Time.frameCount + 1);
                }
            }
            else
            {
                heldHotPotatoBomb = null;
                heldHotPotatoBombSlot = -1;
            }
        }

        public void ServerAttachHotPotatoBomb(MiniVanHotPotatoBomb bomb)
        {
            if (!IsServer || bomb == null)
            {
                return;
            }

            int slot = FindInventorySlot(MiniVanInventoryItem.HotPotatoBomb);
            if (slot < 0)
            {
                slot = FindFirstEmptyInventorySlot();
            }

            if (slot < 0)
            {
                slot = Mathf.Clamp(networkSelectedSlot.Value, 0, 3);
            }

            SetInventorySlot(slot, MiniVanInventoryItem.HotPotatoBomb);
            networkSelectedSlot.Value = slot;
            heldHotPotatoBomb = bomb;
            heldHotPotatoBombSlot = slot;
            SetHotPotatoHeldClientRpc(new NetworkObjectReference(bomb.NetworkObject), true, slot);
        }

        public void ServerDetachHotPotatoBomb(MiniVanHotPotatoBomb bomb)
        {
            if (!IsServer || bomb == null || heldHotPotatoBomb != bomb)
            {
                return;
            }

            int slot = FindInventorySlot(MiniVanInventoryItem.HotPotatoBomb);
            if (slot >= 0)
            {
                SetInventorySlot(slot, MiniVanInventoryItem.None);
            }

            heldHotPotatoBomb = null;
            heldHotPotatoBombSlot = -1;
            SetHotPotatoHeldClientRpc(new NetworkObjectReference(bomb.NetworkObject), false, -1);
        }

        public void ServerExplodeHotPotato(float seconds)
        {
            if (!IsServer)
            {
                return;
            }

            int slot = FindInventorySlot(MiniVanInventoryItem.HotPotatoBomb);
            if (slot >= 0)
            {
                SetInventorySlot(slot, MiniVanInventoryItem.None);
            }

            heldHotPotatoBomb = null;
            heldHotPotatoBombSlot = -1;
            SetHotPotatoHeldClientRpc(default, false, -1);
            StartHotPotatoPoopClientRpc(Mathf.Max(0.1f, seconds));
        }

        [ClientRpc]
        private void PlayBatSwingClientRpc(ClientRpcParams clientRpcParams = default)
        {
            batSwingTimer = GetBatAttackInterval();
            PlayPlayerBatSwingAnimation();
            UpdateHeldBatVisual();
        }

        [ServerRpc]
        private void FinishCoffeeServerRpc(ServerRpcParams rpcParams = default)
        {
            hasCoffee = false;
            int coffeeSlot = FindInventorySlot(MiniVanInventoryItem.Coffee);
            if (coffeeSlot >= 0)
            {
                SetInventorySlot(coffeeSlot, MiniVanInventoryItem.None);
            }

            SetCoffeeHeldClientRpc(false, -1);
        }

        [ClientRpc]
        private void SetCoffeeHeldClientRpc(bool held, int slotIndex, ClientRpcParams clientRpcParams = default)
        {
            hasCoffee = held;
            heldCoffeeSlot = held ? slotIndex : -1;
            if (held)
            {
                localSelectedSlot = Mathf.Clamp(slotIndex, 0, 3);
            }

            coffeeDrinkActive = false;
            coffeeDrinkTimer = 0f;
            EnsureHeldCoffeeVisual();
            UpdateCoffeeVisual();
        }

        [ClientRpc]
        public void RescueStartBoardingClientRpc(NetworkObjectReference vehicleReference, int seatIndex, Vector3 spawnPosition, Vector3 doorPosition, ClientRpcParams clientRpcParams = default)
        {
            if (!vehicleReference.TryGet(out NetworkObject vehicleObject))
            {
                return;
            }

            MiniVanVehicle vehicle = vehicleObject.GetComponent<MiniVanVehicle>();
            if (vehicle == null)
            {
                return;
            }

            MiniVanRescuePassengerVisual.StartBoarding(vehicle, seatIndex, spawnPosition, doorPosition);
        }

        [ClientRpc]
        public void RescueStartExitClientRpc(NetworkObjectReference vehicleReference, int seatIndex, Vector3 bunkerDoorPosition, ClientRpcParams clientRpcParams = default)
        {
            if (!vehicleReference.TryGet(out NetworkObject vehicleObject))
            {
                return;
            }

            MiniVanVehicle vehicle = vehicleObject.GetComponent<MiniVanVehicle>();
            if (vehicle == null)
            {
                return;
            }

            MiniVanRescuePassengerVisual.StartExit(vehicle, seatIndex, bunkerDoorPosition);
        }

        [ClientRpc]
        private void PlayRescueHornClientRpc(Vector3 position, ClientRpcParams clientRpcParams = default)
        {
            MiniVanHornAudio.PlayHorn(position);
        }

        private ClientRpcParams BuildOwnerTarget()
        {
            return new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new[] { OwnerClientId }
                }
            };
        }

        public bool IsCoffeeBoostActive => Time.time < coffeeBoostEndTime;

        public void SetDamWaterSlow(float divisor)
        {
            damWaterSlowDivisor = Mathf.Max(1f, divisor);
            damWaterImmersed = true;
        }

        public void SetDamWaterSurfaceY(float surfaceY)
        {
            damWaterSurfaceY = surfaceY;
        }

        public void ClearDamWaterSlow()
        {
            damWaterSlowDivisor = 1f;
            damWaterImmersed = false;
            damWaterOxygenRemaining = DamWaterOxygenSeconds;
            nextDamDrownDamageTime = 0f;
        }

        public bool IsZombieDead => networkHealth.Value <= 0;

        private float GetFallDamageReferenceHeight()
        {
            CacheStandingControllerPose();
            if (standingControllerHeight > 0.1f)
            {
                return standingControllerHeight;
            }

            if (CharacterController != null && CharacterController.height > 0.1f)
            {
                return CharacterController.height;
            }

            return DefaultPlayerHeightForFall;
        }

        private bool IsSupportedForFallDamage()
        {
            if (currentSeat != null || currentLadder != null || IsZombieDead)
            {
                return true;
            }

            if (damWaterImmersed)
            {
                return true;
            }

            if (currentHoverboardM != null)
            {
                return currentHoverboardM.IsSurfaceGrounded || currentHoverboardM.ProbeNearGround();
            }

            if (currentSkateboard != null)
            {
                // Skateboard has no shared grounded flag here — probe from rider/board.
                Vector3 origin = currentSkateboard.transform.position + Vector3.up * 0.35f;
                if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 1.2f, ~0, QueryTriggerInteraction.Ignore))
                {
                    return hit.collider == null || !hit.collider.transform.IsChildOf(currentSkateboard.transform);
                }

                return false;
            }

            return IsGroundedForJump();
        }

        private void UpdateFallDamageTracking()
        {
            if (!IsOwner || IsZombieDead)
            {
                fallDamageTracking = false;
                return;
            }

            // Seated / climbing: cancel any pending fall.
            if (currentSeat != null || currentLadder != null || damWaterImmersed)
            {
                fallDamageTracking = false;
                return;
            }

            bool supported = IsSupportedForFallDamage();
            float sampleY = transform.position.y;
            if (currentHoverboardM != null)
            {
                sampleY = currentHoverboardM.transform.position.y;
            }
            else if (currentSkateboard != null)
            {
                sampleY = currentSkateboard.transform.position.y;
            }

            if (!supported)
            {
                if (!fallDamageTracking)
                {
                    fallDamageTracking = true;
                    fallApexY = sampleY;
                }
                else
                {
                    fallApexY = Mathf.Max(fallApexY, sampleY);
                }

                return;
            }

            if (!fallDamageTracking)
            {
                return;
            }

            float fallDistance = fallApexY - sampleY;
            fallDamageTracking = false;
            if (fallDistance <= 0.05f)
            {
                return;
            }

            RequestFallDamageServerRpc(fallDistance);
        }

        [ServerRpc]
        private void RequestFallDamageServerRpc(float fallDistance, ServerRpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId)
            {
                return;
            }

            ApplyFallDamageServer(fallDistance);
        }

        private void ApplyFallDamageServer(float fallDistance)
        {
            if (!IsServer || networkHealth.Value <= 0)
            {
                return;
            }

            float playerHeight = GetFallDamageReferenceHeight();
            float mildThreshold = playerHeight * FallDamageHeightFactorMild;
            float lethalThreshold = playerHeight * FallDamageHeightFactorLethal;
            int damage;
            if (fallDistance >= lethalThreshold)
            {
                damage = MaxPlayerHealth;
            }
            else if (fallDistance >= mildThreshold)
            {
                damage = Mathf.Max(1, Mathf.RoundToInt(MaxPlayerHealth * FallDamageHealthFractionMild));
            }
            else
            {
                return;
            }

            ReceiveZombieDamageServer(damage);
        }

        public void ReceiveZombieDamageServer(int amount)
        {
            bool offlineAuthority =
                NetworkManager.Singleton == null ||
                !NetworkManager.Singleton.IsListening;
            if ((!IsServer && !offlineAuthority) || networkHealth.Value <= 0)
            {
                return;
            }

            networkHealth.Value = Mathf.Max(0, networkHealth.Value - Mathf.Max(1, amount));
            ZombieDamageFeedbackClientRpc(BuildOwnerTarget());
            if (networkHealth.Value > 0)
            {
                return;
            }

            ServerEnterDeathState();
        }

        [ClientRpc]
        private void ZombieDamageFeedbackClientRpc(ClientRpcParams clientRpcParams = default)
        {
            if (!IsOwner)
            {
                return;
            }

            damageFlashUntil = Time.time + DamageFlashSeconds;
        }

        [ClientRpc]
        private void ZombieRespawnClientRpc(ClientRpcParams clientRpcParams = default)
        {
            if (!IsOwner)
            {
                return;
            }

            currentSeat = null;
            currentVehicle = null;
            currentSkateboard = null;
            currentHoverboardM = null;
            movingPlatformVehicle = null;
            currentLadder = null;
            verticalVelocity = 0f;
            RestoreVehicleCollisionIgnore();

            if (CharacterController != null)
            {
                CharacterController.enabled = true;
            }

            MoveToSpawnPoint();
            PublishOwnedNetworkTransform(true);
        }

        private void HandleInventoryInput()
        {
            for (int i = 0; i < 4; i++)
            {
                if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i)))
                {
                    localSelectedSlot = i;
                    RequestSelectInventorySlotServerRpc(i);
                }
            }
        }

        private void HandleBatUse()
        {
            if (currentSeat != null || currentSkateboard != null || currentHoverboardM != null || heldTowCube != null || gearDragActive)
            {
                return;
            }

            if (!IsBatWeapon(GetInventorySlot(localSelectedSlot)) || Time.time < nextLocalBatSwingTime)
            {
                return;
            }

            if (Input.GetMouseButtonDown(0))
            {
                float attackInterval = GetBatAttackInterval();
                nextLocalBatSwingTime = Time.time + attackInterval;
                batSwingTimer = attackInterval;
                if (!IsSpawned)
                {
                    PlayPlayerBatSwingAnimation();
                }

                RequestBatAttackServerRpc(localSelectedSlot);
            }
        }

        private void HandleHotPotatoUse()
        {
            if (currentSeat != null || heldTowCube != null || gearDragActive)
            {
                return;
            }

            if (heldHotPotatoBomb == null || GetInventorySlot(localSelectedSlot) != MiniVanInventoryItem.HotPotatoBomb)
            {
                return;
            }

            if (!heldHotPotatoBomb.IsActivated && Time.frameCount > hotPotatoDropBlockedUntilFrame && MiniVanKeyBindings.GetKeyDown(MiniVanKeyAction.Interact))
            {
                Vector3 direction = PlayerCamera != null ? PlayerCamera.transform.forward : transform.forward;
                MiniVanHotPotatoBomb bomb = heldHotPotatoBomb;
                if (!IsServer)
                {
                    PredictClearInventoryItem(MiniVanInventoryItem.HotPotatoBomb);
                    heldHotPotatoBomb = null;
                    heldHotPotatoBombSlot = -1;
                    bomb.PlayLocalDropPrediction(this);
                }

                RequestHotPotatoDropServerRpc(new NetworkObjectReference(bomb.NetworkObject), direction);
                return;
            }

            if (Input.GetMouseButtonDown(0))
            {
                Vector3 direction = PlayerCamera != null ? PlayerCamera.transform.forward : transform.forward;
                MiniVanHotPotatoBomb bomb = heldHotPotatoBomb;
                if (!IsServer)
                {
                    PredictClearInventoryItem(MiniVanInventoryItem.HotPotatoBomb);
                    heldHotPotatoBomb = null;
                    heldHotPotatoBombSlot = -1;
                    bomb.PlayLocalThrowPrediction(this, direction);
                }
                else
                {
                    bomb.PlayLocalThrowPrediction(this, direction);
                }

                RequestHotPotatoThrowServerRpc(new NetworkObjectReference(bomb.NetworkObject), direction);
            }
        }

        private bool HasInventoryItem(MiniVanInventoryItem item)
        {
            return GetInventorySlot(0) == item
                || GetInventorySlot(1) == item
                || GetInventorySlot(2) == item
                || GetInventorySlot(3) == item;
        }

        private bool IsSelectedInventoryItem(MiniVanInventoryItem item)
        {
            return GetInventorySlot(localSelectedSlot) == item;
        }

        private static bool IsBatWeapon(MiniVanInventoryItem item)
        {
            return item == MiniVanInventoryItem.Bat || item == MiniVanInventoryItem.TestBaton;
        }

        public bool IsInventoryItemSelectedForWorld(MiniVanInventoryItem item)
        {
            int selectedSlot = IsOwner ? localSelectedSlot : networkSelectedSlot.Value;
            return GetInventorySlot(selectedSlot) == item;
        }

        /// <summary>
        /// Held inventory props with a fixed local pose. Refresh only when slot/seat/state changes —
        /// not every frame (coffee/bat animations stay on their own Update paths).
        /// </summary>
        private void InvalidateStaticHeldVisuals()
        {
            staticHeldVisualsStateKey = int.MinValue;
        }

        private void RefreshStaticHeldVisualsIfNeeded(bool force = false)
        {
            int selectedSlot = IsOwner ? localSelectedSlot : networkSelectedSlot.Value;
            MiniVanInventoryItem selectedItem = GetInventorySlot(selectedSlot);
            int key = selectedSlot;
            key = unchecked(key * 397) ^ (int)selectedItem;
            key = unchecked(key * 397) ^ (currentSeat != null ? 1 : 0);
            key = unchecked(key * 397) ^ (IsDowned ? 2 : 0);
            key = unchecked(key * 397) ^ (IsFacePainting() ? 4 : 0);
            key = unchecked(key * 397) ^ (extinguisherCharge > 0.001f ? 8 : 0);
            key = unchecked(key * 397) ^ (IsPlayingSnesTelevision() ? 16 : 0);
            // Include all slots so pickup/drop of a non-selected item still refreshes.
            key = unchecked(key * 397) ^ (int)GetInventorySlot(0);
            key = unchecked(key * 397) ^ ((int)GetInventorySlot(1) << 8);
            key = unchecked(key * 397) ^ ((int)GetInventorySlot(2) << 16);
            key = unchecked(key * 397) ^ ((int)GetInventorySlot(3) << 24);

            if (!force && key == staticHeldVisualsStateKey)
            {
                return;
            }

            staticHeldVisualsStateKey = key;
            UpdateDefibrillatorHeldVisual();
            UpdateFireExtinguisherHeldVisual();
            UpdateFlamethrowerHeldVisual();
            UpdateAntonLocatorHeldVisual();
            UpdatePizzaHeldVisual();
        }

        private int FindInventorySlot(MiniVanInventoryItem item)
        {
            for (int i = 0; i < 4; i++)
            {
                if (GetInventorySlot(i) == item)
                {
                    return i;
                }
            }

            return -1;
        }

        private int FindFirstEmptyInventorySlot()
        {
            for (int i = 0; i < 4; i++)
            {
                if (GetInventorySlot(i) == MiniVanInventoryItem.None)
                {
                    return i;
                }
            }

            return -1;
        }

        private MiniVanInventoryItem GetInventorySlot(int slotIndex)
        {
            slotIndex = Mathf.Clamp(slotIndex, 0, 3);
            // Host runs ServerRpc in-process; optimistic slots must not shadow authoritative reads.
            if (IsOwner && !IsServer && optimisticSlotActive[slotIndex])
            {
                return optimisticSlots[slotIndex];
            }

            switch (slotIndex)
            {
                case 0:
                    return (MiniVanInventoryItem)networkSlot0.Value;
                case 1:
                    return (MiniVanInventoryItem)networkSlot1.Value;
                case 2:
                    return (MiniVanInventoryItem)networkSlot2.Value;
                default:
                    return (MiniVanInventoryItem)networkSlot3.Value;
            }
        }

        private void SetInventorySlot(int slotIndex, MiniVanInventoryItem item)
        {
            switch (Mathf.Clamp(slotIndex, 0, 3))
            {
                case 0:
                    networkSlot0.Value = (int)item;
                    break;
                case 1:
                    networkSlot1.Value = (int)item;
                    break;
                case 2:
                    networkSlot2.Value = (int)item;
                    break;
                default:
                    networkSlot3.Value = (int)item;
                    break;
            }
        }

private void HandleCoffeeUse()
        {
            if (!hasCoffee || GetInventorySlot(localSelectedSlot) != MiniVanInventoryItem.Coffee || coffeeDrinkActive || currentSeat != null || gearDragActive)
            {
                return;
            }

            if (Input.GetMouseButtonDown(1))
            {
                coffeeDrinkActive = true;
                coffeeDrinkTimer = 0f;
                StartCoffeeDrinkServerRpc();
            }
        }

        [ServerRpc]
        private void StartCoffeeDrinkServerRpc(ServerRpcParams rpcParams = default)
        {
            if (!hasCoffee)
            {
                return;
            }

            coffeeBoostEndTime = Time.time + CoffeeDrinkSeconds + CoffeeBoostDuration;
            PlayCoffeeDrinkClientRpc();
        }

        [ClientRpc]
        private void PlayCoffeeDrinkClientRpc(ClientRpcParams clientRpcParams = default)
        {
            if (!hasCoffee)
            {
                return;
            }

            coffeeDrinkActive = true;
            coffeeDrinkTimer = 0f;
            EnsureHeldCoffeeVisual();
            UpdateCoffeeVisual();
        }

private void UpdateCoffeeVisual()
        {
            EnsureHeldCoffeeVisual();

            bool shouldShow = hasCoffee && currentSeat == null && GetInventorySlot(IsOwner ? localSelectedSlot : networkSelectedSlot.Value) == MiniVanInventoryItem.Coffee;
            if (heldCoffeeVisual != null)
            {
                heldCoffeeVisual.SetActive(shouldShow);
            }

            if (!shouldShow || heldCoffeePivot == null)
            {
                return;
            }

            float drink01 = 0f;
            if (coffeeDrinkActive)
            {
                coffeeDrinkTimer += Time.deltaTime;
                drink01 = Mathf.Clamp01(coffeeDrinkTimer / CoffeeDrinkSeconds);
            }

            SetCoffeeSurfaceVisible(drink01 < CoffeeSurfaceGoneAt);

            float pour01 = Mathf.InverseLerp(0f, CoffeeSurfaceGoneAt, drink01);
            float return01 = Mathf.InverseLerp(CoffeeReturnStartsAt, 1f, drink01);
            float tilt01 = coffeeDrinkActive ? Mathf.Lerp(pour01, 0f, return01) : 0f;
            float lift = Mathf.Sin(Mathf.Clamp01(pour01) * Mathf.PI) * 0.08f;

            if (IsOwner)
            {
                heldCoffeePivot.localPosition = new Vector3(0.31f, -0.24f + lift, 0.68f);
                heldCoffeePivot.localRotation = Quaternion.Euler(Mathf.Lerp(8f, -72f, tilt01), -12f, Mathf.Lerp(0f, -16f, tilt01));
            }
            else
            {
                heldCoffeePivot.localPosition = new Vector3(0.34f, 0.48f + lift, 0.34f);
                heldCoffeePivot.localRotation = Quaternion.Euler(Mathf.Lerp(-4f, -72f, tilt01), -18f, Mathf.Lerp(0f, -18f, tilt01));
            }

            if (IsOwner && coffeeDrinkActive && coffeeDrinkTimer >= CoffeeDrinkSeconds)
            {
                coffeeDrinkActive = false;
                hasCoffee = false;
                coffeeBoostEndTime = Time.time + CoffeeBoostDuration;
                heldCoffeeVisual.SetActive(false);
                FinishCoffeeServerRpc();
            }
        }

        private void UpdateHeldBatVisual()
        {
            EnsureHeldBatVisual();
            MiniVanInventoryItem shownWeapon = GetInventorySlot(IsOwner ? localSelectedSlot : networkSelectedSlot.Value);
            bool shouldShow = IsBatWeapon(shownWeapon) && currentSeat == null;
            if (heldBatVisual != null)
            {
                heldBatVisual.SetActive(shouldShow);
                Renderer[] batRenderers = heldBatVisual.GetComponentsInChildren<Renderer>(true);
                Color weaponColor = shownWeapon == MiniVanInventoryItem.TestBaton
                    ? new Color(0.92f, 0.025f, 0.018f, 1f)
                    : new Color(0.55f, 0.31f, 0.13f, 1f);
                for (int i = 0; i < batRenderers.Length; i++)
                {
                    if (batRenderers[i] != null && batRenderers[i].name.Contains("Barrel"))
                    {
                        batRenderers[i].material.color = weaponColor;
                    }
                }
            }

            if (!shouldShow || heldBatPivot == null)
            {
                return;
            }

            if (UsesSkeletalHeldItems())
            {
                if (heldBatPivot.parent != playerRightHand)
                {
                    heldBatPivot.SetParent(playerRightHand, false);
                }

                heldBatPivot.localPosition = BatHandLocalPosition;
                heldBatPivot.localRotation = Quaternion.Euler(BatHandLocalRotation);
                heldBatPivot.localScale = Vector3.one;
                return;
            }

            if (batSwingTimer > 0f)
            {
                batSwingTimer = Mathf.Max(0f, batSwingTimer - Time.deltaTime);
            }

            float attackInterval = GetBatAttackInterval();
            float swing01 = 1f - Mathf.Clamp01(batSwingTimer / attackInterval);
            float windupEnd = Mathf.Clamp(BatWindupEnd, 0.03f, 0.75f);
            float strikeEnd = Mathf.Clamp(BatStrikeEnd, windupEnd + 0.05f, 0.95f);
            float windup01 = batSwingTimer > 0f ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, windupEnd, swing01)) : 0f;
            float strike01 = batSwingTimer > 0f ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(windupEnd, strikeEnd, swing01)) : 0f;
            float recover01 = batSwingTimer > 0f ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(strikeEnd, 1f, swing01)) : 0f;

            Vector3 positionOffset = Vector3.Lerp(Vector3.zero, BatWindupPositionOffset, windup01);
            positionOffset = Vector3.Lerp(positionOffset, BatStrikePositionOffset, strike01);
            positionOffset = Vector3.Lerp(positionOffset, Vector3.zero, recover01);
            Vector3 rotationOffset = Vector3.Lerp(Vector3.zero, BatWindupRotationOffset, windup01);
            rotationOffset = Vector3.Lerp(rotationOffset, BatStrikeRotationOffset, strike01);
            rotationOffset = Vector3.Lerp(rotationOffset, Vector3.zero, recover01);

            heldBatPivot.localPosition = BatRestPosition + positionOffset;
            heldBatPivot.localRotation = Quaternion.Euler(BatRestRotation + rotationOffset);
            heldBatPivot.localScale = Vector3.one;
        }

        private float GetBatAttackInterval()
        {
            return Mathf.Max(0.05f, BatAttackInterval);
        }

        private void EnsureHeldBatVisual()
        {
            if (heldBatVisual != null)
            {
                return;
            }

            Transform parent = GetHeldItemParent();
            if (parent == null)
            {
                return;
            }

            heldBatVisual = CreateRuntimeHeldBat(parent);
            heldBatVisual.name = IsOwner ? "Held Bat" : "Remote Held Bat";
            heldBatPivot = heldBatVisual.transform;
            heldBatVisual.SetActive(false);
        }

        private GameObject CreateRuntimeHeldBat(Transform parent)
        {
            GameObject root = new GameObject("Runtime Held Bat");
            root.transform.SetParent(parent, false);

            Material wood = CreateRuntimeMaterial(new Color(0.55f, 0.31f, 0.13f, 1f));
            Material grip = CreateRuntimeMaterial(new Color(0.055f, 0.05f, 0.045f, 1f));

            GameObject barrel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            barrel.name = "Held Bat Barrel";
            barrel.transform.SetParent(root.transform, false);
            barrel.transform.localPosition = new Vector3(0f, 0.74f, 0f);
            barrel.transform.localScale = new Vector3(0.055f, 0.52f, 0.055f);
            SetPrimitiveMaterial(barrel, wood);
            DisablePrimitiveCollider(barrel);

            GameObject handle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            handle.name = "Held Bat Handle";
            handle.transform.SetParent(root.transform, false);
            handle.transform.localPosition = new Vector3(0f, 0.13f, 0f);
            handle.transform.localScale = new Vector3(0.033f, 0.22f, 0.033f);
            SetPrimitiveMaterial(handle, grip);
            DisablePrimitiveCollider(handle);

            return root;
        }

private void EnsureHeldCoffeeVisual()
        {
            if (heldCoffeeVisual != null)
            {
                return;
            }

            Transform parent = CameraRoot != null ? CameraRoot : transform;
            if (parent == null)
            {
                return;
            }

            heldCoffeeVisual = CoffeeMugPrefab != null ? Instantiate(CoffeeMugPrefab, parent, false) : CreateRuntimeCoffeeMug(parent);
            heldCoffeeVisual.name = IsOwner ? "Held Coffee Mug" : "Remote Coffee Mug";
            heldCoffeePivot = heldCoffeeVisual.transform;
            heldCoffeePivot.localPosition = IsOwner ? new Vector3(0.31f, -0.24f, 0.68f) : new Vector3(0.34f, 0.48f, 0.34f);
            heldCoffeePivot.localRotation = IsOwner ? Quaternion.Euler(8f, -12f, 0f) : Quaternion.Euler(-4f, -18f, 0f);
            heldCoffeePivot.localScale = IsOwner ? Vector3.one : Vector3.one * 0.72f;
            DisablePrimitiveCollider(heldCoffeeVisual);
            CacheCoffeeSurfaceRenderers();
            heldCoffeeVisual.SetActive(false);
        }

private GameObject CreateRuntimeCoffeeMug(Transform parent)
        {
            GameObject mugRoot = new GameObject("Runtime Coffee Mug");
            mugRoot.transform.SetParent(parent, false);

            Material mugMaterial = CreateRuntimeMaterial(new Color(0.58f, 0.59f, 0.55f, 1f));
            Material coffeeMaterial = CreateRuntimeMaterial(new Color(0.055f, 0.052f, 0.055f, 1f));

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.name = "Mug Body";
            body.transform.SetParent(mugRoot.transform, false);
            body.transform.localPosition = Vector3.zero;
            body.transform.localScale = new Vector3(0.17f, 0.15f, 0.17f);
            SetPrimitiveMaterial(body, mugMaterial);
            DisablePrimitiveCollider(body);

            GameObject rim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rim.name = "Mug Raised Rim";
            rim.transform.SetParent(mugRoot.transform, false);
            rim.transform.localPosition = new Vector3(0f, 0.158f, 0f);
            rim.transform.localScale = new Vector3(0.185f, 0.018f, 0.185f);
            SetPrimitiveMaterial(rim, mugMaterial);
            DisablePrimitiveCollider(rim);

            GameObject coffee = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            coffee.name = "Dark Coffee Surface";
            coffee.transform.SetParent(mugRoot.transform, false);
            coffee.transform.localPosition = new Vector3(0f, 0.145f, 0f);
            coffee.transform.localScale = new Vector3(0.128f, 0.01f, 0.128f);
            SetPrimitiveMaterial(coffee, coffeeMaterial);
            DisablePrimitiveCollider(coffee);

            Transform previousPivot = heldCoffeePivot;
            heldCoffeePivot = mugRoot.transform;
            CreateMugHandlePiece("Mug Handle Top", new Vector3(0.17f, 0.065f, 0f), new Vector3(0.085f, 0.03f, 0.048f), mugMaterial);
            CreateMugHandlePiece("Mug Handle Side", new Vector3(0.215f, -0.01f, 0f), new Vector3(0.032f, 0.105f, 0.048f), mugMaterial);
            CreateMugHandlePiece("Mug Handle Bottom", new Vector3(0.17f, -0.085f, 0f), new Vector3(0.085f, 0.03f, 0.048f), mugMaterial);
            heldCoffeePivot = previousPivot;
            return mugRoot;
        }

        private void CacheCoffeeSurfaceRenderers()
        {
            heldCoffeeSurfaceRenderers = System.Array.Empty<Renderer>();
            if (heldCoffeeVisual == null)
            {
                return;
            }

            Transform surface = FindChildRecursive(heldCoffeeVisual.transform, "Dark Coffee Surface");
            if (surface != null)
            {
                heldCoffeeSurfaceRenderers = surface.GetComponentsInChildren<Renderer>(true);
            }
        }

        private void SetCoffeeSurfaceVisible(bool visible)
        {
            if (heldCoffeeSurfaceRenderers == null)
            {
                return;
            }

            for (int i = 0; i < heldCoffeeSurfaceRenderers.Length; i++)
            {
                if (heldCoffeeSurfaceRenderers[i] != null)
                {
                    heldCoffeeSurfaceRenderers[i].enabled = visible;
                }
            }
        }

        private static Transform FindChildRecursive(Transform root, string childName)
        {
            if (root == null)
            {
                return null;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child.name == childName)
                {
                    return child;
                }

                Transform nested = FindChildRecursive(child, childName);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }

        private void CreateMugHandlePiece(string pieceName, Vector3 localPosition, Vector3 localScale, Material material)
        {
            GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
            piece.name = pieceName;
            piece.transform.SetParent(heldCoffeePivot, false);
            piece.transform.localPosition = localPosition;
            piece.transform.localScale = localScale;
            SetPrimitiveMaterial(piece, material);
            DisablePrimitiveCollider(piece);
        }

        private static Vector3 CreateRandomHatColor()
        {
            Color color = Random.ColorHSV(0f, 1f, 0.72f, 1f, 0.85f, 1f);
            return new Vector3(color.r, color.g, color.b);
        }

        private static Color VectorToColor(Vector3 colorValue)
        {
            return new Color(Mathf.Clamp01(colorValue.x), Mathf.Clamp01(colorValue.y), Mathf.Clamp01(colorValue.z), 1f);
        }

        private static Material CreateRuntimeMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            Material material = new Material(shader);
            material.color = color;
            return material;
        }

        private static void SetPrimitiveMaterial(GameObject gameObject, Material material)
        {
            Renderer renderer = gameObject != null ? gameObject.GetComponent<Renderer>() : null;
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
            }
        }

        private static void DisablePrimitiveCollider(GameObject gameObject)
        {
            Collider collider = gameObject != null ? gameObject.GetComponent<Collider>() : null;
            if (collider != null)
            {
                collider.enabled = false;
            }
        }


private bool HandleLadderClimb()
        {
            if (CharacterController == null || !CharacterController.enabled || currentSeat != null)
            {
                ReleaseCurrentLadder(false);
                return false;
            }

            // Roof exit blend owns the capsule until finished — don't drop it if the
            // climb trigger is left mid-handoff.
            if (ladderRoofExitT >= 0f)
            {
                return ContinueLadderRoofExitBlend();
            }

            if (ladderTopExitT >= 0f)
            {
                return ContinueLadderTopExit();
            }

            MiniVanLadder ladder = FindNearbyLadder();
            float lookUpDot = PlayerCamera != null
                ? Vector3.Dot(PlayerCamera.transform.forward, Vector3.up)
                : 0f;
            bool alreadyClimbing = currentLadder != null && currentLadder == ladder;
            // Keep look-up / look-down mutually exclusive while latched. The old overlap let S
            // keep moving the climb after the camera came back up, which sank ropes through floors.
            bool lookingUp = alreadyClimbing ? lookUpDot >= 0.12f : lookUpDot >= 0.22f;
            bool lookingDown = alreadyClimbing ? lookUpDot <= -0.12f : lookUpDot <= -0.35f;
            // Jump while looking up = climb; Jump otherwise = dismount (HL2-style drop-off).
            bool jumpPressed = MiniVanKeyBindings.GetKey(MiniVanKeyAction.Jump);
            bool jumpDismount = alreadyClimbing &&
                                MiniVanKeyBindings.GetKeyDown(MiniVanKeyAction.Jump) &&
                                !lookingUp;
            bool wantsAscend = MiniVanKeyBindings.GetKey(MiniVanKeyAction.MoveForward) ||
                               (jumpPressed && lookingUp && !jumpDismount);
            bool wantsDescend = MiniVanKeyBindings.GetKey(MiniVanKeyAction.MoveBack);
            bool ascending = wantsAscend && lookingUp;
            // Van ladders: never begin a downward climb from the cabin floor — only continue while
            // already climbing, or start from roof height. Panelka/building ladders use the same
            // top-entry check, but IsAtLadderRoofEntry treats the walkable roof/floor plane as valid.
            bool canStartDescend = alreadyClimbing || IsAtLadderRoofEntry(ladder);
            bool descending = wantsDescend && lookingDown && canStartDescend;
            bool hasExitSupport = alreadyClimbing &&
                                  HasLadderSupportBelow(ladder, LadderExitSupportReach);
            // Strafe / crouch always lets go; S without looking down also lets go.
            bool wantsStepOff = MiniVanKeyBindings.GetKey(MiniVanKeyAction.MoveLeft) ||
                                MiniVanKeyBindings.GetKey(MiniVanKeyAction.MoveRight) ||
                                MiniVanKeyBindings.GetKey(MiniVanKeyAction.Crouch) ||
                                (wantsDescend && !lookingDown);
            bool steppingOff = alreadyClimbing && (wantsStepOff || jumpDismount);
            // Idling with the feet on a floor hands control straight back to walking, so the
            // latch can never freeze the player against the rails.
            // Ground under the van ladder must not cancel a climb that just started from below.
            // Only treat a floor underfoot as a rest/dismount once the player has left the lower climb.
            bool restingOnFloor = alreadyClimbing && !ascending && !descending &&
                                  HasLadderSupportBelow(ladder, 0.25f) &&
                                  !IsNearLadderClimbBottom(ladder);
            // Stay latched while inside the climb volume so looking around / releasing input
            // mid-climb does not drop the player from a multi-storey Panelka ladder.
            bool holdingLadder = alreadyClimbing && !steppingOff && !restingOnFloor;

            if (ladder == null || steppingOff || restingOnFloor ||
                (!ascending && !descending && !holdingLadder))
            {
                if (ladder != null && (steppingOff || restingOnFloor))
                {
                    if (hasExitSupport || restingOnFloor)
                    {
                        SnapOntoLadderSupport(ladder);
                    }
                    else
                    {
                        // Mid-ladder drop / strafe-off: push clear of the rails then fall.
                        Vector3 face = ladder.GetClimbFaceDirection();
                        transform.position += face * 0.12f;
                        if (jumpDismount)
                        {
                            verticalVelocity = Mathf.Max(verticalVelocity, Mathf.Sqrt(JumpHeight * -2f * Gravity) * 0.55f);
                        }
                    }
                }

                ReleaseCurrentLadder(true);
                return false;
            }

            if (descending && !ladderDescending)
            {
                ladderDescendStartY = transform.position.y;
            }

            ladderDescending = descending;

            Transform ladderTransform = ladder.transform;
            MiniVanVehicle ladderVehicle = ladder.GetComponentInParent<MiniVanVehicle>();
            if (ladderVehicle != null)
            {
                UpdateVehicleCollisionIgnore(ladderVehicle);
            }

            if (currentLadder != ladder)
            {
                currentLadder = ladder;
                lastLadderPosition = ladderTransform.position;
                lastLadderRotation = ladderTransform.rotation;
                if (ladderVehicle != null)
                {
                    // Refresh ignore so solid ladder blocker ghosts only while climbing.
                    ApplyVehicleCollisionIgnoreState();
                }

                // Leave cabin-floor tilt behind so the climb does not inherit slope jerks.
                Vector3 climbUp = ladderVehicle != null ? ladderVehicle.transform.up : Vector3.up;
                if (climbUp.sqrMagnitude < 0.0001f || Vector3.Dot(climbUp.normalized, Vector3.up) < 0.45f)
                {
                    climbUp = Vector3.up;
                }

                smoothedTargetSurfaceUp = climbUp.normalized;
                walkingSurfaceUp = Vector3.Slerp(walkingSurfaceUp, climbUp.normalized, 0.35f).normalized;
                suppressSurfaceNormalProbeUntil = Time.time + LadderExitSurfaceProbeGrace;
                ladderDescendStartY = transform.position.y;
                // No hard snap onto a climb column — soft stick eases the capsule while climbing.
            }

            // Carry the player with the (possibly moving) ladder.
            Quaternion ladderDeltaRotation = ladderTransform.rotation * Quaternion.Inverse(lastLadderRotation);
            Vector3 relativePosition = transform.position - lastLadderPosition;
            Vector3 carriedPosition = ladderTransform.position + ladderDeltaRotation * relativePosition;
            Vector3 carriedDelta = carriedPosition - transform.position;

            // Filter suspension bounce so high-speed climbs don't shake the player.
            if (Mathf.Abs(carriedDelta.y) < MinorPlatformBounceThreshold)
            {
                carriedDelta.y *= MinorPlatformBounceFilter;
            }
            else
            {
                carriedDelta.y = Mathf.Clamp(carriedDelta.y, -MaxPlatformVerticalStep, MaxPlatformVerticalStep);
            }

            // Smooth roof hand-off (avoids the one-frame teleport jerk at the top rung).
            if (ladderRoofExitT >= 0f)
            {
                return ContinueLadderRoofExitBlend();
            }

            float climbDirectionSign = ascending ? 1f : (descending ? -1f : 0f);
            Vector3 climbMotion = ladder.ClimbDirection * (ladder.ClimbSpeed * climbDirectionSign);
            bool atRoofEntry = ascending && ladder.ShouldPushOntoRoof(transform.position);

            if (atRoofEntry && ladderVehicle != null)
            {
                BeginLadderRoofExitBlend(ladderVehicle, ladder);
                return ContinueLadderRoofExitBlend();
            }

            if (atRoofEntry && ladder.AutoTopExit)
            {
                if (TryBeginLadderTopExit(ladder))
                {
                    return ContinueLadderTopExit();
                }

                // Nothing to step onto up there (ladder ends in mid-air, or the landing is
                // blocked). Hold at the top rung rather than riding the rails into the sky.
                climbMotion = Vector3.zero;
            }
            else if (atRoofEntry)
            {
                climbMotion += ladder.RoofEntryDirection * ladder.RoofEntryPushSpeed;
            }

            Vector3 climbDelta = carriedDelta + climbMotion * Time.deltaTime;
            // Controller is off during the climb, so clamp every latched frame — not only while
            // actively descending. Looking up + holding S used to leave sinking uncapped.
            bool reachedLadderBottom = ClampLadderDescent(ladder, ref climbDelta, descending);

            // Prefer raw transform while climbing so the capsule never depenetrates into the Rigidbody.
            bool controllerWasEnabled = CharacterController != null && CharacterController.enabled;
            if (controllerWasEnabled)
            {
                CharacterController.enabled = false;
            }

            Vector3 nextPosition = transform.position + climbDelta;
            // Soft HL2 stick: free inside a deadzone, gentle pull toward preferred stand-off.
            nextPosition = ladder.SoftConstrainClimbPosition(nextPosition, Time.deltaTime);

            transform.position = nextPosition;

            if (controllerWasEnabled)
            {
                CharacterController.enabled = true;
            }

            verticalVelocity = 0f;
            movingPlatformVehicle = ladderVehicle;
            if (ladderVehicle != null)
            {
                // Keep platform trackers in sync during the climb; otherwise the hand-off back to
                // ApplyMovingPlatformMotion uses a stale pose and teleports the player by the distance
                // the van travelled while climbing.
                lastCarriedVehicleSeenTime = Time.time;
                lastPlatformPosition = ladderVehicle.transform.position;
                lastPlatformRotation = GetYawRotation(ladderVehicle.transform.rotation);
                lastPlatformCarryAlignedToTilt = false;
            }

            lastLadderPosition = ladderTransform.position;
            lastLadderRotation = ladderTransform.rotation;

            if (reachedLadderBottom)
            {
                // Hand control back to walking the moment the feet touch the landing.
                SnapOntoLadderSupport(ladder);
                ReleaseCurrentLadder(true);
                suppressSurfaceNormalProbeUntil = Time.time + LadderExitSurfaceProbeGrace;
                return false;
            }

            return true;
        }

        /// <summary>
        /// Standalone-ladder top exit: find the landing behind the rails and blend onto it.
        /// Returns false when there is nothing to step onto, so the caller can hold the climb
        /// at the top rung instead of overshooting.
        /// </summary>
        private bool TryBeginLadderTopExit(MiniVanLadder ladder)
        {
            if (ladder == null || CharacterController == null)
            {
                return false;
            }

            Vector3 inward = ladder.RoofEntryDirection;
            inward.y = 0f;
            if (inward.sqrMagnitude < 0.0001f)
            {
                inward = -ladder.GetClimbFaceDirection();
                inward.y = 0f;
            }

            if (inward.sqrMagnitude < 0.0001f)
            {
                return false;
            }

            inward.Normalize();

            float reach = Mathf.Max(0.2f, ladder.TopExitForward);
            float height = CharacterController.height;
            Vector3 probeOrigin = transform.position + Vector3.up * (height + 0.35f) + inward * reach;
            if (!Physics.Raycast(
                    probeOrigin,
                    Vector3.down,
                    out RaycastHit hit,
                    height + 1.6f,
                    ~0,
                    QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            Collider hitCollider = hit.collider;
            if (hitCollider == null || hitCollider.isTrigger || hitCollider is WheelCollider ||
                hit.distance <= 0f ||
                hitCollider.transform.IsChildOf(transform) ||
                hitCollider.GetComponentInParent<MiniVanLadder>() == ladder)
            {
                return false;
            }

            // Walls and steep faces are not landings.
            if (hit.normal.y < 0.6f)
            {
                return false;
            }

            float footOffset = height * 0.5f - CharacterController.center.y +
                               Mathf.Max(CharacterController.skinWidth, 0.025f);
            Vector3 end = hit.point + Vector3.up * footOffset;

            // The landing has to be at (or just above) the top of the climb — a floor found far
            // below means the ladder tops out over open space.
            if (end.y < transform.position.y - 0.3f)
            {
                return false;
            }

            if (IsLadderExitBlocked(end, ladder))
            {
                return false;
            }

            ladderTopExitStart = transform.position;
            ladderTopExitEnd = end;
            ladderTopExitLadder = ladder;
            ladderTopExitT = 0f;
            return true;
        }

        private bool ContinueLadderTopExit()
        {
            MiniVanLadder ladder = ladderTopExitLadder;
            // currentLadder is cleared directly by death / seat / respawn paths; never resurrect
            // a blend they abandoned.
            if (ladder == null || currentLadder != ladder || CharacterController == null)
            {
                ladderTopExitT = -1f;
                ladderTopExitLadder = null;
                return false;
            }

            bool controllerWasEnabled = CharacterController.enabled;
            if (controllerWasEnabled)
            {
                CharacterController.enabled = false;
            }

            float duration = Mathf.Max(0.1f, LadderTopExitBlendSeconds);
            ladderTopExitT = Mathf.MoveTowards(ladderTopExitT, 1f, Time.deltaTime / duration);
            float u = ladderTopExitT * ladderTopExitT * (3f - 2f * ladderTopExitT); // smoothstep
            transform.position = Vector3.Lerp(ladderTopExitStart, ladderTopExitEnd, u);

            if (controllerWasEnabled)
            {
                CharacterController.enabled = true;
            }

            verticalVelocity = 0f;

            if (ladderTopExitT < 1f)
            {
                return true;
            }

            // Landed on the deck: hand straight back to walking, no push-out (that would shove
            // the player back toward the rails they just cleared).
            ladderTopExitT = -1f;
            ladderTopExitLadder = null;
            ReleaseCurrentLadder(false);
            suppressSurfaceNormalProbeUntil = Time.time + LadderExitSurfaceProbeGrace;
            return false;
        }

        private void ReleaseCurrentLadder(bool pushOutOfLadder)
        {
            ladderDescending = false;
            ladderRoofExitT = -1f;
            ladderRoofExitVehicle = null;
            ladderRoofExitLadder = null;
            ladderTopExitT = -1f;
            ladderTopExitLadder = null;
            MiniVanLadder previous = currentLadder;
            if (previous == null)
            {
                currentLadder = null;
                return;
            }

            currentLadder = null;
            MiniVanVehicle previousVehicle = previous.GetComponentInParent<MiniVanVehicle>();
            if (previousVehicle != null)
            {
                UpdateVehicleCollisionIgnore(previousVehicle);
            }

            if (pushOutOfLadder)
            {
                PushOutOfLadderBlockers(previous);
            }
        }

        /// <summary>
        /// The climb moves the transform with the controller disabled, so the capsule can end
        /// up inside the rails. Nudge it back out horizontally so walking away works.
        /// </summary>
        private void PushOutOfLadderBlockers(MiniVanLadder ladder)
        {
            if (ladder == null || CharacterController == null || !CharacterController.enabled)
            {
                return;
            }

            Collider[] blockers = ladder.GetComponentsInChildren<Collider>(true);
            for (int iteration = 0; iteration < 2; iteration++)
            {
                bool moved = false;
                for (int i = 0; i < blockers.Length; i++)
                {
                    Collider blocker = blockers[i];
                    if (blocker == null || !blocker.enabled || blocker.isTrigger)
                    {
                        continue;
                    }

                    Vector3 direction;
                    float distance;
                    if (!Physics.ComputePenetration(
                            CharacterController,
                            transform.position,
                            transform.rotation,
                            blocker,
                            blocker.transform.position,
                            blocker.transform.rotation,
                            out direction,
                            out distance))
                    {
                        continue;
                    }

                    direction.y = 0f;
                    if (direction.sqrMagnitude < 0.000001f)
                    {
                        continue;
                    }

                    Vector3 candidate = transform.position +
                                        direction.normalized * (distance + 0.01f);
                    // Never trade the ladder for a wall: the thin blocker can push either way.
                    if (IsLadderExitBlocked(candidate, ladder))
                    {
                        continue;
                    }

                    transform.position = candidate;
                    moved = true;
                }

                if (!moved)
                {
                    break;
                }
            }
        }

        private bool IsLadderExitBlocked(Vector3 position, MiniVanLadder ladder)
        {
            if (CharacterController == null)
            {
                return false;
            }

            float radius = Mathf.Max(0.08f, CharacterController.radius * 0.85f);
            Vector3 chest = position + Vector3.up * (CharacterController.height * 0.5f);
            int count = Physics.OverlapSphereNonAlloc(
                chest, radius, ladderProbeResults, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider candidate = ladderProbeResults[i];
                if (candidate == null || candidate.isTrigger || candidate is WheelCollider)
                {
                    continue;
                }

                if (candidate.transform.IsChildOf(transform) ||
                    (ladder != null && candidate.GetComponentInParent<MiniVanLadder>() == ladder))
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private bool HasLadderSupportBelow(MiniVanLadder ladder, float reach)
        {
            float surfaceY;
            return TryProbeLadderSupport(ladder, reach, out surfaceY);
        }

        private bool IsNearLadderClimbBottom(MiniVanLadder ladder)
        {
            if (ladder == null)
            {
                return false;
            }

            float localY = ladder.transform.InverseTransformPoint(transform.position).y;
            // Van ladders: RoofEntryHeight ~3.2. Panelka ladders are taller. Stay latched against
            // ground-support dismount until the player has climbed out of the lower section.
            float bottomReleaseHeight = Mathf.Max(1.15f, ladder.RoofEntryHeight * 0.42f);
            return localY < bottomReleaseHeight;
        }

        /// <summary>
        /// Keeps a latched climb from sinking through the landing / rope floor and reports
        /// whether the feet have reached that surface.
        /// </summary>
        private bool ClampLadderDescent(
            MiniVanLadder ladder,
            ref Vector3 climbDelta,
            bool descending)
        {
            if (ladder == null)
            {
                return false;
            }

            float limitY = ladder.transform.position.y;
            float surfaceY;
            float probeReach = Mathf.Max(0.55f, -climbDelta.y + 0.45f);
            if (TryProbeLadderSupport(ladder, probeReach, out surfaceY))
            {
                // Ignore the floor the climb started on, but always honour floors near the
                // ladder/rope base — that is the landing the player must not fall through.
                bool belowStart = surfaceY <= ladderDescendStartY - 0.85f;
                bool nearBase = surfaceY <= ladder.transform.position.y + 1.35f;
                if (belowStart || nearBase)
                {
                    limitY = Mathf.Max(limitY, surfaceY);
                }
            }

            float nextY = transform.position.y + climbDelta.y;
            if (nextY >= limitY - 0.0001f && transform.position.y >= limitY - 0.02f)
            {
                return false;
            }

            climbDelta.y = limitY - transform.position.y;
            // Idle/hold frames that already sank below the floor still count as "bottom reached"
            // so looking up + S cannot leave the capsule inside the slab.
            return descending || transform.position.y <= limitY + 0.04f;
        }

        private void SnapOntoLadderSupport(MiniVanLadder ladder)
        {
            if (ladder == null || CharacterController == null)
            {
                return;
            }

            float surfaceY;
            if (!TryProbeLadderSupport(ladder, LadderExitSupportReach, out surfaceY) &&
                !TryProbeLadderSupport(ladder, 2.25f, out surfaceY))
            {
                surfaceY = ladder.transform.position.y;
            }

            if (transform.position.y >= surfaceY - 0.01f)
            {
                return;
            }

            bool controllerWasEnabled = CharacterController.enabled;
            if (controllerWasEnabled)
            {
                CharacterController.enabled = false;
            }

            Vector3 position = transform.position;
            position.y = surfaceY;
            transform.position = position;

            if (controllerWasEnabled)
            {
                CharacterController.enabled = true;
            }

            verticalVelocity = 0f;
        }

        private bool TryProbeLadderSupport(MiniVanLadder ladder, float reach, out float surfaceY)
        {
            surfaceY = 0f;
            if (CharacterController == null)
            {
                return false;
            }

            // Start the sweep high enough that a capsule already sunk into the slab still sees
            // the top face of the floor instead of a zero-distance "inside" hit.
            float radius = Mathf.Max(0.08f, CharacterController.radius * 0.7f);
            float lift = Mathf.Max(radius + 0.05f, 1.05f);
            Vector3 origin = transform.position + Vector3.up * lift;
            float distance = Mathf.Max(0.05f, reach) + lift;
            RaycastHit[] hits = Physics.SphereCastAll(
                origin, radius, Vector3.down, distance, ~0, QueryTriggerInteraction.Ignore);
            bool found = false;
            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit hit = hits[i];
                Collider hitCollider = hit.collider;
                if (hitCollider == null || hitCollider.isTrigger || hitCollider is WheelCollider)
                {
                    continue;
                }

                if (hitCollider.transform.IsChildOf(transform) ||
                    (ladder != null && hitCollider.GetComponentInParent<MiniVanLadder>() == ladder))
                {
                    continue;
                }

                // A zero-distance hit means the sweep started inside the collider; its point
                // and normal are meaningless.
                if (hit.distance <= 0f)
                {
                    continue;
                }

                if (hit.normal.y < 0.5f || hit.point.y > transform.position.y + 1.15f)
                {
                    continue;
                }

                if (!found || hit.point.y > surfaceY)
                {
                    surfaceY = hit.point.y;
                    found = true;
                }
            }

            return found;
        }

        private void BeginLadderRoofExitBlend(MiniVanVehicle vehicle, MiniVanLadder ladder)
        {
            if (vehicle == null || ladder == null)
            {
                return;
            }

            UpdateVehicleCollisionIgnore(vehicle);

            Vector3 up = vehicle.transform.up.sqrMagnitude > 0.0001f
                ? vehicle.transform.up.normalized
                : Vector3.up;
            Vector3 inward = ladder.RoofEntryDirection.sqrMagnitude > 0.0001f
                ? ladder.RoofEntryDirection.normalized
                : -ladder.GetClimbFaceDirection();

            ladderRoofExitT = 0f;
            ladderRoofExitStart = transform.position;
            ladderRoofExitEnd = transform.position + inward * 0.9f + up * 0.22f;
            ladderRoofExitVehicle = vehicle;
            ladderRoofExitLadder = ladder;

            // Prefer an actual roof support point under the end pose when available.
            Vector3 probeOrigin = ladderRoofExitEnd + up * 0.8f;
            if (Physics.Raycast(
                    probeOrigin,
                    -up,
                    out RaycastHit hit,
                    2.2f,
                    ~0,
                    QueryTriggerInteraction.Ignore) &&
                hit.collider != null &&
                hit.collider.transform.IsChildOf(vehicle.transform) &&
                !IsRoofHatchDoorCollider(hit.collider))
            {
                float footOffset = CharacterController != null
                    ? CharacterController.height * 0.5f - CharacterController.center.y +
                      Mathf.Max(CharacterController.skinWidth, 0.025f)
                    : 0.9f;
                Vector3 supportLocal = vehicle.transform.InverseTransformPoint(hit.point);
                if (supportLocal.y >= RoofCarryMinHeight - 0.25f)
                {
                    ladderRoofExitEnd = hit.point + up * footOffset;
                }
            }
        }

        private bool ContinueLadderRoofExitBlend()
        {
            MiniVanVehicle vehicle = ladderRoofExitVehicle;
            MiniVanLadder ladder = ladderRoofExitLadder != null ? ladderRoofExitLadder : currentLadder;
            if (vehicle == null || ladder == null || CharacterController == null)
            {
                ladderRoofExitT = -1f;
                return false;
            }

            Transform ladderTransform = ladder.transform;
            bool controllerWasEnabled = CharacterController.enabled;
            if (controllerWasEnabled)
            {
                CharacterController.enabled = false;
            }

            float duration = Mathf.Max(0.12f, LadderRoofExitBlendSeconds);
            ladderRoofExitT = Mathf.MoveTowards(ladderRoofExitT, 1f, Time.deltaTime / duration);
            float u = ladderRoofExitT * ladderRoofExitT * (3f - 2f * ladderRoofExitT); // smoothstep
            Vector3 blended = Vector3.Lerp(ladderRoofExitStart, ladderRoofExitEnd, u);

            // Ease onto the roof support without a hard Y teleport.
            if (TryGetVehicleSupportHit(vehicle, out RaycastHit supportHit) &&
                !IsRoofHatchDoorCollider(supportHit.collider))
            {
                Vector3 up = GetSupportUp(supportHit, vehicle);
                float footOffset = CharacterController.height * 0.5f - CharacterController.center.y +
                                   Mathf.Max(CharacterController.skinWidth, 0.025f);
                Vector3 desiredRoot = supportHit.point + up * footOffset;
                float alongUp = Vector3.Dot(desiredRoot - blended, up);
                if (alongUp > -0.05f)
                {
                    blended += up * Mathf.Lerp(0f, Mathf.Max(0f, alongUp), u);
                }
            }

            transform.position = blended;

            if (controllerWasEnabled)
            {
                CharacterController.enabled = true;
            }

            verticalVelocity = 0f;
            movingPlatformVehicle = vehicle;
            lastCarriedVehicleSeenTime = Time.time;
            lastPlatformPosition = vehicle.transform.position;
            lastPlatformRotation = GetYawRotation(vehicle.transform.rotation);
            lastPlatformCarryAlignedToTilt = false;
            lastLadderPosition = ladderTransform.position;
            lastLadderRotation = ladderTransform.rotation;

            Vector3 roofUp = vehicle.transform.up.sqrMagnitude > 0.0001f
                ? vehicle.transform.up.normalized
                : Vector3.up;
            smoothedTargetSurfaceUp = roofUp;
            walkingSurfaceUp = Vector3.Slerp(walkingSurfaceUp, roofUp, 1f - Mathf.Exp(-8f * Time.deltaTime)).normalized;

            if (ladderRoofExitT < 0.999f)
            {
                return true;
            }

            // Finished blend — hand off to roof walking.
            Vector3 local = vehicle.transform.InverseTransformPoint(transform.position);
            bool onRoof = local.y >= RoofCarryMinHeight - 0.2f || IsStandingOnVehicleRoof(vehicle);
            if (!onRoof)
            {
                // Extend the blend a bit further inward instead of snapping.
                ladderRoofExitStart = transform.position;
                ladderRoofExitEnd = transform.position + ladder.RoofEntryDirection.normalized * 0.45f;
                ladderRoofExitT = 0.35f;
                return true;
            }

            currentLadder = null;
            ladderRoofExitT = -1f;
            ladderRoofExitVehicle = null;
            ladderRoofExitLadder = null;
            suppressSurfaceNormalProbeUntil = Time.time + LadderExitSurfaceProbeGrace;
            ApplyVehicleCollisionIgnoreState();
            return true;
        }

        private bool IsAtLadderRoofEntry(MiniVanLadder ladder)
        {
            if (ladder == null)
            {
                return false;
            }

            if (ladder.ShouldPushOntoRoof(transform.position))
            {
                return true;
            }

            MiniVanVehicle vehicle = ladder.GetComponentInParent<MiniVanVehicle>();
            if (vehicle != null)
            {
                return IsPlayerAtVehicleRoofHeight(vehicle, transform.position) || IsStandingOnVehicleRoof(vehicle);
            }

            // Standalone ladders (Panelka exterior / hatch / ropes): RoofEntryHeight sits slightly
            // above the walkable exit plane, so a player already on the roof/floor would never
            // qualify via ShouldPushOntoRoof alone. Allow starting a descend from near the top.
            float localY = ladder.transform.InverseTransformPoint(transform.position).y;
            return localY >= ladder.RoofEntryHeight - 1.15f;
        }

        private MiniVanLadder FindNearbyLadder()
        {
            Vector3 probeCenter = transform.position + Vector3.up * 0.65f;
            int colliderCount = Physics.OverlapSphereNonAlloc(
                probeCenter,
                LadderProbeRadius,
                ladderProbeResults,
                ~0,
                QueryTriggerInteraction.Collide);
            MiniVanLadder bestLadder = null;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < colliderCount; i++)
            {
                Collider candidate = ladderProbeResults[i];
                MiniVanLadder ladder = candidate != null
                    ? candidate.GetComponentInParent<MiniVanLadder>()
                    : null;
                if (ladder == null || !ladder.CanEngage(probeCenter))
                {
                    continue;
                }

                Vector3 closest = candidate.ClosestPoint(probeCenter);
                float distance = Vector3.Distance(probeCenter, closest);
                if (distance < bestDistance)
                {
                    bestLadder = ladder;
                    bestDistance = distance;
                }
            }

            // Fallback: thin/miswired triggers can miss OverlapSphere while the player is
            // clearly at a van cabin ladder — scan active ladders near the capsule.
            if (bestLadder == null)
            {
                MiniVanLadder[] ladders = MiniVanSceneScan.Get<MiniVanLadder>();
                for (int i = 0; i < ladders.Length; i++)
                {
                    MiniVanLadder ladder = ladders[i];
                    if (ladder == null || !ladder.isActiveAndEnabled || !ladder.CanEngage(probeCenter))
                    {
                        continue;
                    }

                    float distance = Vector3.Distance(
                        probeCenter,
                        ladder.GetClimbHoldPosition(probeCenter));
                    if (distance < bestDistance && distance <= LadderProbeRadius + 0.75f)
                    {
                        bestLadder = ladder;
                        bestDistance = distance;
                    }
                }
            }

            if (bestLadder == null && currentLadder != null && currentLadder.CanEngage(probeCenter))
            {
                bestLadder = currentLadder;
            }

            return bestLadder;
        }
}
}

