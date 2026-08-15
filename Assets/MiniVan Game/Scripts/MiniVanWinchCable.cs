using System.Collections.Generic;
using UnityEngine;

namespace MiniVanGame
{
    public sealed class MiniVanWinchCable : MonoBehaviour
    {
        private enum WinchState
        {
            FirstEndAttached,
            DroppedOpen,
            BothEndsAttached,
            Broken
        }

        private enum AnchorKind
        {
            None,
            Vehicle,
            Rigidbody,
            Static
        }

        public const float MaxCableLength = 15f;
        public const float ReelMetersPerSecond = 1.5f;
        public const float ReelDurabilitySeconds = 30f;
        private const int SegmentCount = 24;
        private const float SegmentLength = MaxCableLength / SegmentCount;
        private const float RopeRadius = 0.07f;
        private const float PointRadius = 0.11f;
        private const float SpringForce = 1500f;
        private const float SpringDamper = 86f;
        private const float PointMass = 0.55f;
        private const float EndpointMass = 2.4f;
        private const float PointLinearDamping = 0.9f;
        private const float PointAngularDamping = 1.35f;
        private const float PullCorrectionPerTick = 0.22f;
        private const float PullSpringForce = 2200f;
        private const float PullDamper = 140f;
        private const float PullMaxForce = 7800f;
        private const float BaseWearPerSecond = 0.00025f;
        private const float TensionWearPerSecond = 0.006f;
        private const float MassWearPerSecond = 0.000045f;
        private const float SpeedWearPerMeterPerSecond = 0.0006f;
        private const float AttachReach = 2.25f;
        private const float ObstacleAttachReach = 3.25f;
        private const float HandFollowHeight = 0.72f;
        private const float FoldReach = 3.25f;
        private const float FoldHoldSeconds = 1.5f;
        private const float MinSecondAnchorDistanceFromStart = 1.15f;
        private const float HeldEndFollowSpeed = 7.5f;
        private const float HeldEndSnapDistance = 4.5f;
        private const float MinDeployedLength = 1.25f;
        private const float HookSideMatchDistance = 1.6f;

        private static readonly Dictionary<MiniVanPlayer, MiniVanWinchCable> activeByPlayer =
            new Dictionary<MiniVanPlayer, MiniVanWinchCable>();
        private static readonly Dictionary<MiniVanPlayer, int> consumedUseFrame =
            new Dictionary<MiniVanPlayer, int>();
        private static readonly List<MiniVanWinchCable> allCables = new List<MiniVanWinchCable>(8);
        private static MiniVanPlayer foldingPlayer;
        private static MiniVanWinchCable foldingCable;
        private static float foldStartTime;

        private readonly List<Rigidbody> points = new List<Rigidbody>(SegmentCount + 1);
        private readonly List<Transform> connectors = new List<Transform>(SegmentCount);
        private readonly List<Collider> ropeColliders = new List<Collider>(SegmentCount + 1);
        private Material ropeMaterial;
        private Material endpointMaterial;
        private MiniVanPlayer owner;
        private WinchState state;
        private float durability01 = 1f;
        private float nextDurabilityPublishTime;

        private AnchorKind startKind;
        private MiniVanVehicle startVehicle;
        private Rigidbody startBody;
        private Vector3 startLocalAnchor;
        private Vector3 startStaticAnchor;

        private AnchorKind endKind;
        private MiniVanVehicle endVehicle;
        private Rigidbody endBody;
        private Vector3 endLocalAnchor;
        private Vector3 endStaticAnchor;

        private float deployedLength = MaxCableLength;
        private float reelHoldUntil;
        private bool reelHoldIn = true;
        private readonly List<SpringJoint> segmentSprings = new List<SpringJoint>(SegmentCount);

        public float Durability01 => durability01;
        public float DeployedLength => deployedLength;

        public bool IsVehicleEndInReach(Vector3 position)
        {
            return IsStartEndInReach(position) || IsEndEndInReach(position);
        }

        public bool IsObstacleEndInReach(Vector3 position)
        {
            return IsStartEndInReach(position) || IsEndEndInReach(position);
        }

        public static bool TryUseSelectedWinch(MiniVanPlayer player, float startingDurability)
        {
            if (player == null)
            {
                return false;
            }

            if (WasUseConsumedThisFrame(player))
            {
                return true;
            }

            if (!activeByPlayer.TryGetValue(player, out MiniVanWinchCable active) || active == null)
            {
                // Prefer interacting with an already-deployed nearby cable (attached/dropped).
                MiniVanWinchCable nearby = FindFoldableCable(player);
                if (nearby != null)
                {
                    if (nearby.state == WinchState.DroppedOpen)
                    {
                        return nearby.TryGrabDroppedFreeEnd(player);
                    }

                    if (nearby.state == WinchState.BothEndsAttached)
                    {
                        bool startInReach = nearby.IsStartEndInReach(player.transform.position);
                        bool endInReach = nearby.IsEndEndInReach(player.transform.position);
                        if (startInReach || endInReach)
                        {
                            return nearby.TakeAttachedEndIntoHands(player, startInReach && !endInReach);
                        }
                    }

                    if (nearby.state == WinchState.FirstEndAttached && nearby.owner == player)
                    {
                        return nearby.TryAttachSecondEndOnly(player);
                    }
                }

                if (!IsSelectedInventoryItemWinch(player))
                {
                    return false;
                }

                if (TryFindNearestVehicleAnchor(player.transform.position, out MiniVanVehicle vehicle, out Rigidbody vehicleBody, out Vector3 vehicleAnchor) &&
                    Vector3.Distance(player.transform.position, vehicleAnchor) <= AttachReach)
                {
                    CreateCable(player, AnchorKind.Vehicle, vehicle, vehicleBody, vehicleAnchor, Mathf.Clamp01(startingDurability));
                    MarkUseConsumed(player);
                    return true;
                }

                if (TryFindObstacleAnchor(player, null, out Rigidbody body, out Vector3 obstacleAnchor))
                {
                    CreateCable(player, body != null ? AnchorKind.Rigidbody : AnchorKind.Static, null, body, obstacleAnchor, Mathf.Clamp01(startingDurability));
                    MarkUseConsumed(player);
                    return true;
                }

                return false;
            }

            if (active.state == WinchState.FirstEndAttached)
            {
                return active.TryAttachSecondEndOnly(player);
            }

            if (active.state == WinchState.BothEndsAttached)
            {
                bool startInReach = active.IsStartEndInReach(player.transform.position);
                bool endInReach = active.IsEndEndInReach(player.transform.position);
                if (startInReach || endInReach)
                {
                    return active.TakeAttachedEndIntoHands(player, startInReach && !endInReach);
                }
            }

            return false;
        }

        private static bool IsSelectedInventoryItemWinch(MiniVanPlayer player)
        {
            return player != null && player.GameModeSelectedItem == MiniVanInventoryItem.Winch;
        }

        public static bool HasActiveCable(MiniVanPlayer player)
        {
            return player != null &&
                   activeByPlayer.TryGetValue(player, out MiniVanWinchCable active) &&
                   active != null &&
                   active.state == WinchState.FirstEndAttached;
        }

        public static float GetDurabilityFor(MiniVanPlayer player)
        {
            return player != null && activeByPlayer.TryGetValue(player, out MiniVanWinchCable active) && active != null
                ? active.Durability01
                : 1f;
        }

        public static float GetFoldProgress01(MiniVanPlayer player)
        {
            if (player == null || foldingPlayer != player || foldingCable == null)
            {
                return 0f;
            }

            return Mathf.Clamp01((Time.time - foldStartTime) / FoldHoldSeconds);
        }

        public static bool TryDropHeldCable(MiniVanPlayer player)
        {
            if (player == null ||
                !activeByPlayer.TryGetValue(player, out MiniVanWinchCable active) ||
                active == null)
            {
                return false;
            }

            return active.DropHeldEndToGround(player);
        }

        public static Vector3 ClampPlayerDeltaWhileHoldingFreeEnd(MiniVanPlayer player, Vector3 delta)
        {
            if (player == null ||
                delta.sqrMagnitude <= 0.000001f ||
                !activeByPlayer.TryGetValue(player, out MiniVanWinchCable active) ||
                active == null ||
                active.state != WinchState.FirstEndAttached)
            {
                return delta;
            }

            return active.ClampOwnerDeltaToCableLength(delta);
        }

        /// <summary>
        /// While the rider holds a free winch end, keep a board Rigidbody within deployed cable length.
        /// </summary>
        public static void ConstrainBoardWhileHoldingFreeEnd(MiniVanPlayer player, Rigidbody boardBody)
        {
            if (player == null ||
                boardBody == null ||
                !activeByPlayer.TryGetValue(player, out MiniVanWinchCable active) ||
                active == null ||
                active.state != WinchState.FirstEndAttached)
            {
                return;
            }

            active.ConstrainOwnerBoardToCableLength(boardBody);
        }

        public static void UpdateFoldInput(MiniVanPlayer player, bool foldHeld)
        {
            if (!foldHeld || player == null || WasUseConsumedThisFrame(player))
            {
                ResetFoldProgress();
                return;
            }

            MiniVanWinchCable target = FindFoldableCable(player);
            if (target == null)
            {
                ResetFoldProgress();
                return;
            }

            // Fold only while holding a free end or a dropped cable — not while both ends are locked.
            if (target.state == WinchState.BothEndsAttached)
            {
                ResetFoldProgress();
                return;
            }

            if (foldingPlayer != player || foldingCable != target)
            {
                foldingPlayer = player;
                foldingCable = target;
                foldStartTime = Time.time;
                return;
            }

            if (Time.time - foldStartTime >= FoldHoldSeconds)
            {
                target.FoldToInventory(player);
                ResetFoldProgress();
            }
        }

        private static void CreateCable(MiniVanPlayer player, AnchorKind kind, MiniVanVehicle vehicle, Rigidbody body, Vector3 anchor, float durability)
        {
            GameObject root = new GameObject("Deployed_Winch_Cable");
            MiniVanWinchCable cable = root.AddComponent<MiniVanWinchCable>();
            cable.Initialize(player, kind, vehicle, body, anchor, durability);
            activeByPlayer[player] = cable;
            RegisterCable(cable);
            player.HideWinchWhileDeployed();
        }

        public static bool CanReel(MiniVanVehicle vehicle, bool frontSide, bool reelIn)
        {
            if (vehicle == null || !vehicle.HasCarBatteryInstalled())
            {
                return false;
            }

            MiniVanWinchCable cable = FindCableForVehicleSide(vehicle, frontSide);
            if (cable == null)
            {
                return false;
            }

            if (reelIn)
            {
                return cable.deployedLength > MinDeployedLength + 0.01f;
            }

            return cable.deployedLength < MaxCableLength - 0.01f;
        }

        public static bool TryHoldReel(MiniVanVehicle vehicle, bool frontSide, bool reelIn)
        {
            if (!CanReel(vehicle, frontSide, reelIn))
            {
                return false;
            }

            MiniVanWinchCable cable = FindCableForVehicleSide(vehicle, frontSide);
            if (cable == null)
            {
                return false;
            }

            cable.reelHoldUntil = Time.time + 0.12f;
            cable.reelHoldIn = reelIn;
            return true;
        }

        public static bool CanFoldNear(MiniVanPlayer player)
        {
            return FindFoldableCable(player) != null;
        }

        public static MiniVanWinchCable FindCableForVehicleSide(MiniVanVehicle vehicle, bool frontSide)
        {
            if (vehicle == null)
            {
                return null;
            }

            for (int i = 0; i < allCables.Count; i++)
            {
                MiniVanWinchCable cable = allCables[i];
                if (cable != null && cable.IsAttachedToVehicleSide(vehicle, frontSide))
                {
                    return cable;
                }
            }

            return null;
        }

        private static void RegisterCable(MiniVanWinchCable cable)
        {
            if (cable != null && !allCables.Contains(cable))
            {
                allCables.Add(cable);
            }
        }

        private static void UnregisterCable(MiniVanWinchCable cable)
        {
            allCables.Remove(cable);
        }

        private void Initialize(MiniVanPlayer player, AnchorKind kind, MiniVanVehicle vehicle, Rigidbody body, Vector3 anchor, float startingDurability)
        {
            owner = player;
            durability01 = Mathf.Clamp01(startingDurability);
            state = WinchState.FirstEndAttached;
            SetStartAnchor(kind, vehicle, body, anchor);

            Vector3 hand = GetHandPosition();

            BuildMaterials();
            BuildRope(anchor, GetClampedFreeEnd(anchor, hand));
            IgnoreOwnerCollision();
            IgnoreVehicleCollision(startVehicle);
            IgnoreAttachedBodyCollision(startBody);
            ConfigureEndpointBody(0, startKind, startBody, false);
            SetEndpointColliderTrigger(SegmentCount, true);
            PublishDurability(true);
        }

        private void Update()
        {
            UpdateAnchorPositions();

            if (state == WinchState.FirstEndAttached)
            {
                FollowPlayerHandWithFreeEnd();
            }

            UpdateConnectors();
        }

        private void FixedUpdate()
        {
            if (state != WinchState.BothEndsAttached)
            {
                return;
            }

            bool reeling = Time.time <= reelHoldUntil;
            if (reeling)
            {
                MiniVanVehicle poweredVehicle = startKind == AnchorKind.Vehicle ? startVehicle :
                    endKind == AnchorKind.Vehicle ? endVehicle : null;
                if (poweredVehicle != null && !poweredVehicle.HasCarBatteryInstalled())
                {
                    reelHoldUntil = 0f;
                    reeling = false;
                }
                else if (reelHoldIn)
                {
                    ApplyReelIn(Time.fixedDeltaTime);
                }
                else
                {
                    ApplyReelOut(Time.fixedDeltaTime);
                }
            }

            ApplyPullPhysics();
            ApplyWear(reeling && reelHoldIn);
        }

        private bool TryAttachSecondEnd()
        {
            if (owner == null || points.Count == 0)
            {
                return false;
            }

            if (Vector3.Distance(owner.transform.position, GetStartAnchorWorld()) < MinSecondAnchorDistanceFromStart)
            {
                return false;
            }

            if (TryFindObstacleAnchor(owner, this, out Rigidbody body, out Vector3 obstacleAnchor) &&
                Vector3.Distance(GetStartAnchorWorld(), obstacleAnchor) <= MaxCableLength)
            {
                AttachEndAnchor(body != null ? AnchorKind.Rigidbody : AnchorKind.Static, null, body, obstacleAnchor);
                return true;
            }

            if (TryFindNearestVehicleAnchor(owner.transform.position, this, out MiniVanVehicle vehicle, out Rigidbody vehicleBody, out Vector3 vehicleAnchor) &&
                Vector3.Distance(GetStartAnchorWorld(), vehicleAnchor) <= MaxCableLength)
            {
                AttachEndAnchor(AnchorKind.Vehicle, vehicle, vehicleBody, vehicleAnchor);
                return true;
            }

            return false;
        }

        private bool TryAttachSecondEndOnly(MiniVanPlayer player)
        {
            if (player == null || state != WinchState.FirstEndAttached || owner != player)
            {
                return false;
            }

            if (!TryAttachSecondEnd())
            {
                return false;
            }

            MarkUseConsumed(player);
            return true;
        }

        private bool TakeAttachedEndIntoHands(MiniVanPlayer player, bool takeStartEnd)
        {
            if (player == null || state != WinchState.BothEndsAttached)
            {
                return false;
            }

            if (takeStartEnd && !IsStartEndInReach(player.transform.position))
            {
                return false;
            }

            if (!takeStartEnd && !IsEndEndInReach(player.transform.position))
            {
                return false;
            }

            if (takeStartEnd)
            {
                SwapAnchors();
            }

            owner = player;
            state = WinchState.FirstEndAttached;
            activeByPlayer[player] = this;
            ClearEndAnchor();
            SetEndpointColliderTrigger(points.Count - 1, true);
            IgnoreOwnerCollision();
            ConfigureEndpointBody(0, startKind, startBody, false);
            ConfigureEndpointBody(points.Count - 1, AnchorKind.None, null, false);
            player.HideWinchWhileDeployed();
            player.SetWinchDurabilityFromWorld(durability01);
            MarkUseConsumed(player);
            return true;
        }

        private Vector3 ClampOwnerDeltaToCableLength(Vector3 delta)
        {
            Vector3 start = GetStartAnchorWorld();
            Vector3 currentHand = GetHandPosition();
            Vector3 nextHand = currentHand + delta;
            Vector3 nextOffset = nextHand - start;
            const float slack = 0.08f;
            float maxLength = Mathf.Max(0.1f, GetHeldReachLength() - slack);
            if (nextOffset.magnitude <= maxLength)
            {
                return delta;
            }

            Vector3 currentOffset = currentHand - start;
            Vector3 direction = nextOffset.sqrMagnitude > 0.0001f
                ? nextOffset.normalized
                : currentOffset.sqrMagnitude > 0.0001f
                    ? currentOffset.normalized
                    : Vector3.forward;
            Vector3 clampedHand = start + direction * maxLength;
            return clampedHand - currentHand;
        }

        private void ConstrainOwnerBoardToCableLength(Rigidbody boardBody)
        {
            if (boardBody == null || !MiniVanTowRopeUtility.IsFinite(boardBody.position))
            {
                return;
            }

            Vector3 start = GetStartAnchorWorld();
            if (!MiniVanTowRopeUtility.IsFinite(start))
            {
                return;
            }

            // Use the board itself (server-authoritative), not the networked rider pose.
            Vector3 heldPoint = boardBody.position + Vector3.up * HandFollowHeight;
            Vector3 offset = heldPoint - start;
            float distance = offset.magnitude;
            if (distance <= 0.001f)
            {
                return;
            }

            const float slack = 0.08f;
            float maxLength = Mathf.Max(0.1f, GetHeldReachLength() - slack);
            Vector3 outward = offset / distance;

            if (distance >= maxLength - 0.05f)
            {
                float velocityAway = Vector3.Dot(boardBody.linearVelocity, outward);
                if (velocityAway > 0f)
                {
                    boardBody.linearVelocity -= outward * velocityAway;
                }
            }

            if (distance <= maxLength)
            {
                return;
            }

            float correction = Mathf.Min(distance - maxLength, 0.45f);
            Transform ignoredRoot = null;
            if (startVehicle != null)
            {
                ignoredRoot = startVehicle.transform.root;
            }
            else if (startBody != null)
            {
                ignoredRoot = startBody.transform;
            }

            MiniVanTowRopeUtility.MoveBodyWithSliding(
                boardBody,
                -outward * correction,
                boardBody.transform,
                ignoredRoot,
                0.025f,
                12,
                10f);
        }

        private float GetHeldReachLength()
        {
            return Mathf.Clamp(deployedLength, MinDeployedLength, MaxCableLength);
        }

        private void AttachEndAnchor(AnchorKind kind, MiniVanVehicle vehicle, Rigidbody body, Vector3 anchor)
        {
            SetEndAnchor(kind, vehicle, body, anchor);
            Rigidbody endPoint = points[points.Count - 1];
            endPoint.position = anchor;
            if (!endPoint.isKinematic)
            {
                endPoint.linearVelocity = Vector3.zero;
                endPoint.angularVelocity = Vector3.zero;
            }

            state = WinchState.BothEndsAttached;
            float span = Vector3.Distance(GetStartAnchorWorld(), GetEndAnchorWorld());
            deployedLength = Mathf.Clamp(span + 0.15f, MinDeployedLength, MaxCableLength);
            UpdateSegmentSpringLengths();
            ClearActive();
            IgnoreVehicleCollision(startVehicle);
            IgnoreVehicleCollision(endVehicle);
            IgnoreAttachedBodyCollision(startBody);
            IgnoreAttachedBodyCollision(endBody);
            ConfigureEndpointBody(0, startKind, startBody, true);
            ConfigureEndpointBody(points.Count - 1, endKind, endBody, true);
        }

        public bool IsAttachedToVehicleSide(MiniVanVehicle vehicle, bool frontSide)
        {
            if (vehicle == null || state != WinchState.BothEndsAttached)
            {
                return false;
            }

            if (!TryGetVehicleHookAnchor(vehicle, frontSide, out Vector3 hook))
            {
                return false;
            }

            if (startKind == AnchorKind.Vehicle && startVehicle == vehicle &&
                Vector3.Distance(GetStartAnchorWorld(), hook) <= HookSideMatchDistance)
            {
                return true;
            }

            if (endKind == AnchorKind.Vehicle && endVehicle == vehicle &&
                Vector3.Distance(GetEndAnchorWorld(), hook) <= HookSideMatchDistance)
            {
                return true;
            }

            return false;
        }

        private void ApplyReelIn(float deltaTime)
        {
            if (deltaTime <= 0f || deployedLength <= MinDeployedLength + 0.001f)
            {
                return;
            }

            float shorten = ReelMetersPerSecond * deltaTime;
            deployedLength = Mathf.Max(MinDeployedLength, deployedLength - shorten);
            UpdateSegmentSpringLengths();

            float reelWear = deltaTime / Mathf.Max(0.1f, ReelDurabilitySeconds);
            durability01 = Mathf.Clamp01(durability01 - reelWear);
            PublishDurability(false);
            if (durability01 <= 0.001f)
            {
                BreakCable();
            }
        }

        private void ApplyReelOut(float deltaTime)
        {
            if (deltaTime <= 0f || deployedLength >= MaxCableLength - 0.001f)
            {
                return;
            }

            float extend = ReelMetersPerSecond * deltaTime;
            deployedLength = Mathf.Min(MaxCableLength, deployedLength + extend);
            UpdateSegmentSpringLengths();
        }

        private void UpdateSegmentSpringLengths()
        {
            float segment = Mathf.Max(0.04f, deployedLength / SegmentCount);
            for (int i = 0; i < segmentSprings.Count; i++)
            {
                SpringJoint spring = segmentSprings[i];
                if (spring == null)
                {
                    continue;
                }

                spring.minDistance = segment * 0.82f;
                spring.maxDistance = segment;
            }
        }

        private static bool TryFindObstacleAnchor(MiniVanPlayer player, MiniVanWinchCable activeCable, out Rigidbody body, out Vector3 anchor)
        {
            body = null;
            anchor = default;
            if (player == null)
            {
                return false;
            }

            if (player.PlayerCamera != null)
            {
                Ray ray = new Ray(player.PlayerCamera.transform.position, player.PlayerCamera.transform.forward);
                if (TryFindObstacleFromHits(Physics.RaycastAll(ray, ObstacleAttachReach, ~0, QueryTriggerInteraction.Ignore), player, activeCable, out body, out anchor))
                {
                    return true;
                }

                if (TryFindObstacleFromHits(Physics.SphereCastAll(ray, 0.35f, ObstacleAttachReach, ~0, QueryTriggerInteraction.Ignore), player, activeCable, out body, out anchor))
                {
                    return true;
                }
            }

            return TryFindNearestObstacle(player, activeCable, out body, out anchor);
        }

        private static bool TryFindObstacleFromHits(RaycastHit[] hits, MiniVanPlayer player, MiniVanWinchCable activeCable, out Rigidbody body, out Vector3 anchor)
        {
            body = null;
            anchor = default;
            if (hits == null || hits.Length == 0)
            {
                return false;
            }

            System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
            for (int i = 0; i < hits.Length; i++)
            {
                Collider hitCollider = hits[i].collider;
                if (!CanAttachToObstacleCollider(hitCollider, player, activeCable, true))
                {
                    continue;
                }

                // Installed screw-eye hooks are fixed world anchors — never attach as a
                // dynamic Rigidbody (cable completion wakes RBs and the hook falls off).
                if (TryGetInstalledWinchHookAnchor(hitCollider, out Vector3 hookAnchor))
                {
                    if (activeCable != null &&
                        !activeCable.IsValidSecondAnchor(AnchorKind.Static, null, null, hookAnchor))
                    {
                        continue;
                    }

                    body = null;
                    anchor = hookAnchor;
                    return true;
                }

                Rigidbody hitBody = hitCollider.attachedRigidbody;
                Vector3 hitAnchor = hits[i].point;
                if (activeCable != null &&
                    !activeCable.IsValidSecondAnchor(hitBody != null ? AnchorKind.Rigidbody : AnchorKind.Static, null, hitBody, hitAnchor))
                {
                    continue;
                }

                body = hitBody;
                anchor = hitAnchor;
                return true;
            }

            return false;
        }

        private static bool TryFindNearestObstacle(MiniVanPlayer player, MiniVanWinchCable activeCable, out Rigidbody body, out Vector3 anchor)
        {
            body = null;
            anchor = default;
            Vector3 origin = player.transform.position + Vector3.up * HandFollowHeight;
            Collider[] nearby = Physics.OverlapSphere(origin, ObstacleAttachReach, ~0, QueryTriggerInteraction.Ignore);
            Collider bestCollider = null;
            Vector3 bestPoint = Vector3.zero;
            float bestDistance = float.MaxValue;

            for (int pass = 0; pass < 2; pass++)
            {
                bool rigidbodiesOnly = pass == 0;
                for (int i = 0; i < nearby.Length; i++)
                {
                    Collider candidate = nearby[i];
                    if (!CanAttachToObstacleCollider(candidate, player, activeCable, !rigidbodiesOnly))
                    {
                        continue;
                    }

                    bool installedHook = TryGetInstalledWinchHookAnchor(candidate, out Vector3 hookAnchor);
                    if (rigidbodiesOnly && candidate.attachedRigidbody == null && !installedHook)
                    {
                        continue;
                    }

                    Vector3 point = installedHook ? hookAnchor : candidate.ClosestPoint(origin);
                    Rigidbody candidateBody = installedHook ? null : candidate.attachedRigidbody;
                    if (activeCable != null &&
                        !activeCable.IsValidSecondAnchor(candidateBody != null ? AnchorKind.Rigidbody : AnchorKind.Static, null, candidateBody, point))
                    {
                        continue;
                    }

                    float distance = Vector3.Distance(origin, point);
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        bestCollider = candidate;
                        bestPoint = point;
                    }
                }

                if (bestCollider != null)
                {
                    if (TryGetInstalledWinchHookAnchor(bestCollider, out Vector3 installedAnchor))
                    {
                        body = null;
                        anchor = installedAnchor;
                    }
                    else
                    {
                        body = bestCollider.attachedRigidbody;
                        anchor = bestPoint;
                    }

                    return true;
                }
            }

            return false;
        }

        private bool IsValidSecondAnchor(AnchorKind kind, MiniVanVehicle vehicle, Rigidbody body, Vector3 anchor)
        {
            Vector3 startAnchor = GetStartAnchorWorld();
            if (Vector3.Distance(startAnchor, anchor) < MinSecondAnchorDistanceFromStart)
            {
                return false;
            }

            if (kind == startKind)
            {
                if (kind == AnchorKind.Vehicle && vehicle != null && vehicle == startVehicle)
                {
                    return false;
                }

                if (kind == AnchorKind.Rigidbody && body != null && body == startBody)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool CanAttachToObstacleCollider(Collider hitCollider, MiniVanPlayer player, MiniVanWinchCable activeCable, bool allowStatic)
        {
            if (hitCollider == null || hitCollider.isTrigger)
            {
                return false;
            }

            if (activeCable != null && hitCollider.transform.IsChildOf(activeCable.transform))
            {
                return false;
            }

            if (player != null && hitCollider.GetComponentInParent<MiniVanPlayer>() != null)
            {
                return false;
            }

            if (hitCollider.GetComponentInParent<MiniVanVehicle>() != null)
            {
                return false;
            }

            if (hitCollider.GetComponentInParent<MiniVanWinchPickup>() != null)
            {
                return false;
            }

            if (hitCollider.attachedRigidbody != null)
            {
                return true;
            }

            return allowStatic && !LooksLikeGround(hitCollider);
        }

        private static bool LooksLikeGround(Collider collider)
        {
            string n = collider.name.ToLowerInvariant();
            string root = collider.transform.root != null ? collider.transform.root.name.ToLowerInvariant() : string.Empty;
            return n.Contains("ground") || n.Contains("terrain") || n.Contains("road") ||
                   root.Contains("ground") || root.Contains("terrain") || root.Contains("road");
        }

        private void ReturnToInventory()
        {
            if (owner != null)
            {
                owner.TryRestoreWinchFromWorld(durability01);
            }

            ClearActive();
            Destroy(gameObject);
        }

        private bool DropHeldEndToGround(MiniVanPlayer player)
        {
            if (player == null || owner != player || state != WinchState.FirstEndAttached || points.Count == 0)
            {
                return false;
            }

            Rigidbody freeEnd = points[points.Count - 1];
            if (freeEnd != null)
            {
                freeEnd.isKinematic = false;
                freeEnd.useGravity = true;
                freeEnd.linearVelocity = Vector3.zero;
                freeEnd.angularVelocity = Vector3.zero;
                freeEnd.WakeUp();
                SetEndpointColliderTrigger(points.Count - 1, false);
            }

            for (int i = 1; i < points.Count - 1; i++)
            {
                if (points[i] != null)
                {
                    points[i].WakeUp();
                }
            }

            state = WinchState.DroppedOpen;
            ClearActive();
            owner = null;
            return true;
        }

        private bool TryGrabDroppedFreeEnd(MiniVanPlayer player)
        {
            if (player == null || state != WinchState.DroppedOpen || points.Count == 0)
            {
                return false;
            }

            Rigidbody freeEnd = points[points.Count - 1];
            if (freeEnd == null || Vector3.Distance(player.transform.position, freeEnd.position) > AttachReach)
            {
                return false;
            }

            owner = player;
            state = WinchState.FirstEndAttached;
            activeByPlayer[player] = this;
            SetEndpointColliderTrigger(points.Count - 1, true);
            IgnoreOwnerCollision();
            player.HideWinchWhileDeployed();
            player.SetWinchDurabilityFromWorld(durability01);
            MarkUseConsumed(player);
            return true;
        }

        private void FoldToInventory(MiniVanPlayer player)
        {
            if (player == null)
            {
                return;
            }

            player.TryRestoreWinchFromWorld(durability01);
            ClearActive();
            Destroy(gameObject);
        }

        private void BreakCable()
        {
            state = WinchState.Broken;
            if (owner != null)
            {
                owner.NotifyWinchBroken();
            }

            for (int i = 0; i < points.Count; i++)
            {
                if (points[i] == null)
                {
                    continue;
                }

                foreach (Joint joint in points[i].GetComponents<Joint>())
                {
                    Destroy(joint);
                }
            }

            ClearActive();
            Destroy(gameObject, 4f);
        }

        private void BuildRope(Vector3 start, Vector3 end)
        {
            ropeColliders.Clear();

            for (int i = 0; i <= SegmentCount; i++)
            {
                float t = (float)i / SegmentCount;
                Vector3 position = Vector3.Lerp(start, end, t);
                GameObject point = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                point.name = i == 0 ? "Winch Start End" : i == SegmentCount ? "Winch Free End" : "Winch Point " + i.ToString("00");
                point.transform.SetParent(transform, true);
                point.transform.position = position;
                point.transform.localScale = Vector3.one * PointRadius * 2f;
                SetMaterial(point, i == 0 || i == SegmentCount ? endpointMaterial : ropeMaterial);
                Collider pointCollider = point.GetComponent<Collider>();
                if (pointCollider != null)
                {
                    pointCollider.isTrigger = false;
                    ropeColliders.Add(pointCollider);
                }

                Rigidbody pointBody = point.AddComponent<Rigidbody>();
                pointBody.mass = i == 0 || i == SegmentCount ? EndpointMass : PointMass;
                pointBody.useGravity = true;
                pointBody.linearDamping = PointLinearDamping;
                pointBody.angularDamping = PointAngularDamping;
                pointBody.interpolation = RigidbodyInterpolation.Interpolate;
                pointBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                pointBody.solverIterations = 12;
                pointBody.solverVelocityIterations = 6;
                pointBody.maxLinearVelocity = 80f;
                points.Add(pointBody);

                if (i > 0)
                {
                    SpringJoint spring = pointBody.gameObject.AddComponent<SpringJoint>();
                    spring.connectedBody = points[i - 1];
                    spring.autoConfigureConnectedAnchor = false;
                    spring.anchor = Vector3.zero;
                    spring.connectedAnchor = Vector3.zero;
                    spring.spring = SpringForce;
                    spring.damper = SpringDamper;
                    spring.minDistance = SegmentLength;
                    spring.maxDistance = SegmentLength;
                    spring.tolerance = 0.03f;
                    spring.enableCollision = false;
                    spring.breakForce = Mathf.Infinity;
                    segmentSprings.Add(spring);
                }
            }

            for (int i = 0; i < SegmentCount; i++)
            {
                GameObject connector = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                connector.name = "Winch Part " + i.ToString("00");
                connector.transform.SetParent(transform, true);
                SetMaterial(connector, ropeMaterial);
                Collider collider = connector.GetComponent<Collider>();
                if (collider != null)
                {
                    Destroy(collider);
                }

                connectors.Add(connector.transform);
            }

            points[0].gameObject.AddComponent<MiniVanWinchEndpoint>().Configure(this, true);
            points[SegmentCount].gameObject.AddComponent<MiniVanWinchEndpoint>().Configure(this, false);
        }

        private void ConfigureEndpointBody(int index, AnchorKind kind, Rigidbody attachedBody, bool cableCompleted)
        {
            Rigidbody endpoint = points.Count > index ? points[index] : null;
            if (endpoint == null)
            {
                return;
            }

            foreach (FixedJoint joint in endpoint.GetComponents<FixedJoint>())
            {
                Destroy(joint);
            }

            endpoint.isKinematic = true;
            endpoint.useGravity = false;
            endpoint.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            SetEndpointColliderTrigger(index, true);

            if (cableCompleted && kind == AnchorKind.Rigidbody && attachedBody != null)
            {
                MiniVanWinchHook installedHook = attachedBody.GetComponentInParent<MiniVanWinchHook>();
                if (installedHook != null && installedHook.IsInstalled)
                {
                    // Permanently fixed screw — keep kinematic so the cable cannot knock it loose.
                    attachedBody.isKinematic = true;
                    attachedBody.useGravity = false;
                    return;
                }

                attachedBody.isKinematic = false;
                attachedBody.useGravity = true;
                attachedBody.WakeUp();
            }
        }

        private static bool TryGetInstalledWinchHookAnchor(Collider hitCollider, out Vector3 anchor)
        {
            anchor = default;
            MiniVanWinchHook hook = hitCollider != null
                ? hitCollider.GetComponentInParent<MiniVanWinchHook>()
                : null;
            if (hook == null || !hook.IsInstalled)
            {
                return false;
            }

            anchor = hook.CableAnchorWorld;
            return true;
        }

        private void IgnoreOwnerCollision()
        {
            if (owner == null)
            {
                return;
            }

            Collider[] ownerColliders = owner.GetComponentsInChildren<Collider>(true);
            IgnoreColliders(ownerColliders);
        }

        private void IgnoreVehicleCollision(MiniVanVehicle vehicle)
        {
            if (vehicle == null)
            {
                return;
            }

            Collider[] vehicleColliders = vehicle.GetComponentsInChildren<Collider>(true);
            IgnoreColliders(vehicleColliders);
        }

        private void IgnoreAttachedBodyCollision(Rigidbody body)
        {
            if (body == null)
            {
                return;
            }

            Collider[] bodyColliders = body.GetComponentsInChildren<Collider>(true);
            IgnoreColliders(bodyColliders);
        }

        private void IgnoreColliders(Collider[] ignoredColliders)
        {
            if (ignoredColliders == null || ignoredColliders.Length == 0)
            {
                return;
            }

            for (int i = 0; i < ropeColliders.Count; i++)
            {
                Collider ropeCollider = ropeColliders[i];
                if (ropeCollider == null)
                {
                    continue;
                }

                for (int j = 0; j < ignoredColliders.Length; j++)
                {
                    Collider ignored = ignoredColliders[j];
                    if (ignored != null && ignored != ropeCollider)
                    {
                        Physics.IgnoreCollision(ropeCollider, ignored, true);
                    }
                }
            }
        }

        private void FollowPlayerHandWithFreeEnd()
        {
            Rigidbody endPoint = points.Count > 0 ? points[points.Count - 1] : null;
            if (endPoint == null)
            {
                return;
            }

            Vector3 start = GetStartAnchorWorld();
            Vector3 hand = GetHandPosition();
            Vector3 target = GetClampedFreeEnd(start, hand);
            endPoint.isKinematic = true;
            endPoint.useGravity = false;
            SetEndpointColliderTrigger(points.Count - 1, true);
            float follow = 1f - Mathf.Exp(-HeldEndFollowSpeed * Time.deltaTime);
            Vector3 next = Vector3.Lerp(endPoint.position, target, follow);
            if (Vector3.Distance(next, target) > HeldEndSnapDistance)
            {
                next = target;
            }

            endPoint.position = next;
        }

        private Vector3 GetClampedFreeEnd(Vector3 start, Vector3 target)
        {
            Vector3 offset = target - start;
            float maxLength = GetHeldReachLength();
            if (offset.magnitude <= maxLength)
            {
                return target;
            }

            return start + offset.normalized * maxLength;
        }

        private Vector3 GetHandPosition()
        {
            if (owner == null)
            {
                return GetStartAnchorWorld() + Vector3.forward * 1.4f;
            }

            Transform cameraRoot = owner.CameraRoot != null ? owner.CameraRoot : owner.transform;
            Vector3 forward = cameraRoot.forward.sqrMagnitude > 0.001f ? cameraRoot.forward.normalized : owner.transform.forward;
            return owner.transform.position + Vector3.up * HandFollowHeight + forward * 0.75f;
        }

        private void UpdateAnchorPositions()
        {
            if (points.Count == 0)
            {
                return;
            }

            UpdateEndpointPosition(0, startKind, GetStartAnchorWorld());

            if (state == WinchState.BothEndsAttached)
            {
                UpdateEndpointPosition(points.Count - 1, endKind, GetEndAnchorWorld());
            }
        }

        private void UpdateEndpointPosition(int index, AnchorKind kind, Vector3 anchor)
        {
            Rigidbody endpoint = points[index];
            if (endpoint == null)
            {
                return;
            }

            endpoint.isKinematic = true;
            endpoint.useGravity = false;
            endpoint.MovePosition(anchor);
        }

        private void ApplyPullPhysics()
        {
            if (state != WinchState.BothEndsAttached)
            {
                return;
            }

            Vector3 startAnchor = GetStartAnchorWorld();
            Vector3 endAnchor = GetEndAnchorWorld();
            Vector3 span = endAnchor - startAnchor;
            float distance = span.magnitude;
            float softLimit = Mathf.Max(0.2f, deployedLength * 0.98f);
            if (!MiniVanTowRopeUtility.IsFinite(distance) || distance <= softLimit || distance <= 0.001f)
            {
                return;
            }

            Vector3 direction = span / distance;
            if (!MiniVanTowRopeUtility.IsFinite(direction))
            {
                return;
            }

            ResolveMovableEnd(startKind, startVehicle, startBody, out Rigidbody startMovable, out float startMass, out Transform startIgnore);
            ResolveMovableEnd(endKind, endVehicle, endBody, out Rigidbody endMovable, out float endMass, out Transform endIgnore);

            // Pull the lighter (or only movable) body toward the other end.
            Rigidbody pullBody = null;
            Vector3 pullAnchor = endAnchor;
            Vector3 pullDirection = -direction;
            Transform ignoredRoot = null;
            Rigidbody otherBody = null;

            bool startFixed = startMovable == null;
            bool endFixed = endMovable == null;
            if (startFixed && endFixed)
            {
                return;
            }

            if (startFixed)
            {
                pullBody = endMovable;
                pullAnchor = endAnchor;
                pullDirection = -direction;
                ignoredRoot = startIgnore;
                otherBody = null;
            }
            else if (endFixed)
            {
                pullBody = startMovable;
                pullAnchor = startAnchor;
                pullDirection = direction;
                ignoredRoot = endIgnore;
                otherBody = null;
            }
            else if (startMass <= endMass)
            {
                pullBody = startMovable;
                pullAnchor = startAnchor;
                pullDirection = direction;
                ignoredRoot = endIgnore;
                otherBody = endMovable;
            }
            else
            {
                pullBody = endMovable;
                pullAnchor = endAnchor;
                pullDirection = -direction;
                ignoredRoot = startIgnore;
                otherBody = startMovable;
            }

            if (pullBody == null)
            {
                return;
            }

            pullBody.isKinematic = false;
            pullBody.useGravity = true;
            pullBody.WakeUp();

            float excess = distance - softLimit;
            float correction = Mathf.Min(excess, PullCorrectionPerTick);
            MiniVanTowRopeUtility.MoveBodyWithSliding(
                pullBody,
                pullDirection * correction,
                pullBody.transform,
                ignoredRoot,
                0.025f,
                12,
                8f);

            Vector3 relativeVelocity = (otherBody != null ? otherBody.linearVelocity : Vector3.zero) - pullBody.linearVelocity;
            float velocityAway = Mathf.Max(0f, Vector3.Dot(relativeVelocity, -pullDirection));
            float forceMagnitude = Mathf.Clamp(excess * PullSpringForce + velocityAway * PullDamper, 0f, PullMaxForce);
            pullBody.AddForceAtPosition(pullDirection * forceMagnitude, pullAnchor, ForceMode.Force);
        }

        private static void ResolveMovableEnd(
            AnchorKind kind,
            MiniVanVehicle vehicle,
            Rigidbody body,
            out Rigidbody movable,
            out float mass,
            out Transform ignoredRoot)
        {
            movable = null;
            mass = float.PositiveInfinity;
            ignoredRoot = null;

            if (kind == AnchorKind.Static || kind == AnchorKind.None)
            {
                return;
            }

            if (kind == AnchorKind.Vehicle)
            {
                movable = vehicle != null ? vehicle.GetComponent<Rigidbody>() : body;
                ignoredRoot = vehicle != null ? vehicle.transform.root : null;
            }
            else if (kind == AnchorKind.Rigidbody)
            {
                movable = body;
                ignoredRoot = body != null ? body.transform.root : null;
            }

            if (movable == null || movable.isKinematic)
            {
                movable = null;
                mass = float.PositiveInfinity;
                return;
            }

            mass = Mathf.Max(0.01f, movable.mass);
        }

        private Vector3 GetStartAnchorWorld()
        {
            return GetAnchorWorld(startKind, startVehicle, startBody, startLocalAnchor, startStaticAnchor);
        }

        private Vector3 GetEndAnchorWorld()
        {
            return GetAnchorWorld(endKind, endVehicle, endBody, endLocalAnchor, endStaticAnchor);
        }

        private static Vector3 GetAnchorWorld(AnchorKind kind, MiniVanVehicle vehicle, Rigidbody body, Vector3 localAnchor, Vector3 staticAnchor)
        {
            switch (kind)
            {
                case AnchorKind.Vehicle:
                    return vehicle != null ? vehicle.transform.TransformPoint(localAnchor) : staticAnchor;
                case AnchorKind.Rigidbody:
                    return body != null ? body.transform.TransformPoint(localAnchor) : staticAnchor;
                case AnchorKind.Static:
                    return staticAnchor;
                default:
                    return staticAnchor;
            }
        }

        private void SetStartAnchor(AnchorKind kind, MiniVanVehicle vehicle, Rigidbody body, Vector3 anchor)
        {
            startKind = kind;
            startVehicle = vehicle;
            startBody = body;
            startStaticAnchor = anchor;
            startLocalAnchor = GetLocalAnchor(kind, vehicle, body, anchor);
        }

        private void SetEndAnchor(AnchorKind kind, MiniVanVehicle vehicle, Rigidbody body, Vector3 anchor)
        {
            endKind = kind;
            endVehicle = vehicle;
            endBody = body;
            endStaticAnchor = anchor;
            endLocalAnchor = GetLocalAnchor(kind, vehicle, body, anchor);
        }

        private void SwapAnchors()
        {
            AnchorKind oldStartKind = startKind;
            MiniVanVehicle oldStartVehicle = startVehicle;
            Rigidbody oldStartBody = startBody;
            Vector3 oldStartLocalAnchor = startLocalAnchor;
            Vector3 oldStartStaticAnchor = startStaticAnchor;

            startKind = endKind;
            startVehicle = endVehicle;
            startBody = endBody;
            startLocalAnchor = endLocalAnchor;
            startStaticAnchor = endStaticAnchor;

            endKind = oldStartKind;
            endVehicle = oldStartVehicle;
            endBody = oldStartBody;
            endLocalAnchor = oldStartLocalAnchor;
            endStaticAnchor = oldStartStaticAnchor;
        }

        private void ClearEndAnchor()
        {
            endKind = AnchorKind.None;
            endVehicle = null;
            endBody = null;
            endLocalAnchor = Vector3.zero;
            endStaticAnchor = Vector3.zero;
        }

        private static Vector3 GetLocalAnchor(AnchorKind kind, MiniVanVehicle vehicle, Rigidbody body, Vector3 anchor)
        {
            if (kind == AnchorKind.Vehicle && vehicle != null)
            {
                return vehicle.transform.InverseTransformPoint(anchor);
            }

            if (kind == AnchorKind.Rigidbody && body != null)
            {
                return body.transform.InverseTransformPoint(anchor);
            }

            return Vector3.zero;
        }

        private bool IsStartEndInReach(Vector3 position)
        {
            return points.Count > 0 && Vector3.Distance(position, points[0].position) <= AttachReach;
        }

        private bool IsEndEndInReach(Vector3 position)
        {
            return points.Count > 0 && Vector3.Distance(position, points[points.Count - 1].position) <= AttachReach;
        }

        private void ApplyWear(bool reeling)
        {
            // Active reel already applies its own ~30s drain; keep tension wear while taut.
            Vector3 start = GetStartAnchorWorld();
            Vector3 end = GetEndAnchorWorld();
            float straightDistance = Vector3.Distance(start, end);
            float stretch = Mathf.Max(0f, straightDistance - deployedLength * 0.82f);
            float tension01 = Mathf.Clamp01(stretch / Mathf.Max(0.05f, deployedLength * 0.18f));
            float mass = GetAttachedObjectMass();
            float speedAway = GetVehicleSpeedAway(start, end);

            float wear = BaseWearPerSecond +
                         tension01 * TensionWearPerSecond +
                         Mathf.Clamp(mass, 0f, 220f) * MassWearPerSecond * tension01 +
                         speedAway * SpeedWearPerMeterPerSecond * tension01;

            // While reeling, tension wear is reduced so the 30s reel budget dominates.
            if (reeling)
            {
                wear *= 0.35f;
            }

            durability01 = Mathf.Clamp01(durability01 - wear * Time.fixedDeltaTime);
            PublishDurability(false);

            if (durability01 <= 0.001f)
            {
                BreakCable();
            }
        }

        private float GetAttachedObjectMass()
        {
            Rigidbody body = startKind == AnchorKind.Rigidbody ? startBody : endKind == AnchorKind.Rigidbody ? endBody : null;
            return body != null ? body.mass : 80f;
        }

        private float GetVehicleSpeedAway(Vector3 start, Vector3 end)
        {
            Rigidbody vehicleBody = null;
            Vector3 vehicleAnchor = start;
            Vector3 otherAnchor = end;
            if (startKind == AnchorKind.Vehicle && startVehicle != null)
            {
                vehicleBody = startVehicle.GetComponent<Rigidbody>();
            }
            else if (endKind == AnchorKind.Vehicle && endVehicle != null)
            {
                vehicleBody = endVehicle.GetComponent<Rigidbody>();
                vehicleAnchor = end;
                otherAnchor = start;
            }

            if (vehicleBody == null)
            {
                return 0f;
            }

            Vector3 pullDirection = vehicleAnchor - otherAnchor;
            return pullDirection.sqrMagnitude > 0.001f
                ? Mathf.Max(0f, Vector3.Dot(vehicleBody.linearVelocity, pullDirection.normalized))
                : 0f;
        }

        private void PublishDurability(bool force)
        {
            if (owner == null)
            {
                return;
            }

            if (!force && Time.time < nextDurabilityPublishTime)
            {
                return;
            }

            nextDurabilityPublishTime = Time.time + 0.18f;
            owner.SetWinchDurabilityFromWorld(durability01);
        }

        private void UpdateConnectors()
        {
            int count = Mathf.Min(connectors.Count, points.Count - 1);
            for (int i = 0; i < count; i++)
            {
                Transform connector = connectors[i];
                Rigidbody a = points[i];
                Rigidbody b = points[i + 1];
                if (connector == null || a == null || b == null)
                {
                    continue;
                }

                Vector3 delta = b.position - a.position;
                float length = delta.magnitude;
                connector.position = (a.position + b.position) * 0.5f;
                if (length > 0.001f)
                {
                    connector.rotation = Quaternion.FromToRotation(Vector3.up, delta.normalized);
                }

                connector.localScale = new Vector3(RopeRadius, Mathf.Max(0.001f, length * 0.5f), RopeRadius);
            }
        }

        private void ClearActive()
        {
            if (owner != null && activeByPlayer.TryGetValue(owner, out MiniVanWinchCable active) && active == this)
            {
                activeByPlayer.Remove(owner);
            }
        }

        private void SetEndpointColliderTrigger(int index, bool isTrigger)
        {
            if (index < 0 || index >= points.Count || points[index] == null)
            {
                return;
            }

            Collider endpointCollider = points[index].GetComponent<Collider>();
            if (endpointCollider != null)
            {
                endpointCollider.isTrigger = isTrigger;
            }
        }

        private Vector3 GetFoldedPickupPosition()
        {
            if (points.Count == 0)
            {
                return transform.position;
            }

            Vector3 sum = Vector3.zero;
            int count = 0;
            for (int i = 0; i < points.Count; i++)
            {
                if (points[i] == null)
                {
                    continue;
                }

                sum += points[i].position;
                count++;
            }

            Vector3 center = count > 0 ? sum / count : transform.position;
            Vector3 rayOrigin = center + Vector3.up * 1.2f;
            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 3f, ~0, QueryTriggerInteraction.Ignore))
            {
                center = hit.point + Vector3.up * 0.16f;
            }

            return center;
        }

        private static bool IsFoldableState(WinchState state)
        {
            return state == WinchState.FirstEndAttached ||
                   state == WinchState.DroppedOpen ||
                   state == WinchState.BothEndsAttached;
        }

        private static MiniVanWinchCable FindFoldableCable(MiniVanPlayer player)
        {
            if (player == null)
            {
                return null;
            }

            if (activeByPlayer.TryGetValue(player, out MiniVanWinchCable active) &&
                active != null &&
                IsFoldableState(active.state))
            {
                return active;
            }

            if (player.PlayerCamera != null)
            {
                Ray ray = new Ray(player.PlayerCamera.transform.position, player.PlayerCamera.transform.forward);
                RaycastHit[] hits = Physics.RaycastAll(ray, FoldReach, ~0, QueryTriggerInteraction.Collide);
                System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
                for (int i = 0; i < hits.Length; i++)
                {
                    MiniVanWinchCable cable = hits[i].collider != null
                        ? hits[i].collider.GetComponentInParent<MiniVanWinchCable>()
                        : null;
                    if (cable != null && IsFoldableState(cable.state) && cable.IsInFoldReach(player.transform.position))
                    {
                        return cable;
                    }
                }
            }

            // Also scan registered cables (BothEndsAttached is often not under activeByPlayer).
            MiniVanWinchCable best = null;
            float bestDistance = float.MaxValue;
            Vector3 playerPos = player.transform.position;
            for (int i = 0; i < allCables.Count; i++)
            {
                MiniVanWinchCable cable = allCables[i];
                if (cable == null || !IsFoldableState(cable.state) || !cable.IsInFoldReach(playerPos))
                {
                    continue;
                }

                float distance = cable.GetNearestEndDistance(playerPos);
                if (distance < bestDistance)
                {
                    best = cable;
                    bestDistance = distance;
                }
            }

            Collider[] nearby = Physics.OverlapSphere(playerPos + Vector3.up * 0.8f, 1.65f, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < nearby.Length; i++)
            {
                MiniVanWinchCable cable = nearby[i] != null ? nearby[i].GetComponentInParent<MiniVanWinchCable>() : null;
                if (cable == null || !IsFoldableState(cable.state) || !cable.IsInFoldReach(playerPos))
                {
                    continue;
                }

                float distance = Vector3.Distance(playerPos, nearby[i].transform.position);
                if (distance < bestDistance)
                {
                    best = cable;
                    bestDistance = distance;
                }
            }

            return best;
        }

        private bool IsInFoldReach(Vector3 playerPosition)
        {
            return GetNearestEndDistance(playerPosition) <= FoldReach;
        }

        private float GetNearestEndDistance(Vector3 playerPosition)
        {
            float best = float.MaxValue;
            if (points.Count > 0 && points[0] != null)
            {
                best = Mathf.Min(best, Vector3.Distance(playerPosition, points[0].position));
            }

            if (points.Count > 0 && points[points.Count - 1] != null)
            {
                best = Mathf.Min(best, Vector3.Distance(playerPosition, points[points.Count - 1].position));
            }

            best = Mathf.Min(best, Vector3.Distance(playerPosition, GetStartAnchorWorld()));
            if (state == WinchState.BothEndsAttached)
            {
                best = Mathf.Min(best, Vector3.Distance(playerPosition, GetEndAnchorWorld()));
            }

            return best;
        }

        private static void ResetFoldProgress()
        {
            foldingPlayer = null;
            foldingCable = null;
            foldStartTime = 0f;
        }

        private static bool WasUseConsumedThisFrame(MiniVanPlayer player)
        {
            return player != null &&
                consumedUseFrame.TryGetValue(player, out int frame) &&
                frame == Time.frameCount;
        }

        private static void MarkUseConsumed(MiniVanPlayer player)
        {
            if (player != null)
            {
                consumedUseFrame[player] = Time.frameCount;
            }
        }

        private void OnDestroy()
        {
            ClearActive();
            UnregisterCable(this);
        }

        private void BuildMaterials()
        {
            ropeMaterial = CreateMaterial(new Color(0.045f, 0.043f, 0.039f, 1f));
            endpointMaterial = CreateMaterial(new Color(0.78f, 0.11f, 0.06f, 1f));
        }

        private static bool TryFindNearestVehicleAnchor(Vector3 worldPosition, out MiniVanVehicle foundVehicle, out Rigidbody foundBody, out Vector3 anchor)
        {
            return TryFindNearestVehicleAnchor(worldPosition, null, out foundVehicle, out foundBody, out anchor);
        }

        private static bool TryFindNearestVehicleAnchor(Vector3 worldPosition, MiniVanWinchCable activeCable, out MiniVanVehicle foundVehicle, out Rigidbody foundBody, out Vector3 anchor)
        {
            foundVehicle = null;
            foundBody = null;
            anchor = default;
            float bestDistance = float.MaxValue;

            MiniVanVehicle[] vehicles = FindObjectsByType<MiniVanVehicle>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < vehicles.Length; i++)
            {
                MiniVanVehicle candidate = vehicles[i];
                if (candidate == null)
                {
                    continue;
                }

                Rigidbody body = candidate.GetComponent<Rigidbody>();
                if (TryGetVehicleHookAnchor(candidate, true, out Vector3 front))
                {
                    Consider(candidate, body, front, worldPosition, activeCable, ref foundVehicle, ref foundBody, ref anchor, ref bestDistance);
                }

                if (TryGetVehicleHookAnchor(candidate, false, out Vector3 rear))
                {
                    Consider(candidate, body, rear, worldPosition, activeCable, ref foundVehicle, ref foundBody, ref anchor, ref bestDistance);
                }
            }

            MiniVanTowHook[] hooks = FindObjectsByType<MiniVanTowHook>(FindObjectsSortMode.None);
            for (int i = 0; i < hooks.Length; i++)
            {
                MiniVanTowHook hook = hooks[i];
                if (hook == null)
                {
                    continue;
                }

                MiniVanVehicle hookVehicle = hook.GetComponentInParent<MiniVanVehicle>();
                Rigidbody body = hook.GetComponentInParent<Rigidbody>();
                Consider(hookVehicle, body, hook.AnchorPosition, worldPosition, activeCable, ref foundVehicle, ref foundBody, ref anchor, ref bestDistance);
            }

            return foundVehicle != null || foundBody != null;
        }

        public static bool TryGetVehicleHookAnchor(MiniVanVehicle vehicle, bool front, out Vector3 anchor)
        {
            anchor = default;
            if (vehicle == null)
            {
                return false;
            }

            string hookName = front ? "Tow Hook Point Front" : "Tow Hook Point Back";
            Transform hook = FindChildRecursive(vehicle.transform, hookName);
            if (hook != null)
            {
                anchor = hook.position;
                return true;
            }

            // Fallback for older vans without named hook points.
            anchor = vehicle.transform.TransformPoint(front
                ? new Vector3(0f, 0.78f, 3.66f)
                : new Vector3(0f, 0.78f, -3.72f));
            return true;
        }

        private static Transform FindChildRecursive(Transform root, string exactName)
        {
            if (root == null)
            {
                return null;
            }

            if (root.name == exactName)
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindChildRecursive(root.GetChild(i), exactName);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static void Consider(MiniVanVehicle candidate, Rigidbody body, Vector3 candidateAnchor, Vector3 worldPosition, MiniVanWinchCable activeCable,
            ref MiniVanVehicle foundVehicle, ref Rigidbody foundBody, ref Vector3 anchor, ref float bestDistance)
        {
            if (activeCable != null && !activeCable.IsValidSecondAnchor(AnchorKind.Vehicle, candidate, body, candidateAnchor))
            {
                return;
            }

            float distance = Vector3.Distance(worldPosition, candidateAnchor);
            if (distance <= AttachReach && distance < bestDistance)
            {
                foundVehicle = candidate;
                foundBody = body;
                anchor = candidateAnchor;
                bestDistance = distance;
            }
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
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
            return material;
        }

        private static void SetMaterial(GameObject target, Material material)
        {
            Renderer renderer = target != null ? target.GetComponent<Renderer>() : null;
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
            }
        }

        private sealed class MiniVanWinchEndpoint : MonoBehaviour, IMiniVanGameModeInteractable
        {
            private MiniVanWinchCable cable;
            private bool startEnd;

            public void Configure(MiniVanWinchCable ownerCable, bool isStartEnd)
            {
                cable = ownerCable;
                startEnd = isStartEnd;
            }

            public string GetPrompt(MiniVanPlayer player)
            {
                if (cable == null || player == null)
                {
                    return string.Empty;
                }

                if (cable.state == WinchState.DroppedOpen)
                {
                    if (startEnd)
                    {
                        return string.Empty;
                    }

                    return cable.IsEndEndInReach(player.transform.position) ? "E - pick up winch end, hold E - fold" : string.Empty;
                }

                if (cable.state == WinchState.FirstEndAttached && cable.owner == player)
                {
                    return "E - attach winch end, hold E - fold";
                }

                if (cable.state == WinchState.BothEndsAttached)
                {
                    bool inReach = startEnd
                        ? cable.IsStartEndInReach(player.transform.position)
                        : cable.IsEndEndInReach(player.transform.position);
                    return inReach ? "E - take winch end, hold E - fold" : string.Empty;
                }

                bool detachReach = startEnd
                    ? cable.IsStartEndInReach(player.transform.position)
                    : cable.IsEndEndInReach(player.transform.position);
                return detachReach ? "E - remove winch" : string.Empty;
            }

            public void Interact(MiniVanPlayer player)
            {
                if (Input.GetMouseButton(1) || WasUseConsumedThisFrame(player))
                {
                    return;
                }

                if (cable != null && player != null)
                {
                    if (cable.state == WinchState.DroppedOpen)
                    {
                        cable.TryGrabDroppedFreeEnd(player);
                        MarkUseConsumed(player);
                        return;
                    }

                    if (cable.state == WinchState.BothEndsAttached)
                    {
                        bool takeStart = startEnd && cable.IsStartEndInReach(player.transform.position);
                        bool takeEnd = !startEnd && cable.IsEndEndInReach(player.transform.position);
                        if (takeStart || takeEnd)
                        {
                            cable.TakeAttachedEndIntoHands(player, takeStart);
                        }

                        return;
                    }

                    cable.TryAttachSecondEndOnly(player);
                }
            }

            public void PrimaryAction(MiniVanPlayer player)
            {
            }
        }
    }
}
