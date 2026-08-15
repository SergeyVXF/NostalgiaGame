using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace MiniVanGame
{
    /// <summary>
    /// Vampire enemy: approaches as a bat, lands as a walking humanoid, reverts to bat when
    /// the player pulls away. Human form reuses zombie chase / doors / minivan attacks.
    /// </summary>
    public sealed class MiniVanVampire : MiniVanZombie
    {
        public enum Form : byte
        {
            Bat = 0,
            Human = 1
        }

        [Header("Vampire")]
        public GameObject HumanVisualPrefab;
        public Texture2D BatWingOutTexture;
        public Texture2D BatWingUpTexture;
        [Header("Vampire Death")]
        [Min(0.1f)] public float DeathFallSeconds = 0.35f;
        [Min(0f)] public float DeathLieSeconds = 0.2f;
        [Tooltip("How high the root sits above ground while lying on its side.")]
        [Min(0.05f)] public float DeathLieGroundHeight = 0.42f;
        [Tooltip("Prefab with a ParticleSystem played once on death.")]
        public GameObject DeathParticlePrefab;
        [Tooltip("Static ash pile spawned at the death point.")]
        public GameObject DeathPowderPrefab;
        [Header("Vampire Shield")]
        [Tooltip("Aura material (slot) removed while a holy cross is aimed at this vampire.")]
        public Material ShieldAuraMaterial;
        [Tooltip("Optional: one or more particle prefabs played once when the shield breaks.")]
        public GameObject[] ShieldBreakParticlePrefabs;
        [Tooltip("Damage taken while the aura shield is up (0.03 = 3%).")]
        [Range(0.01f, 1f)] public float ShieldDamageTakenScale = 0.03f;
        [Tooltip("Bat hits needed to kill after the holy cross removes the shield.")]
        [Min(1)] public int BatHitsToKillWhenExposed = 7;
        [Header("Vampire Ram")]
        [Tooltip("Log minivan ram / stun events to the console.")]
        public bool DebugVampireRam = true;
        [Header("Vampire Movement")]
        [Min(1f)] public float TransformToHumanDistance = 5f;
        [Min(1f)] public float TransformToBatDistance = 10f;
        [Min(0.5f)] public float BatFlySpeedMps = 7.5f;
        [Min(0.05f)] public float BatFlapSeconds = 0.12f;
        [Min(1)] public int HitsBeforeStagger = 3;
        [Tooltip("Pressure slowdown lasts a random duration in this range.")]
        [Min(0.1f)] public float StaggerSecondsMin = 1f;
        [Min(0.1f)] public float StaggerSecondsMax = 1.5f;
        [Tooltip("Speed multiplier while under pressure (0.6 = 40% slower).")]
        [Range(0.05f, 1f)] public float StaggerSpeedScale = 0.6f;
        [Min(0.2f)] public float BatBodyRadius = 0.2f;
        [Tooltip("World-space bat sprite height (~1/4 of a 1.9m zombie).")]
        [Min(0.1f)] public float BatWorldHeight = 0.48f;
        [Min(0.5f)] public float BatEyeHeightFallback = 1.6f;
        [Header("Vampire Animation")]
        [Min(0.1f)] public float WalkSwingDegrees = 42f;
        [Min(0.5f)] public float WalkAnimSpeed = 7.5f;
        [Min(0.05f)] public float AttackWindupSeconds = 0.18f;
        [Min(0.05f)] public float AttackStrikeSeconds = 0.08f;
        [Min(0.05f)] public float AttackRecoverSeconds = 0.16f;
        [Tooltip("Arm raise toward eye height (degrees on X). Negative = up/front.")]
        public float AttackRaiseAngle = -75f;
        [Tooltip("Arm slam angle after the strike (degrees on X). Positive = down/front.")]
        public float AttackSlamAngle = 40f;

        private readonly NetworkVariable<byte> networkForm = new NetworkVariable<byte>(
            (byte)Form.Bat,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private Transform humanVisual;
        private Transform batVisual;
        private SpriteRenderer batSpriteRenderer;
        private Material batMaterial;
        private Sprite batWingOutSprite;
        private Sprite batWingUpSprite;
        private float nextFlapTime;
        private bool flapWingUp;
        private int consecutiveHits;
        private float staggerUntil;
        private bool visualsReady;
        private Form offlineForm = Form.Bat;
        private float nextOfflineMeleeTime;
        private int offlineHitsTaken;
        private Vector3 spawnPosePosition;
        private Quaternion spawnPoseRotation = Quaternion.identity;
        private float spawnPoseLockUntil;
        private bool hasSpawnPose;
        private Transform armPivotL;
        private Transform armPivotR;
        private Transform legPivotL;
        private Transform legPivotR;
        private bool limbPivotsReady;
        private float walkPhase;
        private float attackAnimTime = -1f;
        private Vector3 lastAnimPosition;
        private bool hasLastAnimPosition;
        private MiniVanPlayer awareTarget;
        private float lastAwareTime = -999f;
        private Vector3 idleOrigin;
        private Quaternion idleFacing = Quaternion.identity;
        private Vector3 idleTarget;
        private float nextIdlePickTime;
        private int idleSide = 1;
        private bool hasIdleOrigin;
        private float holyCrossRepelStartedAt = -1f;
        private float holyCrossHesitateSeconds = 2.5f;
        private Vector3 holyCrossFidgetCenter;
        private float holyCrossFidgetAngle;
        private CapsuleCollider stakeHurtbox;
        private bool isDying;
        private bool deathSequenceStarted;
        private readonly NetworkVariable<bool> networkShieldSuppressed = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private bool shieldVisualSuppressed;
        private bool shieldSlotsCached;
        private AuraMaterialSlot[] auraMaterialSlots;
        private float shieldDamageResidue;
        private float ramStunUntil;
        private float ramSlideUntil;
        private Vector3 ramSlideVelocity;
        private int ramSlideSideSign;
        private bool ramStunDisabledController;
        private readonly RaycastHit[] ramStunCastHits = new RaycastHit[16];
        private const float VehicleRamStunSeconds = 2f;
        private const float VehicleRamSlideSeconds = 0.75f;
        private const float VehicleRamEjectMeters = 1.3f;
        private const float VehicleRamSlideMeters = 2.0f;
        private const float RamStunWallSkin = 0.12f;
        private const float VehicleRamAwayWeight = 0.18f;
        private const float VehicleRamSideWeight = 0.85f;
        private const float VehicleRamForwardWeight = 1f;
        private const float MinivanContactDistance = 0.7f;
        private const float VehicleRamCenterlineMeters = 0.45f;

        private struct AuraMaterialSlot
        {
            public Renderer Renderer;
            public Material[] WithAura;
            public Material[] WithoutAura;
        }

        public Form CurrentForm =>
            UsesOfflineForm() ? offlineForm : (Form)networkForm.Value;

        public override string EnemyDisplayName => "Vampire";

        public override int CurrentHealth =>
            UsesOfflineForm()
                ? Mathf.Max(0, MaxHealth - offlineHitsTaken)
                : health.Value;

        public override float CurrentHealthPrecise =>
            Mathf.Clamp(CurrentHealth - shieldDamageResidue, 0f, Mathf.Max(1, MaxHealth));

        /// <summary>
        /// True while the aura shield is up (no holy cross currently suppressing it).
        /// </summary>
        public bool IsShieldActive =>
            !isDying &&
            !deathSequenceStarted &&
            !(IsSpawned ? networkShieldSuppressed.Value : shieldVisualSuppressed);

        public bool IsRamStunned => Time.time < ramStunUntil;

        /// <summary>
        /// Called by the spawner so Netcode / CharacterController cannot yank the vampire
        /// away from Vampire_Spawner on the first frames.
        /// </summary>
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
            ExtraSpeedKph = -2.4f;
            OpenPanelkaDoorsWhenChasing = true;
            EnsureVampireCombatStats();
            EnsureVampireVisuals();
            ApplyFormVisual(CurrentForm, true);
            if (HasAiAuthority())
            {
                SetForm(Form.Bat, true);
            }

            if (hasSpawnPose)
            {
                transform.SetPositionAndRotation(spawnPosePosition, spawnPoseRotation);
            }
        }

        private void LateUpdate()
        {
            EnsureVampireVisuals();
            EnsureStakeHurtbox();

            if (!isDying && !deathSequenceStarted)
            {
                UpdateHolyCrossShieldState();
            }

            if (isDying || deathSequenceStarted)
            {
                return;
            }

            // Hold the spawner pose until Netcode settles; bat billboard still animates.
            if (hasSpawnPose && Time.time < spawnPoseLockUntil)
            {
                if (controller != null && controller.enabled)
                {
                    controller.enabled = false;
                }

                transform.SetPositionAndRotation(spawnPosePosition, spawnPoseRotation);
                if (CurrentForm == Form.Bat)
                {
                    AnimateBatBillboard();
                }

                return;
            }

            if (IsRamStunned)
            {
                ApplyVehicleRamStunMotion();
                if (CurrentForm == Form.Bat)
                {
                    AnimateBatBillboard();
                }
                else
                {
                    UpdateHumanLimbAnimation(false);
                }

                return;
            }

            RestoreRamStunControllerIfNeeded();

            bool ramKnockback = ApplyKnockbackMotion(Time.deltaTime);
            if (!ramKnockback && HandleMinivanContact())
            {
                return;
            }

            // Clients still flap/billboard / limb-animate while network pose catches up.
            if (!HasAiAuthority())
            {
                if (CurrentForm == Form.Bat)
                {
                    AnimateBatBillboard();
                }
                else
                {
                    UpdateHumanLimbAnimation(IsMovingForWalkAnim());
                }

                return;
            }

            if (ramKnockback)
            {
                if (CurrentForm == Form.Bat)
                {
                    AnimateBatBillboard();
                }
                else
                {
                    UpdateHumanLimbAnimation(false);
                }

                return;
            }

            // Drive bat every frame. Zombie ServerUpdate is throttled and IsServer is false
            // when NetworkObject.Spawn failed — both left the bat frozen as a black quad.
            MiniVanPlayer target = FindAwarePlayer(out float distance);
            if (target == null)
            {
                TickIdleWander();
                return;
            }

            if (CurrentForm == Form.Bat)
            {
                UpdateBatForm(target, distance);
                return;
            }

            if (distance >= TransformToBatDistance)
            {
                SetForm(Form.Bat, false);
                UpdateBatForm(target, distance);
                return;
            }

            // Human chase / van bash stay on the same awareness path (not zombie FindTarget/LOS).
            if (controller != null && !controller.enabled)
            {
                controller.enabled = true;
            }

            if (TryHandleVehicleOccupantTarget(target))
            {
                UpdateHumanLimbAnimation(true);
                return;
            }

            if (TryApplyHolyCrossKeepAway(target))
            {
                UpdateHumanLimbAnimation(true);
                return;
            }

            bool walking = false;
            Vector3 flat = Vector3.ProjectOnPlane(
                target.transform.position - transform.position,
                Vector3.up);
            float planarDistance = flat.magnitude;
            if (flat.sqrMagnitude > 0.001f)
            {
                Vector3 dir = flat.normalized;
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    Quaternion.LookRotation(dir, Vector3.up),
                    1f - Mathf.Exp(-8f * Time.deltaTime));
                if (planarDistance > AttackStopDistance && controller != null && controller.enabled)
                {
                    float speed = Mathf.Max(0.5f, target.WalkSpeed + ExtraSpeedKph / 3.6f) *
                                  GetMovementRecoverScale();
                    Vector3 motion = dir * speed;
                    motion.y = -2f;
                    controller.Move(motion * Time.deltaTime);
                    walking = true;
                }
            }

            if (planarDistance <= AttackRange &&
                Time.time >= nextOfflineMeleeTime &&
                !target.DoesHolyCrossBlockPlayerMelee(this))
            {
                nextOfflineMeleeTime = Time.time + AttackInterval;
                OnMeleeAttack();
                target.ReceiveZombieDamageServer(DamagePerHit);
            }

            UpdateHumanLimbAnimation(walking);
        }

        /// <summary>
        /// True when this machine should run vampire AI: offline play, or Netcode server
        /// (including instances that failed to Spawn and therefore report IsServer == false).
        /// </summary>
        private static bool HasAiAuthority()
        {
            NetworkManager network = NetworkManager.Singleton;
            if (network == null || !network.IsListening)
            {
                return true;
            }

            return network.IsServer;
        }

        private bool UsesOfflineForm()
        {
            NetworkManager network = NetworkManager.Singleton;
            if (network == null || !network.IsListening)
            {
                return true;
            }

            // Unspawned NetworkObjects cannot write NetworkVariables.
            return !IsSpawned;
        }

        private bool IsNetworkServerSpawned()
        {
            NetworkManager network = NetworkManager.Singleton;
            return network != null && network.IsListening && IsSpawned && network.IsServer;
        }

        public override void OnNetworkSpawn()
        {
            ExtraSpeedKph = -2.4f;
            OpenPanelkaDoorsWhenChasing = true;
            EnsureVampireCombatStats();
            base.OnNetworkSpawn();
            EnsureVampireVisuals();
            ApplyFormVisual(CurrentForm, true);
            networkForm.OnValueChanged += OnFormChanged;
            networkShieldSuppressed.OnValueChanged += OnShieldSuppressedChanged;
            ApplyShieldSuppressedVisual(networkShieldSuppressed.Value, playBreakFx: false);
            if (HasAiAuthority())
            {
                SetForm(Form.Bat, true);
            }

            if (hasSpawnPose && HasAiAuthority())
            {
                if (controller != null)
                {
                    controller.enabled = false;
                }

                transform.SetPositionAndRotation(spawnPosePosition, spawnPoseRotation);
                spawnPoseLockUntil = Mathf.Max(spawnPoseLockUntil, Time.time + 0.5f);
            }
        }

        public override void OnNetworkDespawn()
        {
            networkForm.OnValueChanged -= OnFormChanged;
            networkShieldSuppressed.OnValueChanged -= OnShieldSuppressedChanged;
            base.OnNetworkDespawn();
        }

        private void OnFormChanged(byte previous, byte current)
        {
            ApplyFormVisual((Form)current, false);
        }

        protected override void EnsureVisual()
        {
            EnsureVampireVisuals();
        }

        protected override bool TryOverrideServerUpdate()
        {
            // Full vampire AI (awareness, bat, human, van) runs in LateUpdate.
            // Zombie FindTarget/LOS would ignore DetectionRange settings and drop pursuits.
            return true;
        }

        public override void TakeBatHit(
            int damage,
            Vector3 hitOrigin,
            float knockbackDistance,
            float knockbackSeconds,
            bool fromAspenStake = false)
        {
            if (isDying || deathSequenceStarted || deathPartsSpawned)
            {
                return;
            }

            int applied = ResolveVampireMeleeDamage(fromAspenStake);

            if (UsesOfflineForm())
            {
                BeginHitKnockback(hitOrigin, knockbackDistance, knockbackSeconds);
                TriggerHitFlash();
                OnDamagedByBat(Mathf.Max(1, applied));
                offlineHitsTaken += Mathf.Max(0, applied);
                if (offlineHitsTaken >= Mathf.Max(1, MaxHealth))
                {
                    BeginVampireDeath(hitOrigin, knockbackVelocity);
                }

                return;
            }

            if (!IsServer || health.Value <= 0)
            {
                return;
            }

            BeginHitKnockback(hitOrigin, knockbackDistance, knockbackSeconds);
            TriggerHitFlash();
            OnDamagedByBat(Mathf.Max(1, applied));
            if (applied > 0)
            {
                health.Value = Mathf.Max(0, health.Value - applied);
            }

            if (health.Value <= 0)
            {
                BeginVampireDeath(hitOrigin, knockbackVelocity);
            }
        }

        /// <summary>
        /// Minivan ram: always knocks the vampire back. Damage is a fraction of MaxHealth
        /// (0 / 0.10 / 0.20 / 0.50) based on van speed. Does not gib like a zombie roadkill.
        /// </summary>
        public bool ServerTakeVehicleRam(
            Vector3 hitOrigin,
            Vector3 impactVelocity,
            float damageFraction,
            MiniVanVehicle vehicle = null)
        {
            if (isDying || deathSequenceStarted || deathPartsSpawned)
            {
                return false;
            }

            Vector3 launchDirection = ResolveVehicleRamLaunchDirection(hitOrigin, impactVelocity, vehicle);
            BeginVehicleRamStun(launchDirection);
            TriggerHitFlash();

            int applied = 0;
            if (damageFraction > 0.001f)
            {
                applied = ConsumeDamageResidue(Mathf.Max(1, MaxHealth) * Mathf.Clamp01(damageFraction));
            }

            if (UsesOfflineForm())
            {
                offlineHitsTaken += Mathf.Max(0, applied);
                MiniVanEnemyCombatHud.Show(this);
                if (offlineHitsTaken >= Mathf.Max(1, MaxHealth))
                {
                    BeginVampireDeath(hitOrigin, knockbackVelocity.sqrMagnitude > 0.01f
                        ? knockbackVelocity
                        : impactVelocity);
                }

                return true;
            }

            if (!IsServer || health.Value <= 0)
            {
                return false;
            }

            if (applied > 0)
            {
                health.Value = Mathf.Max(0, health.Value - applied);
            }

            MiniVanEnemyCombatHud.Show(this);

            if (health.Value <= 0)
            {
                BeginVampireDeath(hitOrigin, knockbackVelocity.sqrMagnitude > 0.01f
                    ? knockbackVelocity
                    : impactVelocity);
            }

            return true;
        }

        private void EnsureVampireCombatStats()
        {
            // Exposed bat = N hits to kill; stake = 1 hit. Keep MaxHealth divisible by bat hits.
            int batHits = Mathf.Max(1, BatHitsToKillWhenExposed);
            if (MaxHealth < batHits || MaxHealth % batHits != 0)
            {
                MaxHealth = batHits;
            }
        }

        private int ResolveVampireMeleeDamage(bool fromAspenStake)
        {
            int maxHp = Mathf.Max(1, MaxHealth);
            int exposedDamage = fromAspenStake
                ? maxHp
                : Mathf.Max(1, maxHp / Mathf.Max(1, BatHitsToKillWhenExposed));

            float scaled = exposedDamage;
            if (IsShieldActive)
            {
                scaled *= Mathf.Clamp(ShieldDamageTakenScale, 0.01f, 1f);
            }

            return ConsumeDamageResidue(scaled);
        }

        private int ConsumeDamageResidue(float amount)
        {
            if (amount <= 0f)
            {
                return 0;
            }

            shieldDamageResidue += amount;
            int whole = Mathf.FloorToInt(shieldDamageResidue);
            shieldDamageResidue -= whole;
            return Mathf.Max(0, whole);
        }

        protected override void ServerSpawnDeathParts(Vector3 impulse)
        {
            // Vampire death uses a fall-then-dissolve sequence instead of instant gibs.
            BeginVampireDeath(transform.position - transform.forward, impulse);
        }

        private void BeginVampireDeath(Vector3 hitOrigin, Vector3 impulse)
        {
            if (deathSequenceStarted || isDying)
            {
                return;
            }

            deathSequenceStarted = true;
            isDying = true;
            deathPartsSpawned = true;

            int seed = Random.Range(int.MinValue, int.MaxValue);
            if (IsSpawned && IsServer)
            {
                VampireDeathSequenceClientRpc(hitOrigin, impulse, seed);
            }

            StartCoroutine(VampireDeathSequenceRoutine(hitOrigin, impulse, seed, true));
        }

        [ClientRpc]
        private void VampireDeathSequenceClientRpc(Vector3 hitOrigin, Vector3 impulse, int seed)
        {
            if (IsServer)
            {
                return;
            }

            isDying = true;
            deathSequenceStarted = true;
            deathPartsSpawned = true;
            StartCoroutine(VampireDeathSequenceRoutine(hitOrigin, impulse, seed, false));
        }

        private IEnumerator VampireDeathSequenceRoutine(
            Vector3 hitOrigin,
            Vector3 impulse,
            int seed,
            bool isAuthority)
        {
            // Prefer a human body for the lie-down; bat billboard looks wrong on its side.
            if (HasAiAuthority() || UsesOfflineForm())
            {
                SetForm(Form.Human, true);
            }
            else
            {
                ApplyFormVisual(Form.Human, true);
            }

            if (controller != null)
            {
                controller.enabled = false;
            }

            if (stakeHurtbox != null)
            {
                stakeHurtbox.enabled = false;
            }

            // Tip onto the side with a pure local roll — no LookRotation (that caused yaw spin).
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

            // Root is at the feet; when rolled on the side, raise it so the torso doesn't clip.
            Vector3 endPosition = new Vector3(
                startPosition.x,
                groundY + Mathf.Max(0.05f, DeathLieGroundHeight),
                startPosition.z);

            float fallSeconds = Mathf.Max(0.1f, DeathFallSeconds);
            float elapsed = 0f;
            while (elapsed < fallSeconds)
            {
                elapsed += Time.deltaTime;
                // Ease-in: starts quick tip, settles (reads as a collapse, not a slow spin).
                float u = Mathf.Clamp01(elapsed / fallSeconds);
                float t = u * u;
                transform.rotation = Quaternion.Slerp(startRotation, endRotation, t);
                transform.position = Vector3.Lerp(startPosition, endPosition, t);
                yield return null;
            }

            transform.SetPositionAndRotation(endPosition, endRotation);

            // Burst as soon as the body finishes tipping — don't wait for the lie pause.
            Vector3 fxPosition = GetDeathBodyCenter();
            SpawnVampireDeathFx(fxPosition, impulse, seed);

            if (humanVisual != null)
            {
                humanVisual.gameObject.SetActive(false);
            }

            if (batVisual != null)
            {
                batVisual.gameObject.SetActive(false);
            }

            if (DeathLieSeconds > 0.001f)
            {
                yield return new WaitForSeconds(DeathLieSeconds);
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

        protected override void OnDamagedByBat(int damage)
        {
            consecutiveHits++;
            if (consecutiveHits >= Mathf.Max(1, HitsBeforeStagger))
            {
                consecutiveHits = 0;
                float minSeconds = Mathf.Max(0.1f, StaggerSecondsMin);
                float maxSeconds = Mathf.Max(minSeconds, StaggerSecondsMax);
                staggerUntil = Time.time + Random.Range(minSeconds, maxSeconds);
            }
        }

        protected override float GetMovementRecoverScale()
        {
            return base.GetMovementRecoverScale() * GetStaggerSpeedScale();
        }

        private float GetStaggerSpeedScale()
        {
            return Time.time < staggerUntil
                ? Mathf.Clamp(StaggerSpeedScale, 0.05f, 1f)
                : 1f;
        }

        private float GetBatFlySpeedMps()
        {
            return BatFlySpeedMps * GetStaggerSpeedScale();
        }

        private void UpdateHolyCrossShieldState()
        {
            if (!HasAiAuthority())
            {
                return;
            }

            bool underCross = IsUnderAnyHolyCross();
            if (IsNetworkServerSpawned())
            {
                if (networkShieldSuppressed.Value != underCross)
                {
                    networkShieldSuppressed.Value = underCross;
                }

                return;
            }

            // Offline / unspawned local authority.
            if (underCross != shieldVisualSuppressed)
            {
                ApplyShieldSuppressedVisual(underCross, playBreakFx: underCross);
            }
        }

        private void OnShieldSuppressedChanged(bool previous, bool current)
        {
            ApplyShieldSuppressedVisual(current, playBreakFx: current && !previous);
        }

        private bool IsUnderAnyHolyCross()
        {
            MiniVanPlayer[] players = FindObjectsByType<MiniVanPlayer>(FindObjectsSortMode.None);
            for (int i = 0; i < players.Length; i++)
            {
                MiniVanPlayer player = players[i];
                if (player == null || player.IsDowned)
                {
                    continue;
                }

                if (player.IsHolyCrossRepelling &&
                    player.IsWorldPointInHolyCrossCone(transform.position))
                {
                    return true;
                }
            }

            return false;
        }

        private void ApplyShieldSuppressedVisual(bool suppressed, bool playBreakFx)
        {
            CacheAuraMaterialSlots();
            if (auraMaterialSlots == null || auraMaterialSlots.Length == 0)
            {
                shieldVisualSuppressed = suppressed;
                return;
            }

            for (int i = 0; i < auraMaterialSlots.Length; i++)
            {
                AuraMaterialSlot slot = auraMaterialSlots[i];
                if (slot.Renderer == null)
                {
                    continue;
                }

                slot.Renderer.sharedMaterials = suppressed ? slot.WithoutAura : slot.WithAura;
            }

            bool wasSuppressed = shieldVisualSuppressed;
            shieldVisualSuppressed = suppressed;
            if (playBreakFx && suppressed && !wasSuppressed)
            {
                PlayShieldBreakParticles();
            }
        }

        private void CacheAuraMaterialSlots()
        {
            if (shieldSlotsCached)
            {
                return;
            }

            if (humanVisual == null)
            {
                return;
            }

            Renderer[] renderers = humanVisual.GetComponentsInChildren<Renderer>(true);
            System.Collections.Generic.List<AuraMaterialSlot> slots =
                new System.Collections.Generic.List<AuraMaterialSlot>(renderers.Length);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                Material[] mats = renderer.sharedMaterials;
                if (mats == null || mats.Length == 0)
                {
                    continue;
                }

                int auraCount = 0;
                for (int m = 0; m < mats.Length; m++)
                {
                    if (IsShieldAuraMaterial(mats[m]))
                    {
                        auraCount++;
                    }
                }

                if (auraCount <= 0)
                {
                    continue;
                }

                Material[] without = new Material[mats.Length - auraCount];
                int write = 0;
                for (int m = 0; m < mats.Length; m++)
                {
                    if (!IsShieldAuraMaterial(mats[m]))
                    {
                        without[write++] = mats[m];
                    }
                }

                // Snapshot the with-aura set once, before any suppress, so restore is stable.
                Material[] with = new Material[mats.Length];
                for (int m = 0; m < mats.Length; m++)
                {
                    with[m] = mats[m];
                }

                slots.Add(new AuraMaterialSlot
                {
                    Renderer = renderer,
                    WithAura = with,
                    WithoutAura = without
                });
            }

            if (slots.Count == 0 && !visualsReady)
            {
                return;
            }

            auraMaterialSlots = slots.ToArray();
            shieldSlotsCached = true;
        }

        private bool IsShieldAuraMaterial(Material material)
        {
            if (material == null)
            {
                return false;
            }

            if (ShieldAuraMaterial != null)
            {
                return material == ShieldAuraMaterial ||
                       material.name == ShieldAuraMaterial.name ||
                       material.name == ShieldAuraMaterial.name + " (Instance)";
            }

            string name = material.name;
            return name == "Vamp_aura" || name.StartsWith("Vamp_aura");
        }

        private void PlayShieldBreakParticles()
        {
            Vector3 position = GetDeathBodyCenter();
            if (ShieldBreakParticlePrefabs == null || ShieldBreakParticlePrefabs.Length == 0)
            {
                return;
            }

            for (int i = 0; i < ShieldBreakParticlePrefabs.Length; i++)
            {
                GameObject prefab = ShieldBreakParticlePrefabs[i];
                if (prefab == null)
                {
                    continue;
                }

                GameObject fx = Instantiate(prefab, position, Quaternion.identity);
                fx.name = prefab.name + "_Break";
                fx.transform.SetParent(null, true);

                float destroyAfter = 2f;
                ParticleSystem[] systems = fx.GetComponentsInChildren<ParticleSystem>(true);
                for (int s = 0; s < systems.Length; s++)
                {
                    ParticleSystem system = systems[s];
                    if (system == null)
                    {
                        continue;
                    }

                    var main = system.main;
                    main.playOnAwake = false;
                    main.loop = false;
                    system.Clear(true);
                    system.Play(true);
                    destroyAfter = Mathf.Max(
                        destroyAfter,
                        main.duration + main.startLifetime.constantMax + 0.5f);
                }

                Destroy(fx, destroyAfter);
            }
        }

        /// <summary>
        /// While the player holds the holy cross, stay outside keep-distance inside the forward
        /// cone. First 2–3s: only back off / fidget in place. After that: may orbit around.
        /// </summary>
        private bool TryApplyHolyCrossKeepAway(MiniVanPlayer target)
        {
            if (!TryBeginHolyCrossRepel(target, out Vector3 away, out float keep, out float dist, out bool hesitating))
            {
                return false;
            }

            float speed = Mathf.Max(0.5f, target.WalkSpeed + ExtraSpeedKph / 3.6f) * GetMovementRecoverScale();
            Vector3 moveDir = ResolveHolyCrossMoveDir(target, away, keep, dist, hesitating);
            if (moveDir.sqrMagnitude > 0.0001f)
            {
                if (controller != null && controller.enabled)
                {
                    Vector3 motion = moveDir * speed;
                    motion.y = -2f;
                    controller.Move(motion * Time.deltaTime);
                }
                else
                {
                    transform.position += moveDir * speed * Time.deltaTime;
                }
            }

            Vector3 face = Vector3.ProjectOnPlane(target.transform.position - transform.position, Vector3.up);
            if (face.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    Quaternion.LookRotation(face.normalized, Vector3.up),
                    1f - Mathf.Exp(-8f * Time.deltaTime));
            }

            return true;
        }

        private bool TryApplyHolyCrossKeepAwayBat(MiniVanPlayer target)
        {
            if (!TryBeginHolyCrossRepel(target, out Vector3 away, out float keep, out float dist, out bool hesitating))
            {
                return false;
            }

            float step = GetBatFlySpeedMps() * Time.deltaTime;
            Vector3 moveDir = ResolveHolyCrossMoveDir(target, away, keep, dist, hesitating);
            if (moveDir.sqrMagnitude > 0.0001f)
            {
                transform.position += moveDir * step;
            }

            return true;
        }

        private bool TryBeginHolyCrossRepel(
            MiniVanPlayer target,
            out Vector3 away,
            out float keep,
            out float dist,
            out bool hesitating)
        {
            away = Vector3.forward;
            keep = 5f;
            dist = 0f;
            hesitating = false;

            if (target == null || !target.IsHolyCrossRepelling || !target.IsWorldPointInHolyCrossCone(transform.position))
            {
                holyCrossRepelStartedAt = -1f;
                return false;
            }

            keep = Mathf.Max(0.5f, target.HolyCrossKeepDistance);
            away = Vector3.ProjectOnPlane(transform.position - target.transform.position, Vector3.up);
            if (away.sqrMagnitude < 0.0001f)
            {
                Vector3 fallback = target.PlayerCamera != null
                    ? target.PlayerCamera.transform.forward
                    : target.transform.forward;
                away = -Vector3.ProjectOnPlane(fallback, Vector3.up);
                if (away.sqrMagnitude < 0.0001f)
                {
                    away = Vector3.right;
                }
            }

            away.Normalize();
            dist = Vector3.ProjectOnPlane(transform.position - target.transform.position, Vector3.up).magnitude;

            if (holyCrossRepelStartedAt < 0f)
            {
                holyCrossRepelStartedAt = Time.time;
                holyCrossHesitateSeconds = Random.Range(2f, 3f);
                holyCrossFidgetCenter = transform.position;
                holyCrossFidgetAngle = Random.Range(0f, Mathf.PI * 2f);
            }

            hesitating = Time.time < holyCrossRepelStartedAt + holyCrossHesitateSeconds;
            return true;
        }

        private Vector3 ResolveHolyCrossMoveDir(
            MiniVanPlayer target,
            Vector3 away,
            float keep,
            float dist,
            bool hesitating)
        {
            Vector3 side = Vector3.Cross(Vector3.up, away).normalized;
            if (GetInstanceID() % 2 == 0)
            {
                side = -side;
            }

            // Always push out first if inside the keep distance.
            if (dist < keep - 0.08f)
            {
                holyCrossFidgetCenter = transform.position;
                return away;
            }

            if (hesitating)
            {
                // 2–3 seconds: retreat to the ring, then fidget in a narrow radius — no flank yet.
                if (dist > keep + 0.4f)
                {
                    Vector3 ring = target.transform.position + away * keep;
                    Vector3 toRing = Vector3.ProjectOnPlane(ring - transform.position, Vector3.up);
                    return toRing.sqrMagnitude > 0.0001f ? toRing.normalized : away;
                }

                holyCrossFidgetAngle += Time.deltaTime * 2.6f;
                const float fidgetRadius = 0.32f;
                Vector3 fidgetOffset =
                    (Mathf.Cos(holyCrossFidgetAngle) * away + Mathf.Sin(holyCrossFidgetAngle) * side) * fidgetRadius;
                Vector3 desired = holyCrossFidgetCenter + fidgetOffset;
                // Keep fidget point outside the keep ring.
                Vector3 fromPlayer = Vector3.ProjectOnPlane(desired - target.transform.position, Vector3.up);
                if (fromPlayer.magnitude < keep)
                {
                    desired = target.transform.position + (fromPlayer.sqrMagnitude > 0.0001f
                        ? fromPlayer.normalized
                        : away) * keep;
                }

                Vector3 toFidget = Vector3.ProjectOnPlane(desired - transform.position, Vector3.up);
                return toFidget.sqrMagnitude > 0.0001f ? toFidget.normalized : Vector3.zero;
            }

            // After hesitate: orbit / hold on the keep ring.
            if (dist <= keep + 0.4f)
            {
                return (side + away * Mathf.Clamp((keep - dist) * 3f, -0.5f, 0.5f)).normalized;
            }

            Vector3 approachRing = target.transform.position + away * keep;
            Vector3 toApproach = Vector3.ProjectOnPlane(approachRing - transform.position, Vector3.up);
            return toApproach.sqrMagnitude > 0.0001f ? toApproach.normalized : side;
        }

        protected override void SpawnDeathPartsLocal(Vector3 position, Vector3 impulse, int seed)
        {
            SpawnVampireDeathFx(position, impulse, seed);
        }

        private void UpdateBatForm(MiniVanPlayer target, float distance)
        {
            if (controller != null && controller.enabled)
            {
                controller.enabled = false;
            }

            AnimateBatBillboard();
            if (target == null)
            {
                return;
            }

            if (TryApplyHolyCrossKeepAwayBat(target))
            {
                return;
            }

            Vector3 aim = GetPlayerAimPoint(target);
            Vector3 to = aim - transform.position;
            float sep = to.magnitude;
            if (sep < 0.001f)
            {
                return;
            }

            Vector3 dir = to / sep;

            // Intact glass still hard-blocks. Walls do not — the bat climbs over and keeps chasing.
            if (TryGetBatBlock(transform.position, aim, out Vector3 hitPoint, out bool intactGlass) &&
                intactGlass)
            {
                Vector3 hold = hitPoint - dir * (BatBodyRadius + 0.15f);
                transform.position = Vector3.MoveTowards(
                    transform.position,
                    hold,
                    GetBatFlySpeedMps() * Time.deltaTime);
                return;
            }

            if (sep <= TransformToHumanDistance)
            {
                SnapAboveGround();
                FaceFlatDirection(dir);
                SetForm(Form.Human, false);
                consecutiveHits = 0;
                return;
            }

            float step = GetBatFlySpeedMps() * Time.deltaTime;
            Vector3 next = transform.position + dir * step;
            if (!TryGetBatBlock(transform.position, next + dir * BatBodyRadius, out _, out _))
            {
                transform.position = next;
                return;
            }

            // Blocked by a wall/solid: rise and keep sliding toward the player through the air.
            // Intact glass still refuses the step; ordinary solids are flown over.
            Vector3 flat = Vector3.ProjectOnPlane(dir, Vector3.up);
            if (flat.sqrMagnitude < 0.001f)
            {
                flat = transform.forward;
            }

            flat.Normalize();
            Vector3 climb = transform.position +
                            Vector3.up * (step * 1.15f) +
                            flat * (step * 0.65f);
            if (!(TryGetBatBlock(transform.position, climb, out _, out bool climbGlass) && climbGlass))
            {
                transform.position = climb;
                return;
            }

            Vector3 side = Vector3.Cross(Vector3.up, flat);
            Vector3 tryA = transform.position + Vector3.up * (step * 0.9f) + side * step;
            Vector3 tryB = transform.position + Vector3.up * (step * 0.9f) - side * step;
            if (!(TryGetBatBlock(transform.position, tryA, out _, out bool glassA) && glassA))
            {
                transform.position = tryA;
            }
            else if (!(TryGetBatBlock(transform.position, tryB, out _, out bool glassB) && glassB))
            {
                transform.position = tryB;
            }
            else
            {
                // Last resort: pure ascent so the bat never freezes against a façade.
                transform.position += Vector3.up * step;
            }
        }

        private void SetForm(Form form, bool force)
        {
            if (!force && CurrentForm == form)
            {
                return;
            }

            if (UsesOfflineForm())
            {
                offlineForm = form;
            }
            else if (HasAiAuthority())
            {
                networkForm.Value = (byte)form;
            }

            ApplyFormVisual(form, force);
        }

        private void ApplyFormVisual(Form form, bool force)
        {
            EnsureVampireVisuals();
            bool bat = form == Form.Bat;
            if (humanVisual != null && (force || humanVisual.gameObject.activeSelf == bat))
            {
                humanVisual.gameObject.SetActive(!bat);
            }

            if (batVisual != null && (force || batVisual.gameObject.activeSelf != bat))
            {
                batVisual.gameObject.SetActive(bat);
            }

            if (controller != null)
            {
                controller.enabled = !bat && !IsRamStunned;
            }

            if (!bat)
            {
                SnapAboveGround();
                limbPivotsReady = false;
                EnsureLimbPivots();
            }

            EnsureStakeHurtbox();
            CacheVisualRenderers();
        }

        private void BeginVehicleRamStun(Vector3 launchDirection)
        {
            if (DebugVampireRam)
            {
                Debug.Log("[MiniVanVampireRam] stun " + VehicleRamStunSeconds.ToString("0.0") +
                          "s launch=" + launchDirection.ToString("F2") + " side=" + ramSlideSideSign);
            }

            ramStunUntil = Time.time + VehicleRamStunSeconds;
            ramSlideUntil = Time.time + VehicleRamSlideSeconds;
            ramSlideVelocity = launchDirection *
                               (VehicleRamSlideMeters / Mathf.Max(0.05f, VehicleRamSlideSeconds));
            knockbackVelocity = Vector3.zero;
            knockbackUntil = 0f;

            if (controller != null && controller.enabled)
            {
                ramStunDisabledController = true;
                controller.enabled = false;
            }

            MoveRamStunWithoutClipping(launchDirection * VehicleRamEjectMeters);
            SnapRamStunToGround();

            if (IsSpawned && IsServer)
            {
                VampireRamStunClientRpc(launchDirection, ramSlideSideSign);
            }
        }

        [ClientRpc]
        private void VampireRamStunClientRpc(Vector3 launchDirection, int sideSign)
        {
            if (IsServer)
            {
                return;
            }

            ramSlideSideSign = sideSign;
            ramStunUntil = Time.time + VehicleRamStunSeconds;
            ramSlideUntil = Time.time + VehicleRamSlideSeconds;
            ramSlideVelocity = launchDirection *
                               (VehicleRamSlideMeters / Mathf.Max(0.05f, VehicleRamSlideSeconds));
            knockbackVelocity = Vector3.zero;
            knockbackUntil = 0f;
            if (controller != null && controller.enabled)
            {
                ramStunDisabledController = true;
                controller.enabled = false;
            }
        }

        private void ApplyVehicleRamStunMotion()
        {
            if (controller != null && controller.enabled)
            {
                ramStunDisabledController = true;
                controller.enabled = false;
            }

            if (Time.time < ramSlideUntil && ramSlideVelocity.sqrMagnitude > 0.001f)
            {
                MoveRamStunWithoutClipping(ramSlideVelocity * Time.deltaTime);
            }
            else
            {
                ramSlideVelocity = Vector3.zero;
            }

            SnapRamStunToGround();
        }

        /// <summary>
        /// The CharacterController is off during the stun so the van can drive through, which also
        /// removes wall collision. Sweep the capsule manually and stop the slide at the first hit.
        /// </summary>
        private void MoveRamStunWithoutClipping(Vector3 delta)
        {
            float distance = delta.magnitude;
            if (distance < 0.0001f)
            {
                return;
            }

            Vector3 direction = delta / distance;
            float radius = controller != null ? Mathf.Max(0.05f, controller.radius) : 0.4f;
            float height = controller != null ? Mathf.Max(radius * 2f, controller.height) : 1.9f;
            Vector3 center = controller != null
                ? transform.TransformPoint(controller.center)
                : transform.position + Vector3.up * (height * 0.5f);
            float half = Mathf.Max(0f, height * 0.5f - radius);

            int hitCount = Physics.CapsuleCastNonAlloc(
                center - Vector3.up * half,
                center + Vector3.up * half,
                radius,
                direction,
                ramStunCastHits,
                distance,
                ~0,
                QueryTriggerInteraction.Ignore);

            float allowed = distance;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = ramStunCastHits[i];
                Collider collider = hit.collider;
                if (collider == null ||
                    collider.transform.IsChildOf(transform) ||
                    hit.distance <= 0.0001f ||
                    collider.GetComponentInParent<MiniVanVehicle>() != null ||
                    collider.GetComponentInParent<MiniVanZombie>() != null ||
                    collider.GetComponentInParent<MiniVanPlayer>() != null)
                {
                    continue;
                }

                allowed = Mathf.Min(allowed, Mathf.Max(0f, hit.distance - RamStunWallSkin));
            }

            if (allowed < distance)
            {
                ramSlideVelocity = Vector3.zero;
                ramSlideUntil = 0f;
            }

            transform.position += direction * allowed;
        }

        private void RestoreRamStunControllerIfNeeded()
        {
            if (IsRamStunned || !ramStunDisabledController)
            {
                return;
            }

            ramStunDisabledController = false;
            ramSlideSideSign = 0;
            ramSlideVelocity = Vector3.zero;
            if (CurrentForm == Form.Human && controller != null && !isDying && !deathSequenceStarted)
            {
                controller.enabled = true;
            }
        }

        private void SnapRamStunToGround()
        {
            Vector3 origin = transform.position + Vector3.up * 1.5f;
            if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 5f, ~0, QueryTriggerInteraction.Ignore))
            {
                return;
            }

            if (hit.collider != null && hit.collider.GetComponentInParent<MiniVanVehicle>() != null)
            {
                return;
            }

            transform.position = new Vector3(transform.position.x, hit.point.y, transform.position.z);
        }

        private Vector3 ResolveVehicleRamLaunchDirection(
            Vector3 hitOrigin,
            Vector3 impactVelocity,
            MiniVanVehicle vehicle)
        {
            Vector3 away = Vector3.ProjectOnPlane(transform.position - hitOrigin, Vector3.up);
            if (away.sqrMagnitude < 0.001f && vehicle != null)
            {
                away = Vector3.ProjectOnPlane(transform.position - vehicle.transform.position, Vector3.up);
            }

            if (away.sqrMagnitude < 0.001f)
            {
                away = -transform.forward;
            }

            away.Normalize();

            Vector3 travel = Vector3.ProjectOnPlane(impactVelocity, Vector3.up);
            if (travel.sqrMagnitude < 0.01f && vehicle != null)
            {
                travel = Vector3.ProjectOnPlane(vehicle.transform.forward, Vector3.up);
            }

            Vector3 sideAxis = Vector3.Cross(Vector3.up, travel);
            if (sideAxis.sqrMagnitude < 0.001f)
            {
                sideAxis = vehicle != null
                    ? Vector3.ProjectOnPlane(vehicle.transform.right, Vector3.up)
                    : Vector3.Cross(Vector3.up, away);
            }

            if (sideAxis.sqrMagnitude < 0.001f)
            {
                sideAxis = Vector3.Cross(Vector3.up, away);
            }

            sideAxis.Normalize();

            Vector3 fromVan = vehicle != null
                ? Vector3.ProjectOnPlane(transform.position - vehicle.transform.position, Vector3.up)
                : away;
            float sideOffset = Vector3.Dot(fromVan, sideAxis);
            int sideSign;
            if (Mathf.Abs(sideOffset) >= VehicleRamCenterlineMeters)
            {
                sideSign = sideOffset > 0f ? 1 : -1;
            }
            else if (ramSlideSideSign != 0)
            {
                sideSign = ramSlideSideSign;
            }
            else
            {
                sideSign = Random.value < 0.5f ? 1 : -1;
            }

            ramSlideSideSign = sideSign;

            // Push along the van's travel too: a pure sideways launch left the frame instantly and
            // read as the vampire vanishing instead of being thrown.
            Vector3 forward = travel.sqrMagnitude > 0.0001f
                ? travel.normalized
                : (vehicle != null
                    ? Vector3.ProjectOnPlane(vehicle.transform.forward, Vector3.up).normalized
                    : away);

            Vector3 launch = forward * VehicleRamForwardWeight +
                             sideAxis * (sideSign * VehicleRamSideWeight) +
                             away * VehicleRamAwayWeight;
            if (launch.sqrMagnitude < 0.001f)
            {
                return sideAxis * sideSign;
            }

            return launch.normalized;
        }

        /// <summary>
        /// Ram detection lives here because the van sweep only runs on a Netcode server, while the
        /// vampire also drives itself offline. The body stays solid, so contact is measured by
        /// distance to the hull instead of an overlap test. True when the ram stun took over.
        /// </summary>
        private bool HandleMinivanContact()
        {
            if (isDying || deathSequenceStarted || !HasAiAuthority() ||
                controller == null || !controller.enabled)
            {
                return false;
            }

            MiniVanVehicle[] vehicles = MiniVanSceneScan.Get<MiniVanVehicle>();
            Vector3 chest = transform.position + Vector3.up * 0.9f;
            for (int i = 0; i < vehicles.Length; i++)
            {
                MiniVanVehicle vehicle = vehicles[i];
                if (vehicle == null || !vehicle.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (vehicle.GetDistanceFromVehicleBody(chest) > MinivanContactDistance)
                {
                    continue;
                }

                if (vehicle.TryRamVampireFromContact(this, vehicle.GetClosestVehicleBodyPoint(chest)))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// World point used by stake/bat proximity hit tests (bat flies higher than human hips).
        /// </summary>
        public Vector3 GetStakeHitPoint()
        {
            if (CurrentForm == Form.Bat)
            {
                return transform.position;
            }

            return transform.position + Vector3.up * 1.0f;
        }

        /// <summary>
        /// Always-on trigger hurtbox so stake SphereCast can hit bat form when CharacterController is off.
        /// </summary>
        private void EnsureStakeHurtbox()
        {
            if (stakeHurtbox == null)
            {
                stakeHurtbox = GetComponent<CapsuleCollider>();
                if (stakeHurtbox == null)
                {
                    stakeHurtbox = gameObject.AddComponent<CapsuleCollider>();
                }
            }

            stakeHurtbox.isTrigger = true;
            stakeHurtbox.direction = 1; // Y-axis
            if (CurrentForm == Form.Bat)
            {
                stakeHurtbox.center = Vector3.zero;
                stakeHurtbox.radius = 0.45f;
                stakeHurtbox.height = 0.7f;
            }
            else
            {
                stakeHurtbox.center = new Vector3(0f, 0.95f, 0f);
                stakeHurtbox.radius = 0.42f;
                stakeHurtbox.height = 1.9f;
            }

            stakeHurtbox.enabled = true;
        }

        private void EnsureVampireVisuals()
        {
            if (visualsReady && humanVisual != null && batVisual != null)
            {
                ApplyHumanMaterialsIfNeeded();
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

            humanVisual = transform.Find("Vampire Human Visual");
            if (humanVisual == null && HumanVisualPrefab != null)
            {
                GameObject human = Instantiate(HumanVisualPrefab, transform);
                human.name = "Vampire Human Visual";
                human.transform.localPosition = Vector3.zero;
                // FBX faces the opposite of Unity forward; rotate visual so chase walks face-first.
                human.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                human.transform.localScale = Vector3.one;
                DisableColliders(human);
                humanVisual = human.transform;
            }

            if (humanVisual == null)
            {
                GameObject placeholder = new GameObject("Vampire Human Visual");
                placeholder.transform.SetParent(transform, false);
                humanVisual = placeholder.transform;
            }
            else if (Mathf.Abs(Mathf.DeltaAngle(humanVisual.localEulerAngles.y, 180f)) > 1f)
            {
                humanVisual.localRotation = Quaternion.Euler(0f, 180f, 0f);
            }

            ApplyHumanMaterialsIfNeeded();

            batVisual = transform.Find("Vampire Bat Visual");
            if (batVisual == null)
            {
                GameObject bat = new GameObject("Vampire Bat Visual");
                bat.transform.SetParent(transform, false);
                batSpriteRenderer = bat.AddComponent<SpriteRenderer>();
                batSpriteRenderer.sortingOrder = 20;
                batVisual = bat.transform;
            }
            else
            {
                batSpriteRenderer = batVisual.GetComponent<SpriteRenderer>();
                if (batSpriteRenderer == null)
                {
                    // Upgrade old Quad billboards created by earlier builds.
                    MeshRenderer meshRenderer = batVisual.GetComponent<MeshRenderer>();
                    MeshFilter meshFilter = batVisual.GetComponent<MeshFilter>();
                    if (meshRenderer != null)
                    {
                        if (Application.isPlaying)
                        {
                            Destroy(meshRenderer);
                        }
                        else
                        {
                            DestroyImmediate(meshRenderer);
                        }
                    }

                    if (meshFilter != null)
                    {
                        if (Application.isPlaying)
                        {
                            Destroy(meshFilter);
                        }
                        else
                        {
                            DestroyImmediate(meshFilter);
                        }
                    }

                    batSpriteRenderer = batVisual.gameObject.AddComponent<SpriteRenderer>();
                    batSpriteRenderer.sortingOrder = 20;
                }
            }

            // Sprite sits on the root so bat flight height == eye aim height.
            batVisual.localPosition = Vector3.zero;
            batVisual.localRotation = Quaternion.identity;
            ApplyBatVisualScale();

            if (batMaterial == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader == null)
                {
                    shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                }

                if (shader == null)
                {
                    shader = Shader.Find("Unlit/Transparent");
                }

                batMaterial = new Material(shader != null ? shader : Shader.Find("Hidden/InternalErrorShader"));
                batMaterial.name = "Vampire_Bat_Billboard";
                batMaterial.color = Color.white;
                batMaterial.renderQueue = 3000;
            }

            if (batSpriteRenderer != null)
            {
                batSpriteRenderer.sharedMaterial = batMaterial;
                batSpriteRenderer.color = Color.white;
            }

            RebuildBatSprites();
            ApplyBatFrame(false);
            visualsReady = true;
            limbPivotsReady = false;
            EnsureLimbPivots();
            leftArm = armPivotL;
            rightArm = armPivotR;
            CacheVisualRenderers();
        }

        protected override void AnimateArms()
        {
            if (CurrentForm != Form.Human)
            {
                return;
            }

            // Smooth limb animation is driven every LateUpdate; keep this no-op for zombies' 15Hz path
            // unless LateUpdate did not run human anim this frame.
            if (!HasAiAuthority())
            {
                UpdateHumanLimbAnimation(IsMovingForWalkAnim());
            }
        }

        protected override void OnMeleeAttack()
        {
            BeginMeleeAttackAnimation();
            if (IsSpawned && IsServer)
            {
                MeleeAttackAnimClientRpc();
            }
        }

        [ClientRpc]
        private void MeleeAttackAnimClientRpc()
        {
            BeginMeleeAttackAnimation();
        }

        private void BeginMeleeAttackAnimation()
        {
            attackAnimTime = 0f;
            EnsureLimbPivots();
        }

        private void EnsureLimbPivots()
        {
            if (limbPivotsReady || humanVisual == null)
            {
                return;
            }

            armPivotL = BuildLimbPivot(
                humanVisual,
                "ArmPivot_L",
                new Vector3(0.45f, 1.42f, -0.03f),
                "Arm_L",
                "Sleeve_L");
            armPivotR = BuildLimbPivot(
                humanVisual,
                "ArmPivot_R",
                new Vector3(-0.45f, 1.42f, -0.03f),
                "Arm_R",
                "Sleeve_R");
            legPivotL = BuildLimbPivot(
                humanVisual,
                "LegPivot_L",
                new Vector3(0.175f, 0.72f, 0f),
                "Leg_L",
                "Shoe_L",
                "Patch_L",
                "PatchX1_L",
                "PatchX2_L");
            legPivotR = BuildLimbPivot(
                humanVisual,
                "LegPivot_R",
                new Vector3(-0.175f, 0.72f, 0f),
                "Leg_R",
                "Shoe_R",
                "Patch_R",
                "PatchX1_R",
                "PatchX2_R");

            limbPivotsReady = armPivotL != null && armPivotR != null && legPivotL != null && legPivotR != null;
            leftArm = armPivotL;
            rightArm = armPivotR;
        }

        private static Transform BuildLimbPivot(
            Transform root,
            string pivotName,
            Vector3 localPivot,
            params string[] childNames)
        {
            Transform pivot = root.Find(pivotName);
            if (pivot == null)
            {
                GameObject pivotObject = new GameObject(pivotName);
                pivot = pivotObject.transform;
                pivot.SetParent(root, false);
                pivot.localPosition = localPivot;
                pivot.localRotation = Quaternion.identity;
                pivot.localScale = Vector3.one;
            }

            for (int i = 0; i < childNames.Length; i++)
            {
                Transform child = root.Find(childNames[i]);
                if (child == null)
                {
                    child = FindDeepChild(root, childNames[i]);
                }

                if (child == null || child.parent == pivot)
                {
                    continue;
                }

                child.SetParent(pivot, true);
            }

            return pivot;
        }

        private static Transform FindDeepChild(Transform root, string name)
        {
            if (root.name == name)
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDeepChild(root.GetChild(i), name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private void UpdateHumanLimbAnimation(bool walking)
        {
            if (CurrentForm != Form.Human)
            {
                return;
            }

            EnsureLimbPivots();
            if (!limbPivotsReady)
            {
                return;
            }

            if (attackAnimTime >= 0f)
            {
                attackAnimTime += Time.deltaTime;
                float windup = Mathf.Max(0.05f, AttackWindupSeconds);
                float strike = Mathf.Max(0.05f, AttackStrikeSeconds);
                float recover = Mathf.Max(0.05f, AttackRecoverSeconds);
                // Keep angles in a small range so Euler X never wraps through a full 360 spin.
                float raiseAngle = Mathf.Clamp(AttackRaiseAngle, -110f, -20f);
                float slamAngle = Mathf.Clamp(AttackSlamAngle, 10f, 70f);

                if (attackAnimTime <= windup)
                {
                    float t = Mathf.Clamp01(attackAnimTime / windup);
                    float angle = Mathf.Lerp(0f, raiseAngle, t);
                    SetArmAngles(angle, angle);
                    SetLegAngles(0f, 0f);
                }
                else if (attackAnimTime <= windup + strike)
                {
                    float t = Mathf.Clamp01((attackAnimTime - windup) / strike);
                    // Ease-in for a sharp drop from eye height to the slam.
                    float angle = Mathf.Lerp(raiseAngle, slamAngle, t * t);
                    SetArmAngles(angle, angle);
                    SetLegAngles(0f, 0f);
                }
                else if (attackAnimTime <= windup + strike + recover)
                {
                    float t = Mathf.Clamp01((attackAnimTime - windup - strike) / recover);
                    float angle = Mathf.Lerp(slamAngle, 0f, t);
                    SetArmAngles(angle, angle);
                    SetLegAngles(0f, 0f);
                }
                else
                {
                    attackAnimTime = -1f;
                    SetArmAngles(0f, 0f);
                    SetLegAngles(0f, 0f);
                }

                hasLastAnimPosition = false;
                return;
            }

            if (Time.time < ramStunUntil)
            {
                walkPhase = 0f;
                attackAnimTime = -1f;
                hasLastAnimPosition = false;
                SetArmAngles(0f, 0f);
                SetLegAngles(0f, 0f);
                return;
            }

            bool moving = walking || IsMovingForWalkAnim();
            if (moving)
            {
                walkPhase += Time.deltaTime * WalkAnimSpeed;
                float swing = Mathf.Sin(walkPhase) * WalkSwingDegrees;
                // Minecraft-style opposite arm/leg swing.
                SetArmAngles(swing, -swing);
                SetLegAngles(-swing, swing);
            }
            else
            {
                walkPhase = Mathf.MoveTowards(walkPhase, 0f, Time.deltaTime * WalkAnimSpeed);
                SetArmAngles(0f, 0f);
                SetLegAngles(0f, 0f);
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
            return delta.sqrMagnitude > 0.00005f;
        }

        private void SetArmAngles(float leftX, float rightX)
        {
            if (armPivotL != null)
            {
                armPivotL.localRotation = Quaternion.Euler(leftX, 0f, 0f);
            }

            if (armPivotR != null)
            {
                armPivotR.localRotation = Quaternion.Euler(rightX, 0f, 0f);
            }
        }

        private void SetLegAngles(float leftX, float rightX)
        {
            if (legPivotL != null)
            {
                legPivotL.localRotation = Quaternion.Euler(leftX, 0f, 0f);
            }

            if (legPivotR != null)
            {
                legPivotR.localRotation = Quaternion.Euler(rightX, 0f, 0f);
            }
        }

        private void ApplyHumanMaterialsIfNeeded()
        {
            if (humanVisual == null)
            {
                return;
            }

            Renderer[] renderers = humanVisual.GetComponentsInChildren<Renderer>(true);
            bool anyMissing = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                Material[] mats = renderer.sharedMaterials;
                if (mats == null || mats.Length == 0 || mats[0] == null)
                {
                    anyMissing = true;
                    break;
                }
            }

            if (!anyMissing)
            {
                return;
            }

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                Material mat = ResolveVampirePartMaterial(renderer.gameObject.name);
                if (mat != null)
                {
                    renderer.sharedMaterial = mat;
                }
            }
        }

        private static Material ResolveVampirePartMaterial(string partName)
        {
            string key = partName ?? string.Empty;
            string matName = "Vamp_skin";
            if (ContainsIgnoreCase(key, "Shirt") || ContainsIgnoreCase(key, "Sleeve"))
            {
                matName = "Vamp_shirt";
            }
            else if (ContainsIgnoreCase(key, "Vest"))
            {
                matName = "Vamp_vest";
            }
            else if (ContainsIgnoreCase(key, "Leg") || ContainsIgnoreCase(key, "Jeans"))
            {
                matName = "Vamp_jeans";
            }
            else if (ContainsIgnoreCase(key, "Patch"))
            {
                matName = "Vamp_patch";
            }
            else if (ContainsIgnoreCase(key, "Shoe"))
            {
                matName = "Vamp_shoe";
            }
            else if (ContainsIgnoreCase(key, "Belt"))
            {
                matName = "Vamp_belt";
            }
            else if (ContainsIgnoreCase(key, "Buckle"))
            {
                matName = "Vamp_buckle";
            }
            else if (ContainsIgnoreCase(key, "Eye") && !ContainsIgnoreCase(key, "Socket"))
            {
                matName = "Vamp_eye";
            }
            else if (ContainsIgnoreCase(key, "Fang"))
            {
                matName = "Vamp_fang";
            }
            else if (ContainsIgnoreCase(key, "Brow") ||
                     ContainsIgnoreCase(key, "Socket") ||
                     ContainsIgnoreCase(key, "Mouth") ||
                     ContainsIgnoreCase(key, "Nostril"))
            {
                matName = "Vamp_black";
            }

            Material mat = Resources.Load<Material>("Vampire/" + matName);
            if (mat != null)
            {
                return mat;
            }

            // Fallback: materials extracted next to the model / Materials folder via editor setup.
            mat = FindLoadedMaterial(matName);
            return mat != null ? mat : CreateRuntimePartMaterial(matName);
        }

        private static Material FindLoadedMaterial(string matName)
        {
            Material[] loaded = Resources.FindObjectsOfTypeAll<Material>();
            for (int i = 0; i < loaded.Length; i++)
            {
                Material candidate = loaded[i];
                if (candidate != null && candidate.name == matName)
                {
                    return candidate;
                }
            }

            return null;
        }

        private static Material CreateRuntimePartMaterial(string matName)
        {
            Color color = Color.magenta;
            switch (matName)
            {
                case "Vamp_skin": color = new Color(0.52f, 0.56f, 0.46f); break;
                case "Vamp_shirt": color = new Color(0.62f, 0.06f, 0.10f); break;
                case "Vamp_vest": color = new Color(0.02f, 0.02f, 0.025f); break;
                case "Vamp_jeans": color = new Color(0.12f, 0.20f, 0.45f); break;
                case "Vamp_patch": color = new Color(0.28f, 0.40f, 0.58f); break;
                case "Vamp_shoe": color = new Color(0.30f, 0.16f, 0.08f); break;
                case "Vamp_belt": color = new Color(0.18f, 0.10f, 0.06f); break;
                case "Vamp_buckle": color = new Color(0.82f, 0.82f, 0.85f); break;
                case "Vamp_black": color = Color.black; break;
                case "Vamp_eye": color = new Color(1f, 0.38f, 0.02f); break;
                case "Vamp_fang": color = new Color(1f, 1f, 0.98f); break;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            Material mat = new Material(shader);
            mat.name = matName + "_Runtime";
            if (mat.HasProperty("_BaseColor"))
            {
                mat.SetColor("_BaseColor", color);
            }

            mat.color = color;
            if (matName == "Vamp_eye" && mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", color * 2.5f);
            }

            return mat;
        }

        private static bool ContainsIgnoreCase(string value, string token)
        {
            return value.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void RebuildBatSprites()
        {
            batWingOutSprite = CreateSprite(BatWingOutTexture, batWingOutSprite);
            batWingUpSprite = CreateSprite(BatWingUpTexture, batWingUpSprite);
        }

        private static Sprite CreateSprite(Texture2D texture, Sprite existing)
        {
            if (texture == null)
            {
                return existing;
            }

            if (existing != null && existing.texture == texture)
            {
                return existing;
            }

            return Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f);
        }

        private void AnimateBatBillboard()
        {
            if (batVisual == null)
            {
                return;
            }

            Camera cam = ResolveViewCamera();
            if (cam != null)
            {
                // World-space view billboard (not local to the vampire root).
                batVisual.rotation = cam.transform.rotation;
            }

            if (Time.time >= nextFlapTime)
            {
                nextFlapTime = Time.time + BatFlapSeconds;
                flapWingUp = !flapWingUp;
                ApplyBatFrame(flapWingUp);
            }
        }

        private Camera ResolveViewCamera()
        {
            // Billboard may use any living player's camera; this is not combat awareness.
            MiniVanPlayer player = FindAnyLivingPlayer();
            if (player != null && player.PlayerCamera != null && player.PlayerCamera.enabled)
            {
                return player.PlayerCamera;
            }

            Camera main = Camera.main;
            if (main != null && main.enabled)
            {
                return main;
            }

            Camera[] cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
            for (int i = 0; i < cameras.Length; i++)
            {
                if (cameras[i] != null && cameras[i].enabled && cameras[i].gameObject.activeInHierarchy)
                {
                    return cameras[i];
                }
            }

            return null;
        }

        private Vector3 GetDeathBodyCenter()
        {
            Renderer[] renderers = null;
            if (humanVisual != null && humanVisual.gameObject.activeInHierarchy)
            {
                renderers = humanVisual.GetComponentsInChildren<Renderer>(true);
            }
            else if (batVisual != null)
            {
                renderers = batVisual.GetComponentsInChildren<Renderer>(true);
            }

            if (renderers != null && renderers.Length > 0)
            {
                bool hasBounds = false;
                Bounds bounds = default;
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

                if (hasBounds)
                {
                    return bounds.center;
                }
            }

            // Fallback: mid-torso in local space (works upright and after side-fall roll).
            return transform.TransformPoint(new Vector3(0f, 0.95f, 0f));
        }

        private void SpawnVampireDeathFx(Vector3 position, Vector3 impulse, int seed)
        {
            if (DeathParticlePrefab != null)
            {
                Quaternion rotation = Quaternion.identity;
                Vector3 flatImpulse = Vector3.ProjectOnPlane(impulse, Vector3.up);
                if (flatImpulse.sqrMagnitude > 0.01f)
                {
                    rotation = Quaternion.LookRotation(flatImpulse.normalized, Vector3.up);
                }

                GameObject fx = Instantiate(DeathParticlePrefab, position, rotation);
                fx.name = DeathParticlePrefab.name + "_" + seed;
                fx.transform.SetParent(null, true);

                float destroyAfter = 2f;
                ParticleSystem[] systems = fx.GetComponentsInChildren<ParticleSystem>(true);
                if (systems.Length == 0)
                {
                    Debug.LogWarning(
                        "Vampire death particle prefab has no ParticleSystem: " + DeathParticlePrefab.name,
                        DeathParticlePrefab);
                }

                for (int i = 0; i < systems.Length; i++)
                {
                    ParticleSystem system = systems[i];
                    if (system == null)
                    {
                        continue;
                    }

                    var main = system.main;
                    main.playOnAwake = false;
                    main.loop = false;
                    system.Clear(true);
                    system.Play(true);
                    destroyAfter = Mathf.Max(
                        destroyAfter,
                        main.duration + main.startLifetime.constantMax + 0.5f);
                }

                Destroy(fx, destroyAfter);
            }
            else
            {
                Debug.LogWarning("Vampire DeathParticlePrefab is not assigned.", this);
            }

            if (DeathPowderPrefab != null)
            {
                // Ash pile sits on the ground under the body center (not at the feet).
                Vector3 powderPos = position;
                if (Physics.Raycast(
                        position + Vector3.up * 0.75f,
                        Vector3.down,
                        out RaycastHit ground,
                        4f,
                        ~0,
                        QueryTriggerInteraction.Ignore))
                {
                    powderPos = ground.point + Vector3.up * 0.02f;
                }
                else
                {
                    powderPos = new Vector3(position.x, position.y - 0.2f, position.z);
                }

                GameObject powder = Instantiate(DeathPowderPrefab, powderPos, Quaternion.identity);
                powder.name = DeathPowderPrefab.name + "_" + seed;
                powder.transform.SetParent(null, true);
            }
        }

        private void ApplyBatFrame(bool wingUp)
        {
            if (batSpriteRenderer == null)
            {
                return;
            }

            Sprite frame = wingUp ? batWingUpSprite : batWingOutSprite;
            if (frame == null)
            {
                frame = wingUp ? batWingOutSprite : batWingUpSprite;
            }

            if (frame != null)
            {
                batSpriteRenderer.sprite = frame;
                ApplyBatVisualScale();
            }
        }

        private void ApplyBatVisualScale()
        {
            if (batVisual == null)
            {
                return;
            }

            float spriteHeight = 1f;
            if (batSpriteRenderer != null && batSpriteRenderer.sprite != null)
            {
                spriteHeight = Mathf.Max(0.01f, batSpriteRenderer.sprite.bounds.size.y);
            }

            // ~1/4 of a ~1.9m humanoid zombie.
            float scale = Mathf.Max(0.1f, BatWorldHeight) / spriteHeight;
            batVisual.localScale = Vector3.one * scale;
        }

        private static void DisableColliders(GameObject root)
        {
            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                {
                    colliders[i].enabled = false;
                }
            }
        }

        private void SnapAboveGround()
        {
            Vector3 origin = transform.position + Vector3.up * 1.5f;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 4f, ~0, QueryTriggerInteraction.Ignore))
            {
                transform.position = hit.point;
            }
        }

        private void FaceFlatDirection(Vector3 dir)
        {
            Vector3 flat = Vector3.ProjectOnPlane(dir, Vector3.up);
            if (flat.sqrMagnitude < 0.001f)
            {
                return;
            }

            transform.rotation = Quaternion.LookRotation(flat.normalized, Vector3.up);
        }

        private Vector3 GetPlayerAimPoint(MiniVanPlayer player)
        {
            if (player == null)
            {
                return transform.position;
            }

            // Bat form flies at the player's eye / camera height by default.
            if (player.PlayerCamera != null && player.PlayerCamera.enabled)
            {
                return player.PlayerCamera.transform.position;
            }

            if (player.CameraRoot != null)
            {
                return player.CameraRoot.position;
            }

            return player.transform.position + Vector3.up * BatEyeHeightFallback;
        }

        private void TickIdleWander()
        {
            EnsureIdleOrigin();
            if (CurrentForm != Form.Human)
            {
                SnapAboveGround();
                SetForm(Form.Human, false);
            }

            if (controller != null && !controller.enabled)
            {
                controller.enabled = true;
            }

            Vector3 flat = Vector3.ProjectOnPlane(idleTarget - transform.position, Vector3.up);
            if (flat.magnitude <= PatrolPointReachDistance || Time.time >= nextIdlePickTime)
            {
                PickIdleTarget(false);
                flat = Vector3.ProjectOnPlane(idleTarget - transform.position, Vector3.up);
            }

            if (flat.sqrMagnitude <= 0.001f)
            {
                UpdateHumanLimbAnimation(false);
                return;
            }

            Vector3 dir = flat.normalized;
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(dir, Vector3.up),
                1f - Mathf.Exp(-7f * Time.deltaTime));
            if (controller != null && controller.enabled)
            {
                Vector3 motion = dir * ((PatrolSpeedKph / 3.6f) * GetMovementRecoverScale());
                motion.y = controller.isGrounded ? -2f : Gravity * Time.deltaTime;
                controller.Move(motion * Time.deltaTime);
            }

            UpdateHumanLimbAnimation(true);
        }

        private void EnsureIdleOrigin()
        {
            if (hasIdleOrigin)
            {
                return;
            }

            idleOrigin = hasSpawnPose ? spawnPosePosition : transform.position;
            idleFacing = hasSpawnPose ? spawnPoseRotation : transform.rotation;
            hasIdleOrigin = true;
            PickIdleTarget(true);
        }

        private void PickIdleTarget(bool immediate)
        {
            idleSide = -idleSide;
            float radius = Mathf.Max(0.5f, PatrolRadius);
            Vector3 right = idleFacing * Vector3.right;
            Vector3 forward = idleFacing * Vector3.forward;
            idleTarget = idleOrigin +
                         right * idleSide * Random.Range(radius * 0.4f, radius) +
                         forward * Random.Range(-radius * 0.28f, radius * 0.28f);
            if (Physics.Raycast(
                    idleTarget + Vector3.up * 6f,
                    Vector3.down,
                    out RaycastHit hit,
                    12f,
                    ~0,
                    QueryTriggerInteraction.Ignore))
            {
                idleTarget = hit.point;
            }

            nextIdlePickTime = Time.time + (immediate
                ? 0.15f
                : PatrolWaitSeconds + Random.Range(0.35f, 1.1f));
        }

        /// <summary>
        /// Combat awareness: first acquire inside DetectionRange (or HearingRange), then briefly
        /// remember the target via ChaseMemorySeconds. Outside that, the vampire idles.
        /// </summary>
        private MiniVanPlayer FindAwarePlayer(out float distance)
        {
            distance = float.MaxValue;
            float detectRange = Mathf.Max(0.5f, DetectionRange);
            float hearRange = Mathf.Max(0f, HearingRange);
            float memorySeconds = Mathf.Max(0.05f, ChaseMemorySeconds);
            Vector3 origin = transform.position;

            MiniVanPlayer bestInRange = null;
            float bestInRangeDistance = float.MaxValue;
            MiniVanPlayer[] players = CollectLivingPlayers();
            for (int i = 0; i < players.Length; i++)
            {
                MiniVanPlayer player = players[i];
                if (player == null || player.IsIgnoredByEnemies)
                {
                    continue;
                }

                float d = GetDetectionDistance(origin, player);
                bool inDetect = d <= detectRange;
                bool inHear = hearRange > 0.01f && d <= hearRange;
                if ((inDetect || inHear) && d < bestInRangeDistance)
                {
                    bestInRange = player;
                    bestInRangeDistance = d;
                }
            }

            if (bestInRange != null)
            {
                awareTarget = bestInRange;
                lastAwareTime = Time.time;
                distance = bestInRangeDistance;
                return awareTarget;
            }

            if (awareTarget != null &&
                !awareTarget.IsIgnoredByEnemies &&
                Time.time - lastAwareTime <= memorySeconds)
            {
                distance = GetDetectionDistance(origin, awareTarget);
                // Soft leash so memory cannot drag a chase across the whole map.
                if (distance <= detectRange * 1.75f)
                {
                    return awareTarget;
                }
            }

            awareTarget = null;
            distance = float.MaxValue;
            return null;
        }

        private static float GetDetectionDistance(Vector3 origin, MiniVanPlayer player)
        {
            // Body distance is stabler for aggro than camera/eye aim (crouch/look shouldn't hide you).
            Vector3 body = player.transform.position + Vector3.up * 0.9f;
            return Vector3.Distance(origin, body);
        }

        private MiniVanPlayer FindAnyLivingPlayer()
        {
            MiniVanPlayer[] players = CollectLivingPlayers();
            MiniVanPlayer best = null;
            float bestDistance = float.MaxValue;
            Vector3 origin = transform.position;
            for (int i = 0; i < players.Length; i++)
            {
                float d = Vector3.Distance(origin, players[i].transform.position);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = players[i];
                }
            }

            return best;
        }

        private static MiniVanPlayer[] CollectLivingPlayers()
        {
            MiniVanPlayer[] scanned = MiniVanSceneScan.Get<MiniVanPlayer>();
            int living = 0;
            for (int i = 0; i < scanned.Length; i++)
            {
                MiniVanPlayer player = scanned[i];
                if (player != null && !player.IsZombieDead && !player.IsDowned)
                {
                    living++;
                }
            }

            if (living == 0)
            {
                MiniVanPlayer[] all = FindObjectsByType<MiniVanPlayer>(FindObjectsSortMode.None);
                int count = 0;
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i] != null && !all[i].IsZombieDead && !all[i].IsDowned)
                    {
                        count++;
                    }
                }

                MiniVanPlayer[] fallback = new MiniVanPlayer[count];
                int write = 0;
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i] != null && !all[i].IsZombieDead && !all[i].IsDowned)
                    {
                        fallback[write++] = all[i];
                    }
                }

                return fallback;
            }

            MiniVanPlayer[] result = new MiniVanPlayer[living];
            int index = 0;
            for (int i = 0; i < scanned.Length; i++)
            {
                MiniVanPlayer player = scanned[i];
                if (player != null && !player.IsZombieDead && !player.IsDowned)
                {
                    result[index++] = player;
                }
            }

            return result;
        }

        /// <summary>
        /// Returns true when something blocks the bat ray. intactGlass distinguishes panes the bat
        /// must not pass from ordinary solids it can fly over.
        /// </summary>
        private bool TryGetBatBlock(
            Vector3 from,
            Vector3 to,
            out Vector3 hitPoint,
            out bool intactGlass)
        {
            hitPoint = to;
            intactGlass = false;
            Vector3 delta = to - from;
            float distance = delta.magnitude;
            if (distance < 0.01f)
            {
                return false;
            }

            RaycastHit[] hits = Physics.RaycastAll(
                from,
                delta / distance,
                distance,
                ~0,
                QueryTriggerInteraction.Collide);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            for (int i = 0; i < hits.Length; i++)
            {
                Collider col = hits[i].collider;
                if (col == null || col.transform.IsChildOf(transform))
                {
                    continue;
                }

                if (col.GetComponentInParent<MiniVanPlayer>() != null ||
                    col.GetComponentInParent<MiniVanZombie>() != null)
                {
                    continue;
                }

                MiniVanPanelkaBreakableWindowBase window =
                    col.GetComponentInParent<MiniVanPanelkaBreakableWindowBase>();
                if (window != null)
                {
                    if (!window.IsBroken)
                    {
                        hitPoint = hits[i].point;
                        intactGlass = true;
                        return true;
                    }

                    continue;
                }

                // Doors / vehicles: bat flies past or over instead of freezing.
                if (col.GetComponentInParent<MiniVanPanelkaRoomDoor>() != null ||
                    col.GetComponentInParent<MiniVanApartmentDoor>() != null ||
                    col.GetComponentInParent<MiniVanVehicle>() != null)
                {
                    continue;
                }

                if (!col.isTrigger)
                {
                    // Floors/ceilings get a pass so the bat can climb stories through holes.
                    if (hits[i].normal.y > 0.55f || hits[i].normal.y < -0.55f)
                    {
                        continue;
                    }

                    hitPoint = hits[i].point;
                    intactGlass = false;
                    return true;
                }
            }

            return false;
        }
    }
}
