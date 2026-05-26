Shader "Custom/OutlineSprite"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _OutlineColor ("Outline Color", Color) = (0.2, 0.1, 0.05, 1)
        _OutlineWidth ("Outline Width", Float) = 0.003
        _OutlineNoise ("Outline Noise", Float) = 0.001
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

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
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;
            float4 _OutlineColor;
            float _OutlineWidth;
            float _OutlineNoise;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                return o;
            }

            float noise(float2 uv)
            {
                return frac(sin(dot(uv, float2(127.1, 311.7))) * 43758.5453);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                fixed4 col = tex2D(_MainTex, uv);

                float w = _OutlineWidth;
                float n = (noise(uv * 100) - 0.5) * _OutlineNoise;

                float a = 0;
                a += tex2D(_MainTex, uv + float2( w+n,  0  )).a;
                a += tex2D(_MainTex, uv + float2(-w+n,  0  )).a;
                a += tex2D(_MainTex, uv + float2( 0,    w+n)).a;
                a += tex2D(_MainTex, uv + float2( 0,   -w+n)).a;
                a += tex2D(_MainTex, uv + float2( w+n,  w+n)).a;
                a += tex2D(_MainTex, uv + float2(-w+n,  w+n)).a;
                a += tex2D(_MainTex, uv + float2( w+n, -w+n)).a;
                a += tex2D(_MainTex, uv + float2(-w+n, -w+n)).a;

                float outline = clamp(a, 0, 1) * (1 - col.a);

                fixed4 result = lerp(col, _OutlineColor, outline);
                result.a = clamp(col.a + outline, 0, 1);
                return result * i.color;
            }
            ENDCG
        }
    }
}
