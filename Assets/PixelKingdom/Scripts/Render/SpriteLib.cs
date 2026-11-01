using System.Collections.Generic;
using UnityEngine;

namespace PixelKingdom
{
    public class SpriteInfo
    {
        public Rect UV;
        public int W, H, PivotX, PivotY;
        public bool Flat;
        public SpriteInfo Shadow, Overlay;
    }

    /// <summary>Packt alle prozeduralen Sprites und Icons in einen Atlas (eine Textur, ein Material).</summary>
    public static class SpriteLib
    {
        public static Texture2D Atlas;
        static readonly Dictionary<string, SpriteInfo> sprites = new Dictionary<string, SpriteInfo>();
        static int builtFor = -1;

        public static void Build(int paletteIndex)
        {
            if (Atlas != null && builtFor == paletteIndex) return;
            builtFor = paletteIndex;
            sprites.Clear();
            var art = SpriteArt.Build(ArtPalette.All[paletteIndex]);
            var icons = IconArt.Build();

            // Alle Bilder sammeln
            var items = new List<(string key, PixelCanvas c)>();
            foreach (var kv in art)
            {
                items.Add((kv.Key, kv.Value.C));
                if (kv.Value.Shadow != null) items.Add((kv.Key + "#s", kv.Value.Shadow));
                if (kv.Value.Overlay != null) items.Add((kv.Key + "#o", kv.Value.Overlay));
            }
            foreach (var kv in icons) items.Add((kv.Key, kv.Value));
            items.Sort((a, b) => b.c.H.CompareTo(a.c.H));

            // Regal-Packen
            const int AW = 1024, pad = 2;
            var pos = new Dictionary<string, Vector2Int>();
            int x = pad, y = pad, rowH = 0;
            foreach (var it in items)
            {
                if (x + it.c.W + pad > AW) { x = pad; y += rowH + pad; rowH = 0; }
                pos[it.key] = new Vector2Int(x, y);
                x += it.c.W + pad;
                rowH = Mathf.Max(rowH, it.c.H);
            }
            int AH = Mathf.NextPowerOfTwo(y + rowH + pad);
            var px = new Color32[AW * AH];
            foreach (var it in items)
            {
                var p0 = pos[it.key];
                for (int j = 0; j < it.c.H; j++)
                    for (int i = 0; i < it.c.W; i++)
                    {
                        var p = it.c.P[j * it.c.W + i];
                        if (p.a == 0) continue;
                        int ty = AH - 1 - (p0.y + j); // Textur von unten
                        px[ty * AW + p0.x + i] = new Color32(p.r, p.g, p.b, p.a);
                    }
            }
            if (Atlas != null) Object.Destroy(Atlas);
            Atlas = new Texture2D(AW, AH, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "PixelKingdomAtlas" };
            Atlas.SetPixels32(px);
            Atlas.Apply(false, false);

            SpriteInfo Info(string key, PixelCanvas c, int pvx, int pvy, bool flat)
            {
                var p0 = pos[key];
                var uv = new Rect(p0.x / (float)AW, (AH - p0.y - c.H) / (float)AH, c.W / (float)AW, c.H / (float)AH);
                return new SpriteInfo { UV = uv, W = c.W, H = c.H, PivotX = pvx, PivotY = pvy, Flat = flat };
            }
            foreach (var kv in art)
            {
                var a = kv.Value;
                var si = Info(kv.Key, a.C, a.PivotX, a.PivotY, a.Flat);
                if (a.Shadow != null) si.Shadow = Info(kv.Key + "#s", a.Shadow, a.ShadowPivotX, a.ShadowPivotY, true);
                if (a.Overlay != null) si.Overlay = Info(kv.Key + "#o", a.Overlay, a.PivotX, a.PivotY, a.Flat);
                sprites[kv.Key] = si;
            }
            foreach (var kv in icons) sprites[kv.Key] = Info(kv.Key, kv.Value, kv.Value.W / 2, 0, true);
        }

        public static SpriteInfo Get(string name) => name != null && sprites.TryGetValue(name, out var s) ? s : null;
        public static bool Has(string name) => name != null && sprites.ContainsKey(name);

        /// <summary>Zeichnet ein Sprite in der Oberfläche (1 Texel = 1 virtuelles Pixel, bei Bedarf verkleinert).</summary>
        public static bool DrawUI(Rect r, string name, Color? tint = null, bool fit = true, bool upscale = false)
        {
            var s = Get(name);
            if (s == null || Atlas == null) return false;
            if (Event.current.type != EventType.Repaint) return true;
            float scale = 1f;
            float maxScale = Mathf.Min(r.width / s.W, r.height / s.H);
            if (maxScale < 1f) scale = maxScale;
            else if (upscale) scale = Mathf.Floor(maxScale);
            if (!fit) scale = maxScale;
            float w = s.W * scale, h = s.H * scale;
            var dst = new Rect(Mathf.Round(r.x + (r.width - w) / 2), Mathf.Round(r.y + (r.height - h) / 2), w, h);
            var old = GUI.color;
            GUI.color = tint ?? Color.white;
            GUI.DrawTextureWithTexCoords(dst, Atlas, s.UV);
            if (s.Overlay != null)
            {
                GUI.color = tint ?? Palette.FactionColor(0);
                GUI.DrawTextureWithTexCoords(dst, Atlas, s.Overlay.UV);
            }
            GUI.color = old;
            return true;
        }
    }
}
