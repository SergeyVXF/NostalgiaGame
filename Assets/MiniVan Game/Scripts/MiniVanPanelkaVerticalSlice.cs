using System.Collections.Generic;
using UnityEngine;

namespace MiniVanGame
{
    public enum MiniVanPanelkaInteractableType
    {
        Door,
        RoofHatch,
        Key,
        Vacuum,
        GoldenCockroach,
        RoachTunnel,
        RoachExit,
        Water,
        ExitDoor,
        Note,
        Cabinet,
        CabinetLoot
    }

    public partial class MiniVanPanelkaInteractionController
    {
        public float InteractDistance = 3.6f;
        public float ActivationDistance = 48f;
        public Material OutlineMaterial;

        private MiniVanPanelkaInteractable lookedAt;
        private readonly RaycastHit[] raycastHits = new RaycastHit[32];
        private MiniVanGameModeInteractionSystem gameModeInteraction;
        private bool searchedGameModeInteraction;
        private float nextLookScanTime;

        protected void Update()
        {
            if (ShouldLetGameModeInteractionHandleInput())
            {
                SetLookedAt(null);
                return;
            }

            MiniVanPlayer player = MiniVanPlayer.LocalPlayer;
            if (player == null || player.PlayerCamera == null)
            {
                SetLookedAt(null);
                return;
            }

            if ((player.transform.position - transform.position).sqrMagnitude >
                ActivationDistance * ActivationDistance)
            {
                SetLookedAt(null);
                return;
            }

            bool forceScan = MiniVanKeyBindings.GetKeyDown(MiniVanKeyAction.Interact) ||
                Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1);
            if (!forceScan && Time.unscaledTime < nextLookScanTime)
            {
                return;
            }
            nextLookScanTime = Time.unscaledTime + 0.06f;

            Ray ray = new Ray(player.PlayerCamera.transform.position, player.PlayerCamera.transform.forward);
            MiniVanPanelkaInteractable target = FindLookedAt(ray);
            SetLookedAt(target);

            MiniVanPanelkaPlayerState state = MiniVanPanelkaPlayerState.GetOrAdd(player);
            if (lookedAt != null)
            {
                if (MiniVanKeyBindings.GetKeyDown(MiniVanKeyAction.Interact))
                {
                    lookedAt.Interact(player, state, MiniVanPanelkaInput.Primary);
                }
                else if (Input.GetMouseButtonDown(0))
                {
                    lookedAt.Interact(player, state, MiniVanPanelkaInput.LeftMouse);
                }
                else if (Input.GetMouseButtonDown(1))
                {
                    lookedAt.Interact(player, state, MiniVanPanelkaInput.RightMouse);
                }
            }

            if (state.IsRoach && Input.GetMouseButtonDown(1))
            {
                state.ReturnHumanNear(player.transform.position);
            }
        }

        private bool ShouldLetGameModeInteractionHandleInput()
        {
            if (!searchedGameModeInteraction || gameModeInteraction == null)
            {
                gameModeInteraction =
                    Object.FindFirstObjectByType<MiniVanGameModeInteractionSystem>(
                        FindObjectsInactive.Exclude);
                searchedGameModeInteraction = true;
            }

            return gameModeInteraction != null && gameModeInteraction.isActiveAndEnabled;
        }

        private MiniVanPanelkaInteractable FindLookedAt(Ray ray)
        {
            int hitCount = Physics.RaycastNonAlloc(
                ray, raycastHits, InteractDistance, ~0, QueryTriggerInteraction.Collide);
            float bestDistance = float.MaxValue;
            MiniVanPanelkaInteractable best = null;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = raycastHits[i];
                MiniVanPanelkaInteractable interactable = hit.collider != null
                    ? hit.collider.GetComponentInParent<MiniVanPanelkaInteractable>()
                    : null;
                if (interactable == null || !interactable.CanBeLookedAt ||
                    !interactable.transform.IsChildOf(transform))
                {
                    continue;
                }

                if (hit.distance < bestDistance)
                {
                    bestDistance = hit.distance;
                    best = interactable;
                }
            }

            return best;
        }

        private void SetLookedAt(MiniVanPanelkaInteractable target)
        {
            if (lookedAt == target)
            {
                return;
            }

            if (lookedAt != null)
            {
                lookedAt.SetOutlined(false, null);
            }

            lookedAt = target;

            if (lookedAt != null)
            {
                lookedAt.SetOutlined(true, null);
            }
        }
    }

    public enum MiniVanPanelkaInput
    {
        Primary,
        LeftMouse,
        RightMouse
    }

    public partial class MiniVanPanelkaInteractable : IMiniVanGameModeInteractable
    {
        public MiniVanPanelkaInteractableType Type;
        public bool StartsOpen;
        public bool RequiresKey;
        public string KeyId = "panelka-key-01";
        public Transform Pivot;
        public Vector3 ClosedEuler;
        public Vector3 OpenEuler = new Vector3(0f, -88f, 0f);
        public Transform TeleportTarget;
        public GameObject LinkedObject;
        public string Message;
        [Tooltip("Direction from the locked entrance door toward the landing, in the door parent's local space.")]
        public Vector3 LockedApproachDirection;
        [Min(1f)] public float DoorAnimationSpeed = 14f;


        private bool isOpen;
        private bool hasDoorTargetRotation;
        private Quaternion targetDoorRotation;
        private bool consumed;
        private Renderer[] renderers;
        private Material[][] originalMaterials;

        public bool CanBeLookedAt => !consumed && gameObject.activeInHierarchy;
        public bool CanShowOutline => Type != MiniVanPanelkaInteractableType.RoofHatch &&
                                      !HasNameInHierarchy("Hatch") &&
                                      !HasNameInHierarchy("Apartment_Entrance_Door");

        public string GetPrompt(MiniVanPlayer player)
        {
            if (consumed)
            {
                return string.Empty;
            }

            switch (Type)
            {
                case MiniVanPanelkaInteractableType.Door:
                case MiniVanPanelkaInteractableType.ExitDoor:
                    if (RequiresKey && !PlayerCanOpenLockedDoorFromThisSide(player))
                    {
                        return string.IsNullOrEmpty(Message) ? "Locked door" : Message;
                    }
                    return isOpen ? "Close door" : "Open door";
                case MiniVanPanelkaInteractableType.RoofHatch:
                    return isOpen ? "Close hatch" : "Open hatch";
                case MiniVanPanelkaInteractableType.Key:
                    return string.IsNullOrEmpty(Message) ? "Take key" : Message;
                case MiniVanPanelkaInteractableType.Vacuum:
                    return "Take vacuum";
                case MiniVanPanelkaInteractableType.GoldenCockroach:
                    return "Eat";
                case MiniVanPanelkaInteractableType.RoachTunnel:
                    return "Enter";
                case MiniVanPanelkaInteractableType.RoachExit:
                    return "Exit";
                case MiniVanPanelkaInteractableType.Water:
                    return "Clean";
                case MiniVanPanelkaInteractableType.Cabinet:
                    return "Open";
                case MiniVanPanelkaInteractableType.CabinetLoot:
                    return "Take";
                case MiniVanPanelkaInteractableType.Note:
                    return string.IsNullOrEmpty(Message) ? "Read" : Message;
                default:
                    return string.Empty;
            }
        }

        public void Interact(MiniVanPlayer player)
        {
            Interact(player, MiniVanPanelkaPlayerState.GetOrAdd(player), MiniVanPanelkaInput.Primary);
        }

        public void PrimaryAction(MiniVanPlayer player)
        {
            Interact(player, MiniVanPanelkaPlayerState.GetOrAdd(player), MiniVanPanelkaInput.LeftMouse);
        }

        protected void Awake()
        {
            isOpen = StartsOpen;
            CacheRenderers();
            if (UsesDoorPose())
            {
                Pivot = Pivot != null ? Pivot : transform;
                ApplyDoorPose(true);
            }
        }

        private void Update()
        {
            if (!UsesDoorPose() || Pivot == null || !hasDoorTargetRotation)
            {
                return;
            }

            float blend = 1f - Mathf.Exp(-DoorAnimationSpeed * Time.deltaTime);
            Pivot.localRotation = Quaternion.Slerp(
                Pivot.localRotation,
                targetDoorRotation,
                blend);
            if (Quaternion.Angle(Pivot.localRotation, targetDoorRotation) < 0.25f)
            {
                Pivot.localRotation = targetDoorRotation;
            }
        }

        private bool UsesDoorPose()
        {
            return Type == MiniVanPanelkaInteractableType.Door ||
                   Type == MiniVanPanelkaInteractableType.RoofHatch ||
                   Type == MiniVanPanelkaInteractableType.ExitDoor;
        }

public void Interact(MiniVanPlayer player, MiniVanPanelkaPlayerState state, MiniVanPanelkaInput input)
        {
            if (player == null || state == null || consumed)
            {
                return;
            }

            switch (Type)
            {
                case MiniVanPanelkaInteractableType.Door:
                case MiniVanPanelkaInteractableType.RoofHatch:
                case MiniVanPanelkaInteractableType.ExitDoor:
                    ToggleDoor(player, input);
                    break;
                case MiniVanPanelkaInteractableType.Key:
                    if (input == MiniVanPanelkaInput.Primary &&
                        player.TryPickupPanelkaKey(KeyId))
                    {
                        Consume();
                    }
                    break;
                case MiniVanPanelkaInteractableType.Vacuum:
                    state.HasVacuum = true;
                    Consume();
                    break;
                case MiniVanPanelkaInteractableType.GoldenCockroach:
                    state.BecomeRoach();
                    Consume();
                    break;
                case MiniVanPanelkaInteractableType.RoachTunnel:
                    if (state.IsRoach && TeleportTarget != null)
                    {
                        state.TeleportPlayer(TeleportTarget.position);
                    }
                    break;
                case MiniVanPanelkaInteractableType.RoachExit:
                    if (state.IsRoach)
                    {
                        state.ReturnHumanNear(transform.position + transform.forward * 0.75f);
                    }
                    break;
                case MiniVanPanelkaInteractableType.Water:
                    if (state.HasVacuum)
                    {
                        MiniVanPanelkaWater water = GetComponent<MiniVanPanelkaWater>();
                        if (water != null)
                        {
                            water.StartDraining();
                        }
                        else
                        {
                            Consume();
                        }
                    }
                    break;
                case MiniVanPanelkaInteractableType.Cabinet:
                    if (input == MiniVanPanelkaInput.Primary)
                    {
                        player.TryTogglePanelkaCabinet(GetComponent<MiniVanPanelkaCabinet>());
                    }
                    break;
                case MiniVanPanelkaInteractableType.CabinetLoot:
                    if (input == MiniVanPanelkaInput.Primary)
                    {
                        player.TryPickupPanelkaCabinetLoot(GetComponent<MiniVanPanelkaCabinetLoot>());
                    }
                    break;
                case MiniVanPanelkaInteractableType.Note:
                    Debug.Log("[Panelka] " + Message);
                    break;
            }
        }

        private void ToggleDoor(MiniVanPlayer player, MiniVanPanelkaInput input)
        {
            if (RequiresKey)
            {
                if (player == null)
                {
                    return;
                }

                float approachDot = GetLockedApproachDot(player);
                bool hasMatchingKey =
                    (input == MiniVanPanelkaInput.Primary ||
                     input == MiniVanPanelkaInput.LeftMouse) &&
                    player.TryUseSelectedPanelkaKey(KeyId);
                if (hasMatchingKey)
                {
                    RequiresKey = false;
                }
                else if (approachDot > -0.15f)
                {
                    return;
                }
            }

            isOpen = !isOpen;
            ApplyDoorPose(false);
        }

        private bool PlayerCanOpenLockedDoorFromThisSide(MiniVanPlayer player)
        {
            return player != null && GetLockedApproachDot(player) <= -0.15f;
        }

public bool IsOpen => isOpen;

        public void ForceOpenForNpc()
        {
            if (Type != MiniVanPanelkaInteractableType.Door &&
                Type != MiniVanPanelkaInteractableType.ExitDoor &&
                Type != MiniVanPanelkaInteractableType.RoofHatch)
            {
                return;
            }

            if (RequiresKey)
            {
                RequiresKey = false;
            }

            if (isOpen)
            {
                return;
            }

            isOpen = true;
            ApplyDoorPose(false);
        }

private float GetLockedApproachDot(MiniVanPlayer player)
        {
            if (player == null)
            {
                return 0f;
            }

            if (LockedApproachDirection.sqrMagnitude < 0.001f)
            {
                return 1f;
            }

            Transform pivotTransform = Pivot != null ? Pivot : transform;
            Transform directionSpace = pivotTransform.parent;
            Vector3 outsideDirection = directionSpace != null
                ? directionSpace.TransformDirection(LockedApproachDirection.normalized)
                : LockedApproachDirection.normalized;
            Vector3 toPlayer = player.transform.position - pivotTransform.position;
            outsideDirection.y = 0f;
            toPlayer.y = 0f;

            if (outsideDirection.sqrMagnitude < 0.001f || toPlayer.sqrMagnitude < 0.001f)
            {
                return 0f;
            }

            return Vector3.Dot(toPlayer.normalized, outsideDirection.normalized);
        }


private void ApplyDoorPose(bool instant)
        {
            if (Pivot == null)
            {
                return;
            }

            if (!isOpen)
            {
                SetDoorTarget(Quaternion.Euler(ClosedEuler), instant);
                return;
            }

            if (Type == MiniVanPanelkaInteractableType.Door ||
                Type == MiniVanPanelkaInteractableType.ExitDoor)
            {
                SetDoorTarget(ResolveSafeDoorRotation(), instant);
                return;
            }

            SetDoorTarget(Quaternion.Euler(OpenEuler), instant);
        }

        private void SetDoorTarget(Quaternion rotation, bool instant)
        {
            targetDoorRotation = rotation;
            hasDoorTargetRotation = true;
            if (instant)
            {
                Pivot.localRotation = targetDoorRotation;
            }
        }

private Quaternion ResolveSafeDoorRotation()
        {
            const float maximumAngle = 117f;
            const float minimumAngle = 58f;
            const float angleStep = 8f;

            float requestedAngle = Mathf.DeltaAngle(0f, OpenEuler.y);
            float preferredSign = requestedAngle < 0f ? -1f : 1f;
            float requestedMagnitude = Mathf.Min(Mathf.Abs(requestedAngle), maximumAngle);
            Quaternion bestRotation = Quaternion.Euler(
                OpenEuler.x,
                preferredSign * requestedMagnitude,
                OpenEuler.z);
            int bestObstructionCount = int.MaxValue;

            for (float angle = requestedMagnitude; angle >= minimumAngle; angle -= angleStep)
            {
                for (int directionIndex = 0; directionIndex < 2; directionIndex++)
                {
                    float sign = directionIndex == 0 ? preferredSign : -preferredSign;
                    Quaternion candidate = Quaternion.Euler(OpenEuler.x, sign * angle, OpenEuler.z);
                    int obstructionCount = CountDoorObstructions(candidate);
                    if (obstructionCount < bestObstructionCount)
                    {
                        bestObstructionCount = obstructionCount;
                        bestRotation = candidate;
                    }

                    if (obstructionCount == 0)
                    {
                        return candidate;
                    }
                }
            }

            return bestRotation;
        }

        private int CountDoorObstructions(Quaternion candidateLocalRotation)
        {
            MeshFilter panelMesh = FindDoorPanelMesh();
            if (panelMesh == null || panelMesh.sharedMesh == null)
            {
                return 0;
            }

            Quaternion previousRotation = Pivot.localRotation;
            Pivot.localRotation = candidateLocalRotation;
            Physics.SyncTransforms();

            Bounds localBounds = panelMesh.sharedMesh.bounds;
            Vector3 scale = panelMesh.transform.lossyScale;
            Vector3 halfExtents = Vector3.Scale(
                localBounds.extents,
                new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z))) * 0.75f;
            Vector3 center = panelMesh.transform.TransformPoint(localBounds.center);
            Collider[] overlaps = Physics.OverlapBox(
                center,
                halfExtents,
                panelMesh.transform.rotation,
                ~0,
                QueryTriggerInteraction.Ignore);

            int obstructionCount = 0;
            for (int i = 0; i < overlaps.Length; i++)
            {
                Collider overlap = overlaps[i];
                if (overlap == null ||
                    overlap.isTrigger ||
                    overlap.transform.IsChildOf(Pivot))
                {
                    continue;
                }

                obstructionCount++;
            }

            Pivot.localRotation = previousRotation;
            Physics.SyncTransforms();
            return obstructionCount;
        }

        private MeshFilter FindDoorPanelMesh()
        {
            MeshFilter[] filters = Pivot.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < filters.Length; i++)
            {
                if (filters[i] != null &&
                    (filters[i].name == "Door_Panel" || filters[i].name == "Hatch_Panel"))
                {
                    return filters[i];
                }
            }

            return filters.Length > 0 ? filters[0] : null;
        }


        private void Consume()
        {
            consumed = true;
            SetOutlined(false, null);
            gameObject.SetActive(false);
        }

        public void SetOutlined(bool value, Material outlineMaterial)
        {
            if (value && !CanShowOutline)
            {
                return;
            }

            CacheRenderers();
            if (renderers == null)
            {
                return;
            }

            if (value)
            {
                Material outline = outlineMaterial != null ? outlineMaterial : GetFallbackOutlineMaterial();
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i] == null)
                    {
                        continue;
                    }

                    if (renderers[i].GetComponent<TextMesh>() != null)
                    {
                        continue;
                    }

                    Material[] source = renderers[i].sharedMaterials;
                    Material[] highlighted = new Material[source.Length + 1];
                    for (int j = 0; j < source.Length; j++)
                    {
                        highlighted[j] = source[j];
                    }
                    highlighted[highlighted.Length - 1] = outline;
                    renderers[i].sharedMaterials = highlighted;
                }
                return;
            }

            if (originalMaterials == null)
            {
                return;
            }

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null && i < originalMaterials.Length)
                {
                    renderers[i].sharedMaterials = originalMaterials[i];
                }
            }
        }

        private void CacheRenderers()
        {
            if (renderers != null)
            {
                return;
            }

            renderers = GetComponentsInChildren<Renderer>(true);
            originalMaterials = new Material[renderers.Length][];
            for (int i = 0; i < renderers.Length; i++)
            {
                originalMaterials[i] = renderers[i] != null ? renderers[i].sharedMaterials : new Material[0];
            }
        }

        private static Material fallbackOutline;

        private bool HasNameInHierarchy(string fragment)
        {
            Transform cursor = transform;
            while (cursor != null)
            {
                if (cursor.name.IndexOf(fragment, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }

                cursor = cursor.parent;
            }

            return false;
        }

        private static Material GetFallbackOutlineMaterial()
        {
            if (fallbackOutline != null)
            {
                return fallbackOutline;
            }

            fallbackOutline = Resources.Load<Material>("Panelka/ThinWhiteOutline");
            if (fallbackOutline != null)
            {
                fallbackOutline = new Material(fallbackOutline)
                {
                    name = "Panelka Thin White Outline"
                };
                MiniVanSnesOutline.ApplyOutlineSettings(fallbackOutline, Color.white);
                return fallbackOutline;
            }

            Shader shader = Shader.Find("MiniVanGame/ThinWhiteOutline");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }
            fallbackOutline = new Material(shader != null ? shader : Shader.Find("Standard"));
            fallbackOutline.name = "Panelka Thin White Outline";
            MiniVanSnesOutline.ApplyOutlineSettings(fallbackOutline, Color.white);
            return fallbackOutline;
        }
    }

    public class MiniVanPanelkaPlayerState : MonoBehaviour
    {
        public bool HasVacuum;
        public bool IsRoach { get; private set; }

        private readonly HashSet<string> keys = new HashSet<string>();
        private MiniVanPlayer player;
        private CharacterController controller;
        private Vector3 originalScale = Vector3.one;
        private float originalHeight;
        private float originalRadius;
        private Vector3 originalCenter;
        private float originalWalkSpeed;
        private Vector3 lastHumanPosition;

        public static MiniVanPanelkaPlayerState GetOrAdd(MiniVanPlayer target)
        {
            MiniVanPanelkaPlayerState state = target != null ? target.GetComponent<MiniVanPanelkaPlayerState>() : null;
            if (state == null && target != null)
            {
                state = target.gameObject.AddComponent<MiniVanPanelkaPlayerState>();
            }
            return state;
        }

        private void Awake()
        {
            Cache();
        }

        private void Cache()
        {
            player = player != null ? player : GetComponent<MiniVanPlayer>();
            controller = controller != null ? controller : GetComponent<CharacterController>();
        }

        public void AddKey(string keyId)
        {
            if (!string.IsNullOrEmpty(keyId))
            {
                keys.Add(keyId);
                Debug.Log("[Panelka] Picked key: " + keyId);
            }
        }

        public bool HasKey(string keyId)
        {
            return string.IsNullOrEmpty(keyId) || keys.Contains(keyId);
        }

        public void BecomeRoach()
        {
            Cache();
            if (IsRoach || player == null)
            {
                return;
            }

            IsRoach = true;
            lastHumanPosition = transform.position;
            originalScale = transform.localScale;
            originalWalkSpeed = player.WalkSpeed;

            if (controller != null)
            {
                originalHeight = controller.height;
                originalRadius = controller.radius;
                originalCenter = controller.center;
                controller.height = 0.35f;
                controller.radius = 0.12f;
                controller.center = new Vector3(0f, 0.18f, 0f);
            }

            transform.localScale = originalScale * 0.18f;
            player.WalkSpeed = Mathf.Max(1.2f, originalWalkSpeed * 0.65f);
            Debug.Log("[Panelka] Golden cockroach eaten. Roach mode: RMB near exit marker to return human.");
        }

        public void ReturnHumanNear(Vector3 position)
        {
            Cache();
            if (!IsRoach)
            {
                return;
            }

            IsRoach = false;
            transform.localScale = originalScale;
            player.WalkSpeed = originalWalkSpeed > 0.01f ? originalWalkSpeed : player.WalkSpeed;

            if (controller != null)
            {
                controller.height = originalHeight > 0.01f ? originalHeight : controller.height;
                controller.radius = originalRadius > 0.01f ? originalRadius : controller.radius;
                controller.center = originalCenter;
            }

            TeleportPlayer(position);
            Debug.Log("[Panelka] Returned human.");
        }

        public void TeleportPlayer(Vector3 position)
        {
            Cache();
            if (controller != null)
            {
                bool wasEnabled = controller.enabled;
                controller.enabled = false;
                transform.position = position;
                controller.enabled = wasEnabled;
            }
            else
            {
                transform.position = position;
            }
        }

        public void CatCaughtRoach(Vector3 safePosition)
        {
            if (!IsRoach)
            {
                return;
            }

            Debug.Log("[Panelka] Cat caught the roach.");
            ReturnHumanNear(safePosition != Vector3.zero ? safePosition : lastHumanPosition);
        }
    }

    public class MiniVanPanelkaCat : MonoBehaviour
    {
        public float DetectRadius = 5f;
        public float CatchDistance = 0.42f;
        public float MoveSpeed = 2.4f;
        public Transform SafeReturnPoint;

        private Vector3 home;

        private void Awake()
        {
            home = transform.position;
        }

        private void Update()
        {
            MiniVanPlayer player = MiniVanPlayer.LocalPlayer;
            MiniVanPanelkaPlayerState state = player != null ? player.GetComponent<MiniVanPanelkaPlayerState>() : null;
            if (player == null || state == null || !state.IsRoach)
            {
                MoveToward(home, MoveSpeed * 0.35f);
                return;
            }

            float distance = Vector3.Distance(transform.position, player.transform.position);
            if (distance > DetectRadius)
            {
                MoveToward(home, MoveSpeed * 0.35f);
                return;
            }

            MoveToward(player.transform.position, MoveSpeed);
            if (distance <= CatchDistance)
            {
                Vector3 safe = SafeReturnPoint != null ? SafeReturnPoint.position : home;
                state.CatCaughtRoach(safe);
            }
        }

        private void MoveToward(Vector3 target, float speed)
        {
            Vector3 flat = Vector3.ProjectOnPlane(target - transform.position, Vector3.up);
            if (flat.sqrMagnitude < 0.001f)
            {
                return;
            }

            transform.position += flat.normalized * speed * Time.deltaTime;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(flat.normalized, Vector3.up), Time.deltaTime * 8f);
        }
    }

    public class MiniVanPanelkaWater : MonoBehaviour
    {
        public float DrainSeconds = 2.25f;

        private bool draining;
        private float drainTimer;
        private Vector3 startScale;

        private void Awake()
        {
            startScale = transform.localScale;
        }

        public void StartDraining()
        {
            draining = true;
            drainTimer = 0f;
        }

        private void Update()
        {
            if (!draining)
            {
                return;
            }

            drainTimer += Time.deltaTime;
            float t = Mathf.Clamp01(drainTimer / Mathf.Max(0.05f, DrainSeconds));
            transform.localScale = Vector3.Lerp(startScale, new Vector3(startScale.x, 0.02f, startScale.z), t);
            if (t >= 1f)
            {
                gameObject.SetActive(false);
            }
        }
    }
}
