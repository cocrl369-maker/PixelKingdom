using System;
using System.Collections.Generic;

namespace PixelKingdom
{
    /// <summary>
    /// Erzeugt eine glaubwürdige Welt: Kontinente, Gebirgszüge, Flüsse, Seen, Inseln, Klimazonen.
    /// Reiner C#-Code (ohne Unity), damit die Welt aus dem Seed immer exakt gleich entsteht.
    /// </summary>
    public static class WorldGenerator
    {
        struct Blob { public float x, y, rx, ry, w; public byte cont; }

        public static WorldMap Generate(int seed)
        {
            var map = new WorldMap();
            var rnd = new Random(seed);
            var n1 = new Noise(seed);
            var n2 = new Noise(seed * 31 + 7);
            var n3 = new Noise(seed * 17 + 3);
            var n4 = new Noise(seed * 13 + 11);
            int W = WorldMap.W, H = WorldMap.H;

            // --- Kontinent-Formen --------------------------------------------------
            var blobs = new List<Blob>();
            void B(float x, float y, float rx, float ry, byte c, float w = 1f) =>
                blobs.Add(new Blob { x = x + Jit(rnd, 6), y = y + Jit(rnd, 6), rx = rx, ry = ry, cont = c, w = w });

            // Mittland (Heimat) – groß, mit Halbinseln
            B(100, 112, 78, 62, 0); B(62, 150, 46, 42, 0); B(145, 78, 50, 44, 0); B(130, 150, 42, 36, 0);
            B(48, 72, 36, 30, 0); B(170, 125, 30, 40, 0, 0.9f); B(90, 50, 30, 22, 0, 0.85f);
            // Nordlande (kalt, oben rechts)
            B(266, 182, 46, 30, 1); B(238, 196, 28, 20, 1); B(292, 160, 22, 24, 1, 0.9f);
            // Südreich (warm, unten rechts)
            B(264, 52, 48, 34, 2); B(292, 82, 24, 26, 2); B(236, 34, 26, 18, 2, 0.9f);
            // Inselbund (Archipel in der Mitte)
            for (int i = 0; i < 12; i++)
                B(208 + Jit(rnd, 16), 82 + (float)rnd.NextDouble() * 70, 4 + (float)rnd.NextDouble() * 7, 4 + (float)rnd.NextDouble() * 6, 3, 0.95f);

            float ox = (float)rnd.NextDouble() * 1000, oy = (float)rnd.NextDouble() * 1000;

            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int i = WorldMap.Idx(x, y);
                    // Domain warp für organische Küsten
                    float wx = x + (n2.Fbm(x * 0.02f + ox, y * 0.02f, 3) - 0.5f) * 34f;
                    float wy = y + (n2.Fbm(x * 0.02f, y * 0.02f + oy, 3) - 0.5f) * 34f;

                    float mask = -1f; byte bestCont = 0;
                    foreach (var b in blobs)
                    {
                        float dx = (wx - b.x) / b.rx, dy = (wy - b.y) / b.ry;
                        float v = (1f - (float)Math.Sqrt(dx * dx + dy * dy)) * b.w;
                        if (v > mask) { mask = v; bestCont = b.cont; }
                    }
                    // Kartenrand ist immer Meer
                    float edge = Math.Min(Math.Min(x, W - 1 - x), Math.Min(y, H - 1 - y)) / 10f;
                    if (edge < 1f) mask -= (1f - edge) * 0.8f;

                    float baseN = n1.Fbm(x * 0.035f + ox, y * 0.035f + oy, 5);
                    float e = 0.37f + Clamp01(mask) * 0.17f + (baseN - 0.5f) * 0.32f + Math.Min(0, mask) * 0.6f;

                    // Gebirgszüge (ridged noise), nur im Landesinneren
                    float ridge = n3.Ridged(x * 0.028f + ox * 0.5f, y * 0.028f, 4);
                    float inland = Clamp01(mask * 2.2f);
                    e += (float)Math.Pow(ridge, 3.2) * 0.62f * inland;

                    map.h[i] = e;
                    map.cont[i] = bestCont;
                }

            // --- Klima ---------------------------------------------------------------
            var temp = new float[W * H];
            var moist = new float[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int i = WorldMap.Idx(x, y);
                    float lat = (float)y / H;  // 0 = Süden (warm), 1 = Norden (kalt)
                    float t = 1f - lat * 1.05f + (n4.Fbm(x * 0.03f, y * 0.03f, 3) - 0.5f) * 0.25f;
                    t -= Math.Max(0, map.h[i] - WorldMap.Sea) * 0.7f;
                    if (map.cont[i] == 2) t += 0.14f;      // Südreich: heiß & trocken
                    else if (map.cont[i] == 1) t -= 0.12f; // Nordlande: rau
                    temp[i] = t;
                    moist[i] = n4.Fbm(x * 0.025f + 300, y * 0.025f + 300, 4) - (map.cont[i] == 2 ? 0.08f : 0f);
                }

            // --- Wasser klassifizieren --------------------------------------------------
            for (int i = 0; i < W * H; i++)
                map.biome[i] = map.h[i] < WorldMap.Sea ? Biome.Ocean : Biome.Grass;

            // Binnenseen: Wasserflächen, die nicht mit dem Kartenrand verbunden sind
            MarkLakes(map);

            // --- Flüsse --------------------------------------------------------------
            int rivers = 0;
            for (int attempt = 0; attempt < 600 && rivers < 46; attempt++)
            {
                int sx = rnd.Next(4, W - 4), sy = rnd.Next(4, H - 4);
                int si = WorldMap.Idx(sx, sy);
                if (map.IsWater(si) || map.h[si] < 0.62f || map.h[si] > 0.9f || map.river[si]) continue;
                if (temp[si] > 0.78f && map.cont[si] == 2 && rnd.NextDouble() < 0.6) continue; // Wüste: wenig Flüsse
                if (TryRiver(map, sx, sy, rnd)) rivers++;
            }

            // --- Flachwasser -----------------------------------------------------------
            var dist = new int[W * H];
            var q = new Queue<int>();
            for (int i = 0; i < W * H; i++)
            {
                dist[i] = int.MaxValue;
                if (!map.IsWater(i)) { dist[i] = 0; q.Enqueue(i); }
            }
            while (q.Count > 0)
            {
                int c = q.Dequeue(); int cx = WorldMap.X(c), cy = WorldMap.Y(c);
                for (int k = 0; k < 4; k++)
                {
                    int nx = cx + WorldMap.DX4[k], ny = cy + WorldMap.DY4[k];
                    if (!WorldMap.In(nx, ny)) continue;
                    int ni = WorldMap.Idx(nx, ny);
                    if (dist[ni] > dist[c] + 1) { dist[ni] = dist[c] + 1; q.Enqueue(ni); }
                }
            }
            for (int i = 0; i < W * H; i++)
                if (map.biome[i] == Biome.Ocean && (dist[i] <= 2 || map.h[i] > WorldMap.Sea - 0.035f)) map.biome[i] = Biome.Shallow;

            // --- Biome an Land --------------------------------------------------------
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int i = WorldMap.Idx(x, y);
                    if (map.IsWater(i)) continue;
                    float e = map.h[i], t = temp[i], m = moist[i];
                    bool nearOcean = false, nearFresh = false;
                    for (int k = 0; k < 8; k++)
                    {
                        int nx = x + WorldMap.DX8[k], ny = y + WorldMap.DY8[k];
                        if (!WorldMap.In(nx, ny)) continue;
                        int ni = WorldMap.Idx(nx, ny);
                        if (map.IsSea(ni)) nearOcean = true;
                        if (map.biome[ni] == Biome.Lake || map.river[ni]) nearFresh = true;
                    }
                    if (map.river[i]) nearFresh = true;

                    Biome b;
                    if (e > 0.90f || (e > 0.84f && t < 0.25f)) b = Biome.Snow;
                    else if (e > 0.77f) b = Biome.Mountain;
                    else if (e > 0.67f) b = Biome.Hills;
                    else if (nearOcean && e < WorldMap.Sea + 0.035f) b = t > 0.25f ? Biome.Beach : Biome.Grass;
                    else if (t > 0.74f && m < 0.55f && !nearFresh) b = Biome.Desert;
                    else if (t > 0.70f && m < 0.6f) b = Biome.Steppe;
                    else if (m > 0.54f) b = t < 0.32f ? Biome.Pine : Biome.Forest;
                    else if (nearFresh || m > 0.47f) b = t < 0.2f ? Biome.Steppe : Biome.Fertile;
                    else b = t < 0.25f ? Biome.Pine : Biome.Grass;
                    map.biome[i] = b;
                }

            // Fruchtbares Schwemmland rund um Flüsse
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int i = WorldMap.Idx(x, y);
                    if (!map.river[i]) continue;
                    for (int k = 0; k < 8; k++)
                    {
                        int nx = x + WorldMap.DX8[k], ny = y + WorldMap.DY8[k];
                        if (!WorldMap.In(nx, ny)) continue;
                        int ni = WorldMap.Idx(nx, ny);
                        if (map.biome[ni] == Biome.Grass || map.biome[ni] == Biome.Steppe) map.biome[ni] = Biome.Fertile;
                    }
                }

            // --- Küsten & Bäume ---------------------------------------------------------
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int i = WorldMap.Idx(x, y);
                    if (map.IsWater(i)) continue;
                    bool c = map.river[i];
                    for (int k = 0; k < 4 && !c; k++)
                    {
                        int nx = x + WorldMap.DX4[k], ny = y + WorldMap.DY4[k];
                        if (WorldMap.In(nx, ny) && (map.IsWater(WorldMap.Idx(nx, ny)) || map.river[WorldMap.Idx(nx, ny)])) c = true;
                    }
                    map.coast[i] = c;

                    var b = map.biome[i];
                    double r = rnd.NextDouble();
                    if (map.river[i]) map.trees[i] = 0;
                    else if (b == Biome.Forest || b == Biome.Pine) map.trees[i] = (byte)(r < 0.3 ? 2 : 3);
                    else if (b == Biome.Grass && r < 0.07) map.trees[i] = 1;
                    else if (b == Biome.Hills && r < 0.18) map.trees[i] = 1;
                    else if (b == Biome.Steppe && r < 0.03) map.trees[i] = 1;
                    else map.trees[i] = 0;
                }

            // Kontinent-Ids der Inseln korrigieren: zusammenhängende Landmassen erhalten Mehrheits-Id
            FixContinents(map);

            // --- Bodenschätze (eigener Zufallsgenerator, damit die restliche Welt gleich bleibt) ----
            var dr = new Random(seed * 7 + 12345);
            for (int i = 0; i < W * H; i++)
            {
                var b = map.biome[i];
                double r = dr.NextDouble();
                if (b == Biome.Mountain) map.deposit[i] = r < 0.07 ? WorldMap.DepIron : r < 0.085 ? WorldMap.DepGold : (byte)0;
                else if (b == Biome.Hills) map.deposit[i] = r < 0.035 ? WorldMap.DepIron : r < 0.04 ? WorldMap.DepGold : r < 0.1 ? WorldMap.DepStone : (byte)0;
                else if (b == Biome.Desert) map.deposit[i] = r < 0.004 ? WorldMap.DepGold : r < 0.02 ? WorldMap.DepStone : (byte)0;
                if (map.deposit[i] != 0) map.trees[i] = 0;
            }
            Array.Copy(map.trees, map.baseTrees, map.trees.Length);
            return map;
        }

        static float Jit(Random r, float a) => ((float)r.NextDouble() * 2f - 1f) * a;
        static float Clamp01(float v) => v < 0 ? 0 : (v > 1 ? 1 : v);

        static void MarkLakes(WorldMap map)
        {
            int W = WorldMap.W, H = WorldMap.H;
            var seen = new bool[W * H];
            var q = new Queue<int>();
            for (int x = 0; x < W; x++) { Seed(x, 0); Seed(x, H - 1); }
            for (int y = 0; y < H; y++) { Seed(0, y); Seed(W - 1, y); }
            void Seed(int x, int y) { int i = WorldMap.Idx(x, y); if (!seen[i] && map.biome[i] == Biome.Ocean) { seen[i] = true; q.Enqueue(i); } }
            while (q.Count > 0)
            {
                int c = q.Dequeue(); int cx = WorldMap.X(c), cy = WorldMap.Y(c);
                for (int k = 0; k < 4; k++)
                {
                    int nx = cx + WorldMap.DX4[k], ny = cy + WorldMap.DY4[k];
                    if (!WorldMap.In(nx, ny)) continue;
                    int ni = WorldMap.Idx(nx, ny);
                    if (!seen[ni] && map.biome[ni] == Biome.Ocean) { seen[ni] = true; q.Enqueue(ni); }
                }
            }
            for (int i = 0; i < W * H; i++)
                if (map.biome[i] == Biome.Ocean && !seen[i]) { map.biome[i] = Biome.Lake; map.h[i] = Math.Max(map.h[i], WorldMap.Sea + 0.01f); }
        }

        static bool TryRiver(WorldMap map, int sx, int sy, Random rnd)
        {
            var path = new List<int>();
            var inPath = new HashSet<int>();
            int x = sx, y = sy;
            bool reachedWater = false;
            for (int step = 0; step < 300; step++)
            {
                int i = WorldMap.Idx(x, y);
                path.Add(i); inPath.Add(i);
                int best = -1; float bestH = float.MaxValue;
                for (int k = 0; k < 4; k++)
                {
                    int nx = x + WorldMap.DX4[k], ny = y + WorldMap.DY4[k];
                    if (!WorldMap.In(nx, ny)) continue;
                    int ni = WorldMap.Idx(nx, ny);
                    if (inPath.Contains(ni)) continue;
                    if (map.IsWater(ni) || map.river[ni]) { best = ni; bestH = -1; break; }
                    float hh = map.h[ni] + (float)rnd.NextDouble() * 0.004f;
                    if (hh < bestH) { bestH = hh; best = ni; }
                }
                if (best < 0) break;
                if (bestH < 0) { reachedWater = true; break; }
                if (bestH > map.h[i] + 0.015f)
                {
                    // Senke: kleiner See
                    if (path.Count > 10) { map.biome[i] = Biome.Lake; path.RemoveAt(path.Count - 1); reachedWater = true; }
                    break;
                }
                x = WorldMap.X(best); y = WorldMap.Y(best);
            }
            if (!reachedWater || path.Count < 7) return false;
            float prev = float.MaxValue;
            foreach (int i in path)
            {
                map.river[i] = true;
                // Flussbett leicht eingraben, stets bergab
                float nh = Math.Min(map.h[i] - 0.01f, prev - 0.002f);
                map.h[i] = Math.Max(WorldMap.Sea + 0.004f, nh);
                prev = map.h[i];
            }
            return true;
        }

        static void FixContinents(WorldMap map)
        {
            int N = WorldMap.W * WorldMap.H;
            var comp = new int[N];
            for (int i = 0; i < N; i++) comp[i] = -1;
            int id = 0;
            var q = new Queue<int>();
            var members = new List<int>();
            for (int s = 0; s < N; s++)
            {
                if (comp[s] >= 0 || map.IsSea(s)) continue;
                members.Clear();
                comp[s] = id; q.Enqueue(s);
                var counts = new int[4];
                while (q.Count > 0)
                {
                    int c = q.Dequeue(); members.Add(c); counts[map.cont[c]]++;
                    int cx = WorldMap.X(c), cy = WorldMap.Y(c);
                    for (int k = 0; k < 4; k++)
                    {
                        int nx = cx + WorldMap.DX4[k], ny = cy + WorldMap.DY4[k];
                        if (!WorldMap.In(nx, ny)) continue;
                        int ni = WorldMap.Idx(nx, ny);
                        if (comp[ni] < 0 && !map.IsSea(ni)) { comp[ni] = id; q.Enqueue(ni); }
                    }
                }
                int best = 0;
                for (int c = 1; c < 4; c++) if (counts[c] > counts[best]) best = c;
                foreach (int m in members) map.cont[m] = (byte)best;
                id++;
            }
        }
    }
}
