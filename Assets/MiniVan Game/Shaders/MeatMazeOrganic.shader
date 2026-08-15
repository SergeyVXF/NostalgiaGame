Shader "MiniVanGame/MeatMazeOrganic"
{
    Properties
    {
        _BaseColor ("Meat Color", Color) = (0.46, 0.11, 0.10, 1)
        _DeepColor ("Deep Color", Color) = (0.16, 0.03, 0.04, 1)
        [HDR]_SheenColor ("Wet Sheen", Color) = (0.7, 0.34, 0.32, 1)
        [HDR]_EmberColor ("Burn Ember", Color) = (2.4, 0.7, 0.12, 1)
        _CharColor ("Char Color", Color) = (0.06, 0.04, 0.04, 1)
        _VeinScale ("Vein Scale", Float) = 2.6
        _VeinStrength ("Vein Strength", Range(0,1)) = 0.55
        _BumpStrength ("Surface Bump", Range(0,1)) = 0.4
        _BreathAmount ("Motion Distort", Range(0,1)) = 0.25
        _BreathSpeed ("Motion Speed", Float) = 1.1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_fog
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _DeepColor;
                float4 _SheenColor;
                float4 _EmberColor;
                float4 _CharColor;
                float _VeinScale;
                float _VeinStrength;
                float _BumpStrength;
                float _BreathAmount;
                float _BreathSpeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionOS : TEXCOORD2;
                float burn : TEXCOORD3;
                float fogFactor : TEXCOORD4;
            };

            float Hash31(float3 p)
            {
                p = frac(p * 0.3183099 + float3(0.1, 0.2, 0.3));
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            float Noise3D(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                float n000 = Hash31(i + float3(0, 0, 0));
                float n100 = Hash31(i + float3(1, 0, 0));
                float n010 = Hash31(i + float3(0, 1, 0));
                float n110 = Hash31(i + float3(1, 1, 0));
                float n001 = Hash31(i + float3(0, 0, 1));
                float n101 = Hash31(i + float3(1, 0, 1));
                float n011 = Hash31(i + float3(0, 1, 1));
                float n111 = Hash31(i + float3(1, 1, 1));

                float x00 = lerp(n000, n100, f.x);
                float x10 = lerp(n010, n110, f.x);
                float x01 = lerp(n001, n101, f.x);
                float x11 = lerp(n011, n111, f.x);
                return lerp(lerp(x00, x10, f.y), lerp(x01, x11, f.y), f.z);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 posOS = input.positionOS.xyz;
                float burn = saturate(input.color.r);

                // Visual-only writhe: inflate along the normal and drift sideways.
                // Colliders stay still, so keep extreme values for mood, not precision.
                float phase = Noise3D(posOS * 0.55) * 6.2831;
                float alive = 1.0 - burn * 0.85;
                float amp = _BreathAmount * _BreathAmount * 0.55 * alive;
                float pulse = sin(_Time.y * _BreathSpeed + phase);
                float pulse2 = sin(_Time.y * _BreathSpeed * 1.37 + phase * 1.7);

                float3 n = normalize(input.normalOS);
                float3 twist = normalize(float3(
                    Noise3D(posOS * 0.9 + float3(_Time.y * 0.15, 0, 0)) - 0.5,
                    Noise3D(posOS * 0.9 + float3(0, _Time.y * 0.11, 17.3)) - 0.5,
                    Noise3D(posOS * 0.9 + float3(0, 31.1, _Time.y * 0.13)) - 0.5));
                twist = normalize(twist - n * dot(twist, n) + 1e-5);

                posOS += n * (pulse * amp);
                posOS += twist * (pulse2 * amp * 0.65);

                float3 positionWS = TransformObjectToWorld(posOS);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.positionWS = positionWS;
                output.positionOS = posOS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.burn = burn;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 geoN = normalize(input.normalWS);
                float3 V = normalize(GetWorldSpaceViewDir(input.positionWS));
                float burn = saturate(input.burn);

                // Veins / marbling from world position so the pattern is continuous across the mass.
                float3 p = input.positionWS * _VeinScale;
                float veinA = Noise3D(p);
                float veinB = Noise3D(p * 2.7 + 13.0);
                float vein = saturate(veinA * 0.7 + veinB * 0.3);
                float sinew = smoothstep(0.42, 0.62, abs(vein - 0.5) * 2.0);

                // Three independent smooth noises wobble the normal without exposing the noise lattice.
                float3 bp = input.positionWS * _VeinScale * 1.8;
                float bumpCenter = Noise3D(bp);
                float3 wobble = float3(bumpCenter, Noise3D(bp + 21.3), Noise3D(bp + 47.7)) - 0.5;
                float3 N = normalize(geoN + wobble * _BumpStrength);

                float3 albedo = lerp(_DeepColor.rgb, _BaseColor.rgb, vein);
                albedo = lerp(albedo, _BaseColor.rgb * 1.25, sinew * _VeinStrength);

                // Cavity: crevices and downward faces stay darker.
                float cavity = saturate(geoN.y * 0.5 + 0.5);
                albedo *= lerp(0.42, 1.0, cavity) * lerp(0.7, 1.1, bumpCenter);

                Light mainLight = GetMainLight();
                float ndl = dot(N, mainLight.direction);
                // Wrapped diffuse fakes the translucency of flesh.
                float wrapped = saturate((ndl + 0.55) / 1.55);
                float3 ambient = SampleSH(N);

                float3 lighting = ambient * 0.9 + mainLight.color * wrapped * 0.85;
                float3 color = albedo * lighting;

                // Subsurface red bleed on the light-facing rim.
                float back = saturate(dot(-N, mainLight.direction) * 0.5 + 0.5);
                color += _BaseColor.rgb * mainLight.color * pow(back, 3.0) * 0.35;

                // Wet specular sheen, kept low so the mass does not read as plastic.
                float fresnel = pow(1.0 - saturate(dot(N, V)), 4.0);
                float3 h = normalize(mainLight.direction + V);
                float spec = pow(saturate(dot(N, h)), 20.0);
                color += _SheenColor.rgb * (fresnel * 0.12 + spec * 0.14) * (1.0 - burn);

                // Burn: char the surface and glow along the burn front.
                float charMask = smoothstep(0.05, 0.55, burn);
                color = lerp(color, _CharColor.rgb, charMask);
                float ember = smoothstep(0.08, 0.3, burn) * (1.0 - smoothstep(0.45, 0.85, burn));
                ember *= lerp(0.6, 1.4, veinB);
                color += _EmberColor.rgb * ember;

                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex shadowVert
            #pragma fragment shadowFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
            };

            ShadowVaryings shadowVert(ShadowAttributes input)
            {
                ShadowVaryings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float3 lightDirectionWS = _MainLightPosition.xyz;
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                return output;
            }

            half4 shadowFrag(ShadowVaryings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex depthVert
            #pragma fragment depthFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct DepthAttributes
            {
                float4 positionOS : POSITION;
            };

            struct DepthVaryings
            {
                float4 positionCS : SV_POSITION;
            };

            DepthVaryings depthVert(DepthAttributes input)
            {
                DepthVaryings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 depthFrag(DepthVaryings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
