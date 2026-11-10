using System.Collections.Generic;
using UnityEngine;

namespace PixelKingdom
{
    /// <summary>Siedlungen (aus ihren echten Gebäuden zusammengesetzt), Armeen, Grenzen und Marschrouten.</summary>
    public class EntityRenderer : MonoBehaviour
    {
        class View { public GameObject go, shadowGo; public Mesh mesh, shadow; public int sig = int.MinValue; public bool flip; public int dir; }

        Game game;
        Material spriteMat, shadowMat;
        ObjectRenderer objects;
        readonly Dictionary<int, View> settlementViews = new Dictionary<int, View>();
        readonly Dictionary<int, View> armyViews = new Dictionary<int, View>();
        readonly QuadBatch qb = new QuadBatch(), sb = new QuadBatch();
        Mesh borderMesh, pathMesh, poiMesh, poiShadow;
        int poiVersion = -1;
        bool bordersDirty = true;
        float refresh;
        int paletteIdx = -1;
        readonly HashSet<int> occupied = new HashSet<int>();

        public int SelectedArmy = -1;

        public void Init(Game g, Material sprite, Material shadow, ObjectRenderer obj)
        {
            game = g; spriteMat = sprite; shadowMat = shadow; objects = obj;
            foreach (Transform c in transform) Destroy(c.gameObject);
            settlementViews.Clear(); armyViews.Clear();
            QuadBatch.MakeObject("Grenzen", transform, spriteMat, out borderMesh);
            QuadBatch.MakeObject("Marschroute", transform, spriteMat, out pathMesh);
            QuadBatch.MakeObject("Ruinen & Lager", transform, spriteMat, out poiMesh);
            QuadBatch.MakeObject("Ruinen & Lager Schatten", transform, shadowMat, out poiShadow);
            poiVersion = -1;
            bordersDirty = true; refresh = 0; paletteIdx = Palette.Index;
            RefreshSettlements(true);
        }

        public void MarkBordersDirty() => bordersDirty = true;
        void BuildPois()
        {
            poiVersion = game.PoisVersion;
            qb.Clear(); sb.Clear();
            var white = new Color32(255, 255, 255, 255);
            var faded = new Color32(200, 195, 185, 255);
            foreach (var p in game.Pois)
            {
                if (p.type == 1 && p.done) continue;
                var sp = SpriteLib.Get(Game.PoiSprite(p));
                if (sp == null) continue;
                float wx = p.x + 0.5f, wy = p.y + 0.3f;
                qb.Add(sp, wx, wy, QuadBatch.DepthOf(wy), p.done ? faded : white);
                if (sp.Shadow != null) sb.Add(sp.Shadow, wx, wy, ObjectRenderer.ShadowZ, white);
            }
            qb.ToMesh(poiMesh); sb.ToMesh(poiShadow);
        }

        public void ForceRefresh() { poiVersion = -1; foreach (var v in settlementViews.Values) v.sig = int.MinValue; foreach (var v in armyViews.Values) v.sig = int.MinValue; bordersDirty = true; }

        View Get(Dictionary<int, View> dict, int id, string name)
        {
            if (dict.TryGetValue(id, out var v)) return v;
            v = new View();
            v.go = QuadBatch.MakeObject(name, transform, spriteMat, out v.mesh);
            v.shadowGo = QuadBatch.MakeObject(name + " Schatten", transform, shadowMat, out v.shadow);
            dict[id] = v;
            return v;
        }

        void Update()
        {
            if (game == null || SpriteLib.Atlas == null) return;
            if (paletteIdx != Palette.Index) { paletteIdx = Palette.Index; ForceRefresh(); }
            refresh -= Time.unscaledDeltaTime;
            if (refresh <= 0) { refresh = 0.5f; RefreshSettlements(false); }
            if (bordersDirty) BuildBorders();
            if (poiVersion != game.PoisVersion) BuildPois();
            UpdateArmies();
            BuildPath();
        }

        // ------------------------------------------------------------------
        //  Siedlungen
        // ------------------------------------------------------------------
        void RefreshSettlements(bool force)
        {
            var alive = new HashSet<int>();
            bool occChanged = false;
            foreach (var s in game.Settlements)
            {
                alive.Add(s.id);
                var v = Get(settlementViews, s.id, "Siedlung " + s.name);
                int sig = Signature(s);
                if (sig == v.sig && !force) continue;
                v.sig = sig;
                occChanged = true;
                BuildSettlement(s, v);
            }
            var remove = new List<int>();
            foreach (var kv in settlementViews)
                if (!alive.Contains(kv.Key)) { Destroy(kv.Value.go); Destroy(kv.Value.shadowGo); remove.Add(kv.Key); occChanged = true; }
            foreach (var k in remove) settlementViews.Remove(k);
            if (occChanged)
            {
                occupied.Clear();
                foreach (var s in game.Settlements) SettlementLayout.Build(game, s, game.Season, occupied);
                objects.SetOccupied(occupied);
            }
        }

        int Signature(SettlementData s)
        {
            unchecked
            {
                int h = s.faction * 7919 + SettlementLayout.CenterLevel(game, s) * 31 + (int)game.Season * 977 + Palette.Index * 13;
                for (int i = 0; i < s.buildings.Length; i++) h = h * 17 + s.buildings[i];
                foreach (var b in s.placed) h = h * 31 + b.x * 1009 + b.y * 13 + b.type;
                h = h * 13 + game.GarrisonCount(s) / 12;
                foreach (var q in s.queue) if (q.kind == 0) h = h * 29 + q.x * 997 + q.y;
                return h;
            }
        }

        void BuildSettlement(SettlementData s, View v)
        {
            qb.Clear(); sb.Clear();
            var f = game.F(s.faction);
            Color32 fc = f != null ? (Color32)Palette.FactionColor(f.color) : new Color32(255, 255, 255, 255);
            var white = new Color32(255, 255, 255, 255);
            foreach (var p in SettlementLayout.Build(game, s, game.Season, null))
            {
                var sp = SpriteLib.Get(p.Name);
                if (sp == null && p.Name.StartsWith("i")) sp = SpriteLib.Get(p.Name.Substring(1));   // kein Insel-Stil vorhanden
                if (sp == null) continue;
                float z = p.Flat ? 19.5f : QuadBatch.DepthOf(p.Y);
                qb.Add(sp, p.X, p.Y, z, white, p.Flip);
                if (sp.Overlay != null) qb.Add(sp.Overlay, p.X, p.Y, z - 0.002f, fc, p.Flip);
                if (sp.Shadow != null) sb.Add(sp.Shadow, p.X, p.Y, ObjectRenderer.ShadowZ, white);
            }
            qb.ToMesh(v.mesh); sb.ToMesh(v.shadow);
        }

        // ------------------------------------------------------------------
        //  Grenzen (gestrichelt, in Fraktionsfarbe)
        // ------------------------------------------------------------------
        void BuildBorders()
        {
            bordersDirty = false;
            qb.Clear();
            var white = SpriteLib.Get("white");
            var map = game.Map;
            const float t = 2f / 16f, inset = 1f / 16f;
            for (int y = 0; y < WorldMap.H; y++)
                for (int x = 0; x < WorldMap.W; x++)
                {
                    int i = WorldMap.Idx(x, y);
                    int o = map.owner[i];
                    if (o < 0 || !map.disc[i]) continue;
                    var f = game.F(o);
                    if (f == null) continue;
                    Color32 c = Palette.FactionColor(f.color);
                    bool L = Own(x - 1, y) != o, R = Own(x + 1, y) != o, D = Own(x, y - 1) != o, U = Own(x, y + 1) != o;
                    if (!(L || R || D || U)) continue;
                    for (int k = 0; k < 2; k++)
                    {
                        float a = k * 0.5f + 0.06f, b = a + 0.32f;
                        if (D) qb.Rect(white, x + a, y + inset, x + b, y + inset + t, 19.4f, c);
                        if (U) qb.Rect(white, x + a, y + 1 - inset - t, x + b, y + 1 - inset, 19.4f, c);
                        if (L) qb.Rect(white, x + inset, y + a, x + inset + t, y + b, 19.4f, c);
                        if (R) qb.Rect(white, x + 1 - inset - t, y + a, x + 1 - inset, y + b, 19.4f, c);
                    }
                }
            qb.ToMesh(borderMesh);
        }

        int Own(int x, int y) => WorldMap.In(x, y) ? game.Map.owner[WorldMap.Idx(x, y)] : -1;

        // ------------------------------------------------------------------
        //  Armeen
        // ------------------------------------------------------------------
        void UpdateArmies()
        {
            var alive = new HashSet<int>();
            foreach (var a in game.Armies)
            {
                alive.Add(a.id);
                var v = Get(armyViews, a.id, "Armee " + a.id);
                bool visible = game.IsDiscovered((int)a.x, (int)a.y) && (a.faction == game.PlayerId || IsSeenByPlayer(a));
                v.go.SetActive(visible); v.shadowGo.SetActive(visible);
                if (!visible) continue;
                // Marschrichtung (8 Richtungen) – die Formation dreht sich mit
                if (a.path != null && a.pathIndex < a.path.Count)
                {
                    float nx = WorldMap.X(a.path[a.pathIndex]) + 0.5f, ny = WorldMap.Y(a.path[a.pathIndex]) + 0.5f;
                    float ddx = nx - a.x, ddy = ny - a.y;
                    if (ddx * ddx + ddy * ddy > 0.0025f)
                    {
                        int d = Mathf.RoundToInt(Mathf.Atan2(ddy, ddx) / (Mathf.PI / 4f));
                        v.dir = (d + 8) % 8;
                        if (Mathf.Abs(ddx) > 0.05f) v.flip = ddx < 0;
                    }
                }
                int sig = ArmySig(a) * 16 + v.dir * 2 + (v.flip ? 1 : 0);
                if (sig != v.sig) { v.sig = sig; BuildArmy(a, v); }
                var tr = v.go.transform;
                Vector2 cur = tr.position, target = new Vector2(a.x, a.y);
                cur = (cur - target).sqrMagnitude > 9f ? target : Vector2.Lerp(cur, target, 1f - Mathf.Exp(-10f * Time.unscaledDeltaTime));
                tr.position = new Vector3(cur.x, cur.y, QuadBatch.DepthOf(cur.y));
                v.shadowGo.transform.position = new Vector3(cur.x, cur.y, ObjectRenderer.ShadowZ);
            }
            var remove = new List<int>();
            foreach (var kv in armyViews) if (!alive.Contains(kv.Key)) { Destroy(kv.Value.go); Destroy(kv.Value.shadowGo); remove.Add(kv.Key); }
            foreach (var k in remove) armyViews.Remove(k);
        }

        bool IsSeenByPlayer(ArmyData a)
        {
            foreach (var s in game.SettlementsOf(game.PlayerId))
                if ((s.x - a.x) * (s.x - a.x) + (s.y - a.y) * (s.y - a.y) < 18 * 18) return true;
            foreach (var o in game.ArmiesOf(game.PlayerId))
                if ((o.x - a.x) * (o.x - a.x) + (o.y - a.y) * (o.y - a.y) < 8 * 8) return true;
            return game.Status(game.PlayerId, a.faction) == DipStatus.Alliance;
        }

        int ArmySig(ArmyData a)
        {
            unchecked
            {
                int h = a.faction * 31 + (a.atSea ? 7 + game.ShipClass * 997 : 0) + a.settlers * 13 + Palette.Index * 101;
                for (int u = 0; u < Units.Count; u++) h = h * 1009 + a.units[u];
                return h;
            }
        }

        /// <summary>
        /// Das Heer als Formation: jeder Soldat einzeln, in engen Reihen in Marschrichtung. Vorne die Reiterei,
        /// dann Speere, Miliz, Bogenschützen und Späher, dahinter die Belagerungsgeräte und Wagen.
        /// Auf See: ein Schiff je Schiffsladung.
        /// </summary>
        void BuildArmy(ArmyData a, View v)
        {
            qb.Clear(); sb.Clear();
            var f = game.F(a.faction);
            Color32 fc = f != null ? (Color32)Palette.FactionColor(f.color) : new Color32(255, 255, 255, 255);
            var white = new Color32(255, 255, 255, 255);
            void Put(string name, float dx, float dy, bool flip)
            {
                var s = SpriteLib.Get(name);
                if (s == null) return;
                float z = -0.01f + dy * 0.04f;   // weiter hinten (Norden) = weiter weg
                qb.Add(s, dx, dy, z, white, flip);
                if (s.Overlay != null) qb.Add(s.Overlay, dx, dy, z - 0.002f, fc, flip);
                if (s.Shadow != null) sb.Add(s.Shadow, dx, dy, 0f, white);
            }
            float ang = v.dir * Mathf.PI / 4f;
            var fwd = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
            if (v.dir == 0 && v.flip) fwd = new Vector2(-1, 0);
            var side = new Vector2(-fwd.y, fwd.x);
            var pts = new List<(string name, Vector2 p)>();
            int total = a.settlers; for (int u = 0; u < Units.Count; u++) total += a.units[u];

            if (a.atSea)
            {
                // Flotte: ein Schiff je Ladung, versetzt in Reihen
                bool mine = a.faction == game.S.playerFaction;
                int cls = mine ? Mathf.Max(1, game.ShipClass) : 2;
                string ship = cls >= 3 ? "u_ship3" : cls == 2 ? "u_ship2" : "u_ship";
                int n = Mathf.Clamp(Mathf.CeilToInt(total / (float)Game.ShipCap(cls)), 1, 40);
                int rowW = Mathf.Min(5, n);
                for (int k = 0; k < n; k++)
                {
                    int row = k / rowW, col = k % rowW;
                    float lat = (col - (Mathf.Min(rowW, n - row * rowW) - 1) / 2f) * 1.15f + (row % 2) * 0.35f;
                    pts.Add((ship, -fwd * (row * 0.95f) + side * lat));
                }
            }
            else
            {
                var af = game.F(a.faction);
                string pre = af != null && af.continent >= 4 && !af.isPlayer ? "imu_" : "mu_";
                // Bei sehr großen Heeren steht eine Figur für mehrere Soldaten (höchstens 1200 Figuren)
                int per = Mathf.Max(1, Mathf.CeilToInt(total / 1200f));
                UType[] order = { UType.Ritter, UType.Speer, UType.Miliz, UType.Bogen, UType.Spaeher };
                int inf = 0; foreach (var u in order) inf += a.units[(int)u] / per;
                int files = Mathf.Clamp(Mathf.CeilToInt(Mathf.Sqrt(inf * 0.7f)), 3, 24);   // Marschkolonne: tiefer als breit
                float depth = 0f;
                foreach (var u in order)
                {
                    int n = Mathf.CeilToInt(a.units[(int)u] / (float)per);
                    if (n <= 0) continue;
                    bool cav = u == UType.Ritter || u == UType.Spaeher;
                    float lat = cav ? 0.48f : 0.32f, dep = cav ? 0.4f : 0.26f;   // sichtbarer Abstand zwischen den Soldaten
                    int w = cav ? Mathf.Max(2, Mathf.RoundToInt(files * 0.32f / 0.48f)) : files;
                    for (int k = 0; k < n; k++)
                    {
                        int rank = k / w, file = k % w;
                        int inRow = Mathf.Min(w, n - rank * w);
                        float jitter = ((k * 37) % 7 - 3) * 0.008f;
                        pts.Add((pre + u, -fwd * (depth + rank * dep + jitter) + side * ((file - (inRow - 1) / 2f) * lat)));
                    }
                    depth += Mathf.CeilToInt(n / (float)w) * dep + 0.2f;
                }
                // Belagerungsgeräte und Wagen folgen dahinter
                int rams = Mathf.CeilToInt(a.units[(int)UType.Ramme] / (float)per);
                int carts = a.settlers > 0 ? Mathf.Clamp(Mathf.CeilToInt(a.settlers / 12f), 1, 6) : 0;
                if (rams + carts > 0) depth += 0.35f;
                for (int k = 0; k < rams + carts; k++)
                {
                    int rank = k / 4, file = k % 4;
                    int inRow = Mathf.Min(4, rams + carts - rank * 4);
                    pts.Add((k < rams ? pre + UType.Ramme : "mu_cart", -fwd * (depth + rank * 0.6f) + side * ((file - (inRow - 1) / 2f) * 0.8f)));
                }
                // Banner an der Spitze
                pts.Add(("u_banner", fwd * 0.25f + side * 0.1f));
            }
            // Formation um die Heeresposition zentrieren
            Vector2 c = Vector2.zero;
            foreach (var p in pts) c += p.p;
            if (pts.Count > 0) c /= pts.Count;
            bool flipS = fwd.x < -0.1f || (Mathf.Abs(fwd.x) <= 0.1f && v.flip);
            foreach (var p in pts) Put(p.name, p.p.x - c.x, p.p.y - c.y - 0.1f, flipS);
            qb.ToMesh(v.mesh); sb.ToMesh(v.shadow);
            v.mesh.bounds = new Bounds(Vector3.zero, new Vector3(1000, 1000, 100));
            v.shadow.bounds = new Bounds(Vector3.zero, new Vector3(1000, 1000, 100));
        }

        // ------------------------------------------------------------------
        //  Marschroute der ausgewählten Armee (gepunktet)
        // ------------------------------------------------------------------
        void BuildPath()
        {
            qb.Clear();
            var a = game.A(SelectedArmy);
            if (a != null && a.path != null && a.pathIndex < a.path.Count)
            {
                var white = SpriteLib.Get("white");
                var f = game.F(a.faction);
                Color32 c = f != null ? (Color32)Palette.FactionColor(f.color) : new Color32(255, 255, 255, 255);
                Color32 dark = new Color32(30, 20, 20, 255);
                Vector2 prev = new Vector2(a.x, a.y);
                float acc = 0;
                for (int k = a.pathIndex; k < a.path.Count; k++)
                {
                    var next = new Vector2(WorldMap.X(a.path[k]) + 0.5f, WorldMap.Y(a.path[k]) + 0.5f);
                    float len = Vector2.Distance(prev, next);
                    for (float t = (0.5f - acc); t < len; t += 0.5f)
                    {
                        var p = Vector2.Lerp(prev, next, t / len);
                        qb.Rect(white, p.x - 2.5f / 16, p.y - 2.5f / 16, p.x + 2.5f / 16, p.y + 2.5f / 16, 0.6f, dark);
                        qb.Rect(white, p.x - 1.5f / 16, p.y - 1.5f / 16, p.x + 1.5f / 16, p.y + 1.5f / 16, 0.59f, c);
                    }
                    acc = (acc + len) % 0.5f;
                    prev = next;
                }
            }
            qb.ToMesh(pathMesh);
        }

        // ------------------------------------------------------------------
        public Vector3 SettlementWorldPos(SettlementData s) => new Vector3(s.x + 0.5f, s.y + 0.5f, 0);

        public Vector3 ArmyWorldPos(ArmyData a)
        {
            if (armyViews.TryGetValue(a.id, out var v) && v.go.activeSelf) return v.go.transform.position;
            return new Vector3(a.x, a.y, 0);
        }

        public bool ArmyVisible(int id) => armyViews.TryGetValue(id, out var v) && v.go.activeSelf;
    }
}
