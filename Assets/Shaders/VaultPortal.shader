Shader "Juicy/VaultPortal"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _EmissionMap ("Emission", 2D) = "black" {}
        _NormalMap ("Normal", 2D) = "bump" {}
        [HDR] _EmissionColor ("Emission Color", Color) = (2, 0.2, 0.2, 1)
        _EmissionGain ("Emission Gain", Float) = 0.7
        _Cutoff ("Cutoff", Range(0, 1)) = 0.15
        _NormalStrength ("Normal Strength", Range(0, 2)) = 0.75
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
            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _EmissionColor;
                float _EmissionGain;
                float _Cutoff;
                float _NormalStrength;
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

                float3 normal = SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, input.uv).xyz * 2.0 - 1.0;
                normal.xy *= _NormalStrength;
                normal = normalize(normal);
                float3 lightDir = normalize(float3(-0.42, 0.70, 0.86));
                float facing = saturate(dot(normal, lightDir));
                float3 halfDir = normalize(lightDir + float3(0.0, 0.0, 1.0));
                float spec = pow(saturate(dot(halfDir, normal)), 24.0);
                color *= lerp(0.94, 1.04, facing);
                color += spec * float3(0.48, 0.38, 0.16);
                return float4(color, 1);
            }
            ENDHLSL
        }
    }
}
