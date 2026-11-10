using System;
using System.Collections.Generic;
using System.Linq;

namespace PixelKingdom
{
    public struct Notice { public string Text; public int Kind; }

    public class DialogLine
    {
        public string Speaker, Portrait, Text;
        public DialogLine(string speaker, string portrait, string text) { Speaker = speaker; Portrait = portrait; Text = text; }
    }

    /// <summary>
    /// Kern der Simulation. Komplett unabhängig von Unity (testbar ohne Editor).
    /// Aufgeteilt in mehrere partielle Dateien: Wirtschaft, Militär, Diplomatie, KI, Ereignisse, Story.
    /// </summary>
    public partial class Game
    {
        public GameState S;
        public WorldMap Map;
        public Random Rng;

        readonly Dictionary<int, FactionData> fac = new Dictionary<int, FactionData>();
        readonly Dictionary<int, SettlementData> set = new Dictionary<int, SettlementData>();
        readonly Dictionary<int, ArmyData> arm = new Dictionary<int, ArmyData>();

        // Laufzeit-Caches (nicht gespeichert)
        readonly Dictionary<int, float[]> lastDelta = new Dictionary<int, float[]>();
        readonly Dictionary<int, int[]> terrainCounts = new Dictionary<int, int[]>();
        readonly Dictionary<int, int> radiusCache = new Dictionary<int, int>();

        public readonly Queue<Notice> Notices = new Queue<Notice>();
        public readonly Queue<DialogLine> Dialog = new Queue<DialogLine>();
        public readonly HashSet<int> DirtyTiles = new HashSet<int>();
        public readonly HashSet<int> DirtyTrees = new HashSet<int>();
        public bool SettlementsDirty = true;
        public bool Offline;      // während der Offline-Simulation
        public bool Headless;     // Tests: keine Dialoge

        public FactionData Player => fac[S.playerFaction];
        public int PlayerId => S.playerFaction;
        public IReadOnlyList<FactionData> Factions => S.factions;
        public IReadOnlyList<SettlementData> Settlements => S.settlements;
        public IReadOnlyList<ArmyData> Armies => S.armies;

        public FactionData F(int id) => id >= 0 && fac.TryGetValue(id, out var f) ? f : null;
        public SettlementData St(int id) => id >= 0 && set.TryGetValue(id, out var s) ? s : null;
        public ArmyData A(int id) => id >= 0 && arm.TryGetValue(id, out var a) ? a : null;

        public IEnumerable<SettlementData> SettlementsOf(int fid) { foreach (var s in S.settlements) if (s.faction == fid) yield return s; }
        public IEnumerable<ArmyData> ArmiesOf(int fid) { foreach (var a in S.armies) if (a.faction == fid) yield return a; }
        public int CountSettlements(int fid) { int n = 0; foreach (var s in S.settlements) if (s.faction == fid) n++; return n; }
        public float TotalPop(int fid) { float p = 0; foreach (var s in S.settlements) if (s.faction == fid) p += s.pop; return p; }
        public float[] Delta(int fid) => lastDelta.TryGetValue(fid, out var d) ? d : (lastDelta[fid] = new float[ResInfo.Count]);
        public int NewId() => S.nextId++;

        // ==================================================================
        //  Erzeugen / Laden
        // ==================================================================
        public static Game NewGame(int seed)
        {
            var g = new Game();
            g.S = new GameState { seed = seed };
            g.Map = WorldGenerator.Generate(seed);
            g.Rng = new Random(seed ^ 0x5f3759df);
            WorldSetup.Populate(g);
            g.Reindex();
            g.RecomputeTerritory();
            foreach (var s in g.SettlementsOf(g.PlayerId)) g.Reveal(s.x, s.y, 13);
            g.InitStory();
            return g;
        }

        public static Game FromState(GameState state)
        {
            var g = new Game { S = state };
            g.Map = WorldGenerator.Generate(state.seed);
            g.Rng = new Random(state.seed ^ state.day * 7919);
            g.Map.DiscoveredFromString(state.discovered);
            g.Map.TreesFromString(state.trees);
            g.ApplyExtraDeposits();
            g.Reindex();
            g.RecomputeTerritory();
            return g;
        }

        public void PrepareSave()
        {
            S.discovered = Map.DiscoveredToString();
            S.trees = Map.TreesToString();
            if (S.log.Count > 200) S.log.RemoveRange(0, S.log.Count - 200);
        }

        public void ApplyExtraDeposits()
        {
            if (S.extraDeposits == null) S.extraDeposits = new List<int>();
            for (int k = 0; k + 1 < S.extraDeposits.Count; k += 2)
            {
                int i = S.extraDeposits[k];
                if (i >= 0 && i < Map.deposit.Length) { Map.deposit[i] = (byte)S.extraDeposits[k + 1]; Map.trees[i] = 0; Map.baseTrees[i] = 0; }
            }
        }

        public void Reindex()
        {
            fac.Clear(); set.Clear(); arm.Clear();
            foreach (var f in S.factions)
            {
                fac[f.id] = f;
                if (f.res == null || f.res.Length != ResInfo.Count) f.res = new float[ResInfo.Count];
            }
            foreach (var s in S.settlements)
            {
                set[s.id] = s;
                // Arrays nach Versionsänderungen anpassen
                if (s.buildings == null || s.buildings.Length != Buildings.Count) { var b = new int[Buildings.Count]; if (s.buildings != null) Array.Copy(s.buildings, b, Math.Min(b.Length, s.buildings.Length)); s.buildings = b; }
                if (s.garrison == null || s.garrison.Length != Units.Count) { var u = new int[Units.Count]; if (s.garrison != null) Array.Copy(s.garrison, u, Math.Min(u.Length, s.garrison.Length)); s.garrison = u; }
                if (s.queue == null) s.queue = new List<QueueItem>();
            }
            foreach (var a in S.armies)
            {
                arm[a.id] = a;
                if (a.units == null || a.units.Length != Units.Count) { var u = new int[Units.Count]; if (a.units != null) Array.Copy(a.units, u, Math.Min(u.Length, a.units.Length)); a.units = u; }
                if (a.path == null) a.path = new List<int>();
            }
            if (S.priceDrift == null || S.priceDrift.Length != ResInfo.Count) S.priceDrift = new float[ResInfo.Count];
            SettlementsDirty = true;
        }

        public void AddSettlement(SettlementData s) { S.settlements.Add(s); set[s.id] = s; SettlementsDirty = true; }
        public void AddFaction(FactionData f) { S.factions.Add(f); fac[f.id] = f; }
        public void AddArmy(ArmyData a) { S.armies.Add(a); arm[a.id] = a; }
        public void RemoveArmy(ArmyData a) { S.armies.Remove(a); arm.Remove(a.id); }

        // ==================================================================
        //  Zeit
        // ==================================================================
        public Season Season => Calendar.SeasonOf(S.day);

        /// <summary>Lässt die Welt um dtDays Tage weiterlaufen.</summary>
        public void Advance(float dtDays)
        {
            if (dtDays <= 0) return;
            // in kleinen Schritten, damit Armeen sauber laufen
            while (dtDays > 0)
            {
                float step = Math.Min(dtDays, 0.1f);
                dtDays -= step;
                UpdateArmies(step);
                S.dayProgress += step;
                if (S.dayProgress >= 1f)
                {
                    S.dayProgress -= 1f;
                    DailyUpdate();
                }
            }
        }

        void DailyUpdate()
        {
            S.day++;
            foreach (var f in S.factions)
            {
                if (!f.alive) continue;
                var d = Delta(f.id);
                Array.Clear(d, 0, d.Length);
            }

            // Wirtschaft aller Siedlungen
            var list = S.settlements.ToArray();
            foreach (var s in list) SettlementDay(s);
            foreach (var f in S.factions) if (f.alive) FactionDay(f);

            if (S.day % 2 == 0) RecomputeTerritoryIfNeeded();
            DiplomacyDay();
            if (!Offline)
            {
                foreach (var f in S.factions)
                    if (f.alive && !f.isPlayer && (S.day + f.id) % 3 == 0) AIThink(f);
            }
            else
            {
                foreach (var f in S.factions)
                    if (f.alive && !f.isPlayer && (S.day + f.id) % 3 == 0) AIEconomyOnly(f);
            }
            if (!Offline) EventsDay();
            PoisDay();
            FrontierDay();
            NavyDay();
            CampsDay();
            MythDay();
            ComputeProblems();
            ProgressDay();
            StoryDay();
            MarketDrift();
            CheckEliminations();
        }

        /// <summary>
        /// Fortschritt während der Abwesenheit: Wirtschaft, Bau und Wachstum laufen weiter,
        /// aber keine Kriegserklärungen oder Ereignisse. So lohnt sich auch gelegentliches Spielen.
        /// </summary>
        public string SimulateOffline(int days)
        {
            if (days <= 0) return null;
            var p = Player;
            var before = (float[])p.res.Clone();
            float popBefore = TotalPop(p.id);
            int buildBefore = SettlementsOf(p.id).Sum(s => s.buildings.Sum());
            int dayBefore = S.day;
            Offline = true;
            try { for (int d = 0; d < days; d++) Advance(1f); }
            finally { Offline = false; }
            var parts = new List<string>();
            parts.Add(Loc.F("{0} Tage sind vergangen.", (S.day - dayBefore)));
            parts.Add(Loc.F("Bevölkerung: {0} -> {1}", ((int)popBefore), ((int)TotalPop(p.id))));
            int built = SettlementsOf(p.id).Sum(s => s.buildings.Sum()) - buildBefore;
            if (built > 0) parts.Add(Loc.F("{0} Gebäude fertiggestellt.", (built)));
            for (int r = 0; r < ResInfo.Count; r++)
            {
                int diff = (int)(p.res[r] - before[r]);
                if (diff != 0) parts.Add($"{ResInfo.Name(r)}: {(diff > 0 ? "+" : "")}{diff}");
            }
            return string.Join("\n", parts);
        }

        // ==================================================================
        //  Meldungen
        // ==================================================================
        public void Notify(string text, int kind = 0, bool log = true)
        {
            if (log) S.log.Add(new LogEntry { day = S.day, text = text, kind = kind });
            if (!Offline) Notices.Enqueue(new Notice { Text = text, Kind = kind });
        }

        public void Say(string speaker, string portrait, string text)
        {
            if (Headless) return; // Offline-Dialoge werden nach dem Laden gezeigt
            Dialog.Enqueue(new DialogLine(speaker, portrait, text));
        }

        // ==================================================================
        //  Gebiete & Erkundung
        // ==================================================================
        public int Radius(SettlementData s)
        {
            int r = 2 + (int)(Math.Sqrt(Math.Max(0, s.pop)) / 3.2);
            if (s.buildings[(int)BType.Burg] > 0) r++;
            if (s.isCapitalOfEmperor) r++;
            return Math.Min(r, 8);
        }

        void RecomputeTerritoryIfNeeded()
        {
            bool changed = false;
            foreach (var s in S.settlements)
            {
                int r = Radius(s);
                if (!radiusCache.TryGetValue(s.id, out int old) || old != r) { changed = true; break; }
            }
            if (changed || SettlementsDirty) RecomputeTerritory();
        }

        public void RecomputeTerritory()
        {
            int N = WorldMap.W * WorldMap.H;
            var oldOwner = (int[])Map.owner.Clone();
            var bestD = new float[N];
            for (int i = 0; i < N; i++) { bestD[i] = float.MaxValue; Map.owner[i] = -1; Map.ownerSettlement[i] = -1; }
            radiusCache.Clear();
            foreach (var s in S.settlements)
            {
                int r = Radius(s);
                radiusCache[s.id] = r;
                for (int y = s.y - r; y <= s.y + r; y++)
                    for (int x = s.x - r; x <= s.x + r; x++)
                    {
                        if (!WorldMap.In(x, y)) continue;
                        float d = (x - s.x) * (x - s.x) + (y - s.y) * (y - s.y);
                        if (d > r * r + 0.5f) continue;
                        int i = WorldMap.Idx(x, y);
                        if (Map.IsSea(i)) continue;
                        if (d < bestD[i]) { bestD[i] = d; Map.owner[i] = s.faction; Map.ownerSettlement[i] = s.id; }
                    }
            }
            for (int i = 0; i < N; i++) if (oldOwner[i] != Map.owner[i]) DirtyTiles.Add(i);
            terrainCounts.Clear();
            SettlementsDirty = false;
            RebuildOccupancy();
            foreach (var s in S.settlements) if (PlacedMismatch(s)) EnsurePlaced(s);
        }

        /// <summary>Zählt im Gebiet der Siedlung: [fruchtbar, Bäume, Hügel, Berg, Küste, Wasser, Eisen, Gold, Stein]</summary>
        public int[] Terrain(SettlementData s)
        {
            if (terrainCounts.TryGetValue(s.id, out var c)) return c;
            c = new int[9];
            int r = Radius(s);
            for (int y = s.y - r; y <= s.y + r; y++)
                for (int x = s.x - r; x <= s.x + r; x++)
                {
                    if (!WorldMap.In(x, y)) continue;
                    int i = WorldMap.Idx(x, y);
                    float d = (x - s.x) * (x - s.x) + (y - s.y) * (y - s.y);
                    if (d > r * r + 0.5f) continue;
                    if (Map.ownerSettlement[i] != s.id && !Map.IsWater(i)) continue;
                    var b = Map.biome[i];
                    if (b == Biome.Fertile) c[0] += 2;
                    else if (b == Biome.Grass || b == Biome.Steppe) c[0] += 1;
                    c[1] += Map.trees[i];
                    if (b == Biome.Hills) c[2]++;
                    if (b == Biome.Mountain || b == Biome.Snow) { c[3]++; c[2]++; }
                    if (Map.coast[i]) c[4]++;
                    if (Map.IsWater(i) || Map.river[i]) c[5]++;
                    if (Map.deposit[i] == WorldMap.DepIron) c[6]++;
                    if (Map.deposit[i] == WorldMap.DepGold) c[7]++;
                    if (Map.deposit[i] == WorldMap.DepStone) c[8]++;
                }
            terrainCounts[s.id] = c;
            return c;
        }

        public void InvalidateTerrain(int settlementId) => terrainCounts.Remove(settlementId);

        public void Reveal(int cx, int cy, int r)
        {
            for (int y = cy - r; y <= cy + r; y++)
                for (int x = cx - r; x <= cx + r; x++)
                {
                    if (!WorldMap.In(x, y)) continue;
                    if ((x - cx) * (x - cx) + (y - cy) * (y - cy) > r * r) continue;
                    int i = WorldMap.Idx(x, y);
                    if (Map.disc[i]) continue;
                    Map.disc[i] = true;
                    DirtyTiles.Add(i);
                    int so = Map.ownerSettlement[i];
                    if (so >= 0)
                    {
                        var st = St(so);
                        if (st != null) MeetFaction(st.faction);
                    }
                }
        }

        public bool IsDiscovered(int x, int y) => WorldMap.In(x, y) && Map.disc[WorldMap.Idx(x, y)];

        public void MeetFaction(int fid)
        {
            var f = F(fid);
            if (f == null || f.isPlayer || f.knownToPlayer != 0) return;
            f.knownToPlayer = 1;
            Notify(Loc.F("Neue Bekanntschaft: {0} ({1}).", (f.name), (f.leader)), 0);
            OnFactionMet(f);
        }

        void CheckEliminations()
        {
            foreach (var f in S.factions)
            {
                if (!f.alive) continue;
                if (CountSettlements(f.id) > 0) continue;
                f.alive = false;
                foreach (var a in ArmiesOf(f.id).ToList()) RemoveArmy(a);
                if (f.isPlayer)
                {
                    Notify(Loc.T("Euer Reich ist gefallen..."), 2);
                    continue;
                }
                Notify(Loc.F("{0} existiert nicht mehr.", (f.name)), 3);
            }
        }

        // ==================================================================
        //  Hilfen
        // ==================================================================
        public static float Dist(int x1, int y1, int x2, int y2) => (float)Math.Sqrt((x1 - x2) * (x1 - x2) + (y1 - y2) * (y1 - y2));

        public SettlementData NearestSettlementOf(int fid, float x, float y)
        {
            SettlementData best = null; float bd = float.MaxValue;
            foreach (var s in S.settlements)
            {
                if (s.faction != fid) continue;
                float d = (s.x - x) * (s.x - x) + (s.y - y) * (s.y - y);
                if (d < bd) { bd = d; best = s; }
            }
            return best;
        }

        public SettlementData SettlementAt(int x, int y)
        {
            foreach (var s in S.settlements) if (s.x == x && s.y == y) return s;
            return null;
        }

        public SettlementData Capital(FactionData f)
        {
            var c = St(f.capital);
            if (c != null && c.faction == f.id) return c;
            SettlementData best = null;
            foreach (var s in SettlementsOf(f.id)) if (best == null || s.pop > best.pop) best = s;
            f.capital = best != null ? best.id : -1;
            return best;
        }

        public string DateString()
        {
            return Loc.F("Tag {0}, {1}, Jahr {2}", (Calendar.DayOfSeason(S.day)), (Calendar.SeasonNames[(int)Season]), (Calendar.Year(S.day)));
        }

        public FactionData Emperor { get { foreach (var f in S.factions) if (f.isEmperor) return f; return null; } }
    }
}
