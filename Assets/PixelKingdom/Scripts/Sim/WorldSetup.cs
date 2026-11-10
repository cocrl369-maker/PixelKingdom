using System;
using System.Collections.Generic;
using System.Linq;

namespace PixelKingdom
{
    /// <summary>Verteilt Spieler, Fürsten, freie Dörfer und den Kaiser auf der Welt.</summary>
    public static class WorldSetup
    {
        class Site { public int x, y; public float score; public int cont; }

        public static void Populate(Game g)
        {
            var map = g.Map; var r = g.Rng;
            var used = new HashSet<string>();
            var sites = FindSites(map, r);
            var taken = new List<(int x, int y)>();

            bool Free(Site s, float minD) => taken.All(t => Game.Dist(t.x, t.y, s.x, s.y) >= minD);
            Site Pick(Func<Site, bool> pred, float minD)
            {
                foreach (var s in sites) if (pred(s) && Free(s, minD)) { taken.Add((s.x, s.y)); return s; }
                return null;
            }

            int colorIdx = 1;
            FactionData NewFaction(string name, string leader, int cont, int culture)
            {
                var f = new FactionData
                {
                    id = g.NewId(), name = name, leader = leader, continent = cont, culture = culture,
                    color = colorIdx++ % 14,
                    aggression = (float)r.NextDouble(), greed = (float)r.NextDouble(), honor = (float)r.NextDouble(),
                    ambition = (float)r.NextDouble(), loyalty = (float)r.NextDouble(),
                };
                if (f.color == 0) f.color = colorIdx++ % 14;
                f.res = new float[] { 150, 120, 60, 15, 120, 10 };
                g.AddFaction(f);
                return f;
            }

            SettlementData NewSettlement(FactionData f, Site s, float pop, bool capital)
            {
                var st = new SettlementData
                {
                    id = g.NewId(), faction = f.id, x = s.x, y = s.y, pop = pop,
                    name = NameGen.Place(r, f.culture, used), founded = 0, originalFaction = f.id,
                    taxRate = 0.1f, happiness = 55,
                };
                g.AddSettlement(st);
                if (capital) f.capital = st.id;
                return st;
            }

            // ---------------- Spieler -----------------------------------------------
            var home = sites.Where(s => s.cont == 0).ToList();
            float minX = home.Min(s => s.x), maxX = home.Max(s => s.x), minY = home.Min(s => s.y), maxY = home.Max(s => s.y);
            // Start im Südwesten der Heimat
            var pSite = home.Where(s => s.x < minX + (maxX - minX) * 0.45f && s.y < minY + (maxY - minY) * 0.55f && GoodStart(map, s))
                            .OrderByDescending(s => s.score).FirstOrDefault()
                        ?? home.Where(s => GoodStart(map, s)).OrderByDescending(s => s.score).FirstOrDefault() ?? home[0];
            taken.Add((pSite.x, pSite.y));
            var player = new FactionData { id = g.NewId(), name = Loc.T("Euer Reich"), leader = Loc.T("Ihr"), isPlayer = true, color = 0, continent = 0, culture = 0,
                honor = 0.7f, res = new float[] { 90, 80, 20, 0, 60, 0 } };
            g.AddFaction(player);
            g.S.playerFaction = player.id;
            var pst = NewSettlement(player, pSite, 22, true);
            player.name = Loc.T("Dorf ") + pst.name;
            pst.buildings[(int)BType.Wohnhaus] = 2;
            pst.buildings[(int)BType.Bauernhof] = 1;
            pst.garrison[(int)UType.Miliz] = 4;
            pst.happiness = 60;
            EnsureDeposit(g, pSite.x, pSite.y, WorldMap.DepIron);
            EnsureDeposit(g, pSite.x, pSite.y, WorldMap.DepStone);

            // ---------------- Kaiser ----------------------------------------------------
            var eSite = home.OrderByDescending(s => Game.Dist(s.x, s.y, pSite.x, pSite.y) * 0.9f + s.score * 2f)
                            .First(s => Free(s, 44));
            taken.Add((eSite.x, eSite.y));
            var emp = NewFaction(Loc.T("Kaiserreich Valdor"), Loc.T("Kaiser Malrik der Eiserne"), 0, 0);
            emp.isEmperor = true; emp.color = 13; emp.aggression = 0.85f; emp.greed = 0.95f; emp.honor = 0.1f; emp.ambition = 1f; emp.loyalty = 1f;
            emp.res = new float[] { 900, 700, 600, 200, 1500, 300 };
            emp.rank = (int)Rank.Koenigreich;
            var cap = NewSettlement(emp, eSite, 420, true);
            cap.name = Loc.T("Valdoran"); cap.isCapitalOfEmperor = true; cap.taxRate = 0.25f;
            int empTowns = 0;
            foreach (var s in sites)
            {
                if (empTowns >= 7) break;
                if (s.cont != 0 || Game.Dist(s.x, s.y, eSite.x, eSite.y) > 46 || !Free(s, 14)) continue;
                taken.Add((s.x, s.y));
                var st = NewSettlement(emp, s, 90 + r.Next(80), false); st.taxRate = 0.22f;
                empTowns++;
            }

            // ---------------- Fürsten von Mittland ---------------------------------------
            for (int k = 0; k < 11; k++)
            {
                var c = Pick(s => s.cont == 0 && Game.Dist(s.x, s.y, pSite.x, pSite.y) > 26 && Game.Dist(s.x, s.y, eSite.x, eSite.y) > 38, 28);
                if (c == null) break;
                var f = NewFaction("", NameGen.Leader(r, 0), 0, 0);
                var capS = NewSettlement(f, c, 55 + r.Next(70), true);
                f.name = NameGen.RealmName(0, capS.name);
                // Fürsten nahe dem Kaiser sind treuer
                float de = Game.Dist(c.x, c.y, eSite.x, eSite.y);
                f.loyalty = Clamp01(f.loyalty * 0.6f + (de < 90 ? 0.4f : 0.05f));
                int extra = r.Next(1, 3);
                for (int e = 0; e < extra; e++)
                {
                    var s2 = Pick(s => s.cont == 0 && Game.Dist(s.x, s.y, c.x, c.y) < 24, 12);
                    if (s2 == null) break;
                    NewSettlement(f, s2, 25 + r.Next(35), false);
                }
            }

            // ---------------- Freie Dörfer -------------------------------------------------
            for (int k = 0; k < 10; k++)
            {
                var c = Pick(s => s.cont == 0 && Game.Dist(s.x, s.y, pSite.x, pSite.y) > 15, 15);
                if (c == null) break;
                var f = NewFaction("", Loc.T("Dorfältester ") + NameGen.Leader(r, 0).Split(' ')[1], 0, 0);
                f.isFree = true; f.aggression *= 0.2f; f.ambition *= 0.2f; f.loyalty *= 0.5f;
                f.res = new float[] { 80, 60, 20, 0, 40, 0 };
                var st = NewSettlement(f, c, 18 + r.Next(18), true);
                f.name = Loc.T("Freidorf ") + st.name;
            }

            // ---------------- Übersee ---------------------------------------------------------
            for (int cont = 1; cont < WorldMap.ContinentCount; cont++)
            {
                // Kultur (Namen): die beiden großen Inseln im Osten ähneln den Nordlanden bzw. dem Inselbund
                int cul = cont >= 4 ? 4 : cont;   // die großen Inseln: das Volk der Saelaner
                bool island = cont >= 4;
                int lords = cont == 3 ? 2 : island ? 5 : 4;
                for (int k = 0; k < lords; k++)
                {
                    var c = Pick(s => s.cont == cont, cont == 3 ? 12 : island ? 18 : 24);
                    if (c == null) break;
                    var f = NewFaction("", NameGen.Leader(r, cul), cont, cul);
                    f.loyalty = 0; // kennen den Kaiser nicht
                    var capS = NewSettlement(f, c, 60 + r.Next(90), true);
                    f.name = NameGen.RealmName(cul, capS.name);
                    int extra = r.Next(1, 4);
                    for (int e = 0; e < extra; e++)
                    {
                        var s2 = Pick(s => s.cont == cont && Game.Dist(s.x, s.y, c.x, c.y) < 26, 12);
                        if (s2 == null) break;
                        NewSettlement(f, s2, 30 + r.Next(50), false);
                    }
                }
                for (int k = 0; k < (island ? 3 : 2); k++)
                {
                    var c = Pick(s => s.cont == cont, 14);
                    if (c == null) break;
                    var f = NewFaction("", Loc.T("Ältester ") + NameGen.Leader(r, cul).Split(' ')[1], cont, cul);
                    f.isFree = true; f.aggression *= 0.2f; f.loyalty = 0;
                    var st = NewSettlement(f, c, 20 + r.Next(20), true);
                    f.name = Loc.T("Freidorf ") + st.name;
                }
            }

            g.Reindex();
            g.RecomputeTerritory();

            // Gebäude & Garnisonen der KI-Siedlungen vorbelegen
            foreach (var s in g.S.settlements)
            {
                if (s.faction == player.id) continue;
                g.SeedSettlement(s);
            }
            // Ränge der KI
            foreach (var f in g.S.factions)
            {
                if (f.isPlayer || f.isEmperor) continue;
                float pop = g.TotalPop(f.id);
                f.rank = pop > 300 ? 3 : pop > 140 ? 2 : pop > 45 ? 1 : 0;
                f.prestige = Ranks.Get(f.rank).Prestige;
            }
            // Kaiserliche Garde in Valdoran
            cap.garrison[(int)UType.Speer] = 50; cap.garrison[(int)UType.Bogen] = 30; cap.garrison[(int)UType.Ritter] = 20;
            cap.garrison[(int)UType.Miliz] = 30; cap.buildings[(int)BType.Mauer] = 2; cap.buildings[(int)BType.Burg] = 1;
            cap.buildings[(int)BType.Kathedrale] = 1;

            g.InitRelations();
            g.PlacePois();
        }

        static float Clamp01(float v) => v < 0 ? 0 : v > 1 ? 1 : v;

        /// <summary>Sorgt dafür, dass der Spieler ein Vorkommen in Reichweite hat (wird im Spielstand gemerkt).</summary>
        static void EnsureDeposit(Game g, int cx, int cy, byte kind)
        {
            var map = g.Map;
            if (map.CountAround(cx, cy, 4, i => map.deposit[i] == kind) > 0) return;
            int best = -1; float bd = float.MaxValue;
            for (int y = cy - 4; y <= cy + 4; y++)
                for (int x = cx - 4; x <= cx + 4; x++)
                {
                    if (!WorldMap.In(x, y)) continue;
                    float d = (x - cx) * (x - cx) + (y - cy) * (y - cy);
                    if (d < 4 || d > 13) continue;
                    int i = WorldMap.Idx(x, y);
                    if (!map.Buildable(i) || map.deposit[i] != 0) continue;
                    var b = map.biome[i];
                    float score = d + (b == Biome.Hills ? -20 : 0) + (b == Biome.Forest || b == Biome.Pine ? 6 : 0);
                    if (score < bd) { bd = score; best = i; }
                }
            if (best < 0) return;
            g.S.extraDeposits.Add(best); g.S.extraDeposits.Add(kind);
            map.deposit[best] = kind; map.trees[best] = 0; map.baseTrees[best] = 0;
        }

        /// <summary>Startplatz des Spielers: Wald, fruchtbares Land und Hügel in Reichweite.</summary>
        static bool GoodStart(WorldMap map, Site s)
        {
            int forest = map.CountAround(s.x, s.y, 2, i => map.biome[i] == Biome.Forest || map.biome[i] == Biome.Pine);
            int fertile = map.CountAround(s.x, s.y, 3, i => map.biome[i] == Biome.Fertile || map.biome[i] == Biome.Grass);
            int hills = map.CountAround(s.x, s.y, 3, i => map.biome[i] == Biome.Hills || map.biome[i] == Biome.Mountain);
            return forest >= 3 && fertile >= 6 && hills >= 1;
        }

        static List<Site> FindSites(WorldMap map, Random r)
        {
            var list = new List<Site>();
            for (int y = 4; y < WorldMap.H - 4; y += 2)
                for (int x = 4; x < WorldMap.W - 4; x += 2)
                {
                    int i = WorldMap.Idx(x, y);
                    if (!map.Buildable(i)) continue;
                    float score = 0; int land = 0;
                    for (int dy = -4; dy <= 4; dy++)
                        for (int dx = -4; dx <= 4; dx++)
                        {
                            int nx = x + dx, ny = y + dy;
                            if (!WorldMap.In(nx, ny) || dx * dx + dy * dy > 17) continue;
                            int ni = WorldMap.Idx(nx, ny);
                            var b = map.biome[ni];
                            if (!map.IsWater(ni)) land++;
                            if (b == Biome.Fertile) score += 1.2f;
                            else if (b == Biome.Grass) score += 0.6f;
                            else if (b == Biome.Forest || b == Biome.Pine) score += 0.5f;
                            else if (b == Biome.Hills) score += 0.5f;
                            else if (b == Biome.Mountain) score += 0.3f;
                            else if (b == Biome.Desert) score -= 0.3f;
                            if (map.river[ni]) score += 0.4f;
                        }
                    if (land < 25) continue;
                    if (map.coast[i]) score += 3;
                    score += (float)r.NextDouble() * 6f;
                    list.Add(new Site { x = x, y = y, score = score, cont = map.cont[i] });
                }
            list.Sort((a, b) => b.score.CompareTo(a.score));
            return list;
        }
    }
}
