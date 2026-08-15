using UnityEngine;

namespace MiniVanGame
{
    [DisallowMultipleComponent]
    public sealed class MiniVanGameModeGateLever : MonoBehaviour, IMiniVanGameModeInteractable
    {
        public MiniVanGameModeGateController Gate;
        public Transform Handle;
        public Vector3 ClosedEuler = new Vector3(-28f, 0f, 0f);
        public Vector3 OpenEuler = new Vector3(28f, 0f, 0f);

        private Renderer[] highlightRenderers;
        private Material[][] originalMaterials;
        private Material outlineMaterial;
        private bool isHighlighted;

        private void Awake()
        {
            highlightRenderers = GetComponentsInChildren<Renderer>(true);
            CacheOriginalMaterials();
        }

        private void Update()
        {
            if (Handle != null && Gate != null)
            {
                Handle.localRotation = Quaternion.Euler(Gate.IsOpen ? OpenEuler : ClosedEuler);
            }
        }

        public string GetPrompt(MiniVanPlayer player)
        {
            if (Gate == null)
            {
                return string.Empty;
            }
            return Gate.IsOpen ? "Gate is open" : "E - open start gate";
        }

        public void Interact(MiniVanPlayer player)
        {
            if (player != null && Gate != null && !Gate.IsOpen)
            {
                player.GameModeRequestOpenGate(Gate.GateId);
            }
        }

        public void PrimaryAction(MiniVanPlayer player)
        {
        }

        public void SetHighlighted(bool highlighted)
        {
            if (isHighlighted == highlighted)
            {
                return;
            }

            if (highlightRenderers == null)
            {
                highlightRenderers = GetComponentsInChildren<Renderer>(true);
                CacheOriginalMaterials();
            }
            isHighlighted = highlighted;
            if (highlighted)
            {
                Material outline = GetOutlineMaterial();
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

        private void CacheOriginalMaterials()
        {
            if (highlightRenderers == null)
            {
                return;
            }

            originalMaterials = new Material[highlightRenderers.Length][];
            for (int i = 0; i < highlightRenderers.Length; i++)
            {
                originalMaterials[i] = highlightRenderers[i] != null
                    ? highlightRenderers[i].sharedMaterials
                    : new Material[0];
            }
        }

        private Material GetOutlineMaterial()
        {
            if (outlineMaterial != null)
            {
                return outlineMaterial;
            }

            outlineMaterial = Resources.Load<Material>("Panelka/ThinWhiteOutline");
            if (outlineMaterial != null)
            {
                outlineMaterial = new Material(outlineMaterial)
                {
                    name = "Gate Lever Thin White Outline"
                };
                MiniVanSnesOutline.ApplyOutlineSettings(outlineMaterial, Color.white);
                return outlineMaterial;
            }

            Shader shader = Shader.Find("MiniVanGame/ThinWhiteOutline");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }
            outlineMaterial = new Material(shader != null ? shader : Shader.Find("Standard"));
            outlineMaterial.name = "Gate Lever Thin White Outline";
            MiniVanSnesOutline.ApplyOutlineSettings(outlineMaterial, Color.white);
            return outlineMaterial;
        }

        private void OnDisable()
        {
            SetHighlighted(false);
        }
    }
}
