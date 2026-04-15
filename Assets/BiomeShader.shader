Shader "Custom/BiomeShader_URP"
{
    Properties
    {
        _SandTex ("Sand", 2D) = "white" {}
        _GrassTex ("Grass", 2D) = "white" {}
        _SnowTex ("Snow", 2D) = "white" {}
        _SandColor ("Sand Color", Color) = (0.82, 0.76, 0.52, 1)
        _GrassColor ("Grass Color", Color) = (0.46, 0.67, 0.28, 1)
        _SnowColor ("Snow Color", Color) = (0.88, 0.92, 0.96, 1)
        _RoadColor ("Road Color", Color) = (0.82, 0.74, 0.58, 1)
        _UseFlatColors ("Use Flat Colors", Float) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
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
            half4 _SandColor;
            half4 _GrassColor;
            half4 _SnowColor;
            half4 _RoadColor;
            float _UseFlatColors;

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionHCS = positionInputs.positionCS;
                output.uv = input.uv * 20.0;
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half3 sand = SAMPLE_TEXTURE2D(_SandTex, sampler_SandTex, input.uv).rgb;
                half3 grass = SAMPLE_TEXTURE2D(_GrassTex, sampler_GrassTex, input.uv).rgb;
                half3 snow = SAMPLE_TEXTURE2D(_SnowTex, sampler_SnowTex, input.uv).rgb;

                sand = lerp(sand, _SandColor.rgb, saturate(_UseFlatColors));
                grass = lerp(grass, _GrassColor.rgb, saturate(_UseFlatColors));
                snow = lerp(snow, _SnowColor.rgb, saturate(_UseFlatColors));

                half3 mixed = lerp(sand, grass, saturate(input.color.g));
                mixed = lerp(mixed, snow, saturate(input.color.r));
                mixed = lerp(mixed, _RoadColor.rgb, saturate(input.color.a));
                return half4(mixed, 1.0);
            }
            ENDHLSL
        }
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }

        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _SandTex;
            sampler2D _GrassTex;
            sampler2D _SnowTex;
            fixed4 _SandColor;
            fixed4 _GrassColor;
            fixed4 _SnowColor;
            fixed4 _RoadColor;
            float _UseFlatColors;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            v2f vert(appdata input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv * 20.0;
                output.color = input.color;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                fixed3 sand = tex2D(_SandTex, input.uv).rgb;
                fixed3 grass = tex2D(_GrassTex, input.uv).rgb;
                fixed3 snow = tex2D(_SnowTex, input.uv).rgb;

                sand = lerp(sand, _SandColor.rgb, saturate(_UseFlatColors));
                grass = lerp(grass, _GrassColor.rgb, saturate(_UseFlatColors));
                snow = lerp(snow, _SnowColor.rgb, saturate(_UseFlatColors));

                fixed3 mixed = lerp(sand, grass, saturate(input.color.g));
                mixed = lerp(mixed, snow, saturate(input.color.r));
                mixed = lerp(mixed, _RoadColor.rgb, saturate(input.color.a));
                return fixed4(mixed, 1.0);
            }
            ENDCG
        }
    }

    Fallback "Standard"
}
