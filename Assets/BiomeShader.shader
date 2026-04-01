Shader "Custom/BiomeShader"
{
    Properties
    {
        _SandTex ("Sand", 2D) = "white" {}
        _GrassTex ("Grass", 2D) = "white" {}
        _SnowTex ("Snow", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
            };

            sampler2D _SandTex;
            sampler2D _GrassTex;
            sampler2D _SnowTex;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv * 20; // tiling
                o.color = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float4 sand = tex2D(_SandTex, i.uv);
                float4 grass = tex2D(_GrassTex, i.uv);
                float4 snow = tex2D(_SnowTex, i.uv);

                float3 mix1 = lerp(sand.rgb, grass.rgb, i.color.g);
                float3 finalColor = lerp(mix1, snow.rgb, i.color.r);

                return float4(finalColor, 1);
            }
            ENDCG
        }
    }
}