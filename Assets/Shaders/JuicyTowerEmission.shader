Shader "Juicy/TowerEmission"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _EmissionMap ("Emission", 2D) = "black" {}
        [HDR] _EmissionColor ("Emission Color", Color) = (1.4, 0.35, 2.4, 1)
        _EmissionGain ("Emission Gain", Float) = 1
        _Cutoff ("Cutoff", Range(0, 1)) = 0.15
    }
    SubShader
    {
        Tags
        {
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
            "RenderPipeline" = "UniversalPipeline"
        }
        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZWrite On
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_EmissionMap);
            SAMPLER(sampler_EmissionMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _EmissionColor;
                float _EmissionGain;
                float _Cutoff;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float4 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;
                clip(albedo.a - _Cutoff);
                float4 emission = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, input.uv);
                float hot = max(emission.r, max(emission.g, emission.b));
                hot = saturate((hot - 0.28) / 0.72);
                float3 color = albedo.rgb + _EmissionColor.rgb * hot * _EmissionGain;
                return float4(color, 1);
            }
            ENDHLSL
        }
    }
}
