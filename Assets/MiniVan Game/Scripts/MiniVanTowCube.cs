using Unity.Netcode;
using UnityEngine;

namespace MiniVanGame
{
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(BoxCollider))]
    public class MiniVanTowCube : NetworkBehaviour
    {
        public const ulong EmptyClientId = ulong.MaxValue;

        public float PickupRadius = 2.35f;
        public float TowAttachExtraRadius = 0.55f;
        public float TowRopeLength = 20f;
        public float TowSoftLimitDamping = 3.5f;
        public float TowRopeCastRadius = 0.16f;
        public float TowMaxCorrectionPerTick = 0.55f;
        public float TowEmergencyMaxCorrectionPerTick = 3.0f;
        public float TowCornerSlideAssist = 12f;
        public float TowWrapSlipExcess = 0.35f;
        public int TowHardLimitIterations = 8;
        public float Mass = 7f;
        public float FloorRecoveryHeight = 0.43f;
        public Vector3 CarryOffset = new Vector3(0.42f, -0.42f, 1.05f);
        public Color CubeColor = new Color(0.25f, 0.55f, 1f, 1f);
        [Header("Tow Debug")]
        public bool DebugTowRope = true;
        public float DebugTowRopeInterval = 0.35f;
        public float DebugTowRopeSnapLengthDelta = 2.5f;
        public float DebugTowRopeLargeCorrection = 1.25f;

        public readonly NetworkVariable<ulong> HolderClientId = new NetworkVariable<ulong>(
            EmptyClientId,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public readonly NetworkVariable<bool> TowAttached = new NetworkVariable<bool>(false);
        public readonly NetworkVariable<Vector3> TowAnchorPosition = new NetworkVariable<Vector3>();
        public readonly NetworkVariable<Vector3> NetworkPosition = new NetworkVariable<Vector3>();
        public readonly NetworkVariable<Quaternion> NetworkRotation = new NetworkVariable<Quaternion>(Quaternion.identity);

        private readonly Vector3[] towRopePath = new Vector3[MiniVanTowRopeUtility.MaxPathPoints];
        private readonly MiniVanTowRopeUtility.RopeState towRopeState = new MiniVanTowRopeUtility.RopeState();
        private Rigidbody body;
        private MiniVanTowHook towHook;
        private LineRenderer towRopeRenderer;
        private Material towRopeMaterial;
        private Material cubeMaterial;
        private Vector3 remoteVelocity;
        private bool localDropPredictionActive;
        private float nextTowDebugLogTime;
        private float lastTowPathLength = -1f;
        private int lastTowPathCount = -1;

        public bool IsCarried => HolderClientId.Value != EmptyClientId;
        public bool IsAvailable => !IsCarried && !TowAttached.Value;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            ConfigureBody();
            ConfigureVisual();
        }

        public override void OnNetworkSpawn()
        {
            body = GetComponent<Rigidbody>();
            ConfigureBody();
            ConfigureVisual();

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
            if ((targetPosition - transform.position).sqrMagnitude > 16f)
            {
                transform.SetPositionAndRotation(targetPosition, targetRotation);
                remoteVelocity = Vector3.zero;
                return;
            }

            transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref remoteVelocity, 0.08f, Mathf.Infinity, Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 1f - Mathf.Exp(-18f * Time.deltaTime));
        }

        private void LateUpdate()
        {
            if (IsSpawned && !localDropPredictionActive && IsLocalHolder())
            {
                SimulateCarried();
            }

            UpdateTowRopeVisual();
        }

        private void FixedUpdate()
        {
            if (!IsServer || body == null)
            {
                return;
            }

            if (IsCarried)
            {
                SimulateCarried();
                NetworkPosition.Value = transform.position;
                NetworkRotation.Value = transform.rotation;
                return;
            }

            if (body.isKinematic)
            {
                body.isKinematic = false;
                body.useGravity = true;
            }

            ApplyTowRopePhysics();
            PreventFloorTunneling();
            NetworkPosition.Value = transform.position;
            NetworkRotation.Value = transform.rotation;
        }

        public bool IsInReach(Vector3 worldPosition)
        {
            return Vector3.Distance(worldPosition, transform.position) <= PickupRadius;
        }

        public bool HasTowHookInRange(Vector3 worldPosition)
        {
            return MiniVanTowHook.FindNearest(worldPosition, TowAttachExtraRadius) != null;
        }

        public bool TryPickup(ulong clientId, MiniVanPlayer player)
        {
            if (!IsServer || player == null || !IsAvailable || !IsInReach(player.transform.position))
            {
                return false;
            }

            DetachTowRope();
            HolderClientId.Value = clientId;

            if (body != null)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.isKinematic = true;
                body.useGravity = false;
            }

            if (NetworkObject != null && NetworkObject.IsSpawned && NetworkObject.OwnerClientId != clientId)
            {
                NetworkObject.ChangeOwnership(clientId);
            }

            SimulateCarried();
            return true;
        }

        public bool TryDrop(ulong clientId, MiniVanPlayer player)
        {
            if (!IsServer || player == null || HolderClientId.Value != clientId)
            {
                return false;
            }

            PlaceOnGroundInFrontOf(player, out Vector3 dropPosition, out Quaternion dropRotation);
            HolderClientId.Value = EmptyClientId;
            transform.SetPositionAndRotation(dropPosition, dropRotation);

            if (body != null)
            {
                body.isKinematic = false;
                body.useGravity = true;
                body.position = dropPosition;
                body.rotation = dropRotation;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.WakeUp();
            }

            RemoveOwnershipIfHeld(clientId);
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

            PlaceOnGroundInFrontOf(player, out Vector3 dropPosition, out Quaternion dropRotation);
            localDropPredictionActive = true;
            transform.SetPositionAndRotation(dropPosition, dropRotation);
            if (body != null)
            {
                body.isKinematic = false;
                body.useGravity = true;
                body.position = dropPosition;
                body.rotation = dropRotation;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.WakeUp();
            }
        }

        public bool TryAttachHeldToHook(ulong clientId, MiniVanPlayer player)
        {
            if (!IsServer || player == null || HolderClientId.Value != clientId)
            {
                return false;
            }

            MiniVanTowHook nearestHook = MiniVanTowHook.FindNearest(player.transform.position, TowAttachExtraRadius);
            if (nearestHook == null)
            {
                return false;
            }

            PlaceOnGroundInFrontOf(player, out Vector3 dropPosition, out Quaternion dropRotation);
            HolderClientId.Value = EmptyClientId;
            towHook = nearestHook;
            towRopeState.Clear();
            ResetTowDebugHistory();
            TowAttached.Value = true;
            TowAnchorPosition.Value = towHook.AnchorPosition;
            transform.SetPositionAndRotation(dropPosition, dropRotation);

            if (body != null)
            {
                body.isKinematic = false;
                body.useGravity = true;
                body.position = dropPosition;
                body.rotation = dropRotation;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.WakeUp();
            }

            RemoveOwnershipIfHeld(clientId);
            NetworkPosition.Value = transform.position;
            NetworkRotation.Value = transform.rotation;
            return true;
        }

        public bool TryDetachTowRope(ulong clientId, MiniVanPlayer player)
        {
            if (!IsServer || player == null || !TowAttached.Value || !IsInReach(player.transform.position))
            {
                return false;
            }

            DetachTowRope();
            return true;
        }

        private void ApplyTowRopePhysics()
        {
            if (!TowAttached.Value || body == null)
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
                towHook = MiniVanTowHook.FindNearest(TowAnchorPosition.Value, 50f);
            }

            if (towHook == null)
            {
                DetachTowRope();
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
                if (excess <= 0.015f || direction.sqrMagnitude <= 0.000001f)
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

            float velocityAwayFromTarget = -Vector3.Dot(body.linearVelocity, direction);
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
            Vector3 fallback = Vector3.zero;
            if (towHook != null && MiniVanTowRopeUtility.IsFinite(towHook.AnchorPosition))
            {
                fallback = towHook.AnchorPosition + Vector3.down * 0.35f;
            }
            else if (MiniVanTowRopeUtility.IsFinite(TowAnchorPosition.Value))
            {
                fallback = TowAnchorPosition.Value + Vector3.down * 0.35f;
            }

            transform.position = fallback;
            if (body != null)
            {
                body.position = fallback;
                body.rotation = Quaternion.identity;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }

            Debug.LogWarning("[MiniVanTowCube] Invalid tow physics state recovered; detaching rope to avoid corrupt transform.");
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

            string message = "[MiniVanTowCube][TowDebug] pos=" + transform.position.ToString("F2")
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

            string fullMessage = "[MiniVanTowCube][TowDebug] " + message;
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

        private void PreventFloorTunneling()
        {
            if (body == null || IsCarried)
            {
                return;
            }

            Vector3 origin = body.position + Vector3.up * 12f;
            RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 30f, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

            RaycastHit groundHit = default;
            bool foundGround = false;
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

                if (hits[i].normal.y < 0.25f)
                {
                    continue;
                }

                groundHit = hits[i];
                foundGround = true;
                break;
            }

            if (!foundGround)
            {
                return;
            }

            float desiredY = groundHit.point.y + Mathf.Max(0.1f, FloorRecoveryHeight);
            if (body.position.y >= desiredY)
            {
                return;
            }

            Vector3 recovered = body.position;
            recovered.y = desiredY;
            body.position = recovered;

            Vector3 velocity = body.linearVelocity;
            if (velocity.y < 0f)
            {
                velocity.y = 0f;
                body.linearVelocity = velocity;
            }
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

        private Vector3 GetTowAttachPosition()
        {
            return transform.position + Vector3.up * 0.22f;
        }

        private void SimulateCarried()
        {
            MiniVanPlayer holder = FindPlayerForClient(HolderClientId.Value);
            if (holder == null)
            {
                return;
            }

            holder.GetTowCubeCarryPose(CarryOffset, out Vector3 targetPosition, out Quaternion targetRotation);
            transform.SetPositionAndRotation(targetPosition, targetRotation);
            if (body != null)
            {
                body.position = targetPosition;
                body.rotation = targetRotation;
            }
        }

        private void PlaceOnGroundInFrontOf(MiniVanPlayer player, out Vector3 position, out Quaternion rotation)
        {
            Vector3 forward = Vector3.ProjectOnPlane(player.transform.forward, Vector3.up);
            forward = forward.sqrMagnitude > 0.01f ? forward.normalized : Vector3.forward;
            position = player.transform.position + forward * 1.05f + Vector3.up * 0.35f;

            if (Physics.Raycast(player.transform.position + Vector3.up * 0.75f + forward * 0.8f, Vector3.down, out RaycastHit groundHit, 3f, ~0, QueryTriggerInteraction.Ignore))
            {
                position = groundHit.point + groundHit.normal * 0.35f;
            }

            rotation = Quaternion.LookRotation(forward, Vector3.up);
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
        }

        private bool IsLocalHolder()
        {
            return IsCarried
                && NetworkManager.Singleton != null
                && HolderClientId.Value == NetworkManager.Singleton.LocalClientId;
        }

        private void RemoveOwnershipIfHeld(ulong clientId)
        {
            if (NetworkObject != null && NetworkObject.IsSpawned && NetworkObject.OwnerClientId == clientId)
            {
                NetworkObject.RemoveOwnership();
            }
        }

        private void ConfigureBody()
        {
            if (body == null)
            {
                return;
            }

            body.mass = Mathf.Max(0.1f, Mass);
            body.linearDamping = 0.35f;
            body.angularDamping = 0.6f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }

        private void ConfigureVisual()
        {
            Renderer renderer = GetComponentInChildren<Renderer>();
            if (renderer == null)
            {
                return;
            }

            if (cubeMaterial == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null)
                {
                    shader = Shader.Find("Standard");
                }

                cubeMaterial = new Material(shader);
                cubeMaterial.name = "Runtime Tow Cube Blue";
                cubeMaterial.color = CubeColor;
            }

            renderer.sharedMaterial = cubeMaterial;
        }

        private void EnsureTowRopeRenderer()
        {
            if (towRopeRenderer != null)
            {
                return;
            }

            GameObject ropeObject = new GameObject("Tow Cube Rope Visual");
            ropeObject.transform.SetParent(transform, false);
            towRopeRenderer = ropeObject.AddComponent<LineRenderer>();
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
            towRopeMaterial.name = "Tow Cube Rope Black";
            towRopeMaterial.color = Color.black;
            towRopeRenderer.sharedMaterial = towRopeMaterial;
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
    }
}
