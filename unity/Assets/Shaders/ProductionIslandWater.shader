Shader "ProjectPenguin/ProductionIsland/Water"
{
    Properties
    {
        _BaseColor ("Water", Color) = (0.12, 0.55, 0.92, 1)
        _FoamColor ("Ripple", Color) = (0.71, 0.86, 0.97, 1)
        _WaveHeight ("Wave height", Range(0, 0.02)) = 0.006
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 world : TEXCOORD0; };
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half4 _FoamColor;
            float _WaveHeight;
            CBUFFER_END
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.world = TransformObjectToWorld(input.positionOS.xyz);
                output.world.y += sin(output.world.x * 6 + output.world.z * 4 + _Time.y) * _WaveHeight;
                output.positionCS = TransformWorldToHClip(output.world);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float ripple = pow(saturate(sin(input.world.x * 8 + input.world.z * 12 + _Time.y * 1.4)), 18);
                return lerp(_BaseColor, _FoamColor, ripple * .25);
            }
            ENDHLSL
        }
    }
}
