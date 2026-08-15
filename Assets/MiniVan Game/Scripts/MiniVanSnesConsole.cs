using Unity.Netcode;
using UnityEngine;

namespace MiniVanGame
{
    [RequireComponent(typeof(NetworkObject))]
    [DisallowMultipleComponent]
    public sealed class MiniVanSnesConsole : NetworkBehaviour
    {
        [Header("Interaction")]
        public float InteractRadius = 2.6f;
        public float TelevisionLinkRadius = 4.5f;
        public Vector3 CarryLocalPosition = new Vector3(0.4f, -0.35f, 0.7f);
        public Vector3 CarryLocalEuler = new Vector3(-8f, 20f, 6f);

        [Header("Slots / Power")]
        public Transform CartridgeSlot;
        public Transform OnOffButton;
        public Vector3 OnOffReleasedLocalPosition;
        public Vector3 OnOffPressedLocalOffset = new Vector3(0f, -0.012f, 0f);

        [Header("Placement")]
        public float PlaceMaxDistance = 5f;
        public LayerMask PlaceMask = ~0;

        private readonly NetworkVariable<bool> isPowered = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<ulong> carriedByClientId = new NetworkVariable<ulong>(
            ulong.MaxValue,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<ulong> insertedCartridgeId = new NetworkVariable<ulong>(
            ulong.MaxValue,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private MiniVanSnesOutline outline;
        private MiniVanSnesOutline powerOutline;
        private Rigidbody body;
        private Collider[] colliders;
        private Renderer[] renderers;
        private bool capturedOnOffRest;
        private bool carriedVisualVisible = true;
        private bool localReleasePredictionActive;
        private bool localReleasePhysical;

        public bool IsPowered => isPowered.Value;
        public bool IsCarried => carriedByClientId.Value != ulong.MaxValue;
        public bool IsAvailable => !IsCarried;
        public ulong CarriedByClientId => carriedByClientId.Value;
        public ulong InsertedCartridgeId => insertedCartridgeId.Value;

        public override void OnNetworkSpawn()
        {
            isPowered.OnValueChanged += OnPowerChanged;
            carriedByClientId.OnValueChanged += (_, __) => ApplyCarryPhysics();
            insertedCartridgeId.OnValueChanged += OnCartridgeChanged;
            CaptureOnOffRest();
            ApplyPowerVisual();
            MiniVanSnesPhysics.IgnoreVehicleCollisions(colliders);
            ApplyCarryPhysics();
            RefreshLinkedTelevisionSignal();
        }

        public override void OnNetworkDespawn()
        {
            isPowered.OnValueChanged -= OnPowerChanged;
            insertedCartridgeId.OnValueChanged -= OnCartridgeChanged;
        }

        private void Awake()
        {
            outline = GetComponent<MiniVanSnesOutline>() ?? gameObject.AddComponent<MiniVanSnesOutline>();
            body = MiniVanSnesPhysics.EnsureKinematicBody(gameObject);
            AutoWireRefs();
            EnsurePowerButtonInteractable();
            CaptureOnOffRest();
            colliders = MiniVanSnesPhysics.EnsureTriggerColliders(
                gameObject, new Vector3(0.4f, 0.14f, 0.28f), new Vector3(0f, 0.06f, 0f));
            MiniVanSnesPhysics.IgnoreVehicleCollisions(colliders);
        }

        private void Update()
        {
            if (!IsSpawned)
            {
                return;
            }

            if (localReleasePredictionActive)
            {
                if (!IsCarried)
                {
                    localReleasePredictionActive = false;
                }
                else if (localReleasePhysical)
                {
                    MiniVanSnesPhysics.TickLooseBody(body, colliders, transform);
                }

                SetCarriedVisualVisible(true);
            }
            else if (IsCarried)
            {
                MiniVanPlayer carrier = FindCarrier();
                if (carrier != null)
                {
                    ApplyCarryPose(carrier);
                    SetCarriedVisualVisible(carrier.IsInventoryItemSelectedForWorld(MiniVanInventoryItem.SnesConsole));
                }
            }
            else
            {
                SetCarriedVisualVisible(true);
            }

            if (IsInsertedCartridge(out MiniVanSnesCartridge cart) && CartridgeSlot != null)
            {
                cart.SnapToSlot(CartridgeSlot);
            }

            ApplyPowerVisual();
        }

        private void FixedUpdate()
        {
            if (!IsServer || !IsSpawned || IsCarried)
            {
                return;
            }

            MiniVanSnesPhysics.TickLooseBody(body, colliders, transform);
        }

        public bool IsInRange(Vector3 worldPosition)
        {
            return Vector3.Distance(worldPosition, transform.position) <= InteractRadius;
        }

        public void SetHighlighted(bool value) => outline?.SetHighlighted(value);

        public void SetPowerButtonHighlighted(bool value)
        {
            if (powerOutline != null)
            {
                powerOutline.SetHighlighted(value);
            }
            else if (value)
            {
                // Fallback if button has no own mesh outline yet.
                SetHighlighted(true);
            }
        }

        public string GetPrompt(MiniVanPlayer player)
        {
            if (player == null)
            {
                return null;
            }

            if (player.IsHoldingSnesCartridge() && !HasInsertedCartridge())
            {
                return "E - insert cartridge";
            }

            if (HasInsertedCartridge())
            {
                return "E - remove cartridge first";
            }

            if (IsAvailable)
            {
                return "E - pick up console";
            }

            if (IsCarried && CarriedByClientId == player.OwnerClientId)
            {
                return "E - place  |  scroll - rotate  |  Q - drop";
            }

            return null;
        }

        public string GetPowerButtonPrompt()
        {
            return IsPowered ? "E - power OFF" : "E - power ON";
        }

        public bool CanPickup => IsAvailable && !HasInsertedCartridge();

        public bool IsAimingAtPowerButton(Ray ray, float maxDistance = 3.2f, float aimRadius = 0.09f)
        {
            if (OnOffButton == null)
            {
                return false;
            }

            Collider powerCollider = OnOffButton.GetComponent<Collider>();
            if (powerCollider != null && powerCollider.enabled)
            {
                if (powerCollider.Raycast(ray, out RaycastHit hit, maxDistance))
                {
                    return true;
                }
            }

            Vector3 toButton = OnOffButton.position - ray.origin;
            float along = Vector3.Dot(toButton, ray.direction);
            if (along < 0f || along > maxDistance)
            {
                return false;
            }

            Vector3 closest = ray.origin + ray.direction * along;
            return Vector3.Distance(closest, OnOffButton.position) <= aimRadius;
        }

        public bool HasInsertedCartridge()
        {
            return insertedCartridgeId.Value != ulong.MaxValue;
        }

        public bool IsInsertedCartridge(out MiniVanSnesCartridge cartridge)
        {
            cartridge = null;
            if (!HasInsertedCartridge() || NetworkManager.Singleton == null)
            {
                return false;
            }

            if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(
                    insertedCartridgeId.Value, out NetworkObject obj) || obj == null)
            {
                return false;
            }

            cartridge = obj.GetComponent<MiniVanSnesCartridge>();
            return cartridge != null;
        }

        public MiniVanSnesTelevision FindLinkedTelevision()
        {
            MiniVanSnesTelevision[] televisions = FindObjectsByType<MiniVanSnesTelevision>(FindObjectsSortMode.None);
            MiniVanSnesTelevision best = null;
            float bestDist = TelevisionLinkRadius;
            for (int i = 0; i < televisions.Length; i++)
            {
                MiniVanSnesTelevision tv = televisions[i];
                if (tv == null || tv.IsCarried)
                {
                    continue;
                }

                float d = Vector3.Distance(transform.position, tv.transform.position);
                if (d <= bestDist)
                {
                    bestDist = d;
                    best = tv;
                }
            }

            return best;
        }

        public bool TryPickupServer(MiniVanPlayer player)
        {
            if (!IsServer || player == null || !CanPickup || !IsInRange(player.transform.position))
            {
                return false;
            }

            if (IsPowered)
            {
                SetPowerServer(false);
            }

            transform.SetParent(null, true);
            carriedByClientId.Value = player.OwnerClientId;
            ApplyCarryPhysics();
            return true;
        }

        public bool TryDropServer(MiniVanPlayer player, Vector3 position, Quaternion rotation)
        {
            if (!IsServer || player == null || CarriedByClientId != player.OwnerClientId)
            {
                return false;
            }

            carriedByClientId.Value = ulong.MaxValue;
            MiniVanSnesPhysics.ApplyDroppedState(body, colliders, transform, position, rotation);
            RefreshLinkedTelevisionSignal();
            return true;
        }

        public void BeginLocalReleasePrediction(MiniVanPlayer player, Vector3 position, Quaternion rotation, bool physicalDrop)
        {
            if (player == null || !IsCarried || CarriedByClientId != player.OwnerClientId)
            {
                return;
            }

            localReleasePredictionActive = true;
            localReleasePhysical = physicalDrop;
            if (physicalDrop)
            {
                MiniVanSnesPhysics.ApplyDroppedState(body, colliders, transform, position, rotation);
            }
            else
            {
                transform.SetPositionAndRotation(position, rotation);
                MiniVanSnesPhysics.ApplyPlacedState(body, colliders, transform, position);
            }

            SetCarriedVisualVisible(true);
        }

        public bool TryPlaceServer(MiniVanPlayer player, Vector3 position, Quaternion rotation)
        {
            if (!IsServer || player == null || CarriedByClientId != player.OwnerClientId)
            {
                return false;
            }

            carriedByClientId.Value = ulong.MaxValue;
            transform.SetPositionAndRotation(position, rotation);
            MiniVanSnesPhysics.ApplyPlacedState(body, colliders, transform, position);
            RefreshLinkedTelevisionSignal();
            return true;
        }

        public bool TryInsertCartridgeServer(MiniVanPlayer player, MiniVanSnesCartridge cartridge)
        {
            if (!IsServer || player == null || cartridge == null || HasInsertedCartridge() || IsCarried)
            {
                return false;
            }

            if (!IsInRange(player.transform.position))
            {
                return false;
            }

            if (!cartridge.TryInsertServer(this))
            {
                return false;
            }

            insertedCartridgeId.Value = cartridge.NetworkObjectId;
            if (CartridgeSlot != null)
            {
                cartridge.SnapToSlot(CartridgeSlot);
            }

            RefreshLinkedTelevisionSignal();
            return true;
        }

        public bool TryRemoveCartridgeServer(MiniVanPlayer player)
        {
            if (!IsServer || player == null || !IsInsertedCartridge(out MiniVanSnesCartridge cartridge))
            {
                return false;
            }

            if (IsPowered)
            {
                SetPowerServer(false);
            }

            insertedCartridgeId.Value = ulong.MaxValue;
            cartridge.TryEjectServer(player);
            RefreshLinkedTelevisionSignal();
            return true;
        }

        public void NotifyCartridgeRemovedServer()
        {
            if (!IsServer)
            {
                return;
            }

            insertedCartridgeId.Value = ulong.MaxValue;
            if (IsPowered)
            {
                SetPowerServer(false);
            }

            RefreshLinkedTelevisionSignal();
        }

        public void TogglePowerServer()
        {
            if (!IsServer || IsCarried)
            {
                return;
            }

            SetPowerServer(!IsPowered);
        }

        private void SetPowerServer(bool powered)
        {
            if (!IsServer)
            {
                return;
            }

            isPowered.Value = powered;
            RefreshLinkedTelevisionSignal();
        }

        private void OnPowerChanged(bool previous, bool current)
        {
            ApplyPowerVisual();
            if (IsServer)
            {
                RefreshLinkedTelevisionSignal();
            }
        }

        private void OnCartridgeChanged(ulong previous, ulong current)
        {
            if (IsServer)
            {
                RefreshLinkedTelevisionSignal();
            }
        }

        public void RefreshLinkedTelevisionSignal()
        {
            // Emulator authority lives on the server — clients only receive video/input sync.
            if (!IsServer)
            {
                return;
            }

            MiniVanSnesTelevision tv = FindLinkedTelevision();
            if (tv == null)
            {
                return;
            }

            if (!IsPowered)
            {
                tv.SetSignalOff();
                return;
            }

            if (IsInsertedCartridge(out MiniVanSnesCartridge cart))
            {
                tv.SetSignalGame(cart.GameName, cart.GamesSubdirectory, cart.RomFileName, cart.SourceRomProjectRelativePath);
            }
            else
            {
                tv.SetSignalWhiteScreen();
            }
        }

        private void ApplyPowerVisual()
        {
            if (OnOffButton == null)
            {
                return;
            }

            CaptureOnOffRest();
            OnOffButton.localPosition = IsPowered
                ? OnOffReleasedLocalPosition + OnOffPressedLocalOffset
                : OnOffReleasedLocalPosition;
        }

        private void CaptureOnOffRest()
        {
            if (capturedOnOffRest || OnOffButton == null)
            {
                return;
            }

            OnOffReleasedLocalPosition = OnOffButton.localPosition;
            capturedOnOffRest = true;
        }

        private void AutoWireRefs()
        {
            if (CartridgeSlot == null)
            {
                Transform slot = transform.Find("Cart Slot");
                if (slot == null)
                {
                    slot = FindDeep(transform, "Cart Slot");
                }

                if (slot != null)
                {
                    CartridgeSlot = slot;
                }
                else
                {
                    GameObject created = new GameObject("Cart Slot");
                    created.transform.SetParent(transform, false);
                    created.transform.localPosition = new Vector3(0f, 0.3f, -0.02f);
                    CartridgeSlot = created.transform;
                }
            }

            if (OnOffButton == null)
            {
                OnOffButton = FindDeep(transform, "ON/OFF");
                if (OnOffButton == null)
                {
                    OnOffButton = FindDeep(transform, "Power Switch");
                }
            }
        }

        private void EnsurePowerButtonInteractable()
        {
            if (OnOffButton == null)
            {
                GameObject created = new GameObject("ON/OFF");
                created.transform.SetParent(transform, false);
                created.transform.localPosition = new Vector3(0.12f, 0.08f, -0.16f);
                OnOffButton = created.transform;
            }

            OnOffButton.name = "ON/OFF";

            BoxCollider powerCollider = OnOffButton.GetComponent<BoxCollider>();
            if (powerCollider == null)
            {
                powerCollider = OnOffButton.gameObject.AddComponent<BoxCollider>();
            }

            // Large enough to aim at even when the console root trigger is in front.
            powerCollider.isTrigger = true;
            if (powerCollider.size.sqrMagnitude < 0.0001f || powerCollider.size.x < 0.04f)
            {
                powerCollider.size = new Vector3(0.08f, 0.06f, 0.08f);
                powerCollider.center = Vector3.zero;
            }

            // Visual so outline can show on the button alone.
            if (OnOffButton.GetComponent<MeshRenderer>() == null)
            {
                GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Mesh cubeMesh = temp.GetComponent<MeshFilter>().sharedMesh;
                if (Application.isPlaying)
                {
                    Object.Destroy(temp);
                }
                else
                {
                    Object.DestroyImmediate(temp);
                }

                MeshFilter filter = OnOffButton.gameObject.AddComponent<MeshFilter>();
                filter.sharedMesh = cubeMesh;
                MeshRenderer meshRenderer = OnOffButton.gameObject.AddComponent<MeshRenderer>();
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                Material mat = new Material(shader)
                {
                    name = "SnesOnOffButton",
                    color = new Color(0.75f, 0.2f, 0.15f, 1f)
                };
                if (mat.HasProperty("_BaseColor"))
                {
                    mat.SetColor("_BaseColor", new Color(0.75f, 0.2f, 0.15f, 1f));
                }

                meshRenderer.sharedMaterial = mat;
                if (OnOffButton.localScale.sqrMagnitude < 0.0001f || OnOffButton.localScale.x > 0.2f)
                {
                    OnOffButton.localScale = new Vector3(0.045f, 0.02f, 0.035f);
                }
            }

            powerOutline = OnOffButton.GetComponent<MiniVanSnesOutline>();
            if (powerOutline == null)
            {
                powerOutline = OnOffButton.gameObject.AddComponent<MiniVanSnesOutline>();
            }
        }

        private static Transform FindDeep(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name)
                {
                    return t;
                }
            }

            return null;
        }

        private void ApplyCarryPhysics()
        {
            if (IsCarried)
            {
                MiniVanSnesPhysics.ApplyCarryState(body, colliders, carried: true);
                return;
            }

            if (body != null && !body.isKinematic)
            {
                MiniVanSnesPhysics.IgnoreVehicleCollisions(colliders);
                return;
            }

            MiniVanSnesPhysics.ApplyCarryState(body, colliders, carried: false);
            MiniVanSnesPhysics.IgnoreVehicleCollisions(colliders);
        }

        private MiniVanPlayer FindCarrier()
        {
            if (!IsCarried)
            {
                return null;
            }

            MiniVanPlayer[] players = FindObjectsByType<MiniVanPlayer>(FindObjectsSortMode.None);
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] != null && players[i].OwnerClientId == CarriedByClientId)
                {
                    return players[i];
                }
            }

            return null;
        }

        private void ApplyCarryPose(MiniVanPlayer player)
        {
            Transform cam = player.PlayerCamera != null ? player.PlayerCamera.transform : player.transform;
            transform.SetPositionAndRotation(
                cam.TransformPoint(CarryLocalPosition),
                cam.rotation * Quaternion.Euler(CarryLocalEuler));
        }

        private void SetCarriedVisualVisible(bool visible)
        {
            if (carriedVisualVisible == visible)
            {
                return;
            }

            carriedVisualVisible = visible;
            if (renderers == null || renderers.Length == 0)
            {
                renderers = GetComponentsInChildren<Renderer>(true);
            }

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    renderers[i].enabled = visible;
                }
            }
        }
    }
}
