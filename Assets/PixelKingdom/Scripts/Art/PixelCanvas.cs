using System;

namespace PixelKingdom
{
    /// <summary>Farbe als 4 Bytes (Unity-unabhängig, damit die Pixelkunst auch außerhalb von Unity testbar ist).</summary>
    public struct Px
    {
        public byte r, g, b, a;
        public Px(byte r, byte g, byte b, byte a = 255) { this.r = r; this.g = g; this.b = b; this.a = a; }

        public static readonly Px Clear = new Px(0, 0, 0, 0);
        /// <summary>Markierungsfarbe für Stellen, die in Fraktionsfarbe eingefärbt werden (Fahnen, Wappenröcke).</summary>
        public static readonly Px Tint = new Px(255, 0, 255);
        public static readonly Px TintDark = new Px(200, 0, 200);

        public static Px Hex(string h)
        {
            if (h[0] == '#') h = h.Substring(1);
            return new Px(Convert.ToByte(h.Substring(0, 2), 16), Convert.ToByte(h.Substring(2, 2), 16), Convert.ToByte(h.Substring(4, 2), 16));
        }

        public Px Mul(float f) => new Px(C(r * f), C(g * f), C(b * f), a);
        public Px Lerp(Px o, float t) => new Px(C(r + (o.r - r) * t), C(g + (o.g - g) * t), C(b + (o.b - b) * t), C(a + (o.a - a) * t));
        public Px Light(float t = 0.25f) => Lerp(new Px(255, 250, 235), t);
        public Px Dark(float t = 0.25f) => Lerp(new Px(30, 20, 40), t);
        public bool IsTint => a == 255 && g == 0 && r == b && (r == 255 || r == 200);
        public bool Same(Px o) => r == o.r && g == o.g && b == o.b && a == o.a;
        static byte C(float v) => (byte)(v < 0 ? 0 : v > 255 ? 255 : v + 0.5f);
    }

    /// <summary>Kleine Zeichenfläche für prozedurale Pixelkunst. Ursprung oben links.</summary>
    public class PixelCanvas
    {
        public readonly int W, H;
        public readonly Px[] P;

        public PixelCanvas(int w, int h) { W = w; H = h; P = new Px[w * h]; }

        public bool In(int x, int y) => x >= 0 && y >= 0 && x < W && y < H;
        public Px Get(int x, int y) => In(x, y) ? P[y * W + x] : Px.Clear;
        public void Set(int x, int y, Px c) { if (In(x, y)) P[y * W + x] = c; }
        public bool Opaque(int x, int y) => In(x, y) && P[y * W + x].a > 0;

        public void Rect(int x, int y, int w, int h, Px c)
        {
            for (int j = y; j < y + h; j++) for (int i = x; i < x + w; i++) Set(i, j, c);
        }

        public void Frame(int x, int y, int w, int h, Px c)
        {
            HLine(x, x + w - 1, y, c); HLine(x, x + w - 1, y + h - 1, c);
            VLine(x, y, y + h - 1, c); VLine(x + w - 1, y, y + h - 1, c);
        }

        public void HLine(int x0, int x1, int y, Px c) { for (int i = Math.Min(x0, x1); i <= Math.Max(x0, x1); i++) Set(i, y, c); }
        public void VLine(int x, int y0, int y1, Px c) { for (int j = Math.Min(y0, y1); j <= Math.Max(y0, y1); j++) Set(x, j, c); }

        public void Line(int x0, int y0, int x1, int y1, Px c)
        {
            int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1, dy = -Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1, err = dx + dy;
            while (true)
            {
                Set(x0, y0, c);
                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
        }

        public void Ellipse(float cx, float cy, float rx, float ry, Px c)
        {
            for (int y = (int)Math.Floor(cy - ry); y <= (int)Math.Ceiling(cy + ry); y++)
                for (int x = (int)Math.Floor(cx - rx); x <= (int)Math.Ceiling(cx + rx); x++)
                {
                    float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;
                    if (dx * dx + dy * dy <= 1f) Set(x, y, c);
                }
        }

        /// <summary>Gefülltes Dreieck.</summary>
        public void Tri(float x0, float y0, float x1, float y1, float x2, float y2, Px c)
        {
            int minX = (int)Math.Floor(Math.Min(x0, Math.Min(x1, x2))), maxX = (int)Math.Ceiling(Math.Max(x0, Math.Max(x1, x2)));
            int minY = (int)Math.Floor(Math.Min(y0, Math.Min(y1, y2))), maxY = (int)Math.Ceiling(Math.Max(y0, Math.Max(y1, y2)));
            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                {
                    float px = x + 0.5f, py = y + 0.5f;
                    float d0 = (x1 - x0) * (py - y0) - (y1 - y0) * (px - x0);
                    float d1 = (x2 - x1) * (py - y1) - (y2 - y1) * (px - x1);
                    float d2 = (x0 - x2) * (py - y2) - (y0 - y2) * (px - x2);
                    bool neg = d0 < 0 || d1 < 0 || d2 < 0, pos = d0 > 0 || d1 > 0 || d2 > 0;
                    if (!(neg && pos)) Set(x, y, c);
                }
        }

        /// <summary>Ersetzt nur bereits gesetzte Pixel (z.B. für Schattierung).</summary>
        public void Recolor(int x, int y, int w, int h, Func<Px, Px> f)
        {
            for (int j = y; j < y + h; j++) for (int i = x; i < x + w; i++) if (Opaque(i, j)) Set(i, j, f(Get(i, j)));
        }

        /// <summary>Zieht eine 1-Pixel-Kontur um alle sichtbaren Pixel (typischer Retro-Look).</summary>
        public void Outline(Px c)
        {
            var copy = (Px[])P.Clone();
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    if (copy[y * W + x].a > 0) continue;
                    bool n = false;
                    if (x > 0 && copy[y * W + x - 1].a > 0) n = true;
                    else if (x < W - 1 && copy[y * W + x + 1].a > 0) n = true;
                    else if (y > 0 && copy[(y - 1) * W + x].a > 0) n = true;
                    else if (y < H - 1 && copy[(y + 1) * W + x].a > 0) n = true;
                    if (n) P[y * W + x] = c;
                }
        }

        public void Blit(PixelCanvas src, int ox, int oy)
        {
            for (int y = 0; y < src.H; y++)
                for (int x = 0; x < src.W; x++)
                {
                    var p = src.P[y * src.W + x];
                    if (p.a > 0) Set(ox + x, oy + y, p);
                }
        }

        public PixelCanvas FlipX()
        {
            var c = new PixelCanvas(W, H);
            for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) c.P[y * W + x] = P[y * W + (W - 1 - x)];
            return c;
        }

        /// <summary>Schneidet leere Ränder ab; gibt die verschobene Zeichenfläche und den Versatz zurück.</summary>
        public PixelCanvas Trim(out int offX, out int offY)
        {
            int minX = W, minY = H, maxX = -1, maxY = -1;
            for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) if (P[y * W + x].a > 0) { if (x < minX) minX = x; if (x > maxX) maxX = x; if (y < minY) minY = y; if (y > maxY) maxY = y; }
            if (maxX < 0) { offX = offY = 0; return new PixelCanvas(1, 1); }
            var c = new PixelCanvas(maxX - minX + 1, maxY - minY + 1);
            for (int y = 0; y < c.H; y++) for (int x = 0; x < c.W; x++) c.P[y * c.W + x] = P[(y + minY) * W + x + minX];
            offX = minX; offY = minY;
            return c;
        }

        static uint Hash(int x, int y, int k)
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + k * 1442695041);
            h = (h ^ (h >> 13)) * 1274126177;
            return h ^ (h >> 16);
        }

        /// <summary>Deterministischer Zufall 0..1 pro Pixel.</summary>
        public static float Rnd(int x, int y, int k) => (Hash(x, y, k) & 0xFFFF) / 65535f;
    }
}
