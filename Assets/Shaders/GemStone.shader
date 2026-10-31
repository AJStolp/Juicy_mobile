Shader "Juicy/GemStone"
{
    Properties
    {
        _BaseColor ("Color", Color) = (1, 1, 1, 1)
        _EmissionColor ("Emission", Color) = (0, 0, 0, 1)
        _Metal ("Metal", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewDirWS : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _EmissionColor;
                float _Metal;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = pos.positionCS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewDirWS = GetWorldSpaceViewDir(pos.positionWS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 normal = normalize(input.normalWS);
                float3 viewDir = normalize(input.viewDirWS);
                float3 key = normalize(float3(0.45, 0.75, -0.40));
                float3 fill = normalize(float3(-0.55, 0.15, -0.35));
                float facing = saturate(dot(normal, viewDir));
                float keyLight = saturate(dot(normal, key));
                float fillLight = saturate(dot(normal, fill));
                float3 halfDir = normalize(key + viewDir);
                float gloss = lerp(28.0, 16.0, _Metal);
                float spec = pow(saturate(dot(normal, halfDir)), gloss);
                float glint = pow(saturate(dot(normal, halfDir)), 6.0);
                float fresnel = pow(1.0 - facing, 2.0);
                float3 body = _BaseColor.rgb;
                float wrap = keyLight * 0.5 + fillLight * 0.22 + 0.62;
                float3 color = body * wrap;
                float sky = saturate(reflect(-viewDir, normal).y * 0.5 + 0.5);
                float3 env = lerp(float3(0.28, 0.12, 0.07), float3(1.0, 0.98, 0.94), sky);
                color += env * fresnel * lerp(0.7, 0.22, _Metal);
                color += glint * lerp(0.45, 0.15, _Metal);
                color += spec * lerp(float3(1.0, 1.0, 1.0), body, _Metal) * lerp(1.7, 1.0, _Metal);
                color += _EmissionColor.rgb;
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
