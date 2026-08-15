Shader "MiniVanGame/ThinWhiteOutline"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (1, 1, 1, 1)
        // OmniShade URP object-space units.
        _OutlineWidth ("Outline Width", Range(0, 0.1)) = 0
        [Toggle] _OutlineWidthIndependent ("Outline Width Camera-Independent", Float) = 0
        _OutlineZPos ("Outline Z Offset", Range(-0.1, 1)) = -0.1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry+10"
        }

        // OmniShade Hide Interior: forward writes stencil (Replace), then outline
        // draws only where stencil != ref → outer silhouette, no inner plates.
        Pass
        {
            Name "OutlineStencilMask"
            Tags { "LightMode" = "UniversalForwardOnly" }
            Cull Back
            ZWrite Off
            ZTest LEqual
            ColorMask 0
            Blend One Zero

            Stencil
            {
                Ref 1
                Comp Always
                Pass Replace
                Fail Keep
                ZFail Keep
            }

            HLSLPROGRAM
            #pragma vertex VertMask
            #pragma fragment FragMask
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings VertMask(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 FragMask(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "OutlineSilhouette"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            ZWrite On
            ZTest LEqual
            Blend One Zero

            Stencil
            {
                Ref 1
                Comp NotEqual
                Pass Keep
                Fail Keep
                ZFail Keep
            }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                float _OutlineWidth;
                float _OutlineWidthIndependent;
                float _OutlineZPos;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float outlineWidth = clamp(_OutlineWidth, 0.0, 0.1);
                float3 viewOrigin = TransformWorldToView(TransformObjectToWorld(float3(0, 0, 0)));
                float objDepth = max(0.001, -viewOrigin.z);

                if (_OutlineWidthIndependent > 0.5)
                {
                    float objDepthLog = objDepth;
                    if (objDepthLog > 1.0)
                    {
                        objDepthLog = 1.0 + log(objDepthLog);
                    }

                    outlineWidth *= objDepthLog;
                }

                // OmniShade: object-space inflate along mesh normals.
                float3 inflatedOS = input.positionOS.xyz + normalize(input.normalOS) * outlineWidth;
                float4 positionCS = TransformObjectToHClip(inflatedOS);

                float outlineOffset = _OutlineZPos / -100.0 / objDepth;
                #if !UNITY_REVERSED_Z
                outlineOffset = -outlineOffset;
                #endif
                positionCS.z += outlineOffset;

                output.positionCS = positionCS;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return _OutlineColor;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
