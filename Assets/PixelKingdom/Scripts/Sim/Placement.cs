using System;
using System.Collections.Generic;
using System.Linq;

namespace PixelKingdom
{
    /// <summary>
    /// Bauplätze: Jedes Gebäude steht auf einem eigenen Feld. Produktionsgebäude dürfen weit draußen stehen
    /// (mit Ertragsverlust durch lange Wege), Stadtgebäude nur in der Stadt. Fremdes Gebiet ist tabu.
    /// Der Ertrag hängt vom Standort ab (Bäume, fruchtbares Land, Erz, Wasser in der Umgebung).
    /// </summary>
    public partial class Game
    {
        public const int MaxRemote = 14;
        int[] tileBuilding;           // -1 frei, sonst Siedlungs-Id (Gebäude, Zentrum, Mauer, Baustelle)
        bool ignoreFog;

        /// <summary>Stimmen Gebäudezahl und Bauplätze nicht überein (z.B. nach Eroberung oder Story-Ereignis)?</summary>
        public bool PlacedMismatch(SettlementData s)
        {
            int need = 0;
            for (int t = 0; t < Buildings.Count; t++) if (NeedsSite((BType)t)) need += s.buildings[t];
            return need != s.placed.Count;
        }

        /// <summary>Gebäude, die ihre Arbeit auch fern der Stadt tun (Anbau, Abbau, Wache).</summary>
        public static bool IsRemote(BType t) =>
            t == BType.Bauernhof || t == BType.Fischerhuette || t == BType.Holzfaeller || t == BType.Steinbruch || t == BType.Mine ||
            t == BType.Goldmine || t == BType.Windmuehle || t == BType.Hafen || t == BType.Wachturm;

        /// <summary>Mauer und Burg haben keinen eigenen Bauplatz (Ring bzw. Ortszentrum).</summary>
        public static bool NeedsSite(BType t) => t != BType.Mauer && t != BType.Burg;

        public int TownRadius(SettlementData s) => Radius(s) + 1;

        public int BuildingOwnerAt(int x, int y) => tileBuilding != null && WorldMap.In(x, y) ? tileBuilding[WorldMap.Idx(x, y)] : -1;

        public void RebuildOccupancy()
        {
            int N = WorldMap.W * WorldMap.H;
            if (tileBuilding == null) tileBuilding = new int[N];
            for (int i = 0; i < N; i++) tileBuilding[i] = -1;
            foreach (var s in S.settlements)
            {
                void Mark(int x, int y) { if (WorldMap.In(x, y)) tileBuilding[WorldMap.Idx(x, y)] = s.id; }
                for (int dy = 0; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++) Mark(s.x + dx, s.y + dy);
                if (s.buildings[(int)BType.Mauer] > 0)
                    for (int k = -3; k <= 3; k++) { Mark(s.x + k, s.y - 3); Mark(s.x + k, s.y + 3); Mark(s.x - 3, s.y + k); Mark(s.x + 3, s.y + k); }
                foreach (var b in s.placed) Mark(b.x, b.y);
                foreach (var q in s.queue) if (q.kind == 0 && q.x >= 0) Mark(q.x, q.y);
            }
            foreach (var p in S.pois) if (p.type != 2 && !(p.type == 1 && p.done)) tileBuilding[WorldMap.Idx(p.x, p.y)] = -2;
        }

        public float DistanceFactor(SettlementData s, int x, int y)
        {
            float d = Dist(s.x, s.y, x, y), r = TownRadius(s);
            if (d <= r) return 1f;
            return Math.Max(0.45f, 1f - (d - r) * 0.05f);
        }

        /// <summary>Wie gut ist dieser Standort für das Gebäude? (1 = normal)</summary>
        public float SiteBonus(BType t, int x, int y)
        {
            int Count(int r, Func<int, int> f)
            {
                int n = 0;
                for (int yy = y - r; yy <= y + r; yy++)
                    for (int xx = x - r; xx <= x + r; xx++)
                        if (WorldMap.In(xx, yy)) n += f(WorldMap.Idx(xx, yy));
                return n;
            }
            switch (t)
            {
                case BType.Bauernhof: return Clamp(0.45f + Count(1, i => Map.biome[i] == Biome.Fertile ? 2 : (Map.biome[i] == Biome.Grass || Map.biome[i] == Biome.Steppe) ? 1 : 0) / 11f, 0.45f, 1.45f);
                case BType.Holzfaeller: return Clamp(0.15f + Count(2, i => Map.trees[i]) / 16f, 0.15f, 1.45f);
                case BType.Steinbruch: return Clamp(0.4f + Count(1, i => (Map.biome[i] == Biome.Hills ? 1 : 0) + (Map.biome[i] == Biome.Mountain || Map.biome[i] == Biome.Snow ? 2 : 0) + (Map.deposit[i] == WorldMap.DepStone ? 3 : 0)) * 0.12f, 0.4f, 1.6f);
                case BType.Mine: return Clamp(0.5f + Count(1, i => Map.deposit[i] == WorldMap.DepIron ? 1 : 0) * 0.4f, 0.5f, 1.7f);
                case BType.Goldmine: return Clamp(0.5f + Count(1, i => Map.deposit[i] == WorldMap.DepGold ? 1 : 0) * 0.45f, 0.5f, 1.7f);
                case BType.Fischerhuette: return Clamp(0.4f + Count(2, i => Map.IsWater(i) || Map.river[i] ? 1 : 0) / 10f, 0.4f, 1.35f);
            }
            return 1f;
        }

        bool Near(int x, int y, int r, Func<int, bool> f)
        {
            for (int yy = y - r; yy <= y + r; yy++)
                for (int xx = x - r; xx <= x + r; xx++)
                    if (WorldMap.In(xx, yy) && f(WorldMap.Idx(xx, yy))) return true;
            return false;
        }

        /// <summary>Darf das Gebäude hier gebaut werden? Gibt einen verständlichen Grund zurück, falls nicht.</summary>
        public bool CanPlace(SettlementData s, BType t, int x, int y, out string why)
        {
            why = null;
            if (tileBuilding == null) RebuildOccupancy();
            if (!WorldMap.In(x, y)) { why = "Außerhalb der Karte"; return false; }
            int i = WorldMap.Idx(x, y);
            var f = F(s.faction);
            if (f.isPlayer && !ignoreFog && !Map.disc[i]) { why = "Unerforschtes Gebiet"; return false; }
            bool mineType = t == BType.Mine || t == BType.Goldmine || t == BType.Steinbruch;
            if (Map.IsWater(i) || Map.river[i]) { why = "Im Wasser kann man nicht bauen"; return false; }
            if (Map.biome[i] == Biome.Mountain || Map.biome[i] == Biome.Snow) { why = "Zu steil – Gebirge"; return false; }
            if (Map.deposit[i] != 0 && !mineType) { why = "Hier liegt ein Erzvorkommen – nur für Minen"; return false; }
            if (tileBuilding[i] != -1) { why = tileBuilding[i] == -2 ? "Hier ist schon etwas" : "Der Platz ist belegt"; return false; }
            int own = Map.owner[i];
            if (own >= 0 && own != s.faction) { var o = F(own); why = "Gebiet von " + (o != null ? o.name : "Fremden"); return false; }
            float d = Dist(s.x, s.y, x, y);
            if (!IsRemote(t) && d > TownRadius(s)) { why = "Muss in der Stadt stehen (nah am Zentrum)"; return false; }
            if (d > MaxRemote) { why = $"Zu weit weg von {s.name} (max. {MaxRemote} Felder)"; return false; }
            switch (t)
            {
                case BType.Bauernhof:
                    if (!(Map.biome[i] == Biome.Fertile || Map.biome[i] == Biome.Grass || Map.biome[i] == Biome.Steppe)) { why = "Braucht Wiese oder fruchtbares Land"; return false; }
                    break;
                case BType.Holzfaeller:
                    if (!Near(x, y, 2, j => Map.trees[j] > 0)) { why = "Braucht Bäume in der Nähe"; return false; }
                    break;
                case BType.Fischerhuette:
                    if (!Near(x, y, 1, j => Map.IsWater(j) || Map.river[j])) { why = "Muss am Wasser stehen"; return false; }
                    break;
                case BType.Hafen:
                    if (!Near(x, y, 1, j => Map.IsSea(j))) { why = "Muss an der Meeresküste stehen"; return false; }
                    break;
                case BType.Steinbruch:
                    if (!Near(x, y, 1, j => Map.biome[j] == Biome.Hills || Map.biome[j] == Biome.Mountain || Map.biome[j] == Biome.Snow || Map.deposit[j] == WorldMap.DepStone)) { why = "Braucht Hügel, Fels oder ein Steinvorkommen"; return false; }
                    break;
                case BType.Mine:
                    if (!Near(x, y, 1, j => Map.deposit[j] == WorldMap.DepIron)) { why = "Muss an einem Eisenvorkommen stehen"; return false; }
                    break;
                case BType.Goldmine:
                    if (!Near(x, y, 1, j => Map.deposit[j] == WorldMap.DepGold)) { why = "Muss an einem Goldvorkommen stehen"; return false; }
                    break;
            }
            if (mineType && Map.deposit[i] != 0)
            {
                bool fits = (t == BType.Mine && Map.deposit[i] == WorldMap.DepIron) || (t == BType.Goldmine && Map.deposit[i] == WorldMap.DepGold) || (t == BType.Steinbruch && Map.deposit[i] == WorldMap.DepStone);
                if (!fits) { why = "Falsches Vorkommen"; return false; }
            }
            return true;
        }

        /// <summary>Bester Bauplatz (für KI, Verwalter und alte Spielstände).</summary>
        public bool FindSite(SettlementData s, BType t, out int bx, out int by)
        {
            bx = by = -1;
            if (tileBuilding == null) RebuildOccupancy();
            int r = IsRemote(t) ? Math.Min(MaxRemote, TownRadius(s) + 6) : TownRadius(s);
            float best = float.MinValue;
            for (int y = s.y - r; y <= s.y + r; y++)
                for (int x = s.x - r; x <= s.x + r; x++)
                {
                    if (!CanPlace(s, t, x, y, out _)) continue;
                    float d = Dist(s.x, s.y, x, y);
                    float score = SiteBonus(t, x, y) * DistanceFactor(s, x, y) * 10f - d * (IsRemote(t) ? 0.1f : 0.6f);
                    // Häuser locker gruppieren: leicht bevorzugt neben anderen Gebäuden, aber nicht direkt daneben in Dörfern
                    if (!IsRemote(t) && s.pop < 90 && s.buildings[(int)BType.Mauer] == 0)
                        for (int k = 0; k < 4; k++)
                            if (BuildingOwnerAt(x + WorldMap.DX4[k], y + WorldMap.DY4[k]) == s.id) score -= 1.5f;
                    score += (PixelCanvasHash(x, y) - 0.5f) * 0.3f;
                    if (score > best) { best = score; bx = x; by = y; }
                }
            return bx >= 0;
        }

        static float PixelCanvasHash(int x, int y)
        {
            uint h = (uint)(x * 374761393 + y * 668265263);
            h = (h ^ (h >> 13)) * 1274126177;
            return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
        }

        /// <summary>Sorgt dafür, dass jedes Gebäude der Siedlung einen Bauplatz hat.</summary>
        public void EnsurePlaced(SettlementData s)
        {
            if (tileBuilding == null) RebuildOccupancy();
            ignoreFog = true;
            bool removed = false;
            for (int t = 0; t < Buildings.Count; t++)
            {
                var bt = (BType)t;
                if (!NeedsSite(bt)) continue;
                int have = 0;
                foreach (var b in s.placed) if (b.type == t) have++;
                while (have > s.buildings[t])
                {
                    int idx = s.placed.FindLastIndex(b => b.type == t);
                    s.placed.RemoveAt(idx); have--; removed = true;
                }
                while (have < s.buildings[t])
                {
                    if (!FindSite(s, bt, out int x, out int y) && !FindAnySite(s, out x, out y)) break;
                    s.placed.Add(new BuildingPos(t, x, y));
                    tileBuilding[WorldMap.Idx(x, y)] = s.id;
                    have++;
                }
            }
            ignoreFog = false;
            if (removed) RebuildOccupancy();
        }

        bool FindAnySite(SettlementData s, out int bx, out int by)
        {
            bx = by = -1;
            for (int r = 1; r <= 8; r++)
                for (int y = s.y - r; y <= s.y + r; y++)
                    for (int x = s.x - r; x <= s.x + r; x++)
                    {
                        if (!WorldMap.In(x, y)) continue;
                        int i = WorldMap.Idx(x, y);
                        if (tileBuilding[i] != -1 || !Map.Buildable(i) || Map.deposit[i] != 0) continue;
                        if (Map.owner[i] >= 0 && Map.owner[i] != s.faction) continue;
                        bx = x; by = y; return true;
                    }
            return false;
        }

        /// <summary>Summe aus Standort- und Entfernungsfaktoren aller Gebäude eines Typs (ersetzt "Anzahl x Geländebonus").</summary>
        public float SiteSum(SettlementData s, BType t)
        {
            float sum = 0; int n = 0;
            foreach (var b in s.placed)
            {
                if (b.type != (int)t) continue;
                int own = Map.owner[WorldMap.Idx(b.x, b.y)];
                float occupied = own >= 0 && own != s.faction ? 0.3f : 1f; // von Nachbarn überwachsenes Land: kaum Ertrag
                sum += SiteBonus(t, b.x, b.y) * DistanceFactor(s, b.x, b.y) * occupied;
                n++;
            }
            var d = Buildings.Get(t);
            int missing = s.buildings[(int)t] - n;
            if (missing > 0) sum += missing * (d.Need == TerrainNeed.None ? 1f : TerrainBonus(s, d.Need));
            return sum;
        }

        /// <summary>Ist das Gebäude ein Außenposten im Niemandsland?</summary>
        public bool IsOutpost(BuildingPos b) => WorldMap.In(b.x, b.y) && Map.owner[WorldMap.Idx(b.x, b.y)] < 0;

        public IEnumerable<(SettlementData s, BuildingPos b)> PlayerOutposts()
        {
            foreach (var s in SettlementsOf(PlayerId))
                foreach (var b in s.placed)
                    if (IsOutpost(b)) yield return (s, b);
        }
    }
}
