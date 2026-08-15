Shader "MiniVanGame/VampireShieldAura"
{
    Properties
    {
        [Header(Shield Color)]
        [HDR] _MainColor ("Shield Color", Color) = (1.4, 0.05, 0.04, 1)
        _EmissionSaturation ("Emission Strength", Range(0, 10)) = 3.2
        _OpacitySaturation ("Inner Opacity", Range(0, 1)) = 0.12

        [Header(Rim Glow)]
        _RimPower ("Rim Power", Range(0.2, 10)) = 2.4
        _RimIntensity ("Rim Intensity", Range(0, 12)) = 3.5
        _RimAlpha ("Rim Alpha Boost", Range(0, 4)) = 1.6

        [Header(Noise (from StandardDissolve))]
        _MainTexture ("Noise Texture", 2D) = "white" {}
        _PanningSpeed ("Panning Speed (XY)", Vector) = (0.08, 0.06, 0, 0)
        _NoiseStrength ("Noise Strength", Range(0, 1)) = 0.55
        _NoiseContrast ("Noise Contrast", Range(0.2, 4)) = 1.4

        [Header(Outline)]
        [Toggle] _OutlineEnabled ("Outline Enabled", Float) = 1
        [HDR] _OutlineColor ("Outline Color", Color) = (1.2, 0.08, 0.05, 0.95)
        _OutlineWidth ("Outline Width", Range(0, 0.08)) = 0.012
        [Toggle] _OutlineWidthIndependent ("Outline Camera Independent", Float) = 0
        _OutlineZPos ("Outline Z Offset", Range(-0.1, 1)) = 0

        [Header(Render)]
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
        [Toggle] _ZWrite ("ZWrite", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "VampireShieldOutline"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull Front
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha
            ColorMask RGB

            HLSLPROGRAM
            #pragma vertex VertOutline
            #pragma fragment FragOutline
            #pragma target 3.0
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _MainColor;
                float4 _MainTexture_ST;
                float4 _PanningSpeed;
                float _EmissionSaturation;
                float _OpacitySaturation;
                float _RimPower;
                float _RimIntensity;
                float _RimAlpha;
                float _NoiseStrength;
                float _NoiseContrast;
                float _OutlineEnabled;
                float4 _OutlineColor;
                float _OutlineWidth;
                float _OutlineWidthIndependent;
                float _OutlineZPos;
            CBUFFER_END

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

            Varyings VertOutline(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float outlineWidth = 0.0;
                if (_OutlineEnabled > 0.5)
                {
                    outlineWidth = max(0.0, _OutlineWidth);
                }

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

            half4 FragOutline(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                if (_OutlineEnabled < 0.5 || _OutlineWidth <= 0.00001)
                {
                    return half4(0, 0, 0, 0);
                }

                return half4(_OutlineColor.rgb, _OutlineColor.a);
            }
            ENDHLSL
        }

        Pass
        {
            Name "VampireShieldAura"
            Tags { "LightMode" = "UniversalForward" }

            Cull [_Cull]
            ZWrite [_ZWrite]
            ZTest LEqual
            Blend SrcAlpha One
            ColorMask RGB

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTexture);
            SAMPLER(sampler_MainTexture);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainColor;
                float4 _MainTexture_ST;
                float4 _PanningSpeed;
                float _EmissionSaturation;
                float _OpacitySaturation;
                float _RimPower;
                float _RimIntensity;
                float _RimAlpha;
                float _NoiseStrength;
                float _NoiseContrast;
                float _OutlineEnabled;
                float4 _OutlineColor;
                float _OutlineWidth;
                float _OutlineWidthIndependent;
                float _OutlineZPos;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float fogFactor : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                output.uv = TRANSFORM_TEX(input.uv, _MainTexture);
                output.fogFactor = ComputeFogFactor(posInputs.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);

                float ndotv = saturate(dot(normalWS, viewDirWS));
                float fresnel = pow(1.0 - ndotv, _RimPower);

                float2 uv = input.uv + _PanningSpeed.xy * _Time.y;
                float noise = SAMPLE_TEXTURE2D(_MainTexture, sampler_MainTexture, uv).r;
                noise = saturate(pow(max(noise, 1e-4), _NoiseContrast));
                float noiseMod = lerp(1.0, noise, _NoiseStrength);

                float rim = fresnel * _RimIntensity * noiseMod;
                float fill = _OpacitySaturation * noiseMod;

                float alpha = saturate(fill + fresnel * _RimAlpha);
                half3 color = _MainColor.rgb * (fill + rim) * _EmissionSaturation;

                color = MixFog(color, input.fogFactor);
                return half4(color, alpha * _MainColor.a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
