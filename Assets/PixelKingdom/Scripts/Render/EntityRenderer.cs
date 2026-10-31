using System.Collections.Generic;
using UnityEngine;

namespace PixelKingdom
{
    /// <summary>Zeichnet Siedlungen (aus ihren tatsächlichen Gebäuden zusammengesetzt) und Armeen.</summary>
    public class EntityRenderer : MonoBehaviour
    {
        class View { public GameObject go; public Mesh mesh; public int sig = int.MinValue; }

        Game game;
        TerrainRenderer terrain;
        Material mat;
        readonly Dictionary<int, View> settlementViews = new Dictionary<int, View>();
        readonly Dictionary<int, View> armyViews = new Dictionary<int, View>();
        readonly MeshBuilder mb = new MeshBuilder();
        LineRenderer pathLine;
        float refreshTimer;
        int paletteIdx = -1;

        public int SelectedArmy = -1;

        public void Init(Game g, TerrainRenderer t, Material m)
        {
            game = g; terrain = t; mat = m;
            foreach (var v in settlementViews.Values) Destroy(v.go);
            foreach (var v in armyViews.Values) Destroy(v.go);
            settlementViews.Clear(); armyViews.Clear();
            if (pathLine == null)
            {
                var go = new GameObject("Pfad");
                go.transform.SetParent(transform, false);
                pathLine = go.AddComponent<LineRenderer>();
                pathLine.sharedMaterial = mat;
                pathLine.widthMultiplier = 0.08f;
                pathLine.numCapVertices = 2;
                pathLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            paletteIdx = Palette.Index;
        }

        public void ForceRefresh()
        {
            foreach (var v in settlementViews.Values) v.sig = int.MinValue;
            foreach (var v in armyViews.Values) v.sig = int.MinValue;
        }

        View Get(Dictionary<int, View> dict, int id, string name)
        {
            if (dict.TryGetValue(id, out var v)) return v;
            v = new View { go = new GameObject(name), mesh = new Mesh() };
            v.go.transform.SetParent(transform, false);
            v.go.AddComponent<MeshFilter>().sharedMesh = v.mesh;
            var mr = v.go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            dict[id] = v;
            return v;
        }

        void Update()
        {
            if (game == null) return;
            if (paletteIdx != Palette.Index) { paletteIdx = Palette.Index; ForceRefresh(); }
            refreshTimer -= Time.unscaledDeltaTime;
            if (refreshTimer <= 0 || terrain.PendingChunks > 0)
            {
                refreshTimer = 0.4f;
                RefreshSettlements();
            }
            UpdateArmies();
            UpdatePath();
        }

        // ------------------------------------------------------------------
        //  Siedlungen
        // ------------------------------------------------------------------
        void RefreshSettlements()
        {
            var alive = new HashSet<int>();
            foreach (var s in game.Settlements)
            {
                alive.Add(s.id);
                bool visible = game.IsDiscovered(s.x, s.y);
                var v = Get(settlementViews, s.id, "Siedlung " + s.name);
                v.go.SetActive(visible);
                if (!visible) continue;
                int sig = Signature(s);
                if (sig == v.sig) continue;
                v.sig = sig;
                BuildSettlement(s);
                mb.ToMesh(v.mesh);
            }
            var remove = new List<int>();
            foreach (var kv in settlementViews) if (!alive.Contains(kv.Key)) { Destroy(kv.Value.go); remove.Add(kv.Key); }
            foreach (var k in remove) settlementViews.Remove(k);
        }

        int Signature(SettlementData s)
        {
            unchecked
            {
                int h = s.faction * 7919 + CenterLevel(s) * 31 + (int)(s.pop / 30) * 131 + terrain.PendingChunks * 0;
                for (int i = 0; i < s.buildings.Length; i++) h = h * 17 + s.buildings[i];
                // Nachbarschaft entdeckt? (Höhen ändern sich beim Aufdecken)
                h = h * 3 + (game.IsDiscovered(s.x + 3, s.y) ? 1 : 0) + (game.IsDiscovered(s.x - 3, s.y) ? 2 : 0);
                return h;
            }
        }

        int CenterLevel(SettlementData s)
        {
            var f = game.F(s.faction);
            if (s.isCapitalOfEmperor) return 4;
            if (s.pop > 160 || (f != null && f.capital == s.id && f.rank >= 3)) return 3;
            if (s.pop > 70 || (f != null && f.capital == s.id && f.rank >= 2)) return 2;
            if (s.pop > 35) return 1;
            return 0;
        }

        struct Slot { public Vector3 pos; public int tile; }

        void BuildSettlement(SettlementData s)
        {
            mb.Clear();
            var pal = Palette.Current;
            var map = game.Map;
            var f = game.F(s.faction);
            Color fc = f != null ? pal.FactionColor(f.color) : Color.white;

            // Kacheln im Umkreis sortiert nach Entfernung
            var tiles = new List<(int i, float d)>();
            for (int y = s.y - 4; y <= s.y + 4; y++)
                for (int x = s.x - 4; x <= s.x + 4; x++)
                {
                    if (!WorldMap.In(x, y)) continue;
                    float d = Mathf.Sqrt((x - s.x) * (x - s.x) + (y - s.y) * (y - s.y)) + TerrainRenderer.Hash(x, y, 77) * 0.3f;
                    if (d > 4.3f) continue;
                    tiles.Add((WorldMap.Idx(x, y), d));
                }
            tiles.Sort((a, b) => a.d.CompareTo(b.d));
            var used = new HashSet<int>();
            int centerTile = WorldMap.Idx(s.x, s.y);
            used.Add(centerTile);

            bool Usable(int i) => map.Buildable(i) && !used.Contains(i) && (map.ownerSettlement[i] == s.id || map.ownerSettlement[i] == -1);

            // Zentrum
            var cpos = new Vector3(s.x + 0.5f, terrain.GroundY(s.x + 0.5f, s.y + 0.5f), s.y + 0.5f);
            mb.SetTransform(cpos, 0);
            Models.Center(mb, pal, CenterLevel(s), fc);
            if (s.buildings[(int)BType.Burg] > 0) Models.Castle(mb, pal, fc);
            mb.ResetTransform();

            int variant = s.id * 13;
            // Gebäude, die eine bestimmte Landschaft brauchen
            PlaceOnTerrain(s, BType.Bauernhof, Mathf.Min(12, s.buildings[(int)BType.Bauernhof]), i => map.biome[i] == Biome.Fertile || map.biome[i] == Biome.Grass || map.biome[i] == Biome.Steppe, tiles, used, ref variant, 1.0f);
            PlaceOnTerrain(s, BType.Fischerhuette, Mathf.Min(6, s.buildings[(int)BType.Fischerhuette]), i => map.coast[i], tiles, used, ref variant, 0.8f);
            PlaceOnTerrain(s, BType.Hafen, s.buildings[(int)BType.Hafen], i => map.coast[i], tiles, used, ref variant, 1.1f);
            PlaceOnTerrain(s, BType.Holzfaeller, Mathf.Min(6, s.buildings[(int)BType.Holzfaeller]), i => NearBiome(i, Biome.Forest, Biome.Pine), tiles, used, ref variant, 0.9f);
            PlaceOnTerrain(s, BType.Steinbruch, s.buildings[(int)BType.Steinbruch], i => NearBiome(i, Biome.Hills, Biome.Mountain), tiles, used, ref variant, 1f);
            PlaceOnTerrain(s, BType.Mine, s.buildings[(int)BType.Mine], i => NearBiome(i, Biome.Mountain, Biome.Snow), tiles, used, ref variant, 1f);

            // Große Gebäude: je eine ganze Kachel
            BType[] big = { BType.Kathedrale, BType.Markt, BType.Taverne, BType.Kapelle, BType.Kaserne, BType.Archerie, BType.Stall, BType.Werkstatt, BType.Lagerhaus };
            var bigTiles = new Queue<int>();
            foreach (var t in tiles) if (t.d < 2.9f && Usable(t.i)) bigTiles.Enqueue(t.i);
            foreach (var b in big)
            {
                int n = Mathf.Min(s.buildings[(int)b], 3);
                for (int k = 0; k < n && bigTiles.Count > 0; k++)
                {
                    int ti = bigTiles.Dequeue();
                    if (used.Contains(ti)) { k--; continue; }
                    used.Add(ti);
                    Place(b, TileCenter(ti, 0, 0), ref variant, b == BType.Kathedrale ? 1.2f : 1f);
                }
            }

            // Kleine Gebäude: je vier pro Kachel
            var small = new List<BType>();
            for (int k = 0; k < Mathf.Min(3, s.buildings[(int)BType.Brunnen]); k++) small.Add(BType.Brunnen);
            for (int k = 0; k < Mathf.Min(4, s.buildings[(int)BType.Schmiede]); k++) small.Add(BType.Schmiede);
            int houses = Mathf.Min(40, s.buildings[(int)BType.Wohnhaus] + 1);
            for (int k = 0; k < houses; k++) small.Add(BType.Wohnhaus);
            int si = 0;
            foreach (var t in tiles)
            {
                if (si >= small.Count) break;
                if (!Usable(t.i) || t.d > 3.6f) continue;
                used.Add(t.i);
                for (int q = 0; q < 4 && si < small.Count; q++)
                {
                    float ox = (q % 2 == 0 ? -0.23f : 0.23f) + (TerrainRenderer.Hash(t.i, q, 1) - 0.5f) * 0.08f;
                    float oz = (q < 2 ? -0.23f : 0.23f) + (TerrainRenderer.Hash(t.i, q, 2) - 0.5f) * 0.08f;
                    Place(small[si++], TileCenter(t.i, ox, oz), ref variant, 0.9f);
                }
            }

            // Mauern
            int walls = s.buildings[(int)BType.Mauer];
            if (walls > 0) Walls(s, walls, pal);
        }

        bool NearBiome(int i, Biome a, Biome b)
        {
            var map = game.Map;
            int x = WorldMap.X(i), y = WorldMap.Y(i);
            for (int k = 0; k < 8; k++)
            {
                int nx = x + WorldMap.DX8[k], ny = y + WorldMap.DY8[k];
                if (!WorldMap.In(nx, ny)) continue;
                var bb = map.biome[WorldMap.Idx(nx, ny)];
                if (bb == a || bb == b) return true;
            }
            return false;
        }

        void PlaceOnTerrain(SettlementData s, BType t, int count, System.Func<int, bool> pred, List<(int i, float d)> tiles, HashSet<int> used, ref int variant, float scale)
        {
            var map = game.Map;
            for (int k = 0; k < count; k++)
            {
                int found = -1;
                foreach (var tt in tiles)
                {
                    if (tt.d < 1.2f || used.Contains(tt.i) || !map.Buildable(tt.i)) continue;
                    if (map.ownerSettlement[tt.i] != s.id && map.ownerSettlement[tt.i] != -1) continue;
                    if (!pred(tt.i)) continue;
                    found = tt.i; break;
                }
                if (found < 0) return;
                used.Add(found);
                Place(t, TileCenter(found, 0, 0), ref variant, scale);
            }
        }

        Vector3 TileCenter(int i, float ox, float oz)
        {
            float x = WorldMap.X(i) + 0.5f + ox, z = WorldMap.Y(i) + 0.5f + oz;
            return new Vector3(x, terrain.GroundY(x, z), z);
        }

        void Place(BType t, Vector3 pos, ref int variant, float scale)
        {
            variant++;
            float rot = (variant * 37 % 4) * 90f + (TerrainRenderer.Hash(variant, 3, 5) - 0.5f) * 20f;
            mb.SetTransform(pos, rot, scale * 1.15f);
            Models.Building(mb, Palette.Current, t, variant);
            mb.ResetTransform();
        }

        void Walls(SettlementData s, int level, Palette pal)
        {
            float r = 2.35f;
            int segs = 28;
            var map = game.Map;
            var c = new Vector2(s.x + 0.5f, s.y + 0.5f);
            for (int k = 0; k < segs; k++)
            {
                if (k == segs / 4) continue; // Tor
                float a0 = k * Mathf.PI * 2 / segs, a1 = (k + 1) * Mathf.PI * 2 / segs;
                var p0 = c + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * r;
                var p1 = c + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * r;
                var mid = (p0 + p1) * 0.5f;
                int mx = Mathf.FloorToInt(mid.x), my = Mathf.FloorToInt(mid.y);
                if (!WorldMap.In(mx, my) || map.IsWater(WorldMap.Idx(mx, my))) continue;
                float y = terrain.GroundY(mid.x, mid.y);
                float len = Vector2.Distance(p0, p1) + 0.02f;
                float ang = -Mathf.Atan2(p1.y - p0.y, p1.x - p0.x) * Mathf.Rad2Deg;
                mb.SetTransform(new Vector3(mid.x, y, mid.y), ang);
                if (level == 1)
                {
                    mb.Box(new Vector3(0, -0.15f, 0), len, 0.36f, 0.05f, pal.Wood);
                }
                else
                {
                    mb.Box(new Vector3(0, -0.15f, 0), len, 0.45f, 0.08f, pal.Stone);
                    if (k % 4 == 0) { mb.ResetTransform(); Models.Tower(mb, pal, new Vector3(p0.x, terrain.GroundY(p0.x, p0.y), p0.y), 0.45f, 0.08f); }
                }
                mb.ResetTransform();
            }
        }

        // ------------------------------------------------------------------
        //  Armeen
        // ------------------------------------------------------------------
        void UpdateArmies()
        {
            var alive = new HashSet<int>();
            var pal = Palette.Current;
            foreach (var a in game.Armies)
            {
                alive.Add(a.id);
                var v = Get(armyViews, a.id, "Armee " + a.id);
                int tx = Mathf.FloorToInt(a.x), ty = Mathf.FloorToInt(a.y);
                bool visible = game.IsDiscovered(tx, ty) && (a.faction == game.PlayerId || IsSeenByPlayer(a));
                v.go.SetActive(visible);
                if (!visible) continue;
                int sig = ArmySig(a);
                if (sig != v.sig)
                {
                    v.sig = sig;
                    BuildArmy(a, pal);
                    mb.ToMesh(v.mesh);
                }
                var target = new Vector3(a.x, a.atSea ? 0.02f : terrain.GroundY(a.x, a.y), a.y);
                var tr = v.go.transform;
                var delta = target - tr.position;
                if (delta.sqrMagnitude > 4f) tr.position = target;
                else
                {
                    tr.position = Vector3.Lerp(tr.position, target, 1f - Mathf.Exp(-10f * Time.unscaledDeltaTime));
                    var flat = new Vector3(delta.x, 0, delta.z);
                    if (flat.sqrMagnitude > 0.00002f)
                        tr.rotation = Quaternion.Slerp(tr.rotation, Quaternion.LookRotation(flat), 1f - Mathf.Exp(-6f * Time.unscaledDeltaTime));
                }
                if (a.atSea) tr.position += Vector3.up * Mathf.Sin(Time.time * 2f + a.id) * 0.015f;
            }
            var remove = new List<int>();
            foreach (var kv in armyViews) if (!alive.Contains(kv.Key)) { Destroy(kv.Value.go); remove.Add(kv.Key); }
            foreach (var k in remove) armyViews.Remove(k);
        }

        bool IsSeenByPlayer(ArmyData a)
        {
            // Fremde Heere sieht man nur in der Nähe eigener Siedlungen/Heere
            foreach (var s in game.SettlementsOf(game.PlayerId))
                if ((s.x - a.x) * (s.x - a.x) + (s.y - a.y) * (s.y - a.y) < 15 * 15) return true;
            foreach (var o in game.ArmiesOf(game.PlayerId))
                if ((o.x - a.x) * (o.x - a.x) + (o.y - a.y) * (o.y - a.y) < 8 * 8) return true;
            var f = game.F(a.faction);
            return f != null && game.Status(game.PlayerId, a.faction) == DipStatus.Alliance;
        }

        int ArmySig(ArmyData a)
        {
            unchecked
            {
                int h = a.faction * 31 + (a.atSea ? 7 : 0) + a.settlers * 13 + Palette.Index * 101;
                for (int u = 0; u < Units.Count; u++) h = h * 7 + Mathf.Min(a.units[u], 6);
                return h;
            }
        }

        void BuildArmy(ArmyData a, Palette pal)
        {
            mb.Clear();
            var f = game.F(a.faction);
            var fc = f != null ? pal.FactionColor(f.color) : Color.white;
            if (a.atSea)
            {
                Models.Ship(mb, pal, fc);
                return;
            }
            if (a.settlers > 0 && a.units[0] + a.units[1] + a.units[2] + a.units[3] == 0)
            {
                Models.Cart(mb, pal);
                Models.Soldier(mb, pal, new Vector3(0.12f, 0, 0.1f), pal.Wood, 0);
                Models.Soldier(mb, pal, new Vector3(-0.12f, 0, -0.05f), pal.Roof, 0);
                Models.Flag(mb, pal, new Vector3(0, 0, -0.18f), fc, 0.3f);
                return;
            }
            // Formation: bis zu 6 Figuren, nach Truppenart
            var kinds = new List<int>();
            for (int u = 0; u < Units.Count; u++)
            {
                int n = a.units[u] == 0 ? 0 : Mathf.Clamp(Mathf.CeilToInt(a.units[u] / 6f), 1, 3);
                for (int k = 0; k < n && kinds.Count < 6; k++)
                    kinds.Add(u == (int)UType.Speer ? 1 : u == (int)UType.Bogen ? 2 : u == (int)UType.Ritter ? 3 : u == (int)UType.Ramme ? 4 : 0);
            }
            if (kinds.Count == 0) kinds.Add(0);
            var body = Color.Lerp(fc, pal.Dark, 0.3f);
            for (int k = 0; k < kinds.Count; k++)
            {
                float x = ((k % 3) - 1) * 0.11f, z = (k / 3) * -0.13f;
                Models.Soldier(mb, pal, new Vector3(x, kinds[k] == 3 ? 0.06f : 0f, z), body, kinds[k]);
            }
            Models.Flag(mb, pal, new Vector3(-0.17f, 0, 0.1f), fc, 0.45f);
        }

        void UpdatePath()
        {
            var a = game.A(SelectedArmy);
            if (a == null || a.path == null || a.pathIndex >= a.path.Count)
            {
                pathLine.positionCount = 0;
                return;
            }
            int n = a.path.Count - a.pathIndex + 1;
            pathLine.positionCount = n;
            var col = Palette.Current.FactionColor(game.F(a.faction).color);
            pathLine.startColor = col; pathLine.endColor = new Color(col.r, col.g, col.b, 1f);
            pathLine.SetPosition(0, new Vector3(a.x, terrain.GroundY(a.x, a.y) + 0.12f, a.y));
            for (int k = a.pathIndex; k < a.path.Count; k++)
            {
                float x = WorldMap.X(a.path[k]) + 0.5f, z = WorldMap.Y(a.path[k]) + 0.5f;
                pathLine.SetPosition(k - a.pathIndex + 1, new Vector3(x, terrain.GroundY(x, z) + 0.12f, z));
            }
        }

        public Vector3 SettlementWorldPos(SettlementData s) => new Vector3(s.x + 0.5f, terrain.GroundY(s.x + 0.5f, s.y + 0.5f), s.y + 0.5f);

        public Vector3 ArmyWorldPos(ArmyData a)
        {
            if (armyViews.TryGetValue(a.id, out var v) && v.go.activeSelf) return v.go.transform.position;
            return new Vector3(a.x, terrain.GroundY(a.x, a.y), a.y);
        }

        public bool ArmyVisible(int id) => armyViews.TryGetValue(id, out var v) && v.go.activeSelf;
    }
}
