using Unity.Netcode;
using UnityEngine;

namespace MiniVanGame
{
    public partial class MiniVanPlayer
    {
        private const float DefibrillatorUseDistance = 3.2f;
        private const float DefibrillatorAimRadius = 0.85f;

        private Transform defibrillatorHeldVisual;
        private ParticleSystem defibrillatorSparkParticles;
        private float defibrillatorSparkUntil;

        public bool HasDefibrillatorInInventory()
        {
            return HasInventoryItem(MiniVanInventoryItem.Defibrillator);
        }

        public void RequestTakeDefibrillator(MiniVanDefibrillatorPickup pickup)
        {
            if (!IsOwner || pickup == null || HasDefibrillatorInInventory() || IsDowned)
            {
                return;
            }

            if (!pickup.IsInReach(transform.position))
            {
                return;
            }

            RequestTakeDefibrillatorServerRpc(new NetworkObjectReference(pickup.NetworkObject));
        }

        private bool HandleDefibrillatorDropInput()
        {
            if (!IsOwner || equipmentWindowOpen || IsDowned || currentSeat != null)
            {
                return false;
            }

            if (!IsSelectedInventoryItem(MiniVanInventoryItem.Defibrillator))
            {
                return false;
            }

            RequestDropDefibrillatorServerRpc(GetLooseItemDropPosition(), GetLooseItemDropRotation());
            if (!IsServer)
            {
                PredictClearInventoryItem(MiniVanInventoryItem.Defibrillator);
                InvalidateStaticHeldVisuals();
                RefreshStaticHeldVisualsIfNeeded(true);
            }

            return true;
        }

        private void HandleDefibrillatorUse()
        {
            if (!IsOwner)
            {
                return;
            }

            TickDefibrillatorSparks();

            if (!IsSelectedInventoryItem(MiniVanInventoryItem.Defibrillator) ||
                currentSkateboard != null ||
                currentHoverboardM != null ||
                heldTowCube != null ||
                gearDragActive ||
                IsDowned)
            {
                return;
            }

            if (!Input.GetMouseButtonDown(0))
            {
                return;
            }

            // Optimistic sparks immediately (no round-trip).
            PlayDefibrillatorSparksLocal();
            RequestDefibrillatorSparkFxServerRpc();

            MiniVanPlayer target = FindAimedUnconsciousPlayer();
            if (target == null || target == this)
            {
                return;
            }

            RequestDefibrillatorReviveServerRpc(target.OwnerClientId);
        }

        private MiniVanPlayer FindAimedUnconsciousPlayer()
        {
            if (PlayerCamera == null)
            {
                return null;
            }

            Ray ray = new Ray(PlayerCamera.transform.position, PlayerCamera.transform.forward);
            RaycastHit[] hits = Physics.SphereCastAll(
                ray, DefibrillatorAimRadius, DefibrillatorUseDistance, ~0, QueryTriggerInteraction.Collide);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            MiniVanPlayer best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < hits.Length; i++)
            {
                if (ShouldIgnoreAimCollider(hits[i].collider))
                {
                    continue;
                }

                MiniVanPlayer candidate = ResolveDownedPlayerFromCollider(hits[i].collider);
                if (candidate == null || candidate == this || !candidate.IsUnconscious)
                {
                    continue;
                }

                float reach = Vector3.Distance(transform.position, candidate.networkCorpsePosition.Value);
                if (reach > DefibrillatorUseDistance + 0.5f)
                {
                    continue;
                }

                if (hits[i].distance < bestDistance)
                {
                    best = candidate;
                    bestDistance = hits[i].distance;
                }
            }

            return best;
        }

        private static MiniVanPlayer ResolveDownedPlayerFromCollider(Collider collider)
        {
            if (collider == null)
            {
                return null;
            }

            MiniVanPlayerCorpseProxy proxy = collider.GetComponentInParent<MiniVanPlayerCorpseProxy>();
            if (proxy != null && proxy.Player != null)
            {
                return proxy.Player;
            }

            return collider.GetComponentInParent<MiniVanPlayer>();
        }

        private void UpdateDefibrillatorHeldVisual()
        {
            bool show = IsOwner &&
                        GetInventorySlot(localSelectedSlot) == MiniVanInventoryItem.Defibrillator;

            if (!show)
            {
                if (defibrillatorHeldVisual != null)
                {
                    defibrillatorHeldVisual.gameObject.SetActive(false);
                }

                return;
            }

            EnsureDefibrillatorHeldVisual();
            defibrillatorHeldVisual.gameObject.SetActive(true);
        }

        private void EnsureDefibrillatorHeldVisual()
        {
            if (defibrillatorHeldVisual != null)
            {
                return;
            }

            Transform parent = PlayerCamera != null ? PlayerCamera.transform : transform;
            defibrillatorHeldVisual = MiniVanDefibrillatorPickup.CreateHeldTubeVisual(parent);
        }

        private void PlayDefibrillatorSparksLocal()
        {
            EnsureDefibrillatorSparkParticles();
            if (defibrillatorSparkParticles == null)
            {
                return;
            }

            Transform tip = MiniVanDefibrillatorPickup.FindSparkAnchor(defibrillatorHeldVisual);
            if (tip == null)
            {
                tip = PlayerCamera != null ? PlayerCamera.transform : transform;
            }

            // Offset lives on SparkAnchor / DefibSparks prefab — keep local identity here.
            defibrillatorSparkParticles.transform.SetParent(tip, false);
            defibrillatorSparkParticles.transform.localPosition = Vector3.zero;
            defibrillatorSparkParticles.transform.localRotation = Quaternion.identity;
            defibrillatorSparkParticles.transform.localScale = Vector3.one;
            defibrillatorSparkParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            defibrillatorSparkParticles.Play(true);
            defibrillatorSparkUntil = Time.time + 0.45f;
        }

        private void TickDefibrillatorSparks()
        {
            if (defibrillatorSparkParticles == null)
            {
                return;
            }

            if (Time.time >= defibrillatorSparkUntil && defibrillatorSparkParticles.isPlaying)
            {
                defibrillatorSparkParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        private void EnsureDefibrillatorSparkParticles()
        {
            if (defibrillatorSparkParticles != null)
            {
                return;
            }

            GameObject prefab = MiniVanDefibrillatorPickup.LoadSparksPrefab();
            GameObject fx;
            if (prefab != null)
            {
                fx = Object.Instantiate(prefab);
                fx.name = "DefibrillatorSparks";
            }
            else
            {
                fx = new GameObject("DefibrillatorSparks");
                ParticleSystem created = fx.AddComponent<ParticleSystem>();
                ParticleSystem.MainModule main = created.main;
                main.playOnAwake = false;
                main.loop = false;
                main.duration = 0.35f;
                main.startLifetime = 0.12f;
                main.startSpeed = 4.5f;
                main.startSize = 0.06f;
                main.startColor = new Color(0.55f, 0.85f, 1f, 1f);
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.maxParticles = 64;

                ParticleSystem.EmissionModule emission = created.emission;
                emission.rateOverTime = 0f;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 28) });

                ParticleSystem.ShapeModule shape = created.shape;
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = 22f;
                shape.radius = 0.02f;
            }

            fx.transform.SetParent(transform, false);
            defibrillatorSparkParticles = fx.GetComponent<ParticleSystem>();
            if (defibrillatorSparkParticles != null)
            {
                defibrillatorSparkParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        [ServerRpc]
        private void RequestTakeDefibrillatorServerRpc(
            NetworkObjectReference pickupReference,
            ServerRpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId ||
                HasInventoryItem(MiniVanInventoryItem.Defibrillator) ||
                IsDowned)
            {
                return;
            }

            if (!pickupReference.TryGet(out NetworkObject pickupObject))
            {
                return;
            }

            MiniVanDefibrillatorPickup pickup = pickupObject.GetComponent<MiniVanDefibrillatorPickup>();
            if (pickup == null || !pickup.IsAvailable || !pickup.IsInReach(transform.position))
            {
                return;
            }

            int emptySlot = FindFirstEmptyInventorySlot();
            if (emptySlot < 0)
            {
                return;
            }

            if (!pickup.TryClaim())
            {
                return;
            }

            SetInventorySlot(emptySlot, MiniVanInventoryItem.Defibrillator);
            networkSelectedSlot.Value = emptySlot;
            SetLocalInventorySlotClientRpc(emptySlot, (int)MiniVanInventoryItem.Defibrillator, BuildOwnerTarget());
        }

        [ServerRpc]
        private void RequestDropDefibrillatorServerRpc(
            Vector3 dropPosition,
            Quaternion dropRotation,
            ServerRpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId)
            {
                return;
            }

            int slot = FindInventorySlot(MiniVanInventoryItem.Defibrillator);
            if (slot < 0)
            {
                return;
            }

            SetInventorySlot(slot, MiniVanInventoryItem.None);
            SetLocalInventorySlotClientRpc(slot, (int)MiniVanInventoryItem.None, BuildOwnerTarget());
            MiniVanDefibrillatorPickup.ServerSpawn(dropPosition, dropRotation);
        }

        [ServerRpc]
        private void RequestDefibrillatorSparkFxServerRpc(ServerRpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId ||
                !HasInventoryItem(MiniVanInventoryItem.Defibrillator))
            {
                return;
            }

            PlayDefibrillatorSparksClientRpc();
        }

        [ClientRpc]
        private void PlayDefibrillatorSparksClientRpc()
        {
            if (IsOwner)
            {
                // Owner already played optimistically.
                return;
            }

            PlayDefibrillatorSparksLocal();
        }

        [ServerRpc]
        private void RequestDefibrillatorReviveServerRpc(ulong targetClientId, ServerRpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId || IsDowned)
            {
                return;
            }

            if (!HasInventoryItem(MiniVanInventoryItem.Defibrillator))
            {
                return;
            }

            MiniVanPlayer target = FindPlayerByClientId(targetClientId);
            if (target == null || target == this || !target.IsUnconscious)
            {
                return;
            }

            float reach = Vector3.Distance(transform.position, target.networkCorpsePosition.Value);
            if (reach > DefibrillatorUseDistance + 0.75f)
            {
                return;
            }

            Vector3 revivePosition = target.networkCorpsePosition.Value + Vector3.up * 0.15f;
            if (!target.ServerReviveFromDefibrillator(revivePosition))
            {
                return;
            }

            // Consume only on successful revive.
            int slot = FindInventorySlot(MiniVanInventoryItem.Defibrillator);
            if (slot >= 0)
            {
                SetInventorySlot(slot, MiniVanInventoryItem.None);
                SetLocalInventorySlotClientRpc(slot, (int)MiniVanInventoryItem.None, BuildOwnerTarget());
            }
        }
    }
}
