// Kartenboden: Chunk-Textur + Gebietstönung + Nebel des Krieges (Pergament mit gerasterter Kante).
// Ohne LightMode-Tag: läuft in Built-in und URP.
Shader "PixelKingdom/Ground"
{
    Properties { _MainTex ("Textur", 2D) = "white" {} }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Cull Off ZWrite On
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "PKCommon.cginc"

            sampler2D _MainTex;
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
                fixed4 c = tex2D(_MainTex, i.uv);
                fixed4 own = tex2D(_PKOwnerTex, i.world * _PKMapSize.zw);
                c.rgb = lerp(c.rgb, own.rgb, own.a * 0.2);
                return PKFog(c, i.world, false);
            }
            ENDCG
        }
    }
    Fallback Off
}
