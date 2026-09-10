Shader "Custom/BiomeShader_URP"
{
    Properties
    {
        _SandTex ("Sand", 2D) = "white" {}
        _GrassTex ("Grass", 2D) = "white" {}
        _SnowTex ("Snow", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalRenderPipeline" "RenderType"="Opaque" }

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
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            TEXTURE2D(_SandTex);
            SAMPLER(sampler_SandTex);

            TEXTURE2D(_GrassTex);
            SAMPLER(sampler_GrassTex);

            TEXTURE2D(_SnowTex);
            SAMPLER(sampler_SnowTex);

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv * 20;
                o.color = v.color;
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                half4 sand = SAMPLE_TEXTURE2D(_SandTex, sampler_SandTex, i.uv);
                half4 grass = SAMPLE_TEXTURE2D(_GrassTex, sampler_GrassTex, i.uv);
                half4 snow = SAMPLE_TEXTURE2D(_SnowTex, sampler_SnowTex, i.uv);

                float3 mix1 = lerp(sand.rgb, grass.rgb, i.color.g);
                float3 finalColor = lerp(mix1, snow.rgb, i.color.r);

                return half4(finalColor, 1);
            }

            ENDHLSL
        }
    }
}