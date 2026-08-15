using UnityEngine;

namespace MiniVanGame
{
    /// <summary>
    /// Controls locator CRT overlays: center blip blink + height TextMesh labels.
    /// Background radar art comes from Resources texture on the screen quad.
    /// </summary>
    public sealed class MiniVanAntonLocatorScreen
    {
        public const float ActiveRangeMeters = 300f;
        public const float HeightLevelMeters = 2f;
        /// <summary>Blink period when Anton is close and off to the side.</summary>
        public const float BlinkPeriodNear = 0.55f;

        /// <summary>Blink period at maximum range while looking away from Anton.</summary>
        public const float BlinkPeriodFar = 6f;

        /// <summary>Looking straight at Anton blinks this many times faster.</summary>
        private const float HotSpeedUp = 3.2f;

        private const float BlipScaleCold = 0.05f;
        private const float BlipScaleHot = 0.12f;
        private const float DutyCold = 0.18f;
        private const float DutyHot = 0.55f;
        private const float ColdHotness = 0.18f;

        private static readonly Color WordOn = new Color(0.35f, 1f, 0.45f, 1f);
        private static readonly Color WordOff = new Color(0.12f, 0.32f, 0.14f, 1f);
        private static readonly Color BlipGreen = new Color(0.45f, 1f, 0.5f, 1f);
        private static readonly Color BlipRed = new Color(0.95f, 0.18f, 0.12f, 1f);

        private readonly Renderer blipRenderer;
        private readonly Material blipMaterial;
        private readonly TextMesh labelVyshe;
        private readonly TextMesh labelUroven;
        private readonly TextMesh labelNizhe;
        private float blinkPhase;
        private float blinkPeriod = BlinkPeriodFar;

        public MiniVanAntonLocatorScreen(Transform visualRoot)
        {
            blipRenderer = MiniVanAntonLocatorVisual.GetBlipRenderer(visualRoot);
            if (blipRenderer != null)
            {
                blipMaterial = blipRenderer.material;
            }

            labelVyshe = MiniVanAntonLocatorVisual.GetLabel(visualRoot, "LabelVyshe");
            labelUroven = MiniVanAntonLocatorVisual.GetLabel(visualRoot, "LabelUroven");
            labelNizhe = MiniVanAntonLocatorVisual.GetLabel(visualRoot, "LabelNizhe");
            ApplyInactive();
        }

        public void Dispose()
        {
            if (blipMaterial != null)
            {
                Object.Destroy(blipMaterial);
            }
        }

        public void ApplyInactive()
        {
            SetBlip(BlipRed, visible: true, BlipScaleHot);
            SetWords(heightMode: 0, active: false);
        }

        /// <param name="heightMode">1=выше, 0=уровень, -1=ниже</param>
        public void ApplyActive(float xzDistance, float hotness, int heightMode)
        {
            // ComputeHotness never returns 0, so rescale it to use the whole cold-to-hot range.
            float hot = Mathf.Clamp01(Mathf.InverseLerp(ColdHotness, 1f, hotness));

            float t = Mathf.Clamp01(xzDistance / ActiveRangeMeters);
            float coldPeriod = Mathf.Lerp(BlinkPeriodNear, BlinkPeriodFar, t * t);
            float targetPeriod = coldPeriod / Mathf.Lerp(1f, HotSpeedUp, hot);

            // Advance a stored phase instead of sampling absolute time. Sampling
            // Time.unscaledTime % period made the phase jump whenever the distance —
            // and therefore the period — changed, which read as faster blinking while moving.
            float smoothing = 1f - Mathf.Exp(-8f * Time.unscaledDeltaTime);
            blinkPeriod = Mathf.Lerp(blinkPeriod, targetPeriod, smoothing);
            blinkPhase += Time.unscaledDeltaTime / Mathf.Max(0.05f, blinkPeriod);
            blinkPhase -= Mathf.Floor(blinkPhase);

            float duty = Mathf.Lerp(DutyCold, DutyHot, hot);
            SetBlip(BlipGreen, blinkPhase < duty, Mathf.Lerp(BlipScaleCold, BlipScaleHot, hot));
            SetWords(heightMode, active: true);
        }

        /// <summary>
        /// Hot cone half-angle vs horizontal distance: closer → narrower.
        /// 0m≈8°, 30m≈15°, 300m≈90°.
        /// </summary>
        public static float GetHotHalfAngleDegrees(float xzDistance)
        {
            float d = Mathf.Max(0f, xzDistance);
            if (d <= 30f)
            {
                float t = d / 30f;
                return Mathf.Lerp(8f, 15f, t);
            }

            float tFar = Mathf.InverseLerp(30f, ActiveRangeMeters, Mathf.Min(d, ActiveRangeMeters));
            tFar = tFar * tFar;
            return Mathf.Lerp(15f, 90f, tFar);
        }

        public static float ComputeHotness(float angleDegrees, float hotHalfAngle)
        {
            float half = Mathf.Max(1f, hotHalfAngle);
            if (angleDegrees <= half)
            {
                return Mathf.Lerp(1f, 0.82f, angleDegrees / half);
            }

            float cold = 1f - Mathf.Clamp01((angleDegrees - half) / Mathf.Max(40f, 180f - half));
            return Mathf.Lerp(0.18f, 0.45f, cold);
        }

        private void SetBlip(Color color, bool visible, float scale)
        {
            if (blipRenderer == null)
            {
                return;
            }

            // Blink by toggling the renderer only: the material is opaque, so fading the
            // colour would darken the dot to black instead of blending it into the screen.
            if (blipMaterial != null)
            {
                blipMaterial.color = color;
                if (blipMaterial.HasProperty("_BaseColor"))
                {
                    blipMaterial.SetColor("_BaseColor", color);
                }
            }

            blipRenderer.transform.localScale = new Vector3(scale, scale, 1f);
            blipRenderer.enabled = visible;
        }

        private void SetWords(int heightMode, bool active)
        {
            SetWord(labelVyshe, active && heightMode == 1);
            SetWord(labelUroven, active && heightMode == 0);
            SetWord(labelNizhe, active && heightMode == -1);
        }

        private static void SetWord(TextMesh label, bool on)
        {
            if (label == null)
            {
                return;
            }

            label.color = on ? WordOn : WordOff;
        }
    }
}
