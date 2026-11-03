using System;
using System.Collections.Generic;

namespace PixelKingdom
{
    /// <summary>Ein platziertes Sprite in Weltkoordinaten (1 Einheit = 1 Kachel, Basis = Bodenpunkt).</summary>
    public struct Placed
    {
        public string Name; public float X, Y; public bool Flat; public bool Tinted; public bool Flip;
        public Placed(string n, float x, float y, bool flat = false, bool tinted = false, bool flip = false) { Name = n; X = x; Y = y; Flat = flat; Tinted = tinted; Flip = flip; }
    }

    /// <summary>Bestimmt, welche Natur-Objekte (Bäume, Stümpfe, Berge, Vorkommen) auf einer Kachel stehen.</summary>
    public static class ObjectPlacer
    {
        static float R(int x, int y, int k)
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + k * 1442695041);
            h = (h ^ (h >> 13)) * 1274126177; h ^= h >> 16;
            return (h & 0xFFFF) / 65535f;
        }

        static bool IsMountain(WorldMap map, int i) => map.biome[i] == Biome.Mountain || map.biome[i] == Biome.Snow;

        /// <summary>Ist dieses Feld das höchste Gebirgsfeld seines 4x4-Blocks?</summary>
        static bool IsBlockPeak(WorldMap map, int x, int y)
        {
            int bx = x / 4 * 4, by = y / 4 * 4;
            int best = -1; float bh = float.MinValue;
            for (int yy = by; yy < by + 4; yy++)
                for (int xx = bx; xx < bx + 4; xx++)
                {
                    if (!WorldMap.In(xx, yy)) continue;
                    int j = WorldMap.Idx(xx, yy);
                    if (!IsMountain(map, j) || map.deposit[j] != 0) continue;
                    if (map.h[j] > bh) { bh = map.h[j]; best = j; }
                }
            return best == WorldMap.Idx(x, y);
        }

        public static void ForTile(WorldMap map, int x, int y, List<Placed> outList)
        {
            int i = WorldMap.Idx(x, y);
            if (map.IsWater(i)) return;
            var b = map.biome[i];
            float cx = x + 0.5f, cy = y + 0.5f;

            byte dep = map.deposit[i];
            if (dep != 0)
            {
                outList.Add(new Placed(dep == WorldMap.DepIron ? "d_iron" : dep == WorldMap.DepGold ? "d_gold" : "d_stone", cx, y + 0.35f));
                return;
            }
            if (b == Biome.Mountain || b == Biome.Snow)
            {
                // Geschlossene Gebirgsketten: in jedem 3x3-Block steht ein Massiv auf dem höchsten Gebirgsfeld.
                // Die Massive sind breiter als 3 Felder und überlappen sich daher lückenlos.
                if (IsBlockPeak(map, x, y))
                {
                    int dens = 0; bool snowy = b == Biome.Snow || map.h[i] > 0.88f;
                    for (int dy = -2; dy <= 2; dy++)
                        for (int dx = -2; dx <= 2; dx++)
                        {
                            int nx = x + dx, ny = y + dy;
                            if (!WorldMap.In(nx, ny)) continue;
                            var nb = map.biome[WorldMap.Idx(nx, ny)];
                            if (nb == Biome.Mountain || nb == Biome.Snow) dens++;
                            if (nb == Biome.Snow) snowy = true;
                        }
                    string n = dens >= 16 ? (snowy ? "m_huge" : "m_big") : dens >= 8 ? (snowy ? "m_big_s" : "m_big") : (snowy ? "m_mid_s" : "m_mid");
                    outList.Add(new Placed(n, cx + (R(x, y, 3) - 0.5f) * 0.5f, y + 0.1f + R(x, y, 4) * 0.3f, false, false, R(x, y, 5) < 0.5f));
                }
                else if (R(x, y, 6) < 0.12f) outList.Add(new Placed("r_rock", cx, y + 0.4f));
            }
            else if (b == Biome.Hills && map.trees[i] == 0 && R(x, y, 7) < 0.18f)
                outList.Add(new Placed("h_hill0", cx + (R(x, y, 8) - 0.5f) * 0.4f, y + 0.35f, false, false, R(x, y, 9) < 0.5f));

            int have = map.trees[i], baseN = Math.Max(have, map.baseTrees[i]);
            for (int k = 0; k < baseN; k++)
            {
                float tx = x + 0.2f + R(x, y, 10 + k) * 0.6f;
                float ty = y + 0.1f + (k / (float)Math.Max(1, baseN)) * 0.65f + R(x, y, 20 + k) * 0.15f;
                if (k >= have) { outList.Add(new Placed("t_stump", tx, ty)); continue; }
                string n;
                switch (b)
                {
                    case Biome.Pine: n = y > WorldMap.H * 0.7f || map.h[i] > 0.7f ? "t_snowpine" + (k % 2) : "t_pine" + (k % 2); break;
                    case Biome.Snow: n = "t_snowpine" + (k % 2); break;
                    case Biome.Steppe: n = "t_bush"; break;
                    case Biome.Desert: n = "t_cactus"; break;
                    default: n = "t_oak" + (int)(R(x, y, 30 + k) * 2.99f); break;
                }
                outList.Add(new Placed(n, tx, ty, false, false, R(x, y, 40 + k) < 0.5f));
            }
            if (map.coast[i] && !map.IsWater(i) && have == 0 && R(x, y, 50) < 0.12f)
                outList.Add(new Placed("t_reeds", x + R(x, y, 51), y + 0.2f));
        }
    }

    /// <summary>Ordnet die tatsächlich gebauten Gebäude einer Siedlung auf der Karte an.</summary>
    public static class SettlementLayout
    {
        public static int CenterLevel(Game g, SettlementData s)
        {
            var f = g.F(s.faction);
            if (s.isCapitalOfEmperor) return 4;
            if (s.pop > 160 || (f != null && f.capital == s.id && f.rank >= 3)) return 3;
            if (s.pop > 70 || (f != null && f.capital == s.id && f.rank >= 2)) return 2;
            if (s.pop > 35) return 1;
            return 0;
        }

        public static List<Placed> Build(Game g, SettlementData s, Season season, HashSet<int> occupied)
        {
            var res = new List<Placed>();
            var map = g.Map;
            var used = new HashSet<int>();
            int cx = s.x, cy = s.y;
            int walls = s.buildings[(int)BType.Mauer];
            const int WR = 3;

            void Use(int x, int y) { if (WorldMap.In(x, y)) { int i = WorldMap.Idx(x, y); used.Add(i); occupied?.Add(i); } }
            // Kleine Dörfer: Gebäude mit Abstand (lockerer Dorfcharakter); Städte bleiben dicht bebaut
            var bld = new HashSet<int>();
            bool sparse = s.pop < 90 && s.buildings[(int)BType.Mauer] == 0;
            bool Spaced(int x, int y)
            {
                if (!sparse) return true;
                for (int k = 0; k < 4; k++)
                {
                    int nx = x + WorldMap.DX4[k], ny = y + WorldMap.DY4[k];
                    if (WorldMap.In(nx, ny) && bld.Contains(WorldMap.Idx(nx, ny))) return false;
                }
                return true;
            }
            bool Free(int x, int y)
            {
                if (!WorldMap.In(x, y)) return false;
                int i = WorldMap.Idx(x, y);
                if (used.Contains(i) || !map.Buildable(i) || map.deposit[i] != 0) return false;
                int o = map.ownerSettlement[i];
                return o == s.id || o == -1;
            }
            bool OnWallLine(int x, int y) => walls > 0 && ((Math.Abs(x - cx) == WR && Math.Abs(y - cy) <= WR) || (Math.Abs(y - cy) == WR && Math.Abs(x - cx) <= WR));

            // Kacheln nach Entfernung
            var tiles = new List<(int x, int y, float d)>();
            for (int y = cy - 6; y <= cy + 6; y++)
                for (int x = cx - 6; x <= cx + 6; x++)
                {
                    if (!WorldMap.In(x, y)) continue;
                    float d = (float)Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    if (d > 6.2f) continue;
                    uint hh = (uint)(x * 73856093 ^ y * 19349663);
                    tiles.Add((x, y, d + (hh % 100) / 400f));
                }
            tiles.Sort((a, b) => a.d.CompareTo(b.d));

            // Zentrum (3x2 Kacheln)
            for (int dy = 0; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++) Use(cx + dx, cy + dy);
            if (WorldMap.In(cx, cy)) bld.Add(WorldMap.Idx(cx, cy));
            int lvl = CenterLevel(g, s);
            string center = s.buildings[(int)BType.Burg] > 0 && !s.isCapitalOfEmperor ? "b_Burg"
                : lvl >= 4 ? "c_palace" : lvl >= 2 ? "c_keep" : lvl == 1 ? "c_rathaus" : "c_dorf";
            res.Add(new Placed(center, cx + 0.5f, cy + 0.25f, false, true));

            // Mauern
            if (walls > 0)
            {
                string k = walls >= 2 ? "stone" : "pal";
                for (int x = cx - WR; x <= cx + WR; x++)
                {
                    foreach (int y in new[] { cy - WR, cy + WR })
                    {
                        if (!WorldMap.In(x, y) || map.IsWater(WorldMap.Idx(x, y)) || map.river[WorldMap.Idx(x, y)]) continue;
                        Use(x, y);
                        bool corner = x == cx - WR || x == cx + WR;
                        string n = corner ? "w_" + k + "_tower" : (y == cy - WR && x == cx) ? "w_" + k + "_gate" : "w_" + k + "_h";
                        res.Add(new Placed(n, x + 0.5f, y + 0.2f));
                    }
                }
                for (int y = cy - WR + 1; y <= cy + WR - 1; y++)
                    foreach (int x in new[] { cx - WR, cx + WR })
                    {
                        if (!WorldMap.In(x, y) || map.IsWater(WorldMap.Idx(x, y)) || map.river[WorldMap.Idx(x, y)]) continue;
                        Use(x, y);
                        res.Add(new Placed("w_" + k + "_v", x + 0.5f, y + 0.0f));
                    }
            }

            bool Inside(int x, int y) => walls == 0 || (Math.Abs(x - cx) < WR && Math.Abs(y - cy) < WR);

            (int x, int y) Find(Func<int, int, bool> pred, bool preferInside, float minD = 0)
            {
                for (int pass = 0; pass < 2; pass++)
                {
                    bool needSpace = pass == 0;
                    if (preferInside)
                        foreach (var t in tiles) if (t.d >= minD && Free(t.x, t.y) && !OnWallLine(t.x, t.y) && Inside(t.x, t.y) && pred(t.x, t.y) && (!needSpace || Spaced(t.x, t.y))) return (t.x, t.y);
                    foreach (var t in tiles) if (t.d >= minD && Free(t.x, t.y) && !OnWallLine(t.x, t.y) && pred(t.x, t.y) && (!needSpace || Spaced(t.x, t.y))) return (t.x, t.y);
                }
                return (-1, -1);
            }

            bool Near(int x, int y, Func<int, bool> f)
            {
                for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = x + dx, ny = y + dy;
                        if (WorldMap.In(nx, ny) && f(WorldMap.Idx(nx, ny))) return true;
                    }
                return false;
            }

            void Put(string name, (int x, int y) t, bool tinted = false)
            {
                if (t.x < 0) return;
                Use(t.x, t.y); bld.Add(WorldMap.Idx(t.x, t.y));
                res.Add(new Placed(name, t.x + 0.5f, t.y + 0.28f, false, tinted));
            }

            // Große Gebäude im Inneren
            if (s.buildings[(int)BType.Kathedrale] > 0)
            {
                var t = Find((x, y) => Free(x - 1, y) && Free(x + 1, y) && Free(x, y + 1), true, 1.5f);
                if (t.x >= 0) { Use(t.x - 1, t.y); Use(t.x + 1, t.y); Use(t.x, t.y + 1); Use(t.x - 1, t.y + 1); Use(t.x + 1, t.y + 1); Put("b_Kathedrale", t); }
            }
            if (s.buildings[(int)BType.Kloster] > 0) Put("b_Kloster", Find((x, y) => Free(x + 1, y), false, 2.5f));
            if (s.buildings[(int)BType.Turnierplatz] > 0) { var tt = Find((x, y) => Free(x - 1, y) && Free(x + 1, y), false, 2f); if (tt.x >= 0) { Use(tt.x - 1, tt.y); Use(tt.x + 1, tt.y); } Put("b_Turnierplatz", tt, true); }
            for (int k = 0; k < Math.Min(2, s.buildings[(int)BType.Windmuehle]); k++)
                Put("b_Windmuehle", Find((x, y) => map.biome[WorldMap.Idx(x, y)] == Biome.Fertile || map.biome[WorldMap.Idx(x, y)] == Biome.Grass, false, 2f));
            BType[] inner = { BType.Markt, BType.Taverne, BType.Kapelle, BType.Kaserne, BType.Archerie, BType.Stall, BType.Werkstatt, BType.Lagerhaus, BType.Schmiede, BType.Brunnen };
            foreach (var bt in inner)
                for (int k = 0; k < Math.Min(3, s.buildings[(int)bt]); k++)
                    Put("b_" + bt, Find((x, y) => true, true), bt == BType.Kaserne);

            // Landschaftsgebundene Gebäude
            string field = season == Season.Winter ? "f_snow" : season == Season.Fruehling ? "f_green" : "f_wheat";
            for (int k = 0; k < Math.Min(10, s.buildings[(int)BType.Bauernhof]); k++)
            {
                var t = Find((x, y) => map.biome[WorldMap.Idx(x, y)] == Biome.Fertile || map.biome[WorldMap.Idx(x, y)] == Biome.Grass || map.biome[WorldMap.Idx(x, y)] == Biome.Steppe, false, 1.5f);
                if (t.x < 0) t = Find((x, y) => true, false, 1.5f);
                if (t.x < 0) break;
                Put("b_Bauernhof", t);
                int fields = 0;
                for (int dy = -1; dy <= 1 && fields < 2; dy++)
                    for (int dx = -1; dx <= 1 && fields < 2; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int fx = t.x + dx, fy = t.y + dy;
                        if (!Free(fx, fy) || OnWallLine(fx, fy)) continue;
                        Use(fx, fy);
                        res.Add(new Placed(field, fx + 0.5f, fy + 0.05f, true));
                        fields++;
                    }
            }
            for (int k = 0; k < Math.Min(5, s.buildings[(int)BType.Fischerhuette]); k++)
                Put("b_Fischerhuette", Find((x, y) => map.coast[WorldMap.Idx(x, y)], false));
            if (s.buildings[(int)BType.Hafen] > 0)
                Put("b_Hafen", Find((x, y) => Near(x, y, i => map.IsSea(i)), false));
            for (int k = 0; k < Math.Min(6, s.buildings[(int)BType.Holzfaeller]); k++)
                Put("b_Holzfaeller", Find((x, y) => Near(x, y, i => map.baseTrees[i] > 0), false, 1.5f));
            for (int k = 0; k < Math.Min(4, s.buildings[(int)BType.Steinbruch]); k++)
                Put("b_Steinbruch", Find((x, y) => Near(x, y, i => map.biome[i] == Biome.Hills || map.biome[i] == Biome.Mountain || map.deposit[i] == WorldMap.DepStone), false, 1.5f));
            for (int k = 0; k < Math.Min(4, s.buildings[(int)BType.Mine]); k++)
                Put("b_Mine", Find((x, y) => Near(x, y, i => map.deposit[i] == WorldMap.DepIron), false));
            for (int k = 0; k < Math.Min(3, s.buildings[(int)BType.Goldmine]); k++)
                Put("b_Goldmine", Find((x, y) => Near(x, y, i => map.deposit[i] == WorldMap.DepGold), false));

            // Wohnhäuser zum Schluss (füllen die Stadt)
            int houses = Math.Min(36, s.buildings[(int)BType.Wohnhaus]);
            for (int k = 0; k < houses; k++)
            {
                var t = Find((x, y) => true, true);
                if (t.x < 0) break;
                Use(t.x, t.y); bld.Add(WorldMap.Idx(t.x, t.y));
                uint hh = (uint)(t.x * 73856093 ^ t.y * 19349663);
                res.Add(new Placed("b_Wohnhaus" + (hh % 3), t.x + 0.5f + ((hh >> 6) % 3 - 1) * 0.08f, t.y + 0.28f));
            }
            return res;
        }
    }
}
