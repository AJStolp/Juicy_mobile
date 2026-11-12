Shader "Juicy/KeyedPortrait"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Key ("Key", Color) = (1, 0, 1, 1)
        _Range ("Range", Range(0, 1)) = 0.32
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

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

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Key;
                float _Range;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half dist = distance(tex.rgb, _Key.rgb);
                half edge = max(_Range, 0.001);
                half alpha = saturate((dist - edge * 0.4) / (edge * 0.6));
                clip(alpha - 0.02);
                return half4(tex.rgb, alpha) * input.color;
            }
            ENDHLSL
        }
    }
}
