Shader "Custom/WindShader"
{
    Properties
    {
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Color ("Tint Color", Color) = (1,1,1,1)
        _WindStrength ("Wind Strength", Range(0.0, 2.0)) = 0.5
        _WindSpeed ("Wind Speed", Range(0.1, 5.0)) = 1.0
        _WindDirection ("Wind Direction", Vector) = (1, 0, 0, 0)
        _LeafStiffness ("Leaf Stiffness", Range(0.1, 2.0)) = 1.0
        _LeafSize ("Leaf Size", Range(0.1, 5.0)) = 1.0
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.5
    }
    SubShader
    {
        Tags { "Queue"="AlphaTest" "RenderType"="TransparentCutout" }
        LOD 200
        Cull Off

        CGPROGRAM
        #pragma surface surf Lambert vertex:vert alphatest:_Cutoff
        #pragma target 3.0

        sampler2D _MainTex;

        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos;
        };

        float _WindStrength;
        float _WindSpeed;
        float4 _WindDirection;
        float _LeafStiffness;
        float _LeafSize;
        float4 _Color;

        void vert(inout appdata_full v)
        {
            float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
            float2 noiseUV = worldPos.xz * _LeafSize + _Time.y * _WindSpeed;
            float windNoise = sin(noiseUV.x + noiseUV.y);
            float3 windOffset = _WindDirection.xyz * windNoise * _WindStrength * _LeafStiffness;
            v.vertex.xyz += windOffset;
        }

        void surf (Input IN, inout SurfaceOutput o)
        {
            float2 windUV = IN.worldPos.xz * _LeafSize + _Time.y * _WindSpeed;
            float windNoise = sin(windUV.x + windUV.y);
            float2 deformedUV = IN.uv_MainTex + windNoise * _WindStrength * 0.02;
            fixed4 c = tex2D (_MainTex, deformedUV);
            o.Albedo = c.rgb * _Color.rgb;
            o.Emission = c.rgb * _Color.rgb;
            o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack "Transparent/Cutout/Diffuse"
} 