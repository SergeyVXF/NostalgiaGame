using UnityEngine;

namespace MiniVanGame
{
    /// <summary>
    /// Boiler furnace. Player loads carried coal (E). Temperature needle moves toward
    /// ON as coal is added. At RequiredCoal the boiler is fueled and the generator starts.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class MiniVanDamBoilerFurnace : MonoBehaviour, IMiniVanGameModeInteractable
    {
        private const float InteractionReach = 2.6f;

        public MiniVanDamObstacleController Controller;
        public Renderer FireRenderer;
        public Renderer GlowRenderer;
        public ParticleSystem FireParticles;
        public Light FireLight;
        public Color ColdColor = new Color(0.15f, 0.15f, 0.16f, 1f);
        public Color HotColor = new Color(1f, 0.45f, 0.08f, 1f);

        [Header("Temperature Gauge")]
        public Transform TempNeedle;
        public float NeedleColdAngle = 110f;
        public float NeedleHotAngle = -110f;

        public int CoalLoaded { get; private set; }
        public bool IsFueled => Controller != null && Controller.BoilerFueled;

        private void Awake()
        {
            BoxCollider box = GetComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(1.4f, 1.2f, 1.2f);
            box.center = new Vector3(0f, 0.5f, 0f);
            RefreshVisual();
        }

        public string GetPrompt(MiniVanPlayer player)
        {
            if (player == null || Vector3.Distance(player.transform.position, transform.position) > InteractionReach)
            {
                return string.Empty;
            }

            if (IsFueled)
            {
                return "Boiler at operating temperature";
            }

            int required = Controller != null ? Controller.RequiredCoal : 15;
            if (MiniVanDamCoal.GetCarriedBy(player) != null)
            {
                return "E - load coal (" + CoalLoaded + "/" + required + ")";
            }

            return "Boiler needs coal (" + CoalLoaded + "/" + required + ")";
        }

        public void Interact(MiniVanPlayer player)
        {
            if (Input.GetMouseButton(1))
            {
                return;
            }

            if (player == null || IsFueled ||
                Vector3.Distance(player.transform.position, transform.position) > InteractionReach)
            {
                return;
            }

            MiniVanDamCoal coal = MiniVanDamCoal.GetCarriedBy(player);
            if (coal == null)
            {
                return;
            }

            coal.Consume();
            CoalLoaded++;
            if (Controller != null)
            {
                Controller.NotifyCoalLoaded(CoalLoaded);
            }

            RefreshVisual();
        }

        public void PrimaryAction(MiniVanPlayer player)
        {
        }

        public float GetHeat01()
        {
            int required = Controller != null ? Mathf.Max(1, Controller.RequiredCoal) : 15;
            return Mathf.Clamp01((float)CoalLoaded / required);
        }

        private void RefreshVisual()
        {
            bool fueled = IsFueled;
            float fill = GetHeat01();

            if (TempNeedle != null)
            {
                float angle = Mathf.Lerp(NeedleColdAngle, NeedleHotAngle, fill);
                TempNeedle.localRotation = Quaternion.Euler(0f, 0f, angle);
            }

            if (FireRenderer != null)
            {
                FireRenderer.sharedMaterial = GetMaterial(fueled ? HotColor : Color.Lerp(ColdColor, HotColor, fill * 0.55f));
            }

            if (GlowRenderer != null)
            {
                GlowRenderer.sharedMaterial = GetMaterial(fueled ? HotColor : Color.Lerp(ColdColor, HotColor, fill * 0.35f));
            }

            if (FireLight != null)
            {
                FireLight.enabled = fill > 0.05f;
                FireLight.intensity = Mathf.Lerp(0.35f, 2.6f, fill);
                FireLight.color = Color.Lerp(new Color(1f, 0.55f, 0.2f), HotColor, fill);
            }

            if (FireParticles != null)
            {
                if (fueled && !FireParticles.isPlaying)
                {
                    FireParticles.Play(true);
                }
                else if (!fueled && FireParticles.isPlaying)
                {
                    FireParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
            }
        }

        private static Material GetMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            Material material = new Material(shader);
            material.color = color;
            return material;
        }
    }
}
