// Unbeleuchteter Vertexfarben-Shader. Das Licht ist in die Vertexfarben gebacken.
// Ohne LightMode-Tag läuft dieser Pass in der Built-in-, URP- und HDRP-Pipeline (SRPDefaultUnlit).
Shader "PixelKingdom/Flat"
{
    Properties
    {
        _Tint ("Tint", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Cull Back
        ZWrite On
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Tint;
            float4 _PKFogColor;
            float _PKFogStart;
            float _PKFogEnd;

            struct appdata { float4 vertex : POSITION; fixed4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; fixed4 color : COLOR; float fog : TEXCOORD0; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color * _Tint;
                #ifndef UNITY_COLORSPACE_GAMMA
                o.color.rgb = GammaToLinearSpace(o.color.rgb); // Vertexfarben sind sRGB
                #endif
                float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                float d = distance(wp, _WorldSpaceCameraPos);
                o.fog = saturate((d - _PKFogStart) / max(0.001, _PKFogEnd - _PKFogStart));
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed3 c = lerp(i.color.rgb, _PKFogColor.rgb, i.fog * i.fog);
                return fixed4(c, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
