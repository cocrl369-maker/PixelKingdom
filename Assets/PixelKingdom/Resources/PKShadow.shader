// Schlagschatten: halbtransparent, per Stencil nur einmal pro Pixel (keine doppelt dunklen Überlappungen).
Shader "PixelKingdom/Shadow"
{
    Properties { _MainTex ("Atlas", 2D) = "white" {} _Color ("Farbe", Color) = (0.08, 0.12, 0.3, 0.32) }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent-10" }
        Cull Off ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Stencil { Ref 7 Comp NotEqual Pass Replace }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "PKCommon.cginc"

            sampler2D _MainTex;
            fixed4 _Color;
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float2 world : TEXCOORD1; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.world = mul(unity_ObjectToWorld, v.vertex).xy;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                clip(tex2D(_MainTex, i.uv).a - 0.5);
                PKFog(fixed4(0, 0, 0, 1), i.world, true);
                return _Color;
            }
            ENDCG
        }
    }
    Fallback Off
}
