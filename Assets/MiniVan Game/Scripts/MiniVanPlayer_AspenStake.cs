using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace MiniVanGame
{
    public partial class MiniVanPlayer
    {
        [Header("Aspen Stake")]
        public float AspenStakeAttackInterval = 0.42f;
        public float AspenStakeAttackRange = 2.1f;
        public float AspenStakeAttackRadius = 0.35f;
        [Range(0f, 0.95f)] public float AspenStakeHitboxStart = 0.28f;
        [Range(0.02f, 0.5f)] public float AspenStakeHitboxDuration = 0.12f;
        public float AspenStakeKnockbackDistance = 1.6f;
        public float AspenStakeKnockbackSeconds = 0.16f;
        // Tip already points along +Z in the mesh — no sideways bat tilt.
        public Vector3 AspenStakeRestPosition = new Vector3(0.28f, -0.22f, 0.55f);
        public Vector3 AspenStakeRestRotation = new Vector3(0f, 0f, 0f);
        public Vector3 AspenStakeWindupPosition = new Vector3(0.28f, -0.2f, 0.38f);
        public Vector3 AspenStakeThrustPosition = new Vector3(0.2f, -0.16f, 0.95f);
        public Vector3 AspenStakeThrustRotation = new Vector3(0f, 0f, 0f);
        public GameObject AspenStakeHeldPrefab;

        private readonly NetworkVariable<int> networkAspenStakeHitsLeft = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private Transform aspenStakeHeldVisual;
        private float nextLocalAspenStakeThrustTime;
        private float nextServerAspenStakeThrustTime;
        private float aspenStakeThrustTimer;
        private int offlineAspenStakeHitsLeft;

        public void RequestTakeAspenStake(MiniVanAspenStakePickup pickup)
        {
            if (!IsOwner || pickup == null || HasInventoryItem(MiniVanInventoryItem.AspenStake))
            {
                return;
            }

            if (!pickup.IsInReach(transform.position))
            {
                return;
            }

            RequestTakeAspenStakeServerRpc(new NetworkObjectReference(pickup.NetworkObject));
        }

        private void HandleAspenStakeUse()
        {
            if (!IsOwner ||
                currentSeat != null ||
                currentSkateboard != null ||
                currentHoverboardM != null ||
                heldTowCube != null ||
                gearDragActive ||
                IsDowned)
            {
                return;
            }

            if (GetInventorySlot(localSelectedSlot) != MiniVanInventoryItem.AspenStake ||
                Time.time < nextLocalAspenStakeThrustTime)
            {
                return;
            }

            if (!Input.GetMouseButtonDown(0))
            {
                return;
            }

            float interval = Mathf.Max(0.05f, AspenStakeAttackInterval);
            nextLocalAspenStakeThrustTime = Time.time + interval;
            aspenStakeThrustTimer = interval;

            // Host / offline: resolve locally. Pure clients go through ServerRpc.
            if (!IsSpawned || IsServer)
            {
                BeginAspenStakeAttack(localSelectedSlot, interval);
            }
            else
            {
                RequestAspenStakeAttackServerRpc(localSelectedSlot);
            }
        }

        private void UpdateAspenStakeHeldVisual()
        {
            EnsureAspenStakeHeldVisual();
            bool shouldShow = IsInventoryItemSelectedForWorld(MiniVanInventoryItem.AspenStake) && currentSeat == null;
            if (aspenStakeHeldVisual != null)
            {
                aspenStakeHeldVisual.gameObject.SetActive(shouldShow);
            }

            if (!shouldShow || aspenStakeHeldVisual == null)
            {
                return;
            }

            if (UsesSkeletalHeldItems())
            {
                if (aspenStakeHeldVisual.parent != playerRightHand)
                {
                    aspenStakeHeldVisual.SetParent(playerRightHand, false);
                }

                aspenStakeHeldVisual.localPosition = StakeHandLocalPosition;
                aspenStakeHeldVisual.localRotation = Quaternion.Euler(StakeHandLocalRotation);
                return;
            }

            if (aspenStakeThrustTimer > 0f)
            {
                aspenStakeThrustTimer = Mathf.Max(0f, aspenStakeThrustTimer - Time.deltaTime);
            }

            float interval = Mathf.Max(0.05f, AspenStakeAttackInterval);
            float t = aspenStakeThrustTimer > 0f
                ? 1f - Mathf.Clamp01(aspenStakeThrustTimer / interval)
                : 0f;

            // Thrust cycle: pull back -> stab forward -> recover.
            Vector3 pos = AspenStakeRestPosition;
            Vector3 rot = AspenStakeRestRotation;
            if (t > 0.001f && t < 0.22f)
            {
                float windup = Mathf.SmoothStep(0f, 1f, t / 0.22f);
                pos = Vector3.Lerp(AspenStakeRestPosition, AspenStakeWindupPosition, windup);
                rot = AspenStakeRestRotation;
            }
            else if (t >= 0.22f && t < 0.55f)
            {
                float stab = Mathf.SmoothStep(0f, 1f, (t - 0.22f) / 0.33f);
                pos = Vector3.Lerp(AspenStakeWindupPosition, AspenStakeThrustPosition, stab);
                rot = Vector3.Lerp(AspenStakeRestRotation, AspenStakeThrustRotation, stab);
            }
            else if (t >= 0.55f)
            {
                float recover = Mathf.SmoothStep(0f, 1f, (t - 0.55f) / 0.45f);
                pos = Vector3.Lerp(AspenStakeThrustPosition, AspenStakeRestPosition, recover);
                rot = Vector3.Lerp(AspenStakeThrustRotation, AspenStakeRestRotation, recover);
            }

            aspenStakeHeldVisual.localPosition = pos;
            aspenStakeHeldVisual.localRotation = Quaternion.Euler(rot);
        }

        private void EnsureAspenStakeHeldVisual()
        {
            if (aspenStakeHeldVisual != null)
            {
                return;
            }

            Transform parent = GetHeldItemParent();
            if (parent == null)
            {
                return;
            }

            if (AspenStakeHeldPrefab == null)
            {
                AspenStakeHeldPrefab = MiniVanAspenStakePickup.ResolveHeldPrefab();
            }

            GameObject held;
            if (AspenStakeHeldPrefab != null)
            {
                held = Instantiate(AspenStakeHeldPrefab, parent, false);
            }
            else
            {
                held = MiniVanAspenStakePickup.CreateHeldVisual(parent);
            }

            held.name = IsOwner ? "Held Aspen Stake" : "Remote Held Aspen Stake";
            aspenStakeHeldVisual = held.transform;
            aspenStakeHeldVisual.localPosition = AspenStakeRestPosition;
            aspenStakeHeldVisual.localRotation = Quaternion.Euler(AspenStakeRestRotation);
            aspenStakeHeldVisual.localScale = Vector3.one * (IsOwner ? 1f : 0.85f);
            held.SetActive(false);
        }

        [ServerRpc]
        private void RequestTakeAspenStakeServerRpc(NetworkObjectReference pickupReference, ServerRpcParams rpcParams = default)
        {
            if (HasInventoryItem(MiniVanInventoryItem.AspenStake) ||
                !pickupReference.TryGet(out NetworkObject pickupObject))
            {
                return;
            }

            MiniVanAspenStakePickup pickup = pickupObject.GetComponent<MiniVanAspenStakePickup>();
            if (pickup == null || !pickup.IsInReach(transform.position))
            {
                return;
            }

            int emptySlot = FindFirstEmptyInventorySlot();
            if (emptySlot < 0 || !pickup.TryClaim())
            {
                return;
            }

            SetInventorySlot(emptySlot, MiniVanInventoryItem.AspenStake);
            networkAspenStakeHitsLeft.Value = MiniVanAspenStakePickup.DefaultHits;
            offlineAspenStakeHitsLeft = MiniVanAspenStakePickup.DefaultHits;
            networkSelectedSlot.Value = emptySlot;
            SetLocalInventorySlotClientRpc(emptySlot, (int)MiniVanInventoryItem.AspenStake, BuildOwnerTarget());
        }

        [ServerRpc]
        private void RequestAspenStakeAttackServerRpc(int slotIndex, ServerRpcParams rpcParams = default)
        {
            BeginAspenStakeAttack(slotIndex, Mathf.Max(0.05f, AspenStakeAttackInterval));
        }

        private void BeginAspenStakeAttack(int slotIndex, float interval)
        {
            if (Time.time < nextServerAspenStakeThrustTime ||
                GetInventorySlot(slotIndex) != MiniVanInventoryItem.AspenStake)
            {
                return;
            }

            EnsureAspenStakeDurability();
            if (GetAspenStakeHitsLeft() <= 0)
            {
                return;
            }

            nextServerAspenStakeThrustTime = Time.time + interval;
            if (IsSpawned)
            {
                PlayAspenStakeThrustClientRpc(interval);
            }
            else
            {
                aspenStakeThrustTimer = interval;
                PlayPlayerStakeStabAnimation();
            }

            StartCoroutine(ResolveAspenStakeHitboxCoroutine(slotIndex, interval));
        }

        private void EnsureAspenStakeDurability()
        {
            if (GetAspenStakeHitsLeft() > 0)
            {
                return;
            }

            // Stake is in inventory but durability was never initialized.
            if (IsServer && IsSpawned)
            {
                networkAspenStakeHitsLeft.Value = MiniVanAspenStakePickup.DefaultHits;
            }

            offlineAspenStakeHitsLeft = MiniVanAspenStakePickup.DefaultHits;
        }

        private int GetAspenStakeHitsLeft()
        {
            if (IsSpawned && IsServer)
            {
                return networkAspenStakeHitsLeft.Value > 0
                    ? networkAspenStakeHitsLeft.Value
                    : offlineAspenStakeHitsLeft;
            }

            if (IsSpawned)
            {
                return networkAspenStakeHitsLeft.Value;
            }

            return offlineAspenStakeHitsLeft;
        }

        private IEnumerator ResolveAspenStakeHitboxCoroutine(int slotIndex, float attackInterval)
        {
            float startDelay = Mathf.Clamp01(AspenStakeHitboxStart) * attackInterval;
            if (startDelay > 0f)
            {
                yield return new WaitForSeconds(startDelay);
            }

            float endTime = Time.time + Mathf.Max(0.02f, AspenStakeHitboxDuration);
            while (Time.time <= endTime)
            {
                if (GetInventorySlot(slotIndex) == MiniVanInventoryItem.AspenStake &&
                    TryResolveAspenStakeHit())
                {
                    yield break;
                }

                yield return null;
            }
        }

        private bool TryResolveAspenStakeHit()
        {
            Vector3 origin = transform.position + Vector3.up * 1.15f;
            Vector3 direction = PlayerCamera != null
                ? PlayerCamera.transform.forward
                : transform.forward;
            if (direction.sqrMagnitude < 0.001f)
            {
                direction = transform.forward;
            }

            direction.Normalize();

            float attackRadius = Mathf.Max(0.08f, AspenStakeAttackRadius);
            float attackRange = Mathf.Max(0.1f, AspenStakeAttackRange);

            MiniVanZombie bestZombie = null;
            float bestDistance = float.MaxValue;

            // Include triggers — vampire bat uses a trigger hurtbox while CC is disabled.
            RaycastHit[] hits = Physics.SphereCastAll(
                origin,
                attackRadius,
                direction,
                attackRange,
                ~0,
                QueryTriggerInteraction.Collide);

            for (int i = 0; i < hits.Length; i++)
            {
                Collider hitCollider = hits[i].collider;
                if (hitCollider == null)
                {
                    continue;
                }

                MiniVanZombie zombie = hitCollider.GetComponentInParent<MiniVanZombie>();
                if (zombie == null || hits[i].distance >= bestDistance)
                {
                    continue;
                }

                bestZombie = zombie;
                bestDistance = hits[i].distance;
            }

            // Same forgiving proximity fallback as the bat (works when CC is off).
            MiniVanZombie[] zombies = MiniVanSceneScan.Get<MiniVanZombie>();
            for (int i = 0; i < zombies.Length; i++)
            {
                MiniVanZombie zombie = zombies[i];
                if (zombie == null)
                {
                    continue;
                }

                Vector3 aimPoint = zombie is MiniVanVampire vampire
                    ? vampire.GetStakeHitPoint()
                    : zombie.transform.position + Vector3.up * 1.0f;
                Vector3 toZombie = aimPoint - origin;
                float distance = toZombie.magnitude;
                if (distance < 0.001f)
                {
                    continue;
                }

                float facing = Vector3.Dot(direction, toZombie / distance);
                if (distance <= attackRange + attackRadius + 0.35f &&
                    facing > 0.2f &&
                    distance < bestDistance)
                {
                    bestZombie = zombie;
                    bestDistance = distance;
                }
            }

            if (bestZombie == null)
            {
                return false;
            }

            int damage = ResolveAspenStakeDamage(bestZombie);
            bestZombie.TakeBatHit(
                damage,
                origin,
                AspenStakeKnockbackDistance,
                AspenStakeKnockbackSeconds,
                fromAspenStake: true);
            ReportEnemyCombatHud(bestZombie);
            ConsumeAspenStakeHit();
            return true;
        }

        private int ResolveAspenStakeDamage(MiniVanZombie zombie)
        {
            // Vampire resolves stake/bat damage itself from shield state + MaxHealth.
            if (zombie is MiniVanVampire vampire)
            {
                return Mathf.Max(1, vampire.MaxHealth);
            }

            return Mathf.Max(1, BatDamage);
        }

        private void ConsumeAspenStakeHit()
        {
            int left = Mathf.Max(0, GetAspenStakeHitsLeft() - 1);
            offlineAspenStakeHitsLeft = left;
            if (IsServer && IsSpawned)
            {
                networkAspenStakeHitsLeft.Value = left;
            }

            if (left > 0)
            {
                return;
            }

            int slot = FindInventorySlot(MiniVanInventoryItem.AspenStake);
            if (slot < 0)
            {
                return;
            }

            SetInventorySlot(slot, MiniVanInventoryItem.None);
            if (IsSpawned)
            {
                SetLocalInventorySlotClientRpc(slot, (int)MiniVanInventoryItem.None, BuildOwnerTarget());
            }
        }

        [ClientRpc]
        private void PlayAspenStakeThrustClientRpc(float attackInterval, ClientRpcParams clientRpcParams = default)
        {
            if (attackInterval > 0.01f)
            {
                aspenStakeThrustTimer = attackInterval;
            }

            PlayPlayerStakeStabAnimation();
            UpdateAspenStakeHeldVisual();
        }
    }
}
