using UnityEngine;

namespace MiniVanGame
{
    /// <summary>
    /// Pump generator. Starts automatically when the boiler reaches operating heat.
    /// Status light turns green while running. Levers then engage the pump.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class MiniVanDamPumpGenerator : MonoBehaviour, IMiniVanGameModeInteractable
    {
        private const float InteractionReach = 2.6f;

        public MiniVanDamObstacleController Controller;
        public Renderer ButtonRenderer;
        public Renderer StatusLightRenderer;
        public Light StatusLight;
        public ParticleSystem RunParticles;
        public AudioSource RunAudio;
        public Color OffColor = new Color(0.45f, 0.08f, 0.06f, 1f);
        public Color OnColor = new Color(0.12f, 0.85f, 0.22f, 1f);

        public bool IsOn => Controller != null && Controller.GeneratorOn;

        private Material offMaterial;
        private Material onMaterial;

        private void Awake()
        {
            BoxCollider box = GetComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(1.4f, 1.2f, 1.2f);
            box.center = new Vector3(0f, 0.5f, 0f);
            offMaterial = CreateMaterial(OffColor, 0.15f);
            onMaterial = CreateMaterial(OnColor, 2.8f);
            RefreshVisual();
        }

        private void LateUpdate()
        {
            RefreshVisual();
        }

        public string GetPrompt(MiniVanPlayer player)
        {
            if (player == null || Vector3.Distance(player.transform.position, transform.position) > InteractionReach)
            {
                return string.Empty;
            }

            if (IsOn)
            {
                return Controller != null && Controller.LeversPulled
                    ? "Generator running - pump engaged"
                    : "Generator ON - pull both levers";
            }

            if (Controller == null)
            {
                return string.Empty;
            }

            if (!Controller.BoilerFueled)
            {
                return "Waiting for boiler heat";
            }

            return "Generator ready";
        }

        public void Interact(MiniVanPlayer player)
        {
            // Auto-starts from boiler heat; keep E as a fallback if somehow not started.
            if (Input.GetMouseButton(1) || player == null || Controller == null || IsOn)
            {
                return;
            }

            if (Vector3.Distance(player.transform.position, transform.position) > InteractionReach)
            {
                return;
            }

            if (Controller.CanStartGenerator())
            {
                Controller.NotifyGeneratorStarted();
                RefreshVisual();
            }
        }

        public void PrimaryAction(MiniVanPlayer player)
        {
        }

        public void RefreshVisual()
        {
            bool on = IsOn;
            Material material = on ? onMaterial : offMaterial;
            if (material == null)
            {
                return;
            }

            if (ButtonRenderer != null)
            {
                ButtonRenderer.sharedMaterial = material;
            }

            if (StatusLightRenderer != null)
            {
                StatusLightRenderer.sharedMaterial = material;
            }

            if (StatusLight != null)
            {
                StatusLight.enabled = true;
                StatusLight.color = on ? OnColor : OffColor;
                StatusLight.intensity = on ? 3.2f : 0.35f;
            }

            if (RunParticles != null)
            {
                if (on && !RunParticles.isPlaying)
                {
                    RunParticles.Play(true);
                }
                else if (!on && RunParticles.isPlaying)
                {
                    RunParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
            }

            if (RunAudio != null)
            {
                if (on && !RunAudio.isPlaying)
                {
                    RunAudio.Play();
                }
                else if (!on && RunAudio.isPlaying)
                {
                    RunAudio.Stop();
                }
            }
        }

        private static Material CreateMaterial(Color color, float emission)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            Material material = new Material(shader);
            material.color = color;
            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * emission);
            }

            return material;
        }
    }
}
