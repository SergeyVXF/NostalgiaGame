using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MiniVanGame
{
    /// <summary>
    /// Hover/press effect for main-menu buttons: on hover the border, icon and label
    /// become brighter and more saturated, and the fill lights up with the button accent.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MiniVanMenuButtonHoverFx : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        [Header("Parts")]
        public Image Border;
        public Image Fill;
        public Image Icon;
        public Text Label;

        [Header("Normal colors")]
        public Color BorderNormal = Color.white;
        public Color FillNormal = Color.black;
        public Color IconNormal = Color.white;
        public Color LabelNormal = Color.white;

        [Header("Hover colors")]
        public Color BorderHover = Color.white;
        public Color FillHover = Color.gray;
        public Color IconHover = Color.white;
        public Color LabelHover = Color.white;

        [Header("Behaviour")]
        [Range(0.5f, 1f)] public float PressedDarken = 0.85f;
        public float FadeDuration = 0.08f;

        private bool hovered;
        private bool pressed;
        private float blend;
        private float appliedBlend = -1f;
        private bool appliedPressed;

        /// <summary>
        /// Adds the effect to a menu button. Works with outline buttons (border Image
        /// on the root or a "Border" child, plus "Fill"/"Icon"/"Label" children),
        /// filled buttons and bare icon buttons. Hover colors derive from current colors.
        /// </summary>
        public static MiniVanMenuButtonHoverFx Attach(Button button)
        {
            if (button == null)
            {
                return null;
            }

            GameObject go = button.gameObject;
            MiniVanMenuButtonHoverFx fx = go.GetComponent<MiniVanMenuButtonHoverFx>();
            if (fx == null)
            {
                fx = go.AddComponent<MiniVanMenuButtonHoverFx>();
            }

            fx.Border = go.GetComponent<Image>();
            if (fx.Border == null)
            {
                fx.Border = FindPart<Image>(go.transform, "Border");
            }

            fx.Fill = FindPart<Image>(go.transform, "Fill");
            fx.Icon = FindPart<Image>(go.transform, "Icon");
            fx.Label = FindPart<Text>(go.transform, "Label");

            fx.BorderNormal = fx.Border != null ? fx.Border.color : Color.white;
            fx.FillNormal = fx.Fill != null ? fx.Fill.color : Color.black;
            fx.IconNormal = fx.Icon != null ? fx.Icon.color : Color.white;
            fx.LabelNormal = fx.Label != null ? fx.Label.color : Color.white;

            // Bare icon buttons (single white Image) cannot get any brighter,
            // so they warm up toward the menu accent instead.
            bool bareIcon = fx.Fill == null && fx.Icon == null && fx.Label == null;

            Color hoverAccent = HoverFor(fx.BorderNormal, bareIcon);
            fx.BorderHover = hoverAccent;
            fx.IconHover = HoverFor(fx.IconNormal, false);
            fx.LabelHover = HoverFor(fx.LabelNormal, false);
            Color fillHover = Color.Lerp(fx.FillNormal, hoverAccent, 0.55f);
            fillHover.a = fx.FillNormal.a;
            fx.FillHover = fillHover;

            // This component fully owns the visuals; the Button tint would double up.
            button.transition = Selectable.Transition.None;
            return fx;
        }

        /// <summary>
        /// Updates the border base color (e.g. when map selection changes)
        /// and recomputes its hover color.
        /// </summary>
        public void SetBorderNormal(Color color)
        {
            BorderNormal = color;
            BorderHover = HoverFor(color, false);
            appliedBlend = -1f;
            Apply();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            hovered = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hovered = false;
            pressed = false;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            pressed = true;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            pressed = false;
        }

        private void OnEnable()
        {
            hovered = false;
            pressed = false;
            blend = 0f;
            appliedBlend = -1f;
            Apply();
        }

        private void Update()
        {
            float target = hovered ? 1f : 0f;
            blend = FadeDuration <= 0f
                ? target
                : Mathf.MoveTowards(blend, target, Time.unscaledDeltaTime / FadeDuration);
            Apply();
        }

        private void Apply()
        {
            if (Mathf.Approximately(appliedBlend, blend) && appliedPressed == pressed)
            {
                return;
            }

            appliedBlend = blend;
            appliedPressed = pressed;
            float k = pressed ? PressedDarken : 1f;
            SetColor(Border, Color.Lerp(BorderNormal, BorderHover, blend), k);
            SetColor(Fill, Color.Lerp(FillNormal, FillHover, blend), k);
            SetColor(Icon, Color.Lerp(IconNormal, IconHover, blend), k);
            SetColor(Label, Color.Lerp(LabelNormal, LabelHover, blend), k);
        }

        private static void SetColor(Graphic graphic, Color color, float darken)
        {
            if (graphic == null)
            {
                return;
            }

            color.r *= darken;
            color.g *= darken;
            color.b *= darken;
            graphic.color = color;
        }

        private static readonly Color WarmAccent = new Color(1f, 0.62f, 0.22f, 1f);

        private static Color HoverFor(Color color, bool warmTintWhenMaxed)
        {
            Color.RGBToHSV(color, out float h, out float s, out float v);
            if (warmTintWhenMaxed && s <= 0.08f && v >= 0.85f)
            {
                Color tinted = Color.Lerp(color, WarmAccent, 0.55f);
                tinted.a = color.a;
                return tinted;
            }

            s = Mathf.Clamp01(s * 1.3f);
            v = Mathf.Clamp01(Mathf.Max(v * 2f, v + 0.35f));
            Color result = Color.HSVToRGB(h, s, v);
            result.a = Mathf.Clamp01(color.a * 1.5f);
            return result;
        }

        private static T FindPart<T>(Transform root, string childName) where T : Component
        {
            Transform child = root.Find(childName);
            return child != null ? child.GetComponent<T>() : null;
        }
    }
}
