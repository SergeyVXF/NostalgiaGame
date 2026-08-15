using UnityEngine;

namespace MiniVanGame
{
    /// <summary>
    /// A door, hood or boot lid on a static scrap car. The mesh already has its
    /// origin on the hinge, so opening is a plain rotation of this transform.
    ///
    /// The hinge axis and the swing direction are baked in by the art setup
    /// (see AutoServiceSetup): a door turns about world up and swings away from
    /// the car body, a lid turns about the horizontal edge it is hinged on and
    /// lifts. That keeps every panel correct no matter which way the wreck is
    /// parked.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MiniVanScrapCarPanel : MonoBehaviour, IMiniVanGameModeInteractable
    {
        public enum PanelKind
        {
            Door,
            Hood,
            Trunk
        }

        [SerializeField] private PanelKind kind = PanelKind.Door;
        [SerializeField] private Vector3 hingeAxis = Vector3.up;
        [SerializeField] private float openAngle = 65f;
        [SerializeField, Min(1f)] private float animationSpeed = 8f;
        [SerializeField, Min(0.5f)] private float interactRadius = 3.0f;
        [SerializeField] private bool startsOpen;

        private Quaternion closedRotation;
        private Quaternion openRotation;
        private bool isOpen;
        private bool poseReady;

        public bool IsOpen => isOpen;

        public void Configure(PanelKind panelKind, Vector3 axis, float angle)
        {
            kind = panelKind;
            hingeAxis = axis.sqrMagnitude > 0.0001f ? axis.normalized : Vector3.up;
            openAngle = angle;
            CapturePose();
        }

        private void Awake()
        {
            CapturePose();
            isOpen = startsOpen;
            transform.localRotation = isOpen ? openRotation : closedRotation;
        }

        private void CapturePose()
        {
            closedRotation = transform.localRotation;
            Vector3 axis = hingeAxis.sqrMagnitude > 0.0001f ? hingeAxis.normalized : Vector3.up;
            openRotation = Quaternion.AngleAxis(openAngle, axis) * closedRotation;
            poseReady = true;
        }

        private void Update()
        {
            if (!poseReady)
                CapturePose();

            Quaternion target = isOpen ? openRotation : closedRotation;
            float blend = 1f - Mathf.Exp(-animationSpeed * Time.deltaTime);
            transform.localRotation = Quaternion.Slerp(transform.localRotation, target, blend);
            if (Quaternion.Angle(transform.localRotation, target) < 0.2f)
                transform.localRotation = target;
        }

        public string GetPrompt(MiniVanPlayer player)
        {
            if (player == null)
                return string.Empty;
            if (Vector3.Distance(player.transform.position, transform.position) > interactRadius)
                return string.Empty;

            // wording matches the rest of the game's prompts (MiniVanVehicleHood)
            switch (kind)
            {
                case PanelKind.Hood:
                    return isOpen ? "E - close hood" : "E - open hood";
                case PanelKind.Trunk:
                    return isOpen ? "E - close trunk" : "E - open trunk";
                default:
                    return isOpen ? "E - close door" : "E - open door";
            }
        }

        public void Interact(MiniVanPlayer player)
        {
            if (player == null)
                return;
            if (Vector3.Distance(player.transform.position, transform.position) > interactRadius)
                return;

            isOpen = !isOpen;
        }

        public void PrimaryAction(MiniVanPlayer player)
        {
        }
    }
}
