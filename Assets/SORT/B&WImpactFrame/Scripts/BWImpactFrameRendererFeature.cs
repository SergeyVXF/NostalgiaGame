using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class BWImpactFrameRendererFeature : ScriptableRendererFeature
{
    [SerializeField] private Shader m_Shader;
    [Range(0f, 1f)] [SerializeField] private float m_Threshold = 0.5f;

    private Material m_Material;
    private BWImpactFramePass m_Pass;

    public override void Create()
    {
        if (m_Shader == null) return;

        m_Material = CoreUtils.CreateEngineMaterial(m_Shader);
        m_Pass = new BWImpactFramePass(m_Material);
        m_Pass.renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (m_Material == null) return;
        if (renderingData.cameraData.cameraType != CameraType.Game) return;

        m_Pass.SetThreshold(m_Threshold);
        m_Pass.SetTarget(renderer.cameraColorTargetHandle);
        renderer.EnqueuePass(m_Pass);
    }

    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(m_Material);
    }

    private class BWImpactFramePass : ScriptableRenderPass
    {
        private const string k_ProfilerTag = "BW Impact Frame";
        private readonly ProfilingSampler m_ProfilerSampler = new ProfilingSampler(k_ProfilerTag);

        private Material m_Material;
        private RTHandle m_TempRT;
        private RTHandle m_CameraColorTarget;
        private float m_Threshold;

        public BWImpactFramePass(Material material)
        {
            m_Material = material;
        }

        public void SetTarget(RTHandle colorTarget)
        {
            m_CameraColorTarget = colorTarget;
        }

        public void SetThreshold(float threshold)
        {
            m_Threshold = threshold;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            var descriptor = renderingData.cameraData.cameraTargetDescriptor;
            descriptor.depthBufferBits = 0;
            descriptor.msaaSamples = 1;
            RenderingUtils.ReAllocateIfNeeded(ref m_TempRT, descriptor, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_BWImpactFrameTemp");
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (m_Material == null) return;

            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, m_ProfilerSampler))
            {
                m_Material.SetFloat("_Threshold", m_Threshold);
                Blitter.BlitCameraTexture(cmd, m_CameraColorTarget, m_TempRT);
                Blitter.BlitCameraTexture(cmd, m_TempRT, m_CameraColorTarget, m_Material, 0);
            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public override void OnCameraCleanup(CommandBuffer cmd) { }
    }
}
