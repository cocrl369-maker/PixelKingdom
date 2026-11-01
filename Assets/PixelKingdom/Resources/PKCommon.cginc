#ifndef PK_COMMON
#define PK_COMMON
sampler2D _PKFogTex;
sampler2D _PKOwnerTex;
float4 _PKMapSize;      // (Breite, Höhe, 1/Breite, 1/Höhe) in Kacheln
float4 _PKParchA;
float4 _PKParchB;
float4 _PKInk;
float _PKFogOn;

float PKBayer(float2 p)
{
    int x = (int)fmod(p.x, 4.0);
    int y = (int)fmod(p.y, 4.0);
    int idx = y * 4 + x;
    float m[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
    return (m[idx] + 0.5) / 16.0;
}

// Liefert die Farbe – oder Pergament, wenn das Gebiet unerforscht ist.
// hide = true: Objekte im Nebel werden verworfen.
fixed4 PKFog(fixed4 c, float2 world, bool hide)
{
    if (_PKFogOn < 0.5) return c;
    float fog = tex2D(_PKFogTex, world * _PKMapSize.zw).r;
    float2 tp = floor(world * 16.0);
    float b = PKBayer(tp);
    float thr = 0.5 + (b - 0.5) * 0.35;
    if (fog < thr)
    {
        if (hide) discard;
        float n = frac(sin(dot(floor(tp / 3.0), float2(12.9898, 78.233))) * 43758.5453);
        fixed4 pc = n > 0.82 ? _PKParchB : _PKParchA;
        if (fog > 0.24) pc = _PKInk;
        return fixed4(pc.rgb, 1);
    }
    return c;
}
#endif
