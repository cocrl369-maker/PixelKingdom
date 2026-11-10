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

        public static void ForTile(WorldMap map, int x, int y, List<Placed> outList)
        {
            int i = WorldMap.Idx(x, y);
            if (map.IsWater(i)) return;
            var b = map.biome[i];
            float cx = x + 0.5f, cy = y + 0.5f;

            // Straße (flach, mit Verbindungen zu den Nachbarfeldern)
            if (map.road[i])
            {
                int mask = 0;
                for (int k = 0; k < 4; k++)
                {
                    int nx = x + WorldMap.DX4[k], ny = y + WorldMap.DY4[k];
                    if (WorldMap.In(nx, ny) && map.road[WorldMap.Idx(nx, ny)]) mask |= 1 << k;
                }
                outList.Add(new Placed("rd_" + mask, x + 0.5f, y, true));
            }

            byte dep = map.deposit[i];
            if (dep != 0)
            {
                outList.Add(new Placed(dep == WorldMap.DepIron ? "d_iron" : dep == WorldMap.DepGold ? "d_gold" : "d_stone", cx, y + 0.35f));
                return;
            }
            if (b == Biome.Mountain || b == Biome.Snow)
            {
                // Das Gebirge selbst malt der GroundPainter in den Boden – es füllt genau das Gebirgsgebiet.
                return;
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
            int WR = s.grand > 0 ? 5 : 3;   // prächtige Hauptstädte haben einen weiteren Mauerring

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
            if (s.grand > 0) { for (int dy = -1; dy <= 2; dy++) for (int dx = -3; dx <= 3; dx++) Use(cx + dx, cy + dy); }
            else for (int dy = 0; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++) Use(cx + dx, cy + dy);
            if (WorldMap.In(cx, cy)) bld.Add(WorldMap.Idx(cx, cy));
            int lvl = CenterLevel(g, s);
            // Orte auf den großen Inseln (Eisenfels, Smaragdinsel) baut das Volk der Saelaner – eigener Baustil ("i"-Sprites)
            string pre = map.cont[WorldMap.Idx(cx, cy)] >= 4 ? "i" : "";
            string center = s.grand == 1 ? "c_imperial" : s.grand == 2 ? "c_citadel" : s.grand == 3 ? "c_emerald"
                : s.buildings[(int)BType.Burg] > 0 && !s.isCapitalOfEmperor ? "b_Burg"
                : lvl >= 4 ? "c_palace" : lvl >= 2 ? "c_keep" : lvl == 1 ? "c_rathaus" : "c_dorf";
            res.Add(new Placed(pre + center, cx + 0.5f, cy + 0.25f, false, true));

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
                        res.Add(new Placed(pre + n, x + 0.5f, y + 0.2f));
                    }
                }
                for (int y = cy - WR + 1; y <= cy + WR - 1; y++)
                    foreach (int x in new[] { cx - WR, cx + WR })
                    {
                        if (!WorldMap.In(x, y) || map.IsWater(WorldMap.Idx(x, y)) || map.river[WorldMap.Idx(x, y)]) continue;
                        Use(x, y);
                        res.Add(new Placed(pre + "w_" + k + "_v", x + 0.5f, y + 0.0f));
                    }
            }

            // Gebäude stehen dort, wo sie gebaut wurden (vom Spieler gewählt oder von der KI gesucht)
            string field = season == Season.Winter ? "f_snow" : season == Season.Fruehling ? "f_green" : "f_wheat";
            foreach (var b in s.placed) if (WorldMap.In(b.x, b.y)) Use(b.x, b.y);
            foreach (var q in s.queue) if (q.kind == 0 && q.x >= 0 && WorldMap.In(q.x, q.y)) Use(q.x, q.y);
            bool FreeField(int x, int y)
            {
                if (!WorldMap.In(x, y)) return false;
                int i = WorldMap.Idx(x, y);
                if (used.Contains(i) || !map.Buildable(i) || map.deposit[i] != 0 || g.BuildingOwnerAt(x, y) != -1) return false;
                int o = map.owner[i];
                return o == s.faction || o == -1;
            }
            foreach (var b in s.placed)
            {
                var bt = (BType)b.type;
                uint hh = (uint)(b.x * 73856093 ^ b.y * 19349663);
                string name = bt == BType.Wohnhaus ? "b_Wohnhaus" + (hh % 3) : "b_" + bt;
                float ox = bt == BType.Wohnhaus ? ((hh >> 6) % 3 - 1) * 0.08f : 0f;
                bool tinted = bt == BType.Kaserne || bt == BType.Turnierplatz || bt == BType.Wachturm || bt == BType.Feldlager;
                res.Add(new Placed(pre + name, b.x + 0.5f + ox, b.y + 0.28f, false, tinted));
                if (bt == BType.Bauernhof)
                {
                    int fields = 0;
                    for (int k = 0; k < 8 && fields < 2; k++)
                    {
                        int fx = b.x + WorldMap.DX8[(k + (int)(hh % 8)) % 8], fy = b.y + WorldMap.DY8[(k + (int)(hh % 8)) % 8];
                        if (!FreeField(fx, fy)) continue;
                        Use(fx, fy);
                        res.Add(new Placed(field, fx + 0.5f, fy + 0.05f, true));
                        fields++;
                    }
                }
            }
            // Baustellen mit Gerüst
            foreach (var q in s.queue)
                if (q.kind == 0 && q.x >= 0) res.Add(new Placed("b_scaffold", q.x + 0.5f, q.y + 0.28f));
            return res;
        }
    }
}
