using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace MiniVanGame
{
    /// <summary>
    /// Wall/ceiling hunter: clings, stalks, pounces, knocks the player down, then flees and hides.
    /// </summary>
    public sealed class MiniVanTraceZombie : MiniVanZombie
    {
        private enum TraceState
        {
            SeekSurface,
            Cling,
            Stalk,
            Aim,
            Pounce,
            LandVulnerable,
            Flee,
            HitchVan,
            Hidden
        }

        public GameObject VisualPrefab;
        [Header("Trace Hunt")]
        [Min(2f)] public float PounceRange = 12f;
        [Min(0.2f)] public float AimSeconds = 1f;
        [Min(0.2f)] public float LandVulnerableSeconds = 0.85f;
        [Min(4f)] public float ClimbSpeed = 5.2f;
        [Min(0.2f)] public float SurfaceStickDistance = 0.2f;
        [Min(4f)] public float SurfaceSearchRadius = 18f;
        [Min(4f)] public float HideRespawnSeconds = 20f;
        [Min(6f)] public float HideFromPlayersDistance = 16f;
        [Header("Trace Death")]
        [Min(0.1f)] public float DeathFallSeconds = 0.35f;
        [Min(0f)] public float DeathLieSeconds = 1f;

        public override string EnemyDisplayName => "Trace Zombie";

        public override int CurrentHealth =>
            UsesOfflineHealth()
                ? Mathf.Max(0, MaxHealth - offlineHitsTaken)
                : health.Value;

        private Transform visualRoot;
        private Animator visualAnimator;
        private Vector3 spawnPosePosition;
        private Quaternion spawnPoseRotation = Quaternion.identity;
        private float spawnPoseLockUntil;
        private bool hasSpawnPose;
        private bool visualsReady;
        private bool isDying;
        private int offlineHitsTaken;
        private TraceState state = TraceState.SeekSurface;
        private MiniVanPlayer markedPlayer;
        private float lastSawMarkedTime = -999f;
        private float aimStartedAt;
        private float landUntil;
        private float hideUntil;
        private Vector3 pounceStart;
        private Vector3 pounceEnd;
        private float pounceElapsed;
        private float pounceDuration = 0.45f;
        private bool pounceHitApplied;
        private Vector3 surfacePoint;
        private Vector3 surfaceNormal = Vector3.up;
        private Transform hitchParent;
        private Vector3 hitchLocalPosition;
        private Quaternion hitchLocalRotation = Quaternion.identity;
        private MiniVanVehicle hitchVehicle;
        private bool attached;
        private bool restingAfterPounce;
        private float restUntil;
        private bool hasPerch;
        private const float MinHuntPerchHeight = 1.4f;
        private const float FallbackPerchHeight = 2f;
        private const float PreferredPerchHeight = 4f;
        private static MiniVanPanelkaBreakableWindowBase[] cachedWindows;
        private static float nextWindowCacheTime;
        private bool hideWatchRunning;
        private bool hideWatchAbort;
        private bool requireHiddenPerch;
        private float coverSearchSince = -1f;
        private float swaySeed;
        private MiniVanPlayer ambushTarget;
        private const float PlayerInterestRange = 20f;
        private const float IdleRelocateSeconds = 30f;
        private const float CoverGiveUpSeconds = 12f;
        private float idleFarSince = -1f;
        private bool spawnRelocateTried;
        private float nextRelocateTryAt;
        private const float RejectedPerchRadius = 2.2f;
        private const float SurfaceSwitchCooldown = 0.3f;
        private float nextSurfaceSwitchAt;
        private Renderer[] visualRenderers;
        private float nextVisualRendererScan;
        private Vector3 fleeTangent;
        private float fleeDirUntil;
        private Vector3 fleeProgressFrom;
        private float fleeProgressCheckAt;
        private float fleeSideSign = 1f;
        private const float RejectedPerchSeconds = 7f;
        private readonly Vector3[] rejectedPerches = new Vector3[6];
        private readonly float[] rejectedPerchUntil = new float[6];
        private int rejectedPerchCursor;
        private Vector3 perchPoint;
        private Vector3 perchNormal = Vector3.up;
        private Transform perchParent;
        private float perchRefreshAt;
        private static MiniVanPlayer[] cachedPlayers;
        private static float nextPlayerCacheTime;
        private static readonly RaycastHit[] SurfaceHits = new RaycastHit[24];
        private static readonly int AnimSpeed = Animator.StringToHash("Speed");
        private static readonly int AnimCling = Animator.StringToHash("Cling");
        private static readonly int AnimPounce = Animator.StringToHash("Pounce");
        private static readonly int AnimLand = Animator.StringToHash("Land");

        public void ApplySpawnPose(Vector3 position, Quaternion rotation)
        {
            if (IsUnderTerrain(position))
            {
                position.y = GetTerrainHeight(position) + 1.7f;
            }

            hasSpawnPose = true;
            spawnPosePosition = position;
            spawnPoseRotation = rotation;
            spawnPoseLockUntil = Time.time + 0.35f;
            transform.SetPositionAndRotation(position, rotation);
        }

        private void Start()
        {
            MaxHealth = 2;
            DamagePerHit = 2;
            DetectionRange = Mathf.Max(DetectionRange, 18f);
            OpenPanelkaDoorsWhenChasing = false;
            EnsureTraceVisuals();
            if (hasSpawnPose)
            {
                transform.SetPositionAndRotation(spawnPosePosition, spawnPoseRotation);
            }

            state = TraceState.SeekSurface;
        }

        private void LateUpdate()
        {
            EnsureTraceVisuals();
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
                SetAnim(0f, false);
                return;
            }

            if (!HasAiAuthority())
            {
                SetAnim(IsMovingForWalkAnim() ? 1f : 0f, attached);
                return;
            }

            if (ApplyKnockbackMotion(Time.deltaTime) && state != TraceState.Pounce)
            {
                SetAnim(0f, attached);
                return;
            }

            TickAi();
        }

        protected override bool TryOverrideServerUpdate()
        {
            return true;
        }

        protected override void EnsureVisual()
        {
            EnsureTraceVisuals();
        }

        protected override void AnimateArms()
        {
        }

        protected override void ServerSpawnDeathParts(Vector3 impulse)
        {
            BeginTraceDeath(transform.position - transform.forward, impulse);
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
            DetachFromSurface();

            if (UsesOfflineHealth())
            {
                offlineHitsTaken += applied;
                if (offlineHitsTaken >= Mathf.Max(1, MaxHealth))
                {
                    BeginTraceDeath(hitOrigin, knockbackVelocity);
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
                BeginTraceDeath(hitOrigin, knockbackVelocity);
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
                BeginTraceDeath(transform.position - transform.forward, impactVelocity);
                return true;
            }

            if (!IsServer || health.Value <= 0)
            {
                return false;
            }

            health.Value = 0;
            BeginTraceDeath(transform.position - transform.forward, impactVelocity);
            return true;
        }

        private void TickAi()
        {
            TickStayNearPlayers();
            MiniVanPlayer player = FindHuntPlayer();
            switch (state)
            {
                case TraceState.SeekSurface:
                    TickSeekSurface(player);
                    break;
                case TraceState.Cling:
                    TickCling(player);
                    break;
                case TraceState.Stalk:
                    TickStalk(player);
                    break;
                case TraceState.Aim:
                    TickAim(player);
                    break;
                case TraceState.Pounce:
                    TickPounce();
                    break;
                case TraceState.LandVulnerable:
                    TickLandVulnerable();
                    break;
                case TraceState.Flee:
                    TickFlee();
                    break;
                case TraceState.HitchVan:
                    TickHitchVan(player);
                    break;
                case TraceState.Hidden:
                    TickHidden();
                    break;
            }
        }

        /// <summary>
        /// Stay in the player's neighborhood: relocate to a hidden perch near the nearest living
        /// player instead of waiting forever on a far wall. Teleport only while unseen.
        /// </summary>
        private void TickStayNearPlayers()
        {
            if (isDying ||
                state == TraceState.Hidden ||
                state == TraceState.Pounce ||
                state == TraceState.Aim ||
                state == TraceState.LandVulnerable ||
                state == TraceState.Flee ||
                state == TraceState.HitchVan)
            {
                idleFarSince = -1f;
                return;
            }

            if (hasSpawnPose && Time.time < spawnPoseLockUntil)
            {
                return;
            }

            if (HasPlayerInInterestRange())
            {
                idleFarSince = -1f;
                spawnRelocateTried = true;
                return;
            }

            if (!spawnRelocateTried)
            {
                spawnRelocateTried = true;
                TryRelocateNearPlayerNow();
                return;
            }

            if (idleFarSince < 0f)
            {
                idleFarSince = Time.time;
                return;
            }

            if (Time.time - idleFarSince < IdleRelocateSeconds ||
                Time.time < nextRelocateTryAt ||
                IsSeenByAnyPlayer())
            {
                return;
            }

            if (TryRelocateNearPlayerNow())
            {
                idleFarSince = -1f;
            }
            else
            {
                nextRelocateTryAt = Time.time + 2f;
            }
        }

        private bool HasPlayerInInterestRange()
        {
            MiniVanPlayer[] players = GetPlayers();
            for (int i = 0; i < players.Length; i++)
            {
                MiniVanPlayer player = players[i];
                if (player == null || !player.isActiveAndEnabled || player.IsIgnoredByEnemies)
                {
                    continue;
                }

                if (Vector3.Distance(transform.position, player.transform.position) <= PlayerInterestRange)
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryRelocateNearPlayerNow()
        {
            if (!TryFindAmbushSpawn(out Vector3 spawn, out Vector3 normal) &&
                !TryFindNearbyPlayerPerch(out spawn, out normal))
            {
                return false;
            }

            AppearAt(spawn, normal);
            return true;
        }

        private void AppearAt(Vector3 spawn, Vector3 normal)
        {
            transform.position = spawn + normal * SurfaceStickDistance;
            Attach(spawn, normal, null);
            spawnPosePosition = transform.position;
            hasSpawnPose = true;
            if (visualRoot != null)
            {
                visualRoot.gameObject.SetActive(true);
            }

            restingAfterPounce = false;
            coverSearchSince = -1f;
            hasPerch = false;
            idleFarSince = -1f;
            ClearPerchBlacklist();
            SetAnim(0f, true);

            if (ambushTarget != null && ambushTarget.isActiveAndEnabled && !ambushTarget.IsIgnoredByEnemies)
            {
                markedPlayer = ambushTarget;
                lastSawMarkedTime = Time.time;
                swaySeed = Time.time;
                state = TraceState.Stalk;
                return;
            }

            markedPlayer = null;
            state = TraceState.Cling;
        }

        private bool TryFindNearbyPlayerPerch(out Vector3 point, out Vector3 normal)
        {
            point = default;
            normal = Vector3.up;
            MiniVanPlayer target = PickNearestLivingPlayer();
            if (target == null)
            {
                return false;
            }

            ambushTarget = target;
            Vector3 pos = target.transform.position;
            Vector3[] anchors =
            {
                pos + Vector3.forward * 8f + Vector3.up * 3f,
                pos - Vector3.forward * 8f + Vector3.up * 3f,
                pos + Vector3.right * 8f + Vector3.up * 3f,
                pos - Vector3.right * 8f + Vector3.up * 3f,
                pos + (Vector3.forward + Vector3.right).normalized * 12f + Vector3.up * 4f,
                pos + (Vector3.forward - Vector3.right).normalized * 12f + Vector3.up * 4f,
                pos + (-Vector3.forward + Vector3.right).normalized * 12f + Vector3.up * 4f,
                pos + (-Vector3.forward - Vector3.right).normalized * 12f + Vector3.up * 4f
            };

            RaycastHit best = default;
            float bestScore = float.MinValue;
            bool found = false;
            for (int i = 0; i < anchors.Length; i++)
            {
                if (!TryFindSurface(anchors[i], 10f, true, PreferredPerchHeight, true, out RaycastHit hit) &&
                    !TryFindSurface(anchors[i], 10f, true, FallbackPerchHeight, true, out hit))
                {
                    continue;
                }

                Vector3 cling = hit.point + hit.normal.normalized * SurfaceStickDistance;
                float dist = Vector3.Distance(pos, cling);
                if (dist < 4f || dist > PlayerInterestRange ||
                    IsUnderTerrain(cling) ||
                    IsSpotSeenByAnyPlayer(hit.point, hit.normal))
                {
                    continue;
                }

                float score = ScoreSurface(hit, pos, true, true) - dist * 0.15f;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = hit;
                    found = true;
                }
            }

            if (!found)
            {
                return false;
            }

            point = best.point;
            normal = best.normal.sqrMagnitude > 0.01f ? best.normal.normalized : Vector3.up;
            return true;
        }

        private MiniVanPlayer PickNearestLivingPlayer()
        {
            MiniVanPlayer[] players = GetPlayers();
            MiniVanPlayer best = null;
            float bestDist = float.MaxValue;
            Vector3 origin = GetRelocateOrigin();
            for (int i = 0; i < players.Length; i++)
            {
                MiniVanPlayer player = players[i];
                if (player == null || !player.isActiveAndEnabled || player.IsIgnoredByEnemies)
                {
                    continue;
                }

                float dist = Vector3.Distance(origin, player.transform.position);
                if (dist < bestDist)
                {
                    best = player;
                    bestDist = dist;
                }
            }

            return best;
        }

        private Vector3 GetRelocateOrigin()
        {
            if (state == TraceState.Hidden || IsUnderTerrain(transform.position))
            {
                return hasSpawnPose ? spawnPosePosition : Vector3.zero;
            }

            return transform.position;
        }

        private void TickSeekSurface(MiniVanPlayer player)
        {
            if (IsUnderTerrain(transform.position))
            {
                Vector3 lifted = transform.position;
                lifted.y = GetTerrainHeight(lifted) + 1.8f;
                transform.position = lifted;
                if (hasSpawnPose)
                {
                    spawnPosePosition = lifted;
                }
            }

            if (player != null && player.CurrentVehicle != null && TryBeginHitch(player.CurrentVehicle))
            {
                return;
            }

            if (player != null &&
                Vector3.Distance(transform.position, player.transform.position) <= 8.5f &&
                CanPounce(player))
            {
                BeginPounce(player);
                return;
            }

            if (EnsurePerch(transform.position, SurfaceSearchRadius, false))
            {
                if (ClimbTowardPerch(ClimbSpeed))
                {
                    Attach(perchPoint, perchNormal, perchParent);
                    hasPerch = false;
                    state = TraceState.Cling;
                    SetAnim(0f, true);
                    return;
                }

                SetAnim(1f, attached);
                return;
            }

            ApplyGroundGravity();
            SetAnim(0.4f, false);
        }

        private void TickCling(MiniVanPlayer player)
        {
            if (restingAfterPounce && Time.time >= restUntil && !hideWatchRunning)
            {
                restingAfterPounce = false;
            }

            if ((hideWatchRunning && !hideWatchAbort) || restingAfterPounce)
            {
                if (IsSeenByAnyPlayer() || !IsHighEnoughToHide())
                {
                    SeekCoverMotion(player);
                    return;
                }

                KeepStuck();
                SetAnim(0f, true);
                return;
            }

            float playerDist = player != null
                ? Vector3.Distance(transform.position, player.transform.position)
                : float.MaxValue;
            bool huntNow = player != null && playerDist <= DetectionRange;
            if (huntNow)
            {
                if (player.CurrentVehicle != null && TryBeginHitch(player.CurrentVehicle))
                {
                    return;
                }

                KeepStuck();
                markedPlayer = player;
                lastSawMarkedTime = Time.time;
                state = TraceState.Stalk;
                swaySeed = Time.time;
                coverSearchSince = -1f;
                SetAnim(0f, true);
                return;
            }

            if (IsLowPerch())
            {
                DetachFromSurface();
                hasPerch = false;
                state = TraceState.SeekSurface;
                SetAnim(0.4f, false);
                return;
            }

            if (TryClimbOntoRoof())
            {
                SetAnim(1f, true);
                return;
            }

            KeepStuck();
            SetAnim(0f, true);
        }

        private void TickStalk(MiniVanPlayer player)
        {
            if (player == null)
            {
                state = attached ? TraceState.Cling : TraceState.SeekSurface;
                return;
            }

            markedPlayer = player;
            lastSawMarkedTime = Time.time;
            if (player.CurrentVehicle != null && TryBeginHitch(player.CurrentVehicle))
            {
                return;
            }

            KeepStuck();
            Vector3 toPlayer = player.transform.position - transform.position;
            float distance = toPlayer.magnitude;
            Vector3 tangent = Vector3.ProjectOnPlane(toPlayer, surfaceNormal);
            if (distance > 4.5f && tangent.sqrMagnitude > 0.04f)
            {
                CrawlAlong(tangent.normalized, ClimbSpeed * 0.95f);
                SetAnim(1f, true);
            }
            else
            {
                SwayWhileTracking(player, 0.85f);
                SetAnim(0.35f, true);
            }

            if (distance <= PounceRange && CanPounce(player))
            {
                state = TraceState.Aim;
                aimStartedAt = Time.time;
                swaySeed = Time.time;
                SetAnim(0.25f, true);
            }
        }

        private void TickAim(MiniVanPlayer player)
        {
            if (player == null)
            {
                state = TraceState.Cling;
                return;
            }

            KeepStuck();
            SwayWhileTracking(player, 1.35f);
            SetAnim(0.25f, true);
            float aimTime = Vector3.Distance(transform.position, player.transform.position) <= 8.5f
                ? Mathf.Min(AimSeconds, 0.45f)
                : AimSeconds;
            if (Time.time < aimStartedAt + aimTime)
            {
                return;
            }

            BeginPounce(player);
        }

        /// <summary>
        /// Small side-to-side and back-and-forth shifts so the zombie visibly lines up its jump.
        /// </summary>
        private void SwayWhileTracking(MiniVanPlayer player, float strength)
        {
            Vector3 toPlayer = player.transform.position + Vector3.up * 0.9f - transform.position;
            Vector3 forward = Vector3.ProjectOnPlane(toPlayer, surfaceNormal);
            if (forward.sqrMagnitude < 0.01f)
            {
                KeepStuck();
                FacePoint(player.transform.position + Vector3.up * 0.9f);
                return;
            }

            forward.Normalize();
            Vector3 side = Vector3.Cross(surfaceNormal, forward);
            float t = (Time.time - swaySeed) * 4.2f;
            Vector3 offset = side * (Mathf.Sin(t) * 0.75f) + forward * (Mathf.Sin(t * 1.7f) * 0.35f);

            // Position-only shuffle: letting the crawl steer the body too would fight FacePoint
            // and make the zombie twitch instead of tracking the player.
            transform.position += offset * (ClimbSpeed * 0.16f * strength * Time.deltaTime);
            KeepStuck();
            FacePoint(player.transform.position + Vector3.up * 0.9f);
        }

        private void BeginPounce(MiniVanPlayer player)
        {
            DetachFromSurface();
            pounceStart = transform.position;
            pounceEnd = player.transform.position + Vector3.up * 0.95f;
            pounceElapsed = 0f;
            pounceDuration = Mathf.Clamp(Vector3.Distance(pounceStart, pounceEnd) / 16f, 0.32f, 0.55f);
            pounceHitApplied = false;
            state = TraceState.Pounce;
            if (visualAnimator != null)
            {
                visualAnimator.ResetTrigger(AnimLand);
                visualAnimator.SetTrigger(AnimPounce);
            }
        }

        private void TickPounce()
        {
            pounceElapsed += Time.deltaTime;
            float u = Mathf.Clamp01(pounceElapsed / Mathf.Max(0.05f, pounceDuration));
            Vector3 pos = Vector3.Lerp(pounceStart, pounceEnd, u);
            pos.y += Mathf.Sin(u * Mathf.PI) * 1.15f;
            if (controller != null)
            {
                controller.enabled = false;
            }

            transform.position = pos;
            Vector3 dir = pounceEnd - pounceStart;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            }

            SetAnim(1.5f, false);
            if (u < 1f)
            {
                return;
            }

            TryApplyPounceHit();
            landUntil = Time.time + LandVulnerableSeconds;
            state = TraceState.LandVulnerable;
            SnapOntoGround();
            if (visualAnimator != null)
            {
                visualAnimator.SetTrigger(AnimLand);
            }
        }

        private void TryApplyPounceHit()
        {
            if (pounceHitApplied)
            {
                return;
            }

            pounceHitApplied = true;
            Collider[] hits = Physics.OverlapSphere(transform.position, 1.15f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits.Length; i++)
            {
                MiniVanPlayer player = hits[i] != null
                    ? hits[i].GetComponentInParent<MiniVanPlayer>()
                    : null;
                if (player == null || player.IsIgnoredByEnemies)
                {
                    continue;
                }

                player.ServerApplyTracePounce(Mathf.Max(1, DamagePerHit), transform.position);
                break;
            }
        }

        private void TickLandVulnerable()
        {
            ApplyGroundGravity();
            SetAnim(0f, false);
            if (Time.time < landUntil)
            {
                return;
            }

            state = TraceState.Flee;
        }

        private void TickFlee()
        {
            MiniVanPlayer player = FindHuntPlayer() ?? markedPlayer;
            if (attached && !IsSeenByAnyPlayer() && IsHighEnoughToHide())
            {
                BeginResting();
                KeepStuck();
                SetAnim(0f, true);
                return;
            }

            SeekCoverMotion(player);
        }

        private void BeginResting()
        {
            hideUntil = Time.time + 1.2f;
            restingAfterPounce = true;
            restUntil = Time.time + 5f;
            coverSearchSince = -1f;
            fleeDirUntil = 0f;
            ClearPerchBlacklist();
            BeginHideWatch();
        }

        private void SeekCoverMotion(MiniVanPlayer player)
        {
            if (player != null && player.CurrentVehicle != null && TryBeginHitch(player.CurrentVehicle))
            {
                return;
            }

            if (coverSearchSince < 0f)
            {
                coverSearchSince = Time.time;
                fleeDirUntil = 0f;
            }
            else if (Time.time - coverSearchSince > CoverGiveUpSeconds)
            {
                GiveUpHidingAndHunt();
                return;
            }

            if (EnsurePerch(transform.position, SurfaceSearchRadius + 10f, true, true))
            {
                if (!ClimbTowardPerch(ClimbSpeed * 1.25f))
                {
                    SetAnim(1.2f, attached);
                    return;
                }

                Attach(perchPoint, perchNormal, perchParent);
                if (!IsSeenByAnyPlayer() && IsHighEnoughToHide())
                {
                    hasPerch = false;
                    BeginResting();
                    SetAnim(0f, true);
                    return;
                }

                // The spot turned out to be exposed. Without banning it the next search picks it
                // again and snaps the zombie back every frame, so it looks frozen on the ledge.
                BlacklistPerch(perchPoint);
                hasPerch = false;
                perchRefreshAt = 0f;
            }

            if (attached)
            {
                if (!IsHighEnoughToHide() && TryClimbOntoRoof())
                {
                    SetAnim(1.2f, true);
                    return;
                }

                CrawlAwayFromView(player);
                SetAnim(1.2f, true);
                return;
            }

            if (player != null)
            {
                Vector3 away = transform.position - player.transform.position;
                away.y = 0f;
                if (away.sqrMagnitude > 0.04f)
                {
                    MoveToward(transform.position + away.normalized * 2.2f, ClimbSpeed * 0.9f, true);
                }
            }

            ApplyGroundGravity();
            SetAnim(0.8f, false);
        }

        private void GiveUpHidingAndHunt()
        {
            coverSearchSince = -1f;
            restingAfterPounce = false;
            hasPerch = false;
            if (hideWatchRunning)
            {
                hideWatchAbort = true;
            }

            state = attached ? TraceState.Cling : TraceState.SeekSurface;
        }

        private bool IsHighEnoughToHide()
        {
            if (!attached)
            {
                return false;
            }

            float elev = transform.position.y - GetTerrainHeight(transform.position);
            if (elev >= PreferredPerchHeight)
            {
                return true;
            }

            if ((IsRoofNormal(surfaceNormal) || surfaceNormal.y < -0.4f) &&
                elev >= FallbackPerchHeight)
            {
                return true;
            }

            return false;
        }

        private void CrawlAwayFromView(MiniVanPlayer player)
        {
            Vector3 dir = ResolveFleeDirection(player);
            if (dir.sqrMagnitude > 0.01f)
            {
                CrawlAlong(dir, ClimbSpeed * 1.15f);
            }
            else
            {
                KeepStuck();
            }
        }

        /// <summary>
        /// Recomputing "away from the player" every frame made the zombie reverse the moment it
        /// rounded a corner, so it paced left and right forever. Commit to one direction instead.
        /// </summary>
        private Vector3 ResolveFleeDirection(MiniVanPlayer player)
        {
            if (Time.time < fleeDirUntil)
            {
                Vector3 kept = Vector3.ProjectOnPlane(fleeTangent, surfaceNormal);
                if (kept.sqrMagnitude > 0.01f)
                {
                    if (Time.time >= fleeProgressCheckAt)
                    {
                        bool moved = (transform.position - fleeProgressFrom).sqrMagnitude > 0.09f;
                        fleeProgressFrom = transform.position;
                        fleeProgressCheckAt = Time.time + 0.5f;
                        if (!moved)
                        {
                            fleeDirUntil = 0f;
                            fleeSideSign = -fleeSideSign;
                        }
                    }

                    if (Time.time < fleeDirUntil)
                    {
                        fleeTangent = kept.normalized;
                        return fleeTangent;
                    }
                }
            }

            Vector3 wallUp = Vector3.ProjectOnPlane(Vector3.up, surfaceNormal);
            Vector3 away = player != null
                ? Vector3.ProjectOnPlane(transform.position - player.transform.position, surfaceNormal)
                : Vector3.zero;

            Vector3 alongSurface = wallUp.sqrMagnitude > 0.04f
                ? wallUp.normalized
                : Vector3.ProjectOnPlane(transform.forward, surfaceNormal);
            Vector3 side = alongSurface.sqrMagnitude > 0.01f
                ? Vector3.Cross(surfaceNormal, alongSurface.normalized)
                : Vector3.zero;
            if (side.sqrMagnitude > 0.01f)
            {
                side.Normalize();
                if (away.sqrMagnitude > 0.01f && Vector3.Dot(side, away) < 0f)
                {
                    side = -side;
                }

                side *= fleeSideSign;
            }

            Vector3 dir = wallUp.sqrMagnitude > 0.04f ? wallUp.normalized * 0.5f : Vector3.zero;
            if (side.sqrMagnitude > 0.01f)
            {
                dir += side * 0.9f;
            }
            else if (away.sqrMagnitude > 0.01f)
            {
                dir += away.normalized * 0.9f;
            }

            if (dir.sqrMagnitude < 0.01f)
            {
                dir = Vector3.ProjectOnPlane(transform.forward, surfaceNormal);
            }

            if (dir.sqrMagnitude < 0.01f)
            {
                return Vector3.zero;
            }

            fleeTangent = dir.normalized;
            fleeDirUntil = Time.time + 1.8f;
            fleeProgressFrom = transform.position;
            fleeProgressCheckAt = Time.time + 0.5f;
            return fleeTangent;
        }

        private void BeginHideWatch()
        {
            hideUntil = Time.time + 1.6f;
            state = TraceState.Cling;
            if (hideWatchRunning)
            {
                return;
            }

            hideWatchAbort = false;
            StartCoroutine(HideAfterUnseenRoutine());
        }

        private IEnumerator HideAfterUnseenRoutine()
        {
            hideWatchRunning = true;
            float unseen = 0f;
            while (!isDying && !hideWatchAbort && state != TraceState.Hidden && state != TraceState.Pounce &&
                   state != TraceState.Aim && state != TraceState.LandVulnerable)
            {
                if (IsSeenByAnyPlayer())
                {
                    unseen = 0f;
                }
                else
                {
                    unseen += Time.deltaTime;
                    if (unseen >= 2.4f && Time.time >= hideUntil && IsHighEnoughToHide())
                    {
                        hideWatchRunning = false;
                        EnterHidden();
                        yield break;
                    }
                }

                yield return null;
            }

            hideWatchRunning = false;
            hideWatchAbort = false;
        }

        private void EnterHidden()
        {
            DetachFromSurface();
            state = TraceState.Hidden;
            hideUntil = Time.time + HideRespawnSeconds;
            if (visualRoot != null)
            {
                visualRoot.gameObject.SetActive(false);
            }

            if (controller != null)
            {
                controller.enabled = false;
            }

            transform.position += Vector3.down * 80f;
        }

        private void TickHidden()
        {
            if (Time.time < hideUntil)
            {
                return;
            }

            if (!TryFindHiddenSpawn(out Vector3 spawn, out Vector3 normal))
            {
                hideUntil = Time.time + 2f;
                return;
            }

            AppearAt(spawn, normal);
        }

        private bool TryBeginHitch(MiniVanVehicle van)
        {
            if (van == null || state == TraceState.Pounce)
            {
                return false;
            }

            Vector3 roof = van.transform.position + van.transform.up * 1.55f;
            if (Vector3.Distance(transform.position, roof) > 14f)
            {
                return false;
            }

            if (!MoveToward(roof, ClimbSpeed * 1.1f, true))
            {
                SetAnim(1f, attached);
                return true;
            }

            hitchVehicle = van;
            hitchParent = van.transform;
            SetSurfaceParent(van.transform);
            hitchLocalPosition = van.transform.InverseTransformPoint(roof);
            hitchLocalRotation = Quaternion.Inverse(van.transform.rotation) *
                                 Quaternion.LookRotation(van.transform.forward, van.transform.up);
            attached = true;
            surfaceNormal = van.transform.up;
            if (controller != null)
            {
                controller.enabled = false;
            }

            state = TraceState.HitchVan;
            SetAnim(0f, true);
            return true;
        }

        private void TickHitchVan(MiniVanPlayer player)
        {
            if (hitchParent == null)
            {
                DetachFromSurface();
                state = TraceState.SeekSurface;
                return;
            }

            if (transform.parent == hitchParent)
            {
                transform.localPosition = hitchLocalPosition;
                transform.localRotation = hitchLocalRotation;
            }
            else
            {
                transform.SetPositionAndRotation(
                    hitchParent.TransformPoint(hitchLocalPosition),
                    hitchParent.rotation * hitchLocalRotation);
            }
            SetAnim(0f, true);
            if (player == null)
            {
                return;
            }

            bool playerOutside = player.CurrentVehicle == null;
            bool vanSlow = hitchVehicle != null && hitchVehicle.GetComponent<Rigidbody>() != null &&
                           hitchVehicle.GetComponent<Rigidbody>().linearVelocity.magnitude < 2.4f;
            if (playerOutside || vanSlow)
            {
                SetSurfaceParent(null);
                hitchParent = null;
                hitchVehicle = null;
                if (playerOutside && Vector3.Distance(transform.position, player.transform.position) <= PounceRange)
                {
                    BeginPounce(player);
                }
                else
                {
                    state = TraceState.SeekSurface;
                }
            }
        }

        private bool CanPounce(MiniVanPlayer player)
        {
            if (player.CurrentVehicle != null || player.IsKnockedDown)
            {
                return false;
            }

            if (!HasPounceLineOfSight(player))
            {
                return false;
            }

            float distance = Vector3.Distance(transform.position, player.transform.position);
            if (distance <= 8.5f)
            {
                return true;
            }

            Vector3 toZombie = transform.position - player.transform.position;
            toZombie.y = 0f;
            Vector3 look = player.transform.forward;
            look.y = 0f;
            bool lookingAway = look.sqrMagnitude > 0.01f &&
                               Vector3.Dot(look.normalized, toZombie.normalized) < 0.15f;
            Vector3 planar = player.CharacterController != null
                ? player.CharacterController.velocity
                : Vector3.zero;
            planar.y = 0f;
            bool stopped = planar.magnitude < 1.5f;
            bool cramped = IsPlayerInNarrowSpace(player);
            return lookingAway || stopped || cramped;
        }

        private bool HasPounceLineOfSight(MiniVanPlayer player)
        {
            Vector3 origin = transform.position;
            Vector3 target = player.transform.position + Vector3.up * 0.9f;
            Vector3 delta = target - origin;
            float dist = delta.magnitude;
            if (dist < 0.2f)
            {
                return true;
            }

            if (!RaycastFiltered(origin, delta / dist, dist - 0.2f, out RaycastHit hit))
            {
                return true;
            }

            return hit.collider.GetComponentInParent<MiniVanPlayer>() == player;
        }

        private static bool IsPlayerInNarrowSpace(MiniVanPlayer player)
        {
            int walls = 0;
            Vector3 origin = player.transform.position + Vector3.up * 0.9f;
            Vector3[] dirs = { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };
            for (int i = 0; i < dirs.Length; i++)
            {
                if (Physics.Raycast(origin, dirs[i], out RaycastHit hit, 1.8f, ~0, QueryTriggerInteraction.Ignore) &&
                    hit.normal.y < 0.45f)
                {
                    walls++;
                }
            }

            return walls >= 2;
        }

        private MiniVanPlayer FindHuntPlayer()
        {
            MiniVanPlayer[] players = GetPlayers();
            MiniVanPlayer best = null;
            float bestDist = float.MaxValue;
            for (int i = 0; i < players.Length; i++)
            {
                MiniVanPlayer player = players[i];
                if (player == null || player.IsIgnoredByEnemies)
                {
                    continue;
                }

                float dist = Vector3.Distance(transform.position, player.transform.position);
                if (dist > DetectionRange)
                {
                    continue;
                }

                if (dist < bestDist)
                {
                    best = player;
                    bestDist = dist;
                }
            }

            if (best != null)
            {
                return best;
            }

            if (markedPlayer != null &&
                !markedPlayer.IsIgnoredByEnemies &&
                Time.time - lastSawMarkedTime < 8f)
            {
                return markedPlayer;
            }

            return null;
        }

        private bool EnsurePerch(Vector3 origin, float radius, bool preferHigh)
        {
            return EnsurePerch(origin, radius, preferHigh, false);
        }

        private bool EnsurePerch(Vector3 origin, float radius, bool preferHigh, bool preferCover)
        {
            if (hasPerch && Time.time < perchRefreshAt &&
                Vector3.Distance(transform.position, perchPoint) > 0.45f)
            {
                if (!preferCover)
                {
                    return true;
                }

                if (!IsSpotSeenByAnyPlayer(perchPoint, perchNormal))
                {
                    return true;
                }
            }

            RaycastHit hit = default;
            bool wasRequiring = requireHiddenPerch;
            requireHiddenPerch = preferCover;
            bool found = TryFindSurface(origin, radius, preferHigh, PreferredPerchHeight, preferCover, out hit) ||
                         TryFindSurface(origin, radius, preferHigh, FallbackPerchHeight, preferCover, out hit);
            requireHiddenPerch = wasRequiring;
            if (!found)
            {
                hasPerch = false;
                return false;
            }

            hasPerch = true;
            perchPoint = hit.point;
            perchNormal = hit.normal.sqrMagnitude > 0.01f ? hit.normal.normalized : Vector3.up;
            perchParent = hit.collider != null ? hit.collider.transform : null;
            perchRefreshAt = Time.time + (preferCover ? 0.55f : 2.2f);
            return true;
        }

        private bool TryFindSurface(Vector3 origin, float radius, out RaycastHit best)
        {
            return TryFindSurface(origin, radius, false, FallbackPerchHeight, false, out best);
        }

        private bool TryFindSurface(Vector3 origin, float radius, bool preferHigh, out RaycastHit best)
        {
            return TryFindSurface(origin, radius, preferHigh, FallbackPerchHeight, preferHigh, out best);
        }

        private bool TryFindSurface(
            Vector3 origin,
            float radius,
            bool preferHigh,
            float minHeight,
            bool preferCover,
            out RaycastHit best)
        {
            best = default;
            float bestScore = float.MinValue;
            bool found = false;
            int rays = 24;
            Vector3 rayOrigin = origin + Vector3.up * 0.45f;
            for (int i = 0; i < rays; i++)
            {
                float yaw = (Mathf.PI * 2f * i) / rays;
                Vector3[] dirs =
                {
                    new Vector3(Mathf.Cos(yaw), 0.02f, Mathf.Sin(yaw)),
                    new Vector3(Mathf.Cos(yaw) * 0.7f, 0.7f, Mathf.Sin(yaw) * 0.7f),
                    new Vector3(Mathf.Cos(yaw) * 0.28f, 0.95f, Mathf.Sin(yaw) * 0.28f),
                    new Vector3(Mathf.Cos(yaw) * 0.55f, -0.25f, Mathf.Sin(yaw) * 0.55f),
                    Vector3.up
                };
                for (int d = 0; d < dirs.Length; d++)
                {
                    if (!RaycastFiltered(rayOrigin, dirs[d].normalized, radius, out RaycastHit hit))
                    {
                        continue;
                    }

                    if (ConsiderPerchCandidate(
                            hit, origin, preferHigh, preferCover, minHeight, ref best, ref bestScore))
                    {
                        found = true;
                    }

                    if (preferCover && !IsRoofNormal(hit.normal) && hit.normal.y > -0.4f)
                    {
                        ConsiderFarSide(hit, origin, preferHigh, minHeight, ref best, ref bestScore, ref found);
                    }
                }
            }

            for (int i = 0; i < 16; i++)
            {
                float yaw = (Mathf.PI * 2f * i) / 16f;
                float dist = radius * (0.25f + 0.7f * ((i % 4) / 3f));
                Vector3 sample = origin + new Vector3(Mathf.Cos(yaw) * dist, 0f, Mathf.Sin(yaw) * dist);
                Vector3 from = sample + Vector3.up * 16f;
                if (RaycastFiltered(from, Vector3.down, 22f, out RaycastHit roof) &&
                    ConsiderPerchCandidate(
                        roof, origin, true, preferCover, minHeight, ref best, ref bestScore))
                {
                    found = true;
                    if (IsRoofNormal(roof.normal))
                    {
                        bestScore += 0.01f;
                    }
                }

                if (RaycastFiltered(sample + Vector3.up * 0.4f, Vector3.up, 14f, out RaycastHit ceiling) &&
                    ConsiderPerchCandidate(
                        ceiling, origin, true, preferCover, minHeight, ref best, ref bestScore))
                {
                    found = true;
                }
            }

            if (preferCover)
            {
                ConsiderBrokenWindows(origin, radius, minHeight, ref best, ref bestScore, ref found);
            }

            return found;
        }

        private bool ConsiderPerchCandidate(
            RaycastHit hit,
            Vector3 origin,
            bool preferHigh,
            bool preferCover,
            float minHeight,
            ref RaycastHit best,
            ref float bestScore)
        {
            if (!IsValidSurface(hit) || !IsUsefulHuntPerch(hit) || !MeetsHeight(hit, minHeight))
            {
                return false;
            }

            if (requireHiddenPerch && IsPerchBlacklisted(hit.point))
            {
                return false;
            }

            float score = ScoreSurface(hit, origin, preferHigh, preferCover);
            if (score <= bestScore)
            {
                return false;
            }

            if (requireHiddenPerch && IsSpotSeenByAnyPlayer(hit.point, hit.normal))
            {
                return false;
            }

            bestScore = score;
            best = hit;
            return true;
        }

        /// <summary>
        /// The body sticks out well past the cling point, so a spot only counts as hidden when the
        /// space the zombie will actually occupy is out of sight too.
        /// </summary>
        private bool IsSpotSeenByAnyPlayer(Vector3 point, Vector3 normal)
        {
            Vector3 n = normal.sqrMagnitude > 0.01f ? normal.normalized : Vector3.up;
            Vector3 cling = point + n * SurfaceStickDistance;
            Vector3 along = Vector3.ProjectOnPlane(Vector3.up, n);
            if (along.sqrMagnitude < 0.04f)
            {
                along = Vector3.ProjectOnPlane(Vector3.forward, n);
            }

            along = along.sqrMagnitude > 0.01f ? along.normalized : Vector3.zero;
            Vector3 side = along.sqrMagnitude > 0.01f ? Vector3.Cross(n, along) : Vector3.zero;
            return IsWorldPointSeenByAnyPlayer(cling) ||
                   IsWorldPointSeenByAnyPlayer(cling + n * 0.5f) ||
                   (along.sqrMagnitude > 0.01f && IsWorldPointSeenByAnyPlayer(cling + along * 0.6f + n * 0.3f)) ||
                   (side.sqrMagnitude > 0.01f && IsWorldPointSeenByAnyPlayer(cling + side * 0.5f + n * 0.3f)) ||
                   (side.sqrMagnitude > 0.01f && IsWorldPointSeenByAnyPlayer(cling - side * 0.5f + n * 0.3f));
        }

        private void BlacklistPerch(Vector3 point)
        {
            rejectedPerches[rejectedPerchCursor] = point;
            rejectedPerchUntil[rejectedPerchCursor] = Time.time + RejectedPerchSeconds;
            rejectedPerchCursor = (rejectedPerchCursor + 1) % rejectedPerches.Length;
        }

        private void ClearPerchBlacklist()
        {
            for (int i = 0; i < rejectedPerchUntil.Length; i++)
            {
                rejectedPerchUntil[i] = 0f;
            }
        }

        private bool IsPerchBlacklisted(Vector3 point)
        {
            float radiusSqr = RejectedPerchRadius * RejectedPerchRadius;
            for (int i = 0; i < rejectedPerches.Length; i++)
            {
                if (Time.time < rejectedPerchUntil[i] &&
                    (rejectedPerches[i] - point).sqrMagnitude < radiusSqr)
                {
                    return true;
                }
            }

            return false;
            return true;
        }

        private void ConsiderFarSide(
            RaycastHit wall,
            Vector3 origin,
            bool preferHigh,
            float minHeight,
            ref RaycastHit best,
            ref float bestScore,
            ref bool found)
        {
            Vector3 n = wall.normal.normalized;
            Vector3 farOrigin = wall.point - n * 3.2f + Vector3.up * 0.6f;
            if (!RaycastFiltered(farOrigin, n, 4.2f, out RaycastHit farHit))
            {
                return;
            }

            if (ConsiderPerchCandidate(farHit, origin, preferHigh, true, minHeight, ref best, ref bestScore))
            {
                found = true;
            }

            Vector3 roofFrom = wall.point - n * 1.4f + Vector3.up * 8f;
            if (RaycastFiltered(roofFrom, Vector3.down, 12f, out RaycastHit roof) &&
                ConsiderPerchCandidate(roof, origin, true, true, minHeight, ref best, ref bestScore))
            {
                found = true;
            }
        }

        private void ConsiderBrokenWindows(
            Vector3 origin,
            float radius,
            float minHeight,
            ref RaycastHit best,
            ref float bestScore,
            ref bool found)
        {
            MiniVanPanelkaBreakableWindowBase[] windows = GetBrokenWindows();
            for (int i = 0; i < windows.Length; i++)
            {
                MiniVanPanelkaBreakableWindowBase window = windows[i];
                if (window == null || !window.IsBroken)
                {
                    continue;
                }

                Vector3 winPos = window.transform.position;
                if (Vector3.Distance(origin, winPos) > radius + 4f)
                {
                    continue;
                }

                Vector3 fwd = window.transform.forward;
                Vector3[] probes =
                {
                    winPos + Vector3.up * 8f,
                    winPos + fwd * 1.6f + Vector3.up * 6f,
                    winPos - fwd * 1.6f + Vector3.up * 6f
                };
                for (int p = 0; p < probes.Length; p++)
                {
                    if (RaycastFiltered(probes[p], Vector3.down, 12f, out RaycastHit roof) &&
                        ConsiderPerchCandidate(roof, origin, true, true, minHeight, ref best, ref bestScore))
                    {
                        found = true;
                    }
                }

                if (RaycastFiltered(winPos + fwd * 0.35f, fwd, 5f, out RaycastHit inner) &&
                    ConsiderPerchCandidate(inner, origin, true, true, minHeight, ref best, ref bestScore))
                {
                    found = true;
                }

                if (RaycastFiltered(winPos - fwd * 0.35f, -fwd, 5f, out RaycastHit outer) &&
                    ConsiderPerchCandidate(outer, origin, true, true, minHeight, ref best, ref bestScore))
                {
                    found = true;
                }
            }
        }

        private MiniVanPanelkaBreakableWindowBase[] GetBrokenWindows()
        {
            if (cachedWindows == null || Time.time >= nextWindowCacheTime)
            {
                cachedWindows = FindObjectsByType<MiniVanPanelkaBreakableWindowBase>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
                nextWindowCacheTime = Time.time + 0.7f;
            }

            return cachedWindows;
        }

        private bool TryFindHiddenSpawn(out Vector3 point, out Vector3 normal)
        {
            if (TryFindAmbushSpawn(out point, out normal))
            {
                return true;
            }

            point = spawnPosePosition;
            normal = Vector3.up;
            MiniVanPlayer[] players = GetPlayers();
            Vector3 searchOrigin = hasSpawnPose ? spawnPosePosition : transform.position;
            searchOrigin.y = Mathf.Max(searchOrigin.y, GetTerrainHeight(searchOrigin) + 1.5f);
            for (int i = 0; i < 28; i++)
            {
                Vector3 probe = searchOrigin + Random.insideUnitSphere * 16f;
                probe.y = Mathf.Max(probe.y, GetTerrainHeight(probe) + 2.4f);
                if (!TryFindSurface(probe, 12f, true, PreferredPerchHeight, true, out RaycastHit hit) &&
                    !TryFindSurface(probe, 12f, true, FallbackPerchHeight, true, out hit))
                {
                    continue;
                }

                Vector3 cling = hit.point + hit.normal * SurfaceStickDistance;
                if (IsUnderTerrain(cling) || IsSpotSeenByAnyPlayer(hit.point, hit.normal))
                {
                    continue;
                }

                bool nearPlayer = false;
                for (int p = 0; p < players.Length; p++)
                {
                    if (players[p] != null &&
                        players[p].isActiveAndEnabled &&
                        Vector3.Distance(players[p].transform.position, hit.point) < HideFromPlayersDistance)
                    {
                        nearPlayer = true;
                        break;
                    }
                }

                if (nearPlayer)
                {
                    continue;
                }

                point = hit.point;
                normal = hit.normal;
                return true;
            }

            return false;
        }

        private bool TryFindAmbushSpawn(out Vector3 point, out Vector3 normal)
        {
            point = default;
            normal = Vector3.up;
            ambushTarget = null;
            MiniVanPlayer target = PickAmbushTarget();
            if (target == null)
            {
                return false;
            }

            Vector3 pos = target.transform.position;
            Vector3 fwd = target.transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.01f)
            {
                fwd = Vector3.forward;
            }

            fwd.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            Vector3[] anchors =
            {
                pos - fwd * 9f + Vector3.up * 2.2f,
                pos - fwd * 13f + Vector3.up * 2.8f,
                pos - fwd * 7f + right * 5f + Vector3.up * 2.2f,
                pos - fwd * 7f - right * 5f + Vector3.up * 2.2f,
                pos - fwd * 11f + right * 3.5f + Vector3.up * 3.2f,
                pos - fwd * 11f - right * 3.5f + Vector3.up * 3.2f,
                pos - fwd * 5.5f + Vector3.up * 5.5f,
                pos - fwd * 4f + Vector3.up * 7f
            };

            RaycastHit best = default;
            float bestScore = float.MinValue;
            bool found = false;
            for (int i = 0; i < anchors.Length; i++)
            {
                if (!TryFindSurface(anchors[i], 10f, true, PreferredPerchHeight, true, out RaycastHit hit) &&
                    !TryFindSurface(anchors[i], 10f, true, FallbackPerchHeight, true, out hit))
                {
                    continue;
                }

                if (!IsAmbushPoint(target, hit))
                {
                    continue;
                }

                float score = ScoreSurface(hit, pos, true, true) + AmbushBehindBonus(target, hit.point);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = hit;
                    found = true;
                }
            }

            Vector3 ceilFrom = pos - fwd * 2.4f + Vector3.up * 1.15f;
            if (RaycastFiltered(ceilFrom, Vector3.up, 14f, out RaycastHit ceiling) &&
                ceiling.normal.y < -0.4f &&
                IsAmbushPoint(target, ceiling))
            {
                float score = ScoreSurface(ceiling, pos, true, true) + 12f;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = ceiling;
                    found = true;
                }
            }

            if (!found)
            {
                return false;
            }

            ambushTarget = target;
            point = best.point;
            normal = best.normal.sqrMagnitude > 0.01f ? best.normal.normalized : Vector3.up;
            return true;
        }

        private MiniVanPlayer PickAmbushTarget()
        {
            if (markedPlayer != null && markedPlayer.isActiveAndEnabled && !markedPlayer.IsIgnoredByEnemies)
            {
                return markedPlayer;
            }

            return PickNearestLivingPlayer();
        }

        private bool IsAmbushPoint(MiniVanPlayer player, RaycastHit hit)
        {
            Vector3 cling = hit.point + hit.normal.normalized * SurfaceStickDistance;
            if (IsUnderTerrain(cling) || IsSpotSeenByAnyPlayer(hit.point, hit.normal))
            {
                return false;
            }

            float dist = Vector3.Distance(player.transform.position, cling);
            if (dist < 5f || dist > 18f)
            {
                return false;
            }

            Vector3 flat = cling - player.transform.position;
            flat.y = 0f;
            Vector3 look = player.transform.forward;
            look.y = 0f;
            bool behind = look.sqrMagnitude > 0.01f &&
                          flat.sqrMagnitude > 0.01f &&
                          Vector3.Dot(look.normalized, flat.normalized) < -0.12f;
            bool ceiling = hit.normal.y < -0.4f && behind;
            bool highRoof = IsRoofNormal(hit.normal) && behind;
            return behind || ceiling || highRoof;
        }

        private static float AmbushBehindBonus(MiniVanPlayer player, Vector3 point)
        {
            Vector3 flat = point - player.transform.position;
            flat.y = 0f;
            Vector3 look = player.transform.forward;
            look.y = 0f;
            if (look.sqrMagnitude < 0.01f || flat.sqrMagnitude < 0.01f)
            {
                return 0f;
            }

            return Vector3.Dot(look.normalized, flat.normalized) * -8f;
        }

        private static bool IsRoofNormal(Vector3 normal)
        {
            return normal.y > 0.45f;
        }

        private bool IsValidSurface(RaycastHit hit)
        {
            if (hit.collider == null || IsIgnoredSurfaceCollider(hit.collider))
            {
                return false;
            }

            if (hit.collider is TerrainCollider)
            {
                return false;
            }

            Vector3 cling = hit.point + hit.normal.normalized * SurfaceStickDistance;
            if (IsUnderTerrain(hit.point) || IsUnderTerrain(cling))
            {
                return false;
            }

            if (!IsSurfaceExposed(hit))
            {
                return false;
            }

            if (IsRoofNormal(hit.normal) &&
                hit.point.y < GetTerrainHeight(hit.point) + 0.85f)
            {
                return false;
            }

            return true;
        }

        private bool IsUsefulHuntPerch(RaycastHit hit)
        {
            if (IsRoofNormal(hit.normal) || hit.normal.y < -0.4f)
            {
                return hit.point.y >= GetTerrainHeight(hit.point) + FallbackPerchHeight;
            }

            return WallRisesTo(hit.point, hit.normal, FallbackPerchHeight);
        }

        private bool MeetsHeight(RaycastHit hit, float minHeight)
        {
            float elev = hit.point.y - GetTerrainHeight(hit.point);
            if (elev >= minHeight)
            {
                return true;
            }

            if (IsRoofNormal(hit.normal) || hit.normal.y < -0.4f)
            {
                return false;
            }

            return WallRisesTo(hit.point, hit.normal, minHeight);
        }

        private bool IsLowPerch()
        {
            if (!attached)
            {
                return false;
            }

            if (IsRoofNormal(surfaceNormal) || surfaceNormal.y < -0.4f)
            {
                return transform.position.y < GetTerrainHeight(transform.position) + FallbackPerchHeight;
            }

            return !WallRisesTo(transform.position, surfaceNormal, FallbackPerchHeight);
        }

        private bool WallRisesHighEnough(Vector3 point, Vector3 normal)
        {
            return WallRisesTo(point, normal, FallbackPerchHeight);
        }

        private bool WallRisesTo(Vector3 point, Vector3 normal, float height)
        {
            float ground = GetTerrainHeight(point);
            float elev = point.y - ground;
            if (elev >= height)
            {
                return true;
            }

            Vector3 n = normal.normalized;
            Vector3 wallUp = Vector3.ProjectOnPlane(Vector3.up, n);
            if (wallUp.sqrMagnitude < 0.04f)
            {
                return false;
            }

            float need = height - elev + 0.12f;
            Vector3 highProbe = point + n * 0.18f + wallUp.normalized * need;
            if (RaycastFiltered(highProbe, -n, 1.6f, out RaycastHit upHit) &&
                !IsIgnoredSurfaceCollider(upHit.collider) &&
                Vector3.Dot(upHit.normal.normalized, n) > 0.5f)
            {
                return true;
            }

            Vector3 roofFrom = point + n * 0.5f + Vector3.up * (need + 0.35f);
            if (RaycastFiltered(roofFrom, Vector3.down, need + 0.8f, out RaycastHit roof) &&
                IsRoofNormal(roof.normal) &&
                roof.point.y >= ground + height)
            {
                return true;
            }

            return false;
        }

        private static bool IsIgnoredSurfaceCollider(Collider collider)
        {
            return collider.GetComponentInParent<MiniVanPlayer>() != null ||
                   collider.GetComponentInParent<MiniVanZombie>() != null ||
                   collider.GetComponentInParent<MiniVanPlayerCorpseProxy>() != null;
        }

        private static bool IsSurfaceExposed(RaycastHit hit)
        {
            Vector3 air = hit.point + hit.normal.normalized * 0.18f;
            int count = Physics.RaycastNonAlloc(air, hit.normal.normalized, SurfaceHits, 0.32f, ~0,
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider col = SurfaceHits[i].collider;
                if (col == null || col == hit.collider || IsIgnoredSurfaceCollider(col))
                {
                    continue;
                }

                return false;
            }

            return true;
        }

        private static bool IsUnderTerrain(Vector3 world)
        {
            Terrain terrain = Terrain.activeTerrain;
            if (terrain != null)
            {
                float height = terrain.SampleHeight(world) + terrain.transform.position.y;
                if (world.y < height - 0.12f)
                {
                    return true;
                }
            }

            int count = Physics.RaycastNonAlloc(world + Vector3.up * 0.08f, Vector3.up, SurfaceHits, 28f, ~0,
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (SurfaceHits[i].collider is TerrainCollider)
                {
                    return true;
                }
            }

            return false;
        }

        private static float GetTerrainHeight(Vector3 world)
        {
            Terrain terrain = Terrain.activeTerrain;
            if (terrain == null)
            {
                return 0f;
            }

            return terrain.SampleHeight(world) + terrain.transform.position.y;
        }

        private float ScoreSurface(RaycastHit hit, Vector3 origin, bool preferHigh, bool preferCover)
        {
            float elev = hit.point.y - GetTerrainHeight(hit.point);
            float score = elev * 0.55f - Vector3.Distance(origin, hit.point) * 0.07f;
            bool tall = elev >= PreferredPerchHeight ||
                        WallRisesTo(hit.point, hit.normal, PreferredPerchHeight);
            if (tall)
            {
                score += 18f;
            }
            else if (elev >= FallbackPerchHeight ||
                     WallRisesTo(hit.point, hit.normal, FallbackPerchHeight))
            {
                score += 2f;
            }
            else
            {
                score -= 14f;
            }

            if (IsRoofNormal(hit.normal))
            {
                score += 7.5f + elev * 0.45f;
            }
            else if (hit.normal.y < -0.4f)
            {
                score += 6.5f + elev * 0.3f;
            }
            else
            {
                score += 2.1f + hit.point.y * 0.18f;
            }

            if (preferHigh)
            {
                score += elev * 0.9f + hit.point.y * 0.12f;
            }

            if (preferCover)
            {
                Vector3 cling = hit.point + hit.normal.normalized * SurfaceStickDistance;
                MiniVanPlayer hunter = markedPlayer != null ? markedPlayer : FindHuntPlayer();
                if (hunter != null)
                {
                    Vector3 toPlayer = hunter.transform.position - hit.point;
                    if (Vector3.Dot(hit.normal, toPlayer) < 0f)
                    {
                        score += 10f;
                    }

                    Vector3 away = cling - hunter.transform.position;
                    away.y = 0f;
                    Vector3 look = hunter.transform.forward;
                    look.y = 0f;
                    if (look.sqrMagnitude > 0.01f &&
                        away.sqrMagnitude > 0.01f &&
                        Vector3.Dot(look.normalized, away.normalized) < -0.15f)
                    {
                        score += 6f;
                    }
                }
            }

            return score;
        }

        private bool TryClimbOntoRoof()
        {
            if (!attached)
            {
                return false;
            }

            if (IsRoofNormal(surfaceNormal))
            {
                return false;
            }

            if (!WallRisesHighEnough(transform.position, surfaceNormal))
            {
                return false;
            }

            if (TryMountRoofFromCurrent())
            {
                return false;
            }

            Vector3 wallUp = Vector3.ProjectOnPlane(Vector3.up, surfaceNormal);
            if (wallUp.sqrMagnitude < 0.04f)
            {
                return false;
            }

            float heightBefore = transform.position.y;
            CrawlAlong(wallUp.normalized, ClimbSpeed * 1.05f);
            if (TryMountRoofFromCurrent())
            {
                return false;
            }

            // Pressed against the top edge with nowhere to go: report "done" so the caller
            // moves on instead of grinding upwards forever.
            return transform.position.y - heightBefore > 0.0005f && !IsRoofNormal(surfaceNormal);
        }

        private bool CanSwitchSurface()
        {
            return Time.time >= nextSurfaceSwitchAt;
        }

        private bool TryMountRoofFromCurrent()
        {
            if (!CanSwitchSurface())
            {
                return false;
            }

            Vector3 outward = attached ? surfaceNormal : Vector3.up;
            Vector3[] probes =
            {
                transform.position + outward * 0.48f + Vector3.up * 0.9f,
                transform.position + outward * 0.9f + Vector3.up * 1.2f,
                transform.position + outward * 0.22f + Vector3.up * 1.35f,
                transform.position + Vector3.up * 0.7f + outward * 0.65f
            };
            if (TryMountRoofProbes(probes, transform.position.y - 1.6f))
            {
                return true;
            }

            return attached && !IsRoofNormal(surfaceNormal) && TryMountRoofOverLedge(outward);
        }

        /// <summary>
        /// A flat roof sits behind the wall plane, so probes pushed along the outward normal always
        /// miss it. Once the wall stops rising, step over the lip instead.
        /// </summary>
        private bool TryMountRoofOverLedge(Vector3 outward)
        {
            Vector3 inward = -outward;
            if (RaycastFiltered(transform.position + Vector3.up * 1.15f, inward, 0.8f, out RaycastHit stillWall) &&
                !IsRoofNormal(stillWall.normal))
            {
                return false;
            }

            Vector3[] probes =
            {
                transform.position + Vector3.up * 1.1f + inward * 0.4f,
                transform.position + Vector3.up * 1.45f + inward * 0.8f,
                transform.position + Vector3.up * 0.8f + inward * 0.28f
            };
            return TryMountRoofProbes(probes, transform.position.y - 0.4f);
        }

        private bool TryMountRoofProbes(Vector3[] probes, float minRoofY)
        {
            for (int i = 0; i < probes.Length; i++)
            {
                if (!RaycastFiltered(probes[i], Vector3.down, 2.4f, out RaycastHit hit) ||
                    !IsValidSurface(hit) ||
                    !IsRoofNormal(hit.normal) ||
                    hit.point.y < minRoofY)
                {
                    continue;
                }

                Attach(hit.point, hit.normal, hit.collider != null ? hit.collider.transform : null);
                return true;
            }

            return false;
        }

        private bool RaycastFiltered(Vector3 origin, Vector3 direction, float maxDistance, out RaycastHit best)
        {
            best = default;
            int count = Physics.RaycastNonAlloc(origin, direction, SurfaceHits, maxDistance, ~0,
                QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = SurfaceHits[i];
                if (hit.collider == null || IsIgnoredSurfaceCollider(hit.collider) || hit.distance >= nearest)
                {
                    continue;
                }

                nearest = hit.distance;
                best = hit;
                found = true;
            }

            return found;
        }

        private void Attach(Vector3 point, Vector3 normal, Transform parent)
        {
            Vector3 n = normal.sqrMagnitude > 0.01f ? normal.normalized : Vector3.up;
            MiniVanVehicle vehicle = parent != null ? parent.GetComponentInParent<MiniVanVehicle>() : null;
            Transform newParent = vehicle != null ? vehicle.transform : null;

            // Re-attaching to the very same spot every frame snapped position and rotation, which
            // read as violent shaking on ledges. Slide along instead when nothing really changed.
            if (attached &&
                newParent == hitchParent &&
                Vector3.Dot(n, surfaceNormal) > 0.985f &&
                (point - surfacePoint).sqrMagnitude < 0.09f)
            {
                surfacePoint = point;
                surfaceNormal = n;
                ApplyStickPose();
                return;
            }

            attached = true;
            surfacePoint = point;
            surfaceNormal = n;
            hitchParent = newParent;
            hitchVehicle = vehicle;
            nextSurfaceSwitchAt = Time.time + SurfaceSwitchCooldown;
            if (controller != null)
            {
                controller.enabled = false;
            }

            Vector3 pos = surfacePoint + surfaceNormal * SurfaceStickDistance;
            Quaternion rot = Quaternion.LookRotation(
                Vector3.ProjectOnPlane(transform.forward, surfaceNormal).sqrMagnitude > 0.01f
                    ? Vector3.ProjectOnPlane(transform.forward, surfaceNormal)
                    : Vector3.ProjectOnPlane(Vector3.forward, surfaceNormal),
                surfaceNormal);
            SetSurfaceParent(hitchParent);
            transform.SetPositionAndRotation(pos, rot);
        }

        /// <summary>
        /// Netcode rejects re-parenting for objects that were never network-spawned, so only
        /// touch the hierarchy when the parent actually changes and the change is allowed.
        /// </summary>
        private void SetSurfaceParent(Transform parent)
        {
            if (transform.parent == parent)
            {
                return;
            }

            if (parent != null && !IsSpawned)
            {
                return;
            }

            transform.SetParent(parent, true);
        }

        private void KeepStuck()
        {
            if (!attached)
            {
                return;
            }

            if (!IsRoofNormal(surfaceNormal) && TryMountRoofFromCurrent())
            {
                return;
            }

            Vector3 origin = transform.position + surfaceNormal * 0.55f;
            if (RaycastFiltered(origin, -surfaceNormal, 1.8f, out RaycastHit hit) &&
                IsValidSurface(hit))
            {
                if (IsRoofNormal(hit.normal) && !IsRoofNormal(surfaceNormal) && CanSwitchSurface())
                {
                    Attach(hit.point, hit.normal, hit.collider != null ? hit.collider.transform : null);
                    return;
                }

                surfacePoint = hit.point;
                surfaceNormal = Vector3.Slerp(surfaceNormal, hit.normal.normalized, 0.35f).normalized;
                ApplyStickPose();
                return;
            }

            if (RaycastFiltered(transform.position + Vector3.up * 0.55f + surfaceNormal * 0.4f, Vector3.down, 2.4f,
                    out hit) &&
                IsValidSurface(hit))
            {
                Attach(hit.point, hit.normal, hit.collider != null ? hit.collider.transform : null);
                return;
            }

            if (TryWrapAroundLedge())
            {
                return;
            }

            ApplyStickPose();
        }

        /// <summary>
        /// Crawled past a roof edge with nothing underneath: catch the outer wall face
        /// instead of hanging in the air at the last surface point.
        /// </summary>
        private bool TryWrapAroundLedge()
        {
            if (!CanSwitchSurface())
            {
                return false;
            }

            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, surfaceNormal);
            if (forward.sqrMagnitude < 0.01f)
            {
                return false;
            }

            forward.Normalize();
            Vector3 below = transform.position + forward * 0.3f - surfaceNormal * 0.15f + Vector3.down * 0.7f;
            Vector3[] dirs =
            {
                -forward,
                -surfaceNormal,
                (-forward - surfaceNormal).normalized
            };
            for (int i = 0; i < dirs.Length; i++)
            {
                if (dirs[i].sqrMagnitude < 0.01f ||
                    !RaycastFiltered(below, dirs[i], 1.5f, out RaycastHit hit) ||
                    !IsValidSurface(hit) ||
                    IsRoofNormal(hit.normal))
                {
                    continue;
                }

                Attach(hit.point, hit.normal, hit.collider != null ? hit.collider.transform : null);
                return true;
            }

            return false;
        }

        private void ApplyStickPose()
        {
            transform.position = surfacePoint + surfaceNormal * SurfaceStickDistance;
            Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, surfaceNormal);
            if (fwd.sqrMagnitude < 0.01f)
            {
                fwd = Vector3.ProjectOnPlane(Vector3.forward, surfaceNormal);
            }

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(fwd.normalized, surfaceNormal),
                1f - Mathf.Exp(-10f * Time.deltaTime));
        }

        private void CrawlAlong(Vector3 tangent, float speed)
        {
            transform.position += tangent * (speed * Time.deltaTime);
            if (tangent.sqrMagnitude > 0.001f)
            {
                Vector3 fwd = Vector3.ProjectOnPlane(tangent, surfaceNormal);
                if (fwd.sqrMagnitude > 0.001f)
                {
                    transform.rotation = Quaternion.Slerp(
                        transform.rotation,
                        Quaternion.LookRotation(fwd.normalized, surfaceNormal),
                        1f - Mathf.Exp(-8f * Time.deltaTime));
                }
            }

            KeepStuck();
        }

        private void DetachFromSurface()
        {
            attached = false;
            SetSurfaceParent(null);
            hitchParent = null;
            hitchVehicle = null;
            surfaceNormal = Vector3.up;
        }

        private bool ClimbTowardPerch(float speed)
        {
            Vector3 stick = perchPoint + perchNormal * SurfaceStickDistance;
            if (attached)
            {
                Vector3 toStick = stick - transform.position;
                if (toStick.magnitude <= 0.34f)
                {
                    return true;
                }

                Vector3 tangent = Vector3.ProjectOnPlane(toStick, surfaceNormal);
                if (tangent.sqrMagnitude < 0.02f)
                {
                    tangent = Vector3.ProjectOnPlane(
                        Vector3.up * Mathf.Sign(toStick.y + 0.05f),
                        surfaceNormal);
                }

                if (tangent.sqrMagnitude > 0.001f)
                {
                    CrawlAlong(tangent.normalized, speed);
                }
                else
                {
                    KeepStuck();
                }

                return Vector3.Distance(transform.position, stick) <= 0.36f;
            }

            if (TryGrabNearbyWall())
            {
                return false;
            }

            Vector3 flat = stick - transform.position;
            flat.y = 0f;
            float xz = flat.magnitude;
            if (xz > 0.65f)
            {
                Vector3 walkTarget = transform.position + flat.normalized * Mathf.Min(xz, 1.6f);
                if (RaycastFiltered(transform.position + Vector3.up * 0.45f, Vector3.down, 2.6f, out RaycastHit ground) &&
                    ground.collider.GetComponentInParent<MiniVanPlayer>() == null &&
                    ground.collider.GetComponentInParent<MiniVanZombie>() == null)
                {
                    walkTarget.y = ground.point.y + 0.28f;
                }
                else
                {
                    walkTarget.y = transform.position.y;
                }

                MoveToward(walkTarget, speed, true);
                return false;
            }

            Vector3 towardWall = new Vector3(-perchNormal.x, 0f, -perchNormal.z);
            if (towardWall.sqrMagnitude < 0.01f)
            {
                towardWall = flat.sqrMagnitude > 0.01f ? flat : transform.forward;
            }

            towardWall.Normalize();
            Vector3 probe = transform.position + Vector3.up * 0.45f;
            if (RaycastFiltered(probe, towardWall, 1.7f, out RaycastHit wall) &&
                IsValidSurface(wall) &&
                IsUsefulHuntPerch(wall))
            {
                Attach(wall.point, wall.normal, wall.collider != null ? wall.collider.transform : null);
                return false;
            }

            if (RaycastFiltered(probe, (stick - probe).normalized, 2.4f, out wall) &&
                IsValidSurface(wall) &&
                !IsRoofNormal(wall.normal) &&
                IsUsefulHuntPerch(wall))
            {
                Attach(wall.point, wall.normal, wall.collider != null ? wall.collider.transform : null);
                return false;
            }

            return false;
        }

        private bool TryGrabNearbyWall()
        {
            Vector3 origin = transform.position + Vector3.up * 0.42f;
            Vector3[] dirs =
            {
                transform.forward,
                -transform.forward,
                transform.right,
                -transform.right,
                (transform.forward + transform.right).normalized,
                (transform.forward - transform.right).normalized
            };
            for (int i = 0; i < dirs.Length; i++)
            {
                if (!RaycastFiltered(origin, dirs[i], 0.85f, out RaycastHit hit) ||
                    !IsValidSurface(hit) ||
                    IsRoofNormal(hit.normal) ||
                    !IsUsefulHuntPerch(hit))
                {
                    continue;
                }

                Attach(hit.point, hit.normal, hit.collider != null ? hit.collider.transform : null);
                return true;
            }

            return false;
        }

        private bool MoveToward(Vector3 target, float speed, bool useControllerOnGround)
        {
            Vector3 delta = target - transform.position;
            float dist = delta.magnitude;
            if (dist <= 0.28f)
            {
                transform.position = target;
                return true;
            }

            Vector3 step = delta.normalized * (speed * Time.deltaTime);
            FacePoint(target);
            if (useControllerOnGround && !attached && controller != null)
            {
                controller.enabled = true;
                controller.Move(step + Vector3.up * (Gravity * Time.deltaTime * 0.02f));
            }
            else
            {
                if (controller != null)
                {
                    controller.enabled = false;
                }

                transform.position += step;
            }

            return Vector3.Distance(transform.position, target) <= 0.3f;
        }

        private void FacePoint(Vector3 point)
        {
            Vector3 flat = Vector3.ProjectOnPlane(point - transform.position, attached ? surfaceNormal : Vector3.up);
            if (flat.sqrMagnitude < 0.001f)
            {
                return;
            }

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(flat.normalized, attached ? surfaceNormal : Vector3.up),
                1f - Mathf.Exp(-10f * Time.deltaTime));
        }

        private void ApplyGroundGravity()
        {
            if (controller == null)
            {
                return;
            }

            controller.enabled = true;
            controller.Move(Vector3.up * (Gravity * Time.deltaTime));
        }

        private void SnapOntoGround()
        {
            Vector3 origin = transform.position + Vector3.up * 1.5f;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 4f, ~0, QueryTriggerInteraction.Ignore) &&
                hit.collider.GetComponentInParent<MiniVanPlayer>() == null)
            {
                transform.position = hit.point + Vector3.up * 0.28f;
            }

            if (controller != null)
            {
                controller.enabled = true;
            }
        }

        private void SetAnim(float speed, bool cling)
        {
            if (visualAnimator == null)
            {
                return;
            }

            visualAnimator.SetFloat(AnimSpeed, speed);
            visualAnimator.SetBool(AnimCling, cling);
        }

        private bool IsMovingForWalkAnim()
        {
            return controller != null && controller.velocity.sqrMagnitude > 0.04f;
        }

        private bool IsSeenByAnyPlayer()
        {
            // Sampling only around the pivot let the zombie vanish while half of the body still
            // stuck out past a ledge, so test the actual rendered volume instead.
            if (TryGetVisualBounds(out Bounds bounds))
            {
                Vector3 c = bounds.center;
                Vector3 e = bounds.extents * 0.85f;
                if (IsWorldPointSeenByAnyPlayer(c))
                {
                    return true;
                }

                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = c + new Vector3(
                        (i & 1) == 0 ? e.x : -e.x,
                        (i & 2) == 0 ? e.y : -e.y,
                        (i & 4) == 0 ? e.z : -e.z);
                    if (IsWorldPointSeenByAnyPlayer(corner))
                    {
                        return true;
                    }
                }

                return false;
            }

            Vector3 pos = transform.position;
            Vector3 n = attached ? surfaceNormal : Vector3.up;
            Vector3[] samples =
            {
                pos,
                pos + n * 0.35f,
                pos + Vector3.up * 0.4f,
                pos - Vector3.up * 0.15f + n * 0.15f
            };
            for (int i = 0; i < samples.Length; i++)
            {
                if (IsWorldPointSeenByAnyPlayer(samples[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryGetVisualBounds(out Bounds bounds)
        {
            bounds = default;
            if (visualRoot == null)
            {
                return false;
            }

            if (visualRenderers == null || Time.time >= nextVisualRendererScan)
            {
                visualRenderers = visualRoot.GetComponentsInChildren<Renderer>(true);
                nextVisualRendererScan = Time.time + 2f;
            }

            bool any = false;
            for (int i = 0; i < visualRenderers.Length; i++)
            {
                Renderer renderer = visualRenderers[i];
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
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

        private bool IsWorldPointSeenByAnyPlayer(Vector3 world)
        {
            MiniVanPlayer[] players = GetPlayers();
            for (int i = 0; i < players.Length; i++)
            {
                MiniVanPlayer player = players[i];
                if (player == null || !player.isActiveAndEnabled)
                {
                    continue;
                }

                if (IsWorldPointVisibleToPlayer(player, world))
                {
                    return true;
                }
            }

            Camera[] cameras = Camera.allCameras;
            for (int i = 0; i < cameras.Length; i++)
            {
                Camera cam = cameras[i];
                if (cam == null || !cam.enabled || !cam.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (IsWorldPointInCameraView(cam, world, null))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsWorldPointVisibleToPlayer(MiniVanPlayer player, Vector3 world)
        {
            Camera cam = player.PlayerCamera;
            if (cam != null && cam.enabled && cam.gameObject.activeInHierarchy)
            {
                if (IsWorldPointInCameraView(cam, world, player))
                {
                    return true;
                }
            }

            Transform view = player.CameraRoot != null ? player.CameraRoot : player.transform;
            Vector3 eye = view.position;
            Vector3 to = world - eye;
            float dist = to.magnitude;
            if (dist < 0.35f)
            {
                return true;
            }

            if (dist > 95f)
            {
                return false;
            }

            float alignment = Vector3.Dot(view.forward, to / dist);
            return alignment > 0.68f && HasLineOfSight(eye, world, player);
        }

        private bool IsWorldPointInCameraView(Camera cam, Vector3 world, MiniVanPlayer viewer)
        {
            Vector3 vp = cam.WorldToViewportPoint(world);
            bool inView = vp.z > 0.12f &&
                          vp.x > -0.14f && vp.x < 1.14f &&
                          vp.y > -0.14f && vp.y < 1.14f;
            return inView && HasLineOfSight(cam.transform.position, world, viewer);
        }

        private bool HasLineOfSight(Vector3 eye, Vector3 world, MiniVanPlayer viewer)
        {
            Vector3 delta = world - eye;
            float dist = delta.magnitude;
            if (dist < 0.12f)
            {
                return true;
            }

            int count = Physics.RaycastNonAlloc(eye, delta / dist, SurfaceHits, dist - 0.05f, ~0,
                QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            Collider nearestCollider = null;
            for (int i = 0; i < count; i++)
            {
                Collider col = SurfaceHits[i].collider;
                if (col == null)
                {
                    continue;
                }

                if (viewer != null && col.GetComponentInParent<MiniVanPlayer>() == viewer)
                {
                    continue;
                }

                if (SurfaceHits[i].distance >= nearest)
                {
                    continue;
                }

                nearest = SurfaceHits[i].distance;
                nearestCollider = col;
            }

            if (nearestCollider == null)
            {
                return true;
            }

            return nearestCollider.GetComponentInParent<MiniVanTraceZombie>() == this;
        }

        private void BeginTraceDeath(Vector3 hitOrigin, Vector3 impulse)
        {
            if (isDying || deathPartsSpawned)
            {
                return;
            }

            isDying = true;
            deathPartsSpawned = true;
            DetachFromSurface();
            if (IsSpawned && IsServer)
            {
                TraceDeathClientRpc(hitOrigin, impulse);
            }

            StartCoroutine(TraceDeathRoutine(hitOrigin, impulse, true));
        }

        [ClientRpc]
        private void TraceDeathClientRpc(Vector3 hitOrigin, Vector3 impulse)
        {
            if (IsServer)
            {
                return;
            }

            isDying = true;
            deathPartsSpawned = true;
            StartCoroutine(TraceDeathRoutine(hitOrigin, impulse, false));
        }

        private IEnumerator TraceDeathRoutine(Vector3 hitOrigin, Vector3 impulse, bool isAuthority)
        {
            if (controller != null)
            {
                controller.enabled = false;
            }

            if (visualAnimator != null)
            {
                visualAnimator.enabled = false;
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
            if (Physics.Raycast(startPosition + Vector3.up * 2f, Vector3.down, out RaycastHit ground, 6f,
                    ~0, QueryTriggerInteraction.Ignore))
            {
                groundY = ground.point.y;
            }

            float endY = groundY + 0.18f;
            Vector3 endPosition = new Vector3(startPosition.x, endY, startPosition.z);
            float fallSeconds = Mathf.Max(0.1f, DeathFallSeconds);
            float elapsed = 0f;
            while (elapsed < fallSeconds)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / fallSeconds);
                t *= t;
                transform.SetPositionAndRotation(
                    Vector3.Lerp(startPosition, endPosition, t),
                    Quaternion.Slerp(startRotation, endRotation, t));
                yield return null;
            }

            transform.SetPositionAndRotation(endPosition, endRotation);
            if (DeathLieSeconds > 0.001f)
            {
                yield return new WaitForSeconds(DeathLieSeconds);
            }

            SpawnTraceHeadLocal(transform.position + Vector3.up * 0.15f, impulse);
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

        private static void SpawnTraceHeadLocal(Vector3 position, Vector3 impulse)
        {
            GameObject root = MiniVanTraceHeadVisual.Create(null, true);
            root.name = "TraceHead_" + Time.frameCount;
            root.transform.position = position;
            root.transform.rotation = Random.rotation;

            MiniVanPizzaItem pickup = root.AddComponent<MiniVanPizzaItem>();
            pickup.Item = MiniVanInventoryItem.TraceHead;
            pickup.Type = MiniVanPizzaItemType.Ingredient;
            pickup.PickupRadius = 2.3f;
            pickup.CanHoldInHands = true;
            pickup.CanPutInInventory = true;

            SphereCollider hull = root.GetComponent<SphereCollider>();
            if (hull == null)
            {
                hull = root.AddComponent<SphereCollider>();
            }

            hull.radius = 0.22f;
            Rigidbody body = root.AddComponent<Rigidbody>();
            body.mass = 0.9f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.linearDamping = 0.2f;
            body.angularDamping = 0.4f;
            body.linearVelocity = impulse * 0.1f + Vector3.up * Random.Range(1.2f, 2.2f) +
                                  Random.insideUnitSphere * 0.7f;
            body.angularVelocity = Random.insideUnitSphere * 4f;
        }

        private void EnsureTraceVisuals()
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

            visualRoot = transform.Find("TraceZombie Visual");
            if (visualRoot == null && VisualPrefab != null)
            {
                GameObject visual = Instantiate(VisualPrefab, transform);
                visual.name = "TraceZombie Visual";
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                visual.transform.localScale = Vector3.one;
                DisableColliders(visual);
                visualRoot = visual.transform;
            }

            if (visualRoot == null)
            {
                GameObject placeholder = new GameObject("TraceZombie Visual");
                placeholder.transform.SetParent(transform, false);
                visualRoot = placeholder.transform;
            }

            visualAnimator = visualRoot.GetComponentInChildren<Animator>(true);
            CacheVisualRenderers();
            visualsReady = true;
        }

        private static MiniVanPlayer[] GetPlayers()
        {
            if (cachedPlayers == null || Time.time >= nextPlayerCacheTime)
            {
                cachedPlayers = FindObjectsByType<MiniVanPlayer>(FindObjectsSortMode.None);
                nextPlayerCacheTime = Time.time + 0.25f;
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
    }
}
