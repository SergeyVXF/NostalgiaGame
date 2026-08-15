using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace MiniVanGame
{
    /// <summary>
    /// Portable car-battery charger. Hold E 1s to pick up, look at floor for ghost preview,
    /// tap E to place (green = ok, red = blocked). Plug free cable end into a wall socket,
    /// seat an AKB_Car on the tray; fills Charge01 0→1 in one minute. Display syncs online.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class MiniVanBatteryCharger : NetworkBehaviour, IMiniVanGameModeInteractable
    {
        private const float PickupReach = 2.35f;
        private const float InteractionReach = 2.35f;
        private const float PlaceMaxDistance = 5.5f;
        private const float PlaceMinFloorNormalY = 0.72f;
        private const float PickupHoldSeconds = 1f;
        private const float OverlapPadding = 0.92f;

        // Camera-local rest pose: bottom-right of the view, out of the crosshair.
        private static readonly Vector3 CarryLocalPosition = new Vector3(0.78f, -0.78f, 1.12f);
        private static readonly Vector3 CarryLocalEuler = new Vector3(12f, 205f, 8f);
        // Third-person (riding) side-mount: right of the body, upper half.
        private static readonly Vector3 RidingCarryLocalPosition = new Vector3(0.55f, 1.15f, 0.12f);
        private static readonly Vector3 RidingCarryLocalEuler = new Vector3(0f, 90f, 0f);
        // Well outside the charger body (box half-extents ~0.55 x / 0.275 z).
        private static readonly Vector3 FreeCableRestLocal = new Vector3(-0.95f, 0.25f, 0.55f);

        public const float SecondsToFullCharge = 60f;
        public const int DisplayNoPower = -1;
        public const int DisplayNoBattery = -2;

        private static readonly Dictionary<MiniVanPlayer, MiniVanBatteryCharger> carriedByPlayer =
            new Dictionary<MiniVanPlayer, MiniVanBatteryCharger>();

        private static readonly Collider[] overlapBuffer = new Collider[32];
        private static Material ghostGreenMaterial;
        private static Material ghostRedMaterial;
        private static Texture2D holdRingTexture;
        private static int holdRingBucket = -1;

        public Transform BatteryPlacementPoint;
        public MiniVanBridgeCableSocket ChargerCableSocket;
        public MiniVanBridgePowerCable PowerCable;
        public TextMesh DisplayText;
        public MiniVanCarBattery InstalledBattery;

        private readonly NetworkVariable<int> networkDisplayCode = new NetworkVariable<int>(
            DisplayNoPower,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<float> networkBatteryCharge01 = new NetworkVariable<float>(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<bool> networkPowered = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<bool> networkHasBattery = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<Vector3> networkPosition = new NetworkVariable<Vector3>(
            Vector3.zero,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<Quaternion> networkRotation = new NetworkVariable<Quaternion>(
            Quaternion.identity,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<bool> networkCarried = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<bool> networkOnShelf = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private Rigidbody body;
        private BoxCollider rootBox;
        private Collider[] colliders;
        private MiniVanPlayer carrier;
        private float nextStatePushTime;
        private float nextTransformPushTime;
        private bool lastLocalPowered;
        private string lastDisplayText = string.Empty;

        private GameObject placementGhost;
        private Renderer[] ghostRenderers;
        private bool placementValid;
        private bool placementHasHit;
        private Vector3 placementPosition;
        private Quaternion placementRotation = Quaternion.identity;
        private MiniVanSkateboardShelf placementTargetShelf;
        private MiniVanSkateboardShelf shelvedOn;
        private float pickupHoldProgress;
        private bool lastGhostValid = true;
        private float nextVehicleIgnoreRefreshTime;

        public bool IsCarried => carrier != null || (IsSpawned && networkCarried.Value);

        public bool HasBattery =>
            InstalledBattery != null && InstalledBattery.IsInstalledOnCharger(this);

        public bool IsPowered =>
            IsSpawned ? networkPowered.Value : IsLocalCablePowered();

        public static MiniVanBatteryCharger GetCarriedBy(MiniVanPlayer player)
        {
            return player != null && carriedByPlayer.TryGetValue(player, out MiniVanBatteryCharger charger)
                ? charger
                : null;
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            rootBox = GetComponent<BoxCollider>();
            colliders = GetComponentsInChildren<Collider>(true);
            EnsureDisplayText();
            ConfigurePlacedBody();
            if (ChargerCableSocket != null)
            {
                ChargerCableSocket.OwnerCharger = this;
            }

            AnchorPermanentCableEnd();
            lastLocalPowered = IsLocalCablePowered();
            RefreshDisplayLocal();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            networkDisplayCode.OnValueChanged += OnDisplayCodeChanged;
            networkBatteryCharge01.OnValueChanged += OnBatteryChargeChanged;
            networkCarried.OnValueChanged += OnNetworkCarriedChanged;
            if (IsServer)
            {
                networkPosition.Value = transform.position;
                networkRotation.Value = transform.rotation;
                networkPowered.Value = IsLocalCablePowered();
                networkHasBattery.Value = HasBattery;
                if (HasBattery)
                {
                    networkBatteryCharge01.Value = Mathf.Clamp01(InstalledBattery.Charge01);
                }

                PushDisplayToNetwork(true);
            }
            else if (!networkCarried.Value)
            {
                transform.SetPositionAndRotation(networkPosition.Value, networkRotation.Value);
                ConfigurePlacedBody();
            }

            SetCableActive(carrier == null && !networkCarried.Value);
            RefreshDisplayLocal();
        }

        public override void OnNetworkDespawn()
        {
            networkDisplayCode.OnValueChanged -= OnDisplayCodeChanged;
            networkBatteryCharge01.OnValueChanged -= OnBatteryChargeChanged;
            networkCarried.OnValueChanged -= OnNetworkCarriedChanged;
            DestroyPlacementGhost();
            base.OnNetworkDespawn();
        }

        private void OnDestroy()
        {
            DestroyPlacementGhost();
            if (carrier != null)
            {
                carriedByPlayer.Remove(carrier);
                carrier = null;
            }
        }

        private void Update()
        {
            ClearStaleBattery();
            if (carrier == null)
            {
                AnchorPermanentCableEnd();
            }

            SyncLocalPowerToNetwork();

            if (IsServer || !IsSpawned)
            {
                TickCharging(Time.deltaTime);
                if (Time.time >= nextStatePushTime)
                {
                    nextStatePushTime = Time.time + 0.12f;
                    PushDisplayToNetwork(false);
                }
            }

            if (IsSpawned && IsServer && carrier == null && !networkCarried.Value && Time.time >= nextTransformPushTime)
            {
                nextTransformPushTime = Time.time + 0.2f;
                if ((networkPosition.Value - transform.position).sqrMagnitude > 0.0004f ||
                    Quaternion.Angle(networkRotation.Value, transform.rotation) > 1f)
                {
                    networkPosition.Value = transform.position;
                    networkRotation.Value = transform.rotation;
                }
            }

            if (IsSpawned && !IsServer && carrier == null && !networkCarried.Value && body != null && body.isKinematic)
            {
                if (networkOnShelf.Value)
                {
                    if (shelvedOn == null)
                    {
                        shelvedOn = FindNearestShelf(networkPosition.Value, 2.8f);
                    }

                    SnapToShelfIfNeeded();
                }
                else
                {
                    transform.SetPositionAndRotation(
                        Vector3.Lerp(transform.position, networkPosition.Value, 1f - Mathf.Exp(-14f * Time.deltaTime)),
                        Quaternion.Slerp(transform.rotation, networkRotation.Value, 1f - Mathf.Exp(-14f * Time.deltaTime)));
                }
            }

            ApplyNetworkChargeToLocalBattery();
            RefreshDisplayLocal();
            UpdatePickupHold();
        }

        private void LateUpdate()
        {
            if (InstalledBattery != null && InstalledBattery.IsInstalledOnCharger(this))
            {
                InstalledBattery.SnapToCharger(this);
            }

            if (carrier == null)
            {
                HidePlacementGhost();
                SnapToShelfIfNeeded();
                if (Time.time >= nextVehicleIgnoreRefreshTime)
                {
                    nextVehicleIgnoreRefreshTime = Time.time + 0.75f;
                    IgnoreCollisionsWithVehicles();
                }

                if (PowerCable != null && PowerCable.gameObject.activeInHierarchy)
                {
                    AnchorPermanentCableEnd();
                    EnforceFreeCableEndOutside();
                }

                return;
            }

            ApplyCarryPose(carrier);
            // Cable stays disabled while carried — no physics / no plug flopping.

            if (carrier != MiniVanPlayer.LocalPlayer)
            {
                HidePlacementGhost();
                return;
            }

            UpdatePlacementPreview(carrier);
            if (MiniVanKeyBindings.GetKeyDown(MiniVanKeyAction.Interact) && placementHasHit && placementValid)
            {
                PlaceAtPreview(carrier);
            }
        }

        private void OnGUI()
        {
            MiniVanPlayer local = MiniVanPlayer.LocalPlayer;
            if (local == null)
            {
                return;
            }

            if (carrier == local)
            {
                string placePrompt;
                if (!placementHasHit)
                {
                    placePrompt = "Look at floor or skateboard shelf";
                }
                else if (!placementValid)
                {
                    placePrompt = "No space";
                }
                else if (placementTargetShelf != null)
                {
                    placePrompt = "E - place on shelf";
                }
                else
                {
                    placePrompt = "E - place charger";
                }

                if (local.IsRidingBoard)
                {
                    placePrompt += "   |   Q - drop";
                }

                GUI.Box(new Rect(Screen.width * 0.5f - 160f, Screen.height - 118f, 320f, 34f), placePrompt);
                return;
            }

            if (pickupHoldProgress > 0.01f && !IsCarried)
            {
                Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f + 72f);
                DrawHoldRing(center, 78f, pickupHoldProgress);
            }
        }

        public string GetPrompt(MiniVanPlayer player)
        {
            if (player == null || GetCarriedBy(player) == this)
            {
                return string.Empty;
            }

            if (IsCarried || Vector3.Distance(player.transform.position, transform.position) > InteractionReach)
            {
                return string.Empty;
            }

            MiniVanCarBattery carriedBattery = MiniVanCarBattery.GetCarriedBy(player);
            if (carriedBattery != null && !HasBattery)
            {
                return "E - insert car battery";
            }

            if (carriedBattery == null &&
                MiniVanBridgeBattery.GetCarriedBy(player) == null &&
                !MiniVanBridgePowerCable.HasCarriedEnd(player) &&
                GetCarriedBy(player) == null)
            {
                if (HasBattery)
                {
                    return "E - remove battery";
                }

                if (CanOfferFreeCable(player))
                {
                    return "E - take cable   |   Hold E - charger";
                }

                return "Hold E - take charger";
            }

            return string.Empty;
        }

        public void Interact(MiniVanPlayer player)
        {
            if (player == null || Input.GetMouseButton(1) || GetCarriedBy(player) == this)
            {
                return;
            }

            ClearStaleBattery();
            MiniVanCarBattery carriedBattery = MiniVanCarBattery.GetCarriedBy(player);
            if (carriedBattery != null && !HasBattery)
            {
                AttachBattery(carriedBattery);
                pickupHoldProgress = 0f;
                return;
            }

            if (HasBattery &&
                !MiniVanBridgePowerCable.HasCarriedEnd(player) &&
                MiniVanBridgeBattery.GetCarriedBy(player) == null &&
                GetCarriedBy(player) == null)
            {
                InstalledBattery.TryPickup(player);
                pickupHoldProgress = 0f;
            }

            // Cable tap vs charger hold are handled in UpdatePickupHold (KeyUp vs hold).
        }

        public void PrimaryAction(MiniVanPlayer player)
        {
        }

        public bool TryPickup(MiniVanPlayer player)
        {
            if (player == null || carrier != null)
            {
                return false;
            }

            if (GetCarriedBy(player) != null ||
                MiniVanCarBattery.GetCarriedBy(player) != null ||
                MiniVanBridgeBattery.GetCarriedBy(player) != null ||
                MiniVanBridgePowerCable.HasCarriedEnd(player) ||
                MiniVanWoodenBoard.GetCarriedBy(player) != null ||
                MiniVanWinchHook.GetCarriedBy(player) != null)
            {
                return false;
            }

            if (HasBattery)
            {
                return false;
            }

            if (Vector3.Distance(player.transform.position, transform.position) > PickupReach)
            {
                return false;
            }

            UnplugFreeCableEnd();
            ClearShelfBinding();
            carriedByPlayer[player] = this;
            carrier = player;
            transform.SetParent(null, true);
            ConfigureCarriedBody();
            ApplyCarryPose(player);
            SetCableActive(false);
            pickupHoldProgress = 0f;

            if (IsSpawned)
            {
                if (IsServer)
                {
                    networkCarried.Value = true;
                }
                else
                {
                    RequestCarryServerRpc(true);
                }
            }

            return true;
        }

        public void PlaceAtPreview(MiniVanPlayer player)
        {
            if (carrier != player || !placementHasHit || !placementValid)
            {
                return;
            }

            if (carrier != null)
            {
                carriedByPlayer.Remove(carrier);
                carrier = null;
            }

            // Force a clean upright pose — never keep carry-camera tilt.
            Quaternion flatRotation = SanitizePlacementRotation(placementRotation);
            Vector3 flatPosition = placementPosition;

            transform.SetParent(null, true);
            transform.SetPositionAndRotation(flatPosition, flatRotation);
            ConfigurePlacedBody();

            shelvedOn = placementTargetShelf;
            if (shelvedOn != null)
            {
                SnapToShelfPose(shelvedOn);
            }

            SetCableActive(true);
            AnchorPermanentCableEnd();
            EnforceFreeCableEndOutside();
            HidePlacementGhost();
            pickupHoldProgress = 0f;

            if (IsSpawned)
            {
                if (IsServer)
                {
                    networkCarried.Value = false;
                    networkOnShelf.Value = shelvedOn != null;
                    networkPosition.Value = transform.position;
                    networkRotation.Value = transform.rotation;
                }
                else
                {
                    RequestPlaceServerRpc(transform.position, transform.rotation, shelvedOn != null);
                }
            }
        }

        /// <summary>
        /// Free drop (no placement preview): puts the charger on the ground just ahead of the
        /// player. Used for the riding Q-tap drop where precise placement is impossible.
        /// </summary>
        public void DropNear(MiniVanPlayer player)
        {
            if (player == null || carrier != player)
            {
                return;
            }

            carriedByPlayer.Remove(carrier);
            carrier = null;

            Vector3 flatForward = player.transform.forward;
            flatForward.y = 0f;
            flatForward = flatForward.sqrMagnitude > 0.001f ? flatForward.normalized : Vector3.forward;

            Vector3 dropPosition = player.transform.position + flatForward * 1.1f;
            Vector3 probeStart = dropPosition + Vector3.up * 1.4f;
            RaycastHit[] groundHits = Physics.RaycastAll(
                probeStart, Vector3.down, 8f, ~0, QueryTriggerInteraction.Ignore);
            float bestDistance = float.MaxValue;
            for (int i = 0; i < groundHits.Length; i++)
            {
                Collider hitCollider = groundHits[i].collider;
                if (hitCollider == null ||
                    player.ShouldIgnoreAimCollider(hitCollider) ||
                    hitCollider.transform.IsChildOf(transform))
                {
                    continue;
                }

                if (groundHits[i].distance < bestDistance)
                {
                    bestDistance = groundHits[i].distance;
                    dropPosition = groundHits[i].point + Vector3.up * 0.02f;
                }
            }

            Quaternion flatRotation = SanitizePlacementRotation(
                Quaternion.LookRotation(flatForward, Vector3.up));

            transform.SetParent(null, true);
            transform.SetPositionAndRotation(dropPosition, flatRotation);
            ConfigurePlacedBody();
            shelvedOn = null;

            SetCableActive(true);
            AnchorPermanentCableEnd();
            EnforceFreeCableEndOutside();
            HidePlacementGhost();
            pickupHoldProgress = 0f;

            if (IsSpawned)
            {
                if (IsServer)
                {
                    networkCarried.Value = false;
                    networkOnShelf.Value = false;
                    networkPosition.Value = transform.position;
                    networkRotation.Value = transform.rotation;
                }
                else
                {
                    RequestPlaceServerRpc(transform.position, transform.rotation, false);
                }
            }
        }

        public bool AttachBattery(MiniVanCarBattery battery)
        {
            ClearStaleBattery();
            if (battery == null || HasBattery)
            {
                return false;
            }

            InstalledBattery = battery;
            battery.PlaceIntoCharger(this);
            float charge = Mathf.Clamp01(battery.Charge01);

            if (IsServer || !IsSpawned)
            {
                if (IsSpawned)
                {
                    networkHasBattery.Value = true;
                    networkBatteryCharge01.Value = charge;
                    PushDisplayToNetwork(true);
                }
            }
            else
            {
                NotifyBatteryChangedServerRpc(true, charge);
            }

            return true;
        }

        public void DetachBattery(MiniVanCarBattery battery)
        {
            if (InstalledBattery != battery)
            {
                return;
            }

            float charge = battery != null ? Mathf.Clamp01(battery.Charge01) : 0f;
            if (IsSpawned && !IsServer)
            {
                charge = networkBatteryCharge01.Value;
                if (battery != null)
                {
                    battery.Charge01 = charge;
                }
            }

            InstalledBattery = null;
            battery?.ClearInstalledCharger(this);

            if (IsServer || !IsSpawned)
            {
                if (IsSpawned)
                {
                    networkHasBattery.Value = false;
                    networkBatteryCharge01.Value = charge;
                    PushDisplayToNetwork(true);
                }
            }
            else
            {
                NotifyBatteryChangedServerRpc(false, charge);
            }
        }

        public Transform GetBatterySocket()
        {
            return BatteryPlacementPoint != null ? BatteryPlacementPoint : transform;
        }

        private void UpdatePickupHold()
        {
            MiniVanPlayer local = MiniVanPlayer.LocalPlayer;
            if (carrier != null || local == null || IsCarried)
            {
                pickupHoldProgress = 0f;
                return;
            }

            bool aimed = IsAimedBy(local);
            bool canHoldCharger = CanStartPickupHold(local);
            bool canCable = CanOfferFreeCable(local);

            if (MiniVanKeyBindings.GetKey(MiniVanKeyAction.Interact) && aimed && canHoldCharger)
            {
                pickupHoldProgress += Time.deltaTime / PickupHoldSeconds;
                if (pickupHoldProgress < 1f)
                {
                    return;
                }

                pickupHoldProgress = 0f;
                TryPickup(local);
                return;
            }

            // Quick tap (released before the hold completes) grabs the free cable plug.
            if (MiniVanKeyBindings.GetKeyUp(MiniVanKeyAction.Interact) &&
                aimed &&
                canCable &&
                pickupHoldProgress > 0.01f &&
                pickupHoldProgress < 0.85f)
            {
                int freeIndex = PowerCable.GetFreeEndIndex();
                PowerCable.DetachEndToPlayer(freeIndex, local);
            }

            pickupHoldProgress = 0f;
        }

        private bool CanStartPickupHold(MiniVanPlayer player)
        {
            if (player == null || HasBattery)
            {
                return false;
            }

            if (MiniVanCarBattery.GetCarriedBy(player) != null ||
                MiniVanBridgeBattery.GetCarriedBy(player) != null ||
                MiniVanBridgePowerCable.HasCarriedEnd(player) ||
                MiniVanWoodenBoard.GetCarriedBy(player) != null ||
                MiniVanWinchHook.GetCarriedBy(player) != null ||
                GetCarriedBy(player) != null)
            {
                return false;
            }

            return Vector3.Distance(player.transform.position, transform.position) <= PickupReach;
        }

        private bool IsAimedBy(MiniVanPlayer player)
        {
            if (player == null || player.PlayerCamera == null)
            {
                return false;
            }

            // Riding: third-person cam can't precisely aim at the charger, being close is enough.
            if (player.IsRidingBoard)
            {
                return Vector3.Distance(player.transform.position, transform.position) <= PickupReach;
            }

            Ray ray = new Ray(player.PlayerCamera.transform.position, player.PlayerCamera.transform.forward);
            RaycastHit[] hits = Physics.RaycastAll(
                ray, InteractionReach + 0.35f, ~0, QueryTriggerInteraction.Collide);
            if (hits == null || hits.Length == 0)
            {
                return false;
            }

            for (int i = 0; i < hits.Length; i++)
            {
                Collider hitCollider = hits[i].collider;
                if (player.ShouldIgnoreAimCollider(hitCollider))
                {
                    continue;
                }

                if (hitCollider.transform.IsChildOf(transform) || hitCollider.transform == transform)
                {
                    return true;
                }
            }

            return false;
        }

        private void UpdatePlacementPreview(MiniVanPlayer player)
        {
            placementHasHit = false;
            placementValid = false;
            placementTargetShelf = null;
            if (player == null || player.PlayerCamera == null)
            {
                HidePlacementGhost();
                return;
            }

            Ray ray = new Ray(player.PlayerCamera.transform.position, player.PlayerCamera.transform.forward);

            // Prefer the van skateboard shelf (trigger collider).
            RaycastHit[] triggerHits = Physics.RaycastAll(ray, PlaceMaxDistance, ~0, QueryTriggerInteraction.Collide);
            System.Array.Sort(triggerHits, (a, b) => a.distance.CompareTo(b.distance));
            for (int i = 0; i < triggerHits.Length; i++)
            {
                Collider hitCollider = triggerHits[i].collider;
                if (hitCollider == null ||
                    hitCollider.transform.IsChildOf(transform) ||
                    hitCollider.transform.IsChildOf(player.transform) ||
                    (placementGhost != null && hitCollider.transform.IsChildOf(placementGhost.transform)))
                {
                    continue;
                }

                MiniVanSkateboardShelf shelf = hitCollider.GetComponentInParent<MiniVanSkateboardShelf>();
                if (shelf == null)
                {
                    continue;
                }

                placementTargetShelf = shelf;
                placementRotation = BuildSurfaceRotation(shelf.transform.up, player.transform.forward);
                placementPosition = ComputePlacementPosition(shelf.AnchorPosition, placementRotation);
                placementHasHit = true;
                placementValid = true;
                ShowPlacementGhost();
                return;
            }

            RaycastHit[] hits = Physics.RaycastAll(ray, PlaceMaxDistance, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            RaycastHit? floorHit = null;
            for (int i = 0; i < hits.Length; i++)
            {
                Collider hitCollider = hits[i].collider;
                if (hitCollider == null)
                {
                    continue;
                }

                if (hitCollider.transform.IsChildOf(transform) ||
                    hitCollider.transform.IsChildOf(player.transform) ||
                    (PowerCable != null && hitCollider.transform.IsChildOf(PowerCable.transform)) ||
                    (placementGhost != null && hitCollider.transform.IsChildOf(placementGhost.transform)))
                {
                    continue;
                }

                if (hits[i].normal.y < PlaceMinFloorNormalY)
                {
                    continue;
                }

                floorHit = hits[i];
                break;
            }

            if (!floorHit.HasValue)
            {
                HidePlacementGhost();
                return;
            }

            RaycastHit hit = floorHit.Value;
            // Align to surface normal so the charger never keeps carry-camera tilt.
            placementRotation = BuildSurfaceRotation(hit.normal, player.transform.forward);
            placementPosition = ComputePlacementPosition(hit.point, placementRotation);
            placementHasHit = true;
            placementValid = IsPlacementClear(placementPosition, placementRotation, player);
            ShowPlacementGhost();
        }

        private void ShowPlacementGhost()
        {
            EnsurePlacementGhost();
            if (placementGhost == null)
            {
                return;
            }

            placementGhost.SetActive(true);
            placementGhost.transform.SetPositionAndRotation(placementPosition, placementRotation);
            if (lastGhostValid != placementValid)
            {
                lastGhostValid = placementValid;
                ApplyGhostColor(placementValid);
            }
        }

        private static Quaternion BuildSurfaceRotation(Vector3 surfaceUp, Vector3 preferredForward)
        {
            Vector3 up = surfaceUp.sqrMagnitude > 0.001f ? surfaceUp.normalized : Vector3.up;
            // For nearly-flat ground, lock to world up so we never inherit a crooked normal.
            if (up.y >= 0.92f)
            {
                up = Vector3.up;
            }

            Vector3 forward = Vector3.ProjectOnPlane(preferredForward, up);
            if (forward.sqrMagnitude < 0.001f)
            {
                forward = Vector3.ProjectOnPlane(Vector3.forward, up);
            }

            if (forward.sqrMagnitude < 0.001f)
            {
                forward = Vector3.ProjectOnPlane(Vector3.right, up);
            }

            return Quaternion.LookRotation(forward.normalized, up);
        }

        private static Quaternion SanitizePlacementRotation(Quaternion rotation)
        {
            Vector3 up = rotation * Vector3.up;
            Vector3 forward = rotation * Vector3.forward;
            return BuildSurfaceRotation(up, forward);
        }

        private Vector3 ComputePlacementPosition(Vector3 floorPoint, Quaternion rotation)
        {
            if (rootBox == null)
            {
                return floorPoint;
            }

            // Use absolute scale only — never the tilted carry-pose axes.
            Vector3 lossy = new Vector3(
                Mathf.Abs(transform.lossyScale.x),
                Mathf.Abs(transform.lossyScale.y),
                Mathf.Abs(transform.lossyScale.z));
            Vector3 localBottom = rootBox.center - Vector3.up * (rootBox.size.y * 0.5f);
            Vector3 scaledBottom = Vector3.Scale(localBottom, lossy);
            return floorPoint - rotation * scaledBottom;
        }

        private bool IsPlacementClear(Vector3 position, Quaternion rotation, MiniVanPlayer player)
        {
            if (rootBox == null)
            {
                return true;
            }

            Vector3 lossy = new Vector3(
                Mathf.Abs(transform.lossyScale.x),
                Mathf.Abs(transform.lossyScale.y),
                Mathf.Abs(transform.lossyScale.z));
            Vector3 halfExtents = Vector3.Scale(rootBox.size * 0.5f, lossy) * OverlapPadding;
            Vector3 worldCenter = position + rotation * Vector3.Scale(rootBox.center, lossy);
            int count = Physics.OverlapBoxNonAlloc(
                worldCenter,
                halfExtents,
                overlapBuffer,
                rotation,
                ~0,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                Collider other = overlapBuffer[i];
                if (other == null)
                {
                    continue;
                }

                if (other.transform.IsChildOf(transform) ||
                    (player != null && other.transform.IsChildOf(player.transform)) ||
                    (PowerCable != null && other.transform.IsChildOf(PowerCable.transform)) ||
                    (placementGhost != null && other.transform.IsChildOf(placementGhost.transform)))
                {
                    continue;
                }

                if (other.GetComponentInParent<MiniVanSkateboardShelf>() != null)
                {
                    continue;
                }

                return false;
            }

            return true;
        }

        private void EnsurePlacementGhost()
        {
            if (placementGhost != null)
            {
                return;
            }

            placementGhost = new GameObject("AKB_Recharger_PlacementGhost");
            placementGhost.hideFlags = HideFlags.HideAndDontSave;

            MeshFilter[] filters = GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < filters.Length; i++)
            {
                MeshFilter filter = filters[i];
                if (filter == null || filter.sharedMesh == null)
                {
                    continue;
                }

                if (ShouldExcludeFromGhost(filter.transform))
                {
                    continue;
                }

                GameObject part = new GameObject(filter.name + "_Ghost");
                part.transform.SetParent(placementGhost.transform, false);
                part.transform.position = filter.transform.position;
                part.transform.rotation = filter.transform.rotation;
                part.transform.localScale = filter.transform.lossyScale;

                MeshFilter ghostFilter = part.AddComponent<MeshFilter>();
                ghostFilter.sharedMesh = filter.sharedMesh;
                MeshRenderer ghostRenderer = part.AddComponent<MeshRenderer>();
                ghostRenderer.sharedMaterial = GetGhostMaterial(true);
                ghostRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                ghostRenderer.receiveShadows = false;
            }

            // Re-parent parts relative to ghost root at identity, then sync each frame via world pose.
            // Capture relative poses from current charger transform.
            Transform[] parts = new Transform[placementGhost.transform.childCount];
            Vector3[] localPositions = new Vector3[parts.Length];
            Quaternion[] localRotations = new Quaternion[parts.Length];
            Vector3[] localScales = new Vector3[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                parts[i] = placementGhost.transform.GetChild(i);
                localPositions[i] = transform.InverseTransformPoint(parts[i].position);
                localRotations[i] = Quaternion.Inverse(transform.rotation) * parts[i].rotation;
                localScales[i] = parts[i].lossyScale;
            }

            for (int i = 0; i < parts.Length; i++)
            {
                parts[i].localPosition = localPositions[i];
                parts[i].localRotation = localRotations[i];
                Vector3 parentScale = transform.lossyScale;
                parts[i].localScale = new Vector3(
                    SafeDiv(localScales[i].x, parentScale.x),
                    SafeDiv(localScales[i].y, parentScale.y),
                    SafeDiv(localScales[i].z, parentScale.z));
            }

            ghostRenderers = placementGhost.GetComponentsInChildren<Renderer>(true);
            lastGhostValid = true;
            ApplyGhostColor(true);
            placementGhost.SetActive(false);
        }

        private bool ShouldExcludeFromGhost(Transform target)
        {
            if (target == null)
            {
                return true;
            }

            if (PowerCable != null && target.IsChildOf(PowerCable.transform))
            {
                return true;
            }

            if (target.GetComponent<TextMesh>() != null || target.GetComponentInParent<TextMesh>() != null)
            {
                // Keep indicator mesh; skip only the ChargeText TextMesh object itself.
                if (DisplayText != null && (target == DisplayText.transform || target.IsChildOf(DisplayText.transform)))
                {
                    return true;
                }
            }

            return false;
        }

        private void ApplyGhostColor(bool valid)
        {
            Material material = GetGhostMaterial(valid);
            if (ghostRenderers == null)
            {
                return;
            }

            for (int i = 0; i < ghostRenderers.Length; i++)
            {
                if (ghostRenderers[i] != null)
                {
                    ghostRenderers[i].sharedMaterial = material;
                }
            }
        }

        private void HidePlacementGhost()
        {
            if (placementGhost != null)
            {
                placementGhost.SetActive(false);
            }

            placementHasHit = false;
            placementValid = false;
        }

        private void DestroyPlacementGhost()
        {
            if (placementGhost != null)
            {
                Destroy(placementGhost);
                placementGhost = null;
                ghostRenderers = null;
            }
        }

        private static Material GetGhostMaterial(bool valid)
        {
            if (valid)
            {
                if (ghostGreenMaterial == null)
                {
                    ghostGreenMaterial = CreateGhostMaterial(new Color(0.15f, 1f, 0.3f, 0.42f));
                }

                return ghostGreenMaterial;
            }

            if (ghostRedMaterial == null)
            {
                ghostRedMaterial = CreateGhostMaterial(new Color(1f, 0.2f, 0.15f, 0.42f));
            }

            return ghostRedMaterial;
        }

        private static Material CreateGhostMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            Material material = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave,
                color = color,
                renderQueue = 3000
            };

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f);
            }

            if (material.HasProperty("_Blend"))
            {
                material.SetFloat("_Blend", 0f);
            }

            if (material.HasProperty("_ZWrite"))
            {
                material.SetFloat("_ZWrite", 0f);
            }

            if (material.HasProperty("_SrcBlend"))
            {
                material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            }

            if (material.HasProperty("_DstBlend"))
            {
                material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            }

            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
            material.SetOverrideTag("RenderType", "Transparent");
            return material;
        }

        private static float SafeDiv(float a, float b)
        {
            return Mathf.Abs(b) < 0.0001f ? a : a / b;
        }

        private static void DrawHoldRing(Vector2 center, float diameter, float progress)
        {
            Texture2D texture = GetHoldRingTexture(progress);
            if (texture == null)
            {
                return;
            }

            Color old = GUI.color;
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(center.x - diameter * 0.5f, center.y - diameter * 0.5f, diameter, diameter), texture);
            GUI.color = old;
        }

        private static Texture2D GetHoldRingTexture(float progress)
        {
            const int size = 96;
            int bucket = Mathf.Clamp(Mathf.RoundToInt(progress * 100f), 0, 100);
            if (holdRingTexture != null && holdRingBucket == bucket)
            {
                return holdRingTexture;
            }

            if (holdRingTexture == null)
            {
                holdRingTexture = new Texture2D(size, size, TextureFormat.ARGB32, false)
                {
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave
                };
            }

            holdRingBucket = bucket;
            Color clear = new Color(0f, 0f, 0f, 0f);
            Color track = new Color(0f, 0f, 0f, 0.45f);
            Color fill = new Color(0.25f, 0.85f, 0.45f, 0.95f);
            float radius = size * 0.5f - 2f;
            float inner = radius - 10f;
            float angleMax = progress * Mathf.PI * 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - size * 0.5f;
                    float dy = y + 0.5f - size * 0.5f;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    if (dist < inner || dist > radius)
                    {
                        holdRingTexture.SetPixel(x, y, clear);
                        continue;
                    }

                    float angle = Mathf.Atan2(dx, -dy);
                    if (angle < 0f)
                    {
                        angle += Mathf.PI * 2f;
                    }

                    holdRingTexture.SetPixel(x, y, angle <= angleMax ? fill : track);
                }
            }

            holdRingTexture.Apply(false);
            return holdRingTexture;
        }

        private void TickCharging(float dt)
        {
            bool powered = IsSpawned ? networkPowered.Value : IsLocalCablePowered();
            bool hasBattery = IsSpawned ? networkHasBattery.Value : HasBattery;
            if (!powered || !hasBattery)
            {
                return;
            }

            float charge = IsSpawned ? networkBatteryCharge01.Value : (InstalledBattery != null ? InstalledBattery.Charge01 : 0f);
            if (charge >= 0.999f)
            {
                charge = 1f;
            }
            else
            {
                charge = Mathf.Min(1f, charge + dt / SecondsToFullCharge);
            }

            if (InstalledBattery != null)
            {
                InstalledBattery.Charge01 = charge;
            }

            if (IsSpawned && IsServer)
            {
                networkBatteryCharge01.Value = charge;
            }
        }

        private void SyncLocalPowerToNetwork()
        {
            bool localPowered = IsLocalCablePowered();
            if (localPowered == lastLocalPowered && Time.time < nextStatePushTime)
            {
                return;
            }

            lastLocalPowered = localPowered;
            if (!IsSpawned)
            {
                return;
            }

            if (IsServer)
            {
                if (networkPowered.Value != localPowered)
                {
                    networkPowered.Value = localPowered;
                    PushDisplayToNetwork(true);
                }
            }
            else if (localPowered != networkPowered.Value)
            {
                ReportPowerServerRpc(localPowered);
            }
        }

        private bool IsLocalCablePowered()
        {
            return PowerCable != null &&
                   PowerCable.gameObject.activeInHierarchy &&
                   PowerCable.GetFreeEndSocket() != null;
        }

        private void PushDisplayToNetwork(bool force)
        {
            if (IsSpawned && !IsServer)
            {
                return;
            }

            int code;
            bool powered = IsSpawned ? networkPowered.Value : IsLocalCablePowered();
            bool hasBattery = IsSpawned ? networkHasBattery.Value : HasBattery;
            if (!powered)
            {
                code = DisplayNoPower;
            }
            else if (!hasBattery)
            {
                code = DisplayNoBattery;
            }
            else
            {
                float charge = IsSpawned ? networkBatteryCharge01.Value : (InstalledBattery != null ? InstalledBattery.Charge01 : 0f);
                code = Mathf.Clamp(Mathf.RoundToInt(charge * 100f), 0, 100);
            }

            if (!IsSpawned)
            {
                return;
            }

            if (force || networkDisplayCode.Value != code)
            {
                networkDisplayCode.Value = code;
            }
        }

        private void ApplyNetworkChargeToLocalBattery()
        {
            if (!IsSpawned || InstalledBattery == null || !InstalledBattery.IsInstalledOnCharger(this))
            {
                return;
            }

            InstalledBattery.Charge01 = networkBatteryCharge01.Value;
        }

        private void RefreshDisplayLocal()
        {
            if (DisplayText == null)
            {
                return;
            }

            int code = IsSpawned ? networkDisplayCode.Value : ComputeLocalDisplayCode();
            string text;
            if (code == DisplayNoPower)
            {
                text = "No power";
            }
            else if (code == DisplayNoBattery)
            {
                text = "No bat";
            }
            else
            {
                text = Mathf.Clamp(code, 0, 100) + "%";
            }

            if (text == lastDisplayText)
            {
                return;
            }

            lastDisplayText = text;
            DisplayText.text = text;
        }

        private int ComputeLocalDisplayCode()
        {
            if (!IsLocalCablePowered())
            {
                return DisplayNoPower;
            }

            if (!HasBattery)
            {
                return DisplayNoBattery;
            }

            float charge = InstalledBattery != null ? InstalledBattery.Charge01 : 0f;
            return Mathf.Clamp(Mathf.RoundToInt(charge * 100f), 0, 100);
        }

        private void OnDisplayCodeChanged(int previous, int next) => RefreshDisplayLocal();

        private void OnBatteryChargeChanged(float previous, float next)
        {
            ApplyNetworkChargeToLocalBattery();
            RefreshDisplayLocal();
        }

        private void OnNetworkCarriedChanged(bool previous, bool next)
        {
            // Local carrier already toggles the cable in TryPickup / PlaceAtPreview.
            if (carrier != null)
            {
                return;
            }

            SetCableActive(!next);
            if (!next)
            {
                AnchorPermanentCableEnd();
                RestFreeCableBesideCharger();
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestCarryServerRpc(bool carried, ServerRpcParams rpcParams = default)
        {
            networkCarried.Value = carried;
            if (carried)
            {
                networkOnShelf.Value = false;
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestPlaceServerRpc(Vector3 position, Quaternion rotation, bool onShelf, ServerRpcParams rpcParams = default)
        {
            networkCarried.Value = false;
            networkOnShelf.Value = onShelf;
            Quaternion flat = SanitizePlacementRotation(rotation);
            networkPosition.Value = position;
            networkRotation.Value = flat;
            transform.SetPositionAndRotation(position, flat);
            ConfigurePlacedBody();
            if (onShelf)
            {
                shelvedOn = FindNearestShelf(position, 2.5f);
                if (shelvedOn != null)
                {
                    SnapToShelfPose(shelvedOn);
                    networkPosition.Value = transform.position;
                    networkRotation.Value = transform.rotation;
                }
            }
            else
            {
                shelvedOn = null;
            }

            SetCableActive(true);
            AnchorPermanentCableEnd();
            EnforceFreeCableEndOutside();
        }

        [ServerRpc(RequireOwnership = false)]
        private void NotifyBatteryChangedServerRpc(bool installed, float charge01, ServerRpcParams rpcParams = default)
        {
            networkHasBattery.Value = installed;
            networkBatteryCharge01.Value = Mathf.Clamp01(charge01);
            PushDisplayToNetwork(true);
        }

        [ServerRpc(RequireOwnership = false)]
        private void ReportPowerServerRpc(bool powered, ServerRpcParams rpcParams = default)
        {
            if (networkPowered.Value == powered)
            {
                return;
            }

            networkPowered.Value = powered;
            PushDisplayToNetwork(true);
        }

        private void UnplugFreeCableEnd()
        {
            if (PowerCable == null)
            {
                return;
            }

            for (int i = 0; i < 2; i++)
            {
                if (!PowerCable.IsEndPermanentlyAnchored(i))
                {
                    PowerCable.DetachEndInPlace(i);
                }
            }

            lastLocalPowered = false;
            RestFreeCableBesideCharger();
            if (IsSpawned && IsServer)
            {
                networkPowered.Value = false;
                PushDisplayToNetwork(true);
            }
            else if (IsSpawned)
            {
                ReportPowerServerRpc(false);
            }
        }

        private void AnchorPermanentCableEnd()
        {
            if (PowerCable == null || ChargerCableSocket == null || !PowerCable.gameObject.activeInHierarchy)
            {
                return;
            }

            if (PowerCable.PermanentlyAnchoredEndIndex < 0)
            {
                PowerCable.PermanentlyAnchoredEndIndex = 0;
            }

            int fixedEnd = PowerCable.PermanentlyAnchoredEndIndex;
            if (!ChargerCableSocket.IsConnected || ChargerCableSocket.ConnectedCable != PowerCable)
            {
                PowerCable.ConnectEndToSocket(fixedEnd, ChargerCableSocket);
            }

            // Always re-snap so End A cannot drift inside the chair and look like a second free plug.
            PowerCable.SnapEndToSocketPose(fixedEnd, ChargerCableSocket);
        }

        private void ClearStaleBattery()
        {
            if (InstalledBattery != null && !InstalledBattery.IsInstalledOnCharger(this))
            {
                InstalledBattery = null;
            }
        }

        public void EnsureDisplayText()
        {
            if (DisplayText != null)
            {
                return;
            }

            Transform display = transform.Find("Receiver Power Indicator/Display");
            if (display == null)
            {
                display = transform.Find("Display");
            }

            if (display == null)
            {
                return;
            }

            Transform existing = display.Find("ChargeText");
            GameObject textObject = existing != null ? existing.gameObject : new GameObject("ChargeText");
            if (existing == null)
            {
                textObject.transform.SetParent(display, false);
            }

            textObject.transform.localPosition = Vector3.zero;
            textObject.transform.localRotation = Quaternion.identity;
            textObject.transform.localScale = Vector3.one;

            DisplayText = textObject.GetComponent<TextMesh>();
            if (DisplayText == null)
            {
                DisplayText = textObject.AddComponent<TextMesh>();
            }

            DisplayText.anchor = TextAnchor.MiddleCenter;
            DisplayText.alignment = TextAlignment.Center;
            DisplayText.characterSize = 0.045f;
            DisplayText.fontSize = 64;
            DisplayText.color = new Color(0.2f, 1f, 0.35f, 1f);
            DisplayText.fontStyle = FontStyle.Bold;
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font != null)
            {
                DisplayText.font = font;
                if (DisplayText.GetComponent<MeshRenderer>() != null && font.material != null)
                {
                    DisplayText.GetComponent<MeshRenderer>().sharedMaterial = font.material;
                }
            }
        }

        private void ConfigureCarriedBody()
        {
            if (body != null)
            {
                body.isKinematic = true;
                body.useGravity = false;
                body.detectCollisions = false;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }

            SetNonSocketCollidersEnabled(false);
        }

        private void ConfigurePlacedBody()
        {
            if (body != null)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.isKinematic = true;
                body.useGravity = false;
                body.detectCollisions = true;
                body.mass = 0.01f;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            }

            SetNonSocketCollidersEnabled(true);
            if (rootBox == null)
            {
                rootBox = GetComponent<BoxCollider>();
            }

            if (rootBox != null)
            {
                rootBox.isTrigger = false;
            }

            IgnoreCollisionsWithVehicles();
        }

        /// <summary>
        /// Keep solid colliders for the player, but never push / weigh down the minivan.
        /// Overlap with the cabin would otherwise explode collision damage.
        /// </summary>
        private void IgnoreCollisionsWithVehicles()
        {
            if (colliders == null || colliders.Length == 0)
            {
                colliders = GetComponentsInChildren<Collider>(true);
            }

            MiniVanVehicle[] vehicles = Object.FindObjectsByType<MiniVanVehicle>(FindObjectsSortMode.None);
            for (int v = 0; v < vehicles.Length; v++)
            {
                MiniVanVehicle vehicle = vehicles[v];
                if (vehicle == null)
                {
                    continue;
                }

                Collider[] vehicleColliders = vehicle.GetComponentsInChildren<Collider>(true);
                for (int i = 0; i < colliders.Length; i++)
                {
                    Collider chargerCollider = colliders[i];
                    if (chargerCollider == null || !chargerCollider.enabled)
                    {
                        continue;
                    }

                    // Cable ends stay interactive with the world; only the charger body ignores the van.
                    if (PowerCable != null && chargerCollider.transform.IsChildOf(PowerCable.transform))
                    {
                        continue;
                    }

                    for (int j = 0; j < vehicleColliders.Length; j++)
                    {
                        Collider vehicleCollider = vehicleColliders[j];
                        if (vehicleCollider == null || !vehicleCollider.enabled)
                        {
                            continue;
                        }

                        Physics.IgnoreCollision(chargerCollider, vehicleCollider, true);
                    }
                }
            }
        }

        private void SetNonSocketCollidersEnabled(bool enabled)
        {
            if (colliders == null)
            {
                return;
            }

            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider == null)
                {
                    continue;
                }

                if (ChargerCableSocket != null && collider.transform.IsChildOf(ChargerCableSocket.transform))
                {
                    collider.enabled = true;
                    continue;
                }

                if (PowerCable != null && collider.transform.IsChildOf(PowerCable.transform))
                {
                    continue;
                }

                collider.enabled = enabled;
            }
        }

        private void ApplyCarryPose(MiniVanPlayer player)
        {
            if (player == null)
            {
                return;
            }

            // Riding (third person): strap the charger to the rider's right side at chest
            // height instead of the first-person in-front-of-camera pose.
            if (player.IsRidingBoard)
            {
                Vector3 sidePosition = player.transform.TransformPoint(RidingCarryLocalPosition);
                Quaternion sideRotation = player.transform.rotation * Quaternion.Euler(RidingCarryLocalEuler);
                transform.SetPositionAndRotation(sidePosition, sideRotation);
                return;
            }

            Transform cam = player.PlayerCamera != null
                ? player.PlayerCamera.transform
                : (player.CameraRoot != null ? player.CameraRoot : player.transform);
            Vector3 position = cam.TransformPoint(CarryLocalPosition);
            Quaternion rotation = cam.rotation * Quaternion.Euler(CarryLocalEuler);
            transform.SetPositionAndRotation(position, rotation);
        }

        private void SetCableActive(bool active)
        {
            if (PowerCable == null)
            {
                return;
            }

            if (!active)
            {
                // Pull orphaned plugs back under the cable BEFORE hiding — otherwise the free
                // end stays in the world (e.g. lodged in a seat) with live physics.
                PowerCable.StowEndsUnderCable();
                if (PowerCable.gameObject.activeSelf)
                {
                    PowerCable.gameObject.SetActive(false);
                }

                return;
            }

            if (!PowerCable.gameObject.activeSelf)
            {
                PowerCable.gameObject.SetActive(true);
            }

            PowerCable.StowEndsUnderCable();
        }

        private void RestFreeCableBesideCharger()
        {
            if (PowerCable == null || !PowerCable.gameObject.activeInHierarchy)
            {
                return;
            }

            EnforceFreeCableEndOutside();
        }

        private void EnforceFreeCableEndOutside()
        {
            if (PowerCable == null || !PowerCable.gameObject.activeInHierarchy)
            {
                return;
            }

            int freeIndex = PowerCable.GetFreeEndIndex();
            if (PowerCable.IsEndCarried(freeIndex))
            {
                return;
            }

            // Already plugged into a real wall/mechanism socket — leave it alone.
            MiniVanBridgeCableSocket freeSocket = PowerCable.GetFreeEndSocket();
            if (freeSocket != null && freeSocket.OwnerCharger == null)
            {
                PowerCable.SetFreeEndPhysicsSuspended(false);
                return;
            }

            Vector3 restPosition = transform.TransformPoint(FreeCableRestLocal);
            // Park strictly outward from charger center through the cable socket —
            // never across the seat / through the side wall.
            if (ChargerCableSocket != null)
            {
                Vector3 socketPos = ChargerCableSocket.GetPlugWorldPosition();
                Vector3 outward = socketPos - transform.position;
                outward = Vector3.ProjectOnPlane(outward, transform.up);
                if (outward.sqrMagnitude < 0.01f)
                {
                    outward = -transform.right;
                }

                restPosition = socketPos + outward.normalized * 0.5f + transform.up * 0.08f;
            }

            PowerCable.RestAllUnpluggedEnds(restPosition, transform.rotation);
            PowerCable.EnsureFreeEndPickupCollider(freeIndex);
        }

        private void ClearShelfBinding()
        {
            shelvedOn = null;
            if (IsSpawned && IsServer)
            {
                networkOnShelf.Value = false;
            }
        }

        private void SnapToShelfIfNeeded()
        {
            if (carrier != null)
            {
                return;
            }

            if (shelvedOn == null && IsSpawned && networkOnShelf.Value)
            {
                shelvedOn = FindNearestShelf(transform.position, 2.8f);
            }

            if (shelvedOn == null)
            {
                return;
            }

            SnapToShelfPose(shelvedOn);
            if (IsSpawned && IsServer)
            {
                networkPosition.Value = transform.position;
                networkRotation.Value = transform.rotation;
            }
        }

        private void SnapToShelfPose(MiniVanSkateboardShelf shelf)
        {
            if (shelf == null)
            {
                return;
            }

            Quaternion rotation = BuildSurfaceRotation(shelf.transform.up, shelf.transform.forward);
            Vector3 position = ComputePlacementPosition(shelf.AnchorPosition, rotation);
            transform.SetPositionAndRotation(position, rotation);
            if (body != null)
            {
                body.position = position;
                body.rotation = rotation;
                if (!body.isKinematic)
                {
                    body.isKinematic = true;
                }
            }
        }

        private static MiniVanSkateboardShelf FindNearestShelf(Vector3 position, float radius)
        {
            MiniVanSkateboardShelf[] shelves = Object.FindObjectsByType<MiniVanSkateboardShelf>(FindObjectsSortMode.None);
            MiniVanSkateboardShelf best = null;
            float bestDist = radius;
            for (int i = 0; i < shelves.Length; i++)
            {
                MiniVanSkateboardShelf shelf = shelves[i];
                if (shelf == null)
                {
                    continue;
                }

                float dist = Vector3.Distance(position, shelf.AnchorPosition);
                if (dist <= bestDist)
                {
                    bestDist = dist;
                    best = shelf;
                }
            }

            return best;
        }

        private bool CanOfferFreeCable(MiniVanPlayer player)
        {
            if (player == null || PowerCable == null || !PowerCable.gameObject.activeInHierarchy)
            {
                return false;
            }

            int freeIndex = PowerCable.GetFreeEndIndex();
            if (PowerCable.IsEndCarried(freeIndex))
            {
                return false;
            }

            MiniVanBridgeCableSocket freeSocket = PowerCable.GetFreeEndSocket();
            return freeSocket == null || freeSocket.OwnerCharger != null;
        }
    }
}
