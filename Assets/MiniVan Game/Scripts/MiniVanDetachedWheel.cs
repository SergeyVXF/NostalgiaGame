using System.Collections.Generic;
using UnityEngine;

namespace MiniVanGame
{
    [DisallowMultipleComponent]
    public sealed class MiniVanDetachedWheel : MonoBehaviour, IMiniVanGameModeInteractable
    {
        private const float PickupReach = 2.25f;
        // First-person: to the right / lower so the tire doesn't fill the crosshair.
        private static readonly Vector3 CarryLocalPosition = new Vector3(0.58f, -0.42f, 0.52f);
        private static readonly Vector3 CarryLocalEuler = new Vector3(10f, 20f, 90f);
        // Riding / third-person: right hip / torso side.
        private static readonly Vector3 RidingCarryLocalPosition = new Vector3(0.62f, 0.95f, 0.08f);
        private static readonly Vector3 RidingCarryLocalEuler = new Vector3(0f, 0f, 90f);

        private static readonly Dictionary<MiniVanPlayer, MiniVanDetachedWheel> carriedByPlayer =
            new Dictionary<MiniVanPlayer, MiniVanDetachedWheel>();

        public const int SpareWheelIndex = -1;

        public MiniVanVehicle Vehicle { get; private set; }
        public int WheelIndex { get; private set; } = SpareWheelIndex;

        private Rigidbody body;
        private Collider[] colliders;
        private MiniVanPlayer carrier;
        private MiniVanRoofAttachPoint roofAttach;
        private Vector3 freeLocalScale = Vector3.one;
        private bool hasFreeLocalScale;
        private float vehicleContactSeconds;
        private MiniVanVehicle settlingVehicle;

        private Renderer[] highlightRenderers;
        private Material[][] originalMaterials;
        private Material outlineMaterial;
        private bool isPlacementHighlighted;

        public bool IsCarried => carrier != null;
        public bool IsSpare => WheelIndex == SpareWheelIndex;
        public bool IsOnRoofAttach => roofAttach != null;

        public static MiniVanDetachedWheel GetCarriedBy(MiniVanPlayer player)
        {
            return player != null && carriedByPlayer.TryGetValue(player, out MiniVanDetachedWheel wheel)
                ? wheel
                : null;
        }

        public void Initialize(MiniVanVehicle vehicle, int wheelIndex)
        {
            Vehicle = vehicle;
            WheelIndex = wheelIndex;
            roofAttach = null;
            RememberFreeScale();
            EnsurePhysics();
            ConfigureFreeBody();
        }

        public void InitializeAsSpare()
        {
            Initialize(null, SpareWheelIndex);
            gameObject.name = "Spare MiniVan Wheel";
        }

        public void PlaceOnRoofAttach(MiniVanRoofAttachPoint attach)
        {
            if (attach == null)
            {
                return;
            }

            SetPlacementHighlighted(false);
            // Stop carry pose BEFORE parenting — LateUpdate must not keep writing hand position.
            ClearCarrier();
            if (roofAttach != null && roofAttach != attach)
            {
                roofAttach.NotifyWheelRemoved(this);
            }

            if (!hasFreeLocalScale)
            {
                RememberFreeScale();
            }

            Vector3 desiredScale = hasFreeLocalScale ? freeLocalScale : transform.localScale;
            roofAttach = attach;
            Transform snap = attach.GetSnapTransform();
            if (snap == null)
            {
                snap = attach.transform;
            }

            // Kill any leftover rigidbody world pose from carry before reparenting.
            if (body == null)
            {
                EnsurePhysics();
            }

            if (body != null)
            {
                body.interpolation = RigidbodyInterpolation.None;
                body.isKinematic = true;
                body.detectCollisions = false;
                body.useGravity = false;
            }

            transform.SetParent(snap, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = desiredScale;
            SyncInstalledPoseToPhysics();
            ConfigureInstalledBody();
            IgnorePhysicsWithVehiclesAndPlayers(true);
        }

        public void ClearRoofAttach(MiniVanRoofAttachPoint attach)
        {
            if (roofAttach == attach)
            {
                roofAttach = null;
            }
        }

        private void Awake()
        {
            RememberFreeScale();
            EnsurePhysics();
            ConfigureFreeBody();
        }

        private void OnDisable()
        {
            SetPlacementHighlighted(false);
        }

        private void OnDestroy()
        {
            SetPlacementHighlighted(false);
            if (outlineMaterial != null)
            {
                Destroy(outlineMaterial);
                outlineMaterial = null;
            }
        }

        public void SetPlacementHighlighted(bool highlighted)
        {
            if (isPlacementHighlighted == highlighted)
            {
                return;
            }

            if (highlightRenderers == null || highlightRenderers.Length == 0)
            {
                highlightRenderers = GetComponentsInChildren<Renderer>(true);
                CacheHighlightMaterials();
            }

            isPlacementHighlighted = highlighted;
            if (highlighted)
            {
                Material outline = GetPlacementOutlineMaterial();
                for (int i = 0; i < highlightRenderers.Length; i++)
                {
                    Renderer renderer = highlightRenderers[i];
                    if (renderer == null)
                    {
                        continue;
                    }

                    Material[] source = renderer.sharedMaterials;
                    Material[] materials = new Material[source.Length + 1];
                    for (int j = 0; j < source.Length; j++)
                    {
                        materials[j] = source[j];
                    }

                    materials[materials.Length - 1] = outline;
                    renderer.sharedMaterials = materials;
                }
            }
            else if (originalMaterials != null)
            {
                for (int i = 0; i < highlightRenderers.Length; i++)
                {
                    if (highlightRenderers[i] != null && i < originalMaterials.Length)
                    {
                        highlightRenderers[i].sharedMaterials = originalMaterials[i];
                    }
                }
            }
        }

        private void LateUpdate()
        {
            // Installed cargo wins over carry pose — fixes rare frames where the tire
            // stayed floating at the hand position after E-to-place.
            if (roofAttach != null)
            {
                ForceRoofSnapPose();
                return;
            }

            if (carrier == null)
            {
                return;
            }

            if (carrier == MiniVanPlayer.LocalPlayer && !carrier.IsRidingBoard &&
                MiniVanKeyBindings.GetKeyDown(MiniVanKeyAction.Drop))
            {
                DropNear(carrier);
                return;
            }

            ApplyCarryPose(carrier);
        }

        private void ForceRoofSnapPose()
        {
            if (roofAttach == null)
            {
                return;
            }

            Transform snap = roofAttach.GetSnapTransform();
            if (snap == null)
            {
                return;
            }

            if (transform.parent != snap)
            {
                transform.SetParent(snap, false);
            }

            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            if (hasFreeLocalScale)
            {
                transform.localScale = freeLocalScale;
            }

            SyncInstalledPoseToPhysics();
        }

        private void SyncInstalledPoseToPhysics()
        {
            if (body == null)
            {
                return;
            }

            body.position = transform.position;
            body.rotation = transform.rotation;
        }

        public string GetPrompt(MiniVanPlayer player)
        {
            if (player == null || IsCarried)
            {
                return string.Empty;
            }

            return Vector3.Distance(player.transform.position, transform.position) <= PickupReach
                ? "E - take wheel"
                : string.Empty;
        }

        public void Interact(MiniVanPlayer player)
        {
            if (Input.GetMouseButton(1))
            {
                return;
            }

            TryPickup(player);
        }

        public void PrimaryAction(MiniVanPlayer player)
        {
        }

        public bool TryPickup(MiniVanPlayer player)
        {
            if (player == null || IsCarried)
            {
                return false;
            }

            MiniVanDetachedWheel carried = GetCarriedBy(player);
            if (carried != null && carried != this)
            {
                return false;
            }

            if (MiniVanCarBattery.GetCarriedBy(player) != null ||
                MiniVanBridgeBattery.GetCarriedBy(player) != null ||
                MiniVanBridgePowerCable.HasCarriedEnd(player) ||
                MiniVanBatteryCharger.GetCarriedBy(player) != null ||
                MiniVanWoodenBoard.GetCarriedBy(player) != null ||
                MiniVanWinchHook.GetCarriedBy(player) != null)
            {
                return false;
            }

            if (Vector3.Distance(player.transform.position, transform.position) > PickupReach)
            {
                return false;
            }

            if (roofAttach != null)
            {
                MiniVanRoofAttachPoint attach = roofAttach;
                roofAttach = null;
                attach.NotifyWheelRemoved(this);
            }

            SetPlacementHighlighted(false);
            ClearVehicleSettle();
            carriedByPlayer[player] = this;
            carrier = player;
            transform.SetParent(null, true);
            if (hasFreeLocalScale)
            {
                transform.localScale = freeLocalScale;
            }

            ConfigureCarriedBody();
            // 3A: carried wheel is visual-only vs vans/players.
            IgnorePhysicsWithVehiclesAndPlayers(true);
            return true;
        }

        public void DropNear(MiniVanPlayer player)
        {
            if (carrier != null)
            {
                carriedByPlayer.Remove(carrier);
                carrier = null;
            }

            if (roofAttach != null)
            {
                MiniVanRoofAttachPoint attach = roofAttach;
                roofAttach = null;
                attach.NotifyWheelRemoved(this);
            }

            SetPlacementHighlighted(false);
            ClearVehicleSettle();
            transform.SetParent(null, true);
            if (hasFreeLocalScale)
            {
                transform.localScale = freeLocalScale;
            }

            if (player != null)
            {
                transform.position = player.transform.position + player.transform.forward * 0.9f + Vector3.up * 0.35f;
            }

            ConfigureFreeBody();
            // 2C: collide with van, but settle without shoving it.
            IgnorePhysicsWithVehiclesAndPlayers(false);
            IgnorePhysicsWithPlayersOnly(true);
            SyncBodyPoseFromTransform();
        }

        public void ClearCarrier()
        {
            if (carrier != null)
            {
                carriedByPlayer.Remove(carrier);
                carrier = null;
            }
        }

        public void SuppressVehiclePhysicsInfluence()
        {
            // Used right after detach spawn: allow contacts but don't launch the chassis.
            IgnorePhysicsWithVehiclesAndPlayers(false);
            IgnorePhysicsWithPlayersOnly(true);
        }

        /// <summary>
        /// Aim-weighted choice between roof rack and missing hub while carrying a wheel.
        /// </summary>
        public static IMiniVanGameModeInteractable FindBestCarryInteractable(
            MiniVanPlayer player,
            MiniVanDetachedWheel wheel)
        {
            if (player == null || wheel == null)
            {
                return null;
            }

            MiniVanRoofAttachPoint roof = MiniVanRoofAttachPoint.FindBestPlaceTarget(player, wheel);
            MiniVanWheelMountPoint mount = MiniVanWheelMountPoint.FindBestInstallTarget(player, wheel);
            if (roof == null)
            {
                return mount;
            }

            if (mount == null)
            {
                return roof;
            }

            Vector3 origin = player.PlayerCamera != null
                ? player.PlayerCamera.transform.position
                : player.transform.position + Vector3.up * 1.4f;
            Vector3 forward = player.PlayerCamera != null
                ? player.PlayerCamera.transform.forward
                : player.transform.forward;

            float Score(Transform t)
            {
                Vector3 to = t.position - origin;
                float distance = Mathf.Max(0.01f, to.magnitude);
                float facing = Vector3.Dot(forward, to / distance);
                // Strongly prefer what the camera is aimed at.
                return (-facing * 4f) + distance * 0.15f;
            }

            return Score(roof.transform) <= Score(mount.transform)
                ? (IMiniVanGameModeInteractable)roof
                : mount;
        }

        private void ConfigureInstalledBody()
        {
            if (body == null)
            {
                EnsurePhysics();
            }

            if (body != null)
            {
                if (!body.isKinematic)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }

                body.isKinematic = true;
                body.useGravity = false;
                body.detectCollisions = false;
            }

            SetCollidersEnabled(false);
        }

        private void EnsurePhysics()
        {
            body = GetComponent<Rigidbody>();
            if (body == null)
            {
                body = gameObject.AddComponent<Rigidbody>();
            }

            colliders = GetComponentsInChildren<Collider>(true);
            bool hasUsableCollider = false;
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null && !(colliders[i] is WheelCollider))
                {
                    hasUsableCollider = true;
                    break;
                }
            }

            if (!hasUsableCollider)
            {
                SphereCollider sphere = gameObject.AddComponent<SphereCollider>();
                sphere.radius = 0.52f;
                sphere.center = Vector3.zero;
                colliders = GetComponentsInChildren<Collider>(true);
            }
        }

        private void ConfigureCarriedBody()
        {
            if (body == null)
            {
                EnsurePhysics();
            }

            if (body != null)
            {
                body.isKinematic = true;
                body.useGravity = false;
                body.detectCollisions = false;
                body.interpolation = RigidbodyInterpolation.None;
            }

            SetCollidersEnabled(false);
            SyncBodyPoseFromTransform();
        }

        private void ConfigureFreeBody()
        {
            if (body == null)
            {
                EnsurePhysics();
            }

            if (body != null)
            {
                // Light enough that cabin contacts won't throw the chassis (2C).
                body.mass = 0.45f;
                body.linearDamping = 0.55f;
                body.angularDamping = 0.85f;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                body.isKinematic = false;
                body.useGravity = true;
                body.detectCollisions = true;
            }

            SetCollidersEnabled(true);
            SetCollidersTrigger(false);
        }

        private void ClearVehicleSettle()
        {
            vehicleContactSeconds = 0f;
            settlingVehicle = null;
        }

        private void OnCollisionStay(Collision collision)
        {
            if (IsCarried || roofAttach != null || body == null || body.isKinematic || collision == null)
            {
                return;
            }

            MiniVanVehicle van = collision.collider != null
                ? collision.collider.GetComponentInParent<MiniVanVehicle>()
                : null;
            if (van == null)
            {
                return;
            }

            Rigidbody vanBody = van.GetComponent<Rigidbody>();
            ContactPoint contact = collision.GetContact(0);
            Vector3 vanVel = vanBody != null ? vanBody.GetPointVelocity(contact.point) : Vector3.zero;
            Vector3 rel = body.linearVelocity - vanVel;
            float into = Vector3.Dot(rel, contact.normal);
            if (into < 0f)
            {
                // Remove the component that would shove the van.
                body.linearVelocity -= contact.normal * into;
            }

            body.linearVelocity = Vector3.Lerp(body.linearVelocity, vanVel, 0.4f);
            body.angularVelocity *= 0.75f;

            if (settlingVehicle != van)
            {
                settlingVehicle = van;
                vehicleContactSeconds = 0f;
            }

            vehicleContactSeconds += Time.fixedDeltaTime;
            if (vehicleContactSeconds >= 0.2f && rel.sqrMagnitude < 1.5f * 1.5f)
            {
                // Rest as a kinematic prop parented to the van — collision remains, no ongoing push.
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.isKinematic = true;
                body.detectCollisions = true;
                transform.SetParent(van.transform, true);
                SyncBodyPoseFromTransform();
            }
        }

        private void OnCollisionExit(Collision collision)
        {
            if (collision == null || collision.collider == null)
            {
                return;
            }

            MiniVanVehicle van = collision.collider.GetComponentInParent<MiniVanVehicle>();
            if (van != null && van == settlingVehicle && (body == null || !body.isKinematic))
            {
                vehicleContactSeconds = 0f;
            }
        }

        private void SyncBodyPoseFromTransform()
        {
            if (body == null)
            {
                return;
            }

            body.position = transform.position;
            body.rotation = transform.rotation;
        }

        private void IgnorePhysicsWithPlayersOnly(bool ignore)
        {
            if (colliders == null || colliders.Length == 0)
            {
                EnsurePhysics();
            }

            if (colliders == null)
            {
                return;
            }

            MiniVanPlayer[] players = FindObjectsByType<MiniVanPlayer>(FindObjectsSortMode.None);
            for (int p = 0; p < players.Length; p++)
            {
                MiniVanPlayer player = players[p];
                if (player == null)
                {
                    continue;
                }

                CharacterController cc = player.CharacterController;
                Collider[] playerColliders = player.GetComponentsInChildren<Collider>(true);
                for (int i = 0; i < colliders.Length; i++)
                {
                    Collider wheelCol = colliders[i];
                    if (wheelCol == null || wheelCol is WheelCollider)
                    {
                        continue;
                    }

                    if (cc != null)
                    {
                        Physics.IgnoreCollision(wheelCol, cc, ignore);
                    }

                    for (int j = 0; j < playerColliders.Length; j++)
                    {
                        Collider other = playerColliders[j];
                        if (other == null || other == wheelCol)
                        {
                            continue;
                        }

                        Physics.IgnoreCollision(wheelCol, other, ignore);
                    }
                }
            }
        }

        private void IgnorePhysicsWithVehiclesAndPlayers(bool ignore)
        {
            if (colliders == null || colliders.Length == 0)
            {
                EnsurePhysics();
            }

            if (colliders == null)
            {
                return;
            }

            MiniVanVehicle[] vehicles = FindObjectsByType<MiniVanVehicle>(FindObjectsSortMode.None);
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
                    Collider wheelCol = colliders[i];
                    if (wheelCol == null || wheelCol is WheelCollider)
                    {
                        continue;
                    }

                    for (int j = 0; j < vehicleColliders.Length; j++)
                    {
                        Collider other = vehicleColliders[j];
                        if (other == null || other is WheelCollider)
                        {
                            continue;
                        }

                        Physics.IgnoreCollision(wheelCol, other, ignore);
                    }
                }
            }

            IgnorePhysicsWithPlayersOnly(ignore);
        }

        private void SetCollidersEnabled(bool enabled)
        {
            if (colliders == null)
            {
                return;
            }

            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null && !(colliders[i] is WheelCollider))
                {
                    colliders[i].enabled = enabled;
                }
            }
        }

        private void SetCollidersTrigger(bool isTrigger)
        {
            if (colliders == null)
            {
                return;
            }

            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null && !(colliders[i] is WheelCollider))
                {
                    colliders[i].isTrigger = isTrigger;
                }
            }
        }

        private void ApplyCarryPose(MiniVanPlayer player)
        {
            if (player == null)
            {
                return;
            }

            if (hasFreeLocalScale)
            {
                transform.localScale = freeLocalScale;
            }

            if (player.IsRidingBoard)
            {
                Vector3 sidePosition = player.transform.TransformPoint(RidingCarryLocalPosition);
                Quaternion sideRotation = player.transform.rotation * Quaternion.Euler(RidingCarryLocalEuler);
                transform.SetPositionAndRotation(sidePosition, sideRotation);
                SyncBodyPoseFromTransform();
                return;
            }

            Transform cam = player.PlayerCamera != null
                ? player.PlayerCamera.transform
                : (player.CameraRoot != null ? player.CameraRoot : player.transform);
            Vector3 position = cam.TransformPoint(CarryLocalPosition);
            Quaternion rotation = cam.rotation * Quaternion.Euler(CarryLocalEuler);
            transform.SetPositionAndRotation(position, rotation);
            SyncBodyPoseFromTransform();
        }

        private void RememberFreeScale()
        {
            freeLocalScale = transform.localScale;
            if (freeLocalScale.sqrMagnitude < 1e-8f)
            {
                freeLocalScale = Vector3.one;
            }

            hasFreeLocalScale = true;
        }

        private void CacheHighlightMaterials()
        {
            if (highlightRenderers == null)
            {
                return;
            }

            originalMaterials = new Material[highlightRenderers.Length][];
            for (int i = 0; i < highlightRenderers.Length; i++)
            {
                originalMaterials[i] = highlightRenderers[i] != null
                    ? highlightRenderers[i].sharedMaterials
                    : System.Array.Empty<Material>();
            }
        }

        private Material GetPlacementOutlineMaterial()
        {
            if (outlineMaterial != null)
            {
                return outlineMaterial;
            }

            Material shared = Resources.Load<Material>("Panelka/ThinWhiteOutline");
            Shader shader = Shader.Find("MiniVanGame/ThinWhiteOutline");
            if (shared != null)
            {
                outlineMaterial = new Material(shared) { name = "Wheel Roof Place Green Outline" };
            }
            else
            {
                outlineMaterial = new Material(shader != null ? shader : Shader.Find("Standard"))
                {
                    name = "Wheel Roof Place Green Outline"
                };
            }

            MiniVanSnesOutline.ApplyOutlineSettings(outlineMaterial, new Color(0.2f, 1f, 0.35f, 1f));
            return outlineMaterial;
        }
    }

    [DisallowMultipleComponent]
    public sealed class MiniVanWheelMountPoint : MonoBehaviour, IMiniVanGameModeInteractable
    {
        private const float InstallReach = 3.25f;
        private const float FacingDotMin = 0.15f;

        public MiniVanVehicle Vehicle;
        public int WheelIndex;
        public Transform SourceWheelVisual;
        public Material GhostMaterial;

        private GameObject ghost;

        private void Update()
        {
            // Keep mount aligned with the wheel hub (visual may be hidden while detached).
            if (SourceWheelVisual != null)
            {
                transform.SetPositionAndRotation(SourceWheelVisual.position, SourceWheelVisual.rotation);
            }

            UpdateGhost();
        }

        public string GetPrompt(MiniVanPlayer player)
        {
            if (!CanInstall(player))
            {
                return string.Empty;
            }

            MiniVanDetachedWheel wheel = MiniVanDetachedWheel.GetCarriedBy(player);
            return wheel != null && wheel.IsSpare
                ? "E - install spare wheel"
                : "E - install wheel";
        }

        public void Interact(MiniVanPlayer player)
        {
            TryInstall(player);
        }

        public void PrimaryAction(MiniVanPlayer player)
        {
        }

        public bool TryInstall(MiniVanPlayer player)
        {
            if (Input.GetMouseButton(1) || !CanInstall(player))
            {
                return false;
            }

            MiniVanDetachedWheel wheel = MiniVanDetachedWheel.GetCarriedBy(player);
            if (wheel == null || Vehicle == null)
            {
                return false;
            }

            wheel.ClearCarrier();
            if (Vehicle.IsSpawned && !Vehicle.IsServer)
            {
                Vehicle.RequestReattachDetachedWheelServerRpc(wheel.IsSpare, wheel.WheelIndex);
                Destroy(wheel.gameObject);
                return true;
            }

            return Vehicle.TryReattachDetachedWheel(wheel, player);
        }

        /// <summary>
        /// Prefer a nearby installable hub while carrying a wheel — raycasts usually hit the van body first.
        /// </summary>
        public static MiniVanWheelMountPoint FindBestInstallTarget(MiniVanPlayer player, MiniVanDetachedWheel wheel)
        {
            if (player == null || wheel == null)
            {
                return null;
            }

            MiniVanWheelMountPoint[] mounts = FindObjectsByType<MiniVanWheelMountPoint>(FindObjectsSortMode.None);
            MiniVanWheelMountPoint best = null;
            float bestScore = float.MaxValue;
            Vector3 playerPos = player.transform.position;
            Vector3 look = player.PlayerCamera != null
                ? player.PlayerCamera.transform.forward
                : player.transform.forward;

            for (int i = 0; i < mounts.Length; i++)
            {
                MiniVanWheelMountPoint mount = mounts[i];
                if (mount == null || !mount.CanInstall(player, wheel))
                {
                    continue;
                }

                Vector3 toMount = mount.transform.position - playerPos;
                float distance = toMount.magnitude;
                if (distance > InstallReach)
                {
                    continue;
                }

                float facing = distance > 0.01f ? Vector3.Dot(look, toMount / distance) : 1f;
                if (facing < FacingDotMin)
                {
                    continue;
                }

                // Prefer closer + more centered in view.
                float score = distance - facing * 0.75f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = mount;
                }
            }

            return best;
        }

        private void UpdateGhost()
        {
            MiniVanPlayer player = MiniVanPlayer.LocalPlayer;
            MiniVanDetachedWheel carried = MiniVanDetachedWheel.GetCarriedBy(player);
            bool visible = CanInstall(player, carried) && IsRoughlyFacing(player);
            if (!visible)
            {
                if (ghost != null)
                {
                    ghost.SetActive(false);
                }

                return;
            }

            EnsureGhost();
            if (ghost != null)
            {
                ghost.transform.SetPositionAndRotation(transform.position, transform.rotation);
                ghost.SetActive(true);
            }
        }

        public bool CanInstall(MiniVanPlayer player)
        {
            return CanInstall(player, MiniVanDetachedWheel.GetCarriedBy(player));
        }

        public bool CanInstall(MiniVanPlayer player, MiniVanDetachedWheel wheel)
        {
            if (player == null || Vehicle == null || wheel == null)
            {
                return false;
            }

            if (Vehicle.DetachedWheelIndex.Value != WheelIndex)
            {
                return false;
            }

            if (Vector3.Distance(player.transform.position, transform.position) > InstallReach)
            {
                return false;
            }

            if (wheel.IsSpare)
            {
                return true;
            }

            return wheel.Vehicle == Vehicle && wheel.WheelIndex == WheelIndex;
        }

        private bool IsRoughlyFacing(MiniVanPlayer player)
        {
            if (player == null)
            {
                return false;
            }

            Vector3 origin = player.PlayerCamera != null
                ? player.PlayerCamera.transform.position
                : player.transform.position + Vector3.up * 1.4f;
            Vector3 forward = player.PlayerCamera != null
                ? player.PlayerCamera.transform.forward
                : player.transform.forward;
            Vector3 toMount = transform.position - origin;
            if (toMount.sqrMagnitude < 0.01f)
            {
                return true;
            }

            return Vector3.Dot(forward, toMount.normalized) >= FacingDotMin;
        }

        private void EnsureGhost()
        {
            if (ghost != null)
            {
                return;
            }

            if (SourceWheelVisual != null)
            {
                ghost = Instantiate(SourceWheelVisual.gameObject, transform.position, transform.rotation, transform);
                ghost.name = "Wheel Install Ghost";
                ghost.SetActive(true);
            }
            else
            {
                ghost = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                ghost.name = "Wheel Install Ghost";
                ghost.transform.SetParent(transform, false);
                ghost.transform.localScale = new Vector3(0.9f, 0.28f, 0.9f);
            }

            Collider[] ghostColliders = ghost.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < ghostColliders.Length; i++)
            {
                if (ghostColliders[i] != null)
                {
                    ghostColliders[i].enabled = false;
                }
            }

            Renderer[] renderers = ghost.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null && GhostMaterial != null)
                {
                    renderers[i].sharedMaterial = GhostMaterial;
                }
            }
        }
    }
}
