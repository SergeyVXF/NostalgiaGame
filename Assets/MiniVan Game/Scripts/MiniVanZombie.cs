using Unity.Netcode;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace MiniVanGame
{
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(CharacterController))]
    public class MiniVanZombie : NetworkBehaviour
    {
        public float DetectionRange = 45f;
        public float HearingRange = 1.5f;
        public float AttackRange = 1.45f;
        public float AttackStopDistance = 1.22f;
        public float PersonalSpaceDistance = 0.95f;
        public float BackOffSpeedKph = 4f;
        public float AttackInterval = 1f;
        public int MaxHealth = 5;

        public virtual string EnemyDisplayName => "Zombie";

        public virtual int CurrentHealth => IsSpawned ? health.Value : MaxHealth;

        /// <summary>
        /// Fractional health for combat HUD (vampires can chip by &lt;1 HP while shielded).
        /// </summary>
        public virtual float CurrentHealthPrecise => CurrentHealth;
        public int DamagePerHit = 1;
        public float ExtraSpeedKph = 1f;
        public float TurnSharpness = 9f;
        public float Gravity = -18f;
        public float PatrolRadius = 6f;
        public float PatrolSpeedKph = 3f;
        public float PatrolWaitSeconds = 1.2f;
        public float PatrolPointReachDistance = 0.75f;
        public float ChaseMemorySeconds = 6f;
        public float FieldOfViewDegrees = 150f;
        public float ObstacleProbeDistance = 2.4f;
        public float ObstacleProbeRadius = 0.32f;
        public float ObstacleAvoidanceStrength = 1.35f;
        public bool UseNavMeshWhenAvailable = true;
        public float NavMeshSampleDistance = 2.0f;
        public bool DetectReachablePlayersThroughNavigation = false;
        public float NavigationDetectionRange = 55f;
        public bool OpenPanelkaDoorsWhenChasing = true;
        [Header("Vehicle Attack")]
        public float VehicleAttackDurationSeconds = 15f;
        public float VehicleAttackStandOffDistance = 1.25f;
        public bool DebugVehicleAttack = true;
        [Min(0.25f)] public float VehicleAttackDebugInterval = 1f;
        [Header("Performance")]
        [Range(8f, 30f)] public float AiUpdateRate = 20f;
        [Range(2f, 20f)] public float NetworkTransformRate = 10f;
        [Min(0.001f)] public float NetworkPositionThreshold = 0.035f;
        [Min(0.1f)] public float NetworkRotationThreshold = 2f;
        [Min(0.05f)] public float TargetScanInterval = 0.2f;
        [Min(0.05f)] public float NavPathRefreshInterval = 0.25f;
        [Min(0.25f)] public float NavTargetMoveThreshold = 1.5f;
        public float DoorOpenProbeRadius = 0.85f;
        public float StuckCheckSeconds = 0.35f;
        public float StuckSidestepSeconds = 1.1f;
        public float KnockbackDistance = 2.15f;
        public float KnockbackSeconds = 0.2f;
        public float MovementRecoverSeconds = 0.7f;
        public float MovementRecoverStartScale = 0.22f;
        public float HitFlashSeconds = 0.15f;

        private readonly NetworkVariable<Vector3> networkPosition = new NetworkVariable<Vector3>(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<Quaternion> networkRotation = new NetworkVariable<Quaternion>(
            Quaternion.identity,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        protected readonly NetworkVariable<int> health = new NetworkVariable<int>(
            5,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        protected CharacterController controller;
        protected Transform leftArm;
        protected Transform rightArm;
        private float verticalVelocity;
        private float nextAttackTime;
        private Vector3 remotePositionVelocity;
        private Vector3 spawnPosition;
        private Quaternion spawnFacing = Quaternion.identity;
        private Vector3 patrolTarget;
        private float nextPatrolPickTime;
        private int patrolSide = 1;
        private float lastSawTargetTime = -999f;
        private MiniVanPlayer chasedTarget;
        private bool hasSpawnPosition;
        private NavMeshPath navMeshPath;
        protected Vector3 knockbackVelocity;
        protected float knockbackUntil;
        private float movementRecoverUntil;
        private float stuckTimer;
        private float avoidanceBiasUntil;
        private float avoidanceBiasSign;
        protected Renderer[] visualRenderers;
        private MaterialPropertyBlock hitFlashBlock;
        protected float hitFlashUntil;
        private bool roadkilled;
        private bool roadkillKnockedDown;
        protected bool deathPartsSpawned;
        private float nextNetworkTransformSendTime;
        private Vector3 lastPublishedNetworkPosition;
        private Quaternion lastPublishedNetworkRotation = Quaternion.identity;
        private bool hasPublishedNetworkTransform;
        private float nextTargetScanTime;
        private float nextNavPathRefreshTime;
        private Vector3 lastNavTarget;
        private Vector3 cachedNavSteerTarget;
        private bool hasCachedNavSteerTarget;
        private bool hitFlashWasApplied;
        private float nextObstacleProbeTime;
        private float nextDoorProbeTime;
        private Vector3 cachedObstacleDirection;
        private float nextAiUpdateTime;
        private float lastAiUpdateTime;
        private float aiDeltaTime = 0.05f;
        private bool isEmerging;
        private Vector3 emergenceStartPosition;
        private Vector3 emergenceGroundPosition;
        private float emergenceStartTime;
        private float emergenceDuration;
        private static MiniVanPlayer[] cachedTargetPlayers;
        private static float nextTargetPlayerCacheTime;
        private float nextArmAnimationTime;
        private MiniVanVehicle vehicleAttackTarget;
        private float vehicleAttackStartedTime = -1f;
        private bool vehicleAttackExpired;
        private readonly Dictionary<string, float> vehicleAttackDebugTimes = new Dictionary<string, float>();

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            navMeshPath = new NavMeshPath();
            ConfigureController();
            EnsureVisual();
            CacheVisualRenderers();
        }

        public override void OnNetworkSpawn()
        {
            controller = GetComponent<CharacterController>();
            if (navMeshPath == null)
            {
                navMeshPath = new NavMeshPath();
            }
            ConfigureController();
            EnsureVisual();
            CacheVisualRenderers();

            if (IsServer)
            {
                spawnPosition = isEmerging ? emergenceGroundPosition : transform.position;
                spawnFacing = transform.rotation;
                hasSpawnPosition = true;
                PickPatrolTarget(true);
                health.Value = MaxHealth;
                float aiInterval = 1f / Mathf.Max(1f, AiUpdateRate);
                lastAiUpdateTime = Time.time;
                nextAiUpdateTime = Time.time + Random.Range(0f, aiInterval);
                PublishNetworkTransform(true);
            }
        }

        private void Update()
        {
            if (Time.time >= nextArmAnimationTime)
            {
                nextArmAnimationTime = Time.time + 1f / 15f;
                AnimateArms();
            }
            UpdateHitFlashVisual();

            if (IsServer)
            {
                if (isEmerging)
                {
                    UpdateEmergence();
                    return;
                }

                float aiInterval = 1f / ResolveEffectiveAiUpdateRate();
                if (Time.time < nextAiUpdateTime)
                {
                    return;
                }
                aiDeltaTime = lastAiUpdateTime > 0f
                    ? Mathf.Clamp(Time.time - lastAiUpdateTime, 0.001f, 0.2f)
                    : aiInterval;
                lastAiUpdateTime = Time.time;
                nextAiUpdateTime = Time.time + aiInterval;
                ServerUpdate();
                return;
            }

            // Unspawned instances (offline tests / failed NetworkObject.Spawn) must keep their
            // Instantiate pose. networkPosition defaults to (0,0,0) and would yank them to origin.
            // Also wait until the server has published a real pose at least once.
            if (!IsSpawned || !hasPublishedNetworkTransform)
            {
                return;
            }

            transform.position = Vector3.SmoothDamp(transform.position, networkPosition.Value, ref remotePositionVelocity, 0.08f);
            transform.rotation = Quaternion.Slerp(transform.rotation, networkRotation.Value, 1f - Mathf.Exp(-18f * Time.deltaTime));
        }

        private float ResolveEffectiveAiUpdateRate()
        {
            float rate = Mathf.Max(1f, AiUpdateRate);
            if (chasedTarget == null)
            {
                return Mathf.Max(4f, rate * 0.5f);
            }

            float distanceSqr = (chasedTarget.transform.position - transform.position).sqrMagnitude;
            if (distanceSqr > 30f * 30f)
            {
                return Mathf.Max(4f, rate * 0.5f);
            }
            if (distanceSqr > 18f * 18f)
            {
                return Mathf.Max(6f, rate * 0.75f);
            }
            return rate;
        }

        public void BeginEmergence(Vector3 groundPosition, float duration, float depth)
        {
            emergenceGroundPosition = groundPosition;
            emergenceStartPosition = groundPosition - Vector3.up * Mathf.Max(0.1f, depth);
            emergenceDuration = Mathf.Max(0.1f, duration);
            emergenceStartTime = Time.time;
            isEmerging = true;
            transform.position = emergenceStartPosition;
            verticalVelocity = 0f;
            if (controller != null)
            {
                controller.enabled = false;
            }
        }

        private void UpdateEmergence()
        {
            float progress = Mathf.Clamp01((Time.time - emergenceStartTime) / emergenceDuration);
            float eased = Mathf.SmoothStep(0f, 1f, progress);
            transform.position = Vector3.LerpUnclamped(
                emergenceStartPosition, emergenceGroundPosition, eased);
            PublishNetworkTransform(progress >= 1f);

            if (progress < 1f)
            {
                return;
            }

            isEmerging = false;
            transform.position = emergenceGroundPosition;
            if (controller != null)
            {
                controller.enabled = true;
            }
            verticalVelocity = 0f;
            float aiInterval = 1f / Mathf.Max(1f, AiUpdateRate);
            lastAiUpdateTime = Time.time;
            nextAiUpdateTime = Time.time + Random.Range(0f, aiInterval);
        }

        public virtual void TakeBatHit(
            int damage,
            Vector3 hitOrigin,
            float knockbackDistance,
            float knockbackSeconds,
            bool fromAspenStake = false)
        {
            if (!IsServer || health.Value <= 0 || roadkilled || roadkillKnockedDown)
            {
                return;
            }

            BeginHitKnockback(hitOrigin, knockbackDistance, knockbackSeconds);
            TriggerHitFlash();
            OnDamagedByBat(Mathf.Max(1, damage));

            health.Value = Mathf.Max(0, health.Value - Mathf.Max(1, damage));
            if (health.Value <= 0)
            {
                ServerSpawnDeathParts(knockbackVelocity);
                if (NetworkObject != null && NetworkObject.IsSpawned)
                {
                    NetworkObject.Despawn(true);
                }
                else
                {
                    Destroy(gameObject);
                }
            }
        }

        protected virtual void OnDamagedByBat(int damage)
        {
        }

        public virtual bool ServerRoadkill(Vector3 impactVelocity)
        {
            if (!IsServer || roadkilled || health.Value <= 0)
            {
                return false;
            }

            roadkilled = true;
            health.Value = 0;
            DisableRoadkillCollision();
            ServerSpawnDeathParts(impactVelocity);

            if (NetworkObject != null && NetworkObject.IsSpawned)
            {
                NetworkObject.Despawn(true);
            }
            else
            {
                Destroy(gameObject);
            }

            return true;
        }

        protected virtual void ServerSpawnDeathParts(Vector3 impulse)
        {
            if (deathPartsSpawned)
            {
                return;
            }

            if (IsSpawned && !IsServer)
            {
                return;
            }

            deathPartsSpawned = true;
            int seed = Random.Range(int.MinValue, int.MaxValue);
            Vector3 position = transform.position + Vector3.up * 0.45f;
            // Spawn immediately on the authority. A ClientRpc-only path races Despawn and
            // often never runs, so death FX would never appear.
            SpawnDeathPartsLocal(position, impulse, seed);
            if (IsSpawned && IsServer)
            {
                SpawnDeathPartsClientRpc(position, impulse, seed);
            }
        }

        [ClientRpc]
        private void SpawnDeathPartsClientRpc(Vector3 position, Vector3 impulse, int seed)
        {
            // Host already spawned locally in ServerSpawnDeathParts.
            if (IsServer)
            {
                return;
            }

            SpawnDeathPartsLocal(position, impulse, seed);
        }

        protected virtual void SpawnDeathPartsLocal(Vector3 position, Vector3 impulse, int seed)
        {
            MiniVanFuelPartSpawner.SpawnDeathParts(position, impulse, seed, transform.Find("Zombie Visual"));
        }

        public bool ServerKnockdownForRoadkill(Vector3 hitOrigin, float explodeAfterSeconds)
        {
            if (!IsServer || roadkilled || roadkillKnockedDown || health.Value <= 0)
            {
                return false;
            }

            roadkillKnockedDown = true;
            DisableRoadkillCollision();

            Vector3 away = Vector3.ProjectOnPlane(transform.position - hitOrigin, Vector3.up);
            if (away.sqrMagnitude < 0.001f)
            {
                away = transform.forward;
            }

            transform.rotation = Quaternion.LookRotation(away.normalized, Vector3.up) * Quaternion.Euler(90f, 0f, 0f);

            if (controller != null)
            {
                controller.enabled = false;
            }

            PublishNetworkTransform(true);

            if (HitFlashSeconds > 0f)
            {
                HitFeedbackClientRpc();
            }

            return true;
        }

        private void DisableRoadkillCollision()
        {
            if (controller == null)
            {
                controller = GetComponent<CharacterController>();
            }

            if (controller != null)
            {
                controller.enabled = false;
            }
        }

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (!IsServer || roadkilled || roadkillKnockedDown || hit == null || hit.collider == null)
            {
                return;
            }

            MiniVanVehicle vehicle = hit.collider.GetComponentInParent<MiniVanVehicle>();
            if (vehicle != null)
            {
                vehicle.TryRoadkillZombie(this, hit.point);
            }
        }

        /// <summary>
        /// Subclasses (e.g. vampire bat form) can fully replace the per-tick AI.
        /// Return true when the subclass handled the update.
        /// </summary>
        protected virtual bool TryOverrideServerUpdate()
        {
            return false;
        }

        private void ServerUpdate()
        {
            if (TryOverrideServerUpdate())
            {
                PublishNetworkTransform(false);
                return;
            }

            if (roadkilled)
            {
                return;
            }

            if (roadkillKnockedDown)
            {
                PublishNetworkTransform();
                return;
            }

            if (MiniVanGameModeInteriorZone.TryGetContainingZombieSafeZone(transform.position,
                    out MiniVanGameModeInteriorZone safeZone))
            {
                Vector3 outside = safeZone.NearestZombieOutsidePoint(transform.position);
                MoveController(outside - transform.position);
                PublishNetworkTransform();
                return;
            }

            if (ApplyKnockback())
            {
                PublishNetworkTransform();
                return;
            }

            MiniVanVehicle occupiedVehicleTarget = FindBestOccupiedVehicleTarget(out MiniVanPlayer vehicleOccupantTarget);
            if (occupiedVehicleTarget != null &&
                TryHandleVehicleTarget(occupiedVehicleTarget, vehicleOccupantTarget))
            {
                PublishNetworkTransform();
                return;
            }

            MiniVanPlayer target = GetTargetThrottled();
            if (target == null)
            {
                LogVehicleAttackDebug(
                    "NO_TARGET",
                    "players=" + (cachedTargetPlayers != null ? cachedTargetPlayers.Length : 0) +
                    " occupiedVehicles=" + CountOccupiedVehicles() +
                    " isServer=" + IsServer);
                ResetVehicleAttackState();
                Patrol();
                PublishNetworkTransform();
                return;
            }

            if (TryHandleVehicleOccupantTarget(target))
            {
                PublishNetworkTransform();
                return;
            }
            ResetVehicleAttackState();

            Vector3 toTarget = target.transform.position - transform.position;
            Vector3 flatToTarget = Vector3.ProjectOnPlane(toTarget, Vector3.up);
            float planarDistance = flatToTarget.magnitude;

            float speed = ResolveChaseSpeed(target) * GetMovementRecoverScale();
            if (planarDistance > AttackStopDistance)
            {
                MoveToward(target.transform.position, speed, true);
            }
            else
            {
                FaceFlatDirection(flatToTarget);
                if (planarDistance < PersonalSpaceDistance && flatToTarget.sqrMagnitude > 0.001f)
                {
                    MoveInDirection(-flatToTarget.normalized, BackOffSpeedKph / 3.6f);
                }
                else
                {
                    ApplyStandingGravity();
                }
            }

            if (Time.time >= nextAttackTime && CanAttackPlayer(target))
            {
                nextAttackTime = Time.time + AttackInterval;
                OnMeleeAttack();
                target.ReceiveZombieDamageServer(DamagePerHit);
            }

            PublishNetworkTransform();
        }

        protected virtual void OnMeleeAttack()
        {
        }

        protected bool TryHandleVehicleOccupantTarget(MiniVanPlayer target)
        {
            MiniVanVehicle targetVehicle = FindVehicleContainingTarget(target);
            if (targetVehicle == null)
            {
                LogVehicleAttackDebug(
                    "TARGET_ON_FOOT",
                    "player=" + target.OwnerClientId +
                    " currentVehicle=null protected=" +
                    MiniVanGameModeInteriorZone.IsZombieProtected(target.transform.position));
                return false;
            }

            return TryHandleVehicleTarget(targetVehicle, target);
        }

        protected bool TryHandleVehicleTarget(MiniVanVehicle targetVehicle, MiniVanPlayer target)
        {
            if (targetVehicle == null)
            {
                return false;
            }

            if (vehicleAttackTarget != targetVehicle)
            {
                vehicleAttackTarget = targetVehicle;
                vehicleAttackStartedTime = -1f;
                vehicleAttackExpired = false;
                if (DebugVehicleAttack)
                {
                    Debug.LogWarning("[MiniVanZombie] \u0437\u043e\u043c\u0431\u0438 \u0432\u044b\u0431\u0440\u0430\u043b \u043c\u0438\u043d\u0438\u0432\u0435\u043d \u0446\u0435\u043b\u044c\u044e");
                }
            }

            Vector3 attackOrigin = transform.position + Vector3.up * 0.8f;
            Vector3 closestBodyPoint = targetVehicle.GetClosestVehicleBodyPoint(attackOrigin);
            Vector3 flatToBody = Vector3.ProjectOnPlane(closestBodyPoint - transform.position, Vector3.up);
            float bodyDistance = targetVehicle.GetDistanceFromVehicleBody(attackOrigin);
            float attackRange = Mathf.Max(0.25f, targetVehicle.ZombieVehicleAttackRange);
            float standOff = Mathf.Max(0.35f, VehicleAttackStandOffDistance);
            float speed = ResolveChaseSpeed(target) * GetMovementRecoverScale();
            string playerId = target != null ? target.OwnerClientId.ToString() : "occupied";

            if (bodyDistance > attackRange * 0.9f && flatToBody.sqrMagnitude > 0.001f)
            {
                LogVehicleAttackDebug(
                    "MOVING_TO_VEHICLE",
                    "player=" + playerId +
                    " vehicle=" + targetVehicle.name +
                    " bodyDistance=" + bodyDistance.ToString("0.00") +
                    " attackRange=" + attackRange.ToString("0.00"));
                Vector3 standDirection = Vector3.ProjectOnPlane(transform.position - closestBodyPoint, Vector3.up);
                if (standDirection.sqrMagnitude < 0.001f)
                {
                    standDirection = -Vector3.ProjectOnPlane(targetVehicle.transform.forward, Vector3.up);
                }
                Vector3 standPoint = closestBodyPoint + standDirection.normalized * standOff;
                MoveToward(standPoint, speed, true);
                FaceFlatDirection(flatToBody);
                return true;
            }

            FaceFlatDirection(flatToBody);
            ApplyStandingGravity();

            if (vehicleAttackExpired)
            {
                LogVehicleAttackDebug(
                    "ATTACK_EXPIRED",
                    "vehicle=" + targetVehicle.name +
                    " elapsed=" + (Time.time - vehicleAttackStartedTime).ToString("0.0"));
                return true;
            }

            if (vehicleAttackStartedTime < 0f)
            {
                vehicleAttackStartedTime = Time.time;
                if (DebugVehicleAttack)
                {
                    Debug.LogWarning("[MiniVanZombie] \u0437\u043e\u043c\u0431\u0438 \u043d\u0430\u0447\u0430\u043b \u0431\u0438\u0442\u044c \u043c\u0438\u043d\u0438\u0432\u0435\u043d");
                }
            }

            if (Time.time - vehicleAttackStartedTime >= Mathf.Max(0.1f, VehicleAttackDurationSeconds))
            {
                vehicleAttackExpired = true;
                if (DebugVehicleAttack)
                {
                    Debug.LogWarning("[MiniVanZombie] \u0437\u043e\u043c\u0431\u0438 \u043f\u0435\u0440\u0435\u0441\u0442\u0430\u043b \u0431\u0438\u0442\u044c \u043c\u0438\u043d\u0438\u0432\u0435\u043d");
                }
                return true;
            }

            if (Time.time >= nextAttackTime &&
                targetVehicle.TryApplyZombieDamageFrom(this, targetVehicle.ZombieVehicleDamagePerHit))
            {
                nextAttackTime = Time.time + AttackInterval;
                OnMeleeAttack();
                if (DebugVehicleAttack)
                {
                    Debug.LogWarning("[MiniVanZombie] \u0437\u043e\u043c\u0431\u0438 \u0431\u044c\u0451\u0442 \u043c\u0438\u043d\u0438\u0432\u0435\u043d");
                }
            }
            else if (Time.time < nextAttackTime)
            {
                LogVehicleAttackDebug(
                    "ATTACK_COOLDOWN",
                    "vehicle=" + targetVehicle.name +
                    " remaining=" + Mathf.Max(0f, nextAttackTime - Time.time).ToString("0.00"));
            }
            else
            {
                LogVehicleAttackDebug(
                    "DAMAGE_REJECTED",
                    "vehicle=" + targetVehicle.name +
                    " bodyDistance=" + bodyDistance.ToString("0.00") +
                    " attackRange=" + attackRange.ToString("0.00"));
            }

            return true;
        }

        private void ResetVehicleAttackState()
        {
            if (vehicleAttackTarget != null)
            {
                LogVehicleAttackDebug(
                    "VEHICLE_TARGET_RESET",
                    "vehicle=" + vehicleAttackTarget.name,
                    true);
            }
            vehicleAttackTarget = null;
            vehicleAttackStartedTime = -1f;
            vehicleAttackExpired = false;
        }

        private void LogVehicleAttackDebug(string state, string details, bool force = false)
        {
            if (!DebugVehicleAttack)
            {
                return;
            }

            if (!force &&
                vehicleAttackDebugTimes.TryGetValue(state, out float nextLogTime) &&
                Time.time < nextLogTime)
            {
                return;
            }

            vehicleAttackDebugTimes[state] = Time.time + Mathf.Max(0.25f, VehicleAttackDebugInterval);
            Debug.LogWarning(
                "[MiniVanZombieDebug] " + state +
                " zombie=" + name +
                " " + details);
        }

        private static int CountOccupiedVehicles()
        {
            MiniVanVehicle[] vehicles = MiniVanSceneScan.Get<MiniVanVehicle>();
            int count = 0;
            for (int i = 0; i < vehicles.Length; i++)
            {
                MiniVanVehicle vehicle = vehicles[i];
                if (vehicle != null && vehicle.HasAnyOccupantOrSeatedPlayer())
                {
                    count++;
                }
            }

            return count;
        }

        private MiniVanVehicle FindBestOccupiedVehicleTarget(out MiniVanPlayer occupantTarget)
        {
            occupantTarget = null;
            MiniVanVehicle[] vehicles = MiniVanSceneScan.Get<MiniVanVehicle>();
            MiniVanVehicle best = null;
            float bestDistance = float.MaxValue;
            Vector3 attackOrigin = transform.position + Vector3.up * 0.8f;

            for (int i = 0; i < vehicles.Length; i++)
            {
                MiniVanVehicle vehicle = vehicles[i];
                if (vehicle == null || !VehicleHasLivingOccupant(vehicle))
                {
                    continue;
                }

                float bodyDistance = vehicle.GetDistanceFromVehicleBody(attackOrigin);
                if (bodyDistance > DetectionRange || bodyDistance >= bestDistance)
                {
                    LogVehicleAttackDebug(
                        "OCCUPIED_VEHICLE_SCAN_TOO_FAR",
                        "vehicle=" + vehicle.name +
                        " bodyDistance=" + bodyDistance.ToString("0.00") +
                        " detectionRange=" + DetectionRange.ToString("0.00"));
                    continue;
                }

                best = vehicle;
                bestDistance = bodyDistance;
            }

            if (best != null)
            {
                occupantTarget = FindVehicleOccupantPlayer(best);
                LogVehicleAttackDebug(
                    "OCCUPIED_VEHICLE_SCAN_TARGET",
                    "vehicle=" + best.name +
                    " occupant=" + (occupantTarget != null ? occupantTarget.OwnerClientId.ToString() : "unknown") +
                    " bodyDistance=" + bestDistance.ToString("0.00"));
            }

            return best;
        }

        private MiniVanPlayer FindVehicleOccupantPlayer(MiniVanVehicle vehicle)
        {
            if (vehicle == null)
            {
                return null;
            }

            if (cachedTargetPlayers == null || Time.time >= nextTargetPlayerCacheTime)
            {
                RefreshTargetPlayerCache();
                nextTargetPlayerCacheTime = Time.time + 0.5f;
            }

            MiniVanPlayer[] players = cachedTargetPlayers;
            for (int i = 0; i < players.Length; i++)
            {
                MiniVanPlayer player = players[i];
                if (player == null || player.IsIgnoredByEnemies)
                {
                    continue;
                }

                if (player.CurrentVehicle == vehicle ||
                    vehicle.HasOccupant(player.OwnerClientId) ||
                    player.IsInsideVehicleCabinForZombieTarget(vehicle))
                {
                    return player;
                }
            }

            return null;
        }

        private bool VehicleHasLivingOccupant(MiniVanVehicle vehicle)
        {
            return FindVehicleOccupantPlayer(vehicle) != null;
        }

        private MiniVanVehicle FindVehicleContainingTarget(MiniVanPlayer target)
        {
            if (target == null)
            {
                return null;
            }

            if (target.CurrentVehicle != null)
            {
                return target.CurrentVehicle;
            }

            ulong clientId = target.OwnerClientId;
            MiniVanVehicle[] vehicles = MiniVanSceneScan.Get<MiniVanVehicle>();
            for (int i = 0; i < vehicles.Length; i++)
            {
                MiniVanVehicle vehicle = vehicles[i];
                if (vehicle != null &&
                    (vehicle.HasOccupant(clientId) ||
                     target.IsInsideVehicleCabinForZombieTarget(vehicle)))
                {
                    return vehicle;
                }
            }

            return null;
        }

        private bool ApplyKnockback()
        {
            return ApplyKnockbackMotion(aiDeltaTime);
        }

        protected bool ApplyKnockbackMotion(float deltaTime)
        {
            if (Time.time > knockbackUntil || knockbackVelocity.sqrMagnitude <= 0.001f)
            {
                return false;
            }

            Vector3 delta = knockbackVelocity * Mathf.Max(0f, deltaTime);
            if (controller != null && controller.enabled)
            {
                MoveController(delta);
            }
            else
            {
                transform.position += delta;
            }

            knockbackVelocity = Vector3.Lerp(
                knockbackVelocity,
                Vector3.zero,
                1f - Mathf.Exp(-18f * Mathf.Max(0f, deltaTime)));
            return true;
        }

        protected void BeginHitKnockback(Vector3 hitOrigin, float knockbackDistance, float knockbackSeconds)
        {
            Vector3 away = Vector3.ProjectOnPlane(transform.position - hitOrigin, Vector3.up);
            if (away.sqrMagnitude < 0.001f)
            {
                away = -transform.forward;
            }

            float resolvedKnockbackDistance = knockbackDistance > 0.01f ? knockbackDistance : KnockbackDistance;
            float resolvedKnockbackSeconds = knockbackSeconds > 0.01f ? knockbackSeconds : KnockbackSeconds;
            float knockbackSpeed = Mathf.Max(0.01f, resolvedKnockbackDistance) /
                                   Mathf.Max(0.01f, resolvedKnockbackSeconds);
            knockbackVelocity = away.normalized * knockbackSpeed;
            knockbackUntil = Time.time + resolvedKnockbackSeconds;
            movementRecoverUntil = knockbackUntil + Mathf.Max(0f, MovementRecoverSeconds);
        }

        protected void TriggerHitFlash()
        {
            hitFlashUntil = Time.time + HitFlashSeconds;
            hitFlashWasApplied = false;
            if (IsSpawned && IsServer)
            {
                HitFeedbackClientRpc();
            }
        }

        private MiniVanPlayer GetTargetThrottled()
        {
            if (Time.time >= nextTargetScanTime || chasedTarget == null || chasedTarget.IsIgnoredByEnemies ||
                MiniVanGameModeInteriorZone.IsZombieProtected(chasedTarget.transform.position))
            {
                nextTargetScanTime = Time.time + Mathf.Max(0.05f, TargetScanInterval);
                return FindTarget();
            }

            if (Time.time - lastSawTargetTime > ChaseMemorySeconds)
            {
                chasedTarget = null;
            }
            return chasedTarget;
        }

        private MiniVanPlayer FindTarget()
        {
            if (cachedTargetPlayers == null || Time.time >= nextTargetPlayerCacheTime)
            {
                RefreshTargetPlayerCache();
                nextTargetPlayerCacheTime = Time.time + 0.5f;
            }
            MiniVanPlayer[] players = cachedTargetPlayers;
            MiniVanPlayer best = null;
            float bestDistance = float.MaxValue;
            bool bestCanSee = false;

            for (int i = 0; i < players.Length; i++)
            {
                MiniVanPlayer player = players[i];
                if (player == null || player.IsIgnoredByEnemies)
                {
                    continue;
                }

                float distance = Vector3.Distance(transform.position, player.transform.position);
                MiniVanVehicle occupiedVehicle = FindVehicleContainingTarget(player);
                bool protectedOnFoot = occupiedVehicle == null &&
                                       MiniVanGameModeInteriorZone.IsZombieProtected(player.transform.position);
                if (protectedOnFoot)
                {
                    LogVehicleAttackDebug(
                        "PLAYER_PROTECTED_ON_FOOT",
                        "player=" + player.OwnerClientId +
                        " distance=" + distance.ToString("0.00"));
                    continue;
                }

                float targetDistance = distance;
                bool canSee;
                if (occupiedVehicle != null)
                {
                    targetDistance = occupiedVehicle.GetDistanceFromVehicleBody(transform.position + Vector3.up * 0.8f);
                    canSee = targetDistance <= DetectionRange;
                    LogVehicleAttackDebug(
                        canSee ? "OCCUPIED_VEHICLE_DETECTED" : "OCCUPIED_VEHICLE_TOO_FAR",
                        "player=" + player.OwnerClientId +
                        " vehicle=" + occupiedVehicle.name +
                        " currentVehicle=" + (player.CurrentVehicle != null) +
                        " hasOccupant=" + occupiedVehicle.HasOccupant(player.OwnerClientId) +
                        " playerProtected=" +
                        MiniVanGameModeInteriorZone.IsZombieProtected(player.transform.position) +
                        " bodyDistance=" + targetDistance.ToString("0.00") +
                        " detectionRange=" + DetectionRange.ToString("0.00"));
                }
                else
                {
                    bool heardClose = HearingRange > 0f && distance <= HearingRange;
                    canSee = heardClose || CanSeePlayer(player, distance);
                    LogVehicleAttackDebug(
                        heardClose ? "PLAYER_HEARD_CLOSE" : (canSee ? "PLAYER_VISIBLE_ON_FOOT" : "PLAYER_NOT_VISIBLE"),
                        "player=" + player.OwnerClientId +
                        " distance=" + distance.ToString("0.00") +
                        " hearingRange=" + HearingRange.ToString("0.00"));
                }
                bool remembered = chasedTarget == player &&
                                  Time.time - lastSawTargetTime <= ChaseMemorySeconds &&
                                  targetDistance <= DetectionRange * 1.35f;

                if (targetDistance < bestDistance && (canSee || remembered))
                {
                    best = player;
                    bestDistance = targetDistance;
                    bestCanSee = canSee;
                }
            }

            if (best != null)
            {
                chasedTarget = best;
                if (bestCanSee)
                {
                    lastSawTargetTime = Time.time;
                }
            }
            else if (Time.time - lastSawTargetTime > ChaseMemorySeconds)
            {
                chasedTarget = null;
            }

            return best;
        }

        private static void RefreshTargetPlayerCache()
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager != null && manager.IsServer)
            {
                int count = manager.ConnectedClientsList.Count;
                if (cachedTargetPlayers == null || cachedTargetPlayers.Length != count)
                {
                    cachedTargetPlayers = new MiniVanPlayer[count];
                }

                for (int i = 0; i < count; i++)
                {
                    NetworkObject playerObject = manager.ConnectedClientsList[i].PlayerObject;
                    MiniVanPlayer player = playerObject != null
                        ? playerObject.GetComponent<MiniVanPlayer>()
                        : null;
                    if (player == null && playerObject != null)
                    {
                        player = playerObject.GetComponentInChildren<MiniVanPlayer>(true);
                    }
                    cachedTargetPlayers[i] = player;
                }
                return;
            }

            cachedTargetPlayers = MiniVanSceneScan.Get<MiniVanPlayer>();
        }

        private bool CanSeePlayer(MiniVanPlayer player, float distance)
        {
            if (player == null || distance > DetectionRange)
            {
                return false;
            }

            Vector3 eye = transform.position + Vector3.up * 1.45f;
            Vector3 targetPoint = GetPlayerBodyPoint(player);
            Vector3 toTarget = targetPoint - eye;
            float rayDistance = toTarget.magnitude;
            if (rayDistance <= 0.001f)
            {
                return true;
            }

            Vector3 flatToTarget = Vector3.ProjectOnPlane(toTarget, Vector3.up);
            if (flatToTarget.sqrMagnitude > 0.001f)
            {
                float facing = Vector3.Angle(transform.forward, flatToTarget.normalized);
                if (facing > FieldOfViewDegrees * 0.5f)
                {
                    return false;
                }
            }

            return HasDirectLineToPlayer(player, eye, targetPoint);
        }

        private bool CanReachPlayerByNavigation(MiniVanPlayer player, float distance)
        {
            if (!DetectReachablePlayersThroughNavigation ||
                !UseNavMeshWhenAvailable ||
                player == null ||
                distance > NavigationDetectionRange)
            {
                return false;
            }

            return HasCompleteNavigationPathTo(player.transform.position);
        }

        private bool HasCompleteNavigationPathTo(Vector3 targetPosition)
        {
            if (navMeshPath == null)
            {
                navMeshPath = new NavMeshPath();
            }

            if (!NavMesh.SamplePosition(transform.position, out NavMeshHit startHit, NavMeshSampleDistance, NavMesh.AllAreas))
            {
                return false;
            }

            if (!NavMesh.SamplePosition(targetPosition, out NavMeshHit targetHit, NavMeshSampleDistance, NavMesh.AllAreas))
            {
                return false;
            }

            if (!NavMesh.CalculatePath(startHit.position, targetHit.position, NavMesh.AllAreas, navMeshPath))
            {
                return false;
            }

            return navMeshPath.status != NavMeshPathStatus.PathInvalid &&
                   navMeshPath.corners != null &&
                   navMeshPath.corners.Length >= 2;
        }
        private void Patrol()
        {
            if (!hasSpawnPosition)
            {
                spawnPosition = transform.position;
                spawnFacing = transform.rotation;
                hasSpawnPosition = true;
                PickPatrolTarget(true);
            }

            Vector3 flatToPatrol = Vector3.ProjectOnPlane(patrolTarget - transform.position, Vector3.up);
            if (flatToPatrol.magnitude <= PatrolPointReachDistance || Time.time >= nextPatrolPickTime)
            {
                PickPatrolTarget(false);
                flatToPatrol = Vector3.ProjectOnPlane(patrolTarget - transform.position, Vector3.up);
            }

            MoveToward(transform.position + flatToPatrol, (PatrolSpeedKph / 3.6f) * GetMovementRecoverScale(), true);
        }

        private void PickPatrolTarget(bool immediate)
        {
            patrolSide = -patrolSide;
            float radius = Mathf.Max(0.5f, PatrolRadius);
            Vector3 right = spawnFacing * Vector3.right;
            Vector3 forward = spawnFacing * Vector3.forward;
            float sideDistance = Random.Range(radius * 0.4f, radius);
            patrolTarget = spawnPosition +
                           right * patrolSide * sideDistance +
                           forward * Random.Range(-radius * 0.28f, radius * 0.28f);

            if (Physics.Raycast(patrolTarget + Vector3.up * 6f, Vector3.down, out RaycastHit hit, 12f, ~0, QueryTriggerInteraction.Ignore))
            {
                patrolTarget = hit.point;
            }

            nextPatrolPickTime = Time.time + (immediate ? 0.2f : PatrolWaitSeconds + Random.Range(0.35f, 1.2f));
        }

        private void MoveToward(Vector3 targetPosition, float speed, bool avoidObstacles)
        {
            Vector3 beforeMove = transform.position;
            Vector3 steerTarget = targetPosition;
            if (avoidObstacles && UseNavMeshWhenAvailable && TryGetNavMeshSteerTarget(targetPosition, out Vector3 navMeshSteerTarget))
            {
                steerTarget = navMeshSteerTarget;
            }

            Vector3 flatDirection = Vector3.ProjectOnPlane(steerTarget - transform.position, Vector3.up);
            Vector3 direction = flatDirection.sqrMagnitude > 0.001f ? flatDirection.normalized : Vector3.zero;
            if (avoidObstacles && direction.sqrMagnitude > 0.001f)
            {
                if (Time.time >= nextObstacleProbeTime || cachedObstacleDirection.sqrMagnitude < 0.001f)
                {
                    nextObstacleProbeTime = Time.time + 0.12f;
                    cachedObstacleDirection = GetObstacleAwareDirection(direction);
                }
                direction = Vector3.Slerp(direction, cachedObstacleDirection, 0.8f).normalized;
            }

            if (avoidObstacles && OpenPanelkaDoorsWhenChasing &&
                direction.sqrMagnitude > 0.001f && Time.time >= nextDoorProbeTime)
            {
                nextDoorProbeTime = Time.time + 0.2f;
                TryOpenPanelkaDoorAhead(direction);
            }

            if (direction.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 1f - Mathf.Exp(-TurnSharpness * aiDeltaTime));
            }

            Vector3 motion = direction * speed;
            if (controller.isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = -1f;
            }

            verticalVelocity += Gravity * aiDeltaTime;
            motion.y = verticalVelocity;
            MoveController(motion * aiDeltaTime);
            UpdateStuckState(beforeMove, direction, speed);
        }

        private void TryOpenPanelkaDoorAhead(Vector3 direction)
        {
            if (DoorOpenProbeRadius <= 0f)
            {
                return;
            }

            Vector3 probeCenter = transform.position + Vector3.up * 0.95f + direction.normalized * 0.55f;
            Collider[] hits = Physics.OverlapSphere(probeCenter, DoorOpenProbeRadius, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < hits.Length; i++)
            {
                Collider hit = hits[i];
                if (hit == null || hit.transform.IsChildOf(transform))
                {
                    continue;
                }

                MiniVanPanelkaRoomDoor door = hit.GetComponentInParent<MiniVanPanelkaRoomDoor>();
                if (door != null)
                {
                    door.ForceOpenForNpc();
                }
            }
        }

        private void MoveInDirection(Vector3 flatDirection, float speed)
        {
            Vector3 direction = Vector3.ProjectOnPlane(flatDirection, Vector3.up);
            if (direction.sqrMagnitude > 0.001f)
            {
                direction.Normalize();
            }

            Vector3 motion = direction * speed;
            if (controller.isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = -1f;
            }

            verticalVelocity += Gravity * aiDeltaTime;
            motion.y = verticalVelocity;
            MoveController(motion * aiDeltaTime);
        }

        private void ApplyStandingGravity()
        {
            if (controller.isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = -1f;
            }

            verticalVelocity += Gravity * aiDeltaTime;
            MoveController(Vector3.up * verticalVelocity * aiDeltaTime);
        }

        private void MoveController(Vector3 displacement)
        {
            if (controller == null)
            {
                return;
            }

            Vector3 current = transform.position;
            Vector3 desired = MiniVanGameModeInteriorZone.ConstrainZombieMovement(current,
                current + displacement);
            controller.Move(desired - current);
        }

        private void FaceFlatDirection(Vector3 flatDirection)
        {
            Vector3 direction = Vector3.ProjectOnPlane(flatDirection, Vector3.up);
            if (direction.sqrMagnitude <= 0.001f)
            {
                return;
            }

            Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 1f - Mathf.Exp(-TurnSharpness * aiDeltaTime));
        }

        private bool TryGetNavMeshSteerTarget(Vector3 targetPosition, out Vector3 steerTarget)
        {
            steerTarget = targetPosition;
            float targetMoveThresholdSqr = NavTargetMoveThreshold * NavTargetMoveThreshold;
            if (hasCachedNavSteerTarget && Time.time < nextNavPathRefreshTime &&
                (targetPosition - lastNavTarget).sqrMagnitude < targetMoveThresholdSqr)
            {
                steerTarget = cachedNavSteerTarget;
                return true;
            }

            nextNavPathRefreshTime = Time.time + Mathf.Max(0.05f, NavPathRefreshInterval);
            lastNavTarget = targetPosition;
            if (navMeshPath == null)
            {
                navMeshPath = new NavMeshPath();
            }

            if (!NavMesh.SamplePosition(transform.position, out NavMeshHit startHit, NavMeshSampleDistance, NavMesh.AllAreas))
            {
                hasCachedNavSteerTarget = false;
                return false;
            }

            if (!NavMesh.SamplePosition(targetPosition, out NavMeshHit targetHit, NavMeshSampleDistance, NavMesh.AllAreas))
            {
                hasCachedNavSteerTarget = false;
                return false;
            }

            if (!NavMesh.CalculatePath(startHit.position, targetHit.position, NavMesh.AllAreas, navMeshPath) || navMeshPath.status == NavMeshPathStatus.PathInvalid || navMeshPath.corners.Length < 2)
            {
                hasCachedNavSteerTarget = false;
                return false;
            }

            for (int i = 1; i < navMeshPath.corners.Length; i++)
            {
                Vector3 toCorner = Vector3.ProjectOnPlane(navMeshPath.corners[i] - transform.position, Vector3.up);
                if (toCorner.sqrMagnitude > 0.25f)
                {
                    steerTarget = navMeshPath.corners[i];
                    cachedNavSteerTarget = steerTarget;
                    hasCachedNavSteerTarget = true;
                    return true;
                }
            }

            hasCachedNavSteerTarget = false;
            return false;
        }

        private Vector3 GetObstacleAwareDirection(Vector3 desiredDirection)
        {
            Vector3 probeOrigin = transform.position + Vector3.up * 0.9f;
            if (Time.time < avoidanceBiasUntil && Mathf.Abs(avoidanceBiasSign) > 0.01f)
            {
                Vector3 biased = Quaternion.Euler(0f, 88f * avoidanceBiasSign, 0f) * desiredDirection;
                if (!IsDirectionBlocked(probeOrigin, biased, ObstacleProbeDistance * 0.8f, out _))
                {
                    return Vector3.Slerp(desiredDirection, biased.normalized, 0.92f).normalized;
                }
            }

            if (!IsDirectionBlocked(probeOrigin, desiredDirection, ObstacleProbeDistance, out RaycastHit forwardHit))
            {
                return desiredDirection;
            }

            Vector3 leftDirection = Quaternion.Euler(0f, -70f, 0f) * desiredDirection;
            Vector3 rightDirection = Quaternion.Euler(0f, 70f, 0f) * desiredDirection;
            float leftClearance = MeasureClearance(probeOrigin, leftDirection);
            float rightClearance = MeasureClearance(probeOrigin, rightDirection);
            Vector3 wallTangent = Vector3.Cross(Vector3.up, forwardHit.normal).normalized;
            if (Vector3.Dot(wallTangent, desiredDirection) < 0f)
            {
                wallTangent = -wallTangent;
            }

            Vector3 chosen = leftClearance > rightClearance ? leftDirection : rightDirection;
            chosen = Vector3.Slerp(chosen.normalized, wallTangent, 0.55f);
            return Vector3.Slerp(desiredDirection, chosen.normalized, Mathf.Clamp01(ObstacleAvoidanceStrength)).normalized;
        }

        private void UpdateStuckState(Vector3 beforeMove, Vector3 desiredDirection, float speed)
        {
            Vector3 moved = Vector3.ProjectOnPlane(transform.position - beforeMove, Vector3.up);
            bool triedToMove = desiredDirection.sqrMagnitude > 0.001f && speed > 0.05f;
            bool barelyMoved = moved.magnitude < speed * aiDeltaTime * 0.22f;
            if (!triedToMove || !barelyMoved)
            {
                stuckTimer = 0f;
                return;
            }

            stuckTimer += aiDeltaTime;
            if (stuckTimer < StuckCheckSeconds)
            {
                return;
            }

            Vector3 probeOrigin = transform.position + Vector3.up * 0.9f;
            Vector3 leftDirection = Quaternion.Euler(0f, -90f, 0f) * desiredDirection;
            Vector3 rightDirection = Quaternion.Euler(0f, 90f, 0f) * desiredDirection;
            float leftClearance = MeasureClearance(probeOrigin, leftDirection);
            float rightClearance = MeasureClearance(probeOrigin, rightDirection);
            avoidanceBiasSign = rightClearance > leftClearance ? 1f : -1f;
            avoidanceBiasUntil = Time.time + StuckSidestepSeconds;
            stuckTimer = 0f;
        }

        private float MeasureClearance(Vector3 origin, Vector3 direction)
        {
            if (IsDirectionBlocked(origin, direction, ObstacleProbeDistance, out RaycastHit hit))
            {
                return hit.distance;
            }

            return ObstacleProbeDistance;
        }

        private bool IsDirectionBlocked(Vector3 origin, Vector3 direction, float distance, out RaycastHit hit)
        {
            RaycastHit[] hits = Physics.SphereCastAll(origin, ObstacleProbeRadius, direction, distance, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
            for (int i = 0; i < hits.Length; i++)
            {
                Collider hitCollider = hits[i].collider;
                if (hitCollider == null || hitCollider.transform.IsChildOf(transform) || hitCollider.GetComponentInParent<MiniVanPlayer>() != null)
                {
                    continue;
                }

                hit = hits[i];
                return true;
            }

            hit = default;
            return false;
        }

        private void PublishNetworkTransform(bool force = false)
        {
            float interval = 1f / Mathf.Max(1f, NetworkTransformRate);
            if (!force && Time.time < nextNetworkTransformSendTime)
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
            nextNetworkTransformSendTime = Time.time + interval;
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

        protected virtual float GetMovementRecoverScale()
        {
            if (Time.time >= movementRecoverUntil || MovementRecoverSeconds <= 0.01f)
            {
                return 1f;
            }

            float recoverStartTime = movementRecoverUntil - MovementRecoverSeconds;
            float recover01 = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(recoverStartTime, movementRecoverUntil, Time.time));
            return Mathf.Lerp(Mathf.Clamp01(MovementRecoverStartScale), 1f, recover01);
        }

        [ClientRpc]
        private void HitFeedbackClientRpc(ClientRpcParams clientRpcParams = default)
        {
            hitFlashUntil = Time.time + HitFlashSeconds;
        }

        protected void CacheVisualRenderers()
        {
            visualRenderers = GetComponentsInChildren<Renderer>(true);
            if (hitFlashBlock == null)
            {
                hitFlashBlock = new MaterialPropertyBlock();
            }
        }

        private void UpdateHitFlashVisual()
        {
            if (visualRenderers == null || visualRenderers.Length == 0)
            {
                CacheVisualRenderers();
            }

            bool flashing = Time.time < hitFlashUntil;
            if (flashing == hitFlashWasApplied)
            {
                return;
            }
            hitFlashWasApplied = flashing;
            Color flashColor = new Color(1f, 0.06f, 0.03f, 1f);
            for (int i = 0; i < visualRenderers.Length; i++)
            {
                Renderer renderer = visualRenderers[i];
                if (renderer == null)
                {
                    continue;
                }

                if (!flashing)
                {
                    renderer.SetPropertyBlock(null);
                    SpriteRenderer clearSprite = renderer as SpriteRenderer;
                    if (clearSprite != null)
                    {
                        clearSprite.color = Color.white;
                    }

                    continue;
                }

                renderer.GetPropertyBlock(hitFlashBlock);
                hitFlashBlock.SetColor("_BaseColor", flashColor);
                hitFlashBlock.SetColor("_Color", flashColor);
                renderer.SetPropertyBlock(hitFlashBlock);

                SpriteRenderer spriteRenderer = renderer as SpriteRenderer;
                if (spriteRenderer != null)
                {
                    spriteRenderer.color = flashColor;
                }
            }
        }

        private float ResolveChaseSpeed(MiniVanPlayer target)
        {
            float normalPlayerSpeed = target != null ? target.WalkSpeed : 4.2f;
            float extra = ExtraSpeedKph / 3.6f;

            if (target != null && target.IsCoffeeBoostActive)
            {
                return normalPlayerSpeed + extra;
            }

            return normalPlayerSpeed + extra;
        }

        private void ConfigureController()
        {
            if (controller == null)
            {
                return;
            }

            controller.height = 1.9f;
            controller.radius = 0.34f;
            controller.center = new Vector3(0f, 0.95f, 0f);
            controller.stepOffset = 0.35f;
            controller.slopeLimit = 55f;
        }

        protected virtual void EnsureVisual()
        {
            Transform existingVisual = transform.Find("Zombie Visual");
            if (existingVisual != null)
            {
                EnsureLegVisuals(existingVisual);
                leftArm = existingVisual.Find("Left Arm");
                rightArm = existingVisual.Find("Right Arm");
                return;
            }

            GameObject root = new GameObject("Zombie Visual");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;

            Material bodyMaterial = CreateMaterial(new Color(0.08f, 0.72f, 0.22f, 1f));
            Material darkSideMaterial = CreateMaterial(new Color(0.03f, 0.44f, 0.28f, 1f));
            Material topMaterial = CreateMaterial(new Color(0.66f, 0.95f, 0.02f, 1f));
            Material eyeMaterial = CreateMaterial(new Color(1f, 0.02f, 0.08f, 1f));

            CreateCube(root.transform, "Body", new Vector3(0f, 0.9f, 0f), new Vector3(0.7f, 1.55f, 0.42f), bodyMaterial);
            CreateCube(root.transform, "Head", new Vector3(0f, 1.8f, 0.02f), new Vector3(0.74f, 0.62f, 0.5f), bodyMaterial);
            CreateCube(root.transform, "Head Top", new Vector3(0f, 2.13f, 0.02f), new Vector3(0.72f, 0.08f, 0.48f), topMaterial);
            CreateCube(root.transform, "Back Shade", new Vector3(0.22f, 0.96f, -0.235f), new Vector3(0.25f, 1.6f, 0.04f), darkSideMaterial);
            leftArm = CreateCube(root.transform, "Left Arm", new Vector3(-0.56f, 1.35f, 0.08f), new Vector3(0.22f, 0.95f, 0.22f), topMaterial).transform;
            rightArm = CreateCube(root.transform, "Right Arm", new Vector3(0.56f, 1.34f, 0.08f), new Vector3(0.22f, 0.95f, 0.22f), topMaterial).transform;
            leftArm.localRotation = Quaternion.Euler(70f, 0f, 70f);
            rightArm.localRotation = Quaternion.Euler(70f, 0f, -70f);
            CreateCube(root.transform, "Left Eye", new Vector3(-0.16f, 1.88f, 0.29f), new Vector3(0.07f, 0.13f, 0.035f), eyeMaterial);
            CreateCube(root.transform, "Right Eye", new Vector3(0.16f, 1.88f, 0.29f), new Vector3(0.07f, 0.13f, 0.035f), eyeMaterial);
            CreateCube(root.transform, "Mouth A", new Vector3(-0.12f, 1.65f, 0.295f), new Vector3(0.05f, 0.18f, 0.03f), eyeMaterial).transform.localRotation = Quaternion.Euler(0f, 0f, -35f);
            CreateCube(root.transform, "Mouth B", new Vector3(0.02f, 1.64f, 0.295f), new Vector3(0.05f, 0.2f, 0.03f), eyeMaterial).transform.localRotation = Quaternion.Euler(0f, 0f, 35f);
            CreateCube(root.transform, "Mouth C", new Vector3(0.18f, 1.65f, 0.295f), new Vector3(0.05f, 0.18f, 0.03f), eyeMaterial).transform.localRotation = Quaternion.Euler(0f, 0f, -35f);
            CreateLegVisuals(root.transform, bodyMaterial, darkSideMaterial);
        }

        private void EnsureLegVisuals(Transform visual)
        {
            if (visual == null || visual.Find("Left Leg") != null)
            {
                return;
            }

            Renderer bodyRenderer = visual.Find("Body") != null ? visual.Find("Body").GetComponent<Renderer>() : null;
            Renderer shadeRenderer = visual.Find("Back Shade") != null ? visual.Find("Back Shade").GetComponent<Renderer>() : null;
            Material bodyMaterial = bodyRenderer != null ? bodyRenderer.sharedMaterial : CreateMaterial(new Color(0.08f, 0.72f, 0.22f, 1f));
            Material shadeMaterial = shadeRenderer != null ? shadeRenderer.sharedMaterial : CreateMaterial(new Color(0.03f, 0.44f, 0.28f, 1f));
            CreateLegVisuals(visual, bodyMaterial, shadeMaterial);
        }

        private static void CreateLegVisuals(Transform visual, Material bodyMaterial, Material shadeMaterial)
        {
            CreateCube(visual, "Left Leg", new Vector3(-0.20f, 0.28f, 0f), new Vector3(0.26f, 0.55f, 0.30f), bodyMaterial);
            CreateCube(visual, "Right Leg", new Vector3(0.20f, 0.28f, 0f), new Vector3(0.26f, 0.55f, 0.30f), bodyMaterial);
            CreateCube(visual, "Left Foot", new Vector3(-0.20f, 0.08f, 0.14f), new Vector3(0.28f, 0.16f, 0.48f), shadeMaterial);
            CreateCube(visual, "Right Foot", new Vector3(0.20f, 0.08f, 0.14f), new Vector3(0.28f, 0.16f, 0.48f), shadeMaterial);
        }

        protected virtual void AnimateArms()
        {
            if (leftArm == null || rightArm == null)
            {
                return;
            }

            float wobble = Mathf.Sin(Time.time * 9f) * 8f;
            leftArm.localRotation = Quaternion.Euler(70f + wobble, 0f, 70f);
            rightArm.localRotation = Quaternion.Euler(70f - wobble, 0f, -70f);
        }

        private static GameObject CreateCube(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = position;
            cube.transform.localScale = scale;
            Renderer renderer = cube.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
            }

            Collider collider = cube.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
            }

            return cube;
        }

        private static Material CreateMaterial(Color color)
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
    

        private bool CanAttackPlayer(MiniVanPlayer player)
        {
            if (player == null || player.IsIgnoredByEnemies)
            {
                return false;
            }

            Vector3 attackOrigin = transform.position + Vector3.up * 1.05f;
            Vector3 targetPoint = GetPlayerBodyPoint(player);
            if (Vector3.Distance(attackOrigin, targetPoint) > AttackRange)
            {
                return false;
            }

            return HasDirectLineToPlayer(player, attackOrigin, targetPoint);
        }

        private Vector3 GetPlayerBodyPoint(MiniVanPlayer player)
        {
            if (player == null)
            {
                return Vector3.zero;
            }

            if (player.IsKnockedDown)
            {
                return player.transform.position + Vector3.up * 0.4f;
            }

            CharacterController playerController = player.GetComponent<CharacterController>();
            if (playerController != null && playerController.enabled)
            {
                return playerController.bounds.center;
            }

            return player.transform.position + Vector3.up;
        }

        private bool HasDirectLineToPlayer(MiniVanPlayer player, Vector3 origin, Vector3 targetPoint)
        {
            Vector3 direction = targetPoint - origin;
            float distance = direction.magnitude;
            if (player == null || distance <= 0.001f)
            {
                return player != null;
            }

            RaycastHit[] hits = Physics.RaycastAll(
                origin,
                direction / distance,
                distance + 0.05f,
                ~0,
                QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

            for (int i = 0; i < hits.Length; i++)
            {
                Collider hitCollider = hits[i].collider;
                if (hitCollider == null || hitCollider.transform.IsChildOf(transform))
                {
                    continue;
                }

                return hitCollider.GetComponentInParent<MiniVanPlayer>() == player;
            }

            // A remote player can temporarily have no enabled physics proxy while
            // ownership/seat state is synchronizing. No hit still means that no
            // world collider blocks the authoritative line of sight.
            return true;
        }
    }

}
