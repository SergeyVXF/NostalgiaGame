using UnityEngine;

namespace MiniVanGame
{
    /// <summary>
    /// Status board on the control tower: furnace / dam / pump indicators.
    /// Active label glows (red or green); inactive label stays dark gray.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MiniVanDamStatusIndication : MonoBehaviour
    {
        public MiniVanDamObstacleController Controller;

        [Header("Furnace")]
        public TextMesh FurnaceOffLabel;
        public TextMesh FurnaceOnLabel;

        [Header("Dam")]
        public TextMesh DamOpenLabel;
        public TextMesh DamClosedLabel;

        [Header("Pump")]
        public TextMesh PumpOffLabel;
        public TextMesh PumpOnLabel;

        public Color ActiveRed = new Color(1f, 0.18f, 0.12f, 1f);
        public Color ActiveGreen = new Color(0.2f, 1f, 0.28f, 1f);
        public Color InactiveGray = new Color(0.22f, 0.22f, 0.22f, 1f);

        private static Material sharedTextMaterial;
        private bool materialsReady;

        private void Awake()
        {
            EnsureTextMaterials();
        }

        private void LateUpdate()
        {
            EnsureTextMaterials();
            Refresh();
        }

        public void Refresh()
        {
            bool furnaceOn = Controller != null && Controller.BoilerFueled;
            bool damClosed = Controller != null && Controller.DamClosed;
            bool pumpOn = Controller != null && Controller.PumpRunning;

            ApplyPair(FurnaceOffLabel, FurnaceOnLabel, furnaceOn);
            // Dam: "открыта" is the off/bad state (red when open), "закрыта" is good (green when closed).
            ApplyPair(DamOpenLabel, DamClosedLabel, damClosed);
            ApplyPair(PumpOffLabel, PumpOnLabel, pumpOn);
        }

        private void ApplyPair(TextMesh offLabel, TextMesh onLabel, bool isOn)
        {
            if (offLabel != null)
            {
                offLabel.color = isOn ? InactiveGray : ActiveRed;
            }

            if (onLabel != null)
            {
                onLabel.color = isOn ? ActiveGreen : InactiveGray;
            }
        }

        private void EnsureTextMaterials()
        {
            if (materialsReady)
            {
                return;
            }

            TextMesh[] labels =
            {
                FurnaceOffLabel, FurnaceOnLabel,
                DamOpenLabel, DamClosedLabel,
                PumpOffLabel, PumpOnLabel
            };

            // Titles / separators live as siblings — also fix any TextMesh under the desk.
            TextMesh[] all = GetComponentsInChildren<TextMesh>(true);
            if (all != null && all.Length > 0)
            {
                labels = all;
            }

            Material textMaterial = GetOrCreateTextMaterial();
            for (int i = 0; i < labels.Length; i++)
            {
                TextMesh label = labels[i];
                if (label == null)
                {
                    continue;
                }

                MeshRenderer renderer = label.GetComponent<MeshRenderer>();
                if (renderer == null)
                {
                    continue;
                }

                Texture fontTexture = renderer.sharedMaterial != null
                    ? renderer.sharedMaterial.mainTexture
                    : null;
                Material instance = new Material(textMaterial);
                if (fontTexture != null)
                {
                    instance.mainTexture = fontTexture;
                }

                renderer.sharedMaterial = instance;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            materialsReady = true;
        }

        private static Material GetOrCreateTextMaterial()
        {
            if (sharedTextMaterial != null)
            {
                return sharedTextMaterial;
            }

            Shader shader = Shader.Find("MiniVan/DamStatusText");
            if (shader == null)
            {
                shader = Shader.Find("GUI/Text Shader");
            }

            sharedTextMaterial = new Material(shader);
            sharedTextMaterial.name = "DamStatusText_Depth";
            sharedTextMaterial.color = Color.white;
            return sharedTextMaterial;
        }
    }
}
