Shader "Custom/B&W Impact Frame"
{
    Properties
    {
        _Threshold ("Threshold", Range(0, 1)) = 0.5
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        LOD 100
        ZWrite Off
        Cull Off

        Pass
        {
            Name "B&WBlitPass"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D(_BlitTexture);

            float _Threshold;

            static const float3 LUMINANCE_WEIGHTS = float3(0.299, 0.587, 0.114);

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float4 color = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, input.texcoord);
                float luminance = dot(color.rgb, LUMINANCE_WEIGHTS);
                float stepResult = step(_Threshold, luminance);
                float3 bwColor = lerp(float3(0, 0, 0), float3(1, 1, 1), stepResult);

                return half4(bwColor, 1);
            }
            ENDHLSL
        }
    }
}
