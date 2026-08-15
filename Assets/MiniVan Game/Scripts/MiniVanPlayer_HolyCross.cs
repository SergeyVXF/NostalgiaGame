using Unity.Netcode;
using UnityEngine;

namespace MiniVanGame
{
    public partial class MiniVanPlayer
    {
        [Header("Holy Cross")]
        public float HolyCrossKeepDistance = 5f;
        public float HolyCrossConeRange = 12f;
        [Range(5f, 80f)] public float HolyCrossConeHalfAngle = 35f;
        [Tooltip("Debug only: show the transparent holy-cross cone in play mode.")]
        public bool ShowHolyCrossConeDebug = false;
        public Vector3 HolyCrossRestPosition = new Vector3(0.34f, -0.28f, 0.7f);
        public Vector3 HolyCrossRestRotation = new Vector3(12f, -8f, 6f);
        public Vector3 HolyCrossActivePosition = new Vector3(0.12f, -0.08f, 0.95f);
        public Vector3 HolyCrossActiveRotation = new Vector3(-18f, -4f, 2f);
        public GameObject HolyCrossHeldPrefab;
        public GameObject HolyCrossParticlesPrefab;
        public GameObject HolyCrossConePrefab;

        private readonly NetworkVariable<bool> networkHolyCrossActive = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private Transform holyCrossHeldVisual;
        private ParticleSystem holyCrossParticles;
        private GameObject holyCrossConeVisual;
        private float holyCrossPose01;
        private bool localHolyCrossActive;

        /// <summary>
        /// Owner/offline use local press state; remote peers use the synced network flag.
        /// </summary>
        public bool IsHolyCrossRepelling
        {
            get
            {
                if (!IsInventoryItemSelectedForWorld(MiniVanInventoryItem.HolyCross))
                {
                    return false;
                }

                if (IsOwner || !IsSpawned)
                {
                    return localHolyCrossActive;
                }

                return networkHolyCrossActive.Value;
            }
        }

        public bool HasInventoryItemPublic(MiniVanInventoryItem item)
        {
            return HasInventoryItem(item);
        }

        public void RequestTakeHolyCross(MiniVanHolyCrossPickup pickup)
        {
            if (!IsOwner || pickup == null || HasInventoryItem(MiniVanInventoryItem.HolyCross))
            {
                return;
            }

            if (!pickup.IsInReach(transform.position))
            {
                return;
            }

            RequestTakeHolyCrossServerRpc(new NetworkObjectReference(pickup.NetworkObject));
        }

        public bool IsWorldPointInHolyCrossCone(Vector3 worldPoint)
        {
            if (!IsHolyCrossRepelling)
            {
                return false;
            }

            Vector3 origin = transform.position + Vector3.up * 0.35f;
            Vector3 flatTo = Vector3.ProjectOnPlane(worldPoint - origin, Vector3.up);
            float dist = flatTo.magnitude;
            if (dist > Mathf.Max(0.35f, HolyCrossConeRange) || dist < 0.01f)
            {
                return false;
            }

            Vector3 aim = GetHolyCrossAimDirection();
            float dot = Vector3.Dot(aim, flatTo / dist);
            float minDot = Mathf.Cos(Mathf.Clamp(HolyCrossConeHalfAngle, 5f, 80f) * Mathf.Deg2Rad);
            return dot >= minDot;
        }

        public bool DoesHolyCrossBlockPlayerMelee(MiniVanVampire vampire)
        {
            return vampire != null && IsHolyCrossRepelling && IsWorldPointInHolyCrossCone(vampire.transform.position);
        }

        private Vector3 GetHolyCrossAimDirection()
        {
            Vector3 aim = PlayerCamera != null ? PlayerCamera.transform.forward : transform.forward;
            aim = Vector3.ProjectOnPlane(aim, Vector3.up);
            if (aim.sqrMagnitude < 0.001f)
            {
                aim = transform.forward;
            }

            return aim.normalized;
        }

        private void HandleHolyCrossUse()
        {
            if (!IsOwner)
            {
                return;
            }

            bool selected = GetInventorySlot(localSelectedSlot) == MiniVanInventoryItem.HolyCross;
            bool canUse = selected &&
                          currentSeat == null &&
                          currentSkateboard == null &&
                          currentHoverboardM == null &&
                          heldTowCube == null &&
                          !gearDragActive &&
                          !IsDowned;

            bool wantActive = canUse && Input.GetMouseButton(0);
            if (wantActive == localHolyCrossActive)
            {
                return;
            }

            localHolyCrossActive = wantActive;
            if (!IsSpawned)
            {
                return;
            }

            if (IsServer)
            {
                networkHolyCrossActive.Value = wantActive;
            }
            else
            {
                SetHolyCrossActiveServerRpc(wantActive);
            }
        }

        private void UpdateHolyCrossHeldVisual()
        {
            bool selected = IsInventoryItemSelectedForWorld(MiniVanInventoryItem.HolyCross) && currentSeat == null;
            bool active = IsHolyCrossRepelling;

            EnsureHolyCrossHeldVisual();
            if (holyCrossHeldVisual != null)
            {
                holyCrossHeldVisual.gameObject.SetActive(selected);
            }

            float targetPose = selected && active ? 1f : 0f;
            holyCrossPose01 = Mathf.MoveTowards(holyCrossPose01, targetPose, Time.deltaTime * 10f);
            if (holyCrossHeldVisual != null && selected)
            {
                if (UsesSkeletalHeldItems())
                {
                    if (holyCrossHeldVisual.parent != playerRightHand)
                    {
                        holyCrossHeldVisual.SetParent(playerRightHand, false);
                    }

                    holyCrossHeldVisual.localPosition = Vector3.Lerp(
                        CrossHandLocalPosition,
                        CrossHandRaisedPosition,
                        holyCrossPose01);
                    holyCrossHeldVisual.localRotation = Quaternion.Euler(Vector3.Lerp(
                        CrossHandLocalRotation,
                        CrossHandRaisedRotation,
                        holyCrossPose01));
                }
                else
                {
                    holyCrossHeldVisual.localPosition = Vector3.Lerp(
                        HolyCrossRestPosition,
                        HolyCrossActivePosition,
                        holyCrossPose01);
                    holyCrossHeldVisual.localRotation = Quaternion.Euler(Vector3.Lerp(
                        HolyCrossRestRotation,
                        HolyCrossActiveRotation,
                        holyCrossPose01));
                }
            }

            if (holyCrossParticles != null)
            {
                var emission = holyCrossParticles.emission;
                emission.enabled = active;
                if (active && !holyCrossParticles.isPlaying)
                {
                    holyCrossParticles.Play(true);
                }
                else if (!active && holyCrossParticles.isPlaying)
                {
                    holyCrossParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
            }

            UpdateHolyCrossConeVisual(active);
        }

        private void UpdateHolyCrossConeVisual(bool active)
        {
            if (holyCrossConeVisual == null)
            {
                return;
            }

            bool show = active && ShowHolyCrossConeDebug;
            holyCrossConeVisual.SetActive(show);
            if (!show)
            {
                return;
            }

            MeshFilter filter = holyCrossConeVisual.GetComponent<MeshFilter>();
            if (filter != null)
            {
                filter.sharedMesh = MiniVanHolyCrossPickup.BuildConeMeshPublic(
                    Mathf.Max(0.5f, HolyCrossConeRange),
                    Mathf.Clamp(HolyCrossConeHalfAngle, 5f, 80f),
                    28);
            }

            MeshRenderer renderer = holyCrossConeVisual.GetComponent<MeshRenderer>();
            if (renderer != null && renderer.sharedMaterial == null)
            {
                renderer.sharedMaterial = MiniVanHolyCrossPickup.LoadConeMaterial();
            }

            Vector3 aim = GetHolyCrossAimDirection();
            holyCrossConeVisual.transform.position = transform.position + Vector3.up * 0.2f;
            if (aim.sqrMagnitude > 0.001f)
            {
                holyCrossConeVisual.transform.rotation = Quaternion.LookRotation(aim, Vector3.up);
            }
        }

        private void EnsureHolyCrossHeldVisual()
        {
            if (holyCrossHeldVisual != null)
            {
                return;
            }

            Transform parent = GetHeldItemParent();
            if (parent == null)
            {
                return;
            }

            GameObject held;
            if (HolyCrossHeldPrefab != null)
            {
                held = Instantiate(HolyCrossHeldPrefab, parent, false);
                MiniVanHolyCrossPickup.EnsureBuiltVisual(held);
            }
            else
            {
                held = MiniVanHolyCrossPickup.CreateHeldVisual(parent);
            }

            held.name = IsOwner ? "Held Holy Cross" : "Remote Held Holy Cross";
            holyCrossHeldVisual = held.transform;
            holyCrossHeldVisual.localPosition = HolyCrossRestPosition;
            holyCrossHeldVisual.localRotation = Quaternion.Euler(HolyCrossRestRotation);
            holyCrossHeldVisual.localScale = Vector3.one * (IsOwner ? 1f : 0.85f);

            GameObject fxRoot;
            if (HolyCrossParticlesPrefab != null)
            {
                fxRoot = Instantiate(HolyCrossParticlesPrefab, holyCrossHeldVisual, false);
            }
            else
            {
                fxRoot = MiniVanHolyCrossPickup.CreateParticlesObject(holyCrossHeldVisual);
            }

            holyCrossParticles = fxRoot.GetComponentInChildren<ParticleSystem>(true);

            // Cone is created for debug toggles; hidden unless ShowHolyCrossConeDebug is on.
            holyCrossConeVisual = MiniVanHolyCrossPickup.CreateConeVisual(
                transform,
                Mathf.Max(0.5f, HolyCrossConeRange),
                Mathf.Clamp(HolyCrossConeHalfAngle, 5f, 80f));
            holyCrossConeVisual.name = "HolyCrossCone_Runtime";
            holyCrossConeVisual.SetActive(false);
            held.SetActive(false);
        }

        [ServerRpc]
        private void SetHolyCrossActiveServerRpc(bool active, ServerRpcParams rpcParams = default)
        {
            if (!HasInventoryItem(MiniVanInventoryItem.HolyCross))
            {
                networkHolyCrossActive.Value = false;
                return;
            }

            networkHolyCrossActive.Value = active;
        }

        [ServerRpc]
        private void RequestTakeHolyCrossServerRpc(NetworkObjectReference pickupReference, ServerRpcParams rpcParams = default)
        {
            if (HasInventoryItem(MiniVanInventoryItem.HolyCross) ||
                !pickupReference.TryGet(out NetworkObject pickupObject))
            {
                return;
            }

            MiniVanHolyCrossPickup pickup = pickupObject.GetComponent<MiniVanHolyCrossPickup>();
            if (pickup == null || !pickup.IsInReach(transform.position))
            {
                return;
            }

            int emptySlot = FindFirstEmptyInventorySlot();
            if (emptySlot < 0 || !pickup.TryClaim())
            {
                return;
            }

            SetInventorySlot(emptySlot, MiniVanInventoryItem.HolyCross);
            networkSelectedSlot.Value = emptySlot;
            SetLocalInventorySlotClientRpc(emptySlot, (int)MiniVanInventoryItem.HolyCross, BuildOwnerTarget());
        }
    }
}
