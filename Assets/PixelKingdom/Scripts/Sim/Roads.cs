using System;
using System.Collections.Generic;
using System.Linq;

namespace PixelKingdom
{
    /// <summary>
    /// Straßen: verbinden eine Siedlung mit ihren Außenposten (Höfe, Minen, Lager ...) und mit
    /// benachbarten eigenen Orten. Wirkung: lange Wege kosten nur halb so viel Ertrag, Heere marschieren
    /// schneller, verbundene Orte handeln miteinander (Gold). Fuhrwerke fahren sichtbar auf den Straßen.
    /// </summary>
    public partial class Game
    {
        public const float RoadWood = 3f, RoadStone = 2f;
        public int RoadsVersion;

        /// <summary>Straßen aus dem Spielstand auf die Karte übertragen.</summary>
        void ApplyRoads()
        {
            Array.Clear(Map.road, 0, Map.road.Length);
            foreach (int i in S.roadPaths) if (i >= 0 && i < Map.road.Length) Map.road[i] = true;
            RoadsVersion++;
        }

        public bool RoadNear(int x, int y)
        {
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                    if (WorldMap.In(x + dx, y + dy) && Map.road[WorldMap.Idx(x + dx, y + dy)]) return true;
            return false;
        }

        /// <summary>Hängt ein Gebäude über eine Straße an seinem Ort? (Straße am Gebäude und am Ortszentrum)</summary>
        public bool RoadConnected(SettlementData s, int x, int y) => RoadNear(x, y) && RoadNear(s.x, s.y + 2);

        /// <summary>Welche Straßenstücke würde ein Ausbau bauen? (Wege zu Außenposten und Nachbarorten)</summary>
        public List<List<int>> PlanRoads(SettlementData s, out int newTiles)
        {
            newTiles = 0;
            var paths = new List<List<int>>();
            var targets = new List<(int x, int y)>();
            int r = TownRadius(s);
            foreach (var b in s.placed)
                if (Dist(s.x, s.y, b.x, b.y) > r && !RoadConnected(s, b.x, b.y)) targets.Add((b.x, b.y));
            // Nachbarorte (eigene), noch nicht verbunden
            foreach (var o in SettlementsOf(s.faction))
            {
                if (o.id == s.id || Dist(s.x, s.y, o.x, o.y) > 40) continue;
                if (RoadNear(o.x, o.y + 2) && RoadNear(s.x, s.y + 2) && S.roadLinks.Contains(Math.Min(s.id, o.id) * 100000 + Math.Max(s.id, o.id))) continue;
                targets.Add((o.x, o.y + 2));
            }
            var seen = new HashSet<int>();
            foreach (var t in targets.Take(12))
            {
                var path = Pathfinder.Find(Map, s.x, s.y + 2, t.x, t.y, false, 60000);
                if (path == null || path.Count < 2) continue;
                var clean = new List<int>();
                foreach (int i in path)
                {
                    if (Map.IsWater(i)) continue;
                    clean.Add(i);
                    if (!Map.road[i] && seen.Add(i)) newTiles++;
                }
                paths.Add(clean);
            }
            return paths;
        }

        public float[] RoadCost(int tiles) => Buildings.R(wood: tiles * RoadWood, stone: tiles * RoadStone);

        public bool BuildRoads(SettlementData s)
        {
            var paths = PlanRoads(s, out int n);
            if (n == 0) return false;
            var f = F(s.faction);
            var cost = RoadCost(n);
            if (!CanAfford(f, cost)) return false;
            Pay(f, cost);
            foreach (var path in paths)
            {
                foreach (int i in path)
                {
                    if (!Map.road[i]) { Map.road[i] = true; Map.trees[i] = 0; Map.baseTrees[i] = 0; }
                    // Darstellung: dieses Feld und die Nachbarn neu zeichnen (Straßenverlauf)
                    DirtyTrees.Add(i);
                    int x = WorldMap.X(i), y = WorldMap.Y(i);
                    for (int k = 0; k < 4; k++) if (WorldMap.In(x + WorldMap.DX4[k], y + WorldMap.DY4[k])) DirtyTrees.Add(WorldMap.Idx(x + WorldMap.DX4[k], y + WorldMap.DY4[k]));
                }
                S.roadPaths.AddRange(path);
                S.roadPaths.Add(-1);   // Trenner zwischen Strecken (für die Fuhrwerke)
            }
            foreach (var o in SettlementsOf(s.faction))
                if (o.id != s.id && RoadNear(o.x, o.y + 2) && RoadNear(s.x, s.y + 2) && Dist(s.x, s.y, o.x, o.y) <= 40)
                {
                    int key = Math.Min(s.id, o.id) * 100000 + Math.Max(s.id, o.id);
                    if (!S.roadLinks.Contains(key)) S.roadLinks.Add(key);
                }
            terrainCounts.Remove(s.id);
            RoadsVersion++;
            Notify(Loc.F("Neue Straßen bei {0}: {1} Felder.", s.name, n), 1);
            Hint("roads", Loc.T("Straßen! Höfe und Minen an der Straße verlieren nur halb so viel Ertrag durch lange Wege, Heere marschieren darauf schneller, und verbundene Orte handeln miteinander – seht die Fuhrwerke."));
            return true;
        }

        /// <summary>Täglich: Handel zwischen per Straße verbundenen Orten.</summary>
        void RoadsDay()
        {
            var p = Player;
            if (p == null || S.roadLinks.Count == 0) return;
            int links = 0;
            foreach (int key in S.roadLinks)
            {
                var a = St(key / 100000); var b = St(key % 100000);
                if (a != null && b != null && a.faction == p.id && b.faction == p.id) links++;
            }
            if (links > 0) Add(p, (int)Res.Gold, links * 0.8f, Delta(p.id));
        }

        /// <summary>Die Strecken für die Darstellung der Fuhrwerke.</summary>
        public List<List<int>> RoadRoutes()
        {
            var res = new List<List<int>>();
            var cur = new List<int>();
            foreach (int i in S.roadPaths)
            {
                if (i < 0) { if (cur.Count > 3) res.Add(cur); cur = new List<int>(); }
                else cur.Add(i);
            }
            if (cur.Count > 3) res.Add(cur);
            return res;
        }
    }
}
