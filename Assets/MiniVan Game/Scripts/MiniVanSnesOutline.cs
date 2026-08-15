using UnityEngine;

namespace MiniVanGame
{
    /// <summary>
    /// OmniShade-style inverted-hull silhouette outline (stencil hide-interior).
    /// </summary>
    public sealed class MiniVanSnesOutline : MonoBehaviour
    {
        public const float ForcedOutlineWidth = 0f;
        public const float ForcedOutlineZPos = -0.1f;

        [SerializeField] private Color outlineColor = Color.white;
        [SerializeField] private bool excludeScreenRenderers = true;

        private Renderer[] highlightRenderers;
        private Material[][] originalMaterials;
        private Material outlineMaterial;
        private bool isHighlighted;

        private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
        private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");
        private static readonly int OutlineWidthIndependentId = Shader.PropertyToID("_OutlineWidthIndependent");
        private static readonly int OutlineZPosId = Shader.PropertyToID("_OutlineZPos");

        public Color OutlineColor
        {
            get => outlineColor;
            set
            {
                outlineColor = value;
                ApplyMaterialProperties();
            }
        }

        private void Awake()
        {
            CacheRenderers();
            ApplyMaterialProperties();
        }

        private void OnDisable()
        {
            SetHighlighted(false);
        }

        private void OnValidate()
        {
            ApplyMaterialProperties();
        }

        public void SetHighlighted(bool highlighted)
        {
            if (isHighlighted == highlighted)
            {
                return;
            }

            if (highlightRenderers == null || highlightRenderers.Length == 0)
            {
                CacheRenderers();
            }

            isHighlighted = highlighted;
            if (highlighted)
            {
                Material outline = GetOutlineMaterial();
                ApplyMaterialProperties();
                for (int i = 0; i < highlightRenderers.Length; i++)
                {
                    Renderer renderer = highlightRenderers[i];
                    if (renderer == null)
                    {
                        continue;
                    }

                    Material[] source = renderer.sharedMaterials;
                    Material[] materials = new Material[source.Length + 1];
                    for (int j = 0; j < source.Length; j++)
                    {
                        materials[j] = source[j];
                    }

                    materials[materials.Length - 1] = outline;
                    renderer.sharedMaterials = materials;
                }
            }
            else if (originalMaterials != null)
            {
                for (int i = 0; i < highlightRenderers.Length; i++)
                {
                    if (highlightRenderers[i] != null && i < originalMaterials.Length)
                    {
                        highlightRenderers[i].sharedMaterials = originalMaterials[i];
                    }
                }
            }
        }

        private void CacheRenderers()
        {
            Renderer[] all = GetComponentsInChildren<Renderer>(true);
            int count = 0;
            for (int i = 0; i < all.Length; i++)
            {
                if (ShouldHighlight(all[i]))
                {
                    count++;
                }
            }

            highlightRenderers = new Renderer[count];
            int write = 0;
            for (int i = 0; i < all.Length; i++)
            {
                if (ShouldHighlight(all[i]))
                {
                    highlightRenderers[write++] = all[i];
                }
            }

            originalMaterials = new Material[highlightRenderers.Length][];
            for (int i = 0; i < highlightRenderers.Length; i++)
            {
                originalMaterials[i] = highlightRenderers[i] != null
                    ? highlightRenderers[i].sharedMaterials
                    : System.Array.Empty<Material>();
            }
        }

        private bool ShouldHighlight(Renderer renderer)
        {
            if (renderer == null)
            {
                return false;
            }

            if (!excludeScreenRenderers)
            {
                return true;
            }

            if (renderer.GetComponent<SK.Libretro.Unity.LibretroInstance>() != null)
            {
                return false;
            }

            string n = renderer.gameObject.name;
            return n.IndexOf("Screen", System.StringComparison.OrdinalIgnoreCase) < 0;
        }

        private Material GetOutlineMaterial()
        {
            if (outlineMaterial != null)
            {
                ApplyMaterialProperties();
                return outlineMaterial;
            }

            Shader shader = Shader.Find("MiniVanGame/ThinWhiteOutline");
            Material shared = Resources.Load<Material>("Panelka/ThinWhiteOutline");
            if (shared != null && shader != null && shared.shader == shader)
            {
                outlineMaterial = new Material(shared)
                {
                    name = "Snes Outline (Instance)"
                };
            }
            else if (shader != null)
            {
                outlineMaterial = new Material(shader)
                {
                    name = "Snes Outline (Instance)"
                };
            }
            else
            {
                outlineMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"))
                {
                    name = "Snes Outline Fallback",
                    color = Color.white
                };
            }

            ApplyMaterialProperties();
            return outlineMaterial;
        }

        private void ApplyMaterialProperties()
        {
            if (outlineMaterial == null)
            {
                return;
            }

            ApplyOutlineSettings(outlineMaterial, outlineColor);
        }

        public static void ApplyOutlineSettings(Material material, Color? color = null)
        {
            if (material == null)
            {
                return;
            }

            if (color.HasValue && material.HasProperty(OutlineColorId))
            {
                material.SetColor(OutlineColorId, color.Value);
            }

            if (material.HasProperty(OutlineWidthId))
            {
                material.SetFloat(OutlineWidthId, ForcedOutlineWidth);
            }

            if (material.HasProperty(OutlineWidthIndependentId))
            {
                material.SetFloat(OutlineWidthIndependentId, 0f);
            }

            if (material.HasProperty(OutlineZPosId))
            {
                material.SetFloat(OutlineZPosId, ForcedOutlineZPos);
            }
        }
    }
}
