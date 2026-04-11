Shader "Custom/BiomeShader_URP"
{
    Properties
    {
        _SandTex ("Sand", 2D) = "white" {}
        _GrassTex ("Grass", 2D) = "white" {}
        _SnowTex ("Snow", 2D) = "white" {}

        _EnchantedColor ("Enchanted Color", Color) = (0.2, 0.35, 0.25, 1)

        _RoadColor ("Road Color", Color) = (0.95, 0.90, 0.72, 1)
        _RoadBlendThreshold ("Road Blend Threshold", Range(0, 1)) = 0.55
        _RoadBlendSoftness ("Road Blend Softness", Range(0.001, 0.25)) = 0.06
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

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
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            TEXTURE2D(_SandTex); SAMPLER(sampler_SandTex);
            TEXTURE2D(_GrassTex); SAMPLER(sampler_GrassTex);
            TEXTURE2D(_SnowTex); SAMPLER(sampler_SnowTex);

            float4 _EnchantedColor;
            float4 _RoadColor;
            float _RoadBlendThreshold;
            float _RoadBlendSoftness;

            Varyings vert(Attributes input)
            {
                Varyings o;
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionHCS = pos.positionCS;
                o.uv = input.uv * 10.0; // menos repetição
                o.color = input.color;
                return o;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half3 sand = SAMPLE_TEXTURE2D(_SandTex, sampler_SandTex, input.uv).rgb;
                half3 grass = SAMPLE_TEXTURE2D(_GrassTex, sampler_GrassTex, input.uv).rgb;
                half3 snow = SAMPLE_TEXTURE2D(_SnowTex, sampler_SnowTex, input.uv).rgb;

                // 🌲 BASE REALISTA (enchanted)
                half3 enchanted = _EnchantedColor.rgb;

                // variação MUITO suave (natural)
                float noise = sin(input.uv.x * 0.5) * cos(input.uv.y * 0.5);
                enchanted *= lerp(0.95, 1.05, noise);

                // leve escurecimento em áreas (profundidade)
                float shade = saturate(input.uv.y * 0.1);
                enchanted *= lerp(0.85, 1.1, shade);

                // 🎯 máscaras
                float forestMask = saturate(input.color.g);
                float snowMask = saturate(input.color.r);
                float enchantedMask = saturate(input.color.b);
                float roadMask = saturate(input.color.a);

                half3 mixed = sand;
                mixed = lerp(mixed, grass, forestMask);
                mixed = lerp(mixed, snow, snowMask);
                mixed = lerp(mixed, enchanted, enchantedMask);

                float roadBlend = smoothstep(
                    _RoadBlendThreshold - _RoadBlendSoftness,
                    _RoadBlendThreshold + _RoadBlendSoftness,
                    roadMask
                );

                mixed = lerp(mixed, _RoadColor.rgb, roadBlend);

                return half4(mixed, 1);
            }

            ENDHLSL
        }
    }
}