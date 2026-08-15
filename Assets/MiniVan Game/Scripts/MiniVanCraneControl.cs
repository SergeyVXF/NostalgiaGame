using UnityEngine;

namespace MiniVanGame
{
    /// <summary>
    /// A cab control on the tower crane.
    ///
    /// Levers are spring-return: aim at one, hold LMB and push the mouse forward
    /// or pull it back. The handle follows, the crane axis moves that way for as
    /// long as you hold it, and the moment LMB is released the handle snaps back
    /// to centre and the axis stops where it is.
    ///
    /// The magnet button stays a plain E toggle and glows red while on.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MiniVanCraneControl : MonoBehaviour, IMiniVanGameModeInteractable
    {
        public enum ControlKind
        {
            TrolleyLever,
            HoistLever,
            SlewLever,
            MagnetButton
        }

        [SerializeField] private ControlKind kind = ControlKind.TrolleyLever;
        [SerializeField] private MiniVanTowerCrane crane;
        [SerializeField] private float throwAngle = 24f;
        [SerializeField] private float pressDepth = 0.02f;
        [SerializeField, Min(0.1f)] private float dragSensitivity = 0.14f;
        [SerializeField, Min(1f)] private float animationSpeed = 16f;
        [SerializeField, Min(0.5f)] private float interactRadius = 2.6f;
        [SerializeField] private Material onMaterial;

        private Quaternion restRotation;
        private Vector3 restPosition;
        private Material restMaterial;
        private Renderer meshRenderer;
        private Material outlineMaterial;
        private bool isHighlighted;
        private bool captured;

        private bool held;
        private float axis;          // -1..1, 0 when released
        private bool lastMagnetOn;

        public void Configure(ControlKind controlKind, MiniVanTowerCrane owner, Material glow = null)
        {
            kind = controlKind;
            crane = owner;
            if (glow != null) onMaterial = glow;
        }

        private void Awake()
        {
            Capture();
        }

        private void Capture()
        {
            if (captured)
                return;
            restRotation = transform.localRotation;
            restPosition = transform.localPosition;
            meshRenderer = GetComponent<Renderer>();
            if (meshRenderer != null)
                restMaterial = meshRenderer.sharedMaterial;
            captured = true;
        }

        private void OnDisable()
        {
            Release();
        }

        private void Update()
        {
            Capture();
            float blend = 1f - Mathf.Exp(-animationSpeed * Time.deltaTime);

            if (kind == ControlKind.MagnetButton)
            {
                bool on = crane != null && crane.MagnetOn;
                Vector3 target = restPosition + (on ? Vector3.down * pressDepth : Vector3.zero);
                transform.localPosition = Vector3.Lerp(transform.localPosition, target, blend);
                if (on != lastMagnetOn)
                {
                    lastMagnetOn = on;
                    RefreshMaterials();
                }
                return;
            }

            if (held)
            {
                if (!Input.GetMouseButton(0))
                {
                    Release();
                }
                else
                {
                    axis = Mathf.Clamp(axis + Input.GetAxis("Mouse Y") * dragSensitivity, -1f, 1f);
                    Drive(axis);
                }
            }

            // handle follows the lever position; snaps straight back once released
            Quaternion target2 = Quaternion.AngleAxis(axis * throwAngle, Vector3.right) * restRotation;
            transform.localRotation = held
                ? Quaternion.Slerp(transform.localRotation, target2, blend)
                : restRotation;
        }

        private void Drive(float value)
        {
            if (crane == null)
                return;
            switch (kind)
            {
                case ControlKind.TrolleyLever: crane.SetTrolleyInput(value); break;
                case ControlKind.HoistLever: crane.SetHoistInput(value); break;
                case ControlKind.SlewLever: crane.SetSlewInput(value); break;
            }
        }

        private void Release()
        {
            held = false;
            axis = 0f;
            Drive(0f);      // the axis stops exactly where it was
        }

        /// <summary>
        /// Rebuilds the renderer's material list: the base slot carries the glow
        /// state, the outline is appended on top. Doing both through one place
        /// keeps a highlighted button from losing its red material and back.
        /// </summary>
        private void RefreshMaterials()
        {
            if (meshRenderer == null)
                return;

            Material basis = restMaterial;
            if (kind == ControlKind.MagnetButton && crane != null && crane.MagnetOn && onMaterial != null)
                basis = onMaterial;

            if (!isHighlighted)
            {
                meshRenderer.sharedMaterials = new[] { basis };
                return;
            }

            meshRenderer.sharedMaterials = new[] { basis, GetOutlineMaterial() };
        }

        /// <summary>Same thin white outline the gate lever and the doors use.</summary>
        public void SetHighlighted(bool highlighted)
        {
            if (isHighlighted == highlighted)
                return;
            Capture();
            isHighlighted = highlighted;
            RefreshMaterials();
        }

        private Material GetOutlineMaterial()
        {
            if (outlineMaterial != null)
                return outlineMaterial;

            Material source = Resources.Load<Material>("Panelka/ThinWhiteOutline");
            if (source != null)
            {
                outlineMaterial = new Material(source) { name = "Crane Control Outline" };
            }
            else
            {
                Shader shader = Shader.Find("MiniVanGame/ThinWhiteOutline");
                if (shader == null)
                    shader = Shader.Find("Universal Render Pipeline/Unlit");
                outlineMaterial = new Material(shader != null ? shader : Shader.Find("Standard"))
                {
                    name = "Crane Control Outline"
                };
            }

            MiniVanSnesOutline.ApplyOutlineSettings(outlineMaterial, Color.white);
            return outlineMaterial;
        }

        private void OnDestroy()
        {
            if (outlineMaterial != null)
            {
                Destroy(outlineMaterial);
                outlineMaterial = null;
            }
        }

        public string GetPrompt(MiniVanPlayer player)
        {
            if (player == null || crane == null)
                return string.Empty;
            if (Vector3.Distance(player.transform.position, transform.position) > interactRadius)
                return string.Empty;

            switch (kind)
            {
                case ControlKind.TrolleyLever:
                    return "Hold LMB + move mouse - trolley out / in";
                case ControlKind.HoistLever:
                    return "Hold LMB + move mouse - magnet down / up";
                case ControlKind.SlewLever:
                    return "Hold LMB + move mouse - slew crane";
                default:
                    return crane.MagnetOn ? "E - magnet off" : "E - magnet on";
            }
        }

        public void Interact(MiniVanPlayer player)
        {
            if (crane == null || player == null)
                return;
            if (kind != ControlKind.MagnetButton)
                return;
            if (Vector3.Distance(player.transform.position, transform.position) > interactRadius)
                return;

            crane.ToggleMagnet();
        }

        /// <summary>LMB down while aiming at this control - grab the lever.</summary>
        public void PrimaryAction(MiniVanPlayer player)
        {
            if (crane == null || player == null || kind == ControlKind.MagnetButton)
                return;
            if (Vector3.Distance(player.transform.position, transform.position) > interactRadius)
                return;

            held = true;
            axis = 0f;
        }
    }
}
