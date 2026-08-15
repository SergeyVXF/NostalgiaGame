using UnityEngine;
using UnityEngine.Rendering;

namespace MiniVanGame
{
    /// <summary>
    /// Keeps TextMesh labels on the depth-tested world-text material so they
    /// don't draw through walls. Applies once on enable and then re-checks at a
    /// low frequency (TextMesh can silently restore its font material).
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class MiniVanPanelkaWorldTextDepth : MonoBehaviour
    {
        private const string MaterialResourceName = "Panelka_WorldTextDepth";
        private const int DepthRenderQueue = 2450;
        private const float RecheckInterval = 2f;

        private static Material worldTextMaterial;
        private static Shader worldTextShader;

        private MeshRenderer[] cachedTextRenderers;
        private float nextCheckTime;

        private void OnEnable()
        {
            CacheRenderers();
            ApplyMaterial();
            nextCheckTime = Time.unscaledTime + RecheckInterval;
        }

        private void LateUpdate()
        {
            if (Time.unscaledTime < nextCheckTime)
            {
                return;
            }

            nextCheckTime = Time.unscaledTime + RecheckInterval;
            ApplyMaterial();
        }

        public void ApplyNow()
        {
            CacheRenderers();
            ApplyMaterial();
        }

        private void CacheRenderers()
        {
            TextMesh[] texts = GetComponentsInChildren<TextMesh>(true);
            cachedTextRenderers = new MeshRenderer[texts.Length];
            for (int i = 0; i < texts.Length; i++)
            {
                cachedTextRenderers[i] = texts[i] != null
                    ? texts[i].GetComponent<MeshRenderer>()
                    : null;
            }
        }

        private void ApplyMaterial()
        {
            EnsureSharedMaterial();
            if (worldTextMaterial == null || cachedTextRenderers == null)
            {
                return;
            }

            if (worldTextMaterial.renderQueue != DepthRenderQueue)
            {
                worldTextMaterial.renderQueue = DepthRenderQueue;
            }

            for (int i = 0; i < cachedTextRenderers.Length; i++)
            {
                MeshRenderer renderer = cachedTextRenderers[i];
                if (renderer != null && renderer.sharedMaterial != worldTextMaterial)
                {
                    renderer.sharedMaterial = worldTextMaterial;
                }
            }
        }

        private static void EnsureSharedMaterial()
        {
            if (worldTextMaterial == null)
            {
                worldTextMaterial = Resources.Load<Material>(MaterialResourceName);
            }

            if (worldTextShader == null)
            {
                worldTextShader = Shader.Find("MiniVan/WorldTextDepth");
            }

            if (worldTextMaterial == null && worldTextShader != null)
            {
                worldTextMaterial = new Material(worldTextShader)
                {
                    name = MaterialResourceName + " (Runtime)",
                    renderQueue = DepthRenderQueue,
                    enableInstancing = true
                };
            }
            else if (worldTextMaterial != null &&
                     worldTextShader != null &&
                     worldTextMaterial.shader != worldTextShader)
            {
                worldTextMaterial.shader = worldTextShader;
            }
        }
    }
}
