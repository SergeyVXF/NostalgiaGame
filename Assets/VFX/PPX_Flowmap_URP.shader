Shader "Effect/PPX_FX/PPX_flowmap_shader_URP"
{
    Properties
    {
        [Enum(Off,0,On,1)] _Zwrite("ZWrite", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 0
        _add_or_blend("Blend (0 Add, 1 Alpha)", Range(0, 1)) = 1

        [NoScaleOffset] _base_texture("Base Texture", 2D) = "white" {}
        _HUE("Hue", Range(0, 1)) = 0
        _Saturation("Saturation", Range(-1, 1)) = 0
        _Value("Value", Range(-1, 1)) = 0
        [HDR] _Base_color("Base Color", Color) = (1, 1, 1, 0)
        _Glow("Base Intensity", Range(0, 3)) = 1
        _alpha_power("Alpha", Range(0, 3)) = 1

        [NoScaleOffset] _emissive_map("Emission Mask", 2D) = "black" {}
        [HDR] _Emissive_color("Emission Color", Color) = (1, 1, 1, 0)
        _Emissive_power("Emission Intensity", Range(0, 3)) = 1

        [Toggle(_USE_DISSOLVE_ON)] _use_dissolve("Use Dissolve", Float) = 0
        [NoScaleOffset] _dissolve_texture("Dissolve Texture", 2D) = "white" {}
        _edge_hardness("Dissolve Edge Hardness", Range(0, 22)) = 1
        _dissolve("Dissolve", Range(0, 1)) = 1
        [Toggle(_USE_CURVE_DISSOLVE_ON)] _use_curve_dissolve("Dissolve From UV0 Z", Float) = 0

        [NoScaleOffset] _flowmap("Flow Map", 2D) = "gray" {}
        _flow("Flow Strength", Range(0, 1.5)) = 1
        [Toggle(_USE_CUSTOM1_Z_FLOW_ON)] _use_custom1_z_flow("Flow From Custom1 X", Float) = 0

        _Distort_tex("Distortion Texture", 2D) = "gray" {}
        _Distort_uv("Distortion Tiling / Offset", Vector) = (1, 1, 0, 0)
        _Distort_mask("Distortion Mask", 2D) = "white" {}
        _Distort_mask_uv("Mask Tiling / Offset", Vector) = (1, 1, 0, 0)
        _X_speed("Distortion X Speed", Range(-1, 1)) = 1
        _Y_speed("Distortion Y Speed", Range(-1, 1)) = 1
        _Time_scale("Distortion Speed", Range(0, 5)) = 1
        _Distort_power("Distortion Strength", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "UniversalMaterialType" = "Unlit"
            "RenderPipeline" = "UniversalPipeline"
        }

        LOD 100
        Blend One OneMinusSrcAlpha
        Cull [_Cull]
        ZWrite [_Zwrite]
        ZTest LEqual

        Pass
        {
            Name "PPXFlowmapUnlit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma shader_feature_local_fragment _USE_DISSOLVE_ON
            #pragma shader_feature_local_fragment _USE_CURVE_DISSOLVE_ON
            #pragma shader_feature_local_fragment _USE_CUSTOM1_Z_FLOW_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float4 uv0 : TEXCOORD0;
                float4 custom1 : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float4 uv0 : TEXCOORD0;
                float4 custom1 : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_base_texture);
            SAMPLER(sampler_base_texture);
            TEXTURE2D(_emissive_map);
            SAMPLER(sampler_emissive_map);
            TEXTURE2D(_dissolve_texture);
            SAMPLER(sampler_dissolve_texture);
            TEXTURE2D(_flowmap);
            SAMPLER(sampler_flowmap);
            TEXTURE2D(_Distort_tex);
            SAMPLER(sampler_Distort_tex);
            TEXTURE2D(_Distort_mask);
            SAMPLER(sampler_Distort_mask);

            CBUFFER_START(UnityPerMaterial)
                float4 _Base_color;
                float4 _Emissive_color;
                float4 _Distort_uv;
                float4 _Distort_mask_uv;
                float _HUE;
                float _Saturation;
                float _Value;
                float _Glow;
                float _alpha_power;
                float _Emissive_power;
                float _edge_hardness;
                float _dissolve;
                float _flow;
                float _X_speed;
                float _Y_speed;
                float _Time_scale;
                float _Distort_power;
                float _add_or_blend;
            CBUFFER_END

            float3 RGBToHSV(float3 color)
            {
                const float4 K = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
                float4 p = lerp(float4(color.bg, K.wz), float4(color.gb, K.xy), step(color.b, color.g));
                float4 q = lerp(float4(p.xyw, color.r), float4(color.r, p.yzx), step(p.x, color.r));
                float delta = q.x - min(q.w, q.y);
                const float epsilon = 1.0e-10;
                return float3(abs(q.z + (q.w - q.y) / (6.0 * delta + epsilon)), delta / (q.x + epsilon), q.x);
            }

            float3 HSVToRGB(float3 hsv)
            {
                const float4 K = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
                float3 p = abs(frac(hsv.xxx + K.xyz) * 6.0 - K.www);
                return hsv.z * lerp(K.xxx, saturate(p - K.xxx), hsv.y);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.uv0 = input.uv0;
                output.custom1 = input.custom1;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 baseUV = input.uv0.xy;
                float2 distortUV = baseUV * _Distort_uv.xy + _Distort_uv.zw;
                float2 maskUV = baseUV * _Distort_mask_uv.xy + _Distort_mask_uv.zw;

                float2 speed = float2(_X_speed, _Y_speed);
                float speedLength = max(length(speed), 1.0e-5);
                float2 speedDirection = speed / speedLength;
                float2 animatedDistortUV = distortUV + (_Time.y * _Time_scale * speedDirection);
                float2 distortion = SAMPLE_TEXTURE2D(_Distort_tex, sampler_Distort_tex, animatedDistortUV).rg;
                float distortionMask = SAMPLE_TEXTURE2D(_Distort_mask, sampler_Distort_mask, maskUV).r;
                float2 distortedUV = baseUV + distortion * (_Distort_power * distortionMask);

                float flowStrength = _flow;
                #if defined(_USE_CUSTOM1_Z_FLOW_ON)
                    flowStrength *= input.custom1.x;
                #endif

                float2 flowUV = SAMPLE_TEXTURE2D(_flowmap, sampler_flowmap, baseUV).rg;
                float2 finalUV = lerp(distortedUV, flowUV, flowStrength);
                half4 baseSample = SAMPLE_TEXTURE2D(_base_texture, sampler_base_texture, finalUV);

                float3 hsv = RGBToHSV(baseSample.rgb);
                hsv += float3(_HUE, _Saturation, _Value);
                float3 adjustedBase = HSVToRGB(hsv);

                half alpha = input.color.a * saturate(_alpha_power * baseSample.a);
                #if defined(_USE_DISSOLVE_ON)
                    float dissolveAmount = _dissolve;
                    #if defined(_USE_CURVE_DISSOLVE_ON)
                        dissolveAmount *= input.uv0.z;
                    #endif

                    float edgeHardness = max(_edge_hardness, 1.0e-4);
                    float threshold = lerp(edgeHardness, -1.0, saturate(dissolveAmount));
                    float dissolveSample = SAMPLE_TEXTURE2D(_dissolve_texture, sampler_dissolve_texture, finalUV).r;
                    alpha *= saturate(dissolveSample * edgeHardness - threshold);
                #endif

                float emissionMask = SAMPLE_TEXTURE2D(_emissive_map, sampler_emissive_map, finalUV).r;
                float3 baseColor = input.color.rgb * adjustedBase * _Base_color.rgb * _Glow;
                float3 emission = emissionMask * _Emissive_color.rgb * input.uv0.w * _Emissive_power;
                float3 premultipliedColor = (baseColor + emission) * alpha;

                return half4(premultipliedColor, alpha * _add_or_blend);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
