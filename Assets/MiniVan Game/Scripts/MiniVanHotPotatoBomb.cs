using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace MiniVanGame
{
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(NetworkTransform))]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(SphereCollider))]
    public class MiniVanHotPotatoBomb : NetworkBehaviour
    {
        private enum BombState
        {
            Available = 0,
            Held = 1,
            Flying = 2,
            Exploded = 3
        }

        public float PickupRadius = 2.4f;
        public float ThrowSpeed = 13.5f;
        public float ThrowUpVelocity = 1.25f;
        public float CatchRadius = 1.6f;
        public int ThrowsBeforeExplosion = 3;
        public int MinThrowsBeforeExplosion = 3;
        public int MaxThrowsBeforeExplosion = 20;
        public float MaxHeldSeconds = 20f;
        public float PoopSeconds = 20f;
        public float ReturnImmunitySeconds = 0.5f;
        public float MissReturnDelay = 0.8f;
        public float PlayRadius = 30f;
        public bool ShowPlayRadius = true;
        public Vector3 PlayerHoldOffset = new Vector3(0.32f, -0.24f, 0.74f);
        public Vector3 RemoteHoldOffset = new Vector3(0.36f, 0.62f, 0.34f);
        public float LocalThrowPredictionSeconds = 0.22f;

        private readonly NetworkVariable<int> state = new NetworkVariable<int>(
            (int)BombState.Available,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<ulong> holderObjectId = new NetworkVariable<ulong>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> throwCount = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> explosionThrowLimit = new NetworkVariable<int>(
            3,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private Rigidbody body;
        private SphereCollider sphereCollider;
        private Renderer[] renderers;
        private Material[] runtimeMaterials;
        private LineRenderer playRadiusLine;
        private ulong lastThrowerObjectId;
        private float ignoreThrowerUntil;
        private float serverHeldStartTime;
        private ulong returnImmuneObjectId;
        private float returnImmuneUntil;
        private float missedReturnAt;
        private float localThrowPredictionUntil;
        private Vector3 localThrowPredictionVelocity;
        private bool localOwnerReleased;
public bool IsAvailable => !IsSpawned || state.Value == (int)BombState.Available;
        public int ThrowCount => throwCount.Value;
        public bool IsActivated => throwCount.Value > 0;
        public int SelectedThrowsBeforeExplosion => CurrentThrowLimit;
        public bool IsPlayRadiusActive => IsActivated && ((BombState)state.Value == BombState.Held || (BombState)state.Value == BombState.Flying);
        public Vector3 PlayRadiusCenter => transform.position;
        private int CurrentThrowLimit => Mathf.Max(1, explosionThrowLimit.Value);
        public bool IsHeldBy(ulong networkObjectId)
        {
            return (BombState)state.Value == BombState.Held && holderObjectId.Value == networkObjectId;
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            sphereCollider = GetComponent<SphereCollider>();
            EnsureVisual();
            CacheRenderers();
            ConfigurePhysicsForState((BombState)state.Value);
        }

        public override void OnNetworkSpawn()
        {
            state.OnValueChanged += HandleStateChanged;
            throwCount.OnValueChanged += HandleThrowCountChanged;
            explosionThrowLimit.OnValueChanged += HandleExplosionThrowLimitChanged;
            ConfigurePhysicsForState((BombState)state.Value);
            ApplyBlinkVisual();
        }

        public override void OnNetworkDespawn()
        {
            state.OnValueChanged -= HandleStateChanged;
            throwCount.OnValueChanged -= HandleThrowCountChanged;
            explosionThrowLimit.OnValueChanged -= HandleExplosionThrowLimitChanged;
        }

        private void Update()
        {
            UpdateLocalThrowPrediction();
            BombState currentState = (BombState)state.Value;
            if (currentState == BombState.Held)
            {
                UpdateHeldExplosionTimer();
            }
            else if (currentState == BombState.Flying)
            {
                UpdateFlyingCatchProbe();
                if ((BombState)state.Value == BombState.Flying)
                {
                    UpdateMissedReturn();
                }
            }

            ApplyBlinkVisual();
            UpdatePlayRadiusVisual();
        }

        private void LateUpdate()
        {
            if ((BombState)state.Value == BombState.Held)
            {
                if (localOwnerReleased || Time.time < localThrowPredictionUntil)
                {
                    return;
                }

                FollowHolder();
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!IsServer || (BombState)state.Value != BombState.Flying || collision == null || collision.collider == null)
            {
                return;
            }

            MiniVanPlayer player = collision.collider.GetComponentInParent<MiniVanPlayer>();
            if (player != null)
            {
                if (player.NetworkObjectId == lastThrowerObjectId && Time.time < ignoreThrowerUntil)
                {
                    return;
                }

                if (IsReturnImmuneTarget(player.NetworkObjectId))
                {
                    ScheduleMissedReturn();
                    return;
                }

                if (player.NetworkObjectId != lastThrowerObjectId)
                {
                    CatchOrExplodeOnPlayer(player);
                }
                else
                {
                    ScheduleMissedReturn();
                }

                return;
            }

            MiniVanHotPotatoDummy dummy = collision.collider.GetComponentInParent<MiniVanHotPotatoDummy>();
            if (dummy != null)
            {
                if (dummy.NetworkObjectId == lastThrowerObjectId && Time.time < ignoreThrowerUntil)
                {
                    return;
                }

                if (IsReturnImmuneTarget(dummy.NetworkObjectId))
                {
                    ScheduleMissedReturn();
                    return;
                }

                if (dummy.NetworkObjectId != lastThrowerObjectId)
                {
                    CatchOrExplodeOnDummy(dummy);
                }
                else
                {
                    ScheduleMissedReturn();
                }

                return;
            }

            ScheduleMissedReturn();
        }

        public bool IsInReach(Vector3 worldPosition)
        {
            return Vector3.Distance(worldPosition, transform.position) <= PickupRadius;
        }

        public void PlayLocalThrowPrediction(MiniVanPlayer player, Vector3 direction)
        {
            if (player == null || (BombState)state.Value != BombState.Held || holderObjectId.Value != player.NetworkObjectId)
            {
                return;
            }

            Vector3 throwDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : player.transform.forward;
            Vector3 origin = player.PlayerCamera != null ? player.PlayerCamera.transform.position : player.transform.position + Vector3.up * 1.25f;
            Vector3 position = origin + throwDirection * 0.62f;
            Vector3 flatDirection = Vector3.ProjectOnPlane(throwDirection, Vector3.up);
            if (flatDirection.sqrMagnitude < 0.001f)
            {
                flatDirection = Vector3.ProjectOnPlane(player.transform.forward, Vector3.up);
            }

            Quaternion rotation = Quaternion.LookRotation(flatDirection.sqrMagnitude > 0.001f ? flatDirection.normalized : Vector3.forward, Vector3.up);
            localOwnerReleased = true;
            localThrowPredictionUntil = Time.time + Mathf.Max(0.05f, LocalThrowPredictionSeconds);
            localThrowPredictionVelocity = throwDirection * ThrowSpeed + Vector3.up * ThrowUpVelocity;
            SetBombVisualVisible(true);
            transform.SetPositionAndRotation(position, rotation);
            if (body != null)
            {
                body.isKinematic = true;
                body.useGravity = false;
                body.detectCollisions = false;
            }
        }

        public void PlayLocalDropPrediction(MiniVanPlayer player)
        {
            if (player == null || (BombState)state.Value != BombState.Held || holderObjectId.Value != player.NetworkObjectId)
            {
                return;
            }

            Vector3 flatForward = Vector3.ProjectOnPlane(player.transform.forward, Vector3.up);
            if (flatForward.sqrMagnitude < 0.001f)
            {
                flatForward = Vector3.forward;
            }

            flatForward.Normalize();
            Vector3 dropPosition = player.transform.position + flatForward * 0.95f + Vector3.up * 0.45f;
            Vector3 rayOrigin = player.transform.position + flatForward * 0.95f + Vector3.up * 1.25f;
            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 3f, ~0, QueryTriggerInteraction.Ignore))
            {
                dropPosition = hit.point + Vector3.up * 0.2f;
            }

            Quaternion rotation = Quaternion.LookRotation(flatForward, Vector3.up);
            localOwnerReleased = true;
            localThrowPredictionUntil = 0f;
            SetBombVisualVisible(true);
            transform.SetPositionAndRotation(dropPosition, rotation);
            ConfigurePhysicsForState(BombState.Available);
            if (body != null)
            {
                body.position = dropPosition;
                body.rotation = rotation;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.WakeUp();
            }
        }

        public bool ServerPickupByPlayer(MiniVanPlayer player)
        {
            if (!IsServer || player == null || !IsAvailable)
            {
                return false;
            }

            ServerChooseThrowLimit();
            throwCount.Value = 0;
            holderObjectId.Value = player.NetworkObjectId;
            serverHeldStartTime = 0f;
            returnImmuneObjectId = 0;
            returnImmuneUntil = 0f;
            missedReturnAt = 0f;
            state.Value = (int)BombState.Held;
            SetBombVisualVisible(true);
            return true;
        }

        public void ServerThrowFromPlayer(MiniVanPlayer player, Vector3 direction)
        {
            if (!IsServer || player == null || (BombState)state.Value != BombState.Held || holderObjectId.Value != player.NetworkObjectId)
            {
                return;
            }

            player.ServerDetachHotPotatoBomb(this);
            ServerLaunch(player.NetworkObjectId, player.transform.position + Vector3.up * 1.25f, direction);
        }

        public void ServerDropInactiveFromPlayer(MiniVanPlayer player, Vector3 position, Quaternion rotation)
        {
            if (!IsServer || player == null || IsActivated || (BombState)state.Value != BombState.Held || holderObjectId.Value != player.NetworkObjectId)
            {
                return;
            }

            player.ServerDetachHotPotatoBomb(this);
            holderObjectId.Value = 0;
            lastThrowerObjectId = 0;
            ignoreThrowerUntil = 0f;
            serverHeldStartTime = 0f;
            returnImmuneObjectId = 0;
            returnImmuneUntil = 0f;
            missedReturnAt = 0f;
            state.Value = (int)BombState.Available;
            transform.SetPositionAndRotation(position, rotation);
            ConfigurePhysicsForState(BombState.Available);
            if (body != null)
            {
                body.position = position;
                body.rotation = rotation;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.WakeUp();
            }

            SetBombVisualVisible(true);
        }

        public void ServerThrowFromDummy(MiniVanHotPotatoDummy dummy, MiniVanPlayer target)
        {
            if (!IsServer || dummy == null || target == null || (BombState)state.Value != BombState.Held || holderObjectId.Value != dummy.NetworkObjectId)
            {
                return;
            }

            dummy.ServerDetachBomb(this);
            Vector3 from = dummy.transform.position + Vector3.up * 1.25f;
            Vector3 to = target.transform.position + Vector3.up * 1.15f;
            ServerLaunch(dummy.NetworkObjectId, from, to - from);
        }

        private void ServerLaunch(ulong throwerObjectId, Vector3 origin, Vector3 direction)
        {
            lastThrowerObjectId = throwerObjectId;
            ignoreThrowerUntil = Time.time + 0.25f;
            holderObjectId.Value = 0;
            serverHeldStartTime = 0f;
            missedReturnAt = 0f;
            state.Value = (int)BombState.Flying;

            Vector3 throwDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : transform.forward;
            transform.position = origin + throwDirection * 0.55f;
            transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(throwDirection, Vector3.up).sqrMagnitude > 0.001f ? Vector3.ProjectOnPlane(throwDirection, Vector3.up).normalized : Vector3.forward, Vector3.up);

            ConfigurePhysicsForState(BombState.Flying);
            body.linearVelocity = throwDirection * ThrowSpeed + Vector3.up * ThrowUpVelocity;
            body.angularVelocity = Random.insideUnitSphere * 8f;
        }

        private void CatchOrExplodeOnPlayer(MiniVanPlayer player)
        {
            if (Time.time < ignoreThrowerUntil && player.NetworkObjectId == lastThrowerObjectId)
            {
                return;
            }

            int nextThrowCount = Mathf.Max(0, throwCount.Value) + 1;
            throwCount.Value = nextThrowCount;

            if (nextThrowCount >= CurrentThrowLimit)
            {
                ExplodeOnPlayer(player);
                return;
            }

            holderObjectId.Value = player.NetworkObjectId;
            serverHeldStartTime = Time.time;
            returnImmuneObjectId = lastThrowerObjectId;
            returnImmuneUntil = Time.time + Mathf.Max(0f, ReturnImmunitySeconds);
            missedReturnAt = 0f;
            state.Value = (int)BombState.Held;
            player.ServerAttachHotPotatoBomb(this);
        }

        private void CatchOrExplodeOnDummy(MiniVanHotPotatoDummy dummy)
        {
            if (Time.time < ignoreThrowerUntil && dummy.NetworkObjectId == lastThrowerObjectId)
            {
                return;
            }

            int nextThrowCount = Mathf.Max(0, throwCount.Value) + 1;
            throwCount.Value = nextThrowCount;

            if (nextThrowCount >= CurrentThrowLimit)
            {
                ExplodeOnDummy(dummy);
                return;
            }

            holderObjectId.Value = dummy.NetworkObjectId;
            serverHeldStartTime = Time.time;
            returnImmuneObjectId = lastThrowerObjectId;
            returnImmuneUntil = Time.time + Mathf.Max(0f, ReturnImmunitySeconds);
            missedReturnAt = 0f;
            state.Value = (int)BombState.Held;
            dummy.ServerAttachBomb(this);
        }

        private void ScheduleMissedReturn()
        {
            if (!IsServer || (BombState)state.Value != BombState.Flying || missedReturnAt > 0f)
            {
                return;
            }

            missedReturnAt = Time.time + Mathf.Max(0.05f, MissReturnDelay);
        }

        private void UpdateMissedReturn()
        {
            if (!IsServer || missedReturnAt <= 0f || Time.time < missedReturnAt)
            {
                return;
            }

            ReturnToThrowerAfterMiss();
        }

        private void ReturnToThrowerAfterMiss()
        {
            missedReturnAt = 0f;
            if (!IsServer || (BombState)state.Value != BombState.Flying || NetworkManager.Singleton == null)
            {
                return;
            }

            if (lastThrowerObjectId == 0
                || !NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(lastThrowerObjectId, out NetworkObject throwerObject)
                || throwerObject == null)
            {
                state.Value = (int)BombState.Available;
                holderObjectId.Value = 0;
                serverHeldStartTime = 0f;
                ConfigurePhysicsForState(BombState.Available);
                return;
            }

            MiniVanPlayer player = throwerObject.GetComponent<MiniVanPlayer>();
            if (player != null)
            {
                holderObjectId.Value = player.NetworkObjectId;
                serverHeldStartTime = throwCount.Value > 0 ? Time.time : 0f;
                returnImmuneObjectId = 0;
                returnImmuneUntil = 0f;
                state.Value = (int)BombState.Held;
                player.ServerAttachHotPotatoBomb(this);
                return;
            }

            MiniVanHotPotatoDummy dummy = throwerObject.GetComponent<MiniVanHotPotatoDummy>();
            if (dummy != null)
            {
                holderObjectId.Value = dummy.NetworkObjectId;
                serverHeldStartTime = throwCount.Value > 0 ? Time.time : 0f;
                returnImmuneObjectId = 0;
                returnImmuneUntil = 0f;
                state.Value = (int)BombState.Held;
                dummy.ServerAttachBomb(this);
            }
        }

        private bool IsReturnImmuneTarget(ulong targetObjectId)
        {
            return targetObjectId != 0
                && returnImmuneObjectId == targetObjectId
                && Time.time < returnImmuneUntil;
        }

        private void UpdateFlyingCatchProbe()
        {
            if (!IsServer || (BombState)state.Value != BombState.Flying)
            {
                return;
            }

            Vector3 bombPosition = transform.position;
            float catchRadius = Mathf.Max(0.2f, CatchRadius);
            float catchRadiusSqr = catchRadius * catchRadius;

            MiniVanPlayer[] players = FindObjectsByType<MiniVanPlayer>(FindObjectsSortMode.None);
            MiniVanPlayer bestPlayer = null;
            float bestPlayerDistance = float.MaxValue;
            for (int i = 0; i < players.Length; i++)
            {
                MiniVanPlayer player = players[i];
                if (player == null || player.NetworkObjectId == lastThrowerObjectId)
                {
                    continue;
                }

                float distanceSqr = GetPlayerCatchDistanceSqr(player, bombPosition);
                if (distanceSqr <= catchRadiusSqr && distanceSqr < bestPlayerDistance)
                {
                    bestPlayer = player;
                    bestPlayerDistance = distanceSqr;
                }
            }

            if (bestPlayer != null)
            {
                if (IsReturnImmuneTarget(bestPlayer.NetworkObjectId))
                {
                    ScheduleMissedReturn();
                    return;
                }

                CatchOrExplodeOnPlayer(bestPlayer);
                return;
            }

            MiniVanHotPotatoDummy[] dummies = FindObjectsByType<MiniVanHotPotatoDummy>(FindObjectsSortMode.None);
            MiniVanHotPotatoDummy bestDummy = null;
            float bestDummyDistance = float.MaxValue;
            for (int i = 0; i < dummies.Length; i++)
            {
                MiniVanHotPotatoDummy dummy = dummies[i];
                if (dummy == null || dummy.NetworkObjectId == lastThrowerObjectId)
                {
                    continue;
                }

                float distanceSqr = GetCapsuleLikeCatchDistanceSqr(dummy.transform.position, bombPosition);
                if (distanceSqr <= catchRadiusSqr && distanceSqr < bestDummyDistance)
                {
                    bestDummy = dummy;
                    bestDummyDistance = distanceSqr;
                }
            }

            if (bestDummy != null)
            {
                if (IsReturnImmuneTarget(bestDummy.NetworkObjectId))
                {
                    ScheduleMissedReturn();
                    return;
                }

                CatchOrExplodeOnDummy(bestDummy);
            }
        }

        private static float GetPlayerCatchDistanceSqr(MiniVanPlayer player, Vector3 bombPosition)
        {
            CharacterController controller = player != null ? player.CharacterController : null;
            if (controller != null)
            {
                Vector3 worldCenter = player.transform.TransformPoint(controller.center);
                float halfHeight = Mathf.Max(controller.radius, controller.height * 0.5f);
                float vertical = Mathf.Clamp(bombPosition.y, worldCenter.y - halfHeight, worldCenter.y + halfHeight);
                Vector3 closest = new Vector3(worldCenter.x, vertical, worldCenter.z);
                return (bombPosition - closest).sqrMagnitude;
            }

            return GetCapsuleLikeCatchDistanceSqr(player != null ? player.transform.position : Vector3.zero, bombPosition);
        }

        private static float GetCapsuleLikeCatchDistanceSqr(Vector3 targetPosition, Vector3 bombPosition)
        {
            float vertical = Mathf.Clamp(bombPosition.y, targetPosition.y + 0.15f, targetPosition.y + 1.75f);
            Vector3 closest = new Vector3(targetPosition.x, vertical, targetPosition.z);
            return (bombPosition - closest).sqrMagnitude;
        }

        private void UpdateHeldExplosionTimer()
        {
            if (!IsServer || MaxHeldSeconds <= 0f || serverHeldStartTime <= 0f || throwCount.Value <= 0)
            {
                return;
            }

            if (Time.time - serverHeldStartTime < MaxHeldSeconds)
            {
                return;
            }

            ExplodeOnCurrentHolder();
        }

        private void ExplodeOnCurrentHolder()
        {
            if (!IsServer || (BombState)state.Value == BombState.Exploded)
            {
                return;
            }

            if (!TryGetHolderTransform(out Transform holder))
            {
            state.Value = (int)BombState.Exploded;
            holderObjectId.Value = 0;
            ConfigurePhysicsForState(BombState.Exploded);
            PlayExplosionClientRpc(transform.position);
            return;
            }

            MiniVanPlayer player = holder.GetComponent<MiniVanPlayer>();
            if (player != null)
            {
                ExplodeOnPlayer(player);
                return;
            }

            MiniVanHotPotatoDummy dummy = holder.GetComponent<MiniVanHotPotatoDummy>();
            if (dummy != null)
            {
                ExplodeOnDummy(dummy);
                return;
            }

            state.Value = (int)BombState.Exploded;
            holderObjectId.Value = 0;
            ConfigurePhysicsForState(BombState.Exploded);
            PlayExplosionClientRpc(transform.position);
        }

        private void ExplodeOnPlayer(MiniVanPlayer player)
        {
            state.Value = (int)BombState.Exploded;
            holderObjectId.Value = player != null ? player.NetworkObjectId : 0;
            serverHeldStartTime = 0f;
            ConfigurePhysicsForState(BombState.Exploded);
            PlayExplosionClientRpc(transform.position);
            if (player != null)
            {
                player.ServerExplodeHotPotato(PoopSeconds);
            }
        }

        private void ExplodeOnDummy(MiniVanHotPotatoDummy dummy)
        {
            state.Value = (int)BombState.Exploded;
            holderObjectId.Value = dummy != null ? dummy.NetworkObjectId : 0;
            serverHeldStartTime = 0f;
            ConfigurePhysicsForState(BombState.Exploded);
            PlayExplosionClientRpc(transform.position);
            if (dummy != null)
            {
                dummy.ServerExplodeAsPoop(PoopSeconds);
            }
        }

        private void UpdateLocalThrowPrediction()
        {
            if (!localOwnerReleased || Time.time >= localThrowPredictionUntil)
            {
                return;
            }

            transform.position += localThrowPredictionVelocity * Time.deltaTime;
            localThrowPredictionVelocity += Physics.gravity * Time.deltaTime;
        }

        private void FollowHolder()
        {
            if (!TryGetHolderTransform(out Transform holder))
            {
                return;
            }

            MiniVanPlayer player = holder.GetComponent<MiniVanPlayer>();
            if (player != null)
            {
                player.GetHotPotatoBombCarryPose(this, out Vector3 position, out Quaternion rotation);
                SetBombVisualVisible(IsActivated || player.IsInventoryItemSelectedForWorld(MiniVanInventoryItem.HotPotatoBomb));
                SetHeldPose(position, rotation);
                return;
            }

            MiniVanHotPotatoDummy dummy = holder.GetComponent<MiniVanHotPotatoDummy>();
            if (dummy != null)
            {
                dummy.GetBombCarryPose(out Vector3 position, out Quaternion rotation);
                SetHeldPose(position, rotation);
            }
        }

        private void SetHeldPose(Vector3 position, Quaternion rotation)
        {
            transform.SetPositionAndRotation(position, rotation);
            if (body == null)
            {
                return;
            }

            body.position = position;
            body.rotation = rotation;
            if (!body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
        }

        private bool TryGetHolderTransform(out Transform holder)
        {
            holder = null;
            if (holderObjectId.Value == 0 || NetworkManager.Singleton == null)
            {
                return false;
            }

            if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(holderObjectId.Value, out NetworkObject holderObject) || holderObject == null)
            {
                return false;
            }

            holder = holderObject.transform;
            return holder != null;
        }

        private void HandleStateChanged(int previousValue, int newValue)
        {
            if ((BombState)newValue != BombState.Held)
            {
                localThrowPredictionUntil = 0f;
                localOwnerReleased = false;
            }

            ConfigurePhysicsForState((BombState)newValue);
        }

        private void HandleThrowCountChanged(int previousValue, int newValue)
        {
            ApplyBlinkVisual();
        }

        private void HandleExplosionThrowLimitChanged(int previousValue, int newValue)
        {
            ApplyBlinkVisual();
        }

        private void ServerChooseThrowLimit()
        {
            if (!IsServer)
            {
                return;
            }

            int minimum = Mathf.Max(1, MinThrowsBeforeExplosion);
            int maximum = Mathf.Max(minimum, MaxThrowsBeforeExplosion);
            int selectedLimit = UnityEngine.Random.Range(minimum, maximum + 1);
            explosionThrowLimit.Value = selectedLimit;
            ThrowsBeforeExplosion = selectedLimit;
        }

        private void ConfigurePhysicsForState(BombState newState)
        {
            if (body == null)
            {
                body = GetComponent<Rigidbody>();
            }

            if (sphereCollider == null)
            {
                sphereCollider = GetComponent<SphereCollider>();
            }

            bool physical = newState == BombState.Available || newState == BombState.Flying;
            if (sphereCollider != null)
            {
                sphereCollider.radius = Mathf.Max(0.24f, CatchRadius * 0.35f);
                sphereCollider.isTrigger = false;
                sphereCollider.enabled = physical;
            }

            if (body == null)
            {
                return;
            }

            bool flying = physical;
            if (!flying && !body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }

            body.isKinematic = !flying;
            body.useGravity = flying;
            body.detectCollisions = physical;
            body.mass = 0.7f;
            body.linearDamping = newState == BombState.Flying ? 0.02f : 0.25f;
            body.angularDamping = 0.08f;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            SetBombVisualVisible(newState != BombState.Exploded);
        }

        private void EnsureVisual()
        {
            if (transform.Find("Bomb Visual") != null)
            {
                return;
            }

            GameObject visual = new GameObject("Bomb Visual");
            visual.transform.SetParent(transform, false);
            visual.transform.localPosition = Vector3.zero;

            Material bombMaterial = CreateRuntimeMaterial(new Color(0.035f, 0.035f, 0.04f, 1f));
            Material fuseMaterial = CreateRuntimeMaterial(new Color(0.35f, 0.18f, 0.06f, 1f));

            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "Bomb Body";
            sphere.transform.SetParent(visual.transform, false);
            sphere.transform.localScale = Vector3.one * 0.48f;
            SetMaterial(sphere, bombMaterial);
            DisableCollider(sphere);

            GameObject cap = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cap.name = "Bomb Fuse Cap";
            cap.transform.SetParent(visual.transform, false);
            cap.transform.localPosition = new Vector3(0f, 0.27f, 0f);
            cap.transform.localScale = new Vector3(0.09f, 0.045f, 0.09f);
            SetMaterial(cap, fuseMaterial);
            DisableCollider(cap);

            GameObject fuse = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            fuse.name = "Bomb Fuse";
            fuse.transform.SetParent(visual.transform, false);
            fuse.transform.localPosition = new Vector3(0f, 0.43f, 0f);
            fuse.transform.localRotation = Quaternion.Euler(28f, 0f, 18f);
            fuse.transform.localScale = new Vector3(0.025f, 0.16f, 0.025f);
            SetMaterial(fuse, fuseMaterial);
            DisableCollider(fuse);
        }

        private void CacheRenderers()
        {
            renderers = GetComponentsInChildren<Renderer>(true);
            runtimeMaterials = new Material[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    runtimeMaterials[i] = renderers[i].material;
                }
            }
        }

        private void ApplyBlinkVisual()
        {
            if (runtimeMaterials == null || runtimeMaterials.Length == 0)
            {
                CacheRenderers();
            }

            BombState currentState = (BombState)state.Value;
            bool blink = throwCount.Value > 0 && currentState != BombState.Exploded;
            float remaining01 = 1f - Mathf.Clamp01(throwCount.Value / (float)CurrentThrowLimit);
            float interval = Mathf.Lerp(0.08f, 0.42f, remaining01);
            bool red = blink && Mathf.Repeat(Time.time, Mathf.Max(0.03f, interval)) < interval * 0.5f;
            Color color = currentState == BombState.Exploded
                ? new Color(0.18f, 0.18f, 0.18f, 0.35f)
                : red ? Color.red : new Color(0.035f, 0.035f, 0.04f, 1f);

            for (int i = 0; i < runtimeMaterials.Length; i++)
            {
                if (runtimeMaterials[i] != null)
                {
                    runtimeMaterials[i].color = color;
                }
            }
        }

        private void SetBombVisualVisible(bool visible)
        {
            if (renderers == null || renderers.Length == 0)
            {
                CacheRenderers();
            }

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer != null && (playRadiusLine == null || renderer != playRadiusLine))
                {
                    renderer.enabled = visible;
                }
            }
        }

        private void UpdatePlayRadiusVisual()
        {
            if (!ShowPlayRadius || !IsPlayRadiusActive || PlayRadius <= 0f)
            {
                if (playRadiusLine != null)
                {
                    playRadiusLine.enabled = false;
                }

                return;
            }

            EnsurePlayRadiusLine();
            if (playRadiusLine == null)
            {
                return;
            }

            const int segments = 96;
            float radius = Mathf.Max(1f, PlayRadius);
            Vector3 center = PlayRadiusCenter;
            center.y = GetPlayRadiusVisualY(center);

            playRadiusLine.enabled = true;
            playRadiusLine.positionCount = segments + 1;
            for (int i = 0; i <= segments; i++)
            {
                float angle = (i / (float)segments) * Mathf.PI * 2f;
                Vector3 point = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                playRadiusLine.SetPosition(i, point);
            }
        }

        private void EnsurePlayRadiusLine()
        {
            if (playRadiusLine != null)
            {
                return;
            }

            Transform existing = transform.Find("Hot Potato Play Radius");
            GameObject lineObject = existing != null ? existing.gameObject : new GameObject("Hot Potato Play Radius");
            lineObject.transform.SetParent(transform, false);
            playRadiusLine = lineObject.GetComponent<LineRenderer>();
            if (playRadiusLine == null)
            {
                playRadiusLine = lineObject.AddComponent<LineRenderer>();
            }

            playRadiusLine.useWorldSpace = true;
            playRadiusLine.loop = false;
            playRadiusLine.widthMultiplier = 0.08f;
            playRadiusLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            playRadiusLine.receiveShadows = false;

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            Material material = new Material(shader);
            material.name = "Hot Potato Radius Material";
            material.color = new Color(1f, 0.1f, 0.04f, 0.75f);
            playRadiusLine.material = material;
        }

        private float GetPlayRadiusVisualY(Vector3 center)
        {
            RaycastHit[] hits = Physics.RaycastAll(center + Vector3.up * 4f, Vector3.down, 12f, ~0, QueryTriggerInteraction.Ignore);
            float bestDistance = float.MaxValue;
            float bestY = center.y;

            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit hit = hits[i];
                if (hit.collider == null || hit.collider.transform.IsChildOf(transform))
                {
                    continue;
                }

                if (hit.distance < bestDistance)
                {
                    bestDistance = hit.distance;
                    bestY = hit.point.y + 0.08f;
                }
            }

            return bestY;
        }

        [ClientRpc]
        private void PlayExplosionClientRpc(Vector3 position, ClientRpcParams clientRpcParams = default)
        {
            transform.position = position;
            SetBombVisualVisible(false);
            SpawnExplosionEffect(position);
        }

        private void SpawnExplosionEffect(Vector3 position)
        {
            GameObject effect = new GameObject("Hot Potato Bomb Explosion");
            effect.transform.position = position;

            ParticleSystem particles = effect.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = particles.main;
            main.playOnAwake = false;
            main.duration = 0.45f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.22f, 0.55f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(4.2f, 7.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.16f, 0.42f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.42f, 0.06f, 1f), new Color(0.25f, 0.22f, 0.2f, 0.7f));
            main.gravityModifier = 0.25f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f, 34),
                new ParticleSystem.Burst(0.06f, 18)
            });

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.28f;

            ParticleSystemRenderer particleRenderer = particles.GetComponent<ParticleSystemRenderer>();
            if (particleRenderer != null)
            {
                particleRenderer.sharedMaterial = CreateRuntimeMaterial(new Color(1f, 0.36f, 0.04f, 1f));
            }

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.8f, 0.18f), 0f),
                    new GradientColorKey(new Color(1f, 0.22f, 0.04f), 0.35f),
                    new GradientColorKey(new Color(0.08f, 0.08f, 0.08f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0.6f, 0.55f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = gradient;

            Light flash = effect.AddComponent<Light>();
            flash.type = LightType.Point;
            flash.color = new Color(1f, 0.5f, 0.12f, 1f);
            flash.intensity = 6f;
            flash.range = 5.5f;

            ExplosionEffectCleaner cleaner = effect.AddComponent<ExplosionEffectCleaner>();
            cleaner.Light = flash;
            cleaner.Lifetime = 1.4f;
            particles.Play();
        }

        private sealed class ExplosionEffectCleaner : MonoBehaviour
        {
            public Light Light;
            public float Lifetime = 1.4f;
            private float createdAt;

            private void Awake()
            {
                createdAt = Time.time;
            }

            private void Update()
            {
                float t = Mathf.Clamp01((Time.time - createdAt) / Mathf.Max(0.01f, Lifetime));
                if (Light != null)
                {
                    Light.intensity = Mathf.Lerp(6f, 0f, t);
                }

                if (t >= 1f)
                {
                    Destroy(gameObject);
                }
            }
        }

        private static Material CreateRuntimeMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            Material material = new Material(shader);
            material.name = "Bomb Material";
            material.color = color;
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

        private static void DisableCollider(GameObject target)
        {
            Collider collider = target != null ? target.GetComponent<Collider>() : null;
            if (collider != null)
            {
                collider.enabled = false;
            }
        }
    }
}


