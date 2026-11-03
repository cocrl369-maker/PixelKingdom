using System;

namespace PixelKingdom
{
    /// <summary>
    /// Malt den Kartenboden pixelgenau im Retro-Stil: weiche Küsten mit Brandung, geschwungene Flüsse,
    /// Hangschattierung (Licht von Nordosten), Klippenkanten an Geländestufen und Bodentexturen.
    /// Threadsicher (liest nur die Karte), damit Chunks im Hintergrund gemalt werden können.
    /// </summary>
    public class GroundPainter
    {
        public const int T = 16; // Pixel pro Kachel

        readonly WorldMap map;
        readonly ArtPalette p;
        readonly float[] th, land, dist;
        readonly byte[] riverMask;
        readonly float[] noise;
        readonly float[] mMask, mSnow, mDist;   // Gebirge: Maske, Schneegebiet, Abstand zum Rand (in Feldern)
        const int NW = 256;

        static readonly float[] Bayer = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };

        public GroundPainter(WorldMap m, ArtPalette pal)
        {
            map = m; p = pal;
            int W = WorldMap.W, H = WorldMap.H, N = W * H;
            th = new float[N]; land = new float[N]; dist = new float[N]; riverMask = new byte[N];
            for (int i = 0; i < N; i++)
            {
                bool water = m.IsWater(i);
                land[i] = water ? 0f : 1f;
                float t = (m.h[i] - WorldMap.Sea) / (1f - WorldMap.Sea);
                th[i] = water ? 0f : Math.Max(0f, t);
            }
            // Entfernung zum Land (für Wassertiefe)
            var q = new System.Collections.Generic.Queue<int>();
            for (int i = 0; i < N; i++) { dist[i] = 99; if (!m.IsWater(i)) { dist[i] = 0; q.Enqueue(i); } }
            while (q.Count > 0)
            {
                int c = q.Dequeue(); int cx = c % W, cy = c / W;
                for (int k = 0; k < 4; k++)
                {
                    int nx = cx + WorldMap.DX4[k], ny = cy + WorldMap.DY4[k];
                    if (!WorldMap.In(nx, ny)) continue;
                    int ni = ny * W + nx;
                    if (dist[ni] > dist[c] + 1) { dist[ni] = dist[c] + 1; q.Enqueue(ni); }
                }
            }
            // Flussverbindungen (Bit 0..3 = rechts, links, oben, unten)
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int i = y * W + x;
                    if (!m.river[i]) continue;
                    byte mask = 0;
                    for (int k = 0; k < 4; k++)
                    {
                        int nx = x + WorldMap.DX4[k], ny = y + WorldMap.DY4[k];
                        if (!WorldMap.In(nx, ny)) continue;
                        int ni = ny * W + nx;
                        if (m.river[ni] || m.IsWater(ni)) mask |= (byte)(1 << k);
                    }
                    riverMask[i] = mask;
                }
            // Gebirge: Abstand jedes Gebirgsfelds zum nächsten Nicht-Gebirgsfeld (Chamfer, 2 Durchläufe)
            mMask = new float[N]; mSnow = new float[N]; mDist = new float[N];
            for (int i = 0; i < N; i++)
            {
                bool mt = m.biome[i] == Biome.Mountain || m.biome[i] == Biome.Snow;
                mMask[i] = mt ? 1f : 0f; mSnow[i] = m.biome[i] == Biome.Snow ? 1f : 0f;
                mDist[i] = mt ? 99f : 0f;
            }
            const float D1 = 1f, D2 = 1.414f;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int i = y * W + x; if (mDist[i] == 0) continue;
                    float d = mDist[i];
                    if (x > 0) d = Math.Min(d, mDist[i - 1] + D1);
                    if (y > 0) d = Math.Min(d, mDist[i - W] + D1);
                    if (x > 0 && y > 0) d = Math.Min(d, mDist[i - W - 1] + D2);
                    if (x < W - 1 && y > 0) d = Math.Min(d, mDist[i - W + 1] + D2);
                    if (x == 0 || y == 0 || x == W - 1 || y == H - 1) d = Math.Min(d, 1f);
                    mDist[i] = d;
                }
            for (int y = H - 1; y >= 0; y--)
                for (int x = W - 1; x >= 0; x--)
                {
                    int i = y * W + x; if (mDist[i] == 0) continue;
                    float d = mDist[i];
                    if (x < W - 1) d = Math.Min(d, mDist[i + 1] + D1);
                    if (y < H - 1) d = Math.Min(d, mDist[i + W] + D1);
                    if (x < W - 1 && y < H - 1) d = Math.Min(d, mDist[i + W + 1] + D2);
                    if (x > 0 && y < H - 1) d = Math.Min(d, mDist[i + W - 1] + D2);
                    mDist[i] = d;
                }
            noise = new float[NW * NW];
            var r = new Random(1234);
            for (int i = 0; i < noise.Length; i++) noise[i] = (float)r.NextDouble();
        }

        // ------------------------------------------------------------------
        static float Bil(float[] a, float u, float v)
        {
            u -= 0.5f; v -= 0.5f;
            int x0 = (int)Math.Floor(u), y0 = (int)Math.Floor(v);
            float fx = u - x0, fy = v - y0;
            float A = At(a, x0, y0), B = At(a, x0 + 1, y0), C = At(a, x0, y0 + 1), D = At(a, x0 + 1, y0 + 1);
            return (A + (B - A) * fx) + ((C + (D - C) * fx) - (A + (B - A) * fx)) * fy;
        }

        static float At(float[] a, int x, int y)
        {
            if (x < 0) x = 0; if (y < 0) y = 0; if (x >= WorldMap.W) x = WorldMap.W - 1; if (y >= WorldMap.H) y = WorldMap.H - 1;
            return a[y * WorldMap.W + x];
        }

        float Noise(float x, float y)
        {
            int x0 = (int)Math.Floor(x), y0 = (int)Math.Floor(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
            float A = noise[(y0 & 255) * NW + (x0 & 255)], B = noise[(y0 & 255) * NW + ((x0 + 1) & 255)];
            float C = noise[((y0 + 1) & 255) * NW + (x0 & 255)], D = noise[((y0 + 1) & 255) * NW + ((x0 + 1) & 255)];
            return (A + (B - A) * fx) + ((C + (D - C) * fx) - (A + (B - A) * fx)) * fy;
        }

        static uint Hash(int x, int y)
        {
            uint h = (uint)(x * 374761393 + y * 668265263);
            h = (h ^ (h >> 13)) * 1274126177;
            return h ^ (h >> 16);
        }
        static float H01(int x, int y, int k) => (Hash(x + k * 7919, y - k * 104729) & 0xFFFF) / 65535f;

        float Height(float u, float v) => Bil(th, u, v);
        int Level(float u, float v) => (int)Math.Floor(Height(u, v) * 7.5f);

        // ------------------------------------------------------------------
        /// <summary>Malt einen Bereich. Ausgabe zeilenweise von unten (Unity-Texturformat).</summary>
        public void Paint(int tx0, int ty0, int tilesW, int tilesH, Px[] outPx)
        {
            int pw = tilesW * T, ph = tilesH * T;
            var grid = BuildMountGrid(tx0, ty0, tilesW, tilesH);
            for (int j = 0; j < ph; j++)
                for (int i = 0; i < pw; i++)
                    outPx[j * pw + i] = PaintPixel(tx0 * T + i, ty0 * T + j, grid);
        }

        /// <summary>Vorberechnetes Höhenfeld des Gebirges für einen Malbereich (pro Pixel einmal statt pro Strahlschritt).</summary>
        sealed class MountGrid
        {
            public int gx0, gy0, w, h;
            public float[] hv;
        }

        const int GridSouth = 8 * T, GridPad = 4;

        MountGrid BuildMountGrid(int tx0, int ty0, int tilesW, int tilesH)
        {
            // Nur nötig, wenn im Bereich (oder knapp daneben) Gebirge liegt
            bool any = false;
            for (int y = ty0 - 1; y <= ty0 + tilesH && !any; y++)
                for (int x = tx0 - 1; x <= tx0 + tilesW && !any; x++)
                    if (WorldMap.In(x, y) && mMask[y * WorldMap.W + x] > 0) any = true;
            if (!any) return null;
            var g = new MountGrid { gx0 = tx0 * T - GridPad, gy0 = ty0 * T - GridSouth - GridPad, w = tilesW * T + 2 * GridPad };
            g.h = tilesH * T + GridSouth + 2 * GridPad;
            g.hv = new float[g.w * g.h];
            for (int j = 0; j < g.h; j++)
            {
                float v = (g.gy0 + j + 0.5f) / T;
                for (int i = 0; i < g.w; i++)
                {
                    float u = (g.gx0 + i + 0.5f) / T;
                    g.hv[j * g.w + i] = Bil(mMask, u, v) > 0.05f ? MountH(u, v) : -1f;
                }
            }
            return g;
        }

        /// <summary>Höhe am Pixel (aus dem Raster, sonst direkt berechnet).</summary>
        float GH(MountGrid g, int gx, int gy)
        {
            if (g != null)
            {
                int i = gx - g.gx0, j = gy - g.gy0;
                if (i >= 0 && j >= 0 && i < g.w && j < g.h) return g.hv[j * g.w + i];
            }
            return MountH((gx + 0.5f) / T, (gy + 0.5f) / T);
        }

        public Px PaintPixel(int gx, int gy) => PaintPixel(gx, gy, null);

        Px PaintPixel(int gx, int gy, MountGrid grid)
        {
            float u = (gx + 0.5f) / T, v = (gy + 0.5f) / T;
            int tx = Math.Min(WorldMap.W - 1, Math.Max(0, (int)u)), ty = Math.Min(WorldMap.H - 1, Math.Max(0, (int)v));
            int ti = ty * WorldMap.W + tx;
            float bay = (Bayer[(gy & 3) * 4 + (gx & 3)] + 0.5f) / 16f;
            float n1 = Noise(u * 1.3f, v * 1.3f), n2 = Noise(u * 1.3f + 77, v * 1.3f + 31);

            // ---- Land oder Wasser? (weiche Küstenlinie) --------------------------
            float L = Bil(land, u, v) + (Noise(u * 1.7f + 13, v * 1.7f + 7) - 0.5f) * 0.5f + (Noise(u * 4.3f + 3, v * 4.3f + 91) - 0.5f) * 0.22f;
            bool isWater = L < 0.5f;
            if (isWater)
            {
                float dd = Bil(dist, u, v);
                if (L > 0.43f) return (bay < 0.6f) ? p.Foam : p.Shallow;
                Px wc = dd < 1.6f || L > 0.25f ? p.Shallow : dd < 3.5f ? p.Water : p.Deep;
                if (dd >= 1.6f && dd < 2.2f && bay < (2.2f - dd) * 1.6f) wc = p.Shallow;
                if (dd >= 3.5f && dd < 4.3f && bay < (4.3f - dd) * 1.2f) wc = p.Water;
                // Wellenkämme
                if (((gx / 3 + (gy / 5) * 7) % 23 == 0) && H01(gx / 6, gy / 3, 1) < 0.5f && dd > 1.5f) wc = wc.Light(0.25f);
                return wc;
            }

            // ---- Gebirge (füllt genau das Gebirgsgebiet, ragt in Schrägansicht auf) ------
            if (Bil(mMask, u, v) > 0.2f)
            {
                var mc = MountainPixel(gx, gy, u, v, bay, grid);
                if (mc.a != 0) return mc;
            }

            // ---- Fluss ------------------------------------------------------------------
            float riverD = RiverDist(u, v, out bool nearRiver);
            if (nearRiver)
            {
                float rw = 0.2f + (n1 - 0.5f) * 0.08f;
                if (riverD < rw - 0.06f) return (bay < 0.08f) ? p.River.Light(0.3f) : p.River;
                if (riverD < rw) return p.Shallow;
                if (riverD < rw + 0.08f) return p.Bank;
            }

            // ---- Biom (mit verwischten Grenzen) -------------------------------------------
            int jx = (int)Math.Floor(u + (n1 - 0.5f) * 0.7f), jy = (int)Math.Floor(v + (n2 - 0.5f) * 0.7f);
            jx = Math.Min(WorldMap.W - 1, Math.Max(0, jx)); jy = Math.Min(WorldMap.H - 1, Math.Max(0, jy));
            int bi = jy * WorldMap.W + jx;
            var b = map.biome[bi];
            if (map.IsWater(bi)) b = map.IsWater(ti) ? Biome.Beach : map.biome[ti];
            if (b == Biome.Lake || b == Biome.Ocean || b == Biome.Shallow) b = Biome.Beach;

            // Strand an Meeresküsten
            bool seaNear = Bil(dist, u, v) < 1.2f;
            if (L < 0.66f && seaNear && b != Biome.Snow && b != Biome.Mountain && b != Biome.Pine)
                b = Biome.Beach;

            // ---- Hangschattierung -------------------------------------------------------
            const float e = 0.08f;
            float sx = Height(u + e, v) - Height(u - e, v), sy = Height(u, v + e) - Height(u, v - e);
            float lum = -(sx + sy) * 18f;
            lum = Math.Max(-0.9f, Math.Min(0.9f, lum));

            // ---- Klippen an Geländestufen ------------------------------------------------
            if (b != Biome.Beach)
            {
                int lv = Level(u, v);
                if (lv > 0 && Height(u, v) > 0.3f)
                {
                    for (int k = 1; k <= 4; k++)
                    {
                        if (Level(u, v - k / (float)T) < lv)
                        {
                            // Klippenwand (k Pixel über der Kante)
                            Px cc = b == Biome.Snow ? p.Snow.Mul(0.8f) : p.Cliff;
                            if (k == 1) cc = p.CliffDark;
                            else if (((gx + (gy >> 1)) % 3) == 0) cc = cc.Mul(0.86f);
                            return cc;
                        }
                    }
                    // Oberkante (Lichtkante)
                    if (Level(u, v - 5f / T) < lv) lum += 0.6f;
                }
            }

            // ---- Grundfarbe & Textur ------------------------------------------------------
            Px baseC = BiomeColor(b);
            float tex = (H01(gx, gy, 3) - 0.5f) * 0.5f + (Noise(u * 4f, v * 4f) - 0.5f) * 0.8f;
            float val = lum * 1.6f + tex + (bay - 0.5f) * 0.9f;
            Px c = val > 0.75f ? baseC.Light(0.14f) : val < -0.75f ? baseC.Dark(0.16f) : baseC;

            float h = H01(gx, gy, 9);
            switch (b)
            {
                case Biome.Grass:
                case Biome.Hills:
                    if (h < 0.035f) c = baseC.Dark(0.25f);
                    else if (h < 0.05f && H01(gx, gy - 1, 9) >= 0.035f) c = baseC.Light(0.22f);
                    break;
                case Biome.Fertile:
                    if (h < 0.01f) c = p.Flower1; else if (h < 0.018f) c = p.Flower2; else if (h < 0.05f) c = baseC.Dark(0.2f);
                    break;
                case Biome.Forest:
                case Biome.Pine:
                    if (h < 0.08f) c = baseC.Dark(0.28f);
                    break;
                case Biome.Mountain:
                    if (h < 0.06f) c = p.RockDark; else if (h < 0.1f) c = p.RockLight;
                    break;
                case Biome.Snow:
                    if (lum < -0.15f && bay < 0.6f) c = p.Snow.Lerp(new Px(160, 185, 215), 0.35f);
                    break;
                case Biome.Desert:
                    if (((gx + gy * 3 + (int)(Noise(u * 2, v * 2) * 12)) % 9) == 0) c = baseC.Dark(0.12f);
                    break;
                case Biome.Steppe:
                    if (h < 0.04f) c = baseC.Light(0.2f); else if (h < 0.07f) c = baseC.Dark(0.2f);
                    break;
                case Biome.Beach:
                    if (h < 0.006f) c = new Px(240, 235, 225);
                    break;
            }
            return c;
        }

        Px BiomeColor(Biome b)
        {
            switch (b)
            {
                case Biome.Grass: return p.Grass;
                case Biome.Fertile: return p.Fertile;
                case Biome.Forest: return p.ForestFloor;
                case Biome.Pine: return p.PineFloor;
                case Biome.Hills: return p.Hills;
                case Biome.Mountain: return p.Hills;   // Fuß des Gebirges: felsige Wiese, kein dunkler Untergrund
                case Biome.Snow: return p.Snow;
                case Biome.Desert: return p.Desert;
                case Biome.Steppe: return p.Steppe;
                case Biome.Beach: return p.Beach;
            }
            return p.Grass;
        }

        // ------------------------------------------------------------------
        //  Gebirge: Höhenfeld aus Abstand zum Rand (Hauptgrat entlang der Kette) + Grat-Rauschen
        //  (Seitengrate, Schluchten). Die Höhe ist nie größer als der Abstand zum Rand – so ragen
        //  die Berge in der Schrägansicht nie über ihr Gebiet hinaus.
        // ------------------------------------------------------------------
        const float Rise = 0.72f;     // Höhe -> Bildversatz nach oben (in Feldern)
        const float Edge = 0.56f;    // Schwelle der (verwischten) Gebirgsmaske

        float Ridged(float x, float y) { float n = 1f - Math.Abs(Noise(x, y) * 2f - 1f); return n * n; }

        float MountMask(float u, float v) => Bil(mMask, u, v) + (Noise(u * 1.9f + 5, v * 1.9f + 11) - 0.5f) * 0.34f + (Noise(u * 5f + 2, v * 5f + 40) - 0.5f) * 0.1f;

        /// <summary>Höhe des Gebirges an einem Bodenpunkt (in Feldern), &lt; 0 außerhalb.</summary>
        float MountH(float u, float v)
        {
            float mv = MountMask(u, v);
            if (mv < Edge) return -1f;
            float e = Math.Min(1f, (mv - Edge) * 5f);
            float d = Bil(mDist, u, v);
            // Gipfel entlang des Hauptgrats, Seitengrate mit Schluchten dazwischen, feine Felsrippen
            float pk = Noise(u * 0.55f + 3, v * 0.55f + 9);
            float peaks = 0.28f + 1.3f * (float)Math.Pow(pk, 2.4);
            float spurs = Ridged(u * 1.25f + 21, v * 1.25f + 2);
            float ribs = Ridged(u * 3.3f + 7, v * 3.3f + 70);
            float h = (float)Math.Pow(d, 0.95) * peaks * (0.62f + 0.5f * spurs) + ribs * 0.16f;
            h *= e;
            return Math.Min(h, Math.Max(0f, d - 0.5f) * 1.05f);
        }

        /// <summary>Welcher Bodenpunkt ist an dieser Bildstelle zu sehen? (Höhenstrahl von Süden, in Pixeln)</summary>
        bool MountHit(MountGrid g, int gx, int gy, out int hitY, out float hitH)
        {
            hitY = gy; hitH = 0;
            float u = (gx + 0.5f) / T, vs = (gy + 0.5f) / T;
            int tMax = (int)(Math.Min(7.5f, Bil(mDist, u, vs) + 1.5f) * Rise * T);
            for (int t = tMax; t >= 0; t--)
            {
                float h = GH(g, gx, gy - t);
                if (h >= 0 && h * Rise * T >= t) { hitY = gy - t; hitH = h; return true; }
            }
            return false;
        }

        Px MountainPixel(int gx, int gy, float u, float v, float bay, MountGrid g)
        {
            if (!MountHit(g, gx, gy, out int hy, out float h)) return default;
            int t = gy - hy;
            float hv = (hy + 0.5f) / T;
            // Kontur: Silhouette gegen dahinterliegendes Gelände (der Bildpunkt darüber sieht weiter nach Norden)
            bool contour = true;
            for (int k = 1; k <= 3 && contour; k++)
            {
                float hh = GH(g, gx, hy + k);
                if (hh >= 0 && hh * Rise * T >= t + 1 - k - 0.2f) contour = false;
            }
            float mv = MountMask(u, hv);
            if (mv < Edge + 0.035f && t < 3) contour = true;   // Fußlinie am Rand
            if (contour) return p.MountD.Dark(0.25f);

            // Licht von links (wie im Vorbild): Westhänge hell, Osthänge dunkel, Vorderseite mittel
            float hx = Sample(g, gx + 1, hy, h) - Sample(g, gx - 1, hy, h), hyd = Sample(g, gx, hy + 1, h) - Sample(g, gx, hy - 1, h);
            float nx = -hx * T / 2f * 0.55f, ny = -hyd * T / 2f * 0.55f;
            float len = (float)Math.Sqrt(nx * nx + ny * ny + 1f);
            float lum = (nx * -0.78f + ny * -0.12f + 0.62f) / len;
            float tex = (Ridged(u * 3.6f + 1, hv * 1.5f + 2) - 0.45f) * 0.16f + (H01(gx >> 1, gy >> 1, 21) - 0.5f) * 0.05f;
            float val = lum + tex + (bay - 0.5f) * 0.07f;

            // Schnee auf den Höhen (im Schneegebiet tiefer)
            float snowLine = p.SnowLine - Bil(mSnow, u, hv) * 1.1f + (Noise(u * 2.7f + 31, hv * 2.7f) - 0.5f) * 0.9f;
            float sd = h - snowLine;
            if (sd > 0.12f || (sd > -0.08f && bay < (sd + 0.08f) * 4f))
            {
                // Felsrippen schauen in steilen Schattenhängen heraus
                if (val < 0.46f && H01(gx, gy >> 1, 23) < 0.16f) return p.MountB;
                return val > 0.62f ? p.CapSnow : val > 0.46f ? p.CapSnow.Lerp(p.CapGrey, 0.5f) : p.CapGrey;
            }
            // vereinzelte grüne Flecken (Latschen, Almwiesen) an mittleren Hängen
            if (h > 0.5f && h < snowLine - 0.7f && Noise(u * 2.1f + 50, hv * 2.1f + 3) > 0.74f)
                return val > 0.66f ? p.PineLight : val > 0.42f ? p.Pine : p.PineDark;
            return val > 0.74f ? p.MountC : val > 0.55f ? p.MountA : val > 0.36f ? p.MountB : p.MountD;
        }

        float Sample(MountGrid g, int gx, int gy, float fallback) { float h = GH(g, gx, gy); return h < 0 ? 0 : h; }

        float RiverDist(float u, float v, out bool near)
        {
            near = false;
            int cx = (int)u, cy = (int)v;
            float best = 9f;
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int x = cx + dx, y = cy + dy;
                    if (!WorldMap.In(x, y)) continue;
                    int i = y * WorldMap.W + x;
                    if (!map.river[i]) continue;
                    near = true;
                    float ox = x + 0.5f, oy = y + 0.5f;
                    byte m = riverMask[i];
                    // Kurven: zwei senkrechte Verbindungen -> Viertelkreis um die gemeinsame Ecke
                    int bits = 0, ka = -1, kb = -1;
                    for (int k = 0; k < 4; k++) if ((m & (1 << k)) != 0) { bits++; if (ka < 0) ka = k; else kb = k; }
                    if (bits >= 3)
                    {
                        // Flusskreuzung/-knäuel: als kleiner Teich zeichnen
                        float dc = (float)Math.Sqrt((u - ox) * (u - ox) + (v - oy) * (v - oy));
                        best = Math.Min(best, Math.Max(0, dc - 0.38f));
                        continue;
                    }
                    if (bits == 2 && (ka < 2) != (kb < 2))
                    {
                        float qx = ox + (WorldMap.DX4[ka] + WorldMap.DX4[kb]) * 0.5f, qy = oy + (WorldMap.DY4[ka] + WorldMap.DY4[kb]) * 0.5f;
                        float dq = (float)Math.Sqrt((u - qx) * (u - qx) + (v - qy) * (v - qy));
                        if ((u - qx) * (ox - qx) >= 0 && (v - qy) * (oy - qy) >= 0) best = Math.Min(best, Math.Abs(dq - 0.5f));
                        continue;
                    }
                    bool any = false;
                    for (int k = 0; k < 4; k++)
                    {
                        if ((m & (1 << k)) == 0) continue;
                        any = true;
                        float ex = ox + WorldMap.DX4[k] * 0.5f, ey = oy + WorldMap.DY4[k] * 0.5f;
                        best = Math.Min(best, SegDist(u, v, ox, oy, ex, ey));
                    }
                    if (!any) best = Math.Min(best, (float)Math.Sqrt((u - ox) * (u - ox) + (v - oy) * (v - oy)));
                }
            return best;
        }

        static float SegDist(float px, float py, float ax, float ay, float bx, float by)
        {
            float vx = bx - ax, vy = by - ay;
            float t = ((px - ax) * vx + (py - ay) * vy) / (vx * vx + vy * vy);
            t = t < 0 ? 0 : t > 1 ? 1 : t;
            float dx = ax + vx * t - px, dy = ay + vy * t - py;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
