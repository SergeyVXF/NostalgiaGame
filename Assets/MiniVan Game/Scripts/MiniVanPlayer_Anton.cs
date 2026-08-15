using Unity.Netcode;
using UnityEngine;

namespace MiniVanGame
{
    public partial class MiniVanPlayer
    {
        private const float AntonCarrySpeedMultiplier = 0.15f;
        private const float AntonSeatUseDistance = 3.4f;

        private float antonTakeHoldProgress;
        private MiniVanAnton antonTakeHoldTarget;
        private bool antonInteractionConsumedThisFrame;
        private bool predictedAntonReleased;

        public bool IsCarryingAnton =>
            !predictedAntonReleased && MiniVanAnton.FindCarriedBy(OwnerClientId) != null;

        public bool IsGrippingStretcher => MiniVanStretcher.FindGrippedBy(OwnerClientId) != null;

        public bool IsStretcherPinned => MiniVanStretcher.FindPinnedFor(OwnerClientId) != null;

        public bool BlocksAntonPickup()
        {
            return IsCarryingAnton ||
                   IsCarryingCorpse ||
                   IsGrippingStretcher ||
                   heldTowCube != null ||
                   currentSeat != null ||
                   currentSkateboard != null ||
                   currentHoverboardM != null;
        }

        public bool BlocksItemUseBecauseAnton()
        {
            return IsCarryingAnton;
        }

        private void UpdateAntonSystem()
        {
            if (predictedAntonReleased && MiniVanAnton.FindCarriedBy(OwnerClientId) == null)
            {
                predictedAntonReleased = false;
            }

            antonInteractionConsumedThisFrame = false;
            if (!IsOwner || currentSeat != null || IsDowned)
            {
                antonTakeHoldProgress = 0f;
                antonTakeHoldTarget = null;
                return;
            }

            HandleAntonInteractionInput();
        }

        private void HandleAntonInteractionInput()
        {
            // Q: drop Anton from back, or release stretcher grip / pinned end.
            if (MiniVanKeyBindings.GetKeyDown(MiniVanKeyAction.Drop))
            {
                if (IsCarryingAnton)
                {
                    Vector3 dropPos = transform.position + transform.forward * 1.1f + Vector3.up * 0.4f;
                    if (!IsServer)
                    {
                        MiniVanAnton anton = MiniVanAnton.FindCarriedBy(OwnerClientId);
                        anton?.BeginLocalDropPrediction(dropPos);
                        predictedAntonReleased = true;
                    }

                    RequestDropAntonServerRpc(dropPos);
                    antonInteractionConsumedThisFrame = true;
                    return;
                }

                if (IsGrippingStretcher || IsStretcherPinned)
                {
                    MiniVanStretcher gripped = MiniVanStretcher.FindGrippedBy(OwnerClientId);
                    if (gripped != null)
                    {
                        RequestReleaseStretcherServerRpc(new NetworkObjectReference(gripped.NetworkObject));
                        antonInteractionConsumedThisFrame = true;
                        return;
                    }
                }

                if (HandleStretcherDropInput())
                {
                    antonInteractionConsumedThisFrame = true;
                    return;
                }
            }

            // Hold E: take Anton off stretcher (only when not aiming a lift handle).
            UpdateAntonTakeFromStretcherHold();

            if (!MiniVanKeyBindings.GetKeyDown(MiniVanKeyAction.Interact) || PlayerCamera == null)
            {
                return;
            }

            // Seat Anton into passenger seat while carrying.
            if (IsCarryingAnton)
            {
                MiniVanSeat seat = FindLookedAtPassengerSeatForAnton();
                if (seat != null && seat.Vehicle != null)
                {
                    RequestSeatAntonServerRpc(
                        new NetworkObjectReference(seat.Vehicle.NetworkObject),
                        seat.SeatIndex);
                    antonInteractionConsumedThisFrame = true;
                }

                return;
            }

            // Lift stretcher end (backup if GameMode ray hits Anton instead of handles).
            if (!BlocksAntonPickup() && TryGripLookedAtStretcherEnd())
            {
                antonInteractionConsumedThisFrame = true;
                antonTakeHoldProgress = 0f;
                antonTakeHoldTarget = null;
            }
        }

        private bool TryGripLookedAtStretcherEnd()
        {
            // Aiming at Anton → hold-E take has priority over lifting.
            if (FindLookedAtAntonOnStretcher() != null)
            {
                return false;
            }

            MiniVanStretcher stretcher = FindLookedAtLoadedStretcher();
            if (stretcher == null || !stretcher.TryResolveLookedAtEnd(this, out MiniVanStretcherEnd end))
            {
                return false;
            }

            RequestGripStretcher(stretcher, end);
            return true;
        }

        private MiniVanStretcher FindLookedAtLoadedStretcher()
        {
            MiniVanStretcher[] stretchers = FindObjectsByType<MiniVanStretcher>(FindObjectsSortMode.None);
            MiniVanStretcher best = null;
            float bestDist = 3.6f;
            Vector3 from = PlayerCamera != null ? PlayerCamera.transform.position : transform.position;
            Vector3 forward = PlayerCamera != null ? PlayerCamera.transform.forward : transform.forward;

            for (int i = 0; i < stretchers.Length; i++)
            {
                MiniVanStretcher stretcher = stretchers[i];
                if (stretcher == null || !stretcher.IsSpawned || !stretcher.IsAvailable || !stretcher.HasAnton)
                {
                    continue;
                }

                Vector3 to = stretcher.transform.position - from;
                float along = Vector3.Dot(to, forward);
                if (along < 0.1f || along > 4f)
                {
                    continue;
                }

                float lateral = Vector3.Cross(forward, to).magnitude;
                float score = along + lateral * 0.5f;
                float feet = Vector3.Distance(transform.position, stretcher.transform.position);
                if (feet < bestDist && (lateral < 1.6f || feet < stretcher.GripReach))
                {
                    bestDist = feet;
                    best = stretcher;
                }
                else if (score < bestDist && lateral < 1.25f)
                {
                    bestDist = score;
                    best = stretcher;
                }
            }

            return best;
        }

        private MiniVanSeat FindLookedAtPassengerSeatForAnton()
        {
            if (PlayerCamera == null)
            {
                return null;
            }

            Ray ray = new Ray(PlayerCamera.transform.position, PlayerCamera.transform.forward);
            if (!Physics.Raycast(ray, out RaycastHit hit, AntonSeatUseDistance, ~0, QueryTriggerInteraction.Collide))
            {
                return null;
            }

            MiniVanSeat seat = hit.collider.GetComponentInParent<MiniVanSeat>();
            if (seat == null || seat.IsDriverSeat || seat.Vehicle == null)
            {
                return null;
            }

            return seat.Vehicle.IsSeatAvailable(seat.SeatIndex) ? seat : null;
        }

        private void UpdateAntonTakeFromStretcherHold()
        {
            if (!MiniVanKeyBindings.GetKey(MiniVanKeyAction.Interact) || PlayerCamera == null || BlocksAntonPickup())
            {
                antonTakeHoldProgress = 0f;
                antonTakeHoldTarget = null;
                return;
            }

            if (IsCarryingAnton || IsGrippingStretcher)
            {
                antonTakeHoldProgress = 0f;
                antonTakeHoldTarget = null;
                return;
            }

            MiniVanAnton target = FindLookedAtAntonOnStretcher();
            if (target == null)
            {
                antonTakeHoldProgress = 0f;
                antonTakeHoldTarget = null;
                return;
            }

            if (antonTakeHoldTarget != target)
            {
                antonTakeHoldTarget = target;
                antonTakeHoldProgress = 0f;
            }

            antonTakeHoldProgress += Time.deltaTime / MiniVanAnton.TakeHoldSeconds;
            antonInteractionConsumedThisFrame = true;
            if (antonTakeHoldProgress >= 1f)
            {
                RequestPickupAntonServerRpc(new NetworkObjectReference(target.NetworkObject));
                antonTakeHoldProgress = 0f;
                antonTakeHoldTarget = null;
            }
        }

        private MiniVanAnton FindLookedAtAntonOnStretcher()
        {
            // Aim-based (works even while Anton triggers are off).
            MiniVanAnton[] antons = FindObjectsByType<MiniVanAnton>(FindObjectsSortMode.None);
            MiniVanAnton best = null;
            float bestDist = float.MaxValue;

            for (int i = 0; i < antons.Length; i++)
            {
                MiniVanAnton anton = antons[i];
                if (anton == null || !anton.IsSpawned || anton.State != MiniVanAntonState.OnStretcher)
                {
                    continue;
                }

                if (!anton.IsInReach(this) || !anton.IsAimedBy(this))
                {
                    continue;
                }

                float dist = Vector3.Distance(transform.position, anton.ReplicatedPosition);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = anton;
                }
            }

            return best;
        }

        public void RequestPickupAnton(MiniVanAnton anton)
        {
            if (!IsOwner || anton == null || BlocksAntonPickup())
            {
                return;
            }

            RequestPickupAntonServerRpc(new NetworkObjectReference(anton.NetworkObject));
        }

        public void RequestPlaceAntonOnStretcher(MiniVanStretcher stretcher)
        {
            if (!IsOwner || stretcher == null || !IsCarryingAnton)
            {
                return;
            }

            RequestPlaceAntonOnStretcherServerRpc(new NetworkObjectReference(stretcher.NetworkObject));
        }

        public void RequestGripStretcher(MiniVanStretcher stretcher, MiniVanStretcherEnd end)
        {
            if (!IsOwner || stretcher == null || BlocksAntonPickup())
            {
                return;
            }

            RequestGripStretcherServerRpc(new NetworkObjectReference(stretcher.NetworkObject), (int)end);
        }

        public bool TryPickupStretcher(MiniVanStretcher stretcher)
        {
            if (stretcher == null || HasInventoryItem(MiniVanInventoryItem.Stretcher) || !stretcher.IsAvailable)
            {
                return false;
            }

            if (stretcher.HasAnton || stretcher.NetworkObject == null || !stretcher.NetworkObject.IsSpawned)
            {
                return false;
            }

            if (BlocksAntonPickup() && !IsCarryingAnton)
            {
                // Carrying Anton is handled by place-on-stretcher path.
            }

            if (IsCarryingAnton || IsGrippingStretcher || IsCarryingCorpse || heldTowCube != null)
            {
                return false;
            }

            RequestStretcherPickupServerRpc(new NetworkObjectReference(stretcher.NetworkObject));
            return true;
        }

        private bool HandleStretcherDropInput()
        {
            if (currentSeat != null || currentSkateboard != null || currentHoverboardM != null)
            {
                return false;
            }

            if (!IsSelectedInventoryItem(MiniVanInventoryItem.Stretcher))
            {
                return false;
            }

            Vector3 dropPosition = GetLooseItemDropPosition();
            Quaternion dropRotation = GetLooseItemDropRotation();
            if (!IsServer)
            {
                PredictClearInventoryItem(MiniVanInventoryItem.Stretcher);
            }

            RequestDropSelectedStretcherServerRpc(dropPosition, dropRotation);
            return true;
        }

        private float GetAntonSpeedMultiplier()
        {
            if (IsStretcherPinned)
            {
                return 0f;
            }

            if (IsCarryingAnton)
            {
                return AntonCarrySpeedMultiplier;
            }

            return 1f;
        }

        private void DrawAntonGui()
        {
            if (!IsOwner)
            {
                return;
            }

            if (antonTakeHoldProgress > 0.01f)
            {
                Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f + 72f);
                DrawCircle(center, 78f, new Color(0f, 0f, 0f, 0.45f));
                DrawAntonHoldRing(center, 78f, antonTakeHoldProgress);
            }

            string banner = null;
            if (IsCarryingAnton)
            {
                banner = "Carrying Anton (15% speed): E - seat / put on stretcher, Q - drop";
            }
            else if (IsStretcherPinned)
            {
                banner = "Holding stretcher end: wait for partner to lift the other end (Q - release)";
            }
            else if (IsGrippingStretcher && MiniVanStretcher.FindGrippedBy(OwnerClientId) is MiniVanStretcher s && s.IsLifted)
            {
                banner = "Carrying stretcher with Anton: Q - release end";
            }

            if (string.IsNullOrEmpty(banner))
            {
                return;
            }

            GUIStyle style = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 15,
                fontStyle = FontStyle.Bold
            };
            style.normal.textColor = Color.white;
            GUI.Box(new Rect(Screen.width * 0.5f - 260f, 70f, 520f, 40f), banner, style);
        }

        private static Texture2D antonHoldTexture;
        private static int antonHoldBucket = -1;

        private static void DrawAntonHoldRing(Vector2 center, float diameter, float progress)
        {
            Texture2D texture = GetAntonHoldTexture(progress);
            if (texture == null)
            {
                return;
            }

            Color old = GUI.color;
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(center.x - diameter * 0.5f, center.y - diameter * 0.5f, diameter, diameter), texture);
            GUI.color = old;
        }

        private static Texture2D GetAntonHoldTexture(float progress)
        {
            const int size = 96;
            int bucket = Mathf.Clamp(Mathf.RoundToInt(progress * 100f), 0, 100);
            if (antonHoldTexture != null && antonHoldBucket == bucket)
            {
                return antonHoldTexture;
            }

            if (antonHoldTexture == null)
            {
                antonHoldTexture = new Texture2D(size, size, TextureFormat.ARGB32, false)
                {
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave
                };
            }

            antonHoldBucket = bucket;
            Color clear = new Color(0f, 0f, 0f, 0f);
            Color fill = new Color(0.25f, 0.75f, 1f, 0.95f);
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
                        antonHoldTexture.SetPixel(x, y, clear);
                        continue;
                    }

                    float angle = Mathf.Atan2(dx, -dy);
                    if (angle < 0f)
                    {
                        angle += Mathf.PI * 2f;
                    }

                    antonHoldTexture.SetPixel(x, y, angle <= angleMax ? fill : clear);
                }
            }

            antonHoldTexture.Apply(false);
            return antonHoldTexture;
        }

        [ServerRpc]
        private void RequestPickupAntonServerRpc(NetworkObjectReference antonReference, ServerRpcParams rpcParams = default)
        {
            if (!antonReference.TryGet(out NetworkObject antonObject))
            {
                return;
            }

            MiniVanAnton anton = antonObject.GetComponent<MiniVanAnton>();
            if (anton == null ||
                MiniVanAnton.FindCarriedBy(OwnerClientId) != null ||
                MiniVanStretcher.FindGrippedBy(OwnerClientId) != null ||
                FindCarriedCorpse(OwnerClientId) != null)
            {
                return;
            }

            if (anton.State != MiniVanAntonState.World && anton.State != MiniVanAntonState.OnStretcher)
            {
                return;
            }

            if (Vector3.Distance(transform.position, anton.ReplicatedPosition) > 4.2f)
            {
                return;
            }

            anton.ServerPickup(this);
        }

        [ServerRpc]
        private void RequestDropAntonServerRpc(Vector3 dropPosition, ServerRpcParams rpcParams = default)
        {
            MiniVanAnton anton = MiniVanAnton.FindCarriedBy(OwnerClientId);
            if (anton == null)
            {
                return;
            }

            anton.ServerDrop(dropPosition);
        }

        [ServerRpc]
        private void RequestSeatAntonServerRpc(NetworkObjectReference vehicleReference, int seatIndex, ServerRpcParams rpcParams = default)
        {
            MiniVanAnton anton = MiniVanAnton.FindCarriedBy(OwnerClientId);
            if (anton == null || !vehicleReference.TryGet(out NetworkObject vehicleObject))
            {
                return;
            }

            MiniVanVehicle vehicle = vehicleObject.GetComponent<MiniVanVehicle>();
            if (vehicle == null)
            {
                return;
            }

            anton.ServerSeat(vehicle, seatIndex);
        }

        [ServerRpc]
        private void RequestPlaceAntonOnStretcherServerRpc(NetworkObjectReference stretcherReference, ServerRpcParams rpcParams = default)
        {
            MiniVanAnton anton = MiniVanAnton.FindCarriedBy(OwnerClientId);
            if (anton == null || !stretcherReference.TryGet(out NetworkObject stretcherObject))
            {
                return;
            }

            MiniVanStretcher stretcher = stretcherObject.GetComponent<MiniVanStretcher>();
            if (stretcher == null || !stretcher.IsAvailable || stretcher.HasAnton)
            {
                return;
            }

            if (Vector3.Distance(transform.position, stretcher.transform.position) > stretcher.PickupRadius + 1f)
            {
                return;
            }

            anton.ServerPlaceOnStretcher(stretcher);
        }

        [ServerRpc]
        private void RequestGripStretcherServerRpc(NetworkObjectReference stretcherReference, int endIndex, ServerRpcParams rpcParams = default)
        {
            if (!stretcherReference.TryGet(out NetworkObject stretcherObject))
            {
                return;
            }

            MiniVanStretcher stretcher = stretcherObject.GetComponent<MiniVanStretcher>();
            if (stretcher == null || !stretcher.HasAnton || IsCarryingAnton || IsGrippingStretcher)
            {
                return;
            }

            MiniVanStretcherEnd end = endIndex == (int)MiniVanStretcherEnd.Rear
                ? MiniVanStretcherEnd.Rear
                : MiniVanStretcherEnd.Front;
            stretcher.ServerTryGrip(OwnerClientId, end);
        }

        [ServerRpc]
        private void RequestReleaseStretcherServerRpc(NetworkObjectReference stretcherReference, ServerRpcParams rpcParams = default)
        {
            if (!stretcherReference.TryGet(out NetworkObject stretcherObject))
            {
                return;
            }

            MiniVanStretcher stretcher = stretcherObject.GetComponent<MiniVanStretcher>();
            if (stretcher == null)
            {
                return;
            }

            stretcher.ServerReleaseGrip(OwnerClientId);
        }

        [ServerRpc]
        private void RequestStretcherPickupServerRpc(NetworkObjectReference stretcherReference, ServerRpcParams rpcParams = default)
        {
            if (HasInventoryItem(MiniVanInventoryItem.Stretcher) ||
                !stretcherReference.TryGet(out NetworkObject stretcherObject))
            {
                return;
            }

            MiniVanStretcher stretcher = stretcherObject.GetComponent<MiniVanStretcher>();
            if (stretcher == null || !stretcher.IsAvailable || stretcher.HasAnton ||
                !stretcher.IsInReach(transform.position))
            {
                return;
            }

            int emptySlot = FindFirstEmptyInventorySlot();
            if (emptySlot < 0 || !stretcher.TryClaim())
            {
                return;
            }

            SetInventorySlot(emptySlot, MiniVanInventoryItem.Stretcher);
            networkSelectedSlot.Value = emptySlot;
            SetLocalInventorySlotClientRpc(emptySlot, (int)MiniVanInventoryItem.Stretcher, BuildOwnerTarget());
        }

        [ServerRpc]
        private void RequestDropSelectedStretcherServerRpc(Vector3 dropPosition, Quaternion dropRotation, ServerRpcParams rpcParams = default)
        {
            if (currentSeat != null || currentSkateboard != null || currentHoverboardM != null)
            {
                return;
            }

            int slot = FindInventorySlot(MiniVanInventoryItem.Stretcher);
            if (slot < 0 || !ServerSpawnStretcher(dropPosition, dropRotation))
            {
                return;
            }

            SetInventorySlot(slot, MiniVanInventoryItem.None);
            SetLocalInventorySlotClientRpc(slot, (int)MiniVanInventoryItem.None, BuildOwnerTarget());
        }

        private bool ServerSpawnStretcher(Vector3 worldPosition, Quaternion rotation)
        {
            if (!IsServer)
            {
                return false;
            }

            GameObject prefab = StretcherPickupPrefab;
            if (prefab == null)
            {
                prefab = MiniVanAntonTestSpawner.ResolveStretcherPrefab();
            }

            if (prefab == null)
            {
                return false;
            }

            GameObject instance = Instantiate(prefab, worldPosition, rotation);
            NetworkObject networkObject = instance.GetComponent<NetworkObject>();
            if (networkObject != null && !networkObject.IsSpawned)
            {
                networkObject.Spawn(true);
            }

            return true;
        }

        public static bool IsAntonSeatOccupied(MiniVanVehicle vehicle, int seatIndex)
        {
            return MiniVanAnton.IsSeatOccupied(vehicle, seatIndex);
        }
    }
}
