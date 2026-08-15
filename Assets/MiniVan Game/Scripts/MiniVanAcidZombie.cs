using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace MiniVanGame
{
    /// <summary>
    /// Ranged acid zombie: holds ~3m, never kites away, only spits (no melee).
    /// Death drops an acid clot and skips zombie gibs.
    /// </summary>
    public sealed class MiniVanAcidZombie : MiniVanZombie
    {
        public GameObject VisualPrefab;
        [Header("Acid Range")]
        [Min(0.5f)] public float PreferredRange = 3f;
        [Min(0.5f)] public float SpitIntervalMin = 0.75f;
        [Min(0.5f)] public float SpitIntervalMax = 1f;
        [Min(1f)] public float SpitSpeed = 16f;
        [Min(0.5f)] public float SpitMaxRange = 14f;
        [Header("Acid Aim")]
        [Min(0.1f)] public float AimLockSeconds = 0.55f;
        [Min(0.2f)] public float MaxAimWaitSeconds = 1.2f;
        [Min(0.5f)] public float StrafeSpeedToWait = 4.6f;
        [Min(0.5f)] public float LeadGain = 1.15f;
        [Min(0.5f)] public float MaxLeadDistance = 3.4f;
        [Header("Acid FX")]
        public GameObject SpitPrefab;
        public GameObject SplashPrefab;
        public GameObject DripParticlePrefab;
        public GameObject PuddlePrefab;
        [Header("Acid Death")]
        [Min(0.1f)] public float DeathFallSeconds = 0.35f;
        [Min(0f)] public float DeathLieSeconds = 1f;
        [Min(0.05f)] public float DeathLieGroundHeight = 0.42f;
        [Header("Acid Spit")]
        [Min(1f)] public float SpitFastMultiplier = 3f;
        [Min(1)] public int SpitFastEveryN = 5;

        public override string EnemyDisplayName => "Acid Zombie";

        public override int CurrentHealth =>
            UsesOfflineHealth()
                ? Mathf.Max(0, MaxHealth - offlineHitsTaken)
                : health.Value;

        private Transform visualRoot;
        private ParticleSystem dripParticles;
        private Vector3 spawnPosePosition;
        private Quaternion spawnPoseRotation = Quaternion.identity;
        private float spawnPoseLockUntil;
        private bool hasSpawnPose;
        private bool visualsReady;
        private MiniVanPlayer awareTarget;
        private float lastAwareTime = -999f;
        private float nextSpitTime;
        private float spitAnimUntil;
        private bool aiming;
        private float aimStartedAt;
        private Vector3 trackedVelocity;
        private Vector3 lastTrackedAim;
        private bool hasTrackedAim;
        private Vector3 patrolOrigin;
        private Quaternion patrolFacing = Quaternion.identity;
        private Vector3 acidPatrolTarget;
        private float acidNextPatrolPickTime;
        private int acidPatrolSide = 1;
        private bool hasPatrolOrigin;
        private float walkPhase;
        private Vector3 lastAnimPosition;
        private bool hasLastAnimPosition;
        private int offlineHitsTaken;
        private bool isDying;
        private static MiniVanPlayer[] cachedPlayers;
        private static float nextPlayerCacheTime;

        public void ApplySpawnPose(Vector3 position, Quaternion rotation)
        {
            hasSpawnPose = true;
            spawnPosePosition = position;
            spawnPoseRotation = rotation;
            spawnPoseLockUntil = Time.time + 0.85f;
            if (controller == null)
            {
                controller = GetComponent<CharacterController>();
            }

            if (controller != null)
            {
                controller.enabled = false;
            }

            transform.SetPositionAndRotation(position, rotation);
        }

        private void Start()
        {
            MaxHealth = Mathf.Max(1, MaxHealth);
            DamagePerHit = 1;
            AttackInterval = 1.75f;
            ExtraSpeedKph = -1.2f;
            OpenPanelkaDoorsWhenChasing = true;
            EnsureAcidVisuals();
            nextSpitTime = Time.time + Random.Range(0.4f, 1.0f);
            if (hasSpawnPose)
            {
                transform.SetPositionAndRotation(spawnPosePosition, spawnPoseRotation);
            }
        }

        private void LateUpdate()
        {
            EnsureAcidVisuals();
            if (isDying || deathPartsSpawned)
            {
                return;
            }

            if (hasSpawnPose && Time.time < spawnPoseLockUntil)
            {
                if (controller != null && controller.enabled)
                {
                    controller.enabled = false;
                }

                transform.SetPositionAndRotation(spawnPosePosition, spawnPoseRotation);
                UpdateLimbAnimation(false);
                return;
            }

            if (!HasAiAuthority())
            {
                UpdateLimbAnimation(IsMovingForWalkAnim());
                return;
            }

            if (ApplyKnockbackMotion(Time.deltaTime))
            {
                UpdateLimbAnimation(false);
                return;
            }

            if (controller != null && !controller.enabled)
            {
                controller.enabled = true;
            }

            MiniVanPlayer target = FindAwarePlayer(out float distance);
            if (target == null)
            {
                aiming = false;
                hasTrackedAim = false;
                bool patrolling = TickPatrol();
                UpdateLimbAnimation(patrolling);
                return;
            }

            MiniVanVehicle vehicle = target.CurrentVehicle;
            Vector3 aimPoint = ResolveAimPoint(target, vehicle);
            TrackTargetMotion(aimPoint, vehicle);
            Vector3 flat = Vector3.ProjectOnPlane(aimPoint - transform.position, Vector3.up);
            float planarDistance = flat.magnitude;
            bool walking = false;
            bool holdForAim = aiming && planarDistance <= PreferredRange + 2.5f;
            if (flat.sqrMagnitude > 0.001f)
            {
                Vector3 dir = flat.normalized;
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    Quaternion.LookRotation(dir, Vector3.up),
                    1f - Mathf.Exp(-10f * Time.deltaTime));

                if (!holdForAim &&
                    planarDistance > PreferredRange + 0.15f &&
                    controller != null &&
                    controller.enabled)
                {
                    float speed = Mathf.Max(0.5f, target.WalkSpeed + ExtraSpeedKph / 3.6f) *
                                  GetMovementRecoverScale();
                    walking = MoveFlat(dir, speed);
                }
                else
                {
                    ApplyStandingGravityLocal();
                }
            }
            else
            {
                ApplyStandingGravityLocal();
            }

            TickSpit(target, vehicle, planarDistance);
            UpdateLimbAnimation(walking);
        }

        protected override bool TryOverrideServerUpdate()
        {
            return true;
        }

        protected override void EnsureVisual()
        {
            EnsureAcidVisuals();
        }

        protected override void AnimateArms()
        {
        }

        protected override void ServerSpawnDeathParts(Vector3 impulse)
        {
            BeginAcidDeath(transform.position - transform.forward, impulse);
        }

        public override void TakeBatHit(
            int damage,
            Vector3 hitOrigin,
            float knockbackDistance,
            float knockbackSeconds,
            bool fromAspenStake = false)
        {
            if (isDying || deathPartsSpawned)
            {
                return;
            }

            int applied = Mathf.Max(1, damage);
            BeginHitKnockback(hitOrigin, knockbackDistance, knockbackSeconds);
            TriggerHitFlash();
            OnDamagedByBat(applied);

            if (UsesOfflineHealth())
            {
                offlineHitsTaken += applied;
                if (offlineHitsTaken >= Mathf.Max(1, MaxHealth))
                {
                    BeginAcidDeath(hitOrigin, knockbackVelocity);
                }

                return;
            }

            if (!IsServer || health.Value <= 0)
            {
                return;
            }

            health.Value = Mathf.Max(0, health.Value - applied);
            if (health.Value <= 0)
            {
                BeginAcidDeath(hitOrigin, knockbackVelocity);
            }
        }

        public override bool ServerRoadkill(Vector3 impactVelocity)
        {
            if (isDying || deathPartsSpawned)
            {
                return false;
            }

            if (UsesOfflineHealth())
            {
                BeginAcidDeath(transform.position - transform.forward, impactVelocity);
                return true;
            }

            if (!IsServer || health.Value <= 0)
            {
                return false;
            }

            health.Value = 0;
            BeginAcidDeath(transform.position - transform.forward, impactVelocity);
            return true;
        }

        private void BeginAcidDeath(Vector3 hitOrigin, Vector3 impulse)
        {
            if (isDying || deathPartsSpawned)
            {
                return;
            }

            isDying = true;
            deathPartsSpawned = true;
            if (IsSpawned && IsServer)
            {
                AcidDeathClientRpc(hitOrigin, impulse);
            }

            StartCoroutine(AcidDeathSequenceRoutine(hitOrigin, impulse, true));
        }

        [ClientRpc]
        private void AcidDeathClientRpc(Vector3 hitOrigin, Vector3 impulse)
        {
            if (IsServer)
            {
                return;
            }

            isDying = true;
            deathPartsSpawned = true;
            StartCoroutine(AcidDeathSequenceRoutine(hitOrigin, impulse, false));
        }

        private IEnumerator AcidDeathSequenceRoutine(Vector3 hitOrigin, Vector3 impulse, bool isAuthority)
        {
            if (controller != null)
            {
                controller.enabled = false;
            }

            if (dripParticles != null)
            {
                dripParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }

            Quaternion startRotation = transform.rotation;
            float tipSign = Vector3.Dot(
                Vector3.ProjectOnPlane(transform.position - hitOrigin, Vector3.up),
                transform.right) >= 0f
                ? 1f
                : -1f;
            Quaternion endRotation = startRotation * Quaternion.Euler(0f, 0f, tipSign * 90f);
            Vector3 startPosition = transform.position;
            float groundY = startPosition.y;
            if (Physics.Raycast(
                    startPosition + Vector3.up * 2f,
                    Vector3.down,
                    out RaycastHit ground,
                    5f,
                    ~0,
                    QueryTriggerInteraction.Ignore))
            {
                groundY = ground.point.y;
            }

            transform.SetPositionAndRotation(new Vector3(startPosition.x, groundY, startPosition.z), endRotation);
            float endY = groundY + Mathf.Max(0.05f, DeathLieGroundHeight);
            if (TryGetVisualBounds(out Bounds bodyBounds))
            {
                endY = transform.position.y + (groundY - bodyBounds.min.y) + 0.03f;
            }

            Vector3 endPosition = new Vector3(startPosition.x, endY, startPosition.z);
            transform.SetPositionAndRotation(startPosition, startRotation);

            float fallSeconds = Mathf.Max(0.1f, DeathFallSeconds);
            float elapsed = 0f;
            while (elapsed < fallSeconds)
            {
                elapsed += Time.deltaTime;
                float u = Mathf.Clamp01(elapsed / fallSeconds);
                float t = u * u;
                transform.rotation = Quaternion.Slerp(startRotation, endRotation, t);
                transform.position = Vector3.Lerp(startPosition, endPosition, t);
                yield return null;
            }

            transform.SetPositionAndRotation(endPosition, endRotation);
            if (TryGetVisualBounds(out Bounds settledBounds))
            {
                transform.position += Vector3.up * (groundY - settledBounds.min.y + 0.03f);
            }

            if (DeathLieSeconds > 0.001f)
            {
                yield return new WaitForSeconds(DeathLieSeconds);
            }

            Vector3 clotPos = transform.position + Vector3.up * 0.2f;
            SpawnAcidClotLocal(clotPos, impulse);
            if (dripParticles != null)
            {
                dripParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            if (visualRoot != null)
            {
                visualRoot.gameObject.SetActive(false);
            }

            if (!isAuthority)
            {
                yield break;
            }

            if (IsSpawned && IsServer && NetworkObject != null && NetworkObject.IsSpawned)
            {
                NetworkObject.Despawn(true);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private bool TryGetVisualBounds(out Bounds bounds)
        {
            bounds = new Bounds();
            Transform root = visualRoot != null ? visualRoot : transform;
            MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            bool any = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                MeshRenderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled || renderer.forceRenderingOff)
                {
                    continue;
                }

                if (!any)
                {
                    bounds = renderer.bounds;
                    any = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return any;
        }

        private static void SpawnAcidClotLocal(Vector3 position, Vector3 impulse)
        {
            GameObject root = MiniVanAcidClotVisual.Create(null, true);
            root.name = "AcidClot_" + Time.frameCount;
            root.transform.position = position;
            root.transform.rotation = Random.rotation;

            MiniVanPizzaItem pickup = root.AddComponent<MiniVanPizzaItem>();
            pickup.Item = MiniVanInventoryItem.AcidClot;
            pickup.Type = MiniVanPizzaItemType.Ingredient;
            pickup.PickupRadius = 2.3f;
            pickup.CanHoldInHands = true;
            pickup.CanPutInInventory = true;

            SphereCollider hull = root.GetComponent<SphereCollider>();
            if (hull == null)
            {
                hull = root.AddComponent<SphereCollider>();
            }

            hull.radius = 0.18f;
            hull.center = Vector3.zero;

            Rigidbody body = root.AddComponent<Rigidbody>();
            body.mass = 0.85f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.linearDamping = 0.18f;
            body.angularDamping = 0.42f;
            body.linearVelocity = impulse * 0.12f + Vector3.up * Random.Range(1.4f, 2.4f) +
                                  Random.insideUnitSphere * 0.8f;
            body.angularVelocity = Random.insideUnitSphere * 5f;
        }

        private void TickSpit(MiniVanPlayer target, MiniVanVehicle vehicle, float planarDistance)
        {
            if (planarDistance > SpitMaxRange)
            {
                aiming = false;
                return;
            }

            if (!aiming)
            {
                if (Time.time >= nextSpitTime)
                {
                    aiming = true;
                    aimStartedAt = Time.time;
                    spitAnimUntil = Time.time + AimLockSeconds;
                }

                return;
            }

            spitAnimUntil = Mathf.Max(spitAnimUntil, Time.time + 0.05f);
            bool jumping = IsTargetJumpingUp(target, vehicle);
            bool strafingHard = Vector3.ProjectOnPlane(trackedVelocity, Vector3.up).magnitude >= StrafeSpeedToWait;
            float needed = AimLockSeconds;
            if (jumping)
            {
                needed += 0.4f;
            }
            else if (strafingHard)
            {
                needed += 0.28f;
            }

            needed = Mathf.Min(needed, MaxAimWaitSeconds);
            if (Time.time < aimStartedAt + needed)
            {
                return;
            }

            float speed = ResolveSpitLaunchSpeed();
            FireSpit(PredictAimPoint(target, vehicle, speed), speed);
            aiming = false;
        }

        private float ResolveSpitLaunchSpeed()
        {
            int everyN = Mathf.Max(1, SpitFastEveryN);
            bool fast = Random.Range(0, everyN) == 0;
            return Mathf.Max(1f, SpitSpeed) * (fast ? Mathf.Max(1f, SpitFastMultiplier) : 1f);
        }

        private void FireSpit(Vector3 aimPoint, float speed)
        {
            nextSpitTime = Time.time + Random.Range(
                Mathf.Min(SpitIntervalMin, SpitIntervalMax),
                Mathf.Max(SpitIntervalMin, SpitIntervalMax));
            spitAnimUntil = Time.time + 0.28f;
            Vector3 origin = GetMouthOrigin();
            Vector3 toAim = aimPoint - origin;
            if (toAim.sqrMagnitude < 0.01f)
            {
                toAim = transform.forward;
            }

            speed = Mathf.Max(1f, speed);
            Vector3 velocity = toAim.normalized * speed;
            float flightTime = toAim.magnitude / speed;
            velocity.y += 0.22f * 0.42f * Mathf.Abs(Physics.gravity.y) * flightTime;
            SpawnSpit(origin, velocity, true);
            if (IsSpawned && IsServer)
            {
                SpitClientRpc(origin, velocity);
            }
        }

        [ClientRpc]
        private void SpitClientRpc(Vector3 origin, Vector3 velocity)
        {
            if (IsServer)
            {
                return;
            }

            spitAnimUntil = Time.time + 0.28f;
            SpawnSpit(origin, velocity, false);
        }

        private void SpawnSpit(Vector3 origin, Vector3 velocity, bool dealsDamage)
        {
            MiniVanAcidSpit.Spawn(SpitPrefab, origin, velocity, dealsDamage, this, SplashPrefab);
        }

        private Vector3 GetMouthOrigin()
        {
            return transform.position + Vector3.up * 1.62f + transform.forward * 0.55f;
        }

        private static Vector3 ResolveAimPoint(MiniVanPlayer target, MiniVanVehicle vehicle)
        {
            if (vehicle != null)
            {
                return vehicle.GetClosestVehicleBodyPoint(target.transform.position + Vector3.up * 0.8f);
            }

            return target.transform.position + Vector3.up * 0.85f;
        }

        private Vector3 PredictAimPoint(MiniVanPlayer target, MiniVanVehicle vehicle, float spitSpeed)
        {
            Vector3 current = ResolveAimPoint(target, vehicle);
            Vector3 velocity = GetTargetVelocity(target, vehicle);
            if (hasTrackedAim)
            {
                velocity = Vector3.Lerp(velocity, trackedVelocity, 0.55f);
            }

            Vector3 origin = GetMouthOrigin();
            float distance = Vector3.Distance(origin, current);
            float flightTime = distance / Mathf.Max(1f, spitSpeed);
            Vector3 lead = velocity * flightTime * LeadGain;
            lead.y = Mathf.Min(lead.y, 0f);
            if (vehicle == null && IsTargetAirborne(target))
            {
                lead.y = Mathf.Min(lead.y, -0.15f);
            }

            lead = Vector3.ClampMagnitude(lead, MaxLeadDistance);
            return current + lead;
        }

        private void TrackTargetMotion(Vector3 aimPoint, MiniVanVehicle vehicle)
        {
            if (!hasTrackedAim)
            {
                lastTrackedAim = aimPoint;
                trackedVelocity = GetTargetVelocity(null, vehicle);
                hasTrackedAim = true;
                return;
            }

            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            Vector3 raw = (aimPoint - lastTrackedAim) / dt;
            trackedVelocity = Vector3.Lerp(trackedVelocity, raw, 1f - Mathf.Exp(-11f * dt));
            lastTrackedAim = aimPoint;
        }

        private static Vector3 GetTargetVelocity(MiniVanPlayer target, MiniVanVehicle vehicle)
        {
            if (vehicle != null)
            {
                Rigidbody body = vehicle.GetComponent<Rigidbody>();
                return body != null ? body.linearVelocity : Vector3.zero;
            }

            if (target == null)
            {
                return Vector3.zero;
            }

            CharacterController playerController = target.CharacterController != null
                ? target.CharacterController
                : target.GetComponent<CharacterController>();
            return playerController != null ? playerController.velocity : Vector3.zero;
        }

        private static bool IsTargetAirborne(MiniVanPlayer target)
        {
            CharacterController playerController = target != null ? target.CharacterController : null;
            return playerController != null && playerController.enabled && !playerController.isGrounded;
        }

        private static bool IsTargetJumpingUp(MiniVanPlayer target, MiniVanVehicle vehicle)
        {
            if (vehicle != null || !IsTargetAirborne(target))
            {
                return false;
            }

            return target.CharacterController.velocity.y > 0.4f;
        }

        private bool TickPatrol()
        {
            EnsurePatrolOrigin();
            Vector3 flat = Vector3.ProjectOnPlane(acidPatrolTarget - transform.position, Vector3.up);
            if (flat.magnitude <= PatrolPointReachDistance || Time.time >= acidNextPatrolPickTime)
            {
                PickPatrolTarget(false);
                flat = Vector3.ProjectOnPlane(acidPatrolTarget - transform.position, Vector3.up);
            }

            if (flat.sqrMagnitude <= 0.001f)
            {
                ApplyStandingGravityLocal();
                return false;
            }

            Vector3 dir = flat.normalized;
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(dir, Vector3.up),
                1f - Mathf.Exp(-7f * Time.deltaTime));
            float speed = (PatrolSpeedKph / 3.6f) * GetMovementRecoverScale();
            return MoveFlat(dir, speed);
        }

        private void EnsurePatrolOrigin()
        {
            if (hasPatrolOrigin)
            {
                return;
            }

            patrolOrigin = hasSpawnPose ? spawnPosePosition : transform.position;
            patrolFacing = hasSpawnPose ? spawnPoseRotation : transform.rotation;
            hasPatrolOrigin = true;
            PickPatrolTarget(true);
        }

        private void PickPatrolTarget(bool immediate)
        {
            acidPatrolSide = -acidPatrolSide;
            float radius = Mathf.Max(0.5f, PatrolRadius);
            Vector3 right = patrolFacing * Vector3.right;
            Vector3 forward = patrolFacing * Vector3.forward;
            float sideDistance = Random.Range(radius * 0.4f, radius);
            acidPatrolTarget = patrolOrigin +
                           right * acidPatrolSide * sideDistance +
                           forward * Random.Range(-radius * 0.28f, radius * 0.28f);
            if (Physics.Raycast(
                    acidPatrolTarget + Vector3.up * 6f,
                    Vector3.down,
                    out RaycastHit hit,
                    12f,
                    ~0,
                    QueryTriggerInteraction.Ignore))
            {
                acidPatrolTarget = hit.point;
            }

            acidNextPatrolPickTime = Time.time + (immediate
                ? 0.15f
                : PatrolWaitSeconds + Random.Range(0.35f, 1.1f));
        }

        private bool MoveFlat(Vector3 dir, float speed)
        {
            if (controller == null || !controller.enabled)
            {
                return false;
            }

            Vector3 motion = dir * Mathf.Max(0.2f, speed);
            motion.y = controller.isGrounded ? -2f : Gravity * Time.deltaTime;
            controller.Move(motion * Time.deltaTime);
            return true;
        }

        private void EnsureAcidVisuals()
        {
            if (visualsReady && visualRoot != null)
            {
                return;
            }

            Transform zombieVisual = transform.Find("Zombie Visual");
            if (zombieVisual != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(zombieVisual.gameObject);
                }
                else
                {
                    DestroyImmediate(zombieVisual.gameObject);
                }
            }

            visualRoot = transform.Find("AcidZombie Visual");
            if (visualRoot == null && VisualPrefab != null)
            {
                GameObject visual = Instantiate(VisualPrefab, transform);
                visual.name = "AcidZombie Visual";
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                visual.transform.localScale = Vector3.one;
                DisableColliders(visual);
                visualRoot = visual.transform;
            }

            if (visualRoot == null)
            {
                GameObject placeholder = new GameObject("AcidZombie Visual");
                placeholder.transform.SetParent(transform, false);
                visualRoot = placeholder.transform;
            }

            leftArm = FindDeep(visualRoot, "Left Arm");
            rightArm = FindDeep(visualRoot, "Right Arm");
            EnsureDripParticles();
            CacheVisualRenderers();
            visualsReady = true;
        }

        private void EnsureDripParticles()
        {
            Transform existing = transform.Find("AcidDrip");
            if (existing != null)
            {
                dripParticles = existing.GetComponent<ParticleSystem>();
                MiniVanAcidDripCollision existingCollision = existing.GetComponent<MiniVanAcidDripCollision>();
                if (existingCollision == null)
                {
                    existingCollision = existing.gameObject.AddComponent<MiniVanAcidDripCollision>();
                }

                if (existingCollision.PuddlePrefab == null)
                {
                    existingCollision.PuddlePrefab = PuddlePrefab;
                }

                MiniVanAcidDripCollision.ConfigureCollision(dripParticles);
                if (dripParticles != null && !dripParticles.isPlaying)
                {
                    dripParticles.Play(true);
                }

                return;
            }

            if (DripParticlePrefab != null)
            {
                GameObject drip = Instantiate(DripParticlePrefab, transform);
                drip.name = "AcidDrip";
                drip.transform.localPosition = new Vector3(0f, 1.1f, 0.05f);
                dripParticles = drip.GetComponent<ParticleSystem>();
                MiniVanAcidDripCollision dripCollision = drip.GetComponent<MiniVanAcidDripCollision>();
                if (dripCollision == null)
                {
                    dripCollision = drip.AddComponent<MiniVanAcidDripCollision>();
                }

                if (PuddlePrefab != null)
                {
                    dripCollision.PuddlePrefab = PuddlePrefab;
                }

                MiniVanAcidDripCollision.ConfigureCollision(dripParticles);
                return;
            }

            GameObject runtime = new GameObject("AcidDrip");
            runtime.transform.SetParent(transform, false);
            runtime.transform.localPosition = new Vector3(0f, 1.1f, 0.05f);
            dripParticles = BuildDripParticles(runtime);
            MiniVanAcidDripCollision runtimeCollision = runtime.GetComponent<MiniVanAcidDripCollision>();
            if (runtimeCollision != null)
            {
                runtimeCollision.PuddlePrefab = PuddlePrefab;
            }
        }

        public static ParticleSystem BuildDripParticles(GameObject host)
        {
            ParticleSystem ps = host.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = 2f;
            main.startSpeed = 0.2f;
            main.startSize = 0.09f;
            main.startColor = MiniVanAcidClotVisual.AcidHot;
            main.gravityModifier = 1.8f;
            main.maxParticles = 96;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 22f;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(0.35f, 0.9f, 0.2f);

            ParticleSystem.ColorOverLifetimeModule color = ps.colorOverLifetime;
            color.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(MiniVanAcidClotVisual.AcidHot, 0f),
                    new GradientColorKey(MiniVanAcidClotVisual.Acid, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0.95f, 0f),
                    new GradientAlphaKey(0.15f, 1f)
                });
            color.color = gradient;

            ParticleSystemRenderer renderer = host.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ??
                                Shader.Find("Particles/Standard Unlit");
                if (shader != null)
                {
                    Material mat = new Material(shader);
                    mat.SetColor("_BaseColor", MiniVanAcidClotVisual.AcidHot);
                    mat.color = MiniVanAcidClotVisual.AcidHot;
                    renderer.sharedMaterial = mat;
                    renderer.renderMode = ParticleSystemRenderMode.Billboard;
                }
            }

            MiniVanAcidDripCollision.ConfigureCollision(ps);
            MiniVanAcidDripCollision dripCollision = host.GetComponent<MiniVanAcidDripCollision>();
            if (dripCollision == null)
            {
                dripCollision = host.AddComponent<MiniVanAcidDripCollision>();
            }

            ps.Play(true);
            return ps;
        }

        private void UpdateLimbAnimation(bool walking)
        {
            float dt = Time.deltaTime;
            if (walking)
            {
                walkPhase += dt * 7.2f;
            }
            else
            {
                walkPhase = Mathf.MoveTowards(walkPhase, Mathf.Round(walkPhase / Mathf.PI) * Mathf.PI, dt * 8f);
            }

            float swing = Mathf.Sin(walkPhase) * (walking ? 28f : 6f);
            float spit = Time.time < spitAnimUntil ? -42f : 8f;
            if (leftArm != null)
            {
                leftArm.localRotation = Quaternion.Euler(spit + swing, 0f, 8f);
            }

            if (rightArm != null)
            {
                rightArm.localRotation = Quaternion.Euler(spit - swing, 0f, -8f);
            }
        }

        private bool IsMovingForWalkAnim()
        {
            Vector3 pos = transform.position;
            if (!hasLastAnimPosition)
            {
                lastAnimPosition = pos;
                hasLastAnimPosition = true;
                return false;
            }

            Vector3 delta = Vector3.ProjectOnPlane(pos - lastAnimPosition, Vector3.up);
            lastAnimPosition = pos;
            return delta.magnitude > 0.01f;
        }

        private void ApplyStandingGravityLocal()
        {
            if (controller == null || !controller.enabled)
            {
                return;
            }

            Vector3 motion = Vector3.zero;
            motion.y = controller.isGrounded ? -2f : Gravity * Time.deltaTime;
            controller.Move(motion * Time.deltaTime);
        }

        private MiniVanPlayer FindAwarePlayer(out float distance)
        {
            distance = float.MaxValue;
            float detectRange = Mathf.Max(0.5f, DetectionRange);
            Vector3 origin = transform.position;
            MiniVanPlayer best = null;
            float bestDistance = float.MaxValue;
            MiniVanPlayer[] players = CollectLivingPlayers();
            for (int i = 0; i < players.Length; i++)
            {
                MiniVanPlayer player = players[i];
                if (!IsCombatTarget(player))
                {
                    continue;
                }

                float d = GetDetectionDistance(origin, player);
                if (d > detectRange || d >= bestDistance)
                {
                    continue;
                }

                if (d > HearingRange && !IsPlayerInView(player))
                {
                    continue;
                }

                best = player;
                bestDistance = d;
            }

            if (best != null)
            {
                awareTarget = best;
                lastAwareTime = Time.time;
                distance = bestDistance;
                return awareTarget;
            }

            if (IsCombatTarget(awareTarget) && Time.time - lastAwareTime <= ChaseMemorySeconds)
            {
                distance = GetDetectionDistance(origin, awareTarget);
                if (distance <= detectRange * 1.75f)
                {
                    return awareTarget;
                }
            }

            awareTarget = null;
            return null;
        }

        private static bool IsCombatTarget(MiniVanPlayer player)
        {
            return player != null && !player.IsIgnoredByEnemies;
        }

        private bool IsPlayerInView(MiniVanPlayer player)
        {
            Vector3 toTarget = Vector3.ProjectOnPlane(
                player.transform.position - transform.position,
                Vector3.up);
            if (toTarget.sqrMagnitude <= 0.001f)
            {
                return true;
            }

            return Vector3.Angle(transform.forward, toTarget.normalized) <= FieldOfViewDegrees * 0.5f;
        }

        private static float GetDetectionDistance(Vector3 origin, MiniVanPlayer player)
        {
            MiniVanVehicle vehicle = player != null ? player.CurrentVehicle : null;
            if (vehicle != null)
            {
                return vehicle.GetDistanceFromVehicleBody(origin + Vector3.up * 0.8f);
            }

            return Vector3.Distance(origin, player.transform.position);
        }

        private static MiniVanPlayer[] CollectLivingPlayers()
        {
            if (cachedPlayers == null || Time.time >= nextPlayerCacheTime)
            {
                cachedPlayers = Object.FindObjectsByType<MiniVanPlayer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                nextPlayerCacheTime = Time.time + 0.35f;
            }

            return cachedPlayers;
        }

        private static bool HasAiAuthority()
        {
            NetworkManager network = NetworkManager.Singleton;
            if (network == null || !network.IsListening)
            {
                return true;
            }

            return network.IsServer;
        }

        private bool UsesOfflineHealth()
        {
            NetworkManager network = NetworkManager.Singleton;
            return network == null || !network.IsListening || !IsSpawned;
        }

        private static void DisableColliders(GameObject root)
        {
            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = false;
            }
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root == null)
            {
                return null;
            }

            if (root.name == name)
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDeep(root.GetChild(i), name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
