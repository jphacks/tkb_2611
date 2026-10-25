Shader "ProjectPenguin/ProductionIsland/Line"
{
    Properties
    {
        _BaseColor ("Colour", Color) = (0.12, 0.55, 0.92, 1)
        _Dashed ("Dashed", Float) = 0
        _Scroll ("Flow", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            float _Dashed;
            float _Scroll;
            CBUFFER_END
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float phase = frac(input.uv.x * 5 - _Time.y * _Scroll);
                half alpha = lerp(1, step(.35, phase), saturate(_Dashed));
                half pulse = lerp(1, .72 + .28 * sin(phase * 6.28318), saturate(_Scroll));
                return half4(_BaseColor.rgb * pulse, _BaseColor.a * alpha);
            }
            ENDHLSL
        }
    }
}
