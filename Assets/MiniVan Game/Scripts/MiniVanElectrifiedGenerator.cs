using UnityEngine;

namespace MiniVanGame
{
    [DisallowMultipleComponent]
    public sealed class MiniVanElectrifiedGenerator : MonoBehaviour, IMiniVanGameModeInteractable
    {
        [Min(1)] public int HitPoints = 5;
        public float VehicleBreakSpeedKph = 40f;
        public MiniVanElectrifiedWaterZone[] PoweredWaterZones;
        public ParticleSystem[] PoweredEffects;
        public ParticleSystem HitSparkEffect;
        [Min(0)] public int HitSparkBurstMin = 3;
        [Min(0)] public int HitSparkBurstMax = 4;
        public Renderer[] PoweredRenderers;
        public Material PoweredMaterial;
        public Material BrokenMaterial;
        public GameObject IntactVisual;
        public GameObject BrokenVisual;
        public Collider SolidCollider;
        public float HitFlashSeconds = 0.15f;
        public Color HitFlashColor = new Color(1f, 0.06f, 0.03f, 1f);
        public bool DebugGenerator = true;

        private int localHealth;
        private bool localBroken;
        private Renderer[] visualRenderers;
        private MaterialPropertyBlock hitFlashBlock;
        private float hitFlashUntil;
        private bool hitFlashWasApplied;

        public bool IsBroken => localBroken;
        public bool IsPowered => !IsBroken;

        private void Awake()
        {
            if (SolidCollider == null)
            {
                SolidCollider = GetComponent<Collider>();
            }

            localHealth = Mathf.Max(1, HitPoints);
            CacheVisualRenderers();
            RefreshVisuals();
        }

        private void Update()
        {
            UpdateHitFlashVisual();
        }

        public string GetPrompt(MiniVanPlayer player)
        {
            if (IsBroken)
            {
                return string.Empty;
            }

            return "Break generator";
        }

        public void Interact(MiniVanPlayer player)
        {
        }

        public void PrimaryAction(MiniVanPlayer player)
        {
        }

        public bool ServerApplyBatHit(int damage)
        {
            if (IsBroken)
            {
                return false;
            }

            ApplyDamage(Mathf.Max(1, damage), "bat");
            return true;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (collision == null || IsBroken)
            {
                return;
            }

            MiniVanVehicle vehicle = collision.collider != null
                ? collision.collider.GetComponentInParent<MiniVanVehicle>()
                : null;
            if (vehicle == null)
            {
                return;
            }

            float speedKph = collision.relativeVelocity.magnitude * 3.6f;
            if (speedKph < Mathf.Max(0f, VehicleBreakSpeedKph))
            {
                return;
            }

            ApplyDamage(Mathf.Max(1, HitPoints), "vehicle " + speedKph.ToString("0") + " kph");
        }

        private void ApplyDamage(int damage, string reason)
        {
            EmitHitSparks();
            TriggerHitFlash();
            localHealth = Mathf.Max(0, localHealth - Mathf.Max(1, damage));
            localBroken = localHealth <= 0;

            if (DebugGenerator)
            {
                Debug.Log("[MiniVanElectric] generator hit reason=" + reason + " hp=" + localHealth);
            }

            RefreshVisuals();
        }

        private void TriggerHitFlash()
        {
            if (HitFlashSeconds <= 0f)
            {
                return;
            }

            hitFlashUntil = Time.time + HitFlashSeconds;
            hitFlashWasApplied = false;
            UpdateHitFlashVisual();
        }

        private void EmitHitSparks()
        {
            if (HitSparkEffect == null)
            {
                return;
            }

            int min = Mathf.Max(0, HitSparkBurstMin);
            int max = Mathf.Max(min, HitSparkBurstMax);
            int count = Random.Range(min, max + 1);
            if (count <= 0)
            {
                return;
            }

            HitSparkEffect.Emit(count);
        }

        private void CacheVisualRenderers()
        {
            visualRenderers = GetComponentsInChildren<Renderer>(true);
            if (hitFlashBlock == null)
            {
                hitFlashBlock = new MaterialPropertyBlock();
            }
        }

        private void UpdateHitFlashVisual()
        {
            if (visualRenderers == null || visualRenderers.Length == 0)
            {
                CacheVisualRenderers();
            }

            bool flashing = Time.time < hitFlashUntil;
            if (flashing == hitFlashWasApplied)
            {
                return;
            }

            hitFlashWasApplied = flashing;
            for (int i = 0; i < visualRenderers.Length; i++)
            {
                Renderer renderer = visualRenderers[i];
                if (renderer == null || renderer is ParticleSystemRenderer)
                {
                    continue;
                }

                if (!flashing)
                {
                    renderer.SetPropertyBlock(null);
                    continue;
                }

                renderer.GetPropertyBlock(hitFlashBlock);
                hitFlashBlock.SetColor("_BaseColor", HitFlashColor);
                hitFlashBlock.SetColor("_Color", HitFlashColor);
                renderer.SetPropertyBlock(hitFlashBlock);
            }
        }

        private void RefreshVisuals()
        {
            bool powered = IsPowered;
            if (IntactVisual != null)
            {
                IntactVisual.SetActive(powered || BrokenVisual == null);
            }

            if (BrokenVisual != null)
            {
                BrokenVisual.SetActive(!powered);
            }

            if (SolidCollider != null)
            {
                SolidCollider.enabled = true;
                SolidCollider.isTrigger = false;
            }

            if (PoweredRenderers != null)
            {
                Material material = powered ? PoweredMaterial : BrokenMaterial;
                if (material != null)
                {
                    for (int i = 0; i < PoweredRenderers.Length; i++)
                    {
                        if (PoweredRenderers[i] != null)
                        {
                            PoweredRenderers[i].sharedMaterial = material;
                        }
                    }
                }
            }

            if (PoweredEffects != null)
            {
                for (int i = 0; i < PoweredEffects.Length; i++)
                {
                    ParticleSystem effect = PoweredEffects[i];
                    if (effect == null || effect == HitSparkEffect)
                    {
                        continue;
                    }

                    if (powered && !effect.isPlaying)
                    {
                        effect.Play(true);
                    }
                    else if (!powered && effect.isPlaying)
                    {
                        effect.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                    }
                }
            }

            if (PoweredWaterZones != null)
            {
                for (int i = 0; i < PoweredWaterZones.Length; i++)
                {
                    if (PoweredWaterZones[i] != null)
                    {
                        PoweredWaterZones[i].SetPowered(powered);
                    }
                }
            }
        }
    }
}
