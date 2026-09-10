Shader "Custom/ForestGrassInstanced"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.2, 0.52, 0.22, 1)
        _TipColor ("Tip Color", Color) = (0.62, 0.88, 0.4, 1)
        _WindStrength ("Wind Strength", Float) = 0.08
        _WindSpeed ("Wind Speed", Float) = 1.65
        _BendStrength ("Bend Strength", Float) = 0.16
        _WindTimeOffset ("Wind Time Offset", Float) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        Cull Off
        ZWrite On

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half3 color : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _TipColor;
                float _WindStrength;
                float _WindSpeed;
                float _BendStrength;
                float _WindTimeOffset;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);

                Varyings output;
                float3 positionOS = input.positionOS.xyz;

                VertexPositionInputs worldInputs = GetVertexPositionInputs(positionOS);
                float phase = worldInputs.positionWS.x * 0.23 + worldInputs.positionWS.z * 0.17 + _WindTimeOffset;
                float heightMask = saturate(input.uv.y);
                float bendMask = heightMask * heightMask;
                float sway = sin(_Time.y * _WindSpeed + phase) * _WindStrength * bendMask;
                float flutter = cos(_Time.y * (_WindSpeed * 1.37) + phase * 1.31) * (_WindStrength * 0.32) * bendMask;
                float forwardLean = _BendStrength * bendMask;

                positionOS.x += sway;
                positionOS.z += flutter + forwardLean;

                VertexPositionInputs positionInputs = GetVertexPositionInputs(positionOS);
                output.positionHCS = positionInputs.positionCS;
                output.uv = input.uv;
                half variation = 0.92h + 0.08h * sin(phase * 1.73h);
                output.color = lerp(_BaseColor.rgb, _TipColor.rgb, sqrt(heightMask)) * variation;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return half4(input.color, 1.0);
            }
            ENDHLSL
        }
    }

    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        Cull Off
        ZWrite On

        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            fixed4 _BaseColor;
            fixed4 _TipColor;
            float _WindStrength;
            float _WindSpeed;
            float _BendStrength;
            float _WindTimeOffset;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed3 color : TEXCOORD1;
            };

            v2f vert(appdata input)
            {
                UNITY_SETUP_INSTANCE_ID(input);

                v2f output;
                float3 positionOS = input.vertex.xyz;
                float3 worldPos = mul(unity_ObjectToWorld, float4(positionOS, 1.0)).xyz;
                float phase = worldPos.x * 0.23 + worldPos.z * 0.17 + _WindTimeOffset;
                float heightMask = saturate(input.uv.y);
                float bendMask = heightMask * heightMask;
                float sway = sin(_Time.y * _WindSpeed + phase) * _WindStrength * bendMask;
                float flutter = cos(_Time.y * (_WindSpeed * 1.37) + phase * 1.31) * (_WindStrength * 0.32) * bendMask;
                float forwardLean = _BendStrength * bendMask;

                positionOS.x += sway;
                positionOS.z += flutter + forwardLean;

                output.vertex = UnityObjectToClipPos(float4(positionOS, 1.0));
                output.uv = input.uv;
                fixed variation = 0.92 + 0.08 * sin(phase * 1.73);
                output.color = lerp(_BaseColor.rgb, _TipColor.rgb, sqrt(heightMask)) * variation;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                return fixed4(input.color, 1.0);
            }
            ENDCG
        }
    }
}
