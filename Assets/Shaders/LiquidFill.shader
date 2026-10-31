Shader "Juicy/LiquidFill"
{
    Properties
    {
        _BaseColor ("Color", Color) = (0.78, 0.02, 0.045, 1)
        _Fill ("Fill", Range(0, 1)) = 1
        _Wobble ("Wobble", Float) = 0.06
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
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _Fill;
                float _Wobble;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 position = input.positionOS.xyz;
                position.y += sin(_Time.y * 3.2 + position.x * 9.0) * _Wobble * _Fill;
                output.positionCS = TransformObjectToHClip(position);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 normal = normalize(input.normalWS);
                float fresnel = pow(1.0 - saturate(dot(normal, float3(0.0, 0.35, 0.94))), 2.0);
                float3 deep = _BaseColor.rgb;
                float3 shine = min(deep * 1.9, float3(1.0, 1.0, 1.0));
                float3 color = lerp(deep, shine, fresnel * 0.75);
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
