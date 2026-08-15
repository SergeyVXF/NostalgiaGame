using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MiniVanGame
{
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(Rigidbody))]
    public partial class MiniVanVehicle : NetworkBehaviour
    {
        public const ulong EmptyClientId = ulong.MaxValue;

        [Header("Seats")]
        public MiniVanSeat[] Seats;

        [Header("Driving")]
        public float MotorAcceleration = 7f;
        public float ReverseAcceleration = 4.5f;
        public float BrakeStrength = 8f;
        public float HandbrakeStrength = 14f;
        public float TurnRate = 82f;
        public float RollingDrag = 0.18f;
        public float StableRideHeight = 0f;

        [Header("Physical Wheels")]
        public Transform FrontLeftWheel;
        public Transform FrontRightWheel;
        public Transform RearLeftWheel;
        public Transform RearRightWheel;
        public Transform SteeringWheel;
        public Transform EngineStatusLight;
        public Transform HandbrakeLever;
        public Transform GearLever;
        public Transform GearLeverGate;

        public float SteeringWheelVisualAngle = 180f;
        public float HandbrakeLeverOnX = -48.18f;
        public float HandbrakeLeverOffX = 0f;
        public Vector3 HandbrakeLeverOnLocalPosition = new Vector3(0f, 0.215f, 0.063f);
        public Vector3 HandbrakeLeverOffLocalPosition = new Vector3(0f, 0.106f, 0.063f);
        [Tooltip("How quickly the stick moves to the selected gate slot.")]
        public float GearLeverVisualMoveSpeed = 14f;

        [Header("Car Battery")]
        public Transform CarBatteryPlacementPoint;
        public Transform FrontCapot;
        public GameObject CarBatteryPrefab;
        public float CarBatteryChargePerMinuteDriving = 0.2f;
        public float CarBatteryChargePerMinuteIdle = 0.18f;
        public float CarBatteryChargePerMinuteFirstGear = 0.2f;
        public float CarBatteryChargePerMinuteSecondGear = 0.4f;
        public float CarBatteryChargePerMinuteThirdGear = 0.8f;
        public float CarBatteryChargePerMinuteFourthGear = 1.2f;
        public float CarBatteryChargePerMinuteFifthGear = 1.6f;
        public float CarBatteryDischargePerMinuteEngineOff = 1f;
        public float CarBatteryDischargePerMinuteInWater = 0.12f;
        [Range(0f, 1f)] public float CarBatteryBlinkThreshold01 = 0.25f;
        [Range(0f, 1f)] public float CarBatteryBlinkDimMultiplier = 0.04f;

        [Header("Engine Temperature")]
        public Transform EngineTemperatureAnchor;
        public GameObject EngineSmokePrefab;
        public float EngineTemperatureMaxC = 130f;
        public float EngineSmokeTemperatureC = 100f;
        [Tooltip("After an overheat stall at max temp, engine can start again only below this.")]
        public float EngineOverheatRestartBelowC = 120f;
        public float EngineFireDurationSeconds = 60f;
        [Range(0.05f, 1f)] public float EngineFireMaxHealthFraction = 0.5f;
        public GameObject EngineFirePrefab;
        public float EngineIdleCoolDegreesPerSecond = 0.5f;
        public float EngineOffCoolDegreesPerSecond = 1f;
        [Tooltip("Engine off + open hood: cool rate while hotter than EngineOffHoodOpenCoolThresholdC (130→50 in ~10s).")]
        public float EngineOffHoodOpenHotCoolDegreesPerSecond = 8f;
        [Tooltip("Below this temp with hood open and engine off, switch to the slow open-hood cool rate.")]
        public float EngineOffHoodOpenCoolThresholdC = 50f;
        [Tooltip("Engine off + open hood: cool rate at/below the threshold.")]
        public float EngineOffHoodOpenColdCoolDegreesPerSecond = 3f;
        [Tooltip("Open hood while running: multiply idle/off-style cooling.")]
        public float EngineOnHoodOpenCoolMultiplier = 2.75f;
        [Tooltip("Open hood while running: multiply positive heat gain (lower = cooler).")]
        [Range(0.1f, 1f)] public float EngineOnHoodOpenHeatMultiplier = 0.55f;
        public float EngineFirstGearHeatDegreesPerSecond = 1.5f;
        public float EngineFirstGearSteepHeatDegreesPerSecond = 1.5f;
        public float EngineSecondGearHeatDegreesPerSecond = 0.5f;
        public float EngineThirdGearHeatDegreesPerSecond = 0.4f;
        public float EngineFourthGearHeatDegreesPerSecond = 0.3f;
        public float EngineFifthGearHeatDegreesPerSecond = 0.25f;
        [Tooltip("Slope angle (degrees) that counts as a steep climb for heat rules.")]
        public float EngineSteepUphillAngle = 35f;
        [Tooltip("On steep climb only: RPM above this applies climb heat tier 1.")]
        public float EngineClimbHeatRpmTier1 = 3000f;
        public float EngineClimbHeatMultiplierTier1 = 2f;
        [Tooltip("On steep climb only: RPM above this applies climb heat tier 2.")]
        public float EngineClimbHeatRpmTier2 = 4000f;
        public float EngineClimbHeatMultiplierTier2 = 2.5f;
        [Tooltip("On steep climb only: RPM above this applies climb heat tier 3.")]
        public float EngineClimbHeatRpmTier3 = 5000f;
        public float EngineClimbHeatMultiplierTier3 = 3f;

        public float WheelRadius = 0.56f;
        public float SuspensionRestLength = 0.86f;
        public float SuspensionSpring = 8500f;
        public float SuspensionDamper = 7800f;
        public float MaxSuspensionForce = 9500f;
        public float RearDriveForce = 18000f;
        public float DirectDriveForce = 14000f;
        public float GearSpeedLimitBrake = 3600f;
        public float LowGearClimbAssist = 17500f;
        public float LowGearMaxSlopeAngle = 76f;
        public float LowGearCrawlSpeedKph = 20f;
        public float LowGearGravityCompensation = 1.22f;
        public float WheelGroundNormalY = 0.2f;
        public float EdgeReleaseSpringFactor = 0.18f;
        public float EdgeReleaseDamperFactor = 0.45f;
        public float EdgeReleaseExtraGravity = 0.7f;
        public float LedgeContactReleaseForce = 5200f;
        public bool UseLowFrictionBodyColliders = true;




        public float BrakeForce = 8000f;
        public float LateralGrip = 2.6f;
        public float FrontLateralGrip = 3.0f;
        public float MaxLateralForce = 3400f;
        public float SlopeSideHoldAcceleration = 6.5f;
        public float MaxSlopeSideHoldAcceleration = 14f;
        public float ParkingBrakeForceMultiplier = 4.5f;
        public float ParkingBrakeHoldAcceleration = 28f;
        public float ParkingBrakeStopSpeed = 0.18f;
        public float LandingImpactImpulse = 0.12f;
        public float MinLandingImpactSpeed = 4.5f;
        [Tooltip("How hard landings punch the body into the suspension (mass * downSpeed * this).")]
        public float LandingSuspensionPunch = 0.55f;
        [Tooltip("Nose bob on landing (VelocityChange torque scale).")]
        public float LandingPitchKick = 0.055f;
        [Tooltip("Extra suspension dig when vertical velocity suddenly stops on bumps.")]
        public float BumpResponseImpulse = 0.28f;
        public float BumpMinVerticalAccel = 16f;
        public float MaxSteerAngle = 63f;
        public float LowSpeedSteerAssist = 220f;
        public float Downforce = 12f;
        public float UprightAssist = 620f;
        public Vector3 CenterOfMassOffset = new Vector3(0f, -1.25f, -0.2f);

        [Header("Damage")]
        public float MaxHealth = 100f;
        public float CollisionDamageMinSpeedKph = 15f;
        public float CollisionDamagePerKph = 0.55f;
        public float FallDamageMinVerticalSpeed = 5.5f;
        public float FallDamagePerMeterPerSecond = 4.2f;
        public float WheelDetachFallVerticalSpeed = 7.2f;
        public float WheelDetachPitSpeedKph = 65f;
        public float WheelDetachPitContactLossSeconds = 0.04f;
        public float WheelDetachPitMinDepth = 0.9f;
        public float ZombieVehicleAttackRange = 3.6f;
        public float ZombieVehicleAttackInterval = 1f;
        public float ZombieVehicleDamagePerHit = 4f;
        public bool DebugVehicleDamage = true;

        [Header("Final Destruction")]
        public GameObject FinalExplosionPrefab;
        public string FinalExplosionResourcePath = "ExlplosionMinivan";
        public Vector3 FinalExplosionLocalOffset = new Vector3(0f, 1.25f, 0f);
        public float FinalVehicleDisappearDelay = 2f;
        public bool DebugFinalDestruction = true;

        [Header("Engine")]
        public float IdleRpm = 850f;
        public float StallRpm = 520f;
        public float RedlineRpm = 5200f;
        public float EngineInertia = 5.8f;
        public float EngineBrakeForce = 950f;

        [Header("Engine Sweet Spot")]
        [Tooltip("Center of the power band as 0..1 between Idle and Redline.")]
        [Range(0.2f, 0.8f)] public float SweetSpotCenterRpm01 = 0.48f;
        [Tooltip("Half-width of the strong power band (0..1 of Idle→Redline).")]
        [Range(0.05f, 0.35f)] public float SweetSpotHalfWidthRpm01 = 0.13f;
        [Tooltip("Extra torque at the exact sweet-spot center.")]
        public float SweetSpotTorqueBonus = 0.28f;
        [Tooltip("Torque floor when lugging far below the band.")]
        public float BelowSweetSpotTorqueFloor = 0.32f;
        [Tooltip("Torque floor when screaming near redline.")]
        public float AboveSweetSpotTorqueFloor = 0.42f;

        [Header("Handling")]
        public float SteeringResponse = 4.8f;
        public float SteeringReturnSpeed = 7.5f;
        public float AntiRollForce = 8500f;
        [Header("Body Sway")]
        [Tooltip("Nose dive / squat from forward acceleration (m/s² → torque).")]
        public float BodySwayPitchTorque = 0.085f;
        [Tooltip("Body roll from lateral acceleration (m/s² → torque).")]
        public float BodySwayRollTorque = 0.11f;
        [Tooltip("Extra pitch from throttle/brake intent at low speed.")]
        public float BodySwayInputPitch = 0.55f;
        [Tooltip("Extra roll from steering intent scaled by speed.")]
        public float BodySwayInputRoll = 0.028f;
        public float BodySwayMaxAccel = 14f;
        public float BodySwayAccelFilter = 10f;
        [Tooltip("How much lean upright-assist ignores before fighting (1 - upDot).")]
        public float BodySwayAllowedLean = 0.028f;
        public float HandbrakeRearGrip = 0.42f;
        public float HandbrakeDriftRearGrip = 0.24f;
        public float HandbrakeDriftMinSpeedKph = 12f;
        public float HandbrakeDriftSteerThreshold = 0.18f;
        public float HandbrakeDriftYawAssist = 9.5f;
        public float HandbrakeDriftLateralAssist = 2.3f;
        [Header("Natural Corner Slip")]
        [Tooltip("How much rear grip drops in hard turns (0 = none, 1 = strong).")]
        [Range(0f, 1f)] public float CornerSlipRearGripLoss = 0.1f;
        [Tooltip("Minimum speed before natural oversteer starts.")]
        public float CornerSlipMinSpeedKph = 48f;
        [Tooltip("Steering amount needed before the rear starts to loosen.")]
        public float CornerSlipSteerThreshold = 0.62f;
        [Tooltip("Small yaw kick when the rear is sliding in a turn.")]
        public float CornerSlipYawAssist = 1.1f;
        public float SlipWarningThreshold = 0.55f;

        [Header("Debug")]
        public bool DebugVehicle = true;
        public float DebugLogInterval = 0.5f;
        public bool DebugVehicleAnomalies = true;

        [Header("Zombie Roadkill")]
        public float ZombieRoadkillMinSpeedKph = 20f;
        public float ZombieKnockdownMinSpeedKph = 3f;
        public float ZombieKnockdownExplodeDelay = 0.75f;
        [Range(0f, 0.5f)] public float ZombieRoadkillSpeedLoss = 0.01f;
        public int ZombieRoadkillCubeCount = 56;
        public int ZombieRoadkillMaxCubesPerFrame = 96;
        public int ZombieRoadkillCrowdCubeCount = 18;
        public bool ZombieRoadkillCubePhysicsCollisions = false;
        public Vector2 ZombieRoadkillCubeLifetimeRange = new Vector2(0.5f, 2f);
        public float ZombieRoadkillCubeImpulse = 12.5f;
        public float ZombieRoadkillIngredientUpOffset = 0.55f;
        public float ZombieRoadkillSweepRadius = 1.75f;
        public float ZombieRoadkillTransformRadius = 3.1f;
        public float ZombieRoadkillVerticalTolerance = 4f;
        public float ZombieRoadkillSweepUpOffset = 0.95f;
        public float ZombieRoadkillLookAheadSeconds = 0.09f;
        public bool DebugZombieRoadkill = true;

        [Header("Vampire Ram")]
        [Tooltip("Below this speed a vampire ram does nothing.")]
        public float VampireRamMinSpeedKph = 1f;
        [Tooltip("From this speed the vampire takes 10% MaxHealth.")]
        public float VampireRamDamage10SpeedKph = 10f;
        [Tooltip("From this speed the vampire takes 20% MaxHealth.")]
        public float VampireRamDamage20SpeedKph = 30f;
        [Tooltip("From this speed the vampire takes 50% MaxHealth.")]
        public float VampireRamDamage50SpeedKph = 75f;
        public float VampireRamCooldownSeconds = 0.45f;

        [Header("Rescue Movement Lock")]
        public bool DebugRescueMovementLock = true;
        public float RescueLockLinearStopRate = 18f;
        public float RescueLockAngularStopRate = 12f;
        public float RescueLockBrakeTorqueMultiplier = 2.5f;




        [Header("Transmission")]
        public float SecondGearMinSpeedKph = 22f;
        public float ThirdGearMinSpeedKph = 45f;
        public float FourthGearMinSpeedKph = 55f;
        public float FifthGearMinSpeedKph = 90f;
        public float GearStallSeconds = 12.5f;
        public float GearBogDragForce = 1400f;


        public readonly NetworkVariable<bool> EngineOn = new NetworkVariable<bool>(false);
        public readonly NetworkVariable<int> CurrentGear = new NetworkVariable<int>((int)MiniVanGear.Neutral);
        public readonly NetworkVariable<float> SpeedKph = new NetworkVariable<float>(0f);
        public readonly NetworkVariable<float> NetworkSteerAngle = new NetworkVariable<float>(0f);
        public readonly NetworkVariable<float> NetworkSteeringInput = new NetworkVariable<float>(0f);
        public readonly NetworkVariable<float> NetworkVisualSpeed = new NetworkVariable<float>(0f);
        public readonly NetworkVariable<float> EngineRpm = new NetworkVariable<float>(0f);
        public readonly NetworkVariable<float> EngineLoad = new NetworkVariable<float>(0f);
        /// <summary>0 = outside power band, 1 = dead-center sweet spot.</summary>
        public readonly NetworkVariable<float> EngineSweetSpot01 = new NetworkVariable<float>(0f);
        public readonly NetworkVariable<float> WheelSlip = new NetworkVariable<float>(0f);
        public readonly NetworkVariable<bool> HandbrakeLocked = new NetworkVariable<bool>(true);
        public readonly NetworkVariable<float> Health = new NetworkVariable<float>(100f);
        public readonly NetworkVariable<int> DetachedWheelIndex = new NetworkVariable<int>(-1);
        public readonly NetworkVariable<float> EngineTemperatureC = new NetworkVariable<float>(0f);
        public readonly NetworkVariable<bool> EngineOnFire = new NetworkVariable<bool>(false);
        public readonly NetworkVariable<float> CarBatteryCharge01 = new NetworkVariable<float>(1f);
        public readonly NetworkVariable<bool> CarBatteryInstalled = new NetworkVariable<bool>(false);
        public readonly NetworkVariable<bool> SideDoorOpen = new NetworkVariable<bool>(false);
        public readonly NetworkVariable<bool> RoofDoorOpen = new NetworkVariable<bool>(false);
        public readonly NetworkVariable<bool> FrontCapotOpen = new NetworkVariable<bool>(false);

        public readonly NetworkVariable<Vector3> NetworkPosition = new NetworkVariable<Vector3>();
        public readonly NetworkVariable<Quaternion> NetworkRotation = new NetworkVariable<Quaternion>(Quaternion.identity);

        public readonly NetworkVariable<ulong> DriverOccupant = new NetworkVariable<ulong>(EmptyClientId);
        public readonly NetworkVariable<ulong> PassengerOneOccupant = new NetworkVariable<ulong>(EmptyClientId);
        public readonly NetworkVariable<ulong> PassengerTwoOccupant = new NetworkVariable<ulong>(EmptyClientId);
        public readonly NetworkVariable<ulong> PassengerThreeOccupant = new NetworkVariable<ulong>(EmptyClientId);

        private Rigidbody body;
        private float throttleInput;
        private float brakeInput;
        private float steeringInput;
        private bool handbrakeInput;
        private float lastDriverInputTime;
        private float currentSpeed;
        private float wheelSpin;
        private float nextDebugLogTime;
        private float gearStressTimer;
        private float engineRpm;
        private float smoothedSteeringInput;
        private float turnSpeedAnchorMps;
        private bool turnSpeedAnchorActive;
        private float frontSlip;
        private float rearSlip;
        private float lastStallDebugTime;
        private float nextAnomalyLogTime;
        private Vector3 lastBodyVelocity;
        private Vector3 swayLastWorldVelocity;
        private Vector3 swaySmoothedWorldAccel;
        private bool swayVelocityInitialized;
        private PhysicsMaterial lowFrictionBodyMaterial;
        private float lastEdgeReleaseDebugTime;
        private bool rescueMovementManuallyLocked;
        private float rescueMovementLockedUntil = -1f;
        private float nextRescueLockDebugTime;
        private Vector3 lastRoadkillSweepPosition;
        private bool hasRoadkillSweepPosition;
        private float nextRoadkillDebugTime;
        private int roadkillFixedTick;
        private int lastRoadkillSpeedLossTick = -1;
        private Vector3 lastStableLinearVelocity;
        private Vector3 lastStableAngularVelocity;
        private float lastStableVelocityTime = -999f;
        private float vampireRamHoldUntil = -999f;
        private Vector3 vampireRamHoldVelocity;
        private const float VampireRamVelocityMemorySeconds = 0.25f;
        private const float VampireRamVelocityHoldSeconds = 0.3f;
        private static int roadkillDebrisFrame = -1;
        private static int roadkillDebrisCubesThisFrame;
        private readonly Collider[] roadkillOverlapHits = new Collider[48];
        private static Material zombieRoadkillCubeMaterial;


        private const float AnomalyLogInterval = 0.35f;
        private const float RearWheelVisualSurfaceOffset = 0.06f;


        private WheelCollider frontLeftWheelCollider;
        private WheelCollider frontRightWheelCollider;
        private WheelCollider rearLeftWheelCollider;
        private WheelCollider rearRightWheelCollider;
        private Quaternion steeringWheelBaseLocalRotation;
        private bool steeringWheelBaseCaptured;
        private Transform gearLeverPos1P;
        private Transform gearLeverPos2P;
        private Transform gearLeverPos3P;
        private Transform gearLeverPos4P;
        private Transform gearLeverPos5P;
        private Transform gearLeverPosNP;
        private Transform gearLeverPosRP;
        private float gearLeverRestLocalY;
        private bool gearLeverVisualReady;
        private readonly List<Vector3> gearLeverPath = new List<Vector3>(4);
        private int gearLeverPathIndex;
        private MiniVanGear gearLeverVisualTargetGear = (MiniVanGear)(-1);
        private Renderer engineStatusLightRenderer;
        private Material engineStatusLightMaterial;
        private MiniVanCarBattery installedCarBattery;
        private MiniVanCarBatteryReceiver carBatteryReceiver;
        private bool localCarBatteryInstalled;
        private float localCarBatteryCharge01 = 1f;
        private float damWaterSlowDivisor = 1f;
        private float damWaterBatteryDrainMultiplier = 1f;
        private float damWaterSubmersion01;
        private GameObject engineSmokeInstance;
        private GameObject engineFireInstance;
        private MiniVanEngineAudio engineAudio;
        private ParticleSystem[] engineSmokeParticles;
        private ParticleSystem[] engineFireParticles;
        private ParticleSystem.MinMaxCurve[] engineSmokeOriginalRates;
        private float engineFireElapsed;
        private float engineFireDamageDealt;
        private readonly Dictionary<MiniVanZombie, float> zombieVehicleAttackTimes =
            new Dictionary<MiniVanZombie, float>();
        private readonly Dictionary<MiniVanVampire, float> vampireRamTimes =
            new Dictionary<MiniVanVampire, float>();
        private int lastGroundedWheelCount = 4;
        private bool vehicleAirborne;
        private float airborneMaxDownSpeed;
        private float airborneStartSpeedKph;
        private float lastBodyVerticalVelocity;
        private bool bodyVerticalVelocityInitialized;
        private readonly bool[] wheelWasGroundedForPit = new bool[4];
        private readonly float[] wheelUngroundedSince = new float[4];
        private bool wheelPitTrackingInitialized;
        private GameObject detachedWheelObject;
        private MiniVanWheelMountPoint[] wheelMountPoints;
        private static Material wheelGhostMaterial;
        private bool finalDestructionTriggered;
        private bool finalVehicleHidden;
        private GameObject finalExplosionInstance;

        private float smoothedWheelSteerAngle;
        private float smoothedWheelSpeed;
        private Vector3 remotePositionVelocity;
        private const float RemotePositionSmoothTime = 0.11f;
        private const float RemoteTeleportDistance = 5f;
        private const float RemoteRotationSharpness = 18f;

        public float Health01 => Mathf.Clamp01(Health.Value / Mathf.Max(1f, MaxHealth));



public override void OnNetworkSpawn()
        {
            finalDestructionTriggered = false;
            finalVehicleHidden = false;
            body = GetComponent<Rigidbody>();
            AutoAssignWheels();
            ConfigureBody();
            EnsureSkateboardShelf();
            EnsureSideDoor();
            EnsureRoofDoor();
            EnsureCarBatteryAndHoodSystem();
            EnsureWheelRepairMounts();
            ApplyInitialCarBatteryNetworkState();
            EnsureFuelSystemVisuals();
            EnsureEngineAudio();
            ConfigureNetworkPhysicsMode();

            if (IsServer)
            {
                Health.Value = Mathf.Clamp(MaxHealth, 1f, MaxHealth);
                DetachedWheelIndex.Value = -1;
                StableRideHeight = transform.position.y;
                NetworkPosition.Value = transform.position;
                NetworkRotation.Value = transform.rotation;
            }

            Health.OnValueChanged += HandleHealthChanged;
        }

        public override void OnNetworkDespawn()
        {
            Health.OnValueChanged -= HandleHealthChanged;
            base.OnNetworkDespawn();
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            AutoAssignWheels();
            ConfigureBody();
            EnsureSkateboardShelf();
            EnsureSideDoor();
            EnsureRoofDoor();
            EnsureCarBatteryAndHoodSystem();
            EnsureWheelRepairMounts();
            EnsureFuelSystemVisuals();
        }

        private void EnsureSideDoor()
        {
            EnsureDoor("Door", false, 2.35f, -88f, Vector3.up, new Vector3(0f, -0.5f, 0f));
        }

        private void EnsureRoofDoor()
        {
            EnsureDoor(new[] { "RoofDoor", "Roof Door", "Roof Hatch", "Hatch", "Roof (4)" }, true, 3.1f, -75f, Vector3.forward, new Vector3(0f, 0f, -0.5f));
        }

        private void EnsureDoor(string doorName, bool isRoofDoor, float interactRadius, float openAngle, Vector3 hingeAxis, Vector3 hingeOffset)
        {
            EnsureDoor(new[] { doorName }, isRoofDoor, interactRadius, openAngle, hingeAxis, hingeOffset);
        }

        private void EnsureDoor(string[] doorNames, bool isRoofDoor, float interactRadius, float openAngle, Vector3 hingeAxis, Vector3 hingeOffset)
        {
            Transform doorTransform = null;
            for (int i = 0; i < doorNames.Length && doorTransform == null; i++)
            {
                doorTransform = FindChildRecursive(transform, doorNames[i]);
            }

            if (doorTransform == null)
            {
                return;
            }

            MiniVanDoor door = doorTransform.GetComponent<MiniVanDoor>();
            if (door == null)
            {
                door = doorTransform.gameObject.AddComponent<MiniVanDoor>();
            }

            door.Vehicle = this;
            door.IsRoofDoor = isRoofDoor;
            door.InteractRadius = interactRadius;
            door.OpenAngle = openAngle;
            door.AnimationSpeed = 10f;
            door.ParentHingeAxis = hingeAxis;
            door.LocalHingeOffset = hingeOffset;
            door.EnsureSetup();
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

        private void EnsureSkateboardShelf()
        {
            Vector3 defaultShelfLocalPosition = new Vector3(0f, 1.42f, -2.82f);
            Transform existing = transform.Find("Skateboard Shelf");
            MiniVanSkateboardShelf shelf = existing != null ? existing.GetComponent<MiniVanSkateboardShelf>() : null;
            if (shelf != null)
            {
                MigrateSkateboardShelfToMovableRoot(existing, shelf, defaultShelfLocalPosition);
                shelf.EnsureShelfSetup();
                return;
            }

            GameObject shelfObject = existing != null ? existing.gameObject : new GameObject("Skateboard Shelf");
            shelfObject.transform.SetParent(transform, false);
            shelfObject.transform.localPosition = defaultShelfLocalPosition;
            shelfObject.transform.localRotation = Quaternion.identity;
            shelf = shelfObject.GetComponent<MiniVanSkateboardShelf>();
            if (shelf == null)
            {
                shelf = shelfObject.AddComponent<MiniVanSkateboardShelf>();
            }

            shelf.DefaultLocalAnchorPosition = Vector3.zero;
            shelf.DefaultLocalShelfSize = new Vector3(1.55f, 0.08f, 0.42f);
            shelf.InteractRadius = 1.55f;
            shelf.EnsureShelfSetup();
        }

        private static void MigrateSkateboardShelfToMovableRoot(Transform shelfTransform, MiniVanSkateboardShelf shelf, Vector3 defaultShelfLocalPosition)
        {
            if (shelfTransform == null || shelf == null)
            {
                return;
            }

            bool rootStillAtLegacyZero = shelfTransform.localPosition.sqrMagnitude <= 0.0001f;
            bool anchorStillStoresShelfPosition = shelf.DefaultLocalAnchorPosition.sqrMagnitude > 0.0001f;
            if (rootStillAtLegacyZero && anchorStillStoresShelfPosition)
            {
                shelfTransform.localPosition = shelf.DefaultLocalAnchorPosition;
            }
            else if (rootStillAtLegacyZero && shelf.DefaultLocalAnchorPosition.sqrMagnitude <= 0.0001f)
            {
                shelfTransform.localPosition = defaultShelfLocalPosition;
            }

            shelf.DefaultLocalAnchorPosition = Vector3.zero;
            shelf.DefaultLocalShelfSize = new Vector3(1.55f, 0.08f, 0.42f);
            shelf.InteractRadius = 1.55f;
        }
private void Update()
        {
            UpdateEngineStatusLight();
            ApplyHandbrakeLeverVisual();
            ApplyGearLeverVisual();
            UpdateFuelSystemVisuals();
            UpdateEngineSmokeVisual();
            UpdateEngineFireVisual();
            ApplyDetachedWheelState();
            UpdateEngineAudio();

            if (!IsSpawned)
            {
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
                remotePositionVelocity = Vector3.zero;
            }
            else
            {
                transform.position = Vector3.SmoothDamp(
                    transform.position,
                    targetPosition,
                    ref remotePositionVelocity,
                    RemotePositionSmoothTime,
                    Mathf.Infinity,
                    Time.deltaTime);

                float rotationBlend = 1f - Mathf.Exp(-RemoteRotationSharpness * Time.deltaTime);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationBlend);
            }

            AnimateRemoteWheels(NetworkSteerAngle.Value, NetworkSteeringInput.Value, NetworkVisualSpeed.Value);
            ApplyHandbrakeLeverVisual();
        }

        private void FixedUpdate()
        {
            if (!IsServer || body == null)
            {
                return;
            }

            roadkillFixedTick++;

            if (finalDestructionTriggered)
            {
                StopVehicleMotion();
                NetworkPosition.Value = transform.position;
                NetworkRotation.Value = transform.rotation;
                SpeedKph.Value = 0f;
                NetworkVisualSpeed.Value = 0f;
                return;
            }

            if (Time.time - lastDriverInputTime > 0.3f)
            {
                ClearDriverInputs();
            }

            ScanZombieRoadkillOverlap();

            if (IsRescueMovementLocked)
            {
                ApplyRescueMovementLock();
            }
            else
            {
                ApplyDriving();
            }

            ApplySuspensionBumpFeel(CountGroundedWheels());

            UpdateFuelConsumption();
            UpdateCarBatteryCharge();
            UpdateEngineTemperature();
            UpdateEngineFire();
            UpdateVehicleDamageSensors();

            UpdateEngineStatusLight();
            ApplyHandbrakeLeverVisual();
            UpdateEngineSmokeVisual();
            UpdateEngineFireVisual();
            ScanZombieVehicleAttacks();
            ApplyVampireRamVelocityHold();
            CacheStableVelocityForVampireRam();

            NetworkPosition.Value = transform.position;
            MonitorVehicleAnomalies();

            NetworkRotation.Value = transform.rotation;
            SpeedKph.Value = GetCurrentSpeedKph();
            NetworkSteeringInput.Value = steeringInput;
            NetworkSteerAngle.Value = frontLeftWheelCollider != null ? frontLeftWheelCollider.steerAngle : 0f;
            NetworkVisualSpeed.Value = currentSpeed;
            EngineRpm.Value = engineRpm;
            EngineSweetSpot01.Value = EngineOn.Value ? EvaluateEngineSweetSpot01(engineRpm) : 0f;
            WheelSlip.Value = Mathf.Max(frontSlip, rearSlip);
            ApplyDetachedWheelState();
        }

        public bool IsSeatAvailable(int seatIndex)
        {
            return GetOccupant(seatIndex) == EmptyClientId &&
                   !MiniVanPlayer.IsCorpseSeatOccupied(this, seatIndex) &&
                   !MiniVanPlayer.IsAntonSeatOccupied(this, seatIndex);
        }

        public bool IsRescueMovementLocked
        {
            get { return rescueMovementManuallyLocked || Time.time < rescueMovementLockedUntil; }
        }

        public void SetRescueMovementLocked(bool locked, string reason = "")
        {
            if (IsSpawned && !IsServer)
            {
                return;
            }

            if (rescueMovementManuallyLocked == locked)
            {
                return;
            }

            rescueMovementManuallyLocked = locked;
            ClearDriverInputs();
            if (locked && body != null)
            {
                ApplyRescueMovementLock();
            }

            if (DebugRescueMovementLock)
            {
                Debug.Log("[MiniVanRescueLock] " + (locked ? "locked" : "unlocked") + " " + reason + " vehicle=" + name);
            }
        }

        public void LockRescueMovementFor(float seconds, string reason = "")
        {
            if (IsSpawned && !IsServer)
            {
                return;
            }

            rescueMovementLockedUntil = Mathf.Max(rescueMovementLockedUntil, Time.time + Mathf.Max(0f, seconds));
            ClearDriverInputs();
            if (body != null)
            {
                ApplyRescueMovementLock();
            }

            if (DebugRescueMovementLock)
            {
                Debug.Log("[MiniVanRescueLock] timed lock " + seconds.ToString("0.0") + "s " + reason + " vehicle=" + name);
            }
        }

public void ClearRescueMovementLock(string reason = "")
        {
            if (IsSpawned && !IsServer)
            {
                return;
            }

            bool wasLocked = IsRescueMovementLocked;
            rescueMovementManuallyLocked = false;
            rescueMovementLockedUntil = -1f;
            ClearDriverInputs();

            if (DebugRescueMovementLock && wasLocked)
            {
                Debug.Log("[MiniVanRescueLock] cleared " + reason + " vehicle=" + name);
            }
        }


        private void ClearDriverInputs()
        {
            throttleInput = 0f;
            brakeInput = 0f;
            steeringInput = 0f;
            handbrakeInput = HandbrakeLocked.Value;
        }

        private void ApplyRescueMovementLock()
        {
            EnsureWheelColliders();
            ClearDriverInputs();

            if (body != null)
            {
                body.linearVelocity = Vector3.MoveTowards(body.linearVelocity, Vector3.zero, Mathf.Max(0.1f, RescueLockLinearStopRate) * Time.fixedDeltaTime);
                body.angularVelocity = Vector3.MoveTowards(body.angularVelocity, Vector3.zero, Mathf.Max(0.1f, RescueLockAngularStopRate) * Time.fixedDeltaTime);
                if (body.linearVelocity.sqrMagnitude < 0.0004f && body.angularVelocity.sqrMagnitude < 0.0004f)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
            }

            float brakeTorque = Mathf.Max(BrakeForce, BrakeForce * Mathf.Max(1f, RescueLockBrakeTorqueMultiplier));
            ApplyWheelLock(frontLeftWheelCollider, brakeTorque);
            ApplyWheelLock(frontRightWheelCollider, brakeTorque);
            ApplyWheelLock(rearLeftWheelCollider, brakeTorque);
            ApplyWheelLock(rearRightWheelCollider, brakeTorque);

            currentSpeed = 0f;
            frontSlip = 0f;
            rearSlip = 0f;
            smoothedSteeringInput = Mathf.MoveTowards(smoothedSteeringInput, 0f, SteeringReturnSpeed * Time.fixedDeltaTime);
            EngineLoad.Value = 0f;

            if (DebugRescueMovementLock && Time.time >= nextRescueLockDebugTime)
            {
                nextRescueLockDebugTime = Time.time + 1f;
                Debug.Log("[MiniVanRescueLock] holding vehicle=" + name + " vel=" + (body != null ? body.linearVelocity.magnitude.ToString("0.00") : "no-body"));
            }
        }

        private static void ApplyWheelLock(WheelCollider wheelCollider, float brakeTorque)
        {
            if (wheelCollider == null)
            {
                return;
            }

            wheelCollider.motorTorque = 0f;
            wheelCollider.brakeTorque = brakeTorque;
        }

        public ulong GetOccupant(int seatIndex)
        {
            switch (seatIndex)
            {
                case 0:
                    return DriverOccupant.Value;
                case 1:
                    return PassengerOneOccupant.Value;
                case 2:
                    return PassengerTwoOccupant.Value;
                case 3:
                    return PassengerThreeOccupant.Value;
                default:
                    return EmptyClientId;
            }
        }

        public MiniVanSeat GetSeat(int seatIndex)
        {
            if (Seats == null)
            {
                return null;
            }

            for (int i = 0; i < Seats.Length; i++)
            {
                if (Seats[i] != null && Seats[i].SeatIndex == seatIndex)
                {
                    return Seats[i];
                }
            }

            return null;
        }

        public bool IsDriver(ulong clientId)
        {
            return DriverOccupant.Value == clientId;
        }

        public void ServerReleaseClientSeat(ulong clientId)
        {
            if (!IsServer) return;
            int seatIndex = FindSeatIndexForClient(clientId);
            if (seatIndex < 0) return;
            SetOccupant(seatIndex, EmptyClientId);
            NotifyPlayerSeatState(clientId, seatIndex, false);
            ServerApplySeatPhysicsForClient(clientId, seatIndex, false);
            SendExitSeatClientRpc(seatIndex, BuildTarget(clientId));
            if (seatIndex == 0) ClearDriverInputs();
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestToggleSideDoorServerRpc(ServerRpcParams rpcParams = default)
        {
            SideDoorOpen.Value = !SideDoorOpen.Value;
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestToggleRoofDoorServerRpc(ServerRpcParams rpcParams = default)
        {
            RoofDoorOpen.Value = !RoofDoorOpen.Value;
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestToggleFrontCapotServerRpc(ServerRpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;
            MiniVanPlayer player = null;
            if (NetworkManager != null &&
                NetworkManager.ConnectedClients.TryGetValue(clientId, out NetworkClient client) &&
                client.PlayerObject != null)
            {
                player = client.PlayerObject.GetComponent<MiniVanPlayer>();
            }

            if (IsPlayerInsideCabinForHood(player))
            {
                return;
            }

            FrontCapotOpen.Value = !FrontCapotOpen.Value;
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestSeatServerRpc(int seatIndex, ServerRpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;

            if (seatIndex < 0 || seatIndex > 3)
            {
                return;
            }

            if (FindSeatIndexForClient(clientId) != -1 || !IsSeatAvailable(seatIndex))
            {
                return;
            }

            MiniVanSeat seat = GetSeat(seatIndex);
            if (seat == null)
            {
                return;
            }

            MiniVanPlayer player = null;
            if (NetworkManager != null &&
                NetworkManager.ConnectedClients.TryGetValue(clientId, out NetworkClient client) &&
                client.PlayerObject != null)
            {
                player = client.PlayerObject.GetComponent<MiniVanPlayer>();
            }

            if (player == null || !seat.IsPlayerInEnterRange(player.transform.position))
            {
                return;
            }

            SetOccupant(seatIndex, clientId);
            
NotifyPlayerSeatState(clientId, seatIndex, true);
            ServerApplySeatPhysicsForClient(clientId, seatIndex, true);
            
SendEnterSeatClientRpc(seatIndex, BuildTarget(clientId));
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestExitSeatServerRpc(int seatIndex, ServerRpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;

            if (GetOccupant(seatIndex) != clientId)
            {
                return;
            }

            SetOccupant(seatIndex, EmptyClientId);
            
NotifyPlayerSeatState(clientId, seatIndex, false);
            ServerApplySeatPhysicsForClient(clientId, seatIndex, false);
            
SendExitSeatClientRpc(seatIndex, BuildTarget(clientId));

            if (seatIndex == 0)
            {
                throttleInput = 0f;
                brakeInput = 0f;
                steeringInput = 0f;
                handbrakeInput = HandbrakeLocked.Value;
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void SubmitDriverInputServerRpc(float throttle, float brake, float steering, bool handbrake, ServerRpcParams rpcParams = default)
        {
            if (!IsDriver(rpcParams.Receive.SenderClientId))
            {
                return;
            }

            if (IsRescueMovementLocked)
            {
                ClearDriverInputs();
                lastDriverInputTime = Time.time;
                return;
            }

            throttleInput = Mathf.Clamp01(throttle);
            brakeInput = Mathf.Clamp01(brake);
            steeringInput = Mathf.Clamp(steering, -1f, 1f);
            handbrakeInput = handbrake;
            if (HandbrakeLocked.Value != handbrake)
            {
                HandbrakeLocked.Value = handbrake;
            }
            lastDriverInputTime = Time.time;
        }

        [ServerRpc(RequireOwnership = false)]
        public void SetEngineServerRpc(bool engineOn, ServerRpcParams rpcParams = default)
        {
            if (!IsDriver(rpcParams.Receive.SenderClientId))
            {
                return;
            }

            if (engineOn && (FuelLiters.Value <= 0.001f || !HasEffectiveCarBatteryInstalled() || GetEffectiveCarBatteryCharge01() <= 0.001f))
            {
                EngineOn.Value = false;
                EngineLoad.Value = 0f;
                return;
            }

            if (engineOn && IsEngineTooHotToStart())
            {
                EngineOn.Value = false;
                EngineLoad.Value = 0f;
                return;
            }

            EngineOn.Value = engineOn;
            if (engineOn)
            {
                engineRpm = Mathf.Max(engineRpm, IdleRpm);
                EngineRpm.Value = engineRpm;
                gearStressTimer = 0f;
            }
            else
            {
                EngineLoad.Value = 0f;
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void SetCarBatteryInstalledServerRpc(bool installed, float charge01, ServerRpcParams rpcParams = default)
        {
            SetCarBatteryInstalledOnAuthority(installed, charge01);
        }

        [ServerRpc(RequireOwnership = false)]
        public void SetGearServerRpc(int gearValue, ServerRpcParams rpcParams = default)
        {
            if (!IsDriver(rpcParams.Receive.SenderClientId))
            {
                return;
            }

            MiniVanGear requestedGear = (MiniVanGear)Mathf.Clamp(gearValue, (int)MiniVanGear.Park, (int)MiniVanGear.Fifth);
            if (!CanShiftToGear(requestedGear))
            {
                return;
            }

            CurrentGear.Value = (int)requestedGear;
            gearStressTimer = 0f;
        }

        [ClientRpc]
        private void SendEnterSeatClientRpc(int seatIndex, ClientRpcParams clientRpcParams = default)
        {
            if (MiniVanPlayer.LocalPlayer != null)
            {
                MiniVanPlayer.LocalPlayer.EnterSeatClientSide(this, seatIndex);
            }
        }

        [ClientRpc]
        private void SendExitSeatClientRpc(int seatIndex, ClientRpcParams clientRpcParams = default)
        {
            if (MiniVanPlayer.LocalPlayer != null)
            {
                MiniVanPlayer.LocalPlayer.ExitSeatClientSide(this, seatIndex);
            }
        }

private void ApplyDriving()
        {
            EnsureWheelColliders();
            ApplyDetachedWheelState();

            MiniVanGear gear = (MiniVanGear)CurrentGear.Value;
            float targetSpeed = GetTargetSpeed(gear);
            Vector3 planarVelocity = Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up);
            float signedForwardSpeed = Vector3.Dot(body.linearVelocity, transform.forward);
            float planarSpeed = planarVelocity.magnitude;
            currentSpeed = signedForwardSpeed;
            int groundedWheels = CountGroundedWheels();
            int groundedRearWheels = CountValidRearGroundedWheels();
            // Hills no longer rewrite the gear's top-speed cap — grade load / torque sag does the work.
            // Keep a tiny crawl boost only for 1st on steep climbs so the "rescue gear" still works.
            targetSpeed = GetSlopeAdjustedTargetSpeed(gear, targetSpeed, groundedWheels);

            float waterKeepFactor = damWaterSlowDivisor > 1.001f ? 1f / damWaterSlowDivisor : 1f;
            if (waterKeepFactor < 0.999f)
            {
                targetSpeed *= waterKeepFactor;
                if (planarSpeed > 0.15f)
                {
                    // Stronger drag when deeper so coasting also hits the water speed cap.
                    float dragStrength = Mathf.Lerp(8f, 22f, damWaterSubmersion01);
                    body.AddForce(-body.linearVelocity * dragStrength * (1f - waterKeepFactor), ForceMode.Acceleration);
                }
            }

            // Use planar speed for gear/load so yaw during turns doesn't fake a near-stop.
            float speedKph = planarSpeed * 3.6f;
            float turnAmount = Mathf.Abs(smoothedSteeringInput);
            UpdateTurnSpeedAnchor(gear, planarSpeed, turnAmount);

            float minimumUsefulSpeed = GetMinimumUsefulSpeedKph(gear);
            // Bog drag when too slow for the gear under throttle. Stall uses a stricter check later.
            bool luggingGear = EngineOn.Value && throttleInput > 0.2f && speedKph + 0.5f < minimumUsefulSpeed;
            float gearLoadFactor = GetGearLoadFactor(gear, speedKph);

            // RPM follows the turn-hold speed so the tach doesn't die when scrub bleeds planar speed.
            float rpmSpeedKph = GetTurnHeldSpeedKph(speedKph);
            UpdateEngineState(gear, rpmSpeedKph, signedForwardSpeed);
            UpdateGearStress(gear, luggingGear, minimumUsefulSpeed, speedKph);

            float engineTorqueFactor = GetEngineTorqueFactor(engineRpm);
            bool canAccelerate = EngineOn.Value &&
                                 throttleInput > 0.01f &&
                                 !handbrakeInput &&
                                 Mathf.Abs(targetSpeed) > 0.01f &&
                                 planarSpeed < Mathf.Abs(targetSpeed);

            float steeringStep = (Mathf.Abs(steeringInput) > Mathf.Abs(smoothedSteeringInput) ? SteeringResponse : SteeringReturnSpeed) * Time.fixedDeltaTime;
            smoothedSteeringInput = Mathf.MoveTowards(smoothedSteeringInput, steeringInput, steeringStep);
            turnAmount = Mathf.Abs(smoothedSteeringInput);

            // Cap steer earlier with speed so tire scrub doesn't act like a brake.
            float steerLimit = Mathf.Lerp(MaxSteerAngle, MaxSteerAngle * 0.24f, Mathf.InverseLerp(8f, 55f, speedKph));
            float steerAngle = smoothedSteeringInput * steerLimit;
            float direction = Mathf.Approximately(targetSpeed, 0f) ? 1f : Mathf.Sign(targetSpeed);
            // Keep drive in turns — WheelCollider scrub already costs speed.
            float turnLoad = 1f;
            float torqueMultiplier = GetTorqueMultiplier(gear);
            float tractionFactor = GetRearTractionFactor();
            // 1st = only real takeoff gear. 2nd+ from standstill barely crawls (readable lugging).
            float launchSoftener = 1f;
            if (gear == MiniVanGear.First)
            {
                launchSoftener = Mathf.Lerp(0.78f, 1f, Mathf.InverseLerp(2f, 20f, speedKph));
            }
            else if (gear == MiniVanGear.Second)
            {
                launchSoftener = Mathf.Lerp(0.18f, 1f, Mathf.InverseLerp(8f, 28f, speedKph));
            }
            else if (gear == MiniVanGear.Third)
            {
                // Full pull by a normal green 2→3 handoff speed.
                launchSoftener = Mathf.Lerp(0.22f, 1f, Mathf.InverseLerp(22f, 40f, speedKph));
            }
            else if (gear == MiniVanGear.Fourth)
            {
                // Full pull by a normal green 3→4 handoff — was starving torque at ~50 and killing RPM.
                launchSoftener = Mathf.Lerp(0.2f, 1f, Mathf.InverseLerp(35f, 55f, speedKph));
            }
            else if (gear == MiniVanGear.Fifth)
            {
                launchSoftener = Mathf.Lerp(0.18f, 0.95f, Mathf.InverseLerp(70f, 100f, speedKph));
            }

            // Falloff uses the gear's true top speed (not a hill-shrunk cap) so tach and pull stay in sync.
            float gearTopKph = Mathf.Max(1f, Mathf.Abs(MiniVanGearUtility.MaxForwardSpeed(gear)) * 3.6f);
            if (gear == MiniVanGear.Reverse)
            {
                gearTopKph = Mathf.Max(1f, Mathf.Abs(targetSpeed) * 3.6f);
            }

            float gearProgress = Mathf.Clamp01(speedKph / gearTopKph);
            // Softer falloff so once you're rolling in gear, light throttle can hold cruise.
            float falloffFloor = gear == MiniVanGear.First ? 0.34f
                : gear == MiniVanGear.Second ? 0.36f
                : gear == MiniVanGear.Third ? 0.42f
                : gear == MiniVanGear.Fourth ? 0.48f
                : gear == MiniVanGear.Fifth ? 0.52f
                : 0.36f;
            float powerFalloff = Mathf.Lerp(1f, falloffFloor, gearProgress * gearProgress);

            // Higher gear on a climb = less shove. 1st stays usable; 3+ struggle hard.
            float uphillSeverity = GetUphillSeverity01();
            float uphillFactor = GetUphillDriveFactor(gear, uphillSeverity);

            float cruiseSustain = GetCruiseSustainFactor(gear, speedKph, throttleInput, uphillSeverity);
            float driveShaping = launchSoftener * powerFalloff * uphillFactor * cruiseSustain;
            float turnKeep = GetTurnDriveKeep(gear, turnAmount);

            float motorTorque = canAccelerate
                ? RearDriveForce * throttleInput * direction * gearLoadFactor * engineTorqueFactor * torqueMultiplier * turnLoad * tractionFactor * waterKeepFactor * driveShaping * turnKeep
                : 0f;

            float brakeTorque = brakeInput > 0.01f ? BrakeForce * brakeInput : 0f;
            float parkingBrakeTorque = BrakeForce * Mathf.Max(1f, ParkingBrakeForceMultiplier);
            bool handbrakeDriftRequested = handbrakeInput &&
                                           speedKph >= HandbrakeDriftMinSpeedKph &&
                                           Mathf.Abs(smoothedSteeringInput) >= HandbrakeDriftSteerThreshold;

            if (gear == MiniVanGear.Park)
            {
                brakeTorque = Mathf.Max(brakeTorque, parkingBrakeTorque);
            }

            if (handbrakeInput)
            {
                motorTorque = 0f;
            }

            // Bog only when the tach is also out of the green — a correct upshift with healthy RPM
            // must not get secretly dragged down below the gear's min-speed number.
            if (luggingGear && !IsEngineTachHealthyForGearHold())
            {
                float dragSeverity = Mathf.InverseLerp(minimumUsefulSpeed, 0f, speedKph);
                float bog = GearBogDragForce * dragSeverity * (1f + GetHillGradeGearCost(gear) * 0.35f);
                bog *= 1f + GetUphillSeverity01() * 0.6f;
                body.AddForce(-transform.forward * Mathf.Sign(Mathf.Max(0.1f, signedForwardSpeed)) * bog, ForceMode.Force);
            }

            ApplyDynamicWheelFriction(speedKph);

            frontLeftWheelCollider.steerAngle = steerAngle;
            frontRightWheelCollider.steerAngle = steerAngle;
            rearLeftWheelCollider.steerAngle = 0f;
            rearRightWheelCollider.steerAngle = 0f;

            frontLeftWheelCollider.motorTorque = 0f;
            frontRightWheelCollider.motorTorque = 0f;
            rearLeftWheelCollider.motorTorque = motorTorque * 0.5f;
            rearRightWheelCollider.motorTorque = motorTorque * 0.5f;

            float frontBrakeTorque = brakeTorque;
            float rearBrakeTorque = brakeTorque;
            if (handbrakeInput)
            {
                rearBrakeTorque = Mathf.Max(rearBrakeTorque, HandbrakeStrength);
                if (!handbrakeDriftRequested)
                {
                    frontBrakeTorque = Mathf.Max(frontBrakeTorque, parkingBrakeTorque);
                    rearBrakeTorque = Mathf.Max(rearBrakeTorque, parkingBrakeTorque);
                }
            }

            frontLeftWheelCollider.brakeTorque = frontBrakeTorque;
            frontRightWheelCollider.brakeTorque = frontBrakeTorque;
            rearLeftWheelCollider.brakeTorque = rearBrakeTorque;
            rearRightWheelCollider.brakeTorque = rearBrakeTorque;

            ApplyWheelEdgeReleaseTuning();

            if (canAccelerate && groundedRearWheels > 0)
            {
                float assist = DirectDriveForce * throttleInput * direction * gearLoadFactor * engineTorqueFactor * torqueMultiplier * turnLoad * tractionFactor * driveShaping;
                body.AddForce(transform.forward * assist * turnKeep, ForceMode.Force);
            }

            ApplyTurnSpeedHold(gear, planarSpeed, turnAmount, direction, canAccelerate);

            if (EngineOn.Value && throttleInput < 0.05f && brakeInput < 0.05f && Mathf.Abs(targetSpeed) > 0.01f && speedKph > 3f)
            {
                float downhillSeverity = GetDownhillSeverity01();
                float engineBrake = EngineBrakeForce * GetEngineBrakeGearFactor(gear) * engineTorqueFactor;
                // Weak engine brake overall; even weaker when gravity is already pulling downhill.
                engineBrake *= Mathf.Lerp(1f, 0.45f, downhillSeverity);
                body.AddForce(-transform.forward * Mathf.Sign(signedForwardSpeed) * engineBrake, ForceMode.Force);
            }

            ApplyHillGradeLoad(gear, groundedWheels);
            ApplyGearSpeedLimit(targetSpeed, signedForwardSpeed);
            ApplyLowGearClimbAssist(gear, groundedWheels);
            ApplySlopeSideHold(groundedWheels);
            ApplyParkingBrakeHold(groundedWheels);

            if (body.linearVelocity.sqrMagnitude > 1f && groundedWheels >= 2)
            {
                body.AddForce(-transform.up * (body.linearVelocity.magnitude * Downforce), ForceMode.Acceleration);
            }

            ApplyAntiRollBars();
            ApplyBodyInertiaSway(groundedWheels);
            UpdateSlipTelemetry();
            ApplyUprightAssist(groundedWheels);
            ApplyAntiFlipSafety(groundedWheels);
            ApplyHandbrakeDriftAssist(speedKph, groundedWheels);
            ApplyNaturalCornerSlip(speedKph, groundedWheels);
            ApplyLowSpeedSteerAssist(speedKph, groundedWheels);

            AnimateWheels(steerAngle, signedForwardSpeed);
            LogVehicleDebug(gear, luggingGear, canAccelerate, targetSpeed, signedForwardSpeed, steerAngle, groundedWheels, groundedRearWheels, motorTorque, engineTorqueFactor, gearLoadFactor);
        }

private void LogVehicleDebug(MiniVanGear gear, bool luggingGear, bool canAccelerate, float targetSpeed, float signedForwardSpeed, float steerAngle, int groundedWheels, int groundedRearWheels, float motorTorque, float engineTorqueFactor, float gearLoadFactor)
        {
            if (!DebugVehicle || Time.time < nextDebugLogTime)
            {
                return;
            }

            nextDebugLogTime = Time.time + Mathf.Max(0.1f, DebugLogInterval);
            float speedKph = Mathf.Abs(signedForwardSpeed) * 3.6f;
            string wheelDebug = GetWheelDebug(FrontLeftWheel, "FL") + " | " +
                                GetWheelDebug(FrontRightWheel, "FR") + " | " +
                                GetWheelDebug(RearLeftWheel, "RL") + " | " +
                                GetWheelDebug(RearRightWheel, "RR");

            string slipState = WheelSlip.Value >= SlipWarningThreshold ? "SLIP" : "GRIP";

            Debug.Log("[MiniVanPhysics] " +
                      "gear=" + gear +
                      " engine=" + EngineOn.Value +
                      " rpm=" + engineRpm.ToString("0") +
                      " load=" + EngineLoad.Value.ToString("0.00") +
                      " throttle=" + throttleInput.ToString("0.00") +
                      " brake=" + brakeInput.ToString("0.00") +
                      " handbrake=" + handbrakeInput +
                      " steerRaw=" + steeringInput.ToString("0.00") +
                      " steerSmooth=" + smoothedSteeringInput.ToString("0.00") +
                      " steerAngle=" + steerAngle.ToString("0.0") +
                      " speed=" + speedKph.ToString("0.0") + "km/h" +
                      " target=" + (targetSpeed * 3.6f).ToString("0.0") + "km/h" +
                      " lugging=" + luggingGear +
                      " gearLoad=" + gearLoadFactor.ToString("0.00") +
                      " torqueFactor=" + engineTorqueFactor.ToString("0.00") +
                      " motorTorque=" + motorTorque.ToString("0") +
                      " canAccel=" + canAccelerate +
                      " slip=" + WheelSlip.Value.ToString("0.00") + "(" + slipState + ")" +
                      " frontSlip=" + frontSlip.ToString("0.00") +
                      " rearSlip=" + rearSlip.ToString("0.00") +
                      " grounded=" + groundedWheels + "/4" +
                      " rearGrounded=" + groundedRearWheels + "/2" +
                      " rbVel=" + body.linearVelocity.ToString("F2") +
                      " rbAng=" + body.angularVelocity.ToString("F2") +
                      " mass=" + body.mass.ToString("0") +
                      " linDamp=" + body.linearDamping.ToString("0.00") +
                      " angDamp=" + body.angularDamping.ToString("0.00") +
                      " wheelRadius=" + WheelRadius.ToString("0.00") +
                      " rest=" + SuspensionRestLength.ToString("0.00") +
                      " spring=" + SuspensionSpring.ToString("0") +
                      " damper=" + SuspensionDamper.ToString("0") +
                      " maxSusp=" + MaxSuspensionForce.ToString("0") +
                      " rearForce=" + RearDriveForce.ToString("0") +
                      " wheels=[" + wheelDebug + "]");
        }

private void MonitorVehicleAnomalies()
        {
            if (!DebugVehicleAnomalies || body == null)
            {
                lastBodyVelocity = body != null ? body.linearVelocity : Vector3.zero;
                return;
            }

            Vector3 velocity = body.linearVelocity;
            float dt = Mathf.Max(Time.fixedDeltaTime, 0.0001f);
            float acceleration = (velocity - lastBodyVelocity).magnitude / dt;
            float speedKph = velocity.magnitude * 3.6f;
            float verticalSpeed = velocity.y;
            float angularSpeed = body.angularVelocity.magnitude;
            float upDot = Vector3.Dot(transform.up, Vector3.up);
            int validGrounded = CountGroundedWheels();
            int rawGrounded = CountRawGroundedWheels();
            bool severe = speedKph > 95f || verticalSpeed > 8.5f || acceleration > 55f || angularSpeed > 3.05f || upDot < 0.15f || rawGrounded > validGrounded;

            if (severe && Time.time >= nextAnomalyLogTime)
            {
                nextAnomalyLogTime = Time.time + AnomalyLogInterval;
                Debug.LogWarning("[MiniVanPhysicsAnomaly] speed=" + speedKph.ToString("0.0") + "km/h" +
                                 " vY=" + verticalSpeed.ToString("0.00") +
                                 " accel=" + acceleration.ToString("0.0") +
                                 " ang=" + angularSpeed.ToString("0.00") +
                                 " upDot=" + upDot.ToString("0.00") +
                                 " grounded=" + validGrounded + "/4" +
                                 " rawGrounded=" + rawGrounded + "/4" +
                                 " invalidWheels=" + GetInvalidWheelSummary() +
                                 " pos=" + transform.position.ToString("F2") +
                                 " rot=" + transform.eulerAngles.ToString("F1") +
                                 " gear=" + (MiniVanGear)CurrentGear.Value +
                                 " throttle=" + throttleInput.ToString("0.00") +
                                 " brake=" + brakeInput.ToString("0.00"));
            }

            lastBodyVelocity = velocity;
        }


        private string GetWheelDebug(Transform wheel, string label)
        {
            if (wheel == null)
            {
                return label + ":null";
            }

            Vector3 wheelUp = transform.up;
            Vector3 rayOrigin = wheel.position + wheelUp * (SuspensionRestLength * 0.5f);
            float rayDistance = SuspensionRestLength + WheelRadius;

            if (!TryGetGroundHit(rayOrigin, -wheelUp, rayDistance, out RaycastHit hit))
            {
                return label + ":air pos=" + wheel.position.ToString("F2") + " ray=" + rayDistance.ToString("0.00") + " scale=" + wheel.localScale.ToString("F2");
            }

            float compression = Mathf.Clamp01((rayDistance - hit.distance) / Mathf.Max(0.01f, SuspensionRestLength));
            return label + ":hit dist=" + hit.distance.ToString("0.00") +
                   " comp=" + compression.ToString("0.00") +
                   " pos=" + wheel.position.ToString("F2") +
                   " scale=" + wheel.localScale.ToString("F2") +
                   " ground=" + hit.collider.name;
        }


private void ApplyUprightAssist(int groundedWheels)
        {
            if (body == null || groundedWheels < 2)
            {
                return;
            }

            Vector3 targetUp = Vector3.up;
            Vector3 groundNormal = GetAverageWheelGroundNormal();
            if (groundNormal.sqrMagnitude > 0.001f)
            {
                float slopeAngle = Vector3.Angle(groundNormal, Vector3.up);
                float slopeBlend = Mathf.Clamp01(Mathf.InverseLerp(5f, 62f, slopeAngle));
                targetUp = Vector3.Slerp(Vector3.up, groundNormal.normalized, slopeBlend);
            }

            Vector3 correctionAxis = Vector3.Cross(transform.up, targetUp);
            if (correctionAxis.sqrMagnitude < 0.0001f)
            {
                correctionAxis = transform.forward;
            }

            float uprightError = 1f - Mathf.Clamp01(Vector3.Dot(transform.up, targetUp));
            // Let small pitch/roll from suspension sway through; only fight larger lean.
            float effectiveError = Mathf.Max(0f, uprightError - Mathf.Max(0f, BodySwayAllowedLean));
            if (effectiveError <= 0.0001f)
            {
                return;
            }

            float groundedFactor = Mathf.Clamp01(groundedWheels / 4f);
            float speedFactor = Mathf.Lerp(1f, 0.35f, Mathf.InverseLerp(25f, 80f, body.linearVelocity.magnitude * 3.6f));
            body.AddTorque(correctionAxis.normalized * (UprightAssist * effectiveError * groundedFactor * speedFactor), ForceMode.Acceleration);
        }

        private void ApplyAntiFlipSafety(int groundedWheels)
        {
            if (body == null || groundedWheels < 3 || body.linearVelocity.magnitude > 2.8f)
            {
                return;
            }

            float upDot = Vector3.Dot(transform.up, Vector3.up);
            if (upDot > -0.28f)
            {
                return;
            }

            Vector3 correctionAxis = Vector3.Cross(transform.up, Vector3.up);
            if (correctionAxis.sqrMagnitude < 0.0001f)
            {
                correctionAxis = transform.forward;
            }

            float severity = Mathf.InverseLerp(-0.28f, -0.75f, upDot);
            body.AddTorque(correctionAxis.normalized * (UprightAssist * 1.15f * severity), ForceMode.Acceleration);
        }


private float GetTargetSpeed(MiniVanGear gear)
        {
            if (MiniVanGearUtility.IsForward(gear))
            {
                return MiniVanGearUtility.MaxForwardSpeed(gear);
            }

            if (gear == MiniVanGear.Reverse)
            {
                return -8f / 3.6f;
            }

            return 0f;
        }

        private float GetSlopeAdjustedTargetSpeed(MiniVanGear gear, float baseTargetSpeed, int groundedWheels)
        {
            // Beater rule: hills do not secretly rewrite top speed. Grade load and gear choice do.
            // Only 1st keeps a small crawl floor so steep rescue climbs remain possible.
            if (gear != MiniVanGear.First || groundedWheels < 2 || Mathf.Abs(baseTargetSpeed) <= 0.01f)
            {
                return baseTargetSpeed;
            }

            Vector3 groundNormal = GetAverageWheelGroundNormal();
            float slopeAngle = Vector3.Angle(groundNormal, Vector3.up);
            if (slopeAngle < 12f || slopeAngle > LowGearMaxSlopeAngle)
            {
                return baseTargetSpeed;
            }

            Vector3 downhill = Vector3.ProjectOnPlane(Physics.gravity, groundNormal);
            if (downhill.sqrMagnitude < 0.001f)
            {
                return baseTargetSpeed;
            }

            float uphillAlignment = Vector3.Dot(transform.forward, -downhill.normalized);
            if (uphillAlignment <= 0.15f)
            {
                return baseTargetSpeed;
            }

            return Mathf.Sign(baseTargetSpeed) * Mathf.Max(Mathf.Abs(baseTargetSpeed), 18f / 3.6f);
        }

        private float GetAccelerationForGear(MiniVanGear gear)
        {
            return gear == MiniVanGear.Reverse ? ReverseAcceleration : MotorAcceleration;
        }

        private void UpdateEngineState(MiniVanGear gear, float speedKph, float signedForwardSpeed)
        {
            if (!EngineOn.Value)
            {
                engineRpm = Mathf.MoveTowards(engineRpm, 0f, RedlineRpm * Time.fixedDeltaTime * 0.9f);
                EngineLoad.Value = 0f;
                EngineSweetSpot01.Value = 0f;
                return;
            }

            if (engineRpm < IdleRpm * 0.5f)
            {
                engineRpm = IdleRpm;
            }

            float targetRpm;
            if (gear == MiniVanGear.Neutral || gear == MiniVanGear.Park)
            {
                targetRpm = Mathf.Lerp(IdleRpm, RedlineRpm * 0.42f, throttleInput);
            }
            else
            {
                targetRpm = GetDrivenTargetRpm(gear, speedKph, throttleInput);
            }

            float rpmInertia = GetGearRpmInertia(gear, engineRpm, targetRpm);
            engineRpm = Mathf.Lerp(engineRpm, Mathf.Clamp(targetRpm, IdleRpm, RedlineRpm), Time.fixedDeltaTime * rpmInertia);
            float sweetSpot01 = EvaluateEngineSweetSpot01(engineRpm);
            // Mechanical load can be tiny at low RPM (weak torque factor) even with full throttle.
            // Keep a pedal floor so audio/fuel react as soon as the driver presses gas.
            float mechanicalLoad = throttleInput * GetEngineTorqueFactor(engineRpm) * GetGearLoadFactor(gear, speedKph);
            float load = Mathf.Clamp01(Mathf.Max(mechanicalLoad, throttleInput * 0.72f));
            EngineLoad.Value = EngineOn.Value ? load : 0f;
            EngineSweetSpot01.Value = sweetSpot01;

            if (engineRpm < StallRpm)
            {
                EngineOn.Value = false;
                throttleInput = 0f;
                EngineLoad.Value = 0f;
                EngineSweetSpot01.Value = 0f;
                if (DebugVehicle && Time.time - lastStallDebugTime > 0.5f)
                {
                    lastStallDebugTime = Time.time;
                    Debug.Log("[MiniVanPhysics] engine stalled rpm=" + engineRpm.ToString("0") + " gear=" + gear + " speed=" + speedKph.ToString("0.0") + "km/h");
                }
            }
        }

        /// <summary>
        /// Real drivetrain model: in gear, RPM rises roughly in proportion to road speed.
        /// Sweet-spot "hang time" comes from how long acceleration takes, not from faking a flat RPM curve.
        /// </summary>
        private float GetDrivenTargetRpm(MiniVanGear gear, float speedKph, float throttle)
        {
            float gearMaxKph = Mathf.Max(1f, Mathf.Abs(MiniVanGearUtility.MaxForwardSpeed(gear)) * 3.6f);
            if (gear == MiniVanGear.Reverse)
            {
                gearMaxKph = Mathf.Max(1f, Mathf.Abs(GetTargetSpeed(gear)) * 3.6f);
            }

            float gearMinKph = GetGearRpmFloorSpeedKph(gear);
            float speed = Mathf.Max(0f, Mathf.Abs(speedKph));

            // How far through this gear's speed band we are (0 at engage, 1 at gear top / shift point).
            float u = Mathf.Clamp01(Mathf.InverseLerp(gearMinKph, gearMaxKph, speed));

            // At gear top, approach shift RPM (not quite absolute redline).
            float shiftRpm01 = 0.9f;
            float rpm01;
            if (gear == MiniVanGear.First || gear == MiniVanGear.Reverse)
            {
                // 1st economy: can accelerate briskly while RPM stays low early, then climbs for the shift.
                // ~0-16 km/h → up to ~2000, ~16-28 → green, ~28-top → shift RPM.
                const float crawlEndKph = 16f;
                const float greenEndKph = 28f;
                if (speed <= crawlEndKph)
                {
                    float t = Mathf.Clamp01(speed / crawlEndKph);
                    rpm01 = Mathf.Lerp(0f, 0.22f, t * t);
                }
                else if (speed <= greenEndKph)
                {
                    float t = Mathf.InverseLerp(crawlEndKph, greenEndKph, speed);
                    rpm01 = Mathf.Lerp(0.22f, 0.5f, t);
                }
                else
                {
                    float t = Mathf.InverseLerp(greenEndKph, gearMaxKph, speed);
                    rpm01 = Mathf.Lerp(0.5f, shiftRpm01, t * t);
                }
            }
            else
            {
                // 2/3/4/5: land in green (~2500) after a correct upshift, hang there for most of
                // the gear, and only climb toward shift RPM near the top. Stops one-tap 4500 spikes.
                rpm01 = EvaluateHighGearCruiseRpm01(gear, u, shiftRpm01);
            }

            // Very low speed + clutch-like takeoff: don't spike off idle on the first centimetre.
            if (speed < 5f && (gear == MiniVanGear.First || gear == MiniVanGear.Reverse || gear == MiniVanGear.Second))
            {
                float launch = Mathf.Clamp01(speed / 5f);
                float launchFloor = gear == MiniVanGear.Second ? GetHighGearLandingRpm01(gear) : 0f;
                rpm01 = Mathf.Lerp(launchFloor, rpm01, launch * launch);
            }

            // Load sync: climbing under throttle raises RPM only a little; high gears stay lazy.
            float uphill = GetUphillSeverity01();
            float downhill = GetDownhillSeverity01();
            float loadLift = uphill * Mathf.Clamp01(throttle) * GetHighGearThrottleRpmLift(gear);
            float unloadDrop = downhill * (1f - Mathf.Clamp01(throttle)) * 0.10f;
            if (throttle < 0.08f && speed > 8f)
            {
                unloadDrop += 0.03f;
            }

            rpm01 = Mathf.Clamp01(rpm01 + loadLift - unloadDrop);

            float rpm = Mathf.Lerp(IdleRpm, RedlineRpm, rpm01);

            // Tiny pedal blip only — never enough to jump a whole band on 2nd.
            float flare = Mathf.Lerp(10f, 45f, u) * Mathf.Clamp01(throttle) * GetHighGearThrottleFlareScale(gear);
            flare *= 1f + uphill * 0.2f;
            if (gear == MiniVanGear.First || gear == MiniVanGear.Reverse)
            {
                flare *= 0.55f;
            }

            if (gear == MiniVanGear.Neutral || gear == MiniVanGear.Park)
            {
                return rpm;
            }

            return rpm + flare;
        }

        /// <summary>
        /// ~2500 RPM landing after a green upshift (Idle=850, Redline=5200 → ~0.38).
        /// </summary>
        private static float GetHighGearLandingRpm01(MiniVanGear gear)
        {
            switch (gear)
            {
                case MiniVanGear.Second:
                    return 0.38f;
                case MiniVanGear.Third:
                    return 0.39f;
                case MiniVanGear.Fourth:
                    return 0.40f;
                case MiniVanGear.Fifth:
                    return 0.40f;
                default:
                    return 0.38f;
            }
        }

        /// <summary>
        /// Soft green cruise target (~2700-3000) held through most of the gear's speed band.
        /// </summary>
        private static float GetHighGearCruiseRpm01(MiniVanGear gear)
        {
            switch (gear)
            {
                case MiniVanGear.Second:
                    return 0.46f;
                case MiniVanGear.Third:
                    return 0.48f;
                case MiniVanGear.Fourth:
                    return 0.49f;
                case MiniVanGear.Fifth:
                    return 0.50f;
                default:
                    return 0.47f;
            }
        }

        /// <summary>
        /// Fraction of the gear speed band spent hanging in the green before the late climb to shift RPM.
        /// </summary>
        private static float GetHighGearGreenHoldFraction(MiniVanGear gear)
        {
            switch (gear)
            {
                case MiniVanGear.Second:
                    return 0.78f;
                case MiniVanGear.Third:
                    return 0.72f;
                case MiniVanGear.Fourth:
                    return 0.70f;
                case MiniVanGear.Fifth:
                    return 0.68f;
                default:
                    return 0.7f;
            }
        }

        private static float EvaluateHighGearCruiseRpm01(MiniVanGear gear, float gearProgress01, float shiftRpm01)
        {
            float u = Mathf.Clamp01(gearProgress01);
            float landing = GetHighGearLandingRpm01(gear);
            float cruise = GetHighGearCruiseRpm01(gear);
            float hold = Mathf.Clamp(GetHighGearGreenHoldFraction(gear), 0.45f, 0.9f);

            if (u <= hold)
            {
                // Slow drift from ~2500 toward mid-green while accelerating through most of the gear.
                float t = Mathf.Pow(u / hold, GetHighGearGreenDriftExponent(gear));
                return Mathf.Lerp(landing, cruise, t);
            }

            // Only the last stretch climbs toward the shift point.
            float climbT = Mathf.InverseLerp(hold, 1f, u);
            climbT = climbT * climbT;
            return Mathf.Lerp(cruise, shiftRpm01, climbT);
        }

        private static float GetHighGearGreenDriftExponent(MiniVanGear gear)
        {
            switch (gear)
            {
                case MiniVanGear.Second:
                    return 1.85f;
                case MiniVanGear.Third:
                    return 1.55f;
                case MiniVanGear.Fourth:
                    return 1.45f;
                case MiniVanGear.Fifth:
                    return 1.35f;
                default:
                    return 1.5f;
            }
        }

        private static float GetHighGearThrottleRpmLift(MiniVanGear gear)
        {
            switch (gear)
            {
                case MiniVanGear.Second:
                    return 0.04f;
                case MiniVanGear.Third:
                    return 0.055f;
                case MiniVanGear.Fourth:
                    return 0.06f;
                case MiniVanGear.Fifth:
                    return 0.065f;
                default:
                    return 0.08f;
            }
        }

        private static float GetHighGearThrottleFlareScale(MiniVanGear gear)
        {
            switch (gear)
            {
                case MiniVanGear.Second:
                    return 0.35f;
                case MiniVanGear.Third:
                    return 0.45f;
                case MiniVanGear.Fourth:
                    return 0.5f;
                case MiniVanGear.Fifth:
                    return 0.55f;
                default:
                    return 1f;
            }
        }

        private float GetGearRpmInertia(MiniVanGear gear, float currentRpm, float targetRpm)
        {
            float inertia = Mathf.Max(0.5f, EngineInertia);
            bool falling = targetRpm <= currentRpm + 15f;
            if (falling)
            {
                // Ease upshift RPM drop so the tach settles near green instead of crashing.
                switch (gear)
                {
                    case MiniVanGear.Second:
                        return inertia * 0.48f;
                    case MiniVanGear.Third:
                        return inertia * 0.42f;
                    case MiniVanGear.Fourth:
                        return inertia * 0.38f;
                    case MiniVanGear.Fifth:
                        return inertia * 0.34f;
                    default:
                        return inertia;
                }
            }

            // Rising RPM is very lazy on 2nd+ so throttle doesn't teleport the tach to 4500.
            switch (gear)
            {
                case MiniVanGear.Second:
                    return inertia * 0.28f;
                case MiniVanGear.Third:
                    return inertia * 0.34f;
                case MiniVanGear.Fourth:
                    return inertia * 0.30f;
                case MiniVanGear.Fifth:
                    return inertia * 0.24f;
                default:
                    return inertia;
            }
        }

        private float GetGearRpmFloorSpeedKph(MiniVanGear gear)
        {
            switch (gear)
            {
                case MiniVanGear.First:
                case MiniVanGear.Reverse:
                    return 0f;
                case MiniVanGear.Second:
                    return SecondGearMinSpeedKph * 0.4f;
                case MiniVanGear.Third:
                    // Align RPM floor with a normal green 2→3 handoff speed.
                    return ThirdGearMinSpeedKph * 0.55f;
                case MiniVanGear.Fourth:
                    // Keep 3→4 green handoff near the sweet-spot floor.
                    return FourthGearMinSpeedKph * 0.55f;
                case MiniVanGear.Fifth:
                    return FifthGearMinSpeedKph * 0.55f;
                default:
                    return 0f;
            }
        }

        public float EvaluateEngineSweetSpot01(float rpm)
        {
            float rpm01 = Mathf.Clamp01(Mathf.InverseLerp(IdleRpm, RedlineRpm, rpm));
            float halfWidth = Mathf.Max(0.04f, SweetSpotHalfWidthRpm01);
            float distance = Mathf.Abs(rpm01 - Mathf.Clamp(SweetSpotCenterRpm01, 0.15f, 0.85f)) / halfWidth;
            // 1 at center, 0 at band edge, stays 0 outside.
            return Mathf.Clamp01(1f - distance);
        }

        private float GetEngineTorqueFactor(float rpm)
        {
            float rpm01 = Mathf.Clamp01(Mathf.InverseLerp(IdleRpm, RedlineRpm, rpm));
            float center = Mathf.Clamp(SweetSpotCenterRpm01, 0.15f, 0.85f);
            float halfWidth = Mathf.Max(0.04f, SweetSpotHalfWidthRpm01);
            float bandLow = center - halfWidth;
            float bandHigh = center + halfWidth;
            float sweet = EvaluateEngineSweetSpot01(rpm);
            float peak = 1f + Mathf.Max(0f, SweetSpotTorqueBonus);
            bool topGear = (MiniVanGear)CurrentGear.Value == MiniVanGear.Fifth;

            if (rpm01 <= bandLow)
            {
                // Lugging: weak pull that climbs toward the band.
                float t = bandLow <= 0.001f ? 1f : Mathf.Clamp01(rpm01 / bandLow);
                t = t * t * (3f - 2f * t);
                return Mathf.Lerp(Mathf.Max(0.15f, BelowSweetSpotTorqueFloor), Mathf.Lerp(0.88f, peak, 0.35f), t);
            }

            if (rpm01 <= bandHigh)
            {
                // Inside the power band — strongest near the center.
                return Mathf.Lerp(0.94f, peak, sweet * sweet);
            }

            // Past the band toward redline — beater screams and loses pull (5th included).
            float over = Mathf.Clamp01((rpm01 - bandHigh) / Mathf.Max(0.05f, 1f - bandHigh));
            over = over * over;
            float floor = Mathf.Max(0.15f, AboveSweetSpotTorqueFloor);
            if (topGear)
            {
                // Top gear still keeps a little cruise, but no free highway pull outside the band.
                floor = Mathf.Max(floor, 0.34f);
            }

            float top = topGear ? 0.88f : 0.9f;
            return Mathf.Lerp(top, floor, over);
        }

        private float GetRearTractionFactor()
        {
            if (handbrakeInput)
            {
                return 0.2f;
            }

            // Keep most drive during cornering slip — heavy cut caused near-stops in turns.
            return Mathf.Lerp(1f, 0.95f, Mathf.Clamp01(rearSlip));
        }

        private bool IsLowGear(MiniVanGear gear)
        {
            return gear == MiniVanGear.First;
        }

        private void ApplyDynamicWheelFriction(float speedKph)
        {
            SetWheelFriction(frontLeftWheelCollider, true, speedKph);
            SetWheelFriction(frontRightWheelCollider, true, speedKph);
            SetWheelFriction(rearLeftWheelCollider, false, speedKph);
            SetWheelFriction(rearRightWheelCollider, false, speedKph);
        }

        private void SetWheelFriction(WheelCollider wheelCollider, bool frontWheel, float speedKph)
        {
            if (wheelCollider == null)
            {
                return;
            }

            MiniVanGear gear = (MiniVanGear)CurrentGear.Value;
            float speedGrip = Mathf.Lerp(1f, 0.88f, Mathf.InverseLerp(35f, 95f, speedKph));
            float rearHandbrakeForwardGrip = !frontWheel && handbrakeInput ? Mathf.Lerp(1.65f, HandbrakeRearGrip, Mathf.InverseLerp(2f, 12f, speedKph)) : 1f;
            float driftFactor = 0f;
            if (!frontWheel && handbrakeInput)
            {
                driftFactor = Mathf.InverseLerp(HandbrakeDriftSteerThreshold, 1f, Mathf.Abs(smoothedSteeringInput)) *
                              Mathf.InverseLerp(HandbrakeDriftMinSpeedKph, HandbrakeDriftMinSpeedKph + 24f, speedKph);
            }

            float rearHandbrakeSideGrip = !frontWheel && handbrakeInput ? Mathf.Lerp(HandbrakeRearGrip, HandbrakeDriftRearGrip, driftFactor) : 1f;
            float lowGearGrip = IsLowGear(gear) && throttleInput > 0.05f ? 1.08f : 1f;
            bool noTurnSpeedLossGear = gear == MiniVanGear.First || gear == MiniVanGear.Reverse;
            float cornerSlipGrip = 1f;
            if (!frontWheel && !handbrakeInput && !noTurnSpeedLossGear)
            {
                float cornerAmount = Mathf.InverseLerp(CornerSlipSteerThreshold, 1f, Mathf.Abs(smoothedSteeringInput)) *
                                     Mathf.InverseLerp(CornerSlipMinSpeedKph, CornerSlipMinSpeedKph + 40f, speedKph);
                // Gas in a hard turn loosens the rear a bit more (power oversteer).
                cornerAmount *= Mathf.Lerp(0.75f, 1.15f, throttleInput);
                // Soften rear slip loss so higher gears only bleed ~25% speed in turns.
                cornerAmount *= 0.45f;
                cornerSlipGrip = Mathf.Lerp(1f, 1f - Mathf.Clamp01(CornerSlipRearGripLoss), cornerAmount);
            }

            WheelFrictionCurve forward = wheelCollider.forwardFriction;
            forward.stiffness = (frontWheel ? 1.08f : 1.45f) * rearHandbrakeForwardGrip * lowGearGrip;
            wheelCollider.forwardFriction = forward;

            WheelFrictionCurve sideways = wheelCollider.sidewaysFriction;
            // 1st/R keep full side grip so scrub doesn't dump speed/RPM.
            float steerSoftening = noTurnSpeedLossGear
                ? 1f
                : Mathf.Lerp(1f, 0.985f, Mathf.Abs(smoothedSteeringInput) * Mathf.InverseLerp(25f, 80f, speedKph));
            sideways.stiffness = (frontWheel ? FrontLateralGrip : LateralGrip) * speedGrip * rearHandbrakeSideGrip * cornerSlipGrip * Mathf.Lerp(1f, 1.18f, lowGearGrip - 1f) * steerSoftening;
            wheelCollider.sidewaysFriction = sideways;
        }

        private void ApplyAntiRollBars()
        {
            ApplyAntiRollBar(frontLeftWheelCollider, frontRightWheelCollider, AntiRollForce);
            ApplyAntiRollBar(rearLeftWheelCollider, rearRightWheelCollider, AntiRollForce * 0.82f);
        }

        private void ApplyBodyInertiaSway(int groundedWheels)
        {
            if (body == null || groundedWheels < 2)
            {
                swayVelocityInitialized = false;
                return;
            }

            float dt = Mathf.Max(Time.fixedDeltaTime, 0.0001f);
            Vector3 worldVelocity = body.linearVelocity;
            if (!swayVelocityInitialized)
            {
                swayLastWorldVelocity = worldVelocity;
                swaySmoothedWorldAccel = Vector3.zero;
                swayVelocityInitialized = true;
                return;
            }

            Vector3 worldAccel = (worldVelocity - swayLastWorldVelocity) / dt;
            swayLastWorldVelocity = worldVelocity;

            // Ignore gravity / slope component along body up so sway reacts to driving loads.
            worldAccel = Vector3.ProjectOnPlane(worldAccel, transform.up);
            float filter = 1f - Mathf.Exp(-Mathf.Max(0.1f, BodySwayAccelFilter) * dt);
            swaySmoothedWorldAccel = Vector3.Lerp(swaySmoothedWorldAccel, worldAccel, filter);

            Vector3 localAccel = transform.InverseTransformDirection(swaySmoothedWorldAccel);
            float forwardAccel = Mathf.Clamp(localAccel.z, -BodySwayMaxAccel, BodySwayMaxAccel);
            float lateralAccel = Mathf.Clamp(localAccel.x, -BodySwayMaxAccel, BodySwayMaxAccel);

            float speedKph = Vector3.ProjectOnPlane(worldVelocity, transform.up).magnitude * 3.6f;
            float accelBlend = Mathf.InverseLerp(4f, 28f, speedKph);
            float groundedFactor = Mathf.Clamp01((groundedWheels - 1) / 3f);

            // Low-speed: emphasize driver intent so takeoff / braking still "nods".
            float inputPitch = (throttleInput - brakeInput) * BodySwayInputPitch * 9.81f;
            if (handbrakeInput && brakeInput < 0.05f && speedKph > 5f)
            {
                inputPitch -= 0.35f * 9.81f;
            }

            float inputRoll = -smoothedSteeringInput * Mathf.Clamp(speedKph, 0f, 90f) * BodySwayInputRoll * 9.81f;
            float pitchAccel = Mathf.Lerp(inputPitch, forwardAccel, accelBlend);
            float rollAccel = Mathf.Lerp(inputRoll, -lateralAccel, accelBlend);

            // Weight transfer: accel → nose up; brake → nose down; turn → lean outward.
            Vector3 pitchTorque = transform.right * (pitchAccel * BodySwayPitchTorque * groundedFactor);
            Vector3 rollTorque = transform.forward * (rollAccel * BodySwayRollTorque * groundedFactor);
            body.AddTorque(pitchTorque + rollTorque, ForceMode.Acceleration);

            // Push loads into suspension corners so WheelColliders visibly compress.
            ApplySuspensionLoadTransfer(pitchAccel, rollAccel, groundedFactor);
        }

        private void ApplySuspensionLoadTransfer(float pitchAccel, float rollAccel, float groundedFactor)
        {
            if (body == null || groundedFactor <= 0.01f)
            {
                return;
            }

            // Positive pitchAccel (forward) loads rear / unloads front.
            float frontLoad = (-pitchAccel + 0f) * 180f * groundedFactor;
            float rearLoad = (pitchAccel + 0f) * 180f * groundedFactor;
            // Positive rollAccel here leans right (outside when turning left with -lateral).
            float leftLoad = rollAccel * 160f * groundedFactor;
            float rightLoad = -rollAccel * 160f * groundedFactor;

            AddWheelLoadForce(frontLeftWheelCollider, frontLoad + leftLoad);
            AddWheelLoadForce(frontRightWheelCollider, frontLoad + rightLoad);
            AddWheelLoadForce(rearLeftWheelCollider, rearLoad + leftLoad);
            AddWheelLoadForce(rearRightWheelCollider, rearLoad + rightLoad);
        }

        private void AddWheelLoadForce(WheelCollider wheelCollider, float downwardForce)
        {
            if (wheelCollider == null || Mathf.Abs(downwardForce) < 1f)
            {
                return;
            }

            if (!IsWheelOnValidGround(wheelCollider, out _))
            {
                return;
            }

            // Positive force presses the body into that corner (compresses suspension).
            body.AddForceAtPosition(-wheelCollider.transform.up * downwardForce, wheelCollider.transform.position, ForceMode.Force);
        }

private void ApplyAntiRollBar(WheelCollider leftWheel, WheelCollider rightWheel, float force)
        {
            if (leftWheel == null || rightWheel == null)
            {
                return;
            }

            bool leftGrounded = IsWheelOnValidGround(leftWheel, out WheelHit leftHit);
            bool rightGrounded = IsWheelOnValidGround(rightWheel, out WheelHit rightHit);
            float leftTravel = leftGrounded ? GetSuspensionTravel(leftWheel, leftHit) : 1f;
            float rightTravel = rightGrounded ? GetSuspensionTravel(rightWheel, rightHit) : 1f;
            float antiRoll = (leftTravel - rightTravel) * force;

            if (leftGrounded)
            {
                body.AddForceAtPosition(leftWheel.transform.up * -antiRoll, leftWheel.transform.position, ForceMode.Force);
            }

            if (rightGrounded)
            {
                body.AddForceAtPosition(rightWheel.transform.up * antiRoll, rightWheel.transform.position, ForceMode.Force);
            }
        }

        private static float GetSuspensionTravel(WheelCollider wheelCollider, WheelHit hit)
        {
            return (-wheelCollider.transform.InverseTransformPoint(hit.point).y - wheelCollider.radius) / Mathf.Max(0.01f, wheelCollider.suspensionDistance);
        }

        private void UpdateSlipTelemetry()
        {
            frontSlip = Mathf.Max(GetWheelSlip(frontLeftWheelCollider), GetWheelSlip(frontRightWheelCollider));
            rearSlip = Mathf.Max(GetWheelSlip(rearLeftWheelCollider), GetWheelSlip(rearRightWheelCollider));
            WheelSlip.Value = Mathf.Max(frontSlip, rearSlip);
        }

private float GetWheelSlip(WheelCollider wheelCollider)
        {
            if (!IsWheelOnValidGround(wheelCollider, out WheelHit hit))
            {
                return 0f;
            }

            return Mathf.Clamp01(Mathf.Abs(hit.sidewaysSlip) + Mathf.Abs(hit.forwardSlip) * 0.55f);
        }

private bool SimulateWheel(Transform wheel, float steerAngle, bool driveWheel, bool canAccelerate, float targetSpeed, ref int groundedRearWheels, ref Vector3 rearDriveDirection)
        {
            if (wheel == null)
            {
                return false;
            }

            Vector3 wheelUp = transform.up;
            Vector3 rayOrigin = wheel.position + wheelUp * (SuspensionRestLength * 0.5f);
            float rayDistance = SuspensionRestLength + WheelRadius;

            if (!TryGetGroundHit(rayOrigin, -wheelUp, rayDistance, out RaycastHit hit))
            {
                return false;
            }

            Vector3 pointVelocity = body.GetPointVelocity(wheel.position);
            float compression = Mathf.Clamp01((rayDistance - hit.distance) / SuspensionRestLength);
            float springForce = compression * SuspensionSpring;
            float damperForce = -Vector3.Dot(pointVelocity, wheelUp) * SuspensionDamper;
            float suspensionForce = Mathf.Clamp(springForce + damperForce, 0f, MaxSuspensionForce);
            body.AddForceAtPosition(wheelUp * suspensionForce, wheel.position, ForceMode.Force);

            Vector3 tireForward = Quaternion.AngleAxis(steerAngle, wheelUp) * transform.forward;
            tireForward = Vector3.ProjectOnPlane(tireForward, hit.normal).normalized;

            if (tireForward.sqrMagnitude < 0.01f)
            {
                tireForward = transform.forward;
            }

            Vector3 tireSide = Vector3.Cross(hit.normal, tireForward).normalized;
            float lateralVelocity = Vector3.Dot(pointVelocity, tireSide);
            float grip = Mathf.Abs(steerAngle) > 0.1f ? FrontLateralGrip : LateralGrip;
            float lateralForce = Mathf.Clamp(lateralVelocity * grip * body.mass, -MaxLateralForce, MaxLateralForce);
            body.AddForceAtPosition(-tireSide * lateralForce, wheel.position, ForceMode.Force);

            float forwardVelocity = Vector3.Dot(pointVelocity, tireForward);
            float brakeAmount = handbrakeInput && driveWheel ? 1f : brakeInput;

            if (brakeAmount > 0.01f || CurrentGear.Value == (int)MiniVanGear.Park)
            {
                float brake = (handbrakeInput && driveWheel ? HandbrakeStrength : BrakeForce) * Mathf.Max(brakeAmount, CurrentGear.Value == (int)MiniVanGear.Park ? 1f : 0f);
                float brakeDirection = Mathf.Abs(forwardVelocity) > 0.05f ? Mathf.Sign(forwardVelocity) : Mathf.Sign(Vector3.Dot(body.linearVelocity, tireForward));
                body.AddForceAtPosition(-tireForward * brakeDirection * brake, wheel.position, ForceMode.Force);
            }

            if (driveWheel)
            {
                groundedRearWheels++;

                if (canAccelerate)
                {
                    rearDriveDirection += tireForward * Mathf.Sign(targetSpeed);
                }
            }

            return true;
        }

private bool TryGetGroundHit(Vector3 origin, Vector3 direction, float distance, out RaycastHit bestHit)
        {
            RaycastHit[] hits = Physics.RaycastAll(origin, direction, distance, ~0, QueryTriggerInteraction.Ignore);
            bestHit = default;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].collider == null || hits[i].collider.transform.IsChildOf(transform))
                {
                    continue;
                }

                if (Vector3.Dot(hits[i].normal, Vector3.up) < 0.18f)
                {
                    continue;
                }

                if (hits[i].distance < bestDistance)
                {
                    bestDistance = hits[i].distance;
                    bestHit = hits[i];
                }
            }

            return bestDistance < float.MaxValue;
        }

private void AnimateWheels(float steerAngle, float signedForwardSpeed)
        {
            smoothedWheelSteerAngle = Mathf.Lerp(smoothedWheelSteerAngle, steerAngle, Time.deltaTime * 18f);
            smoothedWheelSpeed = Mathf.Lerp(smoothedWheelSpeed, signedForwardSpeed, Time.deltaTime * 14f);
            ApplyWheelColliderVisual(frontLeftWheelCollider, FrontLeftWheel, true);
            ApplyWheelColliderVisual(frontRightWheelCollider, FrontRightWheel, true);
            ApplyWheelColliderVisual(rearLeftWheelCollider, RearLeftWheel, false);
            ApplyWheelColliderVisual(rearRightWheelCollider, RearRightWheel, false);
            ApplySteeringWheelVisual();
        }

private void AnimateRemoteWheels(float steerAngle, float steering, float signedForwardSpeed)
        {
            smoothedWheelSteerAngle = Mathf.Lerp(smoothedWheelSteerAngle, steerAngle, Time.deltaTime * 16f);
            smoothedWheelSpeed = Mathf.Lerp(smoothedWheelSpeed, signedForwardSpeed, Time.deltaTime * 10f);

            float radius = Mathf.Max(0.05f, WheelRadius);
            wheelSpin += smoothedWheelSpeed / radius * Mathf.Rad2Deg * Time.deltaTime;

            ApplyWheelVisual(FrontLeftWheel, smoothedWheelSteerAngle);
            ApplyWheelVisual(FrontRightWheel, smoothedWheelSteerAngle);
            ApplyWheelVisual(RearLeftWheel, 0f);
            ApplyWheelVisual(RearRightWheel, 0f);
            ApplySteeringWheelVisual(steering);
        }


private void ApplyWheelColliderVisual(WheelCollider wheelCollider, Transform wheelVisual, bool steeringWheel)
        {
            if (wheelCollider == null || wheelVisual == null)
            {
                return;
            }

            int wheelIndex = GetWheelIndex(wheelVisual);
            if (wheelIndex >= 0 && DetachedWheelIndex.Value == wheelIndex)
            {
                wheelVisual.gameObject.SetActive(false);
                return;
            }
            if (!wheelVisual.gameObject.activeSelf)
            {
                wheelVisual.gameObject.SetActive(true);
            }

            wheelCollider.GetWorldPose(out Vector3 position, out Quaternion rotation);
            if (!steeringWheel)
            {
                position -= wheelCollider.transform.up * RearWheelVisualSurfaceOffset;
            }

            wheelVisual.position = Vector3.Lerp(wheelVisual.position, position, Time.deltaTime * 22f);
            wheelVisual.rotation = Quaternion.Slerp(wheelVisual.rotation, rotation * Quaternion.Euler(0f, 0f, 90f), Time.deltaTime * 22f);
        }

private void ApplySteeringWheelVisual()
        {
            ApplySteeringWheelVisual(steeringInput);
        }

private void UpdateEngineStatusLight()
        {
            if (EngineStatusLight == null)
            {
                return;
            }

            if (engineStatusLightRenderer == null)
            {
                engineStatusLightRenderer = EngineStatusLight.GetComponent<Renderer>();
                if (engineStatusLightRenderer != null)
                {
                    engineStatusLightMaterial = engineStatusLightRenderer.material;
                }
            }

            if (engineStatusLightMaterial == null)
            {
                return;
            }

            Color color;
            if (!HasEffectiveCarBatteryInstalled())
            {
                // Unpowered dash — lamp stays dead gray until AKB is in.
                color = new Color(0.14f, 0.14f, 0.145f, 1f);
            }
            else if (!EngineOn.Value)
            {
                color = new Color(1f, 0.04f, 0.03f, 1f);
            }
            else if (EngineSweetSpot01.Value > 0.55f && EngineLoad.Value > 0.2f)
            {
                // Warm amber pulse while pulling in the power band.
                float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * 9f);
                color = Color.Lerp(new Color(0.15f, 0.95f, 0.2f, 1f), new Color(1f, 0.78f, 0.12f, 1f), EngineSweetSpot01.Value * pulse);
            }
            else
            {
                color = new Color(0.05f, 1f, 0.18f, 1f);
            }

            engineStatusLightMaterial.color = color;
            if (engineStatusLightMaterial.HasProperty("_BaseColor"))
            {
                engineStatusLightMaterial.SetColor("_BaseColor", color);
            }
            if (engineStatusLightMaterial.HasProperty("_EmissionColor"))
            {
                engineStatusLightMaterial.EnableKeyword("_EMISSION");
                float emission = HasEffectiveCarBatteryInstalled() ? 1.7f : 0.05f;
                engineStatusLightMaterial.SetColor("_EmissionColor", color * emission);
            }
        }

        private void ApplyHandbrakeLeverVisual()
        {
            if (HandbrakeLever == null)
            {
                return;
            }

            bool locked = HandbrakeLocked.Value;
            HandbrakeLever.localPosition = locked ? HandbrakeLeverOnLocalPosition : HandbrakeLeverOffLocalPosition;
            Vector3 localEulerAngles = HandbrakeLever.localEulerAngles;
            localEulerAngles.x = locked ? HandbrakeLeverOnX : HandbrakeLeverOffX;
            localEulerAngles.y = 0f;
            localEulerAngles.z = 0f;
            HandbrakeLever.localEulerAngles = localEulerAngles;
        }

        private void EnsureGearLeverVisuals()
        {
            if (GearLever == null)
            {
                GearLever = FindChildByName("GearLeverTuber");
            }

            if (GearLeverGate == null)
            {
                GearLeverGate = transform.Find("GearLevelerBox");
                if (GearLeverGate == null)
                {
                    GearLeverGate = FindChildByName("GearLevelerBox");
                }
            }

            if (GearLever == null || GearLeverGate == null)
            {
                gearLeverVisualReady = false;
                return;
            }

            if (!gearLeverVisualReady)
            {
                gearLeverRestLocalY = GearLever.localPosition.y;
            }

            gearLeverPos1P = gearLeverPos1P != null ? gearLeverPos1P : GearLeverGate.Find("1P");
            gearLeverPos2P = gearLeverPos2P != null ? gearLeverPos2P : GearLeverGate.Find("2P");
            gearLeverPos3P = gearLeverPos3P != null ? gearLeverPos3P : GearLeverGate.Find("3P");
            gearLeverPos4P = gearLeverPos4P != null ? gearLeverPos4P : GearLeverGate.Find("4P");
            gearLeverPos5P = gearLeverPos5P != null ? gearLeverPos5P : GearLeverGate.Find("5P");
            gearLeverPosNP = gearLeverPosNP != null ? gearLeverPosNP : GearLeverGate.Find("NP");
            gearLeverPosRP = gearLeverPosRP != null ? gearLeverPosRP : GearLeverGate.Find("RP");

            gearLeverVisualReady = gearLeverPosNP != null ||
                                   gearLeverPos1P != null ||
                                   gearLeverPos2P != null ||
                                   gearLeverPos3P != null ||
                                   gearLeverPos4P != null ||
                                   gearLeverPos5P != null ||
                                   gearLeverPosRP != null;
        }

        private Transform GetGearLeverSlot(MiniVanGear gear)
        {
            switch (gear)
            {
                case MiniVanGear.First:
                    return gearLeverPos1P;
                case MiniVanGear.Second:
                    return gearLeverPos2P;
                case MiniVanGear.Third:
                    return gearLeverPos3P;
                case MiniVanGear.Fourth:
                    return gearLeverPos4P;
                case MiniVanGear.Fifth:
                    return gearLeverPos5P;
                case MiniVanGear.Reverse:
                    return gearLeverPosRP;
                case MiniVanGear.Park:
                case MiniVanGear.Neutral:
                default:
                    return gearLeverPosNP;
            }
        }

        private Vector3 GetGearLeverLocalFromSlot(Transform slot)
        {
            Vector3 slotInVehicle = transform.InverseTransformPoint(slot.position);
            return new Vector3(slotInVehicle.x, gearLeverRestLocalY, slotInVehicle.z);
        }

        private void RebuildGearLeverPath(MiniVanGear gear)
        {
            gearLeverPath.Clear();
            gearLeverPathIndex = 0;

            Transform slot = GetGearLeverSlot(gear);
            if (slot == null)
            {
                slot = gearLeverPosNP;
            }

            if (slot == null || GearLever == null)
            {
                return;
            }

            Vector3 from = GearLever.localPosition;
            from.y = gearLeverRestLocalY;
            Vector3 to = GetGearLeverLocalFromSlot(slot);
            Vector3 neutral = gearLeverPosNP != null
                ? GetGearLeverLocalFromSlot(gearLeverPosNP)
                : new Vector3(Mathf.Lerp(from.x, to.x, 0.5f), gearLeverRestLocalY, from.z);

            const float axisEps = 0.012f;
            if ((to - from).sqrMagnitude <= axisEps * axisEps)
            {
                return;
            }

            bool sameColumn = Mathf.Abs(from.x - to.x) <= axisEps;
            if (sameColumn)
            {
                // 1↔2 / 4↔3 / 5↔R: only vertical travel in the gate.
                gearLeverPath.Add(to);
                return;
            }

            // H-pattern: vertical to neutral rail → horizontal along N → vertical into gear.
            Vector3 toNeutralRail = new Vector3(from.x, gearLeverRestLocalY, neutral.z);
            Vector3 alongNeutral = new Vector3(to.x, gearLeverRestLocalY, neutral.z);

            if ((toNeutralRail - from).sqrMagnitude > axisEps * axisEps)
            {
                gearLeverPath.Add(toNeutralRail);
            }

            if ((alongNeutral - toNeutralRail).sqrMagnitude > axisEps * axisEps)
            {
                gearLeverPath.Add(alongNeutral);
            }

            if ((to - alongNeutral).sqrMagnitude > axisEps * axisEps)
            {
                gearLeverPath.Add(to);
            }
        }

        private void ApplyGearLeverVisual()
        {
            if (!gearLeverVisualReady)
            {
                EnsureGearLeverVisuals();
            }

            if (!gearLeverVisualReady || GearLever == null)
            {
                return;
            }

            MiniVanGear gear = (MiniVanGear)CurrentGear.Value;
            if (gear != gearLeverVisualTargetGear)
            {
                gearLeverVisualTargetGear = gear;
                RebuildGearLeverPath(gear);
            }

            if (gearLeverPath.Count == 0 || gearLeverPathIndex >= gearLeverPath.Count)
            {
                Transform slot = GetGearLeverSlot(gear) ?? gearLeverPosNP;
                if (slot != null)
                {
                    GearLever.localPosition = GetGearLeverLocalFromSlot(slot);
                }

                return;
            }

            // Path points are axis-aligned (H-gate), so MoveTowards never cuts a diagonal.
            float step = Mathf.Max(0.5f, GearLeverVisualMoveSpeed) * Mathf.Max(Time.deltaTime, 0f);
            Vector3 waypoint = gearLeverPath[gearLeverPathIndex];
            waypoint.y = gearLeverRestLocalY;
            Vector3 current = GearLever.localPosition;
            current.y = gearLeverRestLocalY;

            GearLever.localPosition = Vector3.MoveTowards(current, waypoint, step);
            if ((GearLever.localPosition - waypoint).sqrMagnitude <= 0.00005f)
            {
                GearLever.localPosition = waypoint;
                gearLeverPathIndex++;
            }
        }

        private void EnsureCarBatteryAndHoodSystem()
        {
            CarBatteryPlacementPoint = CarBatteryPlacementPoint != null
                ? CarBatteryPlacementPoint
                : FindFirstChildByName(new[] { "Akb_placementPoint", "AKB_placementPoint", "AkbPlacementPoint", "BatteryPlacementPoint" });
            FrontCapot = FrontCapot != null
                ? FrontCapot
                : FindFirstChildByName(new[] { "FrontCapot", "Front Capot", "Capot", "Hood", "Bonnet" });

            if (CarBatteryPlacementPoint != null)
            {
                carBatteryReceiver = CarBatteryPlacementPoint.GetComponent<MiniVanCarBatteryReceiver>();
                if (carBatteryReceiver == null)
                {
                    carBatteryReceiver = CarBatteryPlacementPoint.gameObject.AddComponent<MiniVanCarBatteryReceiver>();
                }

                carBatteryReceiver.Vehicle = this;
                carBatteryReceiver.PlacementPoint = CarBatteryPlacementPoint;
            }

            if (FrontCapot != null)
            {
                MiniVanVehicleHood hood = FrontCapot.GetComponent<MiniVanVehicleHood>();
                if (hood == null)
                {
                    hood = FrontCapot.gameObject.AddComponent<MiniVanVehicleHood>();
                }

                hood.Vehicle = this;
                hood.ClosedX = 90f;
                hood.OpenX = -47.3f;
                hood.LocalHingeOffset = new Vector3(0f, 0f, -0.5f);
                hood.EnsureSetup();
            }

            if (IsSpawned && IsServer && (carBatteryReceiver == null || !carBatteryReceiver.HasBattery))
            {
                SetCarBatteryInstalledOnAuthority(false, 0f);
            }

            ApplyHandbrakeLeverVisual();
        }

        private void UpdateCarBatteryCharge()
        {
            if (IsSpawned && !IsServer)
            {
                return;
            }

            if (!HasEffectiveCarBatteryInstalled())
            {
                if (EngineOn.Value)
                {
                    EngineOn.Value = false;
                    EngineLoad.Value = 0f;
                    throttleInput = 0f;
                }
                return;
            }

            float ratePerMinute = 0f;
            float planarSpeed = body != null ? Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up).magnitude : 0f;
            if (EngineOn.Value)
            {
                ratePerMinute += planarSpeed > 0.65f
                    ? GetCarBatteryDrivingChargePerMinute()
                    : Mathf.Max(0f, CarBatteryChargePerMinuteIdle);
            }
            else
            {
                ratePerMinute -= Mathf.Max(0f, CarBatteryDischargePerMinuteEngineOff);
            }

            if (IsDrivingThroughWater())
            {
                ratePerMinute -= Mathf.Max(0f, CarBatteryDischargePerMinuteInWater) * damWaterBatteryDrainMultiplier;
            }


            if (Mathf.Abs(ratePerMinute) > 0.000001f)
            {
                SetEffectiveCarBatteryCharge01(GetEffectiveCarBatteryCharge01() + ratePerMinute * Time.fixedDeltaTime / 60f);
            }

            if (installedCarBattery != null)
            {
                installedCarBattery.Charge01 = GetEffectiveCarBatteryCharge01();
            }

            if (GetEffectiveCarBatteryCharge01() <= 0.001f && EngineOn.Value)
            {
                SetEffectiveCarBatteryCharge01(0f);
                EngineOn.Value = false;
                EngineLoad.Value = 0f;
                throttleInput = 0f;
            }
        }

        private float GetCarBatteryDrivingChargePerMinute()
        {
            switch ((MiniVanGear)CurrentGear.Value)
            {
                case MiniVanGear.First:
                    return Mathf.Max(0f, CarBatteryChargePerMinuteFirstGear);
                case MiniVanGear.Second:
                    return Mathf.Max(0f, CarBatteryChargePerMinuteSecondGear);
                case MiniVanGear.Third:
                    return Mathf.Max(0f, CarBatteryChargePerMinuteThirdGear);
                case MiniVanGear.Fourth:
                    return Mathf.Max(0f, CarBatteryChargePerMinuteFourthGear);
                case MiniVanGear.Fifth:
                    return Mathf.Max(0f, CarBatteryChargePerMinuteFifthGear);
                case MiniVanGear.Reverse:
                    return Mathf.Max(0f, CarBatteryChargePerMinuteFirstGear);
                default:
                    return Mathf.Max(0f, CarBatteryChargePerMinuteDriving);
            }
        }

        private void UpdateEngineTemperature()
        {
            if (IsSpawned && !IsServer)
            {
                return;
            }

            float maxTemperature = Mathf.Max(1f, EngineTemperatureMaxC);
            float current = Mathf.Clamp(EngineTemperatureC.Value, 0f, maxTemperature);

            // While burning, keep the engine pinned at max heat until the fire ends or is put out.
            if (EngineOnFire.Value)
            {
                if (current < maxTemperature - 0.001f)
                {
                    EngineTemperatureC.Value = maxTemperature;
                }

                if (EngineOn.Value)
                {
                    StallEngineFromOverheat();
                }

                return;
            }

            float deltaPerSecond;
            float planarSpeed = body != null ? Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up).magnitude : 0f;
            bool moving = planarSpeed > 0.65f;
            bool hoodOpen = FrontCapotOpen.Value;

            if (!EngineOn.Value)
            {
                if (hoodOpen)
                {
                    // Open hood, engine off: 130→50 in ~10s (8°/s), then 3°/s.
                    float hotRate = Mathf.Max(0.01f, EngineOffHoodOpenHotCoolDegreesPerSecond);
                    float coldRate = Mathf.Max(0.01f, EngineOffHoodOpenColdCoolDegreesPerSecond);
                    float threshold = Mathf.Clamp(EngineOffHoodOpenCoolThresholdC, 0f, maxTemperature);
                    deltaPerSecond = current > threshold ? -hotRate : -coldRate;
                }
                else
                {
                    deltaPerSecond = -Mathf.Max(0f, EngineOffCoolDegreesPerSecond);
                }
            }
            else if (!moving)
            {
                deltaPerSecond = -Mathf.Max(0f, EngineIdleCoolDegreesPerSecond);
                if (hoodOpen)
                {
                    deltaPerSecond *= Mathf.Max(1f, EngineOnHoodOpenCoolMultiplier);
                }
            }
            else
            {
                deltaPerSecond = GetEngineHeatDegreesPerSecond((MiniVanGear)CurrentGear.Value);
                // RPM heat multipliers only on steep climbs (> EngineSteepUphillAngle, default 35°).
                if (deltaPerSecond > 0f && IsDrivingSteepUphill())
                {
                    deltaPerSecond *= GetClimbRpmHeatMultiplier(engineRpm);
                }

                if (hoodOpen)
                {
                    if (deltaPerSecond > 0f)
                    {
                        deltaPerSecond *= Mathf.Clamp(EngineOnHoodOpenHeatMultiplier, 0.1f, 1f);
                    }
                    else
                    {
                        deltaPerSecond *= Mathf.Max(1f, EngineOnHoodOpenCoolMultiplier);
                    }
                }
            }

            float next = Mathf.Clamp(current + deltaPerSecond * Time.fixedDeltaTime, 0f, maxTemperature);
            if (Mathf.Abs(next - current) > 0.0001f)
            {
                EngineTemperatureC.Value = next;
            }

            // Hard overheat: stall at max temperature and ignite. Restart only after cooling below restart threshold.
            if (EngineOn.Value && next >= maxTemperature - 0.001f)
            {
                StallEngineFromOverheat();
            }
        }

        private void UpdateEngineFire()
        {
            if (IsSpawned && !IsServer)
            {
                return;
            }

            if (!EngineOnFire.Value)
            {
                return;
            }

            float duration = Mathf.Max(1f, EngineFireDurationSeconds);
            float totalDamage = Mathf.Max(1f, MaxHealth) * Mathf.Clamp01(EngineFireMaxHealthFraction);
            float dt = Time.fixedDeltaTime;
            engineFireElapsed += dt;

            float targetDamage = totalDamage * Mathf.Clamp01(engineFireElapsed / duration);
            float step = Mathf.Max(0f, targetDamage - engineFireDamageDealt);
            if (step > 0.0001f)
            {
                engineFireDamageDealt += step;
                ApplyVehicleDamage(step, "engine_fire");
            }

            if (engineFireElapsed >= duration)
            {
                ExtinguishEngineFire(keepTemperaturePinned: true);
            }
        }

        private bool IsEngineTooHotToStart()
        {
            float restartBelow = Mathf.Min(EngineOverheatRestartBelowC, EngineTemperatureMaxC);
            return EngineTemperatureC.Value >= restartBelow;
        }

        private void StallEngineFromOverheat()
        {
            if (EngineOn.Value)
            {
                EngineOn.Value = false;
                EngineLoad.Value = 0f;
                throttleInput = 0f;
                engineRpm = 0f;
                EngineRpm.Value = 0f;
            }

            StartEngineFire();
        }

        private void StartEngineFire()
        {
            if (IsSpawned && !IsServer)
            {
                return;
            }

            if (EngineOnFire.Value)
            {
                return;
            }

            float maxTemperature = Mathf.Max(1f, EngineTemperatureMaxC);
            EngineTemperatureC.Value = maxTemperature;
            EngineOnFire.Value = true;
            engineFireElapsed = 0f;
            engineFireDamageDealt = 0f;
        }

        private void ExtinguishEngineFire(bool keepTemperaturePinned)
        {
            if (IsSpawned && !IsServer)
            {
                return;
            }

            if (!EngineOnFire.Value && engineFireElapsed <= 0f && engineFireDamageDealt <= 0f)
            {
                return;
            }

            EngineOnFire.Value = false;
            engineFireElapsed = 0f;
            engineFireDamageDealt = 0f;

            if (keepTemperaturePinned)
            {
                EngineTemperatureC.Value = Mathf.Max(1f, EngineTemperatureMaxC);
            }
        }

        /// <summary>
        /// Aim point for extinguisher spray / cabin cooling.
        /// </summary>
        public Vector3 GetEngineCoolWorldPoint()
        {
            Transform anchor = EngineTemperatureAnchor != null
                ? EngineTemperatureAnchor
                : FindChildByName("engine");
            if (anchor != null)
            {
                return anchor.position;
            }

            // Fallback: slightly forward of van center (engine bay).
            return transform.TransformPoint(new Vector3(0f, 0.55f, 1.35f));
        }

        /// <summary>
        /// Server-side extinguisher cooling. Puts out engine fire and drops temperature.
        /// Returns true when any effect was applied.
        /// </summary>
        public bool ServerApplyExtinguisherCool(float degrees)
        {
            if (IsSpawned && !IsServer)
            {
                return false;
            }

            float cool = Mathf.Max(0f, degrees);
            if (cool <= 0.0001f)
            {
                return false;
            }

            bool changed = false;
            if (EngineOnFire.Value)
            {
                ExtinguishEngineFire(keepTemperaturePinned: false);
                changed = true;
            }

            float maxTemperature = Mathf.Max(1f, EngineTemperatureMaxC);
            float current = Mathf.Clamp(EngineTemperatureC.Value, 0f, maxTemperature);
            float next = Mathf.Clamp(current - cool, 0f, maxTemperature);
            if (Mathf.Abs(next - current) > 0.0001f)
            {
                EngineTemperatureC.Value = next;
                changed = true;
            }

            return changed;
        }

        private float GetEngineHeatDegreesPerSecond(MiniVanGear gear)
        {
            switch (gear)
            {
                case MiniVanGear.First:
                    return Mathf.Max(0f, IsDrivingSteepUphill()
                        ? EngineFirstGearSteepHeatDegreesPerSecond
                        : EngineFirstGearHeatDegreesPerSecond);
                case MiniVanGear.Second:
                    return Mathf.Max(0f, EngineSecondGearHeatDegreesPerSecond);
                case MiniVanGear.Third:
                    return Mathf.Max(0f, EngineThirdGearHeatDegreesPerSecond);
                case MiniVanGear.Fourth:
                    return Mathf.Max(0f, EngineFourthGearHeatDegreesPerSecond);
                case MiniVanGear.Fifth:
                    return Mathf.Max(0f, EngineFifthGearHeatDegreesPerSecond);
                case MiniVanGear.Reverse:
                    return Mathf.Max(0f, EngineFirstGearHeatDegreesPerSecond);
                default:
                    return -Mathf.Max(0f, EngineIdleCoolDegreesPerSecond);
            }
        }

        /// <summary>
        /// Steep-climb only: stepped RPM heat multipliers (default 3000→×2, 4000→×2.5, 5000→×3).
        /// </summary>
        private float GetClimbRpmHeatMultiplier(float rpm)
        {
            if (rpm >= EngineClimbHeatRpmTier3)
            {
                return Mathf.Max(1f, EngineClimbHeatMultiplierTier3);
            }

            if (rpm >= EngineClimbHeatRpmTier2)
            {
                return Mathf.Max(1f, EngineClimbHeatMultiplierTier2);
            }

            if (rpm >= EngineClimbHeatRpmTier1)
            {
                return Mathf.Max(1f, EngineClimbHeatMultiplierTier1);
            }

            return 1f;
        }

        private bool IsDrivingSteepUphill()
        {
            Vector3 groundNormal = GetAverageWheelGroundNormal();
            float slopeAngle = Vector3.Angle(groundNormal, Vector3.up);
            if (slopeAngle <= Mathf.Max(0f, EngineSteepUphillAngle))
            {
                return false;
            }

            Vector3 downhill = Vector3.ProjectOnPlane(Vector3.down, groundNormal);
            if (downhill.sqrMagnitude <= 0.001f)
            {
                return false;
            }

            Vector3 uphill = -downhill.normalized;
            Vector3 forwardOnSlope = Vector3.ProjectOnPlane(transform.forward, groundNormal);
            return forwardOnSlope.sqrMagnitude > 0.001f &&
                   Vector3.Dot(forwardOnSlope.normalized, uphill) > 0.25f;
        }

        private void UpdateEngineSmokeVisual()
        {
            // Fire replaces smoke while the engine bay is burning.
            if (EngineOnFire.Value)
            {
                SetEngineSmokeActive(false);
                return;
            }

            bool shouldSmoke = EngineTemperatureC.Value >= EngineSmokeTemperatureC;
            if (!shouldSmoke)
            {
                SetEngineSmokeActive(false);
                return;
            }

            EnsureEngineSmokeInstance();
            SetEngineSmokeActive(true);
        }

        private void UpdateEngineFireVisual()
        {
            if (!EngineOnFire.Value)
            {
                SetEngineFireActive(false);
                return;
            }

            EnsureEngineFireInstance();
            SetEngineFireActive(true);
        }

        private Transform GetEngineVfxAnchor()
        {
            Transform anchor = EngineTemperatureAnchor != null
                ? EngineTemperatureAnchor
                : FindChildByName("engine");
            return anchor != null ? anchor : transform;
        }

        private void EnsureEngineSmokeInstance()
        {
            if (engineSmokeInstance != null || EngineSmokePrefab == null)
            {
                return;
            }

            Transform anchor = GetEngineVfxAnchor();
            engineSmokeInstance = Instantiate(EngineSmokePrefab, anchor);
            engineSmokeInstance.name = "Engine Overheat Smoke";
            engineSmokeInstance.transform.localPosition = Vector3.zero;
            engineSmokeInstance.transform.localRotation = Quaternion.identity;
            engineSmokeInstance.transform.localScale = Vector3.one;
            engineSmokeParticles = engineSmokeInstance.GetComponentsInChildren<ParticleSystem>(true);
            CaptureEngineSmokeEmissionRates();
            engineSmokeInstance.SetActive(false);
        }

        private void EnsureEngineFireInstance()
        {
            if (engineFireInstance != null || EngineFirePrefab == null)
            {
                return;
            }

            Transform anchor = GetEngineVfxAnchor();
            engineFireInstance = Instantiate(EngineFirePrefab, anchor);
            engineFireInstance.name = "Engine Overheat Fire";
            engineFireInstance.transform.localPosition = Vector3.zero;
            engineFireInstance.transform.localRotation = Quaternion.identity;
            engineFireInstance.transform.localScale = Vector3.one;
            engineFireParticles = engineFireInstance.GetComponentsInChildren<ParticleSystem>(true);
            engineFireInstance.SetActive(false);
        }

        private void CaptureEngineSmokeEmissionRates()
        {
            if (engineSmokeParticles == null)
            {
                return;
            }

            engineSmokeOriginalRates = new ParticleSystem.MinMaxCurve[engineSmokeParticles.Length];
            for (int i = 0; i < engineSmokeParticles.Length; i++)
            {
                ParticleSystem particles = engineSmokeParticles[i];
                if (particles != null)
                {
                    engineSmokeOriginalRates[i] = particles.emission.rateOverTime;
                }
            }
        }

        private void SetEngineSmokeActive(bool active)
        {
            if (engineSmokeInstance == null)
            {
                return;
            }

            if (active && !engineSmokeInstance.activeSelf)
            {
                engineSmokeInstance.SetActive(true);
            }

            if (engineSmokeParticles == null)
            {
                engineSmokeParticles = engineSmokeInstance.GetComponentsInChildren<ParticleSystem>(true);
                CaptureEngineSmokeEmissionRates();
            }

            if (engineSmokeOriginalRates == null || engineSmokeOriginalRates.Length != engineSmokeParticles.Length)
            {
                CaptureEngineSmokeEmissionRates();
            }

            bool anyAlive = false;
            for (int i = 0; i < engineSmokeParticles.Length; i++)
            {
                ParticleSystem particles = engineSmokeParticles[i];
                if (particles == null)
                {
                    continue;
                }

                ParticleSystem.EmissionModule emission = particles.emission;
                if (active)
                {
                    if (engineSmokeOriginalRates != null && i < engineSmokeOriginalRates.Length)
                    {
                        emission.rateOverTime = engineSmokeOriginalRates[i];
                    }

                    if (!particles.isPlaying)
                    {
                        particles.Play(true);
                    }
                }
                else
                {
                    emission.rateOverTime = 0f;
                    if (particles.isPlaying)
                    {
                        particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                    }

                    anyAlive |= particles.IsAlive(true);
                }
            }

            if (!active && !anyAlive)
            {
                for (int i = 0; i < engineSmokeParticles.Length; i++)
                {
                    ParticleSystem particles = engineSmokeParticles[i];
                    if (particles != null)
                    {
                        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    }
                }

                engineSmokeInstance.SetActive(false);
            }
        }

        private void SetEngineFireActive(bool active)
        {
            if (engineFireInstance == null)
            {
                return;
            }

            if (active && !engineFireInstance.activeSelf)
            {
                engineFireInstance.SetActive(true);
            }

            if (engineFireParticles == null)
            {
                engineFireParticles = engineFireInstance.GetComponentsInChildren<ParticleSystem>(true);
            }

            bool anyAlive = false;
            for (int i = 0; i < engineFireParticles.Length; i++)
            {
                ParticleSystem particles = engineFireParticles[i];
                if (particles == null)
                {
                    continue;
                }

                if (active)
                {
                    if (!particles.isPlaying)
                    {
                        particles.Play(true);
                    }
                }
                else if (particles.isPlaying)
                {
                    particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                    anyAlive |= particles.IsAlive(true);
                }
                else
                {
                    anyAlive |= particles.IsAlive(true);
                }
            }

            if (!active && !anyAlive)
            {
                for (int i = 0; i < engineFireParticles.Length; i++)
                {
                    ParticleSystem particles = engineFireParticles[i];
                    if (particles != null)
                    {
                        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    }
                }

                engineFireInstance.SetActive(false);
            }
        }

        private bool IsDrivingThroughWater()
        {
            return damWaterSlowDivisor > 1f;
        }

        public void SetDamWaterEffect(float slowDivisor, float batteryDrainMultiplier, float submersion01 = 0.5f)
        {
            damWaterSlowDivisor = Mathf.Max(1f, slowDivisor);
            damWaterBatteryDrainMultiplier = Mathf.Max(1f, batteryDrainMultiplier);
            damWaterSubmersion01 = Mathf.Clamp01(submersion01);
        }

        public void ClearDamWaterEffect()
        {
            damWaterSlowDivisor = 1f;
            damWaterBatteryDrainMultiplier = 1f;
            damWaterSubmersion01 = 0f;
        }


        public void SetFrontCapotOpenLocal(bool open)
        {
            if (!IsSpawned || IsServer)
            {
                FrontCapotOpen.Value = open;
            }
            else
            {
                RequestToggleFrontCapotServerRpc();
            }
        }

        public bool IsFrontCapotOpenForInteraction()
        {
            return FrontCapotOpen.Value;
        }

        public void NotifyCarBatteryInstalled(MiniVanCarBattery battery, bool installed, float charge01)
        {
            installedCarBattery = installed ? battery : null;
            if (!IsSpawned || IsServer)
            {
                SetCarBatteryInstalledOnAuthority(installed, charge01);
            }
            else
            {
                SetCarBatteryInstalledServerRpc(installed, charge01);
            }
        }

        private void ApplyInitialCarBatteryNetworkState()
        {
            if (!IsServer)
            {
                return;
            }

            if (installedCarBattery != null)
            {
                SetCarBatteryInstalledOnAuthority(true, installedCarBattery.Charge01);
                return;
            }

            if (carBatteryReceiver != null && carBatteryReceiver.HasBattery && carBatteryReceiver.InstalledBattery != null)
            {
                installedCarBattery = carBatteryReceiver.InstalledBattery;
                SetCarBatteryInstalledOnAuthority(true, installedCarBattery.Charge01);
            }
        }

        private void SetCarBatteryInstalledOnAuthority(bool installed, float charge01)
        {
            localCarBatteryInstalled = installed;
            localCarBatteryCharge01 = Mathf.Clamp01(installed ? charge01 : 0f);
            if (IsSpawned)
            {
                CarBatteryInstalled.Value = localCarBatteryInstalled;
                CarBatteryCharge01.Value = localCarBatteryCharge01;
            }

            if (!installed && EngineOn.Value)
            {
                EngineOn.Value = false;
                EngineLoad.Value = 0f;
                throttleInput = 0f;
            }
        }

        /// <summary>True when a car battery is installed (charge may still be empty).</summary>
        public bool HasCarBatteryInstalled()
        {
            return HasEffectiveCarBatteryInstalled();
        }

        private bool HasEffectiveCarBatteryInstalled()
        {
            return IsSpawned ? CarBatteryInstalled.Value : localCarBatteryInstalled;
        }

        private float GetEffectiveCarBatteryCharge01()
        {
            return Mathf.Clamp01(IsSpawned ? CarBatteryCharge01.Value : localCarBatteryCharge01);
        }

        private void SetEffectiveCarBatteryCharge01(float charge01)
        {
            localCarBatteryCharge01 = Mathf.Clamp01(charge01);
            if (IsSpawned && IsServer)
            {
                CarBatteryCharge01.Value = localCarBatteryCharge01;
            }
        }


        private void ApplySteeringWheelVisual(float steering)
        {
            if (SteeringWheel == null)
            {
                return;
            }

            if (!steeringWheelBaseCaptured)
            {
                steeringWheelBaseLocalRotation = SteeringWheel.localRotation;
                steeringWheelBaseCaptured = true;
            }

            SteeringWheel.localRotation = steeringWheelBaseLocalRotation * Quaternion.AngleAxis(-steering * SteeringWheelVisualAngle, Vector3.forward);
        }


        private void ApplyWheelVisual(Transform wheel, float steerAngle)
        {
            if (wheel == null)
            {
                return;
            }

            int wheelIndex = GetWheelIndex(wheel);
            if (wheelIndex >= 0 && DetachedWheelIndex.Value == wheelIndex)
            {
                wheel.gameObject.SetActive(false);
                return;
            }
            if (!wheel.gameObject.activeSelf)
            {
                wheel.gameObject.SetActive(true);
            }

            wheel.localRotation = Quaternion.Euler(wheelSpin, steerAngle, 90f);
        }

private bool CanShiftToGear(MiniVanGear gear)
        {
            float signedSpeedKph = body != null ? Vector3.Dot(body.linearVelocity, transform.forward) * 3.6f : currentSpeed * 3.6f;
            float speedKph = Mathf.Abs(signedSpeedKph);

            switch (gear)
            {
                case MiniVanGear.Reverse:
                    return signedSpeedKph <= 3f && speedKph <= 8f;
                case MiniVanGear.Park:
                    return speedKph <= 1f;
                default:
                    return true;
            }
        }

private bool CanDriveInGearAtCurrentSpeed(MiniVanGear gear)
        {
            return true;
        }

        private float GetCurrentSpeedKph()
        {
            if (body != null)
            {
                currentSpeed = Vector3.Dot(body.linearVelocity, transform.forward);
            }

            return Mathf.Abs(currentSpeed) * 3.6f;
        }

private void ConfigureBody()
        {
            if (body == null)
            {
                return;
            }

            ApplyStableRuntimeTuning();
            EnsureWheelColliders();
            EnsureVehicleCollisionMaterials();

            body.useGravity = true;
            body.isKinematic = false;
            body.mass = 2650f;
            body.linearDamping = 0.08f;
            // Slightly lower so pitch/roll sway can settle with a soft bounce instead of locking stiff.
            body.angularDamping = 1.55f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.constraints = RigidbodyConstraints.None;
            body.centerOfMass = CenterOfMassOffset;
            body.maxAngularVelocity = 7.5f;
            body.solverIterations = Mathf.Max(body.solverIterations, 12);
            body.solverVelocityIterations = Mathf.Max(body.solverVelocityIterations, 8);
            ScaleWheelVisualsToRadius();
            DisableWheelVisualColliders();
        }

private void ConfigureNetworkPhysicsMode()
        {
            if (body == null)
            {
                return;
            }

            if (IsServer)
            {
                body.useGravity = true;
                body.isKinematic = false;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                return;
            }

            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.useGravity = false;
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.None;
            body.collisionDetectionMode = CollisionDetectionMode.Discrete;
        }


private void DisableWheelVisualColliders()
        {
            Transform[] wheels = { FrontLeftWheel, FrontRightWheel, RearLeftWheel, RearRightWheel };

            for (int i = 0; i < wheels.Length; i++)
            {
                if (wheels[i] == null)
                {
                    continue;
                }

                Collider[] colliders = wheels[i].GetComponentsInChildren<Collider>(true);
                for (int j = 0; j < colliders.Length; j++)
                {
                    colliders[j].enabled = false;
                }
            }
        }


private void AutoAssignWheels()
        {
            FrontLeftWheel = FrontLeftWheel != null ? FrontLeftWheel : FindChildByName("Wheel FL");
            FrontRightWheel = FrontRightWheel != null ? FrontRightWheel : FindChildByName("Wheel FR");
            RearLeftWheel = RearLeftWheel != null ? RearLeftWheel : FindChildByName("Wheel RL");
            RearRightWheel = RearRightWheel != null ? RearRightWheel : FindChildByName("Wheel RR");
            SteeringWheel = SteeringWheel != null ? SteeringWheel : FindChildByName("Steering Wheel");
            EngineStatusLight = EngineStatusLight != null ? EngineStatusLight : FindChildByName("Engine Status Light");
            HandbrakeLever = HandbrakeLever != null ? HandbrakeLever : FindFirstChildByName(new[] { "BreakStopTube", "Handbrake", "Handbrake Lever", "Parking Brake", "ParkingBrake", "Ruchnik" });
            EngineTemperatureAnchor = EngineTemperatureAnchor != null ? EngineTemperatureAnchor : FindChildByName("engine");
            EnsureGearLeverVisuals();

            if (SteeringWheel != null && !steeringWheelBaseCaptured)
            {
                steeringWheelBaseLocalRotation = SteeringWheel.localRotation;
                steeringWheelBaseCaptured = true;
            }

        }

        private Transform FindFirstChildByName(string[] childNames)
        {
            if (childNames == null)
            {
                return null;
            }

            for (int i = 0; i < childNames.Length; i++)
            {
                Transform found = FindChildByName(childNames[i]);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private Transform FindChildByName(string childName)
        {
            Transform[] children = GetComponentsInChildren<Transform>(true);

            for (int i = 0; i < children.Length; i++)
            {
                if (children[i].name == childName)
                {
                    return children[i];
                }
            }

            return null;
        }

        private void SetOccupant(int seatIndex, ulong clientId)
        {
            switch (seatIndex)
            {
                case 0:
                    DriverOccupant.Value = clientId;
                    break;
                case 1:
                    PassengerOneOccupant.Value = clientId;
                    break;
                case 2:
                    PassengerTwoOccupant.Value = clientId;
                    break;
                case 3:
                    PassengerThreeOccupant.Value = clientId;
                    break;
            }
        }

        private int FindSeatIndexForClient(ulong clientId)
        {
            for (int i = 0; i < 4; i++)
            {
                if (GetOccupant(i) == clientId)
                {
                    return i;
                }
            }

            return -1;
        }

        private static ClientRpcParams BuildTarget(ulong clientId)
        {
            return new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new[] { clientId }
                }
            };
        }

        private void ScaleWheelVisualsToRadius()
        {
            Transform[] wheels = { FrontLeftWheel, FrontRightWheel, RearLeftWheel, RearRightWheel };
            float diameter = WheelRadius * 2f;

            for (int i = 0; i < wheels.Length; i++)
            {
                if (wheels[i] == null)
                {
                    continue;
                }

                Vector3 localScale = wheels[i].localScale;
                float maxSide = Mathf.Max(0.01f, Mathf.Max(Mathf.Abs(localScale.x), Mathf.Abs(localScale.y), Mathf.Abs(localScale.z)));
                float scaleFactor = Mathf.Clamp(diameter / maxSide, 0.75f, 1.8f);
                wheels[i].localScale = localScale * scaleFactor;
            }
        }
    

private void ApplyStableRuntimeTuning()
        {
            WheelRadius = 0.56f;
            // Softer / longer travel so landings and bumps compress visibly instead of feeling glued.
            SuspensionRestLength = 0.98f;
            SuspensionSpring = 16800f;
            SuspensionDamper = 2900f;
            MaxSuspensionForce = 34000f;
            RearDriveForce = 2550f;
            DirectDriveForce = 4800f;
            GearSpeedLimitBrake = 4600f;
            LowGearClimbAssist = 11500f;
            LowGearMaxSlopeAngle = 52f;
            LowGearCrawlSpeedKph = 16f;
            LowGearGravityCompensation = 0.95f;
            WheelGroundNormalY = 0.2f;
            EdgeReleaseSpringFactor = 0.18f;
            EdgeReleaseDamperFactor = 0.45f;
            EdgeReleaseExtraGravity = 0.7f;
            LedgeContactReleaseForce = 5200f;
            UseLowFrictionBodyColliders = true;

            BrakeForce = 5200f;
            LateralGrip = 3.25f;
            FrontLateralGrip = 3.7f;
            MaxLateralForce = 7200f;
            SlopeSideHoldAcceleration = 11f;
            MaxSlopeSideHoldAcceleration = 28f;
            ParkingBrakeForceMultiplier = 4.5f;
            ParkingBrakeHoldAcceleration = 28f;
            ParkingBrakeStopSpeed = 0.18f;
            LandingImpactImpulse = 0.4f;
            MinLandingImpactSpeed = 2f;
            LandingSuspensionPunch = 0.58f;
            LandingPitchKick = 0.06f;
            BumpResponseImpulse = 0.3f;
            BumpMinVerticalAccel = 14f;
            MaxSteerAngle = 52f;
            LowSpeedSteerAssist = 10f;
            Downforce = 0.85f;
            UprightAssist = 115f;
            RollingDrag = 0.068f;
            CenterOfMassOffset = new Vector3(0f, -1.02f, -0.22f);
            BodySwayPitchTorque = 0.11f;
            BodySwayRollTorque = 0.13f;
            BodySwayInputPitch = 0.55f;
            BodySwayInputRoll = 0.028f;
            BodySwayMaxAccel = 16f;
            BodySwayAccelFilter = 10f;
            BodySwayAllowedLean = 0.05f;
            IdleRpm = 850f;
            StallRpm = 520f;
            RedlineRpm = 5200f;
            EngineInertia = 5.8f;
            EngineBrakeForce = 420f;
            // Narrow beater power band — pull only when the tach is in the green.
            SweetSpotCenterRpm01 = 0.47f;
            SweetSpotHalfWidthRpm01 = 0.09f;
            SweetSpotTorqueBonus = 0.32f;
            BelowSweetSpotTorqueFloor = 0.16f;
            AboveSweetSpotTorqueFloor = 0.22f;
            // 2nd needs roll already — launching from standstill in 2nd is lugging.
            // 3rd min sits under a normal green 2→3 upshift so you don't instantly lug/stall.
            SecondGearMinSpeedKph = 24f;
            ThirdGearMinSpeedKph = 38f;
            // Sit under a normal green 3→4 / 4→5 handoff so cruise doesn't instantly "lug".
            FourthGearMinSpeedKph = 48f;
            FifthGearMinSpeedKph = 78f;
            GearStallSeconds = 6.5f;
            MotorAcceleration = 5.5f;
            ReverseAcceleration = 3.5f;
            SteeringResponse = 4.4f;
            SteeringReturnSpeed = 7.5f;
            AntiRollForce = 2400f;
            HandbrakeRearGrip = 0.42f;
            HandbrakeDriftRearGrip = 0.24f;
            HandbrakeDriftMinSpeedKph = 12f;
            HandbrakeDriftSteerThreshold = 0.18f;
            HandbrakeDriftYawAssist = 9.5f;
            HandbrakeDriftLateralAssist = 2.3f;
            CornerSlipRearGripLoss = 0.16f;
            CornerSlipMinSpeedKph = 38f;
            CornerSlipSteerThreshold = 0.5f;
            CornerSlipYawAssist = 1.35f;
            SlipWarningThreshold = 0.5f;
            GearBogDragForce = 1250f;
            DebugVehicle = false;
            DebugVehicleAnomalies = true;
            DebugLogInterval = 1f;
        }


private void EnsureWheelColliders()
        {
            frontLeftWheelCollider = GetOrCreateWheelCollider(frontLeftWheelCollider, FrontLeftWheel, "WheelCollider FL");
            frontRightWheelCollider = GetOrCreateWheelCollider(frontRightWheelCollider, FrontRightWheel, "WheelCollider FR");
            rearLeftWheelCollider = GetOrCreateWheelCollider(rearLeftWheelCollider, RearLeftWheel, "WheelCollider RL");
            rearRightWheelCollider = GetOrCreateWheelCollider(rearRightWheelCollider, RearRightWheel, "WheelCollider RR");

            ConfigureWheelCollider(frontLeftWheelCollider, true);
            ConfigureWheelCollider(frontRightWheelCollider, true);
            ConfigureWheelCollider(rearLeftWheelCollider, false);
            ConfigureWheelCollider(rearRightWheelCollider, false);
        }

        private WheelCollider GetOrCreateWheelCollider(WheelCollider existing, Transform visualWheel, string objectName)
        {
            if (existing != null)
            {
                return existing;
            }

            Transform existingTransform = transform.Find(objectName);
            if (existingTransform != null)
            {
                WheelCollider found = existingTransform.GetComponent<WheelCollider>();
                if (found != null)
                {
                    return found;
                }
            }

            GameObject wheelObject = new GameObject(objectName);
            wheelObject.transform.SetParent(transform, false);
            if (visualWheel != null)
            {
                wheelObject.transform.position = visualWheel.position;
            }

            return wheelObject.AddComponent<WheelCollider>();
        }

private void ConfigureWheelCollider(WheelCollider wheelCollider, bool frontWheel)
        {
            if (wheelCollider == null)
            {
                return;
            }

            wheelCollider.radius = WheelRadius;
            wheelCollider.suspensionDistance = SuspensionRestLength;
            wheelCollider.mass = 52f;
            wheelCollider.forceAppPointDistance = 0.2f;
            wheelCollider.ConfigureVehicleSubsteps(5f, 8, 12);

            JointSpring spring = wheelCollider.suspensionSpring;
            spring.spring = SuspensionSpring;
            spring.damper = SuspensionDamper;
            // Sit higher in the travel so landings/bumps have room to compress downward.
            spring.targetPosition = frontWheel ? 0.48f : 0.4f;
            wheelCollider.suspensionSpring = spring;

            WheelFrictionCurve forward = wheelCollider.forwardFriction;
            forward.extremumSlip = frontWheel ? 0.38f : 0.46f;
            forward.extremumValue = 1f;
            forward.asymptoteSlip = frontWheel ? 0.8f : 0.92f;
            forward.asymptoteValue = 0.68f;
            forward.stiffness = frontWheel ? 1.05f : 1.22f;
            wheelCollider.forwardFriction = forward;

            WheelFrictionCurve sideways = wheelCollider.sidewaysFriction;
            sideways.extremumSlip = frontWheel ? 0.32f : 0.38f;
            sideways.extremumValue = 1f;
            sideways.asymptoteSlip = frontWheel ? 0.72f : 0.82f;
            sideways.asymptoteValue = 0.58f;
            sideways.stiffness = frontWheel ? FrontLateralGrip : LateralGrip;
            wheelCollider.sidewaysFriction = sideways;
        }

private int CountGroundedWheels()
        {
            int grounded = 0;
            WheelHit hit;
            grounded += IsWheelOnValidGround(frontLeftWheelCollider, out hit) ? 1 : 0;
            grounded += IsWheelOnValidGround(frontRightWheelCollider, out hit) ? 1 : 0;
            grounded += IsWheelOnValidGround(rearLeftWheelCollider, out hit) ? 1 : 0;
            grounded += IsWheelOnValidGround(rearRightWheelCollider, out hit) ? 1 : 0;
            return grounded;
        }


private int CountRawGroundedWheels()
        {
            int grounded = 0;
            WheelHit hit;
            grounded += frontLeftWheelCollider != null && frontLeftWheelCollider.GetGroundHit(out hit) ? 1 : 0;
            grounded += frontRightWheelCollider != null && frontRightWheelCollider.GetGroundHit(out hit) ? 1 : 0;
            grounded += rearLeftWheelCollider != null && rearLeftWheelCollider.GetGroundHit(out hit) ? 1 : 0;
            grounded += rearRightWheelCollider != null && rearRightWheelCollider.GetGroundHit(out hit) ? 1 : 0;
            return grounded;
        }

private int CountValidRearGroundedWheels()
        {
            int grounded = 0;
            WheelHit hit;
            grounded += IsWheelOnValidGround(rearLeftWheelCollider, out hit) ? 1 : 0;
            grounded += IsWheelOnValidGround(rearRightWheelCollider, out hit) ? 1 : 0;
            return grounded;
        }

        private bool IsWheelOnValidGround(WheelCollider wheelCollider, out WheelHit hit)
        {
            hit = default;
            return wheelCollider != null && wheelCollider.GetGroundHit(out hit) && IsValidWheelGroundHit(wheelCollider, hit);
        }

        private bool IsValidWheelGroundHit(WheelCollider wheelCollider, WheelHit hit)
        {
            if (wheelCollider == null || hit.collider == null || hit.collider.transform.IsChildOf(transform))
            {
                return false;
            }

            float normalY = Vector3.Dot(hit.normal.normalized, Vector3.up);
            return normalY >= Mathf.Clamp(WheelGroundNormalY, 0.05f, 0.9f);
        }

        private void ApplyWheelEdgeReleaseTuning()
        {
            int rawGrounded = 0;
            int validGrounded = 0;
            int invalidContacts = 0;
            invalidContacts += ApplyWheelEdgeReleaseTuning(frontLeftWheelCollider, ref rawGrounded, ref validGrounded);
            invalidContacts += ApplyWheelEdgeReleaseTuning(frontRightWheelCollider, ref rawGrounded, ref validGrounded);
            invalidContacts += ApplyWheelEdgeReleaseTuning(rearLeftWheelCollider, ref rawGrounded, ref validGrounded);
            invalidContacts += ApplyWheelEdgeReleaseTuning(rearRightWheelCollider, ref rawGrounded, ref validGrounded);

            if (invalidContacts <= 0)
            {
                return;
            }

            if (validGrounded < 2 && EdgeReleaseExtraGravity > 0.01f)
            {
                body.AddForce(Physics.gravity * EdgeReleaseExtraGravity, ForceMode.Acceleration);
            }

            if (DebugVehicleAnomalies && Time.time - lastEdgeReleaseDebugTime > 0.35f)
            {
                lastEdgeReleaseDebugTime = Time.time;
                Debug.LogWarning("[MiniVanPhysicsEdgeRelease] rawGrounded=" + rawGrounded + "/4 validGrounded=" + validGrounded + "/4 invalid=" + invalidContacts + " wheels=" + GetInvalidWheelSummary() + " vel=" + body.linearVelocity.ToString("F2") + " pos=" + transform.position.ToString("F2"));
            }
        }

        private int ApplyWheelEdgeReleaseTuning(WheelCollider wheelCollider, ref int rawGrounded, ref int validGrounded)
        {
            if (wheelCollider == null || !wheelCollider.GetGroundHit(out WheelHit hit))
            {
                return 0;
            }

            rawGrounded++;
            if (IsValidWheelGroundHit(wheelCollider, hit))
            {
                validGrounded++;
                return 0;
            }

            JointSpring spring = wheelCollider.suspensionSpring;
            spring.spring = SuspensionSpring * Mathf.Clamp01(EdgeReleaseSpringFactor);
            spring.damper = SuspensionDamper * Mathf.Clamp01(EdgeReleaseDamperFactor);
            spring.targetPosition = 0.2f;
            wheelCollider.suspensionSpring = spring;
            wheelCollider.motorTorque = 0f;
            wheelCollider.brakeTorque = Mathf.Min(wheelCollider.brakeTorque, BrakeForce * 0.25f);
            return 1;
        }

        private string GetInvalidWheelSummary()
        {
            return GetWheelValidity(frontLeftWheelCollider, "FL") + " " +
                   GetWheelValidity(frontRightWheelCollider, "FR") + " " +
                   GetWheelValidity(rearLeftWheelCollider, "RL") + " " +
                   GetWheelValidity(rearRightWheelCollider, "RR");
        }

        private string GetWheelValidity(WheelCollider wheelCollider, string label)
        {
            if (wheelCollider == null)
            {
                return label + ":null";
            }

            if (!wheelCollider.GetGroundHit(out WheelHit hit))
            {
                return label + ":air";
            }

            float normalY = hit.collider != null ? Vector3.Dot(hit.normal.normalized, Vector3.up) : -1f;
            return label + (IsValidWheelGroundHit(wheelCollider, hit) ? ":ok" : ":bad") + " nY=" + normalY.ToString("0.00") + " c=" + (hit.collider != null ? hit.collider.name : "none");
        }

        private void EnsureVehicleCollisionMaterials()
        {
            if (!UseLowFrictionBodyColliders)
            {
                return;
            }

            PhysicsMaterial material = GetOrCreateLowFrictionBodyMaterial();
            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (ShouldUseLowFrictionVehicleCollider(colliders[i]))
                {
                    colliders[i].sharedMaterial = material;
                }
            }
        }

        private PhysicsMaterial GetOrCreateLowFrictionBodyMaterial()
        {
            if (lowFrictionBodyMaterial != null)
            {
                return lowFrictionBodyMaterial;
            }

            lowFrictionBodyMaterial = new PhysicsMaterial("MiniVan Runtime Low Friction");
            lowFrictionBodyMaterial.staticFriction = 0f;
            lowFrictionBodyMaterial.dynamicFriction = 0f;
            lowFrictionBodyMaterial.frictionCombine = PhysicsMaterialCombine.Minimum;
            lowFrictionBodyMaterial.bounciness = 0f;
            lowFrictionBodyMaterial.bounceCombine = PhysicsMaterialCombine.Minimum;
            return lowFrictionBodyMaterial;
        }

        private bool ShouldUseLowFrictionVehicleCollider(Collider collider)
        {
            if (collider == null || collider.isTrigger || collider is WheelCollider || !collider.transform.IsChildOf(transform))
            {
                return false;
            }

            return true;
        }

        public void ApplyVehicleDamage(float amount, string reason = "")
        {
            if (amount <= 0f)
            {
                return;
            }

            if (IsSpawned && !IsServer)
            {
                return;
            }

            float max = Mathf.Max(1f, MaxHealth);
            float previous = Mathf.Clamp(Health.Value, 0f, max);
            float next = Mathf.Clamp(previous - amount, 0f, max);
            if (Mathf.Approximately(previous, next))
            {
                return;
            }

            Health.Value = next;
            if (DebugVehicleDamage)
            {
                Debug.Log("[MiniVanDamage] -" + amount.ToString("0.0") + " reason=" + reason + " health=" + next.ToString("0.0") + "/" + max.ToString("0"));
            }

            if (next <= 0f)
            {
                TriggerFinalDestruction();
            }
        }

        private void HandleHealthChanged(float previous, float current)
        {
            if (current <= 0f)
            {
                TriggerFinalDestruction();
            }
        }

        private void TriggerFinalDestruction()
        {
            if (finalDestructionTriggered)
            {
                return;
            }

            finalDestructionTriggered = true;
            Vector3 explosionPosition = GetFinalExplosionPosition();

            if (DebugFinalDestruction)
            {
                Debug.Log("[MiniVanGameOver] Final destruction triggered at " + explosionPosition);
            }

            StopVehicleMotion();

            if (IsSpawned && IsServer)
            {
                TriggerFinalDestructionClientRpc(explosionPosition);
                StartCoroutine(HideFinalVehicleAfterDelayServerRoutine());
                return;
            }

            SpawnFinalExplosionLocal(explosionPosition);
            MiniVanGameOverScreen.Show();
            StartCoroutine(HideFinalVehicleAfterDelayLocalRoutine());
        }

        [ClientRpc]
        private void TriggerFinalDestructionClientRpc(Vector3 explosionPosition)
        {
            finalDestructionTriggered = true;
            SpawnFinalExplosionLocal(explosionPosition);
            MiniVanGameOverScreen.Show();
        }

        private IEnumerator HideFinalVehicleAfterDelayServerRoutine()
        {
            yield return new WaitForSeconds(Mathf.Max(0f, FinalVehicleDisappearDelay));
            HideFinalVehicleClientRpc();
        }

        private IEnumerator HideFinalVehicleAfterDelayLocalRoutine()
        {
            yield return new WaitForSeconds(Mathf.Max(0f, FinalVehicleDisappearDelay));
            HideFinalVehicleLocal();
        }

        [ClientRpc]
        private void HideFinalVehicleClientRpc()
        {
            HideFinalVehicleLocal();
        }

        private void SpawnFinalExplosionLocal(Vector3 explosionPosition)
        {
            if (finalExplosionInstance != null)
            {
                return;
            }

            GameObject prefab = FinalExplosionPrefab;
            if (prefab == null && !string.IsNullOrWhiteSpace(FinalExplosionResourcePath))
            {
                prefab = Resources.Load<GameObject>(FinalExplosionResourcePath);
            }

            if (prefab == null)
            {
                Debug.LogWarning("[MiniVanGameOver] Final explosion prefab is not assigned.");
                return;
            }

            finalExplosionInstance = Instantiate(prefab, explosionPosition, transform.rotation);
        }

        private Vector3 GetFinalExplosionPosition()
        {
            Bounds bounds = new Bounds(transform.TransformPoint(FinalExplosionLocalOffset), Vector3.zero);
            bool hasBounds = false;
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return hasBounds ? bounds.center : transform.TransformPoint(FinalExplosionLocalOffset);
        }

        private void HideFinalVehicleLocal()
        {
            if (finalVehicleHidden)
            {
                return;
            }

            finalVehicleHidden = true;
            StopVehicleMotion();

            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    renderers[i].enabled = false;
                }
            }

            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                {
                    colliders[i].enabled = false;
                }
            }

            WheelCollider[] wheelColliders = GetComponentsInChildren<WheelCollider>(true);
            for (int i = 0; i < wheelColliders.Length; i++)
            {
                if (wheelColliders[i] != null)
                {
                    wheelColliders[i].enabled = false;
                }
            }

            AudioSource[] audioSources = GetComponentsInChildren<AudioSource>(true);
            for (int i = 0; i < audioSources.Length; i++)
            {
                if (audioSources[i] != null)
                {
                    audioSources[i].Stop();
                }
            }

            if (DebugFinalDestruction)
            {
                Debug.Log("[MiniVanGameOver] MiniVan hidden after final explosion.");
            }
        }

        private void StopVehicleMotion()
        {
            ClearDriverInputs();

            if (body != null)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.isKinematic = finalDestructionTriggered || finalVehicleHidden;
            }

            WheelCollider[] wheels = GetComponentsInChildren<WheelCollider>(true);
            for (int i = 0; i < wheels.Length; i++)
            {
                WheelCollider wheel = wheels[i];
                if (wheel == null)
                {
                    continue;
                }

                wheel.motorTorque = 0f;
                wheel.brakeTorque = BrakeForce;
            }
        }

        public bool HasOccupant(ulong clientId)
        {
            if (clientId == EmptyClientId)
            {
                return false;
            }

            return DriverOccupant.Value == clientId ||
                   PassengerOneOccupant.Value == clientId ||
                   PassengerTwoOccupant.Value == clientId ||
                   PassengerThreeOccupant.Value == clientId;
        }

        /// <summary>
        /// True when the player is seated in or walking inside this van's cabin
        /// (used to block hood open/close from the salon).
        /// </summary>
        public bool IsPlayerInsideCabinForHood(MiniVanPlayer player)
        {
            if (player == null)
            {
                return false;
            }

            if (player.IsSpawned && HasOccupant(player.OwnerClientId))
            {
                return true;
            }

            Vector3 local = transform.InverseTransformPoint(player.transform.position);
            return Mathf.Abs(local.x) <= 1.55f &&
                   local.z >= -3.4f &&
                   local.z <= 1.45f &&
                   local.y >= 0.35f &&
                   local.y <= 2.15f;
        }

        public bool TryApplyZombieDamageFrom(MiniVanZombie zombie, float amount)
        {
            if (zombie == null || amount <= 0f)
            {
                if (DebugVehicleDamage)
                {
                    Debug.LogWarning("[MiniVanDamageDebug] zombie damage rejected: invalid zombie or amount=" + amount);
                }
                return false;
            }

            if (IsSpawned && !IsServer)
            {
                if (DebugVehicleDamage)
                {
                    Debug.LogWarning("[MiniVanDamageDebug] zombie damage rejected: vehicle is not server");
                }
                return false;
            }

            if (Health.Value <= 0f)
            {
                if (DebugVehicleDamage)
                {
                    Debug.LogWarning("[MiniVanDamageDebug] zombie damage rejected: vehicle health is already zero");
                }
                return false;
            }

            Vector3 attackPoint = zombie.transform.position + Vector3.up * 0.8f;
            float bodyDistance = GetDistanceFromVehicleBody(attackPoint);
            float attackRange = Mathf.Max(0.25f, ZombieVehicleAttackRange);
            if (bodyDistance > attackRange)
            {
                if (DebugVehicleDamage)
                {
                    Debug.LogWarning(
                        "[MiniVanDamageDebug] zombie damage rejected: distance=" +
                        bodyDistance.ToString("0.00") +
                        " range=" + attackRange.ToString("0.00") +
                        " zombie=" + zombie.name);
                }
                return false;
            }

            float previousHealth = Health.Value;
            ApplyVehicleDamage(amount, "zombie");
            if (DebugVehicleDamage)
            {
                Debug.LogWarning(
                    "[MiniVanDamageDebug] zombie damage accepted: health=" +
                    previousHealth.ToString("0.0") + "->" + Health.Value.ToString("0.0") +
                    " distance=" + bodyDistance.ToString("0.00") +
                    " zombie=" + zombie.name);
            }
            return true;
        }

        private void UpdateVehicleDamageSensors()
        {
            EnsureWheelColliders();
            int grounded = CountGroundedWheels();
            float speedKph = body != null ? body.linearVelocity.magnitude * 3.6f : 0f;
            float downSpeed = body != null ? Mathf.Max(0f, -body.linearVelocity.y) : 0f;
            UpdateWheelPitDetachSensors(speedKph);

            if (grounded <= 1)
            {
                if (!vehicleAirborne)
                {
                    vehicleAirborne = true;
                    airborneMaxDownSpeed = downSpeed;
                    airborneStartSpeedKph = speedKph;
                }
                else
                {
                    airborneMaxDownSpeed = Mathf.Max(airborneMaxDownSpeed, downSpeed);
                    airborneStartSpeedKph = Mathf.Max(airborneStartSpeedKph, speedKph);
                }
            }
            else if (vehicleAirborne && grounded >= 2)
            {
                ResolveVehicleLanding(airborneMaxDownSpeed, airborneStartSpeedKph, grounded);
                vehicleAirborne = false;
                airborneMaxDownSpeed = 0f;
                airborneStartSpeedKph = 0f;
            }

            lastGroundedWheelCount = grounded;
        }

        private void UpdateWheelPitDetachSensors(float speedKph)
        {
            bool[] groundedNow =
            {
                IsWheelGroundedForPit(0),
                IsWheelGroundedForPit(1),
                IsWheelGroundedForPit(2),
                IsWheelGroundedForPit(3)
            };

            if (!wheelPitTrackingInitialized)
            {
                for (int i = 0; i < 4; i++)
                {
                    wheelWasGroundedForPit[i] = groundedNow[i];
                    wheelUngroundedSince[i] = groundedNow[i] ? -1f : Time.time;
                }
                wheelPitTrackingInitialized = true;
                return;
            }

            for (int i = 0; i < 4; i++)
            {
                if (groundedNow[i])
                {
                    wheelUngroundedSince[i] = -1f;
                }
                else if (wheelWasGroundedForPit[i])
                {
                    wheelUngroundedSince[i] = Time.time;
                }

                wheelWasGroundedForPit[i] = groundedNow[i];
            }

            if (DetachedWheelIndex.Value >= 0 || speedKph < WheelDetachPitSpeedKph)
            {
                return;
            }

            float requiredLoss = Mathf.Max(0.02f, WheelDetachPitContactLossSeconds);
            for (int i = 0; i < 4; i++)
            {
                if (groundedNow[i] || wheelUngroundedSince[i] < 0f)
                {
                    continue;
                }

                if (Time.time - wheelUngroundedSince[i] >= requiredLoss)
                {
                    if (IsPitDeepEnoughUnderWheel(i))
                    {
                        DetachWheel(i, "pit");
                        return;
                    }
                }
            }
        }

        private bool IsWheelGroundedForPit(int wheelIndex)
        {
            return IsWheelOnValidGround(GetWheelCollider(wheelIndex), out WheelHit hit);
        }

        private bool IsPitDeepEnoughUnderWheel(int wheelIndex)
        {
            WheelCollider wheelCollider = GetWheelCollider(wheelIndex);
            Transform wheelTransform = wheelCollider != null ? wheelCollider.transform : GetWheelVisual(wheelIndex);
            if (wheelTransform == null)
            {
                return false;
            }

            float minDepth = Mathf.Max(0.15f, WheelDetachPitMinDepth);
            Vector3 origin = wheelTransform.position + Vector3.up * 0.08f;
            RaycastHit[] hits = Physics.RaycastAll(
                origin,
                Vector3.down,
                minDepth,
                ~0,
                QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
            for (int i = 0; i < hits.Length; i++)
            {
                Collider hit = hits[i].collider;
                if (hit == null || hit.transform.IsChildOf(transform))
                {
                    continue;
                }

                float normalY = Vector3.Dot(hits[i].normal.normalized, Vector3.up);
                if (normalY >= Mathf.Clamp(WheelGroundNormalY, 0.05f, 0.9f))
                {
                    return false;
                }
            }

            return true;
        }

        private void ResolveVehicleLanding(float maxDownSpeed, float startSpeedKph, int grounded)
        {
            if (maxDownSpeed > FallDamageMinVerticalSpeed)
            {
                float damage = (maxDownSpeed - FallDamageMinVerticalSpeed) * FallDamagePerMeterPerSecond;
                ApplyVehicleDamage(damage, "fall");
            }

            // Weighty landing: punch the chassis into the springs + small nose bob.
            if (body != null && grounded >= 2 && maxDownSpeed > 1.15f)
            {
                float punch = body.mass * maxDownSpeed * Mathf.Max(0f, LandingSuspensionPunch);
                body.AddForce(-transform.up * punch, ForceMode.Impulse);
                body.AddTorque(transform.right * (maxDownSpeed * Mathf.Max(0f, LandingPitchKick)), ForceMode.VelocityChange);
            }

            if (DetachedWheelIndex.Value >= 0)
            {
                return;
            }

            bool hardFall = maxDownSpeed >= WheelDetachFallVerticalSpeed;
            bool fastPitDrop = startSpeedKph >= WheelDetachPitSpeedKph &&
                               maxDownSpeed >= FallDamageMinVerticalSpeed;
            if (hardFall || fastPitDrop)
            {
                DetachRandomWheel();
            }
        }

        /// <summary>
        /// When vertical velocity is suddenly killed by ground/bumps, dig into suspension
        /// so the van feels heavy instead of skating through the hit.
        /// </summary>
        private void ApplySuspensionBumpFeel(int groundedWheels)
        {
            if (body == null)
            {
                bodyVerticalVelocityInitialized = false;
                return;
            }

            float vy = body.linearVelocity.y;
            if (!bodyVerticalVelocityInitialized)
            {
                lastBodyVerticalVelocity = vy;
                bodyVerticalVelocityInitialized = true;
                return;
            }

            float dt = Mathf.Max(0.0001f, Time.fixedDeltaTime);
            float prevVy = lastBodyVerticalVelocity;
            float accelY = (vy - prevVy) / dt;
            lastBodyVerticalVelocity = vy;

            if (groundedWheels < 2 || BumpResponseImpulse <= 0.001f)
            {
                return;
            }

            // Compression impact: falling, then vertical speed is abruptly cancelled.
            if (prevVy < -1.05f && accelY > BumpMinVerticalAccel)
            {
                float impactSpeed = -prevVy;
                float impulse = body.mass * impactSpeed * BumpResponseImpulse;
                body.AddForce(-transform.up * impulse, ForceMode.Impulse);
                body.AddTorque(transform.right * (impactSpeed * LandingPitchKick * 0.65f), ForceMode.VelocityChange);
                return;
            }

            // Obstacle pop / curb: sharp upward kick while mostly planted — settle back into springs.
            if (prevVy > -0.35f && accelY > BumpMinVerticalAccel * 1.15f)
            {
                float bump = Mathf.Clamp01((accelY - BumpMinVerticalAccel) / 45f);
                body.AddForce(-transform.up * body.mass * (0.55f + bump) * BumpResponseImpulse, ForceMode.Impulse);
                body.AddTorque(transform.right * (bump * 0.04f), ForceMode.VelocityChange);
            }
        }

        private void ScanZombieVehicleAttacks()
        {
            // Backup path: if AI stalls (e.g. offline vampire / failed Netcode spawn), an occupied
            // van still takes hits from any nearby zombie — engine and battery do not matter.
            if (!IsServer || !HasAnyOccupantOrSeatedPlayer())
            {
                return;
            }

            MiniVanZombie[] zombies = MiniVanSceneScan.Get<MiniVanZombie>();
            if (zombies == null || zombies.Length == 0)
            {
                return;
            }

            float range = Mathf.Max(0.25f, ZombieVehicleAttackRange);
            float interval = Mathf.Max(0.1f, ZombieVehicleAttackInterval);
            for (int i = 0; i < zombies.Length; i++)
            {
                MiniVanZombie zombie = zombies[i];
                if (zombie == null || !zombie.gameObject.activeInHierarchy)
                {
                    continue;
                }

                // Bat-form vampires fly; only grounded humanoids should dent the van here.
                MiniVanVampire vampire = zombie as MiniVanVampire;
                if (vampire != null && vampire.CurrentForm == MiniVanVampire.Form.Bat)
                {
                    continue;
                }

                float distance = GetDistanceFromVehicleBody(zombie.transform.position + Vector3.up * 0.8f);
                if (distance > range)
                {
                    continue;
                }

                if (zombieVehicleAttackTimes.TryGetValue(zombie, out float nextAttackTime) &&
                    Time.time < nextAttackTime)
                {
                    continue;
                }

                zombieVehicleAttackTimes[zombie] = Time.time + interval;
                ApplyVehicleDamage(ZombieVehicleDamagePerHit, "zombie");
            }
        }

        public bool HasAnyOccupantOrSeatedPlayer()
        {
            if (DriverOccupant.Value != EmptyClientId ||
                PassengerOneOccupant.Value != EmptyClientId ||
                PassengerTwoOccupant.Value != EmptyClientId ||
                PassengerThreeOccupant.Value != EmptyClientId)
            {
                return true;
            }

            MiniVanPlayer[] players = MiniVanSceneScan.Get<MiniVanPlayer>();
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] != null &&
                    (players[i].CurrentVehicle == this ||
                     players[i].IsInsideVehicleCabinForZombieTarget(this)))
                {
                    return true;
                }
            }

            return false;
        }

        public float GetDistanceFromVehicleBody(Vector3 point)
        {
            Vector3 closest = GetClosestVehicleBodyPoint(point);
            return Vector3.Distance(point, closest);
        }

        public Vector3 GetClosestVehicleBodyPoint(Vector3 point)
        {
            float best = float.PositiveInfinity;
            Vector3 bestPoint = transform.position + Vector3.up * 0.85f;
            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider == null || collider.isTrigger || collider is WheelCollider || !collider.enabled)
                {
                    continue;
                }

                Vector3 closest = collider.ClosestPoint(point);
                float distance = Vector3.Distance(point, closest);
                if (distance < best)
                {
                    best = distance;
                    bestPoint = closest;
                }
            }

            return bestPoint;
        }

        private void DetachRandomWheel()
        {
            int[] candidates = { 0, 1, 2, 3 };
            int index = candidates[Random.Range(0, candidates.Length)];
            DetachWheel(index, "impact");
        }

        public bool DetachWheel(int wheelIndex, string reason = "")
        {
            if (wheelIndex < 0 || wheelIndex > 3 || DetachedWheelIndex.Value >= 0)
            {
                return false;
            }

            Transform visual = GetWheelVisual(wheelIndex);
            WheelCollider wheelCollider = GetWheelCollider(wheelIndex);
            Vector3 position = visual != null ? visual.position : transform.TransformPoint(GetDefaultWheelLocalPosition(wheelIndex));
            Quaternion rotation = visual != null ? visual.rotation : transform.rotation;

            DetachedWheelIndex.Value = wheelIndex;
            if (wheelCollider != null)
            {
                wheelCollider.enabled = false;
                wheelCollider.motorTorque = 0f;
                wheelCollider.brakeTorque = 0f;
            }

            if (visual != null)
            {
                visual.gameObject.SetActive(false);
            }

            SpawnDetachedWheelObject(wheelIndex, position, rotation);
            EnsureWheelRepairMounts();
            ApplyVehicleDamage(Mathf.Max(1f, MaxHealth) * 0.1f, "wheel_detach");
            if (DebugVehicleDamage)
            {
                Debug.Log("[MiniVanDamage] detached wheel=" + wheelIndex + " reason=" + reason);
            }

            return true;
        }

        public bool TryReattachDetachedWheel(MiniVanDetachedWheel wheel, MiniVanPlayer player)
        {
            if (wheel == null || player == null)
            {
                return false;
            }

            int missingSlot = DetachedWheelIndex.Value;
            if (missingSlot < 0 || missingSlot > 3)
            {
                return false;
            }

            // Original detached wheel must match this vehicle+slot; spare can fill any missing slot.
            bool isOriginal = !wheel.IsSpare && wheel.Vehicle == this && wheel.WheelIndex == missingSlot;
            bool isSpare = wheel.IsSpare;
            if (!isOriginal && !isSpare)
            {
                return false;
            }

            if (IsSpawned && !IsServer)
            {
                return false;
            }

            if (detachedWheelObject == wheel.gameObject)
            {
                detachedWheelObject = null;
            }

            Destroy(wheel.gameObject);
            return RestoreMissingWheelSlot(missingSlot);
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestReattachDetachedWheelServerRpc(bool isSpare, int carriedWheelIndex, ServerRpcParams rpcParams = default)
        {
            int missingSlot = DetachedWheelIndex.Value;
            if (missingSlot < 0 || missingSlot > 3)
            {
                return;
            }

            if (!isSpare && carriedWheelIndex != missingSlot)
            {
                return;
            }

            if (detachedWheelObject != null)
            {
                Destroy(detachedWheelObject);
                detachedWheelObject = null;
            }

            RestoreMissingWheelSlot(missingSlot);
        }

        private bool RestoreMissingWheelSlot(int wheelIndex)
        {
            if (wheelIndex < 0 || wheelIndex > 3)
            {
                return false;
            }

            WheelCollider wheelCollider = GetWheelCollider(wheelIndex);
            Transform visual = GetWheelVisual(wheelIndex);
            DetachedWheelIndex.Value = -1;
            if (wheelCollider != null)
            {
                wheelCollider.enabled = true;
            }

            if (visual != null)
            {
                visual.gameObject.SetActive(true);
            }

            ApplyDetachedWheelState();
            return true;
        }

        private void SpawnDetachedWheelObject(int wheelIndex, Vector3 position, Quaternion rotation)
        {
            if (detachedWheelObject != null)
            {
                Destroy(detachedWheelObject);
                detachedWheelObject = null;
            }

            Transform source = GetWheelVisual(wheelIndex);
            GameObject wheelObject = source != null
                ? Instantiate(source.gameObject, position, rotation)
                : GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            wheelObject.name = "Detached MiniVan Wheel " + GetWheelLabel(wheelIndex);
            wheelObject.SetActive(true);
            wheelObject.transform.SetParent(null, true);
            wheelObject.layer = gameObject.layer;
            if (source == null)
            {
                wheelObject.transform.position = position;
                wheelObject.transform.rotation = rotation * Quaternion.Euler(0f, 0f, 90f);
                wheelObject.transform.localScale = Vector3.one * Mathf.Max(0.45f, WheelRadius * 1.8f);
            }

            MiniVanDetachedWheel detached = wheelObject.GetComponent<MiniVanDetachedWheel>();
            if (detached == null)
            {
                detached = wheelObject.AddComponent<MiniVanDetachedWheel>();
            }
            detached.Initialize(this, wheelIndex);
            detached.SuppressVehiclePhysicsInfluence();
            detachedWheelObject = wheelObject;
        }

        private void ApplyDetachedWheelState()
        {
            if (finalDestructionTriggered || finalVehicleHidden)
            {
                DisableAllWheelColliders();
                return;
            }

            int detached = DetachedWheelIndex.Value;
            ApplyDetachedWheelState(0, frontLeftWheelCollider, FrontLeftWheel, detached);
            ApplyDetachedWheelState(1, frontRightWheelCollider, FrontRightWheel, detached);
            ApplyDetachedWheelState(2, rearLeftWheelCollider, RearLeftWheel, detached);
            ApplyDetachedWheelState(3, rearRightWheelCollider, RearRightWheel, detached);
        }

        private void DisableAllWheelColliders()
        {
            if (frontLeftWheelCollider != null) frontLeftWheelCollider.enabled = false;
            if (frontRightWheelCollider != null) frontRightWheelCollider.enabled = false;
            if (rearLeftWheelCollider != null) rearLeftWheelCollider.enabled = false;
            if (rearRightWheelCollider != null) rearRightWheelCollider.enabled = false;
        }

        private static void ApplyDetachedWheelState(int index, WheelCollider wheelCollider, Transform visual, int detached)
        {
            bool isDetached = detached == index;
            if (wheelCollider != null)
            {
                wheelCollider.enabled = !isDetached;
                if (isDetached)
                {
                    wheelCollider.motorTorque = 0f;
                    wheelCollider.brakeTorque = 0f;
                }
            }

            if (visual != null && visual.gameObject.activeSelf == isDetached)
            {
                visual.gameObject.SetActive(!isDetached);
            }
        }

        private void EnsureWheelRepairMounts()
        {
            if (wheelMountPoints == null || wheelMountPoints.Length != 4)
            {
                wheelMountPoints = new MiniVanWheelMountPoint[4];
            }

            EnsureWheelRepairMount(0, FrontLeftWheel);
            EnsureWheelRepairMount(1, FrontRightWheel);
            EnsureWheelRepairMount(2, RearLeftWheel);
            EnsureWheelRepairMount(3, RearRightWheel);
        }

        private void EnsureWheelRepairMount(int index, Transform wheelVisual)
        {
            string mountName = "Wheel Repair Mount " + GetWheelLabel(index);
            Transform mount = transform.Find(mountName);
            if (mount == null)
            {
                GameObject mountObject = new GameObject(mountName);
                mountObject.transform.SetParent(transform, false);
                mount = mountObject.transform;
            }

            mount.position = wheelVisual != null ? wheelVisual.position : transform.TransformPoint(GetDefaultWheelLocalPosition(index));
            mount.rotation = wheelVisual != null ? wheelVisual.rotation : transform.rotation;

            BoxCollider collider = mount.GetComponent<BoxCollider>();
            if (collider == null)
            {
                collider = mount.gameObject.AddComponent<BoxCollider>();
            }
            collider.isTrigger = true;
            float mountSize = Mathf.Max(1.35f, WheelRadius * 2.8f);
            collider.size = new Vector3(mountSize, mountSize, mountSize);
            collider.center = Vector3.zero;

            MiniVanWheelMountPoint point = mount.GetComponent<MiniVanWheelMountPoint>();
            if (point == null)
            {
                point = mount.gameObject.AddComponent<MiniVanWheelMountPoint>();
            }

            point.Vehicle = this;
            point.WheelIndex = index;
            point.GhostMaterial = GetWheelGhostMaterial();
            point.SourceWheelVisual = wheelVisual;
            wheelMountPoints[index] = point;
        }

        private Transform GetWheelVisual(int wheelIndex)
        {
            switch (wheelIndex)
            {
                case 0: return FrontLeftWheel;
                case 1: return FrontRightWheel;
                case 2: return RearLeftWheel;
                case 3: return RearRightWheel;
                default: return null;
            }
        }

        private WheelCollider GetWheelCollider(int wheelIndex)
        {
            switch (wheelIndex)
            {
                case 0: return frontLeftWheelCollider;
                case 1: return frontRightWheelCollider;
                case 2: return rearLeftWheelCollider;
                case 3: return rearRightWheelCollider;
                default: return null;
            }
        }

        private int GetWheelIndex(Transform wheel)
        {
            if (wheel == FrontLeftWheel) return 0;
            if (wheel == FrontRightWheel) return 1;
            if (wheel == RearLeftWheel) return 2;
            if (wheel == RearRightWheel) return 3;
            return -1;
        }

        private Vector3 GetDefaultWheelLocalPosition(int wheelIndex)
        {
            switch (wheelIndex)
            {
                case 0: return new Vector3(-1.1f, -0.7f, 1.7f);
                case 1: return new Vector3(1.1f, -0.7f, 1.7f);
                case 2: return new Vector3(-1.1f, -0.7f, -1.7f);
                case 3: return new Vector3(1.1f, -0.7f, -1.7f);
                default: return Vector3.zero;
            }
        }

        private static string GetWheelLabel(int wheelIndex)
        {
            switch (wheelIndex)
            {
                case 0: return "FL";
                case 1: return "FR";
                case 2: return "RL";
                case 3: return "RR";
                default: return "?";
            }
        }

        private static Material GetWheelGhostMaterial()
        {
            if (wheelGhostMaterial != null)
            {
                return wheelGhostMaterial;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            wheelGhostMaterial = new Material(shader);
            Color color = new Color(0.2f, 1f, 0.25f, 0.38f);
            wheelGhostMaterial.color = color;
            if (wheelGhostMaterial.HasProperty("_BaseColor"))
            {
                wheelGhostMaterial.SetColor("_BaseColor", color);
            }
            if (wheelGhostMaterial.HasProperty("_Surface"))
            {
                wheelGhostMaterial.SetFloat("_Surface", 1f);
            }
            if (wheelGhostMaterial.HasProperty("_AlphaClip"))
            {
                wheelGhostMaterial.SetFloat("_AlphaClip", 0f);
            }
            wheelGhostMaterial.renderQueue = 3000;
            return wheelGhostMaterial;
        }

        private void OnCollisionStay(Collision collision)
        {
            if (!IsServer || body == null || collision == null || LedgeContactReleaseForce <= 0.01f || CountGroundedWheels() >= 2)
            {
                return;
            }

            if (collision.collider != null &&
                collision.collider.GetComponentInParent<MiniVanPlayer>() != null)
            {
                return;
            }

            Vector3 releaseNormal = Vector3.zero;
            int contactCount = collision.contactCount;
            for (int i = 0; i < contactCount; i++)
            {
                ContactPoint contact = collision.GetContact(i);
                float normalY = Vector3.Dot(contact.normal.normalized, Vector3.up);
                if (normalY > -0.1f && normalY < Mathf.Clamp(WheelGroundNormalY, 0.05f, 0.9f))
                {
                    releaseNormal += contact.normal;
                }
            }

            if (releaseNormal.sqrMagnitude < 0.0001f)
            {
                return;
            }

            Vector3 forceDirection = (releaseNormal.normalized + Vector3.up * 0.18f).normalized;
            body.AddForce(forceDirection * LedgeContactReleaseForce, ForceMode.Force);
        }

        private void ScanZombieRoadkillOverlap()
        {
            if (!IsServer || body == null)
            {
                return;
            }

            Vector3 current = body.position;
            if (!hasRoadkillSweepPosition)
            {
                lastRoadkillSweepPosition = current;
                hasRoadkillSweepPosition = true;
            }

            float speedKph = Mathf.Max(
                body.linearVelocity.magnitude,
                GetVampireRamRememberedSpeed()) * 3.6f;
            if (speedKph < ZombieKnockdownMinSpeedKph)
            {
                lastRoadkillSweepPosition = current;
                return;
            }

            Vector3 velocity = body.linearVelocity;
            if (velocity.sqrMagnitude < 0.01f)
            {
                velocity = GetVampireRamImpactVelocity();
            }

            Vector3 lookAhead = Vector3.zero;
            if (velocity.sqrMagnitude > 0.01f)
            {
                lookAhead = velocity.normalized * Mathf.Max(0.35f, velocity.magnitude * Mathf.Max(0.01f, ZombieRoadkillLookAheadSeconds));
            }

            Vector3 start = lastRoadkillSweepPosition + Vector3.up * ZombieRoadkillSweepUpOffset;
            Vector3 end = current + lookAhead + Vector3.up * ZombieRoadkillSweepUpOffset;
            if ((end - start).sqrMagnitude < 0.01f)
            {
                end = start + transform.forward * 0.75f;
            }

            float radius = Mathf.Max(0.25f, ZombieRoadkillSweepRadius);
            int hitCount = Physics.OverlapCapsuleNonAlloc(start, end, radius, roadkillOverlapHits, ~0, QueryTriggerInteraction.Ignore);
            int zombiesHit = 0;

            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = roadkillOverlapHits[i];
                roadkillOverlapHits[i] = null;
                if (hit == null || hit.transform.IsChildOf(transform))
                {
                    continue;
                }

                MiniVanZombie zombie = hit.GetComponentInParent<MiniVanZombie>();
                if (zombie == null)
                {
                    continue;
                }

                Vector3 hitPoint = hit.ClosestPoint(current);
                if (TryRoadkillZombie(zombie, hitPoint))
                {
                    zombiesHit++;
                }
            }

            int transformZombiesChecked = 0;
            float closestZombieDistance = float.PositiveInfinity;
            MiniVanZombie closestZombie = null;
            zombiesHit += ScanZombieRoadkillTransforms(lastRoadkillSweepPosition, current + lookAhead, ref transformZombiesChecked, ref closestZombieDistance, ref closestZombie);

            if (DebugZombieRoadkill && Time.time >= nextRoadkillDebugTime)
            {
                nextRoadkillDebugTime = Time.time + 0.35f;
                string closestText = closestZombie != null ? closestZombie.name + " d=" + closestZombieDistance.ToString("0.00") : "none";
                Debug.Log("[MiniVanRoadkillScan] speed=" + speedKph.ToString("0.0") + "km/h hits=" + hitCount + " zombies=" + zombiesHit + " transformZombies=" + transformZombiesChecked + " closest=" + closestText + " radius=" + radius.ToString("0.00") + "/" + ZombieRoadkillTransformRadius.ToString("0.00") + " start=" + start.ToString("F2") + " end=" + end.ToString("F2"));
            }

            lastRoadkillSweepPosition = current;
        }

        private int ScanZombieRoadkillTransforms(Vector3 segmentStart, Vector3 segmentEnd, ref int zombiesChecked, ref float closestDistance, ref MiniVanZombie closestZombie)
        {
            MiniVanZombie[] zombies = MiniVanSceneScan.Get<MiniVanZombie>();
            zombiesChecked = zombies != null ? zombies.Length : 0;
            if (zombies == null || zombies.Length == 0)
            {
                return 0;
            }

            Vector2 a = new Vector2(segmentStart.x, segmentStart.z);
            Vector2 b = new Vector2(segmentEnd.x, segmentEnd.z);
            float radius = Mathf.Max(ZombieRoadkillSweepRadius, ZombieRoadkillTransformRadius);
            float verticalTolerance = Mathf.Max(0.5f, ZombieRoadkillVerticalTolerance);
            int killed = 0;

            for (int i = 0; i < zombies.Length; i++)
            {
                MiniVanZombie zombie = zombies[i];
                if (zombie == null || !zombie.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Vector3 zombiePosition = zombie.transform.position;
                float verticalDistance = Mathf.Abs(zombiePosition.y - transform.position.y);
                Vector2 p = new Vector2(zombiePosition.x, zombiePosition.z);
                float planarDistance = DistancePointToSegment(p, a, b);

                if (planarDistance < closestDistance)
                {
                    closestDistance = planarDistance;
                    closestZombie = zombie;
                }

                if (planarDistance > radius || verticalDistance > verticalTolerance)
                {
                    continue;
                }

                if (TryRoadkillZombie(zombie, zombiePosition + Vector3.up * 0.9f))
                {
                    killed++;
                }
            }

            return killed;
        }

        private static float DistancePointToSegment(Vector2 point, Vector2 segmentStart, Vector2 segmentEnd)
        {
            Vector2 segment = segmentEnd - segmentStart;
            float lengthSq = segment.sqrMagnitude;
            if (lengthSq <= 0.0001f)
            {
                return Vector2.Distance(point, segmentStart);
            }

            float t = Mathf.Clamp01(Vector2.Dot(point - segmentStart, segment) / lengthSq);
            Vector2 closest = segmentStart + segment * t;
            return Vector2.Distance(point, closest);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!IsServer || body == null || collision == null)
            {
                return;
            }

            if (collision.collider != null &&
                collision.collider.GetComponentInParent<MiniVanPlayer>() != null)
            {
                return;
            }

            // Props placed in the cabin (battery charger, etc.) must never damage or shove the van.
            if (collision.collider != null &&
                collision.collider.GetComponentInParent<MiniVanBatteryCharger>() != null)
            {
                return;
            }

            if (TryRoadkillZombieFromCollision(collision))
            {
                return;
            }

            ApplyCollisionDamage(collision);

            if (LandingImpactImpulse <= 0.001f)
            {
                return;
            }

            float strongestImpact = 0f;
            ContactPoint strongestContact = default;
            int contactCount = collision.contactCount;
            for (int i = 0; i < contactCount; i++)
            {
                ContactPoint contact = collision.GetContact(i);
                float normalY = Vector3.Dot(contact.normal.normalized, Vector3.up);
                if (normalY < 0.45f)
                {
                    continue;
                }

                float impact = Mathf.Abs(Vector3.Dot(collision.relativeVelocity, contact.normal.normalized));
                if (impact > strongestImpact)
                {
                    strongestImpact = impact;
                    strongestContact = contact;
                }
            }

            if (strongestImpact < MinLandingImpactSpeed)
            {
                return;
            }

            float impulse = body.mass * (strongestImpact - MinLandingImpactSpeed) * LandingImpactImpulse;
            body.AddForceAtPosition(strongestContact.normal.normalized * impulse, strongestContact.point, ForceMode.Impulse);
        }

        private void ApplyCollisionDamage(Collision collision)
        {
            if (collision == null || body == null)
            {
                return;
            }

            // A player capsule (e.g. standing up from a seat while driving) must never damage the van.
            if (collision.collider != null &&
                collision.collider.GetComponentInParent<MiniVanPlayer>() != null)
            {
                return;
            }

            MiniVanDetachedWheel detachedWheel = collision.collider != null
                ? collision.collider.GetComponentInParent<MiniVanDetachedWheel>()
                : null;
            if (detachedWheel != null)
            {
                return;
            }

            if (collision.collider != null &&
                collision.collider.GetComponentInParent<MiniVanBatteryCharger>() != null)
            {
                return;
            }

            float relativeSpeedKph = collision.relativeVelocity.magnitude * 3.6f;
            TryDetachWheelFromCollision(collision, relativeSpeedKph);
            if (relativeSpeedKph < CollisionDamageMinSpeedKph)
            {
                return;
            }

            float upwardContactWeight = 0f;
            int contactCount = collision.contactCount;
            for (int i = 0; i < contactCount; i++)
            {
                ContactPoint contact = collision.GetContact(i);
                upwardContactWeight = Mathf.Max(upwardContactWeight, Vector3.Dot(contact.normal.normalized, Vector3.up));
            }

            // Rolling / landing on floor must not chew health (channel seams caused death).
            if (upwardContactWeight >= 0.62f)
            {
                return;
            }

            float groundLandingDiscount = Mathf.InverseLerp(0.45f, 0.9f, upwardContactWeight) * 0.55f;
            float damage = (relativeSpeedKph - CollisionDamageMinSpeedKph) * CollisionDamagePerKph * (1f - groundLandingDiscount);
            if (damage > 0.1f)
            {
                ApplyVehicleDamage(damage, "collision");
            }
        }

        private void TryDetachWheelFromCollision(Collision collision, float relativeSpeedKph)
        {
            if (DetachedWheelIndex.Value >= 0 || collision == null)
            {
                return;
            }

            if (collision.collider != null &&
                collision.collider.GetComponentInParent<MiniVanPlayer>() != null)
            {
                return;
            }

            float strongestUpwardImpact = 0f;
            Vector3 strongestPoint = transform.position;
            int contactCount = collision.contactCount;
            for (int i = 0; i < contactCount; i++)
            {
                ContactPoint contact = collision.GetContact(i);
                float normalY = Vector3.Dot(contact.normal.normalized, Vector3.up);
                if (normalY < 0.32f)
                {
                    continue;
                }

                float impact = Mathf.Abs(Vector3.Dot(collision.relativeVelocity, contact.normal.normalized));
                if (impact > strongestUpwardImpact)
                {
                    strongestUpwardImpact = impact;
                    strongestPoint = contact.point;
                }
            }

            bool hardFall = strongestUpwardImpact >= WheelDetachFallVerticalSpeed;
            bool fastPitLikeHit = relativeSpeedKph >= WheelDetachPitSpeedKph &&
                                  strongestUpwardImpact >= FallDamageMinVerticalSpeed;
            if (!hardFall && !fastPitLikeHit)
            {
                return;
            }

            DetachNearestWheel(strongestPoint, "collision-impact");
        }

        private void DetachNearestWheel(Vector3 worldPoint, string reason)
        {
            int bestIndex = -1;
            float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < 4; i++)
            {
                Transform wheel = GetWheelVisual(i);
                Vector3 position = wheel != null ? wheel.position : transform.TransformPoint(GetDefaultWheelLocalPosition(i));
                float distance = (position - worldPoint).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestIndex = i;
                }
            }

            if (bestIndex >= 0)
            {
                DetachWheel(bestIndex, reason);
            }
        }

        private bool TryRoadkillZombieFromCollision(Collision collision)
        {
            MiniVanZombie zombie = collision.collider != null ? collision.collider.GetComponentInParent<MiniVanZombie>() : null;
            Vector3 hitPoint = collision.contactCount > 0 ? collision.GetContact(0).point : collision.transform.position;

            if (zombie == null)
            {
                int contactCount = collision.contactCount;
                for (int i = 0; i < contactCount; i++)
                {
                    ContactPoint contact = collision.GetContact(i);
                    Collider otherCollider = contact.otherCollider;
                    if (otherCollider == null || otherCollider.transform.IsChildOf(transform))
                    {
                        otherCollider = contact.thisCollider;
                    }

                    if (otherCollider == null || otherCollider.transform.IsChildOf(transform))
                    {
                        continue;
                    }

                    zombie = otherCollider.GetComponentInParent<MiniVanZombie>();
                    hitPoint = contact.point;
                    if (zombie != null)
                    {
                        break;
                    }
                }
            }

            return TryRoadkillZombie(zombie, hitPoint);
        }

        public bool TryRoadkillZombie(MiniVanZombie zombie, Vector3 hitPoint)
        {
            if (!IsServer || body == null || zombie == null)
            {
                return false;
            }

            MiniVanVampire vampire = zombie as MiniVanVampire;
            if (vampire != null)
            {
                return TryRamVampire(vampire, hitPoint);
            }

            Vector3 planarVelocity = Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up);
            float speedKph = planarVelocity.magnitude * 3.6f;
            Vector3 toZombie = Vector3.ProjectOnPlane(zombie.transform.position - body.worldCenterOfMass, Vector3.up);
            float closingSpeedKph = toZombie.sqrMagnitude > 0.01f
                ? Mathf.Max(0f, Vector3.Dot(planarVelocity, toZombie.normalized)) * 3.6f
                : 0f;
            if (speedKph < ZombieKnockdownMinSpeedKph || closingSpeedKph < ZombieKnockdownMinSpeedKph)
            {
                return false;
            }
            if (speedKph < ZombieRoadkillMinSpeedKph)
            {
                return TryKnockdownZombieBeforeExplosion(zombie, hitPoint, closingSpeedKph);
            }

            Vector3 zombiePosition = zombie.transform.position;
            Vector3 impactVelocity = planarVelocity;
            MiniVanInventoryItem ingredient = GetRandomRoadkillIngredient();
            int seed = Random.Range(int.MinValue, int.MaxValue);

            if (!zombie.ServerRoadkill(impactVelocity))
            {
                return false;
            }

            ApplyRoadkillSpeedLossOnce();
            SpawnZombieRoadkillClientRpc(zombiePosition + Vector3.up * 0.95f, hitPoint, impactVelocity, (int)ingredient, seed);

            if (DebugVehicle)
            {
                Debug.Log("[MiniVanRoadkill] zombie hit speed=" + speedKph.ToString("0.0") + "km/h body parts spawned velocityLoss=" + (ZombieRoadkillSpeedLoss * 100f).ToString("0") + "%");
            }

            return true;
        }

        /// <summary>
        /// Contact ram requested by the vampire itself. The vampire only calls this when it owns
        /// its AI (server or offline play), so the server-only roadkill sweep is not required.
        /// </summary>
        public bool TryRamVampireFromContact(MiniVanVampire vampire, Vector3 hitPoint)
        {
            return TryRamVampire(vampire, hitPoint);
        }

        private bool TryRamVampire(MiniVanVampire vampire, Vector3 hitPoint)
        {
            if (vampire == null || body == null || !vampire.gameObject.activeInHierarchy)
            {
                return false;
            }

            if (vampire.IsRamStunned)
            {
                return false;
            }

            Vector3 planarVelocity = GetVampireRamImpactVelocity();
            float speedKph = planarVelocity.magnitude * 3.6f;
            if (speedKph < VampireRamMinSpeedKph)
            {
                return false;
            }

            if (vampireRamTimes.TryGetValue(vampire, out float nextRamTime) && Time.time < nextRamTime)
            {
                return false;
            }

            float damageFraction = ResolveVampireRamDamageFraction(speedKph);
            Vector3 hitOrigin = body.worldCenterOfMass;
            if (!vampire.ServerTakeVehicleRam(hitOrigin, planarVelocity, damageFraction, this))
            {
                return false;
            }

            vampireRamTimes[vampire] = Time.time + Mathf.Max(0.05f, VampireRamCooldownSeconds);
            RestoreVelocityAfterVampireRam();

            if (DebugVehicle || DebugZombieRoadkill)
            {
                Debug.Log("[MiniVanVampireRam] speed=" + speedKph.ToString("0.0") + "km/h damage=" +
                          (damageFraction * 100f).ToString("0") + "% velocityLoss=" +
                          (ZombieRoadkillSpeedLoss * 100f).ToString("0") + "%");
            }

            return true;
        }

        private float ResolveVampireRamDamageFraction(float speedKph)
        {
            if (speedKph >= VampireRamDamage50SpeedKph)
            {
                return 0.50f;
            }

            if (speedKph >= VampireRamDamage20SpeedKph)
            {
                return 0.20f;
            }

            if (speedKph >= VampireRamDamage10SpeedKph)
            {
                return 0.10f;
            }

            return 0f;
        }

        private void CacheStableVelocityForVampireRam()
        {
            if (body == null || body.linearVelocity.sqrMagnitude <= 1f)
            {
                return;
            }

            // Keep the fastest sample inside the memory window: the ram frame itself is already
            // slowed by the contact, and copying it back would preserve the stop we want to undo.
            bool memoryExpired = Time.time - lastStableVelocityTime > VampireRamVelocityMemorySeconds;
            if (!memoryExpired &&
                body.linearVelocity.sqrMagnitude < lastStableLinearVelocity.sqrMagnitude)
            {
                return;
            }

            lastStableLinearVelocity = body.linearVelocity;
            lastStableAngularVelocity = body.angularVelocity;
            lastStableVelocityTime = Time.time;
        }

        /// <summary>
        /// A rammed vampire must not brake the van. Physics can bleed speed for several fixed steps
        /// after the hit, so the pre-hit velocity is held briefly instead of restored only once.
        /// </summary>
        private void ApplyVampireRamVelocityHold()
        {
            if (body == null || Time.time > vampireRamHoldUntil)
            {
                return;
            }

            Vector3 planar = Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up);
            Vector3 target = Vector3.ProjectOnPlane(vampireRamHoldVelocity, Vector3.up);
            if (target.sqrMagnitude <= planar.sqrMagnitude)
            {
                return;
            }

            body.linearVelocity = new Vector3(target.x, body.linearVelocity.y, target.z);
        }

        private float GetVampireRamRememberedSpeed()
        {
            if (Time.time - lastStableVelocityTime > VampireRamVelocityMemorySeconds)
            {
                return 0f;
            }

            return lastStableLinearVelocity.magnitude;
        }

        private Vector3 GetVampireRamImpactVelocity()
        {
            Vector3 current = Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up);
            if (Time.time - lastStableVelocityTime > VampireRamVelocityMemorySeconds)
            {
                return current;
            }

            Vector3 stable = Vector3.ProjectOnPlane(lastStableLinearVelocity, Vector3.up);
            return stable.sqrMagnitude > current.sqrMagnitude ? stable : current;
        }

        private void RestoreVelocityAfterVampireRam()
        {
            if (body != null && Time.time - lastStableVelocityTime <= VampireRamVelocityMemorySeconds)
            {
                vampireRamHoldVelocity = lastStableLinearVelocity *
                                         Mathf.Clamp01(1f - ZombieRoadkillSpeedLoss);
                vampireRamHoldUntil = Time.time + VampireRamVelocityHoldSeconds;
            }

            if (body != null &&
                Time.time - lastStableVelocityTime <= VampireRamVelocityMemorySeconds &&
                lastStableLinearVelocity.sqrMagnitude > body.linearVelocity.sqrMagnitude + 1f)
            {
                body.linearVelocity = lastStableLinearVelocity;
                body.angularVelocity = lastStableAngularVelocity;
            }

            ApplyRoadkillSpeedLossOnce();
        }

        private bool TryKnockdownZombieBeforeExplosion(MiniVanZombie zombie, Vector3 hitPoint, float speedKph)
        {
            if (speedKph < ZombieKnockdownMinSpeedKph)
            {
                return false;
            }

            Vector3 impactVelocity = body != null ? body.linearVelocity : transform.forward * (speedKph / 3.6f);
            int seed = Random.Range(int.MinValue, int.MaxValue);
            MiniVanInventoryItem ingredient = GetRandomRoadkillIngredient();

            if (!zombie.ServerKnockdownForRoadkill(transform.position, Mathf.Max(0.05f, ZombieKnockdownExplodeDelay)))
            {
                return false;
            }

            ApplyRoadkillSpeedLossOnce();
            StartCoroutine(DelayedZombieRoadkillCoroutine(zombie, hitPoint, impactVelocity, (int)ingredient, seed, Mathf.Max(0.05f, ZombieKnockdownExplodeDelay)));

            if (DebugVehicle)
            {
                Debug.Log("[MiniVanRoadkill] zombie knocked down speed=" + speedKph.ToString("0.0") + "km/h explodeIn=" + ZombieKnockdownExplodeDelay.ToString("0.00") + "s ingredient=" + ingredient);
            }

            return true;
        }

        private void ApplyRoadkillSpeedLossOnce()
        {
            if (body == null || lastRoadkillSpeedLossTick == roadkillFixedTick)
            {
                return;
            }

            lastRoadkillSpeedLossTick = roadkillFixedTick;
            body.linearVelocity *= Mathf.Clamp01(1f - ZombieRoadkillSpeedLoss);
        }

        private IEnumerator DelayedZombieRoadkillCoroutine(MiniVanZombie zombie, Vector3 hitPoint, Vector3 impactVelocity, int ingredientValue, int seed, float delay)
        {
            yield return new WaitForSeconds(delay);

            if (zombie == null)
            {
                yield break;
            }

            Vector3 zombiePosition = zombie.transform.position;
            if (!zombie.ServerRoadkill(impactVelocity))
            {
                yield break;
            }

            SpawnZombieRoadkillClientRpc(zombiePosition + Vector3.up * 0.55f, hitPoint, impactVelocity, ingredientValue, seed);
        }

        [ClientRpc]
        private void SpawnZombieRoadkillClientRpc(Vector3 zombiePosition, Vector3 hitPoint, Vector3 impactVelocity, int ingredientValue, int seed, ClientRpcParams clientRpcParams = default)
        {
            Random.State previousState = Random.state;
            Random.InitState(seed);

            SpawnRoadkillCubesLocal(zombiePosition, impactVelocity);
            Random.state = previousState;
        }

        private void SpawnRoadkillCubesLocal(Vector3 position, Vector3 impactVelocity)
        {
            int desiredCount = Mathf.Clamp(ZombieRoadkillCubeCount, 4, 140);
            int count = ReserveRoadkillCubeBudget(desiredCount);
            if (count <= 0)
            {
                return;
            }

            if (DebugZombieRoadkill && count < desiredCount)
            {
                Debug.Log("[MiniVanRoadkillDebris] budgeted cubes " + count + "/" + desiredCount + " frameTotal=" + roadkillDebrisCubesThisFrame + " max=" + ZombieRoadkillMaxCubesPerFrame);
            }

            Material material = GetRoadkillCubeMaterial();
            for (int i = 0; i < count; i++)
            {
                GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = "Zombie Red Cube";
                cube.transform.position = position + Random.insideUnitSphere * 0.42f;
                float size = Random.Range(0.14f, 0.36f);
                cube.transform.localScale = Vector3.one * size;

                Renderer renderer = cube.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterial = material;
                }

                Collider cubeCollider = cube.GetComponent<Collider>();
                if (cubeCollider != null && !ZombieRoadkillCubePhysicsCollisions)
                {
                    cubeCollider.enabled = false;
                }

                Rigidbody cubeBody = cube.AddComponent<Rigidbody>();
                cubeBody.mass = 0.035f;
                cubeBody.useGravity = true;
                cubeBody.interpolation = RigidbodyInterpolation.Interpolate;
                cubeBody.collisionDetectionMode = CollisionDetectionMode.Discrete;
                cubeBody.linearDamping = 0.18f;
                cubeBody.angularDamping = 0.25f;
                if (ZombieRoadkillCubePhysicsCollisions)
                {
                    IgnoreVehicleCollisions(cube);
                }

                Vector3 burst = (Random.onUnitSphere + Vector3.up * Random.Range(0.35f, 1.35f)).normalized * Random.Range(ZombieRoadkillCubeImpulse * 0.55f, ZombieRoadkillCubeImpulse * 1.25f);
                cubeBody.linearVelocity = impactVelocity * Random.Range(0.12f, 0.28f) + burst;
                cubeBody.angularVelocity = Random.insideUnitSphere * Random.Range(12f, 30f);

                MiniVanShrinkAndDestroy shrink = cube.AddComponent<MiniVanShrinkAndDestroy>();
                shrink.Lifetime = Random.Range(ZombieRoadkillCubeLifetimeRange.x, ZombieRoadkillCubeLifetimeRange.y);
            }
        }

        private int ReserveRoadkillCubeBudget(int desiredCount)
        {
            int frame = Time.frameCount;
            if (roadkillDebrisFrame != frame)
            {
                roadkillDebrisFrame = frame;
                roadkillDebrisCubesThisFrame = 0;
            }

            int maxPerFrame = Mathf.Max(0, ZombieRoadkillMaxCubesPerFrame);
            int remaining = maxPerFrame - roadkillDebrisCubesThisFrame;
            if (remaining <= 0)
            {
                return 0;
            }

            int perZombieCount = roadkillDebrisCubesThisFrame > 0
                ? Mathf.Min(desiredCount, Mathf.Max(1, ZombieRoadkillCrowdCubeCount))
                : desiredCount;
            int reserved = Mathf.Clamp(perZombieCount, 0, remaining);
            roadkillDebrisCubesThisFrame += reserved;
            return reserved;
        }

        private void SpawnRoadkillIngredientLocal(MiniVanInventoryItem item, Vector3 position, Vector3 impactVelocity, string dropName)
        {
            GameObject prefab = Resources.Load<GameObject>("PizzaLoop/PizzaItem_" + item);
            GameObject pickup = prefab != null ? Instantiate(prefab, position, Random.rotation) : CreateRoadkillIngredientFallback(item, position);
            pickup.name = dropName;

            MiniVanPizzaItem pizzaItem = pickup.GetComponent<MiniVanPizzaItem>();
            if (pizzaItem == null)
            {
                pizzaItem = pickup.AddComponent<MiniVanPizzaItem>();
            }

            pizzaItem.enabled = true;
            pizzaItem.Item = item;
            pizzaItem.Type = MiniVanPizzaItemType.Ingredient;
            pizzaItem.PickupRadius = 2.15f;
            pizzaItem.CanHoldInHands = true;
            pizzaItem.CanPutInInventory = true;

            foreach (Collider collider in pickup.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = true;
                collider.isTrigger = false;
            }
            IgnoreVehicleCollisions(pickup);

            Rigidbody pickupBody = pickup.GetComponent<Rigidbody>();
            if (pickupBody == null)
            {
                pickupBody = pickup.AddComponent<Rigidbody>();
            }

            pickupBody.isKinematic = false;
            pickupBody.useGravity = true;
            pickupBody.detectCollisions = true;
            pickupBody.mass = 0.22f;
            pickupBody.interpolation = RigidbodyInterpolation.Interpolate;
            pickupBody.collisionDetectionMode = CollisionDetectionMode.Discrete;
            pickupBody.linearDamping = 0.12f;
            pickupBody.angularDamping = 0.45f;
            pickupBody.maxLinearVelocity = 22f;
            pickupBody.linearVelocity = impactVelocity * 0.08f + Vector3.up * 1.8f + Random.insideUnitSphere * 1.2f;
            pickupBody.angularVelocity = Random.insideUnitSphere * 5f;
        }

        private static void IgnoreVehicleCollisions(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            Collider[] objectColliders = root.GetComponentsInChildren<Collider>(true);
            if (objectColliders == null || objectColliders.Length == 0)
            {
                return;
            }

            MiniVanVehicle[] vehicles = MiniVanSceneScan.Get<MiniVanVehicle>();
            for (int v = 0; v < vehicles.Length; v++)
            {
                MiniVanVehicle vehicle = vehicles[v];
                if (vehicle == null)
                {
                    continue;
                }

                Collider[] vehicleColliders = vehicle.GetComponentsInChildren<Collider>(true);
                for (int i = 0; i < objectColliders.Length; i++)
                {
                    Collider objectCollider = objectColliders[i];
                    if (objectCollider == null)
                    {
                        continue;
                    }

                    for (int c = 0; c < vehicleColliders.Length; c++)
                    {
                        Collider vehicleCollider = vehicleColliders[c];
                        if (vehicleCollider == null || vehicleCollider.isTrigger || vehicleCollider is WheelCollider)
                        {
                            continue;
                        }

                        Physics.IgnoreCollision(objectCollider, vehicleCollider, true);
                    }
                }
            }
        }

        private static GameObject CreateRoadkillIngredientFallback(MiniVanInventoryItem item, Vector3 position)
        {
            GameObject pickup = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pickup.transform.position = position;
            pickup.transform.localScale = Vector3.one * 0.32f;

            Renderer renderer = pickup.GetComponent<Renderer>();
            if (renderer != null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null)
                {
                    shader = Shader.Find("Standard");
                }

                Material material = new Material(shader);
                ApplyRoadkillIngredientColor(material, GetRoadkillIngredientColor(item));
                renderer.material = material;
            }

            return pickup;
        }

        private static MiniVanInventoryItem GetRandomRoadkillIngredient()
        {
            switch (Random.Range(0, 5))
            {
                case 0:
                    return MiniVanInventoryItem.Flour;
                case 1:
                    return MiniVanInventoryItem.Water;
                case 2:
                    return MiniVanInventoryItem.TomatoPaste;
                case 3:
                    return MiniVanInventoryItem.Cheese;
                default:
                    return MiniVanInventoryItem.Sausage;
            }
        }

        private static Color GetRoadkillIngredientColor(MiniVanInventoryItem item)
        {
            switch (item)
            {
                case MiniVanInventoryItem.Flour:
                    return new Color(0.92f, 0.88f, 0.76f);
                case MiniVanInventoryItem.Water:
                    return new Color(0.25f, 0.58f, 1f);
                case MiniVanInventoryItem.TomatoPaste:
                    return new Color(0.75f, 0.06f, 0.04f);
                case MiniVanInventoryItem.Cheese:
                    return new Color(1f, 0.82f, 0.18f);
                case MiniVanInventoryItem.Sausage:
                    return new Color(0.62f, 0.18f, 0.13f);
                default:
                    return Color.white;
            }
        }

        private static void ApplyRoadkillIngredientColor(Material material, Color color)
        {
            if (material == null)
            {
                return;
            }

            material.color = color;
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
        }

        private static Material GetRoadkillCubeMaterial()
        {
            if (zombieRoadkillCubeMaterial != null)
            {
                return zombieRoadkillCubeMaterial;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            zombieRoadkillCubeMaterial = new Material(shader);
            zombieRoadkillCubeMaterial.name = "Zombie Roadkill Red";
            ApplyRoadkillIngredientColor(zombieRoadkillCubeMaterial, new Color(0.78f, 0.01f, 0.01f, 1f));
            return zombieRoadkillCubeMaterial;
        }

        private float GetMinimumUsefulSpeedKph(MiniVanGear gear)
        {
            switch (gear)
            {
                case MiniVanGear.Second:
                    return SecondGearMinSpeedKph;
                case MiniVanGear.Third:
                    return ThirdGearMinSpeedKph;
                case MiniVanGear.Fourth:
                    return FourthGearMinSpeedKph;
                case MiniVanGear.Fifth:
                    return FifthGearMinSpeedKph;
                default:
                    return 0f;
            }
        }

        private float GetGearLoadFactor(MiniVanGear gear, float speedKph)
        {
            float minimumUsefulSpeed = GetMinimumUsefulSpeedKph(gear);
            if (minimumUsefulSpeed <= 0.01f)
            {
                return 1f;
            }

            float speedRatio = Mathf.Clamp01(speedKph / minimumUsefulSpeed);
            float smoothRatio = speedRatio * speedRatio * (3f - 2f * speedRatio);

            // Wrong gear / too slow: higher gears choke harder (readable beater punishment).
            float floor;
            switch (gear)
            {
                case MiniVanGear.Second:
                    floor = 0.22f;
                    break;
                case MiniVanGear.Third:
                    floor = 0.14f;
                    break;
                case MiniVanGear.Fourth:
                    floor = 0.08f;
                    break;
                case MiniVanGear.Fifth:
                    floor = 0.05f;
                    break;
                default:
                    floor = 0.28f;
                    break;
            }

            float uphill = GetUphillSeverity01();
            floor *= Mathf.Lerp(1f, 0.4f, uphill);
            return Mathf.Lerp(floor, 1f, smoothRatio);
        }

        private bool IsEngineTachHealthyForGearHold()
        {
            float rpm01 = Mathf.Clamp01(Mathf.InverseLerp(IdleRpm, RedlineRpm, engineRpm));
            float bandLow = Mathf.Clamp(SweetSpotCenterRpm01, 0.15f, 0.85f) - Mathf.Max(0.04f, SweetSpotHalfWidthRpm01);
            return rpm01 >= bandLow - 0.03f || EvaluateEngineSweetSpot01(engineRpm) > 0.2f;
        }

        private void UpdateGearStress(MiniVanGear gear, bool luggingGear, float minimumUsefulSpeed, float speedKph)
        {
            // Readable stall rule: only die when the gear is too high AND the tach has left
            // the green band. Speed-below-threshold alone must never kill a healthy engine.
            bool tachHealthy = IsEngineTachHealthyForGearHold();
            bool deepLugging = luggingGear &&
                               speedKph + 2f < minimumUsefulSpeed &&
                               !tachHealthy;

            if (!deepLugging)
            {
                gearStressTimer = Mathf.Max(0f, gearStressTimer - Time.fixedDeltaTime * 1.8f);
                return;
            }

            float severity = Mathf.InverseLerp(minimumUsefulSpeed, 0f, speedKph);
            float stallWindow = Mathf.Max(4f, GearStallSeconds);
            float stallAfterSeconds = Mathf.Lerp(stallWindow, stallWindow * 0.5f, severity);
            stallAfterSeconds *= Mathf.Lerp(1f, 0.55f, GetUphillSeverity01());
            gearStressTimer += Time.fixedDeltaTime;

            if (gearStressTimer >= stallAfterSeconds)
            {
                EngineOn.Value = false;
                throttleInput = 0f;
                gearStressTimer = 0f;
                EngineLoad.Value = 0f;

                if (DebugVehicle && Time.time - lastStallDebugTime > 0.5f)
                {
                    lastStallDebugTime = Time.time;
                    Debug.Log("[MiniVanPhysics] engine stalled by lugging gear=" + gear +
                              " minUseful=" + minimumUsefulSpeed.ToString("0.0") +
                              "km/h speed=" + speedKph.ToString("0.0") +
                              "km/h rpm=" + engineRpm.ToString("0"));
                }
            }
        }


private float GetTorqueMultiplier(MiniVanGear gear)
        {
            // Beater spread: slow to build speed on high gears, but enough shove to hold cruise.
            switch (gear)
            {
                case MiniVanGear.First:
                    return 2.35f;
                case MiniVanGear.Second:
                    return 1.05f;
                case MiniVanGear.Third:
                    return 0.96f;
                case MiniVanGear.Fourth:
                    return 0.84f;
                case MiniVanGear.Fifth:
                    return 0.70f;
                case MiniVanGear.Reverse:
                    return 1.55f;
                default:
                    return 0f;
            }
        }

        /// <summary>
        /// Once rolling in a high gear on mild grade, restore enough pull to hold speed with light throttle.
        /// Acceleration into the gear stays lazy via RPM curve / inertia — this only fights cruise sag.
        /// </summary>
        private float GetCruiseSustainFactor(MiniVanGear gear, float speedKph, float throttle, float uphillSeverity)
        {
            if (!MiniVanGearUtility.IsForward(gear) || gear == MiniVanGear.First)
            {
                return 1f;
            }

            if (throttle < 0.12f || uphillSeverity > 0.4f)
            {
                return 1f;
            }

            float minUseful = GetMinimumUsefulSpeedKph(gear);
            float gearTop = Mathf.Max(1f, MiniVanGearUtility.MaxForwardSpeed(gear) * 3.6f);
            if (speedKph + 0.5f < minUseful)
            {
                return 1f;
            }

            float through = Mathf.Clamp01(Mathf.InverseLerp(minUseful, gearTop, speedKph));
            // Strongest sustain in the mid cruise band; weak near launch and absolute top.
            float cruiseWindow = 1f - Mathf.Abs(through - 0.42f) / 0.42f;
            cruiseWindow = Mathf.Clamp01(cruiseWindow);
            float gearBoost = gear == MiniVanGear.Third ? 1.18f
                : gear == MiniVanGear.Fourth ? 1.26f
                : gear == MiniVanGear.Fifth ? 1.34f
                : 1.1f;
            float throttleBlend = Mathf.Clamp01(Mathf.InverseLerp(0.12f, 0.55f, throttle));
            return Mathf.Lerp(1f, gearBoost, cruiseWindow * throttleBlend * (1f - uphillSeverity * 0.85f));
        }

        private static float GetUphillDriveFactor(MiniVanGear gear, float uphillSeverity)
        {
            float severity = Mathf.Clamp01(uphillSeverity);
            switch (gear)
            {
                case MiniVanGear.First:
                    return Mathf.Lerp(1f, 0.88f, severity);
                case MiniVanGear.Second:
                    return Mathf.Lerp(1f, 0.42f, severity);
                case MiniVanGear.Third:
                    return Mathf.Lerp(1f, 0.22f, severity);
                case MiniVanGear.Fourth:
                    return Mathf.Lerp(1f, 0.12f, severity);
                case MiniVanGear.Fifth:
                    return Mathf.Lerp(1f, 0.06f, severity);
                default:
                    return Mathf.Lerp(1f, 0.35f, severity);
            }
        }

        private static float GetEngineBrakeGearFactor(MiniVanGear gear)
        {
            switch (gear)
            {
                case MiniVanGear.First:
                    return 1.35f;
                case MiniVanGear.Second:
                    return 1.05f;
                case MiniVanGear.Third:
                    return 0.65f;
                case MiniVanGear.Fourth:
                    return 0.4f;
                case MiniVanGear.Fifth:
                    return 0.28f;
                case MiniVanGear.Reverse:
                    return 1.1f;
                default:
                    return 0.5f;
            }
        }

        private static float GetHillGradeGearCost(MiniVanGear gear)
        {
            switch (gear)
            {
                case MiniVanGear.First:
                    return 0.22f;
                case MiniVanGear.Second:
                    return 0.75f;
                case MiniVanGear.Third:
                    return 1.25f;
                case MiniVanGear.Fourth:
                    return 1.7f;
                case MiniVanGear.Fifth:
                    return 2.1f;
                default:
                    return 0.9f;
            }
        }

        /// <summary>
        /// Extra climb resistance on high gears so hills punish wrong gear choice without faking a lower top speed.
        /// </summary>
        private void ApplyHillGradeLoad(MiniVanGear gear, int groundedWheels)
        {
            if (body == null || groundedWheels < 2 || !MiniVanGearUtility.IsForward(gear))
            {
                return;
            }

            float uphill = GetUphillSeverity01();
            if (uphill < 0.05f)
            {
                return;
            }

            Vector3 groundNormal = GetAverageWheelGroundNormal();
            Vector3 uphillDir = Vector3.ProjectOnPlane(transform.forward, groundNormal);
            if (uphillDir.sqrMagnitude < 0.001f)
            {
                return;
            }

            uphillDir.Normalize();
            float cost = GetHillGradeGearCost(gear);
            float drag = body.mass * Mathf.Abs(Physics.gravity.y) * uphill * cost * 0.32f;
            // Throttle in the power band fights the grade; wrong gear still loses.
            float fight = Mathf.Clamp01(throttleInput) * EvaluateEngineSweetSpot01(engineRpm);
            drag *= Mathf.Lerp(1f, 0.62f, fight);
            body.AddForce(-uphillDir * drag, ForceMode.Force);
        }

        private void ApplyLowGearClimbAssist(MiniVanGear gear, int groundedWheels)
        {
            if (!IsLowGear(gear) || !EngineOn.Value || throttleInput <= 0.05f || groundedWheels < 2)
            {
                return;
            }

            Vector3 groundNormal = GetAverageWheelGroundNormal();
            float slopeAngle = Vector3.Angle(groundNormal, Vector3.up);
            if (slopeAngle <= 8f || slopeAngle > LowGearMaxSlopeAngle + 3f)
            {
                return;
            }

            Vector3 uphill = Vector3.ProjectOnPlane(transform.forward, groundNormal);
            if (uphill.sqrMagnitude < 0.001f)
            {
                return;
            }

            Vector3 uphillDirection = uphill.normalized;
            float slopeFactor = Mathf.InverseLerp(8f, LowGearMaxSlopeAngle, slopeAngle);
            float uphillSpeedKph = Vector3.Dot(body.linearVelocity, uphillDirection) * 3.6f;
            float targetCrawlKph = Mathf.Lerp(16f, 20f, slopeFactor);
            float speedDeficit = Mathf.Clamp01((targetCrawlKph - uphillSpeedKph) / Mathf.Max(0.1f, targetCrawlKph));
            Vector3 gravityAlongSlope = Vector3.ProjectOnPlane(Physics.gravity, groundNormal);
            float gravityForce = Mathf.Max(0f, Vector3.Dot(uphillDirection, -gravityAlongSlope)) * body.mass;
            float gravityCompensation = gravityForce * Mathf.Max(0.5f, LowGearGravityCompensation) * Mathf.Lerp(0.82f, 1.12f, slopeFactor) * throttleInput;
            float crawlForce = LowGearClimbAssist * throttleInput * speedDeficit * Mathf.Lerp(0.75f, 1.55f, slopeFactor);
            float maxAssistForce = body.mass * Mathf.Abs(Physics.gravity.y) * 2.05f;
            body.AddForce(uphillDirection * Mathf.Min(maxAssistForce, gravityCompensation + crawlForce), ForceMode.Force);
        }

        private void ApplySlopeSideHold(int groundedWheels)
        {
            if (body == null || handbrakeInput || groundedWheels < 2 || SlopeSideHoldAcceleration <= 0.01f)
            {
                return;
            }

            Vector3 groundNormal = GetAverageWheelGroundNormal();
            if (groundNormal.sqrMagnitude < 0.001f)
            {
                return;
            }

            float slopeAngle = Vector3.Angle(groundNormal, Vector3.up);
            if (slopeAngle < 5f)
            {
                return;
            }

            Vector3 surfaceRight = Vector3.ProjectOnPlane(transform.right, groundNormal);
            if (surfaceRight.sqrMagnitude < 0.001f)
            {
                return;
            }

            surfaceRight.Normalize();
            float lateralSpeed = Vector3.Dot(body.linearVelocity, surfaceRight);
            if (Mathf.Abs(lateralSpeed) < 0.04f)
            {
                return;
            }

            float slopeFactor = Mathf.InverseLerp(5f, Mathf.Max(8f, LowGearMaxSlopeAngle), slopeAngle);
            float holdAcceleration = Mathf.Min(MaxSlopeSideHoldAcceleration, Mathf.Abs(lateralSpeed) * SlopeSideHoldAcceleration * Mathf.Lerp(0.35f, 1f, slopeFactor));
            body.AddForce(-surfaceRight * Mathf.Sign(lateralSpeed) * holdAcceleration, ForceMode.Acceleration);
        }

        private void ApplyParkingBrakeHold(int groundedWheels)
        {
            if (body == null || !handbrakeInput || groundedWheels < 2)
            {
                return;
            }

            Vector3 groundNormal = GetAverageWheelGroundNormal();
            Vector3 surfaceVelocity = Vector3.ProjectOnPlane(body.linearVelocity, groundNormal);
            float surfaceSpeed = surfaceVelocity.magnitude;
            bool driftRequested = Mathf.Abs(smoothedSteeringInput) >= HandbrakeDriftSteerThreshold && surfaceSpeed * 3.6f >= HandbrakeDriftMinSpeedKph;
            if (driftRequested)
            {
                return;
            }

            if (surfaceSpeed <= ParkingBrakeStopSpeed)
            {
                body.linearVelocity -= surfaceVelocity;
                body.angularVelocity = Vector3.MoveTowards(body.angularVelocity, Vector3.zero, ParkingBrakeHoldAcceleration * Time.fixedDeltaTime * 0.35f);
                return;
            }

            float holdAcceleration = Mathf.Max(0f, ParkingBrakeHoldAcceleration);
            body.AddForce(-surfaceVelocity.normalized * holdAcceleration, ForceMode.Acceleration);
            body.angularVelocity = Vector3.MoveTowards(body.angularVelocity, Vector3.zero, holdAcceleration * Time.fixedDeltaTime * 0.08f);
        }

        private Vector3 GetAverageWheelGroundNormal()
        {
            Vector3 normalSum = Vector3.zero;
            int count = 0;
            AddWheelNormal(frontLeftWheelCollider, ref normalSum, ref count);
            AddWheelNormal(frontRightWheelCollider, ref normalSum, ref count);
            AddWheelNormal(rearLeftWheelCollider, ref normalSum, ref count);
            AddWheelNormal(rearRightWheelCollider, ref normalSum, ref count);
            return count > 0 ? normalSum.normalized : Vector3.up;
        }

        /// <summary>
        /// 0 on flat ground / descents, up to 1 on steep climbs the van is actually driving up.
        /// </summary>
        private float GetUphillSeverity01()
        {
            return GetSlopeAlignmentSeverity01(climbing: true);
        }

        /// <summary>
        /// 0 on flat / climbs, up to 1 when pointed downhill on a meaningful grade.
        /// </summary>
        private float GetDownhillSeverity01()
        {
            return GetSlopeAlignmentSeverity01(climbing: false);
        }

        private float GetSlopeAlignmentSeverity01(bool climbing)
        {
            Vector3 groundNormal = GetAverageWheelGroundNormal();
            float slopeAngle = Vector3.Angle(groundNormal, Vector3.up);
            if (slopeAngle < 4f)
            {
                return 0f;
            }

            Vector3 downhill = Vector3.ProjectOnPlane(Physics.gravity, groundNormal);
            if (downhill.sqrMagnitude < 0.0001f)
            {
                return 0f;
            }

            float alignment = Vector3.Dot(transform.forward, climbing ? -downhill.normalized : downhill.normalized);
            if (alignment <= 0.1f)
            {
                return 0f;
            }

            float steepness = Mathf.InverseLerp(4f, 30f, slopeAngle);
            return Mathf.Clamp01(steepness * alignment);
        }

private void AddWheelNormal(WheelCollider wheelCollider, ref Vector3 normalSum, ref int count)
        {
            if (IsWheelOnValidGround(wheelCollider, out WheelHit hit))
            {
                normalSum += hit.normal;
                count++;
            }
        }

private void ApplyLowSpeedSteerAssist(float speedKph, int groundedWheels)
        {
            if (Mathf.Abs(smoothedSteeringInput) < 0.05f || groundedWheels < 2 || speedKph < 1.8f)
            {
                return;
            }

            float movingFactor = Mathf.Clamp01((speedKph - 1.8f) / 9f);
            float speedFactor = Mathf.Lerp(1f, 0.12f, Mathf.InverseLerp(0f, 32f, speedKph));
            float maxYawDegreesPerSecond = Mathf.Clamp(LowSpeedSteerAssist, 4f, 16f);
            float yawDegrees = smoothedSteeringInput * maxYawDegreesPerSecond * speedFactor * movingFactor * Time.fixedDeltaTime;

            body.MoveRotation(body.rotation * Quaternion.Euler(0f, yawDegrees, 0f));
        }

        private void ApplyHandbrakeDriftAssist(float speedKph, int groundedWheels)
        {
            if (body == null || !handbrakeInput || groundedWheels < 2 || speedKph < HandbrakeDriftMinSpeedKph)
            {
                return;
            }

            float steerAmount = Mathf.Abs(smoothedSteeringInput);
            if (steerAmount < HandbrakeDriftSteerThreshold)
            {
                return;
            }

            float steerFactor = Mathf.InverseLerp(HandbrakeDriftSteerThreshold, 1f, steerAmount);
            float speedFactor = Mathf.InverseLerp(HandbrakeDriftMinSpeedKph, 70f, speedKph);
            float direction = Mathf.Sign(smoothedSteeringInput);
            Vector3 groundNormal = GetAverageWheelGroundNormal();
            Vector3 yawAxis = groundNormal.sqrMagnitude > 0.1f ? groundNormal.normalized : Vector3.up;

            body.AddTorque(yawAxis * direction * HandbrakeDriftYawAssist * steerFactor * speedFactor, ForceMode.Acceleration);

            Vector3 lateralVelocity = Vector3.Project(body.linearVelocity, transform.right);
            if (lateralVelocity.sqrMagnitude > 0.05f)
            {
                body.AddForce(lateralVelocity.normalized * HandbrakeDriftLateralAssist * steerFactor * speedFactor, ForceMode.Acceleration);
            }
        }

        private static float GetTurnDriveKeep(MiniVanGear gear, float turnAmount)
        {
            float t = Mathf.Clamp01(turnAmount);
            float t2 = t * t;
            if (gear == MiniVanGear.First || gear == MiniVanGear.Reverse)
            {
                // Cancel tire scrub so 1st / reverse keep speed and RPM in turns.
                return 1f + t2 * 1.55f;
            }

            // Higher gears: only about 25% turn speed loss — partial scrub compensation.
            return 1f + t2 * 0.72f;
        }

        private void UpdateTurnSpeedAnchor(MiniVanGear gear, float planarSpeed, float turnAmount)
        {
            bool holdGear = gear == MiniVanGear.First ||
                            gear == MiniVanGear.Reverse ||
                            gear == MiniVanGear.Second ||
                            gear == MiniVanGear.Third ||
                            gear == MiniVanGear.Fourth ||
                            gear == MiniVanGear.Fifth;
            bool turning = turnAmount > 0.18f && throttleInput > 0.08f && !handbrakeInput && brakeInput < 0.08f;
            if (!holdGear || !turning || planarSpeed < 0.5f)
            {
                turnSpeedAnchorActive = false;
                return;
            }

            if (!turnSpeedAnchorActive)
            {
                turnSpeedAnchorMps = planarSpeed;
                turnSpeedAnchorActive = true;
                return;
            }

            // Anchor can climb with acceleration, but never silently fall with scrub.
            turnSpeedAnchorMps = Mathf.Max(turnSpeedAnchorMps, planarSpeed);
        }

        private float GetTurnHeldSpeedKph(float actualSpeedKph)
        {
            if (!turnSpeedAnchorActive)
            {
                return actualSpeedKph;
            }

            MiniVanGear gear = (MiniVanGear)CurrentGear.Value;
            float anchorKph = turnSpeedAnchorMps * 3.6f;
            if (gear == MiniVanGear.First || gear == MiniVanGear.Reverse)
            {
                return Mathf.Max(actualSpeedKph, anchorKph);
            }

            // Other gears: tach may sag with the allowed 25% speed bleed, not more.
            return Mathf.Max(actualSpeedKph, anchorKph * 0.75f);
        }

        private void ApplyTurnSpeedHold(
            MiniVanGear gear,
            float planarSpeed,
            float turnAmount,
            float direction,
            bool canAccelerate)
        {
            if (body == null || !turnSpeedAnchorActive || !canAccelerate || turnAmount < 0.18f)
            {
                return;
            }

            float minKeep01 = (gear == MiniVanGear.First || gear == MiniVanGear.Reverse) ? 1f : 0.75f;
            float floorSpeed = turnSpeedAnchorMps * minKeep01;
            if (planarSpeed >= floorSpeed * 0.995f)
            {
                return;
            }

            float restore = (floorSpeed - planarSpeed) / Mathf.Max(0.02f, Time.fixedDeltaTime);
            // Soft accel restore — enough to fight scrub without launching the van.
            restore = Mathf.Clamp(restore, 0f, (gear == MiniVanGear.First || gear == MiniVanGear.Reverse) ? 28f : 18f);
            body.AddForce(transform.forward * direction * restore, ForceMode.Acceleration);
        }

        private void ApplyNaturalCornerSlip(float speedKph, int groundedWheels)
        {
            MiniVanGear gear = (MiniVanGear)CurrentGear.Value;
            if (gear == MiniVanGear.First || gear == MiniVanGear.Reverse)
            {
                return;
            }

            if (body == null || handbrakeInput || groundedWheels < 3 || speedKph < CornerSlipMinSpeedKph)
            {
                return;
            }

            float steerAmount = Mathf.Abs(smoothedSteeringInput);
            if (steerAmount < CornerSlipSteerThreshold)
            {
                return;
            }

            // Mild oversteer only when the rear is already starting to slip.
            if (rearSlip < 0.22f)
            {
                return;
            }

            float steerFactor = Mathf.InverseLerp(CornerSlipSteerThreshold, 1f, steerAmount);
            float speedFactor = Mathf.InverseLerp(CornerSlipMinSpeedKph, 85f, speedKph);
            float slipFactor = Mathf.InverseLerp(0.22f, 0.7f, rearSlip);
            float throttleFactor = Mathf.Lerp(0.65f, 1.1f, throttleInput);
            float direction = Mathf.Sign(smoothedSteeringInput);
            Vector3 groundNormal = GetAverageWheelGroundNormal();
            Vector3 yawAxis = groundNormal.sqrMagnitude > 0.1f ? groundNormal.normalized : Vector3.up;

            body.AddTorque(
                yawAxis * direction * CornerSlipYawAssist * steerFactor * speedFactor * slipFactor * throttleFactor,
                ForceMode.Acceleration);
        }

        private void EnsureEngineAudio()
        {
            if (engineAudio == null)
            {
                engineAudio = GetComponent<MiniVanEngineAudio>();
            }

            if (engineAudio == null)
            {
                engineAudio = gameObject.AddComponent<MiniVanEngineAudio>();
            }
        }

        private void UpdateEngineAudio()
        {
            EnsureEngineAudio();
            if (engineAudio != null)
            {
                engineAudio.Tick();
            }
        }


private void NotifyPlayerSeatState(ulong clientId, int seatIndex, bool seated)
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out NetworkClient client) || client.PlayerObject == null)
            {
                return;
            }

            MiniVanPlayer player = client.PlayerObject.GetComponent<MiniVanPlayer>();
            if (player == null)
            {
                return;
            }

            player.SetSeatStateClientRpc(new NetworkObjectReference(NetworkObject), seatIndex, seated);
        }

        private void ServerApplySeatPhysicsForClient(ulong clientId, int seatIndex, bool seated)
        {
            if (!IsServer || NetworkManager.Singleton == null || !NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out NetworkClient client) || client.PlayerObject == null)
            {
                return;
            }

            MiniVanPlayer player = client.PlayerObject.GetComponent<MiniVanPlayer>();
            if (player != null)
            {
                player.ServerApplySeatPhysics(this, seatIndex, seated);
            }
        }


private void ApplyGearSpeedLimit(float targetSpeed, float signedForwardSpeed)
        {
            if (Mathf.Abs(targetSpeed) <= 0.01f || body == null)
            {
                return;
            }

            float planarSpeed = Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up).magnitude;
            float overspeed = planarSpeed - Mathf.Abs(targetSpeed);
            if (overspeed <= 0f)
            {
                return;
            }

            float direction = Mathf.Abs(signedForwardSpeed) > 0.05f
                ? Mathf.Sign(signedForwardSpeed)
                : Mathf.Sign(Vector3.Dot(body.linearVelocity, transform.forward));
            if (Mathf.Approximately(direction, 0f))
            {
                direction = 1f;
            }

            MiniVanGear gear = (MiniVanGear)CurrentGear.Value;
            float gearLimiter = gear == MiniVanGear.First ? 2.1f : 1f;
            // Downhill: let gravity overrun the gear a bit so descents feel scary / need brakes.
            float downhill = GetDownhillSeverity01();
            float downhillSoft = Mathf.Lerp(1f, 0.28f, downhill);
            body.AddForce(
                -transform.forward * direction * GearSpeedLimitBrake * gearLimiter * downhillSoft * Mathf.Clamp01(overspeed * 1.6f),
                ForceMode.Force);
        }
}
}

