using UnityEngine;

namespace MiniVanGame
{
    [DisallowMultipleComponent]
    public sealed class MiniVanApartmentDoor : MonoBehaviour, IMiniVanGameModeInteractable
    {
        [SerializeField] private Transform pivot;
        [SerializeField] private Vector3 closedEuler;
        [SerializeField] private Vector3 openEuler = new Vector3(0f, 110f, 0f);
        [SerializeField, Min(1f)] private float animationSpeed = 12f;
        [SerializeField] private bool startsOpen;
        [SerializeField] private bool stopAtWall = true;
        [SerializeField, Range(60f, 120f)] private float maximumOpenAngle = 115f;
        [SerializeField, Range(1f, 10f)] private float wallCheckStep = 3f;

        private bool isOpen;
        private Quaternion targetRotation;
        private Vector3 resolvedOpenEuler;
        private bool openPoseResolved;
        private Vector3 cachedInsideDirection;
        private bool insideDirectionResolved;
        private readonly Collider[] wallCheckBuffer = new Collider[64];

        public Transform Pivot => pivot;
        public Vector3 ClosedEuler => closedEuler;
        public Vector3 OpenEuler => openEuler;
        public Vector3 EffectiveOpenEuler =>
            openPoseResolved ? resolvedOpenEuler : GetHardLimitedOpenEuler();
        public bool IsOpen => isOpen;

        public void Configure(
            Transform doorPivot,
            Vector3 closedRotation,
            Vector3 openRotation,
            float speed = 12f)
        {
            pivot = doorPivot;
            closedEuler = closedRotation;
            openEuler = openRotation;
            animationSpeed = Mathf.Max(1f, speed);
            isOpen = false;
            openPoseResolved = false;
            ApplyPose(true);
        }

        private void Awake()
        {
            pivot = pivot != null ? pivot : transform;
            isOpen = startsOpen;
            openPoseResolved = false;
            ApplyPose(true);
        }

        private void Update()
        {
            if (pivot == null)
                return;

            float blend = 1f - Mathf.Exp(-animationSpeed * Time.deltaTime);
            pivot.localRotation = Quaternion.Slerp(
                pivot.localRotation,
                targetRotation,
                blend);
            if (Quaternion.Angle(pivot.localRotation, targetRotation) < 0.2f)
                pivot.localRotation = targetRotation;
        }

        public string GetPrompt(MiniVanPlayer player)
        {
            MiniVanApartmentDoorLock doorLock = GetComponent<MiniVanApartmentDoorLock>();
            if (doorLock != null && doorLock.IsLocked && !IsPlayerInsideApartment(player))
                return doorLock.GetPrompt(player);
            return isOpen ? "Close door" : "Open door";
        }

        public void Interact(MiniVanPlayer player)
        {
            TryToggle(player);
        }

        public void PrimaryAction(MiniVanPlayer player)
        {
            TryToggle(player);
        }

        public bool TryToggle(MiniVanPlayer player)
        {
            MiniVanApartmentDoorLock doorLock = GetComponent<MiniVanApartmentDoorLock>();
            if (doorLock != null && doorLock.IsLocked)
            {
                if (IsPlayerInsideApartment(player))
                    doorLock.UnlockFromInside();
                else if (!doorLock.TryUnlock(player))
                    return false;
            }

            SetOpen(!isOpen);
            return true;
        }

        private bool IsPlayerInsideApartment(MiniVanPlayer player)
        {
            Vector3 insideDirection;
            if (player == null || !TryGetInsideDirection(out insideDirection))
                return false;

            Vector3 toPlayer = player.transform.position - transform.position;
            return Vector3.Dot(toPlayer, insideDirection) > 0f;
        }

        private bool TryGetInsideDirection(out Vector3 insideDirection)
        {
            if (!insideDirectionResolved)
                ResolveInsideDirection();

            insideDirection = cachedInsideDirection;
            return insideDirection.sqrMagnitude > 0.0001f;
        }

        private void ResolveInsideDirection()
        {
            insideDirectionResolved = true;
            cachedInsideDirection = Vector3.zero;

            MiniVanPanelkaApartmentRouteMarker apartment =
                GetComponentInParent<MiniVanPanelkaApartmentRouteMarker>();
            if (apartment == null)
                return;

            Bounds bounds;
            if (!TryGetApartmentBounds(apartment.transform, out bounds))
                return;

            // Split the world at the closed door plane. Mirrored apartment templates flip
            // local axes, so take the side the apartment volume actually sits on.
            Transform hinge = pivot != null && pivot.parent != null ? pivot.parent : transform;
            Vector3 normal = hinge.rotation * Quaternion.Euler(closedEuler) * Vector3.right;
            normal.y = 0f;
            if (normal.sqrMagnitude < 0.0001f)
                return;

            normal.Normalize();
            Vector3 toApartment = bounds.center - transform.position;
            cachedInsideDirection =
                Vector3.Dot(toApartment, normal) >= 0f ? normal : -normal;
        }

        private static bool TryGetApartmentBounds(Transform apartment, out Bounds bounds)
        {
            bounds = new Bounds();
            Renderer[] renderers = apartment.GetComponentsInChildren<Renderer>(true);
            bool initialized = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                    continue;

                if (!initialized)
                {
                    bounds = renderer.bounds;
                    initialized = true;
                    continue;
                }

                bounds.Encapsulate(renderer.bounds);
            }

            return initialized;
        }

        public void SetOpen(bool value)
        {
            isOpen = value;
            if (isOpen)
                ResolveOpenPose();
            ApplyPose(false);
        }

        private void ApplyPose(bool instant)
        {
            if (pivot == null)
                return;

            if (isOpen)
                ResolveOpenPose();
            targetRotation = Quaternion.Euler(
                isOpen ? EffectiveOpenEuler : closedEuler);
            if (instant)
                pivot.localRotation = targetRotation;
        }

        private Vector3 GetHardLimitedOpenEuler()
        {
            float requestedDelta = Mathf.DeltaAngle(closedEuler.y, openEuler.y);
            float limitedDelta = Mathf.Clamp(
                requestedDelta,
                -maximumOpenAngle,
                maximumOpenAngle);
            return new Vector3(
                openEuler.x,
                closedEuler.y + limitedDelta,
                openEuler.z);
        }

        private void ResolveOpenPose()
        {
            if (openPoseResolved)
                return;

            resolvedOpenEuler = GetHardLimitedOpenEuler();
            openPoseResolved = true;
            if (!stopAtWall || pivot == null)
                return;

            BoxCollider panelCollider = FindDoorPanelCollider();
            if (panelCollider == null)
                return;

            Quaternion originalRotation = pivot.localRotation;
            float requestedDelta = Mathf.DeltaAngle(
                closedEuler.y,
                resolvedOpenEuler.y);
            float direction = Mathf.Sign(requestedDelta);
            float requestedMagnitude = Mathf.Abs(requestedDelta);
            float clearMagnitude = Mathf.Min(55f, requestedMagnitude);
            float step = Mathf.Max(1f, wallCheckStep);

            for (float magnitude = 55f;
                 magnitude <= requestedMagnitude + 0.01f;
                 magnitude += step)
            {
                float testedMagnitude = Mathf.Min(magnitude, requestedMagnitude);
                pivot.localRotation = Quaternion.Euler(
                    closedEuler.x,
                    closedEuler.y + direction * testedMagnitude,
                    closedEuler.z);
                Physics.SyncTransforms();
                if (DoorPanelTouchesObstacle(panelCollider))
                {
                    clearMagnitude = Mathf.Max(0f, testedMagnitude - step);
                    break;
                }

                clearMagnitude = testedMagnitude;
                if (Mathf.Approximately(testedMagnitude, requestedMagnitude))
                    break;
            }

            pivot.localRotation = originalRotation;
            Physics.SyncTransforms();
            resolvedOpenEuler.y = closedEuler.y + direction * clearMagnitude;
        }

        private BoxCollider FindDoorPanelCollider()
        {
            BoxCollider[] colliders =
                pivot.GetComponentsInChildren<BoxCollider>(true);
            BoxCollider best = null;
            float bestVolume = 0f;
            for (int i = 0; i < colliders.Length; i++)
            {
                BoxCollider candidate = colliders[i];
                if (candidate == null || !candidate.enabled || candidate.isTrigger)
                    continue;
                float volume = candidate.size.x *
                               candidate.size.y *
                               candidate.size.z;
                if (candidate.name == "Door_Panel")
                    volume *= 10f;
                if (volume > bestVolume)
                {
                    best = candidate;
                    bestVolume = volume;
                }
            }

            return best;
        }

        private bool DoorPanelTouchesObstacle(BoxCollider panelCollider)
        {
            Transform panel = panelCollider.transform;
            Vector3 scale = panel.lossyScale;
            Vector3 halfExtents = Vector3.Scale(
                panelCollider.size * 0.5f,
                new Vector3(
                    Mathf.Abs(scale.x),
                    Mathf.Abs(scale.y),
                    Mathf.Abs(scale.z)));
            halfExtents = Vector3.Scale(
                halfExtents,
                new Vector3(0.84f, 0.86f, 0.72f));

            int count = Physics.OverlapBoxNonAlloc(
                panel.TransformPoint(panelCollider.center),
                halfExtents,
                wallCheckBuffer,
                panel.rotation,
                ~0,
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider hit = wallCheckBuffer[i];
                wallCheckBuffer[i] = null;
                if (hit == null ||
                    hit.transform == pivot ||
                    hit.transform.IsChildOf(pivot) ||
                    hit.attachedRigidbody != null ||
                    hit.GetComponentInParent<MiniVanPlayer>() != null)
                {
                    continue;
                }

                return true;
            }

            return false;
        }
    }
}
